using KetCauVXT.Import;
using KetCauVXT.Model;

namespace KetCauVXT.Mep;

/// <summary>Nhập ống / thiết bị điện nước từ Revit (RevitArchImport.MepHandler, bật bằng RevitArchImport.UseMepHandler) thành
/// ĐỐI TƯỢNG ĐIỆN NƯỚC: thiết bị đặt theo họ trong thư viện MEP (so tên họ Revit với từ khoá), ống / ống luồn dây thành tuyến
/// giữ nguyên hình học gốc (FixedPath) nối giữa các "Điểm nối ống" ở hai đầu (đầu trùng nhau ≤ 1 cm dùng chung một điểm).
/// Phần tử không nhận ra (ống gió, khay cáp, loại lạ) → khối chung bộ môn Điện nước như cách nhập cũ. Phụ kiện ống (PipeFitting)
/// bỏ qua vì chương trình tự sinh cút / tê theo tuyến.</summary>
public static class MepRevitImport
{
    /// <summary>Họ dùng cho đầu ống / điểm chia khi nhập.</summary>
    public const string NodeFamily = "Điểm nối ống";
    /// <summary>Khoảng gộp đầu ống (m).</summary>
    public const double MergeTol = 0.01;

    /// <summary>Kết quả nhập (đếm) – lần gọi gần nhất.</summary>
    public static (int Devices, int Runs, int Generic, int Skipped) Last { get; private set; }

    /// <summary>Gọi từ RevitArchImport.Build / Update. ox, oy: gốc toạ độ đã dời.</summary>
    public static void Handle(Project p, List<RvMep> items, ImportReport rep, double ox, double oy)
    {
        if (p == null || items == null) return;
        p.Mep ??= new MepModel();
        var m = p.Mep;
        // nhập lại toàn bộ (Build): bỏ phần đã nhập trước đó có cùng Id Revit
        var ids = items.Where(i => i != null && !string.IsNullOrEmpty(i.Id)).Select(i => i.Id).ToHashSet();
        m.Runs.RemoveAll(r => r.ExternalId != null && ids.Contains(r.ExternalId));
        m.Devices.RemoveAll(d => d.ExternalId != null && ids.Contains(d.ExternalId));

        int nd = 0, nr = 0, ns = 0;
        var generic = new List<RvMep>();
        foreach (var it in items.Where(i => i != null))
        {
            string cat = (it.Category ?? "").Trim().ToLowerInvariant();
            // phần tử dạng đoạn: có đường kính và hai đầu khác nhau (thiết bị Revit chỉ có điểm X1,Y1,Z1 – X2..Z2 = 0, Diameter = 0;
            // cùng quy ước với RevitArchImport.AddMepBoxes)
            bool linear = it.Diameter > 0 && Math.Abs(it.X2 - it.X1) + Math.Abs(it.Y2 - it.Y1) + Math.Abs(it.Z2 - it.Z1) > 1e-6;
            if (cat.Contains("fitting") || (cat.Contains("accessory") && cat.Contains("pipe"))) { ns++; continue; }
            if ((cat == "pipe" || cat == "pipes" || cat.Contains("pipecurve") || cat.Contains("flexpipe")) && linear)
            {
                AddRun(p, it, SystemOfPipe(it), MepRunKind.Ong, ox, oy); nr++; continue;
            }
            if ((cat == "conduit" || cat.Contains("conduit")) && linear)
            {
                AddRun(p, it, SystemOfConduit(it), MepRunKind.OngGen, ox, oy); nr++; continue;
            }
            bool noFamily = linear || cat.Contains("duct") || cat.Contains("cabletray") || cat.Contains("cable tray");
            string fam = noFamily ? null : FamilyFor(m.Library, it);
            if (fam == null) { generic.Add(it); continue; }
            AddDevice(p, it, fam, ox, oy); nd++;
        }
        int ng = generic.Count > 0 ? RevitArchImport.AddMepBoxes(p, generic, new Dictionary<string, double>(), ox, oy) : 0;
        try { MepRouting.RebuildAll(p); } catch (Exception) { }
        Last = (nd, nr, ng, ns);
        rep?.Info.Add($"Điện nước từ Revit (đối tượng MEP): {nd} thiết bị, {nr} tuyến ống/ống luồn dây, {ng} khối chung, bỏ qua {ns} phụ kiện (tự sinh)");
    }

    /// <summary>Tầng chứa cao độ z (ưu tiên tên Level Revit trùng tên tầng).</summary>
    public static string StoryOf(Project p, string level, double z)
    {
        if (!string.IsNullOrWhiteSpace(level))
        {
            var s = p.Stories.FirstOrDefault(q => string.Equals(q.Name, level.Trim(), StringComparison.OrdinalIgnoreCase));
            if (s != null) return s.Name;
        }
        // cùng quy tắc với RevitArchImport.StoryFor (tường / phòng): thấp hơn tầng thấp nhất (Level gốc không thành tầng) → "NEN"
        return p.Stories.Where(s => s.Elevation <= z + 0.05).OrderByDescending(s => s.Elevation).FirstOrDefault()?.Name ?? "NEN";
    }

    private static string Text(RvMep it) => ((it.Name ?? "") + " | " + (it.SystemName ?? "")).ToLowerInvariant();

    /// <summary>Hệ thống của ống theo tên hệ / tên loại Revit.</summary>
    public static MepSystemKind SystemOfPipe(RvMep it)
    {
        string t = Text(it);
        if (Has(t, "nóng", "hot", "dhw")) return MepSystemKind.CapNuocNong;
        if (Has(t, "mưa", "rain", "storm", "roof drain")) return MepSystemKind.ThoatNuocMua;
        if (Has(t, "xám", "grey", "gray")) return MepSystemKind.ThoatNuocXam;
        if (Has(t, "thải", "thoát", "sanitary", "waste", "soil", "drain", "vent", "upvc")) return MepSystemKind.ThoatNuocThai;
        return MepSystemKind.CapNuocLanh;
    }

    /// <summary>Hệ thống của ống luồn dây.</summary>
    public static MepSystemKind SystemOfConduit(RvMep it)
    {
        string t = Text(it);
        if (Has(t, "chiếu sáng", "light", "đèn")) return MepSystemKind.DienChieuSang;
        if (Has(t, "điều hoà", "điều hòa", "hvac", "ac ")) return MepSystemKind.DienDieuHoa;
        if (Has(t, "data", "tel", "camera", "điện nhẹ", "lan", "tv")) return MepSystemKind.DienNhe;
        return MepSystemKind.DienOCam;
    }

    /// <summary>Họ thư viện MEP tương ứng thiết bị Revit (trùng tên trước, rồi từ khoá); null = không nhận ra.</summary>
    public static string FamilyFor(MepLibrary lib, RvMep it)
    {
        if (lib == null) return null;
        var exact = lib.Find((it.Name ?? "").Trim());
        if (exact != null && !exact.IsLinear) return exact.Name;
        string cat = (it.Category ?? "").ToLowerInvariant();
        string t = Text(it);
        string pick = null;
        if (Has(t, "bồn cầu", "toilet", "water closet", "wc", "xí", "closet")) pick = "Bồn cầu";
        else if (Has(t, "lavabo", "basin", "chậu rửa mặt", "washbasin")) pick = "Lavabo";
        else if (Has(t, "sen", "shower")) pick = "Sen tắm";
        else if (Has(t, "chậu bếp", "kitchen", "bếp", "sink")) pick = "Vòi chậu bếp";
        else if (Has(t, "phễu", "thoát sàn", "floor drain", "floor waste")) pick = "Thoát sàn (phễu thu)";
        else if (Has(t, "nóng lạnh", "water heater", "heater", "bình nóng")) pick = "Bình nóng lạnh 30 L";
        else if (Has(t, "van", "valve")) pick = "Van chặn";
        else if (Has(t, "vòi", "faucet", "tap", "bib")) pick = "Vòi rửa";
        else if (Has(t, "ống đứng", "stack", "riser")) pick = "Ống đứng thoát nước";
        else if (Has(t, "tủ điện", "panel", "board", "distribution")) pick = "Tủ điện tầng";
        else if (Has(t, "aptomat", "breaker", "mcb")) pick = "Aptomat 1 pha";
        else if (Has(t, "hộp chia", "junction", "hộp nối")) pick = "Hộp chia (điểm chia)";
        else if (Has(t, "công tắc", "switch"))
            pick = Has(t, "3 hạt", "3 gang", "3-gang", "triple") ? "Công tắc 3 hạt" : Has(t, "2 hạt", "2 gang", "2-gang", "double") ? "Công tắc 2 hạt" : "Công tắc 1 hạt";
        else if (Has(t, "ổ cắm", "socket", "outlet", "receptacle", "duplex"))
            pick = Has(t, "đôi", "double", "duplex", "twin") ? "Ổ cắm đôi" : "Ổ cắm đơn";
        else if (Has(t, "quạt trần", "ceiling fan")) pick = "Quạt trần";
        else if (Has(t, "quạt", "fan")) pick = "Quạt treo tường";
        else if (Has(t, "downlight", "âm trần", "spot")) pick = "Đèn downlight";
        else if (Has(t, "tuýp", "tube", "linear", "batten", "huỳnh quang")) pick = "Đèn tuýp LED 1,2 m";
        else if (Has(t, "hắt", "wall light", "sconce")) pick = "Đèn hắt tường";
        else if (cat.Contains("lighting") || Has(t, "đèn", "light", "lamp")) pick = "Đèn ốp trần";
        else if (cat.Contains("plumbing")) pick = "Vòi rửa";
        else if (cat.Contains("electricalfixture") || cat.Contains("electrical fixture")) pick = "Ổ cắm đơn";
        return pick != null && lib.Find(pick) != null ? pick : null;
    }

    private static bool Has(string t, params string[] keys)
    {
        foreach (var k in keys)
        {
            int i = t.IndexOf(k, StringComparison.Ordinal);
            while (i >= 0)
            {
                // từ khoá ngắn (≤ 3 ký tự): phải đứng riêng (không nằm trong từ khác)
                bool okL = k.Length > 3 || i == 0 || !char.IsLetterOrDigit(t[i - 1]);
                bool okR = k.Length > 3 || i + k.Length >= t.Length || !char.IsLetterOrDigit(t[i + k.Length]);
                if (okL && okR) return true;
                i = t.IndexOf(k, i + 1, StringComparison.Ordinal);
            }
        }
        return false;
    }

    private static MepDevice AddDevice(Project p, RvMep it, string family, double ox, double oy)
    {
        var fam = p.Mep.Library.Find(family);
        double x = Math.Round(it.X1 - ox, 4), y = Math.Round(it.Y1 - oy, 4);
        string story = StoryOf(p, it.Level, it.Z1);
        double h = Math.Round(it.Z1 - MepRouting.StoryElevation(p, story), 4);
        MepDevice d = null;
        if (fam.WallHosted)
        {
            var nw = MepRouting.NearestWall(p, story, x, y, 0.3);
            if (nw.Wall != null) d = MepRouting.PlaceOnWall(p, family, nw.Wall, nw.Offset, nw.Side, h);
        }
        d ??= MepRouting.PlaceFree(p, family, story, x, y, h);
        d.ExternalId = it.Id;
        return d;
    }

    private static MepDevice Node(Project p, string story, double x, double y, double z)
    {
        var m = p.Mep;
        double ez = MepRouting.StoryElevation(p, story);
        foreach (var d in m.Devices)
        {
            if (d.Story != story) continue;
            if (Math.Abs(d.X - x) <= MergeTol && Math.Abs(d.Y - y) <= MergeTol && Math.Abs(ez + d.Height - z) <= MergeTol + 1e-9) return d;
        }
        var nd = MepRouting.PlaceFree(p, NodeFamily, story, x, y, Math.Round(z - ez, 4));
        nd.ExternalId = FormattableString.Invariant($"rv-node:{x:0.###},{y:0.###},{z:0.###}");
        return nd;
    }

    private static double SnapSize(MepLibrary lib, MepRunKind kind, double mm)
    {
        var cat = kind == MepRunKind.OngGen ? MepCategory.OngGen : MepCategory.Ong;
        var sizes = lib.OfCategory(cat).Select(f => f.Size).Where(s => s > 0).Distinct().ToList();
        if (mm <= 0) return kind == MepRunKind.OngGen ? 20 : 25;
        if (sizes.Count == 0) return Math.Round(mm);
        return sizes.OrderBy(s => Math.Abs(s - mm)).First();
    }

    private static MepRun AddRun(Project p, RvMep it, MepSystemKind sys, MepRunKind kind, double ox, double oy)
    {
        var m = p.Mep;
        double x1 = Math.Round(it.X1 - ox, 4), y1 = Math.Round(it.Y1 - oy, 4), x2 = Math.Round(it.X2 - ox, 4), y2 = Math.Round(it.Y2 - oy, 4);
        string story = StoryOf(p, it.Level, Math.Min(it.Z1, it.Z2));
        var a = Node(p, story, x1, y1, it.Z1);
        var b = Node(p, story, x2, y2, it.Z2);
        double mm = it.Diameter * 1000;
        bool drain = MepSystems.IsDrain(sys);
        var r = new MepRun
        {
            Id = m.NextRunId(), System = sys, Kind = kind, Story = story, FromId = a.Id, ToId = b.Id,
            Size = SnapSize(m.Library, kind, mm), AutoSize = false, ExternalId = it.Id, FixedPath = true,
            Material = kind == MepRunKind.OngGen ? "PVC" : drain ? "uPVC" : "PPR PN20", Conductors = kind == MepRunKind.OngGen ? 3 : 0,
            Path = new List<MepPt> { new(x1, y1, it.Z1), new(x2, y2, it.Z2) }
        };
        double lh = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        if (drain && lh > 1e-6) r.Slope = Math.Round(Math.Abs(it.Z2 - it.Z1) / lh, 4);
        m.Runs.Add(r);
        return r;
    }
}

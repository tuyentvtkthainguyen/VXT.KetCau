using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Mep;

// =====================================================================
// ẢNH HƯỞNG CỦA ĐIỆN NƯỚC TỚI KẾT CẤU – CẢNH BÁO ĐIỂM NGUY HIỂM VÀ ĐỀ XUẤT GIA CƯỜNG
// Quy tắc thực hành thiết kế (tham chiếu TCVN 5574:2018 – cấu tạo, chọc thủng; TCVN 9386:2012 – vùng tới hạn kháng chấn):
//  • Lỗ xuyên dầm (ống lồng): Ø lỗ ≤ h/3 (Ø ≤ h/4 không cần gia cường đặc biệt); mép lỗ nằm trong 1/3 giữa chiều cao dầm;
//    trong 1/3 giữa nhịp thông thuỷ; cách mép gối ≥ h (≥ 2h trong vùng tới hạn khi có kháng chấn); hai lỗ cách nhau ≥ 3Ø.
//    Ống đứng xuyên dầm theo phương đứng: không cho phép.
//  • Cột: không đục / khoan xuyên cột (CẤM); ống gen ≤ Ø25 trong cột cạnh ≥ 400: cảnh báo.
//  • Lỗ sàn: cách mép cột &lt; 2,5·hs → NGUY HIỂM (vùng chọc thủng); &lt; 6·hs → CẢNH BÁO (giảm chu vi chọc thủng);
//    lỗ &gt; 300 mm phải có thép gia cường mép lỗ thay phần thép bị cắt (As cắt chia đều 2 bên, tối thiểu 2Ø12) + thanh xiên góc;
//    ≥ 3 lỗ thẳng hàng cách nhau &lt; 1 m → đường yếu.
//  • Rãnh đục tường: tường BTCT – không đục (đặt ống chờ khi đổ); rãnh ngang sâu &gt; 1/3 lõi tường hoặc dài &gt; 1 m trong tường ≤ 110 → cảnh báo.
// =====================================================================

public enum ImpactSeverity { ChoPhep, GoiY, CanhBao, NguyHiem, Cam }

/// <summary>Một thanh thép gia cường đề xuất.</summary>
public sealed record StrengthBar(string Desc, int Count, int Dia, double Length);

/// <summary>Đề xuất gia cường (xuất được sang chi tiết kết cấu: hình DXF).</summary>
public sealed class StrengtheningItem
{
    /// <summary>"LoXuyenDam" | "LoSan"</summary>
    public string Kind { get; set; } = "";
    public string Element { get; set; } = "";
    public string Story { get; set; } = "";
    /// <summary>Lỗ xuyên dầm: Ø lỗ (mm). Lỗ sàn: cạnh lỗ (mm)</summary>
    public double HoleDia { get; set; }
    public double HoleW { get; set; }
    public double HoleL { get; set; }
    /// <summary>Kích thước cấu kiện (m): dầm b × h; sàn hs</summary>
    public double B { get; set; }
    public double H { get; set; }
    /// <summary>Tâm lỗ cách đáy dầm (m)</summary>
    public double HoleZ { get; set; }
    public List<StrengthBar> Bars { get; set; } = new();
    public string Note { get; set; } = "";
}

/// <summary>Một điểm ảnh hưởng tới kết cấu.</summary>
public sealed class StructuralImpactItem
{
    /// <summary>XUYEN_DAM, XUYEN_COT, LO_SAN, LO_SAN_THANG_HANG, RANH_TUONG, TREO_ONG</summary>
    public string Code { get; set; } = "";
    public ImpactSeverity Severity { get; set; }
    public string Story { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public int RunId { get; set; }
    public int FrameId { get; set; }
    public int SlabId { get; set; }
    public int WallId { get; set; }
    public double Dia { get; set; }
    public string Message { get; set; } = "";
    public string Rule { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Proposal { get; set; } = "";
    public StrengtheningItem Strengthening { get; set; }
    public string SeverityText => StructuralImpact.SeverityText(Severity);
    public override string ToString() => $"[{SeverityText}] {Code}: {Message}";
}

public class ImpactOptions
{
    /// <summary>Có kháng chấn: vùng tới hạn 2h quanh gối</summary>
    public bool Seismic { get; set; }
    /// <summary>Lỗ = Ø ống + giá trị này (ống lồng thép) (mm)</summary>
    public double SleeveExtra { get; set; } = 20;
    /// <summary>Thép sàn mặc định khi chưa có dữ liệu thép sàn</summary>
    public int SlabBarDia { get; set; } = 8;
    public double SlabBarSpacing { get; set; } = 0.2;
    /// <summary>Thép gia cường mép lỗ sàn</summary>
    public int TrimBarDia { get; set; } = 12;
    /// <summary>Thép dọc gia cường trên / dưới lỗ xuyên dầm</summary>
    public int BeamExtraBarDia { get; set; } = 12;
}

/// <summary>Kết quả đánh giá một lỗ xuyên dầm.</summary>
public sealed record BeamHoleResult(ImpactSeverity Severity, List<string> Reasons, string Proposal, StrengtheningItem Strengthening);

/// <summary>Kết quả đánh giá một lỗ sàn.</summary>
public sealed record SlabOpeningResult(ImpactSeverity Severity, List<string> Reasons, string Proposal, StrengtheningItem Strengthening);

public static class StructuralImpact
{
    public const string RefBeam = "TCVN 5574:2018 (cấu tạo dầm, lực cắt) – thực hành lỗ xuyên dầm; TCVN 9386:2012 (vùng tới hạn kháng chấn)";
    public const string RefSlab = "TCVN 5574:2018 (chọc thủng, cấu tạo lỗ sàn) – thực hành thiết kế";
    public const string RefColumn = "TCVN 5574:2018 – không làm giảm tiết diện cột";
    public const string RefChase = "Thực hành thi công khối xây (TCVN 4085:2011, TCVN 5573:2011) – rãnh đục tường";

    public static string SeverityText(ImpactSeverity s) => s switch
    {
        ImpactSeverity.ChoPhep => "Cho phép",
        ImpactSeverity.GoiY => "Gợi ý",
        ImpactSeverity.CanhBao => "Cảnh báo",
        ImpactSeverity.NguyHiem => "Nguy hiểm",
        _ => "Cấm"
    };

    private static ImpactSeverity Max(ImpactSeverity a, ImpactSeverity b) => (ImpactSeverity)Math.Max((int)a, (int)b);
    private static double R10(double m) => Math.Ceiling(m * 100 - 1e-9) / 100;   // làm tròn lên 10 mm
    public static double BarArea(int d) => Math.PI * d * d / 4e6;

    // ------------------------------------------------------------------
    // Lỗ xuyên dầm
    // ------------------------------------------------------------------
    /// <summary>Đánh giá lỗ tròn Ø holeMm xuyên dầm b × h (m), nhịp thông thuỷ L0, tâm lỗ cách mép gối gần nhất dSup, cách đáy dầm zFromBottom (m);
    /// otherHoleDist: khoảng cách tới lỗ khác gần nhất trên cùng dầm (m, null = không có), otherHoleMm: Ø lỗ đó.</summary>
    public static BeamHoleResult EvaluateBeamHole(double b, double h, double L0, double dSup, double zFromBottom, double holeMm,
        bool seismic = false, double? otherHoleDist = null, double otherHoleMm = 0, int stirrupDia = 8, int extraBarDia = 12)
    {
        var sev = ImpactSeverity.ChoPhep; var why = new List<string>(); var prop = new List<string>();
        double D = holeMm / 1000;
        if (D > h / 3 + 1e-9)
        {
            sev = Max(sev, ImpactSeverity.NguyHiem);
            why.Add($"Ø lỗ {holeMm:0} > h/3 = {h / 3 * 1000:0} mm");
            prop.Add($"đi ống dưới đáy dầm hoặc tăng chiều cao dầm ≥ 3·Ø = {Math.Ceiling(3 * holeMm / 50) * 50:0} mm");
        }
        else if (D > h / 4 + 1e-9)
        {
            sev = Max(sev, ImpactSeverity.CanhBao);
            why.Add($"Ø lỗ {holeMm:0} > h/4 = {h / 4 * 1000:0} mm – cần gia cường quanh lỗ");
        }
        double lo = zFromBottom - D / 2, hi = zFromBottom + D / 2;
        if (lo < h / 3 - 1e-6 || hi > 2 * h / 3 + 1e-6)
        {
            sev = Max(sev, ImpactSeverity.NguyHiem);
            why.Add($"lỗ ngoài 1/3 giữa chiều cao dầm (mép lỗ {lo * 1000:0}…{hi * 1000:0} mm, cho phép {h / 3 * 1000:0}…{2 * h / 3 * 1000:0} mm từ đáy dầm)");
            prop.Add($"đặt tim ống cách đáy dầm {h / 2 * 1000:0} mm (giữa chiều cao)");
        }
        double need = seismic ? 2 * h : h;
        if (dSup < need - 1e-6)
        {
            sev = Max(sev, ImpactSeverity.NguyHiem);
            why.Add($"cách mép gối {dSup:0.###} m < {(seismic ? "2h" : "h")} = {need:0.###} m (vùng lực cắt lớn{(seismic ? ", vùng tới hạn kháng chấn" : "")})");
            prop.Add($"dời lỗ ra xa mép gối ≥ {Math.Max(need, L0 / 3):0.##} m hoặc đổi tuyến vòng qua");
        }
        else if (dSup < L0 / 3 - 1e-6)
        {
            sev = Max(sev, ImpactSeverity.CanhBao);
            why.Add($"lỗ ngoài 1/3 giữa nhịp (cách mép gối {dSup:0.###} m < L0/3 = {L0 / 3:0.###} m) – cần đai gia cường");
        }
        if (otherHoleDist.HasValue)
        {
            double sp = 3 * Math.Max(holeMm, otherHoleMm) / 1000;
            if (otherHoleDist.Value < sp - 1e-6)
            {
                sev = Max(sev, ImpactSeverity.NguyHiem);
                why.Add($"hai lỗ cách nhau {otherHoleDist.Value:0.###} m < 3Ø = {sp:0.###} m");
                prop.Add($"giãn các lỗ ≥ {sp:0.##} m hoặc gộp chung một ống lồng lớn (kiểm tra ≤ h/3)");
            }
        }
        if (why.Count == 0) why.Add("lỗ trong vùng cho phép (1/3 giữa nhịp và chiều cao, Ø ≤ h/4)");
        // gia cường: đai dày 2 (Ø ≤ h/4) hoặc 3 đai mỗi bên lỗ, bước 50; thép dọc 2Ø12 trên + 2Ø12 dưới lỗ, dài Ø + 2·max(40d; 500)
        int nSt = D > h / 4 + 1e-9 ? 3 : 2;
        int dSt = h >= 0.6 - 1e-9 ? 10 : stirrupDia;
        double lb = R10(D + 2 * Math.Max(40 * extraBarDia / 1000.0, 0.5));
        double sw = Math.Max(0, b - 0.05), sh = Math.Max(0, h - 0.05);
        var st = new StrengtheningItem
        {
            Kind = "LoXuyenDam", HoleDia = holeMm, B = b, H = h, HoleZ = zFromBottom,
            Bars =
            {
                new StrengthBar("Đai gia cường mỗi bên lỗ, bước 50", 2 * nSt, dSt, R10(2 * (sw + sh) + 2 * Math.Max(0.075, 10 * dSt / 1000.0))),
                new StrengthBar("Thép dọc bổ sung trên lỗ", 2, extraBarDia, lb),
                new StrengthBar("Thép dọc bổ sung dưới lỗ", 2, extraBarDia, lb)
            },
            Note = $"Ống lồng thép Ø{holeMm:0} chờ sẵn khi đổ bê tông; {nSt} đai Ø{dSt}a50 mỗi bên lỗ; 2Ø{extraBarDia} trên + 2Ø{extraBarDia} dưới lỗ, L = {lb * 1000:0}"
        };
        string proposal = sev >= ImpactSeverity.NguyHiem
            ? "Không khoan lỗ tại vị trí này: " + string.Join("; ", prop) + ". Nếu bắt buộc: " + st.Note
            : sev == ImpactSeverity.CanhBao ? "Gia cường: " + st.Note : "Đặt ống lồng chờ khi đổ bê tông: " + st.Note;
        return new BeamHoleResult(sev, why, proposal, st);
    }

    // ------------------------------------------------------------------
    // Lỗ sàn
    // ------------------------------------------------------------------
    /// <summary>Thép gia cường mép lỗ (mỗi phương): thanh bị cắt n = ⌊w/s⌋ (≥ 1 khi w ≥ s… lỗ nhỏ hơn bước: 0);
    /// As cắt = n·A(d); mỗi bên lỗ bù As/2 → số thanh Ø dt = max(2; ⌈As/2 / A(dt)⌉); dài = cạnh lỗ còn lại + 2·40·dt.</summary>
    public static (int Cut, double AsCut, int PerSide, double Length) TrimmingBars(double widthAcross, double lengthAlong, int barDia, double spacing, int trimDia)
    {
        int cut = spacing > 1e-9 ? (int)Math.Floor(widthAcross / spacing + 1e-9) : 0;
        double asCut = cut * BarArea(barDia);
        int per = Math.Max(2, (int)Math.Ceiling(asCut / 2 / BarArea(trimDia) - 1e-9));
        double L = R10(lengthAlong + 2 * 40 * trimDia / 1000.0);
        return (cut, asCut, per, L);
    }

    /// <summary>Đánh giá lỗ sàn w × l (m) trong sàn dày hs, khoảng cách từ mép lỗ tới mép cột gần nhất distCol (m, null = không có cột).</summary>
    public static SlabOpeningResult EvaluateSlabOpening(double w, double l, double hs, double? distCol, int barDia = 8, double spacing = 0.2, int trimDia = 12)
    {
        var sev = ImpactSeverity.GoiY; var why = new List<string>(); var prop = new List<string>();
        if (distCol.HasValue)
        {
            if (distCol.Value < 2.5 * hs - 1e-9)
            {
                sev = Max(sev, ImpactSeverity.NguyHiem);
                why.Add($"lỗ cách mép cột {distCol.Value:0.###} m < 2,5·hs = {2.5 * hs:0.###} m – trong vùng chọc thủng");
                prop.Add($"dời lỗ ra ≥ {6 * hs:0.##} m khỏi mép cột hoặc tăng chiều dày / mũ cột, kiểm tra lại chọc thủng");
            }
            else if (distCol.Value < 6 * hs - 1e-9)
            {
                sev = Max(sev, ImpactSeverity.CanhBao);
                why.Add($"lỗ cách mép cột {distCol.Value:0.###} m < 6·hs = {6 * hs:0.###} m – làm giảm chu vi tính chọc thủng, cần kiểm tra lại");
            }
        }
        double big = Math.Max(w, l);
        StrengtheningItem st = null;
        if (big > 0.3 + 1e-9)
        {
            sev = Max(sev, ImpactSeverity.CanhBao);
            var tx = TrimmingBars(w, l, barDia, spacing, trimDia);   // thanh phương Y (song song cạnh l) bị cắt trên bề rộng w
            var ty = TrimmingBars(l, w, barDia, spacing, trimDia);
            double ld = R10(2 * 40 * trimDia / 1000.0);
            st = new StrengtheningItem
            {
                Kind = "LoSan", HoleW = w * 1000, HoleL = l * 1000, H = hs,
                Bars =
                {
                    new StrengthBar($"Mép lỗ song song cạnh {l * 1000:0} (mỗi bên, mỗi lớp)", 2 * tx.PerSide, trimDia, tx.Length),
                    new StrengthBar($"Mép lỗ song song cạnh {w * 1000:0} (mỗi bên, mỗi lớp)", 2 * ty.PerSide, trimDia, ty.Length),
                    new StrengthBar("Thanh xiên 45° tại 4 góc lỗ", 4, trimDia, ld)
                },
                Note = $"Thép bị cắt Ø{barDia}a{spacing * 1000:0}: {tx.Cut}+{ty.Cut} thanh; bù {tx.PerSide}Ø{trimDia} mỗi bên (song song cạnh {l * 1000:0}), {ty.PerSide}Ø{trimDia} mỗi bên (song song cạnh {w * 1000:0}); 1Ø{trimDia} xiên mỗi góc"
            };
            why.Add($"lỗ lớn {w * 1000:0}×{l * 1000:0} > 300 mm – cắt thép sàn, cần thép gia cường mép lỗ");
            prop.Add(st.Note);
        }
        else
        {
            why.Add($"lỗ nhỏ ≤ 300 mm – khoan / đặt ống chờ, dịch thép sàn tránh lỗ, không cắt thép");
        }
        string proposal = string.Join("; ", prop.Count > 0 ? prop : new List<string> { "Đặt ống chờ trước khi đổ bê tông, dịch thép tránh lỗ" });
        return new SlabOpeningResult(sev, why, proposal, st);
    }

    // ------------------------------------------------------------------
    // Quét toàn mô hình
    // ------------------------------------------------------------------
    private static (double B, double H) Dims(Project p, FrameElement f)
    {
        var sec = p.FrameSections.FirstOrDefault(s => s.Name == f.Section);
        return (sec?.B > 0 ? sec.B : 0.22, sec?.H > 0 ? sec.H : 0.3);
    }

    /// <summary>Nửa bề rộng cột gối tại điểm (x, y) theo phương dầm (ux, uy); 0 nếu không có cột.</summary>
    private static double SupportHalf(Project p, double x, double y, double z, double ux, double uy)
    {
        foreach (var c in p.Frames.Where(f => f.IsVerticalKind))
        {
            Node ni, nj;
            try { ni = p.Node(c.NodeI); nj = p.Node(c.NodeJ); } catch { continue; }
            if (MepGeom.Dist2D(ni.X + c.ColOffsetX, ni.Y + c.ColOffsetY, x, y) > 0.3 && MepGeom.Dist2D(ni.X, ni.Y, x, y) > 0.05) continue;
            if (z < Math.Min(ni.Z, nj.Z) - 0.05 || z > Math.Max(ni.Z, nj.Z) + 0.05) continue;
            var (b, h) = Dims(p, c);
            return Math.Abs(ux) * b / 2 + Math.Abs(uy) * h / 2;
        }
        return 0;
    }

    private static string Lbl(FrameElement f, string kind) => string.IsNullOrEmpty(f.Label) ? $"{kind} {f.Id}" : f.Label;

    /// <summary>Đường kính ống / ống gen của tuyến (mm).</summary>
    public static double RunDia(MepRun r) => r.Kind == MepRunKind.Ong ? r.Size : (r.ConduitSize > 0 ? r.ConduitSize : MepSizing.ConduitFor(r.Size, Math.Max(1, r.Conductors)));

    /// <summary>Đánh giá giao cắt ống (Ø mm, tâm (x, y, z)) với dầm f: null nếu không xuyên.</summary>
    public static BeamHoleResult EvaluateBeamCrossing(Project p, FrameElement f, double x, double y, double z, double dia, ImpactOptions o = null, double? otherDist = null, double otherDia = 0)
    {
        o ??= new ImpactOptions();
        Node ni, nj;
        try { ni = p.Node(f.NodeI); nj = p.Node(f.NodeJ); } catch { return null; }
        var (b, h) = Dims(p, f);
        double zt = Math.Max(ni.Z, nj.Z), zb = zt - h;
        double L = MepGeom.Dist2D(ni.X, ni.Y, nj.X, nj.Y);
        if (L < 1e-6) return null;
        double ux = (nj.X - ni.X) / L, uy = (nj.Y - ni.Y) / L;
        var pr = MepGeom.Project(x, y, ni.X, ni.Y, nj.X, nj.Y);
        double hi = SupportHalf(p, ni.X, ni.Y, ni.Z - 0.01, ux, uy), hj = SupportHalf(p, nj.X, nj.Y, nj.Z - 0.01, ux, uy);
        double L0 = Math.Max(0.1, L - hi - hj);
        double dSup = Math.Max(0, Math.Min(pr.T * L - hi, (1 - pr.T) * L - hj));
        return EvaluateBeamHole(b, h, L0, dSup, z - zb, dia + o.SleeveExtra, o.Seismic, otherDist, otherDia + o.SleeveExtra);
    }

    /// <summary>Quét mọi tuyến điện nước: xuyên dầm / cột, lỗ sàn (đoạn đứng xuyên sàn, ống đứng), rãnh đục tường, quang treo ống thoát.</summary>
    public static List<StructuralImpactItem> Scan(Project p, ImpactOptions o = null)
    {
        o ??= new ImpactOptions();
        var m = p.Mep; var list = new List<StructuralImpactItem>();
        if (m == null) return list;
        var beams = p.Frames.Where(f => !f.IsVerticalKind).ToList();
        var cols = p.Frames.Where(f => f.IsVerticalKind).ToList();
        var holesOnBeam = new Dictionary<int, List<(double X, double Y, double Dia, StructuralImpactItem Item)>>();

        foreach (var r in m.Runs)
        {
            var path = r.Path; if (path == null || path.Count < 2) continue;
            double dia = RunDia(r);
            // ---- dầm ----
            foreach (var f in beams)
            {
                Node ni, nj;
                try { ni = p.Node(f.NodeI); nj = p.Node(f.NodeJ); } catch { continue; }
                var (b, h) = Dims(p, f);
                double zt = Math.Max(ni.Z, nj.Z), zb = zt - h, rad = dia / 2000;
                for (int i = 1; i < path.Count; i++)
                {
                    var a = path[i - 1]; var c = path[i];
                    double planL = MepGeom.Dist2D(a.X, a.Y, c.X, c.Y);
                    StructuralImpactItem it = null;
                    if (planL < 1e-6)
                    {
                        // đoạn đứng: nằm trong bề rộng dầm và chiều cao chồng lấn
                        var pr = MepGeom.Project(a.X, a.Y, ni.X, ni.Y, nj.X, nj.Y);
                        if (pr.Dist > b / 2 + rad - 1e-9 || pr.T <= 1e-6 || pr.T >= 1 - 1e-6) continue;
                        if (Math.Max(a.Z, c.Z) <= zb + 1e-6 || Math.Min(a.Z, c.Z) >= zt - 1e-6) continue;
                        it = new StructuralImpactItem
                        {
                            Code = "XUYEN_DAM", Severity = ImpactSeverity.NguyHiem, Story = r.Story, X = a.X, Y = a.Y, Z = zb, RunId = r.Id, FrameId = f.Id, Dia = dia,
                            Message = $"Tuyến {r.Id} ({MepSystems.Name(r.System)}) Ø{dia:0} đi ĐỨNG xuyên qua dầm {Lbl(f, "Dầm")} – cắt thép dọc, không cho phép",
                            Rule = "Không khoan lỗ đứng xuyên dầm", Reference = RefBeam,
                            Proposal = $"Dời ống đứng ra khỏi bề rộng dầm (cách mép dầm ≥ {Math.Max(0.05, rad + 0.03):0.##} m)"
                        };
                    }
                    else
                    {
                        if (!MepGeom.SegmentsIntersect(a.X, a.Y, c.X, c.Y, ni.X, ni.Y, nj.X, nj.Y)) continue;
                        // điểm giao trên mặt bằng → cao độ ống tại đó
                        double t = SegParam(a.X, a.Y, c.X, c.Y, ni.X, ni.Y, nj.X, nj.Y);
                        double x = a.X + t * (c.X - a.X), y = a.Y + t * (c.Y - a.Y), z = a.Z + t * (c.Z - a.Z);
                        if (z + rad <= zb + 1e-6 || z - rad >= zt - 1e-6) continue;
                        double? other = null; double otherDia = 0;
                        if (holesOnBeam.TryGetValue(f.Id, out var hs) && hs.Count > 0)
                        {
                            var nh = hs.OrderBy(q => MepGeom.Dist2D(q.X, q.Y, x, y)).First();
                            other = MepGeom.Dist2D(nh.X, nh.Y, x, y); otherDia = nh.Dia;
                        }
                        var ev = EvaluateBeamCrossing(p, f, x, y, z, dia, o, other, otherDia);
                        if (ev == null) continue;
                        ev.Strengthening.Element = Lbl(f, "Dầm"); ev.Strengthening.Story = r.Story;
                        it = new StructuralImpactItem
                        {
                            Code = "XUYEN_DAM", Severity = ev.Severity, Story = r.Story, X = x, Y = y, Z = z, RunId = r.Id, FrameId = f.Id, Dia = dia,
                            Message = $"Tuyến {r.Id} ({MepSystems.Name(r.System)}) Ø{dia:0} xuyên dầm {Lbl(f, "Dầm")} {b * 1000:0}×{h * 1000:0}: " + string.Join("; ", ev.Reasons),
                            Rule = "Ø ≤ h/3; trong 1/3 giữa chiều cao và nhịp; cách gối ≥ h (2h kháng chấn); lỗ cách lỗ ≥ 3Ø", Reference = RefBeam,
                            Proposal = ev.Proposal, Strengthening = ev.Strengthening
                        };
                        if (!holesOnBeam.TryGetValue(f.Id, out var lst)) holesOnBeam[f.Id] = lst = new();
                        lst.Add((x, y, dia, it));
                    }
                    list.Add(it);
                    break;   // mỗi tuyến – mỗi dầm một mục
                }
            }
            // ---- cột ----
            foreach (var f in cols)
            {
                Node ni, nj;
                try { ni = p.Node(f.NodeI); nj = p.Node(f.NodeJ); } catch { continue; }
                var (b, h) = Dims(p, f);
                double z0 = Math.Min(ni.Z, nj.Z), z1 = Math.Max(ni.Z, nj.Z);
                double cx = ni.X + f.ColOffsetX, cy = ni.Y + f.ColOffsetY;
                double rx1 = cx - b / 2, rx2 = cx + b / 2, ry1 = cy - h / 2, ry2 = cy + h / 2;
                for (int i = 1; i < path.Count; i++)
                {
                    var a = path[i - 1]; var c = path[i];
                    if (Math.Max(a.Z, c.Z) < z0 || Math.Min(a.Z, c.Z) > z1) continue;
                    bool hit = MepGeom.SegmentHitsRect(a.X, a.Y, c.X, c.Y, rx1, ry1, rx2, ry2)
                        || (MepGeom.Dist2D(a.X, a.Y, c.X, c.Y) < 1e-9 && a.X > rx1 && a.X < rx2 && a.Y > ry1 && a.Y < ry2);
                    if (!hit) continue;
                    bool small = r.Kind != MepRunKind.Ong && dia <= 25 + 1e-9 && Math.Min(b, h) >= 0.4 - 1e-9;
                    list.Add(new StructuralImpactItem
                    {
                        Code = "XUYEN_COT", Severity = small ? ImpactSeverity.CanhBao : ImpactSeverity.Cam, Story = r.Story, X = cx, Y = cy, Z = (a.Z + c.Z) / 2,
                        RunId = r.Id, FrameId = f.Id, Dia = dia,
                        Message = $"Tuyến {r.Id} ({MepSystems.Name(r.System)}) Ø{dia:0} đi qua cột {Lbl(f, "Cột")} {b * 1000:0}×{h * 1000:0}" +
                                  (small ? " – ống gen nhỏ trong cột lớn: chỉ đặt ống chờ khi đổ, không đục sau" : " – CẤM đục / khoan cột"),
                        Rule = "Không làm giảm tiết diện cột", Reference = RefColumn,
                        Proposal = "Đổi tuyến vòng qua cột (đi theo tường / trần), không xuyên cột"
                    });
                    break;
                }
            }
            // ---- rãnh tường ----
            for (int i = 1; i < path.Count; i++)
            {
                var a = path[i - 1]; var c = path[i];
                if (c.WallId <= 0) continue;
                double planL = MepGeom.Dist2D(a.X, a.Y, c.X, c.Y);
                if (planL < 1e-6) continue;   // rãnh đứng: cho phép
                var w = p.Walls.FirstOrDefault(x => x.Id == c.WallId);
                if (w == null) continue;
                var info = MepRouting.WallInfo(p, w);
                double depth = dia / 1000 + 0.01;
                bool rc = IsConcreteWall(p, w);
                ImpactSeverity sev; string why;
                if (rc) { sev = ImpactSeverity.NguyHiem; why = "tường BTCT – không đục rãnh sau khi đổ"; }
                else if (depth > info.Core / 3 + 1e-9) { sev = ImpactSeverity.CanhBao; why = $"rãnh ngang sâu {depth * 1000:0} mm > 1/3 lõi tường ({info.Core / 3 * 1000:0} mm)"; }
                else if (info.Core <= 0.11 + 1e-9 && planL > 1.0 + 1e-9) { sev = ImpactSeverity.CanhBao; why = $"rãnh ngang dài {planL:0.##} m > 1 m trong tường {info.Core * 1000:0}"; }
                else continue;
                list.Add(new StructuralImpactItem
                {
                    Code = "RANH_TUONG", Severity = sev, Story = r.Story, X = (a.X + c.X) / 2, Y = (a.Y + c.Y) / 2, Z = c.Z, RunId = r.Id, WallId = w.Id, Dia = dia,
                    Message = $"Tuyến {r.Id} Ø{dia:0}: {why} (tường {w.Id})", Rule = "Rãnh ngang ≤ 1/3 lõi, ngắn; không đục tường BTCT", Reference = RefChase,
                    Proposal = rc ? "Đặt ống chờ trước khi đổ bê tông tường" : "Đi rãnh đứng / dưới sàn – trần; hoặc đi ống nổi trong hộp kỹ thuật"
                });
            }
            // ---- quang treo ống thoát ----
            int nh2 = MepDrainage.Hangers(p, r);
            if (nh2 > 0)
            {
                var q0 = path[Math.Min(1, path.Count - 1)];
                list.Add(new StructuralImpactItem
                {
                    Code = "TREO_ONG", Severity = ImpactSeverity.GoiY, Story = r.Story, X = q0.X, Y = q0.Y, Z = q0.Z, RunId = r.Id, Dia = dia,
                    Message = $"Tuyến thoát {r.Id} Ø{dia:0} treo dưới sàn: {nh2} quang treo, khoảng cách ≤ {MepDrainage.HangerSpacing(r.Size):0.#} m",
                    Rule = "Ty ren + nở đóng vào đáy sàn; không khoan vào đáy dầm trong vùng 1/4 nhịp sát gối", Reference = RefSlab,
                    Proposal = "Bắt quang treo vào đáy sàn giữa ô sàn, tránh vùng thép chịu lực đáy dầm"
                });
            }
        }

        // ---- lỗ sàn: đoạn đứng xuyên sàn + ống đứng ----
        var pens = new List<(string Story, double X, double Y, double Dia, int RunId, int DeviceId)>();
        foreach (var r in m.Runs)
        {
            var path = r.Path; if (path == null) continue;
            for (int i = 1; i < path.Count; i++)
            {
                var a = path[i - 1]; var c = path[i];
                if (MepGeom.Dist2D(a.X, a.Y, c.X, c.Y) > 1e-6) continue;
                double zl = Math.Min(a.Z, c.Z), zh = Math.Max(a.Z, c.Z);
                foreach (var st in p.Stories)
                {
                    double t = MepDrainage.SlabThicknessAt(p, st.Name, a.X, a.Y);
                    if (!p.Slabs.Any(s => s.Story == st.Name)) continue;
                    // đi qua sàn: đoạn đứng phủ cả mặt trên và đáy sàn
                    if (zl < st.Elevation - t + 1e-6 && zh > st.Elevation - 1e-6)
                    {
                        if (!pens.Any(q => q.Story == st.Name && q.RunId == r.Id && MepGeom.Dist2D(q.X, q.Y, a.X, a.Y) < 1e-6))
                            pens.Add((st.Name, a.X, a.Y, RunDia(r), r.Id, 0));
                    }
                }
            }
        }
        foreach (var d in m.Devices.Where(d => MepDrainage.IsStack(m, d)))
        {
            if (!p.Slabs.Any(s => s.Story == d.Story)) continue;
            double ds = m.FamilyOf(d)?.Size > 0 ? m.FamilyOf(d).Size : 110;
            pens.Add((d.Story, d.X, d.Y, ds, 0, d.Id));
        }
        foreach (var pe in pens)
        {
            double hs = MepDrainage.SlabThicknessAt(p, pe.Story, pe.X, pe.Y);
            double el = MepRouting.StoryElevation(p, pe.Story);
            double side = (pe.Dia + o.SleeveExtra) / 1000;
            double? dc = null;
            foreach (var c in cols)
            {
                Node ni, nj;
                try { ni = p.Node(c.NodeI); nj = p.Node(c.NodeJ); } catch { continue; }
                if (el < Math.Min(ni.Z, nj.Z) - 0.05 || el > Math.Max(ni.Z, nj.Z) + 0.05) continue;
                var (b, h) = Dims(p, c);
                double cx = ni.X + c.ColOffsetX, cy = ni.Y + c.ColOffsetY;
                double dx = Math.Max(0, Math.Abs(pe.X - cx) - b / 2 - side / 2), dy = Math.Max(0, Math.Abs(pe.Y - cy) - h / 2 - side / 2);
                double dd = Math.Sqrt(dx * dx + dy * dy);
                if (!dc.HasValue || dd < dc.Value) dc = dd;
            }
            var ev = EvaluateSlabOpening(side, side, hs, dc, o.SlabBarDia, o.SlabBarSpacing, o.TrimBarDia);
            if (ev.Severity < ImpactSeverity.CanhBao) continue;
            var sl = MepDrainage.SlabAt(p, pe.Story, pe.X, pe.Y);
            if (ev.Strengthening != null) { ev.Strengthening.Element = sl?.Label ?? "Sàn"; ev.Strengthening.Story = pe.Story; }
            list.Add(new StructuralImpactItem
            {
                Code = "LO_SAN", Severity = ev.Severity, Story = pe.Story, X = pe.X, Y = pe.Y, Z = el, RunId = pe.RunId, SlabId = sl?.Id ?? 0, Dia = pe.Dia,
                Message = $"Lỗ sàn Ø{pe.Dia + o.SleeveExtra:0} ({(pe.DeviceId > 0 ? "ống đứng" : "tuyến " + pe.RunId)}) sàn {sl?.Label ?? pe.Story} dày {hs * 1000:0}: " + string.Join("; ", ev.Reasons),
                Rule = "Lỗ cách mép cột ≥ 6·hs; lỗ > 300 mm có thép gia cường mép lỗ", Reference = RefSlab,
                Proposal = ev.Proposal, Strengthening = ev.Strengthening
            });
        }
        // ≥ 3 lỗ thẳng hàng cách nhau < 1 m
        foreach (var g in pens.GroupBy(q => q.Story))
        {
            var pts = g.GroupBy(q => (Math.Round(q.X, 3), Math.Round(q.Y, 3))).Select(q => q.First()).ToList();
            var flagged = new HashSet<int>();
            for (int i = 0; i < pts.Count; i++)
                for (int j = i + 1; j < pts.Count; j++)
                {
                    if (MepGeom.Dist2D(pts[i].X, pts[i].Y, pts[j].X, pts[j].Y) >= 1.0) continue;
                    var line = pts.Where(q => Collinear(pts[i].X, pts[i].Y, pts[j].X, pts[j].Y, q.X, q.Y)).OrderBy(q => q.X).ThenBy(q => q.Y).ToList();
                    if (line.Count < 3) continue;
                    bool chained = true;
                    for (int k = 1; k < line.Count; k++) if (MepGeom.Dist2D(line[k - 1].X, line[k - 1].Y, line[k].X, line[k].Y) >= 1.0) chained = false;
                    if (!chained) continue;
                    int key = line.Count * 1000 + (int)(line[0].X * 10) + (int)(line[0].Y * 100);
                    if (!flagged.Add(key)) continue;
                    list.Add(new StructuralImpactItem
                    {
                        Code = "LO_SAN_THANG_HANG", Severity = ImpactSeverity.CanhBao, Story = g.Key, X = line[0].X, Y = line[0].Y,
                        Message = $"{line.Count} lỗ sàn thẳng hàng cách nhau < 1 m tại {g.Key} – tạo đường yếu cắt ngang dải thép sàn",
                        Rule = "Tránh nhiều lỗ thẳng hàng sát nhau", Reference = RefSlab,
                        Proposal = "So le các lỗ hoặc gom vào một hộp kỹ thuật có thép gia cường mép lỗ"
                    });
                }
        }
        return list;
    }

    private static bool Collinear(double x1, double y1, double x2, double y2, double x, double y)
    {
        double L = MepGeom.Dist2D(x1, y1, x2, y2);
        if (L < 1e-9) return false;
        return Math.Abs((x2 - x1) * (y - y1) - (y2 - y1) * (x - x1)) / L < 0.02;
    }

    /// <summary>Tham số t trên đoạn AB tại giao với đoạn CD (giả thiết cắt nhau).</summary>
    private static double SegParam(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy)
    {
        double rX = bx - ax, rY = by - ay, sX = dx - cx, sY = dy - cy;
        double den = rX * sY - rY * sX;
        if (Math.Abs(den) < 1e-12) return 0.5;
        return Math.Clamp(((cx - ax) * sY - (cy - ay) * sX) / den, 0, 1);
    }

    /// <summary>Tường BTCT? (tên kiểu chứa "BTCT" / "bê tông", hoặc lớp lõi vật liệu bê tông).</summary>
    public static bool IsConcreteWall(Project p, WallLine w)
    {
        var t = p.WallTypes.FirstOrDefault(x => x.Name == w.Type);
        string n = (t?.Name ?? w.Type ?? "").ToLowerInvariant();
        if (n.Contains("btct") || n.Contains("bê tông") || n.Contains("be tong")) return true;
        if (t?.IsLayered == true && t.Layers.Any(l => l.IsCore && (l.Material ?? "").ToLowerInvariant().Contains("bê tông") && !(l.Material ?? "").ToLowerInvariant().Contains("khí")))
            return true;
        return false;
    }

    // ------------------------------------------------------------------
    // Chuyển thành cảnh báo điện nước
    // ------------------------------------------------------------------
    public static List<MepWarning> ToWarnings(IEnumerable<StructuralImpactItem> items) => items.Select(it => new MepWarning
    {
        Code = it.Code, Severity = it.SeverityText, RunId = it.RunId, WallId = it.WallId, FrameId = it.FrameId, SlabId = it.SlabId,
        Story = it.Story, X = it.X, Y = it.Y,
        Message = $"[{it.SeverityText}] {it.Message}", Fix = it.Proposal
    }).ToList();

    // ------------------------------------------------------------------
    // Chi tiết gia cường (DXF)
    // ------------------------------------------------------------------
    public const string BeamDetailTitle = "CHI TIẾT GIA CƯỜNG LỖ XUYÊN DẦM";
    public const string SlabDetailTitle = "CHI TIẾT GIA CƯỜNG MÉP LỖ SÀN";

    /// <summary>Hình chi tiết gia cường: một hình cho mỗi (dầm, Ø lỗ) và mỗi (sàn, kích thước lỗ) khác nhau.</summary>
    public static List<Figure> Figures(IEnumerable<StructuralImpactItem> items)
    {
        var figs = new List<Figure>();
        foreach (var g in items.Where(i => i.Strengthening != null && i.Severity < ImpactSeverity.Cam).Select(i => i.Strengthening)
                     .GroupBy(s => (s.Kind, s.Element, s.HoleDia, s.HoleW, s.HoleL)))
        {
            var s = g.First();
            figs.Add(s.Kind == "LoXuyenDam" ? BeamFigure(s, g.Count()) : SlabFigure(s, g.Count()));
        }
        return figs;
    }

    private static string N(double v) => v.ToString("0", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Mặt đứng đoạn dầm quanh lỗ (tỷ lệ 1/20): ống lồng, đai gia cường mỗi bên, thép dọc bổ sung trên / dưới lỗ.</summary>
    public static Figure BeamFigure(StrengtheningItem s, int count = 1)
    {
        var w = new DxfWriter(20);
        double th = w.Th, h = s.H * 1000, D = s.HoleDia, zc = s.HoleZ * 1000;
        var extra = s.Bars.FirstOrDefault(b => b.Desc.Contains("trên"));
        var st = s.Bars.FirstOrDefault(b => b.Desc.StartsWith("Đai"));
        double lb = (extra?.Length ?? 1.0) * 1000;
        double half = Math.Max(lb / 2 + 200, 800);
        // dầm
        w.Polyline(Lay.BeTong, new List<(double, double)> { (-half, 0), (half, 0), (half, h), (-half, h) }, true);
        w.Line(Lay.KichThuoc, -half, h / 3, half, h / 3, "DASHED");
        w.Line(Lay.KichThuoc, -half, 2 * h / 3, half, 2 * h / 3, "DASHED");
        // lỗ + ống lồng
        w.Circle(Lay.BeTong, 0, zc, D / 2);
        w.Circle(Lay.Thep, 0, zc, D / 2 + 3);
        // thép dọc chạy suốt (tượng trưng) + thép bổ sung
        w.Line(Lay.Thep, -half, 40, half, 40); w.Line(Lay.Thep, -half, h - 40, half, h - 40);
        double ya = zc + D / 2 + 40, yb = zc - D / 2 - 40;
        w.Line(Lay.Thep, -lb / 2, ya, lb / 2, ya); w.Line(Lay.Thep, -lb / 2, yb, lb / 2, yb);
        // đai gia cường
        int nSide = st != null ? Math.Max(1, st.Count / 2) : 2;
        for (int k = 0; k < nSide; k++)
            foreach (int sg in new[] { -1, 1 })
            {
                double x = sg * (D / 2 + 50 + k * 50);
                w.Line(Lay.Dai, x, 25, x, h - 25);
            }
        w.Dim(-D / 2, -0.12 * h, D / 2, -0.12 * h, 0, 1, "Ø" + N(D));
        w.Dim(-lb / 2, -0.3 * h, lb / 2, -0.3 * h, 0);
        w.Text(Lay.Chu, half + 2 * th, h * 0.9, th, $"{(extra?.Count ?? 2)}Ø{(extra?.Dia ?? 12)} trên lỗ + {(extra?.Count ?? 2)}Ø{(extra?.Dia ?? 12)} dưới lỗ, L={N(lb)}");
        if (st != null) w.Text(Lay.Chu, half + 2 * th, h * 0.6, th, $"{nSide} đai Ø{st.Dia}a50 mỗi bên lỗ");
        w.Text(Lay.Chu, half + 2 * th, h * 0.3, th, $"Ống lồng thép Ø{N(D)} chờ sẵn");
        w.Text(Lay.Chu, half + 2 * th, h * 0.05, 0.8 * th, "Vùng cho phép: 1/3 giữa chiều cao (nét đứt)");
        string title = $"{BeamDetailTitle} {s.Element} Ø{N(D)}" + (count > 1 ? $" (SL={count})" : "");
        w.Text(Lay.Chu, 0, -0.3 * h - 4 * th, 1.4 * th, title, 0, 1, 0);
        w.Text(Lay.Chu, 0, -0.3 * h - 6 * th, 0.9 * th, "TL: 1/20", 0, 1, 0);
        return new Figure(w, title);
    }

    /// <summary>Mặt bằng lỗ sàn (tỷ lệ 1/20): thép gia cường 4 mép + thanh xiên 4 góc.</summary>
    public static Figure SlabFigure(StrengtheningItem s, int count = 1)
    {
        var w = new DxfWriter(20);
        double th = w.Th, a = s.HoleW / 2, b = s.HoleL / 2;
        var bx = s.Bars.Count > 0 ? s.Bars[0] : new StrengthBar("", 4, 12, 1.2);
        var by = s.Bars.Count > 1 ? s.Bars[1] : bx;
        var dg = s.Bars.Count > 2 ? s.Bars[2] : new StrengthBar("", 4, 12, 0.96);
        double lx = bx.Length * 1000, ly = by.Length * 1000, ld = dg.Length * 1000;
        w.Polyline(Lay.BeTong, new List<(double, double)> { (-a, -b), (a, -b), (a, b), (-a, b) }, true);
        w.Line(Lay.BeTong, -a, -b, a, b); w.Line(Lay.BeTong, -a, b, a, -b);
        // thanh song song cạnh l (phương Y) hai bên trái / phải
        foreach (int sg in new[] { -1, 1 })
        {
            w.Line(Lay.Thep, sg * (a + 50), -lx / 2, sg * (a + 50), lx / 2);
            w.Line(Lay.Thep, -ly / 2, sg * (b + 50), ly / 2, sg * (b + 50));
        }
        double dd = ld / 2 / Math.Sqrt(2);
        foreach (var (sx, sy) in new[] { (1, 1), (-1, 1), (-1, -1), (1, -1) })
        {
            // thanh xiên vuông góc đường phân giác góc lỗ
            double cx = sx * (a + 100), cy = sy * (b + 100);
            w.Line(Lay.Thep, cx + sy * dd, cy - sx * dd, cx - sy * dd, cy + sx * dd);
        }
        w.Dim(-a, -b - 0.6 * lx / 2, a, -b - 0.6 * lx / 2, 0);
        w.Dim(a + 0.6 * ly / 2, -b, a + 0.6 * ly / 2, b, 0);
        double tx = Math.Max(lx, ly) / 2 + 3 * th;
        w.Text(Lay.Chu, tx, b, th, $"{bx.Count / 2}Ø{bx.Dia} mỗi bên L={N(lx)} (mỗi lớp)");
        w.Text(Lay.Chu, tx, b - 2 * th, th, $"{by.Count / 2}Ø{by.Dia} mỗi bên L={N(ly)} (mỗi lớp)");
        w.Text(Lay.Chu, tx, b - 4 * th, th, $"1Ø{dg.Dia} xiên 45° mỗi góc L={N(ld)}");
        if (!string.IsNullOrEmpty(s.Note)) w.Text(Lay.Chu, tx, b - 6 * th, 0.8 * th, s.Note);
        string title = $"{SlabDetailTitle} {s.Element} {N(s.HoleW)}×{N(s.HoleL)}" + (count > 1 ? $" (SL={count})" : "");
        double yb = -Math.Max(lx, ly) / 2 - 4 * th;
        w.Text(Lay.Chu, 0, yb, 1.4 * th, title, 0, 1, 0);
        w.Text(Lay.Chu, 0, yb - 2 * th, 0.9 * th, "TL: 1/20", 0, 1, 0);
        return new Figure(w, title);
    }
}

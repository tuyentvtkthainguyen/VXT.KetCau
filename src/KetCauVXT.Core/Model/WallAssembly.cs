using System.Globalization;
using System.Text.Json.Serialization;

namespace KetCauVXT.Model;

// =====================================================================
// KIỂU TƯỜNG NHIỀU LỚP (kiểu Revit "Edit Assembly"), THƯ VIỆN VẬT LIỆU, KIỂU CỬA và CỬA GẮN VÀO TƯỜNG (hosted openings).
//   • WallType (Walls.cs) có thêm Function + Layers: tải 1 m² = Σ γ·t các lớp; chiều dày lõi = lớp "Lõi kết cấu".
//     Kiểu tường cũ (chỉ Thickness/UnitWeight/Finish) vẫn dùng được; WallAssembly.ToLayered đổi sang nhiều lớp cùng tải.
//   • WallLine.Openings: cửa đi / cửa sổ / ô thoáng gắn vào tường theo khoảng cách dọc tường → tường tự cắt ô cửa
//     (mặt bằng, khối lượng), tải tường trừ đúng diện tích cửa, gợi ý lanh tô.
//   • Kích thước tạm (temporary dimensions) của cửa tới đầu tường / cửa bên cạnh, đặt lại khoảng cách như Revit.
//   • Nối góc L / T của tường trên mặt bằng.
// =====================================================================

/// <summary>Chức năng một lớp tường.</summary>
public static class WallLayerFn
{
    public const string Core = "Lõi kết cấu", Plaster = "Lớp vữa trát", Stone = "Ốp đá", Tile = "Ốp gạch men",
        Paint = "Sơn bả", Insulation = "Cách nhiệt", Finish = "Lớp hoàn thiện";
    public static readonly string[] All = { Core, Plaster, Stone, Tile, Paint, Insulation, Finish };
}

/// <summary>Chức năng tường (kiểu Revit: Function).</summary>
public static class WallFunctionName
{
    public const string Exterior = "Tường ngoài", Interior = "Tường trong", Foundation = "Tường móng", Parapet = "Lan can / tường chắn mái";
    public static readonly string[] All = { Exterior, Interior, Foundation, Parapet };
}

/// <summary>Vật liệu tường: tên, trọng lượng riêng γ (kN/m³), mẫu tô mặt cắt.</summary>
public class WallMaterial
{
    public string Name { get; set; } = "";
    public double UnitWeight { get; set; } = 18;
    /// <summary>Mẫu tô trên mặt cắt: "gach", "be-tong", "vua", "da", "men", "son", "cach-nhiet"</summary>
    public string Hatch { get; set; } = "gach";
    public override string ToString() => Name;
}

/// <summary>Thư viện vật liệu tường mặc định (người dùng sửa γ trong hộp "Vật liệu…").</summary>
public static class WallMaterials
{
    public const string SolidBrick = "Gạch đặc", HollowBrick2 = "Gạch rỗng 2 lỗ", HollowBrick6 = "Gạch rỗng 6 lỗ", Aac = "Gạch bê tông khí AAC",
        Concrete = "Bê tông cốt thép", Mortar = "Vữa xi măng M75", Granite = "Đá granite", Ceramic = "Gạch ốp ceramic", Paint = "Sơn bả matit",
        Insulation = "Bông khoáng cách nhiệt", CementBlock = "Gạch bê tông (block)";

    public static List<WallMaterial> Defaults() => new()
    {
        new() { Name = SolidBrick, UnitWeight = 18, Hatch = "gach" },
        new() { Name = HollowBrick2, UnitWeight = 15, Hatch = "gach" },
        new() { Name = HollowBrick6, UnitWeight = 13.5, Hatch = "gach" },
        new() { Name = CementBlock, UnitWeight = 20, Hatch = "gach" },
        new() { Name = Aac, UnitWeight = 8, Hatch = "gach" },
        new() { Name = Concrete, UnitWeight = 25, Hatch = "be-tong" },
        new() { Name = Mortar, UnitWeight = 18, Hatch = "vua" },
        new() { Name = Granite, UnitWeight = 28, Hatch = "da" },
        new() { Name = Ceramic, UnitWeight = 20, Hatch = "men" },
        new() { Name = Paint, UnitWeight = 12, Hatch = "son" },
        new() { Name = Insulation, UnitWeight = 1.5, Hatch = "cach-nhiet" },
    };

    public static WallMaterial Find(IEnumerable<WallMaterial> lib, string name) =>
        (lib ?? Defaults()).FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Một lớp của kiểu tường (thứ tự từ mặt NGOÀI vào mặt TRONG).</summary>
public class WallLayer
{
    public string Function { get; set; } = WallLayerFn.Core;
    public string Material { get; set; } = WallMaterials.SolidBrick;
    /// <summary>Chiều dày (m)</summary>
    public double Thickness { get; set; } = 0.11;
    /// <summary>Trọng lượng riêng (kN/m³)</summary>
    public double UnitWeight { get; set; } = 18;
    [JsonIgnore] public bool IsCore => Function == WallLayerFn.Core;
    /// <summary>Tải 1 m² của lớp (kN/m²)</summary>
    [JsonIgnore] public double WeightPerM2 => Math.Max(0, Thickness) * Math.Max(0, UnitWeight);
    public WallLayer Clone() => new() { Function = Function, Material = Material, Thickness = Thickness, UnitWeight = UnitWeight };
}

/// <summary>Loại ô mở trong tường.</summary>
public enum WallOpeningKind { Door, Window, Vent, Hole }

/// <summary>Kiểu cửa (họ cửa kiểu Revit): "D1 900x2200", cửa sổ "S1 1200x1400"…</summary>
public class OpeningType
{
    public string Name { get; set; } = "";
    public WallOpeningKind Kind { get; set; } = WallOpeningKind.Door;
    /// <summary>Bề rộng thông thuỷ ô cửa (m)</summary>
    public double Width { get; set; } = 0.9;
    /// <summary>Chiều cao ô cửa (m)</summary>
    public double Height { get; set; } = 2.2;
    /// <summary>Cao độ bậu cửa so với sàn (m) – cửa đi 0</summary>
    public double Sill { get; set; }
    /// <summary>Cách mở: "1 cánh", "2 cánh", "Trượt", "Cố định"</summary>
    public string Operation { get; set; } = "1 cánh";
    /// <summary>Chiều dày khuôn cửa (m) – thể hiện mặt bằng</summary>
    public double Frame { get; set; } = 0.05;
    /// <summary>Ký hiệu thẻ (D1, S1…) – lấy chữ đầu của tên</summary>
    [JsonIgnore] public string Tag => (Name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
    [JsonIgnore] public string KindText => Kind switch
    {
        WallOpeningKind.Window => "Cửa sổ", WallOpeningKind.Vent => "Ô thoáng", WallOpeningKind.Hole => "Lỗ chờ", _ => "Cửa đi"
    };
    public override string ToString() => Name;

    public static List<OpeningType> Defaults() => new()
    {
        new() { Name = "D1 900x2200", Kind = WallOpeningKind.Door, Width = 0.9, Height = 2.2, Operation = "1 cánh" },
        new() { Name = "D2 800x2200", Kind = WallOpeningKind.Door, Width = 0.8, Height = 2.2, Operation = "1 cánh" },
        new() { Name = "D3 700x2100", Kind = WallOpeningKind.Door, Width = 0.7, Height = 2.1, Operation = "1 cánh" },
        new() { Name = "D4 1400x2400", Kind = WallOpeningKind.Door, Width = 1.4, Height = 2.4, Operation = "2 cánh" },
        new() { Name = "D5 2400x2400", Kind = WallOpeningKind.Door, Width = 2.4, Height = 2.4, Operation = "Trượt" },
        new() { Name = "S1 1200x1400", Kind = WallOpeningKind.Window, Width = 1.2, Height = 1.4, Sill = 0.9, Operation = "2 cánh" },
        new() { Name = "S2 1600x1600", Kind = WallOpeningKind.Window, Width = 1.6, Height = 1.6, Sill = 0.8, Operation = "Trượt" },
        new() { Name = "S3 600x600", Kind = WallOpeningKind.Window, Width = 0.6, Height = 0.6, Sill = 1.6, Operation = "1 cánh" },
        new() { Name = "OT1 600x400", Kind = WallOpeningKind.Vent, Width = 0.6, Height = 0.4, Sill = 2.0, Operation = "Cố định" },
    };
}

/// <summary>Cửa gắn vào tường (hosted): vị trí = khoảng cách TÂM cửa tới đầu tường (X1,Y1) dọc tường.</summary>
public class WallOpening
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    /// <summary>Khoảng cách tâm cửa tới đầu tường (m)</summary>
    public double Offset { get; set; }
    /// <summary>Lật hướng mở (phía mở cửa sang mặt kia của tường) – phím Space khi đặt</summary>
    public bool Flip { get; set; }
    /// <summary>Đổi bên bản lề (trái ↔ phải)</summary>
    public bool FlipHand { get; set; }
}

/// <summary>Gợi ý lanh tô trên ô cửa (liên kết chế độ Triển khai hồ sơ – lanh tô).</summary>
public sealed class LintelProposal
{
    public int WallId { get; init; }
    public int OpeningId { get; init; }
    public string Story { get; init; } = "";
    public string OpeningType { get; init; } = "";
    /// <summary>Bề rộng ô cửa L0 (m)</summary>
    public double Span { get; init; }
    /// <summary>Chiều dày tường (m) – bề rộng lanh tô</summary>
    public double WallThickness { get; init; }
    /// <summary>Gối mỗi bên (m)</summary>
    public double Bearing { get; init; }
    /// <summary>Chiều cao lanh tô đề xuất (m)</summary>
    public double Depth { get; init; }
    /// <summary>Chiều dài lanh tô = L0 + 2·gối (m)</summary>
    public double Length => Span + 2 * Bearing;
    public string Note { get; init; } = "";
}

/// <summary>Kích thước tạm của cửa đang chọn / đang đặt: khoảng hở tới mép đầu tường hoặc cửa bên cạnh.</summary>
public readonly record struct OpeningTempDims(double Left, double Right, double LeftRef, double RightRef);

/// <summary>Thao tác kiểu tường nhiều lớp, cửa gắn tường, nối góc.</summary>
public static class WallAssembly
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Cao độ mặt phẳng cắt mặt bằng (m trên sàn) – ô mở cắt qua mặt phẳng này mới thể hiện cắt tường trên mặt bằng.</summary>
    public const double PlanCutHeight = 1.2;
    /// <summary>Khoảng cách tối thiểu từ mép cửa tới đầu tường (m)</summary>
    public const double MinEndClear = 0.05;
    /// <summary>Khe hở tối thiểu giữa 2 cửa cạnh nhau (m)</summary>
    public const double MinGap = 0.10;

    // ------------------------------------------------------------------
    // Kiểu tường nhiều lớp
    // ------------------------------------------------------------------

    /// <summary>Tạo kiểu tường nhiều lớp (ngoài → trong).</summary>
    public static WallType Make(string name, string function, params WallLayer[] layers)
    {
        var t = new WallType { Name = name, Function = function, Layers = layers.Select(l => l.Clone()).ToList() };
        t.SyncFromLayers();
        return t;
    }

    static WallLayer L(string fn, string mat, double tMm, double gamma) => new() { Function = fn, Material = mat, Thickness = tMm / 1000.0, UnitWeight = gamma };
    static WallLayer Plaster(double mm = 15) => L(WallLayerFn.Plaster, WallMaterials.Mortar, mm, 18);

    /// <summary>Thư viện kiểu tường nhiều lớp mặc định (thêm vào dự án, không thay kiểu cũ).</summary>
    public static List<WallType> Library() => new()
    {
        Make("Gạch đặc 110 trát 2 mặt", WallFunctionName.Interior, Plaster(), L(WallLayerFn.Core, WallMaterials.SolidBrick, 110, 18), Plaster()),
        Make("Gạch đặc 220 trát 2 mặt", WallFunctionName.Exterior, Plaster(), L(WallLayerFn.Core, WallMaterials.SolidBrick, 220, 18), Plaster()),
        Make("Gạch rỗng 110 trát 2 mặt", WallFunctionName.Interior, Plaster(), L(WallLayerFn.Core, WallMaterials.HollowBrick2, 110, 15), Plaster()),
        Make("Gạch rỗng 220 trát 2 mặt", WallFunctionName.Exterior, Plaster(), L(WallLayerFn.Core, WallMaterials.HollowBrick2, 220, 15), Plaster()),
        Make("Gạch AAC 100 trát 2 mặt", WallFunctionName.Interior, Plaster(10), L(WallLayerFn.Core, WallMaterials.Aac, 100, 8), Plaster(10)),
        Make("Gạch AAC 200 trát 2 mặt", WallFunctionName.Exterior, Plaster(10), L(WallLayerFn.Core, WallMaterials.Aac, 200, 8), Plaster(10)),
        Make("Tường 220 ốp đá granite mặt ngoài", WallFunctionName.Exterior,
            L(WallLayerFn.Stone, WallMaterials.Granite, 20, 28), L(WallLayerFn.Plaster, WallMaterials.Mortar, 20, 18),
            L(WallLayerFn.Core, WallMaterials.SolidBrick, 220, 18), Plaster()),
        Make("Tường 110 ốp gạch men WC 1 mặt", WallFunctionName.Interior,
            Plaster(), L(WallLayerFn.Core, WallMaterials.SolidBrick, 110, 18), L(WallLayerFn.Plaster, WallMaterials.Mortar, 15, 18),
            L(WallLayerFn.Tile, WallMaterials.Ceramic, 8, 20)),
        Make("Tường BTCT 200", WallFunctionName.Exterior, L(WallLayerFn.Core, WallMaterials.Concrete, 200, 25)),
        Make("Tường chắn mái 110 trát 2 mặt", WallFunctionName.Parapet, Plaster(), L(WallLayerFn.Core, WallMaterials.SolidBrick, 110, 18), Plaster()),
    };

    /// <summary>Đổi kiểu tường cũ (Thickness, UnitWeight, Finish) sang nhiều lớp – GIỮ NGUYÊN tên và tải 1 m².</summary>
    public static WallType ToLayered(WallType old)
    {
        if (old == null) return null;
        if (old.IsLayered) return old;
        var layers = new List<WallLayer>();
        WallLayer P() => new() { Function = WallLayerFn.Plaster, Material = WallMaterials.Mortar, Thickness = old.PlasterThickness, UnitWeight = old.PlasterUnitWeight };
        if (old.PlasterSides >= 1) layers.Add(P());
        string mat = old.UnitWeight >= 24 ? WallMaterials.Concrete : old.UnitWeight <= 10 ? WallMaterials.Aac : old.UnitWeight < 17 ? WallMaterials.HollowBrick2 : WallMaterials.SolidBrick;
        layers.Add(new WallLayer { Function = WallLayerFn.Core, Material = mat, Thickness = old.Thickness, UnitWeight = old.UnitWeight });
        if (old.PlasterSides >= 2) layers.Add(P());
        old.Layers = layers;
        if (string.IsNullOrEmpty(old.Function)) old.Function = old.Thickness >= 0.2 ? WallFunctionName.Exterior : WallFunctionName.Interior;
        old.SyncFromLayers();
        return old;
    }

    /// <summary>Sửa chiều dày / γ LÕI của kiểu tường (bảng "Loại tường" cũ) – giữ các lớp hoàn thiện (vữa, ốp đá…).</summary>
    public static void SetCore(WallType t, double? thickness, double? unitWeight)
    {
        if (t == null) return;
        if (!t.IsLayered)
        {
            if (thickness is double th) t.Thickness = th;
            if (unitWeight is double g) t.UnitWeight = g;
            return;
        }
        var core = t.Layers.Where(l => l.IsCore).ToList();
        if (core.Count == 0) { core = new List<WallLayer> { new() { Function = WallLayerFn.Core, Material = WallMaterials.SolidBrick, Thickness = t.Thickness, UnitWeight = t.UnitWeight } }; t.Layers.Insert(t.Layers.Count / 2, core[0]); }
        if (thickness is double th2)
        {
            double sum = core.Sum(l => l.Thickness);
            foreach (var l in core) l.Thickness = sum > 1e-9 ? l.Thickness / sum * th2 : th2 / core.Count;
        }
        if (unitWeight is double g2) foreach (var l in core) l.UnitWeight = g2;
        t.SyncFromLayers();
    }

    /// <summary>Dựng lại các lớp từ thông số cũ (trát, dày trát) – dùng khi sửa kiểu hoàn thiện ở bảng "Loại tường" cũ.</summary>
    public static void RebuildFromLegacy(WallType t)
    {
        if (t == null || !t.IsLayered) return;
        var extra = t.Layers.Where(l => !l.IsCore && l.Function != WallLayerFn.Plaster).Select(l => l.Clone()).ToList();
        t.Layers = new List<WallLayer>();
        ToLayered(t);
        // giữ lớp ốp đá / gạch men… ở mặt ngoài
        t.Layers.InsertRange(0, extra);
        t.SyncFromLayers();
    }

    /// <summary>Nâng cấp dự án: kiểu tường cũ → nhiều lớp (cùng tải), bổ sung thư viện kiểu tường, vật liệu, kiểu cửa còn thiếu.
    /// Trả về số kiểu tường đã thêm.</summary>
    public static int Upgrade(Project p)
    {
        if (p == null) return 0;
        p.WallTypes ??= new List<WallType>();
        foreach (var t in p.WallTypes) ToLayered(t);
        int added = 0;
        foreach (var t in Library())
            if (p.WallTypes.All(q => q.Name != t.Name)) { p.WallTypes.Add(t); added++; }
        p.WallMaterials ??= new List<WallMaterial>();
        foreach (var m in WallMaterials.Defaults())
            if (p.WallMaterials.All(q => q.Name != m.Name)) p.WallMaterials.Add(m);
        p.OpeningTypes ??= new List<OpeningType>();
        foreach (var o in OpeningType.Defaults())
            if (p.OpeningTypes.All(q => q.Name != o.Name)) p.OpeningTypes.Add(o);
        foreach (var w in p.Walls) w.Openings ??= new List<WallOpening>();
        return added;
    }

    /// <summary>Cập nhật γ các lớp theo thư viện vật liệu (sau khi người dùng sửa γ).</summary>
    public static void ApplyMaterials(Project p)
    {
        if (p?.WallTypes == null) return;
        foreach (var t in p.WallTypes.Where(t => t.IsLayered))
        {
            foreach (var l in t.Layers)
                if (WallMaterials.Find(p.WallMaterials, l.Material) is WallMaterial m) l.UnitWeight = m.UnitWeight;
            t.SyncFromLayers();
        }
    }

    /// <summary>Mô tả ngắn kiểu tường: "15 + 220 + 15 = 250 mm · 4,50 kN/m²".</summary>
    public static string Describe(WallType t)
    {
        if (t == null) return "";
        string w = t.WeightPerM2.ToString("0.00", Inv).Replace('.', ',');
        if (!t.IsLayered) return $"{t.Thickness * 1000:0} mm · {w} kN/m²";
        string parts = string.Join(" + ", t.Layers.Select(l => (l.Thickness * 1000).ToString("0", Inv)));
        return $"{parts} = {t.TotalThickness * 1000:0} mm · {w} kN/m²";
    }

    // ------------------------------------------------------------------
    // Cửa gắn tường
    // ------------------------------------------------------------------

    public static OpeningType TypeOf(Project p, WallOpening o) =>
        p?.OpeningTypes?.FirstOrDefault(t => t.Name == o?.Type) ?? OpeningType.Defaults().FirstOrDefault(t => t.Name == o?.Type);

    /// <summary>Chiếu điểm (x, y) lên tim tường: khoảng cách dọc tường từ đầu (m, chặn trong [0, L]) và khoảng cách vuông góc (m).</summary>
    public static double ProjectPoint(WallLine w, double x, double y, out double dist)
    {
        double dx = w.X2 - w.X1, dy = w.Y2 - w.Y1, L = Math.Sqrt(dx * dx + dy * dy);
        if (L < 1e-9) { dist = Math.Sqrt((x - w.X1) * (x - w.X1) + (y - w.Y1) * (y - w.Y1)); return 0; }
        double ux = dx / L, uy = dy / L;
        double t = (x - w.X1) * ux + (y - w.Y1) * uy;
        double tc = Math.Clamp(t, 0, L);
        double px = w.X1 + ux * tc, py = w.Y1 + uy * tc;
        dist = Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
        return tc;
    }

    /// <summary>Điểm trên tim tường cách đầu tường một khoảng t (m).</summary>
    public static (double X, double Y) PointAt(WallLine w, double t)
    {
        double dx = w.X2 - w.X1, dy = w.Y2 - w.Y1, L = Math.Sqrt(dx * dx + dy * dy);
        if (L < 1e-9) return (w.X1, w.Y1);
        return (w.X1 + dx / L * t, w.Y1 + dy / L * t);
    }

    /// <summary>Chặn tâm cửa để cửa nằm trọn trong tường, cách đầu tường ≥ MinEndClear. NaN nếu tường quá ngắn.</summary>
    public static double ClampOffset(WallLine w, double width, double offset)
    {
        double L = w.Chord;
        double lo = MinEndClear + width / 2, hi = L - MinEndClear - width / 2;
        if (hi < lo - 1e-9) return double.NaN;
        return Math.Clamp(offset, lo, hi);
    }

    /// <summary>Đoạn chiếm chỗ [a, b] (m dọc tường) của cửa.</summary>
    public static (double A, double B) Span(Project p, WallOpening o)
    {
        double wdt = TypeOf(p, o)?.Width ?? 0.9;
        return (o.Offset - wdt / 2, o.Offset + wdt / 2);
    }

    /// <summary>Cửa có chồng lên cửa khác trên cùng tường (khe &lt; MinGap)?</summary>
    public static bool Overlaps(Project p, WallLine w, double width, double offset, int ignoreId = -1)
    {
        double a = offset - width / 2, b = offset + width / 2;
        foreach (var o in w.Openings ?? new List<WallOpening>())
        {
            if (o.Id == ignoreId) continue;
            var (oa, ob) = Span(p, o);
            if (a < ob + MinGap - 1e-9 && b > oa - MinGap + 1e-9) return true;
        }
        return false;
    }

    /// <summary>Tìm vị trí hợp lệ gần offset nhất (chặn trong tường, không chồng cửa khác). NaN nếu không còn chỗ.</summary>
    public static double FreeOffset(Project p, WallLine w, double width, double offset, int ignoreId = -1)
    {
        double c = ClampOffset(w, width, offset);
        if (double.IsNaN(c)) return double.NaN;
        if (!Overlaps(p, w, width, c, ignoreId)) return c;
        // thử các vị trí sát mép các cửa khác
        var cands = new List<double>();
        foreach (var o in w.Openings ?? new List<WallOpening>())
        {
            if (o.Id == ignoreId) continue;
            var (oa, ob) = Span(p, o);
            cands.Add(oa - MinGap - width / 2);
            cands.Add(ob + MinGap + width / 2);
        }
        double best = double.NaN, bd = double.MaxValue;
        foreach (double k in cands)
        {
            double kc = ClampOffset(w, width, k);
            if (double.IsNaN(kc) || Math.Abs(kc - k) > 1e-6 || Overlaps(p, w, width, kc, ignoreId)) continue;
            double d = Math.Abs(kc - offset);
            if (d < bd) { bd = d; best = kc; }
        }
        return best;
    }

    /// <summary>Đặt cửa lên tường (tự cắt tường). offset = tâm cửa tính từ đầu tường; tự dời tới chỗ trống gần nhất.</summary>
    public static EditResult PlaceOpening(Project p, WallLine w, string type, double offset, bool flip = false, bool flipHand = false)
    {
        if (p == null || w == null) return EditResult.Fail("Chưa chọn tường để đặt cửa.");
        var t = p.OpeningTypes?.FirstOrDefault(q => q.Name == type);
        if (t == null) return EditResult.Fail($"Không có kiểu cửa \"{type}\".");
        if (w.IsCurved) return EditResult.Fail("Chưa hỗ trợ đặt cửa trên tường cong.");
        w.Openings ??= new List<WallOpening>();
        double off = FreeOffset(p, w, t.Width, offset);
        if (double.IsNaN(off)) return EditResult.Fail($"Tường dài {w.Chord:0.00} m không đủ chỗ cho cửa {t.Name} (rộng {t.Width:0.00} m).");
        int id = p.Walls.SelectMany(q => q.Openings ?? new List<WallOpening>()).Select(q => q.Id).DefaultIfEmpty(0).Max() + 1;
        w.Openings.Add(new WallOpening { Id = id, Type = t.Name, Offset = Math.Round(off, 4), Flip = flip, FlipHand = flipHand });
        return new EditResult { Ok = true, Message = $"Đã đặt {t.KindText.ToLowerInvariant()} {t.Name} lên tường #{w.Id} (tâm cách đầu tường {off:0.00} m) – tường tự cắt ô cửa, tải tường trừ diện tích cửa." };
    }

    /// <summary>Dời cửa tới vị trí mới trên cùng tường (giữ hợp lệ).</summary>
    public static EditResult MoveOpening(Project p, WallLine w, WallOpening o, double offset)
    {
        var t = TypeOf(p, o);
        if (t == null) return EditResult.Fail("Không rõ kiểu cửa.");
        double off = FreeOffset(p, w, t.Width, offset, o.Id);
        if (double.IsNaN(off)) return EditResult.Fail("Không có chỗ trống cho cửa tại vị trí này.");
        o.Offset = Math.Round(off, 4);
        return new EditResult { Ok = true, Message = $"Đã dời cửa {t.Name} (tâm cách đầu tường {off:0.00} m)." };
    }

    public static EditResult RemoveOpening(WallLine w, WallOpening o)
    {
        if (w?.Openings == null || o == null || !w.Openings.Remove(o)) return EditResult.Fail("Không tìm thấy cửa để xoá.");
        return new EditResult { Ok = true, Message = $"Đã xoá cửa {o.Type} – tường liền lại." };
    }

    /// <summary>Kích thước tạm: khoảng hở từ mép trái cửa tới mép đầu tường / cửa bên trái, và từ mép phải tới cuối tường / cửa bên phải.</summary>
    public static OpeningTempDims TempDims(Project p, WallLine w, double width, double offset, int ignoreId = -1)
    {
        double a = offset - width / 2, b = offset + width / 2, L = w.Chord;
        double leftRef = 0, rightRef = L;
        foreach (var o in w.Openings ?? new List<WallOpening>())
        {
            if (o.Id == ignoreId) continue;
            var (oa, ob) = Span(p, o);
            if (ob <= a + 1e-9 && ob > leftRef) leftRef = ob;
            if (oa >= b - 1e-9 && oa < rightRef) rightRef = oa;
        }
        return new OpeningTempDims(a - leftRef, rightRef - b, leftRef, rightRef);
    }

    /// <summary>Đặt lại kích thước tạm (như sửa số trên kích thước tạm của Revit): left = true → khoảng hở bên trái = value.</summary>
    public static EditResult SetTempDim(Project p, WallLine w, WallOpening o, bool left, double value)
    {
        var t = TypeOf(p, o);
        if (t == null) return EditResult.Fail("Không rõ kiểu cửa.");
        if (value < 0) return EditResult.Fail("Khoảng cách phải ≥ 0.");
        var d = TempDims(p, w, t.Width, o.Offset, o.Id);
        double target = left ? d.LeftRef + value + t.Width / 2 : d.RightRef - value - t.Width / 2;
        return MoveOpening(p, w, o, target);
    }

    /// <summary>Ô mở có cắt qua mặt phẳng cắt mặt bằng (thể hiện ô cửa trên mặt bằng)?</summary>
    public static bool CutsPlan(OpeningType t) => t != null && t.Sill < PlanCutHeight && t.Sill + t.Height > PlanCutHeight;

    /// <summary>Các đoạn tường ĐẶC (m dọc tường) sau khi trừ ô cửa cắt qua mặt phẳng mặt bằng.</summary>
    public static List<(double A, double B)> SolidSegments(Project p, WallLine w, bool planOnly = true)
    {
        double L = w.IsCurved ? w.Length : w.Chord;
        var cuts = (w.Openings ?? new List<WallOpening>())
            .Where(o => !planOnly || CutsPlan(TypeOf(p, o)))
            .Select(o => Span(p, o)).Select(s => (A: Math.Max(0, s.A), B: Math.Min(L, s.B))).Where(s => s.B > s.A)
            .OrderBy(s => s.A).ToList();
        var res = new List<(double, double)>();
        double cur = 0;
        foreach (var (a, b) in cuts)
        {
            if (a > cur + 1e-6) res.Add((cur, a));
            cur = Math.Max(cur, b);
        }
        if (L > cur + 1e-6) res.Add((cur, L));
        return res;
    }

    /// <summary>Đa giác mặt bằng (4 góc, m) của các đoạn tường đặc; ext0 / ext1 = kéo dài đầu / cuối (nối góc).</summary>
    public static List<(double X, double Y)[]> PlanPolygons(Project p, WallLine w, double thickness, double ext0 = 0, double ext1 = 0)
    {
        var res = new List<(double X, double Y)[]>();
        double dx = w.X2 - w.X1, dy = w.Y2 - w.Y1, L = Math.Sqrt(dx * dx + dy * dy);
        if (L < 1e-9) return res;
        double ux = dx / L, uy = dy / L, nx = -uy, ny = ux, h = thickness / 2;
        var segs = SolidSegments(p, w);
        for (int i = 0; i < segs.Count; i++)
        {
            double a = segs[i].A, b = segs[i].B;
            if (a < 1e-6) a -= ext0;
            if (b > L - 1e-6) b += ext1;
            (double, double) Pt(double t, double s) => (w.X1 + ux * t + nx * s, w.Y1 + uy * t + ny * s);
            res.Add(new[] { Pt(a, h), Pt(b, h), Pt(b, -h), Pt(a, -h) });
        }
        return res;
    }

    /// <summary>Diện tích ô mở (m²) trên tường chiều cao H (phần cửa vượt quá H bị cắt).</summary>
    public static double HostedOpeningArea(Project p, WallLine w, double H)
    {
        double s = 0;
        foreach (var o in w.Openings ?? new List<WallOpening>())
        {
            var t = TypeOf(p, o);
            if (t == null) continue;
            double h = Math.Max(0, Math.Min(t.Sill + t.Height, H) - Math.Max(t.Sill, 0));
            s += t.Width * h;
        }
        return s;
    }

    /// <summary>Diện tích cửa dùng tính tải / khối lượng: cửa gắn tường (nếu có) → Σ diện tích thực; không thì theo
    /// diện tích nhập tay của đoạn tường hoặc tỷ lệ cửa của kiểu tường.</summary>
    public static double EffectiveOpenArea(Project p, WallLine w, WallType wt, double grossArea, double H)
    {
        if (w.Openings is { Count: > 0 }) return HostedOpeningArea(p, w, H);
        return w.OpeningArea >= 0 ? w.OpeningArea : (wt?.OpeningRatio ?? 0) * grossArea;
    }

    /// <summary>Tải tường lên dầm (kN/m) = g·H·(1 − tỷ lệ cửa), tỷ lệ cửa ≤ 0,9.</summary>
    public static double LineLoad(Project p, WallLine w, WallType wt, double H)
    {
        if (wt == null || H <= 0) return 0;
        double L = w.Length, area = L * H;
        double ratio = area > 1e-9 ? Math.Clamp(EffectiveOpenArea(p, w, wt, area, H) / area, 0, 0.9) : 0;
        return wt.WeightPerM2 * H * (1 - ratio);
    }

    /// <summary>Gợi ý lanh tô cho mọi cửa gắn tường: gối mỗi bên ≥ 200 mm (≥ 250 mm khi L0 &gt; 1,5 m),
    /// h ≥ max(L0/10; 150 mm), ≥ 200 mm khi L0 &gt; 1,5 m (tường 110), làm tròn 50 mm.</summary>
    public static List<LintelProposal> ProposeLintels(Project p)
    {
        var res = new List<LintelProposal>();
        if (p?.Walls == null) return res;
        foreach (var w in p.Walls)
        {
            var wt = p.WallTypes.FirstOrDefault(t => t.Name == w.Type);
            double tw = wt?.Thickness ?? 0.11;
            foreach (var o in w.Openings ?? new List<WallOpening>())
            {
                var t = TypeOf(p, o);
                if (t == null || t.Kind == WallOpeningKind.Hole) continue;
                double L0 = t.Width;
                double bearing = L0 > 1.5 ? 0.25 : 0.20;
                double h = Math.Max(L0 / 10, 0.15);
                if (tw <= 0.12 && L0 > 1.5) h = Math.Max(h, 0.20);
                h = Math.Ceiling(h / 0.05 - 1e-9) * 0.05;
                string note = tw <= 0.12 && L0 > 1.8 ? "Cửa rộng trên tường 110: nên bổ trụ BTCT 110×220 hai bên hoặc làm dầm BTCT." : "";
                res.Add(new LintelProposal
                {
                    WallId = w.Id, OpeningId = o.Id, Story = w.Story, OpeningType = t.Name, Span = L0, WallThickness = tw,
                    Bearing = bearing, Depth = Math.Round(h, 3), Note = note
                });
            }
        }
        return res;
    }

    // ------------------------------------------------------------------
    // Nối góc tường trên mặt bằng (L / T)
    // ------------------------------------------------------------------

    /// <summary>Độ kéo dài đầu / cuối mỗi tường (m) để nối góc gọn: góc L (hai đầu trùng nhau, không thẳng hàng) → kéo dài
    /// nửa chiều dày tường kia; nối T (đầu tường chạm thân tường kia) → kéo tới tim tường kia (0) – phần chồng được tô chung.</summary>
    public static Dictionary<int, (double Start, double End)> Joins(IList<WallLine> walls, Func<WallLine, double> thickness, double tol = 0.02)
    {
        var res = new Dictionary<int, (double Start, double End)>();
        foreach (var w in walls) res[w.Id] = (0.0, 0.0);
        foreach (var w in walls)
        {
            if (w.IsCurved) continue;
            double e0 = 0, e1 = 0;
            foreach (var o in walls)
            {
                if (ReferenceEquals(o, w) || o.IsCurved) continue;
                double half = thickness(o) / 2;
                foreach (int end in new[] { 0, 1 })
                {
                    double ex = end == 0 ? w.X1 : w.X2, ey = end == 0 ? w.Y1 : w.Y2;
                    bool atEnd = Near(ex, ey, o.X1, o.Y1, tol) || Near(ex, ey, o.X2, o.Y2, tol);
                    if (atEnd)
                    {
                        if (Parallel(w, o)) continue;   // thẳng hàng – nối thẳng, không kéo dài
                        if (end == 0) e0 = Math.Max(e0, half); else e1 = Math.Max(e1, half);
                    }
                }
            }
            res[w.Id] = (e0, e1);
        }
        return res;
    }

    static bool Near(double x1, double y1, double x2, double y2, double tol) => Math.Abs(x1 - x2) <= tol && Math.Abs(y1 - y2) <= tol;

    static bool Parallel(WallLine a, WallLine b)
    {
        double ax = a.X2 - a.X1, ay = a.Y2 - a.Y1, bx = b.X2 - b.X1, by = b.Y2 - b.Y1;
        double la = Math.Sqrt(ax * ax + ay * ay), lb = Math.Sqrt(bx * bx + by * by);
        if (la < 1e-9 || lb < 1e-9) return true;
        return Math.Abs((ax * by - ay * bx) / (la * lb)) < 0.02;
    }

    // ------------------------------------------------------------------
    // Kích thước tạm khi vẽ
    // ------------------------------------------------------------------

    /// <summary>Chiều dài (mm, làm tròn 1) và góc (độ, 0…360) của đoạn đang vẽ – hiển thị kích thước tạm.</summary>
    public static (double LengthMm, double AngleDeg) DrawDims(double x0, double y0, double x1, double y1)
    {
        double dx = x1 - x0, dy = y1 - y0;
        double len = Math.Round(Math.Sqrt(dx * dx + dy * dy) * 1000);
        double ang = Math.Atan2(dy, dx) * 180 / Math.PI;
        if (ang < 0) ang += 360;
        return (len, Math.Round(ang, 1));
    }

    /// <summary>Điểm cuối theo chiều dài gõ vào (m) dọc hướng từ (x0,y0) tới con trỏ (xc,yc).</summary>
    public static (double X, double Y) AlongCursor(double x0, double y0, double xc, double yc, double length)
    {
        double dx = xc - x0, dy = yc - y0, L = Math.Sqrt(dx * dx + dy * dy);
        if (L < 1e-9) return (x0 + length, y0);
        return (x0 + dx / L * length, y0 + dy / L * length);
    }

    /// <summary>Đường định vị tường: dời tim tường từ đường vẽ theo phía (đường vẽ là mặt hoàn thiện ngoài / trong, mặt lõi…).</summary>
    public static double LocationOffset(WallType t, string location)
    {
        if (t == null) return 0;
        double total = t.TotalThickness;
        double coreStart = 0, coreEnd = total;
        if (t.IsLayered && t.Layers.Any(l => l.IsCore))
        {
            double acc = 0; bool seen = false;
            foreach (var l in t.Layers)
            {
                if (l.IsCore && !seen) { coreStart = acc; seen = true; }
                acc += l.Thickness;
                if (l.IsCore) coreEnd = acc;
            }
        }
        // khoảng cách từ mặt NGOÀI tới đường vẽ → tim tường lệch so với đường vẽ (dương = về phía trong / bên phải hướng vẽ)
        double fromExt = location switch
        {
            LocationLines.FinishExterior => 0,
            LocationLines.FinishInterior => total,
            LocationLines.CoreExterior => coreStart,
            LocationLines.CoreInterior => coreEnd,
            LocationLines.CoreCenter => (coreStart + coreEnd) / 2,
            _ => total / 2
        };
        return total / 2 - fromExt;
    }
}

/// <summary>Đường định vị khi vẽ tường (Revit: Location Line).</summary>
public static class LocationLines
{
    public const string WallCenter = "Tim tường", CoreCenter = "Tim lõi kết cấu", FinishExterior = "Mặt hoàn thiện ngoài",
        FinishInterior = "Mặt hoàn thiện trong", CoreExterior = "Mặt lõi ngoài", CoreInterior = "Mặt lõi trong";
    public static readonly string[] All = { WallCenter, CoreCenter, FinishExterior, FinishInterior, CoreExterior, CoreInterior };
}

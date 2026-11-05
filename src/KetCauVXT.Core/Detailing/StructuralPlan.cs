using System.Globalization;
using System.Text.Json.Serialization;

namespace KetCauVXT.Detailing;

// =====================================================================
// MẶT BẰNG KẾT CẤU của chế độ Triển khai hồ sơ: lưới trục, các mặt bằng (Móng, Tầng 2, …, Mái) và vị trí đặt cấu kiện.
// Mỗi vị trí (PlanItem) tham chiếu một cấu kiện trong hồ sơ theo TÊN (D1, C1, M1, S1, LT1, CT1…): một cấu kiện có thể đặt nhiều lần
// → số lượng (SL) đối chiếu với số lần đặt. Vẽ vị trí chưa gán cấu kiện → chương trình tạo cấu kiện mới, suy kích thước từ vị trí
// (nhịp dầm theo các trục cắt qua, ô sàn theo hình chữ nhật, lanh tô theo bề rộng ô cửa) – logic từ mặt bằng tới chi tiết cấu kiện.
// Toạ độ m, gốc tại giao trục đầu tiên (trục 1 – trục A).
// =====================================================================

/// <summary>Loại vị trí đặt trên mặt bằng.</summary>
public static class PlanKinds
{
    public const string Column = "Cot", Footing = "Mong", Beam = "Dam", Slab = "San", Lintel = "LanhTo", Canopy = "OVang", Stair = "CauThang";
    public static readonly string[] All = { Column, Footing, Beam, Slab, Lintel, Canopy, Stair };

    public static string Title(string k) => k switch
    {
        Column => "Cột", Footing => "Móng", Beam => "Dầm", Slab => "Sàn", Lintel => "Lanh tô", Canopy => "Ô văng", Stair => "Cầu thang", _ => k
    };

    /// <summary>Đặt bằng 1 điểm (cột, móng) / đoạn thẳng (dầm, lanh tô, móng băng) / hình chữ nhật (sàn, ô văng, cầu thang, móng bè, bể).</summary>
    public static bool IsPoint(string k) => k == Column || k == Footing;
    public static bool IsSegment(string k) => k == Beam || k == Lintel;
    public static bool IsRect(string k) => k == Slab || k == Canopy || k == Stair;

    /// <summary>Nhóm cấu kiện (khoá DetailingProject.Groups) tạo mới mặc định cho loại vị trí.</summary>
    public static string GroupKey(string k) => k switch
    {
        Column => "Cot", Footing => "MongDon", Beam => "Dam", Slab => "San", Lintel => "LanhTo", Canopy => "LanhTo", Stair => "CauThang", _ => "Dam"
    };

    /// <summary>Cấu kiện có hợp với loại vị trí không (dùng lọc danh sách gán).</summary>
    public static bool Accepts(string k, IDetailMember m) => k switch
    {
        Column => m is ManualColumn,
        Footing => m is ManualFooting || m is ManualPileCap || m is ManualStrip || m is RaftInput || m is WaterTankInput,
        Beam => m is ManualBeam || m is ManualStrip || m is PrestressInput,
        Slab => m is ManualSlab || m is ManualSlabPlan || m is RaftInput || m is WaterTankInput,
        Lintel => m is ManualLintel,
        Canopy => m is ManualLintel,
        Stair => m is ManualStair || m is ManualLiftPit,
        _ => false
    };
}

/// <summary>Một vị trí cấu kiện trên mặt bằng.</summary>
public class PlanItem
{
    /// <summary>Tên cấu kiện trong hồ sơ (D1, C1…); rỗng = chưa gán.</summary>
    public string Member { get; set; } = "";
    /// <summary>PlanKinds.*</summary>
    public string Kind { get; set; } = PlanKinds.Beam;
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    /// <summary>Góc xoay (độ) cho cột / móng; với ô văng: +1 vươn về bên trái hướng X1→X2, −1 bên phải (lưu trong dấu).</summary>
    public double Rotation { get; set; }
    /// <summary>Nhãn hiển thị thêm (tuỳ chọn), vd. "trục 2".</summary>
    public string Label { get; set; } = "";

    [JsonIgnore] public double Length => Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
    [JsonIgnore] public double MinX => Math.Min(X1, X2);
    [JsonIgnore] public double MaxX => Math.Max(X1, X2);
    [JsonIgnore] public double MinY => Math.Min(Y1, Y2);
    [JsonIgnore] public double MaxY => Math.Max(Y1, Y2);
}

/// <summary>Một mặt bằng (cao độ) – Móng, Tầng 2, …, Mái.</summary>
public class PlanLevel
{
    public string Name { get; set; } = "Tầng 2";
    /// <summary>Cao độ mặt sàn / mặt móng (m).</summary>
    public double Elevation { get; set; }
    /// <summary>Mặt bằng móng (vẽ móng, giằng móng, cột chân).</summary>
    public bool IsFoundation { get; set; }
    public List<PlanItem> Items { get; set; } = new();
    /// <summary>Ánh xạ (xref) mặt bằng kiến trúc của tầng – file DXF/DWG (đường dẫn tuyệt đối lúc chọn); chỉ để tham chiếu, không sửa.</summary>
    public string Underlay { get; set; } = "";
    /// <summary>Đường dẫn ánh xạ tương đối so với thư mục hồ sơ (ưu tiên khi mở hồ sơ ở máy khác).</summary>
    public string UnderlayRel { get; set; } = "";
    /// <summary>Dời ánh xạ (m) để trục kiến trúc trùng trục kết cấu.</summary>
    public double UnderlayDx { get; set; }
    public double UnderlayDy { get; set; }
    public bool UnderlayVisible { get; set; } = true;
}

/// <summary>Đường cắt mặt cắt thép sàn do người dùng đặt trên một mặt bằng (vd. "1-1" cắt qua ô sàn điển hình).</summary>
public class SectionMark
{
    /// <summary>Tên mặt cắt, vd. "1-1", "2-2".</summary>
    public string Name { get; set; } = "1-1";
    /// <summary>Tên mặt bằng (PlanLevel.Name) chứa đường cắt.</summary>
    public string Level { get; set; } = "";
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    [JsonIgnore] public double Length => Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
    /// <summary>Ký hiệu đầu mặt cắt: "1-1" → "1".</summary>
    [JsonIgnore] public string Tag => string.IsNullOrWhiteSpace(Name) ? "?" : Name.Split('-')[0].Trim();
}

/// <summary>Một nhóm của cây thư mục cấu kiện theo mặt bằng kết cấu.</summary>
public class PlanTreeGroup
{
    /// <summary>Tiêu đề nút, vd. "Mặt bằng móng", "Mặt bằng kết cấu Tầng 2", "Chưa đặt trên mặt bằng".</summary>
    public string Title { get; set; } = "";
    /// <summary>"Mong", "DamMong", "Cot", "Tang", "ChuaDat".</summary>
    public string Kind { get; set; } = "";
    /// <summary>Mặt bằng tương ứng (null với nhóm cột / chưa đặt).</summary>
    public PlanLevel Level { get; set; }
    public List<IDetailMember> Members { get; set; } = new();
}

public partial class StructuralPlan
{
    /// <summary>Bước trục X (trục đứng 1, 2, 3…), m – "3.6 4.2" hoặc "3*3.6".</summary>
    public string SpansX { get; set; } = "";
    /// <summary>Bước trục Y (trục ngang A, B, C…), m.</summary>
    public string SpansY { get; set; } = "";
    public List<PlanLevel> Levels { get; set; } = new()
    {
        new PlanLevel { Name = "Móng", Elevation = -1.5, IsFoundation = true },
        new PlanLevel { Name = "Tầng 2", Elevation = 3.6 },
        new PlanLevel { Name = "Mái", Elevation = 7.2 }
    };

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Toạ độ trục X (m, từ 0).</summary>
    [JsonIgnore] public double[] Xs => Coords(SpansX);
    [JsonIgnore] public double[] Ys => Coords(SpansY);
    [JsonIgnore] public bool HasGrid => Xs.Length >= 2 && Ys.Length >= 2;
    [JsonIgnore] public bool IsEmpty => Levels.All(l => l.Items.Count == 0);
    /// <summary>Các đường cắt mặt cắt thép sàn (người dùng chọn vị trí trên mặt bằng).</summary>
    public List<SectionMark> Sections { get; set; } = new();

    /// <summary>Tên mặt cắt kế tiếp chưa dùng: "1-1", "2-2"…</summary>
    public string NextSectionName()
    {
        for (int i = 1; ; i++)
        {
            string n = i.ToString(Inv) + "-" + i.ToString(Inv);
            if (!(Sections ?? new()).Any(s => s.Name == n)) return n;
        }
    }

    /// <summary>Cây thư mục cấu kiện theo mặt bằng kết cấu: Mặt bằng móng (móng đơn / cọc / băng / bè / bể), Mặt bằng dầm móng,
    /// Mặt bằng định vị cột, Mặt bằng kết cấu từng tầng theo cao độ (dầm, sàn, lanh tô, ô văng, cầu thang), cuối cùng "Chưa đặt trên mặt bằng".
    /// Một cấu kiện đặt ở nhiều mặt bằng xuất hiện ở mỗi mặt bằng đó. Nhóm rỗng vẫn trả về cho các mặt bằng có trong hồ sơ (trừ dầm móng / cột / chưa đặt).</summary>
    public static List<PlanTreeGroup> GroupForTree(DetailingProject p)
    {
        var groups = new List<PlanTreeGroup>();
        if (p == null) return groups;
        var all = p.All.ToList();
        var plan = p.Plan ?? new StructuralPlan();
        var levels = plan.Levels ?? new();
        var placed = new HashSet<IDetailMember>();
        IEnumerable<IDetailMember> On(PlanLevel lv, Func<PlanItem, IDetailMember, bool> pred)
        {
            var names = new HashSet<string>();
            foreach (var it in lv?.Items ?? new())
                if (!string.IsNullOrEmpty(it.Member)) names.Add(it.Member);
            foreach (var m in all)
                if (m != null && names.Contains(m.Name ?? "") && lv.Items.Any(i => i.Member == m.Name && pred(i, m))) yield return m;
        }
        static bool FoundBeam(IDetailMember m) => m is ManualBeam b && (b.Position ?? "").StartsWith("Dầm móng");

        var fnd = levels.FirstOrDefault(l => l.IsFoundation);
        var gF = new PlanTreeGroup { Title = "Mặt bằng móng", Kind = "Mong", Level = fnd };
        var gB = new PlanTreeGroup { Title = "Mặt bằng dầm móng", Kind = "DamMong", Level = fnd };
        var gC = new PlanTreeGroup { Title = "Mặt bằng định vị cột", Kind = "Cot" };
        if (fnd != null)
        {
            gF.Members.AddRange(On(fnd, (i, m) => i.Kind == PlanKinds.Footing || ((i.Kind == PlanKinds.Slab || i.Kind == PlanKinds.Beam) && m is not ManualBeam && m is not ManualSlab && m is not ManualSlabPlan && m is not PrestressInput)));
            gB.Members.AddRange(On(fnd, (i, m) => i.Kind == PlanKinds.Beam && m is ManualBeam));
        }
        var tangs = new List<PlanTreeGroup>();
        foreach (var lv in levels.Where(l => !l.IsFoundation).OrderBy(l => l.Elevation))
        {
            string nm = (lv.Name ?? "").Trim();
            string title = nm.StartsWith("Mái", StringComparison.OrdinalIgnoreCase) ? "Mặt bằng kết cấu mái" : "Mặt bằng kết cấu " + nm;
            var g = new PlanTreeGroup { Title = title, Kind = "Tang", Level = lv };
            // lanh tô – ô văng: nhóm riêng "Mặt bằng lanh tô – ô văng <tầng>" ngay sau mặt bằng tầng (mục lanh tô cửa các tầng)
            var gl = new PlanTreeGroup { Title = LintelGroupTitle(lv), Kind = LintelGroupKind, Level = lv };
            foreach (var m in On(lv, (i, mm) => i.Kind != PlanKinds.Column))
            {
                if (FoundBeam(m)) { if (!gB.Members.Contains(m)) gB.Members.Add(m); }
                else if (m is ManualLintel && lv.Items.Where(i => i.Member == m.Name).All(i => i.Kind == PlanKinds.Lintel || i.Kind == PlanKinds.Canopy)) gl.Members.Add(m);
                else g.Members.Add(m);
            }
            tangs.Add(g);
            if (gl.Members.Count > 0) tangs.Add(gl);
        }
        foreach (var lv in levels)
            foreach (var m in On(lv, (i, mm) => i.Kind == PlanKinds.Column))
                if (!gC.Members.Contains(m)) gC.Members.Add(m);

        if (fnd != null) groups.Add(gF);
        if (gB.Members.Count > 0) groups.Add(gB);
        if (gC.Members.Count > 0) groups.Add(gC);
        groups.AddRange(tangs);
        foreach (var g in groups) foreach (var m in g.Members) placed.Add(m);
        var un = new PlanTreeGroup { Title = "Chưa đặt trên mặt bằng", Kind = "ChuaDat" };
        un.Members.AddRange(all.Where(m => m != null && !placed.Contains(m)));
        if (un.Members.Count > 0) groups.Add(un);
        return groups;
    }

    public static double[] Coords(string spans)
    {
        var s = DetailShim.ParseList(spans ?? "");
        if (s.Count == 0) return Array.Empty<double>();
        var c = new double[s.Count + 1];
        for (int i = 0; i < s.Count; i++) c[i + 1] = Math.Round(c[i] + s[i], 6);
        return c;
    }

    /// <summary>Tên trục X: 1, 2, 3…</summary>
    public static string NameX(int i) => (i + 1).ToString(Inv);
    /// <summary>Tên trục Y: A, B, … Z, AA, AB… (bỏ I, O dễ nhầm số theo thói quen hồ sơ VN? – giữ đủ chữ cho đơn giản).</summary>
    public static string NameY(int i)
    {
        string s = "";
        i++;
        while (i > 0) { int r = (i - 1) % 26; s = (char)('A' + r) + s; i = (i - 1) / 26; }
        return s;
    }

    public PlanLevel Level(string name) => Levels.FirstOrDefault(l => l.Name == name);
    [JsonIgnore] public PlanLevel Foundation => Levels.FirstOrDefault(l => l.IsFoundation);

    // ------------------------------------------------------------------
    // bắt điểm
    // ------------------------------------------------------------------
    /// <summary>Bắt điểm: giao trục trong bán kính tol → giao trục; từng toạ độ gần đường trục → đường trục; còn lại làm tròn round (m).</summary>
    public (double X, double Y) Snap(double x, double y, double tol = 0.25, double round = 0.05)
    {
        var xs = Xs; var ys = Ys;
        double bx = Nearest(xs, x), by = Nearest(ys, y);
        bool hx = xs.Length > 0 && Math.Abs(bx - x) <= tol, hy = ys.Length > 0 && Math.Abs(by - y) <= tol;
        double rx = hx ? bx : round > 0 ? Math.Round(x / round) * round : x;
        double ry = hy ? by : round > 0 ? Math.Round(y / round) * round : y;
        return (Math.Round(rx, 6), Math.Round(ry, 6));
    }

    public static double Nearest(double[] c, double v)
    {
        if (c == null || c.Length == 0) return v;
        double best = c[0];
        foreach (double t in c) if (Math.Abs(t - v) < Math.Abs(best - v)) best = t;
        return best;
    }

    /// <summary>Chỉ số trục gần nhất (−1 nếu không có).</summary>
    public static int NearestIndex(double[] c, double v)
    {
        int bi = -1; double bd = double.MaxValue;
        for (int i = 0; i < (c?.Length ?? 0); i++) if (Math.Abs(c[i] - v) < bd) { bd = Math.Abs(c[i] - v); bi = i; }
        return bi;
    }

    // ------------------------------------------------------------------
    // số lượng – đối chiếu với SL cấu kiện
    // ------------------------------------------------------------------
    /// <summary>Số cấu kiện theo mặt bằng: cột đếm theo vị trí (một cột nhiều tầng đặt ở nhiều mặt bằng vẫn là 1); ô văng gắn lanh tô không đếm thêm;
    /// các loại khác đếm mỗi lần đặt.</summary>
    public int Count(string member)
    {
        if (string.IsNullOrEmpty(member)) return 0;
        var items = Levels.SelectMany(l => l.Items).Where(i => i.Member == member && i.Kind != PlanKinds.Canopy).ToList();
        int cols = items.Where(i => i.Kind == PlanKinds.Column).Select(i => (Math.Round(i.X1, 3), Math.Round(i.Y1, 3))).Distinct().Count();
        int canopyOnly = items.Count == 0 ? Levels.SelectMany(l => l.Items).Count(i => i.Member == member && i.Kind == PlanKinds.Canopy) : 0;
        return cols + items.Count(i => i.Kind != PlanKinds.Column) + canopyOnly;
    }

    /// <summary>Cảnh báo SL cấu kiện khác số lần đặt trên mặt bằng (chỉ với cấu kiện đã đặt ít nhất 1 lần) và vị trí gán tên không tồn tại.</summary>
    public List<string> QuantityWarnings(DetailingProject p)
    {
        var res = new List<string>();
        foreach (var m in p.All)
        {
            int n = Count(m.Name);
            if (n > 0 && n != m.Quantity) res.Add($"{m.Name}: SL khai báo {m.Quantity} ≠ {n} vị trí trên mặt bằng – bấm \"Cập nhật SL theo mặt bằng\"");
        }
        var names = new HashSet<string>(p.All.Select(m => m.Name));
        foreach (var l in Levels)
            foreach (var it in l.Items.Where(i => !string.IsNullOrEmpty(i.Member) && !names.Contains(i.Member)))
                res.Add($"{l.Name}: vị trí gán \"{it.Member}\" nhưng hồ sơ không có cấu kiện này");
        return res.Distinct().ToList();
    }

    /// <summary>Đặt SL mỗi cấu kiện = số lần đặt trên mặt bằng (cấu kiện chưa đặt giữ nguyên). Trả về số cấu kiện đã đổi.</summary>
    public int SyncQuantities(DetailingProject p)
    {
        int k = 0;
        foreach (var m in p.All)
        {
            int n = Count(m.Name);
            if (n > 0 && n != m.Quantity) { m.Quantity = n; k++; }
        }
        return k;
    }

    /// <summary>Đổi tên cấu kiện: đổi cả tên trong hồ sơ và mọi vị trí tham chiếu. Trả false nếu tên mới trùng / không có cấu kiện.</summary>
    public bool Rename(DetailingProject p, string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || oldName == newName) return false;
        var m = p.All.FirstOrDefault(x => x.Name == oldName);
        if (m == null || p.All.Any(x => x.Name == newName)) return false;
        m.Name = newName;
        foreach (var l in Levels) foreach (var it in l.Items.Where(i => i.Member == oldName)) it.Member = newName;
        return true;
    }

    /// <summary>Xoá các vị trí tham chiếu cấu kiện (khi xoá cấu kiện khỏi hồ sơ).</summary>
    public int RemoveMember(string name)
    {
        int n = 0;
        foreach (var l in Levels) n += l.Items.RemoveAll(i => i.Member == name);
        return n;
    }

    // ------------------------------------------------------------------
    // tạo cấu kiện theo vị trí (suy kích thước)
    // ------------------------------------------------------------------
    /// <summary>Nhịp dầm theo đoạn đặt: cắt tại các trục vuông góc nằm trong đoạn (và các cột đặt trên cùng mặt bằng nằm trên đoạn). Trả về các nhịp (m).</summary>
    public List<double> BeamSpans(PlanItem it, PlanLevel lv = null)
    {
        double len = it.Length;
        if (len < 1e-6) return new List<double>();
        double ux = (it.X2 - it.X1) / len, uy = (it.Y2 - it.Y1) / len;
        var cuts = new List<double> { 0, len };
        bool alongX = Math.Abs(ux) >= Math.Abs(uy);
        // trục vuông góc (dầm theo X cắt các trục X đứng; dầm theo Y cắt trục Y ngang) – chỉ khi dầm gần song song trục
        if (Math.Abs(alongX ? uy : ux) < 1e-3)
            foreach (double g in alongX ? Xs : Ys)
            {
                double t = alongX ? (g - it.X1) / ux : (g - it.Y1) / uy;
                if (t > 0.05 && t < len - 0.05) cuts.Add(t);
            }
        // cột trên mặt bằng nằm trên đoạn
        foreach (var c in (lv?.Items ?? new()).Where(i => i.Kind == PlanKinds.Column))
        {
            double t = (c.X1 - it.X1) * ux + (c.Y1 - it.Y1) * uy;
            double d = Math.Abs(-(c.X1 - it.X1) * uy + (c.Y1 - it.Y1) * ux);
            if (d < 0.05 && t > 0.05 && t < len - 0.05) cuts.Add(t);
        }
        cuts = cuts.OrderBy(t => t).ToList();
        var res = new List<double>();
        for (int i = 1; i < cuts.Count; i++)
        {
            double s = Math.Round(cuts[i] - cuts[i - 1], 3);
            if (s > 0.05 && (res.Count == 0 || cuts[i] - cuts[i - 1] > 0.05)) res.Add(s);
        }
        return res;
    }

    private static string SpansText(IEnumerable<double> spans) => string.Join("; ", spans.Select(v => Dt.F(v, "0.###")));

    /// <summary>Vị trí chưa gán cấu kiện (hoặc gán tên chưa có) → tạo cấu kiện mới trong hồ sơ, suy kích thước, gán tên vào vị trí.
    /// Trả về cấu kiện (có sẵn hoặc mới). newName: tên muốn đặt (rỗng = tự đánh số D1, D2…).</summary>
    public IDetailMember EnsureMember(DetailingProject p, PlanItem it, PlanLevel lv = null, string newName = null)
    {
        if (!string.IsNullOrEmpty(it.Member))
        {
            var ex = p.All.FirstOrDefault(m => m.Name == it.Member);
            if (ex != null) return ex;
            newName ??= it.Member;
        }
        var m2 = p.AddNew(PlanKinds.GroupKey(it.Kind));
        if (!string.IsNullOrWhiteSpace(newName) && !p.All.Any(x => x != m2 && x.Name == newName)) m2.Name = newName.Trim();
        m2.Quantity = 1;
        ApplyGeometry(m2, it, lv);
        it.Member = m2.Name;
        return m2;
    }

    /// <summary>Áp kích thước suy từ vị trí vào cấu kiện (nhịp dầm, ô sàn, bề rộng ô cửa…). Trả về mô tả đã áp.</summary>
    public string ApplyGeometry(IDetailMember m, PlanItem it, PlanLevel lv = null)
    {
        switch (m)
        {
            case ManualBeam b when PlanKinds.IsSegment(it.Kind):
                var sp = BeamSpans(it, lv);
                if (sp.Count > 0) { b.Spans = SpansText(sp); if (lv != null) b.TopElevation = lv.Elevation; return "nhịp " + b.Spans + " m"; }
                break;
            case ManualStrip st when PlanKinds.IsSegment(it.Kind):
                if (it.Length > 0.5) { st.Length = Math.Round(it.Length, 3); return "dài " + Dt.F(st.Length) + " m"; }
                break;
            case ManualSlab s when PlanKinds.IsRect(it.Kind):
                double w = it.MaxX - it.MinX, h = it.MaxY - it.MinY;
                if (w > 0.3 && h > 0.3) { s.Lx = Math.Round(Math.Min(w, h), 3); s.Ly = Math.Round(Math.Max(w, h), 3); return "ô " + Dt.F(s.Lx) + " × " + Dt.F(s.Ly) + " m"; }
                break;
            case ManualLintel l when PlanKinds.IsSegment(it.Kind):
                if (it.Length > 0.3) { l.Lo = Math.Round(it.Length, 3); return "ô cửa " + Dt.F(l.Lo) + " m"; }
                break;
            case ManualLintel l2 when it.Kind == PlanKinds.Canopy:
                double cw = it.MaxX - it.MinX, chh = it.MaxY - it.MinY;
                if (cw > 0.3 && chh > 0.2) { l2.Lc = Math.Round(Math.Min(cw, chh), 3); return "vươn " + Dt.F(l2.Lc) + " m"; }
                break;
            case ManualStair cs when PlanKinds.IsRect(it.Kind):
                double sw = it.MaxX - it.MinX, sh = it.MaxY - it.MinY;
                if (sw > 0.8 && sh > 1.5) { cs.WellWidth = Math.Round(Math.Min(sw, sh), 3); cs.WellLength = Math.Round(Math.Max(sw, sh), 3); return "ô thang " + Dt.F(cs.WellWidth) + " × " + Dt.F(cs.WellLength) + " m"; }
                break;
            case ManualLiftPit lp when PlanKinds.IsRect(it.Kind):
                double lw = it.MaxX - it.MinX, lh = it.MaxY - it.MinY;
                if (lw - 2 * lp.Wall > 0.8 && lh - 2 * lp.Wall > 0.8)
                {
                    lp.ShaftW = Math.Round(lw - 2 * lp.Wall, 3); lp.ShaftD = Math.Round(lh - 2 * lp.Wall, 3);
                    return "giếng thang " + Dt.F(lp.ShaftW) + " × " + Dt.F(lp.ShaftD) + " m (mép ngoài vách)";
                }
                break;
            case RaftInput r when PlanKinds.IsRect(it.Kind):
                double rw = it.MaxX - it.MinX, rh = it.MaxY - it.MinY;
                if (rw > 1 && rh > 1) { r.Lx = Math.Round(rw, 3); r.Ly = Math.Round(rh, 3); return "bè " + Dt.F(r.Lx) + " × " + Dt.F(r.Ly) + " m"; }
                break;
        }
        return "";
    }

    /// <summary>Thêm vị trí vào mặt bằng (đã bắt điểm), tạo / gán cấu kiện. member: tên có sẵn (rỗng = tạo mới).</summary>
    public PlanItem Place(DetailingProject p, PlanLevel lv, string kind, double x1, double y1, double x2, double y2, string member = "")
    {
        var it = new PlanItem { Kind = kind, X1 = x1, Y1 = y1, X2 = PlanKinds.IsPoint(kind) ? x1 : x2, Y2 = PlanKinds.IsPoint(kind) ? y1 : y2, Member = member ?? "" };
        lv.Items.Add(it);
        EnsureMember(p, it, lv);
        return it;
    }

    /// <summary>Vị trí gần điểm (x,y) nhất trong bán kính tol (m) trên mặt bằng lv.</summary>
    public static PlanItem HitTest(PlanLevel lv, double x, double y, double tol)
    {
        PlanItem best = null; double bd = tol;
        foreach (var it in lv?.Items ?? new())
        {
            double d;
            if (PlanKinds.IsPoint(it.Kind)) d = Math.Sqrt((it.X1 - x) * (it.X1 - x) + (it.Y1 - y) * (it.Y1 - y));
            else if (PlanKinds.IsSegment(it.Kind)) d = DistSeg(x, y, it.X1, it.Y1, it.X2, it.Y2);
            // hình chữ nhật (sàn, ô văng, cầu thang): chỉ chọn khi không có cột / móng / dầm gần; ô nhỏ hơn được ưu tiên
            else d = x >= it.MinX && x <= it.MaxX && y >= it.MinY && y <= it.MaxY ? tol * (0.9 + 0.09 * Math.Min(1, (it.MaxX - it.MinX) * (it.MaxY - it.MinY) / 200)) : double.MaxValue;
            if (d < bd) { bd = d; best = it; }
        }
        return best;
    }

    public static double DistSeg(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay, L2 = dx * dx + dy * dy;
        double t = L2 < 1e-12 ? 0 : Math.Clamp(((px - ax) * dx + (py - ay) * dy) / L2, 0, 1);
        double qx = ax + t * dx - px, qy = ay + t * dy - py;
        return Math.Sqrt(qx * qx + qy * qy);
    }

    /// <summary>Mặt bằng mẫu khớp hồ sơ mẫu: lưới 2 nhịp × 1 nhịp, móng + cột ở 6 giao trục, dầm D1 trục A (giằng móng) và trục B, sàn S1, lanh tô + ô văng LT1, cầu thang CT1.</summary>
    public static StructuralPlan Sample()
    {
        var sp = new StructuralPlan { SpansX = "4.2 3.6", SpansY = "4.5" };
        var f = sp.Levels[0]; var t2 = sp.Levels[1];
        foreach (double x in new[] { 0.0, 4.2, 7.8 })
            foreach (double y in new[] { 0.0, 4.5 })
            {
                f.Items.Add(new PlanItem { Kind = PlanKinds.Footing, X1 = x, Y1 = y, X2 = x, Y2 = y, Member = "M1" });
                f.Items.Add(new PlanItem { Kind = PlanKinds.Column, X1 = x, Y1 = y, X2 = x, Y2 = y, Member = "C1" });
                t2.Items.Add(new PlanItem { Kind = PlanKinds.Column, X1 = x, Y1 = y, X2 = x, Y2 = y, Member = "C1" });
            }
        f.Items.Add(new PlanItem { Kind = PlanKinds.Beam, X1 = 0, Y1 = 0, X2 = 7.8, Y2 = 0, Member = "D1" });
        t2.Items.Add(new PlanItem { Kind = PlanKinds.Beam, X1 = 0, Y1 = 4.5, X2 = 7.8, Y2 = 4.5, Member = "D1" });
        t2.Items.Add(new PlanItem { Kind = PlanKinds.Slab, X1 = 4.2, Y1 = 0, X2 = 7.8, Y2 = 4.5, Member = "S1" });
        t2.Items.Add(new PlanItem { Kind = PlanKinds.Lintel, X1 = 1.5, Y1 = 4.5, X2 = 2.7, Y2 = 4.5, Member = "LT1" });
        t2.Items.Add(new PlanItem { Kind = PlanKinds.Canopy, X1 = 1.3, Y1 = 4.5, X2 = 2.9, Y2 = 5.1, Member = "LT1" });
        t2.Items.Add(new PlanItem { Kind = PlanKinds.Stair, X1 = 0.2, Y1 = 0.2, X2 = 2.6, Y2 = 2.0, Member = "CT1" });
        return sp;
    }
}

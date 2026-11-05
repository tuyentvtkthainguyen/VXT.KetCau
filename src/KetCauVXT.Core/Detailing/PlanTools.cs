using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KetCauVXT.Detailing;

// =====================================================================
// CÔNG CỤ MẶT BẰNG KẾT CẤU (chế độ Triển khai hồ sơ):
//  • Nhân bản tầng (các tầng giống nhau về bố trí công năng) – chế độ A "dùng chung cấu kiện" / B "tạo cấu kiện mới".
//  • Bắt điểm khi vẽ: đầu dầm, tâm cột, giao trục, đường trục, chế độ vuông góc (Shift) – PlanSnap.
//  • Sàn bấm trong ô dầm: tìm ô kín bao quanh điểm bấm theo dầm / trục.
//  • Hoàn tác (Ctrl+Z) theo ảnh chụp JSON của mặt bằng – PlanUndo.
//  • Ánh xạ vị trí ↔ cấu kiện (cây thư mục ↔ mặt bằng) – PlanMemberMap.
//
// QUY ƯỚC SỐ LƯỢNG (SL) khi nhân bản:
//  Mỗi lần đặt một cấu kiện trên mặt bằng là 1 cấu kiện thực (StructuralPlan.Count): dầm, sàn, lanh tô, cầu thang… đếm mỗi lần đặt
//  ở mọi mặt bằng; cột đếm theo vị trí (1 cột là 1 cấu kiện nhiều tầng – ManualColumn.Stories). Sau khi nhân bản, SL được đồng bộ
//  theo mặt bằng (SyncQuantities) nên thống kê thép / bê tông / ván khuôn (DetailingExport.Quantities, MaterialTable, RebarSchedule – đều
//  nhân theo Quantity) tự nhân đúng số tầng:
//   – Chế độ A (dùng chung): tầng mới tham chiếu cùng tên D1, S1… → SL D1 = tổng số lần đặt ở các tầng; cột: thêm 1 tầng vào "Chiều cao các tầng".
//   – Chế độ B (tạo mới): mỗi cấu kiện (trừ cột) được sao thành cấu kiện riêng tên có hậu tố tầng (D1 → D1-T3), sửa độc lập; cột vẫn dùng chung
//     (thêm tầng) vì cột liên tục qua các tầng.
// =====================================================================

public partial class StructuralPlan
{
    /// <summary>Kiểu nhóm cây thư mục "Mặt bằng lanh tô – ô văng &lt;tầng&gt;".</summary>
    public const string LintelGroupKind = "LanhTo";

    /// <summary>"Mặt bằng lanh tô – ô văng Tầng 2".</summary>
    public static string LintelGroupTitle(PlanLevel lv) => "Mặt bằng lanh tô – ô văng " + (lv?.Name ?? "").Trim();

    /// <summary>Nhân bản mặt bằng src thành mặt bằng mới (chỉ sao vị trí, dùng chung tên cấu kiện – chế độ A, không đổi hồ sơ).</summary>
    public PlanLevel CopyLevel(PlanLevel src, string newName, double elevation, bool cloneMembers) =>
        PlanStorey.CopyLevel(null, this, src, newName, elevation, cloneMembers);
}

/// <summary>Nhân bản tầng.</summary>
public static class PlanStorey
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Hậu tố tên cấu kiện theo tên tầng: "Tầng 3" → "T3", "Mái" → "M", "Tum" → "TUM", khác → chữ cái đầu + số.</summary>
    public static string LevelCode(string levelName)
    {
        string n = (levelName ?? "").Trim();
        var mNum = Regex.Match(n, @"(\d+)");
        string up = RemoveMarks(n).ToUpperInvariant();
        if (up.StartsWith("MAI")) return "M" + (mNum.Success ? mNum.Value : "");
        if (up.StartsWith("TUM")) return "TUM";
        if (up.StartsWith("TANG") && mNum.Success) return "T" + mNum.Value;
        string letters = new string(up.Where(char.IsLetter).Take(2).ToArray());
        return (letters.Length > 0 ? letters : "T") + (mNum.Success ? mNum.Value : "");
    }

    /// <summary>Bỏ dấu tiếng Việt (đ → d).</summary>
    public static string RemoveMarks(string s)
    {
        var f = (s ?? "").Replace('đ', 'd').Replace('Đ', 'D').Normalize(System.Text.NormalizationForm.FormD);
        return new string(f.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).Normalize(System.Text.NormalizationForm.FormC);
    }

    /// <summary>Tên tầng kế tiếp: "Tầng 2" → "Tầng 3"; tên không có số → "&lt;tên&gt; (2)".</summary>
    public static string NextLevelName(StructuralPlan plan, string srcName, int step = 1)
    {
        string n = (srcName ?? "Tầng").Trim();
        var m = Regex.Match(n, @"^(.*?)(\d+)\s*$");
        var used = new HashSet<string>((plan?.Levels ?? new()).Select(l => l.Name));
        if (m.Success)
        {
            int k = int.Parse(m.Groups[2].Value, Inv) + step;
            string c = m.Groups[1].Value + k.ToString(Inv);
            while (used.Contains(c)) { k++; c = m.Groups[1].Value + k.ToString(Inv); }
            return c;
        }
        for (int i = 1 + step; ; i++) { string c = n + " (" + i.ToString(Inv) + ")"; if (!used.Contains(c)) return c; }
    }

    /// <summary>Sao sâu một cấu kiện (qua JSON theo kiểu thực).</summary>
    public static IDetailMember CloneMember(IDetailMember m)
    {
        if (m == null) return null;
        var t = m.GetType();
        var opt = new JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
        return (IDetailMember)JsonSerializer.Deserialize(JsonSerializer.Serialize(m, t, opt), t, opt);
    }

    /// <summary>Danh sách nhóm (IList) đang chứa cấu kiện m trong hồ sơ.</summary>
    public static System.Collections.IList ListOf(DetailingProject p, IDetailMember m)
    {
        foreach (var (_, _, list) in p?.Groups ?? Enumerable.Empty<(string, string, System.Collections.IList)>())
            if (list.Contains(m)) return list;
        return null;
    }

    /// <summary>Nhân bản mặt bằng src → mặt bằng mới newName ở cao độ elevation (xếp theo cao độ).
    /// cloneMembers = false (A): dùng chung cấu kiện; true (B): sao cấu kiện mới hậu tố tầng (trừ cột). p = null → chỉ sao vị trí.
    /// Cột đặt trên src: thêm một tầng (elevation − cao độ src) vào ManualColumn.Stories nếu p ≠ null.
    /// Các đường cắt mặt cắt sàn trên src cũng được sao (tên mới). SL đồng bộ theo mặt bằng khi p ≠ null.</summary>
    public static PlanLevel CopyLevel(DetailingProject p, StructuralPlan plan, PlanLevel src, string newName, double elevation, bool cloneMembers)
    {
        if (plan == null || src == null) return null;
        string name = string.IsNullOrWhiteSpace(newName) ? NextLevelName(plan, src.Name) : newName.Trim();
        if (plan.Levels.Any(l => l.Name == name)) name = NextLevelName(plan, name, 0 + 1);
        var lv = new PlanLevel
        {
            Name = name, Elevation = Math.Round(elevation, 4), IsFoundation = false,
            Underlay = src.Underlay, UnderlayRel = src.UnderlayRel, UnderlayDx = src.UnderlayDx, UnderlayDy = src.UnderlayDy, UnderlayVisible = src.UnderlayVisible
        };
        var rename = new Dictionary<string, string>();
        if (p != null && cloneMembers)
        {
            string code = LevelCode(name);
            var names = new HashSet<string>(p.All.Select(x => x.Name));
            foreach (string mn in src.Items.Select(i => i.Member).Where(s => !string.IsNullOrEmpty(s)).Distinct())
            {
                var m = p.All.FirstOrDefault(x => x.Name == mn);
                if (m == null || m is ManualColumn) continue;
                var list = ListOf(p, m);
                var c = CloneMember(m);
                if (list == null || c == null) continue;
                string baseName = Regex.Replace(mn, @"-(T\d+|M\d*|TUM|[A-Z]{1,2}\d*)$", "");
                string nn = baseName + "-" + code;
                for (int k = 2; names.Contains(nn); k++) nn = baseName + "-" + code + "." + k.ToString(Inv);
                c.Name = nn; c.Quantity = 1;
                if (c is ManualBeam cb) cb.TopElevation = lv.Elevation;
                names.Add(nn);
                list.Add(c);
                rename[mn] = nn;
            }
        }
        foreach (var it in src.Items)
            lv.Items.Add(new PlanItem
            {
                Member = it.Member != null && rename.TryGetValue(it.Member, out var nn) ? nn : it.Member, Kind = it.Kind,
                X1 = it.X1, Y1 = it.Y1, X2 = it.X2, Y2 = it.Y2, Rotation = it.Rotation, Label = it.Label
            });
        // cột: thêm 1 tầng – chiều cao = cao độ mới − cao độ mặt bằng tầng ngay dưới (trước khi chèn)
        double below = plan.Levels.Where(l => !l.IsFoundation && l.Elevation < elevation - 1e-9).Select(l => l.Elevation).DefaultIfEmpty(src.Elevation).Max();
        double dh = Math.Round(elevation - below, 4);
        if (p != null && dh > 0.5)
            foreach (string cn in src.Items.Where(i => i.Kind == PlanKinds.Column).Select(i => i.Member).Where(s => !string.IsNullOrEmpty(s)).Distinct())
                if (p.All.FirstOrDefault(x => x.Name == cn) is ManualColumn col)
                {
                    var h = DetailShim.ParseList(col.Stories ?? "");
                    h.Add(dh);
                    col.Stories = string.Join("; ", h.Select(v => Dt.F(v, "0.###")));
                }
        // đường cắt sàn
        plan.Sections ??= new();
        foreach (var sm in plan.Sections.Where(q => q.Level == src.Name).ToList())
            plan.Sections.Add(new SectionMark { Name = plan.NextSectionName(), Level = lv.Name, X1 = sm.X1, Y1 = sm.Y1, X2 = sm.X2, Y2 = sm.Y2 });
        // chèn theo cao độ
        int at = plan.Levels.FindIndex(l => !l.IsFoundation && l.Elevation > lv.Elevation + 1e-9);
        if (at < 0) plan.Levels.Add(lv); else plan.Levels.Insert(at, lv);
        if (p != null) plan.SyncQuantities(p);
        return lv;
    }

    /// <summary>Nhân bản src thành count tầng liên tiếp, mỗi tầng cao storeyHeight (m), tên tự tăng (Tầng 2 → Tầng 3, Tầng 4…).
    /// Mặt bằng đã có ở cao độ đích (vd. Mái) được đẩy lên count·storeyHeight nếu shiftAbove.</summary>
    public static List<PlanLevel> CopyLevels(DetailingProject p, StructuralPlan plan, PlanLevel src, int count, double storeyHeight, bool cloneMembers, bool shiftAbove = true)
    {
        var res = new List<PlanLevel>();
        if (plan == null || src == null || count <= 0 || storeyHeight <= 0.5) return res;
        if (shiftAbove)
            foreach (var l in plan.Levels.Where(q => !q.IsFoundation && q.Elevation > src.Elevation + 1e-9))
                l.Elevation = Math.Round(l.Elevation + count * storeyHeight, 4);
        string prevName = src.Name;
        for (int i = 1; i <= count; i++)
        {
            string nm = NextLevelName(plan, prevName);
            var lv = CopyLevel(p, plan, src, nm, src.Elevation + i * storeyHeight, cloneMembers);
            if (lv == null) break;
            res.Add(lv); prevName = lv.Name;
        }
        return res;
    }
}

/// <summary>Bắt điểm khi vẽ trên mặt bằng.</summary>
public static class PlanSnap
{
    public const string KindEnd = "Đầu cấu kiện", KindColumn = "Tâm cột", KindGridX = "Giao trục", KindGridLine = "Đường trục", KindFree = "Tự do", KindOnBeam = "Trên dầm";

    /// <summary>Bắt điểm theo thứ tự ưu tiên: tâm cột / móng, đầu dầm – lanh tô (≤ tol) → giao trục (≤ tol) → điểm trên tim dầm (≤ tol/2)
    /// → đường trục từng phương → làm tròn round. from + ortho: ép vuông góc (ngang / dọc theo phương gần hơn) trước khi bắt.</summary>
    public static (double X, double Y, string Kind) Snap(StructuralPlan plan, PlanLevel lv, double x, double y, double tol = 0.25, (double X, double Y)? from = null,
        bool ortho = false, double round = 0.05)
    {
        if (from is { } f && ortho)
        {
            if (Math.Abs(x - f.X) >= Math.Abs(y - f.Y)) y = f.Y; else x = f.X;
        }
        bool lockY = from != null && ortho && Math.Abs(y - from.Value.Y) < 1e-12, lockX = from != null && ortho && Math.Abs(x - from.Value.X) < 1e-12;
        (double X, double Y)? best = null; double bd = tol; string kind = KindFree;
        void Try(double px, double py, string k)
        {
            if (lockY && Math.Abs(py - y) > 1e-6) return;
            if (lockX && Math.Abs(px - x) > 1e-6) return;
            double d = Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
            if (d < bd - 1e-12) { bd = d; best = (px, py); kind = k; }
        }
        foreach (var it in lv?.Items ?? new())
        {
            if (PlanKinds.IsPoint(it.Kind)) Try(it.X1, it.Y1, KindColumn);
            else if (PlanKinds.IsSegment(it.Kind)) { Try(it.X1, it.Y1, KindEnd); Try(it.X2, it.Y2, KindEnd); }
        }
        if (best != null) return (Math.Round(best.Value.X, 6), Math.Round(best.Value.Y, 6), kind);
        var xs = plan?.Xs ?? Array.Empty<double>(); var ys = plan?.Ys ?? Array.Empty<double>();
        foreach (double gx in xs) foreach (double gy in ys) Try(gx, gy, KindGridX);
        if (best != null) return (Math.Round(best.Value.X, 6), Math.Round(best.Value.Y, 6), kind);
        // điểm trên tim dầm (chân vuông góc)
        double bt = tol / 2;
        foreach (var it in (lv?.Items ?? new()).Where(i => i.Kind == PlanKinds.Beam))
        {
            double L = it.Length; if (L < 1e-6) continue;
            double ux = (it.X2 - it.X1) / L, uy = (it.Y2 - it.Y1) / L;
            double t = Math.Clamp((x - it.X1) * ux + (y - it.Y1) * uy, 0, L);
            double px = it.X1 + t * ux, py = it.Y1 + t * uy;
            double d = Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
            if (d < bt && !(lockX && Math.Abs(px - x) > 1e-6) && !(lockY && Math.Abs(py - y) > 1e-6)) { bt = d; best = (px, py); kind = KindOnBeam; }
        }
        if (best != null) return (Math.Round(best.Value.X, 6), Math.Round(best.Value.Y, 6), kind);
        double bx = StructuralPlan.Nearest(xs, x), by = StructuralPlan.Nearest(ys, y);
        bool hx = xs.Length > 0 && Math.Abs(bx - x) <= tol && !lockX, hy = ys.Length > 0 && Math.Abs(by - y) <= tol && !lockY;
        double rx = hx ? bx : lockX ? x : round > 0 ? Math.Round(x / round) * round : x;
        double ry = hy ? by : lockY ? y : round > 0 ? Math.Round(y / round) * round : y;
        return (Math.Round(rx, 6), Math.Round(ry, 6), hx || hy ? KindGridLine : KindFree);
    }

    /// <summary>Ô kín bao quanh điểm (x, y): cạnh trái / phải / dưới / trên là dầm (theo phương trục) hoặc đường trục gần nhất cắt qua điểm.
    /// Trả về null nếu không đủ 4 cạnh.</summary>
    public static (double X0, double Y0, double X1, double Y1)? BayAt(StructuralPlan plan, PlanLevel lv, double x, double y)
    {
        var vx = new List<double>(); var hy = new List<double>();
        foreach (var it in (lv?.Items ?? new()).Where(i => i.Kind == PlanKinds.Beam))
        {
            if (Math.Abs(it.X1 - it.X2) < 1e-6 && y >= it.MinY - 1e-6 && y <= it.MaxY + 1e-6) vx.Add(it.X1);
            if (Math.Abs(it.Y1 - it.Y2) < 1e-6 && x >= it.MinX - 1e-6 && x <= it.MaxX + 1e-6) hy.Add(it.Y1);
        }
        bool beams = vx.Count + hy.Count > 0;
        if (!beams) { vx.AddRange(plan?.Xs ?? Array.Empty<double>()); hy.AddRange(plan?.Ys ?? Array.Empty<double>()); }
        else
        {
            // bổ sung trục nếu một phía chưa có dầm
            if (!vx.Any(v => v < x) || !vx.Any(v => v > x)) vx.AddRange(plan?.Xs ?? Array.Empty<double>());
            if (!hy.Any(v => v < y) || !hy.Any(v => v > y)) hy.AddRange(plan?.Ys ?? Array.Empty<double>());
        }
        var l = vx.Where(v => v < x - 1e-6).DefaultIfEmpty(double.NaN).Max();
        var r = vx.Where(v => v > x + 1e-6).DefaultIfEmpty(double.NaN).Min();
        var b = hy.Where(v => v < y - 1e-6).DefaultIfEmpty(double.NaN).Max();
        var t = hy.Where(v => v > y + 1e-6).DefaultIfEmpty(double.NaN).Min();
        if (double.IsNaN(l) || double.IsNaN(r) || double.IsNaN(b) || double.IsNaN(t)) return null;
        return (l, b, r, t);
    }

    /// <summary>Dời vị trí (dx, dy) – giữ hình dạng.</summary>
    public static void Move(PlanItem it, double dx, double dy)
    {
        if (it == null) return;
        it.X1 = Math.Round(it.X1 + dx, 6); it.X2 = Math.Round(it.X2 + dx, 6);
        it.Y1 = Math.Round(it.Y1 + dy, 6); it.Y2 = Math.Round(it.Y2 + dy, 6);
    }
}

/// <summary>Hoàn tác thao tác trên mặt bằng (ảnh chụp JSON lưới trục, mặt bằng, đường cắt).</summary>
public sealed class PlanUndo
{
    private readonly List<string> _stack = new();
    public int Limit { get; set; } = 50;
    public int Count => _stack.Count;

    public void Push(StructuralPlan plan)
    {
        if (plan == null) return;
        string s = JsonSerializer.Serialize(plan);
        if (_stack.Count > 0 && _stack[^1] == s) return;
        _stack.Add(s);
        if (_stack.Count > Limit) _stack.RemoveAt(0);
    }

    /// <summary>Khôi phục ảnh chụp gần nhất vào plan (sửa tại chỗ). Trả false nếu hết.</summary>
    public bool Undo(StructuralPlan plan)
    {
        if (plan == null || _stack.Count == 0) return false;
        var o = JsonSerializer.Deserialize<StructuralPlan>(_stack[^1]);
        _stack.RemoveAt(_stack.Count - 1);
        if (o == null) return false;
        plan.SpansX = o.SpansX; plan.SpansY = o.SpansY;
        plan.Levels = o.Levels ?? new();
        foreach (var l in plan.Levels) l.Items ??= new();
        plan.Sections = o.Sections ?? new();
        return true;
    }
}

/// <summary>Ánh xạ vị trí trên mặt bằng ↔ cấu kiện hồ sơ (cây thư mục).</summary>
public static class PlanMemberMap
{
    /// <summary>Cấu kiện của vị trí: tên trùng và loại cấu kiện hợp với loại vị trí (ưu tiên), nếu không có thì chỉ theo tên.</summary>
    public static IDetailMember Find(DetailingProject p, PlanItem it)
    {
        if (p == null || it == null || string.IsNullOrEmpty(it.Member)) return null;
        var same = p.All.Where(m => m != null && m.Name == it.Member).ToList();
        return same.FirstOrDefault(m => PlanKinds.Accepts(it.Kind, m)) ?? same.FirstOrDefault();
    }

    /// <summary>Mọi vị trí (mặt bằng, vị trí) của cấu kiện m – theo thứ tự mặt bằng.</summary>
    public static List<(PlanLevel Level, PlanItem Item)> Placements(StructuralPlan plan, IDetailMember m)
    {
        var res = new List<(PlanLevel, PlanItem)>();
        if (plan == null || m == null) return res;
        foreach (var l in plan.Levels)
            foreach (var it in l.Items.Where(i => i.Member == m.Name && PlanKinds.Accepts(i.Kind, m)))
                res.Add((l, it));
        return res;
    }

    /// <summary>Nhóm cây thư mục chứa cấu kiện m ứng với mặt bằng lv (vị trí đang chọn); null nếu không có.</summary>
    public static PlanTreeGroup GroupFor(List<PlanTreeGroup> groups, IDetailMember m, PlanLevel lv, PlanItem it = null)
    {
        if (groups == null || m == null) return null;
        var cands = groups.Where(g => g.Members.Contains(m)).ToList();
        if (it != null && it.Kind == PlanKinds.Column) return cands.FirstOrDefault(g => g.Kind == "Cot") ?? cands.FirstOrDefault();
        return cands.FirstOrDefault(g => g.Level == lv) ?? cands.FirstOrDefault(g => g.Level?.Name == lv?.Name) ?? cands.FirstOrDefault();
    }

    /// <summary>Tên mới chưa dùng cho tiền tố (D, C, S, LT, OV…).</summary>
    public static string NextName(DetailingProject p, string prefix)
    {
        var names = new HashSet<string>(p?.All.Select(m => m.Name) ?? Enumerable.Empty<string>());
        for (int k = 1; ; k++) if (!names.Contains(prefix + k.ToString(CultureInfo.InvariantCulture))) return prefix + k.ToString(CultureInfo.InvariantCulture);
    }
}

using System.Globalization;
using KetCauVXT.Model;

namespace KetCauVXT.Import.Arch;

// =====================================================================
// MỞ MẶT BẰNG CAD ĐỂ LẤY DỮ LIỆU: chọn nguồn trục (layer trục / theo cột / người dùng chọn), trục đi qua TIM hay MÉP cột,
// đọc thông số (bước trục, cột + tiết diện + lệch tâm, tường 110/220, công năng ô sàn, lỗ cầu thang / thông tầng)
// rồi ÁP DỤNG vào mô hình (chế độ Thiết kế) hoặc mặt bằng kết cấu (chế độ Triển khai hồ sơ).
// Đơn vị: m (mặt bằng đã đổi đơn vị bởi ArchPlanReader). Giao diện: App/GridFromPlanDialog.cs.
// =====================================================================

/// <summary>Nguồn lấy trục.</summary>
public enum AxisSource
{
    /// <summary>Tự nhận theo layer trục (TRUC / AXIS / GRID… hoặc layer người dùng chọn)</summary>
    Layer,
    /// <summary>Tự nhận theo vị trí cột (gom cột thẳng hàng)</summary>
    Columns,
    /// <summary>Người dùng tự chọn / bấm trục</summary>
    User
}

/// <summary>Trục đi qua đâu của cột.</summary>
public enum AxisThrough
{
    /// <summary>Tim cột</summary>
    Center,
    /// <summary>Mép trái (trục đứng) / mép dưới (trục ngang)</summary>
    EdgeMin,
    /// <summary>Mép phải (trục đứng) / mép trên (trục ngang)</summary>
    EdgeMax,
    /// <summary>Mép ngoài nhà ở trục biên, tim cột ở trục giữa</summary>
    Outside
}

/// <summary>Một trục đã chọn.</summary>
public sealed class PickedAxis
{
    public string Name { get; set; } = "";
    /// <summary>GridDir.X = trục đứng 1, 2, 3… (toạ độ X); GridDir.Y = trục ngang A, B, C… (toạ độ Y)</summary>
    public GridDir Dir { get; set; }
    public double Coord { get; set; }
    /// <summary>Riêng trục này đi qua đâu của cột (null = theo lựa chọn chung)</summary>
    public AxisThrough? Through { get; set; }
    /// <summary>"layer" / "cột" / "người dùng"</summary>
    public string Source { get; set; } = "";
    public override string ToString() => $"{Name} ({(Dir == GridDir.X ? "X" : "Y")} = {Coord:0.###})";
}

/// <summary>Cột đọc từ bản vẽ, gắn vào giao trục.</summary>
public sealed class PlanColumnInfo
{
    /// <summary>Tâm cột thật (m, hệ bản vẽ)</summary>
    public double X, Y;
    /// <summary>Kích thước theo X, theo Y (m)</summary>
    public double Bx, By;
    /// <summary>Chỉ số trục X / Y gần nhất (−1 = không thuộc trục nào)</summary>
    public int IX = -1, IY = -1;
    /// <summary>Lệch tâm = tâm cột − trục (m)</summary>
    public double OffX, OffY;
    /// <summary>Tên tiết diện đề xuất, vd. "C22x30" (b theo Y × h theo X như quy ước khung: cạnh H ∥ X)</summary>
    public string Section = "";
    public bool OnGrid => IX >= 0 && IY >= 0;
}

/// <summary>Đoạn tường nằm trên một trục (để gán tải tường lên dầm trục).</summary>
public sealed class PlanWallInfo
{
    public GridDir AxisDir;
    public int Axis;
    /// <summary>Đoạn dọc theo trục (m)</summary>
    public double From, To;
    /// <summary>Chiều dày (m): 0,11 / 0,22</summary>
    public double Thickness;
    public bool Exterior;
    public double Length => To - From;
}

/// <summary>Ô lưới giữa 2 trục X và 2 trục Y: công năng (theo chữ tên phòng), lỗ cầu thang / thông tầng.</summary>
public sealed class PlanBayInfo
{
    public int IX, IY;
    public double X0, Y0, X1, Y1;
    public string RoomText = "";
    public RoomKind Kind = RoomKind.Unknown;
    public SlabUse Use = SlabUse.Normal;
    /// <summary>Không có sàn: cầu thang / thông tầng</summary>
    public OpeningKind? Opening;
    public double Cx => (X0 + X1) / 2;
    public double Cy => (Y0 + Y1) / 2;
}

public sealed class AxisPickOptions
{
    public AxisSource Source { get; set; } = AxisSource.Layer;
    public AxisThrough Through { get; set; } = AxisThrough.Center;
    /// <summary>Sai số gom / bắt (m)</summary>
    public double Tolerance { get; set; } = 0.05;
    /// <summary>Làm tròn bước trục (m)</summary>
    public double RoundM { get; set; } = 0.01;
    /// <summary>Làm tròn kích thước cột (m)</summary>
    public double ColumnRound { get; set; } = 0.005;
}

/// <summary>Kết quả đọc mặt bằng theo lựa chọn trục.</summary>
public sealed class AxisPickResult
{
    public List<PickedAxis> AxesX { get; } = new();
    public List<PickedAxis> AxesY { get; } = new();
    public List<double> SpansX { get; } = new();
    public List<double> SpansY { get; } = new();
    public List<PlanColumnInfo> Columns { get; } = new();
    public List<PlanWallInfo> Walls { get; } = new();
    public List<PlanBayInfo> Bays { get; } = new();
    public List<string> Notes { get; } = new();
    public string SpansXText => GridFromPlan.SpansText(SpansX);
    public string SpansYText => GridFromPlan.SpansText(SpansY);
    /// <summary>Gốc toạ độ = giao trục đầu tiên (X₁, Y_A) trên bản vẽ</summary>
    public double OriginX => AxesX.Count > 0 ? AxesX[0].Coord : 0;
    public double OriginY => AxesY.Count > 0 ? AxesY[0].Coord : 0;
    public int OffsetColumns => Columns.Count(c => c.OnGrid && (Math.Abs(c.OffX) > 1e-4 || Math.Abs(c.OffY) > 1e-4));

    /// <summary>Bảng tóm tắt (hạng mục, nội dung) cho hộp thoại.</summary>
    public List<(string Key, string Item, string Detail)> Summary()
    {
        var l = new List<(string, string, string)>
        {
            ("grid", "Lưới trục", $"X: {AxesX.Count} trục ({SpansXText}) · Y: {AxesY.Count} trục ({SpansYText})"),
            ("columns", "Cột", Columns.Count == 0 ? "không có" : $"{Columns.Count(c => c.OnGrid)} cột trên giao trục ({Columns.Count(c => !c.OnGrid)} ngoài trục); tiết diện: "
                + string.Join(", ", Columns.Where(c => c.OnGrid).GroupBy(c => c.Section).Select(g => $"{g.Key} ×{g.Count()}"))
                + (OffsetColumns > 0 ? $"; {OffsetColumns} cột lệch tâm so với trục" : "")),
            ("walls", "Tường trên trục", Walls.Count == 0 ? "không có" : string.Join(", ", Walls.GroupBy(w => Math.Round(w.Thickness * 1000)).OrderBy(g => g.Key)
                .Select(g => $"tường {g.Key:0}: {g.Sum(w => w.Length):0.0} m"))),
            ("rooms", "Công năng ô sàn", Bays.Count(b => b.Kind != RoomKind.Unknown && b.Opening == null) == 0 ? "không đọc được tên phòng"
                : string.Join(", ", Bays.Where(b => b.Kind != RoomKind.Unknown && b.Opening == null).GroupBy(b => b.Use).Select(g => $"{ModelEdit.BuildUpName(g.Key)} ×{g.Count()}"))),
            ("openings", "Lỗ sàn (cầu thang / thông tầng)", Bays.Count(b => b.Opening != null) == 0 ? "không có"
                : string.Join(", ", Bays.Where(b => b.Opening != null).Select(b => $"{(b.Opening == OpeningKind.Stair ? "thang" : "thông tầng")} ô {b.IX + 1}-{b.IY + 1}")))
        };
        return l;
    }
}

/// <summary>Lựa chọn áp dụng vào mô hình.</summary>
public sealed class AxisApplyOptions
{
    public bool Grid { get; set; } = true;
    public bool ColumnSections { get; set; } = true;
    public bool ColumnOffsets { get; set; } = true;
    /// <summary>Xoá cột ở giao trục bản vẽ không có cột (chỉ khi bản vẽ có cột)</summary>
    public bool RemoveColumnsWithoutCad { get; set; }
    public bool Walls { get; set; } = true;
    public bool RoomUses { get; set; } = true;
    public bool Openings { get; set; } = true;
    public string Concrete { get; set; } = "B25";
}

public static class PlanAxisPicker
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ------------------------------------------------------------------
    // ĐƠN VỊ
    // ------------------------------------------------------------------
    /// <summary>Hệ số đơn vị bản vẽ → m: theo $INSUNITS (4 mm, 5 cm, 6 m, 1 inch, 2 feet); không khai báo → đoán theo phạm vi bản vẽ
    /// (nhà dân dụng 5–100 m: phạm vi số &gt; 2000 → mm; 200…2000 → cm; nhỏ hơn → m).</summary>
    public static double UnitFactor(int insUnits, double rawExtent)
    {
        switch (insUnits)
        {
            case 4: return 0.001;
            case 5: return 0.01;
            case 6: return 1;
            case 1: return 0.0254;
            case 2: return 0.3048;
        }
        if (rawExtent > 2000) return 0.001;
        if (rawExtent > 200) return 0.01;
        return 1;
    }

    public static string UnitName(double factor) => Math.Abs(factor - 0.001) < 1e-9 ? "mm" : Math.Abs(factor - 0.01) < 1e-9 ? "cm" : Math.Abs(factor - 1) < 1e-9 ? "m"
        : factor.ToString("0.####", Inv) + " m/đv";

    /// <summary>Nhân toàn bộ toạ độ / kích thước của mặt bằng (đã đọc) với k – dùng khi người dùng sửa đơn vị.</summary>
    public static void Rescale(ArchFloorPlan p, double k)
    {
        if (p == null || Math.Abs(k - 1) < 1e-12) return;
        foreach (var w in p.Walls) { w.Ax *= k; w.Ay *= k; w.Bx *= k; w.By *= k; w.Thickness *= k; w.OpeningLength *= k; w.Sagitta *= k; }
        foreach (var g in p.Grids) g.Coord *= k;
        foreach (var c in p.Columns) { c.X *= k; c.Y *= k; c.Bx *= k; c.By *= k; c.Diameter *= k; }
        foreach (var r in p.Rooms) { r.X *= k; r.Y *= k; }
        foreach (var z in p.Zones) { z.X0 *= k; z.Y0 *= k; z.X1 *= k; z.Y1 *= k; }
        foreach (var o in p.Openings) { o.X *= k; o.Y *= k; o.Width *= k; }
        foreach (var b in p.Beams) { b.Ax *= k; b.Ay *= k; b.Bx *= k; b.By *= k; b.Width *= k; }
        for (int i = 0; i < p.Preview.Count; i++) { var l = p.Preview[i]; p.Preview[i] = new PreviewLine(l.X1 * k, l.Y1 * k, l.X2 * k, l.Y2 * k, l.Kind); }
    }

    // ------------------------------------------------------------------
    // TRỤC
    // ------------------------------------------------------------------
    /// <summary>Trục từ layer trục của bản vẽ (bỏ trục tự lập theo tường trừ khi không có trục nào).</summary>
    public static (List<PickedAxis> X, List<PickedAxis> Y) FromLayer(ArchFloorPlan p, double tol = 0.05)
    {
        var gs = p?.Grids ?? new List<ArchGrid>();
        var real = gs.Where(g => !g.Auto).ToList();
        if (real.Count == 0) real = gs;
        List<PickedAxis> Of(GridDir d) => Dedup(real.Where(g => g.Dir == d).Select(g => new PickedAxis { Name = g.Name ?? "", Dir = d, Coord = g.Coord, Source = g.Auto ? "tường" : "layer" }), tol);
        return (Of(GridDir.X), Of(GridDir.Y));
    }

    /// <summary>Toạ độ tham chiếu của cột theo phương (tâm / mép) cho lựa chọn "trục đi qua".</summary>
    public static double RefCoord(double centre, double size, AxisThrough t, bool minSide) => t switch
    {
        AxisThrough.EdgeMin => centre - size / 2,
        AxisThrough.EdgeMax => centre + size / 2,
        AxisThrough.Outside => minSide ? centre - size / 2 : centre + size / 2,
        _ => centre
    };

    /// <summary>Lập trục từ vị trí cột: gom tâm cột thẳng hàng (sai số tol) thành hàng; trục đặt tại tim / mép cột theo lựa chọn.
    /// "Mép ngoài nhà": hàng biên (nhỏ nhất / lớn nhất) lấy mép ngoài, hàng giữa lấy tim.</summary>
    public static (List<PickedAxis> X, List<PickedAxis> Y) FromColumns(IList<ArchColumn> cols, AxisThrough through, double tol = 0.05)
    {
        var rx = new List<PickedAxis>(); var ry = new List<PickedAxis>();
        if (cols == null || cols.Count == 0) return (rx, ry);
        foreach (var dir in new[] { GridDir.X, GridDir.Y })
        {
            var rows = Cluster(cols.Select(c => dir == GridDir.X ? c.X : c.Y).ToList(), tol);
            var res = dir == GridDir.X ? rx : ry;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var members = cols.Where(c => Math.Abs((dir == GridDir.X ? c.X : c.Y) - row) <= tol + 1e-9).ToList();
                AxisThrough t = through;
                bool minSide = i == 0;
                if (through == AxisThrough.Outside && i != 0 && i != rows.Count - 1) t = AxisThrough.Center;
                if (through == AxisThrough.Outside && rows.Count == 1) t = AxisThrough.Center;
                var refs = members.Select(c => RefCoord(dir == GridDir.X ? c.X : c.Y, dir == GridDir.X ? c.Bx : c.By, t, minSide)).OrderBy(v => v).ToList();
                double coord = refs[refs.Count / 2];   // trung vị – bền với cột lệch
                res.Add(new PickedAxis { Dir = dir, Coord = Math.Round(coord, 4), Through = t == through ? null : t, Source = "cột" });
            }
        }
        AutoName(rx); AutoName(ry);
        return (rx, ry);
    }

    /// <summary>Gom các giá trị gần nhau (≤ tol) → trung bình mỗi nhóm, tăng dần.</summary>
    public static List<double> Cluster(List<double> vals, double tol)
    {
        var s = vals.Where(double.IsFinite).OrderBy(v => v).ToList();
        var res = new List<double>();
        int i = 0;
        while (i < s.Count)
        {
            int j = i;
            while (j + 1 < s.Count && s[j + 1] - s[j] <= tol + 1e-9) j++;
            res.Add(s.Skip(i).Take(j - i + 1).Average());
            i = j + 1;
        }
        return res;
    }

    private static List<PickedAxis> Dedup(IEnumerable<PickedAxis> a, double tol)
    {
        var res = new List<PickedAxis>();
        foreach (var x in a.OrderBy(q => q.Coord))
            if (res.Count == 0 || x.Coord - res[^1].Coord > tol) res.Add(x);
        return res;
    }

    /// <summary>Người dùng bấm một nét (2 điểm) làm trục: nét gần đứng → trục X (toạ độ = trung bình X), gần ngang → trục Y.</summary>
    public static PickedAxis AxisFromSegment(double x1, double y1, double x2, double y2)
    {
        bool vertical = Math.Abs(x2 - x1) <= Math.Abs(y2 - y1);
        return vertical ? new PickedAxis { Dir = GridDir.X, Coord = Math.Round((x1 + x2) / 2, 4), Source = "người dùng" }
                        : new PickedAxis { Dir = GridDir.Y, Coord = Math.Round((y1 + y2) / 2, 4), Source = "người dùng" };
    }

    /// <summary>Người dùng bấm vào cột → trục qua cột (tim / mép theo lựa chọn; "mép ngoài nhà" xét so với tâm toàn bộ cột).</summary>
    public static PickedAxis AxisThroughColumn(ArchColumn c, GridDir dir, AxisThrough through, IList<ArchColumn> all = null)
    {
        double centre = dir == GridDir.X ? c.X : c.Y, size = dir == GridDir.X ? c.Bx : c.By;
        bool minSide = true;
        if (through == AxisThrough.Outside && all != null && all.Count > 0)
        {
            double mid = all.Average(q => dir == GridDir.X ? q.X : q.Y);
            minSide = centre <= mid;
        }
        return new PickedAxis { Dir = dir, Coord = Math.Round(RefCoord(centre, size, through, minSide), 4), Through = through, Source = "người dùng" };
    }

    /// <summary>Cột gần điểm bấm nhất (trong cột hoặc cách mép ≤ tol).</summary>
    public static ArchColumn ColumnAt(IEnumerable<ArchColumn> cols, double x, double y, double tol = 0.1) =>
        cols?.Where(c => Math.Abs(x - c.X) <= c.Bx / 2 + tol && Math.Abs(y - c.Y) <= c.By / 2 + tol)
             .OrderBy(c => (x - c.X) * (x - c.X) + (y - c.Y) * (y - c.Y)).FirstOrDefault();

    /// <summary>Thêm trục (bỏ nếu trùng trục có sẵn trong sai số), sắp xếp lại.</summary>
    public static bool AddAxis(List<PickedAxis> list, PickedAxis a, double tol = 0.05)
    {
        if (a == null || list.Any(q => q.Dir == a.Dir && Math.Abs(q.Coord - a.Coord) <= tol)) return false;
        list.Add(a);
        list.Sort((p, q) => p.Coord.CompareTo(q.Coord));
        return true;
    }

    /// <summary>Đặt tên tự động: trục X "1, 2, 3…", trục Y "A, B, C… Z, AA…"; giữ tên người dùng đã đặt nếu keepNamed.</summary>
    public static void AutoName(List<PickedAxis> list, bool keepNamed = false)
    {
        list.Sort((p, q) => p.Coord.CompareTo(q.Coord));
        for (int i = 0; i < list.Count; i++)
        {
            if (keepNamed && !string.IsNullOrWhiteSpace(list[i].Name)) continue;
            list[i].Name = list[i].Dir == GridDir.X ? (i + 1).ToString(Inv) : LetterName(i);
        }
    }

    public static string LetterName(int i)
    {
        string s = ""; i++;
        while (i > 0) { int r = (i - 1) % 26; s = (char)('A' + r) + s; i = (i - 1) / 26; }
        return s;
    }

    // ------------------------------------------------------------------
    // ĐỌC THÔNG SỐ
    // ------------------------------------------------------------------
    /// <summary>Đọc toàn bộ thông số mặt bằng theo lựa chọn trục. userAxes: trục người dùng chọn (Source = User).</summary>
    public static AxisPickResult Read(ArchFloorPlan p, AxisPickOptions o, IEnumerable<PickedAxis> userAxes = null)
    {
        o ??= new AxisPickOptions();
        var r = new AxisPickResult();
        if (p == null) { r.Notes.Add("Chưa có mặt bằng."); return r; }
        List<PickedAxis> ax, ay;
        switch (o.Source)
        {
            case AxisSource.Columns:
                (ax, ay) = FromColumns(p.Columns, o.Through, o.Tolerance);
                if (p.Columns.Count == 0) r.Notes.Add("Bản vẽ không có cột (layer COT / COLUMN) – không lập được trục theo cột.");
                break;
            case AxisSource.User:
                var ua = (userAxes ?? Enumerable.Empty<PickedAxis>()).ToList();
                ax = Dedup(ua.Where(a => a.Dir == GridDir.X), o.Tolerance); ay = Dedup(ua.Where(a => a.Dir == GridDir.Y), o.Tolerance);
                if (ua.Count == 0) r.Notes.Add("Chưa chọn trục: bấm lên nét trục / cột trên hình (chế độ Người dùng chọn).");
                break;
            default:
                (ax, ay) = FromLayer(p, o.Tolerance);
                if (p.Grids.Count > 0 && p.Grids.All(g => g.Auto)) r.Notes.Add("Bản vẽ không có layer trục – trục lập theo tim tường chính; nên chọn \"Tự nhận theo cột\" hoặc tự chọn trục.");
                break;
        }
        // tên: giữ tên đọc được nếu đủ và không trùng, còn lại đặt tự động
        foreach (var l in new[] { ax, ay })
        {
            bool named = l.All(a => !string.IsNullOrWhiteSpace(a.Name)) && l.Select(a => a.Name).Distinct().Count() == l.Count;
            AutoName(l, named);
        }
        r.AxesX.AddRange(ax); r.AxesY.AddRange(ay);
        r.SpansX.AddRange(GridFromPlan.Spans(ax.Select(a => a.Coord), o.RoundM));
        r.SpansY.AddRange(GridFromPlan.Spans(ay.Select(a => a.Coord), o.RoundM));
        if (r.SpansX.Count == 0) r.Notes.Add("Chưa đủ ≥ 2 trục phương X (trục đứng 1, 2, 3…).");
        if (r.SpansY.Count == 0) r.Notes.Add("Chưa đủ ≥ 2 trục phương Y (trục ngang A, B, C…).");

        // cột → giao trục + lệch tâm
        foreach (var c in p.Columns)
        {
            var ci = new PlanColumnInfo { X = c.X, Y = c.Y, Bx = RoundTo(c.Bx, o.ColumnRound), By = RoundTo(c.By, o.ColumnRound) };
            (ci.IX, ci.OffX) = Attach(ax, c.X, ci.Bx, o, r.AxesX.Count);
            (ci.IY, ci.OffY) = Attach(ay, c.Y, ci.By, o, r.AxesY.Count);
            ci.Section = ImportGeometry.RectName("C", ci.By, ci.Bx);
            r.Columns.Add(ci);
        }
        int off = r.Columns.Count(c => !c.OnGrid);
        if (off > 0) r.Notes.Add($"{off} cột không nằm trên giao trục nào (cột phụ / cột trang trí) – không áp dụng.");

        // tường trên trục
        foreach (var w in p.Walls.Where(w => w.IsOrthogonal && w.Length >= 0.3))
        {
            bool alongY = w.AlongY;   // tường dọc phương Y nằm trên trục X
            var axes = alongY ? ax : ay;
            double c = alongY ? w.Ax : w.Ay;
            int k = -1; double best = double.MaxValue;
            for (int i = 0; i < axes.Count; i++)
            {
                double d = Math.Abs(axes[i].Coord - c);
                if (d <= w.Thickness / 2 + o.Tolerance && d < best) { best = d; k = i; }
            }
            if (k < 0) continue;
            double a0 = alongY ? Math.Min(w.Ay, w.By) : Math.Min(w.Ax, w.Bx), a1 = alongY ? Math.Max(w.Ay, w.By) : Math.Max(w.Ax, w.Bx);
            var cross = alongY ? ay : ax;
            if (cross.Count >= 2) { a0 = Math.Max(a0, cross[0].Coord); a1 = Math.Min(a1, cross[^1].Coord); }
            if (a1 - a0 < 0.3) continue;
            r.Walls.Add(new PlanWallInfo { AxisDir = alongY ? GridDir.X : GridDir.Y, Axis = k, From = Math.Round(a0, 4), To = Math.Round(a1, 4),
                                           Thickness = w.Thickness <= 0.165 ? 0.11 : 0.22, Exterior = w.Exterior });
        }
        MergeWalls(r.Walls);

        // ô lưới: công năng, lỗ sàn
        for (int i = 0; i + 1 < ax.Count; i++)
            for (int j = 0; j + 1 < ay.Count; j++)
            {
                var b = new PlanBayInfo { IX = i, IY = j, X0 = ax[i].Coord, X1 = ax[i + 1].Coord, Y0 = ay[j].Coord, Y1 = ay[j + 1].Coord };
                var room = p.Rooms.Where(q => q.X > b.X0 && q.X < b.X1 && q.Y > b.Y0 && q.Y < b.Y1 && q.Kind != RoomKind.Unknown)
                                  .OrderBy(q => (q.X - b.Cx) * (q.X - b.Cx) + (q.Y - b.Cy) * (q.Y - b.Cy)).FirstOrDefault();
                if (room != null) { b.RoomText = room.Text; b.Kind = room.Kind; b.Use = UseOf(room.Kind); }
                var z = p.Zones.FirstOrDefault(q => q.Contains(b.Cx, b.Cy) && (q.Kind == RoomKind.Stair || q.Kind == RoomKind.Void));
                if (z != null) b.Opening = z.Kind == RoomKind.Stair ? OpeningKind.Stair : OpeningKind.Void;
                else if (room != null && room.Kind is RoomKind.Stair or RoomKind.Void) b.Opening = room.Kind == RoomKind.Stair ? OpeningKind.Stair : OpeningKind.Void;
                r.Bays.Add(b);
            }
        return r;
    }

    private static double RoundTo(double v, double step) => step > 0 ? Math.Round(Math.Round(v / step) * step, 4) : v;

    /// <summary>Gắn cột vào trục gần nhất (tâm cột cách trục ≤ b/2 + tol) và tính lệch tâm; lệch tâm bắt về 0 / ±b/2 nếu gần.</summary>
    private static (int Index, double Off) Attach(List<PickedAxis> axes, double centre, double size, AxisPickOptions o, int n)
    {
        int k = -1; double best = double.MaxValue;
        for (int i = 0; i < axes.Count; i++)
        {
            double d = Math.Abs(centre - axes[i].Coord);
            if (d <= size / 2 + o.Tolerance && d < best) { best = d; k = i; }
        }
        if (k < 0) return (-1, 0);
        double off = centre - axes[k].Coord;
        if (Math.Abs(off) <= o.Tolerance / 2) off = 0;
        else if (Math.Abs(Math.Abs(off) - size / 2) <= o.Tolerance / 2) off = Math.Sign(off) * size / 2;
        else off = Math.Round(off * 200) / 200;   // 5 mm
        return (k, Math.Round(off, 4));
    }

    /// <summary>Gộp đoạn tường cùng trục, cùng chiều dày, chồng / nối nhau.</summary>
    private static void MergeWalls(List<PlanWallInfo> ws)
    {
        var res = new List<PlanWallInfo>();
        foreach (var g in ws.GroupBy(w => (w.AxisDir, w.Axis, w.Thickness)))
        {
            PlanWallInfo cur = null;
            foreach (var w in g.OrderBy(q => q.From))
            {
                if (cur != null && w.From <= cur.To + 0.05) { cur.To = Math.Max(cur.To, w.To); cur.Exterior |= w.Exterior; }
                else { cur = new PlanWallInfo { AxisDir = w.AxisDir, Axis = w.Axis, From = w.From, To = w.To, Thickness = w.Thickness, Exterior = w.Exterior }; res.Add(cur); }
            }
        }
        ws.Clear(); ws.AddRange(res);
    }

    /// <summary>Công năng phòng → công năng ô sàn của mô hình (tải hoàn thiện / hoạt tải).</summary>
    public static SlabUse UseOf(RoomKind k) => k switch
    {
        RoomKind.WC => SlabUse.WC,
        RoomKind.Balcony => SlabUse.Balcony,
        RoomKind.Corridor or RoomKind.Stair => SlabUse.Corridor,
        RoomKind.Storage or RoomKind.Garage or RoomKind.Shop => SlabUse.Storage,
        RoomKind.RoofTerrace => SlabUse.Roof,
        _ => SlabUse.Normal
    };

    // ------------------------------------------------------------------
    // ÁP DỤNG – CHẾ ĐỘ THIẾT KẾ (mô hình khung)
    // ------------------------------------------------------------------
    /// <summary>Áp dụng thông số mặt bằng lên mô hình ĐÃ dựng theo lưới (ModelBuilder.SetGrid + BuildFromGrid với SpansX/SpansY của r):
    /// tiết diện + lệch tâm cột ở giao trục có cột, (tuỳ chọn) xoá cột ở giao trục không có cột, tường trên trục → tải tường lên dầm,
    /// công năng ô sàn, lỗ cầu thang / thông tầng. Mặt bằng điển hình: áp cho mọi tầng (tường, công năng, lỗ: trừ tầng mái).
    /// Trả về danh sách thay đổi.</summary>
    public static List<string> ApplyToProject(Project p, AxisPickResult r, AxisApplyOptions o = null)
    {
        o ??= new AxisApplyOptions();
        var log = new List<string>();
        if (p == null || r == null) return log;
        var gx = p.Grids.Where(g => g.Dir == GridDir.X).OrderBy(g => g.Coord).ToList();
        var gy = p.Grids.Where(g => g.Dir == GridDir.Y).OrderBy(g => g.Coord).ToList();
        if (gx.Count != r.AxesX.Count || gy.Count != r.AxesY.Count)
        {
            log.Add($"Số trục mô hình ({gx.Count}×{gy.Count}) khác số trục đọc được ({r.AxesX.Count}×{r.AxesY.Count}) – dựng lại mô hình theo lưới mới trước khi áp dụng.");
            return log;
        }
        // tên trục theo bản vẽ
        if (o.Grid)
        {
            for (int i = 0; i < gx.Count; i++) gx[i].Name = r.AxesX[i].Name;
            for (int j = 0; j < gy.Count; j++) gy[j].Name = r.AxesY[j].Name;
            log.Add($"Lưới trục: {gx.Count} trục X ({r.SpansXText}), {gy.Count} trục Y ({r.SpansYText}); tên trục theo bản vẽ.");
        }
        var cols = p.Frames.Where(f => f.IsVerticalKind).ToList();
        (int I, int J) IndexOf(double x, double y)
        {
            int ii = gx.FindIndex(g => Math.Abs(g.Coord - x) < 0.01), jj = gy.FindIndex(g => Math.Abs(g.Coord - y) < 0.01);
            return (ii, jj);
        }
        var cadAt = r.Columns.Where(c => c.OnGrid).GroupBy(c => (c.IX, c.IY)).ToDictionary(g => g.Key, g => g.First());
        int nSec = 0, nOff = 0, nDel = 0;
        foreach (var f in cols)
        {
            var a = p.Nodes.FirstOrDefault(n => n.Id == f.NodeI);
            if (a == null) continue;
            var (i, j) = IndexOf(a.X, a.Y);
            if (i < 0 || j < 0) continue;
            if (cadAt.TryGetValue((i, j), out var c))
            {
                if (o.ColumnSections && c.Bx > 0.05 && c.By > 0.05)
                {
                    string sec = ImportGeometry.EnsureRect(p, "C", c.By, c.Bx, o.Concrete);
                    if (f.Section != sec) { f.Section = sec; f.Angle = 0; nSec++; }
                }
                if (o.ColumnOffsets && (Math.Abs(f.ColOffsetX - c.OffX) > 1e-6 || Math.Abs(f.ColOffsetY - c.OffY) > 1e-6))
                { f.ColOffsetX = c.OffX; f.ColOffsetY = c.OffY; nOff++; }
            }
            else if (o.RemoveColumnsWithoutCad && cadAt.Count > 0)
            {
                ModelEdit.DeleteFrame(p, f); nDel++;
            }
        }
        if (nSec > 0) log.Add($"Cột: gán tiết diện theo bản vẽ cho {nSec} thanh cột ({string.Join(", ", cadAt.Values.Select(c => c.Section).Distinct())}).");
        if (nOff > 0) log.Add($"Cột: lệch tâm so với trục cho {nOff} thanh cột (trục qua mép cột) – vẽ mặt bằng / móng theo vị trí thật, phân tích theo trục.");
        if (nDel > 0) log.Add($"Cột: xoá {nDel} thanh cột ở giao trục bản vẽ không có cột (dầm vẫn liên tục qua nút).");

        var stories = p.Stories.OrderBy(s => s.Elevation).ToList();
        var typical = stories.Count > 1 ? stories.Take(stories.Count - 1).ToList() : stories;   // trừ tầng mái
        if (o.Walls && r.Walls.Count > 0)
        {
            int nw = 0;
            foreach (var st in typical)
                foreach (var w in r.Walls)
                {
                    double x1, y1, x2, y2;
                    if (w.AxisDir == GridDir.X) { x1 = x2 = gx[w.Axis].Coord; y1 = gy[0].Coord + (w.From - r.OriginY); y2 = gy[0].Coord + (w.To - r.OriginY); }
                    else { y1 = y2 = gy[w.Axis].Coord; x1 = gx[0].Coord + (w.From - r.OriginX); x2 = gx[0].Coord + (w.To - r.OriginX); }
                    string type = WallTypeFor(p, w.Thickness);
                    if (type == null) continue;
                    p.Walls.RemoveAll(q => q.Story == st.Name && Math.Abs(q.X1 - x1) < 0.02 && Math.Abs(q.Y1 - y1) < 0.02 && Math.Abs(q.X2 - x2) < 0.02 && Math.Abs(q.Y2 - y2) < 0.02);
                    if (ModelEdit.AddWall(p, st.Name, x1, y1, x2, y2, type).Ok) nw++;
                }
            log.Add($"Tường: {nw} đoạn tường trên trục ({r.Walls.Count} đoạn × {typical.Count} tầng) → tải tường lên dầm.");
        }
        if (o.RoomUses || o.Openings)
        {
            int nu = 0, no = 0;
            foreach (var st in typical)
                foreach (var b in r.Bays)
                {
                    double cx = gx[0].Coord + (b.Cx - r.OriginX), cy = gy[0].Coord + (b.Cy - r.OriginY);
                    if (o.Openings && b.Opening != null)
                    {
                        if (ModelEdit.AddOpening(p, st.Name, cx, cy, b.Opening.Value).Ok) no++;
                        continue;
                    }
                    if (o.RoomUses && b.Kind != RoomKind.Unknown)
                    {
                        var s = ModelEdit.SlabAt(p, st.Name, cx, cy);
                        if (s != null && ModelEdit.SetSlabUse(p, s, b.Use).Ok) nu++;
                    }
                }
            if (nu > 0) log.Add($"Sàn: gán công năng theo tên phòng cho {nu} ô sàn (tải hoàn thiện + hoạt tải theo công năng).");
            if (no > 0) log.Add($"Sàn: {no} lỗ sàn cầu thang / thông tầng.");
        }
        p.InvalidateIndex();
        return log;
    }

    /// <summary>Loại tường phù hợp chiều dày (ưu tiên "trát 2 mặt").</summary>
    public static string WallTypeFor(Project p, double t)
    {
        var c = p.WallTypes.Where(w => Math.Abs(w.Thickness - t) < 0.02).ToList();
        return (c.FirstOrDefault(w => w.Name.Contains("trát 2 mặt")) ?? c.FirstOrDefault())?.Name;
    }

    // ------------------------------------------------------------------
    // ÁP DỤNG – CHẾ ĐỘ TRIỂN KHAI HỒ SƠ (mặt bằng kết cấu)
    // ------------------------------------------------------------------
    /// <summary>Đặt bước trục của mặt bằng kết cấu hồ sơ và (tuỳ chọn) đặt cột ở vị trí thật (giao trục + lệch tâm) lên tầng lv:
    /// cột cùng tiết diện dùng chung cấu kiện (tìm theo B × H, chưa có thì tạo C1, C2…). Toạ độ quy về gốc giao trục đầu tiên.</summary>
    public static List<string> ApplyToDetailing(KetCauVXT.Detailing.DetailingProject dp, KetCauVXT.Detailing.PlanLevel lv, AxisPickResult r, bool columns = true, bool replaceColumns = true)
    {
        var log = new List<string>();
        if (dp == null || r == null) return log;
        var plan = dp.Plan ??= new KetCauVXT.Detailing.StructuralPlan();
        if (r.SpansX.Count > 0) plan.SpansX = r.SpansXText;
        if (r.SpansY.Count > 0) plan.SpansY = r.SpansYText;
        log.Add($"Mặt bằng kết cấu: bước trục X = {plan.SpansX}, Y = {plan.SpansY}.");
        if (!columns || lv == null) return log;
        if (replaceColumns) lv.Items.RemoveAll(i => i.Kind == KetCauVXT.Detailing.PlanKinds.Column);
        int n = 0;
        foreach (var c in r.Columns.Where(c => c.OnGrid))
        {
            var m = dp.Columns.FirstOrDefault(q => Math.Abs(q.B - c.Bx) < 1e-6 && Math.Abs(q.H - c.By) < 1e-6);
            if (m == null)
            {
                m = new KetCauVXT.Detailing.ManualColumn { Name = KetCauVXT.Detailing.PlanMemberMap.NextName(dp, "C"), B = c.Bx, H = c.By, Quantity = 1 };
                dp.Columns.Add(m);
            }
            lv.Items.Add(new KetCauVXT.Detailing.PlanItem
            {
                Kind = KetCauVXT.Detailing.PlanKinds.Column, Member = m.Name,
                X1 = Math.Round(r.AxesX[c.IX].Coord - r.OriginX + c.OffX, 4), Y1 = Math.Round(r.AxesY[c.IY].Coord - r.OriginY + c.OffY, 4)
            });
            n++;
        }
        if (n > 0) log.Add($"{lv.Name}: đặt {n} cột tại vị trí thật (giao trục + lệch tâm).");
        return log;
    }
}

using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

// =====================================================================
// HỒ SƠ THEO BỘ MÔN: Kiến trúc (KT-), Kết cấu (KC-), Điện nước (DN-).
// Mỗi bộ môn là một phần của hồ sơ; chọn bộ môn cần xuất. Tuỳ chọn hiện bộ môn khác làm nền mờ (layer TRUC màu 8 xám, nét liền / nét đứt)
// trên các mặt bằng – để đối chiếu va chạm khi in.
// Bộ môn Điện nước đăng ký bản vẽ của mình qua DisciplineDocs.MepSheets; chưa có → tờ liệt kê thiết bị điện nước.
// =====================================================================
public sealed class DisciplineDocOptions
{
    public List<Discipline> Disciplines { get; set; } = new() { Discipline.KienTruc, Discipline.KetCau, Discipline.DienNuoc };
    /// <summary>Vẽ bộ môn khác làm nền mờ trên mặt bằng</summary>
    public bool ShowOthers { get; set; } = true;
    /// <summary>Mẫu số tỷ lệ mặt bằng</summary>
    public double PlanScale { get; set; } = 100;
    /// <summary>Định dạng / gộp file (null = mặc định DXF tách rời)</summary>
    public DrawingExportOptions Export { get; set; }
}

public static class DisciplineDocs
{
    /// <summary>Layer nền tham chiếu: dùng layer TRUC (màu 8, xám) với kiểu nét ghi đè – có sẵn trong bảng layer mọi bản vẽ.</summary>
    public const string RefLayer = Lay.Truc;

    /// <summary>Bản vẽ của bộ môn Điện nước (do Model/Mep đăng ký). Tham số: dự án, tuỳ chọn; trả về các tờ (chưa đánh số).</summary>
    public static Func<Project, DisciplineDocOptions, List<DrawingItem>> MepSheets;

    /// <summary>Dựng mọi tờ của các bộ môn chọn (đánh số KT-01…, KC-01…, DN-01…).</summary>
    public static List<DrawingItem> Build(Project p, DesignSummary s, DisciplineDocOptions o = null)
    {
        o ??= new DisciplineDocOptions();
        var items = new List<DrawingItem>();
        foreach (var d in DisciplineView.All.Where(o.Disciplines.Contains))
        {
            List<DrawingItem> part = d switch
            {
                Discipline.KienTruc => Architectural(p, o),
                Discipline.KetCau => DrawingSet.BuildDrawings(p, s).Where(i => i.W != null).ToList(),
                _ => MepSheets != null ? (MepSheets(p, o) ?? new List<DrawingItem>()) : MepPlaceholder(p, o)
            };
            int k = 1;
            string pre = DisciplineView.Prefix(d);
            foreach (var it in part)
            {
                it.Number = $"{pre}-{k++:00}";
                if (!it.FileName.StartsWith(pre + "-", StringComparison.OrdinalIgnoreCase))
                {
                    string baseName = System.Text.RegularExpressions.Regex.Replace(it.FileName ?? "", @"^[A-Z]{2}-\d+_?", "");
                    it.FileName = $"{it.Number}_{(string.IsNullOrEmpty(baseName) ? Slug(it.Title) + ".dxf" : baseName)}";
                }
                items.Add(it);
            }
        }
        return items;
    }

    /// <summary>Xuất các tờ ra thư mục (DXF/DWG theo tuỳ chọn). Trả kết quả xuất.</summary>
    public static DrawingExportResult Export(Project p, DesignSummary s, string folder, DisciplineDocOptions o = null)
    {
        o ??= new DisciplineDocOptions();
        var items = Build(p, s, o);
        Directory.CreateDirectory(folder);
        return DrawingExport.Write(items, folder, o.Export, p.Name);
    }

    private static string Slug(string s) =>
        string.Concat(KetCauVXT.Ui.CommandSearch.Fold(s ?? "BanVe").Select(c => char.IsLetterOrDigit(c) && c < 128 ? c : '_')).Trim('_');

    /// <summary>Lưới trục + bong bóng.</summary>
    private static void Grids(DxfWriter w, Project p, (double X0, double Y0, double X1, double Y1) box)
    {
        double ext = 1500;
        foreach (var g in p.GridsX)
        {
            double x = g.Coord * 1000;
            w.Line(Lay.Truc, x, box.Y0 * 1000 - ext, x, box.Y1 * 1000 + ext, "CENTER");
            w.GridBubble(x, box.Y0 * 1000 - ext - w.BubbleR, g.Name);
        }
        foreach (var g in p.GridsY)
        {
            double y = g.Coord * 1000;
            w.Line(Lay.Truc, box.X0 * 1000 - ext, y, box.X1 * 1000 + ext, y, "CENTER");
            w.GridBubble(box.X0 * 1000 - ext - w.BubbleR, y, g.Name);
        }
    }

    private static (double X0, double Y0, double X1, double Y1) Extents(Project p)
    {
        var xs = p.Grids.Where(g => g.Dir == GridDir.X).Select(g => g.Coord).Concat(p.Walls.SelectMany(w => new[] { w.X1, w.X2 })).Concat(p.Nodes.Select(n => n.X)).ToList();
        var ys = p.Grids.Where(g => g.Dir == GridDir.Y).Select(g => g.Coord).Concat(p.Walls.SelectMany(w => new[] { w.Y1, w.Y2 })).Concat(p.Nodes.Select(n => n.Y)).ToList();
        if (xs.Count == 0) xs.Add(0);
        if (ys.Count == 0) ys.Add(0);
        return (xs.Min(), ys.Min(), xs.Max(), ys.Max());
    }

    /// <summary>Kết cấu làm nền mờ: cột (chữ nhật), dầm (2 nét đứt).</summary>
    public static void StructureReference(DxfWriter w, Project p, string story)
    {
        foreach (var sol in ModelSolids.Structural(p).Where(x => x.Story == story))
        {
            if (sol.Category == "Cột")
            {
                var pts = sol.Profile.Select(q => ((sol.Origin.X + q[0]) * 1000, (sol.Origin.Y + q[1]) * 1000)).ToList();
                w.Polyline(RefLayer, pts, true, 0, "CONTINUOUS");
            }
            else if (sol.Category == "Dầm")
            {
                var (b, _) = sol.Rings();
                double hw = (sol.Profile.Max(q => q[0]) - sol.Profile.Min(q => q[0])) / 2;
                var a = sol.Origin; var e = sol.Origin + sol.ZDir * sol.Depth;
                var n = sol.XDir;
                w.Line(RefLayer, (a.X + n.X * hw) * 1000, (a.Y + n.Y * hw) * 1000, (e.X + n.X * hw) * 1000, (e.Y + n.Y * hw) * 1000, "DASHED");
                w.Line(RefLayer, (a.X - n.X * hw) * 1000, (a.Y - n.Y * hw) * 1000, (e.X - n.X * hw) * 1000, (e.Y - n.Y * hw) * 1000, "DASHED");
                _ = b;
            }
        }
    }

    /// <summary>Thiết bị / đồ đạc (khối bao) của một bộ môn trên mặt bằng.</summary>
    public static int Furniture(DxfWriter w, Project p, string story, Discipline d, string layer, bool label = true)
    {
        int n = 0;
        foreach (var f in (p.Arch?.Furniture ?? new List<FurnitureItem>()).Where(f => f.Story == story && f.Discipline == d))
        {
            double a = f.Rotation * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            var pts = new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }
                .Select(q => ((f.X + c * q.Item1 * f.Width / 2 - s * q.Item2 * f.Depth / 2) * 1000, (f.Y + s * q.Item1 * f.Width / 2 + c * q.Item2 * f.Depth / 2) * 1000)).ToList();
            w.Polyline(layer, pts, true, 0, layer == RefLayer ? "CONTINUOUS" : null);
            if (label) w.Text(Lay.Chu, f.X * 1000, f.Y * 1000, w.Th * 0.8, f.Name, 0, 1, 2);
            n++;
        }
        return n;
    }

    /// <summary>Hồ sơ kiến trúc: mặt bằng các tầng (tường, cửa, phòng, đồ đạc), thống kê cửa, thống kê phòng.</summary>
    public static List<DrawingItem> Architectural(Project p, DisciplineDocOptions o)
    {
        var items = new List<DrawingItem>();
        var box = Extents(p);
        var levels = new List<(string Story, string Title)> { ("NEN", "MẶT BẰNG TẦNG 1") };
        int k = 2;
        foreach (var st in p.Stories.OrderBy(s => s.Elevation))
        {
            var kind = Levels.KindOf(p, st);
            string title = kind == LevelKind.San ? $"MẶT BẰNG TẦNG {k++}" : kind == LevelKind.Mai ? "MẶT BẰNG MÁI" : "MẶT BẰNG TUM";
            levels.Add((st.Name, title));
        }
        // tường tầng 1 khai báo trên cao độ chân cột ("Base")/"NEN": gom vào tầng 1
        foreach (var (story, title) in levels)
        {
            var storyKeys = story == "NEN" ? new[] { "NEN", "Base", "" } : new[] { story };
            bool hasAny = p.Walls.Any(w => storyKeys.Contains(w.Story)) || (p.Arch?.Rooms ?? new()).Any(r => storyKeys.Contains(r.Story))
                          || (p.Arch?.Furniture ?? new()).Any(f => storyKeys.Contains(f.Story));
            if (!hasAny) continue;
            var w = new DxfWriter(o.PlanScale);
            Grids(w, p, box);
            if (o.ShowOthers)
            {
                // kết cấu tầng dưới đỡ tường tầng này: cột của tầng phía trên cao độ (cột từ sàn này lên)
                string up = p.Stories.Where(s => s.Elevation > (story == "NEN" ? p.GroundElevation : p.Stories.First(x => x.Name == story).Elevation) + 1e-6)
                    .OrderBy(s => s.Elevation).FirstOrDefault()?.Name;
                if (up != null) StructureReference(w, p, up);
                Furniture(w, p, story, Discipline.DienNuoc, RefLayer, false);
            }
            foreach (var key in storyKeys) WallPlanDrawing.Draw(w, p, key);
            foreach (var r in (p.Arch?.Rooms ?? new List<Room>()).Where(r => storyKeys.Contains(r.Story)))
            {
                var c = r.Center;
                w.Text(Lay.Chu, c.X * 1000, c.Y * 1000 + w.Th, w.Th * 1.2, r.Name.ToUpperInvariant(), 0, 1, 0);
                w.Text(Lay.Chu, c.X * 1000, c.Y * 1000 - w.Th * 0.6, w.Th, $"S = {r.Area:0.0} m²", 0, 1, 0);
            }
            foreach (var key in storyKeys) Furniture(w, p, key, Discipline.KienTruc, Lay.KyHieu);
            w.Title((box.X0 + box.X1) / 2 * 1000, box.Y0 * 1000 - 3500, title, $"tl: 1/{o.PlanScale:0}");
            items.Add(new DrawingItem { Key = "KT:" + story, FileName = $"KT_{Slug(title)}.dxf", Title = title, Scale = o.PlanScale, W = w });
        }
        // mặt cắt công trình 1-1, 2-2 (+ mặt cắt người dùng vẽ) – tự ghi cao độ, kích thước tầng, trục (Drawing/SectionView.cs)
        try { items.AddRange(SectionView.Sheets(p, o.PlanScale)); } catch (Exception) { /* mô hình chưa đủ dữ liệu cắt */ }
        var sch = WallPlanDrawing.ScheduleFigure(p);
        if (sch != null) items.Add(new DrawingItem { Key = "KT:CUA", FileName = "KT_ThongKeCua.dxf", Title = "THỐNG KÊ CỬA", Scale = 1, W = sch.W });
        var rooms = p.Arch?.Rooms ?? new List<Room>();
        if (rooms.Count > 0) items.Add(new DrawingItem { Key = "KT:PHONG", FileName = "KT_ThongKePhong.dxf", Title = "THỐNG KÊ PHÒNG", Scale = 1, W = RoomTable(p, rooms) });
        return items;
    }

    private static DxfWriter RoomTable(Project p, List<Room> rooms)
    {
        var w = new DxfWriter(1);
        double th = 2.5, rh = 7;
        var cw = new[] { 12.0, 30, 50, 28, 50 };
        string[] heads = { "STT", "SỐ PHÒNG", "TÊN PHÒNG", "DIỆN TÍCH (m²)", "LÁT SÀN" };
        double tw = cw.Sum(), y = 0;
        w.Text(Lay.Chu, tw / 2, 4, 1.6 * th, "BẢNG THỐNG KÊ PHÒNG", 0, 1, 0);
        var rows = new List<string[]> { heads };
        int i = 1;
        foreach (var r in rooms.OrderBy(r => r.Story).ThenBy(r => r.Number))
            rows.Add(new[] { (i++).ToString(), r.Number, r.Name, r.Area.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ','), r.FloorFinish });
        rows.Add(new[] { "", "", "TỔNG", rooms.Sum(r => r.Area).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ','), "" });
        w.Line(Lay.KhungTen, 0, y, tw, y);
        foreach (var row in rows)
        {
            double x = 0;
            for (int c = 0; c < cw.Length; c++)
            {
                w.Text(Lay.Chu, x + cw[c] / 2, y - rh / 2 - th / 2, th, row[c] ?? "", 0, 1, 0);
                w.Line(Lay.KhungTen, x, y, x, y - rh);
                x += cw[c];
            }
            w.Line(Lay.KhungTen, tw, y, tw, y - rh);
            y -= rh;
            w.Line(Lay.KhungTen, 0, y, tw, y);
        }
        _ = p;
        return w;
    }

    /// <summary>Tờ tạm của bộ môn Điện nước khi chưa có bản vẽ riêng: mặt bằng thiết bị (khối bao) từng tầng.</summary>
    public static List<DrawingItem> MepPlaceholder(Project p, DisciplineDocOptions o)
    {
        var items = new List<DrawingItem>();
        var box = Extents(p);
        var stories = (p.Arch?.Furniture ?? new List<FurnitureItem>()).Where(f => f.Discipline == Discipline.DienNuoc).Select(f => f.Story).Distinct().ToList();
        foreach (var st in stories)
        {
            var w = new DxfWriter(o.PlanScale);
            Grids(w, p, box);
            if (o.ShowOthers) { foreach (var key in new[] { st }) WallPlanDrawing.Draw(w, p, key); }
            int n = Furniture(w, p, st, Discipline.DienNuoc, Lay.KyHieu);
            string title = $"MẶT BẰNG ĐIỆN NƯỚC – {(st == "NEN" ? "TẦNG 1" : st)}";
            w.Title((box.X0 + box.X1) / 2 * 1000, box.Y0 * 1000 - 3500, title, $"tl: 1/{o.PlanScale:0}");
            if (n > 0) items.Add(new DrawingItem { Key = "DN:" + st, FileName = $"DN_{Slug(title)}.dxf", Title = title, Scale = o.PlanScale, W = w });
        }
        return items;
    }
}

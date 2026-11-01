using System.Globalization;
using System.Text.RegularExpressions;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Import;

/// <summary>Tuỳ chọn nhập mặt bằng kết cấu từ DXF.</summary>
public class DxfImportOptions
{
    /// <summary>Lớp (layer) đường trục. Có thể ghi nhiều lớp, cách nhau dấu phẩy. So khớp không phân biệt hoa thường, chứa chuỗi.</summary>
    public string GridLayer { get; set; } = "TRUC";
    /// <summary>Lớp cột: polyline kín hình chữ nhật</summary>
    public string ColumnLayer { get; set; } = "COT";
    /// <summary>Lớp dầm: đường tim (line/polyline) hoặc 2 nét mép dầm song song</summary>
    public string BeamLayer { get; set; } = "DAM";
    /// <summary>Lớp sàn: polyline kín bao ô sàn</summary>
    public string SlabLayer { get; set; } = "SAN";
    /// <summary>&gt; 0: hệ số đổi đơn vị bản vẽ → m (vd. 0.001 khi vẽ bằng mm). 0 = theo $INSUNITS.</summary>
    public double ScaleOverride { get; set; }
    /// <summary>Tên các tầng áp dụng mặt bằng; rỗng = mọi tầng.</summary>
    public List<string> Stories { get; set; } = new();
    public string DefaultColumnSection { get; set; } = "C22x22";
    public string DefaultBeamSection { get; set; } = "D22x30";
    public string SlabSection { get; set; } = "S100";
    public string Concrete { get; set; } = "B25";
    /// <summary>Xoá phần mô hình cũ của các tầng được nhập</summary>
    public bool ReplaceExisting { get; set; } = true;
    /// <summary>Thay lưới trục của dự án bằng trục đọc từ DXF</summary>
    public bool ImportGrids { get; set; } = true;
    /// <summary>Dời gốc toạ độ về giao điểm trục đầu tiên (hoặc góc trái dưới mô hình)</summary>
    public bool MoveToOrigin { get; set; } = true;
    /// <summary>Đưa tâm cột về giao điểm trục khi giao điểm nằm trong tiết diện cột</summary>
    public bool SnapColumnsToGrid { get; set; } = true;
    /// <summary>Nhận dạng dầm vẽ 2 nét → lấy bề rộng dầm</summary>
    public bool DetectDoubleLineBeams { get; set; } = true;
    /// <summary>Tự tạo ô sàn trong các ô kín bởi dầm khi không có lớp sàn</summary>
    public bool AutoSlabs { get; set; } = true;
    /// <summary>Sai số bắt điểm đầu dầm vào tâm cột / dầm khác, m</summary>
    public double SnapTolerance { get; set; } = 0.30;
    public double FinishLoad { get; set; } = 1.2;
    public double LiveLoad { get; set; } = 1.5;
    public double RoofLiveLoad { get; set; } = 0.75;
    public bool FixedBase { get; set; } = true;
}

/// <summary>Nhập mô hình khung BTCT từ mặt bằng kết cấu DXF.</summary>
public static class DxfImporter
{
    private class Col
    {
        public double X, Y, Bx, By;
        public string Section = "";
        public double Angle;
        public string Group = "";
    }

    private class Bm
    {
        public Seg2 S;
        public double Width;
        public string Section = "";
    }

    private static readonly Regex SizeRx = new(@"(\d{2,4})\s*[xX×\*]\s*(\d{2,4})", RegexOptions.Compiled);

    public static ImportReport Import(Project p, string path, DxfImportOptions o)
    {
        o ??= new DxfImportOptions();
        var rep = new ImportReport { Source = Path.GetFileName(path) };
        var dxf = DxfReader.Read(path, o.ScaleOverride);
        rep.Warnings.AddRange(dxf.Warnings);
        rep.Info.Add($"Đơn vị bản vẽ: $INSUNITS = {dxf.InsUnits} → hệ số {dxf.Scale.ToString(CultureInfo.InvariantCulture)} (đơn vị → m); {dxf.Entities.Count} thực thể");

        var ents = dxf.Entities;
        var gridEnts = ents.Where(e => Match(e.Layer, o.GridLayer)).ToList();
        var colEnts = ents.Where(e => Match(e.Layer, o.ColumnLayer)).ToList();
        var beamEnts = ents.Where(e => Match(e.Layer, o.BeamLayer)).ToList();
        var slabEnts = ents.Where(e => Match(e.Layer, o.SlabLayer)).ToList();
        var texts = ents.Where(e => e.Type == DxfEntityType.Text).ToList();
        var circles = ents.Where(e => e.Type == DxfEntityType.Circle).ToList();

        // ---------------- TRỤC ----------------
        var gridSegs = Segments(gridEnts.Where(e => e.Type is DxfEntityType.Line or DxfEntityType.Polyline)).ToList();
        var gx = new List<(double c, Seg2 s)>();
        var gy = new List<(double c, Seg2 s)>();
        foreach (var s in gridSegs)
        {
            double L = s.Length;
            if (L < 0.5) continue;
            if (Math.Abs(s.Bx - s.Ax) < 0.01 * L) gx.Add(((s.Ax + s.Bx) / 2, s));
            else if (Math.Abs(s.By - s.Ay) < 0.01 * L) gy.Add(((s.Ay + s.By) / 2, s));
            else rep.Warnings.Add($"Bỏ qua đường trục xiên tại ({s.Ax:0.00}; {s.Ay:0.00})");
        }
        var gridX = BuildGrids(gx, GridDir.X, texts, circles);
        var gridY = BuildGrids(gy, GridDir.Y, texts, circles);

        // ---------------- CỘT ----------------
        var cols = new List<Col>();
        foreach (var e in colEnts.Where(e => e.Type == DxfEntityType.Polyline && e.Closed && e.Points.Count >= 3))
        {
            bool rect = ImportGeometry.IsAxisRect(e.Points, out double x0, out double y0, out double x1, out double y1);
            if (!rect) rep.Warnings.Add($"Cột tại ({(x0 + x1) / 2:0.00}; {(y0 + y1) / 2:0.00}) không phải chữ nhật song song trục – lấy hình bao.");
            double bx = ImportGeometry.RoundMm(x1 - x0), by = ImportGeometry.RoundMm(y1 - y0);
            if (bx < 0.05 || by < 0.05) continue;
            cols.Add(new Col { X = (x0 + x1) / 2, Y = (y0 + y1) / 2, Bx = bx, By = by });
        }
        foreach (var e in colEnts.Where(e => e.Type == DxfEntityType.Circle && e.Radius > 0.05))
        {
            // cột tròn → quy đổi tiết diện vuông tương đương diện tích
            double a = ImportGeometry.RoundMm(Math.Sqrt(Math.PI) * e.Radius);
            cols.Add(new Col { X = e.Points[0].X, Y = e.Points[0].Y, Bx = a, By = a });
            rep.Warnings.Add($"Cột tròn R={e.Radius * 1000:0} mm tại ({e.Points[0].X:0.00}; {e.Points[0].Y:0.00}) quy đổi thành vuông {a * 1000:0} mm.");
        }
        if (colEnts.Count > 0 && cols.Count == 0) rep.Warnings.Add($"Lớp cột '{o.ColumnLayer}' không có polyline kín nào.");
        // loại cột trùng
        cols = cols.GroupBy(c => (Math.Round(c.X, 2), Math.Round(c.Y, 2))).Select(g => g.First()).ToList();

        if (o.SnapColumnsToGrid && gridX.Count > 0 && gridY.Count > 0)
            foreach (var c in cols)
            {
                var ix = gridX.Where(g => Math.Abs(g.Coord - c.X) <= c.Bx / 2 + 0.02).OrderBy(g => Math.Abs(g.Coord - c.X)).FirstOrDefault();
                var iy = gridY.Where(g => Math.Abs(g.Coord - c.Y) <= c.By / 2 + 0.02).OrderBy(g => Math.Abs(g.Coord - c.Y)).FirstOrDefault();
                if (ix != null) c.X = ix.Coord;
                if (iy != null) c.Y = iy.Coord;
            }

        // ---------------- DẦM ----------------
        var rawBeams = Segments(beamEnts.Where(e => e.Type is DxfEntityType.Line or DxfEntityType.Polyline)).Where(s => s.Length > 0.05).ToList();
        var beams = new List<Bm>();
        if (o.DetectDoubleLineBeams) beams.AddRange(PairDoubleLines(rawBeams, out rawBeams));
        beams.AddRange(rawBeams.Select(s => new Bm { S = s }));

        // ---------------- DỜI GỐC TOẠ ĐỘ ----------------
        double ox = 0, oy = 0;
        if (o.MoveToOrigin)
        {
            if (gridX.Count > 0) ox = gridX.Min(g => g.Coord);
            else if (cols.Count > 0) ox = cols.Min(c => c.X);
            else if (beams.Count > 0) ox = beams.Min(b => Math.Min(b.S.Ax, b.S.Bx));
            if (gridY.Count > 0) oy = gridY.Min(g => g.Coord);
            else if (cols.Count > 0) oy = cols.Min(c => c.Y);
            else if (beams.Count > 0) oy = beams.Min(b => Math.Min(b.S.Ay, b.S.By));
            ox = Math.Round(ox, 3); oy = Math.Round(oy, 3);
            if (Math.Abs(ox) > 1e-6 || Math.Abs(oy) > 1e-6)
                rep.Info.Add($"Dời gốc toạ độ về ({ox:0.000}; {oy:0.000}) của bản vẽ");
            foreach (var g in gridX) g.Coord = Math.Round(g.Coord - ox, 4);
            foreach (var g in gridY) g.Coord = Math.Round(g.Coord - oy, 4);
            foreach (var c in cols) { c.X -= ox; c.Y -= oy; }
            foreach (var b in beams) b.S = new Seg2(b.S.Ax - ox, b.S.Ay - oy, b.S.Bx - ox, b.S.By - oy);
        }
        Func<double, double, (double, double)> toModel = (qx, qy) => (qx - ox, qy - oy);

        if (o.ImportGrids && gridX.Count + gridY.Count > 0)
        {
            p.Grids.Clear();
            p.Grids.AddRange(gridX);
            p.Grids.AddRange(gridY);
        }
        rep.Grids = gridX.Count + gridY.Count;
        if (rep.Grids == 0) rep.Warnings.Add($"Không tìm thấy đường trục trên lớp '{o.GridLayer}'.");

        // tiết diện cột
        foreach (var c in cols)
        {
            double b = Math.Min(c.Bx, c.By), h = Math.Max(c.Bx, c.By);
            c.Section = ImportGeometry.EnsureRect(p, "C", b, h, o.Concrete);
            c.Angle = c.By > c.Bx + 1e-4 ? 90 : 0;   // trục 2 (cạnh h) mặc định song song X
            var ngx = p.GridsX.FirstOrDefault(g => Math.Abs(g.Coord - c.X) < 0.01);
            var ngy = p.GridsY.FirstOrDefault(g => Math.Abs(g.Coord - c.Y) < 0.01);
            c.Group = ngx != null && ngy != null ? $"C-{ngx.Name}{ngy.Name}" : "";
        }
        int ug = 1;
        foreach (var c in cols.OrderBy(q => q.Y).ThenBy(q => q.X)) if (c.Group == "") c.Group = $"C-P{ug++}";

        // bắt điểm đầu dầm vào tâm cột
        foreach (var b in beams)
        {
            var (ax, ay) = SnapToColumn(b.S.Ax, b.S.Ay, cols, o.SnapTolerance);
            var (bx, by) = SnapToColumn(b.S.Bx, b.S.By, cols, o.SnapTolerance);
            b.S = new Seg2(ax, ay, bx, by);
        }
        // bắt điểm đầu dầm (không tựa cột) lên dầm khác
        for (int i = 0; i < beams.Count; i++)
        {
            var s = beams[i].S;
            double ax = s.Ax, ay = s.Ay, bx = s.Bx, by = s.By;
            if (!OnColumn(ax, ay, cols)) (ax, ay) = SnapToBeam(ax, ay, i, beams, o.SnapTolerance);
            if (!OnColumn(bx, by, cols)) (bx, by) = SnapToBeam(bx, by, i, beams, o.SnapTolerance);
            beams[i].S = new Seg2(ax, ay, bx, by);
        }
        beams = beams.Where(b => b.S.Length > 0.05).ToList();

        // tiết diện dầm
        var defBeam = p.FrameSections.FirstOrDefault(s => s.Name == o.DefaultBeamSection);
        double defH = defBeam?.H > 0 ? defBeam.H : 0.30;
        var beamTexts = texts.Where(t => Match(t.Layer, o.BeamLayer)).Select(t =>
        {
            var (tx, ty) = toModel(t.Points[0].X, t.Points[0].Y);
            return (x: tx, y: ty, text: t.Text);
        }).ToList();
        foreach (var b in beams)
        {
            double w = b.Width, h = defH;
            // ghi chú kích thước "220x400" / "22x40" gần giữa dầm
            double mx = (b.S.Ax + b.S.Bx) / 2, my = (b.S.Ay + b.S.By) / 2;
            foreach (var t in beamTexts.OrderBy(q => (q.x - mx) * (q.x - mx) + (q.y - my) * (q.y - my)))
            {
                var (tt, dist) = ImportGeometry.ProjectPoint(b.S, t.x, t.y);
                if (dist > 0.6 || tt < 0 || tt > 1) break;
                var m = SizeRx.Match(t.text);
                if (!m.Success) continue;
                double v1 = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                double v2 = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                double k = Math.Max(v1, v2) < 150 ? 0.01 : 0.001;   // cm hoặc mm
                w = v1 * k; h = v2 * k;
                break;
            }
            if (w > 0.05) b.Section = ImportGeometry.EnsureRect(p, "D", ImportGeometry.RoundMm(w), ImportGeometry.RoundMm(h), o.Concrete);
            else b.Section = defBeam != null ? defBeam.Name : ImportGeometry.EnsureRect(p, "D", 0.22, defH, o.Concrete);
        }

        // giao điểm giữa các dầm (để chia thanh)
        var crossings = new List<(double x, double y)>();
        for (int i = 0; i < beams.Count; i++)
            for (int j = i + 1; j < beams.Count; j++)
                if (ImportGeometry.Intersect(beams[i].S, beams[j].S, out double x, out double y))
                    crossings.Add((x, y));

        // ---------------- SÀN (mặt bằng) ----------------
        var beamSegs = beams.Select(b => b.S).ToList();
        var slabRects = new List<(double x0, double y0, double x1, double y1)>();
        var slabPolys = new List<List<(double X, double Y)>>();
        foreach (var e in slabEnts.Where(e => e.Type == DxfEntityType.Polyline && e.Closed && e.Points.Count >= 3))
        {
            var pts = e.Points.Select(q => { var (x, y) = toModel(q.X, q.Y); return (X: x, Y: y); }).ToList();
            if (ImportGeometry.IsAxisRect(pts, out double x0, out double y0, out double x1, out double y1))
                slabRects.AddRange(ImportGeometry.RectCells(beamSegs, x0, y0, x1, y1, false, rep.Warnings, "Sàn"));
            else
            {
                var q2 = ImportGeometry.RemoveCollinear(pts);
                if (ImportGeometry.SignedArea(q2) < 0) q2.Reverse();
                slabPolys.Add(q2);
                rep.Warnings.Add($"Ô sàn đa giác {q2.Count} đỉnh tại ({x0:0.00}; {y0:0.00}) – tải dồn đều theo chu vi.");
            }
        }
        if (slabRects.Count == 0 && slabPolys.Count == 0 && o.AutoSlabs && beamSegs.Count > 0)
        {
            double x0 = beamSegs.Min(s => Math.Min(s.Ax, s.Bx)), x1 = beamSegs.Max(s => Math.Max(s.Ax, s.Bx));
            double y0 = beamSegs.Min(s => Math.Min(s.Ay, s.By)), y1 = beamSegs.Max(s => Math.Max(s.Ay, s.By));
            slabRects.AddRange(ImportGeometry.RectCells(beamSegs, x0, y0, x1, y1, true, rep.Warnings, "Sàn tự động"));
            if (slabRects.Count > 0) rep.Info.Add($"Tự tạo {slabRects.Count} ô sàn từ các ô kín bởi dầm (mỗi tầng).");
        }
        slabRects = slabRects.Where(r => r.x1 - r.x0 > 0.3 && r.y1 - r.y0 > 0.3).ToList();

        // ---------------- TẦNG ----------------
        if (p.Stories.Count == 0)
        {
            ModelBuilder.SetStories(p, new[] { 3.6 }, p.BaseElevation);
            rep.Warnings.Add("Dự án chưa có tầng – tạo 1 tầng cao 3.6 m.");
        }
        var allStories = p.Stories.OrderBy(s => s.Elevation).ToList();
        var targets = o.Stories == null || o.Stories.Count == 0
            ? allStories
            : allStories.Where(s => o.Stories.Contains(s.Name)).ToList();
        if (targets.Count == 0) throw new InvalidOperationException("Không có tầng nào được chọn để áp dụng mặt bằng DXF.");
        rep.Stories = targets.Count;

        ImportGeometry.EnsurePatterns(p);
        if (p.SlabSections.All(s => s.Name != o.SlabSection))
            p.SlabSections.Add(new SlabSection { Name = o.SlabSection, Thickness = 0.10, Material = o.Concrete });
        if (o.ReplaceExisting)
        {
            if (targets.Count == allStories.Count) ImportGeometry.ClearModel(p);
            else foreach (var st in targets) RemoveStory(p, st.Name);
        }

        var topStory = allStories[^1];
        foreach (var st in targets)
        {
            int idx = allStories.IndexOf(st);
            double zb = idx == 0 ? p.BaseElevation : allStories[idx - 1].Elevation;
            double zt = st.Elevation;
            bool roof = st == topStory;
            string baseName = idx == 0 ? "Base" : allStories[idx - 1].Name;

            int ci = 1;
            foreach (var c in cols.OrderBy(q => q.Y).ThenBy(q => q.X))
            {
                var a = p.GetOrAddNode(c.X, c.Y, zb, baseName);
                var b = p.GetOrAddNode(c.X, c.Y, zt, st.Name);
                if (Math.Abs(zb - p.BaseElevation) < 1e-3) a.Restraint = o.FixedBase ? Node.Fixed : Node.Pinned;
                if (ImportGeometry.AddFrame(p, a, b, FrameKind.Column, c.Section, st.Name, $"C{ci++}", c.Group, c.Angle) != null) rep.Columns++;
            }

            int bi = 1, pk = 1;
            foreach (var bm in beams)
            {
                var a = p.GetOrAddNode(bm.S.Ax, bm.S.Ay, zt, st.Name);
                var b = p.GetOrAddNode(bm.S.Bx, bm.S.By, zt, st.Name);
                string grid = GridOf(p, bm.S);
                string group = grid != "" ? $"D{st.Name}-{grid}" : $"D{st.Name}-P{pk++}";
                if (ImportGeometry.AddFrame(p, a, b, FrameKind.Beam, bm.Section, st.Name, $"D{bi++}", group) != null) rep.Beams++;
            }
            foreach (var (x, y) in crossings) p.GetOrAddNode(x, y, zt, st.Name);

            int sl = 1;
            foreach (var r in slabRects)
            {
                var n1 = p.GetOrAddNode(r.x0, r.y0, zt, st.Name);
                var n2 = p.GetOrAddNode(r.x1, r.y0, zt, st.Name);
                var n3 = p.GetOrAddNode(r.x1, r.y1, zt, st.Name);
                var n4 = p.GetOrAddNode(r.x0, r.y1, zt, st.Name);
                var slab = new Slab
                {
                    Id = p.NextSlabId(), Label = $"S{st.Name}-{sl++}", Story = st.Name, Section = o.SlabSection,
                    Nodes = new List<int> { n1.Id, n2.Id, n3.Id, n4.Id }
                };
                p.Slabs.Add(slab); rep.Slabs++;
                ImportGeometry.AddSlabLoads(p, slab, roof, o.FinishLoad, o.LiveLoad, o.RoofLiveLoad);
            }
            foreach (var poly in slabPolys)
            {
                var slab = new Slab
                {
                    Id = p.NextSlabId(), Label = $"S{st.Name}-{sl++}", Story = st.Name, Section = o.SlabSection,
                    Nodes = poly.Select(q => p.GetOrAddNode(q.X, q.Y, zt, st.Name).Id).ToList()
                };
                p.Slabs.Add(slab); rep.Slabs++;
                ImportGeometry.AddSlabLoads(p, slab, roof, o.FinishLoad, o.LiveLoad, o.RoofLiveLoad);
            }

            // chân cột không liên kết
            if (idx > 0 && !targets.Contains(allStories[idx - 1]))
            {
                var hanging = p.Frames.Where(f => f.Story == st.Name && f.Kind == FrameKind.Column)
                    .Select(f => f.NodeI).Where(n => p.Frames.Count(f => f.NodeI == n || f.NodeJ == n) < 2).Count();
                if (hanging > 0) rep.Warnings.Add($"Tầng {st.Name}: {hanging} chân cột không nối với kết cấu tầng dưới (tầng dưới chưa nhập).");
            }
        }

        // loại nút treo (giao điểm không dùng)
        p.InvalidateIndex();
        RemoveOrphanNodes(p);
        rep.SplitCount = ModelBuilder.SplitFramesAtNodes(p);
        if (p.Combinations.Count == 0) ComboGenerator.Generate(p);
        p.InvalidateIndex();

        if (cols.Count == 0) rep.Warnings.Add($"Không có cột nào trên lớp '{o.ColumnLayer}'.");
        if (beams.Count == 0) rep.Warnings.Add($"Không có dầm nào trên lớp '{o.BeamLayer}'.");
        foreach (var b in beams)
        {
            bool aOk = OnColumn(b.S.Ax, b.S.Ay, cols) || beams.Any(o2 => o2 != b && ImportGeometry.ProjectPoint(o2.S, b.S.Ax, b.S.Ay).dist < 0.01);
            bool bOk = OnColumn(b.S.Bx, b.S.By, cols) || beams.Any(o2 => o2 != b && ImportGeometry.ProjectPoint(o2.S, b.S.Bx, b.S.By).dist < 0.01);
            if (!aOk || !bOk) rep.Warnings.Add($"Dầm ({b.S.Ax:0.00};{b.S.Ay:0.00})–({b.S.Bx:0.00};{b.S.By:0.00}) có đầu tự do (console) – kiểm tra lại.");
        }
        rep.Nodes = p.Nodes.Count; rep.Frames = p.Frames.Count;
        return rep;
    }

    // ------------------------------------------------------------------
    private static bool Match(string layer, string keys)
    {
        if (string.IsNullOrWhiteSpace(keys)) return false;
        foreach (var k in keys.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (layer.Equals(k, StringComparison.OrdinalIgnoreCase) || layer.ToUpperInvariant().Contains(k.ToUpperInvariant()))
                return true;
        return false;
    }

    private static IEnumerable<Seg2> Segments(IEnumerable<DxfEntity> ents)
    {
        foreach (var e in ents)
        {
            var pts = e.Points;
            for (int i = 0; i + 1 < pts.Count; i++) yield return new Seg2(pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y);
            if (e.Type == DxfEntityType.Polyline && e.Closed && pts.Count > 2)
                yield return new Seg2(pts[^1].X, pts[^1].Y, pts[0].X, pts[0].Y);
        }
    }

    private static List<GridLine> BuildGrids(List<(double c, Seg2 s)> lines, GridDir dir, List<DxfEntity> texts, List<DxfEntity> circles)
    {
        var res = new List<GridLine>();
        foreach (var grp in GroupByCoord(lines, 0.02))
        {
            double c = grp.Average(g => g.c);
            string name = null;
            foreach (var (_, s) in grp)
            {
                foreach (var end in new[] { (s.Ax, s.Ay), (s.Bx, s.By) })
                {
                    name = BubbleName(end.Item1, end.Item2, s, texts, circles);
                    if (name != null) break;
                }
                if (name != null) break;
            }
            res.Add(new GridLine { Name = name ?? "", Dir = dir, Coord = c });
        }
        res = res.OrderBy(g => g.Coord).ToList();
        bool bad = res.Any(g => g.Name == "") || res.Select(g => g.Name).Distinct().Count() != res.Count;
        if (bad)
            for (int i = 0; i < res.Count; i++)
                if (res[i].Name == "" || res.Count(g => g.Name == res[i].Name) > 1) res[i].Name = ImportGeometry.AutoName(dir, i);
        // tên tự động có thể trùng với tên đọc được – đánh lại toàn bộ
        if (res.Select(g => g.Name).Distinct().Count() != res.Count)
            for (int i = 0; i < res.Count; i++) res[i].Name = ImportGeometry.AutoName(dir, i);
        return res;
    }

    private static List<List<(double c, Seg2 s)>> GroupByCoord(List<(double c, Seg2 s)> lines, double tol)
    {
        var groups = new List<List<(double c, Seg2 s)>>();
        foreach (var l in lines.OrderBy(l => l.c))
        {
            if (groups.Count > 0 && Math.Abs(groups[^1][^1].c - l.c) <= tol) groups[^1].Add(l);
            else groups.Add(new List<(double c, Seg2 s)> { l });
        }
        return groups;
    }

    /// <summary>Tên trục: chữ nằm trong vòng tròn đầu trục; nếu không có vòng tròn thì chữ gần đầu trục.</summary>
    private static string BubbleName(double ex, double ey, Seg2 s, List<DxfEntity> texts, List<DxfEntity> circles)
    {
        double L = s.Length;
        double ux = (s.Bx - s.Ax) / L, uy = (s.By - s.Ay) / L;
        foreach (var c in circles)
        {
            double cx = c.Points[0].X, cy = c.Points[0].Y, r = c.Radius;
            if (r < 1e-4 || r > 2.0) continue;
            // tâm vòng tròn nằm trên phương trục và gần đầu mút
            double dx = cx - ex, dy = cy - ey;
            double along = dx * ux + dy * uy, perp = Math.Abs(-dx * uy + dy * ux);
            if (perp > r * 0.5 + 0.01 || Math.Abs(along) > 2.5 * r + 0.05) continue;
            var txt = texts.Where(q => Dist(q.Points[0].X, q.Points[0].Y, cx, cy) < r * 1.6)
                           .OrderBy(q => Dist(q.Points[0].X, q.Points[0].Y, cx, cy)).FirstOrDefault();
            if (txt != null && txt.Text.Trim().Length > 0 && txt.Text.Trim().Length <= 4) return txt.Text.Trim();
        }
        var near = texts.Where(t => t.Text.Trim().Length is > 0 and <= 3 && Dist(t.Points[0].X, t.Points[0].Y, ex, ey) < 0.8)
                        .OrderBy(t => Dist(t.Points[0].X, t.Points[0].Y, ex, ey)).FirstOrDefault();
        return near?.Text.Trim();
    }

    private static double Dist(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

    /// <summary>Ghép các cặp nét song song (mép dầm) thành đường tim có bề rộng.</summary>
    private static List<Bm> PairDoubleLines(List<Seg2> segs, out List<Seg2> remaining)
    {
        var used = new bool[segs.Count];
        var res = new List<Bm>();
        for (int i = 0; i < segs.Count; i++)
        {
            if (used[i]) continue;
            var a = segs[i];
            double La = a.Length;
            double ux = (a.Bx - a.Ax) / La, uy = (a.By - a.Ay) / La;
            int best = -1; double bestW = 0;
            for (int j = 0; j < segs.Count; j++)
            {
                if (j == i || used[j]) continue;
                var b = segs[j];
                double Lb = b.Length;
                double vx = (b.Bx - b.Ax) / Lb, vy = (b.By - b.Ay) / Lb;
                if (Math.Abs(ux * vy - uy * vx) > 0.01) continue; // không song song
                double w = Math.Abs(-(b.Ax - a.Ax) * uy + (b.Ay - a.Ay) * ux);
                if (w < 0.08 || w > 0.60) continue;
                // chồng lấp theo phương dọc
                double t1 = (b.Ax - a.Ax) * ux + (b.Ay - a.Ay) * uy, t2 = (b.Bx - a.Ax) * ux + (b.By - a.Ay) * uy;
                double lo = Math.Max(0, Math.Min(t1, t2)), hi = Math.Min(La, Math.Max(t1, t2));
                if (hi - lo < 0.5 * Math.Min(La, Lb)) continue;
                if (best < 0 || w < bestW) { best = j; bestW = w; }
            }
            if (best < 0) continue;
            var s2 = segs[best];
            used[i] = used[best] = true;
            double tb1 = (s2.Ax - a.Ax) * ux + (s2.Ay - a.Ay) * uy, tb2 = (s2.Bx - a.Ax) * ux + (s2.By - a.Ay) * uy;
            double t0 = Math.Min(0, Math.Min(tb1, tb2)), tE = Math.Max(La, Math.Max(tb1, tb2));
            // pháp tuyến hướng về nét thứ hai
            double nx = -uy, ny = ux;
            double side = (s2.Ax - a.Ax) * nx + (s2.Ay - a.Ay) * ny;
            double off = side / 2;
            double cx = a.Ax + nx * off, cy = a.Ay + ny * off;
            res.Add(new Bm
            {
                S = new Seg2(cx + ux * t0, cy + uy * t0, cx + ux * tE, cy + uy * tE),
                Width = ImportGeometry.RoundMm(bestW)
            });
        }
        remaining = segs.Where((s, k) => !used[k]).ToList();
        return res;
    }

    private static Col ColumnAt(double x, double y, List<Col> cols, double tol)
    {
        Col best = null; double bd = double.MaxValue;
        foreach (var c in cols)
        {
            bool inside = Math.Abs(x - c.X) <= c.Bx / 2 + tol && Math.Abs(y - c.Y) <= c.By / 2 + tol;
            if (!inside) continue;
            double d = Dist(x, y, c.X, c.Y);
            if (d < bd) { bd = d; best = c; }
        }
        return best;
    }

    private static (double, double) SnapToColumn(double x, double y, List<Col> cols, double tol)
    {
        var c = ColumnAt(x, y, cols, tol);
        return c != null ? (c.X, c.Y) : (x, y);
    }

    private static bool OnColumn(double x, double y, List<Col> cols) =>
        cols.Any(c => Math.Abs(c.X - x) < 1e-4 && Math.Abs(c.Y - y) < 1e-4);

    private static (double, double) SnapToBeam(double x, double y, int self, List<Bm> beams, double tol)
    {
        double best = tol; (double, double) r = (x, y);
        for (int j = 0; j < beams.Count; j++)
        {
            if (j == self) continue;
            var s = beams[j].S;
            // đầu mút dầm khác trùng gần → nhập điểm
            foreach (var (px, py) in new[] { (s.Ax, s.Ay), (s.Bx, s.By) })
            {
                double d = Dist(x, y, px, py);
                if (d < best && d < tol / 2) { best = d; r = (px, py); }
            }
            var (t, dist) = ImportGeometry.ProjectPoint(s, x, y);
            if (t > 0 && t < 1 && dist < best && dist > 1e-6)
            {
                best = dist;
                r = (s.Ax + t * (s.Bx - s.Ax), s.Ay + t * (s.By - s.Ay));
            }
        }
        return r;
    }

    /// <summary>Tên trục mà dầm nằm trên (vd "A", "3") hoặc "".</summary>
    private static string GridOf(Project p, Seg2 s)
    {
        if (s.IsAlongX())
        {
            var g = p.GridsY.FirstOrDefault(q => Math.Abs(q.Coord - (s.Ay + s.By) / 2) < 0.02);
            return g?.Name ?? "";
        }
        if (s.IsAlongY())
        {
            var g = p.GridsX.FirstOrDefault(q => Math.Abs(q.Coord - (s.Ax + s.Bx) / 2) < 0.02);
            return g?.Name ?? "";
        }
        return "";
    }

    /// <summary>Xoá cấu kiện của một tầng (để nhập lại).</summary>
    public static void RemoveStory(Project p, string story)
    {
        var fids = new HashSet<int>(p.Frames.Where(f => f.Story == story).Select(f => f.Id));
        var sids = new HashSet<int>(p.Slabs.Where(s => s.Story == story).Select(s => s.Id));
        p.Frames.RemoveAll(f => fids.Contains(f.Id));
        p.Slabs.RemoveAll(s => sids.Contains(s.Id));
        p.FrameLoads.RemoveAll(l => fids.Contains(l.Element));
        p.AreaLoads.RemoveAll(l => sids.Contains(l.Slab));
        p.InvalidateIndex();
        RemoveOrphanNodes(p);
    }

    /// <summary>Xoá nút không thuộc thanh hay ô sàn nào.</summary>
    public static void RemoveOrphanNodes(Project p)
    {
        var used = new HashSet<int>(p.Frames.SelectMany(f => new[] { f.NodeI, f.NodeJ }).Concat(p.Slabs.SelectMany(s => s.Nodes)));
        // nút giao giữa 2 dầm nằm giữa thanh vẫn cần giữ để chia thanh
        var keep = new HashSet<int>();
        foreach (var n in p.Nodes.Where(q => !used.Contains(q.Id)))
        {
            int hits = 0;
            foreach (var f in p.Frames)
            {
                var a = p.Node(f.NodeI).P; var b = p.Node(f.NodeJ).P;
                var u = b - a; double L = u.Length; if (L < 1e-9) continue;
                var d = n.P - a; double t = d.Dot(u) / L;
                if (t > 0.01 && t < L - 0.01 && (d - u * (t / L)).Length < 0.005) hits++;
                if (hits >= 1) break;
            }
            if (hits > 0) keep.Add(n.Id);
        }
        p.Nodes.RemoveAll(n => !used.Contains(n.Id) && !keep.Contains(n.Id));
        p.NodeLoads.RemoveAll(l => p.Nodes.All(n => n.Id != l.Node));
        p.Footings.RemoveAll(f => p.Nodes.All(n => n.Id != f.NodeId));
        p.InvalidateIndex();
    }
}

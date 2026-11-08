using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// [MóngHìnhDạng] BỐ TRÍ MÓNG TRÊN MẶT BẰNG MÓNG
//   • Bố trí móng…: xoay theo mép cột / góc nhập / đường chuẩn; dóng mép móng thẳng mép cột (phương L, B) hoặc lệch tâm nhập tay.
//   • Nhân bản móng…: chép kích thước + bố trí của móng chọn sang chân cột bấm chọn / cùng tiết diện / mọi chân cột / danh sách.
//   • Đồng nhất móng…: gom móng gần bằng nhau thành một loại (lấy kích thước lớn nhất).
//   • Vẽ đường chuẩn: bấm lần lượt các điểm (bắt dính góc cột, giao trục, góc đất) – ranh đất xiên, móng không vượt qua.
//   • Vẽ móng đa giác: bấm các đỉnh quanh chân cột (Enter / bấm lại đỉnh đầu để kết thúc) – móng hình dạng bất kỳ cho đất méo.
// Lõi: Model/FootingPlacement.cs, Design/Foundation/ShapedFooting.cs, FootingLayout.cs, Design/Concrete/FootingDesigner.Shaped.cs.
// =====================================================================
public partial class MainWindow
{
    private enum FndTool { None, LotLine, Polygon, CopyPick }
    private FndTool _fndTool;
    private readonly List<Point> _fndToolPts = new();   // toạ độ thật (m)
    private Point? _fndHoverW;
    private string _fndHoverKind = "";
    private Canvas _fndToolLayer;
    private List<(double X, double Y, string Kind)> _fndSnaps;
    private int _fndCopySrc;
    private readonly List<int> _fndCopyTargets = new();
    private bool _fndCopySize = true, _fndCopyPl = true, _fndCopyRun = true;
    private bool _shapeUiDone;

    private static readonly Brush LotLineBrush = Frozen(Color.FromRgb(200, 0, 160));
    private static readonly Brush ToolBrush = Frozen(Color.FromRgb(0, 110, 220));
    private static readonly Brush CopyPickBrush = Frozen(Color.FromArgb(90, 0, 110, 220));

    private void EnsureShapeUi()
    {
        if (_shapeUiDone || cbFndType?.Parent is not Panel bar) return;
        _shapeUiDone = true;
        int at = bar.Children.IndexOf(cbFndNodeType);
        if (at > 0 && bar.Children[at - 1] is TextBlock) at--;
        if (at < 0) at = bar.Children.Count;
        var items = new (string Text, string Tip, RoutedEventHandler H)[]
        {
            ("Bố trí móng…", "Móng chọn: xoay theo mép cột / góc nhập / đường chuẩn; mép móng thẳng mép cột hoặc lệch tâm nhập tay; cắt theo ranh đất", (s, e) => FndPlacementUi()),
            ("Nhân bản móng…", "Chép kích thước + bố trí của móng chọn sang nhiều chân cột (bấm chọn trên mặt bằng, cùng tiết diện cột, danh sách nút)", (s, e) => FndCopyUi()),
            ("Gán đài cọc giống nhau", "Chép sơ đồ cọc + kích thước đài của đài bấm sau cùng cho mọi đài / chân cột đang chọn (Ctrl + bấm để chọn nhiều, Ctrl + A chọn mọi đài)", (s, e) => FndCapCopyUi()),
            ("Đồng nhất móng…", "Gom các móng có kích thước gần nhau thành một loại móng (lấy kích thước lớn nhất) – giảm số loại móng", (s, e) => FndUnifyUi()),
            ("Vẽ đường chuẩn", "Bấm lần lượt các điểm (bắt dính góc cột, giao trục, góc đất) – đường ranh đất xiên; Esc kết thúc", (s, e) => FndToolStart(FndTool.LotLine)),
            ("Đường chuẩn…", "Danh sách đường chuẩn: toạ độ, khe hở móng, bật / tắt, xoá", (s, e) => FndLotLinesUi()),
            ("Vẽ móng đa giác", "Móng hình dạng bất kỳ (đất méo): bấm các đỉnh quanh chân cột, Enter hoặc bấm lại đỉnh đầu để kết thúc", (s, e) => FndToolStart(FndTool.Polygon)),
        };
        foreach (var (text, tip, h) in Enumerable.Reverse(items))
        {
            var b = new Button { Content = text, ToolTip = tip };
            if (TryFindResource("SmallButton") is Style st) b.Style = st;
            b.Click += h;
            bar.Children.Insert(at, b);
        }
    }

    // ------------------------------------------------------------------
    // Vẽ móng hình dạng + đường chuẩn trên mặt bằng móng
    // ------------------------------------------------------------------
    private bool DrawShapedFooting(FootingDesign fd, bool sel, bool labels)
    {
        if (!fd.Shaped) return false;
        bool ok = fd.Ok;
        var pts = fd.Outline.Select(v => FW2S(v[0], v[1])).ToList();
        var pg = new Shapes.Polygon
        {
            Points = new PointCollection(pts), Fill = ok ? FootOkFill : FootBadFill, Stroke = ok ? FootOkStroke : FootBadStroke,
            StrokeThickness = sel ? 3 : 1.4, Effect = sel ? GlowEffect : null, IsHitTestVisible = false
        };
        fndCanvas.Children.Add(pg);
        double x0 = pts.Min(q => q.X), x1 = pts.Max(q => q.X), y0 = pts.Min(q => q.Y), y1 = pts.Max(q => q.Y);
        _fndHits.Add((new Rect(new Point(x0, y0), new Point(x1, y1)), fd.NodeId, null, fd));
        // dầm móng cân bằng (móng lệch tâm)
        if (fd.StrapNode != 0 && _p.Nodes.FirstOrDefault(x => x.Id == fd.StrapNode) is Node pn)
        {
            var c0 = FW2S(fd.X, fd.Y); var c1 = FW2S(pn.X, pn.Y);
            var sl = new Shapes.Line { X1 = c0.X, Y1 = c0.Y, X2 = c1.X, Y2 = c1.Y, Stroke = Brushes.SaddleBrown, StrokeThickness = Math.Max(2, 0.22 * _fScale), Opacity = 0.35, IsHitTestVisible = false };
            fndCanvas.Children.Add(sl);
            if (labels || sel)
                CText(fndCanvas, $"DCB M={fd.StrapMoment.ToString("0", Inv)} kN.m", new Point((c0.X + c1.X) / 2, (c0.Y + c1.Y) / 2 - 8), Brushes.SaddleBrown, 9, true, true, true);
        }
        // phương L của móng (mũi tên mảnh từ trọng tâm)
        var gc = FW2S(fd.Gx, fd.Gy);
        double a = fd.Angle * Math.PI / 180, len = Math.Min(40, 0.3 * Math.Max(x1 - x0, 20));
        if (Math.Abs(fd.Angle) > 0.05)
            CLine(fndCanvas, gc, new Point(gc.X + len * Math.Cos(a), gc.Y - len * Math.Sin(a)), ok ? FootOkStroke : FootBadStroke, 0.7, true);
        if (labels || sel)
        {
            string name = string.IsNullOrEmpty(fd.Name) ? "M" : fd.Name;
            var tl = CText(fndCanvas, $"{name}  A={fd.Area.ToString("0.00", Inv)} m²  H={fd.H.ToString("0.00", Inv)}" + (Math.Abs(fd.Angle) > 0.05 ? $"  α={fd.Angle.ToString("0.#", Inv)}°" : ""),
                new Point((x0 + x1) / 2, y0 - 9), ok ? FootOkStroke : FootBadStroke, sel ? 11 : 9.5, true, true, true);
            if (sel) tl.FontWeight = FontWeights.Bold;
            CText(fndCanvas, $"N = {fd.N.ToString("0.0", Inv)} kN", new Point((x0 + x1) / 2, y1 + 9), Brushes.DimGray, 9, true, true, true);
        }
        return true;
    }

    private void DrawLotLines()
    {
        if (_p?.Foundation?.LotLines == null) return;
        foreach (var l in _p.Foundation.LotLines)
        {
            var a = FW2S(l.X1, l.Y1); var b = FW2S(l.X2, l.Y2);
            var ln = new Shapes.Line
            {
                X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = LotLineBrush, StrokeThickness = l.Enabled ? 1.8 : 0.8,
                StrokeDashArray = new DoubleCollection { 8, 3, 2, 3 }, Opacity = l.Enabled ? 1 : 0.45, IsHitTestVisible = false
            };
            fndCanvas.Children.Add(ln);
            string name = string.IsNullOrWhiteSpace(l.Name) ? "Đường chuẩn" : l.Name;
            var t = CText(fndCanvas, name + (l.Clearance > 0 ? $" (hở {l.Clearance * 1000:0})" : ""), new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2 - 8), LotLineBrush, 9, true, true, true);
            t.RenderTransformOrigin = new Point(0.5, 0.5);
            double ang = -l.AngleDeg;
            t.RenderTransform = new RotateTransform(ang);
        }
    }

    // ------------------------------------------------------------------
    // 3D: lăng trụ đa giác + cổ cột xoay theo cột
    // ------------------------------------------------------------------
    private void AddShapedFooting3D(MeshGeometry3D m, FootingDesign fd, FootGeom g)
    {
        var poly = fd.Outline.Select(v => new[] { v[0], v[1] }).ToList();
        int n = poly.Count;
        double z0 = g.ZBot, z1 = g.ZBot + g.H;
        int b = m.Positions.Count;
        foreach (var v in poly) m.Positions.Add(new Point3D(v[0], v[1], z0));
        foreach (var v in poly) m.Positions.Add(new Point3D(v[0], v[1], z1));
        void Tri(int x, int y, int z) { m.TriangleIndices.Add(b + x); m.TriangleIndices.Add(b + y); m.TriangleIndices.Add(b + z); m.TriangleIndices.Add(b + x); m.TriangleIndices.Add(b + z); m.TriangleIndices.Add(b + y); }
        foreach (var t in KetCauVXT.Export.MeshExport.Triangulate(poly)) { Tri(t[0], t[1], t[2]); Tri(n + t[0], n + t[1], n + t[2]); }
        for (int i = 0; i < n; i++) { int j = (i + 1) % n; Tri(i, j, n + j); Tri(i, n + j, n + i); }
        if (g.ZNode - z1 > 1e-3)
        {
            var node = _p.Nodes.FirstOrDefault(x => x.Id == fd.NodeId);
            var col = _p.Frames.FirstOrDefault(x => x.Id == fd.ColumnFrame);
            if (node == null) return;
            var cc = ShapedFooting.Column(_p, col, node).Corners();
            AddHexa(m, cc.Select(q => new Point3D(q.X, q.Y, z1)).ToArray(), cc.Select(q => new Point3D(q.X, q.Y, g.ZNode)).ToArray());
        }
    }

    // ------------------------------------------------------------------
    // Công cụ vẽ trên mặt bằng móng
    // ------------------------------------------------------------------
    private void FndToolStart(FndTool t)
    {
        if (_p == null) return;
        _fndTool = t;
        _fndToolPts.Clear();
        _fndHoverW = null;
        _fndSnaps = ShapedFooting.SnapPoints(_p, _design);
        fndHost.Focus();
        SetStatus(t switch
        {
            FndTool.LotLine => "Vẽ đường chuẩn: bấm điểm đầu (bắt dính góc cột / giao trục / góc đất), bấm tiếp các điểm – mỗi đoạn là một đường chuẩn. Esc hoặc Enter kết thúc, Backspace bỏ điểm cuối.",
            FndTool.Polygon => "Vẽ móng đa giác: bấm các đỉnh đáy móng quanh chân cột (bắt dính góc cột, góc đất, đường chuẩn). Enter hoặc bấm lại đỉnh đầu để kết thúc, Esc huỷ, Backspace bỏ đỉnh cuối.",
            FndTool.CopyPick => $"Nhân bản móng tại nút {_fndCopySrc}: bấm các chân cột cần chép (bấm lại để bỏ), Enter để áp dụng, Esc huỷ.",
            _ => ""
        });
        RedrawFoundationPlan();
    }

    private void FndToolEnd(string status = null)
    {
        _fndTool = FndTool.None;
        _fndToolPts.Clear();
        _fndCopyTargets.Clear();
        _fndHoverW = null;
        if (status != null) SetStatus(status);
        RedrawFoundationPlan();
    }

    /// <summary>Điểm bắt dính gần con trỏ (≤ 12 px), ngược lại toạ độ con trỏ.</summary>
    private Point SnapW(Point s, out string kind)
    {
        kind = "";
        double best = 12;
        Point res = FS2W(s);
        if (_fndSnaps != null)
            foreach (var (x, y, k) in _fndSnaps)
            {
                var q = FW2S(x, y);
                double d = Math.Sqrt((q.X - s.X) * (q.X - s.X) + (q.Y - s.Y) * (q.Y - s.Y));
                if (d < best) { best = d; res = new Point(x, y); kind = k; }
            }
        // bắt dính đỉnh đầu đa giác
        if (_fndTool == FndTool.Polygon && _fndToolPts.Count >= 3)
        {
            var q = FW2S(_fndToolPts[0].X, _fndToolPts[0].Y);
            if (Math.Abs(q.X - s.X) + Math.Abs(q.Y - s.Y) < 14) { res = _fndToolPts[0]; kind = "đỉnh đầu – kết thúc"; }
        }
        return res;
    }

    private void FndToolHover(Point s)
    {
        if (_fndTool == FndTool.None) return;
        _fndHoverW = SnapW(s, out _fndHoverKind);
        RefreshToolLayer();
    }

    private bool FndToolClick(Point s)
    {
        if (_fndTool == FndTool.None) return false;
        if (_fndTool == FndTool.CopyPick)
        {
            var w = FS2W(s);
            var hit = BaseColumns(NodeMap()).Select(x => x.n).Where(n => n.Id != _fndCopySrc)
                .OrderBy(n => (n.X - w.X) * (n.X - w.X) + (n.Y - w.Y) * (n.Y - w.Y)).FirstOrDefault();
            if (hit != null && Math.Sqrt((hit.X - w.X) * (hit.X - w.X) + (hit.Y - w.Y) * (hit.Y - w.Y)) * _fScale < Math.Max(18, 0.6 * _fScale))
            {
                if (!_fndCopyTargets.Remove(hit.Id)) _fndCopyTargets.Add(hit.Id);
                SetStatus($"Nhân bản móng nút {_fndCopySrc} → {_fndCopyTargets.Count} chân cột: {string.Join(", ", _fndCopyTargets.Take(12))}{(_fndCopyTargets.Count > 12 ? "…" : "")}. Enter áp dụng, Esc huỷ.");
                RefreshToolLayer();
            }
            return true;
        }
        var pw = SnapW(s, out string kind);
        if (_fndTool == FndTool.Polygon && _fndToolPts.Count >= 3 && kind.StartsWith("đỉnh đầu")) { FinishPolygon(); return true; }
        if (_fndToolPts.Count > 0 && (_fndToolPts[^1] - pw).Length < 1e-4) return true;
        _fndToolPts.Add(pw);
        if (_fndTool == FndTool.LotLine && _fndToolPts.Count >= 2)
        {
            var a = _fndToolPts[^2]; var b = _fndToolPts[^1];
            string snap = Snapshot();
            int k = _p.Foundation.LotLines.Count + 1;
            _p.Foundation.LotLines.Add(new LotLine
            {
                Name = "Ranh đất " + k, X1 = Math.Round(a.X, 4), Y1 = Math.Round(a.Y, 4), X2 = Math.Round(b.X, 4), Y2 = Math.Round(b.Y, 4),
                Clearance = _p.Foundation.BoundaryClearance
            });
            PushUndo(snap);
            MarkDirty(false);
            _fndSnaps = ShapedFooting.SnapPoints(_p, _design);
            SetStatus($"Đã thêm đường chuẩn \"Ranh đất {k}\" ({a.X:0.###}; {a.Y:0.###}) → ({b.X:0.###}; {b.Y:0.###}), dài {(b - a).Length:0.###} m. Bấm tiếp điểm sau, Esc / Enter kết thúc rồi \"Thiết kế móng\".");
            RedrawFoundationPlan();
            return true;
        }
        RefreshToolLayer();
        return true;
    }

    private bool FndToolKey(Key k)
    {
        if (_fndTool == FndTool.None) return false;
        switch (k)
        {
            case Key.Escape:
                FndToolEnd(_fndTool == FndTool.LotLine ? "Kết thúc vẽ đường chuẩn – bấm \"Thiết kế móng\" để thiết kế lại móng theo ranh đất." : "Đã huỷ.");
                return true;
            case Key.Back:
                if (_fndToolPts.Count > 0) _fndToolPts.RemoveAt(_fndToolPts.Count - 1);
                RefreshToolLayer();
                return true;
            case Key.Enter:
                if (_fndTool == FndTool.Polygon) FinishPolygon();
                else if (_fndTool == FndTool.CopyPick) ApplyCopy(_fndCopyTargets.ToList());
                else FndToolEnd("Kết thúc vẽ đường chuẩn – bấm \"Thiết kế móng\" để thiết kế lại móng theo ranh đất.");
                return true;
        }
        return false;
    }

    private async void FinishPolygon()
    {
        var pts = _fndToolPts.ToList();
        if (pts.Count < 3) { SetStatus("Móng đa giác cần ít nhất 3 đỉnh."); return; }
        var poly = pts.Select(q => (q.X, q.Y)).ToList();
        if (PolyGeom.Properties(poly).Area < 0.2) { SetStatus("Đa giác quá nhỏ (diện tích < 0,2 m²)."); return; }
        // chân cột nằm trong đa giác (ưu tiên móng đang chọn)
        var inside = FoundationUtil.BaseColumns(_p).Select(x => (x.Node, cg: ShapedFooting.Column(_p, x.Column, x.Node)))
            .Where(x => PolyGeom.Inside(poly, x.cg.Cx, x.cg.Cy)).ToList();
        if (inside.Count == 0) { SetStatus("Không có chân cột nào nằm trong đa giác – vẽ lại bao quanh chân cột."); return; }
        var sel = SelectedFoundationNodes();
        var pick = inside.FirstOrDefault(x => sel.Contains(x.Node.Id));
        if (pick.Node == null) pick = inside[0];
        string snap = Snapshot();
        var pl = _p.Foundation.Placements.FirstOrDefault(x => x.NodeId == pick.Node.Id);
        if (pl == null) { pl = new FootingPlacement { NodeId = pick.Node.Id }; _p.Foundation.Placements.Add(pl); }
        pl.Polygon = poly.Select(q => new[] { Math.Round(q.X - pick.cg.Cx, 4), Math.Round(q.Y - pick.cg.Cy, 4) }).ToList();
        pl.AlignX = pl.AlignY = FootingAlign.Center;
        FoundationReset.Clear(_p, _design, new[] { pick.Node.Id });
        PushUndo(snap);
        MarkDirty(false);
        FndToolEnd($"Móng đa giác {poly.Count} đỉnh, A = {PolyGeom.Properties(poly).Area:0.00} m² gán cho chân cột nút {pick.Node.Id}"
                   + (inside.Count > 1 ? $" (đa giác chứa {inside.Count} chân cột – chỉ gán cho một cột; nhiều cột chung móng: dùng móng kết hợp / móng băng)" : "") + ".");
        if (_res != null) await DesignFoundation();
    }

    private void DrawFndToolOverlay()
    {
        DrawFndMultiOverlay();   // [Gán nhiều móng] khung các móng chọn bằng Ctrl + bấm
        _fndToolLayer = new Canvas { IsHitTestVisible = false };
        fndCanvas.Children.Add(_fndToolLayer);
        RefreshToolLayer();
    }

    private void RefreshToolLayer()
    {
        if (_fndToolLayer == null) return;
        _fndToolLayer.Children.Clear();
        if (_fndTool == FndTool.None) return;
        if (_fndTool == FndTool.CopyPick)
        {
            var nodes = NodeMap();
            foreach (int id in _fndCopyTargets.Append(_fndCopySrc))
            {
                if (!nodes.TryGetValue(id, out var n)) continue;
                var c = FW2S(n.X, n.Y);
                double r = Math.Max(10, 0.35 * _fScale);
                var e = new Shapes.Ellipse { Width = 2 * r, Height = 2 * r, Fill = id == _fndCopySrc ? null : CopyPickBrush, Stroke = ToolBrush, StrokeThickness = id == _fndCopySrc ? 2.5 : 1.2 };
                Canvas.SetLeft(e, c.X - r); Canvas.SetTop(e, c.Y - r);
                _fndToolLayer.Children.Add(e);
            }
            CText(_fndToolLayer, $"NHÂN BẢN MÓNG: nguồn nút {_fndCopySrc} (vòng đậm) → {_fndCopyTargets.Count} chân cột · Enter áp dụng · Esc huỷ", new Point(8, 26), ToolBrush, 11, false);
            return;
        }
        var spts = _fndToolPts.Select(q => FW2S(q.X, q.Y)).ToList();
        if (_fndHoverW != null && (_fndTool == FndTool.Polygon || spts.Count > 0)) spts.Add(FW2S(_fndHoverW.Value.X, _fndHoverW.Value.Y));
        if (spts.Count >= 2)
        {
            var pl = new Shapes.Polyline { Points = new PointCollection(spts), Stroke = _fndTool == FndTool.LotLine ? LotLineBrush : ToolBrush, StrokeThickness = 1.6 };
            if (_fndTool == FndTool.LotLine) pl.StrokeDashArray = new DoubleCollection { 8, 3, 2, 3 };
            _fndToolLayer.Children.Add(pl);
            if (_fndTool == FndTool.Polygon && spts.Count >= 3)
                _fndToolLayer.Children.Add(new Shapes.Line { X1 = spts[^1].X, Y1 = spts[^1].Y, X2 = spts[0].X, Y2 = spts[0].Y, Stroke = ToolBrush, StrokeThickness = 0.8, StrokeDashArray = new DoubleCollection { 3, 3 } });
        }
        foreach (var q in _fndToolPts.Select(q => FW2S(q.X, q.Y)))
        {
            var r = new Shapes.Rectangle { Width = 6, Height = 6, Fill = ToolBrush };
            Canvas.SetLeft(r, q.X - 3); Canvas.SetTop(r, q.Y - 3);
            _fndToolLayer.Children.Add(r);
        }
        if (_fndHoverW != null)
        {
            var h = FW2S(_fndHoverW.Value.X, _fndHoverW.Value.Y);
            if (!string.IsNullOrEmpty(_fndHoverKind))
            {
                var sq = new Shapes.Rectangle { Width = 12, Height = 12, Stroke = Brushes.DarkOrange, StrokeThickness = 2 };
                Canvas.SetLeft(sq, h.X - 6); Canvas.SetTop(sq, h.Y - 6);
                _fndToolLayer.Children.Add(sq);
            }
            string info = $"({_fndHoverW.Value.X.ToString("0.###", Inv)}; {_fndHoverW.Value.Y.ToString("0.###", Inv)})" + (string.IsNullOrEmpty(_fndHoverKind) ? "" : " – " + _fndHoverKind);
            if (_fndToolPts.Count > 0)
            {
                var last = _fndToolPts[^1];
                double dx = _fndHoverW.Value.X - last.X, dy = _fndHoverW.Value.Y - last.Y;
                info += $"  L = {Math.Sqrt(dx * dx + dy * dy).ToString("0.###", Inv)} m, góc {(Math.Atan2(dy, dx) * 180 / Math.PI).ToString("0.#", Inv)}°";
            }
            CText(_fndToolLayer, info, new Point(h.X + 12, h.Y + 12), Brushes.DarkOrange, 10, false, false, true);
        }
        string title = _fndTool == FndTool.LotLine
            ? "VẼ ĐƯỜNG CHUẨN (ranh đất): bấm các điểm · Backspace bỏ điểm · Enter / Esc kết thúc"
            : $"VẼ MÓNG ĐA GIÁC: {_fndToolPts.Count} đỉnh · bấm lại đỉnh đầu hoặc Enter để kết thúc · Backspace bỏ đỉnh · Esc huỷ";
        CText(_fndToolLayer, title, new Point(8, 26), ToolBrush, 11, false);
    }

    // ------------------------------------------------------------------
    // Hộp thoại
    // ------------------------------------------------------------------
    private int SelectedIsolatedNode()
    {
        var nodes = SelectedFoundationNodes();
        if (nodes.Count == 0 && _sel != null && _sel.IsVerticalKind)
        {
            var a = _p.Node(_sel.NodeI); var b = _p.Node(_sel.NodeJ);
            var lo = a.Z <= b.Z ? a : b;
            if (lo.IsSupport) nodes.Add(lo.Id);
        }
        return nodes.Count == 0 ? 0 : nodes[0];
    }

    private static readonly string[] AngleItems = { "Song song trục X / Y", "Theo mép cột (cột xoay thì móng xoay)", "Góc nhập tay", "Theo đường chuẩn gần nhất" };
    private static readonly string[] AlignItems =
    {
        "Đúng tâm cột", "Mép móng phía âm thẳng mép cột (móng chìa về phía dương)", "Mép móng phía dương thẳng mép cột (móng chìa về phía âm)", "Lệch tâm nhập tay"
    };

    private async void FndPlacementUi()
    {
        if (_p == null || _busy) return;
        int node = SelectedIsolatedNode();
        if (node == 0) { SetStatus("Bấm chọn một móng (hoặc chân cột) trên mặt bằng móng trước."); return; }
        var old = ShapedFooting.PlacementOf(_p, node);
        var pl = old?.Clone() ?? new FootingPlacement { NodeId = node };
        var f = new MiniForm(this, $"Bố trí móng tại chân cột nút {node}", 560);
        f.Combo("ang", "Góc móng (phương L)", AngleItems, (int)pl.AngleMode);
        f.Num("angv", "Góc nhập tay (độ, ngược chiều kim đồng hồ từ trục X)", pl.Angle);
        f.Combo("ax", "Dóng theo phương L (dọc cạnh dài)", AlignItems, (int)pl.AlignX);
        f.Num("ox", "Lệch tâm phương L: tâm móng − tim cột (m)", pl.OffsetX);
        f.Combo("ay", "Dóng theo phương B (vuông góc L)", AlignItems, (int)pl.AlignY);
        f.Num("oy", "Lệch tâm phương B: tâm móng − tim cột (m)", pl.OffsetY);
        f.Check("clip", "Cắt móng theo đường chuẩn (không vượt ranh đất)", pl.ClipToLot);
        f.Check("strap", "Móng lệch tâm: nối dầm móng cân bằng tới cột đối diện (R1 = N·Ls/(Ls − e), áp lực đáy gần đều)", pl.StrapBeam);
        if (pl.IsPolygon) f.Check("poly", $"Giữ móng đa giác đã vẽ ({pl.Polygon.Count} đỉnh) – bỏ chọn để trở về móng chữ nhật", true);
        f.Check("same", "Áp dụng cho mọi chân cột cùng tiết diện cột", false);
        f.Check("reset", "Xoá bố trí riêng (trở về móng chữ nhật đặt tâm cột)", false);
        f.Check("run", "Thiết kế lại móng ngay", true);
        f.Hint("Phía âm / dương theo hệ trục của móng: phương L = trục X xoay theo góc móng, phương B vuông góc. "
               + "VD cột sát ranh trái: dóng phương L \"Mép móng phía âm thẳng mép cột\" → móng chìa vào trong lô đất. "
               + "Kích thước L × B: \"Sửa móng…\" (0 = tự chọn, móng lệch tâm tự tăng kích thước tới khi đạt áp lực nền).");
        if (!f.Run("Áp dụng")) return;
        string snap = Snapshot();
        var targets = new List<int> { node };
        foreach (int x in SelectedFoundationNodes()) if (!targets.Contains(x)) targets.Add(x);   // [Gán nhiều móng] Ctrl + bấm
        if (f.B("same")) targets.AddRange(FootingLayout.SameColumnNodes(_p, node));
        foreach (int t in targets)
        {
            _p.Foundation.Placements.RemoveAll(x => x.NodeId == t);
            FoundationReset.Clear(_p, _design, new[] { t });
            if (f.B("reset")) continue;
            var c = new FootingPlacement
            {
                NodeId = t, AngleMode = (FootingAngleMode)Math.Max(0, f.I("ang")), Angle = f.D("angv", 0),
                AlignX = (FootingAlign)Math.Max(0, f.I("ax")), OffsetX = f.D("ox", 0), AlignY = (FootingAlign)Math.Max(0, f.I("ay")), OffsetY = f.D("oy", 0),
                ClipToLot = f.B("clip"), StrapBeam = f.B("strap"), Polygon = pl.IsPolygon && f.B("poly") ? pl.Polygon.Select(v => new[] { v[0], v[1] }).ToList() : null
            };
            _p.Foundation.Placements.Add(c);
        }
        PushUndo(snap);
        MarkDirty(false);
        RedrawFoundationPlan();
        string what = f.B("reset") ? "xoá bố trí riêng" : $"{AngleItems[Math.Max(0, f.I("ang"))].ToLowerInvariant()}; L: {AlignItems[Math.Max(0, f.I("ax"))].ToLowerInvariant()}";
        if (f.B("run") && await DesignFoundation()) ReportShaped(node, what, targets.Count);
        else SetStatus($"Đã lưu bố trí móng ({what}) cho {targets.Count} chân cột – bấm \"Thiết kế móng\" để thiết kế lại.");
    }

    private void ReportShaped(int node, string what, int count)
    {
        var fd = _design?.Footings.FirstOrDefault(x => EccHasNode(x, node));
        SetStatus(fd == null ? $"Bố trí móng ({what}) đã áp dụng cho {count} chân cột."
            : $"Móng {fd.Name} ({what}, {count} chân cột): khung bao {fd.L:0.00}×{fd.B:0.00}×{fd.H:0.00} m{(fd.Shaped ? $", A = {fd.Area:0.00} m²" : "")}, "
              + $"pmax = {Math.Min(fd.Pmax, 99999):0} kN/m² – {(fd.Ok ? "ĐẠT" : "KHÔNG ĐẠT")} (Hoàn tác để trở lại).");
    }

    private void FndCopyUi()
    {
        if (_p == null || _busy) return;
        int node = SelectedIsolatedNode();
        if (node == 0) { SetStatus("Bấm chọn móng nguồn trên mặt bằng móng trước."); return; }
        var (L, B, H) = FootingLayout.SizeOf(_p, _design, node);
        var f = new MiniForm(this, $"Nhân bản móng tại chân cột nút {node}", 560);
        f.Combo("to", "Nhân bản sang", new[]
        {
            "Các chân cột bấm chọn trên mặt bằng (Enter áp dụng)", "Mọi chân cột cùng tiết diện cột", "Mọi chân cột móng đơn", "Danh sách nút nhập tay"
        }, 0);
        f.Text("list", "Danh sách nút (vd. 3, 5, 8-11)", "");
        f.Check("size", L > 0 ? $"Chép kích thước {L:0.00} × {B:0.00} × {H:0.00} m (chốt cho chân cột đích)" : "Chép kích thước (móng nguồn chưa có kích thước – bỏ qua)", L > 0);
        f.Check("pl", ShapedFooting.PlacementOf(_p, node) != null ? "Chép bố trí (góc, dóng mép cột, lệch tâm, đa giác)" : "Chép bố trí (móng nguồn đặt tâm – xoá bố trí riêng ở chân cột đích)", true);
        f.Check("run", "Thiết kế lại móng ngay", true);
        f.Hint("Móng đích giống hệt móng nguồn sẽ được gộp chung một tên móng (vd. M2, SL = 6). Nếu nội lực chân cột đích lớn hơn, chương trình báo không đạt – "
               + "khi đó dùng \"Đồng nhất móng…\" hoặc tăng kích thước móng nguồn.");
        if (!f.Run("Tiếp tục")) return;
        _fndCopySize = f.B("size"); _fndCopyPl = f.B("pl"); _fndCopyRun = f.B("run");
        _fndCopySrc = node;
        _fndCopyTargets.Clear();
        switch (f.I("to"))
        {
            case 0: FndToolStart(FndTool.CopyPick); return;
            case 1: ApplyCopy(FootingLayout.SameColumnNodes(_p, node)); return;
            case 2: ApplyCopy(FootingLayout.AllIsolatedNodes(_p, node)); return;
            default:
                var ids = ParseIdList(f.S("list"));
                if (ids.Count == 0) { SetStatus("Danh sách nút rỗng."); return; }
                ApplyCopy(ids);
                return;
        }
    }

    private static List<int> ParseIdList(string s)
    {
        var res = new List<int>();
        foreach (var part in (s ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var r = part.Split('-');
            if (r.Length == 2 && int.TryParse(r[0], out int a) && int.TryParse(r[1], out int b))
                for (int i = Math.Min(a, b); i <= Math.Max(a, b) && res.Count < 5000; i++) res.Add(i);
            else if (int.TryParse(part, out int v)) res.Add(v);
        }
        return res.Distinct().ToList();
    }

    private async void ApplyCopy(List<int> targets)
    {
        int src = _fndCopySrc;
        var valid = new HashSet<int>(FoundationUtil.BaseColumns(_p).Select(x => x.Node.Id));
        targets = targets.Where(t => t != src && valid.Contains(t)).Distinct().ToList();
        if (_fndTool == FndTool.CopyPick) FndToolEnd();
        if (targets.Count == 0) { SetStatus("Chưa chọn chân cột đích nào để nhân bản móng."); return; }
        string snap = Snapshot();
        var r = FootingLayout.CopyTo(_p, _design, src, targets, _fndCopySize, _fndCopyPl);
        PushUndo(snap);
        MarkDirty(false);
        RedrawFoundationPlan();
        string what = $"nhân bản móng nút {src} → {r.Targets} chân cột ({r.Sizes} kích thước, {r.Placements} bố trí)";
        if (_fndCopyRun && _res != null && await DesignFoundation())
        {
            var fd = _design?.Footings.FirstOrDefault(x => EccHasNode(x, src));
            int same = fd == null ? 0 : targets.Count(t => EccHasNode(fd, t));
            SetStatus($"Đã {what}. Thiết kế lại: {same}/{targets.Count} chân cột gộp chung móng {fd?.Name}"
                      + (same < targets.Count ? " – các chân cột còn lại có nội lực / tiết diện cột khác nên tách loại móng riêng" : "") + ".");
        }
        else SetStatus($"Đã {what} – bấm \"Thiết kế móng\" để thiết kế lại.");
    }

    private async void FndUnifyUi()
    {
        if (_p == null || _busy) return;
        if (_design == null || _design.Footings.Count == 0) { SetStatus("Chưa có kết quả thiết kế móng đơn – bấm \"Thiết kế móng\" trước."); return; }
        var f = new MiniForm(this, "Đồng nhất móng giống nhau", 520);
        f.Num("tol", "Dung sai (m): móng có cạnh nhỏ hơn không quá giá trị này gộp vào móng lớn", 0.3);
        f.Check("col", "Chỉ gộp móng cùng tiết diện cột", true);
        f.Check("run", "Thiết kế lại móng ngay", true);
        f.Hint("Mỗi nhóm lấy kích thước L, B, H lớn nhất của nhóm và chốt cho mọi chân cột trong nhóm → giảm số loại móng, thi công đồng loạt. "
               + "Móng đa giác và móng kết hợp không gộp.");
        if (!f.Run("Đồng nhất")) return;
        var preview = FootingLayout.Unify(_p, _design, Math.Max(0, f.D("tol", 0.3)), f.B("col"), null, false);
        if (preview.Count == 0) { SetStatus("Không có nhóm móng nào gộp được với dung sai này."); return; }
        string snap = Snapshot();
        var groups = FootingLayout.Unify(_p, _design, Math.Max(0, f.D("tol", 0.3)), f.B("col"));
        PushUndo(snap);
        MarkDirty(false);
        RedrawFoundationPlan();
        string what = $"{groups.Count} nhóm, {groups.Sum(g => g.Nodes.Count)} móng: " + string.Join("; ", groups.Take(6).Select(g => $"{g.L:0.0#}×{g.B:0.0#}×{g.H:0.0#} ({g.Nodes.Count})"));
        if (f.B("run") && _res != null && await DesignFoundation())
            SetStatus($"Đã đồng nhất móng – {what}. Còn {_design.Footings.Count} loại móng đơn.");
        else SetStatus($"Đã đồng nhất móng – {what}. Bấm \"Thiết kế móng\" để thiết kế lại.");
    }

    private async void FndLotLinesUi()
    {
        if (_p == null || _busy) return;
        var f = new MiniForm(this, "Đường chuẩn (ranh đất xiên)", 640);
        var tb = new TextBox
        {
            AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, Height = 180, FontFamily = new FontFamily("Consolas"),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = string.Join(Environment.NewLine, _p.Foundation.LotLines.Select(l =>
                string.Join("  ", new[] { l.X1, l.Y1, l.X2, l.Y2, l.Clearance }.Select(v => v.ToString("0.###", Inv))) + "  " + (l.Enabled ? "1" : "0") + "  " + l.Name))
        };
        f.Hint("Mỗi dòng một đường chuẩn: X1 Y1 X2 Y2 (m)  khe_hở (m)  bật(1/0)  tên. Móng không vượt qua đường (phía chứa tim cột là phía đặt móng), "
               + "cách đường một khe hở. Vẽ bằng chuột: nút \"Vẽ đường chuẩn\" (bắt dính góc cột tại góc đất).");
        f.Add(tb);
        f.Check("run", "Thiết kế lại móng ngay", true);
        if (!f.Run("Lưu")) return;
        var list = new List<LotLine>();
        int ln = 0;
        foreach (var raw in tb.Text.Split('\n'))
        {
            ln++;
            var parts = raw.Trim().Split(new[] { ' ', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            var nums = new List<double>();
            int i = 0;
            for (; i < parts.Length && nums.Count < 6; i++)
            {
                if (!double.TryParse(parts[i].Replace(',', '.'), System.Globalization.NumberStyles.Float, Inv, out double v)) break;
                nums.Add(v);
            }
            if (nums.Count < 4) { SetStatus($"Dòng {ln}: cần ít nhất 4 số X1 Y1 X2 Y2 – bỏ qua."); continue; }
            var l = new LotLine
            {
                X1 = nums[0], Y1 = nums[1], X2 = nums[2], Y2 = nums[3], Clearance = nums.Count > 4 ? Math.Max(0, nums[4]) : 0,
                Enabled = nums.Count <= 5 || nums[5] != 0, Name = string.Join(" ", parts.Skip(i))
            };
            if (l.Length > 1e-6) list.Add(l);
        }
        string snap = Snapshot();
        _p.Foundation.LotLines = list;
        PushUndo(snap);
        MarkDirty(false);
        RedrawFoundationPlan();
        if (f.B("run") && _res != null && await DesignFoundation()) SetStatus($"Đã lưu {list.Count} đường chuẩn và thiết kế lại móng.");
        else SetStatus($"Đã lưu {list.Count} đường chuẩn – bấm \"Thiết kế móng\" để thiết kế lại.");
    }
}

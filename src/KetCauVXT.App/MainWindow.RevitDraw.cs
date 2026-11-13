using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// VẼ KIỂU REVIT TRÊN MẶT BẰNG (phần vẽ / thao tác chuột):
//   • tường nhiều lớp: tô gạch chéo, nối góc L / T gọn (hợp nhất đường viền), cắt ô cửa; mức chi tiết Thô / Trung bình / Chi tiết;
//   • cửa gắn tường (cửa đi: cánh + cung mở; cửa sổ: 3 nét kính; ô thoáng cao: nét đứt) + thẻ D1 / S1;
//   • công cụ "Đặt cửa": rê chuột lên tường → xem trước cửa bắt vào tường + kích thước tạm, Space lật hướng mở, bấm để đặt;
//   • chọn cửa: kích thước tạm (bấm số để sửa khoảng cách như Revit), Space lật, Delete xoá;
//   • tường: đường định vị (tim / mặt hoàn thiện / mặt lõi), vẽ chuỗi hoặc từng đoạn, kích thước tạm (dài + góc) khi vẽ;
//   • nhãn tự tránh chồng, bật / tắt thẻ theo loại (Hiển thị / Đồ hoạ).
// Lõi: Model/WallAssembly.cs. Giao diện ribbon / thuộc tính: MainWindow.RevitUi.cs.
// =====================================================================
public partial class MainWindow
{
    private readonly PlanVisibility _vg = new();
    /// <summary>Hộp bao các nhãn đã đặt trong lần vẽ mặt bằng hiện tại (tránh chồng); null = không tránh.</summary>
    private List<Rect> _lblBoxes;
    /// <summary>Kiểu cửa đang đặt (công cụ Cửa đi / Cửa sổ); null = không đặt.</summary>
    private OpeningType _rvPlaceType;
    private bool _rvPlaceFlip, _rvPlaceHand;
    private WallLine _rvSelWall;
    private WallOpening _rvSelOpen;
    private Canvas _rvDimLayer;
    private string _rvLocation = LocationLines.WallCenter;
    private bool _rvChain = true;

    private static readonly Brush RvBlue = Frozen(Color.FromRgb(0, 120, 215));
    private static readonly Brush RvBlueFill = Frozen(Color.FromArgb(70, 0, 120, 215));
    private static readonly Brush RvCutFill = Frozen(Color.FromRgb(120, 120, 120));
    private static readonly Brush RvDoorStroke = Frozen(Color.FromRgb(40, 40, 40));
    private static readonly Brush RvGlass = Frozen(Color.FromRgb(0, 140, 200));

    // ---- Hiển thị / Đồ hoạ: các thẻ (chế độ cổ điển: luôn như cũ) ----
    private bool VgBeamTags => !_revitOn || _vg.BeamTags;
    private bool VgBeamSection => !_revitOn || !_vg.BeamNameOnly;
    private bool VgColumnTags => !_revitOn || _vg.ColumnTags;
    private bool VgSlabTags => !_revitOn || _vg.SlabTags;
    private bool VgRoomUse => !_revitOn || _vg.RoomUse;
    private bool VgWallTags => !_revitOn || _vg.WallTags;

    /// <summary>Gọi đầu RedrawPlan: bật / tắt tránh chồng nhãn.</summary>
    private void BeginLabelLayout()
    {
        _lblBoxes = null;
        _lblPlacer = _vg.AvoidOverlap ? new KetCauVXT.Ui.PlanLabelPlacer() : null;   // [Nhãn] đặt nhãn theo ưu tiên, tránh chồng mọi loại – cả giao diện cổ điển
        _rvDimLayer?.Children.Clear();
    }

    /// <summary>[Nhãn] đặt toàn bộ nhãn đã gom (Ui/PlanFit.cs – PlanLabelPlacer): trục giữ chỗ trước, rồi cột → dầm → sàn → khác;
    /// nhãn không còn chỗ bị ẩn, rê chuột vào vị trí của nó hiện chú giải.</summary>
    private void EndLabelLayout()
    {
        var lp = _lblPlacer;
        _lblPlacer = null;
        if (lp == null) return;
        lp.Solve();
        foreach (var it in lp.Items)
        {
            if (it.Tag is not TextBlock t) continue;
            if (it.Placed is { } b)
            {
                Canvas.SetLeft(t, b.X); Canvas.SetTop(t, b.Y);
                planCanvas.Children.Add(t);
            }
            else
            {
                var tip = new Shapes.Rectangle { Width = Math.Max(it.Want.W, 6), Height = Math.Max(it.Want.H, 6), Fill = Brushes.Transparent, ToolTip = t.Text };
                Canvas.SetLeft(tip, it.Want.X); Canvas.SetTop(tip, it.Want.Y);
                planCanvas.Children.Add(tip);
            }
        }
        _lblDropped = lp.Dropped.Count();
    }
    private KetCauVXT.Ui.PlanLabelPlacer _lblPlacer;
    /// <summary>Số nhãn bị ẩn vì hết chỗ ở lần vẽ gần nhất (tự kiểm ghi nhật ký).</summary>
    private int _lblDropped;
    private static readonly Brush LabelHalo = Frozen(Color.FromArgb(185, 255, 255, 255));

    /// <summary>Tránh chồng nhãn (AddText): trả về vị trí trái-trên mới hoặc null = bỏ nhãn (không còn chỗ). Nhãn trục / tiêu đề chỉ ghi chỗ, không dời.</summary>
    private Point? PlaceLabel(double x, double y, double w, double h, bool movable)
    {
        if (_lblBoxes == null) return new Point(x, y);
        var r = new Rect(x, y, w, h);
        if (!movable) { _lblBoxes.Add(r); return new Point(x, y); }
        foreach (var (dx, dy) in new[] { (0.0, 0.0), (0, h + 1), (0, -h - 1), (w * 0.6, 0), (-w * 0.6, 0), (0, 2 * h + 2), (0, -2 * h - 2) })
        {
            var c = new Rect(x + dx, y + dy, w, h);
            bool hit = false;
            foreach (var b in _lblBoxes) if (b.IntersectsWith(c)) { hit = true; break; }
            if (!hit) { _lblBoxes.Add(c); return new Point(c.X, c.Y); }
        }
        return null;
    }

    // ==================================================================
    // Tường + cửa
    // ==================================================================
    private double WallDrawThickness(WallLine w)
    {
        var wt = _p.WallTypes.FirstOrDefault(t => t.Name == w.Type);
        if (wt == null) return 0.11;
        return _revitOn ? Math.Max(wt.TotalThickness, 0.05) : Math.Max(wt.Thickness, 0.05);
    }

    private Geometry ScreenPoly((double X, double Y)[] pts)
    {
        var fig = new PathFigure { StartPoint = W2S(pts[0].X, pts[0].Y), IsClosed = true, IsFilled = true };
        for (int i = 1; i < pts.Length; i++) fig.Segments.Add(new LineSegment(W2S(pts[i].X, pts[i].Y), true));
        var g = new PathGeometry(); g.Figures.Add(fig);
        return g;
    }

    /// <summary>Vẽ tường kiểu Revit (gọi đầu DrawWalls). false = để cách vẽ cũ (chế độ cổ điển và không có cửa gắn tường).</summary>
    private bool DrawWallsRevit(bool labels)
    {
        if (_p == null) return false;
        var walls = PlanWalls().ToList();
        if (!_revitOn && !walls.Any(w => w.Openings is { Count: > 0 })) return false;
        var straight = walls.Where(w => !w.IsCurved).ToList();
        var joins = WallAssembly.Joins(straight, WallDrawThickness);
        var fills = new Dictionary<Brush, Geometry>();
        Geometry outline = null;
        bool coarse = _revitOn && _vg.Detail == PlanVisibility.Coarse, fine = _revitOn && _vg.Detail == PlanVisibility.Fine;
        foreach (var w in walls)
        {
            var wt = _p.WallTypes.FirstOrDefault(t => t.Name == w.Type);
            double th = WallDrawThickness(w);
            if (w.IsCurved)
            {
                // tường cong: cách vẽ cũ (một dải)
                var a = W2S(w.X1, w.Y1); var b = W2S(w.X2, w.Y2);
                var quad = WallQuad(a, b, Math.Max(1.5, th * _scale / 2), out _, out _);
                planCanvas.Children.Add(new Shapes.Polygon { Points = quad, Fill = WallFill(wt), Stroke = WallStroke, StrokeThickness = 1, IsHitTestVisible = false });
                continue;
            }
            var (e0, e1) = joins.TryGetValue(w.Id, out var j) ? j : (0.0, 0.0);
            Brush fill = coarse ? RvCutFill : WallFill(wt);
            foreach (var poly in WallAssembly.PlanPolygons(_p, w, th, e0, e1))
            {
                var g = ScreenPoly(poly);
                fills[fill] = fills.TryGetValue(fill, out var prev) ? Geometry.Combine(prev, g, GeometryCombineMode.Union, null) : g;
                outline = outline == null ? g : Geometry.Combine(outline, g, GeometryCombineMode.Union, null);
            }
            if (fine && wt is { IsLayered: true }) DrawCoreLines(w, wt, th);
        }
        foreach (var kv in fills)
            planCanvas.Children.Add(new Shapes.Path { Data = kv.Value, Fill = kv.Key, Opacity = 0.95, IsHitTestVisible = false });
        if (outline != null)
            planCanvas.Children.Add(new Shapes.Path { Data = outline, Stroke = WallStroke, StrokeThickness = _revitOn ? 1.4 : 1, StrokeLineJoin = PenLineJoin.Miter, IsHitTestVisible = false });

        foreach (var w in straight.Where(_ => !_hideDoors))
            foreach (var o in w.Openings ?? new List<WallOpening>())
                DrawHostedOpening(planCanvas, w, o, WallAssembly.TypeOf(_p, o), o.Offset, o.Flip, o.FlipHand, false, labels && (!_revitOn || _vg.DoorTags));

        if (labels && VgWallTags)
            foreach (var w in walls)
            {
                var wt = _p.WallTypes.FirstOrDefault(t => t.Name == w.Type);
                var a = W2S(w.X1, w.Y1); var b = W2S(w.X2, w.Y2);
                if ((b - a).Length <= 30) continue;
                string tag = wt != null ? "T" + (wt.Thickness * 1000).ToString("0", Inv) : "T?";
                var m = a + (b - a) / 2;
                bool vertical = Math.Abs(b.X - a.X) < Math.Abs(b.Y - a.Y);
                double hw = WallDrawThickness(w) * _scale / 2;
                if (vertical) AddText(tag, new Point(m.X - hw - 26, m.Y + 10), WallLabelBrush, 9, false, true);
                else AddText(tag, new Point(m.X + 22, m.Y + hw + 7), WallLabelBrush, 9, true, true);
            }
        DrawRevitSelection();
        return true;
    }

    /// <summary>Mức chi tiết: đường ranh lõi kết cấu (nét mảnh) trên các đoạn tường đặc.</summary>
    private void DrawCoreLines(WallLine w, WallType wt, double th)
    {
        double acc = 0, c0 = -1, c1 = -1;
        foreach (var l in wt.Layers)
        {
            if (l.IsCore && c0 < 0) c0 = acc;
            acc += l.Thickness;
            if (l.IsCore) c1 = acc;
        }
        if (c0 < 0) return;
        double h = th / 2;
        double dx = w.X2 - w.X1, dy = w.Y2 - w.Y1, L = Math.Sqrt(dx * dx + dy * dy);
        if (L < 1e-9) return;
        double ux = dx / L, uy = dy / L, nx = -uy, ny = ux;
        foreach (var (a, b) in WallAssembly.SolidSegments(_p, w))
            foreach (double s in new[] { h - c0, h - c1 })
            {
                if (Math.Abs(Math.Abs(s) - h) < 1e-4) continue;   // trùng mặt ngoài
                var p0 = W2S(w.X1 + ux * a + nx * s, w.Y1 + uy * a + ny * s);
                var p1 = W2S(w.X1 + ux * b + nx * s, w.Y1 + uy * b + ny * s);
                CLine(planCanvas, p0, p1, WallStroke, 0.6, false);
            }
    }

    /// <summary>Ký hiệu cửa trên mặt bằng (cánh + cung mở, cửa sổ 3 nét, ô thoáng nét đứt) + thẻ.</summary>
    private void DrawHostedOpening(Canvas cv, WallLine w, WallOpening o, OpeningType t, double offset, bool flip, bool hand, bool preview, bool tag)
    {
        if (t == null) return;
        double dx = w.X2 - w.X1, dy = w.Y2 - w.Y1, L = Math.Sqrt(dx * dx + dy * dy);
        if (L < 1e-9) return;
        double ux = dx / L, uy = dy / L, nx = -uy, ny = ux;
        double h = WallDrawThickness(w) / 2, W = t.Width;
        double side = flip ? -1 : 1;
        (double, double) P(double along, double across) => (w.X1 + ux * along + nx * across, w.Y1 + uy * along + ny * across);
        Point S((double X, double Y) q) => W2S(q.X, q.Y);
        double a = offset - W / 2, b = offset + W / 2;
        var br = preview ? RvBlue : RvDoorStroke;
        if (preview)
        {
            var g = ScreenPoly(new[] { P(a, h), P(b, h), P(b, -h), P(a, -h) });
            cv.Children.Add(new Shapes.Path { Data = g, Fill = RvBlueFill, Stroke = RvBlue, StrokeThickness = 1, IsHitTestVisible = false });
        }
        if (!WallAssembly.CutsPlan(t))
        {
            // ô mở cao hơn mặt phẳng cắt: nét đứt
            var g = ScreenPoly(new[] { P(a, h), P(b, h), P(b, -h), P(a, -h) });
            cv.Children.Add(new Shapes.Path { Data = g, Stroke = br, StrokeThickness = 0.8, StrokeDashArray = new DoubleCollection { 3, 2 }, IsHitTestVisible = false });
        }
        else if (t.Kind == WallOpeningKind.Window || t.Kind == WallOpeningKind.Vent)
        {
            CLine(cv, S(P(a, h)), S(P(a, -h)), br, 1, false);
            CLine(cv, S(P(b, h)), S(P(b, -h)), br, 1, false);
            foreach (double s in new[] { h * 0.55, 0, -h * 0.55 })
                CLine(cv, S(P(a, s)), S(P(b, s)), s == 0 ? RvGlass : br, s == 0 ? 1.2 : 0.8, false);
        }
        else if (t.Kind == WallOpeningKind.Hole)
        {
            CLine(cv, S(P(a, h)), S(P(b, -h)), br, 0.8, false);
            CLine(cv, S(P(a, -h)), S(P(b, h)), br, 0.8, false);
        }
        else if (t.Operation == "Trượt")
        {
            CLine(cv, S(P(a, h * 0.3)), S(P(offset + 0.05, h * 0.3)), br, 1.6, false);
            CLine(cv, S(P(offset - 0.05, -h * 0.3)), S(P(b, -h * 0.3)), br, 1.6, false);
        }
        else
        {
            // cửa cánh mở: 1 cánh (bản lề trái / phải) hoặc 2 cánh
            void Leaf(double hinge, double free, double len)
            {
                var hp = P(hinge, side * h);
                var tip = P(hinge, side * (h + len));
                var end = P(free, side * h);
                Point sh = S(hp), st = S(tip), se = S(end);
                CLine(cv, sh, st, br, 1.3, false);
                var vs = st - sh; var ve = se - sh;
                double cross = vs.X * ve.Y - vs.Y * ve.X;
                var fig = new PathFigure { StartPoint = st, IsClosed = false };
                double r = len * _scale;
                fig.Segments.Add(new ArcSegment(se, new Size(r, r), 0, false, cross > 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, true));
                var pg = new PathGeometry(); pg.Figures.Add(fig);
                cv.Children.Add(new Shapes.Path { Data = pg, Stroke = br, StrokeThickness = 0.7, StrokeDashArray = new DoubleCollection { 3, 2 }, IsHitTestVisible = false });
            }
            CLine(cv, S(P(a, h)), S(P(a, -h)), br, 1, false);
            CLine(cv, S(P(b, h)), S(P(b, -h)), br, 1, false);
            if (t.Operation == "2 cánh") { Leaf(a, offset, W / 2); Leaf(b, offset, W / 2); }
            else if (hand) Leaf(b, a, W);
            else Leaf(a, b, W);
        }
        if (tag && !preview)
        {
            var c = S(P(offset, -side * (h + 0.05)));
            double off = 9;
            var n = new Vector(nx, -ny);   // màn hình: y ngược
            n.Normalize();
            var pt = c - side * n * off;
            AddText(t.Tag, pt, br, 9, true, true, cv == planCanvas ? null : cv);
        }
    }

    // ==================================================================
    // Chọn cửa – kích thước tạm (bấm số để sửa)
    // ==================================================================
    private (WallLine W, WallOpening O)? HostedOpeningAt(Point s)
    {
        if (_p == null) return null;
        var (x, y) = S2W(s);
        foreach (var w in PlanWalls())
        {
            if (w.IsCurved || w.Openings is not { Count: > 0 }) continue;
            double t = WallAssembly.ProjectPoint(w, x, y, out double d);
            if (d > WallDrawThickness(w) / 2 + 6 / Math.Max(_scale, 1e-6)) continue;
            foreach (var o in w.Openings)
            {
                var (a, b) = WallAssembly.Span(_p, o);
                if (t >= a - 1e-6 && t <= b + 1e-6) return (w, o);
            }
        }
        return null;
    }

    private Canvas DimLayer()
    {
        if (_rvDimLayer != null) return _rvDimLayer;
        if (planCanvas?.Parent is not Grid g) return null;
        _rvDimLayer = new Canvas { Background = null };
        g.Children.Add(_rvDimLayer);
        return _rvDimLayer;
    }

    /// <summary>Tô sáng cửa / tường đang chọn và kích thước tạm (lớp riêng bấm được).</summary>
    private void DrawRevitSelection()
    {
        var layer = DimLayer();
        layer?.Children.Clear();
        if (layer == null || _p == null) return;
        if (DrawFurnitureSelection(layer)) return;   // đồ đạc đang chọn + kích thước tạm – MainWindow.Furnish.cs
        if (_rvSelOpen != null && _rvSelWall != null && _p.Walls.Contains(_rvSelWall) && _rvSelWall.Openings.Contains(_rvSelOpen))
        {
            var t = WallAssembly.TypeOf(_p, _rvSelOpen);
            if (t == null) return;
            DrawHostedOpening(layer, _rvSelWall, _rvSelOpen, t, _rvSelOpen.Offset, _rvSelOpen.Flip, _rvSelOpen.FlipHand, true, false);
            DrawTempDims(layer, _rvSelWall, t.Width, _rvSelOpen.Offset, _rvSelOpen.Id, true);
            return;
        }
        if (_revitOn && _selWall != null && _p.Walls.Contains(_selWall) && !_selWall.IsCurved)
        {
            // tường đang chọn: kích thước tạm chiều dài
            var w = _selWall;
            double h = WallDrawThickness(w) / 2 + 0.35;
            RvDimLine(layer, w, 0, w.Chord, h, $"{w.Chord * 1000:0}", null);
            // khoảng cách tới tường / trục song song hai bên – bấm số để dời tường (Model/ElementTempDims.cs)
            DrawElemDims(layer, ElementTempDims.ForWall(_p, w), d => EditElemDim(d, v => ElementTempDims.MoveWall(w, d, v), $"tường #{w.Id}"));
            return;
        }
        if (_revitOn && _sel != null && _sel.IsVerticalKind && _p.Frames.Contains(_sel))
        {
            var col = _sel;   // cột đang chọn: tâm cột → trục gần nhất (lệch tâm) – bấm số để đổi lệch tâm
            DrawElemDims(layer, ElementTempDims.ForColumn(_p, col), d => EditElemDim(d, v => ElementTempDims.SetColumnOffset(_p, col, d, v), $"cột {col.Label}"));
        }
    }

    /// <summary>Vẽ các kích thước tạm (đường xanh + số bấm sửa) giữa các cặp điểm mặt bằng.</summary>
    private void DrawElemDims(Canvas cv, List<ElemTempDim> dims, Action<ElemTempDim> edit)
    {
        foreach (var d in dims)
        {
            var pa = W2S(d.X1, d.Y1); var pb = W2S(d.X2, d.Y2);
            CLine(cv, pa, pb, RvBlue, 1, true);
            var tb = new TextBlock
            {
                Text = $"{d.Value * 1000:0}", Foreground = RvBlue, FontSize = 11, Background = Brushes.White, Padding = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.IBeam, ToolTip = $"Khoảng cách tới {d.Target.ToLowerInvariant()} {d.RefName} – bấm để nhập số mới (mm)", TextDecorations = TextDecorations.Underline
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var m = new Point((pa.X + pb.X) / 2, (pa.Y + pb.Y) / 2);
            Canvas.SetLeft(tb, m.X - tb.DesiredSize.Width / 2); Canvas.SetTop(tb, m.Y - tb.DesiredSize.Height - 1);
            var dd = d;
            tb.MouseLeftButtonDown += (s, e) => { e.Handled = true; edit(dd); };
            cv.Children.Add(tb);
        }
    }

    private void EditElemDim(ElemTempDim d, Func<double, bool> apply, string what)
    {
        string v = DialogUi.InputBox(this, "Kích thước tạm", $"Khoảng cách từ {what} tới {d.Target.ToLowerInvariant()} {d.RefName} (mm):", (d.Value * 1000).ToString("0", System.Globalization.CultureInfo.InvariantCulture));
        if (v == null) return;
        double mm = DialogUi.ParseNum(v, double.NaN);
        if (double.IsNaN(mm) || mm < 0) { SetStatus("Số không hợp lệ (≥ 0)."); return; }
        string snap = Snapshot();
        if (!apply(mm / 1000)) { SetStatus("Không đổi được khoảng cách."); return; }
        PushUndo(snap);
        _p.InvalidateIndex();
        RefreshAll(false);
        SetStatus($"Đã đặt khoảng cách {what} → {d.Target.ToLowerInvariant()} {d.RefName} = {mm:0} mm.");
    }

    /// <summary>Kích thước tạm của cửa: hai đoạn trái / phải tới đầu tường hoặc cửa bên cạnh. editable = bấm số để sửa.</summary>
    private void DrawTempDims(Canvas cv, WallLine w, double width, double offset, int ignoreId, bool editable)
    {
        var d = WallAssembly.TempDims(_p, w, width, offset, ignoreId);
        double h = WallDrawThickness(w) / 2 + 0.35;
        RvDimLine(cv, w, d.LeftRef, offset - width / 2, h, $"{d.Left * 1000:0}", editable ? () => EditTempDim(true) : null);
        RvDimLine(cv, w, offset + width / 2, d.RightRef, h, $"{d.Right * 1000:0}", editable ? () => EditTempDim(false) : null);
    }

    /// <summary>Đường kích thước tạm màu xanh song song tường tại khoảng cách across (m) – chữ bấm được khi edit != null.</summary>
    private void RvDimLine(Canvas cv, WallLine w, double a, double b, double across, string text, Action edit)
    {
        double dx = w.X2 - w.X1, dy = w.Y2 - w.Y1, L = Math.Sqrt(dx * dx + dy * dy);
        if (L < 1e-9 || b - a < 1e-6) return;
        double ux = dx / L, uy = dy / L, nx = -uy, ny = ux;
        Point P(double t, double s) => W2S(w.X1 + ux * t + nx * s, w.Y1 + uy * t + ny * s);
        var pa = P(a, across); var pb = P(b, across);
        CLine(cv, pa, pb, RvBlue, 1, false);
        CLine(cv, P(a, across - 0.15), P(a, across + 0.1), RvBlue, 1, false);
        CLine(cv, P(b, across - 0.15), P(b, across + 0.1), RvBlue, 1, false);
        var tb = new TextBlock
        {
            Text = text, Foreground = RvBlue, FontSize = 11, Background = Brushes.White, Padding = new Thickness(2, 0, 2, 0),
            Cursor = edit != null ? Cursors.IBeam : null, ToolTip = edit != null ? "Bấm để nhập khoảng cách mới (mm) – như kích thước tạm của Revit" : null
        };
        if (edit != null) tb.TextDecorations = TextDecorations.Underline;
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var m = new Point((pa.X + pb.X) / 2, (pa.Y + pb.Y) / 2);
        Canvas.SetLeft(tb, m.X - tb.DesiredSize.Width / 2); Canvas.SetTop(tb, m.Y - tb.DesiredSize.Height - 1);
        if (edit != null) tb.MouseLeftButtonDown += (s, e) => { e.Handled = true; edit(); };
        else tb.IsHitTestVisible = false;
        cv.Children.Add(tb);
    }

    private void EditTempDim(bool left)
    {
        if (_rvSelOpen == null || _rvSelWall == null) return;
        var t = WallAssembly.TypeOf(_p, _rvSelOpen);
        if (t == null) return;
        var d = WallAssembly.TempDims(_p, _rvSelWall, t.Width, _rvSelOpen.Offset, _rvSelOpen.Id);
        string cur = ((left ? d.Left : d.Right) * 1000).ToString("0", Inv);
        string v = DialogUi.InputBox(this, "Kích thước tạm", $"Khoảng hở {(left ? "bên trái" : "bên phải")} của cửa {t.Name} tới {(left ? "đầu tường / cửa trái" : "cuối tường / cửa phải")} (mm):", cur);
        if (v == null) return;
        double mm = DialogUi.ParseNum(v, double.NaN);
        if (double.IsNaN(mm)) { SetStatus("Số không hợp lệ."); return; }
        var (w, o) = (_rvSelWall, _rvSelOpen);
        string snap = Snapshot();
        var r = WallAssembly.SetTempDim(_p, w, o, left, mm / 1000);
        if (!r.Ok) { SetStatus(r.Message); return; }
        AfterEdit(snap, r, w);
        SelectHostedOpening(w, o);
    }

    private void SelectHostedOpening(WallLine w, WallOpening o)
    {
        _rvSelWall = w; _rvSelOpen = o;
        if (HasSelection) ClearSelection(); else RedrawPlan();
        _rvSelWall = w; _rvSelOpen = o;
        RedrawPlan();
        RevitSelectionChanged();
        var t = WallAssembly.TypeOf(_p, o);
        SetStatus($"Đã chọn {t?.KindText.ToLowerInvariant()} {o.Type} trên tường #{w.Id}: bấm số kích thước tạm để sửa khoảng cách · Space lật hướng mở · Delete xoá · Esc bỏ chọn.");
    }

    private void ClearHostedSelection(bool redraw = true)
    {
        if (_rvSelOpen == null) return;
        _rvSelOpen = null; _rvSelWall = null;
        if (redraw) { RedrawPlan(); RevitSelectionChanged(); }
    }

    // ==================================================================
    // Công cụ đặt cửa
    // ==================================================================
    private void StartPlaceOpening(WallOpeningKind kind, string typeName = null)
    {
        if (_p == null) return;
        WallAssembly.Upgrade(_p);
        var t = _p.OpeningTypes.FirstOrDefault(q => q.Name == typeName && q.Kind == kind)
                ?? (_rvOpenTypeCombo?.SelectedItem is OpeningType ot && ot.Kind == kind ? ot : null)
                ?? _p.OpeningTypes.FirstOrDefault(q => q.Kind == kind);
        if (t == null) { SetStatus("Chưa có kiểu cửa loại này – Kiến trúc → Kiểu cửa…"); return; }
        SetPlanMode("Select");
        ClearHostedSelection();
        _rvPlaceType = t; _rvPlaceFlip = false; _rvPlaceHand = false;
        ShowOptionsFor("Opening");
        if (_rvOpenTypeCombo != null) _rvOpenTypeCombo.SelectedItem = t;
        planHost.Cursor = Cursors.Cross;
        SetStatus($"Đặt {t.KindText.ToLowerInvariant()} {t.Name}: rê chuột lên tường (tầng đang xem) → bấm để đặt · Space lật hướng mở · Shift+Space đổi bên bản lề · Esc dừng.");
    }

    private void StopPlaceOpening()
    {
        if (_rvPlaceType == null) return;
        _rvPlaceType = null;
        if (planHost != null) planHost.Cursor = null;
        planOverlay?.Children.Clear();
        ShowOptionsFor(null);
        SetStatus(ModeHint(_mode));
    }

    /// <summary>Vị trí xem trước cửa dưới con trỏ: tường + tâm hợp lệ.</summary>
    private (WallLine W, double Off)? OpeningPreviewAt(Point s)
    {
        if (_rvPlaceType == null || CurrentStory == AllStories) return null;
        var w = WallAtScreen(s, 14);
        if (w == null || w.IsCurved) return null;
        var (x, y) = S2W(s);
        double t = WallAssembly.ProjectPoint(w, x, y, out _);
        double off = WallAssembly.FreeOffset(_p, w, _rvPlaceType.Width, t);
        if (double.IsNaN(off)) return null;
        return (w, off);
    }

    /// <summary>Di chuột (gọi từ PlanHost_MouseMove). true = đã vẽ lớp phủ.</summary>
    private bool RevitPlanMove(Point s)
    {
        if (_p == null) return false;
        if (FurnishPlanMove(s)) return true;   // [Đặt đồ đạc / mặt cắt] MainWindow.Furnish.cs
        if (_rvPlaceType != null)
        {
            if (_mode != PlanEditMode.Select) { StopPlaceOpening(); return false; }
            planOverlay.Children.Clear();
            var pv = OpeningPreviewAt(s);
            if (pv is { } q)
            {
                DrawHostedOpening(planOverlay, q.W, null, _rvPlaceType, q.Off, _rvPlaceFlip, _rvPlaceHand, true, false);
                DrawTempDims(planOverlay, q.W, _rvPlaceType.Width, q.Off, -1, false);
            }
            else AddText(CurrentStory == AllStories ? "Chọn một tầng cụ thể" : "Rê chuột lên tường để đặt cửa", new Point(s.X + 14, s.Y + 12), RvBlue, 11, false, false, planOverlay);
            return true;
        }
        if (_revitOn && IsLineMode && _lineStart != null && CurrentStory != AllStories)
        {
            DrawHover();
            var (x, y) = S2W(s);
            var pe = SnapPlan(CurrentStory, x, y);
            var (len, ang) = WallAssembly.DrawDims(_lineStart.Value.X, _lineStart.Value.Y, pe.X, pe.Y);
            var tb = new TextBlock
            {
                Text = $"{len:0} mm  ∠ {ang:0.#}°", Foreground = RvBlue, Background = Brushes.White, FontSize = 12, Padding = new Thickness(3, 1, 3, 1),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(tb, s.X + 16); Canvas.SetTop(tb, s.Y + 16);
            planOverlay.Children.Add(tb);
            AddText("gõ số + Enter = chiều dài", new Point(s.X + 16, s.Y + 36), Brushes.Gray, 10, false, false, planOverlay);
            return true;
        }
        return false;
    }

    /// <summary>Bấm trên mặt bằng (gọi đầu PlanClick). true = đã xử lý.</summary>
    private bool RevitPlanClick(Point s)
    {
        if (_p == null) return false;
        if (FurnishPlanClick(s)) return true;   // [Đặt đồ đạc / chọn đồ đạc / mặt cắt] MainWindow.Furnish.cs
        if (_rvPlaceType != null)
        {
            if (_mode != PlanEditMode.Select) { StopPlaceOpening(); return false; }
            if (_busy) { SetStatus("Chương trình đang tính toán – chờ xong rồi sửa mô hình."); return true; }
            var pv = OpeningPreviewAt(s);
            if (pv is not { } q) { SetStatus("Bấm lên một đoạn tường thẳng của tầng đang xem để đặt cửa (Esc để dừng)."); return true; }
            string snap = Snapshot();
            var r = WallAssembly.PlaceOpening(_p, q.W, _rvPlaceType.Name, q.Off, _rvPlaceFlip, _rvPlaceHand);
            if (!r.Ok) { SetStatus(r.Message); return true; }
            AfterEdit(snap, r, q.W);
            SetStatus(r.Message + " Tiếp tục bấm để đặt cửa khác, Esc để dừng.");
            return true;
        }
        if (_mode == PlanEditMode.Select)
        {
            var hit = HostedOpeningAt(s);
            if (hit is { } h) { SelectHostedOpening(h.W, h.O); return true; }
            if (_rvSelOpen != null) ClearHostedSelection(false);
            return false;
        }
        // tường theo đường định vị / không nối chuỗi
        if (_revitOn && _mode == PlanEditMode.AddWall && _lineStart != null && _lineStory == CurrentStory && CurrentStory != AllStories)
        {
            var (type, hgt, open) = WallParams();
            var wt = _p.WallTypes.FirstOrDefault(t => t.Name == type);
            double off = WallAssembly.LocationOffset(wt, _rvLocation);
            if (Math.Abs(off) < 1e-9 && _rvChain) return false;   // tim tường + vẽ chuỗi: cách vẽ thường
            if (wt == null) { SetStatus("Chọn kiểu tường trước."); return true; }
            var (x, y) = S2W(s);
            var p1 = SnapPlan(CurrentStory, x, y);
            var p0 = _lineStart.Value;
            double dx = p1.X - p0.X, dy = p1.Y - p0.Y, L = Math.Sqrt(dx * dx + dy * dy);
            if (L < 1e-6) { SetStatus("Điểm cuối trùng điểm đầu – bấm điểm khác."); return true; }
            double rx = dy / L, ry = -dx / L;   // pháp tuyến bên phải hướng vẽ
            string snap = Snapshot();
            int n0 = _p.Walls.Count;
            var r = ModelEdit.AddWall(_p, CurrentStory, p0.X + rx * off, p0.Y + ry * off, p1.X + rx * off, p1.Y + ry * off, type, hgt, open);
            if (!r.Ok) { SetStatus(r.Message); return true; }
            string st = CurrentStory;
            AfterEdit(snap, r, _p.Walls.Count > n0 ? _p.Walls[^1] : null);
            _lineStart = _rvChain ? p1 : null; _lineStory = st;
            DrawHover();
            return true;
        }
        return false;
    }

    /// <summary>Phím Delete / Esc khi đang đặt / chọn cửa (gọi đầu HandlePlanEditKey).</summary>
    private bool RevitPlanKey(Key key)
    {
        if (IsTextInputFocused()) return false;
        if (FurnishPlanKey(key)) return true;   // MainWindow.Furnish.cs
        if (key == Key.Escape && _rvPlaceType != null) { StopPlaceOpening(); return true; }
        if (key == Key.Escape && _rvSelOpen != null) { ClearHostedSelection(); SetStatus(ModeHint(_mode)); return true; }
        if (key == Key.Delete && _rvSelOpen != null && _rvSelWall != null)
        {
            string snap = Snapshot();
            var w = _rvSelWall;
            var r = WallAssembly.RemoveOpening(w, _rvSelOpen);
            _rvSelOpen = null; _rvSelWall = null;
            if (!r.Ok) { SetStatus(r.Message); return true; }
            AfterEdit(snap, r, w);
            return true;
        }
        return false;
    }

    /// <summary>Space: lật hướng mở (Shift+Space: đổi bên bản lề) khi đang đặt / chọn cửa (gọi đầu CmdWindowKeyDown).</summary>
    private bool RevitSpaceKey(KeyEventArgs e)
    {
        if (e.Key != Key.Space || IsTextInputFocused()) return false;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        if (!RevitFlip(shift)) return false;
        e.Handled = true;
        return true;
    }

    /// <summary>Lật hướng mở (hand = đổi bên bản lề) của cửa đang đặt / đang chọn. false = không có cửa.</summary>
    private bool RevitFlip(bool shift)
    {
        if (!shift && FurnishSpace()) return true;   // xoay đồ đạc 90° – MainWindow.Furnish.cs
        if (_rvPlaceType != null)
        {
            if (shift) _rvPlaceHand = !_rvPlaceHand; else _rvPlaceFlip = !_rvPlaceFlip;
            if (_lastMouse != null) RevitPlanMove(_lastMouse.Value);
            return true;
        }
        if (_rvSelOpen != null && _rvSelWall != null)
        {
            var (w, o) = (_rvSelWall, _rvSelOpen);
            string snap = Snapshot();
            if (shift) o.FlipHand = !o.FlipHand; else o.Flip = !o.Flip;
            AfterEdit(snap, new EditResult { Ok = true, Message = shift ? "Đã đổi bên bản lề cửa." : "Đã lật hướng mở cửa." }, w);
            SelectHostedOpening(w, o);
            return true;
        }
        return false;
    }
}

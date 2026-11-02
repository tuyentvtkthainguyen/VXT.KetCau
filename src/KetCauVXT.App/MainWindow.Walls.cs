using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// TƯỜNG TRÊN MẶT BẰNG: vẽ (2 nét theo chiều dày, tô theo loại), chọn, thông tin, các hộp thoại
// "Loại tường…", "Tường đã khai báo…", "Cấu tạo sàn…" (menu Mô hình).
// =====================================================================
public partial class MainWindow
{
    private static readonly Brush WallHatchLight = MakeWallHatch(Color.FromRgb(244, 236, 220), Color.FromRgb(196, 176, 146));
    private static readonly Brush WallHatchMedium = MakeWallHatch(Color.FromRgb(218, 196, 164), Color.FromRgb(150, 118, 84));
    private static readonly Brush WallHatchDark = MakeWallHatch(Color.FromRgb(160, 132, 100), Color.FromRgb(92, 70, 48));
    private static readonly Brush WallStroke = Frozen(Color.FromRgb(70, 45, 25));
    private static readonly Brush WallLabelBrush = Frozen(Color.FromRgb(120, 70, 30));

    private static Brush MakeWallHatch(Color back, Color line)
    {
        var grp = new DrawingGroup();
        grp.Children.Add(new GeometryDrawing(new SolidColorBrush(back), null, new RectangleGeometry(new Rect(0, 0, 6, 6))));
        grp.Children.Add(new GeometryDrawing(null, new Pen(new SolidColorBrush(line), 0.9), Geometry.Parse("M0,6 L6,0 M-1.5,1.5 L1.5,-1.5 M4.5,7.5 L7.5,4.5")));
        var b = new DrawingBrush(grp)
        {
            TileMode = TileMode.Tile, Stretch = Stretch.None,
            Viewport = new Rect(0, 0, 6, 6), ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 6, 6), ViewboxUnits = BrushMappingMode.Absolute
        };
        b.Freeze();
        return b;
    }

    private Brush WallFill(WallType wt)
    {
        double t = wt?.Thickness ?? 0.11;
        return t < 0.16 ? WallHatchLight : t < 0.27 ? WallHatchMedium : WallHatchDark;
    }

    private IEnumerable<WallLine> PlanWalls()
    {
        if (_p?.Walls == null) return Enumerable.Empty<WallLine>();
        string st = CurrentStory;
        return st == AllStories ? _p.Walls : _p.Walls.Where(w => w.Story == st);
    }

    /// <summary>Nửa chiều dày tường trên màn hình (px), tổng tối thiểu 3 px.</summary>
    private double WallHalfPx(WallLine w)
    {
        var wt = _p.WallTypes.FirstOrDefault(t => t.Name == w.Type);
        return Math.Max(1.5, (wt?.Thickness ?? 0.11) * _scale / 2);
    }

    /// <summary>4 góc hình chữ nhật tường trên màn hình (a, b: hai đầu; hw: nửa chiều dày px).</summary>
    private static PointCollection WallQuad(Point a, Point b, double hw, out Vector u, out Vector n)
    {
        u = b - a;
        double L = u.Length;
        u = L > 1e-9 ? u / L : new Vector(1, 0);
        n = new Vector(-u.Y, u.X);
        return new PointCollection { a + n * hw, b + n * hw, b - n * hw, a - n * hw };
    }

    private void DrawWalls(bool labels)
    {
        foreach (var w in PlanWalls())
        {
            var wt = _p.WallTypes.FirstOrDefault(t => t.Name == w.Type);
            var a = W2S(w.X1, w.Y1); var b = W2S(w.X2, w.Y2);
            double hw = WallHalfPx(w);
            var pts = WallQuad(a, b, hw, out var u, out var n);
            var poly = new Shapes.Polygon
            {
                Points = pts, Fill = WallFill(wt), Opacity = 0.92, Stroke = wt == null ? Brushes.Red : WallStroke,
                StrokeThickness = 1, StrokeLineJoin = PenLineJoin.Miter, IsHitTestVisible = false
            };
            if (wt != null && wt.Finish == WallFinish.NoPlaster) poly.StrokeDashArray = new DoubleCollection { 3, 2 };
            planCanvas.Children.Add(poly);

            double lenPx = (b - a).Length;
            // ký hiệu cửa: khe cửa + cánh mở + cung quét
            if (WallMath.HasDoor(_p, w))
            {
                double g = Math.Min(0.9 * _scale, 0.45 * lenPx);
                if (g >= 6)
                {
                    var m = a + (b - a) / 2;
                    var g0 = m - u * g / 2; var g1 = m + u * g / 2;
                    planCanvas.Children.Add(new Shapes.Polygon
                    {
                        Points = new PointCollection { g0 + n * (hw + 0.5), g1 + n * (hw + 0.5), g1 - n * (hw + 0.5), g0 - n * (hw + 0.5) },
                        Fill = Brushes.White, IsHitTestVisible = false
                    });
                    CLine(planCanvas, g0 + n * hw, g0 - n * hw, WallStroke, 1, false);
                    CLine(planCanvas, g1 + n * hw, g1 - n * hw, WallStroke, 1, false);
                    // cánh cửa quay quanh g0 (mặt +n), mở ra phía +n
                    var hinge = g0 + n * hw;
                    var leaf = hinge + n * g;
                    CLine(planCanvas, hinge, leaf, WallStroke, 1.1, false);
                    var arc = new PathFigure { StartPoint = leaf, IsClosed = false };
                    arc.Segments.Add(new ArcSegment(g1 + n * hw, new Size(g, g), 0, false, SweepDirection.Counterclockwise, true));
                    // n = u quay +90° (toạ độ màn hình) → quét từ n về u là ngược chiều kim đồng hồ
                    var ag = new PathGeometry(); ag.Figures.Add(arc);
                    planCanvas.Children.Add(new Shapes.Path { Data = ag, Stroke = WallStroke, StrokeThickness = 0.7, StrokeDashArray = new DoubleCollection { 3, 2 }, IsHitTestVisible = false });
                }
            }
            if (labels && lenPx > 30)
            {
                string tag = wt != null ? "T" + (wt.Thickness * 1000).ToString("0", Inv) : "T?";
                var m = a + (b - a) / 2;
                bool vertical = Math.Abs(b.X - a.X) < Math.Abs(b.Y - a.Y);
                // ghi phía đối diện tên dầm: dưới (tường ngang) / trái (tường đứng)
                if (vertical) AddText(tag, new Point(m.X - hw - 26, m.Y + 10), WallLabelBrush, 9, false, true);
                else AddText(tag, new Point(m.X + 22, m.Y + hw + 7), WallLabelBrush, 9, true, true);
            }
        }
    }

    /// <summary>Tường gần điểm màn hình nhất trong phạm vi tol (px) cộng nửa chiều dày.</summary>
    private WallLine WallAtScreen(Point s, double tol = 8)
    {
        if (_p == null) return null;
        WallLine best = null;
        double bd = double.MaxValue;
        foreach (var w in PlanWalls())
        {
            double d = DistToSeg(s, W2S(w.X1, w.Y1), W2S(w.X2, w.Y2));
            if (d <= tol + WallHalfPx(w) && d < bd) { bd = d; best = w; }
        }
        return best;
    }

    private void DrawWallHighlight(Canvas target, WallLine w, bool hover)
    {
        var a = W2S(w.X1, w.Y1); var b = W2S(w.X2, w.Y2);
        double hw = WallHalfPx(w) + (hover ? 2 : 3);
        var pts = WallQuad(a, b, hw, out _, out _);
        target.Children.Add(new Shapes.Polygon
        {
            Points = pts, Fill = hover ? HoverFill : HiFill, Stroke = hover ? HoverBrush : HiBrush, StrokeThickness = hover ? 2 : 3,
            StrokeLineJoin = PenLineJoin.Round, Effect = hover ? null : Glow(14), IsHitTestVisible = false
        });
    }

    private string WallInfo(WallLine w)
    {
        var c = WallMath.Calc(_p, w);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Tường #{w.Id}: {w.Type}");
        sb.AppendLine($"Tầng (cao trình đứng): {w.Story}");
        sb.AppendLine($"Từ ({w.X1:0.00}; {w.Y1:0.00}) đến ({w.X2:0.00}; {w.Y2:0.00})");
        sb.AppendLine($"Chiều dài: {w.Length:0.00} m");
        if (c?.Type == null) { sb.AppendLine("LỖI: không có loại tường này – sửa ở Mô hình → Loại tường…"); return sb.ToString(); }
        var t = c.Type;
        sb.AppendLine($"Dày {t.Thickness * 1000:0} mm, γ = {t.UnitWeight:0.#} kN/m³, {WallMath.FinishText(t.Finish).ToLowerInvariant()}");
        sb.AppendLine($"Tải 1 m² tường đặc: {t.WeightPerM2:0.00} kN/m²");
        sb.AppendLine($"Chiều cao: {c.H:0.00} m{(c.AutoHeight ? " (tự động)" : "")}");
        sb.AppendLine(w.OpeningArea >= 0
            ? $"Diện tích cửa: {w.OpeningArea:0.00} m² ({c.Ratio * 100:0}%)"
            : $"Cửa theo loại tường: {t.OpeningRatio * 100:0}% ≈ {c.OpenArea:0.00} m²");
        sb.AppendLine($"Tải lên dầm: {t.WeightPerM2:0.00}·{c.H:0.00}·(1 − {c.Ratio:0.00}) = {c.Q:0.00} kN/m");
        sb.AppendLine(c.Covered >= c.Length - 0.05 ? "Đỡ bởi dầm suốt chiều dài."
            : c.Covered > 0 ? $"Trên dầm {c.Covered:0.00} m; phần còn lại tải lên sàn." : "Không có dầm đỡ – tải quy về ô sàn.");
        if (!string.IsNullOrEmpty(w.Note)) sb.AppendLine("Ghi chú: " + w.Note);
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Menu Mô hình → Loại tường… / Tường đã khai báo… / Cấu tạo sàn…
    // ------------------------------------------------------------------
    private bool CanEditModel()
    {
        if (_p == null) return false;
        if (_busy) { SetStatus("Chương trình đang tính toán – chờ xong rồi sửa mô hình."); return false; }
        return true;
    }

    /// <summary>Sau khi hộp thoại sửa dự án: lưu hoàn tác, đánh dấu thay đổi, vẽ lại.</summary>
    private void AfterDialogEdit(string snapshotBefore, string message)
    {
        PushUndo(snapshotBefore);
        bool hadResults = _res != null;
        _p.InvalidateIndex();
        MarkDirty();
        RefreshAll(false);
        SetStatus(message + (hadResults ? "  – Mô hình đã thay đổi, cần chạy lại phân tích." : ""));
    }

    private void MnuWallTypes_Click(object sender, RoutedEventArgs e)
    {
        if (!CanEditModel()) return;
        string snap = Snapshot();
        var dlg = new WallTypesWindow(_p) { Owner = this };
        dlg.ShowDialog();
        if (dlg.Changed) AfterDialogEdit(snap, $"Đã cập nhật thư viện loại tường ({_p.WallTypes.Count} loại).");
    }

    private void MnuWallList_Click(object sender, RoutedEventArgs e)
    {
        if (!CanEditModel()) return;
        string snap = Snapshot();
        var dlg = new WallListWindow(_p, CurrentStory == AllStories ? null : CurrentStory) { Owner = this };
        dlg.ShowDialog();
        if (dlg.Changed) AfterDialogEdit(snap, $"Đã cập nhật danh sách tường ({_p.Walls.Count} đoạn).");
        var w = dlg.SelectedWall;
        if (w != null && _p.Walls.Contains(w))
        {
            tabView.SelectedItem = tabView.Items.OfType<TabItem>().FirstOrDefault(t => (t.Header as string) == "Mặt bằng") ?? tabView.SelectedItem;
            if (CurrentStory != AllStories && CurrentStory != w.Story && cbStory.Items.Contains(w.Story)) cbStory.SelectedItem = w.Story;
            Select(wall: w);
        }
    }

    private void MnuBuildUps_Click(object sender, RoutedEventArgs e)
    {
        if (!CanEditModel()) return;
        string snap = Snapshot();
        var slab = _selSlab != null && _p.Slabs.Contains(_selSlab) ? _selSlab : null;
        var dlg = new FloorBuildUpWindow(_p, slab, CurrentStory == AllStories ? null : CurrentStory) { Owner = this };
        dlg.ShowDialog();
        if (!dlg.Changed) return;
        // đồng bộ tải hoàn thiện/hoạt tải của mọi ô sàn đang dùng cấu tạo (lớp, tên có thể đã đổi)
        foreach (var s in _p.Slabs.Where(s => !string.IsNullOrEmpty(s.BuildUp)).ToList())
            if (_p.FloorBuildUps.Any(b => b.Name == s.BuildUp)) ModelEdit.SetBuildUp(_p, s, s.BuildUp);
        AfterDialogEdit(snap, dlg.LastMessage ?? $"Đã cập nhật cấu tạo sàn ({_p.FloorBuildUps.Count} loại).");
        if (slab != null && _p.Slabs.Contains(slab)) Select(slab: slab);
    }
}

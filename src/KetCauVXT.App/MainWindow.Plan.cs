using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using KetCauVXT.Analysis;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>Mặt bằng tầng (Canvas): trục, cột, dầm, sàn; thu phóng bằng con lăn, kéo để di chuyển, bấm để chọn.
/// Biểu đồ M3 của cấu kiện đang chọn.</summary>
public partial class MainWindow
{
    public const string AllStories = "(Tất cả)";

    // phép biến đổi thế giới (m) → màn hình (px): sx = _ox + x·_scale ; sy = _oy − y·_scale
    private double _scale = 40, _ox = 60, _oy = 400;
    private bool _panning, _dragged;
    private Point _panStart;
    private double _panOx, _panOy;
    private bool _fitPending = true;

    private static readonly Brush GridBrush = new SolidColorBrush(Color.FromRgb(200, 60, 60));
    private static readonly Brush ColumnFill = new SolidColorBrush(Color.FromRgb(90, 90, 100));
    private static readonly Brush BeamBrush = new SolidColorBrush(Color.FromRgb(30, 90, 200));
    private static readonly Brush SteelBrush = new SolidColorBrush(Color.FromRgb(200, 120, 20));
    private static readonly Brush SlabFill = new SolidColorBrush(Color.FromArgb(50, 120, 200, 120));
    private static readonly Brush SlabFillWC = new SolidColorBrush(Color.FromArgb(80, 90, 170, 240));
    private static readonly Brush SlabFillBalcony = new SolidColorBrush(Color.FromArgb(85, 255, 165, 60));
    private static readonly Brush SlabFillCorridor = new SolidColorBrush(Color.FromArgb(70, 235, 215, 80));
    private static readonly Brush SlabFillStorage = new SolidColorBrush(Color.FromArgb(70, 170, 130, 90));
    private static readonly Brush SlabFillRoof = new SolidColorBrush(Color.FromArgb(70, 150, 140, 175));
    private static readonly Brush OpeningStroke = new SolidColorBrush(Color.FromRgb(110, 70, 40));
    private static readonly Brush OpeningBack = new SolidColorBrush(Color.FromRgb(250, 245, 235));
    private static readonly Brush StairHatch = MakeHatch();
    // tô sáng: lựa chọn (vàng cam, phát sáng) và di chuột (nhạt hơn)
    private static readonly Brush HiBrush = new SolidColorBrush(Color.FromArgb(255, 255, 176, 0));
    private static readonly Brush HiFill = new SolidColorBrush(Color.FromArgb(110, 255, 210, 60));
    private static readonly Brush HoverBrush = new SolidColorBrush(Color.FromArgb(170, 255, 200, 70));
    private static readonly Brush HoverFill = new SolidColorBrush(Color.FromArgb(55, 255, 220, 100));
    private static readonly Brush LabelBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));

    private Point W2S(double x, double y) => new(_ox + x * _scale, _oy - y * _scale);
    private (double x, double y) S2W(Point s) => ((s.X - _ox) / _scale, (_oy - s.Y) / _scale);

    private string CurrentStory => cbStory.SelectedItem as string ?? AllStories;

    /// <summary>Phần tử hiển thị trên mặt bằng tầng đang xem.</summary>
    private IEnumerable<FrameElement> PlanFrames()
    {
        if (_p == null) return Enumerable.Empty<FrameElement>();
        string st = CurrentStory;
        if (st == AllStories) return _p.Frames;
        return _p.Frames.Where(f => f.Story == st);
    }

    private IEnumerable<Slab> PlanSlabs()
    {
        if (_p == null) return Enumerable.Empty<Slab>();
        string st = CurrentStory;
        return st == AllStories ? _p.Slabs : _p.Slabs.Where(s => s.Story == st);
    }

    private IEnumerable<FloorOpening> PlanOpenings()
    {
        if (_p?.Openings == null) return Enumerable.Empty<FloorOpening>();
        string st = CurrentStory;
        return st == AllStories ? _p.Openings : _p.Openings.Where(o => o.Story == st);
    }

    private static Brush MakeHatch()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(150, 110, 70)), 0.8);
        var g = new GeometryDrawing(null, pen, Geometry.Parse("M0,8 L8,0 M-2,2 L2,-2 M6,10 L10,6"));
        var b = new DrawingBrush(g)
        {
            TileMode = TileMode.Tile, Stretch = Stretch.None,
            Viewport = new Rect(0, 0, 8, 8), ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 8, 8), ViewboxUnits = BrushMappingMode.Absolute
        };
        b.Freeze();
        return b;
    }

    private static Brush SlabBrush(SlabUse u) => u switch
    {
        SlabUse.WC => SlabFillWC, SlabUse.Balcony => SlabFillBalcony, SlabUse.Corridor => SlabFillCorridor,
        SlabUse.Storage => SlabFillStorage, SlabUse.Roof => SlabFillRoof, _ => SlabFill
    };

    private static string SlabUseTag(SlabUse u) => u switch
    {
        SlabUse.WC => "WC", SlabUse.Balcony => "BAN CÔNG", SlabUse.Corridor => "HÀNH LANG",
        SlabUse.Storage => "KHO", SlabUse.Roof => "MÁI", _ => ""
    };

    private PointCollection SlabPoints(Slab s, out double cx, out double cy)
    {
        var pts = new PointCollection();
        cx = 0; cy = 0;
        foreach (int id in s.Nodes)
        {
            var n = _p.Nodes.FirstOrDefault(q => q.Id == id);
            if (n == null) continue;
            pts.Add(W2S(n.X, n.Y)); cx += n.X; cy += n.Y;
        }
        if (pts.Count > 0) { cx /= pts.Count; cy /= pts.Count; }
        return pts;
    }

    private void FitPlan()
    {
        if (_p == null) return;
        double w = planHost.ActualWidth, h = planHost.ActualHeight;
        if (w < 10 || h < 10) { _fitPending = true; return; }
        var xs = new List<double>(); var ys = new List<double>();
        foreach (var n in _p.Nodes) { xs.Add(n.X); ys.Add(n.Y); }
        foreach (var g in _p.Grids) { if (g.Dir == GridDir.X) xs.Add(g.Coord); else ys.Add(g.Coord); }
        if (xs.Count == 0) xs.AddRange(new[] { 0.0, 10.0 });
        if (ys.Count == 0) ys.AddRange(new[] { 0.0, 10.0 });
        double x0 = xs.Min(), x1 = xs.Max(), y0 = ys.Min(), y1 = ys.Max();
        double dx = Math.Max(x1 - x0, 1), dy = Math.Max(y1 - y0, 1);
        const double margin = 70;
        _scale = Math.Max(1, Math.Min((w - 2 * margin) / dx, (h - 2 * margin) / dy));
        _ox = (w - dx * _scale) / 2 - x0 * _scale;
        _oy = h - (h - dy * _scale) / 2 + y0 * _scale;
        _fitPending = false;
        RedrawPlan();
    }

    private void RedrawPlan()
    {
        planCanvas.Children.Clear();
        planOverlay.Children.Clear();
        UpdateUndoButton();
        if (_p == null) return;
        if (_fitPending) { FitPlan(); if (_fitPending) return; }
        RefreshEditCombos();
        bool labels = chkLabels.IsChecked == true;
        double w = planHost.ActualWidth, h = planHost.ActualHeight;

        // ---- sàn ----
        foreach (var s in PlanSlabs())
        {
            var pts = SlabPoints(s, out double cx, out double cy);
            if (pts.Count < 3) continue;
            var use = ModelEdit.UseOf(_p, s);
            planCanvas.Children.Add(new Shapes.Polygon { Points = pts, Fill = SlabBrush(use), Stroke = Brushes.Transparent });
            var c = W2S(cx, cy);
            string tag = SlabUseTag(use);
            if (labels) AddText(s.Label, tag.Length > 0 ? new Point(c.X, c.Y - 7) : c, Brushes.DarkGreen, 10, true);
            if (tag.Length > 0)
                AddText(tag, labels ? new Point(c.X, c.Y + 7) : c, use == SlabUse.WC ? Brushes.SteelBlue : Brushes.SaddleBrown, 10, true);
        }

        // ---- lỗ sàn: cầu thang, giếng trời ----
        foreach (var o in PlanOpenings()) DrawOpening(o);

        // ---- trục ----
        double bub = 11;
        var gxs = _p.GridsX.ToList(); var gys = _p.GridsY.ToList();
        double gyMin = gys.Count > 0 ? gys.First().Coord : 0, gyMax = gys.Count > 0 ? gys.Last().Coord : 10;
        double gxMin = gxs.Count > 0 ? gxs.First().Coord : 0, gxMax = gxs.Count > 0 ? gxs.Last().Coord : 10;
        double ext = 1.2;
        foreach (var g in gxs)
        {
            var a = W2S(g.Coord, gyMin - ext); var b = W2S(g.Coord, gyMax + ext);
            AddLine(a, b, GridBrush, 0.8, true);
            AddBubble(g.Name, new Point(a.X, a.Y + bub), bub);
            AddBubble(g.Name, new Point(b.X, b.Y - bub), bub);
        }
        foreach (var g in gys)
        {
            var a = W2S(gxMin - ext, g.Coord); var b = W2S(gxMax + ext, g.Coord);
            AddLine(a, b, GridBrush, 0.8, true);
            AddBubble(g.Name, new Point(a.X - bub, a.Y), bub);
            AddBubble(g.Name, new Point(b.X + bub, b.Y), bub);
        }
        // kích thước bước trục
        for (int i = 0; i + 1 < gxs.Count; i++)
        {
            var a = W2S((gxs[i].Coord + gxs[i + 1].Coord) / 2, gyMin - ext);
            AddText($"{(gxs[i + 1].Coord - gxs[i].Coord) * 1000:0}", new Point(a.X, a.Y + 2 * bub + 10), GridBrush, 10, true);
        }
        for (int i = 0; i + 1 < gys.Count; i++)
        {
            var a = W2S(gxMin - ext, (gys[i].Coord + gys[i + 1].Coord) / 2);
            AddText($"{(gys[i + 1].Coord - gys[i].Coord) * 1000:0}", new Point(a.X - 2 * bub - 22, a.Y), GridBrush, 10, true);
        }

        // ---- dầm / kèo ----
        foreach (var f in PlanFrames().Where(q => !q.IsVerticalKind))
        {
            var ni = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeI); var nj = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
            if (ni == null || nj == null) continue;
            var a = W2S(ni.X, ni.Y); var b = W2S(nj.X, nj.Y);
            double width = BeamPlanWidth(f);
            var arcB = SafeArc(f);
            if (arcB != null) { DrawCurvedBeamPlan(f, arcB, f.IsConcrete ? BeamBrush : SteelBrush, width, labels); continue; } // dầm cong (MainWindow.CurvedBeam.cs)
            AddLine(a, b, f.IsConcrete ? BeamBrush : SteelBrush, width, false);
            if (labels)
            {
                var m = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                bool vertical = Math.Abs(b.X - a.X) < Math.Abs(b.Y - a.Y);
                AddText($"{f.Label} {f.Section}", vertical ? new Point(m.X + 6, m.Y) : new Point(m.X, m.Y - 12), LabelBrush, 9, !vertical);
            }
        }

        // ---- tường (MainWindow.Walls.cs) ----
        DrawWalls(labels);

        // ---- cột ----
        foreach (var f in PlanFrames().Where(q => q.IsVerticalKind))
        {
            var n = _p.Nodes.FirstOrDefault(q => q.Id == f.NodeJ);
            if (n == null) continue;
            var (dx, dy) = ColumnPlanSize(f);
            var c = W2S(n.X, n.Y);
            double rw = Math.Max(4, dx * _scale), rh = Math.Max(4, dy * _scale);
            var r = new Shapes.Rectangle
            {
                Width = rw, Height = rh, Fill = f.IsConcrete ? ColumnFill : SteelBrush,
                Stroke = Brushes.Black, StrokeThickness = 0.8
            };
            Canvas.SetLeft(r, c.X - rw / 2); Canvas.SetTop(r, c.Y - rh / 2);
            planCanvas.Children.Add(r);
            if (labels) AddText($"{f.Label}", new Point(c.X + rw / 2 + 2, c.Y + rh / 2 + 1), Brushes.Black, 9, false);
        }

        // ---- gối ----
        if (CurrentStory == AllStories || _p.Stories.OrderBy(s => s.Elevation).FirstOrDefault()?.Name == CurrentStory)
            foreach (var n in _p.Nodes.Where(q => q.IsSupport))
            {
                var c = W2S(n.X, n.Y);
                var tri = new Shapes.Polygon
                {
                    Points = new PointCollection { new Point(c.X, c.Y + 3), new Point(c.X - 5, c.Y + 11), new Point(c.X + 5, c.Y + 11) },
                    Fill = Brushes.ForestGreen
                };
                planCanvas.Children.Add(tri);
            }

        // ---- tô sáng đối tượng đang chọn (vẽ trên cùng) ----
        DrawSelectionHighlight();

        // tiêu đề
        AddText($"Mặt bằng: {CurrentStory}   –   tỷ lệ {_scale:0.#} px/m", new Point(8, 6), Brushes.Gray, 11, false);

        // di chuột: vẽ lại lớp phủ
        DrawHover();
    }

    private double BeamPlanWidth(FrameElement f)
    {
        var sec = _p.FrameSections.FirstOrDefault(s => s.Name == f.Section);
        return sec != null && sec.Shape == SectionShape.RectConcrete ? Math.Max(1.5, Math.Min(sec.B * _scale, 14)) : 2.5;
    }

    /// <summary>Vẽ ô cầu thang (gạch chéo + mũi tên lên + chữ) hoặc giếng trời (dấu X + chữ).</summary>
    private void DrawOpening(FloorOpening o)
    {
        var a = W2S(o.X0, o.Y1); var b = W2S(o.X1, o.Y0); // góc trên trái, dưới phải (màn hình)
        double w = Math.Max(2, b.X - a.X), h = Math.Max(2, b.Y - a.Y);
        var back = new Shapes.Rectangle { Width = w, Height = h, Fill = OpeningBack, Stroke = OpeningStroke, StrokeThickness = 1.2 };
        Canvas.SetLeft(back, a.X); Canvas.SetTop(back, a.Y);
        planCanvas.Children.Add(back);
        var c = new Point(a.X + w / 2, a.Y + h / 2);
        if (o.Kind == OpeningKind.Stair)
        {
            var hatch = new Shapes.Rectangle { Width = w, Height = h, Fill = StairHatch, IsHitTestVisible = false };
            Canvas.SetLeft(hatch, a.X); Canvas.SetTop(hatch, a.Y);
            planCanvas.Children.Add(hatch);
            // mũi tên hướng lên theo cạnh dài
            bool alongX = w >= h;
            Point p0 = alongX ? new Point(a.X + 0.12 * w, c.Y) : new Point(c.X, a.Y + 0.88 * h);
            Point p1 = alongX ? new Point(a.X + 0.88 * w, c.Y) : new Point(c.X, a.Y + 0.12 * h);
            AddLine(p0, p1, OpeningStroke, 1.4, false);
            var dot = new Shapes.Ellipse { Width = 6, Height = 6, Fill = OpeningStroke };
            Canvas.SetLeft(dot, p0.X - 3); Canvas.SetTop(dot, p0.Y - 3);
            planCanvas.Children.Add(dot);
            double ux = p1.X - p0.X, uy = p1.Y - p0.Y, L = Math.Sqrt(ux * ux + uy * uy);
            if (L > 1)
            {
                ux /= L; uy /= L;
                double k = Math.Min(10, L / 3);
                planCanvas.Children.Add(new Shapes.Polygon
                {
                    Points = new PointCollection { p1, new Point(p1.X - k * ux - k * 0.5 * uy, p1.Y - k * uy + k * 0.5 * ux), new Point(p1.X - k * ux + k * 0.5 * uy, p1.Y - k * uy - k * 0.5 * ux) },
                    Fill = OpeningStroke
                });
            }
            AddTagText("CẦU THANG", alongX ? new Point(c.X, c.Y - 12) : new Point(c.X + 4, c.Y), alongX);
        }
        else
        {
            AddLine(a, b, OpeningStroke, 1, false);
            AddLine(new Point(b.X, a.Y), new Point(a.X, b.Y), OpeningStroke, 1, false);
            AddTagText("GIẾNG TRỜI", c, true);
        }
    }

    private void AddTagText(string s, Point p, bool center)
    {
        var t = new TextBlock
        {
            Text = s, Foreground = OpeningStroke, FontSize = 10, FontWeight = FontWeights.Bold, IsHitTestVisible = false,
            Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), Padding = new Thickness(2, 0, 2, 0)
        };
        t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(t, center ? p.X - t.DesiredSize.Width / 2 : p.X);
        Canvas.SetTop(t, p.Y - t.DesiredSize.Height / 2);
        planCanvas.Children.Add(t);
    }

    // ------------------------------------------------------------------
    // Tô sáng (lựa chọn + di chuột)
    // ------------------------------------------------------------------
    private object _hover;

    private static Effect Glow(double blur) => new DropShadowEffect { Color = Colors.Yellow, BlurRadius = blur, ShadowDepth = 0, Opacity = 1 };

    private void DrawSelectionHighlight()
    {
        if (_p == null) return;
        if (_selWall != null && _p.Walls.Contains(_selWall)) DrawHighlight(planCanvas, _selWall, false);
        else if (_selOpening != null && _p.Openings.Contains(_selOpening)) DrawHighlight(planCanvas, _selOpening, false);
        else if (_selSlab != null && _p.Slabs.Contains(_selSlab)) DrawHighlight(planCanvas, _selSlab, false);
        else if (_sel != null) DrawHighlight(planCanvas, _p.Frames.FirstOrDefault(f => f.Id == _sel.Id), false);
        else if (_selFooting != 0)
        {
            var fn = _p.Nodes.FirstOrDefault(n => n.Id == _selFooting);
            if (fn != null)
            {
                var col = PlanFrames().Where(f => f.IsVerticalKind).FirstOrDefault(f => f.NodeI == fn.Id || f.NodeJ == fn.Id)
                          ?? PlanFrames().Where(f => f.IsVerticalKind).FirstOrDefault(f =>
                          {
                              var n = _p.Nodes.FirstOrDefault(q => q.Id == f.NodeJ);
                              return n != null && Math.Abs(n.X - fn.X) < 1e-3 && Math.Abs(n.Y - fn.Y) < 1e-3;
                          });
                if (col != null) DrawHighlight(planCanvas, col, false);
                else
                {
                    var c = W2S(fn.X, fn.Y);
                    var e = new Shapes.Ellipse { Width = 16, Height = 16, Stroke = HiBrush, StrokeThickness = 3, Effect = Glow(14), IsHitTestVisible = false };
                    Canvas.SetLeft(e, c.X - 8); Canvas.SetTop(e, c.Y - 8);
                    planCanvas.Children.Add(e);
                }
            }
        }
    }

    private void DrawHover()
    {
        planOverlay.Children.Clear();
        if (_p != null && !_panning && IsLineMode) { DrawLineToolOverlay(); return; }
        if (_p == null || _hover == null || _panning) return;
        if (ReferenceEquals(_hover, _selWall)) return;
        if (ReferenceEquals(_hover, _selSlab) || ReferenceEquals(_hover, _selOpening) || (_hover is FrameElement hf && _sel != null && hf.Id == _sel.Id)) return;
        DrawHighlight(planOverlay, _hover, true);
    }

    /// <summary>Vẽ đường viền tô sáng của một đối tượng mặt bằng lên canvas đích.</summary>
    private void DrawHighlight(Canvas target, object obj, bool hover)
    {
        if (obj == null) return;
        Brush stroke = hover ? HoverBrush : HiBrush, fill = hover ? HoverFill : HiFill;
        Effect fx = hover ? null : Glow(14);
        switch (obj)
        {
            case FrameElement f when !f.IsVerticalKind:
            {
                var ni = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeI); var nj = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
                if (ni == null || nj == null) return;
                if (f.IsCurved && SafeArc(f) != null)
                {
                    target.Children.Add(new Shapes.Polyline
                    {
                        Points = BeamScreenPoints(f), Stroke = stroke, StrokeThickness = BeamPlanWidth(f) + (hover ? 3 : 5),
                        StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Effect = fx, IsHitTestVisible = false
                    });
                    break;
                }
                var a = W2S(ni.X, ni.Y); var b = W2S(nj.X, nj.Y);
                target.Children.Add(new Shapes.Line
                {
                    X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = stroke, StrokeThickness = BeamPlanWidth(f) + (hover ? 3 : 5),
                    StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Effect = fx, IsHitTestVisible = false
                });
                break;
            }
            case FrameElement f:
            {
                var n = _p.Nodes.FirstOrDefault(q => q.Id == f.NodeJ);
                if (n == null) return;
                var (dx, dy) = ColumnPlanSize(f);
                var c = W2S(n.X, n.Y);
                double rw = Math.Max(4, dx * _scale) + (hover ? 4 : 6), rh = Math.Max(4, dy * _scale) + (hover ? 4 : 6);
                var r = new Shapes.Rectangle { Width = rw, Height = rh, Fill = fill, Stroke = stroke, StrokeThickness = hover ? 2 : 3, Effect = fx, IsHitTestVisible = false };
                Canvas.SetLeft(r, c.X - rw / 2); Canvas.SetTop(r, c.Y - rh / 2);
                target.Children.Add(r);
                break;
            }
            case Slab s:
            {
                var pts = SlabPoints(s, out _, out _);
                if (pts.Count < 3) return;
                target.Children.Add(new Shapes.Polygon { Points = pts, Fill = fill, Stroke = stroke, StrokeThickness = hover ? 2 : 3.5, StrokeLineJoin = PenLineJoin.Round, Effect = fx, IsHitTestVisible = false });
                break;
            }
            case FloorOpening o:
            {
                var a = W2S(o.X0, o.Y1); var b = W2S(o.X1, o.Y0);
                var r = new Shapes.Rectangle { Width = Math.Max(2, b.X - a.X), Height = Math.Max(2, b.Y - a.Y), Fill = fill, Stroke = stroke, StrokeThickness = hover ? 2 : 3.5, Effect = fx, IsHitTestVisible = false };
                Canvas.SetLeft(r, a.X); Canvas.SetTop(r, a.Y);
                target.Children.Add(r);
                break;
            }
            case WallLine w:
                DrawWallHighlight(target, w, hover);
                break;
        }
    }

    /// <summary>Kích thước tiết diện cột trên mặt bằng (theo X, theo Y), m.
    /// Góc 0: trục 2 (cạnh h) song song X; góc 90: cạnh h song song Y.</summary>
    private (double dx, double dy) ColumnPlanSize(FrameElement f)
    {
        var sec = _p.FrameSections.FirstOrDefault(s => s.Name == f.Section);
        double b = 0.22, h = 0.22;
        if (sec != null)
        {
            var sp = sec.Props(0.5);
            b = Math.Max(sp.Width, 0.05); h = Math.Max(sp.Depth, 0.05);
        }
        double a = ((f.Angle % 180) + 180) % 180;
        return Math.Abs(a - 90) < 45 ? (b, h) : (h, b);
    }

    private void AddLine(Point a, Point b, Brush br, double th, bool dash)
    {
        var l = new Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = br, StrokeThickness = th };
        if (dash) l.StrokeDashArray = new DoubleCollection { 12, 3, 2, 3 };
        planCanvas.Children.Add(l);
    }

    private void AddBubble(string name, Point c, double r)
    {
        var e = new Shapes.Ellipse { Width = 2 * r, Height = 2 * r, Stroke = GridBrush, StrokeThickness = 1, Fill = Brushes.White };
        Canvas.SetLeft(e, c.X - r); Canvas.SetTop(e, c.Y - r);
        planCanvas.Children.Add(e);
        AddText(name, c, GridBrush, 11, true, true);
    }

    private void AddText(string s, Point p, Brush br, double size, bool center, bool vcenter = false, Canvas target = null)
    {
        var t = new TextBlock { Text = s, Foreground = br, FontSize = size, IsHitTestVisible = false };
        t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double x = center ? p.X - t.DesiredSize.Width / 2 : p.X;
        double y = vcenter || center ? p.Y - t.DesiredSize.Height / 2 : p.Y;
        Canvas.SetLeft(t, x); Canvas.SetTop(t, y);
        (target ?? planCanvas).Children.Add(t);
    }

    // ------------------------------------------------------------------
    // Chuột
    // ------------------------------------------------------------------
    private void PlanHost_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var m = e.GetPosition(planHost);
        var (wx, wy) = S2W(m);
        double k = e.Delta > 0 ? 1.2 : 1 / 1.2;
        _scale = Math.Clamp(_scale * k, 0.5, 5000);
        _ox = m.X - wx * _scale;
        _oy = m.Y + wy * _scale;
        RedrawPlan();
        e.Handled = true;
    }

    private void PlanHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        planHost.Focus(); // để Delete / Esc / Ctrl+Z tác động lên mặt bằng
        _panning = true; _dragged = false;
        _panStart = e.GetPosition(planHost);
        _panOx = _ox; _panOy = _oy;
        planHost.CaptureMouse();
    }

    private void PlanHost_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_panning)
        {
            _lastMouse = e.GetPosition(planHost);
            if (IsLineMode) { DrawHover(); return; }   // dây chun + điểm bắt (MainWindow.Edit.cs)
            UpdateHover(_lastMouse.Value);
            return;
        }
        var p = e.GetPosition(planHost);
        double dx = p.X - _panStart.X, dy = p.Y - _panStart.Y;
        if (!_dragged && Math.Abs(dx) + Math.Abs(dy) < 4) return;
        _dragged = true;
        _ox = _panOx + dx; _oy = _panOy + dy;
        RedrawPlan();
    }

    private void PlanHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_panning) return;
        _panning = false;
        planHost.ReleaseMouseCapture();
        if (!_dragged) PlanClick(e.GetPosition(planHost));
    }

    private void PlanHost_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_panning) return;
        if (_lastMouse != null) { _lastMouse = null; if (IsLineMode) DrawHover(); }
        if (_hover == null) return;
        _hover = null;
        DrawHover();
    }

    /// <summary>Cập nhật tô sáng khi di chuột (chỉ vẽ lại lớp phủ, không vẽ lại cả mặt bằng).</summary>
    private void UpdateHover(Point s)
    {
        if (_p == null) return;
        object h = HoverTarget(s);
        if (ReferenceEquals(h, _hover)) return;
        _hover = h;
        DrawHover();
    }

    private void PlanHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_fitPending) FitPlan(); else RedrawPlan();
    }

    private void BtnFit_Click(object sender, RoutedEventArgs e) => FitPlan();

    private void ChkLabels_Changed(object sender, RoutedEventArgs e) { if (IsLoaded) RedrawPlan(); }

    /// <summary>Chế độ "Chọn": ưu tiên cột > dầm (≤ 8 px) > tường > lỗ sàn > ô sàn; chỗ trống → bỏ chọn.
    /// Bấm lại vào dầm đang chọn mà có tường trên dầm → chọn tường (bấm tiếp → lại chọn dầm).</summary>
    private void SelectAt(Point s)
    {
        if (_p == null) return;
        var hit = HitTest(s);
        if (hit is FrameElement hb && !hb.IsVerticalKind && _sel != null && _sel.Id == hb.Id)
        {
            var w = WallAtScreen(s);
            if (w != null) hit = w;
        }
        switch (hit)
        {
            case WallLine w: Select(wall: w); break;
            case FrameElement f: Select(frame: f); break;
            case FloorOpening o: Select(opening: o); break;
            case Slab sl: Select(slab: sl); break;
            default: ClearSelection(); break;
        }
    }

    /// <summary>Đối tượng tại điểm màn hình theo thứ tự ưu tiên cột > dầm > tường > lỗ sàn > ô sàn.</summary>
    private object HitTest(Point s)
    {
        if (_p == null) return null;
        var col = ColumnAt(s);
        if (col != null) return col;
        var beam = NearestBeam(s, 8);
        if (beam != null) return beam;
        var wall = WallAtScreen(s);
        if (wall != null) return wall;
        var (x, y) = S2W(s);
        var op = OpeningAt(x, y);
        if (op != null) return op;
        return SlabAtPlan(x, y);
    }

    private FrameElement ColumnAt(Point s)
    {
        foreach (var f in PlanFrames().Where(q => q.IsVerticalKind))
        {
            var n = _p.Nodes.FirstOrDefault(q => q.Id == f.NodeJ);
            if (n == null) continue;
            var (dx, dy) = ColumnPlanSize(f);
            var c = W2S(n.X, n.Y);
            double hw = Math.Max(4, dx * _scale) / 2 + 3, hh = Math.Max(4, dy * _scale) / 2 + 3;
            if (Math.Abs(s.X - c.X) <= hw && Math.Abs(s.Y - c.Y) <= hh) return f;
        }
        return null;
    }

    /// <summary>Dầm gần điểm bấm nhất trong phạm vi tol (px).</summary>
    private FrameElement NearestBeam(Point s, double tol)
    {
        FrameElement best = null;
        double bd = tol;
        foreach (var f in PlanFrames().Where(q => !q.IsVerticalKind))
        {
            var ni = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeI); var nj = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
            if (ni == null || nj == null) continue;
            double d = f.IsCurved && SafeArc(f) != null ? DistToPolyline(s, BeamScreenPoints(f)) : DistToSeg(s, W2S(ni.X, ni.Y), W2S(nj.X, nj.Y));
            if (d <= bd) { bd = d; best = f; }
        }
        return best;
    }

    private FloorOpening OpeningAt(double x, double y) => PlanOpenings().LastOrDefault(o => o.Contains(x, y));

    /// <summary>Ô sàn chứa điểm (x, y) trong các ô sàn đang hiển thị.</summary>
    private Slab SlabAtPlan(double x, double y)
    {
        foreach (var sl in PlanSlabs())
        {
            var pts = sl.Nodes.Select(id => _p.Nodes.FirstOrDefault(q => q.Id == id)).Where(n => n != null).ToList();
            if (pts.Count < 3) continue;
            bool c = false;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
            {
                var a = pts[i]; var b = pts[j];
                if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X) c = !c;
            }
            if (c) return sl;
        }
        return null;
    }

    private static double DistToSeg(Point p, Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, L2 = dx * dx + dy * dy;
        double t = L2 < 1e-9 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / L2, 0, 1);
        double qx = a.X + t * dx - p.X, qy = a.Y + t * dy - p.Y;
        return Math.Sqrt(qx * qx + qy * qy);
    }

    // ------------------------------------------------------------------
    // Thông tin + biểu đồ M3
    // ------------------------------------------------------------------
    private void ShowSelection()
    {
        if (_p != null && _selWall != null && _p.Walls.Contains(_selWall)) { txtInfo.Text = WallInfo(_selWall); DrawDiagram(); return; }
        if (_p != null && _selOpening != null && _p.Openings.Contains(_selOpening)) { txtInfo.Text = OpeningInfo(_selOpening); DrawDiagram(); return; }
        if (_p != null && _selSlab != null && _p.Slabs.Contains(_selSlab)) { txtInfo.Text = SlabInfo(_selSlab); DrawDiagram(); return; }
        if (_p != null && _sel == null && _selFooting != 0) { txtInfo.Text = FootingInfo(_selFooting); DrawDiagram(); return; }
        if (_sel == null || _p == null)
        {
            txtInfo.Text = "Bấm chọn cột, dầm, tường, ô sàn hoặc lỗ sàn trên mặt bằng để xem thông tin.";
            DrawDiagram();
            return;
        }
        var f = _sel;
        var ni = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeI); var nj = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
        double L = ni != null && nj != null ? (f.IsCurved && SafeArc(f) != null ? SafeArc(f).Length : (nj.P - ni.P).Length) : 0;
        string kind = f.Kind switch
        {
            FrameKind.Column => "Cột BTCT", FrameKind.Beam => "Dầm BTCT", FrameKind.SteelColumn => "Cột thép",
            FrameKind.Rafter => "Kèo thép", FrameKind.Purlin => "Xà gồ", _ => "Giằng"
        };
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{kind}: {f.Label}  (thanh {f.Id})");
        sb.AppendLine($"Tiết diện: {f.Section}   góc: {f.Angle:0.#}°");
        sb.AppendLine($"Tầng: {f.Story}   nhóm: {f.DesignGroup}");
        sb.AppendLine($"Nút I: {f.NodeI}  {ni?.P}");
        sb.AppendLine($"Nút J: {f.NodeJ}  {nj?.P}");
        sb.AppendLine($"Chiều dài: {L:0.000} m");
        if (f.IsCurved) sb.Append(CurvedInfo(f));
        int nl = _p.FrameLoads.Count(l => l.Element == f.Id);
        if (nl > 0) sb.AppendLine($"Số tải gán trên thanh: {nl}");
        if (_res != null)
        {
            string c = cbDiagramCase.SelectedItem as string;
            var ff = ForcesOf(f.Id, c, out var mn);
            if (ff != null && ff.Count > 0 && (mn == null || mn.Count > 0))
            {
                sb.AppendLine($"--- {c} ---");
                double mmax = Enumerable.Range(0, ff.Count).Max(i => ff.M3(i));
                double mmin = Enumerable.Range(0, (mn ?? ff).Count).Min(i => (mn ?? ff).M3(i));
                double vmax = Enumerable.Range(0, ff.Count).Max(i => Math.Abs(ff.V2(i)));
                if (mn != null) vmax = Math.Max(vmax, Enumerable.Range(0, mn.Count).Max(i => Math.Abs(mn.V2(i))));
                double pmax = Enumerable.Range(0, ff.Count).Max(i => ff.P(i));
                double pmin = Enumerable.Range(0, (mn ?? ff).Count).Min(i => (mn ?? ff).P(i));
                sb.AppendLine($"M3 max = {mmax:0.00}  min = {mmin:0.00} kN.m");
                sb.AppendLine($"|V2| max = {vmax:0.00} kN");
                sb.AppendLine($"P max = {pmax:0.00}  min = {pmin:0.00} kN");
                if (f.IsCurved)
                {
                    double tmax = Enumerable.Range(0, ff.Count).Max(i => Math.Abs(ff.T(i)));
                    if (mn != null) tmax = Math.Max(tmax, Enumerable.Range(0, mn.Count).Max(i => Math.Abs(mn.T(i))));
                    sb.AppendLine($"|T| max = {tmax:0.00} kN.m (xoắn dầm cong)");
                }
            }
        }
        txtInfo.Text = sb.ToString();
        DrawDiagram();
    }

    private string SlabInfo(Slab s)
    {
        var sb = new System.Text.StringBuilder();
        var pts = s.Nodes.Select(id => _p.Nodes.FirstOrDefault(n => n.Id == id)).Where(n => n != null).ToList();
        var use = ModelEdit.UseOf(_p, s);
        sb.AppendLine($"Ô sàn: {s.Label}  (sàn {s.Id})");
        sb.AppendLine($"Tầng: {s.Story}");
        if (pts.Count >= 3)
        {
            double x0 = pts.Min(n => n.X), x1 = pts.Max(n => n.X), y0 = pts.Min(n => n.Y), y1 = pts.Max(n => n.Y);
            sb.AppendLine($"Kích thước: {x1 - x0:0.00} × {y1 - y0:0.00} m  ({(x1 - x0) * (y1 - y0):0.00} m²)");
            sb.AppendLine($"Phạm vi: X {x0:0.00}…{x1:0.00}, Y {y0:0.00}…{y1:0.00}");
        }
        var sec = _p.SlabSections.FirstOrDefault(q => q.Name == s.Section);
        sb.AppendLine(sec != null ? $"Tiết diện: {s.Section}  dày {sec.Thickness * 1000:0} mm, {sec.Material}" : $"Tiết diện: {s.Section}");
        sb.AppendLine($"Công năng: {ModelEdit.Loads(use).Name}");
        if (!string.IsNullOrEmpty(s.BuildUp))
        {
            var bu = _p.FloorBuildUps.FirstOrDefault(b => b.Name == s.BuildUp);
            sb.AppendLine(bu != null ? $"Cấu tạo sàn: {bu.Name} ({bu.Layers.Count} lớp, {bu.Finish:0.00} kN/m²)" : $"Cấu tạo sàn: {s.BuildUp} (KHÔNG CÓ trong thư viện)");
        }
        double fin = _p.AreaLoads.Where(a => a.Slab == s.Id && a.Pattern == "HTHT").Sum(a => a.Q);
        double live = _p.AreaLoads.Where(a => a.Slab == s.Id && a.Pattern == "HT").Sum(a => a.Q);
        sb.AppendLine($"Hoàn thiện (HTHT): {fin:0.##} kN/m²");
        sb.AppendLine($"Hoạt tải (HT): {live:0.##} kN/m²");
        foreach (var a in _p.AreaLoads.Where(a => a.Slab == s.Id && a.Pattern != "HTHT" && a.Pattern != "HT"))
            sb.AppendLine($"Tải {a.Pattern}: {a.Q:0.##} kN/m²{(string.IsNullOrEmpty(a.Note) ? "" : " – " + a.Note)}");
        return sb.ToString();
    }

    private string OpeningInfo(FloorOpening o)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{o.Name}  (lỗ sàn {o.Id})");
        sb.AppendLine($"Tầng: {o.Story}");
        sb.AppendLine($"Kích thước: {o.X1 - o.X0:0.00} × {o.Y1 - o.Y0:0.00} m  ({(o.X1 - o.X0) * (o.Y1 - o.Y0):0.00} m²)");
        sb.AppendLine($"Phạm vi: X {o.X0:0.00}…{o.X1:0.00}, Y {o.Y0:0.00}…{o.Y1:0.00}");
        int nl = _p.FrameLoads.Count(l => l.Source == "OPEN" + o.Id);
        if (nl > 0) sb.AppendLine($"Tải viền gán vào dầm: {nl}");
        if (!string.IsNullOrEmpty(o.Note)) sb.AppendLine($"Ghi chú: {o.Note}");
        if (o.Kind == OpeningKind.Stair)   // cầu thang khai báo (MainWindow.Stair.cs)
        {
            var sd = StairEdit.ForOpening(_p, o);
            sb.AppendLine(sd != null ? $"Cầu thang {sd.Name}: {Stair.TypeName(sd.Type)}, bậc {Stair.StepsName(sd.Steps).ToLowerInvariant()}, ốp {Stair.FinishName(sd.Finish).ToLowerInvariant()}"
                                     : "Chưa khai báo cầu thang – bấm \"Khai báo cầu thang…\".");
        }
        return sb.ToString();
    }

    private string FootingInfo(int nodeId)
    {
        var n = _p.Nodes.FirstOrDefault(q => q.Id == nodeId);
        var ft = _p.Footings.FirstOrDefault(f => f.NodeId == nodeId);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Móng {(ft != null ? ft.Label : "")} tại nút {nodeId}  {n?.P}");
        var type = ft?.Type ?? _p.Foundation.Type;
        sb.AppendLine($"Loại móng: {FoundationTypeName(type)}{(ft?.Type != null ? " (riêng chân cột này)" : " (theo công trình)")}");
        if (ft != null && ft.L > 0) sb.AppendLine($"Đáy móng: {ft.L:0.00} × {ft.B:0.00} m, cao {ft.Height:0.00} m");
        var pc = _design?.PileCaps?.FirstOrDefault(c => c.NodeIds.Contains(nodeId));
        if (pc != null)
        {
            sb.AppendLine($"Đài {pc.Name}: {pc.L:0.00}×{pc.B:0.00}×{pc.H:0.00} m, {pc.Piles.Count} cọc {PileText(pc.PileRound, pc.PileSize)}");
            sb.AppendLine($"Pmax = {pc.PMax:0.0}, Pmin = {pc.PMin:0.0} kN; [P] = {pc.PileCapacity:0} kN");
            sb.AppendLine(pc.Ok ? "Kết quả: ĐẠT" : "Kết quả: KHÔNG ĐẠT");
        }
        var sf = _design?.StripFootings?.FirstOrDefault(c => c.NodeIds.Contains(nodeId));
        if (sf != null)
        {
            sb.AppendLine($"Móng băng {sf.Name} ({(sf.AlongX ? "dọc X" : "dọc Y")}): B = {sf.Width:0.00} m, sườn {sf.RibB * 1000:0}×{sf.RibH * 1000:0}");
            sb.AppendLine($"Pmax = {sf.PMax:0.0} kN/m², R = {sf.R:0.0} kN/m²");
            sb.AppendLine(sf.Ok ? "Kết quả: ĐẠT" : "Kết quả: KHÔNG ĐẠT");
        }
        sb.Append(FoundationSafetySummary(nodeId));   // [FoundationChecks] MainWindow.PileLayout.cs
        return sb.ToString();
    }

    /// <summary>Nội lực thanh theo trường hợp/tổ hợp; với tổ hợp bao trả về (max, min).</summary>
    private FrameForces ForcesOf(int frameId, string caseName, out FrameForces min)
    {
        min = null;
        if (_res == null || string.IsNullOrEmpty(caseName)) return null;
        try
        {
            if (_res.IsEnvelope(caseName))
            {
                var (mx, mn) = _res.Envelope(frameId, _res.ExpandCombos(caseName));
                min = mn;
                return mx;
            }
            var r = _res.Get(caseName);
            return r.Frames.TryGetValue(frameId, out var ff) ? ff : null;
        }
        catch (Exception) { return null; }
    }

    private void DrawDiagram()
    {
        diagramCanvas.Children.Clear();
        double w = diagramHost.ActualWidth, h = diagramHost.ActualHeight;
        if (w < 40 || h < 40) return;
        if (_sel == null || _res == null)
        {
            AddText(_res == null ? "Biểu đồ M3: chạy phân tích trước." : "Biểu đồ M3: chọn một cấu kiện.", new Point(8, 8), Brushes.Gray, 11, false, false, diagramCanvas);
            return;
        }
        string c = cbDiagramCase.SelectedItem as string;
        var mx = ForcesOf(_sel.Id, c, out var mn);
        if (mx == null || mx.Count < 2) return;
        var curves = mn != null ? new[] { mx, mn } : new[] { mx };
        double L = mx.Stations[^1] - mx.Stations[0];
        if (L < 1e-9) return;
        double mAbs = curves.SelectMany(f => Enumerable.Range(0, f.Count).Select(i => Math.Abs(f.M3(i)))).DefaultIfEmpty(0).Max();
        double left = 40, right = 40, top = 26, bottom = 22;
        double sx = (w - left - right) / L;
        double y0 = top + (h - top - bottom) / 2;
        double sy = mAbs > 1e-9 ? (h - top - bottom) / 2 / mAbs : 0;

        // trục thanh
        diagramCanvas.Children.Add(new Shapes.Line { X1 = left, Y1 = y0, X2 = left + L * sx, Y2 = y0, Stroke = Brushes.Black, StrokeThickness = 1.5 });
        var colors = new[] { Brushes.Crimson, Brushes.RoyalBlue };
        for (int k = 0; k < curves.Length; k++)
        {
            var f = curves[k];
            var pts = new PointCollection { new Point(left, y0) };
            for (int i = 0; i < f.Count; i++)
                pts.Add(new Point(left + (f.Stations[i] - mx.Stations[0]) * sx, y0 + f.M3(i) * sy)); // M3 dương (căng dưới) vẽ phía dưới
            pts.Add(new Point(left + L * sx, y0));
            var poly = new Shapes.Polygon
            {
                Points = pts, Stroke = colors[k], StrokeThickness = 1.2,
                Fill = new SolidColorBrush(Color.FromArgb(40, ((SolidColorBrush)colors[k]).Color.R, ((SolidColorBrush)colors[k]).Color.G, ((SolidColorBrush)colors[k]).Color.B))
            };
            diagramCanvas.Children.Add(poly);
            // giá trị đầu, cuối, cực trị
            int iMax = 0, iMin = 0;
            for (int i = 1; i < f.Count; i++) { if (f.M3(i) > f.M3(iMax)) iMax = i; if (f.M3(i) < f.M3(iMin)) iMin = i; }
            foreach (int i in new[] { 0, f.Count - 1, iMax, iMin }.Distinct())
            {
                double v = f.M3(i);
                if (Math.Abs(v) < 1e-3 && i != 0 && i != f.Count - 1) continue;
                var p = new Point(left + (f.Stations[i] - mx.Stations[0]) * sx, y0 + v * sy + (v >= 0 ? 8 : -8));
                AddText($"{v:0.0}", p, colors[k], 10, true, false, diagramCanvas);
            }
        }
        AddText($"M3 (kN.m) – {_sel.Label} – {c}{(mn != null ? " (đỏ: max, xanh: min)" : "")}   L = {L:0.00} m",
            new Point(6, 4), Brushes.Black, 11, false, false, diagramCanvas);
    }

    private void DiagramHost_SizeChanged(object sender, SizeChangedEventArgs e) => DrawDiagram();

    private void CbDiagramCase_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ShowSelection();
    }
}

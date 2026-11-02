using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using KetCauVXT.Drawing;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>Dàn trang thủ công trong cửa sổ dàn trang: công cụ đặt chữ/đường/khung/tròn/mây/ảnh/bảng/kích thước,
/// chọn (nhấp, Shift+nhấp, kéo khung), di chuyển, tay nắm đổi cỡ/xoay, bắt lưới và bắt đối tượng (đường gióng),
/// căn hàng/phân bố, thứ tự trên dưới, khoá, sao chép/dán giữa các tờ, hoàn tác/làm lại (ảnh chụp JSON các tờ).</summary>
public partial class SheetWindow
{
    internal enum SheetTool { Select, Text, Line, Arrow, Rect, Circle, Cloud, Image, Table, Dim }

    private enum DragMode { None, Move, Handle, Pan, Rubber, Create, PanContent }
    private enum HKind { Rect, TextScale, Rotate, Vertex, DimOffset, Radius }

    private SheetTool _tool = SheetTool.Select;
    /// <summary>Các đối tượng đang chọn: SheetView hoặc SheetItem.</summary>
    private readonly List<object> _selObjs = new();
    private readonly List<UIElement> _overlay = new();
    private DrawingHost _itemsHost, _gridHost;
    private Shapes.Rectangle _marginRect;

    // hoàn tác / làm lại
    private readonly UndoStack _undo = new() { Limit = 150 };
    private string _undoKey;
    // bộ nhớ tạm dùng chung giữa các cửa sổ dàn trang
    private static string _clip;
    private static Sheet _clipSource;

    private double _gridStep = 5;

    // kéo chuột
    private DragMode _drag;
    private Point _dragStart;
    private bool _dragMoved;
    private double _sx0, _sy0;              // điểm bắt đầu (mm giấy, y lên)
    private double _opx, _opy;              // pan màn hình lúc bắt đầu
    private double _appliedDx, _appliedDy;  // độ dịch đã áp dụng khi kéo
    private Rect2 _dragUnion0;
    private List<Rect2> _snapTargets;
    private List<object> _moving;
    private string _preDrag;
    private double _guideX = double.NaN, _guideY = double.NaN;
    private Rect2 _rubber = Rect2.Empty;
    private bool _rubberCrossing;
    // tay nắm
    private (double X, double Y, HKind K, int I) _hk;
    private object _hObj;
    private Rect2 _hOrig;
    private SheetItem _hOrigItem;
    private SheetView _panView;
    private double _panX0, _panY0;
    // đối tượng đang vẽ
    private SheetItem _pending;

    private static readonly (string Name, string Hex)[] ColorChoices =
    {
        ("Theo lớp", ""), ("Đen", "#000000"), ("Đỏ", "#E00000"), ("Xanh lá", "#008000"), ("Xanh dương", "#0040FF"),
        ("Lục lam", "#0090B0"), ("Tím", "#A000A0"), ("Cam", "#FF7F00"), ("Nâu", "#8B4513"), ("Xám", "#808080")
    };

    private static readonly string[] WeightChoices = { "Theo lớp", "0.13", "0.18", "0.25", "0.35", "0.5", "0.7", "1.0" };

    private void InitEditUi()
    {
        foreach (double g in SheetLayoutMath.GridSteps) cbGrid.Items.Add(g.ToString("0.#", Inv));
        _loading = true;
        cbGrid.SelectedIndex = Array.IndexOf(SheetLayoutMath.GridSteps, 5.0);
        foreach (var l in SheetBuilder.ItemLayers) cbILayer.Items.Add(l);
        foreach (var (n, _) in ColorChoices) cbIColor.Items.Add(n);
        foreach (var w in WeightChoices) cbIWeight.Items.Add(w);
        _loading = false;
    }

    // ==================================================================
    // Hoàn tác / làm lại
    // ==================================================================
    private string Snapshot() => SheetBuilder.Snapshot(_p);

    /// <summary>Ghi trạng thái trước khi sửa. key ≠ null: các lần sửa liên tiếp cùng key (gõ phím, nhích) gộp thành một bước.</summary>
    private void Record(string key = null)
    {
        if (key != null && key == _undoKey) return;
        _undo.Push(Snapshot());
        _undoKey = key;
    }

    internal bool Undo() => ApplyState(_undo.Undo(Snapshot()), "Hoàn tác");
    internal bool Redo() => ApplyState(_undo.Redo(Snapshot()), "Làm lại");

    private bool ApplyState(string json, string what)
    {
        if (json == null) { SetStatus(what + ": không còn bước nào."); return false; }
        int idx = _sheet == null ? 0 : Math.Max(0, _p.Sheets.IndexOf(_sheet));
        if (_pending != null) _pending = null;
        SheetBuilder.Restore(_p, json);
        _undoKey = null;
        _sheet = null;
        _selObjs.Clear(); _sel = null;
        _keepView = true;
        try { RefreshSheetList(Math.Min(idx, _p.Sheets.Count - 1)); }
        finally { _keepView = false; }
        Dirty();
        SetStatus($"{what} (còn {_undo.UndoCount} bước hoàn tác, {_undo.RedoCount} bước làm lại).");
        return true;
    }

    private void BtnUndo_Click(object sender, RoutedEventArgs e) => Undo();
    private void BtnRedo_Click(object sender, RoutedEventArgs e) => Redo();

    // ==================================================================
    // Chọn
    // ==================================================================
    private SheetItem SelItem => _selObjs.Count == 1 ? _selObjs[0] as SheetItem : null;

    private void ClearSelection() => SetSelection();

    /// <summary>Đặt danh sách đối tượng đang chọn (khung nhìn và/hoặc đối tượng chú thích).</summary>
    internal void SetSelection(params object[] objs)
    {
        _selObjs.Clear();
        if (objs != null) foreach (var o in objs) if (o != null && !_selObjs.Contains(o)) _selObjs.Add(o);
        SyncSelection();
    }

    private void SyncSelection()
    {
        if (_sheet != null) _selObjs.RemoveAll(o => !(o is SheetView v && _sheet.Views.Contains(v)) && !(o is SheetItem it && _sheet.Items.Contains(it)));
        else _selObjs.Clear();
        _sel = _selObjs.Count == 1 ? _selObjs[0] as SheetView : null;
        _loading = true;
        lstViews.SelectedIndex = _sel != null && _sheet != null ? _sheet.Views.IndexOf(_sel) : -1;
        _loading = false;
        LoadViewProps();
        LoadItemProps();
        UpdateSelection();
    }

    private (double X, double Y) ToPaper(Point s)
    {
        var pg = ToPage(s);
        return (pg.X, PH - pg.Y);
    }

    private Point ToPage(Point s) => new((s.X - _panX) / _zoom, (s.Y - _panY) / _zoom);

    private object HitObject(double x, double y)
    {
        if (_sheet == null) return null;
        double tol = 5 / _zoom;
        for (int i = _sheet.Items.Count - 1; i >= 0; i--)
            if (SheetLayoutMath.Hit(_sheet.Items[i], x, y, tol)) return _sheet.Items[i];
        for (int i = _sheet.Views.Count - 1; i >= 0; i--)
        {
            var v = _sheet.Views[i];
            if (x >= v.X && x <= v.X + v.W && y >= v.Y && y <= v.Y + v.H) return v;
        }
        return null;
    }

    private IEnumerable<object> AllObjects() =>
        _sheet == null ? Enumerable.Empty<object>() : _sheet.Views.Cast<object>().Concat(_sheet.Items);

    /// <summary>Đích bắt điểm: các đối tượng không đang chọn + mép giấy, khung viền, vùng vẽ.</summary>
    private List<Rect2> SnapTargets(ICollection<object> exclude)
    {
        var res = new List<Rect2>();
        if (_sheet == null) return res;
        foreach (var o in AllObjects()) if (exclude == null || !exclude.Contains(o)) res.Add(SheetLayoutMath.Bounds(o));
        var (pw, ph) = _sheet.PaperSize();
        res.Add(new Rect2(0, 0, pw, ph));
        var (inner, _) = SheetBuilder.Frame(_sheet);
        res.Add(inner);
        res.Add(SheetBuilder.DrawingArea(_sheet));
        return res;
    }

    private double SnapTol => chkSnapObj.IsChecked == true ? 8 / _zoom : 0;
    private double SnapGridStep => chkSnapGrid.IsChecked == true ? _gridStep : 0;

    private (double X, double Y) SnapPt(double x, double y, bool show = true)
    {
        var (sx, sy, gx, gy) = SheetLayoutMath.SnapPoint(x, y, chkSnapObj.IsChecked == true ? _snapTargets : null, SnapTol, SnapGridStep);
        if (show) { _guideX = gx; _guideY = gy; }
        return (sx, sy);
    }

    // ==================================================================
    // Lớp phủ: khung chọn, tay nắm, đường gióng, khung chọn vùng
    // ==================================================================
    private void AddOverlay(UIElement el)
    {
        pageCanvas.Children.Add(el);
        _overlay.Add(el);
    }

    private List<(double X, double Y, HKind K, int I)> Handles()
    {
        var res = new List<(double, double, HKind, int)>();
        if (_selObjs.Count != 1 || SheetLayoutMath.IsLocked(_selObjs[0])) return res;
        var o = _selObjs[0];
        if (o is SheetView v)
        {
            var b = SheetLayoutMath.Bounds(v);
            for (int h = 0; h < 8; h++) { var (hx, hy) = SheetLayoutMath.HandlePos(b, h); res.Add((hx, hy, HKind.Rect, h)); }
            return res;
        }
        if (o is not SheetItem it) return res;
        switch (it.Kind)
        {
            case SheetItemKind.Rect:
            case SheetItemKind.Image:
            case SheetItemKind.Table:
            case SheetItemKind.Cloud:
            {
                var b = SheetLayoutMath.Bounds(it);
                for (int h = 0; h < 8; h++) { var (hx, hy) = SheetLayoutMath.HandlePos(b, h); res.Add((hx, hy, HKind.Rect, h)); }
                break;
            }
            case SheetItemKind.Text:
            {
                var b = SheetLayoutMath.Bounds(it);
                res.Add((b.X2, b.Y1, HKind.TextScale, 2));
                double a = it.Rotation * Math.PI / 180, d = Math.Max(it.TextHeight, 0) + 22 / _zoom;
                res.Add((it.X - Math.Sin(a) * d, it.Y + Math.Cos(a) * d, HKind.Rotate, 0));
                break;
            }
            case SheetItemKind.Circle:
                res.Add((it.X + it.R, it.Y, HKind.Radius, 0));
                break;
            case SheetItemKind.Line:
                for (int i = 0; i + 1 < it.Pts.Count; i += 2) res.Add((it.Pts[i], it.Pts[i + 1], HKind.Vertex, i / 2));
                break;
            case SheetItemKind.Dim:
                if (it.Pts.Count >= 4)
                {
                    res.Add((it.Pts[0], it.Pts[1], HKind.Vertex, 0));
                    res.Add((it.Pts[2], it.Pts[3], HKind.Vertex, 1));
                    var (nx, ny) = DimNormal(it);
                    res.Add(((it.Pts[0] + it.Pts[2]) / 2 + nx * it.Offset, (it.Pts[1] + it.Pts[3]) / 2 + ny * it.Offset, HKind.DimOffset, 0));
                }
                break;
        }
        return res;
    }

    private static (double Nx, double Ny) DimNormal(SheetItem it)
    {
        double dx = it.Pts[2] - it.Pts[0], dy = it.Pts[3] - it.Pts[1], l = Math.Sqrt(dx * dx + dy * dy);
        return l < 1e-9 ? (0, 1) : (-dy / l, dx / l);
    }

    private void UpdateSelection()
    {
        foreach (var el in _overlay) pageCanvas.Children.Remove(el);
        _overlay.Clear();
        if (_sheet == null || _paper == null) return;
        double px = 1 / _zoom, ph = PH;
        bool single = _selObjs.Count == 1;
        foreach (var o in _selObjs)
        {
            var b = SheetLayoutMath.Bounds(o);
            if (b.IsEmpty) continue;
            bool locked = SheetLayoutMath.IsLocked(o);
            var r = new Shapes.Rectangle
            {
                Width = Math.Max(0.1, b.W), Height = Math.Max(0.1, b.H), IsHitTestVisible = false,
                Stroke = locked ? Brushes.OrangeRed : (o is SheetView ? Brushes.Gold : new SolidColorBrush(Color.FromRgb(0, 150, 255))),
                StrokeThickness = (o is SheetView ? 2 : 1.2) * px,
                Fill = o is SheetView ? new SolidColorBrush(Color.FromArgb(28, 255, 230, 0)) : null
            };
            if (locked || o is SheetItem) r.StrokeDashArray = new DoubleCollection { 4, 3 };
            if (single && o is SheetView && Math.Max(b.W, b.H) * _zoom < 3000) r.Effect = _glow;
            Canvas.SetLeft(r, b.X1); Canvas.SetTop(r, ph - b.Y2);
            AddOverlay(r);
        }
        foreach (var (hx, hy, k, _) in Handles())
        {
            double hs = (k == HKind.Rotate ? 10 : 8) * px;
            Shapes.Shape h = k == HKind.Rotate || k == HKind.Radius
                ? new Shapes.Ellipse { Fill = Brushes.LimeGreen }
                : new Shapes.Rectangle { Fill = k == HKind.DimOffset ? Brushes.Cyan : Brushes.Yellow };
            h.Width = h.Height = hs; h.Stroke = Brushes.Black; h.StrokeThickness = px; h.IsHitTestVisible = false;
            Canvas.SetLeft(h, hx - hs / 2); Canvas.SetTop(h, ph - hy - hs / 2);
            AddOverlay(h);
        }
        var guide = new SolidColorBrush(Color.FromRgb(230, 0, 200));
        if (_drag != DragMode.None && !double.IsNaN(_guideX))
            AddOverlay(new Shapes.Line { X1 = _guideX, X2 = _guideX, Y1 = -1000, Y2 = ph + 1000, Stroke = guide, StrokeThickness = px, IsHitTestVisible = false, StrokeDashArray = new DoubleCollection { 6, 3 } });
        if (_drag != DragMode.None && !double.IsNaN(_guideY))
            AddOverlay(new Shapes.Line { X1 = -1000, X2 = PW + 1000, Y1 = ph - _guideY, Y2 = ph - _guideY, Stroke = guide, StrokeThickness = px, IsHitTestVisible = false, StrokeDashArray = new DoubleCollection { 6, 3 } });
        if (!_rubber.IsEmpty)
        {
            var rr = new Shapes.Rectangle
            {
                Width = Math.Max(0.1, _rubber.W), Height = Math.Max(0.1, _rubber.H), StrokeThickness = px, IsHitTestVisible = false,
                Stroke = _rubberCrossing ? Brushes.Green : Brushes.RoyalBlue,
                Fill = new SolidColorBrush(_rubberCrossing ? Color.FromArgb(30, 0, 160, 0) : Color.FromArgb(30, 0, 80, 255))
            };
            if (_rubberCrossing) rr.StrokeDashArray = new DoubleCollection { 5, 3 };
            Canvas.SetLeft(rr, _rubber.X1); Canvas.SetTop(rr, ph - _rubber.Y2);
            AddOverlay(rr);
        }
    }

    private void RenderItemsHost()
    {
        if (_itemsHost == null || _sheet == null) return;
        _itemsHost.Drawing = SheetRender.RenderItems(_sheet.Items, PH, MinStroke);
        _itemsHost.InvalidateVisual();
    }

    private void RebuildGrid()
    {
        if (_gridHost == null || _sheet == null) return;
        if (chkShowGrid.IsChecked != true) { _gridHost.Drawing = null; _gridHost.InvalidateVisual(); return; }
        var (pw, ph) = _sheet.PaperSize();
        double step = Math.Max(0.5, _gridStep);
        while (step * _zoom < 7) step *= 2;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            for (double x = 0; x <= pw + 1e-9; x += step) { ctx.BeginFigure(new Point(x, 0), false, false); ctx.LineTo(new Point(x, ph), true, false); }
            for (double y = 0; y <= ph + 1e-9; y += step) { ctx.BeginFigure(new Point(0, ph - y), false, false); ctx.LineTo(new Point(pw, ph - y), true, false); }
        }
        g.Freeze();
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(205, 222, 245)), 1 / _zoom);
        pen.Freeze();
        var dg = new DrawingGroup();
        using (var dc = dg.Open()) dc.DrawGeometry(null, pen, g);
        dg.Freeze();
        _gridHost.Drawing = dg;
        _gridHost.InvalidateVisual();
    }

    private void UpdateMarginRect()
    {
        if (_marginRect == null || _sheet == null) return;
        var a = SheetBuilder.DrawingArea(_sheet);
        _marginRect.Visibility = chkShowMargins.IsChecked == true && !a.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        _marginRect.Width = Math.Max(0.1, a.W); _marginRect.Height = Math.Max(0.1, a.H);
        Canvas.SetLeft(_marginRect, a.X1); Canvas.SetTop(_marginRect, PH - a.Y2);
    }

    private void LayoutAid_Click(object sender, RoutedEventArgs e)
    {
        RebuildGrid();
        UpdateMarginRect();
    }

    private void CbGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int i = cbGrid.SelectedIndex;
        if (i >= 0 && i < SheetLayoutMath.GridSteps.Length) _gridStep = SheetLayoutMath.GridSteps[i];
        if (!_loading) RebuildGrid();
    }

    // ==================================================================
    // Công cụ
    // ==================================================================
    private void Tool_Checked(object sender, RoutedEventArgs e)
    {
        var t = sender == toolText ? SheetTool.Text : sender == toolLine ? SheetTool.Line : sender == toolArrow ? SheetTool.Arrow
            : sender == toolRect ? SheetTool.Rect : sender == toolCircle ? SheetTool.Circle : sender == toolCloud ? SheetTool.Cloud
            : sender == toolImage ? SheetTool.Image : sender == toolTable ? SheetTool.Table : sender == toolDim ? SheetTool.Dim : SheetTool.Select;
        if (_pending != null && t != _tool) FinishPending();
        _tool = t;
        if (pageHost != null) pageHost.Cursor = t == SheetTool.Select ? Cursors.Arrow : Cursors.Cross;
        if (txtStatus != null && t != SheetTool.Select)
            SetStatus((t == SheetTool.Line || t == SheetTool.Arrow)
                ? "Kéo để vẽ đoạn thẳng, hoặc nhấp từng điểm – nhấp đúp / Enter / chuột phải để kết thúc, Esc huỷ. Shift: thẳng góc."
                : t == SheetTool.Text ? "Nhấp vào trang để đặt chữ, rồi sửa nội dung ở bảng thuộc tính."
                : "Kéo trên trang để đặt đối tượng (nhấp: kích thước mặc định).");
    }

    internal void SetTool(SheetTool t)
    {
        var rb = t switch
        {
            SheetTool.Text => toolText, SheetTool.Line => toolLine, SheetTool.Arrow => toolArrow, SheetTool.Rect => toolRect,
            SheetTool.Circle => toolCircle, SheetTool.Cloud => toolCloud, SheetTool.Image => toolImage, SheetTool.Table => toolTable,
            SheetTool.Dim => toolDim, _ => toolSelect
        };
        rb.IsChecked = true;
    }

    /// <summary>Thêm đối tượng chú thích vào tờ hiện tại (ghi hoàn tác), vẽ lại và chọn nó.</summary>
    internal SheetItem AddItem(SheetItem it, bool record = true)
    {
        if (_sheet == null || it == null) return null;
        if (record) Record();
        _sheet.Items ??= new List<SheetItem>();
        _sheet.Items.Add(it);
        RenderItemsHost();
        SetSelection(it);
        UpdateSheetListText();
        Dirty();
        return it;
    }

    private void BeginCreate(double px, double py, int clicks)
    {
        _snapTargets = SnapTargets(null);
        var (sx, sy) = SnapPt(px, py);
        _sx0 = sx; _sy0 = sy;
        switch (_tool)
        {
            case SheetTool.Text:
            {
                var t = AddItem(SheetBuilder.NewText(sx, sy, "VĂN BẢN"));
                SetTool(SheetTool.Select);
                Dispatcher.BeginInvoke(new Action(() => { txtIText.Focus(); txtIText.SelectAll(); }), System.Windows.Threading.DispatcherPriority.Input);
                SetStatus("Đã đặt chữ – sửa nội dung ở ô \"Nội dung\" rồi Ctrl+Enter hoặc bấm Áp dụng.");
                _ = t;
                return;
            }
            case SheetTool.Line:
            case SheetTool.Arrow:
                if (_pending != null && _pending.Kind == SheetItemKind.Line)
                {
                    if (clicks >= 2) { FinishPending(); return; }
                    var (ox, oy) = Ortho(_pending, sx, sy);
                    _pending.Pts[^2] = ox; _pending.Pts[^1] = oy;
                    _pending.Pts.Add(ox); _pending.Pts.Add(oy);
                }
                else
                {
                    Record();
                    _pending = SheetBuilder.NewLine(sx, sy, sx, sy, _tool == SheetTool.Arrow);
                    if (_tool == SheetTool.Arrow) _pending.LineWeight = 0;
                    _sheet.Items.Add(_pending);
                }
                break;
            default:
                Record();
                _pending = _tool switch
                {
                    SheetTool.Rect => SheetBuilder.NewRect(sx, sy, 0, 0),
                    SheetTool.Cloud => SheetBuilder.NewCloud(sx, sy, 0, 0),
                    SheetTool.Image => SheetBuilder.NewImage(sx, sy, 0, 0, ""),
                    SheetTool.Table => SheetBuilder.NewTable(sx, sy, 0, 0, 3, 3),
                    SheetTool.Circle => SheetBuilder.NewCircle(sx, sy, 0.1),
                    _ => SheetBuilder.NewDim(sx, sy, sx, sy)
                };
                _sheet.Items.Add(_pending);
                break;
        }
        _drag = DragMode.Create;
        pageHost.CaptureMouse();
        RenderItemsHost();
    }

    /// <summary>Shift: ép đoạn cuối của đường nằm ngang/đứng so với điểm cố định trước đó.</summary>
    private static (double X, double Y) Ortho(SheetItem line, double x, double y)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0 || line.Pts.Count < 4) return (x, y);
        double bx = line.Pts[^4], by = line.Pts[^3];
        return Math.Abs(x - bx) >= Math.Abs(y - by) ? (x, by) : (bx, y);
    }

    private void UpdateCreate(double px, double py)
    {
        var it = _pending;
        if (it == null) return;
        var (sx, sy) = SnapPt(px, py);
        switch (it.Kind)
        {
            case SheetItemKind.Line:
            {
                var (ox, oy) = Ortho(it, sx, sy);
                it.Pts[^2] = ox; it.Pts[^1] = oy;
                break;
            }
            case SheetItemKind.Dim:
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                {
                    if (Math.Abs(sx - _sx0) >= Math.Abs(sy - _sy0)) sy = _sy0; else sx = _sx0;
                }
                it.Pts[2] = sx; it.Pts[3] = sy;
                break;
            case SheetItemKind.Circle:
                it.R = Math.Max(0.1, Math.Sqrt((sx - _sx0) * (sx - _sx0) + (sy - _sy0) * (sy - _sy0)));
                break;
            default:
                it.X = Math.Min(_sx0, sx); it.Y = Math.Min(_sy0, sy);
                it.W = Math.Abs(sx - _sx0); it.H = Math.Abs(sy - _sy0);
                break;
        }
        RenderItemsHost();
        UpdateSelection();
    }

    private void EndCreate(double px, double py, bool moved)
    {
        var it = _pending;
        if (it == null) return;
        if (it.Kind == SheetItemKind.Line)
        {
            if (moved) { UpdateCreate(px, py); FinishPending(); }
            return;     // nhấp không kéo: tiếp tục thêm điểm
        }
        if (moved) UpdateCreate(px, py);
        bool tiny = it.Kind switch
        {
            SheetItemKind.Circle => it.R < 1,
            SheetItemKind.Dim => Math.Abs(it.Pts[2] - it.Pts[0]) + Math.Abs(it.Pts[3] - it.Pts[1]) < 1,
            _ => it.W < 2 || it.H < 2
        };
        if (tiny)
        {
            switch (it.Kind)
            {
                case SheetItemKind.Circle: it.R = 10; break;
                case SheetItemKind.Dim: it.Pts[2] = it.Pts[0] + 30; it.Pts[3] = it.Pts[1]; break;
                case SheetItemKind.Table: it.W = 90; it.H = 30; it.Y -= it.H; break;
                case SheetItemKind.Image: it.W = 40; it.H = 30; it.Y -= it.H; break;
                default: it.W = 40; it.H = 25; it.Y -= it.H; break;
            }
        }
        _pending = null;
        if (it.Kind == SheetItemKind.Image && !PickImage(it))
        {
            _sheet.Items.Remove(it);
            RenderItemsHost();
            SetTool(SheetTool.Select);
            UpdateSelection();
            return;
        }
        RenderItemsHost();
        SetTool(SheetTool.Select);
        SetSelection(it);
        UpdateSheetListText();
        Dirty();
        if (it.Kind == SheetItemKind.Table)
        {
            Dispatcher.BeginInvoke(new Action(() => { txtIText.Focus(); txtIText.SelectAll(); }), System.Windows.Threading.DispatcherPriority.Input);
            SetStatus("Sửa nội dung bảng: mỗi hàng một dòng, các ô ngăn bởi dấu | – Ctrl+Enter để áp dụng.");
        }
    }

    /// <summary>Kết thúc đường đang vẽ (bỏ điểm trùng); ít hơn 2 điểm thì huỷ.</summary>
    private void FinishPending()
    {
        var it = _pending;
        _pending = null;
        if (it == null || _sheet == null) return;
        if (it.Kind == SheetItemKind.Line)
        {
            var pts = new List<double>();
            for (int i = 0; i + 1 < it.Pts.Count; i += 2)
            {
                double x = it.Pts[i], y = it.Pts[i + 1];
                if (pts.Count >= 2 && Math.Abs(pts[^2] - x) < 0.05 && Math.Abs(pts[^1] - y) < 0.05) continue;
                pts.Add(x); pts.Add(y);
            }
            it.Pts = pts;
            if (pts.Count < 4) { _sheet.Items.Remove(it); RenderItemsHost(); UpdateSelection(); SetTool(SheetTool.Select); return; }
            it.X = pts[0]; it.Y = pts[1];
        }
        RenderItemsHost();
        SetTool(SheetTool.Select);
        SetSelection(it);
        UpdateSheetListText();
        Dirty();
    }

    private void CancelPending()
    {
        if (_pending == null) return;
        _sheet?.Items.Remove(_pending);
        _pending = null;
        if (_drag == DragMode.Create) { _drag = DragMode.None; pageHost.ReleaseMouseCapture(); }
        RenderItemsHost();
        UpdateSelection();
        SetStatus("Đã huỷ.");
    }

    // ==================================================================
    // Chuột
    // ==================================================================
    private void PageHost_MouseDown(object sender, MouseButtonEventArgs e)
    {
        pageHost.Focus();
        var s = e.GetPosition(pageHost);
        _dragStart = s;
        _dragMoved = false;
        if (e.ChangedButton == MouseButton.Right && _pending != null && _pending.Kind == SheetItemKind.Line) { FinishPending(); e.Handled = true; return; }
        if (e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle)
        {
            _drag = DragMode.Pan; _opx = _panX; _opy = _panY;
            pageHost.CaptureMouse();
            return;
        }
        if (e.ChangedButton != MouseButton.Left || _sheet == null) return;
        var (px, py) = ToPaper(s);
        _sx0 = px; _sy0 = py;
        _guideX = _guideY = double.NaN;
        if (_tool != SheetTool.Select) { BeginCreate(px, py, e.ClickCount); return; }

        var hs = Handles();
        double tol = 7 / _zoom;
        for (int i = hs.Count - 1; i >= 0; i--)
            if (Math.Abs(hs[i].X - px) <= tol && Math.Abs(hs[i].Y - py) <= tol) { BeginHandle(hs[i]); return; }

        var hit = HitObject(px, py);
        var mods = Keyboard.Modifiers;
        bool shift = (mods & ModifierKeys.Shift) != 0, ctrl = (mods & ModifierKeys.Control) != 0;
        if (hit == null)
        {
            if (!shift && !ctrl) ClearSelection();
            _drag = DragMode.Rubber;
            _rubber = new Rect2(px, py, px, py);
            pageHost.CaptureMouse();
            return;
        }
        if (shift || ctrl)
        {
            if (_selObjs.Contains(hit)) _selObjs.Remove(hit); else _selObjs.Add(hit);
            SyncSelection();
            return;
        }
        if (!_selObjs.Contains(hit)) SetSelection(hit);
        if (e.ClickCount == 2 && hit is SheetItem ti && ti.Kind is SheetItemKind.Text or SheetItemKind.Table or SheetItemKind.Dim)
        {
            txtIText.Focus(); txtIText.SelectAll();
            return;
        }
        if ((mods & ModifierKeys.Alt) != 0 && hit is SheetView hv)
        {
            if (hv.Locked) { SetStatus("Khung nhìn đang khoá."); return; }
            _panView = hv; _panX0 = hv.PanX; _panY0 = hv.PanY;
            _preDrag = Snapshot();
            _drag = DragMode.PanContent;
            pageHost.CaptureMouse();
            return;
        }
        BeginMove();
    }

    private void BeginMove()
    {
        _moving = _selObjs.Where(o => !SheetLayoutMath.IsLocked(o)).ToList();
        if (_moving.Count == 0) { if (_selObjs.Count > 0) SetStatus("Đối tượng đang khoá – không di chuyển được (Ctrl+L để mở khoá)."); return; }
        _preDrag = Snapshot();
        _appliedDx = _appliedDy = 0;
        _dragUnion0 = SheetLayoutMath.Union(_moving.Select(SheetLayoutMath.Bounds));
        _snapTargets = SnapTargets(_selObjs);
        _drag = DragMode.Move;
        pageHost.CaptureMouse();
    }

    private void BeginHandle((double X, double Y, HKind K, int I) h)
    {
        _hk = h;
        _hObj = _selObjs[0];
        _hOrig = SheetLayoutMath.Bounds(_hObj);
        _hOrigItem = _hObj is SheetItem it ? SheetBuilder.Clone(it) : null;
        _preDrag = Snapshot();
        _snapTargets = SnapTargets(_selObjs);
        _drag = DragMode.Handle;
        pageHost.CaptureMouse();
    }

    /// <summary>Chép lại toàn bộ thuộc tính từ bản sao src vào dst (danh sách được sao mới).</summary>
    private static void RestoreItem(SheetItem dst, SheetItem src)
    {
        foreach (var pi in typeof(SheetItem).GetProperties())
            if (pi.CanWrite && pi.CanRead) pi.SetValue(dst, pi.GetValue(src));
        dst.Pts = new List<double>(src.Pts ?? new List<double>());
        dst.Cells = new List<string>(src.Cells ?? new List<string>());
        dst.ColWidths = new List<double>(src.ColWidths ?? new List<double>());
    }

    private void PageHost_MouseMove(object sender, MouseEventArgs e)
    {
        var s = e.GetPosition(pageHost);
        if (_sheet != null)
        {
            var (cx, cy) = ToPaper(s);
            txtCursor.Text = $"X {F(cx)}  Y {F(cy)} mm";
        }
        if (_drag == DragMode.None)
        {
            if (_sheet == null) return;
            var (px0, py0) = ToPaper(s);
            if (_pending != null && _pending.Kind == SheetItemKind.Line) { UpdateCreate(px0, py0); return; }
            if (_tool != SheetTool.Select) { pageHost.Cursor = Cursors.Cross; return; }
            double tol = 7 / _zoom;
            var hs = Handles();
            if (hs.Any(h => Math.Abs(h.X - px0) <= tol && Math.Abs(h.Y - py0) <= tol)) { pageHost.Cursor = Cursors.SizeNWSE; return; }
            var hit = HitObject(px0, py0);
            pageHost.Cursor = hit == null ? Cursors.Arrow : SheetLayoutMath.IsLocked(hit) ? Cursors.No : Cursors.SizeAll;
            return;
        }
        double dxs = s.X - _dragStart.X, dys = s.Y - _dragStart.Y;
        if (Math.Abs(dxs) + Math.Abs(dys) > 2) _dragMoved = true;
        if (_drag == DragMode.Pan)
        {
            _panX = _opx + dxs; _panY = _opy + dys; _autoFit = false;
            ApplyTransform();
            return;
        }
        if (!_dragMoved) return;
        var (px, py) = ToPaper(s);
        switch (_drag)
        {
            case DragMode.Move: DragMove(px, py); break;
            case DragMode.Handle: DragHandle(px, py); break;
            case DragMode.Create: UpdateCreate(px, py); break;
            case DragMode.Rubber:
                _rubber = new Rect2(_sx0, _sy0, px, py);
                _rubberCrossing = px < _sx0;
                UpdateSelection();
                break;
            case DragMode.PanContent:
                if (_panView != null)
                {
                    _panView.PanX = _panX0 + (px - _sx0);
                    _panView.PanY = _panY0 + (py - _sy0);
                    RenderView(_panView);
                    LoadViewProps(true);
                }
                break;
        }
    }

    private void DragMove(double px, double py)
    {
        double dx = px - _sx0, dy = py - _sy0;
        var sr = SheetLayoutMath.SnapMove(_dragUnion0.Offset(dx, dy), chkSnapObj.IsChecked == true ? _snapTargets : null, SnapTol, SnapGridStep);
        dx += sr.Dx; dy += sr.Dy;
        _guideX = sr.GuideX; _guideY = sr.GuideY;
        double ddx = dx - _appliedDx, ddy = dy - _appliedDy;
        _appliedDx = dx; _appliedDy = dy;
        bool items = false;
        foreach (var o in _moving)
        {
            SheetLayoutMath.Move(o, ddx, ddy);
            if (o is SheetView v) PositionView(v); else items = true;
        }
        if (items) RenderItemsHost();
        UpdateSelection();
        if (_sel != null) LoadViewProps(true); else LoadItemPos();
    }

    private void DragHandle(double px, double py)
    {
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        var (sx, sy) = SnapPt(px, py);
        if (_hObj is SheetView v)
        {
            var nb = SheetLayoutMath.ResizeRect(_hOrig, _hk.I, sx, sy, 20);
            v.X = nb.X1; v.Y = nb.Y1; v.W = nb.W; v.H = nb.H;
            PositionView(v);
            if (_hosts.TryGetValue(v, out var h)) h.Host.Clip = new RectangleGeometry(new Rect(0, 0, v.W, v.H));
            LoadViewProps(true);
            UpdateSelection();
            return;
        }
        if (_hObj is not SheetItem it || _hOrigItem == null) return;
        RestoreItem(it, _hOrigItem);
        switch (_hk.K)
        {
            case HKind.Rect:
            {
                var nb = SheetLayoutMath.ResizeRect(_hOrig, _hk.I, sx, sy, 1);
                if (shift && it.Kind == SheetItemKind.Image && _hOrig.W > 0 && _hOrig.H > 0)
                {
                    // Shift: giữ tỷ lệ ảnh
                    double k = Math.Max(nb.W / _hOrig.W, nb.H / _hOrig.H);
                    double w = _hOrig.W * k, hgt = _hOrig.H * k;
                    double x1 = (_hk.I is 0 or 6 or 7) ? _hOrig.X2 - w : _hOrig.X1;
                    double y1 = (_hk.I is 0 or 1 or 2) ? _hOrig.Y2 - hgt : _hOrig.Y1;
                    nb = new Rect2(x1, y1, x1 + w, y1 + hgt);
                }
                SheetLayoutMath.SetBounds(it, _hOrig, nb);
                break;
            }
            case HKind.TextScale:
                SheetLayoutMath.SetBounds(it, _hOrig, SheetLayoutMath.ResizeRect(_hOrig, 2, sx, sy, 0.5));
                break;
            case HKind.Rotate:
            {
                double ang = Math.Atan2(py - it.Y, px - it.X) * 180 / Math.PI - 90;
                if (!shift) ang = Math.Round(ang / 5) * 5;
                while (ang > 180) ang -= 360;
                while (ang <= -180) ang += 360;
                it.Rotation = Math.Abs(ang) < 1e-9 ? 0 : ang;
                _guideX = _guideY = double.NaN;
                break;
            }
            case HKind.Vertex:
            {
                int i = 2 * _hk.I;
                if (i + 1 >= it.Pts.Count) break;
                if (shift)
                {
                    int j = i >= 2 ? i - 2 : i + 2;
                    if (j + 1 < it.Pts.Count)
                    {
                        if (Math.Abs(sx - it.Pts[j]) >= Math.Abs(sy - it.Pts[j + 1])) sy = it.Pts[j + 1]; else sx = it.Pts[j];
                    }
                }
                it.Pts[i] = sx; it.Pts[i + 1] = sy;
                if (i == 0) { it.X = sx; it.Y = sy; }
                break;
            }
            case HKind.DimOffset:
            {
                var (nx, ny) = DimNormal(it);
                double off = (px - it.Pts[0]) * nx + (py - it.Pts[1]) * ny;
                it.Offset = Math.Round(off * 2) / 2;
                _guideX = _guideY = double.NaN;
                break;
            }
            case HKind.Radius:
                it.R = Math.Max(0.5, Math.Sqrt((sx - it.X) * (sx - it.X) + (sy - it.Y) * (sy - it.Y)));
                break;
        }
        RenderItemsHost();
        UpdateSelection();
        LoadItemPos();
    }

    private void PageHost_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_drag == DragMode.None) return;
        var mode = _drag;
        _drag = DragMode.None;
        pageHost.ReleaseMouseCapture();
        var (px, py) = ToPaper(e.GetPosition(pageHost));
        _guideX = _guideY = double.NaN;
        switch (mode)
        {
            case DragMode.Pan:
                _redrawTimer.Stop(); _redrawTimer.Start();
                return;
            case DragMode.Rubber:
            {
                var win = _rubber;
                _rubber = Rect2.Empty;
                if (_dragMoved && !win.IsEmpty)
                {
                    bool add = (Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control)) != 0;
                    var found = AllObjects().Where(o => SheetLayoutMath.InWindow(SheetLayoutMath.Bounds(o), win, _rubberCrossing)).ToList();
                    if (!add) _selObjs.Clear();
                    foreach (var o in found) if (!_selObjs.Contains(o)) _selObjs.Add(o);
                    SyncSelection();
                    SetStatus($"Đã chọn {_selObjs.Count} đối tượng" + (_rubberCrossing ? " (chọn giao khung)." : " (nằm trọn trong khung)."));
                }
                else UpdateSelection();
                return;
            }
            case DragMode.Create:
                EndCreate(px, py, _dragMoved);
                return;
        }
        // Move / Handle / PanContent
        if (!_dragMoved) { UpdateSelection(); return; }
        if (_preDrag != null) { _undo.Push(_preDrag); _undoKey = null; _preDrag = null; }
        foreach (var v in _selObjs.OfType<SheetView>()) RenderView(v);
        if (_panView != null) { RenderView(_panView); _panView = null; }
        RenderItemsHost();
        RebuildTitleBlock();
        Dirty();
        RefreshViewListText();
        UpdateSheetListText();
        LoadViewProps();
        LoadItemProps();
        UpdateSelection();
        var b = SheetLayoutMath.Union(_selObjs.Select(SheetLayoutMath.Bounds));
        var (pw, ph) = _sheet.PaperSize();
        bool outside = !b.IsEmpty && (b.X1 < -1e-6 || b.Y1 < -1e-6 || b.X2 > pw + 1e-6 || b.Y2 > ph + 1e-6);
        SetStatus(b.IsEmpty ? "" : $"X={F(b.X1)}, Y={F(b.Y1)}, W={F(b.W)}, H={F(b.H)} mm" + (outside ? " – NẰM NGOÀI GIẤY" : ""));
    }

    // ==================================================================
    // Bàn phím
    // ==================================================================
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is ComboBox) return;
        if (_sheet == null) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;
        bool ctrl = (mods & ModifierKeys.Control) != 0, shift = (mods & ModifierKeys.Shift) != 0;
        if (ctrl)
        {
            switch (key)
            {
                case Key.Z: if (shift) Redo(); else Undo(); break;
                case Key.Y: Redo(); break;
                case Key.C: CopySelection(); break;
                case Key.X: CopySelection(); DeleteSelection(); break;
                case Key.V: Paste(); break;
                case Key.D: DuplicateSelection(); break;
                case Key.A: _selObjs.Clear(); _selObjs.AddRange(AllObjects()); SyncSelection(); break;
                case Key.L: ToggleLock(); break;
                default: return;
            }
            e.Handled = true;
            return;
        }
        switch (key)
        {
            case Key.Escape:
                if (_pending != null) CancelPending();
                else if (_tool != SheetTool.Select) SetTool(SheetTool.Select);
                else if (_selObjs.Count > 0) ClearSelection();
                else return;                     // không có gì để huỷ: Esc đóng cửa sổ như cũ
                e.Handled = true;
                return;
            case Key.Enter:
                if (_pending == null) return;
                FinishPending();
                e.Handled = true;
                return;
            case Key.Delete:
                DeleteSelection();
                e.Handled = true;
                return;
            case Key.Left: case Key.Right: case Key.Up: case Key.Down:
            {
                double step = shift ? 10 : 1;
                double dx = key == Key.Left ? -step : key == Key.Right ? step : 0;
                double dy = key == Key.Down ? -step : key == Key.Up ? step : 0;
                Nudge(dx, dy);
                e.Handled = true;
                return;
            }
        }
    }

    /// <summary>Nhích các đối tượng đang chọn (không khoá) – các lần nhích liên tiếp gộp một bước hoàn tác.</summary>
    internal void Nudge(double dx, double dy)
    {
        var mv = _selObjs.Where(o => !SheetLayoutMath.IsLocked(o)).ToList();
        if (mv.Count == 0) return;
        Record("nudge");
        foreach (var o in mv)
        {
            SheetLayoutMath.Move(o, dx, dy);
            if (o is SheetView v) RenderView(v);
        }
        RenderItemsHost();
        RebuildTitleBlock();
        LoadViewProps(true);
        LoadItemPos();
        UpdateSelection();
        Dirty();
    }

    // ==================================================================
    // Xoá, sao chép, dán, nhân đôi, khoá
    // ==================================================================
    internal void DeleteSelection()
    {
        if (_sheet == null || _selObjs.Count == 0) return;
        var del = _selObjs.Where(o => !SheetLayoutMath.IsLocked(o)).ToList();
        int locked = _selObjs.Count - del.Count;
        if (del.Count == 0) { SetStatus("Đối tượng đang khoá – bỏ khoá trước khi xoá."); return; }
        Record();
        foreach (var o in del)
        {
            if (o is SheetView v) _sheet.Views.Remove(v);
            else if (o is SheetItem it) _sheet.Items.Remove(it);
        }
        _selObjs.Clear();
        RebuildPage();
        RefreshViewList();
        SyncSelection();
        UpdateSheetListText();
        Dirty();
        SetStatus($"Đã xoá {del.Count} đối tượng" + (locked > 0 ? $" ({locked} đối tượng khoá được giữ lại)." : "."));
    }

    private void BtnDeleteSel_Click(object sender, RoutedEventArgs e) => DeleteSelection();

    internal void CopySelection()
    {
        if (_sheet == null || _selObjs.Count == 0) return;
        _clip = SheetBuilder.ToClipboard(_selObjs.OfType<SheetView>(), _selObjs.OfType<SheetItem>(), _sheet.Number);
        _clipSource = _sheet;
        SetStatus($"Đã sao chép {_selObjs.Count} đối tượng – Ctrl+V để dán (cả sang tờ khác).");
    }

    internal void Paste()
    {
        if (_sheet == null) return;
        var c = SheetBuilder.FromClipboard(_clip);
        if (c == null || c.Views.Count + c.Items.Count == 0) { SetStatus("Bộ nhớ tạm trống."); return; }
        double d = ReferenceEquals(_clipSource, _sheet) ? 10 : 0;
        PasteObjects(c.Views, c.Items, d);
        // dán tiếp lần sau lệch thêm
        if (d > 0) _clip = SheetBuilder.ToClipboard(c.Views.Select(v => { var n = SheetBuilder.Clone(v); n.X += d; n.Y -= d; return n; }),
            c.Items.Select(i => { var n = SheetBuilder.Clone(i); SheetLayoutMath.Move(n, d, -d); return n; }), _sheet.Number);
    }

    private void PasteObjects(IEnumerable<SheetView> views, IEnumerable<SheetItem> items, double d)
    {
        Record();
        var added = new List<object>();
        foreach (var v0 in views)
        {
            var v = SheetBuilder.Clone(v0);
            v.X += d; v.Y -= d; v.Locked = false;
            _sheet.Views.Add(v);
            added.Add(v);
        }
        foreach (var i0 in items)
        {
            var it = SheetBuilder.Clone(i0);
            SheetLayoutMath.Move(it, d, -d);
            it.Locked = false;
            _sheet.Items.Add(it);
            added.Add(it);
        }
        RebuildPage();
        RefreshViewList();
        _selObjs.Clear();
        _selObjs.AddRange(added);
        SyncSelection();
        RebuildTitleBlock();
        UpdateSheetListText();
        Dirty();
        SetStatus($"Đã dán {added.Count} đối tượng.");
    }

    internal void DuplicateSelection()
    {
        if (_sheet == null || _selObjs.Count == 0) return;
        PasteObjects(_selObjs.OfType<SheetView>().ToList(), _selObjs.OfType<SheetItem>().ToList(), 10);
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e) => CopySelection();
    private void BtnPaste_Click(object sender, RoutedEventArgs e) => Paste();
    private void BtnDuplicate_Click(object sender, RoutedEventArgs e) => DuplicateSelection();

    private void ToggleLock()
    {
        if (_selObjs.Count == 0) return;
        Record();
        bool lockAll = _selObjs.Any(o => !SheetLayoutMath.IsLocked(o));
        foreach (var o in _selObjs)
        {
            if (o is SheetView v)
            {
                v.Locked = lockAll;
                if (_hosts.TryGetValue(v, out var h)) h.Outline.Stroke = new SolidColorBrush(lockAll ? Color.FromRgb(200, 120, 120) : Color.FromRgb(120, 150, 190));
            }
            else if (o is SheetItem it) it.Locked = lockAll;
        }
        RefreshViewListText();
        LoadViewProps();
        LoadItemProps();
        UpdateSelection();
        Dirty();
        SetStatus(lockAll ? $"Đã khoá {_selObjs.Count} đối tượng." : $"Đã mở khoá {_selObjs.Count} đối tượng.");
    }

    private void BtnLock_Click(object sender, RoutedEventArgs e) => ToggleLock();

    // ==================================================================
    // Căn hàng, phân bố, thứ tự
    // ==================================================================
    internal void AlignSelected(AlignMode mode)
    {
        if (_sheet == null || _selObjs.Count == 0) return;
        var objs = _selObjs.ToList();
        var rects = objs.Select(SheetLayoutMath.Bounds).ToList();
        List<(double Dx, double Dy)> d;
        if (objs.Count == 1)
        {
            // một đối tượng: căn theo vùng vẽ của tờ
            d = SheetLayoutMath.Align(new List<Rect2> { SheetBuilder.DrawingArea(_sheet), rects[0] }, mode).Skip(1).ToList();
        }
        else d = SheetLayoutMath.Align(rects, mode);
        ApplyOffsets(objs, d);
        SetStatus(objs.Count == 1 ? "Đã căn theo vùng vẽ của tờ." : $"Đã căn {objs.Count} đối tượng.");
    }

    internal void DistributeSelected(bool horizontal)
    {
        if (_sheet == null) return;
        if (_selObjs.Count < 3) { SetStatus("Phân bố đều cần chọn ít nhất 3 đối tượng."); return; }
        var objs = _selObjs.ToList();
        ApplyOffsets(objs, SheetLayoutMath.Distribute(objs.Select(SheetLayoutMath.Bounds).ToList(), horizontal));
        SetStatus($"Đã phân bố đều {objs.Count} đối tượng theo phương {(horizontal ? "ngang" : "đứng")}.");
    }

    private void ApplyOffsets(List<object> objs, List<(double Dx, double Dy)> d)
    {
        Record();
        int skipped = 0;
        for (int i = 0; i < objs.Count && i < d.Count; i++)
        {
            if (Math.Abs(d[i].Dx) + Math.Abs(d[i].Dy) < 1e-9) continue;
            if (SheetLayoutMath.IsLocked(objs[i])) { skipped++; continue; }
            SheetLayoutMath.Move(objs[i], d[i].Dx, d[i].Dy);
            if (objs[i] is SheetView v) RenderView(v);
        }
        RenderItemsHost();
        RebuildTitleBlock();
        LoadViewProps(true);
        LoadItemPos();
        UpdateSelection();
        Dirty();
        if (skipped > 0) SetStatus($"{skipped} đối tượng khoá không di chuyển.");
    }

    private void BtnAlign_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string t && Enum.TryParse(t, out AlignMode m)) AlignSelected(m);
    }

    private void BtnDistribute_Click(object sender, RoutedEventArgs e) =>
        DistributeSelected((sender as FrameworkElement)?.Tag as string != "V");

    private void ZOrder(bool front)
    {
        if (_sheet == null || _selObjs.Count == 0) return;
        Record();
        var vs = _selObjs.OfType<SheetView>().ToList();
        var its = _selObjs.OfType<SheetItem>().ToList();
        if (front) { SheetLayoutMath.BringToFront(_sheet.Views, vs); SheetLayoutMath.BringToFront(_sheet.Items, its); }
        else { SheetLayoutMath.SendToBack(_sheet.Views, vs); SheetLayoutMath.SendToBack(_sheet.Items, its); }
        RebuildPage();
        RefreshViewList();
        SyncSelection();
        Dirty();
    }

    private void BtnFront_Click(object sender, RoutedEventArgs e) => ZOrder(true);
    private void BtnBack_Click(object sender, RoutedEventArgs e) => ZOrder(false);

    // ==================================================================
    // Thuộc tính đối tượng
    // ==================================================================
    private static void ShowRow(FrameworkElement el, bool on) => el.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

    private void LoadItemPos()
    {
        var it = SelItem;
        if (it == null) return;
        _loading = true;
        bool pts = it.Kind is SheetItemKind.Line or SheetItemKind.Dim && it.Pts.Count >= 2;
        txtIX.Text = F(pts ? it.Pts[0] : it.X); txtIY.Text = F(pts ? it.Pts[1] : it.Y);
        txtIW.Text = F(it.W); txtIHgt.Text = F(it.H); txtIR.Text = F(it.R);
        txtIH.Text = F(it.TextHeight); txtIRot.Text = F(it.Rotation); txtIOffset.Text = F(it.Offset);
        _loading = false;
    }

    private void LoadItemProps()
    {
        var it = SelItem;
        grpItem.Visibility = it == null ? Visibility.Collapsed : Visibility.Visible;
        if (it == null) return;
        _loading = true;
        try
        {
            var k = it.Kind;
            txtIKind.Text = SheetBuilder.ItemKindName(k) + (it.Locked ? " (khoá)" : "");
            ShowRow(rowIText, k is SheetItemKind.Text or SheetItemKind.Table or SheetItemKind.Dim or SheetItemKind.Image);
            lblIText.Text = k == SheetItemKind.Dim ? "Chữ ghi đè" : k == SheetItemKind.Image ? "Tên ảnh" : k == SheetItemKind.Table ? "Nội dung bảng" : "Nội dung";
            txtIText.Text = k == SheetItemKind.Table ? SheetLayoutMath.TableToText(it) : (it.Text ?? "").Replace("\n", Environment.NewLine);
            txtIText.Height = k == SheetItemKind.Text || k == SheetItemKind.Table ? 78 : 24;
            ShowRow(rowIH, k is SheetItemKind.Text or SheetItemKind.Table or SheetItemKind.Dim);
            ShowRow(rowIRot, k == SheetItemKind.Text);
            ShowRow(rowIAlign, k is SheetItemKind.Text or SheetItemKind.Table);
            ShowRow(rowIWeight, k is SheetItemKind.Line or SheetItemKind.Rect or SheetItemKind.Circle or SheetItemKind.Cloud or SheetItemKind.Table);
            ShowRow(rowILType, k is SheetItemKind.Line or SheetItemKind.Rect or SheetItemKind.Circle or SheetItemKind.Cloud);
            ShowRow(rowIArrows, k == SheetItemKind.Line);
            ShowRow(rowIWH, k is SheetItemKind.Rect or SheetItemKind.Cloud or SheetItemKind.Image or SheetItemKind.Table);
            ShowRow(rowIR, k == SheetItemKind.Circle);
            ShowRow(rowIArc, k == SheetItemKind.Cloud);
            ShowRow(rowIDim, k == SheetItemKind.Dim);
            ShowRow(rowITable, k == SheetItemKind.Table);
            ShowRow(btnIImage, k == SheetItemKind.Image);
            cbIAlign.SelectedIndex = Math.Clamp(it.Align, 0, 2);
            chkIBold.IsChecked = it.Bold;
            cbILayer.Text = SheetBuilder.LayerOf(it);
            int ci = Array.FindIndex(ColorChoices, c => string.Equals(c.Hex, it.Color ?? "", StringComparison.OrdinalIgnoreCase));
            if (ci >= 0) cbIColor.SelectedIndex = ci; else { cbIColor.SelectedIndex = -1; cbIColor.Text = it.Color ?? ""; }
            cbIWeight.Text = it.LineWeight > 0 ? F2(it.LineWeight) : WeightChoices[0];
            string lt = (it.LineType ?? "").ToUpperInvariant();
            cbILType.SelectedIndex = lt == "DASHED" ? 1 : lt == "CENTER" ? 2 : 0;
            chkIArrowStart.IsChecked = it.ArrowStart; chkIArrowEnd.IsChecked = it.ArrowEnd; chkIClosed.IsChecked = it.Closed;
            txtIArc.Text = F(it.ArcLength);
            txtIDimScale.Text = F(it.DimScale);
            txtIColW.Text = it.ColWidths != null && it.ColWidths.Count > 0 ? string.Join(", ", it.ColWidths.Select(F2)) : "";
            chkIHeader.IsChecked = it.HeaderRow;
            chkILock.IsChecked = it.Locked;
        }
        finally { _loading = false; }
        LoadItemPos();
    }

    private static string F2(double v) => v.ToString("0.###", Inv);

    private string ReadColor()
    {
        string t = (cbIColor.Text ?? "").Trim();
        foreach (var (n, hex) in ColorChoices) if (string.Equals(n, t, StringComparison.OrdinalIgnoreCase)) return hex;
        if (SheetBuilder.TryParseColor(t, out byte r, out byte g, out byte b)) return $"#{r:X2}{g:X2}{b:X2}";
        return "";
    }

    internal void ApplyItemProps()
    {
        var it = SelItem;
        if (it == null || _sheet == null) return;
        Record();
        string text = (txtIText.Text ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
        switch (it.Kind)
        {
            case SheetItemKind.Table: SheetLayoutMath.TableFromText(it, text); break;
            case SheetItemKind.Text: case SheetItemKind.Dim: case SheetItemKind.Image: it.Text = text; break;
        }
        if (TryNum(txtIH.Text, out double th) && th > 0.3) it.TextHeight = Math.Min(th, 200);
        if (TryNum(txtIRot.Text, out double rot)) it.Rotation = rot;
        if (cbIAlign.SelectedIndex >= 0) it.Align = cbIAlign.SelectedIndex;
        it.Bold = chkIBold.IsChecked == true;
        string layer = (cbILayer.Text ?? "").Trim();
        it.Layer = layer.Length == 0 || layer == SheetBuilder.DefaultLayer(it.Kind) ? "" : layer.ToUpperInvariant();
        it.Color = ReadColor();
        it.LineWeight = TryNum(cbIWeight.Text, out double lw) && lw > 0 ? Math.Min(lw, 5) : 0;
        it.LineType = cbILType.SelectedIndex == 1 ? "DASHED" : cbILType.SelectedIndex == 2 ? "CENTER" : "";
        it.ArrowStart = chkIArrowStart.IsChecked == true;
        it.ArrowEnd = chkIArrowEnd.IsChecked == true;
        it.Closed = chkIClosed.IsChecked == true;
        if (TryNum(txtIX.Text, out double x) && TryNum(txtIY.Text, out double y))
        {
            if (it.Kind is SheetItemKind.Line or SheetItemKind.Dim && it.Pts.Count >= 2) SheetLayoutMath.Move(it, x - it.Pts[0], y - it.Pts[1]);
            else { it.X = x; it.Y = y; }
        }
        if (it.Kind is SheetItemKind.Rect or SheetItemKind.Cloud or SheetItemKind.Image or SheetItemKind.Table)
        {
            if (TryNum(txtIW.Text, out double w) && w > 0.5) it.W = w;
            if (TryNum(txtIHgt.Text, out double h) && h > 0.5) it.H = h;
        }
        if (TryNum(txtIR.Text, out double r) && r > 0.2) it.R = r;
        if (TryNum(txtIArc.Text, out double arc) && arc >= 1) it.ArcLength = arc;
        if (TryNum(txtIDimScale.Text, out double ds) && ds >= 0) it.DimScale = ds;
        if (TryNum(txtIOffset.Text, out double off)) it.Offset = off;
        if (it.Kind == SheetItemKind.Table)
        {
            var cw = (txtIColW.Text ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(sv => TryNum(sv, out double vv) ? vv : 0).ToList();
            it.ColWidths = cw.Count == Math.Max(1, it.Cols) && cw.All(vv => vv > 0) ? cw : new List<double>();
            it.HeaderRow = chkIHeader.IsChecked == true;
        }
        it.Locked = chkILock.IsChecked == true;
        RenderItemsHost();
        LoadItemProps();
        UpdateSelection();
        UpdateSheetListText();
        Dirty();
    }

    private void BtnApplyItem_Click(object sender, RoutedEventArgs e) => ApplyItemProps();

    private void ItemProp_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { ApplyItemProps(); e.Handled = true; }
    }

    private void ItemText_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { ApplyItemProps(); e.Handled = true; }
    }

    private void ItemCheck_Click(object sender, RoutedEventArgs e)
    {
        if (!_loading) ApplyItemProps();
    }

    private void ItemCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || SelItem == null || sender is not ComboBox cb || cb.SelectedItem == null) return;
        // ô chọn có thể gõ: cập nhật chữ theo mục vừa chọn trước khi đọc
        if (cb.IsEditable) { _loading = true; cb.Text = cb.SelectedItem.ToString(); _loading = false; }
        ApplyItemProps();
    }

    // ==================================================================
    // Ảnh
    // ==================================================================
    /// <summary>Đọc file ảnh, trả về base64 + kích thước điểm ảnh; null nếu huỷ/lỗi.</summary>
    private (string B64, string Name, int W, int H)? OpenImageFile()
    {
        var dlg = new OpenFileDialog { Filter = "Ảnh (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp", Title = "Chọn ảnh nhúng vào tờ in" };
        if (dlg.ShowDialog(this) != true) return null;
        try
        {
            var bytes = File.ReadAllBytes(dlg.FileName);
            if (bytes.Length > 8 * 1024 * 1024) { MessageBox.Show("Ảnh lớn hơn 8 MB – hãy thu nhỏ ảnh trước khi chèn.", Title, MessageBoxButton.OK, MessageBoxImage.Warning); return null; }
            string b64 = Convert.ToBase64String(bytes);
            var img = SheetRender.DecodeImage(b64);
            if (img == null) { MessageBox.Show("Không đọc được ảnh.", Title, MessageBoxButton.OK, MessageBoxImage.Error); return null; }
            return (b64, Path.GetFileName(dlg.FileName), img.PixelWidth, img.PixelHeight);
        }
        catch (Exception ex) { MessageBox.Show("Không đọc được ảnh: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); return null; }
    }

    /// <summary>Chọn ảnh cho đối tượng ảnh: giữ bề rộng, chỉnh chiều cao theo tỷ lệ ảnh (giữ mép trên).</summary>
    private bool PickImage(SheetItem it)
    {
        var r = OpenImageFile();
        if (r == null) return false;
        var (b64, name, pw, ph) = r.Value;
        it.ImageData = b64;
        it.Text = name;
        if (pw > 0 && ph > 0 && it.W > 0)
        {
            double top = it.Y + it.H;
            it.H = it.W * ph / pw;
            it.Y = top - it.H;
        }
        return true;
    }

    private void BtnItemImage_Click(object sender, RoutedEventArgs e)
    {
        var it = SelItem;
        if (it == null || it.Kind != SheetItemKind.Image) return;
        string before = Snapshot();
        if (!PickImage(it)) return;
        _undo.Push(before); _undoKey = null;
        RenderItemsHost();
        LoadItemProps();
        UpdateSelection();
        Dirty();
    }

    // ==================================================================
    // Khung tên công trình
    // ==================================================================
    private void BtnTitleBlock_Click(object sender, RoutedEventArgs e)
    {
        var tb = _p.TitleBlock ?? new TitleBlockInfo();
        var dlg = new Window
        {
            Title = "Khung tên công trình – dùng chung cho mọi tờ", Owner = this, Width = 520, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = Background
        };
        var sp = new StackPanel { Margin = new Thickness(10) };
        TextBox Field(string label, string value, string tip = null)
        {
            var dp = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            dp.Children.Add(new TextBlock { Text = label, Width = 150, VerticalAlignment = VerticalAlignment.Center, ToolTip = tip });
            var t = new TextBox { Text = value ?? "", ToolTip = tip };
            dp.Children.Add(t);
            sp.Children.Add(dp);
            return t;
        }
        var fCompany = Field("Đơn vị thiết kế", tb.Company);
        var fProject = Field("Tên công trình", tb.ProjectName, "Để trống: lấy tên dự án (" + _p.Name + ")");
        var fLoc = Field("Địa điểm", tb.Location, "Để trống: lấy địa điểm dự án");
        var fOwner = Field("Chủ đầu tư", tb.Owner);
        var fDesigner = Field("Người thiết kế", tb.Designer, "Dùng khi ô trên tờ để trống");
        var fChecker = Field("Người kiểm tra", tb.Checker, "Dùng khi ô trên tờ để trống");
        var fApprover = Field("Người duyệt", tb.Approver, "Dùng khi ô trên tờ để trống");
        var fPrefix = Field("Tiền tố số hiệu tờ", string.IsNullOrEmpty(tb.NumberPrefix) ? "KC-" : tb.NumberPrefix, "Dùng cho \"Đánh số lại\" và tờ mới");
        string logo = tb.LogoData ?? "";
        var preview = new Image { Height = 60, Width = 160, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 8, 0) };
        void ShowLogo() => preview.Source = SheetRender.DecodeImage(logo);
        ShowLogo();
        var lp = new DockPanel { Margin = new Thickness(0, 6, 0, 2) };
        lp.Children.Add(new TextBlock { Text = "Logo công ty", Width = 150, VerticalAlignment = VerticalAlignment.Center });
        lp.Children.Add(new Border { Child = preview, BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 8, 0) });
        var bPick = new Button { Content = "Chọn logo…", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
        bPick.Click += (_, _) => { var r = OpenImageFile(); if (r != null) { logo = r.Value.B64; ShowLogo(); } };
        var bClear = new Button { Content = "Xoá logo", Padding = new Thickness(8, 2, 8, 2), VerticalAlignment = VerticalAlignment.Center };
        bClear.Click += (_, _) => { logo = ""; ShowLogo(); };
        lp.Children.Add(bPick); lp.Children.Add(bClear);
        sp.Children.Add(lp);
        var chkAll = new CheckBox { Content = "Ghi người thiết kế / kiểm tra / duyệt vào mọi tờ (ghi đè ô trên tờ)", Margin = new Thickness(0, 6, 0, 0) };
        sp.Children.Add(chkAll);
        var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var ok = new Button { Content = "Đồng ý", IsDefault = true, Padding = new Thickness(14, 3, 14, 3), Margin = new Thickness(0, 0, 6, 0) };
        var cancel = new Button { Content = "Huỷ", IsCancel = true, Padding = new Thickness(14, 3, 14, 3) };
        ok.Click += (_, _) => dlg.DialogResult = true;
        bp.Children.Add(ok); bp.Children.Add(cancel);
        sp.Children.Add(bp);
        dlg.Content = sp;
        if (dlg.ShowDialog() != true) return;
        Record();
        _p.TitleBlock = new TitleBlockInfo
        {
            Company = fCompany.Text.Trim(), ProjectName = fProject.Text.Trim(), Location = fLoc.Text.Trim(), Owner = fOwner.Text.Trim(),
            Designer = fDesigner.Text.Trim(), Checker = fChecker.Text.Trim(), Approver = fApprover.Text.Trim(),
            NumberPrefix = fPrefix.Text.Trim(), LogoData = logo
        };
        if (chkAll.IsChecked == true)
            foreach (var s in _p.Sheets) { s.Designer = _p.TitleBlock.Designer; s.Checker = _p.TitleBlock.Checker; s.Approver = _p.TitleBlock.Approver; }
        Dirty();
        LoadSheetProps();
        RebuildTitleBlock();
        SetStatus("Đã cập nhật khung tên công trình.");
    }

    // ==================================================================
    // Truy cập cho tự kiểm tra
    // ==================================================================
    internal Sheet CurrentSheet => _sheet;
    internal int UndoCount => _undo.UndoCount;
    internal int SelectionCount => _selObjs.Count;

    /// <summary>Dựng lại toàn bộ trang và bảng thuộc tính (sau khi sửa dữ liệu bằng mã).</summary>
    internal void RefreshPage()
    {
        RebuildPage();
        RefreshViewList();
        SyncSelection();
        UpdateSheetListText();
    }

    /// <summary>Đặt thông tin khung tên công trình bằng mã (ghi hoàn tác).</summary>
    internal void SetTitleBlock(TitleBlockInfo info)
    {
        Record();
        _p.TitleBlock = info ?? new TitleBlockInfo();
        RebuildTitleBlock();
        Dirty();
    }

    /// <summary>Chọn mọi đối tượng trên tờ hiện tại.</summary>
    internal void SelectAll()
    {
        _selObjs.Clear();
        _selObjs.AddRange(AllObjects());
        SyncSelection();
    }

    /// <summary>Sửa khung nhìn bằng mã (ghi hoàn tác) rồi vẽ lại.</summary>
    internal void EditView(SheetView v, Action<SheetView> edit)
    {
        if (v == null || edit == null) return;
        Record();
        edit(v);
        if (_hosts.ContainsKey(v)) RenderView(v);
        RebuildTitleBlock();
        RefreshViewListText();
        LoadViewProps();
        UpdateSelection();
        Dirty();
    }
}

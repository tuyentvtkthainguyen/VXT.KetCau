using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using DPlaced = KetCauVXT.Detailing.PlacedFigure;

namespace KetCauVXT.App;

// =====================================================================
// BẢN VẼ KIỂU REVIT trong cửa sổ Triển khai hồ sơ:
//  · Thẻ dọc bên trái: "Cấu kiện" (cây cấu kiện cũ) | "Trình duyệt" (Khung nhìn theo mặt bằng kết cấu, Bảng thống kê, Bản vẽ)
//    + bảng THUỘC TÍNH theo đối tượng đang chọn (bản vẽ, khung nhìn trên tờ, khung nhìn, dự án – cỡ chữ in).
//  · Thẻ "Bản vẽ (sheet)": tờ giấy + khung tên; KÉO khung nhìn từ trình duyệt thả vào tờ; kéo dời; Delete gỡ khung;
//    kích đúp khung nhìn → mở cấu kiện / mặt bằng để sửa; Esc hoặc "↩ Quay lại bản vẽ" trở về tờ.
//  · Tạo bản vẽ mới: số hiệu nhảy tiếp, người dùng đặt tên; DANH MỤC BẢN VẼ sinh tự động.
// =====================================================================
public sealed partial class DetailingWindow
{
    private const string ViewDragFormat = "VietConst.ViewId";
    private TabControl _leftTabs;
    private readonly TreeView _browser = new() { BorderThickness = new Thickness(0) };
    private readonly StackPanel _props = new() { Margin = new Thickness(6, 4, 6, 8) };
    private readonly Canvas _sheetCanvas = new() { Background = new SolidColorBrush(Color.FromRgb(120, 124, 130)), AllowDrop = true, Focusable = true };
    private readonly ScrollViewer _sheetScroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock _sheetHead = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
    private Button _btnBack;
    private TabItem _sheetTab;
    private List<SheetFigure> _vcat = new();
    private Dictionary<string, SheetFigure> _vmap = new();
    private readonly Dictionary<string, System.Windows.Media.Drawing> _vDraw = new();
    private DetailSheet _curSheet, _returnSheet;
    private string _selVp;
    private double _sk = 1, _sZoom = 1;
    private Border _vDrag;
    private Point _vDragStart;
    private double _vDragX0, _vDragY0;
    private Point _browserDown;

    private DetailSheetSet VSet => _prj.Sheets ??= new DetailSheetSet();

    // ------------------------------------------------------------------ dựng giao diện
    /// <summary>Bọc khung cây cấu kiện vào thẻ dọc "Cấu kiện | Trình duyệt" (trả về phần tử đặt vào cột trái).</summary>
    private FrameworkElement WrapLeftWithBrowser(FrameworkElement left)
    {
        _leftTabs = new TabControl { TabStripPlacement = Dock.Left, Padding = new Thickness(0), BorderThickness = new Thickness(0) };
        TabItem Vtab(string header, object content, string tip) => new()
        {
            Header = new TextBlock { Text = header, LayoutTransform = new RotateTransform(-90), Margin = new Thickness(0, 4, 0, 4) },
            Content = content, ToolTip = tip, Padding = new Thickness(2, 6, 2, 6)
        };
        var br = new Grid();
        br.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star), MinHeight = 120 });
        br.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        br.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star), MinHeight = 100 });
        var top = new DockPanel();
        var bh = new TextBlock { Text = "TRÌNH DUYỆT DỰ ÁN", FontWeight = FontWeights.Bold, Margin = new Thickness(8, 6, 8, 4) };
        DockPanel.SetDock(bh, Dock.Top); top.Children.Add(bh);
        var bb = new WrapPanel { Margin = new Thickness(6, 2, 6, 4) };
        Button Sm(string t, Action a, string tip) { var b = new Button { Content = t, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(6, 1, 6, 1), ToolTip = tip }; b.Click += (s, e) => a(); return b; }
        bb.Children.Add(Sm("＋ Bản vẽ", NewSheetPrompt, "Tạo bản vẽ (sheet) mới – số hiệu tự nhảy tiếp"));
        bb.Children.Add(Sm("⟳", () => RebuildViews(true), "Cập nhật khung nhìn theo cấu kiện hiện tại"));
        bb.Children.Add(Sm("Tự dàn trang", AutoLayoutViews, "Xếp các khung nhìn chưa đặt vào tờ (tờ đã khoá giữ nguyên)"));
        DockPanel.SetDock(bb, Dock.Bottom); top.Children.Add(bb);
        top.Children.Add(_browser);
        br.Children.Add(top);
        var gs = new GridSplitter { Height = 4, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Gainsboro };
        Grid.SetRow(gs, 1); br.Children.Add(gs);
        var pd = new DockPanel();
        var ph = new TextBlock { Text = "THUỘC TÍNH", FontWeight = FontWeights.Bold, Margin = new Thickness(8, 4, 8, 2) };
        DockPanel.SetDock(ph, Dock.Top); pd.Children.Add(ph);
        pd.Children.Add(new ScrollViewer { Content = _props, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Grid.SetRow(pd, 2); br.Children.Add(pd);

        _leftTabs.Items.Add(Vtab("Cấu kiện", left, "Cây cấu kiện hồ sơ theo mặt bằng kết cấu"));
        _leftTabs.Items.Add(Vtab("Trình duyệt", br, "Trình duyệt dự án kiểu Revit: khung nhìn, bảng thống kê, bản vẽ (sheet) + thuộc tính"));
        _leftTabs.SelectionChanged += (s, e) => { if (e.Source == _leftTabs && _leftTabs.SelectedIndex == 1) { RebuildViews(false); ShowProps(null); } };

        _browser.SelectedItemChanged += (s, e) => { if (_browser.SelectedItem is TreeViewItem ti) ShowProps(ti.Tag); };
        _browser.PreviewMouseLeftButtonDown += (s, e) => _browserDown = e.GetPosition(_browser);
        _browser.PreviewMouseMove += Browser_MouseMove;
        _browser.MouseDoubleClick += (s, e) =>
        {
            if (_browser.SelectedItem is not TreeViewItem ti) return;
            if (ti.Tag is DetailSheet sh) { OpenSheet(sh); e.Handled = true; }
            else if (ti.Tag is SheetFigure f) { ActivateView(f); e.Handled = true; }
        };
        _browser.KeyDown += (s, e) =>
        {
            if (e.Key == Key.F2 && _browser.SelectedItem is TreeViewItem t && t.Tag is DetailSheet sh) { RenameSheetPrompt(sh); e.Handled = true; }
        };
        return _leftTabs;
    }

    /// <summary>Thẻ "Bản vẽ (sheet)" ở vùng giữa / phải.</summary>
    private void InitSheetsTab()
    {
        var dp = new DockPanel();
        var bar = new WrapPanel { Margin = new Thickness(4) };
        Button B(string t, Action a, string tip) { var b = new Button { Content = t, Margin = new Thickness(0, 0, 4, 2), Padding = new Thickness(6, 1, 6, 1), ToolTip = tip }; b.Click += (s, e) => a(); bar.Children.Add(b); return b; }
        B("＋ Bản vẽ mới", NewSheetPrompt, "Tạo bản vẽ mới (số hiệu nhảy tiếp)");
        B("Thu nhỏ −", () => { _sZoom /= 1.25; RenderSheet(); }, "");
        B("Phóng to +", () => { _sZoom *= 1.25; RenderSheet(); }, "");
        B("Vừa khung", () => { _sZoom = 1; RenderSheet(); }, "");
        B("Gỡ khung nhìn", RemoveSelectedViewport, "Gỡ khung nhìn đang chọn khỏi tờ (Delete)");
        _btnBack = B("↩ Quay lại bản vẽ", BackToSheet, "Trở về tờ đang dàn sau khi sửa cấu kiện (Esc)");
        _btnBack.IsEnabled = false;
        B("Dàn trang / In / PDF…", OpenSheetLayout, "Cửa sổ dàn trang đầy đủ: khổ giấy, khung tên, đánh số, xuất PDF, in");
        bar.Children.Add(_sheetHead);
        DockPanel.SetDock(bar, Dock.Top); dp.Children.Add(bar);
        _sheetScroll.Content = _sheetCanvas;
        dp.Children.Add(_sheetScroll);
        _sheetTab = new TabItem { Header = "Bản vẽ (sheet)", Content = dp };
        _tabs.Items.Add(_sheetTab);
        _tabs.SelectionChanged += (s, e) => { if (e.Source == _tabs && ReferenceEquals(_tabs.SelectedItem, _sheetTab)) { RebuildViews(false); RenderSheet(); } };
        _sheetScroll.SizeChanged += (s, e) => { if (ReferenceEquals(_tabs.SelectedItem, _sheetTab)) RenderSheet(); };
        _sheetCanvas.DragOver += (s, e) => { e.Effects = e.Data.GetDataPresent(ViewDragFormat) && _curSheet != null ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        _sheetCanvas.Drop += SheetCanvas_Drop;
        _sheetCanvas.MouseMove += SheetCanvas_MouseMove;
        _sheetCanvas.MouseLeftButtonUp += SheetCanvas_MouseUp;
        _sheetCanvas.MouseLeftButtonDown += (s, e) => { if (e.OriginalSource == _sheetCanvas || e.OriginalSource is DrawingHost h && h.Tag == null) { _selVp = null; ShowProps(_curSheet); RenderSheet(); } _sheetCanvas.Focus(); };
        PreviewKeyDown += SheetsKey;
    }

    private void SheetsKey(object sender, KeyEventArgs e)
    {
        bool typing = Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is DataGridCell;
        if (typing) return;
        if (e.Key == Key.Escape && _returnSheet != null) { BackToSheet(); e.Handled = true; }
        else if (e.Key == Key.Delete && ReferenceEquals(_tabs.SelectedItem, _sheetTab) && _selVp != null) { RemoveSelectedViewport(); e.Handled = true; }
    }

    // ------------------------------------------------------------------ khung nhìn
    /// <summary>Lập lại danh mục khung nhìn từ cấu kiện hiện tại (force = bỏ bộ đệm hình).</summary>
    private void RebuildViews(bool force)
    {
        try
        {
            if (force || _vcat.Count == 0 || _viewsStale)
            {
                var log = new List<string>();
                _vcat = SheetViews.WithIndexView(SheetLayoutEngine.Catalog(_prj, Results(), log, VSet), VSet);
                _vmap = _vcat.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());
                _vDraw.Clear();
                _viewsStale = false;
                if (log.Count > 0) SetStatus(string.Join(" ", log.Take(3)));
            }
            FillBrowser();
        }
        catch (Exception ex) { SetStatus("Không lập được khung nhìn: " + ex.Message); }
    }

    private bool _viewsStale = true;
    /// <summary>Đánh dấu khung nhìn cần vẽ lại (cấu kiện / cỡ chữ thay đổi).</summary>
    private void ViewsChanged() { _viewsStale = true; }

    private void FillBrowser()
    {
        object sel = (_browser.SelectedItem as TreeViewItem)?.Tag;
        _browser.Items.Clear();
        TreeViewItem Node(string h, object tag, bool bold = false) => new() { Header = h, Tag = tag, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal };
        var views = Node("Khung nhìn (Views)", "views", true); views.IsExpanded = true;
        foreach (var g in SheetViews.Groups(_vcat))
        {
            if (g.Kind == "schedule") continue;
            var gn = Node((g.Kind == "plan" ? "▦ " : "▢ ") + g.Title, null, true);
            foreach (var f in g.Views) gn.Items.Add(ViewNode(f));
            views.Items.Add(gn);
        }
        _browser.Items.Add(views);
        var sch = Node("Bảng thống kê (Schedules)", "schedules", true);
        foreach (var f in _vcat.Where(SheetViews.IsSchedule)) sch.Items.Add(ViewNode(f));
        _browser.Items.Add(sch);
        var sheets = Node("Bản vẽ (Sheets)", "sheets", true); sheets.IsExpanded = true;
        var cm = new ContextMenu();
        var mi = new MenuItem { Header = "Tạo bản vẽ mới…" }; mi.Click += (s, e) => NewSheetPrompt(); cm.Items.Add(mi);
        var mr = new MenuItem { Header = "Đánh số lại liên tục" }; mr.Click += (s, e) => { SheetViews.RenumberSequential(VSet); Touch(); }; cm.Items.Add(mr);
        sheets.ContextMenu = cm;
        foreach (var sh in VSet.Sheets)
        {
            var n = Node(SheetLabel(sh), sh);
            if (ReferenceEquals(sh, _curSheet)) n.FontWeight = FontWeights.SemiBold;
            foreach (var pf in sh.Figures)
                n.Items.Add(new TreeViewItem { Header = "   " + (pf.Id == SheetViews.IndexViewId ? SheetViews.IndexCaption : _vmap.TryGetValue(pf.Id, out var vf) ? vf.Caption : pf.Id), Tag = pf, Foreground = Brushes.DimGray });
            var c2 = new ContextMenu();
            void Mi(string h, Action a) { var x = new MenuItem { Header = h }; x.Click += (s, e) => a(); c2.Items.Add(x); }
            var shc = sh;
            Mi("Mở bản vẽ", () => OpenSheet(shc));
            Mi("Đổi tên… (F2)", () => RenameSheetPrompt(shc));
            Mi("Xoá bản vẽ", () => DeleteSheet(shc));
            n.ContextMenu = c2;
            sheets.Items.Add(n);
        }
        _browser.Items.Add(sheets);
        if (sel != null) Reselect(_browser.Items, sel);
    }

    private static string SheetLabel(DetailSheet sh) => sh.No + " – " + (string.IsNullOrWhiteSpace(sh.Title) ? "(chưa đặt tên)" : sh.Title);

    private TreeViewItem ViewNode(SheetFigure f)
    {
        var on = SheetViews.SheetOf(VSet, f.Id);
        bool sched = SheetViews.IsSchedule(f);
        return new TreeViewItem
        {
            Header = (on != null && !sched ? "■ " : "□ ") + f.Caption + (on != null ? "   [" + on.No + "]" : ""),
            Tag = f, Foreground = on != null && !sched ? Brushes.DimGray : Brushes.Black,
            ToolTip = on != null && !sched ? "Đã đặt trên bản vẽ " + on.No + " (mỗi khung nhìn chỉ trên một tờ – kéo sang tờ khác sẽ chuyển tờ)" : "Kéo thả vào bản vẽ đang mở · kích đúp để sửa"
        };
    }

    private static bool Reselect(ItemCollection items, object tag)
    {
        foreach (TreeViewItem ti in items)
        {
            if (Equals(ti.Tag, tag)) { ti.IsSelected = true; ti.BringIntoView(); return true; }
            if (Reselect(ti.Items, tag)) { ti.IsExpanded = true; return true; }
        }
        return false;
    }

    private void Browser_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(_browser);
        if (Math.Abs(p.X - _browserDown.X) < 6 && Math.Abs(p.Y - _browserDown.Y) < 6) return;
        if (_browser.SelectedItem is not TreeViewItem ti || ti.Tag is not SheetFigure f) return;
        if (_curSheet == null) { SetStatus("Mở một bản vẽ (kích đúp trong mục Bản vẽ) rồi kéo khung nhìn vào tờ."); return; }
        if (!ReferenceEquals(_tabs.SelectedItem, _sheetTab)) _tabs.SelectedItem = _sheetTab;
        DragDrop.DoDragDrop(_browser, new DataObject(ViewDragFormat, f.Id), DragDropEffects.Copy);
    }

    // ------------------------------------------------------------------ bản vẽ
    private void NewSheetPrompt()
    {
        string no = SheetViews.NextNo(VSet);
        string name = DialogUi.InputBox(this, "Tạo bản vẽ mới", "Số hiệu: " + no + "\nTên bản vẽ:", "MẶT BẰNG – CHI TIẾT");
        if (name == null) return;
        var sh = SheetViews.CreateSheet(VSet, name);
        Touch();
        OpenSheet(sh);
        SetStatus("Đã tạo bản vẽ " + sh.No + " – kéo khung nhìn từ Trình duyệt thả vào tờ.");
    }

    private void RenameSheetPrompt(DetailSheet sh)
    {
        string name = DialogUi.InputBox(this, "Đổi tên bản vẽ", "Tên bản vẽ " + sh.No + ":", sh.Title ?? "");
        if (name == null) return;
        SheetViews.Rename(sh, name);
        Touch();
    }

    private void DeleteSheet(DetailSheet sh)
    {
        if (MessageBox.Show(this, "Xoá bản vẽ " + SheetLabel(sh) + "? Các khung nhìn trên tờ trở về danh sách chưa đặt.", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        SheetViews.DeleteSheet(VSet, sh);
        if (ReferenceEquals(_curSheet, sh)) _curSheet = VSet.Sheets.FirstOrDefault();
        Touch();
    }

    private void AutoLayoutViews()
    {
        RebuildViews(false);
        var log = new List<string>();
        var real = _vcat.Where(f => f.Id != SheetViews.IndexViewId).ToList();
        SheetLayoutEngine.AutoLayout(VSet, real, log);
        _curSheet ??= VSet.Sheets.FirstOrDefault();
        Touch();
        SetStatus("Đã dàn " + VSet.Sheets.Count + " bản vẽ." + (log.Count > 0 ? " " + string.Join(" ", log.Take(3)) : ""));
    }

    /// <summary>Đã sửa bộ bản vẽ: lưu cờ, làm mới trình duyệt và tờ.</summary>
    private void Touch()
    {
        _dirty = true; UpdateTitle();
        // danh mục bản vẽ phụ thuộc danh sách tờ → vẽ lại khung nhìn danh mục
        if (_vmap.TryGetValue(SheetViews.IndexViewId, out var idx)) { idx.F = new Figure(SheetViews.IndexTable(VSet, _vmap), SheetViews.IndexCaption); _vDraw.Remove(SheetViews.IndexViewId + "@0@1"); }
        FillBrowser();
        if (ReferenceEquals(_tabs.SelectedItem, _sheetTab)) RenderSheet();
    }

    private void OpenSheet(DetailSheet sh)
    {
        _curSheet = sh; _selVp = null; _returnSheet = null; if (_btnBack != null) _btnBack.IsEnabled = false;
        if (!ReferenceEquals(_tabs.SelectedItem, _sheetTab)) _tabs.SelectedItem = _sheetTab;
        RebuildViews(false);
        RenderSheet();
        ShowProps(sh);
    }

    private System.Windows.Media.Drawing ViewDrawing(SheetFigure f, DPlaced pf)
    {
        string key = f.Id + "@" + pf.Scale.ToString(CultureInfo.InvariantCulture) + "@" + (pf.TextFactor > 0 ? pf.TextFactor : 1).ToString(CultureInfo.InvariantCulture);
        if (_vDraw.TryGetValue(key, out var d)) return d;
        var fw = f.At(pf.Scale, pf.TextFactor).W;
        var e = fw.Extents;
        double k = 1.0 / Math.Max(fw.Scale, 1e-9);
        d = SheetRender.Render(new[] { (fw, k, -e.X1 * k, -e.Y1 * k) }, 0, f.HAt(pf.Scale, pf.TextFactor), 0.1, x => x.GetPrimitives());
        _vDraw[key] = d;
        return d;
    }

    private void RenderSheet()
    {
        _sheetCanvas.Children.Clear();
        var sh = _curSheet;
        if (sh == null || !VSet.Sheets.Contains(sh))
        {
            _curSheet = null;
            _sheetHead.Text = "Chưa mở bản vẽ – tạo bản vẽ mới hoặc kích đúp một bản vẽ trong Trình duyệt (thẻ dọc bên trái).";
            _sheetCanvas.Width = _sheetCanvas.Height = 0;
            return;
        }
        _sheetHead.Text = SheetLabel(sh) + " · " + sh.Paper + (sh.Portrait ? " đứng" : " ngang") + " · " + sh.Figures.Count + " khung nhìn";
        var (pw, ph) = sh.Size;
        double availW = Math.Max(200, _sheetScroll.ActualWidth - 40), availH = Math.Max(200, _sheetScroll.ActualHeight - 40);
        _sk = Math.Min(availW / pw, availH / ph) * _sZoom;
        _sheetCanvas.Width = pw * _sk + 40; _sheetCanvas.Height = ph * _sk + 40;
        // nền: khung + khung tên (không hình)
        System.Windows.Media.Drawing bgd = null;
        try
        {
            var empty = new DetailSheet { No = sh.No, Title = sh.Title, Paper = sh.Paper, Portrait = sh.Portrait, IsIndex = sh.IsIndex };
            var w = SheetLayoutEngine.Compose(_prj, VSet, empty, _vmap);
            var dg = new DrawingGroup();
            using (var dc = dg.Open())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, pw, ph));
                dc.DrawDrawing(SheetRender.Render(new[] { (w, 1.0 / w.Scale, 0.0, 0.0) }, 0, ph, 0.1, x => x.GetPrimitives()));
            }
            dg.Freeze(); bgd = dg;
        }
        catch (Exception ex) { SetStatus("Không vẽ được khung tên: " + ex.Message); }
        var bg = new DrawingHost { Drawing = bgd, Scale = _sk, Width = pw * _sk, Height = ph * _sk, Background = Brushes.White };
        Canvas.SetLeft(bg, 20); Canvas.SetTop(bg, 20);
        _sheetCanvas.Children.Add(bg);
        var area = SheetGeometry.Drawable(sh.Paper, sh.Portrait);
        var tz = SheetLayoutEngine.TitleZone(VSet, sh);
        var rects = new List<(DPlaced Pf, Rect2 R)>();
        foreach (var pf in sh.Figures)
        {
            if (!_vmap.TryGetValue(pf.Id, out var f)) continue;
            double w = f.WAt(pf.Scale, pf.TextFactor), h = f.HAt(pf.Scale, pf.TextFactor);
            rects.Add((pf, new Rect2(pf.X, pf.Y, pf.X + w, pf.Y + h)));
        }
        foreach (var (pf, r) in rects)
        {
            var f = _vmap[pf.Id];
            bool bad = rects.Any(o => !ReferenceEquals(o.Pf, pf) && SheetGeometry.Overlap(o.R, r)) || SheetGeometry.Overlap(r, tz)
                       || r.X1 < area.X1 - 9 || r.Y1 < area.Y1 - 9 || r.X2 > area.X2 + 9 || r.Y2 > area.Y2 + 9;
            System.Windows.Media.Drawing d = null;
            try { d = ViewDrawing(f, pf); } catch (Exception ex) { SetStatus("Không vẽ được " + f.Caption + ": " + ex.Message); }
            var host = new DrawingHost { Drawing = d, Scale = _sk, Width = r.W * _sk, Height = r.H * _sk, Background = Brushes.Transparent, Tag = pf };
            bool sel = pf.Id == _selVp;
            var bd = new Border
            {
                Child = host, Tag = pf, Cursor = Cursors.SizeAll, ToolTip = SheetViews.ViewportTitle(pf, f) + "\nKéo để dời · kích đúp để sửa · Delete để gỡ",
                BorderThickness = new Thickness(sel || bad ? 1.5 : 0.6),
                BorderBrush = bad ? Brushes.Red : sel ? Brushes.DodgerBlue : new SolidColorBrush(Color.FromArgb(70, 30, 110, 200))
            };
            Canvas.SetLeft(bd, 20 + pf.X * _sk); Canvas.SetTop(bd, 20 + (ph - pf.Y - r.H) * _sk);
            bd.MouseLeftButtonDown += Viewport_MouseDown;
            _sheetCanvas.Children.Add(bd);
            if (pf.ShowTitle)
            {
                var tt = new TextBlock { Text = SheetViews.ViewportTitle(pf, f), FontSize = Math.Max(8, 3.5 * _sk), FontWeight = FontWeights.SemiBold, IsHitTestVisible = false, TextDecorations = TextDecorations.Underline };
                Canvas.SetLeft(tt, 20 + pf.X * _sk); Canvas.SetTop(tt, 20 + (ph - pf.Y) * _sk + 2);
                _sheetCanvas.Children.Add(tt);
            }
        }
    }

    private void SheetCanvas_Drop(object sender, DragEventArgs e)
    {
        if (_curSheet == null || !e.Data.GetDataPresent(ViewDragFormat)) return;
        string id = e.Data.GetData(ViewDragFormat) as string;
        if (id == null || !_vmap.TryGetValue(id, out var f)) return;
        var p = e.GetPosition(_sheetCanvas);
        var (_, ph) = _curSheet.Size;
        double x = (p.X - 20) / _sk, y = ph - (p.Y - 20) / _sk;
        var old = SheetViews.SheetOf(VSet, id);
        var pf = SheetViews.PlaceView(VSet, _curSheet, f, _vmap, x, y);
        if (pf == null) { SetStatus("Không đặt được khung nhìn."); return; }
        _selVp = pf.Id;
        Touch();
        ShowProps(pf);
        SetStatus("Đã đặt \"" + f.Caption + "\" lên " + _curSheet.No + (old != null && !ReferenceEquals(old, _curSheet) && !SheetViews.IsSchedule(f) ? " (chuyển từ " + old.No + " – mỗi khung nhìn chỉ trên một tờ)" : "") + ".");
    }

    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border bd || bd.Tag is not DPlaced pf) return;
        _selVp = pf.Id;
        if (e.ClickCount >= 2 && _vmap.TryGetValue(pf.Id, out var f)) { e.Handled = true; ActivateView(f); return; }
        foreach (var c in _sheetCanvas.Children.OfType<Border>()) c.BorderThickness = new Thickness(ReferenceEquals(c, bd) ? 1.5 : 0.6);
        bd.BorderBrush = Brushes.DodgerBlue;
        _vDrag = bd; _vDragStart = e.GetPosition(_sheetCanvas); _vDragX0 = Canvas.GetLeft(bd); _vDragY0 = Canvas.GetTop(bd);
        _sheetCanvas.CaptureMouse();
        ShowProps(pf);
        e.Handled = true;
    }

    private void SheetCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_vDrag == null || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(_sheetCanvas);
        Canvas.SetLeft(_vDrag, _vDragX0 + p.X - _vDragStart.X);
        Canvas.SetTop(_vDrag, _vDragY0 + p.Y - _vDragStart.Y);
    }

    private void SheetCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_vDrag == null) return;
        _sheetCanvas.ReleaseMouseCapture();
        var bd = _vDrag; _vDrag = null;
        var sh = _curSheet;
        if (sh == null || bd.Tag is not DPlaced pf || !_vmap.TryGetValue(pf.Id, out var f)) return;
        var p = e.GetPosition(_sheetCanvas);
        if (Math.Abs(p.X - _vDragStart.X) < 2 && Math.Abs(p.Y - _vDragStart.Y) < 2) return;
        var (pw, ph) = sh.Size;
        double w = f.WAt(pf.Scale, pf.TextFactor), h = f.HAt(pf.Scale, pf.TextFactor);
        double x = (Canvas.GetLeft(bd) - 20) / _sk, top = (Canvas.GetTop(bd) - 20) / _sk;
        // bắt thẳng hàng với khung nhìn khác (≤ 3 mm): cạnh trái / cạnh dưới
        foreach (var o in sh.Figures.Where(q => !ReferenceEquals(q, pf)))
        {
            if (Math.Abs(o.X - x) < 3) x = o.X;
            if (Math.Abs(o.Y - (ph - top - h)) < 3) top = ph - o.Y - h;
        }
        pf.X = Math.Round(Math.Clamp(x, 0, Math.Max(0, pw - w)), 1);
        pf.Y = Math.Round(Math.Clamp(ph - top - h, 0, Math.Max(0, ph - h)), 1);
        sh.Locked = true;
        Touch();
        ShowProps(pf);
    }

    private void RemoveSelectedViewport()
    {
        if (_curSheet == null || _selVp == null) return;
        if (SheetViews.RemoveView(_curSheet, _selVp)) { _selVp = null; Touch(); ShowProps(_curSheet); SetStatus("Đã gỡ khung nhìn – trở về danh sách chưa đặt."); }
    }

    // ------------------------------------------------------------------ kích đúp: sửa cấu kiện / mặt bằng
    private void ActivateView(SheetFigure f)
    {
        var t = SheetViews.TargetOf(f);
        if (t == null) return;
        _returnSheet = ReferenceEquals(_tabs.SelectedItem, _sheetTab) ? _curSheet : _returnSheet;
        if (t.Kind == "member")
        {
            var m = SheetViews.MemberOf(_prj, t);
            if (m == null) { SetStatus("Không tìm thấy cấu kiện " + t.Name + "."); return; }
            if (_leftTabs != null) _leftTabs.SelectedIndex = 0;
            RefreshTree(m); ShowMember(m);
            _tabs.SelectedIndex = 2;   // bản vẽ xem trước của cấu kiện
            ViewsChanged();
            SetStatus("Đang sửa " + t.MemberKind + " " + m.Name + " – sửa thông số bên trái; xong bấm \"↩ Quay lại bản vẽ\" hoặc Esc để trở về tờ.");
        }
        else if (t.Kind == "plan")
        {
            _tabs.SelectedIndex = 4;
            _plan?.ShowLevelByCaption(t.Name);
            ViewsChanged();
            SetStatus("Đang sửa " + t.Name + " – xong bấm Esc để trở về bản vẽ.");
        }
        else { SetStatus(t.Name + ": bảng tự sinh theo hồ sơ (không sửa trực tiếp)."); return; }
        if (_btnBack != null) _btnBack.IsEnabled = _returnSheet != null;
    }

    private void BackToSheet()
    {
        var sh = _returnSheet ?? _curSheet;
        _returnSheet = null;
        if (_btnBack != null) _btnBack.IsEnabled = false;
        ViewsChanged();
        if (sh != null) OpenSheet(sh); else { _tabs.SelectedItem = _sheetTab; RebuildViews(true); RenderSheet(); }
    }

    // ------------------------------------------------------------------ bảng thuộc tính
    private void ShowProps(object o)
    {
        _props.Children.Clear();
        void Head(string t) => _props.Children.Add(new TextBlock { Text = t, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 3), Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x5F, 0xA8)) });
        void Info(string label, string v) => _props.Children.Add(Row(label, new TextBlock { Text = v ?? "", TextWrapping = TextWrapping.Wrap }));
        void Edit(string label, string v, Action<string> set)
        {
            var tb = new TextBox { Text = v ?? "" };
            tb.LostKeyboardFocus += (s, e) => { if (tb.Text != (v ?? "")) { set(tb.Text); v = tb.Text; } };
            tb.KeyDown += (s, e) => { if (e.Key == Key.Enter) { set(tb.Text); v = tb.Text; e.Handled = true; } };
            _props.Children.Add(Row(label, tb));
        }
        void Combo(string label, IEnumerable<string> items, string cur, Action<string> set)
        {
            var cb = new ComboBox { ItemsSource = items.ToList(), SelectedItem = cur, IsEditable = false };
            cb.SelectionChanged += (s, e) => { if (cb.SelectedItem is string x && x != cur) { cur = x; set(x); } };
            _props.Children.Add(Row(label, cb));
        }
        void Check(string label, bool cur, Action<bool> set)
        {
            var c = new CheckBox { IsChecked = cur, VerticalAlignment = VerticalAlignment.Center };
            c.Click += (s, e) => set(c.IsChecked == true);
            _props.Children.Add(Row(label, c));
        }
        void Btn(string text, Action a) { var b = new Button { Content = text, Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(6, 2, 6, 2), HorizontalAlignment = HorizontalAlignment.Left }; b.Click += (s, e) => a(); _props.Children.Add(b); }
        var scales = new[] { "Theo hồ sơ" }.Concat(SheetGeometry.StdScales.Select(x => "1/" + x)).ToList();
        var factors = new[] { "0.7", "0.8", "0.9", "1", "1.2", "1.5", "2" };

        switch (o)
        {
            case DetailSheet sh:
                Head("BẢN VẼ (SHEET)");
                Edit("Số hiệu", sh.No, v => { SheetViews.Renumber(sh, v); Touch(); });
                Edit("Tên bản vẽ", sh.Title, v => { SheetViews.Rename(sh, v); Touch(); });
                Combo("Khổ giấy", SheetGeometry.Papers, sh.Paper, v => { sh.Paper = v; sh.Locked = true; Touch(); });
                Check("Khổ đứng", sh.Portrait, v => { sh.Portrait = v; sh.Locked = true; Touch(); });
                Check("Khoá tờ (giữ khi tự dàn)", sh.Locked, v => { sh.Locked = v; _dirty = true; UpdateTitle(); });
                Edit("Ngày (khung tên)", VSet.Date, v => { VSet.Date = v; Touch(); });
                Info("Khung nhìn", sh.Figures.Count + " · tỷ lệ " + SheetLayoutEngine.ScaleText(sh, _vmap));
                Btn("Mở bản vẽ", () => OpenSheet(sh));
                ProjectProps(Head, Edit);
                break;
            case DPlaced pf:
                var sht = VSet.Sheets.FirstOrDefault(s => s.Figures.Contains(pf));
                _vmap.TryGetValue(pf.Id, out var vf);
                Head("KHUNG NHÌN TRÊN BẢN VẼ");
                Info("Hình", vf?.Caption ?? pf.Id);
                Info("Bản vẽ", sht != null ? SheetLabel(sht) : "");
                Edit("Tên khung nhìn", pf.ViewTitle, v => { pf.ViewTitle = v; Touch(); });
                Check("Hiện tiêu đề (tên + tỷ lệ)", pf.ShowTitle, v => { pf.ShowTitle = v; Touch(); });
                if (vf != null && vf.CanRescale)
                    Combo("Tỷ lệ", scales, pf.Scale > 0 ? "1/" + Math.Round(vf.ScaleAt(pf.Scale)) : "Theo hồ sơ", v =>
                    {
                        double sc = v.StartsWith("1/") && double.TryParse(v.Substring(2), NumberStyles.Float, CultureInfo.InvariantCulture, out double q) ? q : vf.BaseScale;
                        SheetViews.SetViewportScale(sht, pf, vf, sc); Touch();
                    });
                Combo("Cỡ chữ (hệ số)", factors, (pf.TextFactor > 0 ? pf.TextFactor : 1).ToString("0.##", CultureInfo.InvariantCulture), v =>
                {
                    pf.TextFactor = double.Parse(v, CultureInfo.InvariantCulture); if (Math.Abs(pf.TextFactor - 1) < 1e-9) pf.TextFactor = 0; Touch();
                });
                Edit("Vị trí X (mm)", pf.X.ToString("0.#", CultureInfo.InvariantCulture), v => { if (double.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double q)) { pf.X = q; Touch(); } });
                Edit("Vị trí Y (mm)", pf.Y.ToString("0.#", CultureInfo.InvariantCulture), v => { if (double.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double q)) { pf.Y = q; Touch(); } });
                if (vf != null) Btn("✎ Sửa cấu kiện / mặt bằng (kích đúp)", () => ActivateView(vf));
                Btn("Gỡ khỏi bản vẽ (Delete)", () => { if (sht != null && SheetViews.RemoveView(sht, pf.Id)) { _selVp = null; Touch(); ShowProps(sht); } });
                if (vf != null) MemberProps(vf, Head, Edit, Info);
                break;
            case SheetFigure f:
                var on = SheetViews.SheetOf(VSet, f.Id);
                Head(SheetViews.IsSchedule(f) ? "BẢNG THỐNG KÊ" : "KHUNG NHÌN");
                Info("Tên", f.Caption);
                Info("Mục", f.SectionTitle);
                Info("Tỷ lệ gốc", f.BaseScale > 1.0001 ? "1/" + Math.Round(f.BaseScale) : "1/1 (bảng)");
                Info("Đặt trên bản vẽ", on != null ? SheetLabel(on) : "(chưa đặt)");
                if (on != null) Btn("Mở bản vẽ chứa khung nhìn", () => { _selVp = f.Id; OpenSheet(on); });
                if (_curSheet != null) Btn("Đặt vào bản vẽ đang mở (" + _curSheet.No + ")", () =>
                {
                    var p = SheetViews.PlaceView(VSet, _curSheet, f, _vmap);
                    if (p == null) SetStatus("Bản vẽ " + _curSheet.No + " không còn chỗ trống – kéo thả vào vị trí mong muốn.");
                    else { _selVp = p.Id; Touch(); }
                });
                if (!SheetViews.IsSchedule(f)) Btn("✎ Sửa cấu kiện / mặt bằng (kích đúp)", () => ActivateView(f));
                MemberProps(f, Head, Edit, Info);
                break;
            default:
                Head("DỰ ÁN");
                var st = SheetViews.Stats(VSet, _vcat);
                Info("Bộ bản vẽ", st.Sheets + " tờ · " + st.Placed + " khung nhìn đã đặt · " + st.Unplaced + " chưa đặt");
                ProjectProps(Head, Edit);
                TextProps(Head);
                break;
        }
    }

    private static FrameworkElement Row(string label, FrameworkElement editor)
    {
        var g = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap };
        g.Children.Add(l);
        Grid.SetColumn(editor, 1); g.Children.Add(editor);
        return g;
    }

    private void ProjectProps(Action<string> head, Action<string, string, Action<string>> edit)
    {
        head("THÔNG TIN DỰ ÁN (khung tên)");
        edit("Công trình", _prj.ProjectName, v => { _prj.ProjectName = v; Touch(); });
        edit("Chủ đầu tư", _prj.Owner, v => { _prj.Owner = v; Touch(); });
        edit("Địa điểm", _prj.Location, v => { _prj.Location = v; Touch(); });
        edit("Thiết kế", _prj.Designer, v => { _prj.Designer = v; Touch(); });
    }

    /// <summary>Thuộc tính cấu kiện của khung nhìn: tên, số lượng (sửa trực tiếp) + nút mở bảng nhập đầy đủ.</summary>
    private void MemberProps(SheetFigure f, Action<string> head, Action<string, string, Action<string>> edit, Action<string, string> info)
    {
        var t = SheetViews.TargetOf(f);
        var m = SheetViews.MemberOf(_prj, t);
        if (m == null) return;
        head("CẤU KIỆN");
        info("Loại", t.MemberKind);
        edit("Tên cấu kiện", m.Name, v => { if (!string.IsNullOrWhiteSpace(v)) { m.Name = v.Trim(); _res.Remove(m); ViewsChanged(); RefreshTree(m); RebuildViews(true); Touch(); } });
        edit("Số lượng", m.Quantity.ToString(CultureInfo.InvariantCulture), v => { if (int.TryParse(v, out int q) && q > 0) { m.Quantity = q; _res.Remove(m); ViewsChanged(); RefreshTree(m); RebuildViews(true); Touch(); } });
        if (_res.TryGetValue(m, out var r)) info("Kết quả", r.Ok ? "Đạt" : "KHÔNG ĐẠT");
    }

    /// <summary>Cỡ chữ in của hồ sơ (mm giấy; trống = theo Thiết lập bản vẽ).</summary>
    private void TextProps(Action<string> head)
    {
        head("CỠ CHỮ IN (mm khi in; để trống = theo thiết lập bản vẽ)");
        var tx = _prj.Settings.Text ??= new DetailTextSizes();
        void Num(string label, Func<double> get, Action<double> set, KetCauVXT.Model.TextRole? role, double def)
        {
            double cur = get();
            var tb = new TextBox { Text = cur > 0 ? cur.ToString("0.##", CultureInfo.InvariantCulture) : "", ToolTip = "Đang dùng: " + (role.HasValue ? tx.Effective(role.Value) : (cur > 0 ? cur : def)).ToString("0.##", CultureInfo.InvariantCulture) + " mm" };
            void Apply()
            {
                string s = tb.Text.Trim().Replace(',', '.');
                double v = s.Length == 0 ? 0 : double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double q) && q > 0 && q < 40 ? q : cur;
                if (Math.Abs(v - cur) < 1e-9) return;
                set(v); cur = v;
                TextSizesChanged();
            }
            tb.LostKeyboardFocus += (s, e) => Apply();
            tb.KeyDown += (s, e) => { if (e.Key == Key.Enter) { Apply(); e.Handled = true; } };
            _props.Children.Add(Row(label, tb));
        }
        Num("Ghi chú", () => tx.Note, v => tx.Note = v, KetCauVXT.Model.TextRole.Note, 2.5);
        Num("Nhãn thép", () => tx.Tag, v => tx.Tag = v, KetCauVXT.Model.TextRole.Tag, 2.5);
        Num("Tên hình", () => tx.Title, v => tx.Title = v, KetCauVXT.Model.TextRole.MemberTitle, 3.5);
        Num("Tên mặt cắt", () => tx.SectionTitle, v => tx.SectionTitle = v, KetCauVXT.Model.TextRole.SectionTitle, 3.5);
        Num("Kích thước", () => tx.Dimension, v => tx.Dimension = v, KetCauVXT.Model.TextRole.Dimension, 2.5);
        Num("Chữ trục", () => tx.Axis, v => tx.Axis = v, KetCauVXT.Model.TextRole.Axis, 3.0);
        Num("Bảng thống kê", () => tx.Schedule, v => tx.Schedule = v, KetCauVXT.Model.TextRole.Schedule, 2.5);
        Num("Vòng số hiệu Ø", () => tx.MarkDiameter, v => tx.MarkDiameter = v, null, 5.5);
        Num("Đầu trục Ø", () => tx.BubbleDiameter, v => tx.BubbleDiameter = v, null, 7.5);
        Num("Hệ số chung", () => tx.Factor > 0 && Math.Abs(tx.Factor - 1) > 1e-9 ? tx.Factor : 0, v => tx.Factor = v > 0 ? v : 1, null, 1);
    }

    private void TextSizesChanged()
    {
        _dirty = true; UpdateTitle();
        RebuildViews(true);
        if (ReferenceEquals(_tabs.SelectedItem, _sheetTab)) RenderSheet();
        if (_tabs.SelectedIndex == 2) RefreshPreview();
        SetStatus("Đã đổi cỡ chữ in – mọi hình triển khai vẽ lại theo cỡ mới (xem trước, bản vẽ, xuất file).");
    }

    // ------------------------------------------------------------------ tự kiểm (ảnh màn hình)
    /// <summary>Tự kiểm: tạo bản vẽ, đặt 2 khung nhìn, chọn khung nhìn để hiện thuộc tính. Trả về mô tả.</summary>
    private string SelfTestSheets()
    {
        _leftTabs.SelectedIndex = 1;
        RebuildViews(true);
        var sh = SheetViews.CreateSheet(VSet, "CHI TIẾT DẦM – THỬ BẢN VẼ KIỂU REVIT", "A2");
        var views = _vcat.Where(f => !SheetViews.IsSchedule(f) && f.Part == 1).Take(2).ToList();
        var (pw, ph) = sh.Size;
        int i = 0;
        foreach (var f in views) SheetViews.PlaceView(VSet, sh, f, _vmap, pw * (0.3 + 0.35 * i++), ph * 0.6);
        _curSheet = sh;
        _tabs.SelectedItem = _sheetTab;
        FillBrowser();
        var pf = sh.Figures.FirstOrDefault();
        if (pf != null) { _selVp = pf.Id; ShowProps(pf); }
        RenderSheet();
        return "bản vẽ kiểu Revit: " + sh.No + " – " + sh.Figures.Count + " khung nhìn, " + _vcat.Count + " khung nhìn trong trình duyệt";
    }
}

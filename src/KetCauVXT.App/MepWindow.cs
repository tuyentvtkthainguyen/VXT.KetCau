using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Mep;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>Môi trường ĐIỆN NƯỚC: kiến trúc hiện mờ như xref (không sửa được); đặt thiết bị (mặc định gắn tường, chôn ngầm),
/// nối tuyến tự đi trong tường (sinh cút / chếch / tê / côn / măng sông / hộp nối), tự chọn cỡ, cảnh báo khi kiến trúc thay đổi,
/// thư viện điện nước, bảng thống kê vật tư, xuất mặt bằng DXF (DN-).</summary>
public sealed partial class MepWindow : Window
{
    private readonly Project _p;
    private readonly Func<string> _snapshot;
    private readonly Action<string> _pushUndo;
    private readonly Action _changed;

    private readonly Canvas _cv = new() { Background = Brushes.White, ClipToBounds = true };
    private readonly ComboBox _story = new() { Width = 120, Margin = new Thickness(4, 0, 8, 0) };
    private readonly ComboBox _sysWater = new() { Width = 150, Margin = new Thickness(4, 2, 4, 2) };
    private readonly ComboBox _sysElec = new() { Width = 150, Margin = new Thickness(4, 2, 4, 2) };
    private readonly TextBlock _status = new() { Margin = new Thickness(6, 2, 6, 2) };
    private readonly ListBox _warn = new() { MinHeight = 120 };
    private readonly StackPanel _props = new() { Margin = new Thickness(6) };
    private readonly DataGrid _qty = new() { AutoGenerateColumns = true, IsReadOnly = true, Height = 220 };

    private string _tool = "select";      // select | place | connect
    private string _placeFamily = "";
    private MepSystemKind _connectSystem = MepSystemKind.DienOCam;
    private int _connectFrom;
    private int _selDevice, _selRun;
    private double _scale = 60, _ox = 60, _oy = 400;   // px/m, gốc màn hình
    private bool _fitted;
    private Point? _panFrom;

    public MepWindow(Project p, Func<string> snapshot, Action<string> pushUndo, Action changed)
    {
        _p = p; _snapshot = snapshot; _pushUndo = pushUndo; _changed = changed;
        Title = "Việt Const – Môi trường Điện nước (cấp thoát nước, điện)";
        Width = 1280; Height = 820; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontSize = 12;

        foreach (var s in p.Stories.OrderBy(s => s.Elevation)) _story.Items.Add(s.Name);
        if (_story.Items.Count > 0) _story.SelectedIndex = 0;
        _story.SelectionChanged += (_, _) => { _fitted = false; Redraw(); };
        foreach (var s in MepSystems.All.Where(MepSystems.IsWater)) _sysWater.Items.Add(new ComboBoxItem { Content = MepSystems.Name(s), Tag = s });
        foreach (var s in MepSystems.All.Where(MepSystems.IsElectric)) _sysElec.Items.Add(new ComboBoxItem { Content = MepSystems.Name(s), Tag = s });
        _sysWater.SelectedIndex = 0; _sysElec.SelectedIndex = 1;

        var root = new DockPanel();
        var ribbon = BuildRibbon();
        DockPanel.SetDock(ribbon, Dock.Top); root.Children.Add(ribbon);
        var sb = new StatusBar(); sb.Items.Add(_status);
        DockPanel.SetDock(sb, Dock.Bottom); root.Children.Add(sb);

        var left = new DockPanel { Width = 250 };
        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6) };
        top.Children.Add(new TextBlock { Text = "Tầng:", VerticalAlignment = VerticalAlignment.Center });
        top.Children.Add(_story);
        DockPanel.SetDock(top, Dock.Top); left.Children.Add(top);
        left.Children.Add(new GroupBox { Header = "Thuộc tính", Content = new ScrollViewer { Content = _props, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        DockPanel.SetDock(left, Dock.Left); root.Children.Add(left);

        var right = new DockPanel { Width = 330 };
        var wb = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
        wb.Children.Add(Btn("Đi tới", () => GoToWarning()));
        wb.Children.Add(Btn("Sửa nhanh", () => FixWarning(false)));
        wb.Children.Add(Btn("Sửa tất cả", () => FixWarning(true)));
        DockPanel.SetDock(wb, Dock.Bottom); right.Children.Add(wb);
        right.Children.Add(new GroupBox { Header = "Cảnh báo (kiến trúc thay đổi / xung đột)", Content = _warn });
        DockPanel.SetDock(right, Dock.Right); root.Children.Add(right);

        var host = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Child = _cv };
        root.Children.Add(host);
        Content = root;

        _cv.SizeChanged += (_, _) => { if (!_fitted) Fit(); Redraw(); };
        _cv.MouseWheel += OnWheel;
        _cv.MouseDown += OnDown;
        _cv.MouseMove += OnMove;
        _cv.MouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Middle) { _panFrom = null; _cv.ReleaseMouseCapture(); } };
        _warn.MouseDoubleClick += (_, _) => GoToWarning();
        KeyDown += OnKey;
        Activated += (_, _) => CheckArchChanges();
        Loaded += (_, _) =>
        {
            var r = MepUpdater.Reconcile(_p);
            RefreshWarnings(); Fit(); Redraw();
            SetStatus("Kiến trúc hiện mờ (tham chiếu, không sửa được). Chọn thiết bị trên ribbon rồi bấm gần tường để đặt – mặc định chôn ngầm trong tường. " + r.Summary);
        };
    }

    private static Button Btn(string text, Action a, string tip = null)
    {
        var b = new Button { Content = text, Margin = new Thickness(2), Padding = new Thickness(6, 2, 6, 2), ToolTip = tip };
        b.Click += (_, _) => a();
        return b;
    }

    // ------------------------------------------------------------------
    // Ribbon
    // ------------------------------------------------------------------
    private TabControl BuildRibbon()
    {
        var tabs = new TabControl { Height = 112 };
        var lib = _p.Mep.Library;

        WrapPanel Group(string title, IEnumerable<MepFamily> fams)
        {
            var wp = new WrapPanel { Margin = new Thickness(2) };
            wp.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.Bold, Margin = new Thickness(4, 6, 6, 0) });
            foreach (var f in fams)
            {
                var fam = f;
                wp.Children.Add(Btn(fam.Name, () => StartPlace(fam.Name), $"Đặt {fam.Name} – cao {fam.MountHeight:0.##} m{(fam.WallHosted ? ", gắn tường (chôn ngầm)" : fam.Ceiling ? ", trên trần" : ", trên sàn")}"));
            }
            return wp;
        }

        // Cấp thoát nước
        var water = new StackPanel();
        water.Children.Add(Group("Thiết bị:", lib.Families.Where(f => f.Category is MepCategory.VeSinh or MepCategory.Van or MepCategory.DiemNoiOng or MepCategory.BinhNong)));
        var wrow = new WrapPanel();
        wrow.Children.Add(new TextBlock { Text = "Hệ thống:", Margin = new Thickness(4, 6, 0, 0) });
        wrow.Children.Add(_sysWater);
        wrow.Children.Add(Btn("Nối tuyến ống", () => StartConnect((MepSystemKind)((ComboBoxItem)_sysWater.SelectedItem).Tag), "Bấm thiết bị nguồn rồi thiết bị đích – ống tự đi trong tường, sinh cút / tê / côn / măng sông"));
        wrow.Children.Add(Btn("Tự chọn cỡ ống", AutoSize, "Cấp nước theo đương lượng (TCVN 4513), thoát nước theo DFU"));
        wrow.Children.Add(Btn("Nối thoát về ống đứng", () => Edit(() =>
        {
            int n = 0;
            foreach (var st in _p.Mep.Devices.Select(d => d.Story).Distinct().ToList()) n += MepDrainage.ConnectToStack(_p, st).Count;
            MepRouting.RebuildAll(_p);
            SetStatus($"Đã nối {n} thiết bị thoát về ống đứng (đi dưới sàn, có độ dốc).");
        }), "Đề xuất ống đứng (hộp gen) tại góc tường gần nhóm thiết bị vệ sinh, nối các thiết bị thoát đi dưới sàn có độ dốc"));
        wrow.Children.Add(Btn("Chọn / sửa", () => SetTool("select")));
        water.Children.Add(wrow);
        tabs.Items.Add(new TabItem { Header = "Cấp thoát nước", Content = new ScrollViewer { Content = water, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });

        // Điện
        var elec = new StackPanel();
        elec.Children.Add(Group("Thiết bị:", lib.Families.Where(f => f.Category is MepCategory.OCam or MepCategory.CongTac or MepCategory.Den or MepCategory.Quat or MepCategory.HopChia or MepCategory.Aptomat or MepCategory.TuDien)));
        var erow = new WrapPanel();
        erow.Children.Add(new TextBlock { Text = "Hệ thống:", Margin = new Thickness(4, 6, 0, 0) });
        erow.Children.Add(_sysElec);
        erow.Children.Add(Btn("Nối dây (ống gen)", () => StartConnect((MepSystemKind)((ComboBoxItem)_sysElec.SelectedItem).Tag), "Dây luồn ống gen chôn tường; điểm chia ≥ 3 tuyến sinh hộp nối"));
        erow.Children.Add(Btn("Tự chọn tiết diện dây", AutoSize, "I = P/(U·cosφ) → aptomat → dây Cu/PVC (IEC 60364 B1) → ống gen ≤ 40 %"));
        erow.Children.Add(Btn("Chọn / sửa", () => SetTool("select")));
        elec.Children.Add(erow);
        tabs.Items.Add(new TabItem { Header = "Điện", Content = new ScrollViewer { Content = elec, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });

        // Điều hoà – thông gió (MepWindow.Hvac.cs)
        tabs.Items.Add(new TabItem { Header = "Điều hoà – thông gió", Content = new ScrollViewer { Content = BuildHvacRibbon(), VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });

        // Thư viện
        var libp = new WrapPanel();
        libp.Children.Add(Btn("Xem thư viện…", ShowLibrary));
        libp.Children.Add(Btn("Thêm họ thiết bị…", AddFamily, "Lưu vào dự án và thư viện người dùng trên máy (%AppData%\\VietConst\\ThuVienMEP.json)"));
        tabs.Items.Add(new TabItem { Header = "Thư viện MEP", Content = libp });

        // Kiểm tra
        var chk = new WrapPanel();
        chk.Children.Add(Btn("Cập nhật theo kiến trúc", () => { Edit(() => { }, reconcileMsg: true); }, "Đối chiếu tường / cửa: dời thiết bị theo tường, đi lại tuyến, cảnh báo"));
        chk.Children.Add(Btn("Đặt ký hiệu tự động", () => Edit(() => MepDrawings.AutoMark(_p))));
        chk.Children.Add(Btn("Bảng thống kê vật tư", ShowQuantities));
        chk.Children.Add(Btn("Xuất bản vẽ điện nước (DXF)…", ExportDxf));
        chk.Children.Add(Btn("Vừa màn hình", () => { Fit(); Redraw(); }));
        chk.Children.Add(Btn("Cài đặt điện nước…", ShowSettings, "Độ dốc theo hệ / Ø, cao độ mặc định theo nhóm thiết bị, chiều dài cây ống, trần giả, nắp thăm, quang treo (MepWindow.Settings.cs)"));
        tabs.Items.Add(new TabItem { Header = "Kiểm tra", Content = chk });
        return tabs;
    }

    private void SetTool(string t) { _tool = t; _connectFrom = 0; SetStatus(t == "select" ? "Chọn: bấm thiết bị / tuyến để xem thuộc tính – Delete để xoá." : ""); Redraw(); }
    private void StartPlace(string fam) { _tool = "place"; _placeFamily = fam; SetStatus($"Đặt {fam}: bấm gần tường (≤ 1 m) để gắn vào mặt tường phía bấm – Esc để thôi."); }
    private void StartConnect(MepSystemKind s) { _tool = "connect"; _connectSystem = s; _connectFrom = 0; SetStatus($"Nối {MepSystems.Name(s)}: bấm thiết bị nguồn…"); }
    private void SetStatus(string s) => _status.Text = s;

    private string Story => _story.SelectedItem as string ?? (_p.Stories.FirstOrDefault()?.Name ?? "");

    // ------------------------------------------------------------------
    // Sửa có hoàn tác
    // ------------------------------------------------------------------
    private void Edit(Action a, bool reconcileMsg = false)
    {
        string snap = _snapshot?.Invoke();
        try { a(); }
        catch (Exception ex) { SetStatus("Lỗi: " + ex.Message); return; }
        var r = MepUpdater.Reconcile(_p);
        if (snap != null) _pushUndo?.Invoke(snap);
        _changed?.Invoke();
        RefreshWarnings(); Redraw(); ShowProps();
        if (reconcileMsg) SetStatus(r.Summary);
    }

    private void AutoSize() => Edit(() => { int n = MepSizing.AutoSize(_p); MepRouting.RebuildAll(_p); SetStatus($"Đã chọn lại cỡ {n} tuyến."); });

    private void CheckArchChanges()
    {
        if (!IsLoaded || !MepUpdater.HasArchChanges(_p)) return;
        var r = MepUpdater.Reconcile(_p);
        _changed?.Invoke();
        RefreshWarnings(); Redraw();
        SetStatus("Kiến trúc đã thay đổi – đã tự cập nhật: " + r.Summary);
        if (r.Warnings.Count > 0)
            MessageBox.Show(this, $"Kiến trúc đã thay đổi.\n{r.Summary}.\nXem danh sách cảnh báo bên phải (Đi tới / Sửa nhanh).", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void RefreshWarnings()
    {
        _warn.Items.Clear();
        foreach (var w in _p.Mep.Warnings) _warn.Items.Add(new ListBoxItem { Content = new TextBlock { Text = "⚠ " + w.Message + (string.IsNullOrEmpty(w.Fix) ? "" : "  → " + w.Fix), TextWrapping = TextWrapping.Wrap, Width = 300 }, Tag = w });
    }

    private MepWarning SelWarning => (_warn.SelectedItem as ListBoxItem)?.Tag as MepWarning;

    private void GoToWarning()
    {
        var w = SelWarning; if (w == null) return;
        if (!string.IsNullOrEmpty(w.Story) && _story.Items.Contains(w.Story)) _story.SelectedItem = w.Story;
        _selDevice = w.DeviceId; _selRun = w.RunId;
        _ox = _cv.ActualWidth / 2 - w.X * _scale; _oy = _cv.ActualHeight / 2 + w.Y * _scale;
        _fitted = true; Redraw(); ShowProps();
    }

    private void FixWarning(bool all)
    {
        var sel = SelWarning;
        var list = all ? _p.Mep.Warnings.ToList() : (sel != null ? new List<MepWarning> { sel } : new List<MepWarning>());
        if (list.Count == 0) return;
        int ok = 0;
        Edit(() => { foreach (var wn in list) if (MepUpdater.ApplyFix(_p, wn)) ok++; });
        SetStatus($"Đã sửa nhanh {ok}/{list.Count} cảnh báo.");
    }

    // ------------------------------------------------------------------
    // Toạ độ, chuột, phím
    // ------------------------------------------------------------------
    private Point S(double x, double y) => new(_ox + x * _scale, _oy - y * _scale);
    private (double X, double Y) W(Point p) => ((p.X - _ox) / _scale, (_oy - p.Y) / _scale);

    private void Fit()
    {
        double w = _cv.ActualWidth, h = _cv.ActualHeight;
        if (w < 20 || h < 20) return;
        var xs = new List<double>(); var ys = new List<double>();
        foreach (var wl in _p.Walls.Where(x => x.Story == Story)) { xs.Add(wl.X1); xs.Add(wl.X2); ys.Add(wl.Y1); ys.Add(wl.Y2); }
        foreach (var d in _p.Mep.Devices.Where(d => d.Story == Story)) { xs.Add(d.X); ys.Add(d.Y); }
        if (xs.Count == 0) { xs.AddRange(new[] { 0.0, 10 }); ys.AddRange(new[] { 0.0, 10 }); }
        double x0 = xs.Min(), x1 = xs.Max(), y0 = ys.Min(), y1 = ys.Max();
        double sx = (w - 80) / Math.Max(0.5, x1 - x0), sy = (h - 80) / Math.Max(0.5, y1 - y0);
        _scale = Math.Clamp(Math.Min(sx, sy), 2, 600);
        _ox = w / 2 - (x0 + x1) / 2 * _scale; _oy = h / 2 + (y0 + y1) / 2 * _scale;
        _fitted = true;
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        var pt = e.GetPosition(_cv); var (wx, wy) = W(pt);
        _scale = Math.Clamp(_scale * (e.Delta > 0 ? 1.2 : 1 / 1.2), 2, 2000);
        _ox = pt.X - wx * _scale; _oy = pt.Y + wy * _scale;
        _fitted = true; Redraw();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_panFrom is Point a && e.MiddleButton == MouseButtonState.Pressed)
        {
            var b = e.GetPosition(_cv);
            _ox += b.X - a.X; _oy += b.Y - a.Y; _panFrom = b; Redraw();
            return;
        }
        SnapPreview(e.GetPosition(_cv));   // [Kiểu Revit] bóng thiết bị + kích thước tạm khi đặt – MepWindow.Snap.cs
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        var pt = e.GetPosition(_cv);
        if (e.ChangedButton == MouseButton.Middle) { _panFrom = pt; _cv.CaptureMouse(); return; }
        if (e.ChangedButton == MouseButton.Right) { SetTool("select"); return; }
        if (e.ChangedButton != MouseButton.Left) return;
        var (x, y) = W(pt);
        if (_tool.StartsWith("hvac", StringComparison.Ordinal)) { HvacClick(x, y); return; }   // MepWindow.Hvac.cs
        switch (_tool)
        {
            case "place":
                Edit(() =>
                {
                    var d = MepRouting.Place(_p, _placeFamily, Story, x, y);
                    _selDevice = d.Id; _selRun = 0;
                    SetStatus(d.WallId > 0 ? $"Đã đặt {d.Family} trên tường {d.WallId} (cao {d.Height:0.##} m, chôn ngầm). Bấm tiếp để đặt thêm, Esc để thôi."
                                           : $"Đã đặt {d.Family} tự do tại ({x:0.00}; {y:0.00}).");
                });
                break;
            case "connect":
            {
                var dv = HitDevice(pt);
                if (dv == null) { SetStatus("Bấm vào một thiết bị."); return; }
                if (_connectFrom == 0) { _connectFrom = dv.Id; _selDevice = dv.Id; Redraw(); SetStatus($"Nguồn: {MepUpdater.Label(_p.Mep, dv)} – bấm thiết bị đích…"); return; }
                if (dv.Id == _connectFrom) return;
                int from = _connectFrom;
                Edit(() =>
                {
                    var r = MepRouting.Connect(_p, from, dv.Id, _connectSystem);
                    MepSizing.AutoSize(_p); MepRouting.RebuildAll(_p);
                    _selRun = r.Id; _selDevice = 0;
                    SetStatus($"Đã nối tuyến {r.Id}: {MepDrawings.RunLabel(r)}, dài {r.Length:0.00} m – chôn ngầm trong tường. Bấm thiết bị nguồn mới…");
                });
                _connectFrom = 0;
                break;
            }
            default:
            {
                var dv = HitDevice(pt);
                _selDevice = dv?.Id ?? 0;
                _selRun = dv == null ? (HitRun(pt)?.Id ?? 0) : 0;
                ShowProps(); Redraw();
                break;
            }
        }
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { SetTool("select"); e.Handled = true; }
        else if (e.Key == Key.Delete && Keyboard.FocusedElement is not TextBox) { DeleteSelected(); e.Handled = true; }
    }

    private void DeleteSelected()
    {
        if (_selDevice > 0)
        {
            int id = _selDevice;
            Edit(() => { _p.Mep.Devices.RemoveAll(d => d.Id == id); _p.Mep.Runs.RemoveAll(r => r.FromId == id || r.ToId == id); MepRouting.RebuildAll(_p); });
            _selDevice = 0;
        }
        else if (_selRun > 0)
        {
            int id = _selRun;
            Edit(() => { _p.Mep.Runs.RemoveAll(r => r.Id == id); MepRouting.RebuildAll(_p); });
            _selRun = 0;
        }
    }

    private MepDevice HitDevice(Point pt)
    {
        MepDevice best = null; double bd = 10;
        foreach (var d in _p.Mep.Devices.Where(d => d.Story == Story))
        {
            var s = S(d.X, d.Y); double dd = (s - pt).Length;
            if (dd < bd) { bd = dd; best = d; }
        }
        return best;
    }

    private MepRun HitRun(Point pt)
    {
        var (x, y) = W(pt); double tol = 6 / _scale;
        foreach (var r in _p.Mep.Runs.Where(r => r.Story == Story))
            for (int i = 1; i < r.Path.Count; i++)
                if (MepGeom.Project(x, y, r.Path[i - 1].X, r.Path[i - 1].Y, r.Path[i].X, r.Path[i].Y).Dist < tol) return r;
        return null;
    }

    // ------------------------------------------------------------------
    // Vẽ
    // ------------------------------------------------------------------
    private static Brush Br(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex); b.Freeze(); return b; }

    private void Redraw()
    {
        _cv.Children.Clear();
        var gray = Br("#B0B0B0"); var light = Br("#E6E6E6");
        // kiến trúc: tường mờ (xref), ô cửa
        foreach (var wl in _p.Walls.Where(x => x.Story == Story))
        {
            double t = MepRouting.WallInfo(_p, wl).Total;
            var a = S(wl.X1, wl.Y1); var b = S(wl.X2, wl.Y2);
            _cv.Children.Add(new Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = light, StrokeThickness = Math.Max(2, t * _scale), IsHitTestVisible = false });
            _cv.Children.Add(new Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = gray, StrokeThickness = 0.8, IsHitTestVisible = false });
            double L = wl.Chord; if (L < 1e-6) continue;
            foreach (var o in wl.Openings ?? new())
            {
                var ot = WallAssembly.TypeOf(_p, o); if (ot == null) continue;
                var p1 = MepRouting.PointOnWall(wl, Math.Max(0, o.Offset - ot.Width / 2)); var p2 = MepRouting.PointOnWall(wl, Math.Min(L, o.Offset + ot.Width / 2));
                var s1 = S(p1.X, p1.Y); var s2 = S(p2.X, p2.Y);
                _cv.Children.Add(new Shapes.Line { X1 = s1.X, Y1 = s1.Y, X2 = s2.X, Y2 = s2.Y, Stroke = Brushes.White, StrokeThickness = Math.Max(2, t * _scale) + 1, IsHitTestVisible = false });
                var tb = new TextBlock { Text = ot.Tag, Foreground = gray, FontSize = 10 };
                Canvas.SetLeft(tb, (s1.X + s2.X) / 2); Canvas.SetTop(tb, (s1.Y + s2.Y) / 2); _cv.Children.Add(tb);
            }
        }
        // tuyến
        foreach (var r in _p.Mep.Runs.Where(r => r.Story == Story))
        {
            var pl = new Shapes.Polyline { Stroke = Br(MepSystems.Color(r.System)), StrokeThickness = r.Id == _selRun ? 3.5 : 1.8 };
            if (MepSystems.IsDrain(r.System)) pl.StrokeDashArray = new DoubleCollection { 4, 2 };
            foreach (var q in r.Path) pl.Points.Add(S(q.X, q.Y));
            pl.ToolTip = $"Tuyến {r.Id}: {MepSystems.Name(r.System)} – {MepDrawings.RunLabel(r)} – {r.Length:0.00} m";
            _cv.Children.Add(pl);
        }
        foreach (var f in _p.Mep.Fittings.Where(f => f.Story == Story))
        {
            var s = S(f.X, f.Y);
            var el = new Shapes.Rectangle { Width = 5, Height = 5, Fill = Brushes.DimGray, ToolTip = f.Name, IsHitTestVisible = true };
            Canvas.SetLeft(el, s.X - 2.5); Canvas.SetTop(el, s.Y - 2.5); _cv.Children.Add(el);
        }
        // thiết bị
        foreach (var d in _p.Mep.Devices.Where(d => d.Story == Story))
        {
            var fam = _p.Mep.FamilyOf(d);
            var sys = fam?.Connectors.FirstOrDefault()?.System ?? MepSystemKind.DienOCam;
            var s = S(d.X, d.Y);
            bool sel = d.Id == _selDevice || d.Id == _connectFrom;
            var el = new Shapes.Ellipse { Width = 12, Height = 12, Fill = Br(MepSystems.Color(sys)), Stroke = sel ? Brushes.Red : Brushes.Black, StrokeThickness = sel ? 2.5 : 1,
                ToolTip = $"{d.Family}{(d.WallId > 0 ? $" – tường {d.WallId}, cao {d.Height:0.##} m, chôn ngầm" : "")}" };
            Canvas.SetLeft(el, s.X - 6); Canvas.SetTop(el, s.Y - 6); _cv.Children.Add(el);
            var tb = new TextBlock { Text = string.IsNullOrWhiteSpace(d.Mark) ? MepDrawings.Prefix(fam) + d.Id : d.Mark, FontSize = 10, Foreground = Brushes.Black };
            Canvas.SetLeft(tb, s.X + 7); Canvas.SetTop(tb, s.Y - 14); _cv.Children.Add(tb);
        }
        foreach (var w in _p.Mep.Warnings.Where(w => w.Story == Story))
        {
            var s = S(w.X, w.Y);
            var tb = new TextBlock { Text = "⚠", Foreground = Brushes.OrangeRed, FontSize = 14, ToolTip = w.Message };
            Canvas.SetLeft(tb, s.X - 16); Canvas.SetTop(tb, s.Y + 2); _cv.Children.Add(tb);
        }
        DrawDeviceDims();   // [Kiểu Revit] kích thước tạm thiết bị gắn tường – MepWindow.Snap.cs
        DrawHvac();         // điều hoà – thông gió – MepWindow.Hvac.cs
    }

    // ------------------------------------------------------------------
    // Thuộc tính
    // ------------------------------------------------------------------
    private void ShowProps()
    {
        _props.Children.Clear();
        var d = _p.Mep.Device(_selDevice);
        if (d != null)
        {
            var fam = _p.Mep.FamilyOf(d);
            _props.Children.Add(new TextBlock { Text = d.Family, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
            _props.Children.Add(new TextBlock { Text = d.WallId > 0 ? $"Gắn tường {d.WallId}, vị trí {d.Offset:0.###} m, mặt {(d.Side > 0 ? "trái" : "phải")} – chôn ngầm" : "Đặt tự do (sàn / trần)", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6) });
            Field("Ký hiệu", d.Mark, v => d.Mark = v);
            Field("Cao độ (m)", d.Height.ToString("0.###"), v => { if (double.TryParse(v.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h)) d.Height = h; });
            if (d.WallId > 0) Field("Vị trí dọc tường (m)", d.Offset.ToString("0.###"), v => { if (double.TryParse(v.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var o)) d.Offset = o; });
            if (fam != null && MepSystems.IsElectric(fam.Connectors.FirstOrDefault()?.System ?? MepSystemKind.DienOCam)) Field("Mạch", d.Circuit, v => d.Circuit = v);
            if (d.WallId > 0) _props.Children.Add(Btn("Đổi mặt tường (chôn mặt kia)", () => Edit(() => MepUpdater.FlipSide(_p, d))));
            _props.Children.Add(Btn("Xoá thiết bị", DeleteSelected));
            return;
        }
        var r = _p.Mep.Run(_selRun);
        if (r != null)
        {
            _props.Children.Add(new TextBlock { Text = $"Tuyến {r.Id} – {MepSystems.Name(r.System)}", FontWeight = FontWeights.Bold });
            _props.Children.Add(new TextBlock { Text = $"{MepDrawings.RunLabel(r)}\nDài {r.Length:0.00} m – chôn ngầm trong tường\nPhụ kiện: {_p.Mep.Fittings.Count(f => f.RunId == r.Id)}", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6) });
            Field(r.Kind == MepRunKind.Ong ? "Đường kính (mm)" : "Tiết diện dây (mm²)", r.Size.ToString("0.#"), v =>
            {
                if (double.TryParse(v.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0) { r.Size = s; r.AutoSize = false; }
            });
            var auto = new CheckBox { Content = "Tự chọn cỡ", IsChecked = r.AutoSize, Margin = new Thickness(0, 4, 0, 4) };
            auto.Click += (_, _) => Edit(() => r.AutoSize = auto.IsChecked == true);
            _props.Children.Add(auto);
            if (MepSystems.IsDrain(r.System))
            {
                // thoát nước: độ dốc (%), mặc định theo Ø; đi dưới sàn (không khuyến khích đi trong tường)
                double iUse = MepDrainage.SlopeOf(_p, r);
                var prof = MepDrainage.Profile(r);
                _props.Children.Add(new TextBlock
                {
                    Text = $"Đi {(r.DrainInWall ? "TRONG TƯỜNG (không khuyến khích)" : "dưới sàn")} – dốc {iUse * 100:0.#} %" +
                           (prof.Count > 0 ? $"\nĐáy ống đầu {prof[0].Invert:0.000} → cuối {prof[^1].Invert:0.000} m" : ""),
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2)
                });
                Field($"Độ dốc (%) – 0 = mặc định {MepDrainage.DefaultSlope(_p.Mep.Settings, r.Size, r.System) * 100:0.#} %", (r.Slope * 100).ToString("0.##"), v =>
                {
                    if (double.TryParse(v.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sl) && sl >= 0) r.Slope = sl / 100;
                });
                var inWall = new CheckBox { Content = "Ép đi trong tường (không khuyến khích)", IsChecked = r.DrainInWall, Margin = new Thickness(0, 4, 0, 4) };
                inWall.Click += (_, _) => Edit(() => { r.DrainInWall = inWall.IsChecked == true; MepRouting.RebuildAll(_p); });
                _props.Children.Add(inWall);
            }
            _props.Children.Add(Btn("Xoá tuyến", DeleteSelected));
            return;
        }
        _props.Children.Add(new TextBlock { Text = "Chưa chọn đối tượng.\n\nKiến trúc (tường, cửa) là bản vẽ tham chiếu – sửa ở môi trường Kiến trúc; khi tường / cửa đổi, điện nước tự cập nhật và cảnh báo.", TextWrapping = TextWrapping.Wrap });
    }

    private void Field(string label, string value, Action<string> set)
    {
        _props.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 4, 0, 0) });
        var tb = new TextBox { Text = value ?? "" };
        tb.LostFocus += (_, _) => { if (tb.Text != (value ?? "")) Edit(() => { set(tb.Text); MepRouting.RebuildAll(_p); }); };
        tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Keyboard.ClearFocus(); _cv.Focus(); } };
        _props.Children.Add(tb);
    }

    // ------------------------------------------------------------------
    // Thư viện, thống kê, xuất
    // ------------------------------------------------------------------
    private void ShowLibrary()
    {
        var g = new DataGrid { IsReadOnly = true, AutoGenerateColumns = false, ItemsSource = _p.Mep.Library.Families };
        void Col(string h, string path) => g.Columns.Add(new DataGridTextColumn { Header = h, Binding = new System.Windows.Data.Binding(path) });
        Col("Tên", "Name"); Col("Nhóm", "Category"); Col("Ký hiệu", "Symbol"); Col("Cao lắp (m)", "MountHeight"); Col("Gắn tường", "WallHosted");
        Col("Công suất (W)", "Power"); Col("Đương lượng cấp", "SupplyUnits"); Col("DFU thoát", "DrainUnits"); Col("Cỡ", "Size"); Col("Vật liệu", "Material");
        new Window { Title = "Thư viện điện nước", Owner = this, Width = 980, Height = 560, Content = g, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
    }

    private void AddFamily()
    {
        var name = new TextBox { Text = "Thiết bị mới" };
        var cat = new ComboBox { ItemsSource = Enum.GetValues<MepCategory>(), SelectedItem = MepCategory.OCam };
        var sym = new ComboBox { ItemsSource = MepSymbols.All, SelectedIndex = 0 };
        var h = new TextBox { Text = "0.4" }; var pw = new TextBox { Text = "0" };
        var wall = new CheckBox { Content = "Gắn tường (chôn ngầm)", IsChecked = true };
        var sp = new StackPanel { Margin = new Thickness(10) };
        void Row(string l, UIElement c) { sp.Children.Add(new TextBlock { Text = l, Margin = new Thickness(0, 6, 0, 0) }); sp.Children.Add(c); }
        Row("Tên", name); Row("Nhóm", cat); Row("Ký hiệu 2D", sym); Row("Cao độ lắp (m)", h); Row("Công suất (W)", pw); sp.Children.Add(wall);
        var ok = new Button { Content = "Thêm", IsDefault = true, Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(10, 3, 10, 3) };
        var dlg = new Window { Title = "Thêm họ thiết bị", Owner = this, Width = 340, SizeToContent = SizeToContent.Height, Content = sp, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        ok.Click += (_, _) => dlg.DialogResult = true;
        sp.Children.Add(ok);
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(name.Text)) return;
        double D(string s) => double.TryParse(s.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
        var f = new MepFamily { Name = name.Text.Trim(), Category = (MepCategory)cat.SelectedItem, Symbol = sym.SelectedItem as string ?? "", MountHeight = D(h.Text), Power = D(pw.Text), WallHosted = wall.IsChecked == true, User = true };
        Edit(() =>
        {
            _p.Mep.ProjectFamilies.RemoveAll(x => x.Name == f.Name);
            _p.Mep.ProjectFamilies.Add(f);
            _p.Mep.Library = null;
        });
        try
        {
            var ul = MepLibrary.Load();
            ul.Families.RemoveAll(x => x.Name == f.Name); ul.Families.Add(f);
            ul.SaveUser();
        }
        catch (Exception ex) { SetStatus("Không lưu được thư viện người dùng: " + ex.Message); }
        SetStatus($"Đã thêm họ '{f.Name}' (dự án + thư viện người dùng trên máy). Mở lại cửa sổ để có nút trên ribbon.");
    }

    private void ShowQuantities()
    {
        MepRouting.RebuildAll(_p);
        _qty.ItemsSource = MepQuantities.Rows(_p).Select(r => new { Nhóm = r.Group, VậtTư = r.Item, ĐơnVị = r.Unit, KhốiLượng = r.Qty, GhiChú = r.Note }).ToList();
        new Window { Title = MepQuantities.Title, Owner = this, Width = 760, Height = 520, Content = new DataGrid { IsReadOnly = true, AutoGenerateColumns = true, ItemsSource = _qty.ItemsSource }, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
    }

    private void ExportDxf()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Chọn thư mục lưu bản vẽ điện nước" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            MepRouting.RebuildAll(_p);
            var items = MepRegistration.Sheets(_p, new KetCauVXT.Drawing.DisciplineDocOptions());
            int k = 1;
            foreach (var it in items)
            {
                if (it.W == null) continue;
                it.W.Save(System.IO.Path.Combine(dlg.FolderName, $"DN-{k++:00}_{it.FileName}"));
            }
            SetStatus($"Đã xuất {k - 1} bản vẽ điện nước (DN-01…) vào {dlg.FolderName}.");
        }
        catch (Exception ex) { MessageBox.Show(this, "Lỗi xuất bản vẽ: " + ex.Message, Title); }
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Import.Link;
using KetCauVXT.Model;
using KetCauVXT.Ui;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// THANH LỆNH THEO CẤU KIỆN trên tab "Mặt bằng" (thay các hàng nút dài): cụm Chọn chung bên trái + các tab
//   Lưới – Tầng | Móng | Cột | Dầm | Sàn | Tường – Tải | Link CAD.
//   • các nút / ô có sẵn (planEditBar trong XAML, thanh dầm kiểu ETABS, Sửa dầm, Hạ cốt sàn…) được CHUYỂN vào tab tương ứng
//     (giữ nguyên x:Name, handler – tự kiểm và các chức năng khác không đổi);
//   • mỗi tab cấu kiện có nhóm "Gán tải trọng" (trường hợp tải, giá trị, Thay thế | Cộng thêm | Xoá) áp cho MỌI đối tượng đang chọn
//     (một bước hoàn tác), hiện giá trị chung / "<khác nhau>" của các đối tượng chọn;
//   • tab quyết định bộ lọc khi chọn nhiều (bấm / kéo khung window – crossing): Dầm → dầm, Cột/Móng → cột, Sàn → ô sàn;
//   • tab Link CAD: liên kết DWG/DXF làm nền (xref) theo tầng, ẩn/hiện, độ mờ, đọc lại, căn 2 điểm, khoá, bắt điểm CAD,
//     Pick CAD, "Tường CAD → tải dầm".
// Lõi: Model/LoadAssign.cs, Model/SelectionRules.cs. Dòng lệnh kiểu AutoCAD: MainWindow.CadCommandLine.cs.
// =====================================================================
public partial class MainWindow
{
    private TabControl _ribbon;
    private Border _ribbonHost;
    private bool _ribbonCollapsed;
    private SelectFilter _selFilter = SelectFilter.All;
    /// <summary>Ô sàn đang chọn nhiều (Id).</summary>
    private readonly HashSet<int> _multiSlab = new();
    private readonly Dictionary<string, TextBlock> _ribbonInfo = new();
    private ToggleButton _clLockBtn;
    private Slider _clOpacity;
    private ComboBox _cbColSec;
    private static readonly Brush SlabSelFill = Frozen(Color.FromArgb(70, 255, 160, 0));

    private const string RtLuoi = "Lưới – Tầng", RtMong = "Móng", RtCot = "Cột", RtDam = "Dầm", RtSan = "Sàn", RtTuong = "Tường – Tải", RtCad = "Link CAD";

    // ==================================================================
    // Dựng thanh lệnh
    // ==================================================================
    private void InitMemberRibbon()
    {
        try { BuildMemberRibbon(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Thanh lệnh cấu kiện: " + ex.Message); }
    }

    private static string CategoryOf(UIElement el)
    {
        string tag = (el as FrameworkElement)?.Tag as string ?? "";
        string name = (el as FrameworkElement)?.Name ?? "";
        string text = el switch { ContentControl cc => cc.Content as string ?? "", TextBlock tb => tb.Text ?? "", _ => "" };
        switch (tag)
        {
            case "Select": case "Delete": case "MultiSelect": return "";
            case "AddSlab": case "Balcony": case "Stair": case "Void": case "WC": case "SlabDropMode": case "AssignBuildUp": return RtSan;
            case "AddWall": case "AssignWall": return RtTuong;
            case "PickCad": return RtCad;
            case "AddBeam": case "AddCurvedBeam": case "QuickGridBeam": case "SecondaryBeam": case "CantileverBeam":
            case "StretchBeam": case "MoveBeam": case "TrimBeam": case "ExtendBeam": return RtDam;
        }
        switch (name)
        {
            case "btnUndo": return "";
            case "cbNewSlabUse": case "txtBalconyDepth": case "txtRailing": case "btnStair": case "cbEditBuildUp": return RtSan;
            case "cbEditBeamSec": case "cbCurveMode": case "txtCurveValue": return RtDam;
            case "cbEditWallType": case "txtEditWallH": case "txtEditWallOpen": return RtTuong;
        }
        if (text.StartsWith("Hạ cốt")) return RtSan;
        if (text.StartsWith("Uốn cong")) return RtDam;
        return null;   // chưa rõ – theo ngữ cảnh (nhãn đi với ô kế tiếp, mặc định theo thanh nguồn)
    }

    private void BuildMemberRibbon()
    {
        if (planEditBar?.Parent is not Border host || host.Parent is not DockPanel dock) return;
        var beamBar = _beamToolsExpander?.Content as Panel;
        var beamBorder = _beamToolsExpander?.Parent as Border;

        var panels = new Dictionary<string, WrapPanel>();
        WrapPanel P(string key) => panels.TryGetValue(key, out var w) ? w : panels[key] = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 2, 2, 0) };
        foreach (var k in new[] { RtLuoi, RtMong, RtCot, RtDam, RtSan, RtTuong, RtCad }) P(k);
        var common = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(2, 4, 6, 0) };

        // ---- chuyển các phần tử có sẵn vào tab ----
        void Move(Panel src, string fallback, Func<UIElement, string> special = null)
        {
            if (src == null) return;
            var items = src.Children.OfType<UIElement>().ToList();
            var pending = new List<UIElement>();
            foreach (var el in items)
            {
                string cat = special?.Invoke(el) ?? CategoryOf(el);
                if (el is TextBlock && cat == null) { pending.Add(el); continue; }
                cat ??= fallback;
                var dst = cat == "" ? (Panel)common : P(cat);
                foreach (var pe in pending) { src.Children.Remove(pe); dst.Children.Add(pe); }
                pending.Clear();
                src.Children.Remove(el); dst.Children.Add(el);
            }
            foreach (var pe in pending) { src.Children.Remove(pe); P(fallback).Children.Add(pe); }
        }
        Move(planEditBar, RtSan);
        string section = RtDam;
        Move(beamBar, RtDam, el =>
        {
            // thanh dầm kiểu ETABS: phạm vi tầng + bắt điểm + gõ toạ độ → tab Lưới – Tầng; Pick CAD → Link CAD
            string t = el is TextBlock tb ? tb.Text ?? "" : (el as ContentControl)?.Content as string ?? "";
            if (t.StartsWith("Phạm vi tầng") || t.StartsWith("Bắt điểm") || t.StartsWith("Gõ toạ độ")) section = RtLuoi;
            else if (t.StartsWith("Sửa dầm")) section = RtDam;
            else if (t.StartsWith("Pick CAD") || (el as FrameworkElement)?.Tag as string == "PickCad") section = RtCad;
            if (el is TextBlock tb2 && (tb2.Text ?? "").StartsWith("(＋ Dầm")) return RtDam;
            return CategoryOf(el) is string c && c != null && c != RtDam ? c : section;
        });
        host.Visibility = Visibility.Collapsed;
        if (beamBorder != null) beamBorder.Visibility = Visibility.Collapsed;

        // ---- cụm chọn chung ----
        common.Children.Add(SmallBtn("Chọn tương tự ▾", null, "Chọn mọi đối tượng cùng tiết diện / cùng loại (công năng) / cùng tầng với các đối tượng đang chọn", out var simBtn));
        simBtn.Click += (s, e) => ShowSimilarMenu(simBtn);
        common.Children.Add(SmallBtn("Bỏ chọn", ClearMemberSelection, "Bỏ mọi đối tượng đang chọn (Esc)", out _));

        // ---- nhóm thêm cho từng tab ----
        BuildGridTab(P(RtLuoi));
        BuildFootingTab(P(RtMong));
        BuildColumnTab(P(RtCot));
        AddLoadGroup(P(RtDam), RtDam);
        AddLoadGroup(P(RtSan), RtSan);
        BuildWallTab(P(RtTuong));
        BuildCadTab(P(RtCad));

        _ribbon = new TabControl { Padding = new Thickness(0), BorderThickness = new Thickness(0, 1, 0, 0), MinHeight = 30 };
        OneRowTabs.Apply(_ribbon);   // [Hoàn thiện] tiêu đề tab một hàng (không xuống dòng khi khung hẹp)
        foreach (var k in new[] { RtLuoi, RtMong, RtCot, RtDam, RtSan, RtTuong, RtCad })
        {
            var sv = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 92, Content = P(k) };
            _ribbon.Items.Add(new TabItem { Header = k, Content = sv, Padding = new Thickness(8, 1, 8, 1), ToolTip = RibbonTip(k) });
        }
        _ribbon.SelectedIndex = 3;   // Dầm
        _ribbon.SelectionChanged += (s, e) =>
        {
            if (!ReferenceEquals(e.OriginalSource, _ribbon)) return;
            _selFilter = ((_ribbon.SelectedItem as TabItem)?.Header as string) switch
            {
                RtDam => SelectFilter.Beams, RtCot => SelectFilter.Columns, RtMong => SelectFilter.Footings, RtSan => SelectFilter.Slabs, _ => SelectFilter.All
            };
            UpdateRibbonInfo();
        };
        _selFilter = SelectFilter.Beams;
        // nháy đúp tên tab: thu gọn / mở lại nội dung thanh lệnh (như ribbon Office) – thêm chỗ cho mặt bằng
        _ribbon.PreviewMouseDoubleClick += (s, e) =>
        {
            DependencyObject d = e.OriginalSource as DependencyObject;
            while (d != null && d is not TabPanel && d is not ScrollViewer) d = VisualTreeHelper.GetParent(d);
            if (d is not TabPanel) return;
            _ribbonCollapsed = !_ribbonCollapsed;
            foreach (var ti in _ribbon.Items.OfType<TabItem>()) if (ti.Content is ScrollViewer sv) sv.Visibility = _ribbonCollapsed ? Visibility.Collapsed : Visibility.Visible;
            SetStatus(_ribbonCollapsed ? "Đã thu gọn thanh lệnh – nháy đúp tên tab để mở lại." : "Đã mở thanh lệnh.");
            e.Handled = true;
        };

        var rib = new DockPanel { LastChildFill = true };
        var left = new Border { Child = common, BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204)), BorderThickness = new Thickness(0, 0, 1, 0), Width = 214 };
        DockPanel.SetDock(left, Dock.Left);
        rib.Children.Add(left);
        rib.Children.Add(_ribbon);
        var ribHost = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(243, 245, 248)), BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
            BorderThickness = new Thickness(0, 0, 0, 1), Child = rib, Tag = "MemberRibbon"
        };
        // kiểu nút chế độ (RadioButton dạng ToggleButton, GroupName "PlanMode") khai báo trong Resources của planEditBar (XAML)
        foreach (var key in planEditBar.Resources.Keys.Cast<object>().ToList()) ribHost.Resources[key] = planEditBar.Resources[key];
        DockPanel.SetDock(ribHost, Dock.Top);
        dock.Children.Insert(dock.Children.IndexOf(host), ribHost);
        _ribbonHost = ribHost;
    }

    private static string RibbonTip(string k) => k switch
    {
        RtLuoi => "Phạm vi tầng, bắt điểm, đơn vị gõ toạ độ, lấy lưới trục từ mặt bằng",
        RtMong => "Chọn chân cột, gán tải chân cột (móng), thiết kế móng",
        RtCot => "Đặt cột, gán tiết diện cột, gán tải đầu cột",
        RtDam => "Vẽ / sửa dầm, tiết diện, tải phân bố – tập trung trên dầm (kể cả tải tường)",
        RtSan => "Ô sàn, ban công, cầu thang, giếng trời, hạ cốt, cấu tạo, tải sàn theo công năng",
        RtTuong => "Vẽ / gán tường – tải tường dồn vào dầm",
        _ => "Bản vẽ AutoCAD liên kết làm nền (xref) theo tầng, bắt điểm, tường CAD → tải dầm"
    };

    private Button SmallBtn(string text, Action act, string tip, out Button b)
    {
        b = new Button { Content = text, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 3, 2), ToolTip = tip };
        if (act != null) { var a = act; b.Click += (s, e) => a(); }
        return b;
    }

    private static TextBlock RLab(string t, bool bold = false) => new()
    {
        Text = t, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 3, 2), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal
    };

    private static TextBox RBox(string text, double w, string tip) => new()
    {
        Text = text, Width = w, Margin = new Thickness(0, 0, 4, 2), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = tip
    };

    private static Separator RSep() => new() { Margin = new Thickness(6, 2, 6, 4), Width = 1, Height = 18, Background = Brushes.LightGray };

    // ==================================================================
    // Tab Lưới – Tầng, Cột, Móng, Tường, Link CAD
    // ==================================================================
    private void BuildGridTab(WrapPanel w)
    {
        w.Children.Insert(0, SmallBtn("Lấy lưới từ mặt bằng CAD…", () => { try { BtnGridFromPlan_Click(this, new RoutedEventArgs()); } catch (Exception ex) { ShowError("Lấy lưới", ex); } },
            "Mở mặt bằng DWG/DXF/ảnh để lấy trục, bước cột, cột, tường (như nút 'Tải mặt bằng để lấy kích thước…')", out _));
        w.Children.Insert(1, SmallBtn("Lưới trục + cột + tầng…", OpenGridColumnDialog,
            "Một bảng: bước trục X/Y, tên trục, đặt / bỏ cột tại từng giao điểm, chiều cao từng tầng (đổi chiều cao giữ nguyên mô hình)", out _));
        w.Children.Insert(2, SmallBtn("Chiều cao tầng…", StoryHeightsUi, "Sửa chiều cao, tên từng tầng của mô hình đang có – giữ nguyên mô hình", out _));
        w.Children.Insert(3, RSep());
    }

    private void BuildColumnTab(WrapPanel w)
    {
        var rb = new RadioButton { Content = "＋ Cột", Tag = "CmdColumn", GroupName = "PlanMode", ToolTip = "Bấm điểm để đặt cột (bắt nút, giao trục, nét CAD) – gõ lệnh C", Margin = new Thickness(0, 0, 3, 2), Padding = new Thickness(7, 2, 7, 2) };
        if (TryFindResource(typeof(ToggleButton)) is Style tg) rb.Style = tg;
        rb.Checked += (s, e) => StartCadCommand(CadCmd.Column);
        w.Children.Add(rb);
        w.Children.Add(RLab("Tiết diện cột:"));
        _cbColSec = new ComboBox { Width = 90, Margin = new Thickness(0, 0, 4, 2), ToolTip = "Tiết diện cột đặt mới / gán cho cột đang chọn" };
        _cbColSec.DropDownOpened += (s, e) => FillSectionCombo(_cbColSec, true);
        w.Children.Add(_cbColSec);
        w.Children.Add(SmallBtn("Gán tiết diện", AssignColumnSection, "Gán tiết diện cột cho các cột đang chọn", out _));
        w.Children.Add(SmallBtn("Chọn mọi cột tầng", () => SelectAllOfKind(SelectFilter.Columns), "Chọn mọi cột của tầng đang xem", out _));
        w.Children.Add(RSep());
        AddLoadGroup(w, RtCot);
    }

    private void BuildFootingTab(WrapPanel w)
    {
        w.Children.Add(SmallBtn("Chọn mọi chân cột", () => SelectAllOfKind(SelectFilter.Footings), "Chọn mọi cột của tầng đang xem để gán tải chân cột (móng)", out _));
        w.Children.Add(SmallBtn("Thiết kế móng…", () => MnuDesignFoundation_Click(this, new RoutedEventArgs()), "Thông số móng, đất nền (sức chịu tải), loại móng – tải chân cột lấy từ phân tích", out _));
        w.Children.Add(SmallBtn("Xoá móng đã thiết kế…", FndDeleteAll, "Xoá toàn bộ móng đã thiết kế (kích thước đã chốt, bố trí cọc, thép) để thiết kế lại – xoá từng móng: tab Mô hình móng, chọn móng, Delete", out _));
        w.Children.Add(SmallBtn("Thiết kế lại móng", FndRedesign, "Xoá toàn bộ móng đã thiết kế rồi thiết kế lại từ đầu", out _));
        w.Children.Add(RSep());
        AddLoadGroup(w, RtMong);
    }

    private void BuildWallTab(WrapPanel w)
    {
        w.Children.Add(RSep());
        w.Children.Add(RLab("q tường =", true));
        var info = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 2), Foreground = Brushes.DarkSlateBlue };
        void Upd() { info.Text = $"{WallQFromToolbar():0.##} kN/m"; }
        cbEditWallType.SelectionChanged += (s, e) => Upd();
        txtEditWallH.TextChanged += (s, e) => Upd();
        txtEditWallOpen.TextChanged += (s, e) => Upd();
        info.Loaded += (s, e) => Upd();
        w.Children.Add(info);
        w.Children.Add(SmallBtn("Gán q tường cho dầm chọn", () =>
        {
            if (_ribbon?.Items.OfType<TabItem>().FirstOrDefault(t => (string)t.Header == RtDam) is TabItem ti)
            {
                if (_loadBoxes.TryGetValue(RtDam, out var lb)) lb.V1.Text = WallQFromToolbar().ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                _ribbon.SelectedItem = ti;
                SetStatus("Đã điền q tường vào ô tải phân bố tab Dầm – chọn dầm, trường hợp tải (TT/Tường) rồi bấm 'Gán'. Hoặc dùng 'Gán tường' để tải tự dồn khi phân tích.");
            }
        }, "Tính tải tường trên dầm q = g·h·(1 − tỷ lệ cửa) theo loại tường / chiều cao / cửa đang chọn rồi chuyển sang tab Dầm để gán", out _));
    }

    /// <summary>Tải tường (kN/m) theo loại tường, chiều cao (0 = tầng trên − 0,35 m), diện tích cửa của thanh công cụ Tường.</summary>
    private double WallQFromToolbar()
    {
        if (_p == null) return 0;
        var (type, h, open) = WallParams();
        var t = _p.WallTypes.FirstOrDefault(q => q.Name == type);
        if (t == null) return 0;
        if (h <= 0)
        {
            var st = _p.Stories.OrderBy(s => s.Elevation).ToList();
            int i = st.FindIndex(s => s.Name == CurrentStory);
            h = i >= 0 && i + 1 < st.Count ? Math.Max(0.5, st[i + 1].Height - 0.35) : 3.0;
        }
        // diện tích cửa (m²) của một đoạn tường cụ thể không quy được ra tỷ lệ khi chưa biết chiều dài → dùng tỷ lệ cửa của loại tường
        return LoadAssign.WallLineLoad(t, h, -1);
    }

    private void BuildCadTab(WrapPanel w)
    {
        int at = 0;
        void Ins(UIElement el) => w.Children.Insert(at++, el);
        Ins(SmallBtn("📎 Liên kết DWG/DXF…", async () => { try { await CadLinkAddInteractive(); RedrawPlan(); } catch (Exception ex) { ShowError("Liên kết bản vẽ", ex); } },
            "Gắn bản vẽ AutoCAD làm nền mặt bằng (như xref) cho tầng đang xem – bắt điểm vào nét bản vẽ khi vẽ dầm, cột, tường, sàn", out _));
        Ins(SmallBtn("Quản lý…", () => OpenCadLinkManager(), "Danh sách bản vẽ liên kết: tầng, layer bật/tắt, màu, dời / xoay / tỷ lệ, gỡ liên kết", out _));
        Ins(SmallBtn("Ẩn / hiện", () => ForStoryLinks(l => l.Visible = !l.Visible, "Đã ẩn / hiện bản vẽ nền."), "Ẩn hoặc hiện bản vẽ nền của tầng đang xem", out _));
        Ins(RLab("Độ mờ:"));
        _clOpacity = new Slider { Minimum = 0.1, Maximum = 1, Value = 0.55, Width = 70, Margin = new Thickness(0, 0, 6, 2), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Độ đậm nét bản vẽ nền" };
        _clOpacity.ValueChanged += (s, e) => { if (!_clSyncing) ForStoryLinks(l => l.Opacity = Math.Round(_clOpacity.Value, 2), null); };
        Ins(_clOpacity);
        Ins(SmallBtn("Đọc lại", async () =>
        {
            if (_p == null) return;
            foreach (var l in StoryLinks().ToList()) await ReloadCadLinkAsync(l, true, true);
            RedrawPlan();
        }, "Đọc lại bản vẽ nền từ file (sau khi sửa trong AutoCAD)", out _));
        Ins(SmallBtn("Căn 2 điểm", () =>
        {
            var l = StoryLinks().FirstOrDefault();
            if (l == null) { SetStatus("Tầng đang xem chưa có bản vẽ liên kết."); return; }
            if (l.Locked) { SetStatus($"Bản vẽ {l.DisplayName} đang khoá – bỏ khoá trước khi căn."); return; }
            CadLinkStartAlign(l);
        }, "Căn bản vẽ nền: bấm 1 điểm trên bản vẽ rồi điểm tương ứng trên mô hình (giao trục / nút)", out _));
        var lockBtn = new ToggleButton { Content = "🔒 Khoá", Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 3, 2), ToolTip = "Khoá vị trí bản vẽ nền (không căn / dời nhầm)" };
        // khoá lưu trong dự án (CadLink.Locked) – mở lại dự án vẫn giữ
        lockBtn.Checked += (s, e) => { if (!_clSyncing) ForStoryLinks(l => l.Locked = true, "Đã khoá vị trí bản vẽ nền (lưu trong dự án)."); };
        lockBtn.Unchecked += (s, e) => { if (!_clSyncing) ForStoryLinks(l => l.Locked = false, "Đã bỏ khoá bản vẽ nền."); };
        _clLockBtn = lockBtn;
        Ins(lockBtn);
        var snapBtn = new ToggleButton { Content = "Bắt điểm CAD", IsChecked = true, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 3, 2), ToolTip = "Bắt điểm vào nét bản vẽ nền: đầu nét □, trung điểm △, giao ×, tâm ○ (F3 bật/tắt mọi bắt điểm)" };
        snapBtn.Checked += (s, e) => CadLinkSnapEnabled = true; snapBtn.Unchecked += (s, e) => CadLinkSnapEnabled = false;
        Ins(snapBtn);
        Ins(RSep());
        w.Children.Add(RSep());
        w.Children.Add(SmallBtn("Tường CAD → tải dầm…", CadWallsToBeams, "Tìm cặp nét tường (layer tường, cách 110 / 220 mm) trên bản vẽ nền nằm trên dầm của tầng → tạo tường (tải tường dồn vào dầm khi phân tích)", out _));
    }

    private bool _clSyncing;

    /// <summary>Đồng bộ ô độ mờ / nút khoá của tab Link CAD theo bản vẽ nền của tầng đang xem (giá trị lưu trong dự án).</summary>
    private void SyncCadTabFromLinks()
    {
        if (_clOpacity == null && _clLockBtn == null) return;
        var l = StoryLinks().FirstOrDefault();
        _clSyncing = true;
        try
        {
            if (_clOpacity != null && l != null) _clOpacity.Value = Math.Clamp(l.Opacity, _clOpacity.Minimum, _clOpacity.Maximum);
            if (_clLockBtn != null) _clLockBtn.IsChecked = l != null && l.Locked;
        }
        finally { _clSyncing = false; }
    }

    private IEnumerable<CadLink> StoryLinks()
    {
        if (_p?.CadLinks == null) return Enumerable.Empty<CadLink>();
        string st = CurrentStory == AllStories ? null : CurrentStory;
        return _p.CadLinks.Where(l => l.AppliesTo(st));
    }

    private void ForStoryLinks(Action<CadLink> change, string msg)
    {
        var ls = StoryLinks().ToList();
        if (ls.Count == 0) { if (msg != null) SetStatus("Tầng đang xem chưa có bản vẽ liên kết – bấm 'Liên kết DWG/DXF…'."); return; }
        foreach (var l in ls) change(l);
        MarkDirty(false);
        RedrawPlan();
        if (msg != null) SetStatus(msg);
    }

    /// <summary>Nét tường (toạ độ mô hình) của bản vẽ nền tầng đang xem: layer nhận là Tường theo chuẩn layer / ánh xạ của link,
    /// hoặc tên layer chứa TUONG / WALL.</summary>
    private List<(double X1, double Y1, double X2, double Y2)> CadWallLines(string story)
    {
        var res = new List<(double, double, double, double)>();
        foreach (var (l, g) in CadLinkSnap.Active(_p, story))
        {
            var v = g.View(l);
            var std = CadStandard.Active;
            var wallLayer = new bool[g.Layers.Count];
            for (int i = 0; i < g.Layers.Count; i++)
            {
                string name = g.Layers[i] ?? "";
                string cat = "";
                try { cat = std != null ? CadStandard.Classify(name, std.InputRules, (l.LayerMap ?? new()).Concat(std.InputLayerMap), out _) : ""; } catch (Exception) { }
                string fold = CommandSearch.Fold(name).ToUpperInvariant();
                wallLayer[i] = cat == CadCategory.Tuong || fold.Contains("TUONG") || fold.Contains("WALL");
            }
            for (int k = 0; k < v.SegCount; k++)
            {
                int lay = v.SegLayer[k];
                if (lay < 0 || lay >= wallLayer.Length || !wallLayer[lay] || v.SegArc[k]) continue;
                res.Add((v.Seg[4 * k], v.Seg[4 * k + 1], v.Seg[4 * k + 2], v.Seg[4 * k + 3]));
            }
        }
        return res;
    }

    private void CadWallsToBeams()
    {
        if (_p == null) return;
        string st = CurrentStory;
        if (st == AllStories || _p.Stories.All(q => q.Name != st)) { SetStatus("Chọn một tầng cụ thể trước."); return; }
        var lines = CadWallLines(st);
        if (lines.Count == 0) { SetStatus("Không thấy nét tường trên bản vẽ nền của tầng (layer tường chưa được nhận – dùng 'Chuẩn hoá layer' hoặc đặt nhóm Tường cho layer ở 'Quản lý…')."); return; }
        var props = LoadAssign.CadWallsOverBeams(_p, st, lines);
        // bỏ đề xuất trùng tường đã có
        props = props.Where(w => !_p.Walls.Any(x => x.Story == st && Math.Abs(x.X1 - w.X1) + Math.Abs(x.Y1 - w.Y1) + Math.Abs(x.X2 - w.X2) + Math.Abs(x.Y2 - w.Y2) < 0.05)).ToList();
        if (props.Count == 0) { SetStatus($"Đọc {lines.Count} nét tường nhưng không có tường nào nằm trên dầm tầng {st} (hoặc đã tạo)."); return; }
        string list = string.Join("\n", props.Take(14).Select(w => "• " + w)) + (props.Count > 14 ? $"\n… và {props.Count - 14} đoạn khác" : "");
        if (MessageBox.Show(this, $"Tìm thấy {props.Count} đoạn tường trên dầm tầng {st}:\n\n{list}\n\nTạo tường (tải tường dồn vào dầm khi phân tích)?",
                AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var (_, h, _) = WallParams();
        RunBeamOp(() => LoadAssign.ApplyCadWalls(_p, st, props, h), false);
    }

    private void FillSectionCombo(ComboBox cb, bool columns)
    {
        if (_p == null) return;
        string cur = cb.SelectedItem as string;
        var names = _p.FrameSections.Select(s => s.Name)
            .OrderBy(n => _p.Frames.Any(f => f.Section == n && f.IsVerticalKind == columns) ? 0 : 1).ThenBy(n => n).ToList();
        cb.ItemsSource = names;
        cb.SelectedItem = cur != null && names.Contains(cur) ? cur
            : _p.Frames.Where(f => f.IsVerticalKind == columns).GroupBy(f => f.Section).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? names.FirstOrDefault();
    }

    private string ColumnSectionCurrent()
    {
        if (_cbColSec != null && _cbColSec.SelectedItem == null) FillSectionCombo(_cbColSec, true);
        return _cbColSec?.SelectedItem as string;
    }

    private void AssignColumnSection()
    {
        var ids = SelectedColumnIds();
        string sec = ColumnSectionCurrent();
        if (ids.Count == 0) { SetStatus("Chọn cột trước (tab Cột → bấm / kéo khung)."); return; }
        if (string.IsNullOrEmpty(sec)) { SetStatus("Chọn tiết diện cột."); return; }
        RunBeamOp(() => ModelAssign.Section(_p, ids, sec), false);
    }

    // ==================================================================
    // Chọn nhiều theo tab
    // ==================================================================
    private List<int> SelectedColumnIds()
    {
        if (_p == null) return new List<int>();
        CheckMultiSelOwner();
        var ids = _multiSel.Where(id => _p.Frames.Any(f => f.Id == id && f.IsVerticalKind)).ToList();
        if (ids.Count == 0 && _sel != null && _sel.IsVerticalKind && _p.Frames.Any(f => f.Id == _sel.Id)) ids.Add(_sel.Id);
        return ids;
    }

    private List<int> SelectedSlabIds()
    {
        if (_p == null) return new List<int>();
        var ids = _multiSlab.Where(id => _p.Slabs.Any(s => s.Id == id)).ToList();
        if (ids.Count == 0 && _selSlab != null && _p.Slabs.Any(s => s.Id == _selSlab.Id)) ids.Add(_selSlab.Id);
        return ids;
    }

    private void ClearMemberSelection()
    {
        _multiSel.Clear(); _multiSlab.Clear();
        ClearSelection();
        RedrawPlan();
        UpdateRibbonInfo();
        SetStatus("Đã bỏ chọn.");
    }

    private void SelectAllOfKind(SelectFilter k)
    {
        if (_p == null) return;
        CheckMultiSelOwner();
        string st = CurrentStory == AllStories ? null : CurrentStory;
        if (k == SelectFilter.Slabs) foreach (var s in _p.Slabs.Where(q => st == null || q.Story == st)) _multiSlab.Add(s.Id);
        else foreach (var f in _p.Frames.Where(q => (st == null || q.Story == st) && (k == SelectFilter.Beams ? !q.IsVerticalKind : q.IsVerticalKind))) _multiSel.Add(f.Id);
        EnterMultiSelect();
        RedrawPlan();
        UpdateRibbonInfo();
    }

    /// <summary>Bật chế độ "Chọn nhiều" (nút Chọn nhiều của thanh dầm) nếu đang ở chế độ khác.</summary>
    private void EnterMultiSelect()
    {
        if (_mode == PlanEditMode.MultiSelect) return;
        var rb = PlanModeButtons().FirstOrDefault(b => b.Tag as string == nameof(PlanEditMode.MultiSelect));
        if (rb != null) rb.IsChecked = true; else _mode = PlanEditMode.MultiSelect;
    }

    /// <summary>[BeamTools] bấm trong chế độ Chọn nhiều: cột / ô sàn theo tab đang mở. true = đã xử lý (không xét dầm).</summary>
    private bool MemberMultiClick(Point s)
    {
        if (_p == null) return false;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var (x, y) = S2W(s);
        bool wantCol = _selFilter is SelectFilter.All or SelectFilter.Columns or SelectFilter.Footings;
        bool wantSlab = _selFilter is SelectFilter.All or SelectFilter.Slabs;
        bool wantBeam = _selFilter is SelectFilter.All or SelectFilter.Beams;
        if (wantCol && ColumnAt(s) is FrameElement c)
        {
            if (!_multiSel.Remove(c.Id)) _multiSel.Add(c.Id);
            AfterMemberPick($"cột {c.Label}");
            return true;
        }
        if (wantBeam && NearestBeam(s, 8) != null) return false;   // dầm: xử lý như cũ
        if (wantSlab && SlabAtPlan(x, y) is Slab sl)
        {
            if (!_multiSlab.Remove(sl.Id)) _multiSlab.Add(sl.Id);
            AfterMemberPick($"ô sàn {sl.Label}");
            return true;
        }
        if (!ctrl) { _multiSlab.Clear(); if (!wantBeam) { _multiSel.Clear(); ClearSelection(); RedrawPlan(); UpdateRibbonInfo(); return true; } }
        return !wantBeam;
    }

    private void AfterMemberPick(string what)
    {
        RedrawPlan();
        UpdateRibbonInfo();
        SetStatus($"Đã bấm {what} – đang chọn {SelectionCountText()}. Bấm / kéo khung để thêm, Ctrl + kéo để bỏ; Esc bỏ chọn.");
    }

    private string SelectionCountText()
    {
        if (_p == null) return "";
        CheckMultiSelOwner();
        int nb = _multiSel.Count(id => _p.Frames.Any(f => f.Id == id && !f.IsVerticalKind));
        int nc = _multiSel.Count(id => _p.Frames.Any(f => f.Id == id && f.IsVerticalKind));
        int ns = _multiSlab.Count(id => _p.Slabs.Any(s => s.Id == id));
        var parts = new List<string>();
        if (nb > 0) parts.Add($"{nb} dầm"); if (nc > 0) parts.Add($"{nc} cột"); if (ns > 0) parts.Add($"{ns} ô sàn");
        return parts.Count == 0 ? "không có đối tượng" : string.Join(", ", parts);
    }

    /// <summary>[BeamTools] khung chọn trong chế độ Chọn nhiều: cột / ô sàn theo tab (dầm do mã cũ xử lý khi tab cho phép).
    /// Trả về số đối tượng chọn / bỏ.</summary>
    private int MemberWindowSelect(double xa, double ya, double xb, double yb, bool crossing, bool remove)
    {
        if (_p == null) return 0;
        string st = CurrentStory == AllStories ? null : CurrentStory;
        var box = SelectionRules.Box.Of(xa, ya, xb, yb);
        int n = 0;
        if (_selFilter is SelectFilter.All or SelectFilter.Columns or SelectFilter.Footings)
            foreach (int id in SelectionRules.Columns(_p, st, box, crossing)) { n++; if (remove) _multiSel.Remove(id); else _multiSel.Add(id); }
        if (_selFilter is SelectFilter.All or SelectFilter.Slabs)
            foreach (int id in SelectionRules.Slabs(_p, st, box, crossing)) { n++; if (remove) _multiSlab.Remove(id); else _multiSlab.Add(id); }
        if (n > 0) Dispatcher.BeginInvoke(new Action(() => { RedrawPlan(); UpdateRibbonInfo(); }), System.Windows.Threading.DispatcherPriority.Background);
        return n;
    }

    private bool BeamsAllowedInWindow => _selFilter is SelectFilter.All or SelectFilter.Beams;

    /// <summary>[BeamTools] tô các ô sàn đang chọn nhiều (lớp phủ).</summary>
    private void DrawMultiSlabOverlay()
    {
        if (_p == null || _multiSlab.Count == 0) return;
        foreach (var sl in PlanSlabs().Where(q => _multiSlab.Contains(q.Id)))
        {
            var pts = SlabPoints(sl, out _, out _);
            if (pts.Count < 3) continue;
            planOverlay.Children.Add(new Shapes.Polygon { Points = pts, Fill = SlabSelFill, Stroke = MultiSelBrush, StrokeThickness = 2, IsHitTestVisible = false });
        }
    }

    private void ShowSimilarMenu(Button anchor)
    {
        var cm = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom };
        void It(string h, SimilarBy by, bool allStories)
        {
            var mi = new MenuItem { Header = h };
            mi.Click += (s, e) => SelectSimilar(by, allStories);
            cm.Items.Add(mi);
        }
        It("Cùng tiết diện – tầng đang xem", SimilarBy.Section, false);
        It("Cùng tiết diện – mọi tầng", SimilarBy.Section, true);
        It("Cùng loại / công năng – tầng đang xem", SimilarBy.Kind, false);
        It("Cùng tầng (mọi đối tượng cùng loại)", SimilarBy.Story, false);
        cm.IsOpen = true;
    }

    private void SelectSimilar(SimilarBy by, bool allStories)
    {
        if (_p == null) return;
        CheckMultiSelOwner();
        string st = allStories || CurrentStory == AllStories ? null : CurrentStory;
        var seeds = _multiSel.ToList(); if (_sel != null) seeds.Add(_sel.Id);
        var slabSeeds = _multiSlab.ToList(); if (_selSlab != null) slabSeeds.Add(_selSlab.Id);
        if (seeds.Count == 0 && slabSeeds.Count == 0) { SetStatus("Chọn ít nhất một đối tượng mẫu trước khi 'Chọn tương tự'."); return; }
        foreach (int id in SelectionRules.SimilarFrames(_p, seeds, by, st)) _multiSel.Add(id);
        foreach (int id in SelectionRules.SimilarSlabs(_p, slabSeeds, by, st)) _multiSlab.Add(id);
        EnterMultiSelect();
        RedrawPlan();
        UpdateRibbonInfo();
        SetStatus($"Chọn tương tự: đang chọn {SelectionCountText()}.");
    }

    // ==================================================================
    // Nhóm gán tải trọng
    // ==================================================================
    private sealed class LoadBoxes
    {
        public ComboBox Pattern, Mode;
        public TextBox V1, V2, V3;
    }
    private readonly Dictionary<string, LoadBoxes> _loadBoxes = new();

    private void AddLoadGroup(WrapPanel w, string kind)
    {
        var b = new LoadBoxes();
        w.Children.Add(RSep());
        w.Children.Add(RLab("Gán tải:", true));
        b.Pattern = new ComboBox { Width = 92, Margin = new Thickness(0, 0, 4, 2), ToolTip = "Trường hợp tải (Khai báo → Trường hợp tải)" };
        b.Pattern.DropDownOpened += (s, e) => FillPatternCombo(b.Pattern);
        b.Pattern.SelectionChanged += (s, e) => UpdateRibbonInfo();
        b.Pattern.Loaded += (s, e) => FillPatternCombo(b.Pattern);
        w.Children.Add(b.Pattern);
        switch (kind)
        {
            case RtDam:
                w.Children.Add(RLab("q (kN/m):")); w.Children.Add(b.V1 = RBox("", 44, "Tải phân bố đều trên dầm (để trống = không gán tải phân bố)"));
                w.Children.Add(RLab("P (kN):")); w.Children.Add(b.V2 = RBox("", 40, "Lực tập trung (để trống = không gán)"));
                w.Children.Add(RLab("tại x/L:")); w.Children.Add(b.V3 = RBox("0.5", 34, "Vị trí lực tập trung theo tỷ lệ chiều dài từ đầu I (0…1)"));
                break;
            case RtSan:
                w.Children.Add(RLab("q (kN/m²):")); w.Children.Add(b.V1 = RBox("", 44, "Tải phân bố đều trên ô sàn"));
                break;
            default:   // cột (đầu cột) / móng (chân cột)
                w.Children.Add(RLab(kind == RtMong ? "N chân (kN):" : "N đầu (kN):")); w.Children.Add(b.V1 = RBox("", 44, "Lực dọc (dương = nén, hướng xuống)"));
                w.Children.Add(RLab("Mx:")); w.Children.Add(b.V2 = RBox("", 36, "Mô men quanh X (kN.m)"));
                w.Children.Add(RLab("My:")); w.Children.Add(b.V3 = RBox("", 36, "Mô men quanh Y (kN.m)"));
                break;
        }
        b.Mode = new ComboBox { Width = 84, Margin = new Thickness(0, 0, 4, 2), ToolTip = "Thay thế tải nhập tay cùng trường hợp / Cộng thêm / Xoá" };
        foreach (var m in new[] { "Thay thế", "Cộng thêm", "Xoá" }) b.Mode.Items.Add(m);
        b.Mode.SelectedIndex = 0;
        w.Children.Add(b.Mode);
        var go = new Button { Content = "Gán", FontWeight = FontWeights.SemiBold, Padding = new Thickness(10, 1, 10, 1), Margin = new Thickness(0, 0, 4, 2), ToolTip = "Gán cho mọi đối tượng đang chọn (một bước hoàn tác Ctrl+Z)" };
        go.Click += (s, e) => AssignRibbonLoad(kind);
        w.Children.Add(go);
        if (kind == RtSan)
        {
            w.Children.Add(SmallBtn("Theo công năng", AssignSlabLoadsByUse, "Gán tĩnh tải hoàn thiện / hoạt tải theo công năng từng ô (TCVN 2737) cho ô đang chọn – theo loại của trường hợp tải chọn", out _));
            w.Children.Add(RLab("tường trên sàn L (m):"));
            var tl = RBox("", 36, "Chiều dài tường xây trực tiếp trên ô sàn (không có dầm đỡ) – loại tường, chiều cao theo tab Tường");
            w.Children.Add(tl);
            w.Children.Add(SmallBtn("→ q quy đổi", () => WallOnSlab(tl), "Cộng tải tường quy đổi q = g·h·L/A (kN/m²) vào các ô sàn đang chọn", out _));
        }
        if (kind == RtDam) w.Children.Add(SmallBtn("q tường", () => { b.V1.Text = WallQFromToolbar().ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }, "Điền q = tải tường theo loại tường / chiều cao / cửa ở tab Tường – Tải", out _));
        var info = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 2), Foreground = Brushes.DimGray, FontSize = 11 };
        w.Children.Add(info);
        _ribbonInfo[kind] = info;
        _loadBoxes[kind] = b;
    }

    private void FillPatternCombo(ComboBox cb)
    {
        if (_p == null) return;
        string cur = cb.SelectedItem as string;
        var names = _p.Patterns.Select(q => q.Name).ToList();
        if (cb.ItemsSource is List<string> old && old.SequenceEqual(names) && cur != null) return;
        cb.ItemsSource = names;
        cb.SelectedItem = cur != null && names.Contains(cur) ? cur : names.FirstOrDefault(n => _p.Pattern(n)?.Type is LoadType.SuperDead) ?? names.FirstOrDefault();
    }

    private static double? RVal(TextBox tb)
    {
        string t = (tb?.Text ?? "").Trim().Replace(',', '.').Replace('−', '-');
        if (t.Length == 0) return null;
        return double.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : null;
    }

    private LoadAssignMode RibbonMode(LoadBoxes b) => b.Mode.SelectedIndex switch { 1 => LoadAssignMode.Add, 2 => LoadAssignMode.Delete, _ => LoadAssignMode.Replace };

    private void AssignRibbonLoad(string kind)
    {
        if (_p == null || !_loadBoxes.TryGetValue(kind, out var b)) return;
        if (_busy) { SetStatus("Chương trình đang tính toán – chờ xong rồi gán tải."); return; }
        string pat = b.Pattern.SelectedItem as string;
        if (pat == null) { FillPatternCombo(b.Pattern); pat = b.Pattern.SelectedItem as string; }
        if (pat == null) { SetStatus("Chưa có trường hợp tải."); return; }
        var mode = RibbonMode(b);
        double? v1 = RVal(b.V1), v2 = RVal(b.V2), v3 = RVal(b.V3);
        // giá trị khác nhau đang có: hỏi trước khi ghi đè
        string common = RibbonCommonText(kind, pat);
        if (mode == LoadAssignMode.Replace && common.Contains(LoadAssign.Different)
            && MessageBox.Show(this, $"Các đối tượng đang chọn có tải {pat} khác nhau ({common}).\nGhi đè bằng giá trị mới?", AppTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        string snap = Snapshot();
        var msgs = new List<string>();
        int changed = 0;
        try
        {
            switch (kind)
            {
                case RtDam:
                {
                    CheckMultiSelOwner();
                    var ids = SelectedBeamIds();
                    if (ids.Count == 0) { SetStatus("Chọn dầm trước (tab Dầm → 'Chọn nhiều', bấm / kéo khung)."); return; }
                    if (v1 == null && v2 == null && mode != LoadAssignMode.Delete) { SetStatus("Nhập q và/hoặc P."); return; }
                    if (v1 != null || mode == LoadAssignMode.Delete) { var r = LoadAssign.FrameUniform(_p, ids, pat, v1 ?? 0, mode); msgs.Add(r.Message); changed += r.Changed; }
                    if (v2 != null || mode == LoadAssignMode.Delete) { var r = LoadAssign.FramePoint(_p, ids, pat, v2 ?? 0, v3 ?? 0.5, mode); msgs.Add(r.Message); changed += r.Changed; }
                    break;
                }
                case RtSan:
                {
                    var ids = SelectedSlabIds();
                    if (ids.Count == 0) { SetStatus("Chọn ô sàn trước (tab Sàn → 'Chọn nhiều', bấm / kéo khung)."); return; }
                    if (v1 == null && mode != LoadAssignMode.Delete) { SetStatus("Nhập q (kN/m²)."); return; }
                    var r = LoadAssign.Slabs(_p, ids, pat, v1 ?? 0, mode); msgs.Add(r.Message); changed += r.Changed;
                    break;
                }
                default:
                {
                    var cols = SelectedColumnIds();
                    if (cols.Count == 0) { SetStatus("Chọn cột trước (tab Cột / Móng → 'Chọn nhiều', bấm / kéo khung)."); return; }
                    var nodes = cols.Select(id => _p.Frames.First(f => f.Id == id)).Select(f => kind == RtMong ? LoadAssign.ColumnNodes(_p, f).Bottom : LoadAssign.ColumnNodes(_p, f).Top).ToList();
                    var r = LoadAssign.Nodes(_p, nodes, pat, v1 ?? 0, v2 ?? 0, v3 ?? 0, mode); msgs.Add(r.Message); changed += r.Changed;
                    break;
                }
            }
        }
        catch (Exception ex) { RestoreSnapshot(snap); ShowError("Không gán được tải", ex); return; }
        if (changed == 0) { if (Snapshot() != snap) RestoreSnapshot(snap); SetStatus(string.Join(" ", msgs)); return; }
        PushUndo(snap);
        MarkDirty();
        _showAssignedLoads = true;
        RedrawPlan();
        UpdateRibbonInfo();
        SetStatus(string.Join(" ", msgs.Where(m => m.Length > 0)) + " (Ctrl+Z hoàn tác)");
    }

    private void AssignSlabLoadsByUse()
    {
        if (_p == null || !_loadBoxes.TryGetValue(RtSan, out var b)) return;
        var ids = SelectedSlabIds();
        if (ids.Count == 0) { SetStatus("Chọn ô sàn trước."); return; }
        string pat = b.Pattern.SelectedItem as string;
        var lp = pat != null ? _p.Pattern(pat) : null;
        if (lp == null) { SetStatus("Chọn trường hợp tải."); return; }
        bool live = lp.Type is LoadType.Live or LoadType.RoofLive;
        string snap = Snapshot();
        int n = 0;
        foreach (var g in ids.Select(id => _p.Slabs.First(s => s.Id == id)).GroupBy(s => ModelEdit.UseOf(_p, s)))
        {
            var (finish, liveQ, name) = ModelEdit.Loads(g.Key);
            n += LoadAssign.Slabs(_p, g.Select(s => s.Id), pat, live ? liveQ : finish, RibbonMode(b), name).Changed;
        }
        if (n == 0) { SetStatus("Không gán được."); return; }
        PushUndo(snap); MarkDirty(); _showAssignedLoads = true; RedrawPlan(); UpdateRibbonInfo();
        SetStatus($"Đã gán {(live ? "hoạt tải" : "tĩnh tải hoàn thiện")} theo công năng cho {n} ô sàn ({pat}) – TCVN 2737:2023. Ctrl+Z hoàn tác.");
    }

    private void WallOnSlab(TextBox lenBox)
    {
        if (_p == null || !_loadBoxes.TryGetValue(RtSan, out var b)) return;
        var ids = SelectedSlabIds();
        double? L = RVal(lenBox);
        if (ids.Count == 0 || L is not > 0) { SetStatus("Chọn ô sàn và nhập chiều dài tường L (m)."); return; }
        string pat = b.Pattern.SelectedItem as string;
        if (pat == null) { SetStatus("Chọn trường hợp tải."); return; }
        var (type, h, _) = WallParams();
        var t = _p.WallTypes.FirstOrDefault(q => q.Name == type);
        if (t == null) { SetStatus("Chọn loại tường ở tab Tường – Tải."); return; }
        if (h <= 0) h = 3.0;
        string snap = Snapshot();
        int n = 0;
        foreach (var s in ids.Select(id => _p.Slabs.First(q => q.Id == id)))
        {
            double q = LoadAssign.WallOnSlabEquivalent(t.WeightPerM2, h, L.Value, LoadAssign.SlabArea(_p, s), t.OpeningRatio);
            n += LoadAssign.Slabs(_p, new[] { s.Id }, pat, Math.Round(q, 3), LoadAssignMode.Add, $"Tường {t.Name} L={L:0.##} m quy đổi").Changed;
        }
        PushUndo(snap); MarkDirty(); _showAssignedLoads = true; RedrawPlan(); UpdateRibbonInfo();
        SetStatus($"Đã cộng tải tường trên sàn quy đổi (q = g·h·L/A) vào {n} ô sàn – {pat}.");
    }

    /// <summary>Giá trị chung của tải đang có trên các đối tượng đang chọn (tab kind, trường hợp pat).</summary>
    private string RibbonCommonText(string kind, string pat)
    {
        if (_p == null || pat == null) return "";
        switch (kind)
        {
            case RtDam:
            {
                var ids = SelectedBeamIds();
                return ids.Count == 0 ? "" : $"{ids.Count} dầm, q = {LoadAssign.Common(ids.Select(id => LoadAssign.FrameUniformOf(_p, id, pat)), "kN/m")}";
            }
            case RtSan:
            {
                var ids = SelectedSlabIds();
                return ids.Count == 0 ? "" : $"{ids.Count} ô sàn, q = {LoadAssign.Common(ids.Select(id => LoadAssign.SlabQOf(_p, id, pat)), "kN/m²")}";
            }
            default:
            {
                var cols = SelectedColumnIds();
                if (cols.Count == 0) return "";
                var nodes = cols.Select(id => _p.Frames.First(f => f.Id == id)).Select(f => kind == RtMong ? LoadAssign.ColumnNodes(_p, f).Bottom : LoadAssign.ColumnNodes(_p, f).Top);
                return $"{cols.Count} cột, N = {LoadAssign.Common(nodes.Select(n => LoadAssign.NodeFzOf(_p, n, pat)), "kN")}";
            }
        }
    }

    private void UpdateRibbonInfo()
    {
        if (_p == null) return;
        foreach (var (kind, tb) in _ribbonInfo)
        {
            if (!_loadBoxes.TryGetValue(kind, out var b)) continue;
            string t = RibbonCommonText(kind, b.Pattern.SelectedItem as string);
            tb.Text = t.Length == 0 ? "(chưa chọn đối tượng)" : "Đang chọn: " + t;
            tb.Foreground = t.Contains(LoadAssign.Different) ? Brushes.DarkOrange : Brushes.DimGray;
        }
    }
}

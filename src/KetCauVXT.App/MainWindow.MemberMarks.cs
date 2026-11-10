using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.App;

// =====================================================================
// [Chỉ chọn] Nút "Chỉ chọn" (luôn hiện bên trái ribbon + thanh công cụ mặt bằng): thoát mọi lệnh vẽ / đặt, về chế độ Chọn và KHOÁ
// kéo dời (tay nắm, kéo trục) – bấm / quét khung chỉ để chọn, không di chuyển, không tạo đối tượng. Bấm lại để mở khoá.
// Esc: thoát lệnh, về chế độ Chọn ngay (MainWindow.EscCommand.cs).
// [Tên cấu kiện] Đặt một tên cho các cột / dầm / móng cùng tiết diện + chiều dài (Core: Design/MemberMarks.cs).
// =====================================================================
public partial class MainWindow
{
    private bool _selectOnly;
    private Button _rvSelectOnly;

    private FrameworkElement SelectOnlyRibbonButton()
    {
        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        sp.Children.Add(new TextBlock { Text = "", FontFamily = new FontFamily("Segoe MDL2 Assets, Segoe Fluent Icons, Segoe UI Symbol"), FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = "Chỉ chọn", FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center });
        _rvSelectOnly = new Button
        {
            Content = sp, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 0, 6), Focusable = false,
            ToolTip = "Chỉ chọn đối tượng (Esc thoát lệnh về chế độ Chọn): thoát lệnh vẽ đang chạy; bấm / quét khung chỉ để chọn – không kéo dời, không tạo đối tượng. Bấm lại để mở khoá kéo dời."
        };
        _rvSelectOnly.Click += (s, e) => EnterSelectOnly();
        return _rvSelectOnly;
    }

    private void TbSelectOnly_Click(object sender, RoutedEventArgs e) => EnterSelectOnly();

    /// <summary>Bấm "Chỉ chọn": đang có lệnh → thoát lệnh, bật khoá; đang ở chế độ Chọn → bật / tắt khoá kéo dời.</summary>
    private void EnterSelectOnly()
    {
        bool wasDrawing = DrawCommandActive();
        if (wasDrawing) EscapeDrawCommand();
        if (DrawCommandActive())
        {
            // lệnh không tự thoát được bằng Esc → ép về Chọn
            _rvPlaceType = null; _gridTool = false; _secTool = false; _lineStart = null;
        }
        if (rbModeSelect != null && rbModeSelect.IsChecked != true) rbModeSelect.IsChecked = true;
        _mode = PlanEditMode.Select;
        _selectOnly = wasDrawing || !_selectOnly;
        if (planHost != null) planHost.Cursor = null;
        DrawHover();
        UpdateSelectOnlyButtons();
        SetStatus(_selectOnly
            ? "CHỈ CHỌN: bấm để chọn; kéo khung trái → phải chọn đối tượng nằm trọn trong khung, phải → trái chọn đối tượng chạm khung (Ctrl + kéo: thêm, Shift + kéo: bỏ). Không kéo dời, không tạo đối tượng. Bấm \"Chỉ chọn\" lần nữa để mở khoá."
            : "Chế độ Chọn (đã mở khoá: kéo tay nắm để sửa đối tượng).");
    }

    private void UpdateSelectOnlyButtons()
    {
        if (tbSelectOnly != null) tbSelectOnly.IsChecked = _selectOnly && _mode == PlanEditMode.Select;
        if (_rvSelectOnly != null)
        {
            bool on = _selectOnly && _mode == PlanEditMode.Select;
            _rvSelectOnly.Background = on ? new SolidColorBrush(Color.FromRgb(204, 228, 247)) : SystemColors.ControlBrush;
            _rvSelectOnly.BorderBrush = on ? new SolidColorBrush(Color.FromRgb(0, 120, 215)) : SystemColors.ControlDarkBrush;
        }
    }

    // ------------------------------------------------------------------
    // ĐẶT TÊN CẤU KIỆN
    // ------------------------------------------------------------------
    private sealed class MarkRow
    {
        public string Kind { get; set; } = "";
        public string Mark { get; set; } = "";
        public string Section { get; set; } = "";
        public string Length { get; set; } = "";
        public int Count { get; set; }
        public string Where { get; set; } = "";
        public MarkGroup G { get; set; }
    }

    private void MnuMemberMarks_Click(object sender, RoutedEventArgs e) => MemberMarksUi();

    private void MemberMarksUi()
    {
        if (_p == null) return;
        var w = new Window { Title = "Đặt tên cấu kiện (cột, dầm, móng cùng tiết diện + chiều dài)", Owner = this, Width = 1020, Height = 640, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var root = new DockPanel { Margin = new Thickness(10) };
        var hint = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8),
            Text = "Cấu kiện cùng tên phải cùng tiết diện và cùng chiều dài: cột so cả chồng cột (tiết diện, chiều cao từng tầng); dầm so cả dải dầm (tiết diện, chiều dài từng nhịp, sai số 10 mm); "
                 + "móng đơn so L × B × H (gán chung một tên → đồng nhất kích thước theo móng lớn nhất). Khi thiết kế, các cấu kiện cùng tên dùng chung MỘT phương án thép – phương án nhẹ nhất "
                 + "bao được As yêu cầu của mọi cấu kiện trong nhóm – bản vẽ một hình, SL = số cấu kiện."
        };
        DockPanel.SetDock(hint, Dock.Top);
        root.Children.Add(hint);

        var chkC = new CheckBox { Content = "Cột", IsChecked = true, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        var chkD = new CheckBox { Content = "Dầm", IsChecked = true, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        var chkM = new CheckBox { Content = "Móng", IsChecked = true, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        var chkStory = new CheckBox { Content = "Dầm: tên riêng từng tầng", IsChecked = false, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Bỏ chọn: dầm cùng tiết diện + chiều dài ở mọi tầng chung một tên (thép theo tầng bất lợi nhất)" };
        var chkKeep = new CheckBox { Content = "Giữ tên đã gán tay", IsChecked = true, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        var txtName = new TextBox { Width = 90, Margin = new Thickness(4, 0, 6, 0), VerticalContentAlignment = VerticalAlignment.Center };
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Extended, HeadersVisibility = DataGridHeadersVisibility.Column };
        foreach (var (h, b, wd) in new[] { ("Loại", "Kind", 60.0), ("Tên", "Mark", 60.0), ("Tiết diện / kích thước", "Section", 170.0), ("Chiều dài (m)", "Length", 210.0), ("SL", "Count", 40.0), ("Vị trí / tầng", "Where", 420.0) })
            grid.Columns.Add(new DataGridTextColumn { Header = h, Binding = new System.Windows.Data.Binding(b), Width = wd });
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), Foreground = Brushes.DarkSlateBlue };

        void Reload()
        {
            var gs = MemberMarks.Groups(_p, _design);
            grid.ItemsSource = gs.Select(g => new MarkRow { Kind = g.Kind, Mark = g.Mark, Section = g.Section, Length = g.Length, Count = g.Count, Where = g.Where, G = g }).ToList();
            var conf = MemberMarks.Conflicts(_p);
            if (conf.Count > 0) status.Text = string.Join("\n", conf);
        }
        void Changed(string snap, string msg)
        {
            PushUndo(snap); MarkDirty(); _design = null; RefreshAll(false);
            status.Text = msg + " Thiết kế lại để cập nhật tên trên bản vẽ / bảng thép.";
            SetStatus(msg);
            Reload();
        }

        var bAuto = DialogUi.Btn("Tự đặt tên theo nhóm", (s, e) =>
        {
            string snap = Snapshot();
            if (chkKeep.IsChecked != true) MemberMarks.ClearAll(_p, chkC.IsChecked == true, chkD.IsChecked == true, chkM.IsChecked == true);
            var gs = MemberMarks.AutoAssign(_p, _design, chkC.IsChecked == true, chkD.IsChecked == true, chkM.IsChecked == true, chkKeep.IsChecked == true, chkStory.IsChecked == true);
            Changed(snap, $"Đã đặt {gs.Count} tên: " + string.Join(", ", gs.GroupBy(g => g.Kind).Select(g => $"{g.Count()} tên {g.Key.ToLowerInvariant()} ({g.Sum(x => x.Count)} cấu kiện)")) + ".");
        }, true);
        var bAssign = DialogUi.Btn("Gán tên cho đối tượng đang chọn", (s, e) =>
        {
            string mk = txtName.Text.Trim();
            if (mk.Length == 0) { status.Text = "Nhập tên (vd. C1, D3, M2) trước."; return; }
            var frames = new HashSet<int>(_multiSel);
            if (_sel != null) frames.Add(_sel.Id);
            var fnodes = SelectedFoundationNodes();
            string snap = Snapshot();
            string msg;
            if (fnodes.Count > 0 && frames.Count == 0) msg = MemberMarks.AssignFootings(_p, _design, fnodes, mk);
            else
            {
                msg = MemberMarks.AssignFrames(_p, frames, mk);
                if (msg.StartsWith("Không gán"))
                {
                    if (MessageBox.Show(w, msg + "\n\nVẫn gán chung tên (thiết kế lấy phương án bao được mọi cấu kiện)?", AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) { status.Text = msg; return; }
                    msg = MemberMarks.AssignFrames(_p, frames, mk, true);
                }
            }
            if (msg.StartsWith("Chưa") || msg.StartsWith("Các đối tượng") || msg.StartsWith("Không gán chung")) { status.Text = msg; return; }
            Changed(snap, msg);
        });
        var bClearSel = DialogUi.Btn("Xoá tên dòng chọn", (s, e) =>
        {
            var rows = grid.SelectedItems.OfType<MarkRow>().ToList();
            if (rows.Count == 0) { status.Text = "Chọn dòng cần xoá tên."; return; }
            string snap = Snapshot();
            foreach (var r in rows)
            {
                if (r.G.Kind == "Móng") foreach (var f in _p.Footings.Where(f => f.Mark == r.Mark)) f.Mark = "";
                else foreach (var f in _p.Frames.Where(f => f.Mark == r.Mark && (r.G.Kind == "Cột" ? f.Kind == FrameKind.Column : f.Kind == FrameKind.Beam))) f.Mark = "";
            }
            Changed(snap, $"Đã xoá {rows.Count} tên.");
        });
        var bClearAll = DialogUi.Btn("Xoá tất cả tên", (s, e) =>
        {
            string snap = Snapshot();
            int n = MemberMarks.ClearAll(_p, chkC.IsChecked == true, chkD.IsChecked == true, chkM.IsChecked == true);
            Changed(snap, $"Đã xoá tên {n} phần tử / móng – chương trình tự đặt tên như cũ.");
        });
        var bPick = DialogUi.Btn("Chọn trên mặt bằng", (s, e) =>
        {
            var rows = grid.SelectedItems.OfType<MarkRow>().Where(r => r.G.Kind != "Móng").ToList();
            if (rows.Count == 0) { status.Text = "Chọn dòng cột / dầm."; return; }
            ClearSelection();
            _multiSel.Clear(); _multiSelOwner = _p;
            foreach (var r in rows)
                foreach (var f in _p.Frames.Where(f => f.Mark == r.Mark && (r.G.Kind == "Cột" ? f.Kind == FrameKind.Column : f.Kind == FrameKind.Beam))) _multiSel.Add(f.Id);
            DrawHover();
            status.Text = $"Đã chọn {_multiSel.Count} phần tử trên mặt bằng (tầng đang xem).";
        });

        var row1 = DialogUi.Row(chkC, chkD, chkM, chkStory, chkKeep, bAuto);
        var row2 = DialogUi.Row(new TextBlock { Text = "Tên:", VerticalAlignment = VerticalAlignment.Center }, txtName, bAssign, bClearSel, bClearAll, bPick, DialogUi.Btn("Đóng", (s, e) => w.Close()));
        var top = new StackPanel();
        top.Children.Add(row1);
        top.Children.Add(row2);
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);
        root.Children.Add(grid);
        w.Content = root;
        Reload();
        if (string.IsNullOrEmpty(status.Text))
            status.Text = "Chọn cột / dầm trên mặt bằng (bấm hoặc quét khung) hoặc móng ở tab Mô hình móng, nhập tên rồi bấm \"Gán tên cho đối tượng đang chọn\"; hoặc \"Tự đặt tên theo nhóm\" cho cả công trình.";
        w.Show();
    }
}

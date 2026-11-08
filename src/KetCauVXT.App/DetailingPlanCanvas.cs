using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Detailing;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>Tab "Mặt bằng kết cấu" của cửa sổ Triển khai hồ sơ – KHÔNG GIAN VẼ: lưới trục, các mặt bằng (Móng, Tầng…, Mái),
/// công cụ đặt Cột / Móng / Dầm / Sàn / Lanh tô / Ô văng / Cầu thang (bắt điểm giao trục, đường trục), chọn – đặt tên – gán cấu kiện,
/// xoá; thu phóng bằng con lăn, kéo hình bằng nút giữa (hoặc Ctrl + kéo trái). Vị trí chưa gán → tạo cấu kiện mới và suy kích thước
/// (Core/Detailing/StructuralPlan.cs) nên hồ sơ chi tiết đi từ mặt bằng tới chi tiết cấu kiện.</summary>
public sealed partial class DetailingPlanCanvas : DockPanel
{
    private readonly Func<DetailingProject> _prj;
    private readonly Action _changed;
    private readonly Action<IDetailMember> _select;
    private readonly Window _owner;

    private readonly TextBox _sx = new() { Width = 170, Margin = new Thickness(2, 0, 6, 0) };
    private readonly TextBox _sy = new() { Width = 170, Margin = new Thickness(2, 0, 6, 0) };
    private readonly ComboBox _level = new() { Width = 150, Margin = new Thickness(2, 0, 6, 0) };
    private readonly TextBox _elev = new() { Width = 60, Margin = new Thickness(2, 0, 6, 0) };
    private readonly ComboBox _member = new() { Width = 170, Margin = new Thickness(2, 0, 6, 0) };
    private readonly TextBlock _status = new() { Margin = new Thickness(6, 2, 6, 2), TextWrapping = TextWrapping.Wrap };
    private readonly Canvas _cv = new() { Background = Brushes.White, ClipToBounds = true, Focusable = true };
    private readonly Dictionary<string, RadioButton> _tools = new();

    private string _tool = "Chon";
    private (double X, double Y)? _first;
    private (double X, double Y) _mouseW;
    private PlanItem _sel;
    private double _k = 60, _ox = 60, _oy = 500;     // px / m, gốc màn hình
    private Point? _panFrom;
    private bool _fitted;

    public const string NewMember = "＋ Tạo mới (tự đặt tên)";
    /// <summary>Công cụ đặt đường cắt mặt cắt thép sàn (2 điểm).</summary>
    public const string ToolSection = "MatCat";

    public DetailingPlanCanvas(Window owner, Func<DetailingProject> prj, Action changed, Action<IDetailMember> select)
    {
        _owner = owner; _prj = prj; _changed = changed; _select = select;
        LastChildFill = true;
        var top = new StackPanel { Margin = new Thickness(4) };
        var r1 = new WrapPanel { Margin = new Thickness(0, 0, 0, 3) };
        r1.Children.Add(Lbl("Bước trục X (1, 2, 3…) m:")); r1.Children.Add(_sx);
        r1.Children.Add(Lbl("Bước trục Y (A, B, C…) m:")); r1.Children.Add(_sy);
        r1.Children.Add(Btn("Áp dụng lưới", ApplyGrid, "Cập nhật lưới trục theo bước trục (vd. 3.6 4.2 hoặc 3*3.3)"));
        r1.Children.Add(Btn("Thêm trục…", AddGridLine, "Thêm một trục X hoặc Y tại toạ độ nhập (bước trục tự cập nhật); chuột phải lên trục: dời / xoá trục"));
        r1.Children.Add(Btn("Tải mặt bằng…", LoadPlanGrid, "Lấy bước trục từ bản vẽ CAD (DWG/DXF) hoặc ảnh mặt bằng"));
        top.Children.Add(r1);
        var r2 = new WrapPanel { Margin = new Thickness(0, 0, 0, 3) };
        r2.Children.Add(Lbl("Mặt bằng:")); r2.Children.Add(_level);
        r2.Children.Add(Lbl("Cao độ (m):")); r2.Children.Add(_elev);
        r2.Children.Add(Btn("＋ Mặt bằng", AddLevel, "Thêm mặt bằng tầng"));
        r2.Children.Add(Btn("Đổi tên", RenameLevel, "Đổi tên mặt bằng đang xem"));
        r2.Children.Add(Btn("Xoá mặt bằng", DeleteLevel, "Xoá mặt bằng đang xem (và các vị trí trên đó)"));
        r2.Children.Add(Btn("Cập nhật SL theo mặt bằng", SyncQty, "Đặt số lượng mỗi cấu kiện = số vị trí đã đặt trên các mặt bằng"));
        r2.Children.Add(Btn("Vừa khung", () => { _fitted = false; Redraw(); }, "Thu phóng vừa khung"));
        top.Children.Add(r2);
        var r3 = new WrapPanel();
        foreach (var (key, label) in new[] { ("Chon", "Chọn"), (PlanKinds.Column, "Cột"), (PlanKinds.Footing, "Móng"), (PlanKinds.Beam, "Dầm"), (PlanKinds.Slab, "Sàn"),
                     (PlanKinds.Lintel, "Lanh tô"), (PlanKinds.Canopy, "Ô văng"), (PlanKinds.Stair, "Cầu thang"), (ToolSection, "Mặt cắt"),
                     (ToolSlabBay, "Sàn trong ô dầm"), (ToolCanopyDoor, "Ô văng trên cửa"), (ToolMove, "Di chuyển"), (ToolCallout, "Trích chi tiết"), ("Xoa", "Xoá") })
        {
            var rb = new RadioButton { Content = label, GroupName = "plantool", Margin = new Thickness(0, 2, 10, 2), IsChecked = key == "Chon", Tag = key };
            rb.Checked += (s, e) => { _tool = (string)rb.Tag; _first = null; _moveItem = null; FillMembers(); UpdateHint(); Redraw(); };
            _tools[key] = rb; r3.Children.Add(rb);
        }
        r3.Children.Add(Lbl("  Cấu kiện:")); r3.Children.Add(_member);
        top.Children.Add(r3);
        top.Children.Add(BuildToolsRow());
        top.Children.Add(_status);
        DockPanel.SetDock(top, Dock.Top);
        Children.Add(top);
        Children.Add(_cv);

        _level.SelectionChanged += (s, e) => { _sel = null; _first = null; ShowElev(); Redraw(); };
        _elev.LostFocus += (s, e) =>
        {
            var lv = Level; if (lv == null) return;
            if (double.TryParse((_elev.Text ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double z) && Math.Abs(z - lv.Elevation) > 1e-9)
            { lv.Elevation = z; _changed(); }
        };
        _cv.SizeChanged += (s, e) => { if (!_fitted) Redraw(); else Redraw(); };
        _cv.MouseWheel += OnWheel;
        _cv.MouseDown += OnDown;
        _cv.MouseMove += OnMove;
        _cv.MouseUp += (s, e) => { if (_panFrom != null) { _panFrom = null; _cv.ReleaseMouseCapture(); } else WinUp(e); };
        _cv.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape) { _first = null; _sel = null; _moveItem = null; _multi.Clear(); _winFrom = null; Redraw(); UpdateHint(); }
            else if (e.Key == Key.Delete && _multi.Count > 0) DeleteMulti();
            else if (e.Key == Key.Delete && _sel != null) DeleteItem(_sel);
            else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control) { Undo(); e.Handled = true; }
        };
        // [Esc thoát lệnh] đang vẽ cấu kiện (cột, móng, dầm, sàn, lanh tô, ô văng, cầu thang, mặt cắt, di chuyển…): Esc thoát hẳn lệnh về "Chọn"
        // kể cả khi con trỏ nhập đang ở ô chữ / danh sách của mặt bằng (trừ ô bảng đang sửa, danh sách thả xuống đang mở)
        PreviewKeyDown += (s, e) =>
        {
            if (e.Key != Key.Escape || (_tool == "Chon" && _first == null && _moveItem == null && _winFrom == null)) return;
            for (var d = Keyboard.FocusedElement as DependencyObject; d != null; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
                if ((d is DataGridCell c && c.IsEditing) || (d is ComboBox cb && cb.IsDropDownOpen)) return;
            _first = null; _moveItem = null; _winFrom = null;
            if (_tools.TryGetValue("Chon", out var rbSel)) rbSel.IsChecked = true;
            _tool = "Chon";
            _cv.Focus();
            Redraw(); UpdateHint();
            e.Handled = true;
        };
        Reload();
    }

    private static TextBlock Lbl(string t) => new() { Text = t, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) };
    private static Button Btn(string t, Action a, string tip)
    {
        var b = new Button { Content = t, ToolTip = tip, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 4, 0) };
        b.Click += (s, e) => a();
        return b;
    }

    private DetailingProject P => _prj();
    private StructuralPlan Plan => P.Plan ??= new StructuralPlan();
    private PlanLevel Level => _level.SelectedIndex >= 0 && _level.SelectedIndex < Plan.Levels.Count ? Plan.Levels[_level.SelectedIndex] : Plan.Levels.FirstOrDefault();

    /// <summary>Nạp lại từ hồ sơ (mở / mới / mẫu).</summary>
    public void Reload()
    {
        _sx.Text = Plan.SpansX ?? ""; _sy.Text = Plan.SpansY ?? "";
        if (Plan.Levels.Count == 0) Plan.Levels.Add(new PlanLevel { Name = "Móng", Elevation = -1.5, IsFoundation = true });
        RefreshLevels(0);
        _sel = null; _first = null; _fitted = false;
        FillMembers(); UpdateHint(); Redraw();
    }

    private void RefreshLevels(int idx)
    {
        _level.ItemsSource = Plan.Levels.Select(l => l.Name + (l.IsFoundation ? " (móng)" : "")).ToList();
        _level.SelectedIndex = Math.Clamp(idx, 0, Plan.Levels.Count - 1);
        ShowElev();
    }

    private void ShowElev() { var lv = Level; _elev.Text = lv == null ? "" : lv.Elevation.ToString("0.###", CultureInfo.InvariantCulture); }

    private void FillMembers()
    {
        string kind = _tool == ToolSlabBay ? PlanKinds.Slab : _tool;
        if (!PlanKinds.All.Contains(kind)) { _member.ItemsSource = null; _member.IsEnabled = false; return; }
        _member.IsEnabled = true;
        var names = new List<string> { NewMember };
        names.AddRange(P.All.Where(m => PlanKinds.Accepts(kind, m)).Select(m => m.Name));
        _member.ItemsSource = names;
        _member.SelectedIndex = names.Count > 1 ? 1 : 0;
    }

    private void UpdateHint()
    {
        string t = _tool switch
        {
            "Chon" => "Chọn: bấm vào cấu kiện để chọn (bấm đúp → mở chi tiết cấu kiện); chuột phải: đổi tên / gán cấu kiện / xoá. Con lăn: thu phóng; nút giữa hoặc Ctrl + kéo: dời hình.",
            "Xoa" => "Xoá: bấm vào vị trí cấu kiện (hoặc đường cắt) cần xoá khỏi mặt bằng (cấu kiện trong hồ sơ vẫn giữ).",
            ToolSection => _first == null ? "Mặt cắt: bấm điểm đầu đường cắt (cắt qua ô sàn điển hình, vuông góc dầm) – mặt cắt thép sàn tl 1/25 xuất cùng mặt bằng."
                                          : "Mặt cắt: bấm điểm cuối đường cắt – Esc huỷ. Chuột phải lên đường cắt: đổi tên / xoá.",
            ToolSlabBay => "Sàn trong ô dầm: bấm vào bên trong ô được bao bởi dầm (hoặc trục) – tự lấy kích thước ô.",
            ToolCanopyDoor => "Ô văng trên cửa: bấm vào ô cửa của ánh xạ kiến trúc (cửa ở tường ngoài) – tạo ô văng OV… liền lanh tô, vươn ra phía ngoài.",
            ToolCallout => CalloutHint(),
            ToolMove => _moveItem == null ? "Di chuyển: bấm chọn cấu kiện cần dời (bắt điểm gốc)." : "Di chuyển: bấm điểm đích – Esc huỷ.",
            _ when PlanKinds.IsPoint(_tool) =>PlanKinds.Title(_tool) + ": bấm vào giao trục (tự bắt điểm) để đặt.",
            _ when PlanKinds.IsSegment(_tool) => PlanKinds.Title(_tool) + (_first == null ? ": bấm điểm đầu (bắt giao trục / đường trục)." : ": bấm điểm cuối – Esc huỷ."),
            _ => PlanKinds.Title(_tool) + (_first == null ? ": bấm góc thứ nhất của hình chữ nhật." : ": bấm góc đối diện – Esc huỷ.")
        };
        var warn = Plan.QuantityWarnings(P);
        _status.Text = t + (warn.Count > 0 ? "\n⚠ " + string.Join("; ", warn.Take(3)) + (warn.Count > 3 ? "…" : "") : "");
        _status.Foreground = warn.Count > 0 ? Brushes.DarkOrange : Brushes.Black;
    }

    // ------------------------------------------------------------------
    // lưới, mặt bằng
    // ------------------------------------------------------------------
    private void ApplyGrid()
    {
        Plan.SpansX = _sx.Text.Trim(); Plan.SpansY = _sy.Text.Trim();
        _fitted = false; _changed(); Redraw(); UpdateHint();
    }

    private void LoadPlanGrid()
    {
        var d = new GridFromPlanDialog(_sx.Text, _sy.Text, "", true) { Owner = _owner };
        if (d.ShowDialog() != true) return;
        if (!string.IsNullOrWhiteSpace(d.SpansX)) _sx.Text = d.SpansX;
        if (!string.IsNullOrWhiteSpace(d.SpansY)) _sy.Text = d.SpansY;
        ApplyGrid();
        // [Mở mặt bằng CAD] đặt cột ở vị trí thật (giao trục + lệch tâm) lên mặt bằng đang xem – PlanAxisPicker.ApplyToDetailing
        var pr = d.PickResult;
        if (pr != null && d.Rebuild && pr.Columns.Any(c => c.OnGrid) && Level != null)
        {
            bool replace = !Level.Items.Any(i => i.Kind == PlanKinds.Column)
                           || MessageBox.Show(_owner, "Mặt bằng " + Level.Name + " đã có cột. Thay vị trí cột cũ bằng cột đọc từ bản vẽ?", "Mở mặt bằng CAD", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
            _undo.Push(Plan);
            var log = KetCauVXT.Import.Arch.PlanAxisPicker.ApplyToDetailing(P, Level, pr, true, replace);
            // bản vẽ CAD vừa đọc → ánh xạ kiến trúc (xref) của mặt bằng (nếu chưa có), trục đầu tiên trùng trục hồ sơ
            if (ModelToDetailing.AttachUnderlayFromPick(Level, Plan, d.CadPath, pr.OriginX, pr.OriginY, Dir)) log.Add("Gắn bản vẽ làm ánh xạ kiến trúc của " + Level.Name + ".");
            _changed(); FillMembers(); _fitted = false; Redraw();
            _status.Text = string.Join(" ", log);
        }
    }

    private void AddLevel()
    {
        string n = DialogUi.InputBox(_owner, "Thêm mặt bằng", "Tên mặt bằng (vd. Tầng 3, Mái):", "Tầng " + (Plan.Levels.Count(l => !l.IsFoundation) + 2));
        if (string.IsNullOrWhiteSpace(n)) return;
        double z = Plan.Levels.Count > 0 ? Plan.Levels.Max(l => l.Elevation) + 3.6 : 0;
        Plan.Levels.Add(new PlanLevel { Name = n.Trim(), Elevation = Math.Round(z, 3) });
        RefreshLevels(Plan.Levels.Count - 1); _changed(); Redraw();
    }

    private void RenameLevel()
    {
        var lv = Level; if (lv == null) return;
        string n = DialogUi.InputBox(_owner, "Đổi tên mặt bằng", "Tên mới:", lv.Name);
        if (string.IsNullOrWhiteSpace(n)) return;
        lv.Name = n.Trim(); RefreshLevels(_level.SelectedIndex); _changed();
    }

    private void DeleteLevel()
    {
        var lv = Level; if (lv == null || Plan.Levels.Count <= 1) return;
        if (MessageBox.Show(_owner, "Xoá mặt bằng " + lv.Name + " và " + lv.Items.Count + " vị trí cấu kiện trên đó?", "Mặt bằng kết cấu", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        Plan.Levels.Remove(lv); RefreshLevels(0); _changed(); Redraw();
    }

    private void SyncQty()
    {
        int n = Plan.SyncQuantities(P);
        _changed(); UpdateHint();
        MessageBox.Show(_owner, n == 0 ? "Số lượng cấu kiện đã khớp với mặt bằng." : "Đã cập nhật số lượng " + n + " cấu kiện theo số vị trí trên mặt bằng.", "Mặt bằng kết cấu");
    }

    // ------------------------------------------------------------------
    // chuột
    // ------------------------------------------------------------------
    private (double X, double Y) ToW(Point p) => ((p.X - _ox) / _k, (_oy - p.Y) / _k);
    private Point ToS(double x, double y) => new(_ox + x * _k, _oy - y * _k);

    private void OnWheel(object s, MouseWheelEventArgs e)
    {
        var p = e.GetPosition(_cv);
        var w = ToW(p);
        double f = e.Delta > 0 ? 1.2 : 1 / 1.2;
        _k = Math.Clamp(_k * f, 5, 2000);
        _ox = p.X - w.X * _k; _oy = p.Y + w.Y * _k;
        _fitted = true; Redraw();
    }

    private void OnDown(object s, MouseButtonEventArgs e)
    {
        _cv.Focus();
        var p = e.GetPosition(_cv);
        if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && Keyboard.Modifiers == ModifierKeys.Control))
        { _panFrom = p; _cv.CaptureMouse(); return; }
        var lv = Level; if (lv == null) return;
        var w = ToW(p);
        double tol = 12 / _k;
        if (e.ChangedButton == MouseButton.Right)
        {
            var coMenu = CalloutMenuAt(lv, w, tol);   // khung chi tiết trích (DetailingPlanCanvas.Callout.cs)
            if (coMenu != null) { coMenu.IsOpen = true; return; }
            var hitSec = HitSection(lv, w.X, w.Y, tol);
            if (hitSec != null) { SectionMenu(hitSec).IsOpen = true; return; }
            var hit = StructuralPlan.HitTest(lv, w.X, w.Y, tol);
            if (hit != null) { _sel = hit; Redraw(); ContextFor(hit).IsOpen = true; }
            else if (_first != null) { _first = null; Redraw(); UpdateHint(); }
            else GridMenuAt(w, tol);   // chuột phải lên trục: dời / xoá / thêm trục (DetailingPlanCanvas.GridSel.cs)
            return;
        }
        if (e.ChangedButton != MouseButton.Left) return;
        if (_tool != "Chon") _undo.Push(Plan);
        if (HandleCalloutTool(lv, w, tol)) return;
        if (HandleExtraTool(lv, w, tol)) return;
        if (_tool == "Chon")
        {
            var hitC = StructuralPlan.HitTest(lv, w.X, w.Y, tol);
            if (ChonMultiClick(p, hitC)) return;   // quét chọn / Shift + bấm (DetailingPlanCanvas.GridSel.cs)
            _sel = hitC;
            Redraw();
            if (_sel != null)
            {
                var m = P.All.FirstOrDefault(x => x.Name == _sel.Member);
                _status.Text = PlanKinds.Title(_sel.Kind) + " " + (string.IsNullOrEmpty(_sel.Member) ? "(chưa gán)" : _sel.Member) + (m != null ? " – SL " + m.Quantity + ", đặt " + Plan.Count(m.Name) + " vị trí" : "")
                               + " – bấm đúp để mở chi tiết cấu kiện.";
                if (m != null) Picked?.Invoke(m, lv);
                if (e.ClickCount >= 2 && m != null) _select(m);
            }
            return;
        }
        if (_tool == ToolSection)
        {
            if (_first == null) { _first = w; UpdateHint(); Redraw(); return; }
            var a0 = _first.Value;
            _first = null;
            if (Math.Sqrt((a0.X - w.X) * (a0.X - w.X) + (a0.Y - w.Y) * (a0.Y - w.Y)) < 0.05) { UpdateHint(); Redraw(); return; }
            Plan.Sections ??= new();
            string nm = DialogUi.InputBox(_owner, "Mặt cắt thép sàn", "Tên mặt cắt (vd. 1-1):", Plan.NextSectionName());
            if (string.IsNullOrWhiteSpace(nm)) { UpdateHint(); Redraw(); return; }
            Plan.Sections.Add(new SectionMark { Name = nm.Trim(), Level = lv.Name, X1 = Math.Round(a0.X, 3), Y1 = Math.Round(a0.Y, 3), X2 = Math.Round(w.X, 3), Y2 = Math.Round(w.Y, 3) });
            _changed(); UpdateHint(); Redraw();
            return;
        }
        if (_tool == "Xoa")
        {
            var hitSec = HitSection(lv, w.X, w.Y, tol);
            if (hitSec != null) { Plan.Sections.Remove(hitSec); _changed(); UpdateHint(); Redraw(); return; }
            var hit = StructuralPlan.HitTest(lv, w.X, w.Y, tol);
            if (hit != null) DeleteItem(hit);
            return;
        }
        var sp = SnapW(lv, w, tol);
        string member = _member.SelectedItem as string;
        if (member == NewMember) member = "";
        if (PlanKinds.IsPoint(_tool))
        {
            var it = Plan.Place(P, lv, _tool, sp.X, sp.Y, sp.X, sp.Y, member);
            Done(it);
            return;
        }
        if (_first == null) { _first = sp; UpdateHint(); Redraw(); return; }
        var a = _first.Value;
        _first = null;
        if (Math.Abs(a.X - sp.X) < 1e-6 && Math.Abs(a.Y - sp.Y) < 1e-6) { UpdateHint(); Redraw(); return; }
        var it2 = Plan.Place(P, lv, _tool, a.X, a.Y, sp.X, sp.Y, member);
        Done(it2);
    }

    private void Done(PlanItem it)
    {
        _sel = it;
        FillMembers();
        // giữ cấu kiện vừa đặt để đặt tiếp cùng tên
        if (_member.ItemsSource is List<string> l && l.Contains(it.Member)) _member.SelectedItem = it.Member;
        _changed(); UpdateHint(); Redraw();
    }

    private void OnMove(object s, MouseEventArgs e)
    {
        var p = e.GetPosition(_cv);
        if (_panFrom != null)
        {
            _ox += p.X - _panFrom.Value.X; _oy += p.Y - _panFrom.Value.Y; _panFrom = p; _fitted = true; Redraw(); return;
        }
        if (WinMove(p)) return;
        var w = ToW(p);
        _mouseW = _tool == ToolSection ? w : SnapW(Level, w, 12 / _k);
        if (_first != null || _moveItem != null || PlanKinds.All.Contains(_tool)) Redraw();
    }

    private void DeleteItem(PlanItem it)
    {
        _undo.Push(Plan);
        Level?.Items.Remove(it);
        if (_sel == it) _sel = null;
        _changed(); UpdateHint(); Redraw();
    }

    private SectionMark HitSection(PlanLevel lv, double x, double y, double tol) =>
        (Plan.Sections ?? new()).Where(q => q.Level == lv?.Name)
            .Select(q => (Q: q, D: StructuralPlan.DistSeg(x, y, q.X1, q.Y1, q.X2, q.Y2)))
            .Where(t => t.D <= tol).OrderBy(t => t.D).Select(t => t.Q).FirstOrDefault();

    private ContextMenu SectionMenu(SectionMark sm)
    {
        var cm = new ContextMenu();
        var ren = new MenuItem { Header = "Đổi tên mặt cắt " + sm.Name + "…" };
        ren.Click += (s, e) =>
        {
            string n = DialogUi.InputBox(_owner, "Đổi tên mặt cắt", "Tên mới (vd. 2-2):", sm.Name);
            if (string.IsNullOrWhiteSpace(n)) return;
            sm.Name = n.Trim(); _changed(); Redraw();
        };
        var del = new MenuItem { Header = "Xoá mặt cắt " + sm.Name };
        del.Click += (s, e) => { Plan.Sections.Remove(sm); _changed(); UpdateHint(); Redraw(); };
        cm.Items.Add(ren); cm.Items.Add(del);
        return cm;
    }

    private ContextMenu ContextFor(PlanItem it)
    {
        var cm = new ContextMenu();
        MenuItem I(string h, Action a) { var mi = new MenuItem { Header = h }; mi.Click += (s, e) => a(); return mi; }
        var m = P.All.FirstOrDefault(x => x.Name == it.Member);
        if (m != null)
        {
            cm.Items.Add(I("Mở chi tiết cấu kiện " + m.Name, () => _select(m)));
            cm.Items.Add(I("Đổi tên cấu kiện " + m.Name + "…", () =>
            {
                string n = DialogUi.InputBox(_owner, "Đổi tên cấu kiện", "Tên mới (đổi cả trong hồ sơ và mọi vị trí trên mặt bằng):", m.Name);
                if (string.IsNullOrWhiteSpace(n)) return;
                if (!Plan.Rename(P, m.Name, n.Trim())) { MessageBox.Show(_owner, "Tên \"" + n + "\" đã có hoặc không hợp lệ.", "Đổi tên"); return; }
                _changed(); FillMembers(); UpdateHint(); Redraw();
            }));
        }
        cm.Items.Add(I("Gán cấu kiện khác / tạo mới…", () =>
        {
            var names = P.All.Where(x => PlanKinds.Accepts(it.Kind, x)).Select(x => x.Name).ToList();
            string n = DialogUi.InputBox(_owner, "Gán cấu kiện", "Tên cấu kiện (có sẵn: " + string.Join(", ", names.Take(12)) + (names.Count > 12 ? "…" : "") + ").\nTên chưa có → tạo cấu kiện mới theo kích thước vị trí:", it.Member);
            if (n == null) return;
            it.Member = n.Trim();
            Plan.EnsureMember(P, it, Level, string.IsNullOrWhiteSpace(n) ? null : n.Trim());
            _changed(); FillMembers(); UpdateHint(); Redraw();
        }));
        if (m != null)
            cm.Items.Add(I("Cập nhật kích thước cấu kiện theo vị trí này", () =>
            {
                string msg = Plan.ApplyGeometry(m, it, Level);
                _status.Text = string.IsNullOrEmpty(msg) ? "Không suy được kích thước từ vị trí này." : m.Name + ": " + msg;
                _changed();
            }));
        cm.Items.Add(new Separator());
        cm.Items.Add(I("Xoá vị trí khỏi mặt bằng", () => DeleteItem(it)));
        return cm;
    }

    // ------------------------------------------------------------------
    // vẽ
    // ------------------------------------------------------------------
    private void Fit()
    {
        double w = Math.Max(_cv.ActualWidth, 200), h = Math.Max(_cv.ActualHeight, 200);
        var xs = Plan.Xs; var ys = Plan.Ys;
        double x0 = 0, x1 = xs.Length > 0 ? xs[^1] : 8, y0 = 0, y1 = ys.Length > 0 ? ys[^1] : 6;
        foreach (var it in Plan.Levels.SelectMany(l => l.Items)) { x0 = Math.Min(x0, it.MinX); x1 = Math.Max(x1, it.MaxX); y0 = Math.Min(y0, it.MinY); y1 = Math.Max(y1, it.MaxY); }
        double m = 70;
        _k = Math.Max(5, Math.Min((w - 2 * m) / Math.Max(x1 - x0, 1), (h - 2 * m) / Math.Max(y1 - y0, 1)));
        _ox = m - x0 * _k + ((w - 2 * m) - (x1 - x0) * _k) / 2;
        _oy = h - m + y0 * _k - ((h - 2 * m) - (y1 - y0) * _k) / 2;
        _fitted = true;
    }

    public void Redraw()
    {
        if (!IsLoaded && _cv.ActualWidth <= 0) { _cv.Children.Clear(); return; }
        if (!_fitted && _cv.ActualWidth > 0) Fit();
        _cv.Children.Clear();
        var xs = Plan.Xs; var ys = Plan.Ys;
        double ex = 1.0;
        double gx0 = (xs.Length > 0 ? xs[0] : 0) - ex, gx1 = (xs.Length > 0 ? xs[^1] : 0) + ex, gy0 = (ys.Length > 0 ? ys[0] : 0) - ex, gy1 = (ys.Length > 0 ? ys[^1] : 0) + ex;
        var gridBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0x30, 0x30));
        string lvGrid = Level?.Name;   // kiểu trục: bong bóng từng đầu, ẩn trục trên mặt bằng đang xem
        for (int i = 0; i < xs.Length; i++)
        {
            if (!Plan.GridVisible(true, i, lvGrid)) continue;
            var gs = Plan.StyleOf(true, i);
            Line(xs[i], gy0, xs[i], gy1, gridBrush, 0.8, true);
            if (gs.BubbleStart) Bubble(xs[i], gy0, StructuralPlan.NameX(i), 0, -1);
            if (gs.BubbleEnd) Bubble(xs[i], gy1, StructuralPlan.NameX(i), 0, 1);
        }
        for (int j = 0; j < ys.Length; j++)
        {
            if (!Plan.GridVisible(false, j, lvGrid)) continue;
            var gs = Plan.StyleOf(false, j);
            Line(gx0, ys[j], gx1, ys[j], gridBrush, 0.8, true);
            if (gs.BubbleStart) Bubble(gx0, ys[j], StructuralPlan.NameY(j), -1, 0);
            if (gs.BubbleEnd) Bubble(gx1, ys[j], StructuralPlan.NameY(j), 1, 0);
        }
        var lv = Level;
        DrawUnderlay(lv);
        if (lv != null)
        {
            // mặt bằng móng nhìn mờ cột tầng trên & ngược lại: chỉ vẽ mặt bằng đang xem
            foreach (var it in lv.Items.OrderBy(i => i.Kind == PlanKinds.Slab || i.Kind == PlanKinds.Stair || i.Kind == PlanKinds.Canopy ? 0 : i.Kind == PlanKinds.Footing ? 1 : 2))
                DrawItem(it, it == _sel);
            // đường cắt mặt cắt thép sàn
            var secBrush = Brushes.DarkGreen;
            foreach (var sm in (Plan.Sections ?? new()).Where(q => q.Level == lv.Name))
            {
                Line(sm.X1, sm.Y1, sm.X2, sm.Y2, secBrush, 1.2, true);
                double len = Math.Max(sm.Length, 1e-6), nx = -(sm.Y2 - sm.Y1) / len, ny = (sm.X2 - sm.X1) / len, k = 12 / _k;
                foreach (var (px, py) in new[] { (sm.X1, sm.Y1), (sm.X2, sm.Y2) })
                {
                    Line(px, py, px + nx * 1.5 * k, py + ny * 1.5 * k, secBrush, 2.5, false);
                    Text(px + nx * 2.6 * k, py + ny * 2.6 * k, sm.Tag, secBrush, 12);
                }
            }
        }
        DrawCallouts(lv);   // khung chi tiết trích
        // xem trước khi vẽ
        if (_first != null)
        {
            var a = _first.Value; var b = _mouseW;
            var br = Brushes.DodgerBlue;
            if (PlanKinds.IsSegment(_tool) || _tool == ToolSection) Line(a.X, a.Y, b.X, b.Y, _tool == ToolSection ? Brushes.DarkGreen : br, 2, true);
            else { Line(a.X, a.Y, b.X, a.Y, br, 1.5, true); Line(b.X, a.Y, b.X, b.Y, br, 1.5, true); Line(b.X, b.Y, a.X, b.Y, br, 1.5, true); Line(a.X, b.Y, a.X, a.Y, br, 1.5, true); }
            Text(((a.X + b.X) / 2), ((a.Y + b.Y) / 2), Dim(Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y))), br, 11);
        }
        DrawMovePreview();
        DrawMulti();
        if (PlanKinds.All.Contains(_tool))
        {
            var c = ToS(_mouseW.X, _mouseW.Y);
            var cr = new Shapes.Rectangle { Width = 9, Height = 9, Stroke = Brushes.DodgerBlue, StrokeThickness = 1.5, IsHitTestVisible = false };
            Canvas.SetLeft(cr, c.X - 4.5); Canvas.SetTop(cr, c.Y - 4.5); _cv.Children.Add(cr);
        }
    }

    private static string Dim(double m) => (m * 1000).ToString("0", CultureInfo.InvariantCulture);

    private void DrawItem(PlanItem it, bool sel)
    {
        var m = P.All.FirstOrDefault(x => x.Name == it.Member);
        Brush br = sel ? Brushes.DodgerBlue : string.IsNullOrEmpty(it.Member) || m == null ? Brushes.OrangeRed : Brushes.Black;
        string nm = string.IsNullOrEmpty(it.Member) ? "?" : it.Member;
        switch (it.Kind)
        {
            case PlanKinds.Column:
            {
                var c = m as ManualColumn;
                double b = c?.B ?? 0.22, h = c?.H ?? 0.22;
                RectW(it.X1 - b / 2, it.Y1 - h / 2, it.X1 + b / 2, it.Y1 + h / 2, br, 1.5, sel ? Brushes.LightSkyBlue : Brushes.DimGray);
                Text(it.X1 + b / 2 + 0.05, it.Y1 + h / 2 + 0.05, nm, br, 11, false);
                break;
            }
            case PlanKinds.Footing:
            {
                double L = 1.2, B = 1.2;
                if (m is ManualFooting f) { L = f.L > 0 ? f.L : 1.2; B = f.B > 0 ? f.B : 1.2; }
                RectW(it.X1 - L / 2, it.Y1 - B / 2, it.X1 + L / 2, it.Y1 + B / 2, br, 1.2, null, true);
                Text(it.X1 - L / 2, it.Y1 + B / 2 + 0.05, nm, br, 11, false);
                break;
            }
            case PlanKinds.Beam:
            case PlanKinds.Lintel:
            {
                double b = m is ManualBeam mb ? mb.B : m is ManualLintel ml ? ml.Bw : m is ManualStrip ms ? Math.Max(ms.Bs, 0.3) : 0.22;
                var (a1, a2) = (ToS(it.X1, it.Y1), ToS(it.X2, it.Y2));
                var l = new Shapes.Line { X1 = a1.X, Y1 = a1.Y, X2 = a2.X, Y2 = a2.Y, Stroke = it.Kind == PlanKinds.Lintel ? (sel ? Brushes.DodgerBlue : Brushes.SaddleBrown) : br,
                    StrokeThickness = Math.Max(2, b * _k), Opacity = 0.55, IsHitTestVisible = false, StrokeStartLineCap = PenLineCap.Flat, StrokeEndLineCap = PenLineCap.Flat };
                _cv.Children.Add(l);
                string lab = nm + (m is ManualBeam mb2 ? $" ({Dim(mb2.B)}x{Dim(mb2.H)})" : "");
                Text((it.X1 + it.X2) / 2, (it.Y1 + it.Y2) / 2 + b / 2 + 0.05, lab, br, 11);
                break;
            }
            case PlanKinds.Slab:
                RectW(it.MinX, it.MinY, it.MaxX, it.MaxY, br, 0.8, new SolidColorBrush(Color.FromArgb(sel ? (byte)60 : (byte)25, 0x1E, 0x90, 0xFF)));
                Text((it.X1 + it.X2) / 2, (it.Y1 + it.Y2) / 2, nm + (m is ManualSlab s ? " h=" + Dim(s.T) : ""), br, 12);
                break;
            case PlanKinds.Canopy:
                RectW(it.MinX, it.MinY, it.MaxX, it.MaxY, br, 1, new SolidColorBrush(Color.FromArgb(70, 0xA0, 0x52, 0x2D)));
                Text((it.X1 + it.X2) / 2, (it.Y1 + it.Y2) / 2, "Ô văng " + nm, br, 10);
                break;
            case PlanKinds.Stair:
                RectW(it.MinX, it.MinY, it.MaxX, it.MaxY, br, 1, null);
                Line(it.MinX, it.MinY, it.MaxX, it.MaxY, br, 0.6, false); Line(it.MinX, it.MaxY, it.MaxX, it.MinY, br, 0.6, false);
                Text((it.X1 + it.X2) / 2, (it.Y1 + it.Y2) / 2, "Cầu thang " + nm, br, 11);
                break;
        }
    }

    private void Line(double x1, double y1, double x2, double y2, Brush b, double t, bool dash)
    {
        var a = ToS(x1, y1); var c = ToS(x2, y2);
        var l = new Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = c.X, Y2 = c.Y, Stroke = b, StrokeThickness = t, IsHitTestVisible = false };
        if (dash) l.StrokeDashArray = new DoubleCollection { 6, 3 };
        _cv.Children.Add(l);
    }

    private void RectW(double x0, double y0, double x1, double y1, Brush stroke, double t, Brush fill, bool dash = false)
    {
        var a = ToS(x0, y1); var c = ToS(x1, y0);
        var r = new Shapes.Rectangle { Width = Math.Max(1, c.X - a.X), Height = Math.Max(1, c.Y - a.Y), Stroke = stroke, StrokeThickness = t, Fill = fill, IsHitTestVisible = false };
        if (dash) r.StrokeDashArray = new DoubleCollection { 5, 3 };
        Canvas.SetLeft(r, a.X); Canvas.SetTop(r, a.Y); _cv.Children.Add(r);
    }

    private void Text(double x, double y, string s, Brush b, double size, bool center = true)
    {
        var p = ToS(x, y);
        var t = new TextBlock { Text = s, Foreground = b, FontSize = size, IsHitTestVisible = false };
        t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(t, center ? p.X - t.DesiredSize.Width / 2 : p.X); Canvas.SetTop(t, p.Y - t.DesiredSize.Height);
        _cv.Children.Add(t);
    }

    private void Bubble(double x, double y, string name, int dx, int dy)
    {
        var p = ToS(x, y);
        double r = 11;
        double cx = p.X + dx * r, cy = p.Y - dy * r;
        var e = new Shapes.Ellipse { Width = 2 * r, Height = 2 * r, Stroke = Brushes.Firebrick, StrokeThickness = 1, Fill = Brushes.White, IsHitTestVisible = false };
        Canvas.SetLeft(e, cx - r); Canvas.SetTop(e, cy - r); _cv.Children.Add(e);
        var t = new TextBlock { Text = name, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Firebrick, IsHitTestVisible = false };
        t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(t, cx - t.DesiredSize.Width / 2); Canvas.SetTop(t, cy - t.DesiredSize.Height / 2); _cv.Children.Add(t);
    }
}

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Drawing;
using KetCauVXT.Model;
using Microsoft.Win32;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// QUÉT CHỌN MỌI ĐỐI TƯỢNG + LƯỚI TRỤC + CAO ĐỘ TẦNG + MẶT ĐỨNG (chế độ Thiết kế)
//   • Chế độ "Chọn": kéo trái → phải = khung bao (xanh dương, nét liền) – đối tượng nằm trọn; phải → trái = khung cắt
//     (xanh lá, nét đứt) – đối tượng chạm khung. Ctrl + kéo: thêm; Shift + kéo: bỏ bớt. Chọn được cột, dầm, sàn, tường, cửa,
//     đồ đạc, thiết bị / tuyến điện nước, trục, đường mặt cắt. Bộ lọc… giữ loại cần; Delete xoá cả nhóm (1 bước hoàn tác).
//     Di chuyển khung nhìn: nút giữa chuột.
//   • Lưới trục: Vẽ trục (2 điểm, tự đặt tên 1, 2… / A, B…), Sửa lưới trục… (đổi tên, dời tới toạ độ – kéo theo cấu kiện,
//     sao chép lệch, xoá, đánh số lại), bấm đúp gần trục để đổi tên.
//   • Cao độ: Thêm tầng… (trên / dưới tầng chọn, chép không / chỉ cột / toàn bộ), Sửa cao độ – tên tầng… (đặt cao độ →
//     nút, cột, dầm, sàn co giãn theo), xoá tầng.
//   • Mặt đứng 4 hướng: trục có bong bóng trên / dưới, cao độ, cửa; thêm trục bằng cách bấm lên mặt đứng.
// Lõi: Model/GridLevelOps.cs (MixedSelection, GridOps, LevelOps), Drawing/BuildingElevation.cs.
// =====================================================================
public partial class MainWindow
{
    private readonly List<PlanSelItem> _mixSel = new();
    private bool _gridTool;
    private (double X, double Y)? _gridStart;

    private void InitSelectGridLevel()
    {
        try
        {
            if (_rvTabs != null)
                RvTab("Trục & tầng",
                    RvGroup("Chọn",
                        RvBtn("Bộ lọc…", "", "Giữ lại các loại đối tượng cần trong nhóm đã quét chọn (kiểu Revit)", FilterSelectionDialog),
                        RvBtn("Xoá đã chọn", "", "Xoá mọi đối tượng đã quét chọn (Delete) – một bước hoàn tác", DeleteMixedSelection),
                        RvBtn("Bỏ chọn", "", "Bỏ chọn tất cả (Esc)", ClearMixedSelection)),
                    RvGroup("Lưới trục",
                        RvBtn("Vẽ trục", "", "Bấm 2 điểm trên mặt bằng: đường gần đứng → trục X (1, 2…), gần ngang → trục Y (A, B…)", StartGridTool),
                        RvBtn("Sửa lưới trục…", "", "Đổi tên, dời trục tới toạ độ (kéo theo cấu kiện), sao chép lệch, xoá, đánh số lại", () => EditGridsDialog())),
                    RvGroup("Cao độ",
                        RvBtn("Thêm tầng…", "", "Thêm tầng phía trên / dưới tầng chọn – chép không / chỉ cột / toàn bộ cấu kiện", AddLevelDialog),
                        RvBtn("Sửa cao độ…", "", "Đặt cao độ, đổi tên, xoá tầng – nút, cột, dầm, sàn co giãn theo", EditLevelsDialog)),
                    RvGroup("Mặt đứng",
                        RvBtn("Mặt đứng", "", "Mặt đứng 4 hướng: trục có bong bóng, cao độ, cửa; bấm lên mặt đứng để thêm trục", () => OpenElevationView(ElevationSide.Front))));
            if (mainMenu != null)
            {
                var m = new MenuItem { Header = "Trục & _tầng" };
                void Add(string h, Action a) { var mi = new MenuItem { Header = h }; mi.Click += (s, e) => { try { a(); } catch (Exception ex) { ShowError(h.Replace("_", ""), ex); } }; m.Items.Add(mi); }
                Add("Vẽ _trục (2 điểm)", StartGridTool);
                Add("_Sửa lưới trục…", () => EditGridsDialog());
                m.Items.Add(new Separator());
                Add("Thêm _tầng…", AddLevelDialog);
                Add("Sửa _cao độ / tên tầng…", EditLevelsDialog);
                m.Items.Add(new Separator());
                Add("_Mặt đứng công trình…", () => OpenElevationView(ElevationSide.Front));
                m.Items.Add(new Separator());
                Add("_Bộ lọc đối tượng đã chọn…", FilterSelectionDialog);
                Add("Xoá đối tượng đã chọn", DeleteMixedSelection);
                int idx = Math.Max(0, mainMenu.Items.Count - 2);
                mainMenu.Items.Insert(idx, m);
            }
            if (planHost != null) planHost.PreviewMouseLeftButtonDown += PlanHost_GridDoubleClick;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Trục & tầng: " + ex.Message); }
    }

    // ------------------------------------------------------------------
    // quét chọn
    // ------------------------------------------------------------------
    private string StoryOrNull() => _p == null || CurrentStory == AllStories || _p.Stories.All(s => s.Name != CurrentStory) ? null : CurrentStory;

    /// <summary>Kết thúc khung chọn ở chế độ Chọn (gọi từ ApplyWindow).</summary>
    private void MixedWindowSelect(double xa, double ya, double xb, double yb, bool crossing)
    {
        if (_p == null) return;
        var mods = Keyboard.Modifiers;
        var mode = (mods & ModifierKeys.Shift) != 0 ? SelMode.Remove : (mods & ModifierKeys.Control) != 0 ? SelMode.Add : SelMode.Replace;
        var picked = MixedSelection.Select(_p, StoryOrNull(), SelectionRules.Box.Of(xa, ya, xb, yb), crossing);
        var res = MixedSelection.Combine(mode == SelMode.Replace ? null : _mixSel, picked, mode);
        if (mode == SelMode.Replace && HasSelection) ClearSelection();
        _mixSel.Clear(); _mixSel.AddRange(res);
        SyncMultiFromMixed();
        DrawHover();
        SetStatus($"Đã chọn: {MixedSelection.Summary(_mixSel)} (khung {(crossing ? "cắt – chạm khung" : "bao – nằm trọn")}). "
                  + "Ctrl + kéo: thêm · Shift + kéo: bỏ · Bộ lọc… · Delete: xoá · Esc: bỏ chọn.");
    }

    /// <summary>Đưa cột / dầm / ô sàn trong nhóm quét sang bộ chọn nhiều sẵn có (gán tải, sao chép… dùng chung).</summary>
    private void SyncMultiFromMixed()
    {
        _multiSel.Clear(); _multiSlab.Clear(); _multiSelOwner = _p;
        foreach (var it in _mixSel)
        {
            if (it.Cat is SelCat.Cot or SelCat.Dam) _multiSel.Add(it.Id);
            else if (it.Cat == SelCat.San) _multiSlab.Add(it.Id);
        }
    }

    private void ClearMixedSelection()
    {
        _mixSel.Clear(); _multiSel.Clear(); _multiSlab.Clear();
        DrawHover();
        SetStatus("Đã bỏ chọn.");
    }

    private void DeleteMixedSelection()
    {
        if (_p == null) return;
        if (_mixSel.Count == 0) { SetStatus("Chưa quét chọn đối tượng nào (kéo khung trên mặt bằng ở chế độ Chọn)."); return; }
        if (_busy) { SetStatus("Chương trình đang tính toán – chờ xong rồi sửa mô hình."); return; }
        string sum = MixedSelection.Summary(_mixSel);
        if (_mixSel.Any(i => i.Cat == SelCat.Truc) &&
            MessageBox.Show(this, $"Xoá {sum}? Cấu kiện nằm trên trục bị xoá vẫn giữ nguyên (chỉ mất trục định vị).", AppTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        string snap = Snapshot();
        int n = MixedSelection.Delete(_p, _mixSel);
        _mixSel.Clear(); _multiSel.Clear(); _multiSlab.Clear();
        AfterModelEdit($"Đã xoá {n} đối tượng ({sum}).", snap);
    }

    private void FilterSelectionDialog()
    {
        if (_mixSel.Count == 0) { SetStatus("Chưa quét chọn đối tượng nào."); return; }
        var counts = MixedSelection.Counts(_mixSel);
        var w = new Window { Title = "Bộ lọc", Owner = this, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        var sp = new StackPanel { Margin = new Thickness(12) };
        sp.Children.Add(new TextBlock { Text = "Giữ lại các loại đối tượng:", Margin = new Thickness(0, 0, 0, 6) });
        var boxes = new List<(CheckBox B, SelCat C)>();
        foreach (var c in MixedSelection.AllCats.Where(counts.ContainsKey))
        {
            var cb = new CheckBox { Content = $"{MixedSelection.Title(c)} ({counts[c]})", IsChecked = true, Margin = new Thickness(0, 2, 0, 2) };
            boxes.Add((cb, c)); sp.Children.Add(cb);
        }
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var bAll = new Button { Content = "Chọn hết", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
        var bNone = new Button { Content = "Bỏ hết", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 12, 0) };
        var ok = new Button { Content = "OK", IsDefault = true, Width = 80, Margin = new Thickness(0, 0, 6, 0) };
        var cancel = new Button { Content = "Huỷ", IsCancel = true, Width = 80 };
        bAll.Click += (s, e) => boxes.ForEach(b => b.B.IsChecked = true);
        bNone.Click += (s, e) => boxes.ForEach(b => b.B.IsChecked = false);
        ok.Click += (s, e) => { w.DialogResult = true; };
        row.Children.Add(bAll); row.Children.Add(bNone); row.Children.Add(ok); row.Children.Add(cancel);
        sp.Children.Add(row);
        w.Content = sp;
        if (w.ShowDialog() != true) return;
        var keep = boxes.Where(b => b.B.IsChecked == true).Select(b => b.C).ToHashSet();
        var res = MixedSelection.Filter(_mixSel, keep);
        _mixSel.Clear(); _mixSel.AddRange(res);
        SyncMultiFromMixed();
        DrawHover();
        SetStatus("Đã chọn: " + MixedSelection.Summary(_mixSel) + ".");
    }

    /// <summary>Delete / Esc với nhóm đã quét chọn; Esc dừng công cụ vẽ trục.</summary>
    private bool MixedSelKey(Key key, bool ctrl)
    {
        if (_p == null || ctrl || IsTextInputFocused()) return false;
        if (key == Key.Escape && _gridTool)
        {
            if (_gridStart != null) { _gridStart = null; DrawHover(); SetStatus("Vẽ trục: bấm điểm đầu (Esc lần nữa để dừng)."); }
            else StopGridTool();
            return true;
        }
        if (_mixSel.Count == 0) return false;
        if (key == Key.Escape) { ClearMixedSelection(); return true; }
        if (key == Key.Delete) { DeleteMixedSelection(); return true; }
        return false;
    }

    /// <summary>Tô sáng nhóm đã quét (trừ cột / dầm / ô sàn – đã có lớp phủ riêng) và dây chun công cụ vẽ trục.</summary>
    private void DrawMixedOverlay()
    {
        if (_p == null || planOverlay == null) return;
        string st = StoryOrNull();
        bool On(string s) => st == null || s == st;
        void Seg(double x1, double y1, double x2, double y2, double th, bool dash = false)
        {
            var l = CLine(planOverlay, W2S(x1, y1), W2S(x2, y2), MultiSelBrush, th, dash);
            l.Opacity = 0.8; l.IsHitTestVisible = false;
        }
        foreach (var it in _mixSel)
        {
            switch (it.Cat)
            {
                case SelCat.Cot:
                {
                    var f = _p.Frames.FirstOrDefault(x => x.Id == it.Id);
                    var n = f == null ? null : _p.Node(f.NodeJ) ?? _p.Node(f.NodeI);
                    if (n == null || (st != null && f.Story != st)) break;
                    var c = W2S(n.X, n.Y);
                    var r = new Shapes.Rectangle { Width = 18, Height = 18, Stroke = MultiSelBrush, StrokeThickness = 3, IsHitTestVisible = false };
                    Canvas.SetLeft(r, c.X - 9); Canvas.SetTop(r, c.Y - 9);
                    planOverlay.Children.Add(r);
                    break;
                }
                case SelCat.Tuong:
                {
                    var w = _p.Walls.FirstOrDefault(x => x.Id == it.Id);
                    if (w != null && On(w.Story)) Seg(w.X1, w.Y1, w.X2, w.Y2, 7);
                    break;
                }
                case SelCat.Cua:
                {
                    var w = _p.Walls.FirstOrDefault(x => x.Id == it.Host);
                    var o = w?.Openings?.FirstOrDefault(x => x.Id == it.Id);
                    if (o == null || !On(w.Story)) break;
                    var (a, b) = WallAssembly.Span(_p, o);
                    var pa = WallAssembly.PointAt(w, Math.Max(0, a)); var pb = WallAssembly.PointAt(w, Math.Max(0, b));
                    Seg(pa.X, pa.Y, pb.X, pb.Y, 9);
                    break;
                }
                case SelCat.DoDac:
                {
                    var f = _p.Arch?.Furniture.FirstOrDefault(x => x.Id == it.Id);
                    if (f == null || !On(f.Story)) break;
                    var poly = MixedSelection.FurniturePolygon(f);
                    var pc = new PointCollection(poly.Select(q => W2S(q.X, q.Y)));
                    planOverlay.Children.Add(new Shapes.Polygon { Points = pc, Stroke = MultiSelBrush, StrokeThickness = 3, Fill = new SolidColorBrush(Color.FromArgb(50, 255, 160, 0)), IsHitTestVisible = false });
                    break;
                }
                case SelCat.ThietBi:
                {
                    var d = _p.Mep?.Devices.FirstOrDefault(x => x.Id == it.Id);
                    if (d == null || !On(d.Story)) break;
                    var c = W2S(d.X, d.Y);
                    var r = new Shapes.Rectangle { Width = 14, Height = 14, Stroke = MultiSelBrush, StrokeThickness = 3, IsHitTestVisible = false };
                    Canvas.SetLeft(r, c.X - 7); Canvas.SetTop(r, c.Y - 7);
                    planOverlay.Children.Add(r);
                    break;
                }
                case SelCat.TuyenOng:
                {
                    var run = _p.Mep?.Runs.FirstOrDefault(x => x.Id == it.Id);
                    if (run?.Path == null || !On(run.Story)) break;
                    for (int i = 0; i + 1 < run.Path.Count; i++) Seg(run.Path[i].X, run.Path[i].Y, run.Path[i + 1].X, run.Path[i + 1].Y, 5);
                    break;
                }
                case SelCat.Truc:
                {
                    if (it.Id < 0 || it.Id >= _p.Grids.Count) break;
                    var (x1, y1, x2, y2) = MixedSelection.GridSegment(_p, _p.Grids[it.Id]);
                    Seg(x1, y1, x2, y2, 4, true);
                    break;
                }
                case SelCat.MatCat:
                {
                    var secs = _p.Arch?.Sections;
                    if (secs == null || it.Id < 0 || it.Id >= secs.Count) break;
                    Seg(secs[it.Id].X1, secs[it.Id].Y1, secs[it.Id].X2, secs[it.Id].Y2, 4, true);
                    break;
                }
            }
        }
        // công cụ vẽ trục: dây chun + tên trục dự kiến
        if (_gridTool && _gridStart != null && _lastMouse != null)
        {
            var (x, y) = S2W(_lastMouse.Value);
            var d = GridOps.FromTwoPoints(_gridStart.Value.X, _gridStart.Value.Y, x, y);
            var a = W2S(_gridStart.Value.X, _gridStart.Value.Y);
            var l = CLine(planOverlay, a, _lastMouse.Value, WinBrush, 1.5, true);
            l.IsHitTestVisible = false;
            if (d != null)
            {
                var g = d.Value;
                string nm = GridOps.NextName(_p, g.Dir);
                AddText($"Trục {nm}: {(g.Dir == GridDir.X ? "X" : "Y")} = {g.Coord:0.###} m", new Point(_lastMouse.Value.X + 12, _lastMouse.Value.Y + 10), WinBrush, 11, false, false, planOverlay);
            }
        }
    }

    // ------------------------------------------------------------------
    // lưới trục
    // ------------------------------------------------------------------
    private void StartGridTool()
    {
        if (_p == null) return;
        ShowPlanTab();
        if (rbModeSelect != null && rbModeSelect.IsChecked != true) rbModeSelect.IsChecked = true;
        _gridTool = true; _gridStart = null;
        if (planHost != null) planHost.Cursor = Cursors.Cross;
        SetStatus("Vẽ trục: bấm điểm đầu rồi điểm cuối – đường gần đứng → trục X (1, 2…), gần ngang → trục Y (A, B…). Esc dừng.");
    }

    private void StopGridTool()
    {
        _gridTool = false; _gridStart = null;
        if (planHost != null) planHost.Cursor = null;
        DrawHover();
        SetStatus("Đã dừng vẽ trục.");
    }

    /// <summary>Bấm trên mặt bằng khi đang vẽ trục (gọi đầu PlanClick).</summary>
    private bool GridToolClick(Point s)
    {
        if (!_gridTool || _p == null) return false;
        var (x, y) = S2W(s);
        // bắt vào giao trục / nút gần (giúp thẳng hàng)
        double tol = 8 / Math.Max(_scale, 1e-9);
        var near = _p.Nodes.Where(n => Math.Abs(n.X - x) < tol && Math.Abs(n.Y - y) < tol).OrderBy(n => Math.Abs(n.X - x) + Math.Abs(n.Y - y)).FirstOrDefault();
        if (near != null) { x = near.X; y = near.Y; }
        if (_gridStart == null) { _gridStart = (x, y); DrawHover(); SetStatus("Vẽ trục: bấm điểm cuối."); return true; }
        var d = GridOps.FromTwoPoints(_gridStart.Value.X, _gridStart.Value.Y, x, y);
        _gridStart = null;
        if (d == null) { SetStatus("Hai điểm quá gần – bấm lại."); DrawHover(); return true; }
        string snap = Snapshot();
        int before = _p.Grids.Count;
        var g = GridOps.AddGrid(_p, d.Value.Dir, d.Value.Coord);
        if (_p.Grids.Count == before) { SetStatus($"Đã có trục {g.Name} tại vị trí này."); DrawHover(); return true; }
        AfterModelEdit($"Đã thêm trục {g.Name} ({(g.Dir == GridDir.X ? "X" : "Y")} = {g.Coord:0.###} m). Bấm tiếp để vẽ trục khác, Esc dừng.", snap);
        if (planHost != null) planHost.Cursor = Cursors.Cross;
        return true;
    }

    /// <summary>Bấm đúp gần trục (chế độ Chọn) → đổi tên trục.</summary>
    private void PlanHost_GridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || _p == null || _gridTool || _mode != PlanEditMode.Select) return;
        var pt = e.GetPosition(planHost);
        var (x, y) = S2W(pt);
        double tol = 6 / Math.Max(_scale, 1e-9);
        GridLine best = null; double bd = double.MaxValue;
        foreach (var g in _p.Grids)
        {
            var (x1, y1, x2, y2) = MixedSelection.GridSegment(_p, g);
            double dd = g.Dir == GridDir.X ? Math.Abs(x - g.Coord) : Math.Abs(y - g.Coord);
            bool inRange = g.Dir == GridDir.X ? y >= y1 - tol && y <= y2 + tol : x >= x1 - tol && x <= x2 + tol;
            if (inRange && dd < bd) { bd = dd; best = g; }
        }
        if (best == null || bd > tol) return;
        // chỉ đổi tên khi bấm ở đoạn ngoài cấu kiện (gần đầu trục / bong bóng)
        var (sx1, sy1, sx2, sy2) = MixedSelection.GridSegment(_p, best);
        double along = best.Dir == GridDir.X ? Math.Min(Math.Abs(y - sy1), Math.Abs(y - sy2)) : Math.Min(Math.Abs(x - sx1), Math.Abs(x - sx2));
        if (along > 1.6) return;
        e.Handled = true;
        string nm = DialogUi.InputBox(this, "Đổi tên trục", $"Tên mới cho trục {best.Name}:", best.Name);
        if (string.IsNullOrWhiteSpace(nm) || nm.Trim() == best.Name) return;
        string snap = Snapshot();
        if (!GridOps.Rename(_p, best, nm)) { SetStatus($"Tên \"{nm.Trim()}\" đã có trục khác dùng."); return; }
        AfterModelEdit($"Đã đổi tên trục thành {best.Name}.", snap);
    }

    /// <summary>Hộp sửa lưới trục: một bước hoàn tác cho mọi thay đổi trong hộp.</summary>
    private void EditGridsDialog(GridLine pre = null)
    {
        if (_p == null) return;
        pre ??= _mixSel.Where(i => i.Cat == SelCat.Truc && i.Id >= 0 && i.Id < _p.Grids.Count).Select(i => _p.Grids[i.Id]).FirstOrDefault();
        string snapOpen = Snapshot();
        bool changed = false;
        var w = new Window { Title = "Sửa lưới trục", Owner = this, Width = 560, Height = 470, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var list = new ListBox { Margin = new Thickness(8), MinWidth = 220 };
        void Fill(GridLine sel = null)
        {
            var gs = _p.GridsX.Concat(_p.GridsY).ToList();
            list.ItemsSource = gs.Select(g => $"{(g.Dir == GridDir.X ? "X" : "Y")}  {g.Name,-5}  {g.Coord.ToString("0.###", CultureInfo.InvariantCulture)} m").ToList();
            list.Tag = gs;
            if (sel != null) list.SelectedIndex = gs.IndexOf(sel);
        }
        GridLine Cur() => list.Tag is List<GridLine> gs && list.SelectedIndex >= 0 && list.SelectedIndex < gs.Count ? gs[list.SelectedIndex] : null;
        var tName = new TextBox { Width = 120 };
        var tCoord = new TextBox { Width = 120 };
        var chkDrag = new CheckBox { Content = "Kéo theo cấu kiện (cột, đầu dầm, góc sàn, đầu tường trên trục)", IsChecked = true, Margin = new Thickness(0, 6, 0, 6) };
        var info = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 6, 0, 0) };
        list.SelectionChanged += (s, e) =>
        {
            var g = Cur(); if (g == null) return;
            tName.Text = g.Name; tCoord.Text = g.Coord.ToString("0.###", CultureInfo.InvariantCulture);
            var (n, wl) = GridOps.Attached(_p, g);
            info.Text = $"Trục {g.Name}: {n} nút, {wl} tường nằm trên trục.";
        };
        Button B(string t, Action a)
        {
            var b = new Button { Content = t, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 3, 0, 3), HorizontalAlignment = HorizontalAlignment.Stretch };
            b.Click += (s, e) => { try { a(); } catch (Exception ex) { MessageBox.Show(w, ex.Message, AppTitle); } };
            return b;
        }
        var right = new StackPanel { Margin = new Thickness(8), Width = 290 };
        right.Children.Add(new TextBlock { Text = "Tên trục" }); right.Children.Add(tName);
        right.Children.Add(B("Đổi tên", () =>
        {
            var g = Cur(); if (g == null) return;
            if (!GridOps.Rename(_p, g, tName.Text)) { MessageBox.Show(w, "Tên rỗng hoặc trùng trục khác.", AppTitle); return; }
            changed = true; Fill(g);
        }));
        right.Children.Add(new TextBlock { Text = "Toạ độ (m)", Margin = new Thickness(0, 6, 0, 0) }); right.Children.Add(tCoord);
        right.Children.Add(chkDrag);
        right.Children.Add(B("Dời tới toạ độ", () =>
        {
            var g = Cur(); if (g == null) return;
            double c = DialogUi.ParseNum(tCoord.Text, double.NaN);
            if (double.IsNaN(c)) { MessageBox.Show(w, "Toạ độ không hợp lệ.", AppTitle); return; }
            int n = GridOps.MoveGrid(_p, g, c, chkDrag.IsChecked == true);
            changed = true; Fill(g); info.Text = $"Đã dời trục {g.Name} – {n} đối tượng dời theo.";
        }));
        right.Children.Add(B("Sao chép lệch…", () =>
        {
            var g = Cur(); if (g == null) return;
            string v = DialogUi.InputBox(w, "Sao chép lệch", $"Khoảng lệch từ trục {g.Name} (m, âm = về phía trước):", "3.6");
            if (v == null) return;
            double d = DialogUi.ParseNum(v, double.NaN);
            if (double.IsNaN(d) || Math.Abs(d) < 1e-3) return;
            var ng = GridOps.OffsetCopy(_p, g, d);
            changed = true; Fill(ng); info.Text = $"Đã thêm trục {ng.Name}.";
        }));
        right.Children.Add(B("Thêm trục X (đứng)…", () => AddAt(GridDir.X)));
        right.Children.Add(B("Thêm trục Y (ngang)…", () => AddAt(GridDir.Y)));
        right.Children.Add(B("Xoá trục", () =>
        {
            var g = Cur(); if (g == null) return;
            var (n, wl) = GridOps.Attached(_p, g);
            if (n + wl > 0 && MessageBox.Show(w, $"Trục {g.Name} có {n} nút, {wl} tường nằm trên. Xoá trục (cấu kiện giữ nguyên)?", AppTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            GridOps.DeleteGrid(_p, g);
            changed = true; Fill(); info.Text = $"Đã xoá trục {g.Name}.";
        }));
        right.Children.Add(B("Đánh số lại (1, 2… / A, B…)", () => { GridOps.Renumber(_p); changed = true; Fill(); }));
        void AddAt(GridDir dir)
        {
            string v = DialogUi.InputBox(w, "Thêm trục", $"Toạ độ trục {(dir == GridDir.X ? "X" : "Y")} (m):", "0");
            if (v == null) return;
            double c = DialogUi.ParseNum(v, double.NaN);
            if (double.IsNaN(c)) return;
            var g = GridOps.AddGrid(_p, dir, c);
            changed = true; Fill(g); info.Text = $"Trục {g.Name} tại {g.Coord:0.###} m.";
        }
        right.Children.Add(info);
        var close = new Button { Content = "Đóng", IsCancel = true, IsDefault = true, Width = 90, Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (s, e) => w.Close();
        var dock = new DockPanel();
        DockPanel.SetDock(close, Dock.Bottom); DockPanel.SetDock(right, Dock.Right);
        dock.Children.Add(close); dock.Children.Add(right); dock.Children.Add(list);
        w.Content = dock;
        Fill(pre);
        w.ShowDialog();
        if (changed) AfterModelEdit("Đã sửa lưới trục (một bước hoàn tác).", snapOpen);
    }

    // ------------------------------------------------------------------
    // cao độ tầng
    // ------------------------------------------------------------------
    private void AddLevelDialog()
    {
        if (_p == null) return;
        var stories = _p.Stories.OrderBy(s => s.Elevation).Select(s => s.Name).ToList();
        var w = new Window { Title = "Thêm tầng", Owner = this, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        var sp = new StackPanel { Margin = new Thickness(12), Width = 320 };
        var cbRef = new ComboBox { ItemsSource = stories.Prepend("(nền / chân cột)").ToList(), Margin = new Thickness(0, 2, 0, 6) };
        cbRef.SelectedIndex = StoryOrNull() is string cs ? stories.IndexOf(cs) + 1 : stories.Count;
        var cbPos = new ComboBox { ItemsSource = new[] { "Phía trên tầng chọn", "Phía dưới tầng chọn" }, SelectedIndex = 0, Margin = new Thickness(0, 2, 0, 6) };
        var tH = new TextBox { Text = "3.3", Margin = new Thickness(0, 2, 0, 6) };
        var cbCopy = new ComboBox { ItemsSource = new[] { "Không chép cấu kiện", "Chỉ chép cột", "Chép toàn bộ (cột, dầm, sàn)" }, SelectedIndex = 2, Margin = new Thickness(0, 2, 0, 6) };
        sp.Children.Add(new TextBlock { Text = "Tầng tham chiếu" }); sp.Children.Add(cbRef);
        sp.Children.Add(new TextBlock { Text = "Vị trí tầng mới" }); sp.Children.Add(cbPos);
        sp.Children.Add(new TextBlock { Text = "Chiều cao tầng mới (m)" }); sp.Children.Add(tH);
        sp.Children.Add(new TextBlock { Text = "Cấu kiện tầng mới" }); sp.Children.Add(cbCopy);
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        var ok = new Button { Content = "Thêm", IsDefault = true, Width = 80, Margin = new Thickness(0, 0, 6, 0) };
        var cancel = new Button { Content = "Huỷ", IsCancel = true, Width = 80 };
        ok.Click += (s, e) => w.DialogResult = true;
        row.Children.Add(ok); row.Children.Add(cancel); sp.Children.Add(row);
        w.Content = sp;
        if (w.ShowDialog() != true) return;
        double h = DialogUi.ParseNum(tH.Text, double.NaN);
        if (double.IsNaN(h) || h <= 0.5) { SetStatus("Chiều cao tầng phải > 0,5 m."); return; }
        string refSt = cbRef.SelectedIndex <= 0 ? null : stories[cbRef.SelectedIndex - 1];
        bool above = cbPos.SelectedIndex == 0 || refSt == null;
        var mode = cbCopy.SelectedIndex switch { 0 => LevelCopyMode.None, 1 => LevelCopyMode.ColumnsOnly, _ => LevelCopyMode.Full };
        string snap = Snapshot();
        try
        {
            string name = LevelOps.AddLevel(_p, refSt, above, h, mode);
            AfterModelEdit($"Đã thêm tầng {name} cao {h:0.00} m – các tầng phía trên được nâng, cần chạy lại phân tích.", snap);
        }
        catch (Exception ex) { RestoreSnapshot(snap); ShowError("Thêm tầng", ex); }
    }

    private void EditLevelsDialog()
    {
        if (_p == null) return;
        string snapOpen = Snapshot();
        bool changed = false;
        var w = new Window { Title = "Cao độ – tên tầng", Owner = this, Width = 540, Height = 400, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var list = new ListBox { Margin = new Thickness(8), MinWidth = 220 };
        void Fill(string sel = null)
        {
            var lv = _p.Stories.OrderByDescending(s => s.Elevation).ToList();
            list.ItemsSource = lv.Select(s => $"{s.Name,-8} {SectionView.ElevText(s.Elevation)}  (cao {s.Height:0.00} m)").ToList();
            list.Tag = lv;
            if (sel != null) list.SelectedIndex = lv.FindIndex(s => s.Name == sel);
        }
        Story Cur() => list.Tag is List<Story> l && list.SelectedIndex >= 0 && list.SelectedIndex < l.Count ? l[list.SelectedIndex] : null;
        var tName = new TextBox(); var tZ = new TextBox();
        var info = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 6, 0, 0) };
        list.SelectionChanged += (s, e) =>
        {
            var st = Cur(); if (st == null) return;
            tName.Text = st.Name; tZ.Text = st.Elevation.ToString("0.###", CultureInfo.InvariantCulture);
            var (lo, hi) = LevelOps.Limits(_p, st.Name);
            info.Text = $"Kéo cao độ trong khoảng {lo:0.###} … {(double.IsInfinity(hi) ? "∞" : hi.ToString("0.###"))} m (tầng cao ≥ 0,5 m).";
        };
        Button B(string t, Action a)
        {
            var b = new Button { Content = t, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 3, 0, 3) };
            b.Click += (s, e) => { try { a(); } catch (Exception ex) { MessageBox.Show(w, ex.Message, AppTitle); } };
            return b;
        }
        var right = new StackPanel { Margin = new Thickness(8), Width = 270 };
        right.Children.Add(new TextBlock { Text = "Tên tầng" }); right.Children.Add(tName);
        right.Children.Add(B("Đổi tên", () =>
        {
            var st = Cur(); if (st == null || string.IsNullOrWhiteSpace(tName.Text)) return;
            if (_p.Stories.Any(s => s != st && s.Name == tName.Text.Trim())) { MessageBox.Show(w, "Tên trùng tầng khác.", AppTitle); return; }
            string nn = tName.Text.Trim();
            Levels.RenameStory(_p, st.Name, nn); changed = true; Fill(nn);
        }));
        right.Children.Add(new TextBlock { Text = "Cao độ sàn (m)", Margin = new Thickness(0, 6, 0, 0) }); right.Children.Add(tZ);
        right.Children.Add(B("Đặt cao độ (nút, cột, dầm, sàn dời theo)", () =>
        {
            var st = Cur(); if (st == null) return;
            double z = DialogUi.ParseNum(tZ.Text, double.NaN);
            if (double.IsNaN(z)) return;
            LevelOps.SetElevation(_p, st.Name, z); changed = true; Fill(st.Name);
        }));
        right.Children.Add(B("Xoá tầng", () =>
        {
            var st = Cur(); if (st == null) return;
            if (MessageBox.Show(w, $"Xoá tầng {st.Name}: xoá dầm, sàn, tường của tầng, nối cột, hạ các tầng phía trên?", AppTitle, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
            Levels.DeleteStory(_p, st.Name); changed = true; Fill();
        }));
        right.Children.Add(info);
        var close = new Button { Content = "Đóng", IsCancel = true, IsDefault = true, Width = 90, Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (s, e) => w.Close();
        var dock = new DockPanel();
        DockPanel.SetDock(close, Dock.Bottom); DockPanel.SetDock(right, Dock.Right);
        dock.Children.Add(close); dock.Children.Add(right); dock.Children.Add(list);
        w.Content = dock;
        Fill(StoryOrNull());
        w.ShowDialog();
        if (changed) AfterModelEdit("Đã sửa cao độ / tên tầng (một bước hoàn tác) – cần chạy lại phân tích.", snapOpen);
    }

    // ------------------------------------------------------------------
    // mặt đứng
    // ------------------------------------------------------------------
    private void AddElevationViewsToBrowser()
    {
        if (_rvBrowser == null || _p == null) return;
        var root = TI("Mặt đứng", null, false);
        foreach (var side in BuildingElevation.Sides)
        {
            var sd = side;
            string t;
            try { t = BuildingElevation.Title(_p, sd); } catch (Exception) { t = "Mặt đứng " + sd; }
            root.Items.Add(TI(t, () => OpenElevationView(sd)));
        }
        _rvBrowser.Items.Add(root);
    }

    /// <summary>Khung nhìn mặt đứng: chọn hướng, xuất DXF, bấm lên hình để thêm trục, thêm tầng / sửa cao độ.</summary>
    private Window OpenElevationView(ElevationSide side)
    {
        if (_p == null) return null;
        var w = new Window { Title = "Mặt đứng công trình", Owner = this, Width = 1000, Height = 700, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var dock = new DockPanel();
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6) };
        var cb = new ComboBox { Width = 220 };
        var bDxf = new Button { Content = "Xuất DXF…", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var tgGrid = new System.Windows.Controls.Primitives.ToggleButton { Content = "Thêm trục (bấm lên hình)", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var bLvAdd = new Button { Content = "Thêm tầng…", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var bLvEdit = new Button { Content = "Sửa cao độ…", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var info = new TextBlock { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray };
        bar.Children.Add(cb); bar.Children.Add(bDxf); bar.Children.Add(tgGrid); bar.Children.Add(bLvAdd); bar.Children.Add(bLvEdit); bar.Children.Add(info);
        DockPanel.SetDock(bar, Dock.Top);
        var img = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(10) };
        var host = new Border { Background = Brushes.White, Child = img };
        dock.Children.Add(bar); dock.Children.Add(host);
        w.Content = dock;
        BuildingElevationView cur = null;
        double curK = 1, curX1 = 0, curY1 = 0, curH = 0;
        Rect curBounds = Rect.Empty;
        Story dragLv = null; double dragZ = 0;   // kéo đường cao độ (bấm gần đường cao độ khi không bật "Thêm trục")
        double? ModelZ(Point pt)
        {
            if (cur == null || curBounds.IsEmpty || img.ActualHeight < 1) return null;
            return LevelDragMath.ModelZFromView(pt.Y, img.ActualHeight, curBounds.Y, curBounds.Height, curY1, curH, curK);   // m cao độ
        }
        void Titles()
        {
            int keep = Math.Max(0, cb.SelectedIndex);
            cb.ItemsSource = BuildingElevation.Sides.Select(s => BuildingElevation.Title(_p, s)).ToList();
            cb.SelectedIndex = keep;
        }
        void Show()
        {
            int i = Math.Max(0, cb.SelectedIndex);
            cur = BuildingElevation.Draw(_p, BuildingElevation.Sides[i], 100);
            var fw = cur.Figure.W; var e = fw.Extents;
            if (e.IsEmpty) { img.Source = null; info.Text = "Chưa có cấu kiện / trục."; return; }
            double k = 1.0 / Math.Max(fw.Scale, 1e-9);
            var d = SheetRender.Render(new[] { (fw, k, -e.X1 * k, -e.Y1 * k) }, 0, e.H * k, 0.1, x => x.GetPrimitives());
            img.Source = new DrawingImage(d);
            curK = k; curX1 = e.X1; curY1 = e.Y1; curH = e.H; curBounds = d.Bounds;
            info.Text = $"{cur.Items.Count} khối · {cur.Levels.Count} cao độ · {cur.Axes.Count} trục";
        }
        cb.SelectionChanged += (s, e) => { if (cb.SelectedIndex >= 0) Show(); };
        Titles();
        cb.SelectedIndex = Array.IndexOf(BuildingElevation.Sides, side);
        img.MouseLeftButtonDown += (s, e) =>
        {
            if (tgGrid.IsChecked != true && ModelZ(e.GetPosition(img)) is double zz)
            {
                // bắt đầu kéo đường cao độ: tầng gần con trỏ (≤ 0,3 m)
                dragLv = LevelDragMath.NearestStory(_p, zz, 0.3);
                if (dragLv != null) { dragZ = dragLv.Elevation; img.CaptureMouse(); info.Text = $"Kéo cao độ {dragLv.Name}: {dragZ:+0.000;-0.000} m – thả chuột để đặt."; }
                return;
            }
            if (tgGrid.IsChecked != true || cur == null || curBounds.IsEmpty || img.ActualWidth < 1) return;
            var pt = e.GetPosition(img);
            double drawX = curBounds.X + pt.X / img.ActualWidth * curBounds.Width;   // mm giấy
            double u = (drawX / curK + curX1) / 1000.0;                               // m dọc đường nhìn
            int si = Math.Max(0, cb.SelectedIndex);
            var (dir, c) = BuildingElevation.PlanCoord(_p, BuildingElevation.Sides[si], u);
            string v = DialogUi.InputBox(w, "Thêm trục từ mặt đứng", $"Toạ độ trục {(dir == GridDir.X ? "X" : "Y")} trên mặt bằng (m) – sửa cho chính xác:", Math.Round(c, 2).ToString("0.###", CultureInfo.InvariantCulture));
            if (v == null) return;
            double cc = DialogUi.ParseNum(v, double.NaN);
            if (double.IsNaN(cc)) return;
            string snap = Snapshot();
            var g = GridOps.AddGrid(_p, dir, cc);
            AfterModelEdit($"Đã thêm trục {g.Name} từ mặt đứng ({(dir == GridDir.X ? "X" : "Y")} = {g.Coord:0.###} m).", snap);
            Titles(); Show();
        };
        img.MouseMove += (s, e) =>
        {
            if (dragLv == null || ModelZ(e.GetPosition(img)) is not double z) return;
            dragZ = LevelDragMath.Clamp(_p, dragLv.Name, z);
            int iLv = _p.Stories.OrderBy(q => q.Elevation).ToList().FindIndex(q => q.Name == dragLv.Name);
            var ord = _p.Stories.OrderBy(q => q.Elevation).ToList();
            double below = iLv <= 0 ? _p.BaseElevation : ord[iLv - 1].Elevation;
            info.Text = $"Kéo cao độ {dragLv.Name}: {dragZ:+0.000;-0.000} m · cao tầng {(dragZ - below) * 1000:0} mm – thả chuột để đặt.";
        };
        img.MouseLeftButtonUp += (s, e) =>
        {
            if (dragLv == null) return;
            img.ReleaseMouseCapture();
            var lv = dragLv; dragLv = null;
            if (Math.Abs(dragZ - lv.Elevation) < 1e-6) { Show(); return; }
            if (_busy) { SetStatus("Chương trình đang tính toán – chờ xong rồi sửa mô hình."); return; }
            string snap = Snapshot();
            double old = lv.Elevation;
            try
            {
                double zNew = LevelDragMath.Drop(_p, lv.Name, dragZ);
                AfterModelEdit($"Đã kéo cao độ {lv.Name}: {old:+0.000;-0.000} → {zNew:+0.000;-0.000} m (chiều cao các tầng tự tính lại).", snap);
            }
            catch (Exception ex) { MessageBox.Show(w, ex.Message, AppTitle); }
            Titles(); Show();
        };
        bDxf.Click += (s, e) =>
        {
            if (cur == null) return;
            var dlg = new SaveFileDialog { Filter = "DXF (*.dxf)|*.dxf", FileName = "MatDung_" + (Math.Max(0, cb.SelectedIndex) + 1) + ".dxf" };
            if (dlg.ShowDialog(w) != true) return;
            try { cur.Figure.W.Save(dlg.FileName); SetStatus("Đã xuất " + dlg.FileName); }
            catch (Exception ex) { MessageBox.Show(w, ex.Message, AppTitle); }
        };
        bLvAdd.Click += (s, e) => { AddLevelDialog(); Titles(); Show(); };
        bLvEdit.Click += (s, e) => { EditLevelsDialog(); Titles(); Show(); };
        w.Show();
        return w;
    }
}

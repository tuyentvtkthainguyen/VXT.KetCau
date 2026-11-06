using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using KetCauVXT.Analysis;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.App;

// =====================================================================
// QUY TRÌNH MÔ HÌNH KIỂU ETABS – menu Sửa / Xem / Khai báo / Vẽ / Chọn / Gán / Phân tích / Hiển thị (MainWindow.xaml),
// thanh trạng thái (đơn vị, tầng, toạ độ con trỏ, hệ trục), phím tắt Ctrl+E (cây mô hình), Ctrl+T (bảng kết quả), Ctrl+A (chọn mọi thanh).
//   Cây mô hình: MainWindow.ModelExplorer.cs ; tải gán trên mặt bằng: MainWindow.AssignedLoads.cs
//   Lõi: Model/ModelAssign.cs, Model/ModelCheck.cs, Loads/ComboEditor.cs, Analysis/ResultTables.cs
// =====================================================================
public partial class MainWindow
{
    private ComboEditorWindow _comboWin;
    private ModelCheckWindow _checkWin;
    private ResultTablesWindow _tablesWin;
    private DeformedShapeWindow _deformWin;

    private void InitWorkflowUi()
    {
        cbStory.SelectionChanged += (s, e) => UpdateStoryStatus();
        planHost.MouseMove += (s, e) => UpdateCursorStatus(e.GetPosition(planHost));
        planHost.MouseLeave += (s, e) => { if (txtStCursor != null) txtStCursor.Text = "X = –  Y = –"; };
        AddHandler(PreviewKeyDownEvent, new KeyEventHandler(Workflow_PreviewKeyDown), true);   // nhận cả phím đã xử lý (Esc) để vẽ lại cột đang chọn
        InitModelExplorer();
    }

    private void Workflow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_busy) return;
        if (e.Key == Key.Escape)
        {
            // Esc bỏ chọn nhiều (MainWindow.BeamTools.cs) – vẽ lại để xoá viền cột đang chọn
            Dispatcher.BeginInvoke(new Action(() => { if (_p != null && _multiSel.Count == 0) RedrawPlan(); }), System.Windows.Threading.DispatcherPriority.Background);
            return;
        }
        if (e.Handled) return;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (!ctrl) return;
        if (e.Key == Key.E) { MnuModelExplorer_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.T) { MnuTables_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.A && !IsTextInputFocused() && tabView.SelectedItem is TabItem ti && (ti.Header as string) == "Mặt bằng")
        { SelectFramesOnStory("all"); e.Handled = true; }
    }

    // ------------------------------------------------------------------
    // Thanh trạng thái
    // ------------------------------------------------------------------
    private void UpdateStoryStatus()
    {
        if (txtStStory == null || _p == null) return;
        string st = CurrentStory;
        var s = _p.Stories.FirstOrDefault(x => x.Name == st);
        txtStStory.Text = s != null ? $"Tầng: {s.Name} (+{s.Elevation:0.000} m)" : "Tầng: " + st;
    }

    private void UpdateCursorStatus(Point sp)
    {
        if (txtStCursor == null || _p == null) return;
        var (x, y) = S2W(sp);
        var s = _p.Stories.FirstOrDefault(q => q.Name == CurrentStory);
        txtStCursor.Text = $"X = {x,8:0.000}  Y = {y,8:0.000}" + (s != null ? $"  Z = {s.Elevation:0.000}" : "");
    }

    // ------------------------------------------------------------------
    // Lựa chọn dùng cho menu Gán
    // ------------------------------------------------------------------
    /// <summary>Mọi thanh đang chọn: chọn nhiều (_multiSel, dầm + cột) và thanh chọn đơn.</summary>
    private List<int> SelectedFrameIdsAll()
    {
        CheckMultiSelOwner();
        var ids = _multiSel.Where(id => _p.Frames.Any(f => f.Id == id)).ToList();
        if (_sel != null && _p.Frames.Any(f => f.Id == _sel.Id) && !ids.Contains(_sel.Id)) ids.Add(_sel.Id);
        return ids;
    }

    /// <summary>Nút gợi ý cho gán tải nút / liên kết: chân cột của móng đang chọn, nút của thanh đang chọn.</summary>
    private List<int> SuggestedNodes(bool columnBase)
    {
        var l = new List<int>();
        if (_selFooting != 0) l.Add(_selFooting);
        l.AddRange(ModelAssign.NodesOf(_p, SelectedFrameIdsAll(), columnBase));
        if (_selSlab != null) l.AddRange(_selSlab.Nodes);
        return l.Distinct().ToList();
    }

    private bool NeedFrames(List<int> ids, string what)
    {
        if (ids.Count > 0) return true;
        SetStatus($"Chọn thanh trước (Chọn → Chọn từng đối tượng / Chọn nhiều, hoặc Ctrl+A), rồi {what}.");
        MessageBox.Show(this, $"Chưa chọn thanh nào.\nChọn dầm/cột trên mặt bằng (Chọn → Chọn nhiều để kéo khung, hoặc Chọn → Mọi thanh tầng đang xem), rồi {what}.", AppTitle);
        return false;
    }

    /// <summary>Thực hiện thao tác sửa mô hình có hoàn tác, giữ lựa chọn hiện tại.</summary>
    private bool ApplyModelChange(Func<EditResult> op)
    {
        if (_p == null) return false;
        if (_busy) { SetStatus("Chương trình đang tính toán – chờ xong rồi sửa mô hình."); return false; }
        string snap = Snapshot();
        EditResult r;
        try { r = op(); }
        catch (Exception ex) { RestoreSnapshot(snap); ShowError("Không thực hiện được", ex); return false; }
        if (r == null || !r.Ok)
        {
            if (Snapshot() != snap) RestoreSnapshot(snap);
            SetStatus(r?.Message ?? "Không thực hiện được.");
            return false;
        }
        var keepMulti = _multiSel.ToList();
        int keepSel = _sel?.Id ?? 0, keepSlab = _selSlab?.Id ?? 0, keepFoot = _selFooting;
        PushUndo(snap);
        _p.InvalidateIndex();
        MarkDirty();
        RefreshAll(false);
        _multiSel.Clear(); _multiSelOwner = _p;
        foreach (var id in keepMulti) if (_p.Frames.Any(f => f.Id == id)) _multiSel.Add(id);
        _sel = _p.Frames.FirstOrDefault(f => f.Id == keepSel);
        _selSlab = _p.Slabs.FirstOrDefault(s => s.Id == keepSlab);
        _selFooting = _p.Nodes.Any(n => n.Id == keepFoot) ? keepFoot : 0;
        OnSelectionChanged();
        RefreshModelExplorer();
        SetStatus(r.Message + "  (Ctrl+Z để hoàn tác)");
        return true;
    }

    /// <summary>Báo từ cửa sổ không chặn (tổ hợp, kiểm tra mô hình): lưu hoàn tác, đánh dấu thay đổi, làm mới.</summary>
    private void OnWorkflowCommit(string snapBefore, string msg)
    {
        if (snapBefore != null) PushUndo(snapBefore);
        _p.InvalidateIndex();
        _sel = _sel != null ? _p.Frames.FirstOrDefault(f => f.Id == _sel.Id) : null;
        _selSlab = _selSlab != null ? _p.Slabs.FirstOrDefault(s => s.Id == _selSlab.Id) : null;
        MarkDirty();
        RefreshAll(false);
        RefreshModelExplorer();
        SetStatus(msg);
    }

    // ------------------------------------------------------------------
    // Sửa
    // ------------------------------------------------------------------
    private void MnuUndo_Click(object sender, RoutedEventArgs e) => Undo();
    private void MnuRedo_Click(object sender, RoutedEventArgs e) => Redo();
    private void MnuDeleteSel_Click(object sender, RoutedEventArgs e) => DeleteSelection();
    private void MnuReplicate_Click(object sender, RoutedEventArgs e) => ReplicateSelection();
    private void MnuInsertion_Click(object sender, RoutedEventArgs e) => InsertionForSelection();

    private void MnuMergeNodes_Click(object sender, RoutedEventArgs e)
    {
        ApplyModelChange(() =>
        {
            var r = ModelCheck.Fix(_p, new[] { ModelCheck.DuplicateNodes, ModelCheck.DuplicateFrames });
            return r.Total == 0 ? EditResult.Fail("Không có nút trùng (dung sai 5 mm).") : new EditResult { Ok = true, Message = "Gộp nút: " + r };
        });
    }

    // ------------------------------------------------------------------
    // Xem
    // ------------------------------------------------------------------
    private void MnuViewTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string tag) return;
        var tab = tabView.Items.OfType<TabItem>().FirstOrDefault(t => (t.Header as string) == tag);
        if (tab != null) tabView.SelectedItem = tab;
        else SetStatus("Không có khung nhìn " + tag);
    }

    private void MnuToggleLabels_Click(object sender, RoutedEventArgs e) => chkLabels.IsChecked = chkLabels.IsChecked != true;

    private void ShowPlanTab()
    {
        var tab = tabView.Items.OfType<TabItem>().FirstOrDefault(t => (t.Header as string) == "Mặt bằng");
        if (tab != null) tabView.SelectedItem = tab;
    }

    // ------------------------------------------------------------------
    // Vẽ / Chọn: đặt chế độ mặt bằng
    // ------------------------------------------------------------------
    /// <summary>Mọi nút chế độ mặt bằng (thanh sửa mặt bằng + thanh dầm kiểu ETABS).</summary>
    private IEnumerable<RadioButton> PlanModeButtons()
    {
        var root = (planEditBar?.Parent as FrameworkElement)?.Parent as DependencyObject;
        var found = new List<RadioButton>();
        void Walk(object o)
        {
            if (o is RadioButton rb && rb.GroupName == "PlanMode") found.Add(rb);
            if (o is DependencyObject d) foreach (var c in LogicalTreeHelper.GetChildren(d)) Walk(c);
        }
        if (root != null) Walk(root);
        return found;
    }

    private void MnuDrawMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string tag) return;
        SetPlanMode(tag);
    }

    private bool SetPlanMode(string tag)
    {
        ShowPlanTab();
        var rb = PlanModeButtons().FirstOrDefault(b => b.Tag as string == tag);
        if (rb == null) { SetStatus("Không có chế độ " + tag); return false; }
        RibbonTabFor(rb);   // [Thanh lệnh cấu kiện] – MainWindow.CadCommandLine.cs
        if (rb.IsChecked == true) PlanMode_Checked(rb, new RoutedEventArgs());
        else rb.IsChecked = true;
        planHost.Focus();
        return true;
    }

    private void MnuBeamByPos_Click(object sender, RoutedEventArgs e) { ShowPlanTab(); BeamByPositionDialog(0, null); }

    private void MnuSelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string tag) SelectFramesOnStory(tag);
    }

    /// <summary>Chọn mọi dầm / cột / thanh của tầng đang xem (tất cả tầng khi xem "(Tất cả)").</summary>
    private void SelectFramesOnStory(string what)
    {
        if (_p == null) return;
        CheckMultiSelOwner();
        _multiSel.Clear();
        foreach (var f in PlanFrames().Where(f => what == "all" || (what == "columns") == f.IsVerticalKind)) _multiSel.Add(f.Id);
        ShowPlanTab();
        RedrawPlan();
        int nb = _multiSel.Count(id => _p.Frames.Any(f => f.Id == id && !f.IsVerticalKind));
        SetStatus($"Đã chọn {_multiSel.Count} thanh ({nb} dầm, {_multiSel.Count - nb} cột) ở {CurrentStory} – dùng menu Gán; Esc bỏ chọn.");
        RefreshModelExplorer();
    }

    private void MnuSelectBySection_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null) return;
        string cur = _sel?.Section ?? _p.FrameSections.FirstOrDefault()?.Name;
        string sec = AssignDialogs.AskSection(this, _p, cur, PlanFrames().Count());
        if (sec == null) return;
        CheckMultiSelOwner();
        _multiSel.Clear();
        foreach (var f in PlanFrames().Where(f => f.Section == sec)) _multiSel.Add(f.Id);
        ShowPlanTab(); RedrawPlan();
        SetStatus($"Đã chọn {_multiSel.Count} thanh tiết diện {sec} ở {CurrentStory}.");
    }

    private void MnuSelectClear_Click(object sender, RoutedEventArgs e)
    {
        _multiSel.Clear();
        ClearSelection();
        SetStatus("Đã bỏ chọn.");
    }

    // ------------------------------------------------------------------
    // Khai báo
    // ------------------------------------------------------------------
    private void MnuMaterials_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null || _busy) return;
        ApplyUiToProject(false);
        string snap = Snapshot();
        var w = new MaterialsWindow(_p) { Owner = this };
        w.ShowDialog();
        if (!w.Changed && Snapshot() == snap) return;
        OnWorkflowCommit(snap, "Đã cập nhật vật liệu – chạy lại phân tích / thiết kế.");
        LoadUiFromProject();
    }

    private void MnuLoadPatterns_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null || _busy) return;
        string snap = Snapshot();
        var w = new LoadPatternsWindow(_p) { Owner = this };
        w.ShowDialog();
        if (!w.Changed && Snapshot() == snap) return;
        OnWorkflowCommit(snap, $"Đã cập nhật trường hợp tải ({_p.Patterns.Count} trường hợp).");
        _comboWin?.Reload(null);
    }

    private void MnuLoadCases_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null) return;
        var w = new LoadCasesWindow(_p, _res, () => MnuModal_Click(this, new RoutedEventArgs()), () => MnuSpectrum_Click(this, new RoutedEventArgs())) { Owner = this };
        w.ShowDialog();
    }

    private void MnuComboEditor_Click(object sender, RoutedEventArgs e) => OpenComboEditor();

    private ComboEditorWindow OpenComboEditor()
    {
        if (_p == null) return null;
        if (_comboWin != null && _comboWin.IsLoaded) { _comboWin.Reload(null); _comboWin.Activate(); return _comboWin; }
        _comboWin = new ComboEditorWindow(() => _p, Snapshot, OnWorkflowCommit) { Owner = this };
        _comboWin.Closed += (s, e) => _comboWin = null;
        _comboWin.Show();
        return _comboWin;
    }

    private void MnuSpectrum_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null || _busy) return;
        ApplyUiToProject(false);
        string snap = Snapshot();
        var d = new SpectrumDialog(_p) { Owner = this };
        if (d.ShowDialog() != true) return;
        LoadUiFromProject();
        if (Snapshot() != snap) OnWorkflowCommit(snap, $"Đã cập nhật thông số động đất: agR = {_p.Seismic.AgR:0.####} g, nền {_p.Seismic.Soil}, q = {_p.Seismic.Q:0.##}, {_p.Seismic.Method}.");
    }

    private void MnuDiaphragms_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null) return;
        var r = AssignDialogs.AskDiaphragms(this, _p, CurrentStory);
        if (r == null) return;
        ApplyModelChange(() =>
        {
            _p.UseRigidDiaphragm = r.Value.on;
            _p.DiaphragmOff = r.Value.off.Count > 0 ? r.Value.off : null;
            int on = _p.Stories.Count(s => ModelAssign.DiaphragmOn(_p, s));
            return new EditResult { Ok = true, Message = r.Value.on ? $"Sàn cứng: {on}/{_p.Stories.Count} tầng." : "Đã tắt sàn cứng cho mọi tầng." };
        });
    }

    // ------------------------------------------------------------------
    // Gán
    // ------------------------------------------------------------------
    private void MnuAssignFrameLoad_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null) return;
        var ids = SelectedFrameIdsAll();
        if (!NeedFrames(ids, "Gán → Tải trên thanh")) return;
        var inp = AssignDialogs.AskFrameLoad(this, _p, ids.Count);
        if (inp == null) return;
        ApplyModelChange(() =>
        {
            int n = inp.Kind switch
            {
                0 => ModelAssign.FrameUniform(_p, ids, inp.Pattern, inp.Q, inp.Dir, inp.Mode),
                1 => ModelAssign.FrameTrapezoid(_p, ids, inp.Pattern, inp.D1, inp.Q1, inp.D2, inp.Q2, inp.Dir, inp.Relative, inp.Mode),
                _ => ModelAssign.FramePoint(_p, ids, inp.Pattern, inp.D, inp.P, inp.Dir, inp.Relative, inp.Mode)
            };
            string kind = inp.Mode == LoadAssignMode.Delete ? "Đã xoá tải" : inp.Kind == 0 ? $"Đã gán tải đều {inp.Q:0.##} kN/m" : inp.Kind == 1 ? "Đã gán tải hình thang" : $"Đã gán lực tập trung {inp.P:0.##} kN";
            return new EditResult { Ok = n > 0, Message = n > 0 ? $"{kind} ({inp.Pattern}) cho {n} thanh." : "Không gán được tải (thanh dài 0?)." };
        });
        EnsureLoadsVisible();
    }

    private void MnuAssignAreaLoad_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null) return;
        string st = CurrentStory;
        var inp = AssignDialogs.AskAreaLoad(this, _p, _selSlab != null, st);
        if (inp == null) return;
        var ids = inp.Scope == 0 && _selSlab != null ? new List<int> { _selSlab.Id }
                : inp.Scope == 1 ? PlanSlabs().Select(s => s.Id).ToList()
                : _p.Slabs.Select(s => s.Id).ToList();
        ApplyModelChange(() =>
        {
            int n = ModelAssign.AreaUniform(_p, ids, inp.Pattern, inp.Q, inp.Mode);
            return new EditResult { Ok = n > 0, Message = n > 0 ? $"{(inp.Mode == LoadAssignMode.Delete ? "Đã xoá" : $"Đã gán {inp.Q:0.##} kN/m²")} tải sàn ({inp.Pattern}) cho {n} ô sàn." : "Không có ô sàn nào." };
        });
        EnsureLoadsVisible();
    }

    private void MnuAssignJointLoad_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null) return;
        var inp = AssignDialogs.AskJointLoad(this, _p, SuggestedNodes(false));
        if (inp == null) return;
        ApplyModelChange(() =>
        {
            int n = ModelAssign.JointLoad(_p, inp.Nodes, inp.Pattern, inp.V, inp.Mode);
            return new EditResult { Ok = n > 0, Message = $"Đã {(inp.Mode == LoadAssignMode.Delete ? "xoá" : "gán")} tải nút ({inp.Pattern}) cho {n} nút." };
        });
        EnsureLoadsVisible();
    }

    private void MnuAssignRestraint_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null) return;
        var r = AssignDialogs.AskRestraint(this, _p, SuggestedNodes(true));
        if (r == null) return;
        ApplyModelChange(() =>
        {
            int n = ModelAssign.Restraint(_p, r.Value.nodes, r.Value.restraint);
            return new EditResult { Ok = n > 0, Message = $"Đã gán liên kết {ModelAssign.RestraintName(r.Value.restraint)} cho {n} nút." };
        });
    }

    private void MnuAssignRelease_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null) return;
        var ids = SelectedFrameIdsAll();
        if (!NeedFrames(ids, "Gán → Giải phóng đầu thanh")) return;
        var first = _p.Frames.First(f => f.Id == ids[0]);
        var r = AssignDialogs.AskReleases(this, first, ids.Count);
        if (r == null) return;
        ApplyModelChange(() => ModelAssign.Releases(_p, ids, r.Value.relI, r.Value.relJ));
    }

    private void MnuAssignSection_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null) return;
        var ids = SelectedFrameIdsAll();
        if (!NeedFrames(ids, "Gán → Tiết diện thanh")) return;
        string sec = AssignDialogs.AskSection(this, _p, _p.Frames.First(f => f.Id == ids[0]).Section, ids.Count);
        if (sec == null) return;
        ApplyModelChange(() => ModelAssign.Section(_p, ids, sec));
        _secBrushes.Clear();
        RedrawPlan(); Build3D();
    }

    private void MnuAssignModifiers_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null) return;
        var ids = SelectedFrameIdsAll();
        if (!NeedFrames(ids, "Gán → Hệ số độ cứng thanh")) return;
        var m = AssignDialogs.AskModifiers(this, _p.Frames.First(f => f.Id == ids[0]), ids.Count);
        if (m == null) return;
        ApplyModelChange(() =>
        {
            int n = ModelAssign.Modifiers(_p, ids, m);
            return new EditResult { Ok = n > 0, Message = $"Đã gán hệ số độ cứng (A {m[0]:0.##}, I22 {m[4]:0.##}, I33 {m[5]:0.##}, J {m[3]:0.##}) cho {n} thanh." };
        });
    }

    // ------------------------------------------------------------------
    // Phân tích → Kiểm tra mô hình
    // ------------------------------------------------------------------
    private void MnuCheckModel_Click(object sender, RoutedEventArgs e) => OpenModelCheck();

    private ModelCheckWindow OpenModelCheck()
    {
        if (_p == null) return null;
        ApplyUiToProject(false);
        if (_checkWin != null && _checkWin.IsLoaded) { _checkWin.RunCheck(); _checkWin.Activate(); return _checkWin; }
        _checkWin = new ModelCheckWindow(() => _p, Snapshot, OnWorkflowCommit, ZoomToIssue) { Owner = this };
        _checkWin.Closed += (s, e) => _checkWin = null;
        _checkWin.Show();
        var rep = _checkWin.Report;
        if (rep != null) SetStatus("Kiểm tra mô hình: " + rep.Summary);
        return _checkWin;
    }

    private void ZoomToIssue(ModelIssue i)
    {
        if (_p == null || i == null) return;
        if (!string.IsNullOrEmpty(i.Story) && _p.Stories.Any(s => s.Name == i.Story)) cbStory.SelectedItem = i.Story;
        CheckMultiSelOwner();
        _multiSel.Clear();
        foreach (var id in i.Frames) if (_p.Frames.Any(f => f.Id == id)) _multiSel.Add(id);
        var fr = i.Frames.Select(id => _p.Frames.FirstOrDefault(f => f.Id == id)).FirstOrDefault(f => f != null);
        var sl = i.Slabs.Select(id => _p.Slabs.FirstOrDefault(s => s.Id == id)).FirstOrDefault(s => s != null);
        ShowPlanTab();
        if (fr != null) Select(frame: fr);
        else if (sl != null) Select(slab: sl);
        ZoomPlanTo(i.X, i.Y, 3.0);
    }

    /// <summary>Đặt tâm mặt bằng tại (x, y) với bề rộng nhìn ≈ span (m).</summary>
    private void ZoomPlanTo(double x, double y, double span)
    {
        double w = planHost.ActualWidth, h = planHost.ActualHeight;
        if (w < 50 || h < 50) return;
        _scale = Math.Max(_scale, Math.Min(w, h) / Math.Max(span, 0.5));
        _scale = Math.Min(_scale, 400);
        _ox = w / 2 - x * _scale;
        _oy = h / 2 + y * _scale;
        RedrawPlan();
    }

    // ------------------------------------------------------------------
    // Hiển thị
    // ------------------------------------------------------------------
    private async void MnuDeformed_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null || _busy) return;
        if (!await EnsureAnalysis()) return;
        string c = cbCase.SelectedItem as string;
        if (c != null && _res.IsEnvelope(c)) c = _res.Cases.Keys.FirstOrDefault();
        if (_deformWin != null && _deformWin.IsLoaded) { _deformWin.Close(); }
        _deformWin = new DeformedShapeWindow(_p, _res, c) { Owner = this };
        _deformWin.Closed += (s, a) => _deformWin = null;
        _deformWin.Show();
    }

    private async void MnuTables_Click(object sender, RoutedEventArgs e) => await OpenTables(ResultTableKind.JointReactions);

    private async void MnuReactions_Click(object sender, RoutedEventArgs e) => await OpenTables(ResultTableKind.JointReactions);

    private async Task<ResultTablesWindow> OpenTables(ResultTableKind kind)
    {
        if (_p == null || _busy) return null;
        if (!await EnsureAnalysis()) return null;
        if (_tablesWin != null && _tablesWin.IsLoaded) _tablesWin.Close();
        _tablesWin = new ResultTablesWindow(_p, _res, kind) { Owner = this };
        _tablesWin.Closed += (s, a) => _tablesWin = null;
        _tablesWin.Show();
        return _tablesWin;
    }

    private void MnuShowAssignedLoads_Click(object sender, RoutedEventArgs e)
    {
        _showAssignedLoads = mnuShowLoads.IsChecked;
        ShowPlanTab();
        RedrawPlan();
        SetStatus(_showAssignedLoads ? "Hiển thị tải gán trên mặt bằng (tải thanh, sàn, nút; theo trường hợp ở ô 'Biểu đồ M3 theo' nếu là trường hợp tải, ngược lại mọi trường hợp)." : "Đã tắt hiển thị tải gán.");
    }

    /// <summary>Sau khi gán tải: bật hiển thị tải gán để người dùng thấy ngay.</summary>
    private void EnsureLoadsVisible()
    {
        if (_showAssignedLoads) return;
        _showAssignedLoads = true;
        if (mnuShowLoads != null) mnuShowLoads.IsChecked = true;
        RedrawPlan();
    }
}

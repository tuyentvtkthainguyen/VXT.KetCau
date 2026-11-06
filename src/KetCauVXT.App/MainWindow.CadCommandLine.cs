using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Model;
using KetCauVXT.Ui;

namespace KetCauVXT.App;

// =====================================================================
// DÒNG LỆNH KIỂU AUTOCAD dưới mặt bằng (chế độ Thiết kế):
//   • gõ lệnh (L, C, S, W, M, CO, MI, AR, O, TR, EX, ST, E, U, Z/ZE, DI, MA, TAI, SS, ?) – có thể gõ thẳng khi đang trỏ mặt bằng;
//   • toạ độ: x,y | @dx,dy | @L<góc | số = chiều dài theo hướng chuột (vẽ dầm dùng ô nhập toạ độ sẵn có – ApplyCoordInput);
//   • F3 bắt điểm, F8 trực giao, F10 góc cực 45°; Space lặp lệnh; Esc huỷ bước lệnh; ↑↓ lịch sử lệnh;
//   • các bước lệnh cần bấm điểm: đặt cột (C), đo khoảng cách (DI), sao thuộc tính (MA) – PlanClick gọi CmdPlanClick trước;
//   • lệnh nhập số: COPY (dx,dy n), ARRAY (nx ny dx dy), OFFSET (d), MIRROR (tên trục) áp cho dầm đang chọn.
// Lõi: Ui/CadCommand.cs, Model/SelectionRules.cs (PlanDrawOps), Model/BeamTools.cs.
// =====================================================================
public partial class MainWindow
{
    private enum CmdStage { None, ColumnPick, Dist1, Dist2, MatchSource, MatchTarget, CopyVec, ArrayParams, OffsetDist, MirrorAxis }

    private TextBox _cmdBox;
    private TextBlock _cmdPrompt;
    private ToggleButton _tgOsnap, _tgOrtho, _tgPolar;
    private bool _cmdExact, _orthoOn, _polarOn, _osnapOn = true;
    private CadCmd _lastCmd = CadCmd.None;
    private CmdStage _cmdStage = CmdStage.None;
    private PlanEditMode _cmdStageMode;
    private (double X, double Y) _distA;
    private string _matchSection;
    private readonly List<string> _cmdHistory = new();
    private int _cmdHistIdx = -1;

    private void InitCadCommandLine()
    {
        try
        {
            if (planHost?.Parent is not DockPanel dock) return;
            var grid = new Grid { Margin = new Thickness(2, 1, 2, 1) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _cmdPrompt = new TextBlock { Text = "Lệnh:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 6, 0), FontFamily = new FontFamily("Consolas"), Foreground = Brushes.DimGray, MaxWidth = 520, TextTrimming = TextTrimming.CharacterEllipsis };
            _cmdBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas"), FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center, MinWidth = 120,
                ToolTip = "Dòng lệnh kiểu AutoCAD: L dầm, C cột, S sàn, W tường, M dời, CO sao chép, MI đối xứng, AR mảng, O offset, TR trim, EX extend, ST kéo, E xoá, U hoàn tác, Z/ZE zoom, DI đo, MA sao thuộc tính, TAI gán tải, SS chọn tương tự, ? trợ giúp.\nToạ độ: x,y | @dx,dy | @L<góc | số = chiều dài theo hướng chuột. Enter/Space lặp lệnh, Esc huỷ, ↑↓ lịch sử."
            };
            _cmdBox.PreviewKeyDown += CmdBox_PreviewKeyDown;
            Grid.SetColumn(_cmdBox, 1);
            var tg = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0) };
            ToggleButton T(string text, string tip, bool on, Action<bool> set)
            {
                var b = new ToggleButton { Content = text, IsChecked = on, Padding = new Thickness(5, 0, 5, 0), Margin = new Thickness(0, 0, 2, 0), FontSize = 11, ToolTip = tip, Focusable = false };
                b.Checked += (s, e) => set(true); b.Unchecked += (s, e) => set(false);
                tg.Children.Add(b);
                return b;
            }
            _tgOsnap = T("BẮT ĐIỂM F3", "Bắt điểm: nút, đầu dầm, giao trục, trung điểm, vuông góc, gần nhất, nét bản vẽ CAD (□ △ × ⊥ ○)", true, v => { _osnapOn = v; DrawHover(); });
            _tgOrtho = T("TRỰC GIAO F8", "Vẽ dầm / tường chỉ theo phương X hoặc Y", false, v => { _orthoOn = v; if (v && _tgPolar != null) _tgPolar.IsChecked = false; DrawHover(); });
            _tgPolar = T("CỰC 45° F10", "Bắt góc cực bội số 45° khi vẽ dầm / tường", false, v => { _polarOn = v; if (v && _tgOrtho != null) _tgOrtho.IsChecked = false; DrawHover(); });
            Grid.SetColumn(tg, 2);
            grid.Children.Add(_cmdPrompt); grid.Children.Add(_cmdBox); grid.Children.Add(tg);
            var border = new Border { Child = grid, Background = new SolidColorBrush(Color.FromRgb(246, 246, 246)), BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204)), BorderThickness = new Thickness(0, 1, 0, 0), Tag = "CommandLine" };
            DockPanel.SetDock(border, Dock.Bottom);
            dock.Children.Insert(dock.Children.IndexOf(planHost), border);
            planHost.PreviewTextInput += CmdPlanTextInput;
            AddHandler(PreviewKeyDownEvent, new KeyEventHandler(CmdWindowKeyDown), true);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Dòng lệnh: " + ex.Message); }
    }

    private bool PlanTabActive => tabView?.SelectedItem is TabItem ti && (ti.Header as string) == "Mặt bằng";

    private void SetPrompt(string s)
    {
        if (_cmdPrompt != null) _cmdPrompt.Text = string.IsNullOrEmpty(s) ? "Lệnh:" : s;
        if (!string.IsNullOrEmpty(s)) SetStatus(s);
    }

    // ==================================================================
    // Phím
    // ==================================================================
    private void CmdWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (_busy || _cmdBox == null || !PlanTabActive) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.F3) { _tgOsnap.IsChecked = !_osnapOn; SetStatus("Bắt điểm: " + (_osnapOn ? "BẬT" : "TẮT")); e.Handled = true; return; }
        if (key == Key.F8) { _tgOrtho.IsChecked = !_orthoOn; SetStatus("Trực giao: " + (_orthoOn ? "BẬT" : "TẮT")); e.Handled = true; return; }
        if (key == Key.F10) { _tgPolar.IsChecked = !_polarOn; SetStatus("Góc cực 45°: " + (_polarOn ? "BẬT" : "TẮT")); e.Handled = true; return; }
        if (ReferenceEquals(Keyboard.FocusedElement, _cmdBox)) return;
        if (key == Key.Escape && _cmdStage != CmdStage.None) { CancelStage(); e.Handled = true; return; }
        if (e.Handled || IsTextInputFocused()) return;
        if (key == Key.Space && ReferenceEquals(Keyboard.FocusedElement, planHost) && _lineStart == null && _lastCmd != CadCmd.None)
        { RunCmd(_lastCmd, ""); e.Handled = true; }
    }

    /// <summary>Gõ chữ khi đang trỏ mặt bằng → chuyển vào dòng lệnh (số trong ＋ Dầm vẫn mở ô toạ độ cũ – đăng ký trước).</summary>
    private void CmdPlanTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.Handled || _cmdBox == null || string.IsNullOrEmpty(e.Text)) return;
        if (!ReferenceEquals(Keyboard.FocusedElement, planHost)) return;
        char ch = e.Text[0];
        if (!(char.IsLetterOrDigit(ch) || ch is '@' or '?' or '-' or '.' or '<' or '#')) return;
        _cmdBox.Text = e.Text;
        _cmdBox.Focus();
        _cmdBox.CaretIndex = _cmdBox.Text.Length;
        e.Handled = true;
    }

    private void CmdBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:   // Key.Return trùng giá trị Key.Enter
                e.Handled = true;
                ExecuteCommandLine(_cmdBox.Text);
                break;
            case Key.Space when _cmdBox.Text.Trim().Length > 0 && !_cmdBox.Text.Contains('<') && _cmdStage is not (CmdStage.CopyVec or CmdStage.ArrayParams):
                e.Handled = true;   // Space = Enter như AutoCAD (trừ khi nhập nhiều số)
                ExecuteCommandLine(_cmdBox.Text);
                break;
            case Key.Escape:
                e.Handled = true;
                _cmdBox.Clear();
                CancelStage();
                planHost.Focus();
                break;
            case Key.Up:
            case Key.Down:
                if (_cmdHistory.Count == 0) break;
                e.Handled = true;
                _cmdHistIdx = e.Key == Key.Up ? (_cmdHistIdx <= 0 ? _cmdHistory.Count - 1 : _cmdHistIdx - 1) : (_cmdHistIdx + 1) % _cmdHistory.Count;
                _cmdBox.Text = _cmdHistory[_cmdHistIdx];
                _cmdBox.CaretIndex = _cmdBox.Text.Length;
                break;
        }
    }

    // ==================================================================
    // Thực hiện
    // ==================================================================
    private void ExecuteCommandLine(string text)
    {
        if (_p == null) return;
        string raw = (text ?? "").Trim();
        _cmdBox?.Clear();
        if (raw.Length > 0) { _cmdHistory.Remove(raw); _cmdHistory.Add(raw); if (_cmdHistory.Count > 40) _cmdHistory.RemoveAt(0); _cmdHistIdx = -1; }
        try
        {
            if (raw.Length > 0 && _cmdStage is CmdStage.CopyVec or CmdStage.ArrayParams or CmdStage.OffsetDist or CmdStage.MirrorAxis
                && CadCommand.Resolve(raw.Split(' ')[0]) == CadCmd.None)
            { StageText(raw); return; }
            string st = CurrentStory;
            (double X, double Y)? last = _cmdStage == CmdStage.Dist2 ? _distA : _lineStart != null && _lineStory == st ? _lineStart : null;
            (double X, double Y)? dir = null;
            if (_lastMouse != null) { var (mx, my) = S2W(_lastMouse.Value); dir = (mx, my); }
            var inp = CadCommand.Parse(raw, last, _coordUnit, dir);
            switch (inp.Kind)
            {
                case CadInputKind.Empty:
                    if (_cmdStage != CmdStage.None) { CancelStage(); return; }
                    if (_lineStart != null) { _lineStart = null; _arcEnd = null; DrawHover(); SetPrompt(""); return; }
                    if (_lastCmd != CadCmd.None) RunCmd(_lastCmd, "");
                    return;
                case CadInputKind.Error: SetStatus(inp.Error); return;
                case CadInputKind.Command: RunCmd(inp.Cmd, inp.Arg); return;
                default: PlaceTypedPoint(raw, inp); return;
            }
        }
        finally { if (_cmdStage == CmdStage.None && Keyboard.FocusedElement == _cmdBox) planHost.Focus(); }
    }

    /// <summary>Điểm gõ (x,y | @dx,dy | @L&lt;a | chiều dài): bước lệnh đang chờ điểm, hoặc như bấm chuột đúng toạ độ (không bắt điểm).</summary>
    private void PlaceTypedPoint(string raw, CadCommandInput inp)
    {
        string st = CurrentStory;
        if (_mode == PlanEditMode.AddBeam && _cmdStage == CmdStage.None) { ApplyCoordInput(raw); return; }   // ô nhập toạ độ của ＋ Dầm
        if (st == AllStories || _p.Stories.All(q => q.Name != st)) { SetStatus("Chọn một tầng cụ thể trước."); return; }
        var pt = (inp.X, inp.Y);
        switch (_cmdStage)
        {
            case CmdStage.ColumnPick: AddColumnAt(st, pt); return;
            case CmdStage.Dist1: DistPoint(pt); return;
            case CmdStage.Dist2: DistPoint(pt); return;
        }
        _cmdExact = true;
        try { PlanClick(W2S(pt.Item1, pt.Item2)); }
        finally { _cmdExact = false; }
    }

    /// <summary>[Thanh lệnh cấu kiện] mở tab chứa nút chế độ (gọi từ SetPlanMode – MainWindow.Workflow.cs).</summary>
    private void RibbonTabFor(FrameworkElement rb)
    {
        DependencyObject d = rb;
        while (d != null && d is not TabItem) d = LogicalTreeHelper.GetParent(d);
        if (d is TabItem ti && _ribbon != null && _ribbon.Items.Contains(ti)) _ribbon.SelectedItem = ti;
    }

    private void SelectRibbonTab(string header)
    {
        if (_ribbon?.Items.OfType<TabItem>().FirstOrDefault(t => (t.Header as string) == header) is TabItem ti) _ribbon.SelectedItem = ti;
    }

    private void RunCmd(CadCmd cmd, string arg)
    {
        if (_p == null) return;
        if (cmd is not (CadCmd.Undo or CadCmd.Redo or CadCmd.Zoom or CadCmd.ZoomExtents or CadCmd.Help or CadCmd.Ortho or CadCmd.Polar or CadCmd.Osnap)) _lastCmd = cmd;
        CancelStage(false);
        try { tabView.SelectedItem = ViewTab("Mặt bằng"); } catch (Exception) { }
        switch (cmd)
        {
            case CadCmd.Beam: SetPlanMode(nameof(PlanEditMode.AddBeam)); SetPrompt("DẦM – bấm / gõ điểm đầu (x,y), rồi các điểm tiếp (@dx,dy | @L<góc | chiều dài); Enter kết thúc:"); break;
            case CadCmd.Wall: SetPlanMode(nameof(PlanEditMode.AddWall)); SetPrompt("TƯỜNG – bấm / gõ điểm đầu, điểm cuối; Enter kết thúc:"); break;
            case CadCmd.Slab: SetPlanMode(nameof(PlanEditMode.AddSlab)); SetPrompt("SÀN – bấm vào ô kín bởi dầm để thêm ô sàn:"); break;
            case CadCmd.Move: SetPlanMode(nameof(PlanEditMode.MoveBeam)); SetPrompt("DỜI DẦM – bấm dầm (hoặc dầm đã chọn) tại điểm gốc, rồi điểm đích:"); break;
            case CadCmd.Stretch: SetPlanMode(nameof(PlanEditMode.StretchBeam)); SetPrompt("KÉO DẦM – bấm gần đầu dầm, rồi điểm mới:"); break;
            case CadCmd.Trim: SetPlanMode(nameof(PlanEditMode.TrimBeam)); SetPrompt("TRIM – bấm đoạn dầm cần cắt bỏ:"); break;
            case CadCmd.Extend: SetPlanMode(nameof(PlanEditMode.ExtendBeam)); SetPrompt("EXTEND – bấm gần đầu dầm, rồi dầm đích (Enter: tới dầm vuông góc gần nhất):"); break;
            case CadCmd.Select: SetPlanMode(nameof(PlanEditMode.MultiSelect)); SetPrompt("CHỌN – bấm / kéo khung (trái→phải: trọn trong khung; phải→trái: chạm khung), Ctrl để bỏ:"); break;
            case CadCmd.SelectSimilar: SelectSimilar(SimilarBy.Section, false); break;
            case CadCmd.Column: StartCadCommand(CadCmd.Column); break;
            case CadCmd.Erase:
            {
                bool any = SelectedBeamIds().Count + SelectedColumnIds().Count > 0 || _selSlab != null || _selWall != null || _selOpening != null;
                if (any) { if (_mode != PlanEditMode.MultiSelect && _multiSel.Count > 1) EnterMultiSelect(); planHost.Focus(); HandlePlanEditKey(Key.Delete, false); }
                else { SetPlanMode(nameof(PlanEditMode.Delete)); SetPrompt("XOÁ – bấm vào tường, dầm, cột, ô sàn / lỗ sàn cần xoá:"); }
                break;
            }
            case CadCmd.Undo: Undo(); break;
            case CadCmd.Redo: SetStatus("Chưa có làm lại (Redo) – dùng U / Ctrl+Z để hoàn tác."); break;
            case CadCmd.Zoom:
            {
                if (BeamTools.TryLength(arg, CoordUnit.M, out double k) && k > 0.05 && k < 50)
                {
                    double cx = planHost.ActualWidth / 2, cy = planHost.ActualHeight / 2;
                    _ox = cx - (cx - _ox) * k; _oy = cy - (cy - _oy) * k; _scale *= k;
                    RedrawPlan();
                }
                else FitPlan();
                break;
            }
            case CadCmd.ZoomExtents: FitPlan(); break;
            case CadCmd.Dist: StartStage(CmdStage.Dist1, "ĐO – bấm / gõ điểm thứ nhất:"); break;
            case CadCmd.Match: StartStage(CmdStage.MatchSource, "SAO THUỘC TÍNH – bấm dầm / cột nguồn:"); break;
            case CadCmd.Load:
            {
                if (SelectedSlabIds().Count > 0) SelectRibbonTab(RtSan);
                else if (SelectedColumnIds().Count > 0) SelectRibbonTab(RtCot);
                else SelectRibbonTab(RtDam);
                string k = (_ribbon?.SelectedItem as TabItem)?.Header as string ?? RtDam;
                if (_loadBoxes.TryGetValue(k, out var lb)) { lb.V1.Focus(); lb.V1.SelectAll(); }
                SetStatus("GÁN TẢI – chọn đối tượng (Chọn nhiều), chọn trường hợp tải, nhập giá trị rồi bấm 'Gán'.");
                break;
            }
            case CadCmd.Copy: if (NeedBeams()) StartStage(CmdStage.CopyVec, "SAO CHÉP – gõ dx,dy [số bản] (vd 3600,0 2):"); break;
            case CadCmd.Array: if (NeedBeams()) StartStage(CmdStage.ArrayParams, "MẢNG – gõ số cột số hàng dx dy (vd 3 2 3600 4500):"); break;
            case CadCmd.Offset: if (NeedBeams()) StartStage(CmdStage.OffsetDist, "OFFSET – gõ khoảng cách (dương: bên trái hướng I→J, âm: bên phải):"); break;
            case CadCmd.Mirror: if (NeedBeams()) StartStage(CmdStage.MirrorAxis, "ĐỐI XỨNG – gõ tên trục đối xứng (vd 2 hoặc B):"); break;
            case CadCmd.Ortho: _tgOrtho.IsChecked = !_orthoOn; break;
            case CadCmd.Polar: _tgPolar.IsChecked = !_polarOn; break;
            case CadCmd.Osnap: _tgOsnap.IsChecked = !_osnapOn; break;
            case CadCmd.Help: SetStatus(CadCommand.HelpText()); break;
        }
    }

    private bool NeedBeams()
    {
        if (SelectedBeamIds().Count > 0) return true;
        SetStatus("Chọn dầm trước (lệnh CH hoặc 'Chọn nhiều'), rồi gõ lại lệnh.");
        return false;
    }

    /// <summary>Bắt đầu lệnh cần bấm điểm (gọi cả từ nút ＋ Cột).</summary>
    private void StartCadCommand(CadCmd cmd)
    {
        if (cmd != CadCmd.Column) { RunCmd(cmd, ""); return; }
        _lastCmd = CadCmd.Column;
        if (_mode != PlanEditMode.Select) { _mode = PlanEditMode.Select; _lineStart = null; ResetBeamEdit(); }
        SelectRibbonTab(RtCot);
        StartStage(CmdStage.ColumnPick, $"CỘT {ColumnSectionCurrent()} – bấm / gõ vị trí cột (x,y); Esc kết thúc:");
        if (planHost != null) planHost.Cursor = Cursors.Cross;
    }

    private void StartStage(CmdStage stage, string prompt)
    {
        _cmdStage = stage; _cmdStageMode = _mode;
        SetPrompt(prompt);
        if (stage is CmdStage.CopyVec or CmdStage.ArrayParams or CmdStage.OffsetDist or CmdStage.MirrorAxis) { _cmdBox?.Focus(); }
    }

    private void CancelStage(bool report = true)
    {
        if (_cmdStage == CmdStage.None) return;
        _cmdStage = CmdStage.None;
        if (planHost != null && _mode == PlanEditMode.Select) planHost.Cursor = null;
        foreach (var rb in PlanModeButtons().Where(b => b.Tag as string == "CmdColumn")) rb.IsChecked = false;
        SetPrompt("");
        if (report) SetStatus("Đã kết thúc lệnh.");
        DrawHover();
    }

    // ==================================================================
    // Bước lệnh bấm điểm (gọi đầu PlanClick)
    // ==================================================================
    private bool CmdPlanClick(Point s)
    {
        if (_cmdStage == CmdStage.None || _p == null) return false;
        if (_mode != _cmdStageMode) { CancelStage(false); return false; }
        if (_cmdStage is CmdStage.CopyVec or CmdStage.ArrayParams or CmdStage.OffsetDist or CmdStage.MirrorAxis) return false;
        string st = CurrentStory;
        if (st == AllStories || _p.Stories.All(q => q.Name != st)) { SetStatus("Chọn một tầng cụ thể ở ô 'Tầng đang xem'."); return true; }
        var (x, y) = S2W(s);
        switch (_cmdStage)
        {
            case CmdStage.ColumnPick: AddColumnAt(st, SnapPlan(st, x, y)); return true;
            case CmdStage.Dist1:
            case CmdStage.Dist2: DistPoint(SnapPlan(st, x, y)); return true;
            case CmdStage.MatchSource:
            {
                var f = (FrameElement)ColumnAt(s) ?? NearestBeam(s, 8);
                if (f == null) { SetStatus("Bấm vào một dầm / cột nguồn."); return true; }
                _matchSection = f.Section;
                var targets = f.IsVerticalKind ? SelectedColumnIds() : SelectedBeamIds();
                targets.Remove(f.Id);
                if (targets.Count > 0)
                {
                    RunBeamOp(() => ModelAssign.Section(_p, targets, _matchSection), false);
                    CancelStage(false);
                    SetStatus($"Đã sao tiết diện {_matchSection} cho {targets.Count} {(f.IsVerticalKind ? "cột" : "dầm")} đang chọn.");
                }
                else StartStage(CmdStage.MatchTarget, $"SAO THUỘC TÍNH ({_matchSection}) – bấm các dầm / cột đích; Esc kết thúc:");
                return true;
            }
            case CmdStage.MatchTarget:
            {
                var f = (FrameElement)ColumnAt(s) ?? NearestBeam(s, 8);
                if (f == null) return true;
                var sec = _matchSection;
                RunBeamOp(() => ModelAssign.Section(_p, new[] { f.Id }, sec), false);
                _cmdStage = CmdStage.MatchTarget; _cmdStageMode = _mode;
                return true;
            }
        }
        return false;
    }

    private void AddColumnAt(string st, (double X, double Y) pt)
    {
        string sec = ColumnSectionCurrent();
        if (string.IsNullOrEmpty(sec)) { SetStatus("Chọn tiết diện cột ở tab Cột."); return; }
        var stage = _cmdStage; var mode = _mode;
        RunBeamOp(() => PlanDrawOps.AddColumn(_p, st, pt.X, pt.Y, sec), false);
        _cmdStage = stage; _cmdStageMode = mode;   // tiếp tục đặt cột
        SetPrompt($"CỘT {sec} – bấm / gõ vị trí cột tiếp theo; Esc kết thúc:");
    }

    private void DistPoint((double X, double Y) pt)
    {
        if (_cmdStage == CmdStage.Dist1) { _distA = pt; StartStage(CmdStage.Dist2, $"ĐO – điểm 1 ({pt.X:0.000}; {pt.Y:0.000}); bấm / gõ điểm thứ hai (@dx,dy…):"); return; }
        double dx = pt.X - _distA.X, dy = pt.Y - _distA.Y, d = Math.Sqrt(dx * dx + dy * dy), ang = Math.Atan2(dy, dx) * 180 / Math.PI;
        _cmdStage = CmdStage.None;
        SetPrompt("");
        SetStatus($"Khoảng cách = {d * 1000:0} mm   Δx = {dx * 1000:0}   Δy = {dy * 1000:0}   góc = {ang:0.##}°");
    }

    // ==================================================================
    // Bước lệnh nhập số
    // ==================================================================
    private List<double> Lengths(string raw, int max)
    {
        var toks = raw.Split(new[] { ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (toks.Count == 1 && toks[0].Contains(',')) toks = toks[0].Split(',').ToList();
        var res = new List<double>();
        foreach (var t in toks.Take(max)) { if (!BeamTools.TryLength(t, _coordUnit, out double v)) return null; res.Add(v); }
        return res;
    }

    private void StageText(string raw)
    {
        var ids = SelectedBeamIds();
        if (ids.Count == 0) { CancelStage(false); SetStatus("Không còn dầm nào đang chọn."); return; }
        var stage = _cmdStage;
        CancelStage(false);
        switch (stage)
        {
            case CmdStage.CopyVec:
            {
                var parts = raw.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var v = Lengths(parts[0], 2);
                int n = parts.Length > 1 && int.TryParse(parts[1], out int k) ? k : 1;
                if (v == null || v.Count != 2) { SetStatus("Gõ dx,dy [số bản], vd 3600,0 2"); StartStage(CmdStage.CopyVec, _cmdPrompt.Text); return; }
                RunBeamOp(() => BeamTools.Replicate(_p, ids, v[0], v[1], Math.Max(1, n)));
                break;
            }
            case CmdStage.ArrayParams:
            {
                var parts = raw.Split(new[] { ' ', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4 || !int.TryParse(parts[0], out int nx) || !int.TryParse(parts[1], out int ny)
                    || !BeamTools.TryLength(parts[2], _coordUnit, out double dx) || !BeamTools.TryLength(parts[3], _coordUnit, out double dy))
                { SetStatus("Gõ: số cột  số hàng  dx  dy (vd 3 2 3600 4500)"); return; }
                RunBeamOp(() => PlanDrawOps.ArrayBeams(_p, ids, nx, ny, dx, dy));
                break;
            }
            case CmdStage.OffsetDist:
            {
                if (!BeamTools.TryLength(raw.Trim(), _coordUnit, out double d)) { SetStatus("Gõ khoảng offset, vd 1200 hoặc -1200"); return; }
                RunBeamOp(() => PlanDrawOps.OffsetBeams(_p, ids, d));
                break;
            }
            case CmdStage.MirrorAxis:
            {
                var g = BeamTools.FindGrid(_p, raw.Trim());
                if (g == null) { SetStatus($"Không có trục \"{raw.Trim()}\"."); return; }
                RunBeamOp(() => BeamTools.Mirror(_p, ids, g));
                break;
            }
        }
    }

    // ==================================================================
    // F8 / F10 / F3 – gọi trong SnapPlan
    // ==================================================================
    private (double X, double Y) DraftConstrain(double x, double y, (double X, double Y) snapped)
    {
        var pt = _osnapOn ? snapped : (Math.Round(x, 3), Math.Round(y, 3));
        if ((!_orthoOn && !_polarOn) || _lineStart == null || !IsLineMode) return pt;
        var start = _lineStart.Value;
        var q = CadCommand.Constrain(start, (x, y), _orthoOn, _polarOn ? 45 : 0);
        if (!_orthoOn && Math.Abs(q.X - x) < 1e-12 && Math.Abs(q.Y - y) < 1e-12) return pt;   // góc cực chưa bắt
        double ux = q.X - start.X, uy = q.Y - start.Y, L = Math.Sqrt(ux * ux + uy * uy);
        if (L < 1e-9) return pt;
        ux /= L; uy /= L;
        double t = (pt.Item1 - start.X) * ux + (pt.Item2 - start.Y) * uy;
        return (Math.Round(start.X + ux * t, 4), Math.Round(start.Y + uy * t, 4));
    }
}

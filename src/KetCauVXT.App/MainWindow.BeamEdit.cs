using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Import.Link;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// SỬA DẦM KIỂU AUTOCAD trên mặt bằng (lõi: Core/Model/BeamEdit.cs, Core/Import/Link/CadPick.cs):
//   • Kéo dầm: bấm gần đầu dầm → bấm điểm mới (bắt điểm như ＋ Dầm, kể cả nét bản vẽ link); giữ Shift khi bấm dầm = dời cả dầm;
//   • Dời dầm: bấm dầm (hoặc các dầm đã "Chọn nhiều") + điểm gốc → bấm điểm đích;
//   • Trim: bấm vào đoạn dầm cần bỏ (giới hạn bởi dầm / cột giao gần nhất);
//   • Extend: bấm gần đầu dầm cần kéo dài → bấm dầm đích, hoặc chuột phải / Enter = tự tìm dầm vuông góc gần nhất;
//   • Nối mọi dầm hở của tầng vào dầm vuông góc gần nhất (≤ khoảng nối);
//   • Pick CAD: bấm giữa hai mép dầm / tường của bản vẽ liên kết → dầm đúng trục, bề rộng; bấm trong đa tuyến kín → ô sàn /
//     ô cầu thang / giếng trời (dầm bao tự thêm).
// =====================================================================
public partial class MainWindow
{
    private static readonly Brush BeamEditBrush = Frozen(Color.FromRgb(200, 30, 160));
    private static readonly Brush TrimBrush = Frozen(Color.FromRgb(220, 40, 40));

    private bool IsBeamEditMode => _mode is PlanEditMode.StretchBeam or PlanEditMode.MoveBeam or PlanEditMode.TrimBeam or PlanEditMode.ExtendBeam or PlanEditMode.PickCad;

    // trạng thái thao tác 2 bước
    private int _beFrame;          // dầm đang thao tác (0 = chưa chọn)
    private bool _beAtJ;           // đầu J
    private bool _beMoveAll;       // Shift: dời cả dầm
    private (double X, double Y)? _beBase;   // điểm gốc khi dời
    private List<int> _beMoveIds = new();
    private ComboBox _cbPickKind;
    private TextBox _txtJoinGap;

    private static string BeamEditHint(PlanEditMode m) => m switch
    {
        PlanEditMode.StretchBeam => "Kéo dầm: bấm gần ĐẦU dầm cần kéo, rồi bấm điểm mới (bắt nút, giao trục, nét bản vẽ link…). Giữ Shift khi bấm dầm để dời CẢ dầm song song. Esc huỷ.",
        PlanEditMode.MoveBeam => "Dời dầm: bấm vào dầm (hoặc dùng các dầm đã 'Chọn nhiều') tại điểm gốc, rồi bấm điểm đích – dầm dời song song, nút giao được chia lại. Esc huỷ.",
        PlanEditMode.TrimBeam => "Trim dầm: bấm vào đoạn dầm cần bỏ – phần giữa hai dầm / cột giao gần nhất (hoặc phần nhô quá dầm giao) bị cắt bỏ.",
        PlanEditMode.ExtendBeam => "Extend dầm: bấm gần ĐẦU dầm cần kéo dài, rồi bấm dầm đích; hoặc chuột phải / Enter để tự kéo tới dầm vuông góc gần nhất (không cần nhớ kích thước).",
        PlanEditMode.PickCad => "Pick bản vẽ CAD: chọn 'Pick tạo' rồi bấm – Dầm: bấm giữa hai mép dầm / tường (hoặc vào một nét) → dầm đúng trục, bề rộng đo được; Sàn / Cầu thang / Giếng trời: bấm trong đa tuyến kín → dầm bao + ô sàn / ô thang / lỗ.",
        _ => ""
    };

    private void InitBeamEditUi()
    {
        if (_beamToolsExpander?.Content is not WrapPanel bar) return;
        var tgStyle = TryFindResource(typeof(ToggleButton)) as Style;
        void Lab(string t, bool bold = false) => bar.Children.Add(new TextBlock { Text = t, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 2), FontWeight = bold ? FontWeights.Bold : FontWeights.Normal });
        void Mode(string text, PlanEditMode m, string tip)
        {
            var rb = new RadioButton { Content = text, Tag = m.ToString(), GroupName = "PlanMode", ToolTip = tip, Margin = new Thickness(0, 0, 3, 2), Padding = new Thickness(7, 2, 7, 2) };
            if (tgStyle != null) rb.Style = tgStyle;
            rb.Checked += PlanMode_Checked;
            bar.Children.Add(rb);
        }
        Lab("Sửa dầm:", true);
        Mode("Kéo dầm", PlanEditMode.StretchBeam, BeamEditHint(PlanEditMode.StretchBeam));
        Mode("Dời dầm", PlanEditMode.MoveBeam, BeamEditHint(PlanEditMode.MoveBeam));
        Mode("Trim", PlanEditMode.TrimBeam, BeamEditHint(PlanEditMode.TrimBeam));
        Mode("Extend", PlanEditMode.ExtendBeam, BeamEditHint(PlanEditMode.ExtendBeam));
        var join = new Button { Content = "Nối dầm hở", Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(0, 0, 3, 2), ToolTip = "Kéo mọi đầu dầm hở của tầng đang xem tới dầm vuông góc gần nhất (≤ khoảng nối)" };
        join.Click += (s, e) => JoinAllBeams();
        bar.Children.Add(join);
        Lab("khoảng nối (mm):");
        _txtJoinGap = new TextBox { Text = "1500", Width = 46, Margin = new Thickness(0, 0, 3, 2), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Khoảng tối đa kéo dài khi nối dầm hở / Extend tự động" };
        bar.Children.Add(_txtJoinGap);
        Mode("Pick CAD", PlanEditMode.PickCad, BeamEditHint(PlanEditMode.PickCad));
        Lab("tạo:");
        _cbPickKind = new ComboBox { Width = 110, Margin = new Thickness(0, 0, 3, 2), ToolTip = "Đối tượng tạo khi bấm vào bản vẽ liên kết (chế độ Pick CAD)" };
        foreach (var t in new[] { "Dầm", "Sàn", "Cầu thang", "Giếng trời / lỗ", "Chỉ dầm bao" }) _cbPickKind.Items.Add(t);
        _cbPickKind.SelectedIndex = 0;
        bar.Children.Add(_cbPickKind);
    }

    private void ResetBeamEdit()
    {
        _beFrame = 0; _beAtJ = false; _beMoveAll = false; _beBase = null; _beMoveIds = new List<int>();
    }

    private double JoinGap()
    {
        string t = (_txtJoinGap?.Text ?? "1500").Replace(',', '.');
        return double.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double mm) && mm > 0 ? mm / 1000 : 1.5;
    }

    private FrameElement BeamEditFrame => _beFrame > 0 ? _p?.Frames.FirstOrDefault(f => f.Id == _beFrame) : null;

    /// <summary>Đầu dầm gần điểm màn hình hơn: true = đầu J.</summary>
    private bool NearJ(FrameElement f, Point s)
    {
        var ni = _p.Node(f.NodeI); var nj = _p.Node(f.NodeJ);
        var a = W2S(ni.X, ni.Y); var b = W2S(nj.X, nj.Y);
        return (s - b).Length < (s - a).Length;
    }

    /// <summary>Thực hiện một thao tác sửa có hoàn tác.</summary>
    private void RunBeamEdit(Func<(bool Ok, string Msg)> op, Func<object> select = null)
    {
        string snap = Snapshot();
        (bool ok, string msg) = (false, "");
        try { (ok, msg) = op(); }
        catch (Exception ex)
        {
            RestoreSnapshot(snap);
            ShowError("Không sửa được dầm", ex);
            ResetBeamEdit();
            return;
        }
        if (!ok) { if (Snapshot() != snap) RestoreSnapshot(snap); SetStatus(msg); return; }
        ResetBeamEdit();
        AfterEdit(snap, new EditResult { Ok = true, Message = msg }, select?.Invoke());
    }

    /// <summary>Bấm trên mặt bằng ở các chế độ sửa dầm. true = đã xử lý.</summary>
    private bool BeamEditClick(Point s, string st)
    {
        if (!IsBeamEditMode) return false;
        var (x, y) = S2W(s);
        switch (_mode)
        {
            case PlanEditMode.TrimBeam:
            {
                var b = NearestBeam(s, 10);
                if (b == null || b.IsCurved) { SetStatus("Bấm vào đoạn dầm thẳng cần bỏ."); return true; }
                int id = b.Id;
                RunBeamEdit(() => (BeamEdit.Trim(_p, id, x, y, out string m), m));
                return true;
            }
            case PlanEditMode.ExtendBeam:
            {
                if (BeamEditFrame == null)
                {
                    var b = NearestBeam(s, 12);
                    if (b == null || b.IsCurved) { SetStatus("Bấm gần đầu dầm thẳng cần kéo dài."); return true; }
                    _beFrame = b.Id; _beAtJ = NearJ(b, s);
                    SetStatus($"Đã chọn đầu {(_beAtJ ? "J" : "I")} dầm {b.Label} – bấm dầm đích, hoặc chuột phải / Enter để tự kéo tới dầm vuông góc gần nhất. Esc huỷ.");
                    DrawHover();
                    return true;
                }
                var t = NearestBeam(s, 12);
                if (t == null || t.Id == _beFrame) { SetStatus("Bấm vào dầm đích (khác dầm đang kéo), hoặc chuột phải / Enter để tự tìm."); return true; }
                int fid = _beFrame, tid = t.Id; bool atJ = _beAtJ;
                RunBeamEdit(() => (BeamEdit.Extend(_p, fid, atJ, tid, out string m), m), () => _p.Frames.FirstOrDefault(f => f.Id == fid));
                return true;
            }
            case PlanEditMode.StretchBeam:
            case PlanEditMode.MoveBeam:
            {
                if (BeamEditFrame == null && _beMoveIds.Count == 0)
                {
                    var b = NearestBeam(s, 12);
                    if (b == null || b.IsCurved) { SetStatus("Bấm vào dầm thẳng cần kéo / dời."); return true; }
                    bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                    _beFrame = b.Id; _beAtJ = NearJ(b, s);
                    _beMoveAll = _mode == PlanEditMode.MoveBeam || shift;
                    if (_beMoveAll)
                    {
                        _beMoveIds = _multiSel.Count > 0 && _multiSel.Contains(b.Id) ? _multiSel.ToList() : new List<int> { b.Id };
                        _beBase = SnapPlan(st, x, y);
                        SetStatus($"Dời {_beMoveIds.Count} dầm: điểm gốc ({_beBase.Value.X:0.000}; {_beBase.Value.Y:0.000}) – bấm điểm đích. Esc huỷ.");
                    }
                    else SetStatus($"Kéo đầu {(_beAtJ ? "J" : "I")} dầm {b.Label}: bấm điểm mới (bắt nút, giao trục, nét bản vẽ link). Esc huỷ.");
                    DrawHover();
                    return true;
                }
                var pt = SnapPlan(st, x, y);
                if (_beMoveAll && _beBase != null)
                {
                    var ids = _beMoveIds.ToList();
                    double dx = pt.X - _beBase.Value.X, dy = pt.Y - _beBase.Value.Y;
                    RunBeamEdit(() => (BeamEdit.Move(_p, ids, dx, dy, out string m), m), () => _p.Frames.FirstOrDefault(f => f.Id == ids[0]));
                }
                else
                {
                    int fid = _beFrame; bool atJ = _beAtJ;
                    RunBeamEdit(() => (BeamEdit.Stretch(_p, fid, atJ, pt.X, pt.Y, out string m), m), () => _p.Frames.FirstOrDefault(f => f.Id == fid));
                }
                return true;
            }
            case PlanEditMode.PickCad:
            {
                if (!CadLinkSnap.Active(_p, st).Any())
                {
                    SetStatus("Chưa có bản vẽ AutoCAD liên kết đang hiện trên tầng này – Tệp → Liên kết bản vẽ AutoCAD (DWG/DXF)… để nạp mặt bằng kiến trúc sơ bộ.");
                    return true;
                }
                string sec = cbEditBeamSec.SelectedItem as string;
                if (string.IsNullOrEmpty(sec)) { SetStatus("Chọn tiết diện dầm ở ô 'Tiết diện dầm' trước (dùng cho dầm bao / chiều cao dầm)."); return true; }
                int kind = Math.Max(0, _cbPickKind?.SelectedIndex ?? 0);
                if (kind == 0)
                {
                    double tol = Math.Clamp(12 / Math.Max(_scale, 1e-6), 0.15, 0.6);
                    var segs = CadPick.SegmentsNear(_p, st, x, y, 1.0);
                    var pick = CadPick.BeamFromPair(segs, x, y) ?? CadPick.BeamFromLine(segs, x, y, tol);
                    if (pick == null) { SetStatus("Không thấy nét dầm / tường tại vị trí bấm – bấm giữa hai mép song song (100…600 mm) hoặc sát một nét."); return true; }
                    string snap = Snapshot();
                    EditResult r;
                    try { r = CadPick.CreateBeam(_p, st, pick, sec); }
                    catch (Exception ex) { RestoreSnapshot(snap); ShowError("Không tạo được dầm từ bản vẽ", ex); return true; }
                    if (!r.Ok) { SetStatus(r.Message); return true; }
                    AfterEdit(snap, r, null);
                    return true;
                }
                var loop = CadPick.ClosedLoopAt(CadPick.ClosedPolylines(_p, st), x, y);
                if (loop == null) { SetStatus("Không có đa tuyến kín (polyline đóng) chứa điểm bấm trên bản vẽ liên kết."); return true; }
                var target = kind switch { 1 => CadPick.LoopTarget.Slab, 2 => CadPick.LoopTarget.Stair, 3 => CadPick.LoopTarget.Void, _ => CadPick.LoopTarget.BeamsOnly };
                string snap2 = Snapshot();
                EditResult r2;
                try { r2 = CadPick.CreateFromLoop(_p, st, loop, target, sec); }
                catch (Exception ex) { RestoreSnapshot(snap2); ShowError("Không tạo được đối tượng từ bản vẽ", ex); return true; }
                if (!r2.Ok) { SetStatus(r2.Message); return true; }
                AfterEdit(snap2, r2, null);
                return true;
            }
        }
        return false;
    }

    /// <summary>Chuột phải / Enter khi Extend đã chọn đầu dầm: tự kéo tới dầm vuông góc gần nhất. true = đã xử lý.</summary>
    private bool BeamEditAuto()
    {
        if (_mode != PlanEditMode.ExtendBeam || BeamEditFrame == null) return false;
        int fid = _beFrame; bool atJ = _beAtJ;
        double gap = Math.Max(JoinGap(), 30);
        RunBeamEdit(() =>
        {
            bool ok = BeamEdit.Extend(_p, fid, atJ, null, gap, true, out string m);
            if (!ok) ok = BeamEdit.Extend(_p, fid, atJ, null, gap, false, out m);
            return (ok, m);
        }, () => _p.Frames.FirstOrDefault(f => f.Id == fid));
        return true;
    }

    /// <summary>Esc: huỷ bước đang dở. true = đã xử lý.</summary>
    private bool CancelBeamEdit()
    {
        if (!IsBeamEditMode || (_beFrame == 0 && _beMoveIds.Count == 0)) return false;
        ResetBeamEdit();
        DrawHover();
        SetStatus(BeamEditHint(_mode));
        return true;
    }

    private void JoinAllBeams()
    {
        if (_p == null) return;
        string st = CurrentStory;
        if (st == AllStories || _p.Stories.All(q => q.Name != st)) { SetStatus("Chọn một tầng cụ thể trước khi nối dầm hở."); return; }
        double gap = JoinGap();
        RunBeamEdit(() =>
        {
            int n = BeamEdit.JoinAll(_p, st, gap, out string m);
            return (n > 0, m);
        });
    }

    private void MnuJoinBeams_Click(object sender, RoutedEventArgs e) { ShowPlanTab(); JoinAllBeams(); }

    // ==================================================================
    // Lớp phủ xem trước
    // ==================================================================
    /// <summary>Vẽ lớp phủ các chế độ sửa dầm (gọi trong DrawHover). true = đã vẽ, DrawHover dừng.</summary>
    private bool DrawBeamEditOverlay()
    {
        if (_p == null || !IsBeamEditMode || _panning) return false;
        string st = CurrentStory;
        if (st == AllStories || _p.Stories.All(q => q.Name != st)) return true;
        Point? mouse = _lastMouse;
        (double X, double Y)? mw = mouse.HasValue ? S2W(mouse.Value) : null;
        void Line(Point a, Point b, Brush br, double w, bool dash = false)
        {
            var l = new Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = br, StrokeThickness = w, IsHitTestVisible = false, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            if (dash) l.StrokeDashArray = new DoubleCollection { 4, 3 };
            planOverlay.Children.Add(l);
        }
        try
        {
            var f = BeamEditFrame;
            if (f != null)
            {
                var ni = _p.Node(f.NodeI); var nj = _p.Node(f.NodeJ);
                var a = W2S(ni.X, ni.Y); var b = W2S(nj.X, nj.Y);
                Line(a, b, BeamEditBrush, BeamPlanWidth(f) + 4);
                var end = _beAtJ ? b : a;
                if (!_beMoveAll) planOverlay.Children.Add(MakeCircle(end, 6, BeamEditBrush));
                if (_mode == PlanEditMode.ExtendBeam)
                {
                    var target = mouse.HasValue ? NearestBeam(mouse.Value, 12) : null;
                    if (target != null && target.Id == f.Id) target = null;
                    var hit = BeamEdit.FindExtendHit(_p, f, _beAtJ, target?.Id, target != null ? 60 : Math.Max(JoinGap(), 30), target == null);
                    if (target != null) DrawHighlight(planOverlay, target, true);
                    if (hit != null) { var h = W2S(hit.Value.X, hit.Value.Y); Line(end, h, RubberBeam, 2, true); planOverlay.Children.Add(MakeCircle(h, 5, RubberBeam)); }
                }
                else if (mw.HasValue && (_mode == PlanEditMode.StretchBeam || _mode == PlanEditMode.MoveBeam))
                {
                    var pt = SnapPlan(st, mw.Value.X, mw.Value.Y);
                    DrawSnapMarker(st, pt, RubberBeam, false);
                    if (_beMoveAll && _beBase != null)
                    {
                        double dx = pt.X - _beBase.Value.X, dy = pt.Y - _beBase.Value.Y;
                        foreach (var g in _p.Frames.Where(q => _beMoveIds.Contains(q.Id) && !q.IsVerticalKind))
                        {
                            var gi = _p.Node(g.NodeI); var gj = _p.Node(g.NodeJ);
                            Line(W2S(gi.X + dx, gi.Y + dy), W2S(gj.X + dx, gj.Y + dy), RubberBeam, 2, true);
                        }
                    }
                    else
                    {
                        var fixedEnd = _beAtJ ? a : b;
                        Line(fixedEnd, W2S(pt.X, pt.Y), RubberBeam, 2, true);
                    }
                }
                return true;
            }
            if (!mouse.HasValue) return true;
            if (_mode == PlanEditMode.PickCad)
            {
                int kind = Math.Max(0, _cbPickKind?.SelectedIndex ?? 0);
                if (kind == 0)
                {
                    double tol = Math.Clamp(12 / Math.Max(_scale, 1e-6), 0.15, 0.6);
                    var segs = CadPick.SegmentsNear(_p, st, mw.Value.X, mw.Value.Y, 1.0);
                    var pick = CadPick.BeamFromPair(segs, mw.Value.X, mw.Value.Y) ?? CadPick.BeamFromLine(segs, mw.Value.X, mw.Value.Y, tol);
                    if (pick != null) Line(W2S(pick.X1, pick.Y1), W2S(pick.X2, pick.Y2), RubberBeam, Math.Max(3, pick.Width * _scale), false);
                }
                else
                {
                    var loop = CadPick.ClosedLoopAt(CadPick.ClosedPolylines(_p, st), mw.Value.X, mw.Value.Y);
                    if (loop != null)
                        planOverlay.Children.Add(new Shapes.Polygon
                        {
                            Points = new PointCollection(loop.Select(q => W2S(q.X, q.Y))), Stroke = RubberBeam, StrokeThickness = 2,
                            Fill = new SolidColorBrush(Color.FromArgb(40, 230, 120, 0)), IsHitTestVisible = false
                        });
                }
                return true;
            }
            var hb = NearestBeam(mouse.Value, 12);
            if (hb != null && !hb.IsCurved)
            {
                if (_mode == PlanEditMode.TrimBeam)
                {
                    var ni = _p.Node(hb.NodeI); var nj = _p.Node(hb.NodeJ);
                    Line(W2S(ni.X, ni.Y), W2S(nj.X, nj.Y), TrimBrush, BeamPlanWidth(hb) + 3, true);
                }
                else
                {
                    DrawHighlight(planOverlay, hb, true);
                    var ni = _p.Node(hb.NodeI); var nj = _p.Node(hb.NodeJ);
                    bool j = NearJ(hb, mouse.Value);
                    if (_mode != PlanEditMode.MoveBeam) planOverlay.Children.Add(MakeCircle(j ? W2S(nj.X, nj.Y) : W2S(ni.X, ni.Y), 6, BeamEditBrush));
                }
            }
        }
        catch (Exception) { /* xem trước lỗi (mô hình đang sửa dở) – bỏ qua */ }
        return true;
    }
}

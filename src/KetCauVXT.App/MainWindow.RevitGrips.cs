using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Model;
using KetCauVXT.Ui;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// [KIỂU REVIT – HOÀN THIỆN] trên mặt bằng chế độ Thiết kế:
//   • TAY NẮM (grip): tường / dầm đang chọn hiện ô vuông xanh ở 2 đầu và ô giữa – kéo đầu để kéo dài / rút ngắn
//     (bắt điểm trục, nút, nét CAD như khi vẽ), kéo ô giữa để dời; cửa gắn tường giữ vị trí và chặn trong tường; mỗi lần kéo = 1 bước hoàn tác;
//   • TAB: chuyển vòng giữa các đối tượng chồng nhau dưới con trỏ (cột, dầm, tường, lỗ sàn, ô sàn) – như Revit;
//   • LỆNH SỬA BẰNG CHUỘT (dòng lệnh): CO / M (điểm gốc → điểm đích, dây chun xem trước, gõ chiều dài / @dx,dy),
//     MI (2 điểm trục đối xứng bất kỳ), AR (số phần tử → điểm gốc → điểm thứ hai), O (khoảng cách → bấm phía) – áp cho dầm đang chọn và tường đang chọn.
// Lõi: Model/RevitFinish.cs (WallGrips, PlanEditOps, HitCycle).
// =====================================================================
public partial class MainWindow
{
    private enum GripKind { None, WallEnd1, WallEnd2, WallMove, BeamI, BeamJ, BeamMove }

    private GripKind _grip = GripKind.None;
    private bool _gripDrag;
    private (double X, double Y) _gripFrom, _gripTo;
    private const double GripPx = 7;

    // Tab chuyển vòng
    private List<object> _tabCands = new();
    private int _tabIdx = -1;
    private Point _tabAt;

    // lệnh sửa bằng chuột
    private CadCmd _editCmd = CadCmd.None;
    private (double X, double Y) _editBase, _editCursor;
    private bool _editHasBase;
    private int _arrayN = 2;
    private double _offsetDist;

    private static readonly Brush GripFill = Frozen(Color.FromRgb(0, 120, 215));
    private static readonly Brush GhostBrush = Frozen(Color.FromRgb(0, 150, 220));

    // ==================================================================
    // Tay nắm
    // ==================================================================
    private bool GripsEnabled => _p != null && _mode == PlanEditMode.Select && _cmdStage == CmdStage.None && !_busy && !_selectOnly;   // [Chỉ chọn] khoá kéo dời

    private FrameElement SelectedBeamForGrip()
    {
        if (_sel == null) return null;
        var f = _p.Frames.FirstOrDefault(q => q.Id == _sel.Id);
        return f != null && !f.IsVerticalKind && !f.IsCurved ? f : null;
    }

    /// <summary>Các tay nắm đang hiện (toạ độ màn hình).</summary>
    private List<(GripKind Kind, Point S)> Grips()
    {
        var res = new List<(GripKind, Point)>();
        if (!GripsEnabled) return res;
        if (_selWall != null && _p.Walls.Contains(_selWall) && !_selWall.IsCurved && (CurrentStory == AllStories || _selWall.Story == CurrentStory))
        {
            var w = _selWall;
            res.Add((GripKind.WallEnd1, W2S(w.X1, w.Y1)));
            res.Add((GripKind.WallEnd2, W2S(w.X2, w.Y2)));
            res.Add((GripKind.WallMove, W2S((w.X1 + w.X2) / 2, (w.Y1 + w.Y2) / 2)));
            return res;
        }
        var f = SelectedBeamForGrip();
        if (f != null)
        {
            var a = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeI); var b = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
            if (a == null || b == null) return res;
            res.Add((GripKind.BeamI, W2S(a.X, a.Y)));
            res.Add((GripKind.BeamJ, W2S(b.X, b.Y)));
            res.Add((GripKind.BeamMove, W2S((a.X + b.X) / 2, (a.Y + b.Y) / 2)));
        }
        return res;
    }

    /// <summary>Vẽ tay nắm (gọi cuối RedrawPlan).</summary>
    private void DrawGrips()
    {
        foreach (var (k, s) in Grips())
        {
            double sz = k is GripKind.WallMove or GripKind.BeamMove ? 9 : 8;
            var r = new Shapes.Rectangle
            {
                Width = sz, Height = sz, Fill = k is GripKind.WallMove or GripKind.BeamMove ? Brushes.White : GripFill, Stroke = GripFill, StrokeThickness = 1.5, IsHitTestVisible = false,
                RenderTransform = k is GripKind.WallMove or GripKind.BeamMove ? new RotateTransform(45, sz / 2, sz / 2) : null
            };
            Canvas.SetLeft(r, s.X - sz / 2); Canvas.SetTop(r, s.Y - sz / 2);
            planCanvas.Children.Add(r);
        }
    }

    /// <summary>Gọi đầu PlanHost_MouseLeftButtonDown: bấm trúng tay nắm → bắt đầu kéo (không di chuyển khung nhìn).</summary>
    private bool GripMouseDown(Point s)
    {
        if (!GripsEnabled) return false;
        foreach (var (k, g) in Grips())
        {
            if (Math.Abs(g.X - s.X) > GripPx || Math.Abs(g.Y - s.Y) > GripPx) continue;
            _grip = k; _gripDrag = true;
            var (x, y) = S2W(g);
            _gripFrom = (x, y); _gripTo = _gripFrom;
            planHost.Focus();
            planHost.CaptureMouse();
            SetStatus(k is GripKind.WallMove or GripKind.BeamMove ? "Kéo để dời – thả chuột tại vị trí mới (bắt điểm trục, nút, nét CAD; F8 trực giao)."
                                                                  : "Kéo đầu tới vị trí mới – thả chuột để kết thúc (bắt điểm trục, nút, nét CAD; F8 trực giao).");
            return true;
        }
        return false;
    }

    private (double X, double Y) GripFixedEnd()
    {
        if (_grip == GripKind.WallEnd1 && _selWall != null) return (_selWall.X2, _selWall.Y2);
        if (_grip == GripKind.WallEnd2 && _selWall != null) return (_selWall.X1, _selWall.Y1);
        var f = SelectedBeamForGrip();
        if (f != null)
        {
            var n = _p.Nodes.FirstOrDefault(q => q.Id == (_grip == GripKind.BeamI ? f.NodeJ : f.NodeI));
            if (n != null) return (n.X, n.Y);
        }
        return _gripFrom;
    }

    private bool GripMouseMove(Point s)
    {
        if (!_gripDrag) return false;
        string st = _selWall?.Story ?? (SelectedBeamForGrip() is FrameElement bf ? BeamEdit.StoryOf(_p, bf) : CurrentStory);
        var (x, y) = S2W(s);
        var pt = st != null && st != AllStories ? SnapPlan(st, x, y) : (x, y);
        var anchor = _grip is GripKind.WallMove or GripKind.BeamMove ? _gripFrom : GripFixedEnd();
        if (_orthoOn || _polarOn) pt = CadCommand.Constrain(anchor, pt, _orthoOn, _polarOn ? 45 : 0);
        _gripTo = pt;
        DrawHover();
        return true;
    }

    private bool GripMouseUp(Point s)
    {
        if (!_gripDrag) return false;
        _gripDrag = false;
        planHost.ReleaseMouseCapture();
        _panning = false;
        var kind = _grip; _grip = GripKind.None;
        double dx = _gripTo.X - _gripFrom.X, dy = _gripTo.Y - _gripFrom.Y;
        if (Math.Abs(dx) + Math.Abs(dy) < 1e-4) { DrawHover(); SetStatus("Tay nắm: chưa kéo."); return true; }
        var (tx, ty) = _gripTo;
        switch (kind)
        {
            case GripKind.WallEnd1:
            case GripKind.WallEnd2:
            {
                var w = _selWall;
                RunEditOp(() => WallGrips.MoveEnd(_p, w, kind == GripKind.WallEnd2, tx, ty), w);
                break;
            }
            case GripKind.WallMove:
            {
                var w = _selWall;
                RunEditOp(() => WallGrips.Move(w, dx, dy), w);
                break;
            }
            case GripKind.BeamI:
            case GripKind.BeamJ:
            {
                var f = SelectedBeamForGrip();
                if (f == null) break;
                RunEditOp(() => BeamEdit.Stretch(_p, f.Id, kind == GripKind.BeamJ, tx, ty, out string m) ? new EditResult { Ok = true, Message = m } : EditResult.Fail(m), f);
                break;
            }
            case GripKind.BeamMove:
            {
                var f = SelectedBeamForGrip();
                if (f == null) break;
                RunEditOp(() => BeamEdit.Move(_p, new[] { f.Id }, dx, dy, out string m) ? new EditResult { Ok = true, Message = m } : EditResult.Fail(m), f);
                break;
            }
        }
        return true;
    }

    /// <summary>Thực hiện một thao tác sửa = 1 bước hoàn tác, giữ đối tượng đang chọn (như Revit).</summary>
    private void RunEditOp(Func<EditResult> op, object keepSelected)
    {
        if (_p == null) return;
        if (_busy) { SetStatus("Chương trình đang tính toán – chờ xong rồi sửa mô hình."); return; }
        string snap = Snapshot();
        EditResult r;
        try { r = op(); }
        catch (Exception ex) { RestoreSnapshot(snap); ShowError("Không thực hiện được", ex); return; }
        if (!r.Ok)
        {
            if (Snapshot() != snap) RestoreSnapshot(snap);
            SetStatus(r.Message);
            DrawHover();
            return;
        }
        object sel = keepSelected switch
        {
            WallLine w when _p.Walls.Contains(w) => w,
            FrameElement f => _p.Frames.FirstOrDefault(q => q.Id == f.Id),
            _ => null
        };
        AfterEdit(snap, r, sel);
    }

    // ==================================================================
    // Tab: chuyển vòng đối tượng chồng nhau
    // ==================================================================
    private List<object> HitCandidates(Point s)
    {
        var res = new List<object>();
        if (_p == null) return res;
        void Add(object o) { if (o != null && !res.Contains(o)) res.Add(o); }
        Add(ColumnAt(s));
        Add(NearestBeam(s, 8));
        Add(WallAtScreen(s));
        var (x, y) = S2W(s);
        Add(OpeningAt(x, y));
        Add(SlabAtPlan(x, y));
        return res;
    }

    private static string HitName(object o) => o switch
    {
        FrameElement f => (f.IsVerticalKind ? "Cột " : "Dầm ") + f.Label,
        WallLine w => $"Tường #{w.Id} ({w.Type})",
        FloorOpening op => op.Name,
        Slab sl => "Ô sàn " + sl.Label,
        _ => "?"
    };

    /// <summary>Phím Tab khi trỏ mặt bằng (chế độ Chọn): chọn đối tượng kế tiếp trong các đối tượng chồng nhau dưới con trỏ.</summary>
    private bool TabCycle()
    {
        if (_p == null || _mode != PlanEditMode.Select || _lastMouse == null || !PlanTabActive) return false;
        var s = _lastMouse.Value;
        var cands = HitCandidates(s);
        if (cands.Count == 0) return false;
        bool same = Math.Abs(s.X - _tabAt.X) + Math.Abs(s.Y - _tabAt.Y) < 4
                    && HitCycle.Same(cands.Select(HitName).ToList(), _tabCands.Select(HitName).ToList());
        _tabCands = cands; _tabAt = s;
        _tabIdx = HitCycle.Next(cands.Count, same ? _tabIdx : 0);
        switch (cands[_tabIdx])
        {
            case WallLine w: Select(wall: w); break;
            case FrameElement f: Select(frame: f); break;
            case FloorOpening o: Select(opening: o); break;
            case Slab sl: Select(slab: sl); break;
        }
        RedrawPlan();
        SetStatus($"Tab: {HitName(cands[_tabIdx])} ({_tabIdx + 1}/{cands.Count}) – Tab tiếp để chọn đối tượng chồng bên dưới.");
        return true;
    }

    // ==================================================================
    // Lệnh sửa bằng chuột: CO / M / MI / AR / O
    // ==================================================================
    private bool HasEditSelection() => SelectedBeamIds().Count > 0 || (_selWall != null && _p.Walls.Contains(_selWall));

    /// <summary>Bắt đầu lệnh sửa bằng chuột (gọi từ RunCmd). false = không có đối tượng chọn.</summary>
    private bool StartMouseEdit(CadCmd cmd)
    {
        if (!HasEditSelection())
        {
            SetStatus("Chọn dầm (bấm / Chọn nhiều) hoặc tường trước, rồi gõ lại lệnh.");
            return false;
        }
        _editCmd = cmd; _editHasBase = false;
        switch (cmd)
        {
            case CadCmd.Copy: StartStage(CmdStage.EditBase, "SAO CHÉP – bấm điểm gốc (hoặc gõ dx,dy [số bản]):"); break;
            case CadCmd.Move: StartStage(CmdStage.EditBase, "DỜI – bấm điểm gốc (hoặc gõ dx,dy):"); break;
            case CadCmd.Mirror: StartStage(CmdStage.MirrorP1, "ĐỐI XỨNG – bấm điểm thứ nhất của trục đối xứng (hoặc gõ tên trục, vd 2 / B):"); break;
            case CadCmd.Array: StartStage(CmdStage.ArrayCount, "MẢNG – gõ số phần tử (tính cả bản gốc), vd 4 (hoặc: số cột số hàng dx dy):"); break;
            case CadCmd.Offset: StartStage(CmdStage.OffsetDist, "OFFSET – gõ khoảng cách (vd 1200), rồi bấm phía cần offset:"); break;
        }
        if (_cmdStage is CmdStage.EditBase or CmdStage.MirrorP1 && planHost != null) planHost.Cursor = Cursors.Cross;
        return true;
    }

    private bool IsMouseEditStage => _cmdStage is CmdStage.EditBase or CmdStage.EditTarget or CmdStage.MirrorP1 or CmdStage.MirrorP2 or CmdStage.ArrayCount or CmdStage.OffsetPick;

    /// <summary>Bấm điểm trong lệnh sửa (gọi từ CmdPlanClick).</summary>
    private bool MouseEditClick((double X, double Y) pt)
    {
        switch (_cmdStage)
        {
            case CmdStage.EditBase:
                _editBase = pt; _editHasBase = true; _editCursor = pt;
                StartStage(CmdStage.EditTarget, _editCmd == CadCmd.Array
                    ? $"MẢNG {_arrayN} – bấm điểm thứ hai (khoảng cách giữa 2 phần tử liền nhau) hoặc gõ chiều dài / @dx,dy:"
                    : "Bấm điểm đích (hoặc gõ chiều dài theo hướng chuột / @dx,dy); Esc huỷ:");
                return true;
            case CmdStage.EditTarget:
                ApplyMouseEdit(pt);
                return true;
            case CmdStage.MirrorP1:
                _editBase = pt; _editHasBase = true; _editCursor = pt;
                StartStage(CmdStage.MirrorP2, "ĐỐI XỨNG – bấm điểm thứ hai của trục (F8 trực giao):");
                return true;
            case CmdStage.MirrorP2:
                ApplyMirror(pt);
                return true;
            case CmdStage.OffsetPick:
                ApplyOffset(pt);
                return true;
        }
        return false;
    }

    /// <summary>Điểm dây chun đã ràng buộc trực giao / góc cực theo điểm gốc.</summary>
    private (double X, double Y) EditConstrain((double X, double Y) pt) =>
        _editHasBase && (_orthoOn || _polarOn) ? CadCommand.Constrain(_editBase, pt, _orthoOn, _polarOn ? 45 : 0) : pt;

    /// <summary>Chữ gõ trong lệnh sửa (gọi từ ExecuteCommandLine). true = đã xử lý.</summary>
    private bool MouseEditText(string raw)
    {
        switch (_cmdStage)
        {
            case CmdStage.EditBase when raw.Contains(','):
            {
                // biến thể gõ số cũ: dx,dy [n]
                var parts = raw.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var v = Lengths(parts[0], 2);
                int n = parts.Length > 1 && int.TryParse(parts[1], out int k) ? k : 1;
                if (v == null || v.Count != 2) { SetStatus("Gõ dx,dy [số bản], vd 3600,0 2"); return true; }
                ApplyVector(v[0], v[1], _editCmd == CadCmd.Move ? 0 : Math.Max(1, n));
                return true;
            }
            case CmdStage.EditTarget:
            {
                (double X, double Y)? dir = _editCursor;
                var inp = CadCommand.Parse(raw, _editBase, _coordUnit, dir);
                if (inp.Kind == CadInputKind.Error) { SetStatus(inp.Error); return true; }
                if (inp.Kind is CadInputKind.Point or CadInputKind.Number) { ApplyMouseEdit((inp.X, inp.Y)); return true; }
                return false;
            }
            case CmdStage.MirrorP1:
            {
                var g = BeamTools.FindGrid(_p, raw.Trim());
                if (g == null) { SetStatus($"Không có trục \"{raw.Trim()}\" – hoặc bấm 2 điểm trên mặt bằng."); return true; }
                if (g.Dir == GridDir.X) ApplyMirrorLine((g.Coord, 0), (g.Coord, 1));
                else ApplyMirrorLine((0, g.Coord), (1, g.Coord));
                return true;
            }
            case CmdStage.MirrorP2:
            {
                var inp = CadCommand.Parse(raw, _editBase, _coordUnit, _editCursor);
                if (inp.Kind is CadInputKind.Point or CadInputKind.Number) { ApplyMirror((inp.X, inp.Y)); return true; }
                return false;
            }
            case CmdStage.ArrayCount:
            {
                var parts = raw.Split(new[] { ' ', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    // biến thể cũ: số cột số hàng dx dy (chỉ dầm)
                    if (!int.TryParse(parts[0], out int nx) || !int.TryParse(parts[1], out int ny)
                        || !BeamTools.TryLength(parts[2], _coordUnit, out double adx) || !BeamTools.TryLength(parts[3], _coordUnit, out double ady))
                    { SetStatus("Gõ: số cột  số hàng  dx  dy (vd 3 2 3600 4500)"); return true; }
                    var ids = SelectedBeamIds();
                    CancelStage(false);
                    RunBeamOp(() => PlanDrawOps.ArrayBeams(_p, ids, nx, ny, adx, ady));
                    return true;
                }
                if (!int.TryParse(parts.FirstOrDefault() ?? "", out int cnt) || cnt < 2 || cnt > 100) { SetStatus("Số phần tử của mảng từ 2 đến 100."); return true; }
                _arrayN = cnt;
                StartStage(CmdStage.EditBase, $"MẢNG {cnt} – bấm điểm gốc:");
                if (planHost != null) planHost.Cursor = Cursors.Cross;
                planHost?.Focus();
                return true;
            }
            case CmdStage.OffsetDist:
            {
                if (!BeamTools.TryLength(raw.Trim(), _coordUnit, out double d) || Math.Abs(d) < 1e-4) { SetStatus("Gõ khoảng offset, vd 1200"); return true; }
                _offsetDist = d;
                StartStage(CmdStage.OffsetPick, $"OFFSET {Math.Abs(d) * 1000:0} mm – bấm phía cần offset (Enter: theo dấu, dương = bên trái hướng I→J):");
                if (planHost != null) planHost.Cursor = Cursors.Cross;
                planHost?.Focus();
                return true;
            }
            case CmdStage.OffsetPick when raw.Length == 0:
            {
                var ids = SelectedBeamIds();
                double d = _offsetDist;
                CancelStage(false);
                RunBeamOp(() => PlanDrawOps.OffsetBeams(_p, ids, d));
                return true;
            }
        }
        return false;
    }

    private void ApplyMouseEdit((double X, double Y) target)
    {
        var t = EditConstrain(target);
        double dx = t.X - _editBase.X, dy = t.Y - _editBase.Y;
        if (Math.Abs(dx) + Math.Abs(dy) < 1e-4) { SetStatus("Điểm đích trùng điểm gốc."); return; }
        if (_editCmd == CadCmd.Array)
        {
            var (adx, ady, copies) = PlanEditOps.LinearArray(dx, dy, _arrayN);
            ApplyVector(adx, ady, copies);
            return;
        }
        ApplyVector(dx, dy, _editCmd == CadCmd.Move ? 0 : 1);
        if (_editCmd == CadCmd.Copy && HasEditSelection())
        {
            // sao chép nhiều lần như AutoCAD: giữ điểm gốc, bấm tiếp điểm đích
            _editCmd = CadCmd.Copy; _editHasBase = true;
            StartStage(CmdStage.EditTarget, "SAO CHÉP – bấm điểm đích tiếp theo; Esc kết thúc:");
        }
    }

    /// <summary>copies = 0 → dời; ≥ 1 → sao chép k·(dx, dy), k = 1…copies. Dầm + tường đang chọn, 1 bước hoàn tác.</summary>
    private void ApplyVector(double dx, double dy, int copies)
    {
        var beams = SelectedBeamIds();
        var wall = _selWall != null && _p.Walls.Contains(_selWall) ? _selWall : null;
        var cmd = _editCmd;
        CancelStage(false);
        _editCmd = cmd;
        RunBeamOp(() =>
        {
            var msgs = new List<string>();
            var r = new EditResult();
            if (beams.Count > 0)
            {
                EditResult e;
                if (copies == 0) e = BeamEdit.Move(_p, beams, dx, dy, out string m) ? new EditResult { Ok = true, Message = m } : EditResult.Fail(m);
                else e = BeamTools.Replicate(_p, beams, dx, dy, copies);
                if (e.Ok) { r.Ok = true; r.NewFrames.AddRange(e.NewFrames); }
                msgs.Add(e.Message);
            }
            if (wall != null)
            {
                var e = copies == 0 ? PlanEditOps.MoveWalls(_p, new[] { wall.Id }, dx, dy) : PlanEditOps.CopyWalls(_p, new[] { wall.Id }, dx, dy, copies);
                if (e.Ok) r.Ok = true;
                msgs.Add(e.Message);
            }
            r.Message = string.Join(" ", msgs.Where(m => !string.IsNullOrEmpty(m)));
            return r;
        }, copies > 0);
    }

    private void ApplyMirror((double X, double Y) p2)
    {
        var b = EditConstrain(p2);
        if (Math.Abs(b.X - _editBase.X) + Math.Abs(b.Y - _editBase.Y) < 1e-4) { SetStatus("Hai điểm trục đối xứng trùng nhau."); return; }
        ApplyMirrorLine(_editBase, b);
    }

    private void ApplyMirrorLine((double X, double Y) a, (double X, double Y) b)
    {
        var beams = SelectedBeamIds();
        var wall = _selWall != null && _p.Walls.Contains(_selWall) ? _selWall : null;
        CancelStage(false);
        RunBeamOp(() =>
        {
            var msgs = new List<string>();
            var r = new EditResult();
            if (beams.Count > 0)
            {
                var e = PlanEditOps.MirrorBeams(_p, beams, a.X, a.Y, b.X, b.Y);
                if (e.Ok) { r.Ok = true; r.NewFrames.AddRange(e.NewFrames); }
                msgs.Add(e.Message);
            }
            if (wall != null)
            {
                var e = PlanEditOps.MirrorWalls(_p, new[] { wall.Id }, a.X, a.Y, b.X, b.Y);
                if (e.Ok) r.Ok = true;
                msgs.Add(e.Message);
            }
            r.Message = string.Join(" ", msgs.Where(m => !string.IsNullOrEmpty(m)));
            return r;
        });
    }

    private void ApplyOffset((double X, double Y) pick)
    {
        var beams = SelectedBeamIds();
        var wall = _selWall != null && _p.Walls.Contains(_selWall) ? _selWall : null;
        double d = Math.Abs(_offsetDist);
        CancelStage(false);
        RunBeamOp(() =>
        {
            var msgs = new List<string>();
            var r = new EditResult();
            foreach (int id in beams)
            {
                var f = _p.Frames.FirstOrDefault(q => q.Id == id);
                var a = f == null ? null : _p.Nodes.FirstOrDefault(n => n.Id == f.NodeI);
                var c = f == null ? null : _p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
                if (a == null || c == null) continue;
                int side = PlanEditOps.Side(a.X, a.Y, c.X, c.Y, pick.X, pick.Y);
                if (side == 0) continue;
                var e = PlanDrawOps.OffsetBeams(_p, new[] { id }, d * side);
                if (e.Ok) { r.Ok = true; r.NewFrames.AddRange(e.NewFrames); } else msgs.Add(e.Message);
            }
            if (r.NewFrames.Count > 0) msgs.Insert(0, $"Đã offset {r.NewFrames.Count} dầm cách {d * 1000:0} mm.");
            if (wall != null)
            {
                var e = PlanEditOps.OffsetWall(_p, wall, d, pick.X, pick.Y);
                if (e.Ok) r.Ok = true;
                msgs.Add(e.Message);
            }
            r.Message = msgs.Count > 0 ? string.Join(" ", msgs) : "Không offset được – bấm lệch khỏi tim đối tượng.";
            return r;
        });
    }

    /// <summary>Di chuột trong lệnh sửa: cập nhật điểm con trỏ (bắt điểm) rồi vẽ lại lớp phủ.</summary>
    private bool MouseEditMove(Point s)
    {
        if (!IsMouseEditStage || _p == null) return false;
        var (x, y) = S2W(s);
        string st = CurrentStory;
        _editCursor = st != AllStories && _p.Stories.Any(q => q.Name == st) ? SnapPlan(st, x, y) : (x, y);
        DrawHover();
        return true;
    }

    // ==================================================================
    // Lớp phủ: dây chun kéo tay nắm, bóng đối tượng khi dời / sao chép / đối xứng
    // ==================================================================
    private List<((double X, double Y) A, (double X, double Y) B, bool Wall)> EditSourceLines()
    {
        var res = new List<((double, double), (double, double), bool)>();
        foreach (int id in SelectedBeamIds())
        {
            var f = _p.Frames.FirstOrDefault(q => q.Id == id);
            var a = f == null ? null : _p.Nodes.FirstOrDefault(n => n.Id == f.NodeI);
            var b = f == null ? null : _p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
            if (a != null && b != null) res.Add(((a.X, a.Y), (b.X, b.Y), false));
        }
        if (_selWall != null && _p.Walls.Contains(_selWall)) res.Add(((_selWall.X1, _selWall.Y1), (_selWall.X2, _selWall.Y2), true));
        return res;
    }

    private void Ghost((double X, double Y) a, (double X, double Y) b, bool wall)
    {
        var sa = W2S(a.X, a.Y); var sb = W2S(b.X, b.Y);
        planOverlay.Children.Add(new Shapes.Line
        {
            X1 = sa.X, Y1 = sa.Y, X2 = sb.X, Y2 = sb.Y, Stroke = GhostBrush, StrokeThickness = wall ? 5 : 3, Opacity = 0.75,
            StrokeDashArray = new DoubleCollection { 3, 2 }, IsHitTestVisible = false
        });
    }

    private void RubberLine((double X, double Y) a, (double X, double Y) b)
    {
        var sa = W2S(a.X, a.Y); var sb = W2S(b.X, b.Y);
        planOverlay.Children.Add(new Shapes.Line { X1 = sa.X, Y1 = sa.Y, X2 = sb.X, Y2 = sb.Y, Stroke = Brushes.DimGray, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 6, 3 }, IsHitTestVisible = false });
        double dx = b.X - a.X, dy = b.Y - a.Y, L = Math.Sqrt(dx * dx + dy * dy);
        if (L < 1e-6) return;
        var t = new TextBlock
        {
            Text = $"{L * 1000:0}  ∠{Math.Atan2(dy, dx) * 180 / Math.PI:0.#}°", Foreground = GhostBrush, FontSize = 11, IsHitTestVisible = false,
            Background = new SolidColorBrush(Color.FromArgb(210, 255, 255, 255))
        };
        Canvas.SetLeft(t, (sa.X + sb.X) / 2 + 6); Canvas.SetTop(t, (sa.Y + sb.Y) / 2 - 18);
        planOverlay.Children.Add(t);
    }

    /// <summary>Gọi trong DrawHover (sau khi xoá lớp phủ). true = đã vẽ lớp phủ của tay nắm / lệnh sửa.</summary>
    private bool DrawGripOrEditOverlay()
    {
        if (_p == null) return false;
        if (_gripDrag)
        {
            if (_grip is GripKind.WallMove or GripKind.BeamMove)
            {
                double dx = _gripTo.X - _gripFrom.X, dy = _gripTo.Y - _gripFrom.Y;
                foreach (var (a, b, w) in EditSourceLines()) Ghost((a.X + dx, a.Y + dy), (b.X + dx, b.Y + dy), w);
                RubberLine(_gripFrom, _gripTo);
            }
            else
            {
                var fixedEnd = GripFixedEnd();
                Ghost(fixedEnd, _gripTo, _grip is GripKind.WallEnd1 or GripKind.WallEnd2);
                RubberLine(fixedEnd, _gripTo);
            }
            return true;
        }
        if (!IsMouseEditStage) return false;
        var cur = EditConstrain(_editCursor);
        switch (_cmdStage)
        {
            case CmdStage.EditTarget:
            {
                double dx = cur.X - _editBase.X, dy = cur.Y - _editBase.Y;
                int n = _editCmd == CadCmd.Array ? Math.Max(1, _arrayN - 1) : 1;
                for (int k = 1; k <= n; k++)
                    foreach (var (a, b, w) in EditSourceLines()) Ghost((a.X + k * dx, a.Y + k * dy), (b.X + k * dx, b.Y + k * dy), w);
                RubberLine(_editBase, cur);
                break;
            }
            case CmdStage.MirrorP2:
            {
                if (Math.Abs(cur.X - _editBase.X) + Math.Abs(cur.Y - _editBase.Y) > 1e-4)
                    foreach (var (a, b, w) in EditSourceLines())
                        Ghost(PlanEditOps.MirrorPoint(a.X, a.Y, _editBase.X, _editBase.Y, cur.X, cur.Y), PlanEditOps.MirrorPoint(b.X, b.Y, _editBase.X, _editBase.Y, cur.X, cur.Y), w);
                var sa = W2S(_editBase.X, _editBase.Y); var sb = W2S(cur.X, cur.Y);
                planOverlay.Children.Add(new Shapes.Line { X1 = sa.X, Y1 = sa.Y, X2 = sb.X, Y2 = sb.Y, Stroke = Brushes.IndianRed, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 8, 3, 2, 3 }, IsHitTestVisible = false });
                break;
            }
            case CmdStage.OffsetPick:
            {
                double d = Math.Abs(_offsetDist);
                foreach (var (a, b, w) in EditSourceLines())
                {
                    int side = PlanEditOps.Side(a.X, a.Y, b.X, b.Y, cur.X, cur.Y);
                    if (side == 0) continue;
                    double dx = b.X - a.X, dy = b.Y - a.Y, L = Math.Sqrt(dx * dx + dy * dy);
                    if (L < 1e-9) continue;
                    double nx = -dy / L * d * side, ny = dx / L * d * side;
                    Ghost((a.X + nx, a.Y + ny), (b.X + nx, b.Y + ny), w);
                }
                break;
            }
        }
        // dấu thập tại điểm bắt
        var c = W2S(cur.X, cur.Y);
        var mk = new Shapes.Rectangle { Width = 9, Height = 9, Stroke = GhostBrush, StrokeThickness = 1.2, IsHitTestVisible = false };
        Canvas.SetLeft(mk, c.X - 4.5); Canvas.SetTop(mk, c.Y - 4.5);
        planOverlay.Children.Add(mk);
        return true;
    }
}

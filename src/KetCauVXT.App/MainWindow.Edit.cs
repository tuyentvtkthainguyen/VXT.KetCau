using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// SỬA MÔ HÌNH TRÊN MẶT BẰNG: thêm ô sàn, ban công, cầu thang, giếng trời, sàn vệ sinh, dầm, tường, gán tường lên dầm,
// gán cấu tạo sàn, xoá (ô sàn, lỗ sàn, tường, dầm, cột); hoàn tác (Ctrl+Z).
// Thanh công cụ nằm trong tab "Mặt bằng" (MainWindow.xaml). Thao tác mô hình ở KetCauVXT.Model.ModelEdit.
// =====================================================================
public partial class MainWindow
{
    private enum PlanEditMode { Select, AddSlab, Balcony, Stair, Void, WC, Delete, AddBeam, AddWall, AssignWall, AssignBuildUp, AddCurvedBeam,
        /* [BeamTools] MainWindow.BeamTools.cs */ QuickGridBeam, SecondaryBeam, CantileverBeam, MultiSelect,
        /* [Sàn hạ cốt] MainWindow.SlabDrop.cs */ SlabDropMode,
        /* [Sửa dầm] MainWindow.BeamEdit.cs */ StretchBeam, MoveBeam, TrimBeam, ExtendBeam, PickCad }

    private PlanEditMode _mode = PlanEditMode.Select;

    // vẽ dầm / tường bằng 2 điểm: điểm đầu đã bắt (m) trên tầng _lineStory; vị trí chuột cuối cùng trên mặt bằng
    private (double X, double Y)? _lineStart;
    private string _lineStory;
    private Point? _lastMouse;
    private string _editComboSig = "";

    private bool IsLineMode => _mode == PlanEditMode.AddBeam || _mode == PlanEditMode.AddWall || _mode == PlanEditMode.AddCurvedBeam;

    // hoàn tác: ảnh chụp JSON của dự án TRƯỚC mỗi thao tác sửa (tối đa 30); chỉ dùng cho đúng đối tượng Project đã chụp
    private const int MaxUndo = 30;
    private readonly List<string> _undo = new();
    private readonly List<string> _redo = new();   // [Kiểu Revit] làm lại (Ctrl+Y) – Model/RevitFinish.cs SnapshotHistory
    private Project _undoOwner;

    private static string ModeHint(PlanEditMode m) => m switch
    {
        PlanEditMode.AddSlab => "Thêm ô sàn: bấm vào ô kín bởi dầm để thêm sàn (công năng theo ô 'Công năng').",
        PlanEditMode.Balcony => "Thêm ban công: bấm vào dầm biên ngoài nhà để thêm ban công công xôn (độ vươn, tải lan can theo ô nhập).",
        PlanEditMode.Stair => "Thêm cầu thang: bấm vào ô sàn / ô kín bởi dầm để biến thành ô cầu thang (tải truyền vào dầm viền).",
        PlanEditMode.Void => "Thêm giếng trời: bấm vào ô sàn / ô kín bởi dầm để biến thành giếng trời, thông tầng.",
        PlanEditMode.WC => "Sàn vệ sinh: bấm vào ô sàn để đổi thành sàn vệ sinh; bấm lại ô WC để trả về phòng ở.",
        PlanEditMode.SlabDropMode => SlabDropHint,   // [Sàn hạ cốt]
        PlanEditMode.StretchBeam or PlanEditMode.MoveBeam or PlanEditMode.TrimBeam or PlanEditMode.ExtendBeam or PlanEditMode.PickCad => BeamEditHint(m),   // [Sửa dầm]
        PlanEditMode.Delete => "Xoá: bấm vào tường, dầm, cột (hỏi lại), ô sàn hoặc lỗ sàn (cầu thang, giếng trời) để xoá.",
        PlanEditMode.AddBeam => "Thêm dầm: bấm điểm đầu rồi điểm cuối (bắt đầu dầm, nút, giao trục, giao điểm, trung điểm, vuông góc, điểm trên dầm – bật/tắt ở thanh 'Bắt điểm'); gõ số để nhập toạ độ x,y | @dx,dy | L<góc | L; bấm tiếp để vẽ nối; chuột phải / Esc để dừng.",
        PlanEditMode.QuickGridBeam or PlanEditMode.SecondaryBeam or PlanEditMode.CantileverBeam or PlanEditMode.MultiSelect => BeamToolHint(m),   // [BeamTools]
        PlanEditMode.AddCurvedBeam => "Thêm dầm cong: bấm điểm đầu, điểm cuối rồi một điểm bất kỳ trên cung (định độ vồng); bấm tiếp để vẽ nối; Esc để dừng.",
        PlanEditMode.AddWall => "Thêm tường: bấm điểm đầu rồi điểm cuối (loại tường, chiều cao, diện tích cửa theo ô nhập); Esc để dừng.",
        PlanEditMode.AssignWall => "Gán tường: bấm vào dầm để đặt tường suốt chiều dài dầm (thay tường cũ trùng vị trí).",
        PlanEditMode.AssignBuildUp => "Gán cấu tạo sàn: bấm vào ô sàn để gán cấu tạo đang chọn ở ô 'Cấu tạo sàn'.",
        _ => "Chọn: bấm vào cột, dầm, lỗ sàn hoặc ô sàn để xem thông tin; Esc để bỏ chọn."
    };

    private void PlanMode_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string tag || !Enum.TryParse(tag, out PlanEditMode m)) return;
        _mode = m;
        _lineStart = null; _arcEnd = null;
        ResetBeamEdit();   // [Sửa dầm]
        if (planHost != null) planHost.Cursor = m == PlanEditMode.Select ? null : Cursors.Cross;
        _hover = null;
        if (planOverlay != null) DrawHover();
        if (IsLoaded && txtStatus != null) SetStatus(ModeHint(m));
    }

    private SlabUse NewSlabUse() =>
        cbNewSlabUse.SelectedItem is ComboBoxItem it && it.Tag is string t && Enum.TryParse(t, out SlabUse u) ? u : SlabUse.Normal;

    /// <summary>Đối tượng tô sáng khi di chuột, tuỳ chế độ.</summary>
    private object HoverTarget(Point s)
    {
        if (_p == null) return null;
        switch (_mode)
        {
            case PlanEditMode.Select: return HitTest(s);
            case PlanEditMode.Balcony: return CurrentStory == AllStories ? null : NearestBeam(s, 10);
            case PlanEditMode.WC:
            case PlanEditMode.SlabDropMode:   // [Sàn hạ cốt]
            {
                var (x, y) = S2W(s);
                return SlabAtPlan(x, y);
            }
            case PlanEditMode.AssignWall: return CurrentStory == AllStories ? null : NearestBeam(s, 10);
            case PlanEditMode.AssignBuildUp:
            {
                var (x, y) = S2W(s);
                return SlabAtPlan(x, y);
            }
            case PlanEditMode.Delete:
            {
                var (x, y) = S2W(s);
                return (object)ColumnAt(s) ?? (object)WallAtScreen(s) ?? (object)NearestBeam(s, 8) ?? (object)OpeningAt(x, y) ?? SlabAtPlan(x, y);
            }
            case PlanEditMode.Stair:
            case PlanEditMode.Void:
            {
                var (x, y) = S2W(s);
                return (object)OpeningAt(x, y) ?? SlabAtPlan(x, y);
            }
            default: return null;
        }
    }

    /// <summary>Bấm (không kéo) trên mặt bằng.</summary>
    private void PlanClick(Point s)
    {
        if (_p == null) return;
        if (GridToolClick(s)) return;   // [Trục & tầng] vẽ trục bằng 2 điểm – MainWindow.SelectGridLevel.cs
        if (CadLinkAlignClick(s)) return;   // [CadLink] căn bản vẽ liên kết theo 2 điểm – MainWindow.CadLink.cs
        if (RevitPlanClick(s)) return;   // [Kiểu Revit] đặt cửa, chọn cửa, tường theo đường định vị – MainWindow.RevitDraw.cs
        if (CmdPlanClick(s)) return;   // [Dòng lệnh] cột, đo khoảng cách… – MainWindow.CadCommandLine.cs
        if (_mode == PlanEditMode.Select && RoofFramingClick(s)) return;   // [Hệ mái tôn] chọn mái / xà gồ / vì kèo
        if (_mode == PlanEditMode.Select) { _multiSel.Clear(); _multiSlab.Clear(); _mixSel.Clear(); SelectAt(s); return; }
        if (_mode == PlanEditMode.MultiSelect) { MultiSelectClick(s); return; }   // [BeamTools]
        if (_busy) { SetStatus("Chương trình đang tính toán – chờ xong rồi sửa mô hình."); return; }
        string st = CurrentStory;
        if (st == AllStories || _p.Stories.All(q => q.Name != st))
        {
            SetStatus("Chọn một tầng cụ thể ở ô 'Tầng đang xem' trước khi sửa mặt bằng.");
            return;
        }
        if (BeamToolClick(s, st)) return;   // [BeamTools] vẽ nhanh theo lưới, dầm phụ, công xôn – MainWindow.BeamTools.cs
        if (BeamEditClick(s, st)) return;   // [Sửa dầm] kéo, dời, trim, extend, pick CAD – MainWindow.BeamEdit.cs
        var (x, y) = S2W(s);
        // dầm / tường: bấm điểm đầu (chưa sửa mô hình)
        (double X, double Y) pEnd = default, pStart = default;
        if (IsLineMode)
        {
            pEnd = SnapPlan(st, x, y);
            if (_lineStart == null || _lineStory != st)
            {
                _lineStart = pEnd; _lineStory = st; _arcEnd = null;
                SetStatus($"Điểm đầu ({pEnd.X:0.00}; {pEnd.Y:0.00}) – bấm điểm cuối {(_mode == PlanEditMode.AddWall ? "tường" : _mode == PlanEditMode.AddCurvedBeam ? "dầm cong" : "dầm")} (Esc để huỷ).");
                DrawHover();
                return;
            }
            pStart = _lineStart.Value;
            if (_mode == PlanEditMode.AddCurvedBeam && _arcEnd == null)
            {
                // dầm cong: bấm thứ hai = điểm cuối, chờ điểm thứ ba trên cung
                if (Math.Abs(pStart.X - pEnd.X) < 1e-6 && Math.Abs(pStart.Y - pEnd.Y) < 1e-6) { SetStatus("Điểm cuối trùng điểm đầu – bấm điểm khác."); return; }
                _arcEnd = pEnd;
                SetStatus($"Điểm cuối ({pEnd.X:0.00}; {pEnd.Y:0.00}) – bấm một điểm trên cung để định độ vồng (Esc để huỷ).");
                DrawHover();
                return;
            }
            if (_mode != PlanEditMode.AddCurvedBeam && Math.Abs(pStart.X - pEnd.X) < 1e-6 && Math.Abs(pStart.Y - pEnd.Y) < 1e-6) { SetStatus("Điểm cuối trùng điểm đầu – bấm điểm khác."); return; }
        }
        string snap = Snapshot();
        EditResult r;
        object select = null;
        try
        {
            switch (_mode)
            {
                case PlanEditMode.AddBeam:
                {
                    string sec = cbEditBeamSec.SelectedItem as string;
                    if (string.IsNullOrEmpty(sec)) { SetStatus("Chọn tiết diện dầm ở ô 'Tiết diện dầm' trước."); return; }
                    r = AddBeamScoped(st, pStart, pEnd, sec);   // [BeamTools] đúng toạ độ đã bắt + phạm vi tầng (thay ModelEdit.AddBeam)
                    if (r.Ok) _lineStart = pEnd; // vẽ nối tiếp
                    break;
                }
                case PlanEditMode.AddCurvedBeam:
                {
                    string sec = cbEditBeamSec.SelectedItem as string;
                    if (string.IsNullOrEmpty(sec)) { SetStatus("Chọn tiết diện dầm ở ô 'Tiết diện dầm' trước."); return; }
                    var pArcEnd = _arcEnd.Value;
                    r = ModelEdit.AddCurvedBeam(_p, st, pStart, pArcEnd, pEnd, sec);
                    if (r.Ok) { _lineStart = pArcEnd; _arcEnd = null; } // vẽ nối tiếp từ điểm cuối
                    break;
                }
                case PlanEditMode.AddWall:
                {
                    var (type, hgt, open) = WallParams();
                    if (type == null) { SetStatus("Chọn loại tường ở ô 'Loại tường' trước."); return; }
                    int n0 = _p.Walls.Count;
                    r = ModelEdit.AddWall(_p, st, pStart.X, pStart.Y, pEnd.X, pEnd.Y, type, hgt, open);
                    if (r.Ok) { _lineStart = pEnd; if (_p.Walls.Count > n0) select = _p.Walls[^1]; }
                    break;
                }
                case PlanEditMode.AssignWall:
                {
                    var beam = NearestBeam(s, 10);
                    if (beam == null) { SetStatus("Bấm vào dầm (gần đường dầm) để gán tường."); return; }
                    var (type, hgt, open) = WallParams();
                    if (type == null) { SetStatus("Chọn loại tường ở ô 'Loại tường' trước."); return; }
                    var before = _p.Walls.ToList();
                    r = ModelEdit.AssignWallToBeam(_p, beam, type, hgt, open);
                    if (r.Ok) select = _p.Walls.FirstOrDefault(w => !before.Contains(w));
                    break;
                }
                case PlanEditMode.AssignBuildUp:
                {
                    var slab = ModelEdit.SlabAt(_p, st, x, y);
                    if (slab == null) { SetStatus("Không có ô sàn tại vị trí bấm."); return; }
                    string bu = cbEditBuildUp.SelectedItem as string;
                    if (string.IsNullOrEmpty(bu)) { SetStatus("Chọn cấu tạo sàn ở ô 'Cấu tạo sàn' trước."); return; }
                    r = ModelEdit.SetBuildUp(_p, slab, bu);
                    select = slab;
                    break;
                }
                case PlanEditMode.AddSlab:
                    r = ModelEdit.AddSlab(_p, st, x, y, NewSlabUse(), null);
                    break;
                case PlanEditMode.Balcony:
                {
                    var beam = NearestBeam(s, 10);
                    if (beam == null) { SetStatus("Bấm vào dầm biên (gần đường dầm) để thêm ban công."); return; }
                    double depth = Num(txtBalconyDepth, 1.2), rail = Num(txtRailing, 1.0);
                    r = ModelEdit.AddBalcony(_p, st, beam, depth, rail);
                    break;
                }
                case PlanEditMode.Stair:
                case PlanEditMode.Void:
                {
                    var before = _p.Openings.ToList();
                    r = ModelEdit.AddOpening(_p, st, x, y, _mode == PlanEditMode.Stair ? OpeningKind.Stair : OpeningKind.Void);
                    if (r.Ok) select = _p.Openings.FirstOrDefault(o => !before.Contains(o));
                    break;
                }
                case PlanEditMode.WC:
                {
                    var slab = ModelEdit.SlabAt(_p, st, x, y);
                    if (slab == null) { SetStatus("Không có ô sàn tại vị trí bấm – thêm ô sàn trước."); return; }
                    var use = ModelEdit.UseOf(_p, slab) == SlabUse.WC ? SlabUse.Normal : SlabUse.WC;
                    r = ModelEdit.SetSlabUse(_p, slab, use);
                    select = slab;
                    break;
                }
                case PlanEditMode.SlabDropMode:   // [Sàn hạ cốt] MainWindow.SlabDrop.cs
                {
                    var slab = ModelEdit.SlabAt(_p, st, x, y);
                    if (slab == null) { SetStatus("Không có ô sàn tại vị trí bấm – thêm ô sàn trước."); return; }
                    r = SlabDropClick(slab);
                    select = slab;
                    break;
                }
                case PlanEditMode.Delete:
                {
                    var col = ColumnAt(s);
                    if (col != null)
                    {
                        if (!ConfirmDelete($"Xoá cột {col.Label} ({col.Section}, tầng {col.Story}) cùng tải trên cột?\nCấu kiện phía trên có thể mất gối đỡ.")) return;
                        r = ModelEdit.DeleteFrame(_p, col);
                        break;
                    }
                    var wall = WallAtScreen(s);
                    if (wall != null)
                    {
                        r = ModelEdit.DeleteWall(_p, wall);
                        break;
                    }
                    var bm = NearestBeam(s, 8);
                    if (bm != null)
                    {
                        if (!ConfirmDelete($"Xoá dầm {bm.Label} ({bm.Section}, tầng {bm.Story}) cùng tải trên dầm?")) return;
                        r = ModelEdit.DeleteFrame(_p, bm);
                        break;
                    }
                    var op = _p.Openings.LastOrDefault(o => o.Story == st && o.Contains(x, y));
                    if (op != null)
                    {
                        if (!ConfirmDelete($"Xoá {op.Name.ToLowerInvariant()} ({op.X1 - op.X0:0.00}×{op.Y1 - op.Y0:0.00} m) tầng {st} và đặt lại ô sàn?")) return;
                        r = ModelEdit.RemoveOpening(_p, op);
                        select = ModelEdit.SlabAt(_p, st, (op.X0 + op.X1) / 2, (op.Y0 + op.Y1) / 2);
                        break;
                    }
                    var slab = ModelEdit.SlabAt(_p, st, x, y);
                    if (slab == null) { SetStatus("Không có ô sàn hoặc lỗ sàn tại vị trí bấm."); return; }
                    if (!ConfirmDelete($"Xoá ô sàn {slab.Label} (tầng {st}) cùng tải trên sàn?")) return;
                    r = ModelEdit.DeleteSlab(_p, slab);
                    break;
                }
                default: return;
            }
        }
        catch (Exception ex)
        {
            RestoreSnapshot(snap); // thao tác lỗi giữa chừng → trả lại mô hình trước đó
            ShowError("Không sửa được mô hình", ex);
            return;
        }
        if (!r.Ok) { SetStatus(r.Message); return; }
        AfterEdit(snap, r, select);
    }

    private bool ConfirmDelete(string msg) =>
        MessageBox.Show(this, msg, AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    /// <summary>Sau thao tác thành công: lưu hoàn tác, đánh dấu thay đổi (huỷ kết quả), làm mới, chọn đối tượng mới.</summary>
    private void AfterEdit(string snapshotBefore, EditResult r, object select)
    {
        PushUndo(snapshotBefore);
        bool hadResults = _res != null;
        _p.InvalidateIndex();
        _sel = null; _selSlab = null; _selOpening = null; _selFooting = 0; _selWall = null;
        _hover = null;
        MarkDirty();
        RefreshAll(false);
        if (select == null && r.NewSlabs.Count > 0) select = _p.Slabs.FirstOrDefault(q => q.Id == r.NewSlabs[0]);
        if (select == null && r.NewFrames.Count > 0) select = _p.Frames.FirstOrDefault(q => q.Id == r.NewFrames[0]);
        switch (select)
        {
            case WallLine w: Select(wall: w); break;
            case FloorOpening o: Select(opening: o); break;
            case Slab sl: Select(slab: sl); break;
            case FrameElement f: Select(frame: f); break;
            default: ClearSelection(); break;
        }
        SetStatus(r.Message + (hadResults ? "  – Mô hình đã thay đổi, cần chạy lại phân tích." : ""));
    }

    /// <summary>Phím Delete / Esc / Ctrl+Z (gọi từ Window_PreviewKeyDown). Trả về true nếu đã xử lý.</summary>
    private bool HandlePlanEditKey(Key key, bool ctrl)
    {
        if (_p == null) return false;
        if (key == Key.Tab && !ctrl && !IsTextInputFocused() && TabCycle()) return true;   // [Kiểu Revit] Tab chuyển vòng đối tượng chồng nhau – MainWindow.RevitGrips.cs
        if (!IsTextInputFocused() && HandleXrefMoveKey(key)) return true;   // [Di chuyển Xref] Esc, mũi tên nhích – MainWindow.XrefMove.cs
        if (!ctrl && !IsTextInputFocused() && HandleRoofKey(key)) return true;   // [Hệ mái tôn] Esc kết thúc vẽ, Delete xoá hệ mái đang chọn
        if (MixedSelKey(key, ctrl)) return true;   // [Quét chọn] Delete / Esc với nhóm đối tượng đã quét – MainWindow.SelectGridLevel.cs
        if (!ctrl && RevitPlanKey(key)) return true;   // [Kiểu Revit] Esc / Delete khi đặt / chọn cửa – MainWindow.RevitDraw.cs
        if (key == Key.Enter && !ctrl && !IsTextInputFocused() && BeamEditAuto()) return true;   // [Sửa dầm] Extend tự động
        bool del = key == Key.Delete && !ctrl, esc = key == Key.Escape, undo = ctrl && key == Key.Z, redo = ctrl && key == Key.Y;
        if (!del && !esc && !undo && !redo) return false;
        if (IsTextInputFocused()) return false; // để ô nhập / bảng tự xử lý
        if (esc && HandleCadLinkKey(key)) return true;   // [CadLink] huỷ căn bản vẽ
        if (esc && _lineStart != null)
        {
            _lineStart = null; _arcEnd = null;
            DrawHover();
            SetStatus(ModeHint(_mode));
            return true;
        }
        if (esc && CancelBeamEdit()) return true;   // [Sửa dầm] huỷ bước đang dở
        if (esc && CancelBeamTool()) return true;   // [BeamTools] khung chọn / chọn nhiều
        if (esc)
        {
            if (rbModeSelect.IsChecked != true) rbModeSelect.IsChecked = true;
            if (HasSelection) ClearSelection();
            SetStatus(ModeHint(PlanEditMode.Select));
            return true;
        }
        if (undo) { Undo(); return true; }
        if (redo) { Redo(); return true; }
        DeleteSelection();
        return true;
    }

    private static bool IsTextInputFocused()
    {
        var d = Keyboard.FocusedElement as DependencyObject;
        while (d != null)
        {
            if (d is TextBoxBase || d is PasswordBox || d is DataGrid) return true;
            if (d is ComboBox cb && (cb.IsEditable || cb.IsDropDownOpen)) return true;
            d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return false;
    }

    /// <summary>Xoá đối tượng đang chọn (tường, ô sàn, lỗ sàn, dầm, cột) – phím Delete.</summary>
    private void DeleteSelection()
    {
        if (_busy) return;
        if (DeleteMultiSelection()) return;   // [BeamTools] xoá nhiều dầm đã chọn
        string snap;
        EditResult r;
        object select = null;
        try
        {
            if (_selWall != null && _p.Walls.Contains(_selWall))
            {
                snap = Snapshot();
                r = ModelEdit.DeleteWall(_p, _selWall);
            }
            else if (_sel != null && _p.Frames.FirstOrDefault(f => f.Id == _sel.Id) is FrameElement fr)
            {
                if (!ConfirmDelete($"Xoá {(fr.IsVerticalKind ? "cột" : "dầm")} {fr.Label} ({fr.Section}, tầng {fr.Story}) cùng tải trên thanh?")) return;
                snap = Snapshot();
                r = ModelEdit.DeleteFrame(_p, fr);
            }
            else if (_selOpening != null && _p.Openings.Contains(_selOpening))
            {
                var op = _selOpening;
                if (!ConfirmDelete($"Xoá {op.Name.ToLowerInvariant()} ({op.X1 - op.X0:0.00}×{op.Y1 - op.Y0:0.00} m) tầng {op.Story} và đặt lại ô sàn?")) return;
                snap = Snapshot();
                r = ModelEdit.RemoveOpening(_p, op);
                select = ModelEdit.SlabAt(_p, op.Story, (op.X0 + op.X1) / 2, (op.Y0 + op.Y1) / 2);
            }
            else if (_selSlab != null && _p.Slabs.Contains(_selSlab))
            {
                var sl = _selSlab;
                if (!ConfirmDelete($"Xoá ô sàn {sl.Label} (tầng {sl.Story}) cùng tải trên sàn?")) return;
                snap = Snapshot();
                r = ModelEdit.DeleteSlab(_p, sl);
            }
            else
            {
                SetStatus("Chưa chọn tường, dầm, cột, ô sàn hoặc lỗ sàn để xoá.");
                return;
            }
        }
        catch (Exception ex) { ShowError("Không xoá được", ex); return; }
        if (!r.Ok) { SetStatus(r.Message); return; }
        AfterEdit(snap, r, select);
    }

    // ------------------------------------------------------------------
    // Hoàn tác
    // ------------------------------------------------------------------
    private string Snapshot() => JsonSerializer.Serialize(_p, Project.JsonOptions);

    private void PushUndo(string snap)
    {
        if (!ReferenceEquals(_undoOwner, _p)) { _undo.Clear(); _redo.Clear(); _undoOwner = _p; }
        SnapshotHistory.Push(_undo, _redo, snap, MaxUndo);
        UpdateUndoButton();
    }

    private void UpdateUndoButton()
    {
        if (btnUndo != null) btnUndo.IsEnabled = _undo.Count > 0 && ReferenceEquals(_undoOwner, _p);
    }

    private void BtnUndo_Click(object sender, RoutedEventArgs e) => Undo();

    private void Undo()
    {
        if (_busy) return;
        if (_undo.Count == 0 || !ReferenceEquals(_undoOwner, _p))
        {
            _undo.Clear(); UpdateUndoButton();
            SetStatus("Không có thao tác sửa mặt bằng nào để hoàn tác.");
            return;
        }
        string snap = SnapshotHistory.Undo(_undo, _redo, Snapshot(), MaxUndo);
        try
        {
            RestoreSnapshot(snap);
            SetStatus($"Đã hoàn tác thao tác sửa mặt bằng (còn {_undo.Count} bước hoàn tác, {_redo.Count} bước làm lại – Ctrl+Y).");
        }
        catch (Exception ex) { ShowError("Không hoàn tác được", ex); }
        UpdateUndoButton();
    }

    /// <summary>Làm lại thao tác vừa hoàn tác (Ctrl+Y / lệnh REDO).</summary>
    private void Redo()
    {
        if (_busy) return;
        if (_redo.Count == 0 || !ReferenceEquals(_undoOwner, _p))
        {
            _redo.Clear();
            SetStatus("Không có thao tác nào để làm lại.");
            return;
        }
        string snap = SnapshotHistory.Redo(_undo, _redo, Snapshot(), MaxUndo);
        try
        {
            RestoreSnapshot(snap);
            SetStatus($"Đã làm lại (còn {_redo.Count} bước làm lại).");
        }
        catch (Exception ex) { ShowError("Không làm lại được", ex); }
        UpdateUndoButton();
    }

    /// <summary>Nạp lại dự án từ ảnh chụp JSON (giống mở dự án, giữ tầng đang xem nếu còn).</summary>
    private void RestoreSnapshot(string snap)
    {
        var p = JsonSerializer.Deserialize<Project>(snap, Project.JsonOptions) ?? throw new InvalidOperationException("Ảnh chụp mô hình lỗi");
        bool keepUndo = ReferenceEquals(_undoOwner, _p);
        _p = p;
        if (keepUndo) _undoOwner = p;
        _p.InvalidateIndex();
        _res = null; _design = null;
        _sel = null; _selSlab = null; _selOpening = null; _selFooting = 0; _selWall = null;
        _lineStart = null; _arcEnd = null;
        _hover = null;
        MarkDirty();
        LoadUiFromProject();
        RefreshAll(false); // RefreshStories giữ tầng đang xem nếu còn tồn tại
        ClearSelection();
    }

    // ------------------------------------------------------------------
    // Dầm / tường: bắt điểm, dây chun, thông số
    // ------------------------------------------------------------------
    /// <summary>Bắt điểm theo ModelEdit.Snap với dung sai ≈ 10 px (0,15 … 0,5 m).</summary>
    private (double X, double Y) SnapPlan(string story, double x, double y)
    {
        if (_cmdExact) return (x, y);   // [Dòng lệnh] điểm gõ toạ độ – không bắt điểm
        return DraftConstrain(x, y, SnapPlanCore(story, x, y));   // [Dòng lệnh] F3 / F8 / F10 – MainWindow.CadCommandLine.cs
    }

    private (double X, double Y) SnapPlanCore(string story, double x, double y)
    {
        double tol = Math.Clamp(10 / Math.Max(_scale, 1e-6), 0.15, 0.5);
        if (UseEtabsSnap) return EtabsSnap(story, x, y, tol);   // [BeamTools] bắt điểm kiểu ETABS cho ＋ Dầm
        if (TryCadLinkSnap(story, x, y, tol, out var cadPt)) return cadPt;   // [CadLink] nét bản vẽ liên kết (nút/giao trục ưu tiên)
        return ModelEdit.Snap(_p, story, x, y, tol);
    }

    /// <summary>Loại tường, chiều cao (0 = tự động), diện tích cửa (−1 = theo loại tường) từ thanh công cụ.</summary>
    private (string type, double height, double opening) WallParams()
    {
        string type = cbEditWallType.SelectedItem as string;
        double h = string.IsNullOrWhiteSpace(txtEditWallH.Text) ? 0 : Math.Max(0, Num(txtEditWallH, 0));
        double open = string.IsNullOrWhiteSpace(txtEditWallOpen.Text) ? -1 : Num(txtEditWallOpen, -1);
        if (open < 0) open = -1;
        return (type, h, open);
    }

    private static readonly Brush RubberBeam = Frozen(Color.FromRgb(230, 120, 0));
    private static readonly Brush RubberWall = Frozen(Color.FromRgb(150, 80, 20));

    /// <summary>Lớp phủ khi vẽ dầm/tường: điểm bắt dưới chuột, đường dây chun từ điểm đầu, chiều dài.</summary>
    private void DrawLineToolOverlay()
    {
        string st = CurrentStory;
        if (st == AllStories || _p.Stories.All(q => q.Name != st)) return;
        if (_lineStart != null && _lineStory != st) { _lineStart = null; _arcEnd = null; }
        Brush br = _mode == PlanEditMode.AddWall ? RubberWall : RubberBeam;
        (double X, double Y)? cur = null;
        if (_lastMouse != null)
        {
            var (x, y) = S2W(_lastMouse.Value);
            cur = SnapPlan(st, x, y);
        }
        if (_lineStart != null && _mode == PlanEditMode.AddCurvedBeam && _arcEnd != null)
        {
            // dầm cong: cung qua điểm đầu, điểm cuối và vị trí chuột (MainWindow.CurvedBeam.cs)
            DrawSnapMarker(st, _lineStart.Value, br, true);
            DrawArcPreview(_lineStart.Value, _arcEnd.Value, cur, br);
        }
        else if (_lineStart != null)
        {
            var a = W2S(_lineStart.Value.X, _lineStart.Value.Y);
            DrawSnapMarker(st, _lineStart.Value, br, true);
            if (cur != null)
            {
                var b = W2S(cur.Value.X, cur.Value.Y);
                double th = 2;
                if (_mode == PlanEditMode.AddWall)
                {
                    var wt = _p.WallTypes.FirstOrDefault(t => t.Name == cbEditWallType.SelectedItem as string);
                    th = Math.Max(3, (wt?.Thickness ?? 0.11) * _scale);
                }
                var l = new Shapes.Line
                {
                    X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = br, StrokeThickness = th, Opacity = 0.75,
                    StrokeDashArray = new DoubleCollection { 4, 2 }, IsHitTestVisible = false
                };
                planOverlay.Children.Add(l);
                double L = Math.Sqrt(Math.Pow(cur.Value.X - _lineStart.Value.X, 2) + Math.Pow(cur.Value.Y - _lineStart.Value.Y, 2));
                AddText($"L = {L:0.00} m", new Point((a.X + b.X) / 2 + 8, (a.Y + b.Y) / 2 - 16), br, 11, false, false, planOverlay);
            }
        }
        if (cur != null) DrawSnapMarker(st, cur.Value, br, false);
    }

    /// <summary>Ký hiệu điểm bắt: ô vuông = nút có sẵn, dấu × = giao trục, chấm tròn = lưới 50 mm.</summary>
    private void DrawSnapMarker(string story, (double X, double Y) pt, Brush br, bool filled)
    {
        if (!filled && DrawEtabsSnapGlyph(pt)) return;   // [BeamTools] ký hiệu điểm bắt kiểu ETABS
        if (!filled && !UseEtabsSnap && DrawCadSnapGlyph(pt)) return;   // [CadLink] ký hiệu điểm bắt trên bản vẽ link
        var c = W2S(pt.X, pt.Y);
        double z = ModelEdit.StoryZ(_p, story);
        bool onNode = _p.Nodes.Any(n => Math.Abs(n.Z - z) < 0.01 && Math.Abs(n.X - pt.X) < 1e-4 && Math.Abs(n.Y - pt.Y) < 1e-4);
        bool onGridX = _p.Grids.Any(g => g.Dir == GridDir.X && Math.Abs(g.Coord - pt.X) < 1e-4);
        bool onGridY = _p.Grids.Any(g => g.Dir == GridDir.Y && Math.Abs(g.Coord - pt.Y) < 1e-4);
        if (onNode)
        {
            var r = new Shapes.Rectangle { Width = 12, Height = 12, Stroke = br, StrokeThickness = 2, Fill = filled ? br : null, IsHitTestVisible = false };
            Canvas.SetLeft(r, c.X - 6); Canvas.SetTop(r, c.Y - 6);
            planOverlay.Children.Add(r);
        }
        else if (onGridX && onGridY)
        {
            CLine(planOverlay, new Point(c.X - 6, c.Y - 6), new Point(c.X + 6, c.Y + 6), br, 2, false);
            CLine(planOverlay, new Point(c.X - 6, c.Y + 6), new Point(c.X + 6, c.Y - 6), br, 2, false);
        }
        else
        {
            var e = new Shapes.Ellipse { Width = 9, Height = 9, Stroke = br, StrokeThickness = 1.6, Fill = filled ? br : null, IsHitTestVisible = false };
            Canvas.SetLeft(e, c.X - 4.5); Canvas.SetTop(e, c.Y - 4.5);
            planOverlay.Children.Add(e);
        }
        if (!filled) AddText($"({pt.X:0.00}; {pt.Y:0.00})", new Point(c.X + 9, c.Y + 6), Brushes.DimGray, 9, false, false, planOverlay);
    }

    /// <summary>Nạp danh sách cho các ô chọn của thanh công cụ (tiết diện dầm, loại tường, cấu tạo sàn) khi thay đổi.</summary>
    private void RefreshEditCombos()
    {
        if (_p == null || cbEditBeamSec == null) return;
        var rect = _p.FrameSections.Where(q => q.Shape == SectionShape.RectConcrete).Select(q => q.Name).ToList();
        var beams = rect.Where(n => n.StartsWith("D", StringComparison.OrdinalIgnoreCase)).ToList();
        if (beams.Count == 0) beams = rect;
        var walls = _p.WallTypes.Select(t => t.Name).ToList();
        var bus = _p.FloorBuildUps.Select(b => b.Name).ToList();
        string sig = string.Join("|", beams) + "#" + string.Join("|", walls) + "#" + string.Join("|", bus) + "#" + _p.GetHashCode();
        if (sig == _editComboSig) return;
        _editComboSig = sig;
        string mostUsed = _p.Frames.Where(f => !f.IsVerticalKind && f.IsConcrete && beams.Contains(f.Section))
            .GroupBy(f => f.Section).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
        void Set(ComboBox cb, List<string> items, string prefer)
        {
            string cur = cb.SelectedItem as string;
            cb.ItemsSource = items;
            cb.SelectedItem = cur != null && items.Contains(cur) ? cur : prefer != null && items.Contains(prefer) ? prefer : items.FirstOrDefault();
        }
        Set(cbEditBeamSec, beams, mostUsed);
        Set(cbEditWallType, walls, walls.FirstOrDefault(n => n.Contains("220")));
        Set(cbEditBuildUp, bus, bus.FirstOrDefault());
    }
}

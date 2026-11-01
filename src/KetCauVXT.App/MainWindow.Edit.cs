using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Model;

namespace KetCauVXT.App;

// =====================================================================
// SỬA MÔ HÌNH TRÊN MẶT BẰNG: thêm ô sàn, ban công, cầu thang, giếng trời, sàn vệ sinh, xoá; hoàn tác (Ctrl+Z).
// Thanh công cụ nằm trong tab "Mặt bằng" (MainWindow.xaml). Thao tác mô hình ở KetCauVXT.Model.ModelEdit.
// =====================================================================
public partial class MainWindow
{
    private enum PlanEditMode { Select, AddSlab, Balcony, Stair, Void, WC, Delete }

    private PlanEditMode _mode = PlanEditMode.Select;

    // hoàn tác: ảnh chụp JSON của dự án TRƯỚC mỗi thao tác sửa (tối đa 30); chỉ dùng cho đúng đối tượng Project đã chụp
    private const int MaxUndo = 30;
    private readonly List<string> _undo = new();
    private Project _undoOwner;

    private static string ModeHint(PlanEditMode m) => m switch
    {
        PlanEditMode.AddSlab => "Thêm ô sàn: bấm vào ô kín bởi dầm để thêm sàn (công năng theo ô 'Công năng').",
        PlanEditMode.Balcony => "Thêm ban công: bấm vào dầm biên ngoài nhà để thêm ban công công xôn (độ vươn, tải lan can theo ô nhập).",
        PlanEditMode.Stair => "Thêm cầu thang: bấm vào ô sàn / ô kín bởi dầm để biến thành ô cầu thang (tải truyền vào dầm viền).",
        PlanEditMode.Void => "Thêm giếng trời: bấm vào ô sàn / ô kín bởi dầm để biến thành giếng trời, thông tầng.",
        PlanEditMode.WC => "Sàn vệ sinh: bấm vào ô sàn để đổi thành sàn vệ sinh; bấm lại ô WC để trả về phòng ở.",
        PlanEditMode.Delete => "Xoá: bấm vào ô sàn hoặc lỗ sàn (cầu thang, giếng trời) để xoá.",
        _ => "Chọn: bấm vào cột, dầm, lỗ sàn hoặc ô sàn để xem thông tin; Esc để bỏ chọn."
    };

    private void PlanMode_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string tag || !Enum.TryParse(tag, out PlanEditMode m)) return;
        _mode = m;
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
            {
                var (x, y) = S2W(s);
                return SlabAtPlan(x, y);
            }
            case PlanEditMode.Delete:
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
        if (_mode == PlanEditMode.Select) { SelectAt(s); return; }
        if (_busy) { SetStatus("Chương trình đang tính toán – chờ xong rồi sửa mô hình."); return; }
        string st = CurrentStory;
        if (st == AllStories || _p.Stories.All(q => q.Name != st))
        {
            SetStatus("Chọn một tầng cụ thể ở ô 'Tầng đang xem' trước khi sửa mặt bằng.");
            return;
        }
        var (x, y) = S2W(s);
        string snap = Snapshot();
        EditResult r;
        object select = null;
        try
        {
            switch (_mode)
            {
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
                case PlanEditMode.Delete:
                {
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
        _sel = null; _selSlab = null; _selOpening = null; _selFooting = 0;
        _hover = null;
        MarkDirty();
        RefreshAll(false);
        if (select == null && r.NewSlabs.Count > 0) select = _p.Slabs.FirstOrDefault(q => q.Id == r.NewSlabs[0]);
        if (select == null && r.NewFrames.Count > 0) select = _p.Frames.FirstOrDefault(q => q.Id == r.NewFrames[0]);
        switch (select)
        {
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
        bool del = key == Key.Delete && !ctrl, esc = key == Key.Escape, undo = ctrl && key == Key.Z;
        if (!del && !esc && !undo) return false;
        if (IsTextInputFocused()) return false; // để ô nhập / bảng tự xử lý
        if (esc)
        {
            if (rbModeSelect.IsChecked != true) rbModeSelect.IsChecked = true;
            if (HasSelection) ClearSelection();
            SetStatus(ModeHint(PlanEditMode.Select));
            return true;
        }
        if (undo) { Undo(); return true; }
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

    /// <summary>Xoá đối tượng đang chọn (ô sàn hoặc lỗ sàn) – phím Delete.</summary>
    private void DeleteSelection()
    {
        if (_busy) return;
        string snap;
        EditResult r;
        object select = null;
        try
        {
            if (_selOpening != null && _p.Openings.Contains(_selOpening))
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
                SetStatus(_sel != null ? "Phím Delete chỉ xoá ô sàn hoặc lỗ sàn – chưa hỗ trợ xoá dầm/cột trên mặt bằng." : "Chưa chọn ô sàn hoặc lỗ sàn để xoá.");
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
        if (!ReferenceEquals(_undoOwner, _p)) { _undo.Clear(); _undoOwner = _p; }
        _undo.Add(snap);
        while (_undo.Count > MaxUndo) _undo.RemoveAt(0);
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
        string snap = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        try
        {
            RestoreSnapshot(snap);
            SetStatus($"Đã hoàn tác thao tác sửa mặt bằng (còn {_undo.Count} bước hoàn tác).");
        }
        catch (Exception ex) { ShowError("Không hoàn tác được", ex); }
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
        _sel = null; _selSlab = null; _selOpening = null; _selFooting = 0;
        _hover = null;
        MarkDirty();
        LoadUiFromProject();
        RefreshAll(false); // RefreshStories giữ tầng đang xem nếu còn tồn tại
        ClearSelection();
    }
}

using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Detailing;
using Microsoft.Win32;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>Công cụ bổ sung của không gian vẽ mặt bằng kết cấu: nhân bản tầng, hoàn tác, bắt điểm đầu dầm / tâm cột + vuông góc (Shift),
/// sàn bấm trong ô dầm, di chuyển, ánh xạ (xref) mặt bằng kiến trúc → đề xuất lanh tô theo cửa, ô văng trên cửa, nhập mặt bằng kết cấu
/// vẽ trong AutoCAD (DXF / DWG hoặc lệnh VXT_MB_KC), liên kết hai chiều với cây thư mục cấu kiện.</summary>
public sealed partial class DetailingPlanCanvas
{
    public const string ToolSlabBay = "SanO", ToolCanopyDoor = "OVCua", ToolMove = "DiChuyen";

    /// <summary>Thư mục hồ sơ (để lưu / tìm ánh xạ theo đường dẫn tương đối).</summary>
    public Func<string> ProjectDir { get; set; }
    /// <summary>Chọn một vị trí trên mặt bằng → cửa sổ chọn đúng cấu kiện đó trên cây (theo mặt bằng).</summary>
    public event Action<IDetailMember, PlanLevel> Picked;

    private readonly PlanUndo _undo = new();
    private PlanItem _moveItem;
    private (double X, double Y) _moveFrom;

    public PlanLevel CurrentLevel => Level;
    private string Dir => ProjectDir?.Invoke();

    private WrapPanel BuildToolsRow()
    {
        var r = new WrapPanel { Margin = new Thickness(0, 3, 0, 0) };
        r.Children.Add(Btn("⧉ Nhân bản tầng…", CopyStoreyDialog, "Nhân bản toàn bộ cấu kiện của mặt bằng đang xem sang các tầng giống nhau về bố trí công năng"));
        r.Children.Add(Btn("↶ Hoàn tác", Undo, "Hoàn tác thao tác trên mặt bằng (Ctrl+Z)"));
        r.Children.Add(Btn("Ánh xạ KT (xref)…", AttachUnderlay, "Gắn mặt bằng kiến trúc (DXF/DWG) của tầng làm nền tham chiếu – tường, cửa; không sửa file gốc, tự nạp lại khi file thay đổi"));
        r.Children.Add(Btn("Ẩn / hiện xref", ToggleUnderlay, "Ẩn / hiện nền mặt bằng kiến trúc của tầng"));
        r.Children.Add(Btn("Dời xref…", ShiftUnderlay, "Dời nền kiến trúc (m) để trục kiến trúc trùng trục kết cấu"));
        r.Children.Add(Btn("Lanh tô từ cửa mô hình", LintelsFromModel, "Đặt lanh tô cho mọi cửa gắn tường của mô hình (chế độ Thiết kế, giao diện kiểu Revit) lên mặt bằng lanh tô các tầng – gộp cửa giống nhau thành LT1, LT2…"));
        r.Children.Add(Btn("Lanh tô theo cửa KT", LintelsFromDoors, "Đề xuất lanh tô cho mọi ô cửa của ánh xạ kiến trúc tầng này (gộp các cửa giống nhau thành LT1, LT2…)"));
        r.Children.Add(Btn("Nhập MB kết cấu từ CAD…", ImportCadPlan, "Nhập mặt bằng kết cấu vẽ trong AutoCAD (DXF/DWG): dầm (layer DAM/BEAM), cột (COT/COLUMN), chữ tên D1 220x400, C1, S1…"));
        return r;
    }

    // ------------------------------------------------------------------
    // bắt điểm
    // ------------------------------------------------------------------
    private (double X, double Y) SnapW(PlanLevel lv, (double X, double Y) w, double tol)
    {
        bool ortho = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        (double X, double Y)? from = _first;
        if (from == null && _moveItem != null) from = _moveFrom;
        var s = PlanSnap.Snap(Plan, lv, w.X, w.Y, tol, from, ortho);
        return (s.X, s.Y);
    }

    // ------------------------------------------------------------------
    // liên kết cây thư mục
    // ------------------------------------------------------------------
    /// <summary>Chọn mặt bằng lv (nếu có) và đánh dấu vị trí đầu tiên của cấu kiện m trên đó.</summary>
    public void Highlight(IDetailMember m, PlanLevel lv)
    {
        if (m == null) return;
        var pl = PlanMemberMap.Placements(Plan, m);
        if (pl.Count == 0) return;
        var pick = pl.FirstOrDefault(x => x.Level == lv);
        if (pick.Level == null) pick = pl[0];
        int idx = Plan.Levels.IndexOf(pick.Level);
        if (idx >= 0 && idx != _level.SelectedIndex) _level.SelectedIndex = idx;
        _sel = pick.Item;
        Redraw();
    }

    // ------------------------------------------------------------------
    // công cụ thêm
    // ------------------------------------------------------------------
    private bool HandleExtraTool(PlanLevel lv, (double X, double Y) w, double tol)
    {
        switch (_tool)
        {
            case ToolSlabBay:
            {
                var bay = PlanSnap.BayAt(Plan, lv, w.X, w.Y);
                if (bay == null) { _status.Text = "Không tìm thấy ô kín bao quanh điểm bấm (cần dầm hoặc trục ở cả 4 phía)."; return true; }
                var (x0, y0, x1, y1) = bay.Value;
                string member = _member.SelectedItem as string;
                if (member == NewMember) member = "";
                var it = Plan.Place(P, lv, PlanKinds.Slab, x0, y0, x1, y1, member ?? "");
                Done(it);
                return true;
            }
            case ToolCanopyDoor:
            {
                var u = ArchUnderlay.Load(lv, Dir);
                var ops = DoorLintels.Shifted(u, lv);
                var op = ops.Select(o => (O: o, D: StructuralPlan.DistSeg(w.X, w.Y, o.X1, o.Y1, o.X2, o.Y2))).Where(t => t.D <= Math.Max(tol, 0.4)).OrderBy(t => t.D).Select(t => t.O).FirstOrDefault();
                if (op == null) { _status.Text = "Bấm gần một ô cửa của ánh xạ kiến trúc (gắn ánh xạ bằng nút \"Ánh xạ KT (xref)…\")."; return true; }
                string lcs = DialogUi.InputBox(_owner, "Ô văng trên cửa", "Vươn ô văng Lc (mm):", "600");
                if (lcs == null) return true;
                double lc = DialogUi.ParseNum(lcs, 600) / 1000;
                var it = DoorLintels.PlaceCanopy(P, lv, op, Math.Clamp(lc, 0.2, 2.0));
                if (it != null) Done(it);
                return true;
            }
            case ToolMove:
            {
                if (_moveItem == null)
                {
                    var hit = StructuralPlan.HitTest(lv, w.X, w.Y, tol);
                    if (hit == null) return true;
                    _moveItem = hit; _sel = hit;
                    var s0 = PlanSnap.Snap(Plan, lv, w.X, w.Y, tol);
                    _moveFrom = (s0.X, s0.Y);
                    UpdateHint(); Redraw();
                    return true;
                }
                var to = SnapW(lv, w, tol);
                PlanSnap.Move(_moveItem, to.X - _moveFrom.X, to.Y - _moveFrom.Y);
                _moveItem = null;
                _changed(); UpdateHint(); Redraw();
                return true;
            }
        }
        return false;
    }

    private void DrawMovePreview()
    {
        if (_moveItem == null) return;
        double dx = _mouseW.X - _moveFrom.X, dy = _mouseW.Y - _moveFrom.Y;
        var br = Brushes.DodgerBlue;
        var it = _moveItem;
        if (PlanKinds.IsPoint(it.Kind)) RectW(it.X1 + dx - 0.15, it.Y1 + dy - 0.15, it.X1 + dx + 0.15, it.Y1 + dy + 0.15, br, 1.5, null, true);
        else if (PlanKinds.IsSegment(it.Kind)) Line(it.X1 + dx, it.Y1 + dy, it.X2 + dx, it.Y2 + dy, br, 2, true);
        else RectW(it.MinX + dx, it.MinY + dy, it.MaxX + dx, it.MaxY + dy, br, 1.5, null, true);
    }

    private void Undo()
    {
        if (!_undo.Undo(Plan)) { _status.Text = "Không còn thao tác để hoàn tác."; return; }
        _sel = null; _first = null; _moveItem = null;
        int idx = _level.SelectedIndex;
        RefreshLevels(Math.Min(Math.Max(idx, 0), Plan.Levels.Count - 1));
        _sx.Text = Plan.SpansX ?? ""; _sy.Text = Plan.SpansY ?? "";
        _changed(); FillMembers(); UpdateHint(); Redraw();
    }

    // ------------------------------------------------------------------
    // nhân bản tầng
    // ------------------------------------------------------------------
    private void CopyStoreyDialog()
    {
        var src = Level;
        if (src == null) return;
        if (src.IsFoundation) { MessageBox.Show(_owner, "Chọn một mặt bằng tầng (không phải mặt bằng móng) để nhân bản.", "Nhân bản tầng"); return; }
        var w = new Window { Title = "Nhân bản tầng – " + src.Name, Owner = _owner, Width = 520, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        var sp = new StackPanel { Margin = new Thickness(12) };
        var above = Plan.Levels.Where(l => !l.IsFoundation && l.Elevation > src.Elevation + 1e-9).OrderBy(l => l.Elevation).FirstOrDefault();
        double hDef = above != null ? Math.Round(above.Elevation - src.Elevation, 3) : 3.6;
        var nCopies = new TextBox { Text = "1", Width = 60 };
        var hStorey = new TextBox { Text = hDef.ToString("0.###", CultureInfo.InvariantCulture), Width = 60 };
        var rbA = new RadioButton { Content = "A – Dùng chung cấu kiện (cùng tên D1, S1…; SL nhân theo số tầng)", IsChecked = true, Margin = new Thickness(0, 4, 0, 2) };
        var rbB = new RadioButton { Content = "B – Tạo cấu kiện mới cho mỗi tầng (D1 → D1-T3…, sửa độc lập; cột vẫn dùng chung)", Margin = new Thickness(0, 2, 0, 4) };
        var shift = new CheckBox { Content = "Đẩy các mặt bằng phía trên (vd. Mái) lên tương ứng", IsChecked = true, Margin = new Thickness(0, 4, 0, 4) };
        sp.Children.Add(DialogUi.Hint("Sao toàn bộ vị trí cấu kiện (cột, dầm, sàn, lanh tô, ô văng, cầu thang) và đường cắt sàn của " + src.Name + " sang các tầng mới; cột được thêm tầng."));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Số tầng nhân bản:", 170), nCopies));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Chiều cao tầng (m):", 170), hStorey));
        sp.Children.Add(rbA); sp.Children.Add(rbB); sp.Children.Add(shift);
        var ok = new Button { Content = "Nhân bản", IsDefault = true, Width = 100, Margin = new Thickness(0, 8, 8, 0) };
        var cancel = new Button { Content = "Huỷ", IsCancel = true, Width = 80, Margin = new Thickness(0, 8, 0, 0) };
        ok.Click += (s, e) => { w.DialogResult = true; };
        var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        bp.Children.Add(ok); bp.Children.Add(cancel); sp.Children.Add(bp);
        w.Content = sp;
        if (w.ShowDialog() != true) return;
        int n = (int)Math.Round(DialogUi.ParseNum(nCopies.Text, 1));
        double h = DialogUi.ParseNum(hStorey.Text, hDef);
        if (n < 1 || n > 50 || h < 1.5) { MessageBox.Show(_owner, "Số tầng 1…50, chiều cao tầng ≥ 1,5 m.", "Nhân bản tầng"); return; }
        _undo.Push(Plan);
        var made = PlanStorey.CopyLevels(P, Plan, src, n, h, rbB.IsChecked == true, shift.IsChecked == true);
        RefreshLevels(Plan.Levels.IndexOf(made.LastOrDefault() ?? src));
        _changed(); FillMembers(); UpdateHint(); Redraw();
        _status.Text = "Đã nhân bản " + src.Name + " → " + string.Join(", ", made.Select(l => l.Name)) + (rbB.IsChecked == true ? " (cấu kiện mới theo tầng)." : " (dùng chung cấu kiện – SL đã cập nhật theo mặt bằng).");
    }

    // ------------------------------------------------------------------
    // ánh xạ kiến trúc, lanh tô theo cửa
    // ------------------------------------------------------------------
    private void AttachUnderlay()
    {
        var lv = Level; if (lv == null) return;
        var dlg = new OpenFileDialog { Title = "Ánh xạ mặt bằng kiến trúc – " + lv.Name, Filter = "Bản vẽ AutoCAD (*.dxf;*.dwg)|*.dxf;*.dwg|Tất cả|*.*" };
        if (dlg.ShowDialog(_owner) != true) return;
        _undo.Push(Plan);
        ArchUnderlay.Attach(lv, dlg.FileName, Dir);
        var u = ArchUnderlay.Load(lv, Dir);
        if (u == null) { _status.Text = "Không đọc được file ánh xạ."; return; }
        // dời gốc: góc trái – dưới của tường về gốc trục nếu chưa trùng
        if (u.WallCenters.Count > 0 && Plan.HasGrid && Math.Abs(lv.UnderlayDx) + Math.Abs(lv.UnderlayDy) < 1e-9)
        {
            double x0 = u.WallCenters.Min(c => Math.Min(c.Center.X1, c.Center.X2)), y0 = u.WallCenters.Min(c => Math.Min(c.Center.Y1, c.Center.Y2));
            if (Math.Abs(x0) > 2 || Math.Abs(y0) > 2) { lv.UnderlayDx = Math.Round(-x0, 3); lv.UnderlayDy = Math.Round(-y0, 3); }
        }
        _changed(); _fitted = false; Redraw();
        _status.Text = "Ánh xạ " + Path.GetFileName(u.Path) + ": " + u.Walls.Count + " nét tường, " + u.Openings.Count + " ô cửa" + (u.Warnings.Count > 0 ? " – " + u.Warnings[0] : "")
                       + ". Bấm \"Lanh tô theo cửa KT\" để đề xuất lanh tô.";
    }

    private void ToggleUnderlay()
    {
        var lv = Level; if (lv == null) return;
        lv.UnderlayVisible = !lv.UnderlayVisible; _changed(); Redraw();
    }

    private void ShiftUnderlay()
    {
        var lv = Level; if (lv == null) return;
        string t = DialogUi.InputBox(_owner, "Dời ánh xạ kiến trúc", "Dời theo X; Y (m), vd. -12.5; 3.2:",
            lv.UnderlayDx.ToString("0.###", CultureInfo.InvariantCulture) + "; " + lv.UnderlayDy.ToString("0.###", CultureInfo.InvariantCulture));
        if (t == null) return;
        var parts = t.Split(';');
        if (parts.Length < 2) return;
        _undo.Push(Plan);
        lv.UnderlayDx = DialogUi.ParseNum(parts[0], lv.UnderlayDx); lv.UnderlayDy = DialogUi.ParseNum(parts[1], lv.UnderlayDy);
        _changed(); Redraw();
    }

    private void LintelsFromDoors()
    {
        var lv = Level; if (lv == null) return;
        var u = ArchUnderlay.Load(lv, Dir);
        if (u == null) { MessageBox.Show(_owner, "Mặt bằng " + lv.Name + " chưa có ánh xạ kiến trúc – bấm \"Ánh xạ KT (xref)…\".", "Lanh tô theo cửa"); return; }
        var ops = DoorLintels.Shifted(u, lv);
        if (ops.Count == 0) { MessageBox.Show(_owner, "Không nhận được ô cửa nào (layer CUA / DOOR / WINDOW hoặc block cửa).", "Lanh tô theo cửa"); return; }
        _undo.Push(Plan);
        var added = DoorLintels.Place(P, lv, ops);
        _changed(); FillMembers(); UpdateHint(); Redraw();
        var groups = added.GroupBy(i => i.Member).Select(g => g.Key + "×" + g.Count());
        bool thin = added.Any(i => P.Lintels.FirstOrDefault(l => l.Name == i.Member)?.Bw <= ManualLintel.Thin);
        _status.Text = "Đã đặt " + added.Count + " lanh tô trên " + ops.Count + " ô cửa: " + string.Join(", ", groups)
                       + (thin ? ". Cửa trong tường 110: xem gợi ý an toàn tiết diện / gối ở chi tiết lanh tô." : ".")
                       + " Cây thư mục: \"" + StructuralPlan.LintelGroupTitle(lv) + "\".";
    }

    // ------------------------------------------------------------------
    // nhập mặt bằng kết cấu từ AutoCAD
    // ------------------------------------------------------------------
    private void ImportCadPlan()
    {
        var lv = Level; if (lv == null) return;
        var dlg = new OpenFileDialog { Title = "Nhập mặt bằng kết cấu vẽ trong AutoCAD – " + lv.Name, Filter = "Bản vẽ AutoCAD (*.dxf;*.dwg)|*.dxf;*.dwg|Tất cả|*.*" };
        if (dlg.ShowDialog(_owner) != true) return;
        var log = new List<string>();
        var ents = StructuralPlanImport.ReadFile(dlg.FileName, log);
        ImportEntities(ents, Path.GetFileName(dlg.FileName), log);
    }

    /// <summary>Nhập thực thể (từ file hoặc lệnh VXT_MB_KC) vào mặt bằng đang xem.</summary>
    public void ImportEntities(List<KetCauVXT.Import.DxfEntity> ents, string source, List<string> log = null)
    {
        var lv = Level; if (lv == null) return;
        if (ents == null || ents.Count == 0) { MessageBox.Show(_owner, "Không có đối tượng để nhập" + (log?.Count > 0 ? ": " + log[0] : "."), "Nhập mặt bằng kết cấu"); return; }
        bool replace = lv.Items.Any(i => i.Kind == PlanKinds.Beam || i.Kind == PlanKinds.Column)
                       && MessageBox.Show(_owner, "Mặt bằng " + lv.Name + " đã có dầm / cột. Xoá vị trí dầm, cột, sàn cũ trước khi nhập?", "Nhập mặt bằng kết cấu", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
        _undo.Push(Plan);
        var r = StructuralPlanImport.Import(P, lv, ents, true, replace);
        _changed(); FillMembers(); _fitted = false; UpdateHint(); Redraw();
        _status.Text = (source ?? "") + ": " + string.Join(" ", r.Log) + (log?.Count > 0 ? " (" + log[0] + ")" : "");
    }

    // ------------------------------------------------------------------
    // vẽ nền kiến trúc
    // ------------------------------------------------------------------
    private void DrawUnderlay(PlanLevel lv)
    {
        if (lv == null || !lv.UnderlayVisible || (string.IsNullOrEmpty(lv.Underlay) && string.IsNullOrEmpty(lv.UnderlayRel))) return;
        ArchUnderlay u;
        try { u = ArchUnderlay.Load(lv, Dir); } catch { return; }
        if (u == null) return;
        var br = new SolidColorBrush(Color.FromRgb(0xB8, 0xB8, 0xB8));
        double dx = lv.UnderlayDx, dy = lv.UnderlayDy;
        int n = 0;
        foreach (var s in u.Lines)
        {
            if (++n > 30000) break;
            var a = ToS(s.X1 + dx, s.Y1 + dy); var c = ToS(s.X2 + dx, s.Y2 + dy);
            _cv.Children.Add(new Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = c.X, Y2 = c.Y, Stroke = br, StrokeThickness = 0.7, IsHitTestVisible = false });
        }
        var dbr = new SolidColorBrush(Color.FromArgb(0x90, 0xD2, 0x69, 0x1E));
        foreach (var o in DoorLintels.Shifted(u, lv))
        {
            Line(o.X1, o.Y1, o.X2, o.Y2, dbr, 3, false);
            Text(o.X, o.Y - 0.25, (o.Kind == "Cửa sổ" ? "S " : "Đ ") + Dim(o.Width) + (o.WallT > 0 ? " / t" + Dim(o.WallT) : ""), dbr, 9);
        }
    }
}

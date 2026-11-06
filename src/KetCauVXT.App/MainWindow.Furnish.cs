using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using KetCauVXT.Drawing;
using KetCauVXT.Model;
using Microsoft.Win32;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// [Kiểu Revit] ĐẶT ĐỒ ĐẠC BẰNG BẮT ĐIỂM (không nhập X, Y), KÍCH THƯỚC TẠM SỬA ĐƯỢC, THAO TÁC TRÊN 3D,
// KHUNG NHÌN MẶT CẮT NGANG TẦNG + MẶT CẮT ĐỨNG 1-1 / 2-2 TỰ SINH, CÔNG CỤ VẼ MẶT CẮT 2 ĐIỂM.
// Lõi: Model/PlaceSnap.cs (FurnitureSnap, TempDimensions, Pick3DMath, View3DFilter, LevelViews), Drawing/SectionView.cs.
// Móc: RevitPlanMove / RevitPlanClick / RevitPlanKey / RevitFlip / DrawRevitSelection (MainWindow.RevitDraw.cs),
//      BuildScene / chuột 3D (MainWindow.View3D.cs), RefreshBrowser (MainWindow.RevitUi.cs), thư viện 3D (MainWindow.Disciplines.cs).
// =====================================================================
public partial class MainWindow
{
    private LibraryItem _fnPlace;
    private double _fnRot;
    private FurnitureItem _fnSel;
    private bool _secTool;
    private (double X, double Y)? _secStart;
    private readonly View3DFilter _v3Filter = new();
    private string _v3SelId;
    private bool _v3SectionBox;
    // kéo trong 3D
    private bool _v3Drag, _v3Moved;
    private Ray3 _v3DragRay;
    private double _v3X0, _v3Y0, _v3Z;
    /// <summary>Đang kéo thiết bị điện nước trong 3D (Id thiết bị; 0 = kéo đồ đạc).</summary>
    private int _v3DragDev;
    private string _v3DragSnap;
    private Border _v3Bar;

    // ------------------------------------------------------------------
    // Đặt đồ đạc bằng bắt điểm
    // ------------------------------------------------------------------
    private void StartPlaceFurniture(LibraryItem it)
    {
        if (_p == null || it == null) return;
        StopSectionTool();
        _fnPlace = it; _fnRot = 0; _fnSel = null;
        ShowPlanTab();
        if (CurrentStory == AllStories && cbStory.Items.Count > 1) cbStory.SelectedIndex = 1;
        if (planHost != null) planHost.Cursor = Cursors.Cross;
        SetStatus($"Đặt {it.Name}: rê chuột – tự bắt mặt tường (lưng tựa tường), góc phòng, giao trục, cạnh đồ đạc · Space xoay 90° · bấm để đặt · Esc dừng.");
    }

    private void StopPlaceFurniture()
    {
        if (_fnPlace == null) return;
        _fnPlace = null;
        if (planHost != null) planHost.Cursor = null;
        planOverlay?.Children.Clear();
        SetStatus(ModeHint(_mode));
    }

    private string FurnishStory() => CurrentStory == AllStories ? null : CurrentStory;

    private FurnitureSnapResult FurnishSnapAt(Point s)
    {
        var (x, y) = S2W(s);
        return FurnitureSnap.Snap(_p, FurnishStory(), x, y, _fnPlace.Width, _fnPlace.Depth, _fnRot);
    }

    /// <summary>Đa giác chữ nhật xoay (màn hình) của khối w × d tâm (x, y), góc rot.</summary>
    private PointCollection RectScreen(double x, double y, double w, double d, double rot)
    {
        double a = rot * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
        return new PointCollection(new[] { (-1.0, -1.0), (1.0, -1.0), (1.0, 1.0), (-1.0, 1.0) }
            .Select(q => W2S(x + c * q.Item1 * w / 2 - s * q.Item2 * d / 2, y + s * q.Item1 * w / 2 + c * q.Item2 * d / 2)));
    }

    /// <summary>Kích thước tạm (xanh) của khối: bấm số để sửa (edit != null).</summary>
    private void DrawRectDims(Canvas cv, List<RectDim> dims, Action<RectDim> edit)
    {
        foreach (var d in dims)
        {
            var a = W2S(d.X1, d.Y1); var b = W2S(d.X2, d.Y2);
            CLine(cv, a, b, RvBlue, 1, false);
            bool vert = Math.Abs(a.X - b.X) < 1;
            if (vert) { CLine(cv, new Point(a.X - 5, a.Y), new Point(a.X + 5, a.Y), RvBlue, 1, false); CLine(cv, new Point(b.X - 5, b.Y), new Point(b.X + 5, b.Y), RvBlue, 1, false); }
            else { CLine(cv, new Point(a.X, a.Y - 5), new Point(a.X, a.Y + 5), RvBlue, 1, false); CLine(cv, new Point(b.X, b.Y - 5), new Point(b.X, b.Y + 5), RvBlue, 1, false); }
            var tb = new TextBlock
            {
                Text = (d.Value * 1000).ToString("0", Inv), Foreground = RvBlue, FontSize = 11, Background = Brushes.White, Padding = new Thickness(2, 0, 2, 0),
                ToolTip = $"{d.RefKind} {d.RefName}" + (edit != null ? " – bấm để nhập khoảng cách mới (mm)" : "")
            };
            if (edit != null) { tb.TextDecorations = TextDecorations.Underline; tb.Cursor = Cursors.IBeam; var dd = d; tb.MouseLeftButtonDown += (s, e) => { e.Handled = true; edit(dd); }; }
            else tb.IsHitTestVisible = false;
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var m = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
            Canvas.SetLeft(tb, vert ? m.X + 3 : m.X - tb.DesiredSize.Width / 2);
            Canvas.SetTop(tb, vert ? m.Y - tb.DesiredSize.Height / 2 : m.Y - tb.DesiredSize.Height - 1);
            cv.Children.Add(tb);
        }
    }

    /// <summary>Di chuột: bóng đồ đạc + kích thước tạm, hoặc dây chun mặt cắt. true = đã xử lý.</summary>
    private bool FurnishPlanMove(Point s)
    {
        if (_p == null) return false;
        if (_secTool)
        {
            planOverlay.Children.Clear();
            if (_secStart is { } a)
            {
                var pa = W2S(a.X, a.Y);
                CLine(planOverlay, pa, s, RvBlue, 2, true);
            }
            AddText(_secStart == null ? "Mặt cắt: bấm điểm đầu" : "bấm điểm cuối (hướng nhìn: bên trái đường cắt)", new Point(s.X + 14, s.Y + 12), RvBlue, 11, false, false, planOverlay);
            return true;
        }
        if (_fnPlace == null) return false;
        planOverlay.Children.Clear();
        if (FurnishStory() == null) { AddText("Chọn một tầng cụ thể", new Point(s.X + 14, s.Y + 12), RvBlue, 11, false, false, planOverlay); return true; }
        var r = FurnishSnapAt(s);
        planOverlay.Children.Add(new Shapes.Polygon { Points = RectScreen(r.X, r.Y, _fnPlace.Width, _fnPlace.Depth, r.Rotation), Stroke = RvBlue, StrokeThickness = 1.5, Fill = RvBlueFill, IsHitTestVisible = false });
        DrawRectDims(planOverlay, TempDimensions.ForRect(_p, FurnishStory(), r.X, r.Y, _fnPlace.Width, _fnPlace.Depth, r.Rotation), null);
        AddText($"{_fnPlace.Name} · {r.Kind}", new Point(s.X + 14, s.Y + 12), RvBlue, 11, false, false, planOverlay);
        return true;
    }

    private FurnitureItem FurnitureAt(Point s)
    {
        string st = FurnishStory();
        if (_p?.Arch == null || st == null) return null;
        var (x, y) = S2W(s);
        foreach (var f in FurnitureSnap.StoryFurniture(_p, st).Reverse())
        {
            double a = -f.Rotation * Math.PI / 180, dx = x - f.X, dy = y - f.Y;
            double lx = dx * Math.Cos(a) - dy * Math.Sin(a), ly = dx * Math.Sin(a) + dy * Math.Cos(a);
            if (Math.Abs(lx) <= f.Width / 2 && Math.Abs(ly) <= f.Depth / 2) return f;
        }
        return null;
    }

    /// <summary>Bấm trên mặt bằng: đặt đồ đạc / chọn đồ đạc / điểm mặt cắt. true = đã xử lý.</summary>
    private bool FurnishPlanClick(Point s)
    {
        if (_p == null) return false;
        if (_secTool)
        {
            var (x, y) = S2W(s);
            var p1 = CurrentStory == AllStories ? (x, y) : SnapPlanXY(x, y);
            if (_secStart == null) { _secStart = p1; SetStatus("Mặt cắt: bấm điểm cuối."); return true; }
            var a = _secStart.Value;
            string snap = Snapshot();
            var sl = SectionView.Add(_p, a.X, a.Y, p1.Item1, p1.Item2);
            _secStart = null;
            if (sl == null) { SetStatus("Hai điểm trùng nhau – bấm lại."); return true; }
            PushUndo(snap); MarkDirty(false);
            StopSectionTool();
            RefreshBrowser();
            OpenSectionView(sl);
            return true;
        }
        if (_fnPlace != null)
        {
            if (FurnishStory() == null) { SetStatus("Chọn một tầng cụ thể để đặt đồ đạc."); return true; }
            var r = FurnishSnapAt(s);
            string snap = Snapshot();
            var f = FurnitureSnap.Place(_p, _fnPlace, FurnishStory(), r);
            PushUndo(snap);
            FurnishChanged();
            SetStatus($"Đã đặt {f.Name} ({r.Kind}) – bấm tiếp để đặt thêm, Space xoay, Esc dừng.");
            return true;
        }
        if (_mode != PlanEditMode.Select) return false;
        var hit = FurnitureAt(s);
        if (hit == null) { if (_fnSel != null) { _fnSel = null; RedrawPlan(); } return false; }
        _fnSel = hit;
        RedrawPlan();
        SetStatus($"Đã chọn {hit.Name}: bấm số kích thước tạm để sửa khoảng cách · R / Space xoay 90° · Delete xoá · Esc bỏ chọn.");
        return true;
    }

    private (double, double) SnapPlanXY(double x, double y)
    {
        try { var q = SnapPlan(CurrentStory, x, y); return (q.X, q.Y); } catch (Exception) { return (x, y); }
    }

    private bool FurnishPlanKey(Key key)
    {
        if (IsTextInputFocused()) return false;
        if (key == Key.Escape && _secTool) { StopSectionTool(); return true; }
        if (key == Key.Escape && _fnPlace != null) { StopPlaceFurniture(); return true; }
        if (_fnSel == null) return false;
        if (key == Key.Escape) { _fnSel = null; RedrawPlan(); SetStatus(ModeHint(_mode)); return true; }
        if (key == Key.Delete) { DeleteFurniture(_fnSel); _fnSel = null; return true; }
        if (key == Key.R) { RotateFurniture(_fnSel); return true; }
        return false;
    }

    /// <summary>Space: xoay bóng đang đặt / đồ đạc đang chọn 90°.</summary>
    private bool FurnishSpace()
    {
        if (_fnPlace != null) { _fnRot = FurnitureSnap.Norm(_fnRot + 90); if (_lastMouse != null) FurnishPlanMove(_lastMouse.Value); return true; }
        if (_fnSel != null) { RotateFurniture(_fnSel); return true; }
        return false;
    }

    private void RotateFurniture(FurnitureItem f)
    {
        string snap = Snapshot();
        f.Rotation = FurnitureSnap.Norm(f.Rotation + 90);
        PushUndo(snap);
        FurnishChanged();
    }

    private void DeleteFurniture(FurnitureItem f)
    {
        if (f == null || _p?.Arch == null) return;
        string snap = Snapshot();
        _p.Arch.Furniture.Remove(f);
        PushUndo(snap);
        FurnishChanged();
        SetStatus($"Đã xoá {f.Name}.");
    }

    private void FurnishChanged()
    {
        MarkDirty(false);
        RedrawPlan();
        try { Build3D(); } catch (Exception) { /* 3D chưa sẵn */ }
    }

    /// <summary>Tô sáng đồ đạc đang chọn + kích thước tạm sửa được (gọi từ DrawRevitSelection). true = đã vẽ.</summary>
    private bool DrawFurnitureSelection(Canvas layer)
    {
        if (_fnSel == null || _p?.Arch == null) return false;
        if (!_p.Arch.Furniture.Contains(_fnSel) || _fnSel.Story != FurnishStory()) { _fnSel = null; return false; }
        var f = _fnSel;
        layer.Children.Add(new Shapes.Polygon { Points = RectScreen(f.X, f.Y, f.Width, f.Depth, f.Rotation), Stroke = RvBlue, StrokeThickness = 2, Fill = RvBlueFill, IsHitTestVisible = false });
        DrawRectDims(layer, TempDimensions.ForRect(_p, f.Story, f.X, f.Y, f.Width, f.Depth, f.Rotation, true, f.Id), d => EditFurnitureDim(f, d));
        return true;
    }

    private void EditFurnitureDim(FurnitureItem f, RectDim d)
    {
        string v = DialogUi.InputBox(this, "Kích thước tạm", $"Khoảng cách từ mép {f.Name} tới {d.RefKind.ToLowerInvariant()} {d.RefName} (mm):", (d.Value * 1000).ToString("0", Inv));
        if (v == null) return;
        double mm = DialogUi.ParseNum(v, double.NaN);
        if (double.IsNaN(mm) || mm < 0) { SetStatus("Số không hợp lệ (≥ 0)."); return; }
        string snap = Snapshot();
        if (!TempDimensions.Apply(f, d, mm / 1000)) { SetStatus("Không đổi được khoảng cách."); return; }
        PushUndo(snap);
        FurnishChanged();
        SetStatus($"{f.Name}: khoảng cách tới {d.RefKind.ToLowerInvariant()} {d.RefName} = {mm:0} mm.");
    }

    // ------------------------------------------------------------------
    // Mặt cắt đứng
    // ------------------------------------------------------------------
    private void StartSectionTool()
    {
        if (_p == null) return;
        StopPlaceFurniture();
        ShowPlanTab();
        _secTool = true; _secStart = null;
        if (planHost != null) planHost.Cursor = Cursors.Cross;
        SetStatus("Mặt cắt: bấm 2 điểm trên mặt bằng – khung nhìn mặt cắt tự sinh kèm cao độ, kích thước tầng, trục (Esc dừng).");
    }

    private void StopSectionTool()
    {
        if (!_secTool) return;
        _secTool = false; _secStart = null;
        if (planHost != null) planHost.Cursor = null;
        planOverlay?.Children.Clear();
    }

    /// <summary>Mở khung nhìn mặt cắt (cửa sổ): hình DXF, chọn mặt cắt khác, xuất DXF.</summary>
    private Window OpenSectionView(SectionLine first = null, bool? foundation = null)
    {
        if (_p == null) return null;
        var all = SectionView.All(_p);
        if (all.Count == 0) { SetStatus("Chưa có lưới trục / cấu kiện để cắt."); return null; }
        var w = new Window { Title = "Mặt cắt công trình", Owner = this, Width = 980, Height = 680, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var dock = new DockPanel();
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6) };
        var cb = new ComboBox { Width = 140, ItemsSource = all.Select(s => "MẶT CẮT " + s.Name).ToList() };
        var bDxf = new Button { Content = "Xuất DXF…", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var bNew = new Button { Content = "Vẽ mặt cắt mới…", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var info = new TextBlock { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray };
        // kiến trúc: không thể hiện móng (cắt tại cốt sân); kết cấu: vẽ móng, cổ cột, bê tông lót theo kết quả thiết kế móng
        var chkFound = new CheckBox { Content = "Thể hiện móng", Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            IsChecked = foundation ?? first?.ShowFoundation ?? false, ToolTip = "Bỏ chọn: mặt cắt kiến trúc cắt tại cốt sân, không vẽ móng" };
        var chkMat = new CheckBox { Content = "Tô vật liệu", Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsChecked = true };
        bar.Children.Add(cb); bar.Children.Add(bDxf); bar.Children.Add(bNew); bar.Children.Add(chkFound); bar.Children.Add(chkMat); bar.Children.Add(info);
        DockPanel.SetDock(bar, Dock.Top);
        var img = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(10) };
        var host = new Border { Background = Brushes.White, Child = img };
        dock.Children.Add(bar); dock.Children.Add(host);
        w.Content = dock;
        SectionDrawing cur = null;
        double curK = 1, curY1 = 0, curH = 0;
        Rect curBounds = Rect.Empty;
        Story dragLv = null; double dragZ = 0;   // kéo đường cao độ trên mặt cắt (bấm gần đường cao độ ≤ 0,3 m)
        double? ModelZ(Point pt)
        {
            if (cur == null || curBounds.IsEmpty || img.ActualHeight < 1) return null;
            return LevelDragMath.ModelZFromView(pt.Y, img.ActualHeight, curBounds.Y, curBounds.Height, curY1, curH, curK);
        }
        void Show()
        {
            int i = Math.Max(0, cb.SelectedIndex);
            bool fnd = chkFound.IsChecked == true;
            if (!all[i].Auto) all[i].ShowFoundation = fnd;   // mặt cắt người dùng vẽ: lưu lựa chọn trong hồ sơ
            cur = SectionView.Draw(_p, all[i], 100, null, new SectionOptions { ShowFoundation = fnd, Materials = chkMat.IsChecked == true, Footings = _design?.Footings });
            var fw = cur.Figure.W; var e = fw.Extents;
            if (e.IsEmpty) { img.Source = null; info.Text = "Không có cấu kiện bị cắt."; return; }
            double k = 1.0 / Math.Max(fw.Scale, 1e-9);
            var d = SheetRender.Render(new[] { (fw, k, -e.X1 * k, -e.Y1 * k) }, 0, e.H * k, 0.1, x => x.GetPrimitives());
            img.Source = new DrawingImage(d);
            curK = k; curY1 = e.Y1; curH = e.H; curBounds = d.Bounds;
            info.Text = $"{cur.Items.Count} cấu kiện bị cắt · {cur.Levels.Count} cao độ · {cur.Axes.Count} trục" + (cur.Foundations.Count > 0 ? $" · {cur.Foundations.Count} khối móng" : "");
        }
        cb.SelectionChanged += (s, e) => Show();
        chkFound.Click += (s, e) => Show();
        chkMat.Click += (s, e) => Show();
        cb.SelectedIndex = first == null ? 0 : Math.Max(0, all.FindIndex(s => s.Name == first.Name));
        bDxf.Click += (s, e) =>
        {
            if (cur == null) return;
            var dlg = new SaveFileDialog { Filter = "DXF (*.dxf)|*.dxf", FileName = "MatCat_" + cur.Line.Name.Replace("-", "_") + ".dxf" };
            if (dlg.ShowDialog(w) != true) return;
            try { cur.Figure.W.Save(dlg.FileName); SetStatus("Đã xuất " + dlg.FileName); }
            catch (Exception ex) { MessageBox.Show(w, ex.Message, AppTitle); }
        };
        bNew.Click += (s, e) => { w.Close(); StartSectionTool(); };
        // kéo đường cao độ trên mặt cắt (giống mặt đứng): bắt 10 mm, kẹp mỗi tầng ≥ 0,5 m, một bước hoàn tác
        img.MouseLeftButtonDown += (s, e) =>
        {
            if (ModelZ(e.GetPosition(img)) is not double zz) return;
            dragLv = LevelDragMath.NearestStory(_p, zz, 0.3);
            if (dragLv != null) { dragZ = dragLv.Elevation; img.CaptureMouse(); info.Text = $"Kéo cao độ {dragLv.Name}: {dragZ:+0.000;-0.000} m – thả chuột để đặt."; }
        };
        img.MouseMove += (s, e) =>
        {
            if (dragLv == null || ModelZ(e.GetPosition(img)) is not double z) return;
            dragZ = LevelDragMath.Clamp(_p, dragLv.Name, z);
            info.Text = $"Kéo cao độ {dragLv.Name}: {dragZ:+0.000;-0.000} m – thả chuột để đặt.";
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
                AfterModelEdit($"Đã kéo cao độ {lv.Name} trên mặt cắt: {old:+0.000;-0.000} → {zNew:+0.000;-0.000} m.", snap);
            }
            catch (Exception ex) { MessageBox.Show(w, ex.Message, AppTitle); }
            Show();
        };
        w.Show();
        return w;
    }

    /// <summary>Trình duyệt dự án: mặt cắt ngang các tầng theo bộ môn + mặt cắt đứng (gọi cuối RefreshBrowser).</summary>
    private void AddLevelViewsToBrowser()
    {
        if (_rvBrowser == null || _p == null) return;
        var root = TI("Mặt cắt ngang tầng (theo bộ môn)", null, false);
        foreach (var g in LevelViews.Build(_p).GroupBy(v => v.Discipline))
        {
            var node = TI(DisciplineView.Title(g.Key), null, false);
            foreach (var v in g)
            {
                var vv = v;
                node.Items.Add(TI(v.Name, () =>
                {
                    SwitchDiscipline(vv.Discipline);
                    ShowPlanTab();
                    string target = vv.Story == "NEN" ? _p.Stories.OrderBy(s => s.Elevation).FirstOrDefault()?.Name : vv.Story;
                    if (target != null && cbStory.Items.Contains(target)) cbStory.SelectedItem = target;
                }));
            }
            root.Items.Add(node);
        }
        _rvBrowser.Items.Insert(Math.Min(1, _rvBrowser.Items.Count), root);
        var secs = TI("Mặt cắt", null, false);
        foreach (var s in SectionView.All(_p)) { var ss = s; secs.Items.Add(TI("MẶT CẮT " + s.Name + (s.Auto ? " (tự sinh)" : ""), () => OpenSectionView(ss))); }
        secs.Items.Add(TI("＋ Vẽ mặt cắt mới…", StartSectionTool));
        _rvBrowser.Items.Insert(Math.Min(2, _rvBrowser.Items.Count), secs);
    }

    // ------------------------------------------------------------------
    // 3D: đồ đạc + thiết bị điện nước, chọn, kéo, xoay, ẩn / cô lập, hộp cắt
    // ------------------------------------------------------------------
    private bool Show3D(string id, Discipline d) => (_p == null || DisciplineView.Visible(_p, d)) && _v3Filter.Visible(id) && !SectionBoxHides(id);

    /// <summary>Hộp cắt: ẩn đối tượng của các tầng cao hơn tầng đang xem.</summary>
    private bool SectionBoxHides(string id)
    {
        if (!_v3SectionBox || _p == null || CurrentStory == AllStories) return false;
        double zc = _p.Stories.FirstOrDefault(s => s.Name == CurrentStory)?.Elevation ?? double.MaxValue;
        var sol = _v3Cache?.FirstOrDefault(q => q.Id == id);
        if (sol == null) return false;
        return sol.Box().Min.Z > zc + 1e-6;
    }

    private List<Solid> _v3Cache;

    /// <summary>Khối đồ đạc (DD-) và điện nước vào cảnh 3D chính (gọi trong BuildScene trước lớp phủ).</summary>
    private void AddExtras3D(Model3DGroup group, Dictionary<GeometryModel3D, PickMesh> pick, bool dim)
    {
        if (_p == null) return;
        Ensure3DBar();
        try { _v3Cache = ModelSolids.Build(_p, null, false); } catch (Exception) { _v3Cache = new List<Solid>(); }
        var furn = new PickMesh(); var mep = new PickMesh(); var sel = new PickMesh();
        foreach (var so in _v3Cache)
        {
            bool isFurn = so.Id.StartsWith("DD-"), isMep = so.Discipline == Discipline.DienNuoc && !isFurn;
            if (!isFurn && !isMep) continue;
            if (!Show3D(so.Id, so.Discipline)) continue;
            var pm = so.Id == _v3SelId || HvacSame3D(so.Id) ? sel : isMep ? mep : furn;
            pm.Begin(so);
            AddPrism(pm.Mesh, so);
        }
        // rãnh điện nước chôn tường (khối mảnh màu cam) – chỉ hiển thị, không chọn
        var chase = new PickMesh();
        try
        {
            if (DisciplineView.Visible(_p, Discipline.DienNuoc))
                foreach (var so in KetCauVXT.Mep.WallChases.Solids(_p)) { chase.Begin(so); AddPrism(chase.Mesh, so); }
        }
        catch (Exception) { /* mô hình điện nước chưa đủ dữ liệu */ }
        byte a = (byte)(dim ? 50 : 255);
        AddPickModel(group, pick, furn, Diffuse(Color.FromArgb(a, 205, 170, 120)), dim);
        AddPickModel(group, pick, mep, Diffuse(Color.FromArgb(a, 40, 140, 220)), dim);
        AddModel(group, chase.Mesh, Color.FromArgb(a, 240, 140, 40), true);
        AddPickModel(group, pick, sel, SelectedMaterial3D(), true);
    }

    /// <summary>Lăng trụ (đáy đa giác lồi / chữ nhật) vào lưới tam giác.</summary>
    private static void AddPrism(MeshGeometry3D m, Solid so)
    {
        var (b, t) = so.Rings();
        int n = b.Count;
        if (n < 3) return;
        Point3D P(Vec3 v) => new(v.X, v.Y, v.Z);
        if (n == 4) { AddHexa(m, b.Select(P).ToArray(), t.Select(P).ToArray()); return; }
        int i0 = m.Positions.Count;
        foreach (var v in b) m.Positions.Add(P(v));
        foreach (var v in t) m.Positions.Add(P(v));
        void Tri(int x, int y, int z) { m.TriangleIndices.Add(i0 + x); m.TriangleIndices.Add(i0 + y); m.TriangleIndices.Add(i0 + z); m.TriangleIndices.Add(i0 + x); m.TriangleIndices.Add(i0 + z); m.TriangleIndices.Add(i0 + y); }
        for (int i = 1; i + 1 < n; i++) { Tri(0, i, i + 1); Tri(n, n + i, n + i + 1); }
        for (int i = 0; i < n; i++) { int j = (i + 1) % n; Tri(i, j, n + j); Tri(i, n + j, n + i); }
    }

    /// <summary>Chọn khối đồ đạc / thiết bị trong 3D.</summary>
    private void Select3DSolid(Solid so)
    {
        if (HasSelection) ClearSelection();
        _v3SelId = so?.Id;
        Build3D();
        Update3DBar();
        if (so != null) SetStatus($"3D: đã chọn {so.Name} ({so.Category}) – kéo để dời trên mặt sàn · Xoay 90° · Cao độ… · Ẩn / Cô lập · Delete xoá.");
    }

    private FurnitureItem Sel3DFurniture()
    {
        if (_v3SelId == null || !_v3SelId.StartsWith("DD-") || _p?.Arch == null) return null;
        return int.TryParse(_v3SelId.Substring(3), out int id) ? _p.Arch.Furniture.FirstOrDefault(f => f.Id == id) : null;
    }

    private KetCauVXT.Mep.MepDevice Sel3DDevice()
    {
        if (_v3SelId == null || !_v3SelId.StartsWith("DN-TB-") || _p?.Mep == null) return null;
        return int.TryParse(_v3SelId.Substring(6), out int id) ? _p.Mep.Devices.FirstOrDefault(d => d.Id == id) : null;
    }

    private Ray3 CameraRayAt(Point pt)
    {
        var c = camera3D;
        var pos = new Vec3(c.Position.X, c.Position.Y, c.Position.Z);
        var look = new Vec3(c.LookDirection.X, c.LookDirection.Y, c.LookDirection.Z);
        var up = new Vec3(c.UpDirection.X, c.UpDirection.Y, c.UpDirection.Z);
        return Pick3DMath.CameraRay(pos, look, up, c.FieldOfView, viewport3D.ActualWidth, viewport3D.ActualHeight, pt.X, pt.Y);
    }

    /// <summary>Bấm chuột trái trong 3D chính: bắt đầu kéo đồ đạc đang chọn. true = đã xử lý (không xoay camera).</summary>
    private bool View3DDragStart(object sender, MouseButtonEventArgs e)
    {
        if (!ReferenceEquals(sender, view3DHost) || _p == null) return false;
        var f = Sel3DFurniture();
        var dev = f == null ? Sel3DDevice() : null;
        if (f == null && dev == null) return HvacDrag3DStart(e);   // ĐHKK – MainWindow.HvacDrag.cs
        var pt = e.GetPosition(viewport3D);
        var hit = Pick3D(viewport3D, pt, _pick3D) as Solid;
        if (hit == null || hit.Id != _v3SelId) return false;
        _v3Drag = true; _v3Moved = false;
        if (dev != null)
        {
            // thiết bị điện nước: kéo trên mặt phẳng ngang qua tâm thiết bị – gắn tường thì trượt dọc tường chủ, tự do thì bắt bước 5 cm
            var pos = KetCauVXT.Mep.MepRouting.DevicePos(_p, dev);
            _v3DragDev = dev.Id;
            _v3DragRay = CameraRayAt(pt);
            _v3X0 = pos.X; _v3Y0 = pos.Y; _v3Z = pos.Z;
            _v3DragSnap = Snapshot();
            view3DHost.CaptureMouse();
            e.Handled = true;
            return true;
        }
        _v3DragDev = 0;
        _v3DragRay = CameraRayAt(pt);
        _v3X0 = f.X; _v3Y0 = f.Y; _v3Z = ModelSolids.StoryZ(_p, f.Story) + f.Elevation;
        _v3DragSnap = Snapshot();
        view3DHost.CaptureMouse();
        e.Handled = true;
        return true;
    }

    private bool View3DDragMove(MouseEventArgs e)
    {
        if (!_v3Drag) return false;
        if (_v3HvId > 0) return HvacDrag3DMove(e);   // ĐHKK – MainWindow.HvacDrag.cs
        if (_v3DragDev > 0)
        {
            var dev = _p.Mep?.Devices.FirstOrDefault(d => d.Id == _v3DragDev);
            if (dev == null) { _v3Drag = false; return false; }
            var qd = Pick3DMath.Drag(_v3DragRay, CameraRayAt(e.GetPosition(viewport3D)), _v3Z, _v3X0, _v3Y0);
            if (qd == null) return true;
            if (!KetCauVXT.Mep.MepDeviceDragMath.Apply(_p, dev, qd.Value.X, qd.Value.Y, 0.05)) return true;
            foreach (var run in _p.Mep.Runs.Where(x => x.FromId == dev.Id || x.ToId == dev.Id)) KetCauVXT.Mep.MepRouting.Route(_p, run);
            _v3Moved = true;
            Build3D();
            SetStatus(dev.WallId > 0 ? $"Kéo {dev.Family} dọc tường {dev.WallId}: cách đầu tường {dev.Offset:0.00} m" : $"Kéo {dev.Family}: ({dev.X:0.00}; {dev.Y:0.00})");
            return true;
        }
        var f = Sel3DFurniture();
        if (f == null) { _v3Drag = false; return false; }
        var q = Pick3DMath.Drag(_v3DragRay, CameraRayAt(e.GetPosition(viewport3D)), _v3Z, _v3X0, _v3Y0);
        if (q == null) return true;
        var r = FurnitureSnap.Snap(_p, f.Story, q.Value.X, q.Value.Y, f.Width, f.Depth, f.Rotation, true, f.Id, 0.3);
        if (Math.Abs(r.X - f.X) + Math.Abs(r.Y - f.Y) < 1e-4) return true;
        f.X = r.X; f.Y = r.Y; f.Rotation = r.Rotation;
        _v3Moved = true;
        Build3D();
        SetStatus($"Kéo {f.Name}: ({f.X:0.000}; {f.Y:0.000}) · {r.Kind}");
        return true;
    }

    private bool View3DDragEnd()
    {
        if (!_v3Drag) return false;
        _v3Drag = false;
        view3DHost.ReleaseMouseCapture();
        if (_v3Moved && _v3DragDev > 0) { try { KetCauVXT.Mep.MepUpdater.Reconcile(_p); } catch (Exception) { } }
        HvacDrag3DEnd();   // ĐHKK – MainWindow.HvacDrag.cs
        _v3DragDev = 0;
        if (_v3Moved && _v3DragSnap != null) { PushUndo(_v3DragSnap); MarkDirty(false); RedrawPlan(); }
        _v3DragSnap = null;
        return true;
    }

    /// <summary>Thanh lệnh nổi trên khung 3D: xoay, cao độ, xoá, ẩn, cô lập, hiện tất cả, hộp cắt.</summary>
    private void Ensure3DBar()
    {
        if (_v3Bar != null || view3DHost?.Child is not Grid g) return;
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        Button B(string t, string tip, Action a)
        {
            var b = new Button { Content = t, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 4, 0), ToolTip = tip, Focusable = false };
            b.Click += (s, e) => { try { a(); } catch (Exception ex) { ShowError(t, ex); } };
            sp.Children.Add(b);
            return b;
        }
        B("Xoay 90°", "Xoay đồ đạc đang chọn (phím R / Space)", () => { var f = Sel3DFurniture(); if (f != null) RotateFurniture(f); });
        B("Cao độ…", "Cao độ đáy đồ đạc so với sàn / chiều cao lắp thiết bị điện nước (m)", Edit3DHeight);
        B("Xoá", "Xoá đồ đạc / thiết bị đang chọn (Delete)", Delete3DSelected);
        B("Ẩn", "Ẩn đối tượng đang chọn", () => { var id = Current3DId(); if (id != null) { _v3Filter.Hide(new[] { id }); _v3SelId = null; Build3D(); } });
        B("Cô lập", "Chỉ hiện đối tượng đang chọn", () => { var id = Current3DId(); if (id != null) { _v3Filter.Isolate(new[] { id }); Build3D(); } });
        B("Hiện tất cả", "Bỏ ẩn / bỏ cô lập", () => { _v3Filter.Reset(); Build3D(); });
        var box = new CheckBox { Content = "Hộp cắt (tới tầng đang xem)", Foreground = Brushes.WhiteSmoke, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        box.Checked += (s, e) => { _v3SectionBox = true; Build3D(); };
        box.Unchecked += (s, e) => { _v3SectionBox = false; Build3D(); };
        sp.Children.Add(box);
        _v3Bar = new Border { Child = sp, Background = new SolidColorBrush(Color.FromArgb(150, 30, 34, 40)), Padding = new Thickness(4), Margin = new Thickness(8, 32, 8, 6), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        g.Children.Add(_v3Bar);
        EnsureBox3DToggle();
        view3DHost.Focusable = true;
        view3DHost.PreviewKeyDown += (s, e) =>
        {
            if (IsTextInputFocused()) return;
            if (e.Key == Key.Delete && (Sel3DFurniture() != null || Sel3DDevice() != null)) { Delete3DSelected(); e.Handled = true; }
            else if ((e.Key == Key.R || e.Key == Key.Space) && Sel3DFurniture() is { } f) { RotateFurniture(f); e.Handled = true; }
            else if (e.Key == Key.Escape && _v3SelId != null) { Select3DSolid(null); e.Handled = true; }
        };
    }

    private void Update3DBar() { Ensure3DBar(); }

    /// <summary>Id khối đang chọn trong 3D (đồ đạc / thiết bị) hoặc cấu kiện kết cấu đang chọn.</summary>
    private string Current3DId()
    {
        if (_v3SelId != null) return _v3SelId;
        if (_sel != null) return (_sel.IsVerticalKind ? "COT-" : "DAM-") + _sel.Id;
        if (_selSlab != null) return "SAN-" + _selSlab.Id;
        return null;
    }

    private void Edit3DHeight()
    {
        var f = Sel3DFurniture();
        var d = Sel3DDevice();
        if (f == null && d == null) { SetStatus("Chọn đồ đạc / thiết bị trong 3D trước."); return; }
        double cur = f?.Elevation ?? d.Height;
        string v = DialogUi.InputBox(this, "Cao độ", f != null ? $"Cao độ đáy {f.Name} so với sàn (m):" : $"Chiều cao lắp {d.Family} so với sàn (m):", cur.ToString("0.###", Inv));
        if (v == null) return;
        double h = DialogUi.ParseNum(v, double.NaN);
        if (double.IsNaN(h) || h < 0) { SetStatus("Số không hợp lệ."); return; }
        string snap = Snapshot();
        if (f != null) f.Elevation = h;
        else { d.Height = h; try { KetCauVXT.Mep.MepRouting.RebuildAll(_p); } catch (Exception) { /* tuyến dựng lại sau */ } }
        PushUndo(snap);
        FurnishChanged();
    }

    private void Delete3DSelected()
    {
        var f = Sel3DFurniture();
        if (f != null) { DeleteFurniture(f); _v3SelId = null; Build3D(); return; }
        var d = Sel3DDevice();
        if (d == null) { HvacDelete3D(); return; }   // ĐHKK – MainWindow.HvacDrag.cs
        string snap = Snapshot();
        _p.Mep.Devices.Remove(d);
        _p.Mep.Runs.RemoveAll(r => r.FromId == d.Id || r.ToId == d.Id);
        try { KetCauVXT.Mep.MepRouting.RebuildAll(_p); } catch (Exception) { /* bỏ qua */ }
        PushUndo(snap);
        _v3SelId = null;
        FurnishChanged();
    }

    // ------------------------------------------------------------------
    // Tab "Đặt & xem" (ribbon Revit) + menu
    // ------------------------------------------------------------------
    private void InitFurnish()
    {
        try
        {
            if (_rvTabs != null)
                RvTab("Đặt & xem",
                    RvGroup("Đặt",
                        RvBtn("Đặt đồ đạc…", KetCauVXT.Ui.UiGlyphs.For("Đặt đồ đạc"), "Chọn đồ đạc / thiết bị trong thư viện 3D rồi rê chuột: tự bắt mặt tường, góc phòng, giao trục (không nhập toạ độ)", PickFurnitureToPlace)),
                    RvGroup("Khung nhìn",
                        RvBtn("Mặt cắt", KetCauVXT.Ui.UiGlyphs.For("Mặt cắt"), "Vẽ mặt cắt đứng bằng 2 điểm – tự ghi cao độ, kích thước tầng, trục", StartSectionTool),
                        RvBtn("Xem mặt cắt", KetCauVXT.Ui.UiGlyphs.For("Xem mặt cắt"), "Mặt cắt 1-1, 2-2 tự sinh + mặt cắt đã vẽ", () => OpenSectionView())));
            if (mainMenu != null)
            {
                var m = new MenuItem { Header = "Đặt & _xem" };
                void Add(string h, Action a) { var mi = new MenuItem { Header = h }; mi.Click += (s, e) => { try { a(); } catch (Exception ex) { ShowError(h.Replace("_", ""), ex); } }; m.Items.Add(mi); }
                Add("Đặt đồ đạc từ thư viện (bắt điểm)…", PickFurnitureToPlace);
                Add("Vẽ _mặt cắt (2 điểm)", StartSectionTool);
                Add("Xem mặt cắt công trình…", () => OpenSectionView());
                int idx = Math.Max(0, mainMenu.Items.Count - 2);
                mainMenu.Items.Insert(idx, m);
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Đặt & xem: " + ex.Message); }
    }

    /// <summary>Chọn mục thư viện rồi chuyển sang công cụ đặt bằng bắt điểm.</summary>
    private void PickFurnitureToPlace()
    {
        var items = Library3D.LoadAll();
        if (items.Count == 0) { OpenLibrary3D(); return; }
        var w = new Window { Title = "Chọn đồ đạc / thiết bị để đặt", Owner = this, Width = 460, Height = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var list = new ListBox { ItemsSource = items.Select(i => $"{i.Category} – {i.Name} ({i.Width:0.00}×{i.Depth:0.00}×{i.Height:0.00} m)").ToList(), Margin = new Thickness(8) };
        var ok = new Button { Content = "Đặt (bắt điểm)", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true };
        var dock = new DockPanel();
        DockPanel.SetDock(ok, Dock.Bottom);
        dock.Children.Add(ok); dock.Children.Add(list);
        w.Content = dock;
        LibraryItem chosen = null;
        void Go() { if (list.SelectedIndex >= 0) { chosen = items[list.SelectedIndex]; w.Close(); } }
        ok.Click += (s, e) => Go();
        list.MouseDoubleClick += (s, e) => Go();
        w.ShowDialog();
        if (chosen != null) StartPlaceFurniture(chosen);
    }
}

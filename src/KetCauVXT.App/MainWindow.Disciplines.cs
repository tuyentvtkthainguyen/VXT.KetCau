using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using KetCauVXT.Model;
using KetCauVXT.Import;
using KetCauVXT.Export;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// BỘ MÔN + BIM (chế độ Thiết kế):
//   • công tắc bộ môn Kiến trúc | Kết cấu | Điện nước ở đầu ribbon – mỗi bộ môn hiện tab của mình, bộ môn khác vẽ mờ (xref);
//   • tab "BIM": cao độ từ móng đến mái (trình tạo, chèn / xoá tầng, sàn nền, sàn mọi ô), kiến trúc (tìm phòng, chuyển sang kết cấu,
//     thư viện 3D), phối hợp (bộ môn hiển thị, kiểm tra va chạm), trao đổi (IFC, SketchUp DAE/OBJ, nhập IFC, cập nhật từ Revit),
//     hồ sơ theo bộ môn (KT-, KC-, DN-);
//   • menu "BIM" tương ứng cho giao diện cổ điển.
// Lõi: Model/Disciplines.cs, Levels.cs, ArchModel.cs, ModelSolids.cs, Clash.cs, Library3D.cs, Export/IfcWriter.cs, MeshExport.cs,
// Import/IfcImporter.cs, RevitArchImport.cs, Drawing/DisciplineDocs.cs.
// =====================================================================
public partial class MainWindow
{
    private StackPanel _dsSwitch;
    private readonly Dictionary<Discipline, ToggleButtonLike> _dsButtons = new();

    /// <summary>Nút bật kiểu radio (Button + trạng thái) – tránh phụ thuộc kiểu ToggleButton của ribbon.</summary>
    private sealed class ToggleButtonLike
    {
        public Button B;
        public void Set(bool on)
        {
            B.Background = on ? new SolidColorBrush(Color.FromRgb(204, 228, 247)) : Brushes.Transparent;
            B.BorderBrush = on ? new SolidColorBrush(Color.FromRgb(0, 120, 215)) : Brushes.Transparent;
            B.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private sealed class LibRow { public LibraryItem I { get; set; } public string Text { get; set; } = ""; }

    /// <summary>Gọi sau InitRevitUi.</summary>
    private void InitDisciplines()
    {
        try
        {
            if (_rvTabs != null && _rvRibbon != null)
            {
                _dsSwitch = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center };
                _dsSwitch.Children.Add(new TextBlock { Text = "BỘ MÔN", FontSize = 10, Foreground = Brushes.DimGray, HorizontalAlignment = HorizontalAlignment.Center });
                foreach (var d in DisciplineView.All)
                {
                    var dd = d;
                    var b = new Button { Content = DisciplineView.Title(d), Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 1, 0, 0), Focusable = false, ToolTip = $"Làm việc ở bộ môn {DisciplineView.Title(d)} – bộ môn khác hiện mờ như xref" };
                    b.Click += (s, e) => SwitchDiscipline(dd);
                    _dsButtons[d] = new ToggleButtonLike { B = b };
                    _dsSwitch.Children.Add(b);
                }
                var host = new DockPanel();
                var sepBorder = new Border { Child = _dsSwitch, BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)), BorderThickness = new Thickness(0, 0, 1, 0) };
                DockPanel.SetDock(sepBorder, Dock.Left);
                _rvRibbon.Child = null;
                host.Children.Add(sepBorder);
                host.Children.Add(_rvTabs);
                _rvRibbon.Child = host;
                BuildBimTab();
            }
            BuildBimMenu();
            UpdateDisciplineButtons();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Bộ môn: " + ex.Message); }
    }

    private void UpdateDisciplineButtons()
    {
        var cur = _p?.ActiveDiscipline ?? Discipline.KetCau;
        foreach (var kv in _dsButtons) kv.Value.Set(kv.Key == cur);
    }

    /// <summary>Tab ribbon của từng bộ môn (theo tiêu đề tab). Tab dùng chung: Xem, Quản lý, BIM.</summary>
    private static bool TabBelongs(string header, Discipline d)
    {
        string h = header ?? "";
        if (h is "Xem" or "Quản lý" or "BIM" || h.StartsWith("Sửa |")) return true;
        return d switch
        {
            Discipline.KienTruc => h == "Kiến trúc",
            Discipline.KetCau => h is "Kết cấu" or "Tải trọng" or "Phân tích",
            _ => h.Contains("Điện") || h.Contains("Nước") || h.Contains("nước") || h.Contains("MEP")
        };
    }

    private void SwitchDiscipline(Discipline d)
    {
        if (_p == null) return;
        _p.ActiveDiscipline = d;
        UpdateDisciplineButtons();
        if (_rvTabs != null)
        {
            TabItem first = null;
            foreach (var ti in _rvTabs.Items.OfType<TabItem>())
            {
                bool vis = TabBelongs(ti.Header as string, d);
                ti.Visibility = vis ? Visibility.Visible : Visibility.Collapsed;
                if (vis && first == null && (ti.Header as string) is not ("Xem" or "Quản lý" or "BIM")) first = ti;
            }
            if (first != null) _rvTabs.SelectedItem = first;
            else _rvTabs.SelectedItem = _rvTabs.Items.OfType<TabItem>().FirstOrDefault(t => t.Visibility == Visibility.Visible);
        }
        RedrawPlan();
        SetStatus($"Bộ môn {DisciplineView.Title(d)}: bộ môn khác hiện mờ (tham chiếu) – BIM → Bộ môn hiển thị để bật / tắt.");
    }

    /// <summary>Đặt độ mờ / ẩn theo bộ môn cho các phạm vi phần tử vừa vẽ trên mặt bằng.
    /// [a,b): sàn + lỗ sàn (kết cấu), [b,c): trục (giữ), [c,d): dầm (kết cấu), [d,e): tường + kiến trúc, [e,f): cột (kết cấu).</summary>
    private void ApplyDisciplineOpacity(int a, int b, int c, int d, int e, int f)
    {
        if (_p == null) return;
        double so = DisciplineView.Opacity(_p, Discipline.KetCau), ao = DisciplineView.Opacity(_p, Discipline.KienTruc);
        if (so >= 0.999 && ao >= 0.999) return;
        void Range(int i0, int i1, double op)
        {
            for (int i = Math.Max(0, i0); i < Math.Min(i1, planCanvas.Children.Count); i++)
            {
                if (planCanvas.Children[i] is not FrameworkElement fe) continue;
                if (fe.Tag as string == "DN") continue;   // thiết bị điện nước tự đặt độ mờ
                if (op <= 0.001) fe.Visibility = Visibility.Collapsed;
                else { fe.Opacity = op; fe.IsHitTestVisible = op >= 0.999; }
            }
        }
        Range(a, b, so);
        Range(c, d, so);
        Range(d, e, ao);
        Range(e, f, so);
    }

    private bool OnStory(string story) =>
        CurrentStory == AllStories || story == CurrentStory ||
        (story == "NEN" && _p.Stories.OrderBy(s => s.Elevation).FirstOrDefault()?.Name == CurrentStory);

    /// <summary>Phòng (đường bao nét đứt + tên, diện tích), đồ đạc (khối), thiết bị điện nước (khối, theo độ mờ bộ môn).</summary>
    private void DrawArchExtras(bool labels)
    {
        if (_p?.Arch == null) return;
        DrawArchDetails(labels);   // mái, lan can, phào chỉ, ốp – MainWindow.ArchDetail.cs
        foreach (var r in _p.Arch.Rooms.Where(r => OnStory(r.Story) && r.Outline.Count >= 3))
        {
            var pts = new PointCollection(r.Outline.Select(q => W2S(q[0], q[1])));
            planCanvas.Children.Add(new Shapes.Polygon { Points = pts, Stroke = new SolidColorBrush(Color.FromRgb(150, 110, 60)), StrokeThickness = 0.8, StrokeDashArray = new DoubleCollection { 4, 3 }, Fill = Brushes.Transparent, IsHitTestVisible = false });
            if (labels)
            {
                var c = W2S(r.Center.X, r.Center.Y);
                AddText($"{r.Name} ({r.Area:0.0} m²)", c, new SolidColorBrush(Color.FromRgb(120, 80, 30)), 10, true, true);
            }
        }
        double dn = DisciplineView.Opacity(_p, Discipline.DienNuoc);
        foreach (var f in _p.Arch.Furniture.Where(f => OnStory(f.Story)))
        {
            double ang = f.Rotation * Math.PI / 180, cs = Math.Cos(ang), sn = Math.Sin(ang);
            var corners = new[] { (-1.0, -1.0), (1.0, -1.0), (1.0, 1.0), (-1.0, 1.0) }
                .Select(q => W2S(f.X + cs * q.Item1 * f.Width / 2 - sn * q.Item2 * f.Depth / 2, f.Y + sn * q.Item1 * f.Width / 2 + cs * q.Item2 * f.Depth / 2));
            bool mep = f.Discipline == Discipline.DienNuoc;
            var poly = new Shapes.Polygon
            {
                Points = new PointCollection(corners), StrokeThickness = 0.9,
                Stroke = mep ? new SolidColorBrush(Color.FromRgb(0, 110, 200)) : new SolidColorBrush(Color.FromRgb(140, 100, 60)),
                Fill = mep ? new SolidColorBrush(Color.FromArgb(40, 0, 110, 200)) : new SolidColorBrush(Color.FromArgb(30, 160, 120, 70)),
                ToolTip = $"{f.Name} – {f.Category} ({DisciplineView.Title(f.Discipline)})", Tag = mep ? "DN" : null
            };
            if (mep)
            {
                if (dn <= 0.001) continue;
                poly.Opacity = dn; poly.IsHitTestVisible = dn >= 0.999;
            }
            planCanvas.Children.Add(poly);
            // ký hiệu chi tiết: họ tham số → các chi tiết (mặt bàn, chân, đầu giường…); thư viện → đường bao hình chiếu bằng
            Point Loc(double u, double v) => W2S(f.X + cs * u - sn * v, f.Y + sn * u + cs * v);
            var symBr = poly.Stroke;
            var fam = FurnitureFamilies.Find(f.Family);
            if (fam != null)
            {
                foreach (var pt in fam.Parts(Math.Max(0.1, f.Width), Math.Max(0.1, f.Depth), Math.Max(0.1, f.Height)))
                    planCanvas.Children.Add(new Shapes.Polygon
                    {
                        Points = new PointCollection { Loc(pt.X0, pt.Y0), Loc(pt.X1, pt.Y0), Loc(pt.X1, pt.Y1), Loc(pt.X0, pt.Y1) },
                        Stroke = symBr, StrokeThickness = 0.6, IsHitTestVisible = false, Opacity = mep ? dn : 1
                    });
            }
            else if (f.PlanSymbol != null)
                foreach (var loop in f.PlanSymbol.Where(l => l.Count >= 3))
                    planCanvas.Children.Add(new Shapes.Polygon
                    {
                        Points = new PointCollection(loop.Select(q => Loc(q[0] * f.Width, q[1] * f.Depth))),
                        Stroke = symBr, StrokeThickness = 0.7, IsHitTestVisible = false, Opacity = mep ? dn : 1
                    });
        }
    }

    // ==================================================================
    // Tab BIM + menu BIM
    // ==================================================================
    private void BuildBimTab()
    {
        RvTab("BIM",
            RvGroup("Cao độ",
                RvBtn("Lưới trục + cột + tầng…", "", "Một bảng: lưới trục mặt bằng, lưới cột (giao điểm có / không cột), chiều cao từng tầng – đổi chiều cao tầng giữ nguyên mô hình", OpenGridColumnDialog),
                RvBtn("Tạo cao độ tầng…", "", "Móng → nền tầng 1 → sàn tầng 2, 3… → mái (tum): số tầng, chiều cao, chiều sâu móng, sinh khung + sàn", OpenLevelWizard),
                RvBtn("Chèn tầng…", "", "Chèn một tầng phía trên tầng đang xem (sao dầm, sàn, nối cột), nâng các tầng trên", InsertStoryUi),
                RvBtn("Xoá tầng", "", "Xoá tầng đang xem (dầm, sàn, tường), nối cột, hạ các tầng trên", DeleteStoryUi),
                RvBtn("Sàn mọi ô", "", "Tạo ô sàn cho mọi ô lưới còn thiếu ở tầng đang xem (hoặc mọi tầng)", SlabsAllBaysUi),
                RvBtn("Sàn nền", "", "Sàn nền tầng 1 trên đất theo ô lưới (không tải lên khung)", GroundSlabsUi)),
            RvGroup("Kiến trúc",
                RvBtn("Tìm phòng", "", "Tạo phòng từ mạng tường tầng đang xem (tên, diện tích)", DetectRoomsUi),
                RvBtn("Đặt tên phòng…", "", "Đổi tên các phòng tầng đang xem (tên quyết định công năng sàn)", RenameRoomsUi),
                RvBtn("Kiến trúc → kết cấu", "", "Tường → tải dầm / sàn, phòng → công năng sàn, cửa → lanh tô, giao tường → đề xuất cột", ArchToStructureUi),
                RvBtn("Thư viện 3D…", "", "Thư viện đồ đạc / thiết bị: nhập từ SketchUp (OBJ, DAE) hoặc Revit, đặt vào mô hình", OpenLibrary3D)),
            ArchDetailGroup(),
            RvGroup("Phối hợp",
                RvBtn("Bộ môn hiển thị…", "", "Bật / tắt, vẽ mờ bộ môn tham chiếu", OpenDisciplineDisplay),
                RvBtn("Kiểm tra va chạm", "", "Va chạm giữa kiến trúc, kết cấu, điện nước (cứng, khe hở, ống lồng)", RunClashUi)),
            RvGroup("Trao đổi",
                RvBtn("Xuất IFC…", "", "IFC2X3 cho Revit, SketchUp Pro, Navisworks (chọn bộ môn)", () => ExportBim("ifc")),
                RvBtn("Xuất SketchUp…", "", "COLLADA (.dae) – SketchUp: File → Import", () => ExportBim("dae")),
                RvBtn("Xuất OBJ…", "", "OBJ + MTL (đơn vị m)", () => ExportBim("obj")),
                RvBtn("Nhập IFC…", "", "Nhập cột, dầm, sàn, tường, cửa, phòng từ IFC (Revit → Export → IFC)", ImportIfcUi),
                RvBtn("Cập nhật từ Revit…", "", "So khớp *.vxtrevit.json theo UniqueId: thêm / sửa / xoá tường, thiết bị điện nước", UpdateFromRevitUi)),
            RvGroup("Hồ sơ",
                RvBtn("Hồ sơ theo bộ môn…", "", "Xuất bản vẽ Kiến trúc (KT-), Kết cấu (KC-), Điện nước (DN-) – bộ môn khác làm nền mờ", ExportDisciplineDocsUi)));
    }

    private void BuildBimMenu()
    {
        if (mainMenu == null) return;
        var m = new MenuItem { Header = "_BIM" };
        void Add(string h, Action a) { var mi = new MenuItem { Header = h }; mi.Click += (s, e) => { try { a(); } catch (Exception ex) { ShowError(h.Replace("_", ""), ex); } }; m.Items.Add(mi); }
        var ds = new MenuItem { Header = "Bộ _môn đang làm việc" };
        foreach (var d in DisciplineView.All)
        {
            var dd = d;
            var mi = new MenuItem { Header = DisciplineView.Title(d) };
            mi.Click += (s, e) => SwitchDiscipline(dd);
            ds.Items.Add(mi);
        }
        m.Items.Add(ds);
        Add("Bộ môn _hiển thị…", OpenDisciplineDisplay);
        m.Items.Add(new Separator());
        Add("Tạo _cao độ tầng (móng → mái)…", OpenLevelWizard);
        Add("Chèn tầng…", InsertStoryUi);
        Add("Xoá tầng đang xem", DeleteStoryUi);
        Add("Sàn mọi ô lưới", SlabsAllBaysUi);
        Add("Sàn nền tầng 1", GroundSlabsUi);
        m.Items.Add(new Separator());
        Add("Tìm _phòng từ tường", DetectRoomsUi);
        Add("Đặt tên phòng…", RenameRoomsUi);
        Add("Kiến trúc → _kết cấu", ArchToStructureUi);
        Add("Thư viện _3D…", OpenLibrary3D);
        Add("Mái theo đường bao…", RoofUi);
        Add("Trần theo phòng…", CeilingsUi);
        Add("Vẽ lan can", RailingUi);
        Add("Phào chỉ / gờ trang trí…", SweepUi);
        Add("Ốp / sơn mặt tường…", CladdingUi);
        Add("Chép kiến trúc sang tầng khác…", CopyArchUi);
        Add("Khối lượng hoàn thiện kiến trúc…", ArchQtyUi);
        Add("Nhập mô hình SketchUp / Revit (OBJ, DAE, vxtmesh) → mặt bằng…", ImportMeshModelUi);
        Add("Công cụ xuất SketchUp / 3ds Max / Revit → thư viện, mô hình…", SketchUpPluginUi);
        m.Items.Add(new Separator());
        Add("Kiểm tra _va chạm", RunClashUi);
        m.Items.Add(new Separator());
        Add("Xuất _IFC…", () => ExportBim("ifc"));
        Add("Xuất SketchUp (DAE)…", () => ExportBim("dae"));
        Add("Xuất OBJ…", () => ExportBim("obj"));
        Add("Nhập IFC…", ImportIfcUi);
        Add("Cập nhật từ _Revit…", UpdateFromRevitUi);
        m.Items.Add(new Separator());
        Add("Xuất hồ sơ theo bộ _môn…", ExportDisciplineDocsUi);
        int idx = Math.Max(0, mainMenu.Items.Count - 2);
        mainMenu.Items.Insert(idx, m);
    }

    // ==================================================================
    // Cao độ
    // ==================================================================
    private string StoryForEdit() => CurrentStory == AllStories ? _p.Stories.OrderBy(s => s.Elevation).FirstOrDefault()?.Name : CurrentStory;

    private void AfterModelEdit(string msg, string snap)
    {
        if (snap != null) PushUndo(snap);
        _res = null; _design = null; _sel = null;
        MarkDirty();
        LoadUiFromProject();
        RefreshAll(true);
        SetStatus(msg);
    }

    private void OpenLevelWizard()
    {
        if (_p == null) return;
        var o = new LevelWizardOptions();
        var w = new Window { Title = "Tạo cao độ tầng – từ móng đến mái", Owner = this, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var g = new Grid { Margin = new Thickness(12) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        var fields = new List<(string Label, Func<string> Get, Action<string> Set)>();
        TextBox Row(string label, double v, string unit)
        {
            int r = g.RowDefinitions.Count;
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var tb = new TextBlock { Text = label + (unit.Length > 0 ? $" ({unit})" : ""), Margin = new Thickness(0, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center };
            var box = new TextBox { Text = v.ToString(System.Globalization.CultureInfo.CurrentCulture), Margin = new Thickness(0, 3, 0, 3) };
            Grid.SetRow(tb, r); Grid.SetRow(box, r); Grid.SetColumn(box, 1);
            g.Children.Add(tb); g.Children.Add(box);
            return box;
        }
        var tFloors = Row("Số tầng (kể cả tầng 1)", o.Floors, "");
        var tH1 = Row("Chiều cao tầng 1", o.FirstFloorHeight, "m");
        var tH = Row("Chiều cao tầng trên", o.FloorHeight, "m");
        var tG = Row("Cao độ nền tầng 1", o.GroundElevation, "m");
        var tF = Row("Chiều sâu chôn móng (đáy móng)", o.FoundationDepth, "m");
        var tFt = Row("Mặt móng dưới nền", o.FootingTopDepth, "m");
        var tSn = Row("Chiều dày sàn nền", o.GroundSlabThickness, "m");
        int rr = g.RowDefinitions.Count;
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var roof = new ComboBox { ItemsSource = new[] { "Mái bằng BTCT", "Mái tôn", "Mái ngói" }, SelectedIndex = 0, Margin = new Thickness(0, 3, 0, 3) };
        var lr = new TextBlock { Text = "Kiểu mái", Margin = new Thickness(0, 3, 8, 3) };
        Grid.SetRow(lr, rr); Grid.SetRow(roof, rr); Grid.SetColumn(roof, 1);
        g.Children.Add(lr); g.Children.Add(roof);
        var cTum = new CheckBox { Content = "Có tầng tum (3,0 m)", Margin = new Thickness(0, 4, 0, 0) };
        var cFrame = new CheckBox { Content = "Sinh khung + sàn theo lưới trục", IsChecked = true, Margin = new Thickness(0, 4, 0, 0) };
        var cGround = new CheckBox { Content = "Tạo sàn nền tầng 1 (trên đất)", IsChecked = true, Margin = new Thickness(0, 4, 0, 0) };
        foreach (var cb in new[] { cTum, cFrame, cGround })
        {
            int r = g.RowDefinitions.Count; g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(cb, r); Grid.SetColumnSpan(cb, 2); g.Children.Add(cb);
        }
        var ok = new Button { Content = "Tạo", IsDefault = true, Width = 90, Margin = new Thickness(0, 10, 6, 0) };
        var cancel = new Button { Content = "Huỷ", IsCancel = true, Width = 90, Margin = new Thickness(0, 10, 0, 0) };
        var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        bp.Children.Add(ok); bp.Children.Add(cancel);
        int rb = g.RowDefinitions.Count; g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(bp, rb); Grid.SetColumnSpan(bp, 2); g.Children.Add(bp);
        w.Content = g;
        ok.Click += (s, e) => w.DialogResult = true;
        if (w.ShowDialog() != true) return;
        double D(TextBox t, double def) => double.TryParse(t.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : def;
        o.Floors = (int)Math.Round(D(tFloors, o.Floors));
        o.FirstFloorHeight = D(tH1, o.FirstFloorHeight); o.FloorHeight = D(tH, o.FloorHeight);
        o.GroundElevation = D(tG, o.GroundElevation); o.FoundationDepth = D(tF, o.FoundationDepth);
        o.FootingTopDepth = D(tFt, o.FootingTopDepth); o.GroundSlabThickness = D(tSn, o.GroundSlabThickness);
        o.RoofType = roof.SelectedItem as string ?? o.RoofType; o.Tum = cTum.IsChecked == true;
        o.BuildFrame = cFrame.IsChecked == true; o.GroundSlab = cGround.IsChecked == true;
        string snap = Snapshot();
        string msg = Levels.ApplyWizard(_p, o);
        if (msg.StartsWith("Mô hình đã có")) { MessageBox.Show(this, msg, AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return; }
        AfterModelEdit(msg, snap);
        MessageBox.Show(this, msg + "\n\n" + string.Join("\n", Levels.All(_p).Select(l => "• " + l)), "Cao độ công trình", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void InsertStoryUi()
    {
        if (_p == null) return;
        string below = CurrentStory == AllStories ? null : CurrentStory;
        string ans = DialogUi.InputBox(this, "Chèn tầng", $"Chèn tầng mới phía trên {(below ?? "nền tầng 1")} – chiều cao tầng mới (m):", "3.3");
        if (string.IsNullOrWhiteSpace(ans)) return;
        if (!double.TryParse(ans.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double h)) return;
        string snap = Snapshot();
        try
        {
            string name = Levels.InsertStory(_p, below, h);
            AfterModelEdit($"Đã chèn tầng {name} cao {h:0.00} m – các tầng phía trên được nâng, cần chạy lại phân tích.", snap);
        }
        catch (Exception ex) { ShowError("Chèn tầng", ex); }
    }

    private void DeleteStoryUi()
    {
        if (_p == null || CurrentStory == AllStories) { SetStatus("Chọn tầng cần xoá ở ô Tầng đang xem."); return; }
        string st = CurrentStory;
        if (MessageBox.Show(this, $"Xoá tầng {st}: xoá dầm, sàn, tường của tầng, nối cột, hạ các tầng phía trên?", AppTitle, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        string snap = Snapshot();
        Levels.DeleteStory(_p, st);
        AfterModelEdit($"Đã xoá tầng {st}.", snap);
    }

    private void SlabsAllBaysUi()
    {
        if (_p == null) return;
        string snap = Snapshot();
        int n = Levels.SlabsForAllBays(_p, CurrentStory == AllStories ? null : CurrentStory);
        AfterModelEdit($"Đã thêm {n} ô sàn theo lưới.", snap);
    }

    private void GroundSlabsUi()
    {
        if (_p == null) return;
        string snap = Snapshot();
        int n = Levels.GroundSlabsFromGrid(_p);
        AfterModelEdit($"Sàn nền tầng 1: {n} ô (nằm trên đất, không tải lên khung).", snap);
    }

    // ==================================================================
    // Kiến trúc
    // ==================================================================
    private void DetectRoomsUi()
    {
        if (_p == null) return;
        string st = StoryForEdit();
        if (st == null) return;
        string snap = Snapshot();
        var rooms = RoomFinder.Detect(_p, st);
        if (snap != null) PushUndo(snap);
        MarkDirty(false);
        RedrawPlan();
        SetStatus(rooms.Count == 0 ? $"Không tìm thấy vùng kín nào từ tường tầng {st} (vẽ tường khép kín trước)." : $"Tầng {st}: {rooms.Count} phòng, tổng {rooms.Sum(r => r.Area):0.0} m².");
    }

    private void RenameRoomsUi()
    {
        if (_p?.Arch == null) return;
        string st = StoryForEdit();
        foreach (var r in _p.Arch.Rooms.Where(r => r.Story == st).ToList())
        {
            string n = DialogUi.InputBox(this, "Tên phòng", $"Phòng {r.Number} ({r.Area:0.0} m²) – tên (vd. Phòng ngủ, WC, Bếp, Ban công):", r.Name);
            if (n == null) break;
            if (!string.IsNullOrWhiteSpace(n)) r.Name = n.Trim();
        }
        MarkDirty(false);
        RedrawPlan();
    }

    private void ArchToStructureUi()
    {
        if (_p == null) return;
        var rep = ArchStructureSync.Transfer(_p, false);
        string msg = rep.ToString();
        if (rep.ProposedColumns.Count > 0)
        {
            var r = MessageBox.Show(this, msg + $"\n\nThêm {rep.ProposedColumns.Count} cột đề xuất tại giao tường (tiết diện C22x22)?", "Kiến trúc → kết cấu", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes)
            {
                string snap = Snapshot();
                var rep2 = ArchStructureSync.Transfer(_p, true);
                AfterModelEdit(rep2.Lines.LastOrDefault() ?? "", snap);
                return;
            }
        }
        else MessageBox.Show(this, msg, "Kiến trúc → kết cấu", MessageBoxButton.OK, MessageBoxImage.Information);
        if (rep.SlabUsesSet > 0) { _res = null; _design = null; MarkDirty(); RedrawPlan(); }
    }

    private void OpenLibrary3D()
    {
        if (_p == null) return;
        var w = new Window { Title = "Thư viện 3D (lưu trên máy: " + Library3D.DefaultFolder + ")", Owner = this, Width = 560, Height = 460, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var dock = new DockPanel { Margin = new Thickness(8) };
        var list = new ListBox();
        void Reload() { list.ItemsSource = Library3D.LoadAll().Select(i => new LibRow { I = i, Text = $"{i.Category} – {i.Name} ({i.Width:0.00}×{i.Depth:0.00}×{i.Height:0.00} m, {i.Source})" }).ToList(); list.DisplayMemberPath = "Text"; }
        Reload();
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        var bImp = new Button { Content = "Nhập OBJ / DAE / DXF 3D / Revit…", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
        var bPlace = new Button { Content = "Đặt vào mặt bằng (bắt điểm)", Padding = new Thickness(8, 2, 8, 2) };
        bar.Children.Add(bImp); bar.Children.Add(bPlace);
        DockPanel.SetDock(bar, Dock.Bottom);
        dock.Children.Add(bar); dock.Children.Add(list);
        w.Content = dock;
        bImp.Click += (s, e) =>
        {
            var dlg = new OpenFileDialog { Filter = "Mô hình 3D (*.obj;*.dae;*.dxf;*.vxtmesh.json)|*.obj;*.dae;*.dxf;*.json", Multiselect = true, Title = "Nhập vào thư viện 3D (SketchUp, Revit, 3ds Max, AutoCAD)" };
            if (dlg.ShowDialog(w) != true) return;
            foreach (var f in dlg.FileNames)
            {
                try
                {
                    double scale = 1;
                    if (f.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                    {
                        string u = DialogUi.InputBox(w, "Đơn vị OBJ", "Hệ số đổi đơn vị sang m (m = 1, mm = 0.001, inch SketchUp = 0.0254):", "1");
                        if (u != null) double.TryParse(u.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out scale);
                        if (scale <= 0) scale = 1;
                    }
                    Library3D.Import(f, null, scale);
                }
                catch (Exception ex) { MessageBox.Show(w, Path.GetFileName(f) + ": " + ex.Message, AppTitle); }
            }
            Reload();
        };
        bPlace.Click += (s, e) =>
        {
            if (list.SelectedItem is not LibRow sel) return;
            // [Kiểu Revit] không nhập toạ độ: đóng thư viện → rê chuột trên mặt bằng, tự bắt mặt tường / góc phòng / giao trục (MainWindow.Furnish.cs)
            LibraryItem it = sel.I;
            w.Close();
            StartPlaceFurniture(it);
        };
        w.ShowDialog();
    }

    // ==================================================================
    // Phối hợp
    // ==================================================================
    private void OpenDisciplineDisplay()
    {
        if (_p == null) return;
        _p.DisciplineDisplay ??= new DisciplineSettings();
        var ds = _p.DisciplineDisplay;
        var w = new Window { Title = "Bộ môn hiển thị", Owner = this, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var sp = new StackPanel { Margin = new Thickness(12) };
        sp.Children.Add(new TextBlock { Text = $"Đang làm việc: {DisciplineView.Title(_p.ActiveDiscipline)}", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        var boxes = new Dictionary<Discipline, CheckBox>();
        foreach (var d in DisciplineView.All)
        {
            var cb = new CheckBox { Content = "Hiện " + DisciplineView.Title(d), IsChecked = ds.Shown(d), IsEnabled = d != _p.ActiveDiscipline, Margin = new Thickness(0, 2, 0, 2) };
            boxes[d] = cb; sp.Children.Add(cb);
        }
        var half = new CheckBox { Content = "Vẽ mờ bộ môn tham chiếu (như xref)", IsChecked = ds.HalftoneOthers, Margin = new Thickness(0, 8, 0, 2) };
        sp.Children.Add(half);
        var sl = new Slider { Minimum = 0.1, Maximum = 0.9, Value = ds.HalftoneOpacity, Width = 220, Margin = new Thickness(0, 4, 0, 0), TickFrequency = 0.05, IsSnapToTickEnabled = true };
        sp.Children.Add(new TextBlock { Text = "Độ mờ" });
        sp.Children.Add(sl);
        var ok = new Button { Content = "Áp dụng", IsDefault = true, Width = 90, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        ok.Click += (s, e) => w.DialogResult = true;
        sp.Children.Add(ok);
        w.Content = sp;
        if (w.ShowDialog() != true) return;
        foreach (var kv in boxes) ds.SetShown(kv.Key, kv.Value.IsChecked == true);
        ds.HalftoneOthers = half.IsChecked == true; ds.HalftoneOpacity = sl.Value;
        MarkDirty(false);
        RedrawPlan();
    }

    private void RunClashUi()
    {
        if (_p == null) return;
        var res = ClashDetector.Run(_p, new ClashOptions { Clearance = 0.0 });
        var w = new Window { Title = $"Kiểm tra va chạm – {res.Count} kết quả", Owner = this, Width = 760, Height = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var list = new ListBox { ItemsSource = res.Select((c, i) => $"{i + 1}. {c}").ToList() };
        list.MouseDoubleClick += (s, e) =>
        {
            int i = list.SelectedIndex;
            if (i < 0 || i >= res.Count) return;
            var c = res[i];
            string st = !string.IsNullOrEmpty(c.A.Story) && _p.Stories.Any(x => x.Name == c.A.Story) ? c.A.Story : c.B.Story;
            ShowPlanTab();
            if (st != null && cbStory.Items.Contains(st)) cbStory.SelectedItem = st;
            ZoomPlanTo(c.Location.X, c.Location.Y, 3.0);
        };
        var dock = new DockPanel { Margin = new Thickness(8) };
        var info = new TextBlock
        {
            Text = res.Count == 0 ? "Không phát hiện va chạm giữa các bộ môn." :
                $"Cứng: {res.Count(c => c.Type == ClashType.Cung)}, ống lồng: {res.Count(c => c.Type == ClashType.OngLong)}, khe hở: {res.Count(c => c.Type == ClashType.KheHo)} – nháy đúp để xem trên mặt bằng.",
            Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap
        };
        DockPanel.SetDock(info, Dock.Top);
        dock.Children.Add(info); dock.Children.Add(list);
        w.Content = dock;
        w.Show();
    }

    // ==================================================================
    // Trao đổi
    // ==================================================================
    private List<Discipline> AskDisciplines(string title)
    {
        var w = new Window { Title = title, Owner = this, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var sp = new StackPanel { Margin = new Thickness(12) };
        var boxes = DisciplineView.All.Select(d => (d, new CheckBox { Content = DisciplineView.Title(d), IsChecked = true, Margin = new Thickness(0, 2, 0, 2) })).ToList();
        foreach (var (_, cb) in boxes) sp.Children.Add(cb);
        var ok = new Button { Content = "Tiếp tục", IsDefault = true, Width = 90, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        ok.Click += (s, e) => w.DialogResult = true;
        sp.Children.Add(ok);
        w.Content = sp;
        if (w.ShowDialog() != true) return null;
        return boxes.Where(b => b.Item2.IsChecked == true).Select(b => b.d).ToList();
    }

    private void ExportBim(string kind)
    {
        if (_p == null) return;
        var ds = AskDisciplines(kind == "ifc" ? "Xuất IFC – chọn bộ môn" : "Xuất 3D – chọn bộ môn");
        if (ds == null || ds.Count == 0) return;
        var dlg = new SaveFileDialog
        {
            FileName = MeshExport.Safe(_p.Name),
            Filter = kind switch { "ifc" => "IFC2X3 (*.ifc)|*.ifc", "dae" => "COLLADA – SketchUp (*.dae)|*.dae", _ => "Wavefront OBJ (*.obj)|*.obj" }
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            string info;
            switch (kind)
            {
                case "ifc":
                    IfcWriter.Save(_p, dlg.FileName, new IfcOptions { Disciplines = ds });
                    info = "Đã xuất IFC2X3 – mở trong Revit (File → Open → IFC) hoặc SketchUp Pro (File → Import → IFC).";
                    break;
                case "dae":
                    info = $"Đã xuất COLLADA ({MeshExport.SaveDae(_p, dlg.FileName, ds)} tam giác) – SketchUp: File → Import → *.dae.";
                    break;
                default:
                    info = $"Đã xuất OBJ + MTL ({MeshExport.SaveObj(_p, dlg.FileName, ds)} tam giác) – đơn vị m.";
                    break;
            }
            SetStatus(info + " " + dlg.FileName);
            MessageBox.Show(this, info + "\n\n" + dlg.FileName, AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowError("Xuất " + kind.ToUpperInvariant(), ex); }
    }

    private void ImportIfcUi()
    {
        if (_p == null) return;
        var dlg = new OpenFileDialog { Filter = "IFC (*.ifc)|*.ifc", Title = "Nhập IFC (cột, dầm, sàn, tường, cửa, phòng)" };
        if (dlg.ShowDialog(this) != true) return;
        string snap = Snapshot();
        try
        {
            var rep = IfcImporter.Import(_p, File.ReadAllText(dlg.FileName));
            AfterModelEdit(rep.ToString(), snap);
            MessageBox.Show(this, rep + (rep.Warnings.Count > 0 ? "\n\n" + string.Join("\n", rep.Warnings.Take(20)) : ""), "Nhập IFC", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowError("Nhập IFC", ex); }
    }

    private void UpdateFromRevitUi()
    {
        if (_p == null) return;
        var dlg = new OpenFileDialog { Filter = "File Revit → Việt Const (*.vxtrevit.json)|*.vxtrevit.json;*.json", Title = "Cập nhật kiến trúc / điện nước từ Revit" };
        if (dlg.ShowDialog(this) != true) return;
        string snap = Snapshot();
        try
        {
            var ex = RevitJsonImporter.ReadFile(dlg.FileName);
            var rep = RevitArchImport.Update(_p, ex);
            AfterModelEdit(rep.ToString(), snap);
            MessageBox.Show(this, rep + (rep.Lines.Count > 0 ? "\n\n" + string.Join("\n", rep.Lines.Take(30)) : ""), "Cập nhật từ Revit", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex2) { ShowError("Cập nhật từ Revit", ex2); }
    }

    private void ExportDisciplineDocsUi()
    {
        if (_p == null) return;
        var ds = AskDisciplines("Xuất hồ sơ theo bộ môn");
        if (ds == null || ds.Count == 0) return;
        var dlg = new SaveFileDialog { Title = "Chọn thư mục – nhập tên bất kỳ", FileName = "HoSo_BoMon", Filter = "Thư mục|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        string folder = Path.Combine(Path.GetDirectoryName(dlg.FileName) ?? ".", Path.GetFileNameWithoutExtension(dlg.FileName));
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var res = KetCauVXT.Drawing.DisciplineDocs.Export(_p, _design, folder, new KetCauVXT.Drawing.DisciplineDocOptions { Disciplines = ds, ShowOthers = true });
            Mouse.OverrideCursor = null;
            SetStatus($"Đã xuất {res.Files.Count} bản vẽ vào {folder}");
            MessageBox.Show(this, $"Đã xuất {res.Files.Count} bản vẽ:\n" + string.Join("\n", res.Files.Select(Path.GetFileName).Take(40)), "Hồ sơ theo bộ môn", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { Mouse.OverrideCursor = null; ShowError("Hồ sơ theo bộ môn", ex); }
    }
}

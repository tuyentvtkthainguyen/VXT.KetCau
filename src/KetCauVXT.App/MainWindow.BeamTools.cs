using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// CÔNG CỤ DẦM KIỂU ETABS trên mặt bằng (thanh công cụ thứ hai dưới thanh sửa mặt bằng, dựng bằng mã):
//   • Khai báo tiết diện… (SectionManagerWindow, không chặn) – tiết diện hiện hành = ô "Tiết diện dầm" (cbEditBeamSec);
//   • ＋ Dầm (2 điểm, MainWindow.Edit.cs) dùng bắt điểm kiểu ETABS (đầu dầm, nút, giao trục, giao điểm, trung điểm, vuông góc,
//     gần nhất, trên trục; bật/tắt từng loại) + gõ toạ độ "x,y", "@dx,dy", "L<góc", "L" trong ô nổi; chuột phải = kết thúc;
//   • Vẽ nhanh theo lưới (bấm đoạn trục / kéo khung), Dầm phụ (bấm trong ô → số lượng / khoảng cách / danh sách),
//     Công xôn (bấm điểm gốc → độ vươn, hướng), Dầm theo toạ độ / trục…, Chọn nhiều (bấm / kéo khung window-crossing),
//     Gán tiết diện, Điểm chèn…, Sao chép / Đối xứng…;
//   • Phạm vi tầng: tầng hiện tại / các tầng tương tự / tất cả tầng / tầng đánh dấu – áp dụng cho mọi công cụ vẽ dầm.
// Thao tác mô hình ở KetCauVXT.Model.BeamTools; hoàn tác như các thao tác sửa mặt bằng khác (Snapshot → AfterEdit).
// =====================================================================
public partial class MainWindow
{
    private SnapMask _snapMask = SnapMask.All;
    private bool _etabsSnap = true;
    private SnapCandidate _lastSnap;
    private CoordUnit _coordUnit = CoordUnit.Mm;
    private StoryScope _storyScope = StoryScope.Current;
    private List<string> _scopeTicked = new();
    private SecondaryBeamOptions _secOpts;
    private readonly HashSet<int> _multiSel = new();
    private Project _multiSelOwner;
    private bool _winDrag;
    private Point _winStart, _winEnd;
    private Border _coordBox;
    private TextBox _coordText;
    private TextBlock _coordLabel;
    private ComboBox _cbScope;
    private SectionManagerWindow _secWin;
    private readonly Dictionary<string, Brush> _secBrushes = new();
    private static readonly Brush WinBrush = Frozen(Color.FromRgb(30, 110, 220));
    private static readonly Brush WinFill = Frozen(Color.FromArgb(35, 30, 110, 220));
    private static readonly Brush CrossBrush = Frozen(Color.FromRgb(20, 150, 60));
    private static readonly Brush CrossFill = Frozen(Color.FromArgb(35, 20, 150, 60));
    private static readonly Brush SnapGlyph = Frozen(Color.FromRgb(0, 150, 170));
    private static readonly Brush MultiSelBrush = Frozen(Color.FromArgb(200, 255, 160, 0));

    private bool IsBeamToolMode => _mode is PlanEditMode.QuickGridBeam or PlanEditMode.SecondaryBeam or PlanEditMode.CantileverBeam or PlanEditMode.MultiSelect;
    private bool IsWindowMode => _mode is PlanEditMode.QuickGridBeam or PlanEditMode.MultiSelect;
    private bool UseEtabsSnap => _etabsSnap && (_mode == PlanEditMode.AddBeam || _mode == PlanEditMode.CantileverBeam);

    private static string BeamToolHint(PlanEditMode m) => m switch
    {
        PlanEditMode.QuickGridBeam => "Vẽ nhanh dầm theo lưới: bấm vào đoạn trục giữa hai giao trục để đặt dầm; kéo khung (trái → phải: đoạn nằm trọn trong khung, phải → trái: đoạn chạm khung) để đặt dầm trên nhiều đoạn; đoạn đã có dầm được bỏ qua. Shift + kéo hoặc chuột giữa để di chuyển; chuột phải / Esc để thoát.",
        PlanEditMode.SecondaryBeam => "Dầm phụ: bấm vào trong một ô (kín bởi dầm hoặc ô lưới trục) rồi chọn phương, số dầm chia đều / khoảng cách đều / danh sách khoảng cách từ trục; dầm phụ nhịp giữa hai cạnh ô, ô sàn được chia lại.",
        PlanEditMode.CantileverBeam => "Dầm công xôn: bấm điểm gốc (bắt nút, đầu dầm, giao trục, điểm trên dầm) rồi nhập độ vươn và hướng.",
        PlanEditMode.MultiSelect => "Chọn nhiều dầm: bấm để thêm/bớt dầm; kéo khung trái → phải chọn dầm nằm trọn trong khung, phải → trái chọn dầm chạm khung (giữ Ctrl để bỏ chọn); Delete xoá, 'Gán tiết diện', 'Điểm chèn…', 'Sao chép / Đối xứng…' áp dụng cho các dầm đã chọn; Esc bỏ chọn.",
        _ => ""
    };

    // ==================================================================
    // Khởi tạo thanh công cụ
    // ==================================================================
    /// <summary>Khung mở/đóng chứa hàng công cụ dầm kiểu ETABS.</summary>
    private Expander _beamToolsExpander;

    private void InitBeamToolsUi()
    {
        if (planEditBar?.Parent is not Border host || host.Parent is not DockPanel dock) return;
        var bar = new WrapPanel { Orientation = Orientation.Horizontal };
        var tgStyle = TryFindResource(typeof(ToggleButton)) as Style;
        RadioButton Mode(string text, string tag, string tip)
        {
            var rb = new RadioButton { Content = text, Tag = tag, GroupName = "PlanMode", ToolTip = tip, Margin = new Thickness(0, 0, 3, 2), Padding = new Thickness(7, 2, 7, 2) };
            if (tgStyle != null) rb.Style = tgStyle;
            rb.Checked += PlanMode_Checked;
            bar.Children.Add(rb);
            return rb;
        }
        Button Btn(string text, Action act, string tip)
        {
            var b = new Button { Content = text, Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(0, 0, 3, 2), ToolTip = tip };
            b.Click += (s, e) => act();
            bar.Children.Add(b);
            return b;
        }
        TextBlock Lab(string t) { var x = new TextBlock { Text = t, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 2) }; bar.Children.Add(x); return x; }

        Lab("Dầm (kiểu ETABS):").FontWeight = FontWeights.Bold;
        Btn("Khai báo tiết diện…", OpenSectionManager, "Khai báo / sửa / xoá tiết diện dầm, cột (Define → Frame Sections); bấm đúp để đặt làm tiết diện hiện hành");
        Mode("Vẽ nhanh theo lưới", "QuickGridBeam", "Bấm đoạn trục hoặc kéo khung để đặt dầm trên các đoạn trục giữa hai giao trục (Quick Draw Beams)");
        Mode("Dầm phụ", "SecondaryBeam", "Bấm vào ô để thêm dầm phụ theo số lượng / khoảng cách / danh sách khoảng cách từ trục (Quick Draw Secondary Beams)");
        Mode("Công xôn", "CantileverBeam", "Bấm điểm gốc rồi nhập độ vươn, hướng dầm công xôn (ban công, ô văng)");
        Btn("Dầm theo toạ độ / trục…", () => BeamByPositionDialog(0, null), "Thêm dầm theo trục + khoảng cách (vd. trục 2 + 1200 từ A đến C), theo toạ độ, hoặc dầm công xôn");
        Mode("Chọn nhiều", "MultiSelect", "Chọn nhiều dầm: bấm / kéo khung (trái→phải: trọn trong khung, phải→trái: chạm khung)");
        Btn("Gán tiết diện", AssignSectionToSelection, "Gán tiết diện hiện hành (ô 'Tiết diện dầm') cho các dầm đã chọn");
        Btn("Điểm chèn…", InsertionForSelection, "Điểm chèn (cardinal point) và dời ngang tiết diện cho các dầm đã chọn – vd. mép dầm trùng trục / mép tường");
        Btn("Sao chép / Đối xứng…", ReplicateSelection, "Sao chép dầm đã chọn theo Δx, Δy (n bản), đối xứng qua trục, hoặc chép sang tầng khác (Replicate)");

        Lab("Phạm vi tầng:");
        _cbScope = new ComboBox { Width = 140, Margin = new Thickness(0, 0, 3, 2), ToolTip = "Tầng áp dụng cho mọi công cụ vẽ dầm (như ETABS: One Story / Similar Stories / All Stories)" };
        foreach (var item in new[] { "Tầng hiện tại", "Các tầng tương tự", "Tất cả tầng", "Tầng đánh dấu" }) _cbScope.Items.Add(item);
        _cbScope.SelectedIndex = 0;
        _cbScope.SelectionChanged += (s, e) =>
        {
            _storyScope = (StoryScope)Math.Max(0, _cbScope.SelectedIndex);
            if (_storyScope == StoryScope.Selected && _scopeTicked.Count == 0) PickScopeStories();
            if (IsLoaded) SetStatus("Phạm vi tầng cho công cụ vẽ dầm: " + ScopeText());
        };
        bar.Children.Add(_cbScope);
        Btn("Chọn tầng…", PickScopeStories, "Đánh dấu các tầng giống nhau / tầng áp dụng");

        Lab("Bắt điểm:");
        var master = new CheckBox { Content = "ETABS", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 2), ToolTip = "Bật bắt điểm kiểu ETABS cho ＋ Dầm và Công xôn (tắt = bắt nút/giao trục/lưới 50 mm như cũ)" };
        master.Checked += (s, e) => _etabsSnap = true; master.Unchecked += (s, e) => _etabsSnap = false;
        bar.Children.Add(master);
        foreach (var (text, flag, tip) in new[]
        {
            ("Nút", SnapMask.End | SnapMask.Node, "Đầu dầm, nút có sẵn (□)"),
            ("Giao trục", SnapMask.GridIntersection, "Giao điểm hai trục (×)"),
            ("Giao", SnapMask.Intersection, "Giao điểm dầm × dầm, dầm × trục (⊗)"),
            ("Giữa", SnapMask.Midpoint, "Trung điểm dầm (△)"),
            ("Vuông góc", SnapMask.Perpendicular, "Chân đường vuông góc từ điểm trước xuống dầm / trục (⊥)"),
            ("Gần", SnapMask.Nearest, "Điểm gần nhất trên dầm (⧖)"),
            ("Trên trục", SnapMask.GridLine, "Điểm trên đường trục, làm tròn 50 mm (◇)")
        })
        {
            var f = flag;
            var tb = new ToggleButton { Content = text, IsChecked = true, Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(0, 0, 2, 2), ToolTip = tip };
            tb.Checked += (s, e) => _snapMask |= f;
            tb.Unchecked += (s, e) => _snapMask &= ~f;
            bar.Children.Add(tb);
        }
        Lab("Gõ toạ độ:");
        var cbUnit = new ComboBox { Width = 50, Margin = new Thickness(0, 0, 3, 2), ToolTip = "Đơn vị số khi gõ toạ độ (số kèm mm/cm/m theo hậu tố)" };
        cbUnit.Items.Add("mm"); cbUnit.Items.Add("m"); cbUnit.SelectedIndex = 0;
        cbUnit.SelectionChanged += (s, e) => _coordUnit = cbUnit.SelectedIndex == 1 ? CoordUnit.M : CoordUnit.Mm;
        bar.Children.Add(cbUnit);
        bar.Children.Add(new TextBlock
        {
            Text = "(＋ Dầm: gõ x,y | @dx,dy | L<góc | L)", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 2),
            ToolTip = "Trong chế độ ＋ Dầm, gõ số trên mặt bằng để mở ô nhập toạ độ: \"x,y\" tuyệt đối, \"@dx,dy\" tương đối điểm trước, \"L<góc\" toạ độ cực, \"L\" = khoảng cách theo hướng chuột; Enter để đặt điểm, Esc để huỷ."
        });

        // gói trong khung mở/đóng (mặc định đóng) để không chiếm chỗ vùng vẽ mặt bằng trên màn hình nhỏ
        var exp = new Expander
        {
            Header = new TextBlock { Text = "Dầm kiểu ETABS – khai báo tiết diện, vẽ nhanh theo lưới, dầm phụ, công xôn, sao chép, phạm vi tầng, bắt điểm, gõ toạ độ", FontWeight = FontWeights.SemiBold },
            IsExpanded = false, Content = bar
        };
        bar.Children.Remove(bar.Children[0]);   // nhãn "Dầm (kiểu ETABS):" đã có ở tiêu đề khung
        exp.Expanded += (s, e) => Dispatcher.BeginInvoke(new Action(FitPlan), System.Windows.Threading.DispatcherPriority.Background);
        exp.Collapsed += (s, e) => Dispatcher.BeginInvoke(new Action(FitPlan), System.Windows.Threading.DispatcherPriority.Background);
        _beamToolsExpander = exp;
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(236, 242, 249)), BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
            BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(3), Child = exp
        };
        DockPanel.SetDock(border, Dock.Top);
        dock.Children.Insert(dock.Children.IndexOf(host) + 1, border);

        // ô nhập toạ độ nổi trên mặt bằng
        if (planCanvas.Parent is Grid g)
        {
            _coordLabel = new TextBlock { Foreground = Brushes.DimGray, FontSize = 10.5, Margin = new Thickness(0, 0, 0, 2) };
            _coordText = new TextBox { Width = 170, FontSize = 12 };
            _coordText.PreviewKeyDown += CoordText_PreviewKeyDown;
            _coordText.LostKeyboardFocus += (s, e) => HideCoordBox(false);
            var sp = new StackPanel();
            sp.Children.Add(_coordLabel); sp.Children.Add(_coordText);
            _coordBox = new Border
            {
                Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Background = Brushes.White, BorderBrush = RubberBeam, BorderThickness = new Thickness(1.5), Padding = new Thickness(4), Child = sp,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.35 }
            };
            g.Children.Add(_coordBox);
        }
        planHost.PreviewTextInput += PlanHost_PreviewTextInput;
        planHost.MouseRightButtonUp += PlanHost_RightUp;
        planHost.MouseDown += PlanHost_MiddleDown;
        planHost.MouseUp += PlanHost_MiddleUp;
    }

    private void MnuSectionManager_Click(object sender, RoutedEventArgs e) => OpenSectionManager();

    // ==================================================================
    // Cửa sổ khai báo tiết diện
    // ==================================================================
    private void OpenSectionManager()
    {
        if (_p == null) return;
        if (_secWin != null && _secWin.IsLoaded) { _secWin.Reload(); _secWin.Activate(); return; }
        _secWin = new SectionManagerWindow(() => _p, Snapshot, OnSectionsChanged, name =>
        {
            RefreshEditCombos();
            if (cbEditBeamSec.Items.Contains(name)) cbEditBeamSec.SelectedItem = name;
            else SetStatus($"Tiết diện {name} không phải tiết diện dầm chữ nhật – không đặt làm tiết diện hiện hành.");
        }) { Owner = this };
        _secWin.Closed += (s, e) => _secWin = null;
        _secWin.Show();
    }

    private void OnSectionsChanged(string snapBefore, string msg)
    {
        if (snapBefore != null) PushUndo(snapBefore);
        _p.InvalidateIndex();
        _secBrushes.Clear();
        MarkDirty();
        RefreshSections();
        RefreshEditCombos();
        RedrawPlan();
        Build3D();
        ShowSelection();
        SetStatus(msg);
    }

    // ==================================================================
    // Phạm vi tầng
    // ==================================================================
    private List<string> ScopeStories(string current) => BeamTools.ResolveStories(_p, current, _storyScope, _scopeTicked);

    private string ScopeText()
    {
        string st = CurrentStory;
        if (_p == null || st == AllStories || _p.Stories.All(q => q.Name != st)) return _cbScope?.SelectedItem as string ?? "";
        return $"{_cbScope?.SelectedItem} → {string.Join(", ", ScopeStories(st))}";
    }

    private void PickScopeStories()
    {
        if (_p == null) return;
        string st = CurrentStory == AllStories ? null : CurrentStory;
        var r = BeamToolDialogs.AskStories(this, _p, st, _scopeTicked);
        if (r == null) return;
        _scopeTicked = r;
        if (_cbScope != null && _storyScope == StoryScope.Current) _cbScope.SelectedIndex = (int)StoryScope.Selected;
        SetStatus("Phạm vi tầng cho công cụ vẽ dầm: " + ScopeText());
    }

    /// <summary>Thêm dầm 2 điểm đúng toạ độ trên các tầng thuộc phạm vi (thay ModelEdit.AddBeam trong chế độ ＋ Dầm).</summary>
    private EditResult AddBeamScoped(string st, (double X, double Y) a, (double X, double Y) b, string sec) =>
        BeamTools.ForStories(_p, ScopeStories(st), s => BeamTools.AddBeamExact(_p, s, a.X, a.Y, b.X, b.Y, sec, "T", true));

    // ==================================================================
    // Bắt điểm kiểu ETABS + ký hiệu
    // ==================================================================
    private (double X, double Y) EtabsSnap(string story, double x, double y, double tol)
    {
        (double X, double Y)? from = _lineStart != null && _lineStory == story ? _lineStart : null;
        _lastSnap = BeamTools.BestSnap(_p, story, x, y, tol, _snapMask, from);
        return (_lastSnap.X, _lastSnap.Y);
    }

    private double SnapTol() => Math.Clamp(10 / Math.Max(_scale, 1e-6), 0.15, 0.5);

    /// <summary>Ký hiệu điểm bắt kiểu AutoCAD/ETABS (gọi từ DrawSnapMarker). false = không phải điểm bắt ETABS vừa tính.</summary>
    private bool DrawEtabsSnapGlyph((double X, double Y) pt)
    {
        if (!UseEtabsSnap || _lastSnap == null || Math.Abs(_lastSnap.X - pt.X) > 1e-6 || Math.Abs(_lastSnap.Y - pt.Y) > 1e-6) return false;
        var c = W2S(pt.X, pt.Y);
        var br = SnapGlyph;
        const double r = 6;
        void Poly(params Point[] pts) => planOverlay.Children.Add(new Shapes.Polygon { Points = new PointCollection(pts), Stroke = br, StrokeThickness = 2, IsHitTestVisible = false });
        void Ln(double x1, double y1, double x2, double y2) => CLine(planOverlay, new Point(c.X + x1, c.Y + y1), new Point(c.X + x2, c.Y + y2), br, 2, false);
        switch (_lastSnap.Kind)
        {
            case SnapKind.End:
            case SnapKind.Node:
                Poly(new Point(c.X - r, c.Y - r), new Point(c.X + r, c.Y - r), new Point(c.X + r, c.Y + r), new Point(c.X - r, c.Y + r));
                break;
            case SnapKind.GridIntersection:
                Ln(-r, -r, r, r); Ln(-r, r, r, -r);
                break;
            case SnapKind.Intersection:
                Ln(-r, -r, r, r); Ln(-r, r, r, -r);
                planOverlay.Children.Add(MakeCircle(c, r + 2, br));
                break;
            case SnapKind.Midpoint:
                Poly(new Point(c.X, c.Y - r - 1), new Point(c.X + r + 1, c.Y + r), new Point(c.X - r - 1, c.Y + r));
                break;
            case SnapKind.Perpendicular:
                Ln(-r, r, r, r); Ln(-r, r, -r, -r); Ln(-r, 0, 0, 0); Ln(0, 0, 0, r);
                break;
            case SnapKind.Nearest:
                Poly(new Point(c.X - r, c.Y - r), new Point(c.X + r, c.Y - r), new Point(c.X - r, c.Y + r), new Point(c.X + r, c.Y + r));
                break;
            case SnapKind.GridLine:
                Poly(new Point(c.X, c.Y - r), new Point(c.X + r, c.Y), new Point(c.X, c.Y + r), new Point(c.X - r, c.Y));
                break;
            default:
                planOverlay.Children.Add(MakeCircle(c, 4, br));
                break;
        }
        AddText($"{BeamTools.SnapName(_lastSnap.Kind)}  ({pt.X:0.000}; {pt.Y:0.000})", new Point(c.X + 10, c.Y + 7), Brushes.DimGray, 9, false, false, planOverlay);
        return true;
    }

    private static Shapes.Ellipse MakeCircle(Point c, double r, Brush br)
    {
        var e = new Shapes.Ellipse { Width = 2 * r, Height = 2 * r, Stroke = br, StrokeThickness = 1.6, IsHitTestVisible = false };
        Canvas.SetLeft(e, c.X - r); Canvas.SetTop(e, c.Y - r);
        return e;
    }

    // ==================================================================
    // Hiển thị dầm: điểm chèn (dời ngang) + màu tiết diện
    // ==================================================================
    /// <summary>Hai đầu dầm trên màn hình, dời theo điểm chèn / dời ngang (BeamTools.LateralOffset).</summary>
    private (Point A, Point B) BeamPlanEnds(FrameElement f, Node ni, Node nj)
    {
        double off = BeamTools.LateralOffset(_p, f);
        double dx = nj.X - ni.X, dy = nj.Y - ni.Y, L = Math.Sqrt(dx * dx + dy * dy);
        if (Math.Abs(off) < 1e-6 || L < 1e-9) return (W2S(ni.X, ni.Y), W2S(nj.X, nj.Y));
        double ox = -dy / L * off, oy = dx / L * off;
        return (W2S(ni.X + ox, ni.Y + oy), W2S(nj.X + ox, nj.Y + oy));
    }

    /// <summary>Màu hiển thị theo tiết diện (FrameSection.DisplayColor); null = màu mặc định.</summary>
    private Brush SectionBrush(FrameElement f)
    {
        var sec = _p?.FrameSections.FirstOrDefault(s => s.Name == f.Section);
        if (sec == null || string.IsNullOrEmpty(sec.DisplayColor)) return null;
        if (_secBrushes.TryGetValue(sec.DisplayColor, out var b)) return b;
        try
        {
            var br = new SolidColorBrush((Color)ColorConverter.ConvertFromString(sec.DisplayColor));
            br.Freeze();
            _secBrushes[sec.DisplayColor] = br;
            return br;
        }
        catch (Exception) { _secBrushes[sec.DisplayColor] = null; return null; }
    }

    // ==================================================================
    // Bấm trên mặt bằng trong các chế độ dầm
    // ==================================================================
    /// <summary>Gọi từ PlanClick sau khi đã kiểm tra tầng. true = đã xử lý.</summary>
    private bool BeamToolClick(Point s, string st)
    {
        if (_mode != PlanEditMode.QuickGridBeam && _mode != PlanEditMode.SecondaryBeam && _mode != PlanEditMode.CantileverBeam) return false;
        var (x, y) = S2W(s);
        string sec = cbEditBeamSec.SelectedItem as string;
        if (string.IsNullOrEmpty(sec)) { SetStatus("Chọn tiết diện dầm ở ô 'Tiết diện dầm' (hoặc 'Khai báo tiết diện…') trước."); return true; }
        var stories = ScopeStories(st);
        switch (_mode)
        {
            case PlanEditMode.QuickGridBeam:
            {
                var seg = BeamTools.GridSegmentAt(_p, x, y, SnapTol());
                if (seg == null) { SetStatus("Bấm gần một đoạn trục (giữa hai giao trục) để đặt dầm, hoặc kéo khung để chọn nhiều đoạn."); return true; }
                RunBeamOp(() => BeamTools.ForStories(_p, stories, s2 => BeamTools.QuickBeamsOnGridSegments(_p, s2, new[] { seg }, sec)));
                return true;
            }
            case PlanEditMode.SecondaryBeam:
            {
                var bay = BeamTools.BayAt(_p, st, x, y);
                if (bay == null) { SetStatus("Không tìm thấy ô kín bởi dầm / ô lưới trục tại vị trí bấm."); return true; }
                var rect = _p.FrameSections.Where(q => q.Shape == SectionShape.RectConcrete).Select(q => q.Name).ToList();
                var opts = BeamToolDialogs.AskSecondary(this, _p, bay.Value, _secOpts, rect, sec);
                if (opts == null) return true;
                _secOpts = opts;
                RunBeamOp(() => BeamTools.ForStories(_p, stories, s2 => BeamTools.SecondaryBeamsAt(_p, s2, x, y, opts)));
                return true;
            }
            default:
            {
                var start = _etabsSnap ? EtabsSnap(st, x, y, SnapTol()) : ModelEdit.Snap(_p, st, x, y, SnapTol());
                BeamByPositionDialog(2, start);
                return true;
            }
        }
    }

    private void BeamByPositionDialog(int tab, (double X, double Y)? start)
    {
        if (_p == null || _busy) return;
        string st = CurrentStory;
        if (st == AllStories || _p.Stories.All(q => q.Name != st)) { SetStatus("Chọn một tầng cụ thể ở ô 'Tầng đang xem' trước khi thêm dầm."); return; }
        RefreshEditCombos();
        var rect = _p.FrameSections.Where(q => q.Shape == SectionShape.RectConcrete).Select(q => q.Name).ToList();
        var op = BeamToolDialogs.AskBeamByPosition(this, _p, rect, cbEditBeamSec.SelectedItem as string, _coordUnit, tab, start);
        if (op == null) return;
        var stories = ScopeStories(st);
        RunBeamOp(() => BeamTools.ForStories(_p, stories, s2 => op(_p, s2)));
    }

    /// <summary>Chạy thao tác tạo dầm có hoàn tác; thành công → chọn (nhiều) các dầm mới.</summary>
    private void RunBeamOp(Func<EditResult> op, bool selectNew = true)
    {
        if (_p == null) return;
        if (_busy) { SetStatus("Chương trình đang tính toán – chờ xong rồi sửa mô hình."); return; }
        string snap = Snapshot();
        EditResult r;
        try { r = op(); }
        catch (Exception ex) { RestoreSnapshot(snap); ShowError("Không thực hiện được", ex); return; }
        if (!r.Ok)
        {
            if (Snapshot() != snap) RestoreSnapshot(snap);   // thao tác lỗi giữa chừng
            SetStatus(r.Message);
            return;
        }
        var ids = r.NewFrames.ToList();
        AfterEdit(snap, r, null);
        if (selectNew)
        {
            _multiSel.Clear(); _multiSelOwner = _p;
            foreach (var id in ids) if (_p.Frames.Any(f => f.Id == id)) _multiSel.Add(id);
            DrawHover();
        }
    }

    // ==================================================================
    // Chọn nhiều dầm
    // ==================================================================
    private void CheckMultiSelOwner()
    {
        if (ReferenceEquals(_multiSelOwner, _p)) return;
        _multiSel.Clear(); _multiSelOwner = _p;
    }

    /// <summary>Dầm đang chọn: nhiều (Chọn nhiều) hoặc dầm đang chọn đơn.</summary>
    private List<int> SelectedBeamIds()
    {
        CheckMultiSelOwner();
        var ids = _multiSel.Where(id => _p.Frames.Any(f => f.Id == id && !f.IsVerticalKind)).ToList();
        if (ids.Count == 0 && _sel != null && _p.Frames.FirstOrDefault(f => f.Id == _sel.Id) is FrameElement fr && !fr.IsVerticalKind) ids.Add(fr.Id);
        return ids;
    }

    private void MultiSelectClick(Point s)
    {
        CheckMultiSelOwner();
        var b = NearestBeam(s, 8);
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (b == null)
        {
            if (!ctrl) { _multiSel.Clear(); ClearSelection(); }
            SetStatus($"Đã chọn {_multiSel.Count} dầm.");
            DrawHover();
            return;
        }
        if (!_multiSel.Remove(b.Id)) _multiSel.Add(b.Id);
        if (_multiSel.Contains(b.Id)) Select(frame: b); else DrawHover();
        SetStatus($"Đã chọn {_multiSel.Count} dầm – bấm / kéo khung để thêm, Ctrl + kéo để bỏ; Delete xoá; 'Gán tiết diện', 'Điểm chèn…', 'Sao chép / Đối xứng…'.");
    }

    /// <summary>Phím Delete khi có nhiều dầm được chọn (gọi đầu DeleteSelection). true = đã xử lý.</summary>
    private bool DeleteMultiSelection()
    {
        CheckMultiSelOwner();
        var ids = _multiSel.Where(id => _p.Frames.Any(f => f.Id == id)).ToList();
        if (ids.Count < 1 || (ids.Count == 1 && _mode != PlanEditMode.MultiSelect)) return false;
        if (!ConfirmDelete($"Xoá {ids.Count} dầm đã chọn cùng tải trên dầm?")) return true;
        _multiSel.Clear();
        RunBeamOp(() => BeamTools.DeleteFrames(_p, ids), false);
        return true;
    }

    /// <summary>Esc trong công cụ dầm: bỏ khung chọn / bỏ chọn nhiều. true = đã xử lý.</summary>
    private bool CancelBeamTool()
    {
        HideCoordBox(false);
        if (_winDrag) { _winDrag = false; DrawHover(); return true; }
        CheckMultiSelOwner();
        if (_multiSel.Count > 0) { _multiSel.Clear(); DrawHover(); SetStatus("Đã bỏ chọn các dầm."); return true; }
        return false;
    }

    private void AssignSectionToSelection()
    {
        if (_p == null) return;
        var ids = SelectedBeamIds();
        if (ids.Count == 0) { SetStatus("Chọn dầm trước (chế độ 'Chọn' hoặc 'Chọn nhiều'), rồi bấm 'Gán tiết diện'."); return; }
        string sec = cbEditBeamSec.SelectedItem as string;
        RunBeamOp(() => BeamTools.AssignSection(_p, ids, sec), false);
        RestoreMultiSel(ids);
    }

    private void InsertionForSelection()
    {
        if (_p == null) return;
        var ids = SelectedBeamIds();
        if (ids.Count == 0) { SetStatus("Chọn dầm trước (chế độ 'Chọn' hoặc 'Chọn nhiều'), rồi bấm 'Điểm chèn…'."); return; }
        var f0 = _p.Frames.First(f => f.Id == ids[0]);
        var sec = _p.FrameSections.FirstOrDefault(s => s.Name == f0.Section);
        var r = BeamToolDialogs.AskInsertion(this, ids.Count, f0.Insertion, f0.PlanOffset, sec?.Props(0.5).Width ?? 0.22);
        if (r == null) return;
        RunBeamOp(() => BeamTools.SetInsertion(_p, ids, r.Value.Insertion, r.Value.Offset), false);
        RestoreMultiSel(ids);
    }

    private void ReplicateSelection()
    {
        if (_p == null) return;
        var ids = SelectedBeamIds();
        if (ids.Count == 0) { SetStatus("Chọn dầm trước (chế độ 'Chọn' hoặc 'Chọn nhiều'), rồi bấm 'Sao chép / Đối xứng…'."); return; }
        string st = _p.Frames.First(f => f.Id == ids[0]).Story;
        var op = BeamToolDialogs.AskReplicate(this, _p, ids.Count, st);
        if (op == null) return;
        RunBeamOp(() => op(_p, ids));
    }

    private void RestoreMultiSel(List<int> ids)
    {
        if (ids.Count < 2) return;
        _multiSel.Clear(); _multiSelOwner = _p;
        foreach (var id in ids) if (_p.Frames.Any(f => f.Id == id)) _multiSel.Add(id);
        DrawHover();
    }

    // ==================================================================
    // Khung chọn (window / crossing)
    // ==================================================================
    /// <summary>Gọi cuối PlanHost_MouseLeftButtonDown: bắt đầu khung chọn trong chế độ Vẽ nhanh / Chọn nhiều (Shift = di chuyển).</summary>
    private void BeginBeamWindow()
    {
        _winDrag = IsWindowMode && (Keyboard.Modifiers & ModifierKeys.Shift) == 0;
        _winStart = _panStart; _winEnd = _panStart;
    }

    /// <summary>Gọi trong PlanHost_MouseLeftButtonUp. true = đã xử lý (khung chọn).</summary>
    private bool EndBeamWindow(Point end)
    {
        if (!_winDrag) return false;
        _winDrag = false;
        if (!_dragged) return false;   // bấm (không kéo) → PlanClick
        _winEnd = end;
        ApplyWindow();
        DrawHover();
        return true;
    }

    private Rect WindowRect() => new(new Point(Math.Min(_winStart.X, _winEnd.X), Math.Min(_winStart.Y, _winEnd.Y)),
                                     new Point(Math.Max(_winStart.X, _winEnd.X), Math.Max(_winStart.Y, _winEnd.Y)));

    private bool WindowCrossing => _winEnd.X < _winStart.X;   // phải → trái = cắt (như AutoCAD / ETABS)

    private void ApplyWindow()
    {
        if (_p == null) return;
        string st = CurrentStory;
        var (xa, ya) = S2W(_winStart); var (xb, yb) = S2W(_winEnd);
        bool crossing = WindowCrossing;
        if (_mode == PlanEditMode.QuickGridBeam)
        {
            if (st == AllStories || _p.Stories.All(q => q.Name != st)) { SetStatus("Chọn một tầng cụ thể trước khi vẽ dầm."); return; }
            string sec = cbEditBeamSec.SelectedItem as string;
            if (string.IsNullOrEmpty(sec)) { SetStatus("Chọn tiết diện dầm trước."); return; }
            var segs = BeamTools.GridSegmentsInWindow(_p, xa, ya, xb, yb, crossing);
            if (segs.Count == 0) { SetStatus("Không có đoạn trục nào " + (crossing ? "chạm" : "nằm trọn trong") + " khung chọn."); return; }
            var stories = ScopeStories(st);
            RunBeamOp(() => BeamTools.ForStories(_p, stories, s2 => BeamTools.QuickBeamsOnGridSegments(_p, s2, segs, sec)));
            return;
        }
        if (_mode == PlanEditMode.MultiSelect)
        {
            CheckMultiSelOwner();
            var rc = WindowRect();
            bool remove = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            int n = 0;
            foreach (var f in PlanFrames().Where(q => !q.IsVerticalKind))
            {
                var pts = BeamScreenPoints(f);
                if (pts.Count < 2) continue;
                bool hit = crossing ? Enumerable.Range(0, pts.Count - 1).Any(i => SegHitsRect(pts[i], pts[i + 1], rc)) : pts.All(q => rc.Contains(q));
                if (!hit) continue;
                n++;
                if (remove) _multiSel.Remove(f.Id); else _multiSel.Add(f.Id);
            }
            SetStatus($"{(remove ? "Bỏ chọn" : "Chọn")} {n} dầm bằng khung {(crossing ? "cắt (crossing)" : "bao (window)")} – đang chọn {_multiSel.Count} dầm.");
        }
    }

    /// <summary>Đoạn thẳng (màn hình) có chạm hình chữ nhật không (Liang–Barsky).</summary>
    private static bool SegHitsRect(Point a, Point b, Rect r)
    {
        if (r.Contains(a) || r.Contains(b)) return true;
        double t0 = 0, t1 = 1, dx = b.X - a.X, dy = b.Y - a.Y;
        bool Clip(double p, double q)
        {
            if (Math.Abs(p) < 1e-12) return q >= 0;
            double t = q / p;
            if (p < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
            else { if (t < t0) return false; if (t < t1) t1 = t; }
            return true;
        }
        return Clip(-dx, a.X - r.Left) && Clip(dx, r.Right - a.X) && Clip(-dy, a.Y - r.Top) && Clip(dy, r.Bottom - a.Y) && t0 <= t1;
    }

    // ==================================================================
    // Lớp phủ (gọi đầu DrawHover)
    // ==================================================================
    /// <summary>Vẽ lớp phủ của công cụ dầm: dầm đang chọn nhiều, khung chọn, đoạn trục / ô dưới chuột, điểm bắt.
    /// true = chế độ công cụ dầm (DrawHover dừng tại đây).</summary>
    private bool DrawBeamToolOverlay()
    {
        if (_p == null) return false;
        CheckMultiSelOwner();
        foreach (var f in PlanFrames().Where(q => !q.IsVerticalKind && _multiSel.Contains(q.Id)))
        {
            var pts = BeamScreenPoints(f);
            if (pts.Count < 2) continue;
            if (!f.IsCurved)
            {
                var ni = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeI); var nj = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
                if (ni != null && nj != null) { var (pa, pb) = BeamPlanEnds(f, ni, nj); pts = new PointCollection { pa, pb }; }
            }
            planOverlay.Children.Add(new Shapes.Polyline
            {
                Points = pts, Stroke = MultiSelBrush, StrokeThickness = BeamPlanWidth(f) + 5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                Opacity = 0.75, IsHitTestVisible = false
            });
        }
        if (_winDrag && _dragged)
        {
            var rc = WindowRect();
            bool cr = WindowCrossing;
            var rect = new Shapes.Rectangle
            {
                Width = rc.Width, Height = rc.Height, Stroke = cr ? CrossBrush : WinBrush, Fill = cr ? CrossFill : WinFill, StrokeThickness = 1.2, IsHitTestVisible = false
            };
            if (cr) rect.StrokeDashArray = new DoubleCollection { 4, 3 };
            Canvas.SetLeft(rect, rc.Left); Canvas.SetTop(rect, rc.Top);
            planOverlay.Children.Add(rect);
        }
        if (!IsBeamToolMode) return false;
        string st = CurrentStory;
        if (_lastMouse == null || st == AllStories || _p.Stories.All(q => q.Name != st) || (_panning && !_winDrag)) return true;
        var (x, y) = S2W(_lastMouse.Value);
        try
        {
            switch (_mode)
            {
                case PlanEditMode.QuickGridBeam:
                {
                    var segs = _winDrag && _dragged
                        ? BeamTools.GridSegmentsInWindow(_p, S2W(_winStart).x, S2W(_winStart).y, S2W(_winEnd).x, S2W(_winEnd).y, WindowCrossing)
                        : new List<GridSegment>();
                    if (!(_winDrag && _dragged))
                    {
                        var seg = BeamTools.GridSegmentAt(_p, x, y, SnapTol());
                        if (seg != null) segs.Add(seg);
                    }
                    foreach (var sg in segs)
                    {
                        var a = W2S(sg.X1, sg.Y1); var b = W2S(sg.X2, sg.Y2);
                        bool free = BeamTools.Uncovered(_p, st, sg.X1, sg.Y1, sg.X2, sg.Y2).Count > 0;
                        var l = CLine(planOverlay, a, b, free ? RubberBeam : Brushes.Gray, 6, false);
                        l.Opacity = 0.6;
                        if (segs.Count == 1)
                            AddText($"{sg}  L = {sg.Length * 1000:0} mm" + (free ? "" : " – đã có dầm"), new Point((a.X + b.X) / 2 + 8, (a.Y + b.Y) / 2 - 18), RubberBeam, 11, false, false, planOverlay);
                    }
                    var lp = _winDrag && _dragged ? _winEnd : _lastMouse.Value;
                    if (segs.Count > 1) AddText($"{segs.Count} đoạn trục", new Point(lp.X + 12, lp.Y + 12), RubberBeam, 11, false, false, planOverlay);
                    break;
                }
                case PlanEditMode.SecondaryBeam:
                {
                    var bay = BeamTools.BayAt(_p, st, x, y);
                    if (bay == null) break;
                    var bv = bay.Value;
                    var tl = W2S(bv.X0, bv.Y1); var br = W2S(bv.X1, bv.Y0);
                    var rect = new Shapes.Rectangle { Width = Math.Max(1, br.X - tl.X), Height = Math.Max(1, br.Y - tl.Y), Fill = HoverFill, Stroke = HoverBrush, StrokeThickness = 2, IsHitTestVisible = false };
                    Canvas.SetLeft(rect, tl.X); Canvas.SetTop(rect, tl.Y);
                    planOverlay.Children.Add(rect);
                    var o = _secOpts ?? new SecondaryBeamOptions { AlongX = bv.X1 - bv.X0 < bv.Y1 - bv.Y0, Count = 1 };
                    double? rc = string.IsNullOrEmpty(o.RefGrid) ? null : BeamTools.FindGrid(_p, o.RefGrid)?.Coord;
                    foreach (double c in BeamTools.SecondaryPositions(o.AlongX ? bv.Y0 : bv.X0, o.AlongX ? bv.Y1 : bv.X1, o, rc))
                    {
                        var a = o.AlongX ? W2S(bv.X0, c) : W2S(c, bv.Y0); var b = o.AlongX ? W2S(bv.X1, c) : W2S(c, bv.Y1);
                        var l = CLine(planOverlay, a, b, RubberBeam, 2, false);
                        l.StrokeDashArray = new DoubleCollection { 4, 2 };
                    }
                    AddText($"Ô {bv.X1 - bv.X0:0.00} × {bv.Y1 - bv.Y0:0.00} m – bấm để thêm dầm phụ", new Point(tl.X + 6, tl.Y + 4), RubberBeam, 11, false, false, planOverlay);
                    break;
                }
                case PlanEditMode.CantileverBeam:
                {
                    var sp = _etabsSnap ? EtabsSnap(st, x, y, SnapTol()) : ModelEdit.Snap(_p, st, x, y, SnapTol());
                    DrawSnapMarker(st, sp, RubberBeam, false);
                    break;
                }
                case PlanEditMode.MultiSelect:
                {
                    if (_winDrag && _dragged) break;
                    var b = NearestBeam(_lastMouse.Value, 8);
                    if (b != null) DrawHighlight(planOverlay, b, true);
                    break;
                }
            }
        }
        catch (KeyNotFoundException) { }
        catch (ArgumentException) { }
        return true;
    }

    // ==================================================================
    // Chuột phải (kết thúc), chuột giữa (di chuyển)
    // ==================================================================
    private void PlanHost_RightUp(object sender, MouseButtonEventArgs e)
    {
        if (_coordBox != null && _coordBox.IsMouseOver) return;
        if (IsLineMode && _lineStart != null)
        {
            _lineStart = null; _arcEnd = null;
            HideCoordBox(false);
            DrawHover();
            SetStatus("Đã kết thúc chuỗi vẽ. " + ModeHint(_mode));
            e.Handled = true;
            return;
        }
        if (_winDrag) { _winDrag = false; DrawHover(); e.Handled = true; return; }
        if ((IsLineMode || IsBeamToolMode) && rbModeSelect != null)
        {
            rbModeSelect.IsChecked = true;   // chuột phải lần nữa: thoát công cụ (như ETABS)
            e.Handled = true;
        }
    }

    private void PlanHost_MiddleDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || _p == null) return;
        planHost.Focus();
        _panning = true; _dragged = true; _winDrag = false;
        _panStart = e.GetPosition(planHost);
        _panOx = _ox; _panOy = _oy;
        planHost.CaptureMouse();
        e.Handled = true;
    }

    private void PlanHost_MiddleUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || !_panning) return;
        _panning = false; _dragged = false;
        planHost.ReleaseMouseCapture();
        e.Handled = true;
        DrawHover();
    }

    // ==================================================================
    // Ô nhập toạ độ (＋ Dầm)
    // ==================================================================
    private void PlanHost_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_p == null || _coordBox == null || _mode != PlanEditMode.AddBeam || string.IsNullOrEmpty(e.Text)) return;
        if (!ReferenceEquals(Keyboard.FocusedElement, planHost)) return;   // chữ gõ trong ô nhập đi qua đây (tunnel) – bỏ qua
        if (_coordBox.Visibility == Visibility.Visible)
        {
            // ô nhập đã mở nhưng chưa nhận tiêu điểm: nối chữ vào ô
            _coordText.Text += e.Text;
            FocusCoordText();
            e.Handled = true;
            return;
        }
        char ch = e.Text[0];
        if (!(char.IsDigit(ch) || ch == '@' || ch == '#' || ch == '-' || ch == '.' || ch == '<')) return;
        string st = CurrentStory;
        if (st == AllStories || _p.Stories.All(q => q.Name != st)) { SetStatus("Chọn một tầng cụ thể trước khi nhập toạ độ."); return; }
        ShowCoordBox(e.Text);
        e.Handled = true;
    }

    private void ShowCoordBox(string initial)
    {
        string un = _coordUnit == CoordUnit.Mm ? "mm" : "m";
        bool first = _lineStart == null || _lineStory != CurrentStory;
        _coordLabel.Text = first ? $"Điểm đầu: x,y ({un}) – Enter / Esc"
                                 : $"Điểm cuối ({un}): x,y | @dx,dy | L<góc | L – Enter / Esc";
        var m = _lastMouse ?? new Point(40, 40);
        double w = planHost.ActualWidth, h = planHost.ActualHeight;
        _coordBox.Margin = new Thickness(Math.Clamp(m.X + 16, 0, Math.Max(0, w - 260)), Math.Clamp(m.Y + 18, 0, Math.Max(0, h - 60)), 0, 0);
        _coordBox.Visibility = Visibility.Visible;
        _coordText.Text = initial ?? "";
        _coordBox.UpdateLayout();
        FocusCoordText();
    }

    private void FocusCoordText()
    {
        if (_coordText.Focus()) { _coordText.CaretIndex = _coordText.Text.Length; return; }
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_coordBox.Visibility != Visibility.Visible) return;
            _coordText.Focus();
            _coordText.CaretIndex = _coordText.Text.Length;
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void HideCoordBox(bool focusPlan)
    {
        if (_coordBox == null || _coordBox.Visibility != Visibility.Visible) return;
        _coordBox.Visibility = Visibility.Collapsed;
        if (focusPlan) planHost.Focus();
    }

    private void CoordText_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { HideCoordBox(true); SetStatus(ModeHint(_mode)); e.Handled = true; return; }
        if (e.Key != Key.Enter && e.Key != Key.Return) return;
        e.Handled = true;
        ApplyCoordInput(_coordText.Text);
    }

    /// <summary>Áp dụng toạ độ gõ: chưa có điểm đầu → đặt điểm đầu; có rồi → thêm dầm tới điểm vừa nhập (vẽ nối tiếp).</summary>
    private void ApplyCoordInput(string text)
    {
        if (_p == null) return;
        string st = CurrentStory;
        if (st == AllStories || _p.Stories.All(q => q.Name != st)) { HideCoordBox(true); return; }
        (double X, double Y)? last = _lineStart != null && _lineStory == st ? _lineStart : null;
        (double X, double Y)? dir = null;
        if (_lastMouse != null) { var (mx, my) = S2W(_lastMouse.Value); dir = (mx, my); }
        var ci = BeamTools.ParseCoordinateInput(text, last, _coordUnit, dir);
        if (!ci.Ok) { SetStatus(ci.Error); return; }   // giữ ô nhập để sửa
        HideCoordBox(true);
        var pt = (ci.X, ci.Y);
        if (last == null)
        {
            _lineStart = pt; _lineStory = st; _arcEnd = null;
            SetStatus($"Điểm đầu ({ci.X:0.000}; {ci.Y:0.000}) – bấm hoặc gõ điểm cuối (x,y | @dx,dy | L<góc | L); chuột phải để kết thúc.");
            DrawHover();
            return;
        }
        string sec = cbEditBeamSec.SelectedItem as string;
        if (string.IsNullOrEmpty(sec)) { SetStatus("Chọn tiết diện dầm ở ô 'Tiết diện dầm' trước."); return; }
        if (_busy) { SetStatus("Chương trình đang tính toán – chờ xong rồi sửa mô hình."); return; }
        string snap = Snapshot();
        EditResult r;
        try { r = AddBeamScoped(st, last.Value, pt, sec); }
        catch (Exception ex) { RestoreSnapshot(snap); ShowError("Không thêm được dầm", ex); return; }
        if (!r.Ok) { if (Snapshot() != snap) RestoreSnapshot(snap); SetStatus(r.Message); return; }
        AfterEdit(snap, r, null);
        _lineStart = pt; _lineStory = st;   // vẽ nối tiếp
        DrawHover();
    }
}

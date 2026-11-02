using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Hộp thoại "Thiết kế móng": loại móng cho cả công trình, thông số móng cọc và móng băng.
/// Thông số nền đất (φ, c, γ, Df, R) nhập ở tab "Công trình".</summary>
internal sealed class FoundationDialog : Window
{
    public static readonly string[] TypeNames = { "Móng đơn", "Móng cọc", "Móng băng" };
    public static readonly string[] StripDirNames = { "Tự chọn", "Dọc trục X", "Dọc trục Y", "Giao thoa 2 phương" };

    private readonly FoundationSettings _f;
    private readonly ComboBox _cbType, _cbPileShape, _cbStripDir;
    private readonly TextBox _tPileSize, _tPileLen, _tCap, _tTension, _tSpacing, _tEdge, _tEmbed, _tCapH;
    private readonly TextBox _tRib, _tK, _tOver;
    private readonly GroupBox _gPile, _gStrip;
    private readonly CheckBox _chkGeo, _chkBored;
    private readonly TextBox _tTip, _tFs;
    // [FoundationChecks] sức chịu tải ngang cọc, tuỳ chọn bản vẽ
    private readonly TextBox _tLat;
    private readonly CheckBox _chkPyr, _chkMom;
    private readonly CheckBox _chkCuTram;   // [CừTràm]

    /// <summary>true = người dùng bấm "Thiết kế móng" (chạy thiết kế sau khi đóng)</summary>
    public bool RunDesign { get; private set; }

    public FoundationDialog(FoundationSettings f, int overrides)
    {
        _f = f;
        DialogUi.Setup(this, "Thiết kế móng", 560, 640);
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;

        _cbType = new ComboBox { ItemsSource = TypeNames, SelectedIndex = (int)f.Type, Width = 160 };
        _cbType.SelectionChanged += (s, e) => UpdateGroups();
        _cbPileShape = new ComboBox { ItemsSource = new[] { "Cọc vuông", "Cọc tròn" }, SelectedIndex = f.PileRound ? 1 : 0, Width = 120 };
        _tPileSize = Box(f.PileSize * 1000, "0");
        _tPileLen = Box(f.PileLength, "0.##");
        _tCap = Box(f.PileCapacity, "0.#");
        _tTension = Box(f.PileTension, "0.#");
        _tSpacing = Box(f.PileSpacingFactor, "0.##");
        _tEdge = Box(f.CapEdge * 1000, "0");
        _tEmbed = Box(f.PileEmbed * 1000, "0");
        _tCapH = Box(f.CapMinHeight * 1000, "0");
        _chkGeo = new CheckBox { Content = "Tính [P] và chiều dài cọc từ hố khoan (SPT – TCVN 10304:2014 G.3.2)", IsChecked = f.PileCapacityFromBorehole, VerticalAlignment = VerticalAlignment.Center };
        _chkBored = new CheckBox { Content = "Cọc khoan nhồi (qb = 150·Np; bỏ chọn = cọc ép / đóng, qb = 300·Np)", IsChecked = f.PileBored, VerticalAlignment = VerticalAlignment.Center };
        _tTip = Box(f.PileTipDepth, "0.##");
        _tFs = Box(f.PileSafetyFactor, "0.##");
        _tLat = Box(f.PileLateralCapacity, "0.#");
        _chkPyr = new CheckBox { Content = "Bản vẽ: thể hiện tháp chọc thủng 45° trên mặt cắt đài", IsChecked = f.DrawPunchingPyramid, VerticalAlignment = VerticalAlignment.Center };
        _chkMom = new CheckBox { Content = "Bản vẽ: biểu đồ mô men sườn dưới mặt cắt dọc móng băng", IsChecked = f.DrawStripMoment, VerticalAlignment = VerticalAlignment.Center };
        _cbStripDir = new ComboBox { ItemsSource = StripDirNames, SelectedIndex = Math.Clamp(f.StripDirection, 0, 3), Width = 160 };
        _tRib = Box(f.StripRibWidth * 1000, "0");
        _tK = Box(f.SubgradeModulus, "0");
        _tOver = Box(f.StripOverhang, "0.##");

        var root = new StackPanel { Margin = new Thickness(10) };
        root.Children.Add(Line("Loại móng (cả công trình)", _cbType));
        if (overrides > 0) root.Children.Add(DialogUi.Hint($"Có {overrides} chân cột chọn loại móng riêng (đặt ở tab 'Mô hình móng' → 'Tại chân cột này')."));

        var pile = new StackPanel();
        pile.Children.Add(Line("Loại cọc", _cbPileShape));
        pile.Children.Add(Line("Cạnh / đường kính cọc (mm)", _tPileSize));
        pile.Children.Add(Line("Chiều dài cọc (m)", _tPileLen));
        pile.Children.Add(Line("[P] chịu nén 1 cọc (kN)", _tCap));
        pile.Children.Add(Line("[P] chịu kéo 1 cọc (kN, 0 = không cho kéo)", _tTension));
        pile.Children.Add(Line("Khoảng cách tim cọc ≥ hệ số × cạnh cọc", _tSpacing));
        pile.Children.Add(Line("Mép đài tới mép cọc (mm)", _tEdge));
        pile.Children.Add(Line("Đoạn cọc ngàm vào đài (mm)", _tEmbed));
        pile.Children.Add(Line("Chiều cao đài tối thiểu (mm)", _tCapH));
        pile.Children.Add(DialogUi.Hint("[P] lấy theo thí nghiệm nén tĩnh hoặc tính theo TCVN 10304:2014 (giá trị nhỏ hơn giữa vật liệu và đất nền)."));
        pile.Children.Add(_chkGeo);
        pile.Children.Add(_chkBored);
        pile.Children.Add(Line("Độ sâu mũi cọc từ mặt đất (m, 0 = tự chọn)", _tTip));
        pile.Children.Add(Line("Hệ số an toàn FS ([P]đn = Rc,u/FS)", _tFs));
        pile.Children.Add(Line("[H] chịu tải ngang 1 cọc (kN, 0 = ước lượng 0,1[P])", _tLat));
        pile.Children.Add(_chkPyr);
        int nOvr = f.CapOverrides?.Count(o => PileLayout.IsActive(o)) ?? 0;
        pile.Children.Add(DialogUi.Hint("Số cọc / sơ đồ bố trí / kích thước từng đài: chọn đài trên tab 'Mô hình móng' → 'Bố trí cọc…'" +
                                        (nOvr > 0 ? $" (đang có {nOvr} chân cột bố trí riêng)." : ".")));
        pile.Children.Add(DialogUi.Hint("Khi tính từ hố khoan: mỗi chân cột dùng hố khoan gần nhất (tab 'Địa chất'); đầu cọc tại đáy đài Df; mũi tự chọn = mặt lớp đầu tiên có N ≥ 30 (đất rời) / N ≥ 20 (đất dính) + 3d, ≤ 40 m; " +
                                        "[P] = min(Rc,u/FS; Pvl), Pvl = Rb·Ab + Rsc·As (B25, As mặc định 4Ø16 khi d ≤ 300 mm, 6Ø18 khi lớn hơn). FS là hệ số an toàn tổng thể, tương đương γk·γn/γ0 của TCVN 10304:2014."));
        _gPile = new GroupBox { Header = "Móng cọc", Content = pile, Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(4) };
        root.Children.Add(_gPile);

        var strip = new StackPanel();
        strip.Children.Add(Line("Phương dải móng băng", _cbStripDir));
        strip.Children.Add(Line("Bề rộng sườn (mm)", _tRib));
        strip.Children.Add(Line("Hệ số nền (kN/m³, 0 = ≈ 120·R)", _tK));
        strip.Children.Add(Line("Độ vươn đầu dải ra ngoài tim cột biên (m)", _tOver));
        strip.Children.Add(_chkMom);
        _gStrip = new GroupBox { Header = "Móng băng", Content = strip, Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(4) };
        root.Children.Add(_gStrip);
        // [CừTràm] gia cố nền cừ tràm (thông số chi tiết: tab 'Mô hình móng' → 'Cừ tràm…')
        _chkCuTram = new CheckBox { Content = "Gia cố nền bằng cừ tràm dưới móng đơn / móng băng (cả công trình)", IsChecked = f.CuTram?.Enabled == true, Margin = new Thickness(0, 6, 0, 0) };
        root.Children.Add(_chkCuTram);
        root.Children.Add(DialogUi.Hint("Thông số cừ tràm (chiều dài, Ø, mật độ, MNN thấp nhất, phương pháp tính) và chọn riêng từng chân cột: tab 'Mô hình móng' → 'Cừ tràm…'."));

        root.Children.Add(DialogUi.Hint("Móng lệch tâm (cột sát ranh giới đất): khai báo ở tab 'Mô hình móng' → 'Ranh giới đất…'; phương án từng chân cột (dầm cân bằng / tự cân bằng / móng kết hợp) → 'Lệch tâm…'"   // [MóngLệchTâm]
                                        + ((f.Boundaries?.Count(b => b != null && b.Enabled) ?? 0) > 0 ? $" (đang có {f.Boundaries.Count(b => b != null && b.Enabled)} cạnh ranh giới)." : ".")));
        root.Children.Add(DialogUi.Hint("Thông số nền đất (góc ma sát φ, lực dính c, γ, độ sâu chôn móng Df, R nhập tay) nằm ở tab 'Công trình' → nhóm 'Nền đất dưới móng'. " +
                                        "Bê tông móng chọn ở nhóm 'Vật liệu'."));

        var bDesign = DialogUi.Btn("Thiết kế móng", (s, e) => { if (Apply()) { RunDesign = true; DialogResult = true; } }, true);
        bDesign.IsDefault = true;
        var bSave = DialogUi.Btn("Lưu thông số", (s, e) => { if (Apply()) DialogResult = true; });
        var bCancel = DialogUi.Btn("Huỷ", (s, e) => DialogResult = false);
        bCancel.IsCancel = true;
        var bs = DialogUi.Row(bDesign, bSave, bCancel);
        bs.HorizontalAlignment = HorizontalAlignment.Right;
        bs.Margin = new Thickness(0, 10, 0, 0);
        root.Children.Add(bs);
        Content = root;
        UpdateGroups();
    }

    private static TextBox Box(double v, string fmt) => new() { Text = DialogUi.F(v, fmt), Width = 90, VerticalContentAlignment = VerticalAlignment.Center };

    private static DockPanel Line(string label, FrameworkElement ctl)
    {
        var d = new DockPanel { Margin = new Thickness(2) };
        var t = DialogUi.Label(label, 300);
        d.Children.Add(t);
        ctl.HorizontalAlignment = HorizontalAlignment.Left;
        d.Children.Add(ctl);
        return d;
    }

    private void UpdateGroups()
    {
        int t = _cbType.SelectedIndex;
        // chỉ làm nổi nhóm của loại móng đang chọn (nhóm kia vẫn sửa được – dùng cho chân cột chọn loại riêng)
        _gPile.Opacity = t == 1 ? 1 : 0.65;
        _gStrip.Opacity = t == 2 ? 1 : 0.65;
        _gPile.FontWeight = t == 1 ? FontWeights.SemiBold : FontWeights.Normal;
        _gStrip.FontWeight = t == 2 ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private bool Apply()
    {
        var err = new List<string>();
        double Get(TextBox tb, double min, double max, string name)
        {
            double v = DialogUi.ParseNum(tb.Text, double.NaN);
            if (double.IsNaN(v) || v < min || v > max) { err.Add($"{name}: {DialogUi.F(min)} … {DialogUi.F(max)}"); return min; }
            return v;
        }
        double size = Get(_tPileSize, 150, 2500, "Cạnh / đường kính cọc (mm)");
        double len = Get(_tPileLen, 2, 100, "Chiều dài cọc (m)");
        double cap = Get(_tCap, 10, 50000, "[P] nén (kN)");
        double ten = Get(_tTension, 0, 50000, "[P] kéo (kN)");
        double sp = Get(_tSpacing, 2.5, 8, "Hệ số khoảng cách cọc");
        double edge = Get(_tEdge, 50, 1000, "Mép đài (mm)");
        double emb = Get(_tEmbed, 0, 1500, "Ngàm cọc (mm)");
        double capH = Get(_tCapH, 300, 3000, "Chiều cao đài (mm)");
        double tip = Get(_tTip, 0, 80, "Độ sâu mũi cọc (m)");
        double fsf = Get(_tFs, 1.2, 5, "Hệ số an toàn FS");
        double rib = Get(_tRib, 150, 1500, "Bề rộng sườn (mm)");
        double k = Get(_tK, 0, 1e7, "Hệ số nền (kN/m³)");
        double over = Get(_tOver, 0, 3, "Độ vươn đầu dải (m)");
        double lat = Get(_tLat, 0, 50000, "[H] tải ngang cọc (kN)");
        if (err.Count > 0)
        {
            MessageBox.Show(this, "Giá trị ngoài phạm vi cho phép:\n" + string.Join("\n", err.Select(e => "  • " + e)), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        _f.Type = (FoundationType)Math.Clamp(_cbType.SelectedIndex, 0, 2);
        _f.PileRound = _cbPileShape.SelectedIndex == 1;
        _f.PileCapacityFromBorehole = _chkGeo.IsChecked == true; _f.PileBored = _chkBored.IsChecked == true;
        _f.PileTipDepth = tip; _f.PileSafetyFactor = fsf;
        _f.PileSize = size / 1000; _f.PileLength = len; _f.PileCapacity = cap; _f.PileTension = ten;
        _f.PileSpacingFactor = sp; _f.CapEdge = edge / 1000; _f.PileEmbed = emb / 1000; _f.CapMinHeight = capH / 1000;
        _f.StripDirection = Math.Clamp(_cbStripDir.SelectedIndex, 0, 3);
        _f.StripRibWidth = rib / 1000; _f.SubgradeModulus = k; _f.StripOverhang = over;
        _f.PileLateralCapacity = lat; _f.DrawPunchingPyramid = _chkPyr.IsChecked == true; _f.DrawStripMoment = _chkMom.IsChecked == true;
        _f.CuTram ??= new CuTramSettings();
        _f.CuTram.Enabled = _chkCuTram.IsChecked == true;   // [CừTràm]
        return true;
    }
}

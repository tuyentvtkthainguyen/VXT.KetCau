using System.Windows;
using System.Windows.Controls;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>[LùiCọcRanh] Phần bổ sung hộp thoại "Ranh giới đất…": bật lùi tim cọc khỏi ranh để ép cọc (khoảng lùi chung, đo tới tim / mép cọc,
/// phương pháp ép), khoảng lùi riêng từng cạnh (trống = theo chung, 0 = phía không có nhà bên cạnh) và tuỳ chọn mép đài.</summary>
internal sealed partial class BoundaryDialog
{
    private CheckBox _chkSb, _chkSbFace;
    private TextBox _tSb;
    private ComboBox _cbPress, _cbCap;
    private readonly Dictionary<BoundarySide, TextBox> _tSbSide = new();

    private void AddSetbackUi(StackPanel left, FoundationSettings fs)
    {
        Height += 190;
        var box = new StackPanel();
        _chkSb = new CheckBox { Content = "Lùi tim cọc khỏi ranh giới để ép cọc (nhà giáp ranh không ép sát ranh được)", IsChecked = fs.PileSetbackEnabled, Margin = new Thickness(0, 2, 0, 2) };
        _tSb = new TextBox { Width = 90, Text = DialogUi.F(fs.PileSetback * 1000, "0") };
        _chkSbFace = new CheckBox { Content = "Đo tới mép cọc (bỏ chọn = tới tim cọc)", IsChecked = fs.PileSetbackToFace, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        _cbPress = new ComboBox { Width = 150, ItemsSource = new[] { "Ép neo", "Ép tải (đối trọng)", "Ép robot" }, SelectedIndex = (int)fs.PilePressMethod };
        _cbCap = new ComboBox { Width = 230, ItemsSource = new[] { "Đài vươn ra sát ranh (công xôn)", "Mép đài theo cọc" }, SelectedIndex = fs.CapToBoundary ? 0 : 1 };
        box.Children.Add(_chkSb);
        var r1 = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
        r1.Children.Add(DialogUi.Label("Khoảng lùi tối thiểu tới ranh (mm)", 230));
        r1.Children.Add(_tSb);
        r1.Children.Add(_chkSbFace);
        box.Children.Add(r1);
        var r2 = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
        r2.Children.Add(DialogUi.Label("Phương pháp ép cọc", 230));
        _cbPress.HorizontalAlignment = HorizontalAlignment.Left;
        r2.Children.Add(_cbPress);
        box.Children.Add(r2);
        var r3 = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
        r3.Children.Add(DialogUi.Label("Đài cọc biên", 230));
        _cbCap.HorizontalAlignment = HorizontalAlignment.Left;
        r3.Children.Add(_cbCap);
        box.Children.Add(r3);
        var sides = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        sides.Children.Add(DialogUi.Label("Lùi riêng từng cạnh (mm):", 170));
        foreach (BoundarySide sd in Enum.GetValues(typeof(BoundarySide)))
        {
            var cur = fs.Boundaries?.FirstOrDefault(b => b != null && b.Side == sd);
            var tb = new TextBox { Width = 48, Margin = new Thickness(2, 0, 6, 0), Text = cur != null && cur.PileSetback >= 0 ? DialogUi.F(cur.PileSetback * 1000, "0") : "", ToolTip = PropertyBoundary.SideName(sd) + ": trống = theo khoảng lùi chung; 0 = không cần lùi (không có nhà bên cạnh)" };
            sides.Children.Add(DialogUi.Label(sd switch { BoundarySide.Left => "Trái", BoundarySide.Right => "Phải", BoundarySide.Front => "Trước", _ => "Sau" }));
            sides.Children.Add(tb);
            _tSbSide[sd] = tb;
        }
        box.Children.Add(sides);
        box.Children.Add(DialogUi.Hint("Mặc định 500 mm (ép neo). Ép robot cần khoảng lùi lớn hơn (thường ≥ 1,0–1,5 m tuỳ máy) – kiểm tra với đơn vị thi công. "
                                       + "Cọc lùi vào làm nhóm cọc lệch tâm lớn so với cột sát ranh → chương trình đề xuất dầm móng cân bằng / đài chung / đài console và kiểm tra công xôn đài."));
        left.Children.Add(new GroupBox { Header = "Lùi cọc khỏi ranh giới (móng cọc)", Content = box, Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(4) });
    }

    /// <summary>Ghi các ô khoảng lùi vào thông số móng (gọi trong Build()).</summary>
    private void ReadSetback(FoundationSettings fs)
    {
        if (_chkSb == null) return;
        fs.PileSetbackEnabled = _chkSb.IsChecked == true;
        fs.PileSetback = Math.Clamp(DialogUi.ParseNum(_tSb.Text, 500) / 1000, 0, 5);
        fs.PileSetbackToFace = _chkSbFace.IsChecked == true;
        fs.PilePressMethod = (PilePressMethod)Math.Clamp(_cbPress.SelectedIndex, 0, 2);
        fs.CapToBoundary = _cbCap.SelectedIndex != 1;
        foreach (var b in fs.Boundaries)
        {
            if (!_tSbSide.TryGetValue(b.Side, out var tb) || string.IsNullOrWhiteSpace(tb.Text)) { b.PileSetback = -1; continue; }
            double v = DialogUi.ParseNum(tb.Text, double.NaN);
            b.PileSetback = double.IsNaN(v) || v < 0 ? -1 : Math.Min(v / 1000, 5);
        }
    }

    /// <summary>Bật lùi cọc + khoảng lùi (tự kiểm tra).</summary>
    public void SetSetback(bool on, double meters)
    {
        if (_chkSb == null) return;
        _chkSb.IsChecked = on;
        _tSb.Text = DialogUi.F(meters * 1000, "0");
    }
}

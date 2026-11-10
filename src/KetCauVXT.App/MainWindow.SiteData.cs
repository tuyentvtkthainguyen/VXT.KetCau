using System.Windows;
using System.Windows.Controls;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>[Số liệu khu đất] Sau khi dựng mô hình từ bản vẽ (AutoCAD / DXF kiến trúc): hỏi vùng gió, dạng địa hình, nền đất (bảng tra TCVN hoặc
/// hố khoan) rồi chạy phân tích + thiết kế + tự tăng tiết diện. Tải tường, sàn đã tự gán khi dựng mô hình.</summary>
public partial class MainWindow
{
    /// <summary>true = chạy tính toán ngay.</summary>
    private bool AskSiteData()
    {
        if (_p == null || SelfTestOut != null) return false;
        var w = new Window { Title = "Số liệu tính toán – gió, địa chất", Owner = this, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var sp = new StackPanel { Margin = new Thickness(14), Width = 560 };
        sp.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
            Text = "Mô hình đã dựng từ bản vẽ: cột, dầm, sàn và tải tường (theo vị trí tường), tải hoàn thiện + hoạt tải sàn đã tự gán. "
                 + "Nhập số liệu khu vực tính gió và địa chất để chương trình tính nội lực, thiết kế thép và móng."
        });
        var cbZone = new ComboBox { Width = 330 };
        cbZone.Items.Add("(giữ W0 hiện tại: " + _p.Wind.W0.ToString("0.###") + " kN/m²)");
        foreach (var z in WindZones.All) cbZone.Items.Add(z.Label);
        var cur = WindZones.Find(_p.Wind.Zone);
        cbZone.SelectedIndex = cur == null ? 0 : WindZones.All.ToList().IndexOf(cur) + 1;
        var cbTerrain = new ComboBox { Width = 330, ItemsSource = new[] { "A – trống trải (ven biển, đồng ruộng)", "B – tương đối trống trải (vật cản thưa < 10 m)", "C – bị che chắn mạnh (nhà cao ≥ 10 m dày đặc)" } };
        cbTerrain.SelectedIndex = _p.Wind.Terrain switch { "A" => 0, "C" => 2, _ => 1 };
        var chkWind = new CheckBox { Content = "Tính tải gió", IsChecked = _p.Wind.Enabled, Margin = new Thickness(0, 4, 0, 4) };
        const string Bore = "(theo hố khoan / thí nghiệm – nhập ở tab Địa chất)";
        var cbSoil = new ComboBox { Width = 330, ItemsSource = new[] { Bore }.Concat(SoilStandard.Catalog.Select(s => s.Name)).ToList() };
        cbSoil.SelectedItem = SoilStandard.Find(_p.Soil.StdName)?.Name ?? Bore;
        var chkRun = new CheckBox { Content = "Chạy ngay: phân tích nội lực → thiết kế thép, móng → tự tăng tiết diện cấu kiện không đạt", IsChecked = true, Margin = new Thickness(0, 10, 0, 0) };
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Vùng gió (QCVN 02:2022)", 200), cbZone));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Dạng địa hình", 200), cbTerrain));
        sp.Children.Add(chkWind);
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Nền đất (TCVN 9362 bảng tra)", 200), cbSoil));
        sp.Children.Add(DialogUi.Hint("Địa điểm theo xã / phường (tự lấy vùng gió, động đất) và hố khoan nhiều lớp: tab Công trình, tab Địa chất."));
        sp.Children.Add(chkRun);
        bool ok = false;
        var bOk = DialogUi.Btn("Đồng ý", (s, e) => { ok = true; w.Close(); }, true);
        bOk.IsDefault = true;
        var bs = DialogUi.Row(bOk, DialogUi.Btn("Bỏ qua", (s, e) => w.Close()));
        bs.HorizontalAlignment = HorizontalAlignment.Right;
        bs.Margin = new Thickness(0, 12, 0, 0);
        sp.Children.Add(bs);
        w.Content = sp;
        w.ShowDialog();
        if (!ok) return false;
        if (cbZone.SelectedIndex >= 1)
        {
            var z = WindZones.All[cbZone.SelectedIndex - 1];
            _p.Wind.Zone = z.Zone; _p.Wind.W0 = z.W0;
        }
        _p.Wind.Terrain = cbTerrain.SelectedIndex switch { 0 => "A", 2 => "C", _ => "B" };
        _p.Wind.Enabled = chkWind.IsChecked == true;
        var std = SoilStandard.Find(cbSoil.SelectedItem as string);
        if (std != null) SoilStandard.ApplyTo(_p.Soil, std);
        MarkDirty();
        LoadUiFromProject();
        SetStatus($"Gió: W0 = {_p.Wind.W0:0.###} kN/m² (vùng {(_p.Wind.Zone.Length > 0 ? _p.Wind.Zone : "–")}, địa hình {_p.Wind.Terrain}); nền đất: {(std?.Name ?? "theo hố khoan")}.");
        return chkRun.IsChecked == true;
    }
}

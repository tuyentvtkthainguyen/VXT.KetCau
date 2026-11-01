using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using KetCauVXT.Model;

namespace KetCauVXT.App;

public partial class PortalFrameDialog : Window
{
    /// <summary>Thông số khung đã nhập (sau khi bấm Tạo khung)</summary>
    public ModelBuilder.PortalOptions Options { get; private set; } = new();

    /// <summary>Tiết diện cột, kèo đã tạo (tên = Options.ColumnSection / RafterSection)</summary>
    public List<FrameSection> Sections { get; } = new();

    public PortalFrameDialog()
    {
        InitializeComponent();
        var o = Options;
        Set(txtSpan, o.Span); Set(txtEave, o.EaveHeight); Set(txtSlope, o.SlopePercent);
        Set(txtBays, o.Bays); Set(txtBay, o.BaySpacing); Set(txtBracing, o.BracingSpacing);
        chkPinned.IsChecked = o.PinnedBase;
        Set(txtDead, o.RoofDead); Set(txtLive, o.RoofLive);
        Set(txtCe1, o.CeColWind); Set(txtCe2, o.CeColLee); Set(txtCe3, o.CeRoofWind); Set(txtCe4, o.CeRoofLee);
        // mặc định giống ModelBuilder.BuildPortalFrames
        Set(txtCH1, 300); Set(txtCH2, 600); Set(txtCBf, 200); Set(txtCTf, 10); Set(txtCTw, 6);
        Set(txtRH1, 600); Set(txtRH2, 350); Set(txtRBf, 180); Set(txtRTf, 8); Set(txtRTw, 5);
        cbColSteel.ItemsSource = Material.SteelGrades; cbColSteel.SelectedItem = "SS400";
        cbRafSteel.ItemsSource = Material.SteelGrades; cbRafSteel.SelectedItem = "SS400";
    }

    private static void Set(TextBox t, double v) => t.Text = v.ToString("0.###", CultureInfo.InvariantCulture);

    private static double Get(TextBox t, string name, double min, double max)
    {
        var s = (t.Text ?? "").Trim().Replace(',', '.');
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            throw new FormatException($"'{name}' không phải là số.");
        if (v < min || v > max) throw new FormatException($"'{name}' phải nằm trong khoảng {min} … {max}.");
        return v;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var o = new ModelBuilder.PortalOptions
            {
                Span = Get(txtSpan, "Nhịp khung", 6, 80),
                EaveHeight = Get(txtEave, "Chiều cao đỉnh cột", 3, 30),
                SlopePercent = Get(txtSlope, "Độ dốc mái", 1, 60),
                Bays = (int)Math.Round(Get(txtBays, "Số bước khung", 1, 60)),
                BaySpacing = Get(txtBay, "Bước khung", 2, 15),
                BracingSpacing = Get(txtBracing, "Khoảng cách giằng", 0.5, 15),
                PinnedBase = chkPinned.IsChecked == true,
                RoofDead = Get(txtDead, "Tĩnh tải mái", 0, 10),
                RoofLive = Get(txtLive, "Hoạt tải mái", 0, 10),
                CeColWind = Get(txtCe1, "Ce cột đón gió", -3, 3),
                CeColLee = Get(txtCe2, "Ce cột khuất gió", -3, 3),
                CeRoofWind = Get(txtCe3, "Ce mái đón gió", -3, 3),
                CeRoofLee = Get(txtCe4, "Ce mái khuất gió", -3, 3)
            };
            double ch1 = Get(txtCH1, "h1 cột", 100, 2000), ch2 = Get(txtCH2, "h2 cột", 100, 2000);
            double cbf = Get(txtCBf, "bf cột", 50, 800), ctf = Get(txtCTf, "tf cột", 3, 60), ctw = Get(txtCTw, "tw cột", 3, 40);
            double rh1 = Get(txtRH1, "h1 kèo", 100, 2000), rh2 = Get(txtRH2, "h2 kèo", 100, 2000);
            double rbf = Get(txtRBf, "bf kèo", 50, 800), rtf = Get(txtRTf, "tf kèo", 3, 60), rtw = Get(txtRTw, "tw kèo", 3, 40);
            if (2 * ctf >= Math.Min(ch1, ch2) || 2 * rtf >= Math.Min(rh1, rh2))
                throw new FormatException("Chiều dày cánh quá lớn so với chiều cao tiết diện.");

            string cs = cbColSteel.SelectedItem as string ?? "SS400";
            string rs = cbRafSteel.SelectedItem as string ?? "SS400";
            o.ColumnSection = $"CT-I{ch1:0}-{ch2:0}x{cbf:0}";
            o.RafterSection = $"KEO-I{rh1:0}-{rh2:0}x{rbf:0}";
            Sections.Clear();
            Sections.Add(FrameSection.WeldedISection(o.ColumnSection, ch1, ch2, cbf, ctf, ctw, cs));
            Sections.Add(FrameSection.WeldedISection(o.RafterSection, rh1, rh2, rbf, rtf, rtw, rs));
            Options = o;
            DialogResult = true;
        }
        catch (FormatException ex)
        {
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

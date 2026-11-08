using System.Windows.Controls;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.App;

// =====================================================================
// [Bảng tra] Tab "Công trình" – nền đất dưới móng khi chưa dùng hố khoan: chọn lớp đất cấu tạo tiêu chuẩn → φ, c, γ, E theo bảng tra
// TCVN 9362:2012 (ktc = 1,1). Sửa trực tiếp φ, c, γ (có thí nghiệm) → "(theo thí nghiệm)", ktc = 1,0. Lõi: Design/Foundation/SoilStandard.cs.
// =====================================================================
public partial class MainWindow
{
    private const string SoilStdTest = "(theo thí nghiệm – nhập trực tiếp)";
    private bool _soilStdLoading;

    private void LoadSoilStdUi()
    {
        if (cbSoilStd == null || _p == null) return;
        _soilStdLoading = true;
        try
        {
            if (cbSoilStd.ItemsSource == null) cbSoilStd.ItemsSource = new[] { SoilStdTest }.Concat(SoilStandard.Catalog.Select(s => s.Name)).ToList();
            var std = SoilStandard.Find(_p.Soil.StdName);
            cbSoilStd.SelectedItem = std?.Name ?? SoilStdTest;
        }
        finally { _soilStdLoading = false; }
        UpdateSoilStdInfo();
    }

    private void CbSoilStd_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_soilStdLoading || _loadingUi || _p == null) return;
        var std = SoilStandard.Find(cbSoilStd.SelectedItem as string);
        if (std == null)
        {
            _p.Soil.StdName = ""; _p.Soil.Ktc = 1.0;
        }
        else
        {
            SoilStandard.ApplyTo(_p.Soil, std);
            txtPhi.Text = F(_p.Soil.Phi); txtC.Text = F(_p.Soil.C); txtGamma.Text = F(_p.Soil.Gamma); txtROverride.Text = F(_p.Soil.ROverride);
            SetStatus($"Nền đất theo lớp tiêu chuẩn \"{std.Name}\" (bảng tra {SoilStandard.Std}): φ = {F(_p.Soil.Phi)}°, c = {F(_p.Soil.C)} kPa, γ = {F(_p.Soil.Gamma)} kN/m³, E = {F(_p.Soil.E)} MPa, ktc = 1,1.");
        }
        UpdateSoilStdInfo();
        MarkDirty();
    }

    /// <summary>Gọi trong ApplyUiToProject: φ, c, γ đã sửa khác bảng tra → chuyển sang theo thí nghiệm (ktc = 1,0).</summary>
    private void ApplySoilStdUi()
    {
        var std = SoilStandard.Find(_p.Soil.StdName);
        if (std == null) return;
        var chk = new SoilSettings();
        SoilStandard.ApplyTo(chk, std);
        if (Math.Abs(chk.Phi - _p.Soil.Phi) > 0.05 || Math.Abs(chk.C - _p.Soil.C) > 0.05 || Math.Abs(chk.Gamma - _p.Soil.Gamma) > 0.005)
        {
            _p.Soil.StdName = ""; _p.Soil.Ktc = 1.0;
            LoadSoilStdUi();
        }
    }

    private void UpdateSoilStdInfo()
    {
        if (txtSoilStdInfo == null || _p == null) return;
        var std = SoilStandard.Find(_p.Soil.StdName);
        if (std == null)
        {
            txtSoilStdInfo.Text = $"Chỉ tiêu theo thí nghiệm (nhập trực tiếp) – ktc = {F(_p.Soil.Ktc)}. Chưa có thí nghiệm: chọn lớp đất tiêu chuẩn để lấy chỉ tiêu theo bảng tra {SoilStandard.Std}.";
            return;
        }
        var r = SoilStandard.Lookup(std.Kind, std.Grain, std.E0, std.IL, std.Sr);
        txtSoilStdInfo.Text = $"Theo bảng tra {SoilStandard.Std}: {SoilStandard.ClassName(r.Class)} {r.State}, e = {F(std.E0)}{(std.IL >= -0.5 ? ", IL = " + F(std.IL) : "")}; "
            + $"E = {F(_p.Soil.E)} MPa{(double.IsFinite(r.R0) ? ", R0 = " + F(r.R0) + " kPa" : "")}; ktc = 1,1."
            + (r.Warnings.Count > 0 ? " ⚠ " + string.Join("; ", r.Warnings) : "")
            + " Có hố khoan / thí nghiệm: tab Địa chất, hoặc nhập trực tiếp φ, c, γ.";
    }
}

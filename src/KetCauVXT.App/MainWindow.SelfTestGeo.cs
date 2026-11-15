using System.Windows;
using System.Windows.Controls;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest): địa danh → vùng gió, hố khoan, gợi ý phương án móng, phụ lục tính toán.</summary>
public partial class MainWindow
{
    private async Task SelfTestGeoAppendix(string tag)
    {
        // ---- địa danh → vùng gió ----
        tabLeft.SelectedIndex = 0; await Pause();
        cbLocProvince.SelectedItem = "Thái Nguyên"; await Pause(800);
        StLog($"địa danh: {_p.LocationProvince} / {_p.LocationName} → vùng {_p.Wind?.Zone}, W0 = {_p.Wind?.W0:0.00} kN/m²");
        Shot($"{tag}_12_DiaDanhGio");

        // ---- hố khoan khảo sát mẫu (có lớp bùn yếu) ----
        double cx = _p.GridsX.Any() ? _p.GridsX.Average(g => g.Coord) : 0, cy = _p.GridsY.Any() ? _p.GridsY.Average(g => g.Coord) : 0;
        var bh = new Borehole
        {
            Name = "HK-TEST", X = Math.Round(cx, 2), Y = Math.Round(cy, 2), WaterDepth = 2,
            Layers =
            {
                new SoilLayer { Name = "Đất đắp", Kind = SoilKind.Fill, Top = 0, Bottom = 1.2, Gamma = 17, Phi = 10, C = 8, E = 3, N = 4, IL = -1 },
                new SoilLayer { Name = "Sét pha dẻo cứng", Kind = SoilKind.SiltyClay, Top = 1.2, Bottom = 4, Gamma = 19, Phi = 16, C = 25, E = 10, N = 10, IL = 0.35 },
                new SoilLayer { Name = "Bùn sét", Kind = SoilKind.Organic, Top = 4, Bottom = 9, Gamma = 16, Phi = 4, C = 8, E = 1.5, N = 2, IL = 1.1 },
                new SoilLayer { Name = "Cát vừa chặt vừa", Kind = SoilKind.Sand, Grain = SandGrain.Medium, Top = 9, Bottom = 18, Gamma = 19, GammaSat = 20, Phi = 32, C = 1, E = 20, N = 20 },
                new SoilLayer { Name = "Cát thô", Kind = SoilKind.Sand, Grain = SandGrain.Coarse, Top = 18, Bottom = 30, Gamma = 20, GammaSat = 21, Phi = 37, E = 40, N = 40 }
            }
        };
        _p.Boreholes.Add(bh);
        LoadGeoUi();
        _geoList.SelectedIndex = _p.Boreholes.Count - 1;
        tabLeft.SelectedItem = tabGeo; await Pause(1200);
        StLog($"hố khoan: {_p.Boreholes.Count} hố, {bh.Layers.Count} lớp, sâu {bh.Depth:0.0} m");
        Shot($"{tag}_13_DiaChat");
        StLog($"lớp đất: thanh trượt ngang {_geoGrid.HorizontalScrollBarVisibility}, cố định {_geoGrid.FrozenColumnCount} cột");

        var adv = FoundationAdvisor.Recommend(_p, _res, bh);
        StLog($"gợi ý móng: {adv.Options.Count} phương án, chọn: {adv.Recommended?.Name}");
        var aw = new GeoAdvisorWindow(_p, _res, bh) { Owner = this, Width = 1300, Height = 850 };
        aw.Show(); await Pause(1500); ShotWindow(aw, $"{tag}_14_GoiYMong"); aw.Close();

        // ---- phụ lục tính toán ----
        ShowAppendix(); await Pause(2000);
        if (_appendixWin == null) throw new Exception("Không mở được phụ lục tính toán");
        ShotWindow(_appendixWin, $"{tag}_15_PhuLuc"); _appendixWin.Close();
        StLog("phụ lục: mở được");

        // trả lại trạng thái để bước sau không bị ảnh hưởng
        _p.Boreholes.Remove(bh); LoadGeoUi(); tabLeft.SelectedIndex = 0;
        // [Khung trái] khung hẹp: hàng thẻ một dòng + thanh trượt ngang, nội dung không bị cắt
        var w0 = colLeftPanel.Width;
        colLeftPanel.Width = new GridLength(320);
        await Pause(700);
        double sw = (tabLeft.SelectedContent as ScrollViewer)?.ScrollableWidth ?? -1;
        StLog($"khung trái hẹp 320 px: cuộn ngang được {sw:0} px (nội dung tối thiểu {LeftMinContentWidth} px)");
        if (sw <= 0) throw new Exception("Khung trái: không có thanh trượt ngang khi khung hẹp");
        cbGrade.SelectedIndex = 4;
        StLog($"cấp công trình: {_p.BuildingGrade} ({txtGradeHint.Text}); nhóm thẻ đang hiện: {LeftGroups[_leftGroup].Name}, {tabLeft.Items.OfType<TabItem>().Count(t => t.Visibility == Visibility.Visible)} thẻ");
        if (_p.BuildingGrade != "Cấp III") throw new Exception("Cấp công trình không lưu vào dự án");
        Shot($"{tag}_14_KhungTraiHep");
        // [Khai báo từng bước] bảng khai báo + bảng thông số của một nhóm ô nhập
        var hub = OpenDeclareHub();
        int nSteps = BuildDeclSteps().Count;
        if (hub != null) { await Pause(800); ShotWindow(hub, $"{tag}_15_BangKhaiBao"); }
        var gbInfo = FindChildren<GroupBox>(tabLeft).FirstOrDefault(g => g.Header as string == "Thông tin công trình");
        StLog($"khai báo từng bước: {nSteps} mục; nhóm Thông tin công trình {(gbInfo != null ? FieldRows(gbInfo).Count : 0)} thông số");
        if (nSteps < 12 || gbInfo == null || FieldRows(gbInfo).Count < 5) throw new Exception("Bảng khai báo thiếu mục");
        ShowFieldTable(gbInfo, "Thông tin công trình", false);
        await Pause(700);
        var ft = Application.Current.Windows.OfType<Window>().LastOrDefault(x => x.Title == "Thông tin công trình");
        if (ft != null) { ShotWindow(ft, $"{tag}_15b_BangThongTinCT"); ft.Close(); }
        hub?.Close();
        ShowLeftGroup(1, true); await Pause(500);
        Shot($"{tag}_14c_KhungTraiHep_KetQua");
        SelectLeft("Địa chất"); await Pause(300);
        if (_leftGroup != 0 || tabLeft.SelectedItem != tabGeo) throw new Exception("Thẻ theo lớp: chọn thẻ ở nhóm khác không tự chuyển nhóm");
        tabLeft.SelectedItem = tabGeo; await Pause(700);
        Shot($"{tag}_14b_KhungTraiHep_DiaChat");
        colLeftPanel.Width = w0; tabLeft.SelectedIndex = 0; await Pause(300);
    }
}

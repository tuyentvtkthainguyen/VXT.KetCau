using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Tự kiểm tra [Hệ mái tôn] + [Thẻ thông tin] + [Ẩn/hiện tường, cửa]: tạo mái chống nóng trên tầng trên cùng, thưng tường, mái tiền chế,
/// chụp mặt bằng / 3D / thẻ thông tin / hộp thoại / bảng khối lượng, rồi hoàn tác về mô hình ban đầu.</summary>
public partial class MainWindow
{
    private async Task SelfTestRoof(string tag)
    {
        if (_p == null || _p.Grids.Count == 0) { StLog("hệ mái: bỏ qua (không có trục)"); return; }
        int undo0 = _undo.Count;
        bool hadRes = _res != null, hadDesign = _design != null;
        var gx = _p.Grids.Where(g => g.Dir == GridDir.X).Select(g => g.Coord).ToList();
        var gy = _p.Grids.Where(g => g.Dir == GridDir.Y).Select(g => g.Coord).ToList();
        double x0 = gx.Min(), x1 = gx.Max(), y0 = gy.Min(), y1 = gy.Max();
        var top = _p.Stories.OrderBy(s => s.Elevation).Last();
        var first = _p.Stories.OrderBy(s => s.Elevation).First();
        tabView.SelectedItem = ViewTab("Mặt bằng");
        cbStory.SelectedItem = top.Name;
        await Pause(400);

        // 1) mái chống nóng phủ toàn bộ lưới trục tầng trên cùng
        var cn = RoofFramingBuilder.Defaults(RoofFramingKind.ChongNong, _p, top.Name);
        cn.X0 = x0; cn.X1 = x1; cn.Y0 = y0; cn.Y1 = y1; cn.RidgeAlongX = (x1 - x0) >= (y1 - y0);
        CreateRoofFraming(cn);
        var g1 = RoofGeom(cn);
        StLog($"hệ mái chống nóng {cn.Name}: {g1.PurlinCount} xà gồ, {g1.TrussCount} vì kèo, {g1.PostCount} trụ, tôn {g1.SheetArea:0.#} m², thép {g1.SteelKg:0} kg; {string.Join(" | ", g1.Checks)}");
        if (g1.PurlinCount < 4 || g1.TrussCount < 2 || g1.SheetArea <= (x1 - x0) * (y1 - y0)) throw new Exception("Hệ mái chống nóng dựng sai");
        // xà gồ đều bước ≤ bước khai báo (theo mặt dốc)
        var cs = g1.Members.Where(m => m.Role == "Xà gồ mái").Select(m => cn.RidgeAlongX ? m.A.Y : m.A.X).OrderBy(v => v).ToList();
        double cos = Math.Cos(g1.SlopeDeg * Math.PI / 180), maxGap = 0;
        for (int i = 1; i < cs.Count; i++) { double gp = cs[i] - cs[i - 1]; if (gp > 0.05) maxGap = Math.Max(maxGap, gp / cos); }
        StLog($"hệ mái: bước xà gồ lớn nhất theo mặt dốc {maxGap:0.###} m (khai báo {cn.PurlinSpacing} m)");
        if (maxGap > cn.PurlinSpacing + 0.35) throw new Exception("Hệ mái: bước xà gồ vượt bước khai báo");
        await Pause(800);
        Shot($"{tag}_60_MaiChongNong_MatBang");

        // 2) thưng tường dọc trục đầu tiên (tầng 1), 3) mái tiền chế một dốc
        var tt = RoofFramingBuilder.Defaults(RoofFramingKind.ThungTuong, _p, first.Name);
        tt.X0 = x0; tt.X1 = x1; tt.Y0 = y0 - 0.2; tt.Y1 = y0 + 0.2; tt.RidgeAlongX = true; tt.IncludeTrusses = true; tt.TrussSpacing = 3;
        CreateRoofFraming(tt);
        var g2 = RoofGeom(tt);
        StLog($"thưng tường {tt.Name}: {g2.PurlinCount} xà gồ tường, {g2.TrussCount} cột sườn, tôn {g2.SheetArea:0.#} m²; {string.Join(" | ", g2.Checks)}");
        if (g2.PurlinCount < 2 || g2.Panels.Count != 1) throw new Exception("Thưng tường dựng sai");
        var pe = RoofFramingBuilder.Defaults(RoofFramingKind.MaiTonTienChe, _p, top.Name);
        pe.X0 = x0; pe.X1 = (x0 + x1) / 2; pe.Y0 = y0; pe.Y1 = y1; pe.TwoSlopes = false; pe.BaseOffset = 4; pe.RidgeAlongX = false;
        CreateRoofFraming(pe);
        var g3 = RoofGeom(pe);
        StLog($"mái tiền chế {pe.Name}: {g3.PurlinCount} xà gồ {pe.PurlinSection}, tôn {g3.SheetArea:0.#} m²; {string.Join(" | ", g3.Checks)}");
        _p.RoofFramings.Remove(pe); _rfGeo.Remove(pe.Id);

        // thẻ thông tin trên mặt bằng: chọn mái chống nóng
        SelectRoof(cn);
        await Pause(600);
        bool cardVisible = _infoCards.Any(c => c.Card.IsVisible);
        StLog($"thẻ thông tin: {(cardVisible ? "hiện" : "ẨN")} – {txtInfo.Text.Split('\n')[0]}");
        if (!cardVisible) throw new Exception("Thẻ thông tin không hiện khi chọn hệ mái");
        Shot($"{tag}_61_TheThongTin_MatBang");

        // 3D: mái (bỏ chọn để thấy màu thật, tắt thẻ), rồi chọn lại + thẻ thông tin
        tabView.SelectedItem = ViewTab("3D");
        _selRoof = null; OnSelectionChanged();
        await Pause(1500);
        Zoom3DExtents(); await Pause(800);
        Shot($"{tag}_62_MaiChongNong_3D");
        SelectObject(cn); await Pause(800);
        StLog($"3D chọn hệ mái: {(_selRoof == cn ? "đúng" : "SAI")}, thẻ {(_infoCards.Any(c => c.Card.IsVisible) ? "hiện" : "ẩn")}");
        Shot($"{tag}_62b_3D_TheMai");
        // ẩn tường + cửa
        SetCategoryVisible("Tường", false); SetCategoryVisible("Cửa", false);
        await Pause(1000);
        Shot($"{tag}_63_3D_AnTuongCua");
        SetCategoryVisible("Tường", true); SetCategoryVisible("Cửa", true);
        StLog($"ẩn/hiện: tường {(_hideWalls ? "ẩn" : "hiện")}, cửa {(_hideDoors ? "ẩn" : "hiện")}");
        // chọn tường trong 3D → thẻ thông tin tường
        var w0 = _p.Walls.FirstOrDefault();
        if (w0 != null) { SelectObject(w0); await Pause(600); StLog($"3D chọn tường: {txtInfo.Text.Split('\n')[0]}"); Shot($"{tag}_64_3D_TheTuong"); }
        tabView.SelectedItem = ViewTab("Mặt bằng");

        // hộp thoại + profile + bảng khối lượng
        var dlg = new RoofFramingDialog(_p, cn.Clone(), false) { Owner = this };
        dlg.Show(); await Pause(700); ShotWindow(dlg, $"{tag}_65_HopThoaiMai"); dlg.Close();
        var pd = new SheetProfileDialog(_p) { Owner = this };
        pd.Show(); await Pause(900); ShotWindow(pd, $"{tag}_66_ProfileTon"); pd.Close();
        var tw = ShowRoofTakeoff();
        if (tw != null) { await Pause(700); ShotWindow(tw, $"{tag}_67_KhoiLuongMai"); tw.Close(); }

        // hoàn tác về ban đầu
        _selRoof = null;
        while (_undo.Count > undo0) Undo();
        StLog($"hệ mái: hoàn tác – còn {_p.RoofFramings.Count} hệ mái");
        if (_p.RoofFramings.Count != 0) throw new Exception("Hệ mái: hoàn tác chưa xoá hết");
        ClearSelection();
        if (hadRes && _res == null) { if (!await RunAnalysis()) throw new Exception("Hệ mái: phân tích lại lỗi"); }
        if (hadDesign && _design == null) await EnsureDesign();
    }
}

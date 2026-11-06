using System.Text;
using System.Windows;
using System.Windows.Controls;
using KetCauVXT.Mep;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Lệnh điện nước – ĐHKK đợt 4: dò tuyến ống gió tránh cột (A*), bảng tải lạnh chi tiết, bảng tủ điện + gán mạch,
/// bể nước / bơm, xem sơ đồ đứng. Lõi: Mep/MepRound4.cs.</summary>
public sealed partial class MepWindow
{
    private void AddRound4HvacButtons(WrapPanel r)
    {
        r.Children.Add(Btn("Dò tuyến tránh cột", () => Edit(() =>
        {
            var ids = _hvSel.Where(s => s.Kind == HvacElemKind.Duct).Select(s => s.Id).ToList();
            var ducts = ids.Count > 0 ? ids.Select(i => _p.Mep.Hvac.Duct(i)).Where(d => d != null).ToList()
                : (_p.Mep.Hvac?.Ducts ?? new List<HvacDuct>()).Where(d => d.Story == Story).ToList();
            int ok = 0, bends = 0, beams = 0;
            foreach (var d in ducts)
            {
                var rr = HvacGridRouter.Reroute(_p, d);
                if (rr.Found) ok++;
                bends += rr.Bends; beams += rr.BeamCrossings;
            }
            SetStatus($"Dò tuyến lưới A*: {ok}/{ducts.Count} ống gió đi lại tránh cột; tổng {bends} lần đổi hướng, cắt {beams} dầm (đã hạ ống nếu bật).");
        }), "Ống gió đã chọn (hoặc mọi ống tầng này): tìm tuyến trực giao ít đổi hướng, không qua cột, hạn chế cắt dầm / xuyên tường"));
        r.Children.Add(Btn("Tải lạnh chi tiết", CoolingDetailReport, "CLTD rút gọn: tường / kính theo hướng, mái, người, đèn, thiết bị, gió lọt – so với ước lượng nhanh"));
    }

    private void AddRound4ElecButtons(WrapPanel r)
    {
        r.Children.Add(Btn("Bảng tủ điện", PanelReport, "Chia mạch (chiếu sáng ≤ 10 điểm, ổ cắm ≤ 8, mỗi máy lạnh / bình nóng lạnh riêng), aptomat, dây, cân pha, aptomat / cáp tổng"));
        r.Children.Add(Btn("Gán mạch theo tủ", () => Edit(() =>
        {
            var pr = ElectricalPanels.Build(_p);
            int n = ElectricalPanels.Apply(_p, pr);
            SetStatus($"Đã gán {n} thiết bị vào {pr.Circuits.Count} mạch (C1…).");
        }), "Ghi tên mạch C1, C2… vào thiết bị điện theo cách chia mạch của bảng tủ điện"));
    }

    private void AddRound4WaterButtons(WrapPanel r)
    {
        r.Children.Add(Btn("Bể nước – bơm", WaterReport, "Dung tích bể mái / bể ngầm = N·q·ngày; bơm Q, H (khai báo số người trong Cài đặt điện nước)"));
    }

    private void CoolingDetailReport()
    {
        string st = Story;
        var rooms = (_p.Arch?.Rooms ?? new List<Room>()).Where(r => r.Story == st && r.Outline.Count >= 3).ToList();
        if (rooms.Count == 0) { MessageBox.Show(this, "Tầng này chưa có phòng.", "Tải lạnh chi tiết", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        int people = (int)Math.Max(0, Dbl(_hvPeople.Text, 2));
        var sb = new StringBuilder();
        sb.AppendLine(HvacCoolingDetailed.MethodText).AppendLine();
        foreach (var r in rooms)
        {
            var c = HvacCoolingDetailed.Room(_p, r, new CoolingInput { People = people });
            sb.AppendLine($"• {c.Room}: A = {c.Area:0.0} m² – tường {c.QWall:0} W, kính {c.QGlass:0} W, mái {c.QRoof:0} W, người {c.QPeopleS + c.QPeopleL:0} W, "
                + $"đèn {c.QLight:0} W, thiết bị {c.QEquip:0} W, gió lọt {c.QInfS + c.QInfL:0} W → {c.TotalW:0} W = {c.Btu:0} BTU/h → {c.UnitCount} × {c.UnitBtu:0} BTU "
                + $"(ước lượng nhanh {c.QuickBtu:0} BTU/h)");
            foreach (var w in c.Walls) sb.AppendLine($"    tường {w.WallId} hướng {w.Orient}: tường đặc {w.WallArea:0.0} m², kính {w.GlassArea:0.0} m²");
        }
        MessageBox.Show(this, sb.ToString(), $"Tải lạnh chi tiết – tầng {st}", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PanelReport()
    {
        var r = ElectricalPanels.Build(_p);
        if (r.Circuits.Count == 0) { MessageBox.Show(this, "Chưa có thiết bị điện có công suất (đèn, ổ cắm, bình nóng lạnh) hoặc dàn lạnh.", "Bảng tủ điện", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var sb = new StringBuilder();
        foreach (var c in r.Circuits)
            sb.AppendLine($"{c.Name} [{c.Phase}] {c.Story} · {c.Group}: {c.Points} điểm, {c.PowerW:0} W, I = {c.CurrentA:0.0} A → {c.BreakerText}, {c.WireText}");
        sb.AppendLine();
        sb.AppendLine($"P đặt = {r.InstalledW / 1000:0.00} kW; P tính toán Σ Kc·P = {r.DemandW / 1000:0.00} kW; Itt = {r.DesignA:0.0} A");
        sb.AppendLine($"Tủ {(r.ThreePhase ? "3 pha" : "1 pha")}: aptomat tổng {r.MainBreakerText}; cáp tổng {r.MainCableText}");
        if (r.ThreePhase) sb.AppendLine($"Tải pha L1/L2/L3 = {r.PhaseW[0]:0} / {r.PhaseW[1]:0} / {r.PhaseW[2]:0} W; lệch pha {r.UnbalancePct:0.#} %");
        sb.AppendLine().AppendLine("Sơ đồ nguyên lý tủ điện có trong bộ bản vẽ điện nước; bảng tủ điện có trong sheet Excel \"Vật tư điện nước\".");
        MessageBox.Show(this, sb.ToString(), "Bảng tủ điện", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void WaterReport()
    {
        var o = _p.Mep.Settings.Water ?? new WaterTankOptions();
        if (o.Occupants <= 0) { MessageBox.Show(this, "Khai báo số người dùng nước trong \"Cài đặt điện nước\" trước.", "Bể nước – bơm", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var t = WaterSupplyCalc.Table(_p, o);
        var sb = new StringBuilder();
        foreach (var row in t.Rows) sb.AppendLine($"{row[0]}: {row[2]} {row[3]}   ({row[1]})");
        MessageBox.Show(this, sb.ToString(), "Bể nước – bơm", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

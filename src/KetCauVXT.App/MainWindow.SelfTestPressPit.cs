using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>[Tự kiểm tra v62] Truyền tải → cọc ép theo lực ép 50 t → số cọc từng hố móng, cột sát 1 / 2 ranh → áp dụng móng cọc;
/// kiểu bố trí thép dầm A/B/C; bản vẽ riêng từng dầm. Chụp ảnh từng bước (dùng cho hướng dẫn), ghi số liệu, trả lại mô hình.</summary>
public partial class MainWindow
{
    private async Task SelfTestPressPit(string tag)
    {
        if (_p == null || _res == null) { StLog("cọc ép / hố móng: bỏ qua (chưa phân tích)"); return; }
        string snap = Snapshot();
        try
        {
            // ---- 1. Truyền tải & gợi ý móng ----
            var w = new TakedownWindow(_p, _res) { Owner = this, Width = 1350, Height = 860 };
            w.Show();
            try
            {
                await Pause(1500);
                var adv = w.Advice;
                if (adv == null) { StLog("truyền tải: không có kết quả"); return; }
                var t = adv.T;
                StLog($"truyền tải: {t.Columns.Count} chân cột, sàn {t.FloorArea:0} m², G = {t.TotalG:0} kN, P = {t.TotalP:0} kN, (G+P)/m² = {(t.TotalG + t.TotalP) / Math.Max(t.FloorArea, 1):0.0} kN/m²");
                foreach (var c in t.Columns) StLog($"   {c.Name}: A = {c.Area:0.0} m², G = {c.G:0}, P = {c.P:0}, ψ = {c.Psi:0.00}, Ntc = {c.Ntc:0}, Ntt = {c.Ntt:0}, khung Ntt = {c.FeNtt:0}");
                StLog("gợi ý móng: " + adv.Explanation);
                foreach (var n in adv.Notes) StLog("   · " + n);
                if (adv.Press != null) foreach (var n in adv.Press.Notes) StLog("   cọc ép: " + n);
                foreach (var c in adv.Columns) StLog($"   {c.T.Name}: {c.EdgeText}; móng đơn {c.B:0.0}×{c.B:0.0}×{c.H:0.00} m, R1/N = {c.StrapFactor:0.00}; {c.Piles} cọc");
                foreach (var x in adv.Pits) StLog($"   {x.Name} [{x.Names}] {x.EdgeText}: ΣNtt = {x.Ntt:0}, e = {x.Ex:0.00}/{x.Ey:0.00} m, R1/N = {x.Factor:0.00}, N = {x.Ndesign:0}, {x.Piles} cọc ({x.Nx}×{x.Ny}), đài {x.CapLx:0.00}×{x.CapLy:0.00}");
                StLog($"hố móng: {adv.Pits.Count} hố, {adv.PitPiles} cọc, [P] = {adv.BestPile?.Capacity:0} kN ({adv.BestPile?.Name})");
                w.SelectTab("Tải chân cột"); await Pause(1200); ShotWindow(w, $"{tag}_57_TruyenTai");
                w.SelectTab("Móng đơn"); await Pause(1000); ShotWindow(w, $"{tag}_58_GoiYMongDon");
                w.SelectTab("Móng cọc"); await Pause(1000); ShotWindow(w, $"{tag}_59_CocEp");
                w.SelectTab("Hố móng"); await Pause(1200); ShotWindow(w, $"{tag}_60_HoMong");

                // ---- 2. Áp dụng móng cọc theo tải đứng ----
                string msg = TakedownAdvisor.Apply(_p, adv, "Pile", true);
                StLog("áp dụng: " + msg);
            }
            finally { w.Close(); }
            MarkDirty(); RefreshAll(false);
            if (await DesignFoundation())
            {
                int piles = _design.PileCaps.Sum(c => c.Piles.Count * Math.Max(1, c.Locations.Count));
                StLog($"móng cọc sau áp dụng: {_design.PileCaps.Count} loại đài, {piles} cọc, {_design.PileCaps.Count(c => !c.Ok)} đài chưa đạt, ranh {_p.Foundation.Boundaries.Count}");
                foreach (var pc in _design.PileCaps.Take(8))
                    StLog($"   {pc.Name}: {pc.Piles.Count} cọc × {Math.Max(1, pc.Locations.Count)} vị trí, đài {pc.L:0.00}×{pc.B:0.00}×{pc.H:0.00} m, Pmax = {pc.PMax:0} / [P] = {pc.PileCapacity:0} kN – {(pc.Ok ? "đạt" : "CHƯA ĐẠT")}");
                tabView.SelectedItem = ViewTab("Mô hình móng");
                var pc0 = _design.PileCaps.FirstOrDefault();
                _fndSelItem = pc0;
                Select(footingNode: pc0?.NodeIds.FirstOrDefault() ?? 0);
                await Pause(1500); Shot($"{tag}_61_MongCocSauApDung");
            }
            else StLog("móng cọc sau áp dụng: thiết kế lỗi");

            // ---- 3. Kiểu bố trí thép dầm A / B / C ----
            if (_design == null || _design.Beams.Count == 0) await EnsureDesign();
            var beam = _design?.Beams.Where(b => !b.IsCurved).OrderByDescending(b => b.Spans.Count).FirstOrDefault();
            if (beam != null)
            {
                var dlg = CreateRebarDialog(beam);
                dlg.Width = 1250; dlg.Height = 650;
                dlg.Show();
                try
                {
                    await Pause(800);
                    bool ok = dlg.SelectZone(RebarZones.BeamScheme);
                    await Pause(1500);
                    var z = RebarOptions.Zones(_p, beam).FirstOrDefault(q => q.Zone == RebarZones.BeamScheme);
                    if (z != null)
                        foreach (var a in RebarOptions.Alternatives(_p, _res, beam, z)) StLog($"   kiểu bố trí dầm {beam.Name}: {a.Text} – {a.WeightKg:0.0} kg{(a.IsCurrent ? " (đang dùng)" : "")}");
                    StLog($"kiểu bố trí thép: dầm {beam.Name} ({beam.Spans.Count} nhịp), vùng kiểu bố trí {(ok ? "có" : "KHÔNG CÓ")}, {dlg.AlternativeCount} phương án");
                    ShotWindow(dlg, $"{tag}_62_KieuBoTriThepDam");
                    dlg.SelectZone(0);
                    await Pause(1200);
                    ShotWindow(dlg, $"{tag}_63_ChonSoThanhThep");
                }
                finally { dlg.Close(); }

                // ---- 4. Bản vẽ từng dầm ----
                var bw = new BeamSheetWindow(_p, () => _design, null) { Owner = this, Width = 1400, Height = 860 };
                bw.Show();
                try
                {
                    bw.Select(beam.Name);
                    await Pause(2000);
                    ShotWindow(bw, $"{tag}_64_BanVeTungDam");
                    string f = KetCauVXT.Drawing.BeamSheet.Export(_p, _design, beam, System.IO.Path.Combine(SelfTestOut, $"{tag}_ChiTietDam_{beam.Name}.dxf"));
                    StLog($"bản vẽ từng dầm: {_design.Beams.Count} dầm, xuất {System.IO.Path.GetFileName(f)}");
                }
                finally { bw.Close(); }
            }
            else StLog("kiểu bố trí thép: không có dầm");
        }
        catch (Exception ex) { StLog("cọc ép / hố móng: LỖI " + ex.Message); }
        finally
        {
            try
            {
                RestoreSnapshot(snap);
                bool ok = await RunAnalysis() && await EnsureDesign();
                StLog("cọc ép / hố móng: đã trả lại mô hình ban đầu" + (ok ? "" : " (phân tích lại lỗi)"));
            }
            catch (Exception ex) { StLog("cọc ép / hố móng: không trả lại được mô hình – " + ex.Message); }
        }
    }
}

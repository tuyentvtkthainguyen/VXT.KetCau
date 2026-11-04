using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>[Sàn hạ cốt] Bước tự kiểm tra (--selftest): đổi một ô sàn của tầng thành sàn vệ sinh hạ cốt −50 mm, phân tích + thiết kế lại,
/// ghi kết quả kiểm tra dầm biên ô hạ cốt, chụp mặt bằng (ô gạch chéo, ▽ -0.050), rồi trả lại mô hình ban đầu.</summary>
public partial class MainWindow
{
    private async Task SelfTestSlabDrop(string tag, Story story)
    {
        if (_p == null || story == null) { StLog("sàn hạ cốt: bỏ qua (chưa có mô hình / tầng)"); return; }
        string snap = Snapshot();
        try
        {
            // ô có ô kề (để có cạnh bậc), không thì ô đầu tiên của tầng
            var slabs = _p.Slabs.Where(s => s.Story == story.Name && s.Nodes.Count >= 3).ToList();
            if (slabs.Count == 0) { StLog("sàn hạ cốt: tầng " + story.Name + " không có ô sàn"); return; }
            var slab = slabs.FirstOrDefault(s => slabs.Any(o => o != s && o.Nodes.Intersect(s.Nodes).Count() >= 2)) ?? slabs[0];
            var r = ModelEdit.SetSlabUse(_p, slab, SlabUse.WC);
            if (Math.Abs(slab.LevelOffset + 0.05) > 1e-9) r = SlabDrop.SetDrop(_p, slab, -0.05, "Sàn vệ sinh (tự kiểm tra)");
            StLog("sàn hạ cốt: " + r.Message);
            _p.InvalidateIndex();
            MarkDirty(); RefreshAll(false);
            if (!await RunAnalysis()) { StLog("sàn hạ cốt: phân tích lại lỗi"); return; }
            if (!await EnsureDesign()) { StLog("sàn hạ cốt: thiết kế lại lỗi"); return; }
            var ds = _design;
            var sd = ds?.Slabs.FirstOrDefault(x => x.SlabId == slab.Id || x.RebarKeys.Contains("S:" + slab.Id));
            StLog($"sàn hạ cốt: ô {slab.Label} Δ = {SlabDrop.MmText(slab.LevelOffset)} mm, liên kết {sd?.EdgeCondition ?? "?"}, "
                  + $"{sd?.EdgeBars.Count(e => (e.Bar?.Role ?? "").Contains("hạ cốt")) ?? 0} thanh mũ cấu tạo cạnh bậc");
            var cks = ds != null ? SlabDropChecks.BeamChecks(_p, ds) : new List<SlabDropBeamCheck>();
            StLog($"sàn hạ cốt: {cks.Count} dầm đỡ ô hạ cốt, {cks.Count(c => !c.Ok)} không đủ chiều cao");
            foreach (var c in cks.Take(4)) StLog("   " + c.Beam + ": " + SlabDropChecks.Text(c));
            cbStory.SelectedItem = story.Name;
            tabView.SelectedItem = ViewTab("Mặt bằng");
            Select(slab: slab);
            await Pause(1500);
            Shot($"{tag}_55_SanHaCot");
        }
        catch (Exception ex) { StLog("sàn hạ cốt: LỖI " + ex.Message); }
        finally
        {
            try
            {
                RestoreSnapshot(snap);
                StLog("sàn hạ cốt: đã trả lại mô hình ban đầu (" + SlabDrop.Lowered(_p).Count + " ô hạ cốt)");
            }
            catch (Exception ex) { StLog("sàn hạ cốt: không trả lại được mô hình – " + ex.Message); }
        }
    }
}

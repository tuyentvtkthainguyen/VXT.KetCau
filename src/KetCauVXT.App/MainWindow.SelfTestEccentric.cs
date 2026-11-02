using System.Text.Json;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>[MóngLệchTâm] Bước tự kiểm tra (--selftest): khai báo ranh giới đất trái + phải theo trục đầu / cuối, thiết kế lại móng đơn,
/// chụp mặt bằng móng (móng lệch tâm, dầm cân bằng, ranh giới), mở hộp thoại "Lệch tâm…" cho một móng biên (không chặn) và chụp;
/// trả lại thông số móng ban đầu.</summary>
public partial class MainWindow
{
    private async Task SelfTestEccentricFooting(string tag)
    {
        string fsSnap = JsonSerializer.Serialize(_p.Foundation, Project.JsonOptions);
        EccentricFootingDialog dlg = null;
        try
        {
            tabView.SelectedItem = ViewTab("Mô hình móng");
            var gx = _p.GridsX.ToList();
            if (gx.Count < 2) { StLog("móng lệch tâm: cần ≥ 2 trục X"); return; }
            var fs = JsonSerializer.Deserialize<FoundationSettings>(fsSnap, Project.JsonOptions);
            fs.Type = FoundationType.Isolated;
            fs.Boundaries = new List<PropertyBoundary>
            {
                new() { Side = BoundarySide.Left, Grid = gx[0].Name, Offset = 0.11 },
                new() { Side = BoundarySide.Right, Grid = gx[^1].Name, Offset = 0.11 }
            };
            fs.Eccentric = new List<EccentricOverride>();
            await ApplyBoundaries(fs);
            await Pause(1200);
            var ds = _design;
            int ne = ds?.Footings.Count(f => f.Eccentric != null && f.Eccentric.E0 > 1e-6) ?? 0;
            StLog($"móng lệch tâm: {EccentricFooting.Lines(_p).Count} cạnh ranh giới, {ds?.Footings.Count ?? 0} móng đơn ({ne} lệch tâm), "
                  + $"{ds?.StrapBeams?.Count ?? 0} dầm cân bằng, {ds?.Footings.Count(f => f.Eccentric?.Combined != null) ?? 0} móng kết hợp, "
                  + $"{ds?.Footings.Count(f => !f.Ok) ?? 0} móng không đạt");
            foreach (var f in ds?.Footings.Where(x => x.Eccentric != null).Take(4) ?? Enumerable.Empty<KetCauVXT.Design.FootingDesign>())
                StLog($"   {f.Name}: {EccentricOverride.SchemeName(f.Eccentric.Scheme)}, {f.L:0.00}×{f.B:0.00}×{f.H:0.00} m, ex = {f.Eccentric.OffsetX * 1000:0}, ey = {f.Eccentric.OffsetY * 1000:0} mm, "
                      + $"pmax = {Math.Min(f.Pmax, 99999):0.0} / 1,2R = {1.2 * f.R:0.0} kN/m² – {(f.Ok ? "đạt" : "KHÔNG ĐẠT")}");
            foreach (var sb in ds?.StrapBeams?.Take(3) ?? Enumerable.Empty<StrapBeamDesign>())
                StLog($"   {sb.Name} ×{sb.Quantity}: {sb.B * 1000:0}×{sb.H * 1000:0}, L = {sb.Span:0.00} m, M = {sb.M:0.0} kN.m, Q = {sb.V:0.0} kN, {sb.TopBars} / {sb.BottomBars}, {sb.Stirrups} – {(sb.Ok ? "đạt" : "KHÔNG ĐẠT")}");
            _fFitPending = true;
            RedrawFoundationPlan();
            await Pause(1500);
            Shot($"{tag}_36_MongLechTam");

            var edge = ds?.Footings.FirstOrDefault(f => f.Eccentric != null && f.Eccentric.E0 > 1e-6);
            if (edge == null) { StLog("móng lệch tâm: không có móng biên lệch tâm"); return; }
            _fndSelItem = edge;
            Select(footingNode: edge.NodeId);
            await Pause(600);
            dlg = OpenEccentricDialog(edge.NodeId);
            await Pause(1500);
            dlg.Evaluate();
            await Pause(800);
            ShotWindow(dlg, $"{tag}_37_MongLechTamChiTiet");
            StLog("móng lệch tâm (hộp thoại): " + dlg.Summary);
            try { dlg.Close(); } catch (InvalidOperationException) { }
            dlg = null;
        }
        catch (Exception ex) { StLog("móng lệch tâm: LỖI " + ex.Message); }
        finally
        {
            try { dlg?.Close(); } catch (InvalidOperationException) { }
            var fs = JsonSerializer.Deserialize<FoundationSettings>(fsSnap, Project.JsonOptions);
            if (fs != null) _p.Foundation = fs;
            MarkDirty(false);
            SyncFoundationUi();
            RedrawFoundationPlan();
            StLog("móng lệch tâm: đã trả lại thông số móng ban đầu (" + FoundationTypeName(_p.Foundation.Type) + ", " + EccentricFooting.Lines(_p).Count + " ranh giới)");
        }
    }
}

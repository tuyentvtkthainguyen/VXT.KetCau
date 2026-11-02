using System.Text.Json;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>[LùiCọcRanh] + [CừTràm] Bước tự kiểm tra (--selftest):
/// (1) khai báo ranh giới trái + phải, móng cọc có lùi tim cọc 500 mm khỏi ranh để ép cọc, thiết kế lại, chụp mặt bằng móng;
/// (2) chuyển móng đơn trên nền gia cố cừ tràm (thêm tạm hố khoan nền yếu, mực nước ngầm cao), thiết kế lại, mở hộp thoại cừ tràm (không chặn) và chụp;
/// trả lại thông số móng, hố khoan, nền đất ban đầu.</summary>
public partial class MainWindow
{
    private async Task SelfTestCuTram(string tag)
    {
        string fsSnap = JsonSerializer.Serialize(_p.Foundation, Project.JsonOptions);
        string bhSnap = JsonSerializer.Serialize(_p.Boreholes ?? new List<Borehole>(), Project.JsonOptions);
        string soilSnap = JsonSerializer.Serialize(_p.Soil ?? new SoilSettings(), Project.JsonOptions);
        CuTramDialog dlg = null;
        try
        {
            tabView.SelectedItem = ViewTab("Mô hình móng");
            var gx = _p.GridsX.ToList(); var gy = _p.GridsY.ToList();
            if (gx.Count < 2 || gy.Count < 1) { StLog("lùi cọc / cừ tràm: cần ≥ 2 trục X"); return; }
            // ---- (1) lùi cọc khỏi ranh giới ----
            var fs = JsonSerializer.Deserialize<FoundationSettings>(fsSnap, Project.JsonOptions);
            fs.Type = FoundationType.Pile;
            fs.Boundaries = new List<PropertyBoundary>
            {
                new() { Side = BoundarySide.Left, Grid = gx[0].Name, Offset = 0.11 },
                new() { Side = BoundarySide.Right, Grid = gx[^1].Name, Offset = 0.11 }
            };
            fs.Eccentric = new List<EccentricOverride>();
            fs.EccentricDefault = EccentricScheme.Auto;
            fs.PileSetbackEnabled = true;
            fs.PileSetback = 0.5;
            fs.CapToBoundary = true;
            fs.CuTram ??= new CuTramSettings();
            fs.CuTram.Enabled = false;
            await ApplyBoundaries(fs);
            await Pause(1200);
            var ds = _design;
            var lui = ds?.PileCaps.Where(c => c.Setback != null && c.Setback.Shifted).ToList() ?? new List<PileCapDesign>();
            StLog($"lùi cọc: {PileSetback.Lines(_p).Count} cạnh có khoảng lùi, {ds?.PileCaps.Count ?? 0} đài ({lui.Count} đài lùi cọc), {ds?.StrapBeams?.Count ?? 0} dầm cân bằng, "
                  + $"{ds?.PileCaps.Count(c => !c.Ok) ?? 0} đài không đạt");
            foreach (var c in lui.Take(4))
            {
                double dmin = c.Setback.Sides.Count > 0 ? c.Setback.Sides.Min(q => q.Distance) : double.NaN;
                StLog($"   {c.Name}: {c.Piles.Count} cọc, đài {c.L:0.00}×{c.B:0.00}×{c.H:0.00} m, khoảng lùi nhỏ nhất {dmin * 1000:0} mm, e = {c.Setback.GroupEx * 1000:0}/{c.Setback.GroupEy * 1000:0} mm, "
                      + $"Pmax = {c.PMax:0.0} (không DM {c.Setback.PmaxNoStrap:0.0}) / [P] = {c.PileCapacity:0} kN, công xôn M = {c.Setback.MCant:0.0} kN.m – đề xuất {c.Setback.Recommended} – {(c.Ok ? "đạt" : "KHÔNG ĐẠT")}");
            }
            _fFitPending = true;
            RedrawFoundationPlan();
            await Pause(1500);
            Shot($"{tag}_38_LuiCocRanh");

            // ---- (2) móng đơn trên nền gia cố cừ tràm ----
            var bh = CuTramCalc.SoftDefault();
            bh.Name = "HK-CT (tự kiểm tra)";
            bh.X = (gx[0].Coord + gx[^1].Coord) / 2; bh.Y = (gy[0].Coord + gy[^1].Coord) / 2;
            _p.Boreholes ??= new List<Borehole>();
            _p.Boreholes.Add(bh);
            var fs2 = JsonSerializer.Deserialize<FoundationSettings>(JsonSerializer.Serialize(_p.Foundation, Project.JsonOptions), Project.JsonOptions);
            fs2.Type = FoundationType.Isolated;
            fs2.PileSetbackEnabled = false;
            fs2.CuTram ??= new CuTramSettings();
            fs2.CuTram.Enabled = true;
            _p.Foundation = fs2;
            MarkDirty(false);
            SyncFoundationUi();
            bool ok = await DesignFoundation();
            await Pause(1000);
            ds = _design;
            var cu = ds?.Footings.Where(f => f.CuTram != null).ToList() ?? new List<KetCauVXT.Design.FootingDesign>();
            StLog($"cừ tràm: thiết kế {(ok ? "xong" : "LỖI")}, {ds?.Footings.Count ?? 0} móng đơn ({cu.Count} gia cố cừ tràm), {cu.Count(f => !f.Ok)} không đạt");
            foreach (var f in cu.Take(4))
                StLog($"   {f.Name} ×{f.Quantity}: {f.L:0.00}×{f.B:0.00}×{f.H:0.00} m, ptb = {f.Ptb:0.0} kN/m², [p]1 = {f.CuTram.P1:0.0}, [p]2 = {f.CuTram.P2:0.0} → [p] = {f.CuTram.Allowable:0.0}; "
                      + $"{f.CuTram.Count} cây, lún {(f.CuTram.Settlement?.S ?? double.NaN) * 100:0.00} cm, đầu cừ {f.CuTram.TopDepth:0.00} m / MNN {f.CuTram.WaterDepth:0.00} m – {(f.Ok ? "đạt" : "KHÔNG ĐẠT")}");
            _fFitPending = true;
            RedrawFoundationPlan();
            await Pause(800);
            var first = cu.FirstOrDefault();
            if (first != null)
            {
                _fndSelItem = first;
                Select(footingNode: first.NodeId);
                await Pause(500);
            }
            dlg = OpenCuTramDialog(first?.NodeId ?? 0);
            await Pause(1500);
            dlg.Evaluate();
            await Pause(800);
            ShotWindow(dlg, $"{tag}_39_CuTram");
            StLog("cừ tràm (hộp thoại): " + dlg.Summary);
            try { dlg.Close(); } catch (InvalidOperationException) { }
            dlg = null;
        }
        catch (Exception ex) { StLog("lùi cọc / cừ tràm: LỖI " + ex.Message); }
        finally
        {
            try { dlg?.Close(); } catch (InvalidOperationException) { }
            var fs = JsonSerializer.Deserialize<FoundationSettings>(fsSnap, Project.JsonOptions);
            if (fs != null) _p.Foundation = fs;
            _p.Boreholes = JsonSerializer.Deserialize<List<Borehole>>(bhSnap, Project.JsonOptions) ?? new List<Borehole>();
            var so = JsonSerializer.Deserialize<SoilSettings>(soilSnap, Project.JsonOptions);
            if (so != null) _p.Soil = so;
            MarkDirty(false);
            SyncFoundationUi();
            RedrawFoundationPlan();
            StLog("lùi cọc / cừ tràm: đã trả lại thông số móng, hố khoan, nền đất ban đầu (" + FoundationTypeName(_p.Foundation.Type) + ", "
                  + EccentricFooting.Lines(_p).Count + " ranh giới, " + (_p.Boreholes?.Count ?? 0) + " hố khoan)");
        }
    }
}

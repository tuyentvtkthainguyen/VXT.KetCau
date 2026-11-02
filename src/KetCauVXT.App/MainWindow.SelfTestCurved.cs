using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest): thêm dầm cong trên tầng, phân tích lại, chụp mặt bằng, 3D và biểu đồ nội lực.</summary>
public partial class MainWindow
{
    private async Task SelfTestCurvedBeam(string tag, Story story)
    {
        RefreshEditCombos();
        string sec = cbEditBeamSec.SelectedItem as string
                     ?? _p.FrameSections.FirstOrDefault(s => s.Shape == SectionShape.RectConcrete && s.Name.StartsWith("D"))?.Name;
        var gxs = _p.GridsX.ToList(); var gys = _p.GridsY.ToList();
        if (sec == null || gxs.Count < 2 || gys.Count < 1) { StLog("dầm cong: bỏ qua (thiếu tiết diện dầm hoặc trục)"); return; }

        // dầm cong biên ngoài nhà: nối hai giao trục đầu tiên trên trục Y nhỏ nhất, vồng ra phía ngoài (−Y)
        double y0 = gys[0].Coord, xa = gxs[0].Coord, xb = gxs[1].Coord;
        double f = Math.Clamp((xb - xa) / 5, 0.4, 1.5);
        string snap = Snapshot();
        var r = ModelEdit.AddCurvedBeam(_p, story.Name, (xa, y0), (xb, y0), ((xa + xb) / 2, y0 - f), sec);
        if (!r.Ok)
        {
            // đã có dầm thẳng giữa hai giao trục → uốn cong dầm đó
            var edge = ModelEdit.StoryBeams(_p, story.Name).FirstOrDefault(b =>
            {
                var a = _p.Node(b.NodeI); var c = _p.Node(b.NodeJ);
                return Math.Abs(a.Y - y0) < 1e-6 && Math.Abs(c.Y - y0) < 1e-6
                       && Math.Abs(Math.Min(a.X, c.X) - xa) < 1e-6 && Math.Abs(Math.Max(a.X, c.X) - xb) < 1e-6;
            });
            if (edge != null)
            {
                double sg = _p.Node(edge.NodeJ).X > _p.Node(edge.NodeI).X ? -f : f;   // vồng về phía −Y
                r = ModelEdit.SetBeamCurvature(_p, edge, sg);
            }
        }
        StLog("dầm cong: " + r.Message);
        if (!r.Ok) return;
        PushUndo(snap);
        _p.InvalidateIndex();
        MarkDirty(); RefreshAll(false);
        var fr = _p.Frames.FirstOrDefault(q => q.Id == r.NewFrames.FirstOrDefault()) ?? _p.Frames.LastOrDefault(q => q.IsCurved);

        if (!await RunAnalysis()) throw new Exception("Phân tích lại (dầm cong) lỗi");
        fr = _p.Frames.FirstOrDefault(q => fr != null && q.Id == fr.Id) ?? _p.Frames.LastOrDefault(q => q.IsCurved);
        if (fr != null && _res != null && _res.Cases.Count > 0)
        {
            var cr = _res.Cases.Values.First();
            if (cr.Frames.TryGetValue(fr.Id, out var ff) && ff.Count > 0)
                StLog($"dầm cong {fr.Label}: L cung = {_p.Length(fr):0.000} m, |T| max ({cr.Name}) = {Enumerable.Range(0, ff.Count).Max(i => Math.Abs(ff.T(i))):0.00} kN.m");
        }
        cbStory.SelectedItem = story.Name;
        Select(frame: fr);
        tabView.SelectedItem = ViewTab("Mặt bằng");
        await Pause(1500); Shot($"{tag}_16_DamCong");

        tabView.SelectedItem = ViewTab("3D");
        Select(frame: fr);
        await Pause(2000); Shot($"{tag}_17_DamCong3D");

        tabView.SelectedItem = ViewTab("Biểu đồ nội lực");
        cbDiaView.SelectedIndex = 0;
        int iT = Array.FindIndex(DiaQty, q => q.Comp == 3);   // mô men xoắn T
        cbDiaQty.SelectedIndex = iT >= 0 && iT < cbDiaQty.Items.Count ? iT : 0;
        Select(frame: fr);
        await Pause(2000); Shot($"{tag}_18_DamCongBieuDo");
        cbDiaQty.SelectedIndex = 0;
    }
}

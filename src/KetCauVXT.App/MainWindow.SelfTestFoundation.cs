using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest): thêm dầm, tường, gán cấu tạo sàn; thiết kế móng cọc, móng băng và chụp ảnh.</summary>
public partial class MainWindow
{
    private async Task SelfTestWallsFoundation(string tag, Story story)
    {
        // ---- dầm giữa hai giao trục (nếu đã có dầm thì thêm dầm phụ giữa nhịp) ----
        var gxs = _p.GridsX.ToList(); var gys = _p.GridsY.ToList();
        RefreshEditCombos();
        string sec = cbEditBeamSec.SelectedItem as string
                     ?? _p.FrameSections.FirstOrDefault(s => s.Shape == SectionShape.RectConcrete && s.Name.StartsWith("D"))?.Name;
        if (gxs.Count >= 2 && gys.Count >= 2 && sec != null)
        {
            string snap = Snapshot();
            EditResult rb = null;
            // các cặp giao trục kề nhau chưa có dầm
            for (int i = 0; i < gxs.Count && (rb == null || !rb.Ok); i++)
                for (int j = 0; j < gys.Count && (rb == null || !rb.Ok); j++)
                {
                    if (i + 1 < gxs.Count) rb = ModelEdit.AddBeam(_p, story.Name, gxs[i].Coord, gys[j].Coord, gxs[i + 1].Coord, gys[j].Coord, sec);
                    if (rb?.Ok != true && j + 1 < gys.Count) rb = ModelEdit.AddBeam(_p, story.Name, gxs[i].Coord, gys[j].Coord, gxs[i].Coord, gys[j + 1].Coord, sec);
                }
            if (rb == null || !rb.Ok)
            {
                double ym = (gys[0].Coord + gys[1].Coord) / 2;
                rb = ModelEdit.AddBeam(_p, story.Name, gxs[0].Coord, ym, gxs[1].Coord, ym, sec);
            }
            StLog("dầm: " + rb.Message);
            if (rb.Ok) PushUndo(snap);

            // ---- tường loại đầu tiên trên trục Y đầu tiên ----
            var wt = _p.WallTypes.FirstOrDefault();
            if (wt != null)
            {
                var rw = ModelEdit.AddWall(_p, story.Name, gxs[0].Coord, gys[0].Coord, gxs[1].Coord, gys[0].Coord, wt.Name);
                StLog("tường: " + rw.Message);
                var b2 = ModelEdit.StoryBeams(_p, story.Name).FirstOrDefault(f => Math.Abs(_p.Node(f.NodeI).X - _p.Node(f.NodeJ).X) < 1e-6);
                if (b2 != null) StLog("gán tường lên dầm: " + ModelEdit.AssignWallToBeam(_p, b2, wt.Name, 0, 1.6).Message);
            }
        }
        // ---- cấu tạo sàn ----
        var slab = _p.Slabs.LastOrDefault(s => s.Story == story.Name);
        var bu = _p.FloorBuildUps.FirstOrDefault(b => b.Name == "Sàn vệ sinh") ?? _p.FloorBuildUps.FirstOrDefault();
        if (slab != null && bu != null) StLog("cấu tạo sàn: " + ModelEdit.SetBuildUp(_p, slab, bu.Name).Message);
        _p.InvalidateIndex();
        MarkDirty(); RefreshAll(false);
        tabView.SelectedItem = ViewTab("Mặt bằng");
        Select(wall: _p.Walls.LastOrDefault(w => w.Story == story.Name));
        await Pause();

        // ---- móng cọc ----
        tabView.SelectedItem = ViewTab("Mô hình móng");
        _p.Foundation.Type = FoundationType.Pile;
        if (!await DesignFoundation()) throw new Exception("Thiết kế móng cọc lỗi");
        StLog($"móng cọc: {_design.PileCaps.Count} đài, {_design.PileCaps.Count(c => !c.Ok)} không đạt");
        var pc = _design.PileCaps.FirstOrDefault();
        _fndSelItem = pc;
        Select(footingNode: pc?.NodeIds.FirstOrDefault() ?? 0);
        await Pause(1500); Shot($"{tag}_10_MongCoc");

        // ---- móng băng ----
        _p.Foundation.Type = FoundationType.Strip;
        if (!await DesignFoundation()) throw new Exception("Thiết kế móng băng lỗi");
        StLog($"móng băng: {_design.StripFootings.Count} dải, {_design.StripFootings.Count(c => !c.Ok)} không đạt");
        var sf = _design.StripFootings.FirstOrDefault();
        _fndSelItem = sf;
        Select(footingNode: sf?.NodeIds.FirstOrDefault() ?? 0);
        await Pause(1500); Shot($"{tag}_11_MongBang");

        _p.Foundation.Type = FoundationType.Isolated;
        MarkDirty(false);
        SyncFoundationUi();
    }
}

using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest) công cụ dầm kiểu ETABS: mở cửa sổ khai báo tiết diện (chụp ảnh), thêm 2 dầm phụ trong một ô
/// của tầng, vẽ nhanh một đoạn trục còn trống, chọn các dầm mới và chụp mặt bằng; cuối cùng hoàn tác về mô hình trước khi thử
/// (giữ kết quả phân tích / thiết kế đang có cho các bước sau).</summary>
public partial class MainWindow
{
    private async Task SelfTestBeamTools(string tag, Story story)
    {
        string snap = Snapshot();
        var res0 = _res; var des0 = _design;
        bool dirty0 = _dirty;
        bool changed = false;
        try
        {
            // 1. cửa sổ khai báo tiết diện (không chặn)
            OpenSectionManager();
            var w = _secWin;
            if (w != null)
            {
                await Pause(1200);
                ShotWindow(w, $"{tag}_45_KhaiBaoTietDien");
                StLog($"khai báo tiết diện: {_p.FrameSections.Count} tiết diện");
                w.Close();
            }

            // 2. dầm phụ + vẽ nhanh theo lưới
            RefreshEditCombos();
            string sec = cbEditBeamSec.SelectedItem as string
                         ?? _p.FrameSections.FirstOrDefault(s => s.Shape == SectionShape.RectConcrete && s.Name.StartsWith("D"))?.Name;
            var gxs = _p.GridsX.ToList(); var gys = _p.GridsY.ToList();
            if (sec == null || gxs.Count < 2 || gys.Count < 2) { StLog("công cụ dầm: bỏ qua (thiếu tiết diện dầm hoặc lưới trục)"); return; }
            double cx = (gxs[0].Coord + gxs[1].Coord) / 2, cy = (gys[0].Coord + gys[1].Coord) / 2;
            var bay = BeamTools.BayAt(_p, story.Name, cx, cy);
            var newIds = new List<int>();
            if (bay != null)
            {
                var b = bay.Value;
                var opt = new SecondaryBeamOptions { AlongX = b.X1 - b.X0 < b.Y1 - b.Y0, Count = 2, Section = sec };
                var r1 = BeamTools.SecondaryBeams(_p, story.Name, b, opt);
                StLog("dầm phụ: " + r1.Message);
                if (r1.Ok) { newIds.AddRange(r1.NewFrames); changed = true; }
            }
            else StLog("dầm phụ: không tìm thấy ô tại giữa nhịp đầu tiên");
            var free = BeamTools.GridSegments(_p).FirstOrDefault(sg => BeamTools.Uncovered(_p, story.Name, sg.X1, sg.Y1, sg.X2, sg.Y2).Count > 0);
            if (free != null)
            {
                var r2 = BeamTools.QuickBeamsOnGridSegments(_p, story.Name, new[] { free }, sec);
                StLog("vẽ nhanh theo lưới: " + r2.Message);
                if (r2.Ok) { newIds.AddRange(r2.NewFrames); changed = true; }
            }
            else StLog("vẽ nhanh theo lưới: mọi đoạn trục tầng " + story.Name + " đã có dầm");
            if (!changed) return;
            PushUndo(snap);
            _p.InvalidateIndex();
            MarkDirty(); RefreshAll(false);
            cbStory.SelectedItem = story.Name;
            tabView.SelectedItem = ViewTab("Mặt bằng");
            _multiSel.Clear(); _multiSelOwner = _p;
            foreach (var id in newIds) if (_p.Frames.Any(f => f.Id == id)) _multiSel.Add(id);
            Select(frame: _p.Frames.FirstOrDefault(f => newIds.Contains(f.Id)));
            await Pause(1500);
            if (_beamToolsExpander != null) { _beamToolsExpander.IsExpanded = true; await Pause(600); }
            Shot($"{tag}_46_DamPhuTheoKhoangCach");
            if (_beamToolsExpander != null) { _beamToolsExpander.IsExpanded = false; await Pause(300); }
        }
        finally
        {
            // hoàn tác toàn bộ: trả lại mô hình (và kết quả) trước khi thử
            _multiSel.Clear();
            if (changed || Snapshot() != snap)
            {
                RestoreSnapshot(snap);
                if (_undo.Count > 0 && _undo[^1] == snap) _undo.RemoveAt(_undo.Count - 1);
                UpdateUndoButton();
                _res = res0; _design = des0;
                _dirty = dirty0; UpdateTitle();
                RefreshResults(); RefreshDesign(); RedrawPlan(); ShowSelection();
            }
            StLog("công cụ dầm: đã hoàn tác về mô hình trước khi thử");
        }
    }
}

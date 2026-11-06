using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest) giao diện kiểu Revit: bật bố cục Revit, vẽ một tường gạch 220 trát 2 mặt trên trục đầu,
/// đặt cửa đi + cửa sổ (tường tự cắt ô cửa), chọn cửa (kích thước tạm) và chụp mặt bằng; mở "Sửa kiểu tường" (chụp ảnh);
/// cuối cùng trả lại mô hình và giao diện cổ điển cho các bước sau.</summary>
public partial class MainWindow
{
    private async Task SelfTestRevit(string tag, Story story)
    {
        string snap = Snapshot();
        var res0 = _res; var des0 = _design;
        bool dirty0 = _dirty;
        try
        {
            if (_rvRibbon == null) { StLog("giao diện Revit: chưa dựng được ribbon – bỏ qua"); return; }
            WallAssembly.Upgrade(_p);
            ApplyRevitLayout(true, false);
            cbStory.SelectedItem = story.Name;
            tabView.SelectedItem = ViewTab("Mặt bằng");
            var gxs = _p.GridsX.ToList(); var gys = _p.GridsY.ToList();
            if (gxs.Count < 2 || gys.Count < 1) { StLog("giao diện Revit: thiếu lưới trục"); return; }
            string wt = "Gạch đặc 220 trát 2 mặt";
            double y0 = gys[0].Coord, x0 = gxs[0].Coord, x1 = gxs[^1].Coord;
            var r = ModelEdit.AddWall(_p, story.Name, x0, y0, x1, y0, wt, 0, -1);
            StLog("giao diện Revit – vẽ tường: " + r.Message);
            var w = _p.Walls.LastOrDefault(q => q.Type == wt && q.Story == story.Name);
            if (w == null) return;
            var rd = WallAssembly.PlaceOpening(_p, w, "D1 900x2200", Math.Min(1.2, w.Chord / 3));
            var rs = WallAssembly.PlaceOpening(_p, w, "S1 1200x1400", w.Chord * 0.7);
            StLog($"giao diện Revit – cửa: {rd.Message} | {rs.Message}; đoạn tường đặc: {WallAssembly.SolidSegments(_p, w).Count}");
            _p.InvalidateIndex(); MarkDirty(); RefreshAll(false);
            if (w.Openings.Count > 0) SelectHostedOpening(w, w.Openings[0]);
            await Pause(1200);
            Shot($"{tag}_46c_Revit_CuaCatTuong");
            var dlg = new WallTypeEditorWindow(_p, "Tường 220 ốp đá granite mặt ngoài") { Owner = this };
            dlg.Show();
            await Pause(1000);
            ShotWindow(dlg, $"{tag}_46d_SuaKieuTuong");
            dlg.Close();
            // [Hoàn thiện] tường đang chọn: tay nắm 2 đầu + ô giữa (kéo để kéo dài / dời)
            _rvSelOpen = null; _rvSelWall = null;
            Select(wall: w);
            RedrawPlan();
            await Pause(700);
            StLog($"giao diện Revit – tay nắm tường đang chọn: {Grips().Count}");
            Shot($"{tag}_46e_Revit_TayNam");
            // [Hoàn thiện] 3D: tường khoét ô cửa thật
            var pieces = WallSolid3D.Pieces(_p, w, WallSolid3D.DefaultHeight(_p, w));
            StLog($"giao diện Revit – 3D tường quanh {w.Openings.Count} ô cửa: {pieces.Count} khối");
            ClearSelection();
            tabView.SelectedItem = ViewTab("3D");
            await Pause(400);
            BuildMain3D(true);
            await Pause(1200);
            Shot($"{tag}_46f_Revit_3D_TuongCoCua");
            // [Đặt & xem] đồ đạc đặt bằng bắt điểm (lưng tựa mặt tường) → chọn trong 3D; mặt cắt 1-1 tự sinh
            var bed = new LibraryItem { Id = "selftest-giuong", Name = "Giường đôi 1,8 m", Category = "Nội thất", Width = 1.8, Depth = 2.0, Height = 0.5 };
            var sn = FurnitureSnap.Snap(_p, story.Name, (x0 + x1) / 2, y0 + 1.2, bed.Width, bed.Depth, 0);
            var placed = FurnitureSnap.Place(_p, bed, story.Name, sn);
            var dims = TempDimensions.ForRect(_p, story.Name, placed.X, placed.Y, placed.Width, placed.Depth, placed.Rotation, true, placed.Id);
            StLog($"đặt đồ đạc bắt điểm: {sn.Kind}, góc {sn.Rotation:0}°, {dims.Count} kích thước tạm ({string.Join("; ", dims.Select(d => d.Dir + "=" + (d.Value * 1000).ToString("0")))})");
            _v3SelId = "DD-" + placed.Id;
            BuildMain3D(true);
            await Pause(1000);
            Shot($"{tag}_46g_Revit_3D_DoDacChon");
            var secWin = OpenSectionView(KetCauVXT.Drawing.SectionView.Defaults(_p).FirstOrDefault());
            if (secWin != null)
            {
                await Pause(1200);
                ShotWindow(secWin, $"{tag}_46h_MatCat_1_1");
                secWin.Close();
            }
            StLog($"khung nhìn mặt cắt ngang tầng: {LevelViews.Build(_p).Count}; mặt cắt đứng: {KetCauVXT.Drawing.SectionView.All(_p).Count}");
            _v3SelId = null;
            tabView.SelectedItem = ViewTab("Mặt bằng");
        }
        catch (Exception ex) { StLog("giao diện Revit: lỗi " + ex.Message); }
        finally
        {
            _rvSelOpen = null; _rvSelWall = null;
            StopPlaceOpening();
            ApplyRevitLayout(false, false);
            if (Snapshot() != snap)
            {
                RestoreSnapshot(snap);
                if (_undo.Count > 0 && _undo[^1] == snap) _undo.RemoveAt(_undo.Count - 1);
                UpdateUndoButton();
                _res = res0; _design = des0;
                _dirty = dirty0; UpdateTitle();
                RefreshResults(); RefreshDesign(); RedrawPlan(); ShowSelection();
            }
            StLog("giao diện Revit: đã trả lại mô hình và giao diện cổ điển");
        }
    }
}

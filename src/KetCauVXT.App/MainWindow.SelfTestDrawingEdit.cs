using System.IO;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;

namespace KetCauVXT.App;

/// <summary>Tự kiểm tra "Hiệu chỉnh bản vẽ": mở cửa sổ hiệu chỉnh (không chặn) trên bản vẽ chi tiết dầm, dời một nhãn thép và sửa chữ tên hình
/// bằng lệnh, chụp ảnh cửa sổ, xuất bộ bản vẽ gộp 1 file DXF vào thư mục tự kiểm tra, ghi nhật ký, rồi hoàn lại chỉnh sửa.</summary>
public partial class MainWindow
{
    private async Task SelfTestDrawingEdit(string tag)
    {
        var dsg = _design ?? (_res != null ? ConcreteDesigner.Design(_p, _res) : null);
        string before = DrawingOverlay.Snapshot(_p);
        int frozenBefore = _p.FrozenDrawings?.Count ?? 0;
        var w = new DrawingEditWindow(_p, _res, dsg, null) { Owner = this, Width = 1500, Height = 900 };
        w.Show();
        try
        {
            await Pause(600);
            var beam = w.Drawings.FirstOrDefault(d => d.Kind == "DS" && d.Number.StartsWith("KC-04", StringComparison.Ordinal))
                       ?? w.Drawings.FirstOrDefault();
            if (beam == null) throw new Exception("Hiệu chỉnh bản vẽ: không có bản vẽ nào");
            w.SelectDrawing(beam.Key);
            await Pause(1200);
            StLog($"hiệu chỉnh bản vẽ: mở {beam.Number} – {beam.Title}, {w.Shown?.EntityCount ?? 0} thực thể");

            // dời một nhãn thép 6 mm sang phải, 4 mm lên trên (mm giấy)
            bool tag1 = w.SelectWhere(e => e.Group != null && e.Group.StartsWith("TAG|", StringComparison.Ordinal), true);
            double x0 = 0;
            if (tag1) x0 = w.Shown.EntityRefs().Where(e => e.Group != null && e.Group.StartsWith("TAG|", StringComparison.Ordinal) && e.Type == "TEXT").Select(e => e.X).FirstOrDefault();
            if (tag1) w.MoveSelection(6, 4);
            await Pause(300);
            // sửa chữ tên hình (D2-1 (SL=…; L=…) → thêm hậu tố)
            bool txt = w.SelectWhere(e => e.Type == "TEXT" && e.Group != null && e.Group.StartsWith("TITLE|", StringComparison.Ordinal), true);
            string newText = null;
            if (txt)
            {
                var t = w.Shown.EntityRefs().First(e => e.Type == "TEXT" && e.Group != null && e.Group.StartsWith("TITLE|", StringComparison.Ordinal));
                newText = t.Text + " – ĐÃ HIỆU CHỈNH";
                w.EditSelectedText(newText);
            }
            await Pause(800);
            bool hasText = newText != null && w.Shown.Texts().Contains(newText);
            StLog($"hiệu chỉnh bản vẽ: dời nhãn {(tag1 ? "có" : "KHÔNG CÓ nhãn")} (x trước {x0:0}), sửa chữ {(hasText ? "đúng" : "SAI")}; {_p.DrawingEdits.Count} chỉnh sửa, không còn áp dụng: {w.Report.Stale.Count}");
            ShotWindow(w, $"{tag}_35_HieuChinhBanVe");

            // xuất bộ bản vẽ gộp 1 file (chỉnh sửa được áp lại)
            string dir = Path.Combine(SelfTestOut, tag + "_BanVeGop");
            var er = DrawingSet.Export(_p, dsg, dir, DrawingExportOptions.SingleFile(tag + "_BoBanVe"));
            string f = er.Files.FirstOrDefault();
            bool inFile = f != null && newText != null && File.ReadAllText(f).Contains(DxfWriter.Encode(newText));
            StLog($"hiệu chỉnh bản vẽ: xuất gộp {er.Regions.Count} bản vẽ → {Path.GetFileName(f)} ({(f != null ? new FileInfo(f).Length / 1024 : 0)} KB), chữ đã sửa trong file: {(inFile ? "có" : "KHÔNG")}" +
                  (er.Messages.Count > 0 ? "; lưu ý: " + string.Join(" · ", er.Messages.Take(3)) : ""));
            if (txt && !inFile) throw new Exception("Hiệu chỉnh bản vẽ: chữ đã sửa không có trong DXF xuất gộp");
        }
        finally
        {
            DrawingOverlay.Restore(_p, before);   // hoàn lại chỉnh sửa
            if ((_p.FrozenDrawings?.Count ?? 0) != frozenBefore) StLog("hiệu chỉnh bản vẽ: CẢNH BÁO số bản khoá thay đổi");
            w.Close();
            StLog("hiệu chỉnh bản vẽ: đã hoàn lại chỉnh sửa");
        }
    }
}

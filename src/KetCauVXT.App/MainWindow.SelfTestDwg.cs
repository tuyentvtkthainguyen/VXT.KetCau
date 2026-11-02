using System.IO;
using KetCauVXT.Import;
using KetCauVXT.Import.Arch;

namespace KetCauVXT.App;

/// <summary>Tự kiểm tra đọc DWG: ghi bản vẽ DWG mẫu bằng ACadSharp (KetCauVXT.Dwg.DwgSample) → đọc qua đúng đường nhập của menu
/// "Nhập bản vẽ kiến trúc" (ArchImportDialog nạp sẵn file .dwg, bộ đọc tích hợp đăng ký trong App.xaml.cs) → chụp ảnh hộp thoại → đóng,
/// không ghi vào mô hình.</summary>
public partial class MainWindow
{
    private async Task SelfTestDwg(string tag)
    {
        string path = Path.Combine(SelfTestOut, tag + "_selftest.dwg");
        KetCauVXT.Dwg.DwgSample.WriteHouse(path);
        StLog($"đọc DWG: ghi {path} ({new FileInfo(path).Length} byte, {KetCauVXT.Dwg.DwgReader.ReadSignature(path)})");
        if (!CadFileReaders.HasDwgReader) throw new Exception("Đọc DWG: bộ đọc DWG tích hợp chưa được đăng ký (App.xaml.cs)");

        // kiểm tra lõi trước (không hộp thoại) – lỗi thì dừng để không treo vì MessageBox
        var rd = DwgConverter.ReadAny(path, 0, out string err);
        if (rd == null) throw new Exception("Đọc DWG: " + err);
        StLog("đọc DWG: " + KetCauVXT.Dwg.DwgReader.Describe(rd, Path.GetFileName(path)).Replace("\n", " | "));
        var d = ArchPlanReader.Read(path, new ArchImportOptions());
        StLog("đọc DWG: " + string.Join(" | ", d.Floors.Select(f => f.ToString())));
        foreach (var w in d.Warnings) StLog("  ! " + w);
        if (d.Floors.Count == 0 || d.Floors.All(f => f.Walls.Count == 0))
            throw new Exception("Đọc DWG: không nhận diện được mặt bằng có tường");
        var prop = ArchToStructure.Propose(d, new ArchImportOptions());
        StLog($"đọc DWG: đề xuất {prop.Levels.Count} cao trình, {prop.Columns.Count} cột, {prop.Levels.Sum(l => l.Beams.Count)} dầm");

        // cùng đường nhập với menu, mở không chặn (Show) để chụp ảnh rồi đóng – không tạo mô hình
        var dlg = new ArchImportDialog { Owner = this, Width = 1400, Height = 850 };
        dlg.PresetFiles(new[] { path });
        dlg.Show();
        for (int i = 0; i < 40 && dlg.Proposal == null; i++) await Pause(500);
        await Pause(1000);
        StLog(dlg.Proposal != null
            ? $"đọc DWG: hộp thoại – {dlg.Drawing?.Floors.Count} tầng, {dlg.Proposal.Columns.Count} cột, {dlg.Proposal.Levels.Count} cao trình"
            : "đọc DWG: hộp thoại chưa lập xong phương án sau 20 s");
        ShotWindow(dlg, $"{tag}_24_DocDWG");
        dlg.Close();
        if (dlg.Drawing == null || dlg.Drawing.Floors.Count == 0) throw new Exception("Đọc DWG: hộp thoại không đọc được bản vẽ DWG");
    }
}

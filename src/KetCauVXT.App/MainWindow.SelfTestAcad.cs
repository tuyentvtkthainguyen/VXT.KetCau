using System.IO;
using KetCauVXT.Import.Acad;
using KetCauVXT.Import.Arch;

namespace KetCauVXT.App;

/// <summary>Tự kiểm tra liên kết AutoCAD: ghi file trao đổi mẫu (như lệnh VXT_GUI của add-in) → đọc qua đúng đường nhập của menu
/// "Tệp → Nhận dữ liệu từ AutoCAD…" (ArchImportDialog nạp sẵn dữ liệu) → chụp ảnh hộp thoại → đóng, không ghi vào mô hình.</summary>
public partial class MainWindow
{
    /// <summary>Dữ liệu mẫu: nhà 4,5×8 m, 2 tầng vẽ cạnh nhau (tầng 2 lệch 15 m), đơn vị m như add-in ghi.</summary>
    private static AcadExchange SelfTestAcadSample()
    {
        var x = new AcadExchange { Source = "AutoCAD (tự kiểm tra)", Dwg = "TuKiemTra.dwg", Units = "m" };
        for (int k = 0; k < 2; k++)
        {
            double ox = k * 15;
            var s = new AcadStorey { Name = k == 0 ? "Tầng 1" : "Tầng 2", Elevation = k * 3.6, Height = k == 0 ? 3.6 : 3.3, BaseX = ox, X0 = ox - 3, Y0 = -3, X1 = ox + 7, Y1 = 11 };
            x.Storeys.Add(s);
            void L(string cat, double x1, double y1, double x2, double y2) =>
                s.Items.Add(new AcadItem { Category = cat, Kind = "Line", Layer = "KT-" + cat.ToUpperInvariant(), Points = { new[] { ox + x1, y1 }, new[] { ox + x2, y2 } } });
            void T(string cat, double px, double py, string t) =>
                s.Items.Add(new AcadItem { Category = cat, Kind = "Text", Layer = "KT-TEXT", Text = t, Height = 0.3, Points = { new[] { ox + px, py } } });
            // tường 220 hai nét
            foreach (double d in new[] { -0.11, 0.11 })
            {
                L(AcadCategory.Tuong, 0, d, 4.5, d); L(AcadCategory.Tuong, 0, 8 + d, 4.5, 8 + d);
                L(AcadCategory.Tuong, d, 0, d, 8); L(AcadCategory.Tuong, 4.5 + d, 0, 4.5 + d, 8);
            }
            foreach (double d in new[] { -0.055, 0.055 }) { L(AcadCategory.Tuong, 0.11, 4 + d, 3.2, 4 + d); L(AcadCategory.Tuong, 4.1, 4 + d, 4.39, 4 + d); }
            foreach (var (gx, n) in new[] { (0.0, "1"), (4.5, "2") }) { L(AcadCategory.Truc, gx, -1.5, gx, 9.5); T(AcadCategory.Truc, gx, 9.9, n); }
            foreach (var (gy, n) in new[] { (0.0, "A"), (4.0, "B"), (8.0, "C") }) { L(AcadCategory.Truc, -1.5, gy, 6, gy); T(AcadCategory.Truc, -1.9, gy, n); }
            foreach (double cx in new[] { 0.0, 4.5 })
                foreach (double cy in new[] { 0.0, 4.0, 8.0 })
                    s.Items.Add(new AcadItem
                    {
                        Category = AcadCategory.Cot, Kind = "Polyline", Layer = "KT-COT", Closed = true,
                        Points = { new[] { ox + cx - 0.11, cy - 0.11 }, new[] { ox + cx + 0.11, cy - 0.11 }, new[] { ox + cx + 0.11, cy + 0.11 }, new[] { ox + cx - 0.11, cy + 0.11 } },
                    });
            s.Items.Add(new AcadItem { Category = AcadCategory.Cua, Kind = "Line", Layer = "0", Block = "CUA_D1", Points = { new[] { ox + 3.2, 4.0 }, new[] { ox + 3.2, 4.9 } } });
            s.Items.Add(new AcadItem { Category = AcadCategory.Cua, Kind = "Arc", Layer = "0", Block = "CUA_D1", Radius = 0.9, StartAngle = 0, EndAngle = 90, Points = { new[] { ox + 3.2, 4.0 } } });
            L(AcadCategory.Dam, 0, 4, 4.5, 4);
            T(AcadCategory.TenPhong, 2.2, 2.0, k == 0 ? "P.KHÁCH" : "P.NGỦ 1");
            T(AcadCategory.TenPhong, 2.2, 6.0, k == 0 ? "BẾP" : "P.NGỦ 2");
        }
        return x;
    }

    private async Task SelfTestAcadLink(string tag)
    {
        string path = Path.Combine(SelfTestOut, tag + "_acad_selftest.json");
        SelfTestAcadSample().Save(path);
        StLog("liên kết AutoCAD: ghi " + path);

        // kiểm tra lõi trước (không hộp thoại) – lỗi thì dừng để không treo vì MessageBox
        var res = AcadLinkReader.Read(path, new ArchImportOptions());
        StLog("liên kết AutoCAD: " + res + " – " + string.Join(" | ", res.Drawing.Floors.Select(f => f.ToString())));
        foreach (var w in res.Drawing.Warnings) StLog("  ! " + w);
        if (res.Drawing.Floors.Count != 2 || res.Drawing.Floors.Any(f => f.Walls.Count == 0))
            throw new Exception("Liên kết AutoCAD: không nhận diện đủ 2 tầng có tường");
        var prop = ArchToStructure.Propose(res.Drawing, new ArchImportOptions());
        StLog($"liên kết AutoCAD: đề xuất {prop.Levels.Count} cao trình, {prop.Columns.Count} cột, {prop.Levels.Sum(l => l.Beams.Count)} dầm");

        // cùng đường nhập với menu, mở không chặn (Show) để chụp ảnh rồi đóng – không tạo mô hình
        var dlg = new ArchImportDialog { Owner = this, Width = 1400, Height = 850 };
        dlg.PresetAcadLink(path);
        dlg.Show();
        for (int i = 0; i < 40 && dlg.Proposal == null; i++) await Pause(500);
        await Pause(1000);
        StLog(dlg.Proposal != null
            ? $"liên kết AutoCAD: hộp thoại – {dlg.Drawing?.Floors.Count} tầng, {dlg.Proposal.Columns.Count} cột, {dlg.Proposal.Levels.Count} cao trình"
            : "liên kết AutoCAD: hộp thoại chưa lập xong phương án sau 20 s");
        ShotWindow(dlg, $"{tag}_23_AcadLink");
        dlg.Close();
        if (dlg.Proposal == null) throw new Exception("Liên kết AutoCAD: hộp thoại không lập được phương án kết cấu");
    }
}

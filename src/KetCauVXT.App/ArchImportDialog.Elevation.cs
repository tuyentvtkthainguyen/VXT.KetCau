using System.IO;
using System.Windows;
using Microsoft.Win32;
using KetCauVXT.Import.Arch;

namespace KetCauVXT.App;

/// <summary>[Mặt đứng] Đọc bản vẽ mặt đứng / mặt cắt (DXF/DWG): cao độ các sàn (+3.600, ±0.000, COS +7.200…) → chiều cao từng tầng
/// gán cho các mặt bằng tầng đã nhận diện (theo thứ tự tầng). Lõi: Import/Arch/ElevationReader.cs.</summary>
public partial class ArchImportDialog
{
    private List<LevelStory> _levels;

    private void BtnElevation_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Mặt đứng / mặt cắt (*.dxf;*.dwg)|*.dxf;*.dwg|Tất cả (*.*)|*.*", Title = "Chọn bản vẽ mặt đứng hoặc mặt cắt kiến trúc" };
        if (dlg.ShowDialog(this) != true) return;
        var warn = new List<string>();
        double scale = cbUnit.SelectedIndex switch { 1 => 0.001, 2 => 0.01, 3 => 1.0, _ => 0 };
        List<LevelMark> lv;
        try { lv = ElevationReader.FromFile(dlg.FileName, scale, warn); }
        catch (Exception ex) { Warn("Không đọc được mặt đứng: " + ex.Message); return; }
        var st = ElevationReader.Stories(lv);
        if (st.Count == 0)
        {
            Warn("Không đọc được cao độ tầng trên " + Path.GetFileName(dlg.FileName) + " (cần chữ cao độ dạng +3.600, ±0.000, COS +7.200…)."
                 + (warn.Count > 0 ? "\n" + string.Join("\n", warn) : ""));
            return;
        }
        _levels = st;
        string list = string.Join("\n", lv.Select(l => $"  {(l.Elevation >= 0 ? "+" : "")}{l.Elevation:0.000}  {l.Name}"));
        string stories = string.Join("\n", st.Select(s => $"  {s.Name}: cao độ {(s.Elevation >= 0 ? "+" : "")}{s.Elevation:0.000}, cao {s.Height:0.00} m"));
        int n = ApplyLevels(true);
        MessageBox.Show(this, $"Đọc được {lv.Count} cao độ:\n{list}\n\n{st.Count} tầng:\n{stories}\n\n"
                              + (Drawing == null ? "Bấm \"Nhận diện\" – chiều cao tầng sẽ gán cho các mặt bằng theo thứ tự." : $"Đã gán chiều cao cho {n} mặt bằng tầng (theo thứ tự tầng)."),
            "Cao độ từ mặt đứng", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>Gán chiều cao tầng cho các mặt bằng đang dùng (theo thứ tự tầng, bỏ mặt bằng mái). Trả số mặt bằng đã gán.</summary>
    private int ApplyLevels(bool refresh)
    {
        if (_levels == null || _levels.Count == 0 || Drawing == null) return 0;
        var floors = Drawing.Floors.Where(f => f.Include && !f.IsRoofPlan).OrderBy(f => f.FloorNo).ToList();
        int n = 0;
        for (int i = 0; i < floors.Count && i < _levels.Count; i++) { floors[i].Height = _levels[i].Height; n++; }
        // chiều cao tầng điển hình = chiều cao gặp nhiều nhất
        var typ = _levels.GroupBy(l => Math.Round(l.Height, 2)).OrderByDescending(g => g.Count()).First().Key;
        txtTypicalHeight.Text = typ.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        if (refresh) { dgFloors.ItemsSource = null; dgFloors.ItemsSource = Drawing.Floors; }
        SetStatus($"Chiều cao tầng theo mặt đứng: {string.Join(" / ", _levels.Select(l => l.Height.ToString("0.##")))} m" + (n > 0 ? $" – đã gán {n} mặt bằng." : "."));
        return n;
    }
}

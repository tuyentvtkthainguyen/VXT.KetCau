using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KetCauVXT.Import;
using KetCauVXT.Import.Link;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>[Xref] Gắn bản vẽ AutoCAD (DWG / DXF) làm nền mặt bằng giống XREF: kéo thả file vào khung nhìn mặt bằng
/// hoặc bấm "Gắn bản vẽ (Xref)…" trên thanh khung nhìn. Dùng chung tính năng "Bản vẽ liên kết" (CadLink): theo dõi file,
/// tự đọc lại khi file đổi, ẩn/hiện layer, căn theo 2 điểm, gỡ. Chỉ đọc file cục bộ.</summary>
public partial class MainWindow
{
    private static bool IsCadFile(string f)
    {
        string ext = Path.GetExtension(f ?? "").ToLowerInvariant();
        return ext == ".dwg" || ext == ".dxf";
    }

    private static List<string> DroppedCadFiles(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return new List<string>();
        return ((e.Data.GetData(DataFormats.FileDrop) as string[]) ?? Array.Empty<string>()).Where(IsCadFile).ToList();
    }

    private void PlanHost_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = _p != null && !_busy && DroppedCadFiles(e).Count > 0 ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private async void PlanHost_Drop(object sender, DragEventArgs e)
    {
        var files = DroppedCadFiles(e);
        e.Handled = true;
        if (_p == null || _busy || files.Count == 0)
        {
            if (_p != null && files.Count == 0) SetStatus("Chỉ gắn được bản vẽ .dwg / .dxf vào mặt bằng.");
            return;
        }
        var (wx, wy) = S2W(e.GetPosition(planHost));
        foreach (var f in files) await XrefAttach(f, (wx, wy));
    }

    private async void BtnXrefAttach_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null) return;
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = CadFileReaders.OpenFilter, Title = "Gắn bản vẽ AutoCAD làm nền mặt bằng (giống XREF)", Multiselect = true };
        try { if (_path != null) dlg.InitialDirectory = Path.GetDirectoryName(_path); } catch (Exception) { }
        if (dlg.ShowDialog(this) != true) return;
        foreach (var f in dlg.FileNames) await XrefAttach(f, null);
    }

    private void BtnXrefManage_Click(object sender, RoutedEventArgs e) => OpenCadLinkManager();

    /// <summary>Gắn một bản vẽ cho tầng đang xem (hoặc mọi tầng khi đang xem "(Tất cả)").
    /// Đặt bản vẽ như XREF: nếu toạ độ bản vẽ đã trùng vùng mô hình → giữ gốc toạ độ (chèn tại 0,0);
    /// ngược lại góc trái-dưới bản vẽ đặt tại điểm thả (kéo thả) hoặc tại góc trái-dưới mô hình (nút bấm).</summary>
    internal async Task<CadLink> XrefAttach(string path, (double X, double Y)? dropAt)
    {
        if (_p == null || string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        string full = Path.GetFullPath(path);
        bool existed = _p.CadLinks.Any(q => string.Equals(q.Resolve(_path), full, StringComparison.OrdinalIgnoreCase));
        string st = CurrentStory == AllStories || _p.Stories.All(q => q.Name != CurrentStory) ? CadLink.AllStories : CurrentStory;
        var l = await CadLinkAdd(full, st);
        if (l == null || existed) return l;
        var g = CadLinkCache.Get(l);
        if (g == null || !g.HasExtent) return l;

        // vùng mô hình
        bool hasModel = _p.Nodes.Count > 0;
        double mx0 = hasModel ? _p.Nodes.Min(n => n.X) : 0, my0 = hasModel ? _p.Nodes.Min(n => n.Y) : 0;
        double mx1 = hasModel ? _p.Nodes.Max(n => n.X) : 0, my1 = hasModel ? _p.Nodes.Max(n => n.Y) : 0;
        var a = l.ToModel(g.X0, g.Y0); var b = l.ToModel(g.X1, g.Y1);
        double dx0 = Math.Min(a.X, b.X), dx1 = Math.Max(a.X, b.X), dy0 = Math.Min(a.Y, b.Y), dy1 = Math.Max(a.Y, b.Y);
        bool overlap = hasModel && dx0 <= mx1 + 1 && dx1 >= mx0 - 1 && dy0 <= my1 + 1 && dy1 >= my0 - 1
                       && (dx1 - dx0) < 50 * Math.Max(mx1 - mx0 + 1, my1 - my0 + 1);
        string how;
        if (overlap)
            how = "giữ gốc toạ độ bản vẽ (trùng vùng mô hình)";
        else
        {
            var target = dropAt ?? (hasModel ? (mx0, my0) : (0.0, 0.0));
            l.AlignByPoints(a, (Math.Round(target.X, 2), Math.Round(target.Y, 2)));
            how = dropAt != null ? "góc trái-dưới bản vẽ đặt tại điểm thả" : "góc trái-dưới bản vẽ đặt tại góc trái-dưới mô hình";
            CadLinkEdited(l);
        }
        _clWin?.Reload();
        SetStatus($"Đã gắn bản vẽ {l.DisplayName} (Xref) – tầng {l.Story}; {how}. Dùng \"Căn theo 2 điểm\" trong \"Bản vẽ liên kết…\" để căn đúng trục.");
        return l;
    }
}

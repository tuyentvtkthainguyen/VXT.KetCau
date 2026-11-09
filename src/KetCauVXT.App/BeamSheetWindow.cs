using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using KetCauVXT.Design;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>[Bản vẽ từng dầm] Chọn một dầm (D1, D2…) → xem trước bản vẽ riêng (mặt đứng, mặt cắt, thống kê thép), chọn kiểu / số thanh thép,
/// xuất DXF/DWG dầm này hoặc mỗi dầm một tệp.</summary>
internal sealed class BeamSheetWindow : Window
{
    private readonly Project _p;
    private readonly Func<DesignSummary> _design;
    private readonly Action<BeamLineDesign> _editRebar;
    private readonly ListBox _list = new() { MinWidth = 330 };
    private readonly DrawingHost _preview = new() { Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.WhiteSmoke };
    private readonly TextBlock _info = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };
    private readonly CheckBox _chkSchedule = new() { Content = "Kèm bảng thống kê thép", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
    private double _zoom = 1;
    private List<BeamLineDesign> _beams = new();

    public BeamSheetWindow(Project p, Func<DesignSummary> design, Action<BeamLineDesign> editRebar)
    {
        _p = p; _design = design; _editRebar = editRebar;
        DialogUi.Setup(this, "Bản vẽ từng dầm", 1280, 820);
        var root = new DockPanel { Margin = new Thickness(8) };
        var left = new DockPanel { Margin = new Thickness(0, 0, 8, 0) };
        var lt = DialogUi.Label("Chọn dầm:", double.NaN, true);
        DockPanel.SetDock(lt, Dock.Top);
        left.Children.Add(lt);
        var lb = new StackPanel();
        lb.Children.Add(DialogUi.Btn("Chọn bố trí thép dầm này…", (s, e) => EditRebar()));
        lb.Children.Add(DialogUi.Btn("Xuất bản vẽ dầm này…", (s, e) => ExportOne(), true));
        lb.Children.Add(DialogUi.Btn("Xuất mỗi dầm một tệp…", (s, e) => ExportEach()));
        foreach (var b in lb.Children.OfType<FrameworkElement>()) b.Margin = new Thickness(0, 3, 0, 0);
        DockPanel.SetDock(lb, Dock.Bottom);
        left.Children.Add(lb);
        left.Children.Add(_list);
        DockPanel.SetDock(left, Dock.Left);
        root.Children.Add(left);

        var top = DialogUi.Row(DialogUi.Btn("Phóng to", (s, e) => { _zoom *= 1.25; Refresh(); }), DialogUi.Btn("Thu nhỏ", (s, e) => { _zoom /= 1.25; Refresh(); }),
            DialogUi.Btn("Vừa khung", (s, e) => { _zoom = 1; Refresh(); }), _chkSchedule, DialogUi.Btn("Đóng", (s, e) => Close()));
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        DockPanel.SetDock(_info, Dock.Top);
        root.Children.Add(_info);
        _scroll.Content = _preview;
        root.Children.Add(_scroll);
        Content = root;
        _list.SelectionChanged += (s, e) => Refresh();
        _chkSchedule.Click += (s, e) => Refresh();
        _scroll.SizeChanged += (s, e) => { if (e.WidthChanged) Refresh(); };
        Reload(null);
    }

    /// <summary>Chọn sẵn dầm theo tên.</summary>
    public void Select(string name)
    {
        int i = _beams.FindIndex(b => b.Name == name);
        if (i >= 0) _list.SelectedIndex = i;
    }

    private BeamLineDesign Current => _list.SelectedIndex >= 0 && _list.SelectedIndex < _beams.Count ? _beams[_list.SelectedIndex] : null;

    private void Reload(string keepKey)
    {
        _beams = (_design()?.Beams ?? new List<BeamLineDesign>()).OrderBy(b => b.Story).ThenBy(b => NameKey(b.Name)).ToList();
        _list.ItemsSource = _beams.Select(BeamSheet.Label).ToList();
        int i = keepKey == null ? -1 : _beams.FindIndex(b => b.RebarKeys.Contains(keepKey));
        _list.SelectedIndex = i >= 0 ? i : _beams.Count > 0 ? 0 : -1;
        if (_beams.Count == 0) _info.Text = "Chưa có dầm BTCT đã thiết kế – chạy phân tích và thiết kế BTCT trước.";
    }

    private static string NameKey(string n)
    {
        var digits = new string((n ?? "").SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
        return (n ?? "").Length.ToString("000") + (digits.Length > 0 ? digits.PadLeft(6, '0') : n);
    }

    private void Refresh()
    {
        var d = Current;
        if (d == null) { _preview.Drawing = null; _preview.InvalidateVisual(); return; }
        try
        {
            var bars = d.Bars.Where(b => b.Role != "Đai").Sum(b => b.Count * b.WeightPerBar);
            var st = d.Bars.Where(b => b.Role == "Đai").Sum(b => b.Count * b.WeightPerBar);
            _info.Text = $"{d.Name}: {d.Section} – {RebarPreferences.BeamSchemeName(d.Scheme)}; thép dọc {bars:0.0} kg, đai {st:0.0} kg (1 dầm), SL = {Math.Max(1, d.Quantity)}. "
                         + (d.Ok ? "Đạt." : "CHƯA ĐẠT: " + string.Join("; ", d.Checks.Where(c => !c.Ok).Select(c => c.Name)));
            var figs = BeamSheet.Figures(_p, _design(), d, _chkSchedule.IsChecked == true);
            var items = new List<(DxfWriter W, double K, double Dx, double Dy)>();
            double cx = 0, rowTop = 0, rowH = 0, maxW = 0, gap = 12;
            foreach (var f in figs)
            {
                var e = f.W.Extents;
                if (e.W <= 0 || e.H <= 0) continue;
                double k = 1.0 / Math.Max(f.W.Scale, 1);
                double fw = e.W * k, fh = e.H * k;
                if (cx > 0 && (cx + fw > 600 || f.NewRow)) { rowTop -= rowH + gap; cx = 0; rowH = 0; }
                items.Add((f.W, k, cx - e.X1 * k, rowTop - e.Y2 * k));
                cx += fw + gap; rowH = Math.Max(rowH, fh); maxW = Math.Max(maxW, cx - gap);
            }
            if (items.Count == 0) { _preview.Drawing = null; _preview.InvalidateVisual(); return; }
            double totW = maxW, totH = -(rowTop - rowH), pad = 6;
            double fit = Math.Max(0.05, (_scroll.ActualWidth > 50 ? _scroll.ActualWidth - 24 : 900) / ((totW + 2 * pad) * SheetRender.DipPerMm));
            double zoom = fit * _zoom;
            _preview.Drawing = SheetRender.Render(items, -pad, pad, 0.12, x => x.GetPrimitives());
            _preview.Scale = SheetRender.DipPerMm * zoom;
            _preview.Width = (totW + 2 * pad) * SheetRender.DipPerMm * zoom;
            _preview.Height = (totH + 2 * pad) * SheetRender.DipPerMm * zoom;
            _preview.InvalidateVisual();
        }
        catch (Exception ex) { _info.Text = "Không vẽ được: " + ex.Message; }
    }

    private void EditRebar()
    {
        var d = Current;
        if (d == null || _editRebar == null) return;
        string key = d.RebarKeys.FirstOrDefault();
        _editRebar(d);
        Reload(key);
    }

    private static string Safe(string s) => string.Concat((s ?? "").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private string Ext => CadFileWriters.Current == CadOutputFormat.Dwg2018 && CadFileWriters.HasWriter ? ".dwg" : ".dxf";

    private void ExportOne()
    {
        var d = Current;
        if (d == null) return;
        var dlg = new SaveFileDialog
        {
            Filter = Ext == ".dwg" ? "Bản vẽ AutoCAD (*.dwg)|*.dwg" : "AutoCAD DXF (*.dxf)|*.dxf",
            FileName = Safe("ChiTietDam_" + d.Name + "_" + d.Story) + Ext
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            string real = BeamSheet.Export(_p, _design(), d, Path.ChangeExtension(dlg.FileName, ".dxf"));
            MessageBox.Show(this, "Đã xuất " + real, "Việt Const", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, "Không xuất được: " + ex.Message, "Việt Const", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void ExportEach()
    {
        if (_beams.Count == 0) return;
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục lưu bản vẽ từng dầm" };
        if (dlg.ShowDialog(this) != true) return;
        int n = 0;
        var err = new List<string>();
        foreach (var d in _beams)
        {
            try { BeamSheet.Export(_p, _design(), d, Path.Combine(dlg.FolderName, Safe("ChiTietDam_" + d.Name + "_" + d.Story) + ".dxf")); n++; }
            catch (Exception ex) { err.Add(d.Name + ": " + ex.Message); }
        }
        MessageBox.Show(this, $"Đã xuất {n} bản vẽ dầm vào {dlg.FolderName}" + (err.Count > 0 ? "\nLỗi: " + string.Join("\n", err) : ""), "Việt Const",
            MessageBoxButton.OK, err.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }
}

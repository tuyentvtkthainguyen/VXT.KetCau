using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// KHAI BÁO SÀN HẠ CỐT trên mặt bằng (Model/SlabDrop.cs):
//  • Nút chế độ "Hạ cốt sàn": bấm ô sàn để gán Δ ở ô "Hạ cốt (mm)" (bấm lại ô đã đúng Δ → bỏ hạ cốt). Hoàn tác Ctrl+Z.
//  • Nút "Hạ cốt sàn…": hộp thoại gán Δ + ghi chú cho ô đang chọn / mọi ô cùng công năng (tầng đang xem hoặc mọi tầng),
//    sửa bảng "Hạ cốt mặc định theo công năng", bật/tắt tự hạ cốt khi đổi công năng, ngưỡng bậc sàn.
//  • Mặt bằng: ô hạ cốt tô gạch chéo xanh + viền nét đứt, ghi "▽ -0.050 (-50)"; chú thích góc dưới trái.
// Thanh công cụ thêm bằng mã (không sửa MainWindow.xaml) – gọi InitSlabDropUi() trong hàm khởi tạo.
// =====================================================================
public partial class MainWindow
{
    private TextBox _txtDropMm;
    private static readonly Brush DropStroke = Frozen(Color.FromRgb(25, 95, 185));
    private static readonly Brush DropHatchBrush = MakeDropHatch();

    private const string SlabDropHint = "Hạ cốt sàn: bấm vào ô sàn để gán độ hạ ở ô 'Hạ cốt (mm)' (vd. −50 sàn vệ sinh, ban công); bấm lại ô đã hạ đúng giá trị đó để bỏ hạ cốt. "
                                        + "'Hạ cốt sàn…' để gán cho nhiều ô và sửa hạ cốt mặc định theo công năng.";

    private static Brush MakeDropHatch()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(150, 25, 95, 185)), 0.7);
        var g = new GeometryDrawing(null, pen, Geometry.Parse("M0,6 L6,0 M-1.5,1.5 L1.5,-1.5 M4.5,7.5 L7.5,4.5"));
        var b = new DrawingBrush(g)
        {
            TileMode = TileMode.Tile, Stretch = Stretch.None,
            Viewport = new Rect(0, 0, 6, 6), ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 6, 6), ViewboxUnits = BrushMappingMode.Absolute
        };
        b.Freeze();
        return b;
    }

    // ------------------------------------------------------------------
    // Thanh công cụ
    // ------------------------------------------------------------------
    private void InitSlabDropUi()
    {
        if (planEditBar == null) return;
        var kids = planEditBar.Children.OfType<UIElement>().ToList();
        int iWc = kids.FindIndex(e => e is RadioButton rb && rb.Tag as string == "WC");
        var rbDrop = new RadioButton
        {
            Content = "Hạ cốt sàn", Tag = nameof(PlanEditMode.SlabDropMode), GroupName = "PlanMode",
            ToolTip = "Bấm vào ô sàn để gán hạ cốt (ô 'Hạ cốt (mm)'); bấm lại để bỏ – sàn vệ sinh, ban công, lô gia, sân thượng…"
        };
        rbDrop.Checked += PlanMode_Checked;
        planEditBar.Children.Insert(iWc >= 0 ? iWc + 1 : Math.Min(1, planEditBar.Children.Count), rbDrop);

        kids = planEditBar.Children.OfType<UIElement>().ToList();
        int iRail = kids.FindIndex(e => e is TextBox tb && tb.Name == "txtRailing");
        int at = iRail >= 0 ? iRail + 1 : planEditBar.Children.Count;
        var lab = new TextBlock { Text = "Hạ cốt (mm):", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 2) };
        _txtDropMm = new TextBox
        {
            Text = "-50", Width = 40, Margin = new Thickness(0, 0, 3, 2), VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Độ hạ mặt sàn so với sàn tầng (mm, số âm = thấp hơn; −500 … 0) dùng cho chế độ 'Hạ cốt sàn'"
        };
        var btn = new Button
        {
            Content = "Hạ cốt sàn…", Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(0, 0, 6, 2),
            ToolTip = "Gán hạ cốt cho ô đang chọn / mọi ô cùng công năng; bảng hạ cốt mặc định theo công năng (WC −50, ban công −50…)"
        };
        btn.Click += (s, e) => SlabDropDialog();
        planEditBar.Children.Insert(at, btn);
        planEditBar.Children.Insert(at, _txtDropMm);
        planEditBar.Children.Insert(at, lab);
    }

    private double DropMmInput()
    {
        var s = (_txtDropMm?.Text ?? "").Trim().Replace(',', '.').Replace('−', '-');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : double.NaN;
    }

    /// <summary>Chế độ "Hạ cốt sàn": bấm ô → gán Δ ở ô nhập; ô đã đúng Δ → bỏ hạ cốt. Gọi trong PlanClick (đã chụp hoàn tác).</summary>
    private EditResult SlabDropClick(Slab slab)
    {
        double mm = DropMmInput();
        if (!double.IsFinite(mm)) return EditResult.Fail("Nhập độ hạ cốt (mm) ở ô 'Hạ cốt (mm)', vd. −50.");
        if (mm > 0) mm = -mm;   // nhập 50 hiểu là hạ 50 mm
        double off = mm / 1000.0;
        if (SlabDrop.IsLowered(slab) && Math.Abs(slab.LevelOffset - off) < 1e-6) off = 0;
        return SlabDrop.SetDrop(_p, slab, off, off == 0 ? null : (string.IsNullOrEmpty(slab.DropReason) ? ModelEdit.Loads(ModelEdit.UseOf(_p, slab)).Name : null));
    }

    // ------------------------------------------------------------------
    // Mặt bằng
    // ------------------------------------------------------------------
    /// <summary>Tô ô hạ cốt (gạch chéo + viền nét đứt) và ghi cao độ (gọi trong vòng vẽ sàn của RedrawPlan).</summary>
    private void DrawSlabDropPlan(Slab s, PointCollection pts, Point c)
    {
        if (!SlabDrop.IsLowered(s) || pts == null || pts.Count < 3) return;
        planCanvas.Children.Add(new Shapes.Polygon
        {
            Points = pts, Fill = DropHatchBrush, Stroke = DropStroke, StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 3 }, IsHitTestVisible = false
        });
        AddText($"▽ {SlabDrop.LevelText(s.LevelOffset)} ({SlabDrop.MmText(s.LevelOffset)})", new Point(c.X, c.Y + 20), DropStroke, 10, true);
    }

    /// <summary>Chú thích ô hạ cốt ở góc dưới trái mặt bằng (khi tầng đang xem có ô hạ cốt).</summary>
    private void DrawSlabDropLegend()
    {
        var low = PlanSlabs().Where(SlabDrop.IsLowered).ToList();
        if (low.Count == 0) return;
        double h = planHost.ActualHeight;
        var r = new Shapes.Rectangle { Width = 18, Height = 12, Fill = DropHatchBrush, Stroke = DropStroke, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 3, 2 }, IsHitTestVisible = false };
        Canvas.SetLeft(r, 10); Canvas.SetTop(r, h - 24);
        planCanvas.Children.Add(r);
        var vals = string.Join(", ", low.Select(x => SlabDrop.MmText(x.LevelOffset)).Distinct());
        AddText($"Sàn hạ cốt ({low.Count} ô, Δ = {vals} mm): ▽ cao độ mặt sàn so với sàn tầng", new Point(34, h - 25), DropStroke, 11, false);
    }

    /// <summary>Dòng thông tin hạ cốt trong khung thông tin ô sàn.</summary>
    private string SlabDropInfo(Slab s)
    {
        if (!SlabDrop.IsLowered(s)) return "Hạ cốt: không (mặt sàn bằng sàn tầng)" + Environment.NewLine;
        return $"Hạ cốt: Δ = {SlabDrop.MmText(s.LevelOffset)} mm (▽ {SlabDrop.LevelText(s.LevelOffset)} so với sàn tầng)"
               + (string.IsNullOrEmpty(s.DropReason) ? "" : " – " + s.DropReason) + Environment.NewLine;
    }

    // ------------------------------------------------------------------
    // Hộp thoại
    // ------------------------------------------------------------------
    private sealed class DropDefaultRow : RowBase
    {
        public SlabUse Use { get; init; }
        public string Name { get; init; } = "";
        private double _mm;
        public double Mm { get => _mm; set { _mm = value; Raise(); } }
        private string _note = "";
        public string Note { get => _note; set { _note = value ?? ""; Raise(); } }
    }

    private void SlabDropDialog()
    {
        if (_p == null) return;
        if (_busy) { SetStatus("Chương trình đang tính toán – chờ xong rồi sửa mô hình."); return; }
        var cur = SlabDropSettings.Of(_p);
        string st = CurrentStory;
        var sel = _selSlab != null && _p.Slabs.Contains(_selSlab) ? _selSlab : null;
        var selUse = sel != null ? ModelEdit.UseOf(_p, sel) : SlabUse.WC;

        var w = new Window { Owner = this };
        DialogUi.Setup(w, "Hạ cốt sàn (sàn vệ sinh, ban công, lô gia…)", 760, 600);
        var root = new DockPanel { Margin = new Thickness(10) };

        // ---- gán cho ô ----
        var top = new StackPanel();
        top.Children.Add(DialogUi.Label("Gán hạ cốt", double.NaN, true));
        var cbScope = new ComboBox { Width = 430, Margin = new Thickness(0, 0, 6, 0) };
        string useName = ModelEdit.Loads(selUse).Name;
        cbScope.Items.Add(sel != null ? $"Ô đang chọn {sel.Label} (tầng {sel.Story})" : "Ô đang chọn (chưa chọn – bấm ô sàn ở chế độ Chọn)");
        cbScope.Items.Add($"Mọi ô công năng \"{useName}\" – " + (st == AllStories ? "mọi tầng" : "tầng " + st));
        cbScope.Items.Add($"Mọi ô công năng \"{useName}\" – mọi tầng");
        cbScope.Items.Add("Mọi ô đang hạ cốt – " + (st == AllStories ? "mọi tầng" : "tầng " + st));
        cbScope.SelectedIndex = sel != null ? 0 : 1;
        top.Children.Add(DialogUi.Row(DialogUi.Label("Áp dụng cho:", 110), cbScope));
        double initMm = sel != null && SlabDrop.IsLowered(sel) ? Math.Round(sel.LevelOffset * 1000) : Math.Round(cur.For(selUse) * 1000);
        if (initMm == 0) initMm = -50;
        var tbMm = new TextBox { Text = initMm.ToString("0", CultureInfo.InvariantCulture), Width = 70, Margin = new Thickness(0, 0, 6, 0) };
        var tbReason = new TextBox { Text = sel?.DropReason ?? "", Width = 320 };
        top.Children.Add(DialogUi.Row(DialogUi.Label("Hạ cốt Δ (mm):", 110), tbMm, DialogUi.Label("0 = bỏ hạ cốt; −500 … 0"), DialogUi.Label("")));
        top.Children.Add(DialogUi.Row(DialogUi.Label("Ghi chú:", 110), tbReason));
        var chkAssign = new CheckBox { Content = "Gán hạ cốt cho các ô trên khi bấm Đồng ý", IsChecked = true, Margin = new Thickness(0, 4, 0, 6) };
        top.Children.Add(chkAssign);

        // ---- thiết lập ----
        top.Children.Add(DialogUi.Label("Hạ cốt mặc định theo công năng", double.NaN, true));
        top.Children.Add(DialogUi.Hint("Áp dụng khi đổi công năng ô sàn (nút 'Sàn vệ sinh', thêm ô sàn, ban công) – ô đã chỉnh tay giữ nguyên; mở dự án cũ không tự đổi. "
                                       + "Thực hành phổ biến: WC −50 (WC hạ sâu đặt bệt âm / ống thoát âm sàn −100 … −300), ban công / lô gia −50, mái / sân thượng tạo dốc bằng lớp vữa – 0."));
        var chkApply = new CheckBox { Content = "Tự đặt hạ cốt mặc định khi đổi công năng", IsChecked = cur.ApplyOnUse, Margin = new Thickness(0, 2, 0, 2) };
        var tbThr = new TextBox { Text = Math.Round(cur.StepThreshold * 1000).ToString("0", CultureInfo.InvariantCulture), Width = 50, Margin = new Thickness(0, 0, 6, 0) };
        top.Children.Add(chkApply);
        top.Children.Add(DialogUi.Row(DialogUi.Label("Ngưỡng bậc sàn (mm):", 140), tbThr,
            DialogUi.Label("hai ô chênh cốt lớn hơn → cạnh chung tính khớp, thép mũ tách riêng từng ô, kiểm tra dầm h ≥ hs1 + Δ + hs2")));
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        var rows = new ObservableCollection<DropDefaultRow>();
        foreach (SlabUse u in Enum.GetValues(typeof(SlabUse)))
        {
            var r0 = cur.Rows?.FirstOrDefault(x => x.Use == u);
            rows.Add(new DropDefaultRow { Use = u, Name = ModelEdit.Loads(u).Name, Mm = Math.Round((r0?.Offset ?? 0) * 1000), Note = r0?.Note ?? "" });
        }
        var grid = DialogUi.NewGrid();
        grid.Columns.Add(DialogUi.Text("Công năng", nameof(DropDefaultRow.Name), true, 220));
        grid.Columns.Add(DialogUi.Num("Hạ cốt (mm)", nameof(DropDefaultRow.Mm), "0", false, 90));
        grid.Columns.Add(DialogUi.Text("Ghi chú", nameof(DropDefaultRow.Note), false, 380));
        grid.ItemsSource = rows;

        string result = null;
        var ok = DialogUi.Btn("Đồng ý", (s, e) =>
        {
            DialogUi.Commit(grid);
            double mm = DialogUi.ParseNum(tbMm.Text.Replace('−', '-'), double.NaN);
            if (chkAssign.IsChecked == true && (!double.IsFinite(mm) || mm < -500 || mm > 500))
            { MessageBox.Show(w, "Hạ cốt Δ phải từ −500 đến 0 mm.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            double thr = DialogUi.ParseNum(tbThr.Text, cur.StepThreshold * 1000);
            if (thr < 0 || thr > 300) { MessageBox.Show(w, "Ngưỡng bậc sàn từ 0 đến 300 mm.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (rows.Any(x => x.Mm > 0 || x.Mm < -500)) { MessageBox.Show(w, "Hạ cốt mặc định phải từ −500 đến 0 mm.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            string snap = Snapshot();
            var msgs = new List<string>();
            // thiết lập: chỉ ghi vào dự án khi khác hiện tại
            var ns = new SlabDropSettings
            {
                ApplyOnUse = chkApply.IsChecked == true, StepThreshold = thr / 1000.0,
                Rows = rows.Select(x => new SlabDropDefault { Use = x.Use, Offset = x.Mm / 1000.0, Note = x.Note }).ToList()
            };
            bool setChanged = JsonSerializer.Serialize(ns, Project.JsonOptions) != JsonSerializer.Serialize(cur, Project.JsonOptions);
            if (setChanged) { _p.SlabDrop = ns; msgs.Add("Đã lưu hạ cốt mặc định theo công năng."); }
            Slab first = null;
            if (chkAssign.IsChecked == true)
            {
                var targets = DropTargets(cbScope.SelectedIndex, sel, selUse, st);
                if (targets.Count == 0 && !setChanged) { MessageBox.Show(w, "Không có ô sàn nào thuộc phạm vi đã chọn.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return; }
                double off = -Math.Abs(mm) / 1000.0;
                int n = 0;
                foreach (var sl in targets)
                {
                    var r = SlabDrop.SetDrop(_p, sl, off, tbReason.Text ?? "");
                    if (r.Ok) { n++; first ??= sl; }
                }
                if (n > 0) msgs.Add(off == 0 ? $"Bỏ hạ cốt {n} ô sàn." : $"Hạ cốt Δ = {SlabDrop.MmText(off)} mm cho {n} ô sàn.");
            }
            if (msgs.Count == 0) { w.DialogResult = false; return; }
            result = string.Join(" ", msgs);
            AfterEdit(snap, new EditResult { Ok = true, Message = result + " Chạy lại thiết kế để cập nhật sàn, dầm biên và bản vẽ." }, first ?? sel);
            w.DialogResult = true;
        }, true);
        ok.IsDefault = true;
        var cancel = DialogUi.Btn("Huỷ", (s, e) => w.DialogResult = false);
        cancel.IsCancel = true;
        var bs = DialogUi.Row(ok, cancel);
        bs.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(bs, Dock.Bottom);
        root.Children.Add(bs);
        root.Children.Add(grid);
        w.Content = root;
        w.ShowDialog();
    }

    /// <summary>Ô sàn thuộc phạm vi gán: 0 ô chọn; 1 cùng công năng tầng đang xem; 2 cùng công năng mọi tầng; 3 ô đang hạ cốt tầng đang xem.</summary>
    private List<Slab> DropTargets(int scope, Slab sel, SlabUse use, string story)
    {
        IEnumerable<Slab> inStory = story == AllStories ? _p.Slabs : _p.Slabs.Where(s => s.Story == story);
        return scope switch
        {
            0 => sel != null ? new List<Slab> { sel } : new List<Slab>(),
            1 => inStory.Where(s => ModelEdit.UseOf(_p, s) == use).ToList(),
            2 => _p.Slabs.Where(s => ModelEdit.UseOf(_p, s) == use).ToList(),
            _ => inStory.Where(SlabDrop.IsLowered).ToList(),
        };
    }
}

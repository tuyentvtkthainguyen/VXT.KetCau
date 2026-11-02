using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>[CừTràm] + [LùiCọcRanh] Tab "Mô hình móng" (tiếp): nút "Cừ tràm…" (thêm vào thanh công cụ khi vẽ mặt bằng móng lần đầu) mở hộp thoại cừ tràm
/// không chặn; áp dụng có hoàn tác (Snapshot/PushUndo) → thiết kế lại móng → vẽ lại. Trên mặt bằng móng: đường giới hạn tim cọc (lùi ép cọc) song song ranh giới
/// và ghi chú móng gia cố cừ tràm.</summary>
public partial class MainWindow
{
    private bool _cuUiDone;
    private CuTramDialog _cuDlg;
    private static readonly Brush SetbackBrush = Frozen(Color.FromRgb(230, 120, 0));
    private static readonly Brush CuTramBrush = Frozen(Color.FromRgb(120, 80, 30));

    /// <summary>Thêm nút "Cừ tràm…" vào thanh công cụ tab móng (một lần).</summary>
    private void EnsureCuTramUi()
    {
        if (_cuUiDone || cbFndType?.Parent is not Panel bar) return;
        _cuUiDone = true;
        int at = bar.Children.IndexOf(cbFndNodeType);
        if (at > 0 && bar.Children[at - 1] is TextBlock) at--;
        if (at < 0) at = bar.Children.Count;
        var b = new Button { Content = "Cừ tràm…", ToolTip = "Gia cố nền bằng cừ tràm dưới móng đơn / móng băng: thông số, hai phương pháp tính, kiểm tra độ bền (MNN), lún – cả công trình hoặc chân cột đang chọn" };
        if (TryFindResource("SmallButton") is Style st) b.Style = st;
        b.Click += BtnCuTram_Click;
        bar.Children.Insert(at, b);
    }

    private void BtnCuTram_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null || _busy) return;
        try { OpenCuTramDialog(SelectedBaseNode()); }
        catch (Exception ex) { ShowError("Lỗi hộp thoại cừ tràm", ex); }
    }

    /// <summary>Mở (không chặn) hộp thoại cừ tràm cho chân cột node (0 = cả công trình).</summary>
    private CuTramDialog OpenCuTramDialog(int node)
    {
        try { _cuDlg?.Close(); } catch (InvalidOperationException) { }
        var dlg = new CuTramDialog(_p, () => _design, node, ApplyCuTram) { Owner = this };
        dlg.Closed += (s, e) => { if (ReferenceEquals(_cuDlg, dlg)) _cuDlg = null; };
        _cuDlg = dlg;
        dlg.Show();
        return dlg;
    }

    /// <summary>Ghi thông số cừ tràm (+ lựa chọn riêng chân cột) có hoàn tác, thiết kế lại móng, vẽ lại.</summary>
    private async Task ApplyCuTram(CuTramSettings cs, int node, bool? nodeChoice)
    {
        if (_p == null || _busy || cs == null) return;
        string snap = Snapshot();
        _p.Foundation.CuTram = cs;
        if (node != 0) CuTramCalc.Set(_p.Foundation, new[] { node }, nodeChoice);
        PushUndo(snap);
        MarkDirty(false);
        if (!await DesignFoundation()) { SetStatus("Đã lưu thông số cừ tràm – cần thiết kế lại móng."); return; }
        RedrawFoundationPlan();
        int nf = (_design?.Footings.Count(f => f.CuTram != null) ?? 0) + (_design?.StripFootings.Count(f => f.CuTram != null) ?? 0);
        int bad = (_design?.Footings.Count(f => f.CuTram != null && (!f.Ok || f.CuTram.AnyFail)) ?? 0);
        SetStatus($"Cừ tràm: {nf} móng gia cố ({CuTramSettings.MethodName(cs.Method)}), {bad} móng không đạt (Hoàn tác để trở lại).");
    }

    /// <summary>Vẽ trên mặt bằng móng: đường giới hạn tim cọc (lùi ép cọc) + ghi chú móng cừ tràm (gọi sau DrawBoundariesAndStraps).</summary>
    private void DrawGroundOverlays()
    {
        if (_p == null) return;
        var lines = PileSetback.Lines(_p);
        if (lines.Count > 0)
        {
            double w = fndHost.ActualWidth, h = fndHost.ActualHeight;
            var tl = FS2W(new Point(0, 0)); var br = FS2W(new Point(w, h));
            foreach (var (b, c, sb) in lines)
            {
                double v = c + b.Inward * sb;
                string tag = PileSetbackDrawingTag(sb);
                if (b.IsVertical)
                {
                    CLine(fndCanvas, FW2S(v, br.Y), FW2S(v, tl.Y), SetbackBrush, 1.2, true);
                    var t = CText(fndCanvas, tag, FW2S(v + b.Inward * 12 / Math.Max(_fScale, 1e-6), tl.Y - (tl.Y - br.Y) * 0.25), SetbackBrush, 9.5, true, true, true);
                    t.RenderTransformOrigin = new Point(0.5, 0.5); t.RenderTransform = new RotateTransform(-90);
                }
                else
                {
                    CLine(fndCanvas, FW2S(tl.X, v), FW2S(br.X, v), SetbackBrush, 1.2, true);
                    CText(fndCanvas, tag, FW2S(tl.X + (br.X - tl.X) * 0.25, v + b.Inward * 12 / Math.Max(_fScale, 1e-6)), SetbackBrush, 9.5, true, true, true);
                }
            }
        }
        foreach (var f in _design?.Footings.Where(x => x.CuTram != null) ?? Enumerable.Empty<KetCauVXT.Design.FootingDesign>())
        {
            var locs = f.Eccentric?.Locations?.Count > 0 ? f.Eccentric.Locations.Select(l => (X: l.X + l.OffsetX, Y: l.Y + l.OffsetY)).ToList() : new List<(double X, double Y)> { (f.X, f.Y) };
            foreach (var (x, y) in locs)
            {
                // chấm lưới cừ (thưa, minh hoạ) + ghi chú
                int nx = Math.Clamp(f.CuTram.Nx, 1, 12), ny = Math.Clamp(f.CuTram.Ny, 1, 12);
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < ny; j++)
                    {
                        var c = FW2S(x - f.L / 2 + f.L * (i + 0.5) / nx, y - f.B / 2 + f.B * (j + 0.5) / ny);
                        var dot = new Shapes.Ellipse { Width = 2.4, Height = 2.4, Fill = CuTramBrush, IsHitTestVisible = false };
                        Canvas.SetLeft(dot, c.X - 1.2); Canvas.SetTop(dot, c.Y - 1.2);
                        fndCanvas.Children.Add(dot);
                    }
                var pb = FW2S(x, y - f.B / 2);
                CText(fndCanvas, $"cừ tràm {f.CuTram.Count} cây" + (f.CuTram.DurabilityOk ? "" : " – TRÊN MNN!"), new Point(pb.X, pb.Y + 20), f.CuTram.DurabilityOk ? CuTramBrush : FootBadStroke, 8.5, true, true, true);
            }
        }
    }

    private static string PileSetbackDrawingTag(double s) => "≥ " + (s * 1000).ToString("0", Inv) + " (lùi ép cọc)";
}

/// <summary>[LùiCọcRanh] Vẽ ranh giới + đường giới hạn tim cọc trên hình phác của hộp thoại "Bố trí cọc…" (toạ độ so với tim cột).</summary>
internal static class PileSetbackUi
{
    private static readonly Brush Red = Fr(210, 30, 30), Orange = Fr(230, 120, 0);
    private static Brush Fr(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }

    public static void DrawOnPlan(Canvas cv, PileCapDesign t, Func<double, double, Point> S)
    {
        var info = t?.Setback;
        if (cv == null || info == null || info.Sides.Count == 0) return;
        double ext = Math.Max(t.L, t.B);
        void Ln(Point a, Point b, Brush br, double th, DoubleCollection dash)
        {
            var l = new Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = br, StrokeThickness = th, StrokeDashArray = dash, IsHitTestVisible = false };
            cv.Children.Add(l);
        }
        void Tx(string s, Point at, Brush br)
        {
            var tb = new TextBlock { Text = s, FontSize = 9.5, Foreground = br, IsHitTestVisible = false };
            Canvas.SetLeft(tb, at.X + 2); Canvas.SetTop(tb, at.Y + 2);
            cv.Children.Add(tb);
        }
        foreach (var sd in info.Sides)
        {
            if (sd.IsVertical)
            {
                double xb = sd.Boundary - t.X, xl = xb + sd.Inward * (sd.Setback + (info.ToFace ? t.PileSize / 2 : 0));
                Ln(S(xb, -ext), S(xb, ext), Red, 1.6, new DoubleCollection { 5, 3 });
                Ln(S(xl, -ext), S(xl, ext), Orange, 1.1, new DoubleCollection { 8, 3, 2, 3 });
                Tx("Ranh giới", S(xb, ext * 0.9), Red);
                Tx($"≥ {sd.Setback * 1000:0} (lùi ép cọc)", S(xl, ext * 0.75), Orange);
            }
            else
            {
                double yb = sd.Boundary - t.Y, yl = yb + sd.Inward * (sd.Setback + (info.ToFace ? t.PileSize / 2 : 0));
                Ln(S(-ext, yb), S(ext, yb), Red, 1.6, new DoubleCollection { 5, 3 });
                Ln(S(-ext, yl), S(ext, yl), Orange, 1.1, new DoubleCollection { 8, 3, 2, 3 });
                Tx("Ranh giới", S(-ext * 0.9, yb), Red);
                Tx($"≥ {sd.Setback * 1000:0} (lùi ép cọc)", S(-ext * 0.75, yl), Orange);
            }
        }
    }
}

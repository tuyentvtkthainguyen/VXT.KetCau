using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>[MóngLệchTâm] Tab "Mô hình móng" (tiếp): nút "Ranh giới đất…" và "Lệch tâm…" (thêm vào thanh công cụ khi vẽ mặt bằng móng lần đầu),
/// vẽ ranh giới đất (nét đứt đỏ, gạch chéo phía ngoài, chữ "RANH GIỚI ĐẤT"), móng lệch tâm / móng kết hợp, dầm móng cân bằng (DM b×h);
/// áp dụng có hoàn tác (Snapshot/PushUndo) → thiết kế lại móng → vẽ lại.</summary>
public partial class MainWindow
{
    private bool _eccUiDone;
    private EccentricFootingDialog _eccDlg;
    private static readonly Brush BoundaryBrush = Frozen(Color.FromRgb(210, 30, 30));
    private static readonly Brush StrapFill = Frozen(Color.FromArgb(70, 70, 120, 200));
    private static readonly Brush StrapStroke = Frozen(Color.FromRgb(40, 80, 170));

    /// <summary>Thêm 2 nút vào thanh công cụ tab móng (một lần).</summary>
    private void EnsureEccentricUi()
    {
        if (_eccUiDone || cbFndType?.Parent is not Panel bar) return;
        _eccUiDone = true;
        int at = bar.Children.IndexOf(cbFndNodeType);
        if (at > 0 && bar.Children[at - 1] is TextBlock) at--;
        if (at < 0) at = bar.Children.Count;
        var b1 = new Button { Content = "Ranh giới đất…", ToolTip = "Khai báo ranh giới đất (trái / phải / trước / sau theo trục) – móng sát ranh tự lệch tâm, không vượt ranh giới" };
        var b2 = new Button { Content = "Lệch tâm…", ToolTip = "Phương án móng lệch tâm cho chân cột đang chọn: (a) dầm móng cân bằng, (b) tự cân bằng, (c) móng kết hợp; độ lệch tâm tự động / nhập tay" };
        foreach (var b in new[] { b1, b2 })
        {
            if (TryFindResource("SmallButton") is Style st) b.Style = st;
        }
        b1.Click += BtnBoundary_Click;
        b2.Click += BtnEccentric_Click;
        bar.Children.Insert(at, b2);
        bar.Children.Insert(at, b1);
    }

    private async void BtnBoundary_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null || _busy) return;
        var dlg = new BoundaryDialog(_p) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result == null) return;
        await ApplyBoundaries(dlg.Result);
    }

    /// <summary>Ghi thông số ranh giới (có hoàn tác) rồi thiết kế lại móng.</summary>
    private async Task ApplyBoundaries(FoundationSettings fs)
    {
        if (_p == null || _busy || fs == null) return;
        string snap = Snapshot();
        _p.Foundation = fs;
        PushUndo(snap);
        MarkDirty(false);
        int nb = EccentricFooting.Lines(_p).Count;
        if (!await DesignFoundation()) { SetStatus($"Đã lưu {nb} cạnh ranh giới đất – cần thiết kế lại móng."); return; }
        RedrawFoundationPlan();
        int ne = (_design?.Footings.Count(f => f.Eccentric != null) ?? 0) + (_design?.PileCaps.Count(c => c.Eccentric != null) ?? 0);
        SetStatus($"Ranh giới đất: {nb} cạnh – {ne} móng lệch tâm, {_design?.StrapBeams?.Count ?? 0} dầm móng cân bằng (Hoàn tác để trở lại).");
    }

    private void BtnEccentric_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null || _busy) return;
        int node = SelectedBaseNode();
        if (node == 0)
            node = _design?.Footings.FirstOrDefault(f => f.Eccentric != null)?.NodeId ?? _design?.PileCaps.FirstOrDefault(c => c.Eccentric != null)?.NodeIds.FirstOrDefault() ?? 0;
        if (node == 0) { SetStatus("Chọn một móng / chân cột trên mặt bằng móng trước (hoặc khai báo 'Ranh giới đất…')."); return; }
        try { OpenEccentricDialog(node); }
        catch (Exception ex) { ShowError("Lỗi hộp thoại móng lệch tâm", ex); }
    }

    /// <summary>Mở (không chặn) hộp thoại lệch tâm cho chân cột node.</summary>
    private EccentricFootingDialog OpenEccentricDialog(int node)
    {
        try { _eccDlg?.Close(); } catch (InvalidOperationException) { }
        var dlg = new EccentricFootingDialog(_p, () => _res, node, ApplyEccentric) { Owner = this };
        dlg.Closed += (s, e) => { if (ReferenceEquals(_eccDlg, dlg)) _eccDlg = null; };
        _eccDlg = dlg;
        dlg.Show();
        return dlg;
    }

    /// <summary>Ghi lựa chọn lệch tâm (null = tự động) cho chân cột, có hoàn tác, rồi thiết kế lại móng và vẽ lại.</summary>
    private async Task ApplyEccentric(int node, EccentricOverride o)
    {
        if (_p == null || _busy) return;
        string snap = Snapshot();
        EccentricFooting.Set(_p.Foundation, new[] { node }, o);
        PushUndo(snap);
        MarkDirty(false);
        string what = o == null ? "tự động" : EccentricOverride.SchemeName(o.Scheme);
        if (!await DesignFoundation()) { SetStatus($"Đã lưu lựa chọn lệch tâm ({what}) – cần thiết kế lại móng."); return; }
        var fd = _design?.Footings.FirstOrDefault(f => EccHasNode(f, node));
        _fndSelItem = fd;
        Select(footingNode: node);
        RedrawFoundationPlan();
        SetStatus(fd == null ? $"Móng lệch tâm ({what}) đã áp dụng."
            : $"Móng {fd.Name} ({what}): {fd.L:0.00}×{fd.B:0.00}×{fd.H:0.00} m, pmax = {Math.Min(fd.Pmax, 99999):0} kN/m² – {(fd.Ok ? "ĐẠT" : "KHÔNG ĐẠT")} (Hoàn tác để trở lại).");
    }

    /// <summary>Móng đơn chứa nút (kể cả vị trí gộp và cột trong của móng kết hợp).</summary>
    private static bool EccHasNode(FootingDesign f, int node) =>
        f != null && (f.NodeId == node || f.RebarKeys.Contains("M:" + node) || (f.Eccentric != null && (f.Eccentric.Locations.Any(q => q.NodeId == node) || f.Eccentric.Combined?.NodeB == node)));

    /// <summary>Phạm vi vẽ (Vừa màn hình) của móng đơn kể cả độ lệch tâm.</summary>
    private static void EccExtents(FootingDesign f, List<double> xs, List<double> ys)
    {
        if (f.Eccentric?.Combined != null)
        {
            foreach (var q in f.Eccentric.Combined.Outline) { xs.Add(q.X); ys.Add(q.Y); }
            return;
        }
        foreach (var l in f.Eccentric?.Locations ?? new List<EccentricLocation>())
        {
            xs.Add(l.X + l.OffsetX - f.L / 2); xs.Add(l.X + l.OffsetX + f.L / 2);
            ys.Add(l.Y + l.OffsetY - f.B / 2); ys.Add(l.Y + l.OffsetY + f.B / 2);
        }
    }

    /// <summary>Vẽ móng lệch tâm (mọi vị trí gộp; móng kết hợp vẽ đa giác) + vùng bấm chọn. Trả về false nếu móng không lệch tâm (vẽ theo cách cũ).</summary>
    private bool DrawEccentricFooting(FootingDesign fd, bool sel, bool labels)
    {
        var e = fd.Eccentric;
        if (e == null) return false;
        bool ok = fd.Ok;
        Brush st = ok ? FootOkStroke : FootBadStroke;
        if (e.Combined != null)
        {
            var poly = new Shapes.Polygon { Fill = ok ? FootOkFill : FootBadFill, Stroke = st, StrokeThickness = sel ? 3 : 1.4, Effect = sel ? GlowEffect : null, IsHitTestVisible = false };
            foreach (var q in e.Combined.Outline) poly.Points.Add(FW2S(q.X, q.Y));
            fndCanvas.Children.Add(poly);
            double x0 = e.Combined.Outline.Min(q => q.X), x1 = e.Combined.Outline.Max(q => q.X), y0 = e.Combined.Outline.Min(q => q.Y), y1 = e.Combined.Outline.Max(q => q.Y);
            var p0 = FW2S(x0, y1); var p1 = FW2S(x1, y0);
            _fndHits.Add((new Rect(p0, p1), fd.NodeId, null, fd));
            if (labels || sel)
                CText(fndCanvas, $"{fd.Name} – móng kết hợp {fd.L.ToString("0.00", Inv)}×{fd.B.ToString("0.00", Inv)}", new Point((p0.X + p1.X) / 2, p0.Y - 9), st, sel ? 11 : 9.5, true, true, true);
            return true;
        }
        var locs = e.Locations.Count > 0 ? e.Locations : new List<EccentricLocation> { new() { NodeId = fd.NodeId, X = fd.X, Y = fd.Y, OffsetX = e.OffsetX, OffsetY = e.OffsetY } };
        foreach (var l in locs)
        {
            double cx = l.X + l.OffsetX, cy = l.Y + l.OffsetY;
            var p0 = FW2S(cx - fd.L / 2, cy + fd.B / 2); var p1 = FW2S(cx + fd.L / 2, cy - fd.B / 2);
            var r = new Shapes.Rectangle
            {
                Width = Math.Max(2, p1.X - p0.X), Height = Math.Max(2, p1.Y - p0.Y), Fill = ok ? FootOkFill : FootBadFill, Stroke = st,
                StrokeThickness = sel ? 3 : 1.4, Effect = sel ? GlowEffect : null, IsHitTestVisible = false
            };
            Canvas.SetLeft(r, p0.X); Canvas.SetTop(r, p0.Y);
            fndCanvas.Children.Add(r);
            _fndHits.Add((new Rect(p0, p1), l.NodeId, null, fd));
            if (labels || sel)
            {
                var t = CText(fndCanvas, $"{fd.Name}  {fd.L.ToString("0.00", Inv)}×{fd.B.ToString("0.00", Inv)}×{fd.H.ToString("0.00", Inv)} – lệch tâm", new Point((p0.X + p1.X) / 2, p0.Y - 9), st, sel ? 11 : 9.5, true, true, true);
                if (sel) t.FontWeight = FontWeights.Bold;
                string ex = Math.Abs(l.OffsetX) > 1e-6 ? $"ex = {l.OffsetX * 1000:0}" : "", ey = Math.Abs(l.OffsetY) > 1e-6 ? $"ey = {l.OffsetY * 1000:0}" : "";
                CText(fndCanvas, string.Join("; ", new[] { ex, ey }.Where(x => x.Length > 0)) + " mm", new Point((p0.X + p1.X) / 2, p1.Y + 9), Brushes.DimGray, 9, true, true, true);
            }
        }
        return true;
    }

    /// <summary>Ranh giới đất + dầm cân bằng trên mặt bằng móng (vẽ trên cùng).</summary>
    private void DrawBoundariesAndStraps()
    {
        if (_p == null) return;
        foreach (var sb in _design?.StrapBeams ?? new List<StrapBeamDesign>())
            foreach (var l in sb.Links)
            {
                double dir = l.AlongX ? Math.Sign(l.XB - l.XA) : Math.Sign(l.YB - l.YA);
                if (dir == 0) dir = 1;
                Point a, b;
                if (l.AlongX) { a = FW2S(l.XA + dir * l.FaceA, l.YA + sb.B / 2); b = FW2S(l.XA + dir * l.FaceB, l.YA - sb.B / 2); }
                else { a = FW2S(l.XA - sb.B / 2, l.YA + dir * l.FaceA); b = FW2S(l.XA + sb.B / 2, l.YA + dir * l.FaceB); }
                double x0 = Math.Min(a.X, b.X), y0 = Math.Min(a.Y, b.Y);
                var r = new Shapes.Rectangle
                {
                    Width = Math.Max(2, Math.Abs(b.X - a.X)), Height = Math.Max(2, Math.Abs(b.Y - a.Y)), Fill = StrapFill, Stroke = sb.Ok ? StrapStroke : FootBadStroke,
                    StrokeThickness = 1.2, IsHitTestVisible = false
                };
                Canvas.SetLeft(r, x0); Canvas.SetTop(r, y0);
                fndCanvas.Children.Add(r);
                var t = CText(fndCanvas, $"{sb.Name} {sb.B * 1000:0}×{sb.H * 1000:0}", new Point(x0 + Math.Abs(b.X - a.X) / 2, y0 + Math.Abs(b.Y - a.Y) / 2), StrapStroke, 9, true, true, true);
                if (!l.AlongX) { t.RenderTransformOrigin = new Point(0.5, 0.5); t.RenderTransform = new RotateTransform(-90); }
            }
        var lines = EccentricFooting.Lines(_p);
        if (lines.Count == 0) return;
        double w = fndHost.ActualWidth, h = fndHost.ActualHeight;
        var tl = FS2W(new Point(0, 0)); var br = FS2W(new Point(w, h));
        foreach (var (b, c) in lines)
        {
            int o = -b.Inward;
            double tk = 9 / Math.Max(_fScale, 1e-6);
            if (b.IsVertical)
            {
                CLine(fndCanvas, FW2S(c, br.Y), FW2S(c, tl.Y), BoundaryBrush, 1.8, true);
                for (double y = br.Y; y < tl.Y; y += 1.6 * tk) CLine(fndCanvas, FW2S(c, y), FW2S(c + o * tk, y + tk), BoundaryBrush, 0.7, false);
                var t = CText(fndCanvas, "RANH GIỚI ĐẤT", FW2S(c + o * 2.2 * tk, (tl.Y + br.Y) / 2), BoundaryBrush, 10, true, true, true);
                t.RenderTransformOrigin = new Point(0.5, 0.5); t.RenderTransform = new RotateTransform(-90);
            }
            else
            {
                CLine(fndCanvas, FW2S(tl.X, c), FW2S(br.X, c), BoundaryBrush, 1.8, true);
                for (double x = tl.X; x < br.X; x += 1.6 * tk) CLine(fndCanvas, FW2S(x, c), FW2S(x + tk, c + o * tk), BoundaryBrush, 0.7, false);
                CText(fndCanvas, "RANH GIỚI ĐẤT", FW2S((tl.X + br.X) / 2, c + o * 2.2 * tk), BoundaryBrush, 10, true, true, true);
            }
        }
    }
}

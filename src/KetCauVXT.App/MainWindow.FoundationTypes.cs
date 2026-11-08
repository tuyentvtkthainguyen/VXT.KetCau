using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Design;
using KetCauVXT.Model;
using PileCapDesign = KetCauVXT.Design.Foundation.PileCapDesign;
using StripFootingDesign = KetCauVXT.Design.Foundation.StripFootingDesign;
using ConcreteDesigner = KetCauVXT.Design.Concrete.ConcreteDesigner;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>Tab "Mô hình móng" (tiếp): đài cọc và móng băng trên mặt bằng + mặt cắt, chọn loại móng (cả công trình / từng chân cột),
/// hộp thoại "Thiết kế móng…" và nút "Thiết kế móng".</summary>
public partial class MainWindow
{
    /// <summary>Móng đang chọn trên mặt bằng móng (FootingDesign / PileCapDesign / StripFootingDesign) – phân biệt khi 1 nút thuộc nhiều dải.</summary>
    private object _fndSelItem;
    private bool _fndUiSync;

    private static readonly Brush PileFill = Frozen(Color.FromRgb(250, 250, 250));
    private static readonly Brush PileStroke = Frozen(Color.FromRgb(60, 60, 60));

    internal static string FoundationTypeName(FoundationType t) => t switch
    {
        FoundationType.Pile => "Móng cọc", FoundationType.Strip => "Móng băng", _ => "Móng đơn"
    };

    private static string PileText(bool round, double size) =>
        round ? $"D{size * 1000:0}" : $"{size * 1000:0}×{size * 1000:0}";

    private List<PileCapDesign> DesignCaps() => _design?.PileCaps?.Where(c => c.L > 0 && c.B > 0).ToList() ?? new List<PileCapDesign>();
    private List<StripFootingDesign> DesignStrips() => _design?.StripFootings?.Where(s => s.Width > 0 && s.End > s.Start).ToList() ?? new List<StripFootingDesign>();

    private void AddPileStripExtents(List<double> xs, List<double> ys)
    {
        foreach (var c in DesignCaps()) { xs.Add(c.X + c.OffsetX - c.L / 2); xs.Add(c.X + c.OffsetX + c.L / 2); ys.Add(c.Y + c.OffsetY - c.B / 2); ys.Add(c.Y + c.OffsetY + c.B / 2); }   // [MóngLệchTâm] tâm đài lệch
        foreach (var s in DesignStrips())
        {
            if (s.AlongX) { xs.Add(s.Start); xs.Add(s.End); ys.Add(s.C - s.Width / 2); ys.Add(s.C + s.Width / 2); }
            else { ys.Add(s.Start); ys.Add(s.End); xs.Add(s.C - s.Width / 2); xs.Add(s.C + s.Width / 2); }
        }
    }

    /// <summary>Nút chân cột đã có móng (đơn, đài cọc, móng băng) trong kết quả thiết kế.</summary>
    private HashSet<int> CoveredFoundationNodes()
    {
        var h = new HashSet<int>();
        if (_design == null) return h;
        foreach (var f in _design.Footings.Where(f => f.L > 0 && f.B > 0))
        {
            h.Add(f.NodeId);
            foreach (int nn in KetCauVXT.Design.Foundation.FootingLayout.AllNodes(_design, f)) h.Add(nn);   // móng gộp giống hệt
            if (f.Eccentric != null)   // [MóngLệchTâm] vị trí gộp + cột trong của móng kết hợp
            {
                foreach (var l in f.Eccentric.Locations) h.Add(l.NodeId);
                if (f.Eccentric.Combined != null) h.Add(f.Eccentric.Combined.NodeB);
            }
        }
        foreach (var c in DesignCaps()) foreach (int n in c.NodeIds) h.Add(n);
        foreach (var s in DesignStrips()) foreach (int n in s.NodeIds) h.Add(n);
        return h;
    }

    /// <summary>Loại móng áp dụng tại nút chân cột (riêng chân cột hoặc theo công trình).</summary>
    private FoundationType NodeFoundationType(int node) =>
        _p.Footings.FirstOrDefault(f => f.NodeId == node)?.Type ?? _p.Foundation.Type;

    /// <summary>Móng đang chọn (theo _selFooting); null nếu chưa chọn.</summary>
    private object SelectedFndItem()
    {
        if (_selFooting == 0 || _design == null) return null;
        var caps = DesignCaps(); var strips = DesignStrips();
        var foots = _design.Footings.Where(f => f.L > 0 && f.B > 0).ToList();
        switch (_fndSelItem)
        {
            case PileCapDesign pc when caps.Contains(pc) && pc.NodeIds.Contains(_selFooting): return pc;
            case StripFootingDesign sd when strips.Contains(sd) && sd.NodeIds.Contains(_selFooting): return sd;
            case FootingDesign fd when foots.Contains(fd) && EccHasNode(fd, _selFooting): return fd;
        }
        object f0 = foots.FirstOrDefault(f => EccHasNode(f, _selFooting));   // [MóngLệchTâm] kể vị trí gộp / móng kết hợp
        object c0 = caps.FirstOrDefault(c => c.NodeIds.Contains(_selFooting));
        object s0 = strips.FirstOrDefault(s => s.NodeIds.Contains(_selFooting));
        return NodeFoundationType(_selFooting) switch
        {
            FoundationType.Pile => c0 ?? s0 ?? f0,
            FoundationType.Strip => s0 ?? c0 ?? f0,
            _ => f0 ?? c0 ?? s0
        };
    }

    /// <summary>Móng thể hiện ở mặt cắt: móng đang chọn, hoặc móng đầu tiên (auto = true).</summary>
    private object SectionItem(out bool auto)
    {
        var sel = SelectedFndItem();
        auto = sel == null;
        if (sel != null) return sel;
        if (_design == null) return null;
        object f0 = _design.Footings.FirstOrDefault(f => f.L > 0 && f.B > 0);
        object c0 = DesignCaps().FirstOrDefault();
        object s0 = DesignStrips().FirstOrDefault();
        return _p.Foundation.Type switch
        {
            FoundationType.Pile => c0 ?? s0 ?? f0,
            FoundationType.Strip => s0 ?? c0 ?? f0,
            _ => f0 ?? c0 ?? s0
        };
    }

    // ------------------------------------------------------------------
    // Mặt bằng: móng băng + đài cọc
    // ------------------------------------------------------------------
    /// <summary>Vẽ móng băng và đài cọc, thêm vùng bấm chọn. Trả về số móng đã vẽ; bad = số không đạt.</summary>
    private int DrawStripsAndPileCaps(bool labels, out int bad)
    {
        bad = 0;
        int count = 0;
        var selItem = SelectedFndItem();

        foreach (var sf in DesignStrips())
        {
            count++;
            bool ok = sf.Ok, sel = ReferenceEquals(selItem, sf);
            if (!ok) bad++;
            Brush st = ok ? FootOkStroke : FootBadStroke;
            double x0, x1, y0, y1;
            if (sf.AlongX) { x0 = sf.Start; x1 = sf.End; y0 = sf.C - sf.Width / 2; y1 = sf.C + sf.Width / 2; }
            else { x0 = sf.C - sf.Width / 2; x1 = sf.C + sf.Width / 2; y0 = sf.Start; y1 = sf.End; }
            var p0 = FW2S(x0, y1); var p1 = FW2S(x1, y0);
            var r = new Shapes.Rectangle
            {
                Width = Math.Max(2, p1.X - p0.X), Height = Math.Max(2, p1.Y - p0.Y), Fill = ok ? FootOkFill : FootBadFill, Stroke = st,
                StrokeThickness = sel ? 3 : 1.4, Effect = sel ? GlowEffect : null, IsHitTestVisible = false
            };
            Canvas.SetLeft(r, p0.X); Canvas.SetTop(r, p0.Y);
            fndCanvas.Children.Add(r);
            // sườn (nét đứt)
            double hb = (sf.RibB > 0 ? sf.RibB : 0.3) / 2;
            double rc = sf.FlangeSide != 0 ? sf.RibC : sf.C;   // [MóngLệchTâm] sườn sát ranh giới (bản cánh một phía)
            if (sf.AlongX)
            {
                CLine(fndCanvas, FW2S(x0, rc + hb), FW2S(x1, rc + hb), st, 0.8, true);
                CLine(fndCanvas, FW2S(x0, rc - hb), FW2S(x1, rc - hb), st, 0.8, true);
            }
            else
            {
                CLine(fndCanvas, FW2S(rc + hb, y0), FW2S(rc + hb, y1), st, 0.8, true);
                CLine(fndCanvas, FW2S(rc - hb, y0), FW2S(rc - hb, y1), st, 0.8, true);
            }
            _fndHits.Add((new Rect(p0, p1), sf.NodeIds.FirstOrDefault(), null, sf));
            if (labels || sel)
            {
                string name = string.IsNullOrEmpty(sf.Name) ? "MB" : sf.Name;
                string txt = $"{name}  B={sf.Width.ToString("0.00", Inv)}  sườn {(sf.RibB * 1000).ToString("0", Inv)}×{(sf.RibH * 1000).ToString("0", Inv)}";
                Point at = sf.AlongX ? new Point((p0.X + p1.X) / 2, p0.Y - 9) : new Point((p0.X + p1.X) / 2, p0.Y - 9);
                var t = CText(fndCanvas, txt, at, st, sel ? 11 : 9.5, true, true, true);
                if (sel) t.FontWeight = FontWeights.Bold;
                if (sel) CText(fndCanvas, $"Pmax = {sf.PMax.ToString("0.0", Inv)} kN/m²", new Point((p0.X + p1.X) / 2, p1.Y + 9), Brushes.DimGray, 9, true, true, true);
            }
        }

        foreach (var pc in DesignCaps())
        {
            count++;
            bool ok = pc.Ok, sel = ReferenceEquals(selItem, pc);
            if (!ok) bad++;
            Brush st = ok ? FootOkStroke : FootBadStroke;
            var p0 = FW2S(pc.X + pc.OffsetX - pc.L / 2, pc.Y + pc.OffsetY + pc.B / 2); var p1 = FW2S(pc.X + pc.OffsetX + pc.L / 2, pc.Y + pc.OffsetY - pc.B / 2);   // [MóngLệchTâm] tâm đài lệch
            var r = new Shapes.Rectangle
            {
                Width = Math.Max(2, p1.X - p0.X), Height = Math.Max(2, p1.Y - p0.Y), Fill = ok ? FootOkFill : FootBadFill, Stroke = st,
                StrokeThickness = sel ? 3 : 1.4, Effect = sel ? GlowEffect : null, IsHitTestVisible = false
            };
            Canvas.SetLeft(r, p0.X); Canvas.SetTop(r, p0.Y);
            fndCanvas.Children.Add(r);
            double ps = pc.PileSize > 0 ? pc.PileSize : _p.Foundation.PileSize;
            double d = Math.Max(4, ps * _fScale);
            foreach (var (px, py) in pc.Piles)
            {
                var c = FW2S(pc.X + px, pc.Y + py);
                Shapes.Shape sh = pc.PileRound
                    ? new Shapes.Ellipse { Width = d, Height = d }
                    : new Shapes.Rectangle { Width = d, Height = d };
                sh.Fill = PileFill; sh.Stroke = PileStroke; sh.StrokeThickness = 1; sh.IsHitTestVisible = false;
                Canvas.SetLeft(sh, c.X - d / 2); Canvas.SetTop(sh, c.Y - d / 2);
                fndCanvas.Children.Add(sh);
                double k = d * 0.35;
                CLine(fndCanvas, new Point(c.X - k, c.Y - k), new Point(c.X + k, c.Y + k), PileStroke, 0.6, false);
                CLine(fndCanvas, new Point(c.X - k, c.Y + k), new Point(c.X + k, c.Y - k), PileStroke, 0.6, false);
            }
            _fndHits.Add((new Rect(p0, p1), pc.NodeIds.FirstOrDefault(), null, pc));
            if (labels || sel)
            {
                string name = string.IsNullOrEmpty(pc.Name) ? "ĐC" : pc.Name;
                var t = CText(fndCanvas, $"{name} – {pc.Piles.Count} cọc, Pmax={pc.PMax.ToString("0", Inv)} kN", new Point((p0.X + p1.X) / 2, p0.Y - 9), st, sel ? 11 : 9.5, true, true, true);
                if (sel) t.FontWeight = FontWeights.Bold;
                CText(fndCanvas, $"{pc.L.ToString("0.00", Inv)}×{pc.B.ToString("0.00", Inv)}×{pc.H.ToString("0.00", Inv)}", new Point((p0.X + p1.X) / 2, p1.Y + 9), Brushes.DimGray, 9, true, true, true);
            }
        }
        return count;
    }

    // ------------------------------------------------------------------
    // Mặt cắt đài cọc (theo phương X)
    // ------------------------------------------------------------------
    private void DrawPileCapSection(PileCapDesign pc, bool auto, double w, double h)
    {
        var nodes = NodeMap();
        double zN = pc.NodeIds.Count > 0 && nodes.TryGetValue(pc.NodeIds[0], out var nd) ? nd.Z : _p.BaseElevation;
        double L = pc.L, H = pc.H > 0 ? pc.H : 0.6;
        double zb = pc.Depth > 0 ? -pc.Depth : zN - H;
        if (zb + H > zN) zb = zN - H;
        double zTop = zb + H;
        double cx = pc.ColumnH > 0 ? pc.ColumnH : 0.22;
        double colTop = Math.Max(zN, zTop) + 0.5;
        double ps = pc.PileSize > 0 ? pc.PileSize : _p.Foundation.PileSize;
        double emb = Math.Min(_p.Foundation.PileEmbed, H * 0.5);
        double show = Math.Clamp(L * 0.8, 0.9, 1.6);   // đoạn cọc thể hiện
        double zPileBot = zb - show;

        double wx0 = -L / 2 - 0.9 - Math.Abs(pc.OffsetX), wx1 = L / 2 + 1.4 + Math.Abs(pc.OffsetX), wz0 = zPileBot - 0.6, wz1 = Math.Max(colTop, 0.25) + 0.3;   // [MóngLệchTâm]
        double top = 44, bottom = 92;
        double s = Math.Min((w - 20) / (wx1 - wx0), (h - top - bottom) / (wz1 - wz0));
        if (s <= 0) return;
        double ox = 10 + ((w - 20) - (wx1 - wx0) * s) / 2 - wx0 * s;
        double oz = top + ((h - top - bottom) - (wz1 - wz0) * s) / 2 + wz1 * s;
        Point S(double x, double z) => new(ox + x * s, oz - z * s);

        DrawGround(S, wx0, wx1, L);

        double ocx = pc.OffsetX;   // [MóngLệchTâm] tâm đài cách tim cột (gốc toạ độ mặt cắt = tim cột)
        // bê tông lót
        AddRect(fndSecCanvas, S(ocx - L / 2 - 0.1, zb), S(ocx + L / 2 + 0.1, zb - 0.1), LeanFill, Brushes.Gray, 0.8);

        // cọc (các hàng cọc theo X)
        var xs = pc.Piles.Select(p => Math.Round(p.X, 3)).Distinct().OrderBy(x => x).ToList();
        foreach (double x in xs)
        {
            AddRect(fndSecCanvas, S(x - ps / 2, zb + emb), S(x + ps / 2, zPileBot), ConcreteFill, Brushes.Black, 1.2);
            BreakMark(S(x - ps / 2 - 0.05, zPileBot), S(x + ps / 2 + 0.05, zPileBot));
        }

        // đài + cột
        fndSecCanvas.Children.Add(new Shapes.Polygon
        {
            Points = new PointCollection { S(ocx - L / 2, zb), S(ocx + L / 2, zb), S(ocx + L / 2, zTop), S(cx / 2, zTop), S(cx / 2, colTop), S(-cx / 2, colTop), S(-cx / 2, zTop), S(ocx - L / 2, zTop) },
            Fill = ConcreteFill, Stroke = Brushes.Black, StrokeThickness = 1.6, IsHitTestVisible = false
        });
        // đoạn cọc ngàm trong đài (nét đứt)
        foreach (double x in xs)
        {
            CLine(fndSecCanvas, S(x - ps / 2, zb), S(x - ps / 2, zb + emb), Brushes.Gray, 0.7, true);
            CLine(fndSecCanvas, S(x + ps / 2, zb), S(x + ps / 2, zb + emb), Brushes.Gray, 0.7, true);
            CLine(fndSecCanvas, S(x - ps / 2, zb + emb), S(x + ps / 2, zb + emb), Brushes.Gray, 0.7, true);
        }
        BreakMark(S(-cx / 2 - 0.08, colTop), S(cx / 2 + 0.08, colTop));
        if (zN > zTop + 1e-3) CLine(fndSecCanvas, S(-cx / 2, zN), S(cx / 2, zN), Brushes.Gray, 0.6, true);

        // thép đáy đài
        double zr = zb + emb + 0.05, hook = Math.Min(0.2, H - emb - 0.1);
        fndSecCanvas.Children.Add(new Shapes.Polyline
        {
            Points = new PointCollection { S(ocx - L / 2 + 0.05, zr + hook), S(ocx - L / 2 + 0.05, zr), S(ocx + L / 2 - 0.05, zr), S(ocx + L / 2 - 0.05, zr + hook) },
            Stroke = RebarBrush, StrokeThickness = 2, IsHitTestVisible = false
        });
        var tagL = S(L * 0.2, zr); var tagB = S(-L * 0.2, zr);
        Leader(fndSecCanvas, tagL, new Point(tagL.X + 30, S(0, zb + H * 0.55).Y), $"{(string.IsNullOrEmpty(pc.BarsL) ? "?" : pc.BarsL)} (phương L = X)");
        Leader(fndSecCanvas, tagB, new Point(tagB.X - 10, S(0, zb + H * 0.85).Y), $"{(string.IsNullOrEmpty(pc.BarsB) ? "?" : pc.BarsB)} (phương B = Y)");

        // kích thước: chuỗi mép – tim cọc – mép, tổng L
        var chain = new List<double> { ocx - L / 2 };
        chain.AddRange(xs.Where(x => x > ocx - L / 2 + 1e-3 && x < ocx + L / 2 - 1e-3));
        chain.Add(ocx + L / 2);
        for (int i = 0; i + 1 < chain.Count; i++)
            DimLine(fndSecCanvas, S(chain[i], zPileBot), S(chain[i + 1], zPileBot), 16, ((chain[i + 1] - chain[i]) * 1000).ToString("0", Inv));
        DimLine(fndSecCanvas, S(ocx - L / 2, zPileBot), S(ocx + L / 2, zPileBot), 38, (L * 1000).ToString("0", Inv));
        DimLine(fndSecCanvas, S(ocx + L / 2, zb), S(ocx + L / 2, zTop), 22, "H=" + (H * 1000).ToString("0", Inv));
        DimLine(fndSecCanvas, S(-cx / 2, colTop), S(cx / 2, colTop), -14, (cx * 1000).ToString("0", Inv));

        ElevMark(fndSecCanvas, S(L / 2 + 0.75, zb), Elev(zb) + " (đáy đài)");
        ElevMark(fndSecCanvas, S(L / 2 + 0.75, zTop), Elev(zTop));

        string name = string.IsNullOrEmpty(pc.Name) ? "ĐC" : pc.Name;
        CText(fndSecCanvas, $"MẶT CẮT ĐÀI CỌC {name} (theo phương X)", new Point(w / 2, 14), Brushes.Black, 12.5, true, true).FontWeight = FontWeights.Bold;
        if (auto) CText(fndSecCanvas, "(chưa chọn móng – hiển thị đài đầu tiên)", new Point(w / 2, 31), Brushes.Gray, 10, true, true);
        double y = h - bottom + 8;
        void Info(string t, Brush br) { CText(fndSecCanvas, t, new Point(10, y), br, 10.5, false); y += 15; }
        Info($"Đài L×B×H = {pc.L.ToString("0.00", Inv)}×{pc.B.ToString("0.00", Inv)}×{H.ToString("0.00", Inv)} m;  {pc.Piles.Count} cọc {PileText(pc.PileRound, ps)}, dài {_p.Foundation.PileLength.ToString("0.#", Inv)} m", Brushes.Black);
        Info($"N = {pc.N.ToString("0.0", Inv)} kN;  Mx = {pc.Mx.ToString("0.0", Inv)};  My = {pc.My.ToString("0.0", Inv)} kN.m" + (string.IsNullOrEmpty(pc.Combo) ? "" : $"  ({pc.Combo})"), Brushes.Black);
        Info($"Pmax = {pc.PMax.ToString("0.0", Inv)};  Pmin = {pc.PMin.ToString("0.0", Inv)};  [P] = {pc.PileCapacity.ToString("0", Inv)} kN", Brushes.Black);
        Info(ResultText(pc), pc.Ok ? FootOkStroke : FootBadStroke);
    }

    // ------------------------------------------------------------------
    // Mặt cắt ngang móng băng (vuông góc phương dải)
    // ------------------------------------------------------------------
    private void DrawStripSection(StripFootingDesign sf, bool auto, double w, double h)
    {
        var nodes = NodeMap();
        double zN = sf.NodeIds.Count > 0 && nodes.TryGetValue(sf.NodeIds[0], out var nd) ? nd.Z : _p.BaseElevation;
        double B = sf.Width;
        double te = sf.FlangeEdge > 0 ? sf.FlangeEdge : 0.2;
        double tr = Math.Max(te, sf.FlangeRoot > 0 ? sf.FlangeRoot : te);
        double b = sf.RibB > 0 ? sf.RibB : 0.3;
        double hr = Math.Max(sf.RibH > 0 ? sf.RibH : 0.6, tr + 0.1);
        double zb = sf.Depth > 0 ? -sf.Depth : zN - hr;
        if (zb + hr > zN) zb = zN - hr;
        double zTop = zb + hr, colTop = Math.Max(zN, zTop) + 0.5;

        double wx0 = -B / 2 - 0.9, wx1 = B / 2 + 1.4, wz0 = zb - 0.65, wz1 = Math.Max(colTop, 0.25) + 0.3;
        double top = 44, bottom = 92;
        double s = Math.Min((w - 20) / (wx1 - wx0), (h - top - bottom) / (wz1 - wz0));
        if (s <= 0) return;
        double ox = 10 + ((w - 20) - (wx1 - wx0) * s) / 2 - wx0 * s;
        double oz = top + ((h - top - bottom) - (wz1 - wz0) * s) / 2 + wz1 * s;
        Point S(double x, double z) => new(ox + x * s, oz - z * s);

        DrawGround(S, wx0, wx1, B);
        AddRect(fndSecCanvas, S(-B / 2 - 0.1, zb), S(B / 2 + 0.1, zb - 0.1), LeanFill, Brushes.Gray, 0.8);
        fndSecCanvas.Children.Add(new Shapes.Polygon
        {
            Points = new PointCollection { S(-B / 2, zb), S(B / 2, zb), S(B / 2, zb + te), S(b / 2, zb + tr), S(b / 2, zTop), S(-b / 2, zTop), S(-b / 2, zb + tr), S(-B / 2, zb + te) },
            Fill = ConcreteFill, Stroke = Brushes.Black, StrokeThickness = 1.6, IsHitTestVisible = false
        });
        // cột phía sau mặt cắt (nét đứt)
        var colR = AddRect(fndSecCanvas, S(-b / 2, colTop), S(b / 2, zTop), null, Brushes.Gray, 0.9);
        colR.StrokeDashArray = new DoubleCollection { 4, 3 };
        CText(fndSecCanvas, "cột (sau mặt cắt)", S(b / 2 + 0.05, (zTop + colTop) / 2), Brushes.Gray, 9, false, true);

        // thép
        double c = 0.04;
        var stir = AddRect(fndSecCanvas, S(-b / 2 + c, zTop - c), S(b / 2 - c, zb + c), null, RebarBrush, 1.3);
        double dotR = Math.Max(2, Math.Min(4.5, 0.009 * s + 1));
        var botDots = new List<Point>(); var topDots = new List<Point>();
        for (int i = 0; i < 3; i++)
        {
            double x = -b / 2 + c + 0.015 + (b - 2 * c - 0.03) * i / 2;
            botDots.Add(S(x, zb + c + 0.015)); topDots.Add(S(x, zTop - c - 0.015));
        }
        foreach (var pt in botDots.Concat(topDots))
        {
            var e = new Shapes.Ellipse { Width = 2 * dotR, Height = 2 * dotR, Fill = RebarBrush, IsHitTestVisible = false };
            Canvas.SetLeft(e, pt.X - dotR); Canvas.SetTop(e, pt.Y - dotR);
            fndSecCanvas.Children.Add(e);
        }
        fndSecCanvas.Children.Add(new Shapes.Polyline
        {
            Points = new PointCollection { S(-B / 2 + c, zb + te - c), S(-B / 2 + c, zb + c), S(B / 2 - c, zb + c), S(B / 2 - c, zb + te - c) },
            Stroke = RebarBrush, StrokeThickness = 1.6, IsHitTestVisible = false
        });
        string Bar(string v) => string.IsNullOrEmpty(v) ? "?" : v;
        Leader(fndSecCanvas, topDots[2], new Point(S(B / 2, 0).X + 6, S(0, zTop + 0.05).Y), "Trên: " + Bar(sf.TopBars));
        Leader(fndSecCanvas, S(b / 2 - c, (zb + zTop) / 2), new Point(S(B / 2, 0).X + 6, S(0, (zb + tr + zTop) / 2).Y), "Đai: " + Bar(sf.Stirrups));
        Leader(fndSecCanvas, botDots[2], new Point(S(B / 2, 0).X + 6, S(0, zb + tr + 0.02).Y), "Dưới: " + Bar(sf.BottomBars));
        var fb = S(-B / 4, zb + c);
        Leader(fndSecCanvas, fb, new Point(fb.X - 10, S(0, zb - 0.38).Y), "Bản: " + Bar(sf.FlangeBars));

        // kích thước
        DimLine(fndSecCanvas, S(-B / 2, zb - 0.1), S(B / 2, zb - 0.1), 22, (B * 1000).ToString("0", Inv));
        DimLine(fndSecCanvas, S(-b / 2, zTop), S(b / 2, zTop), -14, (b * 1000).ToString("0", Inv));
        DimLine(fndSecCanvas, S(-B / 2, zb), S(-B / 2, zb + te), -18, (te * 1000).ToString("0", Inv));
        DimLine(fndSecCanvas, S(B / 2, zb), S(B / 2, zTop), 22, "h=" + (hr * 1000).ToString("0", Inv));
        ElevMark(fndSecCanvas, S(-B / 2 - 0.55, zb), Elev(zb));

        string name = string.IsNullOrEmpty(sf.Name) ? "MB" : sf.Name;
        CText(fndSecCanvas, $"MẶT CẮT MÓNG BĂNG {name}" + (string.IsNullOrEmpty(sf.Grid) ? "" : $" – trục {sf.Grid}"), new Point(w / 2, 14), Brushes.Black, 12.5, true, true).FontWeight = FontWeights.Bold;
        if (auto) CText(fndSecCanvas, "(chưa chọn móng – hiển thị dải đầu tiên)", new Point(w / 2, 31), Brushes.Gray, 10, true, true);
        double y = h - bottom + 8;
        void Info(string t, Brush br) { CText(fndSecCanvas, t, new Point(10, y), br, 10.5, false); y += 15; }
        Info($"Dải {(sf.AlongX ? "dọc X" : "dọc Y")} tại {(sf.AlongX ? "Y" : "X")} = {sf.C.ToString("0.00", Inv)}: {sf.Start.ToString("0.00", Inv)} → {sf.End.ToString("0.00", Inv)} m (L = {(sf.End - sf.Start).ToString("0.00", Inv)} m), {sf.NodeIds.Count} cột", Brushes.Black);
        Info($"B = {B.ToString("0.00", Inv)} m;  bản {(te * 1000).ToString("0", Inv)}/{(tr * 1000).ToString("0", Inv)} mm;  sườn {(b * 1000).ToString("0", Inv)}×{(hr * 1000).ToString("0", Inv)} mm", Brushes.Black);
        string mtxt = sf.M.Count > 0 ? $";  M = {sf.M.Min().ToString("0.0", Inv)} … {sf.M.Max().ToString("0.0", Inv)} kN.m" : "";
        Info($"Pmax = {sf.PMax.ToString("0.0", Inv)};  R = {sf.R.ToString("0.0", Inv)} kN/m²" + mtxt, Brushes.Black);
        Info(ResultText(sf), sf.Ok ? FootOkStroke : FootBadStroke);
    }

    private static string ResultText(MemberDesignBase m)
    {
        if (m.Ok) return "Kết quả: ĐẠT";
        string msg = m.Messages.FirstOrDefault(x => x.StartsWith("LỖI")) ?? m.Checks.Where(c => !c.Ok).Select(c => $"{c.Name}: {c.Ratio.ToString("0.00", Inv)}").FirstOrDefault();
        return "Kết quả: KHÔNG ĐẠT" + (string.IsNullOrEmpty(msg) ? "" : " – " + msg);
    }

    private void DrawGround(Func<double, double, Point> S, double wx0, double wx1, double width)
    {
        var g0 = S(wx0 + 0.1, 0); var g1 = S(wx1 - 0.1, 0);
        CLine(fndSecCanvas, g0, g1, Brushes.SaddleBrown, 1.2, false);
        for (double x = g0.X; x < g1.X - 4; x += 9)
            CLine(fndSecCanvas, new Point(x, g0.Y), new Point(x + 6, g0.Y + 6), Brushes.SaddleBrown, 0.6, false);
        ElevMark(fndSecCanvas, S(-width / 2 - 0.7, 0), "±0.000 (mặt đất)");
    }

    private static Shapes.Rectangle AddRect(Canvas cv, Point a, Point b, Brush fill, Brush stroke, double th)
    {
        double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
        var r = new Shapes.Rectangle
        {
            Width = Math.Max(1, Math.Abs(b.X - a.X)), Height = Math.Max(1, Math.Abs(b.Y - a.Y)),
            Fill = fill, Stroke = stroke, StrokeThickness = th, IsHitTestVisible = false
        };
        Canvas.SetLeft(r, x); Canvas.SetTop(r, y);
        cv.Children.Add(r);
        return r;
    }

    /// <summary>Ký hiệu cắt ngắt (zigzag) giữa hai điểm cùng cao độ màn hình.</summary>
    private void BreakMark(Point b0, Point b1)
    {
        fndSecCanvas.Children.Add(new Shapes.Polyline
        {
            Points = new PointCollection { b0, new Point(b0.X + (b1.X - b0.X) * 0.4, b0.Y), new Point(b0.X + (b1.X - b0.X) * 0.47, b0.Y - 6),
                                           new Point(b0.X + (b1.X - b0.X) * 0.53, b0.Y + 6), new Point(b0.X + (b1.X - b0.X) * 0.6, b0.Y), b1 },
            Stroke = Brushes.Black, StrokeThickness = 1, Fill = Brushes.White, IsHitTestVisible = false
        });
    }

    // ------------------------------------------------------------------
    // Chọn loại móng + thiết kế
    // ------------------------------------------------------------------
    /// <summary>Nút chân cột đang chọn (móng hoặc cột chân); 0 nếu không có.</summary>
    private int SelectedBaseNode()
    {
        if (_p == null) return 0;
        if (_selFooting != 0) return _selFooting;
        if (_sel != null && _sel.IsVerticalKind)
        {
            var ni = _p.Nodes.FirstOrDefault(n => n.Id == _sel.NodeI); var nj = _p.Nodes.FirstOrDefault(n => n.Id == _sel.NodeJ);
            var bot = ni != null && nj != null ? (ni.Z <= nj.Z ? ni : nj) : null;
            if (bot != null && bot.IsSupport) return bot.Id;
        }
        return 0;
    }

    private void SyncFoundationUi()
    {
        if (_p == null || cbFndType == null) return;
        _fndUiSync = true;
        try
        {
            if (cbFndType.ItemsSource == null) cbFndType.ItemsSource = FoundationDialog.TypeNames;
            if (cbFndNodeType.ItemsSource == null) cbFndNodeType.ItemsSource = new[] { "Theo công trình", "Móng đơn", "Móng cọc", "Móng băng" };
            cbFndType.SelectedIndex = (int)_p.Foundation.Type;
            int node = SelectedBaseNode();
            cbFndNodeType.IsEnabled = node != 0;
            var ft = node != 0 ? _p.Footings.FirstOrDefault(f => f.NodeId == node) : null;
            cbFndNodeType.SelectedIndex = node == 0 || ft?.Type == null ? 0 : (int)ft.Type.Value + 1;
        }
        finally { _fndUiSync = false; }
    }

    private void CbFndType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_fndUiSync || _p == null || cbFndType.SelectedIndex < 0) return;
        var t = (FoundationType)cbFndType.SelectedIndex;
        if (t == _p.Foundation.Type) return;
        if (_busy) { SyncFoundationUi(); return; }
        string snap = Snapshot();
        _p.Foundation.Type = t;
        PushUndo(snap);
        MarkDirty(false);
        ShowSelection();
        RedrawFoundationPlan();
        SetStatus($"Phương án móng: {FoundationTypeName(t)} – bấm 'Thiết kế móng' để tính lại.");
    }

    private void CbFndNodeType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_fndUiSync || _p == null || cbFndNodeType.SelectedIndex < 0) return;
        int node = SelectedBaseNode();
        if (node == 0) return;
        if (_busy) { SyncFoundationUi(); return; }
        FoundationType? t = cbFndNodeType.SelectedIndex <= 0 ? null : (FoundationType)(cbFndNodeType.SelectedIndex - 1);
        var ft = _p.Footings.FirstOrDefault(f => f.NodeId == node);
        if (ft?.Type == t || (ft == null && t == null)) return;
        string snap = Snapshot();
        if (ft == null)
        {
            int k = 1;
            while (_p.Footings.Any(f => f.Label == $"M{k}")) k++;
            ft = new Footing { Label = $"M{k}", NodeId = node };
            _p.Footings.Add(ft);
        }
        ft.Type = t;
        PushUndo(snap);
        MarkDirty(false);
        ShowSelection();
        SetStatus($"Chân cột (nút {node}): {(t == null ? "theo phương án công trình (" + FoundationTypeName(_p.Foundation.Type) + ")" : FoundationTypeName(t.Value))} – bấm 'Thiết kế móng' để tính lại.");
    }

    private async void BtnFndDesign_Click(object sender, RoutedEventArgs e) => await DesignFoundation();

    private async void MnuDesignFoundation_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null || _busy) return;
        string snap = Snapshot();
        var dlg = new FoundationDialog(_p.Foundation, _p.Footings.Count(f => f.Type != null)) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        PushUndo(snap);
        MarkDirty(false);
        SyncFoundationUi();
        if (!dlg.RunDesign) { SetStatus($"Đã lưu thông số móng ({FoundationTypeName(_p.Foundation.Type)})."); return; }
        var tab = tabView.Items.OfType<TabItem>().FirstOrDefault(t => (t.Header as string) == "Mô hình móng");
        if (tab != null) tabView.SelectedItem = tab;
        await DesignFoundation();
    }

    /// <summary>Chạy phân tích (nếu cần) rồi thiết kế BTCT + móng theo phương án hiện tại; giữ kết quả kiểm tra thép.</summary>
    private async Task<bool> DesignFoundation()
    {
        if (_p == null || _busy) return false;
        if (!await EnsureAnalysis()) return false;
        if (!_p.Frames.Any(f => f.IsConcrete))
        {
            MessageBox.Show(this, "Mô hình không có cấu kiện BTCT.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        var res = _res;
        var start = new DesignSummary();
        if (_design != null) start.Steel.AddRange(_design.Steel);
        DesignSummary ds = null;
        bool ok = await RunBusy("Lỗi thiết kế móng", prog =>
        {
            prog.Report($"Thiết kế BTCT và móng ({FoundationTypeName(_p.Foundation.Type).ToLowerInvariant()})...");
            ds = ConcreteDesigner.Design(_p, res, start);
        });
        if (!ok || ds == null) return false;
        _design = ds;
        _fndSelItem = null;
        _fFitPending = true;
        RefreshDesign();
        RedrawSecondaryViews();
        ShowSelection();
        int nf = ds.Footings.Count, nc = ds.PileCaps?.Count ?? 0, nsf = ds.StripFootings?.Count ?? 0;
        int bad = ds.Footings.Count(f => !f.Ok) + (ds.PileCaps?.Count(f => !f.Ok) ?? 0) + (ds.StripFootings?.Count(f => !f.Ok) ?? 0);
        var parts = new List<string>();
        if (nf > 0) parts.Add($"{nf} móng đơn");
        if (nc > 0) parts.Add($"{nc} đài cọc");
        if (nsf > 0) parts.Add($"{nsf} dải móng băng");
        SetStatus($"Thiết kế móng xong ({FoundationTypeName(_p.Foundation.Type)}): " + (parts.Count > 0 ? string.Join(", ", parts) : "không có móng") + $" – {bad} không đạt.");
        return true;
    }
}

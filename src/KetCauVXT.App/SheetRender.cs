using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Phần tử WPF vẽ một Drawing (toạ độ mm) nhân hệ số Scale (DIP / mm).</summary>
internal sealed class DrawingHost : FrameworkElement
{
    public System.Windows.Media.Drawing Drawing { get; set; }
    public double Scale { get; set; } = 1;
    public Brush Background { get; set; }

    protected override void OnRender(DrawingContext dc)
    {
        if (Background != null) dc.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (Drawing == null) return;
        bool sc = Math.Abs(Scale - 1) > 1e-12;
        if (sc) dc.PushTransform(new ScaleTransform(Scale, Scale));
        dc.DrawDrawing(Drawing);
        if (sc) dc.Pop();
    }
}

/// <summary>Chuyển các mảnh hình DXF (Figure) thành hình vẽ WPF: nét theo layer (màu + bề dày in), chữ Arial,
/// cung/tròn, miền tô SOLID. Toạ độ ra là mm giấy, trục y hướng xuống (gốc tại (ox, oy) của giấy).</summary>
internal static class SheetRender
{
    // ---- font chữ theo chuẩn layer CAD (CadStandard.ActiveFontName: Arial | Times New Roman) ----
    private static string _faceName;
    private static Typeface _face, _faceBold;
    private static Typeface FaceOf(bool bold)
    {
        string n = CadStandard.ActiveFontName;
        if (_face == null || n != _faceName)
        {
            _faceName = n;
            _face = new Typeface(new FontFamily(n), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            _faceBold = new Typeface(new FontFamily(n), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        }
        return bold ? _faceBold : _face;
    }
    private static Typeface Face => FaceOf(false);
    // ---- hết phần font ----
    public const double DipPerMm = 96.0 / 25.4;

    private static readonly Dictionary<string, (Color C, double Mm)> Styles = new()
    {
        ["0"] = (Colors.Black, 0.18),
        [Lay.BeTong] = (Colors.Black, 0.35),
        [Lay.Thep] = (Color.FromRgb(200, 0, 0), 0.35),
        [Lay.Dai] = (Color.FromRgb(0, 130, 0), 0.25),
        [Lay.KichThuoc] = (Color.FromRgb(0, 100, 150), 0.13),
        [Lay.Chu] = (Colors.Black, 0.18),
        [Lay.KyHieu] = (Color.FromRgb(130, 0, 150), 0.18),
        [Lay.Truc] = (Color.FromRgb(110, 110, 110), 0.13),
        [Lay.KhungTen] = (Colors.Black, 0.25),
        [Lay.Hatch] = (Color.FromRgb(150, 150, 150), 0.09),   // tô vật liệu mặt cắt (DxfWriter.Hatch.cs)
    };

    private static readonly Dictionary<Color, SolidColorBrush> _brushes = new();

    private static SolidColorBrush BrushOf(Color c)
    {
        if (!_brushes.TryGetValue(c, out var b)) { b = new SolidColorBrush(c); b.Freeze(); _brushes[c] = b; }
        return b;
    }

    private static (Color C, double Mm) Style(string layer) =>
        Styles.TryGetValue(layer ?? "0", out var s) ? s : Styles["0"];

    private sealed class Bucket
    {
        public StreamGeometry G = new();
        public StreamGeometryContext Ctx;
        public Pen Pen;
        public Brush Fill;
    }

    /// <summary>Vẽ các mảnh hình đã đặt (K, Dx, Dy: mô hình → mm giấy) thành Drawing toạ độ (X − ox, oy − Y).
    /// minStroke: bề dày nét tối thiểu (mm giấy) – để nét mảnh vẫn thấy được khi thu nhỏ màn hình; in thì 0.</summary>
    public static DrawingGroup Render(IEnumerable<(DxfWriter W, double K, double Dx, double Dy)> items, double ox, double oy,
        double minStroke, Func<DxfWriter, List<DrawPrim>> prims, Color? color = null, bool bold = false, double weightMm = 0)
    {
        var buckets = new Dictionary<string, Bucket>();
        var texts = new List<(DrawPrim P, double K, Point At)>();

        Bucket Stroke(string layer, string ltype, double widthMm)
        {
            var (c, mm) = Style(layer);
            if (color.HasValue) c = color.Value;
            if (weightMm > 0) mm = weightMm;
            double t = Math.Max(widthMm > 0 ? widthMm : mm, minStroke);
            string lt = !string.IsNullOrEmpty(ltype) ? ltype.ToUpperInvariant() : (layer == Lay.Truc ? "CENTER" : "");
            if (lt == "CONTINUOUS") lt = "";
            string key = $"S|{c}|{lt}|{t:0.####}";
            if (!buckets.TryGetValue(key, out var b))
            {
                var pen = new Pen(BrushOf(c), t) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                if (lt == "CENTER" || lt == "DASHED")
                {
                    double[] mmPat = lt == "CENTER" ? new[] { 10.0, 1.6, 1.6, 1.6 } : new[] { 3.5, 1.6 };
                    pen.DashStyle = new DashStyle(mmPat.Select(x => x / t), 0);
                    pen.StartLineCap = pen.EndLineCap = pen.DashCap = PenLineCap.Flat;
                }
                pen.Freeze();
                b = new Bucket { Pen = pen };
                b.Ctx = b.G.Open();
                buckets[key] = b;
            }
            return b;
        }

        Bucket FillB(string layer)
        {
            var (c, _) = Style(layer);
            if (color.HasValue) c = color.Value;
            string key = $"F|{c}";
            if (!buckets.TryGetValue(key, out var b))
            {
                b = new Bucket { Fill = BrushOf(c) };
                b.Ctx = b.G.Open();
                buckets[key] = b;
            }
            return b;
        }

        // ---- tô vật liệu (HATCH): vẽ trước mọi nét khác (nằm dưới), nét mảnh màu xám; SOLID tô chẵn – lẻ riêng từng miền ----
        int hatchSolidNo = 0;
        Bucket HatchStroke(string layer)
        {
            var (c, mm) = Style(layer);
            if (color.HasValue) c = color.Value;
            double t = Math.Max(Math.Min(mm, 0.13), 0.5 * minStroke);
            string key = $"H|S|{c}|{t:0.####}";
            if (!buckets.TryGetValue(key, out var b))
            {
                var pen = new Pen(BrushOf(c), t) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
                pen.Freeze();
                b = new Bucket { Pen = pen };
                b.Ctx = b.G.Open();
                buckets[key] = b;
            }
            return b;
        }
        Bucket HatchFill(string layer)
        {
            var (c, _) = Style(layer);
            if (color.HasValue) c = color.Value;
            var b = new Bucket { Fill = BrushOf(c) };
            b.G.FillRule = FillRule.EvenOdd;
            b.Ctx = b.G.Open();
            buckets[$"H|F|{hatchSolidNo++}"] = b;
            return b;
        }

        foreach (var (w, k, dx, dy) in items)
        {
            if (w == null) continue;
            Point P(double x, double y) => new(k * x + dx - ox, oy - (k * y + dy));
            foreach (var pr in prims(w))
            {
                var a = pr.Pts;
                switch (pr.Kind)
                {
                    case PrimKind.Line:
                    {
                        var ctx = Stroke(pr.Layer, pr.LType, 0).Ctx;
                        ctx.BeginFigure(P(a[0], a[1]), false, false);
                        ctx.LineTo(P(a[2], a[3]), true, false);
                        break;
                    }
                    case PrimKind.Polyline:
                    {
                        var ctx = Stroke(pr.Layer, pr.LType, pr.Width * k).Ctx;
                        ctx.BeginFigure(P(a[0], a[1]), false, pr.Closed);
                        var list = new List<Point>(a.Length / 2);
                        for (int i = 2; i + 1 < a.Length; i += 2) list.Add(P(a[i], a[i + 1]));
                        ctx.PolyLineTo(list, true, true);
                        break;
                    }
                    case PrimKind.Circle:
                    {
                        double r = pr.R * k;
                        if (r <= 0) break;
                        var c = P(a[0], a[1]);
                        var ctx = Stroke(pr.Layer, pr.LType, 0).Ctx;
                        ctx.BeginFigure(new Point(c.X + r, c.Y), false, true);
                        ctx.ArcTo(new Point(c.X - r, c.Y), new Size(r, r), 0, false, SweepDirection.Counterclockwise, true, false);
                        ctx.ArcTo(new Point(c.X + r, c.Y), new Size(r, r), 0, false, SweepDirection.Counterclockwise, true, false);
                        break;
                    }
                    case PrimKind.Arc:
                    {
                        double r = pr.R * k;
                        if (r <= 0) break;
                        double sweep = ((pr.A2 - pr.A1) % 360 + 360) % 360;
                        if (sweep < 1e-9) sweep = 360;
                        double a1 = pr.A1 * Math.PI / 180, a2 = (pr.A1 + sweep) * Math.PI / 180;
                        var ctx = Stroke(pr.Layer, pr.LType, 0).Ctx;
                        var p1 = P(a[0] + pr.R * Math.Cos(a1), a[1] + pr.R * Math.Sin(a1));
                        var p2 = P(a[0] + pr.R * Math.Cos(a2), a[1] + pr.R * Math.Sin(a2));
                        ctx.BeginFigure(p1, false, false);
                        if (sweep >= 359.999)
                        {
                            var pm = P(a[0] - pr.R * Math.Cos(a1), a[1] - pr.R * Math.Sin(a1));
                            ctx.ArcTo(pm, new Size(r, r), 0, false, SweepDirection.Counterclockwise, true, false);
                            ctx.ArcTo(p1, new Size(r, r), 0, false, SweepDirection.Counterclockwise, true, false);
                        }
                        else ctx.ArcTo(p2, new Size(r, r), 0, sweep > 180, SweepDirection.Counterclockwise, true, false);
                        break;
                    }
                    case PrimKind.Solid:
                    {
                        var ctx = FillB(pr.Layer).Ctx;
                        ctx.BeginFigure(P(a[0], a[1]), true, true);
                        ctx.PolyLineTo(new[] { P(a[2], a[3]), P(a[4], a[5]), P(a[6], a[7]) }, false, false);
                        break;
                    }
                    case PrimKind.Text:
                        if (pr.H * k > 1e-4) texts.Add((pr, k, P(a[0], a[1])));
                        break;
                    case PrimKind.Hatch:
                    {
                        var hd = DxfWriter.HatchOf(pr);
                        if (hd == null || hd.Loops.Count == 0) break;
                        if (hd.IsSolid)
                        {
                            var ctx = HatchFill(pr.Layer).Ctx;
                            foreach (var l in hd.Loops)
                            {
                                ctx.BeginFigure(P(l[0].X, l[0].Y), true, true);
                                ctx.PolyLineTo(l.Skip(1).Select(q => P(q.X, q.Y)).ToList(), false, false);
                            }
                        }
                        else
                        {
                            // cùng bộ sinh nét với bản DXF R12 mô phỏng ⇒ màn hình / in / PDF trùng file R12
                            var ctx = HatchStroke(pr.Layer).Ctx;
                            foreach (var (x1, y1, x2, y2) in DxfWriter.HatchSegments(pr))
                            {
                                ctx.BeginFigure(P(x1, y1), false, false);
                                ctx.LineTo(P(x2, y2), true, false);
                            }
                        }
                        break;
                    }
                }
            }
        }

        var dg = new DrawingGroup();
        using (var dc = dg.Open())
        {
            var order = buckets.Where(kv => kv.Key.StartsWith("H|", StringComparison.Ordinal)).Select(kv => kv.Value)
                .Concat(buckets.Where(kv => !kv.Key.StartsWith("H|", StringComparison.Ordinal)).Select(kv => kv.Value)).ToList();
            foreach (var b in order)
            {
                b.Ctx.Close();
                b.G.Freeze();
                if (b.Fill != null) dc.DrawGeometry(b.Fill, null, b.G);
                else dc.DrawGeometry(null, b.Pen, b.G);
            }
            foreach (var (pr, k, at) in texts) DrawText(dc, pr, k, at, color, bold);
        }
        dg.Freeze();
        return dg;
    }

    private static Typeface FaceBold => FaceOf(true);   // font theo chuẩn layer CAD

    private static void DrawText(DrawingContext dc, DrawPrim pr, double k, Point at, Color? color = null, bool bold = false)
    {
        double h = pr.H * k;                 // chiều cao chữ hoa (mm)
        const double up = 10;                // dựng chữ ở cỡ ×10 rồi thu lại (tránh làm tròn cỡ chữ nhỏ)
        double em = h / 0.716 * up;
        var (c, _) = Style(pr.Layer);
        if (color.HasValue) c = color.Value;
        var ft = new FormattedText(pr.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, bold ? FaceBold : Face, em, BrushOf(c), 1.0);
        double w = ft.WidthIncludingTrailingWhitespace, cap = h * up;
        double dx = pr.HA == 1 || pr.HA == 4 ? -w / 2 : pr.HA == 2 ? -w : 0;
        double dy = pr.VA switch
        {
            1 => -(ft.Baseline + 0.21 * em),
            2 => -(ft.Baseline - cap / 2),
            3 => -(ft.Baseline - cap),
            _ => -ft.Baseline
        };
        dc.PushTransform(new TranslateTransform(at.X, at.Y));
        bool rot = Math.Abs(pr.Rot) > 1e-9;
        if (rot) dc.PushTransform(new RotateTransform(-pr.Rot));
        double wf = pr.WF > 0 ? pr.WF : 1;   // hệ số rộng chữ (Thiết lập bản vẽ) – khớp DXF
        dc.PushTransform(new ScaleTransform(wf / up, 1 / up));
        dc.DrawText(ft, new Point(dx, dy));
        dc.Pop();
        if (rot) dc.Pop();
        dc.Pop();
    }

    // ==================================================================
    // Cả tờ (in, PDF)
    // ==================================================================
    /// <summary>Hình vẽ cả tờ (mm giấy, y xuống): nền trắng, khung tên, các khung nhìn (cắt theo khung).</summary>
    public static DrawingGroup RenderSheet(Project p, Sheet s, Func<SheetView, List<Figure>> figs, Func<DxfWriter, List<DrawPrim>> prims, double minStroke = 0)
    {
        var (pw, ph) = s.PaperSize();
        var lays = s.Views.Select(v => (V: v, L: SheetBuilder.Layout(figs(v), v))).ToList();
        var dg = new DrawingGroup();
        using (var dc = dg.Open())
        {
            dc.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, pw, ph));
            dc.DrawDrawing(RenderTitle(p, s, SheetBuilder.ScaleText(lays.Select(t => (t.V, t.L))), ph, minStroke));
            foreach (var (v, l) in lays)
            {
                dc.PushClip(new RectangleGeometry(new Rect(v.X, ph - v.Y - v.H, Math.Max(0, v.W), Math.Max(0, v.H))));
                dc.DrawDrawing(Render(ViewItems(l), 0, ph, minStroke, prims));
                dc.Pop();
                if (v.Border) dc.DrawDrawing(Render(new[] { (SheetBuilder.ViewBorder(v), 1.0, 0.0, 0.0) }, 0, ph, minStroke, prims));
            }
            // đối tượng chú thích tự do (dàn trang thủ công)
            dc.DrawDrawing(RenderItems(s.Items, ph, minStroke));
        }
        dg.Freeze();
        return dg;
    }

    // ==================================================================
    // Dàn trang thủ công: khung tên + logo, đối tượng chú thích, ảnh nhúng
    // ==================================================================
    private static readonly Dictionary<string, BitmapSource> _images = new();

    /// <summary>Giải mã ảnh base64 (PNG/JPG) – có bộ đệm; lỗi → null.</summary>
    public static BitmapSource DecodeImage(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        if (_images.TryGetValue(base64, out var bmp)) return bmp;
        try
        {
            var bytes = Convert.FromBase64String(base64);
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = new MemoryStream(bytes);
            bi.EndInit();
            bi.Freeze();
            bmp = bi;
        }
        catch (Exception) { bmp = null; }
        if (_images.Count > 64) _images.Clear();
        _images[base64] = bmp;
        return bmp;
    }

    public static Color? ItemColor(SheetItem it) =>
        SheetBuilder.TryParseColor(it?.Color, out byte r, out byte g, out byte b) ? Color.FromRgb(r, g, b) : null;

    /// <summary>Khung tên (và logo công ty nếu có) của tờ, toạ độ mm giấy y xuống.</summary>
    public static DrawingGroup RenderTitle(Project p, Sheet s, string scaleText, double ph, double minStroke)
    {
        var tb = SheetBuilder.TitleBlock(p, s, scaleText);
        var dg = new DrawingGroup();
        using (var dc = dg.Open())
        {
            // ---- [khung tên người dùng dạng ảnh – SheetBuilder.TitleBlockCustom.cs]: ảnh nằm dưới chữ trường ----
            var cr = SheetBuilder.CustomTitleImageRect(p, s);
            var cimg = cr.IsEmpty ? null : DecodeImage(p.TitleBlock.Custom.ImageData);
            if (cimg != null) dc.DrawImage(cimg, new Rect(cr.X1, ph - cr.Y2, Math.Max(0.1, cr.W), Math.Max(0.1, cr.H)));
            // ---- [hết] ----
            dc.DrawDrawing(Render(new[] { (tb, 1.0, 0.0, 0.0) }, 0, ph, minStroke, w => w.GetPrimitives()));
            var lr = SheetBuilder.TitleLogoRect(p, s);
            var img = lr.IsEmpty ? null : DecodeImage(p.TitleBlock.LogoData);
            if (img != null) dc.DrawImage(img, Fit(img, new Rect(lr.X1, ph - lr.Y2, lr.W, lr.H)));
        }
        dg.Freeze();
        return dg;
    }

    /// <summary>Hình chữ nhật giữ tỷ lệ ảnh, căn giữa trong r.</summary>
    private static Rect Fit(BitmapSource img, Rect r)
    {
        double iw = Math.Max(1, img.PixelWidth), ih = Math.Max(1, img.PixelHeight);
        double k = Math.Min(r.Width / iw, r.Height / ih);
        double w = iw * k, h = ih * k;
        return new Rect(r.X + (r.Width - w) / 2, r.Y + (r.Height - h) / 2, w, h);
    }

    /// <summary>Vẽ một đối tượng chú thích (mm giấy, y xuống, gốc góc trái trên giấy cao ph).</summary>
    public static void DrawItem(DrawingContext dc, SheetItem it, double ph, double minStroke)
    {
        if (it == null) return;
        if (it.Kind == SheetItemKind.Image)
        {
            var img = DecodeImage(it.ImageData);
            if (img != null)
            {
                dc.DrawImage(img, new Rect(it.X, ph - it.Y - Math.Max(0.1, it.H), Math.Max(0.1, it.W), Math.Max(0.1, it.H)));
                return;
            }
        }
        var fig = SheetBuilder.ItemFigure(it);
        if (fig.EntityCount == 0) return;
        dc.DrawDrawing(Render(new[] { (fig, 1.0, 0.0, 0.0) }, 0, ph, minStroke, w => w.GetPrimitives(),
            ItemColor(it), it.Bold, (it.Kind == SheetItemKind.Circle || it.Kind == SheetItemKind.Cloud) ? it.LineWeight : 0));
    }

    public static DrawingGroup RenderItems(IEnumerable<SheetItem> items, double ph, double minStroke)
    {
        var dg = new DrawingGroup();
        using (var dc = dg.Open())
            foreach (var it in items ?? Enumerable.Empty<SheetItem>()) DrawItem(dc, it, ph, minStroke);
        dg.Freeze();
        return dg;
    }

    public static IEnumerable<(DxfWriter W, double K, double Dx, double Dy)> ViewItems(ViewLayout l)
    {
        foreach (var it in l.Items) yield return (it.F.W, it.K, it.Dx, it.Dy);
        if (l.Caption != null) yield return (l.Caption, 1, 0, 0);
    }

    /// <summary>Ảnh raster của tờ ở mật độ dpi (nền trắng).</summary>
    public static BitmapSource Rasterize(System.Windows.Media.Drawing sheetDrawing, double pwMm, double phMm, double dpi)
    {
        double pxPerMm = dpi / 25.4;
        int wpx = Math.Max(1, (int)Math.Round(pwMm * pxPerMm)), hpx = Math.Max(1, (int)Math.Round(phMm * pxPerMm));
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, wpx, hpx));
            dc.PushTransform(new ScaleTransform(pxPerMm, pxPerMm));
            dc.DrawDrawing(sheetDrawing);
            dc.Pop();
        }
        var rtb = new RenderTargetBitmap(wpx, hpx, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    /// <summary>Ghi file PDF: mỗi tờ một trang đúng khổ giấy, nội dung là ảnh (nén Flate, không mất dữ liệu).</summary>
    public static void WriteRasterPdf(string path, IList<(BitmapSource Img, double WMm, double HMm)> pages, string title)
    {
        using var fs = File.Create(path);
        var offsets = new List<long>();
        var latin = Encoding.ASCII;
        void W(string s) { var b = latin.GetBytes(s); fs.Write(b, 0, b.Length); }
        void Obj(int id) { while (offsets.Count < id) offsets.Add(0); offsets[id - 1] = fs.Position; W($"{id} 0 obj\n"); }
        string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        int n = pages.Count;
        // 1 catalog, 2 pages, 3 info, rồi mỗi trang 3 đối tượng: page, content, image
        W("%PDF-1.4\n");
        fs.Write(new byte[] { 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A }, 0, 6);
        Obj(1); W("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        var kids = string.Join(" ", Enumerable.Range(0, n).Select(i => $"{4 + 3 * i} 0 R"));
        Obj(2); W($"<< /Type /Pages /Kids [{kids}] /Count {n} >>\nendobj\n");
        Obj(3); W($"<< /Producer (Viet Const) /Title {PdfText(title)} >>\nendobj\n");
        for (int i = 0; i < n; i++)
        {
            var (img, wmm, hmm) = pages[i];
            double wpt = wmm / 25.4 * 72, hpt = hmm / 25.4 * 72;
            int pid = 4 + 3 * i, cid = pid + 1, iid = pid + 2;
            Obj(pid);
            W($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(wpt)} {F(hpt)}] /Resources << /XObject << /Im{i} {iid} 0 R >> >> /Contents {cid} 0 R >>\nendobj\n");
            string content = $"q {F(wpt)} 0 0 {F(hpt)} 0 0 cm /Im{i} Do Q\n";
            Obj(cid); W($"<< /Length {content.Length} >>\nstream\n{content}endstream\nendobj\n");

            var src = img.Format == PixelFormats.Bgra32 || img.Format == PixelFormats.Pbgra32 ? img : new FormatConvertedBitmap(img, PixelFormats.Bgra32, null, 0);
            int pw = src.PixelWidth, ph = src.PixelHeight, stride = pw * 4;
            var bgra = new byte[stride * ph];
            src.CopyPixels(bgra, stride, 0);
            byte[] z;
            using (var ms = new MemoryStream())
            {
                using (var zs = new ZLibStream(ms, CompressionLevel.Optimal, true))
                {
                    var row = new byte[pw * 3];
                    for (int y = 0; y < ph; y++)
                    {
                        int o = y * stride;
                        for (int x = 0; x < pw; x++)
                        {
                            int a = bgra[o + 4 * x + 3];
                            // ghép lên nền trắng (ảnh đã nhân alpha)
                            row[3 * x] = (byte)Math.Min(255, bgra[o + 4 * x + 2] + 255 - a);
                            row[3 * x + 1] = (byte)Math.Min(255, bgra[o + 4 * x + 1] + 255 - a);
                            row[3 * x + 2] = (byte)Math.Min(255, bgra[o + 4 * x] + 255 - a);
                        }
                        zs.Write(row, 0, row.Length);
                    }
                }
                z = ms.ToArray();
            }
            Obj(iid);
            W($"<< /Type /XObject /Subtype /Image /Width {pw} /Height {ph} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {z.Length} >>\nstream\n");
            fs.Write(z, 0, z.Length);
            W("\nendstream\nendobj\n");
        }
        long xref = fs.Position;
        W($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) W(o.ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
        W($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R /Info 3 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }

    /// <summary>Chuỗi PDF dạng UTF-16BE (hex) để giữ chữ tiếng Việt trong tiêu đề tài liệu.</summary>
    private static string PdfText(string s)
    {
        var sb = new StringBuilder("<FEFF");
        foreach (char c in s ?? "") sb.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
        return sb.Append('>').ToString();
    }
}

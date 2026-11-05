using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace KetCauVXT.Drawing;

/// <summary>Ghi PDF DẠNG NÉT (vector) từ các bản vẽ DxfWriter: mỗi trang đúng khổ giấy, nét theo layer (màu + bề dày in),
/// cung / tròn (Bezier), miền tô, chữ tiếng Việt bằng phông TrueType nhúng (Arial / Times New Roman của máy – CIDFont Identity-H).
/// Máy không có phông TrueType: chữ ghi bằng Helvetica, bỏ dấu (vẫn đọc được). Không dùng mạng, không thu thập dữ liệu.</summary>
public static class PdfVectorWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const double PtPerMm = 72.0 / 25.4;

    /// <summary>(màu RGB 0..1, bề dày in mm) theo layer – khớp màn hình / máy in (App/SheetRender).</summary>
    private static (double R, double G, double B, double Mm) Style(string layer) => layer switch
    {
        Lay.BeTong => (0, 0, 0, 0.35),
        Lay.Thep => (200 / 255.0, 0, 0, 0.35),
        Lay.Dai => (0, 130 / 255.0, 0, 0.25),
        Lay.KichThuoc => (0, 100 / 255.0, 150 / 255.0, 0.13),
        Lay.Chu => (0, 0, 0, 0.18),
        Lay.KyHieu => (130 / 255.0, 0, 150 / 255.0, 0.18),
        Lay.Truc => (110 / 255.0, 110 / 255.0, 110 / 255.0, 0.13),
        Lay.KhungTen => (0, 0, 0, 0.25),
        Lay.Hatch => (150 / 255.0, 150 / 255.0, 150 / 255.0, 0.09),
        _ => (0, 0, 0, 0.18)
    };

    /// <summary>Đường dẫn phông TrueType theo tên (Arial / Times New Roman), null nếu không có. Có thể đặt sẵn để kiểm thử.</summary>
    public static string FontPathOverride { get; set; }

    public static string FindFont(string family = null)
    {
        if (!string.IsNullOrEmpty(FontPathOverride) && File.Exists(FontPathOverride)) return FontPathOverride;
        family ??= KetCauVXT.Model.CadStandard.ActiveFontName;
        bool times = (family ?? "").StartsWith("Times", StringComparison.OrdinalIgnoreCase);
        var dirs = new List<string>();
        try { string f = Environment.GetFolderPath(Environment.SpecialFolder.Fonts); if (!string.IsNullOrEmpty(f)) dirs.Add(f); } catch (Exception) { }
        string win = Environment.GetEnvironmentVariable("WINDIR");
        if (!string.IsNullOrEmpty(win)) dirs.Add(Path.Combine(win, "Fonts"));
        var names = times ? new[] { "times.ttf", "Times New Roman.ttf", "arial.ttf" } : new[] { "arial.ttf", "Arial.ttf" };
        foreach (var d in dirs) foreach (var n in names) { var p = Path.Combine(d, n); if (File.Exists(p)) return p; }
        foreach (var p in new[] { "/usr/share/fonts/truetype/msttcorefonts/Arial.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
                                  "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/Library/Fonts/Arial.ttf", "/System/Library/Fonts/Supplemental/Arial.ttf" })
            if (File.Exists(p)) return p;
        return null;
    }

    // ------------------------------------------------------------------
    // Phông TrueType: cmap (Unicode → glyph), độ rộng glyph
    // ------------------------------------------------------------------
    private sealed class Ttf
    {
        public byte[] Data;
        public int UnitsPerEm = 1000, NumH, NumGlyphs;
        public int[] Adv;
        public Dictionary<int, int> Cmap = new();
        public int Ascent = 900, Descent = -200, CapH = 716;
        public string Name = "ArialMT";

        private int U16(int o) => (Data[o] << 8) | Data[o + 1];
        private short S16(int o) => (short)U16(o);
        private uint U32(int o) => (uint)((Data[o] << 24) | (Data[o + 1] << 16) | (Data[o + 2] << 8) | Data[o + 3]);

        public static Ttf Load(string path)
        {
            try
            {
                var t = new Ttf { Data = File.ReadAllBytes(path) };
                t.Parse();
                return t.Cmap.Count > 0 && t.Adv != null ? t : null;
            }
            catch (Exception) { return null; }
        }

        private void Parse()
        {
            if (U32(0) == 0x74746366) throw new InvalidDataException("ttc");   // bộ sưu tập phông: không hỗ trợ
            int n = U16(4);
            var tab = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                int r = 12 + 16 * i;
                tab[Encoding.ASCII.GetString(Data, r, 4)] = (int)U32(r + 8);
            }
            int head = tab["head"], hhea = tab["hhea"], hmtx = tab["hmtx"], maxp = tab["maxp"], cmap = tab["cmap"];
            UnitsPerEm = U16(head + 18);
            NumGlyphs = U16(maxp + 4);
            NumH = U16(hhea + 34);
            Ascent = S16(hhea + 4); Descent = S16(hhea + 6);
            if (tab.TryGetValue("OS/2", out int os2) && U16(os2) >= 2) CapH = S16(os2 + 88);
            else CapH = (int)(0.716 * UnitsPerEm);
            Adv = new int[NumGlyphs];
            int last = 0;
            for (int g = 0; g < NumGlyphs; g++) { if (g < NumH) last = U16(hmtx + 4 * g); Adv[g] = last; }
            // cmap: ưu tiên (3,10) định dạng 12, rồi (3,1)/(0,x) định dạng 4
            int nt = U16(cmap + 2), best4 = -1, best12 = -1;
            for (int i = 0; i < nt; i++)
            {
                int r = cmap + 4 + 8 * i, pid = U16(r), eid = U16(r + 2), off = cmap + (int)U32(r + 4);
                int fmt = U16(off);
                if (fmt == 12 && (pid == 3 && eid == 10 || pid == 0)) best12 = off;
                if (fmt == 4 && (pid == 3 && eid == 1 || pid == 0)) best4 = off;
            }
            if (best12 >= 0)
            {
                uint groups = U32(best12 + 12);
                for (uint gi = 0; gi < groups; gi++)
                {
                    int r = best12 + 16 + (int)gi * 12;
                    uint a = U32(r), b = U32(r + 4), g0 = U32(r + 8);
                    for (uint c = a; c <= b && c <= 0xFFFF; c++) Cmap[(int)c] = (int)(g0 + c - a);
                }
            }
            else if (best4 >= 0)
            {
                int seg = U16(best4 + 6) / 2;
                int ends = best4 + 14, starts = ends + 2 * seg + 2, deltas = starts + 2 * seg, ranges = deltas + 2 * seg;
                for (int s = 0; s < seg; s++)
                {
                    int e = U16(ends + 2 * s), st = U16(starts + 2 * s), dl = S16(deltas + 2 * s), ro = U16(ranges + 2 * s);
                    for (int c = st; c <= e && c != 0xFFFF; c++)
                    {
                        int g;
                        if (ro == 0) g = (c + dl) & 0xFFFF;
                        else
                        {
                            int addr = ranges + 2 * s + ro + 2 * (c - st);
                            g = addr + 1 < Data.Length ? U16(addr) : 0;
                            if (g != 0) g = (g + dl) & 0xFFFF;
                        }
                        if (g != 0 && g < NumGlyphs) Cmap[c] = g;
                    }
                }
            }
            // tên PostScript (bảng name, mã 6) – chỉ để ghi /BaseFont
            if (tab.TryGetValue("name", out int nm))
            {
                int cnt = U16(nm + 2), so = nm + U16(nm + 4);
                for (int i = 0; i < cnt; i++)
                {
                    int r = nm + 6 + 12 * i;
                    if (U16(r + 6) != 6) continue;
                    int pid = U16(r), len = U16(r + 8), off = so + U16(r + 10);
                    string s = pid == 3 || pid == 0 ? Encoding.BigEndianUnicode.GetString(Data, off, len) : Encoding.ASCII.GetString(Data, off, len);
                    s = new string(s.Where(ch => ch > 32 && ch < 127 && ch != '/' && ch != '(' && ch != ')' && ch != '[' && ch != ']' && ch != '<' && ch != '>').ToArray());
                    if (s.Length > 0) { Name = s; break; }
                }
            }
        }

        public int Glyph(char c)
        {
            if (Cmap.TryGetValue(c, out int g)) return g;
            // ký tự tổ hợp không có sẵn: thử chữ gốc (bỏ dấu)
            string d = c.ToString().Normalize(NormalizationForm.FormD);
            return d.Length > 0 && Cmap.TryGetValue(d[0], out g) ? g : 0;
        }
    }

    // ------------------------------------------------------------------
    // Ghi PDF
    // ------------------------------------------------------------------
    /// <summary>Ghi PDF. pages: (bản vẽ, hệ số K: toạ độ mô hình × K = mm giấy, khổ rộng mm, khổ cao mm).
    /// Gốc toạ độ bản vẽ = góc trái dưới trang. monochrome: mọi nét màu đen.</summary>
    public static void Write(string path, IList<(DxfWriter W, double K, double WMm, double HMm)> pages, string title, bool monochrome = false)
    {
        using var fs = File.Create(path);
        Write(fs, pages, title, monochrome);
    }

    public static void Write(Stream fs, IList<(DxfWriter W, double K, double WMm, double HMm)> pages, string title, bool monochrome = false)
    {
        string fp = FindFont();
        var ttf = fp == null ? null : Ttf.Load(fp);
        var used = new SortedDictionary<int, char>();
        var offsets = new SortedDictionary<int, long>();
        void W(string s) { var b = Encoding.ASCII.GetBytes(s); fs.Write(b, 0, b.Length); }
        void Obj(int id) { offsets[id] = fs.Position; W(id + " 0 obj\n"); }
        void PutStream(int id, byte[] raw, string extra = "")
        {
            byte[] z = Deflate(raw);
            Obj(id); W("<< /Length " + z.Length + " /Filter /FlateDecode" + extra + " >>\nstream\n");
            fs.Write(z, 0, z.Length);
            W("\nendstream\nendobj\n");
        }

        int n = pages.Count;
        // 1 catalog, 2 pages, 3 info, 4 phông (Type0 hoặc Helvetica), 5 CIDFont, 6 descriptor, 7 font file, 8 ToUnicode; trang từ 9: page, content
        W("%PDF-1.4\n");
        fs.Write(new byte[] { 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A }, 0, 6);
        Obj(1); W("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        Obj(2); W("<< /Type /Pages /Kids [" + string.Join(" ", Enumerable.Range(0, n).Select(i => (9 + 2 * i) + " 0 R")) + "] /Count " + n + " >>\nendobj\n");
        Obj(3); W("<< /Producer (Viet Const) /Title " + Utf16(title) + " >>\nendobj\n");
        for (int i = 0; i < n; i++)
        {
            var (dw, k, wmm, hmm) = pages[i];
            int pid = 9 + 2 * i, cid = pid + 1;
            Obj(pid);
            W("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + F(wmm * PtPerMm) + " " + F(hmm * PtPerMm) + "] /Resources << /Font << /F1 4 0 R >> >> /Contents " + cid + " 0 R >>\nendobj\n");
            string content = Content(dw, k, ttf, used, monochrome);
            PutStream(cid, Encoding.Latin1.GetBytes(content));
        }
        if (ttf != null)
        {
            Obj(4); W("<< /Type /Font /Subtype /Type0 /BaseFont /" + ttf.Name + " /Encoding /Identity-H /DescendantFonts [5 0 R] /ToUnicode 8 0 R >>\nendobj\n");
            var wsb = new StringBuilder();
            foreach (var g in used.Keys) wsb.Append(g).Append(" [").Append(Math.Round(ttf.Adv[g] * 1000.0 / ttf.UnitsPerEm)).Append("] ");
            Obj(5); W("<< /Type /Font /Subtype /CIDFontType2 /BaseFont /" + ttf.Name + " /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor 6 0 R /DW 556 /W [" + wsb + "] /CIDToGIDMap /Identity >>\nendobj\n");
            double sc = 1000.0 / ttf.UnitsPerEm;
            Obj(6); W("<< /Type /FontDescriptor /FontName /" + ttf.Name + " /Flags 32 /FontBBox [-665 -325 2000 1040] /ItalicAngle 0 /Ascent " + Math.Round(ttf.Ascent * sc) +
                      " /Descent " + Math.Round(ttf.Descent * sc) + " /CapHeight " + Math.Round(ttf.CapH * sc) + " /StemV 80 /FontFile2 7 0 R >>\nendobj\n");
            PutStream(7, ttf.Data, " /Length1 " + ttf.Data.Length);
            var cm = new StringBuilder("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n/CMapName /Adobe-Identity-UCS def\n/CMapType 2 def\n1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");
            var pairs = used.ToList();
            for (int s = 0; s < pairs.Count; s += 100)
            {
                var chunk = pairs.Skip(s).Take(100).ToList();
                cm.Append(chunk.Count).Append(" beginbfchar\n");
                foreach (var (g, ch) in chunk) cm.Append('<').Append(g.ToString("X4", Inv)).Append("> <").Append(((int)ch).ToString("X4", Inv)).Append(">\n");
                cm.Append("endbfchar\n");
            }
            cm.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n");
            PutStream(8, Encoding.ASCII.GetBytes(cm.ToString()));
        }
        else
        {
            Obj(4); W("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");
        }
        long xref = fs.Position;
        int size = offsets.Keys.Max() + 1;
        W("xref\n0 " + size + "\n0000000000 65535 f \n");
        for (int id = 1; id < size; id++)
            W((offsets.TryGetValue(id, out long o) ? o.ToString("0000000000", Inv) + " 00000 n " : "0000000000 65535 f ") + "\n");
        W("trailer\n<< /Size " + size + " /Root 1 0 R /Info 3 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
    }

    private static string F(double v) => (Math.Abs(v) < 1e-9 ? 0 : v).ToString("0.###", Inv);

    private static byte[] Deflate(byte[] raw)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(raw, 0, raw.Length);
        return ms.ToArray();
    }

    private static string Utf16(string s)
    {
        var sb = new StringBuilder("<FEFF");
        foreach (char c in s ?? "") sb.Append(((int)c).ToString("X4", Inv));
        return sb.Append('>').ToString();
    }

    /// <summary>Nội dung một trang (toán tử PDF), toạ độ pt.</summary>
    private static string Content(DxfWriter dw, double k, Ttf ttf, SortedDictionary<int, char> used, bool mono)
    {
        var sb = new StringBuilder();
        double K = k * PtPerMm;   // mô hình → pt
        string curStroke = null, curFill = null;
        void Pt(double x, double y) { sb.Append(F(x * K)).Append(' ').Append(F(y * K)).Append(' '); }
        void SetStroke(string layer, string ltype, double widthMm, bool thinHatch = false)
        {
            var (r, g, b, mm) = Style(layer);
            if (mono) r = g = b = 0;
            if (thinHatch) mm = Math.Min(mm, 0.13);
            double t = (widthMm > 0 ? widthMm : mm) * PtPerMm;
            string lt = !string.IsNullOrEmpty(ltype) ? ltype.ToUpperInvariant() : (layer == Lay.Truc ? "CENTER" : "");
            string dash = lt == "CENTER" ? "[" + F(10 * PtPerMm) + " " + F(1.6 * PtPerMm) + " " + F(1.6 * PtPerMm) + " " + F(1.6 * PtPerMm) + "] 0 d"
                        : lt == "DASHED" || lt == "HIDDEN" ? "[" + F(3.5 * PtPerMm) + " " + F(1.6 * PtPerMm) + "] 0 d" : "[] 0 d";
            string key = F(r) + " " + F(g) + " " + F(b) + " RG " + F(t) + " w " + dash;
            if (key != curStroke) { sb.Append(key).Append('\n'); curStroke = key; }
        }
        void SetFill(string layer)
        {
            var (r, g, b, _) = Style(layer);
            if (mono) r = g = b = 0;
            string key = F(r) + " " + F(g) + " " + F(b) + " rg";
            if (key != curFill) { sb.Append(key).Append('\n'); curFill = key; }
        }
        void ArcPath(double cx, double cy, double rr, double a1Deg, double sweepDeg, bool move)
        {
            int segs = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweepDeg) / 90 - 1e-9));
            double step = sweepDeg / segs * Math.PI / 180, a = a1Deg * Math.PI / 180;
            if (move) { Pt(cx + rr * Math.Cos(a), cy + rr * Math.Sin(a)); sb.Append("m "); }
            for (int i = 0; i < segs; i++)
            {
                double b = a + step, kk = 4.0 / 3.0 * Math.Tan(step / 4);
                Pt(cx + rr * (Math.Cos(a) - kk * Math.Sin(a)), cy + rr * (Math.Sin(a) + kk * Math.Cos(a)));
                Pt(cx + rr * (Math.Cos(b) + kk * Math.Sin(b)), cy + rr * (Math.Sin(b) - kk * Math.Cos(b)));
                Pt(cx + rr * Math.Cos(b), cy + rr * Math.Sin(b));
                sb.Append("c\n");
                a = b;
            }
        }

        var prims = dw.GetPrimitives();
        // miền tô vẽ trước (nằm dưới)
        foreach (var pr in prims.Where(p => p.Kind == PrimKind.Hatch))
        {
            var hd = DxfWriter.HatchOf(pr);
            if (hd == null || hd.Loops.Count == 0) continue;
            if (hd.IsSolid)
            {
                SetFill(pr.Layer);
                foreach (var l in hd.Loops)
                {
                    if (l.Count < 3) continue;
                    Pt(l[0].X, l[0].Y); sb.Append("m ");
                    for (int i = 1; i < l.Count; i++) { Pt(l[i].X, l[i].Y); sb.Append("l "); }
                    sb.Append("h\n");
                }
                sb.Append("f*\n");
            }
            else
            {
                SetStroke(pr.Layer, null, 0, true);
                foreach (var (x1, y1, x2, y2) in DxfWriter.HatchSegments(pr)) { Pt(x1, y1); sb.Append("m "); Pt(x2, y2); sb.Append("l S\n"); }
            }
        }
        var texts = new List<DrawPrim>();
        foreach (var pr in prims)
        {
            var a = pr.Pts;
            switch (pr.Kind)
            {
                case PrimKind.Line:
                    SetStroke(pr.Layer, pr.LType, 0);
                    Pt(a[0], a[1]); sb.Append("m "); Pt(a[2], a[3]); sb.Append("l S\n");
                    break;
                case PrimKind.Polyline:
                    if (a.Length < 4) break;
                    SetStroke(pr.Layer, pr.LType, pr.Width * k);
                    Pt(a[0], a[1]); sb.Append("m ");
                    for (int i = 2; i + 1 < a.Length; i += 2) { Pt(a[i], a[i + 1]); sb.Append("l "); }
                    sb.Append(pr.Closed ? "s\n" : "S\n");
                    break;
                case PrimKind.Circle:
                    if (pr.R <= 0) break;
                    SetStroke(pr.Layer, pr.LType, 0);
                    ArcPath(a[0], a[1], pr.R, 0, 360, true); sb.Append("S\n");
                    break;
                case PrimKind.Arc:
                {
                    if (pr.R <= 0) break;
                    double sweep = ((pr.A2 - pr.A1) % 360 + 360) % 360;
                    if (sweep < 1e-9) sweep = 360;
                    SetStroke(pr.Layer, pr.LType, 0);
                    ArcPath(a[0], a[1], pr.R, pr.A1, sweep, true); sb.Append("S\n");
                    break;
                }
                case PrimKind.Solid:
                    SetFill(pr.Layer);
                    Pt(a[0], a[1]); sb.Append("m "); Pt(a[2], a[3]); sb.Append("l "); Pt(a[4], a[5]); sb.Append("l "); Pt(a[6], a[7]); sb.Append("l h f\n");
                    break;
                case PrimKind.Text:
                    if (pr.H * k > 1e-4 && !string.IsNullOrEmpty(pr.Text)) texts.Add(pr);
                    break;
            }
        }
        // chữ
        foreach (var pr in texts)
        {
            var (r, g, b, _) = Style(pr.Layer);
            if (mono) r = g = b = 0;
            double cap = pr.H * K, em = cap / 0.716, wf = pr.WF > 0 ? pr.WF : 1;
            string hex; double wEm;
            if (ttf != null)
            {
                var h = new StringBuilder("<");
                double adv = 0;
                foreach (char ch in pr.Text)
                {
                    int gid = ttf.Glyph(ch);
                    if (!used.ContainsKey(gid)) used[gid] = ch;
                    adv += ttf.Adv[Math.Min(gid, ttf.Adv.Length - 1)];
                    h.Append(gid.ToString("X4", Inv));
                }
                hex = h.Append('>').ToString();
                wEm = adv / ttf.UnitsPerEm;
            }
            else
            {
                string plain = Plain(pr.Text);
                hex = "(" + plain.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)") + ")";
                wEm = plain.Length * 0.52;
            }
            double w = wEm * em * wf;
            double dx = pr.HA == 1 || pr.HA == 4 ? -w / 2 : pr.HA == 2 ? -w : 0;
            double dy = pr.VA switch { 1 => 0.21 * em, 2 => -cap / 2, 3 => -cap, _ => 0 };
            double rot = pr.Rot * Math.PI / 180, c = Math.Cos(rot), s = Math.Sin(rot);
            double x = pr.Pts[0] * K + c * dx - s * dy, y = pr.Pts[1] * K + s * dx + c * dy;
            sb.Append("BT ").Append(F(r)).Append(' ').Append(F(g)).Append(' ').Append(F(b)).Append(" rg /F1 ").Append(F(em)).Append(" Tf ")
              .Append(F(wf * c)).Append(' ').Append(F(wf * s)).Append(' ').Append(F(-s)).Append(' ').Append(F(c)).Append(' ').Append(F(x)).Append(' ').Append(F(y))
              .Append(" Tm ").Append(hex).Append(" Tj ET\n");
            curFill = null;
        }
        return sb.ToString();
    }

    /// <summary>Bỏ dấu tiếng Việt (khi không có phông TrueType): "Thép đai" → "Thep dai".</summary>
    public static string Plain(string s)
    {
        var sb = new StringBuilder();
        foreach (char ch in (s ?? "").Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            char o = ch switch { 'đ' => 'd', 'Đ' => 'D', _ => ch };
            // ngoài ASCII chỉ giữ ký tự có trong WinAnsi: Ø ø ° ² ³ ×
            if ((o < 32 || o > 126) && o is not ('Ø' or 'ø' or '°' or '²' or '³' or '×')) o = '?';
            sb.Append(o);
        }
        return sb.ToString();
    }
}

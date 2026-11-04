using System.Globalization;
using System.Text;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Dwg;
using KetCauVXT.Model;
using AcEnt = ACadSharp.Entities;
using AcIO = ACadSharp.IO;

namespace KetCauVXT.Tests;

/// <summary>Tô vật liệu mặt cắt (HATCH – Drawing/DxfWriter.Hatch.cs, KetCauVXT.Dwg/CadDocWriter.cs):
/// mô phỏng DXF R12 (SOLID → tam giác, ANSI31 → LINE cắt theo biên, lỗ, giới hạn số nét), ghép/phóng/cắt khung,
/// DXF 2018 / DWG 2018 qua điểm cắm CadFileWriters (HATCH thật, đọc lại bằng ACadSharp, chữ tiếng Việt),
/// thiết lập "Tô vật liệu" (tự động/tắt: DXF R12 giữ nguyên như cũ), mặt cắt dầm/móng có tô vật liệu.
/// File mẫu ghi vào outDir\Hatch (*.dxf R12 + 2018 – bước CI kiểm tra bằng ezdxf audit; *.dwg).</summary>
public static class HatchTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Console.WriteLine("\n=== TÔ VẬT LIỆU MẶT CẮT (HATCH): R12 MÔ PHỎNG, DXF 2018 / DWG ===");
        string dir = Path.Combine(outDir ?? Path.GetTempPath(), "Hatch");
        Directory.CreateDirectory(dir);
        var prevWriter = CadFileWriters.Writer;
        CadDocWriter.Register();
        try
        {
            using (CadStandard.Use(CadStandard.Default()))
                Main(check, dir);
        }
        catch (Exception ex)
        {
            Console.WriteLine("   LỖI: " + ex);
            check("Hatch: không lỗi ngoại lệ", 0, 1, 0);
        }
        finally { CadFileWriters.Writer = prevWriter; }
    }

    private static double B(bool v) => v ? 1 : 0;

    private static List<(double X, double Y)> Sq(double x1, double y1, double x2, double y2) =>
        new() { (x1, y1), (x2, y1), (x2, y2), (x1, y2) };

    /// <summary>Đọc các thực thể trong mục ENTITIES của chuỗi DXF ASCII: (loại, mã nhóm → giá trị).</summary>
    private static List<(string Type, List<(int Code, string Val)> Codes)> Entities(string dxf)
    {
        var res = new List<(string, List<(int, string)>)>();
        var lines = dxf.Replace("\r\n", "\n").Split('\n');
        bool inEnt = false;
        (string, List<(int, string)>)? cur = null;
        for (int i = 0; i + 1 < lines.Length; i += 2)
        {
            if (!int.TryParse(lines[i].Trim(), NumberStyles.Integer, Inv, out int code)) continue;
            string v = lines[i + 1].Trim();
            if (code == 2 && v == "ENTITIES") { inEnt = true; continue; }
            if (!inEnt) continue;
            if (code == 0)
            {
                if (cur != null) res.Add(cur.Value);
                if (v == "ENDSEC") { inEnt = false; cur = null; continue; }
                cur = (v, new List<(int, string)>());
                continue;
            }
            cur?.Item2.Add((code, v));
        }
        return res;
    }

    private static double D(List<(int Code, string Val)> c, int code) =>
        double.TryParse(c.FirstOrDefault(x => x.Code == code).Val ?? "0", NumberStyles.Float, Inv, out double d) ? d : 0;

    private static List<(double X1, double Y1, double X2, double Y2)> LinesOf(string dxf) =>
        Entities(dxf).Where(e => e.Type == "LINE").Select(e => (D(e.Codes, 10), D(e.Codes, 20), D(e.Codes, 11), D(e.Codes, 21))).ToList();

    private static void Main(Action<string, double, double, double> check, string dir)
    {
        const double eps = 1e-6;
        var std = CadStandard.Default();

        // ================= 1. R12: SOLID → tam giác SOLID =================
        {
            var w = new DxfWriter(1) { Standard = std };
            check("Hatch SOLID: ghi được miền tô", B(w.Hatch("SOLID", 1, 0, Lay.Hatch, Sq(0, 0, 1, 1))), 1, 0);
            check("Hatch SOLID: 1 thực thể HATCH trong bộ ghi", w.CountEntities("HATCH"), 1, 0);
            var ents = Entities(w.ToDxfString());
            check("R12: hình vuông SOLID → 2 tam giác SOLID", ents.Count(e => e.Type == "SOLID"), 2, 0);
            check("R12: không còn thực thể HATCH (R12 không có HATCH)", ents.Count(e => e.Type == "HATCH"), 0, 0);
            check("R12: SOLID trên lớp HATCH", B(ents.Where(e => e.Type == "SOLID").All(e => e.Codes.Any(c => c.Code == 8 && c.Val == "HATCH"))), 1, 0);
            check("R12: bảng LAYER có HATCH màu 8", B(w.ToDxfString().Contains("\r\n  2\r\nHATCH\r\n 70\r\n0\r\n 62\r\n8\r\n")), 1, 0);
            // lõm (chữ L) → cắt tai: n − 2 tam giác, diện tích đúng
            var wl = new DxfWriter(1) { Standard = std };
            var L = new List<(double X, double Y)> { (0, 0), (2, 0), (2, 1), (1, 1), (1, 2), (0, 2) };
            wl.Hatch("SOLID", 1, 0, Lay.Hatch, L);
            var pcs = DxfWriter.SolidPieces(new List<IList<(double X, double Y)>> { L });
            double area = pcs.Sum(p => Math.Abs(PolyArea(p)));
            check("SOLID chữ L: 4 tam giác (cắt tai)", pcs.Count, 4, 0);
            check("SOLID chữ L: tổng diện tích = 3", area, 3, 1e-9);
            // có lỗ → dải hình thang, không mảnh nào phủ tâm lỗ
            var hole = DxfWriter.SolidPieces(new List<IList<(double X, double Y)>> { Sq(0, 0, 10, 10), Sq(4, 4, 6, 6) });
            check("SOLID có lỗ: diện tích = 100 − 4", hole.Sum(p => Math.Abs(PolyArea(p))), 96, 1e-6);
            check("SOLID có lỗ: không mảnh nào chứa tâm lỗ", B(!hole.Any(p => Inside(p, 5, 5))), 1, 0);
        }

        // ================= 2. R12: ANSI31 → LINE trong biên =================
        {
            double s = 0.1;                       // khoảng cách nét = 3,175 × tỷ lệ
            var w = new DxfWriter(1) { Standard = std };
            w.Hatch("ANSI31", s / 3.175, 0, Lay.Hatch, Sq(0, 0, 1, 1));
            var ls = LinesOf(w.ToDxfString());
            double expect = Math.Sqrt(2) / s;     // số nét 45° cắt hình vuông 1×1 ≈ √2 / s
            check("ANSI31 1×1, s = 0,1: số LINE ≈ √2/s (±1)", ls.Count, Math.Round(expect), 1.01);
            check("ANSI31: mọi nét nằm trong biên", B(ls.All(l => In01(l.X1, eps) && In01(l.Y1, eps) && In01(l.X2, eps) && In01(l.Y2, eps))), 1, 0);
            check("ANSI31: mọi nét nghiêng 45°", B(ls.All(l => Math.Abs(Math.Abs(l.X2 - l.X1) - Math.Abs(l.Y2 - l.Y1)) < 1e-6)), 1, 0);
            // nét dài nhất = đường chéo
            check("ANSI31: nét dài nhất ≈ đường chéo √2", ls.Max(l => Math.Sqrt((l.X2 - l.X1) * (l.X2 - l.X1) + (l.Y2 - l.Y1) * (l.Y2 - l.Y1))), Math.Sqrt(2), 0.15);
            // góc mẫu 45° → nét đứng/ngang (45 + 45 = 90°)
            var w90 = new DxfWriter(1) { Standard = std };
            w90.Hatch("ANSI31", s / 3.175, 45, Lay.Hatch, Sq(0, 0, 1, 1));
            var l90 = LinesOf(w90.ToDxfString());
            check("ANSI31 xoay 45°: nét đứng", B(l90.Count > 0 && l90.All(l => Math.Abs(l.X2 - l.X1) < 1e-6)), 1, 0);
            check("ANSI31 xoay 45°: số nét ≈ 1/s", l90.Count, 1 / s, 1.01);
        }

        // ================= 3. Lỗ: không nét nào trong lỗ =================
        {
            var w = new DxfWriter(1) { Standard = std };
            w.Hatch(new List<IList<(double X, double Y)>> { Sq(0, 0, 10, 10), Sq(4, 4, 6, 6) }, "ANSI31", 0.25 / 3.175, 0, Lay.Hatch);
            var ls = LinesOf(w.ToDxfString());
            bool InHole(double x, double y) => x > 4 + 1e-6 && x < 6 - 1e-6 && y > 4 + 1e-6 && y < 6 - 1e-6;
            bool none = ls.All(l => !new[] { 0.1, 0.25, 0.5, 0.75, 0.9 }.Any(t => InHole(l.X1 + t * (l.X2 - l.X1), l.Y1 + t * (l.Y2 - l.Y1))));
            check("Hatch có lỗ: không đoạn nào đi qua lỗ", B(none), 1, 0);
            check("Hatch có lỗ: có nét cả hai phía lỗ", B(ls.Any(l => Math.Max(l.X1, l.X2) < 4) && ls.Any(l => Math.Min(l.X1, l.X2) > 6)), 1, 0);
            check("Hatch có lỗ: mọi nét trong biên ngoài", B(ls.All(l => l.X1 >= -eps && l.X2 <= 10 + eps && Math.Min(l.Y1, l.Y2) >= -eps && Math.Max(l.Y1, l.Y2) <= 10 + eps)), 1, 0);
            // nét bị lỗ cắt đôi: tổng chiều dài < không lỗ
            var w0 = new DxfWriter(1) { Standard = std };
            w0.Hatch("ANSI31", 0.25 / 3.175, 0, Lay.Hatch, Sq(0, 0, 10, 10));
            double Len(List<(double X1, double Y1, double X2, double Y2)> x) => x.Sum(l => Math.Sqrt((l.X2 - l.X1) * (l.X2 - l.X1) + (l.Y2 - l.Y1) * (l.Y2 - l.Y1)));
            double l0 = Len(LinesOf(w0.ToDxfString())), l1 = Len(ls);
            check("Hatch có lỗ: tổng chiều dài nét giảm ≈ theo diện tích lỗ (4%)", l1 / l0, 0.96, 0.01);
        }

        // ================= 4. Giới hạn số nét =================
        {
            int prev = DxfWriter.MaxHatchSegments;
            try
            {
                DxfWriter.MaxHatchSegments = 200;
                var w = new DxfWriter(1) { Standard = std };
                w.Hatch("ANSI31", 1 / 3.175, 0, Lay.Hatch, Sq(0, 0, 1000, 1000));   // cần ~1400 nét
                int n = LinesOf(w.ToDxfString()).Count;
                check("Giới hạn nét: số LINE ≤ MaxHatchSegments (200)", B(n > 0 && n <= 200), 1, 0);
                var segs = DxfWriter.PatternSegments(new List<IList<(double X, double Y)>> { Sq(0, 0, 1000, 1000) }, "ANSI31", 1 / 3.175, 0, 200, out double used);
                check("Giới hạn nét: mẫu tự giãn thưa (tỷ lệ tăng)", B(used > 1 / 3.175 * 1.5 && segs.Count <= 200), 1, 0);
                var wc = new DxfWriter(1) { Standard = std };
                wc.Hatch("AR-CONC", 1, 0, Lay.Hatch, Sq(0, 0, 5000, 5000));
                check("Giới hạn nét: AR-CONC miền lớn vẫn ≤ giới hạn", B(wc.HatchR12EntityCount() <= 200 && wc.HatchR12EntityCount() > 0), 1, 0);
            }
            finally { DxfWriter.MaxHatchSegments = prev; }
        }

        // ================= 5. Mẫu kiến trúc, ghép / phóng / cắt khung, nét cơ bản =================
        {
            foreach (var pat in new[] { "AR-CONC", "EARTH", "AR-SAND", "BRICK", "ANSI37" })
            {
                var w = new DxfWriter(1) { Standard = std };
                w.Hatch(pat, 2, 0, Lay.Hatch, Sq(0, 0, 60, 40));
                var ls = LinesOf(w.ToDxfString());
                check($"Mẫu {pat}: có nét, trong biên", B(ls.Count > 3 && ls.All(l => Math.Min(l.X1, l.X2) >= -eps && Math.Max(l.X1, l.X2) <= 60 + eps && Math.Min(l.Y1, l.Y2) >= -eps && Math.Max(l.Y1, l.Y2) <= 40 + eps)), 1, 0);
            }
            var f = new DxfWriter(25) { Standard = std };
            f.Hatch("ANSI31", 25, 0, Lay.Hatch, Sq(0, 0, 100, 50));
            var g = new DxfWriter(50) { Standard = std };
            g.AppendScaled(f, 2, 1000, 0);
            var h = g.Hatches();
            check("Ghép phóng ×2: còn 1 HATCH", h.Count, 1, 0);
            check("Ghép phóng ×2: tỷ lệ mẫu ×2", h.Count == 1 ? h[0].PatScale : 0, 50, 1e-9);
            check("Ghép phóng ×2: đỉnh biên dời đúng", h.Count == 1 ? h[0].Loops[0].Max(p => p.X) : 0, 1200, 1e-9);
            var c = new DxfWriter(50) { Standard = std };
            c.AppendScaledClipped(f, 1, 0, 0, new Rect2(50, -10, 200, 100));
            var hc = c.Hatches();
            check("Ghép cắt khung: miền tô bị cắt theo khung (x ≥ 50)", B(hc.Count == 1 && hc[0].Loops[0].All(p => p.X >= 50 - eps) && Math.Abs(PolyArea(hc[0].Loops[0].ToArray()) - 2500) < 1e-6), 1, 0);
            var prims = f.GetPrimitives().Where(p => p.Kind == PrimKind.Hatch).ToList();
            check("Nét cơ bản: PrimKind.Hatch (màn hình/in)", prims.Count, 1, 0);
            check("Nét cơ bản: nét mẫu màn hình = nét R12", prims.Count == 1 ? DxfWriter.HatchSegments(prims[0]).Count : -1, f.HatchR12EntityCount(), 0);
            var keys = f.EntityRefs();
            check("Khoá thực thể: HATCH có khoá ổn định", B(keys.Count == 1 && !string.IsNullOrEmpty(keys[0].Key) && keys[0].Key.StartsWith("HATCH|", StringComparison.Ordinal)), 1, 0);
            var cl = f.Clone();
            cl.MoveEntity(0, 10, 0);
            check("Hiệu chỉnh: dời miền tô dời cả biên", cl.Hatches()[0].Loops[0].Min(p => p.X), 10, 1e-9);
        }

        // ================= 6. DXF 2018 / DWG 2018 qua điểm cắm =================
        string viet = "MẶT CẮT 1-1 – Bê tông đá 1×2, mác 250; Ø16a150 ±0.000";
        {
            var w = SampleWriter(std, viet);
            string p18 = CadFileWriters.Write(w, Path.Combine(dir, "Hatch_2018.dxf"), CadOutputFormat.Dxf2018);
            check("DXF 2018: ghi được, đuôi .dxf", B(File.Exists(p18) && p18.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase)), 1, 0);
            string raw = File.ReadAllText(p18, Encoding.UTF8);
            check("DXF 2018: $ACADVER AC1032", B(raw.Contains("AC1032")), 1, 0);
            check("DXF 2018: chữ tiếng Việt ghi thẳng UTF-8 (không \\U+)", B(raw.Contains("MẶT CẮT 1-1") && !raw.Contains("\\U+")), 1, 0);
            var doc = AcIO.DxfReader.Read(p18);
            var hs = doc.Entities.OfType<AcEnt.Hatch>().ToList();
            check("DXF 2018: đọc lại 4 HATCH", hs.Count, 4, 0);
            var names = hs.Select(x => x.Pattern?.Name ?? "").OrderBy(x => x, StringComparer.Ordinal).ToList();
            check("DXF 2018: tên mẫu AR-CONC, ANSI31, EARTH, SOLID", B(string.Join(",", names) == "ANSI31,AR-CONC,EARTH,SOLID"), 1, 0);
            check("DXF 2018: HATCH SOLID là tô đặc, mẫu khác có nét định nghĩa", B(hs.All(x => x.IsSolid == (x.Pattern?.Name == "SOLID")) && hs.Where(x => !x.IsSolid).All(x => x.Pattern.Lines.Count > 0)), 1, 0);
            var ansi = hs.FirstOrDefault(x => x.Pattern?.Name == "ANSI31");
            check("DXF 2018: ANSI31 tỷ lệ mẫu giữ đúng", ansi?.PatternScale ?? 0, 25, 1e-6);
            check("DXF 2018: HATCH có lỗ (2 vòng biên)", hs.Max(x => x.Paths.Count), 2, 0);
            check("DXF 2018: HATCH trên lớp HATCH", B(hs.All(x => x.Layer?.Name == "HATCH")), 1, 0);
            check("DXF 2018: lớp HATCH màu 8", doc.Layers.TryGetValue("HATCH", out var hl) ? hl.Color.Index : -1, 8, 0);
            var txt = doc.Entities.OfType<AcEnt.TextEntity>().Select(t => t.Value).ToList();
            check("DXF 2018: chữ tiếng Việt đọc lại đúng", B(txt.Contains(viet)), 1, 0);
            check("DXF 2018: có LINE, LWPOLYLINE, CIRCLE, ARC, SOLID", B(doc.Entities.OfType<AcEnt.Line>().Any() && doc.Entities.OfType<AcEnt.LwPolyline>().Any()
                && doc.Entities.OfType<AcEnt.Circle>().Any() && doc.Entities.OfType<AcEnt.Arc>().Any() && doc.Entities.OfType<AcEnt.Solid>().Any()), 1, 0);
            check("DXF 2018: lớp BE_TONG, THEP theo chuẩn", B(doc.Layers.Contains("BE_TONG") && doc.Layers.TryGetValue("THEP", out var tl) && tl.Color.Index == 1), 1, 0);
            check("DXF 2018: có kiểu kích thước VXT_1-25", B(doc.DimensionStyles.Contains("VXT_1-25")), 1, 0);

            string pdwg = CadFileWriters.Write(w, Path.Combine(dir, "Hatch_2018.dxf"), CadOutputFormat.Dwg2018);
            check("DWG 2018: đổi đuôi .dwg", B(pdwg.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) && File.Exists(pdwg)), 1, 0);
            check("DWG 2018: chữ ký AC1032", B(DwgReader.ReadSignature(pdwg) == "AC1032"), 1, 0);
            var dd = AcIO.DwgReader.Read(pdwg);
            var dh = dd.Entities.OfType<AcEnt.Hatch>().ToList();
            check("DWG 2018: đọc lại 4 HATCH", dh.Count, 4, 0);
            check("DWG 2018: tên mẫu giữ đúng", B(string.Join(",", dh.Select(x => x.Pattern?.Name ?? "").OrderBy(x => x, StringComparer.Ordinal)) == "ANSI31,AR-CONC,EARTH,SOLID"), 1, 0);
            check("DWG 2018: chữ tiếng Việt đọc lại đúng", B(dd.Entities.OfType<AcEnt.TextEntity>().Any(t => t.Value == viet)), 1, 0);
            check("DWG 2018: đọc lại qua bộ đọc của chương trình", B(DwgReader.Read(pdwg, 0) is { } rd && rd.Entities.Count > 0), 1, 0);

            // R12 của cùng bản vẽ (mô phỏng) – mẫu cho CI ezdxf audit
            string p12 = CadFileWriters.Write(w, Path.Combine(dir, "Hatch_MoPhong_R12.dxf"), CadOutputFormat.DxfR12);
            var e12 = Entities(File.ReadAllText(p12, Encoding.UTF8));
            check("R12 mẫu: không có HATCH, có LINE + SOLID trên lớp HATCH", B(!e12.Any(e => e.Type == "HATCH")
                && e12.Count(e => e.Type == "LINE" && e.Codes.Any(c => c.Code == 8 && c.Val == "HATCH")) > 10
                && e12.Any(e => e.Type == "SOLID" && e.Codes.Any(c => c.Code == 8 && c.Val == "HATCH"))), 1, 0);

            // không có bộ ghi ngoài → R12
            var hook = CadFileWriters.Writer;
            CadFileWriters.Writer = null;
            try
            {
                string pf = CadFileWriters.Write(w, Path.Combine(Path.GetTempPath(), "kcvxt_hatch_fallback.dwg"), CadOutputFormat.Dwg2018);
                check("Chưa đăng ký bộ ghi: DWG → ghi DXF R12", B(pf.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) && File.ReadAllText(pf).Contains("AC1009")), 1, 0);
            }
            finally { CadFileWriters.Writer = hook; }
        }

        // ================= 7. Thiết lập "Tô vật liệu": R12 mặc định giữ nguyên =================
        {
            var p = ModelBuilder.NewProject("Hatch_Mau");
            ModelBuilder.SetGrid(p, new[] { 4.0, 4.0 }, new[] { 5.0 });
            ModelBuilder.SetStories(p, new[] { 3.6 }, -0.6);
            ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x30" });
            var r = FrameAnalysis.Run(p);
            var d = ConcreteDesigner.Design(p, r);
            var beam = d.Beams.First(b => b.Bars.Count > 0);
            List<Figure> Figs(HatchMode m, CadOutputFormat? fmt = null)
            {
                var st = new DrawingStyle { HatchSections = m };
                using (CadFileWriters.UseFormat(fmt))
                using (DrawingStyle.Use(st))
                    return BeamDrawing.Build(p, beam, d).Concat(FootingDrawing.BuildAll(p, d)).ToList();
            }
            int Cnt(List<Figure> f, string type) => f.Sum(x => x.W.CountEntities(type));
            string Hash(List<Figure> f) => string.Join("|", f.Select(x => x.W.ContentHash()));
            var fDef = Figs(HatchMode.Auto);   // mặc định: tự động + DXF R12
            var fOff = Figs(HatchMode.Off);
            var fOn = Figs(HatchMode.On);
            var fAuto18 = Figs(HatchMode.Auto, CadOutputFormat.Dxf2018);
            check("Mặc định (tự động, DXF R12): không có HATCH", Cnt(fDef, "HATCH"), 0, 0);
            check("Mặc định = Tắt: bản vẽ giống hệt (mã băm)", B(Hash(fDef) == Hash(fOff)), 1, 0);
            check("Mặc định: số thực thể không đổi so với Tắt", fDef.Sum(x => x.W.EntityCount), fOff.Sum(x => x.W.EntityCount), 0);
            check("Bật: có HATCH trên mặt cắt dầm + móng", B(Cnt(fOn, "HATCH") >= 3), 1, 0);
            check("Bật: số thực thể khác HATCH giữ nguyên (không đổi bố cục)", fOn.Sum(x => x.W.EntityCount) - Cnt(fOn, "HATCH"), fOff.Sum(x => x.W.EntityCount), 0);
            check("Tự động + DXF 2018: có HATCH", B(Cnt(fAuto18, "HATCH") == Cnt(fOn, "HATCH")), 1, 0);
            var pats = fOn.SelectMany(x => x.W.Hatches()).Select(x => x.Pattern).Distinct().ToList();
            check("Bật: mẫu AR-CONC (bê tông), ANSI31 (bê tông lót), EARTH (đất)", B(pats.Contains("AR-CONC") && pats.Contains("ANSI31") && pats.Contains("EARTH")), 1, 0);
            check("Bật: HATCH trên lớp HATCH", B(fOn.SelectMany(x => x.W.Hatches()).All(x => x.Layer == Lay.Hatch)), 1, 0);

            // ghép thành bản mẫu mặt cắt (dầm + móng) – R12 mô phỏng, DXF 2018, DWG
            var all = new DxfWriter(fOn.Max(x => x.W.Scale)) { Standard = std };
            double x0 = 0;
            foreach (var fg in fOn)
            {
                var ex = fg.W.Extents;
                if (ex.IsEmpty) continue;
                all.Append(fg.W, x0 - ex.X1, -ex.Y1);
                x0 += ex.W + 20 * all.Scale;
            }
            string m12 = CadFileWriters.Write(all, Path.Combine(dir, "MatCat_Hatch_R12.dxf"), CadOutputFormat.DxfR12);
            string m18 = CadFileWriters.Write(all, Path.Combine(dir, "MatCat_Hatch_2018.dxf"), CadOutputFormat.Dxf2018);
            string mdw = CadFileWriters.Write(all, Path.Combine(dir, "MatCat_Hatch.dxf"), CadOutputFormat.Dwg2018);
            var md = AcIO.DxfReader.Read(m18);
            check("Mặt cắt DXF 2018: số HATCH = số miền tô", md.Entities.OfType<AcEnt.Hatch>().Count(), all.CountEntities("HATCH"), 0);
            check("Mặt cắt DXF 2018: số chữ = số TEXT của bộ ghi", md.Entities.OfType<AcEnt.TextEntity>().Count(), all.CountEntities("TEXT"), 0);
            check("Mặt cắt DWG: đọc lại số HATCH", AcIO.DwgReader.Read(mdw).Entities.OfType<AcEnt.Hatch>().Count(), all.CountEntities("HATCH"), 0);
            long s12 = new FileInfo(m12).Length, s18 = new FileInfo(m18).Length, sdw = new FileInfo(mdw).Length;
            Console.WriteLine($"   · MatCat: R12 {s12 / 1024} KB ({Entities(File.ReadAllText(m12)).Count} thực thể), DXF 2018 {s18 / 1024} KB, DWG {sdw / 1024} KB");
            check("Mặt cắt: file R12 / 2018 / DWG đều có nội dung", B(s12 > 1000 && s18 > 1000 && sdw > 1000), 1, 0);

            // xuất bộ bản vẽ theo định dạng chọn (DrawingExportOptions.Format) → .dwg
            var ex1 = DrawingSet.Export(p, d, Path.Combine(Path.GetTempPath(), "kcvxt_hatch_set"), new DrawingExportOptions { Mode = DrawingExportMode.Single, SingleFileName = "BoBanVe", Format = CadOutputFormat.Dwg2018 });
            check("Xuất bộ bản vẽ DWG: 1 file .dwg", B(ex1.Files.Count == 1 && ex1.Files[0].EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) && File.Exists(ex1.Files[0])), 1, 0);
            check("Xuất bộ bản vẽ DWG (tự động): có HATCH", ex1.Files.Count == 1 ? B(AcIO.DwgReader.Read(ex1.Files[0]).Entities.OfType<AcEnt.Hatch>().Any()) : 0, 1, 0);
        }

        // ================= 8. Thiết lập & chuẩn: JSON =================
        {
            var s1 = CadStandard.Default();
            check("Chuẩn mặc định: định dạng DXF R12", B(s1.OutputFormat == CadOutputFormat.DxfR12 && s1.Drawing.HatchSections == HatchMode.Auto), 1, 0);
            s1.OutputFormat = CadOutputFormat.Dwg2018;
            s1.Drawing.HatchSections = HatchMode.On;
            s1.Drawing.HatchScaleFactor = 1.5;
            var back = CadStandard.FromJson(s1.ToJson());
            check("JSON: giữ định dạng xuất + tô vật liệu", B(back.OutputFormat == CadOutputFormat.Dwg2018 && back.Drawing.HatchSections == HatchMode.On && Math.Abs(back.Drawing.HatchScaleFactor - 1.5) < 1e-12), 1, 0);
            var old = CadStandard.FromJson("{\"Name\":\"cu\"}");
            check("JSON cũ: định dạng R12, tô vật liệu tự động", B(old.OutputFormat == CadOutputFormat.DxfR12 && old.Drawing.HatchSections == HatchMode.Auto && old.OutputLayers.Count == 9), 1, 0);
            using (CadStandard.Use(back))
                check("Chuẩn DWG đang áp dụng: CadFileWriters.Current = DWG", B(CadFileWriters.Current == CadOutputFormat.Dwg2018 && back.Drawing.HatchEnabled), 1, 0);
            check("Mẫu tự định nghĩa: bảng góc/khoảng cách/nét", B(HatchPatterns.Custom("VXT_TEST", (30, 2.0, null), (120, 4.0, new[] { 1.0, -1.0 })).Lines.Length == 2 && HatchPatterns.Has("vxt_test")), 1, 0);
        }
    }

    /// <summary>Bản vẽ mẫu: 4 miền tô (SOLID, ANSI31 có lỗ, AR-CONC, EARTH), nét, đa tuyến, tròn, cung, SOLID, chữ tiếng Việt, kích thước.</summary>
    private static DxfWriter SampleWriter(CadStandard std, string viet)
    {
        var w = new DxfWriter(25) { Standard = std };
        w.Hatch("SOLID", 1, 0, Lay.Hatch, Sq(0, 0, 400, 300));
        w.Hatch(new List<IList<(double X, double Y)>> { Sq(600, 0, 1400, 600), Sq(800, 200, 1200, 400) }, "ANSI31", 25, 0, Lay.Hatch);
        w.Hatch("AR-CONC", 25, 0, Lay.Hatch, new List<(double X, double Y)> { (1600, 0), (2400, 0), (2400, 500), (2000, 800), (1600, 500) });
        w.Hatch("EARTH", 12.5, 45, Lay.Hatch, Sq(0, -400, 2400, -200));
        w.Rect(Lay.BeTong, 600, 0, 1400, 600);
        w.Polyline(Lay.Dai, Sq(630, 30, 1370, 570), true, 0.25 * 25);
        w.Line(Lay.Truc, 1000, -500, 1000, 900, "CENTER");
        w.Circle(Lay.KyHieu, 2000, 1200, 140);
        w.Arc(Lay.KyHieu, 2000, 1200, 200, 0, 135);
        w.Dot(Lay.Thep, 700, 100, 8);
        w.Text(Lay.Chu, 1000, 1000, w.Th, viet, 0, 1, 0);
        w.Text(Lay.Chu, 100, 1000, w.Th, "Cốt thép Ø6a200 ①", 90, 0, 0);
        w.Dim(600, 0, 1400, 0, -300);
        return w;
    }

    private static bool In01(double v, double eps) => v >= -eps && v <= 1 + eps;

    private static double PolyArea((double X, double Y)[] p)
    {
        double a = 0;
        for (int i = 0, j = p.Length - 1; i < p.Length; j = i++) a += p[j].X * p[i].Y - p[i].X * p[j].Y;
        return a / 2;
    }

    private static bool Inside((double X, double Y)[] p, double x, double y)
    {
        bool c = false;
        for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            if ((p[i].Y > y) != (p[j].Y > y) && x < (p[j].X - p[i].X) * (y - p[i].Y) / (p[j].Y - p[i].Y) + p[i].X) c = !c;
        return c;
    }
}

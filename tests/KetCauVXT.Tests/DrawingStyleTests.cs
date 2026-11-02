using System.Globalization;
using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Kiểm tra Thiết lập bản vẽ (Model/DrawingStyle.cs): mặc định giữ đúng cỡ chữ cũ, đổi cỡ chữ kích thước chỉ đổi chữ kích thước,
/// đổi tỷ lệ mặt đứng dầm, bật/tắt chú thích, mỗi bản vẽ đúng một khối GHI CHÚ CHUNG, kiểu kích thước VXT_1-…, JSON.</summary>
public static class DrawingStyleTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static double B(bool b) => b ? 1 : 0;

    /// <summary>Phạm vi x (mm mô hình) của các nét trên lớp layer.</summary>
    private static (double X1, double X2) RangeX(DxfWriter w, string layer)
    {
        double a = double.MaxValue, b = double.MinValue;
        foreach (var pr in w.GetPrimitives().Where(q => q.Layer == layer && q.Kind is PrimKind.Line or PrimKind.Polyline))
            for (int i = 0; i + 1 < pr.Pts.Length; i += 2) { a = Math.Min(a, pr.Pts[i]); b = Math.Max(b, pr.Pts[i]); }
        return (a, b);
    }

    /// <summary>Các cặp (mã, giá trị) của file DXF.</summary>
    private static List<(int Code, string Val)> Pairs(string dxf)
    {
        var lines = dxf.Replace("\r", "").Split('\n');
        var res = new List<(int, string)>();
        for (int i = 0; i + 1 < lines.Length; i += 2)
            if (int.TryParse(lines[i].Trim(), out int c)) res.Add((c, lines[i + 1]));
        return res;
    }

    /// <summary>Tên DIMSTYLE → DIMSCALE (mã 40) trong bảng DIMSTYLE.</summary>
    private static Dictionary<string, double> DimStyles(string dxf)
    {
        var p = Pairs(dxf);
        var res = new Dictionary<string, double>(StringComparer.Ordinal);
        for (int i = 0; i < p.Count; i++)
        {
            if (p[i].Code != 0 || p[i].Val != "DIMSTYLE") continue;
            string name = null; double sc = double.NaN;
            for (int j = i + 1; j < p.Count && p[j].Code != 0; j++)
            {
                if (p[j].Code == 2) name = p[j].Val;
                else if (p[j].Code == 40) double.TryParse(p[j].Val, NumberStyles.Float, Inv, out sc);
            }
            if (name != null) res[name] = sc;
        }
        return res;
    }

    private static int CountOf(string text, string what)
    {
        int n = 0, i = 0;
        while ((i = text.IndexOf(what, i, StringComparison.Ordinal)) >= 0) { n++; i += what.Length; }
        return n;
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        var p = ModelBuilder.NewProject("Thiết lập bản vẽ");
        p.Location = "Thái Nguyên";
        ModelBuilder.SetGrid(p, new[] { 4.5, 5.0 }, new[] { 4.2, 4.2 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, -0.6);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions
        {
            ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5
        });
        var r = FrameAnalysis.Run(p);
        var d = ConcreteDesigner.Design(p, r);
        var beam = d.Beams.FirstOrDefault(b => b.Spans.Count > 1) ?? d.Beams.First();
        var def = DrawingStyle.Default();

        // ---------------- 1. mặc định = đúng cỡ chữ cũ ----------------
        List<Figure> figs0;
        using (DrawingStyle.Use(def)) figs0 = BeamDrawing.Build(p, beam);
        var el0 = figs0[0].W;
        var h0 = el0.TextHeights();
        // trước khi có thiết lập: chữ 2,5×50 = 125, số trong vòng 0,9×125, đầu trục/vạch cắt 1,2×125, tên hình 1,4×125
        var allowed = new[] { 112.5, 125, 150, 175 };
        check("Thiết lập mặc định: mọi chữ mặt đứng dầm có cỡ cũ (112,5/125/150/175)", h0.Count(h => !allowed.Any(a => Math.Abs(a - h) < 1e-9)), 0, 0);
        check("Thiết lập mặc định: có chữ 125 (2,5 mm × 50) và tên hình 175 (3,5 mm × 50)", B(h0.Any(h => Math.Abs(h - 125) < 1e-9) && h0.Any(h => Math.Abs(h - 175) < 1e-9)), 1, 0);
        check("Thiết lập mặc định: tỷ lệ mặt đứng dầm 1/50", el0.Scale, 50, 0);
        var sec0 = figs0[1].W;
        double k25 = sec0.Scale / 25;
        var dimSec = sec0.TextHeights(Lay.KichThuoc);
        check("Mặt cắt 1/25: chữ kích thước = 2,5 mm × 25 (mô hình thật)", dimSec.Count > 0 ? dimSec.Max() / k25 : 0, 2.5 * 25, 1e-9);
        check("Mặt cắt 1/25: dùng kiểu kích thước VXT_1-25", B(sec0.DimStyleTable().Any(x => x.Name == "VXT_1-25" && x.Scale == 25) && sec0.DimScalesUsed.Contains(25)), 1, 0);
        check("Thiết lập mặc định: DXF không ghi hệ số rộng chữ (mã 41 TEXT)", B(el0.GetPrimitives().All(q => q.Kind != PrimKind.Text || q.WF == 1)), 1, 0);

        // ---------------- 2. đổi cỡ chữ kích thước: chỉ chữ kích thước đổi ----------------
        var sDim = def.Clone(); sDim.DimTextHeight = 3.5;
        List<Figure> figs1;
        using (DrawingStyle.Use(sDim)) figs1 = BeamDrawing.Build(p, beam);
        var el1 = figs1[0].W;
        var dimH0 = el0.TextHeights(Lay.KichThuoc); var dimH1 = el1.TextHeights(Lay.KichThuoc);
        check("Chữ kích thước 3,5 mm: mọi chữ lớp KICH_THUOC = 3,5 × 50", B(dimH1.Count > 0 && dimH1.All(h => Math.Abs(h - 175) < 1e-9)), 1, 0);
        check("Chữ kích thước 3,5 mm: số chữ kích thước không đổi", dimH1.Count, dimH0.Count, 0);
        var chu0 = el0.TextHeights(Lay.Chu).OrderBy(x => x).ToList(); var chu1 = el1.TextHeights(Lay.Chu).OrderBy(x => x).ToList();
        check("Chữ kích thước 3,5 mm: chữ khác (lớp CHU) giữ nguyên cỡ", B(chu0.SequenceEqual(chu1)), 1, 0);

        // kiểu kích thước riêng cho 1/25: chỉ mặt cắt đổi, mặt đứng 1/50 giữ nguyên
        var s25 = def.Clone(); s25.FindDimStyle(25).TextHeight = 3.0;
        List<Figure> figs25;
        using (DrawingStyle.Use(s25)) figs25 = BeamDrawing.Build(p, beam);
        check("Kiểu VXT_1-25 chữ 3 mm: chữ kích thước mặt cắt = 3 × 25 (mô hình thật)", figs25[1].W.TextHeights(Lay.KichThuoc).Max() / k25, 3.0 * 25, 1e-9);
        check("Kiểu VXT_1-25 chữ 3 mm: mặt đứng 1/50 giữ 2,5 mm", B(figs25[0].W.TextHeights(Lay.KichThuoc).All(h => Math.Abs(h - 125) < 1e-9)), 1, 0);

        // mũi tên đặc thay gạch xiên
        var sArr = def.Clone(); sArr.DimArrowType = DimArrow.ClosedArrow;
        DxfWriter elA;
        using (DrawingStyle.Use(sArr)) elA = BeamDrawing.Build(p, beam)[0].W;
        check("Kiểu kích thước mũi tên đặc: có SOLID lớp KICH_THUOC", B(elA.CountEntities("SOLID", Lay.KichThuoc) > 0 && el0.CountEntities("SOLID", Lay.KichThuoc) == 0), 1, 0);

        // ---------------- 3. đổi tỷ lệ mặt đứng dầm 1/50 → 1/100 ----------------
        var s100 = def.Clone(); s100.ScaleBeamElevation = 100;
        List<Figure> figs100;
        using (DrawingStyle.Use(s100)) figs100 = BeamDrawing.Build(p, beam);
        var el2 = figs100[0].W;
        var (a0, b0) = RangeX(el0, Lay.BeTong); var (a2, b2) = RangeX(el2, Lay.BeTong);
        check("Tỷ lệ 1/100: bộ ghi mặt đứng dầm tỷ lệ 100", el2.Scale, 100, 0);
        check("Tỷ lệ 1/100: chiều dài dầm trên giấy giảm một nửa", ((b0 - a0) / 50) / ((b2 - a2) / 100), 2, 1e-9);
        check("Tỷ lệ 1/100: tên hình ghi \"tl: 1/100\"", B(el2.Texts().Contains("tl: 1/100")), 1, 0);
        check("Tỷ lệ 1/100: chữ vẫn 2,5 mm trên giấy (= 250 mô hình)", B(el2.TextHeights(Lay.KichThuoc).All(h => Math.Abs(h - 250) < 1e-9)), 1, 0);
        check("Tỷ lệ 1/100: mặt cắt giữ 1/25 (\"tl: 1/25\")", B(figs100[1].W.Texts().Contains("tl: 1/25")), 1, 0);
        var s20 = def.Clone(); s20.ScaleMemberSection = 20;
        DxfWriter sec20;
        using (DrawingStyle.Use(s20)) sec20 = BeamDrawing.Build(p, beam)[1].W;
        var (c0, e0) = RangeX(sec0, Lay.BeTong); var (c1, e1) = RangeX(sec20, Lay.BeTong);
        check("Mặt cắt 1/20: tiết diện trên giấy lớn gấp 25/20", (e1 - c1) / (e0 - c0), 25.0 / 20, 1e-9);

        // ---------------- 4. bật/tắt chú thích ----------------
        string elev = DxfWriter.ElevText(beam.TopElevation);
        var sOff = def.Clone();
        sOff.ShowElevationMarks = false; sOff.ShowGridDims = false; sOff.ShowLoadValues = false; sOff.ShowMemberIds = false; sOff.ShowDesignValues = false;
        DxfWriter elOff, planOn, planOff, diaOn, diaOff;
        using (DrawingStyle.Use(sOff)) elOff = BeamDrawing.Build(p, beam)[0].W;
        check("Tắt cao độ: mặt đứng dầm không còn " + elev, B(el0.Texts().Contains(elev) && !elOff.Texts().Contains(elev)), 1, 0);
        var st1 = p.Stories.OrderBy(x => x.Elevation).Last();
        using (DrawingStyle.Use(def)) planOn = FramingPlan.Build(p, d, st1, FramingPlan.CurrentScale);
        using (DrawingStyle.Use(sOff)) planOff = FramingPlan.Build(p, d, st1, FramingPlan.CurrentScale);
        check("Tắt kích thước lưới trục: mặt bằng không còn chữ kích thước", B(planOn.CountEntities("TEXT", Lay.KichThuoc) > 0 && planOff.CountEntities("TEXT", Lay.KichThuoc) == 0), 1, 0);
        var g = ElevationDrawing.FindGrid(p, null);
        using (DrawingStyle.Use(def)) diaOn = ElevationDrawing.Build(p, r, g, "M3", null);
        using (DrawingStyle.Use(sOff)) diaOff = ElevationDrawing.Build(p, r, g, "M3", null);
        check("Tắt giá trị nội lực: biểu đồ ít chữ hơn, không còn \"Giá trị lớn nhất\"",
            B(diaOff.Texts().Count < diaOn.Texts().Count && diaOn.Texts().Any(t => t.StartsWith("Giá trị lớn nhất")) && !diaOff.Texts().Any(t => t.StartsWith("Giá trị lớn nhất"))), 1, 0);
        check("Tắt cao độ: mặt đứng khung không còn ký hiệu ±0.000/+…", B(diaOn.Texts().Any(t => t.StartsWith("+")) && !diaOff.Texts().Any(t => t.StartsWith("+") && t.Contains('.'))), 1, 0);

        // ---------------- 5. GHI CHÚ CHUNG: đúng một khối mỗi bản vẽ ----------------
        string enc = DxfWriter.Encode(GeneralNotes.Heading);
        string dir = Path.Combine(outDir, "ThietLapBanVe");
        if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir, "*.dxf")) File.Delete(f);
        List<string> files;
        using (DrawingStyle.Use(def)) files = DrawingSet.ExportAll(p, d, dir);
        int bad = files.Count(f => CountOf(File.ReadAllText(f), enc) != 1);
        check("GHI CHÚ CHUNG: mỗi bản vẽ KC-01…KC-06 đúng 1 khối", bad, 0, 0);
        check("GHI CHÚ CHUNG: có ≥ 4 bản vẽ", B(files.Count >= 4), 1, 0);
        string tmp = Path.Combine(Path.GetTempPath(), "kcvxt_style_off");
        if (Directory.Exists(tmp)) foreach (var f in Directory.GetFiles(tmp, "*.dxf")) File.Delete(f);
        var sNo = def.Clone(); sNo.ShowGeneralNotes = false;
        List<string> filesNo;
        using (DrawingStyle.Use(sNo)) filesNo = DrawingSet.ExportAll(p, d, tmp);
        check("Tắt GHI CHÚ CHUNG: không bản vẽ nào có khối ghi chú", filesNo.Sum(f => CountOf(File.ReadAllText(f), enc)), 0, 0);
        check("GHI CHÚ CHUNG: dầm không còn ghi chú lặp \"dùng đai kín móc 135°\" trên từng hình",
            figs0.Sum(f => f.W.Texts().Count(t => t.Contains("đai kín móc 135°"))), 0, 0);

        // ---------------- 6. bảng DIMSTYLE ----------------
        string kc04 = files.FirstOrDefault(f => Path.GetFileName(f).StartsWith("KC-04", StringComparison.Ordinal)) ?? files[0];
        var ds = DimStyles(File.ReadAllText(kc04));
        double[] want = { 15, 25, 30, 50, 100, 200, 250 };
        check("DIMSTYLE: có đủ 7 kiểu VXT_1-15 … VXT_1-250", want.Count(s => ds.ContainsKey("VXT_1-" + s.ToString("0", Inv))), 7, 0);
        check("DIMSTYLE: DIMSCALE = mẫu số tỷ lệ", want.Count(s => ds.TryGetValue("VXT_1-" + s.ToString("0", Inv), out double v) && Math.Abs(v - s) < 1e-9), 7, 0);
        string txt04 = File.ReadAllText(kc04);
        check("DIMSTYLE: bảng nằm trong TABLES, trước ENTITIES", B(txt04.IndexOf("DIMSTYLE", StringComparison.Ordinal) > 0 &&
            txt04.IndexOf("DIMSTYLE", StringComparison.Ordinal) < txt04.IndexOf("ENTITIES", StringComparison.Ordinal)), 1, 0);
        // tỷ lệ tuỳ ý trên tờ dàn trang → tạo kiểu tự động
        var sheets = SheetBuilder.DefaultSet(p, d, r);
        var plan = sheets.SelectMany(s => s.Views).FirstOrDefault(v => v.Kind == SheetViewKind.PlanStory);
        if (plan != null)
        {
            var v2 = SheetBuilder.Clone(plan); v2.Scale = 40;
            var sh = new Sheet { Number = "KC-T", Name = "THỬ TỶ LỆ", Paper = "A1", Landscape = true, Views = new List<SheetView> { v2 } };
            DxfWriter wsh;
            using (DrawingStyle.Use(def)) wsh = SheetBuilder.Compose(p, r, d, sh);
            check("DIMSTYLE: khung nhìn 1/40 → tạo kiểu VXT_1-40 (DIMSCALE 40)", B(DimStyles(wsh.ToDxfString()).TryGetValue("VXT_1-40", out double v40) && Math.Abs(v40 - 40) < 1e-9), 1, 0);
        }
        var sPre = def.Clone(); sPre.DimStylePrefix = "KC_";
        check("DIMSTYLE: tiền tố tuỳ chọn → KC_1-50", B(sPre.DimStyleName(50) == "KC_1-50"), 1, 0);

        // ---------------- 7. hệ số rộng chữ ----------------
        var sW = def.Clone(); sW.WidthFactor = 0.8;
        DxfWriter elW;
        using (DrawingStyle.Use(sW)) elW = BeamDrawing.Build(p, beam)[0].W;
        check("Hệ số rộng chữ 0,8: chữ đọc lại WF = 0,8", B(elW.GetPrimitives().Where(q => q.Kind == PrimKind.Text).All(q => Math.Abs(q.WF - 0.8) < 1e-9)), 1, 0);

        // ---------------- 8. JSON ----------------
        var std = CadStandard.Default();
        std.Drawing.ScaleBeamElevation = 30; std.Drawing.TagHeight = 3; std.Drawing.ShowGridDims = false;
        std.Drawing.FindDimStyle(100).TextHeight = 2.0; std.Drawing.FindDimStyle(100).Arrow = DimArrow.ClosedArrow;
        var back = CadStandard.FromJson(std.ToJson());
        check("JSON: lưu/mở giữ thiết lập bản vẽ", B(back.Drawing.ToJson() == std.Drawing.ToJson()), 1, 0);
        check("JSON: kiểu VXT_1-100 giữ chữ 2 mm + mũi tên", B(back.Drawing.DimFor(100).TextHeight == 2.0 && back.Drawing.DimFor(100).Arrow == DimArrow.ClosedArrow), 1, 0);
        var old = CadStandard.FromJson("{ \"FontName\": \"Arial\" }");
        check("JSON cũ (không có Drawing): dùng thiết lập mặc định", B(old.Drawing != null && old.Drawing.IsDefault && old.Drawing.DimStyles.Count == 7), 1, 0);
        var bad2 = DrawingStyle.FromJson("{ \"NoteHeight\": -1, \"ScaleBeamElevation\": 0, \"DimStylePrefix\": \"  \" }");
        check("JSON sai: tự sửa về mặc định (chữ 2,5; tỷ lệ 50; tiền tố VXT_)", B(bad2.NoteHeight == 2.5 && bad2.ScaleBeamElevation == 50 && bad2.DimStylePrefix == "VXT_"), 1, 0);
        Console.WriteLine($"   · thiết lập bản vẽ: {files.Count} bản vẽ, DIMSTYLE {string.Join(", ", ds.Keys)}");
    }
}

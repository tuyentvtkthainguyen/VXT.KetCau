using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using KetCauVXT.Analysis;
using KetCauVXT.Export;
using KetCauVXT.Import.Csi;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng "So sánh với ETABS/SAP": bảng kết quả kiểu ETABS do chương trình ghi (ResultTableWriter) → so sánh với chính kết quả
/// của chương trình → sai lệch ≈ 0; tăng nội lực một dầm trong bảng ngoài +7 % → cảnh báo, +15 % → sai lệch lớn; đơn vị Tonf vẫn ≈ 0;
/// thanh không khớp được liệt kê; bảng Base Reactions; file Excel / Word hợp lệ. check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class CrossCheckTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static double B(bool v) => v ? 1 : 0;

    private static Project Sample()
    {
        var p = ModelBuilder.NewProject("Kiểm thử so sánh ETABS");
        ModelBuilder.SetGrid(p, new[] { 3.6, 4.2 }, new[] { 4.5, 4.0 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3, 3.3 }, -0.6);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions
        {
            ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.0
        });
        return p;
    }

    /// <summary>Nhân các cột nội lực (P…M3) của một thanh trong bảng nội lực thanh với k.</summary>
    private static int Scale(List<ExtTable> tabs, string label, double k)
    {
        int n = 0;
        foreach (var t in tabs.Where(t => ResultImportSession.Classify(t) == ExtTableKind.FrameForces))
        {
            int cl = t.Col("Beam", "Column", "Brace", "Frame");
            int[] cf = { t.Col("P"), t.Col("V2"), t.Col("V3"), t.Col("T"), t.Col("M2"), t.Col("M3") };
            foreach (var r in t.Rows)
            {
                if (ExtTable.Cell(r, cl) != label) continue;
                foreach (int c in cf)
                    if (c >= 0 && ExtTable.TryNum(ExtTable.Cell(r, c), out double v)) r[c] = (v * k).ToString("R", Inv);
                n++;
            }
        }
        return n;
    }

    private static CrossCheckReport Compare(Project p, AnalysisResults r, List<ExtTable> tabs, string path, Action<CrossCheckSession> tune = null)
    {
        var s = new CrossCheckSession(p, r);
        s.AddTables(tabs, path);
        s.Analyze();
        tune?.Invoke(s);
        return s.Run();
    }

    private static double MaxPct(IEnumerable<double> v) => v.Where(x => !double.IsNaN(x)).DefaultIfEmpty(0).Max();

    /// <summary>File zip mở được và mọi phần .xml / .rels đọc được bằng XDocument.</summary>
    private static bool ZipXmlOk(string path, string mustHave, out string names)
    {
        names = "";
        try
        {
            using var z = ZipFile.OpenRead(path);
            if (z.GetEntry(mustHave) == null) return false;
            foreach (var e in z.Entries.Where(e => e.FullName.EndsWith(".xml") || e.FullName.EndsWith(".rels")))
            {
                using var st = e.Open();
                var doc = XDocument.Load(st);
                if (e.FullName == "xl/workbook.xml")
                    names = string.Join("|", doc.Descendants().Where(x => x.Name.LocalName == "sheet").Select(x => (string)x.Attribute("name")));
            }
            return true;
        }
        catch (Exception) { return false; }
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Console.WriteLine("CC. So sánh kết quả với ETABS/SAP2000 (bảng kết quả ngoài ↔ phân tích của chương trình)");
        string dir = Path.Combine(outDir, "SoSanhETABS");
        Directory.CreateDirectory(dir);
        var p = Sample();
        var r = FrameAnalysis.Run(p);
        string csv = Path.Combine(dir, "cc_etabs_kN_m.csv");
        ResultTableWriter.Write(r, csv, new ResultTableWriter.Options { Etabs = true });
        var beam = p.Frames.Where(f => f.Kind == FrameKind.Beam)
            .OrderByDescending(f => r.Get(r.DesignCombos()[0]).Frames[f.Id].F.Max(v => Math.Abs(v[5]))).First();
        string beamLabel = E2kExporter.LineName(beam, p);

        // ---------------- 1. cùng kết quả → sai lệch ≈ 0; thanh lạ trong file → báo không khớp ----------------
        CrossCheckReport rep0;
        {
            var tabs = ExternalTableReader.ReadFile(csv);
            var beams = tabs.First(t => t.Name == "Element Forces - Beams");
            int cl = beams.Col("Beam");
            var extra = (string[])beams.Rows[0].Clone();
            extra[cl] = "BX1";
            beams.Rows.Add(extra);
            CrossCheckSession ses = null;
            rep0 = Compare(p, r, tabs, csv, s => ses = s);
            int nPairs = ses.Pairs.Count(x => x.Active);
            int nCases = p.Patterns.Count + p.Combinations.Count(c => c.Type != ComboType.Envelope) + 2 * p.Combinations.Count(c => c.Type == ComboType.Envelope);
            check("So sánh: mọi trường hợp/tổ hợp tự ghép theo tên", nPairs, nCases, 0);
            check("So sánh: bao BAO (Max) ghép với tổ hợp bao BAO", B(ses.Pairs.Any(x => x.External == "BAO (Max)" && x.Ours == "BAO" && x.Active)), 1, 0);
            check("So sánh: số (thanh × trường hợp) = số thanh × số cặp", rep0.MemberCases.Count, p.Frames.Count * nPairs, 0);
            check("So sánh: mọi thanh của dự án có số liệu", rep0.Members.Count(m => m.CaseCount > 0), p.Frames.Count, 0);
            check("So sánh cùng kết quả: sai lệch nội lực thanh lớn nhất (%)", MaxPct(rep0.MemberCases.Select(m => m.WorstPercent)), 0, 1e-4);
            check("So sánh cùng kết quả: sai lệch tuyệt đối M3 lớn nhất (kN.m)", rep0.MemberCases.Max(m => m.Diff[5]), 0, 1e-5);
            check("So sánh cùng kết quả: không có cảnh báo / sai lệch lớn", rep0.Stat(CrossCheckSession.GroupMembers).Warn + rep0.Stat(CrossCheckSession.GroupMembers).Fail, 0, 0);
            check("So sánh cùng kết quả: có so sánh phản lực gối", B(rep0.Reactions.Count > 0), 1, 0);
            check("So sánh cùng kết quả: sai lệch phản lực lớn nhất (%)", MaxPct(rep0.Reactions.Select(x => x.WorstPercent)), 0, 1e-4);
            check("So sánh cùng kết quả: tổng phản lực (tổng nút) sai lệch (%)", MaxPct(rep0.BaseReactions.Select(x => x.WorstPercent)), 0, 1e-4);
            check("So sánh cùng kết quả: có so sánh chuyển vị tầng", B(rep0.Stories.Any(x => x.Quantity == "Chuyển vị" && x.Top)), 1, 0);
            // chuyển vị ghi 7 chữ số thập phân (m) → sai số làm tròn ≤ 5e-8 m so với sàn 0,5 mm
            check("So sánh cùng kết quả: sai lệch chuyển vị tầng (%)", MaxPct(rep0.Stories.Select(x => x.Percent)), 0, 0.05);
            check("So sánh cùng kết quả: đánh giá chung phù hợp", B(rep0.Overall == CrossLevel.Ok), 1, 0);
            check("So sánh: thanh BX1 trong file ngoài được báo không khớp", B(rep0.UnmatchedExternal.Any(m => m.External.StartsWith("BX1"))), 1, 0);
            check("So sánh: nguồn nhận diện ETABS", B(rep0.Software == "ETABS"), 1, 0);
            check("So sánh: có gợi ý nguyên nhân", B(rep0.Hints.Count > 0 && CrossCheckReport.Guidance.Length >= 6), 1, 0);
        }

        // ---------------- 2. +7 % một dầm → cảnh báo ; ngưỡng tuỳ chỉnh ----------------
        {
            var tabs = ExternalTableReader.ReadFile(csv);
            int rows = Scale(tabs, beamLabel, 1.07);
            check("+7 %: đã sửa các dòng của dầm " + beamLabel, B(rows > 0), 1, 0);
            CrossCheckSession ses = null;
            var rep = Compare(p, r, tabs, csv, s => ses = s);
            var m = rep.Members.First(x => x.FrameId == beam.Id);
            check("+7 %: dầm bị đánh giá cảnh báo (5…10 %)", B(m.Level == CrossLevel.Warn), 1, 0);
            check("+7 %: sai lệch % của dầm ≈ 7/1,07", m.WorstPercent, 7 / 1.07, 1e-3);
            check("+7 %: đúng 1 cấu kiện cảnh báo, 0 sai lệch lớn", rep.Stat(CrossCheckSession.GroupMembers).Warn * 10 + rep.Stat(CrossCheckSession.GroupMembers).Fail, 10, 0);
            check("+7 %: dầm nằm đầu danh sách sai lệch lớn nhất", B(rep.Worst.First().FrameId == beam.Id), 1, 0);
            check("+7 %: các thanh khác vẫn ≈ 0 %", MaxPct(rep.Members.Where(x => x.FrameId != beam.Id).Select(x => x.WorstPercent)), 0, 1e-4);
            check("+7 %: đánh giá chung = cảnh báo", B(rep.Overall == CrossLevel.Warn), 1, 0);
            ses.Options.WarnPercent = 8;
            var rep8 = ses.Run();
            check("+7 % với ngưỡng cảnh báo 8 %: dầm phù hợp", B(rep8.Members.First(x => x.FrameId == beam.Id).Level == CrossLevel.Ok), 1, 0);
        }

        // ---------------- 3. +15 % → sai lệch lớn ----------------
        {
            var tabs = ExternalTableReader.ReadFile(csv);
            Scale(tabs, beamLabel, 1.15);
            var rep = Compare(p, r, tabs, csv);
            var m = rep.Members.First(x => x.FrameId == beam.Id);
            check("+15 %: dầm bị đánh giá sai lệch lớn (> 10 %)", B(m.Level == CrossLevel.Fail), 1, 0);
            check("+15 %: sai lệch % ≈ 15/1,15", m.WorstPercent, 15 / 1.15, 1e-3);
            check("+15 %: đúng 1 cấu kiện sai lệch lớn", rep.Stat(CrossCheckSession.GroupMembers).Fail, 1, 0);
            check("+15 %: gợi ý mô hình sàn / đoạn cứng khi dầm lệch do tải đứng", B(rep.Hints.Any(h => h.Contains("45°"))), 1, 0);
            var (xl, wd) = CrossCheckWriter.SaveAll(rep, Path.Combine(dir, "Lech15"));
            check("+15 %: Excel ghi được", B(ZipXmlOk(xl, "xl/workbook.xml", out _)), 1, 0);
            check("+15 %: Excel có nền đỏ cho ô vượt ngưỡng", B(XlHasStyle(xl, (int)KetCauVXT.Report.XlStyle.FillFail)), 1, 0);
        }

        // ---------------- 4. đơn vị Tonf, m + bảng Base Reactions ----------------
        {
            string csvT = Path.Combine(dir, "cc_etabs_Tonf_m.csv");
            ResultTableWriter.Write(r, csvT, new ResultTableWriter.Options { Etabs = true, ForceUnit = "Tonf", LengthUnit = "m" });
            var tabs = ExternalTableReader.ReadFile(csvT);
            // bảng Base Reactions (Tonf, Tonf-m) – tổng phản lực, mô men quanh gốc toạ độ
            var bt = new ExtTable
            {
                Name = "Base Reactions", File = csvT, Program = "ETABS",
                Headers = new List<string> { "Output Case", "Case Type", "Step Type", "FX", "FY", "FZ", "MX", "MY", "MZ" },
                Units = new List<string> { "", "", "", "tonf", "tonf", "tonf", "tonf-m", "tonf-m", "tonf-m" }
            };
            const double tf = 9.80665;
            foreach (var pat in p.Patterns)
            {
                var s = new double[6];
                foreach (var (id, v) in r.Get(pat.Name).Reactions)
                {
                    var n = p.Node(id);
                    s[0] += v[0]; s[1] += v[1]; s[2] += v[2];
                    s[3] += v[3] + n.Y * v[2] - n.Z * v[1];
                    s[4] += v[4] + n.Z * v[0] - n.X * v[2];
                    s[5] += v[5] + n.X * v[1] - n.Y * v[0];
                }
                bt.Rows.Add(new[] { pat.Name, "LinStatic", "" }.Concat(s.Select(x => (x / tf).ToString("R", Inv))).ToArray());
            }
            tabs.Add(bt);
            var rep = Compare(p, r, tabs, csvT);
            check("Tonf, m: sai lệch nội lực thanh lớn nhất (%)", MaxPct(rep.MemberCases.Select(m => m.WorstPercent)), 0, 1e-3);
            check("Tonf, m: sai lệch phản lực lớn nhất (%)", MaxPct(rep.Reactions.Select(x => x.WorstPercent)), 0, 1e-3);
            check("Tonf, m: đơn vị nhận diện Tonf", B(rep.Units.Contains("Tonf", StringComparison.OrdinalIgnoreCase)), 1, 0);
            var bs = rep.BaseReactions.Where(b => b.Source == "Base Reactions").ToList();
            check("Base Reactions: dùng bảng tổng phản lực cho mọi trường hợp tải", bs.Count, p.Patterns.Count, 0);
            check("Base Reactions: sai lệch FX, FY, FZ (%)", MaxPct(bs.Select(b => b.WorstPercent)), 0, 1e-3);
            check("Base Reactions: sai lệch mô men tổng (%)", MaxPct(bs.Select(b => Math.Max(b.Percent[3], Math.Max(b.Percent[4], b.Percent[5])))), 0, 1e-3);
            check("Tonf, m: đánh giá chung phù hợp", B(rep.Overall == CrossLevel.Ok), 1, 0);
        }

        // ---------------- 5. ghép bằng tay sai trường hợp → phát hiện ----------------
        {
            var tabs = ExternalTableReader.ReadFile(csv);
            var live = p.Patterns.FirstOrDefault(x => x.Type == LoadType.Live) ?? p.Patterns.Last();
            var dead = p.Patterns.First(x => x.Type == LoadType.Dead);
            var rep = Compare(p, r, tabs, csv, s =>
            {
                foreach (var pr in s.Pairs) pr.Enabled = pr.External == dead.Name;
                s.SetPair(dead.Name, live.Name);
            });
            check("Ghép tay " + dead.Name + " ↔ " + live.Name + ": phát hiện sai lệch lớn", B(rep.Stat(CrossCheckSession.GroupMembers).Fail > 0), 1, 0);
            check("Ghép tay: chỉ so sánh 1 cặp", rep.MemberCases.Select(m => m.ExternalCase).Distinct().Count(), 1, 0);
        }

        // ---------------- 6. file báo cáo ----------------
        {
            string sub = Path.Combine(dir, "BaoCao");
            var (xl, wd) = CrossCheckWriter.SaveAll(rep0, sub);
            check("Excel " + CrossCheckWriter.ExcelName + " tồn tại", B(File.Exists(xl) && Path.GetFileName(xl) == "SoSanh_ETABS.xlsx"), 1, 0);
            check("Excel: zip + XML hợp lệ", B(ZipXmlOk(xl, "xl/workbook.xml", out string sheets)), 1, 0);
            check("Excel: 5 sheet Tổng hợp, Thanh, Phản lực, Chuyển vị, Ghi chú", B(sheets == "Tổng hợp|Thanh|Phản lực|Chuyển vị|Ghi chú"), 1, 0);
            check("Excel: ô % tô nền xanh", B(XlHasStyle(xl, (int)KetCauVXT.Report.XlStyle.FillOk)), 1, 0);
            check("Word " + CrossCheckWriter.WordName + ": zip + XML hợp lệ", B(ZipXmlOk(wd, "word/document.xml", out _)), 1, 0);
            check("Word: có mục tổng hợp sai lệch", B(DocText(wd).Contains("Tổng hợp sai lệch")), 1, 0);
            Console.WriteLine("   " + string.Join(" | ", rep0.Stats.Select(s => $"{s.Group}: {s.Count} mục, max {s.Max:0.####} %")));
        }
    }

    private static bool XlHasStyle(string xlsx, int style)
    {
        using var z = ZipFile.OpenRead(xlsx);
        foreach (var e in z.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/")))
        {
            using var sr = new StreamReader(e.Open());
            if (sr.ReadToEnd().Contains($" s=\"{style}\"")) return true;
        }
        return false;
    }

    private static string DocText(string docx)
    {
        using var z = ZipFile.OpenRead(docx);
        using var sr = new StreamReader(z.GetEntry("word/document.xml").Open());
        var doc = XDocument.Parse(sr.ReadToEnd());
        return string.Concat(doc.Descendants().Where(x => x.Name.LocalName == "t").Select(x => x.Value));
    }
}

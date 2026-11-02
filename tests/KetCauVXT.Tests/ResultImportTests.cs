using System.Globalization;
using System.Text;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Export;
using KetCauVXT.Import.Csi;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng nhập mô hình (.e2k/.s2k) và nhập kết quả phân tích từ ETABS / SAP2000 / SAFE / bảng CSV:
/// khứ hồi mô hình qua bộ xuất của chương trình, khứ hồi nội lực (bảng ETABS/SAP do chương trình ghi), quy ước dấu (dầm đơn giản,
/// thanh ngược chiều), đổi đơn vị (Tonf, kgf-cm), khớp thanh theo toạ độ khi tên khác, thiết kế theo nội lực nhập cho cùng kết quả thép,
/// mô men dải SAFE thay phương pháp hệ số khi thiết kế sàn. check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class ResultImportTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static double B(bool v) => v ? 1 : 0;

    private static Project Sample()
    {
        var p = ModelBuilder.NewProject("Kiểm thử nhập kết quả");
        ModelBuilder.SetGrid(p, new[] { 3.6, 4.2 }, new[] { 4.5, 4.0 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3, 3.3 }, -0.6);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions
        {
            ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.0
        });
        return p;
    }

    /// <summary>Sai lệch lớn nhất (tuyệt đối) của nội lực thanh giữa hai bộ kết quả trên các trạm của bộ thứ nhất.</summary>
    private static double MaxFrameDiff(AnalysisResults a, AnalysisResults b, IEnumerable<string> cases, out int compared)
    {
        double worst = 0; compared = 0;
        foreach (var cs in cases)
        {
            var ca = a.Get(cs); var cb = b.Get(cs);
            foreach (var kv in ca.Frames)
            {
                if (!cb.Frames.TryGetValue(kv.Key, out var fb)) { worst = Math.Max(worst, 1e9); continue; }
                var fa = kv.Value;
                for (int i = 0; i < fa.Count; i++)
                {
                    var vb = fb.At(fa.Stations[i]);
                    for (int k = 0; k < 6; k++) worst = Math.Max(worst, Math.Abs(fa.F[i][k] - vb[k]));
                }
                compared++;
            }
        }
        return worst;
    }

    private static double MaxNodeDiff(AnalysisResults a, AnalysisResults b, IEnumerable<string> cases, bool reactions)
    {
        double worst = 0;
        foreach (var cs in cases)
        {
            var da = reactions ? a.Get(cs).Reactions : a.Get(cs).Disp;
            var db = reactions ? b.Get(cs).Reactions : b.Get(cs).Disp;
            foreach (var kv in da)
            {
                if (!db.TryGetValue(kv.Key, out var v)) { worst = 1e9; continue; }
                for (int k = 0; k < 6; k++) worst = Math.Max(worst, Math.Abs(kv.Value[k] - v[k]));
            }
        }
        return worst;
    }

    private static AnalysisResults ImportFile(Project p, string path, AnalysisResults own = null, Action<ResultImportSession> tune = null)
    {
        var s = new ResultImportSession(p, own);
        s.AddFile(path);
        s.Analyze();
        tune?.Invoke(s);
        return s.Build();
    }

    private static string DesignSig(DesignSummary d)
    {
        var sb = new StringBuilder();
        foreach (var b in d.Beams.OrderBy(x => x.Name))
        {
            sb.Append(b.Name).Append(':');
            foreach (var m in b.Bars) sb.Append(m.Role).Append(m.Dia).Append('x').Append(m.Count).Append('/').Append(m.Length.ToString("0.00", Inv)).Append(';');
            foreach (var s in b.Spans) sb.Append(s.AsBotReq.ToString("0.000000", Inv)).Append(',');
            sb.Append('|');
        }
        foreach (var c in d.Columns.OrderBy(x => x.Name))
        {
            sb.Append(c.Name).Append(':');
            foreach (var m in c.Bars) sb.Append(m.Role).Append(m.Dia).Append('x').Append(m.Count).Append(';');
            sb.Append('|');
        }
        foreach (var f in d.Footings.OrderBy(x => x.Name)) sb.Append(f.Name).Append(':').Append(f.L.ToString("0.00", Inv)).Append('x').Append(f.B.ToString("0.00", Inv)).Append('|');
        return sb.ToString();
    }

    private static bool SameEnds(Project a, FrameElement fa, Project b, FrameElement fb)
    {
        var a1 = a.Node(fa.NodeI).P; var a2 = a.Node(fa.NodeJ).P;
        var b1 = b.Node(fb.NodeI).P; var b2 = b.Node(fb.NodeJ).P;
        return ((a1 - b1).Length < 1e-3 && (a2 - b2).Length < 1e-3) || ((a1 - b2).Length < 1e-3 && (a2 - b1).Length < 1e-3);
    }

    private static double TotalFz(AnalysisResults r, string cs) => r.Get(cs).Reactions.Values.Sum(v => v[2]);

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Console.WriteLine("RI. Nhập mô hình ETABS/SAP2000 và nhập kết quả nội lực ETABS/SAP2000/SAFE");
        Directory.CreateDirectory(outDir);
        var p = Sample();
        var r = FrameAnalysis.Run(p);
        var allCases = p.Patterns.Select(x => x.Name).Concat(p.Combinations.Where(c => c.Type != ComboType.Envelope).Select(c => c.Name)).ToList();

        // ---------------- 1. đơn vị ----------------
        {
            UnitConv.TryFactor("Tonf-m", out double a); UnitConv.TryFactor("kgf/cm2", out double b2); UnitConv.TryFactor("kN.m/m", out double c);
            UnitConv.TryFactor("kgf-cm", out double d); UnitConv.TryFactor("kip-ft", out double e);
            check("Đơn vị: 1 Tonf-m = 9,80665 kN.m", a, 9.80665, 1e-9);
            check("Đơn vị: 1 kgf/cm² = 98,0665 kN/m²", b2, 98.0665, 1e-9);
            check("Đơn vị: kN.m/m = 1", c, 1, 1e-12);
            check("Đơn vị: 1 kgf-cm = 9,80665e-5 kN.m", d, 9.80665e-5, 1e-9);
            check("Đơn vị: 1 kip-ft = 1,35582 kN.m", e, 4.4482216 * 0.3048, 1e-6);
        }

        // ---------------- 2. khứ hồi mô hình .e2k ----------------
        Project qe;
        {
            string e2k = Path.Combine(outDir, "ri_mohinh.e2k");
            E2kExporter.Export(p, e2k);
            qe = E2kImporter.ImportNew(e2k, out var rep);
            check("E2K: số tầng", qe.Stories.Count, p.Stories.Count, 0);
            check("E2K: tên tầng giống nhau", B(qe.Stories.Select(s => s.Name).SequenceEqual(p.Stories.OrderBy(s => s.Elevation).Select(s => s.Name))), 1, 0);
            check("E2K: cao độ chân cột", qe.BaseElevation, p.BaseElevation, 1e-9);
            check("E2K: số nút", qe.Nodes.Count, p.Nodes.Count, 0);
            check("E2K: số thanh", qe.Frames.Count, p.Frames.Count, 0);
            check("E2K: số ô sàn", qe.Slabs.Count, p.Slabs.Count, 0);
            check("E2K: số trục", qe.Grids.Count, p.Grids.Count, 0);
            int same = p.Frames.Count(f => qe.Frames.Any(g => g.Section == f.Section && g.Kind == f.Kind && SameEnds(p, f, qe, g)));
            check("E2K: mọi thanh trùng toạ độ hai đầu + tiết diện + loại", same, p.Frames.Count, 0);
            check("E2K: số gối", qe.Nodes.Count(n => n.IsSupport), p.Nodes.Count(n => n.IsSupport), 0);
            check("E2K: số trường hợp tải", qe.Patterns.Count, p.Patterns.Count, 0);
            check("E2K: số tổ hợp", qe.Combinations.Count, p.Combinations.Count, 0);
            check("E2K: tên gốc lưu ở thanh", qe.Frames.Count(f => f.ExternalName.Length > 0 && f.ExternalStory.Length > 0), qe.Frames.Count, 0);
            var rq = FrameAnalysis.Run(qe);
            check("E2K: mô hình nhập phân tích được – tổng phản lực đứng TT", TotalFz(rq, "TT"), TotalFz(r, "TT"), 0.01);
            check("E2K: tổng phản lực đứng HT", TotalFz(rq, "HT"), TotalFz(r, "HT"), 0.01);
            Console.WriteLine("   " + rep.ToString().Replace("\n", "\n   "));
        }

        // ---------------- 3. khứ hồi mô hình .s2k ----------------
        {
            string s2k = Path.Combine(outDir, "ri_mohinh.s2k");
            S2kExporter.Export(p, s2k);
            var qs = S2kImporter.ImportNew(s2k, out var rep);
            check("S2K: số nút", qs.Nodes.Count, p.Nodes.Count, 0);
            check("S2K: số thanh", qs.Frames.Count, p.Frames.Count, 0);
            check("S2K: số tầng (suy từ cao độ dầm)", qs.Stories.Count, p.Stories.Count, 0);
            check("S2K: tên tầng giống nhau", B(qs.Stories.Select(s => s.Name).SequenceEqual(p.Stories.OrderBy(s => s.Elevation).Select(s => s.Name))), 1, 0);
            int same = p.Frames.Count(f => qs.Frames.Any(g => g.Section == f.Section && g.Kind == f.Kind && SameEnds(p, f, qs, g)));
            check("S2K: mọi thanh trùng toạ độ hai đầu + tiết diện + loại", same, p.Frames.Count, 0);
            check("S2K: tên Frame = Id thanh", qs.Frames.Count(f => p.Frames.Any(g => g.Id.ToString(Inv) == f.ExternalName && SameEnds(p, g, qs, f))), p.Frames.Count, 0);
            check("S2K: số gối", qs.Nodes.Count(n => n.IsSupport), p.Nodes.Count(n => n.IsSupport), 0);
            check("S2K: số trường hợp tải", qs.Patterns.Count, p.Patterns.Count, 0);
            check("S2K: số tổ hợp", qs.Combinations.Count, p.Combinations.Count, 0);
            var rq = FrameAnalysis.Run(qs);
            check("S2K: mô hình nhập phân tích được – tổng phản lực đứng TT", TotalFz(rq, "TT"), TotalFz(r, "TT"), 0.01);
            Console.WriteLine("   " + rep.ToString().Replace("\n", "\n   "));
        }

        // ---------------- 4. nội lực kiểu ETABS (kN, m) → nhập → giống kết quả gốc ----------------
        AnalysisResults rE;
        {
            string csv = Path.Combine(outDir, "ri_etabs_kN_m.csv");
            ResultTableWriter.Write(r, csv, new ResultTableWriter.Options { Etabs = true });
            var ses = new ResultImportSession(p, null);
            ses.AddFile(csv);
            ses.Analyze();
            check("ETABS CSV: nhận diện 2 bảng nội lực thanh (dầm, cột)", ses.Tables.Count(t => t.Kind == ExtTableKind.FrameForces), 2, 0);   // không có thanh xiên
            check("ETABS CSV: nhận diện bảng phản lực + chuyển vị", ses.Tables.Count(t => t.Kind is ExtTableKind.JointReactions or ExtTableKind.JointDisplacements), 2, 0);
            check("ETABS CSV: khớp mọi thanh (tên xuất VXT)", ses.Matches.Count(m => m.Matched && m.Method == "tên xuất VXT"), p.Frames.Count, 0);
            check("ETABS CSV: TH1 phân loại TTGH I", B(ses.Cases.First(c => c.Name == "TH1").Use == CaseUse.ULS), 1, 0);
            check("ETABS CSV: TC1 phân loại TTGH II", B(ses.Cases.First(c => c.Name == "TC1").Use == CaseUse.SLS), 1, 0);
            check("ETABS CSV: TT là trường hợp tải", B(ses.Cases.First(c => c.Name == "TT").Use == CaseUse.Load), 1, 0);
            check("ETABS CSV: BAO có Max/Min", B(ses.Cases.First(c => c.Name == "BAO").HasMaxMin), 1, 0);
            rE = ses.Build();
            check("ETABS CSV: nguồn kết quả = ETABS", B(rE.Source == "ETABS"), 1, 0);
            double d = MaxFrameDiff(r, rE, allCases, out int n);
            check("ETABS CSV: nội lực thanh trùng kết quả gốc (sai lệch tuyệt đối lớn nhất, kN / kN.m)", d, 0, 1e-5);
            check("ETABS CSV: số (thanh × trường hợp) so sánh", n, p.Frames.Count * allCases.Count, 0);
            check("ETABS CSV: phản lực trùng", MaxNodeDiff(r, rE, allCases, true), 0, 1e-5);
            check("ETABS CSV: chuyển vị trùng", MaxNodeDiff(r, rE, allCases, false), 0, 1e-6);
            check("ETABS CSV: tổ hợp thiết kế giống dự án", B(rE.DesignCombos().SequenceEqual(r.DesignCombos())), 1, 0);
            check("ETABS CSV: tổ hợp TTGH II giống dự án", B(rE.ServiceCombos().SequenceEqual(r.ServiceCombos())), 1, 0);
            check("ETABS CSV: tổ hợp bao BAO (Max/Min) là tổ hợp bao", B(rE.IsEnvelope("BAO")), 1, 0);
            var f0 = p.Frames.First(f => f.Kind == FrameKind.Beam);
            var (mx0, mn0) = r.Envelope(f0.Id, r.ExpandCombos("BAO"));
            var (mx1, mn1) = rE.Envelope(f0.Id, rE.ExpandCombos("BAO"));
            check("ETABS CSV: bao M3 max dầm đầu tiên", mx1.F.Max(v => v[5]), mx0.F.Max(v => v[5]), 1e-6);
            check("ETABS CSV: bao M3 min dầm đầu tiên", mn1.F.Min(v => v[5]), mn0.F.Min(v => v[5]), 1e-6);
        }

        // ---------------- 5. thiết kế theo nội lực nhập → cùng thép ----------------
        {
            var d0 = ConcreteDesigner.Design(p, r);
            var d1 = ConcreteDesigner.Design(p, rE);
            check("Thiết kế theo nội lực ETABS: cùng số dầm", d1.Beams.Count, d0.Beams.Count, 0);
            check("Thiết kế theo nội lực ETABS: cùng số cột", d1.Columns.Count, d0.Columns.Count, 0);
            bool same = DesignSig(d0) == DesignSig(d1);
            if (!same) Console.WriteLine("   khác: " + DesignSig(d0).Length + " / " + DesignSig(d1).Length);
            check("Thiết kế theo nội lực ETABS: thép dầm, cột, móng giống hệt", B(same), 1, 0);
            string docx = Path.Combine(outDir, "ri_thuyetminh_noiluc_ETABS.docx");
            CalculationReport.Export(p, rE, d1, docx);
            check("Thuyết minh có chương nội lực nhập từ ETABS (file .docx)", B(File.Exists(docx) && new FileInfo(docx).Length > 1000), 1, 0);
        }

        // ---------------- 6. kiểu SAP2000, đơn vị Tonf, m ----------------
        {
            string csv = Path.Combine(outDir, "ri_sap_Tonf_m.csv");
            ResultTableWriter.Write(r, csv, new ResultTableWriter.Options { Etabs = false, ForceUnit = "Tonf", LengthUnit = "m" });
            var rS = ImportFile(p, csv);
            check("SAP CSV (Tonf, m): nguồn = SAP2000", B(rS.Source == "SAP2000"), 1, 0);
            check("SAP CSV (Tonf, m): nội lực trùng sau đổi đơn vị", MaxFrameDiff(r, rS, allCases, out _), 0, 1e-5);
            check("SAP CSV (Tonf, m): phản lực trùng", MaxNodeDiff(r, rS, allCases, true), 0, 1e-5);
        }

        // ---------------- 7. kiểu ETABS, kgf-cm, file .xlsx ----------------
        {
            string csv = Path.Combine(outDir, "ri_etabs_kgf_cm.csv");
            ResultTableWriter.Write(r, csv, new ResultTableWriter.Options { Etabs = true, ForceUnit = "kgf", LengthUnit = "cm", Displacements = false });
            // chép sang .xlsx (mỗi bảng một sheet, như ETABS "Export to Excel")
            var tabs = ExternalTableReader.ReadFile(csv);
            var xw = new XlsxWriter();
            foreach (var t in tabs)
            {
                var sh = xw.AddSheet(t.Name.Length > 31 ? t.Name.Substring(0, 31) : t.Name);
                sh.AddRow(new object[] { "TABLE:  " + t.Name });
                sh.AddRow(t.Headers.Cast<object>().ToArray());
                if (t.Units != null) sh.AddRow(t.Units.Cast<object>().ToArray());
                foreach (var row in t.Rows) sh.AddRow(row.Select(c => ExtTable.TryNum(c, out double v) ? (object)v : c).ToArray());
            }
            string xlsx = Path.Combine(outDir, "ri_etabs_kgf_cm.xlsx");
            xw.Save(xlsx);
            var ses = new ResultImportSession(p, null);
            ses.AddFile(xlsx);
            ses.Analyze();
            check("ETABS XLSX (kgf, cm): đọc các sheet", ses.Tables.Count(t => t.Kind != ExtTableKind.Ignored), tabs.Count, 0);
            var rX = ses.Build();
            check("ETABS XLSX (kgf, cm): nội lực trùng sau đổi đơn vị", MaxFrameDiff(r, rX, allCases, out _), 0, 1e-4);
            check("ETABS XLSX (kgf, cm): phản lực trùng", MaxNodeDiff(r, rX, allCases, true), 0, 1e-4);
        }

        // ---------------- 8. khớp theo toạ độ khi tên khác ----------------
        {
            string csv = Path.Combine(outDir, "ri_sap_tenkhac.csv");
            ResultTableWriter.Write(r, csv, new ResultTableWriter.Options
            {
                Etabs = false, Geometry = true, FrameName = f => "F" + (f.Id + 1000).ToString(Inv), JointName = n => "N" + n.Id.ToString(Inv)
            });
            var ses = new ResultImportSession(p, null);
            ses.AddFile(csv);
            ses.Analyze();
            check("Tên khác: khớp mọi thanh theo toạ độ", ses.Matches.Count(m => m.Matched && m.Method == "toạ độ"), p.Frames.Count, 0);
            var rG = ses.Build();
            check("Tên khác: nội lực trùng", MaxFrameDiff(r, rG, allCases, out _), 0, 1e-5);
            check("Tên khác: phản lực trùng (khớp nút theo toạ độ)", MaxNodeDiff(r, rG, allCases, true), 0, 1e-5);
        }

        // ---------------- 9. dự án nhập từ .e2k: khớp theo tên gốc ----------------
        {
            string csv = Path.Combine(outDir, "ri_etabs_tengoc.csv");
            ResultTableWriter.Write(r, csv, new ResultTableWriter.Options { Etabs = true });
            var ses = new ResultImportSession(qe, null);
            ses.AddFile(csv);
            ses.Analyze();
            check("Tên gốc (.e2k): khớp mọi thanh", ses.Matches.Count(m => m.Matched && m.Method == "tên gốc"), qe.Frames.Count, 0);
            var rq = ses.Build();
            // so sánh theo toạ độ: thanh của qe ↔ thanh của p
            double worst = 0;
            foreach (var g in qe.Frames)
            {
                var f = p.Frames.First(x => SameEnds(p, x, qe, g));
                foreach (var cs in new[] { "TT", "TH1" })
                {
                    var fa = r.Get(cs).Frames[f.Id]; var fb = rq.Get(cs).Frames[g.Id];
                    for (int i = 0; i < fa.Count; i++) { var v = fb.At(fa.Stations[i]); for (int k = 0; k < 6; k++) worst = Math.Max(worst, Math.Abs(fa.F[i][k] - v[k])); }
                }
            }
            check("Tên gốc (.e2k): nội lực trùng", worst, 0, 1e-5);
            check("Tên gốc (.e2k): phản lực đủ mọi gối", rq.Get("TT").Reactions.Count, qe.Nodes.Count(n => n.HasRestraint), 0);
            check("Tên gốc (.e2k): tổng phản lực TT", TotalFz(rq, "TT"), TotalFz(r, "TT"), 1e-6);
        }

        // ---------------- 10. quy ước dấu: dầm đơn giản chịu tải đều (ETABS: M3 dương căng dưới) ----------------
        {
            Project Beam(bool reversed)
            {
                var q = new Project();
                q.Materials.Add(Material.Concrete("B25"));
                q.FrameSections.Add(FrameSection.Rect("D22x40", 220, 400));
                q.Patterns.Add(new LoadPattern { Name = "DEAD", Type = LoadType.Dead });
                q.Nodes.Add(new Node { Id = 1, X = 0, Y = 0, Z = 3, Restraint = Node.Fixed });
                q.Nodes.Add(new Node { Id = 2, X = 5, Y = 0, Z = 3, Restraint = Node.Fixed });
                var f = new FrameElement { Id = 1, NodeI = reversed ? 2 : 1, NodeJ = reversed ? 1 : 2, Section = "D22x40", Kind = FrameKind.Beam };
                f.PinBothEnds();
                q.Frames.Add(f);
                q.FrameLoads.Add(FrameLoad.Uniform(1, "DEAD", 8));
                return q;
            }
            // bảng ETABS viết tay: dầm B1 từ điểm 1 (x = 0) đến điểm 2 (x = 5), q = 8 kN/m: V2(I) = −20, M3(giữa) = +25
            string Table(bool geometry)
            {
                var sb = new StringBuilder();
                sb.Append("TABLE:  Element Forces - Beams\r\n");
                sb.Append("Story,Beam,Unique Name,Output Case,Case Type,Step Type,Station,P,V2,V3,T,M2,M3\r\n");
                sb.Append(",,,,,,m,kN,kN,kN,kN-m,kN-m,kN-m\r\n");
                for (int i = 0; i <= 4; i++)
                {
                    double x = i * 1.25, v2 = -20 + 8 * x, m3 = 20 * x - 4 * x * x;
                    sb.Append($"Story1,B1,7,DEAD,LinStatic,,{x.ToString(Inv)},0,{v2.ToString(Inv)},0,0,0,{m3.ToString(Inv)}\r\n");
                }
                if (geometry)
                {
                    sb.Append("\r\nTABLE:  Point Object Connectivity\r\nUnique Name,Story,Label,X,Y,Z\r\n,,,m,m,m\r\n1,Story1,1,0,0,3\r\n2,Story1,2,5,0,3\r\n");
                    sb.Append("\r\nTABLE:  Beam Object Connectivity\r\nUnique Name,Story,Beam,UniquePtI,UniquePtJ\r\n7,Story1,B1,1,2\r\n");
                }
                return sb.ToString();
            }
            foreach (bool rev in new[] { false, true })
            {
                var q = Beam(rev);
                var own = FrameAnalysis.Run(q).Get("DEAD").Frames[1];
                string path = Path.Combine(outDir, rev ? "ri_dau_nguoc.csv" : "ri_dau.csv");
                File.WriteAllText(path, Table(rev));
                var ri = ImportFile(q, path);
                var imp = ri.Get("DEAD").Frames[1];
                string tag = rev ? "thanh ngược chiều (khớp toạ độ)" : "thanh cùng chiều";
                check($"Dấu – {tag}: M3 giữa nhịp nhập = +qL²/8", imp.At(2.5)[5], 25, 1e-9);
                check($"Dấu – {tag}: M3 giữa nhịp = kết quả chương trình", imp.At(2.5)[5], own.At(2.5)[5], 1e-6);
                check($"Dấu – {tag}: V2 đầu I = kết quả chương trình (−qL/2)", imp.At(0)[1], own.At(0)[1], 1e-6);
                check($"Dấu – {tag}: V2 đầu J = kết quả chương trình (+qL/2)", imp.At(5)[1], own.At(5)[1], 1e-6);
            }
        }

        // ---------------- 11. cột khai báo đỉnh → chân: bảng ETABS (đầu I ở dưới) nhập về đúng ----------------
        {
            var q = Sample();
            var col = q.Frames.First(f => f.Kind == FrameKind.Column && q.Node(f.NodeI).Z > q.Node(f.NodeJ).Z - 10);
            (col.NodeI, col.NodeJ) = (col.NodeJ, col.NodeI);   // đảo chiều: đầu I ở trên
            q.Frames.First(f => f.Kind == FrameKind.Column && f.Id != col.Id).Angle = 90;   // một cột xoay 90°
            var rq = FrameAnalysis.Run(q);
            string csv = Path.Combine(outDir, "ri_etabs_cotnguoc.csv");
            ResultTableWriter.Write(rq, csv, new ResultTableWriter.Options { Etabs = true });
            var ri = ImportFile(q, csv);
            var cases = new[] { "TT", "GX", "TH1" };
            check("Cột ngược chiều + cột xoay 90°: nội lực nhập trùng kết quả chương trình", MaxFrameDiff(rq, ri, cases, out _), 0, 1e-5);
            var fo = rq.Get("GX").Frames[col.Id]; var fi = ri.Get("GX").Frames[col.Id];
            check("Cột ngược chiều: M3 tại đầu I (đỉnh cột) – GX", fi.At(0)[5], fo.At(0)[5], 1e-6);
            check("Cột ngược chiều: V2 tại đầu I – GX", fi.At(0)[1], fo.At(0)[1], 1e-6);
        }

        // ---------------- 12. SAFE: mô men dải thay phương pháp hệ số khi thiết kế sàn ----------------
        {
            var q = ModelBuilder.NewProject("Kiểm thử SAFE");
            ModelBuilder.SetGrid(q, new[] { 3.6 }, new[] { 4.5 });
            ModelBuilder.SetStories(q, new[] { 3.6 }, 0);
            ModelBuilder.BuildFromGrid(q, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40" });
            var own = SlabDesigner.Design(q, null, new List<string>());
            var sb = new StringBuilder();
            sb.Append("TABLE:  Strip Forces\r\nStrip,Station,OutputCase,CaseType,StepType,P,V2,T,M2,M3,GlobalX,GlobalY,WidthLeft,WidthRight\r\n");
            sb.Append(",m,,,,kN,kN,kN-m,kN-m,kN-m,m,m,m,m\r\n");
            // dải phương X qua giữa ô (y = 2,25): gối −8, nhịp +5 kN.m (bề rộng 1 m); dải phương Y (x = 1,8): gối −4, nhịp +3
            foreach (var (x, m) in new[] { (0.0, -8.0), (1.8, 5.0), (3.6, -8.0) })
                sb.Append($"CSA1,{x.ToString(Inv)},ULS1,Combination,,0,0,0,0,{m.ToString(Inv)},{x.ToString(Inv)},2.25,0.5,0.5\r\n");
            foreach (var (y, m) in new[] { (0.0, -4.0), (2.25, 3.0), (4.5, -4.0) })
                sb.Append($"CSB1,{y.ToString(Inv)},ULS1,Combination,,0,0,0,0,{m.ToString(Inv)},1.8,{y.ToString(Inv)},0.5,0.5\r\n");
            sb.Append("\r\nTABLE:  Punching Shear Design\r\nPoint,GlobalX,GlobalY,Combo,Ratio,Status\r\n,m,m,,,\r\nP1,0,0,ULS1,0.62,OK\r\nP2,3.6,0,ULS1,1.08,NG\r\n");
            string path = Path.Combine(outDir, "ri_safe_strip.csv");
            File.WriteAllText(path, sb.ToString());
            var ses = new ResultImportSession(q, null);
            ses.AddFile(path);
            ses.Analyze();
            var ri = ses.Build();
            check("SAFE: nguồn = SAFE", B(ri.Source == "SAFE"), 1, 0);
            check("SAFE: số ô sàn có mô men nhập", ri.SlabMoments.Count, 1, 0);
            var w = new List<string>();
            var ds = SlabDesigner.Design(q, ri, w);
            var d0 = ds.First();
            check("SAFE: M1 nhịp phương ngắn (X) = 5", d0.M1, 5, 1e-9);
            check("SAFE: MI gối phương ngắn = −8", d0.MI, -8, 1e-9);
            check("SAFE: M2 nhịp phương dài (Y) = 3", d0.M2, 3, 1e-9);
            check("SAFE: MII gối phương dài = −4", d0.MII, -4, 1e-9);
            check("SAFE: khác phương pháp hệ số", B(Math.Abs(own.First().M1 - 5) > 1e-6), 1, 0);
            check("SAFE: ghi chú nguồn mô men trong thiết kế sàn", B(d0.Messages.Any(m => m.Contains("SAFE"))), 1, 0);
            check("SAFE: đọc bảng chọc thủng (2 điểm, 1 không đạt)", ri.External.Punching.Count(x => x.Ratio > 1), 1, 0);
        }

        // ---------------- 13. bảng "phần mềm khác" ánh xạ cột ----------------
        {
            var sb = new StringBuilder();
            sb.Append("PhanTu;ViTri;TH;N;Q;Mx\r\n");
            var f0 = p.Frames.First(f => f.Kind == FrameKind.Beam);
            var ff = r.Get("TT").Frames[f0.Id];
            double L = p.Length(f0);
            for (int i = 0; i < ff.Count; i++)
                sb.Append($"{f0.Id};{(ff.Stations[i] / L).ToString("0.########", Inv)};TT;{(-ff.P(i)).ToString("R", Inv)};{ff.V2(i).ToString("R", Inv)};{(-ff.M3(i)).ToString("R", Inv)}\r\n");
            string path = Path.Combine(outDir, "ri_khac.csv");
            File.WriteAllText(path, sb.ToString());
            var ses = new ResultImportSession(p, r);
            ses.AddFile(path);
            var t = ses.Tables.First();
            check("Phần mềm khác: bảng chưa nhận diện", B(t.Kind == ExtTableKind.Unknown), 1, 0);
            ses.ApplyGeneric(new GenericMapping
            {
                Table = t.Name, File = t.File, ColElement = 0, ColStation = 1, ColCase = 2, ColP = 3, ColV2 = 4, ColM3 = 5,
                StationRelative = true, FlipM3 = true, FlipP = true
            });
            ses.Analyze();
            var rg = ses.Build();
            var fi = rg.Get("TT").Frames[f0.Id];
            double worst = 0;
            for (int i = 0; i < ff.Count; i++) { worst = Math.Max(worst, Math.Abs(fi.At(ff.Stations[i])[5] - ff.M3(i))); worst = Math.Max(worst, Math.Abs(fi.At(ff.Stations[i])[0] - ff.P(i))); }
            check("Phần mềm khác: M3, P theo trạm tương đối, đổi dấu", worst, 0, 1e-6);
            check("Phần mềm khác: thanh khác lấy nội lực của chương trình", rg.External.FilledFromOwn, p.Frames.Count - 1, 0);
        }
    }
}

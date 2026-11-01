using System.Diagnostics;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Steel;
using KetCauVXT.Drawing;
using KetCauVXT.Export;
using KetCauVXT.Loads;
using KetCauVXT.Model;
using KetCauVXT.Report;

// =====================================================================
// KIỂM CHỨNG LÕI TÍNH + CHẠY THỬ TOÀN BỘ CHUỖI CÔNG VIỆC
//   dotnet run --project tests/KetCauVXT.Tests              → chạy kiểm chứng + ví dụ nhà phố
//   dotnet run --project tests/KetCauVXT.Tests -- D:\out    → ghi kết quả ví dụ vào thư mục chỉ định
// Các giá trị kỳ vọng là lời giải giải tích (sức bền vật liệu / cơ học kết cấu).
// =====================================================================
Console.OutputEncoding = System.Text.Encoding.UTF8;
int pass = 0, fail = 0;

void Check(string name, double got, double exp, double tol = 1e-3)
{
    bool ok = Math.Abs(got - exp) <= tol * Math.Max(1, Math.Abs(exp));
    if (ok) pass++; else fail++;
    Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Red;
    Console.Write(ok ? "  ĐẠT  " : "  SAI  ");
    Console.ResetColor();
    Console.WriteLine($"{name}: {got:0.######} (kỳ vọng {exp:0.######})");
}

Project Single(double x2, double y2, string sec = "D22x40")
{
    var p = new Project();
    p.Materials.Add(Material.Concrete("B25"));
    p.FrameSections.Add(FrameSection.Rect("D22x40", 220, 400));
    p.FrameSections.Add(FrameSection.Rect("C30x30", 300, 300));
    p.Patterns.Add(new LoadPattern { Name = "P", Type = LoadType.Dead });
    p.Nodes.Add(new Node { Id = 1, X = 0, Y = 0, Z = 3, Restraint = Node.Fixed });
    p.Nodes.Add(new Node { Id = 2, X = x2, Y = y2, Z = 3, Restraint = Node.Fixed });
    p.Frames.Add(new FrameElement { Id = 1, NodeI = 1, NodeJ = 2, Section = sec, Kind = FrameKind.Beam });
    return p;
}

Console.WriteLine("1. Dầm ngàm hai đầu, tải đều 10 kN/m, L = 6 m");
{
    var p = Single(6, 0);
    p.FrameLoads.Add(FrameLoad.Uniform(1, "P", 10));
    var r = FrameAnalysis.Run(p).Get("P").Frames[1];
    Check("M3 đầu I = −qL²/12", r.At(0)[5], -30);
    Check("M3 giữa nhịp = qL²/24", r.At(3)[5], 15);
    Check("V2 đầu I = −qL/2", r.At(0)[1], -30);
    Check("V2 đầu J = +qL/2", r.At(6)[1], 30);
}

Console.WriteLine("2. Dầm khớp hai đầu (giải phóng M2, M3), phương Y, q = 8 kN/m, L = 5 m");
{
    var p = Single(0, 5);
    p.Frames[0].PinBothEnds();
    p.FrameLoads.Add(FrameLoad.Uniform(1, "P", 8));
    var res = FrameAnalysis.Run(p);
    var r = res.Get("P").Frames[1];
    Check("M3 giữa nhịp = qL²/8", r.At(2.5)[5], 25);
    Check("M3 đầu dầm = 0", r.At(0)[5], 0);
    Check("Phản lực FZ nút 1 = qL/2", res.Get("P").Reactions[1][2], 20);
}

Console.WriteLine("3. Cột công xôn 300×300 cao 4 m, lực ngang 20 kN tại đỉnh");
{
    var p = new Project();
    p.Materials.Add(Material.Concrete("B25"));
    p.FrameSections.Add(FrameSection.Rect("C30x30", 300, 300));
    p.Patterns.Add(new LoadPattern { Name = "P", Type = LoadType.Wind });
    p.Nodes.Add(new Node { Id = 1, Z = 0, Restraint = Node.Fixed });
    p.Nodes.Add(new Node { Id = 2, Z = 4 });
    p.Frames.Add(new FrameElement { Id = 1, NodeI = 1, NodeJ = 2, Section = "C30x30", Kind = FrameKind.Column });
    p.NodeLoads.Add(new NodeLoad { Node = 2, Pattern = "P", Fx = 20 });
    var res = FrameAnalysis.Run(p).Get("P");
    double E = 30e6, G = E / 2.4, I = 0.3 * Math.Pow(0.3, 3) / 12, As = 5.0 / 6 * 0.09;
    Check("Chuyển vị đỉnh = PL³/3EI + PL/GAs", res.Disp[2][0], 20 * 64 / (3 * E * I) + 20 * 4 / (G * As));
    Check("|M3| chân cột = P·H", Math.Abs(res.Frames[1].At(0)[5]), 80);
    Check("Phản lực FX = −P", res.Reactions[1][0], -20);
}

Console.WriteLine("4. Dầm ngàm tải tam giác 0 → 12 kN/m");
{
    var p = Single(6, 0);
    p.FrameLoads.Add(new FrameLoad { Element = 1, Pattern = "P", Points = { new[] { 0.0, 0.0 }, new[] { 1.0, 12.0 } } });
    var r = FrameAnalysis.Run(p).Get("P").Frames[1];
    Check("M đầu nhỏ = qL²/30", r.At(0)[5], -14.4, 2e-3);
    Check("M đầu lớn = qL²/20", r.At(6)[5], -21.6, 2e-3);
}

Console.WriteLine("5. Dầm ngàm, lực tập trung 50 kN giữa nhịp");
{
    var p = Single(6, 0);
    p.FrameLoads.Add(new FrameLoad { Element = 1, Pattern = "P", PointLoads = { new[] { 0.5, 50.0 } } });
    var r = FrameAnalysis.Run(p).Get("P").Frames[1];
    Check("M đầu = −PL/8", r.At(0)[5], -37.5);
    Check("M giữa = PL/8", r.At(3)[5], 37.5);
}

Console.WriteLine("6. Khung 1 tầng 4 cột, sàn cứng, dầm rất cứng: mỗi cột chịu F/4, M = V·h/2");
{
    var p = new Project();
    p.Materials.Add(Material.Concrete("B25"));
    var rigid = Material.Concrete("B25"); rigid.Name = "RIGID"; rigid.E *= 1e4; p.Materials.Add(rigid);
    p.FrameSections.Add(FrameSection.Rect("C30x30", 300, 300));
    var dr = FrameSection.Rect("DR", 220, 400, "RIGID"); p.FrameSections.Add(dr);
    p.SlabSections.Add(new SlabSection());
    p.Patterns.Add(new LoadPattern { Name = "P", Type = LoadType.Other });
    double[,] xy = { { 0, 0 }, { 5, 0 }, { 5, 4 }, { 0, 4 } };
    for (int i = 0; i < 4; i++)
    {
        p.Nodes.Add(new Node { Id = i + 1, X = xy[i, 0], Y = xy[i, 1], Z = 0, Restraint = Node.Fixed });
        p.Nodes.Add(new Node { Id = i + 5, X = xy[i, 0], Y = xy[i, 1], Z = 3 });
        p.Frames.Add(new FrameElement { Id = i + 1, NodeI = i + 1, NodeJ = i + 5, Section = "C30x30", Kind = FrameKind.Column });
        p.NodeLoads.Add(new NodeLoad { Node = i + 5, Pattern = "P", Fx = 5 });
    }
    for (int i = 0; i < 4; i++)
        p.Frames.Add(new FrameElement { Id = 5 + i, NodeI = 5 + i, NodeJ = 5 + (i + 1) % 4, Section = "DR", Kind = FrameKind.Beam });
    p.Slabs.Add(new Slab { Id = 1, Label = "S1", Nodes = { 5, 6, 7, 8 }, Section = "S100" });
    var res = FrameAnalysis.Run(p).Get("P");
    for (int i = 1; i <= 4; i++)
    {
        Check($"V2 cột {i}", res.Frames[i].At(0)[1], 5, 2e-3);
        Check($"|M3| chân cột {i}", Math.Abs(res.Frames[i].At(0)[5]), 7.5, 3e-3);
    }
}

Console.WriteLine("7. Cột thép vát (h 300→600) công xôn: mô men chân = P·H");
{
    var p = new Project();
    p.Materials.Add(Material.Steel("SS400"));
    p.FrameSections.Add(FrameSection.WeldedISection("V", 300, 600, 200, 10, 6));
    p.Patterns.Add(new LoadPattern { Name = "P", Type = LoadType.Wind });
    p.Nodes.Add(new Node { Id = 1, Restraint = Node.Fixed });
    p.Nodes.Add(new Node { Id = 2, Z = 6 });
    p.Frames.Add(new FrameElement { Id = 1, NodeI = 1, NodeJ = 2, Section = "V", Kind = FrameKind.SteelColumn, Angle = 0 });
    p.NodeLoads.Add(new NodeLoad { Node = 2, Pattern = "P", Fx = 10 });
    var res = FrameAnalysis.Run(p).Get("P");
    Check("|M3| chân", Math.Abs(res.Frames[1].At(0)[5]), 60);
    Check("|M3| giữa", Math.Abs(res.Frames[1].At(3)[5]), 30);
    // chuyển vị đỉnh bằng tích phân số ∫ M·m/EI
    var sec = p.FrameSections[0]; double E = 210e6, d = 0; int n = 600;
    for (int k = 0; k < n; k++)
    {
        double x = (k + 0.5) * 6.0 / n;
        d += 10 * (6 - x) * (6 - x) / (E * sec.Props(x / 6).I33) * 6.0 / n;
    }
    Check("Chuyển vị đỉnh (sai số chia đoạn ≤ 3%)", res.Disp[2][0], d, 0.03);
}

Console.WriteLine("8. Nhà phố 5×13.5 m, 4 tầng – cân bằng phản lực và phân tải sàn");
var house = ModelBuilder.NewProject("Nhà phố mẫu 4 tầng");
house.Location = "Thái Nguyên";
ModelBuilder.SetGrid(house, new[] { 5.0 }, new[] { 4.5, 4.5, 4.5 });
ModelBuilder.SetStories(house, new[] { 3.9, 3.3, 3.3, 3.3 }, -0.6);
ModelBuilder.BuildFromGrid(house, new ModelBuilder.GridFrameOptions
{
    ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5
});
AnalysisResults hr = null;
{
    var sw = Stopwatch.StartNew();
    hr = FrameAnalysis.Run(house);
    Console.WriteLine($"   {house.Nodes.Count} nút, {house.Frames.Count} thanh, {hr.Equations} phương trình, {sw.ElapsedMilliseconds} ms");
    foreach (var w in hr.Warnings) Console.WriteLine("   · " + w);
    foreach (var pat in house.Patterns)
    {
        double applied = 0, appliedX = 0, appliedY = 0;
        if (pat.SelfWeight != 0)
            foreach (var f in house.Frames)
            {
                var s = house.Section(f.Section);
                applied += pat.SelfWeight * house.Material(s.Material).UnitWeight * s.Props().A * house.Length(f);
            }
        foreach (var fl in house.FrameLoads.Where(l => l.Pattern == pat.Name && l.Dir == LoadDir.Gravity))
        {
            double L = house.Length(house.Frame(fl.Element));
            for (int i = 0; i + 1 < fl.Points.Count; i++)
                applied += (fl.Points[i][1] + fl.Points[i + 1][1]) / 2 * (fl.Points[i + 1][0] - fl.Points[i][0]) * L;
        }
        foreach (var nl in house.NodeLoads.Where(l => l.Pattern == pat.Name)) { applied -= nl.Fz; appliedX += nl.Fx; appliedY += nl.Fy; }
        var rc = hr.Get(pat.Name).Reactions.Values;
        Check($"{pat.Name}: ΣFZ phản lực = Σ tải đứng", rc.Sum(r => r[2]), applied, 1e-6);
        if (Math.Abs(appliedX) > 0) Check($"{pat.Name}: ΣFX phản lực = −Σ tải ngang", rc.Sum(r => r[0]), -appliedX, 1e-6);
        if (Math.Abs(appliedY) > 0) Check($"{pat.Name}: ΣFY phản lực = −Σ tải ngang", rc.Sum(r => r[1]), -appliedY, 1e-6);
    }
    // tải sàn tầng 2 dồn về dầm = q·diện tích
    double area = 5 * 13.5, q = 1.2;
    double got = house.FrameLoads.Where(l => l.Source == "SLAB" && l.Pattern == "HTHT")
        .Where(l => house.Frame(l.Element).Story == "T2")
        .Sum(fl =>
        {
            double L = house.Length(house.Frame(fl.Element)), s = 0;
            for (int i = 0; i + 1 < fl.Points.Count; i++) s += (fl.Points[i][1] + fl.Points[i + 1][1]) / 2 * (fl.Points[i + 1][0] - fl.Points[i][0]) * L;
            return s;
        });
    Check("Tải hoàn thiện sàn tầng 2 dồn về dầm = q·A", got, q * area, 1e-9);
}

Console.WriteLine("9. Khung thép tiền chế nhịp 24 m – đối xứng dưới tải đứng");
var shed = ModelBuilder.NewProject("Nhà xưởng tiền chế mẫu");
shed.FrameSections.Clear();
ModelBuilder.BuildPortalFrames(shed, new ModelBuilder.PortalOptions { Span = 24, EaveHeight = 7, Bays = 4, BaySpacing = 6 });
AnalysisResults sr = FrameAnalysis.Run(shed);
{
    var tt = sr.Get("TT");
    var bases = shed.Nodes.Where(n => n.IsSupport && Math.Abs(n.Y - 6) < 1e-6 && Math.Abs(n.Z - shed.BaseElevation) < 1e-6).OrderBy(n => n.X).ToList();
    Check("Phản lực ngang hai chân cột trái/phải trực đối", tt.Reactions[bases[0].Id][0], -tt.Reactions[bases[1].Id][0], 1e-6);
    Check("Phản lực đứng hai chân bằng nhau", tt.Reactions[bases[0].Id][2], tt.Reactions[bases[1].Id][2], 1e-6);
}

Console.WriteLine();
Console.ForegroundColor = fail == 0 ? ConsoleColor.Green : ConsoleColor.Red;
Console.WriteLine($"KẾT QUẢ KIỂM CHỨNG: {pass} đạt, {fail} sai");
Console.ResetColor();

// =====================================================================
// CHUỖI ĐẦY ĐỦ: thiết kế → bản vẽ → thuyết minh → bảng tính → ETABS/SAP
// =====================================================================
string outDir = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau");
Directory.CreateDirectory(outDir);
void Step(string name, Action a)
{
    try { a(); Console.WriteLine("  ✓ " + name); }
    catch (Exception ex) { Console.WriteLine($"  ✗ {name}: {ex.GetType().Name}: {ex.Message}"); fail++; }
}

foreach (var (proj, res, tag) in new[] { (house, hr, "NhaPho"), (shed, sr, "NhaXuong") })
{
    Console.WriteLine($"\n== {proj.Name} → {outDir}");
    string dir = Path.Combine(outDir, tag);
    Directory.CreateDirectory(dir);
    DesignSummary ds = null;
    Step("Lưu mô hình .kcvxt", () => proj.Save(Path.Combine(dir, tag + ".kcvxt")));
    Step("Thiết kế BTCT", () => ds = ConcreteDesigner.Design(proj, res));
    Step("Kiểm tra kết cấu thép", () => { ds ??= new DesignSummary(); SteelDesigner.Check(proj, res, ds); });
    if (ds != null)
    {
        int bad = ds.All.Count(m => !m.Ok);
        Console.WriteLine($"    dầm {ds.Beams.Count}, cột {ds.Columns.Count}, sàn {ds.Slabs.Count}, móng {ds.Footings.Count}, thanh thép {ds.Steel.Count}; không đạt: {bad}");
        foreach (var w in ds.Warnings.Take(10)) Console.WriteLine("    · " + w);
        // bảng tóm tắt: mỗi cấu kiện – hạng mục kiểm tra bất lợi nhất
        foreach (var m in ds.All)
        {
            var worst = m.Checks.OrderByDescending(c => c.Ratio).FirstOrDefault();
            string wtxt = worst == null ? "-" : $"{worst.Name}: {worst.Demand:0.##}/{worst.Capacity:0.##} {worst.Unit} (k={worst.Ratio:0.00})";
            Console.WriteLine($"    {(m.Ok ? "ĐẠT" : "KHÔNG ĐẠT"),-10} {m.Name,-12} {wtxt}");
            foreach (var msg in m.Messages.Where(x => x.StartsWith("LỖI")).Take(3)) Console.WriteLine("               " + msg);
        }
    }
    Step("Bản vẽ DXF", () => DrawingSet.ExportAll(proj, ds, Path.Combine(dir, "BanVe")));
    Step("Thuyết minh Word", () => CalculationReport.Export(proj, res, ds, Path.Combine(dir, "ThuyetMinh.docx")));
    Step("Bảng tính Excel", () => ExcelReport.Export(proj, res, ds, Path.Combine(dir, "BangTinh.xlsx")));
    Step("Nội lực CSV", () => ForceTables.WriteCsv(res, Path.Combine(dir, "NoiLuc")));
    Step("ETABS .e2k", () => E2kExporter.Export(proj, Path.Combine(dir, tag + ".e2k")));
    Step("SAP2000 .s2k", () => S2kExporter.Export(proj, Path.Combine(dir, tag + ".s2k")));
}
return fail == 0 ? 0 : 1;

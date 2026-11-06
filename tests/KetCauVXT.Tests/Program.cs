using System.Diagnostics;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Steel;
using KetCauVXT.Drawing;
using KetCauVXT.Export;
using KetCauVXT.Import;
using KetCauVXT.Import.Arch;
using KetCauVXT.Loads;
using KetCauVXT.Model;
using KetCauVXT.Report;
using KetCauVXT.Tests;

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

Console.WriteLine("10. Nhà 7 tầng 3×3 nhịp – gió + động đất, cân bằng phản lực, chạy ổn định");
var tall = ModelBuilder.NewProject("Nhà 7 tầng mẫu");
tall.Location = "Thái Nguyên";
ModelBuilder.SetGrid(tall, new[] { 4.2, 4.2, 4.2 }, new[] { 5.0, 4.0, 5.0 });
ModelBuilder.SetStories(tall, new[] { 3.9, 3.6, 3.3, 3.3, 3.3, 3.3, 3.3 }, -1.0);
ModelBuilder.BuildFromGrid(tall, new ModelBuilder.GridFrameOptions
{
    ColumnSection = "C30x30", BeamSectionX = "D22x40", BeamSectionY = "D22x50", SlabSection = "S120", PerimeterWallLoad = 7.5
});
tall.Seismic.Enabled = true; tall.Seismic.AgR = 0.0611; tall.Seismic.Soil = "C";
foreach (var n in new[] { "DDX", "DDY" })
    if (tall.Pattern(n) == null) tall.Patterns.Add(new LoadPattern { Name = n, Type = LoadType.Seismic, Gamma = 1.0 });
ComboGenerator.Generate(tall);
AnalysisResults tr = null;
{
    var sw = Stopwatch.StartNew();
    tr = FrameAnalysis.Run(tall);
    Console.WriteLine($"   {tall.Nodes.Count} nút, {tall.Frames.Count} thanh, {tr.Equations} phương trình, {tall.Combinations.Count} tổ hợp, {sw.ElapsedMilliseconds} ms");
    foreach (var w in tr.Warnings.Take(5)) Console.WriteLine("   · " + w);
    foreach (var lf in tr.LateralForces.Where(x => x.Pattern == "DDX").Take(8)) Console.WriteLine($"   · {lf.Pattern} {lf.Story} z={lf.Elevation:0.00}: Fx={lf.Fx:0.0} kN, Fy={lf.Fy:0.0} kN {lf.Note}");
    foreach (var pn in new[] { "GX", "GY", "DDX", "DDY" })
    {
        var cr = tr.Get(pn);
        double sx = tall.NodeLoads.Where(l => l.Pattern == pn).Sum(l => l.Fx), sy = tall.NodeLoads.Where(l => l.Pattern == pn).Sum(l => l.Fy);
        Check($"{pn}: có tải ngang", Math.Abs(sx) + Math.Abs(sy) > 1 ? 1 : 0, 1, 0);
        Check($"{pn}: ΣFX phản lực = −Σ tải", cr.Reactions.Values.Sum(r => r[0]), -sx, 1e-6);
        Check($"{pn}: ΣFY phản lực = −Σ tải", cr.Reactions.Values.Sum(r => r[1]), -sy, 1e-6);
    }
    bool finite = tall.Combinations.Where(c => c.Type != ComboType.Envelope).All(c =>
    {
        var cr = tr.Get(c.Name);
        return cr.Disp.Values.All(d => d.All(double.IsFinite));
    });
    Check("Mọi tổ hợp cho chuyển vị hữu hạn", finite ? 1 : 0, 1, 0);
}

Console.WriteLine("11. Lưu → mở lại mô hình, phân tích lại cho kết quả trùng khớp");
{
    string tmp = Path.Combine(Path.GetTempPath(), "kcvxt_roundtrip.kcvxt");
    tall.Save(tmp);
    var re = Project.Load(tmp);
    var rr = FrameAnalysis.Run(re);
    double maxDiff = 0;
    foreach (var c in new[] { "TT", "HT", "GX", "DDX" })
        foreach (var kv in tr.Get(c).Disp)
            for (int d = 0; d < 6; d++) maxDiff = Math.Max(maxDiff, Math.Abs(kv.Value[d] - rr.Get(c).Disp[kv.Key][d]));
    Check("Sai khác chuyển vị lớn nhất sau khi mở lại", maxDiff, 0, 1e-9);
    Check("Số nút / thanh giữ nguyên", re.Nodes.Count * 1000 + re.Frames.Count, tall.Nodes.Count * 1000 + tall.Frames.Count, 0);
}

Console.WriteLine("12. Mô hình lỗi phải báo lỗi rõ ràng, không sập chương trình");
{
    var bad = ModelBuilder.NewProject("Lỗi");
    bool clear = false;
    try { FrameAnalysis.Run(bad); }
    catch (InvalidOperationException ex) { clear = ex.Message.Length > 10; Console.WriteLine("   · " + ex.Message.Split('\n')[0]); }
    catch (Exception ex) { Console.WriteLine("   · Ngoại lệ không mong đợi: " + ex.GetType().Name); }
    Check("Mô hình rỗng → InvalidOperationException có thông báo", clear ? 1 : 0, 1, 0);

    // cơ cấu (cột khớp 2 đầu, không giằng) → phải báo lỗi hoặc cảnh báo, không treo
    var mech = ModelBuilder.NewProject("Cơ cấu");
    var n1 = mech.GetOrAddNode(0, 0, 0); n1.Restraint = Node.Pinned;
    var n2 = mech.GetOrAddNode(0, 0, 3);
    mech.Frames.Add(new FrameElement { Id = 1, NodeI = n1.Id, NodeJ = n2.Id, Kind = FrameKind.Column, Section = "C22x22" });
    mech.NodeLoads.Add(new NodeLoad { Node = n2.Id, Pattern = "GX", Fx = 10 });
    ComboGenerator.Generate(mech);
    bool handled = false;
    try
    {
        var mr = FrameAnalysis.Run(mech);
        handled = mr.Warnings.Count > 0 || mr.Get("GX").Disp.Values.Any(d => d.Any(v => Math.Abs(v) > 1e3) || d.Any(v => !double.IsFinite(v)));
        Console.WriteLine("   · Cảnh báo: " + string.Join(" | ", mr.Warnings.Take(2)));
    }
    catch (Exception ex) when (ex is InvalidOperationException || ex is SingularStiffnessException)
    {
        handled = true; Console.WriteLine("   · " + ex.Message.Split('\n')[0]);
    }
    Check("Kết cấu biến hình → được phát hiện", handled ? 1 : 0, 1, 0);
}

Console.WriteLine("13. Bản vẽ kiến trúc sơ bộ → triển khai kết cấu (nhà phố 5×15 m, 3 tầng + tum, mặt bằng đặt cạnh nhau trong 1 file DXF)");
var archProj = ModelBuilder.NewProject("Nhà phố từ bản vẽ kiến trúc");
archProj.Location = "Thái Nguyên";
AnalysisResults ar = null;
string archDxf = Path.Combine(Path.GetTempPath(), "KienTrucMau.dxf");
{
    File.WriteAllText(archDxf, ArchSample.Build(), new System.Text.UTF8Encoding(false));
    var ao = new ArchImportOptions();
    var dwg = ArchPlanReader.Read(archDxf, ao);
    foreach (var line in dwg.Info) Console.WriteLine("   · " + line);
    foreach (var w in dwg.Warnings) Console.WriteLine("   ! " + w);
    foreach (var f in dwg.Floors) foreach (var n in f.Notes) Console.WriteLine($"   ! [{f.Title}] {n}");
    Check("Số mặt bằng nhận diện", dwg.Floors.Count, 4, 0);
    Check("Số tầng đọc từ tên hình (1,2,3,tum)", dwg.Floors.Count(f => f.FloorNo is 1 or 2 or 3) + (dwg.Floors.Any(f => f.IsTum) ? 1 : 0), 4, 0);
    var f1 = dwg.Floors.First(f => f.FloorNo == 1);
    var f2 = dwg.Floors.First(f => f.FloorNo == 2);
    var f3 = dwg.Floors.First(f => f.FloorNo == 3);
    Check("Tên tầng 3 gõ TCVN3 được chuyển sang Unicode", f3.Title.Contains("MẶT BẰNG TẦNG 3") ? 1 : 0, 1, 0);
    Check("Trục tầng 1: 2 trục X tên 1,2", f1.Grids.Count(g => g.Dir == GridDir.X && (g.Name == "1" || g.Name == "2")), 2, 0);
    Check("Trục tầng 1: 4 trục Y tên A–D", f1.Grids.Count(g => g.Dir == GridDir.Y && "ABCD".Contains(g.Name)), 4, 0);
    double area1 = Math.Abs(ImportGeometry.SignedArea(f1.Outline));
    // bản vẽ mẫu vẽ tường tới tim trục (thiếu ô góc) → đường bao nằm giữa 5×15 và 5,22×15,22
    Check("Đường bao tầng 1 kín dù mặt tiền có cửa cuốn 3,8 m (≥ 5×15 m²)", area1 >= 74.9 && area1 <= 79.5 ? 1 : 0, 1, 0);
    Check("Tầng 2 nhận ban công", f2.Zones.Count(z => z.Kind == RoomKind.Balcony), 1, 0);
    Check("Tầng 1 nhận cầu thang", f1.Zones.Count(z => z.Kind == RoomKind.Stair), 1, 0);
    Check("Tầng 2 có tường 220 và 110", (f2.Walls.Any(w => Math.Abs(w.Thickness - 0.22) < 0.005) ? 1 : 0) + (f2.Walls.Any(w => Math.Abs(w.Thickness - 0.11) < 0.005) ? 1 : 0), 2, 0);
    Check("Phòng WC tầng 2 được nhận dạng", f2.Rooms.Count(r => r.Kind == RoomKind.WC), 1, 0);
    Check("Tầng 2 dời về cùng hệ toạ độ tầng 1 (trục 1 tại x = 0)", f2.Grids.First(g => g.Name == "1").Coord, 0, 1e-6);

    var prop = ArchToStructure.Propose(dwg, ao);
    foreach (var n in prop.Notes) Console.WriteLine("   · " + n);
    foreach (var w in prop.Warnings) Console.WriteLine("   ! " + w);
    Console.WriteLine("   Cao trình: " + string.Join(", ", prop.Levels.Select(l => $"{l.Name} +{l.Elevation:0.000} ({l.Beams.Count} dầm, {l.Slabs.Count} ô sàn, {l.Holes.Count} lỗ)")));
    foreach (var c in prop.Columns) Console.WriteLine($"   Cột ({c.X:0.00};{c.Y:0.00}) → {c.TopLevel}: " + string.Join(" ", c.SectionByLevel.Select(kv => kv.Key + ":" + kv.Value)) + $" A={c.TributaryArea:0.0} m²");
    foreach (var lv in prop.Levels)
        foreach (var b in lv.Beams.Where(b => b.Role != BeamRole.Main))
            Console.WriteLine($"   {lv.Name} {b.Role,-10} ({b.Ax:0.00};{b.Ay:0.00})–({b.Bx:0.00};{b.By:0.00}) {b.Section} tường {b.WallLoad:0.0} kN/m {b.Note}");
    Check("Các cao trình T2, T3, MAI, MAITUM", string.Join(",", prop.Levels.Select(l => l.Name)) == "T2,T3,MAI,MAITUM" ? 1 : 0, 1, 0);
    Check("8 cột tại giao trục", prop.Columns.Count, 8, 0);
    Check("4 cột lên tới mái tum", prop.Columns.Count(c => c.TopLevel == "MAITUM"), 4, 0);
    var t2 = prop.Levels[0];
    Check("T2: 2 dầm công xôn ban công", t2.Beams.Count(b => b.Role == BeamRole.Cantilever), 2, 0);
    Check("T2: lỗ cầu thang", t2.Holes.Count(h => h.Kind == RoomKind.Stair), 1, 0);
    Check("T2: dầm trên trục giữa 2 cột là dầm chính", t2.Beams.Count(b => b.Role == BeamRole.Main && Math.Abs(b.Ay - b.By) < 1e-6 && Math.Abs(b.Ay - 5.0) < 1e-6), t2.Beams.Count(b => Math.Abs(b.Ay - b.By) < 1e-6 && Math.Abs(b.Ay - 5.0) < 1e-6), 0);
    Check("Mái chính có tường chắn mái ở mép ngoài tum", prop.Levels.First(l => l.Name == "MAI").Beams.Any(b => Math.Abs(b.Ay - 15) < 1e-6 && Math.Abs(b.By - 15) < 1e-6 && b.WallLoad > 1) ? 1 : 0, 1, 0);
    Check("T2: dầm phụ đỡ tường WC", t2.Beams.Any(b => b.Role == BeamRole.Secondary && Math.Abs(b.Ax - 3.5) < 0.06 && Math.Abs(b.Bx - 3.5) < 0.06) ? 1 : 0, 1, 0);
    Check("T2: mọi ô sàn có cạnh ngắn ≤ 4,5 m", t2.Slabs.All(q => Math.Min(q.X1 - q.X0, q.Y1 - q.Y0) <= 4.55) ? 1 : 0, 1, 0);
    Check("T2: ô WC nhận tải hoàn thiện 2,0", t2.Slabs.Count(q => q.Kind == RoomKind.WC && q.Finish >= 2.0 - 1e-9) >= 1 ? 1 : 0, 1, 0);
    Check("Mái: hoạt tải mái 0,75", prop.Levels.First(l => l.Name == "MAITUM").Slabs.All(q => Math.Abs(q.Live - 0.75) < 1e-9) ? 1 : 0, 1, 0);

    var rep = ArchToStructure.Apply(archProj, dwg, prop, ao);
    Console.WriteLine("   " + rep.ToString().Replace("\n", "\n   "));
    var sw = Stopwatch.StartNew();
    ar = FrameAnalysis.Run(archProj);
    Console.WriteLine($"   Phân tích: {archProj.Nodes.Count} nút, {archProj.Frames.Count} thanh, {ar.Equations} phương trình, {sw.ElapsedMilliseconds} ms");
    double applied = archProj.FrameLoads.Where(l => l.Pattern == "HTHT" && l.Source != "SLAB").Sum(fl =>
    {
        double L = archProj.Length(archProj.Frame(fl.Element)), sm = 0;
        for (int i = 0; i + 1 < fl.Points.Count; i++) sm += (fl.Points[i][1] + fl.Points[i + 1][1]) / 2 * (fl.Points[i + 1][0] - fl.Points[i][0]) * L;
        return sm;
    });
    foreach (var al in archProj.AreaLoads.Where(a => a.Pattern == "HTHT"))
    {
        var sl = archProj.Slabs.First(q => q.Id == al.Slab);
        var pts = sl.Nodes.Select(id => (archProj.Node(id).X, archProj.Node(id).Y)).ToList();
        applied += al.Q * Math.Abs(ImportGeometry.SignedArea(pts));
    }
    Check("Tường từ bản vẽ được khai báo thành đoạn tường", archProj.Walls.Count > 10 ? 1 : 0, 1, 0);
    Check("HTHT (tường + hoàn thiện + cầu thang): ΣFZ phản lực = Σ tải", ar.Get("HTHT").Reactions.Values.Sum(r => r[2]), applied, 1e-6);
    Check("Mọi chuyển vị hữu hạn", archProj.Combinations.Where(c => c.Type != ComboType.Envelope).All(c => ar.Get(c.Name).Disp.Values.All(v => v.All(double.IsFinite))) ? 1 : 0, 1, 0);
    var rz = AutoResize.Run(archProj, 12);
    foreach (var l in rz.Log) Console.WriteLine("   · " + l);
    Check("Tự tăng tiết diện: mọi dầm, cột, sàn đạt", rz.AllOk ? 1 : 0, 1, 0);
    ar = rz.Analysis;
}

Console.WriteLine("14. Sửa mô hình trên mặt bằng: cầu thang, giếng trời, sàn vệ sinh, ban công, xoá/thêm sàn");
{
    var ed = ModelBuilder.NewProject("Sửa mô hình");
    ModelBuilder.SetGrid(ed, new[] { 5.0 }, new[] { 4.5, 4.5, 4.5 });
    ModelBuilder.SetStories(ed, new[] { 3.9, 3.3, 3.3, 3.3 }, -0.6);
    ModelBuilder.BuildFromGrid(ed, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5 });
    string st = "T3";
    int slabs0 = ed.Slabs.Count(q => q.Story == st);
    var r1 = ModelEdit.AddOpening(ed, st, 2.5, 6.75, OpeningKind.Stair);
    Console.WriteLine("   · " + r1.Message);
    Check("Cầu thang: bỏ 1 ô sàn", ed.Slabs.Count(q => q.Story == st), slabs0 - 1, 0);
    Check("Cầu thang: tải bản thang lên dầm viền", ed.FrameLoads.Count(l => l.Source == "OPEN1") > 0 ? 1 : 0, 1, 0);
    var r2 = ModelEdit.AddOpening(ed, st, 2.5, 11.25, OpeningKind.Void);
    Console.WriteLine("   · " + r2.Message);
    Check("Giếng trời: 2 lỗ sàn trên tầng", ed.Openings.Count(o => o.Story == st), 2, 0);
    var wc = ModelEdit.SlabAt(ed, st, 2.5, 2.25);
    Console.WriteLine("   · " + ModelEdit.SetSlabUse(ed, wc, SlabUse.WC).Message);
    Check("Sàn vệ sinh: công năng WC", ModelEdit.UseOf(ed, wc) == SlabUse.WC ? 1 : 0, 1, 0);
    var edge = ModelEdit.StoryBeams(ed, st).First(f => Math.Abs(ed.Node(f.NodeI).Y) < 1e-6 && Math.Abs(ed.Node(f.NodeJ).Y) < 1e-6);
    var r3 = ModelEdit.AddBalcony(ed, st, edge, 1.2, 1.0);
    Console.WriteLine("   · " + r3.Message);
    Check("Ban công: có ô sàn ban công", r3.NewSlabs.Count, 1, 0);
    Check("Ban công: đầu công xôn tại y = −1,2 m", ed.Nodes.Count(n => n.Story == st && Math.Abs(n.Y + 1.2) < 1e-6), 2, 0);
    Check("Ban công: tải ban công 2,0 kN/m²", ed.AreaLoads.Any(a => a.Slab == r3.NewSlabs[0] && a.Pattern == "HT" && Math.Abs(a.Q - 2.0) < 1e-9) ? 1 : 0, 1, 0);
    var r4 = ModelEdit.AddSlab(ed, st, 2.5, 2.25);
    Check("Không cho thêm sàn chồng lên sàn có sẵn", r4.Ok ? 0 : 1, 1, 0);
    var r5 = ModelEdit.RemoveOpening(ed, ed.Openings.First(o => o.Kind == OpeningKind.Void));
    Console.WriteLine("   · " + r5.Message);
    Check("Xoá giếng trời → đặt lại ô sàn", ModelEdit.SlabAt(ed, st, 2.5, 11.25) != null ? 1 : 0, 1, 0);
    var dl = ModelEdit.DeleteSlab(ed, ModelEdit.SlabAt(ed, st, 2.5, 11.25));
    var ad = ModelEdit.AddSlab(ed, st, 2.5, 11.25, SlabUse.Storage);
    Check("Xoá rồi thêm lại ô sàn (kho)", dl.Ok && ad.Ok ? 1 : 0, 1, 0);
    string tmp = Path.Combine(Path.GetTempPath(), "suamohinh.kcvxt");
    ed.Save(tmp);
    Check("Lưu/mở lại giữ lỗ sàn", Project.Load(tmp).Openings.Count, ed.Openings.Count, 0);
    double applied = ed.FrameLoads.Where(l => l.Pattern == "HTHT").Sum(fl =>
    {
        double L = ed.Length(ed.Frame(fl.Element)), sm = 0;
        for (int i = 0; i + 1 < fl.Points.Count; i++) sm += (fl.Points[i][1] + fl.Points[i + 1][1]) / 2 * (fl.Points[i + 1][0] - fl.Points[i][0]) * L;
        return sm;
    });
    foreach (var al in ed.AreaLoads.Where(a => a.Pattern == "HTHT"))
    {
        var sl = ed.Slabs.First(q => q.Id == al.Slab);
        applied += al.Q * Math.Abs(ImportGeometry.SignedArea(sl.Nodes.Select(id => (ed.Node(id).X, ed.Node(id).Y)).ToList()));
    }
    var er = FrameAnalysis.Run(ed);
    Check("Sau khi sửa: HTHT ΣFZ phản lực = Σ tải", er.Get("HTHT").Reactions.Values.Sum(r => r[2]), applied, 1e-6);
    var eds = ConcreteDesigner.Design(ed, er);
    Check("Thiết kế chạy được sau khi sửa (có dầm công xôn ban công)", eds.Beams.Any(b => b.Frames.Any(id => r3.NewFrames.Contains(id))) ? 1 : 0, 1, 0);
}

Console.WriteLine("15. Tường khai báo theo vị trí (110/220, trát/không trát, có/không cửa) và cấu tạo sàn");
{
    var wp = ModelBuilder.NewProject("Tường + cấu tạo sàn");
    ModelBuilder.SetGrid(wp, new[] { 5.0 }, new[] { 4.5, 4.5, 4.5 });
    ModelBuilder.SetStories(wp, new[] { 3.9, 3.3, 3.3, 3.3 }, -0.6);
    ModelBuilder.BuildFromGrid(wp, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40" });
    string st = "T3";
    var b0 = ModelEdit.StoryBeams(wp, st).First(f => Math.Abs(wp.Node(f.NodeI).Y) < 1e-6 && Math.Abs(wp.Node(f.NodeJ).Y) < 1e-6);
    Console.WriteLine("   · " + ModelEdit.AssignWallToBeam(wp, b0, "Tường 220 trát 2 mặt").Message);
    var b1 = ModelEdit.StoryBeams(wp, st).First(f => Math.Abs(wp.Node(f.NodeI).Y - 13.5) < 1e-6 && Math.Abs(wp.Node(f.NodeJ).Y - 13.5) < 1e-6);
    Console.WriteLine("   · " + ModelEdit.AssignWallToBeam(wp, b1, "Tường 110 không trát", 0, 3.0).Message);
    Console.WriteLine("   · " + ModelEdit.AddWall(wp, st, 2.5, 0, 2.5, 4.5, "Tường 110 trát 2 mặt").Message);
    Console.WriteLine("   · " + ModelEdit.AddWall(wp, "MAI", 0, 0, 5, 0, "Tường 110 trát 2 mặt").Message);
    var slWc = ModelEdit.SlabAt(wp, st, 2.5, 6.75);
    Console.WriteLine("   · " + ModelEdit.SetBuildUp(wp, slWc, "Sàn vệ sinh").Message);
    var wr = FrameAnalysis.Run(wp);
    var fl0 = wp.FrameLoads.First(l => l.Source == "WALL" && l.Element == b0.Id);
    double h0 = 3.3 - 0.35;
    Check("Tường 220 trát 2 mặt trên dầm: q = (0,22·18 + 2·0,015·18)·H", fl0.Points[0][1], (0.22 * 18 + 2 * 0.015 * 18) * h0, 1e-6);
    var fl1 = wp.FrameLoads.First(l => l.Source == "WALL" && l.Element == b1.Id);
    Check("Tường 110 không trát, trừ 3 m² cửa", fl1.Points[0][1], 0.11 * 18 * h0 * (1 - 3.0 / (5.0 * h0)), 1e-6);
    Check("Tường không có dầm đỡ → tải lên ô sàn", wp.AreaLoads.Count(a => a.Source == "WALL") >= 1 ? 1 : 0, 1, 0);
    var flp = wp.FrameLoads.First(l => l.Source == "WALL" && wp.Frame(l.Element).Story == "MAI");
    Check("Tường trên mái tự lấy cao 0,9 m (tường chắn mái)", flp.Points[0][1], (0.11 * 18 + 2 * 0.015 * 18) * 0.9, 1e-6);
    Check("Cấu tạo sàn vệ sinh: Σ lớp = 2,01 kN/m²", wp.AreaLoads.Where(a => a.Slab == slWc.Id && a.Pattern == "HTHT").Sum(a => a.Q), 2.01, 1e-6);
    double appl = wp.FrameLoads.Where(l => l.Pattern == "HTHT" && l.Source != "SLAB").Sum(fl =>
    {
        double L = wp.Length(wp.Frame(fl.Element)), sm = 0;
        for (int i = 0; i + 1 < fl.Points.Count; i++) sm += (fl.Points[i][1] + fl.Points[i + 1][1]) / 2 * (fl.Points[i + 1][0] - fl.Points[i][0]) * L;
        return sm;
    });
    foreach (var al in wp.AreaLoads.Where(a => a.Pattern == "HTHT"))
    {
        var sl = wp.Slabs.First(q => q.Id == al.Slab);
        appl += al.Q * Math.Abs(ImportGeometry.SignedArea(sl.Nodes.Select(id => (wp.Node(id).X, wp.Node(id).Y)).ToList()));
    }
    Check("Tường + cấu tạo sàn: ΣFZ phản lực = Σ tải", wr.Get("HTHT").Reactions.Values.Sum(r => r[2]), appl, 1e-6);
    var bAdd = ModelEdit.AddBeam(wp, st, 0, 2.25, 5, 2.25, "D22x30");
    Console.WriteLine("   · " + bAdd.Message);
    Check("Thêm dầm chia ô sàn: mô hình phân tích được", FrameAnalysis.Run(wp).Get("TT").Disp.Values.All(v => v.All(double.IsFinite)) ? 1 : 0, 1, 0);
}

Console.WriteLine("16. Móng cọc, móng băng, móng đơn (thiết kế trực tiếp)");
FoundationTests.Run(Check);

Console.WriteLine("18. Địa danh → vùng gió, W0 (QCVN 02:2022), nhập bảng tra");
LocationTests.Run(Check, Path.Combine(Path.GetTempPath(), "kcvxt_loc"));

Console.WriteLine("17. Dàn trang bản vẽ (sheet), xuất DXF theo tờ");
SheetTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("19. Địa chất: hố khoan, nền mặc định, chỉ tiêu gợi ý, lún, sức chịu tải cọc theo SPT, gợi ý phương án móng");
GeoTests.Run(Check);

Console.WriteLine("20. Phụ lục tính toán (công thức, hệ số, giải thích) + xuất Word");
AppendixTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("21. Dầm cong: hình học cung, nội lực (so lời giải giải tích), xoắn, xuất e2k/DXF");
CurvedBeamTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("22. Dàn trang thủ công: đối tượng tự do, bắt dính, căn chỉnh, khổ giấy tuỳ chỉnh, hoàn tác");
ManualSheetTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("23. Chuẩn layer CAD: nhận diện layer kiến trúc, layer bản vẽ kết cấu, kiểu chữ");
CadStandardTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("24. Liên kết AutoCAD: dữ liệu trao đổi → mặt bằng kiến trúc → kết cấu");
AcadLinkTests.Run(Check, Path.Combine(Path.GetTempPath(), "kcvxt_acad"));

Console.WriteLine("25. Lựa chọn bố trí thép, shop drawing (nối chồng, mã hình dạng, cắt thép tối ưu)");
RebarShopTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("26. Bảng khối lượng vật tư, thuyết minh tính toán tự động");
QuantityTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("27. Đọc file DWG (ACadSharp): thực thể, khối, layer, đơn vị, nhập mặt bằng kiến trúc");
DwgTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("28. Thép sàn: mũ cạnh chung tính một lần, phương án lưới 2 lớp, so sánh vượt/tiết kiệm thép");
SlabSchemeTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("29. Nhập kiến trúc: tường xiên, tường cong, ELLIPSE/SPLINE, cột tròn/xoay, DXF nhị phân");
ArchGeometryTests.Run(Check, Path.Combine(Path.GetTempPath(), "kcvxt_archgeo"));

Console.WriteLine("30. Nguyên tắc nối thép: không nối trong vùng nguy hiểm, so le, ống nối ren");
SpliceRuleTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("31. Thiết lập bản vẽ: tỷ lệ, cỡ chữ, kiểu kích thước theo tỷ lệ, ghi chú chung");
DrawingStyleTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("32. Cầu thang bộ: hình học, tải, thiết kế, bản vẽ, khối lượng (bậc xây, ốp granite/gỗ)");
StairTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("33. Mẫu định dạng (template), khung tên người dùng từ DWG/DXF/ảnh");
TemplateTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("34. Móng: bố trí cọc trong đài, kiểm tra an toàn, chọc thủng, sự làm việc móng đơn/móng băng");
FoundationCheckTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("35. Nhập mô hình/kết quả từ ETABS, SAP2000, SAFE và phần mềm khác");
ResultImportTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("36. Bảng thống kê (SL=n), xuất gộp/tách file, hiệu chỉnh bản vẽ sau khi xuất, khoá bản vẽ sửa trong CAD");
DrawingExportTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("37. Móng lệch tâm: ranh giới đất, dầm móng cân bằng, móng phối hợp, đài cọc/móng băng lệch tâm");
EccentricFootingTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("38. Lùi cọc khỏi ranh giới để ép cọc; gia cố nền bằng cừ tràm (2 phương pháp, lún, mực nước ngầm)");
PileSetbackCuTramTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("39. Văn bản pháp lý & tiêu chuẩn hiện hành (Luật 135/2025, NĐ 217/2026, TT 34/38/2026, TCVN 9386-1:2025), nhập định mức Excel");
RegulationTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("40. So sánh với ETABS/SAP/SAFE (không thay nội lực), báo cáo chênh lệch");
CrossCheckTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("41. Phân tích dao động riêng, phổ phản ứng (CQC/SRSS), khối lượng tham gia");
ModalTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("42. Bộ kiểm chứng phần mềm – Báo cáo kiểm chứng (Word/Excel)");
VerificationTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("43. Khai báo tiết diện, vẽ dầm kiểu ETABS (toạ độ, ô lưới, dầm phụ theo khoảng cách, công xôn, sao chép, phạm vi tầng)");
BeamToolsTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("44. Mặt cắt dầm có cánh sàn (chữ T / L), thép sàn trong cánh, đường cắt lửng");
BeamFlangeDrawingTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("45. Hướng dẫn sử dụng tích hợp (Word/Markdown, trợ giúp theo ngữ cảnh F1, menu)");
UserGuideTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("46. Quy trình mô hình kiểu ETABS: kiểm tra mô hình, gán tải, giải phóng, hệ số độ cứng, tổ hợp, bảng kết quả");
ModelWorkflowTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("47. Liên kết bản vẽ AutoCAD (DWG/DXF): nền mặt bằng, bắt điểm, cập nhật khi lưu, so khác biệt");
CadLinkTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("48. Sàn hạ cốt: khai báo, mặc định theo công năng, kiểm tra chiều cao dầm, cạnh sàn không liên tục, bản vẽ");
SlabDropTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

Console.WriteLine("49. Hatch vật liệu (R12 mô phỏng, DXF 2018/DWG có HATCH thật)");
HatchTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("50. Triển khai hồ sơ: dầm / cột / sàn / móng đơn / móng băng / đài cọc, xuất DXF – Excel – Word");
DetailingTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("51. Móng bè (phương pháp móng cứng, dải, chọc thủng)");
RaftTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("52. Bê tông dự ứng lực (tổn hao, ứng suất, Mu, độ vồng)");
PrestressTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("53. Bể nước BTCT (áp lực nước / đất, nứt, đẩy nổi)");
WaterTankTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("54. Lấy kích thước từ AutoCAD (lệnh VXT_KT → Triển khai hồ sơ)");
AcadPickTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("55. Mặt bằng sàn: hạ cốt, nhấn thép, khoét lỗ, thép tự động / thủ công");
SlabPlanTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("56. Lanh tô cửa, ô văng cửa");
LintelTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("57. Sửa dầm: kéo, trim, extend, nối dầm hở; pick đối tượng CAD");
BeamEditTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("58. Lưới trục và tầng: lấy kích thước từ mặt bằng tải lên (CAD / ảnh)");
GridFromPlanTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("59. Mặt bằng kết cấu triển khai hồ sơ (móng, cột, dầm, sàn, lanh tô, ô văng, cầu thang)");
StructuralPlanTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("60. Hoàn thiện bản vẽ: chân dim, mặt cắt dầm, chi tiết thang bộ, bảng thống kê vật liệu");
DrawingFinishTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("61. Tuỳ chọn thép dầm (bẻ mỏ, tăng cường, mũ gối), nối thép cột, nhấn cổ chai, khoảng hở nhãn thép");
BeamColumnOptionsTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("62. Đài cọc lớp trên / lồng thép, râu cọc, móng băng vát, thép sàn trên mặt bằng, mặt cắt sàn, cây theo mặt bằng");
FoundationSlabPlanTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("63. Thang bộ: bậc gạch / bậc BTCT, zíc zắc, xương cá, thang thép hình, mặt bằng thép bản thang; hố pít – vách thang máy");
StairLiftTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("64. Nhân bản tầng, vẽ / nhập mặt bằng KC, lanh tô theo cửa kiến trúc, tường 110");
StoreyPlanLintelTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("65. Dàn trang in, xuất PDF toàn bộ, in trực tiếp (Triển khai hồ sơ)");
SheetLayoutTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("66. Khung tên dàn trang in: tự tạo, tải lên DWG/DXF, tên – số hiệu theo cây thư mục, đổi tên");
TitleBlockTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("67. Khổ giấy – tỷ lệ tự động, vẽ lại đúng cỡ chữ, nhãn mặt bằng không đè");
PaperScaleTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("68. Trích chi tiết, mặt cắt mọi nhịp dầm, shop drawing, điểm cắt thép, ghi chú thép sàn");
ShopCutTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("69. Chia dầm nhiều đoạn, xuất tất cả theo bộ dàn trang");
BeamSplitTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("70. Giao diện gọn: tìm lệnh không dấu, biểu tượng nút, lưu bố cục ui.json");
CompactUiTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("71. Mở mặt bằng CAD: nguồn trục (layer / cột / người dùng), trục qua tim / mép cột, đọc và áp dụng dữ liệu mặt bằng");
CadPlanAxisTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("72. Chuẩn hoá layer CAD → VXT-… (tên, nội dung, block cửa, bộ ánh xạ, xuất DXF chuẩn hoá)");
LayerConvertTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("73. Thanh lệnh cấu kiện, chọn nhiều, gán tải, tường CAD → dầm, dòng lệnh kiểu AutoCAD");
PlanLoadCadTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("74. Giao diện / lệnh vẽ kiểu Revit: kiểu tường nhiều lớp, cửa tự cắt tường, kích thước tạm, nối góc, đường định vị");
RevitDrawTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("75. Hoàn thiện kiểu Revit: 3D tường có ô cửa, tay nắm, làm lại, CO/M/MI/AR/O bằng chuột, khoá link CAD, nhãn, Tab");
RevitFinishTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("76. Hoàn thiện bản vẽ / xuất: cửa trên mặt bằng, thống kê cửa, lanh tô từ mô hình, cột lệch tâm (E2K/S2K), tham chiếu tờ, danh mục mặt cắt");
DrawExportFinishTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("77. Vừa màn hình mặt bằng theo vùng vẽ thật, đặt nhãn theo ưu tiên không chồng");
PlanFitLabelTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("78. Bản vẽ kiểu Revit: cỡ chữ in, trình duyệt khung nhìn, tạo / đặt tên / đánh số bản vẽ, đặt khung nhìn, danh mục, kích đúp → cấu kiện");
RevitSheetsTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("79. Chọn / bỏ chọn tất cả layer: ô Tất cả ba trạng thái, chọn hết / bỏ hết / đảo, chỉ dòng đang hiện");
CheckAllTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("80. Cao độ móng → mái, bộ môn, va chạm, IFC / OBJ / DAE, thư viện 3D, Revit kiến trúc + điện nước, hồ sơ theo bộ môn");
LevelsDisciplineIfcTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("81. Bộ môn điện nước: thư viện, thiết bị chôn tường, tuyến + phụ kiện, chọn cỡ, đối chiếu kiến trúc, thống kê, bản vẽ DN");
MepTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("82. Đặt đồ đạc bắt điểm, kích thước tạm, chọn / kéo 3D, mặt cắt ngang tầng, mặt cắt 1-1 / 2-2 tự sinh");
PlaceSnap3DSectionTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("83. Thoát nước đi dưới sàn có độ dốc, cảnh báo nguy hiểm kết cấu, chi tiết gia cường");
DrainSlopeImpactTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("84. Lỗ xuyên điện nước → hồ sơ kết cấu (KC-10, mặt đứng dầm, thống kê, kiểm tra cắt), tường trừ rãnh điện nước, mặt cắt nhìn thấy, kích thước tạm tường / cột");
MepStructureFinishTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("85. Mặt cắt: kiến trúc không móng / kết cấu có móng, vật liệu đúng loại + chú thích, rãnh điện nước mặt bằng / 3D, lỗ sàn × cột, cắt tại lỗ nội suy");
SectionMaterialTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("86. Chọc thủng đài cọc / móng có kể mô men, cắt mép móng, cọc chịu nhổ, phản lực dầm móng");
PunchingUpliftTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("87. Cọc khoan nhồi mini (TCVN 10304), chọc thủng bậc móng / móng băng, nhổ khối nhóm cọc, tự chuyển phản lực giằng");
MiniBoredPileTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("88. Cọc khoan nhồi nâng cao: chế độ thiết kế theo TCVN 10304, B22,5, ma sát âm, tự chọn chiều dài, hệ số nhóm, đài thanh chống – giằng, ống siêu âm, nối lồng thép, mặt bằng định vị cọc");
BoredPileAdvancedTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("89. Quét chọn mọi đối tượng, sửa lưới trục, thêm / kéo tầng, mặt đứng có trục");
SelectGridLevelTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("90. Thao tác còn lại: quét chọn 3D, kéo trục, kéo cao độ, bong bóng trục, hoàn tác bộ bản vẽ");
InteractionLeftoverTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("91. Bản vẽ / tính toán còn lại: bậc móng trên mặt bằng, cọc chế độ thiết kế, móng bè / móng băng dọc mặt cắt, hoàn thiện theo phòng, mặt cắt dọc thoát nước, độ dốc theo hệ, Excel điện nước, Revit MEP");
DrawingCalcLeftoverTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));
Console.WriteLine("92. Còn lại đợt 2: kéo cao độ mặt cắt, nắm trục giữa, bong bóng / ẩn trục hồ sơ, cài đặt điện nước, điều hoà – thông gió, kéo thiết bị 3D");
RemainingRound2Tests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "KetQuaMau"));

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

foreach (var (proj, res, tag) in new[] { (house, hr, "NhaPho"), (tall, tr, "Nha7Tang"), (archProj, ar, "TuBanVeKienTruc"), (shed, sr, "NhaXuong") })
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
    if (tag == "TuBanVeKienTruc") Step("Chép bản vẽ kiến trúc đầu vào", () => File.Copy(archDxf, Path.Combine(dir, "KienTrucMau_DauVao.dxf"), true));
}
return fail == 0 ? 0 : 1;

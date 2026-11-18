using System.Globalization;
using System.Text;
using System.Text.Json;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Import;
using KetCauVXT.Import.Arch;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Pipeline;

// =====================================================================================================
// Quy trình "Revit kiến trúc → thiết kế kết cấu → hồ sơ" chạy không cần giao diện (cho agent / dòng lệnh):
//   KetCauVXT.exe --revit-thiet-ke <file.vxtrevit.json> <thư mục ra> [tuỳ chọn]
// Cùng các bước với trình hướng dẫn "Revit → Thiết kế" (MainWindow.RevitWizard.cs), nhưng mỗi điểm cần người duyệt
// được GHI vào BaoCao.md / BaoCao.json (mục "Cần người duyệt") thay vì hỏi bằng hộp thoại. Không gửi dữ liệu đi đâu.
// Có thể dừng sớm (--den-buoc) để người dùng duyệt bảng tải / mặt bằng trước khi chạy tiếp.
// =====================================================================================================

public sealed class PipelineOptions
{
    public string InputFile { get; set; } = "";
    public string OutDir { get; set; } = "";
    /// <summary>Tên dự án (thư mục con trong OutDir); rỗng → tên file Revit.</summary>
    public string Name { get; set; } = "";
    /// <summary>Vùng gió QCVN 02:2022 (I..V); rỗng → giữ W0.</summary>
    public string WindZone { get; set; } = "";
    public double? W0 { get; set; }
    public string Terrain { get; set; } = "B";
    public bool Wind { get; set; } = true;
    public bool Optimize { get; set; } = true;
    public bool KeepWidth { get; set; } = true;
    public CostRates Rates { get; set; }
    /// <summary>Đổi công năng theo tên phòng: "Sảnh + cầu thang" → "Sảnh, hành lang" (tên công năng hoặc từ khoá).</summary>
    public Dictionary<string, string> RoomKinds { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Dừng sau bước: doc, tai, bo-tri, kiem-tra, phan-tich, toi-uu, ho-so (mặc định: ho-so = chạy hết).</summary>
    public string StopAfter { get; set; } = "ho-so";
    public bool ExportDocs { get; set; } = true;
}

public sealed class PipelineStep
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public bool Ok { get; set; } = true;
    public List<string> Lines { get; } = new();
}

public sealed class PipelineReport
{
    public string Input { get; set; } = "";
    public string Folder { get; set; } = "";
    public DateTime Time { get; set; } = DateTime.Now;
    public List<PipelineStep> Steps { get; } = new();
    /// <summary>Điểm người có chuyên môn phải xem và quyết định (không tự bỏ qua).</summary>
    public List<string> NeedReview { get; } = new();
    public List<string> Files { get; } = new();
    public string StoppedAt { get; set; } = "";
    public bool Ok => Steps.All(s => s.Ok);
    public int Columns, Beams, Slabs, Footings, Failed;
    public double ConcreteM3, RebarKg, FormworkM2, CostVnd;
    public double? PeriodT1;

    /// <summary>Mã thoát: 0 = chạy hết, mọi cấu kiện đạt; 1 = dừng có chủ ý / còn việc phải duyệt-sửa; 2 = lỗi.</summary>
    public int ExitCode => !Ok ? 2 : (Failed > 0 || StoppedAt.Length > 0) ? 1 : 0;

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Báo cáo Việt Const – Revit → thiết kế kết cấu");
        sb.AppendLine();
        sb.AppendLine($"- File Revit: `{Input}`");
        sb.AppendLine($"- Thư mục hồ sơ: `{Folder}`");
        sb.AppendLine($"- Thời điểm: {Time:dd/MM/yyyy HH:mm}");
        sb.AppendLine($"- Kết quả: {(ExitCode == 0 ? "chạy hết, mọi cấu kiện đạt" : ExitCode == 1 ? (StoppedAt.Length > 0 ? "dừng sau bước " + StoppedAt : "còn cấu kiện không đạt") : "lỗi – xem các bước")}");
        if (Columns + Beams > 0)
            sb.AppendLine($"- Mô hình: {Columns} cột, {Beams} dầm, {Slabs} ô sàn, {Footings} móng; không đạt: {Failed}");
        if (CostVnd > 0)
            sb.AppendLine($"- Vật liệu: bê tông {ConcreteM3:0.00} m³, thép {RebarKg:0} kg, ván khuôn {FormworkM2:0.0} m² ≈ {CostVnd / 1e6:0.00} triệu đồng");
        if (PeriodT1.HasValue) sb.AppendLine($"- Chu kỳ dao động T1 = {PeriodT1:0.000} s");
        sb.AppendLine();
        sb.AppendLine("## Cần người duyệt");
        if (NeedReview.Count == 0) sb.AppendLine("- (không có)");
        foreach (var r in NeedReview) sb.AppendLine("- [ ] " + r);
        sb.AppendLine();
        foreach (var s in Steps)
        {
            sb.AppendLine($"## {(s.Ok ? "✓" : "✗")} {s.Title}");
            foreach (var l in s.Lines) sb.AppendLine("- " + l);
            sb.AppendLine();
        }
        if (Files.Count > 0)
        {
            sb.AppendLine("## File đã xuất");
            foreach (var f in Files) sb.AppendLine("- " + f);
            sb.AppendLine();
        }
        sb.AppendLine("Hồ sơ do phần mềm lập tự động – người có chứng chỉ hành nghề phải kiểm tra thuyết minh, bản vẽ và ký trước khi phát hành.");
        return sb.ToString();
    }

    public string ToJson() => JsonSerializer.Serialize(new
    {
        input = Input, folder = Folder, time = Time, ok = Ok, exitCode = ExitCode, stoppedAt = StoppedAt,
        columns = Columns, beams = Beams, slabs = Slabs, footings = Footings, failed = Failed,
        concreteM3 = Math.Round(ConcreteM3, 2), rebarKg = Math.Round(RebarKg), formworkM2 = Math.Round(FormworkM2, 1), costVnd = Math.Round(CostVnd),
        periodT1 = PeriodT1, needReview = NeedReview,
        steps = Steps.Select(s => new { id = s.Id, title = s.Title, ok = s.Ok, lines = s.Lines }),
        files = Files
    }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}

public static class RevitDesignPipeline
{
    public static readonly string[] StepIds = { "doc", "tai", "bo-tri", "kiem-tra", "phan-tich", "toi-uu", "ho-so" };

    /// <summary>Chạy quy trình. <paramref name="exportDocs"/>: hàm xuất hồ sơ (bản vẽ, thuyết minh…) – null → bản xuất trong Core.</summary>
    public static PipelineReport Run(PipelineOptions o, IProgress<string> progress = null)
    {
        var rep = new PipelineReport { Input = o.InputFile };
        int stopIdx = Math.Max(0, Array.IndexOf(StepIds, (o.StopAfter ?? "ho-so").Trim().ToLowerInvariant()));
        if (Array.IndexOf(StepIds, (o.StopAfter ?? "ho-so").Trim().ToLowerInvariant()) < 0) stopIdx = StepIds.Length - 1;
        bool Stop(int i) { if (i < stopIdx) return false; if (i < StepIds.Length - 1) rep.StoppedAt = StepIds[i]; return true; }
        PipelineStep Begin(string id, string title) { progress?.Report(title + "…"); var s = new PipelineStep { Id = id, Title = title }; rep.Steps.Add(s); return s; }

        Project p = null; RevitExchange ex = null; ArchDrawing d = null; ArchImportOptions ao = null; ArchLoadTakeoff t = null;
        AnalysisResults res = null; DesignSummary ds = null; bool direct = false;
        string folder = "";
        try
        {
            // ---------------------------------------------------------------- 1 đọc
            var s1 = Begin("doc", "1. Đọc mô hình Revit");
            if (!File.Exists(o.InputFile)) { s1.Ok = false; s1.Lines.Add("Không thấy file " + o.InputFile); return rep; }
            ex = RevitJsonImporter.ReadFile(o.InputFile);
            p = ModelBuilder.NewProject(string.IsNullOrWhiteSpace(o.Name) ? ProjectNameOf(o.InputFile) : o.Name.Trim());
            direct = RevitArchReader.HasStructure(ex);
            s1.Lines.Add($"Revit: {ex.Levels.Count} cao độ, {ex.Grids.Count} trục, {ex.Walls.Count} tường, {ex.Rooms.Count} phòng, {ex.Columns.Count} cột KC, {ex.Beams.Count} dầm KC.");
            var skip = RevitArchReader.StructureSkipReason(ex);
            if (skip != null) { s1.Lines.Add("⚠ " + skip); rep.NeedReview.Add(skip); }
            if (direct) s1.Lines.Add("Có khung kết cấu (cột + dầm/sàn) → nhập trực tiếp.");
            else
            {
                ao = new ArchImportOptions { Concrete = p.Concrete?.ConcreteBeam ?? "B25" };
                d = RevitArchReader.ToDrawing(ex, ao);
                foreach (var i in d.Info) s1.Lines.Add(i);
                foreach (var w in d.Warnings) s1.Lines.Add("⚠ " + w);
                if (!d.Floors.Any(f => !f.IsRoofPlan && f.Walls.Count > 0)) { s1.Ok = false; s1.Lines.Add("Không nhận diện được mặt bằng tầng nào có tường."); return rep; }
            }
            if (Stop(0)) return rep;

            // ---------------------------------------------------------------- 2 tải
            var s2 = Begin("tai", "2. Nhận diện tải trọng");
            if (!direct)
            {
                t = ArchLoadTakeoff.From(d, ao, ex);
                foreach (var kv in o.RoomKinds)
                {
                    var row = t.Rooms.FirstOrDefault(r => string.Equals(r.Name.Trim(), kv.Key.Trim(), StringComparison.OrdinalIgnoreCase));
                    var rt = FindKind(t.Catalog, kv.Value);
                    if (row == null) { s2.Lines.Add($"⚠ Không có phòng tên \"{kv.Key}\" để đổi công năng."); continue; }
                    if (rt == null) { s2.Lines.Add($"⚠ Không hiểu công năng \"{kv.Value}\" (phòng \"{kv.Key}\")."); continue; }
                    row.Kind = rt.Kind; row.KindName = rt.Name; row.Finish = rt.Finish; row.Live = rt.Live; row.Sure = true;
                    s2.Lines.Add($"Người dùng chọn: \"{row.Name}\" → {rt.Name}.");
                }
                foreach (var r in t.Rooms)
                    s2.Lines.Add($"Phòng \"{r.Name}\" ×{r.Count} ({r.Plans}) → {r.KindName}: hoàn thiện {r.Finish:0.##} + hoạt tải {r.Live:0.##} kN/m²{(r.Sure ? "" : " – CHƯA CHẮC")}");
                foreach (var w in t.Walls)
                    s2.Lines.Add($"Tường \"{w.TypeName}\": {w.PerArea:0.##} kN/m²{(w.Sure ? "" : " – CHƯA CHẮC")}");
                foreach (var e in t.Equipment)
                    s2.Lines.Add($"Thiết bị \"{e.Name}\": {e.Load:0.##} kN{(e.Sure ? "" : " – CHƯA CHẮC")}");
                foreach (var n in t.Notes) { s2.Lines.Add("⚠ " + n); rep.NeedReview.Add(n); }
                foreach (var r in t.Rooms.Where(r => !r.Sure)) rep.NeedReview.Add($"Công năng phòng \"{r.Name}\" đang đoán là {r.KindName} – xác nhận hoặc đổi (--cong-nang \"{r.Name}=…\").");
                foreach (var w in t.Walls.Where(w => !w.Sure)) rep.NeedReview.Add($"Trọng lượng tường \"{w.TypeName}\" đang đoán {w.PerArea:0.##} kN/m².");
                t.Apply(d, ao);
            }
            else s2.Lines.Add("Nhập trực tiếp: tải lấy theo mô hình Revit (kiểm tra lại tải sàn, tường).");
            if (Stop(1)) return rep;

            // ---------------------------------------------------------------- 3 bố trí + gió
            var s3 = Begin("bo-tri", "3. Bố trí kết cấu");
            if (direct)
            {
                var ir = RevitJsonImporter.Build(p, ex, new RevitImportOptions { Concrete = p.Concrete.ConcreteBeam });
                s3.Lines.Add(ir.ToString().Replace("\r", "").Replace("\n", " · "));
            }
            else
            {
                var prop = ArchToStructure.Propose(d, ao);
                s3.Lines.Add($"Lưới trục: {prop.GridX.Count} trục X, {prop.GridY.Count} trục Y; {prop.Columns.Count(c => c.Include)} vị trí cột; {prop.Levels.Count} cao trình.");
                foreach (var lv in prop.Levels)
                    s3.Lines.Add($"{lv.Name} (+{lv.Elevation.ToString("0.000", CultureInfo.InvariantCulture)}): {lv.Beams.Count(b => b.Include)} dầm, {lv.Slabs.Count(x => x.Include)} ô sàn, {lv.Holes.Count} lỗ sàn");
                foreach (var w in prop.Warnings) { s3.Lines.Add("⚠ " + w); rep.NeedReview.Add(w); }
                var ar = ArchToStructure.Apply(p, d, prop, ao);
                p.RevitLink = new RevitLink { OriginX = d.OriginX, OriginY = d.OriginY, GroundZ = d.GroundZ, SourceFile = o.InputFile };
                int ne = t?.ApplyEquipment(p) ?? 0;
                s3.Lines.Add($"Đã lập: {ar.Columns} cột, {ar.Beams} dầm, {ar.Slabs} ô sàn{(ne > 0 ? $", {ne} tải thiết bị" : "")}.");
                rep.NeedReview.Add("Mặt bằng kết cấu (vị trí cột, dầm, ô sàn, lỗ thang) – mở MoHinh.kcvxt trong Việt Const để xem trước khi dùng hồ sơ.");
            }
            p.Wind.Enabled = o.Wind;
            var z = WindZones.Find(WindZones.Normalize(o.WindZone));
            if (z != null) { p.Wind.Zone = z.Zone; p.Wind.W0 = z.W0; }
            if (o.W0.HasValue && o.W0 > 0) p.Wind.W0 = o.W0.Value;
            if (!string.IsNullOrWhiteSpace(o.Terrain)) p.Wind.Terrain = o.Terrain.Trim().ToUpperInvariant() is "A" or "C" ? o.Terrain.Trim().ToUpperInvariant() : "B";
            s3.Lines.Add(p.Wind.Enabled ? $"Gió: W0 = {p.Wind.W0:0.###} kN/m² (vùng {(p.Wind.Zone.Length > 0 ? p.Wind.Zone : "–")}), địa hình {p.Wind.Terrain}." : "Không tính gió.");
            if (p.Wind.Enabled && z == null && !o.W0.HasValue)
                rep.NeedReview.Add($"Áp lực gió đang để mặc định W0 = {p.Wind.W0:0.##} kN/m² – khai báo vùng gió theo địa điểm (--vung-gio).");
            if (o.Rates != null) p.CostRates = o.Rates;
            if (Stop(2)) return Save(rep, o, p, null, null, ref folder);

            // ---------------------------------------------------------------- 4 tổ hợp + kiểm tra
            var s4 = Begin("kiem-tra", "4. Tổ hợp tải và kiểm tra mô hình");
            p.Combinations.Clear();
            ComboGenerator.Generate(p);
            s4.Lines.Add($"Tổ hợp TCVN 2737:2023: {p.Combinations.Count} tổ hợp.");
            var mc = ModelCheck.Run(p);
            s4.Lines.Add($"{mc.Errors} lỗi, {mc.Warnings} cảnh báo.");
            foreach (var i in mc.Issues.Where(i => i.Severity != IssueSeverity.Info))
            {
                s4.Lines.Add($"[{(i.Severity == IssueSeverity.Error ? "Lỗi" : "Cảnh báo")}] {i.Message}");
                if (i.Severity == IssueSeverity.Warning) rep.NeedReview.Add(i.Message);
            }
            if (mc.Errors > 0) { s4.Ok = false; return Save(rep, o, p, null, null, ref folder); }
            if (Stop(3)) return Save(rep, o, p, null, null, ref folder);

            // ---------------------------------------------------------------- 5 phân tích
            var s5 = Begin("phan-tich", "5. Phân tích nội lực");
            res = FrameAnalysis.Run(p, progress);
            s5.Lines.Add($"{res.Equations} phương trình, {res.Cases.Count} trường hợp tải.");
            foreach (var w in res.Warnings) s5.Lines.Add("· " + w);
            rep.PeriodT1 = PeriodOf(res);
            double ct = 0.075 * Math.Pow(Math.Max(1, p.Stories.Count == 0 ? 3 : p.Stories.Max(s => s.Elevation)), 0.75);
            if (rep.PeriodT1.HasValue && rep.PeriodT1 > 2 * ct)
                rep.NeedReview.Add($"Chu kỳ T1 = {rep.PeriodT1:0.00} s lớn hơn 2 lần công thức kinh nghiệm Ct·H^0,75 = {ct:0.00} s – khung có thể quá mềm theo một phương, xem lại cột / dầm.");
            if (Stop(4)) return Save(rep, o, p, res, null, ref folder);

            // ---------------------------------------------------------------- 6 tối ưu / thiết kế
            var s6 = Begin("toi-uu", o.Optimize ? "6. Tối ưu tiết diện và thiết kế" : "6. Thiết kế");
            if (o.Optimize)
            {
                var r = SectionOptimizer.Run(p, p.CostRates, new OptimizeOptions { KeepWidth = o.KeepWidth }, progress);
                foreach (var l in r.Log) s6.Lines.Add(l);
                res = r.Final.Analysis; ds = r.Final.Design;
                rep.ConcreteM3 = r.Final.ConcreteM3; rep.RebarKg = r.Final.RebarKg; rep.FormworkM2 = r.Final.FormworkM2; rep.CostVnd = r.Final.Cost;
                rep.PeriodT1 = PeriodOf(res) ?? rep.PeriodT1;
            }
            else
            {
                ds = new DesignSummary();
                if (p.Frames.Any(f => f.IsConcrete)) ds = KetCauVXT.Design.Concrete.ConcreteDesigner.Design(p, res, ds);
                if (p.Frames.Any(f => !f.IsConcrete)) KetCauVXT.Design.Steel.SteelDesigner.Check(p, res, ds);
            }
            rep.Columns = p.Frames.Count(f => f.Kind == FrameKind.Column);
            rep.Beams = p.Frames.Count(f => f.Kind == FrameKind.Beam);
            rep.Slabs = p.Slabs.Count;
            rep.Footings = ds.Footings.Count + ds.PileCaps.Count + ds.StripFootings.Count;
            var bad = ds.All.Where(m => !m.Ok).ToList();
            rep.Failed = bad.Count;
            s6.Lines.Add($"Thiết kế: {ds.Beams.Count} dầm, {ds.Columns.Count} cột, {ds.Slabs.Count} ô sàn, {rep.Footings} móng – {bad.Count} cấu kiện không đạt.");
            foreach (var m in bad.Take(30)) s6.Lines.Add($"✗ {m.Name} ({m.Story}): {string.Join("; ", m.Messages.Take(2))}");
            if (bad.Count > 0) rep.NeedReview.Add($"{bad.Count} cấu kiện không đạt – sửa mô hình (tăng tiết diện, thêm cột/dầm) rồi chạy lại.");
            if (Stop(5)) return Save(rep, o, p, res, ds, ref folder);

            // ---------------------------------------------------------------- 7 hồ sơ
            var s7 = Begin("ho-so", "7. Xuất hồ sơ");
            Save(rep, o, p, res, ds, ref folder);
            if (o.ExportDocs) ExportDocs(rep, s7, p, res, ds, folder);
        }
        catch (Exception e)
        {
            var last = rep.Steps.LastOrDefault() ?? Begin("loi", "Lỗi");
            last.Ok = false;
            last.Lines.Add("Lỗi chương trình: " + e.Message);
        }
        return rep;
    }

    private static string ProjectNameOf(string file)
    {
        var n = Path.GetFileName(file);
        foreach (var ext in new[] { ".vxtrevit.json", ".json" })
            if (n.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) { n = n[..^ext.Length]; break; }
        return string.IsNullOrWhiteSpace(n) ? "Cong trinh" : n;
    }

    private static RoomType FindKind(List<RoomType> catalog, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var exact = catalog.FirstOrDefault(c => string.Equals(c.Name, text.Trim(), StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;
        var u = LocationDb.Norm(text);
        return catalog.FirstOrDefault(c => LocationDb.Norm(c.Name).Contains(u) || (c.Keys ?? Array.Empty<string>()).Any(k => u.Contains(k)));
    }

    private static double? PeriodOf(AnalysisResults r)
    {
        try { var m = r?.Modal; if (m != null && m.Modes.Count > 0) return m.Modes.Max(x => x.Period); } catch { }
        return null;
    }

    /// <summary>Lưu mô hình + file cho add-in Revit vào thư mục dự án (tạo nếu chưa có).</summary>
    private static PipelineReport Save(PipelineReport rep, PipelineOptions o, Project p, AnalysisResults res, DesignSummary ds, ref string folder)
    {
        if (string.IsNullOrEmpty(folder))
        {
            folder = Path.Combine(o.OutDir, SafeName(p.Name) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            rep.Folder = folder;
        }
        p.Save(Path.Combine(folder, "MoHinh.kcvxt")); Add(rep, folder, "MoHinh.kcvxt");
        File.WriteAllText(Path.Combine(folder, SafeName(p.Name) + ".vxt2revit.json"), RevitSyncExport.ToJson(p), new UTF8Encoding(false));
        Add(rep, folder, SafeName(p.Name) + ".vxt2revit.json");
        return rep;
    }

    private static void ExportDocs(PipelineReport rep, PipelineStep s, Project p, AnalysisResults res, DesignSummary ds, string folder)
    {
        void Do(string name, Action a)
        {
            try { a(); s.Lines.Add("✓ " + name); }
            catch (Exception e) { s.Ok = false; s.Lines.Add($"✗ {name}: {e.Message}"); }
        }
        var opt = new KetCauVXT.Drawing.DrawingExportOptions();
        Do("bản vẽ DXF", () =>
        {
            string dd = Path.Combine(folder, "BanVe"); Directory.CreateDirectory(dd);
            var r = KetCauVXT.Drawing.DrawingSet.Export(p, ds, dd, opt);
            foreach (var f in r.Files) Add(rep, folder, f);
            foreach (var m in r.Messages.Distinct().Take(10)) s.Lines.Add("· " + m);
        });
        Do("gia công thép", () =>
        {
            string dd = Path.Combine(folder, "GiaCongThep"); Directory.CreateDirectory(dd);
            foreach (var f in KetCauVXT.Drawing.RebarShopDrawing.Export(p, ds, dd, opt).Files) Add(rep, folder, f);
            KetCauVXT.Report.RebarShopReport.Export(p, ds, Path.Combine(dd, "GiaCongThep.xlsx")); Add(rep, folder, Path.Combine(dd, "GiaCongThep.xlsx"));
        });
        Do("thuyết minh Word", () => { KetCauVXT.Report.CalculationReport.Export(p, res, ds, Path.Combine(folder, "ThuyetMinh.docx")); Add(rep, folder, "ThuyetMinh.docx"); });
        Do("bảng tính Excel", () => { KetCauVXT.Report.ExcelReport.Export(p, res, ds, Path.Combine(folder, "BangTinh.xlsx")); Add(rep, folder, "BangTinh.xlsx"); });
        Do("bảng khối lượng", () =>
        {
            var path = Path.Combine(folder, KetCauVXT.Report.QuantityReport.DefaultFileName);
            KetCauVXT.Report.QuantityReport.ExportExcel(p, KetCauVXT.Report.QuantityCalculator.Compute(p, ds), path); Add(rep, folder, path);
        });
        Do("nội lực CSV", () => { string dd = Path.Combine(folder, "NoiLuc"); Directory.CreateDirectory(dd); KetCauVXT.Report.ForceTables.WriteCsv(res, dd); Add(rep, folder, "NoiLuc/"); });
        Do("mô hình ETABS / SAP2000", () =>
        {
            KetCauVXT.Export.E2kExporter.Export(p, Path.Combine(folder, "MoHinh.e2k")); Add(rep, folder, "MoHinh.e2k");
            KetCauVXT.Export.S2kExporter.Export(p, Path.Combine(folder, "MoHinh.s2k")); Add(rep, folder, "MoHinh.s2k");
        });
        rep.NeedReview.Add("Người có chứng chỉ hành nghề kiểm tra thuyết minh, bản vẽ và ký trước khi phát hành.");
    }

    private static void Add(PipelineReport rep, string folder, string f)
    {
        var rel = Path.IsPathRooted(f) ? Path.GetRelativePath(folder, f) : f;
        rel = rel.Replace('\\', '/');
        if (!rep.Files.Contains(rel)) rep.Files.Add(rel);
    }

    public static string SafeName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "Cong trinh";
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Trim();
    }

    /// <summary>Ghi BaoCao.md + BaoCao.json vào thư mục hồ sơ (hoặc OutDir nếu chưa tạo thư mục).</summary>
    public static (string Md, string Json) WriteReport(PipelineReport rep, PipelineOptions o)
    {
        string dir = string.IsNullOrEmpty(rep.Folder) ? o.OutDir : rep.Folder;
        Directory.CreateDirectory(dir);
        string md = Path.Combine(dir, "BaoCao.md"), js = Path.Combine(dir, "BaoCao.json");
        File.WriteAllText(md, rep.ToMarkdown(), new UTF8Encoding(false));
        File.WriteAllText(js, rep.ToJson(), new UTF8Encoding(false));
        return (md, js);
    }

    /// <summary>Đọc tham số dòng lệnh (sau "--revit-thiet-ke"). Trả về null + lỗi nếu sai.</summary>
    public static PipelineOptions ParseArgs(IReadOnlyList<string> a, out string error)
    {
        error = null;
        var o = new PipelineOptions();
        var pos = new List<string>();
        for (int i = 0; i < a.Count; i++)
        {
            string k = a[i];
            string Val() { if (i + 1 >= a.Count) throw new ArgumentException("Thiếu giá trị sau " + k); return a[++i]; }
            double Num() => double.Parse(Val().Replace(',', '.'), CultureInfo.InvariantCulture);
            try
            {
                switch (k.ToLowerInvariant())
                {
                    case "--ten": o.Name = Val(); break;
                    case "--vung-gio": o.WindZone = Val(); break;
                    case "--w0": o.W0 = Num(); break;
                    case "--dia-hinh": o.Terrain = Val(); break;
                    case "--khong-gio": o.Wind = false; break;
                    case "--khong-toi-uu": o.Optimize = false; break;
                    case "--doi-be-rong": o.KeepWidth = false; break;
                    case "--khong-ho-so": o.ExportDocs = false; break;
                    case "--den-buoc": o.StopAfter = Val(); break;
                    case "--don-gia":
                        var parts = Val().Split(',', ';');
                        if (parts.Length != 3) throw new ArgumentException("--don-gia cần 3 số: bê tông,thép,ván khuôn");
                        o.Rates = new CostRates
                        {
                            Concrete = double.Parse(parts[0], CultureInfo.InvariantCulture),
                            Rebar = double.Parse(parts[1], CultureInfo.InvariantCulture),
                            Formwork = double.Parse(parts[2], CultureInfo.InvariantCulture)
                        };
                        break;
                    case "--cong-nang":
                        var v = Val(); int eq = v.IndexOf('=');
                        if (eq <= 0) throw new ArgumentException("--cong-nang dạng \"Tên phòng=Công năng\"");
                        o.RoomKinds[v[..eq].Trim()] = v[(eq + 1)..].Trim();
                        break;
                    default:
                        if (k.StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("Không hiểu tham số " + k);
                        pos.Add(k); break;
                }
            }
            catch (Exception e) { error = e.Message; return null; }
        }
        if (pos.Count < 2) { error = "Cần: <file.vxtrevit.json> <thư mục ra>"; return null; }
        o.InputFile = pos[0]; o.OutDir = pos[1];
        if (Array.IndexOf(StepIds, o.StopAfter.ToLowerInvariant()) < 0) { error = "--den-buoc phải là một trong: " + string.Join(", ", StepIds); return null; }
        return o;
    }

    public const string Usage =
        "KetCauVXT.exe --revit-thiet-ke <file.vxtrevit.json> <thư mục ra> [tuỳ chọn]\n" +
        "  --ten \"Tên công trình\"        tên thư mục hồ sơ\n" +
        "  --vung-gio II | --w0 0.83      áp lực gió (QCVN 02:2022)\n" +
        "  --dia-hinh A|B|C               dạng địa hình (mặc định B)\n" +
        "  --khong-gio                    không tính gió\n" +
        "  --cong-nang \"Tên phòng=Công năng\"   đổi công năng (lặp lại được)\n" +
        "  --don-gia 1500000,22000,180000 đơn giá bê tông, thép, ván khuôn\n" +
        "  --khong-toi-uu                 chỉ thiết kế, không tối ưu tiết diện\n" +
        "  --doi-be-rong                  cho phép đổi bề rộng dầm / cạnh ngắn cột\n" +
        "  --den-buoc doc|tai|bo-tri|kiem-tra|phan-tich|toi-uu|ho-so   dừng sau bước để người duyệt\n" +
        "  --khong-ho-so                  không xuất bản vẽ, thuyết minh\n" +
        "Kết quả: BaoCao.md + BaoCao.json; mã thoát 0 = đạt, 1 = cần duyệt/sửa, 2 = lỗi.";
}

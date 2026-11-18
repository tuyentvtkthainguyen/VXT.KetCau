using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using KetCauVXT.Pipeline;

namespace KetCauVXT.App;

/// <summary>Chạy Việt Const không mở cửa sổ – dành cho agent / dòng lệnh / máy chủ build:
///   KetCauVXT.exe --revit-thiet-ke &lt;file.vxtrevit.json&gt; &lt;thư mục ra&gt; [tuỳ chọn]   (xem RevitDesignPipeline.Usage)
/// In tiến trình ra cửa sổ dòng lệnh đã gọi (nếu có), ghi BaoCao.md + BaoCao.json, trả mã thoát 0 / 1 / 2.
/// Chỉ đọc file đầu vào và ghi vào thư mục ra – không kết nối mạng, không thu thập dữ liệu.</summary>
internal static class HeadlessCli
{
    public const string Flag = "--revit-thiet-ke";

    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int pid);

    public const string FlagCad = "--cad-sang-phuong-an", FlagCheck = "--kiem-tra-phuong-an", FlagSample = "--phuong-an-mau", FlagRevit = "--revit-sang-phuong-an", FlagBeams = "--bo-sung-dam", FlagCoord = "--phoi-hop", FlagStair = "--thiet-ke-thang";

    public static bool Wants(string[] args) => args.Length > 0 &&
        new[] { Flag, FlagCad, FlagCheck, FlagSample, FlagRevit, FlagBeams, FlagCoord, FlagStair }.Any(f => string.Equals(args[0], f, StringComparison.OrdinalIgnoreCase));

    /// <summary>Ghi lại mọi dòng đã in – ứng dụng cửa sổ chạy bằng Start-Process không có console, nên lệnh phương án ghi thêm file *.kiemtra.txt.</summary>
    private static readonly StringBuilder Log = new();
    private static void Say(string s) { Log.AppendLine(s); try { Console.WriteLine(s); } catch { } }
    private static void SaveLog(string planFile) { try { File.WriteAllText(planFile + ".kiemtra.txt", Log.ToString(), new UTF8Encoding(false)); } catch { } }

    public static int Run(string[] args)
    {
        try { AttachConsole(-1); Console.OutputEncoding = Encoding.UTF8; } catch { }
        try
        {
            if (string.Equals(args[0], FlagCad, StringComparison.OrdinalIgnoreCase)) return CadToPlan(args.Skip(1).ToList());
            if (string.Equals(args[0], FlagCheck, StringComparison.OrdinalIgnoreCase)) return CheckPlan(args.Skip(1).ToList());
            if (string.Equals(args[0], FlagSample, StringComparison.OrdinalIgnoreCase)) return SamplePlan(args.Skip(1).ToList());
            if (string.Equals(args[0], FlagRevit, StringComparison.OrdinalIgnoreCase)) return RevitToPlan(args.Skip(1).ToList());
            if (string.Equals(args[0], FlagStair, StringComparison.OrdinalIgnoreCase)) return DesignStairs(args.Skip(1).ToList());
            if (string.Equals(args[0], FlagStair, StringComparison.OrdinalIgnoreCase)) return StairU(args.Skip(1).ToList());
            if (string.Equals(args[0], FlagBeams, StringComparison.OrdinalIgnoreCase) || string.Equals(args[0], FlagCoord, StringComparison.OrdinalIgnoreCase)) return AddBeams(args.Skip(1).ToList());
        }
        catch (Exception e) { Say("Lỗi: " + e.Message); return 2; }
        var o = RevitDesignPipeline.ParseArgs(args.Skip(1).ToList(), out var err);
        if (o == null)
        {
            Say("Lỗi tham số: " + err);
            Say(RevitDesignPipeline.Usage);
            return 2;
        }
        Say($"Việt Const – Revit → thiết kế: {o.InputFile}");
        var rep = RevitDesignPipeline.Run(o, new Progress(Say));
        try
        {
            var (md, js) = RevitDesignPipeline.WriteReport(rep, o);
            Say("");
            Say(rep.ToMarkdown());
            Say("Báo cáo: " + md);
            Say("JSON:    " + js);
        }
        catch (Exception e) { Say("Không ghi được báo cáo: " + e.Message); return 2; }
        return rep.ExitCode;
    }

    /// <summary>KetCauVXT.exe --cad-sang-phuong-an &lt;mb1.dwg&gt; [mb2.dwg …] &lt;ra.vxtphuongan.json&gt; [--ten "…"] [--cao-tang 3.3]
    /// Đọc mặt bằng kiến trúc CAD (tường, cửa, trục, chữ tên phòng, ô thang) → phương án chung cho Revit / SketchUp / Việt Const.</summary>
    private static int CadToPlan(List<string> a)
    {
        string name = "", outFile = null; double h = 0;
        var files = new List<string>();
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] == "--ten" && i + 1 < a.Count) { name = a[++i]; continue; }
            if (a[i] == "--cao-tang" && i + 1 < a.Count) { h = double.Parse(a[++i].Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture); continue; }
            files.Add(a[i]);
        }
        if (files.Count < 2) { Say("Cần: --cad-sang-phuong-an <mặt bằng .dwg/.dxf …> <ra.vxtphuongan.json> [--ten …] [--cao-tang 3.3]"); return 2; }
        outFile = files[^1]; files.RemoveAt(files.Count - 1);
        foreach (var f in files) if (!File.Exists(f)) { Say("Không thấy file " + f); return 2; }
        var o = new KetCauVXT.Import.Arch.ArchImportOptions();
        var d = KetCauVXT.Import.Arch.ArchPlanReader.ReadMany(files, o);
        if (h > 2) foreach (var fl in d.Floors) fl.Height = h;
        var ex = KetCauVXT.Import.Arch.PlanSpec.FromArchDrawing(d, string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(files[0]) : name);
        KetCauVXT.Import.Arch.PlanSpec.Save(ex, outFile);
        foreach (var i in d.Info) Say("  " + i);
        foreach (var w in d.Warnings) Say("  ⚠ " + w);
        Say($"Đã ghi {outFile}: {ex.Levels.Count} cao độ, {ex.Walls.Count} tường, {ex.Openings.Count} cửa, {ex.Rooms.Count} phòng, {ex.Stairs.Count} ô thang.");
        int rc = PrintIssues(ex);
        SaveLog(outFile);
        return rc;
    }

    /// <summary>KetCauVXT.exe --kiem-tra-phuong-an &lt;file.vxtphuongan.json&gt; → in lỗi / cảnh báo; mã thoát 0 không lỗi, 1 có lỗi.</summary>
    private static int CheckPlan(List<string> a)
    {
        if (a.Count < 1 || !File.Exists(a[0])) { Say("Cần: --kiem-tra-phuong-an <file.vxtphuongan.json>"); return 2; }
        var ex = KetCauVXT.Import.RevitJsonImporter.ReadFile(a[0]);
        Say($"{ex.ProjectName}: {ex.Levels.Count} cao độ, {ex.Walls.Count} tường, {ex.Openings.Count} cửa, {ex.Rooms.Count} phòng, {ex.Stairs.Count} cầu thang.");
        int rc = PrintIssues(ex);
        SaveLog(a[0]);
        return rc;
    }

    /// <summary>
    /// KetCauVXT.exe --revit-sang-phuong-an &lt;mô hình.vxtrevit.json&gt; &lt;ra.vxtphuongan.json&gt; [--nguon-ho &lt;dự án / thư mục họ&gt;]… [--goc-lo-dat]
    /// Mô hình Revit đã có (xuất bằng add-in) → phương án để agent làm lại dự án mới: cửa giữ họ / loại, ô thang lấy từ phòng "Cầu thang".
    /// </summary>
    private static int RevitToPlan(List<string> a)
    {
        var files = new List<string>(); var src = new List<string>(); bool origin = false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] == "--nguon-ho" && i + 1 < a.Count) { src.Add(a[++i]); continue; }
            if (a[i] == "--goc-lo-dat") { origin = true; continue; }
            files.Add(a[i]);
        }
        if (files.Count != 2 || !File.Exists(files[0])) { Say("Cần: --revit-sang-phuong-an <mô hình.vxtrevit.json> <ra.vxtphuongan.json> [--nguon-ho <file/thư mục>]… [--goc-lo-dat]"); return 2; }
        var ex = KetCauVXT.Import.RevitJsonImporter.ReadFile(files[0]);
        var plan = KetCauVXT.Import.Arch.PlanSpec.FromRevit(ex, src, origin);
        KetCauVXT.Import.Arch.PlanSpec.Save(plan, files[1]);
        Say($"Đã ghi {files[1]}: {plan.Levels.Count} cao độ, {plan.Walls.Count} tường, {plan.Openings.Count} cửa, {plan.Rooms.Count} phòng, {plan.Stairs.Count} ô thang (cần thiết kế vế thang – Runs).");
        int rc = PrintIssues(plan);
        SaveLog(files[1]);
        return rc;
    }

    /// <summary>
    /// KetCauVXT.exe --thang-u &lt;file.vxtphuongan.json&gt; --id CT1 --tu "Tầng 1" --den "Tầng 2" --long x0,y0,x1,y1
    ///   [--bac 21] [--ve-giua 3] [--rong 1.0] [--huong -Y] [--ben max|min]
    /// Lập (hoặc thay) thang chữ U 3 vế theo quy ước 21 bậc / tầng, vế giữa 2–4 bậc, trong lồng thang (mặt trong tường).
    /// </summary>
    private static int StairU(List<string> a)
    {
        string file = null, id = null, from = null, to = null, box = null;
        var o = new KetCauVXT.Import.Arch.StairDesign.U3Options();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        for (int i = 0; i < a.Count; i++)
        {
            string v = i + 1 < a.Count ? a[i + 1] : null;
            switch (a[i])
            {
                case "--id": id = v; i++; break;
                case "--tu": from = v; i++; break;
                case "--den": to = v; i++; break;
                case "--long": box = v; i++; break;
                case "--bac": o.Risers = int.Parse(v, inv); i++; break;
                case "--ve-giua": o.Middle = int.Parse(v, inv); i++; break;
                case "--rong": o.Width = double.Parse(v.Replace(',', '.'), inv); i++; break;
                case "--huong": o.Run1Dir = v; i++; break;
                case "--ben": o.Run1AtMax = !string.Equals(v, "min", StringComparison.OrdinalIgnoreCase); i++; break;
                default: file = a[i]; break;
            }
        }
        if (file == null || !File.Exists(file) || id == null || from == null || to == null || box == null)
        { Say("Cần: --thang-u <phương án> --id CT1 --tu \"Tầng 1\" --den \"Tầng 2\" --long x0,y0,x1,y1 [--bac 21] [--ve-giua 3] [--rong 1.0] [--huong -Y] [--ben max]"); return 2; }
        var p4 = box.Split(new[] { ',', ';' }).Select(x => double.Parse(x.Trim(), inv)).ToArray();
        if (p4.Length != 4) { Say("--long cần 4 số x0,y0,x1,y1 (m)."); return 2; }
        var ex = KetCauVXT.Import.RevitJsonImporter.ReadFile(file);
        var b = ex.Levels.FirstOrDefault(l => l.Name == from); var t = ex.Levels.FirstOrDefault(l => l.Name == to);
        if (b == null || t == null) { Say("Không có cao độ " + (b == null ? from : to)); return 2; }
        var (st, notes) = KetCauVXT.Import.Arch.StairDesign.U3(id, b, t, p4[0], p4[1], p4[2], p4[3], o);
        foreach (var n in notes) Say(n);
        if (notes.Any(n => n.StartsWith("Lỗi"))) return 1;
        ex.Stairs.RemoveAll(x => x.Id == id);
        ex.Stairs.Add(st);
        KetCauVXT.Import.Arch.PlanSpec.Save(ex, file);
        Say("Đã ghi " + file + " – chạy lại --phoi-hop để cập nhật dầm quanh lỗ thang.");
        return PrintIssues(ex);
    }

    /// <summary>
    /// KetCauVXT.exe --phoi-hop &lt;file.vxtphuongan.json&gt; [--cot 0.22x0.22] [--dam-min 0.3] [--dam-bo 0.2x0.3] (tên cũ: --bo-sung-dam)
    /// Phối hợp hình học theo tiết diện: cột tại nút tường 220 / giao trục, dầm khung theo nhịp, dầm bo, dầm đỡ tường, hạ tường tới đáy dầm / sàn,
    /// dời cửa khỏi cột, hạ dầm qua trên thang; ghi đè file (bản cũ *.truoc-phoi-hop.json) rồi kiểm tra va chạm.
    /// </summary>
    private static int AddBeams(List<string> a)
    {
        string file = null;
        var o = new KetCauVXT.Import.Arch.CoordOptions();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        double P(string v) => double.Parse(v.Replace(',', '.'), inv);
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] == "--cao-dam" && i + 1 < a.Count) { o.MinBeamH = Math.Min(0.3, P(a[++i])); continue; }
            if (a[i] == "--dam-min" && i + 1 < a.Count) { o.MinBeamH = P(a[++i]); continue; }
            if (a[i] == "--dam-bo" && i + 1 < a.Count) { var p = a[++i].ToLowerInvariant().Split('x'); o.EdgeB = P(p[0]); if (p.Length > 1) o.EdgeH = P(p[1]); continue; }
            if (a[i] == "--cot" && i + 1 < a.Count) { var p = a[++i].ToLowerInvariant().Split('x'); o.ColB = P(p[0]); o.ColH = p.Length > 1 ? P(p[1]) : o.ColB; continue; }
            file = a[i];
        }
        if (file == null || !File.Exists(file)) { Say("Cần: --phoi-hop <file.vxtphuongan.json> [--cot 0.22x0.22] [--dam-min 0.3] [--dam-bo 0.2x0.3]"); return 2; }
        if (o.ColB < 0.15 || o.ColH < 0.15 || o.MinBeamH < 0.2 || o.MinBeamH > 1.0) { Say("Tiết diện không hợp lệ (cột ≥ 150 mm, dầm 200–1000 mm)."); return 2; }
        var ex = KetCauVXT.Import.RevitJsonImporter.ReadFile(file);
        try { File.Copy(file, Path.ChangeExtension(file, null) + ".truoc-phoi-hop.json", true); } catch { }
        foreach (var l in KetCauVXT.Import.Arch.PlanCoordination.Coordinate(ex, o)) Say(l);
        KetCauVXT.Import.Arch.PlanSpec.Save(ex, file);
        Say("Đã ghi " + file);
        int rc = PrintIssues(ex);
        SaveLog(file);
        return rc;
    }

    /// <summary>KetCauVXT.exe --thiet-ke-thang &lt;file&gt; [--bac 21] [--giua 3] [--rong 1.0] [--mat-bac 0.26] [--thang CT1]
    /// Thiết kế lại cầu thang thành chữ U 3 vế (21 bậc / tầng thường, vế giữa 2–4 bậc) trong ô thang; ghi đè file (bản cũ *.truoc-thang.json).</summary>
    private static int DesignStairs(List<string> a)
    {
        string file = null, only = null;
        var o = new KetCauVXT.Import.Arch.PlanStairs.Options();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] == "--bac" && i + 1 < a.Count) { o.Risers = int.Parse(a[++i], inv); continue; }
            if (a[i] == "--giua" && i + 1 < a.Count) { o.Middle = int.Parse(a[++i], inv); continue; }
            if (a[i] == "--rong" && i + 1 < a.Count) { o.Width = double.Parse(a[++i].Replace(',', '.'), inv); continue; }
            if (a[i] == "--mat-bac" && i + 1 < a.Count) { o.Tread = double.Parse(a[++i].Replace(',', '.'), inv); continue; }
            if (a[i] == "--thang" && i + 1 < a.Count) { only = a[++i]; continue; }
            file = a[i];
        }
        if (file == null || !File.Exists(file)) { Say("Cần: --thiet-ke-thang <file.vxtphuongan.json> [--bac 21] [--giua 3] [--rong 1.0] [--mat-bac 0.26] [--thang CT1]"); return 2; }
        if (o.Middle < 2 || o.Middle > 4 || o.Tread < 0.25 || o.Tread > 0.32) { Say("Vế giữa 2–4 bậc, mặt bậc 0,25–0,32 m."); return 2; }
        var ex = KetCauVXT.Import.RevitJsonImporter.ReadFile(file);
        try { File.Copy(file, Path.ChangeExtension(file, null) + ".truoc-thang.json", true); } catch { }
        foreach (var t in ex.Stairs.Where(t => only == null || t.Id == only)) Say(KetCauVXT.Import.Arch.PlanStairs.DesignU3(ex, t, o));
        KetCauVXT.Import.Arch.PlanSpec.Save(ex, file);
        Say("Đã ghi " + file + " – chạy lại --phoi-hop để cập nhật dầm / thông thủy.");
        int rc = PrintIssues(ex);
        SaveLog(file);
        return rc;
    }

    /// <summary>KetCauVXT.exe --phuong-an-mau &lt;ra.vxtphuongan.json&gt; → nhà phố mẫu 5 × 20 m làm khuôn cho agent.</summary>
    private static int SamplePlan(List<string> a)
    {
        if (a.Count < 1) { Say("Cần: --phuong-an-mau <ra.vxtphuongan.json>"); return 2; }
        KetCauVXT.Import.Arch.PlanSpec.Save(KetCauVXT.Import.Arch.PlanSpec.SampleTownhouse(), a[0]);
        Say("Đã ghi " + a[0]);
        return 0;
    }

    private static int PrintIssues(KetCauVXT.Import.RevitExchange ex)
    {
        var iss = KetCauVXT.Import.Arch.PlanSpec.Validate(ex);
        foreach (var i in iss) Say("  " + i);
        int err = iss.Count(i => i.Error);
        Say(err == 0 ? $"Không có lỗi ({iss.Count} cảnh báo)." : $"{err} lỗi, {iss.Count - err} cảnh báo – sửa phương án trước khi dựng.");
        return err == 0 ? 0 : 1;
    }

    private sealed class Progress : IProgress<string>
    {
        private readonly Action<string> _say;
        private string _last = "";
        public Progress(Action<string> say) => _say = say;
        public void Report(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == _last) return;
            _last = value;
            _say("  " + value);
        }
    }
}

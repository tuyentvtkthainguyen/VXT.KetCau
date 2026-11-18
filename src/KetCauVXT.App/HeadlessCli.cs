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

    public const string FlagCad = "--cad-sang-phuong-an", FlagCheck = "--kiem-tra-phuong-an", FlagSample = "--phuong-an-mau";

    public static bool Wants(string[] args) => args.Length > 0 &&
        new[] { Flag, FlagCad, FlagCheck, FlagSample }.Any(f => string.Equals(args[0], f, StringComparison.OrdinalIgnoreCase));

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

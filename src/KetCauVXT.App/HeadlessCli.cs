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

    public static bool Wants(string[] args) => args.Length > 0 && string.Equals(args[0], Flag, StringComparison.OrdinalIgnoreCase);

    public static int Run(string[] args)
    {
        try { AttachConsole(-1); Console.OutputEncoding = Encoding.UTF8; } catch { }
        void Say(string s) { try { Console.WriteLine(s); } catch { } }
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

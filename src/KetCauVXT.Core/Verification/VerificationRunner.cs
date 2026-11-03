using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace KetCauVXT.Verification;

/// <summary>Chạy toàn bộ bộ kiểm chứng (Benchmarks.All) và gom kết quả. Không phụ thuộc dự án đang mở: mọi mô hình kiểm chứng
/// được dựng riêng với số liệu cố định (không dùng mẫu định dạng / thiết lập của người dùng) để kết quả lặp lại được.</summary>
public static class VerificationRunner
{
    /// <summary>Tiến trình: (đã chạy, tổng số bài toán, tên bài toán đang/đã chạy).</summary>
    public readonly record struct Progress(int Done, int Total, string Title);

    /// <summary>Chạy mọi bài toán. userData != null → gán giá trị theo tài liệu người dùng đã lưu.</summary>
    public static VerificationResult RunAll(IProgress<Progress> progress = null, VerificationUserData userData = null, CancellationToken cancel = default)
    {
        var res = new VerificationResult { Started = DateTime.Now, Version = ProgramVersion(), RuntimeInfo = RuntimeText() };
        var sw = Stopwatch.StartNew();
        var suites = Benchmarks.All();
        for (int i = 0; i < suites.Count; i++)
        {
            cancel.ThrowIfCancellationRequested();
            var s = suites[i];
            progress?.Report(new Progress(i, suites.Count, s.Id + " " + s.Title));
            res.Cases.AddRange(s.Run());
        }
        res.Skipped.AddRange(Benchmarks.SkippedProblems());
        userData?.ApplyTo(res.Cases);
        sw.Stop();
        res.Elapsed = sw.Elapsed;
        progress?.Report(new Progress(suites.Count, suites.Count, "Hoàn tất"));
        return res;
    }

    /// <summary>Phiên bản lõi tính KetCauVXT.Core (AssemblyInformationalVersion hoặc Version).</summary>
    public static string ProgramVersion()
    {
        var asm = typeof(VerificationRunner).Assembly;
        var info = asm.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion;
        string v = !string.IsNullOrWhiteSpace(info) ? info : asm.GetName().Version?.ToString() ?? "?";
        int plus = v.IndexOf('+');
        if (plus > 0 && v.Length - plus > 9) v = v.Substring(0, plus + 9);   // rút gọn mã commit
        return "Kết cấu VXT " + v;
    }

    /// <summary>Mô tả môi trường chạy: .NET, hệ điều hành, kiến trúc, số nhân, máy tính.</summary>
    public static string RuntimeText()
    {
        try
        {
            return $"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture}); "
                   + $"{Environment.ProcessorCount} luồng CPU; số thực double IEEE 754 (15–16 chữ số có nghĩa); văn hoá số {CultureInfo.CurrentCulture.Name}";
        }
        catch (Exception) { return Environment.Version.ToString(); }
    }
}

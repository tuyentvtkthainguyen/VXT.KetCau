using System.Diagnostics;
using System.Text.RegularExpressions;

namespace KetCauVXT.Import;

/// <summary>Chuyển bản vẽ DWG → DXF bằng ODA File Converter (miễn phí, opendesign.com) nếu máy có cài.</summary>
public static class DwgConverter
{
    public const string HowToMessage =
        "Không đọc trực tiếp được DWG. Lưu bản vẽ dạng DXF (SaveAs DXF) hoặc dùng add-in AutoCAD VXT; hoặc cài ODA File Converter (miễn phí, opendesign.com) để chương trình tự chuyển.";

    /// <summary>Thời gian chờ tối đa cho một lần chuyển đổi, ms.</summary>
    public const int TimeoutMs = 120_000;

    /// <summary>Biến môi trường cho phép chỉ định đường dẫn ODAFileConverter.exe.</summary>
    public const string EnvVar = "ODA_FILE_CONVERTER";

    private static readonly Regex VersionRx = new(@"\d+(\.\d+)*", RegexOptions.Compiled);

    /// <summary>Tìm ODAFileConverter.exe (C:\Program Files\ODA\*\, Program Files (x86)), bản mới nhất trước.
    /// Trả null nếu không có. Không bao giờ ném lỗi (kể cả trên Linux/macOS).</summary>
    public static string FindOdaConverter()
    {
        try
        {
            string env = Environment.GetEnvironmentVariable(EnvVar);
            if (!string.IsNullOrWhiteSpace(env))
            {
                env = env.Trim().Trim('"');
                if (File.Exists(env)) return env;
                string inDir = Path.Combine(env, "ODAFileConverter.exe");
                if (File.Exists(inDir)) return inDir;
            }

            if (!OperatingSystem.IsWindows()) return null;

            var roots = new List<string>();
            void AddRoot(string r)
            {
                if (!string.IsNullOrEmpty(r) && !roots.Contains(r, StringComparer.OrdinalIgnoreCase)) roots.Add(r);
            }
            AddRoot(SafeFolder(Environment.SpecialFolder.ProgramFiles));
            AddRoot(SafeFolder(Environment.SpecialFolder.ProgramFilesX86));
            AddRoot(@"C:\Program Files");
            AddRoot(@"C:\Program Files (x86)");

            var found = new List<(Version ver, string path)>();
            foreach (var root in roots)
            {
                string oda = Path.Combine(root, "ODA");
                if (!Directory.Exists(oda)) continue;
                string direct = Path.Combine(oda, "ODAFileConverter.exe");
                if (File.Exists(direct)) found.Add((new Version(0, 0), direct));
                string[] dirs;
                try { dirs = Directory.GetDirectories(oda); } catch (Exception) { continue; }
                foreach (var d in dirs)
                {
                    string exe = Path.Combine(d, "ODAFileConverter.exe");
                    if (File.Exists(exe)) found.Add((ParseVersion(Path.GetFileName(d)), exe));
                }
            }
            if (found.Count == 0) return null;
            return found.OrderByDescending(f => f.ver).ThenByDescending(f => f.path, StringComparer.OrdinalIgnoreCase).First().path;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string SafeFolder(Environment.SpecialFolder f)
    {
        try { return Environment.GetFolderPath(f); } catch (Exception) { return null; }
    }

    private static Version ParseVersion(string dirName)
    {
        var m = VersionRx.Match(dirName ?? "");
        if (!m.Success) return new Version(0, 0);
        string s = m.Value;
        if (!s.Contains('.')) s += ".0";
        // Version chỉ nhận tối đa 4 thành phần
        var parts = s.Split('.');
        if (parts.Length > 4) s = string.Join(".", parts.Take(4));
        return Version.TryParse(s, out var v) ? v : new Version(0, 0);
    }

    /// <summary>Chuyển DWG → DXF (ACAD2018, ASCII) vào thư mục tạm. Trả đường dẫn file .dxf, hoặc null kèm thông báo lỗi.</summary>
    public static string ConvertToDxf(string dwgPath, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(dwgPath) || !File.Exists(dwgPath))
        {
            error = $"Không tìm thấy file '{dwgPath}'.";
            return null;
        }
        string exe = FindOdaConverter();
        if (exe == null)
        {
            error = HowToMessage;
            return null;
        }

        string work = Path.Combine(Path.GetTempPath(), "KetCauVXT_dwg_" + Guid.NewGuid().ToString("N"));
        string inDir = Path.Combine(work, "in");
        string outDir = Path.Combine(work, "out");
        try
        {
            Directory.CreateDirectory(inDir);
            Directory.CreateDirectory(outDir);
            string fileName = Path.GetFileName(dwgPath);
            File.Copy(dwgPath, Path.Combine(inDir, fileName), true);

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = work,
            };
            // ODAFileConverter "<in>" "<out>" <phiên bản ra> <kiểu ra> <đệ quy 0/1> <kiểm tra lỗi 0/1> "<bộ lọc>"
            psi.ArgumentList.Add(inDir);
            psi.ArgumentList.Add(outDir);
            psi.ArgumentList.Add("ACAD2018");
            psi.ArgumentList.Add("DXF");
            psi.ArgumentList.Add("0");
            psi.ArgumentList.Add("1");
            psi.ArgumentList.Add(fileName);

            using (var proc = Process.Start(psi))
            {
                if (proc == null)
                {
                    error = "Không khởi chạy được ODA File Converter. " + HowToMessage;
                    return null;
                }
                if (!proc.WaitForExit(TimeoutMs))
                {
                    try { proc.Kill(true); } catch (Exception) { /* bỏ qua */ }
                    error = $"ODA File Converter chạy quá {TimeoutMs / 1000} giây – đã dừng. Hãy mở bản vẽ trong AutoCAD và SAVEAS → DXF.";
                    return null;
                }
            }

            string dxf = Directory.GetFiles(outDir, "*.dxf", SearchOption.AllDirectories)
                .OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
            if (dxf == null)
            {
                error = "ODA File Converter không tạo được file DXF (bản vẽ có thể bị hỏng hoặc có mật khẩu). " + HowToMessage;
                return null;
            }
            return dxf;
        }
        catch (Exception ex)
        {
            error = "Lỗi khi chuyển DWG → DXF: " + ex.Message + ". " + HowToMessage;
            return null;
        }
        finally
        {
            TryDelete(inDir);
        }
    }

    /// <summary>Đọc file .dxf hoặc .dwg (DWG được chuyển qua ODA File Converter). Trả null kèm error nếu không đọc được.
    /// scaleOverride &gt; 0: hệ số đơn vị bản vẽ → m như DxfReader.Read.</summary>
    public static DxfReader ReadAny(string path, double scaleOverride, out string error)
    {
        error = null;
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                error = $"Không tìm thấy file '{path}'.";
                return null;
            }
            if (!IsDwg(path)) return DxfReader.Read(path, scaleOverride);

            string dxf = ConvertToDxf(path, out error);
            if (dxf == null) return null;
            try
            {
                var r = DxfReader.Read(dxf, scaleOverride);
                r.Warnings.Insert(0, "Đã chuyển DWG → DXF bằng ODA File Converter.");
                return r;
            }
            finally
            {
                // xoá thư mục tạm (…\KetCauVXT_dwg_xxx\out\file.dxf → …\KetCauVXT_dwg_xxx)
                string outDir = Path.GetDirectoryName(dxf);
                string work = outDir == null ? null : Path.GetDirectoryName(outDir);
                if (work != null && Path.GetFileName(work).StartsWith("KetCauVXT_dwg_", StringComparison.Ordinal)) TryDelete(work);
            }
        }
        catch (Exception ex)
        {
            error = "Lỗi đọc bản vẽ: " + ex.Message;
            return null;
        }
    }

    /// <summary>Nhận dạng DWG theo phần mở rộng hoặc chữ ký đầu file ("AC10xx").</summary>
    public static bool IsDwg(string path)
    {
        if (string.Equals(Path.GetExtension(path), ".dwg", StringComparison.OrdinalIgnoreCase)) return true;
        try
        {
            using var fs = File.OpenRead(path);
            var buf = new byte[6];
            int n = fs.Read(buf, 0, 6);
            return n == 6 && buf[0] == 'A' && buf[1] == 'C' && buf[2] == '1' && buf[3] == '0'
                   && char.IsDigit((char)buf[4]) && char.IsDigit((char)buf[5]);
        }
        catch (Exception) { return false; }
    }

    private static void TryDelete(string dir)
    {
        try { if (dir != null && Directory.Exists(dir)) Directory.Delete(dir, true); } catch (Exception) { /* bỏ qua */ }
    }
}

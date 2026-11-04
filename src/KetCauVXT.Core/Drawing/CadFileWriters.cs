using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Điểm cắm bộ ghi bản vẽ DXF 2018 / DWG nằm ngoài lõi (lõi không dùng gói NuGet nào).
/// Ứng dụng (App.xaml.cs) và bộ kiểm thử đăng ký bộ ghi của thư viện KetCauVXT.Dwg (ACadSharp):
/// <c>CadFileWriters.Writer = KetCauVXT.Dwg.CadDocWriter.Write;</c> (hoặc <c>CadDocWriter.Register()</c>).
/// Chưa đăng ký (hoặc định dạng R12) → ghi DXF R12 bằng bộ ghi gốc <see cref="DxfWriter.Save"/>.</summary>
public static class CadFileWriters
{
    /// <summary>Bộ ghi DXF 2018 / DWG 2018: (bộ ghi, đường dẫn đã đúng phần mở rộng, định dạng). null = chưa đăng ký.</summary>
    public static Action<DxfWriter, string, CadOutputFormat> Writer { get; set; }

    /// <summary>Đã có bộ ghi DXF 2018 / DWG.</summary>
    public static bool HasWriter => Writer != null;

    private static CadOutputFormat? _override;

    /// <summary>Định dạng đang áp dụng: tạm (UseFormat) → CadStandard.Active.OutputFormat → DXF R12.</summary>
    public static CadOutputFormat Current => _override ?? CadStandard.Active?.OutputFormat ?? CadOutputFormat.DxfR12;

    /// <summary>Định dạng thực sự ghi được (DXF 2018 / DWG khi chưa có bộ ghi → R12).</summary>
    public static CadOutputFormat Effective(CadOutputFormat f) => f != CadOutputFormat.DxfR12 && !HasWriter ? CadOutputFormat.DxfR12 : f;

    /// <summary>Tạm dùng định dạng khác cho một lần xuất (null = giữ nguyên); Dispose → trả lại.
    /// Đặt bao quanh cả bước dựng bản vẽ để tuỳ chọn "Tô vật liệu: tự động" theo đúng định dạng.</summary>
    public static IDisposable UseFormat(CadOutputFormat? f)
    {
        var prev = _override;
        if (f.HasValue) _override = f;
        return new Restore(prev);
    }

    private sealed class Restore : IDisposable
    {
        private readonly CadOutputFormat? _prev; private bool _done;
        public Restore(CadOutputFormat? prev) { _prev = prev; }
        public void Dispose() { if (_done) return; _done = true; _override = _prev; }
    }

    /// <summary>Đổi phần mở rộng theo định dạng (".dwg" / ".dxf").</summary>
    public static string PathFor(string path, CadOutputFormat f) =>
        string.IsNullOrEmpty(path) ? path : Path.ChangeExtension(path, CadOutputFormats.Extension(Effective(f)));

    /// <summary>Ghi bộ ghi ra file theo định dạng; trả về đường dẫn thực đã ghi (phần mở rộng .dxf / .dwg theo định dạng).
    /// Chưa đăng ký bộ ghi ngoài → DXF R12. Bộ ghi ngoài lỗi → ném ngoại lệ (không ghi đè bằng R12 để người dùng biết).</summary>
    public static string Write(DxfWriter w, string path, CadOutputFormat fmt)
    {
        if (w == null) throw new ArgumentNullException(nameof(w));
        var f = Effective(fmt);
        string p = PathFor(path, f);
        string dir = Path.GetDirectoryName(Path.GetFullPath(p));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        if (f == CadOutputFormat.DxfR12) { w.Save(p); return p; }
        Writer(w, p, f);
        return p;
    }

    /// <summary>Ghi theo định dạng đang áp dụng (<see cref="Current"/>).</summary>
    public static string Write(DxfWriter w, string path) => Write(w, path, Current);
}

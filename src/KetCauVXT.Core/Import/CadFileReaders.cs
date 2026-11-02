namespace KetCauVXT.Import;

/// <summary>Điểm cắm bộ đọc bản vẽ CAD nằm ngoài lõi (lõi không dùng gói NuGet nào).
/// Ứng dụng (App.xaml.cs) và bộ kiểm thử đăng ký bộ đọc DWG của thư viện KetCauVXT.Dwg (ACadSharp, giấy phép MIT):
/// <c>CadFileReaders.Dwg = KetCauVXT.Dwg.DwgReader.Read;</c>
/// <para>Mọi nơi đọc bản vẽ (ArchPlanReader, ArchLayers.Scan, DxfImporter) đi qua <see cref="DwgConverter.ReadAny"/> /
/// <see cref="Read"/>: file .dwg dùng bộ đọc đã đăng ký, nếu chưa có (hoặc đọc lỗi) thì thử ODA File Converter.</para></summary>
public static class CadFileReaders
{
    /// <summary>Bộ đọc DWG: (đường dẫn, hệ số đơn vị bản vẽ → m ghi đè; 0 = theo $INSUNITS) → DxfReader đã nạp thực thể
    /// (cùng quy ước với bộ đọc DXF: toạ độ m, chỉ model space, block đã phân rã, bảng layer, miền tô).
    /// Ném ngoại lệ có thông báo tiếng Việt khi không đọc được. null = chưa đăng ký.</summary>
    public static Func<string, double, DxfReader> Dwg { get; set; }

    /// <summary>Đã có bộ đọc DWG tích hợp.</summary>
    public static bool HasDwgReader => Dwg != null;

    /// <summary>Bộ lọc hộp thoại mở file cho bản vẽ AutoCAD.</summary>
    public const string OpenFilter = "Bản vẽ AutoCAD (*.dwg;*.dxf)|*.dwg;*.dxf|Bản vẽ DXF (*.dxf)|*.dxf|Bản vẽ DWG (*.dwg)|*.dwg";

    /// <summary>Đọc .dxf hoặc .dwg (nhận DWG theo phần mở rộng hoặc chữ ký "AC10xx"). Trả null kèm error nếu không đọc được.</summary>
    public static DxfReader Read(string path, double scaleOverride, out string error)
    {
        error = null;
        if (DwgConverter.IsDwg(path)) return DwgConverter.ReadAny(path, scaleOverride, out error);
        return DxfReader.Read(path, scaleOverride);
    }
}

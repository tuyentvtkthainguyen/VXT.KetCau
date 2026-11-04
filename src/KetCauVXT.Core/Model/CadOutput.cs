namespace KetCauVXT.Model;

/// <summary>Định dạng file bản vẽ khi xuất (Chuẩn layer CAD → Định dạng xuất; mặc định người dùng + theo dự án).</summary>
public enum CadOutputFormat
{
    /// <summary>DXF R12 (AC1009) – bộ ghi gốc của chương trình, mở được bằng mọi phần mềm CAD; HATCH được mô phỏng bằng LINE/SOLID</summary>
    DxfR12,
    /// <summary>DXF 2018 (AC1032, UTF-8) – có HATCH thật, LWPOLYLINE, nét in theo layer (ghi qua thư viện ACadSharp – KetCauVXT.Dwg)</summary>
    Dxf2018,
    /// <summary>DWG 2018 (AC1032) – có HATCH thật (ghi qua thư viện ACadSharp – KetCauVXT.Dwg)</summary>
    Dwg2018
}

/// <summary>Tên hiển thị, phần mở rộng của định dạng xuất.</summary>
public static class CadOutputFormats
{
    public static readonly CadOutputFormat[] All = { CadOutputFormat.DxfR12, CadOutputFormat.Dxf2018, CadOutputFormat.Dwg2018 };

    public static string Label(CadOutputFormat f) => f switch
    {
        CadOutputFormat.Dxf2018 => "DXF 2018 (có HATCH)",
        CadOutputFormat.Dwg2018 => "DWG 2018 (có HATCH)",
        _ => "DXF R12 (tương thích cũ)"
    };

    /// <summary>".dwg" cho DWG, còn lại ".dxf".</summary>
    public static string Extension(CadOutputFormat f) => f == CadOutputFormat.Dwg2018 ? ".dwg" : ".dxf";

    /// <summary>Đọc lại từ tên hiển thị / tên enum ("DXF 2018", "Dwg2018"…); không nhận ra → R12.</summary>
    public static CadOutputFormat Parse(string s)
    {
        string t = (s ?? "").Trim().ToUpperInvariant().Replace(" ", "");
        if (t.StartsWith("DWG", StringComparison.Ordinal)) return CadOutputFormat.Dwg2018;
        if (t.StartsWith("DXF2018", StringComparison.Ordinal) || t == "DXF2018(CÓHATCH)") return CadOutputFormat.Dxf2018;
        return CadOutputFormat.DxfR12;
    }
}

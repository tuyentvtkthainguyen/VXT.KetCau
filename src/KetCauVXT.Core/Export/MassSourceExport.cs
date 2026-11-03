using System.Globalization;
using KetCauVXT.Model;

namespace KetCauVXT.Export;

/// <summary>Nguồn khối lượng (Mass Source) xuất sang ETABS (.e2k) / SAP2000 (.s2k) cùng định nghĩa với phân tích dao động riêng của
/// chương trình (ModalSettings.Effective) để chu kỳ dao động hai bên so sánh được.
/// – Trường hợp có hệ số trọng lượng bản thân (TT): dùng "khối lượng bản thân phần tử" (Element self mass) của ETABS/SAP thay cho
///   phần trọng lượng bản thân trong trường hợp tải (ETABS/SAP không đổi trọng lượng bản thân của load pattern thành khối lượng khi đã
///   chọn Element self mass – tránh tính hai lần; người dùng kiểm tra lại trong Define → Mass Source).
/// – Chỉ khối lượng ngang (lateral only) và gộp về cao trình sàn, trừ khi chọn kể khối lượng đứng.
/// Cú pháp e2k theo ETABS 2016+; nếu bản ETABS đang dùng báo lỗi dòng MASSSOURCE thì khai báo lại Mass Source theo bảng trong thuyết minh.</summary>
public static class MassSourceExport
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string F(double v) => v.ToString("0.######", Inv);
    public const string Name = "MsSrc1";

    public static List<string> E2kLines(Project p)
    {
        var r = new List<string>();
        var ms = p.Modal ?? new ModalSettings();
        var src = ms.Effective(p);
        if (src.Count == 0) return r;
        bool self = src.Any(x => x.pat.SelfWeight != 0 && x.factor != 0);
        r.Add("$ MASS SOURCE");
        r.Add($"  MASSSOURCE  \"{Name}\"  INCLUDEELEMENTS \"{(self ? "Yes" : "No")}\"  INCLUDEADDEDMASS \"Yes\"  INCLUDELOADS \"Yes\"  INCLUDELATERALONLY \"{(ms.IncludeVerticalMass ? "No" : "Yes")}\"  LUMPMASS \"Yes\"  DEFAULT \"Yes\"");
        foreach (var (pat, f) in src)
            r.Add($"  MASSSOURCE  \"{Name}\"  LOADPAT \"{pat.Name}\"  MULTIPLIER {F(f)}");
        r.Add("");
        return r;
    }

    public static List<string> S2kLines(Project p, Func<string, string> quote)
    {
        var r = new List<string>();
        var ms = p.Modal ?? new ModalSettings();
        var src = ms.Effective(p);
        if (src.Count == 0) return r;
        bool self = src.Any(x => x.pat.SelfWeight != 0 && x.factor != 0);
        r.Add("");
        r.Add("TABLE:  \"MASS SOURCE\"");
        foreach (var (pat, f) in src)
            r.Add($"   MassSource={Name}   Elements={(self ? "Yes" : "No")}   Masses=Yes   Loads=Yes   IsDefault=Yes   LoadPat={quote(pat.Name)}   Multiplier={F(f)}");
        return r;
    }
}

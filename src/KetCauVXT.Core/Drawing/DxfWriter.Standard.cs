using System.Text;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Chuẩn layer CAD khi ghi DXF: đổi tên layer nội bộ (BE_TONG, THEP…) → tên/màu/kiểu nét theo CadStandard,
/// ghi bảng LTYPE (đủ định nghĩa mọi kiểu nét được dùng), LAYER (gộp các layer cùng tên xuất) và STYLE (arial.ttf / times.ttf).
/// Không có chuẩn (Standard và CadStandard.Active đều null) → ghi như cũ.</summary>
public sealed partial class DxfWriter
{
    /// <summary>Chuẩn layer riêng cho bộ ghi này (null = CadStandard.Active).</summary>
    public CadStandard Standard { get; set; }

    /// <summary>Định nghĩa kiểu nét DXF R12: tên, mô tả, chiều dài mẫu, các đoạn (dương = nét, âm = khe, 0 = chấm).</summary>
    private static readonly (string Name, string Desc, double Total, double[] Elems)[] LtypeDefs =
    {
        ("CONTINUOUS", "Solid line", 0.0, Array.Empty<double>()),
        ("DASHED", "__ __ __ __", 19.05, new[] { 12.7, -6.35 }),
        ("CENTER", "____ _ ____ _", 50.8, new[] { 31.75, -6.35, 6.35, -6.35 }),
        ("HIDDEN", "_ _ _ _ _ _", 9.525, new[] { 6.35, -3.175 }),
        ("DASHDOT", "__ . __ . __", 25.4, new[] { 12.7, -6.35, 0.0, -6.35 }),
    };

    /// <summary>Các layer (tên nội bộ, trước khi đổi theo chuẩn) mà thực thể của bộ ghi này đang dùng – theo thứ tự gặp.</summary>
    public List<string> UsedLayers()
    {
        var res = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in _ents)
            foreach (var (code, val) in e.Codes)
                if (code == 8 && val is string ln && seen.Add(ln)) res.Add(ln);
        return res;
    }

    /// <summary>Tên layer nội bộ → tên layer xuất (chỉ các layer có khai báo trong chuẩn).</summary>
    private static Dictionary<string, string> OutputNameMap(CadStandard std)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (std?.OutputLayers == null) return map;
        foreach (var o in std.OutputLayers)
            if (o != null && !string.IsNullOrEmpty(o.Key) && !map.ContainsKey(o.Key))
                map[o.Key] = CadStandard.SafeLayerName(string.IsNullOrWhiteSpace(o.Name) ? o.Key : o.Name);
        return map;
    }

    /// <summary>Tên layer xuất của một layer nội bộ theo chuẩn đang áp dụng (để hiển thị/kiểm thử).</summary>
    public static string OutputLayerName(string internalLayer, CadStandard std = null)
    {
        std ??= CadStandard.Active;
        if (std == null || internalLayer == null) return internalLayer;
        return OutputNameMap(std).TryGetValue(internalLayer, out var n) ? n : internalLayer;
    }

    private void WriteTablesStd(StringBuilder sb, CadStandard std)
    {
        var map = OutputNameMap(std);
        // ---- layer: theo thứ tự LayerDefs, rồi layer khác trong chuẩn, rồi layer thực thể dùng mà chưa khai báo ----
        var layers = new List<(string Name, int Color, string LType)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddLayer(string name, int color, string lt)
        {
            if (string.IsNullOrEmpty(name) || !seen.Add(name)) return;
            layers.Add((name, color is >= 1 and <= 255 ? color : 7, CadStandard.NormalizeLinetype(lt)));
        }
        foreach (var (key, color, lt) in LayerDefs)
        {
            var o = std.Output(key);
            if (o == null) AddLayer(key, color, lt);
            else AddLayer(map[key], o.Color, o.Linetype);
        }
        foreach (var o in std.OutputLayers ?? new List<OutputLayer>())
            if (o != null && !string.IsNullOrEmpty(o.Key) && map.TryGetValue(o.Key, out var on)) AddLayer(on, o.Color, o.Linetype);
        var usedLt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in _ents)
            foreach (var (code, val) in e.Codes)
            {
                if (code == 8 && val is string ln) AddLayer(map.TryGetValue(ln, out var on2) ? on2 : CadStandard.SafeLayerName(ln), 7, "CONTINUOUS");
                else if (code == 6 && val is string lt6) usedLt.Add(lt6);
            }

        // ---- kiểu nét: 3 kiểu cơ bản luôn có + kiểu nét layer/thực thể dùng thêm ----
        var lts = new List<string> { "CONTINUOUS", "DASHED", "CENTER" };
        foreach (var l in layers.Select(q => q.LType).Concat(usedLt.Select(CadStandard.NormalizeLinetype)))
            if (!lts.Contains(l, StringComparer.OrdinalIgnoreCase) && LtypeDefs.Any(d => d.Name == l)) lts.Add(l);

        G(sb, 0, "SECTION"); G(sb, 2, "TABLES");
        G(sb, 0, "TABLE"); G(sb, 2, "LTYPE"); G(sb, 70, lts.Count);
        foreach (var name in lts)
        {
            var d = LtypeDefs.First(x => x.Name == name);
            G(sb, 0, "LTYPE"); G(sb, 2, d.Name); G(sb, 70, 0); G(sb, 3, d.Desc);
            G(sb, 72, 65); G(sb, 73, d.Elems.Length); G(sb, 40, d.Total);
            foreach (double el in d.Elems) G(sb, 49, el);
        }
        G(sb, 0, "ENDTAB");

        G(sb, 0, "TABLE"); G(sb, 2, "LAYER"); G(sb, 70, layers.Count);
        foreach (var (name, color, lt) in layers)
        {
            G(sb, 0, "LAYER"); G(sb, 2, name); G(sb, 70, 0); G(sb, 62, color); G(sb, 6, lt);
        }
        G(sb, 0, "ENDTAB");

        G(sb, 0, "TABLE"); G(sb, 2, "STYLE"); G(sb, 70, 1);
        G(sb, 0, "STYLE"); G(sb, 2, "STANDARD"); G(sb, 70, 0); G(sb, 40, 0.0); G(sb, 41, WidthFactor); G(sb, 50, 0.0);
        G(sb, 71, 0); G(sb, 42, Th); G(sb, 3, CadStandard.FontFile(std.FontName)); G(sb, 4, "");
        G(sb, 0, "ENDTAB");
        G(sb, 0, "ENDSEC");
        _definedLtypes = new HashSet<string>(lts, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Kiểu nét đã khai báo trong bảng LTYPE của lần ghi hiện tại (null = ghi kiểu cũ, không kiểm tra).</summary>
    private HashSet<string> _definedLtypes;
}

/// <summary>Liệt kê các layer (tên nội bộ) sẽ được ghi trong một lần xuất – dùng cho hộp "Xem/đổi tên layer trước khi xuất".</summary>
public static class ExportLayerProbe
{
    /// <summary>Bộ bản vẽ KC-01…KC-06 (DrawingSet.ExportAll) dùng các layer chuẩn của chương trình.</summary>
    public static List<string> ForDrawingSet() => Model.CadStandard.InternalLayerKeys.ToList();

    /// <summary>Các tờ dàn trang (SheetBuilder.ExportDxf / ExportAllDxf): ghép thử từng tờ rồi lấy layer thực thể dùng
    /// (gồm cả layer tự đặt của đối tượng chú thích).</summary>
    public static List<string> ForSheets(Model.Project p, Analysis.AnalysisResults r, Design.DesignSummary d, IEnumerable<Model.Sheet> sheets)
    {
        var res = new List<string>();
        foreach (var s in sheets ?? Enumerable.Empty<Model.Sheet>())
        {
            try
            {
                foreach (var l in SheetBuilder.Compose(p, r, d, s).UsedLayers())
                    if (!res.Contains(l)) res.Add(l);
            }
            catch (Exception) { /* tờ lỗi: bỏ qua khi liệt kê */ }
        }
        foreach (var k in Model.CadStandard.InternalLayerKeys) if (!res.Contains(k)) res.Add(k);
        return res;
    }

    /// <summary>Từ các bộ ghi đã dựng sẵn.</summary>
    public static List<string> ForWriters(IEnumerable<DxfWriter> writers)
    {
        var res = new List<string>();
        foreach (var w in writers ?? Enumerable.Empty<DxfWriter>())
            foreach (var l in w.UsedLayers()) if (!res.Contains(l)) res.Add(l);
        return res;
    }
}

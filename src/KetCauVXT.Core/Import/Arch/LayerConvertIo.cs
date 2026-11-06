using System.Globalization;
using System.Text;
using KetCauVXT.Model;

namespace KetCauVXT.Import.Arch;

/// <summary>Chuẩn hoá layer trong chương trình (không cần AutoCAD): quét file DXF/DWG → thống kê từng layer → đề xuất layer chuẩn VXT-…,
/// bộ ánh xạ lưu trên máy (%AppData%\VietConst\layer-profiles\*.json – chỉ trên máy, không gửi đi đâu), xuất DXF đã chuẩn hoá.</summary>
public static class LayerConvertIo
{
    /// <summary>Thư mục bộ ánh xạ layer (cục bộ).</summary>
    public static string ProfileDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VietConst", "layer-profiles");

    /// <summary>Đọc file DXF/DWG (DWG qua bộ đọc / bộ chuyển có sẵn). null + lỗi nếu không đọc được.</summary>
    public static DxfReader ReadFile(string path, double scaleOverride, out string error)
    {
        error = "";
        try
        {
            if (DwgConverter.IsDwg(path)) return DwgConverter.ReadAny(path, scaleOverride, out error);
            return DxfReader.Read(path, scaleOverride);
        }
        catch (Exception ex) { error = ex.Message; return null; }
    }

    /// <summary>Thống kê từng layer của bản vẽ đã đọc (kể cả đối tượng trong block đã phân rã, hatch).</summary>
    public static Dictionary<string, LayerStats> StatsFrom(DxfReader dxf)
    {
        var res = new Dictionary<string, LayerStats>(StringComparer.OrdinalIgnoreCase);
        if (dxf == null) return res;
        LayerStats Get(string layer)
        {
            layer = string.IsNullOrEmpty(layer) ? "0" : layer;
            if (!res.TryGetValue(layer, out var s))
            {
                s = new LayerStats { Name = layer };
                if (dxf.LayerColors.TryGetValue(layer, out int c)) s.Color = c;
                if (dxf.LayerLineTypes.TryGetValue(layer, out string lt)) s.Linetype = lt ?? "";
                res[layer] = s;
            }
            return s;
        }
        foreach (var kv in dxf.LayerColors) Get(kv.Key);
        foreach (var e in dxf.Entities)
        {
            var s = Get(e.Layer);
            string blk = e.Block ?? "";
            bool dim = blk.StartsWith("*D", StringComparison.OrdinalIgnoreCase) || string.Equals(e.SourceType, "DIMENSION", StringComparison.OrdinalIgnoreCase);
            if (dim) { s.AddDimension(); continue; }
            if (blk.Length > 0 && !blk.StartsWith("*", StringComparison.Ordinal)) s.AddInsert(blk);
            switch (e.Type)
            {
                case DxfEntityType.Line:
                    if (e.Points.Count >= 2) s.AddLine(e.Points[0].X, e.Points[0].Y, e.Points[1].X, e.Points[1].Y, e.LineType);
                    break;
                case DxfEntityType.Polyline:
                    if (string.Equals(e.SourceType, "ARC", StringComparison.OrdinalIgnoreCase) && e.Arcs != null && e.Arcs.Count > 0) { s.AddArc(e.Arcs[0].R); break; }
                    s.AddPolyline(e.Points.Select(p => new[] { p.X, p.Y }).ToList(), e.Closed, e.LineType);
                    break;
                case DxfEntityType.Circle:
                    if (e.Points.Count > 0) s.AddCircle(e.Points[0].X, e.Points[0].Y, e.Radius);
                    break;
                case DxfEntityType.Text:
                    s.AddText(e.Text);
                    break;
            }
        }
        foreach (var e in dxf.FillEntities) Get(e.Layer).AddHatch();
        return res;
    }

    /// <summary>Nhóm theo quy tắc layer của người dùng trong chuẩn CAD đang dùng (bỏ qua bước nhận layer chuẩn VXT-…).</summary>
    public static Func<string, string> UserRules(CadStandard std)
    {
        if (std == null) return null;
        return layer => CadStandard.Classify(layer, std.InputRules, std.InputLayerMap, out _);
    }

    /// <summary>Đề xuất layer chuẩn cho mọi layer có đối tượng (sắp: nhiều đối tượng trước).</summary>
    public static List<LayerSuggestion> Suggest(Dictionary<string, LayerStats> stats, LayerProfile profile = null, CadStandard std = null)
    {
        var f = UserRules(std);
        return stats.Values.Where(s => s.Total > 0).OrderByDescending(s => s.Total).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(s => LayerConvert.Suggest(s, profile, f)).ToList();
    }

    /// <summary>Đổi đề xuất → gán layer → nhóm (đưa vào ArchImportOptions.LayerMap để chương trình đọc đúng mà không sửa file).</summary>
    public static List<LayerMapEntry> ToLayerMap(IEnumerable<LayerSuggestion> sugg) =>
        (sugg ?? Enumerable.Empty<LayerSuggestion>()).Where(s => s != null && s.Category.Length > 0).Select(s => new LayerMapEntry(s.Layer, s.Category)).ToList();

    /// <summary>Bộ ánh xạ từ đề xuất (dùng lại cho bản vẽ khác cùng nguồn).</summary>
    public static LayerProfile ToProfile(string name, IEnumerable<LayerSuggestion> sugg)
    {
        var p = new LayerProfile { Name = string.IsNullOrWhiteSpace(name) ? "Bộ ánh xạ" : name.Trim() };
        foreach (var s in sugg ?? Enumerable.Empty<LayerSuggestion>()) if (s != null && !string.IsNullOrEmpty(s.Layer)) p.Set(s.Layer, s.Key);
        return p;
    }

    public static void SaveProfile(LayerProfile p, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        File.WriteAllText(path, p.ToJson(), new UTF8Encoding(false));
    }

    public static LayerProfile LoadProfile(string path)
    {
        try { return LayerProfile.FromJson(File.ReadAllText(path)); }
        catch (Exception) { return null; }
    }

    // ------------------------------------------------------------------
    // Xuất DXF đã chuẩn hoá (R12 ASCII, đơn vị mm): nét, đa tuyến (giữ bề rộng), đường tròn, chữ – đủ để chương trình đọc mặt bằng.
    // ------------------------------------------------------------------
    /// <summary>Ghi DXF R12 với layer đã đổi sang layer chuẩn. keyOfLayer: layer gốc → khoá chuẩn ("" = giữ tên gốc).
    /// doorBlocks: đối tượng thuộc block cửa (CUA*, DOOR*, D1…) → layer cửa. Trả về số đối tượng theo layer đích.</summary>
    public static Dictionary<string, int> ExportStandardDxf(DxfReader dxf, Func<string, string> keyOfLayer, string outPath, bool doorBlocks = true)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var layers = new Dictionary<string, (int Color, string Lt)>(StringComparer.OrdinalIgnoreCase) { ["0"] = (7, "CONTINUOUS") };
        var ents = new StringBuilder(1 << 16);
        var inv = CultureInfo.InvariantCulture;
        void G(int code, string v) { ents.Append(code.ToString(inv)).Append('\n').Append(v).Append('\n'); }
        void D(int code, double v) => G(code, (v * 1000.0).ToString("0.####", inv));   // m → mm

        string Target(DxfEntity e)
        {
            string key = keyOfLayer?.Invoke(e.Layer ?? "0") ?? "";
            bool dim = (e.Block ?? "").StartsWith("*D", StringComparison.OrdinalIgnoreCase);
            string t = LayerConvert.TargetFor(key, e.Block, dim && key.Length > 0, doorBlocks);
            if (t.Length > 0)
            {
                var std = StdLayers.Of(t);
                if (std != null && !layers.ContainsKey(t)) layers[t] = (std.Color, std.Linetype);
                return t;
            }
            string n = SafeName(e.Layer);
            if (!layers.ContainsKey(n))
                layers[n] = (dxf.LayerColors.TryGetValue(e.Layer ?? "0", out int c) ? c : 7, "CONTINUOUS");
            return n;
        }
        void Count(string l) { counts.TryGetValue(l, out int k); counts[l] = k + 1; }

        foreach (var e in dxf.Entities)
        {
            string L = Target(e);
            switch (e.Type)
            {
                case DxfEntityType.Line:
                    if (e.Points.Count < 2) continue;
                    G(0, "LINE"); G(8, L); D(10, e.Points[0].X); D(20, e.Points[0].Y); G(30, "0"); D(11, e.Points[1].X); D(21, e.Points[1].Y); G(31, "0");
                    break;
                case DxfEntityType.Polyline:
                    if (e.Points.Count < 2) continue;
                    G(0, "POLYLINE"); G(8, L); G(66, "1"); G(10, "0"); G(20, "0"); G(30, "0"); G(70, e.Closed ? "1" : "0");
                    if (e.Width > 1e-9) { D(40, e.Width); D(41, e.Width); }
                    foreach (var p in e.Points) { G(0, "VERTEX"); G(8, L); D(10, p.X); D(20, p.Y); G(30, "0"); }
                    G(0, "SEQEND"); G(8, L);
                    break;
                case DxfEntityType.Circle:
                    if (e.Points.Count < 1 || e.Radius <= 0) continue;
                    G(0, "CIRCLE"); G(8, L); D(10, e.Points[0].X); D(20, e.Points[0].Y); G(30, "0"); D(40, e.Radius);
                    break;
                case DxfEntityType.Text:
                    if (e.Points.Count < 1 || string.IsNullOrWhiteSpace(e.Text)) continue;
                    G(0, "TEXT"); G(8, L); D(10, e.Points[0].X); D(20, e.Points[0].Y); G(30, "0");
                    D(40, e.TextHeight > 1e-6 ? e.TextHeight : 0.25); G(1, EncodeText(e.Text));
                    break;
                default: continue;
            }
            Count(L);
        }

        var sb = new StringBuilder(ents.Length + 4096);
        void H(int code, string v) { sb.Append(code.ToString(inv)).Append('\n').Append(v).Append('\n'); }
        H(0, "SECTION"); H(2, "HEADER"); H(9, "$ACADVER"); H(1, "AC1009"); H(0, "ENDSEC");
        H(0, "SECTION"); H(2, "TABLES");
        H(0, "TABLE"); H(2, "LTYPE"); H(70, "3");
        H(0, "LTYPE"); H(2, "CONTINUOUS"); H(70, "0"); H(3, "Solid line"); H(72, "65"); H(73, "0"); H(40, "0.0");
        H(0, "LTYPE"); H(2, "DASHED"); H(70, "0"); H(3, "__ __ __ __"); H(72, "65"); H(73, "2"); H(40, "19.05"); H(49, "12.7"); H(49, "-6.35");
        H(0, "LTYPE"); H(2, "CENTER"); H(70, "0"); H(3, "____ _ ____ _"); H(72, "65"); H(73, "4"); H(40, "50.8"); H(49, "31.75"); H(49, "-6.35"); H(49, "6.35"); H(49, "-6.35");
        H(0, "ENDTAB");
        H(0, "TABLE"); H(2, "LAYER"); H(70, layers.Count.ToString(inv));
        foreach (var kv in layers)
        {
            string lt = kv.Value.Lt is "DASHED" or "CENTER" ? kv.Value.Lt : "CONTINUOUS";
            H(0, "LAYER"); H(2, kv.Key); H(70, "0"); H(62, Math.Clamp(kv.Value.Color, 1, 255).ToString(inv)); H(6, lt);
        }
        H(0, "ENDTAB");
        H(0, "TABLE"); H(2, "STYLE"); H(70, "1");
        H(0, "STYLE"); H(2, "STANDARD"); H(70, "0"); H(40, "0.0"); H(41, "1.0"); H(50, "0.0"); H(71, "0"); H(42, "2.5"); H(3, "arial.ttf"); H(4, "");
        H(0, "ENDTAB");
        H(0, "ENDSEC");
        H(0, "SECTION"); H(2, "ENTITIES");
        sb.Append(ents);
        H(0, "ENDSEC"); H(0, "EOF");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)) ?? ".");
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
        return counts;
    }

    /// <summary>Tên layer hợp lệ trong DXF (bỏ ký tự cấm, '|' của xref → '_').</summary>
    public static string SafeName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "0";
        var sb = new StringBuilder();
        foreach (char c in s.Trim()) sb.Append("<>/\\\":;?*|=`,".IndexOf(c) >= 0 || c < 32 ? '_' : c);
        return sb.ToString();
    }

    private static string EncodeText(string t)
    {
        var sb = new StringBuilder();
        foreach (char c in (t ?? "").Replace("\r", " ").Replace("\n", " "))
        {
            if (c > 127) sb.Append("\\U+").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            else sb.Append(c);
        }
        return sb.ToString();
    }
}

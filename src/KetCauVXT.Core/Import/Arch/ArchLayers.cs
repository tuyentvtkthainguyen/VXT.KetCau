using System.Globalization;
using KetCauVXT.Model;

namespace KetCauVXT.Import.Arch;

/// <summary>Phân loại layer bản vẽ kiến trúc → nhóm cấu kiện (CadCategory) theo ArchImportOptions:
/// gán trực tiếp (LayerMap) → quy tắc (LayerRules, mặc định CadStandard.DefaultInputRules) + từ khoá kiểu cũ (WallLayers…).
/// Kết quả từng layer được nhớ đệm.</summary>
public sealed class LayerClassifier
{
    private readonly List<LayerRule> _rules;
    private readonly List<LayerMapEntry> _map;
    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public LayerClassifier(IEnumerable<LayerRule> rules, IEnumerable<LayerMapEntry> map)
    {
        _rules = (rules ?? Enumerable.Empty<LayerRule>()).Where(r => r != null).ToList();
        _map = (map ?? Enumerable.Empty<LayerMapEntry>()).Where(m => m != null).ToList();
    }

    public static LayerClassifier For(ArchImportOptions o)
    {
        o ??= new ArchImportOptions();
        var rules = new List<LayerRule>(o.LayerRules ?? CadStandard.DefaultInputRules());
        rules.AddRange(CadStandard.RulesFromKeywords(o.WallLayers, o.GridLayers, o.ColumnLayers, o.DoorLayers, o.StairLayers));
        return new LayerClassifier(rules, o.LayerMap);
    }

    /// <summary>Nhóm của layer ("" = không nhận).</summary>
    public string Of(string layer)
    {
        layer ??= "0";
        if (_cache.TryGetValue(layer, out var c)) return c;
        c = CadStandard.Classify(layer, _rules, _map, out _);
        _cache[layer] = c;
        return c;
    }

    public string Reason(string layer)
    {
        CadStandard.Classify(layer ?? "0", _rules, _map, out var r);
        return r;
    }

    public bool Is(string layer, string category) => Of(layer) == category;

    /// <summary>Các layer đã gặp và nhóm của chúng.</summary>
    public IReadOnlyDictionary<string, string> Seen => _cache;
}

/// <summary>Thông tin một layer trong file DXF (bảng "Layer trong file").</summary>
public class CadLayerInfo
{
    public string Name { get; set; } = "";
    /// <summary>Màu ACI của layer (1…255)</summary>
    public int Color { get; set; } = 7;
    public string Linetype { get; set; } = "";
    public bool Frozen { get; set; }
    public bool Off { get; set; }
    /// <summary>Số thực thể theo loại gốc (LINE, LWPOLYLINE, ARC, TEXT, MTEXT, HATCH…), kể cả trong block đã phân rã</summary>
    public Dictionary<string, int> Counts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int Total => Counts.Values.Sum();
    /// <summary>Nhóm tự nhận theo quy tắc / gán trực tiếp</summary>
    public string Category { get; set; } = "";
    public string Reason { get; set; } = "";
    /// <summary>Các file chứa layer này</summary>
    public List<string> Files { get; set; } = new();

    public string CountsText => Counts.Count == 0 ? (Frozen ? "(đóng băng – không đọc)" : "(trống)")
        : string.Join(", ", Counts.OrderByDescending(kv => kv.Value).Select(kv => $"{ShortType(kv.Key)} {kv.Value}"));

    private static string ShortType(string t) => t switch
    {
        "LWPOLYLINE" => "PLINE",
        "POLYLINE" => "PLINE2D",
        "" => "khác",
        _ => t
    };
}

/// <summary>Đoạn thẳng xem trước (toạ độ m) gắn với chỉ số layer trong CadLayerScanResult.Layers.</summary>
public struct LayerPreviewSeg
{
    public float X1, Y1, X2, Y2;
    public int Layer;
}

public class CadLayerScanResult
{
    public List<CadLayerInfo> Layers { get; } = new();
    public List<LayerPreviewSeg> Segments { get; } = new();
    public double X0 = double.MaxValue, Y0 = double.MaxValue, X1 = double.MinValue, Y1 = double.MinValue;
    public List<string> Warnings { get; } = new();
    public List<string> Info { get; } = new();
    public bool Truncated;
}

/// <summary>Quét layer của các file DXF/DWG (đếm thực thể theo loại, màu, trạng thái, nét xem trước) để người dùng gán nhóm cấu kiện.</summary>
public static class CadLayerScan
{
    public const int MaxPreviewSegments = 120_000;

    /// <summary>Quét các file .dxf (.dwg qua ODA nếu có).</summary>
    public static CadLayerScanResult Scan(IEnumerable<string> paths, ArchImportOptions o)
    {
        var sc = new Builder(o);
        foreach (var f in (paths ?? Enumerable.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            string fn = Path.GetFileName(f);
            DxfReader dxf;
            if (DwgConverter.IsDwg(f))
            {
                dxf = DwgConverter.ReadAny(f, (o ?? new ArchImportOptions()).ScaleOverride, out string err);
                if (dxf == null) { sc.Result.Warnings.Add(fn + ": " + err); continue; }
            }
            else dxf = DxfReader.Read(f, (o ?? new ArchImportOptions()).ScaleOverride);
            sc.Result.Warnings.AddRange(dxf.Warnings.Select(w => fn + ": " + w));
            sc.Add(fn, dxf.Entities.Concat(dxf.FillEntities), dxf.LayerColors, dxf.LayerLineTypes, dxf.FrozenLayers, dxf.OffLayers);
            sc.Result.Info.Add($"{fn}: {dxf.Entities.Count + dxf.FillEntities.Count} thực thể, {dxf.LayerColors.Count} layer, $INSUNITS = {dxf.InsUnits} → ×{dxf.Scale.ToString(CultureInfo.InvariantCulture)}");
        }
        return sc.Finish();
    }

    /// <summary>Quét từ danh sách thực thể đã đọc (vd. bộ đọc DWG riêng tạo ra cùng List&lt;DxfEntity&gt;).
    /// layerColors/frozen/off: bảng layer nếu có (null = chỉ lấy layer có thực thể, màu 7).</summary>
    public static CadLayerScanResult FromEntities(IEnumerable<DxfEntity> ents, ArchImportOptions o, string fileName = "",
        IDictionary<string, int> layerColors = null, IDictionary<string, string> layerLinetypes = null,
        ICollection<string> frozen = null, ICollection<string> off = null)
    {
        var sc = new Builder(o);
        sc.Add(fileName ?? "", ents, layerColors, layerLinetypes, frozen, off);
        return sc.Finish();
    }

    /// <summary>Gom nhiều nguồn (file) vào một bảng layer.</summary>
    public sealed class Builder
    {
        private readonly LayerClassifier _lc;
        private readonly Dictionary<string, CadLayerInfo> _byName = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _index = new(StringComparer.OrdinalIgnoreCase);
        public CadLayerScanResult Result { get; } = new();

        public Builder(ArchImportOptions o) { _lc = LayerClassifier.For(o); }

        private CadLayerInfo Info(string name)
        {
            if (!_byName.TryGetValue(name, out var li))
            {
                li = new CadLayerInfo { Name = name };
                _byName[name] = li; _index[name] = Result.Layers.Count; Result.Layers.Add(li);
            }
            return li;
        }

        public void Add(string fn, IEnumerable<DxfEntity> ents, IDictionary<string, int> colors = null, IDictionary<string, string> linetypes = null,
                        ICollection<string> frozen = null, ICollection<string> off = null)
        {
            fn ??= "";
            if (colors != null)
                foreach (var kv in colors)
                {
                    var li = Info(kv.Key);
                    li.Color = kv.Value;
                    li.Linetype = linetypes != null && linetypes.TryGetValue(kv.Key, out var lt) ? lt ?? "" : "";
                    li.Frozen = frozen?.Contains(kv.Key) == true;
                    li.Off = off?.Contains(kv.Key) == true;
                    if (fn.Length > 0 && !li.Files.Contains(fn)) li.Files.Add(fn);
                }
            foreach (var e in ents ?? Enumerable.Empty<DxfEntity>())
            {
                if (e == null) continue;
                var li = Info(e.Layer ?? "0");
                if (fn.Length > 0 && !li.Files.Contains(fn)) li.Files.Add(fn);
                string t = string.IsNullOrEmpty(e.SourceType) ? e.Type.ToString().ToUpperInvariant() : e.SourceType;
                li.Counts[t] = li.Counts.TryGetValue(t, out int n) ? n + 1 : 1;
                if (Result.Segments.Count >= MaxPreviewSegments) { Result.Truncated = true; continue; }
                AddPreview(Result, e, _index[li.Name]);
            }
        }

        public CadLayerScanResult Finish()
        {
            var res = Result;
            foreach (var li in res.Layers)
            {
                li.Category = _lc.Of(li.Name);
                li.Reason = _lc.Reason(li.Name);
            }
            // sắp theo tên; chỉ số layer của nét xem trước đổi theo thứ tự mới
            var before = res.Layers.ToList();
            res.Layers.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            var newIndex = new Dictionary<CadLayerInfo, int>();
            for (int i = 0; i < res.Layers.Count; i++) newIndex[res.Layers[i]] = i;
            for (int i = 0; i < res.Segments.Count; i++)
            {
                var s = res.Segments[i];
                if (s.Layer >= 0 && s.Layer < before.Count) s.Layer = newIndex[before[s.Layer]];
                res.Segments[i] = s;
            }
            if (res.Truncated) res.Warnings.Add($"Bản vẽ lớn – hình xem trước chỉ vẽ {MaxPreviewSegments:N0} nét đầu tiên.");
            return res;
        }
    }

    private static void AddPreview(CadLayerScanResult r, DxfEntity e, int layer)
    {
        void Seg(double x1, double y1, double x2, double y2)
        {
            r.Segments.Add(new LayerPreviewSeg { X1 = (float)x1, Y1 = (float)y1, X2 = (float)x2, Y2 = (float)y2, Layer = layer });
            r.X0 = Math.Min(r.X0, Math.Min(x1, x2)); r.X1 = Math.Max(r.X1, Math.Max(x1, x2));
            r.Y0 = Math.Min(r.Y0, Math.Min(y1, y2)); r.Y1 = Math.Max(r.Y1, Math.Max(y1, y2));
        }
        var p = e.Points;
        if (p.Count == 0) return;
        switch (e.Type)
        {
            case DxfEntityType.Circle:
                for (int k = 0; k < 12; k++)
                {
                    double a0 = k * Math.PI / 6, a1 = (k + 1) * Math.PI / 6;
                    Seg(p[0].X + e.Radius * Math.Cos(a0), p[0].Y + e.Radius * Math.Sin(a0), p[0].X + e.Radius * Math.Cos(a1), p[0].Y + e.Radius * Math.Sin(a1));
                }
                break;
            case DxfEntityType.Text:
            {
                // chữ: gạch ngang dài ≈ chiều dài chữ
                double h = Math.Max(e.TextHeight, 1e-3);
                Seg(p[0].X, p[0].Y, p[0].X + Math.Min(40, (e.Text?.Length ?? 1)) * 0.6 * h, p[0].Y);
                break;
            }
            default:
                for (int i = 0; i + 1 < p.Count; i++) Seg(p[i].X, p[i].Y, p[i + 1].X, p[i + 1].Y);
                if (e.Closed && p.Count > 2) Seg(p[^1].X, p[^1].Y, p[0].X, p[0].Y);
                break;
        }
    }
}

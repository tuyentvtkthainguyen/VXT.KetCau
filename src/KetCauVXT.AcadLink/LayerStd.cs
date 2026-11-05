using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KetCauVXT.AcadLink
{
    /// <summary>Một layer bản vẽ kết cấu cần tạo.</summary>
    internal sealed class OutLayer
    {
        public string Key = "", Name = "", Linetype = "CONTINUOUS", Description = "";
        public int Color = 7;
        /// <summary>mm (0 = mặc định)</summary>
        public double Lineweight;
        public bool Plot = true;
    }

    // ---- kiểu kích thước VXT_1-25… (CadStandard.json → "Drawing") ----
    /// <summary>Kiểu kích thước theo tỷ lệ (đã quy đổi: ô trống lấy theo thông số chung). Thông số mm trên giấy, Scale = DIMSCALE.</summary>
    internal sealed class DimStyleA
    {
        public string Name = "";
        public double Scale = 100, TextHeight = 2.5, TickSize = 2.5, ExtOffset = 0.625, ExtBeyond = 1.25, LineExtend = 1.0, TextGap = 0.75, Baseline = 7, LinearFactor = 1;
        public bool Arrow;
        public int Decimals;
    }
    // ---- hết kiểu kích thước ----

    internal sealed class LayerRuleDef
    {
        public string Pattern = "", Mode = "Contains", Category = "";
        public int Priority;
        public bool Enabled = true;
    }

    /// <summary>Chuẩn layer CAD: đọc %AppData%\KetCauVXT\CadStandard.json (do chương trình Việt Const ghi) nếu có,
    /// không có thì dùng mặc định trong add-in. Chấp nhận cả hai cách đặt tên trường:
    /// InputRules/LayerRules (Pattern, Match|Mode, Category, Priority, Enabled), InputLayerMap (danh sách {Layer, Category}) / LayerMap (từ điển),
    /// OutputLayers ({Key, Name, Color, Linetype, Lineweight, Plot, Description}), FontName.</summary>
    internal sealed class LayerStd
    {
        public List<OutLayer> OutputLayers = new List<OutLayer>();
        public List<LayerRuleDef> Rules = new List<LayerRuleDef>();
        public Dictionary<string, string> LayerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string FontName = "Arial";
        /// <summary>Kiểu kích thước (mặc định VXT_1-15, 1-25, 1-30, 1-50, 1-100, 1-200, 1-250)</summary>
        public List<DimStyleA> DimStyles = DefaultDimStyles();
        /// <summary>Đường dẫn file đã đọc ("" = mặc định của add-in)</summary>
        public string SourceFile = "";

        public static string AppDataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KetCauVXT");
        public static string FilePath => Path.Combine(AppDataDir, "CadStandard.json");

        /// <summary>Layer kết cấu theo nhóm cấu kiện (dùng khi "chuyển đổi layer").</summary>
        public static List<OutLayer> CategoryDefaults() => new List<OutLayer>
        {
            new OutLayer { Key = Cat.Tuong, Name = "KC-TUONG", Color = 8, Linetype = "CONTINUOUS", Lineweight = 0.25, Description = "Tường (từ bản vẽ kiến trúc)" },
            new OutLayer { Key = Cat.Cot, Name = "KC-COT", Color = 1, Linetype = "CONTINUOUS", Lineweight = 0.35, Description = "Cột" },
            new OutLayer { Key = Cat.Dam, Name = "KC-DAM", Color = 3, Linetype = "HIDDEN", Lineweight = 0.30, Description = "Dầm" },
            new OutLayer { Key = Cat.San, Name = "KC-SAN", Color = 5, Linetype = "CONTINUOUS", Lineweight = 0.25, Description = "Sàn, lỗ sàn" },
            new OutLayer { Key = Cat.Truc, Name = "KC-TRUC", Color = 1, Linetype = "CENTER", Lineweight = 0.13, Description = "Lưới trục định vị" },
            new OutLayer { Key = Cat.Cua, Name = "KC-CUA", Color = 30, Linetype = "CONTINUOUS", Lineweight = 0.18, Description = "Cửa" },
            new OutLayer { Key = Cat.Thang, Name = "KC-THANG", Color = 6, Linetype = "CONTINUOUS", Lineweight = 0.25, Description = "Cầu thang" },
            new OutLayer { Key = Cat.TenPhong, Name = "KC-TENPHONG", Color = 2, Linetype = "CONTINUOUS", Lineweight = 0.18, Description = "Tên phòng" },
        };

        /// <summary>Quy tắc mặc định (giống từ khoá mặc định của chương trình).</summary>
        private static List<LayerRuleDef> DefaultRules()
        {
            var r = new List<LayerRuleDef>();
            void Add(string keys, string cat, int pri)
            {
                foreach (var k in keys.Split(','))
                    r.Add(new LayerRuleDef { Pattern = k, Mode = "Contains", Category = cat, Priority = pri });
            }
            Add("TUONG,WALL,A-WALL,XAY", Cat.Tuong, 50);
            Add("TRUC,GRID,AXIS,A-GRID", Cat.Truc, 40);
            Add("COT,COLUMN,A-COLS,S-COLS", Cat.Cot, 30);
            Add("CUA,DOOR,WINDOW,A-DOOR,A-GLAZ", Cat.Cua, 20);
            Add("THANG,STAIR,A-FLOR-STRS", Cat.Thang, 10);
            r.Add(new LayerRuleDef { Pattern = @"(^|[^A-Z0-9])(DAM|BEAM|S-BEAM)([^A-Z0-9]|$)", Mode = "Regex", Category = Cat.Dam, Priority = 60 });
            r.Add(new LayerRuleDef { Pattern = @"(^|[^A-Z0-9])(SAN|SLAB|S-SLAB)([^A-Z0-9]|$)", Mode = "Regex", Category = Cat.San, Priority = 60 });
            r.Add(new LayerRuleDef { Pattern = "TENPHONG", Mode = "Contains", Category = Cat.TenPhong, Priority = 70 });
            r.Add(new LayerRuleDef { Pattern = "TEN PHONG", Mode = "Contains", Category = Cat.TenPhong, Priority = 70 });
            r.Add(new LayerRuleDef { Pattern = "DEFPOINTS", Mode = "Exact", Category = Cat.BoQua, Priority = 100 });
            r.Add(new LayerRuleDef { Pattern = "NOITHAT", Mode = "Contains", Category = Cat.BoQua, Priority = 80 });
            r.Add(new LayerRuleDef { Pattern = "A-FURN*", Mode = "Wildcard", Category = Cat.BoQua, Priority = 80 });
            return r;
        }

        public static LayerStd Load(List<string> messages = null)
        {
            var s = new LayerStd { Rules = DefaultRules() };
            string f = FilePath;
            try
            {
                if (!File.Exists(f)) return s;
                var root = MiniJson.Parse(File.ReadAllText(f, Encoding.UTF8));
                s.SourceFile = f;
                s.FontName = MiniJson.Str(root, "FontName", "Arial");
                var drw = MiniJson.Get(root, "Drawing");
                if (drw != null) s.DimStyles = ReadDimStyles(drw);
                foreach (var o in MiniJson.Arr(root, "OutputLayers"))
                {
                    string name = MiniJson.Str(o, "Name");
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    s.OutputLayers.Add(new OutLayer
                    {
                        Key = MiniJson.Str(o, "Key"), Name = name.Trim(),
                        Color = (int)MiniJson.Num(o, "Color", 7), Linetype = MiniJson.Str(o, "Linetype", "CONTINUOUS"),
                        Lineweight = MiniJson.Num(o, "Lineweight", 0), Plot = MiniJson.Bool(o, "Plot", true),
                        Description = MiniJson.Str(o, "Description"),
                    });
                }
                var rules = MiniJson.Get(root, "InputRules") as List<object> ?? MiniJson.Get(root, "LayerRules") as List<object>;
                if (rules != null)
                {
                    s.Rules = new List<LayerRuleDef>();
                    foreach (var o in rules)
                    {
                        string cat = Cat.Normalize(MiniJson.Str(o, "Category"));
                        if (cat == null) continue;
                        object mode = MiniJson.Get(o, "Match") ?? MiniJson.Get(o, "Mode");
                        string m = mode is double ? new[] { "Exact", "Contains", "Wildcard", "Regex" }[Math.Max(0, Math.Min(3, (int)(double)mode))] : (mode as string ?? "Contains");
                        s.Rules.Add(new LayerRuleDef
                        {
                            Pattern = MiniJson.Str(o, "Pattern"), Mode = m, Category = cat,
                            Priority = (int)MiniJson.Num(o, "Priority", 0), Enabled = MiniJson.Bool(o, "Enabled", true),
                        });
                    }
                }
                foreach (var o in MiniJson.Arr(root, "InputLayerMap"))
                {
                    string c = Cat.Normalize(MiniJson.Str(o, "Category"));
                    string l = MiniJson.Str(o, "Layer");
                    if (c != null && l.Length > 0) s.LayerMap[l] = c;
                }
                var dict = MiniJson.Get(root, "LayerMap") as Dictionary<string, object>;
                if (dict != null)
                    foreach (var kv in dict)
                    {
                        string c = Cat.Normalize(kv.Value as string);
                        if (c != null) s.LayerMap[kv.Key] = c;
                    }
                if (messages != null) messages.Add($"Đọc chuẩn layer: {f} ({s.OutputLayers.Count} layer xuất, {s.Rules.Count} quy tắc, {s.LayerMap.Count} layer gán sẵn).");
            }
            catch (System.Exception ex)
            {
                messages?.Add("Không đọc được " + f + ": " + ex.Message + " – dùng chuẩn mặc định.");
                s = new LayerStd { Rules = DefaultRules() };
            }
            return s;
        }

        // ---- kiểu kích thước ----
        private static readonly double[] DimScales = { 15, 25, 30, 50, 100, 200, 250 };

        private static List<DimStyleA> DefaultDimStyles() => ReadDimStyles(new Dictionary<string, object>());

        /// <summary>Đọc mục "Drawing" của CadStandard.json:
        /// DimStylePrefix ("VXT_"), DimTextHeight, DimTickSize, DimArrowType ("Tick" | "ClosedArrow"), DimExtOffset, DimExtBeyond, DimLineExtend,
        /// DimTextGap, DimBaselineSpacing, DimDecimals, DimLinearFactor (thông số chung, mm giấy) và
        /// DimStyles: [{ Scale, TextHeight, TickSize, Arrow, ExtOffset, ExtBeyond, LineExtend, TextGap, BaselineSpacing, Decimals, LinearFactor }]
        /// (giá trị null = theo thông số chung). Không có DimStyles → 7 tỷ lệ mặc định.</summary>
        private static List<DimStyleA> ReadDimStyles(object d)
        {
            string prefix = MiniJson.Str(d, "DimStylePrefix", "VXT_");
            var clean = new StringBuilder();
            foreach (char ch in prefix ?? "") if (ch < 128 && (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '$')) clean.Append(ch);
            prefix = clean.Length == 0 ? "VXT_" : clean.ToString();
            var b = new DimStyleA
            {
                TextHeight = MiniJson.Num(d, "DimTextHeight", 2.5), TickSize = MiniJson.Num(d, "DimTickSize", 2.5),
                ExtOffset = MiniJson.Num(d, "DimExtOffset", 0.625), ExtBeyond = MiniJson.Num(d, "DimExtBeyond", 1.25),
                LineExtend = MiniJson.Num(d, "DimLineExtend", 1.0), TextGap = MiniJson.Num(d, "DimTextGap", 0.75),
                Baseline = MiniJson.Num(d, "DimBaselineSpacing", 7), LinearFactor = MiniJson.Num(d, "DimLinearFactor", 1),
                Decimals = (int)MiniJson.Num(d, "DimDecimals", 0),
                Arrow = string.Equals(MiniJson.Str(d, "DimArrowType", "Tick"), "ClosedArrow", StringComparison.OrdinalIgnoreCase)
            };
            var defs = MiniJson.Get(d, "DimStyles") as List<object>;
            var items = defs != null ? defs : DimScales.Select(x => (object)new Dictionary<string, object> { ["Scale"] = x }).ToList();
            var res = new List<DimStyleA>();
            foreach (var o in items)
            {
                double sc = Math.Round(MiniJson.Num(o, "Scale", 0), 2);
                if (sc <= 0 || sc > 2000 || res.Any(q => Math.Abs(q.Scale - sc) < 0.01)) continue;
                object arr = MiniJson.Get(o, "Arrow");
                res.Add(new DimStyleA
                {
                    Name = prefix + "1-" + sc.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', '_'),
                    Scale = sc,
                    TextHeight = MiniJson.Num(o, "TextHeight", b.TextHeight), TickSize = MiniJson.Num(o, "TickSize", b.TickSize),
                    ExtOffset = MiniJson.Num(o, "ExtOffset", b.ExtOffset), ExtBeyond = MiniJson.Num(o, "ExtBeyond", b.ExtBeyond),
                    LineExtend = MiniJson.Num(o, "LineExtend", b.LineExtend), TextGap = MiniJson.Num(o, "TextGap", b.TextGap),
                    Baseline = MiniJson.Num(o, "BaselineSpacing", b.Baseline), LinearFactor = MiniJson.Num(o, "LinearFactor", b.LinearFactor),
                    Decimals = (int)MiniJson.Num(o, "Decimals", b.Decimals),
                    Arrow = arr is string ? string.Equals((string)arr, "ClosedArrow", StringComparison.OrdinalIgnoreCase) : b.Arrow
                });
            }
            return res.OrderBy(q => q.Scale).ToList();
        }
        // ---- hết kiểu kích thước ----

        /// <summary>Layer kết cấu của một nhóm: ưu tiên OutputLayers có Key trùng tên nhóm (vd. "Cot"), không có thì mặc định KC-….</summary>
        public OutLayer ForCategory(string cat)
        {
            var o = OutputLayers.FirstOrDefault(q => string.Equals(q.Key, cat, StringComparison.OrdinalIgnoreCase));
            return o ?? CategoryDefaults().FirstOrDefault(q => q.Key == cat);
        }

        /// <summary>Tên layer chuẩn hoá: bỏ dấu tiếng Việt, chữ hoa, gộp khoảng trắng.</summary>
        public static string NormName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var f = s.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(f.Length);
            foreach (char c in f)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant(), @"\s+", " ").Trim();
        }

        private static IEnumerable<string> Variants(string layer)
        {
            yield return layer;
            int bar = layer.LastIndexOf('|');
            if (bar >= 0 && bar + 1 < layer.Length) { layer = layer.Substring(bar + 1); yield return layer; }
            var m = Regex.Match(layer, @"^.+?\$\d+\$(.+)$");
            if (m.Success) yield return m.Groups[1].Value;
        }

        /// <summary>Nhóm theo tên layer: gán trực tiếp trước, rồi quy tắc ưu tiên cao nhất; null = không nhận.</summary>
        public string Classify(string layer)
        {
            if (string.IsNullOrEmpty(layer)) return null;
            foreach (var v in Variants(layer))
            {
                string c;
                if (LayerMap.TryGetValue(v, out c)) return c;
            }
            LayerRuleDef best = null;
            foreach (var r in Rules)
            {
                if (!r.Enabled || string.IsNullOrWhiteSpace(r.Pattern)) continue;
                if (best != null && r.Priority <= best.Priority) continue;
                if (Variants(layer).Any(v => IsMatch(r, v))) best = r;
            }
            return best?.Category;
        }

        private static bool IsMatch(LayerRuleDef r, string layer)
        {
            string L = NormName(layer);
            string mode = (r.Mode ?? "Contains").Trim();
            string P = mode.Equals("Regex", StringComparison.OrdinalIgnoreCase) ? r.Pattern.Trim() : NormName(r.Pattern);
            if (P.Length == 0) return false;
            try
            {
                if (mode.Equals("Exact", StringComparison.OrdinalIgnoreCase)) return L == P;
                if (mode.Equals("Wildcard", StringComparison.OrdinalIgnoreCase))
                    return Regex.IsMatch(L, "^" + Regex.Escape(P).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
                if (mode.Equals("Regex", StringComparison.OrdinalIgnoreCase))
                    return Regex.IsMatch(L, P, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
                return L.Contains(P);
            }
            catch (ArgumentException) { return false; }
            catch (RegexMatchTimeoutException) { return false; }
        }
    }
}

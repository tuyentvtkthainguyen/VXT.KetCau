// =====================================================================
// CHUẨN HOÁ LAYER BẢN VẼ CAD → LAYER CHUẨN VIỆT CONST (VXT-…)
// File này được DÙNG CHUNG: biên dịch trong KetCauVXT.Core (.NET 8) và được liên kết (Compile Include) vào
// add-in AutoCAD KetCauVXT.AcadLink (.NET Framework 4.8 cho AutoCAD 2021–2024 / .NET 8 cho 2025–2026).
// → chỉ dùng API có trong .NET Framework 4.8: không dùng chỉ số ^1 / range, ToHashSet, GetValueOrDefault,
//   string.Contains(string, StringComparison), System.Text.Json, record… Không phụ thuộc lớp khác của Core.
// Đơn vị hình học: m.
// =====================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KetCauVXT.Import.Arch
{
    /// <summary>Một layer chuẩn Việt Const (VXT-…).</summary>
    public sealed class StdLayer
    {
        /// <summary>Khoá ngắn: "TRUC", "COT", "TUONG-110"…</summary>
        public string Key { get; }
        /// <summary>Tên layer trong bản vẽ: "VXT-" + Key</summary>
        public string Name => StdLayers.Prefix + Key;
        /// <summary>Nhóm cấu kiện chương trình đọc (chuỗi giống CadCategory: Tuong, Cot, Dam, San, Truc, Cua, Thang, TenPhong, BoQua)</summary>
        public string Category { get; }
        /// <summary>Màu ACI</summary>
        public int Color { get; }
        public string Linetype { get; }
        public string Description { get; }

        public StdLayer(string key, string category, int color, string linetype, string description)
        {
            Key = key; Category = category; Color = color; Linetype = linetype; Description = description;
        }

        public override string ToString() => Name + " – " + Description;
    }

    /// <summary>Bộ layer chuẩn Việt Const dùng để đọc bản vẽ (mặt bằng kiến trúc / kết cấu).</summary>
    public static class StdLayers
    {
        public const string Prefix = "VXT-";
        public const string Tuong = "Tuong", Cot = "Cot", Dam = "Dam", San = "San", Truc = "Truc", Cua = "Cua", Thang = "Thang", TenPhong = "TenPhong", BoQua = "BoQua";

        public static readonly StdLayer[] All =
        {
            new StdLayer("TRUC", Truc, 1, "CENTER", "Đường trục định vị"),
            new StdLayer("TRUC-KH", Truc, 1, "CONTINUOUS", "Ký hiệu trục (vòng tròn, tên trục)"),
            new StdLayer("COT", Cot, 6, "CONTINUOUS", "Cột (đa tuyến kín b×h)"),
            new StdLayer("TUONG-110", Tuong, 2, "CONTINUOUS", "Tường 110"),
            new StdLayer("TUONG-220", Tuong, 3, "CONTINUOUS", "Tường 220"),
            new StdLayer("TUONG", Tuong, 2, "CONTINUOUS", "Tường (chiều dày khác)"),
            new StdLayer("CUA-DI", Cua, 30, "CONTINUOUS", "Cửa đi"),
            new StdLayer("CUA-SO", Cua, 5, "CONTINUOUS", "Cửa sổ"),
            new StdLayer("THANG", Thang, 140, "CONTINUOUS", "Cầu thang"),
            new StdLayer("LO-SAN", San, 8, "DASHED", "Lỗ sàn, ô thông tầng"),
            new StdLayer("TEN-PHONG", TenPhong, 7, "CONTINUOUS", "Tên phòng (chữ)"),
            new StdLayer("DAM", Dam, 4, "CONTINUOUS", "Dầm"),
            new StdLayer("SAN", San, 9, "CONTINUOUS", "Sàn / ô sàn"),
            new StdLayer("KICH-THUOC", BoQua, 3, "CONTINUOUS", "Kích thước, ghi chú (không đọc)"),
            new StdLayer("BO-QUA", BoQua, 8, "CONTINUOUS", "Bỏ qua (nội thất, hatch, thiết bị…)"),
        };

        /// <summary>Layer chuẩn theo khoá ("COT") hoặc theo tên ("VXT-COT"); null nếu không có.</summary>
        public static StdLayer Of(string keyOrName)
        {
            if (string.IsNullOrWhiteSpace(keyOrName)) return null;
            string k = keyOrName.Trim().ToUpperInvariant();
            if (k.StartsWith(Prefix, StringComparison.Ordinal)) k = k.Substring(Prefix.Length);
            return All.FirstOrDefault(s => s.Key == k);
        }

        /// <summary>Nhóm cấu kiện nếu layer là layer chuẩn VXT-… (kể cả xref "NHA|VXT-COT", xref đã bind "NHA$0$VXT-COT"); "" nếu không.</summary>
        public static string CategoryOf(string layer)
        {
            var s = StdOf(layer);
            return s == null ? "" : s.Category;
        }

        /// <summary>Layer chuẩn mà tên layer bản vẽ đang trùng (bỏ tiền tố xref); null nếu không.</summary>
        public static StdLayer StdOf(string layer)
        {
            if (string.IsNullOrEmpty(layer)) return null;
            foreach (var v in Variants(layer))
            {
                string u = v.Trim().ToUpperInvariant();
                if (!u.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                var s = All.FirstOrDefault(q => q.Name == u);
                if (s != null) return s;
            }
            return null;
        }

        public static bool IsStandard(string layer) => StdOf(layer) != null;

        /// <summary>Khoá chuẩn mặc định của một nhóm cấu kiện.</summary>
        public static string DefaultKey(string category)
        {
            switch (category)
            {
                case Tuong: return "TUONG";
                case Cot: return "COT";
                case Dam: return "DAM";
                case San: return "SAN";
                case Truc: return "TRUC";
                case Cua: return "CUA-DI";
                case Thang: return "THANG";
                case TenPhong: return "TEN-PHONG";
                case BoQua: return "BO-QUA";
                default: return "";
            }
        }

        /// <summary>Tên đầy đủ + tên đã bỏ tiền tố xref.</summary>
        public static IEnumerable<string> Variants(string layer)
        {
            if (layer == null) yield break;
            yield return layer;
            string s = layer;
            int bar = s.LastIndexOf('|');
            if (bar >= 0 && bar + 1 < s.Length) { s = s.Substring(bar + 1); yield return s; }
            var m = Regex.Match(s, @"^.+?\$\d+\$(.+)$");
            if (m.Success) yield return m.Groups[1].Value;
        }
    }

    /// <summary>Thống kê nội dung một layer (để đoán nhóm khi tên layer không rõ). Toạ độ m.</summary>
    public sealed class LayerStats
    {
        public string Name = "";
        public int Color = 7;
        public string Linetype = "";

        public int Lines, ClosedPolys, Rects, ColumnRects, Circles, BubbleCircles, Arcs, SwingArcs, Texts, Inserts, DoorInserts, WindowInserts,
                   Hatches, Dimensions, LongLines, DashLines, RoomTexts, NumericTexts, AxisLabelTexts;
        public double TotalLength;
        public readonly List<string> TextSamples = new List<string>();
        public readonly Dictionary<string, int> BlockNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Đoạn thẳng (x1, y1, x2, y2) để tìm cặp nét song song (tường) / bậc thang</summary>
        public readonly List<double[]> Segs = new List<double[]>();
        public const int MaxSegs = 1500;

        public int Total => Lines + ClosedPolys + Circles + Arcs + Texts + Inserts + Hatches + Dimensions;

        private static bool IsDash(string lt)
        {
            string n = LayerConvert.Fold(lt);
            return n.Contains("CENTER") || n.Contains("DASHDOT") || n.Contains("CHAIN") || n.Contains("TRUC") || n.Contains("AXIS") || n.Contains("PHANTOM") || n.Contains("DOT");
        }

        public void AddLine(double x1, double y1, double x2, double y2, string linetype = null)
        {
            double L = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            if (L < 1e-6) return;
            Lines++; TotalLength += L;
            if (L >= 5.0) LongLines++;
            if (IsDash(string.IsNullOrEmpty(linetype) ? Linetype : linetype)) DashLines++;
            if (L >= 0.3 && Segs.Count < MaxSegs) Segs.Add(new[] { x1, y1, x2, y2 });
        }

        /// <summary>Đa tuyến: kín → đếm kín / chữ nhật / cột; hở → các đoạn.</summary>
        public void AddPolyline(IList<double[]> pts, bool closed, string linetype = null)
        {
            if (pts == null || pts.Count < 2) return;
            var p = pts.ToList();
            if (p.Count > 2 && Dist(p[0], p[p.Count - 1]) < 1e-6) { p.RemoveAt(p.Count - 1); closed = true; }
            if (closed && p.Count >= 3)
            {
                ClosedPolys++;
                if (p.Count == 4 && IsRect(p, out double a, out double b))
                {
                    Rects++;
                    double mn = Math.Min(a, b), mx = Math.Max(a, b);
                    if (mn >= 0.1 && mx <= 1.2 && mx / mn <= 4.0) ColumnRects++;
                }
                for (int i = 0; i < p.Count; i++) { var q = p[(i + 1) % p.Count]; TotalLength += Dist(p[i], q); }
                return;
            }
            for (int i = 0; i + 1 < p.Count; i++) AddLine(p[i][0], p[i][1], p[i + 1][0], p[i + 1][1], linetype);
        }

        public void AddCircle(double cx, double cy, double r)
        {
            Circles++;
            if (r >= 0.15 && r <= 0.8) BubbleCircles++;
        }

        public void AddArc(double r)
        {
            Arcs++;
            if (r >= 0.5 && r <= 1.4) SwingArcs++;
        }

        public void AddText(string text)
        {
            Texts++;
            string t = (text ?? "").Trim();
            if (TextSamples.Count < 30 && t.Length > 0) TextSamples.Add(t);
            if (LayerConvert.IsRoomText(t)) RoomTexts++;
            else if (Regex.IsMatch(t, @"^[\d\s.,+\-±%x×]+$")) NumericTexts++;
            if (Regex.IsMatch(t, @"^([A-Z]{1,2}\d{0,2}'?|\d{1,2}'?|[A-Z]\d?-\d?)$", RegexOptions.IgnoreCase)) AxisLabelTexts++;
        }

        public void AddInsert(string block)
        {
            Inserts++;
            string b = block ?? "";
            int n; BlockNames.TryGetValue(b, out n); BlockNames[b] = n + 1;
            string k = LayerConvert.BlockKey(b);
            if (k == "CUA-SO") WindowInserts++;
            else if (k == "CUA-DI") DoorInserts++;
        }

        public void AddHatch() => Hatches++;
        public void AddDimension() => Dimensions++;

        private static double Dist(double[] a, double[] b) => Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]));

        private static bool IsRect(List<double[]> p, out double a, out double b)
        {
            a = Dist(p[0], p[1]); b = Dist(p[1], p[2]);
            double c = Dist(p[2], p[3]), d = Dist(p[3], p[0]);
            if (a < 1e-6 || b < 1e-6) return false;
            if (Math.Abs(a - c) > 0.02 * Math.Max(a, c) + 1e-3 || Math.Abs(b - d) > 0.02 * Math.Max(b, d) + 1e-3) return false;
            double dx1 = p[1][0] - p[0][0], dy1 = p[1][1] - p[0][1], dx2 = p[2][0] - p[1][0], dy2 = p[2][1] - p[1][1];
            return Math.Abs(dx1 * dx2 + dy1 * dy2) <= 0.02 * a * b;
        }

        /// <summary>Phân bố khoảng cách cặp nét song song có phần chồng ≥ 30 %: (cặp 0,09–0,13 m; cặp 0,18–0,25 m; tổng cặp 0,05–0,40 m; cặp bậc thang 0,24–0,34 m giữa nét ngắn).</summary>
        public void PairStats(out int c110, out int c220, out int pairs, out int stair)
        {
            c110 = c220 = pairs = stair = 0;
            int n = Segs.Count;
            for (int i = 0; i < n; i++)
            {
                var s = Segs[i];
                double dx = s[2] - s[0], dy = s[3] - s[1], L = Math.Sqrt(dx * dx + dy * dy);
                double ux = dx / L, uy = dy / L;
                double bestD = double.MaxValue; bool bestShort = false;
                for (int j = 0; j < n; j++)
                {
                    if (j == i) continue;
                    var t = Segs[j];
                    double ex = t[2] - t[0], ey = t[3] - t[1], M = Math.Sqrt(ex * ex + ey * ey);
                    if (Math.Abs(ux * ey - uy * ex) / M > 0.02) continue;              // không song song
                    double d = Math.Abs((t[0] - s[0]) * uy - (t[1] - s[1]) * ux);       // khoảng cách vuông góc
                    if (d < 0.05 || d > 0.40 || d >= bestD) continue;
                    double p0 = (t[0] - s[0]) * ux + (t[1] - s[1]) * uy, p1 = (t[2] - s[0]) * ux + (t[3] - s[1]) * uy;
                    double lo = Math.Max(0, Math.Min(p0, p1)), hi = Math.Min(L, Math.Max(p0, p1));
                    if (hi - lo < 0.3 * Math.Min(L, M)) continue;
                    bestD = d; bestShort = L <= 2.5 && M <= 2.5 && Math.Abs(L - M) <= 0.1 * Math.Max(L, M);
                }
                if (bestD == double.MaxValue) continue;
                pairs++;
                if (bestD >= 0.09 && bestD <= 0.13) c110++;
                else if (bestD >= 0.18 && bestD <= 0.25) c220++;
                if (bestShort && bestD >= 0.24 && bestD <= 0.34) stair++;
            }
        }
    }

    /// <summary>Đề xuất layer chuẩn cho một layer bản vẽ.</summary>
    public sealed class LayerSuggestion
    {
        public string Layer = "";
        /// <summary>Khoá layer chuẩn ("COT", "TUONG-110"…); "" = giữ nguyên</summary>
        public string Key = "";
        public double Confidence;
        public string Reason = "";
        public string Category => StdLayers.Of(Key)?.Category ?? "";
        public string Target => Key.Length == 0 ? "" : StdLayers.Prefix + Key;
        public override string ToString() => Layer + " → " + (Key.Length == 0 ? "(giữ nguyên)" : Target) + " (" + Math.Round(Confidence * 100) + "%: " + Reason + ")";
    }

    /// <summary>Bộ phân loại layer: tên layer (tiếng Việt / Anh, có / không dấu, tiền tố AIA A-…, S-…) + nội dung (cặp nét tường,
    /// chữ nhật cột, nét trục + vòng ký hiệu, block cửa, chữ tên phòng, kích thước).</summary>
    public static class LayerConvert
    {
        /// <summary>Bỏ dấu tiếng Việt, chữ hoa, gộp khoảng trắng.</summary>
        public static string Fold(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var f = s.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(f.Length);
            foreach (char c in f)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant(), @"\s+", " ").Trim();
        }

        // (mẫu regex trên tên đã bỏ dấu, khoá chuẩn, lý do) – THỨ TỰ = ưu tiên
        private const string B = @"(^|[^A-Z0-9])", E = @"([^A-Z0-9]|$)";
        private static readonly (string Rx, string Key, string Why)[] NameRules =
        {
            (@"DEFPOINTS", "BO-QUA", "lớp điểm kích thước"),
            (B + @"(DIM|DIMS|KICH ?THUOC|KICHTHUOC|A-ANNO-DIMS|S-ANNO-DIMS)" + E + "|DIMENSION", "KICH-THUOC", "tên layer kích thước"),
            (@"HATCH|PATT|VAT ?LIEU|VATLIEU|NOI ?THAT|FURN|THIET ?BI|TBVS|SANITARY|PLUMB|" + B + @"(CAY|TREE|PLANT|XE|CAR)" + E, "BO-QUA", "nội thất / hatch / thiết bị"),
            (B + @"(TRUC|AXIS|GRID|A-GRID|S-GRID|TIM ?TRUC|CENTER ?LINE)" + E + @"|TRUC|GRID", "TRUC", "tên layer trục"),
            (B + @"(CUA ?SO|CUASO|WINDOW|WINDOWS|GLAZ|A-GLAZ|WIN)" + E + @"|WINDOW|CUA ?SO", "CUA-SO", "tên layer cửa sổ"),
            (B + @"(CUA|CUA ?DI|DOOR|DOORS|A-DOOR)" + E + @"|DOOR", "CUA-DI", "tên layer cửa đi"),
            (B + @"(COT|COLUMN|COLUMNS|COLS|S-COLS|A-COLS|COL)" + E + @"|COLUMN", "COT", "tên layer cột"),
            (@"TUONG|WALL|" + B + @"(XAY|A-WALL)" + E, "TUONG", "tên layer tường"),
            (@"THANG|STAIR|STRS|" + B + @"(BAC)" + E, "THANG", "tên layer cầu thang"),
            (B + @"(LO ?SAN|LOSAN|VOID|OPENING|THONG ?TANG|LO ?THONG|LO)" + E, "LO-SAN", "tên layer lỗ sàn"),
            (@"TEN ?PHONG|TENPHONG|ROOM|A-AREA-IDEN|" + B + @"(PHONG)" + E, "TEN-PHONG", "tên layer tên phòng"),
            (B + @"(DAM|BEAM|BEAMS|S-BEAM|GIANG)" + E, "DAM", "tên layer dầm"),
            (B + @"(SAN|SLAB|S-SLAB|FLOOR)" + E, "SAN", "tên layer sàn"),
            (B + @"(TEXT|CHU|GHI ?CHU|NOTE|NOTES|ANNO|TITLE|KHUNG|FRAME|VIEWPORT|VPORT)" + E, "KICH-THUOC", "chữ / ghi chú / khung tên"),
        };

        /// <summary>Đoán theo TÊN layer: khoá chuẩn + lý do; "" nếu không nhận.</summary>
        public static string ByName(string layer, out string why)
        {
            why = "";
            var std = StdLayers.StdOf(layer);
            if (std != null) { why = "đã là layer chuẩn"; return std.Key; }
            foreach (var v in StdLayers.Variants(layer).Reverse())   // ưu tiên tên đã bỏ tiền tố xref
            {
                string n = Fold(v).Replace('_', '-');
                if (n.Length == 0) continue;
                foreach (var r in NameRules)
                    if (Regex.IsMatch(n, r.Rx, RegexOptions.CultureInvariant))
                    {
                        why = r.Why;
                        string k = r.Key;
                        if (k == "TUONG")
                        {
                            if (Regex.IsMatch(n, @"(^|[^0-9])(100|110|10)([^0-9]|$)")) k = "TUONG-110";
                            else if (Regex.IsMatch(n, @"(^|[^0-9])(200|220|20)([^0-9]|$)")) k = "TUONG-220";
                        }
                        if (k == "TRUC" && Regex.IsMatch(n, @"KH|BUBBLE|IDEN|TEXT|TEN|SO ?TRUC|TAG")) k = "TRUC-KH";
                        return k;
                    }
            }
            return "";
        }

        /// <summary>Khoá cửa theo TÊN BLOCK (block cửa đặt trên layer bất kỳ vẫn chuyển sang layer cửa): "CUA-SO" / "CUA-DI" / "".</summary>
        public static string BlockKey(string block)
        {
            string n = Fold(block);
            if (n.Length == 0 || n.StartsWith("*", StringComparison.Ordinal)) return "";
            if (n.Contains("WINDOW") || n.Contains("CUA SO") || n.Contains("CUASO") || n.Contains("GLAZ") || Regex.IsMatch(n, @"^(S|CS|W)\d")) return "CUA-SO";
            if (n.Contains("DOOR") || n.Contains("CUA") || Regex.IsMatch(n, @"^(D|CD|DD|DI)\d")) return "CUA-DI";
            return "";
        }

        private static readonly string[] RoomWords =
        {
            "PHONG", "P.NGU", "P. NGU", "NGU", "KHACH", "BEP", "AN", "WC", "VE SINH", "VS", "TAM", "SANH", "BAN CONG", "LOGIA", "KHO", "THO",
            "LAM VIEC", "SINH HOAT", "GIAT", "PHOI", "GARA", "GARAGE", "HANH LANG", "SAN TRUOC", "SAN SAU", "TIEN SANH", "THAY DO",
            "ROOM", "BED", "LIVING", "KITCHEN", "DINING", "TOILET", "BATH", "WC", "HALL", "LOBBY", "BALCONY", "STORE", "OFFICE"
        };

        /// <summary>Chữ giống tên phòng (Phòng ngủ, P.Khách, WC, Bếp, Kitchen…).</summary>
        public static bool IsRoomText(string text)
        {
            string n = " " + Fold(text).Replace('.', ' ').Replace('-', ' ') + " ";
            if (n.Trim().Length < 2 || Regex.IsMatch(n, @"^[\s\d.,+\-x×]+$")) return false;
            foreach (var w in RoomWords)
                if (n.Contains(" " + w.Replace('.', ' ') + " ") || (w.Length >= 4 && n.Contains(" " + w))) return true;
            return false;
        }

        /// <summary>Đoán theo NỘI DUNG: khoá + độ tin cậy (0…1) + lý do.</summary>
        public static string ByContent(LayerStats s, out double conf, out string why)
        {
            conf = 0; why = "";
            if (s == null || s.Total == 0) { why = "layer trống"; return ""; }
            int c110, c220, pairs, stair;
            s.PairStats(out c110, out c220, out pairs, out stair);
            int tot = s.Total;
            var cand = new List<(string Key, double Score, string Why)>();

            // trục: nét gạch chấm / nét rất dài + vòng tròn ký hiệu + chữ 1 ký tự
            double ax = (s.DashLines >= 2 ? 0.4 : 0) + (s.LongLines >= 2 ? 0.2 : 0) + (s.BubbleCircles >= 2 ? 0.2 : 0) + (s.AxisLabelTexts >= 2 ? 0.2 : 0);
            if (s.DashLines < 2 && s.LongLines < 2 && s.BubbleCircles >= 2 && s.AxisLabelTexts >= 2) cand.Add(("TRUC-KH", 0.6, $"{s.BubbleCircles} vòng tròn + {s.AxisLabelTexts} tên trục"));
            else if (ax >= 0.4) cand.Add(("TRUC", Math.Min(0.85, ax), $"{s.DashLines} nét gạch chấm, {s.LongLines} nét dài ≥ 5 m, {s.BubbleCircles} vòng tròn ký hiệu"));

            // cột: nhiều chữ nhật kín cạnh 0,1–1,2 m
            if (s.ColumnRects >= 2 && s.ColumnRects >= 0.5 * Math.Max(1, s.ClosedPolys))
                cand.Add(("COT", Math.Min(0.85, 0.5 + 0.2 * s.ColumnRects / Math.Max(1.0, s.ClosedPolys) + (s.Hatches > 0 ? 0.1 : 0)), $"{s.ColumnRects} chữ nhật kín cỡ cột"));

            // tường: cặp nét song song 110 / 220
            if (pairs >= 4 && c110 + c220 >= 0.4 * pairs)
            {
                string k = c110 >= 2 * c220 ? "TUONG-110" : c220 >= 2 * c110 ? "TUONG-220" : "TUONG";
                cand.Add((k, Math.Min(0.8, 0.45 + 0.35 * (c110 + c220) / (double)pairs), $"{c110} cặp nét cách 110, {c220} cặp cách 220"));
            }

            // cửa: block cửa / cung mở cửa
            if (s.DoorInserts + s.WindowInserts >= 1 && s.DoorInserts + s.WindowInserts >= 0.3 * Math.Max(1, s.Inserts))
                cand.Add((s.WindowInserts > s.DoorInserts ? "CUA-SO" : "CUA-DI", 0.7, $"{s.DoorInserts} block cửa đi, {s.WindowInserts} block cửa sổ"));
            else if (s.SwingArcs >= 2 && s.SwingArcs >= 0.15 * tot)
                cand.Add(("CUA-DI", 0.5, $"{s.SwingArcs} cung mở cửa"));

            // thang: nhiều nét ngắn song song cách đều 0,24–0,34 m
            if (stair >= 6) cand.Add(("THANG", Math.Min(0.7, 0.4 + 0.02 * stair), $"{stair} nét bậc cách 0,24–0,34 m"));

            // chữ
            if (s.Texts >= 1 && s.Texts >= 0.6 * tot)
            {
                if (s.RoomTexts >= Math.Max(1, 0.3 * s.Texts)) cand.Add(("TEN-PHONG", 0.65, $"{s.RoomTexts}/{s.Texts} chữ giống tên phòng"));
                else if (s.NumericTexts >= 0.5 * s.Texts) cand.Add(("KICH-THUOC", 0.55, $"{s.NumericTexts}/{s.Texts} chữ là số"));
            }
            if (s.Dimensions >= 1 && s.Dimensions >= 0.5 * tot) cand.Add(("KICH-THUOC", 0.7, $"{s.Dimensions} đường kích thước"));
            if (s.Hatches >= 1 && s.Hatches >= 0.7 * tot) cand.Add(("BO-QUA", 0.5, $"{s.Hatches} mảng hatch"));

            if (cand.Count == 0) { why = "không đủ dấu hiệu"; return ""; }
            var best = cand.OrderByDescending(c => c.Score).First();
            conf = best.Score; why = best.Why;
            return best.Key;
        }

        /// <summary>Đề xuất cho một layer: bộ ánh xạ (profile) → layer chuẩn → tên layer → quy tắc người dùng → nội dung.
        /// userCategory: quy tắc riêng của người dùng (CadStandard / CadStandard.json) trả về nhóm (Tuong, Cot…) hoặc ""/null.</summary>
        public static LayerSuggestion Suggest(LayerStats s, LayerProfile profile = null, Func<string, string> userCategory = null)
        {
            var r = new LayerSuggestion { Layer = s?.Name ?? "" };
            if (s == null) return r;
            string pk = profile?.Lookup(s.Name);
            if (pk != null) { r.Key = pk; r.Confidence = 1; r.Reason = "bộ ánh xạ \"" + profile.Name + "\""; return r; }

            double cc; string cwhy;
            string ck = ByContent(s, out cc, out cwhy);
            string nwhy;
            string nk = ByName(s.Name, out nwhy);
            if (nk.Length > 0)
            {
                r.Key = nk; r.Confidence = StdLayers.IsStandard(s.Name) ? 1.0 : 0.9; r.Reason = nwhy;
                // tinh chỉnh theo nội dung: tường 110 / 220, cửa đi / cửa sổ
                if (nk == "TUONG" && (ck == "TUONG-110" || ck == "TUONG-220")) { r.Key = ck; r.Reason += "; " + cwhy; }
                if ((nk == "CUA-DI" || nk == "CUA-SO") && (ck == "CUA-DI" || ck == "CUA-SO") && cc >= 0.7) { r.Key = ck; r.Reason += "; " + cwhy; }
                if (nk == "TRUC" && ck == "TRUC-KH") { r.Key = ck; r.Reason += "; " + cwhy; }
                return r;
            }
            string uc = userCategory?.Invoke(s.Name);
            if (!string.IsNullOrEmpty(uc))
            {
                string k = StdLayers.DefaultKey(uc);
                if (k.Length > 0)
                {
                    var ckStd = StdLayers.Of(ck);
                    if (ckStd != null && ckStd.Category == uc) k = ck;
                    r.Key = k; r.Confidence = 0.85; r.Reason = "quy tắc layer của người dùng"; return r;
                }
            }
            r.Key = ck; r.Confidence = cc; r.Reason = ck.Length == 0 ? (cwhy.Length > 0 ? cwhy : "không nhận ra") + " – giữ nguyên" : cwhy;
            return r;
        }

        /// <summary>Layer đích cho MỘT đối tượng: block cửa → layer cửa (bất kể layer); kích thước → VXT-KICH-THUOC nếu layer gốc được chuyển;
        /// còn lại theo ánh xạ của layer. "" = giữ nguyên.</summary>
        public static string TargetFor(string layerKey, string blockName, bool isDimension, bool doorBlocks = true)
        {
            if (doorBlocks)
            {
                string bk = BlockKey(blockName);
                if (bk.Length > 0) return StdLayers.Prefix + bk;
            }
            if (string.IsNullOrEmpty(layerKey)) return "";
            if (isDimension) return StdLayers.Prefix + "KICH-THUOC";
            return StdLayers.Prefix + layerKey;
        }
    }

    /// <summary>Bộ ánh xạ layer → layer chuẩn, dùng lại cho các bản vẽ cùng nguồn (cùng văn phòng kiến trúc).
    /// JSON: { "name": "...", "entries": [ { "layer": "A-WALL", "key": "TUONG-220" } ], "rules": [ { "pattern": "KT-TUONG*", "key": "TUONG" } ] }</summary>
    public sealed class LayerProfile
    {
        public string Name = "Bộ ánh xạ";
        /// <summary>Gán trực tiếp: tên layer → khoá chuẩn ("" = giữ nguyên)</summary>
        public readonly List<KeyValuePair<string, string>> Entries = new List<KeyValuePair<string, string>>();
        /// <summary>Quy tắc ký tự đại diện (* ?) trên tên đã bỏ dấu → khoá chuẩn</summary>
        public readonly List<KeyValuePair<string, string>> Rules = new List<KeyValuePair<string, string>>();

        public void Set(string layer, string key)
        {
            if (string.IsNullOrEmpty(layer)) return;
            Entries.RemoveAll(e => string.Equals(e.Key, layer, StringComparison.OrdinalIgnoreCase));
            Entries.Add(new KeyValuePair<string, string>(layer, Clean(key)));
        }

        public void AddRule(string pattern, string key)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return;
            Rules.Add(new KeyValuePair<string, string>(pattern.Trim(), Clean(key)));
        }

        private static string Clean(string key)
        {
            var s = StdLayers.Of(key);
            return s == null ? "" : s.Key;
        }

        /// <summary>Khoá chuẩn của layer theo bộ ánh xạ (gán trực tiếp – cả tên bỏ tiền tố xref – rồi quy tắc); null = bộ ánh xạ không nói gì.</summary>
        public string Lookup(string layer)
        {
            if (string.IsNullOrEmpty(layer)) return null;
            foreach (var v in StdLayers.Variants(layer))
                foreach (var e in Entries)
                    if (string.Equals(e.Key, v, StringComparison.OrdinalIgnoreCase)) return e.Value;
            foreach (var v in StdLayers.Variants(layer))
            {
                string n = LayerConvert.Fold(v);
                foreach (var r in Rules)
                {
                    string p = "^" + Regex.Escape(LayerConvert.Fold(r.Key)).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                    try { if (Regex.IsMatch(n, p, RegexOptions.CultureInvariant)) return r.Value; }
                    catch (ArgumentException) { }
                }
            }
            return null;
        }

        private static string Esc(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string Unesc(string s) =>
            Regex.Replace(s ?? "", @"\\(u[0-9a-fA-F]{4}|.)", m =>
            {
                string g = m.Groups[1].Value;
                if (g.Length == 5 && g[0] == 'u') return ((char)int.Parse(g.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString();
                switch (g) { case "n": return "\n"; case "t": return "\t"; case "r": return "\r"; default: return g; }
            });

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"name\": \"").Append(Esc(Name)).Append("\",\n  \"entries\": [");
            for (int i = 0; i < Entries.Count; i++)
                sb.Append(i == 0 ? "\n" : ",\n").Append("    { \"layer\": \"").Append(Esc(Entries[i].Key)).Append("\", \"key\": \"").Append(Esc(Entries[i].Value)).Append("\" }");
            sb.Append(Entries.Count > 0 ? "\n  " : "").Append("],\n  \"rules\": [");
            for (int i = 0; i < Rules.Count; i++)
                sb.Append(i == 0 ? "\n" : ",\n").Append("    { \"pattern\": \"").Append(Esc(Rules[i].Key)).Append("\", \"key\": \"").Append(Esc(Rules[i].Value)).Append("\" }");
            sb.Append(Rules.Count > 0 ? "\n  " : "").Append("]\n}\n");
            return sb.ToString();
        }

        private const string Str = "\"((?:[^\"\\\\]|\\\\.)*)\"";

        /// <summary>Đọc JSON do ToJson ghi (chịu được khoảng trắng / thứ tự trường layer–key); lỗi → bộ rỗng.</summary>
        public static LayerProfile FromJson(string json)
        {
            var p = new LayerProfile();
            if (string.IsNullOrWhiteSpace(json)) return p;
            var mn = Regex.Match(json, "\"name\"\\s*:\\s*" + Str);
            if (mn.Success) p.Name = Unesc(mn.Groups[1].Value);
            foreach (Match m in Regex.Matches(json, "\\{[^{}]*\\}"))
            {
                string o = m.Value;
                var mk = Regex.Match(o, "\"key\"\\s*:\\s*" + Str);
                var ml = Regex.Match(o, "\"layer\"\\s*:\\s*" + Str);
                var mp = Regex.Match(o, "\"pattern\"\\s*:\\s*" + Str);
                if (!mk.Success) continue;
                if (ml.Success) p.Set(Unesc(ml.Groups[1].Value), Unesc(mk.Groups[1].Value));
                else if (mp.Success) p.AddRule(Unesc(mp.Groups[1].Value), Unesc(mk.Groups[1].Value));
            }
            return p;
        }
    }
}

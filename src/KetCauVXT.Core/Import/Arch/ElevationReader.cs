using System.Globalization;
using System.Text.RegularExpressions;

namespace KetCauVXT.Import.Arch;

/// <summary>Một cao độ đọc được trên mặt đứng / mặt cắt.</summary>
public sealed class LevelMark
{
    /// <summary>Cao độ (m)</summary>
    public double Elevation { get; set; }
    /// <summary>Chữ cao độ gốc ("+3.600", "±0.000"…)</summary>
    public string Text { get; set; } = "";
    /// <summary>Tên tầng đọc được cạnh cao độ ("TẦNG 2", "MÁI"…), rỗng nếu không có</summary>
    public string Name { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>Tầng suy ra từ các cao độ.</summary>
public sealed record LevelStory(string Name, double Elevation, double Height);

/// <summary>[Mặt đứng] Đọc cao độ tầng trên bản vẽ mặt đứng / mặt cắt kiến trúc (chữ "+3.600", "±0.000", "-0.450", "COS +7.200", "▽+10.500",
/// "+3600" theo mm…) → danh sách cao độ, tên tầng ghi cạnh, chiều cao tầng. Dùng chung cho nhập bản vẽ trong chương trình và
/// add-in AutoCAD (lệnh VXT_MATDUNG có bộ đọc riêng cùng quy tắc).</summary>
public static class ElevationReader
{
    // dấu bắt buộc (+, -, ±) hoặc tiền tố cốt / cos / el / ▽ ; số dạng 3.600 / 3,600 / 3600
    private static readonly Regex Rx = new(
        @"(?<pre>(?:c[oố]t|cos|code|el\.?|elev\.?|cao\s*đ[ộo]|▽|▼|∇)\s*)?(?<sgn>[+\-±−]|%%p)?\s*(?<int>\d{1,5})(?:[.,](?<dec>\d{1,3}))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex StoryName = new(@"(t[aầ]ng\s*(h[aầ]m\s*)?\d+|t[aầ]ng\s*(tr[eệ]t|l[ửu]ng|m[aá]i|tum|h[aầ]m)|m[aá]i|tum|s[aâ]n\s*th[uư][ợo]ng|tr[eệ]t|l[ửu]ng|n[eề]n|h[aầ]m\s*\d*|đ[iỉ]nh\s*m[aá]i)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Đọc một chuỗi cao độ. Chỉ nhận chuỗi có dấu (+ - ±) hoặc tiền tố cốt/cos/▽ để không lẫn kích thước thường.</summary>
    public static bool TryParse(string text, out double z)
    {
        z = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string t = text.Trim().Replace("\\P", " ");
        if (t.Length > 40) return false;
        foreach (Match m in Rx.Matches(t))
        {
            bool hasSign = m.Groups["sgn"].Success, hasPre = m.Groups["pre"].Success;
            if (!hasSign && !hasPre) continue;
            string sgn = m.Groups["sgn"].Value;
            if (!m.Groups["dec"].Success)
            {
                // "+3600" (mm) hoặc "+3" (m)
                if (!double.TryParse(m.Groups["int"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out double iv)) continue;
                z = iv >= 100 ? iv / 1000.0 : iv;
            }
            else
            {
                string dec = m.Groups["dec"].Value;
                if (!double.TryParse(m.Groups["int"].Value + "." + dec, NumberStyles.Float, CultureInfo.InvariantCulture, out z)) continue;
                // "+3,6" hoặc "+3.60" vẫn là m; số nguyên lớn + 3 chữ số thập phân ("+12.600") là m
            }
            if (sgn == "-" || sgn == "−") z = -z;
            if (Math.Abs(z) > 300) continue;
            return true;
        }
        return false;
    }

    /// <summary>Gom các chữ cao độ (toạ độ m) → cao độ khác nhau (sai số 5 mm), kèm tên tầng gần nhất trên cùng hàng.</summary>
    public static List<LevelMark> FromTexts(IEnumerable<(string Text, double X, double Y, double H)> texts)
    {
        var all = texts?.Where(t => !string.IsNullOrWhiteSpace(t.Text)).ToList() ?? new List<(string, double, double, double)>();
        var marks = new List<LevelMark>();
        foreach (var t in all)
            if (TryParse(t.Text, out double z)) marks.Add(new LevelMark { Elevation = Math.Round(z, 3), Text = t.Text.Trim(), X = t.X, Y = t.Y });
        var res = new List<LevelMark>();
        foreach (var g in marks.GroupBy(m => Math.Round(m.Elevation / 0.005)).OrderBy(g => g.Key))
        {
            var first = g.First();
            // tên tầng: chữ chứa "TẦNG …/MÁI/TUM" nằm gần cùng hàng với một trong các chữ cao độ của nhóm
            string name = "";
            double best = double.MaxValue;
            foreach (var m in g)
                foreach (var t in all)
                {
                    var mm = StoryName.Match(t.Text);
                    if (!mm.Success) continue;
                    double tol = Math.Max(t.H, 0.2) * 2.5;
                    double dy = Math.Abs(t.Y - m.Y);
                    if (dy > tol) continue;
                    double d = dy * 10 + Math.Abs(t.X - m.X);
                    if (d < best) { best = d; name = Clean(mm.Value); }
                }
            res.Add(new LevelMark { Elevation = first.Elevation, Text = first.Text, Name = name, X = first.X, Y = first.Y });
        }
        return res;
    }

    private static string Clean(string s)
    {
        s = Regex.Replace(s.Trim(), @"\s+", " ");
        return CultureInfo.GetCultureInfo("vi-VN").TextInfo.ToUpper(s);
    }

    /// <summary>Đọc file mặt đứng / mặt cắt DXF / DWG.</summary>
    public static List<LevelMark> FromFile(string path, double scale = 0, List<string> warnings = null)
    {
        var d = new ArchDrawing();
        var dxf = ArchPlanReader.ReadFile(path, scale, d);
        if (warnings != null) warnings.AddRange(d.Warnings);
        if (dxf == null) return new List<LevelMark>();
        return FromTexts(dxf.Entities.Where(e => e.Type == DxfEntityType.Text && e.Points.Count > 0)
                                     .Select(e => (e.Text, e.Points[0].X, e.Points[0].Y, e.TextHeight)));
    }

    /// <summary>Tầng từ các cao độ: bắt đầu từ cao độ gốc (±0.000 nếu có, không thì cao độ thấp nhất ≥ minBase), mỗi tầng cao đến cao độ kế tiếp.
    /// Cao độ trên cùng là mái (không thành tầng). Bỏ các cao độ cách nhau &lt; minHeight (sênô, bậu cửa…).</summary>
    public static List<LevelStory> Stories(IList<LevelMark> levels, double minHeight = 2.2, double? baseElevation = null)
    {
        var res = new List<LevelStory>();
        if (levels == null || levels.Count < 2) return res;
        var zs = levels.OrderBy(l => l.Elevation).ToList();
        double b = baseElevation ?? (zs.Any(l => Math.Abs(l.Elevation) < 0.0005) ? 0 : zs.Where(l => l.Elevation > -1.5).Select(l => l.Elevation).DefaultIfEmpty(zs[0].Elevation).Min());
        var use = new List<LevelMark> { zs.OrderBy(l => Math.Abs(l.Elevation - b)).First() };
        foreach (var l in zs.Where(l => l.Elevation > use[0].Elevation + 1e-6))
            if (l.Elevation - use[^1].Elevation >= minHeight - 1e-6) use.Add(l);
        for (int i = 0; i + 1 < use.Count; i++)
        {
            string nm = use[i].Name.Length > 0 ? use[i].Name : "TẦNG " + (i + 1).ToString(CultureInfo.InvariantCulture);
            res.Add(new LevelStory(nm, use[i].Elevation, Math.Round(use[i + 1].Elevation - use[i].Elevation, 3)));
        }
        return res;
    }
}

using System.Globalization;
using System.Text;
using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.Export;

/// <summary>So sánh nội lực của chương trình với bảng "Element Forces - Frames" xuất từ ETABS / SAP2000 (CSV hoặc TXT phân tách bằng dấu phẩy, chấm phẩy hoặc tab).
/// Cách lấy bảng trong ETABS: Display > Show Tables > Analysis > Results > Frame Results > Element Forces - Frames,
/// sau đó File > Export > Current Table to CSV. Trong SAP2000: Display > Show Tables > Element Forces - Frames > File > Export to Excel/CSV.
/// Khớp thanh theo tên: "C12", "B12", "D12" (file .e2k) hoặc "12", "12_3" (file .s2k) → Id = 12.</summary>
public static class ForceComparer
{
    public class Row
    {
        public int FrameId { get; set; }
        public string Label { get; set; } = "";
        public string Case { get; set; } = "";
        public double Station { get; set; }
        public double[] Theirs { get; set; } = new double[6];
        public double[] Ours { get; set; } = new double[6];
        public double MaxAbsDiff { get; set; }
        /// <summary>Sai lệch lớn nhất chuẩn hoá theo giá trị lớn nhất của cùng thanh/trường hợp (%)</summary>
        public double MaxRelDiff { get; set; }
    }

    public class Report
    {
        public List<Row> Rows { get; } = new();
        public List<string> Messages { get; } = new();
        public int Matched => Rows.Count;
        public double WorstPercent => Rows.Count == 0 ? 0 : Rows.Max(r => r.MaxRelDiff);
        public double MeanPercent => Rows.Count == 0 ? 0 : Rows.Average(r => r.MaxRelDiff);

        public void SaveCsv(string path)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("FrameId,Label,Case,Station,P_ETABS,V2_ETABS,V3_ETABS,T_ETABS,M2_ETABS,M3_ETABS,P_VXT,V2_VXT,V3_VXT,T_VXT,M2_VXT,M3_VXT,MaxAbsDiff,MaxRelDiff_%");
            foreach (var r in Rows)
                sb.AppendLine(string.Join(",", new[]
                {
                    r.FrameId.ToString(), r.Label, r.Case, r.Station.ToString("0.###", inv)
                }.Concat(r.Theirs.Select(v => v.ToString("0.###", inv)))
                 .Concat(r.Ours.Select(v => v.ToString("0.###", inv)))
                 .Append(r.MaxAbsDiff.ToString("0.###", inv)).Append(r.MaxRelDiff.ToString("0.##", inv))));
            sb.AppendLine();
            foreach (var m in Messages) sb.AppendLine("# " + m.Replace(',', ';'));
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }
    }

    public static Report Compare(AnalysisResults res, string tablePath)
    {
        var rep = new Report();
        var lines = File.ReadAllLines(tablePath).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (lines.Count < 2) { rep.Messages.Add("File rỗng"); return rep; }
        char sep = lines.Take(5).Any(l => l.Contains('\t')) ? '\t' : lines.Take(5).Any(l => l.Contains(';')) ? ';' : ',';

        // tìm dòng tiêu đề (dòng có cả "Station" và "M3")
        int hi = lines.FindIndex(l => l.Contains("Station", StringComparison.OrdinalIgnoreCase) && l.Contains("M3"));
        if (hi < 0) { rep.Messages.Add("Không tìm thấy dòng tiêu đề có cột Station và M3"); return rep; }
        var head = Split(lines[hi], sep).Select(h => h.Trim().Trim('"')).ToList();
        int Col(params string[] names)
        {
            foreach (var n in names)
            {
                int i = head.FindIndex(h => h.Equals(n, StringComparison.OrdinalIgnoreCase));
                if (i >= 0) return i;
            }
            return -1;
        }
        int cFrame = Col("Unique Name", "UniqueName", "Frame", "Beam", "Column", "Label");
        int cLabel = Col("Frame", "Beam", "Column", "Label");
        int cCase = Col("Output Case", "OutputCase", "Load Case/Combo", "Load Case");
        int cSta = Col("Station");
        int[] cF = { Col("P"), Col("V2"), Col("V3"), Col("T"), Col("M2"), Col("M3") };
        if (cFrame < 0 || cCase < 0 || cSta < 0 || cF.Any(c => c < 0)) { rep.Messages.Add("Thiếu cột bắt buộc trong bảng"); return rep; }

        var inv = CultureInfo.InvariantCulture;
        var p = res.Project;
        var byFrame = p.Frames.ToDictionary(f => f.Id);
        int skipped = 0;

        // thanh vát xuất thành các đoạn Id_k: cộng dồn vị trí theo đoạn
        foreach (var line in lines.Skip(hi + 1))
        {
            var c = Split(line, sep).Select(cell => cell.Trim().Trim('"')).ToList();
            if (c.Count <= cF.Max()) continue;
            if (!double.TryParse(c[cSta], NumberStyles.Float, inv, out double sta)) continue; // dòng đơn vị
            string fname = c[cFrame];
            if (cLabel >= 0 && !TryId(fname, out _) && TryId(c[cLabel], out _)) fname = c[cLabel];
            if (!TryId(fname, out int id, out int seg) || !byFrame.TryGetValue(id, out var f)) { skipped++; continue; }
            string cs = c[cCase];
            CaseResult cr;
            try { cr = res.Get(cs); } catch { skipped++; continue; }
            if (!cr.Frames.TryGetValue(id, out var ff)) { skipped++; continue; }
            double L = p.Length(f);
            double x = sta;
            if (seg > 0)
            {
                int n = Math.Max(2, p.Steel.TaperSegments);
                x = (seg - 1) * L / n + sta;
            }
            var theirs = new double[6];
            for (int k = 0; k < 6; k++) double.TryParse(c[cF[k]], NumberStyles.Float, inv, out theirs[k]);
            var ours = ff.At(x);
            rep.Rows.Add(new Row { FrameId = id, Label = f.Label, Case = cs, Station = x, Theirs = theirs, Ours = ours });
        }
        // chuẩn hoá theo max của thanh–trường hợp
        foreach (var g in rep.Rows.GroupBy(r => (r.FrameId, r.Case)))
        {
            var scale = new double[6];
            foreach (var r in g) for (int k = 0; k < 6; k++) scale[k] = Math.Max(scale[k], Math.Max(Math.Abs(r.Theirs[k]), Math.Abs(r.Ours[k])));
            double gmax = scale.Max();
            foreach (var r in g)
            {
                double absd = 0, reld = 0;
                for (int k = 0; k < 6; k++)
                {
                    double d = Math.Abs(r.Theirs[k] - r.Ours[k]);
                    absd = Math.Max(absd, d);
                    // bỏ qua thành phần quá nhỏ so với thành phần lớn nhất của thanh (nhiễu số)
                    double s = Math.Max(scale[k], 0.05 * gmax);
                    if (s > 1e-6) reld = Math.Max(reld, d / s * 100);
                }
                r.MaxAbsDiff = absd; r.MaxRelDiff = reld;
            }
        }
        rep.Messages.Add($"Đã khớp {rep.Rows.Count} dòng, bỏ qua {skipped} dòng (không nhận diện được thanh hoặc trường hợp tải).");
        if (rep.Rows.Count > 0)
            rep.Messages.Add($"Sai lệch trung bình {rep.MeanPercent:0.00}% ; lớn nhất {rep.WorstPercent:0.00}% (thanh {rep.Rows.OrderByDescending(r => r.MaxRelDiff).First().FrameId}).");
        return rep;
    }

    private static bool TryId(string name, out int id) => TryId(name, out id, out _);

    /// <summary>"C12" → 12 ; "12" → 12 ; "12_3" → (12, đoạn 3) ; "B5-1" (ETABS tự chia) → 5.</summary>
    private static bool TryId(string name, out int id, out int seg)
    {
        id = 0; seg = 0;
        if (string.IsNullOrWhiteSpace(name)) return false;
        string s = name.Trim();
        int i = 0;
        while (i < s.Length && char.IsLetter(s[i])) i++;
        int j = i;
        while (j < s.Length && char.IsDigit(s[j])) j++;
        if (j == i || !int.TryParse(s.Substring(i, j - i), out id)) return false;
        if (j < s.Length && s[j] == '_' && int.TryParse(s.Substring(j + 1), out int k)) seg = k;
        return true;
    }

    private static List<string> Split(string line, char sep)
    {
        var res = new List<string>();
        var cur = new StringBuilder();
        bool q = false;
        foreach (char ch in line)
        {
            if (ch == '"') { q = !q; continue; }
            if (ch == sep && !q) { res.Add(cur.ToString()); cur.Clear(); continue; }
            cur.Append(ch);
        }
        res.Add(cur.ToString());
        return res;
    }
}

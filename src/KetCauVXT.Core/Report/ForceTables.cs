using System.Globalization;
using System.Text;
using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>Bảng kết quả phân tích theo định dạng bảng của ETABS (Element Forces - Frames, Joint Reactions,
/// Joint Displacements) và bảng lực ngang tầng. Đơn vị: kN, kN.m, m, rad.
/// Cột "Frame" lấy tên hiển thị (Label), cột "Unique Name" lấy Id phần tử (duy nhất).</summary>
public static class ForceTables
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static readonly string[] FrameForceHeaders =
        { "Story", "Frame", "Unique Name", "Output Case", "Case Type", "Station", "P", "V2", "V3", "T", "M2", "M3" };
    public static readonly string[] ReactionHeaders =
        { "Story", "Joint", "Unique Name", "Output Case", "Case Type", "FX", "FY", "FZ", "MX", "MY", "MZ" };
    public static readonly string[] DisplacementHeaders =
        { "Story", "Joint", "Unique Name", "Output Case", "Case Type", "Ux", "Uy", "Uz", "Rx", "Ry", "Rz" };
    public static readonly string[] LateralHeaders =
        { "Load Pattern", "Story", "Elevation", "FX", "FY", "Note" };

    /// <summary>Danh sách (tên, loại) các trường hợp xuất: mọi trường hợp tải rồi mọi tổ hợp tuyến tính (bỏ tổ hợp bao).</summary>
    public static List<(string name, string type)> OutputCases(AnalysisResults r)
    {
        var l = new List<(string, string)>();
        var p = r.Project;
        var seen = new HashSet<string>();
        if (p != null)
            foreach (var pat in p.Patterns)
                if (r.Cases.ContainsKey(pat.Name) && seen.Add(pat.Name)) l.Add((pat.Name, "LinStatic"));
        foreach (var k in r.Cases.Keys)
            if (seen.Add(k)) l.Add((k, "LinStatic"));
        if (p != null)
            foreach (var c in p.Combinations)
                if (c.Type != ComboType.Envelope && seen.Add(c.Name)) l.Add((c.Name, "Combination"));
        return l;
    }

    private static CaseResult TryGet(AnalysisResults r, string name)
    {
        try { return r.Get(name); }
        catch (Exception) { return null; }
    }

    private static string NodeStory(Project p, Node n)
    {
        if (n == null) return "";
        if (!string.IsNullOrEmpty(n.Story)) return n.Story;
        if (p == null) return "";
        if (Math.Abs(n.Z - p.BaseElevation) < 1e-3) return "Base";
        var st = p.StoryAt(n.Z);
        return st?.Name ?? "";
    }

    // ------------------------------------------------------------------
    public static IEnumerable<object[]> FrameForceRows(AnalysisResults r)
    {
        var p = r.Project;
        var cases = OutputCases(r);
        var frames = p?.Frames ?? new List<FrameElement>();
        foreach (var (name, type) in cases)
        {
            var cr = TryGet(r, name);
            if (cr == null) continue;
            foreach (var f in frames)
            {
                if (!cr.Frames.TryGetValue(f.Id, out var ff)) continue;
                for (int i = 0; i < ff.Count; i++)
                    yield return new object[]
                    {
                        f.Story ?? "", string.IsNullOrEmpty(f.Label) ? f.Id.ToString(Inv) : f.Label, f.Id, name, type,
                        ff.Stations[i], ff.P(i), ff.V2(i), ff.V3(i), ff.T(i), ff.M2(i), ff.M3(i)
                    };
            }
        }
    }

    public static IEnumerable<object[]> ReactionRows(AnalysisResults r)
    {
        var p = r.Project;
        foreach (var (name, type) in OutputCases(r))
        {
            var cr = TryGet(r, name);
            if (cr == null) continue;
            foreach (var kv in cr.Reactions.OrderBy(k => k.Key))
            {
                Node n = p?.Nodes.FirstOrDefault(x => x.Id == kv.Key);
                var v = kv.Value;
                yield return new object[] { NodeStory(p, n), kv.Key.ToString(Inv), kv.Key, name, type, v[0], v[1], v[2], v[3], v[4], v[5] };
            }
        }
    }

    public static IEnumerable<object[]> DisplacementRows(AnalysisResults r)
    {
        var p = r.Project;
        foreach (var (name, type) in OutputCases(r))
        {
            var cr = TryGet(r, name);
            if (cr == null) continue;
            foreach (var kv in cr.Disp.OrderBy(k => k.Key))
            {
                if (kv.Key <= 0) continue; // nút trung gian nội bộ
                Node n = p?.Nodes.FirstOrDefault(x => x.Id == kv.Key);
                var v = kv.Value;
                yield return new object[] { NodeStory(p, n), kv.Key.ToString(Inv), kv.Key, name, type, v[0], v[1], v[2], v[3], v[4], v[5] };
            }
        }
    }

    public static IEnumerable<object[]> LateralRows(AnalysisResults r)
    {
        foreach (var lf in r.LateralForces)
            yield return new object[] { lf.Pattern, lf.Story, lf.Elevation, lf.Fx, lf.Fy, lf.Note ?? "" };
    }

    // ------------------------------------------------------------------
    /// <summary>Thêm 4 trang: Element Forces - Frames, Joint Reactions, Joint Displacements, Story Forces.</summary>
    public static void AddToWorkbook(AnalysisResults r, XlsxWriter x)
    {
        if (r == null || x == null) return;
        AddFrameForces(r, x.AddSheet("Element Forces - Frames"));
        AddReactions(r, x.AddSheet("Joint Reactions"));
        AddDisplacements(r, x.AddSheet("Joint Displacements"));
        AddLateral(r, x.AddSheet("Story Forces"));
    }

    public static void AddFrameForces(AnalysisResults r, XlsxSheet sh)
    {
        sh.AddRow(FrameForceHeaders.Cast<object>().ToArray());
        foreach (var row in FrameForceRows(r)) sh.AddRow(row);
        sh.SetColumnWidths(10, 12, 12, 14, 14, 10, 12, 12, 12, 12, 12, 12);
        sh.NumberStyle = XlStyle.Number3;
    }

    public static void AddReactions(AnalysisResults r, XlsxSheet sh)
    {
        sh.AddRow(ReactionHeaders.Cast<object>().ToArray());
        foreach (var row in ReactionRows(r)) sh.AddRow(row);
        sh.SetColumnWidths(10, 10, 12, 14, 14, 12, 12, 12, 12, 12, 12);
        sh.NumberStyle = XlStyle.Number3;
    }

    public static void AddDisplacements(AnalysisResults r, XlsxSheet sh)
    {
        sh.AddRow(DisplacementHeaders.Cast<object>().ToArray());
        foreach (var row in DisplacementRows(r))
        {
            // chuyển vị nhỏ: giữ đủ chữ số – đổi sang mm cho Ux..Uz
            for (int i = 5; i <= 7; i++) row[i] = (double)row[i] * 1000.0;
            sh.AddRow(row);
        }
        sh.SetColumnWidths(10, 10, 12, 14, 14, 12, 12, 12, 14, 14, 14);
        sh.NumberStyle = XlStyle.Number3;
        sh.AddEmptyRow();
        sh.AddRow("Ghi chú: Ux, Uy, Uz đơn vị mm; Rx, Ry, Rz đơn vị rad.");
    }

    public static void AddLateral(AnalysisResults r, XlsxSheet sh)
    {
        sh.AddRow(LateralHeaders.Cast<object>().ToArray());
        foreach (var row in LateralRows(r)) sh.AddRow(row);
        sh.SetColumnWidths(14, 12, 12, 12, 12, 70);
    }

    // ------------------------------------------------------------------
    /// <summary>Ghi 4 file CSV (UTF-8 có BOM, số dạng invariant với dấu chấm thập phân) vào thư mục.</summary>
    public static void WriteCsv(AnalysisResults r, string folder)
    {
        if (r == null) return;
        Directory.CreateDirectory(folder);
        WriteCsvFile(Path.Combine(folder, "Element Forces - Frames.csv"), FrameForceHeaders, FrameForceRows(r));
        WriteCsvFile(Path.Combine(folder, "Joint Reactions.csv"), ReactionHeaders, ReactionRows(r));
        WriteCsvFile(Path.Combine(folder, "Joint Displacements.csv"), DisplacementHeaders, DisplacementRows(r));
        WriteCsvFile(Path.Combine(folder, "Story Forces.csv"), LateralHeaders, LateralRows(r));
    }

    public static void WriteCsvFile(string path, string[] headers, IEnumerable<object[]> rows)
    {
        using var w = new StreamWriter(path, false, new UTF8Encoding(true));
        w.WriteLine(string.Join(",", headers.Select(CsvField)));
        foreach (var row in rows)
            w.WriteLine(string.Join(",", row.Select(v => CsvField(Fmt(v)))));
    }

    private static string Fmt(object v) => v switch
    {
        null => "",
        double d => double.IsFinite(d) ? d.ToString("0.######", Inv) : "",
        float f => ((double)f).ToString("0.######", Inv),
        int i => i.ToString(Inv),
        long l => l.ToString(Inv),
        _ => Convert.ToString(v, Inv) ?? ""
    };

    public static string CsvField(string s)
    {
        s ??= "";
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0) return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}

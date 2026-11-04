using System.Globalization;
using System.Text;
using KetCauVXT.Model;

namespace KetCauVXT.Analysis;

/// <summary>Các bảng kết quả (Display → Show Tables của ETABS).</summary>
public enum ResultTableKind
{
    JointDisplacements, JointReactions, BeamForces, ColumnForces, StoryDrifts, ModalPeriods, BaseReactions
}

/// <summary>Bảng kết quả dạng dòng/cột (giá trị là string hoặc double).</summary>
public class ResultTable
{
    public ResultTableKind Kind { get; set; }
    public string Title { get; set; } = "";
    public List<string> Columns { get; } = new();
    public List<object[]> Rows { get; } = new();
    public string Note { get; set; } = "";

    public string ToCsv(char sep = ',')
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(sep, Columns.Select(c => Esc(c, sep))));
        foreach (var r in Rows)
            sb.AppendLine(string.Join(sep, r.Select(v => v is double d ? d.ToString("0.######", CultureInfo.InvariantCulture) : Esc(v?.ToString() ?? "", sep))));
        return sb.ToString();
    }

    private static string Esc(string s, char sep) =>
        s.IndexOfAny(new[] { sep, '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}

/// <summary>Lọc bảng kết quả.</summary>
public class ResultTableFilter
{
    /// <summary>Trường hợp tải / tổ hợp; null hoặc rỗng = mọi trường hợp tải + tổ hợp tuyến tính (tổ hợp bao chỉ khi chọn đích danh → Max/Min).</summary>
    public List<string> Cases { get; set; }
    /// <summary>Tầng; null/rỗng = mọi tầng.</summary>
    public string Story { get; set; }
}

/// <summary>Sinh bảng kết quả từ AnalysisResults: chuyển vị nút, phản lực, nội lực dầm/cột, chuyển vị lệch tầng, chu kỳ & tỷ lệ khối lượng,
/// phản lực đáy. Đơn vị: kN, m (chuyển vị mm, góc xoay rad).</summary>
public static class ResultTables
{
    public static readonly (ResultTableKind Kind, string Title)[] All =
    {
        (ResultTableKind.JointDisplacements, "Chuyển vị nút (Joint Displacements)"),
        (ResultTableKind.JointReactions, "Phản lực gối (Joint Reactions)"),
        (ResultTableKind.BeamForces, "Nội lực dầm (Element Forces – Beams)"),
        (ResultTableKind.ColumnForces, "Nội lực cột (Element Forces – Columns)"),
        (ResultTableKind.StoryDrifts, "Chuyển vị lệch tầng (Story Drifts)"),
        (ResultTableKind.ModalPeriods, "Chu kỳ & tỷ lệ khối lượng (Modal Periods & Mass Ratios)"),
        (ResultTableKind.BaseReactions, "Phản lực đáy (Base Reactions)")
    };

    public static string TitleOf(ResultTableKind k) => All.First(a => a.Kind == k).Title;

    /// <summary>Danh sách trường hợp mặc định: mọi trường hợp tải đã giải + tổ hợp tuyến tính.</summary>
    public static List<string> DefaultCases(AnalysisResults r)
    {
        var l = r.Cases.Keys.ToList();
        foreach (var c in r.Project.Combinations.Where(c => c.Type != ComboType.Envelope)) if (!l.Contains(c.Name)) l.Add(c.Name);
        foreach (var c in r.ExternalCombos.Where(c => c.Type != ComboType.Envelope)) if (!l.Contains(c.Name)) l.Add(c.Name);
        return l;
    }

    /// <summary>Mọi tên chọn được trong bộ lọc (kể cả tổ hợp bao).</summary>
    public static List<string> SelectableCases(AnalysisResults r)
    {
        var l = DefaultCases(r);
        foreach (var c in r.Project.Combinations.Concat(r.ExternalCombos).Where(c => c.Type == ComboType.Envelope)) if (!l.Contains(c.Name)) l.Add(c.Name);
        return l;
    }

    public static ResultTable Build(AnalysisResults r, ResultTableKind kind, ResultTableFilter f = null)
    {
        f ??= new ResultTableFilter();
        var p = r.Project;
        var t = new ResultTable { Kind = kind, Title = TitleOf(kind) };
        var cases = f.Cases != null && f.Cases.Count > 0 ? f.Cases : DefaultCases(r);
        bool InStory(string st) => string.IsNullOrEmpty(f.Story) || st == f.Story;
        var nodeStory = NodeStories(p);

        switch (kind)
        {
            case ResultTableKind.JointDisplacements:
                t.Columns.AddRange(new[] { "Tầng", "Nút", "Trường hợp", "Giá trị", "UX (mm)", "UY (mm)", "UZ (mm)", "RX (rad)", "RY (rad)", "RZ (rad)" });
                foreach (var c in cases)
                    foreach (var (kindName, get) in Variants(r, c, cr => cr.Disp))
                        foreach (var n in p.Nodes.OrderBy(n => n.Z).ThenBy(n => n.Id))
                        {
                            string st = nodeStory.GetValueOrDefault(n.Id, n.Story);
                            if (!InStory(st)) continue;
                            var d = get(n.Id);
                            if (d == null) continue;
                            t.Rows.Add(new object[] { st, n.Id, c, kindName, d[0] * 1000, d[1] * 1000, d[2] * 1000, d[3], d[4], d[5] });
                        }
                break;

            case ResultTableKind.JointReactions:
                t.Columns.AddRange(new[] { "Tầng", "Nút", "X (m)", "Y (m)", "Trường hợp", "Giá trị", "FX (kN)", "FY (kN)", "FZ (kN)", "MX (kN.m)", "MY (kN.m)", "MZ (kN.m)" });
                foreach (var c in cases)
                    foreach (var (kindName, get) in Variants(r, c, cr => cr.Reactions))
                        foreach (var n in p.Nodes.Where(n => n.HasRestraint).OrderBy(n => n.Id))
                        {
                            string st = nodeStory.GetValueOrDefault(n.Id, n.Story);
                            if (!InStory(st)) continue;
                            var v = get(n.Id);
                            if (v == null) continue;
                            t.Rows.Add(new object[] { st, n.Id, n.X, n.Y, c, kindName, v[0], v[1], v[2], v[3], v[4], v[5] });
                        }
                break;

            case ResultTableKind.BeamForces:
            case ResultTableKind.ColumnForces:
            {
                bool col = kind == ResultTableKind.ColumnForces;
                t.Columns.AddRange(new[] { "Tầng", "Thanh", "Tên", "Tiết diện", "Trường hợp", "Giá trị", "Vị trí (m)", "P (kN)", "V2 (kN)", "V3 (kN)", "T (kN.m)", "M2 (kN.m)", "M3 (kN.m)" });
                var frames = p.Frames.Where(x => x.IsVerticalKind == col && InStory(x.Story)).OrderBy(x => x.Story).ThenBy(x => x.Id).ToList();
                foreach (var c in cases)
                {
                    if (r.IsEnvelope(c))
                    {
                        var list = r.ExpandCombos(c);
                        foreach (var fr in frames)
                        {
                            var (mx, mn) = r.Envelope(fr.Id, list);
                            if (mx == null) continue;
                            for (int i = 0; i < mx.Count; i++) t.Rows.Add(ForceRow(fr, c, "Max", mx.Stations[i], mx.F[i]));
                            for (int i = 0; i < mn.Count; i++) t.Rows.Add(ForceRow(fr, c, "Min", mn.Stations[i], mn.F[i]));
                        }
                        continue;
                    }
                    var cr = SafeGet(r, c);
                    if (cr == null) continue;
                    foreach (var fr in frames)
                    {
                        if (!cr.Frames.TryGetValue(fr.Id, out var ff) || ff == null) continue;
                        for (int i = 0; i < ff.Count; i++) t.Rows.Add(ForceRow(fr, c, "", ff.Stations[i], ff.F[i]));
                    }
                }
                break;
            }

            case ResultTableKind.StoryDrifts:
            {
                t.Columns.AddRange(new[] { "Tầng", "Cao độ (m)", "h (m)", "Trường hợp", "Phương", "Δ lệch tầng max (mm)", "Tỷ số Δ/h", "h/Δ", "Cột" });
                var stories = p.Stories.OrderBy(s => s.Elevation).ToList();
                foreach (var c in cases)
                {
                    if (r.IsEnvelope(c)) continue;
                    var cr = SafeGet(r, c);
                    if (cr == null) continue;
                    foreach (var s in stories)
                    {
                        if (!InStory(s.Name)) continue;
                        var cols = p.Frames.Where(x => x.IsVerticalKind && x.Story == s.Name).ToList();
                        if (cols.Count == 0) continue;
                        for (int d = 0; d < 2; d++)
                        {
                            double best = 0, hBest = s.Height; string at = "";
                            foreach (var cf in cols)
                            {
                                var a = p.Nodes.FirstOrDefault(n => n.Id == cf.NodeI); var b = p.Nodes.FirstOrDefault(n => n.Id == cf.NodeJ);
                                if (a == null || b == null) continue;
                                if (a.Z > b.Z) (a, b) = (b, a);
                                cr.Disp.TryGetValue(a.Id, out var ua); cr.Disp.TryGetValue(b.Id, out var ub);
                                double h = b.Z - a.Z;
                                if (h < 1e-6) continue;
                                double drift = Math.Abs((ub?[d] ?? 0) - (ua?[d] ?? 0));
                                if (drift / h > best / Math.Max(hBest, 1e-9) || at == "") { best = drift; hBest = h; at = cf.Label; }
                            }
                            double ratio = hBest > 1e-9 ? best / hBest : 0;
                            t.Rows.Add(new object[] { s.Name, s.Elevation, hBest, c, d == 0 ? "X" : "Y", best * 1000, ratio, ratio > 1e-12 ? 1 / ratio : 0.0, at });
                        }
                    }
                }
                break;
            }

            case ResultTableKind.ModalPeriods:
            {
                t.Columns.AddRange(new[] { "Dạng", "T (s)", "f (Hz)", "UX", "UY", "UZ", "RZ", "ΣUX", "ΣUY", "ΣUZ", "ΣRZ", "Phương" });
                var m = r.Modal;
                if (m == null || m.Modes.Count == 0) { t.Note = "Chưa có kết quả dao động riêng (Khai báo → Nguồn khối lượng & dao động riêng…)."; break; }
                foreach (var md in m.Modes)
                    t.Rows.Add(new object[] { md.Index, md.Period, md.Frequency, md.Ratio[0], md.Ratio[1], md.Ratio[2], md.Ratio[3],
                                              md.Cumulative[0], md.Cumulative[1], md.Cumulative[2], md.Cumulative[3], md.Dominant });
                break;
            }

            case ResultTableKind.BaseReactions:
            {
                t.Columns.AddRange(new[] { "Trường hợp", "Giá trị", "FX (kN)", "FY (kN)", "FZ (kN)", "MX (kN.m)", "MY (kN.m)", "MZ (kN.m)" });
                t.Note = "Tổng phản lực gối; mô men lấy với gốc toạ độ (0; 0; cao độ chân cột).";
                double z0 = p.BaseElevation;
                foreach (var c in cases)
                {
                    if (r.IsEnvelope(c))
                    {
                        double[] mx = null, mn = null;
                        foreach (var name in r.ExpandCombos(c))
                        {
                            var cr = SafeGet(r, name);
                            if (cr == null) continue;
                            var v = BaseSum(p, cr, z0);
                            if (mx == null) { mx = (double[])v.Clone(); mn = (double[])v.Clone(); continue; }
                            for (int k = 0; k < 6; k++) { mx[k] = Math.Max(mx[k], v[k]); mn[k] = Math.Min(mn[k], v[k]); }
                        }
                        if (mx != null) { t.Rows.Add(Row6(c, "Max", mx)); t.Rows.Add(Row6(c, "Min", mn)); }
                        continue;
                    }
                    var res = SafeGet(r, c);
                    if (res == null) continue;
                    t.Rows.Add(Row6(c, "", BaseSum(p, res, z0)));
                }
                break;
            }
        }
        return t;
    }

    /// <summary>Tổng phản lực {FX, FY, FZ, MX, MY, MZ} quanh điểm (0, 0, z0).</summary>
    public static double[] BaseSum(Project p, CaseResult cr, double z0)
    {
        var s = new double[6];
        foreach (var kv in cr.Reactions)
        {
            var n = p.Nodes.FirstOrDefault(q => q.Id == kv.Key);
            if (n == null) continue;
            var v = kv.Value;
            for (int k = 0; k < 6; k++) s[k] += v[k];
            double x = n.X, y = n.Y, z = n.Z - z0;
            // r × F
            s[3] += y * v[2] - z * v[1];
            s[4] += z * v[0] - x * v[2];
            s[5] += x * v[1] - y * v[0];
        }
        return s;
    }

    private static object[] Row6(string c, string k, double[] v) => new object[] { c, k, v[0], v[1], v[2], v[3], v[4], v[5] };

    private static object[] ForceRow(FrameElement fr, string c, string k, double x, double[] v) =>
        new object[] { fr.Story, fr.Id, fr.Label, fr.Section, c, k, x, v[0], v[1], v[2], v[3], v[4], v[5] };

    private static CaseResult SafeGet(AnalysisResults r, string c)
    {
        try { return r.Get(c); } catch { return null; }
    }

    /// <summary>Kết quả theo nút của một trường hợp: thường → một biến thể ""; tổ hợp bao → "Max", "Min".</summary>
    private static List<(string, Func<int, double[]>)> Variants(AnalysisResults r, string c, Func<CaseResult, Dictionary<int, double[]>> pick)
    {
        var l = new List<(string, Func<int, double[]>)>();
        if (r.IsEnvelope(c))
        {
            var dicts = r.ExpandCombos(c).Select(n => SafeGet(r, n)).Where(x => x != null).Select(pick).ToList();
            double[] Ext(int id, bool max)
            {
                double[] acc = null;
                foreach (var d in dicts)
                {
                    if (!d.TryGetValue(id, out var v)) continue;
                    if (acc == null) { acc = (double[])v.Clone(); continue; }
                    for (int k = 0; k < 6; k++) acc[k] = max ? Math.Max(acc[k], v[k]) : Math.Min(acc[k], v[k]);
                }
                return acc;
            }
            l.Add(("Max", id => Ext(id, true)));
            l.Add(("Min", id => Ext(id, false)));
            return l;
        }
        var cr = SafeGet(r, c);
        if (cr == null) return l;
        var dd = pick(cr);
        l.Add(("", id => dd.TryGetValue(id, out var v) ? v : null));
        return l;
    }

    /// <summary>Tầng của nút theo cao độ (nút không ghi tầng → tầng có cùng cao độ; dưới tầng thấp nhất → "Chân cột").</summary>
    public static Dictionary<int, string> NodeStories(Project p)
    {
        var d = new Dictionary<int, string>();
        foreach (var n in p.Nodes)
        {
            var st = p.Stories.FirstOrDefault(s => Math.Abs(s.Elevation - n.Z) < 1e-3);
            d[n.Id] = st?.Name ?? (string.IsNullOrEmpty(n.Story) ? (Math.Abs(n.Z - p.BaseElevation) < 1e-3 ? "Chân cột" : "") : n.Story);
        }
        return d;
    }
}

using System.Globalization;
using System.Text;
using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.Export;

/// <summary>Ghi kết quả phân tích của chương trình thành bảng CSV đúng mẫu "Export tables" của ETABS hoặc SAP2000
/// (dòng "TABLE:  tên", dòng tiêu đề, dòng đơn vị, dữ liệu) – dùng kiểm thử bộ nhập kết quả và trao đổi với phần mềm khác.
/// Kiểu ETABS: tên thanh theo E2kExporter (C{Id}, B{Id}, D{Id}) + tầng; cột/thanh xiên ghi theo quy ước ETABS (đầu I ở dưới):
/// trạm đo từ đầu dưới, nội lực đổi sang hệ trục của thanh ngược chiều. Kiểu SAP2000: Frame = Id, Joint = Id nút.</summary>
public static class ResultTableWriter
{
    public class Options
    {
        /// <summary>true = ETABS ("Element Forces - Beams/Columns/Braces"), false = SAP2000 ("Element Forces - Frames").</summary>
        public bool Etabs { get; set; } = true;
        /// <summary>Đơn vị lực / dài ghi ra (vd. "kN","m" hoặc "Tonf","m" hoặc "kgf","cm").</summary>
        public string ForceUnit { get; set; } = "kN";
        public string LengthUnit { get; set; } = "m";
        /// <summary>Tên trường hợp/tổ hợp ghi ra; null = mọi trường hợp tải + tổ hợp tuyến tính của dự án.</summary>
        public List<string> Cases { get; set; }
        /// <summary>Ghi thêm tổ hợp bao (dòng Step Type = Max/Min).</summary>
        public bool Envelopes { get; set; } = true;
        public bool Reactions { get; set; } = true;
        public bool Displacements { get; set; } = true;
        /// <summary>Ghi bảng toạ độ nút + nối thanh (để khớp thanh theo toạ độ).</summary>
        public bool Geometry { get; set; }
        /// <summary>Đặt tên thanh / nút tuỳ ý (kiểm thử khớp theo toạ độ); null = tên mặc định.</summary>
        public Func<FrameElement, string> FrameName { get; set; }
        public Func<Node, string> JointName { get; set; }
        /// <summary>Bỏ dòng đơn vị (để kiểm thử đơn vị mặc định).</summary>
        public bool NoUnitsRow { get; set; }
    }

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Write(AnalysisResults r, string path, Options o = null)
    {
        o ??= new Options();
        File.WriteAllText(path, Build(r, o), new UTF8Encoding(true));
    }

    /// <summary>Tên tầng ETABS của cao độ z – cùng quy tắc với E2kExporter: tầng dự án, "Base", hoặc "Z{mm}".</summary>
    public static string EtabsStory(Project p, double z)
    {
        if (Math.Abs(z - p.BaseElevation) < 1e-3) return "Base";
        var st = p.Stories.FirstOrDefault(s => Math.Abs(s.Elevation - z) < 1e-3);
        return st != null ? st.Name : $"Z{z * 1000:0}";
    }

    public static string Build(AnalysisResults r, Options o)
    {
        var p = r.Project;
        p.InvalidateIndex();
        ToFactor(o.ForceUnit, out double fF); ToFactor(o.LengthUnit, out double fL);
        var pts = EtabsPoints(p);
        string F(double v) => v.ToString("0.#######", Inv);
        string fu = o.ForceUnit, lu = o.LengthUnit, mu = o.ForceUnit + "-" + o.LengthUnit;
        var sb = new StringBuilder();
        void Row(IEnumerable<string> cells) => sb.Append(string.Join(",", cells.Select(Csv))).Append("\r\n");

        // ---- danh sách trường hợp: (tên ghi ra, loại, step, kết quả) ----
        var list = new List<(string name, string type, string step, CaseResult cr)>();
        var names = o.Cases ?? p.Patterns.Select(x => x.Name).Concat(p.Combinations.Where(c => c.Type != ComboType.Envelope).Select(c => c.Name)).ToList();
        foreach (var n in names)
        {
            CaseResult cr;
            try { cr = r.Get(n); } catch (Exception) { continue; }
            list.Add((n, r.Cases.ContainsKey(n) && p.Pattern(n) != null ? "LinStatic" : "Combination", "", cr));
        }
        var envs = new List<(string name, List<string> items)>();
        if (o.Envelopes)
            foreach (var c in p.Combinations.Where(c => c.Type == ComboType.Envelope))
                envs.Add((c.Name, r.ExpandCombos(c.Name)));

        // ---- nội lực thanh ----
        var groups = o.Etabs
            ? new[] { ("Element Forces - Beams", "Beam", 'B'), ("Element Forces - Columns", "Column", 'C'), ("Element Forces - Braces", "Brace", 'D') }
            : new[] { ("Element Forces - Frames", "Frame", '*') };
        int unique = 1;
        var uniq = p.Frames.ToDictionary(f => f.Id, f => (unique++).ToString(Inv));
        foreach (var (title, labelCol, kind) in groups)
        {
            var frames = p.Frames.Where(f => kind == '*' || E2kExporter.LineName(f, p)[0] == kind).ToList();
            if (frames.Count == 0) continue;
            sb.Append("TABLE:  ").Append(title).Append("\r\n");
            if (o.Etabs)
            {
                Row(new[] { "Story", labelCol, "Unique Name", "Output Case", "Case Type", "Step Type", "Station", "P", "V2", "V3", "T", "M2", "M3" });
                if (!o.NoUnitsRow) Row(new[] { "", "", "", "", "", "", lu, fu, fu, fu, mu, mu, mu });
            }
            else
            {
                Row(new[] { "Frame", "Station", "OutputCase", "CaseType", "StepType", "P", "V2", "V3", "T", "M2", "M3" });
                if (!o.NoUnitsRow) Row(new[] { "", lu, "", "", "", fu, fu, fu, mu, mu, mu });
            }
            void Emit(FrameElement f, string cname, string ctype, string step, FrameForces ff)
            {
                var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
                double L = p.Length(f);
                bool flip = o.Etabs && E2kExporter.LineName(f, p)[0] != 'B' && a.Z > b.Z;
                var rOur = ElementMatrices.LocalAxes(a.P, b.P, f.Angle);
                var rRev = ElementMatrices.LocalAxes(b.P, a.P, f.Angle);
                string story = EtabsStory(p, Math.Max(a.Z, b.Z));
                string label = o.FrameName?.Invoke(f) ?? (o.Etabs ? E2kExporter.LineName(f, p) : f.Id.ToString(Inv));
                var idx = Enumerable.Range(0, ff.Count);
                if (flip) idx = idx.Reverse();
                foreach (int i in idx)
                {
                    double s = flip ? L - ff.Stations[i] : ff.Stations[i];
                    // thanh ghi theo chiều ngược: nội lực trên mặt dương của thanh ngược = −R'·Rᵀ·f (cùng công thức với bộ nhập)
                    var v = flip ? KetCauVXT.Import.Csi.ResultImportSession.Reverse(ff.F[i], rRev, rOur) : ff.F[i];
                    var cells = new List<string>();
                    if (o.Etabs) cells.AddRange(new[] { story, label, uniq[f.Id], cname, ctype, step });
                    else cells.AddRange(new[] { label });
                    cells.Add(F(s / fL));
                    if (!o.Etabs) cells.AddRange(new[] { cname, ctype, step });
                    for (int k = 0; k < 6; k++) cells.Add(F(v[k] / (k < 3 ? fF : fF * fL)));
                    Row(cells);
                }
            }
            foreach (var f in frames)
            {
                foreach (var (n, ty, st, cr) in list)
                    if (cr.Frames.TryGetValue(f.Id, out var ff)) Emit(f, n, ty, st, ff);
                foreach (var (en, items) in envs)
                {
                    var (mx, mn) = r.Envelope(f.Id, items);
                    if (mx == null) continue;
                    Emit(f, en, "Combination", "Max", mx);
                    Emit(f, en, "Combination", "Min", mn);
                }
            }
            sb.Append("\r\n");
        }

        // ---- nút ----
        string JName(Node n) => o.JointName?.Invoke(n) ?? (o.Etabs ? pts[n.Id].ToString(Inv) : n.Id.ToString(Inv));
        void JointTable(string title, bool reac)
        {
            sb.Append("TABLE:  ").Append(title).Append("\r\n");
            var h = reac ? new[] { "FX", "FY", "FZ", "MX", "MY", "MZ" } : new[] { "UX", "UY", "UZ", "RX", "RY", "RZ" };
            if (!o.Etabs) h = reac ? new[] { "F1", "F2", "F3", "M1", "M2", "M3" } : new[] { "U1", "U2", "U3", "R1", "R2", "R3" };
            var u = reac ? new[] { fu, fu, fu, mu, mu, mu } : new[] { lu, lu, lu, "Radians", "Radians", "Radians" };
            if (o.Etabs)
            {
                Row(new[] { "Story", "Label", "Unique Name", "Output Case", "Case Type", "Step Type" }.Concat(h));
                if (!o.NoUnitsRow) Row(new[] { "", "", "", "", "", "" }.Concat(u));
            }
            else
            {
                Row(new[] { "Joint", "OutputCase", "CaseType", "StepType" }.Concat(h));
                if (!o.NoUnitsRow) Row(new[] { "", "", "", "" }.Concat(u));
            }
            void EmitJ(Node n, string cname, string ctype, string step, double[] v)
            {
                var cells = new List<string>();
                if (o.Etabs) cells.AddRange(new[] { EtabsStory(p, n.Z), JName(n), "J" + n.Id.ToString(Inv), cname, ctype, step });
                else cells.AddRange(new[] { JName(n), cname, ctype, step });
                for (int k = 0; k < 6; k++) cells.Add(F(v[k] / (reac ? (k < 3 ? fF : fF * fL) : (k < 3 ? fL : 1))));
                Row(cells);
            }
            foreach (var n in p.Nodes)
            {
                foreach (var (cn, ty, st, cr) in list)
                {
                    var d = reac ? cr.Reactions : cr.Disp;
                    if (d.TryGetValue(n.Id, out var v)) EmitJ(n, cn, ty, st, v);
                }
            }
            sb.Append("\r\n");
        }
        if (o.Reactions) JointTable("Joint Reactions", true);
        if (o.Displacements) JointTable("Joint Displacements", false);

        if (o.Geometry)
        {
            sb.Append("TABLE:  ").Append(o.Etabs ? "Point Object Connectivity" : "Joint Coordinates").Append("\r\n");
            if (o.Etabs) { Row(new[] { "Unique Name", "Story", "Label", "X", "Y", "Z" }); if (!o.NoUnitsRow) Row(new[] { "", "", "", lu, lu, lu }); }
            else { Row(new[] { "Joint", "CoordSys", "CoordType", "XorR", "Y", "Z" }); if (!o.NoUnitsRow) Row(new[] { "", "", "", lu, lu, lu }); }
            foreach (var n in p.Nodes)
                Row(o.Etabs ? new[] { "J" + n.Id.ToString(Inv), EtabsStory(p, n.Z), JName(n), F(n.X / fL), F(n.Y / fL), F(n.Z / fL) }
                            : new[] { JName(n), "GLOBAL", "Cartesian", F(n.X / fL), F(n.Y / fL), F(n.Z / fL) });
            sb.Append("\r\n");
            sb.Append("TABLE:  ").Append(o.Etabs ? "Frame Object Connectivity" : "Connectivity - Frame").Append("\r\n");
            if (o.Etabs) Row(new[] { "Unique Name", "Story", "Label", "UniquePtI", "UniquePtJ" });
            else Row(new[] { "Frame", "JointI", "JointJ" });
            foreach (var f in p.Frames)
            {
                var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
                bool flip = o.Etabs && E2kExporter.LineName(f, p)[0] != 'B' && a.Z > b.Z;
                var (ni, nj) = flip ? (b, a) : (a, b);
                string label = o.FrameName?.Invoke(f) ?? (o.Etabs ? E2kExporter.LineName(f, p) : f.Id.ToString(Inv));
                Row(o.Etabs ? new[] { uniq[f.Id], EtabsStory(p, Math.Max(a.Z, b.Z)), label, "J" + ni.Id.ToString(Inv), "J" + nj.Id.ToString(Inv) }
                            : new[] { label, JName(ni), JName(nj) });
            }
            sb.Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>Số hiệu điểm mặt bằng của từng nút như E2kExporter (thứ tự xuất hiện của toạ độ (x, y)).</summary>
    public static Dictionary<int, int> EtabsPoints(Project p)
    {
        var seen = new Dictionary<(double, double), int>();
        var res = new Dictionary<int, int>();
        foreach (var q in p.Nodes)
        {
            var k = (Math.Round(q.X * 1000), Math.Round(q.Y * 1000));
            if (!seen.TryGetValue(k, out int v)) seen[k] = v = seen.Count + 1;
            res[q.Id] = v;
        }
        return res;
    }

    private static void ToFactor(string u, out double f)
    {
        if (!KetCauVXT.Import.Csi.UnitConv.TryFactor(u, out f)) f = 1;
    }

    private static string Csv(string s)
    {
        s ??= "";
        return s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}

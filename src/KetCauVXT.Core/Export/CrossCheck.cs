using System.Globalization;
using System.Text.RegularExpressions;
using KetCauVXT.Analysis;
using KetCauVXT.Import.Csi;
using KetCauVXT.Model;

namespace KetCauVXT.Export;

/// <summary>Mức đánh giá sai lệch khi so sánh với ETABS/SAP2000/SAFE.</summary>
public enum CrossLevel
{
    /// <summary>≤ ngưỡng cảnh báo (mặc định 5 %)</summary>
    Ok,
    /// <summary>ngưỡng cảnh báo &lt; sai lệch ≤ ngưỡng không đạt (5…10 %)</summary>
    Warn,
    /// <summary>&gt; ngưỡng không đạt (mặc định 10 %)</summary>
    Fail,
    /// <summary>thiếu số liệu một phía</summary>
    NoData
}

/// <summary>Tuỳ chọn so sánh kết quả.</summary>
public class CrossCheckOptions
{
    /// <summary>Ngưỡng cảnh báo (%).</summary>
    public double WarnPercent { get; set; } = 5;
    /// <summary>Ngưỡng không đạt (%).</summary>
    public double FailPercent { get; set; } = 10;
    /// <summary>Sàn giá trị nhỏ: mẫu số % không nhỏ hơn FloorRatio × giá trị lớn nhất cùng loại (lực / mô men) của cấu kiện.</summary>
    public double FloorRatio { get; set; } = 0.05;
    /// <summary>Sàn tuyệt đối cho lực (kN) và mô men (kN.m) – tránh % rất lớn khi nội lực gần 0.</summary>
    public double ForceFloor { get; set; } = 1.0;
    public double MomentFloor { get; set; } = 1.0;
    /// <summary>Sàn tuyệt đối cho chuyển vị (m) và chuyển vị lệch tầng (tỷ số).</summary>
    public double DispFloor { get; set; } = 0.0005;
    public double DriftFloor { get; set; } = 1e-5;
    /// <summary>Số cấu kiện sai lệch lớn nhất liệt kê trong báo cáo.</summary>
    public int WorstCount { get; set; } = 20;
    /// <summary>Dung sai vị trí khi so sánh tại bước nhảy lực cắt (tỷ lệ chiều dài thanh).</summary>
    public double JumpTolerance { get; set; } = 0.002;

    public CrossCheckOptions Clone() => (CrossCheckOptions)MemberwiseClone();

    public CrossLevel Classify(double percent) =>
        double.IsNaN(percent) ? CrossLevel.NoData : percent > FailPercent + 1e-9 ? CrossLevel.Fail : percent > WarnPercent + 1e-9 ? CrossLevel.Warn : CrossLevel.Ok;
}

/// <summary>Cặp trường hợp/tổ hợp: tên trong file ngoài (có thể kèm " (Max)"/" (Min)") ↔ trường hợp/tổ hợp của chương trình.</summary>
public class CrossCasePair
{
    public string External { get; set; } = "";
    /// <summary>Tên trường hợp tải / tổ hợp của dự án; rỗng = không so sánh.</summary>
    public string Ours { get; set; } = "";
    public bool Enabled { get; set; } = true;
    /// <summary>Cách ghép: "trùng tên", "trùng tên (không phân biệt hoa thường, dấu)", "người dùng chọn", "không tìm thấy".</summary>
    public string Method { get; set; } = "";
    public string Step => External.EndsWith(" (Max)") ? "Max" : External.EndsWith(" (Min)") ? "Min" : "";
    public string ExternalBase => Step.Length > 0 ? External.Substring(0, External.Length - 6) : External;
    public bool Active => Enabled && !string.IsNullOrEmpty(Ours);
    public override string ToString() => External + " ↔ " + (string.IsNullOrEmpty(Ours) ? "—" : Ours);
}

/// <summary>So sánh nội lực một thanh trong một cặp trường hợp. Thành phần k = 0…5: P, V2, V3, T, M2, M3.</summary>
public class CrossMemberCase
{
    public int FrameId { get; set; }
    public string Label { get; set; } = "";
    public string Story { get; set; } = "";
    public string Kind { get; set; } = "";
    public string ExternalName { get; set; } = "";
    public string ExternalCase { get; set; } = "";
    public string OurCase { get; set; } = "";
    /// <summary>max |giá trị| trên các trạm của chương trình / phần mềm ngoài.</summary>
    public double[] OurMax { get; set; } = new double[6];
    public double[] ExtMax { get; set; } = new double[6];
    /// <summary>Sai lệch tuyệt đối lớn nhất tại các trạm chung (kN, kN.m).</summary>
    public double[] Diff { get; set; } = new double[6];
    /// <summary>Sai lệch % = Diff / max(max|.| hai phía, sàn giá trị nhỏ).</summary>
    public double[] Percent { get; set; } = new double[6];
    public double WorstPercent { get; set; }
    public string WorstComponent { get; set; } = "";
    public CrossLevel Level { get; set; }
    public int Stations { get; set; }
    /// <summary>Nội lực hai phía (hệ trục, chiều thanh của dự án) – vẽ biểu đồ.</summary>
    public FrameForces OurForces { get; set; }
    public FrameForces ExtForces { get; set; }
}

/// <summary>Tổng hợp một thanh qua mọi cặp trường hợp.</summary>
public class CrossMember
{
    public int FrameId { get; set; }
    public string Label { get; set; } = "";
    public string Story { get; set; } = "";
    public string Kind { get; set; } = "";
    public string ExternalName { get; set; } = "";
    public double WorstPercent { get; set; } = double.NaN;
    public string WorstCase { get; set; } = "";
    public string WorstComponent { get; set; } = "";
    public CrossLevel Level { get; set; } = CrossLevel.NoData;
    /// <summary>|M3| lớn nhất hai phía (qua các cặp) và sai lệch % lớn nhất của M3, V2, P.</summary>
    public double OurM3 { get; set; }
    public double ExtM3 { get; set; }
    public double M3Percent { get; set; }
    public double V2Percent { get; set; }
    public double PPercent { get; set; }
    public int CaseCount { get; set; }
    public List<CrossMemberCase> Cases { get; } = new();
}

/// <summary>Phản lực một gối trong một cặp trường hợp (FX, FY, FZ, MX, MY, MZ – hệ toàn cục).</summary>
public class CrossReaction
{
    public int NodeId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public string ExternalCase { get; set; } = "";
    public string OurCase { get; set; } = "";
    public double[] Ours { get; set; } = new double[6];
    public double[] Ext { get; set; } = new double[6];
    public double[] Percent { get; set; } = new double[6];
    public double WorstPercent { get; set; }
    public string WorstComponent { get; set; } = "";
    public CrossLevel Level { get; set; }
    public string Note { get; set; } = "";
}

/// <summary>Tổng phản lực chân công trình (Base Reactions) một cặp trường hợp; mô men lấy quanh gốc toạ độ (hoặc điểm tham chiếu của bảng).</summary>
public class CrossBaseReaction
{
    public string ExternalCase { get; set; } = "";
    public string OurCase { get; set; } = "";
    public double[] Ours { get; set; } = new double[6];
    public double[] Ext { get; set; } = new double[6];
    public double[] Percent { get; set; } = new double[6];
    public double WorstPercent { get; set; }
    public CrossLevel Level { get; set; }
    /// <summary>"Base Reactions" hoặc "tổng phản lực nút".</summary>
    public string Source { get; set; } = "";
}

/// <summary>Chuyển vị lớn nhất (m) hoặc chuyển vị lệch tầng (tỷ số) của một tầng.</summary>
public class CrossStory
{
    /// <summary>"Chuyển vị" hoặc "Lệch tầng".</summary>
    public string Quantity { get; set; } = "";
    public string Story { get; set; } = "";
    public double Elevation { get; set; }
    public string ExternalCase { get; set; } = "";
    public string OurCase { get; set; } = "";
    public string Direction { get; set; } = "";
    public double Ours { get; set; }
    public double Ext { get; set; }
    public double Diff { get; set; }
    public double Percent { get; set; }
    public CrossLevel Level { get; set; }
    public bool Top { get; set; }
}

/// <summary>Chu kỳ dao động riêng.</summary>
public class CrossModal
{
    public int Mode { get; set; }
    public double ExtPeriod { get; set; }
    /// <summary>Chu kỳ của chương trình; NaN = không có (chỉ có ước lượng T1 = Ct·H^0,75 cho mode 1).</summary>
    public double OurPeriod { get; set; } = double.NaN;
    public double Percent { get; set; } = double.NaN;
    public string Note { get; set; } = "";
}

/// <summary>Thống kê một nhóm đại lượng.</summary>
public class CrossStats
{
    public string Group { get; set; } = "";
    public int Count { get; set; }
    public int Ok { get; set; }
    public int Warn { get; set; }
    public int Fail { get; set; }
    public int NoData { get; set; }
    public double Mean { get; set; }
    public double Max { get; set; }
    public string MaxAt { get; set; } = "";
}

/// <summary>Kết quả so sánh kết quả phân tích của chương trình với ETABS / SAP2000 / SAFE.</summary>
public class CrossCheckReport
{
    public string ProjectName { get; set; } = "";
    public string Software { get; set; } = "";
    public List<string> Files { get; } = new();
    public string Units { get; set; } = "";
    public DateTime Created { get; set; } = DateTime.Now;
    public CrossCheckOptions Options { get; set; } = new();
    public List<CrossCasePair> Pairs { get; } = new();
    public List<CrossMember> Members { get; } = new();
    public List<CrossMemberCase> MemberCases { get; } = new();
    public List<CrossReaction> Reactions { get; } = new();
    public List<CrossBaseReaction> BaseReactions { get; } = new();
    public List<CrossStory> Stories { get; } = new();
    public List<CrossModal> Modal { get; } = new();
    /// <summary>Thanh trong file ngoài không khớp được với dự án.</summary>
    public List<FrameMatchInfo> UnmatchedExternal { get; } = new();
    /// <summary>Thanh của dự án không có số liệu trong file ngoài.</summary>
    public List<int> FramesWithoutData { get; } = new();
    public List<string> Messages { get; } = new();
    /// <summary>Gợi ý nguyên nhân khi sai lệch lớn (theo dấu hiệu nhận được).</summary>
    public List<string> Hints { get; } = new();
    public List<CrossStats> Stats { get; } = new();

    public CrossStats Stat(string group) => Stats.FirstOrDefault(s => s.Group == group);
    public IEnumerable<CrossMember> Worst => Members.Where(m => !double.IsNaN(m.WorstPercent)).OrderByDescending(m => m.WorstPercent).Take(Math.Max(1, Options.WorstCount));

    /// <summary>Đánh giá chung: mức cao nhất trong các nhóm thanh, phản lực, tổng phản lực, chuyển vị.</summary>
    public CrossLevel Overall
    {
        get
        {
            var all = Stats.Where(s => s.Count > 0).ToList();
            if (all.Count == 0) return CrossLevel.NoData;
            if (all.Any(s => s.Fail > 0)) return CrossLevel.Fail;
            if (all.Any(s => s.Warn > 0)) return CrossLevel.Warn;
            return CrossLevel.Ok;
        }
    }

    public static string LevelText(CrossLevel l) => l switch
    {
        CrossLevel.Ok => "Phù hợp",
        CrossLevel.Warn => "Cảnh báo",
        CrossLevel.Fail => "Sai lệch lớn",
        _ => "Thiếu số liệu"
    };

    /// <summary>Hướng dẫn chung về các nguyên nhân sai lệch thường gặp (ghi vào báo cáo).</summary>
    public static readonly string[] Guidance =
    {
        "Sàn cứng tuyệt đối (rigid diaphragm): Kết cấu VXT gán sàn cứng bằng ràng buộc nút chủ – nút phụ khi bật tuỳ chọn; ETABS gán diaphragm cho sàn/điểm. Khác nhau về diaphragm (có/không, cứng/bán cứng) làm lực dọc dầm và phân phối lực ngang giữa các khung khác nhau.",
        "Đoạn cứng đầu thanh (end offsets / rigid zone factor): ETABS mặc định tự tính chiều dài đoạn cứng tại nút giao và có thể dùng hệ số vùng cứng; chương trình tính theo trục thanh. Mô men tại mép gối dầm và mô men đầu cột sẽ khác.",
        "Mô hình sàn: chương trình dồn tải sàn về dầm theo đường phân giác 45° (hình thang/tam giác); ETABS chia lưới phần tử vỏ (shell/membrane) – sàn shell tham gia chịu uốn cùng dầm, giảm mô men dầm và thay đổi phân phối tải giữa dầm biên/dầm giữa.",
        "Hệ số giảm độ cứng tiết diện (property modifiers): ETABS thường gán 0,35·I cho dầm, 0,7·I cho cột, 0,25 cho sàn… Chuyển vị và nội lực do tải ngang thay đổi rõ, nội lực do tải đứng thay đổi ít hơn.",
        "Trọng lượng bản thân (self-weight multiplier) và khối lượng riêng vật liệu: kiểm tra hệ số SELFWEIGHT của trường hợp TT trong ETABS, khối lượng riêng bê tông (25 kN/m³), trọng lượng sàn có bị kể hai lần (vừa tự trọng shell vừa tải phân bố) hay không.",
        "Hiệu ứng P-Delta và phân tích phi tuyến: nếu ETABS bật P-Delta, chuyển vị và mô men cột tầng dưới do tải ngang lớn hơn kết quả tuyến tính của chương trình.",
        "Chia nhỏ phần tử (auto mesh / auto frame subdivision) và trạm xuất kết quả (output stations): ETABS chỉ xuất nội lực tại các trạm đã chọn, giá trị cực trị giữa các trạm có thể bị bỏ sót.",
        "Tải gió/động đất: chương trình sinh lực ngang tầng theo TCVN 2737:2023 / TCVN 9386:2012 đặt tại tâm khối lượng; ETABS có thể dùng tải tự động theo tiêu chuẩn khác, độ lệch tâm ngẫu nhiên 5 %, hoặc phân tích phổ phản ứng (giá trị tuyệt đối, mất dấu).",
        "Tổ hợp tải: kiểm tra hệ số tổ hợp, tổ hợp bao (Envelope) so với tổ hợp tuyến tính, và việc ghép đúng tên trường hợp/tổ hợp giữa hai phần mềm."
    };
}

/// <summary>So sánh kết quả phân tích của Kết cấu VXT với bảng kết quả xuất từ ETABS / SAP2000 / SAFE (mô hình xuất bằng .e2k/.s2k của chương trình).
/// Đọc và khớp bảng bằng <see cref="ResultImportSession"/> (nhận diện bảng, đơn vị, quy ước dấu, khớp thanh theo tên/toạ độ) –
/// không thay kết quả hiện hành mà đối chiếu với <see cref="AnalysisResults"/> của chương trình: nội lực thanh tại các trạm chung,
/// phản lực gối, tổng phản lực, chuyển vị và chuyển vị lệch tầng, chu kỳ dao động (nếu có).</summary>
public class CrossCheckSession
{
    public Project Project { get; }
    /// <summary>Kết quả phân tích của chương trình (không phải kết quả nhập).</summary>
    public AnalysisResults Ours { get; }
    public ResultImportSession Import { get; private set; }
    public CrossCheckOptions Options { get; set; } = new();
    public List<CrossCasePair> Pairs { get; } = new();
    /// <summary>Kết quả ngoài đã dựng (theo phân loại hiện tại); null = chưa dựng.</summary>
    public AnalysisResults External { get; private set; }

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public CrossCheckSession(Project p, AnalysisResults ours)
    {
        Project = p ?? throw new ArgumentNullException(nameof(p));
        Ours = ours ?? throw new ArgumentNullException(nameof(ours));
        if (ours.External != null) throw new InvalidOperationException("Kết quả hiện hành là kết quả NHẬP từ " + ours.External.Software + " – cần chạy phân tích của chương trình trước khi so sánh.");
        Reset();
    }

    public void Reset()
    {
        // Own = null và không bù thanh thiếu: chỉ lấy đúng số liệu có trong file ngoài
        Import = new ResultImportSession(Project, null);
        Import.Options.FillUnmatchedFromOwn = false;
        Pairs.Clear();
        External = null;
    }

    public void AddFile(string path) { Import.AddFile(path); External = null; }

    public void AddTables(IEnumerable<ExtTable> tabs, string path = "") { Import.AddTables(tabs, path); External = null; }

    /// <summary>Nhận diện trường hợp, khớp thanh, dựng kết quả ngoài và tự ghép cặp trường hợp (giữ cặp người dùng đã sửa).</summary>
    public void Analyze()
    {
        Import.Options.FillUnmatchedFromOwn = false;
        Import.Analyze();
        // so sánh mọi trường hợp có số liệu (kể cả trường hợp mà bộ nhập tự cho "bỏ qua", trừ dao động riêng)
        foreach (var c in Import.Cases)
            if (c.Use == CaseUse.Ignore && !ExtTable.Norm(c.CaseType).Contains("MODAL")) c.Use = CaseUse.Load;
        External = Import.Build();
        var old = Pairs.ToDictionary(x => x.External, x => x);
        Pairs.Clear();
        foreach (var key in ExternalKeys())
        {
            if (old.TryGetValue(key, out var prev) && prev.Method == "người dùng chọn") { Pairs.Add(prev); continue; }
            var pr = new CrossCasePair { External = key };
            AutoPair(pr);
            if (old.TryGetValue(key, out prev)) pr.Enabled = prev.Enabled || string.IsNullOrEmpty(prev.Ours);
            Pairs.Add(pr);
        }
    }

    /// <summary>Tên trường hợp có số liệu trong kết quả ngoài (thứ tự xuất hiện trong bảng).</summary>
    public List<string> ExternalKeys()
    {
        var res = new List<string>();
        if (External == null) return res;
        foreach (var c in Import.Cases)
            foreach (var k in new[] { c.Name, c.Name + " (Max)", c.Name + " (Min)" })
                if (External.Cases.ContainsKey(k) && !res.Contains(k)) res.Add(k);
        foreach (var k in External.Cases.Keys) if (!res.Contains(k)) res.Add(k);
        return res;
    }

    /// <summary>Tên trường hợp tải + tổ hợp của chương trình để ghép cặp.</summary>
    public List<string> OurNames() =>
        Project.Patterns.Select(x => x.Name).Concat(Project.Combinations.Select(c => c.Name)).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();

    private static string NormName(string s) =>
        Regex.Replace(ResultImportSession.RemoveDiacritics(s ?? "").ToUpperInvariant(), "[^A-Z0-9]+", "");

    /// <summary>Ghép tự động theo tên (trùng tên → không phân biệt hoa thường/dấu/ký tự phân cách).</summary>
    public void AutoPair(CrossCasePair pr)
    {
        var ours = OurNames();
        string b = pr.ExternalBase;
        string m = ours.FirstOrDefault(n => n == b);
        if (m != null) { pr.Ours = m; pr.Method = "trùng tên"; pr.Enabled = true; return; }
        m = ours.FirstOrDefault(n => string.Equals(n, b, StringComparison.OrdinalIgnoreCase))
            ?? ours.FirstOrDefault(n => NormName(n) == NormName(b) && NormName(b).Length > 0);
        if (m != null) { pr.Ours = m; pr.Method = "trùng tên (không phân biệt hoa thường, dấu)"; pr.Enabled = true; return; }
        pr.Ours = ""; pr.Method = "không tìm thấy"; pr.Enabled = false;
    }

    /// <summary>Đặt cặp bằng tay (giao diện / kiểm thử).</summary>
    public void SetPair(string external, string ours, bool enabled = true)
    {
        var pr = Pairs.FirstOrDefault(x => x.External == external);
        if (pr == null) { pr = new CrossCasePair { External = external }; Pairs.Add(pr); }
        pr.Ours = ours ?? ""; pr.Enabled = enabled && !string.IsNullOrEmpty(pr.Ours); pr.Method = "người dùng chọn";
    }

    // ==================================================================
    // PHÍA CHƯƠNG TRÌNH
    // ==================================================================
    private sealed class OurView
    {
        public string Name = "", Step = "";
        public bool Envelope;
        public CaseResult Linear;
        public List<CaseResult> Parts = new();
        public Dictionary<int, double[]> Reactions = new(), Disp = new();
        public readonly Dictionary<int, FrameForces> FrameCache = new();
    }

    private OurView View(CrossCasePair pr)
    {
        var v = new OurView { Name = pr.Ours, Step = pr.Step };
        if (Ours.IsEnvelope(pr.Ours))
        {
            v.Envelope = true;
            foreach (var n in Ours.ExpandCombos(pr.Ours))
            {
                try { v.Parts.Add(Ours.Get(n)); } catch (Exception) { }
            }
            bool min = pr.Step == "Min";
            void Env(Func<CaseResult, Dictionary<int, double[]>> sel, Dictionary<int, double[]> dst)
            {
                foreach (var cr in v.Parts)
                    foreach (var (id, val) in sel(cr))
                    {
                        if (!dst.TryGetValue(id, out var d)) { dst[id] = (double[])val.Clone(); continue; }
                        for (int k = 0; k < 6; k++) d[k] = min ? Math.Min(d[k], val[k]) : Math.Max(d[k], val[k]);
                    }
            }
            Env(c => c.Reactions, v.Reactions);
            Env(c => c.Disp, v.Disp);
        }
        else
        {
            try { v.Linear = Ours.Get(pr.Ours); } catch (Exception) { return null; }
            v.Parts.Add(v.Linear);
            v.Reactions = v.Linear.Reactions;
            v.Disp = v.Linear.Disp;
        }
        return v;
    }

    private FrameForces OurFrame(OurView v, int fid)
    {
        if (v.FrameCache.TryGetValue(fid, out var ff)) return ff;
        if (!v.Envelope) ff = v.Linear.Frames.TryGetValue(fid, out var x) ? x : null;
        else
        {
            var (mx, mn) = Ours.Envelope(fid, Ours.ExpandCombos(v.Name));
            ff = v.Step == "Min" ? mn : mx;
        }
        v.FrameCache[fid] = ff;
        return ff;
    }

    // ==================================================================
    // SO SÁNH
    // ==================================================================
    internal static readonly string[] Comp = { "P", "V2", "V3", "T", "M2", "M3" };
    internal static readonly string[] ReacComp = { "FX", "FY", "FZ", "MX", "MY", "MZ" };

    public static string KindText(FrameKind k) => k switch
    {
        FrameKind.Column => "Cột",
        FrameKind.Beam => "Dầm",
        FrameKind.SteelColumn => "Cột thép",
        FrameKind.Rafter => "Kèo thép",
        FrameKind.Purlin => "Xà gồ",
        FrameKind.Brace => "Giằng",
        _ => k.ToString()
    };

    /// <summary>% theo nhóm (lực k = 0…2, mô men k = 3…5) với sàn giá trị nhỏ.</summary>
    private double[] Percents(double[] diff, double[] scale)
    {
        double gF = Math.Max(scale[0], Math.Max(scale[1], scale[2])), gM = Math.Max(scale[3], Math.Max(scale[4], scale[5]));
        double fF = Math.Max(Options.ForceFloor, Options.FloorRatio * gF), fM = Math.Max(Options.MomentFloor, Options.FloorRatio * gM);
        var pc = new double[6];
        for (int k = 0; k < 6; k++)
        {
            double den = Math.Max(scale[k], k < 3 ? fF : fM);
            pc[k] = den > 1e-12 ? diff[k] / den * 100 : 0;
        }
        return pc;
    }

    private static int ArgMax(double[] a)
    {
        int b = 0;
        for (int i = 1; i < a.Length; i++) if (a[i] > a[b]) b = i;
        return b;
    }

    /// <summary>Chạy so sánh theo các cặp trường hợp hiện tại.</summary>
    public CrossCheckReport Run()
    {
        if (External == null) Analyze();
        var p = Project;
        p.InvalidateIndex();
        var rep = new CrossCheckReport { ProjectName = p.Name, Options = Options.Clone(), Software = External.External?.Software ?? Import.DetectSoftware() };
        rep.Files.AddRange(Import.Files);
        rep.Units = External.External?.SourceUnits ?? "";
        rep.Pairs.AddRange(Pairs);
        rep.UnmatchedExternal.AddRange(Import.Matches.Where(m => !m.Matched));
        rep.FramesWithoutData.AddRange(Import.FramesWithoutData);
        foreach (var m in Import.Messages) rep.Messages.Add(m);
        var active = Pairs.Where(x => x.Active && External.Cases.ContainsKey(x.External)).ToList();
        foreach (var pr in Pairs.Where(x => !x.Active)) rep.Messages.Add($"Trường hợp \"{pr.External}\" không so sánh ({(string.IsNullOrEmpty(pr.Ours) ? "chưa ghép với trường hợp/tổ hợp của chương trình" : "đã bỏ chọn")}).");
        foreach (var pr in Pairs.Where(x => x.Active && x.Step.Length == 0 && Ours.IsEnvelope(x.Ours)))
            rep.Messages.Add($"\"{pr.External}\" ghép với tổ hợp bao \"{pr.Ours}\" – so sánh với giá trị Max của bao (file ngoài không có dòng Max/Min).");

        var views = new Dictionary<CrossCasePair, OurView>();
        foreach (var pr in active)
        {
            var v = View(pr);
            if (v == null) { rep.Messages.Add($"Không lấy được kết quả \"{pr.Ours}\" của chương trình."); continue; }
            views[pr] = v;
        }

        CompareFrames(rep, views);
        CompareReactions(rep, views);
        CompareBase(rep, views);
        CompareStories(rep, views);
        CompareModal(rep);
        BuildStats(rep);
        BuildHints(rep);
        return rep;
    }

    // ------------------------------------------------------------------ thanh
    private void CompareFrames(CrossCheckReport rep, Dictionary<CrossCasePair, OurView> views)
    {
        var p = Project;
        var extName = new Dictionary<int, string>();
        foreach (var m in Import.Matches.Where(m => m.Matched))
            if (!extName.ContainsKey(m.FrameId)) extName[m.FrameId] = m.External + (m.Story.Length > 0 ? " @" + m.Story : "");
        foreach (var f in p.Frames)
        {
            var mem = new CrossMember
            {
                FrameId = f.Id, Label = f.Label, Story = f.Story, Kind = KindText(f.Kind),
                ExternalName = extName.TryGetValue(f.Id, out var en) ? en : ""
            };
            foreach (var (pr, v) in views)
            {
                if (!External.Cases[pr.External].Frames.TryGetValue(f.Id, out var ef) || ef == null || ef.Count == 0) continue;
                var of = OurFrame(v, f.Id);
                if (of == null || of.Count == 0) continue;
                var mc = CompareFrame(f, pr, of, ef);
                mc.ExternalName = mem.ExternalName;
                mem.Cases.Add(mc);
                rep.MemberCases.Add(mc);
            }
            if (mem.Cases.Count > 0)
            {
                var w = mem.Cases.OrderByDescending(c => c.WorstPercent).First();
                mem.WorstPercent = w.WorstPercent; mem.WorstCase = w.ExternalCase; mem.WorstComponent = w.WorstComponent;
                mem.Level = Options.Classify(mem.WorstPercent);
                mem.OurM3 = mem.Cases.Max(c => c.OurMax[5]); mem.ExtM3 = mem.Cases.Max(c => c.ExtMax[5]);
                mem.M3Percent = mem.Cases.Max(c => c.Percent[5]); mem.V2Percent = mem.Cases.Max(c => c.Percent[1]); mem.PPercent = mem.Cases.Max(c => c.Percent[0]);
                mem.CaseCount = mem.Cases.Count;
            }
            else if (views.Count == 0) continue;
            rep.Members.Add(mem);
        }
    }

    private CrossMemberCase CompareFrame(FrameElement f, CrossCasePair pr, FrameForces o, FrameForces e)
    {
        double L = Project.Length(f);
        var xs = new SortedSet<double>();
        foreach (var s in o.Stations) xs.Add(Math.Round(Math.Clamp(s, 0, L), 6));
        foreach (var s in e.Stations) xs.Add(Math.Round(Math.Clamp(s, 0, L), 6));
        double eps = Math.Max(1e-4, Options.JumpTolerance * L);
        var mc = new CrossMemberCase
        {
            FrameId = f.Id, Label = f.Label, Story = f.Story, Kind = KindText(f.Kind),
            ExternalCase = pr.External, OurCase = pr.Ours + (pr.Step.Length > 0 && Ours.IsEnvelope(pr.Ours) ? " (" + pr.Step + ")" : ""),
            Stations = xs.Count, OurForces = o, ExtForces = e
        };
        foreach (double x in xs)
        {
            var ov = o.At(x); var ev = e.At(x);
            var om = o.At(Math.Max(0, x - eps)); var op = o.At(Math.Min(L, x + eps));
            for (int k = 0; k < 6; k++)
            {
                mc.OurMax[k] = Math.Max(mc.OurMax[k], Math.Abs(ov[k]));
                mc.ExtMax[k] = Math.Max(mc.ExtMax[k], Math.Abs(ev[k]));
                // bước nhảy lực cắt: lấy sai lệch nhỏ nhất trong lân cận ±ε (hai phần mềm đặt trạm tại bước nhảy khác nhau)
                double d = Math.Min(Math.Abs(ov[k] - ev[k]), Math.Min(Math.Abs(om[k] - ev[k]), Math.Abs(op[k] - ev[k])));
                mc.Diff[k] = Math.Max(mc.Diff[k], d);
            }
        }
        var scale = new double[6];
        for (int k = 0; k < 6; k++) scale[k] = Math.Max(mc.OurMax[k], mc.ExtMax[k]);
        mc.Percent = Percents(mc.Diff, scale);
        int w = ArgMax(mc.Percent);
        mc.WorstPercent = mc.Percent[w]; mc.WorstComponent = Comp[w];
        mc.Level = Options.Classify(mc.WorstPercent);
        return mc;
    }

    // ------------------------------------------------------------------ phản lực
    private void CompareReactions(CrossCheckReport rep, Dictionary<CrossCasePair, OurView> views)
    {
        var p = Project;
        foreach (var (pr, v) in views)
        {
            var ext = External.Cases[pr.External].Reactions;
            if (ext.Count == 0) continue;
            var ids = v.Reactions.Keys.Union(ext.Keys).Distinct().OrderBy(i => i).ToList();
            foreach (int id in ids)
            {
                Node n;
                try { n = p.Node(id); } catch (Exception) { continue; }
                if (n == null) continue;
                var r = new CrossReaction { NodeId = id, X = n.X, Y = n.Y, Z = n.Z, ExternalCase = pr.External, OurCase = pr.Ours };
                bool hasO = v.Reactions.TryGetValue(id, out var ov), hasE = ext.TryGetValue(id, out var ev);
                if (hasO) r.Ours = (double[])ov.Clone();
                if (hasE) r.Ext = (double[])ev.Clone();
                if (!hasO || !hasE)
                {
                    // gối chỉ có một phía với phản lực ≈ 0 → bỏ qua; ngược lại ghi thiếu số liệu
                    var only = hasO ? ov : ev;
                    if (only.All(x => Math.Abs(x) < 1e-6)) continue;
                    r.Level = CrossLevel.NoData; r.WorstPercent = double.NaN;
                    r.Note = hasO ? "không có trong file ngoài" : "chương trình không có gối tại nút này";
                    rep.Reactions.Add(r);
                    continue;
                }
                var diff = new double[6]; var scale = new double[6];
                for (int k = 0; k < 6; k++) { diff[k] = Math.Abs(ov[k] - ev[k]); scale[k] = Math.Max(Math.Abs(ov[k]), Math.Abs(ev[k])); }
                r.Percent = Percents(diff, scale);
                int w = ArgMax(r.Percent);
                r.WorstPercent = r.Percent[w]; r.WorstComponent = ReacComp[w];
                r.Level = Options.Classify(r.WorstPercent);
                rep.Reactions.Add(r);
            }
        }
    }

    // ------------------------------------------------------------------ tổng phản lực
    private static double[] SumAbout(Project p, Dictionary<int, double[]> reac, Vec3 o)
    {
        var s = new double[6];
        foreach (var (id, v) in reac)
        {
            Node n;
            try { n = p.Node(id); } catch (Exception) { continue; }
            if (n == null) continue;
            double rx = n.X - o.X, ry = n.Y - o.Y, rz = n.Z - o.Z;
            s[0] += v[0]; s[1] += v[1]; s[2] += v[2];
            s[3] += v[3] + ry * v[2] - rz * v[1];
            s[4] += v[4] + rz * v[0] - rx * v[2];
            s[5] += v[5] + rx * v[1] - ry * v[0];
        }
        return s;
    }

    /// <summary>Bảng "Base Reactions": khoá trường hợp → (FX…MZ kN, kN.m; điểm tham chiếu).</summary>
    private Dictionary<string, (double[] v, Vec3 at)> ReadBaseTables()
    {
        var res = new Dictionary<string, (double[], Vec3)>();
        foreach (var t in Import.Tables.Where(x => x.Kind == ExtTableKind.BaseReactions))
        {
            int cc = t.Col("Output Case", "OutputCase", "Load Case/Combo", "LoadCase", "Load Case", "Combo", "Load Combo", "Case"), cs = t.Col("Step Type", "StepType");
            int[] col = { t.Col("FX", "GlobalFX", "F1"), t.Col("FY", "GlobalFY", "F2"), t.Col("FZ", "GlobalFZ", "F3"), t.Col("MX", "GlobalMX", "M1"), t.Col("MY", "GlobalMY", "M2"), t.Col("MZ", "GlobalMZ", "M3") };
            int cx = t.Col("X", "GlobalX"), cy = t.Col("Y", "GlobalY"), cz = t.Col("Z", "GlobalZ");
            if (cc < 0 || col[2] < 0) continue;
            var fac = new double[6];
            for (int k = 0; k < 6; k++) fac[k] = col[k] < 0 ? 0 : t.Factor(col[k], k < 3 ? Qty.Force : Qty.Moment);
            double fl = cx >= 0 ? t.Factor(cx, Qty.Length) : 1;
            foreach (var r in t.Rows)
            {
                string name = ExtTable.Cell(r, cc);
                if (name.Length == 0) continue;
                var v = new double[6];
                bool any = false;
                for (int k = 0; k < 6; k++)
                    if (col[k] >= 0 && ExtTable.TryNum(ExtTable.Cell(r, col[k]), out double x)) { v[k] = x * fac[k]; any = true; }
                if (!any) continue;
                double X(int c) => c >= 0 && ExtTable.TryNum(ExtTable.Cell(r, c), out double q) ? q * fl : 0;
                res[ResultImportSession.CaseKey(name, ExtTable.Cell(r, cs))] = (v, new Vec3(X(cx), X(cy), X(cz)));
            }
        }
        return res;
    }

    private void CompareBase(CrossCheckReport rep, Dictionary<CrossCasePair, OurView> views)
    {
        var tab = ReadBaseTables();
        foreach (var (pr, v) in views)
        {
            var ext = External.Cases[pr.External].Reactions;
            var b = new CrossBaseReaction { ExternalCase = pr.External, OurCase = pr.Ours };
            Vec3 at = new Vec3(0, 0, 0);
            if (tab.TryGetValue(pr.External, out var row)) { b.Ext = row.v; at = row.at; b.Source = "Base Reactions"; }
            else if (ext.Count > 0) { b.Ext = SumAbout(Project, ext, at); b.Source = "tổng phản lực nút"; }
            else continue;
            // tổ hợp bao: tổng các gối không phải phản lực tổng của một tổ hợp → chỉ so sánh khi phía chương trình tuyến tính
            if (v.Envelope) { b.Ours = SumAbout(Project, v.Reactions, at); b.Source += " (bao – tham khảo)"; }
            else b.Ours = SumAbout(Project, v.Reactions, at);
            var diff = new double[6]; var scale = new double[6];
            for (int k = 0; k < 6; k++) { diff[k] = Math.Abs(b.Ours[k] - b.Ext[k]); scale[k] = Math.Max(Math.Abs(b.Ours[k]), Math.Abs(b.Ext[k])); }
            b.Percent = Percents(diff, scale);
            // mô men tổng phụ thuộc điểm tham chiếu – đánh giá theo lực
            b.WorstPercent = Math.Max(b.Percent[0], Math.Max(b.Percent[1], b.Percent[2]));
            b.Level = v.Envelope ? CrossLevel.NoData : Options.Classify(b.WorstPercent);
            rep.BaseReactions.Add(b);
        }
    }

    // ------------------------------------------------------------------ tầng
    private void CompareStories(CrossCheckReport rep, Dictionary<CrossCasePair, OurView> views)
    {
        var p = Project;
        var stories = p.Stories.OrderBy(s => s.Elevation).ToList();
        if (stories.Count == 0) return;
        double top = stories[^1].Elevation;
        var drifts = External.External?.Drifts ?? new List<ExternalDrift>();
        foreach (var (pr, v) in views)
        {
            var ed = External.Cases[pr.External].Disp;
            if (ed.Count > 0)
                foreach (var st in stories)
                {
                    var nodes = p.Nodes.Where(n => Math.Abs(n.Z - st.Elevation) < 2e-3 && v.Disp.ContainsKey(n.Id) && ed.ContainsKey(n.Id)).ToList();
                    if (nodes.Count == 0) continue;
                    for (int k = 0; k < 2; k++)
                    {
                        double o = nodes.Max(n => Math.Abs(v.Disp[n.Id][k])), e = nodes.Max(n => Math.Abs(ed[n.Id][k]));
                        double d = nodes.Max(n => Math.Abs(v.Disp[n.Id][k] - ed[n.Id][k]));
                        double pc = d / Math.Max(Math.Max(o, e), Options.DispFloor) * 100;
                        rep.Stories.Add(new CrossStory
                        {
                            Quantity = "Chuyển vị", Story = st.Name, Elevation = st.Elevation, ExternalCase = pr.External, OurCase = pr.Ours,
                            Direction = k == 0 ? "X" : "Y", Ours = o, Ext = e, Diff = d, Percent = pc, Level = Options.Classify(pc), Top = Math.Abs(st.Elevation - top) < 1e-6
                        });
                    }
                }
            if (pr.Step == "Min") continue;
            foreach (var g in drifts.Where(x => x.Case == pr.ExternalBase))
            {
                if (!Import.TryStoryElevation(g.Story, out double z)) continue;
                int si = stories.FindIndex(s => Math.Abs(s.Elevation - z) < 2e-3);
                if (si < 0) continue;
                double zb = si > 0 ? stories[si - 1].Elevation : p.BaseElevation;
                string dir = (g.Direction ?? "").Trim().ToUpperInvariant();
                int k = dir.StartsWith("Y") ? 1 : 0;
                double o = OurDrift(v, z, zb, k);
                double d = Math.Abs(o - g.Drift);
                double pc = d / Math.Max(Math.Max(o, g.Drift), Options.DriftFloor) * 100;
                rep.Stories.Add(new CrossStory
                {
                    Quantity = "Lệch tầng", Story = stories[si].Name, Elevation = z, ExternalCase = pr.ExternalBase, OurCase = pr.Ours,
                    Direction = k == 0 ? "X" : "Y", Ours = o, Ext = g.Drift, Diff = d, Percent = pc, Level = Options.Classify(pc), Top = Math.Abs(z - top) < 1e-6
                });
            }
        }
    }

    /// <summary>Chuyển vị lệch tầng lớn nhất |Δu|/h giữa nút tầng z và nút cùng toạ độ mặt bằng ở cao độ zb (qua các tổ hợp thành phần nếu là bao).</summary>
    private double OurDrift(OurView v, double z, double zb, int k)
    {
        double h = z - zb;
        if (h < 1e-6) return 0;
        var p = Project;
        var below = new Dictionary<(long, long), Node>();
        foreach (var n in p.Nodes.Where(n => Math.Abs(n.Z - zb) < 2e-3)) below.TryAdd((Key(n.X), Key(n.Y)), n);
        double best = 0;
        foreach (var cr in v.Parts)
            foreach (var n in p.Nodes.Where(n => Math.Abs(n.Z - z) < 2e-3))
            {
                if (!cr.Disp.TryGetValue(n.Id, out var u)) continue;
                double ub = 0;
                if (below.TryGetValue((Key(n.X), Key(n.Y)), out var nb) && cr.Disp.TryGetValue(nb.Id, out var d0)) ub = d0[k];
                else if (!below.ContainsKey((Key(n.X), Key(n.Y)))) continue;
                best = Math.Max(best, Math.Abs(u[k] - ub) / h);
            }
        return best;
    }

    private static long Key(double v) => (long)Math.Round(v * 1000);

    // ------------------------------------------------------------------ dao động
    private void CompareModal(CrossCheckReport rep)
    {
        foreach (var t in Import.Tables)
        {
            string n = ExtTable.Norm(t.Name);
            if (!(n.Contains("MODAL") && n.Contains("PERIOD")) && !(n.Contains("MODAL") && t.Has("Period"))) continue;
            int cm = t.Col("Mode", "StepNum", "Step Num"), cp = t.Col("Period");
            if (cp < 0) continue;
            foreach (var r in t.Rows)
            {
                if (!ExtTable.TryNum(ExtTable.Cell(r, cp), out double T)) continue;
                int mode = ExtTable.TryNum(ExtTable.Cell(r, cm), out double md) ? (int)Math.Round(md) : rep.Modal.Count + 1;
                if (rep.Modal.Any(x => x.Mode == mode)) continue;
                rep.Modal.Add(new CrossModal { Mode = mode, ExtPeriod = T });
            }
        }
        if (rep.Modal.Count == 0) return;
        rep.Modal.Sort((a, b) => a.Mode.CompareTo(b.Mode));
        // chương trình không phân tích dao động riêng: ước lượng T1 = Ct·H^(3/4) (TCVN 9386:2012) để tham khảo
        var p = Project;
        var top = p.Stories.OrderBy(s => s.Elevation).LastOrDefault();
        if (top != null)
        {
            double H = top.Elevation - p.BaseElevation;
            bool steel = p.Frames.Count(f => !f.IsConcrete) > p.Frames.Count / 2;
            double T1 = (steel ? 0.085 : 0.075) * Math.Pow(Math.Max(H, 0), 0.75);
            var m1 = rep.Modal[0];
            m1.OurPeriod = T1;
            m1.Percent = Math.Abs(T1 - m1.ExtPeriod) / Math.Max(Math.Max(T1, m1.ExtPeriod), 1e-6) * 100;
            m1.Note = $"chương trình không phân tích dao động riêng – T1 ước lượng theo Ct·H^0,75 (H = {H.ToString("0.00", Inv)} m), chỉ để tham khảo";
        }
        foreach (var m in rep.Modal.Skip(1)) m.Note = "chỉ có trong file ngoài";
    }

    // ------------------------------------------------------------------ thống kê + gợi ý
    private CrossStats MakeStats(string group, IEnumerable<(double pct, CrossLevel lv, string at)> items)
    {
        var s = new CrossStats { Group = group };
        var vals = new List<double>();
        foreach (var (pct, lv, at) in items)
        {
            s.Count++;
            switch (lv)
            {
                case CrossLevel.Ok: s.Ok++; break;
                case CrossLevel.Warn: s.Warn++; break;
                case CrossLevel.Fail: s.Fail++; break;
                default: s.NoData++; break;
            }
            if (lv == CrossLevel.NoData || double.IsNaN(pct)) continue;
            vals.Add(pct);
            if (vals.Count == 1 || pct > s.Max) { s.Max = pct; s.MaxAt = at; }
        }
        s.Mean = vals.Count > 0 ? vals.Average() : 0;
        return s;
    }

    public const string GroupMembers = "Thanh (cấu kiện)";
    public const string GroupMemberCases = "Thanh × trường hợp";
    public const string GroupReactions = "Phản lực gối";
    public const string GroupBase = "Tổng phản lực";
    public const string GroupDisp = "Chuyển vị tầng";
    public const string GroupDrift = "Lệch tầng";

    private void BuildStats(CrossCheckReport rep)
    {
        rep.Stats.Add(MakeStats(GroupMembers, rep.Members.Select(m => (m.WorstPercent, m.Level, $"{m.Label} #{m.FrameId} ({m.WorstCase}, {m.WorstComponent})"))));
        rep.Stats.Add(MakeStats(GroupMemberCases, rep.MemberCases.Select(m => (m.WorstPercent, m.Level, $"{m.Label} #{m.FrameId} {m.ExternalCase} {m.WorstComponent}"))));
        rep.Stats.Add(MakeStats(GroupReactions, rep.Reactions.Select(r => (r.WorstPercent, r.Level, $"nút {r.NodeId} {r.ExternalCase} {r.WorstComponent}"))));
        rep.Stats.Add(MakeStats(GroupBase, rep.BaseReactions.Where(b => b.Level != CrossLevel.NoData).Select(b => (b.WorstPercent, b.Level, b.ExternalCase))));
        rep.Stats.Add(MakeStats(GroupDisp, rep.Stories.Where(s => s.Quantity == "Chuyển vị").Select(s => (s.Percent, s.Level, $"{s.Story} {s.ExternalCase} U{s.Direction}"))));
        rep.Stats.Add(MakeStats(GroupDrift, rep.Stories.Where(s => s.Quantity == "Lệch tầng").Select(s => (s.Percent, s.Level, $"{s.Story} {s.ExternalCase} {s.Direction}"))));
    }

    private bool IsLateral(string ourName, int depth = 0)
    {
        var pat = Project.Pattern(ourName);
        if (pat != null) return pat.Type is LoadType.Wind or LoadType.Seismic;
        if (depth > 6) return false;
        var c = Project.Combinations.FirstOrDefault(x => x.Name == ourName);
        return c != null && c.Items.Any(i => IsLateral(i.Case, depth + 1));
    }

    private bool IsGravityOnly(string ourName, int depth = 0)
    {
        var pat = Project.Pattern(ourName);
        if (pat != null) return pat.Type is not (LoadType.Wind or LoadType.Seismic);
        if (depth > 6) return false;
        var c = Project.Combinations.FirstOrDefault(x => x.Name == ourName);
        return c != null && c.Items.Count > 0 && c.Items.All(i => IsGravityOnly(i.Case, depth + 1));
    }

    private void BuildHints(CrossCheckReport rep)
    {
        var o = Options;
        var H = rep.Hints;
        if (rep.Pairs.Count == 0 || rep.Pairs.All(x => !x.Active)) H.Add("Chưa ghép được trường hợp/tổ hợp nào – đặt cùng tên trường hợp tải/tổ hợp trong ETABS như khi xuất .e2k hoặc ghép bằng tay.");
        if (rep.UnmatchedExternal.Count > 0) H.Add($"{rep.UnmatchedExternal.Count} thanh trong file ngoài không khớp với dự án – mô hình ETABS có thể đã sửa (thêm/xoá/chia thanh). Xuất kèm bảng Point/Frame Object Connectivity để khớp theo toạ độ.");
        var mcs = rep.MemberCases;
        if (mcs.Count == 0) { if (rep.Members.Count > 0) H.Add("Không có cặp thanh × trường hợp nào để so sánh nội lực."); return; }
        bool bad(CrossLevel l) => l is CrossLevel.Warn or CrossLevel.Fail;
        var baseBad = rep.BaseReactions.Where(b => bad(b.Level) && b.Percent[2] > o.WarnPercent).ToList();
        var baseGravOk = rep.BaseReactions.Where(b => b.Level == CrossLevel.Ok && IsGravityOnly(b.OurCase)).ToList();
        if (baseBad.Count > 0)
            H.Add($"Tổng phản lực đứng FZ lệch ở {baseBad.Count} trường hợp (vd. {baseBad[0].ExternalCase}: {baseBad[0].Ours[2]:0.0} / {baseBad[0].Ext[2]:0.0} kN) – tổng TẢI khác nhau: kiểm tra hệ số trọng lượng bản thân (SELFWEIGHT), trọng lượng sàn/shell kể hai lần, tải tường, tải hoàn thiện, khối lượng riêng vật liệu.");
        var gravBeams = mcs.Where(m => m.Kind == "Dầm" && IsGravityOnly(Strip(m.OurCase)) && bad(Options.Classify(m.Percent[5]))).ToList();
        if (gravBeams.Count > 0)
            H.Add($"{gravBeams.Select(m => m.FrameId).Distinct().Count()} dầm lệch M3 do tải đứng" + (baseGravOk.Count > 0 ? " trong khi tổng tải đứng khớp" : "")
                  + " – thường do mô hình sàn (chương trình dồn tải 45° về dầm, ETABS chia lưới shell chịu uốn cùng dầm) và đoạn cứng đầu thanh (end offsets/rigid zone) làm giảm mô men tại mép gối.");
        var lat = mcs.Where(m => IsLateral(Strip(m.OurCase)) && bad(m.Level)).ToList();
        if (lat.Count > 0)
            H.Add($"{lat.Select(m => m.FrameId).Distinct().Count()} thanh lệch nội lực trong các trường hợp/tổ hợp có tải ngang – kiểm tra sàn cứng (diaphragm), hệ số giảm độ cứng tiết diện (property modifiers), P-Delta, điểm đặt tải gió/động đất và độ lệch tâm ngẫu nhiên.");
        var beamsP = mcs.Where(m => m.Kind == "Dầm" && m.Percent[0] > o.FailPercent && m.Percent[5] <= o.WarnPercent).ToList();
        if (beamsP.Count > 0)
            H.Add($"{beamsP.Select(m => m.FrameId).Distinct().Count()} dầm chỉ lệch lực dọc P – do khác nhau về sàn cứng: với diaphragm cứng lực dọc dầm trong mặt phẳng sàn ≈ 0.");
        var disp = rep.Stories.Where(s => s.Quantity == "Chuyển vị" && bad(s.Level)).ToList();
        int memBad = rep.Members.Count(m => bad(m.Level));
        if (disp.Count > 0 && memBad <= Math.Max(1, rep.Members.Count / 20))
            H.Add("Chuyển vị lệch trong khi nội lực khớp – độ cứng khác nhau: mô đun đàn hồi bê tông, hệ số giảm độ cứng tiết diện (0,35I dầm, 0,7I cột…), biến dạng cắt, đoạn cứng đầu thanh.");
        if (rep.Modal.Count > 0) H.Add("Chu kỳ dao động: chương trình không phân tích dao động riêng (lực động đất theo phương pháp tĩnh lực ngang tương đương, T1 = Ct·H^0,75); chu kỳ ETABS chỉ để tham khảo, nên kiểm tra khối lượng tham gia dao động (mass source).");
        if (memBad == 0 && rep.Reactions.All(r => !bad(r.Level)) && rep.BaseReactions.All(b => !bad(b.Level)))
            H.Add($"Toàn bộ sai lệch trong ngưỡng {o.WarnPercent:0.#} % – mô hình hai phần mềm tương đương.");
    }

    private static string Strip(string s) => s.EndsWith(" (Max)") || s.EndsWith(" (Min)") ? s.Substring(0, s.Length - 6) : s;
}

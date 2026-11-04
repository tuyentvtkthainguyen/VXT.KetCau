using KetCauVXT.Analysis;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Verification;

/// <summary>Bộ bài toán chuẩn kiểm chứng. Mỗi bài toán là một "suite" (một mô hình) cho một hoặc nhiều đại lượng.
/// Phần A (tệp này): phân tích kết cấu – so với lời giải giải tích dạng kín. Giá trị tham chiếu tính bằng công thức viết tay
/// trong tệp này (không đọc lại kết quả của chương trình); kết quả Kết cấu VXT lấy từ <see cref="FrameAnalysis"/>.
/// Chương trình dùng phần tử thanh Timoshenko (kể biến dạng trượt, diện tích chịu cắt 5/6·b·h với tiết diện chữ nhật) nên lời giải
/// tham chiếu dùng lý thuyết dầm Timoshenko (số hạng uốn Euler–Bernoulli + số hạng trượt) – ghi rõ trong cột nguồn.</summary>
public static partial class Benchmarks
{
    public const string GroupAnalysis = "A. Phân tích kết cấu – so với lời giải giải tích";
    public const string GroupConcrete = "B. Thiết kế BTCT theo TCVN 5574:2018 – so với tính tay độc lập";
    public const string GroupFoundation = "C. Nền móng (TCVN 9362:2012, TCVN 10304:2014) – so với tính tay độc lập";
    public const string GroupLoads = "D. Tải trọng và tổ hợp (TCVN 2737:2023) – so với tính tay độc lập";

    /// <summary>Một bài toán chuẩn: tên + hàm chạy trả về các đại lượng so sánh.</summary>
    public sealed class Suite
    {
        public string Id { get; init; } = "";
        public string Group { get; init; } = "";
        public string Title { get; init; } = "";
        public Func<List<BenchmarkCase>> Run { get; init; }
    }

    /// <summary>Toàn bộ bài toán theo thứ tự báo cáo.</summary>
    public static List<Suite> All()
    {
        var l = new List<Suite>();
        l.AddRange(AnalysisSuites());
        l.AddRange(DesignSuites());
        l.AddRange(FoundationSuites());
        l.AddRange(LoadSuites());
        return l;
    }

    /// <summary>Bài toán trong danh sách đề xuất nhưng chưa có chức năng tương ứng trong chương trình (ghi vào báo cáo).</summary>
    public static List<string> SkippedProblems() => new()
    {
        "Chu kỳ dao động riêng (phân tích dao động / modal) của cột công xôn một bậc tự do: chương trình đã có phân tích dao động riêng và phổ phản ứng "
        + "(được kiểm thử riêng trong bộ kiểm thử ModalTests) nhưng bài toán này chưa đưa vào bộ kiểm chứng của báo cáo; bỏ qua.",
        "Hiệu ứng P-delta (phân tích phi tuyến hình học): chương trình chỉ phân tích tĩnh tuyến tính bậc nhất; bỏ qua.",
        "Bề rộng vết nứt acrc: chương trình chưa tính bề rộng vết nứt (chỉ kiểm tra độ võng theo độ cứng có kể nứt); bỏ qua."
    };

    // ------------------------------------------------------------------
    // tiện ích chung
    // ------------------------------------------------------------------
    /// <summary>Tạo một đại lượng so sánh.</summary>
    internal static BenchmarkCase C(string id, string group, string title, string desc, string source, string quantity, string unit,
        double reference, double computed, double tolPct, string notes = "", double scale = 0) => new()
    {
        Id = id, Group = group, Title = title, Description = desc, ReferenceSource = source, Quantity = quantity, Unit = unit,
        Reference = reference, Computed = computed, Tolerance = tolPct, Notes = notes ?? "", Scale = scale
    };

    /// <summary>Chạy một bài toán, bắt mọi ngoại lệ thành một dòng "LỖI" (không làm dừng cả bộ kiểm chứng).</summary>
    internal static List<BenchmarkCase> Safe(string id, string group, string title, Func<List<BenchmarkCase>> body)
    {
        try
        {
            var l = body() ?? new List<BenchmarkCase>();
            if (l.Count == 0) throw new InvalidOperationException("bài toán không trả về đại lượng nào");
            return l;
        }
        catch (Exception ex)
        {
            return new List<BenchmarkCase>
            {
                new()
                {
                    Id = id + "-1", Group = group, Title = title, Quantity = "(không chạy được)", Reference = double.NaN,
                    Computed = double.NaN, Error = ex.GetType().Name + ": " + ex.Message, Tolerance = 0,
                    Description = "Bài toán bị lỗi khi chạy – xem cột ghi chú."
                }
            };
        }
    }

    private static Suite S(string id, string group, string title, Func<List<BenchmarkCase>> run) =>
        new() { Id = id, Group = group, Title = title, Run = () => Safe(id, group, title, run) };

    /// <summary>Hằng số vật liệu dùng cho lời giải tham chiếu (nhập tay theo tiêu chuẩn, không đọc từ thư viện của chương trình):
    /// bê tông B25 Eb = 30 000 MPa (TCVN 5574:2018 Bảng 10), ν = 0,2 → G = E/2,4; thép hình E = 210 000 MPa, ν = 0,3 → G = E/2,6.</summary>
    internal const double Eb = 30.0e6, Gb = Eb / 2.4, Esteel = 210.0e6, Gsteel = Esteel / 2.6;

    private static bool[] Rs(string mask)
    {
        var r = new bool[6];
        for (int i = 0; i < 6 && i < mask.Length; i++) r[i] = mask[i] == '1';
        return r;
    }

    private static Project NewModel(params FrameSection[] sections)
    {
        var p = new Project { Name = "Kiểm chứng", UseRigidDiaphragm = true };
        p.Materials.Add(Material.Concrete("B25"));
        p.Materials.Add(Material.Steel("SS400"));
        foreach (var s in sections) p.FrameSections.Add(s);
        p.Patterns.Add(new LoadPattern { Name = "P", Type = LoadType.Dead, SelfWeight = 0, Gamma = 1.0 });
        p.Wind.Enabled = false;
        p.Seismic.Enabled = false;
        return p;
    }

    private static Node AddNode(Project p, int id, double x, double y, double z, string restraint = "000000")
    {
        var n = new Node { Id = id, X = x, Y = y, Z = z, Restraint = Rs(restraint) };
        p.Nodes.Add(n);
        return n;
    }

    private static FrameElement AddFrame(Project p, int id, int i, int j, string sec, FrameKind kind = FrameKind.Beam)
    {
        var f = new FrameElement { Id = id, NodeI = i, NodeJ = j, Section = sec, Kind = kind, Label = "T" + id };
        p.Frames.Add(f);
        return f;
    }

    private static double MaxOver(FrameForces f, int comp) => f.F.Max(v => v[comp]);

    // ==================================================================
    // A. PHÂN TÍCH
    // ==================================================================
    private static IEnumerable<Suite> AnalysisSuites()
    {
        const string G = GroupAnalysis;
        yield return S("A01", G, "Dầm đơn giản chịu tải phân bố đều", SimpleBeam);
        yield return S("A02", G, "Dầm hai đầu ngàm chịu tải phân bố đều", FixedBeam);
        yield return S("A03", G, "Dầm một đầu ngàm – một đầu khớp (tải đều)", ProppedCantilever);
        yield return S("A04", G, "Dầm công xôn chịu lực tập trung ở đầu tự do", CantileverPoint);
        yield return S("A05", G, "Dầm liên tục hai nhịp bằng nhau chịu tải đều", TwoSpan);
        yield return S("A06", G, "Khung cổng một nhịp chân ngàm chịu lực ngang", PortalFrame);
        yield return S("A07", G, "Cột chịu nén đúng tâm – biến dạng dọc trục", AxialColumn);
        yield return S("A08", G, "Thanh công xôn chịu xoắn", TorsionCantilever);
        yield return S("A09", G, "Dầm cong 1/4 đường tròn công xôn (mặt bằng) chịu tải đều", CurvedCantilever);
        yield return S("A10", G, "Dầm dài vô hạn trên nền Winkler chịu lực tập trung", WinklerInfinite);
        yield return S("A11", G, "Cột thép tiết diện thay đổi (vát) công xôn – độ nhạy chia đoạn", TaperedColumn);
        yield return S("A12", G, "Khung nhà 3 tầng – cân bằng phản lực với tổng tải", StoreyEquilibrium);
    }

    // ---- A01 ----
    private static List<BenchmarkCase> SimpleBeam()
    {
        const string G = GroupAnalysis, T = "Dầm đơn giản chịu tải phân bố đều";
        double L = 6, q = 20, b = 0.30, h = 0.50;
        var p = NewModel(FrameSection.Rect("D30x50", 300, 500));
        AddNode(p, 1, 0, 0, 3, "111100");
        AddNode(p, 2, L / 2, 0, 3);
        AddNode(p, 3, L, 0, 3, "011100");
        AddFrame(p, 1, 1, 2, "D30x50"); AddFrame(p, 2, 2, 3, "D30x50");
        p.FrameLoads.Add(FrameLoad.Uniform(1, "P", q)); p.FrameLoads.Add(FrameLoad.Uniform(2, "P", q));
        var r = FrameAnalysis.Run(p).Get("P");

        double I = b * h * h * h / 12, As = 5.0 / 6 * b * h;
        double dB = 5 * q * Math.Pow(L, 4) / (384 * Eb * I), dS = q * L * L / 8 / (Gb * As);
        string desc = $"Dầm BTCT B25 {b * 1000:0}×{h * 1000:0} mm, nhịp L = {L} m, gối khớp – gối di động, q = {q} kN/m; mô hình 2 phần tử (nút giữa nhịp).";
        return new List<BenchmarkCase>
        {
            C("A01-1", G, T, desc, "Sức bền vật liệu: M = qL²/8", "M3 giữa nhịp", "kN.m", q * L * L / 8, r.Frames[1].At(L / 2)[5], 0.1,
              "Quy ước M3 dương căng thớ dưới (như ETABS/SAP2000)."),
            C("A01-2", G, T, desc, "Sức bền vật liệu: V = qL/2", "|V2| tại gối", "kN", q * L / 2, Math.Abs(r.Frames[1].At(0)[1]), 0.1),
            C("A01-3", G, T, desc, "Cân bằng: R = qL/2", "Phản lực FZ gối trái", "kN", q * L / 2, r.Reactions[1][2], 0.1),
            C("A01-4", G, T, desc, "Dầm Timoshenko: δ = 5qL⁴/(384EI) + qL²/(8GAs), E = 30 000 MPa, G = E/2,4, As = 5/6·bh",
              "|UZ| giữa nhịp", "m", dB + dS, Math.Abs(r.Disp[2][2]), 0.5,
              $"Riêng số hạng uốn Euler 5qL⁴/384EI = {BenchmarkCase.Fmt(dB)} m; số hạng trượt = {BenchmarkCase.Fmt(dS)} m ({BenchmarkCase.Fmt(dS / dB * 100, "0.0")}%).")
        };
    }

    // ---- A02 ----
    private static List<BenchmarkCase> FixedBeam()
    {
        const string G = GroupAnalysis, T = "Dầm hai đầu ngàm chịu tải phân bố đều";
        double L = 6, q = 20, b = 0.30, h = 0.50;
        var p = NewModel(FrameSection.Rect("D30x50", 300, 500));
        AddNode(p, 1, 0, 0, 3, "111111");
        AddNode(p, 2, L / 2, 0, 3);
        AddNode(p, 3, L, 0, 3, "111111");
        AddFrame(p, 1, 1, 2, "D30x50"); AddFrame(p, 2, 2, 3, "D30x50");
        p.FrameLoads.Add(FrameLoad.Uniform(1, "P", q)); p.FrameLoads.Add(FrameLoad.Uniform(2, "P", q));
        var r = FrameAnalysis.Run(p).Get("P");
        double I = b * h * h * h / 12, As = 5.0 / 6 * b * h;
        double dB = q * Math.Pow(L, 4) / (384 * Eb * I), dS = q * L * L / 8 / (Gb * As);
        string desc = $"Dầm BTCT B25 {b * 1000:0}×{h * 1000:0} mm, nhịp L = {L} m, hai đầu ngàm, q = {q} kN/m; 2 phần tử.";
        return new List<BenchmarkCase>
        {
            C("A02-1", G, T, desc, "Cơ học kết cấu: Mngàm = −qL²/12", "M3 đầu ngàm", "kN.m", -q * L * L / 12, r.Frames[1].At(0)[5], 0.1),
            C("A02-2", G, T, desc, "Cơ học kết cấu: Mgiữa = qL²/24", "M3 giữa nhịp", "kN.m", q * L * L / 24, r.Frames[1].At(L / 2)[5], 0.1),
            C("A02-3", G, T, desc, "Dầm Timoshenko: δ = qL⁴/(384EI) + qL²/(8GAs)", "|UZ| giữa nhịp", "m", dB + dS, Math.Abs(r.Disp[2][2]), 0.5,
              $"Số hạng uốn Euler qL⁴/384EI = {BenchmarkCase.Fmt(dB)} m; số hạng trượt = {BenchmarkCase.Fmt(dS)} m.")
        };
    }

    /// <summary>Phản lực gối khớp của dầm một đầu ngàm – một đầu khớp nhịp L, tải đều q, kể biến dạng trượt (phương pháp lực):
    /// R = (qL⁴/8EI + qL²/2GAs) / (L³/3EI + L/GAs); Euler: R = 3qL/8.</summary>
    internal static double ProppedReaction(double q, double L, double EI, double GAs) =>
        (q * Math.Pow(L, 4) / (8 * EI) + q * L * L / (2 * GAs)) / (L * L * L / (3 * EI) + L / GAs);

    // ---- A03 ----
    private static List<BenchmarkCase> ProppedCantilever()
    {
        const string G = GroupAnalysis, T = "Dầm một đầu ngàm – một đầu khớp (tải đều)";
        double L = 8, q = 15, b = 0.30, h = 0.50;
        var p = NewModel(FrameSection.Rect("D30x50", 300, 500));
        AddNode(p, 1, 0, 0, 3, "111111");
        AddNode(p, 2, L, 0, 3, "001000");
        AddFrame(p, 1, 1, 2, "D30x50");
        p.FrameLoads.Add(FrameLoad.Uniform(1, "P", q));
        var r = FrameAnalysis.Run(p).Get("P");
        double EI = Eb * b * h * h * h / 12, GAs = Gb * 5.0 / 6 * b * h;
        double R = ProppedReaction(q, L, EI, GAs);
        double MA = q * L * L / 2 - R * L, Mspan = R * R / (2 * q);
        string desc = $"Dầm B25 {b * 1000:0}×{h * 1000:0} mm, L = {L} m, ngàm tại x = 0, gối di động tại x = L, q = {q} kN/m; 1 phần tử.";
        string src = "Phương pháp lực, dầm Timoshenko: R = (qL⁴/8EI + qL²/2GAs)/(L³/3EI + L/GAs)";
        return new List<BenchmarkCase>
        {
            C("A03-1", G, T, desc, src + " (Euler: R = 3qL/8)", "Phản lực FZ gối khớp", "kN", R, r.Reactions[2][2], 0.1,
              $"Euler–Bernoulli: 3qL/8 = {BenchmarkCase.Fmt(3 * q * L / 8)} kN."),
            C("A03-2", G, T, desc, "M ngàm = qL²/2 − R·L (Euler: −qL²/8)", "M3 đầu ngàm", "kN.m", -MA, r.Frames[1].At(0)[5], 0.1,
              $"Euler–Bernoulli: −qL²/8 = {BenchmarkCase.Fmt(-q * L * L / 8)} kN.m."),
            C("A03-3", G, T, desc, "Mmax nhịp = R²/(2q) (Euler: 9qL²/128)", "M3 lớn nhất trong nhịp", "kN.m", Mspan, MaxOver(r.Frames[1], 5), 0.2,
              $"Euler–Bernoulli: 9qL²/128 = {BenchmarkCase.Fmt(9 * q * L * L / 128)} kN.m; giá trị VXT = lớn nhất trên các mặt cắt cách 0,5 m (mặt cắt x = 5 m trùng vị trí cực trị).")
        };
    }

    // ---- A04 ----
    private static List<BenchmarkCase> CantileverPoint()
    {
        const string G = GroupAnalysis, T = "Dầm công xôn chịu lực tập trung ở đầu tự do";
        double L = 3, P = 40, b = 0.30, h = 0.50;
        var p = NewModel(FrameSection.Rect("D30x50", 300, 500));
        AddNode(p, 1, 0, 0, 3, "111111");
        AddNode(p, 2, L, 0, 3);
        AddFrame(p, 1, 1, 2, "D30x50");
        p.NodeLoads.Add(new NodeLoad { Node = 2, Pattern = "P", Fz = -P });
        var r = FrameAnalysis.Run(p).Get("P");
        double EI = Eb * b * h * h * h / 12, GAs = Gb * 5.0 / 6 * b * h;
        double dB = P * L * L * L / (3 * EI), dS = P * L / GAs;
        string desc = $"Công xôn B25 {b * 1000:0}×{h * 1000:0} mm, L = {L} m, lực P = {P} kN hướng xuống tại đầu tự do.";
        return new List<BenchmarkCase>
        {
            C("A04-1", G, T, desc, "Tĩnh học: M = −P·L", "M3 tại ngàm", "kN.m", -P * L, r.Frames[1].At(0)[5], 0.1),
            C("A04-2", G, T, desc, "Dầm Timoshenko: δ = PL³/(3EI) + PL/(GAs)", "|UZ| đầu tự do", "m", dB + dS, Math.Abs(r.Disp[2][2]), 0.5,
              $"Số hạng uốn PL³/3EI = {BenchmarkCase.Fmt(dB)} m; số hạng trượt = {BenchmarkCase.Fmt(dS)} m.")
        };
    }

    // ---- A05 ----
    private static List<BenchmarkCase> TwoSpan()
    {
        const string G = GroupAnalysis, T = "Dầm liên tục hai nhịp bằng nhau chịu tải đều";
        double L = 5, q = 20, b = 0.22, h = 0.40;
        var p = NewModel(FrameSection.Rect("D22x40", 220, 400));
        AddNode(p, 1, 0, 0, 3, "111100");
        AddNode(p, 2, L, 0, 3, "011100");
        AddNode(p, 3, 2 * L, 0, 3, "011100");
        AddFrame(p, 1, 1, 2, "D22x40"); AddFrame(p, 2, 2, 3, "D22x40");
        p.FrameLoads.Add(FrameLoad.Uniform(1, "P", q)); p.FrameLoads.Add(FrameLoad.Uniform(2, "P", q));
        var r = FrameAnalysis.Run(p).Get("P");
        double EI = Eb * b * h * h * h / 12, GAs = Gb * 5.0 / 6 * b * h;
        double R = ProppedReaction(q, L, EI, GAs);   // đối xứng: mỗi nhịp như dầm ngàm tại gối giữa
        double MB = -(q * L * L / 2 - R * L), Rmid = 2 * (q * L - R);
        string desc = $"Dầm B25 {b * 1000:0}×{h * 1000:0} mm, 2 nhịp × {L} m, gối khớp/di động, q = {q} kN/m trên cả hai nhịp.";
        string src = "Đối xứng → mỗi nhịp là dầm ngàm–khớp; dầm Timoshenko (Euler: ";
        return new List<BenchmarkCase>
        {
            C("A05-1", G, T, desc, src + "MB = −qL²/8)", "M3 tại gối giữa", "kN.m", MB, r.Frames[1].At(L)[5], 0.1,
              $"Euler–Bernoulli: −qL²/8 = {BenchmarkCase.Fmt(-q * L * L / 8)} kN.m."),
            C("A05-2", G, T, desc, src + "RA = 3qL/8)", "Phản lực FZ gối biên", "kN", R, r.Reactions[1][2], 0.1,
              $"Euler–Bernoulli: 3qL/8 = {BenchmarkCase.Fmt(3 * q * L / 8)} kN."),
            C("A05-3", G, T, desc, src + "RB = 5qL/4)", "Phản lực FZ gối giữa", "kN", Rmid, r.Reactions[2][2], 0.1,
              $"Euler–Bernoulli: 5qL/4 = {BenchmarkCase.Fmt(5 * q * L / 4)} kN.")
        };
    }

    /// <summary>Khung cổng một nhịp chân ngàm, cột cao h (EIc, GAsc), dầm nhịp L (EIb, GAsb), lực ngang H ở cao trình dầm; bỏ qua biến dạng dọc trục.
    /// Phương pháp chuyển vị (góc xoay – chuyển vị) với hệ số độ cứng thanh Timoshenko φ = 12EI/(GAs·l²):
    /// a = (4+φ)/(1+φ)·EI/l, b = (2−φ)/(1+φ)·EI/l, s = 6EI/((1+φ)l²); dầm đối xứng ngược: kb = 6EIb/((1+φb)L).
    /// Ẩn: góc xoay nút θ (hai nút bằng nhau) và chuyển vị ngang Δ.
    /// Cân bằng nút: (ac + kb)·θ = sc·Δ; cân bằng lực cắt tầng: 2·(2sc·Δ − (ac+bc)·θ)/h = H.
    /// → Δ = H·h / (2sc·(2 − (ac+bc)/(ac+kb))); M chân = sc·Δ − bc·θ; M đỉnh cột = M đầu dầm = kb·θ.
    /// Kiểm tra giới hạn: dầm cứng vô cùng → Δ = H·h³/(24EIc) (Euler), M chân = M đỉnh = H·h/4.</summary>
    public static (double Drift, double MBase, double MTop, double Theta) PortalClosedForm(double H, double h, double L,
        double EIc, double GAsc, double EIb, double GAsb)
    {
        double phic = 12 * EIc / (GAsc * h * h), phib = 12 * EIb / (GAsb * L * L);
        double ac = (4 + phic) / (1 + phic) * EIc / h, bc = (2 - phic) / (1 + phic) * EIc / h, sc = 6 * EIc / ((1 + phic) * h * h);
        double kb = 6 * EIb / ((1 + phib) * L);
        double D = H * h / (2 * sc * (2 - (ac + bc) / (ac + kb)));
        double th = sc * D / (ac + kb);
        return (D, sc * D - bc * th, kb * th, th);
    }

    // ---- A06 ----
    private static List<BenchmarkCase> PortalFrame()
    {
        const string G = GroupAnalysis, T = "Khung cổng một nhịp chân ngàm chịu lực ngang";
        double h = 3.6, L = 5.0, H = 20, bc = 0.30, hc = 0.30, bb = 0.22, hb = 0.50;
        var col = FrameSection.Rect("C30x30R", 300, 300); col.ModA = 1000;
        var bm = FrameSection.Rect("D22x50R", 220, 500); bm.ModA = 1000;
        var p = NewModel(col, bm);
        AddNode(p, 1, 0, 0, 0, "111111");
        AddNode(p, 2, 0, 0, h);
        AddNode(p, 3, L, 0, h);
        AddNode(p, 4, L, 0, 0, "111111");
        AddFrame(p, 1, 1, 2, "C30x30R", FrameKind.Column);
        AddFrame(p, 2, 4, 3, "C30x30R", FrameKind.Column);
        AddFrame(p, 3, 2, 3, "D22x50R", FrameKind.Beam);
        p.NodeLoads.Add(new NodeLoad { Node = 2, Pattern = "P", Fx = H });
        var r = FrameAnalysis.Run(p).Get("P");
        double EIc = Eb * bc * hc * hc * hc / 12, GAsc = Gb * 5.0 / 6 * bc * hc;
        double EIb = Eb * bb * hb * hb * hb / 12, GAsb = Gb * 5.0 / 6 * bb * hb;
        var cf = PortalClosedForm(H, h, L, EIc, GAsc, EIb, GAsb);
        string desc = $"Khung BTCT B25: cột {bc * 1000:0}×{hc * 1000:0} cao h = {h} m, dầm {bb * 1000:0}×{hb * 1000:0} nhịp L = {L} m "
                      + $"(Ib/Ic = {BenchmarkCase.Fmt(bb * hb * hb * hb / (bc * hc * hc * hc), "0.00")}), chân ngàm, lực ngang H = {H} kN tại nút đỉnh cột trái. "
                      + "Bỏ qua biến dạng dọc trục (hệ số độ cứng dọc trục ModA = 1000) để so với lời giải giáo khoa.";
        string src = "Phương pháp chuyển vị (góc xoay – chuyển vị), hệ số độ cứng thanh Timoshenko; tự lập (xem mục Phụ lục)";
        return new List<BenchmarkCase>
        {
            C("A06-1", G, T, desc, src, "Chuyển vị ngang đỉnh UX", "m", cf.Drift, r.Disp[2][0], 0.5,
              $"Giới hạn dầm cứng vô cùng (Euler): H·h³/(24EIc) = {BenchmarkCase.Fmt(H * h * h * h / (24 * EIc))} m."),
            C("A06-2", G, T, desc, src, "|M3| chân cột", "kN.m", cf.MBase, Math.Abs(r.Frames[1].At(0)[5]), 0.5),
            C("A06-3", G, T, desc, src, "|M3| đầu dầm = đỉnh cột", "kN.m", cf.MTop, Math.Abs(r.Frames[3].At(0)[5]), 0.5,
              $"Kiểm tra cân bằng: M chân + M đỉnh = H·h/2 = {BenchmarkCase.Fmt(H * h / 2)} kN.m (tham chiếu {BenchmarkCase.Fmt(cf.MBase + cf.MTop)})."),
            C("A06-4", G, T, desc, "Phản đối xứng, cột giống nhau: Vchân = H/2", "Phản lực FX chân cột trái", "kN", -H / 2, r.Reactions[1][0], 0.1)
        };
    }

    // ---- A07 ----
    private static List<BenchmarkCase> AxialColumn()
    {
        const string G = GroupAnalysis, T = "Cột chịu nén đúng tâm – biến dạng dọc trục";
        double H = 3.0, P = 600, b = 0.30;
        var p = NewModel(FrameSection.Rect("C30x30", 300, 300));
        AddNode(p, 1, 0, 0, 0, "111111");
        AddNode(p, 2, 0, 0, H);
        AddFrame(p, 1, 1, 2, "C30x30", FrameKind.Column);
        p.NodeLoads.Add(new NodeLoad { Node = 2, Pattern = "P", Fz = -P });
        var r = FrameAnalysis.Run(p).Get("P");
        string desc = $"Cột B25 {b * 1000:0}×{b * 1000:0} mm cao {H} m, ngàm chân, lực nén P = {P} kN tại đỉnh.";
        return new List<BenchmarkCase>
        {
            C("A07-1", G, T, desc, "Định luật Hooke: Δ = −P·L/(E·A)", "UZ đỉnh cột", "m", -P * H / (Eb * b * b), r.Disp[2][2], 0.1),
            C("A07-2", G, T, desc, "Cân bằng: N = −P (nén âm)", "Lực dọc P trong cột", "kN", -P, r.Frames[1].At(H / 2)[0], 0.1)
        };
    }

    /// <summary>Hằng số xoắn Saint-Venant chính xác của tiết diện chữ nhật a ≥ c (chuỗi Fourier, lý thuyết đàn hồi – Timoshenko &amp; Goodier):
    /// J = a·c³/3·[1 − 192·c/(π⁵·a)·Σ(n lẻ) tanh(n·π·a/(2c))/n⁵].</summary>
    public static double TorsionConstantExact(double b, double h)
    {
        double a = Math.Max(b, h), c = Math.Min(b, h), s = 0;
        for (int n = 1; n < 200; n += 2) s += Math.Tanh(n * Math.PI * a / (2 * c)) / Math.Pow(n, 5);
        return a * c * c * c / 3 * (1 - 192 * c / (Math.Pow(Math.PI, 5) * a) * s);
    }

    // ---- A08 ----
    private static List<BenchmarkCase> TorsionCantilever()
    {
        const string G = GroupAnalysis, T = "Thanh công xôn chịu xoắn";
        double L = 4, Tq = 10, b = 0.30, h = 0.50;
        var p = NewModel(FrameSection.Rect("D30x50", 300, 500));
        AddNode(p, 1, 0, 0, 3, "111111");
        AddNode(p, 2, L, 0, 3);
        AddFrame(p, 1, 1, 2, "D30x50");
        p.NodeLoads.Add(new NodeLoad { Node = 2, Pattern = "P", Mx = Tq });
        var r = FrameAnalysis.Run(p).Get("P");
        double J = TorsionConstantExact(b, h);
        string desc = $"Thanh B25 {b * 1000:0}×{h * 1000:0} mm dài {L} m theo phương X, ngàm một đầu, mô men xoắn T = {Tq} kN.m tại đầu tự do.";
        return new List<BenchmarkCase>
        {
            C("A08-1", G, T, desc, "Xoắn Saint-Venant: φ = T·L/(G·J), J chính xác theo chuỗi Fourier", "Góc xoắn RX đầu tự do", "rad",
              Tq * L / (Gb * J), r.Disp[2][3], 1.0,
              $"J chính xác = {BenchmarkCase.Fmt(J)} m⁴; chương trình dùng J = β·a·c³ với β gần đúng (Roark) – sai khác nhỏ hơn 0,5%."),
            C("A08-2", G, T, desc, "Tĩnh học: MX = −T", "Phản lực MX tại ngàm", "kN.m", -Tq, r.Reactions[1][3], 0.1)
        };
    }

    // ---- A09 ----
    private static Project QuarterArc(double R, double q, int segs)
    {
        var p = NewModel(FrameSection.Rect("D30x50", 300, 500));
        p.Stories.Add(new Story { Name = "T1", Elevation = 3, Height = 3 });
        AddNode(p, 1, R, 0, 3, "111111").Story = "T1";
        AddNode(p, 2, 0, R, 3).Story = "T1";
        double s = CurvedBeam.SagittaThrough(R, 0, 0, R, R / Math.Sqrt(2), R / Math.Sqrt(2));
        var f = AddFrame(p, 1, 1, 2, "D30x50");
        f.Story = "T1"; f.CurveMidOffset = s; f.CurveSegments = segs;
        p.FrameLoads.Add(FrameLoad.Uniform(1, "P", q));
        return p;
    }

    private static List<BenchmarkCase> CurvedCantilever()
    {
        const string G = GroupAnalysis, T = "Dầm cong 1/4 đường tròn công xôn (mặt bằng) chịu tải đều";
        double R = 4, q = 10, half = Math.PI / 2;
        var p9 = QuarterArc(R, q, 0);
        var r9 = FrameAnalysis.Run(p9).Get("P");
        var p36 = QuarterArc(R, q, 36);
        var r36 = FrameAnalysis.Run(p36).Get("P");
        double L = R * half;
        var f9 = r9.Frames[1]; var f36 = r36.Frames[1];
        int nAuto = CurvedBeam.SegmentCount(p9.Frames[0], CurvedBeam.Arc(p9, p9.Frames[0]));
        string desc = $"Dầm cong B25 300×500 trên mặt bằng, bán kính R = {R} m, góc ở tâm 90°, ngàm một đầu, tải đứng q = {q} kN/m theo chiều dài cung. "
                      + $"Chương trình chia cung thành các đoạn thẳng: tự động ({nAuto} đoạn, ≤ 10°/đoạn) và 36 đoạn (độ nhạy chia đoạn).";
        string src = "Dầm cong tròn: M(θ) = qR²(1 − cos θ), T(θ) = qR²(θ − sin θ), θ đo từ đầu tự do";
        double th = Math.PI / 4;
        return new List<BenchmarkCase>
        {
            C("A09-1", G, T, desc, src + " – θ = π/2", $"|M3| tại ngàm ({nAuto} đoạn)", "kN.m", q * R * R, Math.Abs(f9.At(0)[5]), 2.0,
              "Sai số do thay cung bằng các đoạn dây cung – giảm khi chia mịn (xem dòng 36 đoạn)."),
            C("A09-2", G, T, desc, src + " – θ = π/2", $"|T| tại ngàm ({nAuto} đoạn)", "kN.m", q * R * R * (half - 1), Math.Abs(f9.At(0)[3]), 2.0),
            C("A09-3", G, T, desc, src + " – θ = π/2", "|M3| tại ngàm (36 đoạn)", "kN.m", q * R * R, Math.Abs(f36.At(0)[5]), 0.5),
            C("A09-4", G, T, desc, src + " – θ = π/2", "|T| tại ngàm (36 đoạn)", "kN.m", q * R * R * (half - 1), Math.Abs(f36.At(0)[3]), 0.5),
            C("A09-5", G, T, desc, src + " – θ = π/4", "|M3| giữa cung (36 đoạn)", "kN.m", q * R * R * (1 - Math.Cos(th)), Math.Abs(f36.At(L / 2)[5]), 1.0),
            C("A09-6", G, T, desc, src + " – θ = π/4", "|T| giữa cung (36 đoạn)", "kN.m", q * R * R * (th - Math.Sin(th)), Math.Abs(f36.At(L / 2)[3]), 2.0,
              "Giá trị T giữa cung nhỏ (≈ 0,08·qR²) nên sai số tương đối lớn hơn; ngưỡng 2%."),
            C("A09-7", G, T, desc, "Cân bằng: FZ = q·πR/2 (chiều dài cung)", "Phản lực FZ tại ngàm", "kN", q * L, r9.Reactions[1][2], 0.01)
        };
    }

    /// <summary>Dầm dài vô hạn trên nền Winkler chịu lực tập trung P (Hetényi): λ = (k·B/(4EI))^¼, M0 = P/(4λ), w0 = P·λ/(2k·B).</summary>
    public static (double Lambda, double M0, double W0) WinklerClosedForm(double P, double EI, double k, double B)
    {
        double lam = Math.Pow(k * B / (4 * EI), 0.25);
        return (lam, P / (4 * lam), P * lam / (2 * k * B));
    }

    // ---- A10 ----
    private static List<BenchmarkCase> WinklerInfinite()
    {
        const string G = GroupAnalysis, T = "Dầm dài vô hạn trên nền Winkler chịu lực tập trung";
        double L = 40, b = 0.4, h = 0.6, k = 20000, B = 1.0, P = 300;
        double EI = Eb * b * h * h * h / 12;
        var cf = WinklerClosedForm(P, EI, k, B);
        var res = new List<BenchmarkCase>();
        string desc = $"Dầm móng EI = Eb·{b}·{h}³/12 = {BenchmarkCase.Fmt(EI, "0")} kN.m², dài {L} m (λL/2 = {BenchmarkCase.Fmt(cf.Lambda * L / 2, "0.0")} ≫ π – coi như vô hạn), "
                      + $"hệ số nền k = {k} kN/m³, bề rộng đáy B = {B} m, lực P = {P} kN ở giữa. Mô hình dầm trên nền đàn hồi của móng băng (StripFootingDesigner.WinklerBeam): "
                      + "phần tử Euler, lò xo tập trung k·B·a tại nút.";
        string src = "Hetényi (1946): M0 = P/(4λ), w0 = Pλ/(2kB), λ = (kB/4EI)^¼";
        foreach (var (dx, tol, tag) in new[] { (0.10, 0.5, "1"), (0.50, 1.0, "2") })
        {
            int n = (int)Math.Round(L / dx) + 1;
            var xs = Enumerable.Range(0, n).Select(i => i * dx).ToList();
            var beam = new StripFootingDesigner.WinklerBeam(xs, EI, k, B);
            var fz = new double[n]; var mt = new double[n];
            int mid = n / 2;
            fz[mid] = -P;
            var sol = beam.Solve(fz, mt);
            res.Add(C($"A10-{tag}a", G, T, desc, src, $"Mô men dưới lực M0 (phần tử {dx * 1000:0} mm)", "kN.m", cf.M0, sol.M[mid], tol,
                dx > 0.2 ? "Độ nhạy chia phần tử: lò xo tập trung tại nút cho sai số mô men ≈ −0,6% với phần tử 0,5 m." : ""));
            res.Add(C($"A10-{tag}b", G, T, desc, src, $"Độ lún dưới lực w0 (phần tử {dx * 1000:0} mm)", "m", cf.W0, -sol.W[mid], tol));
        }
        return res;
    }

    // ---- A11 ----
    private static List<BenchmarkCase> TaperedColumn()
    {
        const string G = GroupAnalysis, T = "Cột thép tiết diện thay đổi (vát) công xôn – độ nhạy chia đoạn";
        double H = 6, P = 10, h1 = 0.30, h2 = 0.60, bf = 0.20, tf = 0.010, tw = 0.006;
        // tham chiếu: nguyên lý công khả dĩ δ = ∫ M·m/EI dx + ∫ V·v/(G·Aw) dx, tích phân số trung điểm 20 000 đoạn
        double dRef = 0; int nInt = 20000;
        for (int i = 0; i < nInt; i++)
        {
            double x = (i + 0.5) * H / nInt, hx = h1 + (h2 - h1) * x / H, hw = hx - 2 * tf;
            double I = 2 * (bf * tf * tf * tf / 12 + bf * tf * Math.Pow((hx - tf) / 2, 2)) + tw * hw * hw * hw / 12;
            double Aw = hx * tw;
            dRef += (P * (H - x) * (H - x) / (Esteel * I) + P / (Gsteel * Aw)) * H / nInt;
        }
        var res = new List<BenchmarkCase>();
        string desc = $"Cột thép SS400 chữ I tổ hợp: h = {h1 * 1000:0} → {h2 * 1000:0} mm (chân → đỉnh), cánh {bf * 1000:0}×{tf * 1000:0}, bụng {tw * 1000:0} mm, cao {H} m, ngàm chân, "
                      + $"lực ngang P = {P} kN tại đỉnh. Chương trình chia thanh vát thành các đoạn lăng trụ (đặc trưng tại giữa đoạn).";
        string src = "Nguyên lý công khả dĩ: δ = ∫M·m/EI dx + ∫V·v/(G·h·tw) dx, tích phân số 20 000 đoạn";
        foreach (var (segs, tol, tag) in new[] { (6, 3.0, "1"), (24, 0.5, "2") })
        {
            var p = NewModel(FrameSection.WeldedISection("V", h1 * 1000, h2 * 1000, bf * 1000, tf * 1000, tw * 1000));
            p.Steel.TaperSegments = segs;
            AddNode(p, 1, 0, 0, 0, "111111");
            AddNode(p, 2, 0, 0, H);
            AddFrame(p, 1, 1, 2, "V", FrameKind.SteelColumn);
            p.NodeLoads.Add(new NodeLoad { Node = 2, Pattern = "P", Fx = P });
            var r = FrameAnalysis.Run(p).Get("P");
            res.Add(C($"A11-{tag}", G, T, desc, src, $"UX đỉnh cột ({segs} đoạn)", "m", dRef, r.Disp[2][0], tol,
                segs == 6 ? "Mặc định 6 đoạn (Thiết lập thép → số đoạn chia thanh vát): sai số ≈ −1,7% (thiên về độ cứng lớn) – tăng số đoạn để chính xác hơn." : ""));
        }
        return res;
    }

    /// <summary>Diện tích đa giác (công thức Gauss – shoelace) trên mặt bằng XY.</summary>
    internal static double Shoelace(IList<(double X, double Y)> pts)
    {
        double a = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var (x1, y1) = pts[i]; var (x2, y2) = pts[(i + 1) % pts.Count];
            a += x1 * y2 - x2 * y1;
        }
        return Math.Abs(a) / 2;
    }

    /// <summary>Hệ số k(z) theo dạng hàm mũ chương trình dùng (TCVN 2737:1995 Bảng 5 lập từ công thức này): k = 1,844·(z/zg)^(2α),
    /// z ≥ 3 m; A: α = 0,07, zg = 250 m; B: α = 0,09, zg = 300 m; C: α = 0,14, zg = 400 m. Viết lại độc lập để kiểm chứng.</summary>
    public static double HandKz(double z, string terrain)
    {
        double alpha, zg;
        switch ((terrain ?? "B").Trim().ToUpperInvariant())
        {
            case "A": alpha = 0.07; zg = 250; break;
            case "C": alpha = 0.14; zg = 400; break;
            default: alpha = 0.09; zg = 300; break;
        }
        return 1.844 * Math.Exp(2 * alpha * Math.Log(Math.Max(z, 3.0) / zg));
    }

    /// <summary>Lực gió tầng tính tay: F = W0·k(z)·c·hệ số·h·B (h = nửa tầng dưới + nửa tầng trên).</summary>
    internal static List<(string Story, double Z, double K, double H, double F)> HandWindForces(Project p, double width)
    {
        var st = p.Stories.Where(s => s.Elevation > p.BaseElevation + 1e-3).OrderBy(s => s.Elevation).ToList();
        var l = new List<(string, double, double, double, double)>();
        for (int i = 0; i < st.Count; i++)
        {
            double below = st[i].Elevation - (i == 0 ? p.BaseElevation : st[i - 1].Elevation);
            double above = i == st.Count - 1 ? 0 : st[i + 1].Elevation - st[i].Elevation;
            double z = st[i].Elevation - p.BaseElevation, k = HandKz(z, p.Wind.Terrain), hh = below / 2 + above / 2;
            l.Add((st[i].Name, z, k, hh, p.Wind.W0 * k * p.Wind.Cp * p.Wind.Factor * hh * width));
        }
        return l;
    }

    // ---- A12 (+ D02, D03) ----
    /// <summary>Khung 3 tầng 2×1 nhịp sinh từ lưới trục (ModelBuilder.BuildFromGrid) – dùng chung cho A12, D02, D03.</summary>
    internal static Project StoreyModel()
    {
        var p = new Project { Name = "Kiểm chứng khung 3 tầng", Location = "Kiểm chứng" };
        foreach (var c in new[] { "B25" }) p.Materials.Add(Material.Concrete(c));
        foreach (var r in new[] { "CB240-T", "CB400-V" }) p.Materials.Add(Material.Rebar(r));
        p.FrameSections.Add(FrameSection.Rect("C30x30", 300, 300));
        p.FrameSections.Add(FrameSection.Rect("D22x40", 220, 400));
        p.SlabSections.Add(new SlabSection { Name = "S120", Thickness = 0.12, Material = "B25" });
        p.Patterns.Add(new LoadPattern { Name = "TT", Type = LoadType.Dead, SelfWeight = 1, Gamma = 1.1 });
        p.Patterns.Add(new LoadPattern { Name = "HTHT", Type = LoadType.SuperDead, Gamma = 1.3 });
        p.Patterns.Add(new LoadPattern { Name = "HT", Type = LoadType.Live, Gamma = 1.3 });
        p.Patterns.Add(new LoadPattern { Name = "GX", Type = LoadType.Wind, Gamma = 1.2 });
        p.Wind.Enabled = true; p.Wind.W0 = 0.95; p.Wind.Terrain = "B"; p.Wind.Cp = 1.4; p.Wind.Factor = 1.0;
        p.Wind.WidthForWindX = 0; p.Wind.WidthForWindY = 0;
        p.Seismic.Enabled = false;
        ModelBuilder.SetGrid(p, new[] { 4.0, 4.5 }, new[] { 5.0 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3, 3.3 }, 0.0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions
        {
            ColumnSection = "C30x30", BeamSectionX = "D22x40", BeamSectionY = "D22x40", SlabSection = "S120",
            FinishLoad = 1.2, LiveLoad = 1.5, RoofLiveLoad = 0.75, PerimeterWallLoad = 7.5, FixedBase = true
        });
        return p;
    }

    private static double SlabArea(Project p, Slab s) => Shoelace(s.Nodes.Select(id => (p.Node(id).X, p.Node(id).Y)).ToList());

    private static List<BenchmarkCase> StoreyEquilibrium()
    {
        const string G = GroupAnalysis, T = "Khung nhà 3 tầng – cân bằng phản lực với tổng tải";
        var p = StoreyModel();
        // tổng tải tính tay từ DỮ LIỆU NHẬP (trước khi chương trình dồn tải sàn / sinh tải gió)
        double sw = 0;
        foreach (var f in p.Frames)
        {
            var sec = p.Section(f.Section);
            var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
            double len = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2) + Math.Pow(b.Z - a.Z, 2));
            sw += 25.0 * sec.B * sec.H * len;
        }
        double slabSw = p.Slabs.Sum(s => 25.0 * p.SlabSections.First(x => x.Name == s.Section).Thickness * SlabArea(p, s));
        double LineLoads(string pat) => p.FrameLoads.Where(l => l.Pattern == pat && l.Source == "" && l.Dir == LoadDir.Gravity)
            .Sum(l => l.Points[0][1] * p.Length(p.Frame(l.Element)));
        double AreaSum(string pat) => p.AreaLoads.Where(a => a.Pattern == pat).Sum(a => a.Q * SlabArea(p, p.Slabs.First(s => s.Id == a.Slab)));
        double tt = sw + slabSw + LineLoads("TT") + AreaSum("TT");
        double htht = LineLoads("HTHT") + AreaSum("HTHT");
        double ht = LineLoads("HT") + AreaSum("HT");
        double width = p.Nodes.Max(n => n.Y) - p.Nodes.Min(n => n.Y);
        double gx = HandWindForces(p, width).Sum(x => x.F);

        var res = FrameAnalysis.Run(p);
        double SumR(string pat, int d) => res.Get(pat).Reactions.Values.Sum(r => r[d]);
        string desc = $"Khung BTCT 3 tầng (3,6 + 3,3 + 3,3 m), lưới 2 nhịp × 1 nhịp (4,0 + 4,5) × 5,0 m, cột 300×300, dầm 220×400, sàn 120 mm, "
                      + $"{p.Nodes.Count} nút, {p.Frames.Count} thanh, {p.Slabs.Count} ô sàn; tường biên 7,5 kN/m; hoàn thiện 1,2; hoạt tải 1,5 (mái 0,75) kN/m²; gió W0 = 0,95 kN/m², địa hình B, c = 1,4.";
        string src = "Cân bằng tĩnh học toàn khối: ΣR = −Σ tải; tổng tải tính tay từ dữ liệu nhập (γ·A·L thanh, γ·t·A sàn theo diện tích đa giác, q·A, q·L, lực gió tầng)";
        return new List<BenchmarkCase>
        {
            C("A12-1", G, T, desc, src, "ΣFZ phản lực – TT (bản thân dầm, cột, sàn)", "kN", tt, SumR("TT", 2), 0.01),
            C("A12-2", G, T, desc, src, "ΣFZ phản lực – HTHT (tường + hoàn thiện)", "kN", htht, SumR("HTHT", 2), 0.01),
            C("A12-3", G, T, desc, src, "ΣFZ phản lực – HT (hoạt tải sàn, mái)", "kN", ht, SumR("HT", 2), 0.01),
            C("A12-4", G, T, desc, src + "; lực gió tầng theo D02", "ΣFX phản lực – GX", "kN", -gx, SumR("GX", 0), 0.01)
        };
    }
}

using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Detailing;

// =====================================================================
// MẶT BẰNG SÀN (nhiều ô trên lưới dầm) – chế độ Triển khai hồ sơ.
// • Hạ cốt từng ô (WC, ban công…), lỗ thông tầng (cả ô), khoét lỗ thủng trong ô.
// • Cạnh ô liên tục (ngàm) khi ô bên cạnh có sàn và chênh cốt ≤ ngưỡng bậc; ngược lại khớp.
// • Thép mũ trên dầm giữa 2 ô: thẳng (cùng cốt), NHẤN (bẻ xiên 1:6) khi chênh cốt ≤ ngưỡng nhấn, hoặc cắt – neo riêng từng ô.
// • Chế độ "Tự động theo diện tích": chương trình chọn thép; "Thủ công": người dùng khai báo vùng thép, chương trình chỉ kiểm tra.
// Đơn vị: m, kN; Δ hạ cốt lưu m (âm = thấp hơn), hiển thị mm.
// =====================================================================

/// <summary>Khai báo riêng một ô sàn (cột i, hàng j tính từ 1).</summary>
public class SlabPanelRow
{
    [Field("Cột (phương X, từ 1)")] public int Col { get; set; } = 1;
    [Field("Hàng (phương Y, từ 1)")] public int Row { get; set; } = 1;
    [Field("Hạ cốt Δ (âm = thấp hơn)", Unit = "mm", Scale = 1000)] public double Drop { get; set; }
    [Field("Chiều dày (0 = chung)", Unit = "mm", Scale = 1000)] public double Thickness { get; set; }
    [Field("Công năng", Choices = "Phòng|Vệ sinh|Ban công|Hành lang|Mái|Lỗ thông tầng (không sàn)")] public string Function { get; set; } = "Phòng";
    [Field("Ghi chú")] public string Note { get; set; } = "";
}

/// <summary>Lỗ khoét trong ô sàn (toạ độ góc dưới trái tính từ giao trục đầu tiên).</summary>
public class SlabOpening
{
    [Field("Tên lỗ")] public string Name { get; set; } = "L1";
    [Field("X góc dưới trái", Unit = "m")] public double X { get; set; }
    [Field("Y góc dưới trái", Unit = "m")] public double Y { get; set; }
    [Field("Rộng theo X", Unit = "mm", Scale = 1000)] public double W { get; set; } = 0.6;
    [Field("Cao theo Y", Unit = "mm", Scale = 1000)] public double H { get; set; } = 0.6;
}

/// <summary>Một vùng thép vẽ thủ công: thanh chạy theo phương Direction, rải trong hình chữ nhật (X1,Y1)–(X2,Y2).</summary>
public class SlabBarRow
{
    [Field("Lớp", Choices = "Dưới|Trên")] public string Layer { get; set; } = "Dưới";
    [Field("Phương thanh", Choices = "X|Y")] public string Direction { get; set; } = "X";
    [Field("Ø", Unit = "mm")] public int Dia { get; set; } = 8;
    [Field("Bước", Unit = "mm", Scale = 1000)] public double Spacing { get; set; } = 0.20;
    [Field("X1", Unit = "m")] public double X1 { get; set; }
    [Field("Y1", Unit = "m")] public double Y1 { get; set; }
    [Field("X2", Unit = "m")] public double X2 { get; set; }
    [Field("Y2", Unit = "m")] public double Y2 { get; set; }
    [Field("Ghi chú / tên")] public string Label { get; set; } = "";
}

public class ManualSlabPlan : IDetailMember
{
    public const string ModeAuto = "Tự động theo diện tích";
    public const string ModeManual = "Thủ công";
    public const string CrankBend = "Nhấn thép (bẻ xiên 1:6) khi Δ ≤ ngưỡng nhấn";
    public const string CrankCut = "Cắt thép, neo riêng từng ô";
    public const string VoidFunction = "Lỗ thông tầng (không sàn)";

    [Field("Tên mặt bằng sàn", Group = "Chung")] public string Name { get; set; } = "SAN1";
    [Field("Số lượng (tầng giống nhau)", Group = "Chung")] public int Quantity { get; set; } = 1;
    [Field("Khoảng cách trục phương X", Unit = "m", Group = "Lưới dầm", Hint = "vd. 4,2; 3,6 (trục 1, 2, 3…)")] public string SpansX { get; set; } = "4,2; 3,6";
    [Field("Khoảng cách trục phương Y", Unit = "m", Group = "Lưới dầm", Hint = "vd. 4,5; 3,3 (trục A, B, C…)")] public string SpansY { get; set; } = "4,5; 3,3";
    [Field("Bề rộng dầm", Unit = "mm", Scale = 1000, Group = "Lưới dầm")] public double BeamWidth { get; set; } = 0.22;
    [Field("Chiều cao dầm (vẽ mặt cắt)", Unit = "mm", Scale = 1000, Group = "Lưới dầm")] public double BeamDepth { get; set; } = 0.40;
    [Field("Chiều dày sàn chung", Unit = "mm", Scale = 1000, Group = "Sàn")] public double T { get; set; } = 0.10;
    [Field("Tĩnh tải hoàn thiện (tiêu chuẩn)", Unit = "kN/m²", Group = "Sàn")] public double Finish { get; set; } = 1.5;
    [Field("Hoạt tải (tiêu chuẩn)", Unit = "kN/m²", Group = "Sàn")] public double Live { get; set; } = 2.0;
    [Field("Cách bố trí thép", Group = "Thép", Choices = ModeAuto + "|" + ModeManual, Hint = "Thủ công: khai báo vùng thép ở bảng dưới, chương trình chỉ kiểm tra")] public string Mode { get; set; } = ModeAuto;
    [Field("Ngưỡng bậc sàn (coi là liên tục)", Unit = "mm", Scale = 1000, Group = "Hạ cốt")] public double StepThreshold { get; set; } = 0.030;
    [Field("Thép mũ qua dầm có chênh cốt", Group = "Hạ cốt", Choices = CrankBend + "|" + CrankCut)] public string CrankMode { get; set; } = CrankBend;
    [Field("Ngưỡng nhấn thép (|Δ| tối đa)", Unit = "mm", Scale = 1000, Group = "Hạ cốt")] public double CrankLimit { get; set; } = 0.050;

    public List<SlabPanelRow> Panels { get; set; } = new();
    public List<SlabOpening> Openings { get; set; } = new();
    public List<SlabBarRow> ManualBars { get; set; } = new();

    public static readonly int[] Dias = { 6, 8, 10, 12 };

    /// <summary>Mẫu: lưới 2×2, ô (2,1) vệ sinh hạ −50, lỗ 0,8×0,8 ở ô (1,1), bố trí tự động.</summary>
    public static ManualSlabPlan Sample() => new()
    {
        Panels = { new SlabPanelRow { Col = 2, Row = 1, Drop = -0.05, Function = "Vệ sinh", Note = "WC" } },
        Openings = { new SlabOpening { Name = "L1", X = 1.5, Y = 1.5, W = 0.8, H = 0.8 } }
    };

    // ------------------------------------------------------------------
    // Hình học, kết quả trung gian (dùng cho bản vẽ, kiểm thử)
    // ------------------------------------------------------------------
    public sealed class Panel
    {
        public int I, J;
        public double X0, X1, Y0, Y1, Drop, T, Q;
        public string Function = "Phòng";
        public bool Void;
        public bool FixL, FixR, FixB, FixT;
        public double MxSpan, MxSup, MySpan, MySup;
        public (int D, double S) BotX, BotY;
        public BarMark BarX, BarY;
        public double Lx => X1 - X0;
        public double Ly => Y1 - Y0;
        public bool Wet => Function == "Vệ sinh" || Function == "Ban công" || Function == "Mái";
        public string Label => (I + 1) + "-" + (J + 1);
    }

    /// <summary>Thép trên một đoạn dầm (đường trục): Vertical = dầm theo phương Y (thép chạy X).</summary>
    public sealed class TopLine
    {
        public bool Vertical;          // dầm dọc trục X = xs[K] (thép chạy theo X)
        public int K, Seg;             // chỉ số trục, đoạn
        public double Pos, From, To;   // toạ độ trục, đoạn dầm (m)
        public Panel A, B;             // A: trái / dưới, B: phải / trên (null = biên)
        public bool Continuous;        // hai ô liên tục (chênh ≤ ngưỡng bậc)
        public double DeltaDiff;       // |ΔA − ΔB| (m)
        public string Kind = "";       // "Thẳng", "Nhấn", "Neo riêng", "Biên"
        public double M;               // mô men gối (kN.m/m), 0 = cấu tạo
        public (int D, double S) Bar;
        public double Ext;             // chiều dài mỗi bên tính từ tim dầm (m)
        public List<BarMark> Marks = new();
        public string AxisName = "";
    }

    [System.Text.Json.Serialization.JsonIgnore] public double[] Xs { get; private set; } = Array.Empty<double>();
    [System.Text.Json.Serialization.JsonIgnore] public double[] Ys { get; private set; } = Array.Empty<double>();
    [System.Text.Json.Serialization.JsonIgnore] public List<Panel> PanelList { get; private set; } = new();
    [System.Text.Json.Serialization.JsonIgnore] public List<TopLine> TopLines { get; private set; } = new();

    public static string AxisX(int k) => (k + 1).ToString(Dt.Inv);
    public static string AxisY(int k) => k < 26 ? ((char)('A' + k)).ToString() : "A" + (k - 25).ToString(Dt.Inv);

    private static double DeflCoef(int f) => f >= 2 ? 1.0 : f == 1 ? 2.08 : 5.0;

    /// <summary>Hình học nhấn thép: đoạn xiên theo độ dốc 1:6 qua chênh cốt Δ → (dài ngang, dài xiên).</summary>
    public static (double Run, double Incline) Crank(double delta)
    {
        double d = Math.Abs(delta), run = 6 * d;
        return (run, Math.Sqrt(run * run + d * d));
    }

    private Panel PanelAt(int i, int j) => i < 0 || j < 0 || i >= Xs.Length - 1 || j >= Ys.Length - 1 ? null : PanelList.FirstOrDefault(p => p.I == i && p.J == j);

    /// <summary>Panel chứa điểm (x, y) (m) – null nếu ngoài lưới.</summary>
    public Panel PanelContaining(double x, double y) => PanelList.FirstOrDefault(p => x >= p.X0 - 1e-9 && x <= p.X1 + 1e-9 && y >= p.Y0 - 1e-9 && y <= p.Y1 + 1e-9);

    private void BuildGeometry()
    {
        var sx = DetailShim.ParseList(SpansX); var sy = DetailShim.ParseList(SpansY);
        Xs = new double[sx.Count + 1]; Ys = new double[sy.Count + 1];
        for (int i = 0; i < sx.Count; i++) Xs[i + 1] = Xs[i] + sx[i];
        for (int j = 0; j < sy.Count; j++) Ys[j + 1] = Ys[j] + sy[j];
        PanelList = new List<Panel>();
        TopLines = new List<TopLine>();
        if (sx.Count == 0 || sy.Count == 0) return;
        for (int j = 0; j < sy.Count; j++)
            for (int i = 0; i < sx.Count; i++)
            {
                var row = (Panels ?? new()).LastOrDefault(r => r != null && r.Col == i + 1 && r.Row == j + 1);
                var p = new Panel
                {
                    I = i, J = j, X0 = Xs[i], X1 = Xs[i + 1], Y0 = Ys[j], Y1 = Ys[j + 1],
                    Drop = row != null ? Math.Min(row.Drop, 0) : 0,
                    T = row != null && row.Thickness > 0.04 ? row.Thickness : T,
                    Function = row?.Function ?? "Phòng"
                };
                p.Void = p.Function == VoidFunction;
                PanelList.Add(p);
            }
    }

    private bool Continuous(Panel a, Panel b) => a != null && b != null && !a.Void && !b.Void && Math.Abs(a.Drop - b.Drop) <= StepThreshold + 1e-9;

    public DetailResult Run(DetailSettings s)
    {
        var r = new DetailResult { Kind = "Mặt bằng sàn", Name = Name, Quantity = Math.Max(1, Quantity) };
        try { Design(s, r); }
        catch (Exception e) { r.Messages.Add("LỖI tính toán mặt bằng sàn: " + e.Message); }
        return r;
    }

    private void Design(DetailSettings s, DetailResult r)
    {
        s ??= new DetailSettings();
        var conc = DetailShim.TryConcrete(s.Concrete, r.Messages);
        var small = DetailShim.TryRebar(s.RebarSmall, r.Messages);
        var main = DetailShim.TryRebar(s.RebarMain, r.Messages);
        Material St(int d) => d <= 10 ? small : main;
        r.ConcreteClass = conc.Name;
        BuildGeometry();
        if (PanelList.Count == 0 || T < 0.05 || BeamWidth < 0 || Xs.Skip(1).Zip(Xs, (a, b) => a - b).Any(v => v < 0.5) || Ys.Skip(1).Zip(Ys, (a, b) => a - b).Any(v => v < 0.5))
        {
            r.Messages.Add("LỖI lưới dầm / chiều dày sàn không hợp lệ (khoảng cách trục ≥ 0,5 m, vd. \"4,2; 3,6\"; t ≥ 50 mm)");
            return;
        }
        double c = s.CoverSlab, bw = BeamWidth;
        bool manual = Mode == ModeManual;
        r.Calc.Add("## Mặt bằng sàn " + Name + ": lưới " + (Xs.Length - 1) + " × " + (Ys.Length - 1) + " ô, trục X " + SpansX + " m, trục Y " + SpansY + " m, t = " + Dt.Mm(T)
                   + " mm, bê tông " + conc.Name + " – " + (manual ? "bố trí thép THỦ CÔNG (chương trình kiểm tra)" : "bố trí thép TỰ ĐỘNG theo nội lực từng ô"));
        r.Calc.Add("Cạnh ô liên tục (ngàm) khi ô kề có sàn và |ΔA − ΔB| ≤ " + Dt.Mm(StepThreshold) + " mm; ngược lại tính khớp. Phân tải Marcus, dải 1 m: khớp–khớp qL²/8; ngàm–khớp 9qL²/128, −qL²/8; ngàm–ngàm qL²/24, −qL²/12.");

        // ---- liên kết cạnh, nội lực từng ô ----
        foreach (var p in PanelList.Where(p => !p.Void))
        {
            p.FixL = Continuous(p, PanelAt(p.I - 1, p.J)); p.FixR = Continuous(p, PanelAt(p.I + 1, p.J));
            p.FixB = Continuous(p, PanelAt(p.I, p.J - 1)); p.FixT = Continuous(p, PanelAt(p.I, p.J + 1));
            int fx = (p.FixL ? 1 : 0) + (p.FixR ? 1 : 0), fy = (p.FixB ? 1 : 0) + (p.FixT ? 1 : 0);
            p.Q = 1.1 * 25 * p.T + 1.2 * Finish + (Live < 2 ? 1.3 : 1.2) * Live;
            double lx = p.Lx, ly = p.Ly, qx, qy;
            bool twoWay = Math.Max(lx, ly) / Math.Min(lx, ly) <= 2.0;
            if (twoWay)
            {
                double kx = DeflCoef(fx) * Math.Pow(lx, 4), ky = DeflCoef(fy) * Math.Pow(ly, 4);
                qx = p.Q * ky / (kx + ky); qy = p.Q - qx;
            }
            else if (lx <= ly) { qx = p.Q; qy = 0; }
            else { qx = 0; qy = p.Q; }
            (p.MxSpan, p.MxSup) = ManualSlab.Strip(qx, lx, fx);
            (p.MySpan, p.MySup) = ManualSlab.Strip(qy, ly, fy);
            r.Calc.Add("Ô " + p.Label + " (" + Dt.F(lx) + " × " + Dt.F(ly) + " m, t = " + Dt.Mm(p.T) + ", Δ = " + Dt.Mm(p.Drop) + " mm, " + p.Function + "): q = " + Dt.F(p.Q, "0.00")
                       + " kN/m²; cạnh ngàm X " + fx + ", Y " + fy + (twoWay ? "" : " – bản loại dầm") + "; Mx = " + Dt.F(p.MxSpan, "0.00") + " / " + Dt.F(-p.MxSup, "0.00")
                       + ", My = " + Dt.F(p.MySpan, "0.00") + " / " + Dt.F(-p.MySup, "0.00") + " kN.m/m");
            double lmin = Math.Min(lx, ly);
            r.Checks.Add(Dt.Check("Ô " + p.Label + ": độ mảnh lmin/t ≤ 45", lmin / p.T, 45, ""));
        }
        double sMax = Math.Min(1.5 * T, 0.20);
        double ReqAs(double m, double t, double extra, int dia) => RcSection.DesignRect(Math.Max(m, 0), 1.0, t - c - extra - dia / 2000.0, c, conc, St(dia), 0.10).As;
        (int D, double S) Pick(double m, double t, double extra)
        {
            foreach (int dia in Dias)
            {
                double a = ReqAs(m, t, extra, dia);
                double sp = Math.Min(Math.Floor(Dt.Area(dia) / Math.Max(a, 1e-12) * 100 + 1e-9) / 100.0, sMax);
                if (sp >= 0.10 - 1e-9) return (dia, Math.Max(sp, 0.10));
            }
            return (12, 0.10);
        }
        double Mu(double t, double extra, (int D, double S) b) =>
            RcSection.MomentCapacity(1.0, t - c - extra - b.D / 2000.0, Dt.AreaPerM(b.D, b.S), 0, c, conc, St(b.D));
        static double La(int d) => Math.Max(10 * d / 1000.0, 0.10);

        // ---- đường thép mũ trên dầm ----
        for (int k = 0; k < Xs.Length; k++)
            for (int j = 0; j < Ys.Length - 1; j++)
            {
                var a = PanelAt(k - 1, j); var b = PanelAt(k, j);
                if ((a == null || a.Void) && (b == null || b.Void)) continue;
                TopLines.Add(new TopLine { Vertical = true, K = k, Seg = j, Pos = Xs[k], From = Ys[j], To = Ys[j + 1], A = a?.Void == true ? null : a, B = b?.Void == true ? null : b, AxisName = "trục " + AxisX(k) + " (" + AxisY(j) + "-" + AxisY(j + 1) + ")" });
            }
        for (int k = 0; k < Ys.Length; k++)
            for (int i = 0; i < Xs.Length - 1; i++)
            {
                var a = PanelAt(i, k - 1); var b = PanelAt(i, k);
                if ((a == null || a.Void) && (b == null || b.Void)) continue;
                TopLines.Add(new TopLine { Vertical = false, K = k, Seg = i, Pos = Ys[k], From = Xs[i], To = Xs[i + 1], A = a?.Void == true ? null : a, B = b?.Void == true ? null : b, AxisName = "trục " + AxisY(k) + " (" + AxisX(i) + "-" + AxisX(i + 1) + ")" });
            }
        foreach (var tl in TopLines)
        {
            if (tl.A != null && tl.B != null)
            {
                tl.DeltaDiff = Math.Abs(tl.A.Drop - tl.B.Drop);
                tl.Continuous = tl.DeltaDiff <= StepThreshold + 1e-9;
                tl.Kind = tl.DeltaDiff < 0.001 ? "Thẳng" : CrankMode == CrankBend && tl.DeltaDiff <= CrankLimit + 1e-9 ? "Nhấn" : "Neo riêng";
                tl.M = tl.Continuous ? Math.Max(tl.Vertical ? tl.A.MxSup : tl.A.MySup, tl.Vertical ? tl.B.MxSup : tl.B.MySup) : 0;
            }
            else tl.Kind = "Biên";
        }

        // ---- lỗ khoét ----
        var opens = new List<(SlabOpening O, Panel P)>();
        foreach (var o in Openings ?? new())
        {
            if (o == null) continue;
            if (o.W <= 0.01 || o.H <= 0.01) { r.Messages.Add("LỖI lỗ " + o.Name + ": kích thước không hợp lệ"); continue; }
            var p = PanelContaining(o.X + o.W / 2, o.Y + o.H / 2);
            if (p == null || p.Void) { r.Messages.Add("LỖI lỗ " + o.Name + " nằm ngoài lưới sàn hoặc trong ô lỗ thông tầng"); continue; }
            double fx0 = p.X0 + bw / 2, fx1 = p.X1 - bw / 2, fy0 = p.Y0 + bw / 2, fy1 = p.Y1 - bw / 2;
            if (o.X < fx0 - 1e-6 || o.X + o.W > fx1 + 1e-6 || o.Y < fy0 - 1e-6 || o.Y + o.H > fy1 + 1e-6)
                r.Messages.Add("LỖI lỗ " + o.Name + " chạm / cắt qua dầm của ô " + p.Label + " – dời lỗ vào trong ô (cách mép dầm) hoặc bố trí dầm viền");
            r.Checks.Add(Dt.Check("Lỗ " + o.Name + " (ô " + p.Label + "): bề rộng ≤ 1/3 nhịp lx", o.W, p.Lx / 3, "m"));
            r.Checks.Add(Dt.Check("Lỗ " + o.Name + " (ô " + p.Label + "): chiều cao ≤ 1/3 nhịp ly", o.H, p.Ly / 3, "m"));
            if (o.W > p.Lx / 3 + 1e-9 || o.H > p.Ly / 3 + 1e-9)
                r.Messages.Add("Cảnh báo lỗ " + o.Name + " lớn hơn 1/3 nhịp ô " + p.Label + " – nên bố trí dầm viền quanh lỗ (tính lại ô sàn có lỗ)");
            opens.Add((o, p));
        }

        if (manual) DesignManual(r, c, ReqAs);
        else DesignAuto(r, c, bw, ReqAs, Pick, Mu, La, opens);

        // ---- khối lượng ----
        double area = PanelList.Where(p => !p.Void).Sum(p => p.Lx * p.Ly) - opens.Sum(x => x.O.W * x.O.H);
        double vol = PanelList.Where(p => !p.Void).Sum(p => p.Lx * p.Ly * p.T) - opens.Sum(x => x.O.W * x.O.H * x.P.T);
        r.ConcreteVolume = vol;
        r.FormworkArea = area;
        r.Extra.Add(("Diện tích sàn", "m²", area));
        double wet = PanelList.Where(p => !p.Void && p.Drop < 0 && p.Wet).Sum(p => p.Lx * p.Ly);
        if (wet > 0) r.Extra.Add(("Chống thấm sàn vệ sinh / ban công", "m²", wet));
        foreach (var p in PanelList.Where(p => p.Drop < 0 && !p.Void))
            r.Calc.Add("Ô " + p.Label + " hạ cốt " + Dt.Mm(p.Drop) + " mm (" + p.Function + "): cạnh chung với ô chênh cốt > " + Dt.Mm(StepThreshold) + " mm tính khớp; dầm đỡ cần h ≥ hs1 + |Δ| + hs2 = "
                       + Dt.Mm(T + Math.Abs(p.Drop) + p.T) + " mm" + (BeamDepth + 1e-9 < T + Math.Abs(p.Drop) + p.T ? " – LỖI dầm thấp hơn" : ""));
        if (PanelList.Any(p => p.Drop < 0 && !p.Void && BeamDepth + 1e-9 < T + Math.Abs(p.Drop) + p.T))
            r.Messages.Add("LỖI dầm cao " + Dt.Mm(BeamDepth) + " mm không đủ đỡ bậc sàn hạ cốt (h ≥ hs1 + |Δ| + hs2)");
        foreach (var k in r.Checks.Where(x => !x.Ok)) r.Messages.Add("LỖI " + k.Name + ": " + Dt.F(k.Demand, "0.00") + " > " + Dt.F(k.Capacity, "0.00") + " " + k.Unit);
        RebarNumbering.Renumber(r.Bars);
    }

    private void DesignAuto(DetailResult r, double c, double bw,
        Func<double, double, double, int, double> reqAs, Func<double, double, double, (int D, double S)> pick,
        Func<double, double, (int D, double S), double> mu, Func<int, double> la, List<(SlabOpening O, Panel P)> opens)
    {
        // ---- lớp dưới từng ô ----
        foreach (var p in PanelList.Where(p => !p.Void))
        {
            p.BotX = pick(p.MxSpan, p.T, 0);
            p.BotY = pick(p.MySpan, p.T, p.BotX.D / 1000.0);
            r.Checks.Add(Dt.Check("Ô " + p.Label + " lớp dưới X " + Dt.Spaced(p.BotX.D, p.BotX.S) + ": M ≤ Mu", p.MxSpan, mu(p.T, 0, p.BotX), "kN.m/m"));
            r.Checks.Add(Dt.Check("Ô " + p.Label + " lớp dưới Y " + Dt.Spaced(p.BotY.D, p.BotY.S) + ": M ≤ Mu", p.MySpan, mu(p.T, p.BotX.D / 1000.0, p.BotY), "kN.m/m"));
            double lenX = Dt.R10(p.Lx - bw + 2 * Math.Min(la(p.BotX.D), bw - c));
            double lenY = Dt.R10(p.Ly - bw + 2 * Math.Min(la(p.BotY.D), bw - c));
            int nX = Dt.CountAcross(p.Ly - bw, 0.05, p.BotX.S), nY = Dt.CountAcross(p.Lx - bw, 0.05, p.BotY.S);
            // thanh bị lỗ cắt: bớt thanh suốt, thêm 2 đoạn ngắn mỗi thanh bị cắt
            foreach (var (o, op) in opens.Where(x => x.P == p))
            {
                int cutX = Math.Min(nX, (int)Math.Ceiling(o.H / p.BotX.S - 1e-9)), cutY = Math.Min(nY, (int)Math.Ceiling(o.W / p.BotY.S - 1e-9));
                nX -= cutX; nY -= cutY;
                double lA = Dt.R10(o.X - (p.X0 + bw / 2) + Math.Min(la(p.BotX.D), bw - c) - 0.025), lB = Dt.R10(p.X1 - bw / 2 - (o.X + o.W) + Math.Min(la(p.BotX.D), bw - c) - 0.025);
                double lC = Dt.R10(o.Y - (p.Y0 + bw / 2) + Math.Min(la(p.BotY.D), bw - c) - 0.025), lD = Dt.R10(p.Y1 - bw / 2 - (o.Y + o.H) + Math.Min(la(p.BotY.D), bw - c) - 0.025);
                foreach (var (len, role, d, n) in new[] { (lA, "X", p.BotX.D, cutX), (lB, "X", p.BotX.D, cutX), (lC, "Y", p.BotY.D, cutY), (lD, "Y", p.BotY.D, cutY) })
                    if (len > 0.15 && n > 0) r.Bars.Add(Dt.Straight("Ô " + p.Label + " lớp dưới " + role + " (cắt lỗ " + o.Name + ")", d, n, len, false, role == "X" ? p.BotX.S : p.BotY.S));
                // thép gia cường mép lỗ: bù diện tích thanh bị cắt, chia 2 bên, tối thiểu 2Ø12 mỗi bên, dài = cạnh lỗ + 2·40d
                double asCutX = cutX * Dt.Area(p.BotX.D), asCutY = cutY * Dt.Area(p.BotY.D);
                int trX = Math.Max(2, (int)Math.Ceiling(asCutX / 2 / Dt.Area(12) - 1e-9)), trY = Math.Max(2, (int)Math.Ceiling(asCutY / 2 / Dt.Area(12) - 1e-9));
                r.Bars.Add(Dt.Straight("Gia cường lỗ " + o.Name + " phương X", 12, 2 * trX, Dt.R10(o.W + 2 * 40 * 0.012)));
                r.Bars.Add(Dt.Straight("Gia cường lỗ " + o.Name + " phương Y", 12, 2 * trY, Dt.R10(o.H + 2 * 40 * 0.012)));
                r.Calc.Add("Lỗ " + o.Name + " " + Dt.Mm(o.W) + "×" + Dt.Mm(o.H) + " mm trong ô " + p.Label + ": cắt " + cutX + " thanh X, " + cutY + " thanh Y → gia cường mỗi bên "
                           + trX + "Ø12 (X), " + trY + "Ø12 (Y), dài cạnh lỗ + 2·40d; thêm 2Ø12 chéo góc nếu lỗ > 400 mm");
                if (Math.Max(o.W, o.H) > 0.4) r.Bars.Add(Dt.Straight("Gia cường lỗ " + o.Name + " chéo góc", 12, 4 * 1, Dt.R10(2 * 40 * 0.012 + 0.2)));
            }
            p.BarX = Dt.Straight("Ô " + p.Label + " lớp dưới X", p.BotX.D, Math.Max(nX, 0), lenX, false, p.BotX.S);
            p.BarY = Dt.Straight("Ô " + p.Label + " lớp dưới Y", p.BotY.D, Math.Max(nY, 0), lenY, false, p.BotY.S);
            if (p.BarX.Count > 0) r.Bars.Add(p.BarX);
            if (p.BarY.Count > 0) r.Bars.Add(p.BarY);
        }
        // ---- thép mũ ----
        foreach (var tl in TopLines)
        {
            double segLen = tl.To - tl.From;
            var pa = tl.A ?? tl.B; var pb = tl.B ?? tl.A;
            double Lsh(Panel p) => p == null ? 0 : Math.Min(p.Lx, p.Ly);
            double tA = tl.A?.T ?? T, tB = tl.B?.T ?? T;
            double tMin = Math.Min(tA, tB);
            if (tl.Kind == "Biên")
            {
                var p = pa;
                double asBot = tl.Vertical ? Dt.AreaPerM(p.BotX.D, p.BotX.S) : Dt.AreaPerM(p.BotY.D, p.BotY.S);
                tl.Bar = Dt.ChooseSpaced(Math.Max(Dt.Area(6) / 0.20, asBot / 3), Dias, 0.20, 0.10);
                tl.Ext = Lsh(p) / 4 + bw / 2;
                double straight = Dt.R10(tl.Ext + bw / 2 - c), legIn = Dt.R10(Math.Max(10 * tl.Bar.D / 1000.0, 0.10)), legOut = Dt.R10(p.T - 2 * c);
                var bm = Dt.Hooked("Mũ biên " + tl.AxisName, tl.Bar.D, Dt.CountAcross(segLen - bw, 0.05, tl.Bar.S), straight, legIn, legOut, true, tl.Bar.S);
                tl.Marks.Add(bm); r.Bars.Add(bm);
                continue;
            }
            double asReq = tl.M > 0 ? reqAs(tl.M, tMin, 0, 8) : 0;
            double asMin = Math.Max(Dt.Area(6) / 0.20, Math.Max(tl.Vertical ? Dt.AreaPerM(pa.BotX.D, pa.BotX.S) : Dt.AreaPerM(pa.BotY.D, pa.BotY.S),
                                                             tl.Vertical ? Dt.AreaPerM(pb.BotX.D, pb.BotX.S) : Dt.AreaPerM(pb.BotY.D, pb.BotY.S)) / 3);
            if (tl.M > 0)
            {
                tl.Bar = pick(tl.M, tMin, 0);
                if (Dt.AreaPerM(tl.Bar.D, tl.Bar.S) < asMin) tl.Bar = Dt.ChooseSpaced(asMin, Dias, 0.20, 0.10);
                r.Checks.Add(Dt.Check("Mũ " + tl.AxisName + " " + Dt.Spaced(tl.Bar.D, tl.Bar.S) + ": M ≤ Mu", tl.M, mu(tMin, 0, tl.Bar), "kN.m/m"));
            }
            else tl.Bar = Dt.ChooseSpaced(asMin, Dias, 0.20, 0.10);
            tl.Ext = Math.Max(Lsh(tl.A), Lsh(tl.B)) / 4 + bw / 2;
            int n = Dt.CountAcross(segLen - bw, 0.05, tl.Bar.S);
            double legA = Dt.R10(tA - 2 * c), legB = Dt.R10(tB - 2 * c);
            if (tl.Kind == "Thẳng")
            {
                var bm = Dt.Hooked("Mũ " + tl.AxisName, tl.Bar.D, n, Dt.R10(2 * tl.Ext), legA, legB, true, tl.Bar.S);
                tl.Marks.Add(bm); r.Bars.Add(bm);
            }
            else if (tl.Kind == "Nhấn")
            {
                var (run, inc) = Crank(tl.DeltaDiff);
                double h1 = Dt.R10(tl.Ext - run / 2), h2 = Dt.R10(tl.Ext - run / 2);
                var segs = new List<double> { Math.Round(legA * 1000), Math.Round(h1 * 1000), Math.Round(inc * 1000), Math.Round(h2 * 1000), Math.Round(legB * 1000) };
                var bm = new BarMark
                {
                    Role = "Thép mũ nhấn " + tl.AxisName, Dia = tl.Bar.D, Count = n, IsTop = true, Spacing = tl.Bar.S, Shape = "Nhấn",
                    Segments = segs, Length = Dt.R10(legA + h1 + inc + h2 + legB), Start = 0, End = Dt.R10(h1 + run + h2)
                };
                tl.Marks.Add(bm); r.Bars.Add(bm);
                r.Calc.Add("Thép mũ nhấn " + tl.AxisName + ": chênh cốt " + Dt.Mm(tl.DeltaDiff) + " mm ≤ " + Dt.Mm(CrankLimit) + " mm → bẻ xiên dốc 1:6 (ngang " + Dt.Mm(run) + ", xiên " + Dt.Mm(inc)
                           + " mm) đặt trong bề rộng dầm, " + Dt.Spaced(tl.Bar.D, tl.Bar.S));
            }
            else
            {
                double legIn = Dt.R10(Math.Max(10 * tl.Bar.D / 1000.0, 0.10));
                double straight = Dt.R10(tl.Ext + bw / 2 - c);
                var b1 = Dt.Hooked("Mũ gối (neo riêng) " + tl.AxisName + " phía " + (tl.Vertical ? "trái" : "dưới"), tl.Bar.D, n, straight, legIn, legA, true, tl.Bar.S);
                var b2 = Dt.Hooked("Mũ gối (neo riêng) " + tl.AxisName + " phía " + (tl.Vertical ? "phải" : "trên"), tl.Bar.D, n, straight, legIn, legB, true, tl.Bar.S);
                tl.Marks.Add(b1); tl.Marks.Add(b2); r.Bars.Add(b1); r.Bars.Add(b2);
                r.Calc.Add("Mũ " + tl.AxisName + ": chênh cốt " + Dt.Mm(tl.DeltaDiff) + " mm" + (CrankMode == CrankCut ? " (chọn cắt thép)" : " > " + Dt.Mm(CrankLimit) + " mm")
                           + " → cắt thép, mỗi ô neo riêng vào dầm (chân " + Dt.Mm(legIn) + " mm), " + Dt.Spaced(tl.Bar.D, tl.Bar.S));
            }
        }
        foreach (var p in PanelList.Where(p => !p.Void))
            r.Calc.Add("Ô " + p.Label + ": lớp dưới X " + Dt.Spaced(p.BotX.D, p.BotX.S) + ", Y " + Dt.Spaced(p.BotY.D, p.BotY.S));
    }

    private readonly List<(SlabBarRow Row, BarMark Bar)> _manualMap = new();

    private void DesignManual(DetailResult r, double c, Func<double, double, double, int, double> reqAs)
    {
        _manualMap.Clear();
        var rows = (ManualBars ?? new()).Where(b => b != null).ToList();
        if (rows.Count == 0) r.Messages.Add("LỖI chế độ thủ công: chưa khai báo vùng thép nào (bảng THÉP VẼ THỦ CÔNG)");
        int idx = 1;
        foreach (var b in rows)
        {
            if (b.Dia <= 0 || b.Spacing < 0.05) { r.Messages.Add("LỖI vùng thép " + idx + ": Ø / bước không hợp lệ"); idx++; continue; }
            double x1 = Math.Min(b.X1, b.X2), x2 = Math.Max(b.X1, b.X2), y1 = Math.Min(b.Y1, b.Y2), y2 = Math.Max(b.Y1, b.Y2);
            bool alongX = b.Direction != "Y";
            double len = alongX ? x2 - x1 : y2 - y1, width = alongX ? y2 - y1 : x2 - x1;
            if (len < 0.1) { r.Messages.Add("LỖI vùng thép " + idx + ": chiều dài thanh < 100 mm"); idx++; continue; }
            int n = (int)Math.Floor(width / b.Spacing + 1e-6) + 1;
            bool top = b.Layer == "Trên";
            string role = "Thủ công " + (top ? "lớp trên " : "lớp dưới ") + (alongX ? "X" : "Y") + (string.IsNullOrWhiteSpace(b.Label) ? " #" + idx : " " + b.Label);
            var bm = top ? Dt.Hooked(role, b.Dia, n, Dt.R10(len), Dt.R10(T - 2 * c), Dt.R10(T - 2 * c), true, b.Spacing) : Dt.Straight(role, b.Dia, n, Dt.R10(len), false, b.Spacing);
            r.Bars.Add(bm);
            _manualMap.Add((b, bm));
            idx++;
        }
        double Provided(string layer, string dir, double x, double y) => rows
            .Where(b => b.Layer == layer && (b.Direction == "Y" ? "Y" : "X") == dir && b.Spacing >= 0.05 && b.Dia > 0
                        && x >= Math.Min(b.X1, b.X2) - 1e-9 && x <= Math.Max(b.X1, b.X2) + 1e-9 && y >= Math.Min(b.Y1, b.Y2) - 1e-9 && y <= Math.Max(b.Y1, b.Y2) + 1e-9)
            .Sum(b => Dt.AreaPerM(b.Dia, b.Spacing));
        foreach (var p in PanelList.Where(p => !p.Void))
        {
            double cx = (p.X0 + p.X1) / 2, cy = (p.Y0 + p.Y1) / 2;
            double rx = reqAs(p.MxSpan, p.T, 0, 8), ry = reqAs(p.MySpan, p.T, 0.008, 8);
            r.Checks.Add(Dt.Check("Ô " + p.Label + " lớp dưới X: As yêu cầu ≤ As bố trí", rx * 1e4, Provided("Dưới", "X", cx, cy) * 1e4, "cm²/m"));
            r.Checks.Add(Dt.Check("Ô " + p.Label + " lớp dưới Y: As yêu cầu ≤ As bố trí", ry * 1e4, Provided("Dưới", "Y", cx, cy) * 1e4, "cm²/m"));
        }
        foreach (var tl in TopLines.Where(t => t.M > 0))
        {
            double mx = tl.Vertical ? tl.Pos : (tl.From + tl.To) / 2, my = tl.Vertical ? (tl.From + tl.To) / 2 : tl.Pos;
            double req = reqAs(tl.M, Math.Min(tl.A.T, tl.B.T), 0, 8);
            r.Checks.Add(Dt.Check("Mũ " + tl.AxisName + ": As yêu cầu ≤ As bố trí", req * 1e4, Provided("Trên", tl.Vertical ? "X" : "Y", mx, my) * 1e4, "cm²/m"));
        }
        if ((Openings?.Count ?? 0) > 0) r.Messages.Add("Chế độ thủ công: tự khai báo thép gia cường mép lỗ (gợi ý ≥ 2Ø12 mỗi cạnh, dài cạnh lỗ + 2·40d)");
        r.Calc.Add("Thủ công: " + rows.Count + " vùng thép; As bố trí tại tâm mỗi ô / giữa mỗi đoạn dầm liên tục so với As yêu cầu (TCVN 5574:2018, μmin 0,1 %)");
    }

    // ==================================================================
    // BẢN VẼ
    // ==================================================================
    public List<Figure> Draw(DetailResult r, DetailSettings s)
    {
        var figs = new List<Figure>();
        if (r == null || PanelList.Count == 0) return figs;
        s ??= new DetailSettings();
        double W = Xs[^1] * 1000, H = Ys[^1] * 1000, bw = BeamWidth * 1000;
        double S = Math.Max(W, H) > 9000 ? 100 : 50;
        var w = new DxfWriter(S);
        double th = w.Th, r0 = w.BubbleR;
        // trục + bong bóng
        double ext = 1200 + 2 * r0;
        for (int k = 0; k < Xs.Length; k++)
        {
            double x = Xs[k] * 1000;
            w.Line(Lay.Truc, x, -ext, x, H + ext, "CENTER");
            w.GridBubble(x, -ext - r0, AxisX(k)); w.GridBubble(x, H + ext + r0, AxisX(k));
        }
        for (int k = 0; k < Ys.Length; k++)
        {
            double y = Ys[k] * 1000;
            w.Line(Lay.Truc, -ext, y, W + ext, y, "CENTER");
            w.GridBubble(-ext - r0, y, AxisY(k)); w.GridBubble(W + ext + r0, y, AxisY(k));
        }
        // dầm nét đứt
        foreach (var tl in TopLines)
        {
            double p = tl.Pos * 1000, a = tl.From * 1000, b = tl.To * 1000;
            if (tl.Vertical) w.Rect(Lay.BeTong, p - bw / 2, a - bw / 2, p + bw / 2, b + bw / 2, "DASHED");
            else w.Rect(Lay.BeTong, a - bw / 2, p - bw / 2, b + bw / 2, p + bw / 2, "DASHED");
        }
        // ô sàn
        foreach (var p in PanelList)
        {
            double x0 = p.X0 * 1000 + bw / 2, x1 = p.X1 * 1000 - bw / 2, y0 = p.Y0 * 1000 + bw / 2, y1 = p.Y1 * 1000 - bw / 2;
            if (p.Void)
            {
                w.Rect(Lay.BeTong, x0, y0, x1, y1);
                w.Line(Lay.KyHieu, x0, y0, x1, y1); w.Line(Lay.KyHieu, x0, y1, x1, y0);
                w.Text(Lay.Chu, (x0 + x1) / 2, (y0 + y1) / 2 + th, th, "LỖ THÔNG TẦNG", 0, 1, 0);
                continue;
            }
            if (p.Drop < -1e-6)
            {
                // gạch chéo 45° bước 600 mm (giấy ~6 mm ở 1/100)
                double step = 6 * S;
                for (double d0 = -(y1 - y0); d0 < (x1 - x0); d0 += step)
                {
                    // đường x − y = d0 (gốc x0,y0) cắt hình chữ nhật
                    double ax = Math.Max(0, d0), ay = ax - d0, bx = Math.Min(x1 - x0, (y1 - y0) + d0), by = bx - d0;
                    if (bx - ax > 1) w.Line(Lay.KyHieu, x0 + ax, y0 + ay, x0 + bx, y0 + by);
                }
                w.Text(Lay.Chu, (x0 + x1) / 2, (y0 + y1) / 2 - 2.2 * th, th, "▽ " + DxfWriter.ElevText(p.Drop) + " (HẠ COS " + Dt.Mm(p.Drop) + ")", 0, 1, 0);
            }
            w.Text(Lay.Chu, (x0 + x1) / 2, (y0 + y1) / 2 - 3.6 * th, 0.8 * th, "S" + p.Label + " – t = " + Dt.Mm(p.T), 0, 1, 0);
            if (Mode != ModeManual)
            {
                if (p.BarX != null && p.BarX.Count > 0)
                {
                    double y = p.Y0 * 1000 + 0.35 * (p.Ly * 1000);
                    PlanBarDraw.Bar(w, true, x0 - bw / 2 + 25, x1 + bw / 2 - 25, y, Dt.Spaced(p.BarX.Dia, p.BarX.Spacing), p.BarX.Mark, 0.9 * th, 0.9 * th, 1, 0.40, (y0 + 50, y1 - 50), 0.15);
                }
                if (p.BarY != null && p.BarY.Count > 0)
                {
                    double x = p.X0 * 1000 + 0.65 * (p.Lx * 1000);
                    PlanBarDraw.Bar(w, false, y0 - bw / 2 + 25, y1 + bw / 2 - 25, x, Dt.Spaced(p.BarY.Dia, p.BarY.Spacing), p.BarY.Mark, 0.9 * th, 0.9 * th, -1, 0.62, (x0 + 50, x1 - 50), 0.85);
                }
            }
        }
        // lỗ khoét
        foreach (var o in Openings ?? new())
        {
            if (o == null || o.W <= 0.01 || o.H <= 0.01) continue;
            double x0 = o.X * 1000, y0 = o.Y * 1000, x1 = x0 + o.W * 1000, y1 = y0 + o.H * 1000;
            w.Rect(Lay.BeTong, x0, y0, x1, y1);
            w.Line(Lay.KyHieu, x0, y0, x1, y1); w.Line(Lay.KyHieu, x0, y1, x1, y0);
            w.Text(Lay.Chu, (x0 + x1) / 2, y1 + 0.4 * th, 0.8 * th, "LỖ " + o.Name + " " + Dt.Mm(o.W) + "×" + Dt.Mm(o.H), 0, 1, 0);
            var tr = r.Bars.FirstOrDefault(b => b.Role == "Gia cường lỗ " + o.Name + " phương X");
            if (tr != null)
            {
                double L = tr.Length * 1000;
                w.Line(Lay.Thep, (x0 + x1) / 2 - L / 2, y0 - 60, (x0 + x1) / 2 + L / 2, y0 - 60);
                w.Line(Lay.Thep, (x0 + x1) / 2 - L / 2, y1 + 60, (x0 + x1) / 2 + L / 2, y1 + 60);
                w.LeaderTag((x0 + x1) / 2 - L / 2 + 50, y0 - 60, (x0 + x1) / 2 - L / 2 - 2 * th, y0 - 3 * th, Dt.Bars(tr.Count / 2, tr.Dia) + " mỗi bên", tr.Mark, -1);
            }
            var ty = r.Bars.FirstOrDefault(b => b.Role == "Gia cường lỗ " + o.Name + " phương Y");
            if (ty != null)
            {
                double L = ty.Length * 1000;
                w.Line(Lay.Thep, x0 - 60, (y0 + y1) / 2 - L / 2, x0 - 60, (y0 + y1) / 2 + L / 2);
                w.Line(Lay.Thep, x1 + 60, (y0 + y1) / 2 - L / 2, x1 + 60, (y0 + y1) / 2 + L / 2);
            }
        }
        // thép mũ
        if (Mode != ModeManual)
            foreach (var tl in TopLines.Where(t => t.Marks.Count > 0))
            {
                double p = tl.Pos * 1000, mid = (tl.From + 0.3 * (tl.To - tl.From)) * 1000, e = tl.Ext * 1000;
                var bm = tl.Marks[0];
                string tag = Dt.Spaced(bm.Dia, bm.Spacing) + (tl.Kind == "Nhấn" ? " (nhấn)" : tl.Kind == "Neo riêng" ? " (neo riêng)" : "");
                double e1 = tl.A != null ? e : bw / 2 - 25, e2 = tl.B != null ? e : bw / 2 - 25;
                if (tl.Vertical)
                {
                    PlanBarDraw.Bar(w, true, p - e1, p + e2, mid, tag, bm.Mark, 0.9 * th, 0.9 * th, -1, 0.5, (tl.From * 1000 + 50, tl.To * 1000 - 50), 0.85);
                }
                else
                {
                    PlanBarDraw.Bar(w, false, p - e1, p + e2, mid, tag, bm.Mark, 0.9 * th, 0.9 * th, 1, 0.5, (tl.From * 1000 + 50, tl.To * 1000 - 50), 0.85, -1);
                }
            }
        else
            foreach (var (row, b) in _manualMap)
            {
                double x1 = Math.Min(row.X1, row.X2) * 1000, x2 = Math.Max(row.X1, row.X2) * 1000, y1 = Math.Min(row.Y1, row.Y2) * 1000, y2 = Math.Max(row.Y1, row.Y2) * 1000;
                if (row.Direction == "Y") PlanBarDraw.Bar(w, false, y1, y2, (x1 + x2) / 2, Dt.Spaced(b.Dia, b.Spacing), b.Mark, 0, 0, 1, 0.5, (x1 + 50, x2 - 50), 0.2);
                else PlanBarDraw.Bar(w, true, x1, x2, (y1 + y2) / 2, Dt.Spaced(b.Dia, b.Spacing), b.Mark, 0, 0, 1, 0.5, (y1 + 50, y2 - 50), 0.2);
            }
        // kích thước
        var xsMm = Xs.Select(x => x * 1000).ToList(); var ysMm = Ys.Select(y => y * 1000).ToList();
        w.DimChainH(xsMm, 0, -ext + 3 * th);
        w.Dim(0, 0, W, 0, -ext + 0.8 * th);
        w.DimChainV(ysMm, 0, -ext + 3 * th);
        w.Title(W / 2, -ext - 2 * r0 - 2 * th, RebarSchedule.MemberLabel("MẶT BẰNG BỐ TRÍ THÉP SÀN " + Name, r.Quantity), DrawUtil.ScaleText(S));
        figs.Add(new Figure(w, "Mặt bằng sàn " + Name, true));
        figs.Add(Section(r, s));
        return figs;
    }

    /// <summary>Mặt cắt 1-1 qua dầm giữa hai ô (ưu tiên chỗ có chênh cốt): bậc sàn, thép mũ nhấn / neo riêng, thép lớp dưới.</summary>
    private Figure Section(DetailResult r, DetailSettings s)
    {
        double S = 25;
        var w = new DxfWriter(S);
        double th = w.Th, c = s.CoverSlab * 1000, bw = BeamWidth * 1000, hb = Math.Max(BeamDepth * 1000, 200);
        var tl = TopLines.Where(t => t.A != null && t.B != null).OrderByDescending(t => t.DeltaDiff).FirstOrDefault();
        double dA = (tl?.A.Drop ?? 0) * 1000, dB = (tl?.B.Drop ?? 0) * 1000, tA = (tl?.A.T ?? T) * 1000, tB = (tl?.B.T ?? T) * 1000;
        double L = 1300, top = Math.Max(dA, dB);
        w.Rect(Lay.BeTong, -L, dA - tA, -bw / 2, dA);
        w.Rect(Lay.BeTong, bw / 2, dB - tB, L, dB);
        w.Polyline(Lay.BeTong, new List<(double, double)> { (-bw / 2, dA - tA), (-bw / 2, top - hb), (bw / 2, top - hb), (bw / 2, dB - tB) }, false);
        if (Math.Abs(dA - dB) > 0.5) w.Line(Lay.BeTong, dA > dB ? bw / 2 : -bw / 2, Math.Min(dA, dB), dA > dB ? bw / 2 : -bw / 2, top);
        w.Line(Lay.BeTong, -bw / 2, top, bw / 2, top);
        w.BreakV(Lay.BeTong, -L, dA - tA - 80, dA + 80);
        w.BreakV(Lay.BeTong, L, dB - tB - 80, dB + 80);
        w.BreakH(Lay.BeTong, -bw / 2 - 80, bw / 2 + 80, top - hb);
        // thép lớp dưới
        double yA = dA - tA + c, yB = dB - tB + c;
        w.Line(Lay.Thep, -L + 40, yA, 0, yA);
        w.Line(Lay.Thep, 0, yB, L - 40, yB);
        if (tl != null && Mode != ModeManual)
        {
            var botA = tl.Vertical ? tl.A.BarX : tl.A.BarY; var botB = tl.Vertical ? tl.B.BarX : tl.B.BarY;
            if (botA != null) w.LeaderTag(-L * 0.6, yA, -L * 0.6 - 2 * th, yA - 5 * th, Dt.Spaced(botA.Dia, botA.Spacing), botA.Mark, -1);
            if (botB != null) w.LeaderTag(L * 0.6, yB, L * 0.6 + 2 * th, yB - 5 * th, Dt.Spaced(botB.Dia, botB.Spacing), botB.Mark);
        }
        // thép mũ
        double e = Math.Min((tl?.Ext ?? 0.8) * 1000, L - 60);
        double uA = dA - c, uB = dB - c, legA = tA - 2 * c, legB = tB - 2 * c;
        string kind = tl?.Kind ?? "Thẳng";
        var top0 = tl?.Marks.FirstOrDefault();
        if (kind == "Neo riêng")
        {
            double leg = Math.Max(100, 10 * (top0?.Dia ?? 8));
            w.Polyline(Lay.Thep, new List<(double, double)> { (-e, uA - legA), (-e, uA), (bw / 2 - c, uA), (bw / 2 - c, uA - leg) }, false);
            w.Polyline(Lay.Thep, new List<(double, double)> { (e, uB - legB), (e, uB), (-bw / 2 + c, uB), (-bw / 2 + c, uB - leg) }, false);
            if (tl?.Marks.Count > 1) w.LeaderTag(e * 0.5, uB, e * 0.5 + 2 * th, uB + 5 * th, Dt.Spaced(tl.Marks[1].Dia, tl.Marks[1].Spacing), tl.Marks[1].Mark);
        }
        else
        {
            var (run, _) = Crank((dA - dB) / 1000.0);
            double rr = run * 1000;
            w.Polyline(Lay.Thep, new List<(double, double)> { (-e, uA - legA), (-e, uA), (-rr / 2, uA), (rr / 2, uB), (e, uB), (e, uB - legB) }, false);
        }
        if (top0 != null)
            w.LeaderTag(-e * 0.5, uA, -e * 0.5 - 2 * th, uA + 5 * th, Dt.Spaced(top0.Dia, top0.Spacing) + (kind == "Nhấn" ? " nhấn 1:6" : ""), top0.Mark, -1);
        w.ElevMark(-L + 200, dA, DxfWriter.ElevText(dA / 1000.0));
        w.ElevMark(L - 200, dB, DxfWriter.ElevText(dB / 1000.0), 1);
        if (Math.Abs(dA - dB) > 0.5) w.Dim(bw / 2 + 150, Math.Min(dA, dB), bw / 2 + 150, Math.Max(dA, dB), -3 * th);
        w.Dim(-L, dA - tA, -L, dA, 3 * th);
        w.Dim(-bw / 2, top - hb, bw / 2, top - hb, -3 * th);
        string where = tl == null ? "" : " – " + tl.AxisName + (kind == "Nhấn" ? " (thép mũ nhấn)" : kind == "Neo riêng" ? " (thép mũ neo riêng)" : "");
        w.Text(Lay.Chu, 0, top + 7 * th, 0.8 * th, "Ô " + (tl?.A.Label ?? "") + " | Ô " + (tl?.B.Label ?? "") + where, 0, 1, 0);
        w.Title(0, top - hb - 6 * th, "1-1", DrawUtil.ScaleText(S), true);
        return new Figure(w, "Mặt cắt sàn " + Name);
    }
}

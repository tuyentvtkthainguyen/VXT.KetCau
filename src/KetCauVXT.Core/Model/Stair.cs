namespace KetCauVXT.Model;

// =====================================================================
// CẦU THANG BỘ BTCT (Project.Stairs)
// Một cầu thang gắn với một lỗ sàn loại "Cầu thang" (OpeningId) hoặc khai báo độc lập (X0..Y1 + Story).
// Cầu thang đi từ cao độ tầng dưới lên cao độ tầng Story (lỗ sàn nằm ở tầng Story).
// Hình học tính ra bằng Stair.Layout(Project) – không lưu (StairLayout).
// Quy ước: bê tông chỉ gồm bản thang (bản nghiêng), chiếu nghỉ, dầm cốn (nếu chọn), dầm chiếu nghỉ;
// bậc thang xây gạch (mặc định) hoặc đổ BTCT liền khối; mặt bậc ốp đá granite / gỗ / gạch.
// =====================================================================

/// <summary>Dạng cầu thang.</summary>
public enum StairType
{
    /// <summary>2 vế chữ U có chiếu nghỉ giữa tầng (mặc định nhà phố)</summary>
    TwoFlightU,
    /// <summary>1 vế thẳng (không chiếu nghỉ)</summary>
    Straight,
    /// <summary>3 vế chữ U: 2 chiếu nghỉ góc, vế giữa chạy ngang đầu ô thang</summary>
    ThreeFlightU
}

/// <summary>Sơ đồ chịu lực của bản thang.</summary>
public enum StairSupport
{
    /// <summary>Bản thang chịu lực tựa lên dầm chiếu nghỉ và dầm sàn (bản làm việc theo phương vế)</summary>
    Slab,
    /// <summary>Có dầm cốn (limon) 1 hoặc 2 bên – bản thang làm việc theo phương ngang vế, tựa lên dầm cốn</summary>
    Stringer
}

/// <summary>Cấu tạo bậc thang.</summary>
public enum StepConstruction
{
    /// <summary>Xây gạch trên bản thang (không tính vào bê tông)</summary>
    Brick,
    /// <summary>Đổ BTCT liền khối với bản thang</summary>
    Concrete
}

/// <summary>Vật liệu ốp mặt bậc.</summary>
public enum StepFinish { Granite, Wood, Tile, None }

/// <summary>Lan can / tay vịn (chỉ để tính tải).</summary>
public enum RailingKind
{
    None,
    /// <summary>Lan can sắt / inox, tay vịn gỗ ≈ 0,3 kN/m</summary>
    Steel,
    /// <summary>Lan can kính cường lực ≈ 0,5 kN/m</summary>
    Glass,
    /// <summary>Tường lan can xây gạch 110 cao ~0,9 m, trát 2 mặt ≈ 2,3 kN/m</summary>
    Brick
}

/// <summary>Phương chạy của vế thang trên mặt bằng.</summary>
public enum StairAxis { Auto, X, Y }

/// <summary>Khai báo một cầu thang. Giá trị 0 = tự chọn theo quy tắc (Stair.Layout).</summary>
public class Stair
{
    public int Id { get; set; }
    /// <summary>Tên cầu thang (CT1, CT2…)</summary>
    public string Name { get; set; } = "";
    /// <summary>Lỗ sàn cầu thang gắn với (0 = khai báo độc lập, dùng Story + X0..Y1)</summary>
    public int OpeningId { get; set; }
    /// <summary>Tầng trên cùng của cầu thang (tầng có lỗ sàn); thang đi lên từ cao độ tầng dưới</summary>
    public string Story { get; set; } = "";
    /// <summary>Phạm vi mặt bằng khi khai báo độc lập (m)</summary>
    public double X0 { get; set; }
    public double Y0 { get; set; }
    public double X1 { get; set; }
    public double Y1 { get; set; }

    public StairType Type { get; set; } = StairType.TwoFlightU;
    /// <summary>Phương chạy vế (Auto = theo cạnh dài của ô)</summary>
    public StairAxis Axis { get; set; } = StairAxis.Auto;
    /// <summary>true = chân thang (đầu vế 1) ở phía toạ độ lớn của ô</summary>
    public bool StartAtMax { get; set; }

    /// <summary>Bề rộng một vế (m); 0 = (bề rộng ô − khe thang)/2 với thang chữ U, = bề rộng ô với thang thẳng</summary>
    public double FlightWidth { get; set; }
    /// <summary>Khe giữa hai vế (giếng thang), m</summary>
    public double Gap { get; set; } = 0.10;
    /// <summary>Tổng số bậc (cổ bậc) của cầu thang; 0 = chiều cao tầng / ~170 mm</summary>
    public int Risers { get; set; }
    /// <summary>Số cổ bậc vế 1; 0 = chia đều (số bậc lẻ: vế trên nhiều hơn)</summary>
    public int FirstFlightRisers { get; set; }
    /// <summary>Bề rộng mặt bậc b (m); 0 = 0,63 − 2h trong khoảng 260…300 mm, co lại nếu ô thang ngắn</summary>
    public double Tread { get; set; }
    /// <summary>Chiều dày bản thang (m); 0 = max(100 mm; L/30)</summary>
    public double WaistThickness { get; set; }
    /// <summary>Chiều sâu chiếu nghỉ theo phương vế (m); 0 = max(bề rộng vế; 0,9 m), lấy phần ô còn lại</summary>
    public double LandingDepth { get; set; }
    /// <summary>Chiều dày chiếu nghỉ (m); 0 = bằng bản thang</summary>
    public double LandingThickness { get; set; }

    public StairSupport Support { get; set; } = StairSupport.Slab;
    /// <summary>Số dầm cốn mỗi vế (1 = một bên, bên kia tựa tường; 2 = hai bên)</summary>
    public int StringerSides { get; set; } = 2;
    public double StringerB { get; set; } = 0.15;
    public double StringerH { get; set; } = 0.30;
    /// <summary>Có dầm chiếu nghỉ (false = bản thang + chiếu nghỉ gãy khúc tựa lên tường/dầm đầu ô)</summary>
    public bool LandingBeam { get; set; } = true;
    public double LandingBeamB { get; set; } = 0.22;
    public double LandingBeamH { get; set; } = 0.30;
    /// <summary>Gối bản thang: false = khớp (M nhịp = qL²/8, thép mũ cấu tạo qL²/24); true = ngàm một phần (qL²/10, gối qL²/16)</summary>
    public bool PartialFixity { get; set; }

    public StepConstruction Steps { get; set; } = StepConstruction.Brick;
    public StepFinish Finish { get; set; } = StepFinish.Granite;
    /// <summary>Chiều dày lớp ốp (m); 0 = mặc định theo vật liệu (granite 20, gỗ 25, gạch 10 mm)</summary>
    public double FinishThickness { get; set; }
    /// <summary>Vữa lót dưới lớp ốp (m)</summary>
    public double BeddingThickness { get; set; } = 0.02;
    /// <summary>Vữa trát mặt dưới bản thang (m)</summary>
    public double PlasterThickness { get; set; } = 0.015;
    /// <summary>Tính len chân tường (cổ bậc sát tường) vào khối lượng ốp</summary>
    public bool Skirting { get; set; }
    public RailingKind Railing { get; set; } = RailingKind.Steel;
    /// <summary>Tải lan can (kN/m); 0 = theo loại lan can</summary>
    public double RailingLoad { get; set; }
    /// <summary>Hoạt tải tiêu chuẩn cầu thang (kN/m²) – TCVN 2737:2023 Bảng 4: cầu thang nhà ở 3,0</summary>
    public double LiveLoad { get; set; } = StairUnits.LiveDefault;
    public string Note { get; set; } = "";

    // ------------------------------------------------------------------
    public static double FinishDefaultThickness(StepFinish f) => f switch
    {
        StepFinish.Granite => 0.02, StepFinish.Wood => 0.025, StepFinish.Tile => 0.01, _ => 0
    };

    public static double FinishUnitWeight(StepFinish f) => f switch
    {
        StepFinish.Granite => StairUnits.Granite, StepFinish.Wood => StairUnits.Wood, StepFinish.Tile => StairUnits.Tile, _ => 0
    };

    public static string FinishName(StepFinish f) => f switch
    {
        StepFinish.Granite => "Đá granite", StepFinish.Wood => "Gỗ", StepFinish.Tile => "Gạch ốp", _ => "Không ốp"
    };

    public static string StepsName(StepConstruction s) => s == StepConstruction.Concrete ? "Đổ BTCT liền khối" : "Xây gạch";

    public static string TypeName(StairType t) => t switch
    {
        StairType.Straight => "1 vế thẳng", StairType.ThreeFlightU => "3 vế chữ U", _ => "2 vế chữ U có chiếu nghỉ"
    };

    public static string SupportName(StairSupport s, int sides) =>
        s == StairSupport.Stringer ? $"Có dầm cốn {(sides >= 2 ? "2 bên" : "1 bên")}" : "Bản thang tựa dầm chiếu nghỉ / dầm sàn";

    public static double RailingDefault(RailingKind k) => k switch
    {
        RailingKind.Steel => 0.3, RailingKind.Glass => 0.5, RailingKind.Brick => 2.3, _ => 0
    };

    public double FinishT => Finish == StepFinish.None ? 0 : (FinishThickness > 0 ? FinishThickness : FinishDefaultThickness(Finish));
    public double RailingQ => Railing == RailingKind.None ? 0 : (RailingLoad > 0 ? RailingLoad : RailingDefault(Railing));

    /// <summary>Cầu thang mặc định cho lỗ sàn (dùng khi dự án chưa khai báo cầu thang).</summary>
    public static Stair FromOpening(FloorOpening o, QuantitySettings qs = null)
    {
        var s = new Stair
        {
            Id = 0, OpeningId = o.Id, Story = o.Story, X0 = o.X0, Y0 = o.Y0, X1 = o.X1, Y1 = o.Y1,
            Name = "CT-" + o.Id, Note = "Mặc định theo lỗ sàn"
        };
        if (qs != null)
        {
            if (qs.StairSlabThickness > 0) s.WaistThickness = qs.StairSlabThickness;
            s.Steps = qs.StairSteps;
            s.Finish = qs.StairFinish;
        }
        return s;
    }

    public Stair Clone() => (Stair)MemberwiseClone();

    // ------------------------------------------------------------------
    /// <summary>Danh sách cầu thang dùng tính toán: cầu thang đã khai báo + cầu thang mặc định cho mỗi lỗ sàn cầu thang chưa có khai báo.</summary>
    public static List<Stair> Effective(Project p, QuantitySettings qs = null)
    {
        var res = new List<Stair>();
        if (p == null) return res;
        var list = p.Stairs ?? new List<Stair>();
        res.AddRange(list);
        foreach (var o in p.Openings.Where(o => o.Kind == OpeningKind.Stair))
            if (!list.Any(s => s.OpeningId == o.Id)) res.Add(FromOpening(o, qs ?? p.QuantitySettings));
        return res;
    }

    /// <summary>Hình học cầu thang (không đổi Project).</summary>
    public StairLayout Layout(Project p) => StairLayout.Build(p, this);
}

/// <summary>Trọng lượng riêng vật liệu (kN/m³) và hoạt tải cầu thang dùng khi tính tải (TCVN 2737:2023).</summary>
public static class StairUnits
{
    public const double Concrete = 25.0, Brick = 18.0, Granite = 28.0, Wood = 7.0, Tile = 20.0, Mortar = 18.0;
    /// <summary>Hoạt tải tiêu chuẩn cầu thang nhà ở (TCVN 2737:2023 Bảng 4) – kN/m²</summary>
    public const double LiveDefault = 3.0;
}

/// <summary>Một vế thang trong toạ độ cục bộ (u theo phương vế, tính từ cạnh chân thang; v ngang vế).</summary>
public class StairFlight
{
    public int Index { get; set; }
    public int Risers { get; set; }
    public int Treads => Math.Max(0, Risers - 1);
    public double Width { get; set; }
    /// <summary>Chân vế (u, v) và hướng đi lên: (DU, DV) ∈ {(±1,0),(0,±1)}</summary>
    public double U0 { get; set; }
    public double V0 { get; set; }
    public int DU { get; set; }
    public int DV { get; set; }
    /// <summary>Hình chữ nhật mặt bằng của vế (cục bộ)</summary>
    public double Umin { get; set; }
    public double Umax { get; set; }
    public double Vmin { get; set; }
    public double Vmax { get; set; }
    public double ZBottom { get; set; }
    public double ZTop { get; set; }
    /// <summary>Hình chiếu bằng phần bậc = (n−1)·b (m)</summary>
    public double Run { get; set; }
    /// <summary>Chiều dài bản nghiêng giữa hai gối = (n−1)·√(b²+h²)</summary>
    public double Inclined { get; set; }
    public double CosA { get; set; }
    /// <summary>Gối dưới / gối trên: "Sàn tầng dưới", "Dầm chiếu nghỉ", "Dầm sàn tầng trên", "Móng / nền"</summary>
    public string SupportBottom { get; set; } = "";
    public string SupportTop { get; set; } = "";
}

/// <summary>Chiếu nghỉ (cục bộ).</summary>
public class StairLanding
{
    public double Umin { get; set; }
    public double Umax { get; set; }
    public double Vmin { get; set; }
    public double Vmax { get; set; }
    /// <summary>Cao độ mặt bê tông chiếu nghỉ (m)</summary>
    public double Z { get; set; }
    public double Thickness { get; set; }
    /// <summary>Nhịp tính toán của bản chiếu nghỉ (theo phương u)</summary>
    public double Span => Umax - Umin;
    public double Width => Vmax - Vmin;
    public double Area => Span * Width;
}

/// <summary>Hình học cầu thang tính từ khai báo Stair (đơn vị m).</summary>
public class StairLayout
{
    public Stair S { get; private set; }
    public bool Valid { get; private set; }
    public List<string> Warnings { get; } = new();
    public double X0, Y0, X1, Y1;
    public bool AlongX;
    /// <summary>Chiều dài ô theo phương vế / bề rộng ô</summary>
    public double Lr, Wr;
    public double ZBot, ZTop;
    public double H => ZTop - ZBot;
    public int NRisers;
    /// <summary>Chiều cao bậc h, bề rộng bậc b (m)</summary>
    public double Riser, Tread;
    public double Check2hb => 2 * Riser + Tread;
    public bool StepOk => Check2hb >= 0.60 - 1e-6 && Check2hb <= 0.65 + 1e-6;
    public double FlightWidth;
    public double WaistT, LandingT;
    public string BottomStory = "", TopStory = "";
    public List<StairFlight> Flights { get; } = new();
    public List<StairLanding> Landings { get; } = new();
    /// <summary>Dầm chiếu nghỉ tại u = LandingBeamU, nhịp theo v (0…Wr); NaN = không có</summary>
    public double LandingBeamU = double.NaN;
    public double LandingBeamZ;
    public bool HasLandingBeam => !double.IsNaN(LandingBeamU);
    public bool Stringers => S.Support == StairSupport.Stringer;
    public int StringerCount => Stringers ? Math.Clamp(S.StringerSides, 1, 2) : 0;

    /// <summary>Đổi toạ độ cục bộ (u, v) → toạ độ mặt bằng (x, y).</summary>
    public (double X, double Y) ToXY(double u, double v)
    {
        if (AlongX) return (S.StartAtMax ? X1 - u : X0 + u, Y0 + v);
        return (X0 + v, S.StartAtMax ? Y1 - u : Y0 + u);
    }

    public static StairLayout Build(Project p, Stair s)
    {
        var L = new StairLayout { S = s };
        // ---- phạm vi ----
        var op = s.OpeningId > 0 ? p?.Openings.FirstOrDefault(o => o.Id == s.OpeningId) : null;
        string story = op?.Story ?? s.Story;
        L.X0 = Math.Min(op?.X0 ?? s.X0, op?.X1 ?? s.X1); L.X1 = Math.Max(op?.X0 ?? s.X0, op?.X1 ?? s.X1);
        L.Y0 = Math.Min(op?.Y0 ?? s.Y0, op?.Y1 ?? s.Y1); L.Y1 = Math.Max(op?.Y0 ?? s.Y0, op?.Y1 ?? s.Y1);
        double dx = L.X1 - L.X0, dy = L.Y1 - L.Y0;
        L.AlongX = s.Axis == StairAxis.X || (s.Axis == StairAxis.Auto && dx >= dy);
        L.Lr = L.AlongX ? dx : dy; L.Wr = L.AlongX ? dy : dx;
        // ---- cao độ ----
        var stories = (p?.Stories ?? new List<Story>()).OrderBy(t => t.Elevation).ToList();
        var st = stories.FirstOrDefault(t => t.Name == story);
        if (st == null) { L.Warnings.Add($"Cầu thang {s.Name}: không có tầng \"{story}\"."); return L; }
        int idx = stories.IndexOf(st);
        L.ZTop = st.Elevation; L.TopStory = st.Name;
        L.ZBot = idx > 0 ? stories[idx - 1].Elevation : p.BaseElevation;
        L.BottomStory = idx > 0 ? stories[idx - 1].Name : "";
        if (st.Height > 0 && idx == 0) L.ZBot = st.Elevation - st.Height;
        if (L.H < 0.5 || L.Lr < 0.6 || L.Wr < 0.5) { L.Warnings.Add($"Cầu thang {s.Name}: ô thang hoặc chiều cao tầng quá nhỏ."); return L; }

        var type = s.Type;
        // ---- bậc ----
        int n = s.Risers > 1 ? s.Risers : Math.Max(2, (int)Math.Round(L.H / 0.17));
        if (s.Risers <= 1)
        {
            while (L.H / n > 0.18 && n < 200) n++;
            while (L.H / n < 0.15 && n > 2) n--;
        }
        L.NRisers = n;
        L.Riser = L.H / n;
        double h = L.Riser;
        // bề rộng vế
        double w;
        if (type == StairType.Straight) w = s.FlightWidth > 0 ? Math.Min(s.FlightWidth, L.Wr) : L.Wr;
        else w = s.FlightWidth > 0 ? Math.Min(s.FlightWidth, (L.Wr - Math.Max(0, s.Gap)) / 2) : (L.Wr - Math.Max(0, s.Gap)) / 2;
        if (type != StairType.Straight && w < 0.6) L.Warnings.Add($"Cầu thang {s.Name}: bề rộng vế {w * 1000:0} mm < 600 mm – ô thang hẹp.");
        w = Math.Max(0.3, w);
        L.FlightWidth = w;
        double bTarget = Math.Clamp(0.63 - 2 * h, 0.26, 0.30);
        double b = s.Tread > 0 ? s.Tread : bTarget;
        double ldMin = s.LandingDepth > 0 ? s.LandingDepth : Math.Max(w, 0.9);

        // chia số bậc theo vế
        int[] nf;
        if (type == StairType.Straight) nf = new[] { n };
        else if (type == StairType.ThreeFlightU)
        {
            int n2 = Math.Clamp((int)Math.Floor((L.Wr - 2 * w) / b + 1e-9) + 1, 2, Math.Max(2, n - 4));
            if (L.Wr - 2 * w < b + 1e-6)
            {
                L.Warnings.Add($"Cầu thang {s.Name}: ô thang không đủ rộng cho 3 vế – tính như thang 2 vế chữ U.");
                type = StairType.TwoFlightU;
                w = s.FlightWidth > 0 ? Math.Min(s.FlightWidth, (L.Wr - Math.Max(0, s.Gap)) / 2) : (L.Wr - Math.Max(0, s.Gap)) / 2;
                L.FlightWidth = w = Math.Max(0.3, w);
                nf = Split2(n, s.FirstFlightRisers);
            }
            else
            {
                int n1 = s.FirstFlightRisers > 1 ? Math.Min(s.FirstFlightRisers, n - n2 - 2) : (int)Math.Ceiling((n - n2) / 2.0);
                nf = new[] { n1, n2, n - n2 - n1 };
                ldMin = w;
            }
        }
        else nf = Split2(n, s.FirstFlightRisers);
        if (nf.Any(k => k < 2)) { L.Warnings.Add($"Cầu thang {s.Name}: số bậc mỗi vế quá ít."); return L; }

        // ---- bề rộng bậc theo chiều dài ô ----
        int m = type == StairType.ThreeFlightU ? Math.Max(nf[0], nf[2]) - 1 : nf.Max() - 1;
        double avail = type == StairType.Straight ? L.Lr : L.Lr - ldMin;
        if (s.Tread <= 0 && m * b > avail + 1e-9) b = avail / m;
        if (m * b > avail + 1e-6)
            L.Warnings.Add($"Cầu thang {s.Name}: {m} bậc × {b * 1000:0} mm = {m * b:0.00} m dài hơn chỗ cho vế ({avail:0.00} m).");
        if (b < 0.22) L.Warnings.Add($"Cầu thang {s.Name}: bề rộng bậc {b * 1000:0} mm < 220 mm – thang quá dốc, nên tăng chiều dài ô thang.");
        L.Tread = b;
        if (!L.StepOk) L.Warnings.Add($"Cầu thang {s.Name}: 2h + b = {L.Check2hb * 1000:0} mm ngoài khoảng 600–650 mm (h = {h * 1000:0}, b = {b * 1000:0} mm).");
        if (h > 0.19) L.Warnings.Add($"Cầu thang {s.Name}: chiều cao bậc {h * 1000:0} mm > 190 mm.");
        double diag = Math.Sqrt(b * b + h * h), cosA = b / diag;

        StairFlight F(int i, int risers, double u0, double v0, int du, int dv, double z0)
        {
            double run = (risers - 1) * b;
            var f = new StairFlight
            {
                Index = i, Risers = risers, Width = w, U0 = u0, V0 = v0, DU = du, DV = dv, ZBottom = z0, ZTop = z0 + risers * h,
                Run = run, Inclined = (risers - 1) * diag, CosA = cosA
            };
            double u1 = u0 + du * run, v1 = v0 + dv * run;
            if (du != 0) { f.Umin = Math.Min(u0, u1); f.Umax = Math.Max(u0, u1); f.Vmin = v0; f.Vmax = v0 + w; }
            else { f.Vmin = Math.Min(v0, v1); f.Vmax = Math.Max(v0, v1); f.Umin = u0; f.Umax = u0 + w; }
            return f;
        }

        string bottomSup = string.IsNullOrEmpty(L.BottomStory) ? "Móng / nền tầng trệt" : "Dầm, sàn tầng " + L.BottomStory;
        string topSup = "Dầm sàn tầng " + L.TopStory;
        switch (type)
        {
            case StairType.Straight:
            {
                double run = (n - 1) * b;
                var f = F(1, n, Math.Max(0, L.Lr - run), 0, 1, 0, L.ZBot);
                f.SupportBottom = bottomSup; f.SupportTop = topSup;
                L.Flights.Add(f);
                break;
            }
            case StairType.ThreeFlightU:
            {
                double uL = L.Lr - w;
                double r1 = (nf[0] - 1) * b, r3 = (nf[2] - 1) * b;
                var f1 = F(1, nf[0], uL - r1, 0, 1, 0, L.ZBot);
                double z1 = f1.ZTop;
                var f2 = F(2, nf[1], uL, w, 0, 1, z1);
                double z2 = f2.ZTop;
                // vế 3 chạy ngược về cạnh chân thang, chân vế tại u = uL
                var f3 = F(3, nf[2], uL, L.Wr - w, -1, 0, z2);
                f1.SupportBottom = bottomSup; f1.SupportTop = "Dầm chiếu nghỉ";
                f2.SupportBottom = "Chiếu nghỉ 1"; f2.SupportTop = "Chiếu nghỉ 2";
                f3.SupportBottom = "Dầm chiếu nghỉ"; f3.SupportTop = topSup;
                L.Flights.Add(f1); L.Flights.Add(f2); L.Flights.Add(f3);
                L.Landings.Add(new StairLanding { Umin = uL, Umax = L.Lr, Vmin = 0, Vmax = w, Z = z1 });
                L.Landings.Add(new StairLanding { Umin = uL, Umax = L.Lr, Vmin = L.Wr - w, Vmax = L.Wr, Z = z2 });
                if (s.LandingBeam) { L.LandingBeamU = uL; L.LandingBeamZ = z1; }
                break;
            }
            default:
            {
                double r1 = (nf[0] - 1) * b, r2 = (nf[1] - 1) * b;
                double uL = s.LandingDepth > 0 ? L.Lr - s.LandingDepth : Math.Max(r1, r2);
                uL = Math.Clamp(uL, 0.3, L.Lr - 0.3);
                var f1 = F(1, nf[0], uL - r1, 0, 1, 0, L.ZBot);
                var f2 = F(2, nf[1], uL, L.Wr - w, -1, 0, f1.ZTop);
                f1.SupportBottom = bottomSup; f1.SupportTop = s.LandingBeam ? "Dầm chiếu nghỉ" : "Chiếu nghỉ (bản gãy khúc)";
                f2.SupportBottom = s.LandingBeam ? "Dầm chiếu nghỉ" : "Chiếu nghỉ (bản gãy khúc)"; f2.SupportTop = topSup;
                L.Flights.Add(f1); L.Flights.Add(f2);
                L.Landings.Add(new StairLanding { Umin = uL, Umax = L.Lr, Vmin = 0, Vmax = L.Wr, Z = f1.ZTop });
                if (s.LandingBeam) { L.LandingBeamU = uL; L.LandingBeamZ = f1.ZTop; }
                if (f1.Umin < -1e-6 || f2.Umin < -1e-6) L.Warnings.Add($"Cầu thang {s.Name}: vế thang vượt ra ngoài ô thang – kiểm tra số bậc / bề rộng bậc.");
                break;
            }
        }
        // ---- chiều dày ----
        double span = L.Stringers ? L.FlightWidth : L.Flights.Max(f => f.Inclined + (s.LandingBeam || L.Landings.Count == 0 ? 0 : L.Landings[0].Span));
        L.WaistT = s.WaistThickness > 0 ? s.WaistThickness : Math.Max(0.10, CeilTo(span / 30, 0.01));
        L.LandingT = s.LandingThickness > 0 ? s.LandingThickness : L.WaistT;
        foreach (var ld in L.Landings) ld.Thickness = L.LandingT;
        if (!L.Stringers && L.WaistT < span / 35 - 1e-9)
            L.Warnings.Add($"Cầu thang {s.Name}: bản thang dày {L.WaistT * 1000:0} mm < L/35 = {span / 35 * 1000:0} mm – nên tăng chiều dày.");
        L.Valid = true;
        return L;
    }

    private static int[] Split2(int n, int first)
    {
        // số bậc lẻ: vế trên nhiều hơn 1 bậc → vế trên về đúng mép sàn tầng trên (dầm viền ô thang)
        int n1 = first > 1 && first < n - 1 ? first : n / 2;
        return new[] { n1, n - n1 };
    }

    private static double CeilTo(double v, double step) => Math.Round(Math.Ceiling(v / step - 1e-9) * step, 6);

    // ------------------------------------------------------------------
    // Khối lượng hình học (dùng chung cho tải, khối lượng, thuyết minh)
    // ------------------------------------------------------------------
    /// <summary>Bề rộng phần bản giữa các dầm cốn (bản thang không kể dầm cốn).</summary>
    public double SlabWidth(StairFlight f) => Math.Max(0.1, f.Width - StringerCount * S.StringerB);

    /// <summary>Thể tích bản thang của vế (không kể bậc, không kể dầm cốn): L nghiêng × bề rộng × t.</summary>
    public double FlightSlabVolume(StairFlight f) => f.Inclined * SlabWidth(f) * WaistT;

    /// <summary>Thể tích bậc tam giác của vế: Σ ½·b·h·w (n − 1 bậc trên bản nghiêng).</summary>
    public double StepVolume(StairFlight f) => f.Treads * 0.5 * Tread * Riser * f.Width;

    /// <summary>Diện tích ốp mặt bậc + cổ bậc của vế: ((n−1)·b + n·h)·w.</summary>
    public double FinishArea(StairFlight f) => (f.Treads * Tread + f.Risers * Riser) * f.Width;

    /// <summary>Phần chiếu nghỉ đã trừ nửa dầm chiếu nghỉ (dầm tính riêng).</summary>
    public double LandingSlabSpan(StairLanding ld) =>
        Math.Max(0, ld.Span - (HasLandingBeam && Math.Abs(ld.Umin - LandingBeamU) < 1e-6 ? S.LandingBeamB / 2 : 0));
}

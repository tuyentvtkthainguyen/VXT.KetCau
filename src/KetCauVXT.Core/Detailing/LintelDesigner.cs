using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Detailing;

// =====================================================================
// LANH TÔ CỬA – Ô VĂNG CỬA (chế độ Triển khai hồ sơ)
// Lanh tô: dầm đơn giản gác lên tường hai bên ô cửa, l0 = min(1,05·Lo; Lo + a).
// Tải tường phía trên: khi tường cao ≥ Lo/3 và không có sàn / dầm tựa lên tường trong phạm vi đó
// → chỉ lấy tải tường cao Lo/3 (hiệu ứng vòm của khối xây).
// Ô văng: console ngàm vào lanh tô (hoặc dầm có sẵn), mô men ngàm per 1 m dài; tải sửa chữa phân bố hoặc
// tải tập trung 1 kN ở mút (TCVN 2737:2023 – mái / ô văng không sử dụng, không lấy đồng thời).
// Lanh tô + ô văng liền khối: lanh tô chịu xoắn T = mt·Lo/2 khi hai đầu liền cột / giằng tường;
// nếu chỉ gác lên tường → kiểm tra chống lật quanh mép ngoài tường K = Mgiữ / Mlật ≥ 1,3.
// Đơn vị: m, kN, kN.m; ứng suất kN/m²; Ø mm.
// =====================================================================

public class ManualLintel : IDetailMember
{
    public const string TypeLintel = "Lanh tô";
    public const string TypeCombined = "Lanh tô + ô văng liền khối";
    public const string TypeCanopy = "Ô văng (console từ dầm/lanh tô có sẵn)";

    /// <summary>Trọng lượng riêng vữa trát (kN/m³).</summary>
    public const double GammaPlaster = 18;
    /// <summary>Hệ số an toàn chống lật tối thiểu.</summary>
    public const double OverturnLimit = 1.3;

    [Field("Tên lanh tô / ô văng", Group = "Chung")] public string Name { get; set; } = "LT1";
    [Field("Số lượng", Group = "Chung")] public int Quantity { get; set; } = 1;
    [Field("Loại cấu kiện", Group = "Chung", Choices = TypeLintel + "|" + TypeCombined + "|" + TypeCanopy)] public string Type { get; set; } = TypeCombined;

    [Field("Bề rộng ô cửa Lo", Unit = "m", Group = "Lanh tô")] public double Lo { get; set; } = 1.2;
    [Field("Chiều dày tường (= bề rộng lanh tô)", Unit = "mm", Scale = 1000, Group = "Lanh tô", Hint = "220 hoặc 110")] public double Bw { get; set; } = 0.22;
    [Field("Chiều cao lanh tô (0 = tự chọn)", Unit = "mm", Scale = 1000, Group = "Lanh tô", Hint = "tự chọn: max(Lo/10; 150 mm), làm tròn lên 50 mm")] public double Hl { get; set; }
    [Field("Chiều dài gối mỗi bên (0 = max(200; h))", Unit = "mm", Scale = 1000, Group = "Lanh tô", Hint = "gối lên tường ≥ 200 mm")] public double La { get; set; }
    [Field("Lớp bảo vệ tới mép cốt dọc", Unit = "mm", Scale = 1000, Group = "Lanh tô")] public double Cover { get; set; } = 0.02;
    [Field("Cường độ chịu nén tính toán khối xây dưới gối R", Unit = "MPa", Group = "Lanh tô",
        Hint = "TCVN 5573:2011 – gạch đặc M75, vữa M50 ≈ 1,3 MPa; gạch M100 vữa M75 ≈ 1,7 MPa; gạch rỗng / bê tông nhẹ nhỏ hơn")] public double BrickR { get; set; } = 1.3;
    [Field("Lanh tô hai đầu liền cột / giằng tường (chịu xoắn)", Group = "Lanh tô",
        Hint = "bỏ chọn: lanh tô chỉ gác lên tường → kiểm tra chống lật ô văng")] public bool AnchoredEnds { get; set; } = true;

    [Field("Chiều cao tường xây phía trên lanh tô", Unit = "m", Group = "Tải trọng")] public double Hm { get; set; } = 0.6;
    [Field("Trọng lượng riêng khối xây", Unit = "kN/m³", Group = "Tải trọng")] public double GammaWall { get; set; } = 18;
    [Field("Vữa trát mỗi mặt", Unit = "mm", Scale = 1000, Group = "Tải trọng")] public double PlasterT { get; set; } = 0.015;
    [Field("Sàn / dầm tựa lên tường trong phạm vi Lo/3 phía trên", Group = "Tải trọng")] public bool FloorOnWall { get; set; }
    [Field("Tải sàn / dầm truyền lên lanh tô (tính toán)", Unit = "kN/m", Group = "Tải trọng")] public double Q2 { get; set; }

    [Field("Vươn ô văng Lc", Unit = "m", Group = "Ô văng")] public double Lc { get; set; } = 0.6;
    [Field("Dày ô văng tại ngàm", Unit = "mm", Scale = 1000, Group = "Ô văng")] public double Tr { get; set; } = 0.08;
    [Field("Dày ô văng tại mút", Unit = "mm", Scale = 1000, Group = "Ô văng")] public double Tt { get; set; } = 0.06;
    [Field("Chiều dài ô văng (0 = Lo + 2·gối)", Unit = "m", Group = "Ô văng")] public double CanopyLength { get; set; }
    [Field("Bề rộng dầm đỡ (loại ô văng console)", Unit = "mm", Scale = 1000, Group = "Ô văng")] public double HostWidth { get; set; } = 0.22;
    [Field("Hoàn thiện (láng, chống thấm)", Unit = "kN/m²", Group = "Ô văng")] public double Finish { get; set; } = 0.5;
    [Field("Hoạt tải sửa chữa", Unit = "kN/m²", Group = "Ô văng")] public double Live { get; set; } = 0.75;
    [Field("Tải tập trung ở mút (không đồng thời hoạt tải)", Unit = "kN", Group = "Ô văng")] public double Ptip { get; set; } = 1.0;
    [Field("Có gờ chắn nước ở mút", Group = "Ô văng")] public bool Upstand { get; set; } = true;
    [Field("Gờ – chiều cao", Unit = "mm", Scale = 1000, Group = "Ô văng")] public double UpH { get; set; } = 0.10;
    [Field("Gờ – chiều dày", Unit = "mm", Scale = 1000, Group = "Ô văng")] public double UpT { get; set; } = 0.06;

    [Field("Thép dưới lanh tô – số thanh (0 = tự chọn)", Group = "Thép")] public int BotN { get; set; }
    [Field("Thép dưới lanh tô – Ø (0 = tự chọn)", Unit = "mm", Group = "Thép")] public int BotD { get; set; }
    [Field("Thép trên lanh tô – số thanh (0 = tự chọn)", Group = "Thép")] public int TopN { get; set; }
    [Field("Thép trên lanh tô – Ø (0 = tự chọn)", Unit = "mm", Group = "Thép")] public int TopD { get; set; }
    [Field("Đai lanh tô – Ø", Unit = "mm", Group = "Thép")] public int StirD { get; set; } = 6;
    [Field("Đai lanh tô – bước (0 = tự chọn)", Unit = "mm", Scale = 1000, Group = "Thép")] public double StirS { get; set; }
    [Field("Thép chịu lực ô văng – Ø (0 = tự chọn)", Unit = "mm", Group = "Thép")] public int CanD { get; set; }
    [Field("Thép chịu lực ô văng – bước", Unit = "mm", Scale = 1000, Group = "Thép")] public double CanS { get; set; }
    [Field("Thép phân bố ô văng – Ø", Unit = "mm", Group = "Thép")] public int DistD { get; set; } = 6;
    [Field("Thép phân bố ô văng – bước", Unit = "mm", Scale = 1000, Group = "Thép")] public double DistS { get; set; } = 0.25;

    // ---- kết quả (để vẽ, kiểm thử) ----
    [System.Text.Json.Serialization.JsonIgnore] public double HlUsed { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore] public double LaUsed { get; private set; }
    /// <summary>Chiều cao tường tính tải (sau quy tắc hiệu ứng vòm), m.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public double HwEff { get; private set; }
    /// <summary>Tải tường tính toán lên lanh tô (kN/m).</summary>
    [System.Text.Json.Serialization.JsonIgnore] public double WallLoad { get; private set; }
    /// <summary>Tải ô văng truyền xuống lanh tô (tính toán, kN/m).</summary>
    [System.Text.Json.Serialization.JsonIgnore] public double CanopyLoad { get; private set; }
    /// <summary>Tổng tải tính toán lanh tô (kN/m).</summary>
    [System.Text.Json.Serialization.JsonIgnore] public double Q { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore] public double L0 { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore] public double M { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore] public double V { get; private set; }
    /// <summary>Mô men ngàm ô văng trên 1 m dài (kN.m/m).</summary>
    [System.Text.Json.Serialization.JsonIgnore] public double MRoot { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore] public double VRoot { get; private set; }
    /// <summary>Mô men xoắn lanh tô tại gối (kN.m).</summary>
    [System.Text.Json.Serialization.JsonIgnore] public double Torsion { get; private set; }
    /// <summary>Hệ số chống lật K = Mgiữ / Mlật (khi lanh tô chỉ gác lên tường); 0 nếu không kiểm tra.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public double OverturnK { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore] public double AsLintel { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore] public double AsCanopy { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore] public double CanopyLen { get; private set; }

    public bool HasLintel => !(Type ?? "").StartsWith("Ô văng");
    public bool HasCanopy => (Type ?? "").Contains("ô văng") || (Type ?? "").StartsWith("Ô văng");

    /// <summary>Lanh tô + ô văng cửa đi 1,2 m, tường 220, hai đầu liền giằng tường – đạt mọi kiểm tra.</summary>
    public static ManualLintel Sample() => new() { Name = "LT1", Type = TypeCombined, Lo = 1.2, Bw = 0.22, Lc = 0.6, AnchoredEnds = true };

    /// <summary>Tường mỏng (≤ 120 mm – tường 110): áp dụng gợi ý an toàn lanh tô tường 110.</summary>
    public const double Thin = 0.12;
    /// <summary>Ô cửa lớn trong tường 110: Lo &gt; 1,8 m → khuyến nghị bổ trụ BTCT / lanh tô liền giằng – cột.</summary>
    public const double LargeDoor110 = 1.8;

    /// <summary>Chiều cao lanh tô tường 110 tự chọn: max(Lo/10; 150 mm), Lo &gt; 1,5 m lấy ≥ 200 mm; làm tròn lên 50 mm.</summary>
    public static double AutoHeight110(double lo) => RcSection.CeilTo(Math.Max(Math.Max(lo / 10, 0.15), lo > 1.5 + 1e-9 ? 0.20 : 0), 0.05);

    /// <summary>Gối (ngàm) lanh tô tường 110 mỗi bên: ≥ 200 mm (Lo ≤ 1,5 m), ≥ 250 mm (Lo &gt; 1,5 m), và ≥ h.</summary>
    public static double MinBearing110(double lo) => lo > 1.5 + 1e-9 ? 0.25 : 0.20;
    public static double AutoBearing110(double lo, double h) => Math.Max(MinBearing110(lo), h);

    /// <summary>Khả năng chịu nén cục bộ của khối xây dưới đầu lanh tô (kN): N ≤ ψ·d·R·Ac, ψ = 0,5 (ứng suất tam giác dưới đầu dầm uốn),
    /// d = 1,5 − 0,5·ψ = 1,25, Ac = t·a (TCVN 5573:2011 mục nén cục bộ, lấy hệ số tăng cường ξ = 1 thiên an toàn). R MPa, t, a m.</summary>
    public static double BearingCapacity(double rMpa, double t, double a) => 0.5 * 1.25 * Math.Max(rMpa, 0) * 1000 * t * a;

    /// <summary>Gợi ý an toàn lanh tô tường 110 (cửa đi các phòng, cửa đi lớn): chiều cao, gối mỗi bên, nén cục bộ khối xây, ô cửa lớn.</summary>
    private void Wall110(DetailResult r, double hl, double la)
    {
        double hMin = AutoHeight110(Lo), aMin = MinBearing110(Lo);
        r.Calc.Add("## Tường 110 – gợi ý an toàn lanh tô: h ≥ max(Lo/10; 150 mm" + (Lo > 1.5 ? "; 200 mm khi Lo > 1,5 m" : "") + ") = " + Dt.Mm(hMin)
                   + " mm; gối mỗi bên ≥ " + Dt.Mm(aMin) + " mm (Lo " + (Lo > 1.5 ? "> " : "≤ ") + "1,5 m); bề rộng lanh tô = chiều dày tường " + Dt.Mm(Bw) + " mm");
        r.Messages.Add("Gợi ý (tường 110): tiết diện lanh tô " + Dt.Mm(Bw) + "×" + Dt.Mm(Math.Max(hl, hMin)) + " mm, gối (ngàm) vào tường mỗi bên ≥ " + Dt.Mm(aMin)
                       + " mm, thép dưới ≥ 2Ø10, đai Ø6a150; xây tường phía trên sau khi bê tông lanh tô đủ cường độ");
        r.Checks.Add(Dt.Check("Tường 110: chiều cao lanh tô h ≥ " + Dt.Mm(hMin) + " mm", hMin * 1000, hl * 1000, "mm"));
        r.Checks.Add(Dt.Check("Tường 110: gối mỗi bên a ≥ " + Dt.Mm(aMin) + " mm", aMin * 1000, la * 1000, "mm"));
        double nu = BearingCapacity(BrickR, Bw, la);
        r.Calc.Add("Nén cục bộ khối xây dưới gối lanh tô: N = Q = " + Dt.F(V, "0.00") + " kN ≤ ψ·d·R·t·a = 0,5·1,25·" + Dt.F(BrickR, "0.0#") + "·10³·" + Dt.F(Bw, "0.###") + "·" + Dt.F(la, "0.###")
                   + " = " + Dt.F(nu, "0.00") + " kN (TCVN 5573:2011, ξ = 1)");
        r.Checks.Add(Dt.Check("Tường 110: nén cục bộ khối xây dưới gối lanh tô", Math.Max(V, 0), nu, "kN", "R = " + Dt.F(BrickR, "0.0#") + " MPa"));
        if (Lo > LargeDoor110 + 1e-9)
            r.Messages.Add("Gợi ý (tường 110, cửa lớn Lo = " + Dt.F(Lo) + " m > " + Dt.F(LargeDoor110) + " m): trụ gạch 110 hai bên cửa chịu gối yếu – nên bổ trụ BTCT 110×220 hai bên ô cửa "
                           + "(thép 4Ø12, đai Ø6a150, neo vào dầm / sàn trên dưới) và đổ lanh tô liền trụ, hoặc thay lanh tô bằng dầm BTCT gác lên cột");
    }

    /// <summary>Chiều cao lanh tô tự chọn: max(Lo/10; 0,15 m), làm tròn lên 50 mm.</summary>
    public static double AutoHeight(double lo) => RcSection.CeilTo(Math.Max(lo / 10, 0.15), 0.05);

    /// <summary>Chiều cao tường tính tải: tường cao ≥ Lo/3 và không có sàn / dầm tựa trong phạm vi đó → Lo/3 (hiệu ứng vòm), ngược lại toàn bộ Hm.</summary>
    public static double EffectiveWallHeight(double lo, double hm, bool floorOnWall) =>
        !floorOnWall && hm >= lo / 3 ? lo / 3 : Math.Max(hm, 0);

    /// <summary>Mô men ngàm ô văng tính toán trên 1 m dài: bản thân (dày thay đổi Tr → Tt) + hoàn thiện + gờ + max(hoạt tải phân bố; tải tập trung ở mút).</summary>
    public static (double M, double V) CanopyRoot(double lc, double tr, double tt, double finish, double live, double p, bool upstand, double upH, double upT)
    {
        double self = 1.1 * 25 * (tr + tt) / 2 * lc;
        double mSelf = 1.1 * 25 * lc * lc * (tr / 6 + tt / 3);
        double gUp = upstand ? 1.1 * 25 * upT * upH : 0;
        double mLive = Math.Max(1.3 * live * lc * lc / 2, 1.3 * p * lc);
        double vLive = Math.Max(1.3 * live * lc, 1.3 * p);
        double m = mSelf + 1.3 * finish * lc * lc / 2 + gUp * (lc - upT / 2) + mLive;
        double v = self + 1.3 * finish * lc + gUp + vLive;
        return (m, v);
    }

    /// <summary>Tải tường tính toán (kN/m): 1,1·γ·b·h + 1,3·(2 lớp trát)·18·h.</summary>
    public static double WallLineLoad(double gammaWall, double bw, double plasterT, double hw) =>
        1.1 * gammaWall * bw * hw + 1.3 * 2 * plasterT * GammaPlaster * hw;

    public DetailResult Run(DetailSettings s)
    {
        s ??= new DetailSettings();
        var r = new DetailResult { Kind = "Lanh tô – ô văng", Name = Name, Quantity = Math.Max(1, Quantity) };
        try { Compute(r, s); }
        catch (Exception e) { r.Messages.Add("LỖI tính toán lanh tô / ô văng: " + e.Message); }
        return r;
    }

    private static bool Bad(double v) => double.IsNaN(v) || double.IsInfinity(v);

    private void Compute(DetailResult r, DetailSettings s)
    {
        var conc = DetailShim.TryConcrete(s.Concrete, r.Messages);
        var main = DetailShim.TryRebar(s.RebarMain, r.Messages);
        var small = DetailShim.TryRebar(s.RebarSmall, r.Messages);
        Material St(int d) => d <= 10 ? small : main;
        r.ConcreteClass = conc.Name;
        bool lintel = HasLintel, canopy = HasCanopy;
        if (Bad(Lo) || Bad(Bw) || Bad(Lc) || Bad(Hm) || Lo <= 0.1 || Bw < 0.08 || (canopy && (Lc <= 0.05 || Tr < 0.04 || Tt < 0.03)))
        {
            r.Messages.Add("LỖI kích thước không hợp lệ (Lo ≥ 100 mm, tường ≥ 80 mm; ô văng: Lc ≥ 50 mm, dày ngàm ≥ 40 mm, dày mút ≥ 30 mm)");
            return;
        }
        double c = Math.Max(Cover, 0.015);
        double b = Bw;
        bool thin = Bw <= Thin + 1e-9;
        double hl = Hl > 0 ? Hl : thin ? AutoHeight110(Lo) : AutoHeight(Lo);
        double la = La > 0 ? La : thin ? AutoBearing110(Lo, hl) : Math.Max(0.20, hl);
        HlUsed = hl; LaUsed = la;
        double ltot = Lo + 2 * la;
        CanopyLen = CanopyLength > 0 ? CanopyLength : ltot;
        r.Calc.Add("## " + (Type ?? "") + " " + Name + ": ô cửa Lo = " + Dt.F(Lo) + " m, tường " + Dt.Mm(Bw) + " mm"
                   + (lintel ? ", lanh tô " + Dt.Mm(b) + "×" + Dt.Mm(hl) + " gối " + Dt.Mm(la) + " mm mỗi bên" : "")
                   + (canopy ? ", ô văng vươn " + Dt.Mm(Lc) + " mm dày " + Dt.Mm(Tr) + " → " + Dt.Mm(Tt) + " mm" : ""));

        // ---------------- ô văng ----------------
        if (canopy)
        {
            var (mr, vr) = CanopyRoot(Lc, Tr, Tt, Finish, Live, Ptip, Upstand, UpH, UpT);
            MRoot = mr; VRoot = vr;
            r.Calc.Add("Ô văng (dải 1 m): bản thân 1,1·25·Lc²·(hr/6 + ht/3) = " + Dt.F(1.1 * 25 * Lc * Lc * (Tr / 6 + Tt / 3), "0.000") + " kN.m; hoàn thiện 1,3·g·Lc²/2; gờ chắn nước ở mút; "
                       + "hoạt tải sửa chữa max(1,3·p·Lc²/2 = " + Dt.F(1.3 * Live * Lc * Lc / 2, "0.000") + "; 1,3·P·Lc = " + Dt.F(1.3 * Ptip * Lc, "0.000") + ") kN.m (TCVN 2737:2023 – không lấy đồng thời)");
            r.Calc.Add("Mô men ngàm M = " + Dt.F(mr, "0.000") + " kN.m/m; lực cắt Q = " + Dt.F(vr, "0.00") + " kN/m");
            double h0c = Tr - c - 0.004;
            var fr = RcSection.DesignRect(mr, 1.0, h0c, c, conc, St(8), 0.1);
            AsCanopy = fr.As;
            (int D, double S) cb = CanD > 0 && CanS > 0 ? (CanD, CanS) : Dt.ChooseSpaced(fr.As, new[] { 6, 8, 10, 12 }, 0.20, 0.10);
            h0c = Tr - c - cb.D / 2000.0;
            double muC = RcSection.MomentCapacity(1.0, h0c, Dt.AreaPerM(cb.D, cb.S), 0, c, conc, St(cb.D));
            r.Calc.Add("Thép chịu lực ô văng (lớp trên): As yêu cầu = " + Dt.F(fr.As * 1e4, "0.00") + " cm²/m → " + Dt.Spaced(cb.D, cb.S) + " (As = "
                       + Dt.F(Dt.AreaPerM(cb.D, cb.S) * 1e4, "0.00") + " cm²/m, h0 = " + Dt.Mm(h0c) + " mm) → Mu = " + Dt.F(muC, "0.000") + " kN.m/m");
            r.Checks.Add(Dt.Check("Ô văng chịu uốn tại ngàm (" + Dt.Spaced(cb.D, cb.S) + ")", mr, muC, "kN.m/m"));
            double qb = 0.5 * conc.Rbt * 1.0 * h0c;
            r.Checks.Add(Dt.Check("Ô văng chịu cắt không cốt đai Q ≤ 0,5·Rbt·b·h0", vr, qb, "kN/m", "TCVN 5574:2018 mục 8.1.3.3"));
            r.Checks.Add(Dt.Check("Độ mảnh ô văng Lc/hr ≤ 12 (kiểm soát võng)", Lc / Tr, 12, ""));
            if (lintel) r.Checks.Add(Dt.Check("Chiều dày ngàm ô văng ≤ chiều cao lanh tô", Tr * 1000, hl * 1000, "mm"));
            r.Calc.Add("Bề rộng vết nứt: ô văng ngoài trời, bước thép ≤ 200 mm, lớp bảo vệ ≥ 15 mm – thoả mãn cấu tạo chống nứt; láng vữa xi măng mác cao + chống thấm mặt trên, dốc i = 1–2 % ra phía ngoài, rãnh nhỏ giọt ở mặt dưới mút.");

            // neo thép ô văng vào lanh tô / dầm
            double host = lintel ? b : Math.Max(HostWidth, 0.1);
            double hostH = lintel ? hl : Math.Max(0.3, Tr);
            double lan = Math.Max(30 * cb.D / 1000.0, RebarDetailing.AnchorageLength(conc, St(cb.D), cb.D));
            double horiz = Lc - c + host - c;
            double legIn = Dt.R10(Math.Min(Math.Max(lan - (host - c), 10 * cb.D / 1000.0), Math.Max(hostH - 2 * c, 0.05)));
            double legTip = Dt.R10(Math.Max(Tt - 2 * c, 0.03));
            if (host - c + legIn < lan - 1e-6)
                r.Messages.Add("Thép ô văng neo vào " + (lintel ? "lanh tô" : "dầm") + " " + Dt.Mm(host - c + legIn) + " mm < lan = " + Dt.Mm(lan) + " mm – kéo dài thép sang sàn / dùng móc hoặc tăng bề rộng gối");
            var mainBar = Dt.Hooked("Ô văng chịu lực", cb.D, Dt.CountAcross(CanopyLen, 0.05, cb.S), Dt.R10(horiz), legTip, legIn, true, cb.S);
            r.Bars.Add(mainBar);
            int dd = DistD > 0 ? DistD : 6; double ds = DistS > 0 ? DistS : 0.25;
            int nDist = Math.Max(2, Dt.CountAcross(Lc, c, ds));
            r.Bars.Add(Dt.Straight("Ô văng phân bố", dd, nDist, Dt.R10(CanopyLen - 2 * c), true, ds));
            if (Upstand && UpH > 0 && UpT > 0)
            {
                var up = Dt.Hooked("Gờ ô văng", 6, Dt.CountAcross(CanopyLen, 0.05, 0.25), Dt.R10(UpH + Tt - 2 * c), 0, Dt.R10(Math.Max(0.15, Lc / 3)), false, 0.25);
                r.Bars.Add(up);
                r.Bars.Add(Dt.Straight("Gờ ô văng – thép dọc", 6, 1, Dt.R10(CanopyLen - 2 * c), true));
            }
        }

        // ---------------- lanh tô ----------------
        if (lintel)
        {
            HwEff = EffectiveWallHeight(Lo, Hm, FloorOnWall);
            WallLoad = WallLineLoad(GammaWall, Bw, PlasterT, HwEff);
            double self = 1.1 * 25 * b * hl;
            CanopyLoad = canopy
                ? 1.1 * 25 * (Tr + Tt) / 2 * Lc + 1.3 * Finish * Lc + (Upstand ? 1.1 * 25 * UpT * UpH : 0) + 1.3 * Live * Lc
                : 0;
            double q2 = FloorOnWall ? Math.Max(Q2, 0) : 0;
            Q = self + WallLoad + CanopyLoad + q2;
            L0 = Math.Min(1.05 * Lo, Lo + la);
            M = Q * L0 * L0 / 8;
            V = Q * Lo / 2;
            r.Calc.Add("Tường phía trên cao " + Dt.F(Hm) + " m" + (!FloorOnWall && Hm >= Lo / 3
                ? " ≥ Lo/3 = " + Dt.F(Lo / 3, "0.000") + " m, không có sàn / dầm tựa trong phạm vi đó → khi tường phía trên cao ≥ Lo/3 chỉ lấy tải tường trong phạm vi Lo/3 (hiệu ứng vòm): hw = " + Dt.F(HwEff, "0.000") + " m"
                : " → lấy toàn bộ: hw = " + Dt.F(HwEff, "0.000") + " m"));
            r.Calc.Add("Tải tường gw = 1,1·γ·b·hw + 1,3·2·δtrát·18·hw = " + Dt.F(WallLoad, "0.00") + " kN/m; bản thân lanh tô 1,1·25·b·h = " + Dt.F(self, "0.00") + " kN/m"
                       + (canopy ? "; ô văng truyền xuống " + Dt.F(CanopyLoad, "0.00") + " kN/m" : "") + (q2 > 0 ? "; sàn / dầm " + Dt.F(q2, "0.00") + " kN/m" : "")
                       + " → q = " + Dt.F(Q, "0.00") + " kN/m");
            r.Calc.Add("Sơ đồ dầm đơn giản l0 = min(1,05·Lo; Lo + a) = " + Dt.F(L0, "0.000") + " m → M = q·l0²/8 = " + Dt.F(M, "0.000") + " kN.m; Q = q·Lo/2 = " + Dt.F(V, "0.00") + " kN");

            // xoắn / chống lật
            double asTorsion = 0;
            int sd = StirD > 0 ? StirD : 6;
            // bước đai tự chọn: min(h0/2; 200 mm), không nhỏ hơn 100 mm (lanh tô thấp – tối thiểu cấu tạo)
            double ss = StirS > 0 ? StirS : RcSection.FloorTo(Math.Min(Math.Max((hl - c) / 2, 0.10), 0.20), 0.05);
            if (ss < 0.05) ss = 0.05;
            if (canopy)
            {
                Torsion = MRoot * Lo / 2;
                if (AnchoredEnds)
                {
                    double h0t = hl - c - 0.006;
                    var tr = RcTorsion.Design(Torsion, V, b, hl, h0t, c, conc, small, sd, ss, main);
                    asTorsion = tr.AsLong;
                    if (StirS <= 0 && tr.S < ss) ss = tr.S;
                    r.Calc.Add("Lanh tô chịu xoắn do ô văng: mt = " + Dt.F(MRoot, "0.000") + " kN.m/m → T = mt·Lo/2 = " + Dt.F(Torsion, "0.000") + " kN.m (hai đầu liền cột / giằng tường); "
                               + "Asl = T·uk/(2·Ak·Rs) = " + Dt.F(asTorsion * 1e4, "0.00") + " cm² (chia đều thép trên / dưới) – TCVN 5574:2018 mục 8.1.4");
                    r.Checks.Add(Dt.Check("Xoắn lanh tô – dải bê tông chịu nén T ≤ 0,1·Rb·b²·h", Torsion, tr.TStrutReduced, "kN.m"));
                    // khả năng xoắn của đai kín với bước chọn cuối cùng, sau khi trừ phần đai chịu cắt
                    double rsw = Math.Max(small.Rsw, 1);
                    double perQ = RcSection.Shear(V, b, h0t, conc, small, sd).QswReq / (2 * rsw);
                    double tsw = Math.Max(0, 2 * tr.Ak * rsw * (Dt.Area(sd) / ss - perQ));
                    r.Checks.Add(Dt.Check("Xoắn lanh tô – cốt đai kín " + Dt.Spaced(sd, ss), Torsion, tsw, "kN.m"));
                    if (!string.IsNullOrEmpty(tr.Note)) r.Messages.Add("Xoắn lanh tô: " + tr.Note);
                }
                else
                {
                    double gChar = 25 * b * hl + (GammaWall * Bw + 2 * PlasterT * GammaPlaster) * Math.Max(Hm, 0);
                    double mr = 0.9 * gChar * b / 2;
                    OverturnK = MRoot > 1e-9 ? mr / MRoot : 99;
                    r.Calc.Add("Chống lật (lanh tô chỉ gác lên tường): Mgiữ = 0,9·(25·b·h + gtường·Hm)·b/2 = " + Dt.F(mr, "0.000") + " kN.m/m; Mlật = M ngàm ô văng = "
                               + Dt.F(MRoot, "0.000") + " kN.m/m → K = " + Dt.F(OverturnK, "0.00") + " (yêu cầu ≥ " + Dt.F(OverturnLimit, "0.0") + ")");
                    r.Checks.Add(Dt.Check("Chống lật ô văng: 1,3·Mlật ≤ Mgiữ", OverturnLimit * MRoot, mr, "kN.m/m"));
                    if (OverturnK < OverturnLimit)
                        r.Messages.Add("LỖI ô văng có nguy cơ lật (K = " + Dt.F(OverturnK, "0.00") + " < 1,3) – cần neo ô văng vào dầm/sàn hoặc tăng chiều dài gối / đổ liền với giằng tường");
                }
            }

            double h0 = hl - c - 0.006;
            var fl = RcSection.DesignRect(M, b, h0, c, conc, main, 0.1);
            AsLintel = fl.As + asTorsion / 2;
            int bd = BotD, bn = BotN;
            if (bd <= 0 || bn <= 0)
            {
                foreach (int d in new[] { 10, 12, 14, 16 })
                {
                    if (BotD > 0 && d != BotD) continue;
                    int n = Math.Max(2, (int)Math.Ceiling(AsLintel / Dt.Area(d) - 1e-9));
                    if (BotN > 0) n = BotN;
                    if (Dt.FitsInLayer(b, c, sd, n, d, false) || d == 16) { bd = d; bn = n; break; }
                }
                if (bd <= 0) bd = BotD > 0 ? BotD : 10;
                if (bn <= 0) bn = Math.Max(2, BotN);
            }
            int td = TopD > 0 ? TopD : 10;
            int tn = TopN > 0 ? TopN : Math.Max(2, (int)Math.Ceiling(asTorsion / 2 / Dt.Area(td) - 1e-9));
            h0 = hl - c - bd / 2000.0;
            double asBot = bn * Dt.Area(bd), asTop = tn * Dt.Area(td);
            double mu = RcSection.MomentCapacity(b, h0, Math.Max(asBot - asTorsion / 2, 1e-9), asTop, c + td / 2000.0, conc, main);
            double qu = RcSection.ShearCapacity(b, h0, conc, St(sd), sd, 2, ss);
            r.Calc.Add("Thép dưới " + Dt.Bars(bn, bd) + " (As = " + Dt.F(asBot * 1e4, "0.00") + " cm², yêu cầu " + Dt.F(AsLintel * 1e4, "0.00") + " cm²), h0 = " + Dt.Mm(h0) + " mm → Mu = "
                       + Dt.F(mu, "0.000") + " kN.m; đai " + Dt.Spaced(sd, ss) + " → Qu = " + Dt.F(qu, "0.00") + " kN");
            r.Checks.Add(Dt.Check("Lanh tô chịu uốn M ≤ Mu (" + Dt.Bars(bn, bd) + ")", M, mu, "kN.m"));
            r.Checks.Add(Dt.Check("Lanh tô chịu cắt Q ≤ Qb + Qsw (đai " + Dt.Spaced(sd, ss) + ")", V, qu, "kN"));
            r.Checks.Add(Dt.Check("Chiều dài gối lên tường a ≥ 200 mm", 200, la * 1000, "mm"));
            r.Checks.Add(Dt.Check("Thép dưới lanh tô ≥ 2Ø10", 2 * Dt.Area(10) * 1e4, asBot * 1e4, "cm²"));
            if (!Dt.FitsInLayer(b, c, sd, bn, bd, false)) r.Messages.Add("Thép dưới lanh tô " + Dt.Bars(bn, bd) + " không đủ khe hở trên bề rộng " + Dt.Mm(b) + " mm – đặt 2 lớp hoặc tăng h");
            if (la < 0.20 - 1e-9) r.Messages.Add("Cảnh báo: gối lanh tô " + Dt.Mm(la) + " mm < 200 mm – không đảm bảo gối tựa lên khối xây");
            if (thin) Wall110(r, hl, la);

            double len = Dt.R10(ltot - 2 * c);
            var bot = Dt.Straight("Lanh tô dưới", bd, bn, len);
            var top = Dt.Straight("Lanh tô trên", td, tn, len, true);
            double cs = Math.Max(c - sd / 1000.0, 0.012);
            var stir = Dt.Stirrup("Đai lanh tô", sd, (int)Math.Floor((ltot - 0.10) / ss + 1e-6) + 1, b - 2 * cs, hl - 2 * cs, ss);
            r.Bars.InsertRange(0, new[] { bot, top, stir });
        }
        else if (canopy)
        {
            Torsion = MRoot * CanopyLen / 2;
            r.Messages.Add("Ô văng console từ dầm / lanh tô có sẵn: dầm đỡ chịu thêm xoắn mt = " + Dt.F(MRoot, "0.000") + " kN.m/m (T gối = " + Dt.F(Torsion, "0.000")
                           + " kN.m) – kiểm tra dầm đỡ với xoắn này; thép ô văng neo qua hết dầm và bẻ xuống");
        }

        foreach (var k in r.Checks.Where(x => !x.Ok)) r.Messages.Add("LỖI " + k.Name + ": " + Dt.F(k.Demand, "0.000") + " > " + Dt.F(k.Capacity, "0.000") + " " + k.Unit);
        RebarNumbering.Renumber(r.Bars);

        // ---------------- khối lượng ----------------
        double vol = 0, fw = 0;
        if (lintel)
        {
            vol += b * HlUsed * ltot;
            fw += b * Lo + 2 * HlUsed * ltot - (canopy ? Tr * CanopyLen : 0);
        }
        if (canopy)
        {
            vol += (Tr + Tt) / 2 * Lc * CanopyLen + (Upstand ? UpT * UpH * CanopyLen : 0);
            fw += Lc * CanopyLen + Tt * CanopyLen + (Upstand ? 2 * UpH * CanopyLen : 0);
            r.Extra.Add(("Trát / chống thấm mặt ô văng", "m²", Lc * CanopyLen + (Upstand ? UpH * CanopyLen : 0)));
        }
        r.ConcreteVolume = vol;
        r.FormworkArea = Math.Max(fw, 0);
    }

    // ==================================================================
    // Bản vẽ
    // ==================================================================
    private static BarMark Find(DetailResult r, string role) => r.Bars.FirstOrDefault(b => b.Role == role);

    /// <summary>Đặt nhãn thép không chồng: tìm chỗ trống theo bước stepY (đơn vị bản vẽ) rồi vẽ LeaderTag.</summary>
    private static void Tag(DxfWriter w, TagLayout lay, double px, double py, double kx, double ky, string text, int mark, int dir, double stepY, bool dot = true)
    {
        var box = w.TagBox(kx, ky, text, mark, dir);
        var (dx, dy) = lay.Place(box, 0, stepY, 40);
        w.LeaderTag(px, py, kx + dx, ky + dy, text, mark, dir, false, dot);
    }

    public List<Figure> Draw(DetailResult r, DetailSettings s)
    {
        var figs = new List<Figure>();
        if (r == null || r.Bars.Count == 0) return figs;
        s ??= new DetailSettings();
        if (HasLintel && HlUsed > 0) figs.Add(new Figure(Elevation(r, s), "Lanh tô " + Name, true));
        figs.Add(new Figure(Section(r, s), "Mặt cắt " + Name));
        return figs;
    }

    /// <summary>Mặt đứng lanh tô 1/25: ô cửa, khối xây hai bên (nét gạch chéo), tường phía trên (nét đứt), thép trên / dưới, đai, kích thước.</summary>
    private DxfWriter Elevation(DetailResult r, DetailSettings s)
    {
        double S = 25;
        var w = new DxfWriter(S);
        double th = w.Th;
        double lo = Lo * 1000, la = LaUsed * 1000, hl = HlUsed * 1000, c = Math.Max(Cover, 0.015) * 1000;
        double pier = Math.Max(la + 250, 450), wallUp = Math.Max(Math.Min(Hm * 1000, 600), 250), below = 700;
        var lay = new TagLayout(0.4 * th);
        // khối xây hai bên + phía trên
        foreach (int sg in new[] { -1, 1 })
        {
            double x0 = sg < 0 ? -pier : lo, x1 = sg < 0 ? 0 : lo + pier;
            w.Polyline(Lay.BeTong, new List<(double, double)> { (x0, -below), (x0, hl + wallUp), (x1, hl + wallUp), (x1, -below) }, false, 0, "DASHED");
            w.Line(Lay.BeTong, sg < 0 ? 0 : lo, -below, sg < 0 ? 0 : lo, 0);
            double dyH = 0.25 * (x1 - x0);
            for (double y = -below + 40; y + dyH < -20; y += 120)   // nét gạch chéo khối xây
                w.Line(Lay.KyHieu, x0, y, x1, y + dyH);
            w.BreakH(Lay.BeTong, x0 - 60, x1 + 60, -below);
        }
        w.Line(Lay.BeTong, -pier, hl + wallUp, lo + pier, hl + wallUp, "DASHED");
        w.Text(Lay.Chu, lo / 2, hl + wallUp / 2, th, "TƯỜNG XÂY", 0, 1, 2);
        w.Text(Lay.Chu, lo / 2, -below / 2, th, "Ô CỬA " + Dt.Mm(Lo), 0, 1, 2);
        // lanh tô
        w.Rect(Lay.BeTong, -la, 0, lo + la, hl);
        // vùng chiếm chỗ: tường hai bên, lanh tô, tường trên, chữ "Ô CỬA", dải kích thước dưới (nhãn đặt trong ô cửa, không đè kích thước)
        lay.Reserve(new Rect2(-pier, -below, 0, hl + wallUp));
        lay.Reserve(new Rect2(lo, -below, lo + pier, hl + wallUp));
        lay.Reserve(new Rect2(-la, 0, lo + la, hl + wallUp));
        lay.Reserve(new Rect2(lo / 2 - 5 * th, -below / 2 - 0.8 * th, lo / 2 + 5 * th, -below / 2 + 0.8 * th));
        lay.Reserve(new Rect2(-la - th, -below - 7 * th, lo + la + th, -below + 1.5 * th));
        var bot = Find(r, "Lanh tô dưới"); var top = Find(r, "Lanh tô trên"); var st = Find(r, "Đai lanh tô");
        double yb = c + (bot?.Dia ?? 10) / 2.0, yt = hl - c - (top?.Dia ?? 10) / 2.0;
        w.Line(Lay.Thep, -la + c, yb, lo + la - c, yb);
        w.Line(Lay.Thep, -la + c, yt, lo + la - c, yt);
        if (st != null)
        {
            double sp = Math.Max(st.Spacing * 1000, 50);
            for (double x = -la + 50; x <= lo + la - 50 + 1e-6; x += sp) w.Line(Lay.Dai, x, yb, x, yt);
        }
        // nhãn: dưới – trong ô cửa, trên – trên tường, đai – trong ô cửa (chồng tầng tránh nhau)
        if (bot != null) Tag(w, lay, lo * 0.30, yb, lo * 0.30 + 2 * th, -3.0 * th, Dt.Bars(bot.Count, bot.Dia), bot.Mark, 1, -2.2 * th);
        if (st != null)
        {
            double xs = -la + 50 + Math.Round((lo * 0.62 + la - 50) / Math.Max(st.Spacing * 1000, 50)) * Math.Max(st.Spacing * 1000, 50);
            Tag(w, lay, xs, hl / 2, xs + 2 * th, -3.0 * th, Dt.Spaced(st.Dia, st.Spacing), st.Mark, 1, -2.2 * th);
        }
        if (top != null) Tag(w, lay, lo * 0.40, yt, lo * 0.40 + 2 * th, hl + wallUp + 2.5 * th, Dt.Bars(top.Count, top.Dia), top.Mark, 1, 2.2 * th);
        // kích thước
        double yd = -below - 3 * th;
        w.DimChainH(new List<double> { -la, 0, lo, lo + la }, -below, yd);
        w.Dim(-la, -below, lo + la, -below, yd - 2.5 * th - (-below));
        w.Dim(lo + la, hl, lo + la, 0, 3 * th);
        w.Title(lo / 2, yd - 5 * th, RebarSchedule.MemberLabel("LANH TÔ " + Name, r.Quantity), DrawUtil.ScaleText(S));
        return w;
    }

    /// <summary>Mặt cắt 1-1 1/10: lanh tô + ô văng (đáy dốc từ hr tới ht), gờ chắn nước, rãnh nhỏ giọt, thép ô văng neo bẻ xuống lanh tô, thép phân bố, thép lanh tô + đai.</summary>
    private DxfWriter Section(DetailResult r, DetailSettings s)
    {
        double S = 10;
        var w = new DxfWriter(S);
        double th = w.Th;
        bool lintel = HasLintel && HlUsed > 0, canopy = HasCanopy;
        double c = Math.Max(Cover, 0.015) * 1000;
        double b = lintel ? Bw * 1000 : Math.Max(HostWidth, 0.1) * 1000;
        double h = lintel ? HlUsed * 1000 : Math.Max(300, Tr * 1000 + 150);
        double lc = Lc * 1000, tr = Tr * 1000, tt = Tt * 1000, upH = UpH * 1000, upT = UpT * 1000;
        var lay = new TagLayout(0.4 * th);
        // lanh tô / dầm đỡ (ô văng nhô ra phía âm x)
        w.Rect(Lay.BeTong, 0, 0, b, h, lintel ? null : "DASHED");
        double wallH = 300;
        w.Polyline(Lay.BeTong, new List<(double, double)> { (0, h), (0, h + wallH) }, false, 0, "DASHED");
        w.Polyline(Lay.BeTong, new List<(double, double)> { (b, h), (b, h + wallH) }, false, 0, "DASHED");
        w.BreakH(Lay.BeTong, -60, b + 60, h + wallH);
        if (!lintel) w.Text(Lay.Chu, b / 2, h / 2, th, "DẦM ĐỠ", 90, 1, 2);
        lay.Reserve(new Rect2(0, 0, b, h + wallH));
        if (canopy)
        {
            w.Polyline(Lay.BeTong, new List<(double, double)> { (0, h), (-lc, h), (-lc, h - tt), (0, h - tr) }, false);
            if (Upstand && upH > 0 && upT > 0) w.Rect(Lay.BeTong, -lc, h, -lc + upT, h + upH);
            lay.Reserve(new Rect2(-lc, h - tr, 0, h + (Upstand ? upH : 0)));
            lay.Reserve(new Rect2(-lc - th, h + (Upstand ? upH : 0) + 0.5 * th, th, h + (Upstand ? upH : 0) + 3.5 * th));   // kích thước chiều vươn
            // rãnh nhỏ giọt mặt dưới gần mút
            double xg = -lc + Math.Max(30, 0.08 * lc), yg = h - tt - (tr - tt) * (lc + xg) / lc;
            w.Polyline(Lay.BeTong, new List<(double, double)> { (xg - 10, yg), (xg - 10, yg + 10), (xg + 10, yg + 10), (xg + 10, yg) }, false);
            w.LeaderTag(xg, yg + 5, xg - 3 * th, yg - 4 * th, "Rãnh nhỏ giọt 20×10", 0, -1);
            w.Text(Lay.Chu, -lc / 2, h + 1.2 * th, 0.9 * th, "Dốc i = 1–2 % →", 0, 1, 0);
            // thép ô văng chịu lực
            var mb = Find(r, "Ô văng chịu lực");
            if (mb != null)
            {
                double ytop = h - c - mb.Dia / 2.0;
                double legIn = mb.Segments.Count >= 3 ? mb.Segments[^1] : 0, legTip = mb.Segments.Count >= 3 ? mb.Segments[0] : Math.Max(tt - 2 * c, 30);
                w.Polyline(Lay.Thep, new List<(double, double)> { (-lc + c, ytop - legTip), (-lc + c, ytop), (b - c, ytop), (b - c, ytop - legIn) }, false);
                Tag(w, lay, -lc * 0.45, ytop, -lc * 0.45 - 2 * th, h + (Upstand ? upH : 0) + 3 * th, Dt.Spaced(mb.Dia, mb.Spacing), mb.Mark, -1, 2.2 * th);
            }
            var db = Find(r, "Ô văng phân bố");
            if (db != null)
            {
                double y = h - c - (mb?.Dia ?? 8) - db.Dia / 2.0;
                int n = Math.Max(2, Math.Min(db.Count, 12));
                double x0 = -lc + c + 20, x1 = -40;
                var xsd = new List<double>();
                for (int i = 0; i < n; i++) { double x = x0 + (x1 - x0) * i / Math.Max(n - 1, 1); xsd.Add(x); w.Dot(Lay.Thep, x, y, db.Dia / 2.0); }
                w.Line(Lay.KyHieu, xsd[0], y, xsd[^1], y);
                Tag(w, lay, xsd[n / 2], y, xsd[n / 2] - 2 * th, h - tr - 3.5 * th, Dt.Spaced(db.Dia, db.Spacing), db.Mark, -1, -2.2 * th, false);
            }
            var ub = Find(r, "Gờ ô văng");
            if (ub != null && Upstand)
            {
                double xu = -lc + upT / 2;
                double leg = ub.Segments.Count >= 2 ? ub.Segments[^1] : 150;
                w.Polyline(Lay.Thep, new List<(double, double)> { (xu, h + upH - c), (xu, h - tt + c), (xu + leg, h - tt + c) }, false);
                Tag(w, lay, xu, h + upH * 0.6, xu - 3 * th, h + upH + 1.5 * th, Dt.Spaced(ub.Dia, ub.Spacing), ub.Mark, -1, 2.2 * th);
            }
            w.Dim(-lc, h + (Upstand ? upH : 0), 0, h + (Upstand ? upH : 0), 2 * th);
            w.Dim(-lc, h, -lc, h - tt, -2.5 * th);
            w.Text(Lay.Chu, -0.6 * th, h - tr - 1.4 * th, 0.8 * th, "hr = " + Dt.Mm(Tr), 0, 2, 0);
        }
        if (lintel)
        {
            var bot = Find(r, "Lanh tô dưới"); var top = Find(r, "Lanh tô trên"); var st = Find(r, "Đai lanh tô");
            double cs = c - (st?.Dia ?? 6);
            if (st != null) w.Rect(Lay.Dai, cs + st.Dia / 2.0, cs + st.Dia / 2.0, b - cs - st.Dia / 2.0, h - cs - st.Dia / 2.0);
            void Row(BarMark bm, double y, bool up)
            {
                if (bm == null) return;
                int n = Math.Max(bm.Count, 2);
                double x0 = c + bm.Dia / 2.0, x1 = b - c - bm.Dia / 2.0;
                for (int i = 0; i < n; i++) w.Dot(Lay.Thep, x0 + (x1 - x0) * i / (n - 1), y, bm.Dia / 2.0);
                w.Line(Lay.KyHieu, x0, y, x1, y);
                Tag(w, lay, (x0 + x1) / 2, y, b + 2.5 * th, up ? h + wallH + 1.5 * th : -2.5 * th, Dt.Bars(bm.Count, bm.Dia), bm.Mark, 1, up ? 2.2 * th : -2.2 * th, false);
            }
            Row(top, h - c - (top?.Dia ?? 10) / 2.0, true);
            Row(bot, c + (bot?.Dia ?? 10) / 2.0, false);
            if (st != null) Tag(w, lay, b - cs - st.Dia / 2.0, h * 0.5, b + 2.5 * th, h * 0.5, Dt.Spaced(st.Dia, st.Spacing), st.Mark, 1, -2.2 * th);
            w.Dim(0, 0, b, 0, -6 * th);
            w.Dim(b, h, b, 0, 1.2 * th);
        }
        w.Title(b / 2 - (canopy ? lc / 2 : 0), -9 * th, "1-1", DrawUtil.ScaleText(S), true);
        return w;
    }
}

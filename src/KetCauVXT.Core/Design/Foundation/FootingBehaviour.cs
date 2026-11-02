using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

/// <summary>Tính chất và sự làm việc của móng nông (móng đơn, móng băng) – kiểm tra bổ sung, phân loại và giải thích:
/// <list type="bullet">
/// <item>Móng đơn: tiêu chí móng cứng c ≤ 2,5H (c = độ vươn bản móng ngoài mép cột) để giả thiết áp lực đáy móng phân bố tuyến tính;
/// lệch tâm trong lõi tiết diện eL/L + eB/B ≤ 1/6 (không tách nền), ngược lại tiếp xúc một phần: pmax = 2N/(3·B·c0), c0 = L/2 − e, vùng tiếp xúc 3c0 ≥ 0,75L;
/// trượt H ≤ [(N + 0,9G)·tgφ + c·A]/1,2; chọc thủng có kể mô men (TCVN 5574:2018 mục 8.1.6.2: F/Fb,u + Σ 0,5M/Mb,u ≤ 1, Mb,u = Rbt·Wb·h0,
/// Wb = Lx²/3 + Lx·Ly, phần mô men ≤ F/Fb,u); cắt trên tiết diện nghiêng của bản móng (8.1.3.3).</item>
/// <item>Móng băng: dầm trên nền Winkler, λ = (k·B/4EI)^¼; λL ≤ π/4 → dầm cứng (áp lực gần tuyến tính), π/4 &lt; λL &lt; π → dầm hữu hạn,
/// λL ≥ π → dầm dài / mềm (áp lực tập trung dưới chân cột, lan trong phạm vi ≈ π/λ); lún Winkler s = p/k, lún lệch tương đối Δs/l ≤ 0,002 (TCVN 9362:2012 Bảng 16, khung BTCT).</item>
/// <item>Lời giải dầm vô hạn trên nền đàn hồi chịu lực tập trung P (kiểm chứng): M0 = P/(4λ), w0 = P·λ/(2·k·B).</item>
/// </list></summary>
public static class FootingBehaviour
{
    /// <summary>Tiêu chí móng cứng: độ vươn c ≤ RigidRatio·H</summary>
    public const double RigidRatio = 2.5;
    /// <summary>Lún lệch tương đối giới hạn (khung BTCT, TCVN 9362:2012 Bảng 16)</summary>
    public const double RelativeSettlementLimit = 0.002;
    /// <summary>Hệ số an toàn trượt</summary>
    public const double SlidingFs = 1.2;

    /// <summary>λ = (k·B/(4·EI))^¼ (1/m): k hệ số nền (kN/m³), B bề rộng đáy (m), EI (kN.m²).</summary>
    public static double Lambda(double k, double B, double EI) => EI > 0 && k > 0 && B > 0 ? Math.Pow(k * B / (4 * EI), 0.25) : 0;

    /// <summary>Mô men dưới lực tập trung của dầm vô hạn trên nền đàn hồi M0 = P/(4λ).</summary>
    public static double InfiniteBeamMoment(double P, double lambda) => lambda > 0 ? P / (4 * lambda) : 0;

    /// <summary>Độ lún dưới lực tập trung của dầm vô hạn trên nền đàn hồi w0 = P·λ/(2·k·B).</summary>
    public static double InfiniteBeamDeflection(double P, double lambda, double k, double B) => k > 0 && B > 0 ? P * lambda / (2 * k * B) : 0;

    /// <summary>Phân loại dầm trên nền đàn hồi theo λL: 0 = cứng (≤ π/4), 1 = hữu hạn, 2 = dài / mềm (≥ π).</summary>
    public static int StripClass(double lambdaL) => lambdaL <= Math.PI / 4 + 1e-12 ? 0 : lambdaL < Math.PI - 1e-12 ? 1 : 2;

    public static string StripClassName(int c) => c switch { 0 => "Dầm cứng (ngắn)", 1 => "Dầm hữu hạn (trung gian)", _ => "Dầm dài / mềm" };

    /// <summary>Áp lực biên khi móng tách nền một phần (lệch tâm e &gt; L/6): pmax = 2N/(3·B·(L/2 − e)); e ≥ L/2 → vô cùng.</summary>
    public static double PartialContactPmax(double N, double e, double L, double B)
    {
        double c0 = L / 2 - Math.Abs(e);
        return c0 > 1e-9 && B > 0 ? 2 * N / (3 * B * c0) : double.PositiveInfinity;
    }

    // ------------------------------------------------------------------
    // MÓNG ĐƠN
    // ------------------------------------------------------------------
    /// <summary>Phân tích sự làm việc móng đơn đã thiết kế (gọi cuối FootingDesigner.DesignOne) → d.Safety; thêm "Cảnh báo: …"/"LỖI …" vào Messages.
    /// Không thêm CheckItem (giữ nguyên danh sách Checks).</summary>
    public static FoundationCheckReport AnalyzeIsolated(FootingDesign d, SoilSettings soil, Material conc, double cover)
    {
        var rep = new FoundationCheckReport { Kind = "Móng đơn" };
        if (d == null || d.L <= 0 || d.B <= 0) return rep;
        soil ??= new SoilSettings();
        double L = d.L, B = d.B, H = Math.Max(d.H, 0.1), hc = d.ColumnH > 0 ? d.ColumnH : 0.3, bc = d.ColumnB > 0 ? d.ColumnB : 0.3;
        double Df = Math.Max(soil.Depth, 0), gtb = soil.GammaAvg, A = L * B;
        double kt = d.Ntc > 1e-9 ? Math.Max(d.N / d.Ntc, 1.0) : Math.Max(soil.LoadFactorAvg, 1.0);
        double rbt = conc?.Rbt ?? 1050;

        // ---- móng cứng ----
        double cMax = Math.Max((L - hc) / 2, (B - bc) / 2);
        bool rigid = cMax <= RigidRatio * H + 1e-9;
        rep.Items.Add(SafetyItem.Make("Sự làm việc", "Móng cứng: độ vươn bản móng c ≤ 2,5H", cMax * 1000, RigidRatio * H * 1000, "mm",
            $"c = max((L − hc)/2; (B − bc)/2) = {cMax * 1000:0} mm; H = {H * 1000:0} mm; c/H = {cMax / H:0.00}",
            "Móng đủ cứng: áp lực đáy móng phân bố tuyến tính (p = N/A ± M/W) đúng với giả thiết tính toán.",
            "Móng mềm (bản móng vươn xa so với chiều cao): áp lực tập trung dưới cột, giả thiết phân bố tuyến tính thiên về không an toàn khi tính uốn – nên tăng chiều cao móng.",
            false, CheckStatus.Warning));

        // ---- lệch tâm (tiêu chuẩn) ----
        double ntot = d.Ntc + gtb * Df * A;
        double mL = (Math.Abs(d.My) + Math.Abs(d.Qx) * Df) / kt, mB = (Math.Abs(d.Mx) + Math.Abs(d.Qy) * Df) / kt;
        double eL = ntot > 1e-9 ? mL / ntot : 0, eB = ntot > 1e-9 ? mB / ntot : 0;
        double kern = eL / L + eB / B;
        rep.Items.Add(SafetyItem.Make("Nền", "Lệch tâm trong lõi: eL/L + eB/B ≤ 1/6", kern, 1.0 / 6, "",
            $"eL = M_L/N = {eL * 1000:0} mm, eB = {eB * 1000:0} mm (tổ hợp {d.Combo}, tiêu chuẩn, kể G = γtb·Df·A)",
            "Hợp lực nằm trong lõi tiết diện đáy móng: toàn bộ đáy móng tiếp xúc với nền (pmin ≥ 0).",
            "Hợp lực ra ngoài lõi: một phần đáy móng tách khỏi nền, áp lực phân bố tam giác trên vùng tiếp xúc – kiểm tra pmax theo công thức tiếp xúc một phần.",
            false, CheckStatus.Warning));
        if (kern > 1.0 / 6 + 1e-9)
        {
            bool alongL = eL / L >= eB / B;
            double e = alongL ? eL : eB, len = alongL ? L : B, wid = alongL ? B : L;
            double pm = PartialContactPmax(ntot, e, len, wid);
            double contact = Math.Max(3 * (len / 2 - e), 0) / len;
            var it1 = SafetyItem.Make("Nền", "Tách nền một phần: pmax = 2N/(3B·c0) ≤ 1,2R", pm, 1.2 * d.R, "kN/m²",
                $"c0 = {(alongL ? "L" : "B")}/2 − e = {(len / 2 - e) * 1000:0} mm; pmax = 2·{ntot:0}/(3·{wid:0.00}·{len / 2 - e:0.000})",
                "Áp lực biên vùng tiếp xúc vẫn nhỏ hơn 1,2R.", "Áp lực biên vùng tiếp xúc vượt 1,2R – tăng kích thước đáy móng theo phương lệch tâm.");
            var it2 = SafetyItem.Make("Nền", "Vùng tiếp xúc 3c0 ≥ 75% cạnh móng", contact * 100, 75, "%",
                "3c0/L ≥ 0,75 (giới hạn tách nền thực hành cho móng chịu tải lệch tâm)",
                "Vùng tách nền nhỏ, chấp nhận được với tổ hợp có tải gió / động đất.",
                "Vùng tách nền quá lớn – móng có nguy cơ lật / xoay: mở rộng móng theo phương lệch tâm hoặc giằng móng tiếp nhận mô men.", true);
            rep.Items.Add(it1); rep.Items.Add(it2);
            rep.Warnings.Add($"Cảnh báo: móng tách nền một phần (e = {e * 1000:0} mm > {(alongL ? "L" : "B")}/6 = {len / 6 * 1000:0} mm), pmax = {pm:0} kN/m², vùng tiếp xúc {contact * 100:0}%.");
        }

        // ---- trượt ----
        double hd = Math.Sqrt(d.Qx * d.Qx + d.Qy * d.Qy);
        if (hd > 1e-3)
        {
            double tg = Math.Tan(Math.Max(soil.Phi, 0) * Math.PI / 180);
            double res = (Math.Max(d.N, 0) + 0.9 * gtb * Df * A) * tg + Math.Max(soil.C, 0) * A;
            rep.Items.Add(SafetyItem.Make("Nền", "Trượt theo đáy móng H ≤ [(N + 0,9G)·tgφ + c·A]/1,2", hd, res / SlidingFs, "kN",
                $"H = √(Qx² + Qy²); φ = {soil.Phi:0.#}°, c = {soil.C:0.#} kPa; FS = {SlidingFs:0.0}",
                "Ma sát và lực dính dưới đáy móng đủ chống trượt.",
                "Nguy cơ trượt đáy móng – bố trí giằng móng tiếp nhận lực ngang, tăng độ sâu chôn móng.", false, CheckStatus.Warning));
        }

        // ---- chọc thủng có kể mô men ----
        double h0 = H - cover - Math.Max(d.DiaL, 10) / 1000.0;
        if (h0 > 0.05 && d.N > 0)
        {
            double pnet = d.N / A;
            double apz = Math.Min((hc + 2 * h0) * (bc + 2 * h0), A);
            double F = Math.Max(d.N - pnet * apz, 0);
            double lx = hc + h0, ly = bc + h0, u = 2 * (lx + ly);
            double fbu = rbt * u * h0;
            double mbx = rbt * (lx * lx / 3 + lx * ly) * h0, mby = rbt * (ly * ly / 3 + lx * ly) * h0;
            double fr = fbu > 0 ? F / fbu : 99;
            double mr = (mbx > 0 ? 0.5 * Math.Abs(d.My) / mbx : 0) + (mby > 0 ? 0.5 * Math.Abs(d.Mx) / mby : 0);
            mr = Math.Min(mr, fr);
            rep.Items.Add(SafetyItem.Make("Chọc thủng", "Chọc thủng có kể mô men F/Fb,u + ΣM/Mb,u ≤ 1", fr + mr, 1, "",
                $"F = N − pnet·(hc + 2h0)(bc + 2h0) = {F:0} kN, Fb,u = Rbt·u·h0 = {fbu:0} kN; M = 0,5·M chân cột, Mb,u = Rbt·Wb·h0 = {mbx:0} / {mby:0} kN.m (TCVN 5574:2018 mục 8.1.6.2)",
                F <= 1e-6 ? "Toàn bộ đáy móng nằm trong tháp chọc thủng 45° – móng không bị chọc thủng (móng cứng, làm việc như khối nén)."
                          : "Bản móng đủ chống chọc thủng của cột kể cả ảnh hưởng mô men chân cột.",
                "Chọc thủng có kể mô men vượt khả năng chịu lực – nên tăng chiều cao móng (h0).", false, CheckStatus.Warning));
        }

        // ---- cắt trên tiết diện nghiêng bản móng ----
        void Shear(bool alongL)
        {
            double len = alongL ? L : B, wid = alongL ? B : L, col = alongL ? hc : bc;
            double cl = (len - col) / 2;
            double hh = alongL ? h0 : h0 - Math.Max(d.DiaB, 10) / 1000.0;
            if (cl <= 1e-6 || hh <= 0.05 || d.N <= 0) return;
            double m = alongL ? Math.Abs(d.My) + Math.Abs(d.Qx) * H : Math.Abs(d.Mx) + Math.Abs(d.Qy) * H;
            double pe = d.N / A + 6 * m / (wid * len * len);
            double worst = -1, wq = 0, wqb = 0, wc = 0;
            int steps = 24;
            for (int i = 1; i <= steps; i++)
            {
                double c = Math.Min(cl, 3 * hh) * i / steps;
                double q = pe * wid * Math.Max(cl - c, 0);
                double qb = PileCapChecks.ShearCapacity(wid, hh, c, rbt);
                double r = qb > 0 ? q / qb : 99;
                if (r > worst) { worst = r; wq = q; wqb = qb; wc = c; }
            }
            rep.Items.Add(SafetyItem.Make("Cắt", "Cắt tiết diện nghiêng bản móng phương " + (alongL ? "L" : "B"), wq, wqb, "kN",
                $"Q = pmax,tt·b·(c_vươn − c) ≤ Qb = 1,5·Rbt·b·h0²/c; c bất lợi = {wc * 1000:0} mm (TCVN 5574:2018 mục 8.1.3.3)",
                "Bản móng đủ chịu cắt không cần cốt đai.", "Bản móng không đủ chịu cắt – tăng chiều cao móng.", false, CheckStatus.Warning));
        }
        Shear(true); Shear(false);

        // ---- hạng mục đã có ----
        foreach (var c in d.Checks)
        {
            string expl = c.Name.StartsWith("Áp lực") ? "Áp lực đáy móng tiêu chuẩn so với sức chịu tải nền R (TCVN 9362:2012) – điều kiện biến dạng tuyến tính của nền."
                        : c.Name.StartsWith("Chọc thủng") ? "Tháp chọc thủng 45° từ mép cột; phản lực nền trong đáy tháp không gây chọc thủng."
                        : c.Name.StartsWith("Uốn") ? "Bản móng làm việc như công xôn ngàm tại mép cột chịu phản lực nền ròng – thép đáy chịu kéo."
                        : c.Name.StartsWith("Độ lún") ? "Lún tính theo phương pháp cộng lún từng lớp với hố khoan." : "";
            rep.Items.Add(new SafetyItem
            {
                Group = c.Name.StartsWith("Uốn") ? "Uốn" : c.Name.StartsWith("Chọc") ? "Chọc thủng" : "Nền", Name = c.Name, Value = c.Demand, Limit = c.Capacity, Unit = c.Unit,
                Status = c.Ok ? CheckStatus.Ok : CheckStatus.Fail, Formula = c.Note, Explanation = expl
            });
        }

        rep.Classification = rigid ? "Móng cứng" : "Móng mềm (bản vươn dài)";
        rep.Behaviour = $"Móng đơn BTCT {L:0.00}×{B:0.00}×{H:0.00} m dưới cột {hc * 1000:0}×{bc * 1000:0}: "
                        + (rigid ? $"móng cứng (c/H = {cMax / H:0.00} ≤ 2,5), " : $"móng tương đối mềm (c/H = {cMax / H:0.00} > 2,5), ")
                        + "áp lực đáy móng coi phân bố tuyến tính theo N và mô men (kể lực cắt × chiều sâu). "
                        + (kern <= 1.0 / 6 + 1e-9 ? "Hợp lực nằm trong lõi tiết diện nên toàn bộ đáy móng tiếp xúc với nền. " : "Hợp lực ra ngoài lõi – đáy móng tách nền một phần. ")
                        + "Bản móng làm việc như bản công xôn 4 phía ngàm tại mép cột: chiều cao chọn theo chọc thủng (tháp 45°) và cắt, "
                        + "thép đáy 2 phương theo mô men tại mép cột; lực ngang chân cột do ma sát đáy móng và hệ giằng móng tiếp nhận.";
        foreach (var it in rep.Items.Where(i => i.Status == CheckStatus.Warning && i.Group != "Nền")) rep.Warnings.Add("Cảnh báo: " + it.Name + " – " + it.Explanation);
        d.Safety = rep;
        foreach (var w in rep.Warnings.Where(w => !d.Messages.Contains(w))) d.Messages.Add(w);
        foreach (var it in rep.Items.Where(i => i.Status == CheckStatus.Fail && i.Name.StartsWith("Vùng tiếp xúc")))
            d.Messages.Add("LỖI " + it.Name + ": " + it.Explanation);
        return rep;
    }

    // ------------------------------------------------------------------
    // MÓNG BĂNG
    // ------------------------------------------------------------------
    /// <summary>Phân loại sự làm việc của dải móng băng (dầm trên nền Winkler) → d.Safety; thêm thông báo sự làm việc / cảnh báo vào Messages.</summary>
    public static FoundationCheckReport AnalyzeStrip(StripFootingDesign d, Material conc, FoundationSettings fs = null)
    {
        var rep = new FoundationCheckReport { Kind = "Móng băng" };
        if (d == null || d.Width <= 0 || d.Length <= 0) return rep;
        double hfAvg = (Math.Max(d.FlangeEdge, 0) + Math.Max(d.FlangeRoot, 0)) / 2;
        double I = StripFootingDesigner.InertiaT(d.Width, hfAvg > 0 ? hfAvg : d.RibH, d.RibB > 0 ? d.RibB : d.Width, d.RibH);
        double EI = (conc?.E ?? 3.0e7) * I;
        double lam = Lambda(d.K, d.Width, EI);
        double lamL = lam * d.Length;
        int cls = StripClass(lamL);
        rep.EI = EI; rep.Lambda = lam; rep.LambdaL = lamL;
        rep.Classification = StripClassName(cls);

        double maxSpan = 0;
        for (int i = 0; i + 1 < d.ColumnPositions.Count; i++) maxSpan = Math.Max(maxSpan, d.ColumnPositions[i + 1] - d.ColumnPositions[i]);
        string kSrc = fs != null && fs.SubgradeModulus > 0 ? "do người dùng khai báo" : "lấy gần đúng k ≈ 120·R";
        rep.Items.Add(new SafetyItem
        {
            Group = "Sự làm việc", Name = "Độ mềm dải λL (π/4 = 0,79; π = 3,14)", Value = lamL, Limit = Math.PI, Unit = "", Status = CheckStatus.Info,
            Formula = $"λ = (k·B/4EI)^¼ = ({d.K:0}·{d.Width:0.00}/(4·{EI:0}))^¼ = {lam:0.000} 1/m; L = {d.Length:0.00} m; k {kSrc}",
            Explanation = cls switch
            {
                0 => "λL ≤ π/4: dải làm việc như dầm cứng – lún gần như đều, áp lực nền phân bố gần tuyến tính; nội lực có thể tính như dầm cứng.",
                1 => "π/4 < λL < π: dầm hữu hạn – biến dạng uốn của dải ảnh hưởng rõ tới phân bố áp lực; phải tính theo dầm trên nền đàn hồi (đã áp dụng).",
                _ => "λL ≥ π: dầm dài / mềm – áp lực nền tập trung dưới chân cột, mô men âm dưới cột và dương giữa nhịp xen kẽ; bắt buộc tính theo dầm trên nền đàn hồi (đã áp dụng)."
            }
        });
        if (maxSpan > 0)
        {
            double ls = lam * maxSpan;
            rep.Items.Add(SafetyItem.Make("Sự làm việc", "Nhịp cột lớn nhất λ·l ≤ π (dải phân phối tải giữa các cột)", ls, Math.PI, "",
                $"l = {maxSpan:0.00} m, π/λ = {(lam > 0 ? Math.PI / lam : 0):0.00} m",
                "Các cột đủ gần nhau so với chiều dài đặc trưng 1/λ: dải phân phối lại tải giữa các cột, giảm lún lệch.",
                "Nhịp giữa các cột lớn hơn π/λ: mỗi cột gần như làm việc độc lập trên một đoạn dải – móng băng ít tác dụng san đều lún; cân nhắc tăng chiều cao sườn.",
                false, CheckStatus.Warning));
        }

        // ---- áp lực / lún Winkler ----
        if (d.Pressure.Count > 0 && d.K > 0)
        {
            double pavg = d.PAvg > 0 ? d.PAvg : d.Pressure.Average();
            double pmx = d.Pressure.Max(), pmn = d.Pressure.Min();
            rep.Items.Add(new SafetyItem
            {
                Group = "Nền", Name = "Độ không đều áp lực nền pmax/ptb", Value = pmx, Limit = pavg, Unit = "kN/m²", Status = CheckStatus.Info,
                Formula = $"pmax = {pmx:0.0}, pmin = {pmn:0.0}, ptb = {pavg:0.0} kN/m² (tổ hợp tiêu chuẩn {d.ComboService})",
                Explanation = pavg > 0 && pmx / pavg < 1.15 ? "Áp lực nền gần đều – dải làm việc như móng cứng." : "Áp lực nền tập trung dưới chân cột do dải có độ mềm đáng kể."
            });
            double sMax = pmx / d.K;
            rep.Items.Add(SafetyItem.Make("Nền", "Lún Winkler s = p/k ≤ 8 cm", sMax * 100, GeotechCalc.SettlementLimit * 100, "cm",
                "s = p/k (mô hình Winkler – chỉ là ước lượng, lún thực tính theo phương pháp cộng lún khi có hố khoan)",
                "Độ lún ước lượng theo hệ số nền nhỏ hơn giới hạn.", "Độ lún ước lượng theo hệ số nền lớn – kiểm tra lại hệ số nền / tính lún theo hố khoan.",
                false, CheckStatus.Warning));
            double worst = 0; string where = "";
            for (int i = 0; i + 1 < d.ColumnPositions.Count; i++)
            {
                double l = d.ColumnPositions[i + 1] - d.ColumnPositions[i];
                if (l <= 1e-6) continue;
                double w1 = Interp(d.Stations, d.Pressure, d.ColumnPositions[i]) / d.K, w2 = Interp(d.Stations, d.Pressure, d.ColumnPositions[i + 1]) / d.K;
                double r = Math.Abs(w1 - w2) / l;
                if (r > worst) { worst = r; where = $"cột {i + 1}–{i + 2}"; }
            }
            if (d.ColumnPositions.Count > 1)
                rep.Items.Add(SafetyItem.Make("Nền", "Lún lệch tương đối Δs/l ≤ 0,002", worst, RelativeSettlementLimit, "",
                    $"Δs/l lớn nhất tại {where} (Winkler, tổ hợp tiêu chuẩn); giới hạn khung BTCT TCVN 9362:2012 Bảng 16",
                    "Lún lệch giữa các cột nhỏ – dải đủ cứng để san đều lún.", "Lún lệch giữa các cột lớn – tăng chiều cao sườn (độ cứng EI) hoặc mở rộng đáy dưới cột tải lớn.",
                    false, CheckStatus.Warning));
        }

        // ---- hạng mục đã có ----
        foreach (var c in d.Checks)
        {
            string expl = c.Name.StartsWith("Áp lực") ? "Áp lực nền (tiêu chuẩn) từ lời giải dầm trên nền đàn hồi so với sức chịu tải nền R."
                        : c.Name.StartsWith("Uốn sườn") ? "Sườn là dầm liên tục trên nền đàn hồi nhận tải tập trung tại chân cột: mô men dương (căng dưới) giữa các cột hay dưới cột tuỳ độ mềm dải; thép theo bao TTGH I."
                        : c.Name.StartsWith("Cắt sườn") ? "Lực cắt lớn nhất trong sườn tại mép cột – cốt đai chịu."
                        : c.Name.Contains("bản cánh") ? "Bản cánh làm việc như bản công xôn ngàm vào sườn chịu áp lực nền ròng: thép ngang dưới bản cánh, không cốt đai."
                        : c.Name.StartsWith("Độ lún") ? "Lún tính theo phương pháp cộng lún từng lớp với hố khoan (móng chữ nhật L × B)." : "";
            rep.Items.Add(new SafetyItem
            {
                Group = c.Name.StartsWith("Uốn") ? "Uốn" : c.Name.StartsWith("Cắt") ? "Cắt" : "Nền", Name = c.Name, Value = c.Demand, Limit = c.Capacity, Unit = c.Unit,
                Status = c.Ok ? CheckStatus.Ok : CheckStatus.Fail, Formula = c.Note, Explanation = expl
            });
        }

        double cf = Math.Max((d.Width - d.RibB) / 2, 0);
        rep.Behaviour = $"Móng băng {d.Name} ({d.Grid}) làm việc như dầm trên nền đàn hồi Winkler (k = {d.K:0} kN/m³, {kSrc}): "
                        + $"EI = {EI:0} kN.m², λ = {lam:0.000} 1/m, chiều dài đặc trưng 1/λ = {(lam > 0 ? 1 / lam : 0):0.00} m, λL = {lamL:0.00} → {StripClassName(cls).ToLowerInvariant()}. "
                        + (cls == 0 ? "Dải rất cứng so với nền: lún gần đều, áp lực phân bố gần tuyến tính. "
                           : cls == 1 ? "Độ cứng dải và nền cùng quyết định phân bố áp lực: áp lực lớn hơn dưới chân cột. "
                           : "Dải mềm so với nền: áp lực tập trung dưới các chân cột, nội lực sườn phụ thuộc mạnh vào k. ")
                        + "Sườn chịu mô men, lực cắt theo phương dải (thép dọc trên/dưới, cốt đai); "
                        + (cf > 1e-6 ? $"bản cánh vươn {cf * 1000:0} mm mỗi bên làm việc như công xôn ngàm vào sườn (thép ngang đáy bản)." : "không có bản cánh (đáy bằng bề rộng sườn).");
        d.Safety = rep;
        string msg = $"Sự làm việc: dầm trên nền đàn hồi, λL = {lamL:0.00} – {StripClassName(cls).ToLowerInvariant()}";
        if (!d.Messages.Contains(msg)) d.Messages.Add(msg);
        foreach (var it in rep.Items.Where(i => i.Status == CheckStatus.Warning))
        {
            string w = "Cảnh báo: " + it.Name + " – " + it.Explanation;
            rep.Warnings.Add(w);
            if (!d.Messages.Contains(w)) d.Messages.Add(w);
        }
        return rep;
    }

    private static double Interp(List<double> xs, List<double> ys, double x)
    {
        if (xs == null || ys == null || xs.Count == 0 || ys.Count < xs.Count) return 0;
        if (x <= xs[0]) return ys[0];
        for (int i = 0; i + 1 < xs.Count; i++)
            if (x <= xs[i + 1] + 1e-12)
            {
                double t = xs[i + 1] - xs[i] > 1e-12 ? (x - xs[i]) / (xs[i + 1] - xs[i]) : 0;
                return ys[i] + t * (ys[i + 1] - ys[i]);
            }
        return ys[xs.Count - 1];
    }
}

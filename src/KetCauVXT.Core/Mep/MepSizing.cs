using KetCauVXT.Model;

namespace KetCauVXT.Mep;

/// <summary>Chọn cỡ ống / dây (bảng tiêu chuẩn ở Mep/MepStandards.cs, ghi đè được trong Cài đặt điện nước):
/// • Cấp nước: q = 0,2·a·√N + K·N (TCVN 4513, nhà ở: a theo tiêu chuẩn dùng nước – 125 l → 2,15; K theo N – N ≤ 300 → 0,002;
///   công trình công cộng q = 0,2·α·√N), ống PPR PN20 với v ≤ 1,5 m/s.
/// • Thoát nước: theo tổng đương lượng thoát DFU (TCVN 4474 giản lược); nhánh có bồn cầu ≥ Ø110.
/// • Dây điện: I = P/(U·cosφ), aptomat In ≥ I, dây Cu/PVC trong ống gen chôn tường / sàn (IEC 60364-5-52 Bảng B.52.4 cách lắp B1)
///   Iz·k1·k2 ≥ In (k1 nhiệt độ B.52.14, k2 nhóm mạch B.52.17); tối thiểu chiếu sáng 1,5 mm², ổ cắm / điều hoà 2,5 mm²;
///   sụt áp ≤ 3 % chiếu sáng / 5 % mạch khác (IEC 60364-5-52 Phụ lục G) – không đạt thì tăng tiết diện.
/// • Ống gen: tổng diện tích dây ≤ 40 % tiết diện trong ống.</summary>
public static class MepSizing
{
    public const double Alpha = 2.15, K = 0.002, VMax = 1.5;

    /// <summary>Đường kính danh nghĩa PPR (mm) → đường kính trong (mm), PN20.</summary>
    public static readonly (double Dn, double Di)[] Ppr = { (20, 13.2), (25, 16.6), (32, 21.2), (40, 26.6), (50, 33.4), (63, 42.0) };
    /// <summary>Ống thoát uPVC (mm).</summary>
    public static readonly double[] Upvc = { 34, 42, 49, 60, 76, 90, 110 };
    /// <summary>Tiết diện dây (mm²) → dòng cho phép Iz (A), Cu/PVC trong ống chôn tường / sàn, 2 dây mang tải (IEC 60364-5-52 Bảng B.52.4, B1).</summary>
    public static readonly (double S, double Iz)[] Wire = MepStandards.Sections.Select((x, i) => (x, MepStandards.IzB1[i])).ToArray();
    /// <summary>Đường kính ngoài dây CV (mm).</summary>
    public static readonly (double S, double Do)[] WireOd = { (1.5, 3.0), (2.5, 3.6), (4, 4.2), (6, 4.8), (10, 6.0), (16, 7.4), (25, 9.2), (35, 10.4) };
    /// <summary>Ống gen PVC (mm) → đường kính trong (mm).</summary>
    public static readonly (double Dn, double Di)[] Conduit = { (16, 13.0), (20, 16.5), (25, 21.3), (32, 27.6) };
    public static readonly double[] Breakers = { 6, 10, 16, 20, 25, 32, 40, 50, 63 };

    /// <summary>Lưu lượng cấp nước tính toán (l/s) theo tổng đương lượng N – nhà ở, tiêu chuẩn 125 l/người·ngđ (a = 2,15; K theo N).</summary>
    public static double SupplyFlow(double n) => MepStandards.SupplyFlow(n, null);

    /// <summary>Lưu lượng cấp nước tính toán (l/s) theo loại công trình / tiêu chuẩn dùng nước của dự án (TCVN 4513).</summary>
    public static double SupplyFlow(double n, MepStdOptions o) => MepStandards.SupplyFlow(n, o);

    /// <summary>Ống PPR nhỏ nhất có vận tốc ≤ vmax với lưu lượng q (l/s).</summary>
    public static double SupplyPipe(double q, double vmax = VMax)
    {
        foreach (var (dn, di) in Ppr)
        {
            double a = Math.PI * Math.Pow(di / 1000, 2) / 4;
            if (q / 1000 / a <= vmax + 1e-9) return dn;
        }
        return Ppr[^1].Dn;
    }

    /// <summary>Vận tốc (m/s) trong ống PPR danh nghĩa dn với lưu lượng q (l/s).</summary>
    public static double Velocity(double q, double dn)
    {
        double di = Ppr.FirstOrDefault(x => Math.Abs(x.Dn - dn) < 1e-6).Di;
        if (di <= 0) di = dn * 0.66;
        return q / 1000 / (Math.PI * Math.Pow(di / 1000, 2) / 4);
    }

    /// <summary>Ống thoát theo tổng đương lượng thoát (DFU); có bồn cầu → Ø110.</summary>
    public static double DrainPipe(double dfu, bool hasWc)
    {
        double d = dfu <= 1 ? 42 : dfu <= 3 ? 49 : dfu <= 6 ? 60 : dfu <= 12 ? 76 : dfu <= 20 ? 90 : 110;
        return hasWc ? Math.Max(d, 110) : d;
    }

    /// <summary>Dòng điện 1 pha (A).</summary>
    public static double Current(double powerW, double u = 220, double cosPhi = 0.85) => powerW <= 0 ? 0 : powerW / (u * cosPhi);

    public static double Breaker(double i)
    {
        foreach (double br in Breakers) if (br >= i - 1e-9) return br;
        return Breakers[^1];
    }

    /// <summary>Tiết diện tối thiểu theo hệ thống (mm²).</summary>
    public static double MinSection(MepSystemKind s) => s is MepSystemKind.DienOCam or MepSystemKind.DienDieuHoa ? 2.5 : 1.5;

    /// <summary>Tiết diện dây (mm²) cho công suất P (W): In = aptomat ≥ I, Iz ≥ In, ≥ tối thiểu hệ thống.</summary>
    public static double WireFor(double powerW, MepSystemKind sys, double u = 220, double cosPhi = 0.85) => WireFor(powerW, sys, u, cosPhi, null, 0);

    /// <summary>Tiết diện dây theo IEC 60364-5-52: In = aptomat ≥ I; Iz·k1·k2 ≥ In; sụt áp trên chiều dài lengthM ≤ giới hạn; ≥ tối thiểu.</summary>
    public static double WireFor(double powerW, MepSystemKind sys, double u, double cosPhi, MepStdOptions o, double lengthM)
    {
        double i = Current(powerW, u, cosPhi), inom = Breaker(i);
        double min = Math.Max(MinSection(sys), MepStandards.Get(o, sys is MepSystemKind.DienOCam or MepSystemKind.DienDieuHoa ? "dien.s_min_oc" : "dien.s_min_cs"));
        double lim = MepStandards.VoltageDropLimit(sys, o);
        foreach (double s in MepStandards.Sections)
        {
            if (s < min - 1e-9 || MepStandards.Iz(s, o) < inom - 1e-9) continue;
            if (lengthM > 0 && MepStandards.VoltageDropPct(i, lengthM, s, cosPhi, u, o) > lim + 1e-9) continue;
            return s;
        }
        return MepStandards.Sections[^1];
    }

    /// <summary>Ống gen nhỏ nhất chứa n dây tiết diện s (hệ số lấp đầy 40 %).</summary>
    public static double ConduitFor(double section, int n)
    {
        double od = WireOd.FirstOrDefault(x => Math.Abs(x.S - section) < 1e-6).Do;
        if (od <= 0) od = 3.6;
        double need = Math.Max(1, n) * Math.PI * od * od / 4;
        foreach (var (dn, di) in Conduit)
            if (need <= MepStandards.Val("dien.fill") * Math.PI * di * di / 4 + 1e-9) return dn;
        return Conduit[^1].Dn;
    }

    /// <summary>Thiết bị phía sau tuyến (phía ToId, không quay lại FromId) theo cùng hệ thống.</summary>
    public static List<MepDevice> Downstream(MepModel m, MepRun r)
    {
        var seen = new HashSet<int> { r.FromId };
        var list = new List<MepDevice>();
        var q = new Queue<int>(); q.Enqueue(r.ToId); seen.Add(r.ToId);
        while (q.Count > 0)
        {
            int id = q.Dequeue();
            var d = m.Device(id); if (d != null) list.Add(d);
            foreach (var x in m.Runs.Where(x => x.System == r.System && (x.FromId == id || x.ToId == id)))
            {
                int o = x.FromId == id ? x.ToId : x.FromId;
                if (seen.Add(o)) q.Enqueue(o);
            }
        }
        return list;
    }

    /// <summary>Tự chọn cỡ mọi tuyến AutoSize; trả số tuyến đã đổi.</summary>
    public static int AutoSize(Project p)
    {
        var m = p.Mep; int changed = 0;
        foreach (var r in m.Runs.Where(r => r.AutoSize))
        {
            var down = Downstream(m, r);
            var fams = down.Select(d => m.FamilyOf(d)).Where(f => f != null).ToList();
            var so = m.Settings?.Standards;
            double old = r.Size, oldC = r.ConduitSize;
            if (MepSystems.IsSupply(r.System))
                r.Size = SupplyPipe(SupplyFlow(fams.Sum(f => f.SupplyUnits), so), MepStandards.Get(so, "cap.v_max"));
            else if (MepSystems.IsDrain(r.System))
                r.Size = DrainPipe(fams.Sum(f => f.DrainUnits), fams.Any(f => f.Symbol == "boncau"));
            else
            {
                var c = m.Circuits.FirstOrDefault(x => x.Name == r.Circuit);
                double pw = fams.Sum(f => f.Power) * (c?.Demand ?? 1.0);
                // sụt áp theo chiều dài tuyến tới thiết bị xa nhất phía sau (đơn giản: chiều dài tuyến này + các tuyến phía sau cùng hệ)
                double len = r.Length + down.Sum(d => m.Runs.Where(x => x.System == r.System && x.FromId == d.Id).Sum(x => x.Length));
                r.Size = WireFor(pw, r.System, c?.Voltage ?? 220, c?.CosPhi ?? 0.85, so, len);
                r.ConduitSize = ConduitFor(r.Size, Math.Max(1, r.Conductors));
            }
            if (Math.Abs(old - r.Size) > 1e-9 || Math.Abs(oldC - r.ConduitSize) > 1e-9) changed++;
        }
        return changed;
    }
}

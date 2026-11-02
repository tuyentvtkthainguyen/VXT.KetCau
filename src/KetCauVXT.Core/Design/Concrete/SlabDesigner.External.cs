using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

/// <summary>Thiết kế sàn theo mô men nhập từ SAFE / ETABS (AnalysisResults.SlabMoments): mô men nhịp lấy giá trị dương lớn nhất,
/// mô men gối lấy giá trị âm nhỏ nhất trong các điểm/trạm của dải rơi vào ô sàn, bao qua các tổ hợp TTGH I.
/// Phương có số liệu thay hẳn kết quả phương pháp hệ số (kể cả khi không có mô men âm → gối = 0); phương không có số liệu giữ nguyên.</summary>
public static partial class SlabDesigner
{
    private static void ApplyExternalMoments(AnalysisResults r, Slab slab, SlabDesign d, bool shortIsX,
        ref double m1, ref double mI, ref double m2, ref double mII)
    {
        if (r == null || r.SlabMoments.Count == 0 || !r.SlabMoments.TryGetValue(slab.Id, out var byCase) || byCase.Count == 0) return;
        var combos = r.DesignCombos();
        double xs = double.NaN, xg = double.NaN, ys = double.NaN, yg = double.NaN;
        int nx = 0, ny = 0;
        string src = "";
        foreach (var cn in combos.Where(byCase.ContainsKey))
        {
            var m = byCase[cn];
            src = m.Source;
            if (m.PointsX > 0)
            {
                nx += m.PointsX;
                xs = NanMax(xs, double.IsNaN(m.MxSpan) ? 0 : m.MxSpan);
                xg = NanMin(xg, double.IsNaN(m.MxSupport) ? 0 : m.MxSupport);
            }
            if (m.PointsY > 0)
            {
                ny += m.PointsY;
                ys = NanMax(ys, double.IsNaN(m.MySpan) ? 0 : m.MySpan);
                yg = NanMin(yg, double.IsNaN(m.MySupport) ? 0 : m.MySupport);
            }
        }
        if (nx == 0 && ny == 0) return;
        // phương ngắn (m1, mI) và phương dài (m2, mII) theo phương tổng thể
        double sS = shortIsX ? xs : ys, sG = shortIsX ? xg : yg, lS = shortIsX ? ys : xs, lG = shortIsX ? yg : xg;
        int nS = shortIsX ? nx : ny, nL = shortIsX ? ny : nx;
        string old = "M1 = " + DesignUtil.F(m1, "0.00") + ", MI = " + DesignUtil.F(mI, "0.00") + ", M2 = " + DesignUtil.F(m2, "0.00") + ", MII = " + DesignUtil.F(mII, "0.00");
        if (nS > 0) { m1 = Math.Max(sS, 0); mI = Math.Min(sG, 0); }
        if (nL > 0 && d.TwoWay) { m2 = Math.Max(lS, 0); mII = Math.Min(lG, 0); }
        d.Messages.Add("Mô men sàn lấy theo kết quả " + (string.IsNullOrEmpty(src) ? r.Source : src) + " (" + (nx + ny) + " điểm số liệu trong ô, bao "
                       + combos.Count(byCase.ContainsKey) + " tổ hợp TTGH I) thay phương pháp hệ số [" + old + "]: M1 = "
                       + DesignUtil.F(m1, "0.00") + ", MI = " + DesignUtil.F(mI, "0.00") + ", M2 = " + DesignUtil.F(m2, "0.00") + ", MII = " + DesignUtil.F(mII, "0.00") + " kN.m/m"
                       + (nS == 0 ? " – phương ngắn không có số liệu, giữ hệ số" : "") + (nL == 0 && d.TwoWay ? " – phương dài không có số liệu, giữ hệ số" : ""));
    }

    private static double NanMax(double a, double b) => double.IsNaN(a) ? b : Math.Max(a, b);
    private static double NanMin(double a, double b) => double.IsNaN(a) ? b : Math.Min(a, b);
}

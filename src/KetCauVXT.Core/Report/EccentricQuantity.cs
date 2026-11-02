using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>[MóngLệchTâm] Khối lượng phần móng lệch tâm (gọi từ QuantityCalculator.Compute sau phần móng):
/// <list type="bullet">
/// <item>Dầm móng cân bằng DM: bê tông b × h × L thông thuỷ (giữa 2 mép móng), ván khuôn 2 thành (đáy trên lớp đệm xốp / cát), bê tông lót dưới lớp đệm;
/// cốt thép lấy theo bảng thống kê (RebarSchedule – nhóm "Giằng móng").</item>
/// <item>Giằng móng mô hình trùng vị trí dầm cân bằng (nối đúng 2 chân cột của dầm DM) được thay bằng dầm DM – không tính trùng.</item>
/// <item>Móng kết hợp hình thang: trừ phần chênh giữa hình chữ nhật L × Bmax (đã tính ở mục móng đơn) và hình thang thật.</item>
/// </list></summary>
public static class EccentricQuantity
{
    private static string F2(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    private static string Mm(double m) => (m * 1000).ToString("0", System.Globalization.CultureInfo.InvariantCulture);

    public static void Compute(Project p, QuantityResult q, DesignSummary s, QuantitySettings qs, ConcreteDesignSettings cs)
    {
        if (p == null || q == null || s == null) return;
        string g = QuantityCalculator.Grade(cs?.ConcreteFooting, "B20");
        double lt = Math.Max(0, qs?.LeanThickness ?? 0), lo = Math.Max(0, qs?.LeanOffset ?? 0);
        int replaced = 0;
        foreach (var sb in s.StrapBeams ?? new List<StrapBeamDesign>())
        {
            int n = Math.Max(1, sb.Quantity);
            double Lc = Math.Max(sb.ClearLength, 0);
            if (Lc <= 1e-6 || sb.B <= 0 || sb.H <= 0) continue;
            q.Concrete.Add(new ConcreteItem
            {
                Category = QtyCat.TieBeam, Story = QuantityCalculator.FoundationStory, Member = sb.Name, Grade = g, Count = n,
                Dims = $"{Mm(sb.B)}×{Mm(sb.H)}, Ltt = {F2(Lc)} (dầm móng cân bằng)", Volume = n * sb.B * sb.H * Lc,
                Formwork = n * 2 * sb.H * Lc, FormworkNote = (n > 1 ? n + "×" : "") + $"2×{F2(sb.H)}×{F2(Lc)} (2 thành, đáy trên lớp đệm)",
                Note = "Dầm móng cân bằng – móng lệch tâm sát ranh giới đất"
            });
            if (lt > 0)
                q.Lean.Add(new ConcreteItem
                {
                    Category = QtyCat.Lean, Story = QuantityCalculator.FoundationStory, Member = "Lót " + sb.Name, Grade = qs.LeanGrade, Count = n,
                    Dims = $"{F2(sb.B + 2 * lo)}×{F2(Lc)}×{F2(lt)}", Volume = n * (sb.B + 2 * lo) * Lc * lt
                });
            // giằng móng mô hình nối đúng 2 chân cột của dầm cân bằng → thay bằng dầm DM
            foreach (var l in sb.Links)
                foreach (var f in p.Frames.Where(f => f.Kind == FrameKind.Beam && ((f.NodeI == l.NodeA && f.NodeJ == l.NodeB) || (f.NodeI == l.NodeB && f.NodeJ == l.NodeA))))
                {
                    string m = string.IsNullOrWhiteSpace(f.Label) ? "GM" + f.Id : f.Label;
                    replaced += q.Concrete.RemoveAll(c => c.Category == QtyCat.TieBeam && c.Member == m);
                    q.Lean.RemoveAll(c => c.Member == "Lót " + m);
                }
        }
        foreach (var f in s.Footings.Where(x => x.Eccentric?.Combined != null && x.Eccentric.Combined.Trapezoid))
        {
            var c = f.Eccentric.Combined;
            int n = Math.Max(1, f.Quantity);
            double bm = Math.Max(c.B1, c.B2), dv = c.Length * (bm - (c.B1 + c.B2) / 2) * f.H;
            double side = Math.Sqrt(c.Length * c.Length + Math.Pow((c.B1 - c.B2) / 2, 2));
            double dfw = 2 * (f.L + f.B) * f.H - (c.B1 + c.B2 + 2 * side) * f.H;
            q.Concrete.Add(new ConcreteItem
            {
                Category = QtyCat.Footing, Story = QuantityCalculator.FoundationStory, Member = "Trừ hình thang " + f.Name, Grade = g, Count = n,
                Dims = $"−{F2(c.Length)}×({F2(bm)} − ({F2(c.B1)}+{F2(c.B2)})/2)×{F2(f.H)}", Volume = -n * dv, Formwork = -n * dfw,
                Note = "Móng kết hợp hình thang (B1 ≠ B2)"
            });
            if (lt > 0)
                q.Lean.Add(new ConcreteItem
                {
                    Category = QtyCat.Lean, Story = QuantityCalculator.FoundationStory, Member = "Trừ lót hình thang " + f.Name, Grade = qs.LeanGrade, Count = n,
                    Volume = -n * c.Length * (bm - (c.B1 + c.B2) / 2) * lt
                });
        }
        if ((s.StrapBeams?.Count ?? 0) > 0)
            q.Notes.Add("Dầm móng cân bằng (DM – móng lệch tâm sát ranh giới đất): bê tông b×h × chiều dài thông thuỷ giữa 2 mép móng, ván khuôn 2 thành; "
                        + "đáy dầm đặt trên lớp đệm xốp / cát (không tựa nền); cốt thép theo bảng thống kê nhóm 'Giằng móng'."
                        + (replaced > 0 ? $" {replaced} giằng móng mô hình trùng vị trí đã thay bằng dầm DM." : ""));
    }
}

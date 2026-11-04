using System.Globalization;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>Sàn hạ cốt trong thuyết minh (mục con của "Thiết kế sàn") và bảng khối lượng (trang "Hoàn thiện sàn", chỉ thông tin):
/// danh sách ô hạ cốt (tầng, ô, Δ, công năng, diện tích) và kiểm tra chiều cao dầm biên h ≥ hs1 + Δ + hs2.
/// Khối lượng hoàn thiện KHÔNG đổi: lớp bù cốt / tạo dốc đã nằm trong cấu tạo sàn (vd. "Sàn vệ sinh": vữa tạo dốc, hạ cốt 50).</summary>
public static class SlabDropReport
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static double AreaOf(Project p, Slab s)
    {
        try
        {
            var poly = s.Nodes.Select(id => p.Node(id)).Select(n => (n.X, n.Y)).ToList();
            return poly.Count >= 3 ? Math.Abs(QuantityCalculator.Area(poly)) : 0;
        }
        catch (KeyNotFoundException) { return 0; }
    }

    private static string UseName(Project p, Slab s)
    {
        string n = ModelEdit.Loads(ModelEdit.UseOf(p, s)).Name;
        return string.IsNullOrEmpty(s.BuildUp) ? n : n + " / " + s.BuildUp;
    }

    /// <summary>Bảng thông tin ô hạ cốt cho trang "Hoàn thiện sàn"; null nếu dự án không có ô hạ cốt.</summary>
    public static QtyTable QuantityTable(Project p)
    {
        var low = SlabDrop.Lowered(p);
        if (low.Count == 0) return null;
        var order = p.Stories.OrderBy(x => x.Elevation).Select(x => x.Name).ToList();
        var t = new QtyTable
        {
            Title = "Sàn hạ cốt (thông tin – khối lượng lớp bù cốt / tạo dốc đã tính trong cấu tạo sàn)",
            Headers = new[] { "Tầng", "Ô sàn", "Δ (mm)", "Cao độ so với sàn tầng", "Công năng / cấu tạo", "Diện tích (m²)", "Ghi chú" },
            Widths = new double[] { 10, 12, 9, 14, 34, 13, 30 }
        };
        double sum = 0;
        foreach (var s in low.OrderBy(x => order.IndexOf(x.Story)).ThenBy(x => x.Label, StringComparer.Ordinal))
        {
            double a = Math.Round(AreaOf(p, s), 2);
            sum += a;
            t.Add(s.Story, s.Label, Math.Round(s.LevelOffset * 1000, 0), SlabDrop.LevelText(s.LevelOffset), UseName(p, s), a, s.DropReason ?? "");
        }
        t.AddTotal("TỔNG", low.Count.ToString(Inv) + " ô", null, null, null, Math.Round(sum, 2), null);
        return t;
    }

    /// <summary>Mục "Sàn hạ cốt" trong chương thiết kế sàn (chỉ khi có ô hạ cốt).</summary>
    public static void Section(Project p, DesignSummary s, DocxWriter d, int n)
    {
        var low = SlabDrop.Lowered(p);
        if (low.Count == 0) return;
        var order = p.Stories.OrderBy(x => x.Elevation).Select(x => x.Name).ToList();
        d.Heading($"{n}.3. Sàn hạ cốt (sàn vệ sinh, ban công, lô gia…)", 2);
        double thr = SlabDropChecks.Threshold(p) * 1000;
        d.Paragraph("Ô sàn hạ cốt có mặt trên thấp hơn cao độ sàn tầng một khoảng Δ. Tải trọng ô sàn giữ nguyên (lớp bù cốt, tạo dốc, chống thấm đã kể trong cấu tạo sàn) "
                    + "và truyền về các dầm đỡ như ô sàn thường. Cạnh chung giữa hai ô chênh cốt lớn hơn " + thr.ToString("0", Inv)
                    + " mm được coi là khớp (bản sàn gãy tại bậc, không liên tục mô men); thép mũ không vắt qua bậc – mỗi ô đặt thép mũ cấu tạo riêng "
                    + "(≥ ⅓ thép chịu lực nhịp, ≥ hàm lượng tối thiểu, bước ≤ 200 mm) vươn lx/4 và neo qua dầm. "
                    + "Dầm đỡ ô hạ cốt kiểm tra chiều cao h ≥ hs1 + Δ + hs2 (hs1 – chiều dày ô phía cao, dầm biên lấy hs1 = hs2; hs2 – chiều dày ô hạ) "
                    + "để mặt dưới ô hạ còn cách đáy dầm tối thiểu một chiều dày sàn, đủ neo thép sàn và bố trí cốt đai.");
        d.Table(new[] { "Tầng", "Ô sàn", "Δ (mm)", "▽ so với sàn tầng", "Công năng", "Ghi chú" },
            low.OrderBy(x => order.IndexOf(x.Story)).ThenBy(x => x.Label, StringComparer.Ordinal).Select(x => new[]
            {
                x.Story, x.Label, SlabDrop.MmText(x.LevelOffset), SlabDrop.LevelText(x.LevelOffset), UseName(p, x), x.DropReason ?? ""
            }), new[] { 1.6, 2.0, 1.4, 2.2, 5.6, 4.2 }, 9);
        var cks = s != null ? SlabDropChecks.BeamChecks(p, s) : new List<SlabDropBeamCheck>();
        if (cks.Count == 0) { d.Paragraph("Không có dầm đỡ ô sàn hạ cốt trong kết quả thiết kế dầm.", false, true); return; }
        d.Paragraph("Kiểm tra chiều cao dầm đỡ ô sàn hạ cốt (nhịp bất lợi nhất mỗi dầm):");
        d.Table(new[] { "Dầm", "Tầng", "Nhịp", "Ô hạ / ô kề", "hs1", "Δ", "hs2", "h yêu cầu", "h dầm", "Kết luận" },
            cks.Select(c => new[]
            {
                c.Beam, c.Story, c.Span.ToString(Inv), c.LowSlab + (string.IsNullOrEmpty(c.HighSlab) ? " (biên)" : " / " + c.HighSlab),
                c.Hs1.ToString("0", Inv), c.Drop.ToString("0", Inv), c.Hs2.ToString("0", Inv), c.HReq.ToString("0", Inv), c.H.ToString("0", Inv),
                c.Ok ? "Đạt" : "KHÔNG ĐẠT – đề nghị h ≥ " + c.Suggested.ToString("0", Inv)
            }), new[] { 1.6, 1.3, 1.1, 3.0, 1.1, 1.1, 1.1, 1.5, 1.4, 3.8 }, 9);
        d.Paragraph("Kích thước mm.", false, true);
    }
}

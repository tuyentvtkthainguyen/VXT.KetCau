using KetCauVXT.Design;

namespace KetCauVXT.Drawing;

/// <summary>Lỗ xuyên dầm (ống lồng chờ) thể hiện trên MẶT ĐỨNG DẦM – từ cảnh báo điện nước (Mep/PenetrationRegister) hoặc nhập tay
/// (ManualBeam.Penetrations). Toạ độ theo hệ mặt đứng: X (mm) tính từ tim gối đầu dầm, Zb (mm) tâm lỗ cách đáy dầm.</summary>
public sealed class BeamHole
{
    public double X { get; set; }
    public double Zb { get; set; }
    /// <summary>Ø lỗ / ống lồng (mm)</summary>
    public double Dia { get; set; }
    /// <summary>Ký hiệu chi tiết gia cường, vd. "LX1"</summary>
    public string Tag { get; set; } = "";
    /// <summary>Số đai gia cường mỗi bên lỗ (bước 50)</summary>
    public int StirrupsPerSide { get; set; } = 2;
    public int StirrupDia { get; set; } = 8;
    /// <summary>Thép dọc bổ sung trên + dưới lỗ: Ø và chiều dài (mm)</summary>
    public int BarDia { get; set; } = 12;
    public double BarLen { get; set; } = 1000;
    /// <summary>Mức cảnh báo (chữ) – "Nguy hiểm" vẽ thêm dấu "!"</summary>
    public string Severity { get; set; } = "";
}

/// <summary>Vẽ lỗ xuyên dầm trên mặt đứng: vòng lỗ + ống lồng, đai gia cường mỗi bên, thép dọc bổ sung trên / dưới lỗ,
/// nhãn "LX1 – Ø130, xem chi tiết LX1".</summary>
public static class BeamHoleDraw
{
    /// <summary>Tiền tố vai trò của thép gia cường lỗ (không vẽ như thép dọc chạy suốt / không dùng làm đai chính của dầm).</summary>
    public const string RolePrefix = "Gia cường lỗ";

    public static bool IsHoleBar(BarMark b) => b != null && (b.Role ?? "").StartsWith(RolePrefix, StringComparison.Ordinal);

    /// <summary>Thép của dầm trừ thép gia cường lỗ.</summary>
    public static IEnumerable<BarMark> MainBars(BeamLineDesign d) => (d?.Bars ?? new List<BarMark>()).Where(b => !IsHoleBar(b));

    /// <summary>Vẽ các lỗ (h: chiều cao dầm mm; đỉnh dầm y = 0, đáy y = −h). Trả về số lỗ đã vẽ.</summary>
    public static int Draw(DxfWriter w, IEnumerable<BeamHole> holes, double h, TagLayout layout = null)
    {
        int n = 0;
        if (holes == null) return 0;
        double th = w.Th;
        foreach (var ho in holes.OrderBy(x => x.X))
        {
            if (ho == null || ho.Dia <= 0) continue;
            double r = ho.Dia / 2, yc = -h + ho.Zb;
            w.Circle(Lay.BeTong, ho.X, yc, r);
            w.Circle(Lay.Thep, ho.X, yc, r + 3);
            // đai gia cường mỗi bên lỗ, bước 50
            for (int k = 0; k < Math.Max(1, ho.StirrupsPerSide); k++)
                foreach (int sg in new[] { -1, 1 })
                {
                    double x = ho.X + sg * (r + 50 + k * 50);
                    w.Line(Lay.Dai, x, -25, x, -h + 25);
                }
            // thép dọc bổ sung trên / dưới lỗ
            double lb = Math.Max(ho.BarLen, ho.Dia + 200);
            double ya = yc + r + 30, yb = yc - r - 30;
            w.Line(Lay.Thep, ho.X - lb / 2, ya, ho.X + lb / 2, ya);
            w.Line(Lay.Thep, ho.X - lb / 2, yb, ho.X + lb / 2, yb);
            // nhãn: đặt dưới dầm, dịch tránh nhãn khác
            string txt = $"{ho.Tag} – lỗ Ø{DxfWriter.N0(ho.Dia)} (xem chi tiết {ho.Tag})";
            double ky = -h - 6.0 * th;
            var box = w.TagBox(ho.X, ky, txt, 0, 1);
            double dy = 0;
            if (layout != null) (_, dy) = layout.Place(box, 0, -1.8 * th);
            w.Line(Lay.KyHieu, ho.X, yc - r, ho.X, ky + dy + 0.2 * th);
            w.Text(Lay.Chu, ho.X + 0.4 * th, ky + dy, 0.9 * th, txt, 0, 0, 0);
            if ((ho.Severity ?? "").StartsWith("Nguy"))
                w.Text(Lay.Chu, ho.X + r + 0.3 * th, yc + r + 0.2 * th, 1.2 * th, "!", 0, 0, 0);
            n++;
        }
        return n;
    }
}

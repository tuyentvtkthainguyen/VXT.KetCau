using KetCauVXT.Drawing;

namespace KetCauVXT.Detailing;

/// <summary>THUYẾT MINH NGUYÊN TẮC BỐ TRÍ THÉP SÀN THEO MIỀN CHỊU KÉO + THỨ TỰ ĐẶT THÉP (ghi chú cấu tạo sàn, dầm).
/// Mô men dương (giữa ô) gây kéo thớ DƯỚI → thép lớp dưới; mô men âm (trên dầm / cạnh ngàm) gây kéo thớ TRÊN → thép mũ.
/// Phương ngắn chịu mô men lớn hơn ⇒ thép phương ngắn đặt ở vị trí có cánh tay đòn h0 lớn nhất: lớp dưới đặt TRƯỚC (sát đáy),
/// lớp trên đặt SAU CÙNG (trên cùng).</summary>
public static class RebarNotes
{
    /// <summary>Thứ tự đặt thép sàn (ghi chú thi công).</summary>
    public static List<string> SlabOrder(double cover = 0.015) => new()
    {
        "1) Thép lớp dưới PHƯƠNG NGẮN (chịu mô men dương lớn hơn) – đặt trước, sát đáy sàn, cánh tay đòn h0 lớn nhất.",
        "2) Thép lớp dưới PHƯƠNG DÀI – đặt trên lớp phương ngắn, buộc tại các giao điểm.",
        "3) Kê con kê bê tông / nhựa dưới lớp dưới đảm bảo lớp bảo vệ " + Dt.Mm(cover) + " mm, bước ≤ 1 m (≈ 4 con/m²).",
        "4) Đặt chân chó (ghế đỡ) Ø8–Ø10 bước ≤ 1 m đỡ lưới thép trên.",
        "5) Thép mũ PHƯƠNG DÀI (cạnh ngắn) và thép phân bố đỡ mũ – lớp dưới của lưới trên.",
        "6) Thép mũ PHƯƠNG NGẮN (cạnh dài, mô men âm lớn hơn) – đặt sau cùng, trên cùng.",
        "7) Buộc toàn bộ giao điểm hàng biên, các hàng trong buộc so le ô cờ; kiểm tra lớp bảo vệ, vệ sinh trước khi đổ bê tông."
    };

    /// <summary>Nguyên tắc bố trí thép sàn theo miền chịu kéo (thuyết minh). lShort: cạnh ngắn ô (m), t: chiều dày (m), bw: bề rộng dầm (m).</summary>
    public static List<string> SlabPrinciple(double lShort, double t, double bw, double cover, int dBot = 8, int dTop = 8, double lap = 0)
    {
        double ext = Math.Max(lShort / 4, 0);
        var l = new List<string>
        {
            "## Nguyên tắc bố trí thép sàn theo miền chịu kéo",
            "Miền giữa ô (mô men dương) – thớ dưới chịu kéo: thép lớp dưới hai phương; phương ngắn mô men lớn hơn nên đặt DƯỚI CÙNG (h0 = t − c − d/2), phương dài đặt trên (h0 nhỏ hơn d).",
            "Miền trên dầm / cạnh ngàm (mô men âm) – thớ trên chịu kéo: thép mũ vươn từ mép dầm ≥ L0/4 cạnh ngắn"
                + (ext > 0 ? " = " + Dt.Mm(ext) + " mm" : "") + ", đầu trong bẻ móc xuống (t − 2c" + (t > 0 ? " = " + Dt.Mm(Math.Max(t - 2 * cover, 0)) + " mm" : "") + "); mũ phương ngắn đặt TRÊN CÙNG.",
            "Thép phân bố (đỡ mũ / bản loại dầm) ≥ 10 % thép chịu lực và ≥ Ø6a250, đặt vuông góc, phía trong thép chịu lực (TCVN 5574:2018 mục 10.3.8).",
            "Neo thép lớp dưới vào dầm biên ≥ max(10d; 100 mm) tính từ mép dầm" + (bw > 0 ? " (dầm b = " + Dt.Mm(bw) + " mm)" : "") + ", hoặc chạy suốt qua dầm sang ô kề.",
            "Nối thép lớp dưới tại gối (mô men dương nhỏ), nối thép lớp trên ở khoảng giữa nhịp (mô men âm ≈ 0); nối chồng ll"
                + (lap > 0 ? " = " + Dt.Mm(lap) + " mm" : " ≥ 1,2·lan") + ", ≤ 50 % số thanh tại một tiết diện, so le ≥ 1,3·ll.",
            "Lớp bê tông bảo vệ c = " + Dt.Mm(cover) + " mm; mép lỗ > 300 mm gia cường ≥ 2Ø12 mỗi cạnh, dài cạnh lỗ + 2·40d.",
            "## Thứ tự đặt thép sàn"
        };
        l.AddRange(SlabOrder(cover));
        return l;
    }

    /// <summary>Thứ tự lắp dựng thép dầm (ghi chú cấu tạo).</summary>
    public static List<string> BeamOrder() => new()
    {
        "## Thứ tự lắp dựng thép dầm",
        "1) Thép dọc DƯỚI: chạy suốt (góc đai) → tăng cường hàng 1 → kê thép đệm Ø25 ngăn hàng → tăng cường hàng 2.",
        "2) Luồn ĐAI: vùng gối bước dày trước, giữa nhịp sau; móc 135° bố trí so le các góc.",
        "3) Thép dọc TRÊN chạy suốt buộc vào góc đai.",
        "4) Thép MŨ GỐI (tăng cường trên) hàng 1, rồi hàng 2; thép giá / đai C (nếu có).",
        "5) Kê con kê lớp bảo vệ đáy và thành dầm; kiểm tra vị trí cắt, nối thép trước khi đặt thép sàn."
    };

    /// <summary>Khối ghi chú "GHI CHÚ CẤU TẠO THÉP SÀN" (tỷ lệ 1/1 – kích thước giấy mm), ngắt dòng ~ 110 ký tự.</summary>
    public static DxfWriter SlabNoteFigure(IEnumerable<string> lines, string title = "GHI CHÚ CẤU TẠO THÉP SÀN")
    {
        var w = new DxfWriter(1);
        double th = 2.5, y = 0;
        w.Text(Lay.Chu, 0, y, 3.5, title, 0, 0, 0);
        w.Line(Lay.Chu, 0, y - 1.0, Math.Max(DxfWriter.TextWidth(title, 3.5) * w.WidthFactor, 40), y - 1.0);
        y -= 7;
        foreach (var raw in lines ?? Enumerable.Empty<string>())
        {
            bool head = raw.StartsWith("## ");
            string t = head ? raw[3..].ToUpperInvariant() : raw;
            foreach (var ln in Wrap(t, 110))
            {
                w.Text(Lay.Chu, head ? 0 : 3, y, head ? 2.8 : th, ln, 0, 0, 0);
                y -= head ? 5 : 4.2;
            }
            if (head) y -= 0.5;
        }
        return w;
    }

    /// <summary>Ghép khối ghi chú (cỡ giấy) vào dưới hình w theo tỷ lệ của w (chữ in đúng cỡ).</summary>
    public static void AppendBelow(DxfWriter w, IEnumerable<string> lines, string title = "GHI CHÚ CẤU TẠO THÉP SÀN")
    {
        if (w == null) return;
        var note = SlabNoteFigure(lines, title);
        var e = w.Extents;
        double k = w.Scale > 0 ? w.Scale : 1;
        double x0 = e.IsEmpty ? 0 : e.X1, y0 = e.IsEmpty ? 0 : e.Y1 - 6 * k;
        w.AppendScaled(note, k, x0, y0);
    }

    public static IEnumerable<string> Wrap(string t, int max)
    {
        if (string.IsNullOrEmpty(t)) { yield return ""; yield break; }
        var words = t.Split(' ');
        var cur = "";
        foreach (var wd in words)
        {
            if (cur.Length > 0 && cur.Length + 1 + wd.Length > max) { yield return cur; cur = "   " + wd; }
            else cur = cur.Length == 0 ? wd : cur + " " + wd;
        }
        if (cur.Length > 0) yield return cur;
    }
}

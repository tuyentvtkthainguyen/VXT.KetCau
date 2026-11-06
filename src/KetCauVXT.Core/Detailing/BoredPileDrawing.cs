using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Drawing;

namespace KetCauVXT.Detailing;

/// <summary>Bản vẽ cọc khoan nhồi (mini): mặt đứng cọc với lồng thép (thép dọc, đai xoắn 2 vùng bước, đai gia cường, con kê, neo vào đài),
/// mặt cắt ngang cọc, trụ địa chất với chiều dài cọc và ghi chú thi công / thử tải.</summary>
public static class BoredPileDrawing
{
    public static List<Figure> Figures(ManualPileCap cap, DetailResult r, DetailSettings s)
    {
        var figs = new List<Figure>();
        if (cap == null || r == null || cap.PileD <= 0) return figs;
        figs.Add(Elevation(cap, r));
        figs.Add(Section(cap, r));
        if (cap.SoilLayers != null && cap.SoilLayers.Count > 0) figs.Add(SoilProfile(cap));
        return figs;
    }

    static BarMark Bar(DetailResult r, string role) => r.Bars.FirstOrDefault(b => b.Role == role);

    /// <summary>Mặt đứng cọc (tỉ lệ 1/50, chiều dài thật – dài thì cắt ngắt đoạn giữa thân).</summary>
    public static Figure Elevation(ManualPileCap cap, DetailResult r)
    {
        double S = 50;
        var w = new DxfWriter(S);
        double d = cap.PileD * 1000, L = Math.Max(cap.PileLength, 1) * 1000, cage = (cap.CageLenUsed > 0 ? cap.CageLenUsed : cap.PileLength) * 1000;
        double cover = 50, em = cap.Embed * 1000, anchor = cap.AnchorUsed * 1000;
        // đầu cọc tại y = 0, mũi tại y = −L
        w.Rect(Lay.BeTong, -d / 2, -L, d / 2, 0);
        w.Rect(Lay.BeTong, -d, 0, d, Math.Max(cap.CapH * 1000, 400));   // phần đài (tượng trưng)
        w.Text(Lay.Chu, 0, Math.Max(cap.CapH * 1000, 400) / 2, 0.8 * w.Th, "ĐÀI", 0, 1, 2);
        w.Line(Lay.Truc, 0, -L - 600, 0, Math.Max(cap.CapH * 1000, 400) + 600, "CENTER");
        // thép dọc 2 bên (thể hiện 2 thanh)
        var bl = Bar(r, "Cọc – thép dọc");
        double xs = d / 2 - cover - (cap.PileBarD / 2.0);
        foreach (double sg in new[] { -1.0, 1.0 })
            w.Line(Lay.Thep, sg * xs, -cage, sg * xs, em + anchor);
        if (bl != null) w.LeaderTag(xs, -cage / 2, d / 2 + 3 * w.Th, -cage / 2 + 1.5 * w.Th, bl.Count / Math.Max(cap.Piles.Count, 1) + "Ø" + bl.Dia + " (thép dọc cọc)", bl.Mark);
        // đai xoắn: vùng đầu cọc bước nhỏ, thân cọc bước lớn (vẽ nét chéo đại diện)
        var sp = Bar(r, "Cọc – đai xoắn");
        double sTop = Math.Min(100, cap.SpiralS * 1000), sBody = Math.Max(cap.SpiralS * 1000, 100);
        double lTop = Math.Min(Math.Max(2000, 3 * d), cage);
        for (double y = -sTop / 2; y > -lTop; y -= sTop) w.Line(Lay.Dai, -xs, y, xs, y - sTop / 2);
        for (double y = -lTop - sBody / 2; y > -cage; y -= sBody) w.Line(Lay.Dai, -xs, y, xs, y - sBody / 2);
        if (sp != null)
        {
            w.LeaderTag(-xs, -lTop / 2, -d / 2 - 4 * w.Th, -lTop / 2 + 1.5 * w.Th, "Ø" + sp.Dia + "a" + DxfWriter.N0(sTop) + " (đai xoắn)", sp.Mark, -1);
            w.LeaderTag(-xs, -(lTop + cage) / 2, -d / 2 - 4 * w.Th, -(lTop + cage) / 2 + 1.5 * w.Th, "Ø" + sp.Dia + "a" + DxfWriter.N0(sBody) + " (đai xoắn)", sp.Mark, -1);
        }
        // đai gia cường + con kê mỗi 2 m
        var rg = Bar(r, "Cọc – đai gia cường");
        for (double y = -1000; y > -cage; y -= 2000)
        {
            w.Line(Lay.Thep, -xs, y, xs, y);
            foreach (double sg in new[] { -1.0, 1.0 }) w.Circle(Lay.KyHieu, sg * (d / 2 - cover / 2), y, cover / 2);
        }
        if (rg != null) w.LeaderTag(xs, -1000, d / 2 + 3 * w.Th, -1000 - 2.5 * w.Th, "Ø" + rg.Dia + " đai gia cường a2000", rg.Mark);
        w.Text(Lay.KyHieu, d / 2 + 0.5 * w.Th, -3000, 0.6 * w.Th, "CON KÊ 4 CÁI / TẦNG, a2000", 0, 0, 2);
        // chân lồng thép
        if (cage < L - 1)
        {
            w.Line(Lay.KyHieu, -d / 2 - 300, -cage, d / 2 + 300, -cage, "DASHED");
            w.Text(Lay.Chu, d / 2 + 0.5 * w.Th, -cage, 0.7 * w.Th, "CHÂN LỒNG THÉP", 0, 0, 2);
        }
        // kích thước
        w.Dim(-d / 2, -L, -d / 2, 0, -3.5 * w.Th);
        w.Dim(d / 2, -cage, d / 2, 0, 6.0 * w.Th);
        w.Dim(d / 2, -lTop, d / 2, 0, 3.0 * w.Th);
        w.Dim(-d / 2, -L, d / 2, -L, -2.5 * w.Th);
        w.ElevMark(-d - 300, 0, DxfWriter.ElevText(-cap.Df), -1);
        w.ElevMark(-d - 300, -L, DxfWriter.ElevText(-cap.Df - cap.PileLength), -1);
        w.Text(Lay.KyHieu, 0, em + 0.3 * w.Th, 0.6 * w.Th, "ĐẬP ĐẦU CỌC, NGÀM " + DxfWriter.N0(em) + " VÀO ĐÀI", 0, 1, 0);
        w.Title(0, -L - 1800, "CHI TIẾT CỌC KHOAN NHỒI D" + DxfWriter.N0(d) + " (" + cap.Name + ")", DrawUtil.ScaleText(S));
        return new Figure(w, cap.Name + " cọc khoan nhồi");
    }

    /// <summary>Mặt cắt ngang cọc (tỉ lệ 1/20): vòng thép dọc, đai xoắn, con kê.</summary>
    public static Figure Section(ManualPileCap cap, DetailResult r)
    {
        double S = 20;
        var w = new DxfWriter(S);
        double d = cap.PileD * 1000, cover = 50;
        int nb = Math.Max(cap.PileBarN, 0);
        double rs = d / 2 - cover - cap.PileBarD / 2.0;
        w.Circle(Lay.BeTong, 0, 0, d / 2);
        w.Circle(Lay.Dai, 0, 0, d / 2 - cover + 4);
        for (int i = 0; i < nb; i++)
        {
            double a = 2 * Math.PI * i / Math.Max(nb, 1) + Math.PI / 2;
            w.Dot(Lay.Thep, rs * Math.Cos(a), rs * Math.Sin(a), Math.Max(cap.PileBarD / 2.0, 6));
        }
        var bl = Bar(r, "Cọc – thép dọc");
        var sp = Bar(r, "Cọc – đai xoắn");
        if (bl != null) w.LeaderTag(0, rs, d / 2 + 2 * w.Th, d / 2 + 1.5 * w.Th, bl.Count / Math.Max(cap.Piles.Count, 1) + "Ø" + bl.Dia, bl.Mark);
        if (sp != null) w.LeaderTag(d / 2 - cover, 0, d / 2 + 2 * w.Th, -1.5 * w.Th, "Ø" + sp.Dia + " xoắn", sp.Mark);
        w.Dim(-d / 2, -d / 2, d / 2, -d / 2, -2.5 * w.Th, 1, "D" + DxfWriter.N0(d));
        w.Text(Lay.KyHieu, 0, -d / 2 - 4.5 * w.Th, 0.6 * w.Th, "LỚP BẢO VỆ " + DxfWriter.N0(cover), 0, 1, 0);
        w.Title(0, -d / 2 - 6.5 * w.Th, "MẶT CẮT CỌC", DrawUtil.ScaleText(S), true);
        return new Figure(w, cap.Name + " mặt cắt cọc");
    }

    /// <summary>Trụ địa chất với vị trí đầu / mũi cọc (tỉ lệ 1/100).</summary>
    public static Figure SoilProfile(ManualPileCap cap)
    {
        double S = 100;
        var w = new DxfWriter(S);
        double colW = 1500, y = 0;
        double total = cap.SoilLayers.Sum(l => Math.Max(l.Thickness, 0)) * 1000;
        double tip = (cap.Df + cap.PileLength) * 1000;
        if (tip > total) total = tip + 1000;
        for (int i = 0; i < cap.SoilLayers.Count; i++)
        {
            var l = cap.SoilLayers[i];
            double th = Math.Max(l.Thickness, 0) * 1000;
            if (i == cap.SoilLayers.Count - 1) th = Math.Max(th, total - (-y));
            w.Rect(Lay.KyHieu, 0, y - th, colW, y);
            string info = l.Name + (l.IsSand ? " – N = " + DxfWriter.N0(l.NSpt) : " – IL = " + l.IL.ToString("0.##") + (l.NSpt > 0 ? ", N = " + DxfWriter.N0(l.NSpt) : ""));
            w.Text(Lay.Chu, colW + 0.5 * w.Th, y - th / 2, 0.8 * w.Th, info, 0, 0, 2);
            w.ElevMark(-200, y - th, DxfWriter.ElevText((y - th) / 1000), -1);
            y -= th;
        }
        double xp = colW / 2, d = Math.Max(cap.PileD * 1000, 300);
        w.Rect(Lay.BeTong, xp - d / 2, -tip, xp + d / 2, -cap.Df * 1000);
        w.Text(Lay.Chu, xp, -tip - 0.6 * w.Th, 0.8 * w.Th, "MŨI CỌC " + DxfWriter.ElevText(-cap.Df - cap.PileLength), 0, 1, 3);
        var br = cap.BoredResult;
        string cap1 = br != null ? "[P]đ = " + br.Psoil.ToString("0") + " kN; Pvl = " + br.Pvl.ToString("0") + " kN; [P] = " + cap.PaUsed.ToString("0") + " kN" : "[P] = " + cap.PaUsed.ToString("0") + " kN";
        w.Text(Lay.Chu, 0, 1.5 * w.Th, 0.8 * w.Th, cap1, 0, 0, 0);
        w.Title(colW / 2, -total - 1500, "TRỤ ĐỊA CHẤT – CHIỀU DÀI CỌC", DrawUtil.ScaleText(S));
        return new Figure(w, cap.Name + " trụ địa chất");
    }
}

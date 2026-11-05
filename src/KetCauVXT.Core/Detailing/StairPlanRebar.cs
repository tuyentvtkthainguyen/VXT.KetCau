using KetCauVXT.Design;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Detailing;

/// <summary>Bản vẽ bố trí thép bản thang (bản thang thường): MẶT BẰNG BỐ TRÍ THÉP (hình chiếu bằng các vế + chiếu nghỉ, thép chịu lực dọc bản,
/// thép mũ gối, thép phân bố – thanh đại diện có móc, chữ Ø..a.. + vòng số hiệu, đường phạm vi rải kiểu Kết cấu Soft; dầm chiếu nghỉ / dầm chiếu tới nét đứt;
/// mũi tên LÊN; vạch cắt 1-1 dọc vế 1 và A-A ngang vế) và MẶT CẮT NGANG BẢN THANG A-A (bậc xây gạch nét mảnh + tô gạch, không có thép).
/// Số hiệu lấy từ bảng thép của cấu kiện (DetailResult.Bars, vai trò "tên bộ phận – vai trò thép").</summary>
public static class StairPlanRebar
{
    public const double PlanScale = 50, CrossScale = 20;

    private static BarMark Find(DetailResult r, StairPart part, string role, int nth = 0)
    {
        if (part == null) return null;
        string key = part.Name + " – " + role;
        return r.Bars.Where(b => b.Role == key).Skip(nth).FirstOrDefault();
    }

    private static string Tag(BarMark b) => b == null ? "" : b.Spacing > 0 ? Dt.Spaced(b.Dia, b.Spacing) : $"{b.Count}Ø{b.Dia}";

    private static StairPart FlightPart(StairDesign sd, int flight) => sd.Parts.FirstOrDefault(p => p.Kind == "Bản thang" && p.Flights.Contains(flight));
    private static StairPart LandingPart(StairDesign sd, int landing) => sd.Parts.FirstOrDefault(p => p.Kind == "Chiếu nghỉ" && p.Flights.Contains(landing));

    public static List<Figure> Build(DetailResult r, StairDesign sd, StairLayout L, DetailSettings s)
    {
        var list = new List<Figure>();
        if (r == null || sd == null || L == null || !L.Valid || L.Flights.Count == 0) return list;
        list.Add(new Figure(Plan(r, sd, L, s), "MẶT BẰNG BỐ TRÍ THÉP BẢN THANG " + sd.Name, true));
        var f1 = L.Flights.FirstOrDefault(f => f.Treads > 0) ?? L.Flights[0];
        list.Add(new Figure(Cross(r, sd, L, f1, s), "MẶT CẮT A-A BẢN THANG " + sd.Name, false));
        return list;
    }

    public static DxfWriter Plan(DetailResult r, StairDesign sd, StairLayout L, DetailSettings s)
    {
        var w = new DxfWriter(PlanScale);
        double th = w.Th;
        double c = Math.Max(s?.CoverSlab ?? 0.015, 0.015);
        static double M(double v) => v * 1000;
        // ô thang, dầm bao (nét đứt), dầm chiếu nghỉ (nét đứt)
        w.Rect(Lay.BeTong, 0, 0, M(L.Lr), M(L.Wr));
        double bw = 110;
        w.Rect(Lay.BeTong, -bw, -bw, M(L.Lr) + bw, M(L.Wr) + bw, "DASHED");
        if (L.HasLandingBeam)
        {
            double hb = M(Math.Max(L.S.LandingBeamB, 0.1)) / 2;
            w.Rect(Lay.BeTong, M(L.LandingBeamU) - hb, 0, M(L.LandingBeamU) + hb, M(L.Wr), "DASHED");
            w.Text(Lay.Chu, M(L.LandingBeamU) + hb + 0.4 * th, M(L.Wr) * 0.5, 0.85 * th, "DẦM CHIẾU NGHỈ " + Dt.Mm(L.S.LandingBeamB) + "×" + Dt.Mm(L.S.LandingBeamH), 90, 1, 0);
        }
        w.Text(Lay.Chu, M(L.Lr) / 2, -bw - 0.5 * th, 0.85 * th, "DẦM CHIẾU TỚI (SÀN TẦNG)", 0, 1, 3);

        foreach (var f in L.Flights)
        {
            bool alongU = f.DU != 0;
            // vế: viền + vạch bậc (nét mảnh – bậc xây gạch)
            w.Rect(Lay.BeTong, M(f.Umin), M(f.Vmin), M(f.Umax), M(f.Vmax));
            for (int i = 1; i <= f.Treads; i++)
            {
                double t = i * L.Tread;
                if (alongU) { double u = f.U0 + f.DU * t; w.Line(Lay.KyHieu, M(u), M(f.Vmin), M(u), M(f.Vmax)); }
                else { double v = f.V0 + f.DV * t; w.Line(Lay.KyHieu, M(f.Umin), M(v), M(f.Umax), M(v)); }
            }
            // mũi tên LÊN
            double a0 = 0.4 * L.Tread, a1 = Math.Max(a0 + 0.1, f.Run - 0.3 * L.Tread);
            (double, double) P(double t) => alongU ? (M(f.U0 + f.DU * t), M(f.Vmin + 0.82 * f.Width)) : (M(f.Umin + 0.82 * (f.Umax - f.Umin)), M(f.V0 + f.DV * t));
            var (ax, ay) = P(a0); var (bx, by) = P(a1);
            w.Line(Lay.KyHieu, ax, ay, bx, by);
            w.Circle(Lay.KyHieu, ax, ay, 0.3 * th);
            double ux = Math.Sign(bx - ax), uy = Math.Sign(by - ay);
            w.Polyline(Lay.KyHieu, new List<(double, double)> { (bx - ux * th + uy * 0.4 * th, by - uy * th + ux * 0.4 * th), (bx, by), (bx - ux * th - uy * 0.4 * th, by - uy * th - ux * 0.4 * th) }, false);
            w.Text(Lay.Chu, ax, ay + 0.6 * th, 0.85 * th, "LÊN", 0, 1, 0);

            var part = FlightPart(sd, f.Index);
            var main = Find(r, part, "Dưới");
            var top1 = Find(r, part, "Mũ gối", 0);
            var top2 = Find(r, part, "Mũ gối", 1);
            var dist = Find(r, part, "Thép rải dưới");
            double cosA = f.CosA > 0 ? f.CosA : 1;
            if (alongU)
            {
                double ua = Math.Min(f.Umin, f.Umax), ub = Math.Max(f.Umin, f.Umax);
                if (main != null)
                    PlanBarDraw.Bar(w, true, M(ua) - 150, M(ub) + 150, M(f.Vmin + 0.32 * f.Width), Tag(main), main.Mark,
                        range: (M(f.Vmin) + M(c), M(f.Vmax) - M(c)), rangeAt: 0.30, textAt: 0.55);
                double lt = top1 != null ? Math.Max(0.3, (top1.End - top1.Start) * cosA) : 0.25 * f.Run;
                if (top1 != null)
                    PlanBarDraw.Bar(w, true, M(ua) - 150, M(ua + lt), M(f.Vmin + 0.60 * f.Width), Tag(top1), top1.Mark, hookB: 60, hookSide: -1,
                        range: (M(f.Vmin) + M(c), M(f.Vmax) - M(c)), rangeAt: 0.6, textAt: 0.55);
                if (top2 != null)
                    PlanBarDraw.Bar(w, true, M(ub - lt), M(ub) + 150, M(f.Vmin + 0.60 * f.Width), Tag(top2), top2.Mark, hookA: 60, hookSide: -1,
                        range: (M(f.Vmin) + M(c), M(f.Vmax) - M(c)), rangeAt: 0.4, textAt: 0.45);
                if (dist != null)
                    PlanBarDraw.Bar(w, false, M(f.Vmin) + M(c), M(f.Vmax) - M(c), M(ua + 0.55 * (ub - ua)), Tag(dist), dist.Mark,
                        range: (M(ua) + 50, M(ub) - 50), rangeAt: 0.20, textAt: 0.55);
                // vạch cắt 1-1 dọc vế (vế 1), A-A ngang vế
                if (f.Index == 1)
                {
                    double vc = f.Vmin + 0.45 * f.Width;
                    w.CutMark(M(ua) - 2.6 * th, M(vc), M(ua) - 1.2 * th, M(vc), "1", M(ua) - 3.3 * th, M(vc) + 0.9 * th);
                    w.CutMark(M(ub) + 1.2 * th, M(vc), M(ub) + 2.6 * th, M(vc), "1", M(ub) + 3.3 * th, M(vc) + 0.9 * th);
                    double uc = ua + 0.40 * (ub - ua);
                    w.CutMark(M(uc), M(f.Vmin) - 2.6 * th - bw, M(uc), M(f.Vmin) - 1.2 * th - bw, "A", M(uc) + 0.9 * th, M(f.Vmin) - 3.3 * th - bw);
                }
            }
            else
            {
                double va = Math.Min(f.Vmin, f.Vmax), vb = Math.Max(f.Vmin, f.Vmax);
                double w0 = f.Umax - f.Umin;
                if (main != null)
                    PlanBarDraw.Bar(w, false, M(va) - 150, M(vb) + 150, M(f.Umin + 0.32 * w0), Tag(main), main.Mark,
                        range: (M(f.Umin) + M(c), M(f.Umax) - M(c)), rangeAt: 0.30, textAt: 0.55);
                if (dist != null)
                    PlanBarDraw.Bar(w, true, M(f.Umin) + M(c), M(f.Umax) - M(c), M(va + 0.55 * (vb - va)), Tag(dist), dist.Mark,
                        range: (M(va) + 50, M(vb) - 50), rangeAt: 0.2, textAt: 0.55);
                var tp = top1 ?? top2;
                if (tp != null)
                    PlanBarDraw.Bar(w, false, M(va) - 150, M(va + Math.Max(0.3, (tp.End - tp.Start) * cosA)), M(f.Umin + 0.65 * w0), Tag(tp), tp.Mark, hookB: 60, hookSide: -1);
            }
        }
        // chiếu nghỉ
        for (int li = 0; li < L.Landings.Count; li++)
        {
            var ld = L.Landings[li];
            w.Rect(Lay.BeTong, M(ld.Umin), M(ld.Vmin), M(ld.Umax), M(ld.Vmax));
            var part = LandingPart(sd, li + 1) ?? sd.Parts.FirstOrDefault(p => p.Kind == "Chiếu nghỉ");
            var main = Find(r, part, "Dưới");
            var dist = Find(r, part, "Thép rải dưới");
            bool along = part?.MainAlongFlight ?? true;
            double ua = ld.Umin, ub = ld.Umax, va = ld.Vmin, vb = ld.Vmax;
            if (main != null)
            {
                if (along) PlanBarDraw.Bar(w, true, M(ua) - 120, M(ub) + 100, M(va + 0.35 * (vb - va)), Tag(main), main.Mark, range: (M(va) + M(c), M(vb) - M(c)), rangeAt: 0.35);
                else PlanBarDraw.Bar(w, false, M(va) - 100, M(vb) + 100, M(ua + 0.35 * (ub - ua)), Tag(main), main.Mark, range: (M(ua) + M(c), M(ub) - M(c)), rangeAt: 0.35);
            }
            if (dist != null)
            {
                if (along) PlanBarDraw.Bar(w, false, M(va) + M(c), M(vb) - M(c), M(ua + 0.70 * (ub - ua)), Tag(dist), dist.Mark, range: (M(ua) + 50, M(ub) - 50), rangeAt: 0.7);
                else PlanBarDraw.Bar(w, true, M(ua) + M(c), M(ub) - M(c), M(va + 0.70 * (vb - va)), Tag(dist), dist.Mark, range: (M(va) + 50, M(vb) - 50), rangeAt: 0.7);
            }
            if (li == 0) w.Text(Lay.Chu, M((ua + ub) / 2), M(vb) - 1.2 * th, 0.85 * th, "CHIẾU NGHỈ", 0, 1, 2);
        }
        // kích thước
        w.Dim(0, 0, M(L.Lr), 0, -bw - 5 * th);
        w.Dim(0, 0, 0, M(L.Wr), bw + 5 * th);
        var us = new List<double> { 0 };
        foreach (var f in L.Flights.Where(x => x.DU != 0)) { us.Add(M(Math.Min(f.Umin, f.Umax))); us.Add(M(Math.Max(f.Umin, f.Umax))); }
        us.Add(M(L.Lr));
        us = us.Distinct().OrderBy(x => x).ToList();
        if (us.Count > 2) w.DimChainH(us, 0, -bw - 3 * th);
        w.Text(Lay.Chu, M(L.Lr) / 2, M(L.Wr) + bw + 1.0 * th, 0.85 * th, "BẬC XÂY GẠCH ĐẶC VỮA M75 TRÊN BẢN THANG – KHÔNG CÓ THÉP TRONG BẬC", 0, 1, 0);
        w.Title(M(L.Lr) / 2, w.Extents.Y2 + 3 * th, "MẶT BẰNG BỐ TRÍ THÉP BẢN THANG " + sd.Name, DrawUtil.ScaleText(PlanScale));
        return w;
    }

    /// <summary>Mặt cắt ngang vế A-A (mặt phẳng đứng vuông góc phương vế): bản thang dày t/cos α theo phương đứng, thép chịu lực (chấm), thép phân bố, bậc phía trên.</summary>
    public static DxfWriter Cross(DetailResult r, StairDesign sd, StairLayout L, StairFlight f, DetailSettings s)
    {
        var w = new DxfWriter(CrossScale);
        double th = w.Th;
        double c = Math.Max(s?.CoverSlab ?? 0.015, 0.015) * 1000;
        double cosA = f.CosA > 0 ? f.CosA : 1;
        double bw = f.Width * 1000, tv = L.WaistT * 1000 / cosA, hStep = L.Riser * 1000 * 0.5;
        bool brick = L.S.Steps == StepConstruction.Brick;
        // bản thang
        if (w.HatchOn) w.HatchMatRect(HatchMaterial.Concrete, 0, 0, bw, tv);
        w.Rect(Lay.BeTong, 0, 0, bw, tv);
        // bậc (cắt qua giữa bậc: phần bậc cao trung bình h/2)
        if (w.HatchOn) w.HatchMatRect(brick ? HatchMaterial.Brick : HatchMaterial.Concrete, 0, tv, bw, tv + hStep);
        w.Polyline(brick ? Lay.KyHieu : Lay.BeTong, new List<(double X, double Y)> { (0, tv), (0, tv + hStep), (bw, tv + hStep), (bw, tv) }, false);
        w.Text(Lay.Chu, bw / 2, tv + hStep + 0.5 * th, 0.85 * th, brick ? "BẬC XÂY GẠCH ĐẶC (KHÔNG THÉP)" : "BẬC BTCT LIỀN KHỐI", 0, 1, 0);
        // thép
        var part = FlightPart(sd, f.Index);
        var main = Find(r, part, "Dưới");
        var dist = Find(r, part, "Thép rải dưới");
        var top = Find(r, part, "Mũ gối");
        int dM = main?.Dia ?? 10;
        double dr = Math.Max(dM / 2.0, 0.12 * th);
        double yM = c + dr;
        double sp = Math.Max((main?.Spacing ?? 0.15) * 1000, 50);
        for (double x = c + dr; x <= bw - c - dr + 1e-6; x += sp) w.Dot(Lay.Thep, x, yM, dr);
        w.Line(Lay.Thep, c, yM + dr + 4, bw - c, yM + dr + 4);
        if (top != null)
        {
            double drT = Math.Max(top.Dia / 2.0, 0.12 * th);
            double spT = Math.Max(top.Spacing * 1000, 50);
            for (double x = c + drT; x <= bw - c - drT + 1e-6; x += spT) w.Dot(Lay.Thep, x, tv - c - drT, drT);
        }
        if (main != null) w.LeaderTag(bw * 0.28, yM, bw * 0.28 + 2 * th, -4 * th, Tag(main), main.Mark, 1);
        if (dist != null) w.LeaderTag(bw * 0.62, yM + dr + 4, bw + 2 * th, -2.5 * th, Tag(dist), dist.Mark, 1);
        if (top != null) w.LeaderTag(bw * 0.45, tv - c - Math.Max(top.Dia / 2.0, 0.12 * th), -2 * th, tv + hStep + 3 * th, Tag(top) + " (gối)", top.Mark, -1);
        w.Dim(0, 0, bw, 0, -7 * th);
        w.Dim(bw, 0, bw, tv, -(3 * th), 1, Dt.Mm(L.WaistT) + "/cosα");
        w.Title(bw / 2, w.Extents.Y1 - 1.2 * th, "MẶT CẮT A-A BẢN THANG " + sd.Name, DrawUtil.ScaleText(CrossScale), true);
        return w;
    }
}

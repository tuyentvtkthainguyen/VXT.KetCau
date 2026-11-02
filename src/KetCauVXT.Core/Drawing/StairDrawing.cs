using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Bản vẽ cầu thang bộ (KC-08, SheetViewKind.StairDetail) theo quy tắc ghi thép kiểu Kết cấu Soft:
/// <list type="number">
/// <item>MẶT BẰNG CẦU THANG (1/S, mặc định 1/50): vế, bậc đánh số, mũi tên LÊN, chiếu nghỉ, dầm chiếu nghỉ / dầm cốn, vạch cắt, kích thước, cao độ.</item>
/// <item>MẶT CẮT i-i dọc từng vế (1/(S/2) = 1/25): bậc (xây gạch / BTCT), lớp ốp, bản thang, chiếu nghỉ, dầm, sàn tầng trên/dưới, cao độ,
///   kích thước (n × b, n × h) và bố trí thép bản thang: thép chịu lực neo vào dầm, thép mũ L0/4 tại gối, thép phân bố – nhãn "Ø10a150 ①".</item>
/// <item>CHI TIẾT CHIẾU NGHỈ, MẶT CẮT DẦM CHIẾU NGHỈ / DẦM CỐN (1/20) có nhãn đai.</item>
/// <item>BẢNG THỐNG KÊ CỐT THÉP CẦU THANG (số hiệu từ 1 theo từng bộ phận) + một khối ghi chú chung.</item>
/// </list>
/// Chữ, kích thước dùng các hàm của DxfWriter (chiều cao chữ Th theo tỷ lệ tờ) để theo thiết lập kiểu bản vẽ chung.</summary>
public static class StairDrawing
{
    /// <summary>Tỷ lệ tờ mặc định (mặt bằng); mặt cắt vẽ ở tỷ lệ S/2, mặt cắt dầm ở S·2/5.</summary>
    public const double SheetScale = 50;
    /// <summary>Tỷ lệ mặt bằng cầu thang theo Thiết lập bản vẽ (DrawingKind.StairPlan, mặc định 1/50).</summary>
    public static double CurrentScale => Pos(DrawingStyle.Current.ScaleOf(DrawingKind.StairPlan), SheetScale);
    /// <summary>Hệ số phóng hình mặt cắt vế / chiếu nghỉ = tỷ lệ tờ / tỷ lệ mặt cắt (DrawingKind.StairSection, mặc định 1/25).</summary>
    public static double SectionK(double S) => S / Pos(DrawingStyle.Current.ScaleOf(DrawingKind.StairSection), 25);
    /// <summary>Hệ số phóng hình mặt cắt dầm chiếu nghỉ / dầm cốn (DrawingKind.MemberSection, mặc định 1/25).</summary>
    public static double BeamK(double S) => S / Pos(DrawingStyle.Current.ScaleOf(DrawingKind.MemberSection), 25);
    private static double Pos(double v, double def) => v > 0 && double.IsFinite(v) ? v : def;

    public static List<Figure> BuildAll(Project p, DesignSummary d, double scale = 0)
    {
        var res = new List<Figure>();
        if (d?.Stairs == null) return res;
        if (scale <= 0) scale = CurrentScale;
        foreach (var sd in d.Stairs)
        {
            try { res.AddRange(Build(p, d, sd, scale)); }
            catch (Exception ex) { res.Add(new Figure(DrawUtil.ErrorFigure("cầu thang " + sd.Name, ex, scale), sd.Name, true)); }
        }
        return res;
    }

    /// <summary>Bản vẽ một cầu thang khai báo (thiết kế ngay nếu bản thiết kế chưa có cầu thang này).</summary>
    public static List<Figure> Build(Project p, DesignSummary design, Stair stair, double scale = 0)
    {
        var sd = design?.Stairs?.FirstOrDefault(x => (stair.Id > 0 && x.StairId == stair.Id) || (stair.Id <= 0 && stair.OpeningId > 0 && x.OpeningId == stair.OpeningId))
                 ?? StairDesigner.DesignOne(p, stair);
        return Build(p, design, sd, scale);
    }

    /// <summary>Các mảnh hình của một cầu thang ở tỷ lệ tờ 1/scale.</summary>
    public static List<Figure> Build(Project p, DesignSummary design, StairDesign sd, double scale = 0)
    {
        var res = new List<Figure>();
        if (sd?.Stair == null) return res;
        double S = scale > 0 ? scale : CurrentScale;
        var L = sd.Stair.Layout(p);
        if (!L.Valid) { res.Add(new Figure(DrawUtil.ErrorFigure("cầu thang " + sd.Name, new InvalidOperationException(string.Join(" ", L.Warnings)), S), sd.Name, true)); return res; }
        res.Add(new Figure(Plan(p, sd, L, S), sd.Name + " MB", true));
        int i = 1;
        foreach (var f in L.Flights)
        {
            var part = sd.Parts.FirstOrDefault(x => x.Kind == "Bản thang" && x.Flights.Contains(f.Index));
            res.Add(new Figure(FlightSection(p, sd, L, f, part, i, S), $"{sd.Name} {i}-{i}", i == 1));
            i++;
        }
        bool first = true;
        foreach (var part in sd.Parts.Where(x => x.Kind == "Chiếu nghỉ"))
        {
            res.Add(new Figure(LandingDetail(p, sd, L, part, S), part.Name, first));
            first = false;
        }
        foreach (var part in sd.Parts.Where(x => x.Kind is "Dầm chiếu nghỉ" or "Dầm cốn"))
        {
            res.Add(new Figure(BeamSection(p, part, S), part.Name, first));
            first = false;
        }
        var rows = StairSchedule.RowsOf(sd);
        if (rows.Count > 0) res.Add(new Figure(ScheduleTable(rows, S, $"BẢNG THỐNG KÊ CỐT THÉP CẦU THANG {sd.Name}"), sd.Name + " BTK", true));
        res.Add(new Figure(Notes(p, sd, L, S), sd.Name + " GC", false));
        return res;
    }

    // ==================================================================
    // MẶT BẰNG (toạ độ cục bộ: u → X, v → Y; vế chạy theo phương ngang tờ)
    // ==================================================================
    private static DxfWriter Plan(Project p, StairDesign sd, StairLayout L, double S)
    {
        var w = new DxfWriter(S);
        double th = w.Th;
        double X(double u) => u * 1000;
        double Y(double v) => v * 1000;
        var s = L.S;
        // ô thang + dầm viền
        w.Rect(Lay.BeTong, X(0), Y(0), X(L.Lr), Y(L.Wr));
        double bw = 220;
        w.Rect(Lay.BeTong, X(0) - bw / 2, Y(0) - bw / 2, X(L.Lr) + bw / 2, Y(L.Wr) + bw / 2);
        int stepNo = 0;
        int cut = 1;
        foreach (var f in L.Flights)
        {
            w.Rect(Lay.BeTong, X(f.Umin), Y(f.Vmin), X(f.Umax), Y(f.Vmax));
            double b = L.Tread;
            for (int i = 0; i <= f.Treads; i++)
            {
                double t = i * b;
                if (f.DU != 0) { double u = f.U0 + f.DU * t; w.Line(Lay.BeTong, X(u), Y(f.Vmin), X(u), Y(f.Vmax)); }
                else { double v = f.V0 + f.DV * t; w.Line(Lay.BeTong, X(f.Umin), Y(v), X(f.Umax), Y(v)); }
            }
            // số bậc (mặt bậc) – đánh liên tục từ chân thang
            for (int i = 1; i <= f.Treads; i++)
            {
                double t = (i - 0.5) * b;
                double cu = f.DU != 0 ? f.U0 + f.DU * t : (f.Umin + f.Umax) / 2;
                double cv = f.DU != 0 ? f.Vmin + 0.25 * f.Width : f.V0 + f.DV * t;
                w.Text(Lay.Chu, X(cu), Y(cv), 0.8 * th, (stepNo + i).ToString(DrawUtil.Inv), f.DU != 0 ? 90 : 0, 1, 2);
            }
            stepNo += f.Risers;
            // mũi tên lên
            double a0 = 0.5 * b, a1 = Math.Max(a0 + 0.1, f.Run - 0.3 * b);
            (double, double) P(double t) => f.DU != 0 ? (X(f.U0 + f.DU * t), Y((f.Vmin + f.Vmax) / 2 + 0.1 * f.Width)) : (X((f.Umin + f.Umax) / 2), Y(f.V0 + f.DV * t));
            var (ax, ay) = P(a0); var (bx, by) = P(a1);
            w.Line(Lay.KyHieu, ax, ay, bx, by);
            w.Circle(Lay.KyHieu, ax, ay, 0.35 * th);
            double ux = Math.Sign(bx - ax), uy = Math.Sign(by - ay);
            w.Polyline(Lay.KyHieu, new List<(double, double)> { (bx - ux * th + uy * 0.4 * th, by - uy * th + ux * 0.4 * th), (bx, by), (bx - ux * th - uy * 0.4 * th, by - uy * th - ux * 0.4 * th) }, false);
            if (f.Index == 1) w.Text(Lay.Chu, ax, ay + 0.8 * th, th, "LÊN", 0, 1, 0);
            // dầm cốn
            if (L.Stringers)
            {
                double sb = s.StringerB;
                if (f.DU != 0)
                {
                    if (L.StringerCount >= 2 || f.Vmin > 1e-6) w.Rect(Lay.BeTong, X(f.Umin), Y(f.Vmin), X(f.Umax), Y(f.Vmin + sb), null);
                    if (L.StringerCount >= 2 || f.Vmin < 1e-6) w.Rect(Lay.BeTong, X(f.Umin), Y(f.Vmax - sb), X(f.Umax), Y(f.Vmax), null);
                }
            }
            // vạch cắt dọc vế
            if (f.DU != 0)
            {
                double vc = (f.Vmin + f.Vmax) / 2 - 0.15 * f.Width;
                double e = 3 * th;
                w.CutMark(X(0) - bw / 2 - e, Y(vc) - th, X(0) - bw / 2 - e, Y(vc) + th, cut.ToString(DrawUtil.Inv), X(0) - bw / 2 - e - 1.2 * th, Y(vc));
                w.CutMark(X(L.Lr) + bw / 2 + e, Y(vc) - th, X(L.Lr) + bw / 2 + e, Y(vc) + th, cut.ToString(DrawUtil.Inv), X(L.Lr) + bw / 2 + e + 1.2 * th, Y(vc));
            }
            cut++;
        }
        // chiếu nghỉ
        int k = 1;
        foreach (var ld in L.Landings)
        {
            w.Rect(Lay.BeTong, X(ld.Umin), Y(ld.Vmin), X(ld.Umax), Y(ld.Vmax));
            double cx = X((ld.Umin + ld.Umax) / 2), cy = Y((ld.Vmin + ld.Vmax) / 2);
            w.Text(Lay.Chu, cx, cy + 0.6 * th, th, L.Landings.Count > 1 ? $"CHIẾU NGHỈ {k}" : "CHIẾU NGHỈ", 0, 1, 0);
            w.ElevMark(cx, cy - 1.2 * th, DxfWriter.ElevText(ld.Z), 1);
            k++;
        }
        // dầm chiếu nghỉ
        if (L.HasLandingBeam)
        {
            double b2 = s.LandingBeamB / 2;
            w.Line(Lay.BeTong, X(L.LandingBeamU - b2), Y(0), X(L.LandingBeamU - b2), Y(L.Wr), null);
            w.Line(Lay.BeTong, X(L.LandingBeamU + b2), Y(0), X(L.LandingBeamU + b2), Y(L.Wr), null);
            w.Text(Lay.Chu, X(L.LandingBeamU) - 0.5 * th, Y(L.Wr * 0.5), 0.8 * th, $"DCN {DesignUtil.F(s.LandingBeamB * 1000, "0")}x{DesignUtil.F(s.LandingBeamH * 1000, "0")}", 90, 1, 0);
        }
        // cao độ sàn
        w.ElevMark(X(0) + 2 * th, Y(L.Wr) + bw / 2 + 0.5 * th, DxfWriter.ElevText(L.ZBot) + " / " + DxfWriter.ElevText(L.ZTop), 1);
        // kích thước
        var us = new List<double> { 0, L.Lr };
        foreach (var f in L.Flights.Where(f => f.DU != 0)) { us.Add(f.Umin); us.Add(f.Umax); }
        if (L.HasLandingBeam) us.Add(L.LandingBeamU);
        var uu = us.Select(u => Math.Round(X(u))).Distinct().OrderBy(x => x).ToList();
        double yd = Y(0) - bw / 2 - 2.5 * th;
        w.DimChainH(uu, Y(0), yd);
        w.Dim(X(0), yd - 2.5 * th, X(L.Lr), yd - 2.5 * th, 0);
        var vs = new List<double> { 0, L.Wr };
        foreach (var f in L.Flights) { vs.Add(f.Vmin); vs.Add(f.Vmax); }
        var vv = vs.Select(v => Math.Round(Y(v))).Distinct().OrderBy(x => x).ToList();
        double xd = X(0) - bw / 2 - 6 * th;
        w.DimChainV(vv, X(0), xd);
        w.Dim(xd - 2.5 * th, Y(0), xd - 2.5 * th, Y(L.Wr), 0);
        w.Title(X(L.Lr / 2), w.Extents.Y2 + 4 * th, $"MẶT BẰNG CẦU THANG {sd.Name}", DrawUtil.ScaleText(S));
        return w;
    }

    // ==================================================================
    // MẶT CẮT DỌC VẾ + THÉP BẢN THANG
    // ==================================================================
    private static (double B, double H) SupportBeam(Project p, StairLayout L, StairFlight f, double u, double z)
    {
        try
        {
            double vm = f.DU != 0 ? (f.Vmin + f.Vmax) / 2 : (f.Umin + f.Umax) / 2;
            var (x1, y1) = f.DU != 0 ? L.ToXY(u, f.Vmin) : L.ToXY(f.Umin, u);
            var (x2, y2) = f.DU != 0 ? L.ToXY(u, f.Vmax) : L.ToXY(f.Umax, u);
            var bu = WallLoadGenerator.BeamsUnder(p, new WallLine { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 }, z, 0.25);
            if (bu.Count > 0) { var si = DrawUtil.SectionOf(p, bu[0].f.Section); return (si.B, si.H); }
        }
        catch (Exception) { }
        return (0, 0);
    }

    private static double FloorSlabT(Project p, string story)
    {
        if (string.IsNullOrEmpty(story)) return 0.10;
        var sl = p.Slabs.FirstOrDefault(x => x.Story == story);
        var ss = sl == null ? null : p.SlabSections.FirstOrDefault(x => x.Name == sl.Section);
        return ss?.Thickness > 0 ? ss.Thickness : 0.10;
    }

    private static BarMark Bar(StairPart part, string role, bool? top = null) =>
        part?.Bars.FirstOrDefault(b => b.Role == role && (top == null || b.IsTop == top));

    private static string TagOf(BarMark b) => b == null ? "" : DrawUtil.StirrupTag(b.Dia, b.Spacing);

    private static DxfWriter FlightSection(Project p, StairDesign sd, StairLayout L, StairFlight f, StairPart part, int no, double S)
    {
        var w = new DxfWriter(S);
        double th = w.Th, k = SectionK(S);            // mặt cắt ở tỷ lệ Thiết lập bản vẽ (mặc định 1/25)
        var s = L.S;
        double b = L.Tread, h = L.Riser, run = f.Run, t = L.WaistT, cos = f.CosA, tan = h / Math.Max(b, 1e-6);
        double zB = f.ZBottom, zT = f.ZTop, zRef = zB;
        double X(double sx) => sx * 1000 * k;
        double Y(double z) => (z - zRef) * 1000 * k;
        double c = Math.Max(0.015, p.Concrete.CoverSlab);
        double zTopLine(double sx) => zB + sx * tan;
        double zSoffit(double sx) => zTopLine(sx) - t / cos;

        // ---- bản thang ----
        w.Polyline(Lay.BeTong, new List<(double X, double Y)>
        {
            (X(0), Y(zSoffit(0))), (X(run), Y(zSoffit(run)))
        }, false);
        // ---- bậc ----
        var prof = new List<(double X, double Y)> { (X(0), Y(zB)) };
        for (int i = 1; i <= f.Treads; i++)
        {
            double xa = X(b * (i - 1)), xb = X(b * i), yi = Y(zB + i * h);
            prof.Add((xa, yi)); prof.Add((xb, yi));
        }
        prof.Add((X(run), Y(zT)));
        w.Polyline(Lay.BeTong, prof, false);
        w.Line(s.Steps == StepConstruction.Concrete ? Lay.BeTong : Lay.KyHieu, X(0), Y(zB), X(run), Y(zTopLine(run)));
        if (s.Steps == StepConstruction.Brick)
            for (int i = 1; i <= f.Treads; i++)
            {
                // ký hiệu khối xây: 2 nét xiên trong tam giác bậc
                double x0 = X(b * (i - 1)), y0 = Y(zB + (i - 1) * h);
                w.Line(Lay.KyHieu, x0 + 0.25 * b * 1000 * k, y0 + 0.75 * h * 1000 * k, x0 + 0.5 * b * 1000 * k, y0 + 0.95 * h * 1000 * k);
            }
        if (s.Finish != StepFinish.None)
        {
            double tf = (s.FinishT + Math.Max(0, s.BeddingThickness)) * 1000 * k;
            var fin = new List<(double X, double Y)>();
            for (int i = 1; i <= f.Treads; i++)
            {
                double xa = X(b * (i - 1)) - tf, xb = X(b * i) - tf, yi = Y(zB + i * h) + tf;
                fin.Add((xa, yi)); fin.Add((xb, yi));
            }
            if (fin.Count > 1) w.Polyline(Lay.KyHieu, fin, false);
        }

        // ---- gối hai đầu ----
        double ldDepth = f.DV != 0 ? L.FlightWidth : (L.HasLandingBeam ? L.Lr - L.LandingBeamU : (L.Landings.Count > 0 ? L.Landings[0].Span : 1.0));
        double uBot = f.DU != 0 ? f.U0 : f.V0, uTopE = (f.DU != 0 ? f.U0 + f.DU * run : f.V0 + f.DV * run);
        void End(double se, int sigma, double zf, bool atBottom)
        {
            bool floorTop = Math.Abs(zf - L.ZTop) < 1e-6, floorBot = Math.Abs(zf - L.ZBot) < 1e-6;
            if (floorTop || floorBot)
            {
                string story = floorTop ? L.TopStory : L.BottomStory;
                bool ground = floorBot && string.IsNullOrEmpty(L.BottomStory);
                double tfl = ground ? 0.10 : FloorSlabT(p, story);
                double ext = 0.8;
                w.Rect(Lay.BeTong, Math.Min(X(se), X(se + sigma * ext)), Y(zf - tfl), Math.Max(X(se), X(se + sigma * ext)), Y(zf));
                w.BreakV(Lay.BeTong, X(se + sigma * ext), Y(zf - tfl), Y(zf));
                double uE = atBottom ? uBot : uTopE;
                double edge = f.DU != 0 ? L.Lr : L.Wr;
                bool atEdge = Math.Abs(uE) < 0.06 || Math.Abs(uE - edge) < 0.06;
                if (!ground)
                {
                    var (bb, hb) = atEdge ? SupportBeam(p, L, f, uE, zf) : (0.0, 0.0);
                    if (bb > 0)
                        w.Rect(Lay.BeTong, X(se - bb / 2), Y(zf - hb), X(se + bb / 2), Y(zf - tfl));
                }
                else w.Line(Lay.KyHieu, X(se + sigma * ext), Y(zf - tfl), X(se - sigma * 0.3), Y(zf - tfl));
                w.ElevMark(X(se + sigma * ext * 0.6), Y(zf), DxfWriter.ElevText(zf), sigma);
            }
            else
            {
                double tl = L.LandingT;
                w.Rect(Lay.BeTong, Math.Min(X(se), X(se + sigma * ldDepth)), Y(zf - tl), Math.Max(X(se), X(se + sigma * ldDepth)), Y(zf));
                w.BreakV(Lay.BeTong, X(se + sigma * ldDepth), Y(zf - tl), Y(zf));
                if (L.HasLandingBeam && f.DU != 0)
                    w.Rect(Lay.BeTong, X(se - s.LandingBeamB / 2), Y(zf - s.LandingBeamH), X(se + s.LandingBeamB / 2), Y(zf - tl));
                w.ElevMark(X(se + sigma * ldDepth * 0.6), Y(zf), DxfWriter.ElevText(zf), sigma);
            }
        }
        End(0, -1, zB, true);
        End(run, +1, zT, false);

        // ---- thép ----
        var main = Bar(part, "Dưới");
        var mu = Bar(part, "Mũ gối");
        var dB = Bar(part, "Thép rải dưới");
        var dT = Bar(part, "Thép rải trên");
        bool along = part?.MainAlongFlight ?? true;
        double anch = 0.15;
        int dMain = main?.Dia ?? 10;
        double off1 = (c + dMain / 2000.0) / cos;
        // lớp dưới: thanh chạy dọc vế (chịu lực khi bản tựa dầm; thép rải khi bản tựa dầm cốn)
        var alongBar = along ? main : dB;
        var acrossBar = along ? dB : main;
        double sx0 = -anch * cos, sx1 = run + anch * cos;
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (X(sx0), Y(zSoffit(sx0) + off1)), (X(sx1), Y(zSoffit(sx1) + off1)) }, false, 0.35 * S);
        // chấm thanh ngang vế (lớp trên của lớp dưới)
        double sAc = acrossBar?.Spacing > 0 ? acrossBar.Spacing : 0.2;
        double off2 = (c + dMain / 1000.0 + (acrossBar?.Dia ?? 6) / 2000.0) / cos;
        for (double q = 0.05; q <= f.Inclined - 0.05 + 1e-9; q += sAc)
        {
            double sx = q * cos;
            w.Dot(Lay.Thep, X(sx), Y(zSoffit(sx) + off2), Math.Max(0.15 * th, (acrossBar?.Dia ?? 6) * k / 2));
        }
        // thép mũ hai đầu: đoạn thẳng song song mặt bản, móc xuống trong gối
        double lmH = mu != null && mu.Segments.Count >= 2 ? mu.Segments[1] / 1000.0 * cos : run / 4;
        double hookV = mu != null && mu.Segments.Count >= 2 ? mu.Segments[0] / 1000.0 : t - 2 * c;
        double offT = (c + (mu?.Dia ?? 8) / 2000.0) / cos;
        double a0 = -anch * cos, a1 = lmH - anch * cos;
        w.Polyline(Lay.Thep, new List<(double X, double Y)>
        {
            (X(a0), Y(zTopLine(a0) - offT - hookV)), (X(a0), Y(zTopLine(a0) - offT)), (X(a1), Y(zTopLine(a1) - offT))
        }, false, 0.3 * S);
        double b0 = run - lmH + anch * cos, b1 = run + anch * cos;
        w.Polyline(Lay.Thep, new List<(double X, double Y)>
        {
            (X(b0), Y(zTopLine(b0) - offT)), (X(b1), Y(zTopLine(b1) - offT)), (X(b1), Y(zTopLine(b1) - offT - hookV))
        }, false, 0.3 * S);
        double sDt = dT?.Spacing > 0 ? dT.Spacing : 0.2;
        double offT2 = (c + (mu?.Dia ?? 8) / 1000.0 + (dT?.Dia ?? 6) / 2000.0) / cos;
        for (double q = 0.05; q * cos <= a1 + 1e-9; q += sDt) { double sx = q * cos; w.Dot(Lay.Thep, X(sx), Y(zTopLine(sx) - offT2), Math.Max(0.15 * th, (dT?.Dia ?? 6) * k / 2)); }
        for (double q = 0.05; q * cos <= lmH - anch * cos + 1e-9; q += sDt) { double sx = run - q * cos; w.Dot(Lay.Thep, X(sx), Y(zTopLine(sx) - offT2), Math.Max(0.15 * th, (dT?.Dia ?? 6) * k / 2)); }

        // ---- nhãn thép (Kết cấu Soft): chấm tại thanh → đường dẫn → vai có chữ → vòng số ----
        double yLow = Y(zSoffit(0)) - 3 * th;
        if (alongBar != null)
        {
            double sx = 0.55 * run;
            w.LeaderTag(X(sx), Y(zSoffit(sx) + off1), X(sx) + 2 * th, Math.Min(yLow, Y(zSoffit(sx))) - 2 * th, TagOf(alongBar), alongBar.Mark, 1);
        }
        if (acrossBar != null)
        {
            double sx = 0.35 * run;
            w.LeaderTag(X(sx), Y(zSoffit(sx) + off2), X(sx) - 2 * th, Math.Min(yLow, Y(zSoffit(sx))) - 5.5 * th, TagOf(acrossBar), acrossBar.Mark, -1);
        }
        if (mu != null)
        {
            double sx = 0.5 * a1;
            w.LeaderTag(X(sx), Y(zTopLine(sx) - offT), X(sx) - 4 * th, Y(zB + 4 * h) + 3 * th, TagOf(mu), mu.Mark, -1);
            double sy = run - 0.5 * (lmH - anch * cos);
            w.LeaderTag(X(sy), Y(zTopLine(sy) - offT), X(run) + 2 * th, Y(zT) + 4 * th, TagOf(mu), mu.Mark, 1);
        }
        if (dT != null)
        {
            double sx = Math.Min(0.15, a1 * 0.5);
            w.LeaderTag(X(sx), Y(zTopLine(sx) - offT2), X(sx) - 6 * th, Y(zB + 2 * h) + 1.5 * th, TagOf(dT), dT.Mark, -1);
        }
        // ---- kích thước ----
        double ydim = w.Extents.Y1 - 2 * th;
        w.Dim(X(0), Y(zB), X(run), Y(zB), ydim - Y(zB), k, $"{f.Treads}x{DesignUtil.F(b * 1000, "0")}={DesignUtil.F(run * 1000, "0")}");
        double xdim = w.Extents.X1 - 2 * th;
        w.Dim(X(0), Y(zB), X(0), Y(zT), X(0) - xdim, k, $"{f.Risers}x{DesignUtil.F(h * 1000, "0")}={DesignUtil.F((zT - zB) * 1000, "0")}");
        // chiều dày bản thang (đo vuông góc)
        double sm = 0.75 * run;
        double nx = -Math.Sin(Math.Atan(tan)), ny = Math.Cos(Math.Atan(tan));
        double px = X(sm), py = Y(zTopLine(sm));
        w.Dim(px, py, px - nx * t * 1000 * k, py - ny * t * 1000 * k, -2.5 * th, k);
        w.Title(X(run / 2), w.Extents.Y2 + 4 * th, $"MẶT CẮT {no}-{no}", DrawUtil.ScaleText(S / k));
        return w;
    }

    // ==================================================================
    // CHI TIẾT CHIẾU NGHỈ
    // ==================================================================
    private static DxfWriter LandingDetail(Project p, StairDesign sd, StairLayout L, StairPart part, double S)
    {
        var w = new DxfWriter(S);
        double th = w.Th, k = SectionK(S);
        var s = L.S;
        double span = part.Span, t = part.H, c = Math.Max(0.015, p.Concrete.CoverSlab);
        double X(double x) => x * 1000 * k;
        double Y(double z) => z * 1000 * k;
        w.Rect(Lay.BeTong, X(0), Y(-t), X(span), Y(0));
        double zL = L.Landings.Count > 0 ? L.Landings[0].Z : 0;
        // gối trái: dầm chiếu nghỉ (hoặc tường), gối phải: tường đầu ô
        if (L.HasLandingBeam) w.Rect(Lay.BeTong, X(-s.LandingBeamB), Y(-s.LandingBeamH), X(0), Y(-t));
        else w.Rect(Lay.KyHieu, X(-0.22), Y(-0.6), X(0), Y(-t));
        w.Rect(Lay.KyHieu, X(span), Y(-0.6), X(span + 0.22), Y(0));
        w.BreakH(Lay.KyHieu, X(span), X(span + 0.22), Y(-0.6));
        var main = Bar(part, "Dưới"); var mu = Bar(part, "Mũ gối"); var dB = Bar(part, "Thép rải dưới"); var dT = Bar(part, "Thép rải trên");
        int dm = main?.Dia ?? 8;
        double yb = -t + c + dm / 2000.0;
        double anch = 0.15;
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (X(-anch), Y(yb)), (X(span + anch), Y(yb)) }, false, 0.35 * S);
        double sd2 = dB?.Spacing > 0 ? dB.Spacing : 0.2;
        for (double x = 0.05; x <= span - 0.05 + 1e-9; x += sd2) w.Dot(Lay.Thep, X(x), Y(yb + (dm + (dB?.Dia ?? 6)) / 2000.0), Math.Max(0.15 * th, (dB?.Dia ?? 6) * k / 2));
        double lm = mu != null && mu.Segments.Count >= 2 ? mu.Segments[1] / 1000.0 : span / 4;
        double hook = mu != null && mu.Segments.Count >= 2 ? mu.Segments[0] / 1000.0 : t - 2 * c;
        double yt = -c - (mu?.Dia ?? 8) / 2000.0;
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (X(-anch), Y(yt - hook)), (X(-anch), Y(yt)), (X(lm - anch), Y(yt)) }, false, 0.3 * S);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (X(span - lm + anch), Y(yt)), (X(span + anch), Y(yt)), (X(span + anch), Y(yt - hook)) }, false, 0.3 * S);
        double sdt = dT?.Spacing > 0 ? dT.Spacing : 0.2;
        double ytd = yt - ((mu?.Dia ?? 8) + (dT?.Dia ?? 6)) / 2000.0;
        for (double x = 0.05; x <= lm - anch + 1e-9; x += sdt) { w.Dot(Lay.Thep, X(x), Y(ytd), Math.Max(0.15 * th, (dT?.Dia ?? 6) * k / 2)); w.Dot(Lay.Thep, X(span - x), Y(ytd), Math.Max(0.15 * th, (dT?.Dia ?? 6) * k / 2)); }
        double yLow = Y(-Math.Max(0.6, s.LandingBeamH)) - 2 * th;
        if (main != null) w.LeaderTag(X(span * 0.55), Y(yb), X(span * 0.55) + 2 * th, yLow, TagOf(main), main.Mark, 1);
        if (dB != null) w.LeaderTag(X(span * 0.3), Y(yb + (dm + dB.Dia) / 2000.0), X(span * 0.3) - 2 * th, yLow - 3 * th, TagOf(dB), dB.Mark, -1);
        if (mu != null)
        {
            w.LeaderTag(X((lm - anch) * 0.5), Y(yt), X((lm - anch) * 0.5) - 2 * th, Y(0) + 3 * th, TagOf(mu), mu.Mark, -1);
            w.LeaderTag(X(span - (lm - anch) * 0.5), Y(yt), X(span - (lm - anch) * 0.5) + 2 * th, Y(0) + 3 * th, TagOf(mu), mu.Mark, 1);
        }
        if (dT != null) w.LeaderTag(X(0.1), Y(ytd), X(0.1) + 3 * th, Y(0) + 6 * th, TagOf(dT), dT.Mark, 1);
        w.Dim(X(0), Y(-t), X(span), Y(-t), w.Extents.Y1 - 2 * th - Y(-t), k);
        w.Dim(X(span + 0.22), Y(-t), X(span + 0.22), Y(0), 2.5 * th, k);
        w.ElevMark(X(span * 0.8), Y(0), DxfWriter.ElevText(zL), 1);
        w.Title(X(span / 2), w.Extents.Y2 + 4 * th, $"CHI TIẾT {part.Name}" + (part.Quantity > 1 ? $" (SL={part.Quantity})" : ""), DrawUtil.ScaleText(S / k));
        return w;
    }

    // ==================================================================
    // MẶT CẮT DẦM CHIẾU NGHỈ / DẦM CỐN
    // ==================================================================
    private static DxfWriter BeamSection(Project p, StairPart part, double S)
    {
        var w = new DxfWriter(S);
        double th = w.Th, k = BeamK(S);   // mặt cắt dầm theo Thiết lập bản vẽ (mặc định 1/25)
        double c = Math.Max(0.02, p.Concrete.CoverBeam);
        double B = part.B * 1000 * k, H = part.H * 1000 * k;
        w.Rect(Lay.BeTong, 0, 0, B, H);
        var bot = part.Bars.Where(b => b.Role == "Dưới").ToList();
        var top = part.Bars.Where(b => b.Role == "Trên").ToList();
        var st = part.Bars.FirstOrDefault(b => b.Role == "Đai");
        double cc = c * 1000 * k;
        int sdia = st?.Dia ?? 6;
        w.Rect(Lay.Dai, cc - sdia * k, cc - sdia * k, B - cc + sdia * k, H - cc + sdia * k);
        void Row(List<BarMark> bars, double y, bool upper)
        {
            int n = bars.Sum(b => b.Count);
            if (n == 0) return;
            var dias = bars.SelectMany(b => Enumerable.Repeat(b.Dia, b.Count)).ToList();
            double x0 = cc + dias[0] * k / 2, x1 = B - cc - dias[^1] * k / 2;
            var pts = new List<(double X, double Y)>();
            for (int i = 0; i < n; i++)
            {
                double x = n == 1 ? B / 2 : x0 + (x1 - x0) * i / (n - 1);
                w.Dot(Lay.Thep, x, y, dias[i] * k / 2);
                pts.Add((x, y));
            }
            if (pts.Count > 1) w.Line(Lay.KyHieu, pts[0].X, pts[0].Y, pts[^1].X, pts[^1].Y);
            double ky = upper ? H + 3 * th : -3 * th;
            int j = 0;
            foreach (var bm in bars)
            {
                w.LeaderTag(pts[Math.Min(j, pts.Count - 1)].X, y, B + 2 * th, ky + (upper ? 1 : -1) * j * 2.6 * th, DrawUtil.BarTag(bm.Count, bm.Dia), bm.Mark, 1);
                j += bm.Count;
            }
        }
        int db = bot.Count > 0 ? bot.Max(b => b.Dia) : 12, dt = top.Count > 0 ? top.Max(b => b.Dia) : 12;
        Row(bot, cc + db * k / 2, false);
        Row(top, H - cc - dt * k / 2, true);
        if (st != null) w.LeaderTag(B - cc + sdia * k / 2, H * 0.5, B + 3 * th, H * 0.5, DrawUtil.StirrupTag(st.Dia, st.Spacing), st.Mark, 1, false, false);
        w.Dim(0, 0, B, 0, -6 * th, k);
        w.Dim(0, 0, 0, H, 3 * th, k);
        string nm = part.Kind == "Dầm cốn" ? $"MẶT CẮT {part.Name}" : $"MẶT CẮT {part.Name}";
        w.Title(B / 2, w.Extents.Y2 + 4 * th, nm + (part.Quantity > 1 ? $" (SL={part.Quantity}; L={DesignUtil.F(part.Length * 1000, "0")})" : $" (L={DesignUtil.F(part.Length * 1000, "0")})"), DrawUtil.ScaleText(S / k));
        return w;
    }

    // ==================================================================
    // GHI CHÚ CHUNG (một khối)
    // ==================================================================
    private static DxfWriter Notes(Project p, StairDesign sd, StairLayout L, double S)
    {
        var w = new DxfWriter(S);
        double th = w.Th;
        var s = L.S;
        var cs = p.Concrete;
        string steps = s.Steps == StepConstruction.Brick ? $"Bậc xây gạch đặc vữa {p.QuantitySettings?.MasonryMortar ?? "M75"}" : "Bậc đổ BTCT liền khối với bản thang";
        string fin = s.Finish == StepFinish.None ? "" : $", ốp {Stair.FinishName(s.Finish).ToLowerInvariant()} dày {DesignUtil.F(s.FinishT * 1000, "0")} mm";
        var lines = new List<string>
        {
            $"1. Bê tông {cs.ConcreteSlab}; thép Ø<10 {cs.RebarStirrup}, Ø≥10 {cs.RebarMain}; lớp bảo vệ bản {DesignUtil.F(cs.CoverSlab * 1000, "0")} mm, dầm {DesignUtil.F(cs.CoverBeam * 1000, "0")} mm.",
            $"2. {steps}{fin}. {L.NRisers} bậc h = {DesignUtil.F(L.Riser * 1000, "0")}, b = {DesignUtil.F(L.Tread * 1000, "0")} mm (2h + b = {DesignUtil.F(L.Check2hb * 1000, "0")}).",
            "3. Thép bản thang chạy suốt, không nối trong nhịp; thép dưới neo vào dầm ≥ 150 mm; thép mũ dài L0/4 từ mép gối."
        };
        // khối "GHI CHÚ CHUNG" dùng chung (GeneralNotes.cs – Thiết lập bản vẽ): một khối mỗi bản vẽ, tắt được; tắt → mảnh hình rỗng (bị bỏ qua khi ghép tờ)
        _ = th;
        return GeneralNotes.Figure(lines, S) ?? w;
    }

    // ==================================================================
    // BẢNG THỐNG KÊ CỐT THÉP CẦU THANG (mm giấy × S)
    // ==================================================================
    private static readonly string[] Heads =
    {
        "TÊN CẤU KIỆN", "SỐ HIỆU", "HÌNH DẠNG & KÍCH THƯỚC (mm)", "Ø (mm)", "CHIỀU DÀI 1 THANH (mm)",
        "SỐ C.KIỆN", "SỐ THANH", "TỔNG CHIỀU DÀI (m)", "TỔNG TRỌNG LƯỢNG (kg)"
    };
    private static readonly double[] ColW = { 28, 14, 62, 13, 22, 15, 30, 22, 24 };

    public static DxfWriter ScheduleTable(List<ScheduleRow> rows, double S, string title)
    {
        var w = new DxfWriter(S);
        double th = w.ScheduleTh, u = S;
        double rowH = 12 * u, headH = 16 * u;
        var cx = new double[ColW.Length + 1];
        for (int i = 0; i < ColW.Length; i++) cx[i + 1] = cx[i] + ColW[i] * u;
        double W = cx[^1];
        w.Text(Lay.Chu, W / 2, 3 * u, 1.4 * th, title, 0, 1, 0);
        double yh = -headH;
        w.Line(Lay.KhungTen, 0, 0, W, 0);
        w.Line(Lay.KhungTen, 0, yh, W, yh);
        for (int i = 0; i < Heads.Length; i++)
        {
            double xm = (cx[i] + cx[i + 1]) / 2;
            if (i == 6)
            {
                double ym = -headH / 2, cm = xm;
                w.Line(Lay.KhungTen, cx[i], ym, cx[i + 1], ym);
                w.Line(Lay.KhungTen, cm, ym, cm, yh);
                w.Text(Lay.Chu, xm, ym + headH / 4, 0.8 * th, "SỐ THANH", 0, 1, 2);
                w.Text(Lay.Chu, (cx[i] + cm) / 2, yh + headH / 4, 0.75 * th, "MỘT CK", 0, 1, 2);
                w.Text(Lay.Chu, (cm + cx[i + 1]) / 2, yh + headH / 4, 0.75 * th, "TOÀN BỘ", 0, 1, 2);
            }
            else Wrap(w, xm, -headH / 2, ColW[i] * u, Heads[i], 0.75 * th);
        }
        double yr = yh;
        foreach (var g in rows.GroupBy(r => r.Member))
        {
            double top = yr;
            foreach (var r in g)
            {
                double y2 = yr - rowH, ym = (yr + y2) / 2;
                w.Line(Lay.KhungTen, cx[1], y2, W, y2);
                w.MarkCircle((cx[1] + cx[2]) / 2, ym, r.Mark);
                Shape(w, r, cx[2] + 3 * u, cx[3] - 3 * u, y2 + 2 * u, yr - 2 * u);
                w.Text(Lay.Chu, (cx[3] + cx[4]) / 2, ym, th, r.Dia.ToString(DrawUtil.Inv), 0, 1, 2);
                w.Text(Lay.Chu, (cx[4] + cx[5]) / 2, ym, th, DxfWriter.N0(r.LengthMm), 0, 1, 2);
                w.Text(Lay.Chu, (cx[5] + cx[6]) / 2, ym, th, r.MemberCount.ToString(DrawUtil.Inv), 0, 1, 2);
                double cm = (cx[6] + cx[7]) / 2;
                w.Text(Lay.Chu, (cx[6] + cm) / 2, ym, th, r.BarsPerMember.ToString(DrawUtil.Inv), 0, 1, 2);
                w.Text(Lay.Chu, (cm + cx[7]) / 2, ym, th, r.BarsTotal.ToString(DrawUtil.Inv), 0, 1, 2);
                w.Text(Lay.Chu, (cx[7] + cx[8]) / 2, ym, th, r.TotalLengthM.ToString("0.00", DrawUtil.Inv), 0, 1, 2);
                w.Text(Lay.Chu, (cx[8] + cx[9]) / 2, ym, th, r.WeightKg.ToString("0.00", DrawUtil.Inv), 0, 1, 2);
                yr = y2;
            }
            w.Line(Lay.KhungTen, 0, yr, cx[1], yr);
            Wrap(w, (cx[0] + cx[1]) / 2, (top + yr) / 2, ColW[0] * u, g.Key, 0.85 * th);
        }
        double yt = yr - rowH;
        w.Line(Lay.KhungTen, 0, yt, W, yt);
        w.Text(Lay.Chu, cx[1] + 2 * u, (yr + yt) / 2, th, "TỔNG CỘNG", 0, 0, 2);
        w.Text(Lay.Chu, (cx[8] + cx[9]) / 2, (yr + yt) / 2, th, rows.Sum(r => r.WeightKg).ToString("0.00", DrawUtil.Inv), 0, 1, 2);
        for (int i = 0; i < cx.Length; i++)
        {
            bool full = i == 0 || i == cx.Length - 1 || i >= 8;
            w.Line(Lay.KhungTen, cx[i], 0, cx[i], full ? yt : yr);
        }
        double cmid = (cx[6] + cx[7]) / 2;
        w.Line(Lay.KhungTen, cmid, yh, cmid, yr);
        // tổng hợp theo đường kính + nhóm
        var byDia = RebarSchedule.ByDiameter(rows);
        var grp = RebarSchedule.Groups(rows);
        double y0 = yt - 6 * u, c0 = 40 * u, cw = 22 * u, rh = 9 * u;
        w.Text(Lay.Chu, 0, y0 + 2 * u, th, "TỔNG HỢP CỐT THÉP", 0, 0, 0);
        string[] lab = { "Đường kính (mm)", "Chiều dài (m)", "Trọng lượng (kg)" };
        int nc = Math.Max(1, byDia.Count);
        for (int r = 0; r <= 3; r++) w.Line(Lay.KhungTen, 0, y0 - r * rh, c0 + nc * cw, y0 - r * rh);
        for (int i = 0; i <= nc; i++) w.Line(Lay.KhungTen, c0 + i * cw, y0, c0 + i * cw, y0 - 3 * rh);
        w.Line(Lay.KhungTen, 0, y0, 0, y0 - 3 * rh);
        for (int r = 0; r < 3; r++) w.Text(Lay.Chu, 2 * u, y0 - (r + 0.5) * rh, th, lab[r], 0, 0, 2);
        for (int i = 0; i < byDia.Count; i++)
        {
            double xm = c0 + (i + 0.5) * cw;
            w.Text(Lay.Chu, xm, y0 - 0.5 * rh, th, "Ø" + byDia[i].Dia, 0, 1, 2);
            w.Text(Lay.Chu, xm, y0 - 1.5 * rh, th, byDia[i].LengthM.ToString("0.00", DrawUtil.Inv), 0, 1, 2);
            w.Text(Lay.Chu, xm, y0 - 2.5 * rh, th, byDia[i].WeightKg.ToString("0.00", DrawUtil.Inv), 0, 1, 2);
        }
        double yg = y0 - 3 * rh - 4 * u;
        string[] gl = { "NHÓM Ø≤10 (kg)", "NHÓM 10<Ø≤18 (kg)", "NHÓM Ø>18 (kg)", "TỔNG CỘNG (kg)" };
        double[] gv = { grp.WeightLe10, grp.Weight10To18, grp.WeightGt18, grp.Total };
        for (int r = 0; r <= gl.Length; r++) w.Line(Lay.KhungTen, 0, yg - r * rh, 75 * u, yg - r * rh);
        foreach (double xx in new[] { 0.0, 45 * u, 75 * u }) w.Line(Lay.KhungTen, xx, yg, xx, yg - gl.Length * rh);
        for (int r = 0; r < gl.Length; r++)
        {
            w.Text(Lay.Chu, 2 * u, yg - (r + 0.5) * rh, th, gl[r], 0, 0, 2);
            w.Text(Lay.Chu, 60 * u, yg - (r + 0.5) * rh, th, gv[r].ToString("0.00", DrawUtil.Inv), 0, 1, 2);
        }
        return w;
    }

    private static void Wrap(DxfWriter w, double cx, double cy, double width, string text, double h)
    {
        var words = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        string cur = "";
        foreach (var wd in words)
        {
            string t = cur.Length == 0 ? wd : cur + " " + wd;
            if (DxfWriter.TextWidth(t, h) > width * 0.92 && cur.Length > 0) { lines.Add(cur); cur = wd; }
            else cur = t;
        }
        if (cur.Length > 0) lines.Add(cur);
        double lh = 1.35 * h, y0 = cy + (lines.Count - 1) * lh / 2;
        for (int i = 0; i < lines.Count; i++) w.Text(Lay.Chu, cx, y0 - i * lh, h, lines[i], 0, 1, 2);
    }

    private static void Shape(DxfWriter w, ScheduleRow r, double x1, double x2, double y1, double y2)
    {
        double th = 0.8 * w.ScheduleTh;
        var seg = r.Segments ?? new List<double>();
        string sh = r.Shape ?? "";
        double xm = (x1 + x2) / 2, ym = (y1 + y2) / 2, hh = y2 - y1, yb = ym - 0.15 * hh, hk = 0.45 * hh;
        if (sh.StartsWith("Đai"))
        {
            double a = seg.Count > 0 ? seg[0] : 170, b = seg.Count > 1 ? seg[1] : 250;
            double bw = Math.Min((x2 - x1) * 0.35, hh * 1.2), bh = hh * 0.7, bx = xm - bw / 2, by = ym - bh / 2;
            w.Rect(Lay.Thep, bx, by, bx + bw, by + bh);
            double k = 0.3 * bh;
            w.Line(Lay.Thep, bx, by + bh - 0.15 * k, bx + 0.7 * k, by + bh - 0.85 * k);
            w.Text(Lay.Chu, xm, by - 0.3 * th, th, DxfWriter.N0(a), 0, 1, 3);
            w.Text(Lay.Chu, bx + bw + 0.4 * th, ym, th, DxfWriter.N0(b), 0, 0, 2);
            return;
        }
        if (sh.StartsWith("L") && seg.Count >= 2)
        {
            double xa = x1 + 0.12 * (x2 - x1), xb = x2 - 0.04 * (x2 - x1);
            w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xa, yb - hk), (xa, yb), (xb, yb) }, false);
            w.Text(Lay.Chu, xa - 0.3 * th, yb - hk / 2, th, DxfWriter.N0(seg[0]), 0, 2, 2);
            w.Text(Lay.Chu, (xa + xb) / 2, yb + 0.3 * th, th, DxfWriter.N0(seg[^1]), 0, 1, 1);
            return;
        }
        w.Line(Lay.Thep, x1 + 0.05 * (x2 - x1), yb, x2 - 0.05 * (x2 - x1), yb);
        w.Text(Lay.Chu, xm, yb + 0.3 * th, th, DxfWriter.N0(seg.Count > 0 ? seg.Sum() : r.LengthMm), 0, 1, 1);
    }
}

/// <summary>Hàng thống kê cốt thép cầu thang (Category = "Cầu thang"). Gộp vào bảng chung bằng một dòng trong RebarSchedule.BuildRows:
/// <c>StairSchedule.AppendTo(rows, s);</c> (gọi nhiều lần không bị trùng).</summary>
public static class StairSchedule
{
    public const string Category = "Cầu thang";

    public static List<ScheduleRow> RowsOf(StairDesign sd)
    {
        var rows = new List<ScheduleRow>();
        if (sd == null) return rows;
        foreach (var m in sd.Parts)
        {
            int q = Math.Max(1, m.Quantity);
            foreach (var g in m.Bars.Where(x => x.Count > 0 && x.Length > 0 && x.Dia > 0).Select((x, i) => (Bar: x, Key: x.Mark > 0 ? x.Mark : -1 - i))
                         .GroupBy(t => t.Key).OrderBy(t => t.Key))
            {
                var b = g.First().Bar;
                int cnt = g.Sum(t => t.Bar.Count);
                double lmm = Math.Round(b.Length * 100) * 10;
                int tot = cnt * q;
                double tl = tot * lmm / 1000.0;
                rows.Add(new ScheduleRow
                {
                    Member = m.Name, Category = Category, Story = sd.Story, Mark = b.Mark, Role = b.Role, Shape = b.Shape,
                    ShapeText = RebarSchedule.ShapeText(b), Segments = b.Segments?.ToList() ?? new List<double>(),
                    Dia = b.Dia, LengthMm = lmm, MemberCount = q, BarsPerMember = cnt, BarsTotal = tot,
                    TotalLengthM = Math.Round(tl, 2), WeightKg = Math.Round(RebarSchedule.UnitWeight(b.Dia) * tl, 2)
                });
            }
        }
        return rows;
    }

    public static List<ScheduleRow> Rows(DesignSummary s) =>
        s?.Stairs == null ? new List<ScheduleRow>() : s.Stairs.SelectMany(x => RowsOf(x)).ToList();

    /// <summary>Thêm hàng thép cầu thang vào danh sách (bỏ qua nếu đã có hàng loại "Cầu thang").</summary>
    public static void AppendTo(List<ScheduleRow> rows, DesignSummary s)
    {
        if (rows == null || s?.Stairs == null || s.Stairs.Count == 0) return;
        if (rows.Any(r => r.Category == Category)) return;
        rows.AddRange(Rows(s));
    }
}

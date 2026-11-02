using System.Globalization;
using System.Text;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Types.Units;
using CSMath;
using KetCauVXT.Dwg;
using KetCauVXT.Import;
using KetCauVXT.Import.Arch;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Nhập bản vẽ kiến trúc – hình học không trực giao: tường xiên 45°, tường cong (2 cung đồng tâm), ELLIPSE / SPLINE trên layer sàn,
/// cột tròn, cột chữ nhật xoay, DXF nhị phân; triển khai kết cấu: dầm xiên, dầm cong dưới tường cong, tải tường cong theo chiều dài cung.
/// Bản vẽ DXF dựng ngay trong bộ kiểm thử (đơn vị mm), đọc bằng DxfReader.FromLines (không ghi file *.dxf vào outDir).</summary>
public static class ArchGeometryTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static int B(bool b) => b ? 1 : 0;
    private static bool Near(double a, double b, double tol = 1e-6) => Math.Abs(a - b) <= tol;

    /// <summary>Bộ dựng nội dung DXF ASCII đơn giản.</summary>
    private sealed class Dxf
    {
        private readonly List<string> _l = new();
        public void P(int code, object v)
        {
            _l.Add(code.ToString(Inv));
            _l.Add(v is double d ? d.ToString("R", Inv) : v is int i ? i.ToString(Inv) : Convert.ToString(v, Inv));
        }
        public void Begin()
        {
            P(0, "SECTION"); P(2, "HEADER"); P(9, "$ACADVER"); P(1, "AC1027"); P(9, "$INSUNITS"); P(70, 4); P(0, "ENDSEC");
            P(0, "SECTION"); P(2, "ENTITIES");
        }
        public string[] End() { P(0, "ENDSEC"); P(0, "EOF"); return _l.ToArray(); }
        public void Line(string layer, double x1, double y1, double x2, double y2)
        { P(0, "LINE"); P(8, layer); P(10, x1); P(20, y1); P(30, 0.0); P(11, x2); P(21, y2); P(31, 0.0); }
        public void Arc(string layer, double cx, double cy, double r, double a1, double a2)
        { P(0, "ARC"); P(8, layer); P(10, cx); P(20, cy); P(30, 0.0); P(40, r); P(50, a1); P(51, a2); }
        public void Circle(string layer, double cx, double cy, double r)
        { P(0, "CIRCLE"); P(8, layer); P(10, cx); P(20, cy); P(30, 0.0); P(40, r); }
        public void Text(string layer, double x, double y, double h, string s)
        { P(0, "TEXT"); P(8, layer); P(10, x); P(20, y); P(30, 0.0); P(40, h); P(1, s); }
        public void LwPoly(string layer, bool closed, params (double X, double Y, double Bulge)[] v)
        {
            P(0, "LWPOLYLINE"); P(8, layer); P(90, v.Length); P(70, closed ? 1 : 0);
            foreach (var (x, y, bu) in v) { P(10, x); P(20, y); if (Math.Abs(bu) > 0) P(42, bu); }
        }
        public void Ellipse(string layer, double cx, double cy, double mx, double my, double ratio, double t0, double t1)
        { P(0, "ELLIPSE"); P(8, layer); P(10, cx); P(20, cy); P(30, 0.0); P(11, mx); P(21, my); P(31, 0.0); P(40, ratio); P(41, t0); P(42, t1); }
        public void Spline(string layer, int degree, bool closed, double[] knots, (double X, double Y)[] ctrl)
        {
            P(0, "SPLINE"); P(8, layer); P(70, (closed ? 1 : 0) | 8); P(71, degree); P(72, knots.Length); P(73, ctrl.Length); P(74, 0);
            foreach (var k in knots) P(40, k);
            foreach (var (x, y) in ctrl) { P(10, x); P(20, y); P(30, 0.0); }
        }
        public void SplineFit(string layer, (double X, double Y)[] fit)
        {
            P(0, "SPLINE"); P(8, layer); P(70, 8); P(71, 3); P(72, 0); P(73, 0); P(74, fit.Length);
            foreach (var (x, y) in fit) { P(11, x); P(21, y); P(31, 0.0); }
        }
    }

    private static readonly (double X, double Y)[] OpenCtrl = { (0, 0), (1000, 2000), (2000, -1000), (3000, 1500), (4000, 0) };
    private static readonly (double X, double Y)[] LoopCtrl = { (1000, 1000), (3000, 1000), (3000, 3000), (1000, 3000), (1000, 1000) };
    private static readonly double[] Knots9 = { 0, 0, 0, 0, 0.5, 1, 1, 1, 1 };

    /// <summary>Một mặt bằng 8×6 m (lưới trục 1-2-3 cách 4 m, A-B cách 6 m), tường bao 220 theo tim trục,
    /// tường 110 xiên 45° từ (4;0) tới (8;4), tường cong 220 tim R = 1,5 m tâm (2;6) (nửa vòng nhô ra mặt sau),
    /// cột xoay 30° (300×220) tại (8;6), cột tròn D300 tại (0;6). Tầng 1 thêm ELLIPSE và SPLINE kín trên layer sàn.</summary>
    private static void Floor(Dxf d, double ox, string title, bool extras)
    {
        const string W = "KT-TUONG", G = "KT-TRUC", C = "KT-COT", S = "KT-SAN";
        // tường bao 220
        d.Line(W, ox - 110, -110, ox + 8110, -110); d.Line(W, ox + 110, 110, ox + 7890, 110);
        d.Line(W, ox + 110, 5890, ox + 7890, 5890); d.Line(W, ox - 110, 6110, ox + 8110, 6110);
        d.Line(W, ox - 110, -110, ox - 110, 6110); d.Line(W, ox + 110, 110, ox + 110, 5890);
        d.Line(W, ox + 7890, 110, ox + 7890, 5890); d.Line(W, ox + 8110, -110, ox + 8110, 6110);
        // tường xiên 45°, dày 110: tim (4000;0) → (8000;4000), hai nét lệch ±55 mm theo pháp tuyến
        double o = 55 / Math.Sqrt(2);
        d.Line(W, ox + 4000 - o, o, ox + 8000 - o, 4000 + o);
        d.Line(W, ox + 4000 + o, -o, ox + 8000 + o, 4000 - o);
        // tường cong dày 220: 2 cung đồng tâm R 1390 / 1610, 0° → 180°
        d.Arc(W, ox + 2000, 6000, 1390, 0, 180);
        d.Arc(W, ox + 2000, 6000, 1610, 0, 180);
        // lưới trục + tên trục
        foreach (var (x, n) in new[] { (0.0, "1"), (4000.0, "2"), (8000.0, "3") })
        { d.Line(G, ox + x, -1500, ox + x, 9000); d.Text(G, ox + x, 9400, 350, n); }
        foreach (var (y, n) in new[] { (0.0, "A"), (6000.0, "B") })
        { d.Line(G, ox - 1500, y, ox + 9500, y); d.Text(G, ox - 1900, y, 350, n); }
        d.Text("KT-TENHINH", ox + 4000, -2600, 300, title);
        if (!extras) return;
        // cột chữ nhật 300×220 xoay 30° tại (8000;6000)
        double a = 30 * Math.PI / 180, ux = Math.Cos(a), uy = Math.Sin(a), nx = -uy, ny = ux, hb = 150, hh = 110;
        (double, double, double) Pt(double su, double sn) => (ox + 8000 + su * hb * ux + sn * hh * nx, 6000 + su * hb * uy + sn * hh * ny, 0);
        d.LwPoly(C, true, Pt(-1, -1), Pt(1, -1), Pt(1, 1), Pt(-1, 1));
        // cột tròn D300 tại (0;6000)
        d.Circle(C, ox, 6000, 150);
        // vùng sàn: ELLIPSE kín (a = 1,5 m, b = 0,75 m) + SPLINE kín
        d.Ellipse(S, ox + 5500, 3000, 1500, 0, 0.5, 0, 2 * Math.PI);
        d.Spline(S, 3, true, Knots9, LoopCtrl.Select(p => (ox + p.X, p.Y)).ToArray());
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ------------------------------------------------------------ 1. DxfReader: ELLIPSE, SPLINE, cung (ARC, bulge), DXF nhị phân
        {
            var d = new Dxf();
            d.Begin();
            d.Ellipse("E1", 0, 0, 1500, 0, 0.5, 0, 2 * Math.PI);
            d.Ellipse("E2", 0, 0, 0, 2000, 0.25, 0, Math.PI);
            d.Spline("S1", 3, false, Knots9, OpenCtrl);
            d.Spline("S2", 3, true, Knots9, LoopCtrl);
            d.SplineFit("S3", new[] { (0.0, 0.0), (1000.0, 1000.0), (2000.0, 0.0), (3000.0, 1000.0) });
            d.Spline("S4", 3, false, new double[] { 0, 1, 2 }, OpenCtrl);   // vectơ nút sai số lượng → nút đều kẹp hai đầu
            d.Arc("A1", 1000, 2000, 500, 0, 90);
            d.LwPoly("B1", false, (0, 0, 1), (2000, 0, 0));
            var r = DxfReader.FromLines(d.End());
            DxfEntity On(string layer) => r.Entities.FirstOrDefault(e => e.Layer == layer);
            var e1 = On("E1");
            check("DXF ELLIPSE kín → đa tuyến kín 36 điểm (bước 10°)", e1 != null && e1.Closed && e1.SourceType == "ELLIPSE" ? e1.Points.Count : -1, 36, 0);
            double area = e1 == null ? 0 : Math.Abs(ImportGeometry.SignedArea(e1.Points));
            check("DXF ELLIPSE: diện tích ≈ π·a·b = π·1,5·0,75 m² (sai số chia đoạn < 1%)", area, Math.PI * 1.5 * 0.75, 0.01);
            check("DXF ELLIPSE: điểm đầu = đầu trục lớn (1,5; 0) m", B(e1 != null && Near(e1.Points[0].X, 1.5) && Near(e1.Points[0].Y, 0)), 1, 0);
            var e2 = On("E2");
            check("DXF nửa ELLIPSE (0 → π) → 19 điểm, hở", e2 != null && !e2.Closed ? e2.Points.Count : -1, 19, 0);
            check("DXF nửa ELLIPSE trục lớn theo Y: điểm giữa (−0,5; 0) m (trục nhỏ 0,25·2)", B(e2 != null && e2.Points.Count == 19 && Near(e2.Points[9].X, -0.5, 1e-9) && Near(e2.Points[9].Y, 0, 1e-9)), 1, 0);
            var s1 = On("S1");
            check("DXF SPLINE bậc 3, 5 điểm điều khiển, 2 nhịp nút → 17 điểm, hở", s1 != null && !s1.Closed ? s1.Points.Count : -1, 17, 0);
            check("DXF SPLINE kẹp hai đầu: điểm đầu/cuối = điểm điều khiển đầu/cuối",
                B(s1 != null && Near(s1.Points[0].X, 0) && Near(s1.Points[0].Y, 0) && Near(s1.Points[^1].X, 4.0, 1e-9) && Near(s1.Points[^1].Y, 0, 1e-9)), 1, 0);
            var s2 = On("S2");
            check("DXF SPLINE kín (cờ 70 = 1, điểm đầu = cuối) → 16 điểm, kín", s2 != null && s2.Closed ? s2.Points.Count : -1, 16, 0);
            var s3 = On("S3");
            check("DXF SPLINE chỉ có điểm khớp (4 điểm) → Catmull-Rom 25 điểm qua các điểm khớp",
                s3 != null && Near(s3.Points[8].X, 1.0, 1e-9) && Near(s3.Points[8].Y, 1.0, 1e-9) ? s3.Points.Count : -1, 25, 0);
            var s4 = On("S4");
            check("DXF SPLINE vectơ nút sai → dùng nút đều kẹp hai đầu (17 điểm)", s4?.Points.Count ?? -1, 17, 0);
            var a1 = On("A1");
            check("DXF ARC giữ tâm/bán kính cung (DxfEntity.Arcs): R = 0,5 m", a1?.Arcs?.Count == 1 && Near(a1.Arcs[0].Cx, 1.0) && Near(a1.Arcs[0].Cy, 2.0) ? a1.Arcs[0].R : -1, 0.5, 1e-9);
            var b1 = On("B1");
            check("DXF LWPOLYLINE cung lồi (bulge 1): cung tâm (1; 0), R = 1 m, điểm 0 … 12",
                b1?.Arcs?.Count == 1 && Near(b1.Arcs[0].Cx, 1.0, 1e-9) && Near(b1.Arcs[0].Cy, 0, 1e-9) && b1.Arcs[0].I0 == 0 && b1.Arcs[0].I1 == 12 ? b1.Arcs[0].R : -1, 1.0, 1e-9);

            // DXF nhị phân
            var bin = Encoding.ASCII.GetBytes("AutoCAD Binary DXF\r\n\u001a\0").Concat(new byte[] { 0, 0, (byte)'S', (byte)'E', 0 }).ToArray();
            var rb = DxfReader.FromBytes(bin);
            check("DXF nhị phân: không đọc thực thể, báo \"Lưu lại dạng DXF ASCII\"", B(rb.Entities.Count == 0 && rb.Warnings.Any(w => w.Contains("Lưu lại dạng DXF ASCII"))), 1, 0);
            string binPath = Path.Combine(Path.GetTempPath(), "KetCauVXT_BinaryDxf_" + Guid.NewGuid().ToString("N") + ".dxf");
            try
            {
                File.WriteAllBytes(binPath, bin);
                var dr = ArchPlanReader.Read(binPath, new ArchImportOptions());
                check("Nhập KT từ DXF nhị phân: cảnh báo rõ ràng (tiếng Việt)", B(dr.Floors.Count == 0 && dr.Warnings.Any(w => w.Contains("Binary DXF") && w.Contains("DXF ASCII"))), 1, 0);
            }
            finally { try { File.Delete(binPath); } catch (IOException) { } }
        }

        // ------------------------------------------------------------ 2. DWG (ACadSharp): ELLIPSE, SPLINE → đa tuyến
        {
            string tmp = Path.Combine(Path.GetTempPath(), "KetCauVXT_ArchGeom");
            Directory.CreateDirectory(tmp);
            var doc = new CadDocument();
            doc.Header.InsUnits = UnitsType.Millimeters;
            doc.Entities.Add(new Ellipse { Center = new XYZ(0, 0, 0), MajorAxisEndPoint = new XYZ(1500, 0, 0), RadiusRatio = 0.5 });
            var sp = new Spline { Degree = 3 };
            foreach (var (x, y) in OpenCtrl) sp.ControlPoints.Add(new XYZ(x, y, 0));
            foreach (var k in Knots9) sp.Knots.Add(k);
            doc.Entities.Add(sp);
            string dwg = Path.Combine(tmp, "ElipSpline.dwg");
            bool written = false;
            try { DwgReader.WriteDwg(doc, dwg); written = true; }
            catch (Exception ex) { Console.WriteLine("   ! Không ghi được DWG có SPLINE bằng ACadSharp: " + ex.Message); }
            if (!written)
            {
                // ghi lại chỉ với ELLIPSE
                doc = new CadDocument();
                doc.Header.InsUnits = UnitsType.Millimeters;
                doc.Entities.Add(new Ellipse { Center = new XYZ(0, 0, 0), MajorAxisEndPoint = new XYZ(1500, 0, 0), RadiusRatio = 0.5 });
                DwgReader.WriteDwg(doc, dwg);
            }
            var rd = DwgReader.Read(dwg);
            var el = rd.Entities.FirstOrDefault(e => e.SourceType == "ELLIPSE");
            check("DWG ELLIPSE kín → đa tuyến kín 36 điểm", el != null && el.Closed ? el.Points.Count : -1, 36, 0);
            check("DWG ELLIPSE: đầu trục lớn (1,5; 0) m", B(el != null && Near(el.Points[0].X, 1.5, 1e-6) && Near(el.Points[0].Y, 0, 1e-6)), 1, 0);
            var spl = rd.Entities.FirstOrDefault(e => e.SourceType == "SPLINE");
            if (written && spl != null)
                check("DWG SPLINE bậc 3, 5 điểm điều khiển → 17 điểm", spl.Points.Count, 17, 0);
            else Console.WriteLine("   ! DWG: không có SPLINE sau khi ghi/đọc lại (giới hạn bộ ghi ACadSharp) – bỏ qua kiểm tra SPLINE DWG.");
        }

        // ------------------------------------------------------------ 3. Nhận diện mặt bằng: tường xiên, tường cong, cột xoay, cột tròn, sàn elip/spline
        var dx = new Dxf();
        dx.Begin();
        Floor(dx, 0, "MẶT BẰNG TẦNG 1", true);
        Floor(dx, 20000, "MẶT BẰNG TẦNG 2", false);
        var rr = DxfReader.FromLines(dx.End());
        var o = new ArchImportOptions();
        var dwgArch = ArchPlanReader.FromEntities(rr.Entities, rr.FillEntities, o, "HinhHocKhongTrucGiao");
        foreach (var line in dwgArch.Info) Console.WriteLine("   · " + line);
        foreach (var w in dwgArch.Warnings) Console.WriteLine("   ! " + w);
        foreach (var f in dwgArch.Floors) foreach (var n in f.Notes) Console.WriteLine($"   ! [{f.Title}] {n}");
        check("KT hình học: 2 mặt bằng (tầng 1, 2)", dwgArch.Floors.Count(f => f.FloorNo is 1 or 2), 2, 0);
        var f1 = dwgArch.Floors.FirstOrDefault(f => f.FloorNo == 1);
        var f2 = dwgArch.Floors.FirstOrDefault(f => f.FloorNo == 2);
        if (f1 == null || f2 == null) { check("KT hình học: thiếu mặt bằng – dừng", 0, 1, 0); return; }

        var dg = f1.Walls.Where(w => w.IsDiagonal).ToList();
        check("Tường xiên: nhận 1 đoạn", dg.Count, 1, 0);
        var wd = dg.FirstOrDefault();
        check("Tường xiên: góc 45°", wd?.AngleDeg ?? -1, 45, 1e-4);
        check("Tường xiên: chiều dày 110 mm", wd?.Thickness ?? -1, 0.11, 1e-6);
        check("Tường xiên: chiều dài 4√2 = 5,657 m", wd?.Length ?? -1, 4 * Math.Sqrt(2), 1e-3);
        check("Tường xiên: hai đầu (4; 0) và (8; 4)", B(wd != null && Near(Math.Min(wd.Ax, wd.Bx), 4, 1e-3) && Near(Math.Max(wd.Ax, wd.Bx), 8, 1e-3)
                                                     && Near(Math.Min(wd.Ay, wd.By), 0, 1e-3) && Near(Math.Max(wd.Ay, wd.By), 4, 1e-3)), 1, 0);
        var cw = f1.Walls.Where(w => w.IsCurved).ToList();
        check("Tường cong: nhận 1 đoạn (nửa vòng)", cw.Count, 1, 0);
        var wc = cw.FirstOrDefault();
        check("Tường cong: bán kính tim 1,5 m", wc?.Radius ?? -1, 1.5, 1e-4);
        check("Tường cong: chiều dày 220 mm", wc?.Thickness ?? -1, 0.22, 1e-6);
        check("Tường cong: chiều dài cung 1,5·π = 4,712 m", wc?.Length ?? -1, 1.5 * Math.PI, 1e-4);
        check("Tường cong: không lẫn vào tường song song trục", B(wc != null && !wc.AlongX && !wc.AlongY), 1, 0);
        check("Tường bao 220 vẫn nhận đủ 4 đoạn", f1.Walls.Count(w => w.IsOrthogonal && Near(w.Thickness, 0.22, 0.005)), 4, 0);
        check("Ghi chú nhận diện: tường xiên + tường cong", B(f1.Notes.Any(n => n.Contains("tường xiên")) && f1.Notes.Any(n => n.Contains("tường cong"))), 1, 0);

        var rot = f1.Columns.FirstOrDefault(c => Math.Abs(c.Angle) > 1e-6);
        check("Cột chữ nhật xoay: góc 30°", rot?.Angle ?? -1, 30, 1e-3);
        check("Cột xoay: kích thước theo trục riêng 300 × 220", B(rot != null && Near(rot.Bx, 0.30, 1e-3) && Near(rot.By, 0.22, 1e-3)), 1, 0);
        check("Cột xoay: tâm (8; 6)", B(rot != null && Near(rot.X, 8, 1e-3) && Near(rot.Y, 6, 1e-3)), 1, 0);
        var rnd = f1.Columns.FirstOrDefault(c => c.Diameter > 0);
        check("Cột tròn: đường kính 0,30 m", rnd?.Diameter ?? -1, 0.30, 1e-6);
        check("Cột tròn → cột vuông cùng diện tích: cạnh = D·√π/2 = 0,266 m", rnd?.Bx ?? -1, Math.Round(0.30 * Math.Sqrt(Math.PI) / 2, 3), 1e-6);
        check("Ghi chú nhận diện: cột xoay + cột tròn", B(f1.Notes.Any(n => n.Contains("cột xoay")) && f1.Notes.Any(n => n.Contains("cột tròn"))), 1, 0);

        var ell = f1.SlabRegions.FirstOrDefault(sr => sr.Points.Count == 36);
        check("Vùng sàn từ ELLIPSE kín (36 đỉnh)", B(ell != null && !ell.IsOpening), 1, 0);
        check("Vùng sàn ELLIPSE: diện tích ≈ π·1,5·0,75 m²", ell?.Area ?? -1, Math.PI * 1.5 * 0.75, 0.01);
        var spr = f1.SlabRegions.FirstOrDefault(sr => sr.Points.Count == 16);
        check("Vùng sàn từ SPLINE kín (16 đỉnh)", B(spr != null && spr.Area > 0.25), 1, 0);
        check("Ghi chú: ELLIPSE/SPLINE chia thành đa tuyến", B(f1.Notes.Any(n => n.Contains("ELLIPSE/SPLINE"))), 1, 0);
        check("Tầng 2 dời về hệ toạ độ tầng 1: tường cong tầng 2 tâm (2; 6)",
            B(f2.Walls.Any(w => w.IsCurved && Near(Math.Min(w.Ax, w.Bx), 0.5, 1e-3) && Near(Math.Max(w.Ax, w.Bx), 3.5, 1e-3) && Near(w.Ay, 6, 1e-3))), 1, 0);

        // ------------------------------------------------------------ 4. Triển khai kết cấu: dầm xiên, dầm cong, góc cột, tải tường cong
        var prop = ArchToStructure.Propose(dwgArch, o);
        foreach (var n in prop.Notes) Console.WriteLine("   · " + n);
        foreach (var w in prop.Warnings) Console.WriteLine("   ! " + w);
        var t2 = prop.Levels.FirstOrDefault(l => l.Name == "T2");
        check("Đề xuất: có cao trình T2", B(t2 != null), 1, 0);
        if (t2 == null) return;
        foreach (var b in t2.Beams.Where(b => Math.Abs(b.Sagitta) > 1e-6 || (Math.Abs(b.Ax - b.Bx) > 1e-6 && Math.Abs(b.Ay - b.By) > 1e-6)))
            Console.WriteLine($"   T2 {b.Role} ({b.Ax:0.00};{b.Ay:0.00})–({b.Bx:0.00};{b.By:0.00}) vồng {b.Sagitta:0.000} {b.Section} tường {b.WallLoad:0.0} kN/m {b.Note}");
        bool IsSeg(PropBeam b, double x1, double y1, double x2, double y2) =>
            (Near(b.Ax, x1, 0.01) && Near(b.Ay, y1, 0.01) && Near(b.Bx, x2, 0.01) && Near(b.By, y2, 0.01)) || (Near(b.Ax, x2, 0.01) && Near(b.Ay, y2, 0.01) && Near(b.Bx, x1, 0.01) && Near(b.By, y1, 0.01));
        var pd = t2.Beams.FirstOrDefault(b => Math.Abs(b.Sagitta) < 1e-9 && IsSeg(b, 4, 0, 8, 4));
        check("T2: dầm xiên (4;0)–(8;4) dưới tường xiên", B(pd != null), 1, 0);
        check("T2: dầm xiên mang tải tường > 0", B(pd != null && pd.WallLoad > 1), 1, 0);
        var pc = t2.Beams.FirstOrDefault(b => Math.Abs(b.Sagitta) > 1e-6);
        check("T2: dầm cong dưới tường cong, hai đầu (3,5;6) và (0,5;6)", B(pc != null && IsSeg(pc, 3.5, 6, 0.5, 6)), 1, 0);
        check("T2: độ vồng dầm cong |f| = 1,5 m (nửa vòng R = 1,5 m)", Math.Abs(pc?.Sagitta ?? 0), 1.5, 1e-3);
        var colRot = prop.Columns.FirstOrDefault(c => Near(c.X, 8, 0.01) && Near(c.Y, 6, 0.01));
        check("Đề xuất: cột tại (8;6) giữ góc xoay 30°", colRot?.Angle ?? -1, 30, 1e-3);

        var p = new Project();
        var rep = ArchToStructure.Apply(p, dwgArch, prop, o);
        Console.WriteLine("   " + rep.ToString().Replace("\n", "\n   "));
        var sT2 = p.Stories.FirstOrDefault(st => st.Name == "T2");
        double zT2 = sT2?.Elevation ?? double.NaN;
        var curvedFrames = p.Frames.Where(f => f.IsCurved && !f.IsVerticalKind).ToList();
        check("Mô hình: có dầm cong", B(curvedFrames.Count >= 1), 1, 0);
        var arc0 = curvedFrames.Count > 0 ? CurvedBeam.Arc(p, curvedFrames[0]) : null;
        check("Mô hình: dầm cong bán kính R = 1,5 m", arc0?.Radius ?? -1, 1.5, 1e-3);
        check("Mô hình: dầm cong tâm (2; 6)", B(arc0 != null && Near(arc0.Cx, 2, 1e-3) && Near(arc0.Cy, 6, 1e-3)), 1, 0);
        check("Mô hình: tổng chiều dài cung dầm cong 1,5·π", curvedFrames.Sum(f => p.Length(f)), 1.5 * Math.PI, 1e-3);
        bool Has(double x1, double y1, double x2, double y2) => p.Frames.Any(f =>
        {
            if (f.IsVerticalKind || f.IsCurved) return false;
            var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
            if (!Near(a.Z, zT2, 1e-3) || !Near(b.Z, zT2, 1e-3)) return false;
            return (Near(a.X, x1, 0.005) && Near(a.Y, y1, 0.005) && Near(b.X, x2, 0.005) && Near(b.Y, y2, 0.005)) || (Near(a.X, x2, 0.005) && Near(a.Y, y2, 0.005) && Near(b.X, x1, 0.005) && Near(b.Y, y1, 0.005));
        });
        check("Mô hình: dầm xiên 45° nối nút (4;0) – (8;4) tại T2", B(Has(4, 0, 8, 4)), 1, 0);
        check("Mô hình: dầm trục 3 được chia tại đầu dầm xiên (8;4)", B(Has(8, 0, 8, 4) && Has(8, 4, 8, 6)), 1, 0);
        check("Mô hình: dầm trục B được chia tại hai đầu dầm cong (0,5;6), (3,5;6)", B(Has(0.5, 6, 3.5, 6)), 1, 0);
        var colF = p.Frames.FirstOrDefault(f => f.IsVerticalKind && Near(p.Node(f.NodeI).X, 8, 0.01) && Near(p.Node(f.NodeI).Y, 6, 0.01));
        double ang = colF == null ? -1 : ((colF.Angle % 90) + 90) % 90;
        check("Mô hình: cột (8;6) có góc tiết diện ≡ 30° (mod 90°)", ang, 30, 1e-6);
        check("Mô hình: kiểm tra mô hình không lỗi dầm cong", B(!rep.Warnings.Any(w => w.Contains("độ vồng") || w.Contains("dầm cong phải"))), 1, 0);

        var wl = p.Walls.FirstOrDefault(w => w.IsCurved && w.Story == "T2");
        check("Tường cong khai báo thành WallLine cong (độ vồng ±1,5 m)", Math.Abs(wl?.Sagitta ?? 0), 1.5, 1e-3);
        check("WallLine cong: chiều dài theo cung 1,5·π", wl?.Length ?? -1, 1.5 * Math.PI, 1e-3);
        var warns = WallLoadGenerator.Apply(p);
        foreach (var w in warns) Console.WriteLine("   ! " + w);
        if (wl != null && curvedFrames.Count > 0)
        {
            var wt = p.WallTypes.First(t => t.Name == wl.Type);
            var stories = p.Stories.OrderBy(st => st.Elevation).ToList();
            int idx = stories.FindIndex(st => st.Name == wl.Story);
            double hBeam = curvedFrames.Select(f => p.Section(f.Section).Props(0.5).Depth).Max();
            double H = stories[idx + 1].Elevation - stories[idx].Elevation - Math.Max(hBeam, 0.35);
            double unit = wt.Thickness * wt.UnitWeight + wt.PlasterSides * wt.PlasterThickness * wt.PlasterUnitWeight;
            double expected = unit * H * (1.5 * Math.PI);
            double got = p.FrameLoads.Where(fl => fl.Source == WallLoadGenerator.WallSource && p.Frame(fl.Element).IsCurved).Sum(fl =>
            {
                double L = p.Length(p.Frame(fl.Element)), sm = 0;
                for (int i = 0; i + 1 < fl.Points.Count; i++) sm += (fl.Points[i][1] + fl.Points[i + 1][1]) / 2 * (fl.Points[i + 1][0] - fl.Points[i][0]) * L;
                return sm;
            });
            Console.WriteLine($"   Tải tường cong: (t·γ + trát) = {unit:0.###} kN/m², H = {H:0.###} m, L cung = {1.5 * Math.PI:0.###} m → {expected:0.###} kN; trên dầm cong {got:0.###} kN");
            check("Tải tường cong trên dầm cong = (t·γ + 2·tp·γp)·H·L cung", got, expected, 1e-6);
            var under = WallLoadGenerator.BeamsUnder(p, wl, stories[idx].Elevation, wt.Thickness);
            check("Tường cong: toàn bộ chiều dài cung có dầm cong đỡ (không quy tải về sàn)", under.Sum(u => u.len), 1.5 * Math.PI, 1e-6);
            // tường bao thẳng trục B (trùng dây cung dầm cong) không bị cộng tải lên dầm cong
            check("Tường thẳng trùng dây cung: không cộng tải lên dầm cong", p.Walls.Where(w => !w.IsCurved && w.Story == "T2" && Near(w.Y1, 6, 0.01) && Near(w.Y2, 6, 0.01))
                .Sum(w => WallLoadGenerator.BeamsUnder(p, w, stories[idx].Elevation, 0.22).Count(u => u.f.IsCurved)), 0, 0);
        }
    }
}

using System.Text.Json;
using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>[Bảng tra] Chỉ tiêu cơ lý đất theo bảng tra TCVN 9362:2012 (c, φ, E theo e, IL; R0; γ), nguồn chỉ tiêu (bảng tra / thí nghiệm, ktc),
/// danh mục đơn vị hành chính sau sắp xếp (34 tỉnh, 3.321 xã/phường), gán đài cọc giống nhau cho nhiều chân cột.</summary>
public static class SoilStandardTests
{
    private static double B(bool x) => x ? 1 : 0;

    public static void Run(Action<string, double, double, double> check)
    {
        Tables(check);
        Layers(check);
        Admin(check);
        PileCopy(check);
    }

    private static void Tables(Action<string, double, double, double> check)
    {
        var r = SoilStandard.Lookup(SoilKind.Sand, SandGrain.Fine, 0.65, -1, 0.7);
        check("Cát hạt nhỏ e = 0,65: c = 2 kPa", r.C, 2, 1e-9);
        check("Cát hạt nhỏ e = 0,65: φ = 32°", r.Phi, 32, 1e-9);
        check("Cát hạt nhỏ e = 0,65: E = 28 MPa", r.E, 28, 1e-9);
        check("Cát hạt nhỏ e = 0,65: chặt vừa → R0 = 300 kPa (ẩm)", r.R0, 300, 1e-9);
        var r2 = SoilStandard.Lookup(SoilKind.Sand, SandGrain.Fine, 0.60, -1, 0.7);
        check("Cát hạt nhỏ e = 0,60: nội suy c = 3", r2.C, 3, 1e-9);
        check("Cát hạt nhỏ e = 0,60: nội suy φ = 34", r2.Phi, 34, 1e-9);
        check("Cát hạt nhỏ e = 0,60: nội suy E = 33", r2.E, 33, 1e-9);
        var rs = SoilStandard.Lookup(SoilKind.Sand, SandGrain.Fine, 0.65, -1, 0.95);
        check("Cát hạt nhỏ bão hoà nước chặt vừa: R0 = 200 kPa", rs.R0, 200, 1e-9);
        var rm = SoilStandard.Lookup(SoilKind.Sand, SandGrain.Medium, 0.50, -1, 0.7);
        check("Cát hạt vừa chặt e = 0,5: φ = 39° (nội suy 40–38)", rm.Phi, 39, 1e-9);
        check("Cát hạt vừa chặt: R0 = 500 kPa", rm.R0, 500, 1e-9);
        var rc = SoilStandard.Lookup(SoilKind.Sand, SandGrain.Coarse, 0.45, -1, 0.7);
        check("Cát thô e = 0,45: φ = 43°, c = 2", rc.Phi * 10 + rc.C, 432, 1e-9);
        var rl = SoilStandard.Lookup(SoilKind.Sand, SandGrain.Fine, 0.80, -1, 0.7);
        check("Cát hạt nhỏ rời: không có R0", B(double.IsNaN(rl.R0)), 1, 0);
        check("Cát hạt nhỏ rời: có cảnh báo", B(rl.Warnings.Count > 0), 1, 0);
        var silt = SoilStandard.Lookup(SoilKind.Sand, SandGrain.Silty, 0.75, -1, 0.9);
        check("Cát bụi e = 0,75: c = 2, φ = 26, E = 11", silt.C * 10000 + silt.Phi * 100 + silt.E, 20000 + 2600 + 11, 1e-9);
        check("Cát bụi chặt vừa bão hoà: R0 = 100 kPa", silt.R0, 100, 1e-9);

        var lo = SoilStandard.Lookup(SoilKind.SiltyClay, SandGrain.Medium, 0.75, 0.38, 0.92);
        check("Sét pha dẻo cứng e = 0,75, IL = 0,38: c = 23 kPa", lo.C, 23, 1e-9);
        check("Sét pha dẻo cứng: φ = 21°", lo.Phi, 21, 1e-9);
        check("Sét pha dẻo cứng: E = 14 MPa", lo.E, 14, 1e-9);
        check("Sét pha e = 0,75, IL = 0,38: R0 nội suy theo e và IL", lo.R0, Math.Round(Rclay(0.75, 0.38, new[] { (0.5, 300.0, 250.0), (0.7, 250.0, 180.0), (1.0, 200.0, 100.0) })), 1e-9);
        check("Sét pha dẻo cứng: γ = 10·(2,70 + 0,92·0,75)/1,75", lo.Gamma, Math.Round(10 * (2.70 + 0.92 * 0.75) / 1.75, 2), 1e-9);
        check("Sét pha dẻo cứng: γsat = 10·(2,70 + 0,75)/1,75", lo.GammaSat, Math.Round(10 * (2.70 + 0.75) / 1.75, 2), 1e-9);
        check("Sét pha dẻo cứng: trạng thái", B(lo.State == "dẻo cứng"), 1, 0);
        var cl = SoilStandard.Lookup(SoilKind.Clay, SandGrain.Medium, 0.75, 0.15, 0.93);
        check("Sét nửa cứng e = 0,75: c = 54, φ = 19, E = 21", cl.C * 10000 + cl.Phi * 100 + cl.E, 540000 + 1900 + 21, 1e-9);
        var cl2 = SoilStandard.Lookup(SoilKind.Clay, SandGrain.Medium, 0.70, 0.5, 0.95);
        check("Sét e = 0,7, IL = 0,5: R0 = (400 + 250)/2 = 325 kPa", cl2.R0, 325, 1e-9);
        check("Sét e = 0,7, IL = 0,5: dải 0,25 < IL ≤ 0,5 → c = (57 + 50)/2", cl2.C, 53.5, 1e-9);
        var sp = SoilStandard.Lookup(SoilKind.SandyClay, SandGrain.Medium, 0.65, 0.5, 0.9);
        check("Cát pha dẻo e = 0,65: c = 13, φ = 24, E = 16", sp.C * 10000 + sp.Phi * 100 + sp.E, 130000 + 2400 + 16, 1e-9);
        check("Cát pha e = 0,65, IL = 0,5: R0 nội suy", sp.R0, Math.Round(Rclay(0.65, 0.5, new[] { (0.5, 300.0, 300.0), (0.7, 250.0, 200.0) })), 1e-9);
        var hard = SoilStandard.Lookup(SoilKind.SiltyClay, SandGrain.Medium, 0.60, -0.1, 0.9);
        check("Sét pha cứng (IL < 0): tra dải 0…0,25 → c = (37 + 31)/2", hard.C, 34, 1e-9);
        var soft = SoilStandard.Lookup(SoilKind.Clay, SandGrain.Medium, 1.05, 0.85, 0.98);
        check("Sét dẻo chảy (IL > 0,75): cảnh báo ngoài bảng", B(soft.Warnings.Any(w => w.Contains("0,75"))), 1, 0);
        var fill = SoilStandard.Lookup(SoilKind.Fill, SandGrain.Medium, 0.9, -1, 0.8);
        check("Đất đắp: không có trong bảng tra", B(!fill.InTable && fill.Warnings.Count > 0), 1, 0);
        var ext = SoilStandard.Lookup(SoilKind.Clay, SandGrain.Medium, 1.3, 0.3, 0.98);
        check("Sét e = 1,3 > 1,05: lấy giá trị biên + cảnh báo", ext.C * 100 + (ext.Warnings.Count > 0 ? 1 : 0), 3201, 1e-9);

        check("Danh mục lớp tiêu chuẩn: tên không trùng", SoilStandard.Catalog.Select(s => s.Name).Distinct().Count(), SoilStandard.Catalog.Count, 0);
        int bad = 0;
        foreach (var s in SoilStandard.Catalog)
        {
            var x = SoilStandard.Lookup(s.Kind, s.Grain, s.E0, s.IL, s.Sr);
            if (SoilStandard.ClassOf(s.Kind, s.Grain) != SoilStdClass.None && !(x.InTable && double.IsFinite(x.E) && x.Gamma > 14 && x.Gamma < 23)) bad++;
            if (string.IsNullOrEmpty(SoilStandard.Summary(s))) bad++;
        }
        check("Danh mục: mọi lớp trong bảng tra có c, φ, E, γ hợp lệ", bad, 0, 0);
        var st = SoilStandard.Find("Sét pha – dẻo cứng");
        Console.WriteLine("   · Sét pha – dẻo cứng: " + SoilStandard.Summary(st));
        Console.WriteLine("   · Cát hạt vừa – chặt vừa: " + SoilStandard.Summary(SoilStandard.Find("Cát hạt vừa – chặt vừa")));
        check("Trạng thái đất dính đúng mốc TCVN", B(SoilStandard.ClayConsistency(SoilStdClass.Clay, 0.6) == "dẻo mềm" && SoilStandard.ClayConsistency(SoilStdClass.Loam, 0.2) == "nửa cứng"), 1, 0);
        check("Độ chặt cát đúng mốc TCVN", B(SoilStandard.SandDensity(SoilStdClass.FineSand, 0.58) == "chặt" && SoilStandard.SandDensity(SoilStdClass.SiltySand, 0.79) == "chặt vừa"), 1, 0);
    }

    private static double Rclay(double e, double il, (double e, double a, double b)[] t)
    {
        double I(Func<(double e, double a, double b), double> f)
        {
            if (e <= t[0].e) return f(t[0]);
            if (e >= t[^1].e) return f(t[^1]);
            for (int i = 0; i + 1 < t.Length; i++)
                if (e <= t[i + 1].e) return f(t[i]) + (e - t[i].e) / (t[i + 1].e - t[i].e) * (f(t[i + 1]) - f(t[i]));
            return f(t[^1]);
        }
        double r0 = I(x => x.a), r1 = I(x => x.b);
        return r0 + Math.Clamp(il, 0, 1) * (r1 - r0);
    }

    private static void Layers(Action<string, double, double, double> check)
    {
        var std = SoilStandard.Find("Sét pha – dẻo cứng");
        var l = SoilStandard.NewLayer(std, 0, 4);
        check("Lớp mới từ lớp tiêu chuẩn: chỉ tiêu theo bảng tra", B(l.Source == SoilPropSource.Table), 1, 0);
        check("Lớp mới: φ = bảng tra", l.Phi, 21, 1e-9);
        check("Lớp mới: R0 > 0", B(l.R0 > 0), 1, 0);
        check("Lớp mới: ghi lớp tiêu chuẩn", B(l.StdName == std.Name), 1, 0);
        // đổi e → tra lại
        l.VoidRatio = 0.65; SoilStandard.ApplyTo(l);
        check("Đổi e = 0,65 → c = 28 kPa (sét pha dẻo cứng)", l.C, 28, 1e-9);
        // ktc khi tính R
        var bh = new Borehole { Name = "HK1", Layers = { l } };
        var s1 = new SoilSettings { Depth = 1.5 };
        GeotechCalc.ApplyToSoil(s1, bh, 1.5);
        check("Lớp đặt móng theo bảng tra → ktc = 1,1", s1.Ktc, 1.1, 1e-12);
        var lt = l.Copy(); lt.Source = SoilPropSource.Test;
        var s2 = new SoilSettings { Depth = 1.5, StdName = "Sét pha – dẻo cứng", Ktc = 1.1 };
        GeotechCalc.ApplyToSoil(s2, new Borehole { Name = "HK2", Layers = { lt } }, 1.5);
        check("Lớp đặt móng theo thí nghiệm → ktc = 1,0", s2.Ktc, 1.0, 1e-12);
        check("Sức chịu tải: R(bảng tra) = R(thí nghiệm)/1,1", FootingDesigner.BearingR(s1, 1.2), FootingDesigner.BearingR(s2, 1.2) / 1.1, 1e-6);
        // nền nhập tay (không hố khoan)
        var ss = new SoilSettings();
        SoilStandard.ApplyTo(ss, SoilStandard.Find("Sét – nửa cứng"));
        check("Nền nhập tay theo lớp tiêu chuẩn: ktc = 1,1", ss.Ktc, 1.1, 1e-12);
        check("Nền nhập tay theo lớp tiêu chuẩn: φ = 19° (sét nửa cứng e = 0,75)", ss.Phi, 19, 1e-9);
        check("Nền nhập tay theo lớp tiêu chuẩn: tên lớp", B(ss.StdName == "Sét – nửa cứng"), 1, 0);
        // JSON
        var js = JsonSerializer.Serialize(l, Project.JsonOptions);
        var l2 = JsonSerializer.Deserialize<SoilLayer>(js, Project.JsonOptions);
        check("JSON: giữ nguồn chỉ tiêu + lớp tiêu chuẩn + Sr + R0", B(l2.Source == SoilPropSource.Table && l2.StdName == l.StdName && Math.Abs(l2.Sr - l.Sr) < 1e-12 && Math.Abs(l2.R0 - l.R0) < 1e-12), 1, 0);
        var old = JsonSerializer.Deserialize<SoilLayer>("{\"Name\":\"L1\",\"Phi\":12}", Project.JsonOptions);
        check("JSON cũ: lớp đất theo thí nghiệm (giữ nguyên chỉ tiêu)", B(old.Source == SoilPropSource.Test && old.Phi == 12), 1, 0);
        var sb = SoilStandard.NewLayer(SoilStandard.Find("Cát bụi – chặt vừa"), 0, 2);
        check("Cát bụi: tên loại đất", B(sb.KindText == "Cát bụi"), 1, 0);
    }

    private static void Admin(Action<string, double, double, double> check)
    {
        check("Danh mục hành chính: 3.321 xã / phường / đặc khu", AdminUnits.Count, 3321, 0);
        check("Danh mục hành chính: 34 tỉnh / thành", AdminUnits.ProvinceCount, 34, 0);
        check("Mọi tỉnh trong LocationDb có danh mục xã", LocationDb.Provinces.Count(p => AdminUnits.Of(p.Name).Count == 0), 0, 0);
        var tn = AdminUnits.Of("Thái Nguyên");
        Console.WriteLine($"   · Thái Nguyên: {tn.Count} xã/phường; TP Hồ Chí Minh: {AdminUnits.Of("TP Hồ Chí Minh").Count}; Hà Nội: {AdminUnits.Of("Hà Nội").Count}");
        check("Thái Nguyên có Phường Quyết Thắng", B(tn.Any(c => c.FullName == "Phường Quyết Thắng")), 1, 0);
        check("Tìm theo tên tỉnh cũ (Bắc Kạn → Thái Nguyên)", AdminUnits.Of("Bắc Kạn").Count, tn.Count, 0);
        check("Tìm xã không dấu / không tiền tố", B(AdminUnits.Find("Thái Nguyên", "quyet thang")?.FullName == "Phường Quyết Thắng"), 1, 0);
        check("Huế (tên cũ Thừa Thiên Huế)", B(AdminUnits.Of("Thừa Thiên Huế").Count > 0), 1, 0);
        check("Có đặc khu", B(AdminUnits.Of("Quảng Ninh").Any(c => c.Type == "Đặc khu")), 1, 0);
        check("Tìm toàn quốc", B(AdminUnits.Search("Quyết Thắng").Any(c => c.Province == "Thái Nguyên")), 1, 0);
    }

    private static void PileCopy(Action<string, double, double, double> check)
    {
        var p = ModelBuilder.NewProject("Đài cọc giống nhau");
        ModelBuilder.SetGrid(p, new[] { 4.0, 4.0 }, new[] { 5.0 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3, 3.3, 3.3 }, 0.0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x22", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5 });
        var r = FrameAnalysis.Run(p);
        p.Foundation.Type = FoundationType.Pile;
        p.Foundation.PileCapacity = 450; p.Foundation.PileSize = 0.25;
        var s = ConcreteDesigner.Design(p, r);
        check("Đài cọc: có đài", B(s.PileCaps.Count > 0), 1, 0);
        var src = s.PileCaps.OrderByDescending(c => c.Piles.Count).ThenByDescending(c => c.PMax).First();
        var ovr = PileLayout.FromCap(p.Foundation, src);
        check("Bố trí đài nguồn: toạ độ cọc", ovr.CustomPiles.Count, src.Piles.Count, 0);
        check("Bố trí đài nguồn: kích thước đài", ovr.L * 100 + ovr.B, src.L * 100 + src.B, 1e-9);
        var all = FoundationUtil.BaseColumns(p).Select(x => x.Node.Id).ToList();
        PileLayout.Set(p.Foundation, all, ovr);
        var s2 = ConcreteDesigner.Design(p, r);
        check("Gán giống nhau: mọi đài cùng số cọc", s2.PileCaps.Select(c => c.Piles.Count).Distinct().Count(), 1, 0);
        check("Gán giống nhau: mọi đài cùng kích thước", s2.PileCaps.Select(c => Math.Round(c.L * 1000) * 10000 + Math.Round(c.B * 1000)).Distinct().Count(), 1, 0);
        check("Gán giống nhau: số đài ≤ trước", B(s2.PileCaps.Count <= s.PileCaps.Count), 1, 0);
        check("Gán giống nhau: tổng vị trí đài = số chân cột", s2.PileCaps.Sum(c => Math.Max(1, c.Locations.Count)), all.Count, 0);
        Console.WriteLine($"   · Gán đài {src.Name} ({src.Piles.Count} cọc) cho {all.Count} chân cột: {s.PileCaps.Count} → {s2.PileCaps.Count} loại đài");
        var again = PileLayout.FromCap(p.Foundation, s2.PileCaps[0]);
        check("Đài đã có bố trí người dùng: lấy lại đúng bản ghi", again.CustomPiles.Count, ovr.CustomPiles.Count, 0);
    }
}

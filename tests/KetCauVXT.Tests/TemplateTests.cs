using System.Globalization;
using System.Text;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CSMath;
using KetCauVXT.Drawing;
using KetCauVXT.Dwg;
using KetCauVXT.Import;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>Mẫu định dạng (*.kcvxt-mau) và khung tên người dùng: lưu/mở gói đủ các phần, áp dụng gộp/thay thế, mẫu mặc định cho dự án mới,
/// thư viện (đổi tên, xoá, tờ mẫu), nhập khung tên từ DXF (khung + chữ {TEN_CONG_TRINH}, {SO_HIEU} + INSERT có ATTRIB, ATTDEF trong model space),
/// đơn vị/điểm gốc/chế độ đặt, xuất DXF tờ đã thay trường, khung tên ảnh (base64), DWG (ACadSharp).
/// File DXF nhập (viết tay) để trong thư mục tạm; chỉ DXF do DxfWriter của chương trình xuất mới ghi vào outDir (CI kiểm tra ezdxf).</summary>
public static class TemplateTests
{
    private const string TinyPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAIAAAB7QOjdAAAAD0lEQVR4nGP4z8DAwPAfAAcAAf9+CLHQAAAAAElFTkSuQmCC";

    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir ?? Path.GetTempPath(), "MauDinhDang");
        string tmp = Path.Combine(Path.GetTempPath(), "kcvxt_template_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(tmp);
        var prevLib = TemplateService.LibraryFolderOverride;
        bool prevAuto = TemplateService.AutoApplyDefault;
        var prevDwg = CadFileReaders.Dwg;
        var prevDwgToDxf = CadFileReaders.DwgToDxf;
        try
        {
            TemplateService.LibraryFolderOverride = Path.Combine(tmp, "ThuVien");
            Fields(check);
            TemplateRoundtrip(check, tmp);
            Library(check, tmp);
            TitleImport(check, dir, tmp);
            ImageTitle(check, tmp);
            Dwg(check, dir, tmp);
        }
        catch (Exception ex)
        {
            Console.WriteLine("   LOI TemplateTests: " + ex);
            check("Mẫu định dạng: chạy hết bộ kiểm thử không ngoại lệ", 0, 1, 0);
        }
        finally
        {
            TemplateService.LibraryFolderOverride = prevLib;
            TemplateService.AutoApplyDefault = prevAuto;
            CadFileReaders.Dwg = prevDwg;
            CadFileReaders.DwgToDxf = prevDwgToDxf;
            try { Directory.Delete(tmp, true); } catch (Exception) { /* bỏ qua */ }
        }
    }

    // ==================================================================
    // Trường khung tên
    // ==================================================================
    private static void Fields(Action<string, double, double, double> check)
    {
        check("Trường: 'Tên công trình' → TEN_CONG_TRINH", B(TitleFields.Canonical("Tên công trình") == TitleFields.TenCongTrinh), 1, 0);
        check("Trường: 'drawing_no' → SO_HIEU", B(TitleFields.Canonical("drawing_no") == TitleFields.SoHieu), 1, 0);
        check("Trường: 'người thiết kế' → THIET_KE", B(TitleFields.Canonical("người thiết kế") == TitleFields.ThietKe), 1, 0);
        check("Trường: 'ChuDauTu' → CHU_DAU_TU", B(TitleFields.Canonical("ChuDauTu") == TitleFields.ChuDauTu), 1, 0);
        check("Trường: 'Chủ trì' → CHU_TRI", B(TitleFields.Canonical("Chủ trì") == TitleFields.ChuTri), 1, 0);
        check("Trường: tên lạ → null", B(TitleFields.Canonical("XYZ_ABC") == null), 1, 0);
        var ph = TitleFields.Placeholders("SỐ: {so_hieu} / {TONG_TRANG} – {SO_HOP_DONG}");
        check("Trường: 3 chỗ trống trong chữ", ph.Count, 3, 0);
        check("Trường: chỗ trống tự đặt giữ tên chuẩn hoá", B(ph.Contains("SO_HOP_DONG")), 1, 0);

        var p = new Project { Name = "Nhà ông A", Location = "Thái Nguyên" };
        p.TitleBlock.Extra["so hop dong"] = "HĐ-12/2026";
        var s1 = new Sheet { Number = "KC-01", Name = "GHI CHÚ" };
        var s2 = new Sheet { Number = "KC-02", Name = "MẶT BẰNG" };
        p.Sheets.Add(s1); p.Sheets.Add(s2);
        var v = TitleFields.Values(p, s2, "1/100");
        string filled = TitleFields.Fill("{TEN_CONG_TRINH} | {so_hieu} | {TRANG}/{TONG_TRANG} | {TY_LE} | {SO_HOP_DONG} | {KHONG_CO}", v);
        check("Trường: thay đủ giá trị (tên, số hiệu, trang, tỷ lệ, trường tự đặt, trường lạ rỗng)",
            B(filled == "Nhà ông A | KC-02 | 2/2 | 1/100 | HĐ-12/2026 | "), 1, 0);
    }

    // ==================================================================
    // Gói mẫu: lưu / mở / áp dụng
    // ==================================================================
    private static Project SampleProject()
    {
        var p = ModelBuilder.NewProject("Dự án nguồn mẫu");
        ModelBuilder.SetGrid(p, new[] { 4.0 }, new[] { 4.0 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, 0);
        p.Location = "Phường Quyết Thắng";
        p.CadStandard = CadStandard.Default();
        p.CadStandard.FontName = CadStandard.FontTimes;
        p.TitleBlock = new TitleBlockInfo
        {
            Company = "CÔNG TY TNHH TƯ VẤN A", ProjectName = "Tên riêng công trình", Owner = "Ông B", Designer = "",
            Checker = "KS Kiểm Tra", Approver = "Giám đốc C", Leader = "KS Chủ Trì", NumberPrefix = "KT-", LogoData = TinyPng,
            Extra = new Dictionary<string, string> { ["SO_HOP_DONG"] = "HĐ-01" }
        };
        p.ReportOptions = new ReportOptions { DesignCompany = "CÔNG TY TNHH TƯ VẤN A", ProjectName = "Riêng", ChiefDesigner = "KS Chủ Trì" };
        p.ReportOptions.Chapters.Geotech = false;
        p.ReportOptions.ExamplesPerType = 2;
        p.QuantitySettings.BrickName = "Gạch không nung 6 lỗ";
        p.QuantitySettings.TieWirePerTon = 15.5;
        p.RebarPrefs.MaxBarsPerLayer = 3;
        var s = new Sheet { Number = "KC-01", Name = "MẶT BẰNG KẾT CẤU", Paper = "A2", MarginLeft = 30 };
        s.Views.Add(new SheetView { Kind = SheetViewKind.PlanStory, Param = p.Stories[1].Name, Title = "MẶT BẰNG KẾT CẤU " + p.Stories[1].Name, X = 40, Y = 60, W = 300, H = 200, Scale = 100 });
        s.Views.Add(new SheetView { Kind = SheetViewKind.Notes, Param = "Ghi chú mẫu", X = 360, Y = 60, W = 150, H = 200 });
        s.Items.Add(SheetBuilder.NewText(40, 400, "GHI CHÚ CỦA CÔNG TY", 5));
        s.Items.Add(SheetBuilder.NewImage(450, 380, 40, 20, TinyPng, "logo.png"));
        p.Sheets.Add(s);
        p.TitleBlock.Custom = new TitleBlockDef
        {
            Name = "KT vuông", Entities =
            {
                new TitleBlockEntity { Type = "PLINE", Closed = true, Pts = { -100, 0, 0, 0, 0, 30, -100, 30 } },
                new TitleBlockEntity { Type = "TEXT", Text = "{TEN_CONG_TRINH}", H = 3, Pts = { -95, 20 } },
                new TitleBlockEntity { Type = "TEXT", Text = "x", Field = TitleFields.SoHieu, H = 4, Pts = { -20, 5 } }
            }
        };
        p.TitleBlock.Custom.UpdateExtents();
        return p;
    }

    private static void TemplateRoundtrip(Action<string, double, double, double> check, string tmp)
    {
        var src = SampleProject();
        var pkg = TemplateService.FromProject(src, TemplateParts.All, "Mẫu công ty A", "Mẫu kiểm thử");
        check("Mẫu: tạo từ dự án có đủ 7 phần", (int)pkg.Parts, (int)TemplateParts.All, 0);
        check("Mẫu: bỏ tên công trình/chủ đầu tư riêng khỏi thông tin khung tên", B(pkg.TitleBlock.ProjectName == "" && pkg.TitleBlock.Owner == "" && pkg.TitleBlock.Custom == null), 1, 0);
        check("Mẫu: bỏ tên công trình khỏi tuỳ chọn thuyết minh", B(pkg.Report.ProjectName == ""), 1, 0);
        var stv = pkg.SheetTemplates[0].Layout.Views[0];
        check("Mẫu: ô khung nhìn mặt bằng bỏ tham số, tên thay bằng {THAM_SO}", B(stv.Param == "" && stv.Title == "MẶT BẰNG KẾT CẤU " + TemplateService.ParamToken), 1, 0);
        check("Mẫu: ô ghi chú giữ nội dung", B(pkg.SheetTemplates[0].Layout.Views[1].Param == "Ghi chú mẫu"), 1, 0);

        string path = Path.Combine(tmp, "mau_cong_ty" + TemplateService.Extension);
        TemplateService.Save(path, pkg);
        var back = TemplateService.Load(path);
        check("Mẫu: mở lại đủ 7 phần", (int)back.Parts, (int)TemplateParts.All, 0);
        check("Mẫu: tên, mô tả, ngày", B(back.Name == "Mẫu công ty A" && back.Description == "Mẫu kiểm thử" && back.Modified.Length > 0), 1, 0);
        check("Mẫu: chuẩn CAD (font Times)", B(back.CadStandard.FontName == CadStandard.FontTimes), 1, 0);
        check("Mẫu: khung tên người dùng 3 thực thể", back.TitleBlockDef.Entities.Count, 3, 0);
        check("Mẫu: thông tin khung tên (đơn vị, chủ trì, logo, trường tự đặt)",
            B(back.TitleBlock.Company == "CÔNG TY TNHH TƯ VẤN A" && back.TitleBlock.Leader == "KS Chủ Trì" && back.TitleBlock.LogoData == TinyPng && back.TitleBlock.Extra["SO_HOP_DONG"] == "HĐ-01"), 1, 0);
        check("Mẫu: thuyết minh (bỏ chương địa chất, 2 ví dụ)", B(!back.Report.Chapters.Geotech && back.Report.ExamplesPerType == 2), 1, 0);
        check("Mẫu: định mức (tên gạch, dây thép buộc)", B(back.Quantity.BrickName == "Gạch không nung 6 lỗ" && Math.Abs(back.Quantity.TieWirePerTon - 15.5) < 1e-12), 1, 0);
        check("Mẫu: tuỳ chọn thép (3 thanh/hàng)", back.Rebar.MaxBarsPerLayer, 3, 0);
        var bs = back.SheetTemplates[0].Layout;
        check("Mẫu: tờ mẫu giữ khổ A2, lề trái 30, 2 đối tượng (ảnh base64)",
            B(bs.Paper == "A2" && bs.MarginLeft == 30 && bs.Items.Count == 2 && bs.Items[1].ImageData == TinyPng), 1, 0);

        // gói chỉ một phần
        string p2 = Path.Combine(tmp, "chi_khung_ten" + TemplateService.Extension);
        TemplateService.Save(p2, pkg, TemplateParts.TitleBlockDef | TemplateParts.TitleBlockInfo);
        var sub = TemplateService.Load(p2);
        check("Mẫu: lưu chỉ phần đã chọn (khung tên + thông tin)", (int)sub.Parts, (int)(TemplateParts.TitleBlockDef | TemplateParts.TitleBlockInfo), 0);

        // file zip chứa template.json
        string pz = Path.Combine(tmp, "mau_zip" + TemplateService.Extension);
        using (var zs = File.Create(pz))
        using (var zip = new System.IO.Compression.ZipArchive(zs, System.IO.Compression.ZipArchiveMode.Create))
        {
            var en = zip.CreateEntry("template.json");
            using var w = new StreamWriter(en.Open(), new UTF8Encoding(false));
            w.Write(TemplateService.ToJson(sub));
        }
        check("Mẫu: mở được gói zip (template.json)", (int)TemplateService.Load(pz).Parts, (int)sub.Parts, 0);

        bool bad = false;
        try { TemplateService.FromJson("{\"Format\":\"Khac\"}"); } catch (InvalidDataException) { bad = true; }
        check("Mẫu: từ chối file không đúng định dạng", B(bad), 1, 0);

        // ---- áp dụng: gộp vào dự án mới ----
        var dst = ModelBuilder.NewProject("Dự án đích");
        ModelBuilder.SetGrid(dst, new[] { 5.0 }, new[] { 4.0 });
        ModelBuilder.SetStories(dst, new[] { 3.3, 3.3 }, 0);
        dst.TitleBlock.Designer = "Giữ lại";
        dst.TitleBlock.ProjectName = "Tên đích";
        var applied = TemplateService.ApplyTo(dst, back, TemplateParts.All, TemplateApplyMode.Merge);
        check("Áp dụng gộp: mọi phần", (int)applied, (int)TemplateParts.All, 0);
        check("Áp dụng gộp: chuẩn CAD", B(dst.CadStandard?.FontName == CadStandard.FontTimes), 1, 0);
        check("Áp dụng gộp: đơn vị thiết kế từ mẫu, giữ người thiết kế + tên công trình của dự án",
            B(dst.TitleBlock.Company == "CÔNG TY TNHH TƯ VẤN A" && dst.TitleBlock.Designer == "Giữ lại" && dst.TitleBlock.ProjectName == "Tên đích"), 1, 0);
        check("Áp dụng gộp: khung tên người dùng", B(SheetBuilder.HasCustomTitle(dst) && dst.TitleBlock.Custom.Entities.Count == 3), 1, 0);
        check("Áp dụng gộp: thuyết minh + định mức + thép",
            B(dst.ReportOptions != null && !dst.ReportOptions.Chapters.Geotech && dst.ReportOptions.DesignCompany == "CÔNG TY TNHH TƯ VẤN A"
              && dst.QuantitySettings.BrickName == "Gạch không nung 6 lỗ" && dst.RebarPrefs.MaxBarsPerLayer == 3), 1, 0);
        check("Áp dụng gộp: định mức giữ danh sách cấp phối", B(dst.QuantitySettings.ConcreteNorms.Count > 0), 1, 0);
        check("Áp dụng gộp: thêm 1 tờ từ tờ mẫu", dst.Sheets.Count, 1, 0);
        var ns = dst.Sheets[0];
        string st2 = dst.Stories.OrderBy(x => x.Elevation).First().Name;
        check("Áp dụng gộp: ô mặt bằng tự điền tầng đầu tiên, tên thay tham số",
            B(ns.Views[0].Param == st2 && ns.Views[0].Title == "MẶT BẰNG KẾT CẤU " + st2), 1, 0);
        check("Áp dụng gộp: số hiệu tờ theo tiền tố mẫu (KT-01)", B(ns.Number == "KT-01"), 1, 0);

        // gộp lần 2 → thêm tờ; thay thế → chỉ còn 1 tờ, người thiết kế theo mẫu (rỗng), giữ tên công trình
        TemplateService.ApplyTo(dst, back, TemplateParts.SheetTemplates, TemplateApplyMode.Merge);
        check("Áp dụng gộp lần 2: thêm tờ (2 tờ), số hiệu KT-02", B(dst.Sheets.Count == 2 && dst.Sheets[1].Number == "KT-02"), 1, 0);
        TemplateService.ApplyTo(dst, back, TemplateParts.All, TemplateApplyMode.Replace);
        check("Áp dụng thay thế: danh sách tờ thay toàn bộ (1 tờ)", dst.Sheets.Count, 1, 0);
        check("Áp dụng thay thế: thông tin khung tên theo mẫu, giữ tên công trình",
            B(dst.TitleBlock.Designer == "" && dst.TitleBlock.ProjectName == "Tên đích" && dst.TitleBlock.Custom != null), 1, 0);

        // chỉ áp dụng phần đã chọn
        var only = ModelBuilder.NewProject("Chỉ chuẩn CAD");
        var ap2 = TemplateService.ApplyTo(only, back, TemplateParts.CadStandard | TemplateParts.Rebar);
        check("Áp dụng: chỉ các phần đã chọn", B(ap2 == (TemplateParts.CadStandard | TemplateParts.Rebar) && only.TitleBlock.Custom == null && only.Sheets.Count == 0), 1, 0);

        // gộp sâu JSON: chuỗi rỗng của mẫu không xoá giá trị dự án
        var a = new ReportOptions { DesignCompany = "Giữ", Preparer = "X" };
        var o = new ReportOptions { DesignCompany = "", Preparer = "Y", ExamplesPerType = 3 };
        o.Chapters.Steel = false;
        var m = TemplateService.MergeObj(a, o);
        check("Gộp sâu: chuỗi rỗng không ghi đè, giá trị khác ghi đè, đối tượng con gộp",
            B(m.DesignCompany == "Giữ" && m.Preparer == "Y" && m.ExamplesPerType == 3 && !m.Chapters.Steel && m.Chapters.Beams), 1, 0);
    }

    // ==================================================================
    // Thư viện, mẫu mặc định cho dự án mới
    // ==================================================================
    private static void Library(Action<string, double, double, double> check, string tmp)
    {
        var src = SampleProject();
        var pkg = TemplateService.FromProject(src, TemplateParts.TitleBlockInfo | TemplateParts.CadStandard | TemplateParts.TitleBlockDef, "Mẫu mặc định công ty");
        string lp = TemplateService.SaveToLibrary(pkg);
        check("Thư viện: lưu vào thư mục thư viện", B(File.Exists(lp) && Path.GetDirectoryName(lp) == TemplateService.LibraryFolder), 1, 0);
        string lp2 = TemplateService.SaveToLibrary(pkg);
        check("Thư viện: trùng tên → file khác", B(lp2 != lp && File.Exists(lp2)), 1, 0);
        TemplateService.SetDefault(lp);
        var list = TemplateService.List();
        check("Thư viện: liệt kê 2 mẫu, 1 mặc định", B(list.Count == 2 && list.Count(x => x.IsDefault) == 1 && list.First(x => x.IsDefault).Path == lp), 1, 0);

        TemplateService.AutoApplyDefault = false;
        var p0 = ModelBuilder.NewProject("Không áp dụng");
        check("Mẫu mặc định: tắt tự áp dụng → dự án mới không đổi", B(p0.TitleBlock.Company == "" && p0.CadStandard == null), 1, 0);
        TemplateService.AutoApplyDefault = true;
        var p1 = ModelBuilder.NewProject("Dự án mới theo mẫu");
        TemplateService.AutoApplyDefault = false;
        check("Mẫu mặc định: dự án mới nhận đơn vị thiết kế, chuẩn CAD, khung tên người dùng",
            B(p1.TitleBlock.Company == "CÔNG TY TNHH TƯ VẤN A" && p1.CadStandard?.FontName == CadStandard.FontTimes && SheetBuilder.HasCustomTitle(p1)), 1, 0);
        check("Mẫu mặc định: dự án mới giữ tên của nó", B(p1.Name == "Dự án mới theo mẫu" && p1.TitleBlock.ProjectName == ""), 1, 0);

        string rn = TemplateService.Rename(lp, "Mẫu đổi tên");
        check("Thư viện: đổi tên (file mới, vẫn là mặc định)", B(File.Exists(rn) && !File.Exists(lp) && TemplateService.DefaultTemplatePath == rn && TemplateService.Load(rn).Name == "Mẫu đổi tên"), 1, 0);
        string ex = Path.Combine(tmp, "xuat" + TemplateService.Extension);
        TemplateService.Export(rn, ex);
        string im = TemplateService.Import(ex);
        check("Thư viện: xuất rồi nhập lại (3 mẫu)", B(File.Exists(ex) && File.Exists(im) && TemplateService.List().Count == 3), 1, 0);
        TemplateService.Delete(rn);
        check("Thư viện: xoá mẫu mặc định → bỏ mặc định", B(!File.Exists(rn) && TemplateService.DefaultTemplatePath == null && TemplateService.List().Count == 2), 1, 0);

        // tờ mẫu từ cửa sổ dàn trang
        string sp = TemplateService.AddSheetTemplateToLibrary(src.Sheets[0], "Tờ mặt bằng A2");
        TemplateService.AddSheetTemplateToLibrary(src.Sheets[0], "Tờ mặt bằng A2");     // trùng tên → thay
        TemplateService.AddSheetTemplateToLibrary(src.Sheets[0], "Tờ thứ hai");
        var sts = TemplateService.LibrarySheetTemplates();
        check("Tờ mẫu: gói 'Tờ mẫu' có 2 tờ mẫu", B(File.Exists(sp) && sts.Count == 2), 1, 0);
        var dst = ModelBuilder.NewProject("Tờ từ mẫu");
        ModelBuilder.SetStories(dst, new[] { 3.0, 3.0, 3.0 }, 0);
        var t = sts.First(x => x.Template.Name == "Tờ mặt bằng A2").Template;
        string last = dst.Stories.OrderBy(x => x.Elevation).Last().Name;
        var ns = TemplateService.SheetFromTemplate(dst, t, "KC-09", new[] { last, null });
        check("Tờ mẫu: tạo tờ, ô khung nhìn nhận tham số chọn, vị trí/tỷ lệ giữ nguyên",
            B(ns.Number == "KC-09" && ns.Views[0].Param == last && ns.Views[0].Scale == 100 && ns.Views[0].W == 300 && ns.Views[1].Param == "Ghi chú mẫu"), 1, 0);
    }

    // ==================================================================
    // Nhập khung tên từ DXF
    // ==================================================================
    /// <summary>DXF viết tay: block KHUNG_TEN (khung 180×40, chữ {TEN_CONG_TRINH}, "SỐ HIỆU: {SO_HIEU}" căn giữa, ATTDEF TEN_BAN_VE, THIET_KE),
    /// chèn tại (1000, 500) có ATTRIB; đường tròn ngoài block (bị loại khi chọn block). Đơn vị mm.</summary>
    public static string TitleDxf()
    {
        var sb = new StringBuilder();
        void P(int c, object v) => sb.Append(c).Append('\n').Append(Convert.ToString(v, CultureInfo.InvariantCulture)).Append('\n');
        void Line(double x1, double y1, double x2, double y2) { P(0, "LINE"); P(8, "KHUNG"); P(10, x1); P(20, y1); P(30, 0); P(11, x2); P(21, y2); P(31, 0); }
        P(0, "SECTION"); P(2, "HEADER");
        P(9, "$ACADVER"); P(1, "AC1021");
        P(9, "$INSUNITS"); P(70, 4);
        P(0, "ENDSEC");
        P(0, "SECTION"); P(2, "BLOCKS");
        P(0, "BLOCK"); P(8, "0"); P(2, "KHUNG_TEN"); P(70, 2); P(10, 0); P(20, 0); P(30, 0); P(3, "KHUNG_TEN");
        Line(0, 0, 180, 0); Line(180, 0, 180, 40); Line(180, 40, 0, 40); Line(0, 40, 0, 0);
        Line(0, 20, 180, 20); Line(130, 0, 130, 20);
        P(0, "TEXT"); P(8, "CHU"); P(10, 5); P(20, 28); P(30, 0); P(40, 3.5); P(1, "{TEN_CONG_TRINH}");
        P(0, "TEXT"); P(8, "CHU"); P(10, 155); P(20, 8); P(30, 0); P(40, 3.0); P(1, "SỐ HIỆU: {SO_HIEU}"); P(72, 1); P(11, 155); P(21, 8); P(31, 0); P(73, 0);
        P(0, "TEXT"); P(8, "CHU"); P(10, 5); P(20, 12); P(30, 0); P(40, 2.5); P(1, "Thiết kế:");
        P(0, "ATTDEF"); P(8, "CHU"); P(10, 40); P(20, 12); P(30, 0); P(40, 2.5); P(1, "TÊN BẢN VẼ"); P(3, "Tên bản vẽ"); P(2, "TEN_BAN_VE"); P(70, 0);
        P(0, "ATTDEF"); P(8, "CHU"); P(10, 40); P(20, 5); P(30, 0); P(40, 2.5); P(1, "NGƯỜI TK"); P(3, "Người thiết kế"); P(2, "NGUOI_THIET_KE"); P(70, 0);
        P(0, "ENDBLK"); P(8, "0");
        P(0, "ENDSEC");
        P(0, "SECTION"); P(2, "ENTITIES");
        P(0, "INSERT"); P(8, "KHUNG"); P(66, 1); P(2, "KHUNG_TEN"); P(10, 1000); P(20, 500); P(30, 0);
        P(0, "ATTRIB"); P(8, "CHU"); P(10, 1040); P(20, 512); P(30, 0); P(40, 2.5); P(1, "TÊN BẢN VẼ"); P(2, "TEN_BAN_VE"); P(70, 0);
        P(0, "ATTRIB"); P(8, "CHU"); P(10, 1040); P(20, 505); P(30, 0); P(40, 2.5); P(1, "NGƯỜI TK"); P(2, "Người thiết kế"); P(70, 0);
        P(0, "SEQEND"); P(8, "KHUNG");
        P(0, "CIRCLE"); P(8, "KHAC"); P(10, 5000); P(20, 5000); P(30, 0); P(40, 100);
        P(0, "ENDSEC");
        P(0, "EOF");
        return sb.ToString();
    }

    /// <summary>DXF khung tên vẽ trực tiếp (ATTDEF trong model space, như file block WBLOCK), vẽ theo cm.</summary>
    public static string ModelAttDefDxf()
    {
        var sb = new StringBuilder();
        void P(int c, object v) => sb.Append(c).Append('\n').Append(Convert.ToString(v, CultureInfo.InvariantCulture)).Append('\n');
        P(0, "SECTION"); P(2, "HEADER"); P(9, "$ACADVER"); P(1, "AC1021"); P(9, "$INSUNITS"); P(70, 5); P(0, "ENDSEC");
        P(0, "SECTION"); P(2, "ENTITIES");
        P(0, "LWPOLYLINE"); P(8, "KHUNG"); P(90, 4); P(70, 1); P(10, 0); P(20, 0); P(10, 42); P(20, 0); P(10, 42); P(20, 29.7); P(10, 0); P(20, 29.7);
        P(0, "ATTDEF"); P(8, "CHU"); P(10, 30); P(20, 2); P(30, 0); P(40, 0.3); P(1, "CHỦ ĐẦU TƯ"); P(3, "Chủ đầu tư"); P(2, "CHU_DAU_TU"); P(70, 0);
        P(0, "ATTDEF"); P(8, "CHU"); P(10, 30); P(20, 1); P(30, 0); P(40, 0.3); P(1, "AN"); P(3, "ẩn"); P(2, "AN"); P(70, 1);
        P(0, "TEXT"); P(8, "CHU"); P(10, 2); P(20, 2); P(30, 0); P(40, 0.25); P(1, "Tờ {TRANG}/{TONG_TRANG}"); P(50, 90);
        P(0, "ENDSEC"); P(0, "EOF");
        return sb.ToString();
    }

    private static List<string> TextsOf(string dxfPath) =>
        DxfReader.Read(dxfPath).Entities.Where(e => e.Type == DxfEntityType.Text).Select(e => e.Text).ToList();

    private static void TitleImport(Action<string, double, double, double> check, string outDir, string tmp)
    {
        string dxf = Path.Combine(tmp, "khung_ten_nguoi_dung.dxf");
        File.WriteAllText(dxf, TitleDxf(), new UTF8Encoding(false));
        var res = TitleBlockImporter.FromFile(dxf, new TitleBlockImportOptions());
        var def = res.Def;
        foreach (var w in res.Warnings) Console.WriteLine("   ! " + w);
        Console.WriteLine("   · khung tên: " + res.Info);
        check("Khung tên DXF: tự chọn block KHUNG_TEN", B(res.UsedBlock == "KHUNG_TEN" && res.Blocks.Contains("KHUNG_TEN")), 1, 0);
        check("Khung tên DXF: bỏ đường tròn ngoài block", def.Entities.Count(e => e.Type == "CIRCLE"), 0, 0);
        check("Khung tên DXF: 6 đoạn thẳng", def.Entities.Count(e => e.Type == "LINE"), 6, 0);
        check("Khung tên DXF: rộng 180 mm", def.W, 180, 1e-6);
        check("Khung tên DXF: cao 40 mm", def.H, 40, 1e-6);
        check("Khung tên DXF: gốc góc phải dưới (MaxX = 0, MinY = 0)", Math.Abs(def.MaxX) + Math.Abs(def.MinY), 0, 1e-6);
        var tName = def.Entities.FirstOrDefault(e => e.Tag == "TEN_BAN_VE");
        check("Khung tên DXF: ATTRIB TEN_BAN_VE → trường TEN_BAN_VE", B(tName?.Field == TitleFields.TenBanVe), 1, 0);
        var tDes = def.Entities.FirstOrDefault(e => e.Tag == "Người thiết kế");
        check("Khung tên DXF: thẻ 'Người thiết kế' (bí danh) → THIET_KE", B(tDes?.Field == TitleFields.ThietKe), 1, 0);
        var tNo = def.Entities.FirstOrDefault(e => (e.Text ?? "").Contains("{SO_HIEU}"));
        check("Khung tên DXF: chữ 'SỐ HIỆU: {SO_HIEU}' căn giữa (đọc thêm mã 72)", B(tNo != null && tNo.Ha == 1), 1, 0);
        check("Khung tên DXF: toạ độ chữ số hiệu (155−180, 8)", (tNo?.Pts[0] ?? 0) + 1000 * (tNo?.Pts[1] ?? 0), -25 + 8000, 1e-6);
        check("Khung tên DXF: nhận 4 trường", res.FieldCount, 4, 0);
        var used = def.UsedFields();
        check("Khung tên DXF: trường dùng gồm tên công trình, số hiệu, tên bản vẽ, thiết kế",
            B(used.Contains(TitleFields.TenCongTrinh) && used.Contains(TitleFields.SoHieu) && used.Contains(TitleFields.TenBanVe) && used.Contains(TitleFields.ThietKe)), 1, 0);

        // đơn vị + điểm gốc + toàn bộ model space
        var r2 = TitleBlockImporter.FromFile(dxf, new TitleBlockImportOptions { MmPerUnit = 0.5, Base = TitleBlockBase.BottomLeft, Block = "KHUNG_TEN" });
        check("Khung tên DXF: hệ số 0,5 mm/đơn vị → rộng 90 mm, gốc trái dưới", r2.Def.W + 1000 * Math.Abs(r2.Def.MinX), 90, 1e-6);
        var r3 = TitleBlockImporter.FromFile(dxf, new TitleBlockImportOptions { Block = "*" });
        check("Khung tên DXF: toàn bộ model space có đường tròn", r3.Def.Entities.Count(e => e.Type == "CIRCLE"), 1, 0);
        var r4 = TitleBlockImporter.FromFile(dxf, new TitleBlockImportOptions { Block = "*", UseRegion = true, RegionX1 = 900, RegionY1 = 400, RegionX2 = 1300, RegionY2 = 600 });
        check("Khung tên DXF: vùng chọn loại đường tròn", B(r4.Def.Entities.Count(e => e.Type == "CIRCLE") == 0 && r4.Def.Entities.Count(e => e.Type == "LINE") == 6), 1, 0);

        // ---- dùng trong dự án: xuất DXF tờ ----
        var p = ModelBuilder.NewProject("NHÀ Ở GIA ĐÌNH ÔNG VŨ");
        p.Designer = "Vũ Xuân Tuyến";
        p.TitleBlock.Custom = def;
        var s = new Sheet { Number = "KC-07", Name = "MẶT BẰNG MÓNG", Paper = "A3", Date = "02/11/2026" };
        p.Sheets.Add(s);
        var (inner, _) = SheetBuilder.Frame(s);
        var rect = SheetBuilder.CustomTitleRect(p, s);
        check("Đặt khung tên: góc phải dưới khung viền trong", Math.Abs(rect.X2 - inner.X2) + Math.Abs(rect.Y1 - inner.Y1), 0, 1e-6);
        check("Đặt khung tên: rộng 180 trên giấy", rect.W, 180, 1e-6);
        string outDxf = Path.Combine(outDir, "To_KhungTenNguoiDung.dxf");
        SheetBuilder.ExportDxf(p, null, null, s, outDxf);
        var texts = TextsOf(outDxf);
        string all = string.Join("|", texts);
        check("Xuất DXF tờ: có tên công trình", B(texts.Contains("NHÀ Ở GIA ĐÌNH ÔNG VŨ")), 1, 0);
        check("Xuất DXF tờ: 'SỐ HIỆU: KC-07'", B(texts.Contains("SỐ HIỆU: KC-07")), 1, 0);
        check("Xuất DXF tờ: tên bản vẽ thay ATTRIB", B(texts.Contains("MẶT BẰNG MÓNG")), 1, 0);
        check("Xuất DXF tờ: người thiết kế thay ATTRIB", B(texts.Contains("Vũ Xuân Tuyến")), 1, 0);
        check("Xuất DXF tờ: chữ cố định giữ nguyên", B(texts.Contains("Thiết kế:")), 1, 0);
        check("Xuất DXF tờ: không còn chỗ trống {…} và chữ thẻ gốc", B(!all.Contains("{") && !all.Contains("TÊN BẢN VẼ") && !all.Contains("NGƯỜI TK")), 1, 0);
        check("Xuất DXF tờ: không vẽ khung tên mặc định", B(!texts.Contains("ĐƠN VỊ THIẾT KẾ") && !texts.Contains("Lập bằng Kết cấu VXT")), 1, 0);
        var tbw = SheetBuilder.TitleBlock(p, s, "1/100");
        check("Khung tên người dùng: hình có thực thể", B(tbw.EntityCount >= 10), 1, 0);

        // khung tên tắt trên tờ
        s.ShowTitleBlock = false;
        check("Tờ tắt khung tên: không vẽ chữ khung tên", SheetBuilder.TitleBlock(p, s).GetPrimitives().Count(x => x.Text != null && x.Text.Length > 0), 0, 0);
        s.ShowTitleBlock = true;

        // trở về khung tên mặc định
        p.TitleBlock.Custom = null;
        string outDef = Path.Combine(outDir, "To_KhungTenMacDinh.dxf");
        SheetBuilder.ExportDxf(p, null, null, s, outDef);
        check("Bỏ khung tên người dùng → khung tên mặc định", B(TextsOf(outDef).Contains("ĐƠN VỊ THIẾT KẾ")), 1, 0);

        // ---- ATTDEF trong model space, đơn vị cm, khung bao toàn tờ co theo khổ ----
        string dxf2 = Path.Combine(tmp, "khung_ba_toan_to_cm.dxf");
        File.WriteAllText(dxf2, ModelAttDefDxf(), new UTF8Encoding(false));
        var rf = TitleBlockImporter.FromFile(dxf2, new TitleBlockImportOptions { Mode = TitleBlockMode.WholeFrame });
        check("Khung bao toàn tờ: đơn vị cm → 420 × 297 mm", rf.Def.W * 1000 + rf.Def.H, 420 * 1000 + 297, 1e-6);
        var ad = rf.Def.Entities.FirstOrDefault(e => e.Tag == "CHU_DAU_TU");
        check("Khung bao toàn tờ: ATTDEF model space → trường CHU_DAU_TU, cao 3 mm", B(ad != null && ad.Field == TitleFields.ChuDauTu && Math.Abs(ad.H - 3) < 1e-9), 1, 0);
        check("Khung bao toàn tờ: bỏ ATTDEF ẩn", rf.Def.Entities.Count(e => e.Tag == "AN"), 0, 0);
        var rot = rf.Def.Entities.FirstOrDefault(e => (e.Text ?? "").Contains("{TRANG}"));
        check("Khung bao toàn tờ: chữ xoay 90° (đọc thêm mã 50)", rot?.Rot ?? -1, 90, 1e-9);
        var pf = ModelBuilder.NewProject("Công trình B");
        pf.Owner = "Bà Nguyễn Thị C";
        pf.TitleBlock.Custom = rf.Def;
        var a3 = new Sheet { Number = "KC-01", Name = "TỜ A3", Paper = "A3" };
        var a1 = new Sheet { Number = "KC-02", Name = "TỜ A1", Paper = "A1" };
        pf.Sheets.Add(a3); pf.Sheets.Add(a1);
        var ra3 = SheetBuilder.CustomTitleRect(pf, a3);
        var ra1 = SheetBuilder.CustomTitleRect(pf, a1);
        check("Khung bao toàn tờ: A3 phủ đúng khổ 420 × 297", Math.Abs(ra3.X1) + Math.Abs(ra3.Y1) + Math.Abs(ra3.X2 - 420) + Math.Abs(ra3.Y2 - 297), 0, 1e-6);
        check("Khung bao toàn tờ: A1 co giãn ×2 (841 rộng → cao 594)", ra1.H, 594, 0.5);
        string outF = Path.Combine(outDir, "To_KhungBaoToanTo_A1.dxf");
        SheetBuilder.ExportDxf(pf, null, null, a1, outF);
        var tf = TextsOf(outF);
        check("Khung bao toàn tờ: DXF có chủ đầu tư + 'Tờ 2/2'", B(tf.Contains("Bà Nguyễn Thị C") && tf.Contains("Tờ 2/2")), 1, 0);
        var prims = SheetBuilder.TitleBlock(pf, a1).GetPrimitives();
        check("Khung bao toàn tờ: không vẽ khung viền trong mặc định", B(!SheetBuilder.TitleBlock(pf, a1).GetPrimitives().Any(x => x.Text == "Lập bằng Kết cấu VXT")), 1, 0);
        check("Khung bao toàn tờ: có hình", B(prims.Count > 0), 1, 0);

        // lưu trong gói mẫu: chữ gán trường giữ lại
        var pk = new TemplatePackage { Name = "Khung tên DXF", TitleBlockDef = def };
        string pkp = Path.Combine(tmp, "khung_dxf" + TemplateService.Extension);
        TemplateService.Save(pkp, pk);
        var bk = TemplateService.Load(pkp).TitleBlockDef;
        check("Gói mẫu: khung tên DXF giữ thực thể + trường", B(bk.Entities.Count == def.Entities.Count && bk.Entities.Count(e => e.Field.Length > 0) == 2 && Math.Abs(bk.W - 180) < 1e-9), 1, 0);
    }

    // ==================================================================
    // Khung tên ảnh
    // ==================================================================
    private static void ImageTitle(Action<string, double, double, double> check, string tmp)
    {
        string png = Path.Combine(tmp, "khung_anh.png");
        File.WriteAllBytes(png, Convert.FromBase64String(TinyPng));
        var (wmm, hmm) = TitleBlockImporter.ImageSizeMm(File.ReadAllBytes(png));
        check("Ảnh: kích thước PNG 2 px ở 96 dpi → mm", wmm, 2 / 96.0 * 25.4, 1e-9);
        var res = TitleBlockImporter.FromFile(png);
        check("Ảnh: lưu base64", B(res.Def.Kind == TitleBlockKind.Image && res.Def.ImageData == TinyPng), 1, 0);
        var def = TitleBlockImporter.FromImage(TinyPng, 180, 45);
        check("Ảnh: 9 chữ trường đặt sẵn, gốc góc phải dưới", B(def.Fields.Count == 9 && Math.Abs(def.MaxX) < 1e-9 && Math.Abs(def.MinX + 180) < 1e-9), 1, 0);
        var p = ModelBuilder.NewProject("CÔNG TRÌNH ẢNH");
        p.TitleBlock.Custom = def;
        var s = new Sheet { Number = "KC-03", Name = "CHI TIẾT", Paper = "A3", Date = "01/01/2026" };
        p.Sheets.Add(s);
        var ir = SheetBuilder.CustomTitleImageRect(p, s);
        check("Ảnh: ô ảnh 180 × 45 trên giấy", ir.W * 1000 + ir.H, 180 * 1000 + 45, 1e-6);
        var tx = SheetBuilder.TitleBlock(p, s, "1/50").GetPrimitives().Where(x => !string.IsNullOrEmpty(x.Text)).Select(x => x.Text).ToList();
        check("Ảnh: chữ trường thay giá trị (tên, số hiệu, tỷ lệ)", B(tx.Contains("CÔNG TRÌNH ẢNH") && tx.Contains("KC-03") && tx.Contains("TỶ LỆ: 1/50")), 1, 0);
        var pk = new TemplatePackage { Name = "Ảnh", TitleBlockDef = def };
        string pp = Path.Combine(tmp, "anh" + TemplateService.Extension);
        TemplateService.Save(pp, pk);
        var back = TemplateService.Load(pp).TitleBlockDef;
        check("Ảnh: gói mẫu giữ ảnh base64 + chữ trường", B(back.ImageData == TinyPng && back.Fields.Count == 9 && back.Kind == TitleBlockKind.Image && Math.Abs(back.W - 180) < 1e-9), 1, 0);
    }

    // ==================================================================
    // DWG (ACadSharp)
    // ==================================================================
    private static void Dwg(Action<string, double, double, double> check, string outDir, string tmp)
    {
        try
        {
            var doc = new CadDocument(ACadVersion.AC1032);
            doc.Header.InsUnits = UnitsType.Millimeters;
            var lay = new Layer("KHUNG_TEN_KH") { Color = new Color(7) };
            doc.Layers.Add(lay);
            var blk = new BlockRecord("TITLE_BLOCK");
            void Ln(double x1, double y1, double x2, double y2) => blk.Entities.Add(new Line { StartPoint = new XYZ(x1, y1, 0), EndPoint = new XYZ(x2, y2, 0) });
            Ln(0, 0, 160, 0); Ln(160, 0, 160, 35); Ln(160, 35, 0, 35); Ln(0, 35, 0, 0);
            blk.Entities.Add(new TextEntity { Value = "{TEN_CONG_TRINH}", InsertPoint = new XYZ(5, 25, 0), Height = 3.5 });
            blk.Entities.Add(new AttributeDefinition { Tag = "DRAWING_NO", Value = "SỐ", InsertPoint = new XYZ(130, 5, 0), Height = 4, Prompt = "Số hiệu" });
            doc.BlockRecords.Add(blk);
            var ins = new Insert(blk) { InsertPoint = new XYZ(0, 0, 0), Layer = lay };
            foreach (var a in ins.Attributes) a.Value = "SỐ CŨ";
            doc.Entities.Add(ins);
            string dwg = Path.Combine(outDir, "KhungTen_NguoiDung.dwg");
            DwgReader.WriteDwg(doc, dwg);

            DwgReader.Register();
            CadFileReaders.DwgToDxf = DwgReader.ConvertToDxf;
            var res = TitleBlockImporter.FromFile(dwg);
            foreach (var w in res.Warnings) Console.WriteLine("   ! " + w);
            Console.WriteLine("   · khung tên DWG: " + res.Info);
            check("Khung tên DWG: block TITLE_BLOCK", B(res.UsedBlock == "TITLE_BLOCK"), 1, 0);
            check("Khung tên DWG: rộng 160 mm", res.Def.W, 160, 1e-6);
            check("Khung tên DWG: ATTRIB DRAWING_NO → SO_HIEU, chữ {TEN_CONG_TRINH}",
                B(res.Def.Entities.Any(e => e.Field == TitleFields.SoHieu) && res.Def.Entities.Any(e => (e.Text ?? "").Contains("{TEN_CONG_TRINH}"))), 1, 0);

            // không có bộ chuyển DWG → DXF: vẫn đọc qua bộ đọc DWG
            CadFileReaders.DwgToDxf = null;
            var res2 = TitleBlockImporter.FromFile(dwg);
            check("Khung tên DWG (không chuyển DXF): vẫn nhận 2 trường", res2.FieldCount, 2, 0);

            var p = ModelBuilder.NewProject("CÔNG TRÌNH DWG");
            p.TitleBlock.Custom = res.Def;
            var s = new Sheet { Number = "KC-11", Name = "DWG", Paper = "A3" };
            p.Sheets.Add(s);
            string outDxf = Path.Combine(outDir, "To_KhungTenDWG.dxf");
            SheetBuilder.ExportDxf(p, null, null, s, outDxf);
            var t = TextsOf(outDxf);
            check("Khung tên DWG: DXF tờ có tên công trình + số hiệu KC-11", B(t.Contains("CÔNG TRÌNH DWG") && t.Contains("KC-11") && !t.Contains("SỐ CŨ")), 1, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine("   LOI khung tên DWG: " + ex);
            check("Khung tên DWG: không ngoại lệ", 0, 1, 0);
        }
    }
}

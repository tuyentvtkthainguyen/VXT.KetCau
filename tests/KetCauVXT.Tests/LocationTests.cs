using System.IO.Compression;
using System.Text;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng địa danh – vùng gió (QCVN 02:2022/BXD): bảng vùng, 34 tỉnh/thành, tìm không dấu,
/// nhập CSV/XLSX, lưu/mở JSON, áp vùng gió vào dự án.</summary>
public static class LocationTests
{
    public static void Run(Action<string, double, double, double> check, string tmpDir)
    {
        Directory.CreateDirectory(tmpDir);

        // ---- bảng vùng gió ----
        double[] w0 = { 0.65, 0.95, 1.25, 1.55, 1.85 }, v3 = { 36, 44, 50, 55, 61 }, v10 = { 26, 31, 36, 39, 43 };
        string[] zs = { "I", "II", "III", "IV", "V" };
        check("Địa danh: số vùng gió", WindZones.All.Count, 5, 0);
        for (int i = 0; i < 5; i++)
        {
            check($"Vùng {zs[i]}: W0 (kN/m²)", WindZones.W0(zs[i]), w0[i], 1e-9);
            check($"Vùng {zs[i]}: V3s,50 (m/s)", WindZones.V3s(zs[i]), v3[i], 1e-9);
            check($"Vùng {zs[i]}: V10m,50 (m/s)", WindZones.V10m(zs[i]), v10[i], 1e-9);
        }
        check("Vùng: chuẩn hoá 'vùng iii' → III", WindZones.Normalize("vùng iii") == "III" ? 1 : 0, 1, 0);
        check("Vùng: '2' → II", WindZones.Normalize("2") == "II" ? 1 : 0, 1, 0);
        check("Vùng: 'IIA' (kiểu cũ) không quy đổi", WindZones.Normalize("IIA") == "" ? 1 : 0, 1, 0);
        check("Vùng: W0 = 125 daN/m² → III", WindZones.FromW0(125)?.Zone == "III" ? 1 : 0, 1, 0);
        check("Vùng II: mô tả", WindZones.Find("II").Describe().Contains("0,95 kN/m² (95 daN/m²), V3s,50 = 44 m/s") ? 1 : 0, 1, 0);
        check("Dạng địa hình: 3 dạng A/B/C", TerrainInfo.All.Count, 3, 0);

        // ---- 34 tỉnh/thành ----
        check("Địa danh: số tỉnh/thành từ 01/7/2025", LocationDb.Provinces.Count, 34, 0);
        check("Địa danh: 23 đơn vị hợp nhất", LocationDb.Provinces.Count(p => p.Merged), 23, 0);
        check("Địa danh: tổng số tỉnh cũ = 63", LocationDb.Provinces.Sum(p => p.Former.Count), 63, 0);
        check("Địa danh: tên tỉnh không trùng", LocationDb.Provinces.Select(p => LocationDb.ProvinceKey(p.Name)).Distinct().Count(), 34, 0);
        check("Bắc Kạn → Thái Nguyên", LocationDb.FindProvince("Bắc Kạn")?.Name == "Thái Nguyên" ? 1 : 0, 1, 0);
        check("'tinh bac kan' (không dấu) → Thái Nguyên", LocationDb.FindProvince("tinh bac kan")?.Name == "Thái Nguyên" ? 1 : 0, 1, 0);
        check("Vĩnh Phúc → Phú Thọ", LocationDb.FindProvince("Vĩnh Phúc")?.Name == "Phú Thọ" ? 1 : 0, 1, 0);
        check("Bà Rịa - Vũng Tàu → TP Hồ Chí Minh", LocationDb.FindProvince("Ba Ria - Vung Tau")?.Name == "TP Hồ Chí Minh" ? 1 : 0, 1, 0);
        check("Thừa Thiên Huế → Huế", LocationDb.FindProvince("Thừa Thiên Huế")?.Name == "Huế" ? 1 : 0, 1, 0);
        check("Kiên Giang → An Giang", LocationDb.FindProvince("Kiên Giang")?.Name == "An Giang" ? 1 : 0, 1, 0);
        check("Tìm tỉnh 'bac kan' → 1 kết quả", LocationDb.SearchProvinces("bac kan").Count, 1, 0);
        check("Tìm tỉnh 'thai nguyen' → Thái Nguyên", LocationDb.SearchProvinces("thai nguyen").FirstOrDefault()?.Name == "Thái Nguyên" ? 1 : 0, 1, 0);

        // ---- nhập CSV (dấu ;) ----
        string csv1 = Path.Combine(tmpDir, "diadanh_semicolon.csv");
        File.WriteAllText(csv1,
            "Tỉnh/Thành phố;Tỉnh cũ;Địa danh (xã, phường, huyện);Vùng gió;W0 (kN/m²);agR (g);Loại nền;Ghi chú\n" +
            "Thái Nguyên;Bắc Kạn;Xã Thử A;I;;0,0500;C;\n" +
            ";Thái Nguyên;Phường Thử B;II;0,95;;;ô tỉnh để trống = theo dòng trên\n" +
            "Thái Nguyên;;Xã Ví Dụ;II;;;;VÍ DỤ – xoá trước khi dùng\n" +
            "Hà Nội;;Phường Thử C;;125;;;W0 theo daN/m²\n" +
            "Hà Nội;;Xã Không Số;;;;;\n", new UTF8Encoding(true));
        var db = new LocationDb();
        var r1 = db.ImportCsv(csv1);
        check("CSV ';': thêm 3 địa danh", r1.Added, 3, 0);
        check("CSV ';': bỏ qua dòng ví dụ + dòng không số liệu", r1.Skipped, 2, 0);
        var a = db.Find("Thái Nguyên", "thu a");
        check("CSV ';': tìm 'Xã Thử A' bằng 'thu a'", a != null ? 1 : 0, 1, 0);
        check("CSV ';': Xã Thử A vùng I → W0", a?.EffectiveW0 ?? 0, 0.65, 1e-9);
        check("CSV ';': Xã Thử A agR", a?.AgR ?? 0, 0.05, 1e-9);
        check("CSV ';': Xã Thử A tỉnh cũ Bắc Kạn", a?.FormerProvince == "Bắc Kạn" ? 1 : 0, 1, 0);
        var b = db.Find("Thái Nguyên", "Phường Thử B");
        check("CSV ';': ô tỉnh trống lấy theo dòng trên", b != null ? 1 : 0, 1, 0);
        check("CSV ';': W0 trùng bảng vùng không lưu riêng", b?.W0 ?? -1, 0, 0);
        var c = db.Find("Hà Nội", "Phường Thử C");
        check("CSV ';': W0 = 125 daN/m² → vùng III", c?.WindZone == "III" ? 1 : 0, 1, 0);
        check("Tìm 'bac kan' → Xã Thử A", db.Search("bac kan").Count(e => e.Name == "Xã Thử A"), 1, 0);
        check("Tìm 'thai nguyen' → 2 địa danh", db.Search("thai nguyen").Count, 2, 0);
        check("ForProvince('Bắc Kạn') = ForProvince('Thái Nguyên')", db.ForProvince("Bắc Kạn").Count, 2, 0);

        // ---- nhập CSV (dấu ,) – cập nhật địa danh đã có ----
        string csv2 = Path.Combine(tmpDir, "diadanh_comma.csv");
        File.WriteAllText(csv2,
            "Tỉnh,Tỉnh cũ,Huyện cũ,Xã/phường,Vùng gió,agR (g),Ghi chú\n" +
            "Thái Nguyên,Bắc Kạn,,Xã Thử A,II,\"0,0600\",\"đã sửa, có dấu phẩy\"\n" +
            "Bắc Ninh,Bắc Giang,Huyện Thử,Xã Thử D,II,,\n", Encoding.UTF8);
        var r2 = db.ImportCsv(csv2);
        check("CSV ',': 1 thêm mới", r2.Added, 1, 0);
        check("CSV ',': 1 cập nhật", r2.Updated, 1, 0);
        a = db.Find("Thái Nguyên", "Xã Thử A");
        check("CSV ',': Xã Thử A cập nhật vùng II", a?.EffectiveW0 ?? 0, 0.95, 1e-9);
        check("CSV ',': agR dấu phẩy trong ngoặc kép", a?.AgR ?? 0, 0.06, 1e-9);
        check("CSV ',': ghi chú có dấu phẩy", a?.Note == "đã sửa, có dấu phẩy" ? 1 : 0, 1, 0);
        var d = db.Find("Bắc Ninh", "Xã Thử D");
        check("CSV ',': huyện cũ được ghi nhận", d?.FormerDistrict == "Huyện Thử" ? 1 : 0, 1, 0);

        // ---- Windows-1258 (không BOM, byte không hợp lệ UTF-8) ----
        string csv3 = Path.Combine(tmpDir, "diadanh_1258.csv");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        // Windows-1258: nguyên âm cơ sở (ă, ư…) dựng sẵn + dấu thanh tổ hợp (U+0300, U+0301, U+0303, U+0309, U+0323)
        var cp1258 = Encoding.GetEncoding(1258, EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback);
        File.WriteAllBytes(csv3, cp1258.GetBytes("Ti\u0309nh;\u0110i\u0323a danh;V\u00F9ng gi\u00F3\nCao B\u0103\u0300ng;Xa\u0303 Th\u01B0\u0309 E;I\n"));
        var r3 = db.ImportCsv(csv3);
        check("CSV Windows-1258: đọc được dấu tiếng Việt", r3.Added == 1 && db.Find("Cao Bằng", "Xã Thử E") != null ? 1 : 0, 1, 0);

        // ---- file CSV mẫu: chỉ có dòng ví dụ → không thêm gì ----
        string tpl = Path.Combine(tmpDir, "diadanh_mau.csv");
        LocationDb.WriteTemplateCsv(tpl);
        var rt = new LocationDb().ImportCsv(tpl);
        check("CSV mẫu: dòng ví dụ bị bỏ qua", rt.Added + rt.Updated, 0, 0);

        // ---- xuất CSV rồi nhập lại ----
        string exp = Path.Combine(tmpDir, "diadanh_xuat.csv");
        LocationDb.ExportCsv(exp, db.Entries);
        var db2 = new LocationDb();
        var rx = db2.ImportCsv(exp);
        check("Xuất/nhập CSV: số địa danh", db2.Entries.Count, db.Entries.Count, 0);
        check("Xuất/nhập CSV: agR giữ nguyên", db2.Find("Thái Nguyên", "Xã Thử A")?.AgR ?? 0, 0.06, 1e-9);

        // ---- XLSX tối thiểu (sharedStrings + inline + số) ----
        string xlsx = Path.Combine(tmpDir, "diadanh.xlsx");
        WriteMiniXlsx(xlsx);
        var db3 = new LocationDb();
        var r4 = db3.ImportXlsx(xlsx);
        check("XLSX: thêm 2 địa danh", r4.Added, 2, 0);
        check("XLSX: Phường Thử F vùng IV", db3.Find("Đà Nẵng", "Phường Thử F")?.EffectiveW0 ?? 0, 1.55, 1e-9);
        check("XLSX: Quảng Nam → Đà Nẵng, agR số", db3.Find("Đà Nẵng", "Xã Thử G")?.AgR ?? 0, 0.0797, 1e-9);

        // ---- lưu / mở JSON ----
        string json = Path.Combine(tmpDir, "DiaDanh.json");
        db.Save(json);
        var back = LocationDb.Load(json);
        check("JSON: số địa danh", back.Entries.Count, db.Entries.Count, 0);
        var ab = back.Find("Thái Nguyên", "Xã Thử A");
        check("JSON: vùng/agR/tỉnh cũ", ab != null && ab.WindZone == "II" && Math.Abs(ab.AgR - 0.06) < 1e-12 && ab.FormerProvince == "Bắc Kạn" ? 1 : 0, 1, 0);
        check("JSON: file chưa có → CSDL rỗng", LocationDb.Load(Path.Combine(tmpDir, "khong_co.json")).Entries.Count, 0, 0);

        // ---- áp vùng gió vào dự án ----
        var p = new Project();
        p.Wind.W0 = 0.65;
        bool ok = WindZones.Apply(p, "II");
        check("Dự án: áp vùng II → Wind.W0", ok ? p.Wind.W0 : -1, 0.95, 1e-9);
        check("Dự án: Wind.Zone = II", p.Wind.Zone == "II" ? 1 : 0, 1, 0);
        p.LocationProvince = "Thái Nguyên"; p.LocationName = "Xã Thử A";
        var pc = p.Clone();
        check("Dự án: lưu vị trí + vùng qua JSON", pc.LocationName == "Xã Thử A" && pc.LocationProvince == "Thái Nguyên" && pc.Wind.Zone == "II" ? 1 : 0, 1, 0);
    }

    private static void WriteMiniXlsx(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Put(string name, string xml)
        {
            var e = zip.CreateEntry(name);
            using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
            w.Write(xml);
        }
        const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Put("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"/>");
        Put("xl/workbook.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"{ns}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Bảng 5.1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        Put("xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
        string[] ss = { "Tỉnh/Thành phố", "Tỉnh cũ", "Địa danh (xã, phường, huyện)", "Vùng gió", "agR (g)", "Đà Nẵng", "Phường Thử F", "IV", "Quảng Nam" };
        Put("xl/sharedStrings.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><sst xmlns=\"{ns}\">" + string.Concat(ss.Select(s => $"<si><t>{s}</t></si>")) + "</sst>");
        Put("xl/worksheets/sheet1.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"{ns}\"><sheetData>" +
            "<row r=\"1\"><c r=\"A1\"><is><t>BẢNG PHÂN VÙNG (thử)</t></is></c></row>" +
            "<row r=\"2\"><c r=\"A2\" t=\"s\"><v>0</v></c><c r=\"B2\" t=\"s\"><v>1</v></c><c r=\"C2\" t=\"s\"><v>2</v></c><c r=\"D2\" t=\"s\"><v>3</v></c><c r=\"E2\" t=\"s\"><v>4</v></c></row>" +
            "<row r=\"3\"><c r=\"A3\" t=\"s\"><v>5</v></c><c r=\"C3\" t=\"s\"><v>6</v></c><c r=\"D3\" t=\"s\"><v>7</v></c></row>" +
            "<row r=\"5\"><c r=\"B5\" t=\"s\"><v>8</v></c><c r=\"C5\" t=\"inlineStr\"><is><t>Xã Thử G</t></is></c><c r=\"D5\" t=\"inlineStr\"><is><t>II</t></is></c><c r=\"E5\"><v>0.0797</v></c></row>" +
            "</sheetData></worksheet>");
    }
}

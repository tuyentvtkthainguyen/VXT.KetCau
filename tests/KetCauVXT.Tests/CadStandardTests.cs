using System.Globalization;
using System.Text;
using System.Text.Json;
using KetCauVXT.Drawing;
using KetCauVXT.Import;
using KetCauVXT.Import.Arch;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Chuẩn layer CAD: quy tắc nhận diện layer (chứa, mẫu *, biểu thức chính quy, ưu tiên, tiền tố xref), lưu/mở JSON,
/// nhập bản vẽ kiến trúc với bảng gán layer tự đặt (tường, dầm, sàn, cột HATCH, tường đa tuyến có bề rộng, MTEXT \p),
/// chữ VNI/TCVN3, đổi tên layer khi xuất DXF (bảng LAYER/LTYPE/STYLE), font Times New Roman.</summary>
public static class CadStandardTests
{
    private static double B(bool b) => b ? 1 : 0;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var prevActive = CadStandard.Active;
        CadStandard.Activate(null);
        try
        {
            Rules(check);
            Json(check);
            ArchImport(check);
            Text(check);
            Output(check, outDir);
        }
        finally { CadStandard.Activate(prevActive); CadStandard.UserFolderOverride = null; }
    }

    // ------------------------------------------------------------------
    private static void Rules(Action<string, double, double, double> check)
    {
        check("Layer: quy tắc 'chứa' bỏ dấu, không phân biệt hoa thường", B(new LayerRule("TUONG", LayerMatchKind.Contains, CadCategory.Tuong, 1).IsMatch("kt-Tường xây")), 1, 0);
        var wc = new LayerRule("A-WALL*", LayerMatchKind.Wildcard, CadCategory.Tuong, 1);
        check("Layer: mẫu * khớp đầu tên", B(wc.IsMatch("a-wall-ext") && !wc.IsMatch("X-A-WALL")), 1, 0);
        check("Layer: mẫu ? một ký tự", B(new LayerRule("KT?COT", LayerMatchKind.Wildcard, CadCategory.Cot, 1).IsMatch("KT_COT") && !new LayerRule("KT?COT", LayerMatchKind.Wildcard, CadCategory.Cot, 1).IsMatch("KT__COT")), 1, 0);
        var def = CadStandard.DefaultInputRules();
        string Cls(string layer) => CadStandard.Classify(layer, def, null, out _);
        check("Layer mặc định: KC-DAM → Dầm (biểu thức chính quy)", B(Cls("KC-DAM") == CadCategory.Dam), 1, 0);
        check("Layer mặc định: SAN-BTCT → Sàn, 'SANH' không phải sàn", B(Cls("SAN-BTCT") == CadCategory.San && Cls("SANH") != CadCategory.San), 1, 0);
        check("Layer mặc định: KT-TUONG → Tường, KT-CAUTHANG → Cầu thang, A-DOOR → Cửa", B(Cls("KT-TUONG") == CadCategory.Tuong && Cls("KT-CAUTHANG") == CadCategory.Thang && Cls("A-DOOR") == CadCategory.Cua), 1, 0);
        check("Layer mặc định: DEFPOINTS → Bỏ qua, layer lạ → không nhận", B(Cls("Defpoints") == CadCategory.BoQua && Cls("ABC123") == ""), 1, 0);
        // ưu tiên
        var rules = new List<LayerRule>
        {
            new("TUONG", LayerMatchKind.Contains, CadCategory.Tuong, 50),
            new("TUONG-HATCH", LayerMatchKind.Exact, CadCategory.BoQua, 90),
        };
        check("Layer: ưu tiên lớn hơn thắng (TUONG-HATCH → Bỏ qua)", B(CadStandard.Classify("TUONG-HATCH", rules, null, out _) == CadCategory.BoQua && CadStandard.Classify("TUONG-110", rules, null, out _) == CadCategory.Tuong), 1, 0);
        // xref
        var ex = new LayerRule("KT-TUONG", LayerMatchKind.Exact, CadCategory.Tuong, 1);
        check("Layer: tiền tố xref 'NHA|' và xref đã bind 'NHA$0$' được bỏ khi so", B(ex.IsMatch("NHA|KT-TUONG") && ex.IsMatch("NHA$0$KT-TUONG") && !ex.IsMatch("KT-TUONG-2")), 1, 0);
        // gán trực tiếp
        var std = CadStandard.Default();
        std.SetMapping("KT-TUONG", CadCategory.Cot);
        check("Layer: gán trực tiếp ưu tiên hơn quy tắc", B(std.Classify("KT-TUONG", out var why) == CadCategory.Cot && why == "gán trực tiếp"), 1, 0);
        std.SetMapping("KT-TUONG", "");
        check("Layer: xoá gán trực tiếp → về quy tắc", B(std.Classify("KT-TUONG", out _) == CadCategory.Tuong), 1, 0);
        check("Layer: chuỗi nhóm cố định dùng chung với add-in", B(string.Join(",", CadCategory.All) == "Tuong,Cot,Dam,San,Truc,Cua,Thang,TenPhong,BoQua"), 1, 0);
        check("Layer: biểu thức chính quy lỗi được báo", B(new LayerRule("([", LayerMatchKind.Regex, CadCategory.Tuong, 1).Validate() != null), 1, 0);
        var kw = CadStandard.RulesFromKeywords("TUONG,XAY", "TRUC", "", "CUA", "");
        check("Từ khoá kiểu cũ → quy tắc 'chứa'", kw.Count, 4, 0);
    }

    // ------------------------------------------------------------------
    private static void Json(Action<string, double, double, double> check)
    {
        string dir = Path.Combine(Path.GetTempPath(), "kcvxt_cadstd_" + Guid.NewGuid().ToString("N"));
        CadStandard.UserFolderOverride = dir;
        try
        {
            check("Chuẩn mặc định khi chưa có file", B(!File.Exists(CadStandard.UserFilePath) && CadStandard.LoadUserDefault().OutputLayers.Count == 9), 1, 0);
            var s = CadStandard.Default();
            s.OutputLayers = CadStandard.PresetTcvn(); s.OutputPreset = CadStandard.PresetTcvnName;
            s.FontName = CadStandard.FontTimes; s.ReportFont = CadStandard.FontArial;
            s.SetMapping("A_WALL_EXT", CadCategory.Tuong);
            s.InputRules.Add(new LayerRule("S-BEAM*", LayerMatchKind.Wildcard, CadCategory.Dam, 77));
            s.AskLayersBeforeExport = false;
            CadStandard.SaveUserDefault(s);
            var t = CadStandard.LoadUserDefault();
            check("JSON: lưu/mở mặc định người dùng (thư mục đổi được để kiểm thử)", B(File.Exists(Path.Combine(dir, "CadStandard.json"))), 1, 0);
            check("JSON: giữ tên layer xuất, font, gán, quy tắc", B(t.Output(Lay.BeTong).Name == "KC-BETONG" && t.FontName == CadStandard.FontTimes && t.ReportFont == CadStandard.FontArial
                && t.InputLayerMap.Any(m => m.Layer == "A_WALL_EXT" && m.Category == CadCategory.Tuong) && t.InputRules.Any(r => r.Pattern == "S-BEAM*" && r.Match == LayerMatchKind.Wildcard && r.Priority == 77)
                && !t.AskLayersBeforeExport), 1, 0);
            string json = File.ReadAllText(CadStandard.UserFilePath);
            check("JSON: kiểu so khớp ghi dạng chuỗi (\"Wildcard\")", B(json.Contains("\"Wildcard\"") && json.Contains("\"OutputLayers\"") && json.Contains("\"InputRules\"")), 1, 0);
            var min = CadStandard.FromJson("{ \"FontName\": \"times new roman\", \"OutputLayers\": [ { \"Key\": \"THEP\", \"Name\": \"K:T*\", \"Color\": 999 } ] }");
            check("JSON tối giản/sửa tay: bổ sung layer thiếu, sửa tên/màu sai, chuẩn hoá font", B(min.OutputLayers.Count == 9 && min.Output(Lay.Thep).Name == "K_T_" && min.Output(Lay.Thep).Color == 7 && min.FontName == CadStandard.FontTimes), 1, 0);
            CadStandard.ResetUserDefault();
            check("Khôi phục mặc định: xoá file người dùng", B(!File.Exists(CadStandard.UserFilePath)), 1, 0);
            // dự án
            var p = ModelBuilder.NewProject("Chuẩn layer");
            var p2 = JsonSerializer.Deserialize<Project>(JsonSerializer.Serialize(p, Project.JsonOptions), Project.JsonOptions);
            check("Dự án: CadStandard = null (dùng mặc định) sau khi lưu/mở", B(p2.CadStandard == null && p2.ArchLayerMap != null), 1, 0);
            p.CadStandard = t; p.ArchLayerMap.Add(new LayerMapEntry("KC-DAM", CadCategory.Dam));
            var p3 = JsonSerializer.Deserialize<Project>(JsonSerializer.Serialize(p, Project.JsonOptions), Project.JsonOptions);
            check("Dự án: lưu chuẩn riêng + bảng gán layer đã dùng", B(p3.CadStandard?.Output(Lay.Chu)?.Name == "KC-CHU" && p3.ArchLayerMap.Count == 1 && CadStandard.EffectiveFor(p3).FontName == CadStandard.FontTimes), 1, 0);
            var old = JsonSerializer.Deserialize<Project>("{ \"Name\": \"cũ\" }", Project.JsonOptions);
            check("Dự án cũ (không có khoá CadStandard) mở được", B(old.CadStandard == null && old.ArchLayerMap != null && old.ArchLayerMap.Count == 0), 1, 0);
        }
        finally
        {
            CadStandard.UserFolderOverride = null;
            try { Directory.Delete(dir, true); } catch (Exception) { }
        }
    }

    // ------------------------------------------------------------------
    /// <summary>Bản vẽ kiến trúc 1 tầng 6×10 m, layer tự đặt: tường A_WALL_EXT (2 nét) + 1 tường đa tuyến bề rộng 110,
    /// trục AXIS_X, dầm KC-DAM (2 nét, y = 5 m), sàn SAN-BTCT (bao + 1 lỗ), cột HATCH trên COT, tên phòng MTEXT có mã \pxqc;.</summary>
    public static string BuildArchDxf()
    {
        var sb = new StringBuilder();
        void P(int code, object v) { sb.Append(code).Append('\n').Append(Convert.ToString(v, Inv)).Append('\n'); }
        void Line(string layer, double x1, double y1, double x2, double y2) { P(0, "LINE"); P(8, layer); P(10, x1); P(20, y1); P(30, 0); P(11, x2); P(21, y2); P(31, 0); }
        void Rect(string layer, double x0, double y0, double x1, double y1)
        {
            P(0, "LWPOLYLINE"); P(8, layer); P(90, 4); P(70, 1);
            foreach (var (x, y) in new[] { (x0, y0), (x1, y0), (x1, y1), (x0, y1) }) { P(10, x); P(20, y); }
        }
        P(0, "SECTION"); P(2, "HEADER"); P(9, "$ACADVER"); P(1, "AC1015"); P(9, "$INSUNITS"); P(70, 4); P(0, "ENDSEC");
        P(0, "SECTION"); P(2, "TABLES"); P(0, "TABLE"); P(2, "LAYER"); P(70, 7);
        foreach (var (n, c) in new[] { ("A_WALL_EXT", 7), ("AXIS_X", 1), ("KC-DAM", 3), ("SAN-BTCT", 5), ("COT", 2), ("TEXT", 7), ("NOI-THAT-TAT", -8) })
        { P(0, "LAYER"); P(2, n); P(70, 0); P(62, c); P(6, "CONTINUOUS"); }
        P(0, "ENDTAB"); P(0, "ENDSEC");
        P(0, "SECTION"); P(2, "ENTITIES");
        const double t = 110;
        // tường bao 220: nét ngoài + nét trong
        Line("A_WALL_EXT", -t, -t, 6000 + t, -t); Line("A_WALL_EXT", t, t, 6000 - t, t);
        Line("A_WALL_EXT", -t, 10000 + t, 6000 + t, 10000 + t); Line("A_WALL_EXT", t, 10000 - t, 6000 - t, 10000 - t);
        Line("A_WALL_EXT", -t, -t, -t, 10000 + t); Line("A_WALL_EXT", t, t, t, 10000 - t);
        Line("A_WALL_EXT", 6000 + t, -t, 6000 + t, 10000 + t); Line("A_WALL_EXT", 6000 - t, t, 6000 - t, 10000 - t);
        // tường ngăn vẽ 1 đa tuyến bề rộng 110
        P(0, "LWPOLYLINE"); P(8, "A_WALL_EXT"); P(90, 2); P(70, 0); P(43, 110); P(10, 3000); P(20, t); P(10, 3000); P(20, 4890);
        // trục
        Line("AXIS_X", 0, -1500, 0, 11500); Line("AXIS_X", 6000, -1500, 6000, 11500);
        Line("AXIS_X", -1500, 0, 7500, 0); Line("AXIS_X", -1500, 10000, 7500, 10000);
        // dầm 220 vẽ 2 nét tại y = 5 m
        Line("KC-DAM", t, 4890, 6000 - t, 4890); Line("KC-DAM", t, 5110, 6000 - t, 5110);
        // sàn: vùng bao + lỗ sàn 1,5 × 2 m
        Rect("SAN-BTCT", 0, 0, 6000, 10000);
        Rect("SAN-BTCT", 1000, 7000, 2500, 9000);
        // cột vẽ bằng HATCH (biên đa tuyến) tại góc (6000;10000)
        P(0, "HATCH"); P(8, "COT"); P(10, 0); P(20, 0); P(30, 0); P(210, 0); P(220, 0); P(230, 1); P(2, "SOLID"); P(70, 1); P(71, 0);
        P(91, 1); P(92, 2); P(72, 0); P(73, 1); P(93, 4);
        foreach (var (x, y) in new[] { (5890.0, 9890.0), (6110.0, 9890.0), (6110.0, 10110.0), (5890.0, 10110.0) }) { P(10, x); P(20, y); }
        P(97, 0); P(75, 0); P(76, 1); P(98, 0);
        // tên phòng MTEXT có mã định dạng đoạn văn
        P(0, "MTEXT"); P(8, "TEXT"); P(10, 3000); P(20, 7500); P(30, 0); P(40, 250); P(1, "{\\pxqc;PHÒNG KHÁCH}");
        // nội thất trên layer đang tắt (bỏ qua)
        Line("NOI-THAT-TAT", 500, 500, 2500, 500);
        // tên hình
        P(0, "TEXT"); P(8, "TEXT"); P(10, 2000); P(20, -2500); P(30, 0); P(40, 400); P(1, "MẶT BẰNG TẦNG 1");
        P(0, "ENDSEC"); P(0, "EOF");
        return sb.ToString();
    }

    private static void ArchImport(Action<string, double, double, double> check)
    {
        string path = Path.Combine(Path.GetTempPath(), "kcvxt_cadstd_arch.dxf");
        File.WriteAllText(path, BuildArchDxf(), new UTF8Encoding(false));
        var dxf = DxfReader.Read(path);
        check("DXF: HATCH đọc thành miền tô (không gộp vào Entities)", B(dxf.FillEntities.Count == 1 && dxf.FillEntities[0].Closed && dxf.FillEntities[0].Points.Count == 4 && dxf.Entities.All(e => e.SourceType != "HATCH")), 1, 0);
        check("DXF: đa tuyến bề rộng 110 → Width 0,11 m", B(dxf.Entities.Any(e => e.SourceType == "LWPOLYLINE" && Math.Abs(e.Width - 0.11) < 1e-9)), 1, 0);
        check("DXF: MTEXT bỏ mã \\pxqc; (không còn 'xqc')", B(dxf.Entities.Any(e => e.Text == "PHÒNG KHÁCH")), 1, 0);
        check("DXF: màu layer, layer tắt", B(dxf.LayerColors["SAN-BTCT"] == 5 && dxf.OffLayers.Contains("NOI-THAT-TAT")), 1, 0);

        // bảng layer trong file
        var o = new ArchImportOptions
        {
            WallLayers = "", GridLayers = "", ColumnLayers = "", DoorLayers = "", StairLayers = "",
            LayerRules = new List<LayerRule>(),   // không quy tắc nào – chỉ bảng gán trực tiếp
            LayerMap = new List<LayerMapEntry>
            {
                new("A_WALL_EXT", CadCategory.Tuong), new("AXIS_X", CadCategory.Truc), new("KC-DAM", CadCategory.Dam),
                new("SAN-BTCT", CadCategory.San), new("COT", CadCategory.Cot)
            }
        };
        var scan = CadLayerScan.Scan(new[] { path }, o);
        var lw = scan.Layers.FirstOrDefault(l => l.Name == "A_WALL_EXT");
        check("Quét layer: 7 layer, đếm LINE/LWPOLYLINE, nhóm theo bảng gán", B(scan.Layers.Count == 7 && lw != null && lw.Counts["LINE"] == 8 && lw.Counts["LWPOLYLINE"] == 1 && lw.Category == CadCategory.Tuong
            && scan.Layers.First(l => l.Name == "COT").Counts.ContainsKey("HATCH") && scan.Layers.First(l => l.Name == "NOI-THAT-TAT").Off && scan.Segments.Count > 20), 1, 0);
        var scan2 = CadLayerScan.FromEntities(dxf.Entities, o, "x.dwg", dxf.LayerColors);
        check("Quét layer từ List<DxfEntity> (bộ đọc DWG riêng)", B(scan2.Layers.Count == 7 && scan2.Layers.First(l => l.Name == "KC-DAM").Category == CadCategory.Dam), 1, 0);

        var d = ArchPlanReader.Read(path, o);
        check("Nhập KT layer tự đặt: 1 mặt bằng tầng 1", B(d.Floors.Count == 1 && d.Floors[0].FloorNo == 1), 1, 0);
        if (d.Floors.Count == 0) return;
        var f = d.Floors[0];
        check("Nhập KT: tường bao 220 nhận từ layer A_WALL_EXT", B(f.Walls.Count(w => Math.Abs(w.Thickness - 0.22) < 0.005) >= 4), 1, 0);
        check("Nhập KT: tường vẽ đa tuyến bề rộng 110 tại x = 3 m", B(f.Walls.Any(w => w.AlongY && Math.Abs(w.Ax - 3.0) < 0.01 && Math.Abs(w.Thickness - 0.11) < 0.005)), 1, 0);
        check("Nhập KT: trục từ layer AXIS_X (2×2)", B(f.Grids.Count(g => g.Dir == GridDir.X && !g.Auto) == 2 && f.Grids.Count(g => g.Dir == GridDir.Y && !g.Auto) == 2), 1, 0);
        check("Nhập KT: dầm vẽ sẵn KC-DAM rộng 220 tại y = 5 m", B(f.Beams.Count == 1 && Math.Abs(f.Beams[0].Ay - 5.0) < 1e-6 && Math.Abs(f.Beams[0].Width - 0.22) < 1e-6), 1, 0);
        check("Nhập KT: vùng sàn + lỗ sàn từ SAN-BTCT", B(f.SlabRegions.Count == 2 && f.SlabRegions.Count(r => r.IsOpening) == 1), 1, 0);
        check("Nhập KT: cột vẽ bằng HATCH", B(f.Columns.Any(c => Math.Abs(c.X - 6.0) < 0.01 && Math.Abs(c.Y - 10.0) < 0.01 && Math.Abs(c.Bx - 0.22) < 0.005)), 1, 0);
        check("Nhập KT: tên phòng MTEXT → phòng khách", B(f.Rooms.Any(r => r.Kind == RoomKind.Living)), 1, 0);
        check("Nhập KT: ghi lại nhóm đã dùng của layer", B(d.LayerCategories.TryGetValue("KC-DAM", out var cdam) && cdam == CadCategory.Dam), 1, 0);
        check("Nhập KT: không quy tắc → layer không gán không được dùng làm tường", B(ArchPlanReader.Read(path, new ArchImportOptions { WallLayers = "", GridLayers = "", ColumnLayers = "", DoorLayers = "", StairLayers = "", LayerRules = new List<LayerRule>() }).Floors.Count == 0), 1, 0);

        var prop = ArchToStructure.Propose(d, o);
        var lv = prop.Levels[0];
        check("Đề xuất: dầm theo dầm vẽ sẵn (y = 5 m, b = 220)", B(lv.Beams.Any(b => b.Note.Contains("vẽ sẵn") && Math.Abs(b.Ay - 5.0) < 1e-6 && Math.Abs(b.By - 5.0) < 1e-6 && Math.Abs(b.B - 0.22) < 1e-6)), 1, 0);
        check("Đề xuất: lỗ sàn từ layer sàn", B(lv.Holes.Any(h => h.Kind == RoomKind.Void)), 1, 0);
        check("Đề xuất: có ô sàn", B(lv.Slabs.Count >= 2), 1, 0);
        var rep = ArchToStructure.Apply(ModelBuilder.NewProject("KT layer tự đặt"), d, prop, o);
        check("Tạo mô hình từ bản vẽ layer tự đặt: có dầm, sàn, cột", B(rep.Beams > 0 && rep.Slabs > 0 && rep.Columns > 0), 1, 0);
        try { File.Delete(path); } catch (Exception) { }
    }

    // ------------------------------------------------------------------
    private static void Text(Action<string, double, double, double> check)
    {
        check("VNI → Unicode: \"Phoøng nguû\"", B(VniText.Convert("Phoøng nguû") == "Phòng ngủ"), 1, 0);
        check("VNI → Unicode: \"MAËT BAÈNG TAÀNG 2\"", B(VniText.Convert("MAËT BAÈNG TAÀNG 2") == "MẶT BẰNG TẦNG 2"), 1, 0);
        check("VNI → Unicode: ơ, ư, đ (\"Ñöôøng ñoâi\")", B(VniText.Convert("Ñöôøng ñoâi") == "Đường đôi"), 1, 0);
        check("Tự nhận VNI / TCVN3 / Unicode", B(VnText.Fix("Phoøng khaùch") == "Phòng khách" && VnText.Fix("MÆT B»NG TÇNG 3") == "MẶT BẰNG TẦNG 3" && VnText.Fix("Phòng khách") == "Phòng khách"), 1, 0);
        check("Gợi ý font VNI-Times / .VnTime", B(VnText.Fix("Taàng", "VNI") == "Tầng" && VnText.Fix("TÇng", "TCVN3") == "Tầng"), 1, 0);
    }

    // ------------------------------------------------------------------
    private static void Output(Action<string, double, double, double> check, string outDir)
    {
        DxfWriter Fig()
        {
            var w = new DxfWriter(50);
            w.Rect(Lay.BeTong, 0, 0, 2200, 3000);
            w.Line(Lay.Thep, 50, 50, 2150, 50);
            w.Line(Lay.Dai, 50, 50, 50, 2950);
            w.Dim(0, 0, 2200, 0, -400);
            w.Text(Lay.Chu, 0, 3300, w.Th, "DẦM D1 – Ø16");
            w.GridBubble(1100, -1200, "A");
            w.Line(Lay.Truc, 1100, -900, 1100, 3500, "CENTER");
            w.Line(Lay.BeTong, 0, 1500, 2200, 1500, "DASHED");
            w.Rect(Lay.KhungTen, -500, -2000, 2700, 4000);
            w.Line("KC-MOI", 0, 0, 100, 100);
            return w;
        }
        string plain = Fig().ToDxfString();
        var wv = Fig(); wv.Standard = CadStandard.Default();
        check("Chuẩn \"Kết cấu VXT\": DXF giống hệt khi không có chuẩn (trừ layer tự đặt được khai báo)", B(wv.ToDxfString().Replace("  0\r\nLAYER\r\n  2\r\nKC-MOI\r\n 70\r\n0\r\n 62\r\n7\r\n  6\r\nCONTINUOUS\r\n", "")
            .Replace(" 70\r\n10\r\n  0\r\nLAYER", " 70\r\n9\r\n  0\r\nLAYER") == plain), 1, 0);
        var w9 = new DxfWriter(50); w9.Line(Lay.Thep, 0, 0, 1, 1); w9.Text(Lay.Chu, 0, 0, 1, "Thép");
        string a = w9.ToDxfString(); w9.Standard = CadStandard.Default();
        check("Chuẩn \"Kết cấu VXT\": byte giống hệt với layer chuẩn", B(a == w9.ToDxfString()), 1, 0);

        var std = CadStandard.Default();
        std.OutputLayers = CadStandard.PresetTcvn(); std.OutputPreset = CadStandard.PresetTcvnName;
        std.Output(Lay.KyHieu).Name = "KC-CHU";         // gộp KY_HIEU vào KC-CHU
        std.Output(Lay.Dai).Linetype = "HIDDEN";
        std.OutputLayers.Add(new OutputLayer("KC-MOI", "KC-PHU", 30, "DASHDOT", 0.13, "layer thêm"));
        std.FontName = CadStandard.FontTimes;
        var wt = Fig(); wt.Standard = std;
        string s = wt.ToDxfString();
        string fileT = Path.Combine(outDir, "ChuanLayer_TCVN.dxf");
        File.WriteAllText(fileT, s, new UTF8Encoding(false));
        var rd = DxfReader.FromLines(s.Replace("\r\n", "\n").Split('\n'));
        var layers = rd.Entities.Select(e => e.Layer).Distinct().ToList();
        check("Chuẩn TCVN: thực thể ghi tên layer mới (KC-BETONG, KC-THEP…)", B(layers.Contains("KC-BETONG") && layers.Contains("KC-THEP") && layers.Contains("KC-DAI") && layers.Contains("KC-KICHTHUOC") && layers.Contains("KC-TRUC") && !layers.Contains("BE_TONG") && !layers.Contains("THEP")), 1, 0);
        check("Chuẩn TCVN: gộp KY_HIEU → KC-CHU (1 dòng trong bảng LAYER)", B(!layers.Contains("KY_HIEU") && !layers.Contains("KC-KYHIEU") && CountOf(s, "  0\r\nLAYER\r\n  2\r\nKC-CHU\r\n") == 1), 1, 0);
        check("Chuẩn TCVN: bảng LAYER có màu/kiểu nét theo chuẩn", B(s.Contains("  2\r\nKC-DAI\r\n 70\r\n0\r\n 62\r\n3\r\n  6\r\nHIDDEN\r\n") && s.Contains("  2\r\nKC-PHU\r\n 70\r\n0\r\n 62\r\n30\r\n  6\r\nDASHDOT\r\n")), 1, 0);
        check("Chuẩn TCVN: bảng LTYPE định nghĩa đủ HIDDEN, DASHDOT (5 kiểu)", B(s.Contains("  2\r\nLTYPE\r\n 70\r\n5\r\n") && s.Contains("  0\r\nLTYPE\r\n  2\r\nHIDDEN\r\n") && s.Contains("  0\r\nLTYPE\r\n  2\r\nDASHDOT\r\n")), 1, 0);
        check("Chuẩn TCVN: layer tự đặt KC-MOI đổi thành KC-PHU", B(layers.Contains("KC-PHU") && !layers.Contains("KC-MOI")), 1, 0);
        check("Font Times New Roman → STYLE times.ttf; mặc định arial.ttf", B(s.Contains("  3\r\ntimes.ttf\r\n") && !s.Contains("arial.ttf") && plain.Contains("  3\r\narial.ttf\r\n")), 1, 0);
        check("Chữ tiếng Việt vẫn mã hoá \\U+XXXX", B(s.Contains("D\\U+1EA6M D1")), 1, 0);
        check("Mọi layer thực thể đều có trong bảng LAYER", B(layers.All(l => s.Contains("  0\r\nLAYER\r\n  2\r\n" + l + "\r\n"))), 1, 0);
        check("Mọi kiểu nét thực thể đều có trong bảng LTYPE", B(rd.Entities.Select(e => e.LineType).Where(x => !string.IsNullOrEmpty(x)).Distinct().All(x => s.Contains("  0\r\nLTYPE\r\n  2\r\n" + x + "\r\n"))), 1, 0);
        check("Layer liệt kê trước khi xuất (UsedLayers)", B(Fig().UsedLayers().Contains("KC-MOI") && ExportLayerProbe.ForWriters(new[] { Fig() }).Contains(Lay.Truc)), 1, 0);

        // chuẩn đang áp dụng toàn cục + tạm thời cho 1 lần xuất
        using (CadStandard.Use(std))
        {
            check("CadStandard.Use: bộ ghi không đặt Standard dùng chuẩn đang áp dụng", B(Fig().ToDxfString().Contains("  8\r\nKC-BETONG\r\n") && DxfWriter.OutputLayerName(Lay.Thep) == "KC-THEP"), 1, 0);
            check("Font thuyết minh theo chuẩn đang áp dụng", B(Report.DocxWriter.DefaultFont == CadStandard.FontTimes), 1, 0);
        }
        check("CadStandard.Use: Dispose trả lại chuẩn cũ", B(CadStandard.Active == null && Fig().ToDxfString().Contains("  8\r\nBE_TONG\r\n") && Report.DocxWriter.DefaultFont == "Times New Roman"), 1, 0);
        var std2 = CadStandard.Default(); std2.ReportFont = CadStandard.FontArial;
        using (CadStandard.Use(std2))
        {
            string docx = Path.Combine(Path.GetTempPath(), "kcvxt_cadstd_font.docx");
            new Report.DocxWriter().Paragraph("Thuyết minh").Save(docx);
            using var z = System.IO.Compression.ZipFile.OpenRead(docx);
            using var rs = new StreamReader(z.GetEntry("word/styles.xml").Open());
            string st = rs.ReadToEnd();
            check("Word: font thuyết minh Arial khi chọn", B(st.Contains("w:ascii=\"Arial\"") && !st.Contains("Times New Roman\" w:hAnsi=\"Times New Roman\" w:eastAsia")), 1, 0);
        }
        check("Lineweight/cờ in giữ trong chuẩn (cho add-in AutoCAD)", B(std.Output(Lay.Thep).Lineweight > 0 && std.Output(Lay.Thep).Plot), 1, 0);
    }

    private static int CountOf(string s, string sub)
    {
        int n = 0, i = 0;
        while ((i = s.IndexOf(sub, i, StringComparison.Ordinal)) >= 0) { n++; i += sub.Length; }
        return n;
    }
}

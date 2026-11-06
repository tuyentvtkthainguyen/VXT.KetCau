using System.Globalization;
using System.Text;
using KetCauVXT.Detailing;
using KetCauVXT.Import;
using KetCauVXT.Import.Arch;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Chuẩn hoá layer CAD → layer chuẩn VXT-… (Core dùng chung với lệnh AutoCAD VXT_CHUANLAYER): nhận theo tên, theo nội dung,
/// block cửa, bộ ánh xạ JSON, các bộ đọc nhận layer chuẩn, xuất DXF đã chuẩn hoá rồi đọc lại.</summary>
public static class LayerConvertTests
{
    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ---- 1. theo tên layer ----
        (string Layer, string Key)[] names =
        {
            ("A-WALL", "TUONG"), ("KT_TUONG 220", "TUONG-220"), ("Tường 110", "TUONG-110"), ("KT-TUONG", "TUONG"), ("tuong xay", "TUONG"),
            ("S-COLS", "COT"), ("Cột", "COT"), ("KC_COT", "COT"), ("COLUMN", "COT"),
            ("TRUC", "TRUC"), ("Trục định vị", "TRUC"), ("A-GRID", "TRUC"), ("A-GRID-IDEN", "TRUC-KH"), ("TRUC_TT", "TRUC"),
            ("A-DOOR", "CUA-DI"), ("Cửa đi", "CUA-DI"), ("A-GLAZ", "CUA-SO"), ("CỬA SỔ", "CUA-SO"), ("WINDOW", "CUA-SO"),
            ("Cầu thang", "THANG"), ("A-FLOR-STRS", "THANG"), ("Tên phòng", "TEN-PHONG"), ("A-AREA-IDEN", "TEN-PHONG"),
            ("DIM", "KICH-THUOC"), ("Kích thước", "KICH-THUOC"), ("A-FURN", "BO-QUA"), ("Nội thất", "BO-QUA"), ("A-WALL-PATT", "BO-QUA"),
            ("DEFPOINTS", "BO-QUA"), ("Lỗ sàn", "LO-SAN"), ("KC-DAM", "DAM"), ("S-BEAM", "DAM"), ("S-SLAB", "SAN"),
            ("VXT-COT", "COT"), ("NHA|A-WALL", "TUONG"), ("NHA$0$S-COLS", "COT"), ("Layer1", ""),
        };
        foreach (var (layer, key) in names)
            check($"Tên layer \"{layer}\" → {(key.Length == 0 ? "(không nhận)" : "VXT-" + key)}", B(LayerConvert.ByName(layer, out _) == key), 1, 0);
        check("\"KT-TUONG\" (KT = kiến trúc) không bị nhận nhầm là kích thước", B(LayerConvert.ByName("KT-TUONG", out _) != "KICH-THUOC"), 1, 0);

        // ---- 2. layer chuẩn & nhóm ----
        check("Bộ layer chuẩn: 15 layer, tên VXT-…, khoá không trùng", B(StdLayers.All.Length == 15 && StdLayers.All.All(s => s.Name.StartsWith("VXT-"))
                                                                         && StdLayers.All.Select(s => s.Key).Distinct().Count() == 15), 1, 0);
        check("VXT-TUONG-220 → nhóm Tường", B(StdLayers.CategoryOf("VXT-TUONG-220") == CadCategory.Tuong), 1, 0);
        check("xref NHA|VXT-CUA-SO → nhóm Cửa", B(StdLayers.CategoryOf("NHA|vxt-cua-so") == CadCategory.Cua), 1, 0);
        check("VXT-KICH-THUOC / VXT-BO-QUA → Bỏ qua; VXT-LO-SAN → Sàn", B(StdLayers.CategoryOf("VXT-KICH-THUOC") == CadCategory.BoQua
                                                                      && StdLayers.CategoryOf("VXT-BO-QUA") == CadCategory.BoQua && StdLayers.CategoryOf("VXT-LO-SAN") == CadCategory.San), 1, 0);
        check("Layer không chuẩn → \"\"", B(StdLayers.CategoryOf("A-WALL") == "" && StdLayers.CategoryOf(null) == ""), 1, 0);
        check("Nhóm chuẩn trùng CadCategory", B(StdLayers.All.All(s => CadCategory.All.Contains(s.Category))), 1, 0);

        // ---- 3. các bộ đọc của chương trình nhận layer chuẩn trước quy tắc ----
        var rules = CadStandard.DefaultInputRules();
        check("CadStandard.Classify: VXT-TUONG-110 → Tường (lý do: layer chuẩn)", B(CadStandard.Classify("VXT-TUONG-110", rules, null, out string why) == CadCategory.Tuong && why.Contains("chuẩn")), 1, 0);
        check("CadStandard.Classify: VXT-KICH-THUOC → Bỏ qua (dù chứa chữ giống từ khoá)", B(CadStandard.Classify("VXT-KICH-THUOC", rules, null, out _) == CadCategory.BoQua), 1, 0);
        check("CadStandard.Classify: gán trực tiếp vẫn thắng layer chuẩn", B(CadStandard.Classify("VXT-COT", rules, new[] { new LayerMapEntry("VXT-COT", CadCategory.BoQua) }, out _) == CadCategory.BoQua), 1, 0);
        var lc = new LayerClassifier(rules, null);
        check("LayerClassifier (ArchPlanReader): VXT-TRUC-KH → Trục, VXT-TEN-PHONG → Tên phòng", B(lc.Of("VXT-TRUC-KH") == CadCategory.Truc && lc.Of("VXT-TEN-PHONG") == CadCategory.TenPhong), 1, 0);
        var keys = new ArchLayerKeys();
        check("Nhập mặt bằng KC: VXT-COT là cột, VXT-DAM là dầm, VXT-CUA-DI là cửa (không là tường)", B(keys.IsColumn("VXT-COT") && keys.IsBeam("VXT-DAM") && keys.IsDoor("VXT-CUA-DI")
                                                                                                && !keys.IsWall("VXT-CUA-DI") && keys.IsWall("VXT-TUONG-220") && keys.IsSlab("VXT-LO-SAN")), 1, 0);

        // ---- 4. theo nội dung ----
        var wall = new LayerStats { Name = "Layer1" };
        for (int i = 0; i < 6; i++) { double y = i * 3; wall.AddLine(0, y, 4, y); wall.AddLine(0, y + 0.11, 4, y + 0.11); }
        check("Nội dung: 6 cặp nét cách 110 → VXT-TUONG-110", B(LayerConvert.ByContent(wall, out double cw, out _) == "TUONG-110" && cw >= 0.45), 1, 0);
        wall.PairStats(out int c110, out int c220, out int pairs, out _);
        check("Cặp nét 110: đếm 12 cặp (mỗi nét có 1 nét đối)", pairs, 12, 0);
        check("Cặp nét 110: không lẫn 220", c220, 0, 0);
        var wall2 = new LayerStats { Name = "L_2" };
        for (int i = 0; i < 5; i++) { double x = i * 4; wall2.AddLine(x, 0, x, 5); wall2.AddLine(x + 0.22, 0, x + 0.22, 5); }
        check("Nội dung: 5 cặp nét dọc cách 220 → VXT-TUONG-220", B(LayerConvert.ByContent(wall2, out _, out _) == "TUONG-220"), 1, 0);

        var col = new LayerStats { Name = "abc" };
        for (int i = 0; i < 6; i++) col.AddPolyline(Rect(i * 4, 0, 0.22, 0.22), true);
        check("Nội dung: 6 chữ nhật kín 220×220 → VXT-COT", B(LayerConvert.ByContent(col, out double cc, out _) == "COT" && cc >= 0.6), 1, 0);
        check("Chữ nhật cột: đếm 6", col.ColumnRects, 6, 0);
        var big = new LayerStats { Name = "khung" };
        for (int i = 0; i < 4; i++) big.AddPolyline(Rect(i * 50, 0, 42, 29.7), true);
        check("Khung giấy lớn (42×29,7 m) không phải cột", B(LayerConvert.ByContent(big, out _, out _) != "COT"), 1, 0);

        var ax = new LayerStats { Name = "L-3", Linetype = "CENTER" };
        for (int i = 0; i < 4; i++) { ax.AddLine(i * 4, -1, i * 4, 19); ax.AddCircle(i * 4, -1.5, 0.4); ax.AddText(((char)('1' + i)).ToString()); }
        check("Nội dung: nét gạch chấm dài + vòng tròn + tên trục → VXT-TRUC", B(LayerConvert.ByContent(ax, out double ca, out _) == "TRUC" && ca >= 0.8), 1, 0);
        var bub = new LayerStats { Name = "L-4" };
        for (int i = 0; i < 4; i++) { bub.AddCircle(i * 4, -1.5, 0.4); bub.AddText(((char)('A' + i)).ToString()); }
        check("Nội dung: chỉ vòng tròn + tên trục → VXT-TRUC-KH", B(LayerConvert.ByContent(bub, out _, out _) == "TRUC-KH"), 1, 0);

        var room = new LayerStats { Name = "T1" };
        foreach (var t in new[] { "Phòng ngủ 1", "P.Khách", "Bếp", "WC", "Ban công" }) room.AddText(t);
        check("Nội dung: chữ Phòng ngủ / P.Khách / Bếp / WC → VXT-TEN-PHONG", B(LayerConvert.ByContent(room, out _, out _) == "TEN-PHONG"), 1, 0);
        check("Tên phòng: nhận \"Phòng ngủ 1\", \"WC\"; không nhận \"3600\", \"D1 220x400\"",
              B(LayerConvert.IsRoomText("Phòng ngủ 1") && LayerConvert.IsRoomText("WC") && !LayerConvert.IsRoomText("3600") && !LayerConvert.IsRoomText("D1 220x400")), 1, 0);
        var dims = new LayerStats { Name = "T2" };
        foreach (var t in new[] { "3600", "4200", "1.200", "±0.000" }) dims.AddText(t);
        check("Nội dung: chữ toàn số → VXT-KICH-THUOC", B(LayerConvert.ByContent(dims, out _, out _) == "KICH-THUOC"), 1, 0);
        var stair = new LayerStats { Name = "T3" };
        for (int i = 0; i < 10; i++) stair.AddLine(0, i * 0.28, 1.2, i * 0.28);
        check("Nội dung: 10 nét bậc 1,2 m cách 280 → VXT-THANG", B(LayerConvert.ByContent(stair, out _, out _) == "THANG"), 1, 0);
        var empty = new LayerStats { Name = "rong" };
        check("Layer trống → giữ nguyên", B(LayerConvert.ByContent(empty, out double c0, out _) == "" && c0 == 0), 1, 0);

        // ---- 5. block cửa, đối tượng ----
        check("Block CUA_D1 / DOOR-900 / D2 → cửa đi", B(LayerConvert.BlockKey("CUA_D1") == "CUA-DI" && LayerConvert.BlockKey("DOOR-900") == "CUA-DI" && LayerConvert.BlockKey("D2") == "CUA-DI"), 1, 0);
        check("Block WINDOW-1200 / Cửa sổ S1 / S1 → cửa sổ", B(LayerConvert.BlockKey("WINDOW-1200") == "CUA-SO" && LayerConvert.BlockKey("Cửa sổ S1") == "CUA-SO" && LayerConvert.BlockKey("S1") == "CUA-SO"), 1, 0);
        check("Block BED / *U12 / rỗng → không phải cửa", B(LayerConvert.BlockKey("BED") == "" && LayerConvert.BlockKey("*U12") == "" && LayerConvert.BlockKey(null) == ""), 1, 0);
        check("Đối tượng: block cửa trên layer bất kỳ → VXT-CUA-DI", B(LayerConvert.TargetFor("", "CUA_D1", false) == "VXT-CUA-DI"), 1, 0);
        check("Đối tượng: tắt chuyển block cửa → theo layer", B(LayerConvert.TargetFor("TUONG-220", "CUA_D1", false, false) == "VXT-TUONG-220"), 1, 0);
        check("Đối tượng: kích thước trên layer tên phòng → VXT-KICH-THUOC (chữ kích thước không thành tên phòng)", B(LayerConvert.TargetFor("TEN-PHONG", null, true) == "VXT-KICH-THUOC"), 1, 0);
        check("Đối tượng: layer giữ nguyên → \"\"", B(LayerConvert.TargetFor("", null, false) == ""), 1, 0);
        var doors = new LayerStats { Name = "Q" };
        for (int i = 0; i < 4; i++) doors.AddInsert("DOOR-" + i);
        check("Nội dung: 4 block cửa → VXT-CUA-DI", B(LayerConvert.ByContent(doors, out _, out _) == "CUA-DI"), 1, 0);

        // ---- 6. đề xuất tổng hợp ----
        var kt = new LayerStats { Name = "KT-TUONG" };
        for (int i = 0; i < 5; i++) { double x = i * 4; kt.AddLine(x, 0, x, 5); kt.AddLine(x + 0.22, 0, x + 0.22, 5); }
        var sg = LayerConvert.Suggest(kt);
        check("Đề xuất: tên \"KT-TUONG\" + nội dung 220 → VXT-TUONG-220", B(sg.Key == "TUONG-220" && sg.Target == "VXT-TUONG-220" && sg.Category == CadCategory.Tuong), 1, 0);
        check("Đề xuất theo tên: tin cậy 0,9", sg.Confidence, 0.9, 1e-9);
        var odd = new LayerStats { Name = "ABC-123" };
        odd.AddLine(0, 0, 1, 0);
        var su = LayerConvert.Suggest(odd, null, l => l == "ABC-123" ? CadCategory.Cot : "");
        check("Đề xuất: quy tắc riêng người dùng (ABC-123 → Cột) → VXT-COT, tin cậy 0,85", B(su.Key == "COT" && Math.Abs(su.Confidence - 0.85) < 1e-9), 1, 0);
        var std0 = LayerConvert.Suggest(new LayerStats { Name = "VXT-DAM" });
        check("Đề xuất: layer đã chuẩn → giữ, tin cậy 1", B(std0.Key == "DAM" && std0.Confidence == 1), 1, 0);

        // ---- 7. bộ ánh xạ JSON ----
        var prof = new LayerProfile { Name = "Văn phòng \"A\"" };
        prof.Set("A-WALL", "TUONG-220");
        prof.Set("Tường ngăn", "VXT-TUONG-110");
        prof.Set("XREF-ONLY", "");
        prof.AddRule("KT-NT*", "BO-QUA");
        prof.Set("a-wall", "TUONG");   // ghi đè (không phân biệt hoa thường)
        prof.Set("KT-TUONG", "TUONG-220");
        string json = prof.ToJson();
        var back = LayerProfile.FromJson(json);
        check("Bộ ánh xạ: tên có dấu + ngoặc kép giữ nguyên sau JSON", B(back.Name == "Văn phòng \"A\""), 1, 0);
        check("Bộ ánh xạ: 4 gán trực tiếp, 1 quy tắc", B(back.Entries.Count == 4 && back.Rules.Count == 1), 1, 0);
        check("Bộ ánh xạ: ghi đè a-wall → TUONG; khoá nhận cả dạng VXT-…", B(back.Lookup("A-WALL") == "TUONG" && back.Lookup("Tường ngăn") == "TUONG-110"), 1, 0);
        check("Bộ ánh xạ: xref NHA|A-WALL dùng lại gán của A-WALL", B(back.Lookup("NHA|A-WALL") == "TUONG"), 1, 0);
        check("Bộ ánh xạ: quy tắc KT-NT* (bỏ dấu) → BO-QUA", B(back.Lookup("KT-NT bàn ghế") == "BO-QUA"), 1, 0);
        check("Bộ ánh xạ: gán \"giữ nguyên\" trả \"\" (khác null = không biết)", B(back.Lookup("XREF-ONLY") == "" && back.Lookup("LẠ") == null), 1, 0);
        var sp = LayerConvert.Suggest(kt, back);
        check("Đề xuất dùng bộ ánh xạ trước (tin cậy 1)", B(sp.Key == "TUONG-220" && sp.Confidence == 1 && sp.Reason.Contains("bộ ánh xạ")), 1, 0);
        check("JSON hỏng → bộ rỗng", B(LayerProfile.FromJson("{ không phải json").Entries.Count == 0 && LayerProfile.FromJson(null).Entries.Count == 0), 1, 0);
        string pdir = Path.Combine(outDir, "ChuanHoaLayer");
        Directory.CreateDirectory(pdir);
        string pfile = Path.Combine(pdir, "BoAnhXa_VanPhongA.json");
        LayerConvertIo.SaveProfile(back, pfile);
        var re = LayerConvertIo.LoadProfile(pfile);
        check("Bộ ánh xạ: lưu file rồi mở lại giống hệt", B(re != null && re.ToJson() == back.ToJson()), 1, 0);

        // ---- 8. DXF: đọc → thống kê → đề xuất → xuất DXF chuẩn hoá → đọc lại ----
        string src = Path.Combine(pdir, "MatBang_LayerLa_DauVao.dxf");
        File.WriteAllText(src, SampleDxf(), new UTF8Encoding(false));
        var dxf = DxfReader.Read(src);
        var stats = LayerConvertIo.StatsFrom(dxf);
        var sugg = LayerConvertIo.Suggest(stats);
        string K(string layer) => sugg.FirstOrDefault(s => s.Layer == layer)?.Key ?? "?";
        check("DXF: layer A-WALL (cặp nét 220) → VXT-TUONG-220", B(K("A-WALL") == "TUONG-220"), 1, 0);
        check("DXF: layer LAYER_X (9 chữ nhật 220×220) → VXT-COT theo nội dung", B(K("LAYER_X") == "COT"), 1, 0);
        check("DXF: layer TRUC_DINH_VI → VXT-TRUC", B(K("TRUC_DINH_VI") == "TRUC"), 1, 0);
        check("DXF: layer XYZ (chữ Phòng ngủ, Bếp, WC) → VXT-TEN-PHONG", B(K("XYZ") == "TEN-PHONG"), 1, 0);
        check("DXF: nét trục đọc kiểu đường CENTER của layer", B(stats.TryGetValue("TRUC_DINH_VI", out var ts) && ts.DashLines >= 6), 1, 0);
        var map = LayerConvertIo.ToLayerMap(sugg);
        check("Gán layer → nhóm cho bộ đọc: LAYER_X → Cột", B(map.Any(m => m.Layer == "LAYER_X" && m.Category == CadCategory.Cot)), 1, 0);

        // đọc bản vẽ GỐC kèm gán (không sửa file) → cột nhận được dù layer tên lạ
        var o1 = new ArchImportOptions();
        o1.LayerMap.AddRange(map);
        var d1 = ArchPlanReader.Read(src, o1);
        check("Đọc bản vẽ gốc theo gán: LAYER_X là Cột", B(d1.LayerCategories.TryGetValue("LAYER_X", out string cx) && cx == CadCategory.Cot), 1, 0);
        check("Đọc bản vẽ gốc theo gán: nhận ≥ 9 cột", d1.Floors.Sum(f => f.Columns.Count) >= 9 ? 1 : 0, 1, 0);

        string outStd = Path.Combine(pdir, "MatBang_ChuanHoa_DauVao.dxf");
        var kmap = sugg.ToDictionary(s => s.Layer, s => s.Key, StringComparer.OrdinalIgnoreCase);
        var counts = LayerConvertIo.ExportStandardDxf(dxf, l => kmap.TryGetValue(l, out var k) ? k : "", outStd);
        check("Xuất DXF chuẩn hoá: 9 đối tượng trên VXT-COT", counts.TryGetValue("VXT-COT", out int nc) ? nc : 0, 9, 0);
        check("Xuất DXF chuẩn hoá: 12 nét trên VXT-TUONG-220", counts.TryGetValue("VXT-TUONG-220", out int nw) ? nw : 0, 12, 0);
        var dxf2 = DxfReader.Read(outStd);
        check("Đọc lại: không còn layer gốc LAYER_X / A-WALL", B(!dxf2.Entities.Any(e => e.Layer == "LAYER_X" || e.Layer == "A-WALL")), 1, 0);
        check("Đọc lại: chữ tiếng Việt giữ nguyên (\\U+…)", B(dxf2.Entities.Any(e => e.Layer == "VXT-TEN-PHONG" && e.Text == "Phòng ngủ")), 1, 0);
        check("Đọc lại: layer VXT-TRUC có kiểu đường CENTER", B(dxf2.LayerLineTypes.TryGetValue("VXT-TRUC", out string lt) && lt.Equals("CENTER", StringComparison.OrdinalIgnoreCase)), 1, 0);
        check("Đọc lại: toạ độ giữ đúng (mm → m)", B(dxf2.Entities.Where(e => e.Layer == "VXT-COT").SelectMany(e => e.Points).Max(p => p.X) is double mx && Math.Abs(mx - 8.11) < 1e-6), 1, 0);
        var d2 = ArchPlanReader.Read(outStd, new ArchImportOptions());
        check("ArchPlanReader trên bản chuẩn hoá: VXT-COT → Cột, VXT-TUONG-220 → Tường (không cần gán)",
              B(d2.LayerCategories.TryGetValue("VXT-COT", out string c1) && c1 == CadCategory.Cot && d2.LayerCategories.TryGetValue("VXT-TUONG-220", out string c2) && c2 == CadCategory.Tuong), 1, 0);
        check("ArchPlanReader trên bản chuẩn hoá: nhận ≥ 9 cột", d2.Floors.Sum(f => f.Columns.Count) >= 9 ? 1 : 0, 1, 0);
        var sugg2 = LayerConvertIo.Suggest(LayerConvertIo.StatsFrom(dxf2));
        check("Đề xuất lại trên bản chuẩn hoá: mọi layer VXT-… giữ nguyên khoá, tin cậy 1",
              B(sugg2.Where(s => s.Layer.StartsWith("VXT-")).All(s => "VXT-" + s.Key == s.Layer && s.Confidence == 1)), 1, 0);
    }

    private static List<double[]> Rect(double cx, double cy, double w, double h) => new()
    {
        new[] { cx - w / 2, cy - h / 2 }, new[] { cx + w / 2, cy - h / 2 }, new[] { cx + w / 2, cy + h / 2 }, new[] { cx - w / 2, cy + h / 2 }
    };

    /// <summary>Mặt bằng 2×2 ô (trục 0/4000/8000 × 0/3000/6000 mm), layer tên lạ: tường A-WALL (cặp nét 220), cột LAYER_X, trục TRUC_DINH_VI (CENTER), chữ XYZ.</summary>
    private static string SampleDxf()
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        void G(int c, object v) => sb.Append(c.ToString(inv)).Append('\n').Append(Convert.ToString(v, inv)).Append('\n');
        G(0, "SECTION"); G(2, "HEADER"); G(9, "$ACADVER"); G(1, "AC1009"); G(0, "ENDSEC");
        G(0, "SECTION"); G(2, "TABLES");
        G(0, "TABLE"); G(2, "LTYPE"); G(70, 2);
        G(0, "LTYPE"); G(2, "CONTINUOUS"); G(70, 0); G(3, "Solid line"); G(72, 65); G(73, 0); G(40, 0.0);
        G(0, "LTYPE"); G(2, "CENTER"); G(70, 0); G(3, "____ _ ____ _"); G(72, 65); G(73, 4); G(40, 50.8); G(49, 31.75); G(49, -6.35); G(49, 6.35); G(49, -6.35);
        G(0, "ENDTAB");
        G(0, "TABLE"); G(2, "LAYER"); G(70, 5);
        foreach (var (ln, lcol, lty) in new[] { ("0", 7, "CONTINUOUS"), ("A-WALL", 2, "CONTINUOUS"), ("LAYER_X", 6, "CONTINUOUS"), ("TRUC_DINH_VI", 1, "CENTER"), ("XYZ", 7, "CONTINUOUS") })
        { G(0, "LAYER"); G(2, ln); G(70, 0); G(62, lcol); G(6, lty); }
        G(0, "ENDTAB");
        G(0, "ENDSEC");
        G(0, "SECTION"); G(2, "ENTITIES");
        void Line(string l, double x1, double y1, double x2, double y2) { G(0, "LINE"); G(8, l); G(10, x1); G(20, y1); G(30, 0); G(11, x2); G(21, y2); G(31, 0); }
        double[] xs = { 0, 4000, 8000 }, ys = { 0, 3000, 6000 };
        // trục + vòng ký hiệu + tên trục
        for (int i = 0; i < 3; i++)
        {
            Line("TRUC_DINH_VI", xs[i], -1500, xs[i], 7500);
            Line("TRUC_DINH_VI", -1500, ys[i], 9500, ys[i]);
            G(0, "CIRCLE"); G(8, "TRUC_DINH_VI"); G(10, xs[i]); G(20, -1900); G(30, 0); G(40, 400);
            G(0, "TEXT"); G(8, "TRUC_DINH_VI"); G(10, xs[i] - 100); G(20, -2000); G(30, 0); G(40, 250); G(1, (i + 1).ToString(inv));
        }
        // tường 220: cặp nét ±110 quanh trục
        for (int i = 0; i < 3; i++)
        {
            Line("A-WALL", xs[i] - 110, 0, xs[i] - 110, 6000); Line("A-WALL", xs[i] + 110, 0, xs[i] + 110, 6000);
            Line("A-WALL", 0, ys[i] - 110, 8000, ys[i] - 110); Line("A-WALL", 0, ys[i] + 110, 8000, ys[i] + 110);
        }
        // cột 220×220 tại 9 giao trục
        foreach (var x in xs)
            foreach (var y in ys)
            {
                G(0, "POLYLINE"); G(8, "LAYER_X"); G(66, 1); G(10, 0); G(20, 0); G(30, 0); G(70, 1);
                foreach (var (px, py) in new[] { (x - 110, y - 110), (x + 110, y - 110), (x + 110, y + 110), (x - 110, y + 110) })
                { G(0, "VERTEX"); G(8, "LAYER_X"); G(10, px); G(20, py); G(30, 0); }
                G(0, "SEQEND"); G(8, "LAYER_X");
            }
        // chữ tên phòng (tiếng Việt mã hoá \U+)
        foreach (var (t, x, y) in new[] { ("Phòng ngủ", 1500.0, 1500.0), ("Bếp", 5500.0, 4500.0), ("WC", 1500.0, 4500.0) })
        {
            var e = new StringBuilder();
            foreach (char ch in t) { if (ch > 127) e.Append("\\U+").Append(((int)ch).ToString("X4", inv)); else e.Append(ch); }
            G(0, "TEXT"); G(8, "XYZ"); G(10, x); G(20, y); G(30, 0); G(40, 250); G(1, e.ToString());
        }
        G(0, "ENDSEC"); G(0, "EOF");
        return sb.ToString();
    }
}

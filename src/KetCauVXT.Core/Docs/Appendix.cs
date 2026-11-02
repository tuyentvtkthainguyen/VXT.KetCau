using System.Globalization;
using System.Text;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Steel;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Docs;

/// <summary>Một dòng của phụ lục tính toán: một đại lượng / công thức / quy tắc mà chương trình thực sự áp dụng.</summary>
public class AppendixItem
{
    /// <summary>Đại lượng / hạng mục</summary>
    public string Name { get; set; } = "";
    /// <summary>Ký hiệu</summary>
    public string Symbol { get; set; } = "";
    /// <summary>Công thức (văn bản Unicode)</summary>
    public string Formula { get; set; } = "";
    /// <summary>Hệ số / giá trị mặc định / phạm vi</summary>
    public string Values { get; set; } = "";
    /// <summary>Hệ số an toàn / hệ số độ tin cậy (nếu có)</summary>
    public string SafetyFactor { get; set; } = "";
    /// <summary>Giải thích dễ hiểu: ý nghĩa, khi nào áp dụng, lưu ý</summary>
    public string Explanation { get; set; } = "";
    /// <summary>Tiêu chuẩn / điều khoản</summary>
    public string Standard { get; set; } = "";

    public AppendixItem() { }
    public AppendixItem(string name, string symbol, string formula, string values, string safety, string explanation, string standard)
    {
        Name = name ?? ""; Symbol = symbol ?? ""; Formula = formula ?? ""; Values = values ?? "";
        SafetyFactor = safety ?? ""; Explanation = explanation ?? ""; Standard = standard ?? "";
    }

    /// <summary>Toàn bộ văn bản của dòng (dùng để tìm kiếm).</summary>
    public string AllText => string.Join(" ", Name, Symbol, Formula, Values, SafetyFactor, Explanation, Standard);
    public override string ToString() => Name;
}

/// <summary>Bảng tra kèm theo một mục phụ lục (vd. cường độ bê tông theo cấp, k(z) theo độ cao).</summary>
public class AppendixTable
{
    public string Title { get; set; } = "";
    public string[] Headers { get; set; } = Array.Empty<string>();
    public List<string[]> Rows { get; set; } = new();
    /// <summary>Ghi chú dưới bảng</summary>
    public string Note { get; set; } = "";
    public string AllText => Title + " " + string.Join(" ", Headers) + " " + string.Join(" ", Rows.Select(r => string.Join(" ", r))) + " " + Note;
}

/// <summary>Một mục phụ lục (PL1…PLn).</summary>
public class AppendixSection
{
    /// <summary>Mã mục: "PL1".."PLn"</summary>
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>Giải thích mở đầu cho người mới</summary>
    public string Intro { get; set; } = "";
    public List<AppendixItem> Items { get; set; } = new();
    public List<AppendixTable> Tables { get; set; } = new();
    public string FullTitle => Code + ". " + Title;
    public override string ToString() => FullTitle;
}

/// <summary>Phụ lục tính toán: công thức, chỉ số, hệ số, hệ số an toàn, giải thích và phương pháp mà Kết cấu VXT áp dụng.
/// Nội dung lấy trực tiếp từ mã nguồn: các bảng số (cường độ vật liệu, k(z), phổ động đất, hệ số A/B/D, φ thép, tải tường,
/// cấu tạo sàn…) được tính lại bằng chính các hàm của chương trình nên luôn khớp với phiên bản đang chạy.</summary>
public static class Appendix
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Số kiểu Việt Nam (dấu phẩy thập phân).</summary>
    public static string F(double v, string fmt = "0.##") =>
        double.IsFinite(v) ? v.ToString(fmt, Inv).Replace('.', ',') : "–";

    private static AppendixItem I(string name, string symbol, string formula, string values, string safety, string explanation, string standard) =>
        new(name, symbol, formula, values, safety, explanation, standard);

    /// <summary>Chuẩn hoá chuỗi để tìm kiếm không dấu: bỏ dấu tiếng Việt, đ → d, chữ thường.</summary>
    public static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (char ch in d)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark || cat == UnicodeCategory.SpacingCombiningMark || cat == UnicodeCategory.EnclosingMark) continue;
            sb.Append(ch switch { 'đ' => 'd', 'Đ' => 'd', _ => char.ToLowerInvariant(ch) });
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Tìm kiếm không dấu trên toàn bộ phụ lục: mọi từ trong query phải xuất hiện trong dòng (hoặc trong tiêu đề mục).</summary>
    public static List<(AppendixSection Section, AppendixItem Item)> Search(IEnumerable<AppendixSection> sections, string query)
    {
        var res = new List<(AppendixSection, AppendixItem)>();
        var words = Normalize(query).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return res;
        foreach (var s in sections)
        {
            string head = Normalize(s.FullTitle);
            foreach (var it in s.Items)
            {
                string t = head + " " + Normalize(it.AllText);
                if (words.All(w => t.Contains(w))) res.Add((s, it));
            }
            // bảng tra: trả về dạng dòng giả để hiển thị
            foreach (var tb in s.Tables)
            {
                string t = head + " " + Normalize(tb.AllText);
                if (words.All(w => t.Contains(w)))
                    res.Add((s, new AppendixItem("Bảng tra: " + tb.Title, "", "", "", "", TableAsText(tb), "")));
            }
        }
        return res;
    }

    /// <summary>Bảng tra dạng văn bản nhiều dòng (dùng cho tìm kiếm, Excel).</summary>
    public static string TableAsText(AppendixTable t)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(" | ", t.Headers));
        foreach (var r in t.Rows) sb.Append('\n').Append(string.Join(" | ", r));
        if (!string.IsNullOrEmpty(t.Note)) sb.Append('\n').Append(t.Note);
        return sb.ToString();
    }

    // ==================================================================
    // NỘI DUNG
    // ==================================================================

    /// <summary>Toàn bộ các mục phụ lục. p ≠ null: bổ sung giá trị thực của công trình (W0, địa hình, cấp bê tông, R nền…).</summary>
    public static List<AppendixSection> Sections(Project p = null)
    {
        var list = new List<AppendixSection>
        {
            Pl01Units(p), Pl02Materials(p), Pl03Loads(p), Pl04Wind(p), Pl05Seismic(p), Pl06Combos(p),
            Pl07Analysis(p), Pl08Beam(p), Pl09Column(p), Pl10Slab(p), Pl11Footing(p), Pl12Pile(p),
            Pl13Strip(p), Pl14Steel(p), Pl15Geotech(p), Pl16Preliminary(p), Pl17Safety(p), Pl18Workflow(p)
        };
        for (int i = 0; i < list.Count; i++) list[i].Code = "PL" + (i + 1);
        return list;
    }

    private const string ProjTag = "Giá trị đang dùng trong công trình";

    // ------------------------------------------------------------------
    private static AppendixSection Pl01Units(Project p)
    {
        var s = new AppendixSection
        {
            Title = "Đơn vị, ký hiệu và quy ước dấu",
            Intro = "Mọi phép tính bên trong chương trình dùng một hệ đơn vị thống nhất kN – m. Giao diện, bản vẽ và thuyết minh chỉ đổi sang mm, MPa, kN·m "
                  + "để dễ đọc. Khi nhập số, hãy để ý đơn vị ghi ở tiêu đề ô nhập. Quy ước dấu nội lực giống ETABS/SAP2000 nên có thể so sánh trực tiếp."
        };
        s.Items.Add(I("Hệ đơn vị tính toán", "kN, m, °C", "", "Lực kN; chiều dài m; ứng suất kN/m²; mô men kN·m; trọng lượng riêng kN/m³",
            "", "Kích thước tiết diện nhập bằng mm nhưng lưu bằng m. Cường độ vật liệu hiển thị MPa nhưng lưu kN/m².", ""));
        s.Items.Add(I("Đổi đơn vị ứng suất", "MPa", "1 MPa = 1 N/mm² = 1000 kN/m²", "1 kN/m² = 100 daN/m² ≈ 0,1 T/m²; 1 kG/cm² ≈ 98,1 kN/m² ≈ 0,1 MPa",
            "", "Bảng tra cũ hay dùng daN/m² hoặc kG/cm²: W0 = 95 daN/m² = 0,95 kN/m²; R = 1,5 kG/cm² ≈ 147 kN/m².", ""));
        s.Items.Add(I("Đổi đơn vị diện tích thép", "As", "1 cm² = 10⁻⁴ m² = 100 mm²", "Thanh Ø: as = π·d²/4",
            "", "Thuyết minh ghi As bằng cm². Ví dụ 1Ø16 = 2,01 cm²; 1Ø20 = 3,14 cm².", ""));
        s.Items.Add(I("Gia tốc trọng trường", "g", "g = 9,81 m/s²", "agR nhập theo đơn vị g (vd. 0,0783g)",
            "", "Khối lượng tham gia dao động được dùng trực tiếp dưới dạng trọng lượng W (kN); phổ Sd tính theo đơn vị g nên Fb = Sd·W.", "TCVN 9386:2012"));
        s.Items.Add(I("Hệ trục tổng thể", "X, Y, Z", "", "Z hướng lên; trục X ngang mặt bằng (trục 1, 2, 3…), trục Y (A, B, C…)",
            "", "Tải trọng lực (Gravity) mang dấu dương khi hướng xuống (−Z). Tải tập trung nút nhập theo hệ tổng thể.", ""));
        s.Items.Add(I("Hệ trục địa phương thanh", "1, 2, 3", "Trục 1: từ nút I → J. Thanh đứng: trục 2 // +X, 3 = 1×2. Thanh ngang: trục 3 = 1×Z (nằm ngang), 2 = 3×1 (hướng lên)",
            "Góc xoay quanh trục 1 do người dùng nhập", "",
            "Giống mặc định ETABS/SAP2000: dầm uốn chính quanh trục 3 (M3), cột có cạnh H theo trục 2.", ""));
        s.Items.Add(I("Quy ước dấu nội lực", "P, V2, V3, T, M2, M3", "P > 0: kéo; M3 > 0: căng thớ phía −2 (căng dưới đối với dầm)",
            "Đơn vị kN, kN·m; vị trí mặt cắt (station) tính từ đầu I", "",
            "Biểu đồ mô men dầm: dương ở giữa nhịp (căng dưới) và âm tại gối (căng trên). Lực dọc cột mang dấu âm khi nén.", ""));
        s.Items.Add(I("Nội lực chân cột truyền xuống móng", "N, Mx, My, Qx, Qy", "N = Fz phản lực gối (N > 0: nén); Mx, My, Qx, Qy lấy theo phản lực",
            "", "", "Móng đơn, đài cọc và móng băng đều đọc nội lực chân cột theo quy ước này. N ≤ 0 nghĩa là chân cột bị nhổ.", ""));
        s.Items.Add(I("Ký hiệu trường hợp tải mặc định", "TT, HTHT, HT, GX, GY, ĐĐX, ĐĐY",
            "", "TT: tĩnh tải (kể trọng lượng bản thân); HTHT: tĩnh tải hoàn thiện + tường; HT: hoạt tải; GX/GY: gió theo X/Y; ĐĐX/ĐĐY: động đất", "",
            "Tên trường hợp gió/động đất chứa chữ Y được hiểu là tác dụng theo phương Y, còn lại theo phương X.", ""));
        s.Items.Add(I("TTGH I / TTGH II", "ULS / SLS", "", "TTGH I: kiểm tra độ bền (tải tính toán = tiêu chuẩn × γf); TTGH II: võng, lún, áp lực nền (tải tiêu chuẩn)", "",
            "Tổ hợp TH… là TTGH I, TC… là TTGH II, BAO là bao các tổ hợp TTGH I.", "TCVN 2737:2023"));
        return s;
    }

    // ------------------------------------------------------------------
    private static Material SafeConcrete(Project p, string cls)
    {
        var m = p?.Materials.FirstOrDefault(x => x.Name == cls && x.Kind == MaterialKind.Concrete);
        if (m != null) return m;
        try { return Material.Concrete(cls); } catch (ArgumentException) { return null; }
    }

    private static Material SafeRebar(Project p, string g)
    {
        var m = p?.Materials.FirstOrDefault(x => x.Name == g && x.Kind == MaterialKind.Rebar);
        if (m != null) return m;
        try { return Material.Rebar(g); } catch (ArgumentException) { return null; }
    }

    private static AppendixSection Pl02Materials(Project p)
    {
        var s = new AppendixSection
        {
            Title = "Vật liệu: bê tông, cốt thép, thép hình",
            Intro = "Cường độ trong bảng là cường độ TÍNH TOÁN (đã chia hệ số độ tin cậy vật liệu γb, γs), dùng trực tiếp trong công thức thiết kế. "
                  + "Cấp bê tông B (B20, B25…) là cường độ chịu nén đặc trưng của mẫu lập phương tính bằng MPa – khác với mác M cũ (B20 ≈ M250, B25 ≈ M350)."
        };
        var cd = new ConcreteDesignSettings();
        s.Items.Add(I("Cường độ chịu nén tính toán của bê tông", "Rb", "Rb = Rb,n / γb", "Theo cấp B – xem bảng", "γb ≈ 1,3 (đã có trong Rb)",
            "Dùng cho vùng nén của dầm, cột, sàn, móng. Rb,n chỉ dùng khi xuất sang ETABS (fc').", "TCVN 5574:2018 Bảng 6, 7"));
        s.Items.Add(I("Cường độ chịu kéo tính toán của bê tông", "Rbt", "Rbt = Rbt,n / γbt", "Theo cấp B – xem bảng", "γbt ≈ 1,5 (đã có trong Rbt)",
            "Dùng khi tính chịu cắt (Qb), chọc thủng móng/đài và mô men nứt Mcr.", "TCVN 5574:2018 Bảng 7"));
        s.Items.Add(I("Mô đun đàn hồi bê tông", "Eb", "", "Theo cấp B – xem bảng; ν = 0,2; γ = 25 kN/m³; α = 1·10⁻⁵/°C", "",
            "Dùng trong phân tích khung (độ cứng tiết diện nguyên, không giảm do nứt) và tính võng.", "TCVN 5574:2018 Bảng 10"));
        s.Items.Add(I("Cường độ cốt thép", "Rs, Rsc, Rsw", "Rs = Rs,n / γs", "Theo mác thép – xem bảng; Es = 2·10⁵ MPa; γ = 78,5 kN/m³", "γs ≈ 1,15 (CB240-T ≈ 1,14), đã có trong Rs",
            "Rs: thép chịu kéo; Rsc: thép chịu nén; Rsw: cốt đai chịu cắt. Mặc định thép dọc " + cd.RebarMain + ", thép đai/thép sàn Ø < 10: " + cd.RebarStirrup + ".",
            "TCVN 5574:2018 Bảng 13; TCVN 1651:2018"));
        s.Items.Add(I("Chiều cao vùng nén giới hạn", "ξR, αR", "ξR = 0,8 / (1 + εs,el/εb2), εs,el = Rs/Es, εb2 = 0,0035; αR = ξR·(1 − ξR/2)", "Theo mác thép – xem bảng", "",
            "Nếu αm > αR tiết diện bị phá hoại giòn từ vùng nén → chương trình chuyển sang tính cốt kép và khuyên tăng tiết diện.", "TCVN 5574:2018 mục 8.1.2.2.3"));
        s.Items.Add(I("Cường độ tính toán thép hình", "f", "f = fy / γM", "γM = 1,05; E = 2,1·10⁵ MPa; G = E/(2(1+ν)), ν = 0,3", "γM = 1,05; γc = " + F(new SteelDesignSettings().GammaC),
            "Áp dụng cho cột, kèo, xà gồ, giằng khung thép tiền chế. Người dùng có thể sửa f trực tiếp trong thư viện vật liệu.", "TCVN 5575:2012/2024"));
        s.Items.Add(I("Lớp bê tông bảo vệ (tới mép cốt dọc)", "c", "", "Dầm " + F(cd.CoverBeam * 1000) + " mm; cột " + F(cd.CoverColumn * 1000) + " mm; sàn " + F(cd.CoverSlab * 1000)
            + " mm; móng " + F(cd.CoverFooting * 1000) + " mm", "", "Chiều cao làm việc h0 = h − c − Ø/2 (dầm lấy a = c + 10 mm).", "TCVN 5574:2018 mục 10.3.1"));

        var tc = new AppendixTable { Title = "Bê tông nặng – cường độ tính toán (MPa)", Headers = new[] { "Cấp", "Rb", "Rbt", "Eb", "Rb,n" } };
        foreach (var c in Material.ConcreteClasses)
        {
            var m = Material.Concrete(c);
            tc.Rows.Add(new[] { c, F(U.ToMPa(m.Rb)), F(U.ToMPa(m.Rbt)), F(U.ToMPa(m.E), "0"), F(U.ToMPa(m.Rbn)) });
        }
        s.Tables.Add(tc);
        var tr = new AppendixTable { Title = "Cốt thép – cường độ tính toán (MPa) và ξR, αR", Headers = new[] { "Mác", "Rs", "Rsc", "Rsw", "fy", "ξR", "αR" } };
        foreach (var g in Material.RebarGrades)
        {
            var m = Material.Rebar(g);
            double xi = RcSection.XiR(m.Rs, m.E);
            tr.Rows.Add(new[] { g, F(U.ToMPa(m.Rs)), F(U.ToMPa(m.Rsc)), F(U.ToMPa(m.Rsw)), F(U.ToMPa(m.Fy)), F(xi, "0.000"), F(RcSection.AlphaR(xi), "0.000") });
        }
        s.Tables.Add(tr);
        var ts = new AppendixTable { Title = "Thép hình – giới hạn chảy, giới hạn bền, cường độ tính toán (MPa)", Headers = new[] { "Mác", "fy", "fu", "f = fy/1,05" } };
        foreach (var g in Material.SteelGrades)
        {
            var m = Material.Steel(g);
            ts.Rows.Add(new[] { g, F(U.ToMPa(m.Fy)), F(U.ToMPa(m.Fu)), F(U.ToMPa(m.F)) });
        }
        ts.Note = "G350: thép mạ kẽm cán nguội làm xà gồ.";
        s.Tables.Add(ts);

        if (p != null)
        {
            var c = p.Concrete;
            string Line(string what, string cls)
            {
                var m = SafeConcrete(p, cls);
                return m == null ? what + " " + cls : $"{what} {cls}: Rb = {F(U.ToMPa(m.Rb))}, Rbt = {F(U.ToMPa(m.Rbt))} MPa";
            }
            var rm = SafeRebar(p, c.RebarMain);
            var rs = SafeRebar(p, c.RebarStirrup);
            s.Items.Add(I(ProjTag, "", "",
                string.Join("; ", Line("cột", c.ConcreteColumn), Line("dầm", c.ConcreteBeam), Line("sàn", c.ConcreteSlab), Line("móng", c.ConcreteFooting))
                + (rm != null ? $"; thép dọc {rm.Name}: Rs = {F(U.ToMPa(rm.Rs))} MPa" : "")
                + (rs != null ? $"; thép đai {rs.Name}: Rsw = {F(U.ToMPa(rs.Rsw))} MPa" : ""),
                "", "Lấy từ Thiết kế BTCT của công trình đang mở.", ""));
        }
        return s;
    }

    // ------------------------------------------------------------------
    private static AppendixSection Pl03Loads(Project p)
    {
        var s = new AppendixSection
        {
            Title = "Tải trọng đứng và hệ số độ tin cậy γf",
            Intro = "Tải trọng nhập là giá trị TIÊU CHUẨN. Khi tạo tổ hợp TTGH I chương trình nhân với hệ số độ tin cậy γf của từng trường hợp tải "
                  + "(cột \"Hệ số vượt tải γf\" trong bảng trường hợp tải – người dùng sửa được). Tải sàn được dồn về dầm theo đường phân giác 45°, "
                  + "tải tường đặt lên dầm nằm dưới tường."
        };
        var def = ModelBuilder.NewProject("_").Patterns;
        string G(string n) => F(def.FirstOrDefault(x => x.Name == n)?.Gamma ?? 1.0);
        s.Items.Add(I("Tĩnh tải – trọng lượng bản thân", "TT", "g = γ·A (thanh, kN/m); g = γ·t (sàn, kN/m²)", "γ bê tông 25 kN/m³; thép 78,5 kN/m³", "γf = " + G("TT"),
            "Tự tính từ tiết diện, không cần nhập. Trọng lượng bản thân sàn được dồn về dầm cùng với tải sàn.", "TCVN 2737:2023"));
        s.Items.Add(I("Tĩnh tải hoàn thiện + tường", "HTHT", "gs = Σ ti·γi + tải cố định (cấu tạo sàn)", "Xem bảng cấu tạo sàn mặc định", "γf = " + G("HTHT"),
            "Gồm lớp lát, vữa, trát trần, chống thấm, trần treo, M&E và toàn bộ tải tường. Nếu ô sàn gán \"Cấu tạo sàn\" thì HTHT và HT lấy theo cấu tạo đó.", "TCVN 2737:2023"));
        s.Items.Add(I("Hoạt tải sử dụng", "HT", "p theo công năng (kN/m²)", "Phòng ở 1,5; WC 1,5; ban công 2,0; sảnh/hành lang 3,0; kho 4,0; mái BTCT 0,75", "γf = " + G("HT") + " (mặc định)",
            "Giá trị tiêu chuẩn toàn phần theo công năng. Lưu ý: TCVN 2737 quy định γf lớn hơn (1,3) cho hoạt tải phân bố có giá trị nhỏ (< 2,0 kN/m²) – "
            + "kiểm tra và sửa γf của trường hợp HT nếu cần (chương trình không tự đổi).", "TCVN 2737:2023 (bảng hoạt tải theo công năng)"));
        s.Items.Add(I("Hoạt tải mái tôn khung thép", "HT (mái)", "", "0,30 kN/m² (mái tôn không sử dụng); tĩnh tải mái 0,15 kN/m²", "γf = 1,3; TT γf = 1,1; gió γf = 1,2",
            "Giá trị mặc định khi tạo khung tiền chế (hộp thoại Khung tiền chế).", "TCVN 2737:2023"));
        s.Items.Add(I("Tải tường trên 1 m² tường", "gw", "gw = t·γ + n·tp·γtrát", "t = 0,11/0,22/0,33 m; γ gạch đặc 18, gạch rỗng 15, AAC 7–9 kN/m³; tp = 15 mm, γtrát = 18 kN/m³; n = 0/1/2 mặt trát", "",
            "Tải tiêu chuẩn của tường đặc (chưa trừ cửa). Xem bảng loại tường mặc định.", ""));
        s.Items.Add(I("Tải tường phân bố trên dầm", "qw", "qw = gw·H·(1 − tỷ lệ cửa)", "H mặc định = chiều cao tầng trên − max(hdầm dưới tường; 0,35 m); tường chắn mái 0,9 m; tỷ lệ cửa ≤ 0,9", "γf của HTHT",
            "Đặt vào phần dầm thẳng hàng nằm dưới tường (trong phạm vi t/2 + 50 mm). Phần tường không có dầm đỡ → tải phân bố đều lên ô sàn chứa trung điểm đoạn tường: q = qw·Lhở/Asàn.", ""));
        s.Items.Add(I("Dồn tải sàn về dầm (ô chữ nhật)", "q(t)", "q(t) = qsàn·min(t; le − t; lngắn/2)", "Cạnh dài: hình thang; cạnh ngắn: tam giác (đường phân giác 45°)", "",
            "t là khoảng cách dọc cạnh tính từ góc ô. Giống cách ETABS dồn tải sàn kiểu membrane. Cạnh không có dầm: tải phần đó dồn về 2 nút góc.", ""));
        s.Items.Add(I("Dồn tải sàn về dầm (ô đa giác khác)", "w", "w = A/chu vi, q = qsàn·w", "", "", "Ô không phải chữ nhật được phân đều theo chu vi (gần đúng – nên chia thành các ô chữ nhật).", ""));
        s.Items.Add(I("Tải phân bố khi chưa gán cấu tạo sàn", "gs, p", "", string.Join("; ", Enum.GetValues<SlabUse>().Select(u => { var l = ModelEdit.Loads(u); return $"{l.Name}: {F(l.Finish)} / {F(l.Live)}"; })) + " (HTHT / HT, kN/m²)", "",
            "Giá trị gán theo công năng ô sàn khi vẽ sàn trên mặt bằng hoặc triển khai từ bản vẽ kiến trúc.", ""));

        var tb = new AppendixTable { Title = "Cấu tạo sàn mặc định (kN/m², tiêu chuẩn)", Headers = new[] { "Cấu tạo", "Các lớp", "Hoàn thiện gs", "Hoạt tải p" } };
        foreach (var b in FloorBuildUp.Defaults())
            tb.Rows.Add(new[] { b.Name, string.Join("; ", b.Layers.Select(l => l.Thickness > 0 ? $"{l.Name} {F(l.Thickness * 1000, "0")} mm × {F(l.UnitWeight)}" : $"{l.Name} {F(l.FixedLoad)}")), F(b.Finish, "0.00"), F(b.Live) });
        tb.Note = "Không gồm bản BTCT (trọng lượng bản thân sàn tính theo chiều dày tiết diện sàn trong TT).";
        s.Tables.Add(tb);
        var tw = new AppendixTable { Title = "Loại tường mặc định – tải 1 m² tường đặc (kN/m², tiêu chuẩn)", Headers = new[] { "Loại tường", "t (mm)", "γ (kN/m³)", "Mặt trát", "gw", "Tỷ lệ cửa" } };
        foreach (var w in WallType.Defaults())
            tw.Rows.Add(new[] { w.Name, F(w.Thickness * 1000, "0"), F(w.UnitWeight), w.PlasterSides.ToString(Inv), F(w.WeightPerM2, "0.00"), F(w.OpeningRatio * 100, "0") + "%" });
        s.Tables.Add(tw);
        var tg = new AppendixTable { Title = "Hệ số độ tin cậy γf mặc định của các trường hợp tải", Headers = new[] { "Trường hợp", "Loại", "γf" } };
        foreach (var pt in def) tg.Rows.Add(new[] { pt.Name, pt.Type.ToString(), F(pt.Gamma) });
        tg.Rows.Add(new[] { "ĐĐX, ĐĐY", "Seismic", "1" });
        s.Tables.Add(tg);

        if (p != null && p.Patterns.Count > 0)
            s.Items.Add(I(ProjTag, "γf", "", string.Join("; ", p.Patterns.Select(x => $"{x.Name} ({x.Type}) γf = {F(x.Gamma)}")), "",
                "γf hiện khai báo trong bảng trường hợp tải của công trình.", ""));
        return s;
    }

    // ------------------------------------------------------------------
    /// <summary>Bảng vùng áp lực gió QCVN 02:2022/BXD (Bảng 5.1) – chu kỳ lặp 50 năm.</summary>
    public static readonly (string Zone, double W0, double V3s)[] WindZoneTable =
    {
        ("I", 0.65, 36), ("II", 0.95, 44), ("III", 1.25, 50), ("IV", 1.55, 55), ("V", 1.85, 61)
    };

    private static AppendixSection Pl04Wind(Project p)
    {
        var w = new WindSettings();
        var s = new AppendixSection
        {
            Title = "Tải trọng gió (gió tĩnh)",
            Intro = "Chương trình tính thành phần tĩnh của gió, quy về lực tập trung tại mỗi cao trình sàn rồi chia đều cho các nút đầu cột của tầng đó. "
                  + "Giá trị W0 phụ thuộc vùng gió của địa danh: chương trình KHÔNG kèm bảng phân vùng chi tiết theo xã/phường – người dùng chọn vùng (hoặc nhập/nhập khẩu danh mục địa danh) "
                  + "theo QCVN 02:2022/BXD. Thành phần động của gió chưa tính (nhà thấp tầng ≤ 40 m thường không cần)."
        };
        s.Items.Add(I("Áp lực gió cơ sở", "W0", "Theo vùng gió I…V", string.Join("; ", WindZoneTable.Select(z => $"{z.Zone}: {F(z.W0)} kN/m² (V3s,50 = {F(z.V3s, "0")} m/s)")), "",
            "Mặc định W0 = " + F(w.W0) + " kN/m² (vùng II). Tra vùng của địa danh trong QCVN 02:2022/BXD (Phụ lục phân vùng) rồi nhập vào ô W0.", "QCVN 02:2022/BXD Bảng 5.1; TCVN 2737:2023"));
        s.Items.Add(I("Dạng địa hình", "A, B, C", "", "A: trống trải (bờ biển, hồ lớn, cánh đồng); B: tương đối trống trải, có vật cản thưa cao ≤ 10 m (ngoại ô, làng); C: bị che chắn mạnh, vật cản cao ≥ 10 m dày đặc (trong đô thị)", "",
            "Mặc định địa hình " + w.Terrain + ". Địa hình càng trống trải gió càng lớn (k(z) lớn hơn).", "TCVN 2737:2023"));
        s.Items.Add(I("Hệ số thay đổi áp lực gió theo độ cao", "k(z)", "k(z) = 1,844·(z/zg)^(2α), z ≥ 3 m", "A: α = 0,07, zg = 250 m; B: α = 0,09, zg = 300 m; C: α = 0,14, zg = 400 m", "",
            "z là cao độ sàn so với chân cột (cao độ móng). Dạng hàm mũ gần đúng – đối chiếu bảng k(z) của tiêu chuẩn áp dụng (xem bảng tra bên dưới).", "TCVN 2737:2023 (dạng hàm mũ)"));
        s.Items.Add(I("Hệ số khí động tổng", "c", "c = c(đón gió) + |c(hút gió)| = 0,8 + 0,6", "Mặc định c = " + F(w.Cp), "",
            "Gộp áp lực mặt đón gió và hút mặt khuất gió thành một lực ngang tổng. Khung tiền chế dùng hệ số riêng: cột đón gió 0,8, cột khuất gió −0,5, mái đón gió −0,6, mái khuất gió −0,4.", "TCVN 2737:2023 (bảng hệ số khí động)"));
        s.Items.Add(I("Hệ số hiệu chỉnh", "kh", "", "Mặc định " + F(w.Factor), "",
            "Người dùng nhập khi cần kể hệ số theo thời gian sử dụng / hệ số gió giật theo tiêu chuẩn áp dụng.", ""));
        s.Items.Add(I("Áp lực gió tiêu chuẩn tại cao trình z", "W", "W = W0·k(z)·c·kh", "kN/m²", "γf gió = 1,2 (trường hợp GX, GY)",
            "Giá trị tiêu chuẩn; hệ số γf được đưa vào khi tổ hợp. Ô \"γ gió\" trong thiết lập gió không được dùng – γf lấy theo trường hợp tải GX/GY.", "TCVN 2737:2023"));
        s.Items.Add(I("Lực gió tại sàn tầng", "Fi", "Fi = W(zi)·hi·B, hi = ½·h(tầng dưới) + ½·h(tầng trên)", "B = bề rộng đón gió (mặc định = kích thước mặt bằng vuông góc phương gió)", "",
            "Tầng trên cùng chỉ lấy ½ chiều cao tầng dưới. Lực chia đều cho các nút đầu cột tại cao trình sàn; khi có sàn cứng hợp lực coi như đặt tại tâm các nút.", ""));

        var tk = new AppendixTable { Title = "k(z) theo công thức của chương trình", Headers = new[] { "z (m)", "Địa hình A", "Địa hình B", "Địa hình C" } };
        foreach (double z in new[] { 3.0, 5, 10, 15, 20, 25, 30, 40 })
            tk.Rows.Add(new[] { F(z, "0"), F(LateralLoadGenerator.Kz(z, "A"), "0.000"), F(LateralLoadGenerator.Kz(z, "B"), "0.000"), F(LateralLoadGenerator.Kz(z, "C"), "0.000") });
        s.Tables.Add(tk);
        var tz = new AppendixTable { Title = "Vùng áp lực gió (QCVN 02:2022/BXD, chu kỳ lặp 50 năm)", Headers = new[] { "Vùng", "W0 (kN/m²)", "W0 (daN/m²)", "V3s,50 (m/s)" } };
        foreach (var z in WindZoneTable) tz.Rows.Add(new[] { z.Zone, F(z.W0), F(z.W0 * 100, "0"), F(z.V3s, "0") });
        tz.Note = "Danh mục vùng theo từng xã/phường: tra QCVN 02:2022/BXD; chương trình không kèm sẵn bảng đầy đủ.";
        s.Tables.Add(tz);

        if (p != null)
        {
            var pw = p.Wind;
            double H = p.Stories.Count > 0 ? p.Stories.Max(x => x.Elevation) - p.BaseElevation : 0;
            s.Items.Add(I(ProjTag, "W0, k, c", "W(đỉnh) = W0·k(H)·c·kh",
                $"Tính gió: {(pw.Enabled ? "có" : "không")}; W0 = {F(pw.W0)} kN/m²; địa hình {pw.Terrain}; c = {F(pw.Cp)}; kh = {F(pw.Factor)}"
                + (H > 0 ? $"; H = {F(H)} m → k(H) = {F(LateralLoadGenerator.Kz(H, pw.Terrain), "0.000")}, W(H) = {F(pw.W0 * LateralLoadGenerator.Kz(H, pw.Terrain) * pw.Cp * pw.Factor, "0.000")} kN/m²" : "")
                + (string.IsNullOrWhiteSpace(p.Location) ? "" : "; địa điểm: " + p.Location),
                "", "Kiểm tra lại W0 và địa hình đúng với địa danh xây dựng.", ""));
        }
        return s;
    }

    // ------------------------------------------------------------------
    /// <summary>Tham số phổ (S, TB, TC, TD) suy ngược từ hàm phổ của chương trình (LateralLoadGenerator.Sd) – luôn khớp mã nguồn.</summary>
    public static (double S, double TB, double TC, double TD) SpectrumParams(string soil)
    {
        // q nhỏ để nhánh phổ luôn lớn hơn cận dưới β·ag; K = 2,5/q
        const double q = 0.5, K = 2.5 / q;
        var st = new SeismicSettings { AgR = 1.0, ImportanceFactor = 1.0, Q = q, Soil = soil };
        // vùng phẳng (TB < T ≤ TC): Sd = ag·S·K
        double S = LateralLoadGenerator.Sd(0.30, st) / K;
        // nhánh TC < T ≤ TD: Sd = ag·S·K·TC/T
        double TC = LateralLoadGenerator.Sd(1.5, st) * 1.5 / (K * S);
        // nhánh T ≤ TB: Sd = ag·S·(2/3 + T/TB·(K − 2/3))
        const double t0 = 0.05;
        double TB = t0 * (K - 2.0 / 3.0) / (LateralLoadGenerator.Sd(t0, st) / S - 2.0 / 3.0);
        // nhánh T > TD: Sd = ag·S·K·TC·TD/T²
        double TD = LateralLoadGenerator.Sd(3.0, st) * 9.0 / (K * S * TC);
        return (Math.Round(S, 3), Math.Round(TB, 3), Math.Round(TC, 3), Math.Round(TD, 3));
    }

    private static AppendixSection Pl05Seismic(Project p)
    {
        var d = new SeismicSettings();
        var s = new AppendixSection
        {
            Title = "Tải trọng động đất (phương pháp tĩnh lực ngang tương đương)",
            Intro = "Động đất được quy về lực ngang tĩnh đặt tại các sàn, tính từ phổ thiết kế loại 1 của TCVN 9386:2012 và chu kỳ dao động cơ bản ước lượng T1. "
                  + "Phương pháp này áp dụng cho nhà đều đặn theo mặt đứng, T1 ≤ min(4TC; 2 s). Chưa xét độ lệch tâm ngẫu nhiên và chưa phân tích phổ phản ứng dạng dao động."
        };
        s.Items.Add(I("Đỉnh gia tốc nền tham chiếu", "agR", "", "Tra Phụ lục H TCVN 9386:2012 theo địa danh (đơn vị g); mặc định " + F(d.AgR), "",
            "Bật \"Tính động đất\" và nhập agR. Theo tiêu chuẩn: ag < 0,04g (động đất rất yếu) không cần thiết kế kháng chấn; 0,04g ≤ ag < 0,08g (yếu) được dùng biện pháp đơn giản – chương trình không tự kiểm tra ngưỡng này.", "TCVN 9386:2012 Phụ lục H; mục 3.2.1"));
        s.Items.Add(I("Hệ số tầm quan trọng", "γI", "ag = γI·agR", "Mặc định γI = " + F(d.ImportanceFactor) + " (nhà ở thông thường, cấp II)", "",
            "Công trình quan trọng hơn (trường học, bệnh viện…) lấy γI > 1 theo Phụ lục E/F TCVN 9386:2012.", "TCVN 9386:2012 mục 4.2.5"));
        s.Items.Add(I("Loại nền đất", "A…E", "", "A: đá; B: cát/sỏi rất chặt, sét rất cứng; C: cát chặt vừa, sét cứng; D: đất rời xốp – trung bình, sét mềm; E: lớp phủ mỏng trên nền cứng. Mặc định " + d.Soil, "",
            "Xác định từ hồ sơ khảo sát (vs,30, NSPT, cu). Ảnh hưởng tới S, TB, TC, TD.", "TCVN 9386:2012 Bảng 3.1"));
        s.Items.Add(I("Phổ thiết kế (phổ loại 1)", "Sd(T)",
            "0 ≤ T ≤ TB: Sd = ag·S·[2/3 + T/TB·(2,5/q − 2/3)]\nTB ≤ T ≤ TC: Sd = ag·S·2,5/q\nTC ≤ T ≤ TD: Sd = max(ag·S·2,5/q·TC/T; β·ag)\nT ≥ TD: Sd = max(ag·S·2,5/q·TC·TD/T²; β·ag)",
            "β = 0,2", "q (hệ số ứng xử) đóng vai trò giảm lực do khả năng biến dạng dẻo",
            "Sd tính theo đơn vị g. Xem bảng tham số phổ theo loại nền bên dưới.", "TCVN 9386:2012 mục 3.2.2.5, công thức (3.13)–(3.16)"));
        s.Items.Add(I("Hệ số ứng xử", "q", "", "Mặc định q = " + F(d.Q) + " (khung BTCT nhiều tầng nhiều nhịp, cấp dẻo trung bình: q = 3,0·αu/α1 = 3,0×1,3)", "",
            "Khung BTCT dẻo thấp (DCL) lấy q = 1,5. Nhập sai q làm lực động đất sai tỉ lệ nghịch.", "TCVN 9386:2012 mục 5.2.2.2"));
        s.Items.Add(I("Chu kỳ dao động cơ bản", "T1", "T1 = Ct·H^(3/4)", "Ct = 0,075 (khung BTCT); 0,085 (khung thép – khi > ½ số thanh là thép); H = chiều cao từ chân cột tới sàn trên cùng (m)", "",
            "Công thức gần đúng cho nhà cao ≤ 40 m. Có thể so với chu kỳ ETABS (thường ETABS cho T1 lớn hơn vì kể khối lượng đầy đủ và độ cứng thực).", "TCVN 9386:2012 mục 4.3.3.2.2, công thức (4.6)"));
        s.Items.Add(I("Trọng lượng tham gia dao động", "W", "W = Σ TT + Σ HTHT + ψE·Σ HT", "ψE = φ·ψ2, mặc định " + F(d.PsiLive), "",
            "Gồm trọng lượng bản thân thanh, tải trên dầm (kể tải sàn đã dồn), tải nút hướng xuống. Trọng lượng cột chia đôi cho 2 cao trình; phần sát chân cột bỏ qua.", "TCVN 9386:2012 mục 3.2.4, 4.2.4"));
        s.Items.Add(I("Hệ số hiệu chỉnh", "λ", "λ = 0,85 nếu T1 ≤ 2TC và nhà > 2 tầng; ngược lại λ = 1,0", "", "", "Kể đến khối lượng hữu hiệu của dạng dao động cơ bản nhỏ hơn tổng khối lượng.", "TCVN 9386:2012 mục 4.3.3.2.2"));
        s.Items.Add(I("Lực cắt đáy", "Fb", "Fb = Sd(T1)·W·λ", "kN", "Tổ hợp động đất dùng hệ số 1,0 (tổ hợp đặc biệt)", "Tổng lực ngang động đất theo mỗi phương.", "TCVN 9386:2012 công thức (4.5)"));
        s.Items.Add(I("Phân phối lực theo tầng", "Fi", "Fi = Fb·zi·Wi / Σ(zj·Wj)", "zi: cao độ sàn i so với chân cột", "",
            "Dạng dao động cơ bản tuyến tính theo chiều cao. Lực tầng chia đều cho các nút đầu cột như tải gió.", "TCVN 9386:2012 công thức (4.11)"));
        var t = new AppendixTable { Title = "Tham số phổ đàn hồi loại 1 theo loại nền (lấy từ hàm phổ của chương trình)", Headers = new[] { "Nền", "S", "TB (s)", "TC (s)", "TD (s)" } };
        foreach (var soil in new[] { "A", "B", "C", "D", "E" })
        {
            var sp = SpectrumParams(soil);
            t.Rows.Add(new[] { soil, F(sp.S, "0.00"), F(sp.TB, "0.00"), F(sp.TC, "0.00"), F(sp.TD, "0.0") });
        }
        s.Tables.Add(t);
        if (p != null)
        {
            var ps = p.Seismic;
            string txt = $"Tính động đất: {(ps.Enabled ? "có" : "không")}; agR = {F(ps.AgR, "0.0000")}g; nền {ps.Soil}; γI = {F(ps.ImportanceFactor)}; q = {F(ps.Q)}; ψE = {F(ps.PsiLive)}";
            var stories = p.Stories.Where(x => x.Elevation > p.BaseElevation + 1e-3).ToList();
            if (stories.Count > 0 && p.Frames.Count > 0)
            {
                double H = stories.Max(x => x.Elevation) - p.BaseElevation;
                bool steel = p.Frames.Count(f => !f.IsConcrete) > p.Frames.Count / 2;
                double T1 = (steel ? 0.085 : 0.075) * Math.Pow(H, 0.75);
                txt += $"; H = {F(H)} m → T1 = {F(T1, "0.000")} s, Sd(T1) = {F(LateralLoadGenerator.Sd(T1, ps), "0.0000")}g";
            }
            s.Items.Add(I(ProjTag, "agR, T1, Sd", "", txt, "", "Lực tầng chi tiết xem bảng \"Lực ngang tầng\" của thuyết minh/Excel sau khi phân tích.", ""));
        }
        return s;
    }

    // ------------------------------------------------------------------
    private static AppendixSection Pl06Combos(Project p)
    {
        var s = new AppendixSection
        {
            Title = "Tổ hợp tải trọng",
            Intro = "Lệnh Phân tích → Tạo tổ hợp tải sinh tự động các tổ hợp dưới đây (xoá các tổ hợp cũ). Mỗi trường hợp tải được phân tích riêng rồi cộng tuyến tính theo hệ số. "
                  + "Thiết kế cấu kiện lấy bao (BAO) của tất cả tổ hợp TTGH I; võng và áp lực nền móng băng dùng tổ hợp TTGH II."
        };
        s.Items.Add(I("Tổ hợp cơ bản 1 – tĩnh tải + hoạt tải", "TH1", "Σγf·TT + γf·HT (+ γf·tải khác)", "", "γf theo từng trường hợp",
            "Tĩnh tải gồm TT và HTHT; hoạt tải gồm HT và hoạt tải mái. Tất cả hoạt tải lấy đồng thời (chưa xét chất tải lệch nhịp).", "TCVN 2737:2023"));
        s.Items.Add(I("Tổ hợp cơ bản 1 – tĩnh tải + gió", "TH…", "Σγf·TT ± γf·W", "Mỗi phương gió GX, GY với dấu ±", "γf gió = 1,2", "Gió X và gió Y không cộng đồng thời.", "TCVN 2737:2023"));
        s.Items.Add(I("Tổ hợp cơ bản 2", "TH…", "Σγf·TT + ψ·(γf·HT) ± ψ·(γf·W)", "ψ = 0,9", "ψ = 0,9 (hệ số tổ hợp khi có ≥ 2 tải tạm thời)",
            "Khi có từ 2 tải tạm thời trở lên, các tải tạm thời nhân thêm ψ = 0,9.", "TCVN 2737:2023"));
        s.Items.Add(I("Tổ hợp đặc biệt có động đất", "TH…", "TT + HTHT + ψE·HT ± E1 ± 0,3·E2", "ψE = " + F(new SeismicSettings().PsiLive) + "; E1 phương chính, E2 phương vuông góc", "Hệ số 1,0 cho tĩnh tải và động đất",
            "Chỉ sinh khi bật động đất và có trường hợp tải loại Seismic. Không tổ hợp gió với động đất.", "TCVN 9386:2012 mục 4.3.3.5"));
        s.Items.Add(I("Tổ hợp tiêu chuẩn (TTGH II)", "TC…", "TC1 = TT + HTHT + HT; TC… = TT + HTHT + 0,9·HT ± 0,9·W", "Hệ số 1,0 (giá trị tiêu chuẩn)", "",
            "Dùng tính võng dầm/sàn, chuyển vị khung thép, áp lực đáy móng băng.", "TCVN 2737:2023"));
        s.Items.Add(I("Tổ hợp bao", "BAO", "max/min của mọi tổ hợp TTGH I", "", "", "Thiết kế dầm, cột, sàn, móng lấy nội lực bất lợi nhất qua các tổ hợp TTGH I.", ""));
        s.Items.Add(I("Quy đổi nội lực tính toán → tiêu chuẩn cho móng đơn", "kt", "Ntc = Ntt / kt", "kt = " + F(new SoilSettings().LoadFactorAvg) + " (hệ số vượt tải trung bình)", "",
            "Móng đơn kiểm tra áp lực nền bằng nội lực tính toán chia kt (gần đúng); móng băng dùng trực tiếp tổ hợp TC nếu có.", "TCVN 9362:2012"));
        if (p != null)
            s.Items.Add(I(ProjTag, "", "", $"{p.Combinations.Count(c => c.Type == ComboType.ULS)} tổ hợp TTGH I; {p.Combinations.Count(c => c.Type == ComboType.SLS)} tổ hợp TTGH II; "
                + $"{p.Combinations.Count(c => c.Type == ComboType.Envelope)} tổ hợp bao", "", "Nếu số tổ hợp = 0 hãy chạy Phân tích → Tạo tổ hợp tải.", ""));
        return s;
    }

    // ------------------------------------------------------------------
    private static AppendixSection Pl07Analysis(Project p)
    {
        var s = new AppendixSection
        {
            Title = "Phân tích kết cấu (phần tử hữu hạn khung không gian)",
            Intro = "Chương trình giải khung không gian bằng phương pháp độ cứng trực tiếp (như ETABS/SAP2000 ở mức thanh). Bài toán tuyến tính đàn hồi bậc 1: "
                  + "nội lực tỉ lệ với tải, có thể cộng theo tổ hợp. Sàn không được mô hình bằng phần tử tấm mà chỉ truyền tải về dầm và (nếu bật) làm sàn cứng trong mặt phẳng."
        };
        s.Items.Add(I("Phần tử thanh Timoshenko 12 bậc tự do", "K", "k_uốn = E·I/((1+Φ)·L³)·[12, 6L, −12, 6L; 6L, (4+Φ)L², −6L, (2−Φ)L²; …], Φ = 12·E·I/(G·As·L²)",
            "As = 5/6·b·h (tiết diện chữ nhật); thép I: diện tích bụng", "",
            "Kể biến dạng trượt như ETABS/SAP. Bậc tự do mỗi nút: UX, UY, UZ, RX, RY, RZ.", ""));
        s.Items.Add(I("Giải phóng liên kết đầu thanh", "K*", "K* = Kkk − Kkr·Krr⁻¹·Krk; P* = Pk − Kkr·Krr⁻¹·Pr", "", "", "Khớp đầu dầm (release) được khử tĩnh ở mức phần tử.", ""));
        s.Items.Add(I("Tải phân bố trên thanh", "P0", "Tải nút tương đương bằng hàm dạng Hermite", "Tải tuyến tính từng đoạn + lực tập trung", "", "Nội lực xuất tại các mặt cắt cách nhau ≤ 0,5 m và tại điểm đặt lực.", ""));
        s.Items.Add(I("Thanh tiết diện thay đổi", "", "Chia thành n đoạn lăng trụ", "n = " + new SteelDesignSettings().TaperSegments + " (cột/kèo vát khung tiền chế)", "", "Mỗi đoạn lấy tiết diện tại giữa đoạn.", ""));
        s.Items.Add(I("Sàn tuyệt đối cứng (diaphragm)", "", "UX, UY, RZ của các nút sàn cùng cao độ ràng buộc theo nút chủ tại trọng tâm nhóm nút", "Bật/tắt trong thiết lập (mặc định bật)", "",
            "Phù hợp sàn BTCT toàn khối. Khung tiền chế (mái tôn) tự tắt. Lực dọc trong dầm khi bật sàn cứng gần bằng 0.", ""));
        s.Items.Add(I("Giải hệ phương trình", "K·u = P", "Ma trận skyline, phân tích LDLᵀ (Crout – Bathe)", "", "", "Báo lỗi kèm số phương trình nếu kết cấu biến hình (thiếu liên kết).", ""));
        s.Items.Add(I("Độ cứng tiết diện", "E·I, E·A", "Tiết diện nguyên, không hệ số giảm", "Không có vùng cứng đầu thanh (end offset)", "",
            "Đơn giản hoá – khi so với ETABS cần tắt Property Modifiers và End Length Offsets hoặc khai báo giống nhau.", ""));
        s.Items.Add(I("Giới hạn của phân tích", "", "", "Chưa P-Delta; chưa phân tích dao động / phổ phản ứng; chưa tương tác nền – kết cấu khi phân tích khung (chân cột ngàm/khớp)", "",
            "Nhà ≤ 7 tầng thông thường ảnh hưởng P-Delta nhỏ. Cần kiểm tra chéo bằng ETABS/SAP2000 (xuất .e2k/.s2k) với công trình quan trọng.", ""));
        return s;
    }

    // ------------------------------------------------------------------
    private static AppendixSection Pl08Beam(Project p)
    {
        var cd = new ConcreteDesignSettings();
        var s = new AppendixSection
        {
            Title = "Dầm BTCT",
            Intro = "Dầm được gom thành dải dầm liên tục (cùng nhóm thiết kế, tầng, tiết diện, thẳng hàng), chia nhịp tại cột hoặc dầm giao cao không kém. "
                  + "Mỗi nhịp tính thép tại mép gối trái, giữa nhịp, mép gối phải và lực cắt tại mép gối. Tiết diện tính như chữ nhật b×h (bỏ qua cánh chữ T – thiên về an toàn)."
        };
        s.Items.Add(I("Mô men thiết kế", "M", "Mgối = M tại mép gối; Mnhịp = max M trong đoạn giữa hai mép gối", "Mép gối = min(bgối/2; 0,45L) từ tim gối", "",
            "Lấy bao mọi tổ hợp TTGH I. Mô men âm tại gối → thép trên; dương giữa nhịp → thép dưới.", ""));
        s.Items.Add(I("Hệ số mô men", "αm", "αm = M / (Rb·b·h0²)", "h0 = h − a, a = c + 10 mm", "", "Đại lượng không thứ nguyên đo mức độ \"nặng\" của tiết diện.", "TCVN 5574:2018 mục 8.1.2.2.4"));
        s.Items.Add(I("Diện tích cốt thép đơn", "As", "αm ≤ αR: ξ = 1 − √(1 − 2αm); As = ξ·Rb·b·h0 / Rs", "", "", "Trường hợp thông thường.", "TCVN 5574:2018 mục 8.1.2.2"));
        s.Items.Add(I("Cốt kép", "As', As", "αm > αR: As' = (M − αR·Rb·b·h0²)/(Rsc·(h0 − a')); As = (ξR·Rb·b·h0 + Rsc·As')/Rs", "", "",
            "Khi As' > 0,5·As chương trình khuyên tăng tiết diện.", "TCVN 5574:2018 mục 8.1.2.2"));
        s.Items.Add(I("Hàm lượng thép tối thiểu", "μmin", "As ≥ μmin·b·h0", "μmin = " + F(cd.MinRatioBeam) + "%", "", "Tránh phá hoại đột ngột khi bê tông vừa nứt.", "TCVN 5574:2018 mục 10.3.3"));
        s.Items.Add(I("Khả năng chịu mô men (kiểm tra thép đã bố trí)", "Mu", "x = (Rs·As − Rsc·As')/(Rb·b); 2a' ≤ x ≤ ξR·h0: Mu = Rb·b·x·(h0 − x/2) + Rsc·As'·(h0 − a'); x < 2a': Mu = Rs·As·(h0 − a')", "x ≤ ξR·h0", "",
            "h0 tính lại theo số hàng thép thực tế (khe hở giữa hàng 25 mm).", "TCVN 5574:2018 mục 8.1.2.2.4"));
        s.Items.Add(I("Kiểm tra dải bê tông chịu nén giữa vết nứt nghiêng", "Q ≤ 0,3Rb·b·h0", "Q ≤ 0,3·Rb·b·h0", "", "", "Không đạt → phải tăng tiết diện (tăng đai không có tác dụng).", "TCVN 5574:2018 mục 8.1.3.2"));
        s.Items.Add(I("Khả năng chịu cắt của bê tông", "Qb", "Qb = 1,5·Rbt·b·h0²/c, c = 2h0 → Qb = 0,75·Rbt·b·h0", "0,5·Rbt·b·h0 ≤ Qb ≤ 2,5·Rbt·b·h0", "",
            "Đơn giản hoá: chỉ xét một tiết diện nghiêng c = c0 = 2h0.", "TCVN 5574:2018 mục 8.1.3.3.2"));
        s.Items.Add(I("Cốt đai yêu cầu", "qsw, s", "Q ≤ 0,5·Rbt·b·h0: đai cấu tạo; ngược lại qsw = max((Q − Qb)/(1,5·h0); 0,25·Rbt·b); s = Rsw·Asw/qsw", "Đai 2 nhánh; Ø nhỏ nhất trong danh sách đạt yêu cầu (h > 800 mm: Ø ≥ 8)", "",
            "Qsw = 0,75·qsw·c0 với c0 = 2h0. Khả năng chịu cắt Qu = min(Qb + 1,5·qsw·h0; 0,3·Rb·b·h0), đai chỉ được kể khi qsw ≥ 0,25·Rbt·b.", "TCVN 5574:2018 mục 8.1.3.3"));
        s.Items.Add(I("Bước đai cấu tạo", "s", "Vùng gối: s ≤ min(h0/2; 300 mm; Rbt·b·h0²/Q); giữa nhịp: s ≤ min(0,75h0; 500 mm)", "Làm tròn xuống bội số 50 mm, tối thiểu 100 mm", "",
            "Bước tính được < 100 mm → báo lỗi, cần tăng Ø hoặc số nhánh đai.", "TCVN 5574:2018 mục 10.3.4"));
        s.Items.Add(I("Độ cứng chống uốn khi tính võng", "B", "B = Eb,red·Ieff; Eb,red = Eb/(1 + φb,cr); Ieff = Icr/(1 − (1 − Icr/Ig)·(Mcr/Ms)²) ≤ Ig; Mcr = Rbt,ser·b·h²/6",
            "φb,cr = " + F(RcSection.CreepLongTerm) + "; Rbt,ser ≈ 1,3·Rbt; Icr từ b·x²/2 = αs·As·(h0 − x), αs = Es/Eb,red", "",
            "Kể nứt (theo thép thực bố trí) và từ biến dài hạn. Nội suy Ieff kiểu Bischoff – gần đúng tương đương mục 8.2.3.", "TCVN 5574:2018 mục 8.2.3 (gần đúng)"));
        s.Items.Add(I("Độ võng dầm liên tục", "f", "f = 5/48·[Mg − 0,1·(Mt + Mp)]·L²/B", "Mg: mô men giữa nhịp; Mt, Mp: trị tuyệt đối mô men âm hai gối – cùng tổ hợp TTGH II; L: nhịp tim gối", "Giới hạn f ≤ L/" + F(cd.BeamDeflectionLimit),
            "Công xôn: f = M·L²/(4·B), giới hạn 2L/" + F(cd.BeamDeflectionLimit) + ".", "TCVN 5574:2018 mục 8.2.3; giới hạn võng theo tiêu chuẩn áp dụng (TCVN 2737:2023 – chuyển vị và độ võng)"));
        s.Items.Add(I("Bố trí thép dầm", "", "", "2 thanh góc chạy suốt trên và dưới (neo 30d vào gối biên, đoạn bẻ ≥ 10d); thép tăng cường dưới dài 0,8·L0 giữa nhịp; mũ gối giữa vươn L0/3, gối biên L0/4 từ mép gối; giá cạnh khi h > 700 mm; cây thép dài 11,7 m (dài hơn phải nối chồng)", "",
            "Tối đa 2 loại Ø (chênh ≤ 6 mm); khoảng hở giữa các thanh ≥ max(25 mm; d). L0 là nhịp thông thủy.", "TCVN 5574:2018 mục 10.3"));
        s.Items.Add(I("Đánh số hiệu thép", "", "", "Mỗi cấu kiện đánh số riêng từ 1; thứ tự: thép dọc (dưới, trên, mũ gối, tăng cường, giá) → đai, đai C, đai lồng → thép rải; khác vai trò/Ø/chiều dài/hình dạng → khác số", "",
            "Theo quy tắc thể hiện kiểu Kết cấu Soft; bảng thống kê gộp theo số hiệu.", ""));
        if (p != null)
        {
            var c = p.Concrete;
            s.Items.Add(I(ProjTag, "", "", $"Bê tông dầm {c.ConcreteBeam}; thép dọc {c.RebarMain}; đai {c.RebarStirrup}; c = {F(c.CoverBeam * 1000)} mm; μmin = {F(c.MinRatioBeam)}%; [f] = L/{F(c.BeamDeflectionLimit)}; Ø dọc: {string.Join(", ", c.BeamBars)}", "", "", ""));
        }
        return s;
    }

    // ------------------------------------------------------------------
    private static AppendixSection Pl09Column(Project p)
    {
        var cd = new ConcreteDesignSettings();
        var s = new AppendixSection
        {
            Title = "Cột BTCT (nén lệch tâm xiên)",
            Intro = "Cột chữ nhật, cốt thép đặt đối xứng trên chu vi. Mỗi tầng tính riêng tại hai đầu cột cho mọi tổ hợp, kể độ lệch tâm ngẫu nhiên và uốn dọc. "
                  + "Chương trình thử các phương án Ø và số thanh tăng dần, chọn phương án nhỏ nhất thoả kiểm tra đường bao tải trọng."
        };
        s.Items.Add(I("Chiều dài tính toán", "l0", "l0 = ψ·H", "ψ = " + F(cd.ColumnLengthFactor) + " (khung nhiều tầng nhiều nhịp, sàn đổ toàn khối); H: chiều cao tầng", "",
            "Khung một nhịp hoặc sàn lắp ghép nên tăng ψ (1,0–1,2) trong thiết lập.", "TCVN 5574:2018 mục 8.1.2.4"));
        s.Items.Add(I("Độ mảnh", "λ", "λ = l0/i, i = h/√12 (mỗi phương)", "λ > 120 → báo lỗi; λ ≤ 14: bỏ qua uốn dọc (η = 1)", "", "", "TCVN 5574:2018 mục 10.2.2"));
        s.Items.Add(I("Độ lệch tâm ngẫu nhiên", "ea", "ea = max(L/600; h/30; 10 mm)", "", "", "Mô men thiết kế mỗi phương M = η·max(|M|; N·ea).", "TCVN 5574:2018 mục 8.1.2.2"));
        s.Items.Add(I("Hệ số uốn dọc", "η", "η = 1/(1 − N/Ncr); Ncr = π²·D/l0²; D = kb·Eb·I + ks·Es·Is; kb = 0,15/(φL·(0,3 + δe)); ks = 0,7", "δe = e0/h ∈ [0,15; 1,5]; φL = 2,0 (chưa tách tải dài hạn – thiên về an toàn)", "",
            "N ≥ Ncr → cột mất ổn định, phải tăng tiết diện.", "TCVN 5574:2018 mục 8.1.2.4"));
        s.Items.Add(I("Khả năng chịu uốn một phương tại lực dọc N", "Mu2, Mu3", "Khối ứng suất chữ nhật cao 0,8x với Rb; εb2 = 0,0035 tại thớ nén; σs = Es·εs ∈ [−Rs; Rsc]; x dò bằng chia đôi để ΣN = N", "", "",
            "Mô hình tương thích biến dạng gần đúng mô hình phi tuyến của tiêu chuẩn.", "TCVN 5574:2018 mục 8.1.2.3, 8.1.2.7 (gần đúng)"));
        s.Items.Add(I("Kiểm tra nén lệch tâm xiên (đường bao tải trọng)", "U", "U = (M2/Mu2)^α + (M3/Mu3)^α ≤ 1; α = 1 + N/N0 ∈ [1; 2]; N0 = Rb·A + Rsc·As", "", "U ≤ 1 là đạt",
            "Phương pháp Bresler (load contour) – đơn giản hoá thay cho tích phân mặt chịu lực.", ""));
        s.Items.Add(I("Hàm lượng thép", "μ", "μ = As/(b·h)", "μmin = " + F(cd.MinRatioColumn) + "%; μmax = " + F(cd.MaxRatioColumn) + "%", "", "Vượt μmax → tăng tiết diện hoặc cấp bê tông.", "TCVN 5574:2018 mục 10.3.3"));
        s.Items.Add(I("Bố trí thép dọc", "", "", "Khoảng hở ≥ max(50 mm; d); khoảng cách tim ≤ 400 mm; nối chồng 30d mỗi tầng; tầng trên neo 20d vào dầm; chân cột neo 0,5 m + móc max(200 mm; 10d)", "", "", "TCVN 5574:2018 mục 10.3.5, 10.3.6"));
        s.Items.Add(I("Cốt đai cột", "s", "Giữa cột: s ≤ min(15d; bmin; 400 mm); vùng đầu cột: s = min(s/2; 100 mm) ≥ 50 mm", "Vùng đai dày dài max(h; L/6; 450 mm)", "", "d: Ø thép dọc nhỏ nhất; bmin: cạnh nhỏ tiết diện.", "TCVN 5574:2018 mục 10.3.4.3"));
        if (p != null)
        {
            var c = p.Concrete;
            s.Items.Add(I(ProjTag, "", "", $"Bê tông cột {c.ConcreteColumn}; thép {c.RebarMain}; ψ = {F(c.ColumnLengthFactor)}; μ = {F(c.MinRatioColumn)}…{F(c.MaxRatioColumn)}%; c = {F(c.CoverColumn * 1000)} mm", "", "", ""));
        }
        return s;
    }

    // ------------------------------------------------------------------
    private static AppendixSection Pl10Slab(Project p)
    {
        var cd = new ConcreteDesignSettings();
        var s = new AppendixSection
        {
            Title = "Sàn BTCT",
            Intro = "Ô sàn chữ nhật tính theo dải bản rộng 1 m, độc lập với kết quả phân tích khung. Điều kiện biên mỗi cạnh: ngàm nếu có ô sàn kề cùng cao độ (chung ≥ 10% cạnh), "
                  + "ngược lại khớp (người dùng có thể gán riêng). Ô không chữ nhật bị bỏ qua (báo cảnh báo)."
        };
        s.Items.Add(I("Tải tính toán trên sàn", "q", "q = Σ γf·(γ·t·hệ số bản thân + tải phân bố của ô)", "Không kể gió, động đất", "γf theo từng trường hợp", "qs (tiêu chuẩn) dùng tính võng.", ""));
        s.Items.Add(I("Phân loại ô bản", "ly/lx", "ly/lx ≤ 2: bản kê 4 cạnh; > 2: bản loại dầm (dải 1 phương ngắn)", "", "", "", "TCVN 5574:2018"));
        s.Items.Add(I("Chia tải bản kê 4 cạnh (Marcus)", "qx, qy", "qx·cx·lx⁴ = qy·cy·ly⁴; qx + qy = q → qx = q·cy·ly⁴/(cx·lx⁴ + cy·ly⁴)", "c = 5 (khớp–khớp); 2,08 (ngàm–khớp); 1 (ngàm–ngàm)", "",
            "Điều kiện võng bằng nhau tại giữa ô; bỏ qua giảm mô men do xoắn → thiên về an toàn.", ""));
        s.Items.Add(I("Mô men dải bản", "M1, MI, M2, MII", "Khớp–khớp: Mnh = qL²/8; ngàm–khớp: Mnh = 9qL²/128, Mg = −qL²/8; ngàm–ngàm: Mnh = qL²/24, Mg = −qL²/12", "M1, MI: phương ngắn (nhịp, gối); M2, MII: phương dài", "", "", ""));
        s.Items.Add(I("Thép sàn", "As/m", "Tính như tiết diện chữ nhật b = 1 m (công thức PL8), μmin = " + F(SlabDesigner.MinRatioSlab) + "%",
            "Bước 100 mm … min(1,5t; 200 mm), ưu tiên ≥ 150 mm; lớp dưới phương ngắn đặt ngoài; c = " + F(cd.CoverSlab * 1000) + " mm", "",
            "Ø < 10 dùng thép " + cd.RebarStirrup + ", Ø ≥ 10 dùng " + cd.RebarMain + ". Bản loại dầm: thép phân bố phương dài ≥ 10% thép chịu lực.", "TCVN 5574:2018 mục 10.3.8"));
        s.Items.Add(I("Độ võng sàn", "f", "f = c/384·qx,tc·lx⁴/B·ν; bản kê: ν = 1 − 5/6·lx²·ly²/(lx⁴ + ly⁴); B tính như dầm (PL8) cho dải 1 m", "", "Giới hạn f ≤ lx/200",
            "ν là hệ số Marcus kể xoắn (ô vuông khớp cho kết quả gần lời giải tấm 0,00406·q·a⁴/D).", "TCVN 5574:2018 mục 8.2.3"));
        s.Items.Add(I("Chiều dày sàn khuyến nghị", "tmin", "Bản kê: t ≥ lx/45; bản dầm: t ≥ lx/35", "", "", "Chỉ cảnh báo, không bắt buộc. Tự tăng tiết diện tăng 10 mm/lần tới 200 mm.", ""));
        s.Items.Add(I("Mũ gối sàn", "", "", "Vươn ¼ nhịp phương ngắn tính từ mép gối, bẻ móc xuống chạm lớp dưới; lớp dưới neo vào dầm 10d", "", "", "TCVN 5574:2018 mục 10.3"));
        return s;
    }

    // ------------------------------------------------------------------
    private static double[] BearingFactors(double phiDeg)
    {
        // suy ra A, B, D từ chính hàm FootingDesigner.BearingR (m1 = m2 = ktc = 1)
        double Rof(double b, double g, double h, double gh, double c) =>
            FootingDesigner.BearingR(new SoilSettings { Phi = phiDeg, Gamma = g, Depth = h, GammaAbove = gh, C = c, M1 = 1, M2 = 1, Ktc = 1, ROverride = 0 }, b);
        return new[] { Rof(1, 1, 0, 0, 0), Rof(0, 0, 1, 1, 0), Rof(0, 0, 0, 0, 1) };
    }

    private static AppendixSection Pl11Footing(Project p)
    {
        var sd = new SoilSettings();
        var s = new AppendixSection
        {
            Title = "Móng đơn trên nền thiên nhiên",
            Intro = "Móng đơn dưới chân cột BTCT: kích thước đáy chọn theo áp lực nền (TTGH II), chiều cao theo chọc thủng, thép đáy theo mô men tại mép cột (TTGH I). "
                  + "Thông số nền (φ, c, γ) phải lấy từ báo cáo khảo sát địa chất của lớp đất dưới đáy móng."
        };
        s.Items.Add(I("Sức chịu tải tiêu chuẩn của nền", "R", "R = m1·m2/ktc·(A·b·γII + B·h·γ'II + D·cII)", $"m1 = {F(sd.M1)}; m2 = {F(sd.M2)}; ktc = {F(sd.Ktc)}; b: cạnh ngắn móng; h = Df = {F(sd.Depth)} m (mặc định)",
            "m1, m2: hệ số điều kiện làm việc; ktc = 1,0 khi chỉ tiêu lấy từ thí nghiệm trực tiếp (1,1 nếu tra bảng)",
            "γII: trọng lượng riêng đất dưới đáy móng; γ'II: đất trên đáy móng; cII: lực dính. Nếu nhập R trực tiếp (> 0) thì dùng giá trị này.", "TCVN 9362:2012 mục 4.6.9, công thức (3)"));
        s.Items.Add(I("Hệ số A, B, D", "A, B, D", "A = 0,25π/(cotφ + φ − π/2); B = 1 + π/(cotφ + φ − π/2); D = π·cotφ/(cotφ + φ − π/2)", "φ tính bằng radian – xem bảng", "", "Tính trực tiếp từ φ thay cho tra Bảng 14.", "TCVN 9362:2012 Bảng 14"));
        s.Items.Add(I("Áp lực đáy móng tiêu chuẩn", "ptb, pmax, pmin", "p = Ntc/(L·B) + γtb·Df ± 6Mx/(L·B²) ± 6My/(B·L²); M kể cả Q·Df",
            $"γtb = {F(sd.GammaAvg)} kN/m³; Ntc = Ntt/kt, kt = {F(sd.LoadFactorAvg)}; L/B = {F(sd.AspectRatio)}; Lmin = 0,8 m", "",
            "pmax tính ở góc móng (cộng mô men hai phương). Cạnh dài đặt theo phương có mô men lớn hơn.", "TCVN 9362:2012 mục 4.6"));
        s.Items.Add(I("Điều kiện áp lực nền", "", "ptb ≤ R; pmax ≤ 1,2R; pmin ≥ 0", "", "1,2R: cho phép tăng 20% ở mép móng do lệch tâm", "Chương trình tăng cạnh móng 0,1 m/lần tới khi đạt (tối đa 200 lần).", "TCVN 9362:2012 mục 4.6.10"));
        s.Items.Add(I("Ứng suất bản thân hiệu quả", "σ'bt", "σ' = Σγi·hi (trên mực nước); dưới mực nước dùng γ' = γsat − 10 kN/m³", "", "", "Dùng trong tính lún và sức chịu tải cọc (σ'v).", "TCVN 9362:2012"));
        s.Items.Add(I("Ứng suất gây lún", "σgl", "σgl = p − σ'bt(Df)", "p: áp lực trung bình tiêu chuẩn", "", "", "TCVN 9362:2012 (tính lún)"));
        s.Items.Add(I("Ứng suất do tải trong nền", "σz", "σz = 4·k_góc·σgl (Boussinesq – móng chữ nhật, chia 4 hình chữ nhật con tại tâm)", "Chia lớp phân tố hi ≤ 0,4B", "", "", "TCVN 9362:2012 (tính lún)"));
        s.Items.Add(I("Độ sâu vùng chịu nén (dừng tính lún)", "Ha", "Dừng khi σz ≤ 0,2·σ'bt (0,1·σ'bt nếu E < 5 MPa)", "", "", "", "TCVN 9362:2012 (tính lún)"));
        s.Items.Add(I("Độ lún móng", "S", "S = 0,8·Σ σz,i·hi / Ei", "β = 0,8", "Giới hạn Sgh = 8 cm (nhà khung BTCT)", "Phương pháp cộng lún các lớp phân tố. S > Sgh → chương trình đề xuất móng cọc.", "TCVN 9362:2012 (cộng lún từng lớp); Sgh theo Bảng 16"));
        s.Items.Add(I("Nền đất tốt giả định", "", "", "Sét pha dẻo cứng: γ = 19 kN/m³, φ = 16°, c = 25 kPa, E = 10 MPa", "",
            "CHỈ dùng khi chưa áp hố khoan nào – kết quả phải được kiểm chứng bằng khảo sát địa chất thực tế.", ""));
        s.Items.Add(I("Chọc thủng móng", "F ≤ Fu", "F = N − pnet·(hc + 2h0)(bc + 2h0); Fu = Rbt·u·h0; u = 2·(hc + h0 + bc + h0); pnet = N/(L·B)", "Chiều cao móng ≥ 300 mm, tăng 50 mm/lần", "",
            "Tháp chọc thủng 45°, chu vi tính toán cách mép cột h0/2. Dùng nội lực tính toán, áp lực ròng (không kể trọng lượng móng, đất).", "TCVN 5574:2018 mục 8.1.6.2, công thức (8.88)"));
        s.Items.Add(I("Mô men và thép đáy móng", "M, As", "M = B·l²·(2·pmax + pI)/6 với l = (L − hc)/2; As = M/(0,9·Rs·h0)", "μmin = 0,05%·h0 trên 1 m; bước 100…200 mm; thép bẻ móc 10d (≥ 100 mm) hai đầu", "",
            "pmax, pI: áp lực ròng tính toán tại mép móng và mép cột (tuyến tính). Lớp dưới cùng theo phương L.", "TCVN 5574:2018"));
        var t = new AppendixTable { Title = "Hệ số A, B, D theo φ (tính bằng công thức của chương trình)", Headers = new[] { "φ (°)", "A", "B", "D" } };
        foreach (double phi in new[] { 0.5, 4, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34 })
        {
            var f = BearingFactors(phi);
            t.Rows.Add(new[] { F(phi, "0.#"), F(f[0], "0.00"), F(f[1], "0.00"), F(f[2], "0.00") });
        }
        s.Tables.Add(t);
        if (p != null)
        {
            var so = p.Soil;
            double R1 = FootingDesigner.BearingR(so, 1.0), R2 = FootingDesigner.BearingR(so, 2.0);
            s.Items.Add(I(ProjTag, "R", "", (so.ROverride > 0 ? $"R nhập trực tiếp = {F(so.ROverride)} kN/m²" :
                    $"φ = {F(so.Phi)}°, c = {F(so.C)} kPa, γ = {F(so.Gamma)}, γ' = {F(so.GammaAbove)} kN/m³, Df = {F(so.Depth)} m, m1 = {F(so.M1)}, m2 = {F(so.M2)}, ktc = {F(so.Ktc)} → R(b = 1 m) = {F(R1, "0.0")}, R(b = 2 m) = {F(R2, "0.0")} kN/m²"),
                "", "Kiểm tra các chỉ tiêu nền đúng với lớp đất dưới đáy móng trong báo cáo khảo sát.", ""));
        }
        return s;
    }

    // ------------------------------------------------------------------
    private static AppendixSection Pl12Pile(Project p)
    {
        var fs = new FoundationSettings();
        var s = new AppendixSection
        {
            Title = "Móng cọc: sức chịu tải cọc theo SPT, phản lực cọc, đài cọc",
            Intro = "Sức chịu tải cọc tính từ chỉ số SPT của hố khoan theo TCVN 10304:2014 Phụ lục G (công thức Nhật Bản). Đài cọc coi là tuyệt đối cứng; "
                  + "số cọc tăng dần theo các sơ đồ chuẩn cho tới khi phản lực cọc lớn nhất ≤ [P]. Nên kiểm chứng [P] bằng thí nghiệm nén tĩnh / PDA."
        };
        s.Items.Add(I("Sức chịu tải cực hạn của cọc (SPT)", "Rc,u", "Rc,u = qb·Ab + u·Σ(fc,i·lc,i + fs,i·ls,i)", "Ab: diện tích mũi; u: chu vi; lc,i / ls,i: chiều dài cọc trong lớp dính / rời", "",
            "Cộng sức kháng mũi và ma sát thân qua từng lớp đất.", "TCVN 10304:2014 Phụ lục G.3.2"));
        s.Items.Add(I("Đất rời (cát, sỏi) – ma sát thân", "fs,i", "fs,i = 10·Ns,i/3 (kPa)", "Ns,i: SPT trung bình lớp", "", "", "TCVN 10304:2014 G.3.2"));
        s.Items.Add(I("Đất rời – sức kháng mũi", "qb", "qb = 300·Np (cọc đóng/ép); qb = 150·Np (cọc khoan nhồi) (kPa)", "Np ≤ 50: SPT tại mũi cọc", "", "", "TCVN 10304:2014 G.3.2"));
        s.Items.Add(I("Đất dính (sét, sét pha, cát pha, bùn)", "cu, fc,i", "cu = 6,25·Nc (kPa); fc,i = αp·fL·cu,i; fL = 1", "αp = 0,5·ψ^(−0,5) khi ψ ≤ 1; αp = 0,5·ψ^(−0,25) khi ψ > 1; αp ≤ 1; ψ = cu/σ'v", "",
            "σ'v: ứng suất hữu hiệu thẳng đứng tại giữa lớp.", "TCVN 10304:2014 G.3.2"));
        s.Items.Add(I("Đất dính – sức kháng mũi", "qb", "qb = 9·cu", "", "", "", "TCVN 10304:2014 G.3.2"));
        s.Items.Add(I("Sức chịu tải cho phép theo đất nền", "[P]đn", "[P]đn = Rc,u / FS", "FS = 2,5", "FS = 2,5 – hệ số an toàn tổng thể (đại diện cho γk·γn/γ0 của TCVN 10304:2014)",
            "Hệ số an toàn tổng thể đơn giản hoá; đối chiếu hệ số γ0, γn, γk theo mục 7.1.11 TCVN 10304:2014 khi lập hồ sơ.", "TCVN 10304:2014 mục 7.1.11"));
        s.Items.Add(I("Sức chịu tải theo vật liệu cọc", "Pvl", "Pvl = φ·(Rb·Ab + Rsc·As)", "φ = 1 (cọc không bị uốn dọc trong đất)", "", "", "TCVN 5574:2018; TCVN 10304:2014"));
        s.Items.Add(I("Sức chịu tải thiết kế của cọc", "[P]", "[P] = min([P]đn; Pvl)", "Mặc định nhập tay [P] = " + F(fs.PileCapacity) + " kN", "", "Có thể nhập [P] từ kết quả nén tĩnh thay cho tính toán.", ""));
        s.Items.Add(I("Tự chọn độ sâu mũi cọc", "Lc", "Mũi cọc = mặt lớp đầu tiên có N ≥ 30 (cát/sỏi) hoặc N ≥ 20 (sét) + 3d", "Lc ≤ 40 m", "", "Gợi ý ban đầu – kỹ sư quyết định theo hồ sơ địa chất và thiết bị thi công.", ""));
        s.Items.Add(I("Số cọc sơ bộ", "n", "n ≥ 1,2·Nmax/[P]", "Sơ đồ chuẩn 1, 2, 3, 4, 5, 6, 8, 9 cọc; lưới m×k 12, 15, 16, 20, 25, 30, 36 cọc", "1,2: kể mô men và trọng lượng đài", "", ""));
        s.Items.Add(I("Phản lực cọc (đài cứng)", "Pi", "Pi = (N + G)/n + My'·xi/Σx² + Mx'·yi/Σy²; M' = M + Q·Hđài", "G = 1,1·γtb·L·B·max(Df; Hđài)", "Hệ số vượt tải trọng lượng đài 1,1",
            "Kiểm tra Pmax ≤ [P] và Pmin ≥ −[Pk] (sức chịu nhổ, mặc định 0 = không cho kéo). Dùng nội lực TTGH I so với [P] – thiên về an toàn.", ""));
        s.Items.Add(I("Khoảng cách và cấu tạo cọc", "s", "s ≥ k·d", $"k = {F(fs.PileSpacingFactor)}; mép đài tới mép cọc {F(fs.CapEdge * 1000)} mm; cọc ngàm vào đài {F(fs.PileEmbed * 1000)} mm; Hđài ≥ {F(fs.CapMinHeight * 1000)} mm", "", "", "TCVN 10304:2014"));
        s.Items.Add(I("Chọc thủng đài do cột", "Fcol ≤ Fu", "Fu = Rbt·2·(hc + bc + 2h0)·h0", "h0 = Hđài − đoạn ngàm cọc − 50 mm; tăng Hđài 50 mm/lần (≤ 3 m)", "", "Fcol: tổng phản lực các cọc nằm ngoài tháp chọc thủng 45°.", "TCVN 5574:2018 mục 8.1.6"));
        s.Items.Add(I("Chọc thủng đài do cọc góc", "Fu,cọc", "Fu = Rbt·min(2·(e + d + h0/2); 4·(d + h0))·h0", "e: khoảng mép đài – mép cọc", "", "", "TCVN 5574:2018 mục 8.1.6 (đơn giản hoá)"));
        s.Items.Add(I("Cắt trên tiết diện nghiêng của đài", "Qb", "Qb = 1,5·Rbt·b·h0²/c; 0,5 ≤ Qb/(Rbt·b·h0) ≤ 2,5", "c: khoảng cách mép cột – mép trong hàng cọc", "", "Đài không đặt cốt đai.", "TCVN 5574:2018 mục 8.1.3.3"));
        s.Items.Add(I("Thép đài", "M, As", "M = Σ Pi·(khoảng cách tim cọc – mép cột); As tính như tiết diện chữ nhật b = cạnh đài", "μmin = 0,05%·h0; lưới trên tối thiểu Ø12a200", "", "", "TCVN 5574:2018"));
        if (p != null)
        {
            var f = p.Foundation;
            s.Items.Add(I(ProjTag, "[P]", "", $"Cọc {(f.PileRound ? "tròn D" : "vuông ")}{F(f.PileSize * 1000)} mm, dài {F(f.PileLength)} m; [P] = {F(f.PileCapacity)} kN; [Pk] = {F(f.PileTension)} kN; s = {F(f.PileSpacingFactor)}d", "", "", ""));
        }
        return s;
    }

    // ------------------------------------------------------------------
    private static AppendixSection Pl13Strip(Project p)
    {
        var s = new AppendixSection
        {
            Title = "Móng băng (dầm trên nền đàn hồi Winkler)",
            Intro = "Các chân cột thẳng hàng được gom thành dải móng băng tiết diện chữ T ngược (bản đáy + sườn). Nền được mô tả bằng các lò xo độc lập (mô hình Winkler): "
                  + "áp lực tại mỗi điểm tỉ lệ với độ lún tại điểm đó."
        };
        s.Items.Add(I("Gom dải móng", "", "", "Tự chọn phương có nhiều cột thẳng hàng trên một trục (dung sai 50 mm), hoặc chọn X, Y, giao thoa 2 phương; vươn đầu dải " + F(new FoundationSettings().StripOverhang) + " m", "", "", ""));
        s.Items.Add(I("Hệ số nền", "k", "k = " + "120·R (kN/m³) khi chưa nhập", "Hoặc nhập trực tiếp hệ số nền (kN/m³)", "", "Ước lượng thô từ sức chịu tải – nên lấy k theo thí nghiệm bàn nén hoặc tương quan E của đất.", ""));
        s.Items.Add(I("Mô hình dầm trên nền đàn hồi", "", "Dầm Euler chia phần tử ≈ 0,25 m; lò xo nút k·B·a (a: chiều dài ảnh hưởng của nút); EI tiết diện chữ T ngược", "", "",
            "Giải cho từng tổ hợp. Mô men dương = căng thớ dưới.", ""));
        s.Items.Add(I("Kích thước sơ bộ", "h, B", "Sườn h = max(0,40 m; Lnhịp,max/7); bản cánh dày gốc 0,25 m, mép ≥ 0,20 m; B0 = ΣNtc/(L·(R − γtb·Df))", "B ≤ 6 m, tăng 50 mm/lần; sườn b mặc định 300 mm", "", "", ""));
        s.Items.Add(I("Điều kiện áp lực nền", "", "ptb ≤ R(B); pmax ≤ 1,2·R(B)", "Áp lực lấy từ lời giải Winkler với tổ hợp TTGH II (nếu chưa có thì dùng TTGH I/kt)", "1,2R", "R tính theo TCVN 9362:2012 với b = B.", "TCVN 9362:2012"));
        s.Items.Add(I("Thiết kế sườn và bản cánh", "", "Sườn b×h: thép dưới/trên theo bao mô men, đai theo lực cắt (PL8); bản cánh: công xôn (B − b)/2 chịu áp lực nền ròng", "Giá cạnh sườn khi cao: bước ≤ 400 mm", "", "", "TCVN 5574:2018"));
        return s;
    }

    // ------------------------------------------------------------------
    private static AppendixSection Pl14Steel(Project p)
    {
        var st = new SteelDesignSettings();
        var s = new AppendixSection
        {
            Title = "Kết cấu thép tiền chế (cột, kèo, xà gồ, giằng)",
            Intro = "Kiểm tra theo tinh thần TCVN 5575 (công thức kiểu SNiP II-23-81*). ĐÂY LÀ DẠNG ĐƠN GIẢN HOÁ phục vụ thiết kế sơ bộ / kiểm tra nhanh – "
                  + "phải đối chiếu lại theo bản TCVN 5575:2024 đang áp dụng (hệ số c, mx, ψ, φe, ổn định cục bộ, liên kết, chân cột). Chưa thiết kế liên kết bu lông, bản mã."
        };
        s.Items.Add(I("Cường độ tính toán", "f, fv", "f = fy/γM; fv = 0,58·f", "γM = 1,05", "γc = " + F(st.GammaC) + " (hệ số điều kiện làm việc)", "", "TCVN 5575"));
        s.Items.Add(I("Kiểm tra bền", "σ", "σ = |N|/A + |M3|/W33 + |M2|/W22 ≤ f·γc", "", "γc", "", "TCVN 5575"));
        s.Items.Add(I("Kiểm tra cắt", "τ", "τ ≈ V2/(hw·tw) ≤ fv·γc", "", "γc", "", "TCVN 5575"));
        s.Items.Add(I("Hệ số uốn dọc", "φ", "λ̄ = λ·√(f/E); λ̄ ≤ 2,5: φ = 1 − (0,073 − 5,53·f/E)·λ̄^1,5; 2,5 < λ̄ ≤ 4,5: φ = 1,47 − 13·f/E − (0,371 − 27,3·f/E)·λ̄ + (0,0275 − 5,53·f/E)·λ̄²; λ̄ > 4,5: φ = 332/(λ̄²·(51 − λ̄))", "Xem bảng tra", "",
            "", "TCVN 5575 / SNiP II-23-81* công thức (8)–(9)"));
        s.Items.Add(I("Ổn định trong mặt phẳng khung", "", "N/(φx·A·f·γc) + |M3|/(W33·f·γc·(1 − N/Ne)) + |M2|/(W22·f·γc) ≤ 1; Ne = π²·E·A/λx²", "", "",
            "Dùng lực Euler Ne – cách tiếp cận gần đúng thay cho φe của tiêu chuẩn.", "TCVN 5575 (gần đúng)"));
        s.Items.Add(I("Ổn định ngoài mặt phẳng", "φb", "N/(φy·A·f·γc) + |M3|/(φb·W33·f·γc) + |M2|/(W22·f·γc) ≤ 1; φb = 1 khi l0y ≤ 13·bf; φ1 = ψ·(Iy/Ix)·(h/l0y)²·E/f; ψ = 1,6 + 0,08·α; α = 1,54·(It/Iy)·(l0y/h)²",
            "φb = φ1 nếu φ1 ≤ 0,85, ngược lại min(1; 0,68 + 0,21·φ1)", "", "ψ lấy thấp hơn bảng tiêu chuẩn → thiên về an toàn. l0y = khoảng cách giằng ngoài mặt phẳng (mặc định 1,5 m).", "TCVN 5575 (gần đúng)"));
        s.Items.Add(I("Chiều dài tính toán", "μ", "l0 = μ·L", "Cột chân khớp μ = 2,0; chân ngàm μ = 1,5 (khung có chuyển vị ngang); kèo μ = 1,0 với chiều dài cả dãy kèo; xà gồ, giằng μ = 1,0", "", "", ""));
        s.Items.Add(I("Độ mảnh giới hạn", "[λ]", "Cột, kèo: [λ] = 180 − 60α; thanh phụ chịu nén: 210 − 60α; thanh chỉ chịu kéo, giằng: 300", "α = N/(φ·A·f·γc) ≥ 0,5", "", "", "TCVN 5575:2012 Bảng 25"));
        s.Items.Add(I("Ổn định cục bộ cánh", "b0/tf", "b0/tf ≤ 0,5·√(E/f)", "", "", "Giới hạn đơn giản hoá.", ""));
        s.Items.Add(I("Ổn định cục bộ bụng", "hw/tw", "α = (σ − σ1)/σ; α ≤ 0,5: như cột nén đúng tâm [1,30 + 0,15λ̄² (λ̄ ≤ 2) hoặc min(1,20 + 0,35λ̄; 2,3)]·√(E/f); α ≥ 1: 4,35·√((2α − 1)·E/(σ·(2 − α + √(α² + 4β²)))) ≤ 3,8·√(E/f); chủ yếu chịu uốn: 5,5·√(E/f)",
            "Bụng mảnh → kể tiết diện hữu hiệu hef = 2·0,85·tw·√(E/f)", "", "", "TCVN 5575:2012 mục 7.6.2.2, 7.6.2.6"));
        s.Items.Add(I("Chuyển vị (TTGH II)", "Δ", "Võng đỉnh kèo Δ = UZ(đỉnh) − UZ(đầu kèo) ≤ L/" + F(st.RafterDeflection) + "; chuyển vị ngang đỉnh cột ≤ H/" + F(st.ColumnDrift), "", "", "L: nhịp ngang dãy kèo; H: chiều cao cột.", "TCVN 5575; TCVN 2737:2023"));
        s.Items.Add(I("Xà gồ (dầm đơn giản uốn xiên)", "", "qn = q·cosα·a; qt = q·sinα·a; Mx = qn·L²/8; My = qt·L²/8; σ = Mx/W33 + My/W22 ≤ f·γc; τ = V/As2 ≤ 0,58·f·γc",
            "Tổ hợp: 1,1D + 1,3L; 1,1D + 0,9(1,3L + 1,2W); 0,9D + 1,2W", "Võng f = 5·qn·L⁴/(384·E·I33) ≤ L/200", "Chưa kể thanh chống xà gồ, oằn xoắn và ổn định cục bộ thép thành mỏng.", ""));
        double fe = Material.Steel("CCT34").F, E = Material.Steel("CCT34").E;
        var t = new AppendixTable { Title = "Hệ số uốn dọc φ theo λ̄ (thép CCT34, f = " + F(U.ToMPa(fe)) + " MPa)", Headers = new[] { "λ̄", "λ tương ứng", "φ" } };
        foreach (double lb in new[] { 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0, 6.0 })
            t.Rows.Add(new[] { F(lb, "0.0"), F(lb / Math.Sqrt(fe / E), "0"), F(SteelDesigner.Phi(lb, fe, E), "0.000") });
        s.Tables.Add(t);
        return s;
    }

    // ------------------------------------------------------------------
    /// <summary>Bảng chỉ tiêu cơ lý tham khảo (phạm vi điển hình) – không thay khảo sát.</summary>
    public static readonly string[][] SoilPresetRows =
    {
        new[] { "Đất đắp (chưa đầm chặt)", "16–18", "5–15", "5–15", "2–5", "2–6", "50–80" },
        new[] { "Bùn sét, bùn sét pha", "15–17", "2–6", "5–12", "0,5–2", "0–2", "< 50 (không dùng làm nền trực tiếp)" },
        new[] { "Sét, sét pha dẻo mềm", "17,5–18,5", "6–10", "10–20", "3–6", "3–6", "80–120" },
        new[] { "Sét pha dẻo cứng", "18,5–19,5", "12–18", "20–30", "8–15", "7–15", "150–200" },
        new[] { "Sét dẻo cứng – nửa cứng", "19–20,5", "15–20", "30–50", "12–25", "12–25", "200–300" },
        new[] { "Cát pha", "18–19,5", "18–24", "5–15", "6–12", "5–12", "120–200" },
        new[] { "Cát nhỏ rời", "17–18,5", "25–28", "0–2", "5–10", "4–10", "80–150" },
        new[] { "Cát vừa, chặt vừa", "18,5–19,5", "30–33", "0", "15–30", "10–30", "200–350" },
        new[] { "Cát thô, sỏi sạn chặt", "19–21", "35–40", "0", "30–50", "30–50", "350–600" },
        new[] { "Đá phong hoá (mạnh – vừa)", "21–24", "30–35", "30–100", "50–200", "> 50", "600–1000" },
    };

    private static AppendixSection Pl15Geotech(Project p)
    {
        var s = new AppendixSection
        {
            Title = "Địa chất – chỉ tiêu cơ lý tham khảo và tương quan SPT",
            Intro = "Mọi tính toán nền móng phụ thuộc vào số liệu khảo sát địa chất. Bảng dưới đây CHỈ THAM KHẢO để nhận biết số liệu có hợp lý không, hoặc lập phương án sơ bộ "
                  + "khi chưa có khảo sát. Hồ sơ thiết kế phải dùng chỉ tiêu từ báo cáo khảo sát của chính công trình (TCVN 9363 – khảo sát cho nhà cao tầng / nhà thấp tầng)."
        };
        s.Items.Add(I("Chỉ số SPT", "N", "Số búa đóng 30 cm cuối (búa 63,5 kg rơi 76 cm)", "N < 4: rất yếu; 4–10: yếu/rời; 10–30: trung bình/chặt vừa; 30–50: chặt/cứng; > 50: rất chặt/đá", "",
            "Dùng để tính sức chịu tải cọc (PL12) và nhận biết lớp yếu.", "TCVN 9351:2012"));
        s.Items.Add(I("Phân biệt đất dính / đất rời", "", "", "Dính: sét, sét pha, cát pha, bùn (và đất đắp có IL ≥ 0); rời: cát, sỏi, cuội", "",
            "Chương trình chọn công thức SPT theo loại đất của lớp: đất dính dùng cu = 6,25·N; đất rời dùng fs = 10N/3.", "TCVN 10304:2014 Phụ lục G"));
        s.Items.Add(I("Sức kháng cắt không thoát nước", "cu", "cu = 6,25·N (kPa)", "", "", "Tương quan dùng trong công thức Nhật Bản cho cọc.", "TCVN 10304:2014 G.3.2"));
        s.Items.Add(I("Trọng lượng riêng đẩy nổi", "γ'", "γ' = γsat − γw, γw = 10 kN/m³", "γsat mặc định = γ + 1 khi không nhập", "", "Dùng cho đất dưới mực nước ngầm.", ""));
        s.Items.Add(I("Đề xuất phương án móng (gợi ý tự động)", "", "Móng đơn → móng băng khi bề rộng móng > 0,5·khoảng cách cột nhỏ nhất; móng băng → móng cọc khi bề rộng băng > 0,4·khoảng cách cột (cả 2 phương) hoặc có lớp yếu (N < 4 / bùn) trong phạm vi 2B dưới đáy móng; lún S > Sgh → móng cọc",
            "", "", "Chỉ là gợi ý ban đầu – kỹ sư quyết định dựa trên địa chất, tải trọng, công trình lân cận và điều kiện thi công.", ""));
        s.Items.Add(I("Nền đất tốt giả định", "", "", "Sét pha dẻo cứng: γ = 19 kN/m³, φ = 16°, c = 25 kPa, E = 10 MPa", "", "Dùng khi chưa áp hố khoan. BẮT BUỘC kiểm chứng bằng khảo sát.", ""));
        var t = new AppendixTable { Title = "Chỉ tiêu cơ lý điển hình (CHỈ THAM KHẢO)", Headers = new[] { "Loại đất", "γ (kN/m³)", "φ (°)", "c (kPa)", "E (MPa)", "N (SPT)", "R0 (kPa)" } };
        t.Rows.AddRange(SoilPresetRows.Select(r => (string[])r.Clone()));
        t.Note = "Phạm vi điển hình tổng hợp từ kinh nghiệm – chỉ để đối chiếu sơ bộ; không dùng thay số liệu khảo sát.";
        s.Tables.Add(t);
        var sd = new SoilSettings();
        s.Items.Add(I("Thông số nền mặc định của móng đơn", "φ, c, γ", "", $"φ = {F(sd.Phi)}°; c = {F(sd.C)} kPa; γ = {F(sd.Gamma)} kN/m³; γ' (trên đáy móng) = {F(sd.GammaAbove)} kN/m³; Df = {F(sd.Depth)} m", "",
            "Giá trị khởi tạo của thiết lập nền khi tạo công trình mới – phải sửa theo khảo sát.", ""));
        return s;
    }

    // ------------------------------------------------------------------
    private static AppendixSection Pl16Preliminary(Project p)
    {
        var s = new AppendixSection
        {
            Title = "Kích thước sơ bộ (triển khai từ bản vẽ kiến trúc) và tự tăng tiết diện",
            Intro = "Khi nhập bản vẽ kiến trúc, chương trình đặt lưới cột, dầm, sàn và chọn tiết diện sơ bộ theo các tỉ lệ kinh nghiệm dưới đây. "
                  + "Sau khi phân tích – thiết kế, lệnh \"Tự tăng tiết diện đến khi đạt\" tăng dần các cấu kiện không đạt."
        };
        s.Items.Add(I("Chiều cao dầm chính", "h", "h = L/12 (làm tròn lên 50 mm)", "h ≥ 300 mm; dầm đỡ tường nặng (> 6 kN/m): h ≥ L/11", "", "L: nhịp dầm. Dầm cùng một đường trục lấy chung chiều cao lớn nhất.", ""));
        s.Items.Add(I("Chiều cao dầm phụ", "h", "h = L/14", "h ≥ 250 mm; dầm chính ≥ dầm phụ lớn nhất", "", "", ""));
        s.Items.Add(I("Chiều cao dầm công xôn", "h", "h = L/5", "h ≥ 300 mm", "", "L: chiều dài vươn.", ""));
        s.Items.Add(I("Bề rộng dầm", "b", "b = max(bề rộng tường; h/3)", "Làm tròn 50 mm", "", "", ""));
        s.Items.Add(I("Chiều dày sàn", "t", "Bản kê: t = Lngắn/45; bản dầm: t = Lngắn/35", "t ≥ 100 mm, làm tròn 10 mm", "", "", ""));
        s.Items.Add(I("Diện tích cột", "A", "A = k·N/Rb; N = Σ q·Atruyền tải qua các tầng phía trên", "q = 12 kN/m²/sàn (mái 0,8q); k = 1,2 cột giữa; 1,35 cột biên/góc", "",
            "Cạnh b = max(cạnh tối thiểu; bề rộng dầm), h = A/b; nếu h > 600 mm thì b = 300 mm. Giữ nguyên 2 tầng/lần đổi tiết diện, tầng trên không lớn hơn tầng dưới; h ≥ 300 mm.", ""));
        s.Items.Add(I("Tự tăng tiết diện – cột", "", "Tăng 50 mm/lần", "Tăng cạnh dài h tới 500 mm → tăng cạnh ngắn b lên 300 mm → tiếp tục tăng h (≤ 800 mm); cột dưới không nhỏ hơn cột trên", "", "", ""));
        s.Items.Add(I("Tự tăng tiết diện – dầm", "", "Tăng 50 mm/lần", "Tăng h (≤ 900 mm); khi h/b > 3 hoặc thiếu chỗ đặt thép thì tăng b; cả dải dầm dùng chung tiết diện", "", "", ""));
        s.Items.Add(I("Tự tăng tiết diện – sàn", "", "Tăng 10 mm/lần", "≤ 200 mm", "", "Móng không đổi (bộ thiết kế móng tự chọn kích thước). Mặc định tối đa 10 vòng phân tích – thiết kế.", ""));
        return s;
    }

    // ------------------------------------------------------------------
    private static AppendixSection Pl17Safety(Project p)
    {
        var cd = new ConcreteDesignSettings(); var st = new SteelDesignSettings(); var sd = new SoilSettings();
        var s = new AppendixSection
        {
            Title = "Bảng tổng hợp hệ số an toàn và giới hạn kiểm tra",
            Intro = "Độ an toàn của kết cấu đến từ nhiều lớp hệ số: tải trọng được phóng đại (γf), cường độ vật liệu được giảm (γb, γs, γM), điều kiện làm việc (γc, m1, m2) "
                  + "và các giới hạn sử dụng (võng, lún, chuyển vị). Bảng dưới tổng hợp tất cả giá trị chương trình đang dùng – kỹ sư cần nắm để biết kết quả \"an toàn\" tới đâu."
        };
        s.Items.Add(I("Hệ số độ tin cậy tải trọng", "γf", "Ptt = γf·Ptc", "TT 1,1; HTHT 1,2; HT 1,2; gió 1,2; động đất 1,0; khung thép: HT mái 1,3", "γf", "Người dùng sửa theo từng trường hợp tải. Xem lưu ý hoạt tải nhỏ ở PL3.", "TCVN 2737:2023"));
        s.Items.Add(I("Hệ số tổ hợp", "ψ, ψE", "", "ψ = 0,9 (≥ 2 tải tạm thời); ψE = " + F(new SeismicSettings().PsiLive) + "; thành phần động đất vuông góc 0,3", "", "", "TCVN 2737:2023; TCVN 9386:2012"));
        s.Items.Add(I("Hệ số độ tin cậy bê tông", "γb, γbt", "Rb = Rb,n/γb; Rbt = Rbt,n/γbt", "γb ≈ 1,3; γbt ≈ 1,5 (đã nằm trong Rb, Rbt)", "γb, γbt", "", "TCVN 5574:2018 mục 6.1"));
        s.Items.Add(I("Hệ số độ tin cậy cốt thép", "γs", "Rs = Rs,n/γs", "γs ≈ 1,15 (đã nằm trong Rs)", "γs", "", "TCVN 5574:2018 mục 6.2"));
        s.Items.Add(I("Hệ số vật liệu thép hình / điều kiện làm việc", "γM, γc", "f = fy/γM; kiểm tra ≤ f·γc", "γM = 1,05; γc = " + F(st.GammaC), "γM, γc", "", "TCVN 5575"));
        s.Items.Add(I("Giới hạn chiều cao vùng nén", "ξR", "ξ ≤ ξR", "CB400-V: ξR = " + F(RcSection.XiR(Material.Rebar("CB400-V").Rs), "0.000"), "Tránh phá hoại giòn", "", "TCVN 5574:2018"));
        s.Items.Add(I("Hàm lượng thép tối thiểu / tối đa", "μ", "", $"Dầm ≥ {F(cd.MinRatioBeam)}%; sàn ≥ {F(SlabDesigner.MinRatioSlab)}%; cột {F(cd.MinRatioColumn)}…{F(cd.MaxRatioColumn)}%; móng/đài ≥ 0,05%", "", "", "TCVN 5574:2018 mục 10.3.3"));
        s.Items.Add(I("Dải nén giữa vết nứt nghiêng", "", "Q ≤ 0,3·Rb·b·h0", "", "", "", "TCVN 5574:2018 mục 8.1.3.2"));
        s.Items.Add(I("Nền thiên nhiên", "m1, m2, ktc", "R = m1·m2/ktc·(…)", $"m1 = {F(sd.M1)}; m2 = {F(sd.M2)}; ktc = {F(sd.Ktc)}", "ptb ≤ R; pmax ≤ 1,2R; pmin ≥ 0", "", "TCVN 9362:2012"));
        s.Items.Add(I("Quy đổi nội lực móng đơn", "kt", "Ntc = Ntt/kt", "kt = " + F(sd.LoadFactorAvg), "", "", ""));
        s.Items.Add(I("Độ lún giới hạn", "Sgh", "S ≤ Sgh", "Sgh = 8 cm (nhà khung BTCT); hệ số β = 0,8", "", "", "TCVN 9362:2012 Bảng 16"));
        s.Items.Add(I("Cọc", "FS", "[P]đn = Rc,u/FS", "FS = 2,5; Pvl với φ = 1; n ≥ 1,2·N/[P]; trọng lượng đài × 1,1", "FS = 2,5", "", "TCVN 10304:2014"));
        s.Items.Add(I("Giới hạn độ võng / chuyển vị", "[f], [Δ]", "", $"Dầm L/{F(cd.BeamDeflectionLimit)} (công xôn 2L/{F(cd.BeamDeflectionLimit)}); sàn lx/200; kèo L/{F(st.RafterDeflection)}; xà gồ L/200; đỉnh cột thép H/{F(st.ColumnDrift)}", "", "", "TCVN 5574:2018; TCVN 5575"));
        s.Items.Add(I("Độ mảnh giới hạn", "λ", "", "Cột BTCT l0/i ≤ 120; thép: [λ] = 180 − 60α (chính), 210 − 60α (phụ), 300 (chịu kéo)", "", "", "TCVN 5574:2018; TCVN 5575:2012"));
        s.Items.Add(I("Hệ số sử dụng", "Demand/Capacity", "Tỷ số = nội lực / khả năng chịu lực", "≤ 1,0: đạt", "",
            "Bảng kiểm tra trong thuyết minh ghi tỷ số này. Nên giữ ≤ 0,9 để dự phòng sai số mô hình và thi công.", ""));
        return s;
    }

    // ------------------------------------------------------------------
    private static AppendixSection Pl18Workflow(Project p)
    {
        var s = new AppendixSection
        {
            Title = "Quy trình sử dụng cho người mới và lỗi thường gặp",
            Intro = "Làm theo thứ tự dưới đây. Sau mỗi bước hãy xem thanh trạng thái và các cảnh báo; bước sau phụ thuộc kết quả bước trước."
        };
        s.Items.Add(I("Bước 1 – Thông tin, địa điểm", "", "", "Tên công trình, địa điểm; chọn vùng gió (W0), dạng địa hình; agR và loại nền nếu tính động đất", "", "Tra W0 theo QCVN 02:2022/BXD, agR theo TCVN 9386:2012 Phụ lục H cho đúng xã/phường.", ""));
        s.Items.Add(I("Bước 2 – Lưới trục, tầng", "", "", "Khoảng cách trục X/Y; số tầng và chiều cao tầng; hoặc nhập bản vẽ kiến trúc / DXF / Revit", "", "Kiểm tra cao độ chân cột (mặt móng) – ảnh hưởng chiều cao H khi tính gió, động đất.", ""));
        s.Items.Add(I("Bước 3 – Mô hình", "", "", "Tạo khung BTCT từ lưới trục; chỉnh tiết diện, thêm/xoá dầm, ô sàn, lỗ cầu thang trên mặt bằng", "", "", ""));
        s.Items.Add(I("Bước 4 – Tải trọng", "", "", "Khai báo loại tường và vẽ tường; gán cấu tạo sàn cho ô sàn (WC, ban công, mái…); kiểm tra γf các trường hợp tải", "", "", ""));
        s.Items.Add(I("Bước 5 – Tổ hợp và phân tích", "", "", "Phân tích → Tạo tổ hợp tải → Chạy phân tích (F5)", "", "Xem biểu đồ mô men, phản lực; tổng phản lực đứng phải xấp xỉ tổng tải.", ""));
        s.Items.Add(I("Bước 6 – Thiết kế BTCT / thép", "", "", "Thiết kế BTCT; Kiểm tra thép; nếu còn cấu kiện không đạt → Tự tăng tiết diện", "", "", ""));
        s.Items.Add(I("Bước 7 – Địa chất và móng", "", "", "Nhập hố khoan (chỉ tiêu từng lớp, SPT, mực nước); chọn loại móng (đơn/băng/cọc) theo gợi ý; Thiết kế móng", "", "Nếu chưa có khảo sát, kết quả móng chỉ là sơ bộ.", ""));
        s.Items.Add(I("Bước 8 – Bản vẽ và thuyết minh", "", "", "Xuất DXF, dàn trang in; xuất thuyết minh Word, bảng tính Excel (kèm phụ lục này)", "", "", ""));
        s.Items.Add(I("Bước 9 – Kiểm tra chéo", "", "", "Xuất .e2k (ETABS) / .s2k (SAP2000), chạy lại và so sánh nội lực, chuyển vị, phản lực", "", "Tắt Property Modifiers và End Offsets ở ETABS hoặc khai báo giống nhau; chênh lệch nội lực > 10% cần tìm nguyên nhân.", ""));
        s.Items.Add(I("Lỗi thường gặp: nhầm đơn vị", "", "", "W0 nhập 95 (daN/m²) thay vì 0,95 kN/m²; tải tường nhập kN/m² thay vì kN/m; kích thước m thay vì mm", "", "Đọc kỹ đơn vị ở tiêu đề ô.", ""));
        s.Items.Add(I("Lỗi thường gặp: quên tạo lại tổ hợp", "", "", "Thêm trường hợp tải (vd. động đất) mà không tạo lại tổ hợp", "", "Tổ hợp cũ không chứa trường hợp mới → thiết kế bỏ sót tải.", ""));
        s.Items.Add(I("Lỗi thường gặp: γf hoạt tải", "", "", "Giữ γf = 1,2 cho hoạt tải nhỏ (1,5 kN/m²)", "", "Kiểm tra lại theo TCVN 2737 (xem PL3).", ""));
        s.Items.Add(I("Lỗi thường gặp: thông số nền mặc định", "", "", "Dùng φ, c, γ mặc định hoặc nền giả định khi chưa có khảo sát", "", "Móng có thể không an toàn hoặc lãng phí – phải cập nhật theo báo cáo địa chất.", ""));
        s.Items.Add(I("Lỗi thường gặp: kết cấu biến hình", "", "", "Thiếu gối chân cột, dầm treo không nối nút, ô sàn không có dầm đỡ", "", "Đọc thông báo lỗi (số phương trình suy biến) và cảnh báo dồn tải sàn.", ""));
        s.Items.Add(I("Lỗi thường gặp: tin hoàn toàn vào chương trình", "", "", "", "",
            "Chương trình là công cụ hỗ trợ có các giả thiết đơn giản hoá (đã ghi rõ trong phụ lục). Kỹ sư chịu trách nhiệm kiểm tra kết quả, cấu tạo và hồ sơ.", ""));
        return s;
    }
}

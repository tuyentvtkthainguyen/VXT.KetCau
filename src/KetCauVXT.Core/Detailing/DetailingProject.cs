using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using KetCauVXT.Design;
using KetCauVXT.Drawing;
using KetCauVXT.Report;

namespace KetCauVXT.Detailing;

/// <summary>Hồ sơ của chế độ "Triển khai hồ sơ" (file *.kcvxth, JSON): thông tin công trình, vật liệu và danh sách cấu kiện nhập tay.</summary>
public class DetailingProject
{
    public const string Extension = ".kcvxth";
    public const string FileFilter = "Hồ sơ triển khai Việt Const (*.kcvxth)|*.kcvxth";

    public int Version { get; set; } = 1;
    public string ProjectName { get; set; } = "Công trình mới";
    public string Owner { get; set; } = "";
    public string Location { get; set; } = "";
    public string Designer { get; set; } = "";
    public DetailSettings Settings { get; set; } = new();

    public List<ManualBeam> Beams { get; set; } = new();
    public List<ManualColumn> Columns { get; set; } = new();
    public List<ManualSlab> Slabs { get; set; } = new();
    public List<ManualSlabPlan> SlabPlans { get; set; } = new();
    public List<ManualLintel> Lintels { get; set; } = new();
    public List<ManualFooting> Footings { get; set; } = new();
    public List<ManualStrip> Strips { get; set; } = new();
    public List<ManualPileCap> PileCaps { get; set; } = new();
    public List<RaftInput> Rafts { get; set; } = new();
    public List<PrestressInput> Prestress { get; set; } = new();
    public List<WaterTankInput> Tanks { get; set; } = new();
    public List<ManualStair> Stairs { get; set; } = new();
    public List<ManualLiftPit> LiftPits { get; set; } = new();

    /// <summary>Mặt bằng kết cấu: lưới trục, các mặt bằng, vị trí cấu kiện (Detailing/StructuralPlan.cs).</summary>
    public StructuralPlan Plan { get; set; } = new();

    /// <summary>Dàn trang in (Detailing/SheetLayout.cs); null = chưa dàn trang (tự dàn khi mở cửa sổ Dàn trang in).</summary>
    public DetailSheetSet Sheets { get; set; }

    /// <summary>Các nhóm cấu kiện theo thứ tự hiển thị: (khoá, tên nhóm, danh sách).</summary>
    [JsonIgnore]
    public IEnumerable<(string Key, string Title, System.Collections.IList List)> Groups => new (string, string, System.Collections.IList)[]
    {
        ("Dam", "Dầm", Beams), ("Cot", "Cột", Columns), ("San", "Sàn (ô bản)", Slabs), ("MatBangSan", "Mặt bằng sàn", SlabPlans), ("LanhTo", "Lanh tô – ô văng", Lintels), ("CauThang", "Cầu thang bộ", Stairs), ("HoPit", "Hố pít – vách thang máy", LiftPits),
        ("MongDon", "Móng đơn", Footings), ("MongBang", "Móng băng", Strips), ("MongCoc", "Móng cọc (đài cọc)", PileCaps),
        ("MongBe", "Móng bè", Rafts), ("DuUngLuc", "Dự ứng lực", Prestress), ("BeNuoc", "Bể nước", Tanks)
    };

    [JsonIgnore]
    public IEnumerable<IDetailMember> All => Groups.SelectMany(g => g.List.Cast<IDetailMember>());

    /// <summary>Tạo cấu kiện mới mặc định cho nhóm (khoá trong <see cref="Groups"/>), tên đánh số tiếp theo.</summary>
    public IDetailMember AddNew(string key)
    {
        var g = Groups.First(x => x.Key == key);
        IDetailMember m = key switch
        {
            "Dam" => new ManualBeam(), "Cot" => new ManualColumn(), "San" => new ManualSlab(), "MatBangSan" => ManualSlabPlan.Sample(), "LanhTo" => ManualLintel.Sample(), "CauThang" => ManualStair.Sample(), "HoPit" => ManualLiftPit.Sample(),
            "MongDon" => new ManualFooting(), "MongBang" => new ManualStrip(), "MongCoc" => new ManualPileCap(),
            "MongBe" => RaftInput.Sample(), "DuUngLuc" => PrestressSample(), _ => new WaterTankInput()
        };
        string prefix = new string(m.Name.TakeWhile(ch => !char.IsDigit(ch)).ToArray());
        int k = 1;
        var names = new HashSet<string>(All.Select(x => x.Name));
        while (names.Contains(prefix + k)) k++;
        m.Name = prefix + k;
        g.List.Add(m);
        return m;
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);
    public static DetailingProject FromJson(string json)
    {
        var p = JsonSerializer.Deserialize<DetailingProject>(json, Json) ?? new DetailingProject();
        p.Settings ??= new DetailSettings();
        p.Beams ??= new(); p.Columns ??= new(); p.Slabs ??= new(); p.SlabPlans ??= new(); p.Lintels ??= new(); p.Footings ??= new(); p.Strips ??= new();
        p.PileCaps ??= new(); p.Rafts ??= new(); p.Prestress ??= new(); p.Tanks ??= new(); p.Stairs ??= new(); p.LiftPits ??= new();
        p.Plan ??= new StructuralPlan();
        p.Plan.Levels ??= new();
        foreach (var l in p.Plan.Levels) l.Items ??= new();
        p.Plan.Sections ??= new();
        if (p.Sheets != null)
        {
            p.Sheets.Sheets ??= new();
            p.Sheets.Sheets.RemoveAll(x => x == null);
            foreach (var sh in p.Sheets.Sheets) { sh.Figures ??= new(); sh.Figures.RemoveAll(f => f == null || string.IsNullOrEmpty(f.Id)); }
        }
        return p;
    }
    public void Save(string path) => File.WriteAllText(path, ToJson(), new System.Text.UTF8Encoding(false));
    public static DetailingProject Load(string path) => FromJson(File.ReadAllText(path));

    /// <summary>Tính mọi cấu kiện (lỗi ngoại lệ ghi thành "LỖI …", không dừng).</summary>
    public List<(IDetailMember Member, DetailResult Result)> RunAll()
    {
        var res = new List<(IDetailMember, DetailResult)>();
        foreach (var m in All) res.Add((m, RunSafe(m, Settings)));
        return res;
    }

    public static DetailResult RunSafe(IDetailMember m, DetailSettings s)
    {
        try { return m.Run(s) ?? new DetailResult { Name = m.Name, Messages = { "LỖI không có kết quả" } }; }
        catch (Exception e) { return new DetailResult { Name = m.Name, Quantity = m.Quantity, Messages = { "LỖI tính toán: " + e.Message } }; }
    }

    /// <summary>Dầm DƯL căng sau mẫu nhịp 12 m: 400×900, B40, 12 tao T15.2 (3 bó × 4 tao), e = 220 mm – đạt mọi kiểm tra với G2 = 12, Q = 10 kN/m.</summary>
    public static PrestressInput PrestressSample() => new()
    {
        B = 0.40, H = 0.90, L = 12.0, ConcreteGrade = "B40", TransferRatio = 0.8, NumStrands = 12, StrandsPerTendon = 4, EcMid = 0.22,
        StirrupDia = 8, StirrupSpacing = 0.15
    };

    /// <summary>Hồ sơ mẫu: 1 dầm, 1 cột, 1 sàn và mỗi loại móng / DƯL / bể nước một cấu kiện.</summary>
    public static DetailingProject Sample()
    {
        var p = new DetailingProject { ProjectName = "Nhà ở mẫu – triển khai hồ sơ", Owner = "Chủ đầu tư", Location = "Thái Nguyên" };
        p.Beams.Add(new ManualBeam { MSpan = 45, MSupport = 60, Q = 70 });
        p.Columns.Add(new ManualColumn { N = 650, Mx = 25, My = 12 });
        p.Slabs.Add(new ManualSlab());
        p.SlabPlans.Add(ManualSlabPlan.Sample());
        p.Lintels.Add(ManualLintel.Sample());
        p.Footings.Add(new ManualFooting());
        p.Strips.Add(new ManualStrip());
        p.PileCaps.Add(new ManualPileCap());
        p.Rafts.Add(RaftInput.Sample());
        p.Prestress.Add(PrestressSample());
        p.Tanks.Add(new WaterTankInput());
        p.Stairs.Add(ManualStair.Sample());
        p.LiftPits.Add(ManualLiftPit.Sample());
        p.Plan = StructuralPlan.Sample();   // mặt bằng: móng M1 + cột C1 ở 6 giao trục, dầm D1, sàn S1, lanh tô LT1, cầu thang CT1
        p.Plan.SyncQuantities(p);           // SL cấu kiện = số vị trí trên mặt bằng
        return p;
    }
}

/// <summary>Xuất hồ sơ chế độ triển khai: bản vẽ DXF (gộp / tách), bảng khối lượng Excel, thuyết minh Word.</summary>
public static class DetailingExport
{
    public static DesignSummary Summary(IEnumerable<(IDetailMember Member, DetailResult Result)> results)
    {
        var s = new DesignSummary();
        foreach (var (_, r) in results)
        {
            if (r == null || r.Bars.Count == 0) continue;
            switch (r.Source)
            {
                case BeamLineDesign b: s.Beams.Add(b); break;
                case ColumnDesign c: s.Columns.Add(c); break;
                case FootingDesign f: s.Footings.Add(f); break;
                default: s.Others.Add((r, string.IsNullOrEmpty(r.Kind) ? "Khác" : r.Kind)); break;
            }
        }
        return s;
    }

    private static List<Figure> FiguresOf(IDetailMember m, DetailResult r, DetailSettings s, List<string> log)
    {
        try { return m.Draw(r, s) ?? new List<Figure>(); }
        catch (Exception e) { log.Add("Không vẽ được " + m.Name + ": " + e.Message); return new List<Figure>(); }
    }

    private static string Safe(string name) => string.Concat((name ?? "BanVe").Select(ch => Path.GetInvalidFileNameChars().Contains(ch) || ch == ' ' ? '_' : ch));

    /// <summary>Định dạng xuất mặc định của chế độ triển khai: DWG AutoCAD 2018 (khi chưa có bộ ghi DWG – vd. trong kiểm thử – tự về DXF R12).</summary>
    public static KetCauVXT.Model.CadOutputFormat DefaultFormat { get; set; } = KetCauVXT.Model.CadOutputFormat.Dwg2018;

    private static string Write(DxfWriter w, string pathNoExt, KetCauVXT.Model.CadOutputFormat fmt) => CadFileWriters.Write(w, pathNoExt + ".dxf", fmt);

    /// <summary>Xuất bản vẽ: MẶT BẰNG KẾT CẤU (móng, định vị cột, các tầng) → chi tiết từng cấu kiện → thống kê cốt thép → bảng thống kê vật liệu.
    /// split = true: mỗi mục một file (KC-01, KC-02…); false: một file gộp. format: DWG 2018 (mặc định) / DXF 2018 / DXF R12.
    /// Trả về danh sách file đã ghi; log ghi cảnh báo.</summary>
    public static List<string> Drawings(DetailingProject p, List<(IDetailMember Member, DetailResult Result)> results, string folder, bool split, List<string> log,
        KetCauVXT.Model.CadOutputFormat? format = null)
    {
        var fmt = format ?? DefaultFormat;
        Directory.CreateDirectory(folder);
        var files = new List<string>();
        var info = new SheetComposer.SheetInfo { ProjectName = p.ProjectName, Owner = p.Owner, Location = p.Location, Designer = p.Designer };
        var all = new List<Figure>();
        int no = 1;
        void Sheet(List<Figure> figs, string title, string file)
        {
            if (figs.Count == 0) return;
            if (split)
            {
                info.Title = title; info.SheetNo = "KC-" + no.ToString("00");
                var (sc, paper) = SheetFor(figs);
                var w = SheetComposer.Compose(figs, sc, info, paper);
                files.Add(Write(w, Path.Combine(folder, info.SheetNo + "_" + Safe(file)), fmt));
                no++;
            }
            else all.AddRange(figs);
        }
        // 1. mặt bằng kết cấu
        List<Figure> plans;
        try { plans = StructuralPlanDrawing.Build(p, results); }
        catch (Exception e) { log.Add("Không vẽ được mặt bằng kết cấu: " + e.Message); plans = new List<Figure>(); }
        foreach (var f in plans)
        {
            string key = f.Name == StructuralPlanDrawing.TitleFoundation ? "MB_KetCauMong" : f.Name == StructuralPlanDrawing.TitleColumns ? "MB_DinhViCot" : "MB_" + f.Name.Replace(StructuralPlanDrawing.TitleLevelPrefix, "").Split('(')[0].Trim();
            Sheet(new List<Figure> { f }, f.Name, key);
        }
        // 2. chi tiết cấu kiện; lanh tô – ô văng đặt trên mặt bằng: gộp theo tầng (mặt bằng lanh tô tầng + chi tiết) – mục lanh tô cửa các tầng
        List<(PlanLevel Level, Figure Plan, List<IDetailMember> Members)> lsh;
        try { lsh = LintelPlanDrawing.Sheets(p, results); }
        catch (Exception e) { log.Add("Không lập được mặt bằng lanh tô: " + e.Message); lsh = new(); }
        var lintelDone = new HashSet<IDetailMember>();
        bool lintelSheets = false;
        void LintelSheets()
        {
            // mục LANH TÔ – Ô VĂNG CÁC TẦNG: mỗi tầng một tờ (mặt bằng lanh tô tầng + chi tiết lanh tô của tầng), theo cao độ
            lintelSheets = true;
            foreach (var ls in lsh)
            {
                var lf = new List<Figure>();
                if (ls.Plan != null) lf.Add(ls.Plan);
                foreach (var lm in ls.Members)
                {
                    lintelDone.Add(lm);
                    var lr = results.FirstOrDefault(x => ReferenceEquals(x.Member, lm)).Result;
                    if (lr != null) lf.AddRange(FiguresOf(lm, lr, p.Settings, log));
                }
                Sheet(lf, "LANH TÔ – Ô VĂNG " + (ls.Level?.Name ?? "").ToUpperInvariant(), "LanhTo_" + (ls.Level?.Name ?? ""));
            }
        }
        foreach (var (m, r) in results)
        {
            if (lintelDone.Contains(m)) continue;
            if (!lintelSheets && lsh.Any(x => x.Members.Contains(m))) { LintelSheets(); continue; }
            var figs = FiguresOf(m, r, p.Settings, log);
            Sheet(figs, (r.Kind + " " + m.Name).ToUpperInvariant(), r.Kind + "_" + m.Name);
        }
        if (!lintelSheets && lsh.Count > 0) LintelSheets();
        // 3. thống kê cốt thép
        var sch = RebarSchedule.BuildFigures(Summary(results), 1, 30);
        if (split)
        {
            info.Title = "THỐNG KÊ CỐT THÉP"; info.SheetNo = "KC-" + no.ToString("00");
            var w = SheetComposer.Compose(sch, 100, info, "A1");
            files.Add(Write(w, Path.Combine(folder, info.SheetNo + "_ThongKeThep"), fmt));
            no++;
        }
        else all.AddRange(sch);
        // 4. bảng thống kê vật liệu (thép, bê tông, ván khuôn)
        List<Figure> mat;
        try { mat = MaterialTable.Build(p, results) ?? new List<Figure>(); }
        catch (Exception e) { log.Add("Không lập được bảng thống kê vật liệu: " + e.Message); mat = new List<Figure>(); }
        Sheet(mat, "THỐNG KÊ VẬT LIỆU", "ThongKeVatLieu");
        if (!split)
        {
            info.Title = "HỒ SƠ TRIỂN KHAI KẾT CẤU"; info.SheetNo = "KC-01";
            var w = SheetComposer.Compose(all, SheetFor(all).Scale, info, "A1");
            files.Add(Write(w, Path.Combine(folder, Safe(p.ProjectName) + "_BoBanVe"), fmt));
        }
        return files;
    }

    /// <summary>Tỷ lệ tờ = tỷ lệ thô nhất của các hình (hình chi tiết hơn được phóng đúng tỷ lệ riêng khi ghép);
    /// khổ giấy nhỏ nhất (A3 → A2 → A1) chứa được các hình sau khi xếp (ước lượng theo diện tích, hệ số lấp đầy 0,45).</summary>
    public static (double Scale, string Paper) SheetFor(IList<Figure> figs)
    {
        var list = figs.Where(f => f?.W != null && f.W.EntityCount > 0).ToList();
        if (list.Count == 0) return (50, "A3");
        double sc = list.Max(f => f.W.Scale > 0 ? f.W.Scale : 50);
        double area = 0, maxW = 0, maxH = 0;
        foreach (var f in list)
        {
            double k = sc / (f.W.Scale > 0 ? f.W.Scale : sc);
            var e = f.W.Extents;
            double w = e.W * k / sc, h = e.H * k / sc;   // mm giấy
            area += (w + 12) * (h + 12); maxW = Math.Max(maxW, w); maxH = Math.Max(maxH, h);
        }
        foreach (var p in new[] { "A3", "A2", "A1" })
        {
            var (pw, ph) = SheetComposer.Paper(p);
            double uw = pw - 25 - 10 - 24, uh = ph - 20 - 24 - 45 * 0.5;
            if (maxW <= uw && maxH <= uh && area <= 0.45 * uw * uh) return (sc, p);
        }
        return (sc, "A1");
    }

    /// <summary>Dòng tổng hợp khối lượng: (nhóm, tên công tác, đơn vị, khối lượng).</summary>
    public static List<(string Group, string Item, string Unit, double Qty)> Quantities(DetailingProject p, List<(IDetailMember Member, DetailResult Result)> results)
    {
        var rows = new List<(string, string, string, double)>();
        foreach (var grp in results.Where(x => x.Result != null).GroupBy(x => string.IsNullOrEmpty(x.Result.Kind) ? "Khác" : x.Result.Kind))
        {
            double v = grp.Sum(x => x.Result.ConcreteVolume * x.Result.Quantity);
            double fw = grp.Sum(x => x.Result.FormworkArea * x.Result.Quantity);
            double lean = grp.Sum(x => x.Result.LeanVolume * x.Result.Quantity);
            string cls = grp.Select(x => x.Result.ConcreteClass).FirstOrDefault(c => !string.IsNullOrEmpty(c)) ?? p.Settings.Concrete;
            if (v > 0) rows.Add((grp.Key, "Bê tông " + cls + " " + grp.Key.ToLowerInvariant(), "m³", v));
            if (lean > 0) rows.Add((grp.Key, "Bê tông lót " + p.Settings.LeanConcrete + " dày " + Dt.Mm(p.Settings.LeanThickness) + " mm", "m³", lean));
            if (fw > 0) rows.Add((grp.Key, "Ván khuôn " + grp.Key.ToLowerInvariant(), "m²", fw));
            foreach (var (lo, hi, label) in new[] { (0, 10, "Ø ≤ 10"), (11, 18, "10 < Ø ≤ 18"), (19, 99, "Ø > 18") })
            {
                double kg = grp.Sum(x => x.Result.Bars.Where(b => b.Dia >= lo && b.Dia <= hi).Sum(b => b.WeightPerBar * b.Count) * x.Result.Quantity);
                if (kg > 0) rows.Add((grp.Key, "Cốt thép " + grp.Key.ToLowerInvariant() + " " + label, "kg", kg));
            }
            double strand = grp.Sum(x => x.Result.StrandWeight * x.Result.Quantity);
            if (strand > 0) rows.Add((grp.Key, "Cáp dự ứng lực", "kg", strand));
            foreach (var ex in grp.SelectMany(x => x.Result.Extra.Select(e => (e.Item, e.Unit, Qty: e.Qty * x.Result.Quantity))).GroupBy(e => (e.Item, e.Unit)))
                rows.Add((grp.Key, ex.Key.Item, ex.Key.Unit, ex.Sum(e => e.Qty)));
        }
        return rows;
    }

    public static void Excel(DetailingProject p, List<(IDetailMember Member, DetailResult Result)> results, string path)
    {
        var x = new XlsxWriter();
        var sh = x.AddSheet("Tổng hợp khối lượng");
        sh.AddBoldRow("BẢNG TỔNG HỢP KHỐI LƯỢNG – " + p.ProjectName);
        sh.AddRow("Bê tông " + p.Settings.Concrete + ", thép " + p.Settings.RebarMain + " / " + p.Settings.RebarSmall);
        sh.AddEmptyRow();
        sh.AddHeaderRow("STT", "Nhóm cấu kiện", "Tên công tác", "Đơn vị", "Khối lượng");
        int i = 1;
        foreach (var (g, item, unit, q) in Quantities(p, results)) sh.AddRow(i++, g, item, unit, Math.Round(q, unit == "kg" ? 1 : 3));
        sh.SetColumnWidths(6, 18, 52, 8, 14);

        var sm = x.AddSheet("Theo cấu kiện");
        sm.AddHeaderRow("Cấu kiện", "Loại", "SL", "Bê tông 1 CK (m³)", "Ván khuôn 1 CK (m²)", "Thép 1 CK (kg)", "Bê tông toàn bộ (m³)", "Thép toàn bộ (kg)", "Hàm lượng (kg/m³)", "Kết quả kiểm tra");
        foreach (var (m, r) in results)
        {
            if (r == null) continue;
            double kg = r.SteelWeight;
            sm.AddRow(m.Name, r.Kind, r.Quantity, Math.Round(r.ConcreteVolume, 3), Math.Round(r.FormworkArea, 2), Math.Round(kg, 1),
                Math.Round(r.ConcreteVolume * r.Quantity, 3), Math.Round(kg * r.Quantity, 1), r.ConcreteVolume > 0 ? Math.Round(kg / r.ConcreteVolume, 0) : 0, r.Status);
        }
        sm.SetColumnWidths(12, 14, 6, 14, 14, 12, 14, 14, 12, 14);

        var st = x.AddSheet("Thống kê cốt thép");
        st.AddHeaderRow("Cấu kiện", "Nhóm", "Số hiệu", "Vai trò", "Hình dạng (mm)", "Ø (mm)", "Dài 1 thanh (mm)", "SL CK", "Thanh/1 CK", "Thanh toàn bộ", "Tổng dài (m)", "Tổng KL (kg)");
        var rows = RebarSchedule.BuildRows(Summary(results));
        foreach (var r in rows)
            st.AddRow(RebarSchedule.MemberLabel(r.Member, r.MemberCount), r.Category, r.Mark, r.Role, r.ShapeText, r.Dia, r.LengthMm, r.MemberCount, r.BarsPerMember, r.BarsTotal, r.TotalLengthM, r.WeightKg);
        st.AddEmptyRow();
        st.AddBoldRow("TỔNG HỢP THEO ĐƯỜNG KÍNH");
        st.AddHeaderRow("Ø (mm)", "Tổng dài (m)", "Trọng lượng (kg)");
        foreach (var g in rows.GroupBy(r => r.Dia).OrderBy(g => g.Key))
            st.AddRow(g.Key, Math.Round(g.Sum(r => r.TotalLengthM), 2), Math.Round(g.Sum(r => r.WeightKg), 1));
        st.AddBoldRow("Tổng cộng", "", Math.Round(rows.Sum(r => r.WeightKg), 1));
        st.SetColumnWidths(16, 12, 8, 22, 26, 7, 12, 7, 9, 11, 11, 11);
        ShopDrawing.AddExcelSheet(x, p, results);   // gia công – cắt thép dầm, sàn (Detailing/ShopDrawing.cs)
        x.Save(path);
    }

    /// <summary>Thuyết minh tính toán Word: căn cứ, vật liệu, từng cấu kiện (công thức, thay số, bảng kiểm tra, thông báo).</summary>
    public static void Word(DetailingProject p, List<(IDetailMember Member, DetailResult Result)> results, string path)
    {
        var w = new DocxWriter();
        w.Title("THUYẾT MINH TÍNH TOÁN KẾT CẤU");
        w.Paragraph("Công trình: " + p.ProjectName, true, false, ParaAlign.Center);
        if (!string.IsNullOrEmpty(p.Location)) w.Paragraph("Địa điểm: " + p.Location, false, false, ParaAlign.Center);
        if (!string.IsNullOrEmpty(p.Owner)) w.Paragraph("Chủ đầu tư: " + p.Owner, false, false, ParaAlign.Center);
        w.Heading("1. Căn cứ và tiêu chuẩn áp dụng");
        foreach (var t in new[]
        {
            "Luật Xây dựng số 135/2025/QH15; Nghị định 217/2026/NĐ-CP về quản lý dự án đầu tư xây dựng; Thông tư 34/2026/TT-BXD phân cấp công trình.",
            "TCVN 2737:2023 Tải trọng và tác động; TCVN 5574:2018 Kết cấu bê tông và bê tông cốt thép (cả bê tông ứng suất trước – mục 9).",
            "TCVN 9362:2012 Nền nhà và công trình; TCVN 10304:2014 Móng cọc; TCVN 1651:2018 Thép cốt bê tông.",
            "Hồ sơ triển khai: kích thước, bố trí thép và nội lực thiết kế do kỹ sư nhập (không phân tích mô hình tổng thể) – nội lực lấy từ tính toán khung / phần mềm khác."
        }) w.Bullet(t);
        w.Heading("2. Vật liệu");
        w.Table(new[] { "Vật liệu", "Ký hiệu", "Cường độ tính toán" }, new[]
        {
            new[] { "Bê tông", p.Settings.Concrete, "Rb = " + Dt.F(p.Settings.Conc().Rb / 1000, "0.0") + " MPa; Rbt = " + Dt.F(p.Settings.Conc().Rbt / 1000, "0.00") + " MPa" },
            new[] { "Thép dọc", p.Settings.RebarMain, "Rs = " + Dt.F(p.Settings.Main().Rs / 1000, "0") + " MPa" },
            new[] { "Thép đai, thép sàn Ø ≤ 10", p.Settings.RebarSmall, "Rs = " + Dt.F(p.Settings.Small().Rs / 1000, "0") + " MPa; Rsw = " + Dt.F(p.Settings.Small().Rsw / 1000, "0") + " MPa" },
            new[] { "Lớp bảo vệ", "dầm / cột / sàn / móng", Dt.Mm(p.Settings.CoverBeam) + " / " + Dt.Mm(p.Settings.CoverColumn) + " / " + Dt.Mm(p.Settings.CoverSlab) + " / " + Dt.Mm(p.Settings.CoverFooting) + " mm" }
        });
        int sec = 3;
        foreach (var grp in results.Where(x => x.Result != null).GroupBy(x => string.IsNullOrEmpty(x.Result.Kind) ? "Khác" : x.Result.Kind))
        {
            w.Heading(sec++ + ". " + grp.Key.ToUpperInvariant());
            foreach (var (m, r) in grp)
            {
                w.Heading(m.Name + (r.Quantity > 1 ? " (SL = " + r.Quantity + ")" : "") + " – " + r.Status, 2);
                foreach (var line in r.Calc)
                {
                    if (line.StartsWith("## ")) w.Paragraph(line[3..], true);
                    else w.Paragraph(line);
                }
                if (r.Checks.Count > 0)
                    w.Table(new[] { "Kiểm tra", "Yêu cầu", "Khả năng", "Đơn vị", "Tỷ số", "Kết luận" },
                        r.Checks.Select(c => new[] { c.Name, Dt.F(c.Demand, "0.0##"), Dt.F(c.Capacity, "0.0##"), c.Unit, Dt.F(c.Ratio, "0.00"), c.Ok ? "Đạt" : "Không đạt" }),
                        new[] { 6.5, 2.2, 2.2, 1.6, 1.5, 2.0 }, 10);
                foreach (var msg in r.Messages) w.Bullet(msg);
            }
        }
        w.Heading(sec + ". Tổng hợp khối lượng");
        w.Table(new[] { "Nhóm", "Công tác", "Đơn vị", "Khối lượng" },
            Quantities(p, results).Select(q => new[] { q.Group, q.Item, q.Unit, Dt.F(q.Qty, q.Unit == "kg" ? "0.0" : "0.000") }), new[] { 3.0, 9.0, 1.8, 2.5 }, 10);
        w.Save(path);
    }
}

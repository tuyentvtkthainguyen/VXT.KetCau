using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Detailing;

// =====================================================================
// DẦM – CỘT – SÀN nhập tay: người dùng khai báo kích thước, chọn thép, (tuỳ chọn) nội lực thiết kế để kiểm tra.
// Kết quả dựng thành BeamLineDesign / ColumnDesign để dùng lại bản vẽ chi tiết (BeamDrawing, ColumnDrawing)
// và bảng thống kê thép như ở chế độ mô hình.
// =====================================================================

/// <summary>Dự án giả lập tối thiểu cho các bộ vẽ có sẵn (chỉ dùng lớp bảo vệ, vật liệu).</summary>
public static class DetailShim
{
    public static Project Project(DetailSettings s)
    {
        var p = new Project();
        p.Concrete.ConcreteBeam = p.Concrete.ConcreteColumn = p.Concrete.ConcreteSlab = p.Concrete.ConcreteFooting = s.Concrete;
        p.Concrete.RebarMain = s.RebarMain;
        p.Concrete.RebarStirrup = s.RebarSmall;
        p.Concrete.CoverBeam = s.CoverBeam;
        p.Concrete.CoverColumn = s.CoverColumn;
        p.Concrete.CoverSlab = s.CoverSlab;
        p.Concrete.CoverFooting = s.CoverFooting;
        try
        {
            p.Materials.Add(Material.Concrete(s.Concrete));
            p.Materials.Add(Material.Rebar(s.RebarMain));
            if (s.RebarSmall != s.RebarMain) p.Materials.Add(Material.Rebar(s.RebarSmall));
        }
        catch (ArgumentException) { }
        return p;
    }

    /// <summary>Danh sách số (m) phân cách ';' hoặc khoảng trắng, dấu phẩy thập phân được chấp nhận: "4,2; 3,6" → [4.2, 3.6].</summary>
    public static List<double> ParseList(string text)
    {
        var res = new List<double>();
        if (string.IsNullOrWhiteSpace(text)) return res;
        string[] parts = text.Contains(';') ? text.Split(';') : text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in parts)
        {
            var t = raw.Trim().Replace(',', '.');
            if (t.Length == 0) continue;
            int mult = 1;
            int star = t.IndexOf('*');            // "3*4.2" = 3 nhịp 4,2 m
            if (star > 0 && int.TryParse(t[..star], out int m)) { mult = Math.Clamp(m, 1, 50); t = t[(star + 1)..]; }
            if (double.TryParse(t, System.Globalization.NumberStyles.Float, Dt.Inv, out double v) && v > 0)
                for (int i = 0; i < mult; i++) res.Add(v);
        }
        return res;
    }

    public static Material TryConcrete(string cls, List<string> msgs)
    {
        try { return Material.Concrete(cls); }
        catch (ArgumentException e) { msgs.Add("LỖI " + e.Message + " – dùng B25"); return Material.Concrete("B25"); }
    }

    public static Material TryRebar(string grade, List<string> msgs)
    {
        try { return Material.Rebar(grade); }
        catch (ArgumentException e) { msgs.Add("LỖI " + e.Message + " – dùng CB400-V"); return Material.Rebar("CB400-V"); }
    }
}

// ---------------------------------------------------------------------
// DẦM
// ---------------------------------------------------------------------
public class ManualBeam : IDetailMember
{
    [Field("Tên dầm", Group = "Chung")] public string Name { get; set; } = "D1";
    [Field("Số lượng", Group = "Chung")] public int Quantity { get; set; } = 1;
    [Field("Bề rộng b", Unit = "mm", Scale = 1000, Group = "Tiết diện")] public double B { get; set; } = 0.22;
    [Field("Chiều cao h", Unit = "mm", Scale = 1000, Group = "Tiết diện")] public double H { get; set; } = 0.40;
    [Field("Các nhịp tim – tim", Unit = "m", Group = "Tiết diện", Hint = "vd. 4,2; 3,6 hoặc 3*4,2")] public string Spans { get; set; } = "4,2; 3,6";
    [Field("Bề rộng gối (cột)", Unit = "mm", Scale = 1000, Group = "Tiết diện")] public double SupportWidth { get; set; } = 0.22;
    [Field("Cao độ đỉnh dầm", Unit = "m", Group = "Tiết diện")] public double TopElevation { get; set; } = 3.6;
    [Field("Vị trí dầm", Group = "Chung", Choices = "Dầm tầng|Dầm móng (giằng móng)|Dầm mái")] public string Position { get; set; } = "Dầm tầng";

    [Field("Cánh sàn (mặt cắt dầm)", Group = "Sàn hai bên dầm", Choices = "Chữ T – sàn 2 bên|Chữ L – sàn bên trái|Chữ L – sàn bên phải|Không có sàn")] public string Flange { get; set; } = "Chữ T – sàn 2 bên";
    [Field("Chiều dày sàn", Unit = "mm", Scale = 1000, Group = "Sàn hai bên dầm")] public double SlabT { get; set; } = 0.10;
    [Field("Hạ cốt sàn bên trái Δ (≤ 0)", Unit = "mm", Scale = 1000, Group = "Sàn hai bên dầm", Hint = "vd. −50 sàn vệ sinh; mặt cắt vẽ bậc sàn và thép mũ nhấn / neo")] public double DropLeft { get; set; }
    [Field("Hạ cốt sàn bên phải Δ (≤ 0)", Unit = "mm", Scale = 1000, Group = "Sàn hai bên dầm")] public double DropRight { get; set; }
    [Field("Thép sàn lớp dưới (thể hiện)", Group = "Sàn hai bên dầm", Hint = "vd. Ø8a200")] public string SlabBot { get; set; } = "Ø8a200";
    [Field("Thép mũ sàn qua dầm (thể hiện)", Group = "Sàn hai bên dầm")] public string SlabTop { get; set; } = "Ø8a200";

    [Field("Bê tông lót dầm móng – vươn mỗi bên", Unit = "mm", Scale = 1000, Group = "Dầm móng: bê tông lót")] public double LeanOverhang { get; set; } = 0.10;
    [Field("Bên giáp nhà hàng xóm (hết đất)", Group = "Dầm móng: bê tông lót", Choices = "Không giáp nhà bên|Giáp nhà hàng xóm bên trái (hết đất)|Giáp nhà hàng xóm bên phải (hết đất)",
        Hint = "bên giáp nhà hàng xóm: bê tông lót và bê tông dầm không vươn sang đất nhà bên; thành dầm phía đó xây gạch làm khuôn")] public string Boundary { get; set; } = "Không giáp nhà bên";

    // ---- thép dọc: mỗi nhóm một dòng "Ø × số thanh / hàng (× hàng) + tuỳ chọn neo / cắt" (DetailingWindow gộp theo Row) ----
    public const string HookYes = "Bẻ mỏ neo vào gối", HookNo = "Thẳng (neo thẳng)";
    public const string CutC01 = "Cắt tại 0,1·L0 từ mép gối", CutL7 = "Cắt tại L0/7 từ mép gối", CutL8 = "Cắt tại L0/8 từ mép gối", CutAnchor = "Kéo vào gối (neo)";
    public const string ExtStd = "L0/3 gối giữa, L0/4 gối biên", ExtL4 = "L0/4", ExtL5 = "L0/5", ExtShort = "Qua hết nhịp ngắn";
    private const string HookChoices = HookYes + "|" + HookNo, CutChoices = CutC01 + "|" + CutL7 + "|" + CutL8 + "|" + CutAnchor, ExtChoices = ExtStd + "|" + ExtL4 + "|" + ExtL5 + "|" + ExtShort;

    [Field("Thép dưới chạy suốt – Ø", Unit = "mm", Group = "Thép dọc", Row = "bot", RowTitle = "Dưới chạy suốt", Short = "Ø")] public int BotD { get; set; } = 16;
    [Field("Thép dưới chạy suốt – số thanh", Group = "Thép dọc", Row = "bot", Short = "thanh/hàng")] public int BotN { get; set; } = 2;
    [Field("Thép dưới chạy suốt – neo ở gối biên", Group = "Thép dọc", Row = "bot", Short = "neo", Choices = HookChoices)] public string BotHook { get; set; } = HookYes;
    [Field("Thép trên chạy suốt – Ø", Unit = "mm", Group = "Thép dọc", Row = "top", RowTitle = "Trên chạy suốt", Short = "Ø")] public int TopD { get; set; } = 16;
    [Field("Thép trên chạy suốt – số thanh", Group = "Thép dọc", Row = "top", Short = "thanh/hàng")] public int TopN { get; set; } = 2;
    [Field("Thép trên chạy suốt – neo ở gối biên", Group = "Thép dọc", Row = "top", Short = "neo", Choices = HookChoices)] public string TopHook { get; set; } = HookYes;

    [Field("Tăng cường dưới (2) – Ø", Unit = "mm", Group = "Thép tăng cường dưới nhịp", Row = "ab1", RowTitle = "Tăng cường dưới (2)", Short = "Ø")] public int AddBotD { get; set; } = 16;
    [Field("Tăng cường dưới (2) – số thanh", Group = "Thép tăng cường dưới nhịp", Row = "ab1", Short = "thanh", Hint = "0 = không có")] public int AddBotN { get; set; } = 1;
    [Field("Tăng cường dưới (2) – hàng", Group = "Thép tăng cường dưới nhịp", Row = "ab1", Short = "hàng")] public int AddBotRow { get; set; } = 1;
    [Field("Tăng cường dưới (2) – vị trí cắt", Group = "Thép tăng cường dưới nhịp", Row = "ab1b", RowTitle = "   vị trí cắt / neo", Short = "cắt", Choices = CutChoices)] public string AddBotCut { get; set; } = CutC01;
    [Field("Tăng cường dưới (2) – bẻ mỏ", Group = "Thép tăng cường dưới nhịp", Row = "ab1b", Short = "mỏ", Choices = HookChoices)] public string AddBotHook { get; set; } = HookNo;
    [Field("Tăng cường dưới (3) – Ø", Unit = "mm", Group = "Thép tăng cường dưới nhịp", Row = "ab2", RowTitle = "Tăng cường dưới (3)", Short = "Ø")] public int AddBot2D { get; set; } = 16;
    [Field("Tăng cường dưới (3) – số thanh", Group = "Thép tăng cường dưới nhịp", Row = "ab2", Short = "thanh")] public int AddBot2N { get; set; }
    [Field("Tăng cường dưới (3) – hàng", Group = "Thép tăng cường dưới nhịp", Row = "ab2", Short = "hàng")] public int AddBot2Row { get; set; } = 2;
    [Field("Tăng cường dưới (3) – vị trí cắt", Group = "Thép tăng cường dưới nhịp", Row = "ab2b", RowTitle = "   vị trí cắt / neo", Short = "cắt", Choices = CutChoices)] public string AddBot2Cut { get; set; } = CutL7;
    [Field("Tăng cường dưới (3) – bẻ mỏ", Group = "Thép tăng cường dưới nhịp", Row = "ab2b", Short = "mỏ", Choices = HookChoices)] public string AddBot2Hook { get; set; } = HookNo;
    [Field("Tăng cường dưới (4) – Ø", Unit = "mm", Group = "Thép tăng cường dưới nhịp", Row = "ab3", RowTitle = "Tăng cường dưới (4)", Short = "Ø")] public int AddBot3D { get; set; } = 16;
    [Field("Tăng cường dưới (4) – số thanh", Group = "Thép tăng cường dưới nhịp", Row = "ab3", Short = "thanh")] public int AddBot3N { get; set; }
    [Field("Tăng cường dưới (4) – hàng", Group = "Thép tăng cường dưới nhịp", Row = "ab3", Short = "hàng")] public int AddBot3Row { get; set; } = 2;
    [Field("Tăng cường dưới (4) – vị trí cắt", Group = "Thép tăng cường dưới nhịp", Row = "ab3b", RowTitle = "   vị trí cắt / neo", Short = "cắt", Choices = CutChoices)] public string AddBot3Cut { get; set; } = CutL8;
    [Field("Tăng cường dưới (4) – bẻ mỏ", Group = "Thép tăng cường dưới nhịp", Row = "ab3b", Short = "mỏ", Choices = HookChoices)] public string AddBot3Hook { get; set; } = HookNo;

    [Field("Mũ gối (2) – Ø", Unit = "mm", Group = "Thép mũ gối (tăng cường trên)", Row = "s1", RowTitle = "Mũ gối (2)", Short = "Ø")] public int SupD { get; set; } = 16;
    [Field("Mũ gối (2) – số thanh", Group = "Thép mũ gối (tăng cường trên)", Row = "s1", Short = "thanh", Hint = "0 = không có")] public int SupN { get; set; } = 1;
    [Field("Mũ gối (2) – hàng", Group = "Thép mũ gối (tăng cường trên)", Row = "s1", Short = "hàng")] public int SupRow { get; set; } = 1;
    [Field("Mũ gối (2) – chiều dài vươn", Group = "Thép mũ gối (tăng cường trên)", Row = "s1b", RowTitle = "   vươn từ mép gối", Short = "vươn", Choices = ExtChoices)] public string SupExt { get; set; } = ExtStd;
    [Field("Mũ gối (2) – bẻ mỏ ở gối biên", Group = "Thép mũ gối (tăng cường trên)", Row = "s1b", Short = "mỏ", Choices = HookChoices)] public string SupHook { get; set; } = HookYes;
    [Field("Mũ gối (3) – Ø", Unit = "mm", Group = "Thép mũ gối (tăng cường trên)", Row = "s2", RowTitle = "Mũ gối (3)", Short = "Ø")] public int Sup2D { get; set; } = 16;
    [Field("Mũ gối (3) – số thanh", Group = "Thép mũ gối (tăng cường trên)", Row = "s2", Short = "thanh")] public int Sup2N { get; set; }
    [Field("Mũ gối (3) – hàng", Group = "Thép mũ gối (tăng cường trên)", Row = "s2", Short = "hàng")] public int Sup2Row { get; set; } = 2;
    [Field("Mũ gối (3) – chiều dài vươn", Group = "Thép mũ gối (tăng cường trên)", Row = "s2b", RowTitle = "   vươn từ mép gối", Short = "vươn", Choices = ExtChoices)] public string Sup2Ext { get; set; } = ExtL4;
    [Field("Mũ gối (3) – bẻ mỏ ở gối biên", Group = "Thép mũ gối (tăng cường trên)", Row = "s2b", Short = "mỏ", Choices = HookChoices)] public string Sup2Hook { get; set; } = HookYes;
    [Field("Mũ gối (4) – Ø", Unit = "mm", Group = "Thép mũ gối (tăng cường trên)", Row = "s3", RowTitle = "Mũ gối (4)", Short = "Ø")] public int Sup3D { get; set; } = 16;
    [Field("Mũ gối (4) – số thanh", Group = "Thép mũ gối (tăng cường trên)", Row = "s3", Short = "thanh")] public int Sup3N { get; set; }
    [Field("Mũ gối (4) – hàng", Group = "Thép mũ gối (tăng cường trên)", Row = "s3", Short = "hàng")] public int Sup3Row { get; set; } = 2;
    [Field("Mũ gối (4) – chiều dài vươn", Group = "Thép mũ gối (tăng cường trên)", Row = "s3b", RowTitle = "   vươn từ mép gối", Short = "vươn", Choices = ExtChoices)] public string Sup3Ext { get; set; } = ExtL5;
    [Field("Mũ gối (4) – bẻ mỏ ở gối biên", Group = "Thép mũ gối (tăng cường trên)", Row = "s3b", Short = "mỏ", Choices = HookChoices)] public string Sup3Hook { get; set; } = HookYes;

    [Field("Đai – Ø", Unit = "mm", Group = "Thép đai", Row = "st", RowTitle = "Đai", Short = "Ø")] public int StirD { get; set; } = 6;
    [Field("Số nhánh đai", Group = "Thép đai", Row = "st", Short = "nhánh")] public int Legs { get; set; } = 2;
    [Field("Bước đai vùng gối", Unit = "mm", Scale = 1000, Group = "Thép đai", Row = "st2", RowTitle = "Bước đai", Short = "gối")] public double StirSupport { get; set; } = 0.10;
    [Field("Bước đai giữa nhịp", Unit = "mm", Scale = 1000, Group = "Thép đai", Row = "st2", Short = "giữa nhịp")] public double StirMid { get; set; } = 0.20;

    /// <summary>Nhóm thép tăng cường (dưới hoặc mũ gối) đã chuẩn hoá.</summary>
    public sealed record AddGroup(int No, int D, int N, int Layer, string Opt, bool Hook);

    public List<AddGroup> BottomGroups() => new[]
    {
        new AddGroup(2, AddBotD, AddBotN, AddBotRow, AddBotCut, AddBotHook == HookYes),
        new AddGroup(3, AddBot2D, AddBot2N, AddBot2Row, AddBot2Cut, AddBot2Hook == HookYes),
        new AddGroup(4, AddBot3D, AddBot3N, AddBot3Row, AddBot3Cut, AddBot3Hook == HookYes)
    }.Where(g => g.N > 0 && g.D > 0).ToList();

    public List<AddGroup> TopGroups() => new[]
    {
        new AddGroup(2, SupD, SupN, SupRow, SupExt, SupHook != HookNo),
        new AddGroup(3, Sup2D, Sup2N, Sup2Row, Sup2Ext, Sup2Hook != HookNo),
        new AddGroup(4, Sup3D, Sup3N, Sup3Row, Sup3Ext, Sup3Hook != HookNo)
    }.Where(g => g.N > 0 && g.D > 0).ToList();

    /// <summary>Khoảng cắt thép tăng cường dưới tính từ mép gối (m) theo lựa chọn; kéo vào gối → âm (= đoạn neo vào gối).</summary>
    public static double CutDistance(string opt, double l0) => opt switch
    {
        CutL7 => l0 / 7, CutL8 => l0 / 8, CutAnchor => 0, _ => 0.1 * l0
    };

    /// <summary>Chiều dài vươn mũ gối từ mép gối vào nhịp (m) theo lựa chọn: tiêu chuẩn L0/3 (gối giữa) – L0/4 (gối biên).</summary>
    public static double ExtLength(string opt, double l0, bool endSupport) => opt switch
    {
        ExtL4 => l0 / 4, ExtL5 => l0 / 5, _ => endSupport ? l0 / 4 : l0 / 3
    };

    [Field("M dương nhịp lớn nhất (0 = không kiểm tra)", Unit = "kN.m", Group = "Nội lực (tuỳ chọn)")] public double MSpan { get; set; }
    [Field("M âm gối lớn nhất (trị tuyệt đối)", Unit = "kN.m", Group = "Nội lực (tuỳ chọn)")] public double MSupport { get; set; }
    [Field("Q lớn nhất", Unit = "kN", Group = "Nội lực (tuỳ chọn)")] public double Q { get; set; }

    public DetailResult Run(DetailSettings s)
    {
        var msgs = new List<string>();
        var conc = DetailShim.TryConcrete(s.Concrete, msgs);
        var main = DetailShim.TryRebar(s.RebarMain, msgs);
        var small = DetailShim.TryRebar(s.RebarSmall, msgs);
        Material St(int d) => d <= 10 ? small : main;
        var spans = DetailShim.ParseList(Spans);
        var d = new BeamLineDesign
        {
            Name = Name, Quantity = Math.Max(1, Quantity), B = B, H = H, TopElevation = TopElevation,
            Section = Dt.Mm(B) + "x" + Dt.Mm(H)
        };
        var r = new DetailResult { Kind = "Dầm", Name = Name, Quantity = d.Quantity, ConcreteClass = conc.Name, Source = d };
        r.Messages = d.Messages; r.Checks = d.Checks; r.Bars = d.Bars;
        d.Messages.AddRange(msgs);
        if (B <= 0.05 || H <= 0.10 || spans.Count == 0)
        {
            d.Messages.Add("LỖI kích thước dầm / danh sách nhịp không hợp lệ (b, h > 0; nhịp vd. \"4,2; 3,6\")");
            return r;
        }
        if (BotN < 2 || TopN < 2) d.Messages.Add("LỖI thép chạy suốt tối thiểu 2 thanh (ở 2 góc đai) cho cả lớp trên và lớp dưới");
        int ns = spans.Count;
        bool isFound = (Position ?? "").StartsWith("Dầm móng"), isRoof = (Position ?? "").StartsWith("Dầm mái");
        double c = isFound ? Math.Max(s.CoverBeam, 0.035) : s.CoverBeam, w = Math.Max(SupportWidth, 0);
        var nodeX = new double[ns + 1];
        for (int k = 0; k < ns; k++) nodeX[k + 1] = nodeX[k] + spans[k];
        double xEnd = nodeX[ns];
        var lcl = spans.Select(L => Math.Max(L - w, 0.05)).ToArray();

        var gBot = BottomGroups(); var gTop = TopGroups();
        double asBotC = Math.Max(BotN, 0) * Dt.Area(BotD), asTopC = Math.Max(TopN, 0) * Dt.Area(TopD);
        double asAdd = gBot.Sum(g => g.N * Dt.Area(g.D)), asSup = gTop.Sum(g => g.N * Dt.Area(g.D));
        string botText = RcSection.BarText(new[] { (BotD, Math.Max(BotN, 0)) }.Concat(gBot.Select(g => (g.D, g.N))).Where(g => g.Item2 > 0));
        string topText = RcSection.BarText(new[] { (TopD, Math.Max(TopN, 0)) }.Concat(gTop.Select(g => (g.D, g.N))).Where(g => g.Item2 > 0));
        for (int k = 0; k < ns; k++)
        {
            d.Spans.Add(new BeamSpanResult
            {
                L = spans[k], Lclear = lcl[k], SupportWidthLeft = w, SupportWidthRight = w,
                LeftSupport = (k + 1).ToString(Dt.Inv), RightSupport = (k + 2).ToString(Dt.Inv),
                MLeft = -Math.Abs(MSupport), MRight = -Math.Abs(MSupport), MSpan = Math.Abs(MSpan), QMax = Math.Abs(Q),
                AsBotProv = asBotC + asAdd, AsTopLeftProv = asTopC + asSup, AsTopRightProv = asTopC + asSup,
                BotText = botText, TopLeftText = topText, TopRightText = topText,
                StirrupDia = StirD, StirrupSupport = StirSupport, StirrupMid = StirMid
            });
        }

        // ---- thép dọc (quy tắc như BeamDesigner.Detail; tuỳ chọn neo / cắt theo thực hành TCVN 5574:2018 mục 10.3.5) ----
        double xs = -w / 2 + c, xe = xEnd + w / 2 - c;
        double Lan(int dia) => Math.Max(30 * dia / 1000.0, RebarDetailing.AnchorageLength(conc, St(dia), dia));
        // neo ở gối biên: đoạn thẳng trong gối (w − c) + đoạn bẻ mỏ để tổng ≥ lan (10d ≤ mỏ ≤ h − 2c); hook = false → thẳng, không mỏ
        double AnchorLeg(int dia, bool hook)
        {
            double dm = dia / 1000.0;
            if (w <= 1e-6 || !hook) return 0;
            double straight = Math.Max(w - c, 0);
            double leg = Math.Max(Lan(dia) - straight, 0);
            if (leg > 1e-6) leg = Math.Min(Math.Max(leg, 10 * dm), H - 2 * c);
            return Dt.R10(leg);
        }
        BarMark Long(string role, bool top, int dia, int n, double x0, double x1, double legL, double legR, int span)
        {
            var bm = Dt.Hooked(role, dia, n, Dt.R10(x1 - x0), legL, legR, top);
            bm.Start = x0; bm.End = x1; bm.Span = span;
            return bm;
        }
        bool botHook = BotHook != HookNo, topHook = TopHook != HookNo;
        if (BotN > 0) d.Bars.Add(Long("Dưới", false, BotD, BotN, xs, xe, AnchorLeg(BotD, botHook), AnchorLeg(BotD, botHook), -1));
        if (TopN > 0) d.Bars.Add(Long("Trên", true, TopD, TopN, xs, xe, AnchorLeg(TopD, topHook), AnchorLeg(TopD, topHook), -1));
        if (!botHook && w > 1e-6 && w - c < 10 * BotD / 1000.0 - 1e-9)
            d.Messages.Add("Thép dưới neo thẳng vào gối biên " + Dt.Mm(w - c) + " mm < 10d = " + Dt.Mm(10 * BotD / 1000.0) + " mm (TCVN 5574:2018 mục 10.3.5.5) – nên bẻ mỏ hoặc tăng bề rộng gối");
        if (xe - xs > s.StockLength)
            d.Messages.Add("Thép chạy suốt dài " + Dt.F(xe - xs) + " m > " + Dt.F(s.StockLength) + " m – nối chồng thép trên tại giữa nhịp, thép dưới tại gối (ll = "
                           + Dt.Mm(RebarDetailing.LapLength(conc, main, Math.Max(BotD, TopD))) + " mm), so le ≥ 1,3·ll, ≤ 50% số thanh tại một tiết diện");
        // mũ gối: vươn từ mép gối theo lựa chọn; "qua hết nhịp ngắn" → chạy qua nhịp ngắn hơn kề bên tới gối bên kia
        foreach (var g in gTop)
        {
            string role = g.No == 2 ? "Mũ gối" : "Mũ gối " + g.No;
            for (int j = 0; j <= ns; j++)
            {
                bool end0 = j == 0, endN = j == ns;
                double extL = end0 ? 0 : ExtLength(g.Opt, lcl[j - 1], false), extR = endN ? 0 : ExtLength(g.Opt, lcl[j], false);
                if (end0) extR = ExtLength(g.Opt, lcl[0], true);
                if (endN) extL = ExtLength(g.Opt, lcl[ns - 1], true);
                if (g.Opt == ExtShort && !end0 && !endN)
                {
                    if (lcl[j - 1] <= lcl[j]) extL = lcl[j - 1] + w / 2 - c; else extR = lcl[j] + w / 2 - c;
                }
                double x0 = end0 ? xs : nodeX[j] - w / 2 - extL, x1 = endN ? xe : nodeX[j] + w / 2 + extR;
                double lg0 = end0 ? AnchorLeg(g.D, g.Hook) : 0, lg1 = endN ? AnchorLeg(g.D, g.Hook) : 0;
                d.Bars.Add(Long(role, true, g.D, g.N, x0, x1, lg0, lg1, end0 ? 0 : endN ? ns - 1 : j));
            }
        }
        // tăng cường dưới nhịp: cắt cách mép gối 0,1·L0 / L0/7 / L0/8 hoặc kéo vào gối (neo ≥ max(10d; 150) qua mép gối, gối biên bẻ mỏ nếu chọn)
        foreach (var g in gBot)
        {
            string role = g.No == 2 ? "Tăng cường dưới" : "Tăng cường dưới " + g.No;
            for (int k = 0; k < ns; k++)
            {
                double f0 = nodeX[k] + w / 2, f1 = nodeX[k + 1] - w / 2, l0 = lcl[k];
                double x0, x1, lg0 = 0, lg1 = 0;
                if (g.Opt == CutAnchor)
                {
                    double into = Math.Min(Math.Max(10 * g.D / 1000.0, 0.15), w - c);
                    x0 = k == 0 ? xs : f0 - into; x1 = k == ns - 1 ? xe : f1 + into;
                    if (k == 0) lg0 = AnchorLeg(g.D, g.Hook);
                    if (k == ns - 1) lg1 = AnchorLeg(g.D, g.Hook);
                }
                else { double cd = CutDistance(g.Opt, l0); x0 = f0 + cd; x1 = f1 - cd; }
                d.Bars.Add(Long(role, false, g.D, g.N, x0, x1, lg0, lg1, k));
            }
        }
        if (H > 0.7)
        {
            int pairs = Math.Max((int)Math.Ceiling((H - 2 * c) / 0.4) - 1, 1);
            d.Bars.Add(Long("Giá", false, 12, 2 * pairs, xs, xe, 0, 0, -1));
            d.Messages.Add("Dầm cao h > 700 mm: đặt cốt giá cạnh 2×" + pairs + "Ø12 (TCVN 5574:2018 mục 10.3.6)");
        }

        // ---- đai ----
        int total = 0;
        double ss = Math.Max(StirSupport, 0.05), sm = Math.Max(StirMid, 0.05);
        for (int k = 0; k < ns; k++)
        {
            double L0 = lcl[k];
            int nsup = (int)Math.Ceiling(L0 / 4 / ss - 1e-9);
            double zs = nsup * ss;
            if (2 * zs > L0) { zs = L0 / 2; nsup = (int)Math.Ceiling(zs / ss - 1e-9); }
            double mid = L0 - 2 * zs;
            total += mid < 1e-6 ? 2 * nsup + 1 : 2 * (nsup + 1) + Math.Max((int)Math.Ceiling(mid / sm - 1e-9) - 1, 0);
            d.Spans[k].SupportZone = zs;
            double f0 = nodeX[k] + w / 2, f1 = nodeX[k + 1] - w / 2;
            d.StirrupZones.Add(new StirrupZone { Span = k, From = f0, To = f0 + zs, Dia = StirD, Spacing = ss, Legs = Legs });
            if (mid > 1e-6) d.StirrupZones.Add(new StirrupZone { Span = k, From = f0 + zs, To = f1 - zs, Dia = StirD, Spacing = sm, Legs = Legs });
            d.StirrupZones.Add(new StirrupZone { Span = k, From = f1 - zs, To = f1, Dia = StirD, Spacing = ss, Legs = Legs });
        }
        double cst = Math.Max(c - StirD / 1000.0, 0.015);
        var st = Dt.Stirrup("Đai", StirD, total, B - 2 * cst, H - 2 * cst, ss);
        st.End = xEnd;
        d.Bars.Add(st);
        if (Legs > 2) d.Messages.Add("Đai " + Legs + " nhánh: thêm đai lồng / đai C cho nhánh giữa (thống kê theo đai kín 2 nhánh × " + Dt.F(Legs / 2.0) + ")");

        // ---- kiểm tra ----
        // trọng tâm thép theo hàng: hàng 1 sát lớp bảo vệ, hàng 2 cách hàng 1 khe 25 mm (dưới) / 30 mm (trên); hàng 1 không đủ khe hở → thanh dư sang hàng 2
        double a1 = LayerCentroid(c, BotD, BotN, gBot, B, StirD, false, out bool twoLayers);
        double aT = LayerCentroid(c, TopD, TopN, gTop, B, StirD, true, out bool twoTop);
        double h0b = H - a1, h0t = H - aT;
        double aComp = c + TopD / 2000.0;
        double muSpan = RcSection.MomentCapacity(B, h0b, asBotC + asAdd, asTopC, aComp, conc, main);
        double muSup = RcSection.MomentCapacity(B, h0t, asTopC + asSup, asBotC, a1, conc, main);
        r.Calc.Add("## Dầm " + Name + " " + d.Section + " – " + ns + " nhịp " + string.Join(" + ", spans.Select(x => Dt.F(x))) + " m, bê tông " + conc.Name + ", thép " + main.Name);
        r.Calc.Add("Thép dưới giữa nhịp " + botText + ": As = " + Dt.F((asBotC + asAdd) * 1e4, "0.00") + " cm², h0 = " + Dt.Mm(h0b) + " mm"
                   + (twoLayers ? " (2 lớp – không đủ khe hở trên 1 lớp)" : ""));
        r.Calc.Add("Mu nhịp = Rb·b·x·(h0 − x/2) + Rsc·As'·(h0 − a') = " + Dt.F(muSpan, "0.0") + " kN.m (TCVN 5574:2018 mục 8.1.2.2.4)");
        foreach (var g in gBot) r.Calc.Add("Tăng cường dưới (" + g.No + ") " + g.N + "Ø" + g.D + " hàng " + Math.Clamp(g.Layer, 1, 2) + ": " + (g.Opt == CutAnchor ? "kéo vào gối, neo ≥ max(10d; 150 mm)" + (g.Hook ? ", gối biên bẻ mỏ" : "") : g.Opt.ToLowerInvariant()));
        foreach (var g in gTop) r.Calc.Add("Mũ gối (" + g.No + ") " + g.N + "Ø" + g.D + " hàng " + Math.Clamp(g.Layer, 1, 2) + ": vươn " + (g.Opt == ExtStd ? "L0/3 ở gối giữa, L0/4 ở gối biên" : g.Opt) + (g.Hook ? ", gối biên bẻ mỏ" : ", gối biên neo thẳng"));
        r.Calc.Add("Thép trên tại gối " + topText + ": As = " + Dt.F((asTopC + asSup) * 1e4, "0.00") + " cm², Mu gối = " + Dt.F(muSup, "0.0") + " kN.m");
        if (MSpan > 0) d.Checks.Add(Dt.Check("Chịu uốn giữa nhịp M ≤ Mu", MSpan, muSpan, "kN.m"));
        if (MSupport > 0) d.Checks.Add(Dt.Check("Chịu uốn tại gối |M| ≤ Mu", MSupport, muSup, "kN.m"));
        if (Q > 0)
        {
            double qu = RcSection.ShearCapacity(B, h0b, conc, small, StirD, Math.Max(Legs, 2), ss);
            double strut = 0.3 * conc.Rb * B * h0b;
            d.Checks.Add(Dt.Check("Chịu cắt vùng gối Q ≤ Qb + Qsw", Q, qu, "kN", "đai " + Dt.Spaced(StirD, ss) + " " + Legs + " nhánh"));
            d.Checks.Add(Dt.Check("Dải nén giữa các vết nứt nghiêng Q ≤ 0,3·Rb·b·h0", Q, strut, "kN"));
            r.Calc.Add("Qu = Qb + Qsw = " + Dt.F(qu, "0.0") + " kN với đai " + Dt.Spaced(StirD, ss) + "; 0,3·Rb·b·h0 = " + Dt.F(strut, "0.0") + " kN (mục 8.1.3)");
        }
        double muMin = 0.001;
        d.Checks.Add(Dt.Check("Hàm lượng thép dưới μ ≥ μmin = 0,1 %", muMin * 100, (asBotC + asAdd) / (B * h0b) * 100, "%"));
        d.Checks.Add(Dt.Check("Hàm lượng thép trên tại gối μ ≥ 0,1 %", muMin * 100, (asTopC + asSup) / (B * h0t) * 100, "%"));
        d.Checks.Add(Dt.Check("Bước đai vùng gối s ≤ min(h0/2; 300 mm)", ss * 1000, Math.Min(h0b / 2, 0.30) * 1000, "mm", "TCVN 5574:2018 mục 10.3.4"));
        d.Checks.Add(Dt.Check("Bước đai giữa nhịp s ≤ min(0,75·h0; 500 mm)", sm * 1000, Math.Min(0.75 * h0b, 0.50) * 1000, "mm"));
        if (twoTop)
            d.Messages.Add("Thép trên tại gối " + topText + " đặt 2 hàng (h0 gối = " + Dt.Mm(h0t) + " mm)");
        if (twoLayers && !Dt.FitsInLayer(B, c, StirD, BotN, BotD, false))
            d.Messages.Add("LỖI thép dưới chạy suốt " + BotN + "Ø" + BotD + " không đủ khe hở trên 1 lớp – giảm số thanh / tăng b");
        if (MSpan <= 0 && MSupport <= 0 && Q <= 0)
            d.Messages.Add("Chưa nhập nội lực – chỉ kiểm tra cấu tạo (hàm lượng, bước đai, khe hở thép); Mu, Qu ghi trong thuyết minh để đối chiếu");
        foreach (var k in d.Checks.Where(x => !x.Ok)) d.Messages.Add("LỖI " + k.Name + ": " + Dt.F(k.Demand, "0.0") + " > " + Dt.F(k.Capacity, "0.0") + " " + k.Unit);

        RebarNumbering.Renumber(d.Bars);
        double clear = lcl.Sum();
        r.ConcreteVolume = B * H * clear;
        r.FormworkArea = (B + 2 * H) * clear;
        d.ManualSlab = SlabContext(this);
        bool leftB = (Boundary ?? "").Contains("trái"), rightB = (Boundary ?? "").Contains("phải");
        if (isFound)
        {
            r.Kind = "Dầm móng";
            double over = Math.Max(LeanOverhang, 0);
            double lw = B + (leftB ? 0 : over) + (rightB ? 0 : over);
            d.ManualSlab.LeanT = s.LeanThickness * 1000; d.ManualSlab.LeanOver = over * 1000;
            d.ManualSlab.LeanBoundaryL = leftB; d.ManualSlab.LeanBoundaryR = rightB;
            d.ManualSlab.LeanText = "BÊ TÔNG LÓT " + s.LeanConcrete + " DÀY " + Dt.Mm(s.LeanThickness) + (leftB || rightB ? " (GIÁP NHÀ BÊN: KHÔNG VƯƠN SANG)" : "");
            r.LeanVolume = lw * s.LeanThickness * (clear + 0.0);
            r.FormworkArea = (leftB || rightB ? 1 : 2) * H * clear;   // đáy đổ trên lót, không ván khuôn đáy
            r.Calc.Add("Dầm móng: lớp bảo vệ ≥ 35 mm (tiếp xúc đất, có bê tông lót); bê tông lót " + s.LeanConcrete + " dày " + Dt.Mm(s.LeanThickness) + " mm rộng "
                       + Dt.Mm(lw) + " mm" + (leftB || rightB ? " – giáp nhà hàng xóm " + (leftB ? "bên trái" : "bên phải") + " (hết đất): lót và dầm không vươn sang đất nhà bên" : " (vươn " + Dt.Mm(over) + " mm mỗi bên)"));
            if (leftB || rightB)
            {
                r.Extra.Add(("Xây gạch làm khuôn thành dầm móng giáp nhà hàng xóm (không tháo)", "m²", H * clear));
                d.Messages.Add("Dầm móng giáp nhà hàng xóm (hết đất): thành phía đó dùng gạch xây / tấm khuôn để lại, không vươn bê tông lót sang đất nhà bên");
            }
            if ((Flange ?? "").StartsWith("Chữ T") || (Flange ?? "").StartsWith("Chữ L")) d.Messages.Add("Dầm móng có sàn nền (cánh) – kiểm tra lại lựa chọn \"Cánh sàn\" nếu nền không đổ liền khối");
        }
        else if (isRoof)
        {
            r.Kind = "Dầm mái";
            d.Messages.Add("Dầm mái: chống thấm mặt trên sàn mái, đổ bê tông liên tục; thép cột tầng mái uốn mỏ neo vào dầm (khoá đầu cột)");
        }
        return r;
    }

    /// <summary>Khoảng cách trọng tâm thép tới mép chịu kéo (m): thanh chạy suốt + nhóm tăng cường theo hàng khai báo;
    /// hàng 1 không đủ khe hở (≥ max(d; 25/30 mm)) thì phần dư chuyển sang hàng 2; hàng 2 cách hàng 1 khe 25 mm (dưới) / 30 mm (trên).</summary>
    public static double LayerCentroid(double c, int dCont, int nCont, IList<AddGroup> groups, double b, int stirD, bool top, out bool twoLayers)
    {
        var l1 = new List<(int D, int N)> { (dCont, Math.Max(nCont, 0)) };
        var l2 = new List<(int D, int N)>();
        foreach (var g in groups) (g.Layer >= 2 ? l2 : l1).Add((g.D, g.N));
        // dồn thanh dư hàng 1 sang hàng 2
        while (!Dt.FitsInLayer(b, c, stirD, l1.Sum(x => x.N), l1.Max(x => x.D), top))
        {
            int i = l1.FindLastIndex(x => x.N > 0);
            if (i < 0 || (i == 0 && l1[0].N <= 2)) break;   // giữ tối thiểu 2 thanh góc đai ở hàng 1
            var gi = l1[i]; l1[i] = (gi.D, gi.N - 1);
            l2.Add((gi.D, 1));
        }
        twoLayers = l2.Sum(x => x.N) > 0;
        double d1 = l1.Where(x => x.N > 0).Select(x => x.D).DefaultIfEmpty(dCont).Max() / 1000.0;
        double y1 = c + d1 / 2;
        double sum = 0, mom = 0;
        foreach (var (dd, n) in l1) { double a = n * Dt.Area(dd); sum += a; mom += a * y1; }
        foreach (var (dd, n) in l2) { double a = n * Dt.Area(dd); sum += a; mom += a * (c + d1 + (top ? 0.030 : 0.025) + dd / 2000.0); }
        return sum > 1e-12 ? mom / sum : y1;
    }

    /// <summary>Bối cảnh sàn nhập tay cho mặt cắt dầm: chữ T / L, hạ cốt từng bên (mm), thép sàn thể hiện.</summary>
    public static BeamSlabSection SlabContext(ManualBeam b)
    {
        string f = b.Flange ?? "";
        if (f.StartsWith("Không")) return new BeamSlabSection();
        SlabSideInfo Side(double drop)
        {
            var si = new SlabSideInfo { Hs = Math.Max(b.SlabT, 0.04) * 1000, Dz = Math.Min(drop, 0) * 1000, Label = "S" };
            if (BeamSlabContext.TryParseBar(b.SlabBot ?? "", out int db, out double sb)) { si.BotPerpDia = si.BotParDia = db; si.BotPerpSpacing = si.BotParSpacing = sb; si.BotFromDesign = true; }
            if (BeamSlabContext.TryParseBar(b.SlabTop ?? "", out int dt, out double st)) { si.TopDia = dt; si.TopSpacing = st; si.TopFromDesign = true; }
            return si;
        }
        return new BeamSlabSection
        {
            Left = f.StartsWith("Chữ T") || f.Contains("trái") ? Side(b.DropLeft) : null,
            Right = f.StartsWith("Chữ T") || f.Contains("phải") ? Side(b.DropRight) : null
        };
    }

    public List<Figure> Draw(DetailResult r, DetailSettings s) =>
        r?.Source is BeamLineDesign d && d.Spans.Count > 0 ? BeamDrawing.Build(DetailShim.Project(s), d) : new List<Figure>();
}

// ---------------------------------------------------------------------
// CỘT
// ---------------------------------------------------------------------
public class ManualColumn : IDetailMember
{
    [Field("Tên cột", Group = "Chung")] public string Name { get; set; } = "C1";
    [Field("Số lượng", Group = "Chung")] public int Quantity { get; set; } = 1;
    [Field("Cạnh b (phương X)", Unit = "mm", Scale = 1000, Group = "Tiết diện")] public double B { get; set; } = 0.22;
    [Field("Cạnh h (phương Y)", Unit = "mm", Scale = 1000, Group = "Tiết diện")] public double H { get; set; } = 0.30;
    [Field("Chiều cao các tầng (từ dưới lên)", Unit = "m", Group = "Tiết diện", Hint = "vd. 3,9; 3,6; 3,6")] public string Stories { get; set; } = "3,9; 3,6";
    [Field("Cao độ chân cột (mặt móng)", Unit = "m", Group = "Tiết diện")] public double BaseElevation { get; set; } = -1.0;
    [Field("Chiều cao dầm tại nút (đỉnh tầng)", Unit = "mm", Scale = 1000, Group = "Tiết diện")] public double JointDepth { get; set; } = 0.40;

    [Field("Chân cột", Group = "Vị trí cột", Choices = "Từ móng – thép chờ trong móng|Trên dầm / sàn – thép chờ trong dầm đỡ")] public string BaseType { get; set; } = "Từ móng – thép chờ trong móng";
    [Field("Chiều cao móng / dầm đỡ (chỗ đặt thép chờ)", Unit = "mm", Scale = 1000, Group = "Vị trí cột")] public double SupportDepth { get; set; } = 0.40;
    [Field("Thép chờ trên mặt móng – cổ cột (0 = max(30d; ll))", Unit = "mm", Scale = 1000, Group = "Vị trí cột")] public double StarterLen { get; set; }
    [Field("Đỉnh cột", Group = "Vị trí cột", Choices = "Tầng mái – khoá đầu cột uốn mỏ|Chờ nối lên tầng trên")] public string TopType { get; set; } = "Tầng mái – khoá đầu cột uốn mỏ";

    [Field("Thép dọc – Ø", Unit = "mm", Group = "Thép")] public int Dia { get; set; } = 16;
    [Field("Số thanh trên cạnh b (kể cả góc)", Group = "Thép")] public int NB { get; set; } = 2;
    [Field("Số thanh trên cạnh h (kể cả góc)", Group = "Thép")] public int NH { get; set; } = 3;
    [Field("Đai – Ø", Unit = "mm", Group = "Thép")] public int StirD { get; set; } = 6;
    [Field("Bước đai đầu cột", Unit = "mm", Scale = 1000, Group = "Thép")] public double StirEnd { get; set; } = 0.10;
    [Field("Bước đai giữa cột", Unit = "mm", Scale = 1000, Group = "Thép")] public double StirMid { get; set; } = 0.20;

    [Field("N nén tính toán (0 = không kiểm tra)", Unit = "kN", Group = "Nội lực tầng dưới (tuỳ chọn)")] public double N { get; set; }
    [Field("Mx (uốn quanh trục X, căng cạnh h)", Unit = "kN.m", Group = "Nội lực tầng dưới (tuỳ chọn)")] public double Mx { get; set; }
    [Field("My (uốn quanh trục Y, căng cạnh b)", Unit = "kN.m", Group = "Nội lực tầng dưới (tuỳ chọn)")] public double My { get; set; }
    [Field("Hệ số chiều dài tính toán ψ", Group = "Nội lực tầng dưới (tuỳ chọn)")] public double Psi { get; set; } = 0.7;

    public const string SpliceAuto = "Tự gợi ý (giữa chiều cao thông thuỷ, ngoài vùng nguy hiểm lcr)",
        SpliceFloor = "Nối trên mặt sàn (chân cột – thường dùng nhà thấp tầng)",
        SpliceMech = "Nối cơ khí (ống ren)";
    [Field("Vị trí / cách nối thép dọc", Group = "Nối thép cột", Choices = SpliceAuto + "|" + SpliceFloor + "|" + SpliceMech,
        Hint = "Tự gợi ý: nối ngoài vùng nguy hiểm lcr = max(hc; Hthông thuỷ/6; 450 mm) ở hai đầu cột")]
    public string SpliceMode { get; set; } = SpliceAuto;
    [Field("Nối so le 50 % số thanh (2 nhóm cách nhau ≥ 1,3·ll)", Group = "Nối thép cột")] public bool Stagger { get; set; } = true;
    [Field("Nhấn cổ chai 1:6 khi nối (thanh dưới nhấn vào trong 1Ø)", Group = "Nối thép cột",
        Hint = "Đầu thanh tầng dưới uốn xiên 1:6 vào trong một đường kính để thanh tầng trên giữ đúng lớp bảo vệ")]
    public bool Crank { get; set; }

    public const double FootingAnchor = 0.50;

    public DetailResult Run(DetailSettings s)
    {
        var msgs = new List<string>();
        var conc = DetailShim.TryConcrete(s.Concrete, msgs);
        var main = DetailShim.TryRebar(s.RebarMain, msgs);
        var h = DetailShim.ParseList(Stories);
        var d = new ColumnDesign { Name = Name, Quantity = Math.Max(1, Quantity), GridName = Name };
        var r = new DetailResult { Kind = "Cột", Name = Name, Quantity = d.Quantity, ConcreteClass = conc.Name, Source = d };
        r.Messages = d.Messages; r.Checks = d.Checks; r.Bars = d.Bars;
        d.Messages.AddRange(msgs);
        if (B <= 0.1 || H <= 0.1 || h.Count == 0 || NB < 2 || NH < 2 || Dia <= 0)
        {
            d.Messages.Add("LỖI kích thước cột / chiều cao tầng / số thanh không hợp lệ (b, h ≥ 100 mm; ≥ 2 thanh mỗi cạnh)");
            return r;
        }
        double c = s.CoverColumn, z = BaseElevation;
        var lay = ColumnLayout.Create(B, H, c + Dia / 2000.0, Dia, NB, NH);
        for (int i = 0; i < h.Count; i++)
        {
            d.Stories.Add(new ColumnStoryResult
            {
                Story = "Tầng " + (i + 1).ToString(Dt.Inv), B = B, H = H, Length = h[i], ZBottom = z, ZTop = z + h[i],
                Dia = Dia, BarsAlongB = NB, BarsAlongH = NH, StirrupDia = StirD, StirrupSpacing = StirMid, StirrupSpacingEnd = StirEnd,
                JointDepth = Math.Min(JointDepth, h[i] / 2), AsProv = lay.As, N = i == 0 ? N : 0, M2 = i == 0 ? My : 0, M3 = i == 0 ? Mx : 0
            });
            z += h[i];
        }
        int nTot = 2 * NB + 2 * Math.Max(0, NH - 2);
        double dm = Dia / 1000.0;
        double lap = Dt.R10(Math.Max(30 * dm, RebarDetailing.LapLength(conc, main, Dia)));
        int ns = d.Stories.Count;
        // vị trí nối theo lựa chọn: tự gợi ý (giữa chiều cao thông thuỷ, ngoài vùng lcr hai đầu), trên mặt sàn, hoặc ống nối ren;
        // so le 2 nhóm ≥ 1,3·ll (ống ren: ≥ max(35d; 500 mm)) nếu chọn và đủ chỗ
        var cA = new double[ns + 1]; var cB = new double[ns + 1];
        bool mech = SpliceMode == SpliceMech, floorSp = SpliceMode == SpliceFloor;
        bool stagger = Stagger;
        double lenS = mech ? 0 : lap;
        double zb0 = BaseElevation;
        r.Calc.Add("Nối thép dọc: " + (mech ? "ống nối ren (cơ khí), không cần chiều dài nối chồng" : floorSp ? "nối chồng ngay trên mặt sàn, ll = " + Dt.Mm(lap) + " mm" : "nối chồng ll = " + Dt.Mm(lap) + " mm tại vùng gợi ý ngoài lcr")
                   + (Stagger ? ", so le 50 % số thanh" : ", nối 100 % tại một tiết diện") + (Crank && !mech ? ", nhấn cổ chai 1:6 đầu thanh dưới" : ""));
        for (int t = 1; t < ns; t++)
        {
            var su = d.Stories[t];
            double lcr = Math.Max(Math.Max(B, H), Math.Max(su.Length / 6, 0.45));
            double zf = su.ZBottom - zb0;
            double za = zf + lcr, zbb = su.ZTop - zb0 - su.JointDepth - lcr;
            double zm = (zf + su.ZTop - zb0 - su.JointDepth) / 2;
            double sd = mech ? RcSection.CeilTo(Math.Max(35 * dm, 0.50), 0.01) : RcSection.CeilTo(1.3 * lap, 0.01);
            r.Calc.Add(su.Story + ": vùng nối gợi ý cách mặt sàn " + Dt.Mm(lcr) + " … " + Dt.Mm(Math.Max(zbb - zf, lcr)) + " mm (ngoài vùng nguy hiểm lcr = max(hc; H/6; 450) = "
                       + Dt.Mm(lcr) + " mm ở chân và đỉnh cột)");
            if (floorSp)
            {
                cA[t] = zf + lenS / 2; cB[t] = stagger ? cA[t] + sd : cA[t];
                if (t == 1) d.Messages.Add("Nối thép cột trên mặt sàn: mối nối nằm trong vùng nguy hiểm lcr chân cột – chỉ nên dùng cho nhà thấp tầng / không yêu cầu kháng chấn; đai vùng nối bước ≤ min(10d; 100 mm)");
            }
            else if (mech)
            {
                if (!stagger || zbb - za >= sd) { cA[t] = stagger ? Math.Clamp(zm - sd / 2, za, Math.Max(zbb - sd, za)) : zm; cB[t] = stagger ? cA[t] + sd : cA[t]; }
                else { stagger = false; cA[t] = cB[t] = zm; d.Messages.Add("Ống nối " + su.Story + ": không đủ chỗ so le ngoài vùng lcr – nối 100 % tại giữa cột (ống ren loại cấp 1 cho phép)"); }
                if (zbb - za < 0) d.Messages.Add("Ống nối " + su.Story + ": cột thấp, vùng ngoài lcr không còn – đặt ống nối tại giữa chiều cao thông thuỷ");
            }
            else if (zbb - za >= lap + (stagger ? sd : 0))
            {
                if (stagger) { cA[t] = Math.Clamp(zm - sd / 2, za + lap / 2, zbb - lap / 2 - sd); cB[t] = cA[t] + sd; }
                else cA[t] = cB[t] = Math.Clamp(zm, za + lap / 2, zbb - lap / 2);
            }
            else
            {
                stagger = false; cA[t] = cB[t] = zm;
                d.Messages.Add((zbb - za >= lap ? "" : "LỖI ") + "Nối thép cột " + su.Story + ": đoạn ngoài vùng nguy hiểm " + Dt.F(Math.Max(zbb - za, 0)) + " m "
                               + (zbb - za >= lap ? "không đủ so le – nối 100 % tại một tiết diện" : "< ll = " + Dt.F(lap) + " m – dùng ống nối ren hoặc thép liên tục qua tầng"));
            }
            foreach (var (g, cc) in new[] { ("A", cA[t]), ("B", cB[t]) })
                d.Splices.Add(new SpliceInfo { Dia = Dia, Span = t, Center = cc, Lap = lenS, Group = g, Count = g == "A" ? (nTot + 1) / 2 : nTot / 2, ZoneA = za, ZoneB = zbb,
                    Note = mech ? "ống nối ren" : "nối chồng ll = " + Dt.Mm(lap) + " mm" + (Crank ? ", nhấn cổ chai 1:6" : "") });
        }
        if (mech && ns > 1) r.Extra.Add(("Ống nối ren thép Ø" + Dia, "cái", nTot * (ns - 1)));
        // thanh nhấn cổ chai: đoạn thẳng v1 → đoạn xiên 1:6 lệch vào trong 1Ø → đoạn nối chồng ll (đầu trên)
        BarMark CrankBar(string crRole, int crN, double crVert, double crFoot)
        {
            double e = dm, run = 6 * e, incl = Math.Sqrt(run * run + e * e);
            double v1 = Math.Max(crVert - lap - run, 0.05);
            var segs = new List<double>();
            if (crFoot > 1e-6) segs.Add(Math.Round(crFoot * 1000));
            segs.Add(Math.Round(v1 * 1000)); segs.Add(Math.Round(incl * 1000)); segs.Add(Math.Round(lap * 1000));
            return new BarMark { Role = crRole, Dia = Dia, Count = crN, Shape = crFoot > 1e-6 ? "Nhấn L" : "Nhấn", Segments = segs, Length = Dt.R10(crFoot + v1 + incl + lap) };
        }
        bool crank = Crank && !mech;
        // ---- thép chờ chân cột (cổ cột) và khoá đầu cột tầng mái ----
        bool fromFooting = !(BaseType ?? "").StartsWith("Trên dầm"), roofTop = !(TopType ?? "").StartsWith("Chờ");
        double lan = Math.Max(30 * dm, RebarDetailing.AnchorageLength(conc, main, Dia));
        double starter = StarterLen > 0 ? StarterLen : RcSection.CeilTo(Math.Max(30 * dm, lap), 0.05);
        double embedC = fromFooting ? Math.Max(s.CoverFooting, 0.035) + 0.03 : s.CoverBeam + 0.02;   // tựa lên lưới thép đáy móng / thép dưới dầm
        double embed = Math.Max(SupportDepth - embedC, 0.15);
        double footLeg = Dt.R10(Math.Max(Math.Max(0.20, 12 * dm), lan - embed));
        if (footLeg > 0.6) footLeg = 0.6;
        string sRole = fromFooting ? "Thép chờ cột (trong móng)" : "Thép chờ cột (trong dầm đỡ)";
        var sb0 = crank ? CrankBar(sRole, nTot, Dt.R10(embed + starter), footLeg) : Dt.Hooked(sRole, Dia, nTot, Dt.R10(embed + starter), footLeg, 0);
        sb0.Start = -embed; sb0.End = starter; sb0.Span = 0;
        d.Bars.Add(sb0);
        r.Calc.Add("Thép chờ chân cột: chôn " + Dt.Mm(embed) + " mm vào " + (fromFooting ? "móng" : "dầm đỡ") + ", bẻ chân " + Dt.Mm(footLeg) + " mm tựa lưới thép; nhô khỏi mặt "
                   + (fromFooting ? "móng (cổ cột)" : "dầm") + " " + Dt.Mm(starter) + " mm ≥ max(30d = " + Dt.Mm(30 * dm) + "; ll = " + Dt.Mm(lap) + ") mm (TCVN 5574:2018 mục 10.3.6.3)");
        if (StarterLen > 0 && StarterLen < Math.Max(30 * dm, lap) - 1e-6)
            d.Messages.Add("LỖI thép chờ " + Dt.Mm(StarterLen) + " mm < max(30d; ll) = " + Dt.Mm(Math.Max(30 * dm, lap)) + " mm");
        double roofHook = 0;
        if (roofTop)
        {
            double inBeam = Math.Max(JointDepth - s.CoverBeam, 0.1);
            roofHook = Math.Ceiling(Math.Max(Math.Max(12 * dm, 0.15), lan - inBeam) * 100 - 1e-9) / 100.0;   // làm tròn lên 10 mm
            r.Calc.Add("Khoá đầu cột tầng mái: thép dọc kéo lên sát mặt dầm mái (cách mặt " + Dt.Mm(s.CoverBeam) + " mm) rồi uốn mỏ vào trong dầm đoạn "
                       + Dt.Mm(roofHook) + " mm ≥ max(12d; lan − (hd − c)) – tổng neo ≥ lan = " + Dt.Mm(lan) + " mm");
        }
        else r.Calc.Add("Đỉnh cột chờ nối lên tầng trên: thép nhô khỏi mặt sàn ll = " + Dt.Mm(lap) + " mm");
        for (int si = 0; si < ns; si++)
        {
            var st = d.Stories[si];
            double z0 = st.ZBottom - zb0, z1 = st.ZTop - zb0;
            bool bottom = si == 0, top = si == ns - 1;
            int nA = stagger ? (nTot + 1) / 2 : nTot;
            foreach (var (grp, cnt) in new[] { ("A", nA), ("B", nTot - nA) })
            {
                if (cnt <= 0) continue;
                double start = bottom ? z0 : (grp == "A" ? cA[si] : cB[si]) - lenS / 2;
                double end = top ? (roofTop ? z1 - s.CoverBeam : z1 + lap) : (grp == "A" ? cA[si + 1] : cB[si + 1]) + lenS / 2;
                double vert = Dt.R10(end - start);
                string bRole = grp == "A" ? "Thép dọc" : "Thép dọc (so le)";
                var bm = crank && !(top && roofTop) ? CrankBar(bRole, cnt, vert, 0) : Dt.Hooked(bRole, Dia, cnt, vert, 0, top && roofTop ? roofHook : 0);
                bm.Start = start; bm.End = end; bm.Span = si;
                d.Bars.Add(bm);
            }
            double cs = Math.Max(c - StirD / 1000.0, 0.015);
            double s1 = B - 2 * cs, s2 = H - 2 * cs;
            double zone = Math.Min(Math.Max(Math.Max(H, B), Math.Max(st.Length / 6, 0.45)), st.Length / 2);
            double se = Math.Max(StirEnd, 0.05), sm = Math.Max(StirMid, 0.05);
            int ne = (int)Math.Ceiling(zone / se - 1e-9);
            double ze = Math.Min(ne * se, st.Length / 2), mid = st.Length - 2 * ze;
            int cnt2 = mid > 1e-6 ? 2 * (ne + 1) + Math.Max((int)Math.Ceiling(mid / sm - 1e-9) - 1, 0) : 2 * ne + 1;
            d.StirrupZones.Add(new StirrupZone { Span = si, From = z0, To = z0 + ze, Dia = StirD, Spacing = se });
            if (mid > 1e-6) d.StirrupZones.Add(new StirrupZone { Span = si, From = z0 + ze, To = z1 - ze, Dia = StirD, Spacing = sm });
            d.StirrupZones.Add(new StirrupZone { Span = si, From = z1 - ze, To = z1, Dia = StirD, Spacing = se });
            var sb = Dt.Stirrup("Đai", StirD, cnt2, s1, s2, se); sb.Start = z0; sb.End = z1; sb.Span = si;
            d.Bars.Add(sb);
            int tiesH = NB >= 4 ? (NB - 1) / 2 : 0, tiesB = NH >= 4 ? (NH - 1) / 2 : 0;
            double hk = 2 * Math.Max(0.075, 10 * StirD / 1000.0);
            if (tiesH > 0) d.Bars.Add(new BarMark { Role = "Đai C", Dia = StirD, Count = tiesH * cnt2, Shape = "Đai C", Spacing = se, Start = z0, End = z1, Span = si, Length = Dt.R10(s2 + hk), Segments = new List<double> { Math.Round(s2 * 1000) } });
            if (tiesB > 0) d.Bars.Add(new BarMark { Role = "Đai C", Dia = StirD, Count = tiesB * cnt2, Shape = "Đai C", Spacing = se, Start = z0, End = z1, Span = si, Length = Dt.R10(s1 + hk), Segments = new List<double> { Math.Round(s1 * 1000) } });
        }

        // ---- kiểm tra ----
        double ratio = lay.As / (B * H) * 100;
        r.Calc.Add("## Cột " + Name + " " + Dt.Mm(B) + "x" + Dt.Mm(H) + ", " + nTot + "Ø" + Dia + " (As = " + Dt.F(lay.As * 1e4, "0.00") + " cm², μ = " + Dt.F(ratio, "0.00") + " %), bê tông " + conc.Name);
        d.Checks.Add(Dt.Check("Hàm lượng thép μ ≥ μmin = 0,4 %", 0.4, ratio, "%", "khuyến nghị cho cột khung nhà"));
        d.Checks.Add(Dt.Check("Hàm lượng thép μ ≤ 3 %", ratio, 3.0, "%"));
        double clrB = (B - 2 * c - NB * dm) / Math.Max(NB - 1, 1), clrH = (H - 2 * c - NH * dm) / Math.Max(NH - 1, 1);
        d.Checks.Add(Dt.Check("Khe hở thép cạnh b ≥ max(Ø; 50 mm) (thép đổ đứng)", Math.Max(dm, 0.05) * 1000, clrB * 1000, "mm"));
        d.Checks.Add(Dt.Check("Khe hở thép cạnh h ≥ max(Ø; 50 mm)", Math.Max(dm, 0.05) * 1000, clrH * 1000, "mm"));
        d.Checks.Add(Dt.Check("Khoảng cách thép dọc ≤ 400 mm (mục 10.3.3)", Math.Max(clrB, clrH) * 1000, 400, "mm"));
        d.Checks.Add(Dt.Check("Bước đai giữa cột s ≤ min(15Ø; 500 mm)", StirMid * 1000, Math.Min(15 * dm, 0.5) * 1000, "mm", "TCVN 5574:2018 mục 10.3.4.3"));
        d.Checks.Add(Dt.Check("Đường kính đai ≥ max(0,25·Ø; 6 mm)", Math.Max(0.25 * Dia, 6), StirD, "mm"));
        if (N > 0)
        {
            double L0 = Psi * h[0];
            double ea = RcSection.Ea(L0, Math.Max(B, H));
            double mx = Math.Max(Math.Abs(Mx), N * ea), my = Math.Max(Math.Abs(My), N * ea);
            double u = RcSection.BiaxialUtilization(N, my, mx, lay, conc, main, out double mu2, out double mu3);
            r.Calc.Add("Tầng dưới: N = " + Dt.F(N, "0.0") + " kN; Mx = " + Dt.F(mx, "0.0") + ", My = " + Dt.F(my, "0.0") + " kN.m (đã kể độ lệch tâm ngẫu nhiên ea = " + Dt.Mm(ea) + " mm; l0 = ψ·H = " + Dt.F(L0) + " m)");
            r.Calc.Add("Khả năng chịu uốn tại N: Mu,x = " + Dt.F(mu3, "0.0") + ", Mu,y = " + Dt.F(mu2, "0.0") + " kN.m; (Mx/Mu,x)^α + (My/Mu,y)^α = " + Dt.F(u, "0.000") + " (đường bao Bresler)");
            d.Stories[0].Utilization = u;
            d.Checks.Add(Dt.Check("Nén lệch tâm xiên tầng dưới (Bresler) ≤ 1", u, 1.0, ""));
            d.Messages.Add("Chưa kể uốn dọc η (nhập M đã nhân η nếu cột mảnh l0/h > 8)");
        }
        else d.Messages.Add("Chưa nhập nội lực – chỉ kiểm tra cấu tạo cột");
        foreach (var k in d.Checks.Where(x => !x.Ok)) d.Messages.Add("LỖI " + k.Name + ": " + Dt.F(k.Demand, "0.0##") + " > " + Dt.F(k.Capacity, "0.0##") + " " + k.Unit);
        RebarNumbering.Renumber(d.Bars);
        double totalH = h.Sum();
        r.ConcreteVolume = B * H * totalH;
        r.FormworkArea = 2 * (B + H) * totalH;
        return r;
    }

    public List<Figure> Draw(DetailResult r, DetailSettings s) =>
        r?.Source is ColumnDesign d && d.Stories.Count > 0 ? ColumnDrawing.Build(DetailShim.Project(s), d) : new List<Figure>();
}

// ---------------------------------------------------------------------
// SÀN (ô bản chữ nhật)
// ---------------------------------------------------------------------
public class ManualSlab : IDetailMember
{
    [Field("Tên ô sàn", Group = "Chung")] public string Name { get; set; } = "S1";
    [Field("Số lượng", Group = "Chung")] public int Quantity { get; set; } = 1;
    [Field("Cạnh ngắn Lx", Unit = "m", Group = "Kích thước")] public double Lx { get; set; } = 3.6;
    [Field("Cạnh dài Ly", Unit = "m", Group = "Kích thước")] public double Ly { get; set; } = 4.2;
    [Field("Chiều dày", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double T { get; set; } = 0.10;
    [Field("Số cạnh dài ngàm (0–2)", Group = "Liên kết", Hint = "cạnh dài giáp ô sàn khác = ngàm")] public int FixedLongEdges { get; set; } = 2;
    [Field("Số cạnh ngắn ngàm (0–2)", Group = "Liên kết")] public int FixedShortEdges { get; set; } = 2;
    [Field("Bề rộng dầm biên", Unit = "mm", Scale = 1000, Group = "Liên kết")] public double BeamWidth { get; set; } = 0.22;

    [Field("Tĩnh tải hoàn thiện (tiêu chuẩn)", Unit = "kN/m²", Group = "Tải trọng (0 = chỉ cấu tạo)")] public double Finish { get; set; } = 1.5;
    [Field("Hoạt tải (tiêu chuẩn)", Unit = "kN/m²", Group = "Tải trọng (0 = chỉ cấu tạo)")] public double Live { get; set; } = 2.0;

    [Field("Lớp dưới phương ngắn – Ø (0 = tự chọn)", Unit = "mm", Group = "Thép")] public int BotXD { get; set; }
    [Field("Lớp dưới phương ngắn – bước", Unit = "mm", Scale = 1000, Group = "Thép")] public double BotXS { get; set; }
    [Field("Lớp dưới phương dài – Ø", Unit = "mm", Group = "Thép")] public int BotYD { get; set; }
    [Field("Lớp dưới phương dài – bước", Unit = "mm", Scale = 1000, Group = "Thép")] public double BotYS { get; set; }
    [Field("Mũ gối – Ø", Unit = "mm", Group = "Thép")] public int TopD { get; set; }
    [Field("Mũ gối – bước", Unit = "mm", Scale = 1000, Group = "Thép")] public double TopS { get; set; }

    public static readonly int[] Dias = { 6, 8, 10, 12 };

    /// <summary>Mô men dải 1 m (TCVN – Marcus): khớp–khớp qL²/8; ngàm–khớp 9qL²/128 / −qL²/8; ngàm–ngàm qL²/24 / −qL²/12.</summary>
    public static (double Span, double Sup) Strip(double q, double L, int fixedEnds)
    {
        double ql2 = q * L * L;
        return fixedEnds >= 2 ? (ql2 / 24, ql2 / 12) : fixedEnds == 1 ? (9 * ql2 / 128, ql2 / 8) : (ql2 / 8, 0);
    }

    private static double DeflCoef(int f) => f >= 2 ? 1.0 : f == 1 ? 2.08 : 5.0;

    public DetailResult Run(DetailSettings s)
    {
        var r = new DetailResult { Kind = "Sàn", Name = Name, Quantity = Math.Max(1, Quantity) };
        var conc = DetailShim.TryConcrete(s.Concrete, r.Messages);
        var small = DetailShim.TryRebar(s.RebarSmall, r.Messages);
        var main = DetailShim.TryRebar(s.RebarMain, r.Messages);
        Material St(int d) => d <= 10 ? small : main;
        r.ConcreteClass = conc.Name;
        double lx = Math.Min(Lx, Ly), ly = Math.Max(Lx, Ly);
        if (lx <= 0.3 || T < 0.05)
        {
            r.Messages.Add("LỖI kích thước ô sàn không hợp lệ (Lx, Ly ≥ 0,3 m; t ≥ 50 mm)");
            return r;
        }
        int fx = Math.Clamp(FixedLongEdges, 0, 2), fy = Math.Clamp(FixedShortEdges, 0, 2);   // cạnh dài ngàm → dải phương ngắn ngàm
        double q = 1.1 * 25 * T + 1.2 * Finish + (Live < 2 ? 1.3 : 1.2) * Live;
        bool twoWay = ly / lx <= 2.0;
        double qx = q, qy = 0;
        if (twoWay)
        {
            double kx = DeflCoef(fx) * Math.Pow(lx, 4), ky = DeflCoef(fy) * Math.Pow(ly, 4);
            qx = q * ky / (kx + ky); qy = q - qx;
        }
        var (m1, mI) = Strip(qx, lx, fx);
        var (m2, mII) = twoWay ? Strip(qy, ly, fy) : (0.0, 0.0);
        double c = s.CoverSlab, sMax = Math.Min(1.5 * T, 0.20);
        double Req(double m, double extra, int dia) => RcSection.DesignRect(m, 1.0, T - c - extra - dia / 2000.0, c, conc, St(dia), 0.10).As;
        (int D, double S) Pick(int ud, double us, double m, double extra)
        {
            if (ud > 0 && us > 0) return (ud, us);
            foreach (int dia in Dias)
            {
                double a = Req(m, extra, dia);
                double sp = Math.Min(Math.Floor(Dt.Area(dia) / Math.Max(a, 1e-12) * 100 + 1e-9) / 100.0, sMax);
                if (sp >= 0.10 - 1e-9) return (dia, Math.Max(sp, 0.10));
            }
            return (12, 0.10);
        }
        var bx = Pick(BotXD, BotXS, m1, 0);
        var by = Pick(BotYD, BotYS, twoWay ? m2 : 0, bx.D / 1000.0);
        if (!twoWay && (BotYD <= 0 || BotYS <= 0))
        {
            // thép phân bố bản dầm ≥ 10 % thép chịu lực, ≥ Ø6a250 (TCVN 5574:2018 mục 10.3.8)
            var p = Dt.ChooseSpaced(Math.Max(0.1 * Dt.AreaPerM(bx.D, bx.S), 0), Dias, 0.25, 0.15);
            by = p;
        }
        double mTop = Math.Max(mI, mII);
        var tp = Pick(TopD, TopS, mTop, 0);
        r.Calc.Add("## Ô sàn " + Name + " " + Dt.F(lx) + " × " + Dt.F(ly) + " m, t = " + Dt.Mm(T) + " mm – " + (twoWay ? "bản kê 4 cạnh (ly/lx ≤ 2)" : "bản loại dầm (ly/lx > 2)"));
        r.Calc.Add("q = 1,1·25·t + 1,2·g_ht + n·p = " + Dt.F(q, "0.00") + " kN/m² (TCVN 2737:2023: n = 1,3 khi p < 2 kN/m², 1,2 khi p ≥ 2)");
        if (twoWay) r.Calc.Add("Phân tải Marcus: qx = q·ky/(kx + ky) = " + Dt.F(qx, "0.00") + ", qy = " + Dt.F(qy, "0.00") + " kN/m²");
        r.Calc.Add("Dải 1 m phương ngắn (" + fx + " đầu ngàm): M1 = " + Dt.F(m1, "0.00") + ", MI = " + Dt.F(-mI, "0.00") + " kN.m/m; phương dài (" + fy + " đầu ngàm): M2 = "
                   + Dt.F(m2, "0.00") + ", MII = " + Dt.F(-mII, "0.00") + " kN.m/m");
        void Chk(string zone, double m, (int D, double S) sc, double extra)
        {
            double h0 = T - c - extra - sc.D / 2000.0;
            double mu = RcSection.MomentCapacity(1.0, h0, Dt.AreaPerM(sc.D, sc.S), 0, c, conc, St(sc.D));
            r.Checks.Add(Dt.Check(zone + " " + Dt.Spaced(sc.D, sc.S) + ": M ≤ Mu", m, mu, "kN.m/m"));
            r.Calc.Add(zone + " " + Dt.Spaced(sc.D, sc.S) + ": As = " + Dt.F(Dt.AreaPerM(sc.D, sc.S) * 1e4, "0.00") + " cm²/m, h0 = " + Dt.Mm(h0) + " mm → Mu = " + Dt.F(mu, "0.00") + " kN.m/m");
        }
        if (Finish + Live > 0)
        {
            Chk("Lớp dưới phương ngắn", m1, bx, 0);
            if (twoWay) Chk("Lớp dưới phương dài", m2, by, bx.D / 1000.0);
            if (mTop > 1e-9) Chk("Thép mũ gối", mTop, tp, 0);
        }
        r.Checks.Add(Dt.Check("Bước thép lớp dưới ≤ min(1,5t; 200 mm)", Math.Max(bx.S, twoWay ? by.S : 0) * 1000, sMax * 1000, "mm"));
        double ratio = lx / T;
        r.Checks.Add(Dt.Check("Độ mảnh lx/t ≤ 45 (kiểm soát võng sơ bộ)", ratio, 45, ""));
        foreach (var k in r.Checks.Where(x => !x.Ok)) r.Messages.Add("LỖI " + k.Name + ": " + Dt.F(k.Demand, "0.00") + " > " + Dt.F(k.Capacity, "0.00") + " " + k.Unit);

        // ---- thép ----
        double w = BeamWidth;
        static double an(int d) => Math.Max(10 * d / 1000.0, 0.10);
        double lenX = Dt.R10(lx + w - 2 * c), lenY = Dt.R10(ly + w - 2 * c);
        var bX = Dt.Straight("Lớp dưới phương ngắn", bx.D, Dt.CountAcross(ly, 0.05, bx.S), lenX, false, bx.S);
        var bY = Dt.Straight("Lớp dưới phương dài", by.D, Dt.CountAcross(lx, 0.05, by.S), lenY, false, by.S);
        r.Bars.Add(bX); r.Bars.Add(bY);
        double leg = Dt.R10(T - 2 * c);
        if (fx > 0)
        {
            double ext = Dt.R10(lx / 4 + w / 2 + an(tp.D));
            var t1 = Dt.Hooked("Mũ gối cạnh dài", tp.D, fx * Dt.CountAcross(ly, 0.05, tp.S), ext, leg, 0, true, tp.S);
            r.Bars.Add(t1);
        }
        if (fy > 0 || !twoWay)
        {
            var tq = twoWay ? tp : Dt.ChooseSpaced(0, Dias, 0.20, 0.10);
            double ext = Dt.R10(lx / 4 + w / 2 + an(tq.D));
            var t2 = Dt.Hooked("Mũ gối cạnh ngắn", tq.D, Math.Max(fy, 1) * Dt.CountAcross(lx, 0.05, tq.S), ext, leg, 0, true, tq.S);
            r.Bars.Add(t2);
        }
        // thép phân bố đỡ mũ (Ø6a250)
        int nDist = (int)Math.Ceiling(lx / 4 / 0.25) * 2 * (fx + fy);
        if (nDist > 0) r.Bars.Add(Dt.Straight("Thép phân bố mũ", 6, nDist, Dt.R10(Math.Max(lx, ly) / 2), true, 0.25));
        RebarNumbering.Renumber(r.Bars);
        r.ConcreteVolume = lx * ly * T;
        r.FormworkArea = lx * ly;
        r.Extra.Add(("Diện tích sàn", "m²", lx * ly));
        _lastQ = q; _twoWay = twoWay;
        return r;
    }

    private double _lastQ; private bool _twoWay;

    /// <summary>Mặt bằng thép ô sàn (1/50): viền ô, dầm biên nét đứt, thép lớp dưới mỗi phương 1 thanh đại diện, thép mũ ở các cạnh ngàm; mặt cắt 1-1.</summary>
    public List<Figure> Draw(DetailResult r, DetailSettings s)
    {
        var figs = new List<Figure>();
        if (r == null || r.Bars.Count == 0) return figs;
        double S = 50;
        var w = new DxfWriter(S);
        double lx = Math.Min(Lx, Ly) * 1000, ly = Math.Max(Lx, Ly) * 1000, bw = BeamWidth * 1000;
        w.Rect(Lay.BeTong, 0, 0, lx, ly);
        w.Rect(Lay.BeTong, -bw / 2, -bw / 2, lx + bw / 2, ly + bw / 2, "DASHED");
        BarMark Find(string role) => r.Bars.FirstOrDefault(b => b.Role == role);
        var bX = Find("Lớp dưới phương ngắn"); var bY = Find("Lớp dưới phương dài");
        double hk = 0.9 * w.Th, e0 = -bw / 2 + 25, e1x = lx + bw / 2 - 25, e1y = ly + bw / 2 - 25;
        // lớp dưới: thanh đại diện chạy suốt ô, móc 2 đầu, chữ trên thanh, đường phạm vi rải vuông góc
        if (bX != null)
            PlanBarDraw.Bar(w, true, e0, e1x, ly * 0.55, Dt.Spaced(bX.Dia, bX.Spacing), bX.Mark, hk, hk, 1, 0.24, (50, ly - 50), 0.82);
        if (bY != null)
            PlanBarDraw.Bar(w, false, e0, e1y, lx * 0.66, Dt.Spaced(bY.Dia, bY.Spacing), bY.Mark, hk, hk, -1, 0.40, (50, lx - 50), 0.12);
        // thép mũ: thanh từ mép ngoài dầm vào trong ô, bẻ xuống ở đầu trong; phạm vi rải dọc cạnh
        var tL = Find("Mũ gối cạnh dài"); var tS = Find("Mũ gối cạnh ngắn");
        if (tL != null)
        {
            double ext = (tL.Segments.Count > 0 ? tL.Segments[0] : lx / 4);
            for (int side = 0; side < Math.Clamp(FixedLongEdges, 1, 2); side++)
            {
                double y = side == 0 ? ly * 0.30 : ly * 0.76;
                if (side == 0) PlanBarDraw.Bar(w, true, e0, e0 + ext, y, Dt.Spaced(tL.Dia, tL.Spacing), tL.Mark, 0, hk, -1, 0.55, (50, ly - 50), 0.30);
                else PlanBarDraw.Bar(w, true, e1x - ext, e1x, y, Dt.Spaced(tL.Dia, tL.Spacing), tL.Mark, hk, 0, -1, 0.45, (50, ly - 50), 0.70);
            }
        }
        if (tS != null)
        {
            double ext = (tS.Segments.Count > 0 ? tS.Segments[0] : lx / 4);
            for (int side = 0; side < Math.Clamp(FixedShortEdges, 1, 2); side++)
            {
                double x = lx * 0.40;
                if (side == 0) PlanBarDraw.Bar(w, false, e0, e0 + ext, x, Dt.Spaced(tS.Dia, tS.Spacing), tS.Mark, 0, hk, 1, 0.55, (50, lx - 50), 0.30, -1);
                else PlanBarDraw.Bar(w, false, e1y - ext, e1y, x, Dt.Spaced(tS.Dia, tS.Spacing), tS.Mark, hk, 0, 1, 0.45, (50, lx - 50), 0.70, -1);
            }
        }
        w.Dim(0, -bw / 2, lx, -bw / 2, -3 * w.Th);
        w.Dim(-bw / 2, 0, -bw / 2, ly, 3 * w.Th);
        w.Text(Lay.Chu, lx * 0.18, ly * 0.10, w.Th, "h = " + Dt.Mm(T), 0, 1, 2);
        w.Title(lx / 2, -bw / 2 - 7 * w.Th, RebarSchedule.MemberLabel("MẶT BẰNG THÉP SÀN " + Name, r.Quantity), DrawUtil.ScaleText(S));
        figs.Add(new Figure(w, "Sàn " + Name));

        // mặt cắt 1-1 theo phương ngắn
        double Ss = 25;
        var sct = new DxfWriter(Ss);
        double t = T * 1000, bh = 400;
        sct.Polyline(Lay.BeTong, new List<(double, double)> { (-bw, -bh), (0, -bh), (0, -t), (lx, -t), (lx, -bh), (lx + bw, -bh), (lx + bw, 0), (-bw, 0) }, true);
        double c = s.CoverSlab * 1000;
        if (bX != null)
        {
            sct.Line(Lay.Thep, -bw / 2, -t + c, lx + bw / 2, -t + c);
            sct.LeaderTag(lx * 0.5, -t + c, lx * 0.5 + 2.0 * sct.Th, -t - 4.0 * sct.Th, Dt.Spaced(bX.Dia, bX.Spacing), bX.Mark);
        }
        if (bY != null)
            for (double x = 100; x < lx; x += Math.Max(bY.Spacing * 1000, 100)) sct.Dot(Lay.Thep, x, -t + c + (bX?.Dia ?? 8), bY.Dia / 2.0);
        if (tL != null)
        {
            double ext = tL.Segments.Count > 0 ? tL.Segments[0] : lx / 4;
            sct.Polyline(Lay.Thep, new List<(double, double)> { (-bw / 2, -c - (t - 2 * c)), (-bw / 2, -c), (-bw / 2 + ext, -c), (-bw / 2 + ext, -c - 60) }, false);
            sct.Polyline(Lay.Thep, new List<(double, double)> { (lx + bw / 2, -c - (t - 2 * c)), (lx + bw / 2, -c), (lx + bw / 2 - ext, -c), (lx + bw / 2 - ext, -c - 60) }, false);
            sct.LeaderTag(ext * 0.6, -c, ext * 0.6 + 1.6 * sct.Th, 500, Dt.Spaced(tL.Dia, tL.Spacing), tL.Mark);
        }
        sct.Dim(0, -t, lx, -t, -bh - 1.5 * sct.Th);
        sct.Dim(lx + bw, -t, lx + bw, 0, 2.0 * sct.Th);
        sct.Title(lx / 2, -bh - 1000, "1-1", DrawUtil.ScaleText(Ss), true);
        figs.Add(new Figure(sct, "Mặt cắt sàn " + Name));
        return figs;
    }
}

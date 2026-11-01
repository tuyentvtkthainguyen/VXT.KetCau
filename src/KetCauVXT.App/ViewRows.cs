using System.ComponentModel;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.App;

// =====================================================================
// CÁC LỚP DÒNG HIỂN THỊ CHO DataGrid
// – Dòng sửa được: bọc đối tượng của Project, ghi thẳng vào mô hình (kích thước hiển thị mm).
// – Dòng kết quả: chỉ đọc, đã làm tròn.
// Tiêu đề cột lấy từ [DisplayName]; [Browsable(false)] = ẩn cột.
// =====================================================================

public class SectionRow
{
    private readonly Project _p;
    [Browsable(false)] public FrameSection Sec { get; }
    public SectionRow(Project p, FrameSection s) { _p = p; Sec = s; }

    private static double Mm(double m) => Math.Round(m * 1000, 1);
    private static double M(double mm) => mm / 1000.0;

    [DisplayName("Tên")]
    public string Name
    {
        get => Sec.Name;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value == Sec.Name) return;
            if (_p.FrameSections.Any(s => s != Sec && s.Name == value)) return; // trùng tên – bỏ qua
            foreach (var f in _p.Frames.Where(f_ => f_.Section == Sec.Name)) f.Section = value;
            Sec.Name = value;
            _p.InvalidateIndex();
        }
    }

    [DisplayName("Hình dạng")]
    public SectionShape Shape
    {
        get => Sec.Shape;
        set
        {
            Sec.Shape = value;
            if (value == SectionShape.WeldedI && Sec.HStart <= 0)
            {
                Sec.HStart = Sec.HEnd = Math.Max(Sec.H, 0.3); Sec.BfTop = Sec.BfBot = 0.2;
                Sec.TfTop = Sec.TfBot = 0.01; Sec.Tw = 0.006;
            }
            if (value == SectionShape.RectConcrete && Sec.B <= 0) { Sec.B = 0.22; Sec.H = 0.3; }
        }
    }

    [DisplayName("Vật liệu")] public string Material { get => Sec.Material; set { if (!string.IsNullOrWhiteSpace(value)) Sec.Material = value.Trim(); } }
    [DisplayName("b (mm)")] public double B { get => Mm(Sec.B); set => Sec.B = M(value); }
    [DisplayName("h (mm)")] public double H { get => Mm(Sec.H); set => Sec.H = M(value); }
    [DisplayName("I: h1 (mm)")] public double H1 { get => Mm(Sec.HStart); set => Sec.HStart = M(value); }
    [DisplayName("I: h2 (mm)")] public double H2 { get => Mm(Sec.HEnd); set => Sec.HEnd = M(value); }
    [DisplayName("I: bf (mm)")] public double Bf { get => Mm(Sec.BfTop); set { Sec.BfTop = M(value); Sec.BfBot = M(value); } }
    [DisplayName("I: tf (mm)")] public double Tf { get => Mm(Sec.TfTop); set { Sec.TfTop = M(value); Sec.TfBot = M(value); } }
    [DisplayName("I: tw (mm)")] public double Tw { get => Mm(Sec.Tw); set => Sec.Tw = M(value); }
    [DisplayName("Số thanh dùng")] public int UsedBy => _p.Frames.Count(f => f.Section == Sec.Name);
}

public class SlabSectionRow
{
    private readonly Project _p;
    [Browsable(false)] public SlabSection Sec { get; }
    public SlabSectionRow(Project p, SlabSection s) { _p = p; Sec = s; }

    [DisplayName("Tên")]
    public string Name
    {
        get => Sec.Name;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value == Sec.Name || _p.SlabSections.Any(s => s != Sec && s.Name == value)) return;
            foreach (var s in _p.Slabs.Where(s_ => s_.Section == Sec.Name)) s.Section = value;
            Sec.Name = value;
        }
    }
    [DisplayName("Vật liệu")] public string Material { get => Sec.Material; set { if (!string.IsNullOrWhiteSpace(value)) Sec.Material = value.Trim(); } }
    [DisplayName("Chiều dày (mm)")] public double T { get => Math.Round(Sec.Thickness * 1000, 1); set => Sec.Thickness = value / 1000.0; }
    [DisplayName("Số ô sàn dùng")] public int UsedBy => _p.Slabs.Count(s => s.Section == Sec.Name);
}

public class PatternRow
{
    private readonly Project _p;
    [Browsable(false)] public LoadPattern Pat { get; }
    public PatternRow(Project p, LoadPattern pat) { _p = p; Pat = pat; }

    [DisplayName("Tên")]
    public string Name
    {
        get => Pat.Name;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value == Pat.Name || _p.Patterns.Any(x => x != Pat && x.Name == value)) return;
            string old = Pat.Name;
            foreach (var l in _p.FrameLoads.Where(l_ => l_.Pattern == old)) l.Pattern = value;
            foreach (var l in _p.NodeLoads.Where(l_ => l_.Pattern == old)) l.Pattern = value;
            foreach (var l in _p.AreaLoads.Where(l_ => l_.Pattern == old)) l.Pattern = value;
            foreach (var c in _p.Combinations) foreach (var it in c.Items.Where(i => i.Case == old)) it.Case = value;
            Pat.Name = value;
        }
    }
    [DisplayName("Loại")] public LoadType Type { get => Pat.Type; set => Pat.Type = value; }
    [DisplayName("Hệ số TLBT")] public double SelfWeight { get => Pat.SelfWeight; set => Pat.SelfWeight = value; }
    [DisplayName("Hệ số vượt tải γf")] public double Gamma { get => Pat.Gamma; set => Pat.Gamma = value; }
}

public class ComboRow
{
    [DisplayName("Tên")] public string Name { get; set; } = "";
    [DisplayName("Loại")] public string Type { get; set; } = "";
    [DisplayName("Thành phần")] public string Description { get; set; } = "";

    public static ComboRow From(LoadCombination c) => new()
    {
        Name = c.Name,
        Type = c.Type switch { ComboType.ULS => "TTGH I", ComboType.SLS => "TTGH II", _ => "Bao" },
        Description = ComboGenerator.Describe(c)
    };
}

public class FrameLoadRow
{
    private readonly Project _p;
    [Browsable(false)] public FrameLoad Load { get; }
    public FrameLoadRow(Project p, FrameLoad l) { _p = p; Load = l; }

    [DisplayName("Thanh")] public int Element => Load.Element;
    [DisplayName("Tên")] public string Label => _p.Frames.FirstOrDefault(f => f.Id == Load.Element)?.Label ?? "?";
    [DisplayName("Trường hợp")] public string Pattern { get => Load.Pattern; set { if (!string.IsNullOrWhiteSpace(value)) Load.Pattern = value.Trim(); } }
    [DisplayName("Phương")] public LoadDir Dir { get => Load.Dir; set => Load.Dir = value; }

    [DisplayName("q đầu (kN/m)")]
    public double Q1
    {
        get => Load.Points.Count > 0 ? Math.Round(Load.Points[0][1], 3) : 0;
        set => SetUniformish(value, Q2);
    }

    [DisplayName("q cuối (kN/m)")]
    public double Q2
    {
        get => Load.Points.Count > 0 ? Math.Round(Load.Points[^1][1], 3) : 0;
        set => SetUniformish(Q1, value);
    }

    [DisplayName("Số điểm")] public int NPoints => Load.Points.Count;
    [DisplayName("Ghi chú")] public string Note { get => Load.Note; set => Load.Note = value ?? ""; }
    [DisplayName("Nguồn")] public string Source => Load.Source == "" ? "Nhập tay" : Load.Source;

    private void SetUniformish(double q1, double q2)
    {
        Load.Points = new List<double[]> { new[] { 0.0, q1 }, new[] { 1.0, q2 } };
    }
}

public class AreaLoadRow
{
    private readonly Project _p;
    [Browsable(false)] public AreaLoad Load { get; }
    public AreaLoadRow(Project p, AreaLoad l) { _p = p; Load = l; }

    [DisplayName("Ô sàn")] public int Slab => Load.Slab;
    [DisplayName("Tên")] public string Label => _p.Slabs.FirstOrDefault(s => s.Id == Load.Slab)?.Label ?? "?";
    [DisplayName("Tầng")] public string Story => _p.Slabs.FirstOrDefault(s => s.Id == Load.Slab)?.Story ?? "";
    [DisplayName("Trường hợp")] public string Pattern { get => Load.Pattern; set { if (!string.IsNullOrWhiteSpace(value)) Load.Pattern = value.Trim(); } }
    [DisplayName("q (kN/m²)")] public double Q { get => Load.Q; set => Load.Q = value; }
    [DisplayName("Ghi chú")] public string Note { get => Load.Note; set => Load.Note = value ?? ""; }
}

// ---------------------------------------------------------------------
// KẾT QUẢ
// ---------------------------------------------------------------------
public class ForceRow
{
    [DisplayName("Thanh")] public int Frame { get; set; }
    [DisplayName("Tên")] public string Label { get; set; } = "";
    [DisplayName("Tầng")] public string Story { get; set; } = "";
    [DisplayName("Giá trị")] public string Kind { get; set; } = "";
    [DisplayName("Vị trí (m)")] public double Station { get; set; }
    [DisplayName("P (kN)")] public double P { get; set; }
    [DisplayName("V2 (kN)")] public double V2 { get; set; }
    [DisplayName("V3 (kN)")] public double V3 { get; set; }
    [DisplayName("T (kN.m)")] public double T { get; set; }
    [DisplayName("M2 (kN.m)")] public double M2 { get; set; }
    [DisplayName("M3 (kN.m)")] public double M3 { get; set; }

    public static ForceRow Make(FrameElement f, string kind, double st, double[] v) => new()
    {
        Frame = f.Id, Label = f.Label, Story = f.Story, Kind = kind, Station = Math.Round(st, 3),
        P = R(v[0]), V2 = R(v[1]), V3 = R(v[2]), T = R(v[3]), M2 = R(v[4]), M3 = R(v[5])
    };

    public static double R(double x) => Math.Round(x, 2);
}

public class ReactionRow
{
    [DisplayName("Nút")] public int Node { get; set; }
    [DisplayName("X (m)")] public double X { get; set; }
    [DisplayName("Y (m)")] public double Y { get; set; }
    [DisplayName("Giá trị")] public string Kind { get; set; } = "";
    [DisplayName("FX")] public double FX { get; set; }
    [DisplayName("FY")] public double FY { get; set; }
    [DisplayName("FZ")] public double FZ { get; set; }
    [DisplayName("MX")] public double MX { get; set; }
    [DisplayName("MY")] public double MY { get; set; }
    [DisplayName("MZ")] public double MZ { get; set; }

    public static ReactionRow Make(Node n, string kind, double[] v) => new()
    {
        Node = n.Id, X = Math.Round(n.X, 3), Y = Math.Round(n.Y, 3), Kind = kind,
        FX = ForceRow.R(v[0]), FY = ForceRow.R(v[1]), FZ = ForceRow.R(v[2]),
        MX = ForceRow.R(v[3]), MY = ForceRow.R(v[4]), MZ = ForceRow.R(v[5])
    };
}

// ---------------------------------------------------------------------
// THIẾT KẾ
// ---------------------------------------------------------------------
public abstract class DesignRowBase
{
    [Browsable(false)] public bool IsOk { get; set; } = true;
    [DisplayName("Kết quả")] public string Result => IsOk ? "OK" : "KHÔNG ĐẠT";
}

public class BeamDesignRow : DesignRowBase
{
    [DisplayName("Dầm")] public string Name { get; set; } = "";
    [DisplayName("Tầng")] public string Story { get; set; } = "";
    [DisplayName("Tiết diện")] public string Section { get; set; } = "";
    [DisplayName("Nhịp")] public string Span { get; set; } = "";
    [DisplayName("L (m)")] public double L { get; set; }
    [DisplayName("M gối trái")] public double MLeft { get; set; }
    [DisplayName("M nhịp")] public double MSpan { get; set; }
    [DisplayName("M gối phải")] public double MRight { get; set; }
    [DisplayName("Qmax (kN)")] public double Q { get; set; }
    [DisplayName("Thép trên trái")] public string TopLeft { get; set; } = "";
    [DisplayName("Thép dưới")] public string Bot { get; set; } = "";
    [DisplayName("Thép trên phải")] public string TopRight { get; set; } = "";
    [DisplayName("Đai")] public string Stirrup { get; set; } = "";
    [DisplayName("Võng (mm)")] public double Defl { get; set; }
    [DisplayName("[f] (mm)")] public double DeflLim { get; set; }
    [DisplayName("Tỷ số max")] public double Ratio { get; set; }

    public static IEnumerable<BeamDesignRow> From(BeamLineDesign b)
    {
        for (int i = 0; i < b.Spans.Count; i++)
        {
            var s = b.Spans[i];
            yield return new BeamDesignRow
            {
                Name = b.Name, Story = b.Story, Section = b.Section, Span = $"{i + 1}: {s.LeftSupport}–{s.RightSupport}",
                L = Math.Round(s.L, 3), MLeft = ForceRow.R(s.MLeft), MSpan = ForceRow.R(s.MSpan), MRight = ForceRow.R(s.MRight),
                Q = ForceRow.R(s.QMax), TopLeft = s.TopLeftText, Bot = s.BotText, TopRight = s.TopRightText,
                Stirrup = s.StirrupDia > 0 ? $"Ø{s.StirrupDia}a{s.StirrupSupport * 1000:0}/a{s.StirrupMid * 1000:0}" : "",
                Defl = Math.Round(s.Deflection * 1000, 1), DeflLim = Math.Round(s.DeflectionLimit * 1000, 1),
                Ratio = Math.Round(b.MaxRatio, 3),
                IsOk = b.Ok && (s.DeflectionLimit <= 0 || s.Deflection <= s.DeflectionLimit + 1e-9)
            };
        }
        if (b.Spans.Count == 0)
            yield return new BeamDesignRow { Name = b.Name, Story = b.Story, Section = b.Section, Ratio = Math.Round(b.MaxRatio, 3), IsOk = b.Ok };
    }
}

public class ColumnDesignRow : DesignRowBase
{
    [DisplayName("Cột")] public string Name { get; set; } = "";
    [DisplayName("Trục")] public string Grid { get; set; } = "";
    [DisplayName("Tầng")] public string Story { get; set; } = "";
    [DisplayName("b×h (mm)")] public string Size { get; set; } = "";
    [DisplayName("N (kN)")] public double N { get; set; }
    [DisplayName("M2 (kN.m)")] public double M2 { get; set; }
    [DisplayName("M3 (kN.m)")] public double M3 { get; set; }
    [DisplayName("Tổ hợp")] public string Combo { get; set; } = "";
    [DisplayName("As yc (cm²)")] public double AsReq { get; set; }
    [DisplayName("As chọn (cm²)")] public double AsProv { get; set; }
    [DisplayName("Thép dọc")] public string Bars { get; set; } = "";
    [DisplayName("μ (%)")] public double Mu { get; set; }
    [DisplayName("Đai")] public string Stirrup { get; set; } = "";
    [DisplayName("Tỷ số")] public double Util { get; set; }

    public static IEnumerable<ColumnDesignRow> From(ColumnDesign c)
    {
        foreach (var s in c.Stories)
            yield return new ColumnDesignRow
            {
                Name = c.Name, Grid = c.GridName, Story = s.Story, Size = $"{s.B * 1000:0}×{s.H * 1000:0}",
                N = ForceRow.R(s.N), M2 = ForceRow.R(s.M2), M3 = ForceRow.R(s.M3), Combo = s.Combo,
                AsReq = Math.Round(s.AsReq * 1e4, 2), AsProv = Math.Round(s.AsProv * 1e4, 2),
                Bars = s.Dia > 0 ? $"{s.TotalBars}Ø{s.Dia}" : "", Mu = Math.Round(s.Ratio, 2),
                Stirrup = s.StirrupDia > 0 ? $"Ø{s.StirrupDia}a{s.StirrupSpacingEnd * 1000:0}/a{s.StirrupSpacing * 1000:0}" : "",
                Util = Math.Round(s.Utilization, 3), IsOk = c.Ok && s.Utilization <= 1.0 + 1e-6
            };
        if (c.Stories.Count == 0) yield return new ColumnDesignRow { Name = c.Name, Grid = c.GridName, IsOk = c.Ok };
    }
}

public class SlabDesignRow : DesignRowBase
{
    [DisplayName("Ô sàn")] public string Name { get; set; } = "";
    [DisplayName("Tầng")] public string Story { get; set; } = "";
    [DisplayName("Lx×Ly (m)")] public string Size { get; set; } = "";
    [DisplayName("hs (mm)")] public double T { get; set; }
    [DisplayName("Loại")] public string Kind { get; set; } = "";
    [DisplayName("Biên")] public string Edge { get; set; } = "";
    [DisplayName("q (kN/m²)")] public double Q { get; set; }
    [DisplayName("M1")] public double M1 { get; set; }
    [DisplayName("M2")] public double M2 { get; set; }
    [DisplayName("MI")] public double MI { get; set; }
    [DisplayName("MII")] public double MII { get; set; }
    [DisplayName("Dưới X")] public string BotX { get; set; } = "";
    [DisplayName("Dưới Y")] public string BotY { get; set; } = "";
    [DisplayName("Mũ X")] public string TopX { get; set; } = "";
    [DisplayName("Mũ Y")] public string TopY { get; set; } = "";
    [DisplayName("Tỷ số")] public double Ratio { get; set; }

    public static SlabDesignRow From(SlabDesign s) => new()
    {
        Name = s.Name, Story = s.Story, Size = $"{s.Lx:0.00}×{s.Ly:0.00}", T = Math.Round(s.Thickness * 1000),
        Kind = s.TwoWay ? "Bản kê 4 cạnh" : "Bản dầm", Edge = s.EdgeCondition, Q = Math.Round(s.Q, 2),
        M1 = ForceRow.R(s.M1), M2 = ForceRow.R(s.M2), MI = ForceRow.R(s.MI), MII = ForceRow.R(s.MII),
        BotX = s.BotX, BotY = s.BotY, TopX = s.TopX, TopY = s.TopY, Ratio = Math.Round(s.MaxRatio, 3), IsOk = s.Ok
    };
}

public class FootingDesignRow : DesignRowBase
{
    [DisplayName("Móng")] public string Name { get; set; } = "";
    [DisplayName("Nút")] public int Node { get; set; }
    [DisplayName("L×B×H (m)")] public string Size { get; set; } = "";
    [DisplayName("N (kN)")] public double N { get; set; }
    [DisplayName("Mx")] public double Mx { get; set; }
    [DisplayName("My")] public double My { get; set; }
    [DisplayName("R (kN/m²)")] public double R { get; set; }
    [DisplayName("Pmax")] public double Pmax { get; set; }
    [DisplayName("Ptb")] public double Ptb { get; set; }
    [DisplayName("Thép L")] public string BarsL { get; set; } = "";
    [DisplayName("Thép B")] public string BarsB { get; set; } = "";
    [DisplayName("Tỷ số")] public double Ratio { get; set; }

    public static FootingDesignRow From(FootingDesign f) => new()
    {
        Name = f.Name, Node = f.NodeId, Size = $"{f.L:0.00}×{f.B:0.00}×{f.H:0.00}", N = ForceRow.R(f.N),
        Mx = ForceRow.R(f.Mx), My = ForceRow.R(f.My), R = Math.Round(f.R, 1), Pmax = Math.Round(f.Pmax, 1), Ptb = Math.Round(f.Ptb, 1),
        BarsL = f.BarsL, BarsB = f.BarsB, Ratio = Math.Round(f.MaxRatio, 3), IsOk = f.Ok
    };
}

public class SteelCheckRow : DesignRowBase
{
    [DisplayName("Cấu kiện")] public string Name { get; set; } = "";
    [DisplayName("Thanh")] public int Frame { get; set; }
    [DisplayName("Tiết diện")] public string Section { get; set; } = "";
    [DisplayName("Tổ hợp")] public string Combo { get; set; } = "";
    [DisplayName("N (kN)")] public double N { get; set; }
    [DisplayName("M (kN.m)")] public double M { get; set; }
    [DisplayName("V (kN)")] public double V { get; set; }
    [DisplayName("λ")] public double Lambda { get; set; }
    [DisplayName("Võng (mm)")] public double Defl { get; set; }
    [DisplayName("[f] (mm)")] public double DeflLim { get; set; }
    [DisplayName("Tỷ số max")] public double Ratio { get; set; }
    [DisplayName("Kiểm tra quyết định")] public string Governing { get; set; } = "";

    public static SteelCheckRow From(SteelMemberCheck s)
    {
        var gov = s.Checks.OrderByDescending(c => c.Ratio).FirstOrDefault();
        return new SteelCheckRow
        {
            Name = s.Name, Frame = s.Frame, Section = s.Section, Combo = s.Combo, N = ForceRow.R(s.N), M = ForceRow.R(s.M),
            V = ForceRow.R(s.V), Lambda = Math.Round(s.Lambda, 1), Defl = Math.Round(s.Deflection * 1000, 1),
            DeflLim = Math.Round(s.DeflectionLimit * 1000, 1), Ratio = Math.Round(s.MaxRatio, 3),
            Governing = gov != null ? gov.Name : "", IsOk = s.Ok
        };
    }
}

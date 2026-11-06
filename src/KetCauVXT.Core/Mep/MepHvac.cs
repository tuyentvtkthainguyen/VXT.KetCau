using System.Text.Json.Serialization;
using KetCauVXT.Drawing;
using KetCauVXT.Import;
using KetCauVXT.Model;

namespace KetCauVXT.Mep;

// =====================================================================
// ĐIỀU HOÀ – THÔNG GIÓ (ĐHKK) CƠ BẢN – phần mở rộng của bộ môn Điện nước (Project.Mep.Hvac).
// • Ống gió (chữ nhật w×h hoặc tròn Ø, mm) theo hệ: gió cấp / gió hồi / gió thải / gió tươi; tuyến là đường gấp khúc 3D (m).
// • Miệng gió (cấp / hồi), dàn lạnh (FCU / treo tường / âm trần cassette), dàn nóng.
// • Ống đồng gas (cặp lỏng / gas theo công suất dàn lạnh) + ống nước ngưng (uPVC, có độ dốc ≥ 1 %).
// • Phụ kiện ống gió tự sinh: cút (đổi hướng), tê (đầu ống nằm trên thân ống khác), côn (hai ống nối đầu khác tiết diện).
// • Chọn cỡ ống gió: A = Q / (3600·v); tròn → Ø chuẩn ≥ √(4A/π); chữ nhật → h chuẩn, w bội 50 mm, tỷ lệ w/h ≤ MaxAspect.
// • Khối lượng: tôn ống gió m² = chu vi × chiều dài (tròn πD·L, chữ nhật 2(w+h)·L), độ dày tôn theo cạnh lớn;
//   ống đồng m (cặp), ống nước ngưng m, quang treo, thiết bị, miệng gió, phụ kiện.
// • Va chạm ống gió ↔ dầm (cao độ đỉnh / đáy ống so với đáy dầm).
// • Mặt bằng ĐHKK: ống gió 2 nét + tim, ký hiệu miệng gió / dàn lạnh / dàn nóng, ống gas, ống ngưng có độ dốc, bảng ký hiệu.
// Dữ liệu cũ (không có Hvac) → mô hình rỗng; enum lưu dạng chuỗi.
// =====================================================================

/// <summary>Hệ ống gió.</summary>
public enum HvacDuctSystem { GioCap, GioHoi, GioThai, GioTuoi }

/// <summary>Loại thiết bị ĐHKK.</summary>
public enum HvacUnitKind { DanLanh, DanNong }

/// <summary>Loại ống phục vụ điều hoà: ống đồng gas (cặp lỏng / gas) hoặc ống nước ngưng.</summary>
public enum HvacPipeKind { OngDong, NuocNgung }

/// <summary>Loại phụ kiện ống gió.</summary>
public enum HvacFittingKind { Cut, Te, Con }

/// <summary>Đoạn / tuyến ống gió.</summary>
public class HvacDuct
{
    public int Id { get; set; }
    public string Story { get; set; } = "";
    public HvacDuctSystem System { get; set; } = HvacDuctSystem.GioCap;
    /// <summary>true = ống tròn (Dia); false = chữ nhật W×H (mm)</summary>
    public bool Round { get; set; }
    public double W { get; set; } = 300;
    public double H { get; set; } = 200;
    public double Dia { get; set; } = 200;
    /// <summary>Lưu lượng thiết kế (m³/h); 0 = không khai báo</summary>
    public double Flow { get; set; }
    /// <summary>Đường tim ống (m, cao độ tuyệt đối)</summary>
    public List<MepPt> Path { get; set; } = new();
    public string ExternalId { get; set; }
    [JsonIgnore] public double Length => MepGeom.Length(Path);
    /// <summary>Chu vi tiết diện (m).</summary>
    [JsonIgnore] public double Perimeter => Round ? Math.PI * Dia / 1000 : 2 * (W + H) / 1000;
    /// <summary>Diện tích tôn (m²) = chu vi × chiều dài.</summary>
    [JsonIgnore] public double SheetArea => Perimeter * Length;
    /// <summary>Kích thước lớn nhất của tiết diện (mm) – chọn độ dày tôn.</summary>
    [JsonIgnore] public double MaxSide => Round ? Dia : Math.Max(W, H);
    /// <summary>Chiều cao tiết diện (mm) – kiểm tra va chạm.</summary>
    [JsonIgnore] public double Depth => Round ? Dia : H;
    [JsonIgnore] public double Width => Round ? Dia : W;
    [JsonIgnore] public string SizeText => Round ? $"Ø{Dia:0}" : $"{W:0}x{H:0}";
}

/// <summary>Miệng gió.</summary>
public class HvacTerminal
{
    public int Id { get; set; }
    public string Story { get; set; } = "";
    public string Name { get; set; } = "Miệng gió cấp 600x600";
    /// <summary>true = miệng cấp; false = miệng hồi / hút</summary>
    public bool Supply { get; set; } = true;
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>Cao độ so với sàn tầng (m)</summary>
    public double Height { get; set; } = 2.8;
    public double W { get; set; } = 600;
    public double H { get; set; } = 600;
    public double Flow { get; set; }
    public string ExternalId { get; set; }
}

/// <summary>Dàn lạnh / dàn nóng.</summary>
public class HvacUnit
{
    public int Id { get; set; }
    public string Story { get; set; } = "";
    public HvacUnitKind Kind { get; set; } = HvacUnitKind.DanLanh;
    public string Name { get; set; } = "Dàn lạnh treo tường 12000 BTU";
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>Cao độ tâm so với sàn tầng (m)</summary>
    public double Height { get; set; } = 2.4;
    /// <summary>Công suất lạnh (BTU/h)</summary>
    public double CapacityBtu { get; set; } = 12000;
    /// <summary>Kích thước (m)</summary>
    public double W { get; set; } = 0.9;
    public double D { get; set; } = 0.25;
    public double Hh { get; set; } = 0.3;
    /// <summary>Hướng trục dọc (độ)</summary>
    public double Angle { get; set; }
    public string ExternalId { get; set; }
}

/// <summary>Ống đồng gas (cặp) hoặc ống nước ngưng.</summary>
public class HvacPipe
{
    public int Id { get; set; }
    public string Story { get; set; } = "";
    public HvacPipeKind Kind { get; set; } = HvacPipeKind.OngDong;
    public int FromUnit { get; set; }
    public int ToUnit { get; set; }
    /// <summary>Ống đồng: Ø lỏng / Ø gas (mm)</summary>
    public double LiquidDia { get; set; } = 6.35;
    public double GasDia { get; set; } = 9.52;
    /// <summary>Ống nước ngưng: Ø (mm)</summary>
    public double Dia { get; set; } = 21;
    /// <summary>Độ dốc ống nước ngưng (0,01 = 1 %)</summary>
    public double Slope { get; set; }
    public List<MepPt> Path { get; set; } = new();
    public string ExternalId { get; set; }
    [JsonIgnore] public double Length => MepGeom.Length(Path);
    [JsonIgnore] public string SizeText => Kind == HvacPipeKind.OngDong
        ? $"Ø{HvacSizing.Mm(LiquidDia)}/Ø{HvacSizing.Mm(GasDia)}" : $"Ø{Dia:0}";
}

/// <summary>Phụ kiện ống gió (tự sinh).</summary>
public class HvacFitting
{
    public HvacFittingKind Kind { get; set; }
    public int DuctId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>Cài đặt ĐHKK.</summary>
public class HvacSettings
{
    /// <summary>Vận tốc gió ống chính (m/s)</summary>
    public double VelocityMain { get; set; } = 6;
    /// <summary>Vận tốc gió ống nhánh (m/s)</summary>
    public double VelocityBranch { get; set; } = 4;
    /// <summary>Tỷ lệ cạnh w/h lớn nhất ống chữ nhật</summary>
    public double MaxAspect { get; set; } = 4;
    /// <summary>Khe từ đáy sàn trên tới đỉnh ống gió (m)</summary>
    public double DuctGap { get; set; } = 0.1;
    /// <summary>Độ dốc ống nước ngưng (≥ 1 %)</summary>
    public double CondensateSlope { get; set; } = 0.01;
    /// <summary>Ø ống nước ngưng (mm)</summary>
    public double CondensateDia { get; set; } = 21;
    /// <summary>Khoảng cách giá treo ống gió (m)</summary>
    public double DuctHangerSpacing { get; set; } = 2.4;
    /// <summary>Chiều cao thông thủy tối thiểu dưới đáy ống gió so với sàn (m) – giới hạn khi hạ ống qua dầm</summary>
    public double MinClearHeight { get; set; } = 2.4;
    /// <summary>Khe hở đáy dầm – đỉnh ống gió khi hạ ống qua dầm (m)</summary>
    public double BeamClearance { get; set; } = 0.05;
    /// <summary>Tự hạ ống gió qua dầm (2 cút 45° mỗi phía) khi vẽ ống trên mặt bằng</summary>
    public bool AutoDropUnderBeams { get; set; } = true;
    /// <summary>Cách lắp dàn nóng: "Đặt sàn / ban công" (cao độ 0,4 m) hoặc "Giá treo tường" (cao độ OutdoorBracketHeight)</summary>
    public string OutdoorMount { get; set; } = HvacMount.Floor;
    /// <summary>Cao độ tâm dàn nóng lắp giá treo tường so với sàn (m)</summary>
    public double OutdoorBracketHeight { get; set; } = 2.2;
    /// <summary>Phương pháp tải lạnh: "Ước lượng nhanh" (theo diện tích) hoặc "Chi tiết (CLTD)"</summary>
    public string CoolingMethod { get; set; } = HvacMount.QuickMethod;
    /// <summary>Bước lưới dò tuyến ống gió tránh cột / tường (m)</summary>
    public double RouteStep { get; set; } = 0.1;
    /// <summary>Phạt mỗi lần đổi hướng khi dò tuyến (m tương đương)</summary>
    public double BendPenalty { get; set; } = 1.0;
    /// <summary>Phạt mỗi lần cắt qua dầm (phải hạ ống) (m tương đương)</summary>
    public double BeamPenalty { get; set; } = 2.0;
    /// <summary>Phạt mỗi lần xuyên tường (m tương đương)</summary>
    public double WallPenalty { get; set; } = 3.0;
}

/// <summary>Hằng tên lựa chọn ĐHKK (lưu dạng chuỗi).</summary>
public static class HvacMount
{
    public const string Floor = "Đặt sàn / ban công";
    public const string Bracket = "Giá treo tường";
    public const string QuickMethod = "Ước lượng nhanh";
    public const string DetailMethod = "Chi tiết (CLTD)";
    public const double FloorHeight = 0.4;
}

/// <summary>Dữ liệu điều hoà – thông gió (MepModel.Hvac).</summary>
public class HvacModel
{
    public List<HvacDuct> Ducts { get; set; } = new();
    public List<HvacTerminal> Terminals { get; set; } = new();
    public List<HvacUnit> Units { get; set; } = new();
    public List<HvacPipe> Pipes { get; set; } = new();
    public HvacSettings Settings { get; set; } = new();
    [JsonIgnore] public List<HvacFitting> Fittings { get; set; } = new();
    [JsonIgnore] public bool IsEmpty => (Ducts?.Count ?? 0) == 0 && (Terminals?.Count ?? 0) == 0 && (Units?.Count ?? 0) == 0 && (Pipes?.Count ?? 0) == 0;
    public int NextId() => 1 + Math.Max(Math.Max(Ducts.Count == 0 ? 0 : Ducts.Max(d => d.Id), Terminals.Count == 0 ? 0 : Terminals.Max(d => d.Id)),
        Math.Max(Units.Count == 0 ? 0 : Units.Max(d => d.Id), Pipes.Count == 0 ? 0 : Pipes.Max(d => d.Id)));
    public HvacUnit Unit(int id) => Units.FirstOrDefault(u => u.Id == id);
    public HvacDuct Duct(int id) => Ducts.FirstOrDefault(u => u.Id == id);
}

/// <summary>Chọn cỡ ống gió, ống đồng.</summary>
public static class HvacSizing
{
    /// <summary>Ø ống gió tròn chuẩn (mm).</summary>
    public static readonly double[] RoundSizes = { 100, 125, 150, 160, 200, 250, 300, 315, 350, 400, 450, 500, 560, 630, 710, 800, 900, 1000, 1120, 1250 };
    /// <summary>Chiều cao ống gió chữ nhật chuẩn (mm).</summary>
    public static readonly double[] RectHeights = { 150, 200, 250, 300, 350, 400, 500, 600, 800, 1000 };

    /// <summary>Tiết diện cần (m²): A = Q / (3600·v), Q m³/h, v m/s.</summary>
    public static double Area(double flow, double v) => flow / 3600.0 / Math.Max(v, 1e-9);
    /// <summary>Vận tốc thực (m/s) trong ống chữ nhật w×h (mm).</summary>
    public static double Velocity(double flow, double w, double h) => flow / 3600.0 / Math.Max(w * h / 1e6, 1e-12);
    /// <summary>Vận tốc thực (m/s) trong ống tròn Ø (mm).</summary>
    public static double VelocityRound(double flow, double d) => flow / 3600.0 / Math.Max(Math.PI * d * d / 4e6, 1e-12);

    /// <summary>Ø chuẩn nhỏ nhất đạt vận tốc ≤ v.</summary>
    public static double RoundDia(double flow, double v)
    {
        double dmin = Math.Sqrt(4 * Area(flow, v) / Math.PI) * 1000;
        foreach (var d in RoundSizes) if (d >= dmin - 1e-6) return d;
        return Math.Ceiling(dmin / 50) * 50;
    }

    /// <summary>Ống chữ nhật: h cho trước (h &gt; 0) hoặc chiều cao chuẩn nhỏ nhất thoả w/h ≤ maxAspect; w làm tròn lên bội 50 mm.</summary>
    public static (double W, double H) RectSize(double flow, double v, double h = 0, double maxAspect = 4)
    {
        double a = Area(flow, v) * 1e6; // mm²
        if (h > 0) return (Math.Max(100, Math.Ceiling(a / h / 50 - 1e-9) * 50), h);
        foreach (var hh in RectHeights)
        {
            double w = Math.Max(hh, Math.Ceiling(a / hh / 50 - 1e-9) * 50);
            if (w / hh <= maxAspect + 1e-9) return (w, hh);
        }
        double hl = RectHeights[^1];
        return (Math.Ceiling(a / hl / 50 - 1e-9) * 50, hl);
    }

    /// <summary>Đường kính tương đương ống chữ nhật (Huebscher): De = 1,30·(wh)^0,625 / (w+h)^0,25.</summary>
    public static double EquivalentDia(double w, double h) => 1.30 * Math.Pow(w * h, 0.625) / Math.Pow(w + h, 0.25);

    /// <summary>Độ dày tôn mạ kẽm (mm) theo cạnh lớn (thực hành SMACNA / TCVN 5687): ≤ 300: 0,5; ≤ 750: 0,75; ≤ 1500: 1,0; còn lại 1,2.</summary>
    public static double SheetThickness(double maxSide) => maxSide <= 300 + 1e-9 ? 0.5 : maxSide <= 750 + 1e-9 ? 0.75 : maxSide <= 1500 + 1e-9 ? 1.0 : 1.2;

    /// <summary>Cặp ống đồng (Ø lỏng, Ø gas, mm) theo công suất dàn lạnh (BTU/h) – thực hành máy treo tường / cassette.</summary>
    public static (double Liquid, double Gas) CopperPair(double btu) =>
        btu <= 12000 + 1e-6 ? (6.35, 9.52) : btu <= 18000 + 1e-6 ? (6.35, 12.7) : btu <= 24000 + 1e-6 ? (6.35, 15.88)
        : btu <= 48000 + 1e-6 ? (9.52, 15.88) : (9.52, 19.05);

    /// <summary>Lưu lượng gió dàn lạnh ước tính (m³/h) ≈ 0,05 m³/h cho mỗi BTU/h (≈ 600 m³/h / 12000 BTU).</summary>
    public static double UnitAirflow(double btu) => Math.Round(btu * 0.05 / 10) * 10;

    /// <summary>Ghi Ø ống đồng (mm) gọn.</summary>
    public static string Mm(double d) => d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    public static string SystemName(HvacDuctSystem s) => s switch
    {
        HvacDuctSystem.GioCap => "Gió cấp",
        HvacDuctSystem.GioHoi => "Gió hồi",
        HvacDuctSystem.GioThai => "Gió thải",
        _ => "Gió tươi"
    };
    public static string SystemColor(HvacDuctSystem s) => s switch
    {
        HvacDuctSystem.GioCap => "#1E88E5",
        HvacDuctSystem.GioHoi => "#43A047",
        HvacDuctSystem.GioThai => "#8D6E63",
        _ => "#00ACC1"
    };
}

/// <summary>Đi tuyến / thêm phần tử ĐHKK.</summary>
public static class HvacRouting
{
    private static HvacModel H(Project p) { p.Mep ??= new MepModel(); p.Mep.Hvac ??= new HvacModel(); return p.Mep.Hvac; }

    /// <summary>Cao độ tim ống gió (tuyệt đối, m): sát đáy sàn tầng trên − khe − nửa chiều cao ống.</summary>
    public static double DuctZ(Project p, string story, double depthMm)
    {
        var set = H(p).Settings;
        double top = MepRouting.StoryElevation(p, story) + MepRouting.StoryHeight(p, story, p.Mep.Settings);
        return top - set.DuctGap - depthMm / 2000.0;
    }

    /// <summary>Đường gấp khúc trực giao (đi theo X rồi theo Y) ở cao độ z.</summary>
    public static List<MepPt> Orthogonal(double x1, double y1, double x2, double y2, double z)
    {
        var pts = new List<MepPt> { new() { X = x1, Y = y1, Z = z } };
        if (Math.Abs(x2 - x1) > 1e-6 && Math.Abs(y2 - y1) > 1e-6) pts.Add(new MepPt { X = x2, Y = y1, Z = z });
        pts.Add(new MepPt { X = x2, Y = y2, Z = z });
        return pts;
    }

    /// <summary>Thêm ống gió từ (x1,y1) tới (x2,y2) (trực giao, sát trần); flow &gt; 0 → tự chọn cỡ theo vận tốc ống chính.</summary>
    public static HvacDuct AddDuct(Project p, string story, double x1, double y1, double x2, double y2, HvacDuctSystem sys = HvacDuctSystem.GioCap,
        double flow = 0, bool round = false, bool branch = false)
    {
        var h = H(p);
        var d = new HvacDuct { Id = h.NextId(), Story = story, System = sys, Round = round, Flow = flow };
        Size(p, d, branch);
        d.Path = Orthogonal(x1, y1, x2, y2, DuctZ(p, story, d.Depth));
        h.Ducts.Add(d);
        return d;
    }

    /// <summary>Thêm ống gió như AddDuct rồi (nếu cài đặt AutoDropUnderBeams) hạ ống qua các dầm va chạm bằng cặp cút 45°.</summary>
    public static HvacDuct AddDuctAvoidBeams(Project p, string story, double x1, double y1, double x2, double y2, out HvacDropResult drop,
        HvacDuctSystem sys = HvacDuctSystem.GioCap, double flow = 0, bool round = false, bool branch = false)
    {
        var d = AddDuct(p, story, x1, y1, x2, y2, sys, flow, round, branch);
        drop = H(p).Settings.AutoDropUnderBeams ? HvacBeamAvoid.DropUnderBeams(p, d) : new HvacDropResult(d.Id, 0, false, new List<MepWarning>());
        return d;
    }

    /// <summary>Chọn lại cỡ ống theo lưu lượng (không đổi nếu Flow = 0).</summary>
    public static void Size(Project p, HvacDuct d, bool branch = false)
    {
        if (d.Flow <= 0) return;
        var set = H(p).Settings;
        double v = branch ? set.VelocityBranch : set.VelocityMain;
        if (d.Round) d.Dia = HvacSizing.RoundDia(d.Flow, v);
        else (d.W, d.H) = HvacSizing.RectSize(d.Flow, v, 0, set.MaxAspect);
    }

    public static HvacUnit AddUnit(Project p, string story, HvacUnitKind kind, double x, double y, double btu = 12000, double height = double.NaN)
    {
        var h = H(p);
        var u = new HvacUnit
        {
            Id = h.NextId(), Story = story, Kind = kind, X = x, Y = y, CapacityBtu = btu,
            Name = kind == HvacUnitKind.DanLanh ? $"Dàn lạnh treo tường {btu:0} BTU" : $"Dàn nóng {btu:0} BTU",
            W = kind == HvacUnitKind.DanLanh ? 0.9 : 0.8, D = kind == HvacUnitKind.DanLanh ? 0.25 : 0.3, Hh = kind == HvacUnitKind.DanLanh ? 0.3 : 0.6,
            Height = double.IsNaN(height) ? (kind == HvacUnitKind.DanLanh ? 2.4 : OutdoorHeight(p)) : height
        };
        h.Units.Add(u);
        return u;
    }

    public static HvacTerminal AddTerminal(Project p, string story, double x, double y, bool supply = true, double flow = 0, double size = 600)
    {
        var h = H(p);
        var t = new HvacTerminal
        {
            Id = h.NextId(), Story = story, X = x, Y = y, Supply = supply, Flow = flow, W = size, H = size,
            Name = (supply ? "Miệng gió cấp " : "Miệng gió hồi ") + $"{size:0}x{size:0}",
            Height = Math.Max(0.1, MepRouting.StoryHeight(p, story, p.Mep.Settings) - 0.3)
        };
        h.Terminals.Add(t);
        return t;
    }

    /// <summary>Cao độ lắp dàn nóng mặc định theo cài đặt (đặt sàn 0,4 m / giá treo tường OutdoorBracketHeight).</summary>
    public static double OutdoorHeight(Project p)
    {
        var set = H(p).Settings;
        return set.OutdoorMount == HvacMount.Bracket ? Math.Max(0.3, set.OutdoorBracketHeight) : HvacMount.FloorHeight;
    }

    /// <summary>Cao độ tuyệt đối tâm thiết bị.</summary>
    public static double UnitZ(Project p, HvacUnit u) => MepRouting.StoryElevation(p, u.Story) + u.Height;

    /// <summary>Ống đồng gas nối dàn lạnh – dàn nóng (trực giao ở cao độ dàn lạnh, rồi đứng xuống / lên dàn nóng); cỡ theo công suất dàn lạnh.</summary>
    public static HvacPipe ConnectRefrigerant(Project p, HvacUnit indoor, HvacUnit outdoor)
    {
        var h = H(p);
        var (liq, gas) = HvacSizing.CopperPair(indoor.CapacityBtu);
        double z1 = UnitZ(p, indoor), z2 = UnitZ(p, outdoor);
        var path = Orthogonal(indoor.X, indoor.Y, outdoor.X, outdoor.Y, z1);
        if (Math.Abs(z2 - z1) > 1e-6) path.Add(new MepPt { X = outdoor.X, Y = outdoor.Y, Z = z2 });
        var pp = new HvacPipe { Id = h.NextId(), Story = indoor.Story, Kind = HvacPipeKind.OngDong, FromUnit = indoor.Id, ToUnit = outdoor.Id, LiquidDia = liq, GasDia = gas, Path = path };
        h.Pipes.Add(pp);
        return pp;
    }

    /// <summary>Đường ống nước ngưng có độ dốc: trực giao từ (x1,y1,z1) tới (x2,y2), cao độ giảm i × chiều dài cộng dồn.</summary>
    public static List<MepPt> SlopedPath(double x1, double y1, double z1, double x2, double y2, double slope)
    {
        var flat = Orthogonal(x1, y1, x2, y2, z1);
        var res = new List<MepPt>();
        double s = 0;
        for (int i = 0; i < flat.Count; i++)
        {
            if (i > 0) s += MepGeom.Dist2D(flat[i - 1].X, flat[i - 1].Y, flat[i].X, flat[i].Y);
            res.Add(new MepPt { X = flat[i].X, Y = flat[i].Y, Z = z1 - slope * s });
        }
        return res;
    }

    /// <summary>Ống nước ngưng từ dàn lạnh tới điểm xả (x, y) với độ dốc cài đặt (tối thiểu 1 %).</summary>
    public static HvacPipe AddCondensate(Project p, HvacUnit indoor, double x, double y, double slope = 0)
    {
        var h = H(p);
        double i = Math.Max(0.01, slope > 0 ? slope : h.Settings.CondensateSlope);
        double z0 = UnitZ(p, indoor) - indoor.Hh / 2;
        var pp = new HvacPipe
        {
            Id = h.NextId(), Story = indoor.Story, Kind = HvacPipeKind.NuocNgung, FromUnit = indoor.Id, Dia = h.Settings.CondensateDia, Slope = i,
            Path = SlopedPath(indoor.X, indoor.Y, z0, x, y, i)
        };
        h.Pipes.Add(pp);
        return pp;
    }

    /// <summary>Độ dốc thực của ống nước ngưng (độ hạ / chiều dài ngang).</summary>
    public static double ActualSlope(HvacPipe pp)
    {
        double lh = 0;
        for (int i = 1; i < pp.Path.Count; i++) lh += MepGeom.Dist2D(pp.Path[i - 1].X, pp.Path[i - 1].Y, pp.Path[i].X, pp.Path[i].Y);
        return lh < 1e-9 || pp.Path.Count < 2 ? 0 : (pp.Path[0].Z - pp.Path[^1].Z) / lh;
    }

    /// <summary>Sinh lại phụ kiện ống gió: cút ở mỗi điểm đổi hướng, tê khi đầu ống nằm trên thân ống khác, côn khi hai đầu ống trùng nhau khác tiết diện.</summary>
    public static List<HvacFitting> RebuildFittings(Project p)
    {
        var h = H(p);
        var res = new List<HvacFitting>();
        const double tol = 0.05;
        foreach (var d in h.Ducts)
        {
            for (int i = 1; i + 1 < d.Path.Count; i++)
            {
                var a = d.Path[i - 1]; var b = d.Path[i]; var c = d.Path[i + 1];
                double ang = MepRouting.TurnAngle(a, b, c);
                if (ang > 10) res.Add(new HvacFitting { Kind = HvacFittingKind.Cut, DuctId = d.Id, X = b.X, Y = b.Y, Z = b.Z,
                    Name = ang < 60 ? $"Cút 45° ống gió {d.SizeText}" : $"Cút ống gió {d.SizeText}" });
            }
        }
        for (int k = 0; k < h.Ducts.Count; k++)
        {
            var d = h.Ducts[k];
            if (d.Path.Count < 2) continue;
            foreach (var e in new[] { d.Path[0], d.Path[^1] })
                foreach (var o in h.Ducts)
                {
                    if (o.Id == d.Id || o.Story != d.Story || o.Path.Count < 2) continue;
                    // đầu chạm đầu: côn nếu khác tiết diện (mỗi cặp ghi 1 lần – Id nhỏ hơn)
                    bool endHit = MepGeom.Dist(e, o.Path[0]) <= tol || MepGeom.Dist(e, o.Path[^1]) <= tol;
                    if (endHit)
                    {
                        if (d.Id < o.Id && Math.Abs(d.Perimeter - o.Perimeter) > 1e-6)
                            res.Add(new HvacFitting { Kind = HvacFittingKind.Con, DuctId = d.Id, X = e.X, Y = e.Y, Z = e.Z, Name = $"Côn ống gió {d.SizeText} – {o.SizeText}" });
                        continue;
                    }
                    for (int i = 1; i < o.Path.Count; i++)
                    {
                        var pr = MepGeom.Project(e.X, e.Y, o.Path[i - 1].X, o.Path[i - 1].Y, o.Path[i].X, o.Path[i].Y);
                        if (pr.Dist <= tol && pr.T > 1e-6 && pr.T < 1 - 1e-6 && Math.Abs(e.Z - o.Path[i].Z) <= 0.5)
                        {
                            res.Add(new HvacFitting { Kind = HvacFittingKind.Te, DuctId = o.Id, X = pr.Px, Y = pr.Py, Z = o.Path[i].Z, Name = $"Tê ống gió {o.SizeText} – nhánh {d.SizeText}" });
                            break;
                        }
                    }
                }
        }
        h.Fittings = res;
        return res;
    }

    /// <summary>Số giá treo ống gió (mỗi DuctHangerSpacing m chiều dài ngang, tối thiểu 2 / đoạn).</summary>
    public static int DuctHangers(Project p, HvacDuct d)
    {
        double sp = Math.Max(0.5, H(p).Settings.DuctHangerSpacing);
        double lh = 0;
        for (int i = 1; i < d.Path.Count; i++) lh += MepGeom.Dist2D(d.Path[i - 1].X, d.Path[i - 1].Y, d.Path[i].X, d.Path[i].Y);
        return lh < 1e-6 ? 0 : Math.Max(2, (int)Math.Ceiling(lh / sp - 1e-9) + 1);
    }
}

/// <summary>Một va chạm ống gió ↔ dầm.</summary>
public sealed record HvacClash(int DuctId, int FrameId, string Story, double X, double Y, double DuctTop, double DuctBottom, double BeamBottom, string Message);

/// <summary>Kiểm tra ống gió đi ngang dưới dầm: phần ống cao hơn đáy dầm tại giao điểm (trên mặt bằng) là va chạm.</summary>
public static class HvacClashCheck
{
    private static (double B, double H) Dims(Project p, FrameElement f)
    {
        var sec = p.FrameSections.FirstOrDefault(s => s.Name == f.Section);
        return (sec?.B > 0 ? sec.B : 0.22, sec?.H > 0 ? sec.H : 0.3);
    }

    public static List<HvacClash> Beams(Project p)
    {
        var res = new List<HvacClash>();
        var h = p?.Mep?.Hvac;
        if (h == null) return res;
        var beams = p.Frames.Where(f => !f.IsVerticalKind).ToList();
        foreach (var d in h.Ducts)
        {
            double half = d.Depth / 2000.0, halfW = d.Width / 2000.0;
            for (int i = 1; i < d.Path.Count; i++)
            {
                var a = d.Path[i - 1]; var c = d.Path[i];
                if (MepGeom.Dist2D(a.X, a.Y, c.X, c.Y) < 1e-6) continue;
                foreach (var f in beams)
                {
                    Node ni, nj;
                    try { ni = p.Node(f.NodeI); nj = p.Node(f.NodeJ); } catch { continue; }
                    if (!MepGeom.SegmentsIntersect(a.X, a.Y, c.X, c.Y, ni.X, ni.Y, nj.X, nj.Y)) continue;
                    var (_, bh) = Dims(p, f);
                    double zt = Math.Max(ni.Z, nj.Z), zb = zt - bh;
                    // cao độ ống tại giao điểm ≈ trung bình (đoạn ngang)
                    double z = (a.Z + c.Z) / 2, top = z + half, bot = z - half;
                    if (top <= zb + 1e-6 || bot >= zt - 1e-6) continue;
                    double dx = c.X - a.X, dy = c.Y - a.Y, ex = nj.X - ni.X, ey = nj.Y - ni.Y;
                    double den = dx * ey - dy * ex;
                    double t = Math.Abs(den) < 1e-12 ? 0.5 : ((ni.X - a.X) * ey - (ni.Y - a.Y) * ex) / den;
                    double x = a.X + t * dx, y = a.Y + t * dy;
                    res.Add(new HvacClash(d.Id, f.Id, d.Story, x, y, top, bot, zb,
                        $"Ống gió {d.SizeText} ({HvacSizing.SystemName(d.System)}) va dầm {f.Id}: đỉnh ống {top:0.###} > đáy dầm {zb:0.###} m – hạ ống ≥ {(top - zb) * 1000:0} mm hoặc dẹt ống (giữ tiết diện), không khoét dầm (rộng {halfW * 2000:0} mm)"));
                }
            }
        }
        return res;
    }

    /// <summary>Đổi va chạm thành cảnh báo điện nước (mã VA_CHAM_ONG_GIO).</summary>
    public static List<MepWarning> Warnings(Project p) => Beams(p).Select(c => new MepWarning
    {
        Code = "VA_CHAM_ONG_GIO", Message = c.Message, Story = c.Story, X = c.X, Y = c.Y, Fix = "Hạ cao độ ống gió dưới đáy dầm hoặc đổi tiết diện dẹt"
    }).ToList();
}

/// <summary>Khối lượng ĐHKK (gộp vào bảng vật tư điện nước).</summary>
public static class HvacQuantities
{
    private static string N(double v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    public static List<MepQtyRow> Rows(Project p)
    {
        var rows = new List<MepQtyRow>();
        var h = p?.Mep?.Hvac;
        if (h == null || h.IsEmpty) return rows;
        HvacRouting.RebuildFittings(p);
        foreach (var g in h.Units.GroupBy(u => u.Name).OrderBy(g => g.Key))
            rows.Add(new MepQtyRow("Điều hoà", g.Key, "bộ", g.Count()));
        foreach (var g in h.Terminals.GroupBy(u => u.Name).OrderBy(g => g.Key))
            rows.Add(new MepQtyRow("Miệng gió", g.Key, "cái", g.Count()));
        foreach (var g in h.Ducts.GroupBy(d => (Th: HvacSizing.SheetThickness(d.MaxSide), d.Round)).OrderBy(g => g.Key.Th))
            rows.Add(new MepQtyRow("Ống gió", $"Tôn mạ kẽm dày {N(g.Key.Th)} mm – ống gió {(g.Key.Round ? "tròn" : "chữ nhật")}", "m²",
                Math.Round(g.Sum(d => d.SheetArea), 2), "chu vi × chiều dài"));
        foreach (var g in h.Ducts.GroupBy(d => (d.System, d.SizeText)).OrderBy(g => g.Key.System).ThenBy(g => g.Key.SizeText))
            rows.Add(new MepQtyRow("Ống gió", $"Ống gió {HvacSizing.SystemName(g.Key.System).ToLowerInvariant()} {g.Key.SizeText}", "m", Math.Round(g.Sum(d => d.Length), 2)));
        var ins = h.Ducts.Where(d => d.System is HvacDuctSystem.GioCap or HvacDuctSystem.GioHoi).Sum(d => d.SheetArea);
        if (ins > 1e-9) rows.Add(new MepQtyRow("Ống gió", "Bảo ôn ống gió cấp / hồi (bông thuỷ tinh 25 mm có bạc)", "m²", Math.Round(ins, 2)));
        int nh = h.Ducts.Sum(d => HvacRouting.DuctHangers(p, d));
        if (nh > 0) rows.Add(new MepQtyRow("Ống gió", "Giá treo ống gió (ty ren + thanh đỡ)", "bộ", nh, $"khoảng cách ≤ {N(h.Settings.DuctHangerSpacing)} m"));
        foreach (var g in h.Fittings.GroupBy(f => f.Name).OrderBy(g => g.Key))
            rows.Add(new MepQtyRow("Phụ kiện ống gió", g.Key, "cái", g.Count()));
        foreach (var g in h.Pipes.Where(q => q.Kind == HvacPipeKind.OngDong).GroupBy(q => (q.LiquidDia, q.GasDia)).OrderBy(g => g.Key.GasDia))
        {
            double L = Math.Round(g.Sum(q => q.Length), 2);
            rows.Add(new MepQtyRow("Ống đồng", $"Cặp ống đồng gas Ø{HvacSizing.Mm(g.Key.LiquidDia)}/Ø{HvacSizing.Mm(g.Key.GasDia)}", "m", L, "chiều dài cặp (lỏng + gas)"));
            rows.Add(new MepQtyRow("Ống đồng", $"Bảo ôn ống đồng Ø{HvacSizing.Mm(g.Key.LiquidDia)}/Ø{HvacSizing.Mm(g.Key.GasDia)}", "m", Math.Round(2 * L, 2), "bọc riêng từng ống"));
        }
        foreach (var g in h.Pipes.Where(q => q.Kind == HvacPipeKind.NuocNgung).GroupBy(q => q.Dia).OrderBy(g => g.Key))
            rows.Add(new MepQtyRow("Ống nước ngưng", $"Ống uPVC Ø{g.Key:0} thoát nước ngưng", "m", Math.Round(g.Sum(q => q.Length), 2), "độ dốc ≥ 1 %"));
        return rows;
    }

    /// <summary>Tổng m² tôn ống gió.</summary>
    public static double SheetArea(Project p) => p?.Mep?.Hvac?.Ducts.Sum(d => d.SheetArea) ?? 0;
    /// <summary>Tổng m cặp ống đồng.</summary>
    public static double CopperLength(Project p) => p?.Mep?.Hvac?.Pipes.Where(q => q.Kind == HvacPipeKind.OngDong).Sum(q => q.Length) ?? 0;
}

/// <summary>Nhập ống gió / miệng gió / thiết bị điều hoà từ Revit (gọi trong MepRevitImport.Handle).</summary>
public static class HvacRevitImport
{
    /// <summary>Thêm phần tử ĐHKK; true nếu đã nhận (kể cả phụ kiện ống gió bỏ qua).</summary>
    public static bool TryAdd(Project p, RvMep it, double ox, double oy)
    {
        string cat = (it.Category ?? "").Trim().ToLowerInvariant().Replace(" ", "");
        string t = ((it.Name ?? "") + " | " + (it.SystemName ?? "")).ToLowerInvariant();
        bool linear = Math.Abs(it.X2 - it.X1) + Math.Abs(it.Y2 - it.Y1) + Math.Abs(it.Z2 - it.Z1) > 1e-6;
        p.Mep ??= new MepModel(); p.Mep.Hvac ??= new HvacModel();
        var h = p.Mep.Hvac;
        if (cat.Contains("ductfitting") || cat.Contains("ductaccessor")) return true; // tự sinh
        if ((cat == "duct" || cat == "ducts" || cat.Contains("flexduct") || cat.Contains("ductcurve")) && linear)
        {
            string story = MepRevitImport.StoryOf(p, it.Level, Math.Min(it.Z1, it.Z2));
            bool round = it.Diameter > 0;
            var d = new HvacDuct
            {
                Id = h.NextId(), Story = story, Round = round, ExternalId = it.Id, System = SystemOf(t),
                Dia = round ? Math.Round(it.Diameter * 1000) : 0,
                W = round ? 0 : Math.Round((it.W > 0 ? it.W : 0.3) * 1000), H = round ? 0 : Math.Round((it.H > 0 ? it.H : 0.2) * 1000),
                Path = new List<MepPt> { new() { X = Math.Round(it.X1 - ox, 4), Y = Math.Round(it.Y1 - oy, 4), Z = it.Z1 }, new() { X = Math.Round(it.X2 - ox, 4), Y = Math.Round(it.Y2 - oy, 4), Z = it.Z2 } }
            };
            h.Ducts.Add(d);
            return true;
        }
        if (cat.Contains("airterminal"))
        {
            string story = MepRevitImport.StoryOf(p, it.Level, it.Z1);
            bool supply = !(t.Contains("hồi") || t.Contains("return") || t.Contains("exhaust") || t.Contains("thải") || t.Contains("hút"));
            double s = it.W > 0 ? Math.Round(it.W * 1000) : 600;
            h.Terminals.Add(new HvacTerminal
            {
                Id = h.NextId(), Story = story, Supply = supply, X = Math.Round(it.X1 - ox, 4), Y = Math.Round(it.Y1 - oy, 4),
                Height = Math.Round(it.Z1 - MepRouting.StoryElevation(p, story), 4), W = s, H = it.D > 0 ? Math.Round(it.D * 1000) : s,
                Name = string.IsNullOrWhiteSpace(it.Name) ? (supply ? "Miệng gió cấp" : "Miệng gió hồi") : it.Name.Trim(), ExternalId = it.Id
            });
            return true;
        }
        if (cat.Contains("mechanicalequipment") || cat.Contains("hvac"))
        {
            bool outdoor = t.Contains("outdoor") || t.Contains("dàn nóng") || t.Contains("condens") || t.Contains("cục nóng") || t.Contains("odu");
            bool indoor = t.Contains("indoor") || t.Contains("dàn lạnh") || t.Contains("fcu") || t.Contains("cassette") || t.Contains("cục lạnh")
                || t.Contains("split") || t.Contains("idu") || t.Contains("ahu") || t.Contains("điều hoà") || t.Contains("điều hòa");
            if (!outdoor && !indoor) return false;
            string story = MepRevitImport.StoryOf(p, it.Level, it.Z1);
            var m = System.Text.RegularExpressions.Regex.Match(t, @"(\d{4,6})\s*btu");
            double btu = m.Success ? double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 12000;
            h.Units.Add(new HvacUnit
            {
                Id = h.NextId(), Story = story, Kind = outdoor ? HvacUnitKind.DanNong : HvacUnitKind.DanLanh, CapacityBtu = btu,
                X = Math.Round(it.X1 - ox, 4), Y = Math.Round(it.Y1 - oy, 4), Height = Math.Round(it.Z1 - MepRouting.StoryElevation(p, story), 4),
                W = it.W > 0 ? it.W : 0.9, D = it.D > 0 ? it.D : 0.25, Hh = it.H > 0 ? it.H : 0.3,
                Name = string.IsNullOrWhiteSpace(it.Name) ? (outdoor ? "Dàn nóng" : "Dàn lạnh") : it.Name.Trim(), ExternalId = it.Id
            });
            return true;
        }
        return false;
    }

    /// <summary>Xoá phần ĐHKK đã nhập trước đó có Id Revit trong tập ids (nhập lại).</summary>
    public static void RemoveImported(Project p, ISet<string> ids)
    {
        var h = p?.Mep?.Hvac; if (h == null) return;
        h.Ducts.RemoveAll(d => d.ExternalId != null && ids.Contains(d.ExternalId));
        h.Terminals.RemoveAll(d => d.ExternalId != null && ids.Contains(d.ExternalId));
        h.Units.RemoveAll(d => d.ExternalId != null && ids.Contains(d.ExternalId));
        h.Pipes.RemoveAll(d => d.ExternalId != null && ids.Contains(d.ExternalId));
    }

    public static HvacDuctSystem SystemOf(string t) =>
        t.Contains("return") || t.Contains("hồi") ? HvacDuctSystem.GioHoi
        : t.Contains("exhaust") || t.Contains("thải") || t.Contains("hút") ? HvacDuctSystem.GioThai
        : t.Contains("fresh") || t.Contains("outside") || t.Contains("tươi") ? HvacDuctSystem.GioTuoi
        : HvacDuctSystem.GioCap;
}

/// <summary>Mặt bằng điều hoà – thông gió (DHKK) theo tầng.</summary>
public static class HvacDrawings
{
    public const string TitlePrefix = "MẶT BẰNG ĐIỀU HOÀ – THÔNG GIÓ";

    public static List<Figure> Figures(Project p)
    {
        var figs = new List<Figure>();
        var h = p?.Mep?.Hvac;
        if (h == null || h.IsEmpty) return figs;
        var names = h.Ducts.Select(d => d.Story).Concat(h.Units.Select(u => u.Story)).Concat(h.Terminals.Select(t => t.Story)).Concat(h.Pipes.Select(q => q.Story)).Distinct().ToList();
        var ordered = p.Stories.OrderBy(s => s.Elevation).Select(s => s.Name).Where(names.Contains).ToList();
        ordered.AddRange(names.Where(n => !ordered.Contains(n)));
        foreach (var st in ordered)
        {
            var f = Plan(p, st);
            if (f != null) figs.Add(f);
        }
        return figs;
    }

    /// <summary>Ký hiệu miệng gió: ô vuông + 2 đường chéo (cấp) / 1 đường chéo (hồi).</summary>
    public static int TerminalSymbol(DxfWriter w, double x, double y, double half, bool supply)
    {
        w.Rect(Lay.Thep, x - half, y - half, x + half, y + half);
        w.Line(Lay.Thep, x - half, y - half, x + half, y + half);
        if (supply) w.Line(Lay.Thep, x - half, y + half, x + half, y - half);
        return supply ? 3 : 2;
    }

    /// <summary>Ký hiệu dàn lạnh (hình chữ nhật + chữ DL) / dàn nóng (hình chữ nhật + vòng quạt + DN).</summary>
    public static void UnitSymbol(DxfWriter w, HvacUnit u, double th)
    {
        double x = u.X * 1000, y = u.Y * 1000, hw = Math.Max(u.W, 0.3) * 500, hd = Math.Max(u.D, 0.15) * 500;
        double a = u.Angle * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
        (double, double) P(double lx, double ly) => (x + lx * c - ly * s, y + lx * s + ly * c);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { P(-hw, -hd), P(hw, -hd), P(hw, hd), P(-hw, hd) }, true);
        if (u.Kind == HvacUnitKind.DanNong) w.Circle(Lay.Thep, x, y, Math.Min(hw, hd) * 0.8);
        w.Text(Lay.Chu, x, y, 0.7 * th, u.Kind == HvacUnitKind.DanLanh ? "DL" : "DN", 0, 1, 2);
    }

    public static Figure Plan(Project p, string story)
    {
        var h = p.Mep.Hvac;
        var ducts = h.Ducts.Where(d => d.Story == story).ToList();
        var units = h.Units.Where(d => d.Story == story).ToList();
        var terms = h.Terminals.Where(d => d.Story == story).ToList();
        var pipes = h.Pipes.Where(d => d.Story == story).ToList();
        if (ducts.Count + units.Count + terms.Count + pipes.Count == 0) return null;
        var w = new DxfWriter(MepDrawings.Scale);
        double th = w.Th;
        // nền: tim tường
        foreach (var wl in p.Walls.Where(x => x.Story == story))
            w.Line(Lay.BeTong, wl.X1 * 1000, wl.Y1 * 1000, wl.X2 * 1000, wl.Y2 * 1000);
        // ống gió: 2 nét biên + tim
        foreach (var d in ducts)
        {
            double half = d.Width / 2;
            for (int i = 1; i < d.Path.Count; i++)
            {
                var a = d.Path[i - 1]; var b = d.Path[i];
                double L = MepGeom.Dist2D(a.X, a.Y, b.X, b.Y);
                if (L < 1e-6) continue;
                double ux = (b.X - a.X) / L, uy = (b.Y - a.Y) / L, nx = -uy, ny = ux;
                foreach (int sg in new[] { 1, -1 })
                    w.Line(Lay.Thep, a.X * 1000 + nx * half * sg, a.Y * 1000 + ny * half * sg, b.X * 1000 + nx * half * sg, b.Y * 1000 + ny * half * sg);
                w.Line(Lay.Chu, a.X * 1000, a.Y * 1000, b.X * 1000, b.Y * 1000, "CENTER");
            }
            int best = -1; double bl = 0;
            for (int i = 1; i < d.Path.Count; i++) { double l2 = MepGeom.Dist2D(d.Path[i - 1].X, d.Path[i - 1].Y, d.Path[i].X, d.Path[i].Y); if (l2 > bl) { bl = l2; best = i; } }
            if (best > 0)
            {
                var a = d.Path[best - 1]; var b = d.Path[best];
                double ang = Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI;
                if (ang > 90) ang -= 180; if (ang < -90) ang += 180;
                string lb = d.SizeText + (d.Flow > 0 ? $" – {d.Flow:0} m³/h" : "") + $" ({HvacSizing.SystemName(d.System)})";
                w.Text(Lay.Chu, (a.X + b.X) / 2 * 1000, (a.Y + b.Y) / 2 * 1000 + d.Width / 2 + 0.3 * th, 0.8 * th, lb, ang, 1, 0);
            }
        }
        foreach (var t in terms)
        {
            TerminalSymbol(w, t.X * 1000, t.Y * 1000, Math.Max(t.W, 150) / 2, t.Supply);
            w.Text(Lay.Chu, t.X * 1000, t.Y * 1000 - Math.Max(t.W, 150) / 2 - 0.4 * th, 0.7 * th, (t.Supply ? "MGC" : "MGH") + t.Id, 0, 1, 3);
        }
        foreach (var u in units)
        {
            UnitSymbol(w, u, th);
            w.Text(Lay.Chu, u.X * 1000, u.Y * 1000 + Math.Max(u.D, 0.15) * 500 + 0.4 * th, 0.7 * th, $"{(u.Kind == HvacUnitKind.DanLanh ? "DL" : "DN")}{u.Id} – {u.CapacityBtu:0} BTU", 0, 1, 0);
        }
        foreach (var q in pipes)
        {
            var pts = q.Path.Select(v => (v.X * 1000, v.Y * 1000)).ToList();
            if (pts.Count >= 2) w.Polyline(Lay.Thep, pts, false, 0, q.Kind == HvacPipeKind.NuocNgung ? "DASHED" : "CENTER");
            if (q.Path.Count >= 2)
            {
                var a = q.Path[0]; var b = q.Path[1];
                string lb = q.Kind == HvacPipeKind.OngDong ? "G " + q.SizeText : "N " + q.SizeText + " " + MepDrawings.SlopeText(HvacRouting.ActualSlope(q));
                w.Text(Lay.Chu, (a.X + b.X) / 2 * 1000, (a.Y + b.Y) / 2 * 1000 + 0.3 * th, 0.7 * th, lb, 0, 1, 0);
            }
        }
        var ext = w.Extents;
        string title = TitlePrefix + " " + story.ToUpperInvariant();
        w.Text(Lay.Chu, ext.IsEmpty ? 0 : ext.Cx, (ext.IsEmpty ? 0 : ext.Y1) - 3 * th, 1.4 * th, title, 0, 1, 0);
        w.Text(Lay.Chu, ext.IsEmpty ? 0 : ext.Cx, (ext.IsEmpty ? 0 : ext.Y1) - 4.8 * th, 0.9 * th, "TL: 1/" + MepDrawings.Scale.ToString("0"), 0, 1, 0);
        Legend(w, ducts, units, terms, pipes, (ext.IsEmpty ? 0 : ext.X2) + 4 * th, ext.IsEmpty ? 0 : ext.Y2, th);
        return new Figure(w, title);
    }

    /// <summary>Bảng ký hiệu ĐHKK (chỉ các loại có trên mặt bằng).</summary>
    public static void Legend(DxfWriter w, List<HvacDuct> ducts, List<HvacUnit> units, List<HvacTerminal> terms, List<HvacPipe> pipes, double x, double yTop, double th)
    {
        double y = yTop, r = 1.2 * th;
        w.Text(Lay.Chu, x, y, 1.1 * th, "KÝ HIỆU ĐIỀU HOÀ – THÔNG GIÓ", 0, 0, 0);
        y -= 2.2 * th;
        void Row(string text) { w.Text(Lay.Chu, x + 3 * r, y, 0.8 * th, text, 0, 0, 2); y -= 2.4 * r; }
        foreach (var sys in ducts.Select(d => d.System).Distinct().OrderBy(s => s))
        {
            w.Line(Lay.Thep, x, y + r * 0.4, x + 2 * r, y + r * 0.4); w.Line(Lay.Thep, x, y - r * 0.4, x + 2 * r, y - r * 0.4);
            Row("Ống gió " + HvacSizing.SystemName(sys).ToLowerInvariant() + " (w×h / Ø, mm)");
        }
        if (terms.Any(t => t.Supply)) { TerminalSymbol(w, x + r, y, r * 0.8, true); Row("Miệng gió cấp (MGC)"); }
        if (terms.Any(t => !t.Supply)) { TerminalSymbol(w, x + r, y, r * 0.8, false); Row("Miệng gió hồi (MGH)"); }
        if (units.Any(u => u.Kind == HvacUnitKind.DanLanh)) { w.Rect(Lay.Thep, x, y - 0.5 * r, x + 2 * r, y + 0.5 * r); Row("Dàn lạnh (DL)"); }
        if (units.Any(u => u.Kind == HvacUnitKind.DanNong)) { w.Rect(Lay.Thep, x, y - 0.6 * r, x + 2 * r, y + 0.6 * r); w.Circle(Lay.Thep, x + r, y, 0.45 * r); Row("Dàn nóng (DN)"); }
        if (pipes.Any(q => q.Kind == HvacPipeKind.OngDong)) { w.Line(Lay.Thep, x, y, x + 2 * r, y, "CENTER"); Row("Ống đồng gas G (Ø lỏng / Ø gas)"); }
        if (pipes.Any(q => q.Kind == HvacPipeKind.NuocNgung)) { w.Line(Lay.Thep, x, y, x + 2 * r, y, "DASHED"); Row("Ống nước ngưng N (uPVC, i ≥ 1 %)"); }
    }
}

/// <summary>Kéo thiết bị điện nước trong khung nhìn 3D: dọc tường chủ (thiết bị gắn tường) hoặc trên mặt sàn (thiết bị tự do) có bắt bước lưới.</summary>
public static class MepDeviceDragMath
{
    /// <summary>Làm tròn về bội step (step ≤ 0: giữ nguyên).</summary>
    public static double Snap(double v, double step) => step > 1e-9 ? Math.Round(v / step) * step : v;

    /// <summary>Thiết bị gắn tường: chiếu điểm chuột (x, y trên mặt sàn) lên tường chủ → vị trí dọc tường (m từ đầu tường), bắt bước, kẹp trong [half, L − half].</summary>
    public static double AlongWall(WallLine w, double x, double y, double step = 0.05, double halfWidth = 0)
    {
        double L = Math.Max(1e-9, w.Chord);
        var pr = MepGeom.Project(x, y, w.X1, w.Y1, w.X2, w.Y2);
        double s = Snap(pr.T * L, step);
        double lo = Math.Min(halfWidth, L / 2), hi = Math.Max(L - halfWidth, L / 2);
        return Math.Clamp(s, lo, hi);
    }

    /// <summary>Thiết bị tự do: bắt bước theo X, Y.</summary>
    public static (double X, double Y) OnFloor(double x, double y, double step = 0.05) => (Snap(x, step), Snap(y, step));

    /// <summary>Áp dụng kéo cho thiết bị (true nếu đổi vị trí). Thiết bị gắn tường đổi Offset; tự do đổi X, Y. Không đi lại tuyến – nơi gọi gọi MepRouting.RebuildAll.</summary>
    public static bool Apply(Project p, MepDevice d, double x, double y, double step = 0.05)
    {
        if (p?.Mep == null || d == null) return false;
        var f = p.Mep.FamilyOf(d);
        var wall = d.WallId > 0 ? p.Walls.FirstOrDefault(q => q.Id == d.WallId) : null;
        if (wall != null)
        {
            double s = AlongWall(wall, x, y, step, (f?.W ?? 0) / 2);
            if (Math.Abs(s - d.Offset) < 1e-9) return false;
            d.Offset = s;
            return true;
        }
        var (nx, ny) = OnFloor(x, y, step);
        if (Math.Abs(nx - d.X) < 1e-9 && Math.Abs(ny - d.Y) < 1e-9) return false;
        d.X = nx; d.Y = ny;
        return true;
    }
}

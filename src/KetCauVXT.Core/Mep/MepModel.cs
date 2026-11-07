using System.Text.Json;
using System.Text.Json.Serialization;
using KetCauVXT.Model;

namespace KetCauVXT.Mep;

// =====================================================================
// BỘ MÔN ĐIỆN NƯỚC (MEP) – mô hình dữ liệu.
// Thiết bị (ổ cắm, đèn, quạt, lavabo…) gắn vào TƯỜNG kiến trúc (tham chiếu Id tường + vị trí dọc tường + cao độ + mặt),
// tuyến ống / ống gen / dây nối giữa các thiết bị, MẶC ĐỊNH CHÔN NGẦM TRONG TƯỜNG (đi trong lõi tường).
// Kiến trúc là "xref": phần điện nước chỉ đọc tường / cửa / tầng; khi kiến trúc thay đổi MepUpdater.Reconcile tự cập nhật
// vị trí thiết bị, đi lại tuyến và cảnh báo xung đột.
// Đơn vị: m (toạ độ, chiều dài), mm (đường kính ống), mm² (tiết diện dây), W (công suất).
// =====================================================================

/// <summary>Hệ thống điện nước.</summary>
public enum MepSystemKind
{
    CapNuocLanh, CapNuocNong, ThoatNuocThai, ThoatNuocXam, ThoatNuocMua,
    DienChieuSang, DienOCam, DienDieuHoa, DienNhe
}

/// <summary>Nhóm họ thiết bị / vật tư.</summary>
public enum MepCategory
{
    Ong, PhuKien, Van, OCam, CongTac, Den, Quat, HopChia, Aptomat, TuDien, OngGen, Day, Cap, VeSinh, BinhNong, DiemNoiOng
}

/// <summary>Loại tuyến: ống nước, ống gen điện (luồn dây), dây đơn, cáp nhiều lõi.</summary>
public enum MepRunKind { Ong, OngGen, Day, Cap }

/// <summary>Loại phụ kiện sinh tự động khi đi tuyến.</summary>
public enum MepFittingKind { Cut90, Chech45, Te, TeGiam, Con, MangSong, HopNoi, TeXien, KiemTra }

/// <summary>Thông tin hệ thống: tên, nhóm, layer bản vẽ, màu.</summary>
public static class MepSystems
{
    public static string Name(MepSystemKind k) => k switch
    {
        MepSystemKind.CapNuocLanh => "Cấp nước lạnh",
        MepSystemKind.CapNuocNong => "Cấp nước nóng",
        MepSystemKind.ThoatNuocThai => "Thoát nước thải",
        MepSystemKind.ThoatNuocXam => "Thoát nước xám",
        MepSystemKind.ThoatNuocMua => "Thoát nước mưa",
        MepSystemKind.DienChieuSang => "Điện chiếu sáng",
        MepSystemKind.DienOCam => "Điện ổ cắm",
        MepSystemKind.DienDieuHoa => "Điện điều hoà",
        _ => "Điện nhẹ"
    };
    public static bool IsSupply(MepSystemKind k) => k is MepSystemKind.CapNuocLanh or MepSystemKind.CapNuocNong;
    public static bool IsDrain(MepSystemKind k) => k is MepSystemKind.ThoatNuocThai or MepSystemKind.ThoatNuocXam or MepSystemKind.ThoatNuocMua;
    public static bool IsWater(MepSystemKind k) => IsSupply(k) || IsDrain(k);
    public static bool IsElectric(MepSystemKind k) => !IsWater(k);
    /// <summary>Layer DXF theo hệ thống.</summary>
    public static string Layer(MepSystemKind k) => k switch
    {
        MepSystemKind.CapNuocLanh => "DN_CAP_LANH",
        MepSystemKind.CapNuocNong => "DN_CAP_NONG",
        MepSystemKind.ThoatNuocThai => "DN_THOAT_THAI",
        MepSystemKind.ThoatNuocXam => "DN_THOAT_XAM",
        MepSystemKind.ThoatNuocMua => "DN_THOAT_MUA",
        MepSystemKind.DienChieuSang => "DN_DIEN_CS",
        MepSystemKind.DienOCam => "DN_DIEN_OC",
        MepSystemKind.DienDieuHoa => "DN_DIEN_DH",
        _ => "DN_DIEN_NHE"
    };
    /// <summary>Màu hiển thị (#RRGGBB).</summary>
    public static string Color(MepSystemKind k) => k switch
    {
        MepSystemKind.CapNuocLanh => "#1E88E5",
        MepSystemKind.CapNuocNong => "#E53935",
        MepSystemKind.ThoatNuocThai => "#6D4C41",
        MepSystemKind.ThoatNuocXam => "#8D6E63",
        MepSystemKind.ThoatNuocMua => "#00897B",
        MepSystemKind.DienChieuSang => "#F9A825",
        MepSystemKind.DienOCam => "#8E24AA",
        MepSystemKind.DienDieuHoa => "#3949AB",
        _ => "#546E7A"
    };
    public static IEnumerable<MepSystemKind> All => Enum.GetValues<MepSystemKind>();
}

/// <summary>Đầu nối của họ thiết bị.</summary>
public class MepConnector
{
    public MepSystemKind System { get; set; }
    /// <summary>Đường kính ống (mm) hoặc tiết diện dây (mm²) mặc định</summary>
    public double Size { get; set; }
}

/// <summary>Họ thiết bị / vật tư trong thư viện (kiểu Revit "family type").</summary>
public class MepFamily
{
    public string Name { get; set; } = "";
    public MepCategory Category { get; set; }
    /// <summary>Khoá ký hiệu 2D (MepSymbols): "ocam1", "ocam2", "congtac", "den", "dentuyp", "downlight", "denhat", "quattran",
    /// "quattuong", "hopchia", "aptomat", "tudien", "boncau", "lavabo", "sen", "voi", "thoatsan", "binhnong", "van", "diemnoi"</summary>
    public string Symbol { get; set; } = "";
    /// <summary>Kích thước hộp 3D (m): rộng (dọc tường), sâu (vuông góc tường), cao</summary>
    public double W { get; set; } = 0.1;
    public double D { get; set; } = 0.05;
    public double H { get; set; } = 0.1;
    /// <summary>Cao độ lắp mặc định so với sàn hoàn thiện (m); thiết bị trần: cao độ tầng – 0,1</summary>
    public double MountHeight { get; set; } = 0.4;
    /// <summary>Gắn tường (true) hay đặt tự do trên sàn / trần (false)</summary>
    public bool WallHosted { get; set; } = true;
    /// <summary>Thiết bị trên trần (đèn, quạt trần): cao độ = cao tầng − 0,1</summary>
    public bool Ceiling { get; set; }
    /// <summary>Công suất (W) – thiết bị điện</summary>
    public double Power { get; set; }
    /// <summary>Quang thông đèn (lm) – dùng cho phương pháp hệ số sử dụng N = E·A / (Φ·UF·MF); 0 = Power × hiệu suất mặc định (cs.lm_w)</summary>
    public double Lumen { get; set; }
    /// <summary>Đương lượng cấp nước N (TCVN 4513) – thiết bị vệ sinh</summary>
    public double SupplyUnits { get; set; }
    /// <summary>Đương lượng thoát nước (DFU, TCVN 4474 giản lược)</summary>
    public double DrainUnits { get; set; }
    /// <summary>Đường kính ống (mm) / tiết diện dây (mm²) / số lõi cáp – vật tư dạng tuyến</summary>
    public double Size { get; set; }
    public int Cores { get; set; }
    public string Material { get; set; } = "";
    public List<MepConnector> Connectors { get; set; } = new();
    public string Note { get; set; } = "";
    /// <summary>Thêm bởi người dùng (lưu ở thư viện người dùng)</summary>
    public bool User { get; set; }
    [JsonIgnore] public bool IsLinear => Category is MepCategory.Ong or MepCategory.OngGen or MepCategory.Day or MepCategory.Cap;
    public MepFamily Clone() => JsonSerializer.Deserialize<MepFamily>(JsonSerializer.Serialize(this, MepLibrary.JsonOpt), MepLibrary.JsonOpt);
}

/// <summary>Thư viện điện nước: mặc định + họ người dùng thêm (%AppData%\VietConst\ThuVienMEP.json – CHỈ trên máy).</summary>
public class MepLibrary
{
    public List<MepFamily> Families { get; set; } = new();

    internal static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public MepFamily Find(string name) => Families.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
    public IEnumerable<MepFamily> OfCategory(MepCategory c) => Families.Where(f => f.Category == c);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOpt);
    public static MepLibrary FromJson(string json) => JsonSerializer.Deserialize<MepLibrary>(json, JsonOpt) ?? new MepLibrary();

    /// <summary>Đường dẫn thư viện người dùng (chỉ lưu trên máy).</summary>
    public static string UserPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VietConst", "ThuVienMEP.json");

    /// <summary>Thư viện mặc định + họ người dùng (đọc từ file nếu có; lỗi đọc → bỏ qua).</summary>
    public static MepLibrary Load(string path = null)
    {
        var lib = Defaults();
        try
        {
            path ??= UserPath;
            if (File.Exists(path))
                foreach (var f in FromJson(File.ReadAllText(path)).Families.Where(f => !string.IsNullOrWhiteSpace(f.Name)))
                {
                    f.User = true;
                    lib.Families.RemoveAll(x => string.Equals(x.Name, f.Name, StringComparison.OrdinalIgnoreCase));
                    lib.Families.Add(f);
                }
        }
        catch { }
        return lib;
    }

    /// <summary>Lưu riêng các họ người dùng thêm / sửa.</summary>
    public void SaveUser(string path = null)
    {
        path ??= UserPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var u = new MepLibrary { Families = Families.Where(f => f.User).ToList() };
        File.WriteAllText(path, u.ToJson());
    }

    private static MepConnector C(MepSystemKind s, double size) => new() { System = s, Size = size };

    /// <summary>Thư viện mặc định (ống, phụ kiện, thiết bị điện, dây cáp, thiết bị vệ sinh).</summary>
    public static MepLibrary Defaults()
    {
        var L = new List<MepFamily>();
        // ---- ống cấp (PPR) và thoát (uPVC) ----
        foreach (var d in new[] { 20, 25, 32, 40, 50, 63 })
            L.Add(new MepFamily { Name = $"Ống PPR Ø{d}", Category = MepCategory.Ong, Size = d, Material = "PPR PN20", WallHosted = false, Symbol = "" });
        foreach (var d in new[] { 34, 42, 49, 60, 76, 90, 110 })
            L.Add(new MepFamily { Name = $"Ống uPVC Ø{d}", Category = MepCategory.Ong, Size = d, Material = "uPVC", WallHosted = false, Symbol = "" });
        // ---- phụ kiện ----
        L.Add(new MepFamily { Name = "Cút 90°", Category = MepCategory.PhuKien, Symbol = "", WallHosted = false, Note = "Đổi hướng 90°" });
        L.Add(new MepFamily { Name = "Chếch 45° (lơi)", Category = MepCategory.PhuKien, Symbol = "", WallHosted = false, Note = "Đổi hướng 45°" });
        L.Add(new MepFamily { Name = "Tê đều", Category = MepCategory.PhuKien, Symbol = "", WallHosted = false, Note = "Rẽ nhánh" });
        L.Add(new MepFamily { Name = "Tê giảm", Category = MepCategory.PhuKien, Symbol = "", WallHosted = false, Note = "Rẽ nhánh khác cỡ" });
        L.Add(new MepFamily { Name = "Côn thu", Category = MepCategory.PhuKien, Symbol = "", WallHosted = false, Note = "Thay đổi tiết diện ống" });
        L.Add(new MepFamily { Name = "Măng sông", Category = MepCategory.PhuKien, Symbol = "", WallHosted = false, Note = "Khớp nối theo chiều dài cây ống" });
        L.Add(new MepFamily { Name = "Van chặn", Category = MepCategory.Van, Symbol = "van", W = 0.08, D = 0.06, H = 0.08, MountHeight = 0.6,
            Connectors = { C(MepSystemKind.CapNuocLanh, 20) } });
        L.Add(new MepFamily { Name = "Điểm nối ống", Category = MepCategory.DiemNoiOng, Symbol = "diemnoi", W = 0.06, D = 0.05, H = 0.06, MountHeight = 0.3,
            Note = "Điểm chia nhánh ống (sinh tê)" });
        L.Add(new MepFamily { Name = "Ống đứng thoát nước", Category = MepCategory.DiemNoiOng, Symbol = "ongdung", W = 0.16, D = 0.16, H = 0.1, MountHeight = 0,
            WallHosted = false, Size = 110, Material = "uPVC", Note = "Ống đứng (hộp gen kỹ thuật) – điểm thu các tuyến thoát ngang dưới sàn",
            Connectors = { C(MepSystemKind.ThoatNuocThai, 110) } });
        // ---- điện ----
        L.Add(new MepFamily { Name = "Ổ cắm đơn", Category = MepCategory.OCam, Symbol = "ocam1", W = 0.086, D = 0.04, H = 0.086, MountHeight = 0.4, Power = 300,
            Connectors = { C(MepSystemKind.DienOCam, 2.5) } });
        L.Add(new MepFamily { Name = "Ổ cắm đôi", Category = MepCategory.OCam, Symbol = "ocam2", W = 0.12, D = 0.04, H = 0.086, MountHeight = 0.4, Power = 600,
            Connectors = { C(MepSystemKind.DienOCam, 2.5) } });
        for (int k = 1; k <= 3; k++)
            L.Add(new MepFamily { Name = $"Công tắc {k} hạt", Category = MepCategory.CongTac, Symbol = "congtac" + k, W = 0.086, D = 0.04, H = 0.086, MountHeight = 1.25,
                Connectors = { C(MepSystemKind.DienChieuSang, 1.5) } });
        L.Add(new MepFamily { Name = "Đèn ốp trần", Category = MepCategory.Den, Symbol = "den", W = 0.3, D = 0.3, H = 0.08, WallHosted = false, Ceiling = true, Power = 24, Lumen = 2200,
            Connectors = { C(MepSystemKind.DienChieuSang, 1.5) } });
        L.Add(new MepFamily { Name = "Đèn tuýp LED 1,2 m", Category = MepCategory.Den, Symbol = "dentuyp", W = 1.2, D = 0.06, H = 0.05, WallHosted = false, Ceiling = true, Power = 18, Lumen = 1800,
            Connectors = { C(MepSystemKind.DienChieuSang, 1.5) } });
        L.Add(new MepFamily { Name = "Đèn downlight", Category = MepCategory.Den, Symbol = "downlight", W = 0.12, D = 0.12, H = 0.06, WallHosted = false, Ceiling = true, Power = 9, Lumen = 800,
            Connectors = { C(MepSystemKind.DienChieuSang, 1.5) } });
        L.Add(new MepFamily { Name = "Đèn hắt tường", Category = MepCategory.Den, Symbol = "denhat", W = 0.2, D = 0.1, H = 0.1, MountHeight = 2.2, Power = 12, Lumen = 960,
            Connectors = { C(MepSystemKind.DienChieuSang, 1.5) } });
        L.Add(new MepFamily { Name = "Quạt trần", Category = MepCategory.Quat, Symbol = "quattran", W = 1.2, D = 1.2, H = 0.4, WallHosted = false, Ceiling = true, Power = 75,
            Connectors = { C(MepSystemKind.DienChieuSang, 1.5) } });
        L.Add(new MepFamily { Name = "Quạt treo tường", Category = MepCategory.Quat, Symbol = "quattuong", W = 0.45, D = 0.3, H = 0.45, MountHeight = 2.2, Power = 55,
            Connectors = { C(MepSystemKind.DienOCam, 2.5) } });
        L.Add(new MepFamily { Name = "Hộp chia (điểm chia)", Category = MepCategory.HopChia, Symbol = "hopchia", W = 0.1, D = 0.05, H = 0.1, MountHeight = 2.5,
            Note = "Hộp nối dây âm tường" });
        L.Add(new MepFamily { Name = "Aptomat 1 pha", Category = MepCategory.Aptomat, Symbol = "aptomat", W = 0.1, D = 0.08, H = 0.12, MountHeight = 1.5 });
        L.Add(new MepFamily { Name = "Tủ điện tầng", Category = MepCategory.TuDien, Symbol = "tudien", W = 0.3, D = 0.1, H = 0.4, MountHeight = 1.5,
            Note = "Nguồn các mạch điện của tầng" });
        foreach (var d in new[] { 16, 20, 25, 32 })
            L.Add(new MepFamily { Name = $"Ống gen PVC Ø{d}", Category = MepCategory.OngGen, Size = d, Material = "PVC", WallHosted = false });
        foreach (var s in new[] { 1.5, 2.5, 4, 6, 10 })
            L.Add(new MepFamily { Name = $"Dây CV {DxfNum(s)} mm²", Category = MepCategory.Day, Size = s, Cores = 1, Material = "Cu/PVC", WallHosted = false });
        foreach (var (s, n, m) in new[] { (2.5, 3, "Cu/PVC/PVC"), (4.0, 3, "Cu/PVC/PVC"), (6.0, 3, "Cu/XLPE/PVC"), (10.0, 4, "Cu/XLPE/PVC") })
            L.Add(new MepFamily { Name = $"Cáp CVV {n}x{DxfNum(s)} mm²", Category = MepCategory.Cap, Size = s, Cores = n, Material = m, WallHosted = false });
        // ---- thiết bị vệ sinh ----
        L.Add(new MepFamily { Name = "Bồn cầu", Category = MepCategory.VeSinh, Symbol = "boncau", W = 0.4, D = 0.7, H = 0.4, MountHeight = 0, SupplyUnits = 0.5, DrainUnits = 4,
            Connectors = { C(MepSystemKind.CapNuocLanh, 20), C(MepSystemKind.ThoatNuocThai, 110) } });
        L.Add(new MepFamily { Name = "Lavabo", Category = MepCategory.VeSinh, Symbol = "lavabo", W = 0.5, D = 0.4, H = 0.2, MountHeight = 0.8, SupplyUnits = 0.33, DrainUnits = 1,
            Connectors = { C(MepSystemKind.CapNuocLanh, 20), C(MepSystemKind.CapNuocNong, 20), C(MepSystemKind.ThoatNuocXam, 42) } });
        L.Add(new MepFamily { Name = "Sen tắm", Category = MepCategory.VeSinh, Symbol = "sen", W = 0.2, D = 0.1, H = 0.2, MountHeight = 1.1, SupplyUnits = 0.67, DrainUnits = 2,
            Connectors = { C(MepSystemKind.CapNuocLanh, 20), C(MepSystemKind.CapNuocNong, 20) } });
        L.Add(new MepFamily { Name = "Vòi rửa", Category = MepCategory.VeSinh, Symbol = "voi", W = 0.06, D = 0.12, H = 0.06, MountHeight = 0.6, SupplyUnits = 1.0, DrainUnits = 0,
            Connectors = { C(MepSystemKind.CapNuocLanh, 20) } });
        L.Add(new MepFamily { Name = "Vòi chậu bếp", Category = MepCategory.VeSinh, Symbol = "voi", W = 0.06, D = 0.2, H = 0.3, MountHeight = 0.9, SupplyUnits = 1.0, DrainUnits = 2,
            Connectors = { C(MepSystemKind.CapNuocLanh, 20), C(MepSystemKind.ThoatNuocXam, 49) } });
        L.Add(new MepFamily { Name = "Thoát sàn (phễu thu)", Category = MepCategory.VeSinh, Symbol = "thoatsan", W = 0.15, D = 0.15, H = 0.1, WallHosted = false, MountHeight = 0, DrainUnits = 2,
            Connectors = { C(MepSystemKind.ThoatNuocXam, 60) } });
        L.Add(new MepFamily { Name = "Bình nóng lạnh 30 L", Category = MepCategory.BinhNong, Symbol = "binhnong", W = 0.5, D = 0.35, H = 0.5, MountHeight = 1.9, Power = 2500,
            Connectors = { C(MepSystemKind.CapNuocLanh, 20), C(MepSystemKind.CapNuocNong, 20), C(MepSystemKind.DienOCam, 4) } });
        return new MepLibrary { Families = L };
    }

    private static string DxfNum(double v) => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
}

/// <summary>Thiết bị đặt trong mô hình. Gắn tường: WallId &gt; 0, vị trí = Offset dọc tường từ đầu (X1,Y1), Side = mặt tường (+1 trái / −1 phải theo
/// hướng X1→X2), Height = cao độ so với sàn. Tự do (sàn/trần): WallId = 0, X/Y là vị trí thật.</summary>
public class MepDevice
{
    public int Id { get; set; }
    public string Family { get; set; } = "";
    public string Story { get; set; } = "";
    public int WallId { get; set; }
    public double Offset { get; set; }
    public int Side { get; set; } = 1;
    public double Height { get; set; } = 0.4;
    /// <summary>Vị trí mặt bằng (m): thiết bị tự do = vị trí đặt; thiết bị gắn tường = vị trí tính lần gần nhất (cập nhật bởi Reconcile)</summary>
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>Tên mạch điện (C1, C2…) – thiết bị điện</summary>
    public string Circuit { get; set; } = "";
    /// <summary>Ký hiệu tự đặt (OC1, Đ1, LV1…) – trống = tự sinh</summary>
    public string Mark { get; set; } = "";
    /// <summary>Đặt nổi (không chôn tường) – mặc định false = chôn ngầm</summary>
    public bool Exposed { get; set; }
    /// <summary>Id phần tử nguồn (Revit UniqueId) khi nhập từ Revit; null = vẽ trong chương trình</summary>
    public string ExternalId { get; set; }
}

/// <summary>Điểm 3D của tuyến (m), kèm Id tường mà đoạn tới điểm này nằm trong (0 = ngoài tường: trong sàn / trần).</summary>
public struct MepPt
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public int WallId { get; set; }
    public MepPt(double x, double y, double z, int wallId = 0) { X = x; Y = y; Z = z; WallId = wallId; }
    public override readonly string ToString() => $"({X:0.###};{Y:0.###};{Z:0.###})";
}

/// <summary>Tuyến ống / ống gen / dây / cáp nối 2 thiết bị (FromId → ToId). Mặc định chôn ngầm trong tường; ManualPoints (mặt bằng) = người dùng tự vẽ.</summary>
public class MepRun
{
    public int Id { get; set; }
    public MepSystemKind System { get; set; }
    public MepRunKind Kind { get; set; } = MepRunKind.Ong;
    /// <summary>Ghi chú lần đi tuyến gần nhất (vd. ống gen không đi được trong sàn → đã đi trong tường); không lưu.</summary>
    [JsonIgnore] public string RouteNote { get; set; }
    public string Story { get; set; } = "";
    public int FromId { get; set; }
    public int ToId { get; set; }
    /// <summary>Đường kính ống / ống gen (mm); dây: tiết diện (mm²)</summary>
    public double Size { get; set; }
    /// <summary>Ống gen luồn dây (mm) – tuyến dây điện; 0 = tự chọn</summary>
    public double ConduitSize { get; set; }
    /// <summary>Số ruột dây trong tuyến (L + N + PE = 3)</summary>
    public int Conductors { get; set; } = 3;
    public string Material { get; set; } = "";
    public string Circuit { get; set; } = "";
    /// <summary>Tự chọn cỡ (MepSizing) khi bấm "Tự chọn tiết diện"</summary>
    public bool AutoSize { get; set; } = true;
    /// <summary>Điểm mặt bằng người dùng vẽ tay (m); rỗng = tự đi trong tường</summary>
    public List<double[]> ManualPoints { get; set; } = new();
    /// <summary>Đường đi đã tính (cập nhật bởi MepRouting / Reconcile)</summary>
    public List<MepPt> Path { get; set; } = new();
    /// <summary>Độ dốc ống thoát (vd. 0,02 = 2 %); 0 = mặc định theo Ø (MepDrainage.DefaultSlope)</summary>
    public double Slope { get; set; }
    /// <summary>Ép đi ống thoát trong tường (không khuyến khích – có cảnh báo); mặc định đi dưới sàn</summary>
    public bool DrainInWall { get; set; }
    /// <summary>Id phần tử nguồn (Revit UniqueId) khi nhập từ Revit; null = vẽ trong chương trình</summary>
    public string ExternalId { get; set; }
    /// <summary>Giữ nguyên đường đi đã có (tuyến nhập từ Revit) – không tự đi lại khi Reconcile / Route</summary>
    public bool FixedPath { get; set; }
    [JsonIgnore] public double Length => MepGeom.Length(Path);
}

/// <summary>Phụ kiện sinh tự động.</summary>
public class MepFitting
{
    public MepFittingKind Kind { get; set; }
    public MepSystemKind System { get; set; }
    public double Size { get; set; }
    public double Size2 { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public int RunId { get; set; }
    public int DeviceId { get; set; }
    public string Story { get; set; } = "";
    public string Name => MepFittings.Name(Kind, System, Size, Size2);
}

public static class MepFittings
{
    public static string KindName(MepFittingKind k, MepSystemKind s) => k switch
    {
        MepFittingKind.Cut90 => MepSystems.IsElectric(s) ? "Cút ống gen 90°" : "Cút 90°",
        MepFittingKind.Chech45 => "Chếch 45° (lơi)",
        MepFittingKind.Te => "Tê đều",
        MepFittingKind.TeGiam => "Tê giảm",
        MepFittingKind.Con => "Côn thu",
        MepFittingKind.MangSong => MepSystems.IsElectric(s) ? "Khớp nối ống gen" : "Măng sông",
        MepFittingKind.TeXien => "Tê xiên 45° (chữ Y)",
        MepFittingKind.KiemTra => "Ống kiểm tra (nắp thăm)",
        _ => "Hộp nối (điểm chia)"
    };
    public static string Name(MepFittingKind k, MepSystemKind s, double size, double size2)
    {
        string n = KindName(k, s);
        if (k == MepFittingKind.HopNoi) return n;
        string sz = size2 > 0 && Math.Abs(size2 - size) > 1e-6 ? $"Ø{size:0}/Ø{size2:0}" : $"Ø{size:0}";
        return $"{n} {sz}";
    }
}

/// <summary>Cảnh báo bộ môn điện nước.</summary>
public class MepWarning
{
    /// <summary>Mã: TUONG_XOA, TRONG_CUA, TUONG_MONG, VA_CHAM, XUYEN_COT, XUYEN_DAM, KHONG_TUYEN, NGOAI_TUONG</summary>
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public int DeviceId { get; set; }
    public int RunId { get; set; }
    public int WallId { get; set; }
    public string Story { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>Gợi ý xử lý nhanh</summary>
    public string Fix { get; set; } = "";
    /// <summary>Mức độ (kết cấu): "Cho phép", "Gợi ý", "Cảnh báo", "Nguy hiểm", "Cấm"; rỗng = cảnh báo thường</summary>
    public string Severity { get; set; } = "";
    /// <summary>Cấu kiện kết cấu liên quan (Id thanh / ô sàn), 0 = không</summary>
    public int FrameId { get; set; }
    public int SlabId { get; set; }
    public override string ToString() => $"[{Code}] {Message}";
}

/// <summary>Mạch điện (tủ → thiết bị) dùng cho tự chọn dây / aptomat.</summary>
public class MepCircuit
{
    public string Name { get; set; } = "";
    public MepSystemKind System { get; set; } = MepSystemKind.DienOCam;
    public double Voltage { get; set; } = 220;
    public double CosPhi { get; set; } = 0.85;
    /// <summary>Hệ số đồng thời</summary>
    public double Demand { get; set; } = 1.0;
}

/// <summary>Ảnh chụp hình học tường (để nhận biết thay đổi kiến trúc).</summary>
public class MepWallSnap
{
    public int WallId { get; set; }
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public double Thickness { get; set; }
    public string Openings { get; set; } = "";
}

/// <summary>Độ dốc ống thoát riêng cho một hệ thống (và tuỳ chọn giới hạn Ø).</summary>
public class MepSlopeRule
{
    public MepSystemKind System { get; set; }
    /// <summary>Áp dụng cho Ø ≤ DiaMax (mm); 0 = mọi Ø</summary>
    public double DiaMax { get; set; }
    /// <summary>Độ dốc (vd. 0,01 = 1 %)</summary>
    public double Slope { get; set; }
    /// <summary>Áp dụng cho Ø ≥ DiaMin (mm); 0 = không giới hạn dưới</summary>
    public double DiaMin { get; set; }
    /// <summary>Độ dốc nhỏ nhất cho phép (kẹp cả độ dốc nhập tay theo hệ này); 0 = không kẹp</summary>
    public double SlopeMin { get; set; }
    /// <summary>Độ dốc lớn nhất cho phép; 0 = không kẹp</summary>
    public double SlopeMax { get; set; }
}

/// <summary>Cao độ lắp đặt mặc định theo nhóm thiết bị (m so với sàn hoàn thiện) – ghi đè MountHeight của họ khi đặt mới.</summary>
public class MepCategoryHeight
{
    public MepCategory Category { get; set; }
    public double Height { get; set; }
}

/// <summary>Cài đặt đi tuyến.</summary>
public class MepSettings
{
    /// <summary>Cao độ tuyến ngang chôn tường so với sàn (ổ cắm, cấp nước) (m)</summary>
    public double RunHeight { get; set; } = 0.3;
    /// <summary>Tuyến chiếu sáng đi ngang sát trần: cao tầng − giá trị này (m)</summary>
    public double CeilingDrop { get; set; } = 0.15;
    /// <summary>Thoát nước đi trong sàn: cao độ so với sàn hoàn thiện (âm = dưới sàn) (m)</summary>
    public double DrainLevel { get; set; } = -0.15;
    /// <summary>Chiều dài cây ống nước (m) – khớp nối (măng sông)</summary>
    public double StockPipe { get; set; } = 4.0;
    /// <summary>Chiều dài cây ống gen (m)</summary>
    public double StockConduit { get; set; } = 2.9;
    /// <summary>Chiều cao tầng mặc định khi tầng không khai báo (m)</summary>
    public double DefaultStoryHeight { get; set; } = 3.3;

    // ---- thoát nước đi dưới sàn, có độ dốc (MepDrainage) ----
    /// <summary>Ống thoát đi DƯỚI sàn (treo dưới đáy sàn / trong lớp hạ cốt) tới ống đứng – mặc định true. false = đi trong lớp sàn (DrainLevel).</summary>
    public bool DrainBelowSlab { get; set; } = true;
    /// <summary>Độ dốc mặc định Ø ≤ 50 (TCVN 4474 – thực hành): 3 %</summary>
    public double DrainSlopeSmall { get; set; } = 0.03;
    /// <summary>Độ dốc mặc định 50 &lt; Ø &lt; 110 (Ø60…Ø90): 2 %</summary>
    public double DrainSlopeMedium { get; set; } = 0.02;
    /// <summary>Độ dốc mặc định Ø ≥ 110: 2 % (tối thiểu 1 %)</summary>
    public double DrainSlopeLarge { get; set; } = 0.02;
    /// <summary>Độ dốc lớn nhất khuyến nghị</summary>
    public double DrainSlopeMax { get; set; } = 0.08;
    /// <summary>Độ dốc riêng theo HỆ THỐNG (+ Ø): ghi đè độ dốc mặc định theo Ø. DiaMax = 0 → mọi Ø của hệ.
    /// Ví dụ: thoát nước mưa 1 % mọi Ø; thoát nước xám Ø ≤ 60 → 2,5 %. Dòng khớp đầu tiên (DiaMax nhỏ nhất ≥ Ø) được dùng.</summary>
    public List<MepSlopeRule> SlopeRules { get; set; } = new();
    /// <summary>Khe từ đáy sàn tới đỉnh ống treo (m)</summary>
    public double DrainHangerGap { get; set; } = 0.05;
    /// <summary>Chiều sâu không gian trần (từ đáy sàn xuống trần giả) cho phép ống thoát (m)</summary>
    public double CeilingVoid { get; set; } = 0.6;
    /// <summary>Khoảng cách ống kiểm tra (nắp thăm) trên tuyến ngang dài (m)</summary>
    public double CleanoutSpacing { get; set; } = 12;
    /// <summary>Khoảng cách quang treo ống ngang cố định (m); 0 = theo Ø (0,8 / 1,0 / 1,2 m)</summary>
    public double HangerSpacing { get; set; }
    /// <summary>Cao độ mặc định theo nhóm thiết bị (rỗng = theo họ)</summary>
    public List<MepCategoryHeight> CategoryHeights { get; set; } = new();
    /// <summary>Tủ điện: chia mạch, hệ số nhu cầu, 1 / 3 pha (Mep/MepRound4.cs)</summary>
    public PanelOptions Panel { get; set; } = new();
    /// <summary>Bể nước mái / bể ngầm, bơm (Mep/MepRound4.cs)</summary>
    public WaterTankOptions Water { get; set; } = new();

    // ---- bảng TCVN + tuyến ống gen trong sàn / trần (Mep/MepStandards.cs, Mep/SlabConduit.cs) ----
    /// <summary>Lựa chọn áp dụng tiêu chuẩn (loại công trình, nhiệt độ, nhóm mạch, khí hậu) + giá trị tuỳ chỉnh</summary>
    public MepStdOptions Standards { get; set; } = new();
    /// <summary>Tuyến ống gen điện: TranSan (mặc định, khuyến nghị) = ngang trong sàn / trần, chỉ đi đứng trong tường tới thiết bị;
    /// TrongTuong = ngang trong tường (cách cũ). Cấp nước vẫn đi trong tường.</summary>
    public ConduitRouteMode ConduitRoute { get; set; } = ConduitRouteMode.TranSan;
    /// <summary>Lớp cán nền trên mặt sàn để chuyển ống gen ra khỏi dầm dưới tường (m)</summary>
    public double ScreedLevel { get; set; } = 0.03;
    /// <summary>Bước lưới dò tuyến ống gen trong sàn (m)</summary>
    public double ConduitRouteStep { get; set; } = 0.1;
    /// <summary>Phạt mỗi lần ống gen qua dầm trong chiều dày sàn (m tương đương)</summary>
    public double ConduitBeamPenalty { get; set; } = 3.0;
    /// <summary>Phạt mỗi lần đổi hướng ống gen (m tương đương)</summary>
    public double ConduitBendPenalty { get; set; } = 0.3;
}

/// <summary>Toàn bộ dữ liệu điện nước của dự án (Project.Mep).</summary>
public class MepModel
{
    public List<MepDevice> Devices { get; set; } = new();
    public List<MepRun> Runs { get; set; } = new();
    public List<MepCircuit> Circuits { get; set; } = new();
    public MepSettings Settings { get; set; } = new();
    /// <summary>Họ người dùng nhúng trong dự án (để mở máy khác vẫn có)</summary>
    public List<MepFamily> ProjectFamilies { get; set; } = new();
    /// <summary>Ảnh chụp tường lần cập nhật gần nhất</summary>
    public List<MepWallSnap> WallSnaps { get; set; } = new();
    /// <summary>Điều hoà – thông gió (ống gió, miệng gió, dàn lạnh / dàn nóng, ống đồng, ống nước ngưng) – Mep/MepHvac.cs</summary>
    public HvacModel Hvac { get; set; } = new();

    [JsonIgnore] public List<MepFitting> Fittings { get; set; } = new();
    [JsonIgnore] public List<MepWarning> Warnings { get; set; } = new();
    [JsonIgnore] private MepLibrary _lib;

    /// <summary>Thư viện dùng cho dự án: mặc định + họ nhúng trong dự án (không đọc file người dùng – để kết quả lặp lại được).</summary>
    [JsonIgnore]
    public MepLibrary Library
    {
        get
        {
            if (_lib == null)
            {
                _lib = MepLibrary.Defaults();
                foreach (var f in ProjectFamilies)
                {
                    _lib.Families.RemoveAll(x => string.Equals(x.Name, f.Name, StringComparison.OrdinalIgnoreCase));
                    _lib.Families.Add(f);
                }
            }
            return _lib;
        }
        set => _lib = value;
    }

    public MepFamily FamilyOf(MepDevice d) => Library.Find(d.Family);
    public MepDevice Device(int id) => Devices.FirstOrDefault(d => d.Id == id);
    public MepRun Run(int id) => Runs.FirstOrDefault(r => r.Id == id);
    public int NextDeviceId() => Devices.Count == 0 ? 1 : Devices.Max(d => d.Id) + 1;
    public int NextRunId() => Runs.Count == 0 ? 1 : Runs.Max(r => r.Id) + 1;
    [JsonIgnore] public bool IsEmpty => Devices.Count == 0 && Runs.Count == 0 && (Hvac == null || Hvac.IsEmpty);
}

/// <summary>Tiện ích hình học.</summary>
public static class MepGeom
{
    public static double Length(IList<MepPt> pts)
    {
        double s = 0;
        for (int i = 1; i < pts.Count; i++) s += Dist(pts[i - 1], pts[i]);
        return s;
    }
    public static double Dist(MepPt a, MepPt b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
    public static double Dist2D(double x1, double y1, double x2, double y2) => Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));

    /// <summary>Chiếu điểm lên đoạn AB: trả (t ∈ [0,1], khoảng cách, điểm chiếu).</summary>
    public static (double T, double Dist, double Px, double Py) Project(double x, double y, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay, L2 = dx * dx + dy * dy;
        double t = L2 < 1e-12 ? 0 : ((x - ax) * dx + (y - ay) * dy) / L2;
        t = Math.Clamp(t, 0, 1);
        double px = ax + t * dx, py = ay + t * dy;
        return (t, Dist2D(x, y, px, py), px, py);
    }

    /// <summary>Hai đoạn 2D cắt nhau (kể cả chạm).</summary>
    public static bool SegmentsIntersect(double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4)
    {
        static double Cr(double ax, double ay, double bx, double by, double cx, double cy) => (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
        double d1 = Cr(x3, y3, x4, y4, x1, y1), d2 = Cr(x3, y3, x4, y4, x2, y2);
        double d3 = Cr(x1, y1, x2, y2, x3, y3), d4 = Cr(x1, y1, x2, y2, x4, y4);
        if (((d1 > 1e-12 && d2 < -1e-12) || (d1 < -1e-12 && d2 > 1e-12)) && ((d3 > 1e-12 && d4 < -1e-12) || (d3 < -1e-12 && d4 > 1e-12))) return true;
        return false;
    }

    /// <summary>Đoạn 2D cắt hình chữ nhật (trục song song).</summary>
    public static bool SegmentHitsRect(double x1, double y1, double x2, double y2, double rx1, double ry1, double rx2, double ry2)
    {
        bool Inside(double x, double y) => x > rx1 + 1e-9 && x < rx2 - 1e-9 && y > ry1 + 1e-9 && y < ry2 - 1e-9;
        if (Inside(x1, y1) || Inside(x2, y2)) return true;
        return SegmentsIntersect(x1, y1, x2, y2, rx1, ry1, rx2, ry1) || SegmentsIntersect(x1, y1, x2, y2, rx2, ry1, rx2, ry2)
            || SegmentsIntersect(x1, y1, x2, y2, rx2, ry2, rx1, ry2) || SegmentsIntersect(x1, y1, x2, y2, rx1, ry2, rx1, ry1);
    }
}

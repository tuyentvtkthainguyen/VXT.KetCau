using KetCauVXT.Model;

namespace KetCauVXT.Import.Arch;

// =====================================================================
// HỢP ĐỒNG DỮ LIỆU: NHẬP BẢN VẼ KIẾN TRÚC SƠ BỘ → TRIỂN KHAI KẾT CẤU
//   ArchPlanReader.Read(path, opt)        → ArchDrawing (các mặt bằng tầng đã nhận diện)
//   ArchToStructure.Propose(drawing, opt) → StructuralProposal (cột, dầm, sàn, tải đề xuất – xem trước, sửa được)
//   ArchToStructure.Apply(project, drawing, proposal, opt) → ImportReport (ghi vào Project)
// Mọi toạ độ: m, hệ toạ độ của mặt bằng tầng 1 sau khi dời gốc về giao trục đầu tiên.
// =====================================================================

/// <summary>Công năng phòng – quyết định tải hoàn thiện và hoạt tải (TCVN 2737:2023).</summary>
public enum RoomKind { Unknown, Living, Bedroom, Kitchen, WC, Corridor, Stair, Balcony, Storage, Garage, Worship, Office, Shop, RoofTerrace, Void }

/// <summary>Bảng tra công năng phòng: từ khoá nhận dạng, tải hoàn thiện (kN/m²), hoạt tải tiêu chuẩn (kN/m²).</summary>
public class RoomType
{
    public RoomKind Kind { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Từ khoá (không dấu, chữ hoa) để nhận dạng chữ ghi tên phòng trên bản vẽ</summary>
    public string[] Keys { get; set; } = Array.Empty<string>();
    /// <summary>Tĩnh tải hoàn thiện (lát nền, vữa, trần, chống thấm), kN/m²</summary>
    public double Finish { get; set; }
    /// <summary>Hoạt tải tiêu chuẩn toàn phần, kN/m²</summary>
    public double Live { get; set; }
    /// <summary>Không có sàn (ô thông tầng) / sàn đặc biệt (cầu thang tính riêng)</summary>
    public bool NoSlab { get; set; }
}

public static class RoomCatalog
{
    /// <summary>Bảng mặc định – người dùng sửa được trên giao diện. Hoạt tải theo TCVN 2737:2023 Bảng 4 (nhà ở, văn phòng, cửa hàng);
    /// giá trị gara, kho lấy thiên về an toàn – cần đối chiếu công năng thực tế.</summary>
    public static List<RoomType> Default() => new()
    {
        new() { Kind = RoomKind.Bedroom,  Name = "Phòng ngủ",         Keys = new[] { "NGU", "BED" },                                    Finish = 1.2, Live = 1.5 },
        new() { Kind = RoomKind.Living,   Name = "Phòng khách/SH",    Keys = new[] { "KHACH", "SINH HOAT", "P.SH", "LIVING", "GIA DINH" }, Finish = 1.2, Live = 1.5 },
        new() { Kind = RoomKind.Kitchen,  Name = "Bếp, phòng ăn",     Keys = new[] { "BEP", "AN", "KITCHEN", "DINING" },              Finish = 1.2, Live = 1.5 },
        new() { Kind = RoomKind.WC,       Name = "Vệ sinh, tắm",      Keys = new[] { "WC", "VE SINH", "VS", "TAM", "TOILET" },        Finish = 2.0, Live = 1.5 },
        new() { Kind = RoomKind.Worship,  Name = "Phòng thờ",         Keys = new[] { "THO" },                                          Finish = 1.2, Live = 1.5 },
        new() { Kind = RoomKind.Office,   Name = "Làm việc",          Keys = new[] { "LAM VIEC", "VAN PHONG", "OFFICE", "DOC SACH" },  Finish = 1.2, Live = 2.0 },
        new() { Kind = RoomKind.Shop,     Name = "Kinh doanh",        Keys = new[] { "KINH DOANH", "CUA HANG", "SHOP", "BAN HANG" },  Finish = 1.2, Live = 4.0 },
        new() { Kind = RoomKind.Corridor, Name = "Sảnh, hành lang",   Keys = new[] { "SANH", "HANH LANG", "LOBBY", "HALL" },           Finish = 1.2, Live = 3.0 },
        new() { Kind = RoomKind.Stair,    Name = "Cầu thang",         Keys = new[] { "CAU THANG", "THANG", "STAIR", "LEN", "UP", "XUONG" }, Finish = 1.2, Live = 3.0, NoSlab = true },
        new() { Kind = RoomKind.Balcony,  Name = "Ban công, lô gia",  Keys = new[] { "BAN CONG", "LO GIA", "LOGIA", "BALCONY" },       Finish = 1.2, Live = 2.0 },
        new() { Kind = RoomKind.Storage,  Name = "Kho",               Keys = new[] { "KHO", "STORE" },                                 Finish = 1.2, Live = 4.0 },
        new() { Kind = RoomKind.Garage,   Name = "Gara, để xe",       Keys = new[] { "GARA", "GARAGE", "DE XE", "XE" },                Finish = 1.2, Live = 5.0 },
        new() { Kind = RoomKind.RoofTerrace, Name = "Sân thượng, mái BTCT", Keys = new[] { "SAN THUONG", "MAI BTCT", "SAN PHOI", "TERRACE" }, Finish = 2.5, Live = 1.5 },
        new() { Kind = RoomKind.Void,     Name = "Thông tầng",        Keys = new[] { "THONG TANG", "O THOANG", "GIENG TROI", "KHOANG THONG", "LOI THONG", "VOID" }, Finish = 0, Live = 0, NoSlab = true },
    };
}

public class ArchWall
{
    /// <summary>Tim tường (đã quy về song song trục X hoặc Y nếu lệch &lt; 1°)</summary>
    public double Ax, Ay, Bx, By;
    /// <summary>Chiều dày tường, m (0,11 / 0,22 …)</summary>
    public double Thickness;
    /// <summary>Tường bao ngoài nhà</summary>
    public bool Exterior;
    /// <summary>Tổng chiều dài lỗ cửa trên đoạn tường, m</summary>
    public double OpeningLength;
    public string Layer = "";
    public double Length => Math.Sqrt((Bx - Ax) * (Bx - Ax) + (By - Ay) * (By - Ay));
    public bool AlongX => Math.Abs(By - Ay) < 1e-6;
    public bool AlongY => Math.Abs(Bx - Ax) < 1e-6;
}

public class ArchGrid
{
    public string Name = "";
    /// <summary>Trục tự lập theo tường (bản vẽ không có lớp trục) – tên không dùng để khớp tầng</summary>
    public bool Auto;
    public GridDir Dir;
    /// <summary>Toạ độ trục (X với trục dọc theo phương Y …), m</summary>
    public double Coord;
}

public class ArchColumn
{
    /// <summary>Tâm cột</summary>
    public double X, Y;
    /// <summary>Kích thước theo X, Y (m)</summary>
    public double Bx, By;
}

public class ArchRoom
{
    public string Text = "";
    public RoomKind Kind;
    public double X, Y;
}

/// <summary>Vùng đặc biệt trên mặt bằng: cầu thang, thông tầng (không có sàn).</summary>
public class ArchZone
{
    public RoomKind Kind;
    public double X0, Y0, X1, Y1;
    public string Note = "";
    public bool Contains(double x, double y, double tol = 0) => x >= X0 - tol && x <= X1 + tol && y >= Y0 - tol && y <= Y1 + tol;
}

public class ArchOpening
{
    public double X, Y;
    public double Width;
    /// <summary>"Cửa đi" / "Cửa sổ"</summary>
    public string Kind = "";
}

/// <summary>Đoạn thẳng để vẽ nền bản vẽ kiến trúc khi xem trước (đã dời về hệ toạ độ chung).</summary>
public struct PreviewLine
{
    public double X1, Y1, X2, Y2;
    /// <summary>0 = nét khác, 1 = tường, 2 = trục, 3 = cột, 4 = cửa, 5 = cầu thang</summary>
    public int Kind;
    public PreviewLine(double x1, double y1, double x2, double y2, int kind) { X1 = x1; Y1 = y1; X2 = x2; Y2 = y2; Kind = kind; }
}

/// <summary>Một mặt bằng tầng kiến trúc.</summary>
public class ArchFloorPlan
{
    /// <summary>Tên hình đọc trên bản vẽ, vd "MẶT BẰNG TẦNG 2"</summary>
    public string Title { get; set; } = "";
    /// <summary>Số thứ tự tầng (1 = tầng trệt). Tum = N+1, mái = 99.</summary>
    public int FloorNo { get; set; }
    public bool IsRoofPlan { get; set; }
    public bool IsTum { get; set; }
    /// <summary>Dùng mặt bằng này khi triển khai</summary>
    public bool Include { get; set; } = true;
    /// <summary>Chiều cao tầng (từ mặt sàn tầng này đến mặt sàn tầng trên), m</summary>
    public double Height { get; set; } = 3.3;
    /// <summary>Phạm vi hình trên bản vẽ gốc (m, trước khi dời)</summary>
    public double X0 { get; set; }
    public double Y0 { get; set; }
    public double X1 { get; set; }
    public double Y1 { get; set; }
    /// <summary>Vector dời từ toạ độ bản vẽ gốc về hệ chung (khớp trục với tầng 1)</summary>
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }

    public List<ArchWall> Walls { get; set; } = new();
    public List<ArchGrid> Grids { get; set; } = new();
    public List<ArchColumn> Columns { get; set; } = new();
    public List<ArchRoom> Rooms { get; set; } = new();
    public List<ArchZone> Zones { get; set; } = new();
    public List<ArchOpening> Openings { get; set; } = new();
    public List<PreviewLine> Preview { get; set; } = new();
    /// <summary>Đường bao sàn tầng (đa giác trực giao), hệ toạ độ chung</summary>
    public List<(double X, double Y)> Outline { get; set; } = new();
    public List<string> Notes { get; set; } = new();

    public override string ToString() => $"{Title} – {Walls.Count} tường, {Grids.Count} trục, {Columns.Count} cột KT, {Rooms.Count} phòng";
}

public class ArchDrawing
{
    public string Source { get; set; } = "";
    public List<ArchFloorPlan> Floors { get; set; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Info { get; } = new();
}

public class ArchImportOptions
{
    // ---- nhận dạng ----
    /// <summary>Từ khoá tên lớp (chứa chuỗi, không phân biệt hoa thường, cách nhau dấu phẩy)</summary>
    public string WallLayers { get; set; } = "TUONG,WALL,A-WALL,TƯỜNG,XAY";
    public string GridLayers { get; set; } = "TRUC,GRID,AXIS,A-GRID,TRỤC";
    public string ColumnLayers { get; set; } = "COT,COLUMN,A-COLS,S-COLS,CỘT";
    public string DoorLayers { get; set; } = "CUA,DOOR,WINDOW,A-DOOR,A-GLAZ,CỬA";
    public string StairLayers { get; set; } = "THANG,STAIR,A-FLOR-STRS";
    /// <summary>&gt; 0: hệ số đổi đơn vị bản vẽ → m; 0 = theo $INSUNITS (mặc định mm)</summary>
    public double ScaleOverride { get; set; }
    public double MinWallThickness { get; set; } = 0.08;
    public double MaxWallThickness { get; set; } = 0.45;
    /// <summary>Tường vẽ 1 nét: lấy chiều dày này</summary>
    public double DefaultWallThickness { get; set; } = 0.11;
    /// <summary>Tường ngắn hơn giá trị này bỏ qua (m)</summary>
    public double MinWallLength { get; set; } = 0.6;
    public List<RoomType> Rooms { get; set; } = RoomCatalog.Default();

    // ---- tầng ----
    public double BaseElevation { get; set; } = -0.6;
    public double GroundStoreyHeight { get; set; } = 3.6;
    public double TypicalStoreyHeight { get; set; } = 3.3;
    public double TumHeight { get; set; } = 3.0;
    /// <summary>Chiều cao tường chắn mái (m) đặt trên dầm biên mái</summary>
    public double ParapetHeight { get; set; } = 0.9;

    // ---- triển khai kết cấu ----
    public string Concrete { get; set; } = "B25";
    /// <summary>Nhịp dầm lớn nhất cho phép trước khi chèn thêm cột (m)</summary>
    public double MaxBeamSpan { get; set; } = 6.0;
    /// <summary>Cạnh ngắn ô sàn lớn nhất trước khi thêm dầm phụ chia ô (m)</summary>
    public double MaxSlabSpan { get; set; } = 4.5;
    /// <summary>Công xôn (ban công) lớn nhất (m)</summary>
    public double MaxCantilever { get; set; } = 2.0;
    /// <summary>Đặt dầm phụ đỡ tường không nằm trên trục</summary>
    public bool BeamsUnderWalls { get; set; } = true;
    /// <summary>Bề rộng dầm mặc định (m)</summary>
    public double BeamWidth { get; set; } = 0.22;
    public double MinColumnSize { get; set; } = 0.22;
    /// <summary>Tải trung bình 1 sàn để chọn sơ bộ cột (kN/m²)</summary>
    public double ColumnPreloadPerFloor { get; set; } = 12.0;
    /// <summary>Trọng lượng riêng khối xây (kN/m³): gạch đặc 18, gạch rỗng 15, bê tông nhẹ 7–8</summary>
    public double WallUnitWeight { get; set; } = 18.0;
    /// <summary>Chiều dày lớp trát mỗi mặt (m)</summary>
    public double PlasterThickness { get; set; } = 0.015;
    /// <summary>Chiều cao cửa đi trung bình để trừ tải tường (m)</summary>
    public double DoorHeight { get; set; } = 2.2;
    public double RoofFinish { get; set; } = 2.5;
    public double RoofLive { get; set; } = 0.75;
    public bool FixedBase { get; set; } = true;
}

// ---------------------------------------------------------------------
// ĐỀ XUẤT KẾT CẤU (xem trước, sửa được trước khi ghi vào mô hình)
// ---------------------------------------------------------------------
public enum BeamRole { Main, Secondary, Cantilever, Edge }

public class PropColumn
{
    public double X, Y;
    /// <summary>Kích thước theo X, Y (m)</summary>
    public double Bx, By;
    /// <summary>Tầng cao nhất cột đi lên (tên cao trình, vd "MAI")</summary>
    public string TopLevel = "";
    /// <summary>Tiết diện theo từng đoạn tầng: tên cao trình đỉnh đoạn → tên tiết diện</summary>
    public Dictionary<string, string> SectionByLevel = new();
    public double TributaryArea;
    public string Note = "";
    public bool Include = true;
}

public class PropBeam
{
    public double Ax, Ay, Bx, By;
    public BeamRole Role;
    public double B, H;
    public string Section = "";
    /// <summary>Tải tường trên dầm (tiêu chuẩn, kN/m), đã trừ lỗ cửa – gán vào HTHT</summary>
    public double WallLoad;
    /// <summary>Tĩnh tải khác trên dầm (vd. phần cầu thang truyền vào dầm viền lỗ), kN/m – HTHT</summary>
    public double OtherDead;
    /// <summary>Hoạt tải trên dầm (vd. cầu thang), kN/m – HT</summary>
    public double LiveLoad;
    /// <summary>Tên trục nếu dầm nằm trên trục (dùng đặt nhóm thiết kế)</summary>
    public string Grid = "";
    public string Note = "";
    public bool Include = true;
    public double Length => Math.Sqrt((Bx - Ax) * (Bx - Ax) + (By - Ay) * (By - Ay));
}

public class PropSlab
{
    public double X0, Y0, X1, Y1;
    public double Thickness;
    public RoomKind Kind;
    public string RoomName = "";
    public double Finish, Live;
    public bool Include = true;
}

public class ProposalLevel
{
    /// <summary>Tên cao trình (tầng kết cấu) – "T2", "T3", …, "MAI", "MAITUM"</summary>
    public string Name = "";
    public double Elevation;
    public double StoreyHeight;
    public bool Roof;
    /// <summary>Mặt bằng kiến trúc có tường đặt trên cao trình này</summary>
    public string FromPlan = "";
    public List<PropBeam> Beams = new();
    public List<PropSlab> Slabs = new();
    /// <summary>Lỗ sàn: cầu thang, thông tầng</summary>
    public List<ArchZone> Holes = new();
    public List<(double X, double Y)> Outline = new();
}

public class StructuralProposal
{
    public List<double> GridX = new();
    public List<double> GridY = new();
    public List<string> GridXNames = new();
    public List<string> GridYNames = new();
    public List<double> StoreyHeights = new();
    public List<PropColumn> Columns = new();
    public List<ProposalLevel> Levels = new();
    public List<string> Notes = new();
    public List<string> Warnings = new();
}

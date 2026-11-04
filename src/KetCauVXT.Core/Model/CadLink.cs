using System.Text.Json.Serialization;

namespace KetCauVXT.Model;

// =====================================================================
// BẢN VẼ LIÊN KẾT (link DWG/DXF như Xref / "Import DXF as background" của ETABS):
// bản vẽ AutoCAD được gắn vào dự án làm nền (underlay) cho mặt bằng tầng, bắt điểm vào nét bản vẽ, tự cập nhật khi file đổi,
// so sánh bản vẽ ↔ mô hình (Import/Link/CadLinkSync.cs). Hình học đã đọc giữ ở bộ nhớ đệm Import/Link/CadLinkGeometry.cs (không lưu JSON).
// Toạ độ mô hình = Offset + R(Rotation)·Scale·(điểm bản vẽ (m) − Base).
// =====================================================================

/// <summary>Một bản vẽ AutoCAD (DWG/DXF) liên kết vào dự án. Lưu trong <see cref="Project.CadLinks"/> (file cũ không có → rỗng).</summary>
public class CadLink
{
    /// <summary>Giá trị <see cref="Story"/> khi bản vẽ hiện trên mọi tầng.</summary>
    public const string AllStories = "Tất cả tầng";

    /// <summary>Mã định danh (khoá bộ nhớ đệm hình học, không đổi khi hoàn tác / mở lại).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>Tên hiển thị (mặc định = tên file).</summary>
    public string Name { get; set; } = "";
    /// <summary>Đường dẫn tuyệt đối lần lưu gần nhất.</summary>
    public string FilePath { get; set; } = "";
    /// <summary>Đường dẫn tương đối so với thư mục file dự án (ghi khi lưu dự án) – dời cả thư mục dự án + bản vẽ vẫn giữ liên kết.</summary>
    public string RelativePath { get; set; } = "";
    /// <summary>Tầng hiển thị bản vẽ (tên tầng) hoặc <see cref="AllStories"/> / "" = mọi tầng.</summary>
    public string Story { get; set; } = AllStories;

    /// <summary>Điểm gốc trên bản vẽ (m, sau khi đổi đơn vị) – điểm này đặt vào (OffsetX, OffsetY) của mô hình.</summary>
    public double BaseX { get; set; }
    public double BaseY { get; set; }
    /// <summary>Điểm chèn trên mô hình (m) – độ dời dx, dy khi Base = (0; 0).</summary>
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    /// <summary>Góc xoay bản vẽ quanh điểm gốc (độ, ngược chiều kim đồng hồ).</summary>
    public double Rotation { get; set; }
    /// <summary>Hệ số phóng thêm (1 = đúng kích thước).</summary>
    public double Scale { get; set; } = 1.0;
    /// <summary>Đơn vị bản vẽ → m do người dùng chọn (0 = tự động theo $INSUNITS; 0,001 = mm; 0,01 = cm; 1 = m).</summary>
    public double UnitScale { get; set; }
    /// <summary>Hệ số đơn vị đã dùng lần đọc gần nhất (chỉ để hiển thị).</summary>
    public double DetectedUnitScale { get; set; }
    /// <summary>$INSUNITS lần đọc gần nhất.</summary>
    public int InsUnits { get; set; }

    public bool Visible { get; set; } = true;
    /// <summary>Độ đậm nét nền 0,05 … 1.</summary>
    public double Opacity { get; set; } = 0.55;
    /// <summary>true = vẽ theo màu layer (nhạt theo Opacity); false = xám nhạt.</summary>
    public bool ByLayerColor { get; set; }
    /// <summary>Hiện chữ (TEXT/MTEXT) của bản vẽ.</summary>
    public bool ShowText { get; set; }
    /// <summary>Bắt điểm vào nét bản vẽ này (đầu nét, trung điểm, giao điểm).</summary>
    public bool Snap { get; set; } = true;
    /// <summary>Tự đọc lại khi file thay đổi (AutoCAD lưu bản vẽ).</summary>
    public bool AutoReload { get; set; } = true;
    /// <summary>Các layer đang ẩn trên nền (chỉ hiển thị / bắt điểm – không ảnh hưởng nhận dạng).</summary>
    public List<string> HiddenLayers { get; set; } = new();
    /// <summary>Gán layer → nhóm cấu kiện (CadCategory) dùng khi "Cập nhật mô hình từ link"; rỗng = theo chuẩn layer CAD.</summary>
    public List<LayerMapEntry> LayerMap { get; set; } = new();

    /// <summary>Thời điểm ghi file (UTC) + kích thước + mã băm SHA-256 lần đọc gần nhất – phát hiện bản vẽ thay đổi.</summary>
    public DateTime? LastWriteUtc { get; set; }
    public long LastLength { get; set; }
    public string LastHash { get; set; } = "";
    /// <summary>Mã băm bản vẽ lần "Cập nhật mô hình từ link" gần nhất (để báo "bản vẽ đã đổi từ lần đồng bộ trước").</summary>
    public string SyncedHash { get; set; } = "";

    [JsonIgnore]
    public bool IsAllStories => string.IsNullOrWhiteSpace(Story) || Story == AllStories;

    /// <summary>Bản vẽ hiện trên tầng story ("(Tất cả)" của mặt bằng → mọi bản vẽ).</summary>
    public bool AppliesTo(string story) => IsAllStories || string.Equals(Story, story, StringComparison.Ordinal) || story == null;

    /// <summary>Đổi toạ độ bản vẽ (m) → toạ độ mô hình (m).</summary>
    public (double X, double Y) ToModel(double x, double y)
    {
        double s = Scale > 0 ? Scale : 1, a = Rotation * Math.PI / 180, c = Math.Cos(a) * s, sn = Math.Sin(a) * s;
        double dx = x - BaseX, dy = y - BaseY;
        return (OffsetX + c * dx - sn * dy, OffsetY + sn * dx + c * dy);
    }

    /// <summary>Đổi toạ độ mô hình → toạ độ bản vẽ (m).</summary>
    public (double X, double Y) ToDrawing(double x, double y)
    {
        double s = Scale > 0 ? Scale : 1, a = -Rotation * Math.PI / 180, c = Math.Cos(a) / s, sn = Math.Sin(a) / s;
        double dx = x - OffsetX, dy = y - OffsetY;
        return (BaseX + c * dx - sn * dy, BaseY + sn * dx + c * dy);
    }

    /// <summary>Chuỗi đặc trưng phép biến đổi (đổi → dựng lại hình đã biến đổi trong bộ đệm).</summary>
    [JsonIgnore]
    public string TransformKey => FormattableString.Invariant($"{BaseX:R}|{BaseY:R}|{OffsetX:R}|{OffsetY:R}|{Rotation:R}|{Scale:R}");

    /// <summary>Căn bản vẽ theo một cặp điểm: điểm bản vẽ (toạ độ mô hình hiện tại của nó) → điểm mô hình đích (giữ góc xoay, tỷ lệ).</summary>
    public void AlignByPoints((double X, double Y) currentModelPoint, (double X, double Y) target)
    {
        var d = ToDrawing(currentModelPoint.X, currentModelPoint.Y);
        BaseX = Math.Round(d.X, 6); BaseY = Math.Round(d.Y, 6);
        OffsetX = Math.Round(target.X, 6); OffsetY = Math.Round(target.Y, 6);
    }

    /// <summary>Căn theo hai cặp điểm (điểm bản vẽ → điểm mô hình, toạ độ mô hình hiện tại): dời + xoay; scaleToo = cả tỷ lệ.</summary>
    public void AlignByTwoPairs((double X, double Y) a0, (double X, double Y) a1, (double X, double Y) b0, (double X, double Y) b1, bool scaleToo = false)
    {
        var da = ToDrawing(a0.X, a0.Y); var db = ToDrawing(b0.X, b0.Y);
        double vx = db.X - da.X, vy = db.Y - da.Y, wx = b1.X - a1.X, wy = b1.Y - a1.Y;
        double lv = Math.Sqrt(vx * vx + vy * vy), lw = Math.Sqrt(wx * wx + wy * wy);
        if (lv < 1e-9 || lw < 1e-9) { AlignByPoints(a0, a1); return; }
        double rot = (Math.Atan2(wy, wx) - Math.Atan2(vy, vx)) * 180 / Math.PI;
        rot = Math.Round(((rot % 360) + 540) % 360 - 180, 6);
        Rotation = rot;
        if (scaleToo) Scale = Math.Round(lw / lv, 9);
        BaseX = Math.Round(da.X, 6); BaseY = Math.Round(da.Y, 6);
        OffsetX = Math.Round(a1.X, 6); OffsetY = Math.Round(a1.Y, 6);
    }

    // ------------------------------------------------------------------
    // Đường dẫn
    // ------------------------------------------------------------------
    /// <summary>Đường dẫn thực của bản vẽ: ưu tiên đường dẫn tương đối theo thư mục dự án (đã dời thư mục), rồi đường dẫn tuyệt đối,
    /// rồi file cùng tên trong thư mục dự án. null = không tìm thấy ("Không tìm thấy bản vẽ link – chọn lại").</summary>
    public string Resolve(string projectPath)
    {
        string dir = null;
        try { if (!string.IsNullOrEmpty(projectPath)) dir = Path.GetDirectoryName(Path.GetFullPath(projectPath)); } catch (Exception) { dir = null; }
        try
        {
            if (dir != null && !string.IsNullOrWhiteSpace(RelativePath))
            {
                string c = Path.GetFullPath(Path.Combine(dir, RelativePath));
                if (File.Exists(c)) return c;
            }
        }
        catch (Exception) { /* đường dẫn hỏng */ }
        try { if (!string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath)) return Path.GetFullPath(FilePath); } catch (Exception) { }
        try
        {
            string fn = Path.GetFileName(!string.IsNullOrWhiteSpace(FilePath) ? FilePath : RelativePath ?? "");
            if (dir != null && !string.IsNullOrEmpty(fn))
            {
                string c = Path.Combine(dir, fn);
                if (File.Exists(c)) return c;
            }
        }
        catch (Exception) { }
        return null;
    }

    /// <summary>Ghi lại đường dẫn tương đối theo vị trí file dự án (gọi khi lưu dự án). Khác ổ đĩa → để trống.</summary>
    public void UpdateRelative(string projectPath)
    {
        if (string.IsNullOrEmpty(projectPath) || string.IsNullOrWhiteSpace(FilePath)) return;
        try
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(projectPath));
            string full = Path.GetFullPath(FilePath);
            if (!string.Equals(Path.GetPathRoot(dir), Path.GetPathRoot(full), StringComparison.OrdinalIgnoreCase)) { RelativePath = ""; return; }
            RelativePath = Path.GetRelativePath(dir, full);
        }
        catch (Exception) { RelativePath = ""; }
    }

    [JsonIgnore]
    public string DisplayName => !string.IsNullOrWhiteSpace(Name) ? Name : Path.GetFileName(FilePath ?? "");

    public override string ToString() => $"{DisplayName} [{(IsAllStories ? AllStories : Story)}]";
}

public partial class Project
{
    /// <summary>Bản vẽ AutoCAD liên kết làm nền mặt bằng (Model/CadLink.cs). File dự án cũ không có mục này → rỗng.</summary>
    public List<CadLink> CadLinks { get; set; } = new();

    /// <summary>Ghi đường dẫn tương đối của mọi bản vẽ liên kết theo vị trí file dự án (trước khi lưu).</summary>
    public void UpdateCadLinkPaths(string projectPath)
    {
        foreach (var l in CadLinks ?? new List<CadLink>()) l?.UpdateRelative(projectPath);
    }
}

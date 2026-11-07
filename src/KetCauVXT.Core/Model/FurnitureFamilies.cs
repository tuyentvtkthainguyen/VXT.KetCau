namespace KetCauVXT.Model;

// =====================================================================
// HỌ ĐỒ ĐẠC THAM SỐ (kiểu Revit family): hình 3D sinh từ kích thước Rộng × Sâu × Cao của từng đồ đạc,
// đổi kích thước là hình tự dựng lại (chân bàn, mặt bàn, đầu giường, tay sofa… giữ đúng tỷ lệ cấu tạo).
// Toạ độ địa phương: x dọc bề rộng (−W/2…W/2), y theo chiều sâu (−D/2 = lưng, áp tường khi bắt điểm; +D/2 = mặt trước), z từ đáy.
// Có sẵn trong "Thư viện 3D" (nguồn "Họ tham số"); đồ đạc nhập từ SketchUp / Revit vẫn dùng khối bao như trước.
// =====================================================================
public sealed record FamilyPart(string Name, double X0, double Y0, double Z0, double X1, double Y1, double Z1, double[] Color);

public sealed class FurnitureFamily
{
    public string Name { get; init; } = "";
    public string Category { get; init; } = "Nội thất";
    public Discipline Discipline { get; init; } = Discipline.KienTruc;
    public double W { get; init; }
    public double D { get; init; }
    public double H { get; init; }
    /// <summary>Cao độ đáy mặc định so với sàn (tủ bếp trên…)</summary>
    public double Elevation { get; init; }
    public Func<double, double, double, List<FamilyPart>> Parts { get; init; } = (w, d, h) => new();
}

public static class FurnitureFamilies
{
    public const string IdPrefix = "HO/";

    private static readonly double[] Wood = { 0.62, 0.45, 0.28 }, WoodDark = { 0.42, 0.28, 0.17 }, Fabric = { 0.55, 0.6, 0.68 },
        White = { 0.95, 0.95, 0.95 }, Cream = { 0.93, 0.9, 0.82 }, Metal = { 0.6, 0.62, 0.65 }, Stone = { 0.3, 0.3, 0.32 };

    private static FamilyPart Box(string n, double x0, double y0, double z0, double x1, double y1, double z1, double[] c) =>
        new(n, Math.Min(x0, x1), Math.Min(y0, y1), Math.Min(z0, z1), Math.Max(x0, x1), Math.Max(y0, y1), Math.Max(z0, z1), c);

    private static IEnumerable<FamilyPart> Legs(double w, double d, double z1, double s, double inset, double[] c)
    {
        foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
        {
            double cx = sx * (w / 2 - inset - s / 2), cy = sy * (d / 2 - inset - s / 2);
            yield return Box("Chân", cx - s / 2, cy - s / 2, 0, cx + s / 2, cy + s / 2, z1, c);
        }
    }

    public static readonly List<FurnitureFamily> All = new()
    {
        new() { Name = "Bàn ăn", W = 1.6, D = 0.9, H = 0.75, Parts = (w, d, h) => Legs(w, d, h - 0.03, 0.05, 0.05, Wood).Append(Box("Mặt bàn", -w / 2, -d / 2, h - 0.03, w / 2, d / 2, h, Wood)).ToList() },
        new() { Name = "Bàn trà", W = 1.0, D = 0.6, H = 0.45, Parts = (w, d, h) => Legs(w, d, h - 0.03, 0.05, 0.04, WoodDark).Append(Box("Mặt bàn", -w / 2, -d / 2, h - 0.03, w / 2, d / 2, h, WoodDark)).ToList() },
        new() { Name = "Bàn làm việc", W = 1.2, D = 0.6, H = 0.75, Parts = (w, d, h) => new()
            {
                Box("Mặt bàn", -w / 2, -d / 2, h - 0.03, w / 2, d / 2, h, Wood),
                Box("Vách trái", -w / 2, -d / 2, 0, -w / 2 + 0.025, d / 2, h - 0.03, Wood),
                Box("Vách phải", w / 2 - 0.025, -d / 2, 0, w / 2, d / 2, h - 0.03, Wood),
                Box("Hộc kéo", w / 2 - 0.025 - 0.4, -d / 2 + 0.02, h - 0.03 - 0.5, w / 2 - 0.025, d / 2, h - 0.03, WoodDark)
            } },
        new() { Name = "Ghế tựa", W = 0.45, D = 0.5, H = 0.9, Parts = (w, d, h) => Legs(w, d, 0.42, 0.035, 0.02, Wood)
            .Append(Box("Mặt ghế", -w / 2, -d / 2, 0.42, w / 2, d / 2, 0.46, Wood))
            .Append(Box("Tựa lưng", -w / 2, -d / 2, 0.46, w / 2, -d / 2 + 0.04, h, Wood)).ToList() },
        new() { Name = "Giường đôi", W = 1.8, D = 2.0, H = 1.0, Parts = Bed },
        new() { Name = "Giường đơn", W = 1.2, D = 2.0, H = 0.9, Parts = Bed },
        new() { Name = "Tủ quần áo", W = 1.6, D = 0.6, H = 2.2, Parts = (w, d, h) =>
            {
                var l = new List<FamilyPart> { Box("Chân tủ", -w / 2 + 0.03, -d / 2 + 0.03, 0, w / 2 - 0.03, d / 2 - 0.05, 0.08, WoodDark),
                                                Box("Thân tủ", -w / 2, -d / 2, 0.08, w / 2, d / 2 - 0.02, h, Wood) };
                int n = Math.Max(2, (int)Math.Round(w / 0.45));
                for (int k = 0; k < n; k++)
                    l.Add(Box("Cánh tủ", -w / 2 + k * w / n + 0.003, d / 2 - 0.02, 0.09, -w / 2 + (k + 1) * w / n - 0.003, d / 2, h - 0.01, Cream));
                return l;
            } },
        new() { Name = "Sofa 3 chỗ", W = 2.1, D = 0.9, H = 0.85, Parts = (w, d, h) => new()
            {
                Box("Đế", -w / 2, -d / 2, 0.1, w / 2, d / 2, 0.42, Fabric),
                Box("Tựa lưng", -w / 2, -d / 2, 0.42, w / 2, -d / 2 + 0.2, h, Fabric),
                Box("Tay trái", -w / 2, -d / 2, 0.42, -w / 2 + 0.15, d / 2, 0.65, Fabric),
                Box("Tay phải", w / 2 - 0.15, -d / 2, 0.42, w / 2, d / 2, 0.65, Fabric),
                Box("Chân", -w / 2 + 0.05, -d / 2 + 0.05, 0, w / 2 - 0.05, d / 2 - 0.05, 0.1, WoodDark)
            } },
        new() { Name = "Kệ tivi", W = 1.8, D = 0.45, H = 0.5, Parts = (w, d, h) => new()
            {
                Box("Thân kệ", -w / 2, -d / 2, 0.06, w / 2, d / 2, h, WoodDark),
                Box("Chân kệ", -w / 2 + 0.05, -d / 2 + 0.05, 0, w / 2 - 0.05, d / 2 - 0.05, 0.06, Metal)
            } },
        new() { Name = "Tủ bếp dưới", W = 2.4, D = 0.6, H = 0.86, Parts = (w, d, h) => new()
            {
                Box("Chân tủ", -w / 2, -d / 2, 0, w / 2, d / 2 - 0.06, 0.1, Stone),
                Box("Thân tủ", -w / 2, -d / 2, 0.1, w / 2, d / 2 - 0.02, h - 0.03, Cream),
                Box("Mặt đá", -w / 2, -d / 2, h - 0.03, w / 2, d / 2 + 0.02, h, Stone)
            } },
        new() { Name = "Tủ bếp trên", W = 2.4, D = 0.35, H = 0.7, Elevation = 1.5, Parts = (w, d, h) => new() { Box("Thân tủ", -w / 2, -d / 2, 0, w / 2, d / 2, h, Cream) } },
        new() { Name = "Bồn cầu", Category = "Thiết bị vệ sinh", Discipline = Discipline.DienNuoc, W = 0.4, D = 0.7, H = 0.75, Parts = (w, d, h) => new()
            {
                Box("Két nước", -w / 2, -d / 2, 0.4, w / 2, -d / 2 + 0.18, h, White),
                Box("Thân bồn", -w / 2 + 0.03, -d / 2 + 0.08, 0, w / 2 - 0.03, d / 2, 0.4, White)
            } },
        new() { Name = "Lavabo chân đứng", Category = "Thiết bị vệ sinh", Discipline = Discipline.DienNuoc, W = 0.5, D = 0.45, H = 0.85, Parts = (w, d, h) => new()
            {
                Box("Chậu", -w / 2, -d / 2, h - 0.17, w / 2, d / 2, h, White),
                Box("Chân", -0.08, -d / 2, 0, 0.08, -d / 2 + 0.18, h - 0.17, White)
            } },
        new() { Name = "Bồn tắm", Category = "Thiết bị vệ sinh", Discipline = Discipline.DienNuoc, W = 1.7, D = 0.75, H = 0.55, Parts = (w, d, h) => new()
            {
                Box("Thành bồn", -w / 2, -d / 2, 0, w / 2, d / 2, h, White)
            } },
        new() { Name = "Bàn thờ", W = 1.53, D = 0.81, H = 1.27, Parts = (w, d, h) => new()
            {
                Box("Thân", -w / 2, -d / 2, 0.1, w / 2, d / 2, h - 0.05, WoodDark),
                Box("Mặt", -w / 2 - 0.03, -d / 2, h - 0.05, w / 2 + 0.03, d / 2 + 0.03, h, WoodDark),
                Box("Chân", -w / 2 + 0.05, -d / 2 + 0.05, 0, w / 2 - 0.05, d / 2 - 0.05, 0.1, WoodDark)
            } }
    };

    private static List<FamilyPart> Bed(double w, double d, double h) => new()
    {
        Box("Khung giường", -w / 2, -d / 2, 0.1, w / 2, d / 2, 0.32, WoodDark),
        Box("Chân giường", -w / 2 + 0.05, -d / 2 + 0.05, 0, w / 2 - 0.05, d / 2 - 0.05, 0.1, WoodDark),
        Box("Đệm", -w / 2 + 0.03, -d / 2 + 0.06, 0.32, w / 2 - 0.03, d / 2 - 0.03, 0.52, Cream),
        Box("Đầu giường", -w / 2, -d / 2, 0.32, w / 2, -d / 2 + 0.06, h, WoodDark)
    };

    public static FurnitureFamily Find(string name) => string.IsNullOrEmpty(name) ? null : All.FirstOrDefault(f => f.Name == name);

    /// <summary>Các họ dưới dạng mục thư viện (Id "HO/&lt;tên&gt;", nguồn "Họ tham số").</summary>
    public static IEnumerable<LibraryItem> AsLibraryItems() => All.Select(f => new LibraryItem
    {
        Id = IdPrefix + f.Name, Name = f.Name, Category = f.Category, Source = "Họ tham số", Width = f.W, Depth = f.D, Height = f.H, Discipline = f.Discipline
    });

    /// <summary>Mã khối gốc của đồ đạc (bỏ hậu tố ".k" của từng chi tiết): "DD-12.3" → "DD-12".</summary>
    public static string BaseId(string id)
    {
        if (id == null || !id.StartsWith("DD-")) return id;
        int k = id.IndexOf('.');
        return k > 0 ? id[..k] : id;
    }

    /// <summary>Khối 3D các chi tiết của đồ đạc thuộc họ tham số (null nếu không phải họ tham số).</summary>
    public static List<Solid> Solids(Project p, FurnitureItem f)
    {
        var fam = Find(f.Family);
        if (fam == null) return null;
        double ang = f.Rotation * Math.PI / 180, cs = Math.Cos(ang), sn = Math.Sin(ang);
        var x = new Vec3(cs, sn, 0);
        double z = ModelSolids.StoryZ(p, f.Story) + f.Elevation;
        var res = new List<Solid>();
        int k = 0;
        foreach (var pt in fam.Parts(Math.Max(0.1, f.Width), Math.Max(0.1, f.Depth), Math.Max(0.1, f.Height)))
        {
            double wx = f.X + cs * pt.X0 - sn * pt.Y0, wy = f.Y + sn * pt.X0 + cs * pt.Y0;
            res.Add(new Solid
            {
                Id = k == 0 ? $"DD-{f.Id}" : $"DD-{f.Id}.{k}", Name = $"{f.Name} – {pt.Name}", Discipline = f.Discipline,
                Category = string.IsNullOrEmpty(f.Category) ? "Đồ đạc" : f.Category,
                IfcType = f.Discipline == Discipline.DienNuoc ? "IfcFlowTerminal" : "IfcFurnishingElement", Story = f.Story,
                Origin = new Vec3(wx, wy, z + pt.Z0), XDir = x, ZDir = Vec3.UnitZ,
                Profile = Solid.Rect(0, 0, pt.X1 - pt.X0, pt.Y1 - pt.Y0), Depth = Math.Max(0.005, pt.Z1 - pt.Z0), Color = pt.Color
            });
            k++;
        }
        return res;
    }
}

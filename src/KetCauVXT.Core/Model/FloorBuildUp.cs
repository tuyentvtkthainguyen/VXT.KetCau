namespace KetCauVXT.Model;

/// <summary>Một lớp cấu tạo sàn: chiều dày × trọng lượng riêng, hoặc tải cố định (trần treo, đường ống…).</summary>
public class FloorLayer
{
    public string Name { get; set; } = "";
    /// <summary>Chiều dày (m)</summary>
    public double Thickness { get; set; }
    /// <summary>Trọng lượng riêng (kN/m³)</summary>
    public double UnitWeight { get; set; }
    /// <summary>Tải cố định thêm (kN/m²), vd. trần thạch cao 0,15</summary>
    public double FixedLoad { get; set; }
    public double Load => Thickness * UnitWeight + FixedLoad;
}

/// <summary>Cấu tạo sàn theo thiết kế: các lớp hoàn thiện (không gồm bản BTCT – trọng lượng bản thân tính theo tiết diện sàn) + hoạt tải.</summary>
public class FloorBuildUp
{
    public string Name { get; set; } = "";
    public List<FloorLayer> Layers { get; set; } = new();
    /// <summary>Hoạt tải tiêu chuẩn (kN/m²) theo công năng (TCVN 2737:2023)</summary>
    public double Live { get; set; } = 1.5;
    public double Finish => Layers.Sum(l => l.Load);
    public override string ToString() => Name;

    public static FloorLayer L(string n, double tMm, double g, double fixedLoad = 0) => new() { Name = n, Thickness = tMm / 1000.0, UnitWeight = g, FixedLoad = fixedLoad };

    /// <summary>Thư viện cấu tạo sàn mặc định (tên trùng tên công năng SlabUse trong ModelEdit).</summary>
    public static List<FloorBuildUp> Defaults() => new()
    {
        new() { Name = "Sàn phòng ở", Live = 1.5, Layers = { L("Gạch ceramic 600×600", 10, 20), L("Vữa lót XM M75", 30, 18), L("Vữa trát trần", 15, 18), L("Điện nước, trần nhẹ", 0, 0, 0.20) } },
        new() { Name = "Sàn vệ sinh", Live = 1.5, Layers = { L("Gạch chống trơn", 10, 20), L("Vữa lót XM M75", 30, 18), L("Chống thấm 2 lớp", 0, 0, 0.10), L("Vữa/gạch vỡ tạo dốc, hạ cốt 50", 50, 18), L("Vữa trát trần", 15, 18) } },
        new() { Name = "Ban công, lô gia", Live = 2.0, Layers = { L("Gạch chống trơn", 10, 20), L("Vữa lót tạo dốc", 30, 18), L("Chống thấm", 0, 0, 0.10), L("Vữa trát trần", 15, 18) } },
        new() { Name = "Sảnh, hành lang", Live = 3.0, Layers = { L("Gạch granite", 15, 22), L("Vữa lót XM M75", 30, 18), L("Vữa trát trần", 15, 18), L("Điện nước, trần nhẹ", 0, 0, 0.20) } },
        new() { Name = "Kho", Live = 4.0, Layers = { L("Láng vữa XM", 30, 18), L("Vữa trát trần", 15, 18) } },
        new() { Name = "Mái BTCT", Live = 0.75, Layers = { L("2 lớp gạch lá nem", 40, 18), L("Vữa lót", 20, 18), L("Chống thấm", 0, 0, 0.10), L("Bê tông xỉ tạo dốc TB", 60, 14), L("Vữa trát trần", 15, 18) } },
        new() { Name = "Sân thượng", Live = 1.5, Layers = { L("Gạch chống trơn", 10, 20), L("Vữa lót", 30, 18), L("Chống thấm", 0, 0, 0.10), L("Bê tông xỉ tạo dốc TB", 60, 14), L("Vữa trát trần", 15, 18) } },
    };
}

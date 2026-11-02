namespace KetCauVXT.Model;

/// <summary>Kiểu hoàn thiện tường.</summary>
public enum WallFinish { Plaster2Sides, Plaster1Side, NoPlaster }

/// <summary>Loại tường: chiều dày, vật liệu xây, trát, tỷ lệ lỗ cửa mặc định. Tải 1 m² tường = t·γ + số mặt trát·tp·γtrát.</summary>
public class WallType
{
    public string Name { get; set; } = "";
    /// <summary>Chiều dày khối xây (m): 0,11 / 0,22 / 0,33</summary>
    public double Thickness { get; set; } = 0.11;
    /// <summary>Trọng lượng riêng khối xây (kN/m³): gạch đặc 18, gạch rỗng 15, gạch bê tông nhẹ 7–9</summary>
    public double UnitWeight { get; set; } = 18;
    public WallFinish Finish { get; set; } = WallFinish.Plaster2Sides;
    /// <summary>Chiều dày lớp trát mỗi mặt (m)</summary>
    public double PlasterThickness { get; set; } = 0.015;
    public double PlasterUnitWeight { get; set; } = 18;
    /// <summary>Tỷ lệ diện tích lỗ cửa mặc định (0 = tường đặc). Mỗi đoạn tường có thể khai báo diện tích cửa riêng.</summary>
    public double OpeningRatio { get; set; }

    public int PlasterSides => Finish == WallFinish.Plaster2Sides ? 2 : Finish == WallFinish.Plaster1Side ? 1 : 0;
    /// <summary>Tải tiêu chuẩn 1 m² tường đặc (kN/m²)</summary>
    public double WeightPerM2 => Thickness * UnitWeight + PlasterSides * PlasterThickness * PlasterUnitWeight;
    public override string ToString() => Name;

    public static List<WallType> Defaults()
    {
        var l = new List<WallType>();
        foreach (var t in new[] { 0.11, 0.22, 0.33 })
        {
            string tt = (t * 1000).ToString("0");
            l.Add(new WallType { Name = $"Tường {tt} trát 2 mặt", Thickness = t });
            l.Add(new WallType { Name = $"Tường {tt} trát 1 mặt", Thickness = t, Finish = WallFinish.Plaster1Side });
            l.Add(new WallType { Name = $"Tường {tt} không trát", Thickness = t, Finish = WallFinish.NoPlaster });
            l.Add(new WallType { Name = $"Tường {tt} có cửa (≈30%)", Thickness = t, OpeningRatio = 0.30 });
        }
        l.Add(new WallType { Name = "Tường 110 gạch nhẹ AAC", Thickness = 0.11, UnitWeight = 8 });
        l.Add(new WallType { Name = "Tường 220 gạch rỗng", Thickness = 0.22, UnitWeight = 15 });
        return l;
    }
}

/// <summary>Đoạn tường đặt tại vị trí trên một cao trình (tường đứng trên sàn/dầm của tầng Story).
/// Tải tường dồn vào dầm nằm dưới tường (thẳng hàng, trong phạm vi nửa chiều dày + 50 mm); phần không có dầm đỡ quy về tải phân bố lên ô sàn.</summary>
public class WallLine
{
    public int Id { get; set; }
    /// <summary>Cao trình tường đứng (tên tầng của dầm/sàn đỡ tường)</summary>
    public string Story { get; set; } = "";
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public string Type { get; set; } = "";
    /// <summary>Chiều cao tường (m). 0 = tự lấy: chiều cao tầng trên − 0,35 m (tường chắn mái trên tầng mái: 0,9 m)</summary>
    public double Height { get; set; }
    /// <summary>Diện tích cửa trên đoạn tường (m²); &lt; 0 = dùng tỷ lệ cửa của loại tường</summary>
    public double OpeningArea { get; set; } = -1;
    public string Note { get; set; } = "";
    /// <summary>Tường cong: độ vồng có dấu của tim tường so với dây cung (X1,Y1)→(X2,Y2), m – dương = vồng về bên trái
    /// (cùng quy ước với <see cref="FrameElement.CurveMidOffset"/>). 0 = tường thẳng (mặc định, tương thích file cũ).
    /// Tải tường cong đặt lên các dầm cong cùng đường tròn nằm dưới tường, theo chiều dài cung.</summary>
    public double Sagitta { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsCurved => Math.Abs(Sagitta) > 1e-6;
    /// <summary>Chiều dài dây cung (tường thẳng: chiều dài tường), m</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double Chord => Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
    /// <summary>Chiều dài tường, m (tường cong: chiều dài cung)</summary>
    public double Length => IsCurved ? CurvedBeam.ArcLength(Chord, Sagitta) : Chord;
}

using KetCauVXT.Model;
using WallLoadGenerator = KetCauVXT.Loads.WallLoadGenerator;

namespace KetCauVXT.App;

/// <summary>Kết quả tính tải một đoạn tường (giống KetCauVXT.Loads.WallLoadGenerator, dùng để hiển thị).</summary>
internal sealed class WallCalcResult
{
    public WallType Type { get; init; }
    /// <summary>Chiều cao tường dùng tính tải (m)</summary>
    public double H { get; init; }
    public bool AutoHeight { get; init; }
    /// <summary>Tỷ lệ lỗ cửa đã trừ (0…0,9)</summary>
    public double Ratio { get; init; }
    /// <summary>Diện tích cửa (m²)</summary>
    public double OpenArea { get; init; }
    /// <summary>Tải phân bố lên dầm (kN/m) = WeightPerM2·H·(1 − tỷ lệ cửa)</summary>
    public double Q { get; init; }
    /// <summary>Chiều dài tường nằm trên dầm (m)</summary>
    public double Covered { get; init; }
    public double Length { get; init; }
}

internal static class WallMath
{
    public static WallCalcResult Calc(Project p, WallLine w)
    {
        if (p == null || w == null) return null;
        var wt = p.WallTypes.FirstOrDefault(t => t.Name == w.Type);
        double L = w.Length;
        if (wt == null) return new WallCalcResult { Length = L, AutoHeight = w.Height <= 0 };
        var stories = p.Stories.OrderBy(s => s.Elevation).ToList();
        var st = stories.FirstOrDefault(s => s.Name == w.Story);
        double covered = 0, hBeam = 0.35;
        if (st != null)
        {
            try
            {
                var beams = WallLoadGenerator.BeamsUnder(p, w, st.Elevation, wt.Thickness);
                covered = beams.Sum(b => b.len);
                hBeam = beams.Select(b => p.Section(b.f.Section).Props(0.5).Depth).DefaultIfEmpty(0.35).Max();
            }
            catch (Exception) { /* tiết diện thiếu – bỏ qua khi hiển thị */ }
        }
        int idx = st != null ? stories.IndexOf(st) : -1;
        double H = w.Height > 0 ? w.Height
            : idx >= 0 && idx + 1 < stories.Count ? stories[idx + 1].Elevation - st.Elevation - Math.Max(hBeam, 0.35) : 0.9;
        H = Math.Max(0, H);
        double area = L * H;
        double open = WallAssembly.EffectiveOpenArea(p, w, wt, area, H);   // [Kiểu Revit] cửa gắn tường → diện tích thực
        double ratio = area > 1e-9 ? Math.Clamp(open / area, 0, 0.9) : 0;
        return new WallCalcResult
        {
            Type = wt, H = H, AutoHeight = w.Height <= 0, Ratio = ratio, OpenArea = Math.Max(0, open),
            Q = wt.WeightPerM2 * H * (1 - ratio), Covered = Math.Min(covered, L), Length = L
        };
    }

    /// <summary>Đoạn tường có cửa (diện tích cửa riêng &gt; 0 hoặc loại tường có tỷ lệ cửa).</summary>
    public static bool HasDoor(Project p, WallLine w)
    {
        if (w.Openings is { Count: > 0 }) return false;   // [Kiểu Revit] cửa thật gắn tường – vẽ riêng (MainWindow.RevitDraw.cs)
        if (w.OpeningArea > 0) return true;
        if (w.OpeningArea == 0) return false;
        var wt = p.WallTypes.FirstOrDefault(t => t.Name == w.Type);
        return wt != null && wt.OpeningRatio > 0;
    }

    public static string FinishText(WallFinish f) => f switch
    {
        WallFinish.Plaster1Side => "Trát 1 mặt", WallFinish.NoPlaster => "Không trát", _ => "Trát 2 mặt"
    };

    public static WallFinish ParseFinish(string s) => s switch
    {
        "Trát 1 mặt" => WallFinish.Plaster1Side, "Không trát" => WallFinish.NoPlaster, _ => WallFinish.Plaster2Sides
    };

    public static readonly string[] FinishNames = { "Trát 2 mặt", "Trát 1 mặt", "Không trát" };
}

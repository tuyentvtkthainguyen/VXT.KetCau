using System.Globalization;

namespace KetCauVXT.Import.Arch;

/// <summary>Lấy KÍCH THƯỚC LƯỚI TRỤC VÀ TẦNG từ mặt bằng người dùng tải lên:
/// (1) bản vẽ CAD (DWG/DXF) – trục đọc bằng ArchPlanReader (layer nhóm Trục, chữ tên trục; không có thì theo tim tường);
/// (2) ảnh mặt bằng (JPG/PNG) – người dùng hiệu chỉnh tỷ lệ bằng một kích thước đã biết rồi bấm vị trí các trục.
/// Kết quả là chuỗi bước trục theo định dạng ô nhập "3.6 4.2 3*3.3" (m, dấu chấm thập phân).</summary>
public static class GridFromPlan
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Toạ độ trục (m) → bước trục (m) làm tròn theo roundM (vd. 0,01 = 10 mm). Bỏ bước &lt; minSpan (trục trùng / nét nhiễu).</summary>
    public static List<double> Spans(IEnumerable<double> coords, double roundM = 0.01, double minSpan = 0.3)
    {
        var c = coords.Where(double.IsFinite).OrderBy(x => x).ToList();
        var merged = new List<double>();
        foreach (var x in c)
            if (merged.Count == 0 || x - merged[^1] >= minSpan) merged.Add(x);
        var res = new List<double>();
        for (int i = 1; i < merged.Count; i++)
        {
            double s = merged[i] - merged[i - 1];
            if (roundM > 0) s = Math.Round(s / roundM) * roundM;
            res.Add(Math.Round(s, 6));
        }
        return res;
    }

    /// <summary>Chuỗi ô nhập: các bước giống nhau liên tiếp gộp "n*L" (vd. 3.6 3.6 3.6 4.2 → "3*3.6 4.2").</summary>
    public static string SpansText(IList<double> spans)
    {
        var parts = new List<string>();
        int i = 0;
        while (i < spans.Count)
        {
            int j = i;
            while (j + 1 < spans.Count && Math.Abs(spans[j + 1] - spans[i]) < 1e-6) j++;
            string v = spans[i].ToString("0.###", Inv);
            int n = j - i + 1;
            parts.Add(n >= 3 ? n.ToString(Inv) + "*" + v : string.Join(" ", Enumerable.Repeat(v, n)));
            i = j + 1;
        }
        return string.Join(" ", parts);
    }

    /// <summary>Kết quả đọc từ một mặt bằng: bước trục X, Y, tên trục, ghi chú.</summary>
    public sealed class Result
    {
        public List<double> SpansX { get; } = new();
        public List<double> SpansY { get; } = new();
        public List<string> NamesX { get; } = new();
        public List<string> NamesY { get; } = new();
        /// <summary>Toạ độ trục (m, hệ bản vẽ) để vẽ đánh dấu lên hình xem trước.</summary>
        public List<double> CoordsX { get; } = new();
        public List<double> CoordsY { get; } = new();
        /// <summary>Chiều cao tầng (m) nếu bản vẽ có nhiều mặt bằng tầng (dưới → trên); rỗng nếu không xác định.</summary>
        public List<double> Heights { get; } = new();
        public bool AutoFromWalls { get; set; }
        public List<string> Notes { get; } = new();
        public string SpansXText => SpansText(SpansX);
        public string SpansYText => SpansText(SpansY);
        public string HeightsText => SpansText(Heights);
    }

    /// <summary>Trục của một mặt bằng kiến trúc đã đọc.</summary>
    public static Result FromPlan(ArchFloorPlan p, double roundM = 0.01)
    {
        var r = new Result();
        if (p == null) { r.Notes.Add("Không có mặt bằng."); return r; }
        var gx = p.Grids.Where(g => g.Dir == KetCauVXT.Model.GridDir.X).OrderBy(g => g.Coord).ToList();
        var gy = p.Grids.Where(g => g.Dir == KetCauVXT.Model.GridDir.Y).OrderBy(g => g.Coord).ToList();
        r.CoordsX.AddRange(gx.Select(g => g.Coord)); r.CoordsY.AddRange(gy.Select(g => g.Coord));
        r.NamesX.AddRange(gx.Select(g => g.Name ?? "")); r.NamesY.AddRange(gy.Select(g => g.Name ?? ""));
        r.SpansX.AddRange(Spans(r.CoordsX, roundM)); r.SpansY.AddRange(Spans(r.CoordsY, roundM));
        r.AutoFromWalls = p.Grids.Any(g => g.Auto);
        if (r.AutoFromWalls) r.Notes.Add("Bản vẽ không có layer trục – trục lập theo tim tường chính, kiểm tra lại.");
        if (r.SpansX.Count == 0) r.Notes.Add("Không tìm được ≥ 2 trục phương X (trục đứng 1, 2, 3…).");
        if (r.SpansY.Count == 0) r.Notes.Add("Không tìm được ≥ 2 trục phương Y (trục ngang A, B, C…).");
        r.Notes.AddRange(p.Notes);
        return r;
    }

    /// <summary>Đọc bản vẽ CAD: chọn mặt bằng tầng thấp nhất có trục (hoặc floorIndex), chiều cao tầng lấy theo các mặt bằng tầng nếu có ≥ 2.</summary>
    public static Result FromDrawing(ArchDrawing d, int floorIndex = -1, double roundM = 0.01)
    {
        if (d == null || d.Floors.Count == 0)
        {
            var e = new Result(); e.Notes.Add("Bản vẽ không có mặt bằng đọc được."); if (d != null) e.Notes.AddRange(d.Warnings); return e;
        }
        var floors = d.Floors.OrderBy(f => f.FloorNo).ToList();
        var pick = floorIndex >= 0 && floorIndex < d.Floors.Count ? d.Floors[floorIndex]
            : floors.FirstOrDefault(f => f.Grids.Count(g => !g.Auto) >= 4) ?? floors[0];
        var r = FromPlan(pick, roundM);
        var levels = floors.Where(f => !f.IsRoofPlan).ToList();
        if (levels.Count >= 2) r.Heights.AddRange(levels.Select(f => Math.Round(f.Height, 2)));
        return r;
    }

    // ------------------------------------------------------------------
    // ẢNH MẶT BẰNG: hiệu chỉnh tỷ lệ + bấm vị trí trục (toạ độ điểm ảnh)
    // ------------------------------------------------------------------
    /// <summary>Tỷ lệ m / điểm ảnh từ hai điểm bấm và chiều dài thật (m) giữa chúng.</summary>
    public static double MetresPerPixel((double X, double Y) a, (double X, double Y) b, double lengthM)
    {
        double d = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        return d > 1e-9 && lengthM > 0 ? lengthM / d : 0;
    }

    /// <summary>Bước trục từ vị trí trục bấm trên ảnh (điểm ảnh) và tỷ lệ m/điểm ảnh; làm tròn roundM (mặc định 50 mm vì bấm tay).</summary>
    public static List<double> SpansFromPixels(IEnumerable<double> px, double mPerPx, double roundM = 0.05)
        => mPerPx <= 0 ? new List<double>() : Spans(px.Select(v => v * mPerPx), roundM, 0.2);
}

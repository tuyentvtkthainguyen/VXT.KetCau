using System.Globalization;
using System.Text;
using System.Text.Json;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Import.Acad;

/// <summary>Một đối tượng kết quả gửi ngược về AutoCAD (toạ độ BẢN VẼ, m).</summary>
public sealed class AcadResultItem
{
    /// <summary>"text" | "poly"</summary>
    public string Type { get; set; } = "text";
    public string Layer { get; set; } = "";
    public string Story { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>Chiều cao chữ (m)</summary>
    public double H { get; set; }
    /// <summary>Góc chữ (độ)</summary>
    public double Rot { get; set; }
    public string Text { get; set; } = "";
    public bool Closed { get; set; }
    public List<double[]> Pts { get; set; } = new();
}

/// <summary>Gói kết quả gửi về AutoCAD (lệnh VXT_NHAN của add-in).</summary>
public sealed class AcadResultPackage
{
    public int Version { get; set; } = 1;
    public string Kind { get; set; } = "ketqua";
    public string Project { get; set; } = "";
    public string Dwg { get; set; } = "";
    public string Unit { get; set; } = "m";
    public string Created { get; set; } = "";
    /// <summary>true = toạ độ đã đưa về đúng vị trí trên bản vẽ gốc (theo bản vẽ liên kết); false = toạ độ mô hình, add-in hỏi điểm chèn.</summary>
    public bool Aligned { get; set; }
    public List<AcadResultItem> Items { get; set; } = new();
    /// <summary>Bản vẽ chi tiết (DXF, không khung tên) để chèn bằng VXT_CHEN</summary>
    public List<string> Details { get; set; } = new();
    public List<string> Notes { get; set; } = new();

    private static readonly JsonSerializerOptions Opt = new() { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public string ToJson() => JsonSerializer.Serialize(this, Opt);
    public static AcadResultPackage Parse(string json) => JsonSerializer.Deserialize<AcadResultPackage>(json, Opt);
}

/// <summary>[Kết quả về AutoCAD] Dựng gói kết quả thiết kế (tên cấu kiện, tiết diện, thép; khung cột / móng / đài cọc) theo toạ độ bản vẽ gốc
/// (qua bản vẽ liên kết của từng tầng – Model/CadLink.cs). Mỗi tầng một bộ lớp VXT-KQ-…; add-in VXT_NHAN vẽ vào bản vẽ đang mở.
/// Chỉ ghi file cục bộ, không gửi đi đâu.</summary>
public static class AcadResults
{
    public const string LCot = "VXT-KQ-COT", LDam = "VXT-KQ-DAM", LSan = "VXT-KQ-SAN", LMong = "VXT-KQ-MONG", LChu = "VXT-KQ-CHU";

    private static string F(double v, string f = "0.##") => v.ToString(f, CultureInfo.InvariantCulture);
    private static string Mm(double m) => Math.Round(m * 1000).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>Bản vẽ liên kết dùng đổi toạ độ của tầng: liên kết riêng của tầng, không có thì liên kết "mọi tầng".</summary>
    public static CadLink LinkFor(Project p, string story) =>
        p.CadLinks?.FirstOrDefault(l => !l.IsAllStories && string.Equals(l.Story, story, StringComparison.Ordinal))
        ?? p.CadLinks?.FirstOrDefault(l => l.IsAllStories);

    public static AcadResultPackage Build(Project p, DesignSummary s, double textH = 0.25)
    {
        var pk = new AcadResultPackage { Project = p?.Name ?? "", Created = DateTime.Now.ToString("s", CultureInfo.InvariantCulture) };
        if (p == null || s == null) return pk;
        var stories = p.Stories.OrderBy(x => x.Elevation).Select(x => x.Name).ToList();
        bool anyLink = p.CadLinks != null && p.CadLinks.Count > 0;
        pk.Aligned = anyLink && stories.All(st => LinkFor(p, st) != null);
        pk.Dwg = p.CadLinks?.FirstOrDefault()?.FilePath ?? "";
        if (!pk.Aligned) pk.Notes.Add(anyLink ? "Có tầng chưa có bản vẽ liên kết – các tầng đó theo toạ độ mô hình (VXT_NHAN hỏi điểm chèn)."
                                                : "Mô hình chưa liên kết bản vẽ AutoCAD – toạ độ theo mô hình, VXT_NHAN hỏi điểm chèn; các tầng đặt cạnh nhau theo phương X.");
        // tầng không liên kết: đặt cạnh nhau theo X
        var bb = p.Nodes.Count == 0 ? (0.0, 0.0, 0.0, 0.0) : (p.Nodes.Min(n => n.X), p.Nodes.Min(n => n.Y), p.Nodes.Max(n => n.X), p.Nodes.Max(n => n.Y));
        double stepX = (bb.Item3 - bb.Item1) + 10;
        (double X, double Y) ToDwg(string story, double x, double y)
        {
            var l = LinkFor(p, story);
            if (l != null) return l.ToDrawing(x, y);
            int i = Math.Max(0, stories.IndexOf(story));
            return (x + i * stepX, y);
        }
        double RotOf(string story, double deg) => deg + (LinkFor(p, story)?.Rotation ?? 0);
        void Text(string layer, string story, double x, double y, string t, double rot = 0, double hk = 1)
        {
            var (X, Y) = ToDwg(story, x, y);
            double r = RotOf(story, rot);
            while (r > 90) r -= 180;
            while (r <= -90) r += 180;
            pk.Items.Add(new AcadResultItem { Type = "text", Layer = layer, Story = story, X = X, Y = Y, H = textH * hk, Rot = r, Text = t });
        }
        void Poly(string layer, string story, IEnumerable<(double X, double Y)> pts, bool closed = true) =>
            pk.Items.Add(new AcadResultItem { Type = "poly", Layer = layer, Story = story, Closed = closed, Pts = pts.Select(q => { var d = ToDwg(story, q.X, q.Y); return new[] { d.X, d.Y }; }).ToList() });
        FrameSection Sec(string n) => p.FrameSections.FirstOrDefault(x => x.Name == n);

        // ---- cột: mỗi tầng ghi tên + tiết diện + thép tại chân cột tầng đó ----
        foreach (var c in s.Columns)
            foreach (var st in c.Stories)
            {
                var f = p.Frames.FirstOrDefault(x => x.Id == st.Frame);
                if (f == null) continue;
                var n = DesignUtil.LowerNode(p, f);
                var sec = Sec(f.Section);
                double b = st.B > 0 ? st.B : sec?.B ?? 0.2, h = st.H > 0 ? st.H : sec?.H ?? 0.2;
                double a = f.Angle * Math.PI / 180, ca = Math.Cos(a), sa = Math.Sin(a);
                var pts = new[] { (-b / 2, -h / 2), (b / 2, -h / 2), (b / 2, h / 2), (-b / 2, h / 2) }
                    .Select(q => (n.X + f.ColOffsetX + q.Item1 * ca - q.Item2 * sa, n.Y + f.ColOffsetY + q.Item1 * sa + q.Item2 * ca));
                string story = f.Story;
                Poly(LCot, story, pts);
                string bars = st.Dia > 0 ? $"{st.TotalBars}Ø{st.Dia}" : "";
                Text(LCot, story, n.X + f.ColOffsetX + b / 2 + 0.1, n.Y + f.ColOffsetY + h / 2 + 0.1, $"{c.Name} {Mm(b)}x{Mm(h)}" + (bars.Length > 0 ? " " + bars : ""));
            }

        // ---- dầm: tên + tiết diện tại giữa dải, thép từng nhịp (trên gối / dưới nhịp) ----
        foreach (var d in s.Beams)
        {
            var frames = d.Frames.Select(id => p.Frames.FirstOrDefault(x => x.Id == id)).Where(x => x != null).ToList();
            if (frames.Count == 0) continue;
            string story = frames[0].Story;
            for (int k = 0; k < d.Spans.Count; k++)
            {
                var sp = d.Spans[k];
                var f = p.Frames.FirstOrDefault(x => x.Id == sp.Frame) ?? frames[Math.Min(k, frames.Count - 1)];
                var a = p.Node(f.NodeI); var b2 = p.Node(f.NodeJ);
                if (a == null || b2 == null) continue;
                double mx = (a.X + b2.X) / 2, my = (a.Y + b2.Y) / 2;
                double ang = Math.Atan2(b2.Y - a.Y, b2.X - a.X) * 180 / Math.PI;
                double nx = -Math.Sin(ang * Math.PI / 180), ny = Math.Cos(ang * Math.PI / 180);
                double off = d.B / 2 + 0.15;
                string head = k == 0 || d.Spans.Count == 1 ? $"{d.Name} ({Mm(d.B)}x{Mm(d.H)})" + (d.Quantity > 1 ? $" SL={d.Quantity}" : "") + ": " : "";
                Text(LDam, story, mx + nx * off, my + ny * off, head + "dưới " + sp.BotText, ang, 0.9);
                string top = sp.TopLeftText == sp.TopRightText ? "trên " + sp.TopLeftText : $"trên {sp.TopLeftText} / {sp.TopRightText}";
                Text(LDam, story, mx - nx * (off + textH), my - ny * (off + textH), top, ang, 0.9);
            }
        }

        // ---- sàn: tên + thép tại tâm ô ----
        foreach (var sl in s.Slabs)
        {
            if (sl.Outline == null || sl.Outline.Count < 3) continue;
            string story = p.Stories.OrderBy(x => Math.Abs(x.Elevation - sl.Elevation)).FirstOrDefault()?.Name ?? "";
            double cx = sl.Outline.Average(q => q.x), cy = sl.Outline.Average(q => q.y);
            Text(LSan, story, cx, cy + textH, $"{sl.Name} h={Mm(sl.Thickness)}", 0, 0.9);
            Text(LSan, story, cx, cy - textH * 0.6, $"dưới {sl.BotX} / {sl.BotY}", 0, 0.75);
            if (!string.IsNullOrEmpty(sl.TopX)) Text(LSan, story, cx, cy - textH * 1.8, $"mũ {sl.TopX} / {sl.TopY}", 0, 0.75);
        }

        // ---- móng đơn / đài cọc: khung + tên + kích thước (tầng thấp nhất) ----
        string baseStory = stories.FirstOrDefault() ?? "";
        foreach (var f in FootingLayout.AllInstances(p, s))
        {
            var pts = new[] { (f.X - f.L / 2, f.Y - f.B / 2), (f.X + f.L / 2, f.Y - f.B / 2), (f.X + f.L / 2, f.Y + f.B / 2), (f.X - f.L / 2, f.Y + f.B / 2) };
            Poly(LMong, baseStory, pts);
            Text(LMong, baseStory, f.X - f.L / 2, f.Y - f.B / 2 - textH * 1.4, $"{f.Name} {F(f.L)}x{F(f.B)}x{F(f.H)} m");
        }
        foreach (var pc in s.PileCaps)
        {
            var locs = pc.Locations.Count > 0 ? pc.Locations : new List<(double X, double Y)> { (pc.X, pc.Y) };
            foreach (var (x, y) in locs)
            {
                double cx = x + pc.OffsetX, cy = y + pc.OffsetY;
                Poly(LMong, baseStory, new[] { (cx - pc.L / 2, cy - pc.B / 2), (cx + pc.L / 2, cy - pc.B / 2), (cx + pc.L / 2, cy + pc.B / 2), (cx - pc.L / 2, cy + pc.B / 2) });
                double r = Math.Max(pc.PileSize / 2, 0.05);
                foreach (var (px, py) in pc.Piles)
                    Poly(LMong, baseStory, new[] { (x + px - r, y + py - r), (x + px + r, y + py - r), (x + px + r, y + py + r), (x + px - r, y + py + r) });
                Text(LMong, baseStory, cx - pc.L / 2, cy - pc.B / 2 - textH * 1.4, $"{pc.Name} {pc.Piles.Count} cọc, {F(pc.L)}x{F(pc.B)}x{F(pc.H)} m");
            }
        }
        return pk;
    }

    /// <summary>Thư mục gói kết quả: %AppData%\KetCauVXT\Link\Back</summary>
    public static string BackDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KetCauVXT", "Link", "Back");

    /// <summary>Ghi gói kết quả (+ bản vẽ chi tiết rời DXF) vào thư mục; trả đường dẫn file JSON.</summary>
    public static string Write(AcadResultPackage pk, string folder = null)
    {
        folder ??= BackDir;
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "ketqua_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".json");
        File.WriteAllText(path, pk.ToJson(), new UTF8Encoding(false));
        return path;
    }
}

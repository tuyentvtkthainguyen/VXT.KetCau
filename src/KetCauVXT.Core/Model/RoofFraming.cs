using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace KetCauVXT.Model;

/// <summary>Loại hệ mái / bao che vẽ theo diện kéo chọn.</summary>
public enum RoofFramingKind
{
    /// <summary>Lợp mái tôn nhà thép tiền chế: xà gồ trên kèo khung, tôn lợp (có thể thêm vì kèo)</summary>
    MaiTonTienChe,
    /// <summary>Thưng bịt tường: xà gồ tường (đà tôn) + tôn thưng, cột sườn tường tuỳ chọn</summary>
    ThungTuong,
    /// <summary>Mái chống nóng nhà dân dụng trên sàn mái BTCT: trụ đỡ, vì kèo thép hộp, xà gồ, tôn</summary>
    ChongNong,
}

/// <summary>Profile tôn (mặt cắt sóng): bước sóng, cao sóng, khổ hữu dụng, dày, trọng lượng.</summary>
public class SheetProfile
{
    public string Name { get; set; } = "";
    /// <summary>"Sóng vuông" | "Sóng tròn" | "Cliplock" | "Panel cách nhiệt"</summary>
    public string Shape { get; set; } = "Sóng vuông";
    /// <summary>Bước sóng (m)</summary>
    public double RibPitch { get; set; } = 0.25;
    /// <summary>Chiều cao sóng (m)</summary>
    public double RibHeight { get; set; } = 0.032;
    /// <summary>Bề rộng đỉnh sóng (m) – sóng vuông</summary>
    public double RibTop { get; set; } = 0.03;
    /// <summary>Khổ hữu dụng (m)</summary>
    public double CoverWidth { get; set; } = 1.0;
    /// <summary>Chiều dày tôn (mm)</summary>
    public double Thickness { get; set; } = 0.45;
    /// <summary>Trọng lượng (kg/m² diện tích lợp)</summary>
    public double Weight { get; set; } = 4.2;
    /// <summary>Bước xà gồ khuyến nghị lớn nhất (m)</summary>
    public double MaxPurlinSpacing { get; set; } = 1.2;
    public override string ToString() => Name;
    public SheetProfile Clone() => (SheetProfile)MemberwiseClone();

    /// <summary>Đường mặt cắt một khổ tôn (toạ độ m, gốc trái-dưới) – vẽ xem trước.</summary>
    public List<(double X, double Y)> Section(double width = 0)
    {
        double w = width > 0 ? width : CoverWidth;
        var pts = new List<(double, double)>();
        double p = Math.Max(0.02, RibPitch), h = Math.Max(0.001, RibHeight);
        if (Shape == "Sóng tròn")
        {
            int n = (int)Math.Ceiling(w / p * 16);
            for (int i = 0; i <= n; i++) { double x = w * i / n; pts.Add((x, h / 2 * (1 - Math.Cos(2 * Math.PI * x / p)))); }
            return pts;
        }
        double top = Math.Clamp(RibTop, 0.005, p * 0.6), slope = Math.Min(h * 0.6, (p - top) * 0.25);
        for (double x0 = 0; x0 < w - 1e-9; x0 += p)
        {
            // sóng: đáy – sườn lên – đỉnh – sườn xuống
            double c = x0 + p / 2;
            pts.Add((x0, 0));
            pts.Add((c - top / 2 - slope, 0));
            pts.Add((c - top / 2, h));
            pts.Add((c + top / 2, h));
            pts.Add((c + top / 2 + slope, 0));
        }
        pts.Add((w, 0));
        if (Shape == "Panel cách nhiệt") { pts.Add((w, -0.05)); pts.Add((0, -0.05)); pts.Add((0, 0)); }
        return pts;
    }

    public static List<SheetProfile> Defaults() => new()
    {
        new() { Name = "Tôn 5 sóng vuông dày 0.45", Shape = "Sóng vuông", RibPitch = 0.25, RibHeight = 0.032, RibTop = 0.03, CoverWidth = 1.0, Thickness = 0.45, Weight = 4.2, MaxPurlinSpacing = 1.2 },
        new() { Name = "Tôn 9 sóng vuông dày 0.45", Shape = "Sóng vuông", RibPitch = 0.125, RibHeight = 0.021, RibTop = 0.02, CoverWidth = 1.0, Thickness = 0.45, Weight = 4.2, MaxPurlinSpacing = 1.0 },
        new() { Name = "Tôn 11 sóng vuông dày 0.40", Shape = "Sóng vuông", RibPitch = 0.10, RibHeight = 0.018, RibTop = 0.018, CoverWidth = 1.07, Thickness = 0.40, Weight = 3.7, MaxPurlinSpacing = 0.9 },
        new() { Name = "Tôn sóng tròn 13 sóng dày 0.35", Shape = "Sóng tròn", RibPitch = 0.076, RibHeight = 0.017, CoverWidth = 0.98, Thickness = 0.35, Weight = 3.2, MaxPurlinSpacing = 0.8 },
        new() { Name = "Tôn Cliplock dày 0.47", Shape = "Cliplock", RibPitch = 0.235, RibHeight = 0.041, RibTop = 0.025, CoverWidth = 0.705, Thickness = 0.47, Weight = 4.8, MaxPurlinSpacing = 1.5 },
        new() { Name = "Tôn PU cách nhiệt 3 lớp 50 mm", Shape = "Panel cách nhiệt", RibPitch = 0.25, RibHeight = 0.035, RibTop = 0.03, CoverWidth = 1.0, Thickness = 0.40, Weight = 10, MaxPurlinSpacing = 1.5 },
    };
}

/// <summary>Hệ mái tôn / thưng tường vẽ theo diện kéo chọn trên mặt bằng (khung chữ nhật X0..X1 × Y0..Y1).
/// Thưng tường: đường tường là trục giữa của khung theo cạnh dài.</summary>
public class RoofFraming
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public RoofFramingKind Kind { get; set; } = RoofFramingKind.ChongNong;
    /// <summary>Tầng đặt (cao độ gốc = cao độ tầng)</summary>
    public string Story { get; set; } = "";
    public double X0 { get; set; }
    public double Y0 { get; set; }
    public double X1 { get; set; }
    public double Y1 { get; set; }
    /// <summary>Mái: đỉnh (nóc) chạy song song trục X (dốc theo Y). Thưng tường: tường chạy theo X.</summary>
    public bool RidgeAlongX { get; set; } = true;
    /// <summary>Mái hai dốc (false = một dốc)</summary>
    public bool TwoSlopes { get; set; } = true;
    /// <summary>Mái một dốc: đầu thấp ở cạnh nhỏ (Y0 hoặc X0)</summary>
    public bool LowAtStart { get; set; } = true;
    /// <summary>Độ dốc mái (%)</summary>
    public double SlopePercent { get; set; } = 15;
    /// <summary>Cao độ chân mái / chân tường thưng so với cao độ tầng (m)</summary>
    public double BaseOffset { get; set; } = 0.6;
    /// <summary>Thưng tường: chiều cao đỉnh thưng so với cao độ tầng (m)</summary>
    public double TopOffset { get; set; } = 6;
    /// <summary>Đua mái (m)</summary>
    public double Overhang { get; set; } = 0.3;
    public string Profile { get; set; } = "Tôn 5 sóng vuông dày 0.45";
    public string PurlinSection { get; set; } = "C100x50x15x1.8";
    /// <summary>Bước xà gồ (m) – đo theo mặt dốc (mái) hoặc theo phương đứng (thưng tường)</summary>
    public double PurlinSpacing { get; set; } = 0.9;
    /// <summary>Có vì kèo (mái) / cột sườn tường (thưng)</summary>
    public bool IncludeTrusses { get; set; } = true;
    /// <summary>Bước vì kèo / bước khung / bước cột sườn – cũng là nhịp xà gồ (m)</summary>
    public double TrussSpacing { get; set; } = 3.0;
    public string ChordSection { get; set; } = "□40x80x1.4";
    public string WebSection { get; set; } = "□30x30x1.2";
    /// <summary>Trụ đỡ chân vì kèo (chống nóng): tiết diện, rỗng = không vẽ trụ</summary>
    public string PostSection { get; set; } = "□100x100x2.0";
    /// <summary>Hoạt tải mái (kN/m²) – kiểm tra xà gồ</summary>
    public double LiveLoad { get; set; } = 0.3;

    [JsonIgnore] public double MinX => Math.Min(X0, X1);
    [JsonIgnore] public double MaxX => Math.Max(X0, X1);
    [JsonIgnore] public double MinY => Math.Min(Y0, Y1);
    [JsonIgnore] public double MaxY => Math.Max(Y0, Y1);

    public static string KindText(RoofFramingKind k) => k switch
    {
        RoofFramingKind.MaiTonTienChe => "Mái tôn nhà thép tiền chế",
        RoofFramingKind.ThungTuong => "Thưng bịt tường tôn",
        _ => "Mái chống nóng nhà dân dụng",
    };

    public RoofFraming Clone() => (RoofFraming)MemberwiseClone();
}

/// <summary>Một thanh của hệ mái (toạ độ 3D, m).</summary>
public sealed record RfMember(string Role, string Section, Vec3 A, Vec3 B)
{
    public double Length => (B - A).Length;
}

/// <summary>Một tấm tôn phẳng (4 góc, m) – U: phương dọc nóc / dọc tường (ngang sóng), V: phương xuôi dốc (dọc sóng).</summary>
public sealed record RfPanel(Vec3 P0, Vec3 P1, Vec3 P2, Vec3 P3)
{
    public double Area => ((P1 - P0).Cross(P3 - P0)).Length;
}

/// <summary>Đặc trưng tiết diện thép thành mỏng (xà gồ C/Z, hộp, ống) đọc từ tên.</summary>
public sealed record ThinSection(string Name, double H, double B, double T, double A, double Ix, double Wx, double KgPerM)
{
    private static readonly Regex Num = new(@"\d+(?:[.,]\d+)?", RegexOptions.CultureInvariant);

    public static ThinSection Parse(string name)
    {
        string s = (name ?? "").Trim();
        var v = Num.Matches(s).Select(m => double.Parse(m.Value.Replace(',', '.'), CultureInfo.InvariantCulture)).ToList();
        string u = s.ToUpperInvariant();
        if ((u.StartsWith("C") || u.StartsWith("Z")) && v.Count >= 3)
        {
            double h = v[0], b = v[1], c = v.Count >= 4 ? v[2] : 0, t = v.Count >= 4 ? v[3] : v[2];
            double hc = h - t, bc = b - t, cc = Math.Max(0, c - t / 2);
            double A = t * (hc + 2 * bc + 2 * cc);
            double Ix = t * hc * hc * hc / 12 + 2 * bc * t * (hc / 2) * (hc / 2)
                        + (cc > 0 ? 2 * (t * cc * cc * cc / 12 + cc * t * Math.Pow(hc / 2 - cc / 2, 2)) : 0);
            return Make(s, h, b, t, A, Ix);
        }
        if ((u.StartsWith("□") || u.StartsWith("HỘP") || u.StartsWith("HOP") || u.StartsWith("BOX") || u.StartsWith("[]")) && v.Count >= 3)
        {
            double b = v[0], h = v[1], t = v[2];
            double A = 2 * t * (b + h) - 4 * t * t;
            double Ix = (b * h * h * h - (b - 2 * t) * Math.Pow(h - 2 * t, 3)) / 12;
            return Make(s, h, b, t, A, Ix);
        }
        if ((u.StartsWith("Ø") || u.StartsWith("O") || u.StartsWith("PHI")) && v.Count >= 2)
        {
            double d = v[0], t = v[1];
            double A = Math.PI / 4 * (d * d - Math.Pow(d - 2 * t, 2));
            double Ix = Math.PI / 64 * (Math.Pow(d, 4) - Math.Pow(d - 2 * t, 4));
            return Make(s, d, d, t, A, Ix);
        }
        if (u.Contains("GẠCH") || u.Contains("GACH") || u.Contains("XÂY"))
        {
            double b = v.Count >= 1 ? v[0] : 220, h = v.Count >= 2 ? v[1] : b;
            return new ThinSection(s, h / 1000, b / 1000, 0, 0, 0, 0, 1800 * b * h / 1e6);
        }
        return new ThinSection(s, 0.1, 0.05, 0.002, 0, 0, 0, 0);
    }

    // mm → đơn vị m, m², m⁴, kg/m
    private static ThinSection Make(string s, double h, double b, double t, double Amm, double Imm) =>
        new(s, h / 1000, b / 1000, t / 1000, Amm * 1e-6, Imm * 1e-12, Imm * 1e-12 / (h / 2000), Amm * 1e-6 * 7850);
}

/// <summary>Kết quả dựng hình học + khối lượng + kiểm tra xà gồ.</summary>
public sealed class RoofFramingGeom
{
    public List<RfMember> Members { get; } = new();
    public List<RfPanel> Panels { get; } = new();
    /// <summary>Đường nóc (úp nóc) – mái hai dốc</summary>
    public (Vec3 A, Vec3 B)? Ridge { get; set; }
    public SheetProfile Profile { get; set; }
    public double SlopeDeg { get; set; }
    public int PurlinCount { get; set; }
    public int TrussCount { get; set; }
    public int PostCount { get; set; }
    public double SheetArea => Panels.Sum(p => p.Area);
    public double PlanArea { get; set; }
    public List<string> Checks { get; } = new();
    public List<string> Notes { get; } = new();

    public IEnumerable<(string Role, string Section, int Count, double Length, double Kg)> Takeoff() =>
        Members.GroupBy(m => (m.Role, m.Section)).Select(g =>
        {
            double L = g.Sum(m => m.Length);
            return (g.Key.Role, g.Key.Section, g.Count(), L, L * ThinSection.Parse(g.Key.Section).KgPerM);
        });

    public double SteelKg => Takeoff().Sum(t => t.Kg);
}

/// <summary>Dựng hệ mái: xà gồ theo bước, vì kèo (cánh thượng, cánh hạ, thanh đứng, thanh xiên), trụ đỡ, tôn lợp / tôn thưng;
/// bóc khối lượng và kiểm tra nhanh xà gồ (dầm đơn giản nhịp = bước vì kèo, tĩnh tải tôn + xà gồ + hoạt tải mái).</summary>
public static class RoofFramingBuilder
{
    public static double StoryElevation(Project p, string story) =>
        p?.Stories.FirstOrDefault(s => s.Name == story)?.Elevation ?? p?.Stories.OrderBy(s => s.Elevation).FirstOrDefault()?.Elevation ?? 0;

    public static SheetProfile ProfileOf(Project p, RoofFraming r) =>
        p?.SheetProfiles?.FirstOrDefault(q => q.Name == r.Profile) ?? SheetProfile.Defaults().FirstOrDefault(q => q.Name == r.Profile) ?? SheetProfile.Defaults()[0];

    public static RoofFramingGeom Build(Project p, RoofFraming r)
    {
        var g = new RoofFramingGeom { Profile = ProfileOf(p, r) };
        double z0 = StoryElevation(p, r.Story);
        if (r.Kind == RoofFramingKind.ThungTuong) { BuildWall(g, r, z0); Check(g, r); return g; }

        bool ax = r.RidgeAlongX;
        // a: dọc nóc, c: ngang dốc
        double A0 = ax ? r.MinX : r.MinY, A1 = ax ? r.MaxX : r.MaxY, C0 = ax ? r.MinY : r.MinX, C1 = ax ? r.MaxY : r.MaxX;
        double La = A1 - A0, W = C1 - C0;
        g.PlanArea = La * W;
        if (La < 0.3 || W < 0.3) { g.Notes.Add("Diện chọn quá nhỏ."); return g; }
        double tan = Math.Max(0, r.SlopePercent) / 100, cos = 1 / Math.Sqrt(1 + tan * tan);
        g.SlopeDeg = Math.Atan(tan) * 180 / Math.PI;
        double zb = z0 + r.BaseOffset;
        double cm = (C0 + C1) / 2;
        double Z(double c) => r.TwoSlopes ? zb + tan * (W / 2 - Math.Abs(c - cm)) : zb + tan * (r.LowAtStart ? c - C0 : C1 - c);
        Vec3 P(double a, double c, double z) => ax ? new Vec3(a, c, z) : new Vec3(c, a, z);
        double O = Math.Max(0, r.Overhang);

        // ---- tôn lợp (mỗi mặt dốc một tấm, kể đua mái) ----
        double ZO(double c) // cao độ mặt tôn kéo dài ra ngoài đua
        {
            if (r.TwoSlopes) return zb + tan * (W / 2 - Math.Abs(c - cm));
            return zb + tan * (r.LowAtStart ? c - C0 : C1 - c);
        }
        if (r.TwoSlopes)
        {
            g.Panels.Add(new RfPanel(P(A0 - O, C0 - O, ZO(C0 - O)), P(A1 + O, C0 - O, ZO(C0 - O)), P(A1 + O, cm, ZO(cm)), P(A0 - O, cm, ZO(cm))));
            g.Panels.Add(new RfPanel(P(A0 - O, C1 + O, ZO(C1 + O)), P(A1 + O, C1 + O, ZO(C1 + O)), P(A1 + O, cm, ZO(cm)), P(A0 - O, cm, ZO(cm))));
            g.Ridge = (P(A0 - O, cm, ZO(cm)), P(A1 + O, cm, ZO(cm)));
        }
        else
        {
            double lo = r.LowAtStart ? C0 - O : C1 + O, hi = r.LowAtStart ? C1 + O : C0 - O;
            g.Panels.Add(new RfPanel(P(A0 - O, lo, ZO(lo)), P(A1 + O, lo, ZO(lo)), P(A1 + O, hi, ZO(hi)), P(A0 - O, hi, ZO(hi))));
        }

        // ---- vị trí xà gồ (theo phương c) – bước đo theo mặt dốc ----
        double sp = Math.Max(0.3, r.PurlinSpacing) * cos;
        var cs = new List<double>();
        void Slope(double from, double to)   // từ diềm (from) lên nóc / đầu cao (to)
        {
            double dir = Math.Sign(to - from), len = Math.Abs(to - from);
            double first = 0.1, last = len - (r.TwoSlopes ? 0.15 : 0.1);
            if (last <= first) { cs.Add(from + dir * len / 2); return; }
            int n = Math.Max(1, (int)Math.Ceiling((last - first) / sp - 1e-6));
            for (int i = 0; i <= n; i++) cs.Add(from + dir * (first + (last - first) * i / n));
        }
        if (r.TwoSlopes) { Slope(C0, cm); Slope(C1, cm); }
        else if (r.LowAtStart) Slope(C0, C1); else Slope(C1, C0);
        cs = cs.Distinct().OrderBy(c => c).ToList();
        var ps = ThinSection.Parse(r.PurlinSection);
        foreach (double c in cs)
            g.Members.Add(new RfMember("Xà gồ mái", r.PurlinSection, P(A0 - Math.Min(O, 0.2), c, Z(c) - ps.H / 2), P(A1 + Math.Min(O, 0.2), c, Z(c) - ps.H / 2)));
        g.PurlinCount = cs.Count;

        // ---- vì kèo ----
        if (r.IncludeTrusses)
        {
            int nb = Math.Max(1, (int)Math.Ceiling(La / Math.Max(0.5, r.TrussSpacing) - 1e-6));
            var cz = ThinSection.Parse(r.ChordSection);
            for (int i = 0; i <= nb; i++)
            {
                double a = A0 + La * i / nb;
                double top = ps.H;   // cánh thượng nằm dưới xà gồ
                double Zt(double c) => Z(c) - top - cz.H / 2;
                double zbot = zb - cz.H / 2;
                // cánh thượng
                if (r.TwoSlopes)
                {
                    g.Members.Add(new RfMember("Cánh thượng vì kèo", r.ChordSection, P(a, C0, Math.Max(zbot, Zt(C0))), P(a, cm, Zt(cm))));
                    g.Members.Add(new RfMember("Cánh thượng vì kèo", r.ChordSection, P(a, cm, Zt(cm)), P(a, C1, Math.Max(zbot, Zt(C1)))));
                }
                else g.Members.Add(new RfMember("Cánh thượng vì kèo", r.ChordSection, P(a, C0, Math.Max(zbot, Zt(C0))), P(a, C1, Math.Max(zbot, Zt(C1)))));
                // cánh hạ
                g.Members.Add(new RfMember("Cánh hạ vì kèo", r.ChordSection, P(a, C0, zbot), P(a, C1, zbot)));
                // thanh bụng: đứng tại mỗi mắt (vị trí xà gồ) + xiên zigzag
                var nodes = cs.Where(c => c > C0 + 0.05 && c < C1 - 0.05).ToList();
                if (r.TwoSlopes && !nodes.Any(c => Math.Abs(c - cm) < 0.05)) nodes.Add(cm);
                if (!r.TwoSlopes) nodes.Add(r.LowAtStart ? C1 : C0);
                nodes = nodes.Distinct().OrderBy(c => c).ToList();
                var all = new List<double> { C0 }; all.AddRange(nodes.Where(c => c > C0 + 0.05 && c < C1 - 0.05)); all.Add(C1);
                all = all.Distinct().OrderBy(c => c).ToList();
                for (int k = 0; k < all.Count; k++)
                {
                    double c = all[k];
                    double h = Zt(c) - zbot;
                    if (h > 0.08) g.Members.Add(new RfMember("Thanh bụng vì kèo", r.WebSection, P(a, c, zbot), P(a, c, Zt(c))));
                    if (k + 1 < all.Count)
                    {
                        double c2 = all[k + 1];
                        // xiên: từ chân thanh đứng phía diềm lên đỉnh thanh đứng phía nóc
                        bool up = r.TwoSlopes ? c2 <= cm + 1e-9 : (r.LowAtStart);
                        var a1 = up ? P(a, c, zbot) : P(a, c, Zt(c));
                        var a2 = up ? P(a, c2, Zt(c2)) : P(a, c2, zbot);
                        if (Math.Abs(Zt(c) - zbot) + Math.Abs(Zt(c2) - zbot) > 0.15) g.Members.Add(new RfMember("Thanh bụng vì kèo", r.WebSection, a1, a2));
                    }
                }
                // trụ đỡ
                if (!string.IsNullOrWhiteSpace(r.PostSection) && r.BaseOffset > 0.05)
                    foreach (double c in new[] { C0, C1 })
                    {
                        g.Members.Add(new RfMember("Trụ đỡ vì kèo", r.PostSection, P(a, c, z0), P(a, c, zbot)));
                        g.PostCount++;
                    }
            }
            g.TrussCount = nb + 1;
            // giằng xà gồ / thanh chống: ghi chú
            g.Notes.Add($"{nb + 1} vì kèo cách {La / nb:0.###} m; mắt vì kèo tại vị trí xà gồ.");
        }
        else if (r.Kind == RoofFramingKind.MaiTonTienChe)
            g.Notes.Add("Xà gồ gác lên kèo khung thép tiền chế (bước khung = nhịp xà gồ).");
        Check(g, r);
        return g;
    }

    private static void BuildWall(RoofFramingGeom g, RoofFraming r, double z0)
    {
        bool ax = r.RidgeAlongX;
        double A0 = ax ? r.MinX : r.MinY, A1 = ax ? r.MaxX : r.MaxY;
        double c = ax ? (r.MinY + r.MaxY) / 2 : (r.MinX + r.MaxX) / 2;
        Vec3 P(double a, double z) => ax ? new Vec3(a, c, z) : new Vec3(c, a, z);
        double L = A1 - A0, zb = z0 + r.BaseOffset, zt = z0 + Math.Max(r.TopOffset, r.BaseOffset + 0.5);
        g.PlanArea = L * (zt - zb);
        if (L < 0.3) { g.Notes.Add("Đoạn tường chọn quá ngắn."); return; }
        g.Panels.Add(new RfPanel(P(A0, zb), P(A1, zb), P(A1, zt), P(A0, zt)));
        double sp = Math.Max(0.3, r.PurlinSpacing);
        int n = Math.Max(1, (int)Math.Ceiling((zt - zb - 0.2) / sp - 1e-6));
        for (int i = 0; i <= n; i++)
        {
            double z = zb + 0.1 + (zt - zb - 0.2) * i / n;
            g.Members.Add(new RfMember("Xà gồ tường", r.PurlinSection, P(A0, z), P(A1, z)));
        }
        g.PurlinCount = n + 1;
        if (r.IncludeTrusses)
        {
            int nb = Math.Max(1, (int)Math.Ceiling(L / Math.Max(0.5, r.TrussSpacing) - 1e-6));
            for (int i = 0; i <= nb; i++) g.Members.Add(new RfMember("Cột sườn tường", r.ChordSection, P(A0 + L * i / nb, z0), P(A0 + L * i / nb, zt)));
            g.TrussCount = nb + 1;
        }
    }

    /// <summary>Kiểm tra xà gồ: dầm đơn giản nhịp = bước vì kèo / khung / cột sườn; tải vuông góc mặt dốc (mái) hoặc gió (tường).</summary>
    private static void Check(RoofFramingGeom g, RoofFraming r)
    {
        var s = ThinSection.Parse(r.PurlinSection);
        var pf = g.Profile;
        double L = Math.Max(0.5, r.TrussSpacing);
        if (s.Wx <= 0 || s.Ix <= 0) { g.Checks.Add($"Không đọc được tiết diện xà gồ \"{r.PurlinSection}\" (dạng C150x50x20x2.0, Z200x62x20x2.0, □40x80x1.4)."); return; }
        double sp = Math.Max(0.3, r.PurlinSpacing);
        double qs, qd; string what;
        if (r.Kind == RoofFramingKind.ThungTuong)
        {
            double w0 = 0.83;   // kN/m² – áp lực gió tham khảo (vùng II-B) để kiểm tra nhanh
            qs = w0 * sp; qd = qs * 1.2;
            what = $"gió {w0} kN/m² × bước {sp:0.##} m";
        }
        else
        {
            double cos = Math.Cos(g.SlopeDeg * Math.PI / 180);
            double gk = pf.Weight * 0.00981 + s.KgPerM * 0.00981 / sp;   // kN/m² mặt dốc
            qs = (gk + r.LiveLoad * cos) * sp * cos;
            qd = (gk * 1.1 + r.LiveLoad * 1.3 * cos) * sp * cos;
            what = $"tôn {pf.Weight:0.#} kg/m² + xà gồ {s.KgPerM:0.##} kg/m + hoạt tải {r.LiveLoad:0.##} kN/m², bước {sp:0.##} m";
        }
        double M = qd * L * L / 8;               // kN·m
        double sigma = M / s.Wx / 1000;          // MPa
        double f = 210;
        double E = 2.06e8;                       // kN/m²
        double d = 5 * qs * Math.Pow(L, 4) / (384 * E * s.Ix);
        double dl = L / 200;
        g.Checks.Add($"Xà gồ {r.PurlinSection} nhịp {L:0.##} m ({what}): M = {M:0.###} kN·m, σ = {sigma:0} MPa / f = {f} MPa ({sigma / f:0.00}) {(sigma <= f ? "ĐẠT" : "KHÔNG ĐẠT")}; " +
                     $"võng {d * 1000:0.#} mm / [L/200 = {dl * 1000:0.#} mm] {(d <= dl ? "ĐẠT" : "KHÔNG ĐẠT")}.");
        if (r.Kind != RoofFramingKind.ThungTuong && r.PurlinSpacing > pf.MaxPurlinSpacing + 1e-9)
            g.Checks.Add($"Bước xà gồ {r.PurlinSpacing:0.##} m lớn hơn bước khuyến nghị {pf.MaxPurlinSpacing:0.##} m của {pf.Name}.");
        if (r.Kind != RoofFramingKind.ThungTuong && r.SlopePercent < 10)
            g.Checks.Add($"Độ dốc {r.SlopePercent:0.#}% nhỏ – mái tôn nên ≥ 10% (tôn cliplock có thể 5%).");
    }

    /// <summary>Ước lượng hệ mái mặc định theo loại.</summary>
    public static RoofFraming Defaults(RoofFramingKind k, Project p, string story)
    {
        var st = p?.Stories.FirstOrDefault(s => s.Name == story);
        double h = st?.Height > 0 ? st.Height : 3.6;
        return k switch
        {
            RoofFramingKind.MaiTonTienChe => new RoofFraming { Kind = k, Story = story, BaseOffset = h, SlopePercent = 10, Overhang = 0.5, PurlinSection = "Z200x62x20x2.0", PurlinSpacing = 1.2, IncludeTrusses = false, TrussSpacing = 6, Profile = "Tôn 5 sóng vuông dày 0.45", PostSection = "" },
            RoofFramingKind.ThungTuong => new RoofFraming { Kind = k, Story = story, BaseOffset = 0.2, TopOffset = h, PurlinSection = "C150x50x20x1.8", PurlinSpacing = 1.4, IncludeTrusses = false, TrussSpacing = 6, ChordSection = "□100x100x2.0", Profile = "Tôn 9 sóng vuông dày 0.45", PostSection = "" },
            _ => new RoofFraming { Kind = k, Story = story, BaseOffset = 0.6, SlopePercent = 20, Overhang = 0.4, PurlinSection = "□40x80x1.4", PurlinSpacing = 0.9, IncludeTrusses = true, TrussSpacing = 3.0, ChordSection = "□40x80x1.4", WebSection = "□30x30x1.2", PostSection = "□100x100x2.0", Profile = "Tôn sóng tròn 13 sóng dày 0.35" },
        };
    }
}

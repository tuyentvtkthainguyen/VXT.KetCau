using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

// =====================================================================
// TƯỜNG + CỬA GẮN TƯỜNG (kiểu Revit) TRÊN BẢN VẼ MẶT BẰNG DXF và BẢNG THỐNG KÊ CỬA
//  • Tường đứng trên tầng (WallLine.Story = tầng của dầm / sàn đỡ) vẽ nét mảnh 2 mép theo tổng chiều dày kiểu tường,
//    CẮT tại ô cửa cắt qua mặt phẳng mặt bằng (cao 1,2 m – WallAssembly.PlanCutHeight), nối góc L/T gọn (WallAssembly.Joins).
//  • Cửa đi: cánh (đường thẳng vuông góc tường) + cung mở 90°; 2 cánh: hai cánh + hai cung; cửa trượt: hai nét lệch.
//    Cửa sổ: 3 nét song song trong ô tường; ô thoáng / cửa không cắt mặt phẳng mặt bằng: nét đứt. Thẻ D1 / S1 cạnh cửa.
//  • Bảng thống kê cửa: kiểu, loại, kích thước, số lượng theo tầng và tổng.
// Toạ độ mặt bằng mm (= toạ độ mô hình × 1000), giống FramingPlan.
// =====================================================================
public static class WallPlanDrawing
{
    public const string ScheduleTitle = "BẢNG THỐNG KÊ CỬA";

    /// <summary>Có vẽ tường trên mặt bằng kết cấu không: chỉ khi mô hình có cửa gắn tường (mô hình kiểu Revit) – giữ nguyên bản vẽ cũ.</summary>
    public static bool Enabled(Project p) => p?.Walls != null && p.Walls.Any(w => w.Openings is { Count: > 0 });

    private static double Thickness(Project p, WallLine w)
    {
        var wt = p.WallTypes?.FirstOrDefault(t => t.Name == w.Type);
        double t = wt?.TotalThickness ?? 0;
        return t > 1e-6 ? t : 0.22;
    }

    /// <summary>Vẽ tường + cửa của tầng storyName lên w. Trả về số cửa đã vẽ.</summary>
    public static int Draw(DxfWriter w, Project p, string storyName)
    {
        if (w == null || p?.Walls == null) return 0;
        var walls = p.Walls.Where(q => q.Story == storyName && !q.IsCurved && q.Chord > 1e-6).ToList();
        if (walls.Count == 0) return 0;
        double th = w.Th;
        var joins = WallAssembly.Joins(walls, q => Thickness(p, q));
        int doors = 0;
        foreach (var wl in walls)
        {
            double t = Thickness(p, wl);
            var (e0, e1) = joins.TryGetValue(wl.Id, out var jj) ? jj : (0.0, 0.0);
            foreach (var poly in WallAssembly.PlanPolygons(p, wl, t, e0, e1))
            {
                var pts = poly.Select(q => (q.X * 1000, q.Y * 1000)).ToList();
                // hai mép dọc tường (nét liền) + hai đầu đoạn (nét liền)
                w.Polyline(Lay.KyHieu, pts, true);
            }
            double L = wl.Chord, ux = (wl.X2 - wl.X1) / L, uy = (wl.Y2 - wl.Y1) / L, nx = -uy, ny = ux;
            foreach (var o in wl.Openings ?? new List<WallOpening>())
            {
                var ot = WallAssembly.TypeOf(p, o);
                if (ot == null) continue;
                var (a, b) = WallAssembly.Span(p, o);
                a = Math.Max(0, a); b = Math.Min(L, b);
                if (b - a < 1e-3) continue;
                double hw = t / 2 * 1000;
                (double X, double Y) P(double s, double off) => ((wl.X1 + ux * s) * 1000 + nx * off, (wl.Y1 + uy * s) * 1000 + ny * off);
                bool cut = WallAssembly.CutsPlan(ot);
                if (!cut)
                {
                    // không cắt mặt phẳng mặt bằng (ô thoáng, cửa sổ cao): khung nét đứt
                    var q1 = P(a, hw); var q2 = P(b, hw); var q3 = P(b, -hw); var q4 = P(a, -hw);
                    w.Polyline(Lay.KyHieu, new List<(double, double)> { q1, q2, q3, q4 }, true, 0, "DASHED");
                }
                else if (ot.Kind == WallOpeningKind.Window)
                {
                    // cửa sổ: 2 mép tường tại ô + 1 nét giữa (kính)
                    foreach (double off in new[] { hw, 0.0, -hw })
                    {
                        var s1 = P(a, off); var s2 = P(b, off);
                        w.Line(Lay.KyHieu, s1.X, s1.Y, s2.X, s2.Y);
                    }
                }
                else if (ot.Kind == WallOpeningKind.Door)
                {
                    int side = o.Flip ? -1 : 1;                 // phía mở cánh
                    double face = side * hw;                    // mép tường phía mở
                    string op = ot.Operation ?? "";
                    if (op.StartsWith("Trượt", StringComparison.OrdinalIgnoreCase))
                    {
                        double mid = (a + b) / 2, lap = 0.05;
                        var c1 = P(a, 0.15 * hw); var c2 = P(mid + lap, 0.15 * hw);
                        var c3 = P(mid - lap, -0.15 * hw); var c4 = P(b, -0.15 * hw);
                        w.Line(Lay.KyHieu, c1.X, c1.Y, c2.X, c2.Y);
                        w.Line(Lay.KyHieu, c3.X, c3.Y, c4.X, c4.Y);
                    }
                    else
                    {
                        bool two = op.StartsWith("2", StringComparison.Ordinal);
                        var leaves = two ? new[] { (Hinge: a, Len: (b - a) / 2, Dir: 1), (Hinge: b, Len: (b - a) / 2, Dir: -1) }
                                         : new[] { o.FlipHand ? (Hinge: b, Len: b - a, Dir: -1) : (Hinge: a, Len: b - a, Dir: 1) };
                        foreach (var (hinge, len, dir) in leaves)
                        {
                            var hp = P(hinge, face);
                            // cánh mở 90°: vuông góc tường về phía mở
                            var tip = (X: hp.X + nx * side * len * 1000, Y: hp.Y + ny * side * len * 1000);
                            w.Line(Lay.KyHieu, hp.X, hp.Y, tip.X, tip.Y);
                            // cung từ đầu cánh về ô cửa
                            double angLeaf = Math.Atan2(ny * side, nx * side) * 180 / Math.PI;
                            double angWall = Math.Atan2(uy * dir, ux * dir) * 180 / Math.PI;
                            double a1 = angLeaf, a2 = angWall;
                            // cung ngược chiều kim đồng hồ từ a1 → a2: chọn cặp có độ mở 90°
                            double sweep = ((a2 - a1) % 360 + 360) % 360;
                            if (Math.Abs(sweep - 90) > 1) (a1, a2) = (a2, a1);
                            w.Arc(Lay.KyHieu, hp.X, hp.Y, len * 1000, Norm(a1), Norm(a2));
                        }
                    }
                    doors++;
                }
                else
                {
                    var q1 = P(a, hw); var q2 = P(b, -hw);
                    w.Line(Lay.KyHieu, q1.X, q1.Y, q2.X, q2.Y, "DASHED");
                }
                // thẻ cửa (D1, S1…) phía ngoài tường đối diện phía mở
                string tag = ot.Tag;
                if (!string.IsNullOrEmpty(tag))
                {
                    int ts = o.Flip ? 1 : -1;
                    var tp = P((a + b) / 2, ts * (hw + 1.2 * th));
                    double ang = Math.Atan2(uy, ux) * 180 / Math.PI;
                    if (ang > 90.001) ang -= 180; else if (ang <= -89.999) ang += 180;
                    w.Text(Lay.Chu, tp.X, tp.Y, 0.8 * th, tag, ang, 1, 2);
                }
            }
        }
        return doors;
    }

    private static double Norm(double a) => ((a % 360) + 360) % 360;

    /// <summary>Một dòng bảng thống kê cửa.</summary>
    public sealed class ScheduleRow
    {
        public string Type = "", Kind = "", Operation = "";
        public double Width, Height, Sill;
        /// <summary>Số lượng theo tầng (tên tầng → số cửa).</summary>
        public Dictionary<string, int> PerStory = new();
        public int Total => PerStory.Values.Sum();
    }

    /// <summary>Thống kê cửa gắn tường theo kiểu và tầng (thứ tự: cửa đi, cửa sổ, ô thoáng; trong loại theo tên kiểu).</summary>
    public static List<ScheduleRow> Schedule(Project p)
    {
        var rows = new Dictionary<string, ScheduleRow>();
        if (p?.Walls == null) return new List<ScheduleRow>();
        foreach (var wl in p.Walls)
            foreach (var o in wl.Openings ?? new List<WallOpening>())
            {
                var ot = WallAssembly.TypeOf(p, o);
                string key = ot?.Name ?? o.Type ?? "";
                if (!rows.TryGetValue(key, out var r))
                {
                    r = new ScheduleRow
                    {
                        Type = key, Kind = ot?.KindText ?? "", Operation = ot?.Operation ?? "",
                        Width = ot?.Width ?? 0, Height = ot?.Height ?? 0, Sill = ot?.Sill ?? 0
                    };
                    rows[key] = r;
                }
                string st = wl.Story ?? "";
                r.PerStory[st] = r.PerStory.TryGetValue(st, out int n) ? n + 1 : 1;
            }
        int Order(ScheduleRow r) => r.Kind == "Cửa đi" ? 0 : r.Kind == "Cửa sổ" ? 1 : 2;
        return rows.Values.OrderBy(Order).ThenBy(r => r.Type, StringComparer.Ordinal).ToList();
    }

    /// <summary>Hình BẢNG THỐNG KÊ CỬA (vẽ theo mm giấy, tỷ lệ 1/1) – null nếu mô hình không có cửa gắn tường.</summary>
    public static Figure ScheduleFigure(Project p)
    {
        var rows = Schedule(p);
        if (rows.Count == 0) return null;
        var stories = p.Stories.OrderBy(s => s.Elevation).Select(s => s.Name).Where(n => rows.Any(r => r.PerStory.ContainsKey(n))).ToList();
        foreach (var extra in rows.SelectMany(r => r.PerStory.Keys).Distinct().Where(k => !stories.Contains(k))) stories.Add(extra);
        var w = new DxfWriter(1);
        double th = 2.5, rh = 7;
        var heads = new List<string> { "STT", "KÝ HIỆU", "LOẠI", "RỘNG", "CAO", "BẬU", "CÁCH MỞ" };
        var cw = new List<double> { 12, 34, 22, 16, 16, 14, 22 };
        foreach (var s in stories) { heads.Add(s); cw.Add(18); }
        heads.Add("TỔNG"); cw.Add(16);
        double tw = cw.Sum();
        double y = 0;
        w.Text(Lay.Chu, tw / 2, y + 4, 1.6 * th, ScheduleTitle, 0, 1, 0);
        var table = new List<List<string>> { heads };
        int i = 1;
        foreach (var r in rows)
        {
            var row = new List<string>
            {
                (i++).ToString(), r.Type, r.Kind, DxfWriter.N0(r.Width * 1000), DxfWriter.N0(r.Height * 1000), DxfWriter.N0(r.Sill * 1000), r.Operation
            };
            foreach (var s in stories) row.Add(r.PerStory.TryGetValue(s, out int n) ? n.ToString() : "");
            row.Add(r.Total.ToString());
            table.Add(row);
        }
        var tot = new List<string> { "", "TỔNG CỘNG", "", "", "", "", "" };
        foreach (var s in stories) tot.Add(rows.Sum(r => r.PerStory.TryGetValue(s, out int n) ? n : 0).ToString());
        tot.Add(rows.Sum(r => r.Total).ToString());
        table.Add(tot);
        w.Line(Lay.KhungTen, 0, y, tw, y);
        foreach (var row in table)
        {
            double cx = 0;
            for (int c = 0; c < cw.Count; c++)
            {
                string tx = c < row.Count ? row[c] : "";
                w.Text(Lay.Chu, c == 1 ? cx + 1.5 : cx + cw[c] / 2, y - rh / 2, th, tx, 0, c == 1 ? 0 : 1, 2);
                cx += cw[c];
            }
            y -= rh;
            w.Line(Lay.KhungTen, 0, y, tw, y);
        }
        double xx = 0;
        for (int c = 0; c <= cw.Count; c++) { w.Line(Lay.KhungTen, xx, 0, xx, y); if (c < cw.Count) xx += cw[c]; }
        return new Figure(w, ScheduleTitle, true);
    }
}

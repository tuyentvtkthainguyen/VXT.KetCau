using KetCauVXT.Model;

namespace KetCauVXT.Import.Link;

/// <summary>[Ánh xạ Xref – tự động] Tìm vector dời (theo toạ độ mô hình) để bản vẽ liên kết trùng mặt bằng kết cấu theo TÊN TRỤC:
/// ký hiệu trục trên bản vẽ ("1", "2", "A", "B"… – bản vẽ do Việt Const xuất hoặc bản vẽ có ký hiệu trục giống mô hình) → tâm vòng tròn
/// ký hiệu (nếu có) → bỏ phiếu độ lệch theo từng phương → tinh chỉnh bằng nét trục thẳng đứng / nằm ngang gần nhất.
/// Chỉ dời (giữ góc xoay, tỷ lệ đang đặt của bản vẽ).</summary>
public static class CadLinkGridMatch
{
    public sealed class Result
    {
        public bool Ok;
        public double Dx, Dy;
        public int MatchX, MatchY;
        public List<string> Names = new();
        public string Message = "";
    }

    private static string Norm(string s)
    {
        s = (s ?? "").Trim().Replace("\\P", "").Replace(" ", "");
        if (s.StartsWith("TRỤC", StringComparison.OrdinalIgnoreCase)) s = s[4..];
        return s.ToUpperInvariant();
    }

    public static Result Match(Project p, CadLink l, CadLinkGeometry g)
    {
        var res = new Result();
        if (p == null || l == null || g == null) { res.Message = "Chưa đọc được bản vẽ."; return res; }
        var gx = p.Grids.Where(q => q.Dir == GridDir.X && !string.IsNullOrWhiteSpace(q.Name)).ToList();
        var gy = p.Grids.Where(q => q.Dir == GridDir.Y && !string.IsNullOrWhiteSpace(q.Name)).ToList();
        if (gx.Count + gy.Count == 0) { res.Message = "Mô hình chưa có trục có tên – không khớp tự động được."; return res; }
        var names = new HashSet<string>(gx.Concat(gy).Select(q => Norm(q.Name)));
        var circles = g.Prims.Where(q => q.IsCircle && q.R > 0).ToList();

        // ký hiệu trục trên bản vẽ → điểm neo (toạ độ mô hình theo phép đặt hiện tại)
        var anchors = new List<(string Name, double X, double Y)>();
        foreach (var t in g.Texts)
        {
            string n = Norm(t.Text);
            if (n.Length == 0 || n.Length > 6 || !names.Contains(n)) continue;
            double ax = t.X + t.Height * 0.35, ay = t.Y + t.Height * 0.5;
            CadLinkPrim best = null; double bd = double.MaxValue;
            foreach (var c in circles)
            {
                double d = Math.Sqrt((c.Cx - ax) * (c.Cx - ax) + (c.Cy - ay) * (c.Cy - ay));
                if (d <= c.R * 1.3 && c.R <= t.Height * 6 && d < bd) { bd = d; best = c; }
            }
            if (best != null) { ax = best.Cx; ay = best.Cy; }
            var m = l.ToModel(ax, ay);
            anchors.Add((n, m.X, m.Y));
        }
        if (anchors.Count == 0) { res.Message = "Không thấy ký hiệu trục nào trên bản vẽ trùng tên trục của mô hình (" + string.Join(", ", names.Take(8)) + ")."; return res; }

        // nét thẳng đứng / nằm ngang dài (toạ độ mô hình) để tinh chỉnh
        var vert = new List<double>(); var horz = new List<double>();
        foreach (var pr in g.Prims)
        {
            if (pr.IsCircle || pr.Xs.Length < 2) continue;
            for (int i = 0; i + 1 < pr.Xs.Length; i++)
            {
                var a = l.ToModel(pr.Xs[i], pr.Ys[i]); var b = l.ToModel(pr.Xs[i + 1], pr.Ys[i + 1]);
                double ddx = b.X - a.X, ddy = b.Y - a.Y, len = Math.Sqrt(ddx * ddx + ddy * ddy);
                if (len < 1.0) continue;
                if (Math.Abs(ddx) < len * 1e-3) vert.Add((a.X + b.X) / 2);
                else if (Math.Abs(ddy) < len * 1e-3) horz.Add((a.Y + b.Y) / 2);
            }
        }

        (double S, int N, List<string> Names) Vote(List<GridLine> grids, Func<(string Name, double X, double Y), double> coord, List<double> lines)
        {
            var cand = new List<(double S, string Name, double Coord)>();
            foreach (var gl in grids)
            {
                string n = Norm(gl.Name);
                foreach (var a in anchors.Where(q => q.Name == n)) cand.Add((gl.Coord - coord(a), n, gl.Coord));
            }
            if (cand.Count == 0) return (0, 0, new List<string>());
            (double S, int N, List<string> Names) best = (0, 0, new List<string>());
            foreach (var c in cand)
            {
                var grp = cand.Where(q => Math.Abs(q.S - c.S) <= 0.3).ToList();
                var nm = grp.Select(q => q.Name).Distinct().ToList();
                double s = grp.Average(q => q.S);
                if (nm.Count > best.N || (nm.Count == best.N && Math.Abs(s) < Math.Abs(best.S))) best = (s, nm.Count, nm);
            }
            // tinh chỉnh: nét trục gần nhất của từng trục đã khớp
            var refined = new List<double>();
            foreach (var gl in grids.Where(q => best.Names.Contains(Norm(q.Name))))
            {
                double want = gl.Coord - best.S;   // vị trí nét trục trên bản vẽ (toạ độ mô hình hiện tại)
                double bestLine = double.NaN, bd = 0.35;
                foreach (var x in lines) { double d = Math.Abs(x - want); if (d < bd) { bd = d; bestLine = x; } }
                if (!double.IsNaN(bestLine)) refined.Add(gl.Coord - bestLine);
            }
            if (refined.Count > 0) { refined.Sort(); best.S = refined[refined.Count / 2]; }
            return best;
        }

        var vx = Vote(gx, a => a.X, vert);
        var vy = Vote(gy, a => a.Y, horz);
        res.MatchX = vx.N; res.MatchY = vy.N;
        res.Dx = Math.Round(vx.S, 4); res.Dy = Math.Round(vy.S, 4);
        res.Names = vx.Names.Concat(vy.Names).ToList();
        bool okX = vx.N >= Math.Min(2, gx.Count) && vx.N > 0, okY = vy.N >= Math.Min(2, gy.Count) && vy.N > 0;
        res.Ok = okX && okY;
        res.Message = res.Ok
            ? $"Khớp {vx.N} trục phương X ({string.Join(", ", vx.Names)}) và {vy.N} trục phương Y ({string.Join(", ", vy.Names)}): dời dx = {res.Dx:0.###} m, dy = {res.Dy:0.###} m."
            : $"Chưa đủ trục trùng tên để khớp: phương X {vx.N} trục, phương Y {vy.N} trục (cần ≥ 2 mỗi phương, hoặc đủ số trục nếu mô hình chỉ có 1). Dùng cách bấm điểm.";
        return res;
    }
}

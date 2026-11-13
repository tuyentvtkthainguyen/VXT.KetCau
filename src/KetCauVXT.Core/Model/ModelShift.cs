namespace KetCauVXT.Model;

/// <summary>[Ánh xạ Xref] Dời TOÀN BỘ mô hình (mặt bằng kết cấu + kiến trúc + điện nước) một vector (dx, dy) trong mặt bằng.
/// Dời các toạ độ tuyệt đối: trục, nút, tường, lỗ sàn, cầu thang, hố khoan, ranh đất (khi nhập toạ độ), phòng / sàn nền / trần / mái,
/// đồ đạc, lan can, mặt cắt người dùng vẽ, thiết bị – tuyến điện nước, điều hoà. Toạ độ tương đối (cọc theo tim cột, độ lệch tâm,
/// tải theo chiều dài thanh…) giữ nguyên. Bản vẽ liên kết: tuỳ chọn dời theo (giữ quan hệ với mô hình) – trừ bản vẽ làm mốc.</summary>
public static class ModelShift
{
    public sealed record Result(int Grids, int Nodes, int Walls, int Others, int Links)
    {
        public string Summary => $"{Grids} trục, {Nodes} nút, {Walls} tường, {Others} đối tượng khác" + (Links > 0 ? $", {Links} bản vẽ liên kết dời theo" : "");
    }

    public static Result Translate(Project p, double dx, double dy, string keepLinkId = null, bool moveOtherLinks = true)
    {
        if (p == null || (Math.Abs(dx) < 1e-12 && Math.Abs(dy) < 1e-12)) return new Result(0, 0, 0, 0, 0);
        int nG = 0, nN = 0, nW = 0, nO = 0, nL = 0;
        foreach (var g in p.Grids) { g.Coord = R(g.Coord + (g.Dir == GridDir.X ? dx : dy)); nG++; }
        foreach (var n in p.Nodes) { n.X = R(n.X + dx); n.Y = R(n.Y + dy); nN++; }
        foreach (var w in p.Walls) { w.X1 = R(w.X1 + dx); w.Y1 = R(w.Y1 + dy); w.X2 = R(w.X2 + dx); w.Y2 = R(w.Y2 + dy); nW++; }
        foreach (var o in p.Openings) { o.X0 = R(o.X0 + dx); o.Y0 = R(o.Y0 + dy); o.X1 = R(o.X1 + dx); o.Y1 = R(o.Y1 + dy); nO++; }
        foreach (var s in p.Stairs) { s.X0 = R(s.X0 + dx); s.Y0 = R(s.Y0 + dy); s.X1 = R(s.X1 + dx); s.Y1 = R(s.Y1 + dy); nO++; }
        foreach (var r in p.RoofFramings) { r.X0 = R(r.X0 + dx); r.Y0 = R(r.Y0 + dy); r.X1 = R(r.X1 + dx); r.Y1 = R(r.Y1 + dy); nO++; }
        foreach (var b in p.Boreholes) { b.X = R(b.X + dx); b.Y = R(b.Y + dy); nO++; }
        if (p.Foundation?.Boundaries != null)
            foreach (var b in p.Foundation.Boundaries)
                if (string.IsNullOrWhiteSpace(b.Grid)) b.Coord = R(b.Coord + (b.IsVertical ? dx : dy));
        var a = p.Arch;
        if (a != null)
        {
            foreach (var x in a.GroundSlabs) nO += Poly(x.Outline, dx, dy);
            foreach (var x in a.Rooms) nO += Poly(x.Outline, dx, dy);
            foreach (var x in a.Ceilings) nO += Poly(x.Outline, dx, dy);
            foreach (var x in a.Roofs) nO += Poly(x.Outline, dx, dy);
            foreach (var x in a.Railings) nO += Poly(x.Points, dx, dy);
            foreach (var f in a.Furniture) { f.X = R(f.X + dx); f.Y = R(f.Y + dy); nO++; }
            foreach (var s in a.Sections) { s.X1 = R(s.X1 + dx); s.Y1 = R(s.Y1 + dy); s.X2 = R(s.X2 + dx); s.Y2 = R(s.Y2 + dy); nO++; }
        }
        var m = p.Mep;
        if (m != null)
        {
            foreach (var d in m.Devices) { d.X = R(d.X + dx); d.Y = R(d.Y + dy); nO++; }
            foreach (var r in m.Runs) { nO += Poly(r.ManualPoints, dx, dy); r.Path = Pts(r.Path, dx, dy); }
            foreach (var s in m.WallSnaps) { s.X1 = R(s.X1 + dx); s.Y1 = R(s.Y1 + dy); s.X2 = R(s.X2 + dx); s.Y2 = R(s.Y2 + dy); }
            foreach (var f in m.Fittings) { f.X = R(f.X + dx); f.Y = R(f.Y + dy); }
            foreach (var w in m.Warnings) { w.X = R(w.X + dx); w.Y = R(w.Y + dy); }
            var h = m.Hvac;
            if (h != null)
            {
                foreach (var d in h.Ducts) { d.Path = Pts(d.Path, dx, dy); nO++; }
                foreach (var q in h.Pipes) { q.Path = Pts(q.Path, dx, dy); nO++; }
                foreach (var t in h.Terminals) { t.X = R(t.X + dx); t.Y = R(t.Y + dy); nO++; }
                foreach (var u in h.Units) { u.X = R(u.X + dx); u.Y = R(u.Y + dy); nO++; }
                foreach (var f in h.Fittings) { f.X = R(f.X + dx); f.Y = R(f.Y + dy); }
            }
        }
        if (moveOtherLinks)
            foreach (var l in p.CadLinks)
                if (l.Id != keepLinkId) { l.OffsetX = R(l.OffsetX + dx); l.OffsetY = R(l.OffsetY + dy); nL++; }
        return new Result(nG, nN, nW, nO, nL);
    }

    private static double R(double v) => Math.Round(v, 6);

    private static int Poly(List<double[]> pts, double dx, double dy)
    {
        if (pts == null) return 0;
        foreach (var q in pts)
            if (q != null && q.Length >= 2) { q[0] = R(q[0] + dx); q[1] = R(q[1] + dy); }
        return 1;
    }

    private static List<KetCauVXT.Mep.MepPt> Pts(List<KetCauVXT.Mep.MepPt> path, double dx, double dy) =>
        path?.Select(q => new KetCauVXT.Mep.MepPt(R(q.X + dx), R(q.Y + dy), q.Z, q.WallId)).ToList() ?? new List<KetCauVXT.Mep.MepPt>();
}

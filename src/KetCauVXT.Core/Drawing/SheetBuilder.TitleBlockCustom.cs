using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Khung tên người dùng (TitleBlockInfo.Custom – tải từ DWG/DXF/ảnh): đặt lên tờ, thay trường bằng giá trị của dự án/tờ,
/// vẽ trên lớp KHUNG_TEN (chuẩn CAD đổi tên lớp như các hình khác). Thay cho khung tên mặc định khi đã đặt.</summary>
public static partial class SheetBuilder
{
    /// <summary>Dự án đang dùng khung tên người dùng.</summary>
    public static bool HasCustomTitle(Project p)
    {
        var c = p?.TitleBlock?.Custom;
        if (c == null) return false;
        return (c.Entities != null && c.Entities.Count > 0) || !string.IsNullOrEmpty(c.ImageData) || (c.Fields != null && c.Fields.Count > 0);
    }

    /// <summary>Phép đặt khung tên lên tờ: điểm cục bộ P → K·P + (Dx, Dy) (mm giấy).</summary>
    public static (double K, double Dx, double Dy) CustomTitlePlacement(TitleBlockDef def, Sheet s)
    {
        if (def == null || s == null) return (1, 0, 0);
        double k = def.Scale > 0 ? def.Scale : 1;
        if (def.Mode == TitleBlockMode.WholeFrame)
        {
            var (pw, ph) = s.PaperSize();
            double w = def.W, h = def.H;
            if (def.FitToPaper && w > 1e-6 && h > 1e-6)
            {
                k = Math.Min(pw / w, ph / h);
                return (k, -def.MinX * k + (pw - w * k) / 2, -def.MinY * k + (ph - h * k) / 2);
            }
            return (k, -def.MinX * k, -def.MinY * k);
        }
        var (inner, _) = Frame(s);
        return (k, inner.X2, inner.Y1);
    }

    /// <summary>Phạm vi khung tên người dùng trên giấy (mm); rỗng nếu không dùng.</summary>
    public static Rect2 CustomTitleRect(Project p, Sheet s)
    {
        if (!HasCustomTitle(p) || s == null || !s.ShowTitleBlock) return Rect2.Empty;
        var def = p.TitleBlock.Custom;
        var (k, dx, dy) = CustomTitlePlacement(def, s);
        return new Rect2(def.MinX * k + dx, def.MinY * k + dy, def.MaxX * k + dx, def.MaxY * k + dy);
    }

    /// <summary>Ô ảnh của khung tên dạng ảnh trên giấy (mm, y lên); rỗng nếu không phải khung tên ảnh.</summary>
    public static Rect2 CustomTitleImageRect(Project p, Sheet s)
    {
        if (!HasCustomTitle(p) || p.TitleBlock.Custom.Kind != TitleBlockKind.Image || string.IsNullOrEmpty(p.TitleBlock.Custom.ImageData)) return Rect2.Empty;
        return CustomTitleRect(p, s);
    }

    /// <summary>Ô logo trong khung tên người dùng (mm giấy).</summary>
    public static Rect2 CustomTitleLogoRect(Project p, Sheet s)
    {
        var def = p?.TitleBlock?.Custom;
        if (def == null || s == null || !s.ShowTitleBlock || def.LogoW <= 0 || def.LogoH <= 0 || string.IsNullOrEmpty(p.TitleBlock.LogoData)) return Rect2.Empty;
        var (k, dx, dy) = CustomTitlePlacement(def, s);
        return new Rect2(def.LogoX * k + dx, def.LogoY * k + dy, (def.LogoX + def.LogoW) * k + dx, (def.LogoY + def.LogoH) * k + dy);
    }

    /// <summary>Giấy + khung viền (nếu cần) + khung tên người dùng của tờ (mm giấy, 1/1) – thay cho khung tên mặc định.</summary>
    public static DxfWriter CustomTitleBlock(Project p, Sheet s, string scaleText = null)
    {
        var w = new DxfWriter(1);
        string L = Lay.KhungTen;
        var (pw, ph) = s.PaperSize();
        var def = p.TitleBlock.Custom;
        w.Rect(L, 0, 0, pw, ph);
        if (s.ShowFrame && def.Mode == TitleBlockMode.TitleBox && def.DrawInnerFrame)
        {
            var (inner, _) = Frame(s);
            w.Polyline(L, new List<(double X, double Y)> { (inner.X1, inner.Y1), (inner.X2, inner.Y1), (inner.X2, inner.Y2), (inner.X1, inner.Y2) }, true, 0.5);
        }
        if (!s.ShowTitleBlock) return w;
        var (k, dx, dy) = CustomTitlePlacement(def, s);
        var values = TitleFields.Values(p, s, scaleText);
        AppendTitleDef(w, def, values, k, dx, dy);
        return w;
    }

    /// <summary>Hình khung tên người dùng ở toạ độ cục bộ (xem trước trong hộp thoại). values null = hiện chỗ trống/thẻ gốc.</summary>
    public static DxfWriter TitleDefFigure(TitleBlockDef def, IReadOnlyDictionary<string, string> values = null)
    {
        var w = new DxfWriter(1);
        if (def != null) AppendTitleDef(w, def, values, 1, 0, 0);
        return w;
    }

    /// <summary>Chữ hiển thị của một chữ khung tên sau khi thay trường. values null = giữ nguyên (xem trước chỗ trống).</summary>
    public static string TitleText(TitleBlockEntity e, IReadOnlyDictionary<string, string> values)
    {
        if (e == null) return "";
        if (values == null) return !string.IsNullOrEmpty(e.Field) ? "{" + e.Field + "}" : e.Text ?? "";
        if (!string.IsNullOrEmpty(e.Field)) return values.TryGetValue(e.Field, out var v) ? v ?? "" : "";
        return TitleFields.Fill(e.Text, values);
    }

    private static void TitleTextLines(DxfWriter w, string L, string text, double x, double y, double h, double rot, int ha, int va, double maxW)
    {
        var lines = TextLines(text);
        double sp = SheetLayoutMath.LineSpacing * h;
        double a = rot * Math.PI / 180, ddx = Math.Sin(a), ddy = -Math.Cos(a);
        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i].TrimEnd();
            if (t.Length == 0) continue;
            double hh = h;
            if (maxW > 0) hh = Math.Max(0.5, Math.Min(h, maxW / Math.Max(1, t.Length * 0.62)));
            w.Text(L, x + ddx * i * sp, y + ddy * i * sp, hh, t, rot, Math.Clamp(ha, 0, 2), Math.Clamp(va, 0, 3));
        }
    }

    private static void AppendTitleDef(DxfWriter w, TitleBlockDef def, IReadOnlyDictionary<string, string> values, double k, double dx, double dy)
    {
        string L = Lay.KhungTen;
        (double X, double Y) P(double x, double y) => (x * k + dx, y * k + dy);
        if (def.Kind == TitleBlockKind.Image)
        {
            var (x1, y1) = P(def.MinX, def.MinY);
            var (x2, y2) = P(def.MaxX, def.MaxY);
            w.Rect(L, x1, y1, x2, y2);   // DXF R12 không có ảnh: viền ô ảnh
        }
        foreach (var e in def.Entities ?? new List<TitleBlockEntity>())
        {
            if (e?.Pts == null || e.Pts.Count < 2) continue;
            switch (e.Type)
            {
                case "LINE":
                    if (e.Pts.Count >= 4)
                    {
                        var (a1, b1) = P(e.Pts[0], e.Pts[1]);
                        var (a2, b2) = P(e.Pts[2], e.Pts[3]);
                        w.Line(L, a1, b1, a2, b2);
                    }
                    break;
                case "PLINE":
                {
                    var pts = new List<(double X, double Y)>();
                    for (int i = 0; i + 1 < e.Pts.Count; i += 2) pts.Add(P(e.Pts[i], e.Pts[i + 1]));
                    if (pts.Count >= 2) w.Polyline(L, pts, e.Closed && pts.Count > 2, Math.Max(0, e.Width * k));
                    break;
                }
                case "CIRCLE":
                    if (e.R > 0) { var (cx, cy) = P(e.Pts[0], e.Pts[1]); w.Circle(L, cx, cy, e.R * k); }
                    break;
                case "ARC":
                    if (e.R > 0) { var (cx, cy) = P(e.Pts[0], e.Pts[1]); w.Arc(L, cx, cy, e.R * k, e.A1, e.A2); }
                    break;
                case "TEXT":
                {
                    string t = TitleText(e, values);
                    if (string.IsNullOrWhiteSpace(t)) break;
                    var (tx, ty) = P(e.Pts[0], e.Pts[1]);
                    TitleTextLines(w, L, t, tx, ty, Math.Max(0.3, e.H * k), e.Rot, e.Ha, e.Va, e.MaxW * k);
                    break;
                }
            }
        }
        foreach (var f in def.Fields ?? new List<TitleBlockField>())
        {
            if (f == null) continue;
            string fmt = string.IsNullOrEmpty(f.Format) ? "{" + f.Key + "}" : f.Format;
            string t = values == null ? fmt : TitleFields.Fill(fmt, values);
            if (string.IsNullOrWhiteSpace(t)) continue;
            var (tx, ty) = P(f.X, f.Y);
            TitleTextLines(w, L, t, tx, ty, Math.Max(0.3, f.H * k), f.Rot, f.Ha, f.Va, f.MaxW * k);
        }
    }
}

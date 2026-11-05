namespace KetCauVXT.Drawing;

/// <summary>Tránh chồng nhãn đơn giản: lưu các hình chữ nhật đã chiếm; khi đặt nhãn mới
/// thì dịch theo một hướng từng bước tới khi không còn chạm.</summary>
public sealed class TagLayout
{
    private readonly List<Rect2> _occupied = new();
    public double Gap { get; set; }

    public TagLayout(double gap = 0) { Gap = gap; }

    public IReadOnlyList<Rect2> Occupied => _occupied;

    public void Reserve(Rect2 r) { if (!r.IsEmpty) _occupied.Add(r.Inflate(Gap)); }

    public bool IsFree(Rect2 r)
    {
        foreach (var o in _occupied) if (o.Intersects(r)) return false;
        return true;
    }

    /// <summary>Tìm độ dịch (dx,dy) = k·(stepX,stepY), k = 0..maxSteps, đầu tiên để r không chạm vùng đã chiếm.
    /// Nếu không tìm được trả về bước cuối. Vùng tìm được được giữ chỗ luôn.</summary>
    public (double Dx, double Dy) Place(Rect2 r, double stepX, double stepY, int maxSteps = 60)
    {
        for (int k = 0; k <= maxSteps; k++)
        {
            var t = r.Offset(stepX * k, stepY * k);
            if (IsFree(t)) { Reserve(t); return (stepX * k, stepY * k); }
        }
        Reserve(r.Offset(stepX * maxSteps, stepY * maxSteps));
        return (stepX * maxSteps, stepY * maxSteps);
    }

    /// <summary>Thử dịch xen kẽ hai phía (0, +1, −1, +2, −2...) – dùng cho nhãn nằm giữa hai vùng.</summary>
    public (double Dx, double Dy) PlaceBothWays(Rect2 r, double stepX, double stepY, int maxSteps = 30)
    {
        for (int k = 0; k <= maxSteps; k++)
        {
            foreach (int sg in k == 0 ? new[] { 1 } : new[] { 1, -1 })
            {
                var t = r.Offset(stepX * k * sg, stepY * k * sg);
                if (IsFree(t)) { Reserve(t); return (stepX * k * sg, stepY * k * sg); }
            }
        }
        Reserve(r);
        return (0.0, 0.0);
    }
}

/// <summary>Một mảnh hình (mặt đứng, mặt cắt...) vẽ ở toạ độ cục bộ.</summary>
public sealed class Figure
{
    public DxfWriter W;
    public string Name = "";
    /// <summary>Bắt đầu một hàng mới trước hình này.</summary>
    public bool NewRow;
    public Figure(DxfWriter w, string name = "", bool newRow = false) { W = w; Name = name; NewRow = newRow; }
}

/// <summary>Xếp các mảnh hình trái→phải, trên→dưới vào các khổ giấy (A1/A3) có khung tên;
/// hết chỗ thì thêm khung mới bên phải khung trước.</summary>
public static class SheetComposer
{
    public sealed class SheetInfo
    {
        public string ProjectName = "";
        public string Title = "";
        public string SheetNo = "KC-01";
        public string Designer = "";
        public string Owner = "";
        public string Location = "";
    }

    /// <summary>Khổ giấy (mm in): A1 = 841×594, A3 = 420×297.</summary>
    public static (double W, double H) Paper(string size) => size switch
    {
        "A3" => (420.0, 297.0),
        "A2" => (594.0, 420.0),
        "A0" => (1189.0, 841.0),
        _ => (841.0, 594.0)
    };

    /// <summary>Ghép hình vào khung. scale: mẫu số tỷ lệ của tờ (chữ khung tên tính theo scale này).</summary>
    public static DxfWriter Compose(IList<Figure> figs, double scale, SheetInfo info, string paper = "A1")
    {
        var outW = new DxfWriter(scale);
        var (pw, ph) = Paper(paper);
        double S = scale;
        double fw = pw * S, fh = ph * S;
        double mL = 25 * S, mO = 10 * S;          // lề đóng gáy trái 25, lề khác 10
        double tbH = 45 * S;                      // chiều cao khung tên (dải dưới cùng bên phải)
        double tbW = 180 * S;
        double gap = 12 * S;

        double innerX1 = mL, innerX2 = fw - mO, innerY1 = mO, innerY2 = fh - mO;
        double usableTop = innerY2 - gap;
        double usableLeft = innerX1 + gap, usableRight = innerX2 - gap;
        double usableBottom = innerY1 + gap;

        int sheet = 0;
        var placements = new List<(Figure F, int Sheet, double Dx, double Dy)>();
        double cx = usableLeft, rowTop = usableTop, rowH = 0;
        var list = figs.Where(f => f != null && f.W != null && f.W.EntityCount > 0).ToList();

        foreach (var f in list)
        {
            var e = f.W.Extents;
            double w = e.W, h = e.H;
            bool fitsRow = cx + w <= usableRight + 1e-6;
            if (f.NewRow || !fitsRow)
            {
                if (cx > usableLeft + 1e-6) { rowTop -= rowH + gap; cx = usableLeft; rowH = 0; }
            }
            // phần dưới cùng bên phải dành cho khung tên
            double bottomLimit = usableBottom;
            bool overTitle = cx + w > innerX2 - tbW - gap;
            if (overTitle) bottomLimit = innerY1 + tbH + gap;
            if (rowTop - h < bottomLimit && (rowTop < usableTop - 1e-6 || cx > usableLeft + 1e-6))
            {
                // sang tờ mới
                sheet++;
                cx = usableLeft; rowTop = usableTop; rowH = 0;
            }
            double ox = sheet * (fw + 20 * S);
            placements.Add((f, sheet, ox + cx - e.X1, rowTop - e.Y2));
            cx += w + gap;
            rowH = Math.Max(rowH, h);
        }

        int nSheets = Math.Max(1, sheet + 1);
        for (int i = 0; i < nSheets; i++)
        {
            double ox = i * (fw + 20 * S);
            DrawFrame(outW, ox, 0, fw, fh, mL, mO, tbW, tbH, info, i, nSheets);
        }
        foreach (var (f, _, dx, dy) in placements) outW.Append(f.W, dx, dy);
        return outW;
    }

    private static void DrawFrame(DxfWriter w, double ox, double oy, double fw, double fh, double mL, double mO,
        double tbW, double tbH, SheetInfo info, int idx, int count)
    {
        string L = Lay.KhungTen;
        double S = w.Scale, th = w.Th;
        w.Rect(L, ox, oy, ox + fw, oy + fh);
        double x1 = ox + mL, y1 = oy + mO, x2 = ox + fw - mO, y2 = oy + fh - mO;
        w.Polyline(L, new List<(double X, double Y)> { (x1, y1), (x2, y1), (x2, y2), (x1, y2) }, true, 0.5 * S);

        double tx1 = x2 - tbW, ty2 = y1 + tbH;
        w.Rect(L, tx1, y1, x2, ty2);
        double r1 = y1 + tbH * 0.75, r2 = y1 + tbH * 0.5, r3 = y1 + tbH * 0.25;
        w.Line(L, tx1, r1, x2, r1);
        w.Line(L, tx1, r2, x2, r2);
        w.Line(L, tx1, r3, x2, r3);
        double c1 = tx1 + 40 * S, c2 = x2 - 40 * S;
        w.Line(L, c1, y1, c1, ty2);
        w.Line(L, c2, y1, c2, r2);

        double pad = 2 * S;
        double rowH = tbH * 0.25;
        void Cell(double xa, double ya, string label, string value, double hScale = 1.0)
        {
            w.Text(L, xa + pad, ya + rowH / 2, th * hScale, label ?? "", 0, 0, 2);
        }
        Cell(tx1, r1, "CÔNG TRÌNH", "");
        w.Text(L, c1 + pad, r1 + rowH / 2, 1.2 * th, info.ProjectName ?? "", 0, 0, 2);
        Cell(tx1, r2, "ĐỊA ĐIỂM", "");
        w.Text(L, c1 + pad, r2 + rowH / 2, th, info.Location ?? "", 0, 0, 2);
        Cell(tx1, r3, "TÊN BẢN VẼ", "");
        string title = info.Title ?? "";
        if (count > 1) title += $" ({idx + 1}/{count})";
        w.Text(L, c1 + pad, r3 + rowH / 2, 1.3 * th, title, 0, 0, 2);
        w.Text(L, c2 + pad, r3 + rowH * 0.70, 0.8 * th, "SỐ HIỆU BẢN VẼ", 0, 0, 2);
        string no = count > 1 ? $"{info.SheetNo}.{idx + 1}" : info.SheetNo;
        w.Text(L, c2 + 20 * S, r3 + rowH * 0.30, 1.4 * th, no ?? "", 0, 1, 2);
        Cell(tx1, y1, "THIẾT KẾ", "");
        w.Text(L, c1 + pad, y1 + rowH / 2, th, "Thiết kế: " + (info.Designer ?? ""), 0, 0, 2);
        w.Text(L, c2 + pad, y1 + rowH / 2, 0.8 * th, "Việt Const", 0, 0, 2);
    }
}

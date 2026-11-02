using System.Globalization;
using System.Text.Json;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Một mảnh hình đặt lên tờ: điểm mô hình P → điểm giấy K·P + (Dx, Dy) (mm giấy, gốc trái dưới).</summary>
public sealed class PlacedFigure
{
    public Figure F;
    public double K, Dx, Dy;
}

/// <summary>Kết quả xếp các mảnh hình của một khung nhìn vào hình chữ nhật của nó.</summary>
public sealed class ViewLayout
{
    public List<PlacedFigure> Items = new();
    /// <summary>Hệ số phóng so với tỷ lệ gốc của mảnh hình (1 = đúng tỷ lệ gốc).</summary>
    public double Zoom = 1;
    /// <summary>Mẫu số tỷ lệ thực của khung nhìn (theo mảnh hình chính).</summary>
    public double EffectiveScale;
    /// <summary>Nội dung tràn ra ngoài khung (khi đặt tỷ lệ cố định quá lớn).</summary>
    public bool Overflow;
    /// <summary>Phạm vi nội dung trên giấy (mm).</summary>
    public Rect2 Content = Rect2.Empty;
    /// <summary>Tên khung nhìn (nếu có Title) vẽ ở mép dưới khung, toạ độ giấy, tỷ lệ 1/1. Có thể null.</summary>
    public DxfWriter Caption;
}

/// <summary>Dàn trang bản vẽ (paper space): dựng mảnh hình cho từng khung nhìn, xếp vào khung theo tỷ lệ,
/// vẽ khung tên, xuất mỗi tờ thành một file DXF (đơn vị mm giấy, tỷ lệ 1/1) và tạo bộ tờ mặc định.</summary>
public static partial class SheetBuilder
{
    public const double MarginLeft = 25, MarginOther = 10;
    public const double TitleW = 180, TitleH = 40;
    /// <summary>Khoảng hở giữa khung viền và các khung nhìn / giữa các khung nhìn (mm).</summary>
    public const double Gap = 6;
    /// <summary>Lề trong của khung nhìn (mm).</summary>
    public const double ViewPad = 3;
    /// <summary>Khoảng cách giữa các mảnh hình trong một khung nhìn (mm giấy ở tỷ lệ gốc).</summary>
    public const double FigGap = 8;
    /// <summary>Bước bắt điểm khi kéo khung nhìn (mm).</summary>
    public const double Snap = 5;
    public const double CaptionH = 9;
    public static readonly string[] Papers = { "A0", "A1", "A2", "A3", "A4" };
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ==================================================================
    // Tên, mô tả
    // ==================================================================
    public static string KindName(SheetViewKind k) => k switch
    {
        SheetViewKind.PlanStory => "Mặt bằng kết cấu tầng",
        SheetViewKind.SlabRebarPlan => "Mặt bằng thép sàn",
        SheetViewKind.Elevation => "Mặt đứng khung trục",
        SheetViewKind.ForceDiagram => "Biểu đồ nội lực khung",
        SheetViewKind.FoundationPlan => "Mặt bằng móng",
        SheetViewKind.FoundationDetail => "Chi tiết móng",
        SheetViewKind.BeamDetail => "Chi tiết dầm",
        SheetViewKind.ColumnDetail => "Chi tiết cột",
        SheetViewKind.RebarSchedule => "Bảng thống kê thép",
        SheetViewKind.Notes => "Ghi chú chung",
        SheetViewKind.View3D => "Phối cảnh 3D",
        SheetViewKind.RebarShop => "Gia công thép (shop drawing)",   // RebarShopDrawing.cs
        SheetViewKind.StairDetail => "Chi tiết cầu thang",            // StairDrawing.cs
        _ => k.ToString()
    };

    /// <summary>Các loại khung nhìn có thể thêm từ bảng chọn (không gồm 3D).</summary>
    public static readonly SheetViewKind[] PaletteKinds =
    {
        SheetViewKind.PlanStory, SheetViewKind.SlabRebarPlan, SheetViewKind.Elevation, SheetViewKind.ForceDiagram,
        SheetViewKind.FoundationPlan, SheetViewKind.FoundationDetail, SheetViewKind.BeamDetail, SheetViewKind.ColumnDetail,
        SheetViewKind.RebarSchedule, SheetViewKind.Notes, SheetViewKind.RebarShop,
        SheetViewKind.StairDetail
    };

    public static string Describe(SheetView v)
    {
        if (v == null) return "";
        string par = v.Kind == SheetViewKind.Notes ? "" : (string.IsNullOrEmpty(v.Param) ? "" : " " + v.Param);
        string t = string.IsNullOrEmpty(v.Title) ? "" : $" – {v.Title}";
        string sc = v.Scale > 0 ? $" (1/{v.Scale.ToString("0.##", Inv)})" : " (vừa khung)";
        return KindName(v.Kind) + par + t + sc;
    }

    public static string FileName(Sheet s) => DrawUtil.SafeFile($"{s.Number}_{s.Name}") + ".dxf";

    // ==================================================================
    // Khung giấy
    // ==================================================================
    /// <summary>Khung viền trong (lề trái 25, lề khác 10; khổ nhỏ dọc A4 lề trái 20) và ô khung tên (góc phải dưới).</summary>
    public static (Rect2 Inner, Rect2 Title) Frame(Sheet s)
    {
        var (pw, ph) = s.PaperSize();
        double mL = pw < 250 ? 20 : MarginLeft, mO = pw < 250 ? 5 : MarginOther;
        // lề tự chọn của tờ (< 0 = mặc định) – dàn trang thủ công
        double mR = s.MarginRight >= 0 ? s.MarginRight : mO, mT = s.MarginTop >= 0 ? s.MarginTop : mO, mB = s.MarginBottom >= 0 ? s.MarginBottom : mO;
        if (s.MarginLeft >= 0) mL = s.MarginLeft;
        var inner = new Rect2(mL, mB, Math.Max(mL + 20, pw - mR), Math.Max(mB + 20, ph - mT));
        double tw = Math.Min(TitleW, inner.W);
        var tb = new Rect2(inner.X2 - tw, inner.Y1, inner.X2, inner.Y1 + TitleH);
        return (inner, tb);
    }

    /// <summary>Vùng vẽ (trong khung viền, chừa khoảng hở Gap).</summary>
    public static Rect2 DrawingArea(Sheet s)
    {
        var (inner, _) = Frame(s);
        return inner.Inflate(-Gap);
    }

    public static bool InsidePaper(Sheet s, SheetView v)
    {
        var (pw, ph) = s.PaperSize();
        return v.W > 0 && v.H > 0 && v.X >= -1e-6 && v.Y >= -1e-6 && v.X + v.W <= pw + 1e-6 && v.Y + v.H <= ph + 1e-6;
    }

    // ==================================================================
    // Mảnh hình của khung nhìn
    // ==================================================================
    public static Story FindStory(Project p, string name)
    {
        var list = p.Stories.OrderBy(x => x.Elevation).ToList();
        if (list.Count == 0) return null;
        return list.FirstOrDefault(x => string.Equals(x.Name, (name ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
               ?? list.FirstOrDefault(x => x.Elevation > p.BaseElevation + 1e-6) ?? list[0];
    }

    public static string BeamStory(Project p, BeamLineDesign b)
    {
        if (!string.IsNullOrEmpty(b.Story)) return b.Story;
        var st = p.Stories.OrderBy(x => Math.Abs(x.Elevation - b.TopElevation)).FirstOrDefault();
        return st?.Name ?? "Khac";
    }

    private static Figure Placeholder(string msg)
    {
        var w = new DxfWriter(1);
        w.Rect(Lay.KyHieu, 0, 0, Math.Max(60, DxfWriter.TextWidth(msg, 3) + 8), 14);
        w.Text(Lay.Chu, 4, 7, 3, msg, 0, 0, 2);
        return new Figure(w, "placeholder");
    }

    /// <summary>Mảnh hình ghi chú (tỷ lệ 1/1, mm giấy). Dòng phân cách bằng xuống dòng.</summary>
    public static DxfWriter NotesFigure(string text)
    {
        var w = new DxfWriter(1);
        // cỡ chữ theo Thiết lập bản vẽ: tiêu đề = 2 × chữ ghi chú, nội dung = 1,2 × chữ ghi chú (mặc định 5 và 3 mm như trước)
        double nh = w.Th, hh = 2 * nh, bh = 1.2 * nh;
        double y = 0;
        double tw = DxfWriter.TextWidth(GeneralNotes.Heading, hh) * w.WidthFactor;
        w.Text(Lay.Chu, 0, y, hh, GeneralNotes.Heading);
        w.Line(Lay.Chu, 0, y - 0.24 * hh, tw, y - 0.24 * hh);
        y -= 1.8 * hh;
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            string line = raw.TrimEnd();
            // ngắt dòng dài ~95 ký tự
            while (line.Length > 0)
            {
                string part = line;
                if (part.Length > 95)
                {
                    int cut = part.LastIndexOf(' ', 95);
                    if (cut < 40) cut = 95;
                    part = line.Substring(0, cut);
                }
                w.Text(Lay.Chu, 0, y, bh, part);
                y -= bh * 5.5 / 3;
                line = line.Substring(part.Length).TrimStart();
                if (line.Length > 0) line = "   " + line;
            }
            if (raw.Trim().Length == 0) y -= bh;
        }
        return w;
    }

    public static string DefaultNotes(Project p)
    {
        var c = p.Concrete ?? new ConcreteDesignSettings();
        string mm(double m) => Math.Round(m * 1000).ToString("0", Inv);
        return string.Join("\n", new[]
        {
            "1. Kích thước ghi trên bản vẽ tính bằng mm, cao độ tính bằng m. Cao độ ±0.000 lấy theo cao độ nền tầng 1.",
            $"2. Bê tông: cột {c.ConcreteColumn}, dầm {c.ConcreteBeam}, sàn {c.ConcreteSlab}, móng {c.ConcreteFooting}; bê tông lót móng B7,5 dày 100.",
            $"3. Cốt thép dọc chịu lực {c.RebarMain}; cốt đai và thép sàn {c.RebarStirrup}.",
            $"4. Chiều dày lớp bê tông bảo vệ: dầm {mm(c.CoverBeam)}, cột {mm(c.CoverColumn)}, sàn {mm(c.CoverSlab)}, móng {mm(c.CoverFooting)}.",
            "5. Neo, nối cốt thép theo TCVN 5574:2018; mối nối so le, không nối quá 50% số thanh tại một mặt cắt.",
            "6. Thi công và nghiệm thu theo TCVN 4453:1995, TCVN 9115:2019 và các tiêu chuẩn hiện hành.",
            "7. Khi có sai khác giữa bản vẽ và hiện trường phải báo cho đơn vị thiết kế để xử lý."
        }.Concat(GeneralNotes.HasCurvedBeams(p)
            ? new[] { "8. Dầm cong: mặt đứng thể hiện trải theo cung, chiều dài thép dọc tính theo cung; dùng đai kín móc 135°." }
            : Array.Empty<string>()));
    }

    /// <summary>Tỷ lệ của loại khung nhìn theo Thiết lập bản vẽ (0 = không có tỷ lệ riêng: bảng, ghi chú, 3D, gia công).</summary>
    public static double StyleScale(SheetViewKind k, Project p = null)
    {
        var st = DrawingStyle.Current;
        return k switch
        {
            SheetViewKind.PlanStory => st.ScaleOf(DrawingKind.FramingPlan),
            SheetViewKind.SlabRebarPlan => p != null ? SlabDrawing.ChooseScale(p) : st.ScaleOf(DrawingKind.SlabPlan),
            SheetViewKind.Elevation or SheetViewKind.ForceDiagram => st.ScaleOf(DrawingKind.FrameElevation),
            SheetViewKind.FoundationPlan => st.ScaleOf(DrawingKind.FoundationPlan),
            SheetViewKind.FoundationDetail => st.ScaleOf(DrawingKind.FootingDetail),
            SheetViewKind.BeamDetail => st.ScaleOf(DrawingKind.BeamElevation),
            SheetViewKind.ColumnDetail => st.ScaleOf(DrawingKind.ColumnElevation),
            SheetViewKind.StairDetail => st.ScaleOf(DrawingKind.StairPlan),
            _ => 0
        };
    }

    /// <summary>Danh sách mảnh hình (từ các bộ vẽ sẵn có) cho một khung nhìn.</summary>
    public static List<Figure> Figures(Project p, AnalysisResults r, DesignSummary d, SheetView v)
    {
        var res = new List<Figure>();
        if (p == null || v == null) return res;
        p.InvalidateIndex();
        string par = (v.Param ?? "").Trim();
        try
        {
            switch (v.Kind)
            {
                case SheetViewKind.PlanStory:
                {
                    var st = FindStory(p, par);
                    if (st != null) res.Add(new Figure(FramingPlan.Build(p, d, st, FramingPlan.CurrentScale), st.Name, true));
                    break;
                }
                case SheetViewKind.SlabRebarPlan:
                {
                    var st = FindStory(p, par);
                    if (st != null) res.Add(new Figure(SlabDrawing.Build(p, d, st, SlabDrawing.ChooseScale(p)), st.Name, true));
                    break;
                }
                case SheetViewKind.Elevation:
                    res.Add(new Figure(ElevationDrawing.Build(p, null, ElevationDrawing.FindGrid(p, par)), par, true));
                    break;
                case SheetViewKind.ForceDiagram:
                {
                    var parts = par.Split(';');
                    string gname = parts[0];
                    if (gname.Contains('|')) gname = gname.Substring(gname.LastIndexOf('|') + 1);
                    string qty = parts.Length > 1 && parts[1].Trim().Length > 0 ? parts[1].Trim() : "M3";
                    string cs = parts.Length > 2 ? parts[2].Trim() : null;
                    var g = ElevationDrawing.FindGrid(p, gname);
                    res.Add(new Figure(ElevationDrawing.Build(p, r, g, qty, cs), par, true));
                    if (r == null) res.Add(Placeholder("Chưa có kết quả phân tích – chạy Phân tích để vẽ biểu đồ nội lực"));
                    break;
                }
                case SheetViewKind.FoundationPlan:
                case SheetViewKind.FoundationDetail:
                {
                    if (d == null) break;
                    var all = FootingDrawing.BuildAll(p, d);
                    if (all.Count == 0) break;
                    bool IsPlan(Figure f) => (f.Name ?? "").StartsWith("MB", StringComparison.OrdinalIgnoreCase);
                    if (v.Kind == SheetViewKind.FoundationPlan)
                    {
                        var plan = all.Where(IsPlan).ToList();
                        res.AddRange(plan.Count > 0 ? plan : all.Take(1));
                    }
                    else
                    {
                        var det = all.Where(f => !IsPlan(f)).ToList();
                        if (par.Length > 0)
                        {
                            var sel = det.Where(f => f.Name == par || (f.Name ?? "").StartsWith(par + " ", StringComparison.Ordinal)).ToList();
                            if (sel.Count > 0) det = sel;
                        }
                        if (det.Count > 0) det[0].NewRow = true;
                        res.AddRange(det);
                    }
                    break;
                }
                case SheetViewKind.BeamDetail:
                {
                    if (d == null) break;
                    var beams = d.Beams.Where(b => b.Name == par).ToList();
                    if (beams.Count == 0 && par.Length > 0) beams = d.Beams.Where(b => BeamStory(p, b) == par).ToList();
                    if (beams.Count == 0 && par.Length == 0) beams = d.Beams.ToList();
                    res.AddRange(BeamDrawing.BuildAll(p, beams));
                    break;
                }
                case SheetViewKind.ColumnDetail:
                {
                    if (d == null) break;
                    var cols = d.Columns.Where(c => c.Name == par).ToList();
                    if (cols.Count == 0 && par.Length == 0) cols = d.Columns.ToList();
                    res.AddRange(ColumnDrawing.BuildAll(p, cols));
                    break;
                }
                case SheetViewKind.RebarSchedule:
                    if (d != null && d.All.Any(m => m.Bars.Count > 0)) res.AddRange(RebarSchedule.BuildFigures(d, 1));
                    break;
                case SheetViewKind.Notes:
                    res.Add(new Figure(NotesFigure(par.Length > 0 ? v.Param : DefaultNotes(p)), "Ghi chú", true));
                    break;
                case SheetViewKind.View3D:
                    res.Add(Placeholder("Phối cảnh 3D chưa hỗ trợ trên tờ in"));
                    break;
                // ---- gia công thép (shop drawing) – RebarShopDrawing.cs ----
                case SheetViewKind.RebarShop:
                    if (d != null && d.All.Any(m => m.Bars.Count > 0)) res.AddRange(RebarShopDrawing.BuildFigures(p, d, par));
                    break;
                // ---- hết gia công thép ----
                // ---- cầu thang – StairDrawing.cs ----
                case SheetViewKind.StairDetail:
                    if (d?.Stairs != null)
                        foreach (var sd in d.Stairs.Where(x => par.Length == 0 || x.Name == par))
                            res.AddRange(StairDrawing.Build(p, d, sd, StairDrawing.CurrentScale));
                    break;
                // ---- hết cầu thang ----
            }
        }
        catch (Exception ex)
        {
            res.Clear();
            res.Add(new Figure(DrawUtil.ErrorFigure(KindName(v.Kind) + " " + par, ex, 1), "error"));
        }
        res = res.Where(f => f != null && f.W != null && f.W.EntityCount > 0).ToList();
        if (res.Count == 0)
        {
            bool needDesign = v.Kind is SheetViewKind.FoundationPlan or SheetViewKind.FoundationDetail or SheetViewKind.BeamDetail
                or SheetViewKind.ColumnDetail or SheetViewKind.RebarSchedule or SheetViewKind.RebarShop or SheetViewKind.StairDetail;
            res.Add(Placeholder(needDesign && d == null
                ? $"{KindName(v.Kind)}: cần chạy \"Thiết kế BTCT\" trước"
                : $"{KindName(v.Kind)} {par}: không có dữ liệu"));
        }
        // ---- hiệu chỉnh bản vẽ: khoá hình (dùng chung với bộ KC-xx) + áp chỉnh sửa của từng hình (DrawingOverlay.cs) ----
        DrawingOverlay.AssignOwners(res, DrawingOverlay.KindOf(v.Kind));
        DrawingOverlay.ApplyFigures(p, res);
        // ---- hết ----
        return res;
    }

    // ==================================================================
    // Xếp mảnh hình vào khung nhìn
    // ==================================================================
    private static (List<(Figure F, double X, double Y)> Pl, double W, double H) Flow(IList<Figure> figs, double limit)
    {
        var pl = new List<(Figure, double, double)>();
        double cx = 0, rowTop = 0, rowH = 0, maxW = 0;
        foreach (var f in figs)
        {
            var e = f.W.Extents;
            double s = f.W.Scale <= 0 ? 1 : f.W.Scale;
            double w = e.W / s, h = e.H / s;
            if (cx > 1e-9 && (f.NewRow || cx + w > limit + 1e-6)) { rowTop += rowH + FigGap; cx = 0; rowH = 0; }
            pl.Add((f, cx, rowTop));
            cx += w + FigGap;
            rowH = Math.Max(rowH, h);
            maxW = Math.Max(maxW, cx - FigGap);
        }
        return (pl, maxW, rowTop + rowH);
    }

    /// <summary>Kích thước nội dung (mm giấy) khi đặt ở tỷ lệ gốc với bề rộng hàng tối đa limit.</summary>
    public static (double W, double H) NativeSize(IList<Figure> figs, double limit)
    {
        var list = figs.Where(f => f?.W != null && f.W.EntityCount > 0).ToList();
        if (list.Count == 0) return (0, 0);
        var (_, w, h) = Flow(list, limit);
        return (w, h);
    }

    /// <summary>Mẫu số tỷ lệ gốc của khung nhìn (theo mảnh hình đầu tiên).</summary>
    public static double NativeScale(IList<Figure> figs)
    {
        var f = figs?.FirstOrDefault(x => x?.W != null && x.W.EntityCount > 0 && x.Name != "placeholder");
        return f?.W.Scale > 0 ? f.W.Scale : 1;
    }

    /// <summary>Xếp mảnh hình vào khung nhìn v theo tỷ lệ v.Scale (0 = tự co vừa khung, phóng tối đa ×2).
    /// Nội dung căn giữa khung; khung có Title thì chừa dải tên ở mép dưới.</summary>
    public static ViewLayout Layout(IList<Figure> figs, SheetView v)
    {
        var lay = new ViewLayout();
        var list = (figs ?? new List<Figure>()).Where(f => f?.W != null && f.W.EntityCount > 0).ToList();
        bool cap = !v.HideTitle && !string.IsNullOrWhiteSpace(v.Title);
        double ax1 = v.X + ViewPad, ax2 = v.X + v.W - ViewPad;
        double ay1 = v.Y + ViewPad + (cap ? CaptionH : 0), ay2 = v.Y + v.H - ViewPad;
        double aw = Math.Max(1, ax2 - ax1), ah = Math.Max(1, ay2 - ay1);
        double refS = NativeScale(list);
        if (list.Count == 0) { lay.EffectiveScale = v.Scale > 0 ? v.Scale : refS; return lay; }

        List<(Figure F, double X, double Y)> pl;
        double bw, bh, z;
        if (v.Scale > 0)
        {
            z = refS / v.Scale;
            (pl, bw, bh) = Flow(list, aw / z);
        }
        else
        {
            double maxFig = list.Max(f => f.W.Extents.W / (f.W.Scale <= 0 ? 1 : f.W.Scale));
            double total = list.Sum(f => f.W.Extents.W / (f.W.Scale <= 0 ? 1 : f.W.Scale) + FigGap);
            var cands = new List<double> { maxFig, total };
            foreach (double m in new[] { 1.25, 1.5, 2, 2.5, 3, 4, 6, 8, 12 }) if (maxFig * m < total) cands.Add(maxFig * m);
            pl = null; bw = bh = 0; z = 0;
            foreach (double lim in cands)
            {
                var (p2, w2, h2) = Flow(list, lim);
                double z2 = Math.Min(aw / Math.Max(w2, 1e-6), ah / Math.Max(h2, 1e-6));
                if (pl == null || z2 > z + 1e-9) { pl = p2; bw = w2; bh = h2; z = z2; }
            }
            z = Math.Min(z, 2.0);
        }
        lay.Zoom = z;
        lay.EffectiveScale = refS / z;
        double cw = bw * z, ch = bh * z;
        lay.Overflow = cw > aw + 0.5 || ch > ah + 0.5;
        double ox = ax1 + Math.Max(0, (aw - cw) / 2) + v.PanX;      // PanX/PanY: dịch hình trong khung (crop thủ công)
        double top = ay2 - Math.Max(0, (ah - ch) / 2) + v.PanY;
        lay.Content = new Rect2(ox, top - ch, ox + cw, top);
        foreach (var (f, nx, ny) in pl)
        {
            var e = f.W.Extents;
            double s = f.W.Scale <= 0 ? 1 : f.W.Scale;
            double a = z / s;
            lay.Items.Add(new PlacedFigure { F = f, K = a, Dx = ox + nx * z - e.X1 * a, Dy = top - ny * z - e.Y2 * a });
        }
        if (cap)
        {
            var c = new DxfWriter(1);
            string sc = "TL 1/" + Math.Round(lay.EffectiveScale).ToString("0", Inv);
            double cx = v.X + v.W / 2, y = v.Y + ViewPad + 2.5;
            string t = v.Title.Trim();
            double hMax = c.TitleTh;   // cỡ tên hình theo Thiết lập bản vẽ (mặc định 3,5 mm)
            double h = Math.Min(hMax, Math.Max(Math.Min(1.8, hMax), (v.W - 2 * ViewPad) / Math.Max(1, (t.Length + sc.Length + 3) * 0.62 * c.WidthFactor)));
            double tw = DxfWriter.TextWidth(t, h), sw = DxfWriter.TextWidth(sc, 0.75 * h);
            double x0 = cx - (tw + 1.5 * h + sw) / 2;
            c.Text(Lay.Chu, x0, y, h, t);
            c.Line(Lay.Chu, x0, y - 0.35 * h, x0 + tw, y - 0.35 * h);
            c.Text(Lay.Chu, x0 + tw + 1.5 * h, y, 0.75 * h, sc);
            lay.Caption = c;
        }
        return lay;
    }

    // ==================================================================
    // Khung tên
    // ==================================================================
    private static void FitText(DxfWriter w, double x, double y, double maxW, double h, string text, int ha = 0, int va = 2)
    {
        if (string.IsNullOrEmpty(text)) return;
        double hh = Math.Min(h, maxW / Math.Max(1, text.Length * 0.62));
        w.Text(Lay.KhungTen, x, y, Math.Max(1.0, hh), text, 0, ha, va);
    }

    /// <summary>Khung giấy + khung viền + khung tên của tờ (mm giấy, tỷ lệ 1/1).
    /// Khung tên 6 hàng: đơn vị thiết kế, công trình, địa điểm, tên bản vẽ, thiết kế/phê duyệt, kiểm tra/lần sửa;
    /// cột phải: logo (nếu có), tỷ lệ, ngày, số hiệu bản vẽ. Ô trống trên tờ lấy theo khung tên mặc định của công trình.</summary>
    public static DxfWriter TitleBlock(Project p, Sheet s, string scaleText = null)
    {
        if (HasCustomTitle(p)) return CustomTitleBlock(p, s, scaleText);   // [khung tên người dùng – SheetBuilder.TitleBlockCustom.cs]
        var w = new DxfWriter(1);
        string L = Lay.KhungTen;
        var (pw, ph) = s.PaperSize();
        var (inner, tb) = Frame(s);
        w.Rect(L, 0, 0, pw, ph);
        if (s.ShowFrame)
            w.Polyline(L, new List<(double X, double Y)> { (inner.X1, inner.Y1), (inner.X2, inner.Y1), (inner.X2, inner.Y2), (inner.X1, inner.Y2) }, true, 0.5);
        if (!s.ShowTitleBlock) return w;
        var info = p?.TitleBlock ?? new TitleBlockInfo();
        bool logo = !string.IsNullOrEmpty(info.LogoData);
        if (!string.IsNullOrWhiteSpace(s.ScaleNote)) scaleText = s.ScaleNote.Trim();

        double tx1 = tb.X1, tx2 = tb.X2, y1 = tb.Y1, tw = tb.W, rh = TitleH / 6;
        w.Polyline(L, new List<(double X, double Y)> { (tx1, y1), (tx2, y1), (tx2, tb.Y2), (tx1, tb.Y2) }, true, 0.35);
        double cL = tx1 + 0.17 * tw, cR = tx2 - 0.28 * tw;
        double cM = cL + 0.27 * tw, cM2 = cM + 0.12 * tw;
        double topR = logo ? cR : tx2;
        w.Line(L, tx1, y1 + rh, cR, y1 + rh);
        w.Line(L, tx1, y1 + 2 * rh, tx2, y1 + 2 * rh);
        w.Line(L, tx1, y1 + 3 * rh, tx2, y1 + 3 * rh);
        w.Line(L, tx1, y1 + 4 * rh, logo ? cR : tx2, y1 + 4 * rh);
        w.Line(L, tx1, y1 + 5 * rh, topR, y1 + 5 * rh);
        w.Line(L, cL, y1, cL, tb.Y2);
        w.Line(L, cR, y1, cR, logo ? tb.Y2 : y1 + 4 * rh);
        w.Line(L, cM, y1, cM, y1 + 2 * rh);
        w.Line(L, cM2, y1, cM2, y1 + 2 * rh);

        string Eff(params string[] v) => v.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "";
        double pad = 1.5, lw = cL - tx1 - 2 * pad, vw = cR - cL - 2 * pad, rw = tx2 - cR - 2 * pad;
        double nw = cM - cL - 2 * pad, lw2 = cM2 - cM - 2 * pad, nw2 = cR - cM2 - 2 * pad;
        double Row(int i) => y1 + (i + 0.5) * rh;    // i = 0 dưới cùng
        var cu = CultureInfo.CurrentCulture;
        FitText(w, tx1 + pad, Row(5), lw, 2.0, "ĐƠN VỊ THIẾT KẾ");
        FitText(w, cL + pad, Row(5), topR - cL - 2 * pad, 3.0, Eff(info.Company).ToUpper(cu));
        FitText(w, tx1 + pad, Row(4), lw, 2.2, "CÔNG TRÌNH");
        FitText(w, cL + pad, Row(4), topR - cL - 2 * pad, 3.2, Eff(info.ProjectName, p?.Name).ToUpper(cu));
        FitText(w, tx1 + pad, Row(3), lw, 2.2, "ĐỊA ĐIỂM");
        FitText(w, cL + pad, Row(3), vw, 2.5, Eff(info.Location, p?.Location));
        FitText(w, tx1 + pad, Row(2), lw, 2.2, "TÊN BẢN VẼ");
        FitText(w, cL + pad, Row(2), vw, 3.0, (s.Name ?? "").ToUpper(cu));
        FitText(w, tx1 + pad, Row(1), lw, 2.2, "THIẾT KẾ");
        FitText(w, cL + pad, Row(1), nw, 2.5, Eff(s.Designer, info.Designer, p?.Designer));
        FitText(w, tx1 + pad, Row(0), lw, 2.2, "KIỂM TRA");
        FitText(w, cL + pad, Row(0), nw, 2.5, Eff(s.Checker, info.Checker));
        FitText(w, cM + pad, Row(1), lw2, 2.0, "PHÊ DUYỆT");
        FitText(w, cM2 + pad, Row(1), nw2, 2.5, Eff(s.Approver, info.Approver));
        FitText(w, cM + pad, Row(0), lw2, 2.0, "LẦN SỬA");
        FitText(w, cM2 + pad, Row(0), nw2, 2.5, Eff(s.Revision));

        FitText(w, cR + pad, Row(3), rw, 2.2, "TỶ LỆ: " + (scaleText ?? ""));
        FitText(w, cR + pad, Row(2), rw, 2.2, "NGÀY: " + (s.Date ?? ""));
        FitText(w, cR + pad, y1 + 1.65 * rh, rw, 1.8, "SỐ HIỆU BẢN VẼ");
        FitText(w, (cR + tx2) / 2, y1 + 0.75 * rh, rw, 4.5, s.Number ?? "", 1, 2);
        w.Text(L, inner.X2, inner.Y1 - 1.2, 1.8, "Lập bằng Kết cấu VXT", 0, 2, 3);
        return w;
    }

    /// <summary>Ô đặt logo công ty trong khung tên (mm giấy); rỗng nếu không có logo hoặc tờ không có khung tên.</summary>
    public static Rect2 TitleLogoRect(Project p, Sheet s)
    {
        if (HasCustomTitle(p)) return CustomTitleLogoRect(p, s);   // [khung tên người dùng – SheetBuilder.TitleBlockCustom.cs]
        if (s == null || !s.ShowTitleBlock || string.IsNullOrEmpty(p?.TitleBlock?.LogoData)) return Rect2.Empty;
        var (_, tb) = Frame(s);
        double rh = TitleH / 6, cR = tb.X2 - 0.28 * tb.W;
        return new Rect2(cR, tb.Y1 + 4 * rh, tb.X2, tb.Y2).Inflate(-1);
    }

    /// <summary>Chuỗi tỷ lệ cho khung tên từ các khung nhìn đã xếp.</summary>
    public static string ScaleText(IEnumerable<(SheetView V, ViewLayout L)> views)
    {
        var den = views.Where(t => t.V.Kind is not (SheetViewKind.Notes or SheetViewKind.RebarSchedule or SheetViewKind.View3D or SheetViewKind.RebarShop))
            .Select(t => Math.Round(t.L.EffectiveScale)).Where(x => x > 0).Distinct().ToList();
        if (den.Count == 0) return "";
        if (den.Count == 1) return "1/" + den[0].ToString("0", Inv);
        return "GHI TRÊN HÌNH";
    }

    // ==================================================================
    // Ghép tờ, xuất DXF
    // ==================================================================
    /// <summary>Ghép toàn bộ tờ (khung tên + các khung nhìn) thành một bản vẽ mm giấy tỷ lệ 1/1.</summary>
    public static DxfWriter Compose(Project p, AnalysisResults r, DesignSummary d, Sheet s)
    {
        // ---- tờ đã khoá (sửa trong CAD rồi nạp lại) → dùng bản đã nạp (DrawingOverlay.cs) ----
        if (!DrawingOverlay.Bypassed && !DrawingOverlay.FrozenIgnored && DrawingOverlay.FrozenOf(p, DrawingOverlay.SheetKey(s)) is { } fz)
        {
            var fw = DrawingOverlay.FrozenWriter(fz);
            if (fw != null && fw.EntityCount > 0) { DrawingOverlay.Current?.Warn(FrozenSheetWarning(p, r, d, s, fz)); return fw; }
        }
        // ---- hết ----
        var lays = new List<(SheetView, ViewLayout)>();
        foreach (var v in s.Views) lays.Add((v, Layout(Figures(p, r, d, v), v)));
        var outW = new DxfWriter(1);
        outW.Append(TitleBlock(p, s, ScaleText(lays)), 0, 0);
        // ---- dàn trang thủ công: cắt khung nhìn, viền, đối tượng chú thích (SheetBuilder.Manual.cs) ----
        foreach (var (v, lay) in lays) ComposeView(outW, v, lay);
        // kiểu kích thước theo tỷ lệ thực của từng khung nhìn (tỷ lệ tuỳ ý → tạo VXT_1-… tự động)
        foreach (var (v, lay) in lays)
            if (v.Kind is not (SheetViewKind.Notes or SheetViewKind.RebarSchedule or SheetViewKind.View3D or SheetViewKind.RebarShop) && lay.EffectiveScale > 0)
                outW.RegisterDimStyle(lay.EffectiveScale);
        ComposeItems(outW, s);
        // chỉnh sửa của chính tờ (khung tên, tên khung nhìn…); chỉnh sửa của từng hình đã áp trong Figures()
        if (!DrawingOverlay.Bypassed) DrawingOverlay.Apply(p, outW, DrawingOverlay.SheetKey(s), false);
        return outW;
    }

    public static void ExportDxf(Project p, AnalysisResults r, DesignSummary d, Sheet s, string path)
    {
        if (p == null) throw new ArgumentNullException(nameof(p));
        if (s == null) throw new ArgumentNullException(nameof(s));
        // tờ đã khoá: ghi nguyên văn file đã nạp
        if (!DrawingOverlay.Bypassed && !DrawingOverlay.FrozenIgnored && DrawingOverlay.FrozenOf(p, DrawingOverlay.SheetKey(s)) is { } fz && DrawingOverlay.RawBytes(fz).Length > 0)
        {
            DrawingOverlay.Current?.Warn(FrozenSheetWarning(p, r, d, s, fz));
            DrawingOverlay.WriteFrozen(fz, path);
            return;
        }
        Compose(p, r, d, s).Save(path);
    }

    /// <summary>Mã băm tờ tự sinh (đã áp chỉnh sửa, không dùng bản khoá) – so với lúc khoá.</summary>
    public static string GeneratedHash(Project p, AnalysisResults r, DesignSummary d, Sheet s)
    {
        try { using (DrawingOverlay.IgnoreFrozen()) return Compose(p, r, d, s).ContentHash(); }
        catch (Exception) { return null; }
    }

    /// <summary>Cảnh báo tờ đã khoá (kèm "mô hình thay đổi sau ngày …" nếu tờ tự sinh khác lúc khoá).</summary>
    public static string FrozenSheetWarning(Project p, AnalysisResults r, DesignSummary d, Sheet s, FrozenDrawing fz) =>
        DrawingOverlay.FrozenWarning(fz, GeneratedHash(p, r, d, s));

    /// <summary>Xuất các tờ (mặc định p.Sheets) theo tuỳ chọn: tách rời (mỗi tờ một file, như ExportAllDxf) hoặc gộp 1 file
    /// (các tờ đặt cạnh nhau theo trục X: tờ i ở X = Σ(W + khoảng hở), mm giấy, mỗi tờ có tên "số – tên" phía trên).</summary>
    public static DrawingExportResult ExportDxf(Project p, AnalysisResults r, DesignSummary d, string folder, IEnumerable<Sheet> sheets, DrawingExportOptions o)
    {
        if (p == null) throw new ArgumentNullException(nameof(p));
        var res = new DrawingExportResult();
        var list = (sheets ?? p.Sheets).ToList();
        using (DrawingOverlay.Collect(res.Report))
        {
            if (o == null || (o.Mode == DrawingExportMode.Split && string.IsNullOrWhiteSpace(o.FilePattern)))
                res.Files.AddRange(ExportAllDxf(p, r, d, folder, list));
            else
            {
                var items = list.Select(s => new DrawingItem
                {
                    Key = DrawingOverlay.SheetKey(s), FileName = FileName(s), Number = s.Number, Title = s.Name, Scale = 1,
                    W = Compose(p, r, d, s), Frozen = DrawingOverlay.Bypassed ? null : DrawingOverlay.FrozenOf(p, DrawingOverlay.SheetKey(s))
                }).ToList();
                DrawingExport.Write(items, folder, o, p.Name, res);
            }
        }
        foreach (var m in res.Report.Messages()) if (!res.Messages.Contains(m)) res.Messages.Add(m);
        return res;
    }

    /// <summary>Xuất mỗi tờ (mặc định: p.Sheets) thành một file DXF trong thư mục; trả về danh sách file.</summary>
    public static List<string> ExportAllDxf(Project p, AnalysisResults r, DesignSummary d, string folder, IEnumerable<Sheet> sheets = null)
    {
        Directory.CreateDirectory(folder);
        var paths = new List<string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in (sheets ?? p.Sheets).ToList())
        {
            string fn = FileName(s);
            for (int k = 2; !used.Add(fn); k++) fn = Path.GetFileNameWithoutExtension(FileName(s)) + "_" + k + ".dxf";
            string path = Path.Combine(folder, fn);
            ExportDxf(p, r, d, s, path);
            paths.Add(path);
        }
        return paths;
    }

    // ==================================================================
    // Tạo khung nhìn, bộ tờ mặc định
    // ==================================================================
    public static List<string> ParamOptions(Project p, DesignSummary d, SheetViewKind k)
    {
        var res = new List<string>();
        if (p == null) return res;
        var stories = p.Stories.OrderBy(x => x.Elevation).Select(x => x.Name).ToList();
        var grids = p.GridsX.Concat(p.GridsY).Select(g => g.Name).Distinct().ToList();
        switch (k)
        {
            case SheetViewKind.PlanStory: res.AddRange(stories); break;
            case SheetViewKind.SlabRebarPlan:
                res.AddRange(stories.Where(s => p.Slabs.Any(x => x.Story == s) || (d?.Slabs.Any(x => x.Story == s) ?? false)));
                if (res.Count == 0) res.AddRange(stories);
                break;
            case SheetViewKind.Elevation: res.AddRange(grids); break;
            case SheetViewKind.ForceDiagram:
                foreach (var q in ElevationDrawing.Quantities) foreach (var g in grids) res.Add($"{g};{q}");
                break;
            case SheetViewKind.FoundationDetail:
                res.Add("");
                if (d != null) res.AddRange(d.Footings.Select(f => f.Name).Where(n => !string.IsNullOrEmpty(n)).Distinct());
                break;
            case SheetViewKind.BeamDetail:
                if (d != null)
                {
                    res.AddRange(d.Beams.Select(b => BeamStory(p, b)).Distinct().OrderBy(s => { int i = stories.IndexOf(s); return i < 0 ? int.MaxValue : i; }));
                    res.AddRange(d.Beams.Select(b => b.Name).Distinct());
                }
                break;
            case SheetViewKind.ColumnDetail:
                res.Add("");
                if (d != null) res.AddRange(d.Columns.Select(c => c.Name).Distinct());
                break;
            case SheetViewKind.RebarShop:   // gia công thép theo loại cấu kiện
                res.Add("");
                res.AddRange(RebarShopDrawing.Categories);
                break;
            case SheetViewKind.StairDetail:   // cầu thang theo tên
                res.Add("");
                if (d?.Stairs != null) res.AddRange(d.Stairs.Select(x => x.Name).Distinct());
                break;
            default: res.Add(""); break;
        }
        return res;
    }

    private static double Ceil5(double x) => Math.Ceiling(x / Snap - 1e-9) * Snap;
    private static double Floor5(double x) => Math.Floor(x / Snap + 1e-9) * Snap;

    /// <summary>Khung nhìn mới cho tờ s: đúng tỷ lệ gốc nếu vừa vùng vẽ, ngược lại tự co vừa ~70% vùng vẽ.</summary>
    public static SheetView NewView(Project p, AnalysisResults r, DesignSummary d, Sheet s, SheetViewKind kind, string param)
    {
        var v = new SheetView { Kind = kind, Param = param ?? "" };
        var figs = Figures(p, r, d, v);
        var area = DrawingArea(s);
        double refS = NativeScale(figs);
        var (w, h) = NativeSize(figs, area.W - 2 * ViewPad);
        double W = Ceil5(w + 2 * ViewPad), H = Ceil5(h + 2 * ViewPad);
        if (W <= area.W && H <= area.H - TitleH)
        {
            v.Scale = refS; v.W = W; v.H = H;
        }
        else
        {
            v.Scale = 0;
            double aspect = w > 0 && h > 0 ? w / h : 1.4;
            double mw = 0.75 * area.W, mh = 0.7 * area.H;
            if (mw / aspect > mh) mw = mh * aspect; else mh = mw / aspect;
            v.W = Math.Max(40, Floor5(mw)); v.H = Math.Max(30, Floor5(mh));
        }
        int n = s.Views.Count % 6;
        v.X = Ceil5(area.X1 + n * 10);
        v.Y = Floor5(area.Y2 - n * 10) - v.H;
        if (v.Y < area.Y1) v.Y = Ceil5(area.Y1);
        return v;
    }

    private static List<(SheetViewKind, string)> L(params (SheetViewKind, string)[] a) => a.ToList();

    private sealed class Item
    {
        public SheetView V;
        public double W, H;
        public bool Fit;
    }

    /// <summary>Xếp các khung nhìn vào các tờ cùng khổ (xếp hàng trái→phải, trên→dưới, tránh khung tên).</summary>
    private static List<List<SheetView>> Pack(List<Item> items, Sheet proto)
    {
        var area = DrawingArea(proto);
        var (_, tb) = Frame(proto);
        var tbObs = new Rect2(tb.X1 - Gap, tb.Y1 - Gap, tb.X2 + Gap, tb.Y2 + Gap);
        var sheets = new List<List<SheetView>>();
        var cur = new List<SheetView>();
        double cx = area.X1, rowTop = area.Y2, rowH = 0;
        foreach (var it in items)
        {
            double w = it.W, h = it.H;
            int guard = 0;
            while (guard++ < 200)
            {
                double x = Ceil5(cx), top = Floor5(rowTop);
                var rc = new Rect2(x, top - h, x + w, top);
                bool tooWide = x + w > area.X2 + 1e-6;
                bool tooLow = top - h < area.Y1 - 1e-6;
                bool hitTb = rc.Intersects(tbObs);
                if (!tooWide && !tooLow && !hitTb)
                {
                    it.V.X = x; it.V.Y = top - h; it.V.W = w; it.V.H = h;
                    cur.Add(it.V);
                    cx = x + w + Gap; rowH = Math.Max(rowH, h);
                    break;
                }
                if ((tooWide || hitTb) && cx > area.X1 + 1e-6 && !tooLow)
                {
                    rowTop = top - rowH - Gap; cx = area.X1; rowH = 0;
                    continue;
                }
                if (cur.Count == 0)
                {
                    // tờ trống vẫn không vừa: đặt vừa vùng vẽ phía trên khung tên
                    it.V.Scale = 0;
                    it.V.X = Ceil5(area.X1); it.V.W = Floor5(area.X2 - it.V.X);
                    double yb = Ceil5(tbObs.Y2);
                    it.V.H = Floor5(area.Y2 - yb); it.V.Y = yb;
                    cur.Add(it.V);
                    sheets.Add(cur); cur = new List<SheetView>();
                    cx = area.X1; rowTop = area.Y2; rowH = 0;
                    break;
                }
                sheets.Add(cur); cur = new List<SheetView>();
                cx = area.X1; rowTop = area.Y2; rowH = 0;
            }
        }
        if (cur.Count > 0) sheets.Add(cur);
        return sheets;
    }

    private static List<Item> MakeItems(Project p, AnalysisResults r, DesignSummary d, Sheet proto, IEnumerable<(SheetViewKind K, string P)> defs, out bool allFit)
    {
        var area = DrawingArea(proto);
        var res = new List<Item>();
        allFit = true;
        foreach (var (k, par) in defs)
        {
            var v = new SheetView { Kind = k, Param = par ?? "" };
            var figs = Figures(p, r, d, v);
            v.Scale = k == SheetViewKind.RebarSchedule || k == SheetViewKind.Notes || k == SheetViewKind.RebarShop ? 1 : NativeScale(figs);
            var (w, h) = NativeSize(figs, area.W - 2 * ViewPad);
            double W = Ceil5(w + 2 * ViewPad), H = Ceil5(h + 2 * ViewPad);
            bool fit = W <= area.W + 1e-6 && (H <= area.H - TitleH - Gap + 1e-6 || (W <= area.W - TitleW - Gap && H <= area.H));
            if (!fit) { allFit = false; v.Scale = 0; }
            res.Add(new Item { V = v, W = Math.Min(W, area.W), H = Math.Min(H, area.H), Fit = fit });
        }
        return res;
    }

    /// <summary>Bộ tờ mặc định giống bộ KC-01…KC-06: ghi chú, mặt bằng kết cấu từng tầng, mặt đứng khung,
    /// (biểu đồ nội lực nếu có r), móng, cột, dầm theo tầng, thép sàn, thống kê thép. Khổ giấy nhỏ nhất (A3→A1) vừa hình.</summary>
    public static List<Sheet> DefaultSet(Project p, DesignSummary d, AnalysisResults r = null)
    {
        var sheets = new List<Sheet>();
        if (p == null) return sheets;
        p.InvalidateIndex();
        string date = DateTime.Today.ToString("dd/MM/yyyy", Inv);
        var groups = new List<(string Name, List<(SheetViewKind, string)> Defs)>();
        var stories = p.Stories.OrderBy(x => x.Elevation).ToList();

        groups.Add(("GHI CHÚ CHUNG", L((SheetViewKind.Notes, ""))));
        foreach (var st in stories)
        {
            bool hasAny = p.Frames.Any(f => f.Story == st.Name) || p.Slabs.Any(x => x.Story == st.Name)
                          || p.Frames.Any(f => f.IsVerticalKind && p.Nodes.Any(n => n.Id == f.NodeJ && Math.Abs(n.Z - st.Elevation) < 1e-3));
            if (!hasAny) continue;
            groups.Add(($"MẶT BẰNG KẾT CẤU TẦNG {st.Name}", L((SheetViewKind.PlanStory, st.Name))));
        }
        var gx = p.GridsX.ToList();
        if (gx.Count > 0 && p.Frames.Count > 0)
        {
            groups.Add(("MẶT ĐỨNG KHUNG", gx.Take(6).Select(g => (SheetViewKind.Elevation, g.Name)).ToList()));
            if (r != null) groups.Add(("BIỂU ĐỒ MÔ MEN KHUNG", gx.Take(6).Select(g => (SheetViewKind.ForceDiagram, g.Name + ";M3")).ToList()));
        }
        if (d != null)
        {
            if (FootingDrawing.BuildAll(p, d).Count > 0)
                groups.Add(("MẶT BẰNG VÀ CHI TIẾT MÓNG", L((SheetViewKind.FoundationPlan, ""), (SheetViewKind.FoundationDetail, ""))));
            if (d.Columns.Count > 0)
                groups.Add(("CHI TIẾT CỘT", d.Columns.Select(c => (SheetViewKind.ColumnDetail, c.Name)).ToList()));
            var order = stories.Select(x => x.Name).ToList();
            foreach (var g in d.Beams.GroupBy(b => BeamStory(p, b))
                         .OrderBy(gr => { int i = order.IndexOf(gr.Key); return i < 0 ? int.MaxValue : i; }).ThenBy(gr => gr.Key, StringComparer.Ordinal))
                groups.Add(($"CHI TIẾT DẦM TẦNG {g.Key}", g.Select(b => (SheetViewKind.BeamDetail, b.Name)).Distinct().ToList()));
        }
        foreach (var st in stories)
            if (p.Slabs.Any(x => x.Story == st.Name) || (d?.Slabs.Any(x => x.Story == st.Name) ?? false))
                groups.Add(($"MẶT BẰNG THÉP SÀN TẦNG {st.Name}", L((SheetViewKind.SlabRebarPlan, st.Name))));
        // ---- cầu thang (StairDrawing.cs) ----
        if (d?.Stairs != null)
            foreach (var sd in d.Stairs)
                groups.Add(($"CHI TIẾT CẦU THANG {sd.Name}", L((SheetViewKind.StairDetail, sd.Name))));
        // ---- hết cầu thang ----
        if (d != null && d.All.Any(m => m.Bars.Count > 0))
            groups.Add(("THỐNG KÊ CỐT THÉP", L((SheetViewKind.RebarSchedule, ""))));

        int no = 0;
        foreach (var (name, defs) in groups)
        {
            List<List<SheetView>> packed = null;
            string paper = "A1";
            foreach (var pp in new[] { "A3", "A2", "A1" })
            {
                var proto = new Sheet { Paper = pp, Landscape = true };
                var items = MakeItems(p, r, d, proto, defs, out bool allFit);
                if (!allFit && pp != "A1") continue;
                var pk = Pack(items, proto);
                paper = pp;
                packed = pk;
                if (pk.Count <= 2 || pp == "A1") break;
            }
            if (packed == null) continue;
            for (int i = 0; i < packed.Count; i++)
            {
                no++;
                sheets.Add(new Sheet
                {
                    Number = "KC-" + no.ToString("00", Inv),
                    Name = packed.Count > 1 ? $"{name} ({i + 1}/{packed.Count})" : name,
                    Paper = paper, Landscape = true,
                    Designer = p.Designer ?? "", Checker = "", Date = date,
                    Views = packed[i]
                });
            }
        }
        return sheets;
    }

    // ==================================================================
    // Sao chép
    // ==================================================================
    public static Sheet Clone(Sheet s) => JsonSerializer.Deserialize<Sheet>(JsonSerializer.Serialize(s));
    public static SheetView Clone(SheetView v) => JsonSerializer.Deserialize<SheetView>(JsonSerializer.Serialize(v));
}

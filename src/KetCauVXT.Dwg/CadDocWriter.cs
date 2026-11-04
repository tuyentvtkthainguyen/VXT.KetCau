using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CSMath;
using KetCauVXT.Drawing;
using KetCauVXT.Model;
using AcHatch = ACadSharp.Entities.Hatch;
using AcIO = ACadSharp.IO;
using VxtWriter = KetCauVXT.Drawing.DxfWriter;

namespace KetCauVXT.Dwg;

/// <summary>Ghi bản vẽ của chương trình (bộ ghi <see cref="KetCauVXT.Drawing.DxfWriter"/>) ra DXF 2018 / DWG 2018 (AC1032) bằng ACadSharp:
/// <list type="bullet">
/// <item>bảng LAYER theo Chuẩn layer CAD (tên/màu/kiểu nét/nét in/cờ in – cùng quy tắc gộp với DXF R12), LTYPE (DASHED, CENTER, HIDDEN, DASHDOT),
/// STYLE "Standard" font arial.ttf / times.ttf, DIMSTYLE VXT_1-25… theo Thiết lập bản vẽ, $INSUNITS = mm, $LTSCALE = 0,3 × tỷ lệ;</item>
/// <item>LINE, LWPOLYLINE (bề rộng nét), CIRCLE, ARC, SOLID, TEXT (Unicode tiếng Việt ghi thẳng UTF-8 – không cần mã \U+), màu ACI riêng;</item>
/// <item>HATCH thật: SOLID hoặc mẫu có định nghĩa nét (bảng <see cref="HatchPatterns"/>), góc + tỷ lệ mẫu, vòng biên ngoài + lỗ.</item>
/// </list>
/// Đăng ký vào lõi: <c>CadDocWriter.Register()</c> (= <c>CadFileWriters.Writer = CadDocWriter.Write</c>).</summary>
public static class CadDocWriter
{
    /// <summary>Đăng ký bộ ghi này vào lõi (CadFileWriters.Writer). Gọi một lần khi khởi động.</summary>
    public static void Register() => CadFileWriters.Writer = Write;

    /// <summary>Ghi ra file: Dwg2018 → DWG AC1032, còn lại → DXF ASCII AC1032 (UTF-8).</summary>
    public static void Write(VxtWriter w, string path, CadOutputFormat fmt)
    {
        if (w == null) throw new ArgumentNullException(nameof(w));
        var doc = Build(w);
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        try
        {
            if (fmt == CadOutputFormat.Dwg2018) AcIO.DwgWriter.Write(path, doc);
            else AcIO.DxfWriter.Write(path, doc, false);
        }
        catch (Exception ex)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { /* bỏ qua */ }
            throw new InvalidDataException($"Không ghi được {(fmt == CadOutputFormat.Dwg2018 ? "DWG" : "DXF 2018")} '{Path.GetFileName(path)}': {ex.Message}", ex);
        }
    }

    private static readonly short[] LineWeights = { 0, 5, 9, 13, 15, 18, 20, 25, 30, 35, 40, 50, 53, 60, 70, 80, 90, 100, 106, 120, 140, 158, 200, 211 };

    /// <summary>Nét in mm → giá trị chuẩn gần nhất (0 = mặc định).</summary>
    private static LineWeightType Lw(double mm)
    {
        if (double.IsNaN(mm) || mm <= 0) return LineWeightType.Default;
        int v = (int)Math.Round(mm * 100);
        short best = LineWeights[0];
        foreach (short s in LineWeights) if (Math.Abs(s - v) < Math.Abs(best - v)) best = s;
        return (LineWeightType)best;
    }

    private static double Rad(double deg) => deg * Math.PI / 180.0;

    /// <summary>Dựng CadDocument từ bộ ghi (chưa ghi file) – dùng cho kiểm thử và Write.</summary>
    public static CadDocument Build(VxtWriter w, ACadVersion version = ACadVersion.AC1032)
    {
        if (w == null) throw new ArgumentNullException(nameof(w));
        var doc = new CadDocument(version);
        doc.Header.InsUnits = UnitsType.Millimeters;
        doc.Header.LineTypeScale = Math.Max(1e-6, 0.3 * w.Scale);

        // ---- kiểu nét ----
        foreach (var (name, desc, elems) in VxtWriter.LinetypeTable)
        {
            if (string.Equals(name, "CONTINUOUS", StringComparison.OrdinalIgnoreCase) || doc.LineTypes.Contains(name)) continue;
            var lt = new LineType(name) { Description = desc };
            foreach (double el in elems) lt.AddSegment(new LineType.Segment { Length = el });
            doc.LineTypes.Add(lt);
        }
        LineType Lt(string n)
        {
            string s = CadStandard.NormalizeLinetype(n);
            if (s == "CONTINUOUS") return doc.LineTypes.Continuous;
            return doc.LineTypes.TryGetValue(s, out var t) ? t : doc.LineTypes.Continuous;
        }

        // ---- layer ----
        var layers = new Dictionary<string, Layer>(StringComparer.OrdinalIgnoreCase);
        Layer GetLayer(string outName)
        {
            string n = CadStandard.SafeLayerName(outName);
            if (layers.TryGetValue(n, out var l)) return l;
            if (!doc.Layers.TryGetValue(n, out l)) { l = new Layer(n); doc.Layers.Add(l); }
            layers[n] = l;
            return l;
        }
        foreach (var (name, color, ltype, lw, plot) in w.OutputLayerTable())
        {
            var l = GetLayer(name);
            l.Color = new Color((short)(color is >= 1 and <= 255 ? color : 7));
            l.LineType = Lt(ltype);
            l.LineWeight = Lw(lw);
            l.PlotFlag = plot;
        }

        // ---- kiểu chữ (font theo Chuẩn layer CAD) ----
        var std = w.EffectiveStandard;
        if (!doc.TextStyles.TryGetValue(TextStyle.DefaultName, out var ts)) { ts = new TextStyle(TextStyle.DefaultName); doc.TextStyles.Add(ts); }
        ts.Filename = CadStandard.FontFile(std?.FontName ?? CadStandard.FontArial);
        ts.Width = w.WidthFactor > 0 ? w.WidthFactor : 1.0;

        // ---- kiểu kích thước VXT_1-25… (kích thước vẽ bằng nét; bảng để người dùng đo/ghi tiếp trong CAD) ----
        foreach (var d in w.DimStyleTable())
        {
            try
            {
                if (string.IsNullOrEmpty(d.Name) || doc.DimensionStyles.Contains(d.Name)) continue;
                bool tick = d.Arrow == DimArrow.Tick;
                var ds = new DimensionStyle(d.Name)
                {
                    ScaleFactor = d.Scale > 0 ? d.Scale : 1,
                    TextHeight = d.TextHeight > 0 ? d.TextHeight : 2.5,
                    ArrowSize = Math.Max(0, d.TickSize),
                    TickSize = tick ? Math.Max(0, d.TickSize / 2) : 0,
                    ExtensionLineOffset = Math.Max(0, d.ExtOffset),
                    ExtensionLineExtension = Math.Max(0, d.ExtBeyond),
                    DimensionLineIncrement = Math.Max(0, d.BaselineSpacing),
                    DimensionLineExtension = tick ? Math.Max(0, d.LineExtend) : 0,
                    DimensionLineGap = Math.Max(0, d.TextGap),
                    LinearScaleFactor = d.LinearFactor > 0 ? d.LinearFactor : 1,
                    TextVerticalAlignment = DimensionTextVerticalAlignment.Above,
                    TextInsideHorizontal = false,
                    TextOutsideHorizontal = false
                };
                doc.DimensionStyles.Add(ds);
            }
            catch (Exception) { /* kiểu kích thước lỗi: bỏ qua, bản vẽ vẫn ghi */ }
        }

        // ---- thực thể ----
        foreach (var pr in w.GetPrimitives())
        {
            Entity e = ToEntity(pr);
            if (e == null) continue;
            e.Layer = GetLayer(w.OutputLayerOf(pr.Layer));
            if (pr.Aci is > 0 and < 256) e.Color = new Color((short)pr.Aci);
            if (!string.IsNullOrEmpty(pr.LType) && pr.Kind != PrimKind.Hatch && pr.Kind != PrimKind.Text && pr.Kind != PrimKind.Solid)
            {
                string s = CadStandard.NormalizeLinetype(pr.LType);
                if (s != "CONTINUOUS" || string.Equals(pr.LType, "CONTINUOUS", StringComparison.OrdinalIgnoreCase)) e.LineType = Lt(s);
            }
            doc.Entities.Add(e);
        }
        return doc;
    }

    private static XYZ P3(double x, double y) => new(x, y, 0);

    private static Entity ToEntity(DrawPrim pr)
    {
        var a = pr.Pts ?? Array.Empty<double>();
        switch (pr.Kind)
        {
            case PrimKind.Line:
                if (a.Length < 4) return null;
                return new Line { StartPoint = P3(a[0], a[1]), EndPoint = P3(a[2], a[3]) };
            case PrimKind.Polyline:
            {
                if (a.Length < 4) return null;
                var pl = new LwPolyline { IsClosed = pr.Closed };
                if (pr.Width > 0) pl.ConstantWidth = pr.Width;
                for (int i = 0; i + 1 < a.Length; i += 2) pl.Vertices.Add(new LwPolyline.Vertex(new XY(a[i], a[i + 1])));
                return pl;
            }
            case PrimKind.Circle:
                if (a.Length < 2 || pr.R <= 0) return null;
                return new Circle { Center = P3(a[0], a[1]), Radius = pr.R };
            case PrimKind.Arc:
                if (a.Length < 2 || pr.R <= 0) return null;
                return new Arc { Center = P3(a[0], a[1]), Radius = pr.R, StartAngle = Rad(pr.A1), EndAngle = Rad(pr.A2) };
            case PrimKind.Solid:
                if (a.Length < 8) return null;
                // DrawPrim: 4 đỉnh theo chu vi (1, 2, 4, 3) → SOLID mã 10, 11, 12, 13 = đỉnh 1, 2, 3, 4
                return new Solid { FirstCorner = P3(a[0], a[1]), SecondCorner = P3(a[2], a[3]), ThirdCorner = P3(a[6], a[7]), FourthCorner = P3(a[4], a[5]) };
            case PrimKind.Text:
            {
                if (a.Length < 2 || string.IsNullOrEmpty(pr.Text) || !(pr.H > 0)) return null;
                var p = P3(a[0], a[1]);
                var t = new TextEntity
                {
                    Value = pr.Text, InsertPoint = p, AlignmentPoint = p, Height = pr.H,
                    Rotation = Rad(pr.Rot), WidthFactor = pr.WF > 0 ? pr.WF : 1.0
                };
                if (pr.HA is >= 0 and <= 2) t.HorizontalAlignment = (TextHorizontalAlignment)pr.HA;
                if (pr.VA is >= 0 and <= 3) t.VerticalAlignment = (TextVerticalAlignmentType)pr.VA;
                return t;
            }
            case PrimKind.Hatch:
                return ToHatch(VxtWriter.HatchOf(pr));
        }
        return null;
    }

    /// <summary>HATCH thật: mẫu từ bảng HatchPatterns (định nghĩa nét theo đơn vị mẫu, rồi xoay/phóng theo góc + tỷ lệ của miền tô).</summary>
    private static AcHatch ToHatch(HatchData hd)
    {
        if (hd == null || hd.Loops.Count == 0) return null;
        var h = new AcHatch();
        var def = HatchPatterns.Get(hd.Pattern);
        if (def.IsSolid)
        {
            h.IsSolid = true;
            h.Pattern = HatchPattern.Solid;
            h.PatternType = (HatchPatternType)1;   // DXF mã 76 = 1: mẫu có sẵn (predefined)
            h.PatternScale = 1;
        }
        else
        {
            var pat = new HatchPattern(def.Name) { Description = def.Description };
            foreach (var ln in def.Lines)
            {
                var l = new HatchPattern.Line { Angle = Rad(ln.Angle), BasePoint = new XY(ln.X, ln.Y), Offset = new XY(ln.Dx, ln.Dy) };
                if (ln.Dash != null) l.DashLengths.AddRange(ln.Dash);
                pat.Lines.Add(l);
            }
            h.IsSolid = false;
            h.Pattern = pat;
            h.PatternType = (HatchPatternType)(def.Predefined ? 1 : 2);   // 1 = predefined, 2 = custom
            // thứ tự: gán mẫu trước, rồi góc (xoay nét mẫu), rồi tỷ lệ (phóng nét mẫu) – ACadSharp cập nhật định nghĩa nét khi gán
            if (Math.Abs(hd.Angle) > 1e-12) h.PatternAngle = Rad(hd.Angle);
            h.PatternScale = hd.PatScale > 0 ? hd.PatScale : 1;
        }
        h.Style = HatchStyleType.Normal;
        for (int i = 0; i < hd.Loops.Count; i++)
        {
            var bp = new AcHatch.BoundaryPath { Flags = i == 0 ? BoundaryPathFlags.External : BoundaryPathFlags.Default };
            bp.Edges.Add(new AcHatch.BoundaryPath.Polyline(hd.Loops[i].Select(p => new XYZ(p.X, p.Y, 0)), true));
            h.Paths.Add(bp);
        }
        return h;
    }
}

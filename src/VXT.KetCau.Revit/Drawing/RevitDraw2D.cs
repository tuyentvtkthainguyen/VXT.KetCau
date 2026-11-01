using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using VXT.KetCau.Core.Drawing;
using VXT.KetCau.Revit.Util;

namespace VXT.KetCau.Revit.Drawing
{
    /// <summary>
    /// Bề mặt vẽ 2D trên một view Revit (section/drafting): nét chi tiết (Detail Lines) theo kiểu nét VXT_*,
    /// chữ TextNote font Arial, kích thước thật (Dimension) bám vào nét chi tiết.
    /// Toạ độ vào: mm trong hệ (u, v) của view tính từ gốc <c>origin</c>.
    /// </summary>
    public class RevitDraw2D : IDraw2D
    {
        readonly Document doc;
        readonly View view;
        readonly XYZ origin, right, up;
        readonly double shortTol;
        readonly Dictionary<string, GraphicsStyle> styles = new Dictionary<string, GraphicsStyle>();
        readonly Dictionary<int, ElementId> textTypes = new Dictionary<int, ElementId>();
        ElementId solidFill;
        public bool RealDimensions { get; set; } = true;

        static readonly Dictionary<string, (byte r, byte g, byte b, int w)> LayerStyle = new Dictionary<string, (byte, byte, byte, int)>
        {
            ["BE_TONG"] = (0, 0, 0, 3), ["THEP"] = (255, 0, 0, 5), ["DAI"] = (0, 160, 0, 2), ["KICH_THUOC"] = (0, 128, 255, 1),
            ["CHU"] = (0, 0, 0, 1), ["KY_HIEU"] = (200, 0, 200, 1), ["TRUC"] = (128, 128, 128, 1), ["BANG"] = (0, 0, 0, 1),
        };

        public RevitDraw2D(Document d, View v, XYZ originModel, XYZ rightDir, XYZ upDir)
        {
            doc = d; view = v; right = rightDir.Normalize(); up = upDir.Normalize();
            // chiếu gốc lên mặt phẳng view để mọi nét nằm đúng mặt phẳng
            var n = v.ViewDirection;
            double dist = (originModel - v.Origin).DotProduct(n);
            origin = originModel - n * dist;
            shortTol = d.Application.ShortCurveTolerance * 1.5;
        }

        /// <summary>View drafting: gốc (0,0,0), trục X/Y.</summary>
        public static RevitDraw2D ForDrafting(Document d, View v) => new RevitDraw2D(d, v, XYZ.Zero, XYZ.BasisX, XYZ.BasisY);

        XYZ P(double x, double y) => origin + right * Rv.Ft(x) + up * Rv.Ft(y);

        GraphicsStyle Style(string layer)
        {
            if (styles.TryGetValue(layer, out var gs)) return gs;
            var cats = doc.Settings.Categories;
            var lines = cats.get_Item(BuiltInCategory.OST_Lines);
            string name = "VXT_" + layer;
            Category sub = null;
            foreach (Category c in lines.SubCategories) if (c.Name == name) { sub = c; break; }
            if (sub == null)
            {
                sub = cats.NewSubcategory(lines, name);
                if (LayerStyle.TryGetValue(layer, out var st)) { sub.LineColor = new Color(st.r, st.g, st.b); sub.SetLineWeight(st.w, GraphicsStyleType.Projection); }
            }
            gs = sub.GetGraphicsStyle(GraphicsStyleType.Projection);
            styles[layer] = gs;
            return gs;
        }

        public void Line(string layer, double x1, double y1, double x2, double y2)
        {
            var a = P(x1, y1); var b = P(x2, y2);
            if (a.DistanceTo(b) < shortTol) return;
            var dc = doc.Create.NewDetailCurve(view, Autodesk.Revit.DB.Line.CreateBound(a, b));
            dc.LineStyle = Style(layer);
        }

        DetailCurve LineRet(string layer, XYZ a, XYZ b)
        {
            var dc = doc.Create.NewDetailCurve(view, Autodesk.Revit.DB.Line.CreateBound(a, b));
            dc.LineStyle = Style(layer);
            return dc;
        }

        public void Circle(string layer, double x, double y, double r)
        {
            var c = P(x, y); double rf = Rv.Ft(r);
            if (rf < shortTol) return;
            foreach (var (s, e) in new[] { (0.0, Math.PI), (Math.PI, 2 * Math.PI) })
            {
                var arc = Arc.Create(c, rf, s, e, right, up);
                var dc = doc.Create.NewDetailCurve(view, arc);
                dc.LineStyle = Style(layer);
            }
        }

        public void Dot(string layer, double x, double y, double r)
        {
            var c = P(x, y); double rf = Rv.Ft(r);
            if (rf < shortTol) { return; }
            if (solidFill == null)
                solidFill = Rv.All<FilledRegionType>(doc).Where(t => doc.GetElement(t.ForegroundPatternId) is FillPatternElement fp && fp.GetFillPattern().IsSolidFill)
                                                      .Select(t => t.Id).FirstOrDefault() ?? ElementId.InvalidElementId;
            if (solidFill == ElementId.InvalidElementId) { Circle(layer, x, y, r); return; }
            try
            {
                var loop = new CurveLoop();
                loop.Append(Arc.Create(c, rf, 0, Math.PI, right, up));
                loop.Append(Arc.Create(c, rf, Math.PI, 2 * Math.PI, right, up));
                FilledRegion.Create(doc, solidFill, view.Id, new List<CurveLoop> { loop });
            }
            catch { Circle(layer, x, y, r); }
        }

        public void Text(string layer, double x, double y, double height, string text, int h, int v, double rotDeg)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            // chiều cao chữ mô hình → chiều cao chữ in (mm) theo tỉ lệ view
            double paper = Math.Round(height / Math.Max(1, view.Scale) * 10) / 10.0;
            var opt = new TextNoteOptions(TextType(paper))
            {
                HorizontalAlignment = h == 1 ? HorizontalTextAlignment.Center : (h == 2 ? HorizontalTextAlignment.Right : HorizontalTextAlignment.Left),
                VerticalAlignment = v == 2 ? VerticalTextAlignment.Middle : (v == 3 ? VerticalTextAlignment.Top : VerticalTextAlignment.Bottom),
                Rotation = rotDeg * Math.PI / 180,
            };
            // TextNote căn theo khung chữ: dịch nhẹ để chữ ngồi trên vai nhãn như mẫu
            TextNote.Create(doc, view.Id, P(x, y), text, opt);
        }

        ElementId TextType(double paperMm)
        {
            int key = (int)Math.Round(paperMm * 10);
            if (textTypes.TryGetValue(key, out var id)) return id;
            string name = $"VXT Arial {paperMm:0.0}mm";
            var tt = Rv.All<TextNoteType>(doc).FirstOrDefault(t => t.Name == name);
            if (tt == null)
            {
                var baseT = Rv.All<TextNoteType>(doc).First();
                tt = (TextNoteType)baseT.Duplicate(name);
                tt.get_Parameter(BuiltInParameter.TEXT_SIZE).Set(Rv.Ft(paperMm));
                tt.get_Parameter(BuiltInParameter.TEXT_FONT).Set("Arial");
                tt.get_Parameter(BuiltInParameter.TEXT_WIDTH_SCALE).Set(0.8);
                tt.get_Parameter(BuiltInParameter.TEXT_BACKGROUND).Set(1); // trong suốt
                tt.get_Parameter(BuiltInParameter.LEADER_OFFSET_SHEET)?.Set(0.0);
            }
            textTypes[key] = tt.Id;
            return tt.Id;
        }

        /// <summary>Kích thước Revit thật: tạo 2 nét gióng ngắn làm tham chiếu rồi đặt Dimension.</summary>
        public bool Dimension(double x0, double y0, double x1, double y1, double lineY, bool vertical, string overrideText)
        {
            if (!RealDimensions) return false;
            try
            {
                double t = 0.6 * 2.5 * view.Scale; // độ dài nét gióng (mm mô hình)
                XYZ a0, a1, b0, b1, l0, l1;
                if (!vertical)
                {
                    a0 = P(x0, lineY - t); a1 = P(x0, lineY + t / 2); b0 = P(x1, lineY - t); b1 = P(x1, lineY + t / 2);
                    l0 = P(x0, lineY); l1 = P(x1, lineY);
                }
                else
                {
                    a0 = P(lineY - t, y0); a1 = P(lineY + t / 2, y0); b0 = P(lineY - t, y1); b1 = P(lineY + t / 2, y1);
                    l0 = P(lineY, y0); l1 = P(lineY, y1);
                }
                if (l0.DistanceTo(l1) < shortTol * 2) return true;
                var da = LineRet("KICH_THUOC", a0, a1); var db = LineRet("KICH_THUOC", b0, b1);
                var refs = new ReferenceArray();
                refs.Append(da.GeometryCurve.Reference); refs.Append(db.GeometryCurve.Reference);
                var dim = doc.Create.NewDimension(view, Autodesk.Revit.DB.Line.CreateBound(l0, l1), refs);
                if (dim != null && !string.IsNullOrEmpty(overrideText))
                {
                    double real = Math.Round(Rv.Mm(l0.DistanceTo(l1)));
                    if (overrideText != real.ToString("0")) dim.ValueOverride = overrideText;
                }
                return dim != null;
            }
            catch { return false; }
        }
    }
}

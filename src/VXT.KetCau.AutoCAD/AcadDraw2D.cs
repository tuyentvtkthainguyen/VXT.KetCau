using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using VXT.KetCau.Core.Drawing;
using AcHatch = Autodesk.AutoCAD.DatabaseServices.Hatch;

namespace VXT.KetCau.AutoCAD
{
    /// <summary>
    /// Vẽ shop drawing trực tiếp vào Model Space: nét, chữ (font Arial, tiếng Việt), chấm đặc,
    /// kích thước thật (RotatedDimension, kiểu dim "VXT" gạch xiên). Đơn vị bản vẽ mm.
    /// </summary>
    public class AcadDraw2D : IDraw2DEx
    {
        readonly Database db;
        readonly Transaction tr;
        readonly BlockTableRecord ms;
        readonly Vector3d off;
        readonly ObjectId textStyle;
        ObjectId dimStyle;
        double scale = 50;
        readonly Dictionary<double, ObjectId> dimStyles = new Dictionary<double, ObjectId>();
        readonly HashSet<string> layers = new HashSet<string>();
        public int Count { get; private set; }

        static readonly Dictionary<string, short> LayerColors = new Dictionary<string, short>
        {
            ["BE_TONG"] = 7, ["THEP"] = 1, ["DAI"] = 3, ["KICH_THUOC"] = 4, ["CHU"] = 2, ["KY_HIEU"] = 6, ["TRUC"] = 8, ["BANG"] = 7,
            ["TUONG"] = 7, ["COT"] = 7, ["CUA"] = 3, ["HATCH"] = 8, ["THANG"] = 5, ["PHONG"] = 2, ["KHUNG"] = 7, ["GIANG"] = 7, ["LATNEN"] = 9, ["THIETBI"] = 5, ["VXT_NHANDIEN"] = 6,
        };

        /// <summary>Bề dày nét in theo lớp.</summary>
        static readonly Dictionary<string, LineWeight> LayerWeights = new Dictionary<string, LineWeight>
        {
            ["TUONG"] = LineWeight.LineWeight040, ["COT"] = LineWeight.LineWeight040, ["BE_TONG"] = LineWeight.LineWeight035, ["THEP"] = LineWeight.LineWeight035,
            ["KHUNG"] = LineWeight.LineWeight035, ["CUA"] = LineWeight.LineWeight018, ["DAI"] = LineWeight.LineWeight018, ["THANG"] = LineWeight.LineWeight018,
            ["KICH_THUOC"] = LineWeight.LineWeight013, ["CHU"] = LineWeight.LineWeight018, ["KY_HIEU"] = LineWeight.LineWeight013, ["TRUC"] = LineWeight.LineWeight013,
            ["BANG"] = LineWeight.LineWeight018, ["HATCH"] = LineWeight.LineWeight009, ["LATNEN"] = LineWeight.LineWeight009, ["THIETBI"] = LineWeight.LineWeight018, ["GIANG"] = LineWeight.LineWeight025,
        };

        public AcadDraw2D(Database db, Transaction tr, Point3d origin, double textHeight)
        {
            this.db = db; this.tr = tr;
            off = origin - Point3d.Origin;
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            foreach (var kv in LayerColors) EnsureLayer(kv.Key);
            textStyle = EnsureTextStyle();
            dimStyle = EnsureDimStyle(textHeight);
        }

        Point3d P(double x, double y) => new Point3d(x, y, 0) + off;

        void Add(Entity e, string layer)
        {
            EnsureLayer(layer);
            e.SetDatabaseDefaults(db);
            e.Layer = layer;
            if (layer == "TRUC" && centerLoaded) { e.LinetypeScale = scale * 0.3; }
            ms.AppendEntity(e);
            tr.AddNewlyCreatedDBObject(e, true);
            Count++;
        }

        void EnsureLayer(string name)
        {
            if (string.IsNullOrEmpty(name) || layers.Contains(name)) return;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(name))
            {
                lt.UpgradeOpen();
                var r = new LayerTableRecord { Name = name, Color = Color.FromColorIndex(ColorMethod.ByAci, LayerColors.TryGetValue(name, out var c) ? c : (short)7) };
                if (LayerWeights.TryGetValue(name, out var lw)) r.LineWeight = lw;
                if (name == "TRUC") { var ltid = CenterLineType(); if (!ltid.IsNull) { r.LinetypeObjectId = ltid; centerLoaded = true; } }
                lt.Add(r); tr.AddNewlyCreatedDBObject(r, true);
            }
            layers.Add(name);
        }

        bool centerLoaded;

        ObjectId CenterLineType()
        {
            try
            {
                var tbl = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                if (!tbl.Has("CENTER")) db.LoadLineTypeFile("CENTER", "acadiso.lin");
                tbl = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                return tbl.Has("CENTER") ? tbl["CENTER"] : ObjectId.Null;
            }
            catch { return ObjectId.Null; }
        }

        ObjectId EnsureTextStyle()
        {
            var tt = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (tt.Has("VXT")) return tt["VXT"];
            tt.UpgradeOpen();
            var s = new TextStyleTableRecord { Name = "VXT", FileName = "arial.ttf", XScale = 0.8, TextSize = 0 };
            var id = tt.Add(s); tr.AddNewlyCreatedDBObject(s, true);
            return id;
        }

        ObjectId EnsureDimStyle(double th, string name = "VXT")
        {
            var dt = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
            DimStyleTableRecord s;
            ObjectId id;
            if (dt.Has(name)) { id = dt[name]; s = (DimStyleTableRecord)tr.GetObject(id, OpenMode.ForWrite); }
            else { dt.UpgradeOpen(); s = new DimStyleTableRecord { Name = name }; id = dt.Add(s); tr.AddNewlyCreatedDBObject(s, true); }
            s.Dimscale = 1;
            s.Dimtxt = th;            // chữ kích thước = chữ in 2,5 mm × tỉ lệ
            s.Dimtsz = th * 0.5;      // gạch xiên thay mũi tên
            s.Dimasz = th * 0.5;
            s.Dimexo = th * 0.4;
            s.Dimexe = th * 0.4;
            s.Dimgap = th * 0.25;
            s.Dimdec = 0;
            s.Dimzin = 8;
            s.Dimtad = 1;             // chữ trên đường kích thước
            s.Dimtih = false; s.Dimtoh = false;
            s.Dimtxsty = textStyle;
            s.Dimclrd = Color.FromColorIndex(ColorMethod.ByAci, 4);
            s.Dimclre = Color.FromColorIndex(ColorMethod.ByAci, 4);
            s.Dimclrt = Color.FromColorIndex(ColorMethod.ByAci, 2);
            return id;
        }

        /// <summary>Mỗi tỉ lệ một kiểu kích thước VXT_100, VXT_50… để chữ in luôn cao 2,5 mm.</summary>
        public void SetScale(double newScale)
        {
            scale = newScale;
            if (!dimStyles.TryGetValue(newScale, out var id))
            {
                id = EnsureDimStyle(2.5 * newScale, "VXT_" + newScale.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                dimStyles[newScale] = id;
            }
            dimStyle = id;
        }

        public void Arc(string layer, double cx, double cy, double r, double startDeg, double endDeg)
        {
            if (r <= 0) return;
            Add(new Arc(P(cx, cy), Vector3d.ZAxis, r, startDeg * Math.PI / 180, endDeg * Math.PI / 180), layer);
        }

        public void Hatch(string layer, IList<(double x, double y)> poly, string pattern, double spacing)
        {
            if (poly == null || poly.Count < 3) return;
            var h = new AcHatch();
            Add(h, layer);
            bool solid = pattern == "SOLID";
            h.SetHatchPattern(HatchPatternType.PreDefined, solid ? "SOLID" : pattern);
            if (!solid)
            {
                // ANSI31 có khoảng cách nét 3,175 đơn vị ở tỉ lệ 1
                h.PatternScale = Math.Max(spacing / 3.175, 1e-3);
                h.SetHatchPattern(HatchPatternType.PreDefined, pattern);
            }
            h.Associative = false;
            var vtx = new Point2dCollection();
            var bulge = new DoubleCollection();
            foreach (var (x, y) in poly) { var p = P(x, y); vtx.Add(new Point2d(p.X, p.Y)); bulge.Add(0); }
            var first = P(poly[0].x, poly[0].y);
            vtx.Add(new Point2d(first.X, first.Y)); bulge.Add(0);
            h.AppendLoop(HatchLoopTypes.Default, vtx, bulge);
            h.EvaluateHatch(true);
        }

        public void Line(string layer, double x1, double y1, double x2, double y2)
        {
            if (Math.Abs(x1 - x2) < 1e-6 && Math.Abs(y1 - y2) < 1e-6) return;
            Add(new Line(P(x1, y1), P(x2, y2)), layer);
        }

        public void Circle(string layer, double x, double y, double r)
        {
            if (r <= 0) return;
            Add(new Circle(P(x, y), Vector3d.ZAxis, r), layer);
        }

        /// <summary>Chấm đặc dạng donut (polyline 2 cung, bề rộng = bán kính).</summary>
        public void Dot(string layer, double x, double y, double r)
        {
            if (r <= 0) return;
            var c = P(x, y);
            var pl = new Polyline(2);
            pl.AddVertexAt(0, new Point2d(c.X - r / 2, c.Y), 1, r, r);
            pl.AddVertexAt(1, new Point2d(c.X + r / 2, c.Y), 1, r, r);
            pl.Closed = true;
            Add(pl, layer);
        }

        public void Text(string layer, double x, double y, double height, string text, int h, int v, double rotDeg)
        {
            if (string.IsNullOrEmpty(text)) return;
            var pt = P(x, y);
            var t = new DBText { TextString = text, Height = height, Rotation = rotDeg * Math.PI / 180, TextStyleId = textStyle, WidthFactor = 0.8, Position = pt };
            if (h != 0 || v != 0)
            {
                t.HorizontalMode = h == 1 ? TextHorizontalMode.TextCenter : h == 2 ? TextHorizontalMode.TextRight : TextHorizontalMode.TextLeft;
                t.VerticalMode = v == 1 ? TextVerticalMode.TextBottom : v == 2 ? TextVerticalMode.TextVerticalMid : v == 3 ? TextVerticalMode.TextTop : TextVerticalMode.TextBase;
                t.AlignmentPoint = pt;
            }
            Add(t, layer);
            if (h != 0 || v != 0) t.AdjustAlignment(db);
        }

        public bool Dimension(double x0, double y0, double x1, double y1, double lineY, bool vertical, string overrideText)
        {
            var p1 = P(x0, y0); var p2 = P(x1, y1);
            var dl = vertical ? P(lineY, (y0 + y1) / 2) : P((x0 + x1) / 2, lineY);
            if (p1.DistanceTo(p2) < 1e-6) return true;
            var d = new RotatedDimension(vertical ? Math.PI / 2 : 0, p1, p2, dl, overrideText ?? "", dimStyle);
            Add(d, "KICH_THUOC");
            return true;
        }
    }
}

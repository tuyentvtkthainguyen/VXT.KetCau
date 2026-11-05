using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

[assembly: CommandClass(typeof(KetCauVXT.AcadLink.PickDims))]

namespace KetCauVXT.AcadLink
{
    /// <summary>Lệnh VXT_KT – lấy kích thước đối tượng AutoCAD cho chế độ "Triển khai hồ sơ" của Kết cấu VXT:
    /// dầm (trục + cột → nhịp, 2 cạnh → b, nhập h), cột (hình chữ nhật → b × h), sàn (lưới trục → nhịp X / Y, đa tuyến kín bên trong → lỗ thủng),
    /// móng đơn (hình chữ nhật móng + cột bên trong), móng băng (trục + cột → chiều dài, vị trí cột).
    /// Ghi %AppData%\KetCauVXT\AcadPick\pick_*.json (đơn vị m) – chương trình tự nhận khi cửa sổ Triển khai hồ sơ đang mở
    /// (hoặc bấm "Lấy kích thước từ AutoCAD"). Thuật toán hình học giống KetCauVXT.Core/Detailing/AcadPick.cs (AcadPickGeometry).</summary>
    public class PickDims
    {
        private static Document Doc => AcApp.DocumentManager.MdiActiveDocument;
        private static double _lastH = 0.40;
        private const double MergeTol = 0.010;   // m – gộp các nét trục lệch nhau ≤ 10 mm

        public static string PickDir => Path.Combine(LayerStd.AppDataDir, "AcadPick");

        private sealed class Rect
        {
            public double X0, Y0, X1, Y1;
            public string Name = "";
            public double W { get { return X1 - X0; } }
            public double H { get { return Y1 - Y0; } }
            public double Cx { get { return (X0 + X1) / 2; } }
            public double Cy { get { return (Y0 + Y1) / 2; } }
            public bool Contains(double x, double y) { return x >= X0 && x <= X1 && y >= Y0 && y <= Y1; }
            public bool Inside(Rect o) { return X0 >= o.X0 - 1e-9 && X1 <= o.X1 + 1e-9 && Y0 >= o.Y0 - 1e-9 && Y1 <= o.Y1 + 1e-9; }
        }

        private sealed class Txt { public double X, Y; public string S = ""; }

        [CommandMethod("VXT_KT", CommandFlags.Modal)]
        public void LayKichThuoc()
        {
            var doc = Doc;
            if (doc == null) return;
            var ed = doc.Editor; var db = doc.Database;
            try
            {
                var ko = new PromptKeywordOptions("\nLấy kích thước cho Triển khai hồ sơ [Dam/Cot/San/Mong/MongBang] <Dam>: ", "Dam Cot San Mong MongBang") { AllowNone = true };
                ko.Keywords.Default = "Dam";
                var kr = ed.GetKeywords(ko);
                if (kr.Status != PromptStatus.OK && kr.Status != PromptStatus.None) return;
                string kind = string.IsNullOrEmpty(kr.StringResult) ? "Dam" : kr.StringResult;

                string unitName;
                double k = Exchange.UnitToM((int)db.Insunits, out unitName);
                bool unitKnown = (int)db.Insunits != 0;

                var w = new JsonOut();
                int n = 0;
                switch (kind)
                {
                    case "Dam": n = Beam(ed, db, w, k, unitKnown); break;
                    case "Cot": n = Columns(ed, db, w, k, unitKnown); break;
                    case "San": n = Slab(ed, db, w, k, unitKnown); break;
                    case "Mong": n = Footings(ed, db, w, k, unitKnown); break;
                    case "MongBang": n = Strip(ed, db, w, k, unitKnown); break;
                }
                if (n <= 0) { ed.WriteMessage("\nKhông lấy được đối tượng nào – lệnh kết thúc."); return; }
                Directory.CreateDirectory(PickDir);
                string file = Path.Combine(PickDir, "pick_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ".json");
                File.WriteAllText(file, w.ToString(), new System.Text.UTF8Encoding(false));
                ed.WriteMessage("\nĐã lấy " + n + " đối tượng (" + kind + ", đơn vị bản vẽ: " + unitName + ") → " + file
                                + "\nTrong Kết cấu VXT (chế độ Triển khai hồ sơ): chương trình tự nhận khi cửa sổ đang mở, hoặc bấm \"Lấy kích thước từ AutoCAD\".");
            }
            catch (Exception ex) { ed.WriteMessage("\nLỗi VXT_KT: " + ex.Message); }
        }

        // ------------------------------------------------------------------
        // Đọc hình học
        // ------------------------------------------------------------------
        private static ObjectId[] Select(Editor ed, string msg, string types)
        {
            var o = new PromptSelectionOptions { MessageForAdding = msg };
            var r = ed.GetSelection(o, new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, types) }));
            return r.Status == PromptStatus.OK ? r.Value.GetObjectIds() : new ObjectId[0];
        }

        private static List<Point2d> Vertices(Entity e)
        {
            var l = new List<Point2d>();
            var pl = e as Polyline;
            if (pl != null) { for (int i = 0; i < pl.NumberOfVertices; i++) l.Add(pl.GetPoint2dAt(i)); return l; }
            var ln = e as Line;
            if (ln != null) { l.Add(new Point2d(ln.StartPoint.X, ln.StartPoint.Y)); l.Add(new Point2d(ln.EndPoint.X, ln.EndPoint.Y)); return l; }
            var p2 = e as Polyline2d;
            if (p2 != null)
            {
                foreach (ObjectId vid in p2)
                {
                    var v = vid.GetObject(OpenMode.ForRead) as Vertex2d;
                    if (v != null) l.Add(new Point2d(v.Position.X, v.Position.Y));
                }
            }
            return l;
        }

        private static bool IsClosedRect(Entity e, out Rect r)
        {
            r = null;
            var pl = e as Polyline;
            if (pl == null || pl.NumberOfVertices < 3) return false;
            var pts = Vertices(e);
            bool closed = pl.Closed || pts[0].GetDistanceTo(pts[pts.Count - 1]) < 1e-6;
            if (!closed) return false;
            r = new Rect { X0 = pts.Min(p => p.X), Y0 = pts.Min(p => p.Y), X1 = pts.Max(p => p.X), Y1 = pts.Max(p => p.Y) };
            return r.W > 1e-9 && r.H > 1e-9;
        }

        private static List<Txt> Texts(Transaction tr, ObjectId[] ids)
        {
            var l = new List<Txt>();
            foreach (var id in ids)
            {
                var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                var t = e as DBText;
                if (t != null && !string.IsNullOrWhiteSpace(t.TextString)) { l.Add(new Txt { X = t.Position.X, Y = t.Position.Y, S = t.TextString.Trim() }); continue; }
                var m = e as MText;
                if (m != null && !string.IsNullOrWhiteSpace(m.Text)) l.Add(new Txt { X = m.Location.X, Y = m.Location.Y, S = m.Text.Replace("\r", " ").Replace("\n", " ").Trim() });
            }
            return l;
        }

        /// <summary>Đơn vị bản vẽ chưa khai báo: đoán theo kích thước điển hình (≥ 50 đơn vị → mm, nhỏ hơn → m).</summary>
        private static double Unit(double k, bool known, double typical, Editor ed)
        {
            if (known) return k;
            double g = typical >= 50 ? 0.001 : 1.0;
            ed.WriteMessage("\n$INSUNITS chưa khai báo – đoán đơn vị bản vẽ là " + (g < 0.01 ? "mm" : "m") + " (kích thước điển hình " + typical.ToString("0.###", CultureInfo.InvariantCulture) + ").");
            return g;
        }

        private static List<Rect> Rects(Transaction tr, ObjectId[] ids, List<Txt> texts, string prefix)
        {
            var rs = new List<Rect>();
            foreach (var id in ids)
            {
                var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                Rect r;
                if (e != null && IsClosedRect(e, out r)) rs.Add(r);
            }
            // bỏ hình chữ nhật nằm trong hình khác (vd. nét trong của cột)
            rs = rs.Where(a => !rs.Any(b => !ReferenceEquals(a, b) && a.Inside(b) && (b.W * b.H) > (a.W * a.H) * 1.0001 && prefix != "M")).ToList();
            int i = 1;
            foreach (var r in rs.OrderBy(q => -q.Cy).ThenBy(q => q.Cx))
            {
                var t = texts.FirstOrDefault(x => r.Contains(x.X, x.Y));
                r.Name = t != null && t.S.Length <= 12 ? t.S : prefix + i;
                i++;
            }
            return rs.OrderBy(q => -q.Cy).ThenBy(q => q.Cx).ToList();
        }

        private static void Head(JsonOut w, string kind)
        {
            w.BeginObject();
            w.Prop("version", 1);
            w.Prop("kind", kind);
            w.Prop("unit", "m");
            w.Prop("source", "AutoCAD VXT_KT");
            w.Prop("dwg", Doc != null ? Doc.Name : "");
            w.BeginArray("items");
        }

        private static void Tail(JsonOut w) { w.EndArray(); w.EndObject(); }

        // ------------------------------------------------------------------
        // CỘT: hình chữ nhật → b (X), h (Y)
        // ------------------------------------------------------------------
        private static int Columns(Editor ed, Database db, JsonOut w, double k, bool known)
        {
            var ids = Select(ed, "\nChọn các hình chữ nhật cột (kèm chữ tên cột nếu có): ", "LWPOLYLINE,TEXT,MTEXT");
            if (ids.Length == 0) return 0;
            Head(w, "Cot");
            int n = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var rs = Rects(tr, ids, Texts(tr, ids), "C");
                if (rs.Count > 0) k = Unit(k, known, rs.Max(r => Math.Max(r.W, r.H)), ed);
                foreach (var r in rs)
                {
                    w.BeginObject(); w.Prop("name", r.Name); w.Prop("b", r.W * k); w.Prop("h", r.H * k); w.EndObject(); n++;
                    ed.WriteMessage("\n  " + r.Name + ": " + Math.Round(r.W * k * 1000) + " × " + Math.Round(r.H * k * 1000) + " mm");
                }
                tr.Commit();
            }
            Tail(w);
            return n;
        }

        // ------------------------------------------------------------------
        // MÓNG ĐƠN: hình chữ nhật lớn = móng, hình nhỏ bên trong = cột
        // ------------------------------------------------------------------
        private static int Footings(Editor ed, Database db, JsonOut w, double k, bool known)
        {
            var ids = Select(ed, "\nChọn hình chữ nhật móng và cột bên trong (kèm chữ tên móng nếu có): ", "LWPOLYLINE,TEXT,MTEXT");
            if (ids.Length == 0) return 0;
            Head(w, "Mong");
            int n = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var all = Rects(tr, ids, Texts(tr, ids), "M");
                var foots = all.Where(a => !all.Any(b => !ReferenceEquals(a, b) && a.Inside(b))).ToList();
                if (foots.Count > 0) k = Unit(k, known, foots.Max(r => Math.Max(r.W, r.H)), ed);
                int i = 1;
                foreach (var f in foots)
                {
                    var col = all.Where(c => !ReferenceEquals(c, f) && c.Inside(f)).OrderByDescending(c => c.W * c.H).FirstOrDefault();
                    w.BeginObject();
                    w.Prop("name", f.Name.StartsWith("M") && f.Name.Length <= 12 ? f.Name : "M" + i);
                    w.Prop("L", f.W * k); w.Prop("B", f.H * k);
                    if (col != null)
                    {
                        w.Prop("colL", col.W * k); w.Prop("colB", col.H * k);
                        w.Prop("colX", (col.Cx - f.Cx) * k); w.Prop("colY", (col.Cy - f.Cy) * k);
                    }
                    w.EndObject(); n++; i++;
                    ed.WriteMessage("\n  Móng " + Math.Round(f.W * k, 2) + " × " + Math.Round(f.H * k, 2) + " m" + (col != null ? ", cột " + Math.Round(col.W * k * 1000) + " × " + Math.Round(col.H * k * 1000) + " mm" : ""));
                }
                tr.Commit();
            }
            Tail(w);
            return n;
        }

        // ------------------------------------------------------------------
        // DẦM / MÓNG BĂNG: trục (LINE / đa tuyến) + hình chữ nhật cột
        // ------------------------------------------------------------------
        private static List<Point2d> Axis(Editor ed, Transaction tr, string msg)
        {
            var ids = Select(ed, msg, "LINE,LWPOLYLINE,POLYLINE");
            var segs = new List<List<Point2d>>();
            foreach (var id in ids)
            {
                var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (e == null) continue;
                var v = Vertices(e);
                if (v.Count >= 2) segs.Add(v);
            }
            if (segs.Count == 0) return new List<Point2d>();
            if (segs.Count == 1) return segs[0];
            // nhiều đoạn thẳng thẳng hàng → nối theo phương chính
            var pts = segs.SelectMany(s => s).ToList();
            double dx = pts.Max(p => p.X) - pts.Min(p => p.X), dy = pts.Max(p => p.Y) - pts.Min(p => p.Y);
            var sorted = dx >= dy ? pts.OrderBy(p => p.X).ToList() : pts.OrderBy(p => p.Y).ToList();
            return new List<Point2d> { sorted[0], sorted[sorted.Count - 1] };
        }

        private static double AxisLen(List<Point2d> a) { double s = 0; for (int i = 0; i + 1 < a.Count; i++) s += a[i].GetDistanceTo(a[i + 1]); return s; }

        /// <summary>Chiếu điểm lên trục: (s dọc trục, khoảng cách).</summary>
        private static void Project(List<Point2d> a, double x, double y, out double s, out double dist)
        {
            dist = double.MaxValue; s = 0; double acc = 0;
            for (int i = 0; i + 1 < a.Count; i++)
            {
                double ax = a[i].X, ay = a[i].Y, bx = a[i + 1].X, by = a[i + 1].Y, ddx = bx - ax, ddy = by - ay, L2 = ddx * ddx + ddy * ddy, L = Math.Sqrt(L2);
                double t = L2 < 1e-18 ? 0 : Math.Max(0, Math.Min(1, ((x - ax) * ddx + (y - ay) * ddy) / L2));
                double px = ax + t * ddx, py = ay + t * ddy, d = Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                if (d < dist) { dist = d; s = acc + t * L; }
                acc += L;
            }
        }

        private static List<double> Spacings(IEnumerable<double> pos, double tol)
        {
            var s = pos.OrderBy(x => x).ToList();
            var m = new List<double>();
            int i = 0;
            while (i < s.Count)
            {
                int j = i; double sum = 0;
                while (j < s.Count && s[j] - s[i] <= tol) { sum += s[j]; j++; }
                m.Add(sum / (j - i)); i = j;
            }
            var r = new List<double>();
            for (int q = 1; q < m.Count; q++) r.Add(m[q] - m[q - 1]);
            return r;
        }

        private static int Beam(Editor ed, Database db, JsonOut w, double k, bool known)
        {
            int n = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var axis = Axis(ed, tr, "\nChọn trục dầm (đường thẳng / đa tuyến theo tim dầm): ");
                if (axis.Count < 2) { tr.Commit(); return 0; }
                k = Unit(k, known, AxisLen(axis), ed);
                var cids = Select(ed, "\nChọn các hình chữ nhật cột đỡ dầm (Enter = lấy các đỉnh đa tuyến làm gối): ", "LWPOLYLINE,TEXT,MTEXT");
                var cols = cids.Length > 0 ? Rects(tr, cids, Texts(tr, cids), "C") : new List<Rect>();
                var ss = new List<double>();
                double maxW = 0;
                foreach (var c in cols)
                {
                    double s, d;
                    Project(axis, c.Cx, c.Cy, out s, out d);
                    if (d <= Math.Max(c.W, c.H)) { ss.Add(s); maxW = Math.Max(maxW, Math.Min(c.W, c.H)); }
                }
                if (ss.Count < 2)
                {
                    ss.Clear(); double acc = 0; ss.Add(0);
                    for (int i = 0; i + 1 < axis.Count; i++) { acc += axis[i].GetDistanceTo(axis[i + 1]); ss.Add(acc); }
                }
                var spans = Spacings(ss, MergeTol / k).Where(x => x * k > MergeTol).Select(x => x * k).ToList();
                // bề rộng dầm: 2 cạnh dầm song song (tuỳ chọn)
                double b = 0;
                var eids = Select(ed, "\nChọn 2 cạnh dầm trên mặt bằng để lấy bề rộng b (Enter = bỏ qua): ", "LINE,LWPOLYLINE");
                if (eids.Length >= 2)
                {
                    var mids = new List<double>();
                    foreach (var id in eids.Take(2))
                    {
                        var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        var v = e != null ? Vertices(e) : new List<Point2d>();
                        if (v.Count < 2) continue;
                        double s, d; var mp = new Point2d((v[0].X + v[1].X) / 2, (v[0].Y + v[1].Y) / 2);
                        Project(axis, mp.X, mp.Y, out s, out d);
                        // khoảng cách có dấu tới trục: dùng tích có hướng với đoạn trục đầu
                        double ax = axis[1].X - axis[0].X, ay = axis[1].Y - axis[0].Y;
                        double sign = Math.Sign(ax * (mp.Y - axis[0].Y) - ay * (mp.X - axis[0].X));
                        mids.Add(sign * d);
                    }
                    if (mids.Count == 2) b = Math.Abs(mids[0] - mids[1]) * k;
                }
                var ph = ed.GetDouble(new PromptDoubleOptions("\nChiều cao dầm h (mm) <" + Math.Round(_lastH * 1000) + ">: ") { AllowNegative = false, AllowZero = false, AllowNone = true, DefaultValue = _lastH * 1000, UseDefaultValue = true });
                double h = ph.Status == PromptStatus.OK ? ph.Value / 1000.0 : _lastH;
                _lastH = h;
                var pn = ed.GetString(new PromptStringOptions("\nTên dầm <D1>: ") { AllowSpaces = false, DefaultValue = "D1", UseDefaultValue = true });
                string name = pn.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(pn.StringResult) ? pn.StringResult.Trim() : "D1";
                Head(w, "Dam");
                w.BeginObject();
                w.Prop("name", name);
                if (b > 0) w.Prop("b", b);
                w.Prop("h", h);
                w.Numbers("spans", spans);
                if (maxW > 0)
                {
                    w.BeginArray("columns");
                    foreach (var c in cols) { w.BeginObject(); w.Prop("name", c.Name); w.Prop("b", Math.Min(c.W, c.H) * k); w.EndObject(); }
                    w.EndArray();
                }
                w.EndObject();
                Tail(w);
                n = spans.Count > 0 ? 1 : 0;
                ed.WriteMessage("\n  Dầm " + name + ": nhịp " + string.Join(" + ", spans.Select(x => x.ToString("0.###", CultureInfo.InvariantCulture))) + " m"
                                + (b > 0 ? ", b = " + Math.Round(b * 1000) + " mm" : "") + ", h = " + Math.Round(h * 1000) + " mm");
                tr.Commit();
            }
            return n;
        }

        private static int Strip(Editor ed, Database db, JsonOut w, double k, bool known)
        {
            int n = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var axis = Axis(ed, tr, "\nChọn trục móng băng (đường thẳng / đa tuyến, đầu trục = đầu băng): ");
                if (axis.Count < 2) { tr.Commit(); return 0; }
                k = Unit(k, known, AxisLen(axis), ed);
                var cids = Select(ed, "\nChọn các hình chữ nhật cột trên móng băng: ", "LWPOLYLINE,TEXT,MTEXT");
                var cols = cids.Length > 0 ? Rects(tr, cids, Texts(tr, cids), "C") : new List<Rect>();
                var items = new List<Tuple<string, double, double>>();
                foreach (var c in cols)
                {
                    double s, d;
                    Project(axis, c.Cx, c.Cy, out s, out d);
                    if (d > Math.Max(c.W, c.H)) continue;
                    // cạnh cột dọc trục ≈ kích thước theo phương trục chính
                    double ax = Math.Abs(axis[axis.Count - 1].X - axis[0].X), ay = Math.Abs(axis[axis.Count - 1].Y - axis[0].Y);
                    items.Add(Tuple.Create(c.Name, s * k, (ax >= ay ? c.W : c.H) * k));
                }
                Head(w, "MongBang");
                w.BeginObject();
                w.Prop("name", "MBG1");
                w.Prop("length", AxisLen(axis) * k);
                w.BeginArray("columns");
                foreach (var it in items.OrderBy(t => t.Item2)) { w.BeginObject(); w.Prop("name", it.Item1); w.Prop("x", it.Item2); w.Prop("b", it.Item3); w.EndObject(); }
                w.EndArray();
                w.EndObject();
                Tail(w);
                n = 1;
                ed.WriteMessage("\n  Móng băng dài " + Math.Round(AxisLen(axis) * k, 3) + " m, " + items.Count + " cột");
                tr.Commit();
            }
            return n;
        }

        // ------------------------------------------------------------------
        // SÀN: lưới trục (LINE ngang / đứng) → nhịp X, Y; đa tuyến kín bên trong → lỗ thủng
        // ------------------------------------------------------------------
        private static int Slab(Editor ed, Database db, JsonOut w, double k, bool known)
        {
            var ids = Select(ed, "\nChọn các đường trục (ngang + đứng) của vùng sàn và các đa tuyến kín lỗ thủng: ", "LINE,LWPOLYLINE,TEXT,MTEXT");
            if (ids.Length == 0) return 0;
            var xs = new List<double>(); var ys = new List<double>();
            var holes = new List<Rect>();
            Rect outer = null;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var texts = Texts(tr, ids);
                foreach (var id in ids)
                {
                    var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (e == null) continue;
                    Rect r;
                    if (IsClosedRect(e, out r)) { holes.Add(r); continue; }
                    var v = Vertices(e);
                    for (int i = 0; i + 1 < v.Count; i++)
                    {
                        double dx = v[i + 1].X - v[i].X, dy = v[i + 1].Y - v[i].Y, L = Math.Sqrt(dx * dx + dy * dy);
                        if (L < 1e-9) continue;
                        if (Math.Abs(dx) <= 0.02 * L) xs.Add((v[i].X + v[i + 1].X) / 2);        // nét đứng → toạ độ X
                        else if (Math.Abs(dy) <= 0.02 * L) ys.Add((v[i].Y + v[i + 1].Y) / 2);   // nét ngang → toạ độ Y
                    }
                }
                // không có lưới trục: đa tuyến kín lớn nhất là biên ô sàn
                if (xs.Count < 2 || ys.Count < 2)
                {
                    outer = holes.OrderByDescending(r => r.W * r.H).FirstOrDefault();
                    if (outer != null)
                    {
                        holes.Remove(outer);
                        xs.Add(outer.X0); xs.Add(outer.X1); ys.Add(outer.Y0); ys.Add(outer.Y1);
                    }
                }
                tr.Commit();
                if (xs.Count < 2 || ys.Count < 2) { ed.WriteMessage("\nCần ít nhất 2 trục đứng và 2 trục ngang (hoặc 1 đa tuyến kín biên ô sàn)."); return 0; }
                double typical = Math.Max(xs.Max() - xs.Min(), ys.Max() - ys.Min());
                k = Unit(k, known, typical, ed);
                double ox = xs.Min(), oy = ys.Min();
                var sx = Spacings(xs, MergeTol / k).Where(x => x * k > MergeTol).Select(x => x * k).ToList();
                var sy = Spacings(ys, MergeTol / k).Where(x => x * k > MergeTol).Select(x => x * k).ToList();
                Head(w, "San");
                w.BeginObject();
                w.Prop("name", "SMB1");
                w.Numbers("spansX", sx);
                w.Numbers("spansY", sy);
                w.BeginArray("openings");
                int i2 = 1;
                foreach (var h in holes.OrderBy(q => q.Y0).ThenBy(q => q.X0))
                {
                    var t = texts.FirstOrDefault(x => h.Contains(x.X, x.Y));
                    w.BeginObject();
                    w.Prop("name", t != null && t.S.Length <= 12 ? t.S : "L" + i2);
                    w.Prop("x", (h.X0 - ox) * k); w.Prop("y", (h.Y0 - oy) * k); w.Prop("w", h.W * k); w.Prop("h", h.H * k);
                    w.EndObject(); i2++;
                }
                w.EndArray();
                w.EndObject();
                Tail(w);
                ed.WriteMessage("\n  Sàn: nhịp X " + string.Join(" + ", sx.Select(x => x.ToString("0.###", CultureInfo.InvariantCulture))) + " m; nhịp Y "
                                + string.Join(" + ", sy.Select(x => x.ToString("0.###", CultureInfo.InvariantCulture))) + " m; " + holes.Count + " lỗ thủng");
            }
            return 1;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace KetCauVXT.AcadLink
{
    /// <summary>Một đối tượng hình học trong file trao đổi (toạ độ m). Kind: Line | Polyline | Circle | Arc | Text | Block.</summary>
    internal sealed class Item
    {
        public string Category = "", Layer = "", Kind = "Line", Text = "", Block = "";
        public List<double[]> Points = new List<double[]>();
        public bool Closed;
        public List<double> Bulges;
        public double Height, Rotation, Radius, StartAngle, EndAngle;
    }

    /// <summary>Phân rã đối tượng AutoCAD thành các Item (block phân rã ảo trong bộ nhớ – không sửa bản vẽ).</summary>
    internal sealed class Collector
    {
        private readonly Transaction _tr;
        private readonly double _k;
        public readonly List<Item> Items = new List<Item>();
        public int Skipped;

        /// <param name="unitToM">hệ số đổi đơn vị bản vẽ → m</param>
        public Collector(Transaction tr, double unitToM) { _tr = tr; _k = unitToM; }

        private double[] P(Point3d p) => new[] { p.X * _k, p.Y * _k };
        private double[] P(Point2d p) => new[] { p.X * _k, p.Y * _k };
        private static double Deg(double rad) => rad * 180.0 / Math.PI;

        public void Add(Entity e, string category) => Emit(e, category, null, 0, null);

        /// <param name="parentLayer">lớp của block chứa (nét trên lớp "0" trong block lấy lớp của block)</param>
        private void Emit(Entity e, string cat, string block, int depth, string parentLayer)
        {
            if (e == null || depth > 8) return;
            string layer;
            try { layer = e.Layer ?? "0"; } catch (System.Exception) { layer = "0"; }
            if (layer == "0" && parentLayer != null) layer = parentLayer;
            if (e is AttributeDefinition) return;   // ATTDEF trong block phân rã – giá trị lấy từ AttributeReference
            var line = e as Line;
            if (line != null)
            {
                Items.Add(new Item { Category = cat, Layer = layer, Kind = "Line", Block = block ?? "", Points = { P(line.StartPoint), P(line.EndPoint) } });
                return;
            }
            var pl = e as Polyline;
            if (pl != null)
            {
                var it = new Item { Category = cat, Layer = layer, Kind = "Polyline", Block = block ?? "", Closed = pl.Closed, Bulges = new List<double>() };
                double sgn = pl.Normal.Z < 0 ? -1 : 1;    // mặt phẳng đa tuyến lật (normal −Z) → đổi chiều bulge
                bool anyBulge = false;
                for (int i = 0; i < pl.NumberOfVertices; i++)
                {
                    it.Points.Add(P(pl.GetPoint3dAt(i)));
                    double b = pl.GetBulgeAt(i) * sgn;
                    if (Math.Abs(b) > 1e-12) anyBulge = true;
                    it.Bulges.Add(b);
                }
                if (!anyBulge) it.Bulges = null;
                if (it.Points.Count >= 2) Items.Add(it); else Skipped++;
                return;
            }
            var circle = e as Circle;
            if (circle != null)
            {
                Items.Add(new Item { Category = cat, Layer = layer, Kind = "Circle", Block = block ?? "", Radius = circle.Radius * _k, Points = { P(circle.Center) } });
                return;
            }
            var arc = e as Arc;
            if (arc != null)
            {
                double a0 = arc.StartAngle, a1 = arc.EndAngle;
                if (arc.Normal.Z < 0) { double t = Math.PI - a1; a1 = Math.PI - a0; a0 = t; }
                Items.Add(new Item { Category = cat, Layer = layer, Kind = "Arc", Block = block ?? "", Radius = arc.Radius * _k, StartAngle = Deg(a0), EndAngle = Deg(a1), Points = { P(arc.Center) } });
                return;
            }
            var txt = e as DBText;   // gồm cả AttributeReference
            if (txt != null)
            {
                if (string.IsNullOrWhiteSpace(txt.TextString)) return;
                bool left = txt.HorizontalMode == TextHorizontalMode.TextLeft && txt.VerticalMode == TextVerticalMode.TextBase;
                var at = left ? txt.Position : txt.AlignmentPoint;
                Items.Add(new Item { Category = cat, Layer = layer, Kind = "Text", Block = block ?? "", Text = txt.TextString, Height = txt.Height * _k, Rotation = Deg(txt.Rotation), Points = { P(at) } });
                return;
            }
            var mt = e as MText;
            if (mt != null)
            {
                string s = mt.Text;
                if (string.IsNullOrWhiteSpace(s)) return;
                Items.Add(new Item { Category = cat, Layer = layer, Kind = "Text", Block = block ?? "", Text = s.Replace("\r", "").Trim(), Height = mt.TextHeight * _k, Rotation = Deg(mt.Rotation), Points = { P(mt.Location) } });
                return;
            }
            var br = e as BlockReference;
            if (br != null)
            {
                string name = BlockName(br);
                string outer = block ?? name;
                Items.Add(new Item { Category = cat, Layer = layer, Kind = "Block", Block = name, Rotation = Deg(br.Rotation), Points = { P(br.Position) } });
                // thuộc tính (tên trục trong block ký hiệu trục …)
                try
                {
                    foreach (ObjectId aid in br.AttributeCollection)
                    {
                        var ar = _tr.GetObject(aid, OpenMode.ForRead) as AttributeReference;
                        if (ar != null && !ar.Invisible) Emit(ar, cat, outer, depth + 1, layer);
                    }
                }
                catch (System.Exception) { /* block phân rã trong bộ nhớ không có thuộc tính trong CSDL */ }
                var parts = new DBObjectCollection();
                try { br.Explode(parts); }
                catch (System.Exception) { Skipped++; }
                foreach (DBObject o in parts)
                {
                    var ce = o as Entity;
                    if (ce != null) Emit(ce, cat, outer, depth + 1, layer);
                    o.Dispose();
                }
                return;
            }
            var h = e as Hatch;
            if (h != null)
            {
                EmitHatch(h, cat, layer, block);
                return;
            }
            if (e is Polyline2d || e is Polyline3d)
            {
                var parts = new DBObjectCollection();
                try { e.Explode(parts); }
                catch (System.Exception) { Skipped++; }
                foreach (DBObject o in parts)
                {
                    Emit(o as Entity, cat, block, depth + 1, layer);
                    o.Dispose();
                }
                return;
            }
            Skipped++;
        }

        /// <summary>Biên miền tô → đa tuyến kín (đoạn thẳng + cung tròn; elip/spline bỏ qua).</summary>
        private void EmitHatch(Hatch h, string cat, string layer, string block)
        {
            int n;
            try { n = h.NumberOfLoops; } catch (System.Exception) { Skipped++; return; }
            for (int i = 0; i < n; i++)
            {
                HatchLoop loop;
                try { loop = h.GetLoopAt(i); } catch (System.Exception) { Skipped++; continue; }
                var it = new Item { Category = cat, Layer = layer, Kind = "Polyline", Block = block ?? "", Closed = true, Bulges = new List<double>() };
                if (loop.IsPolyline)
                {
                    foreach (BulgeVertex bv in loop.Polyline)
                    {
                        it.Points.Add(P(bv.Vertex));
                        it.Bulges.Add(bv.Bulge);
                    }
                }
                else
                {
                    foreach (Curve2d c in loop.Curves)
                    {
                        var ls = c as LineSegment2d;
                        var ca = c as CircularArc2d;
                        if (ls != null) { it.Points.Add(P(ls.StartPoint)); it.Bulges.Add(0); }
                        else if (ca != null)
                        {
                            double sweep = Math.Abs(ca.EndAngle - ca.StartAngle);
                            it.Points.Add(P(ca.StartPoint));
                            it.Bulges.Add(Math.Tan(sweep / 4) * (ca.IsClockWise ? -1 : 1));
                        }
                        else Skipped++;
                    }
                }
                if (it.Bulges.All(b => Math.Abs(b) < 1e-12)) it.Bulges = null;
                if (it.Points.Count >= 3) Items.Add(it);
            }
        }

        private string BlockName(BlockReference br)
        {
            try
            {
                if (br.IsDynamicBlock)
                {
                    var btr = _tr.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    if (btr != null) return btr.Name;
                }
            }
            catch (System.Exception) { }
            return br.Name;
        }

        /// <summary>Điểm đại diện để xếp đối tượng vào tầng (đơn vị bản vẽ).</summary>
        public static bool RefPoint(Entity e, out double x, out double y)
        {
            x = y = 0;
            try
            {
                var t = e as DBText;
                if (t != null) { x = t.Position.X; y = t.Position.Y; return true; }
                var m = e as MText;
                if (m != null) { x = m.Location.X; y = m.Location.Y; return true; }
                var b = e as BlockReference;
                if (b != null) { x = b.Position.X; y = b.Position.Y; return true; }
                var ext = e.GeometricExtents;
                x = (ext.MinPoint.X + ext.MaxPoint.X) / 2; y = (ext.MinPoint.Y + ext.MaxPoint.Y) / 2;
                return true;
            }
            catch (System.Exception) { return false; }
        }
    }

    /// <summary>Ghi file trao đổi acad_*.json và giao cho chương trình Kết cấu VXT.</summary>
    internal static class Exchange
    {
        public static string LinkDir => Path.Combine(LayerStd.AppDataDir, "Link");

        /// <summary>Hệ số đổi đơn vị bản vẽ ($INSUNITS) → m; không khai báo → coi là mm.</summary>
        public static double UnitToM(int insunits, out string name)
        {
            switch (insunits)
            {
                case 1: name = "inch"; return 0.0254;
                case 2: name = "feet"; return 0.3048;
                case 4: name = "mm"; return 0.001;
                case 5: name = "cm"; return 0.01;
                case 6: name = "m"; return 1.0;
                case 14: name = "dm"; return 0.1;
                default: name = "không khai báo – coi là mm"; return 0.001;
            }
        }

        public static string Write(string source, string dwg, IList<StoreyDef> storeys, IList<List<Item>> items, double k)
        {
            var w = new JsonOut();
            w.BeginObject();
            w.Prop("Version", 1);
            w.Prop("Source", source);
            w.Prop("Dwg", dwg ?? "");
            w.Prop("Units", "m");
            w.Prop("Created", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
            w.BeginArray("Storeys");
            for (int i = 0; i < storeys.Count; i++)
            {
                var s = storeys[i];
                w.BeginObject();
                w.Prop("Name", s.Name);
                w.Prop("Elevation", s.Elevation);
                w.Prop("Height", s.Height);
                w.Prop("BaseX", s.BaseX * k);
                w.Prop("BaseY", s.BaseY * k);
                w.Prop("X0", Math.Min(s.X0, s.X1) * k);
                w.Prop("Y0", Math.Min(s.Y0, s.Y1) * k);
                w.Prop("X1", Math.Max(s.X0, s.X1) * k);
                w.Prop("Y1", Math.Max(s.Y0, s.Y1) * k);
                w.BeginArray("Items");
                foreach (var it in items[i])
                {
                    w.BeginObject();
                    w.Prop("Category", it.Category);
                    w.Prop("Layer", it.Layer);
                    w.Prop("Kind", it.Kind);
                    w.Points("Points", it.Points);
                    if (it.Closed) w.Prop("Closed", true);
                    if (it.Bulges != null) w.Numbers("Bulges", it.Bulges);
                    if (!string.IsNullOrEmpty(it.Text)) { w.Prop("Text", it.Text); w.Prop("Height", it.Height); }
                    if (Math.Abs(it.Rotation) > 1e-9) w.Prop("Rotation", it.Rotation);
                    if (!string.IsNullOrEmpty(it.Block)) w.Prop("Block", it.Block);
                    if (it.Radius > 0) w.Prop("Radius", it.Radius);
                    if (it.Kind == "Arc") { w.Prop("StartAngle", it.StartAngle); w.Prop("EndAngle", it.EndAngle); }
                    w.EndObject();
                }
                w.EndArray();
                w.EndObject();
            }
            w.EndArray();
            w.EndObject();

            Directory.CreateDirectory(LinkDir);
            string path = Path.Combine(LinkDir, "acad_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".json");
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, w.ToString(), new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);     // đổi tên khi ghi xong → chương trình (FileSystemWatcher) không đọc file dở dang
            return path;
        }

        /// <summary>Báo/mở chương trình: đang chạy → chương trình tự nhận file mới; chưa chạy → mở exe (đường dẫn trong app_path.txt) với --acad.</summary>
        public static string Notify(string jsonPath)
        {
            try
            {
                if (Process.GetProcessesByName("KetCauVXT").Length > 0)
                    return "Kết cấu VXT đang mở – chương trình sẽ hỏi nhập dữ liệu mới.";
            }
            catch (System.Exception) { }
            string exe = "";
            try
            {
                string f = Path.Combine(LayerStd.AppDataDir, "app_path.txt");
                if (File.Exists(f)) exe = File.ReadAllText(f).Trim();
            }
            catch (System.Exception) { }
            if (exe.Length == 0 || !File.Exists(exe))
                return "Chưa tìm thấy chương trình Kết cấu VXT (hãy mở KetCauVXT.exe một lần để đăng ký).\nTrong chương trình: Tệp → Nhận dữ liệu từ AutoCAD… và chọn file:\n" + jsonPath;
            try
            {
                Process.Start(new ProcessStartInfo(exe) { Arguments = "--acad \"" + jsonPath + "\"", UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) });
                return "Đã mở Kết cấu VXT: " + exe;
            }
            catch (System.Exception ex)
            {
                return "Không mở được " + exe + ": " + ex.Message + "\nMở chương trình rồi vào Tệp → Nhận dữ liệu từ AutoCAD… và chọn file:\n" + jsonPath;
            }
        }
    }
}

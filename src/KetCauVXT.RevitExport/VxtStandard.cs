using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace KetCauVXT.RevitExport
{
    // =====================================================================
    // CHUẨN VIỆT CONST TRONG REVIT
    // Đọc từ file *.vxt2revit.json do Việt Const xuất (Tệp → Xuất sang Revit), hoặc chuẩn mặc định khi chưa có mô hình Việt Const.
    // Gồm: cao độ, trục, vật liệu, loại tường / cột / dầm / sàn, thông tin dự án + khung tên, nét in, và cấu kiện kết cấu (dựng / đồng bộ).
    // Quy ước tên: cột "C22x30", dầm "D22x40" (cm), sàn "S100" (mm), tường "Tường 220 trát 2 mặt", cao độ "Tầng 1", "T2"…, "MAI".
    // =====================================================================
    internal sealed class VxtLayer { public string Function = "Structure"; public string Material = ""; public double Thickness; }
    internal sealed class VxtWallType { public string Name = ""; public double Thickness; public double WeightPerM2; public List<VxtLayer> Layers = new List<VxtLayer>(); }
    internal sealed class VxtRectType { public string Name = ""; public double B, H; public string Material = ""; }
    internal sealed class VxtSlabType { public string Name = ""; public double Thickness; public string Material = ""; }
    internal sealed class VxtMaterial { public string Name = ""; public string Kind = ""; public double UnitWeight; }
    internal sealed class VxtLevel { public string Name = ""; public double Elevation; }
    internal sealed class VxtGrid { public string Name = ""; public string Dir = "X"; public double Coord; }
    internal sealed class VxtLineWeight { public string Category = ""; public int Cut, Projection; }
    internal sealed class VxtColumn { public string Id = "", Label = "", Section = ""; public double X, Y, ZBot, ZTop, B, H, Angle; }
    internal sealed class VxtBeam { public string Id = "", Label = "", Section = ""; public double X1, Y1, X2, Y2, Z, B, H; }
    internal sealed class VxtSlab { public string Id = "", Label = "", Section = ""; public double Z, Thickness; public List<double[]> Outline = new List<double[]>(); }

    internal sealed class VxtStandard
    {
        /// <summary>Có đọc từ file Việt Const (false = chuẩn mặc định)</summary>
        public bool FromFile;
        public string File = "";
        public string ProjectName = "", Owner = "", Location = "", Designer = "", Grade = "";
        public List<VxtLevel> Levels = new List<VxtLevel>();
        public List<VxtGrid> Grids = new List<VxtGrid>();
        public List<VxtMaterial> Materials = new List<VxtMaterial>();
        public List<VxtWallType> WallTypes = new List<VxtWallType>();
        public List<VxtRectType> ColumnTypes = new List<VxtRectType>();
        public List<VxtRectType> BeamTypes = new List<VxtRectType>();
        public List<VxtSlabType> SlabTypes = new List<VxtSlabType>();
        public List<VxtColumn> Columns = new List<VxtColumn>();
        public List<VxtBeam> Beams = new List<VxtBeam>();
        public List<VxtSlab> Slabs = new List<VxtSlab>();
        public Dictionary<string, string> TitleFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<VxtLineWeight> LineWeights = new List<VxtLineWeight>();
        public string PrefixStructure = "KC";

        // ------------------------------------------------------------ quy ước tên
        public static string Cm(double m)
        {
            double v = Math.Round(m * 1000) / 10;
            return v.ToString(Math.Abs(v - Math.Round(v)) < 1e-9 ? "0" : "0.#", CultureInfo.InvariantCulture);
        }
        public static string ColumnName(double b, double h) => "C" + Cm(b) + "x" + Cm(h);
        public static string BeamName(double b, double h) => "D" + Cm(b) + "x" + Cm(h);
        public static string SlabName(double t) => "S" + Math.Round(t * 1000).ToString(CultureInfo.InvariantCulture);
        public static string WallName(double t) => "Tường " + Math.Round(t * 1000).ToString(CultureInfo.InvariantCulture) + " trát 2 mặt";

        // ------------------------------------------------------------ chuẩn mặc định
        public static VxtStandard Default()
        {
            var s = new VxtStandard();
            foreach (var g in new[] { "B20", "B25", "B30" }) s.Materials.Add(new VxtMaterial { Name = "Bê tông " + g, Kind = "Concrete", UnitWeight = 25 });
            foreach (var g in new[] { "CB240-T", "CB300-V", "CB400-V" }) s.Materials.Add(new VxtMaterial { Name = "Thép " + g, Kind = "Rebar", UnitWeight = 78.5 });
            s.Materials.Add(new VxtMaterial { Name = "Vữa xi măng", Kind = "Masonry", UnitWeight = 18 });
            s.Materials.Add(new VxtMaterial { Name = "Gạch đặc", Kind = "Masonry", UnitWeight = 18 });
            s.Materials.Add(new VxtMaterial { Name = "Gạch rỗng", Kind = "Masonry", UnitWeight = 15 });
            s.Materials.Add(new VxtMaterial { Name = "Bê tông nhẹ AAC", Kind = "Masonry", UnitWeight = 8 });
            foreach (var t in new[] { 0.11, 0.22 })
            {
                var w = new VxtWallType { Name = WallName(t), Thickness = t };
                w.Layers.Add(new VxtLayer { Function = "Finish", Material = "Vữa xi măng", Thickness = 0.015 });
                w.Layers.Add(new VxtLayer { Function = "Structure", Material = t < 0.15 ? "Gạch rỗng" : "Gạch đặc", Thickness = Math.Round(t - 0.03, 3) });
                w.Layers.Add(new VxtLayer { Function = "Finish", Material = "Vữa xi măng", Thickness = 0.015 });
                s.WallTypes.Add(w);
            }
            foreach (var lw in new[] { ("Structural Columns", 6, 2), ("Structural Framing", 5, 2), ("Floors", 4, 1), ("Walls", 5, 1), ("Structural Foundations", 5, 2),
                                       ("Stairs", 3, 1), ("Doors", 2, 1), ("Windows", 2, 1), ("Grids", 1, 1), ("Levels", 1, 1) })
                s.LineWeights.Add(new VxtLineWeight { Category = lw.Item1, Cut = lw.Item2, Projection = lw.Item3 });
            return s;
        }

        // ------------------------------------------------------------ đọc file Việt Const
        public static VxtStandard Load(string path)
        {
            var root = MiniJson.Parse(System.IO.File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
            if (root == null || !MiniJson.S(root, "Schema").StartsWith("vxt2revit", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Không phải file Việt Const → Revit (*.vxt2revit.json).");
            var s = Default();
            s.FromFile = true; s.File = path;
            var pj = MiniJson.O(root, "Project");
            s.ProjectName = MiniJson.S(pj, "Name"); s.Owner = MiniJson.S(pj, "Owner"); s.Location = MiniJson.S(pj, "Location");
            s.Designer = MiniJson.S(pj, "Designer"); s.Grade = MiniJson.S(pj, "Grade");
            foreach (var o in MiniJson.A(root, "Levels")) s.Levels.Add(new VxtLevel { Name = MiniJson.S(o, "Name"), Elevation = MiniJson.D(o, "Elevation") });
            foreach (var o in MiniJson.A(root, "Grids")) s.Grids.Add(new VxtGrid { Name = MiniJson.S(o, "Name"), Dir = MiniJson.S(o, "Dir", "X"), Coord = MiniJson.D(o, "Coord") });
            foreach (var o in MiniJson.A(root, "Materials"))
            {
                string n = MiniJson.S(o, "Name");
                if (n.Length == 0 || s.Materials.Any(m => m.Name.Equals(n, StringComparison.OrdinalIgnoreCase))) continue;
                s.Materials.Add(new VxtMaterial { Name = n, Kind = MiniJson.S(o, "Kind"), UnitWeight = MiniJson.D(o, "UnitWeight") });
            }
            var wts = MiniJson.A(root, "WallTypes");
            if (wts.Count > 0) s.WallTypes.Clear();
            foreach (var o in wts)
            {
                var w = new VxtWallType { Name = MiniJson.S(o, "Name"), Thickness = MiniJson.D(o, "Thickness"), WeightPerM2 = MiniJson.D(o, "WeightPerM2") };
                foreach (var l in MiniJson.A(o, "Layers"))
                    w.Layers.Add(new VxtLayer { Function = MiniJson.S(l, "Function", "Structure"), Material = MiniJson.S(l, "Material"), Thickness = MiniJson.D(l, "Thickness") });
                if (w.Layers.Count == 0) w.Layers.Add(new VxtLayer { Function = "Structure", Material = "Gạch đặc", Thickness = w.Thickness });
                s.WallTypes.Add(w);
            }
            foreach (var o in MiniJson.A(root, "ColumnTypes")) s.ColumnTypes.Add(Rect(o));
            foreach (var o in MiniJson.A(root, "BeamTypes")) s.BeamTypes.Add(Rect(o));
            foreach (var o in MiniJson.A(root, "SlabTypes")) s.SlabTypes.Add(new VxtSlabType { Name = MiniJson.S(o, "Name"), Thickness = MiniJson.D(o, "Thickness"), Material = MiniJson.S(o, "Material") });
            foreach (var o in MiniJson.A(root, "Columns"))
                s.Columns.Add(new VxtColumn
                {
                    Id = MiniJson.S(o, "Id"), Label = MiniJson.S(o, "Label"), Section = MiniJson.S(o, "Section"),
                    X = MiniJson.D(o, "X"), Y = MiniJson.D(o, "Y"), ZBot = MiniJson.D(o, "ZBot"), ZTop = MiniJson.D(o, "ZTop"),
                    B = MiniJson.D(o, "B"), H = MiniJson.D(o, "H"), Angle = MiniJson.D(o, "Angle"),
                });
            foreach (var o in MiniJson.A(root, "Beams"))
                s.Beams.Add(new VxtBeam
                {
                    Id = MiniJson.S(o, "Id"), Label = MiniJson.S(o, "Label"), Section = MiniJson.S(o, "Section"),
                    X1 = MiniJson.D(o, "X1"), Y1 = MiniJson.D(o, "Y1"), X2 = MiniJson.D(o, "X2"), Y2 = MiniJson.D(o, "Y2"), Z = MiniJson.D(o, "Z"),
                    B = MiniJson.D(o, "B"), H = MiniJson.D(o, "H"),
                });
            foreach (var o in MiniJson.A(root, "Slabs"))
            {
                var sl = new VxtSlab { Id = MiniJson.S(o, "Id"), Label = MiniJson.S(o, "Label"), Section = MiniJson.S(o, "Section"), Z = MiniJson.D(o, "Z"), Thickness = MiniJson.D(o, "Thickness") };
                foreach (var pt in MiniJson.A(o, "Outline"))
                    if (pt is List<object> xy && xy.Count >= 2 && xy[0] is double x && xy[1] is double y) sl.Outline.Add(new[] { x, y });
                if (sl.Outline.Count >= 3) s.Slabs.Add(sl);
            }
            var sh = MiniJson.O(root, "Sheet");
            if (sh != null)
            {
                s.PrefixStructure = MiniJson.S(sh, "PrefixStructure", "KC");
                var tf = MiniJson.O(sh, "TitleFields");
                if (tf != null) foreach (var kv in tf) if (kv.Value is string v && v.Length > 0) s.TitleFields[kv.Key] = v;
            }
            var lws = MiniJson.A(root, "LineWeights");
            if (lws.Count > 0) s.LineWeights.Clear();
            foreach (var o in lws)
                s.LineWeights.Add(new VxtLineWeight { Category = MiniJson.S(o, "Category"), Cut = (int)MiniJson.D(o, "Cut", 1), Projection = (int)MiniJson.D(o, "Projection", 1) });
            return s;
        }

        private static VxtRectType Rect(object o) => new VxtRectType { Name = MiniJson.S(o, "Name"), B = MiniJson.D(o, "B"), H = MiniJson.D(o, "H"), Material = MiniJson.S(o, "Material") };

        /// <summary>Hỏi file Việt Const; bỏ qua → chuẩn mặc định (nếu cho phép).</summary>
        public static VxtStandard Ask(string title, bool allowDefault)
        {
            using (var dlg = new System.Windows.Forms.OpenFileDialog())
            {
                dlg.Title = title;
                dlg.Filter = "File Việt Const → Revit (*.vxt2revit.json)|*.vxt2revit.json|JSON (*.json)|*.json";
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) return Load(dlg.FileName);
            }
            if (!allowDefault) return null;
            var r = System.Windows.Forms.MessageBox.Show(
                "Không chọn file Việt Const.\nDùng chuẩn mặc định của Việt Const (tên tầng, trục, vật liệu B20–B30, tường 110/220 trát 2 mặt, tên loại cột C/dầm D/sàn S, nét in)?",
                title, System.Windows.Forms.MessageBoxButtons.OKCancel, System.Windows.Forms.MessageBoxIcon.Question);
            return r == System.Windows.Forms.DialogResult.OK ? Default() : null;
        }
    }
}

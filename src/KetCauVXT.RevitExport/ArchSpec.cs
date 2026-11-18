using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KetCauVXT.RevitExport
{
    // =====================================================================
    // PHƯƠNG ÁN KIẾN TRÚC (*.vxtphuongan.json) – do Việt Const / agent lập từ lời mô tả, ảnh phác, CAD.
    // Cùng cấu trúc với *.vxtrevit.json (đơn vị m, toạ độ dự án) + cầu thang, mặt tiền, khu đất.
    // =====================================================================
    internal sealed class AsLevel { public string Name = ""; public double Elevation; }
    internal sealed class AsGrid { public string Name = ""; public double X1, Y1, X2, Y2; }
    internal sealed class AsWall { public string Id = "", Level = "", TypeName = ""; public double X1, Y1, X2, Y2, Height, BaseOffset; public List<VxtLayer> Layers = new List<VxtLayer>(); public double Thickness => Layers.Sum(l => l.Thickness); }
    internal sealed class AsOpening { public string Id = "", Level = "", Category = "Door", TypeName = "", Family = ""; public double X, Y, Width, Height, Sill; }
    internal sealed class AsRoom { public string Id = "", Level = "", Name = "", Number = ""; public List<double[]> Outline = new List<double[]>(); }
    internal sealed class AsFloor { public string Id = "", Level = ""; public double Thickness; public List<double[]> Outline = new List<double[]>(); }
    internal sealed class AsRun { public double X1, Y1, X2, Y2, Width = 1.0; public int Risers; }
    internal sealed class AsStair
    {
        public string Id = "", BaseLevel = "", TopLevel = "", Shape = ""; public double Width; public List<double[]> Outline = new List<double[]>();
        public List<AsRun> Runs = new List<AsRun>();
        public List<List<double[]>> Landings = new List<List<double[]>>();
    }
    internal sealed class AsFacadeMaterial { public string Part = "", Material = "", Color = ""; }
    internal sealed class AsColumn { public string Id = "", BaseLevel = "", TopLevel = ""; public double X, Y, B, H, Rot; }
    internal sealed class AsBeam { public string Id = "", Level = ""; public double X1, Y1, X2, Y2, Z, B, H; }
    /// <summary>Chi tiết mặt tiền (LanCan / Khoi / MaiDoc / Lam) – xem RvFacadeElement trong Việt Const.</summary>
    internal sealed class AsFacadeElement
    {
        public string Id = "", Type = "Khoi", Part = "", Style = "", Level = "", SlopeAxis = "", Color = "";
        public List<double[]> Points = new List<double[]>();
        public double Z0, Z1, Height, Thickness, Spacing;
    }

    internal sealed class ArchSpec
    {
        public string File = "", ProjectName = "";
        public List<AsLevel> Levels = new List<AsLevel>();
        public List<AsGrid> Grids = new List<AsGrid>();
        public List<AsWall> Walls = new List<AsWall>();
        public List<AsOpening> Openings = new List<AsOpening>();
        public List<AsRoom> Rooms = new List<AsRoom>();
        public List<AsFloor> Floors = new List<AsFloor>();
        public List<AsStair> Stairs = new List<AsStair>();
        public string FacadeStyle = "", FacadeDescription = "";
        public double FrontDirectionDeg = 180;
        public List<AsFacadeMaterial> FacadeMaterials = new List<AsFacadeMaterial>();
        public List<AsFacadeElement> FacadeElements = new List<AsFacadeElement>();
        public List<AsBeam> Beams = new List<AsBeam>();
        public List<AsColumn> Columns = new List<AsColumn>();
        public double SiteWidth, SiteDepth;
        public List<string> FamilySources = new List<string>();

        public static ArchSpec Load(string path)
        {
            var root = MiniJson.Parse(System.IO.File.ReadAllText(path, Encoding.UTF8));
            var s = new ArchSpec { File = path, ProjectName = MiniJson.S(root, "ProjectName") };
            foreach (var o in MiniJson.A(root, "Levels")) s.Levels.Add(new AsLevel { Name = MiniJson.S(o, "Name"), Elevation = MiniJson.D(o, "Elevation") });
            foreach (var o in MiniJson.A(root, "Grids")) s.Grids.Add(new AsGrid { Name = MiniJson.S(o, "Name"), X1 = MiniJson.D(o, "X1"), Y1 = MiniJson.D(o, "Y1"), X2 = MiniJson.D(o, "X2"), Y2 = MiniJson.D(o, "Y2") });
            foreach (var o in MiniJson.A(root, "Walls"))
            {
                var w = new AsWall { Id = MiniJson.S(o, "Id"), Level = MiniJson.S(o, "Level"), TypeName = MiniJson.S(o, "TypeName"), X1 = MiniJson.D(o, "X1"), Y1 = MiniJson.D(o, "Y1"), X2 = MiniJson.D(o, "X2"), Y2 = MiniJson.D(o, "Y2"), Height = MiniJson.D(o, "Height"), BaseOffset = MiniJson.D(o, "BaseOffset") };
                foreach (var l in MiniJson.A(o, "Layers")) w.Layers.Add(new VxtLayer { Function = MiniJson.S(l, "Function", "Structure"), Material = MiniJson.S(l, "Material"), Thickness = MiniJson.D(l, "Thickness") });
                s.Walls.Add(w);
            }
            foreach (var o in MiniJson.A(root, "Openings")) s.Openings.Add(new AsOpening { Id = MiniJson.S(o, "Id"), Level = MiniJson.S(o, "Level"), Category = MiniJson.S(o, "Category", "Door"), TypeName = MiniJson.S(o, "TypeName"), Family = MiniJson.S(o, "Family"), X = MiniJson.D(o, "X"), Y = MiniJson.D(o, "Y"), Width = MiniJson.D(o, "Width"), Height = MiniJson.D(o, "Height"), Sill = MiniJson.D(o, "Sill") });
            foreach (var o in MiniJson.A(root, "Columns"))
                s.Columns.Add(new AsColumn { Id = MiniJson.S(o, "Id"), BaseLevel = MiniJson.S(o, "BaseLevel"), TopLevel = MiniJson.S(o, "TopLevel"), X = MiniJson.D(o, "X"), Y = MiniJson.D(o, "Y"), B = MiniJson.D(o, "B", 0.22), H = MiniJson.D(o, "H", 0.22), Rot = MiniJson.D(o, "RotationDeg") });
            foreach (var o in MiniJson.A(root, "Beams"))
                s.Beams.Add(new AsBeam { Id = MiniJson.S(o, "Id"), Level = MiniJson.S(o, "Level"), X1 = MiniJson.D(o, "X1"), Y1 = MiniJson.D(o, "Y1"), X2 = MiniJson.D(o, "X2"), Y2 = MiniJson.D(o, "Y2"), Z = MiniJson.D(o, "Z1"), B = MiniJson.D(o, "B", 0.22), H = MiniJson.D(o, "H", 0.35) });
            foreach (var o in MiniJson.A(root, "Rooms")) s.Rooms.Add(new AsRoom { Id = MiniJson.S(o, "Id"), Level = MiniJson.S(o, "Level"), Name = MiniJson.S(o, "Name"), Number = MiniJson.S(o, "Number"), Outline = Pts(o, "Outline") });
            foreach (var o in MiniJson.A(root, "Floors")) s.Floors.Add(new AsFloor { Id = MiniJson.S(o, "Id"), Level = MiniJson.S(o, "Level"), Thickness = MiniJson.D(o, "Thickness", 0.1), Outline = Pts(o, "Outline") });
            foreach (var o in MiniJson.A(root, "Stairs"))
            {
                var st = new AsStair { Id = MiniJson.S(o, "Id"), BaseLevel = MiniJson.S(o, "BaseLevel"), TopLevel = MiniJson.S(o, "TopLevel"), Shape = MiniJson.S(o, "Shape"), Width = MiniJson.D(o, "Width", 0.9), Outline = Pts(o, "Outline") };
                foreach (var r in MiniJson.A(o, "Runs"))
                    st.Runs.Add(new AsRun { X1 = MiniJson.D(r, "X1"), Y1 = MiniJson.D(r, "Y1"), X2 = MiniJson.D(r, "X2"), Y2 = MiniJson.D(r, "Y2"), Width = MiniJson.D(r, "Width", 1.0), Risers = (int)MiniJson.D(r, "Risers") });
                foreach (var l in MiniJson.A(o, "Landings"))
                    if (l is List<object> pl) { var poly = new List<double[]>(); foreach (var p in pl) if (p is List<object> q && q.Count >= 2 && q[0] is double x && q[1] is double y) poly.Add(new[] { x, y }); st.Landings.Add(poly); }
                s.Stairs.Add(st);
            }
            foreach (var p in MiniJson.A(root, "FamilySources")) if (p is string ps && ps.Trim().Length > 0) s.FamilySources.Add(ps.Trim());
            var f = MiniJson.O(root, "Facade");
            if (f != null)
            {
                s.FacadeStyle = MiniJson.S(f, "Style"); s.FacadeDescription = MiniJson.S(f, "Description");
                s.FrontDirectionDeg = MiniJson.D(f, "FrontDirectionDeg", 180);
                foreach (var m in MiniJson.A(f, "Materials")) s.FacadeMaterials.Add(new AsFacadeMaterial { Part = MiniJson.S(m, "Part"), Material = MiniJson.S(m, "Material"), Color = MiniJson.S(m, "Color") });
                foreach (var e in MiniJson.A(f, "Elements"))
                    s.FacadeElements.Add(new AsFacadeElement
                    {
                        Id = MiniJson.S(e, "Id"), Type = MiniJson.S(e, "Type", "Khoi"), Part = MiniJson.S(e, "Part"), Style = MiniJson.S(e, "Style"), Level = MiniJson.S(e, "Level"),
                        SlopeAxis = MiniJson.S(e, "SlopeAxis"), Color = MiniJson.S(e, "Color"), Points = Pts(e, "Points"),
                        Z0 = MiniJson.D(e, "Z0"), Z1 = MiniJson.D(e, "Z1"), Height = MiniJson.D(e, "Height"), Thickness = MiniJson.D(e, "Thickness"), Spacing = MiniJson.D(e, "Spacing"),
                    });
            }
            var site = MiniJson.O(root, "Site");
            if (site != null) { s.SiteWidth = MiniJson.D(site, "Width"); s.SiteDepth = MiniJson.D(site, "Depth"); }
            if (s.Levels.Count == 0) throw new InvalidDataException("Phương án không có cao độ (Levels).");
            return s;
        }

        private static List<double[]> Pts(object o, string key)
        {
            var r = new List<double[]>();
            foreach (var p in MiniJson.A(o, key))
                if (p is List<object> l && l.Count >= 2 && l[0] is double x && l[1] is double y) r.Add(new[] { x, y });
            return r;
        }

        /// <summary>Khung bao mặt bằng (m): xmin, ymin, xmax, ymax; ưu tiên tường, sau đó sàn.</summary>
        public double[] Bounds()
        {
            var xs = new List<double>(); var ys = new List<double>();
            foreach (var w in Walls) { xs.Add(w.X1); xs.Add(w.X2); ys.Add(w.Y1); ys.Add(w.Y2); }
            foreach (var f in Floors) foreach (var p in f.Outline) { xs.Add(p[0]); ys.Add(p[1]); }
            if (xs.Count == 0) return new double[] { 0, 0, 10, 10 };
            return new[] { xs.Min(), ys.Min(), xs.Max(), ys.Max() };
        }

        /// <summary>Cao độ đỉnh công trình (m): cao độ cao nhất + tường cao nhất đặt trên cao độ đó.</summary>
        public double TopElevation()
        {
            var top = Levels.OrderByDescending(l => l.Elevation).First();
            return top.Elevation + Walls.Where(w => w.Level == top.Name).Select(w => w.Height).DefaultIfEmpty(0).Max();
        }

        /// <summary>Hỏi file phương án (khi không chạy qua agent).</summary>
        public static ArchSpec Ask(string title)
        {
            string preset = AgentMode.TakeInputPath();
            if (!string.IsNullOrEmpty(preset)) return Load(preset);
            using (var dlg = new System.Windows.Forms.OpenFileDialog())
            {
                dlg.Title = title;
                dlg.Filter = "Phương án Việt Const (*.vxtphuongan.json)|*.vxtphuongan.json|Mô hình trao đổi (*.vxtrevit.json)|*.vxtrevit.json|JSON (*.json)|*.json";
                return dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK ? Load(dlg.FileName) : null;
            }
        }
    }

    /// <summary>
    /// Trạng thái khi lệnh chạy qua cầu nối agent: đường dẫn đặt sẵn (thay hộp chọn file), không hiện thông báo cuối,
    /// tự áp dụng (CHỈ bật cho lệnh kiến trúc, ảnh, bản vẽ – lệnh kết cấu luôn để người dùng duyệt).
    /// </summary>
    internal static class AgentMode
    {
        public static bool Quiet;
        public static bool AutoApply;
        public static string InputPath;
        public static string OutputPath;
        public static string Report = "";
        /// <summary>Nhóm phần tử KT- xoá để dựng lại theo phương án mới: "cua", "cau-thang", "san"</summary>
        public static HashSet<string> Rebuild = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static string TakeInputPath() { var p = InputPath; InputPath = null; return p; }
        public static string TakeOutputPath() { var p = OutputPath; OutputPath = null; return p; }

        public static void Reset() { Quiet = false; AutoApply = false; InputPath = null; OutputPath = null; Rebuild.Clear(); }
    }
}

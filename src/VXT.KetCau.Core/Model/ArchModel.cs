using System.Collections.Generic;
using VXT.KetCau.Core.Geometry;

namespace VXT.KetCau.Core.Model
{
    /// <summary>
    /// Dữ liệu hồ sơ kiến trúc đã chuẩn hoá — đầu vào chung cho mọi nguồn (Revit kiến trúc, DWG/DXF).
    /// Đơn vị: mm. Gốc toạ độ theo nguồn.
    /// </summary>
    public class ArchModel
    {
        public string ProjectName { get; set; } = "Công trình";
        public string Source { get; set; } = "";
        public List<ArchGrid> Grids { get; } = new List<ArchGrid>();
        public List<ArchLevel> Levels { get; } = new List<ArchLevel>();
        public List<ArchWall> Walls { get; } = new List<ArchWall>();
        public List<ArchColumn> Columns { get; } = new List<ArchColumn>();
        public List<ArchFloor> Floors { get; } = new List<ArchFloor>();
        public List<ArchOpening> Openings { get; } = new List<ArchOpening>();
        public List<ArchRoom> Rooms { get; } = new List<ArchRoom>();
        public List<ArchStair> Stairs { get; } = new List<ArchStair>();
    }

    public class ArchGrid
    {
        public string Name { get; set; }
        public Seg2 Line { get; set; }
        public override string ToString() => $"Trục {Name} {Line}";
    }

    public class ArchLevel
    {
        public string Name { get; set; }
        /// <summary>Cao độ hoàn thiện (mm).</summary>
        public double Elevation { get; set; }
        /// <summary>Có phải tầng mái / sân thượng.</summary>
        public bool IsRoof { get; set; }
    }

    public class ArchWall
    {
        public string Id { get; set; }
        public Seg2 Line { get; set; }
        public double Thickness { get; set; } = 220;
        public string BaseLevel { get; set; }
        public string TopLevel { get; set; }
        public double BaseElevation { get; set; }
        public double Height { get; set; }
        /// <summary>Tường bao ngoài (true) hay tường ngăn.</summary>
        public bool IsExterior { get; set; }
        /// <summary>Vật liệu: "Gạch đặc", "Gạch rỗng", "BTCT"…</summary>
        public string Material { get; set; } = "Gạch";
    }

    /// <summary>Cột kiến trúc (vị trí cột thể hiện trên mặt bằng kiến trúc).</summary>
    public class ArchColumn
    {
        public string Id { get; set; }
        public P2 Center { get; set; }
        public double B { get; set; } = 220;
        public double H { get; set; } = 220;
        public double RotationDeg { get; set; }
        public string BaseLevel { get; set; }
        public string TopLevel { get; set; }
    }

    public class ArchFloor
    {
        public string Id { get; set; }
        public List<P2> Boundary { get; set; } = new List<P2>();
        public string Level { get; set; }
        public double Elevation { get; set; }
        public double Thickness { get; set; } = 120;
    }

    public class ArchOpening
    {
        public string Id { get; set; }
        public string HostWallId { get; set; }
        public P2 Location { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double SillHeight { get; set; }
        /// <summary>"Cửa đi" / "Cửa sổ".</summary>
        public string Kind { get; set; }
    }

    public class ArchRoom
    {
        public string Name { get; set; }
        public string Level { get; set; }
        public List<P2> Boundary { get; set; } = new List<P2>();
        /// <summary>Công năng để tra hoạt tải TCVN 2737 (phòng ngủ, WC, ban công, mái…).</summary>
        public string Usage { get; set; }
    }

    public class ArchStair
    {
        public string Id { get; set; }
        public List<P2> Boundary { get; set; } = new List<P2>();
        public string BaseLevel { get; set; }
        public string TopLevel { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using VXT.KetCau.Core.Geometry;

namespace VXT.KetCau.Core.Model
{
    /// <summary>
    /// Mô hình kết cấu trung gian — được sinh từ kiến trúc, xuất sang ETABS/SAP2000,
    /// nhận nội lực trả về, rồi đưa vào thiết kế/cấu tạo thép và dựng trong Revit.
    /// Đơn vị: mm, kN, kN·m, MPa.
    /// </summary>
    public class StructModel
    {
        public string ProjectName { get; set; } = "Công trình";
        public DesignSettings Settings { get; set; } = new DesignSettings();
        public List<Story> Stories { get; } = new List<Story>();
        public List<GridLine> Grids { get; } = new List<GridLine>();
        public List<ConcreteSection> Sections { get; } = new List<ConcreteSection>();
        public List<SlabSection> SlabSections { get; } = new List<SlabSection>();
        public List<WallSection> WallSections { get; } = new List<WallSection>();
        public List<ColumnElem> Columns { get; } = new List<ColumnElem>();
        public List<BeamElem> Beams { get; } = new List<BeamElem>();
        public List<SlabElem> Slabs { get; } = new List<SlabElem>();
        public List<WallElem> Walls { get; } = new List<WallElem>();
        public List<FootingElem> Footings { get; } = new List<FootingElem>();
        public List<LoadPattern> LoadPatterns { get; } = new List<LoadPattern>();
        public List<LoadCombo> Combos { get; } = new List<LoadCombo>();
        public List<LineLoad> LineLoads { get; } = new List<LineLoad>();
        public List<AreaLoad> AreaLoads { get; } = new List<AreaLoad>();

        public Story StoryByName(string n) => Stories.FirstOrDefault(s => s.Name == n);
        public ConcreteSection Section(string n) => Sections.FirstOrDefault(s => s.Name == n);
        public SlabSection SlabSection(string n) => SlabSections.FirstOrDefault(s => s.Name == n);

        public ConcreteSection GetOrAddSection(string prefix, double b, double h)
        {
            var name = $"{prefix}{b:0}x{h:0}";
            var s = Section(name);
            if (s == null) { s = new ConcreteSection { Name = name, B = b, H = h, Kind = prefix.StartsWith("C") ? SectionKind.Column : SectionKind.Beam }; Sections.Add(s); }
            return s;
        }

        public SlabSection GetOrAddSlab(double t)
        {
            var name = $"S{t:0}";
            var s = SlabSection(name);
            if (s == null) { s = new SlabSection { Name = name, Thickness = t }; SlabSections.Add(s); }
            return s;
        }
    }

    /// <summary>Thông số thiết kế chung của dự án.</summary>
    public class DesignSettings
    {
        public string Concrete { get; set; } = "B20";
        public string MainSteel { get; set; } = "CB400-V";
        public string StirrupSteel { get; set; } = "CB240-T";
        /// <summary>Lớp bê tông bảo vệ (mm).</summary>
        public double CoverBeam { get; set; } = 25;
        public double CoverColumn { get; set; } = 25;
        public double CoverSlab { get; set; } = 15;
        public double CoverFooting { get; set; } = 40;
        /// <summary>Cấp động đất (0 = không xét). Gia tốc nền agR/g theo TCVN 9386.</summary>
        public double SeismicAgR { get; set; } = 0;
        /// <summary>Vùng gió TCVN 2737:2023 (I-A … V-B). W0 (daN/m²) theo vùng.</summary>
        public string WindZone { get; set; } = "II-A";
        /// <summary>Áp lực gió W0 (kN/m²) tra theo vùng gió TCVN 2737:2023 — 0 = không tính gió.</summary>
        public double WindW0 { get; set; } = 0;
        public string Terrain { get; set; } = "B";
        /// <summary>Loại nền đất động đất (A–E) theo TCVN 9386.</summary>
        public string SeismicGround { get; set; } = "C";
        /// <summary>Hệ số ứng xử q (khung BTCT cấp dẻo trung bình ≈ 3.9).</summary>
        public double SeismicQ { get; set; } = 3.9;
        /// <summary>Đường kính ưu tiên thép dọc dầm (mm).</summary>
        public int[] BeamBarDiameters { get; set; } = { 14, 16, 18, 20, 22, 25 };
        public int[] ColumnBarDiameters { get; set; } = { 16, 18, 20, 22, 25, 28 };
        public int[] SlabBarDiameters { get; set; } = { 8, 10, 12 };
        public int[] StirrupDiameters { get; set; } = { 6, 8, 10 };
        /// <summary>Chiều dài cây thép thương phẩm (mm) — dùng tính nối chồng.</summary>
        public double StockBarLength { get; set; } = 11700;
        /// <summary>Ký hiệu đường kính: "Ø" mặc định, có thể đổi "φ".</summary>
        public string DiaSymbol { get; set; } = "Ø";
        /// <summary>Đánh số thép liên tục toàn bản vẽ (false = mỗi cấu kiện đánh từ 1).</summary>
        public bool ContinuousMarking { get; set; } = false;
        /// <summary>Hệ số độ tin cậy tầm quan trọng γn (TCVN 2737).</summary>
        public double ImportanceFactor { get; set; } = 1.0;
    }

    public class Story
    {
        public string Name { get; set; }
        /// <summary>Cao độ kết cấu đỉnh sàn tầng (mm).</summary>
        public double Elevation { get; set; }
        public double Height { get; set; }
        public bool IsRoof { get; set; }
        public override string ToString() => $"{Name} +{Elevation / 1000:0.000}";
    }

    public class GridLine
    {
        public string Name { get; set; }
        public Seg2 Line { get; set; }
        /// <summary>true: trục theo phương X (đường song song trục X) — thường đặt tên chữ.</summary>
        public bool IsXDir { get; set; }
    }

    public enum SectionKind { Beam, Column }

    public class ConcreteSection
    {
        public string Name { get; set; }
        public SectionKind Kind { get; set; }
        /// <summary>Bề rộng (mm).</summary>
        public double B { get; set; }
        /// <summary>Chiều cao (mm).</summary>
        public double H { get; set; }
        public string Concrete { get; set; }
        public override string ToString() => Name;
    }

    public class SlabSection
    {
        public string Name { get; set; }
        public double Thickness { get; set; }
        public string Concrete { get; set; }
    }

    public class WallSection
    {
        public string Name { get; set; }
        public double Thickness { get; set; }
        public string Concrete { get; set; }
    }

    public abstract class StructElem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        /// <summary>Tên hiển thị trên bản vẽ (C1, D2-8, S3…).</summary>
        public string Label { get; set; }
        /// <summary>Tên đối tượng trong ETABS/SAP2000 sau khi xuất.</summary>
        public string CsiName { get; set; }
        /// <summary>UniqueId phần tử Revit sau khi dựng.</summary>
        public string RevitId { get; set; }
    }

    public class ColumnElem : StructElem
    {
        public P2 Location { get; set; }
        public string BaseStory { get; set; }
        public string TopStory { get; set; }
        public double BaseZ { get; set; }
        public double TopZ { get; set; }
        public string Section { get; set; }
        public double RotationDeg { get; set; }
        public double Length => TopZ - BaseZ;
        public ColumnForces Forces { get; set; }
        public Design.ColumnDesignResult Design { get; set; }
    }

    public class BeamElem : StructElem
    {
        public P2 Start { get; set; }
        public P2 End { get; set; }
        public string Story { get; set; }
        /// <summary>Cao độ đỉnh dầm (mm).</summary>
        public double TopZ { get; set; }
        public string Section { get; set; }
        /// <summary>Lệch tâm ngang so với trục (mm) — dầm biên lệch theo mép tường.</summary>
        public double OffsetY { get; set; }
        public bool IsSecondary { get; set; }
        public double Length => Start.DistanceTo(End);
        public BeamForces Forces { get; set; }
        public Design.BeamDesignResult Design { get; set; }
    }

    public class SlabElem : StructElem
    {
        public List<P2> Boundary { get; set; } = new List<P2>();
        public string Story { get; set; }
        public double TopZ { get; set; }
        public string Section { get; set; }
        /// <summary>Hoạt tải tiêu chuẩn (kN/m²) theo công năng.</summary>
        public double LiveLoad { get; set; }
        /// <summary>Tĩnh tải hoàn thiện (kN/m²), không gồm trọng lượng bản thân.</summary>
        public double SuperDead { get; set; }
        public string Usage { get; set; }
        public SlabForces Forces { get; set; }
        public Design.SlabDesignResult Design { get; set; }
    }

    public class WallElem : StructElem
    {
        public Seg2 Line { get; set; }
        public string BaseStory { get; set; }
        public string TopStory { get; set; }
        public double BaseZ { get; set; }
        public double TopZ { get; set; }
        public string Section { get; set; }
    }

    public class FootingElem : StructElem
    {
        public P2 Center { get; set; }
        public string ColumnId { get; set; }
        public double B { get; set; }
        public double L { get; set; }
        public double H { get; set; }
        public double BottomZ { get; set; }
        /// <summary>"Đơn", "Băng", "Cọc".</summary>
        public string Kind { get; set; } = "Đơn";
    }

    public enum LoadType { Dead, SuperDead, Live, LiveRoof, Wind, Seismic }

    public class LoadPattern
    {
        public string Name { get; set; }
        public LoadType Type { get; set; }
        public double SelfWeightMultiplier { get; set; }
    }

    public class LoadCombo
    {
        public string Name { get; set; }
        /// <summary>true: tổ hợp bao (envelope).</summary>
        public bool IsEnvelope { get; set; }
        public bool IsServiceability { get; set; }
        public List<(string Case, double Factor)> Items { get; } = new List<(string, double)>();
    }

    /// <summary>Tải phân bố đều trên dầm (kN/m) — tường xây trên dầm…</summary>
    public class LineLoad
    {
        public string BeamId { get; set; }
        public string Pattern { get; set; }
        public double Value { get; set; }
        public string Note { get; set; }
    }

    public class AreaLoad
    {
        public string SlabId { get; set; }
        public string Pattern { get; set; }
        public double Value { get; set; }
    }

    // ======= Nội lực nhận từ phần mềm phân tích =======

    /// <summary>Nội lực tại một trạm của dầm (một tổ hợp).</summary>
    public class BeamStation
    {
        /// <summary>Vị trí tính từ đầu dầm (mm).</summary>
        public double X { get; set; }
        public string Combo { get; set; }
        /// <summary>Mô men M3 (kN·m), dương = căng thớ dưới.</summary>
        public double M { get; set; }
        public double V { get; set; }
        public double T { get; set; }
        public double N { get; set; }
    }

    public class BeamForces
    {
        public List<BeamStation> Stations { get; } = new List<BeamStation>();
        /// <summary>Bao mô men dương lớn nhất / âm lớn nhất tại 3 vùng: gối trái, nhịp, gối phải.</summary>
        public double MnegLeft { get; set; }
        public double MposSpan { get; set; }
        public double MnegRight { get; set; }
        public double VmaxLeft { get; set; }
        public double VmaxRight { get; set; }
        public double Tmax { get; set; }

        /// <summary>Tính bao nội lực 3 vùng từ các trạm. Vùng gối = L/4 mỗi đầu.</summary>
        public void Envelope(double length)
        {
            if (Stations.Count == 0) return;
            double z = length / 4;
            var left = Stations.Where(s => s.X <= z).ToList();
            var right = Stations.Where(s => s.X >= length - z).ToList();
            var mid = Stations.Where(s => s.X > z * 0.5 && s.X < length - z * 0.5).ToList();
            if (left.Count == 0) left = Stations; if (right.Count == 0) right = Stations; if (mid.Count == 0) mid = Stations;
            MnegLeft = Math.Min(0, left.Min(s => s.M));
            MnegRight = Math.Min(0, right.Min(s => s.M));
            MposSpan = Math.Max(0, mid.Max(s => s.M));
            VmaxLeft = left.Max(s => Math.Abs(s.V));
            VmaxRight = right.Max(s => Math.Abs(s.V));
            Tmax = Stations.Max(s => Math.Abs(s.T));
        }
    }

    public class ColumnForceSet
    {
        public string Combo { get; set; }
        /// <summary>Vị trí: "Chân" / "Đỉnh".</summary>
        public string Location { get; set; }
        /// <summary>Lực dọc (kN), nén dương.</summary>
        public double N { get; set; }
        public double M2 { get; set; }
        public double M3 { get; set; }
        public double V2 { get; set; }
        public double V3 { get; set; }
    }

    public class ColumnForces
    {
        public List<ColumnForceSet> Sets { get; } = new List<ColumnForceSet>();
    }

    /// <summary>Mô men sàn trên 1 m dải (kN·m/m).</summary>
    public class SlabForces
    {
        public double M1Span { get; set; }
        public double M2Span { get; set; }
        public double M1Support { get; set; }
        public double M2Support { get; set; }
    }
}

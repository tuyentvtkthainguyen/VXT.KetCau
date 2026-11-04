using System.Collections.Generic;
using VXT.KetCau.Core.Drawing;

namespace VXT.KetCau.Core.Documentation
{
    /// <summary>Thông tin ghi trên khung tên và lựa chọn khi dựng hồ sơ.</summary>
    public class DocOptions
    {
        public string Company { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string Owner { get; set; } = "";
        public string Address { get; set; } = "";
        public string Director { get; set; } = "";
        public string Designer { get; set; } = "";
        public string Checker { get; set; } = "";
        public string Date { get; set; } = "";
        /// <summary>Giai đoạn thiết kế ghi trên khung tên, vd "Thiết kế thi công".</summary>
        public string Stage { get; set; } = "Thiết kế thi công";
        /// <summary>A4, A3, A2, A1, A0 — tự nâng khổ nếu hình quá lớn.</summary>
        public string Paper { get; set; } = "A3";
        /// <summary>Tỉ lệ mặt bằng, mặt đứng, mặt cắt (100 = 1/100).</summary>
        public double PlanScale { get; set; } = 100;
        /// <summary>Tỉ lệ chi tiết (thép, thang, cửa).</summary>
        public double DetailScale { get; set; } = 50;
        public bool DrawArchitecture { get; set; } = true;
        public bool DrawStructure { get; set; } = true;
        public bool CreateLayouts { get; set; } = true;
        /// <summary>Chiều cao tường chắn mái trên mặt đứng, mặt cắt (mm).</summary>
        public double ParapetHeight { get; set; } = 900;
        /// <summary>Điểm gốc từng tầng khi quét mặt bằng trong một bản vẽ — để vẽ lại lớp nhận diện đúng chỗ.</summary>
        public List<PlanBase> PlanBases { get; set; } = new List<PlanBase>();
    }

    public class PlanBase
    {
        public string Level { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    /// <summary>Một hình đã dựng (mặt bằng, mặt đứng, chi tiết dầm…), chờ xếp vào tờ.</summary>
    public class DrawingBlock
    {
        /// <summary>"KT" hoặc "KC".</summary>
        public string Discipline { get; set; }
        /// <summary>Các hình cùng nhóm và cùng tỉ lệ được xếp chung tờ; đây cũng là tên tờ.</summary>
        public string Group { get; set; }
        public double Scale { get; set; }
        public string ScaleText { get; set; }
        public RecordingDraw2D Drawing { get; set; }
    }

    /// <summary>Một tờ bản vẽ đã xếp xong.</summary>
    public class SheetInfo
    {
        public string Number { get; set; }
        public string Title { get; set; }
        public string Discipline { get; set; }
        public string Paper { get; set; }
        public double PaperW { get; set; }
        public double PaperH { get; set; }
        public double Scale { get; set; }
        public string ScaleText { get; set; }
        /// <summary>Góc dưới trái khung giấy trong không gian mô hình (mm), tính từ gốc bộ hồ sơ.</summary>
        public double X { get; set; }
        public double Y { get; set; }
        public double ModelW => PaperW * Scale;
        public double ModelH => PaperH * Scale;
        public List<(DrawingBlock Block, double X, double Y)> Items { get; } = new List<(DrawingBlock, double, double)>();
    }
}

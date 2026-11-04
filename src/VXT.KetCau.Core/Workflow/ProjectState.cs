using VXT.KetCau.Core.Documentation;
using VXT.KetCau.Core.Framing;
using VXT.KetCau.Core.Model;

namespace VXT.KetCau.Core.Workflow
{
    /// <summary>Thông số làm việc của dự án (lưu kèm file .rvt / .dwg dạng *.vxtkc.json).</summary>
    public class ProjectState
    {
        public DesignSettings Settings { get; set; } = new DesignSettings();
        public FramingOptions Framing { get; set; } = new FramingOptions();
        /// <summary>"ETABS" / "SAP2000" / "Sơ bộ".</summary>
        public string Engine { get; set; } = "ETABS";
        public bool AttachToRunning { get; set; } = true;
        public string EnginePath { get; set; } = "";
        public string OdaConverterPath { get; set; } = "";
        public StructModel Model { get; set; }
        public ArchModel Arch { get; set; }
        /// <summary>Thông tin khung tên, khổ giấy, tỉ lệ khi lập hồ sơ.</summary>
        public DocOptions Doc { get; set; } = new DocOptions();
    }
}

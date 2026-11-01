using System;
using System.Collections.Generic;
using VXT.KetCau.Core.Model;

namespace VXT.KetCau.Core.Analysis
{
    /// <summary>Phần mềm phân tích kết cấu (ETABS, SAP2000…).</summary>
    public interface IAnalysisEngine : IDisposable
    {
        string Name { get; }
        List<string> Log { get; }
        /// <summary>Kết nối: gắn vào phiên đang mở, hoặc khởi động chương trình mới.</summary>
        void Connect(bool attachToRunning, string programPath = null);
        /// <summary>Tạo mô hình mới từ StructModel (vật liệu, tiết diện, phần tử, tải, tổ hợp) và lưu file.</summary>
        void BuildModel(StructModel m, string savePath);
        void RunAnalysis();
        /// <summary>Đọc nội lực các tổ hợp về StructModel (BeamForces, ColumnForces, SlabForces).</summary>
        void ReadResults(StructModel m, IEnumerable<string> combos = null);
    }
}

namespace KetCauVXT.Analysis;

/// <summary>Thông tin nguồn của bộ nội lực NHẬP từ phần mềm khác (ETABS, SAP2000, SAFE, bảng CSV tự do).
/// Khi <see cref="AnalysisResults.External"/> khác null, thiết kế – biểu đồ – bản vẽ – thuyết minh dùng bộ nội lực này
/// đúng như kết quả của chương trình; mô hình phân tích (sơ đồ, tải trọng, tổ hợp) là trách nhiệm của người lập mô hình.
/// Quy ước sau khi nhập: kN, m, kN.m; nội lực thanh theo trục địa phương 1-2-3 của chương trình (giống ETABS/SAP2000):
/// P kéo dương; V2, V3; T; M2; M3 dương khi căng thớ phía −2 (dầm: căng dưới); trạm đo từ đầu I của thanh trong chương trình.</summary>
public class ExternalResultInfo
{
    /// <summary>"ETABS", "SAP2000", "SAFE", "ETABS + SAFE", "Phần mềm khác"</summary>
    public string Software { get; set; } = "ETABS";
    /// <summary>Các file đã đọc (đường dẫn đầy đủ) và ngày sửa file.</summary>
    public List<ExternalFileInfo> Files { get; set; } = new();
    public DateTime ImportedAt { get; set; } = DateTime.Now;
    /// <summary>Đơn vị nhận diện trong file (vd. "Tonf, m") – đã đổi về kN, m.</summary>
    public string SourceUnits { get; set; } = "";
    /// <summary>Trường hợp/tổ hợp dùng thiết kế TTGH I; rỗng = dùng tổ hợp ULS của dự án (tổ hợp từ các trường hợp tải nhập về).</summary>
    public List<string> UlsCases { get; set; } = new();
    /// <summary>Trường hợp/tổ hợp TTGH II (võng, nứt, móng theo tiêu chuẩn); rỗng = theo dự án.</summary>
    public List<string> SlsCases { get; set; } = new();
    /// <summary>Trường hợp/tổ hợp TTGH I có mô men sàn nhập (SAFE) – dùng thiết kế sàn khi tổ hợp thiết kế khung khác tên.</summary>
    public List<string> SlabDesignCases { get; set; } = new();
    /// <summary>Tên tổ hợp bao TTGH I sinh khi nhập (vd. "BAO-ETABS") – mặc định vẽ biểu đồ.</summary>
    public string EnvelopeName { get; set; } = "";
    public int MatchedFrames { get; set; }
    public int UnmatchedFrames { get; set; }
    /// <summary>Thanh không có trong file nhập, đã lấy nội lực của chương trình (cùng tên tổ hợp).</summary>
    public int FilledFromOwn { get; set; }
    public int MatchedNodes { get; set; }
    public int SlabPanels { get; set; }
    /// <summary>Bảng đã dùng: tên bảng – số dòng – file.</summary>
    public List<ExternalTableUse> Tables { get; set; } = new();
    /// <summary>Ghi chú / cảnh báo khi nhập (cũng chép vào AnalysisResults.Warnings).</summary>
    public List<string> Notes { get; set; } = new();
    /// <summary>Kiểm tra chọc thủng đọc từ SAFE/ETABS (tỷ số ứng suất).</summary>
    public List<ExternalPunching> Punching { get; set; } = new();
    /// <summary>Áp lực đất nền lớn nhất theo trường hợp (SAFE "Soil Pressures"), kN/m², trị tuyệt đối.</summary>
    public List<ExternalSoilPressure> SoilPressures { get; set; } = new();
    /// <summary>Chuyển vị lệch tầng (ETABS "Story Drifts").</summary>
    public List<ExternalDrift> Drifts { get; set; } = new();

    public string FileList => string.Join("; ", Files.Select(f => Path.GetFileName(f.Path)));
    public string Summary => $"{Software} ({FileList})";
}

public class ExternalFileInfo
{
    public string Path { get; set; } = "";
    public DateTime Modified { get; set; }
}

public class ExternalTableUse
{
    public string Table { get; set; } = "";
    public string File { get; set; } = "";
    public int Rows { get; set; }
    public string Units { get; set; } = "";
}

public class ExternalPunching
{
    public string Point { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public string Case { get; set; } = "";
    public double Ratio { get; set; }
    public string Status { get; set; } = "";
}

public class ExternalSoilPressure
{
    public string Case { get; set; } = "";
    public double MaxPressure { get; set; }
    public string Point { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
}

public class ExternalDrift
{
    public string Story { get; set; } = "";
    public string Case { get; set; } = "";
    public string Direction { get; set; } = "";
    public double Drift { get; set; }
}

/// <summary>Mô men thiết kế một ô sàn theo phương tổng thể (kN.m/m, dương = căng dưới, âm = căng trên):
/// X = thép đặt theo phương X; NaN = không có số liệu (giữ phương pháp hệ số).</summary>
public class SlabMomentSet
{
    public double MxSpan { get; set; } = double.NaN;
    public double MxSupport { get; set; } = double.NaN;
    public double MySpan { get; set; } = double.NaN;
    public double MySupport { get; set; } = double.NaN;
    /// <summary>Số điểm/trạm số liệu rơi vào ô sàn (tổng, theo phương X, theo phương Y).</summary>
    public int Points { get; set; }
    public int PointsX { get; set; }
    public int PointsY { get; set; }
    public string Source { get; set; } = "";

    /// <summary>Gộp một giá trị mô men phương X hoặc Y: nhịp lấy lớn nhất dương, gối lấy nhỏ nhất âm.</summary>
    public void Add(bool alongX, double m)
    {
        if (!double.IsFinite(m)) return;
        Points++;
        if (alongX) PointsX++; else PointsY++;
        if (alongX)
        {
            if (m > 0) MxSpan = double.IsNaN(MxSpan) ? m : Math.Max(MxSpan, m);
            else MxSupport = double.IsNaN(MxSupport) ? m : Math.Min(MxSupport, m);
        }
        else
        {
            if (m > 0) MySpan = double.IsNaN(MySpan) ? m : Math.Max(MySpan, m);
            else MySupport = double.IsNaN(MySupport) ? m : Math.Min(MySupport, m);
        }
    }
}

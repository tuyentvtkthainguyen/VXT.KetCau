using KetCauVXT.Model;

namespace KetCauVXT.Analysis;

/// <summary>Một dạng dao động riêng. Dạng dao động chuẩn hoá theo khối lượng đơn vị: φᵀ·M·φ = 1 (t·m²).
/// Hệ số tham gia Γ = φᵀ·M·ι (ι = véc tơ dịch chuyển cứng đơn vị theo phương); khối lượng hữu hiệu Meff = Γ² (t).</summary>
public class ModeResult
{
    /// <summary>Số thứ tự dạng (1 = chu kỳ dài nhất).</summary>
    public int Index { get; set; }
    /// <summary>Tần số vòng ω (rad/s).</summary>
    public double Omega { get; set; }
    /// <summary>Chu kỳ T = 2π/ω (s).</summary>
    public double Period { get; set; }
    /// <summary>Tần số f = 1/T (Hz).</summary>
    public double Frequency { get; set; }
    /// <summary>Hệ số tham gia Γ theo UX, UY, UZ, RZ.</summary>
    public double[] Gamma { get; set; } = new double[4];
    /// <summary>Tỷ lệ khối lượng hữu hiệu UX, UY, UZ, RZ (0..1).</summary>
    public double[] Ratio { get; set; } = new double[4];
    /// <summary>Tỷ lệ khối lượng hữu hiệu cộng dồn UX, UY, UZ, RZ (0..1).</summary>
    public double[] Cumulative { get; set; } = new double[4];
    /// <summary>Phương chủ đạo: "X", "Y", "Xoắn", "Z", "Cục bộ".</summary>
    public string Dominant { get; set; } = "";
    /// <summary>Chuyển vị nút của dạng dao động (chuẩn hoá khối lượng đơn vị) – UX, UY, UZ, RX, RY, RZ.</summary>
    public Dictionary<int, double[]> Disp { get; set; } = new();
    /// <summary>Chuyển vị tịnh tiến (UX, UY, UZ) các điểm trung gian của thanh chia đoạn (dầm cong, thanh vát), theo thứ tự từ I → J (không gồm hai đầu).</summary>
    public Dictionary<int, double[][]> FrameInner { get; set; } = new();
    /// <summary>Hệ số nhân để chuyển vị tịnh tiến lớn nhất = 1 (hiển thị).</summary>
    public double DisplayScale { get; set; } = 1.0;
}

/// <summary>Khối lượng tầng, tâm khối lượng, tâm cứng.</summary>
public class StoryMass
{
    public string Story { get; set; } = "";
    public double Elevation { get; set; }
    /// <summary>Khối lượng tịnh tiến (t).</summary>
    public double Mass { get; set; }
    /// <summary>Trọng lượng tương ứng (kN) = M·g.</summary>
    public double Weight => Mass * ModalResult.G;
    /// <summary>Mô men quán tính khối lượng quanh trục đứng qua tâm khối lượng (t·m²).</summary>
    public double Inertia { get; set; }
    public double Xm { get; set; }
    public double Ym { get; set; }
    /// <summary>Tâm cứng (m); NaN = không tính được (không có sàn cứng).</summary>
    public double Xr { get; set; } = double.NaN;
    public double Yr { get; set; } = double.NaN;
    /// <summary>Kích thước sàn theo X, Y (m) – dùng cho độ lệch tâm ngẫu nhiên.</summary>
    public double Lx { get; set; }
    public double Ly { get; set; }
}

/// <summary>Kết quả phổ phản ứng một dạng theo một phương.</summary>
public class RsModeRow
{
    public int Mode { get; set; }
    public double Period { get; set; }
    /// <summary>Sd(T)/g.</summary>
    public double Sd { get; set; }
    /// <summary>Khối lượng hữu hiệu (t).</summary>
    public double Meff { get; set; }
    /// <summary>Lực cắt đáy của dạng (kN) = Meff·Sd·g.</summary>
    public double BaseShear { get; set; }
}

/// <summary>Kết quả phổ phản ứng cho một trường hợp tải động đất (phương X hoặc Y).</summary>
public class RsCase
{
    public string Pattern { get; set; } = "";
    /// <summary>'X' hoặc 'Y'.</summary>
    public char Dir { get; set; } = 'X';
    public string Combination { get; set; } = "CQC";
    public List<RsModeRow> Rows { get; } = new();
    /// <summary>Lực cắt đáy tổ hợp từ các dạng (kN) – trước khi hiệu chỉnh.</summary>
    public double BaseShear { get; set; }
    /// <summary>Lực cắt đáy theo phương pháp tĩnh lực ngang tương đương dùng T theo dao động riêng (kN).</summary>
    public double StaticBaseShear { get; set; }
    /// <summary>Chu kỳ dùng tính lực cắt đáy tĩnh (dạng có tỷ lệ khối lượng lớn nhất theo phương), s.</summary>
    public double StaticPeriod { get; set; }
    /// <summary>Hệ số hiệu chỉnh đã nhân vào kết quả phổ phản ứng (1 = không hiệu chỉnh).</summary>
    public double ScaleFactor { get; set; } = 1.0;
    /// <summary>Tổng tỷ lệ khối lượng hữu hiệu các dạng đã dùng theo phương.</summary>
    public double MassRatio { get; set; }
    /// <summary>Mô men xoắn ngẫu nhiên theo tầng (kN.m).</summary>
    public List<(string story, double force, double length, double moment)> Torsion { get; } = new();
}

/// <summary>Toàn bộ kết quả phân tích dao động riêng (và phổ phản ứng nếu dùng).</summary>
public class ModalResult
{
    /// <summary>Gia tốc trọng trường (m/s²) – quy đổi kN → t.</summary>
    public const double G = 9.81;

    public List<ModeResult> Modes { get; } = new();
    public List<StoryMass> Stories { get; } = new();
    /// <summary>Tổng khối lượng tham gia theo UX, UY, UZ (t) và mô men quán tính khối lượng RZ (t·m²) – chỉ tính bậc tự do không bị giữ.</summary>
    public double[] TotalMass { get; set; } = new double[4];
    /// <summary>Khối lượng tại nút gối (bị giữ, không tham gia dao động), t.</summary>
    public double BaseMass { get; set; }
    /// <summary>Tâm khối lượng toàn công trình (m).</summary>
    public double Xc { get; set; }
    public double Yc { get; set; }
    public string MassSource { get; set; } = "";
    public bool Diaphragm { get; set; }
    public bool VerticalMass { get; set; }
    public string Solver { get; set; } = "";
    /// <summary>Số bậc tự do có khối lượng (sau khử tĩnh các bậc tự do không khối lượng).</summary>
    public int DynamicDofs { get; set; }
    public int RequestedModes { get; set; }
    public int Iterations { get; set; }
    public List<string> Notes { get; } = new();
    public List<RsCase> Spectrum { get; } = new();
    /// <summary>Chu kỳ T1 theo công thức Ct·H^0,75 (s) để so sánh.</summary>
    public double T1Formula { get; set; }
    /// <summary>Chiều cao công trình H (m).</summary>
    public double Height { get; set; }

    public ModeResult First => Modes.Count > 0 ? Modes[0] : null;

    /// <summary>Dạng có tỷ lệ khối lượng lớn nhất theo phương d (0 = X, 1 = Y, 2 = Z, 3 = RZ).</summary>
    public ModeResult Dominant(int d) => Modes.Count == 0 ? null : Modes.OrderByDescending(m => m.Ratio[d]).First();

    /// <summary>Tổng tỷ lệ khối lượng của các dạng đã tính theo phương d.</summary>
    public double SumRatio(int d) => Modes.Count == 0 ? 0 : Modes[^1].Cumulative[d];

    /// <summary>Số dạng cần để tỷ lệ khối lượng cộng dồn theo phương d ≥ ratio; -1 = chưa đạt.</summary>
    public int ModesToReach(int d, double ratio = 0.9)
    {
        foreach (var m in Modes) if (m.Cumulative[d] >= ratio - 1e-9) return m.Index;
        return -1;
    }
}

public partial class AnalysisResults
{
    // ---- [Dao động riêng – Analysis/FrameAnalysis.Modal.cs] ----
    /// <summary>Kết quả dao động riêng; null = không chạy (tắt hoặc kết quả nhập từ phần mềm khác).</summary>
    public ModalResult Modal { get; set; }
    // ---- [hết] ----
}

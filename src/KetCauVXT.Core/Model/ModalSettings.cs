namespace KetCauVXT.Model;

/// <summary>Phương pháp tính tải động đất (SeismicSettings.Method).</summary>
public static class SeismicMethods
{
    /// <summary>Phương pháp phân tích tĩnh lực ngang tương đương (TCVN 9386, mục 4.3.3.2 – số điều theo bản 2012) – MẶC ĐỊNH.</summary>
    public const string Static = "Tĩnh lực ngang tương đương";
    /// <summary>Phương pháp phân tích phổ phản ứng dạng dao động (TCVN 9386, mục 4.3.3.3 – số điều theo bản 2012).</summary>
    public const string ResponseSpectrum = "Phổ phản ứng dạng dao động";
    public static readonly string[] All = { Static, ResponseSpectrum };
}

/// <summary>Một dòng của "Nguồn khối lượng" (tương tự Mass Source của ETABS): trường hợp tải × hệ số.
/// Khối lượng (t) = hệ số × tải trọng theo phương trọng lực (kN) / g.</summary>
public class MassSourceItem
{
    public string Pattern { get; set; } = "";
    public double Factor { get; set; } = 1.0;
    public MassSourceItem() { }
    public MassSourceItem(string pattern, double factor) { Pattern = pattern; Factor = factor; }
}

/// <summary>Thiết lập phân tích dao động riêng (modal) và phổ phản ứng (Analysis/FrameAnalysis.Modal.cs).
/// Giá trị mặc định:
/// – Nguồn khối lượng: tĩnh tải (TT, HTHT, tường – mọi trường hợp loại Dead/SuperDead) × 1,0 + hoạt tải × ψE.
///   ψE = φ·ψ2 (TCVN 9386 mục 3.2.4 và 4.2.4 – số điều theo bản 2012, cùng cơ sở EN 1998-1 4.2.4 + EN 1990 bảng A1.1):
///   nhà ở ψ2 = 0,3; φ = 0,8 cho các tầng sử dụng đồng thời → ψE = 0,24; mái (hoạt tải mái, loại H) ψ2 = 0 → 0.
///   Lưu ý: phương pháp tĩnh lực ngang tương đương hiện dùng SeismicSettings.PsiLive (mặc định 0,3) khi tính trọng lượng tầng.
/// – Sàn tuyệt đối cứng: dùng chung tuỳ chọn Project.UseRigidDiaphragm (mặc định bật) của phân tích tĩnh.
/// – Số dạng dao động: 0 = tự động max(12, 3 × số tầng), không vượt số bậc tự do động.
/// – Tổ hợp dạng dao động: CQC, tỷ số cản ζ = 5 % (Der Kiureghian 1981); có thể chọn SRSS (TCVN 9386 mục 4.3.3.3.2).
/// – Độ lệch tâm ngẫu nhiên ±0,05·L (TCVN 9386 mục 4.3.2 và 4.3.3.3.3): mô men xoắn tầng Mi = 0,05·Li·Fi.</summary>
public class ModalSettings
{
    /// <summary>Chạy phân tích dao động riêng cùng phân tích tĩnh.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Số dạng dao động yêu cầu; 0 = tự động max(12, 3 × số tầng).</summary>
    public int NumModes { get; set; } = 0;
    /// <summary>Sai số hội tụ trị riêng (phương pháp lặp không gian con).</summary>
    public double Tolerance { get; set; } = 1e-8;
    /// <summary>"Tự động" (khử tĩnh + QL khi số bậc tự do động ≤ 800, ngược lại lặp không gian con) | "Khử tĩnh + QL" | "Lặp không gian con".</summary>
    public string Solver { get; set; } = SolverAuto;
    public const string SolverAuto = "Tự động", SolverDense = "Khử tĩnh + QL", SolverSubspace = "Lặp không gian con";

    /// <summary>true = nguồn khối lượng mặc định theo loại trường hợp tải (bỏ qua MassSource).</summary>
    public bool UseDefaultMassSource { get; set; } = true;
    /// <summary>Nguồn khối lượng do người dùng khai báo (khi UseDefaultMassSource = false).</summary>
    public List<MassSourceItem> MassSource { get; set; } = new();
    /// <summary>Hệ số khối lượng hoạt tải sàn mặc định ψE = φ·ψ2 = 0,8 × 0,3.</summary>
    public double LiveMassFactor { get; set; } = 0.24;
    /// <summary>Hệ số khối lượng hoạt tải mái mặc định (ψ2 = 0 với mái).</summary>
    public double RoofLiveMassFactor { get; set; } = 0.0;
    /// <summary>Kể khối lượng theo phương đứng (UZ). Mặc định không (như ETABS "lateral mass only").</summary>
    public bool IncludeVerticalMass { get; set; }

    /// <summary>"CQC" | "SRSS".</summary>
    public string Combination { get; set; } = "CQC";
    /// <summary>Tỷ số cản dùng cho hệ số tương quan CQC.</summary>
    public double Damping { get; set; } = 0.05;
    /// <summary>Kể độ lệch tâm ngẫu nhiên khi phân tích phổ phản ứng.</summary>
    public bool AccidentalTorsion { get; set; } = true;
    /// <summary>Độ lệch tâm ngẫu nhiên tính theo kích thước sàn vuông góc phương động đất.</summary>
    public double Eccentricity { get; set; } = 0.05;
    /// <summary>0 = không hiệu chỉnh; vd 0,85 → nếu lực cắt đáy phổ phản ứng &lt; 0,85 × lực cắt đáy tĩnh (T theo dao động riêng) thì nhân kết quả phổ phản ứng lên.
    /// TCVN 9386 không bắt buộc – tuỳ chọn tham khảo (tương tự ASCE 7 mục 12.9.1.4).</summary>
    public double ScaleToStatic { get; set; } = 0.0;

    /// <summary>Dùng chu kỳ T1 từ phân tích dao động riêng (dạng có tỷ lệ khối lượng lớn nhất theo phương) cho phương pháp tĩnh lực ngang tương đương.</summary>
    public bool UseModalT1 { get; set; }
    /// <summary>Giới hạn T1 dao động ≤ hệ số × Ct·H^0,75; 0 = không giới hạn.</summary>
    public double ModalT1Cap { get; set; } = 0.0;

    public ModalSettings Clone()
    {
        var c = (ModalSettings)MemberwiseClone();
        c.MassSource = MassSource.Select(m => new MassSourceItem(m.Pattern, m.Factor)).ToList();
        return c;
    }

    /// <summary>Hệ số khối lượng mặc định theo loại trường hợp tải.</summary>
    public double DefaultFactor(LoadType t) => t switch
    {
        LoadType.Dead or LoadType.SuperDead => 1.0,
        LoadType.Live => LiveMassFactor,
        LoadType.RoofLive => RoofLiveMassFactor,
        _ => 0.0
    };

    /// <summary>Nguồn khối lượng hiệu lực (trường hợp tải có trong dự án, hệ số ≠ 0).</summary>
    public List<(LoadPattern pat, double factor)> Effective(Project p)
    {
        var r = new List<(LoadPattern, double)>();
        if (UseDefaultMassSource || MassSource.Count == 0)
        {
            foreach (var pat in p.Patterns)
            {
                double f = DefaultFactor(pat.Type);
                if (f != 0) r.Add((pat, f));
            }
            return r;
        }
        foreach (var g in MassSource.Where(m => m.Factor != 0).GroupBy(m => m.Pattern))
        {
            var pat = p.Pattern(g.Key);
            if (pat != null) r.Add((pat, g.Sum(m => m.Factor)));
        }
        return r;
    }

    /// <summary>Bảng nguồn khối lượng mặc định (để hiện trong hộp thoại cho người dùng sửa).</summary>
    public List<MassSourceItem> DefaultTable(Project p) =>
        p.Patterns.Select(t => new MassSourceItem(t.Name, DefaultFactor(t.Type))).Where(m => m.Factor != 0).ToList();

    /// <summary>Số dạng dao động yêu cầu.</summary>
    public int RequestedModes(int storyCount) => NumModes > 0 ? NumModes : Math.Max(12, 3 * Math.Max(storyCount, 1));

    /// <summary>Mô tả nguồn khối lượng dạng chữ: "1·TT + 1·HTHT + 0.24·HT".</summary>
    public string Describe(Project p)
    {
        var e = Effective(p);
        if (e.Count == 0) return "(trống)";
        return string.Join(" + ", e.Select(x => x.factor.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "·" + x.pat.Name));
    }
}

public partial class Project
{
    // ---- [Dao động riêng – Model/ModalSettings.cs, Analysis/FrameAnalysis.Modal.cs] ----
    /// <summary>Thiết lập phân tích dao động riêng / phổ phản ứng; file cũ không có mục này → mặc định.</summary>
    public ModalSettings Modal { get; set; } = new();
    // ---- [hết] ----
}

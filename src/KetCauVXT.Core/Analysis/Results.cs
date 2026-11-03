using KetCauVXT.Model;

namespace KetCauVXT.Analysis;

/// <summary>Nội lực một thanh tại các mặt cắt. Quy ước giống ETABS/SAP2000 (nội lực trên mặt dương):
/// P kéo dương; V2, V3; T; M2, M3 (M3 dương khi căng thớ phía −2, tức căng dưới với dầm).
/// Đơn vị kN, kN.m. Station tính từ đầu I của thanh gốc (m).</summary>
public class FrameForces
{
    public double[] Stations { get; set; } = Array.Empty<double>();
    /// <summary>[mặt cắt, 6] = P, V2, V3, T, M2, M3</summary>
    public double[][] F { get; set; } = Array.Empty<double[]>();

    public int Count => Stations.Length;
    public double P(int i) => F[i][0];
    public double V2(int i) => F[i][1];
    public double V3(int i) => F[i][2];
    public double T(int i) => F[i][3];
    public double M2(int i) => F[i][4];
    public double M3(int i) => F[i][5];

    public FrameForces CloneScaled(double k)
    {
        var r = new FrameForces { Stations = (double[])Stations.Clone(), F = new double[F.Length][] };
        for (int i = 0; i < F.Length; i++) { r.F[i] = new double[6]; for (int c = 0; c < 6; c++) r.F[i][c] = F[i][c] * k; }
        return r;
    }

    public void AddScaled(FrameForces o, double k)
    {
        for (int i = 0; i < F.Length; i++) for (int c = 0; c < 6; c++) F[i][c] += o.F[i][c] * k;
    }

    /// <summary>Nội lực nội suy tuyến tính tại vị trí x (m).</summary>
    public double[] At(double x)
    {
        if (x <= Stations[0]) return (double[])F[0].Clone();
        for (int i = 1; i < Stations.Length; i++)
            if (x <= Stations[i] + 1e-9)
            {
                double t = (x - Stations[i - 1]) / Math.Max(Stations[i] - Stations[i - 1], 1e-12);
                var r = new double[6];
                for (int c = 0; c < 6; c++) r[c] = F[i - 1][c] + t * (F[i][c] - F[i - 1][c]);
                return r;
            }
        return (double[])F[^1].Clone();
    }
}

/// <summary>Kết quả một trường hợp tải hoặc một tổ hợp tuyến tính.</summary>
public class CaseResult
{
    public string Name { get; set; } = "";
    /// <summary>Chuyển vị nút (UX,UY,UZ,RX,RY,RZ), m và rad</summary>
    public Dictionary<int, double[]> Disp { get; set; } = new();
    /// <summary>Phản lực gối (FX,FY,FZ,MX,MY,MZ), kN, kN.m</summary>
    public Dictionary<int, double[]> Reactions { get; set; } = new();
    public Dictionary<int, FrameForces> Frames { get; set; } = new();

    public CaseResult Scaled(double k, string name)
    {
        var r = new CaseResult { Name = name };
        foreach (var kv in Disp) r.Disp[kv.Key] = kv.Value.Select(v => v * k).ToArray();
        foreach (var kv in Reactions) r.Reactions[kv.Key] = kv.Value.Select(v => v * k).ToArray();
        foreach (var kv in Frames) r.Frames[kv.Key] = kv.Value.CloneScaled(k);
        return r;
    }

    public void AddScaled(CaseResult o, double k)
    {
        foreach (var kv in o.Disp)
        {
            if (!Disp.TryGetValue(kv.Key, out var d)) Disp[kv.Key] = d = new double[6];
            for (int c = 0; c < 6; c++) d[c] += kv.Value[c] * k;
        }
        foreach (var kv in o.Reactions)
        {
            if (!Reactions.TryGetValue(kv.Key, out var d)) Reactions[kv.Key] = d = new double[6];
            for (int c = 0; c < 6; c++) d[c] += kv.Value[c] * k;
        }
        foreach (var kv in o.Frames)
        {
            if (!Frames.TryGetValue(kv.Key, out var f)) Frames[kv.Key] = kv.Value.CloneScaled(k);
            else f.AddScaled(kv.Value, k);
        }
    }
}

/// <summary>Toàn bộ kết quả phân tích: các trường hợp tải và tổ hợp.</summary>
public partial class AnalysisResults   // dao động riêng: Analysis/ModalResults.cs
{
    public Project Project { get; }
    public Dictionary<string, CaseResult> Cases { get; } = new();
    private readonly Dictionary<string, CaseResult> _combos = new();
    public List<string> Warnings { get; } = new();
    public int Equations { get; set; }
    /// <summary>Lực ngang tầng đã sinh (tên trường hợp → tầng → lực kN)</summary>
    public List<StoryLateralForce> LateralForces { get; } = new();

    public AnalysisResults(Project p) { Project = p; }

    // ---- [kết quả nhập từ phần mềm khác – Analysis/ExternalResults.cs, Import/External/ResultImporter.cs] ----
    /// <summary>Thông tin nguồn khi nội lực được NHẬP từ ETABS/SAP2000/SAFE/phần mềm khác; null = kết quả của chương trình.</summary>
    public ExternalResultInfo External { get; set; }
    /// <summary>Tên nguồn kết quả để hiển thị ("Kết cấu VXT", "ETABS", "SAP2000"…).</summary>
    public string Source => External == null ? "Kết cấu VXT" : External.Software;
    /// <summary>Tổ hợp "ngoài" sinh khi nhập kết quả (tổ hợp bao Max/Min của ETABS, bao các tổ hợp TTGH I nhập về).
    /// Ưu tiên hơn Project.Combinations cùng tên; không lưu vào dự án.</summary>
    public List<LoadCombination> ExternalCombos { get; } = new();
    /// <summary>Mô men dải sàn nhập từ SAFE/ETABS (Id ô sàn → tên trường hợp/tổ hợp → mô men kN.m/m) – thay phương pháp hệ số khi thiết kế sàn.</summary>
    public Dictionary<int, Dictionary<string, SlabMomentSet>> SlabMoments { get; } = new();

    /// <summary>Tìm tổ hợp theo tên: tổ hợp ngoài (kết quả nhập) trước, sau đó tổ hợp của dự án.</summary>
    public LoadCombination FindCombo(string name) =>
        ExternalCombos.FirstOrDefault(x => x.Name == name) ?? Project.Combinations.FirstOrDefault(x => x.Name == name);

    /// <summary>Tên là tổ hợp bao (dùng Envelope + ExpandCombos thay cho Get).</summary>
    public bool IsEnvelope(string name) =>
        !string.IsNullOrEmpty(name) && !Cases.ContainsKey(name) && FindCombo(name)?.Type == ComboType.Envelope;
    // ---- [hết] ----

    /// <summary>Lấy kết quả theo tên trường hợp tải hoặc tổ hợp tuyến tính (ULS/SLS).</summary>
    public CaseResult Get(string name)
    {
        if (Cases.TryGetValue(name, out var c)) return c;
        if (_combos.TryGetValue(name, out c)) return c;
        var combo = FindCombo(name)
                    ?? throw new KeyNotFoundException("Không có trường hợp/tổ hợp " + name);
        if (combo.Type == ComboType.Envelope)
            throw new InvalidOperationException($"Tổ hợp {name} là tổ hợp bao – dùng Envelope()");
        CaseResult r = null;
        foreach (var it in combo.Items)
        {
            var src = Get(it.Case);
            if (r == null) r = src.Scaled(it.Factor, name);
            else r.AddScaled(src, it.Factor);
        }
        r ??= new CaseResult { Name = name };
        _combos[name] = r;
        return r;
    }

    /// <summary>Danh sách tổ hợp tuyến tính thành phần (mở rộng các tổ hợp bao).</summary>
    public List<string> ExpandCombos(string name)
    {
        if (Cases.ContainsKey(name)) return new List<string> { name };
        var combo = FindCombo(name);
        if (combo == null || combo.Type != ComboType.Envelope) return new List<string> { name };
        return combo.Items.SelectMany(i => ExpandCombos(i.Case)).Distinct().ToList();
    }

    public IEnumerable<string> CombosOfType(ComboType t)
    {
        // kết quả nhập: danh sách TTGH I / TTGH II do người dùng phân loại (rỗng = dùng tổ hợp của dự án)
        if (External != null)
        {
            if (t == ComboType.ULS && External.UlsCases.Count > 0) return External.UlsCases.ToList();
            if (t == ComboType.SLS && External.SlsCases.Count > 0) return External.SlsCases.ToList();
            if (t == ComboType.Envelope && ExternalCombos.Any(c => c.Type == ComboType.Envelope))
                return ExternalCombos.Where(c => c.Type == ComboType.Envelope).Select(c => c.Name).ToList();
        }
        return Project.Combinations.Where(c => c.Type == t).Select(c => c.Name);
    }

    /// <summary>Tổ hợp dùng để thiết kế (TTGH I). Nếu chưa có tổ hợp nào thì dùng các trường hợp tải.</summary>
    public List<string> DesignCombos()
    {
        var l = CombosOfType(ComboType.ULS).ToList();
        return l.Count > 0 ? l : Cases.Keys.ToList();
    }

    public List<string> ServiceCombos()
    {
        var l = CombosOfType(ComboType.SLS).ToList();
        return l.Count > 0 ? l : DesignCombos();
    }

    /// <summary>Bao nội lực max/min của một thanh qua các tổ hợp.</summary>
    public (FrameForces max, FrameForces min) Envelope(int frameId, IEnumerable<string> combos)
    {
        FrameForces mx = null, mn = null;
        foreach (var c in combos)
        {
            // kết quả nhập có thể thiếu thanh ở một số tổ hợp → bỏ qua tổ hợp đó
            if (!Get(c).Frames.TryGetValue(frameId, out var f) || f == null) continue;
            if (mx == null) { mx = f.CloneScaled(1); mn = f.CloneScaled(1); continue; }
            if (f.Count != mx.Count) f = Resample(f, mx.Stations);
            for (int i = 0; i < f.Count; i++)
                for (int k = 0; k < 6; k++)
                {
                    mx.F[i][k] = Math.Max(mx.F[i][k], f.F[i][k]);
                    mn.F[i][k] = Math.Min(mn.F[i][k], f.F[i][k]);
                }
        }
        return (mx, mn);
    }

    /// <summary>Nội suy nội lực về dãy mặt cắt cho trước.</summary>
    public static FrameForces Resample(FrameForces f, double[] stations)
    {
        var r = new FrameForces { Stations = (double[])stations.Clone(), F = new double[stations.Length][] };
        for (int i = 0; i < stations.Length; i++) r.F[i] = f.Count == 0 ? new double[6] : f.At(stations[i]);
        return r;
    }
}

public class StoryLateralForce
{
    public string Pattern { get; set; } = "";
    public string Story { get; set; } = "";
    public double Elevation { get; set; }
    public double Fx { get; set; }
    public double Fy { get; set; }
    public string Note { get; set; } = "";
}

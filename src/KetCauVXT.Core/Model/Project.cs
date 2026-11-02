using System.Text.Json;
using System.Text.Json.Serialization;

namespace KetCauVXT.Model;

/// <summary>Thông số thiết kế BTCT.</summary>
public class ConcreteDesignSettings
{
    public string ConcreteColumn { get; set; } = "B25";
    public string ConcreteBeam { get; set; } = "B25";
    public string ConcreteSlab { get; set; } = "B25";
    public string ConcreteFooting { get; set; } = "B25";
    /// <summary>Thép dọc (Ø ≥ 10)</summary>
    public string RebarMain { get; set; } = "CB400-V";
    /// <summary>Thép đai, thép sàn (Ø &lt; 10)</summary>
    public string RebarStirrup { get; set; } = "CB240-T";
    /// <summary>Lớp bê tông bảo vệ (tới mép cốt dọc), m</summary>
    public double CoverBeam { get; set; } = 0.025;
    public double CoverColumn { get; set; } = 0.025;
    public double CoverSlab { get; set; } = 0.015;
    public double CoverFooting { get; set; } = 0.050;
    public int[] BeamBars { get; set; } = { 14, 16, 18, 20, 22, 25 };
    public int[] ColumnBars { get; set; } = { 14, 16, 18, 20, 22, 25 };
    public int[] SlabBars { get; set; } = { 6, 8, 10, 12 };
    public int[] StirrupBars { get; set; } = { 6, 8, 10 };
    public int[] FootingBars { get; set; } = { 10, 12, 14, 16 };
    /// <summary>Hàm lượng thép tối thiểu dầm/cột (%)</summary>
    public double MinRatioBeam { get; set; } = 0.10;
    public double MinRatioColumn { get; set; } = 0.40;
    public double MaxRatioColumn { get; set; } = 3.0;
    /// <summary>Hệ số chiều dài tính toán cột (khung nhiều tầng nhiều nhịp, sàn đổ toàn khối)</summary>
    public double ColumnLengthFactor { get; set; } = 0.7;
    /// <summary>Độ võng giới hạn dầm L/xxx (TTGH II)</summary>
    public double BeamDeflectionLimit { get; set; } = 250;
}

/// <summary>Thông số nền đất cho móng đơn trên nền thiên nhiên (TCVN 9362:2012).</summary>
public class SoilSettings
{
    /// <summary>Góc ma sát trong φ (độ)</summary>
    public double Phi { get; set; } = 16;
    /// <summary>Lực dính c, kN/m²</summary>
    public double C { get; set; } = 15;
    /// <summary>Trọng lượng riêng đất dưới đáy móng, kN/m³</summary>
    public double Gamma { get; set; } = 18.5;
    /// <summary>Trọng lượng riêng đất trên đáy móng, kN/m³</summary>
    public double GammaAbove { get; set; } = 18.0;
    /// <summary>Độ sâu chôn móng Df tính từ cốt tự nhiên, m</summary>
    public double Depth { get; set; } = 1.5;
    public double M1 { get; set; } = 1.1;
    public double M2 { get; set; } = 1.0;
    public double Ktc { get; set; } = 1.0;
    /// <summary>Nếu &gt; 0 thì dùng trực tiếp sức chịu tải tiêu chuẩn này (kN/m²) thay cho công thức</summary>
    public double ROverride { get; set; }
    /// <summary>Trọng lượng riêng trung bình móng + đất đắp, kN/m³</summary>
    public double GammaAvg { get; set; } = 22;
    /// <summary>Hệ số vượt tải trung bình để quy đổi nội lực tính toán → tiêu chuẩn</summary>
    public double LoadFactorAvg { get; set; } = 1.15;
    /// <summary>Tỷ số cạnh móng l/b mong muốn</summary>
    public double AspectRatio { get; set; } = 1.2;
}

public class SteelDesignSettings
{
    /// <summary>Hệ số điều kiện làm việc γc</summary>
    public double GammaC { get; set; } = 0.95;
    /// <summary>Độ võng giới hạn kèo L/xxx</summary>
    public double RafterDeflection { get; set; } = 250;
    /// <summary>Chuyển vị ngang đỉnh cột giới hạn H/xxx</summary>
    public double ColumnDrift { get; set; } = 150;
    /// <summary>Số đoạn chia phần tử vát khi phân tích</summary>
    public int TaperSegments { get; set; } = 6;
}

/// <summary>Toàn bộ dữ liệu công trình. Lưu/mở bằng JSON (*.kcvxt).</summary>
public class Project
{
    public string Name { get; set; } = "Công trình mới";
    public string Location { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Designer { get; set; } = "";
    public string Version { get; set; } = "1.0";

    public List<Material> Materials { get; set; } = new();
    public List<FrameSection> FrameSections { get; set; } = new();
    public List<SlabSection> SlabSections { get; set; } = new();
    public List<GridLine> Grids { get; set; } = new();
    public List<Story> Stories { get; set; } = new();
    /// <summary>Cao độ chân cột (mặt móng), m</summary>
    public double BaseElevation { get; set; } = 0.0;
    /// <summary>Gán sàn tuyệt đối cứng trong mặt phẳng (diaphragm) cho các nút sàn mỗi tầng</summary>
    public bool UseRigidDiaphragm { get; set; } = true;

    public List<Node> Nodes { get; set; } = new();
    public List<FrameElement> Frames { get; set; } = new();
    public List<Slab> Slabs { get; set; } = new();
    public List<Footing> Footings { get; set; } = new();
    /// <summary>Lỗ sàn: cầu thang, giếng trời (ô không có sàn)</summary>
    public List<FloorOpening> Openings { get; set; } = new();
    /// <summary>Thư viện loại tường và các đoạn tường khai báo theo vị trí</summary>
    public List<WallType> WallTypes { get; set; } = WallType.Defaults();
    public List<WallLine> Walls { get; set; } = new();
    /// <summary>Thư viện cấu tạo sàn (lớp hoàn thiện + hoạt tải); Slab.BuildUp tham chiếu theo tên</summary>
    public List<FloorBuildUp> FloorBuildUps { get; set; } = FloorBuildUp.Defaults();
    public FoundationSettings Foundation { get; set; } = new();
    /// <summary>Bộ bản vẽ in (dàn trang)</summary>
    public List<Sheet> Sheets { get; set; } = new();

    public List<LoadPattern> Patterns { get; set; } = new();
    public List<FrameLoad> FrameLoads { get; set; } = new();
    public List<NodeLoad> NodeLoads { get; set; } = new();
    public List<AreaLoad> AreaLoads { get; set; } = new();
    public List<LoadCombination> Combinations { get; set; } = new();

    public WindSettings Wind { get; set; } = new();
    public SeismicSettings Seismic { get; set; } = new();
    public ConcreteDesignSettings Concrete { get; set; } = new();
    public SteelDesignSettings Steel { get; set; } = new();
    public SoilSettings Soil { get; set; } = new();

    // ------------------------------------------------------------------
    // Truy vấn
    // ------------------------------------------------------------------
    private Dictionary<int, Node> _nodeIndex;
    private Dictionary<string, FrameSection> _secIndex;
    private Dictionary<string, Material> _matIndex;

    public void InvalidateIndex() { _nodeIndex = null; _secIndex = null; _matIndex = null; }

    public Node Node(int id)
    {
        if (_nodeIndex == null || _nodeIndex.Count != Nodes.Count) _nodeIndex = Nodes.ToDictionary(n => n.Id);
        return _nodeIndex[id];
    }

    public FrameSection Section(string name)
    {
        if (_secIndex == null || _secIndex.Count != FrameSections.Count) _secIndex = FrameSections.ToDictionary(s => s.Name);
        return _secIndex.TryGetValue(name, out var sec) ? sec : throw new KeyNotFoundException("Không có tiết diện " + name);
    }

    public SlabSection SlabSection(string name) =>
        SlabSections.FirstOrDefault(s => s.Name == name) ?? throw new KeyNotFoundException("Không có tiết diện sàn " + name);

    public Material Material(string name)
    {
        if (_matIndex == null || _matIndex.Count != Materials.Count) _matIndex = Materials.ToDictionary(m => m.Name);
        if (_matIndex.TryGetValue(name, out var found)) return found;
        // tự thêm từ thư viện nếu có
        Material lib = null;
        if (Model.Material.ConcreteClasses.Contains(name)) lib = Model.Material.Concrete(name);
        else if (Model.Material.RebarGrades.Contains(name)) lib = Model.Material.Rebar(name);
        else if (Model.Material.SteelGrades.Contains(name)) lib = Model.Material.Steel(name);
        if (lib == null) throw new KeyNotFoundException("Không có vật liệu " + name);
        Materials.Add(lib); _matIndex = null;
        return lib;
    }

    public FrameElement Frame(int id) => Frames.First(f => f.Id == id);

    public double Length(FrameElement f) => (Node(f.NodeJ).P - Node(f.NodeI).P).Length;

    public LoadPattern Pattern(string name) => Patterns.FirstOrDefault(p => p.Name == name);

    public int NextNodeId() => Nodes.Count == 0 ? 1 : Nodes.Max(n => n.Id) + 1;
    public int NextFrameId() => Frames.Count == 0 ? 1 : Frames.Max(n => n.Id) + 1;
    public int NextSlabId() => Slabs.Count == 0 ? 1 : Slabs.Max(n => n.Id) + 1;

    /// <summary>Tìm nút trùng toạ độ (sai số 1 mm) hoặc tạo mới.</summary>
    public Node GetOrAddNode(double x, double y, double z, string story = "")
    {
        const double tol = 1e-3;
        foreach (var n in Nodes)
            if (Math.Abs(n.X - x) < tol && Math.Abs(n.Y - y) < tol && Math.Abs(n.Z - z) < tol) return n;
        var nn = new Node { Id = NextNodeId(), X = x, Y = y, Z = z, Story = story };
        Nodes.Add(nn); _nodeIndex = null;
        return nn;
    }

    public Story StoryAt(double z) =>
        Stories.OrderBy(s => s.Elevation).FirstOrDefault(s => Math.Abs(s.Elevation - z) < 1e-3);

    public IEnumerable<GridLine> GridsX => Grids.Where(g => g.Dir == GridDir.X).OrderBy(g => g.Coord);
    public IEnumerable<GridLine> GridsY => Grids.Where(g => g.Dir == GridDir.Y).OrderBy(g => g.Coord);

    /// <summary>Kiểm tra dữ liệu, trả về danh sách lỗi (rỗng = hợp lệ).</summary>
    public List<string> Validate()
    {
        var err = new List<string>();
        var ids = new HashSet<int>(Nodes.Select(n => n.Id));
        foreach (var f in Frames)
        {
            if (!ids.Contains(f.NodeI) || !ids.Contains(f.NodeJ)) err.Add($"Thanh {f.Id} tham chiếu nút không tồn tại");
            else if (Length(f) < 1e-4) err.Add($"Thanh {f.Id} có chiều dài bằng 0");
            if (FrameSections.All(s => s.Name != f.Section)) err.Add($"Thanh {f.Id} dùng tiết diện chưa khai báo '{f.Section}'");
        }
        if (!Nodes.Any(n => n.IsSupport)) err.Add("Mô hình chưa có liên kết gối (ngàm chân cột)");
        foreach (var s in Slabs)
            if (s.Nodes.Any(i => !ids.Contains(i))) err.Add($"Sàn {s.Id} tham chiếu nút không tồn tại");
        foreach (var l in FrameLoads)
            if (Patterns.All(p => p.Name != l.Pattern)) err.Add($"Tải trên thanh {l.Element} dùng trường hợp tải chưa khai báo '{l.Pattern}'");
        return err;
    }

    // ------------------------------------------------------------------
    // Lưu / mở
    // ------------------------------------------------------------------
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));

    public static Project Load(string path) =>
        JsonSerializer.Deserialize<Project>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("File dự án lỗi");

    public Project Clone() => JsonSerializer.Deserialize<Project>(JsonSerializer.Serialize(this, JsonOptions), JsonOptions);
}

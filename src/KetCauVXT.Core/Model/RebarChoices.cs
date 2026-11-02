using System.Text.Json.Serialization;

namespace KetCauVXT.Model;

// =====================================================================
// LỰA CHỌN BỐ TRÍ THÉP CỦA NGƯỜI DÙNG + TUỲ CHỌN CHUNG
// Lưu trong Project (JSON *.kcvxt). File cũ không có các mục này → danh sách rỗng / mặc định = hành vi cũ.
// Đơn vị: chiều dài m, đường kính mm.
// =====================================================================

/// <summary>Một nhóm thanh cùng đường kính (dạng lưu JSON được, thay cho tuple).</summary>
public class RebarGroup
{
    public int Dia { get; set; }
    public int Count { get; set; }
    public RebarGroup() { }
    public RebarGroup(int dia, int count) { Dia = dia; Count = count; }
}

/// <summary>Phương án bố trí thép sàn.</summary>
public enum SlabRebarScheme
{
    /// <summary>Lưới dưới 2 phương + thép mũ tại các cạnh ngàm (cạnh chung 2 ô: một thanh vắt qua dầm)</summary>
    TopBarsBottomMesh = 0,
    /// <summary>Lưới 2 lớp: lưới trên liên tục cả ô / dải ô (As = max yêu cầu gối, hàm lượng tối thiểu); mũ bổ sung chỉ nơi lưới thiếu</summary>
    TwoLayerMesh = 1,
    /// <summary>Lưới 2 lớp (lưới trên theo hàm lượng tối thiểu) + mũ tăng cường tại gối</summary>
    TwoLayerMeshPlusTop = 2
}

/// <summary>Tên vùng bố trí thép dùng trong RebarChoice.Zone.</summary>
public static class RebarZones
{
    // dầm
    /// <summary>Thép dưới giữa nhịp (Index = số thứ tự nhịp, từ 0) – tổng số thanh tại tiết diện, kể cả 2 thanh góc chạy suốt.</summary>
    public const string BeamBottom = "Dưới nhịp";
    /// <summary>Thép trên tại gối (Index = số thứ tự gối, 0..số nhịp) – tổng số thanh kể cả 2 thanh góc chạy suốt.</summary>
    public const string BeamTopSupport = "Trên gối";
    /// <summary>Thép trên chạy suốt (giữa nhịp) – 2 thanh góc, Index = −1.</summary>
    public const string BeamTopCont = "Trên chạy suốt";
    /// <summary>Thép dưới chạy suốt – 2 thanh góc, Index = −1.</summary>
    public const string BeamBotCont = "Dưới chạy suốt";
    /// <summary>Đai dầm (Index = nhịp) / đai cột (Index = tầng): Dia, Spacing (vùng gối/đầu cột), Spacing2 (giữa).</summary>
    public const string Stirrup = "Đai";
    // cột
    /// <summary>Thép dọc cột tầng Index: Dia, NB (số thanh mỗi cạnh b, kể cả góc), NH (mỗi cạnh h).</summary>
    public const string ColumnMain = "Thép dọc";
    // sàn (X = phương ngắn như SlabDesign.BotX)
    public const string SlabBotX = "Dưới X";
    public const string SlabBotY = "Dưới Y";
    public const string SlabTopX = "Mũ X";
    public const string SlabTopY = "Mũ Y";
    /// <summary>Lưới trên phương ngắn / dài (phương án lưới 2 lớp): Dia, Spacing</summary>
    public const string SlabMeshX = "Lưới trên X";
    public const string SlabMeshY = "Lưới trên Y";
    /// <summary>Phương án bố trí thép của ô sàn (RebarChoice.Scheme)</summary>
    public const string SlabScheme = "Phương án sàn";
    /// <summary>Phương án bố trí thép cho cả tầng (chỉ dùng trong hộp thoại – ghi thành SlabScheme cho mọi ô của tầng)</summary>
    public const string SlabSchemeStory = "Phương án sàn cả tầng";
    // móng đơn
    public const string FootingL = "Phương L";
    public const string FootingB = "Phương B";
    // đài cọc (lớp dưới: FootingL / FootingB)
    public const string CapTopL = "Lớp trên phương L";
    public const string CapTopB = "Lớp trên phương B";
    // móng băng (đai sườn: Stirrup, Index = −1)
    public const string StripRibBottom = "Sườn dưới";
    public const string StripRibTop = "Sườn trên";
    public const string StripFlange = "Bản cánh";
}

/// <summary>Một lựa chọn bố trí thép do người dùng chốt cho một vùng của một cấu kiện vật lý.
/// Khoá cấu kiện (Member): "D:{id phần tử nhỏ nhất của dải dầm}", "C:{id phần tử cột tầng dưới cùng}", "S:{id ô sàn}", "M:{id nút chân cột}".</summary>
public class RebarChoice
{
    public string Member { get; set; } = "";
    public string Zone { get; set; } = "";
    public int Index { get; set; } = -1;
    /// <summary>Dầm: các nhóm thanh (tổng tại tiết diện).</summary>
    public List<RebarGroup> Bars { get; set; } = new();
    /// <summary>Cột / sàn / móng / đai: đường kính.</summary>
    public int Dia { get; set; }
    /// <summary>Cột: số thanh trên mỗi cạnh b và mỗi cạnh h (kể cả 2 thanh góc).</summary>
    public int NB { get; set; }
    public int NH { get; set; }
    /// <summary>Sàn / móng: bước thép (m). Đai: bước vùng gối / đầu cột (m).</summary>
    public double Spacing { get; set; }
    /// <summary>Đai: bước giữa nhịp / giữa cột (m).</summary>
    public double Spacing2 { get; set; }
    /// <summary>Sàn: phương án bố trí (Zone = RebarZones.SlabScheme)</summary>
    public SlabRebarScheme Scheme { get; set; }
    /// <summary>Kích thước tiết diện lúc chọn ("220x400", "t=120", "1.6x1.4x0.5"); khác hiện tại → bỏ qua lựa chọn.</summary>
    public string Basis { get; set; } = "";
    /// <summary>Tên cấu kiện lúc chọn (chỉ để tham khảo, vd "D2-1").</summary>
    public string Label { get; set; } = "";

    [JsonIgnore] public string Key => Member + "|" + Zone + "|" + Index;
}

/// <summary>Tuỳ chọn chung khi tự chọn thép và triển khai gia công. Giá trị mặc định = hành vi cũ của chương trình.</summary>
public class RebarPreferences
{
    /// <summary>Đường kính ưu tiên cho thép dọc (giao với danh sách Ø của từng loại cấu kiện); rỗng = dùng hết.</summary>
    public int[] PreferredDias { get; set; } = Array.Empty<int>();
    /// <summary>Số thanh tối đa trên một hàng thép dầm; 0 = theo bề rộng (khoảng hở TCVN 5574:2018 mục 10.3.5).</summary>
    public int MaxBarsPerLayer { get; set; }
    /// <summary>Ưu tiên thép tăng cường / mũ gối cùng Ø với thép chạy suốt.</summary>
    public bool PreferSameDia { get; set; }
    /// <summary>Cho phép đặt thép dầm 2 hàng.</summary>
    public bool AllowTwoLayers { get; set; } = true;
    /// <summary>Ø đai cố định (mm); 0 = tự chọn.</summary>
    public int StirrupDia { get; set; }
    /// <summary>Bước đai nhỏ nhất (m); 0 = không giới hạn.</summary>
    public double MinStirrupSpacing { get; set; }
    /// <summary>Bước đai lớn nhất (m); 0 = theo cấu tạo của tiêu chuẩn.</summary>
    public double MaxStirrupSpacing { get; set; }
    /// <summary>Chiều dài cây thép thương phẩm (m) – thanh dài hơn phải nối chồng.</summary>
    public double StockLength { get; set; } = 11.7;
    /// <summary>Đầu thừa ≥ giá trị này (m) được coi là tái sử dụng được (không tính hao hụt).</summary>
    public double MinReuseOffcut { get; set; } = 1.0;
    /// <summary>Nối so le: không nối quá 50% số thanh tại một tiết diện (TCVN 5574:2018 mục 10.3.6).</summary>
    public bool StaggerSplices { get; set; } = true;
    // ---- nguyên tắc nối thép (không nối trong vùng nguy hiểm của cấu kiện) ----
    /// <summary>Vùng tới hạn dầm lcr = k·h tính từ mép cột (khung không kháng chấn); không nối trong nút khung và vùng này.</summary>
    public double BeamCriticalFactor { get; set; } = 1.0;
    /// <summary>Vùng tới hạn dầm khi Project.Seismic.Enabled (khung kháng chấn, TCVN 9386-1:2025 (thay thế TCVN 9386:2012; số điều theo bản 2012) 5.4.3.1.2 kiểu cấp dẻo trung bình/cao): lcr = k·h.</summary>
    public double BeamCriticalFactorSeismic { get; set; } = 2.0;
    /// <summary>Vùng tới hạn cột lcr = max(hc; Hthông thuỷ / tỷ số; giá trị nhỏ nhất) ở hai đầu mỗi tầng (TCVN 9386-1:2025 (thay thế TCVN 9386:2012; số điều theo bản 2012) 5.4.3.2.2 / 5.5.3.2.2).</summary>
    public double ColumnCriticalMin { get; set; } = 0.45;
    public double ColumnCriticalRatio { get; set; } = 6.0;
    /// <summary>Hệ số α chiều dài nối chồng (TCVN 5574:2018 10.3.6.2): vùng kéo 1,2 (khi ≤ 50% số thanh nối tại một tiết diện), vùng nén 0,9.</summary>
    public double LapAlphaTension { get; set; } = 1.2;
    public double LapAlphaCompression { get; set; } = 0.9;
    /// <summary>Khoảng so le tối thiểu giữa tâm các mối nối = hệ số × ll (≥ 1,3 – các mối nối gần hơn coi là cùng tiết diện).</summary>
    public double StaggerFactor { get; set; } = 1.3;
    /// <summary>Cho phép dùng ống nối ren (coupler) khi vùng cho phép ngắn hơn chiều dài nối chồng.</summary>
    public bool AllowCoupler { get; set; } = true;
    /// <summary>Không nối thép móng đơn / đài cọc (dùng thanh nguyên; dài hơn cây thương phẩm → cảnh báo, đặt cây dài).</summary>
    public bool NoLapInFootings { get; set; } = true;
    /// <summary>Trừ độ dãn dài khi uốn vào chiều dài cắt trên bản vẽ gia công (bản vẽ chi tiết vẫn ghi kích thước ngoài).</summary>
    public bool BendDeduction { get; set; } = true;
    /// <summary>Lượng trừ mỗi chỗ uốn theo góc, tính bằng bội số d (bảng thực hành gia công, xấp xỉ với bán kính uốn trong 2–2,5d):
    /// 90° → 2d, 135° → 3d, 180° → 4d.</summary>
    public double Deduct90 { get; set; } = 2.0;
    public double Deduct135 { get; set; } = 3.0;
    public double Deduct180 { get; set; } = 4.0;
    /// <summary>Phương án bố trí thép sàn mặc định cho cả công trình (từng ô có thể chọn riêng).</summary>
    public SlabRebarScheme SlabScheme { get; set; } = SlabRebarScheme.TopBarsBottomMesh;

    /// <summary>Tên hiển thị phương án sàn.</summary>
    public static string SchemeName(SlabRebarScheme s) => s switch
    {
        SlabRebarScheme.TwoLayerMesh => "Lưới 2 lớp",
        SlabRebarScheme.TwoLayerMeshPlusTop => "Lưới 2 lớp + mũ tăng cường",
        _ => "Thép mũ + lưới dưới"
    };

    /// <summary>Lọc danh sách Ø theo PreferredDias (giữ nguyên nếu giao rỗng).</summary>
    public int[] Filter(int[] dias)
    {
        if (dias == null) return dias;
        if (PreferredDias == null || PreferredDias.Length == 0) return dias;
        var f = dias.Where(d => PreferredDias.Contains(d)).ToArray();
        return f.Length > 0 ? f : dias;
    }
}

/// <summary>Tra cứu / ghi lựa chọn thép trong Project.</summary>
public static class RebarChoiceExt
{
    public static RebarChoice FindRebarChoice(this Project p, string member, string zone, int index)
    {
        if (p?.RebarChoices == null || string.IsNullOrEmpty(member)) return null;
        return p.RebarChoices.LastOrDefault(c => c != null && c.Member == member && c.Zone == zone && c.Index == index);
    }

    /// <summary>Lựa chọn còn hiệu lực: cùng khoá và cùng kích thước tiết diện (Basis rỗng = luôn hiệu lực).</summary>
    public static RebarChoice ValidRebarChoice(this Project p, string member, string zone, int index, string basis)
    {
        var c = p.FindRebarChoice(member, zone, index);
        if (c == null) return null;
        if (!string.IsNullOrEmpty(c.Basis) && !string.IsNullOrEmpty(basis) && c.Basis != basis) return null;
        return c;
    }

    /// <summary>Thêm hoặc thay lựa chọn cùng khoá (Member, Zone, Index).</summary>
    public static void SetRebarChoice(this Project p, RebarChoice c)
    {
        if (p == null || c == null) return;
        p.RebarChoices ??= new List<RebarChoice>();
        p.RebarChoices.RemoveAll(x => x == null || (x.Member == c.Member && x.Zone == c.Zone && x.Index == c.Index));
        p.RebarChoices.Add(c);
    }

    /// <summary>Xoá lựa chọn; zone = null → xoá mọi vùng của cấu kiện. Trả về số lựa chọn đã xoá.</summary>
    public static int ClearRebarChoice(this Project p, string member, string zone = null, int? index = null)
    {
        if (p?.RebarChoices == null) return 0;
        return p.RebarChoices.RemoveAll(x => x == null || (x.Member == member && (zone == null || x.Zone == zone) && (index == null || x.Index == index.Value)));
    }
}

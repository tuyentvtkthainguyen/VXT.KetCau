namespace KetCauVXT.Design.Foundation;

/// <summary>Trạng thái một hạng mục kiểm tra an toàn móng.</summary>
public enum CheckStatus { Ok, Warning, Fail, Info }

/// <summary>Một hạng mục kiểm tra an toàn móng: giá trị, giới hạn, hệ số sử dụng, kết luận và giải thích tiếng Việt.</summary>
public class SafetyItem
{
    /// <summary>Nhóm hạng mục: "Phản lực cọc", "Chọc thủng", "Cắt", "Uốn", "Bố trí cọc", "Nền", "Sự làm việc"…</summary>
    public string Group { get; set; } = "";
    public string Name { get; set; } = "";
    public double Value { get; set; }
    public double Limit { get; set; }
    public string Unit { get; set; } = "";
    /// <summary>true = điều kiện dạng Value ≥ Limit (hệ số sử dụng = Limit/Value)</summary>
    public bool Inverse { get; set; }
    public CheckStatus Status { get; set; } = CheckStatus.Ok;
    /// <summary>Công thức / điều khoản áp dụng</summary>
    public string Formula { get; set; } = "";
    /// <summary>Giải thích sự làm việc / ý nghĩa / cách khắc phục</summary>
    public string Explanation { get; set; } = "";

    /// <summary>Hệ số sử dụng (≤ 1 đạt)</summary>
    public double Utilisation
    {
        get
        {
            if (Inverse) return Math.Abs(Value) > 1e-12 ? Limit / Math.Abs(Value) : (Limit > 1e-12 ? 99 : 0);
            return Limit > 1e-12 ? Math.Abs(Value) / Limit : (Math.Abs(Value) > 1e-12 ? 99 : 0);
        }
    }

    public string StatusText => Status switch
    {
        CheckStatus.Ok => "ĐẠT", CheckStatus.Warning => "CẢNH BÁO", CheckStatus.Fail => "KHÔNG ĐẠT", _ => "THÔNG TIN"
    };

    /// <summary>Tạo hạng mục từ điều kiện Value ≤ Limit (Inverse = false) hoặc Value ≥ Limit (Inverse = true);
    /// failStatus: trạng thái khi không thoả (Fail hoặc Warning).</summary>
    public static SafetyItem Make(string group, string name, double value, double limit, string unit, string formula, string okText, string badText,
        bool inverse = false, CheckStatus failStatus = CheckStatus.Fail)
    {
        var it = new SafetyItem { Group = group, Name = name, Value = value, Limit = limit, Unit = unit, Inverse = inverse, Formula = formula };
        bool ok = it.Utilisation <= 1 + 1e-6;
        it.Status = ok ? CheckStatus.Ok : failStatus;
        it.Explanation = ok ? okText : badText;
        return it;
    }
}

/// <summary>Phản lực một cọc trong đài.</summary>
public class PileReactionRow
{
    public int No { get; set; }
    /// <summary>Toạ độ tim cọc so với tim cột (m)</summary>
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>Phản lực ở tổ hợp bất lợi (kN, nén dương)</summary>
    public double P { get; set; }
    /// <summary>Phản lực lớn nhất / nhỏ nhất qua mọi tổ hợp TTGH I (kN)</summary>
    public double PMax { get; set; }
    public double PMin { get; set; }
    /// <summary>Tổ hợp gây PMax</summary>
    public string ComboMax { get; set; } = "";
    /// <summary>true = tim cọc nằm trong đáy tháp chọc thủng 45° của cột (không gây chọc thủng do cột)</summary>
    public bool InsidePyramid { get; set; }
}

/// <summary>Chi tiết kiểm tra chọc thủng đài do cột (TCVN 5574:2018 mục 8.1.6.2 + hệ số h0/c cho đài cọc).</summary>
public class PunchingDetail
{
    /// <summary>Lực chọc thủng F = Σ phản lực ròng các cọc ngoài tháp (kN)</summary>
    public double F { get; set; }
    /// <summary>Khả năng chịu chọc thủng của bê tông Fb,u (kN)</summary>
    public double Fbu { get; set; }
    /// <summary>Khoảng cách từ mép cột tới mép trong hàng cọc theo X, Y (m, đã giới hạn 0,4h0 ≤ c ≤ h0)</summary>
    public double C1 { get; set; }
    public double C2 { get; set; }
    /// <summary>Hệ số tăng k = h0/c (1 … 2,5)</summary>
    public double K1 { get; set; } = 1;
    public double K2 { get; set; } = 1;
    public double H0 { get; set; }
    /// <summary>Chu vi tính toán u = 2(hc + bc + 2h0) (m)</summary>
    public double U { get; set; }
    /// <summary>true = mọi cọc nằm trong đáy tháp chọc thủng → F = 0, không xảy ra chọc thủng do cột</summary>
    public bool AllInside { get; set; }
    /// <summary>Cốt thép chống chọc thủng yêu cầu Asw/sw (m²/m chu vi), 0 = không cần (8.1.6.3)</summary>
    public double AswPerM { get; set; }
    /// <summary>Phương án cốt thép chống chọc thủng gợi ý (vd. "Ø10a75")</summary>
    public string Reinforcement { get; set; } = "";
    /// <summary>Fsw,u = 0,8·qsw·u của cốt thép đề xuất (kN)</summary>
    public double Fswu { get; set; }
    /// <summary>true = F &gt; 2Fb,u: cốt thép ngang không đủ, bắt buộc tăng chiều cao đài</summary>
    public bool NeedHeight { get; set; }
}

/// <summary>Báo cáo kiểm tra an toàn + sự làm việc của một móng (đài cọc / móng đơn / móng băng).</summary>
public class FoundationCheckReport
{
    /// <summary>"Đài cọc", "Móng đơn", "Móng băng"</summary>
    public string Kind { get; set; } = "";
    /// <summary>Tên sơ đồ bố trí cọc / phân loại sự làm việc (vd. "4 cọc hình vuông", "Dầm mềm (dài)")</summary>
    public string Classification { get; set; } = "";
    /// <summary>Đoạn giải thích sự làm việc của kết cấu móng</summary>
    public string Behaviour { get; set; } = "";
    public List<SafetyItem> Items { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<PileReactionRow> Piles { get; set; } = new();
    public PunchingDetail Punching { get; set; }
    /// <summary>true = bố trí cọc / kích thước đài do người dùng chọn</summary>
    public bool UserLayout { get; set; }
    /// <summary>Hệ số nhóm η (đài cọc)</summary>
    public double GroupEta { get; set; } = 1;
    /// <summary>Móng băng: λ = (k·B/4EI)^¼ (1/m), λL, độ cứng EI (kN.m²)</summary>
    public double Lambda { get; set; }
    public double LambdaL { get; set; }
    public double EI { get; set; }
    /// <summary>Chiều cao đài ban đầu (tối thiểu) và chiều cao chọn (m) – báo cáo việc tăng chiều cao theo chọc thủng / cắt</summary>
    public double HStart { get; set; }
    public double HFinal { get; set; }

    public bool AnyFail => Items.Any(i => i.Status == CheckStatus.Fail);
    public bool AnyWarning => Items.Any(i => i.Status == CheckStatus.Warning) || Warnings.Count > 0;
    public double MaxUtilisation => Items.Where(i => i.Status != CheckStatus.Info).Select(i => i.Utilisation).DefaultIfEmpty(0).Max();
}

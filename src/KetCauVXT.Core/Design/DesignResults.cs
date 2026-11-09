namespace KetCauVXT.Design;

// =====================================================================
// HỢP ĐỒNG DỮ LIỆU KẾT QUẢ THIẾT KẾ
// Module thiết kế ghi vào các lớp này; module bản vẽ, thuyết minh, bảng tính chỉ đọc.
// Đơn vị: chiều dài m, diện tích m², lực kN, mô men kN.m, đường kính thép mm.
// =====================================================================

/// <summary>Một số hiệu thép (một bộ Ø – chiều dài – hình dạng) trong một cấu kiện.</summary>
public class BarMark
{
    /// <summary>Số hiệu (đánh lại theo quy tắc Kết cấu Soft trước khi vẽ/thống kê)</summary>
    public int Mark { get; set; }
    /// <summary>Vai trò: "Dưới", "Trên", "Mũ gối", "Tăng cường dưới", "Giá", "Đai", "Đai C", "Thép dọc cột", "Lớp dưới X"...</summary>
    public string Role { get; set; } = "";
    public int Dia { get; set; }
    /// <summary>Số thanh trong MỘT cấu kiện</summary>
    public int Count { get; set; }
    /// <summary>Chiều dài 1 thanh (m), đã làm tròn 10 mm</summary>
    public double Length { get; set; }
    /// <summary>Hình dạng: "Thẳng", "L", "U", "Đai", "Đai C"</summary>
    public string Shape { get; set; } = "Thẳng";
    /// <summary>Kích thước các đoạn để vẽ phác trong bảng thống kê (mm). Đai: [cạnh 1, cạnh 2].</summary>
    public List<double> Segments { get; set; } = new();
    /// <summary>Thép dọc: vị trí đầu/cuối dọc cấu kiện (m, tính từ đầu cấu kiện)</summary>
    public double Start { get; set; }
    public double End { get; set; }
    /// <summary>Đai, thép rải: bước (m). Vùng gối/giữa nhịp thể hiện bằng 2 BarMark cùng số hiệu? Không – xem StirrupZone.</summary>
    public double Spacing { get; set; }
    /// <summary>Nhịp chứa thanh (dầm liên tục), −1 nếu chạy suốt</summary>
    public int Span { get; set; } = -1;
    /// <summary>Thép nằm ở nửa trên tiết diện</summary>
    public bool IsTop { get; set; }

    public double WeightPerBar => 0.00617 * Dia * Dia * Length;
    /// <summary>Vùng cho phép nối (m, cùng hệ toạ độ đoạn thẳng của thanh, gốc tại đầu thanh sau móc) dạng cặp [a0, b0, a1, b1…];
    /// null = theo quy tắc của loại cấu kiện (RebarShop), rỗng = không được nối.</summary>
    public List<double> SpliceAllowed { get; set; }
    public string Tag => Shape.StartsWith("Đai") || Spacing > 0 ? $"Ø{Dia}a{Spacing * 1000:0}" : $"{Count}Ø{Dia}";
}

/// <summary>Một mối nối thép: tâm (m), chiều dài nối (0 = ống nối ren), nhóm so le, vùng cho phép chứa nó, ghi chú vi phạm.</summary>
public class SpliceInfo
{
    public string Member { get; set; } = "";
    public int Mark { get; set; }
    public int Dia { get; set; }
    /// <summary>Nhịp / tầng chứa mối nối</summary>
    public int Span { get; set; } = -1;
    public double Center { get; set; }
    public double Lap { get; set; }
    public bool Coupler => Lap <= 1e-9;
    /// <summary>"A" / "B" (nhóm so le)</summary>
    public string Group { get; set; } = "A";
    public int Count { get; set; }
    /// <summary>Vùng cho phép chứa mối nối (m); ZoneA = ZoneB = NaN nếu không có</summary>
    public double ZoneA { get; set; } = double.NaN;
    public double ZoneB { get; set; } = double.NaN;
    /// <summary>Mối nối nằm ngoài vùng cho phép (không tránh được) – phải xử lý</summary>
    public bool Violation { get; set; }
    public string Note { get; set; } = "";
    public double From => Center - Lap / 2;
    public double To => Center + Lap / 2;
}

/// <summary>Vùng bố trí đai trên một nhịp dầm hoặc một đoạn cột.</summary>
public class StirrupZone
{
    public int Span { get; set; }
    /// <summary>Từ – đến (m) dọc cấu kiện</summary>
    public double From { get; set; }
    public double To { get; set; }
    public int Dia { get; set; }
    public double Spacing { get; set; }
    public int Mark { get; set; }
    public int Legs { get; set; } = 2;
}

public class CheckItem
{
    public string Name { get; set; } = "";
    public double Demand { get; set; }
    public double Capacity { get; set; }
    public string Unit { get; set; } = "";
    public double Ratio => Capacity > 1e-12 ? Math.Abs(Demand) / Capacity : (Math.Abs(Demand) > 1e-12 ? 99 : 0);
    public bool Ok => Ratio <= 1.0 + 1e-6;
    public string Note { get; set; } = "";
}

public abstract class MemberDesignBase
{
    /// <summary>Tên cấu kiện thể hiện: "D2-8", "C2", "S3", "M1", "K1"</summary>
    public string Name { get; set; } = "";
    public string Story { get; set; } = "";
    /// <summary>Số lượng cấu kiện giống hệt (gộp hình)</summary>
    public int Quantity { get; set; } = 1;
    public List<CheckItem> Checks { get; set; } = new();
    public List<string> Messages { get; set; } = new();
    public List<BarMark> Bars { get; set; } = new();
    /// <summary>Khoá các cấu kiện vật lý gộp vào thiết kế này (dùng cho lựa chọn thép Project.RebarChoices): "D:12", "C:5", "S:3", "M:7".</summary>
    public List<string> RebarKeys { get; set; } = new();
    /// <summary>Mối nối thép đã bố trí trên bản vẽ chi tiết (vd. nối thép cột giữa tầng) – kiểm tra nguyên tắc nối.</summary>
    public List<SpliceInfo> Splices { get; set; } = new();
    /// <summary>Số vùng thép đang dùng lựa chọn của người dùng (0 = tự chọn hoàn toàn).</summary>
    public int UserRebarZones { get; set; }
    public bool Ok => Checks.All(c => c.Ok) && !Messages.Any(m => m.StartsWith("LỖI"));
    public double MaxRatio => Checks.Count == 0 ? 0 : Checks.Max(c => c.Ratio);
}

// ---------------------------------------------------------------------
// DẦM
// ---------------------------------------------------------------------
public class BeamSpanResult
{
    public int Frame { get; set; }
    public string LeftSupport { get; set; } = "";   // tên trục/cột gối trái
    public string RightSupport { get; set; } = "";
    /// <summary>Chiều dài nhịp tim–tim (m)</summary>
    public double L { get; set; }
    /// <summary>Nhịp thông thuỷ (m) = L − ½bc trái − ½bc phải</summary>
    public double Lclear { get; set; }
    /// <summary>Bề rộng cột gối trái/phải theo phương dầm (m)</summary>
    public double SupportWidthLeft { get; set; }
    public double SupportWidthRight { get; set; }
    // nội lực thiết kế (bao TTGH I)
    public double MLeft { get; set; }      // mô men âm gối trái (≤ 0)
    public double MSpan { get; set; }      // mô men dương nhịp (≥ 0)
    public double MRight { get; set; }     // mô men âm gối phải (≤ 0)
    public double QMax { get; set; }
    public string ComboMLeft { get; set; } = "";
    public string ComboMSpan { get; set; } = "";
    public string ComboMRight { get; set; } = "";
    public string ComboQ { get; set; } = "";
    // thép yêu cầu / bố trí (m²)
    public double AsTopLeftReq { get; set; }
    public double AsBotReq { get; set; }
    public double AsTopRightReq { get; set; }
    public double AsTopLeftProv { get; set; }
    public double AsBotProv { get; set; }
    public double AsTopRightProv { get; set; }
    public string TopLeftText { get; set; } = "";   // "2Ø18+1Ø16"
    public string BotText { get; set; } = "";
    public string TopRightText { get; set; } = "";
    // đai
    public int StirrupDia { get; set; }
    public double StirrupSupport { get; set; }      // bước đai vùng gối (m)
    public double StirrupMid { get; set; }          // bước đai giữa nhịp (m)
    public double SupportZone { get; set; }         // chiều dài vùng gối (m)
    // võng
    public double Deflection { get; set; }          // m (TTGH II, đàn hồi × hệ số nứt)
    public double DeflectionLimit { get; set; }
    // xoắn (dầm cong)
    /// <summary>Mô men xoắn bao lớn nhất |T| trong nhịp (kN.m) – chỉ tính cho dầm cong</summary>
    public double TMax { get; set; }
    public string ComboT { get; set; } = "";
    /// <summary>Diện tích cốt dọc chịu xoắn toàn chu vi Asl (m²) – đã cộng ½ vào thép trên, ½ vào thép dưới</summary>
    public double AsTorsion { get; set; }
}

public class BeamLineDesign : MemberDesignBase
{
    /// <summary>[Triển khai hồ sơ] bối cảnh sàn hai bên dầm nhập tay (cánh chữ T / L, hạ cốt) – BeamDrawing dùng thay cho việc dò mô hình.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public KetCauVXT.Drawing.BeamSlabSection ManualSlab { get; set; }
    public List<int> Frames { get; set; } = new();
    /// <summary>Lỗ xuyên dầm (ống lồng) + gia cường – từ cảnh báo điện nước hoặc nhập tay (Drawing/BeamHoles.cs)</summary>
    public List<KetCauVXT.Drawing.BeamHole> Holes { get; set; } = new();
    public double B { get; set; }
    public double H { get; set; }
    public string Section { get; set; } = "";
    /// <summary>Cao độ đỉnh dầm (m)</summary>
    public double TopElevation { get; set; }
    /// <summary>Kiểu bố trí thép dọc (A / B / C)</summary>
    public KetCauVXT.Model.BeamRebarScheme Scheme { get; set; }
    public List<BeamSpanResult> Spans { get; set; } = new();
    public List<StirrupZone> StirrupZones { get; set; } = new();
    /// <summary>Tổng chiều dài dầm tim trục đầu – tim trục cuối (m); dầm cong: theo chiều dài cung</summary>
    public double TotalLength => Spans.Sum(s => s.L);
    // dầm cong (FrameElement.CurveMidOffset ≠ 0)
    /// <summary>Dầm cong trên mặt bằng: thiết kế và thể hiện trên chiều dài cung (mặt đứng trải)</summary>
    public bool IsCurved { get; set; }
    /// <summary>Bán kính cong R (m)</summary>
    public double CurveRadius { get; set; }
    /// <summary>Chiều dài cung (m)</summary>
    public double ArcLength { get; set; }
    /// <summary>Dây cung (m)</summary>
    public double Chord { get; set; }
    /// <summary>Độ vồng (m, trị tuyệt đối)</summary>
    public double Sagitta { get; set; }
    /// <summary>Mô men xoắn bao lớn nhất |T| (kN.m)</summary>
    public double TorsionMax { get; set; }
    /// <summary>Diện tích thép trên yêu cầu tại từng gối 0..số nhịp (m²; 0 tại đầu tự do công xôn)</summary>
    public List<double> SupportAsReq { get; set; } = new();
    /// <summary>Diện tích cốt chịu nén yêu cầu cho thép chạy suốt dưới (tại gối) / trên (giữa nhịp), m²</summary>
    public double ContBotAsReq { get; set; }
    public double ContTopAsReq { get; set; }
}

// ---------------------------------------------------------------------
// CỘT
// ---------------------------------------------------------------------
public class ColumnStoryResult
{
    public int Frame { get; set; }
    public string Story { get; set; } = "";
    public double B { get; set; }      // cạnh theo trục 3 (m)
    public double H { get; set; }      // cạnh theo trục 2 (m)
    public double Length { get; set; } // chiều cao (m)
    public double ZBottom { get; set; }
    public double ZTop { get; set; }
    // nội lực tới hạn
    public double N { get; set; }      // nén dương (kN)
    public double M2 { get; set; }     // kN.m (đã kể uốn dọc)
    public double M3 { get; set; }
    public string Combo { get; set; } = "";
    public double Eta2 { get; set; } = 1;
    public double Eta3 { get; set; } = 1;
    public double AsReq { get; set; }   // tổng diện tích yêu cầu (m²)
    public double AsProv { get; set; }
    public int Dia { get; set; }
    /// <summary>Số thanh trên mỗi cạnh song song trục 3 (cạnh b, kể cả góc) và trục 2 (cạnh h)</summary>
    public int BarsAlongB { get; set; }
    public int BarsAlongH { get; set; }
    public int TotalBars => 2 * BarsAlongB + 2 * Math.Max(0, BarsAlongH - 2);
    public double Ratio => B * H > 0 ? AsProv / (B * H) * 100 : 0;
    public int StirrupDia { get; set; }
    public double StirrupSpacing { get; set; }
    public double StirrupSpacingEnd { get; set; }   // vùng đầu cột / nút khung
    public double Utilization { get; set; }          // tỷ số khả năng chịu lực
    /// <summary>Chiều cao nút khung đỉnh tầng = chiều cao dầm lớn nhất tại nút trên (m)</summary>
    public double JointDepth { get; set; }
}

public class ColumnDesign : MemberDesignBase
{
    /// <summary>Phần tử cột từ dưới lên trên</summary>
    public List<int> Frames { get; set; } = new();
    public string GridName { get; set; } = "";
    public List<ColumnStoryResult> Stories { get; set; } = new();
    public List<StirrupZone> StirrupZones { get; set; } = new();
}

// ---------------------------------------------------------------------
// SÀN
// ---------------------------------------------------------------------
public class SlabDesign : MemberDesignBase
{
    public int SlabId { get; set; }
    public double Lx { get; set; }       // cạnh ngắn (m)
    public double Ly { get; set; }       // cạnh dài (m)
    public double Thickness { get; set; }
    public bool TwoWay { get; set; }
    public string EdgeCondition { get; set; } = "";   // "4 cạnh ngàm", ...
    public double Q { get; set; }        // tải tính toán toàn phần kN/m²
    public double M1 { get; set; }       // nhịp phương ngắn kN.m/m
    public double M2 { get; set; }       // nhịp phương dài
    public double MI { get; set; }       // gối phương ngắn (âm)
    public double MII { get; set; }      // gối phương dài (âm)
    public string BotX { get; set; } = "";  // "Ø8a200" – lớp dưới theo phương ngắn
    public string BotY { get; set; } = "";
    public string TopX { get; set; } = "";  // mũ gối cạnh vuông góc phương ngắn
    public string TopY { get; set; } = "";
    public double Deflection { get; set; }
    /// <summary>Phương án bố trí thép đã dùng</summary>
    public KetCauVXT.Model.SlabRebarScheme Scheme { get; set; }
    /// <summary>Tổng chiều dài cạnh ngàm có thép mũ theo phương ngắn (cạnh dài) / phương dài (cạnh ngắn), m</summary>
    public double FixedEdgeLenShort { get; set; }
    public double FixedEdgeLenLong { get; set; }
    /// <summary>Thép trên vẽ trên mặt bằng (toạ độ m): mũ gối (cạnh chung 2 ô chỉ ghi ở MỘT ô) và lưới trên liên tục (ghi ở ô đầu dải).
    /// Các ô gộp (Quantity &gt; 1) nối thêm hình của từng ô.</summary>
    public List<SlabEdgeBar> EdgeBars { get; set; } = new();
    public List<SlabTopMesh> TopMeshes { get; set; } = new();
    /// <summary>Đỉnh ô (m) để vẽ mặt bằng thép sàn</summary>
    public List<(double x, double y)> Outline { get; set; } = new();
    public double Elevation { get; set; }
}

/// <summary>Một thanh mũ trên một cạnh sàn để vẽ: đoạn cạnh (X1,Y1)–(X2,Y2), pháp tuyến đơn vị (NX, NY) hướng về phía 1,
/// đoạn vươn Ext1 (phía 1) và Ext2 (phía 2) tính từ trục cạnh (m).</summary>
public class SlabEdgeBar
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public double NX { get; set; }
    public double NY { get; set; }
    public double Ext1 { get; set; }
    public double Ext2 { get; set; }
    /// <summary>Cạnh chung hai ô (thanh vắt qua dầm)</summary>
    public bool Shared { get; set; }
    public BarMark Bar { get; set; }
}

/// <summary>Lưới trên liên tục một dải ô (vẽ một thanh đại diện): hình chữ nhật dải (m), thép chạy theo X (AlongX) hoặc Y.</summary>
public class SlabTopMesh
{
    public bool AlongX { get; set; }
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public BarMark Bar { get; set; }
}

// ---------------------------------------------------------------------
// MÓNG ĐƠN
// ---------------------------------------------------------------------
public class FootingDesign : MemberDesignBase
{
    public int NodeId { get; set; }
    public int ColumnFrame { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double ColumnB { get; set; }
    public double ColumnH { get; set; }
    // nội lực tính toán tại chân cột (TTGH I) và tiêu chuẩn
    public double N { get; set; }
    public double Mx { get; set; }
    public double My { get; set; }
    public double Qx { get; set; }
    public double Qy { get; set; }
    public double Ntc { get; set; }
    public string Combo { get; set; } = "";
    // kích thước
    public double L { get; set; }        // cạnh theo X (m)
    public double B { get; set; }        // cạnh theo Y (m)
    public double H { get; set; }        // chiều cao móng (m)
    public double Hb { get; set; }       // chiều cao mép móng (m) – móng vát
    /// <summary>Móng bậc: cạnh bậc trên theo phương L (m), 0 = móng vát / phẳng.</summary>
    public double StepL { get; set; }
    /// <summary>Móng bậc: cạnh bậc trên theo phương B (m).</summary>
    public double StepB { get; set; }
    public double Depth { get; set; }    // độ sâu chôn móng (m)
    public double R { get; set; }        // sức chịu tải tiêu chuẩn nền (kN/m²)
    public double Pmax { get; set; }
    public double Pmin { get; set; }
    public double Ptb { get; set; }
    public double AsL { get; set; }      // thép theo phương L (m²)
    public double AsB { get; set; }
    public string BarsL { get; set; } = "";  // "Ø12a150"
    public string BarsB { get; set; } = "";
    public int DiaL { get; set; }
    public int DiaB { get; set; }
    public double SpacingL { get; set; }
    public double SpacingB { get; set; }
    /// <summary>Mô men tính thép tại mép cột theo phương L / B (kN.m, toàn bề rộng móng)</summary>
    public double ML { get; set; }
    public double MB { get; set; }
    /// <summary>[FoundationChecks] Sự làm việc (móng cứng, lệch tâm, trượt, chọc thủng có mô men, cắt) – FootingBehaviour.AnalyzeIsolated</summary>
    public KetCauVXT.Design.Foundation.FoundationCheckReport Safety { get; set; }
    /// <summary>[MóngLệchTâm] Móng lệch tâm (ranh giới đất): phương án, độ lệch tâm, dầm cân bằng, móng kết hợp; null = móng đặt tâm tại tim cột</summary>
    public KetCauVXT.Design.Foundation.EccentricInfo Eccentric { get; set; }
    /// <summary>[CừTràm] Gia cố nền bằng cừ tràm dưới móng (null = nền thiên nhiên) – Design/Foundation/CuTramCalc.cs</summary>
    public KetCauVXT.Design.Foundation.CuTramResult CuTram { get; set; }
    // ---- [MóngHìnhDạng] móng xoay / lệch tâm dóng mép cột / cắt theo ranh đất xiên / đa giác (Design/Foundation/ShapedFooting.cs) ----
    /// <summary>Đường bao đáy móng (x, y tuyệt đối, m, ngược chiều kim đồng hồ); null = chữ nhật L × B tâm tại tim cột (+ Eccentric)</summary>
    public List<double[]> Outline { get; set; }
    /// <summary>Góc phương L của móng so với trục X (độ)</summary>
    public double Angle { get; set; }
    /// <summary>Trọng tâm đáy móng (m) – móng hình dạng</summary>
    public double Gx { get; set; }
    public double Gy { get; set; }
    /// <summary>Diện tích đáy móng (m²) – móng hình dạng</summary>
    public double Area { get; set; }
    /// <summary>Tỷ lệ vùng tiếp xúc nền (1 = toàn bộ đáy chịu nén)</summary>
    public double Contact { get; set; } = 1;
    /// <summary>Tim cột trong hệ địa phương của móng (x' theo L, y' theo B; gốc = tâm khung bao địa phương), m</summary>
    public double ColLocalX { get; set; }
    public double ColLocalY { get; set; }
    /// <summary>Kích thước chữ nhật trước khi cắt theo đường chuẩn (m) – dùng khi nhân bản / đồng nhất móng; 0 = móng đa giác</summary>
    public double RectL { get; set; }
    public double RectB { get; set; }
    /// <summary>Dầm móng cân bằng của móng hình dạng lệch tâm: nút chân cột đối diện (0 = không), nhịp (m), M = N·e (kN.m), Q = ΔR (kN)</summary>
    public int StrapNode { get; set; }
    public double StrapSpan { get; set; }
    public double StrapMoment { get; set; }
    public double StrapShear { get; set; }
    public bool Shaped => Outline != null && Outline.Count >= 3;
    // ---- hết [MóngHìnhDạng] ----
}

// ---------------------------------------------------------------------
// THÉP TIỀN CHẾ
// ---------------------------------------------------------------------
public class SteelMemberCheck : MemberDesignBase
{
    public int Frame { get; set; }
    public string Section { get; set; } = "";
    public string Combo { get; set; } = "";
    public double N { get; set; }
    public double M { get; set; }
    public double V { get; set; }
    public double Station { get; set; }
    public double Lambda { get; set; }
    public double Deflection { get; set; }
    public double DeflectionLimit { get; set; }
}

/// <summary>Toàn bộ kết quả thiết kế.</summary>
public class DesignSummary
{
    public List<BeamLineDesign> Beams { get; set; } = new();
    public List<ColumnDesign> Columns { get; set; } = new();
    public List<SlabDesign> Slabs { get; set; } = new();
    public List<FootingDesign> Footings { get; set; } = new();
    /// <summary>Đài cọc (khi Project.Foundation.Type = Pile hoặc chân cột chọn móng cọc)</summary>
    public List<KetCauVXT.Design.Foundation.PileCapDesign> PileCaps { get; set; } = new();
    /// <summary>Móng băng (dải móng dưới dãy cột)</summary>
    public List<KetCauVXT.Design.Foundation.StripFootingDesign> StripFootings { get; set; } = new();
    public List<SteelMemberCheck> Steel { get; set; } = new();
    // ---- [cầu thang – Design/StairResults.cs, Design/Concrete/StairDesigner.cs] ----
    /// <summary>Cầu thang bộ (không nằm trong All: thép thống kê theo từng bộ phận StairDesign.Parts)</summary>
    public List<StairDesign> Stairs { get; set; } = new();
    // ---- [hết cầu thang] ----
    // ---- [MóngLệchTâm] dầm móng cân bằng (Design/Foundation/EccentricFooting.cs) ----
    /// <summary>Dầm móng cân bằng DM nối móng lệch tâm (sát ranh giới đất) với móng cột trong</summary>
    public List<KetCauVXT.Design.Foundation.StrapBeamDesign> StrapBeams { get; set; } = new();
    // ---- hết [MóngLệchTâm] ----
    public List<string> Warnings { get; set; } = new();

    /// <summary>[Triển khai hồ sơ] cấu kiện khác (móng bè, bể nước, dầm DƯL…) đưa vào bảng thống kê thép: (cấu kiện, nhóm).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<(MemberDesignBase Member, string Category)> Others { get; set; } = new();

    public IEnumerable<MemberDesignBase> All =>
        Beams.Cast<MemberDesignBase>().Concat(Columns).Concat(Slabs).Concat(Footings).Concat(PileCaps).Concat(StripFootings).Concat(StrapBeams ?? new List<KetCauVXT.Design.Foundation.StrapBeamDesign>()).Concat(Steel);   // [MóngLệchTâm] StrapBeams
}

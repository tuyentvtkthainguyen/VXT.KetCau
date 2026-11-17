using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Docs;

// =====================================================================
// KHOÁ BƯỚC THEO QUY TRÌNH – mỗi thao tác chỉ được làm khi các bước tiền đề đã xong; gợi ý bước tiếp theo.
// Chuỗi bắt buộc (theo thứ tự):
//   1 lưới trục + tầng → 2 tiết diện → 3 cấu kiện → 4 tải trọng + tổ hợp → 5 kiểm tra mô hình không còn lỗi
//   → 6 phân tích → 7 người dùng xem / xác nhận kết quả → 8 thiết kế → 9 mọi cấu kiện đạt → hồ sơ.
// Tường, địa chất là bước nên làm nhưng không chặn (công trình có thể không có).
// Giao diện: MainWindow.Gate.cs (hộp thoại chặn, nút "Bước tiếp" ở thanh trạng thái), QuickStartWindow (khoá 🔒).
// =====================================================================

/// <summary>Nhóm thao tác cần kiểm tra bước tiền đề.</summary>
public enum WorkflowAction
{
    /// <summary>Sửa mô hình (dầm, sàn, tường, tải gán): cần lưới trục, tầng.</summary>
    EditModel,
    /// <summary>Xuất mô hình ETABS / SAP để kiểm chéo: cần mô hình đủ cấu kiện, tiết diện.</summary>
    ExportModel,
    /// <summary>Chạy phân tích.</summary>
    Analysis,
    /// <summary>Xem / xuất kết quả phân tích (bảng, biểu đồ, phản lực, so sánh ETABS, CSV).</summary>
    Results,
    /// <summary>Thiết kế BTCT, móng, kiểm tra thép, tự tăng tiết diện.</summary>
    Design,
    /// <summary>Chỉnh thép đã thiết kế, bản vẽ từng dầm, khối lượng: cần đã thiết kế.</summary>
    DesignEdit,
    /// <summary>Phát hành hồ sơ: bản vẽ, shop drawing, dàn trang, thuyết minh, bảng tính, xuất tất cả.</summary>
    Documents,
}

/// <summary>Trạng thái dự án dùng để xét khoá bước.</summary>
public sealed class WorkflowState
{
    public Project Project { get; init; }
    /// <summary>Đã có kết quả phân tích hợp lệ (mô hình chưa đổi sau lần phân tích).</summary>
    public bool HasResults { get; init; }
    public DesignSummary Design { get; init; }
    /// <summary>Người dùng đã xem bảng / biểu đồ kết quả hoặc xác nhận đã kiểm tra kết quả hiện hành.</summary>
    public bool ResultsReviewed { get; init; }
}

/// <summary>Một bước tiền đề chưa xong.</summary>
public sealed class GateItem
{
    /// <summary>Mã bước (trùng QuickStartStep.Id, hoặc "kiem-tra-mh", "thiet-ke-dat") – giao diện dùng để chuyển tới bước.</summary>
    public string StepId { get; init; } = "";
    /// <summary>Tên bước, vd. "Tải trọng, tổ hợp".</summary>
    public string Title { get; init; } = "";
    /// <summary>Vì sao chưa đạt, vd. "Chưa có tổ hợp tải trọng".</summary>
    public string Reason { get; init; } = "";
    /// <summary>Việc người dùng cần làm, vd. "Phân tích → Tự tạo tổ hợp theo TCVN 2737:2023".</summary>
    public string Todo { get; init; } = "";
    public override string ToString() => $"{Title}: {Reason}";
}

/// <summary>Kết quả xét khoá một thao tác.</summary>
public sealed class GateResult
{
    public WorkflowAction Action { get; init; }
    public List<GateItem> Missing { get; } = new();
    public bool Allowed => Missing.Count == 0;
    /// <summary>Bước nên làm ngay (bước thiếu đầu tiên theo thứ tự quy trình).</summary>
    public GateItem First => Missing.FirstOrDefault();
    /// <summary>Chỉ còn thiếu bước người dùng xác nhận đã kiểm tra kết quả.</summary>
    public bool OnlyReviewMissing => Missing.Count == 1 && Missing[0].StepId == WorkflowGate.ReviewStepId;
}

/// <summary>Kiểm tra bước tiền đề và gợi ý bước tiếp theo theo quy trình chuẩn của chương trình.</summary>
public static class WorkflowGate
{
    public const string ModelCheckStepId = "kiem-tra-mh";
    public const string ReviewStepId = "kiem-tra";
    public const string DesignOkStepId = "thiet-ke-dat";

    /// <summary>Tên thao tác hiển thị trong hộp thoại.</summary>
    public static string ActionTitle(WorkflowAction a) => a switch
    {
        WorkflowAction.EditModel => "Sửa mô hình",
        WorkflowAction.ExportModel => "Xuất mô hình ETABS / SAP",
        WorkflowAction.Analysis => "Chạy phân tích",
        WorkflowAction.Results => "Xem kết quả phân tích",
        WorkflowAction.Design => "Thiết kế",
        WorkflowAction.DesignEdit => "Chỉnh thép / khối lượng",
        WorkflowAction.Documents => "Xuất hồ sơ",
        _ => a.ToString(),
    };

    // ------------------------------------------------------------------ từng điều kiện
    private static GateItem Grid(Project p) =>
        p.Stories.Count >= 1 && (p.Grids.Count >= 2 || p.Frames.Count > 0) ? null : new GateItem
        {
            StepId = "luoi", Title = "Khởi tạo lưới trục, tầng",
            Reason = p.Stories.Count == 0 ? "Chưa có tầng." : "Chưa có lưới trục.",
            Todo = "Tab Công trình: nhập bước cột X, Y, chiều cao tầng → Tạo mô hình (hoặc Tệp → nhập bản vẽ / Revit / ETABS).",
        };

    private static GateItem Members(Project p) =>
        p.Frames.Count > 0 ? null : new GateItem
        {
            StepId = "cau-kien", Title = "Cột, dầm, sàn",
            Reason = "Mô hình chưa có cột, dầm.",
            Todo = "Bấm Tạo khung BTCT / Khung tiền chế, hoặc vẽ cột, dầm trên tab Mặt bằng.",
        };

    private static GateItem Sections(Project p)
    {
        if (p.Frames.Count == 0) return null;   // chưa có cấu kiện: đã báo ở bước Cột, dầm, sàn
        var names = new HashSet<string>(p.FrameSections.Select(s => s.Name), StringComparer.Ordinal);
        var miss = p.Frames.Select(f => f.Section).Where(s => !names.Contains(s ?? "")).Distinct().ToList();
        return miss.Count == 0 ? null : new GateItem
        {
            StepId = "tiet-dien", Title = "Tiết diện, vật liệu",
            Reason = $"{miss.Count} tiết diện thanh chưa khai báo: {string.Join(", ", miss.Take(6).Select(s => string.IsNullOrEmpty(s) ? "(trống)" : s))}{(miss.Count > 6 ? "…" : "")}.",
            Todo = "Khai báo → Tiết diện dầm/cột…, hoặc gán lại tiết diện cho các thanh đó.",
        };
    }

    private static GateItem Loads(Project p)
    {
        if (p.Patterns.Count == 0) return new GateItem
        {
            StepId = "tai-trong", Title = "Tải trọng, tổ hợp",
            Reason = "Chưa có trường hợp tải.",
            Todo = "Tab Tải trọng: khai báo trường hợp tải (tĩnh tải, hoạt tải, gió, động đất).",
        };
        if (p.Combinations.Count == 0) return new GateItem
        {
            StepId = "tai-trong", Title = "Tải trọng, tổ hợp",
            Reason = "Chưa có tổ hợp tải trọng.",
            Todo = "Khai báo → Tự tạo tổ hợp theo TCVN 2737:2023 (hoặc Tổ hợp tải trọng… để tự nhập).",
        };
        return null;
    }

    private static GateItem ModelErrors(Project p, bool runCheck)
    {
        if (!runCheck) return null;
        ModelCheckReport rep;
        try { rep = ModelCheck.Run(p, new ModelCheckOptions { CheckEmptyPatterns = false, CheckSlabEdges = false }); }
        catch { return null; }   // không kiểm tra được: không chặn vì lỗi của chính bộ kiểm tra
        if (rep.Errors == 0) return null;
        var first = rep.Issues.Where(i => i.Severity == IssueSeverity.Error).Take(3).Select(i => i.Message);
        return new GateItem
        {
            StepId = ModelCheckStepId, Title = "Kiểm tra mô hình",
            Reason = $"Mô hình còn {rep.Errors} lỗi: " + string.Join(" ", first) + (rep.Errors > 3 ? " …" : ""),
            Todo = "Phân tích → Kiểm tra mô hình…: sửa tự động hoặc sửa tay cho hết lỗi.",
        };
    }

    private static GateItem Analysed(WorkflowState s) =>
        s.HasResults ? null : new GateItem
        {
            StepId = "phan-tich", Title = "Phân tích",
            Reason = "Chưa có kết quả phân tích (hoặc mô hình đã thay đổi sau lần phân tích trước).",
            Todo = "Phân tích → Chạy phân tích (F5).",
        };

    private static GateItem Reviewed(WorkflowState s) =>
        s.ResultsReviewed ? null : new GateItem
        {
            StepId = ReviewStepId, Title = "Kiểm tra kết quả",
            Reason = "Bạn chưa xem kết quả phân tích hiện hành.",
            Todo = "Xem biểu đồ nội lực, phản lực, chuyển vị, cảnh báo (Hiển thị → Bảng kết quả…, Ctrl+T); so sánh ETABS/SAP khi cần, rồi xác nhận đã kiểm tra.",
        };

    private static GateItem Designed(WorkflowState s) =>
        s.Design != null ? null : new GateItem
        {
            StepId = "thiet-ke", Title = "Thiết kế",
            Reason = "Chưa thiết kế cấu kiện.",
            Todo = "Thiết kế → Thiết kế BTCT / Kiểm tra thép / Thiết kế móng….",
        };

    private static GateItem DesignOk(WorkflowState s)
    {
        if (s.Design == null) return null;
        var bad = s.Design.All.Where(m => m != null && !m.Ok).ToList();
        if (bad.Count == 0) return null;
        return new GateItem
        {
            StepId = DesignOkStepId, Title = "Mọi cấu kiện đạt",
            Reason = $"{bad.Count} cấu kiện không đạt: {string.Join(", ", bad.Take(8).Select(m => string.IsNullOrEmpty(m.Story) ? m.Name : m.Name + " (" + m.Story + ")"))}{(bad.Count > 8 ? "…" : "")}.",
            Todo = "Tăng tiết diện / cấp bê tông / chọn lại thép cho các cấu kiện tô đỏ (Thiết kế → Tự tăng tiết diện đến khi đạt), rồi thiết kế lại.",
        };
    }

    // ------------------------------------------------------------------ chuỗi điều kiện theo thao tác
    /// <summary>Các điều kiện tiền đề của thao tác, theo thứ tự quy trình. Có kết quả phân tích nghĩa là các bước mô hình đã qua
    /// (mọi thay đổi mô hình đều xoá kết quả); có kết quả thiết kế nghĩa là bước phân tích đã qua.</summary>
    private static IEnumerable<Func<GateItem>> Chain(WorkflowAction a, WorkflowState s, bool runModelCheck)
    {
        var p = s.Project;
        switch (a)
        {
            case WorkflowAction.EditModel:
                yield return () => Grid(p);
                yield break;
            case WorkflowAction.ExportModel:
                yield return () => Grid(p);
                yield return () => Members(p);
                yield return () => Sections(p);
                yield break;
            case WorkflowAction.Analysis:
                yield return () => Grid(p);
                yield return () => Members(p);
                yield return () => Sections(p);
                yield return () => Loads(p);
                yield return () => ModelErrors(p, runModelCheck);
                yield break;
            case WorkflowAction.Results:
                if (!s.HasResults)
                {
                    foreach (var f in Chain(WorkflowAction.Analysis, s, false)) yield return f;
                    yield return () => Analysed(s);
                }
                yield break;
            case WorkflowAction.Design:
                foreach (var f in Chain(WorkflowAction.Results, s, false)) yield return f;
                yield return () => Reviewed(s);
                yield break;
            case WorkflowAction.DesignEdit:
                if (s.Design == null)
                {
                    foreach (var f in Chain(WorkflowAction.Design, s, false)) yield return f;
                    yield return () => Designed(s);
                }
                yield break;
            case WorkflowAction.Documents:
                foreach (var f in Chain(WorkflowAction.DesignEdit, s, false)) yield return f;
                yield return () => DesignOk(s);
                yield break;
        }
    }

    /// <summary>Xét thao tác <paramref name="a"/>: danh sách bước tiền đề chưa xong (rỗng = được làm).
    /// <paramref name="runModelCheck"/>: chạy Kiểm tra mô hình (chỉ cần khi bấm Chạy phân tích).</summary>
    public static GateResult Check(WorkflowAction a, WorkflowState s, bool runModelCheck = true)
    {
        var r = new GateResult { Action = a };
        if (s?.Project == null)
        {
            r.Missing.Add(new GateItem { StepId = "luoi", Title = "Dự án", Reason = "Chưa có dự án.", Todo = "Tệp → Mới hoặc Mở dự án." });
            return r;
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in Chain(a, s, runModelCheck))
        {
            GateItem g;
            try { g = f(); }
            catch { g = null; }
            if (g != null && seen.Add(g.StepId + "|" + g.Reason)) r.Missing.Add(g);
        }
        return r;
    }

    /// <summary>Bước tiếp theo nên làm (bước bắt buộc chưa xong đầu tiên, rồi các bước nên làm); null khi đã đủ để xuất hồ sơ.
    /// Không chạy Kiểm tra mô hình (rẻ, gọi được sau mỗi thao tác).</summary>
    public static GateItem Next(WorkflowState s)
    {
        if (s?.Project == null) return Check(WorkflowAction.Documents, s).First;
        var p = s.Project;
        var g = Check(WorkflowAction.Documents, s, runModelCheck: false).First;
        if (g != null)
        {
            // mô hình đủ để phân tích: nhắc kiểm tra mô hình trước khi chạy (lỗi chặn được xét khi bấm Chạy phân tích)
            if (g.StepId == "phan-tich")
                return new GateItem
                {
                    StepId = "phan-tich", Title = "Kiểm tra mô hình rồi phân tích",
                    Reason = "Mô hình đã đủ lưới trục, cấu kiện, tiết diện, tải trọng." + OptionalNote(p),
                    Todo = "Phân tích → Kiểm tra mô hình…, rồi Chạy phân tích (F5).",
                };
            return g;
        }
        if (p.Sheets.Count == 0)
            return new GateItem
            {
                StepId = "ho-so", Title = "Bản vẽ, thuyết minh",
                Reason = "Mọi cấu kiện đã đạt – đủ điều kiện xuất hồ sơ.",
                Todo = "Xuất → Xuất tất cả vào thư mục… (bản vẽ DXF, shop drawing, thuyết minh, Excel, khối lượng, .e2k/.s2k).",
            };
        return null;
    }

    /// <summary>Nhắc bước nên làm nhưng không bắt buộc (tường, địa chất) trước khi phân tích.</summary>
    private static string OptionalNote(Project p)
    {
        var l = new List<string>();
        if (p.Walls.Count == 0 && !p.FrameLoads.Any(x => x.Source == "WALL")) l.Add("tường (tải tường lên dầm)");
        if (p.Boreholes.Count == 0) l.Add("địa chất (hố khoan) cho thiết kế móng");
        return l.Count == 0 ? "" : " Nên khai báo thêm nếu công trình có: " + string.Join(", ", l) + ".";
    }

    /// <summary>Thao tác bị khoá của một bước Bắt đầu nhanh (null = bước không bị khoá).</summary>
    public static WorkflowAction? ActionOfStep(string stepId) => stepId switch
    {
        "cau-kien" or "tuong" or "tai-trong" or "mong" => WorkflowAction.EditModel,
        "phan-tich" => WorkflowAction.Analysis,
        ReviewStepId => WorkflowAction.Results,
        "thiet-ke" => WorkflowAction.Design,
        "ho-so" => WorkflowAction.Documents,
        _ => null,
    };

    /// <summary>Bước tiền đề chưa xong của một bước Bắt đầu nhanh (rỗng = làm được). Không chạy Kiểm tra mô hình.</summary>
    public static GateResult CheckStep(string stepId, WorkflowState s)
    {
        var a = ActionOfStep(stepId);
        return a == null ? new GateResult() : Check(a.Value, s, runModelCheck: false);
    }
}

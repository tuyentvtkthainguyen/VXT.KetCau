using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Docs;
using KetCauVXT.Model;
using ConcreteDesigner = KetCauVXT.Design.Concrete.ConcreteDesigner;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng khoá bước theo quy trình (Docs/WorkflowGate.cs): thao tác chỉ được làm khi bước tiền đề xong,
/// bước thiếu đầu tiên đúng thứ tự quy trình, gợi ý bước tiếp theo. check(tên, giá trị, kỳ vọng, sai số).</summary>
public static class WorkflowGateTests
{
    private static Project House()
    {
        var p = ModelBuilder.NewProject("Kiểm tra khoá bước");
        ModelBuilder.SetGrid(p, new[] { 5.0 }, new[] { 4.5, 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, 0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x30" });
        p.Wind.Enabled = false;
        return p;
    }

    private static WorkflowState St(Project p, bool res = false, DesignSummary d = null, bool reviewed = false) =>
        new() { Project = p, HasResults = res, Design = d, ResultsReviewed = reviewed };

    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ---- mã bước dùng trong bộ khoá phải là bước Bắt đầu nhanh (giao diện chuyển tới được) ----
        var ids = new HashSet<string>(QuickStart.Steps.Select(s => s.Id));
        foreach (var id in new[] { "luoi", "tiet-dien", "cau-kien", "tai-trong", "phan-tich", WorkflowGate.ReviewStepId, "thiet-ke", "ho-so" })
            check("Khoá bước: mã '" + id + "' là bước Bắt đầu nhanh", B(ids.Contains(id)), 1, 0);
        check("Khoá bước: bước Phân tích khoá theo thao tác Phân tích", B(WorkflowGate.ActionOfStep("phan-tich") == WorkflowAction.Analysis), 1, 0);
        check("Khoá bước: bước Lưới trục không bị khoá", B(WorkflowGate.ActionOfStep("luoi") == null), 1, 0);

        // ---- dự án rỗng: không được phân tích / thiết kế / xuất hồ sơ ----
        {
            var p = ModelBuilder.NewProject("rỗng");
            var a = WorkflowGate.Check(WorkflowAction.Analysis, St(p));
            check("Dự án rỗng: chặn phân tích", B(a.Allowed), 0, 0);
            check("Dự án rỗng: bước thiếu đầu tiên là lưới trục hoặc cấu kiện", B(a.First?.StepId is "luoi" or "cau-kien"), 1, 0);
            check("Dự án rỗng: chặn thiết kế", B(WorkflowGate.Check(WorkflowAction.Design, St(p)).Allowed), 0, 0);
            check("Dự án rỗng: chặn xuất hồ sơ", B(WorkflowGate.Check(WorkflowAction.Documents, St(p)).Allowed), 0, 0);
            check("Dự án rỗng: có gợi ý bước tiếp", B(WorkflowGate.Next(St(p)) != null), 1, 0);
            check("Không có dự án: chặn mọi thao tác", B(WorkflowGate.Check(WorkflowAction.EditModel, null).Allowed), 0, 0);
        }

        // ---- mô hình đủ: phân tích được, chưa thiết kế được ----
        var house = House();
        {
            var a = WorkflowGate.Check(WorkflowAction.Analysis, St(house));
            foreach (var m in a.Missing) Console.WriteLine("    thiếu: " + m);
            check("Nhà mẫu: được phân tích", B(a.Allowed), 1, 0);
            check("Nhà mẫu: được xuất .e2k/.s2k", B(WorkflowGate.Check(WorkflowAction.ExportModel, St(house)).Allowed), 1, 0);
            var r = WorkflowGate.Check(WorkflowAction.Results, St(house));
            check("Nhà mẫu chưa phân tích: chặn xem kết quả", B(r.Allowed), 0, 0);
            check("Nhà mẫu chưa phân tích: bước thiếu là Phân tích", B(r.First?.StepId == "phan-tich"), 1, 0);
            var d = WorkflowGate.Check(WorkflowAction.Design, St(house));
            check("Nhà mẫu chưa phân tích: chặn thiết kế", B(d.Allowed), 0, 0);
            check("Nhà mẫu chưa phân tích: thiết kế thiếu Phân tích rồi Kiểm tra kết quả",
                B(d.Missing.Count == 2 && d.Missing[0].StepId == "phan-tich" && d.Missing[1].StepId == WorkflowGate.ReviewStepId), 1, 0);
            check("Nhà mẫu chưa phân tích: gợi ý Kiểm tra mô hình rồi phân tích", B(WorkflowGate.Next(St(house))?.StepId == "phan-tich"), 1, 0);
        }

        // ---- đã phân tích: phải xem kết quả trước khi thiết kế ----
        var res = FrameAnalysis.Run(house);
        {
            check("Đã phân tích: được xem kết quả", B(WorkflowGate.Check(WorkflowAction.Results, St(house, true)).Allowed), 1, 0);
            var d = WorkflowGate.Check(WorkflowAction.Design, St(house, true));
            check("Đã phân tích, chưa xem kết quả: chặn thiết kế", B(d.Allowed), 0, 0);
            check("Đã phân tích, chưa xem kết quả: chỉ thiếu bước xác nhận kết quả", B(d.OnlyReviewMissing), 1, 0);
            check("Đã phân tích: gợi ý Kiểm tra kết quả", B(WorkflowGate.Next(St(house, true))?.StepId == WorkflowGate.ReviewStepId), 1, 0);
            check("Đã xem kết quả: được thiết kế", B(WorkflowGate.Check(WorkflowAction.Design, St(house, true, null, true)).Allowed), 1, 0);
            var doc = WorkflowGate.Check(WorkflowAction.Documents, St(house, true, null, true));
            check("Chưa thiết kế: chặn xuất hồ sơ, thiếu bước Thiết kế", B(!doc.Allowed && doc.First?.StepId == "thiet-ke"), 1, 0);
            check("Chưa thiết kế: chặn chỉnh thép / khối lượng", B(WorkflowGate.Check(WorkflowAction.DesignEdit, St(house, true, null, true)).Allowed), 0, 0);
            check("Đã xem kết quả: gợi ý Thiết kế", B(WorkflowGate.Next(St(house, true, null, true))?.StepId == "thiet-ke"), 1, 0);
            check("Bắt đầu nhanh: bước Thiết kế bị khoá khi chưa xem kết quả", B(WorkflowGate.CheckStep("thiet-ke", St(house, true)).Allowed), 0, 0);
        }

        // ---- đã thiết kế: xuất hồ sơ khi và chỉ khi mọi cấu kiện đạt ----
        {
            var ds = ConcreteDesigner.Design(house, res, new DesignSummary());
            int bad = ds.All.Count(m => !m.Ok);
            var s = St(house, true, ds, true);
            check("Đã thiết kế: được chỉnh thép / khối lượng", B(WorkflowGate.Check(WorkflowAction.DesignEdit, s).Allowed), 1, 0);
            var doc = WorkflowGate.Check(WorkflowAction.Documents, s);
            check("Đã thiết kế: xuất hồ sơ được ⇔ mọi cấu kiện đạt", B(doc.Allowed), B(bad == 0), 0);
            if (bad > 0) check("Có cấu kiện không đạt: bước thiếu là Mọi cấu kiện đạt", B(doc.First?.StepId == WorkflowGate.DesignOkStepId), 1, 0);
            var nx = WorkflowGate.Next(s);
            check("Đã thiết kế: gợi ý xuất hồ sơ hoặc sửa cấu kiện không đạt",
                B(bad == 0 ? nx?.StepId == "ho-so" : nx?.StepId == WorkflowGate.DesignOkStepId), 1, 0);
            Console.WriteLine($"    {ds.All.Count()} cấu kiện thiết kế, {bad} không đạt");
        }

        // ---- lỗi mô hình chặn phân tích theo đúng bước ----
        {
            var p = House();
            p.Combinations.Clear();
            var a = WorkflowGate.Check(WorkflowAction.Analysis, St(p));
            check("Không có tổ hợp: chặn phân tích ở bước Tải trọng", B(!a.Allowed && a.First?.StepId == "tai-trong"), 1, 0);
        }
        {
            var p = House();
            p.Frames[0].Section = "TIET_DIEN_KHONG_CO";
            var a = WorkflowGate.Check(WorkflowAction.Analysis, St(p));
            check("Tiết diện chưa khai báo: chặn phân tích ở bước Tiết diện", B(!a.Allowed && a.First?.StepId == "tiet-dien"), 1, 0);
            check("Tiết diện chưa khai báo: chặn xuất .e2k", B(WorkflowGate.Check(WorkflowAction.ExportModel, St(p)).Allowed), 0, 0);
        }
        {
            var p = House();
            var f0 = p.Frames.First(f => f.Kind == FrameKind.Beam);
            p.Frames.Add(new FrameElement { Id = p.Frames.Max(f => f.Id) + 1, NodeI = f0.NodeI, NodeJ = f0.NodeJ, Section = f0.Section, Kind = f0.Kind, Story = f0.Story, Label = f0.Label + "'" });
            p.InvalidateIndex();
            var a = WorkflowGate.Check(WorkflowAction.Analysis, St(p), runModelCheck: true);
            check("Thanh trùng: chặn phân tích ở bước Kiểm tra mô hình", B(!a.Allowed && a.First?.StepId == WorkflowGate.ModelCheckStepId), 1, 0);
            check("Thanh trùng: xét nhanh (không kiểm tra mô hình) không chặn", B(WorkflowGate.Check(WorkflowAction.Analysis, St(p), runModelCheck: false).Allowed), 1, 0);
        }
    }
}

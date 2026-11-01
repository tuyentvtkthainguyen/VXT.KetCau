using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

/// <summary>Điểm vào thiết kế BTCT: dầm, cột, sàn, móng đơn theo TCVN 5574:2018 và TCVN 9362:2012.</summary>
public static class ConcreteDesigner
{
    /// <summary>Thiết kế toàn bộ cấu kiện BTCT của dự án từ kết quả phân tích r.
    /// Nếu s được truyền vào, các danh sách Beams/Columns/Slabs/Footings của s được thay mới, Warnings được nối thêm.
    /// Lỗi từng cấu kiện không làm dừng chương trình mà ghi vào s.Warnings với tiền tố "LỖI".</summary>
    public static DesignSummary Design(Project p, AnalysisResults r, DesignSummary s = null)
    {
        s ??= new DesignSummary();
        if (p == null) throw new ArgumentNullException(nameof(p));
        if (r == null) { s.Warnings.Add("LỖI chưa có kết quả phân tích – hãy chạy phân tích trước khi thiết kế"); return s; }
        p.InvalidateIndex();

        s.Beams = Run(s, "dầm", () => BeamDesigner.Design(p, r, s.Warnings));
        s.Columns = Run(s, "cột", () => ColumnDesigner.Design(p, r, s.Warnings));
        s.Slabs = Run(s, "sàn", () => SlabDesigner.Design(p, r, s.Warnings));
        s.Footings = Run(s, "móng", () => FootingDesigner.Design(p, r, s.Warnings));

        foreach (var m in s.All.Where(x => !x.Ok).ToList())
        {
            var failed = m.Checks.Where(c => !c.Ok).Select(c => c.Name).ToList();
            string why = failed.Count > 0 ? "không đạt: " + string.Join("; ", failed) : string.Join("; ", m.Messages.Where(x => x.StartsWith("LỖI")));
            s.Warnings.Add("Cấu kiện " + m.Name + " " + why);
        }
        return s;
    }

    private static List<T> Run<T>(DesignSummary s, string what, Func<List<T>> f)
    {
        try
        {
            return f() ?? new List<T>();
        }
        catch (Exception ex)
        {
            s.Warnings.Add("LỖI thiết kế " + what + ": " + ex.Message);
            return new List<T>();
        }
    }
}

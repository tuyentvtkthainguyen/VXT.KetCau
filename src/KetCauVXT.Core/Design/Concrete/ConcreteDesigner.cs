using KetCauVXT.Analysis;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

/// <summary>Điểm vào thiết kế BTCT: dầm, cột, sàn, móng đơn, đài cọc, móng băng theo TCVN 5574:2018, TCVN 9362:2012 và TCVN 10304:2014.</summary>
public static class ConcreteDesigner
{
    /// <summary>Thiết kế toàn bộ cấu kiện BTCT của dự án từ kết quả phân tích r.
    /// Nếu s được truyền vào, các danh sách Beams/Columns/Slabs/Footings/PileCaps/StripFootings của s được thay mới, Warnings được nối thêm.
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
        s.PileCaps = Run(s, "đài cọc", () => PileCapDesigner.Design(p, r, s.Warnings));
        s.StripFootings = Run(s, "móng băng", () => StripFootingDesigner.Design(p, r, s.Warnings));
        s.StrapBeams = Run(s, "dầm móng cân bằng", () => EccentricFooting.DesignStraps(p, s, s.Warnings));   // [MóngLệchTâm]
        // ---- cầu thang (Design/Concrete/StairDesigner.cs) ----
        s.Stairs = Run(s, "cầu thang", () => StairDesigner.Design(p, s.Warnings));
        // ---- hết cầu thang ----
        // ---- [Sàn hạ cốt] kiểm tra dầm biên ô sàn hạ cốt h ≥ hs1 + Δ + hs2 – Design/Concrete/SlabDropChecks.cs ----
        try { SlabDropChecks.Apply(p, s); }
        catch (Exception ex) { s.Warnings.Add("LỖI kiểm tra dầm biên sàn hạ cốt: " + ex.Message); }
        // ---- hết Sàn hạ cốt ----

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

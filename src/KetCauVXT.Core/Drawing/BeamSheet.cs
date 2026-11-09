using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>[Bản vẽ từng dầm] Bản vẽ riêng MỘT dầm (D1, D2…): mặt đứng (chia đoạn khi dài), mặt cắt, bảng thống kê cốt thép của riêng dầm đó.</summary>
public static class BeamSheet
{
    /// <summary>Tên hiển thị trong danh sách chọn dầm: "D1 – tầng 2 – 220×300 – 3 nhịp – SL=2".</summary>
    public static string Label(BeamLineDesign d) =>
        $"{d.Name} – {d.Story} – {d.B * 1000:0}×{d.H * 1000:0} – {d.Spans.Count} nhịp, L = {d.TotalLength:0.00} m – SL={Math.Max(1, d.Quantity)}{(d.Ok ? "" : " ⚠")}";

    /// <summary>Các hình của một dầm: mặt đứng + mặt cắt (+ danh mục mặt cắt), bảng thống kê thép (tuỳ chọn).</summary>
    public static List<Figure> Figures(Project p, DesignSummary s, BeamLineDesign d, bool schedule = true, string paper = null)
    {
        var figs = new List<Figure>();
        if (d == null) return figs;
        double maxW = SheetComposer.Paper(paper ?? DrawingSet.Paper).W - 60;
        figs.AddRange(BeamDrawing.BuildAllSplit(p, new[] { d }, s, maxW));
        if (schedule)
        {
            var one = new DesignSummary();
            one.Beams.Add(d);
            var sch = RebarSchedule.BuildFigures(one, 1);
            if (sch.Count > 0) sch[0].NewRow = true;
            figs.AddRange(sch);
        }
        return figs;
    }

    /// <summary>Tờ bản vẽ (khung tên) của một dầm.</summary>
    public static DxfWriter Compose(Project p, DesignSummary s, BeamLineDesign d, string sheetNo = "KC-04", string paper = null)
    {
        var figs = Figures(p, s, d, true, paper);
        var info = new SheetComposer.SheetInfo
        {
            ProjectName = p?.Name ?? "", Title = "CHI TIẾT DẦM " + RebarSchedule.MemberLabel(d.Name, d.Quantity), SheetNo = sheetNo,
            Designer = p?.Designer ?? "", Owner = p?.Owner ?? "", Location = p?.Location ?? ""
        };
        return SheetComposer.Compose(figs, BeamDrawing.CurrentScale, info, paper ?? DrawingSet.Paper);
    }

    /// <summary>Xuất bản vẽ một dầm ra tệp (DXF / DWG theo định dạng); trả về đường dẫn thực.</summary>
    public static string Export(Project p, DesignSummary s, BeamLineDesign d, string path, CadOutputFormat? fmt = null)
    {
        var w = Compose(p, s, d);
        return fmt.HasValue ? CadFileWriters.Write(w, path, fmt.Value) : CadFileWriters.Write(w, path);
    }
}

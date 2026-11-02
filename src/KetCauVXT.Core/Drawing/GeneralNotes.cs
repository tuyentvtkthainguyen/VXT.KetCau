using System.Globalization;
using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Phạm vi của khối ghi chú chung: chỉ ghi các mục liên quan tới loại bản vẽ.</summary>
public enum NotesScope { All, Plan, Foundation, Column, Beam, Slab, Schedule }

/// <summary>Khối "GHI CHÚ CHUNG" – MỘT khối cho mỗi bản vẽ/tờ: đơn vị, mác bê tông, thép, lớp bảo vệ, neo nối.
/// Các ghi chú chung trước đây lặp trên từng hình (vd. "chiều dài thép dọc tính theo cung; dùng đai kín móc 135°"
/// ghi dưới mỗi dầm cong) được gom vào đây.</summary>
public static class GeneralNotes
{
    public const string Heading = "GHI CHÚ CHUNG";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string Mm(double m) => Math.Round(m * 1000).ToString("0", Inv);

    /// <summary>Có dầm cong (trong mô hình hoặc trong kết quả thiết kế).</summary>
    public static bool HasCurvedBeams(Project p, DesignSummary s = null) =>
        (s?.Beams.Any(b => b.IsCurved) ?? false) || (p?.Frames.Any(f => f.Kind == FrameKind.Beam && f.IsCurved) ?? false);

    /// <summary>Các dòng ghi chú (đã đánh số) cho phạm vi scope.</summary>
    public static List<string> Lines(Project p, NotesScope scope, DesignSummary s = null)
    {
        var c = p?.Concrete ?? new ConcreteDesignSettings();
        var res = new List<string>();
        bool all = scope == NotesScope.All;
        res.Add(scope == NotesScope.Schedule
            ? "Chiều dài thanh tính bằng mm (làm tròn 10 mm), tổng chiều dài tính bằng m; trọng lượng = 0,00617·d²·L (kg)."
            : "Kích thước ghi trên bản vẽ tính bằng mm, cao độ tính bằng m.");
        string conc = scope switch
        {
            NotesScope.Foundation => $"Bê tông móng {c.ConcreteFooting}; bê tông lót móng B7,5 dày 100.",
            NotesScope.Column => $"Bê tông cột {c.ConcreteColumn}.",
            NotesScope.Beam => $"Bê tông dầm {c.ConcreteBeam}.",
            NotesScope.Slab => $"Bê tông sàn {c.ConcreteSlab}.",
            NotesScope.Schedule => "",
            _ => $"Bê tông: cột {c.ConcreteColumn}, dầm {c.ConcreteBeam}, sàn {c.ConcreteSlab}" + (all ? $", móng {c.ConcreteFooting}; bê tông lót móng B7,5 dày 100." : ".")
        };
        if (conc.Length > 0) res.Add(conc);
        bool slabToo = scope == NotesScope.All || scope == NotesScope.Plan || scope == NotesScope.Schedule;
        res.Add(scope == NotesScope.Slab ? $"Thép sàn {c.RebarStirrup}."
                                         : $"Cốt thép dọc chịu lực {c.RebarMain}; cốt đai{(slabToo ? " và thép sàn" : "")} {c.RebarStirrup}.");
        string cover = scope switch
        {
            NotesScope.Foundation => $"móng {Mm(c.CoverFooting)}",
            NotesScope.Column => $"cột {Mm(c.CoverColumn)}",
            NotesScope.Beam => $"dầm {Mm(c.CoverBeam)}",
            NotesScope.Slab => $"sàn {Mm(c.CoverSlab)}",
            NotesScope.Schedule => "",
            _ => $"dầm {Mm(c.CoverBeam)}, cột {Mm(c.CoverColumn)}, sàn {Mm(c.CoverSlab)}, móng {Mm(c.CoverFooting)}"
        };
        if (cover.Length > 0) res.Add("Chiều dày lớp bê tông bảo vệ: " + cover + ".");
        if (scope != NotesScope.Schedule)
            res.Add("Neo, nối cốt thép theo TCVN 5574:2018; mối nối so le, không nối quá 50% số thanh tại một mặt cắt.");
        if ((all || scope == NotesScope.Beam || scope == NotesScope.Plan) && HasCurvedBeams(p, s))
            res.Add("Dầm cong: mặt đứng thể hiện trải theo cung, chiều dài thép dọc tính theo cung; dùng đai kín móc 135°.");
        if (scope != NotesScope.Schedule)
            res.Add("Số hiệu thép trên bản vẽ trùng số hiệu trong bảng thống kê cốt thép.");
        if (all)
        {
            res.Add(StandardsLine(p));
            res.Add("Thi công và nghiệm thu theo TCVN 4453:1995, TCVN 9115:2019 và các tiêu chuẩn hiện hành.");
            res.Add("Khi có sai khác giữa bản vẽ và hiện trường phải báo cho đơn vị thiết kế để xử lý.");
        }
        for (int i = 0; i < res.Count; i++) res[i] = (i + 1).ToString(Inv) + ". " + res[i];
        return res;
    }

    /// <summary>Dòng ghi chú tiêu chuẩn thiết kế + căn cứ pháp lý (tra danh mục văn bản – Model/Regulations.cs; chỉ văn bản đang áp dụng).</summary>
    public static string StandardsLine(Project p)
    {
        var stds = RegulationService.Applied(p).Where(x => !RegulationService.IsLegal(x)).Select(x => x.Citation).ToList();
        return "Tiêu chuẩn thiết kế: " + (stds.Count > 0 ? string.Join(", ", stds) : Std.Concrete + ", " + Std.Loads)
             + $"; căn cứ {Std.Law}, {Std.Decree}.";
    }

    /// <summary>Mảnh hình khối ghi chú ở tỷ lệ 1/S: tiêu đề "GHI CHÚ CHUNG" gạch chân (cỡ tên hình) + các dòng (cỡ ghi chú),
    /// dòng dài tự ngắt ~maxChars ký tự. null nếu Thiết lập bản vẽ tắt ghi chú chung.</summary>
    public static DxfWriter Figure(IEnumerable<string> lines, double S, int maxChars = 110, bool force = false)
    {
        var w = new DxfWriter(S);
        if (!force && !w.Style.ShowGeneralNotes) return null;
        double th = w.Th, hh = w.TitleTh;
        double tw = DxfWriter.TextWidth(Heading, hh) * w.WidthFactor;
        double y = 0;
        w.Text(Lay.Chu, 0, y, hh, Heading);
        w.Line(Lay.Chu, 0, y - 0.35 * th, tw, y - 0.35 * th);
        y -= hh + 1.2 * th;
        foreach (var raw in lines ?? Enumerable.Empty<string>())
        {
            string line = (raw ?? "").TrimEnd();
            if (line.Trim().Length == 0) { y -= 0.8 * th; continue; }
            while (line.Length > 0)
            {
                string part = line;
                if (part.Length > maxChars)
                {
                    int cut = part.LastIndexOf(' ', maxChars);
                    if (cut < maxChars / 3) cut = maxChars;
                    part = line.Substring(0, cut);
                }
                w.Text(Lay.Chu, 0, y, th, part);
                y -= 1.8 * th;
                line = line.Substring(part.Length).TrimStart();
                if (line.Length > 0) line = "   " + line;
            }
        }
        return w;
    }

    /// <summary>Mảnh hình ghi chú chung của bản vẽ phạm vi scope (null nếu tắt).</summary>
    public static Figure FigureFor(Project p, NotesScope scope, double S, DesignSummary s = null)
    {
        var w = Figure(Lines(p, scope, s), S);
        return w == null ? null : new Figure(w, "Ghi chú chung", true);
    }

    /// <summary>Số khối "GHI CHÚ CHUNG" trong một bộ ghi (kiểm thử: mỗi bản vẽ đúng một khối).</summary>
    public static int Count(DxfWriter w) => w?.Texts().Count(t => t.Trim() == Heading) ?? 0;
}

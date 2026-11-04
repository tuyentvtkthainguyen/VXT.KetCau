namespace KetCauVXT.Report;

/// <summary>Phần mở rộng DocxWriter cho hướng dẫn sử dụng (Docs/UserGuide.cs): đoạn nhiều run có thụt lề treo (gạch đầu dòng, bước)
/// và nền tô (ô ghi chú / cảnh báo).</summary>
public partial class DocxWriter
{
    /// <summary>Đoạn văn nhiều run, thụt trái leftTwips với dòng đầu treo hangingTwips (0 = không thụt), nền tô fill (hex RRGGBB, null = không tô).</summary>
    public DocxWriter RichBlock(int leftTwips, int hangingTwips, string fill, params (string text, bool bold, bool italic)[] runs)
    {
        _body.Append("<w:p><w:pPr>");
        if (!string.IsNullOrEmpty(fill))
            _body.Append("<w:pBdr><w:top w:val=\"single\" w:sz=\"4\" w:space=\"2\" w:color=\"BFBFBF\"/><w:left w:val=\"single\" w:sz=\"4\" w:space=\"4\" w:color=\"BFBFBF\"/>")
                 .Append("<w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"2\" w:color=\"BFBFBF\"/><w:right w:val=\"single\" w:sz=\"4\" w:space=\"4\" w:color=\"BFBFBF\"/></w:pBdr>")
                 .Append($"<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"{Esc(fill)}\"/>");
        if (leftTwips > 0) _body.Append($"<w:ind w:left=\"{leftTwips}\" w:hanging=\"{Math.Max(0, hangingTwips)}\"/>");
        _body.Append("<w:jc w:val=\"both\"/></w:pPr>");
        foreach (var r in runs ?? Array.Empty<(string, bool, bool)>()) _body.Append(Run(r.text, r.bold, r.italic));
        _body.Append("</w:p>");
        return this;
    }
}

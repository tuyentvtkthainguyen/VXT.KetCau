namespace KetCauVXT.App;

// [Bản vẽ kiểu Revit] kích đúp khung nhìn mặt bằng trên tờ → mở đúng mặt bằng (tầng) trong không gian vẽ.
public sealed partial class DetailingPlanCanvas
{
    /// <summary>Chọn mặt bằng có tên nằm trong tiêu đề khung nhìn (vd. "MẶT BẰNG KẾT CẤU TẦNG 2 (+3.600)" → "Tầng 2";
    /// "MẶT BẰNG KẾT CẤU MÓNG" → mặt bằng móng). Trả về true nếu tìm thấy.</summary>
    public bool ShowLevelByCaption(string caption)
    {
        if (string.IsNullOrWhiteSpace(caption)) return false;
        string c = caption.ToUpperInvariant();
        int idx = -1, best = -1;
        for (int i = 0; i < Plan.Levels.Count; i++)
        {
            var lv = Plan.Levels[i];
            string n = (lv.Name ?? "").ToUpperInvariant();
            bool hit = n.Length > 0 && c.Contains(n) || lv.IsFoundation && c.Contains("MÓNG");
            if (hit && n.Length > best) { best = n.Length; idx = i; }
        }
        if (idx < 0) return false;
        if (idx != _level.SelectedIndex) _level.SelectedIndex = idx;
        Redraw();
        return true;
    }
}

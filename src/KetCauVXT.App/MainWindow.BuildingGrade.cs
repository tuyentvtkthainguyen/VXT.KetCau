using System.Windows.Controls;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>[Cấp công trình] Chọn cấp công trình ở Thông tin công trình (Thông tư 06/2021/TT-BXD); gợi ý theo số tầng nổi.
/// Ghi vào thuyết minh tính toán và bảng tính Excel.</summary>
public partial class MainWindow
{
    private bool _gradeHooked;

    private void LoadGradeUi()
    {
        if (cbGrade == null || _p == null) return;
        int i = Array.IndexOf(Project.BuildingGrades, _p.BuildingGrade ?? "");
        cbGrade.SelectedIndex = i >= 0 ? i + 1 : 0;
        if (!_gradeHooked)
        {
            _gradeHooked = true;
            cbGrade.SelectionChanged += (s, e) =>
            {
                if (_p == null) return;
                string g = cbGrade.SelectedIndex > 0 ? Project.BuildingGrades[cbGrade.SelectedIndex - 1] : "";
                if (g == (_p.BuildingGrade ?? "")) return;
                _p.BuildingGrade = g;
                MarkDirty(false);
                UpdateGradeHint();
                SetStatus(g.Length > 0 ? $"Cấp công trình: {g} (ghi vào thuyết minh, bảng tính)." : "Đã bỏ chọn cấp công trình.");
            };
            txtHeights.TextChanged += (s, e) => UpdateGradeHint();
        }
        UpdateGradeHint();
    }

    /// <summary>Số tầng nổi theo ô chiều cao tầng (hoặc mô hình) → gợi ý cấp.</summary>
    private void UpdateGradeHint()
    {
        if (txtGradeHint == null) return;
        int n = 0;
        try { n = ParseList(txtHeights.Text).Count; } catch (Exception) { }
        if (n == 0) n = _p?.Stories.Count(s => s.Elevation >= -0.01) ?? 0;
        string sg = Project.SuggestGrade(n);
        txtGradeHint.Text = sg.Length > 0 ? $"gợi ý theo {n} tầng: {sg}" : "";
        txtGradeHint.ToolTip = "Gợi ý theo tiêu chí số tầng (TT 06/2021/TT-BXD, Phụ lục I): 1 tầng → IV; 2–7 → III; 8–24 → II; 25–50 → I; > 50 → đặc biệt. " +
                               "Cấp chính thức còn xét chiều cao, nhịp, độ sâu tầng hầm, tổng diện tích sàn… theo Thông tư.";
    }
}

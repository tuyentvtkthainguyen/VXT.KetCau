using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Dòng bảng "Nguồn khối lượng": trường hợp tải × hệ số.</summary>
internal sealed class MassSourceRow : RowBase
{
    private string _pattern = "";
    private double _factor = 1;
    public string Pattern { get => _pattern; set { _pattern = value ?? ""; Raise(); } }
    public double Factor { get => _factor; set { _factor = value; Raise(); } }
}

/// <summary>Hộp thoại Phân tích → Dao động riêng…: nguồn khối lượng (như Mass Source của ETABS), sàn cứng, số dạng,
/// phương pháp giải, phương pháp động đất (tĩnh lực ngang tương đương | phổ phản ứng), tổ hợp CQC/SRSS, xoắn ngẫu nhiên.</summary>
internal sealed class ModalDialog : Window
{
    private readonly Project _p;
    private readonly ObservableCollection<MassSourceRow> _rows = new();
    private readonly DataGrid _grid = DialogUi.NewGrid();
    private readonly CheckBox _chkEnabled = new() { Content = "Chạy phân tích dao động riêng cùng phân tích tĩnh (F5)", Margin = new Thickness(0, 2, 0, 2) };
    private readonly CheckBox _chkDefault = new() { Content = "Nguồn khối lượng mặc định: tĩnh tải × 1,0 + hoạt tải × ψE (ψE = φ·ψ2 = 0,8 × 0,3 = 0,24; mái 0)", Margin = new Thickness(0, 2, 0, 2) };
    private readonly CheckBox _chkDia = new() { Content = "Sàn tuyệt đối cứng (diaphragm) – dùng chung cho phân tích tĩnh", Margin = new Thickness(0, 2, 0, 2) };
    private readonly CheckBox _chkVert = new() { Content = "Kể khối lượng theo phương đứng (UZ)", Margin = new Thickness(0, 2, 0, 2) };
    private readonly TextBox _txtModes = new() { Width = 60 };
    private readonly ComboBox _cbSolver = new() { Width = 200 };
    private readonly ComboBox _cbMethod = new() { Width = 220 };
    private readonly ComboBox _cbComb = new() { Width = 80 };
    private readonly TextBox _txtDamp = new() { Width = 50 };
    private readonly CheckBox _chkTors = new() { Content = "Xoắn ngẫu nhiên, độ lệch tâm e = ", VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _txtEcc = new() { Width = 50 };
    private readonly TextBox _txtScale = new() { Width = 50 };
    private readonly CheckBox _chkT1 = new() { Content = "Phương pháp tĩnh: dùng T1 từ phân tích dao động riêng, giới hạn ≤ ", VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _txtCap = new() { Width = 50 };

    public ModalDialog(Project p)
    {
        _p = p;
        DialogUi.Setup(this, "Phân tích dao động riêng", 760, 680);
        Help.Attach(this, "phan-tich.dao-dong");   // nút "?" + F1 – Help.cs
        var ms = p.Modal ??= new ModalSettings();
        var names = p.Patterns.Select(t => t.Name).ToList();

        _chkEnabled.IsChecked = ms.Enabled;
        _chkDefault.IsChecked = ms.UseDefaultMassSource;
        _chkDia.IsChecked = p.UseRigidDiaphragm;
        _chkVert.IsChecked = ms.IncludeVerticalMass;
        _txtModes.Text = ms.NumModes.ToString();
        _cbSolver.ItemsSource = new[] { ModalSettings.SolverAuto, ModalSettings.SolverDense, ModalSettings.SolverSubspace };
        _cbSolver.SelectedItem = ms.Solver;
        if (_cbSolver.SelectedIndex < 0) _cbSolver.SelectedIndex = 0;
        _cbMethod.ItemsSource = SeismicMethods.All;
        _cbMethod.SelectedItem = p.Seismic.Method;
        if (_cbMethod.SelectedIndex < 0) _cbMethod.SelectedIndex = 0;
        _cbComb.ItemsSource = new[] { "CQC", "SRSS" };
        _cbComb.SelectedItem = string.Equals(ms.Combination, "SRSS", StringComparison.OrdinalIgnoreCase) ? "SRSS" : "CQC";
        _txtDamp.Text = DialogUi.F(ms.Damping);
        _chkTors.IsChecked = ms.AccidentalTorsion;
        _txtEcc.Text = DialogUi.F(ms.Eccentricity);
        _txtScale.Text = DialogUi.F(ms.ScaleToStatic);
        _chkT1.IsChecked = ms.UseModalT1;
        _txtCap.Text = DialogUi.F(ms.ModalT1Cap);

        foreach (var it in (ms.UseDefaultMassSource || ms.MassSource.Count == 0 ? ms.DefaultTable(p) : ms.MassSource))
            _rows.Add(new MassSourceRow { Pattern = it.Pattern, Factor = it.Factor });
        _grid.ItemsSource = _rows;
        _grid.Columns.Add(DialogUi.Combo("Trường hợp tải", "Pattern", names, 200));
        _grid.Columns.Add(DialogUi.Num("Hệ số khối lượng", "Factor", "0.###", false, 140));
        _grid.Height = 170;
        void SyncGrid() => _grid.IsEnabled = _chkDefault.IsChecked != true;
        _chkDefault.Checked += (s, e) =>
        {
            _rows.Clear();
            foreach (var it in ms.DefaultTable(p)) _rows.Add(new MassSourceRow { Pattern = it.Pattern, Factor = it.Factor });
            SyncGrid();
        };
        _chkDefault.Unchecked += (s, e) => SyncGrid();
        SyncGrid();

        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(_chkEnabled);
        sp.Children.Add(DialogUi.Label("Nguồn khối lượng (Mass Source) – khối lượng = Σ hệ số × tải theo phương trọng lực / g (g = 9,81 m/s²)", double.NaN, true));
        sp.Children.Add(_chkDefault);
        sp.Children.Add(_grid);
        sp.Children.Add(DialogUi.Row(
            DialogUi.Btn("Thêm dòng", (s, e) => { DialogUi.Commit(_grid); _rows.Add(new MassSourceRow { Pattern = names.FirstOrDefault() ?? "", Factor = 1 }); }),
            DialogUi.Btn("Xoá dòng", (s, e) => { DialogUi.Commit(_grid); if (_grid.SelectedItem is MassSourceRow r) _rows.Remove(r); })));
        sp.Children.Add(DialogUi.Hint("Trường hợp có hệ số trọng lượng bản thân (TT) gồm trọng lượng dầm, cột, sàn; tường và hoàn thiện dồn lên dầm được quy về nút theo quy tắc đòn bẩy. "
            + "TCVN 9386 mục 3.2.4 (số điều theo bản 2012): G + ψE·Q, ψE = φ·ψ2."));
        sp.Children.Add(DialogUi.Label("Mô hình và lời giải", double.NaN, true));
        sp.Children.Add(_chkDia);
        sp.Children.Add(_chkVert);
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Số dạng dao động (0 = tự động max(12; 3 × số tầng)):"), _txtModes));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Phương pháp giải trị riêng:"), _cbSolver));
        sp.Children.Add(DialogUi.Hint("Tự động: khử tĩnh các bậc tự do không khối lượng rồi giải đầy đủ (Householder + QL) khi ≤ 800 bậc tự do động; ngược lại lặp không gian con (Bathe), sai số 1e-8."));
        sp.Children.Add(DialogUi.Label("Động đất", double.NaN, true));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Phương pháp:"), _cbMethod));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Tổ hợp dạng dao động:"), _cbComb, DialogUi.Label("  tỷ số cản ζ ="), _txtDamp));
        sp.Children.Add(DialogUi.Row(_chkTors, _txtEcc, DialogUi.Label("·L (mô men xoắn tầng Mi = e·Li·Fi)")));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Hiệu chỉnh lực cắt đáy phổ phản ứng ≥"), _txtScale, DialogUi.Label("× lực cắt đáy tĩnh (0 = không hiệu chỉnh; tuỳ chọn, TCVN 9386 không bắt buộc)")));
        sp.Children.Add(DialogUi.Row(_chkT1, _txtCap, DialogUi.Label("× Ct·H^0,75 (0 = không giới hạn)")));
        sp.Children.Add(DialogUi.Hint("Phổ phản ứng: kết quả DDX/DDY là giá trị không dấu (CQC/SRSS); các tổ hợp G + ψE·Q ± E1 ± 0,3·E2 lấy cả hai dấu như cặp bao +/− của ETABS. "
            + "Mặc định vẫn là tĩnh lực ngang tương đương."));

        var ok = DialogUi.Btn("Đồng ý", (s, e) => { Apply(); DialogResult = true; }, true);
        ok.IsDefault = true;
        var cancel = DialogUi.Btn("Huỷ", (s, e) => DialogResult = false);
        cancel.IsCancel = true;
        var buttons = DialogUi.Row(ok, cancel);
        buttons.HorizontalAlignment = HorizontalAlignment.Right;
        var dock = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        buttons.Margin = new Thickness(10, 4, 10, 10);
        dock.Children.Add(buttons);
        dock.Children.Add(new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = dock;
    }

    private void Apply()
    {
        DialogUi.Commit(_grid);
        var ms = _p.Modal;
        ms.Enabled = _chkEnabled.IsChecked == true;
        ms.UseDefaultMassSource = _chkDefault.IsChecked == true;
        ms.MassSource = _rows.Where(r => !string.IsNullOrWhiteSpace(r.Pattern)).Select(r => new MassSourceItem(r.Pattern, r.Factor)).ToList();
        _p.UseRigidDiaphragm = _chkDia.IsChecked == true;
        ms.IncludeVerticalMass = _chkVert.IsChecked == true;
        ms.NumModes = Math.Max(0, (int)Math.Round(DialogUi.ParseNum(_txtModes.Text, ms.NumModes)));
        ms.Solver = _cbSolver.SelectedItem as string ?? ModalSettings.SolverAuto;
        _p.Seismic.Method = _cbMethod.SelectedItem as string ?? SeismicMethods.Static;
        ms.Combination = _cbComb.SelectedItem as string ?? "CQC";
        ms.Damping = Math.Clamp(DialogUi.ParseNum(_txtDamp.Text, ms.Damping), 0.001, 0.5);
        ms.AccidentalTorsion = _chkTors.IsChecked == true;
        ms.Eccentricity = Math.Clamp(DialogUi.ParseNum(_txtEcc.Text, ms.Eccentricity), 0, 0.5);
        ms.ScaleToStatic = Math.Max(0, DialogUi.ParseNum(_txtScale.Text, ms.ScaleToStatic));
        ms.UseModalT1 = _chkT1.IsChecked == true;
        ms.ModalT1Cap = Math.Max(0, DialogUi.ParseNum(_txtCap.Text, ms.ModalT1Cap));
    }
}

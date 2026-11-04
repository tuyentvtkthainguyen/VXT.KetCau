using System.Windows;
using System.Windows.Controls;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Hộp "Tuỳ chọn xuất bản vẽ": Kiểu xuất (Gộp 1 file / Tách rời từng bản vẽ), mẫu tên file, nhớ lựa chọn
/// (%AppData%\KetCauVXT\XuatBanVe.json – DrawingExportOptions.LoadUser/SaveUser).</summary>
internal sealed class ExportOptionsDialog : Window
{
    private readonly ComboBox _cbMode = new() { Width = 260 };
    private readonly TextBox _tbPattern = new() { Width = 260 };
    private readonly TextBox _tbSingle = new() { Width = 260 };
    private readonly ComboBox _cbFormat = new() { Width = 260 };
    private readonly bool _pdf;
    private readonly CheckBox _chkRemember = new() { Content = "Ghi nhớ lựa chọn cho lần sau", IsChecked = true, Margin = new Thickness(0, 6, 0, 0) };
    private readonly TextBlock _lblPattern, _lblSingle;
    public DrawingExportOptions Result { get; private set; }

    private ExportOptionsDialog(Window owner, string what, DrawingExportOptions start, bool pdf)
    {
        Owner = owner;
        DialogUi.Setup(this, "Tuỳ chọn xuất – " + what, 470, 300);
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        MinHeight = 0;
        _cbMode.Items.Add(new ComboBoxItem { Content = pdf ? "Gộp 1 file (PDF nhiều trang)" : "Gộp 1 file (các bản vẽ đặt cạnh nhau)", Tag = DrawingExportMode.Single });
        _cbMode.Items.Add(new ComboBoxItem { Content = pdf ? "Tách rời từng tờ (mỗi tờ một PDF)" : "Tách rời từng bản vẽ (mỗi bản một file)", Tag = DrawingExportMode.Split });
        _cbMode.SelectedIndex = start.Mode == DrawingExportMode.Single ? 0 : 1;
        _tbPattern.Text = start.FilePattern ?? "";
        _tbSingle.Text = string.IsNullOrWhiteSpace(start.SingleFileName) ? "{duan}_BoBanVe" : start.SingleFileName;
        _tbPattern.ToolTip = "Để trống = tên mặc định (KC-01_MatBangKetCau…). Ví dụ: {so}_{ten} hoặc {duan}_{stt}_{so}";
        _tbSingle.ToolTip = "Tên file gộp (không cần .dxf/.pdf)";
        _cbMode.SelectionChanged += (s, e) => UpdateEnabled();
        _pdf = pdf;
        // định dạng file: theo Chuẩn layer CAD (CadStandard.OutputFormat) hoặc chọn riêng cho lần xuất này
        var stdFmt = CadStandard.Active?.OutputFormat ?? CadOutputFormat.DxfR12;
        _cbFormat.Items.Add(new ComboBoxItem { Content = "Theo Chuẩn layer CAD – " + CadOutputFormats.Label(stdFmt), Tag = null });
        foreach (var f in CadOutputFormats.All) _cbFormat.Items.Add(new ComboBoxItem { Content = CadOutputFormats.Label(f), Tag = f });
        _cbFormat.SelectedIndex = start.Format.HasValue ? 1 + Array.IndexOf(CadOutputFormats.All, start.Format.Value) : 0;
        if (_cbFormat.SelectedIndex < 0) _cbFormat.SelectedIndex = 0;
        _cbFormat.ToolTip = "DXF R12: mở được bằng mọi phần mềm CAD, tô vật liệu (nếu bật) mô phỏng bằng nét. " +
                            "DXF 2018 / DWG 2018: HATCH thật, LWPOLYLINE, nét in theo layer, chữ tiếng Việt Unicode." +
                            (CadFileWriters.HasWriter ? "" : " (Chưa có bộ ghi DXF 2018/DWG – sẽ ghi DXF R12.)");

        var sp = new StackPanel { Margin = new Thickness(12) };
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Kiểu xuất:", 120, true), _cbMode));
        if (!pdf) sp.Children.Add(DialogUi.Row(DialogUi.Label("Định dạng file:", 120, true), _cbFormat));
        _lblSingle = DialogUi.Label("Tên file gộp:", 120);
        sp.Children.Add(DialogUi.Row(_lblSingle, _tbSingle));
        _lblPattern = DialogUi.Label("Mẫu tên từng file:", 120);
        sp.Children.Add(DialogUi.Row(_lblPattern, _tbPattern));
        sp.Children.Add(DialogUi.Hint("Mẫu tên: {so} số hiệu bản vẽ, {ten} tên bản vẽ, {duan} tên công trình, {stt} thứ tự 01, 02…, {ngay} ngày xuất. " +
                                      (pdf ? "" : "Gộp 1 file: mỗi bản vẽ giữ nguyên tỷ lệ, đặt trong khung bao riêng có tên ở phía trên, cạnh nhau theo phương X.")));
        sp.Children.Add(_chkRemember);
        var ok = DialogUi.Btn("Xuất", (s, e) => Accept(), true);
        ok.IsDefault = true;
        var cancel = DialogUi.Btn("Huỷ", (s, e) => DialogResult = false);
        cancel.IsCancel = true;
        var bs = DialogUi.Row(ok, cancel);
        bs.HorizontalAlignment = HorizontalAlignment.Right;
        bs.Margin = new Thickness(0, 10, 0, 0);
        sp.Children.Add(bs);
        Content = sp;
        UpdateEnabled();
    }

    private DrawingExportMode Mode => (_cbMode.SelectedItem as ComboBoxItem)?.Tag is DrawingExportMode m ? m : DrawingExportMode.Split;

    private void UpdateEnabled()
    {
        bool single = Mode == DrawingExportMode.Single;
        _tbSingle.IsEnabled = _lblSingle.IsEnabled = single;
        _tbPattern.IsEnabled = _lblPattern.IsEnabled = !single;
    }

    private void Accept()
    {
        Result = new DrawingExportOptions { Mode = Mode, FilePattern = _tbPattern.Text.Trim(), SingleFileName = _tbSingle.Text.Trim() };
        if (!_pdf && (_cbFormat.SelectedItem as ComboBoxItem)?.Tag is CadOutputFormat f) Result.Format = f;
        if (_chkRemember.IsChecked == true) Result.SaveUser();
        DialogResult = true;
    }

    /// <summary>Hỏi kiểu xuất (bắt đầu từ lựa chọn đã nhớ). null = huỷ.</summary>
    public static DrawingExportOptions Ask(Window owner, string what, bool pdf = false)
    {
        var d = new ExportOptionsDialog(owner, what, DrawingExportOptions.LoadUser(), pdf);
        return d.ShowDialog() == true ? d.Result : null;
    }
}

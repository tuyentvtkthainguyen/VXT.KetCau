using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using KetCauVXT.Import;

namespace KetCauVXT.App;

public partial class DxfImportDialog : Window
{
    private readonly DxfImportOptions _o;

    public DxfImportDialog(DxfImportOptions options, List<string> stories)
    {
        InitializeComponent();
        _o = options;
        txtGrid.Text = _o.GridLayer; txtCol.Text = _o.ColumnLayer; txtBeam.Text = _o.BeamLayer; txtSlab.Text = _o.SlabLayer;
        txtTol.Text = _o.SnapTolerance.ToString("0.###", CultureInfo.InvariantCulture);
        chkGrids.IsChecked = _o.ImportGrids; chkOrigin.IsChecked = _o.MoveToOrigin; chkSnap.IsChecked = _o.SnapColumnsToGrid;
        chkDouble.IsChecked = _o.DetectDoubleLineBeams; chkAutoSlab.IsChecked = _o.AutoSlabs; chkReplace.IsChecked = _o.ReplaceExisting;
        lstStories.ItemsSource = stories;
        lstStories.SelectAll();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(txtTol.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double tol) || tol < 0)
        {
            MessageBox.Show(this, "Sai số bắt điểm không hợp lệ.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var sel = lstStories.SelectedItems.OfType<string>().ToList();
        if (lstStories.Items.Count > 0 && sel.Count == 0)
        {
            MessageBox.Show(this, "Chọn ít nhất một tầng.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _o.GridLayer = txtGrid.Text.Trim(); _o.ColumnLayer = txtCol.Text.Trim();
        _o.BeamLayer = txtBeam.Text.Trim(); _o.SlabLayer = txtSlab.Text.Trim();
        _o.SnapTolerance = tol;
        _o.ScaleOverride = cbUnit.SelectedIndex switch { 1 => 0.001, 2 => 0.01, 3 => 1.0, _ => 0 };
        _o.ImportGrids = chkGrids.IsChecked == true; _o.MoveToOrigin = chkOrigin.IsChecked == true;
        _o.SnapColumnsToGrid = chkSnap.IsChecked == true; _o.DetectDoubleLineBeams = chkDouble.IsChecked == true;
        _o.AutoSlabs = chkAutoSlab.IsChecked == true; _o.ReplaceExisting = chkReplace.IsChecked == true;
        // chọn tất cả = để trống (mọi tầng)
        _o.Stories = sel.Count == lstStories.Items.Count ? new List<string>() : sel;
        DialogResult = true;
    }
}

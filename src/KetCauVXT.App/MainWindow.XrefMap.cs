using System.Windows;
using System.Windows.Controls;
using KetCauVXT.Import.Link;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>[Ánh xạ Xref] Cho bản vẽ liên kết (Xref) và mặt bằng kết cấu trùng nhau theo 3 cách:
/// tự động theo tên trục (bản vẽ do Việt Const xuất hoặc có ký hiệu trục giống mô hình), bấm 2 điểm tương ứng, hoặc nhập vector dời;
/// mỗi cách chọn được đối tượng dời: dời Xref đến kết cấu, hoặc dời toàn bộ mặt bằng kết cấu (toạ độ mô hình) đến Xref. Có hoàn tác.</summary>
public partial class MainWindow
{
    private Window _xmWin;

    private void BtnXrefMap_Click(object sender, RoutedEventArgs e) => OpenXrefMap();

    internal Window OpenXrefMap(CadLink pre = null)
    {
        if (_p == null) return null;
        if (_p.CadLinks.Count == 0)
        {
            MessageBox.Show(this, "Chưa có bản vẽ Xref nào – kéo thả file DWG / DXF vào mặt bằng hoặc bấm \"Gắn bản vẽ (Xref)…\" trước.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }
        _xmWin?.Close();
        var w = new Window { Owner = this, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize };
        DialogUi.Setup(w, "Ánh xạ Xref ↔ mặt bằng kết cấu", 560, 420);
        w.MinHeight = 0;
        var root = new StackPanel { Margin = new Thickness(12) };

        var cbLink = new ComboBox { Width = 380 };
        foreach (var l in _p.CadLinks) cbLink.Items.Add(new ComboBoxItem { Content = $"{l.DisplayName}  (tầng {l.Story})", Tag = l });
        cbLink.SelectedIndex = Math.Max(0, pre != null ? _p.CadLinks.IndexOf(pre) : _p.CadLinks.Count - 1);
        root.Children.Add(DialogUi.Row(DialogUi.Label("Bản vẽ Xref", 90), cbLink));

        var rbMoveX = new RadioButton { Content = "Dời Xref đến trùng mặt bằng kết cấu (toạ độ mô hình giữ nguyên)", IsChecked = true, Margin = new Thickness(0, 2, 0, 2), GroupName = "xmWhat" };
        var rbMoveM = new RadioButton { Content = "Dời mặt bằng kết cấu đến trùng Xref (toàn bộ mô hình đổi toạ độ theo bản vẽ)", Margin = new Thickness(0, 2, 0, 2), GroupName = "xmWhat" };
        var chkLinks = new CheckBox { Content = "Dời cả các Xref khác theo mặt bằng kết cấu (giữ vị trí đã căn của chúng)", IsChecked = true, Margin = new Thickness(20, 2, 0, 2), IsEnabled = false };
        rbMoveM.Checked += (s, e) => chkLinks.IsEnabled = true;
        rbMoveX.Checked += (s, e) => chkLinks.IsEnabled = false;
        var what = new StackPanel();
        what.Children.Add(rbMoveX); what.Children.Add(rbMoveM); what.Children.Add(chkLinks);
        root.Children.Add(new GroupBox { Header = "Đối tượng di chuyển", Content = what, Margin = new Thickness(0, 6, 0, 0) });

        var rbAuto = new RadioButton { Content = "Tự động theo tên trục – đặt Xref trùng mặt bằng kết cấu do phần mềm tạo (bản vẽ Việt Const xuất, hoặc có ký hiệu trục 1, 2…, A, B… giống mô hình)", IsChecked = true, Margin = new Thickness(0, 2, 0, 2), GroupName = "xmHow" };
        ((RadioButton)rbAuto).Content = new TextBlock { Text = (string)rbAuto.Content, TextWrapping = TextWrapping.Wrap, MaxWidth = 480 };
        var rbPick = new RadioButton { Content = "Bấm điểm: một điểm trên Xref và điểm tương ứng trên kết cấu (giao trục, nút – có bắt điểm)", Margin = new Thickness(0, 2, 0, 2), GroupName = "xmHow" };
        var rbVec = new RadioButton { Content = "Nhập vector dời", Margin = new Thickness(0, 2, 0, 2), GroupName = "xmHow" };
        var txDx = new TextBox { Width = 70, Text = "0", Margin = new Thickness(0, 0, 8, 0) };
        var txDy = new TextBox { Width = 70, Text = "0" };
        var how = new StackPanel();
        how.Children.Add(rbAuto); how.Children.Add(rbPick);
        how.Children.Add(DialogUi.Row(rbVec, DialogUi.Label("  dx (m)"), txDx, DialogUi.Label("dy (m)"), txDy));
        root.Children.Add(new GroupBox { Header = "Cách khớp", Content = how, Margin = new Thickness(0, 6, 0, 0) });

        var info = DialogUi.Hint("Có hoàn tác (Ctrl+Z). Dời mặt bằng kết cấu dời mọi toạ độ tuyệt đối: trục, nút, tường, lỗ sàn, cầu thang, kiến trúc, điện nước, hố khoan – cần chạy lại phân tích.");
        root.Children.Add(info);

        CadLink Sel() => (cbLink.SelectedItem as ComboBoxItem)?.Tag as CadLink;
        var btnRun = DialogUi.Btn("Thực hiện", (s, e) =>
        {
            var l = Sel();
            if (l == null) return;
            bool model = rbMoveM.IsChecked == true, links = chkLinks.IsChecked == true;
            if (rbPick.IsChecked == true)
            {
                w.Close();
                CadLinkStartAlign(l, model, links);
                return;
            }
            double dx, dy;
            if (rbVec.IsChecked == true)
            {
                dx = DialogUi.ParseNum(txDx.Text, double.NaN); dy = DialogUi.ParseNum(txDy.Text, double.NaN);
                if (!double.IsFinite(dx) || !double.IsFinite(dy)) { info.Text = "Nhập dx, dy dạng số (m)."; return; }
                // vector dời là vector của đối tượng được dời
                if (model) CadLinkMoveModel(l, dx, dy, links);
                else CadLinkMoveLink(l, dx, dy);
                w.Close();
                return;
            }
            var r = CadLinkAutoMatch(l, model, links);
            if (r == null) { info.Text = "Bản vẽ chưa đọc được."; return; }
            info.Text = r.Message;
            if (r.Ok) w.Close();
        }, true);
        root.Children.Add(DialogUi.Row(btnRun, DialogUi.Btn("Đóng", (s, e) => w.Close())));
        w.Content = root;
        w.Closed += (s, e) => { if (_xmWin == w) _xmWin = null; };
        _xmWin = w;
        w.Show();
        return w;
    }

    /// <summary>Dời bản vẽ liên kết một vector (toạ độ mô hình).</summary>
    internal bool CadLinkMoveLink(CadLink l, double dx, double dy)
    {
        if (_p == null || l == null) return false;
        if (l.Locked) { SetStatus($"Bản vẽ {l.DisplayName} đang khoá vị trí – bỏ khoá trước khi dời."); return false; }
        string snap = Snapshot();
        l.OffsetX = Math.Round(l.OffsetX + dx, 6); l.OffsetY = Math.Round(l.OffsetY + dy, 6);
        PushUndo(snap);
        MarkDirty(false);
        _clDraw.Remove(l.Id);
        RedrawPlan();
        _clWin?.Reload();
        SetStatus($"Đã dời Xref {l.DisplayName}: dx = {dx:0.###} m, dy = {dy:0.###} m (Ctrl+Z để hoàn tác).");
        return true;
    }

    /// <summary>Dời toàn bộ mặt bằng kết cấu (toạ độ mô hình) một vector; bản vẽ mốc giữ nguyên, các bản vẽ khác dời theo nếu chọn.</summary>
    internal ModelShift.Result CadLinkMoveModel(CadLink keep, double dx, double dy, bool moveOtherLinks)
    {
        if (_p == null) return null;
        string snap = Snapshot();
        var r = ModelShift.Translate(_p, dx, dy, keep?.Id, moveOtherLinks);
        PushUndo(snap);
        MarkDirty(true);
        _clDraw.Clear();
        RedrawPlan();
        _clWin?.Reload();
        SetStatus($"Đã dời mặt bằng kết cấu đến Xref {keep?.DisplayName}: dx = {dx:0.###} m, dy = {dy:0.###} m – {r.Summary}. Chạy lại phân tích; Ctrl+Z để hoàn tác.");
        return r;
    }

    /// <summary>Tự khớp theo tên trục; model = dời mặt bằng kết cấu thay vì dời Xref.</summary>
    internal CadLinkGridMatch.Result CadLinkAutoMatch(CadLink l, bool model, bool moveOtherLinks = true)
    {
        var g = l != null ? CadLinkCache.Get(l) : null;
        if (g == null) return null;
        var r = CadLinkGridMatch.Match(_p, l, g);
        if (!r.Ok) { SetStatus("Ánh xạ Xref: " + r.Message); return r; }
        if (model) CadLinkMoveModel(l, -r.Dx, -r.Dy, moveOtherLinks);
        else if (!CadLinkMoveLink(l, r.Dx, r.Dy)) { r.Ok = false; r.Message = "Bản vẽ đang khoá vị trí – bỏ khoá trước."; return r; }
        SetStatus("Ánh xạ Xref tự động: " + r.Message + (model ? " (đã dời mặt bằng kết cấu – chạy lại phân tích)" : " (đã dời Xref)"));
        return r;
    }
}

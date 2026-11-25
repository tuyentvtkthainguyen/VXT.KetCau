using System.Windows;
using System.Windows.Controls;
using KetCauVXT.Detailing;

namespace KetCauVXT.App;

/// <summary>[Tên trục] Đổi tên các trục X / Y một lần (danh sách cách nhau dấu cách hoặc dấu phẩy).</summary>
public partial class DetailingPlanCanvas
{
    private void RenameAxes()
    {
        if (!Plan.HasGrid) { _status.Text = "Chưa có hệ trục – tạo hệ trục trước."; return; }
        int nx = Plan.Xs.Length, ny = Plan.Ys.Length;
        var w = new Window { Owner = _owner, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize };
        DialogUi.Setup(w, "Tên trục", 560, 300);
        w.MinHeight = 0;
        var root = new StackPanel { Margin = new Thickness(12) };
        var tx = new TextBox { Width = 380, Text = string.Join(" ", Enumerable.Range(0, nx).Select(i => Plan.AxisLabel(true, i))) };
        var ty = new TextBox { Width = 380, Text = string.Join(" ", Enumerable.Range(0, ny).Select(i => Plan.AxisLabel(false, i))) };
        root.Children.Add(DialogUi.Row(DialogUi.Label($"Trục X ({nx} trục)", 130), tx));
        root.Children.Add(DialogUi.Row(DialogUi.Label($"Trục Y ({ny} trục)", 130), ty));
        var hint = DialogUi.Hint("Tên theo thứ tự từ trái sang phải / từ dưới lên, cách nhau dấu cách hoặc dấu phẩy (vd. 1 2 2' 3 4 – A B C). Thiếu tên → giữ tên mặc định.");
        hint.TextWrapping = TextWrapping.Wrap; hint.MaxWidth = 520;
        root.Children.Add(hint);
        var msg = new TextBlock { Foreground = System.Windows.Media.Brushes.DarkRed, TextWrapping = TextWrapping.Wrap };
        root.Children.Add(msg);
        List<string> Split(string t) => (t ?? "").Split(new[] { ' ', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        var ok = DialogUi.Btn("OK", (s, e) =>
        {
            var a = Split(tx.Text); var b = Split(ty.Text);
            if (a.Count > nx || b.Count > ny) { msg.Text = $"Nhiều tên hơn số trục (X: {a.Count}/{nx}, Y: {b.Count}/{ny})."; return; }
            var all = a.Concat(b).ToList();
            var dup = all.GroupBy(q => q, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (dup != null) { msg.Text = "Tên trục trùng nhau: " + dup.Key; return; }
            _undo.Push(Plan);
            Plan.SetAxisLabels(true, a); Plan.SetAxisLabels(false, b);
            GridChanged("Đã đổi tên trục.");
            w.Close();
        }, true);
        var reset = DialogUi.Btn("Về tên mặc định", (s, e) =>
        {
            tx.Text = string.Join(" ", Enumerable.Range(0, nx).Select(i => StructuralPlan.GridName(true, i)));
            ty.Text = string.Join(" ", Enumerable.Range(0, ny).Select(i => StructuralPlan.GridName(false, i)));
        });
        root.Children.Add(DialogUi.Row(ok, reset, DialogUi.Btn("Huỷ", (s, e) => w.Close())));
        w.Content = root;
        w.ShowDialog();
    }
}

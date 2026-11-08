using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using KetCauVXT.Model;

namespace KetCauVXT.App;

// =====================================================================
// "Chiều cao tầng…": sửa chiều cao, tên từng tầng và cao độ chân cột của dự án ĐANG CÓ mà giữ nguyên mô hình
// (cột, dầm, sàn, tường, cầu thang, ống điện nước dời theo cao độ mới – GridColumnSetup.ChangeStoryHeights; đổi tên → Levels.RenameStory).
// Cũng áp dụng khi sửa ô "Chiều cao các tầng" / "Cao độ chân cột" ở tab Công trình sau khi đã lập mô hình. Có hoàn tác (Ctrl+Z).
// =====================================================================
public partial class MainWindow
{
    private void StoryHeightsUi()
    {
        if (_p == null || _busy) return;
        var st = _p.Stories.OrderBy(s => s.Elevation).ToList();
        if (st.Count == 0) { OpenGridColumnDialog(); return; }   // chưa có tầng: lập lưới + tầng
        var rows = new ObservableCollection<GridColumnDialog.StoryVm>();
        double prev = _p.BaseElevation;
        foreach (var s in st) { rows.Add(new GridColumnDialog.StoryVm { Name = s.Name, Height = Math.Round(s.Elevation - prev, 4), Elevation = s.Elevation }); prev = s.Elevation; }

        var w = new Window { Owner = this, Title = "Chiều cao tầng – giữ nguyên mô hình", Width = 470, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Background = SystemColors.ControlBrush };
        var sp = new StackPanel { Margin = new Thickness(12) };
        var tBase = new TextBox { Text = F(_p.BaseElevation), Width = 80 };
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Cao độ chân cột (m)", 170), tBase));
        var g = DialogUi.NewGrid();
        g.ItemsSource = rows; g.Height = Math.Min(360, 60 + 26 * rows.Count);
        g.CanUserAddRows = false; g.CanUserDeleteRows = false;
        g.Columns.Add(DialogUi.Text("Tên tầng (sàn trên)", "Name", false, 140));
        g.Columns.Add(DialogUi.Num("Chiều cao (m)", "Height", "0.###", false, 100));
        g.Columns.Add(DialogUi.Num("Cao độ sàn (m)", "Elevation", "+0.000;-0.000", true, 110));
        void Recalc()
        {
            double z = DialogUi.ParseNum(tBase.Text, _p.BaseElevation);
            foreach (var r in rows) { z += r.Height; r.Elevation = Math.Round(z, 4); }
        }
        g.CellEditEnding += (s, e) => Dispatcher.BeginInvoke(new Action(Recalc));
        tBase.LostFocus += (s, e) => Recalc();
        sp.Children.Add(g);
        var tAll = new TextBox { Text = "3.3", Width = 50 };
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Đặt mọi tầng từ tầng 2 cao"), tAll, DialogUi.Label(" m "),
            DialogUi.Btn("Áp dụng", (s, e) => { DialogUi.Commit(g); double h = DialogUi.ParseNum(tAll.Text, 0); if (h >= 0.5) { for (int k = 1; k < rows.Count; k++) rows[k].Height = h; Recalc(); } })));
        sp.Children.Add(DialogUi.Hint("Cột, dầm, sàn, tường, cầu thang, ống điện nước dời theo cao độ mới; nút giữa tầng (chiếu nghỉ…) nội suy. "
            + "Thêm / bớt tầng: dùng \"Chèn tầng…\" / \"Xoá tầng\" (tab BIM → Cao độ). Sau khi đổi cần chạy lại phân tích."));
        bool ok = false;
        var bOk = DialogUi.Btn("Áp dụng", (s, e) => { DialogUi.Commit(g); ok = true; w.DialogResult = true; }, true); bOk.IsDefault = true;
        var bNo = DialogUi.Btn("Huỷ", (s, e) => w.DialogResult = false); bNo.IsCancel = true;
        var bar = DialogUi.Row(bOk, bNo); bar.HorizontalAlignment = HorizontalAlignment.Right; bar.Margin = new Thickness(0, 10, 0, 0);
        sp.Children.Add(bar);
        w.Content = sp;
        w.ShowDialog();
        if (!ok) return;
        var names = rows.Select(r => (r.Name ?? "").Trim()).ToList();
        if (names.Any(n => n.Length == 0) || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
        { MessageBox.Show(this, "Tên tầng không được trống hoặc trùng nhau.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        ApplyStoryHeights(rows.Select(r => r.Height).ToList(), DialogUi.ParseNum(tBase.Text, _p.BaseElevation), names);
    }

    /// <summary>Đổi chiều cao (và tên) các tầng của mô hình đang có. Trả false nếu không đổi được.</summary>
    private bool ApplyStoryHeights(IList<double> heights, double baseElev, IList<string> names = null)
    {
        try
        {
            var st = _p.Stories.OrderBy(s => s.Elevation).ToList();
            if (heights.Count != st.Count)
            {
                MessageBox.Show(this, $"Dự án có {st.Count} tầng nhưng nhập {heights.Count} chiều cao. Muốn thêm / bớt tầng: dùng \"Chèn tầng…\" / \"Xoá tầng\" (tab BIM → Cao độ), hoặc \"Lưới trục + cột + tầng…\" để tạo lại khung.",
                    AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            string snap = Snapshot();
            int moved = GridColumnSetup.ChangeStoryHeights(_p, heights, baseElev);
            int renamed = 0;
            if (names != null && names.Count == st.Count)
            {
                // đổi tên qua tên tạm để không đụng nhau (vd T2 ↔ T3)
                var tmp = st.Select((s, i) => (s, t: $"__tmp{i}__")).ToList();
                var old = st.Select(s => s.Name).ToList();
                for (int i = 0; i < st.Count; i++) if (old[i] != names[i]) Levels.RenameStory(_p, old[i], tmp[i].t);
                for (int i = 0; i < st.Count; i++) if (old[i] != names[i]) { Levels.RenameStory(_p, tmp[i].t, names[i]); renamed++; }
            }
            txtHeights.Text = string.Join(" ", heights.Select(h => F(h)));
            txtBaseElev.Text = F(baseElev);
            AfterModelEdit($"Đã đổi chiều cao tầng: {string.Join(" / ", _p.Stories.OrderBy(s => s.Elevation).Select(s => $"{s.Name} {s.Height:0.###} m"))}"
                           + $" – dời {moved} nút{(renamed > 0 ? $", đổi tên {renamed} tầng" : "")}, giữ nguyên mô hình. Chạy lại phân tích để cập nhật nội lực.", snap);
            return true;
        }
        catch (Exception ex) { ShowError("Không đổi được chiều cao tầng", ex); return false; }
    }

    private bool _heightsPrompting;

    /// <summary>Sửa ô "Chiều cao các tầng" / "Cao độ chân cột" ở tab Công trình khi đã có mô hình → hỏi áp dụng ngay (giữ mô hình).</summary>
    private void HeightsTextChanged()
    {
        if (_p == null || _busy || _heightsPrompting || _p.Frames.Count == 0 || _p.Stories.Count == 0) return;
        List<double> hs;
        try { hs = ParseList(txtHeights.Text); } catch (Exception) { return; }
        double baseElev = Num(txtBaseElev, _p.BaseElevation);
        var st = _p.Stories.OrderBy(s => s.Elevation).ToList();
        double prev = _p.BaseElevation;
        var cur = st.Select(s => { double h = s.Elevation - prev; prev = s.Elevation; return Math.Round(h, 4); }).ToList();
        bool same = hs.Count == cur.Count && hs.Zip(cur, (a, b) => Math.Abs(a - b) < 1e-6).All(x => x) && Math.Abs(baseElev - _p.BaseElevation) < 1e-6;
        if (same) return;
        _heightsPrompting = true;
        try
        {
            if (hs.Count != cur.Count)
            {
                MessageBox.Show(this, $"Mô hình có {cur.Count} tầng – ô chiều cao đang có {hs.Count} giá trị. Muốn thêm / bớt tầng: dùng \"Chèn tầng…\" / \"Xoá tầng\" (tab BIM → Cao độ) hoặc \"Lưới trục + cột + tầng…\".",
                    AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                txtHeights.Text = string.Join(" ", cur.Select(h => F(h)));
                return;
            }
            var ans = MessageBox.Show(this, $"Áp dụng chiều cao tầng mới cho mô hình đang có?\n{string.Join(" / ", hs.Select(h => F(h)))} m, chân cột {F(baseElev)} m\n\nCột, dầm, sàn, tường… dời theo cao độ mới, giữ nguyên mô hình.",
                AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ans == MessageBoxResult.Yes) ApplyStoryHeights(hs, baseElev);
            else { txtHeights.Text = string.Join(" ", cur.Select(h => F(h))); txtBaseElev.Text = F(_p.BaseElevation); }
        }
        finally { _heightsPrompting = false; }
    }

    private void MnuStoryHeights_Click(object sender, RoutedEventArgs e) => StoryHeightsUi();
    private void TxtHeights_LostFocus(object sender, RoutedEventArgs e) => Dispatcher.BeginInvoke(new Action(HeightsTextChanged));
}

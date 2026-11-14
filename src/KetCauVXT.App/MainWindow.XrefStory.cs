using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>[Xref theo tầng] Mỗi tầng có chế độ hiển thị Xref riêng: Hiện / Mờ (tham chiếu – không bắt điểm, ví dụ xem mặt bằng tầng dưới khi vẽ
/// tầng trên) / Ẩn, kèm độ đậm riêng. Bảng ma trận bản vẽ × tầng; nút "Ẩn / hiện", thanh độ mờ ở tab Link CAD chỉ tác động tầng đang xem.</summary>
public partial class MainWindow
{
    private void BtnXrefStory_Click(object sender, RoutedEventArgs e) => OpenXrefStoryViews();

    /// <summary>Ẩn / hiện các Xref của RIÊNG tầng đang xem.</summary>
    private void ToggleStoryLinks()
    {
        if (_p == null) return;
        string st = CurrentStory;
        if (st == AllStories) { ForStoryLinks(l => l.Visible = !l.Visible, "Đang xem mọi tầng: đã ẩn / hiện bản vẽ nền ở mọi tầng."); return; }
        var ls = _p.CadLinks.Where(l => l.ShownOn(st) || l.AppliesTo(st)).ToList();
        if (ls.Count == 0) { SetStatus("Tầng đang xem chưa có bản vẽ Xref."); return; }
        bool anyShown = ls.Any(l => l.ShownOn(st));
        string snap = Snapshot();
        foreach (var l in ls) l.SetStoryMode(st, anyShown ? CadLinkStoryView.Hide : CadLinkStoryView.Show, l.ViewOn(st)?.Opacity ?? 0);
        PushUndo(snap); MarkDirty(false);
        _clDraw.Clear();
        RedrawPlan();
        SetStatus($"Tầng {st}: đã {(anyShown ? "ẩn" : "hiện")} {ls.Count} bản vẽ Xref (tầng khác giữ nguyên).");
    }

    /// <summary>Bảng hiển thị Xref theo tầng (bản vẽ × tầng).</summary>
    internal Window OpenXrefStoryViews()
    {
        if (_p == null) return null;
        if (_p.CadLinks.Count == 0) { SetStatus("Chưa có bản vẽ Xref – kéo thả file DWG / DXF vào mặt bằng để gắn."); return null; }
        var stories = _p.Stories.OrderBy(s => s.Elevation).Select(s => s.Name).ToList();
        var links = _p.CadLinks.ToList();
        // bản sao để huỷ được
        var work = links.ToDictionary(l => l.Id, l => (l.StoryViews ?? new()).ToDictionary(kv => kv.Key, kv => new CadLinkStoryView { Mode = kv.Value?.Mode ?? CadLinkStoryView.Default, Opacity = kv.Value?.Opacity ?? 0 }));

        var w = new Window { Owner = this };
        DialogUi.Setup(w, "Hiển thị Xref theo tầng – mỗi tầng một chế độ riêng", Math.Min(1300, 360 + 150 * stories.Count), Math.Min(760, 260 + 34 * links.Count));
        var grid = new Grid { Margin = new Thickness(6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        foreach (var _ in stories) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        void Cell(UIElement el, int r, int c) { Grid.SetRow(el, r); Grid.SetColumn(el, c); grid.Children.Add(el); }
        TextBlock H(string t, bool cur = false) => new() { Text = t, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 2, 8, 6), Foreground = cur ? new SolidColorBrush(Color.FromRgb(0, 90, 170)) : Brushes.Black };
        Cell(H("Bản vẽ Xref"), 0, 0); Cell(H("Gắn cho"), 0, 1);
        for (int j = 0; j < stories.Count; j++) Cell(H(stories[j] + (stories[j] == CurrentStory ? "  ◄ đang xem" : ""), stories[j] == CurrentStory), 0, 2 + j);

        var combos = new List<(CadLink L, string St, ComboBox Cb)>();
        string DefText(CadLink l, string st) => CadLinkStoryView.Default + (l.Visible && l.AppliesTo(st) ? " (hiện)" : " (ẩn)");
        for (int i = 0; i < links.Count; i++)
        {
            var l = links[i];
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Cell(new TextBlock { Text = l.DisplayName, Margin = new Thickness(4, 4, 10, 4), VerticalAlignment = VerticalAlignment.Center, MaxWidth = 220, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = l.FilePath }, i + 1, 0);
            Cell(new TextBlock { Text = l.IsAllStories ? "mọi tầng" : l.Story, Margin = new Thickness(4, 4, 10, 4), VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray }, i + 1, 1);
            for (int j = 0; j < stories.Count; j++)
            {
                string st = stories[j];
                var cb = new ComboBox { Width = 140, Margin = new Thickness(2) };
                cb.Items.Add(DefText(l, st)); cb.Items.Add(CadLinkStoryView.Show); cb.Items.Add(CadLinkStoryView.Halftone); cb.Items.Add(CadLinkStoryView.Hide);
                string m = work[l.Id].TryGetValue(st, out var v) ? v.Mode : CadLinkStoryView.Default;
                cb.SelectedIndex = Math.Max(0, Array.IndexOf(CadLinkStoryView.Modes, m));
                cb.SelectionChanged += (s, e) =>
                {
                    string mode = CadLinkStoryView.Modes[Math.Max(0, cb.SelectedIndex)];
                    var d = work[l.Id];
                    if (mode == CadLinkStoryView.Default && !(d.TryGetValue(st, out var o) && o.Opacity > 0)) d.Remove(st);
                    else { if (!d.TryGetValue(st, out var o2)) d[st] = o2 = new CadLinkStoryView(); o2.Mode = mode; }
                };
                combos.Add((l, st, cb));
                Cell(cb, i + 1, 2 + j);
            }
        }

        void SetAll(Func<CadLink, string, int> pick)
        {
            foreach (var (l, st, cb) in combos) { int k = pick(l, st); if (k >= 0) cb.SelectedIndex = k; }
        }
        string cur = CurrentStory;
        var tools = DialogUi.Wrap(
            DialogUi.Btn("Mỗi bản vẽ chỉ hiện ở tầng được gắn", (s, e) => SetAll((l, st) => l.IsAllStories ? 0 : st == l.Story ? 1 : 3)),
            DialogUi.Btn("Thêm mờ tầng ngay dưới (tham chiếu)", (s, e) =>
            {
                // mỗi tầng: hiện bản vẽ của chính tầng, bản vẽ gắn cho tầng ngay dưới hiện mờ
                SetAll((l, st) =>
                {
                    if (l.IsAllStories) return -1;
                    int i = stories.IndexOf(st), k = stories.IndexOf(l.Story);
                    return k == i ? 1 : k == i - 1 ? 2 : 3;
                });
            }),
            DialogUi.Btn(cur == AllStories ? "Tất cả về mặc định" : $"Tầng {cur}: hiện tất cả", (s, e) => SetAll((l, st) => cur == AllStories ? 0 : st == cur ? 1 : -1)),
            DialogUi.Btn(cur == AllStories ? "—" : $"Tầng {cur}: ẩn tất cả", (s, e) => SetAll((l, st) => cur != AllStories && st == cur ? 3 : -1)),
            DialogUi.Btn("Tất cả về mặc định", (s, e) => SetAll((l, st) => 0)));
        var hint = DialogUi.Hint("Mặc định: bản vẽ hiện ở tầng được gắn (hoặc mọi tầng). Hiện: hiện cả ở tầng khác. Mờ (tham chiếu): hiện nhạt, KHÔNG bắt điểm – xem mặt bằng tầng dưới khi vẽ tầng trên. Ẩn: không hiện, không bắt điểm ở tầng đó. Độ đậm riêng từng tầng chỉnh bằng thanh \"Độ mờ\" ở tab Link CAD khi đang xem tầng đó.");
        hint.TextWrapping = TextWrapping.Wrap;
        var ok = DialogUi.Btn("OK", (s, e) =>
        {
            string snap = Snapshot();
            foreach (var l in links) l.StoryViews = work[l.Id];
            PushUndo(snap); MarkDirty(false);
            _clDraw.Clear();
            RedrawPlan();
            SyncCadTabFromLinks();
            SetStatus($"Đã đặt chế độ hiển thị Xref theo tầng ({links.Sum(l => l.StoryViews.Count)} ô riêng). Ctrl+Z để hoàn tác.");
            w.Close();
        }, true);
        var body = new StackPanel();
        body.Children.Add(new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 520 });
        body.Children.Add(tools); body.Children.Add(hint);
        w.Content = DialogUi.ScrollBody(body, DialogUi.Row(ok, DialogUi.Btn("Huỷ", (s, e) => w.Close())));
        w.Show();
        return w;
    }
}

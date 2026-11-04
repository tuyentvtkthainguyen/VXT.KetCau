using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Cây mô hình (Model Explorer của ETABS) – khung neo bên trái (cột colExplorer của MainWindow.xaml), bật/tắt bằng Xem → Cây mô hình (Ctrl+E):
/// Thông tin dự án; Lưới trục; Tầng; Vật liệu; Tiết diện; Tải trọng (trường hợp tải, trường hợp phân tích, tổ hợp); Đối tượng (cột, dầm, sàn, tường, móng
/// theo tầng – bấm để chọn và phóng tới); Kết quả (chu kỳ dao động, phản lực đáy) khi đã phân tích. Bấm đúp nút nhánh mở hộp thoại khai báo tương ứng.</summary>
public partial class MainWindow
{
    private TreeView _explorer;
    private bool _explorerVisible, _explorerPending;
    private readonly HashSet<string> _explorerExpanded = new() { "Đối tượng", "Tải trọng" };

    private void InitModelExplorer()
    {
        if (explorerHost == null) return;
        _explorer = new TreeView { BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(2) };
        _explorer.SelectedItemChanged += (s, e) => { if (e.NewValue is TreeViewItem ti) ExplorerSelect(ti.Tag); };
        _explorer.MouseDoubleClick += (s, e) => { if (_explorer.SelectedItem is TreeViewItem ti) ExplorerOpen(ti.Tag); };
        var head = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(228, 234, 242)) };
        var close = new Button { Content = "✕", Padding = new Thickness(5, 0, 5, 0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, ToolTip = "Ẩn cây mô hình (Ctrl+E)" };
        close.Click += (s, e) => ShowModelExplorer(false);
        DockPanel.SetDock(close, Dock.Right);
        head.Children.Add(close);
        head.Children.Add(new TextBlock { Text = "Cây mô hình", FontWeight = FontWeights.Bold, Margin = new Thickness(6, 3, 6, 3) });
        var dock = new DockPanel();
        DockPanel.SetDock(head, Dock.Top);
        dock.Children.Add(head);
        dock.Children.Add(_explorer);
        explorerHost.Child = dock;
    }

    private void MnuModelExplorer_Click(object sender, RoutedEventArgs e)
    {
        bool show = sender is MenuItem mi && ReferenceEquals(mi, mnuExplorer) ? mi.IsChecked : !_explorerVisible;
        ShowModelExplorer(show);
    }

    private void ShowModelExplorer(bool show)
    {
        _explorerVisible = show;
        if (mnuExplorer != null) mnuExplorer.IsChecked = show;
        colExplorer.Width = show ? new GridLength(250) : new GridLength(0);
        colExplorer.MinWidth = show ? 160 : 0;
        colExplorerSplit.Width = show ? new GridLength(5) : new GridLength(0);
        explorerHost.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        explorerSplitter.Visibility = explorerHost.Visibility;
        if (show) BuildModelExplorer();
    }

    /// <summary>Gộp các lần gọi, dựng lại cây khi rảnh (chỉ khi cây đang hiện).</summary>
    private void RefreshModelExplorer()
    {
        if (!_explorerVisible || _explorerPending || _explorer == null) return;
        _explorerPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { _explorerPending = false; BuildModelExplorer(); }));
    }

    private static TreeViewItem TNode(string text, object tag = null, string tip = null, bool bold = false)
    {
        var t = new TreeViewItem { Header = text, Tag = tag };
        if (tip != null) t.ToolTip = tip;
        if (bold) t.FontWeight = FontWeights.SemiBold;
        return t;
    }

    private static TreeViewItem TAdd(TreeViewItem parent, string text, object tag = null, string tip = null)
    {
        var t = TNode(text, tag, tip);
        parent.Items.Add(t);
        return t;
    }

    /// <summary>Thẻ hành động của nút cây.</summary>
    private sealed record ExplorerAction(string Kind, string Arg = null);

    private void BuildModelExplorer()
    {
        if (_explorer == null || _p == null) return;
        // ghi nhớ nhánh đang mở
        foreach (var it in _explorer.Items.OfType<TreeViewItem>())
        {
            string h = BaseHeader(it.Header as string);
            if (it.IsExpanded) _explorerExpanded.Add(h); else _explorerExpanded.Remove(h);
        }
        _explorer.Items.Clear();
        var p = _p;

        var info = TNode("Thông tin dự án", new ExplorerAction("project"), "Bấm đúp: thẻ Công trình", true);
        TAdd(info, "Tên: " + p.Name);
        if (!string.IsNullOrEmpty(p.Location)) TAdd(info, "Địa điểm: " + p.Location);
        TAdd(info, $"Gió: {(p.Wind.Enabled ? $"W0 = {p.Wind.W0:0.##} kN/m², địa hình {p.Wind.Terrain}" : "tắt")}");
        TAdd(info, $"Động đất: {(p.Seismic.Enabled ? $"agR = {p.Seismic.AgR:0.####} g, nền {p.Seismic.Soil}, {p.Seismic.Method}" : "tắt")}", new ExplorerAction("spectrum"));
        TAdd(info, "Đơn vị: kN, m, °C");

        var grids = TNode($"Lưới trục ({p.Grids.Count})", new ExplorerAction("project"), null, true);
        foreach (var g in p.GridsX.Concat(p.GridsY)) TAdd(grids, $"{g.Name}: {(g.Dir == GridDir.X ? "X" : "Y")} = {g.Coord:0.###} m");

        var stories = TNode($"Tầng ({p.Stories.Count})", null, null, true);
        foreach (var s in p.Stories.OrderByDescending(s => s.Elevation))
            TAdd(stories, $"{s.Name}  +{s.Elevation:0.000}  (h = {s.Height:0.###} m){(ModelAssign.DiaphragmOn(p, s) ? "  · sàn cứng" : "")}", new ExplorerAction("story", s.Name));

        var mats = TNode($"Vật liệu ({p.Materials.Count})", new ExplorerAction("materials"), "Bấm đúp: Khai báo → Vật liệu…", true);
        foreach (var m in p.Materials)
            TAdd(mats, m.Kind == MaterialKind.Concrete ? $"{m.Name}: Rb = {m.Rb / U.MPa:0.##}, Rbt = {m.Rbt / U.MPa:0.##}, E = {m.E / U.MPa:0} MPa"
                    : m.Kind == MaterialKind.Rebar ? $"{m.Name}: Rs = {m.Rs / U.MPa:0}, Rsw = {m.Rsw / U.MPa:0} MPa" : $"{m.Name}: f = {m.F / U.MPa:0}, E = {m.E / U.MPa:0} MPa",
                new ExplorerAction("materials"));

        var secs = TNode($"Tiết diện ({p.FrameSections.Count} thanh, {p.SlabSections.Count} sàn)", new ExplorerAction("sections"), "Bấm đúp: Khai báo → Tiết diện…", true);
        foreach (var s in p.FrameSections)
        {
            int n = p.Frames.Count(f => f.Section == s.Name);
            TAdd(secs, $"{s.Name} ({s.Material}) – {n} thanh", new ExplorerAction("section", s.Name), "Bấm: chọn mọi thanh tiết diện này ở tầng đang xem");
        }
        foreach (var s in p.SlabSections) TAdd(secs, $"Sàn {s.Name}: {s.Thickness * 1000:0} mm ({s.Material})");

        var loads = TNode("Tải trọng", null, null, true);
        var pats = TAdd(loads, $"Trường hợp tải ({p.Patterns.Count})", new ExplorerAction("patterns"), "Bấm đúp: Khai báo → Trường hợp tải…");
        foreach (var pt in p.Patterns)
        {
            var (f, a, n) = LoadPatternEditor_Counts(p, pt.Name);
            TAdd(pats, $"{pt.Name} [{pt.Type}]{(pt.SelfWeight != 0 ? $" TLBT×{pt.SelfWeight:0.##}" : "")} – {f + a + n} tải", new ExplorerAction("pattern", pt.Name), "Bấm: hiển thị tải gán của trường hợp này");
        }
        var cases = TAdd(loads, "Trường hợp phân tích", new ExplorerAction("cases"), "Bấm đúp: Khai báo → Trường hợp phân tích…");
        TAdd(cases, $"{p.Patterns.Count} tĩnh tuyến tính", new ExplorerAction("cases"));
        if (p.Modal?.Enabled ?? false) TAdd(cases, $"MODAL – {p.Modal.RequestedModes(p.Stories.Count)} dạng", new ExplorerAction("modal"));
        var combos = TAdd(loads, $"Tổ hợp ({p.Combinations.Count})", new ExplorerAction("combos"), "Bấm đúp: Khai báo → Tổ hợp tải trọng…");
        foreach (var c in p.Combinations.Take(80)) TAdd(combos, $"{c.Name}: {KetCauVXT.Loads.ComboGenerator.Describe(c)}", new ExplorerAction("combo", c.Name));
        if (p.Combinations.Count > 80) TAdd(combos, $"… {p.Combinations.Count - 80} tổ hợp khác");

        var objs = TNode("Đối tượng", null, null, true);
        var cols = TAdd(objs, $"Cột ({p.Frames.Count(f => f.IsVerticalKind)})");
        var beams = TAdd(objs, $"Dầm ({p.Frames.Count(f => !f.IsVerticalKind)})");
        foreach (var g in p.Frames.Where(f => f.IsVerticalKind).GroupBy(f => f.Story).OrderBy(g => StoryElev(g.Key)))
        {
            var sn = TAdd(cols, $"{g.Key} ({g.Count()})", new ExplorerAction("storyframes", "C|" + g.Key));
            foreach (var f in g.Take(200)) TAdd(sn, $"{f.Label} – {f.Section}", f);
        }
        foreach (var g in p.Frames.Where(f => !f.IsVerticalKind).GroupBy(f => f.Story).OrderBy(g => StoryElev(g.Key)))
        {
            var sn = TAdd(beams, $"{g.Key} ({g.Count()})", new ExplorerAction("storyframes", "B|" + g.Key));
            foreach (var f in g.Take(300)) TAdd(sn, $"{f.Label} – {f.Section}{(ModelAssign.ReleaseName(f).Length > 0 ? " · khớp" : "")}", f);
        }
        var slabs = TAdd(objs, $"Sàn ({p.Slabs.Count})");
        foreach (var g in p.Slabs.GroupBy(s => s.Story).OrderBy(g => StoryElev(g.Key)))
        {
            var sn = TAdd(slabs, $"{g.Key} ({g.Count()})", new ExplorerAction("story", g.Key));
            foreach (var s in g.Take(300)) TAdd(sn, $"{s.Label} – {s.Section}", s);
        }
        var walls = TAdd(objs, $"Tường ({p.Walls.Count})");
        foreach (var g in p.Walls.GroupBy(w => w.Story).OrderBy(g => StoryElev(g.Key)))
        {
            var sn = TAdd(walls, $"{g.Key} ({g.Count()})", new ExplorerAction("story", g.Key));
            foreach (var w in g.Take(300)) TAdd(sn, $"{w.Type} – {Math.Sqrt((w.X2 - w.X1) * (w.X2 - w.X1) + (w.Y2 - w.Y1) * (w.Y2 - w.Y1)):0.00} m", w);
        }
        var sup = p.Nodes.Where(n => n.IsSupport).ToList();
        var fnd = TAdd(objs, $"Móng / gối ({sup.Count} gối, {p.Footings.Count} móng)", new ExplorerAction("foundation"));
        foreach (var n in sup.Take(300))
        {
            var ft = p.Footings.FirstOrDefault(x => x.NodeId == n.Id);
            TAdd(fnd, $"Nút {n.Id} ({n.X:0.##}; {n.Y:0.##}) – {ModelAssign.RestraintName(n.Restraint)}{(ft != null ? " · " + ft.Label : "")}", new ExplorerAction("node", n.Id.ToString()));
        }

        var res = TNode("Kết quả", null, null, true);
        if (_res == null) TAdd(res, "Chưa phân tích (F5)", new ExplorerAction("run"));
        else
        {
            TAdd(res, $"Nguồn: {_res.Source} – {_res.Equations} phương trình, {_res.Cases.Count} trường hợp", new ExplorerAction("tables"));
            if (_res.Modal != null && _res.Modal.Modes.Count > 0)
            {
                var md = TAdd(res, $"Dao động riêng ({_res.Modal.Modes.Count} dạng)", new ExplorerAction("view", "Dao động"));
                foreach (var m in _res.Modal.Modes.Take(6))
                    TAdd(md, $"Dạng {m.Index}: T = {m.Period:0.000} s, UX {m.Ratio[0] * 100:0.#} %, UY {m.Ratio[1] * 100:0.#} %, RZ {m.Ratio[3] * 100:0.#} % ({m.Dominant})", new ExplorerAction("view", "Dao động"));
            }
            var br = TAdd(res, "Phản lực đáy", new ExplorerAction("tables"));
            foreach (var c in _res.Cases.Keys.Take(12))
            {
                var v = KetCauVXT.Analysis.ResultTables.BaseSum(p, _res.Cases[c], p.BaseElevation);
                TAdd(br, $"{c}: FX = {v[0]:0.#}, FY = {v[1]:0.#}, FZ = {v[2]:0.#} kN", new ExplorerAction("tables"));
            }
            TAdd(res, "Biểu đồ nội lực", new ExplorerAction("view", "Biểu đồ nội lực"));
            TAdd(res, "Hình biến dạng…", new ExplorerAction("deformed"));
            TAdd(res, "Bảng kết quả…", new ExplorerAction("tables"));
        }

        foreach (var top in new[] { info, grids, stories, mats, secs, loads, objs, res })
        {
            top.IsExpanded = _explorerExpanded.Contains(BaseHeader(top.Header as string));
            _explorer.Items.Add(top);
        }
    }

    private static (int, int, int) LoadPatternEditor_Counts(Project p, string name) => KetCauVXT.Loads.LoadPatternEditor.LoadCounts(p, name);

    private static string BaseHeader(string h)
    {
        if (h == null) return "";
        int i = h.IndexOf(" (", StringComparison.Ordinal);
        return i > 0 ? h.Substring(0, i) : h;
    }

    private double StoryElev(string story) => _p.Stories.FirstOrDefault(s => s.Name == story)?.Elevation ?? double.MinValue;

    /// <summary>Bấm một nút cây: chọn và phóng tới đối tượng.</summary>
    private void ExplorerSelect(object tag)
    {
        if (_p == null || tag == null) return;
        switch (tag)
        {
            case FrameElement f:
            {
                var fr = _p.Frames.FirstOrDefault(x => x.Id == f.Id);
                if (fr == null) return;
                if (_p.Stories.Any(s => s.Name == fr.Story)) cbStory.SelectedItem = fr.Story;
                ShowPlanTab();
                CheckMultiSelOwner(); _multiSel.Clear();
                Select(frame: fr);
                var a = _p.Nodes.FirstOrDefault(n => n.Id == fr.NodeI); var b = _p.Nodes.FirstOrDefault(n => n.Id == fr.NodeJ);
                if (a != null && b != null) ZoomPlanTo((a.X + b.X) / 2, (a.Y + b.Y) / 2, Math.Max(3, Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)) * 2));
                break;
            }
            case Slab s:
            {
                var sl = _p.Slabs.FirstOrDefault(x => x.Id == s.Id);
                if (sl == null) return;
                if (_p.Stories.Any(q => q.Name == sl.Story)) cbStory.SelectedItem = sl.Story;
                ShowPlanTab();
                Select(slab: sl);
                var ns = sl.Nodes.Select(id => _p.Nodes.FirstOrDefault(n => n.Id == id)).Where(n => n != null).ToList();
                if (ns.Count > 0) ZoomPlanTo(ns.Average(n => n.X), ns.Average(n => n.Y), Math.Max(4, (ns.Max(n => n.X) - ns.Min(n => n.X)) * 2));
                break;
            }
            case WallLine w:
                if (_p.Stories.Any(q => q.Name == w.Story)) cbStory.SelectedItem = w.Story;
                ShowPlanTab();
                Select(wall: _p.Walls.FirstOrDefault(x => x.Id == w.Id) ?? w);
                ZoomPlanTo((w.X1 + w.X2) / 2, (w.Y1 + w.Y2) / 2, 4);
                break;
            case ExplorerAction act:
                switch (act.Kind)
                {
                    case "story":
                        if (_p.Stories.Any(q => q.Name == act.Arg)) { cbStory.SelectedItem = act.Arg; ShowPlanTab(); }
                        break;
                    case "storyframes":
                    {
                        var parts = act.Arg.Split('|');
                        if (_p.Stories.Any(q => q.Name == parts[1])) cbStory.SelectedItem = parts[1];
                        CheckMultiSelOwner(); _multiSel.Clear();
                        foreach (var f in _p.Frames.Where(f => f.Story == parts[1] && f.IsVerticalKind == (parts[0] == "C"))) _multiSel.Add(f.Id);
                        ShowPlanTab(); RedrawPlan();
                        SetStatus($"Đã chọn {_multiSel.Count} {(parts[0] == "C" ? "cột" : "dầm")} tầng {parts[1]}.");
                        break;
                    }
                    case "section":
                    {
                        CheckMultiSelOwner(); _multiSel.Clear();
                        foreach (var f in PlanFrames().Where(f => f.Section == act.Arg)) _multiSel.Add(f.Id);
                        ShowPlanTab(); RedrawPlan();
                        SetStatus($"Đã chọn {_multiSel.Count} thanh tiết diện {act.Arg} ở {CurrentStory}.");
                        break;
                    }
                    case "pattern":
                        _loadPatternFilter = act.Arg;
                        if (!_showAssignedLoads) { _showAssignedLoads = true; if (mnuShowLoads != null) mnuShowLoads.IsChecked = true; }
                        ShowPlanTab(); RedrawPlan();
                        SetStatus($"Hiển thị tải gán của trường hợp {act.Arg} (Hiển thị → Hiển thị tải gán để tắt).");
                        break;
                    case "node":
                        if (int.TryParse(act.Arg, out int nid) && _p.Nodes.FirstOrDefault(n => n.Id == nid) is Node nd)
                        {
                            var bottom = _p.Stories.OrderBy(q => q.Elevation).FirstOrDefault();
                            if (bottom != null) cbStory.SelectedItem = bottom.Name;
                            ShowPlanTab();
                            ZoomPlanTo(nd.X, nd.Y, 4);
                            SetStatus($"Nút {nd.Id} ({nd.X:0.###}; {nd.Y:0.###}; {nd.Z:0.###}) – liên kết {ModelAssign.RestraintName(nd.Restraint)}.");
                        }
                        break;
                }
                break;
        }
    }

    /// <summary>Bấm đúp nút cây: mở hộp thoại khai báo / khung nhìn tương ứng.</summary>
    private void ExplorerOpen(object tag)
    {
        if (tag is not ExplorerAction act) return;
        var e = new RoutedEventArgs();
        switch (act.Kind)
        {
            case "project": tabLeft.SelectedIndex = 0; break;
            case "spectrum": MnuSpectrum_Click(this, e); break;
            case "materials": MnuMaterials_Click(this, e); break;
            case "sections": OpenSectionManager(); break;
            case "patterns": MnuLoadPatterns_Click(this, e); break;
            case "cases": MnuLoadCases_Click(this, e); break;
            case "modal": MnuModal_Click(this, e); break;
            case "combos": OpenComboEditor(); break;
            case "combo": OpenComboEditor()?.SelectCombo(act.Arg); break;
            case "foundation": { var t = tabView.Items.OfType<TabItem>().FirstOrDefault(x => (x.Header as string) == "Mô hình móng"); if (t != null) tabView.SelectedItem = t; break; }
            case "run": MnuRun_Click(this, e); break;
            case "tables": MnuTables_Click(this, e); break;
            case "deformed": MnuDeformed_Click(this, e); break;
            case "view": { var t = tabView.Items.OfType<TabItem>().FirstOrDefault(x => (x.Header as string) == act.Arg); if (t != null) tabView.SelectedItem = t; break; }
        }
    }
}

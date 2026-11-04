using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using KetCauVXT.Import;
using KetCauVXT.Import.Link;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// BẢN VẼ LIÊN KẾT AUTOCAD (DWG/DXF) – nền mặt bằng kiểu Xref / ETABS "Import DXF as background" + liên kết sống:
//   • Menu Mô hình → "Liên kết bản vẽ AutoCAD (DWG/DXF)…" mở cửa sổ "Bản vẽ liên kết" (CadLinkWindow.cs, không chặn);
//   • vẽ nét bản vẽ mờ dưới mô hình theo tầng (RedrawPlan → DrawCadLinks), hình đã đọc giữ ở CadLinkCache (không đọc lại khi vẽ);
//   • bắt điểm vào nét bản vẽ (đầu nét, trung điểm, giao nét, tâm) cho ＋ Dầm, công cụ ETABS, tường (SnapPlan / EtabsSnap);
//   • FileSystemWatcher: AutoCAD lưu bản vẽ → đọc lại nền (chờ 0,9 s, chạy nền, thử lại khi file đang khoá) → thông báo
//     "Bản vẽ link đã thay đổi – đã cập nhật" + nút "Cập nhật mô hình từ link…" (so sánh, đánh dấu, áp dụng có hoàn tác);
//   • lưu đường dẫn tương đối theo file dự án; mở dự án mà không thấy bản vẽ → "Không tìm thấy bản vẽ link – chọn lại";
//   • add-in AutoCAD gửi JSON có "Dwg" → đề nghị tạo / làm mới liên kết cho bản vẽ đó.
// Lõi: Model/CadLink.cs, Import/Link/CadLinkGeometry.cs (đọc, bộ đệm, bắt điểm), Import/Link/CadLinkSync.cs (so sánh, áp dụng).
// =====================================================================
public partial class MainWindow
{
    private bool _clSnap = true;
    private SnapCandidate _clLastSnap;
    private readonly Dictionary<string, FileSystemWatcher> _clWatchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _clResolved = new();
    private readonly Dictionary<string, int> _clTokens = new();
    private readonly HashSet<string> _clMissing = new();
    private readonly Dictionary<string, (CadLinkView View, string Sig, List<(Brush Br, Geometry Geo)> Parts)> _clDraw = new();
    private readonly Dictionary<int, Brush> _clBrushes = new();
    private CadLinkWindow _clWin;
    private Border _clNotice;
    private TextBlock _clNoticeText;
    private string _clNoticeLink;
    private List<CadDiffItem> _clPreview;
    private int _clAlignStep;
    private string _clAlignLink;
    private (double X, double Y) _clAlignFrom;
    private bool _clPromptingMissing;

    private const int ClMaxTexts = 800;
    private static readonly Brush ClGrey = Frozen(Color.FromRgb(105, 105, 110));
    private static readonly Brush ClAddBrush = Frozen(Color.FromRgb(0, 160, 60));
    private static readonly Brush ClMoveBrush = Frozen(Color.FromRgb(230, 130, 0));
    private static readonly Brush ClRemoveBrush = Frozen(Color.FromRgb(220, 30, 30));
    private static readonly Brush ClGlyph = Frozen(Color.FromRgb(170, 60, 190));

    internal Project CadLinkProject => _p;
    internal string CadLinkProjectPath => _path;
    internal bool CadLinkSnapEnabled { get => _clSnap; set { _clSnap = value; if (IsLoaded) DrawHover(); } }
    internal bool CadLinkIsMissing(CadLink l) => l != null && _clMissing.Contains(l.Id);
    private bool CadAlignActive => _clAlignStep > 0;

    private CadLink CadLinkById(string id) => _p?.CadLinks?.FirstOrDefault(q => q.Id == id);

    // ==================================================================
    // Menu, cửa sổ quản lý
    // ==================================================================
    private void MnuCadLinks_Click(object sender, RoutedEventArgs e) => OpenCadLinkManager();

    /// <summary>Mở (hoặc đưa lên trước) cửa sổ "Bản vẽ liên kết" – không chặn cửa sổ chính.</summary>
    internal CadLinkWindow OpenCadLinkManager()
    {
        if (_p == null) return null;
        if (_clWin != null && _clWin.IsLoaded) { _clWin.Reload(); _clWin.Activate(); return _clWin; }
        _clWin = new CadLinkWindow(this) { Owner = this };
        _clWin.Closed += (s, e) => _clWin = null;
        _clWin.Show();
        return _clWin;
    }

    internal string CadLinkSnapshot() => Snapshot();
    internal void CadLinkPushUndo(string snap) { if (snap != null) PushUndo(snap); }

    /// <summary>Hộp chọn file DWG/DXF để liên kết (tầng mặc định = tầng đang xem).</summary>
    internal async Task<CadLink> CadLinkAddInteractive()
    {
        if (_p == null) return null;
        var dlg = new OpenFileDialog { Filter = CadFileReaders.OpenFilter, Title = "Chọn bản vẽ AutoCAD để liên kết làm nền mặt bằng" };
        try { if (_path != null) dlg.InitialDirectory = Path.GetDirectoryName(_path); } catch (Exception) { }
        if (dlg.ShowDialog(this) != true) return null;
        string st = CurrentStory == AllStories || _p.Stories.All(q => q.Name != CurrentStory) ? CadLink.AllStories : CurrentStory;
        return await CadLinkAdd(dlg.FileName, st);
    }

    /// <summary>Thêm liên kết bản vẽ (có hoàn tác), đọc hình ngay.</summary>
    internal async Task<CadLink> CadLinkAdd(string path, string story)
    {
        if (_p == null || string.IsNullOrWhiteSpace(path)) return null;
        string full = Path.GetFullPath(path);
        var exist = _p.CadLinks.FirstOrDefault(q => string.Equals(q.Resolve(_path), full, StringComparison.OrdinalIgnoreCase));
        if (exist != null)
        {
            await ReloadCadLinkAsync(exist, true, false);
            SetStatus($"Bản vẽ {Path.GetFileName(full)} đã được liên kết – đã đọc lại.");
            return exist;
        }
        string snap = Snapshot();
        var l = new CadLink { FilePath = full, Name = Path.GetFileName(full), Story = string.IsNullOrWhiteSpace(story) ? CadLink.AllStories : story };
        l.UpdateRelative(_path);
        _p.CadLinks.Add(l);
        PushUndo(snap);
        MarkDirty(false);
        var r = await ReloadCadLinkAsync(l, true, false);
        UpdateCadWatchers();
        _clWin?.Reload();
        var g = CadLinkCache.Get(l);
        SetStatus(r.Ok && g != null
            ? $"Đã liên kết bản vẽ {l.DisplayName} ({g.SegmentCount:N0} nét, {g.Layers.Count} layer, ×{g.UnitScale:0.####} m/đơn vị) – tầng {l.Story}. Dùng \"Căn theo 2 điểm\" nếu bản vẽ lệch gốc toạ độ."
            : $"Đã thêm liên kết {l.DisplayName} nhưng chưa đọc được: {r.Error}");
        return l;
    }

    internal void CadLinkRemove(CadLink l)
    {
        if (_p == null || l == null) return;
        var cur = CadLinkById(l.Id);
        if (cur == null) return;
        string snap = Snapshot();
        _p.CadLinks.Remove(cur);
        PushUndo(snap);
        MarkDirty(false);
        _clDraw.Remove(cur.Id); _clMissing.Remove(cur.Id);
        if (_clNoticeLink == cur.Id) HideCadNotice();
        UpdateCadWatchers();
        RedrawPlan();
        _clWin?.Reload();
        SetStatus($"Đã bỏ liên kết bản vẽ {cur.DisplayName} (Ctrl+Z để hoàn tác).");
    }

    /// <summary>Sau khi sửa thuộc tính link ở cửa sổ quản lý: đánh dấu thay đổi, vẽ lại nền.</summary>
    internal void CadLinkEdited(CadLink l, bool reread = false)
    {
        if (l == null) return;
        MarkDirty(false);
        _clDraw.Remove(l.Id);
        UpdateCadWatchers();
        if (reread) _ = ReloadCadLinkAsync(l, true, false);
        else RedrawPlan();
    }

    internal async Task CadLinkReload(CadLink l)
    {
        if (l == null) return;
        var r = await ReloadCadLinkAsync(l, true, false);
        SetStatus(r.Ok ? $"Đã đọc lại bản vẽ link {l.DisplayName}" + (r.Changed ? " – nội dung đã thay đổi." : " – không thay đổi.") : r.Error);
    }

    /// <summary>Chọn lại file cho liên kết (bản vẽ bị dời / đổi tên).</summary>
    internal async Task<bool> CadLinkRelink(CadLink l)
    {
        if (l == null) return false;
        var dlg = new OpenFileDialog { Filter = CadFileReaders.OpenFilter, Title = "Không tìm thấy bản vẽ link – chọn lại: " + l.DisplayName, FileName = Path.GetFileName(l.FilePath ?? "") };
        try
        {
            string d = Path.GetDirectoryName(l.FilePath ?? "");
            if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) dlg.InitialDirectory = d;
            else if (_path != null) dlg.InitialDirectory = Path.GetDirectoryName(_path);
        }
        catch (Exception) { }
        if (dlg.ShowDialog(this) != true) return false;
        var cur = CadLinkById(l.Id) ?? l;
        cur.FilePath = Path.GetFullPath(dlg.FileName);
        cur.UpdateRelative(_path);
        if (string.IsNullOrWhiteSpace(cur.Name) || cur.Name == Path.GetFileName(l.FilePath ?? "")) cur.Name = Path.GetFileName(cur.FilePath);
        MarkDirty(false);
        var r = await ReloadCadLinkAsync(cur, true, false);
        UpdateCadWatchers();
        _clWin?.Reload();
        SetStatus(r.Ok ? $"Đã liên kết lại {cur.DisplayName}." : r.Error);
        return r.Ok;
    }

    internal void CadLinkOpenFolder(CadLink l)
    {
        string p = l?.Resolve(_path);
        try
        {
            if (p != null) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{p}\"") { UseShellExecute = true });
            else SetStatus("Không tìm thấy bản vẽ link – chọn lại.");
        }
        catch (Exception ex) { SetStatus("Không mở được thư mục: " + ex.Message); }
    }

    internal void CadLinkOpenInAutoCad(CadLink l)
    {
        string p = l?.Resolve(_path);
        try
        {
            if (p != null) Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
            else SetStatus("Không tìm thấy bản vẽ link – chọn lại.");
        }
        catch (Exception ex) { SetStatus("Không mở được bản vẽ bằng chương trình mặc định (AutoCAD): " + ex.Message); }
    }

    // ==================================================================
    // Đọc / đọc lại (chạy nền, thử lại khi AutoCAD đang ghi file)
    // ==================================================================
    private async Task<CadLinkCache.ReloadResult> ReloadCadLinkAsync(CadLink l, bool force, bool notify)
    {
        var res = new CadLinkCache.ReloadResult();
        if (l == null) return res;
        string id = l.Id;
        string path = l.Resolve(_path);
        if (path == null)
        {
            _clMissing.Add(id);
            res.Missing = true; res.Error = $"Không tìm thấy bản vẽ link – chọn lại: {l.DisplayName} ({l.FilePath})";
            _clWin?.Reload();
            return res;
        }
        _clMissing.Remove(id);
        _clResolved[id] = path;
        res.Path = path;
        var old = CadLinkCache.Get(l);
        if (!force && old != null && string.Equals(old.Path, path, StringComparison.OrdinalIgnoreCase) && Math.Abs(old.RequestedUnitScale - l.UnitScale) < 1e-12 && !CadLinkCache.LooksChanged(l, path))
        { res.Ok = true; res.Geometry = old; return res; }
        int tok = (_clTokens.TryGetValue(id, out int t0) ? t0 : 0) + 1;
        _clTokens[id] = tok;
        double unit = l.UnitScale;
        CadLinkGeometry g = null; string err = null;
        for (int i = 0; i < 16 && g == null; i++)
        {
            try { g = await Task.Run(() => CadLinkGeometry.Load(path, unit)); }
            catch (FileNotFoundException ex) { err = ex.Message; break; }
            catch (IOException ex) { err = "Bản vẽ đang được AutoCAD ghi – " + ex.Message; await Task.Delay(400 + 100 * i); }
            catch (UnauthorizedAccessException ex) { err = ex.Message; await Task.Delay(500); }
            catch (Exception ex) { err = ex.Message; break; }
        }
        if (_clTokens.TryGetValue(id, out int t1) && t1 != tok) { res.Error = "(đã có lần đọc mới hơn)"; return res; }
        var cur = CadLinkById(id) ?? l;   // dự án có thể đã được thay (hoàn tác) trong lúc đọc
        if (g == null)
        {
            res.Error = $"Không đọc được bản vẽ link {cur.DisplayName}: {err}";
            SetStatus(res.Error);
            return res;
        }
        res.Changed = CadLinkCache.Accept(cur, g);
        if (!ReferenceEquals(cur, l)) CadLinkCache.Accept(l, g);
        res.Ok = true; res.Geometry = g;
        _clDraw.Remove(id);
        RedrawPlan();
        _clWin?.Reload();
        if (res.Changed && notify)
        {
            string msg = $"Bản vẽ link đã thay đổi – đã cập nhật: {cur.DisplayName} ({DateTime.Now:HH:mm:ss}).";
            SetStatus(msg);
            ShowCadNotice(cur, msg);
        }
        return res;
    }

    /// <summary>Gọi khi đặt dự án (mở / mới): nạp hình các bản vẽ liên kết, bật theo dõi, báo bản vẽ không tìm thấy.</summary>
    private void OnCadLinksProjectSet()
    {
        _clDraw.Clear(); _clPreview = null; _clAlignStep = 0; _clMissing.Clear(); _clResolved.Clear();
        HideCadNotice();
        if (_p == null) return;
        _p.CadLinks ??= new List<CadLink>();
        var missing = new List<CadLink>();
        foreach (var l in _p.CadLinks.Where(q => q != null).ToList())
        {
            if (l.Resolve(_path) == null) { missing.Add(l); _clMissing.Add(l.Id); continue; }
            _ = ReloadCadLinkAsync(l, false, false);
        }
        UpdateCadWatchers();
        _clWin?.Reload();
        if (missing.Count == 0) return;
        string names = string.Join(", ", missing.Select(m => m.DisplayName));
        Dispatcher.BeginInvoke(new Action(() =>
        {
            SetStatus("Không tìm thấy bản vẽ link – chọn lại: " + names + " (Mô hình → Liên kết bản vẽ AutoCAD…).");
            if (SelfTestOut == null && !_clPromptingMissing) _ = PromptMissingCadLinks(missing.Select(m => m.Id).ToList());
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private async Task PromptMissingCadLinks(List<string> ids)
    {
        _clPromptingMissing = true;
        try
        {
            foreach (var id in ids)
            {
                var l = CadLinkById(id);
                if (l == null || !_clMissing.Contains(id)) continue;
                var r = MessageBox.Show(this, $"Không tìm thấy bản vẽ link – chọn lại?\n\n{l.DisplayName}\n{l.FilePath}" +
                                              (string.IsNullOrEmpty(l.RelativePath) ? "" : $"\n(đường dẫn tương đối: {l.RelativePath})"),
                    AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r == MessageBoxResult.Yes) await CadLinkRelink(l);
            }
        }
        finally { _clPromptingMissing = false; }
    }

    // ==================================================================
    // Theo dõi file (liên kết sống)
    // ==================================================================
    private void UpdateCadWatchers()
    {
        var need = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in _p?.CadLinks ?? new List<CadLink>())
        {
            if (l == null || !l.AutoReload) continue;
            string p = _clResolved.TryGetValue(l.Id, out var rp) ? rp : l.Resolve(_path);
            if (p == null) continue;
            _clResolved[l.Id] = p;
            try { string d = Path.GetDirectoryName(p); if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) need.Add(d); } catch (Exception) { }
        }
        foreach (var d in _clWatchers.Keys.ToList())
        {
            if (need.Contains(d)) continue;
            try { _clWatchers[d].EnableRaisingEvents = false; _clWatchers[d].Dispose(); } catch (Exception) { }
            _clWatchers.Remove(d);
        }
        foreach (var d in need)
        {
            if (_clWatchers.ContainsKey(d)) continue;
            try
            {
                var w = new FileSystemWatcher(d) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size, IncludeSubdirectories = false };
                w.Changed += (s, e) => OnCadFileEvent(e.FullPath);
                w.Created += (s, e) => OnCadFileEvent(e.FullPath);
                w.Renamed += (s, e) => OnCadFileEvent(e.FullPath);
                w.EnableRaisingEvents = true;
                _clWatchers[d] = w;
            }
            catch (Exception) { /* thư mục mạng không hỗ trợ theo dõi – vẫn dùng nút "Đọc lại" */ }
        }
    }

    private void OnCadFileEvent(string fullPath)
    {
        string ext = Path.GetExtension(fullPath ?? "");
        if (!string.Equals(ext, ".dwg", StringComparison.OrdinalIgnoreCase) && !string.Equals(ext, ".dxf", StringComparison.OrdinalIgnoreCase)) return;
        Dispatcher.BeginInvoke(new Action(async () => await CadFileChanged(fullPath)));
    }

    private readonly Dictionary<string, int> _clDebounce = new();

    private async Task CadFileChanged(string fullPath)
    {
        if (_p?.CadLinks == null) return;
        string f;
        try { f = Path.GetFullPath(fullPath); } catch (Exception) { return; }
        foreach (var l in _p.CadLinks.Where(q => q != null && q.AutoReload).ToList())
        {
            if (!_clResolved.TryGetValue(l.Id, out var p) || !string.Equals(p, f, StringComparison.OrdinalIgnoreCase)) continue;
            string id = l.Id;
            int t = (_clDebounce.TryGetValue(id, out int k) ? k : 0) + 1;
            _clDebounce[id] = t;
            await Task.Delay(900);   // AutoCAD ghi file nhiều lần khi lưu – chờ yên rồi mới đọc
            if (_clDebounce.TryGetValue(id, out int k2) && k2 != t) continue;
            var cur = CadLinkById(id);
            if (cur == null) continue;
            if (!CadLinkCache.LooksChanged(cur, f) && CadLinkCache.Get(cur) != null) continue;
            await ReloadCadLinkAsync(cur, true, true);
        }
    }

    // ==================================================================
    // Thông báo trên mặt bằng
    // ==================================================================
    private void ShowCadNotice(CadLink l, string msg)
    {
        if (l == null) return;
        _clNoticeLink = l.Id;
        if (_clNotice == null)
        {
            if (planCanvas.Parent is not Grid g) return;
            _clNoticeText = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 340, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            var upd = new Button { Content = "Cập nhật mô hình từ link…", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 4, 0), FontWeight = FontWeights.SemiBold };
            upd.Click += (s, e) => { var cur = CadLinkById(_clNoticeLink); if (cur != null) CadLinkUpdateModel(cur); };
            var close = new Button { Content = "✕", Padding = new Thickness(6, 1, 6, 1), ToolTip = "Ẩn thông báo" };
            close.Click += (s, e) => HideCadNotice();
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(_clNoticeText); sp.Children.Add(upd); sp.Children.Add(close);
            _clNotice = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 30, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(255, 248, 225)), BorderBrush = new SolidColorBrush(Color.FromRgb(230, 160, 40)),
                BorderThickness = new Thickness(1.2), CornerRadius = new CornerRadius(3), Padding = new Thickness(8, 5, 6, 5), Child = sp,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.3 }
            };
            g.Children.Add(_clNotice);
        }
        _clNoticeText.Text = msg;
        _clNotice.Visibility = Visibility.Visible;
    }

    private void HideCadNotice()
    {
        if (_clNotice != null) _clNotice.Visibility = Visibility.Collapsed;
        _clNoticeLink = null;
    }

    // ==================================================================
    // Vẽ nền (gọi từ RedrawPlan, trước sàn/dầm/cột)
    // ==================================================================
    private void DrawCadLinks()
    {
        if (_p?.CadLinks == null || _p.CadLinks.Count == 0) return;
        string st = CurrentStory;
        bool all = st == AllStories;
        var mt = new MatrixTransform(_scale, 0, 0, -_scale, _ox, _oy);
        mt.Freeze();
        double w = planHost.ActualWidth, h = planHost.ActualHeight;
        var (vx0, vy1) = S2W(new Point(0, 0));
        var (vx1, vy0) = S2W(new Point(w, h));
        foreach (var l in _p.CadLinks)
        {
            if (l == null || !l.Visible || !(all || l.AppliesTo(st))) continue;
            var g = CadLinkCache.Get(l);
            if (g == null) continue;
            CadLinkView v;
            try { v = g.View(l); } catch (Exception) { continue; }
            if (v.X1 < vx0 || v.X0 > vx1 || v.Y1 < vy0 || v.Y0 > vy1) continue;   // ngoài khung nhìn
            double op = Math.Clamp(l.Opacity, 0.05, 1.0);
            foreach (var (br, geo) in CadLinkParts(l, g, v))
            {
                var gg = new GeometryGroup { Transform = mt };
                gg.Children.Add(geo);
                planCanvas.Children.Add(new Shapes.Path { Data = gg, Stroke = br, StrokeThickness = 1, Opacity = op, IsHitTestVisible = false, SnapsToDevicePixels = true });
            }
            if (!l.ShowText || v.Texts.Count == 0) continue;
            var vis = CadLinkSnap.LayerFilter(l, g);
            int n = 0;
            foreach (var t in v.Texts)
            {
                if (vis != null && !vis(t.Layer)) continue;
                if (t.X < vx0 || t.X > vx1 || t.Y < vy0 || t.Y > vy1) continue;
                double fs = t.Height * _scale * 1.3;
                if (fs < 5) continue;
                var tb = new TextBlock { Text = t.Text, FontSize = Math.Min(fs, 60), Foreground = l.ByLayerColor ? ClBrush(g.LayerColors[t.Layer]) : ClGrey, Opacity = op, IsHitTestVisible = false };
                var sp = W2S(t.X, t.Y);
                Canvas.SetLeft(tb, sp.X); Canvas.SetTop(tb, sp.Y - fs);
                planCanvas.Children.Add(tb);
                if (++n >= ClMaxTexts) break;
            }
        }
        DrawCadDiffPreview();
    }

    /// <summary>Nét của bản vẽ gom theo màu (StreamGeometry đã đóng băng, toạ độ mô hình) – dựng lại khi đổi phép đặt / layer ẩn / chế độ màu.</summary>
    private List<(Brush Br, Geometry Geo)> CadLinkParts(CadLink l, CadLinkGeometry g, CadLinkView v)
    {
        string sig = (l.ByLayerColor ? "L|" : "G|") + string.Join("|", (l.HiddenLayers ?? new List<string>()).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        if (_clDraw.TryGetValue(l.Id, out var c) && ReferenceEquals(c.View, v) && c.Sig == sig) return c.Parts;
        var vis = CadLinkSnap.LayerFilter(l, g);
        var geos = new Dictionary<int, StreamGeometry>();
        var ctxs = new Dictionary<int, StreamGeometryContext>();
        try
        {
            for (int i = 0; i < v.SegCount; i++)
            {
                int lay = v.SegLayer[i];
                if (vis != null && !vis(lay)) continue;
                int key = l.ByLayerColor && lay >= 0 && lay < g.LayerColors.Count ? Math.Abs(g.LayerColors[lay]) : -1;
                if (!ctxs.TryGetValue(key, out var ctx))
                {
                    var sg = new StreamGeometry();
                    geos[key] = sg;
                    ctx = sg.Open();
                    ctxs[key] = ctx;
                }
                ctx.BeginFigure(new Point(v.Seg[4 * i], v.Seg[4 * i + 1]), false, false);
                ctx.LineTo(new Point(v.Seg[4 * i + 2], v.Seg[4 * i + 3]), true, false);
            }
        }
        finally
        {
            foreach (var ctx in ctxs.Values) ctx.Close();
        }
        var parts = new List<(Brush Br, Geometry Geo)>();
        foreach (var kv in geos)
        {
            kv.Value.Freeze();
            parts.Add((kv.Key < 0 ? ClGrey : ClBrush(kv.Key), kv.Value));
        }
        _clDraw[l.Id] = (v, sig, parts);
        return parts;
    }

    private Brush ClBrush(int aci)
    {
        if (_clBrushes.TryGetValue(aci, out var b)) return b;
        b = Frozen(AciColor(aci));
        _clBrushes[aci] = b;
        return b;
    }

    /// <summary>Màu ACI (AutoCAD Color Index) → RGB gần đúng; màu 7 (trắng/đen) và màu quá sáng đổi sang tối để thấy trên nền trắng.</summary>
    internal static Color AciColor(int aci)
    {
        aci = Math.Abs(aci);
        Color c = aci switch
        {
            1 => Color.FromRgb(255, 0, 0), 2 => Color.FromRgb(200, 180, 0), 3 => Color.FromRgb(0, 190, 0), 4 => Color.FromRgb(0, 170, 190),
            5 => Color.FromRgb(0, 0, 255), 6 => Color.FromRgb(200, 0, 200), 7 or 0 or 256 => Color.FromRgb(40, 40, 40), 8 => Color.FromRgb(128, 128, 128),
            9 => Color.FromRgb(150, 150, 150),
            >= 250 and <= 255 => Color.FromRgb((byte)(51 + (aci - 250) * 30), (byte)(51 + (aci - 250) * 30), (byte)(51 + (aci - 250) * 30)),
            >= 10 and <= 249 => FromHue(aci),
            _ => Color.FromRgb(40, 40, 40)
        };
        // làm tối màu quá sáng
        double lum = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
        if (lum > 200) { double k = 200 / lum; c = Color.FromRgb((byte)(c.R * k), (byte)(c.G * k), (byte)(c.B * k)); }
        return c;
    }

    private static Color FromHue(int aci)
    {
        double hue = (aci / 10 - 1) * 15.0;
        int v = aci % 10;
        double val = (v / 2) switch { 0 => 1.0, 1 => 0.8, 2 => 0.6, 3 => 0.5, _ => 0.3 };
        double sat = v % 2 == 0 ? 1.0 : 0.5;
        double cc = val * sat, x = cc * (1 - Math.Abs(hue / 60 % 2 - 1)), m = val - cc;
        (double r, double g, double b) = hue switch
        {
            < 60 => (cc, x, 0.0), < 120 => (x, cc, 0.0), < 180 => (0.0, cc, x), < 240 => (0.0, x, cc), < 300 => (x, 0.0, cc), _ => (cc, 0.0, x)
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    /// <summary>Xem trước khác biệt bản vẽ ↔ mô hình (khi hộp thoại "Cập nhật mô hình từ link" đang mở).</summary>
    private void DrawCadDiffPreview()
    {
        if (_clPreview == null) return;
        foreach (var it in _clPreview)
        {
            Brush br = it.Action == CadDiffAction.Add ? ClAddBrush : it.Action == CadDiffAction.Move ? ClMoveBrush : ClRemoveBrush;
            double th = it.Apply ? 3 : 1.5;
            switch (it.Element)
            {
                case CadDiffElement.Column:
                {
                    var c = W2S(it.X1, it.Y1);
                    double s = Math.Max(8, Math.Max(it.Bx, it.By) * _scale);
                    var r = new Shapes.Rectangle { Width = s, Height = s, Stroke = br, StrokeThickness = th, IsHitTestVisible = false, StrokeDashArray = it.Apply ? null : new DoubleCollection { 3, 2 } };
                    Canvas.SetLeft(r, c.X - s / 2); Canvas.SetTop(r, c.Y - s / 2);
                    planCanvas.Children.Add(r);
                    break;
                }
                case CadDiffElement.Grid:
                {
                    double w = planHost.ActualWidth, h = planHost.ActualHeight;
                    var a = W2S(it.Dir == GridDir.X ? it.Coord : 0, it.Dir == GridDir.X ? 0 : it.Coord);
                    if (it.Dir == GridDir.X) CLine(planCanvas, new Point(a.X, 0), new Point(a.X, h), br, th, true);
                    else CLine(planCanvas, new Point(0, a.Y), new Point(w, a.Y), br, th, true);
                    break;
                }
                default:
                    CLine(planCanvas, W2S(it.X1, it.Y1), W2S(it.X2, it.Y2), br, Math.Max(th, (it.Thickness > 0 ? it.Thickness : 0.1) * _scale * 0.8), !it.Apply);
                    break;
            }
        }
    }

    /// <summary>Phạm vi các bản vẽ liên kết đang hiện (thêm vào khi vừa màn hình mà mô hình còn trống).</summary>
    private void CadLinkExtent(List<double> xs, List<double> ys)
    {
        if (_p?.CadLinks == null) return;
        string st = CurrentStory;
        foreach (var l in _p.CadLinks)
        {
            if (l == null || !l.Visible || !(st == AllStories || l.AppliesTo(st))) continue;
            var g = CadLinkCache.Get(l);
            if (g == null || g.SegmentCount == 0) continue;
            var v = g.View(l);
            if (v.X1 < v.X0) continue;
            xs.Add(v.X0); xs.Add(v.X1); ys.Add(v.Y0); ys.Add(v.Y1);
        }
    }

    /// <summary>Phóng vừa màn hình cả mô hình và bản vẽ liên kết (l = null: mọi bản vẽ đang hiện; l: chỉ bản vẽ đó).</summary>
    internal void CadLinkZoom(CadLink l = null)
    {
        if (_p == null) return;
        double w = planHost.ActualWidth, h = planHost.ActualHeight;
        if (w < 160 || h < 160) { FitPlan(); return; }
        var xs = new List<double>(); var ys = new List<double>();
        if (l == null)
        {
            foreach (var n in _p.Nodes) { xs.Add(n.X); ys.Add(n.Y); }
            foreach (var gl in _p.Grids) { if (gl.Dir == GridDir.X) xs.Add(gl.Coord); else ys.Add(gl.Coord); }
            CadLinkExtent(xs, ys);
        }
        else
        {
            var g = CadLinkCache.Get(l);
            if (g != null) { var v = g.View(l); if (v.X1 >= v.X0) { xs.Add(v.X0); xs.Add(v.X1); ys.Add(v.Y0); ys.Add(v.Y1); } }
        }
        if (xs.Count == 0 || ys.Count == 0) { FitPlan(); return; }
        double x0 = xs.Min(), x1 = xs.Max(), y0 = ys.Min(), y1 = ys.Max();
        double dx = Math.Max(x1 - x0, 1), dy = Math.Max(y1 - y0, 1);
        const double margin = 70;
        _scale = Math.Max(0.05, Math.Min((w - 2 * margin) / dx, (h - 2 * margin) / dy));
        _ox = (w - dx * _scale) / 2 - x0 * _scale;
        _oy = h - (h - dy * _scale) / 2 + y0 * _scale;
        _fitPending = false;
        RedrawPlan();
    }

    // ==================================================================
    // Bắt điểm vào nét bản vẽ
    // ==================================================================
    /// <summary>Thêm/bỏ cờ bắt điểm bản vẽ link theo công tắc "Bắt điểm bản vẽ link" (dùng cho EtabsSnap).</summary>
    private SnapMask CadSnapMask(SnapMask m) => _clSnap ? m | SnapMask.CadLink : m & ~SnapMask.CadLink;

    /// <summary>Bắt điểm thường (tường, dầm cong, khi tắt ETABS): nút / giao trục mô hình ưu tiên, không có → nét bản vẽ link.</summary>
    private bool TryCadLinkSnap(string story, double x, double y, double tol, out (double X, double Y) pt)
    {
        pt = default;
        _clLastSnap = null;
        if (!_clSnap || _p?.CadLinks == null || _p.CadLinks.Count == 0) return false;
        try
        {
            if (BeamTools.SnapCandidates(_p, story, x, y, tol, SnapMask.End | SnapMask.Node | SnapMask.GridIntersection).Count > 0) return false;
            var c = CadLinkSnap.Best(_p, story, x, y, tol, SnapMask.All);
            if (c == null) return false;
            _clLastSnap = c;
            pt = (c.X, c.Y);
            return true;
        }
        catch (ArgumentException) { return false; }   // tầng không tồn tại
    }

    /// <summary>Ký hiệu điểm bắt trên nét bản vẽ link (gọi từ DrawSnapMarker / DrawEtabsSnapGlyph). false = không phải điểm bắt link.</summary>
    private bool DrawCadSnapGlyph((double X, double Y) pt, SnapKind? kind = null)
    {
        SnapKind k;
        if (kind != null) k = kind.Value;
        else
        {
            if (_clLastSnap == null || Math.Abs(_clLastSnap.X - pt.X) > 1e-6 || Math.Abs(_clLastSnap.Y - pt.Y) > 1e-6) return false;
            k = _clLastSnap.Kind;
        }
        if (k is not (SnapKind.CadEnd or SnapKind.CadMid or SnapKind.CadIntersection or SnapKind.CadCenter)) return false;
        var c = W2S(pt.X, pt.Y);
        var br = ClGlyph;
        const double r = 6;
        void Ln(double x1, double y1, double x2, double y2) => CLine(planOverlay, new Point(c.X + x1, c.Y + y1), new Point(c.X + x2, c.Y + y2), br, 2, false);
        switch (k)
        {
            case SnapKind.CadEnd:
                planOverlay.Children.Add(new Shapes.Polygon { Points = new PointCollection { new Point(c.X - r, c.Y - r), new Point(c.X + r, c.Y - r), new Point(c.X + r, c.Y + r), new Point(c.X - r, c.Y + r) }, Stroke = br, StrokeThickness = 2, IsHitTestVisible = false });
                break;
            case SnapKind.CadMid:
                planOverlay.Children.Add(new Shapes.Polygon { Points = new PointCollection { new Point(c.X, c.Y - r - 1), new Point(c.X + r + 1, c.Y + r), new Point(c.X - r - 1, c.Y + r) }, Stroke = br, StrokeThickness = 2, IsHitTestVisible = false });
                break;
            case SnapKind.CadIntersection:
                Ln(-r, -r, r, r); Ln(-r, r, r, -r);
                break;
            default:
                planOverlay.Children.Add(MakeCircle(c, r, br));
                Ln(-2, 0, 2, 0); Ln(0, -2, 0, 2);
                break;
        }
        if (kind == null) AddText($"{BeamTools.SnapName(k)}  ({pt.X:0.000}; {pt.Y:0.000})", new Point(c.X + 10, c.Y + 7), Brushes.DimGray, 9, false, false, planOverlay);
        return true;
    }

    // ==================================================================
    // Căn theo 2 điểm: điểm trên bản vẽ → giao trục / nút của mô hình
    // ==================================================================
    internal void CadLinkStartAlign(CadLink l)
    {
        if (l == null) return;
        if (CadLinkCache.Get(l) == null) { SetStatus("Bản vẽ link chưa đọc được – không căn được."); return; }
        _clAlignStep = 1; _clAlignLink = l.Id;
        try { tabView.SelectedItem = ViewTab("Mặt bằng"); } catch (Exception) { }
        if (!l.IsAllStories && cbStory.Items.Contains(l.Story) && CurrentStory != l.Story && CurrentStory != AllStories) cbStory.SelectedItem = l.Story;
        if (planHost != null) planHost.Cursor = Cursors.Cross;
        Activate();
        SetStatus($"Căn bản vẽ {l.DisplayName} (1/2): bấm một điểm trên bản vẽ link (đầu nét, giao nét – ký hiệu tím); Esc để huỷ.");
        DrawHover();
    }

    private void CancelCadAlign()
    {
        if (_clAlignStep == 0) return;
        _clAlignStep = 0;
        if (planHost != null) planHost.Cursor = _mode == PlanEditMode.Select ? null : Cursors.Cross;
        SetStatus("Đã huỷ căn bản vẽ link.");
        DrawHover();
    }

    /// <summary>Điểm đích trên mô hình: giao trục / nút gần nhất trong dung sai (không phụ thuộc tầng đang xem).</summary>
    private (double X, double Y) CadAlignTarget(double x, double y, double tol)
    {
        (double X, double Y) best = (Math.Round(x, 3), Math.Round(y, 3)); double bd = tol;
        foreach (var gx in _p.GridsX)
            foreach (var gy in _p.GridsY)
            {
                double d = Math.Sqrt((gx.Coord - x) * (gx.Coord - x) + (gy.Coord - y) * (gy.Coord - y));
                if (d <= bd) { bd = d; best = (gx.Coord, gy.Coord); }
            }
        if (bd < tol) return best;
        foreach (var n in _p.Nodes)
        {
            double d = Math.Sqrt((n.X - x) * (n.X - x) + (n.Y - y) * (n.Y - y));
            if (d <= bd) { bd = d; best = (n.X, n.Y); }
        }
        return best;
    }

    private (double X, double Y) CadAlignSource(CadLink l, double x, double y, double tol)
    {
        if (CadLinkCache.Get(l) == null) return (x, y);
        var c = CadLinkSnap.Candidates(CadLinkSnap.Active(new[] { l }, null, true), x, y, tol, SnapMask.All)
            .OrderBy(q => q.Priority).ThenBy(q => q.Dist).FirstOrDefault();
        return c != null ? (c.X, c.Y) : (x, y);
    }

    /// <summary>Bấm trên mặt bằng khi đang căn bản vẽ; true = đã xử lý.</summary>
    private bool CadLinkAlignClick(Point s)
    {
        if (_clAlignStep == 0) return false;
        var l = CadLinkById(_clAlignLink);
        if (l == null) { _clAlignStep = 0; return false; }
        var (x, y) = S2W(s);
        double tol = SnapTol();
        if (_clAlignStep == 1)
        {
            _clAlignFrom = CadAlignSource(l, x, y, tol);
            _clAlignStep = 2;
            SetStatus($"Căn bản vẽ (2/2): điểm bản vẽ ({_clAlignFrom.X:0.000}; {_clAlignFrom.Y:0.000}) – bấm giao trục / nút đích trên mô hình; Esc để huỷ.");
            DrawHover();
            return true;
        }
        var target = CadAlignTarget(x, y, tol);
        string snap = Snapshot();
        l.AlignByPoints(_clAlignFrom, target);
        PushUndo(snap);
        MarkDirty(false);
        _clAlignStep = 0;
        if (planHost != null) planHost.Cursor = _mode == PlanEditMode.Select ? null : Cursors.Cross;
        _clDraw.Remove(l.Id);
        RedrawPlan();
        _clWin?.Reload();
        SetStatus($"Đã căn bản vẽ {l.DisplayName}: điểm bản vẽ → ({target.X:0.000}; {target.Y:0.000}); dời dx = {target.X - _clAlignFrom.X:0.000} m, dy = {target.Y - _clAlignFrom.Y:0.000} m (Ctrl+Z để hoàn tác).");
        return true;
    }

    /// <summary>Lớp phủ khi căn bản vẽ: điểm bắt dưới chuột + đường nối từ điểm bản vẽ đã chọn. true = đã vẽ.</summary>
    private bool DrawCadAlignOverlay()
    {
        if (_clAlignStep == 0 || _p == null) return false;
        var l = CadLinkById(_clAlignLink);
        if (l == null) return false;
        if (_lastMouse == null) return true;
        var (x, y) = S2W(_lastMouse.Value);
        double tol = SnapTol();
        if (_clAlignStep == 1)
        {
            var src = CadAlignSource(l, x, y, tol);
            var c = W2S(src.X, src.Y);
            planOverlay.Children.Add(MakeCircle(c, 7, ClGlyph));
            AddText($"Điểm bản vẽ ({src.X:0.000}; {src.Y:0.000})", new Point(c.X + 10, c.Y + 7), ClGlyph, 10, false, false, planOverlay);
            return true;
        }
        var a = W2S(_clAlignFrom.X, _clAlignFrom.Y);
        var t = CadAlignTarget(x, y, tol);
        var b = W2S(t.X, t.Y);
        planOverlay.Children.Add(MakeCircle(a, 6, ClGlyph));
        CLine(planOverlay, a, b, ClGlyph, 1.5, true);
        CLine(planOverlay, new Point(b.X - 7, b.Y - 7), new Point(b.X + 7, b.Y + 7), ClGlyph, 2, false);
        CLine(planOverlay, new Point(b.X - 7, b.Y + 7), new Point(b.X + 7, b.Y - 7), ClGlyph, 2, false);
        AddText($"→ ({t.X:0.000}; {t.Y:0.000})  dx = {t.X - _clAlignFrom.X:0.000}, dy = {t.Y - _clAlignFrom.Y:0.000} m", new Point(b.X + 10, b.Y + 7), ClGlyph, 10, false, false, planOverlay);
        return true;
    }

    /// <summary>Esc khi đang căn bản vẽ.</summary>
    private bool HandleCadLinkKey(Key key)
    {
        if (key != Key.Escape || _clAlignStep == 0) return false;
        CancelCadAlign();
        return true;
    }

    // ==================================================================
    // Cập nhật mô hình từ link (so sánh → đánh dấu → áp dụng có hoàn tác)
    // ==================================================================
    internal async void CadLinkUpdateModel(CadLink l)
    {
        if (_p == null || l == null) return;
        if (_busy) { SetStatus("Chương trình đang tính toán – chờ xong rồi cập nhật mô hình."); return; }
        l = CadLinkById(l.Id) ?? l;
        string story = l.IsAllStories ? CurrentStory : l.Story;
        if (story == AllStories || _p.Stories.All(q => q.Name != story))
        {
            MessageBox.Show(this, $"Bản vẽ \"{l.DisplayName}\" gắn cho {(l.IsAllStories ? "mọi tầng" : "tầng " + l.Story)} – chọn một tầng cụ thể ở ô 'Tầng đang xem' (hoặc gán tầng cho bản vẽ) rồi cập nhật lại.",
                AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var g = CadLinkCache.Get(l);
        if (g == null)
        {
            var rr = await ReloadCadLinkAsync(l, true, false);
            if (!rr.Ok) { MessageBox.Show(this, rr.Error, AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            g = rr.Geometry;
        }
        CadLinkDiff diff;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var p = _p;
            diff = await Task.Run(() => CadLinkSync.Compare(p, l, g, story));
        }
        catch (Exception ex) { ShowError("Không so sánh được bản vẽ link với mô hình", ex); return; }
        finally { Mouse.OverrideCursor = null; }
        if (diff.IsEmpty)
        {
            HideCadNotice();
            l.SyncedHash = g.Hash;
            MessageBox.Show(this, diff.Summary + (diff.Warnings.Count > 0 ? "\n\nLưu ý:\n- " + string.Join("\n- ", diff.Warnings.Take(8)) : ""),
                AppTitle, MessageBoxButton.OK, diff.Warnings.Count > 0 && diff.Plan == null ? MessageBoxImage.Warning : MessageBoxImage.Information);
            return;
        }
        _clPreview = diff.Items;
        cbStory.SelectedItem = story;
        RedrawPlan();
        var dlg = new CadLinkDiffDialog(diff, () => RedrawPlan()) { Owner = this };
        bool ok = dlg.ShowDialog() == true;
        _clPreview = null;
        if (!ok) { RedrawPlan(); return; }
        string snap = Snapshot();
        EditResult r;
        try { r = CadLinkSync.Apply(_p, diff); }
        catch (Exception ex) { RestoreSnapshot(snap); ShowError("Không áp dụng được thay đổi từ bản vẽ link", ex); return; }
        if (!r.Ok) { if (Snapshot() != snap) RestoreSnapshot(snap); RedrawPlan(); SetStatus(r.Message); return; }
        HideCadNotice();
        AfterEdit(snap, r, null);
        SetStatus(r.Message + " (Ctrl+Z để hoàn tác)");
    }

    // ==================================================================
    // Add-in AutoCAD: JSON có đường dẫn DWG → tạo / làm mới liên kết
    // ==================================================================
    private async Task OfferCadLinkFromAcad(KetCauVXT.Import.Acad.AcadExchange x)
    {
        if (_p == null || x == null || string.IsNullOrWhiteSpace(x.Dwg)) return;
        string dwg;
        try { dwg = Path.GetFullPath(x.Dwg); } catch (Exception) { return; }
        if (!File.Exists(dwg)) return;
        var exist = _p.CadLinks.FirstOrDefault(q => string.Equals(q.Resolve(_path), dwg, StringComparison.OrdinalIgnoreCase));
        if (exist != null)
        {
            var r = await ReloadCadLinkAsync(exist, true, false);
            SetStatus(r.Ok ? $"Đã làm mới bản vẽ link {exist.DisplayName} theo AutoCAD." : r.Error);
            return;
        }
        if (SelfTestOut != null) return;
        var ans = MessageBox.Show(this, $"AutoCAD gửi dữ liệu của bản vẽ:\n{dwg}\n\nBản vẽ này chưa liên kết vào dự án. Tạo liên kết làm nền mặt bằng (cập nhật tự động khi lưu trong AutoCAD)?",
            AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ans != MessageBoxResult.Yes) return;
        string st = CurrentStory == AllStories || _p.Stories.All(q => q.Name != CurrentStory) ? CadLink.AllStories : CurrentStory;
        await CadLinkAdd(dwg, st);
    }
}

using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using KetCauVXT.Import.Arch;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// CHI TIẾT KIẾN TRÚC KIỂU REVIT trên mặt bằng / 3D: Mái…, Trần theo phòng…, Lan can (vẽ đường), Phào chỉ… / Ốp – sơn tường…
// (bấm mặt tường), Chép kiến trúc sang tầng…, Khối lượng hoàn thiện KT…, Nhập mô hình SketchUp / Revit (OBJ, DAE) → mặt bằng → kết cấu.
// Lõi: Model/ArchDetail.cs, Import/Arch/MeshToPlan.cs. Có hoàn tác (Ctrl+Z).
// =====================================================================
public partial class MainWindow
{
    private enum AdTool { None, Railing, Sweep, Cladding }
    private AdTool _adTool;
    private readonly List<(double X, double Y)> _adPts = new();
    private RailKind _adRailKind = RailKind.Inox;
    private double _adRailH = 1.0;
    private int _adRailUse;
    private static readonly double[] RailHz = { 0.3, 0.8, 1.5, 0 };
    private SweepPreset _adSweep = ArchDetail.SweepPresets[0];
    private CladPreset _adClad = ArchDetail.CladPresets[0];

    /// <summary>Nhóm nút "Hoàn thiện KT" cho tab BIM.</summary>
    private FrameworkElement ArchDetailGroup() =>
        RvGroup("Hoàn thiện KT",
            RvBtn("Mái…", "", "Mái theo đường bao: mái bằng, 1 dốc, 2 dốc (tường thu hồi), 4 dốc / mái chóp – độ dốc, đua mái, tải mái lên sàn", RoofUi),
            RvBtn("Trần theo phòng…", "", "Tạo trần cho mọi phòng của tầng đang xem (cao độ, loại trần)", CeilingsUi),
            RvBtn("Lan can", "", "Vẽ lan can bằng các điểm (Enter kết thúc): inox, kính, sắt, xây – tự sinh tải lên dầm đỡ khi phân tích", RailingUi),
            RvBtn("Phào chỉ…", "", "Phào chân tường, gờ chỉ ngang tầng, gờ bậu cửa sổ, phào trần… chạy theo mặt tường (tự cắt qua ô cửa)", SweepUi),
            RvBtn("Ốp / sơn tường…", "", "Vùng ốp đá, gạch thẻ, gạch men, gỗ, sơn màu nhấn trên mặt tường (trừ ô cửa)", CladdingUi),
            RvBtn("Chép KT sang tầng…", "", "Sao chép tường (kèm cửa, phào, ốp), phòng, trần, lan can, đồ đạc của tầng đang xem sang các tầng khác", CopyArchUi),
            RvBtn("KL hoàn thiện KT…", "", "Bảng khối lượng mái, trần, lan can, phào chỉ, ốp / sơn theo tầng – xuất CSV", ArchQtyUi),
            RvBtn("Công cụ xuất SketchUp / 3ds Max / Revit…", "", "Plugin SketchUp, công cụ 3ds Max, add-in Revit: đưa đồ đạc / họ vào thư viện, xuất mô hình (m, Z lên) – kèm trong bộ cài", SketchUpPluginUi),
            RvBtn("Nhập mô hình SketchUp / Revit…", "", "Mô hình 3D (OBJ, DAE, *.vxtmesh.json) → tự tìm cao độ sàn, cắt mặt bằng từng tầng (tường, cột, cửa) → triển khai kết cấu", ImportMeshModelUi));

    // ==================================================================
    // Biểu mẫu đơn giản
    // ==================================================================
    private sealed class MiniForm : Window
    {
        private readonly StackPanel _sp = new() { Margin = new Thickness(12) };
        private readonly Dictionary<string, FrameworkElement> _ctl = new();
        public MiniForm(Window owner, string title, double width = 460)
        {
            Owner = owner; Title = title; Width = width; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false; Background = SystemColors.ControlBrush;
        }
        private void Line(string label, FrameworkElement c)
        {
            var d = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            d.Children.Add(new TextBlock { Text = label, Width = 190, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            d.Children.Add(c);
            _sp.Children.Add(d);
        }
        public void Text(string key, string label, string val) { var t = new TextBox { Text = val }; _ctl[key] = t; Line(label, t); }
        public void Num(string key, string label, double v) => Text(key, label, v.ToString("0.###", Inv));
        public void Combo(string key, string label, IEnumerable<string> items, int sel = 0, bool editable = false)
        {
            var c = new ComboBox { ItemsSource = items.ToList(), SelectedIndex = sel, IsEditable = editable };
            _ctl[key] = c; Line(label, c);
        }
        public void Check(string key, string label, bool v) { var c = new CheckBox { Content = label, IsChecked = v, Margin = new Thickness(0, 3, 0, 3) }; _ctl[key] = c; _sp.Children.Add(c); }
        public void Hint(string s) => _sp.Children.Add(DialogUi.Hint(s));
        public void Add(UIElement e) => _sp.Children.Add(e);
        public string S(string k) => _ctl[k] switch { TextBox t => t.Text, ComboBox c => c.IsEditable ? c.Text : c.SelectedItem as string ?? "", _ => "" };
        public int I(string k) => (_ctl[k] as ComboBox)?.SelectedIndex ?? -1;
        public bool B(string k) => (_ctl[k] as CheckBox)?.IsChecked == true;
        public double D(string k, double def) => DialogUi.ParseNum(S(k), def);
        public FrameworkElement C(string k) => _ctl[k];
        public bool Run(string okText = "Đồng ý")
        {
            var ok = DialogUi.Btn(okText, (s, e) => DialogResult = true, true); ok.IsDefault = true;
            var no = DialogUi.Btn("Huỷ", (s, e) => DialogResult = false); no.IsCancel = true;
            var r = DialogUi.Row(ok, no); r.HorizontalAlignment = HorizontalAlignment.Right; r.Margin = new Thickness(0, 10, 0, 0);
            _sp.Children.Add(r);
            Content = _sp;
            return ShowDialog() == true;
        }
    }

    private string AdStory(bool warn = true)
    {
        string st = CurrentStory;
        if (st == AllStories || _p.Stories.All(q => q.Name != st))
        {
            if (warn) MessageBox.Show(this, "Chọn một tầng cụ thể ở ô 'Tầng đang xem' trước.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }
        return st;
    }

    private void ArchDetailChanged(string msg, string snap)
    {
        if (snap != null) PushUndo(snap);
        MarkDirty(false);
        _res = null;
        RedrawPlan();
        try { Build3D(); } catch (Exception) { /* 3D chưa sẵn */ }
        SetStatus(msg);
    }

    // ==================================================================
    // Mái
    // ==================================================================
    private static readonly string[] RoofShapeNames = { "Mái bằng", "Mái 1 dốc", "Mái 2 dốc (tường thu hồi)", "Mái 4 dốc / mái chóp" };
    private static readonly string[] RoofTypes = { "Mái tôn xà gồ thép", "Mái ngói đỏ 22v/m²", "Mái BTCT dán ngói", "Mái bằng BTCT chống nóng", "Mái kính khung nhôm" };

    private void RoofUi()
    {
        if (_p == null) return;
        var stories = _p.Stories.OrderBy(s => s.Elevation).Select(s => s.Name).ToList();
        if (stories.Count == 0) return;
        string st = CurrentStory != AllStories && stories.Contains(CurrentStory) ? CurrentStory : stories[^1];
        var fp = ArchDetail.Footprint(_p, st) ?? (0, 0, 8, 5);
        var f = new MiniForm(this, "Mái theo đường bao");
        f.Combo("st", "Đặt trên cao độ (tầng)", stories, stories.IndexOf(st));
        f.Combo("shape", "Hình mái", RoofShapeNames, 2);
        f.Combo("type", "Loại mái", RoofTypes, 0, true);
        f.Num("slope", "Độ dốc (%)", 30);
        f.Num("ov", "Đua mái (m)", 0.5);
        f.Num("eave", "Chân mái trên cao độ tầng (m)", 0);
        f.Combo("src", "Đường bao", new[] { "Theo đường bao ô sàn của tầng (nhà chữ L, T, U…)", "Hình chữ nhật nhập toạ độ bên dưới" });
        f.Num("x0", "Đường bao X từ (m)", fp.Item1); f.Num("x1", "đến (m)", fp.Item3);
        f.Num("y0", "Đường bao Y từ (m)", fp.Item2); f.Num("y1", "đến (m)", fp.Item4);
        f.Check("load", "Dồn tải lớp mái (kể xà gồ / li tô) lên các ô sàn trong đường bao khi phân tích", true);
        f.Check("rep", "Xoá mái cũ của tầng này", false);
        f.Hint("Nhà chữ L, T, U: mái chia theo khối, mái cánh chạy vào mái chính (giao nóc). Tầng chưa có ô sàn → dùng hình chữ nhật. Mái tôn ~0,3, mái ngói ~0,9 kN/m² mặt bằng (sửa được trong tải sàn sau khi tạo).");
        if (!f.Run("Tạo mái")) return;
        try
        {
            string snap = Snapshot();
            string story = f.S("st"), type = string.IsNullOrWhiteSpace(f.S("type")) ? RoofTypes[0] : f.S("type").Trim();
            if (f.B("rep")) _p.Arch?.Roofs.RemoveAll(r => r.Story == story);
            var outline = f.I("src") == 0 ? ArchDetail.FootprintOutline(_p, story) : null;
            var r = outline != null && outline.Count >= 4
                ? ArchDetail.AddRoof(_p, story, outline, (RoofShape)Math.Max(0, f.I("shape")), f.D("slope", 30), f.D("ov", 0.5), type, f.D("eave", 0))
                : ArchDetail.AddRoof(_p, story, f.D("x0", 0), f.D("y0", 0), f.D("x1", 8), f.D("y1", 5), (RoofShape)Math.Max(0, f.I("shape")),
                    f.D("slope", 30), f.D("ov", 0.5), type, f.D("eave", 0));
            if (f.B("load")) r.SlabLoad = ArchDetailLoads.DefaultRoofLoad(type);
            ArchDetailChanged($"Đã tạo {type} ({RoofShapeNames[(int)r.Shape]}) dốc {r.SlopePercent:0.#}% – cao đỉnh {r.RidgeHeight:0.00} m, diện tích nghiêng {RoofGeometry.SlopedArea(r):0.0} m²"
                              + (r.SlabLoad > 0 ? $", tải {r.SlabLoad:0.##} kN/m² lên sàn {story} (chạy lại phân tích)." : "."), snap);
        }
        catch (Exception ex) { ShowError("Không tạo được mái", ex); }
    }

    // ==================================================================
    // Trần
    // ==================================================================
    private void CeilingsUi()
    {
        if (_p == null) return;
        string st = AdStory();
        if (st == null) return;
        if (_p.Arch == null || _p.Arch.Rooms.All(r => r.Story != st))
        {
            MessageBox.Show(this, $"Tầng {st} chưa có phòng. Bấm \"Tìm phòng\" (nhóm Kiến trúc) để tạo phòng từ mạng tường trước.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var f = new MiniForm(this, "Trần theo phòng – tầng " + st);
        f.Num("h", "Cao độ trần so với sàn (m)", 2.9);
        f.Combo("type", "Loại trần", new[] { "Trần thạch cao khung chìm", "Trần thạch cao khung nổi 600×600", "Trần nhựa", "Trần gỗ", "Trần BTCT sơn bả" }, 0, true);
        if (!f.Run("Tạo trần")) return;
        string snap = Snapshot();
        int n = ArchDetail.CeilingsFromRooms(_p, st, Math.Max(2.0, f.D("h", 2.9)), f.S("type"));
        if (n == 0) { SetStatus("Mọi phòng của tầng đã có trần."); return; }
        ArchDetailChanged($"Đã tạo {n} trần {f.S("type")} cao {f.D("h", 2.9):0.00} m cho tầng {st}.", snap);
    }

    // ==================================================================
    // Lan can, phào chỉ, ốp – công cụ bấm trên mặt bằng
    // ==================================================================
    private void RailingUi()
    {
        if (_p == null || AdStory() == null) return;
        var f = new MiniForm(this, "Lan can");
        f.Combo("k", "Loại lan can", Enum.GetValues<RailKind>().Select(ArchDetail.RailName), (int)_adRailKind);
        f.Num("h", "Chiều cao (m)", _adRailH);
        f.Combo("use", "Tải ngang tay vịn (TCVN 2737)", new[] { "Nhà ở – 0,3 kN/m", "Công trình công cộng – 0,8 kN/m", "Khán đài, sân vận động – 1,5 kN/m", "Không xét" }, _adRailUse);
        f.Hint("Bấm các điểm trên mặt bằng (bắt nút, giao trục), Enter kết thúc, Backspace bỏ điểm cuối, Esc dừng. "
               + "Tải đứng: inox 0,3 · kính 0,5 · sắt 0,4 · xây 110 ≈ 2,52·H kN/m – tự dồn lên dầm đỡ (hoặc sàn) khi phân tích.");
        if (!f.Run("Bắt đầu vẽ")) return;
        _adRailKind = (RailKind)Math.Max(0, f.I("k"));
        _adRailH = Math.Clamp(f.D("h", 1.0), 0.3, 3.0);
        _adRailUse = Math.Max(0, f.I("use"));
        StartAdTool(AdTool.Railing, $"Lan can ({ArchDetail.RailName(_adRailKind)}, cao {_adRailH:0.00} m): bấm điểm đầu.");
    }

    private void SweepUi()
    {
        if (_p == null) return;
        string st = AdStory();
        if (st == null) return;
        var f = new MiniForm(this, "Phào chỉ / gờ trang trí", 520);
        f.Combo("pre", "Mẫu", ArchDetail.SweepPresets.Select(q => $"{q.Name} – {q.Note}"), Array.IndexOf(ArchDetail.SweepPresets, _adSweep));
        f.Text("z", "Cao độ tâm so với sàn (m, để trống = theo mẫu)", "");
        f.Combo("mode", "Cách đặt", new[] { "Bấm từng mặt tường trên mặt bằng", "Mọi tường biên của tầng – mặt ngoài nhà", "Mọi tường của tầng – mặt trong nhà" });
        if (!f.Run("Đặt")) return;
        var pre = ArchDetail.SweepPresets[Math.Max(0, f.I("pre"))];
        if (!string.IsNullOrWhiteSpace(f.S("z"))) pre = pre with { Z = f.D("z", pre.Z) };
        _adSweep = pre;
        int mode = f.I("mode");
        if (mode == 0) { StartAdTool(AdTool.Sweep, $"{pre.Name}: bấm sát mặt tường cần đặt (phía bấm = mặt tường), Esc dừng."); return; }
        string snap = Snapshot();
        int n = 0;
        foreach (var w in mode == 1 ? ArchDetail.PerimeterWalls(_p, st) : _p.Walls.Where(q => q.Story == st && !q.IsCurved).ToList())
        {
            int outSide = ArchDetail.OutsideSide(_p, w);
            if (mode == 1) { ArchDetail.AddSweep(_p, w, outSide, pre); n++; }
            else
            {
                bool perim = ArchDetail.PerimeterWalls(_p, st).Contains(w);
                if (perim) { ArchDetail.AddSweep(_p, w, -outSide, pre); n++; }
                else { ArchDetail.AddSweep(_p, w, 1, pre); ArchDetail.AddSweep(_p, w, -1, pre); n += 2; }
            }
        }
        if (n == 0) { SetStatus("Tầng không có tường thẳng để đặt phào."); return; }
        ArchDetailChanged($"Đã đặt {n} đoạn {pre.Name} (tầng {st}).", snap);
    }

    private void CladdingUi()
    {
        if (_p == null) return;
        string st = AdStory();
        if (st == null) return;
        var f = new MiniForm(this, "Ốp / sơn mặt tường", 520);
        f.Combo("pre", "Vật liệu", ArchDetail.CladPresets.Select(q => q.Material), Array.IndexOf(ArchDetail.CladPresets, _adClad));
        f.Num("z0", "Từ cao độ (m so với sàn)", _adClad.Z0);
        f.Num("z1", "Đến cao độ (m)", _adClad.Z1);
        f.Combo("mode", "Cách đặt", new[] { "Bấm từng mặt tường trên mặt bằng", "Mọi tường biên của tầng – mặt ngoài nhà" });
        f.Hint("Ô cửa tự trừ khỏi diện tích ốp. Ốp gạch men WC: 0…2,7 m; ốp chân tường ngoài: 0…0,6 m.");
        if (!f.Run("Đặt")) return;
        var pre = ArchDetail.CladPresets[Math.Max(0, f.I("pre"))];
        double z0 = f.D("z0", pre.Z0), z1 = f.D("z1", pre.Z1);
        if (z1 <= z0) { MessageBox.Show(this, "Cao độ trên phải lớn hơn cao độ dưới.", AppTitle); return; }
        _adClad = pre with { Z0 = z0, Z1 = z1 };
        if (f.I("mode") == 0) { StartAdTool(AdTool.Cladding, $"{pre.Material} {z0:0.00}…{z1:0.00} m: bấm sát mặt tường cần ốp, Esc dừng."); return; }
        string snap = Snapshot();
        int n = 0;
        foreach (var w in ArchDetail.PerimeterWalls(_p, st)) { ArchDetail.AddCladding(_p, w, ArchDetail.OutsideSide(_p, w), _adClad, z0, z1); n++; }
        if (n == 0) { SetStatus("Tầng không có tường biên."); return; }
        ArchDetailChanged($"Đã ốp {n} mặt tường ngoài bằng {pre.Material} (tầng {st}).", snap);
    }

    private void StartAdTool(AdTool t, string msg)
    {
        _adTool = t; _adPts.Clear();
        if (planHost != null) planHost.Cursor = Cursors.Cross;
        SetStatus(msg);
        DrawHover();
    }

    private void StopAdTool(string msg = null)
    {
        _adTool = AdTool.None; _adPts.Clear();
        if (planHost != null) planHost.Cursor = null;
        DrawHover();
        SetStatus(msg ?? ModeHint(_mode));
    }

    /// <summary>Bấm trên mặt bằng khi đang dùng công cụ chi tiết KT (gọi đầu FurnishPlanClick).</summary>
    private bool ArchDetailClick(Point s)
    {
        if (_adTool == AdTool.None || _p == null) return false;
        string st = AdStory(false);
        if (st == null) { SetStatus("Chọn một tầng cụ thể để vẽ."); return true; }
        var (x, y) = S2W(s);
        if (_adTool == AdTool.Railing)
        {
            var q = SnapPlanXY(x, y);
            _adPts.Add(q);
            SetStatus($"Lan can: {_adPts.Count} điểm – bấm tiếp, Enter kết thúc, Backspace bỏ điểm cuối, Esc dừng.");
            DrawHover();
            return true;
        }
        var w = WallAtScreen(s);
        if (w == null || w.IsCurved) { SetStatus("Bấm sát một đoạn tường thẳng của tầng đang xem."); return true; }
        int side = ArchDetail.SideOf(w, x, y);
        string snap = Snapshot();
        if (_adTool == AdTool.Sweep)
        {
            var sw = ArchDetail.AddSweep(_p, w, side, _adSweep);
            ArchDetailChanged($"Đã đặt {sw.Name} dài {ArchDetail.SweepLength(_p, sw):0.00} m trên tường #{w.Id} – bấm tiếp, Esc dừng.", snap);
        }
        else
        {
            var c = ArchDetail.AddCladding(_p, w, side, _adClad, _adClad.Z0, _adClad.Z1);
            ArchDetailChanged($"Đã ốp {c.Material} {ArchDetail.CladdingArea(_p, c):0.00} m² trên tường #{w.Id} – bấm tiếp, Esc dừng.", snap);
        }
        return true;
    }

    private void FinishRailing()
    {
        string st = AdStory(false);
        if (st == null || _adPts.Count < 2) { SetStatus("Lan can cần ít nhất 2 điểm."); return; }
        try
        {
            string snap = Snapshot();
            var r = ArchDetail.AddRailing(_p, st, _adPts, _adRailKind, _adRailH);
            r.HorizontalLoad = RailHz[Math.Clamp(_adRailUse, 0, RailHz.Length - 1)];
            _adPts.Clear();
            ArchDetailChanged($"Đã vẽ {ArchDetail.RailName(r.Kind)} dài {r.Length:0.00} m, tải {ArchDetail.RailLoad(r):0.##} kN/m – bấm điểm đầu lan can tiếp theo, Esc dừng.", snap);
        }
        catch (Exception ex) { ShowError("Lan can", ex); }
    }

    /// <summary>Phím khi đang dùng công cụ chi tiết KT (gọi đầu FurnishPlanKey).</summary>
    private bool ArchDetailKey(Key key)
    {
        if (_adTool == AdTool.None || IsTextInputFocused()) return false;
        if (key == Key.Escape)
        {
            if (_adTool == AdTool.Railing && _adPts.Count >= 2) FinishRailing();
            StopAdTool();
            return true;
        }
        if (_adTool == AdTool.Railing && (key == Key.Enter || key == Key.Space)) { FinishRailing(); return true; }
        if (_adTool == AdTool.Railing && key == Key.Back && _adPts.Count > 0) { _adPts.RemoveAt(_adPts.Count - 1); DrawHover(); return true; }
        return false;
    }

    /// <summary>Dây chun khi vẽ lan can (gọi đầu DrawHover).</summary>
    private bool DrawArchDetailOverlay()
    {
        if (_adTool != AdTool.Railing || _p == null) return false;
        var br = new SolidColorBrush(Color.FromRgb(0, 140, 120));
        var pts = _adPts.Select(q => W2S(q.X, q.Y)).ToList();
        if (_lastMouse != null && pts.Count > 0) pts.Add(_lastMouse.Value);
        for (int i = 1; i < pts.Count; i++)
            planOverlay.Children.Add(new Shapes.Line { X1 = pts[i - 1].X, Y1 = pts[i - 1].Y, X2 = pts[i].X, Y2 = pts[i].Y, Stroke = br, StrokeThickness = 2, IsHitTestVisible = false, StrokeDashArray = i == pts.Count - 1 && _lastMouse != null ? new DoubleCollection { 4, 2 } : null });
        foreach (var q in _adPts)
        {
            var c = W2S(q.X, q.Y);
            var e = new Shapes.Ellipse { Width = 7, Height = 7, Fill = br, IsHitTestVisible = false };
            Canvas.SetLeft(e, c.X - 3.5); Canvas.SetTop(e, c.Y - 3.5);
            planOverlay.Children.Add(e);
        }
        if (_lastMouse != null && _adPts.Count > 0)
        {
            var (x, y) = S2W(_lastMouse.Value);
            double L = Math.Sqrt(Math.Pow(x - _adPts[^1].X, 2) + Math.Pow(y - _adPts[^1].Y, 2));
            AddText($"{L:0.00} m", new Point(_lastMouse.Value.X + 12, _lastMouse.Value.Y + 10), br, 11, false, false, planOverlay);
        }
        return true;
    }

    /// <summary>Vẽ mái, lan can, phào chỉ, ốp trên mặt bằng (gọi trong DrawArchExtras).</summary>
    private void DrawArchDetails(bool labels)
    {
        var a = _p?.Arch;
        if (a == null) return;
        var roofBr = new SolidColorBrush(Color.FromRgb(160, 70, 50));
        foreach (var r in a.Roofs.Where(r => OnStory(r.Story) && r.Outline.Count >= 3))
        {
            var (faces, gables) = RoofGeometry.Build(r, 0);
            foreach (var f in faces)
            {
                var poly = new Shapes.Polygon
                {
                    Points = new PointCollection(f.Select(q => W2S(q.X, q.Y))), Stroke = roofBr, StrokeThickness = 0.9,
                    StrokeDashArray = new DoubleCollection { 6, 3 }, Fill = new SolidColorBrush(Color.FromArgb(18, 160, 70, 50)),
                    ToolTip = $"{r.Type} – {RoofShapeNames[(int)r.Shape]}, dốc {r.SlopePercent:0.#}%, đua {r.Overhang:0.00} m", IsHitTestVisible = true
                };
                planCanvas.Children.Add(poly);
            }
            foreach (var g in gables)
                planCanvas.Children.Add(new Shapes.Line { X1 = W2S(g[0].X, g[0].Y).X, Y1 = W2S(g[0].X, g[0].Y).Y, X2 = W2S(g[1].X, g[1].Y).X, Y2 = W2S(g[1].X, g[1].Y).Y, Stroke = roofBr, StrokeThickness = 3, IsHitTestVisible = false });
            if (labels && faces.Count > 0)
            {
                double cx = faces.SelectMany(q => q).Average(q => q.X), cy = faces.SelectMany(q => q).Average(q => q.Y);
                AddText($"{r.Type} · i = {r.SlopePercent:0.#}%", W2S(cx, cy), roofBr, 10, true, true);
            }
        }
        var railBr = new SolidColorBrush(Color.FromRgb(0, 140, 120));
        foreach (var r in a.Railings.Where(r => OnStory(r.Story) && r.Points.Count >= 2))
        {
            var pl = new Shapes.Polyline
            {
                Points = new PointCollection(r.Points.Select(q => W2S(q[0], q[1]))), Stroke = railBr, StrokeThickness = r.Kind == RailKind.Xay ? 3.5 : 2,
                StrokeDashArray = r.Kind == RailKind.Kinh ? new DoubleCollection { 3, 1.5 } : null,
                ToolTip = $"{ArchDetail.RailName(r.Kind)} cao {r.Height:0.00} m, dài {r.Length:0.00} m – tải {ArchDetail.RailLoad(r):0.##} kN/m"
            };
            planCanvas.Children.Add(pl);
        }
        foreach (var s in a.Sweeps) DrawFaceBand(s.WallId, s.Side, s.Width, Color.FromRgb(200, 170, 60), $"{s.Name} – Z = {s.Z:0.00} m");
        foreach (var c in a.Claddings) { var col = ArchDetail.CladColor(c.Material); DrawFaceBand(c.WallId, c.Side, 0.03, Color.FromRgb((byte)(col[0] * 255), (byte)(col[1] * 255), (byte)(col[2] * 255)), $"{c.Material} {c.Z0:0.00}…{c.Z1:0.00} m – {ArchDetail.CladdingArea(_p, c):0.00} m²"); }
    }

    private void DrawFaceBand(int wallId, int side, double width, Color col, string tip)
    {
        var w = ArchDetail.WallOf(_p, wallId);
        if (w == null || w.IsCurved || !OnStory(w.Story) || w.Chord < 1e-6) return;
        double t = ModelSolids.WallThickness(_p, w), L = w.Chord;
        double nx = -(w.Y2 - w.Y1) / L * side, ny = (w.X2 - w.X1) / L * side, off = t / 2 + Math.Max(0.02, width) / 2;
        var a = W2S(w.X1 + nx * off, w.Y1 + ny * off); var b = W2S(w.X2 + nx * off, w.Y2 + ny * off);
        planCanvas.Children.Add(new Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = new SolidColorBrush(col), StrokeThickness = 2.5, ToolTip = tip });
    }

    /// <summary>Sửa kích thước / cao độ / góc xoay đồ đạc (họ tham số dựng lại hình theo kích thước mới).</summary>
    private void EditFurnitureSize(FurnitureItem fi)
    {
        if (fi == null) return;
        var f = new MiniForm(this, "Thuộc tính đồ đạc – " + fi.Name);
        f.Text("name", "Tên", fi.Name);
        f.Num("w", "Rộng (m)", fi.Width); f.Num("d", "Sâu (m)", fi.Depth); f.Num("h", "Cao (m)", fi.Height);
        f.Num("el", "Cao độ đáy so với sàn (m)", fi.Elevation);
        f.Num("rot", "Góc xoay (độ)", fi.Rotation);
        f.Hint(fi.Family != null ? $"Họ tham số \"{fi.Family}\": chân, mặt, đầu giường, tay sofa… tự dựng lại theo kích thước mới." : "Khối thư viện nhập từ SketchUp / Revit: hiển thị theo khối bao.");
        if (!f.Run("Áp dụng")) return;
        string snap = Snapshot();
        fi.Name = string.IsNullOrWhiteSpace(f.S("name")) ? fi.Name : f.S("name").Trim();
        fi.Width = Math.Clamp(f.D("w", fi.Width), 0.05, 20); fi.Depth = Math.Clamp(f.D("d", fi.Depth), 0.05, 20); fi.Height = Math.Clamp(f.D("h", fi.Height), 0.02, 10);
        fi.Elevation = f.D("el", fi.Elevation);
        fi.Rotation = FurnitureSnap.Norm(f.D("rot", fi.Rotation));
        PushUndo(snap);
        FurnishChanged();
        SetStatus($"{fi.Name}: {fi.Width:0.00} × {fi.Depth:0.00} × {fi.Height:0.00} m.");
    }

    // ==================================================================
    // Chép kiến trúc sang tầng, khối lượng
    // ==================================================================
    private void CopyArchUi()
    {
        if (_p == null) return;
        string src = AdStory();
        if (src == null) return;
        var f = new MiniForm(this, $"Chép kiến trúc tầng {src} sang tầng khác");
        var targets = new List<CheckBox>();
        foreach (var s in _p.Stories.OrderBy(s => s.Elevation).Where(s => s.Name != src))
        {
            var cb = new CheckBox { Content = $"{s.Name} (+{s.Elevation:0.000})", Tag = s.Name, Margin = new Thickness(0, 2, 0, 2) };
            targets.Add(cb); f.Add(cb);
        }
        f.Check("w", "Tường (kèm cửa, phào chỉ, ốp)", true);
        f.Check("r", "Phòng", true); f.Check("c", "Trần", true); f.Check("l", "Lan can", true); f.Check("f", "Đồ đạc", true);
        f.Hint("Tường trùng vị trí ở tầng đích được bỏ qua. Tải tường, lan can tự sinh lại khi phân tích.");
        if (!f.Run("Sao chép")) return;
        var tg = targets.Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToList();
        if (tg.Count == 0) return;
        string snap = Snapshot();
        int n = ArchDetail.CopyToStories(_p, src, tg, new ArchDetail.CopyOptions { Walls = f.B("w"), Rooms = f.B("r"), Ceilings = f.B("c"), Railings = f.B("l"), Furniture = f.B("f") });
        AfterModelEdit($"Đã chép {n} phần tử kiến trúc từ {src} sang {string.Join(", ", tg)}.", snap);
    }

    private void ArchQtyUi()
    {
        if (_p == null) return;
        var rows = ArchDetail.Quantities(_p);
        var w = new Window { Owner = this, Title = "Khối lượng hoàn thiện kiến trúc", Width = 760, Height = 520, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var g = DialogUi.NewGrid();
        g.IsReadOnly = true;
        g.ItemsSource = rows;
        g.Columns.Add(DialogUi.Text("Nhóm", "Group", true, 90));
        g.Columns.Add(DialogUi.Text("Tầng", "Story", true, 70));
        g.Columns.Add(DialogUi.Text("Hạng mục", "Item", true, 330));
        g.Columns.Add(DialogUi.Text("Đơn vị", "Unit", true, 60));
        g.Columns.Add(DialogUi.Num("Khối lượng", "Qty", "0.##", true, 100));
        var save = DialogUi.Btn("Xuất CSV…", (s, e) =>
        {
            var d = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "KhoiLuongHoanThienKT.csv" };
            if (d.ShowDialog(w) != true) return;
            File.WriteAllText(d.FileName, ArchDetail.QuantitiesCsv(_p), new System.Text.UTF8Encoding(true));
        });
        var dock = new DockPanel { Margin = new Thickness(8) };
        var bar = DialogUi.Row(save, new TextBlock { Text = rows.Count == 0 ? "Chưa có mái, trần, lan can, phào chỉ, ốp nào." : $"{rows.Count} dòng", VerticalAlignment = VerticalAlignment.Center });
        DockPanel.SetDock(bar, Dock.Bottom);
        dock.Children.Add(bar); dock.Children.Add(g);
        w.Content = dock;
        w.ShowDialog();
    }

    /// <summary>Hướng dẫn cài plugin SketchUp kèm theo (VietConst_SketchUp.rbz trong thư mục cài) và mở thư mục chứa.</summary>
    private void SketchUpPluginUi()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "SketchUp");
        string rbz = Path.Combine(dir, "VietConst_SketchUp.rbz");
        string ms = Path.Combine(AppContext.BaseDirectory, "3dsMax", "VietConst_Export.ms");
        string msg = "CÔNG CỤ XUẤT SANG VIỆT CONST (chỉ ghi file trên máy, không kết nối mạng, không thu thập dữ liệu)\n\n"
                     + "• SketchUp – plugin VietConst_SketchUp.rbz: Window → Extension Manager → Install Extension.\n"
                     + "   Extensions → Việt Const → Xuất cả mô hình / phần chọn (*.vxtmesh.json) hoặc \"Đưa component đang chọn vào thư viện\".\n"
                     + "• 3ds Max – VietConst_Export.ms: Scripting → Run Script… → bảng Việt Const: đưa đối tượng / nhóm vào thư viện, xuất mô hình.\n"
                     + "• Revit – add-in Việt Const: lệnh \"Đưa họ vào thư viện Việt Const\" (mỗi loại họ một mục, theo gốc chèn của họ) và \"Xuất sang Việt Const\".\n"
                     + "• Phần mềm khác: xuất OBJ / DAE / DXF 3D (3DFACE, lưới đa diện, MESH) → BIM → Thư viện 3D → Nhập….\n\n"
                     + "Mục thư viện ghi thẳng vào %AppData%\\VietConst\\ThuVien3D – mở lại Thư viện 3D là thấy; ký hiệu mặt bằng tự lấy theo hình chiếu bằng.\n\n"
                     + (File.Exists(rbz) ? "Plugin SketchUp: " + rbz + "\n" : "") + (File.Exists(ms) ? "Công cụ 3ds Max: " + ms + "\n" : "")
                     + (File.Exists(rbz) ? "\nMở thư mục chứa plugin SketchUp?" : "\nKhông thấy plugin trong thư mục cài (" + dir + ") – cài lại Việt Const bản mới.");
        if (MessageBox.Show(this, msg, "Công cụ xuất SketchUp / 3ds Max / Revit", File.Exists(rbz) ? MessageBoxButton.YesNo : MessageBoxButton.OK, MessageBoxImage.Information) == MessageBoxResult.Yes)
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "/select,\"" + rbz + "\"") { UseShellExecute = true }); } catch (Exception ex) { ShowError("Mở thư mục", ex); }
    }

    // ==================================================================
    // Nhập mô hình 3D SketchUp / Revit → mặt bằng tầng → kết cấu
    // ==================================================================
    private void ImportMeshModelUi()
    {
        if (_p == null || _busy) return;
        var od = new OpenFileDialog
        {
            Title = "Mô hình 3D: SketchUp (File → Export → 3D Model → OBJ / COLLADA) hoặc Revit (OBJ, *.vxtmesh.json)",
            Filter = "Mô hình 3D (*.obj;*.dae;*.json)|*.obj;*.dae;*.json|Mọi file (*.*)|*.*"
        };
        if (od.ShowDialog(this) != true) return;
        var f = new MiniForm(this, "Nhập mô hình 3D → mặt bằng các tầng", 520);
        f.Combo("unit", "Đơn vị file OBJ", new[] { "Tự nhận theo kích thước", "m", "cm", "mm", "inch (SketchUp mặc định)" });
        f.Combo("up", "Trục đứng", new[] { "Tự nhận", "Y lên (OBJ SketchUp: “Swap YZ”)", "Z lên (Revit, DAE)" });
        f.Num("cut", "Cao độ cắt mặt bằng trên sàn (m)", 1.2);
        f.Hint("Chương trình tìm các mặt sàn (mặt ngang lớn hướng lên), cắt ngang mỗi tầng để lấy tường, cột (vòng kín ≤ 0,8 m), cửa đi / cửa sổ (khe hở trên tường), "
               + "ghi thành bản vẽ DXF rồi mở bảng \"Nhập bản vẽ kiến trúc\" để nhận diện và triển khai kết cấu như với bản vẽ CAD. Mọi xử lý trên máy.");
        if (!f.Run("Đọc mô hình")) return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var o = new MeshPlanOptions
            {
                Scale = f.I("unit") switch { 1 => 1, 2 => 0.01, 3 => 0.001, 4 => 0.0254, _ => 0 },
                UpAxis = f.I("up") switch { 1 => 1, 2 => 2, _ => 0 },
                CutHeight = Math.Clamp(f.D("cut", 1.2), 0.5, 2.0)
            };
            var mesh = MeshToPlan.Read(od.FileName, o);
            var res = MeshToPlan.Build(mesh, o);
            Mouse.OverrideCursor = null;
            if (res.Levels.Count < 2 || res.Walls == 0)
            {
                MessageBox.Show(this, "Không nhận được mặt bằng tầng từ mô hình.\n\n" + string.Join("\n", res.Info) + "\n\nThử chọn lại đơn vị / trục đứng, hoặc kiểm tra mô hình có sàn (mặt ngang) cho từng tầng.",
                    AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VietConst", "NhapMoHinh");
            Directory.CreateDirectory(dir);
            string dxf = Path.Combine(dir, Path.GetFileNameWithoutExtension(od.FileName) + "_MatBangCat.dxf");
            MeshToPlan.WriteDxf(res.Entities, dxf);
            var hs = res.Heights;
            double typical = hs.Count > 1 ? hs.Skip(1).OrderBy(h => h).ElementAt((hs.Count - 1) / 2) : hs[0];
            string msg = $"Đọc được {res.Levels.Count - 1} tầng + mái, chiều cao {string.Join(" / ", hs.Select(h => h.ToString("0.00", Inv)))} m.\n"
                         + string.Join("\n", res.Info) + $"\n\nĐã ghi mặt bằng cắt: {dxf}\n\nMở bảng nhận diện kiến trúc để triển khai kết cấu?";
            if (MessageBox.Show(this, msg, AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
            var hint = res.Roof;
            ImportArchWith(d => d.Preload(new[] { dxf }, hs[0], typical, null), hint == null ? null : () =>
            {
                // mái dốc đọc từ mô hình → mái theo đường bao sàn tầng trên cùng
                var top = _p.Stories.OrderBy(s => s.Elevation).LastOrDefault();
                if (top == null) return;
                string snap = Snapshot();
                var outline = ArchDetail.FootprintOutline(_p, top.Name);
                var r = outline != null && outline.Count >= 4
                    ? ArchDetail.AddRoof(_p, top.Name, outline, hint.Shape, hint.SlopePercent, 0.5, "Mái (từ mô hình 3D)")
                    : ArchDetail.AddRoof(_p, top.Name, hint.X0, hint.Y0, hint.X1, hint.Y1, hint.Shape, hint.SlopePercent, 0, "Mái (từ mô hình 3D)");
                r.SlabLoad = ArchDetailLoads.DefaultRoofLoad(r.Type);
                ArchDetailChanged($"Đã thêm mái dốc từ mô hình: i = {r.SlopePercent:0}%, {RoofGeometry.SlopedArea(r):0} m² trên sàn {top.Name} (sửa loại mái / tải ở BIM → Mái…).", snap);
            });
        }
        catch (Exception ex) { Mouse.OverrideCursor = null; ShowError("Không đọc được mô hình 3D", ex); }
        finally { Mouse.OverrideCursor = null; }
    }
}

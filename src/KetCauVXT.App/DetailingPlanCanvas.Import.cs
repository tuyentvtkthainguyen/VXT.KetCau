using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Detailing;
using KetCauVXT.Import;

namespace KetCauVXT.App;

/// <summary>[Triển khai – nhập từ AutoCAD] Hộp thoại nhận diện layer khi nhập mặt bằng kết cấu (DXF/DWG hoặc lệnh VXT_MB_KC):
/// bảng layer của bản vẽ (số nét, đa tuyến kín cỡ cột, cung tròn, chữ) + vai trò gợi ý (Cột / Dầm / Sàn / Bỏ qua) – người dùng sửa;
/// gốc toạ độ: dời về giao trục đầu / đặt theo ánh xạ (xref) kiến trúc của tầng / giữ toạ độ bản vẽ; tạo sàn từ dầm; sao chép sang
/// các mặt bằng giống nhau. Cùng file: Tạo sàn từ dầm, Sao chép sang tầng có sẵn.</summary>
public sealed partial class DetailingPlanCanvas
{
    internal sealed class ImportOptions
    {
        public ArchLayerKeys Keys = new();
        public bool AlignOrigin = true, Replace, AutoSlabs = true, MergeBeams = true;
        public (double X, double Y)? Origin;
        public List<PlanLevel> CopyTo = new();
    }

    private sealed class LayerRow
    {
        public string Layer { get; set; } = "";
        public int Lines { get; set; }
        public int Rects { get; set; }
        public int Arcs { get; set; }
        public int Texts { get; set; }
        public string Role { get; set; } = "";
    }

    private static readonly string[] Roles = { "", "Cột", "Dầm", "Sàn" };

    /// <summary>Tự kiểm tra: bỏ qua hộp thoại, dùng gợi ý.</summary>
    internal static bool AutoAcceptImport;

    internal ImportOptions AskImportOptions(PlanLevel lv, List<DxfEntity> ents, string source)
    {
        var o = new ImportOptions();
        var stats = PlanCadGeom.LayerStats(ents);
        var rows = stats.Select(s => new LayerRow { Layer = s.Layer, Lines = s.Lines, Rects = s.ColRects, Arcs = s.Arcs, Texts = s.Texts, Role = s.Suggest }).ToList();
        bool hasUnderlay = !string.IsNullOrEmpty(lv.Underlay) || !string.IsNullOrEmpty(lv.UnderlayRel);
        bool hasItems = lv.Items.Any(i => i.Kind == PlanKinds.Beam || i.Kind == PlanKinds.Column);
        void Fill()
        {
            HashSet<string> Set(string role) => new(rows.Where(r => r.Role == role).Select(r => r.Layer), StringComparer.OrdinalIgnoreCase);
            o.Keys = new ArchLayerKeys { BeamLayers = Set("Dầm"), ColumnLayers = Set("Cột"), SlabLayers = Set("Sàn") };
        }
        if (AutoAcceptImport)
        {
            Fill();
            o.Replace = hasItems;
            if (hasUnderlay) { o.AlignOrigin = false; o.Origin = (-lv.UnderlayDx, -lv.UnderlayDy); }
            return o;
        }

        var w = new Window { Owner = _owner };
        DialogUi.Setup(w, "Nhập mặt bằng kết cấu – nhận diện layer" + (string.IsNullOrEmpty(source) ? "" : " – " + source), 760, 680);
        var g = DialogUi.NewGrid();
        g.ItemsSource = rows;
        g.Columns.Add(DialogUi.Text("Layer", "Layer", true, 200));
        g.Columns.Add(DialogUi.Combo("Vai trò", "Role", Roles, 90));
        g.Columns.Add(DialogUi.Num("Nét", "Lines", "0", true));
        g.Columns.Add(DialogUi.Num("Đa tuyến kín cỡ cột", "Rects", "0", true));
        g.Columns.Add(DialogUi.Num("Cung tròn", "Arcs", "0", true));
        g.Columns.Add(DialogUi.Num("Chữ", "Texts", "0", true));
        g.Height = 280;
        DialogUi.HScroll(g, 1);
        var body = new StackPanel { Margin = new Thickness(10) };
        body.Children.Add(DialogUi.Hint("Chọn vai trò từng layer: Cột (đa tuyến kín → tâm + b×h), Dầm (cặp nét song song → tim + bề rộng; nét đơn → tim; CUNG TRÒN / cung lồi đa tuyến → DẦM CONG), Sàn. Để trống = bỏ qua. Gợi ý theo tên layer (COT/COLUMN, DAM/BEAM/GIANG, SAN/SLAB, layer chuẩn VXT-…) và hình (nhiều đa tuyến kín cỡ cột). Chữ \"D1 220x400\", \"C1\", \"S1\" gần cấu kiện → tên, tiết diện."));
        body.Children.Add(g);
        var rbGrid = new RadioButton { Content = "Dời gốc về giao trục đầu (điểm trái – dưới nhất của cột / dầm trùng trục 1-A)", IsChecked = !hasUnderlay, Margin = new Thickness(0, 6, 0, 2), GroupName = "org" };
        var rbUnder = new RadioButton { Content = "Đặt theo ánh xạ (xref) kiến trúc của tầng – bản vẽ kết cấu cùng toạ độ với mặt bằng kiến trúc" + (hasUnderlay ? $" (dời {lv.UnderlayDx:0.###}; {lv.UnderlayDy:0.###} m)" : " – tầng chưa gắn ánh xạ"), IsChecked = hasUnderlay, IsEnabled = hasUnderlay, Margin = new Thickness(0, 2, 0, 2), GroupName = "org" };
        var rbKeep = new RadioButton { Content = "Giữ toạ độ bản vẽ", Margin = new Thickness(0, 2, 0, 2), GroupName = "org" };
        var gbOrg = new GroupBox { Header = "Gốc toạ độ", Margin = new Thickness(0, 6, 0, 0) };
        var spOrg = new StackPanel(); spOrg.Children.Add(rbGrid); spOrg.Children.Add(rbUnder); spOrg.Children.Add(rbKeep); gbOrg.Content = spOrg;
        body.Children.Add(gbOrg);
        var chkSlab = new CheckBox { Content = "Tạo mặt bằng sàn từ mặt bằng dầm đã nhận (ô kín bao bởi dầm; ô cùng kích thước dùng chung S1, S2…)", IsChecked = true, Margin = new Thickness(0, 6, 0, 2) };
        var chkReplace = new CheckBox { Content = "Xoá vị trí dầm, cột, sàn cũ của mặt bằng " + lv.Name + " trước khi nhập", IsChecked = hasItems, IsEnabled = hasItems, Margin = new Thickness(0, 2, 0, 2) };
        var chkMerge = new CheckBox { Content = "Dầm thẳng hàng nối liền (cùng tên / tiết diện) là MỘT dầm dài nhiều nhịp – chia lại sau bằng công cụ \"Chia dầm\"", IsChecked = true, Margin = new Thickness(0, 2, 0, 2) };
        body.Children.Add(chkMerge); body.Children.Add(chkSlab); body.Children.Add(chkReplace);
        var others = Plan.Levels.Where(l => l != lv && !l.IsFoundation).ToList();
        var lst = new ListBox { SelectionMode = SelectionMode.Multiple, Height = 70, ItemsSource = others.Select(l => l.Name).ToList() };
        if (others.Count > 0)
            body.Children.Add(new GroupBox { Header = "Sao chép mặt bằng vừa nhập sang các tầng giống nhau (chọn – tuỳ chọn)", Content = lst, Margin = new Thickness(0, 6, 0, 0) });
        var info = DialogUi.Hint("");
        body.Children.Add(info);
        bool ok = false;
        var btnOk = DialogUi.Btn("Nhập", (s, e) =>
        {
            DialogUi.Commit(g);
            if (!rows.Any(r => r.Role == "Dầm" || r.Role == "Cột")) { info.Text = "Chọn ít nhất một layer Cột hoặc Dầm."; info.Foreground = Brushes.DarkOrange; return; }
            ok = true; w.Close();
        }, true);
        btnOk.IsDefault = true;
        w.Content = DialogUi.ScrollBody(body, DialogUi.Row(btnOk, DialogUi.Btn("Huỷ", (s, e) => w.Close())));
        w.ShowDialog();
        if (!ok) return null;
        Fill();
        o.AutoSlabs = chkSlab.IsChecked == true;
        o.MergeBeams = chkMerge.IsChecked == true;
        o.Replace = chkReplace.IsChecked == true;
        if (rbUnder.IsChecked == true) { o.AlignOrigin = false; o.Origin = (-lv.UnderlayDx, -lv.UnderlayDy); }
        else if (rbKeep.IsChecked == true) { o.AlignOrigin = false; o.Origin = (0, 0); }
        o.CopyTo = lst.SelectedItems.OfType<string>().Select(n => others.First(l => l.Name == n)).ToList();
        return o;
    }

    /// <summary>Nút "Gộp dầm thẳng hàng".</summary>
    private void MergeBeamsHere()
    {
        var lv = Level; if (lv == null) return;
        _undo.Push(Plan);
        var log = new List<string>();
        int n = PlanBeamMerge.Merge(P, lv, false, null, log);
        _changed(); FillMembers(); UpdateHint(); Redraw();
        _status.Text = n > 0 ? string.Join(" ", log) : "Không có đoạn dầm thẳng hàng nối liền cùng tiết diện để gộp.";
    }

    /// <summary>Nút "Tạo sàn từ dầm".</summary>
    private void AutoSlabsHere()
    {
        var lv = Level; if (lv == null) return;
        _undo.Push(Plan);
        var log = new List<string>();
        int n = PlanAutoSlab.Create(P, lv, log);
        _changed(); FillMembers(); UpdateHint(); Redraw();
        _status.Text = string.Join(" ", log) + (n == 0 ? " Không có ô kín mới (cần dầm thẳng song song trục bao kín ô; ô đã có sàn được giữ)." : "");
    }

    /// <summary>Nút "Sao chép sang tầng có sẵn…".</summary>
    private void CopyToLevelsDialog()
    {
        var src = Level; if (src == null) return;
        if (src.Items.Count == 0) { _status.Text = "Mặt bằng " + src.Name + " chưa có cấu kiện để sao chép."; return; }
        var others = Plan.Levels.Where(l => l != src && l.IsFoundation == src.IsFoundation).ToList();
        if (others.Count == 0) { _status.Text = "Chưa có mặt bằng nào khác – dùng \"⧉ Nhân bản tầng…\" để tạo tầng mới giống tầng này."; return; }
        var w = new Window { Owner = _owner };
        DialogUi.Setup(w, "Sao chép mặt bằng " + src.Name + " sang tầng có sẵn", 520, 460);
        var sp = new StackPanel { Margin = new Thickness(12) };
        sp.Children.Add(DialogUi.Hint("Sao toàn bộ vị trí cấu kiện (cột, dầm – cả dầm cong, sàn, lanh tô, ô văng, cầu thang) và đường cắt sàn của " + src.Name +
                                      " sang các mặt bằng chọn dưới đây. Dùng chung cấu kiện (cùng tên D1, S1…; SL tự cập nhật theo số vị trí)."));
        var lst = new ListBox { SelectionMode = SelectionMode.Multiple, Height = 200, ItemsSource = others.Select(l => $"{l.Name}  (cao độ {l.Elevation:0.###} m, {l.Items.Count} vị trí)").ToList(), Margin = new Thickness(0, 6, 0, 6) };
        var chkRep = new CheckBox { Content = "Xoá vị trí cũ của mặt bằng đích trước khi sao chép", IsChecked = true };
        sp.Children.Add(lst); sp.Children.Add(chkRep);
        bool ok = false;
        w.Content = DialogUi.ScrollBody(sp, DialogUi.Row(DialogUi.Btn("Sao chép", (s, e) => { ok = lst.SelectedItems.Count > 0; w.Close(); }, true), DialogUi.Btn("Huỷ", (s, e) => w.Close())));
        w.ShowDialog();
        if (!ok) return;
        var targets = lst.SelectedItems.OfType<string>().Select(t => others[lst.Items.IndexOf(t)]).ToList();
        _undo.Push(Plan);
        int n = PlanCopy.CopyTo(P, src, targets, chkRep.IsChecked == true);
        _changed(); FillMembers(); UpdateHint(); Redraw();
        _status.Text = $"Đã sao chép {n} vị trí của {src.Name} sang {string.Join(", ", targets.Select(t => t.Name))} (dùng chung cấu kiện – SL đã cập nhật).";
    }
}

public sealed partial class DetailingPlanCanvas
{
    /// <summary>[Sàn hạ cốt] nhập độ hạ (mm) cho vị trí sàn; 0 = bỏ hạ cốt.</summary>
    private void AskDrop(PlanItem slab, bool pushUndo = true)
    {
        if (slab == null) return;
        string s = DialogUi.InputBox(_owner, "Sàn hạ cốt", "Độ hạ mặt sàn so với cao độ mặt bằng (mm) – vd. 50 sàn WC, 100 lô gia / sân phơi; 0 = không hạ:",
                                     slab.Drop > 1e-4 ? Dim(slab.Drop) : "50");
        if (s == null) return;
        double mm = DialogUi.ParseNum(s, 0);
        if (mm < 0 || mm > 1500) { _status.Text = "Độ hạ 0…1500 mm."; return; }
        if (pushUndo) _undo.Push(Plan);
        slab.Drop = Math.Round(mm / 1000, 4);
        _changed(); Redraw();
        _status.Text = slab.Drop > 0 ? $"Sàn {slab.Member} hạ cốt −{mm:0} mm (bản vẽ: biên nét đứt + \"SÀN HẠ COS -{mm:0}\")." : "Đã bỏ hạ cốt sàn " + slab.Member + ".";
    }
}

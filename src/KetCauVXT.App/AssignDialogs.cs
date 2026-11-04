using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Hộp thoại menu Gán (như Assign của ETABS): tải trên thanh, tải sàn, tải nút, liên kết nút, giải phóng đầu thanh,
/// tiết diện, hệ số độ cứng, sàn cứng theo tầng. Hộp thoại chỉ thu thập thông số; MainWindow.Workflow.cs thực hiện qua
/// <see cref="ModelAssign"/> (có hoàn tác).</summary>
internal static class AssignDialogs
{
    internal static readonly (LoadDir Dir, string Name)[] Dirs =
    {
        (LoadDir.Gravity, "Trọng lực (−Z, dương hướng xuống)"),
        (LoadDir.GlobalX, "Tổng thể X"), (LoadDir.GlobalY, "Tổng thể Y"), (LoadDir.GlobalZ, "Tổng thể Z (dương hướng lên)"),
        (LoadDir.Local1, "Địa phương 1 (dọc trục)"), (LoadDir.Local2, "Địa phương 2"), (LoadDir.Local3, "Địa phương 3")
    };

    internal static readonly (LoadAssignMode Mode, string Name)[] Modes =
    {
        (LoadAssignMode.Add, "Cộng thêm vào tải đã có"),
        (LoadAssignMode.Replace, "Thay thế tải nhập tay cùng trường hợp"),
        (LoadAssignMode.Delete, "Xoá tải nhập tay cùng trường hợp")
    };

    private static Window NewWindow(Window owner, string title, double w, double h)
    {
        var win = new Window { Owner = owner, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize };
        DialogUi.Setup(win, title, w, h);
        win.MinHeight = 0;
        return win;
    }

    private static ComboBox Combo(IEnumerable<string> items, string select, double width = 220)
    {
        var cb = new ComboBox { Width = width, ItemsSource = items.ToList(), Margin = new Thickness(0, 0, 6, 0) };
        cb.SelectedItem = select;
        if (cb.SelectedIndex < 0 && cb.Items.Count > 0) cb.SelectedIndex = 0;
        return cb;
    }

    private static TextBox Num(double v, double width = 70) => new() { Text = DialogUi.F(v), Width = width, Margin = new Thickness(0, 0, 6, 0), VerticalContentAlignment = VerticalAlignment.Center };

    private static StackPanel Field(string label, UIElement e, double labelWidth = 150) => DialogUi.Row(DialogUi.Label(label, labelWidth), e);

    /// <summary>Hàng nút Đồng ý / Huỷ; trả về panel.</summary>
    private static StackPanel OkCancel(Window w, Func<bool> onOk)
    {
        var ok = DialogUi.Btn("Đồng ý", (s, e) => { if (onOk()) w.DialogResult = true; }, true);
        ok.IsDefault = true;
        var cancel = DialogUi.Btn("Huỷ", (s, e) => w.DialogResult = false);
        cancel.IsCancel = true;
        var r = DialogUi.Row(ok, cancel);
        r.HorizontalAlignment = HorizontalAlignment.Right;
        r.Margin = new Thickness(0, 10, 0, 0);
        return r;
    }

    private static string DefaultPattern(Project p) =>
        p.Pattern("HTHT") != null ? "HTHT" : p.Patterns.FirstOrDefault()?.Name ?? "";

    // ------------------------------------------------------------------
    // Tải trên thanh
    // ------------------------------------------------------------------
    internal sealed class FrameLoadInput
    {
        public string Pattern = "";
        public int Kind;   // 0 đều, 1 hình thang, 2 tập trung
        public LoadDir Dir = LoadDir.Gravity;
        public bool Relative = true;
        public double Q, D1, Q1, D2 = 1, Q2, D, P;
        public LoadAssignMode Mode = LoadAssignMode.Add;
    }

    internal static FrameLoadInput AskFrameLoad(Window owner, Project p, int count)
    {
        var w = NewWindow(owner, $"Gán tải trên thanh – {count} thanh đang chọn", 520, 420);
        var cbPat = Combo(p.Patterns.Select(x => x.Name), DefaultPattern(p));
        var cbDir = Combo(Dirs.Select(d => d.Name), Dirs[0].Name, 260);
        var cbMode = Combo(Modes.Select(m => m.Name), Modes[0].Name, 260);
        var rbU = new RadioButton { Content = "Phân bố đều", IsChecked = true, GroupName = "fl", Margin = new Thickness(0, 0, 12, 0) };
        var rbT = new RadioButton { Content = "Hình thang / tam giác", GroupName = "fl", Margin = new Thickness(0, 0, 12, 0) };
        var rbP = new RadioButton { Content = "Lực tập trung", GroupName = "fl" };
        var cbDist = Combo(new[] { "Tương đối (0…1 chiều dài)", "Tuyệt đối (m từ đầu I)" }, "Tương đối (0…1 chiều dài)", 220);
        var tq = Num(5);
        var td1 = Num(0); var tq1 = Num(0); var td2 = Num(1); var tq2 = Num(5);
        var td = Num(0.5); var tp = Num(10);
        var pnU = Field("q (kN/m)", tq);
        var pnT = new StackPanel();
        pnT.Children.Add(DialogUi.Row(DialogUi.Label("Điểm 1: khoảng cách", 150), td1, DialogUi.Label("q1 (kN/m)"), tq1));
        pnT.Children.Add(DialogUi.Row(DialogUi.Label("Điểm 2: khoảng cách", 150), td2, DialogUi.Label("q2 (kN/m)"), tq2));
        var pnP = DialogUi.Row(DialogUi.Label("Khoảng cách", 150), td, DialogUi.Label("P (kN)"), tp);
        void Upd()
        {
            pnU.Visibility = rbU.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            pnT.Visibility = rbT.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            pnP.Visibility = rbP.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            cbDist.IsEnabled = rbU.IsChecked != true;
        }
        rbU.Checked += (s, e) => Upd(); rbT.Checked += (s, e) => Upd(); rbP.Checked += (s, e) => Upd();
        FrameLoadInput res = null;
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(Field("Trường hợp tải", cbPat));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Dạng tải", 150), rbU, rbT, rbP));
        sp.Children.Add(Field("Phương", cbDir));
        sp.Children.Add(Field("Khoảng cách tính theo", cbDist));
        sp.Children.Add(pnU); sp.Children.Add(pnT); sp.Children.Add(pnP);
        sp.Children.Add(Field("Cách gán", cbMode));
        sp.Children.Add(DialogUi.Hint("Như Assign → Frame Loads của ETABS. Trọng lực: giá trị dương hướng xuống. Phương địa phương: trục 1 dọc thanh I→J, trục 2 hướng lên (dầm), trục 3 = 1×2. "
                                      + "Tải nhập tay (nguồn \"Nhập tay\") – tải dồn từ sàn / tường được sinh lại mỗi lần phân tích."));
        sp.Children.Add(OkCancel(w, () =>
        {
            if (cbPat.SelectedItem is not string pat) { MessageBox.Show(w, "Chưa có trường hợp tải."); return false; }
            res = new FrameLoadInput
            {
                Pattern = pat, Kind = rbU.IsChecked == true ? 0 : rbT.IsChecked == true ? 1 : 2,
                Dir = Dirs[Math.Max(0, cbDir.SelectedIndex)].Dir, Mode = Modes[Math.Max(0, cbMode.SelectedIndex)].Mode,
                Relative = cbDist.SelectedIndex != 1,
                Q = DialogUi.ParseNum(tq.Text, 0), D1 = DialogUi.ParseNum(td1.Text, 0), Q1 = DialogUi.ParseNum(tq1.Text, 0),
                D2 = DialogUi.ParseNum(td2.Text, 1), Q2 = DialogUi.ParseNum(tq2.Text, 0), D = DialogUi.ParseNum(td.Text, 0.5), P = DialogUi.ParseNum(tp.Text, 0)
            };
            return true;
        }));
        w.Content = sp;
        Upd();
        return w.ShowDialog() == true ? res : null;
    }

    // ------------------------------------------------------------------
    // Tải trên sàn
    // ------------------------------------------------------------------
    internal sealed class AreaLoadInput
    {
        public string Pattern = "";
        public double Q;
        public int Scope;   // 0 ô đang chọn, 1 tầng đang xem, 2 mọi ô sàn
        public LoadAssignMode Mode = LoadAssignMode.Add;
    }

    internal static AreaLoadInput AskAreaLoad(Window owner, Project p, bool hasSelected, string story)
    {
        var w = NewWindow(owner, "Gán tải trên sàn (Assign → Shell Loads → Uniform)", 500, 300);
        var cbPat = Combo(p.Patterns.Select(x => x.Name), p.Pattern("HT") != null ? "HT" : DefaultPattern(p));
        var tq = Num(1.5);
        var cbScope = Combo(new[] { "Ô sàn đang chọn", $"Mọi ô sàn tầng đang xem ({story})", "Mọi ô sàn của công trình" }, null, 260);
        cbScope.SelectedIndex = hasSelected ? 0 : 1;
        var cbMode = Combo(Modes.Select(m => m.Name), Modes[0].Name, 260);
        AreaLoadInput res = null;
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(Field("Trường hợp tải", cbPat));
        sp.Children.Add(Field("q (kN/m², dương xuống)", tq));
        sp.Children.Add(Field("Áp dụng cho", cbScope));
        sp.Children.Add(Field("Cách gán", cbMode));
        sp.Children.Add(DialogUi.Hint("Tải sàn được dồn về dầm theo đường phân giác 45° khi phân tích (Loads/SlabLoadDistributor.cs)."));
        sp.Children.Add(OkCancel(w, () =>
        {
            if (cbPat.SelectedItem is not string pat) return false;
            if (cbScope.SelectedIndex == 0 && !hasSelected) { MessageBox.Show(w, "Chưa chọn ô sàn nào trên mặt bằng."); return false; }
            res = new AreaLoadInput { Pattern = pat, Q = DialogUi.ParseNum(tq.Text, 0), Scope = Math.Max(0, cbScope.SelectedIndex), Mode = Modes[Math.Max(0, cbMode.SelectedIndex)].Mode };
            return true;
        }));
        w.Content = sp;
        return w.ShowDialog() == true ? res : null;
    }

    // ------------------------------------------------------------------
    // Tải nút, liên kết nút
    // ------------------------------------------------------------------
    internal static List<int> ParseIds(string s) =>
        (s ?? "").Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                 .Select(t => int.TryParse(t, out int v) ? v : -1).Where(v => v > 0).Distinct().ToList();

    internal sealed class JointLoadInput
    {
        public string Pattern = "";
        public double[] V = new double[6];
        public List<int> Nodes = new();
        public LoadAssignMode Mode = LoadAssignMode.Add;
    }

    internal static JointLoadInput AskJointLoad(Window owner, Project p, IEnumerable<int> nodes)
    {
        var w = NewWindow(owner, "Gán tải tại nút (Assign → Joint Loads → Forces)", 560, 360);
        var cbPat = Combo(p.Patterns.Select(x => x.Name), DefaultPattern(p));
        var tNodes = new TextBox { Text = string.Join(", ", nodes ?? Enumerable.Empty<int>()), Width = 340 };
        string[] names = { "Fx (kN)", "Fy (kN)", "Fz (kN, + lên)", "Mx (kN.m)", "My (kN.m)", "Mz (kN.m)" };
        var boxes = names.Select(n => Num(0, 60)).ToArray();
        var cbMode = Combo(Modes.Select(m => m.Name), Modes[0].Name, 260);
        JointLoadInput res = null;
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(Field("Trường hợp tải", cbPat));
        sp.Children.Add(Field("Nút (Id, cách nhau dấu phẩy)", tNodes, 180));
        sp.Children.Add(DialogUi.Row(DialogUi.Label(names[0], 70), boxes[0], DialogUi.Label(names[1], 70), boxes[1], DialogUi.Label(names[2], 90), boxes[2]));
        sp.Children.Add(DialogUi.Row(DialogUi.Label(names[3], 70), boxes[3], DialogUi.Label(names[4], 70), boxes[4], DialogUi.Label(names[5], 90), boxes[5]));
        sp.Children.Add(Field("Cách gán", cbMode));
        sp.Children.Add(DialogUi.Hint("Hệ toạ độ tổng thể; Fz dương hướng lên (tải trọng lực nhập số âm). Nút gợi ý lấy từ thanh / chân cột đang chọn."));
        sp.Children.Add(OkCancel(w, () =>
        {
            var ids = ParseIds(tNodes.Text).Where(id => p.Nodes.Any(n => n.Id == id)).ToList();
            if (ids.Count == 0) { MessageBox.Show(w, "Chưa nhập nút hợp lệ."); return false; }
            if (cbPat.SelectedItem is not string pat) return false;
            res = new JointLoadInput { Pattern = pat, Nodes = ids, Mode = Modes[Math.Max(0, cbMode.SelectedIndex)].Mode, V = boxes.Select(b => DialogUi.ParseNum(b.Text, 0)).ToArray() };
            return true;
        }));
        w.Content = sp;
        return w.ShowDialog() == true ? res : null;
    }

    internal static (List<int> nodes, bool[] restraint)? AskRestraint(Window owner, Project p, IEnumerable<int> nodes)
    {
        var w = NewWindow(owner, "Gán liên kết nút (Assign → Joint → Restraints)", 540, 330);
        var tNodes = new TextBox { Text = string.Join(", ", nodes ?? Enumerable.Empty<int>()), Width = 320 };
        string[] n = { "UX", "UY", "UZ", "RX", "RY", "RZ" };
        var first = p.Nodes.FirstOrDefault(q => ParseIds(tNodes.Text).Contains(q.Id));
        var chk = n.Select((t, i) => new CheckBox { Content = t, Margin = new Thickness(0, 0, 10, 0), IsChecked = first?.Restraint[i] ?? true }).ToArray();
        void Set(bool[] r) { for (int i = 0; i < 6; i++) chk[i].IsChecked = r[i]; }
        var bases = p.Nodes.Where(q => Math.Abs(q.Z - (p.Nodes.Count > 0 ? p.Nodes.Min(z => z.Z) : 0)) < 1e-3).Select(q => q.Id).ToList();
        (List<int>, bool[])? res = null;
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(Field("Nút (Id, cách nhau dấu phẩy)", tNodes, 180));
        sp.Children.Add(DialogUi.Row(DialogUi.Btn("Mọi nút chân cột thấp nhất", (s, e) => tNodes.Text = string.Join(", ", bases))));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Giữ:", 50), chk[0], chk[1], chk[2], chk[3], chk[4], chk[5]));
        sp.Children.Add(DialogUi.Row(
            DialogUi.Btn("Ngàm", (s, e) => Set(Node.Fixed)),
            DialogUi.Btn("Khớp", (s, e) => Set(Node.Pinned)),
            DialogUi.Btn("Con lăn (UZ)", (s, e) => Set(new[] { false, false, true, false, false, false })),
            DialogUi.Btn("Tự do", (s, e) => Set(new bool[6]))));
        sp.Children.Add(DialogUi.Hint("Ngàm: giữ cả 6 bậc tự do; khớp: giữ UX, UY, UZ; tự do: bỏ liên kết. Gối thật (chân cột/móng) cần giữ UZ."));
        sp.Children.Add(OkCancel(w, () =>
        {
            var ids = ParseIds(tNodes.Text).Where(id => p.Nodes.Any(q => q.Id == id)).ToList();
            if (ids.Count == 0) { MessageBox.Show(w, "Chưa nhập nút hợp lệ."); return false; }
            res = (ids, chk.Select(c => c.IsChecked == true).ToArray());
            return true;
        }));
        w.Content = sp;
        return w.ShowDialog() == true ? res : null;
    }

    // ------------------------------------------------------------------
    // Giải phóng, tiết diện, hệ số độ cứng
    // ------------------------------------------------------------------
    internal static (bool[] relI, bool[] relJ)? AskReleases(Window owner, FrameElement first, int count)
    {
        var w = NewWindow(owner, $"Giải phóng đầu thanh – {count} thanh (Assign → Frame → Releases)", 560, 320);
        string[] n = { "P", "V2", "V3", "T", "M2", "M3" };
        var ci = n.Select((t, i) => new CheckBox { IsChecked = first?.ReleaseI?.ElementAtOrDefault(i) ?? false, Margin = new Thickness(0, 0, 22, 0) }).ToArray();
        var cj = n.Select((t, i) => new CheckBox { IsChecked = first?.ReleaseJ?.ElementAtOrDefault(i) ?? false, Margin = new Thickness(0, 0, 22, 0) }).ToArray();
        var head = DialogUi.Row(DialogUi.Label("", 70));
        foreach (var t in n) head.Children.Add(new TextBlock { Text = t, Width = 35, FontWeight = FontWeights.Bold });
        var rowI = DialogUi.Row(DialogUi.Label("Đầu I", 70)); foreach (var c in ci) rowI.Children.Add(c);
        var rowJ = DialogUi.Row(DialogUi.Label("Đầu J", 70)); foreach (var c in cj) rowJ.Children.Add(c);
        void Set(bool[] a, bool[] b) { for (int i = 0; i < 6; i++) { ci[i].IsChecked = a[i]; cj[i].IsChecked = b[i]; } }
        bool[] M = { false, false, false, false, true, true }, Z = new bool[6];
        (bool[], bool[])? res = null;
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(head); sp.Children.Add(rowI); sp.Children.Add(rowJ);
        sp.Children.Add(DialogUi.Row(
            DialogUi.Btn("Khớp hai đầu (M2, M3)", (s, e) => Set(M, M)),
            DialogUi.Btn("Khớp đầu I", (s, e) => Set(M, Z)),
            DialogUi.Btn("Khớp đầu J", (s, e) => Set(Z, M)),
            DialogUi.Btn("Không giải phóng", (s, e) => Set(Z, Z))));
        var msg = new TextBlock { Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap };
        sp.Children.Add(msg);
        sp.Children.Add(DialogUi.Hint("Giải phóng mô men đầu thanh (dầm gác khớp lên dầm chính, dầm phụ liên kết khớp). Phân tích khử tĩnh các bậc tự do được giải phóng "
                                      + "(Analysis/ElementMatrices.Condense). Không giải phóng P, V2, V3, T ở cả hai đầu (thanh biến hình)."));
        sp.Children.Add(OkCancel(w, () =>
        {
            var a = ci.Select(c => c.IsChecked == true).ToArray(); var b = cj.Select(c => c.IsChecked == true).ToArray();
            string err = ModelAssign.ValidateReleases(a, b);
            if (err != null) { msg.Text = err; return false; }
            res = (a, b);
            return true;
        }));
        w.Content = sp;
        return w.ShowDialog() == true ? res : null;
    }

    internal static string AskSection(Window owner, Project p, string current, int count)
    {
        var w = NewWindow(owner, $"Gán tiết diện – {count} thanh (Assign → Frame → Section)", 440, 200);
        var cb = Combo(p.FrameSections.Select(s => s.Name), current, 220);
        string res = null;
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(Field("Tiết diện", cb, 100));
        sp.Children.Add(DialogUi.Hint("Áp dụng cho mọi thanh đang chọn (dầm và cột). Khai báo tiết diện mới: Khai báo → Tiết diện dầm/cột…"));
        sp.Children.Add(OkCancel(w, () => { res = cb.SelectedItem as string; return res != null; }));
        w.Content = sp;
        return w.ShowDialog() == true ? res : null;
    }

    internal static double[] AskModifiers(Window owner, FrameElement first, int count)
    {
        var w = NewWindow(owner, $"Hệ số độ cứng thanh – {count} thanh (Frame Property Modifiers)", 460, 360);
        var boxes = ModelAssign.ModifierNames.Select((n, i) => Num(first?.StiffnessModifiers?.ElementAtOrDefault(i) is double d && d > 0 ? d : 1.0)).ToArray();
        double[] res = null;
        var sp = new StackPanel { Margin = new Thickness(10) };
        for (int i = 0; i < boxes.Length; i++) sp.Children.Add(Field(ModelAssign.ModifierNames[i], boxes[i], 160));
        sp.Children.Add(DialogUi.Row(
            DialogUi.Btn("Dầm nứt (I33 = 0,35)", (s, e) => { foreach (var b in boxes) b.Text = "1"; boxes[5].Text = "0.35"; boxes[4].Text = "0.35"; }),
            DialogUi.Btn("Cột nứt (I = 0,7)", (s, e) => { foreach (var b in boxes) b.Text = "1"; boxes[5].Text = "0.7"; boxes[4].Text = "0.7"; }),
            DialogUi.Btn("Đặt lại 1", (s, e) => { foreach (var b in boxes) b.Text = "1"; })));
        sp.Children.Add(DialogUi.Hint("Nhân thêm với hệ số của tiết diện (Khai báo → Tiết diện). Toàn bộ = 1 → bỏ gán. Chỉ ảnh hưởng độ cứng khi phân tích, không ảnh hưởng thiết kế."));
        sp.Children.Add(OkCancel(w, () => { res = boxes.Select(b => Math.Max(1e-4, DialogUi.ParseNum(b.Text, 1))).ToArray(); return true; }));
        w.Content = sp;
        return w.ShowDialog() == true ? res : null;
    }

    /// <summary>Bật/tắt sàn cứng cho từng tầng. Trả về (dùng sàn cứng chung, tầng tắt).</summary>
    internal static (bool on, List<string> off)? AskDiaphragms(Window owner, Project p, string focusStory)
    {
        var w = NewWindow(owner, "Sàn cứng (diaphragm) theo tầng", 460, 420);
        var master = new CheckBox { Content = "Dùng sàn tuyệt đối cứng trong mặt phẳng (chung cho phân tích tĩnh và dao động)", IsChecked = p.UseRigidDiaphragm, Margin = new Thickness(0, 2, 0, 6) };
        var list = new StackPanel();
        var checks = new List<(string name, CheckBox cb)>();
        foreach (var s in p.Stories.OrderByDescending(s => s.Elevation))
        {
            int slabs = p.Slabs.Count(x => x.Story == s.Name);
            var cb = new CheckBox
            {
                Content = $"{s.Name} (+{s.Elevation:0.000}) – {slabs} ô sàn", IsChecked = !(p.DiaphragmOff?.Contains(s.Name) ?? false), Margin = new Thickness(12, 1, 0, 1)
            };
            if (s.Name == focusStory) cb.FontWeight = FontWeights.Bold;
            checks.Add((s.Name, cb));
            list.Children.Add(cb);
        }
        master.Checked += (s, e) => list.IsEnabled = true;
        master.Unchecked += (s, e) => list.IsEnabled = false;
        list.IsEnabled = master.IsChecked == true;
        (bool, List<string>)? res = null;
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(master);
        sp.Children.Add(new ScrollViewer { Content = list, MaxHeight = 240, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        sp.Children.Add(DialogUi.Hint("Tầng được đánh dấu: các nút sàn cùng cao trình chuyển vị ngang và xoay quanh Z như một khối (nút chủ tại trọng tâm). "
                                      + "Bỏ đánh dấu cho tầng sàn có lỗ lớn, sàn lệch cốt hoặc mái tôn."));
        sp.Children.Add(OkCancel(w, () => { res = (master.IsChecked == true, checks.Where(c => c.cb.IsChecked != true).Select(c => c.name).ToList()); return true; }));
        w.Content = sp;
        return w.ShowDialog() == true ? res : null;
    }
}

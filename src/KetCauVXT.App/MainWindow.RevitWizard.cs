using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using KetCauVXT.Design;
using KetCauVXT.Import;
using KetCauVXT.Import.Arch;
using KetCauVXT.Loads;
using KetCauVXT.Model;
using WorkflowAction = KetCauVXT.Docs.WorkflowAction;

namespace KetCauVXT.App;

// =====================================================================
// TỪ MÔ HÌNH REVIT ĐẾN THIẾT KẾ – TỪNG BƯỚC, MỖI BƯỚC NGƯỜI DÙNG XÁC NHẬN
//   1 Đọc file Revit (kiến trúc)        → RevitArchReader.ToDrawing  (có cột/dầm kết cấu → nhập trực tiếp RevitJsonImporter)
//   2 Bảng nhận diện tải trọng          → ArchLoadTakeoff (phòng → công năng, loại tường → kN/m², mái, bồn nước); mục chưa chắc tô vàng
//   3 Bố trí kết cấu đề xuất            → ArchToStructure.Propose → Apply (xem mặt bằng ở cửa sổ chính trước khi xác nhận)
//   4 Tổ hợp tải trọng                  → ComboGenerator (TCVN 2737:2023) tự tạo, người dùng xác nhận / sửa
//   5 Kiểm tra mô hình                  → ModelCheck, sửa tự động; hết lỗi mới đi tiếp
//   6 Phân tích + xem kết quả           → FrameAnalysis; xác nhận đã kiểm tra kết quả
//   7 Tối ưu tiết diện theo chi phí     → SectionOptimizer (giữ bề rộng theo tường); chấp nhận hoặc quay lại
//   8 Thiết kế, xuất hồ sơ
// Cửa sổ không chặn: người dùng xem mặt bằng, 3D, biểu đồ ở cửa sổ chính giữa các bước.
// =====================================================================
public partial class MainWindow
{
    private Window _wzWin;

    private void MnuRevitWizard_Click(object sender, RoutedEventArgs e) => OpenRevitWizard();

    /// <summary>Xuất chuẩn (cao độ, trục, vật liệu, loại tường / cột / dầm / sàn, khung tên, nét in) + cột, dầm, sàn sang Revit:
    /// add-in Việt Const trong Revit dùng file này để chuẩn hoá, dựng kết cấu và nhận ngược tiết diện (Import/RevitSyncExport.cs).</summary>
    private void MnuExportToRevit_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null || !PassGate(WorkflowAction.ExportModel)) return;
        if (_p.RevitLink == null && MessageBox.Show(this,
                "Mô hình này không nhập từ Revit nên chưa biết gốc toạ độ Revit: cấu kiện sẽ đặt từ gốc toạ độ nội bộ (0; 0) của Revit, tầng 1 tại cao độ 0.\nTiếp tục?",
                "Xuất sang Revit", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        var dlg = new SaveFileDialog
        {
            Title = "Xuất chuẩn + kết cấu sang Revit", Filter = "File Việt Const → Revit (*.vxt2revit.json)|*.vxt2revit.json",
            FileName = SafeFile(_p.Name) + ".vxt2revit.json",
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            System.IO.File.WriteAllText(dlg.FileName, RevitSyncExport.ToJson(_p), new System.Text.UTF8Encoding(false));
            int nc = _p.Frames.Count(f => f.Kind == FrameKind.Column), nb = _p.Frames.Count(f => f.Kind != FrameKind.Column);
            SetStatus($"Đã xuất {nc} cột, {nb} dầm, {_p.Slabs.Count} ô sàn + chuẩn sang {System.IO.Path.GetFileName(dlg.FileName)}. Trong Revit: tab Việt Const → Chuẩn hoá / Dựng kết cấu / Nhận tiết diện.");
        }
        catch (Exception ex) { ShowError("Xuất sang Revit", ex); }
    }

    private static string SafeFile(string s)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars()) s = (s ?? "").Replace(c, '_');
        return string.IsNullOrWhiteSpace(s) ? "VietConst" : s.Trim();
    }

    private void OpenRevitWizard()
    {
        if (_wzWin != null && _wzWin.IsLoaded) { _wzWin.Activate(); return; }
        var wz = new RevitWizard(this);
        _wzWin = wz.Window;
        _wzWin.Closed += (s, a) => _wzWin = null;
        _wzWin.Show();
    }

    /// <summary>Chữ ký bộ tổ hợp hiện tại (để biết người dùng đã xác nhận đúng bộ tổ hợp đang dùng).</summary>
    private string ComboSignature() => _p == null ? "" :
        string.Join("|", _p.Combinations.Select(c => c.Name + ":" + string.Join(",", c.Items.Select(i => i.Case + "*" + i.Factor.ToString("0.###", CultureInfo.InvariantCulture)))));

    private string _confirmedComboSig;

    /// <summary>Trước khi phân tích: thiếu tổ hợp → tự tạo theo TCVN 2737:2023; tổ hợp chưa được xác nhận → hỏi người dùng.</summary>
    private bool EnsureCombosConfirmed()
    {
        if (_p == null || IsSelfTestRun) return true;
        if (_p.Patterns.Count > 0 && _p.Combinations.Count == 0)
        {
            ComboGenerator.Generate(_p);
            MarkDirty();
            RefreshLoads(); RefreshCaseCombos();
        }
        if (_p.Combinations.Count == 0) return true;   // không có trường hợp tải: bộ khoá bước báo
        string sig = ComboSignature();
        if (sig == _confirmedComboSig) return true;
        var list = string.Join("\n", _p.Combinations.Take(12).Select(c => "• " + c.Name + " = " + string.Join(" + ", c.Items.Select(i => i.Factor.ToString("0.##", CultureInfo.InvariantCulture) + "×" + i.Case))))
                   + (_p.Combinations.Count > 12 ? $"\n… (còn {_p.Combinations.Count - 12} tổ hợp)" : "");
        var r = MessageBox.Show(this,
            $"Phần mềm dùng {_p.Combinations.Count} tổ hợp tải trọng (tự tạo theo TCVN 2737:2023):\n\n{list}\n\n"
            + "Có: xác nhận dùng các tổ hợp này và chạy phân tích.\nKhông: mở bảng Tổ hợp tải trọng để xem / sửa.\nHuỷ: không chạy.",
            "Xác nhận tổ hợp tải trọng", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (r == MessageBoxResult.Yes) { _confirmedComboSig = sig; return true; }
        if (r == MessageBoxResult.No) OpenComboEditor();
        return false;
    }

    // =================================================================
    internal sealed class RoomRowVm
    {
        public RoomLoadRow Row;
        public ArchLoadTakeoff T;
        public string Name => Row.Name;
        public int Count => Row.Count;
        public string Plans => Row.Plans;
        public bool Sure => Row.Sure;
        public string Kind
        {
            get => T.Catalog.FirstOrDefault(c => c.Kind == Row.Kind)?.Name ?? Row.Kind.ToString();
            set
            {
                var c = T.Catalog.FirstOrDefault(q => q.Name == value);
                if (c == null) return;
                Row.Kind = c.Kind; Row.KindName = c.Name; Row.Sure = true;
            }
        }
        public string Loads
        {
            get
            {
                var c = T.Catalog.FirstOrDefault(q => q.Kind == Row.Kind);
                return c == null ? "" : c.NoSlab ? "không có sàn" : $"{c.Finish:0.##} + {c.Live:0.##}";
            }
        }
    }

    internal sealed class CatalogRowVm
    {
        public RoomType R;
        public string Name => R.Name;
        public double Finish { get => R.Finish; set => R.Finish = Math.Max(0, value); }
        public double Live { get => R.Live; set => R.Live = Math.Max(0, value); }
    }

    internal sealed class WallRowVm
    {
        public WallLoadRow Row;
        public string TypeName => Row.TypeName;
        public double ThicknessMm => Math.Round(Row.Thickness * 1000);
        public double PerArea { get => Row.PerArea; set { Row.PerArea = Math.Max(0, value); Row.Sure = true; } }
        public string Core => Row.Core;
        public int Count => Row.Count;
        public double Length => Row.Length;
        public string Note => Row.Note;
        public bool Sure => Row.Sure;
    }

    internal sealed class EquipRowVm
    {
        public EquipmentLoadRow Row;
        public bool Include { get => Row.Include; set => Row.Include = value; }
        public string Name => Row.Name;
        public double Load { get => Row.Load; set { Row.Load = Math.Max(0, value); Row.Sure = true; } }
        public string Note => Row.Note;
        public bool Sure => Row.Sure;
    }

    // =================================================================
    /// <summary>Cửa sổ từng bước. Giữ trạng thái giữa các bước; mỗi bước có nút "Xác nhận" chuyển sang bước sau.</summary>
    private sealed class RevitWizard
    {
        private readonly MainWindow _m;
        public readonly Window Window;
        private readonly StackPanel _steps = new() { Margin = new Thickness(8), Width = 230 };
        private readonly ContentControl _body = new() { Margin = new Thickness(8) };
        private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 2, 8, 2), Foreground = Brushes.DimGray };
        private readonly Button _btnNext;
        private int _step;
        private int _maxDone = -1;

        private RevitExchange _ex;
        private string _file;
        private ArchDrawing _drawing;
        private ArchImportOptions _opt;
        private ArchLoadTakeoff _loads;
        private StructuralProposal _prop;
        private bool _applied;
        private bool _directStructure;
        private string _beforeOptimize;
        private SectionOptimizer.Result _optRes;
        private Func<bool> _confirm;   // việc phải làm khi bấm Xác nhận ở bước hiện tại (false = chưa đi tiếp được)

        private static readonly string[] Titles =
        {
            "1. Đọc mô hình Revit", "2. Nhận diện tải trọng", "3. Bố trí kết cấu", "4. Tổ hợp tải trọng",
            "5. Kiểm tra mô hình", "6. Phân tích, xem kết quả", "7. Tối ưu tiết diện", "8. Thiết kế, xuất hồ sơ",
        };

        public RevitWizard(MainWindow m)
        {
            _m = m;
            Window = new Window();
            DialogUi.Setup(Window, "Từ mô hình Revit đến thiết kế – Việt Const", 1000, 680);
            Window.Owner = m;
            Window.ShowInTaskbar = true;
            Window.ResizeMode = ResizeMode.CanResizeWithGrip;
            Window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            _btnNext = DialogUi.Btn("Xác nhận, sang bước tiếp ➜", (s, e) => Next(), bold: true);
            var btnBack = DialogUi.Btn("◀ Bước trước", (s, e) => { if (_step > 0) Show(_step - 1); });
            var btnClose = DialogUi.Btn("Đóng", (s, e) => Window.Close());
            var bottom = new DockPanel { Margin = new Thickness(8, 4, 8, 8), LastChildFill = true };
            var btns = DialogUi.Row(btnBack, _btnNext, btnClose);
            DockPanel.SetDock(btns, Dock.Right); bottom.Children.Add(btns);
            bottom.Children.Add(_status);
            var left = new Border { Child = _steps, BorderBrush = new SolidColorBrush(Color.FromRgb(220, 224, 230)), BorderThickness = new Thickness(0, 0, 1, 0), Background = new SolidColorBrush(Color.FromRgb(248, 249, 251)) };
            var root = new DockPanel();
            DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
            DockPanel.SetDock(left, Dock.Left); root.Children.Add(left);
            root.Children.Add(new ScrollViewer { Content = _body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            Window.Content = root;
            Show(0);
        }

        private void Status(string s) { _status.Text = s; _m.SetStatus(s); }

        private void DrawSteps()
        {
            _steps.Children.Clear();
            _steps.Children.Add(new TextBlock { Text = "Các bước", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) });
            for (int i = 0; i < Titles.Length; i++)
            {
                bool done = i <= _maxDone, cur = i == _step;
                var t = new TextBlock
                {
                    Text = (done ? "✓ " : cur ? "➜ " : "○ ") + Titles[i], Margin = new Thickness(0, 3, 0, 3), TextWrapping = TextWrapping.Wrap,
                    FontWeight = cur ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = done ? new SolidColorBrush(Color.FromRgb(30, 130, 60)) : cur ? Brushes.Black : Brushes.Gray,
                };
                _steps.Children.Add(t);
            }
            _steps.Children.Add(DialogUi.Hint("Mỗi bước phần mềm tự làm, bạn kiểm tra rồi bấm Xác nhận. Cửa sổ này không chặn – xem mặt bằng, 3D, biểu đồ ở cửa sổ chính."));
        }

        private void Next()
        {
            bool ok;
            try { ok = _confirm?.Invoke() ?? true; }
            catch (Exception ex) { MessageBox.Show(Window, ex.Message, Window.Title, MessageBoxButton.OK, MessageBoxImage.Error); return; }
            if (!ok) return;
            _maxDone = Math.Max(_maxDone, _step);
            if (_step + 1 < Titles.Length) Show(_step + 1);
            else { Status("Đã xong các bước. Có thể xuất hồ sơ bất cứ lúc nào (Xuất → Xuất tất cả vào thư mục…)."); DrawSteps(); }
        }

        private void Show(int step)
        {
            // không nhảy qua bước chưa xác nhận
            if (step > _maxDone + 1) step = _maxDone + 1;
            _step = step;
            _confirm = null;
            _btnNext.IsEnabled = true;
            _btnNext.Content = step + 1 < Titles.Length ? "Xác nhận, sang bước tiếp ➜" : "Xác nhận, hoàn thành";
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = Titles[step], FontSize = 17, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) });
            try
            {
                switch (step)
                {
                    case 0: StepRead(sp); break;
                    case 1: StepLoads(sp); break;
                    case 2: StepLayout(sp); break;
                    case 3: StepCombos(sp); break;
                    case 4: StepCheck(sp); break;
                    case 5: StepAnalysis(sp); break;
                    case 6: StepOptimize(sp); break;
                    default: StepDesign(sp); break;
                }
            }
            catch (Exception ex) { sp.Children.Add(Err("Lỗi: " + ex.Message)); }
            _body.Content = sp;
            DrawSteps();
        }

        private static TextBlock Para(string s, bool bold = false) => new() { Text = s, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal };
        private static TextBlock Err(string s) => new() { Text = s, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4), Foreground = Brushes.DarkRed, FontWeight = FontWeights.SemiBold };
        private static TextBox Box(string s, double h = 160) => new() { Text = s, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = h, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0, 4, 0, 4) };

        private static void Yellow(DataGrid g, Func<object, bool> sure) =>
            g.LoadingRow += (s, e) => e.Row.Background = sure(e.Row.Item) ? Brushes.White : new SolidColorBrush(Color.FromRgb(255, 244, 200));

        // ------------------------------------------------------------- 1
        private void StepRead(StackPanel sp)
        {
            sp.Children.Add(Para("Trong Revit: Add-Ins → Việt Const → Xuất mô hình (file *.vxtrevit.json). Mô hình chỉ cần kiến trúc: tường (có lớp cấu tạo), cửa, phòng (Room đặt tên), cao độ, lưới trục. "
                                 + "Có sẵn cột / dầm kết cấu thì phần mềm nhập trực tiếp."));
            var info = Box(_drawing == null ? "(chưa chọn file)" : Summary(), 260);
            var pick = DialogUi.Btn("Chọn file Revit…", (s, e) =>
            {
                var dlg = new OpenFileDialog { Filter = "File trao đổi Revit (*.vxtrevit.json;*.json)|*.vxtrevit.json;*.json|Tất cả (*.*)|*.*", Title = "Chọn file xuất từ add-in Revit" };
                if (dlg.ShowDialog(Window) != true) return;
                try
                {
                    _file = dlg.FileName;
                    _ex = RevitJsonImporter.ReadFile(_file);
                    _directStructure = RevitArchReader.HasStructure(_ex);
                    _opt = NewOptions();
                    _drawing = _directStructure ? null : RevitArchReader.ToDrawing(_ex, _opt);
                    _loads = null; _prop = null; _applied = false; _maxDone = -1;
                    info.Text = Summary();
                    Status(_directStructure ? "Mô hình Revit có cột / dầm kết cấu – sẽ nhập trực tiếp." : "Đã đọc kiến trúc Revit – kiểm tra rồi bấm Xác nhận.");
                    DrawSteps();
                }
                catch (Exception ex) { info.Text = "Không đọc được file: " + ex.Message; _drawing = null; _ex = null; }
            }, bold: true);
            sp.Children.Add(DialogUi.Row(pick));
            sp.Children.Add(info);
            _confirm = () =>
            {
                if (_ex == null) { Warn("Chọn file Revit trước."); return false; }
                if (_directStructure)
                {
                    if (!AskReplace()) return false;
                    var rep = RevitJsonImporter.Build(_m._p, _ex, new RevitImportOptions { Concrete = _m._p.Concrete.ConcreteBeam });
                    AfterModelReplaced();
                    MessageBox.Show(Window, rep.ToString(), "Nhập mô hình Revit có kết cấu", MessageBoxButton.OK, MessageBoxImage.Information);
                    _maxDone = 2;   // bỏ qua bước nhận diện tải / bố trí: mô hình đã có kết cấu
                    Show(3);
                    return false;
                }
                if (_drawing == null || !_drawing.Floors.Any(f => !f.IsRoofPlan && f.Walls.Count > 0)) { Warn("Không nhận diện được mặt bằng tầng nào có tường."); return false; }
                return true;
            };
        }

        private string Summary()
        {
            var lines = new List<string> { "File: " + _file };
            if (_ex != null) lines.Add($"Revit: {_ex.Levels?.Count ?? 0} cao độ, {_ex.Grids?.Count ?? 0} trục, {_ex.Walls?.Count ?? 0} tường, {_ex.Openings?.Count ?? 0} cửa, {_ex.Rooms?.Count ?? 0} phòng, {_ex.Columns?.Count ?? 0} cột KC, {_ex.Beams?.Count ?? 0} dầm KC, {_ex.Mep?.Count ?? 0} đối tượng điện nước");
            if (_directStructure) lines.Add("→ Có cấu kiện kết cấu: nhập trực tiếp cột, dầm, sàn; tải phòng / tường theo kiến trúc đi kèm.");
            if (_drawing != null)
            {
                lines.AddRange(_drawing.Info);
                foreach (var f in _drawing.Floors) foreach (var n in f.Notes) lines.Add("  · " + f.Title + ": " + n);
                foreach (var w in _drawing.Warnings) lines.Add("⚠ " + w);
            }
            return string.Join(Environment.NewLine, lines);
        }

        private ArchImportOptions NewOptions()
        {
            var o = new ArchImportOptions { Concrete = _m._p?.Concrete?.ConcreteBeam ?? "B25" };
            if (_m._p != null) { o.BaseElevation = _m._p.BaseElevation < 0 ? _m._p.BaseElevation : o.BaseElevation; }
            return o;
        }

        // ------------------------------------------------------------- 2
        private void StepLoads(StackPanel sp)
        {
            _loads ??= ArchLoadTakeoff.From(_drawing, _opt, _ex);
            var t = _loads;
            sp.Children.Add(Para($"Phần mềm đã nhận diện tải từ mô hình kiến trúc. Dòng tô vàng ({t.UncertainCount} mục) là chỗ phần mềm chưa chắc – sửa nếu cần. Tải: kN/m², hoạt tải tiêu chuẩn theo TCVN 2737:2023."));
            foreach (var n in t.Notes) sp.Children.Add(Err("⚠ " + n));

            sp.Children.Add(Para("Phòng → công năng (chọn lại ở cột Công năng):", true));
            var names = t.Catalog.Select(c => c.Name).ToList();
            var gR = DialogUi.NewGrid(); gR.MaxHeight = 220;
            gR.Columns.Add(DialogUi.Text("Tên phòng", "Name", true, 170));
            gR.Columns.Add(DialogUi.Num("Số phòng", "Count", "0", true, 70));
            gR.Columns.Add(DialogUi.Text("Tầng", "Plans", true, 110));
            gR.Columns.Add(DialogUi.Combo("Công năng", "Kind", names, 170));
            gR.Columns.Add(DialogUi.Text("Hoàn thiện + hoạt tải", "Loads", true, 150));
            Yellow(gR, o => ((RoomRowVm)o).Sure);
            gR.ItemsSource = t.Rooms.Select(r => new RoomRowVm { Row = r, T = t }).ToList();
            sp.Children.Add(gR);

            sp.Children.Add(Para("Tải theo công năng (sửa được):", true));
            var gC = DialogUi.NewGrid(); gC.MaxHeight = 200;
            gC.Columns.Add(DialogUi.Text("Công năng", "Name", true, 190));
            gC.Columns.Add(DialogUi.Num("Hoàn thiện (kN/m²)", "Finish", "0.##", false, 130));
            gC.Columns.Add(DialogUi.Num("Hoạt tải (kN/m²)", "Live", "0.##", false, 120));
            gC.ItemsSource = t.Catalog.Where(c => !c.NoSlab).Select(c => new CatalogRowVm { R = c }).ToList();
            sp.Children.Add(gC);

            sp.Children.Add(Para("Loại tường → trọng lượng 1 m² (theo lớp cấu tạo; tải tường lên dầm trừ lỗ cửa):", true));
            var gW = DialogUi.NewGrid(); gW.MaxHeight = 220;
            gW.Columns.Add(DialogUi.Text("Loại tường", "TypeName", true, 190));
            gW.Columns.Add(DialogUi.Num("Dày (mm)", "ThicknessMm", "0", true, 70));
            gW.Columns.Add(DialogUi.Num("kN/m²", "PerArea", "0.00", false, 70));
            gW.Columns.Add(DialogUi.Text("Vật liệu chính", "Core", true, 150));
            gW.Columns.Add(DialogUi.Num("Số đoạn", "Count", "0", true, 60));
            gW.Columns.Add(DialogUi.Num("Dài (m)", "Length", "0.0", true, 70));
            gW.Columns.Add(DialogUi.Text("Ghi chú", "Note", true, 260));
            Yellow(gW, o => ((WallRowVm)o).Sure);
            gW.ItemsSource = t.Walls.Select(w => new WallRowVm { Row = w }).ToList();
            sp.Children.Add(gW);

            if (t.Equipment.Count > 0)
            {
                sp.Children.Add(Para("Thiết bị nặng (tải tập trung, trường hợp HTHT):", true));
                var gE = DialogUi.NewGrid(); gE.MaxHeight = 140;
                gE.Columns.Add(new DataGridCheckBoxColumn { Header = "Dùng", Binding = new System.Windows.Data.Binding("Include") { Mode = System.Windows.Data.BindingMode.TwoWay, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged } });
                gE.Columns.Add(DialogUi.Text("Thiết bị", "Name", true, 220));
                gE.Columns.Add(DialogUi.Num("Tải (kN)", "Load", "0.0", false, 80));
                gE.Columns.Add(DialogUi.Text("Cách tính", "Note", true, 320));
                Yellow(gE, o => ((EquipRowVm)o).Sure);
                gE.ItemsSource = t.Equipment.Select(q => new EquipRowVm { Row = q }).ToList();
                sp.Children.Add(gE);
            }

            var tRoofF = new TextBox { Width = 70, Text = t.RoofFinish.ToString("0.##", CultureInfo.InvariantCulture) };
            var tRoofL = new TextBox { Width = 70, Text = t.RoofLive.ToString("0.##", CultureInfo.InvariantCulture) };
            var tPar = new TextBox { Width = 70, Text = t.ParapetHeight.ToString("0.##", CultureInfo.InvariantCulture) };
            sp.Children.Add(DialogUi.Row(DialogUi.Label("Mái BTCT: hoàn thiện (kN/m²)", 210), tRoofF, DialogUi.Label("  hoạt tải", 70), tRoofL, DialogUi.Label("  tường chắn mái cao (m)", 170), tPar));
            sp.Children.Add(DialogUi.Hint("Gió, động đất: theo địa điểm ở tab Công trình (hỏi lại sau khi lập mô hình)."));

            _confirm = () =>
            {
                foreach (var g in new[] { gR, gC, gW }) DialogUi.Commit(g);
                t.RoofFinish = DialogUi.ParseNum(tRoofF.Text, t.RoofFinish);
                t.RoofLive = DialogUi.ParseNum(tRoofL.Text, t.RoofLive);
                t.ParapetHeight = DialogUi.ParseNum(tPar.Text, t.ParapetHeight);
                int unsure = t.UncertainCount;
                if (unsure > 0 && MessageBox.Show(Window, $"Còn {unsure} mục tô vàng (phần mềm chưa chắc). Vẫn dùng giá trị hiện tại?", Window.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return false;
                t.Apply(_drawing, _opt);
                _prop = null;
                return true;
            };
        }

        // ------------------------------------------------------------- 3
        private void StepLayout(StackPanel sp)
        {
            sp.Children.Add(Para("Phần mềm đặt cột tại giao trục trong nhà, dầm trên trục và dưới tường, chia ô sàn, chọn tiết diện sơ bộ (bề rộng dầm, cột theo tường). "
                                 + "Bấm \"Lập mô hình\" rồi xem mặt bằng kết cấu ở cửa sổ chính trước khi xác nhận."));
            var box = Box(_prop == null ? "(chưa lập)" : ProposalText(), 300);
            var btn = DialogUi.Btn(_applied ? "Lập lại mô hình" : "Lập mô hình kết cấu", (s, e) =>
            {
                try
                {
                    _prop = ArchToStructure.Propose(_drawing, _opt);
                    box.Text = ProposalText();
                    if (!AskReplace()) return;
                    var rep = ArchToStructure.Apply(_m._p, _drawing, _prop, _opt);
                    _m._p.RevitLink = new RevitLink { OriginX = _drawing.OriginX, OriginY = _drawing.OriginY, GroundZ = _drawing.GroundZ, SourceFile = _file ?? "" };
                    int ne = _loads?.ApplyEquipment(_m._p) ?? 0;
                    AfterModelReplaced();
                    _applied = true;
                    box.Text = ProposalText() + Environment.NewLine + Environment.NewLine + "ĐÃ LẬP MÔ HÌNH:" + Environment.NewLine + rep
                               + (ne > 0 ? Environment.NewLine + $"Tải thiết bị: {ne} điểm (HTHT)." : "");
                    Status($"Đã lập mô hình: {rep.Columns} cột, {rep.Beams} dầm, {rep.Slabs} ô sàn – xem mặt bằng ở cửa sổ chính.");
                    _m.SelectView("Mặt bằng");
                    _m.AskSiteData();   // gió, địa chất
                }
                catch (Exception ex) { box.Text = "Lỗi lập mô hình: " + ex.Message; }
            }, bold: true);
            sp.Children.Add(DialogUi.Row(btn, DialogUi.Btn("Xem 3D", (s, e) => _m.SelectView("3D"))));
            sp.Children.Add(box);
            sp.Children.Add(DialogUi.Hint("Cần sửa: sửa trực tiếp trên mặt bằng ở cửa sổ chính (thêm / xoá cột, dầm, ô sàn, cầu thang) rồi quay lại xác nhận."));
            _confirm = () =>
            {
                if (!_applied) { Warn("Bấm \"Lập mô hình kết cấu\" trước."); return false; }
                return MessageBox.Show(Window, "Bạn đã xem mặt bằng kết cấu ở cửa sổ chính và đồng ý với cách bố trí?", Window.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            };
        }

        private string ProposalText()
        {
            var s = _prop;
            var l = new List<string> { $"Lưới trục: {s.GridX.Count} trục X, {s.GridY.Count} trục Y; {s.Columns.Count(c => c.Include)} cột; {s.Levels.Count} cao trình." };
            foreach (var lv in s.Levels)
                l.Add($"  {lv.Name} (+{lv.Elevation:0.000}): {lv.Beams.Count(b => b.Include)} dầm, {lv.Slabs.Count(x => x.Include)} ô sàn, {lv.Holes.Count} lỗ sàn"
                      + (lv.Slabs.Count > 0 ? $", sàn dày {lv.Slabs.Max(x => x.Thickness) * 1000:0} mm" : ""));
            foreach (var w in s.Warnings) l.Add("⚠ " + w);
            foreach (var n in s.Notes) l.Add("· " + n);
            return string.Join(Environment.NewLine, l);
        }

        // ------------------------------------------------------------- 4
        private void StepCombos(StackPanel sp)
        {
            var p = _m._p;
            if (p.Patterns.Count > 0 && p.Combinations.Count == 0) { ComboGenerator.Generate(p); _m.MarkDirty(); _m.RefreshLoads(); _m.RefreshCaseCombos(); }
            sp.Children.Add(Para($"Phần mềm tự tạo {p.Combinations.Count} tổ hợp tải trọng theo TCVN 2737:2023 từ {p.Patterns.Count} trường hợp tải: "
                                 + string.Join(", ", p.Patterns.Select(x => x.Name)) + "."));
            sp.Children.Add(Box(string.Join(Environment.NewLine, p.Combinations.Select(c => $"{c.Name,-10} {c.Type}: " + string.Join(" + ", c.Items.Select(i => i.Factor.ToString("0.##", CultureInfo.InvariantCulture) + "×" + i.Case)))), 320));
            sp.Children.Add(DialogUi.Row(
                DialogUi.Btn("Tạo lại theo TCVN", (s, e) => { ComboGenerator.Generate(p); _m.MarkDirty(); _m.RefreshLoads(); _m.RefreshCaseCombos(); Show(3); }),
                DialogUi.Btn("Xem / sửa tổ hợp…", (s, e) => _m.OpenComboEditor())));
            _confirm = () =>
            {
                if (p.Combinations.Count == 0) { Warn("Chưa có tổ hợp nào (mô hình không có trường hợp tải)."); return false; }
                _m._confirmedComboSig = _m.ComboSignature();
                return true;
            };
        }

        // ------------------------------------------------------------- 5
        private void StepCheck(StackPanel sp)
        {
            var p = _m._p;
            var rep = ModelCheck.Run(p);
            sp.Children.Add(Para(rep.Summary, true));
            sp.Children.Add(Box(rep.Issues.Count == 0 ? "Không có vấn đề." : string.Join(Environment.NewLine, rep.Issues.OrderBy(i => i.Severity).Take(200).Select(i => i.ToString())), 300));
            sp.Children.Add(DialogUi.Row(
                DialogUi.Btn("Sửa tự động các mục sửa được", (s, e) =>
                {
                    string snap = _m.Snapshot();
                    var fx = ModelCheck.Fix(p);
                    if (fx.Total > 0) { _m.PushUndo(snap); _m.MarkDirty(); _m.RefreshAll(false); }
                    Status("Sửa tự động: " + fx);
                    Show(4);
                }, bold: rep.Fixable > 0),
                DialogUi.Btn("Mở bảng Kiểm tra mô hình…", (s, e) => _m.OpenModelCheck()),
                DialogUi.Btn("Kiểm tra lại", (s, e) => Show(4))));
            _confirm = () =>
            {
                var r2 = ModelCheck.Run(p);
                if (r2.Errors > 0) { Warn($"Mô hình còn {r2.Errors} lỗi – sửa hết lỗi mới phân tích được."); Show(4); return false; }
                return true;
            };
        }

        // ------------------------------------------------------------- 6
        private void StepAnalysis(StackPanel sp)
        {
            var box = Box(ResultText(), 260);
            sp.Children.Add(Para("Chạy phân tích nội lực (tĩnh, gió, động đất theo khai báo). Sau đó xem biểu đồ nội lực, phản lực, chuyển vị ở cửa sổ chính."));
            sp.Children.Add(DialogUi.Row(
                DialogUi.Btn("Chạy phân tích", async (s, e) =>
                {
                    _m._confirmedComboSig = _m.ComboSignature();   // đã xác nhận ở bước 4
                    if (await _m.RunAnalysis()) { box.Text = ResultText(); Status("Phân tích xong – xem kết quả ở cửa sổ chính."); }
                }, bold: true),
                DialogUi.Btn("Biểu đồ nội lực", (s, e) => { _m.SelectView("Biểu đồ nội lực"); }),
                DialogUi.Btn("Bảng kết quả…", (s, e) => _m.MnuTables_Click(this, new RoutedEventArgs())),
                DialogUi.Btn("So sánh ETABS/SAP…", (s, e) => _m.MnuCrossCheck_Click(this, new RoutedEventArgs()))));
            sp.Children.Add(box);
            _confirm = () =>
            {
                if (_m._res == null) { Warn("Chưa có kết quả phân tích – bấm \"Chạy phân tích\"."); return false; }
                if (MessageBox.Show(Window, "Bạn đã xem kết quả phân tích (biểu đồ, phản lực, chuyển vị, cảnh báo) và thấy hợp lý?", Window.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return false;
                _m.MarkResultsReviewed();
                return true;
            };
        }

        private string ResultText()
        {
            var r = _m._res;
            if (r == null) return "(chưa phân tích)";
            var l = new List<string> { $"{r.Equations} phương trình, {r.Cases.Count} trường hợp tải / tổ hợp, {r.Warnings.Count} cảnh báo." };
            foreach (var w in r.Warnings.Take(30)) l.Add("· " + w);
            return string.Join(Environment.NewLine, l);
        }

        // ------------------------------------------------------------- 7
        private void StepOptimize(StackPanel sp)
        {
            var p = _m._p;
            p.CostRates ??= new CostRates();
            var tC = new TextBox { Width = 110, Text = p.CostRates.Concrete.ToString("0", CultureInfo.InvariantCulture) };
            var tR = new TextBox { Width = 110, Text = p.CostRates.Rebar.ToString("0", CultureInfo.InvariantCulture) };
            var tF = new TextBox { Width = 110, Text = p.CostRates.Formwork.ToString("0", CultureInfo.InvariantCulture) };
            var chkW = new CheckBox { Content = "Giữ bề rộng dầm, cạnh ngắn cột theo tường (chỉ đổi chiều cao dầm, cạnh dài cột, chiều dày sàn)", IsChecked = true, Margin = new Thickness(0, 4, 0, 4) };
            sp.Children.Add(Para("Phần mềm tăng tiết diện cấu kiện không đạt, rồi thử giảm tiết diện cấu kiện dư khả năng và tăng chiều cao dầm nhiều thép; mỗi phương án đều phân tích, thiết kế lại; "
                                 + "chỉ giữ thay đổi làm tổng chi phí bê tông + thép + ván khuôn giảm và mọi cấu kiện vẫn đạt."));
            sp.Children.Add(DialogUi.Row(DialogUi.Label("Bê tông (đ/m³)", 130), tC, DialogUi.Label("  Thép (đ/kg)", 100), tR, DialogUi.Label("  Ván khuôn (đ/m²)", 130), tF));
            sp.Children.Add(DialogUi.Hint("Đơn giá mặc định chỉ để so sánh phương án – sửa theo đơn giá địa phương (lưu trong dự án)."));
            sp.Children.Add(chkW);
            var box = Box(_optRes == null ? "(chưa tối ưu)" : OptText(), 300);
            sp.Children.Add(DialogUi.Row(
                DialogUi.Btn("Chạy tối ưu", async (s, e) =>
                {
                    p.CostRates.Concrete = DialogUi.ParseNum(tC.Text, p.CostRates.Concrete);
                    p.CostRates.Rebar = DialogUi.ParseNum(tR.Text, p.CostRates.Rebar);
                    p.CostRates.Formwork = DialogUi.ParseNum(tF.Text, p.CostRates.Formwork);
                    var o = new OptimizeOptions { KeepWidth = chkW.IsChecked == true };
                    _beforeOptimize ??= _m.Snapshot();
                    SectionOptimizer.Result res = null;
                    bool ok = await _m.RunBusy("Lỗi tối ưu tiết diện", prog => { res = SectionOptimizer.Run(p, p.CostRates, o, prog); });
                    if (!ok || res == null) return;
                    _optRes = res;
                    _m.MarkDirty();                       // tiết diện đổi → kết quả cũ hết hiệu lực …
                    _m._res = res.Final.Analysis;         // … thay bằng kết quả của phương án tối ưu
                    _m._design = res.Final.Design;
                    _m.RefreshAll(false); _m.RefreshResults(); _m.RefreshDesign();
                    box.Text = OptText();
                    Status(res.AllOk ? $"Tối ưu xong: {res.Final.Cost / 1e6:0.00} triệu đồng (giảm {res.Saving / 1e6:0.00} triệu so với ban đầu)." : "Tối ưu: còn cấu kiện không đạt – xem nhật ký.");
                }, bold: true),
                DialogUi.Btn("Bỏ kết quả tối ưu (về tiết diện trước)", (s, e) =>
                {
                    if (_beforeOptimize == null) return;
                    _m.RestoreSnapshot(_beforeOptimize);
                    _m._res = null; _m._design = null; _optRes = null; _beforeOptimize = null;
                    _m.MarkDirty(); _m.RefreshAll(true);
                    box.Text = "(đã quay về tiết diện trước khi tối ưu – cần phân tích lại)";
                    _maxDone = Math.Min(_maxDone, 4);
                    Status("Đã quay về tiết diện trước khi tối ưu – chạy lại phân tích (bước 6).");
                })));
            sp.Children.Add(box);
            _confirm = () =>
            {
                if (_optRes == null) { Warn("Bấm \"Chạy tối ưu\" trước (hoặc \"Bỏ kết quả tối ưu\" rồi quay lại bước 6)."); return false; }
                if (!_optRes.AllOk) { Warn("Phương án còn cấu kiện không đạt – xem nhật ký, sửa mô hình rồi chạy lại."); return false; }
                string msg = $"Chấp nhận phương án tối ưu?\n\nChi phí: {_optRes.Final.Cost / 1e6:0.00} triệu đồng (ban đầu {_optRes.Initial.Cost / 1e6:0.00}).";
                if (_optRes.WidthChanges.Count > 0) msg += "\n\nLưu ý – phải tăng bề rộng khác tường ở: " + string.Join("; ", _optRes.WidthChanges.Distinct().Take(8));
                if (MessageBox.Show(Window, msg, Window.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return false;
                _m.MarkResultsReviewed();   // người dùng đã xem kết quả phương án tối ưu
                _beforeOptimize = null;
                return true;
            };
        }

        private string OptText()
        {
            var r = _optRes;
            var l = new List<string>(r.Log);
            if (r.Changes.Count > 0) { l.Add(""); l.Add("Thay đổi tiết diện:"); l.AddRange(r.Changes.Distinct().Take(80).Select(c => "  " + c)); }
            if (r.WidthChanges.Count > 0) { l.Add(""); l.Add("PHẢI TĂNG BỀ RỘNG (khác tường kiến trúc) – bạn quyết định:"); l.AddRange(r.WidthChanges.Distinct().Select(c => "  " + c)); }
            return string.Join(Environment.NewLine, l);
        }

        // ------------------------------------------------------------- 8
        private void StepDesign(StackPanel sp)
        {
            var d = _m._design;
            if (d == null)
            {
                sp.Children.Add(Para("Chưa có kết quả thiết kế – bấm \"Thiết kế\"."));
                sp.Children.Add(DialogUi.Row(DialogUi.Btn("Thiết kế", (s, e) => { _m.MnuDesignConcrete_Click(this, new RoutedEventArgs()); }, bold: true), DialogUi.Btn("Làm mới", (s, e) => Show(7))));
            }
            else
            {
                int bad = d.All.Count(m => !m.Ok);
                sp.Children.Add(Para($"Thiết kế: {d.Beams.Count} dầm, {d.Columns.Count} cột, {d.Slabs.Count} ô sàn, {d.Footings.Count + d.PileCaps.Count + d.StripFootings.Count} móng – {bad} cấu kiện không đạt.", true));
                if (bad > 0) sp.Children.Add(Err("Cấu kiện không đạt: " + string.Join(", ", d.All.Where(m => !m.Ok).Take(20).Select(m => m.Name)) + " – thường là móng (cần số liệu địa chất) hoặc cần sửa tay."));
                sp.Children.Add(DialogUi.Row(
                    DialogUi.Btn("Xem tab Thiết kế", (s, e) => _m.SelectLeft("Thiết kế")),
                    DialogUi.Btn("Thiết kế móng…", (s, e) => _m.MnuDesignFoundation_Click(this, new RoutedEventArgs())),
                    DialogUi.Btn("Xuất toàn bộ hồ sơ…", (s, e) => _m.MnuExpAll_Click(this, new RoutedEventArgs()), bold: true),
                    DialogUi.Btn("Xuất sang Revit (kết cấu + chuẩn)…", (s, e) => _m.MnuExportToRevit_Click(this, new RoutedEventArgs())),
                    DialogUi.Btn("Làm mới", (s, e) => Show(7))));
            }
            sp.Children.Add(DialogUi.Hint("Người có chứng chỉ hành nghề kiểm tra thuyết minh, bản vẽ và ký trước khi phát hành."));
            _confirm = () => true;
        }

        // -------------------------------------------------------------
        private bool AskReplace() =>
            _m._p.Frames.Count == 0 || MessageBox.Show(Window, "Thay thế mô hình hiện tại bằng mô hình lập từ Revit?\n(Giữ lại vật liệu, tiết diện, thông số gió / động đất.)",
                Window.Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;

        private void AfterModelReplaced()
        {
            _m._res = null; _m._design = null; _m._sel = null;
            _m.MarkDirty();
            _m.LoadUiFromProject();
            _m.RefreshAll(true);
        }

        private void Warn(string s) { Status(s); MessageBox.Show(Window, s, Window.Title, MessageBoxButton.OK, MessageBoxImage.Information); }
    }
}

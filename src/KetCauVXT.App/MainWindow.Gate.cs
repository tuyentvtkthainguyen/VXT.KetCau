using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Docs;

namespace KetCauVXT.App;

// =====================================================================
// KHOÁ BƯỚC THEO QUY TRÌNH (lõi: Docs/WorkflowGate.cs)
//   PassGate(thao tác): chưa xong bước tiền đề → hộp thoại liệt kê bước thiếu + nút "Làm bước: …" chuyển tới bước đó; không làm thao tác.
//   Nút "➜ Bước tiếp: …" ở thanh trạng thái: luôn chỉ bước nên làm tiếp theo, bấm để chuyển tới.
//   Kết quả phân tích được coi là "đã kiểm tra" khi người dùng mở bảng kết quả, biểu đồ nội lực, dạng dao động, hình biến dạng,
//   so sánh ETABS/SAP, hoặc bấm "Tôi đã kiểm tra kết quả" – gắn với đúng bộ kết quả hiện hành (phân tích lại phải xem lại).
//   Chạy kiểm thử giao diện (tham số --…) không khoá để giữ nguyên các kịch bản tự kiểm tra.
// =====================================================================
public partial class MainWindow
{
    /// <summary>Bộ kết quả phân tích người dùng đã xem / xác nhận (so sánh tham chiếu với _res).</summary>
    private object _reviewedRes;
    private Button _btnNextStep;
    private string _nextStepId;

    private bool ResultsReviewed => _res != null && ReferenceEquals(_reviewedRes, _res);

    private WorkflowState GateState() => new()
    {
        Project = _p,
        HasResults = _res != null,
        Design = _design,
        ResultsReviewed = ResultsReviewed,
    };

    /// <summary>Gọi từ InitWorkflowUi: nút Bước tiếp ở thanh trạng thái, theo dõi xem kết quả.</summary>
    private void InitGateUi()
    {
        try
        {
            _btnNextStep = new Button
            {
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(4, 0, 4, 0),
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromRgb(232, 242, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(120, 160, 220)),
                Visibility = Visibility.Collapsed,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            _btnNextStep.Click += (s, e) => { if (!string.IsNullOrEmpty(_nextStepId)) DoGateStep(_nextStepId); };
            var item = new System.Windows.Controls.Primitives.StatusBarItem { Content = _btnNextStep };
            int idx = mainStatus.Items.IndexOf(mainStatus.Items.OfType<System.Windows.Controls.Primitives.StatusBarItem>().FirstOrDefault(i => ReferenceEquals(i.Content, txtStatus)));
            if (idx >= 0) mainStatus.Items.Insert(idx, item); else mainStatus.Items.Add(item);
        }
        catch { _btnNextStep = null; }   // không chèn được nút: vẫn chạy, chỉ thiếu gợi ý ở thanh trạng thái

        // xem khung nhìn kết quả = đã kiểm tra kết quả hiện hành
        tabView.SelectionChanged += (s, e) =>
        {
            if (!ReferenceEquals(e.OriginalSource, tabView)) return;
            if (tabView.SelectedItem is TabItem t && t.Header is string h && (h == "Biểu đồ nội lực" || h == "Dao động")) MarkResultsReviewed();
        };
        Dispatcher.BeginInvoke(new Action(UpdateNextStep), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    /// <summary>Đánh dấu bộ kết quả hiện hành đã được người dùng xem.</summary>
    private void MarkResultsReviewed()
    {
        if (_res == null || ReferenceEquals(_reviewedRes, _res)) return;
        _reviewedRes = _res;
        UpdateNextStep();
    }

    /// <summary>Cập nhật nút "Bước tiếp" và bảng Bắt đầu nhanh (gọi sau mỗi thay đổi trạng thái dự án).</summary>
    private void UpdateNextStep()
    {
        try
        {
            var g = _p == null ? null : WorkflowGate.Next(GateState());
            _nextStepId = g?.StepId;
            if (_btnNextStep != null)
            {
                if (g == null)
                {
                    _btnNextStep.Content = "✓ Đã đủ các bước";
                    _btnNextStep.ToolTip = "Mọi bước bắt buộc đã xong và đã dàn trang. Có thể xuất lại hồ sơ bất cứ lúc nào.";
                    _btnNextStep.Visibility = _p != null && _p.Frames.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    _btnNextStep.Content = "➜ Bước tiếp: " + g.Title;
                    _btnNextStep.ToolTip = g.Reason + "\n" + g.Todo + "\n(Bấm để chuyển tới bước này)";
                    _btnNextStep.Visibility = Visibility.Visible;
                }
            }
            if (_quickStartWin != null && _quickStartWin.IsLoaded) _quickStartWin.Refresh();
        }
        catch { /* gợi ý không được làm hỏng thao tác chính */ }
    }

    /// <summary>Kiểm tra bước tiền đề của thao tác. Được làm → true. Chưa được → hộp thoại nêu bước thiếu, gợi ý bước tiếp → false.</summary>
    private bool PassGate(WorkflowAction action)
    {
        if (IsSelfTestRun) return true;   // kịch bản tự kiểm tra tự dựng đủ dữ liệu
        if (_p == null) return false;
        // phân tích / xuất mô hình vốn đọc ô nhập vào dự án trước khi chạy: đọc trước để xét tải trọng, gió, động đất đang nhập
        if (action == WorkflowAction.Analysis || action == WorkflowAction.ExportModel)
            try { ApplyUiToProject(false); } catch { /* đọc giao diện lỗi: xét theo dự án hiện có */ }
        GateResult r;
        try { r = WorkflowGate.Check(action, GateState(), runModelCheck: action == WorkflowAction.Analysis); }
        catch { return true; }   // lỗi của bộ xét khoá không chặn công việc
        if (r.Allowed) return true;
        bool go = ShowGateDialog(r);
        UpdateNextStep();
        return go;
    }

    /// <summary>Hộp thoại "Chưa thể …": danh sách bước thiếu theo thứ tự, nút làm bước đầu tiên.
    /// Chỉ thiếu bước xác nhận đã xem kết quả → có nút "Tôi đã kiểm tra kết quả – tiếp tục" (trả true).</summary>
    private bool ShowGateDialog(GateResult r)
    {
        bool proceed = false;
        string goStep = null;
        var w = new Window();
        DialogUi.Setup(w, "Chưa thể " + WorkflowGate.ActionTitle(r.Action).ToLowerInvariant() + " – Việt Const", 640, 420);
        if (IsLoaded) w.Owner = this;
        w.SizeToContent = SizeToContent.Height;
        w.ResizeMode = ResizeMode.NoResize;

        var body = new StackPanel { Margin = new Thickness(14, 12, 14, 6) };
        body.Children.Add(new TextBlock
        {
            Text = $"Cần làm xong {(r.Missing.Count == 1 ? "bước" : r.Missing.Count + " bước")} sau trước khi {WorkflowGate.ActionTitle(r.Action).ToLowerInvariant()}:",
            FontWeight = FontWeights.SemiBold, FontSize = 13.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8),
        });
        int i = 0;
        foreach (var m in r.Missing)
        {
            i++;
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            sp.Children.Add(new TextBlock { Text = $"{i}. {m.Title}", FontWeight = i == 1 ? FontWeights.Bold : FontWeights.SemiBold, Foreground = i == 1 ? Brushes.DarkRed : Brushes.Black });
            sp.Children.Add(new TextBlock { Text = m.Reason, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 1, 0, 0) });
            sp.Children.Add(new TextBlock { Text = "→ " + m.Todo, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(16, 1, 0, 0) });
            body.Children.Add(sp);
        }
        body.Children.Add(DialogUi.Hint("Quy trình: lưới trục, tầng → tiết diện → cấu kiện → tải trọng, tổ hợp → kiểm tra mô hình → phân tích → kiểm tra kết quả → thiết kế → mọi cấu kiện đạt → xuất hồ sơ."));

        var first = r.First;
        var buttons = new List<UIElement>();
        if (r.OnlyReviewMissing)
        {
            buttons.Add(DialogUi.Btn("Xem kết quả", (s, e) => { goStep = first.StepId; w.Close(); }, bold: true));
            buttons.Add(DialogUi.Btn("Tôi đã kiểm tra kết quả – tiếp tục", (s, e) => { _reviewedRes = _res; proceed = true; w.Close(); }));
        }
        else if (first != null)
            buttons.Add(DialogUi.Btn("Làm bước: " + first.Title, (s, e) => { goStep = first.StepId; w.Close(); }, bold: true));
        buttons.Add(DialogUi.Btn("Đóng", (s, e) => w.Close()));
        var row = DialogUi.Row(buttons.ToArray());
        row.HorizontalAlignment = HorizontalAlignment.Right;
        row.Margin = new Thickness(14, 4, 14, 12);

        var root = new DockPanel();
        DockPanel.SetDock(row, Dock.Bottom); root.Children.Add(row);
        root.Children.Add(body);
        w.Content = root;
        w.ShowDialog();

        if (goStep != null) Dispatcher.BeginInvoke(new Action(() => DoGateStep(goStep)), System.Windows.Threading.DispatcherPriority.Background);
        else if (!proceed && first != null) SetStatus($"Chưa thể {WorkflowGate.ActionTitle(r.Action).ToLowerInvariant()}: {first.Title} – {first.Reason}");
        return proceed;
    }

    /// <summary>Chuyển tới bước theo mã (bước Bắt đầu nhanh và các bước phụ của bộ xét khoá).</summary>
    private void DoGateStep(string id)
    {
        switch (id)
        {
            case WorkflowGate.ModelCheckStepId:
                Activate();
                OpenModelCheck();
                break;
            case WorkflowGate.DesignOkStepId:
                Activate();
                SelectLeft("Thiết kế");
                SetStatus("Cấu kiện không đạt tô đỏ trong tab Thiết kế: tăng tiết diện / chọn lại thép (Thiết kế → Tự tăng tiết diện đến khi đạt), rồi thiết kế lại.");
                break;
            case WorkflowGate.ReviewStepId:
                Activate();
                if (!IsSelfTestRun && _res == null) { ShowGateDialog(WorkflowGate.Check(WorkflowAction.Results, GateState(), runModelCheck: false)); break; }
                SelectLeft("Kiểm tra");
                SelectView("Biểu đồ nội lực");   // xem biểu đồ = đã kiểm tra (InitGateUi)
                SetStatus("Xem biểu đồ nội lực, phản lực (Ctrl+T), cảnh báo; so sánh ETABS/SAP khi cần.");
                break;
            default:
                // bước bị khoá (vd. Thiết kế khi chưa phân tích): báo bước tiền đề thay vì chuyển tới
                var a = WorkflowGate.ActionOfStep(id);
                if (a != null && !IsSelfTestRun && _p != null)
                {
                    var r = WorkflowGate.Check(a.Value, GateState(), runModelCheck: false);
                    if (!r.Allowed && !ShowGateDialog(r)) break;   // chỉ thiếu xác nhận đã xem kết quả + người dùng xác nhận → làm tiếp
                }
                DoQuickStep(id);
                break;
        }
        UpdateNextStep();
    }
}

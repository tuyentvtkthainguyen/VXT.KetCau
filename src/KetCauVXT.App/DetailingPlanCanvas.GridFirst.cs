using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Detailing;

namespace KetCauVXT.App;

/// <summary>[Hệ trục trước] Mặt bằng chưa có hệ trục: chọn công cụ tạo cấu kiện → hộp thoại tạo hệ trục trước
/// (nhập bước trục, tự nhận từ cột / dầm đã có, lấy từ bản vẽ CAD, hoặc vẽ tự do không trục). Nhập AutoCAD khi chưa có trục → tự tạo trục.</summary>
public partial class DetailingPlanCanvas
{
    private bool _gridSkip;          // người dùng chọn vẽ không trục (trong phiên)
    private bool _gridAsking;

    private static bool CreatesItems(string tool) => tool is not ("Chon" or "Xoa" or ToolMove or ToolSection or ToolCallout or ToolSplitBeam);

    /// <summary>Gọi khi đổi công cụ: chưa có trục → hỏi tạo hệ trục. false = quay về công cụ Chọn.</summary>
    private bool EnsureGrid()
    {
        if (_gridAsking || _gridSkip || Plan == null || Plan.HasGrid || !CreatesItems(_tool)) return true;
        _gridAsking = true;
        try
        {
            AskGridFirst();
            if (Plan.HasGrid || _gridSkip) return true;
            _tools["Chon"].IsChecked = true;
            return false;
        }
        finally { _gridAsking = false; }
    }

    private void AskGridFirst()
    {
        var w = new Window { Owner = _owner, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize };
        DialogUi.Setup(w, "Bước 1 – Tạo hệ trục trước khi đặt cấu kiện", 600, 380);
        w.MinHeight = 0;
        var root = new StackPanel { Margin = new Thickness(12) };
        var intro = DialogUi.Hint("Mặt bằng chưa có hệ trục. Hồ sơ kết cấu định vị cột, dầm, sàn theo trục (1, 2, 3… / A, B, C…) – tạo hệ trục trước rồi mới đặt cấu kiện: bắt điểm giao trục, nhịp dầm chia theo trục, bản vẽ ghi kích thước trục.");
        intro.TextWrapping = TextWrapping.Wrap; intro.MaxWidth = 560;
        root.Children.Add(intro);
        var tx = new TextBox { Width = 300, Text = Plan.SpansX ?? "" };
        var ty = new TextBox { Width = 300, Text = Plan.SpansY ?? "" };
        var (lx, ly) = PlanGridFirst.Lines(Plan.Levels);
        string ix = PlanGridFirst.Spans(lx), iy = PlanGridFirst.Spans(ly);
        if (string.IsNullOrEmpty(tx.Text) && !string.IsNullOrEmpty(ix)) tx.Text = ix;
        if (string.IsNullOrEmpty(ty.Text) && !string.IsNullOrEmpty(iy)) ty.Text = iy;
        root.Children.Add(DialogUi.Row(DialogUi.Label("Bước trục X (1, 2, 3…) m", 190), tx));
        root.Children.Add(DialogUi.Row(DialogUi.Label("Bước trục Y (A, B, C…) m", 190), ty));
        var note = DialogUi.Hint("Ví dụ: 3.6 4.2 4.2 hoặc 3*3.3 (3 nhịp 3,3 m). Sau này thêm / dời / xoá trục: nút \"Thêm trục…\" hoặc chuột phải lên trục.");
        note.TextWrapping = TextWrapping.Wrap; note.MaxWidth = 560;
        root.Children.Add(note);
        var msg = new TextBlock { Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap, MaxWidth = 560, Margin = new Thickness(0, 4, 0, 0) };
        root.Children.Add(msg);

        var ok = DialogUi.Btn("Tạo hệ trục", (s, e) =>
        {
            var cx = StructuralPlan.Coords(tx.Text); var cy = StructuralPlan.Coords(ty.Text);
            if (cx.Length < 2 || cy.Length < 2) { msg.Text = "Nhập ít nhất một bước trục mỗi phương (m)."; return; }
            _undo.Push(Plan);
            _sx.Text = tx.Text.Trim(); _sy.Text = ty.Text.Trim();
            ApplyGrid();
            _status.Text = $"Đã tạo hệ trục {cx.Length} × {cy.Length} trục – bắt đầu đặt cấu kiện.";
            w.Close();
        }, true);
        var auto = DialogUi.Btn("Tự nhận từ cột / dầm đã có", (s, e) =>
        {
            _undo.Push(Plan);
            string r = PlanGridFirst.Infer(Plan);
            if (r == null) { msg.Text = "Chưa đủ cột / dầm thẳng hàng để nhận trục (cần ≥ 2 trục mỗi phương, mỗi trục ≥ 2 cấu kiện)."; return; }
            _sx.Text = Plan.SpansX; _sy.Text = Plan.SpansY;
            _fitted = false; _changed(); Redraw();
            _status.Text = r;
            w.Close();
        });
        auto.IsEnabled = !string.IsNullOrEmpty(ix) && !string.IsNullOrEmpty(iy);
        var cad = DialogUi.Btn("Lấy từ bản vẽ CAD / ảnh…", (s, e) => { w.Close(); LoadPlanGrid(); });
        var skip = DialogUi.Btn("Vẽ không có trục", (s, e) => { _gridSkip = true; w.Close(); });
        skip.ToolTip = "Đặt cấu kiện tự do (bắt điểm làm tròn 50 mm) – không hỏi lại trong phiên này";
        root.Children.Add(DialogUi.Row(ok, auto, cad));
        root.Children.Add(DialogUi.Row(skip, DialogUi.Btn("Huỷ", (s, e) => w.Close())));
        w.Content = root;
        w.ShowDialog();
    }

    /// <summary>Sau khi nhập AutoCAD: chưa có trục → tự tạo từ cột / dầm vừa nhập.</summary>
    private string GridAfterImport()
    {
        if (Plan.HasGrid) return "";
        string r = PlanGridFirst.Infer(Plan);
        if (r == null) return " Chưa nhận được hệ trục từ bản vẽ – tạo hệ trục bằng bước trục X / Y.";
        _sx.Text = Plan.SpansX; _sy.Text = Plan.SpansY;
        return " " + r;
    }
}

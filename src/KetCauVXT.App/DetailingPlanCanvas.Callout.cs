using System.Globalization;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Detailing;

namespace KetCauVXT.App;

/// <summary>Công cụ "Trích chi tiết" của không gian vẽ mặt bằng: khoanh cửa sổ (2 góc) → chi tiết "CHI TIẾT A" tl 1/25 vẽ lại vùng đó
/// (Core/Detailing/DetailCallout.cs); chuột phải lên khung: đổi tên / tỷ lệ / ghi chú / xoá; bật tắt vạch cắt dầm trên mặt bằng.</summary>
public sealed partial class DetailingPlanCanvas
{
    public const string ToolCallout = "TrichCT";

    private string CalloutHint() => _first == null
        ? "Trích chi tiết: bấm góc thứ nhất của vùng cần trích (khoanh / \"scan\" vị trí cần thể hiện rõ) – chi tiết tl 1/25 xuất cùng mặt bằng."
        : "Trích chi tiết: bấm góc đối diện – Esc huỷ. Chuột phải lên khung chi tiết: đổi tên, tỷ lệ, ghi chú, xoá.";

    /// <summary>Xử lý công cụ trích chi tiết (và xoá khung chi tiết bằng công cụ Xoá). true = đã xử lý.</summary>
    private bool HandleCalloutTool(PlanLevel lv, (double X, double Y) w, double tol)
    {
        if (_tool == "Xoa")
        {
            var hit = Plan.HitCallout(lv, w.X, w.Y, tol);
            if (hit == null) return false;
            Plan.Callouts.Remove(hit); _changed(); UpdateHint(); Redraw();
            return true;
        }
        if (_tool != ToolCallout) return false;
        if (_first == null) { _first = w; _status.Text = CalloutHint(); Redraw(); return true; }
        var a = _first.Value;
        _first = null;
        var c = Plan.AddCallout(lv, a.X, a.Y, w.X, w.Y, 25);
        if (c == null) { _status.Text = "Vùng trích quá nhỏ (≥ 50 mm mỗi cạnh)."; Redraw(); return true; }
        string nm = DialogUi.InputBox(_owner, "Trích chi tiết", "Tên chi tiết (vd. A):", c.Name);
        if (string.IsNullOrWhiteSpace(nm)) { Plan.Callouts.Remove(c); Redraw(); return true; }
        c.Name = nm.Trim();
        _changed(); _status.Text = CalloutHint(); Redraw();
        return true;
    }

    /// <summary>Menu chuột phải khung chi tiết; null nếu không trúng.</summary>
    private ContextMenu CalloutMenuAt(PlanLevel lv, (double X, double Y) w, double tol)
    {
        var c = Plan.HitCallout(lv, w.X, w.Y, tol);
        if (c == null) return null;
        var cm = new ContextMenu();
        var ren = new MenuItem { Header = "Đổi tên chi tiết " + c.Name + "…" };
        ren.Click += (s, e) =>
        {
            string n = DialogUi.InputBox(_owner, "Đổi tên chi tiết", "Tên mới:", c.Name);
            if (string.IsNullOrWhiteSpace(n)) return;
            c.Name = n.Trim(); _changed(); Redraw();
        };
        var sc = new MenuItem { Header = "Tỷ lệ chi tiết (1/" + c.Scale.ToString("0", CultureInfo.InvariantCulture) + ")" };
        foreach (double v in CalloutDrawing.Scales)
        {
            var mi = new MenuItem { Header = "1/" + v.ToString("0", CultureInfo.InvariantCulture), IsCheckable = true, IsChecked = Math.Abs(v - c.Scale) < 1e-6 };
            mi.Click += (s, e) => { c.Scale = v; _changed(); Redraw(); };
            sc.Items.Add(mi);
        }
        var note = new MenuItem { Header = "Ghi chú dưới tên chi tiết…" };
        note.Click += (s, e) =>
        {
            string n = DialogUi.InputBox(_owner, "Ghi chú chi tiết " + c.Name, "Ghi chú:", c.Note ?? "");
            if (n == null) return;
            c.Note = n.Trim(); _changed();
        };
        var del = new MenuItem { Header = "Xoá chi tiết " + c.Name };
        del.Click += (s, e) => { Plan.Callouts.Remove(c); _changed(); UpdateHint(); Redraw(); };
        var cuts = new MenuItem { Header = "Thể hiện vị trí mặt cắt dầm trên mặt bằng", IsCheckable = true, IsChecked = Plan.ShowBeamCuts };
        cuts.Click += (s, e) => { Plan.ShowBeamCuts = !Plan.ShowBeamCuts; _changed(); };
        cm.Items.Add(ren); cm.Items.Add(sc); cm.Items.Add(note); cm.Items.Add(del); cm.Items.Add(new Separator()); cm.Items.Add(cuts);
        return cm;
    }

    /// <summary>Vẽ khung các chi tiết trích của mặt bằng đang xem (nét đứt tím + vòng tên).</summary>
    private void DrawCallouts(PlanLevel lv)
    {
        if (lv == null) return;
        var br = Brushes.DarkViolet;
        foreach (var c in (Plan.Callouts ?? new()).Where(q => q.Level == lv.Name))
        {
            Line(c.MinX, c.MinY, c.MaxX, c.MinY, br, 1.2, true); Line(c.MaxX, c.MinY, c.MaxX, c.MaxY, br, 1.2, true);
            Line(c.MaxX, c.MaxY, c.MinX, c.MaxY, br, 1.2, true); Line(c.MinX, c.MaxY, c.MinX, c.MinY, br, 1.2, true);
            double k = 14 / _k;
            Text(c.MaxX + k, c.MaxY + k, c.Tag + " (1/" + c.Scale.ToString("0", CultureInfo.InvariantCulture) + ")", br, 12);
        }
    }
}

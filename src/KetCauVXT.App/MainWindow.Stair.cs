using System.ComponentModel;
using System.Windows;
using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Một dòng bảng cầu thang ở tab Thiết kế → Cầu thang (mỗi bộ phận một dòng).</summary>
internal sealed class StairDesignRow
{
    [DisplayName("Cầu thang")] public string Stair { get; set; } = "";
    [DisplayName("Bộ phận")] public string Part { get; set; } = "";
    [DisplayName("SL")] public int Qty { get; set; }
    [DisplayName("Nhịp L0 (m)")] public double Span { get; set; }
    [DisplayName("b×h (mm)")] public string Size { get; set; } = "";
    [DisplayName("q tt")] public double Q { get; set; }
    [DisplayName("M (kN.m)")] public double M { get; set; }
    [DisplayName("Q (kN)")] public double V { get; set; }
    [DisplayName("Thép chịu lực")] public string Main { get; set; } = "";
    [DisplayName("Thép mũ / trên")] public string Top { get; set; } = "";
    [DisplayName("Phân bố / đai")] public string Dist { get; set; } = "";
    [DisplayName("Tỷ số")] public double Ratio { get; set; }
    [Browsable(false)] public bool IsOk { get; set; }
    [DisplayName("Kết luận")] public string Result => IsOk ? "Đạt" : "KHÔNG ĐẠT";
    [DisplayName("Ghi chú")] public string Note { get; set; } = "";

    public static IEnumerable<StairDesignRow> From(StairDesign d)
    {
        string head = $"{d.Name} ({d.Story}) – {d.Risers} bậc {d.Riser * 1000:0}×{d.Tread * 1000:0}, 2h+b = {d.Check2hb * 1000:0}" + (d.IsDefault ? " – mặc định theo lỗ sàn" : "");
        foreach (var p in d.Parts)
            yield return new StairDesignRow
            {
                Stair = head, Part = p.Name, Qty = p.Quantity, Span = Math.Round(p.Span, 3),
                Size = p.Kind.StartsWith("Dầm") ? $"{p.B * 1000:0}×{p.H * 1000:0}" : $"bản {p.H * 1000:0}",
                Q = Math.Round(p.Q, 2), M = Math.Round(p.M, 2), V = Math.Round(p.V, 2), Main = p.MainText, Top = p.TopText,
                Dist = p.Kind.StartsWith("Dầm") ? p.StirrupText : p.DistText, Ratio = Math.Round(p.MaxRatio, 3), IsOk = p.Ok,
                Note = string.Join("; ", p.Messages)
            };
    }
}

// =====================================================================
// CẦU THANG: nút "Khai báo cầu thang…" (thanh sửa mặt bằng) cho ô cầu thang đang chọn, hộp thoại StairDialog,
// áp dụng có hoàn tác (Snapshot/PushUndo qua AfterEdit), tự phân tích + thiết kế lại; bảng kết quả ở tab Thiết kế → Cầu thang.
// =====================================================================
public partial class MainWindow
{
    private void BtnStair_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null || _busy) return;
        var o = _selOpening;
        if (o == null || !_p.Openings.Contains(o) || o.Kind != OpeningKind.Stair)
        {
            SetStatus("Chọn một ô cầu thang trên mặt bằng (chế độ Chọn) rồi bấm \"Khai báo cầu thang…\".");
            return;
        }
        var dlg = CreateStairDialog(o, true);
        dlg.ShowDialog();
    }

    /// <summary>Hộp thoại cầu thang cho lỗ sàn o. redesign = sau khi áp dụng tự chạy phân tích + thiết kế.</summary>
    private StairDialog CreateStairDialog(FloorOpening o, bool redesign)
    {
        var draft = StairEdit.Draft(_p, o);
        bool exists = StairEdit.ForOpening(_p, o) != null;
        return new StairDialog(_p, draft, exists,
            s => { bool ok = ApplyStair(s); if (ok && redesign) _ = RedesignAfterStair(); return ok; },
            s => RemoveStair(o))
        { Owner = this };
    }

    /// <summary>Đưa cầu thang vào dự án (có hoàn tác). Kết quả phân tích/thiết kế cũ bị huỷ.</summary>
    private bool ApplyStair(Stair s)
    {
        if (_p == null || _busy || s == null) return false;
        string snap = Snapshot();
        EditResult r;
        try { r = StairEdit.Apply(_p, s.Clone()); }
        catch (Exception ex) { RestoreSnapshot(snap); ShowError("Không khai báo được cầu thang", ex); return false; }
        if (!r.Ok) { SetStatus(r.Message); return false; }
        var o = _p.Openings.FirstOrDefault(x => x.Id == s.OpeningId);
        AfterEdit(snap, r, o);
        return true;
    }

    private bool RemoveStair(FloorOpening o)
    {
        var s = StairEdit.ForOpening(_p, o);
        if (s == null || _busy) return false;
        if (!ConfirmDelete($"Xoá khai báo cầu thang {s.Name}? Lỗ sàn trở lại tải ước tính trên dầm viền.")) return false;
        string snap = Snapshot();
        var r = StairEdit.Remove(_p, s);
        if (!r.Ok) { SetStatus(r.Message); return false; }
        AfterEdit(snap, r, _p.Openings.FirstOrDefault(x => x.Id == o.Id));
        return true;
    }

    /// <summary>Phân tích + thiết kế lại sau khi sửa cầu thang (để có ngay thép, bản vẽ, khối lượng).</summary>
    private async Task<bool> RedesignAfterStair()
    {
        if (_busy) return false;
        if (!await RunAnalysis()) return false;
        if (!await EnsureDesign()) return false;
        int n = _design?.Stairs?.Count ?? 0;
        SetStatus($"Đã khai báo cầu thang, phân tích và thiết kế lại: {n} cầu thang, {_design?.Stairs?.Count(x => !x.Ok) ?? 0} không đạt.");
        return true;
    }

    /// <summary>Bảng cầu thang ở tab Thiết kế (gọi trong RefreshDesign).</summary>
    private void RefreshStairGrid()
    {
        if (dgStairs == null) return;
        dgStairs.ItemsSource = _design?.Stairs == null ? null : _design.Stairs.SelectMany(x => StairDesignRow.From(x)).ToList();
    }
}

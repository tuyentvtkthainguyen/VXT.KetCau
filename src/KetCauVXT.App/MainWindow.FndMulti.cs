using System.Windows;
using System.Windows.Media;
using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// [Gán nhiều móng] CHỌN NHIỀU MÓNG / ĐÀI CỌC / CHÂN CỘT TRÊN MẶT BẰNG MÓNG: Ctrl + bấm (thêm / bỏ), Ctrl + A (mọi móng cùng loại), Esc bỏ chọn.
// Các lệnh áp dụng cho TẤT CẢ móng đang chọn: Sửa móng… (cùng L × B × H, loại móng), Bố trí cọc… (cùng sơ đồ cọc / kích thước đài),
// Gán đài cọc giống nhau (chép bố trí cọc của đài nguồn), Bố trí móng…, Xoá móng chọn.
// =====================================================================
public partial class MainWindow
{
    private readonly HashSet<int> _fndMulti = new();
    private static readonly Brush MultiSelStroke = Frozen(Color.FromRgb(255, 120, 0));

    /// <summary>Ctrl + bấm: thêm / bỏ chân cột của móng (hoặc cột chưa có móng) khỏi danh sách chọn. false = không có chân cột để chọn.</summary>
    private bool FndToggleMulti(int node, FrameElement col, object item)
    {
        int n = node;
        if (n == 0 && col != null)
        {
            var a = _p.Node(col.NodeI); var b = _p.Node(col.NodeJ);
            n = (a.Z <= b.Z ? a : b).Id;
        }
        if (n == 0) return false;
        if (_fndMulti.Count == 0) foreach (int x in SelectedFoundationNodes()) _fndMulti.Add(x);
        bool added = _fndMulti.Add(n);
        if (!added) _fndMulti.Remove(n);
        if (added)
        {
            if (item != null) _fndSelItem = item;
            Select(footingNode: n);
        }
        RedrawFoundationPlan();
        SetStatus(_fndMulti.Count == 0 ? "Đã bỏ chọn." :
            $"Đang chọn {_fndMulti.Count} chân cột (nút {string.Join(", ", _fndMulti.Take(12))}{(_fndMulti.Count > 12 ? "…" : "")}) – Sửa móng… / Bố trí cọc… / Gán đài giống nhau / Xoá móng chọn áp dụng cho tất cả; Esc bỏ chọn.");
        return true;
    }

    /// <summary>Ctrl + A: chọn mọi chân cột cùng loại móng với móng đang chọn (mọi đài cọc / mọi móng đơn / mọi móng băng).</summary>
    private void FndSelectAllSameType()
    {
        if (_design == null) return;
        _fndMulti.Clear();
        switch (_fndSelItem)
        {
            case PileCapDesign: foreach (var c in DesignCaps()) foreach (int n in c.NodeIds) _fndMulti.Add(n); break;
            case StripFootingDesign: foreach (var c in DesignStrips()) foreach (int n in c.NodeIds) _fndMulti.Add(n); break;
            default: foreach (var f in FootingLayout.AllInstances(_p, _design)) _fndMulti.Add(f.NodeId); break;
        }
        RedrawFoundationPlan();
        SetStatus($"Đã chọn {_fndMulti.Count} chân cột cùng loại móng – Esc bỏ chọn.");
    }

    private void DrawFndMultiOverlay()
    {
        if (_fndMulti.Count == 0) return;
        var done = new HashSet<int>();
        foreach (var h in _fndHits)
        {
            int n = h.node;
            if (n == 0 && h.col != null)
            {
                var a = _p.Node(h.col.NodeI); var b = _p.Node(h.col.NodeJ);
                n = (a.Z <= b.Z ? a : b).Id;
            }
            if (n == 0 || !_fndMulti.Contains(n) || !done.Add(n)) continue;
            var r = new Shapes.Rectangle
            {
                Width = h.r.Width + 8, Height = h.r.Height + 8, Stroke = MultiSelStroke, StrokeThickness = 2.2,
                StrokeDashArray = new DoubleCollection { 4, 2 }, IsHitTestVisible = false
            };
            System.Windows.Controls.Canvas.SetLeft(r, h.r.X - 4); System.Windows.Controls.Canvas.SetTop(r, h.r.Y - 4);
            fndCanvas.Children.Add(r);
        }
    }

    /// <summary>Gán bố trí cọc của đài nguồn (đài đang bấm sau cùng) cho mọi chân cột đang chọn → các đài giống nhau.</summary>
    private async void FndCapCopyUi()
    {
        if (_p == null || _busy) return;
        var src = SelectedFndItem() as PileCapDesign ?? _fndSelItem as PileCapDesign;
        var nodes = SelectedFoundationNodes();
        if (src == null) { SetStatus("Bấm chọn đài cọc nguồn (đài bấm sau cùng), giữ Ctrl để chọn thêm các đài / chân cột cần gán giống."); return; }
        var targets = nodes.Where(n => !src.NodeIds.Contains(n)).ToList();
        if (targets.Count == 0) { SetStatus("Giữ Ctrl và bấm thêm các đài / chân cột cần gán giống đài " + src.Name + "."); return; }
        var ovr = PileLayout.FromCap(_p.Foundation, src);
        if (MessageBox.Show(this, $"Gán bố trí cọc của đài {src.Name} ({src.Piles.Count} cọc, đài {src.L:0.00} × {src.B:0.00} × {src.H:0.00} m) cho {targets.Count} chân cột:\n"
                + string.Join(", ", targets.Take(20)) + (targets.Count > 20 ? "…" : "") + "\n\nCác chân cột này sẽ chuyển sang móng cọc với cùng sơ đồ cọc và kích thước đài, rồi thiết kế lại.",
                AppTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        string snap = Snapshot();
        foreach (int t in targets)
        {
            if (FoundationUtil.TypeOf(_p, t) != FoundationType.Pile)
            {
                var ft = _p.Footings.FirstOrDefault(f => f.NodeId == t);
                if (ft == null) FoundationReset.SetSize(_p, t, 0, 0, 0, FoundationType.Pile);
                else ft.Type = FoundationType.Pile;
            }
        }
        PileLayout.Set(_p.Foundation, src.NodeIds.Concat(targets), ovr);
        PushUndo(snap);
        MarkDirty(false);
        _fndMulti.Clear();
        if (!await DesignFoundation()) { SetStatus("Đã gán bố trí cọc – cần thiết kế lại móng."); return; }
        var cap = DesignCaps().FirstOrDefault(c => c.NodeIds.Contains(src.NodeIds[0]));
        int same = cap == null ? 0 : targets.Count(t => cap.NodeIds.Contains(t));
        SetStatus($"Đã gán bố trí cọc đài {src.Name} cho {targets.Count} chân cột – {same} chân cột gộp chung đài {cap?.Name}"
                  + (same < targets.Count ? " (các chân cột còn lại khác nội lực / tiết diện cột nên tách tên đài, cùng sơ đồ cọc)" : "") + ".");
    }
}

using System.Windows;
using System.Windows.Input;
using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.App;

// =====================================================================
// MÓNG: xoá móng đã thiết kế để thiết kế lại, sửa kích thước móng trực tiếp trên mặt bằng móng
// (bấm đúp móng / phím E, Delete xoá móng chọn), di chuyển mặt bằng móng bằng chuột giữa / phải, F vừa màn hình.
// Lõi: Design/Foundation/FoundationReset.cs.
// =====================================================================
public partial class MainWindow
{
    private bool _fMidPan;
    private Point _fMidStart;
    private double _fMidOx, _fMidOy;

    /// <summary>Chân cột thuộc móng đang chọn trên mặt bằng móng (móng đơn, đài cọc, móng băng).</summary>
    private List<int> SelectedFoundationNodes()
    {
        var res = new List<int>(_fndMulti);   // [Gán nhiều móng] Ctrl + bấm chọn nhiều móng / chân cột
        switch (_fndSelItem)
        {
            case FootingDesign fd: res.Add(fd.NodeId); break;
            case PileCapDesign pc: res.AddRange(pc.NodeIds); break;
            case StripFootingDesign sf: res.AddRange(sf.NodeIds); break;
        }
        if (res.Count == 0 && _selFooting != 0) res.Add(_selFooting);
        return res.Distinct().ToList();
    }

    private void AfterFoundationReset(FoundationReset.ResetResult r, string what, string snap)
    {
        PushUndo(snap);
        MarkDirty(false);
        _fndSelItem = null;
        RefreshDesign();
        RedrawSecondaryViews();
        RedrawFoundationPlan();
        ShowSelection();
        SetStatus($"Đã xoá {what}: {r} – bấm \"Thiết kế móng\" để thiết kế lại.");
    }

    private void FndDeleteSelected()
    {
        if (_p == null || _busy) return;
        var nodes = SelectedFoundationNodes();
        if (nodes.Count == 0) { SetStatus("Chọn một móng / đài cọc / móng băng trên mặt bằng móng trước (bấm vào móng)."); return; }
        if (MessageBox.Show(this, $"Xoá móng đã thiết kế tại {nodes.Count} chân cột (nút {string.Join(", ", nodes.Take(8))}{(nodes.Count > 8 ? "…" : "")})?\n\n"
                + "Bỏ kích thước đã chốt, bố trí cọc, thép đã chọn của móng này – loại móng riêng của chân cột được giữ.", AppTitle,
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        string snap = Snapshot();
        var r = FoundationReset.Clear(_p, _design, nodes);
        AfterFoundationReset(r, "móng chọn", snap);
    }

    private void FndDeleteAll()
    {
        if (_p == null || _busy) return;
        int n = (_design?.Footings.Count ?? 0) + (_design?.PileCaps?.Count ?? 0) + (_design?.StripFootings?.Count ?? 0);
        if (MessageBox.Show(this, $"Xoá toàn bộ móng đã thiết kế ({n} móng / đài / dải) để thiết kế lại?\n\n"
                + "Bỏ mọi kích thước móng đã chốt, bố trí cọc, thép đã chọn của móng. Giữ: thông số đất nền, loại móng, ranh giới đất, lệch tâm.", AppTitle,
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        string snap = Snapshot();
        var r = FoundationReset.Clear(_p, _design);
        AfterFoundationReset(r, "toàn bộ móng", snap);
    }

    private async void FndRedesign()
    {
        if (_p == null || _busy) return;
        if (MessageBox.Show(this, "Xoá toàn bộ móng đã thiết kế (kích thước đã chốt, bố trí cọc, thép đã chọn) rồi thiết kế lại từ đầu?", AppTitle,
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        string snap = Snapshot();
        FoundationReset.Clear(_p, _design);
        PushUndo(snap);
        MarkDirty(false);
        await DesignFoundation();
    }

    /// <summary>Sửa kích thước móng chọn (L × B × H, loại móng) ngay trên mặt bằng móng → thiết kế lại với kích thước đó.</summary>
    private async void FndEditSelected()
    {
        if (_p == null || _busy) return;
        var nodes = SelectedFoundationNodes();
        if (nodes.Count == 0) { SetStatus("Bấm chọn một móng trên mặt bằng móng trước."); return; }
        int node = nodes[0];
        var fd = _fndSelItem as FootingDesign ?? _design?.Footings.FirstOrDefault(f => f.NodeId == node);
        var ft = _p.Footings.FirstOrDefault(f => f.NodeId == node);
        double L0 = ft?.L > 0 ? ft.L : fd?.L ?? 0, B0 = ft?.B > 0 ? ft.B : fd?.B ?? 0, H0 = ft?.Height > 0 ? ft.Height : fd?.H ?? 0;
        var f = new MiniForm(this, nodes.Count > 1 ? $"Sửa {nodes.Count} móng chọn (gán giống nhau) – nút {string.Join(", ", nodes.Take(10))}{(nodes.Count > 10 ? "…" : "")}" : $"Sửa móng tại chân cột nút {node}");
        f.Combo("type", "Loại móng tại chân cột", new[] { "Theo công trình (" + FoundationTypeName(_p.Foundation.Type) + ")", "Móng đơn", "Móng cọc", "Móng băng" },
            ft?.Type == null ? 0 : (int)ft.Type.Value + 1);
        f.Num("L", "Cạnh L theo X (m, 0 = tự chọn)", Math.Round(L0, 3));
        f.Num("B", "Cạnh B theo Y (m, 0 = tự chọn)", Math.Round(B0, 3));
        f.Num("H", "Chiều cao móng H (m, 0 = tự chọn)", Math.Round(H0, 3));
        f.Check("run", "Thiết kế lại móng ngay (kiểm tra áp lực, chọc thủng, tính thép)", true);
        f.Hint("Kích thước nhập được chốt cho chân cột này (móng đơn); đặt 0 để chương trình tự chọn lại. Đài cọc: dùng \"Bố trí cọc…\".");
        if (!f.Run("Áp dụng")) return;
        string snap = Snapshot();
        FoundationType? t = f.I("type") <= 0 ? null : (FoundationType)(f.I("type") - 1);
        foreach (int nd in nodes) FoundationReset.Clear(_p, _design, new[] { nd });
        Footing nf = null;
        foreach (int nd in nodes)   // [Gán nhiều móng] mọi móng chọn (Ctrl + bấm) nhận cùng kích thước / loại móng
        {
            var x = FoundationReset.SetSize(_p, nd, f.D("L", 0), f.D("B", 0), f.D("H", 0));
            x.Type = t;
            nf ??= x;
        }
        PushUndo(snap);
        MarkDirty(false);
        SyncFoundationUi();
        RedrawFoundationPlan();
        if (f.B("run")) await DesignFoundation();
        else SetStatus($"Đã chốt {nodes.Count} móng (nút {string.Join(", ", nodes.Take(8))}): {nf.L:0.##} × {nf.B:0.##} × {nf.Height:0.##} m – bấm \"Thiết kế móng\" để kiểm tra.");
    }

    private void BtnFndEdit_Click(object sender, RoutedEventArgs e) => FndEditSelected();
    private void BtnFndDelete_Click(object sender, RoutedEventArgs e) => FndDeleteSelected();
    private void BtnFndDeleteAll_Click(object sender, RoutedEventArgs e) => FndDeleteAll();
    private void BtnFndRedesign_Click(object sender, RoutedEventArgs e) => FndRedesign();

    // ---- chuột giữa / phải: di chuyển; bấm đúp trái: sửa móng; phím ----
    private void FndHost_MouseDown(object sender, MouseButtonEventArgs e)
    {
        fndHost.Focus();
        if (_fndTool != FndTool.None && e.ChangedButton == MouseButton.Left) return;   // [MóngHìnhDạng] đang vẽ: bấm trái là điểm vẽ
        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
        {
            Dispatcher.BeginInvoke(new Action(FndEditSelected), System.Windows.Threading.DispatcherPriority.Background);   // sau khi lần bấm chọn móng đã xử lý
            return;
        }
        if (e.ChangedButton == MouseButton.Middle && e.ClickCount == 2) { BtnFndFit_Click(this, new RoutedEventArgs()); e.Handled = true; return; }
        if (e.ChangedButton != MouseButton.Middle && e.ChangedButton != MouseButton.Right) return;
        _fMidPan = true;
        _fMidStart = e.GetPosition(fndHost);
        _fMidOx = _fOx; _fMidOy = _fOy;
        fndHost.CaptureMouse();
        e.Handled = true;
    }

    private void FndHost_MouseUpAny(object sender, MouseButtonEventArgs e)
    {
        if (!_fMidPan || (e.ChangedButton != MouseButton.Middle && e.ChangedButton != MouseButton.Right)) return;
        _fMidPan = false;
        fndHost.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void FndHost_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_fMidPan) return;
        if (e.MiddleButton != MouseButtonState.Pressed && e.RightButton != MouseButtonState.Pressed) { _fMidPan = false; fndHost.ReleaseMouseCapture(); return; }
        var p = e.GetPosition(fndHost);
        _fOx = _fMidOx + p.X - _fMidStart.X; _fOy = _fMidOy + p.Y - _fMidStart.Y;
        RedrawFoundationPlan();
        e.Handled = true;
    }

    private void FndHost_KeyDown(object sender, KeyEventArgs e)
    {
        if (IsTextInputFocused()) return;
        if (FndToolKey(e.Key)) { e.Handled = true; return; }   // [MóngHìnhDạng] Enter / Esc / Backspace khi đang vẽ
        switch (e.Key)
        {
            case Key.Delete: FndDeleteSelected(); e.Handled = true; break;
            case Key.E: case Key.Enter: FndEditSelected(); e.Handled = true; break;
            case Key.F: case Key.Home: BtnFndFit_Click(this, new RoutedEventArgs()); e.Handled = true; break;
            case Key.Escape when _fndMulti.Count > 0: _fndMulti.Clear(); RedrawFoundationPlan(); SetStatus("Đã bỏ chọn nhiều móng."); e.Handled = true; break;
            case Key.A when (Keyboard.Modifiers & ModifierKeys.Control) != 0: FndSelectAllSameType(); e.Handled = true; break;
        }
    }
}

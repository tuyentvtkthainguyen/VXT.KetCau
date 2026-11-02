using System.Windows;
using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Tab "Mô hình móng" (tiếp): nút "Bố trí cọc…" (chọn số cọc / sơ đồ / kích thước đài cho đài đang chọn, kiểm tra trực tiếp, áp dụng có hoàn tác)
/// và "Kiểm tra an toàn…" (bảng kiểm tra + sự làm việc của móng đang chọn: đài cọc, móng đơn, móng băng).</summary>
public partial class MainWindow
{
    private PileLayoutDialog _pileDlg;

    /// <summary>Đài cọc đang chọn trên mặt bằng móng; nếu chưa chọn: đài đầu tiên.</summary>
    private PileCapDesign SelectedPileCap()
    {
        if (SelectedFndItem() is PileCapDesign pc) return pc;
        return SectionItem(out _) as PileCapDesign ?? DesignCaps().FirstOrDefault();
    }

    private async void BtnPileLayout_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null || _busy) return;
        var cap = SelectedPileCap();
        if (cap == null)
        {
            if (_p.Foundation.Type != FoundationType.Pile && !_p.Footings.Any(f => f.Type == FoundationType.Pile))
            {
                MessageBox.Show(this, "Chưa có đài cọc. Chọn 'Loại móng: Móng cọc' (cả công trình hoặc tại chân cột) rồi bấm 'Thiết kế móng'.", AppTitle,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!await DesignFoundation()) return;
            cap = SelectedPileCap();
            if (cap == null) { SetStatus("Không có đài cọc nào được thiết kế."); return; }
        }
        try { OpenPileLayout(cap); }
        catch (Exception ex) { ShowError("Lỗi hộp thoại bố trí cọc", ex); }
    }

    /// <summary>Mở (không chặn) hộp thoại bố trí cọc cho đài cap.</summary>
    private PileLayoutDialog OpenPileLayout(PileCapDesign cap)
    {
        try { _pileDlg?.Close(); } catch (InvalidOperationException) { }
        var dlg = new PileLayoutDialog(_p, () => _res, cap, ApplyPileLayout) { Owner = this };
        dlg.Closed += (s, e) => { if (ReferenceEquals(_pileDlg, dlg)) _pileDlg = null; };
        _pileDlg = dlg;
        dlg.Show();
        return dlg;
    }

    /// <summary>Ghi bố trí cọc (ovr = null: bỏ bố trí riêng) cho các nút chân cột, có hoàn tác, rồi thiết kế lại móng và vẽ lại.</summary>
    private async Task ApplyPileLayout(List<int> nodes, PileCapOverride ovr)
    {
        if (_p == null || _busy || nodes == null || nodes.Count == 0) return;
        string snap = Snapshot();
        PileLayout.Set(_p.Foundation, nodes, ovr);
        PushUndo(snap);
        MarkDirty(false);
        int node = nodes[0];
        string what = ovr == null ? "tự chọn" : PileLayout.Name(ovr.Pattern) + (ovr.Pattern == PilePattern.Grid ? $" {ovr.Nx}×{ovr.Ny}" : "");
        if (!await DesignFoundation()) { SetStatus($"Đã lưu bố trí cọc ({what}) – cần thiết kế lại móng."); return; }
        var cap = DesignCaps().FirstOrDefault(c => c.NodeIds.Contains(node));
        _fndSelItem = cap;
        Select(footingNode: node);
        RedrawFoundationPlan();
        SetStatus(cap == null ? $"Bố trí cọc ({what}) đã áp dụng." :
            $"Bố trí cọc {cap.Name} ({what}): {cap.Piles.Count} cọc, đài {cap.L:0.00}×{cap.B:0.00}×{cap.H:0.00} m, Pmax = {cap.PMax:0} kN – {(cap.Ok ? "ĐẠT" : "KHÔNG ĐẠT")} (Hoàn tác để trở lại).");
    }

    private void BtnFndSafety_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null) return;
        object item = SelectedFndItem() ?? SectionItem(out _);
        if (item == null) { SetStatus("Chưa có kết quả thiết kế móng – bấm 'Thiết kế móng'."); return; }
        var (title, rep, msgs) = item switch
        {
            PileCapDesign pc => ("Kiểm tra an toàn đài cọc " + pc.Name, pc.Safety, pc.Messages),
            StripFootingDesign sf => ("Sự làm việc móng băng " + sf.Name + " (" + sf.Grid + ")", sf.Safety, sf.Messages),
            FootingDesign fd => ("Sự làm việc móng đơn " + fd.Name, fd.Safety, fd.Messages),
            _ => ("Kiểm tra móng", (FoundationCheckReport)null, new List<string>())
        };
        try { FoundationSafetyView.ShowWindow(this, title, rep, msgs.Where(m => m.StartsWith("LỖI") || m.StartsWith("Cảnh báo"))); }
        catch (Exception ex) { ShowError("Lỗi bảng kiểm tra móng", ex); }
    }

    /// <summary>Vài dòng tóm tắt phân loại / cảnh báo của móng tại nút (khung thông tin dưới cùng).</summary>
    private string FoundationSafetySummary(int nodeId)
    {
        if (_design == null) return "";
        var sb = new System.Text.StringBuilder();
        void Add(FoundationCheckReport r)
        {
            if (r == null) return;
            if (!string.IsNullOrEmpty(r.Classification)) sb.AppendLine("Sự làm việc: " + r.Classification + (r.LambdaL > 0 ? $" (λL = {r.LambdaL:0.00})" : ""));
            int nf = r.Items.Count(i => i.Status == CheckStatus.Fail), nw = r.Items.Count(i => i.Status == CheckStatus.Warning);
            sb.AppendLine($"Kiểm tra an toàn: {r.Items.Count} hạng mục, {nf} không đạt, {nw} cảnh báo (nút 'Kiểm tra an toàn…')");
        }
        Add(_design.PileCaps?.FirstOrDefault(c => c.NodeIds.Contains(nodeId))?.Safety);
        Add(_design.StripFootings?.FirstOrDefault(c => c.NodeIds.Contains(nodeId))?.Safety);
        Add(_design.Footings?.FirstOrDefault(c => c.NodeId == nodeId)?.Safety);
        // [MóngLệchTâm] phương án móng lệch tâm tại chân cột (nút "Lệch tâm…")
        var ecc = _design.Footings?.FirstOrDefault(f => EccHasNode(f, nodeId))?.Eccentric ?? _design.PileCaps?.FirstOrDefault(c => c.NodeIds.Contains(nodeId))?.Eccentric;
        if (ecc != null)
            sb.AppendLine($"Lệch tâm ({(string.IsNullOrEmpty(ecc.Sides) ? "ranh giới" : ecc.Sides)}): {EccentricOverride.SchemeName(ecc.Scheme)}, ex = {ecc.OffsetX * 1000:0}, ey = {ecc.OffsetY * 1000:0} mm (nút 'Lệch tâm…')");
        return sb.ToString();
    }
}

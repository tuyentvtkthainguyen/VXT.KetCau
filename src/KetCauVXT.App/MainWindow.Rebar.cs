using System.IO;
using System.Windows;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;
using RebarShopDrawing = KetCauVXT.Drawing.RebarShopDrawing;
using RebarShopReport = KetCauVXT.Report.RebarShopReport;

namespace KetCauVXT.App;

/// <summary>Thiết kế → "Chọn bố trí thép…" (hộp thoại RebarChoiceDialog cho dầm/cột/sàn/móng đang chọn), tuỳ chọn chọn thép,
/// Xuất → "Xuất shop drawing thép…" (bảng gia công DXF theo loại cấu kiện + sơ đồ cắt + Excel "Gia công thép").</summary>
public partial class MainWindow
{
    // ------------------------------------------------------------------
    // Chọn bố trí thép
    // ------------------------------------------------------------------

    /// <summary>Loại cấu kiện theo bảng thiết kế đang hiển thị.</summary>
    private string CurrentDesignKind()
    {
        if (dgColumns.IsVisible) return "Cột";
        if (dgSlabs.IsVisible) return "Sàn";
        if (dgFootings.IsVisible) return "Móng";
        return "Dầm";
    }

    /// <summary>Cấu kiện đã thiết kế ứng với dòng đang chọn trong bảng (không chọn → cấu kiện đầu tiên).</summary>
    private MemberDesignBase SelectedDesignMember(string kind)
    {
        if (_design == null) return null;
        switch (kind)
        {
            case "Cột":
            {
                string n = (dgColumns.SelectedItem as ColumnDesignRow)?.Name;
                return _design.Columns.FirstOrDefault(c => c.Name == n) ?? _design.Columns.FirstOrDefault();
            }
            case "Sàn":
            {
                string n = (dgSlabs.SelectedItem as SlabDesignRow)?.Name;
                return _design.Slabs.FirstOrDefault(c => c.Name == n) ?? _design.Slabs.FirstOrDefault();
            }
            case "Móng":
            {
                string n = (dgFootings.SelectedItem as FootingDesignRow)?.Name;
                return _design.Footings.FirstOrDefault(c => c.Name == n) ?? (MemberDesignBase)_design.Footings.FirstOrDefault()
                       ?? _design.PileCaps.FirstOrDefault() ?? (MemberDesignBase)_design.StripFootings.FirstOrDefault();
            }
            default:
            {
                string n = (dgBeams.SelectedItem as BeamDesignRow)?.Name;
                return _design.Beams.FirstOrDefault(c => c.Name == n) ?? _design.Beams.FirstOrDefault();
            }
        }
    }

    /// <summary>Thiết kế hiện tại chứa cấu kiện vật lý có khoá key (tên có thể đổi sau khi thiết kế lại).</summary>
    private MemberDesignBase DesignByRebarKey(string key) =>
        string.IsNullOrEmpty(key) || _design == null ? null : _design.All.FirstOrDefault(m => m.RebarKeys.Contains(key));

    /// <summary>Tạo hộp thoại chọn thép cho cấu kiện m (chưa hiển thị).</summary>
    private RebarChoiceDialog CreateRebarDialog(MemberDesignBase m, IList<MemberDesignBase> pick = null)
    {
        string key = m.RebarKeys.FirstOrDefault() ?? "";
        var dlg = new RebarChoiceDialog(
            () => DesignByRebarKey(key),
            mm => RebarOptions.Zones(_p, mm),
            (mm, z) => RebarOptions.Alternatives(_p, _res, mm, z),
            async (z, a) => await ApplyRebarChoice(key, z, a),
            async z => await ApplyRebarChoice(key, z, null, clearAll: z == null))
        { Owner = this };
        // chọn cấu kiện khác cùng nhóm (móng đơn / đài cọc / móng băng) ngay trong hộp thoại
        if (pick != null && pick.Count > 1)
            dlg.SetMemberPicker(pick.Where(x => x.RebarKeys.Count > 0).Select(x => (FoundationLabel(x), x.RebarKeys[0])).ToList(), key, k => key = k);
        return dlg;
    }

    private static string FoundationLabel(MemberDesignBase x) => (x switch
    {
        KetCauVXT.Design.Foundation.PileCapDesign => "Đài cọc " + x.Name,
        KetCauVXT.Design.Foundation.StripFootingDesign => "Móng băng " + x.Name,
        FootingDesign => "Móng đơn " + x.Name,
        _ => x.Name
    }) + (x.Quantity > 1 ? " (×" + x.Quantity + ")" : "");

    private async void BtnRebarChoice_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _p == null) return;
        string kind = (sender as FrameworkElement)?.Tag as string ?? CurrentDesignKind();
        if (!await EnsureDesign()) return;
        var m = SelectedDesignMember(kind);
        if (m == null || m.RebarKeys.Count == 0)
        {
            MessageBox.Show($"Chưa có {kind.ToLowerInvariant()} BTCT đã thiết kế để chọn thép.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var pick = kind == "Móng"
            ? _design.Footings.Cast<MemberDesignBase>().Concat(_design.PileCaps).Concat(_design.StripFootings).Where(x => x.RebarKeys.Count > 0).ToList()
            : null;
        try { CreateRebarDialog(m, pick).ShowDialog(); }
        catch (Exception ex) { ShowError("Lỗi chọn bố trí thép", ex); }
    }

    /// <summary>Ghi (a ≠ null) hoặc xoá (a = null) lựa chọn thép cho cấu kiện có khoá key, có hoàn tác, rồi thiết kế lại BTCT.</summary>
    private async Task ApplyRebarChoice(string key, RebarZoneInfo z, RebarAlternative a, bool clearAll = false)
    {
        if (_busy) return;
        var m = DesignByRebarKey(key);
        if (m == null) return;
        string snap = Snapshot();
        int n = a != null ? RebarOptions.Apply(_p, m, z, a) : RebarOptions.Clear(_p, m, clearAll ? null : z);
        if (n == 0) { SetStatus("Không có lựa chọn thép nào thay đổi."); return; }
        PushUndo(snap);
        MarkDirty(false);
        string what = a != null ? $"{m.Name}: {z?.Label} → {a.Text}" : $"{m.Name}: bỏ chọn {(clearAll ? "mọi vùng" : z?.Label)} (tự chọn thép)";
        await RedesignConcrete(what);
    }

    /// <summary>Thiết kế lại BTCT với kết quả phân tích hiện có (giữ kết quả kiểm tra thép hình).</summary>
    private async Task<bool> RedesignConcrete(string why)
    {
        if (_res == null) { SetStatus(why + " – cần chạy phân tích + thiết kế lại."); return false; }
        var res = _res;
        var start = new DesignSummary();
        if (_design != null) start.Steel.AddRange(_design.Steel);
        DesignSummary ds = null;
        bool ok = await RunBusy("Lỗi thiết kế lại BTCT", prog =>
        {
            prog.Report("Thiết kế lại BTCT theo lựa chọn thép...");
            ds = ConcreteDesigner.Design(_p, res, start);
        });
        if (!ok || ds == null) return false;
        _design = ds;
        RefreshDesign();
        int bad = ds.All.Count(x => !x.Ok);
        SetStatus($"{why} – đã thiết kế lại: {bad} cấu kiện không đạt.");
        return true;
    }

    private async void MnuRebarPrefs_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _p == null) return;
        var dlg = new RebarPrefsDialog(_p.RebarPrefs) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result == null) return;
        string snap = Snapshot();
        _p.RebarPrefs = dlg.Result;
        PushUndo(snap);
        MarkDirty(false);
        if (_design != null && _res != null) await RedesignConcrete("Đã đổi tuỳ chọn chọn thép");
        else SetStatus("Đã lưu tuỳ chọn chọn thép & gia công – áp dụng ở lần thiết kế sau.");
    }

    // ------------------------------------------------------------------
    // Shop drawing / gia công thép
    // ------------------------------------------------------------------

    /// <summary>Ghi bộ gia công thép vào thư mục: KC-07.x DXF theo loại cấu kiện, KC-07 phương án cắt, GiaCongThep.xlsx. Trả về số file.</summary>
    private int ExportRebarShop(DesignSummary ds, string folder)
    {
        Directory.CreateDirectory(folder);
        var files = RebarShopDrawing.ExportAll(_p, ds, folder);
        var shop = RebarShopReport.Export(_p, ds, Path.Combine(folder, "GiaCongThep.xlsx"));
        _lastShopSummary = $"{shop.Bars.Count} dòng gia công, {shop.Plans.Sum(x => x.StockCount)} cây thép, {shop.TotalWeightKg:0.0} kg"
                           + (shop.Plans.Count > 0 ? $", hao hụt {shop.Plans.Sum(x => x.WasteM):0.00} m" : "");
        return files.Count + 1;
    }

    private string _lastShopSummary = "";

    private async void MnuExpShop_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !await EnsureDesign()) return;
        if (!_design.All.Any(m => m.Bars.Count > 0))
        {
            MessageBox.Show("Chưa có cấu kiện BTCT có bố trí thép.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        string folder = AskFolder("Chọn thư mục lưu bản vẽ gia công thép (shop drawing)");
        if (folder == null) return;
        var ds = _design;
        int n = 0;
        if (await RunBusy("Lỗi xuất shop drawing thép", prog => { prog.Report("Đang triển khai thép gia công..."); n = ExportRebarShop(ds, folder); }))
            Done($"Đã xuất {n} file gia công thép (DXF + Excel \"Gia công thép\") vào\n{folder}\n{_lastShopSummary}");
    }
}

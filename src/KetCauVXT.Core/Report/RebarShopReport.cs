using KetCauVXT.Design;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>Bảng Excel "Gia công thép": danh sách cắt (số hiệu gia công, mã hình, kích thước, chiều dài cắt, số lượng, khối lượng),
/// phương án cắt cây thép thương phẩm theo Ø (mẫu cắt, số cây, đầu thừa, hao hụt) và tổng hợp theo Ø / nhóm Ø.</summary>
public static class RebarShopReport
{
    /// <summary>Ghi file Excel chỉ gồm trang "Gia công thép".</summary>
    public static ShopResult Export(Project p, DesignSummary s, string path, ShopResult shop = null)
    {
        shop ??= RebarShop.Build(p, s, ShopOptions.From(p?.RebarPrefs));
        var x = new XlsxWriter();
        AddSheet(p, shop, x.AddSheet("Gia công thép"));
        x.Save(path);
        return shop;
    }

    /// <summary>Điền trang tính gia công thép.</summary>
    public static void AddSheet(Project p, ShopResult shop, XlsxSheet sh)
    {
        sh.FirstRowIsHeader = false;
        sh.FreezeRows = 0;
        sh.SetColumnWidths(12, 9, 9, 8, 18, 30, 7, 12, 10, 12, 9, 10, 10, 13, 13, 36);
        sh.AddRow(new XlCell("BẢNG GIA CÔNG CỐT THÉP – " + (p?.Name ?? ""), XlStyle.Title));
        sh.AddRow("Cây thép thương phẩm " + (shop.Options.StockLength * 1000).ToString("0", DrawUtil.Inv) + " mm; đầu thừa ≥ "
                  + (shop.Options.MinReuseOffcut * 1000).ToString("0", DrawUtil.Inv) + " mm tái sử dụng; nối chồng theo TCVN 5574:2018 mục 10.3.6"
                  + (shop.Options.Stagger ? ", so le ≤ 50% số thanh tại một tiết diện" : ""));
        sh.AddEmptyRow();
        sh.AddBoldRow("1. DANH SÁCH CẮT – UỐN");
        sh.AddHeaderRow("Loại", "Cấu kiện", "Số hiệu CT", "Số hiệu GC", "Hình dạng (mã)", "Kích thước ngoài các đoạn (mm)", "Ø (mm)", "Tổng kích thước ngoài (mm)",
            "Trừ dãn khi uốn (mm)", "Chiều dài cắt (mm)", "Số CK", "Thanh / 1 CK", "Tổng số thanh", "Tổng chiều dài cắt (m)", "Khối lượng (kg)", "Ghi chú");
        if (shop.Bars.Count == 0) { sh.AddRow("Chưa có dữ liệu bố trí thép (chạy thiết kế trước)."); return; }
        foreach (var b in shop.Bars)
            sh.AddRow(b.Category, b.MemberLabel, b.ParentMark, b.Label, b.ShapeCode + " " + b.ShapeName,
                string.Join(" × ", b.Segments.Select(DxfWriter.N0)) + (b.HookMm > 0 ? " + 2 móc " + DxfWriter.N0(b.HookMm) : ""),
                b.Dia, b.OutsideLengthMm, b.BendDeductionMm, b.CutLengthMm, b.MemberCount, b.BarsPerMember, b.BarsTotal, b.TotalLengthM, b.WeightKg,
                string.IsNullOrEmpty(b.Note) ? b.Role : b.Role + "; " + b.Note);
        sh.AddBoldRow("TỔNG CỘNG", null, null, null, null, null, null, null, null, null, null, null, shop.Bars.Sum(b => b.BarsTotal),
            Math.Round(shop.Bars.Sum(b => b.TotalLengthM), 2), shop.TotalWeightKg);

        sh.AddEmptyRow();
        sh.AddBoldRow("2. PHƯƠNG ÁN CẮT CÂY THÉP (first-fit decreasing)");
        sh.AddHeaderRow("Ø (mm)", "Mẫu cắt", "Số cây", "Số đoạn / cây", "Các đoạn cắt (số hiệu GC: chiều dài mm)", null, null, "Dùng (mm)", "Thừa (mm)", "Thừa dùng lại");
        foreach (var pl in shop.Plans)
        {
            int k = 1;
            foreach (var pt in pl.Patterns())
            {
                double used = pt.Pieces.Sum(q => q.Mm);
                sh.AddRow(pl.Dia, "M" + k++, pt.Count, pt.Pieces.Count, pt.Text, null, null, used, Math.Round(pt.LeftoverMm), pt.LeftoverMm > 1 ? (pt.Reusable ? "có" : "hao hụt") : "");
            }
            foreach (var (label, mm) in pl.Oversize)
                sh.AddRow(pl.Dia, "Riêng", 1, 1, label + ": " + DxfWriter.N0(mm) + " mm > cây thương phẩm – đặt cây dài riêng");
        }

        sh.AddEmptyRow();
        sh.AddBoldRow("3. TỔNG HỢP THEO ĐƯỜNG KÍNH");
        sh.AddHeaderRow("Ø (mm)", "Số cây", "Chiều dài cần (m)", "Chiều dài cây (m)", "Thừa dùng lại (m)", "Hao hụt (m)", "Hao hụt (%)", "KL gia công (kg)", "KL thép cây (kg)");
        foreach (var pl in shop.Plans)
            sh.AddRow(pl.Dia, pl.StockCount, pl.PieceLengthM, pl.StockLengthM, pl.ReusableM, pl.WasteM, pl.WastePct, pl.PieceWeightKg, pl.StockWeightKg);
        double st = shop.Plans.Sum(q => q.StockLengthM), ws = shop.Plans.Sum(q => q.WasteM);
        sh.AddBoldRow("CỘNG", shop.Plans.Sum(q => q.StockCount), Math.Round(shop.Plans.Sum(q => q.PieceLengthM), 2), Math.Round(st, 2),
            Math.Round(shop.Plans.Sum(q => q.ReusableM), 2), Math.Round(ws, 2), st > 0 ? Math.Round(ws / st * 100, 2) : 0,
            Math.Round(shop.Plans.Sum(q => q.PieceWeightKg), 2), Math.Round(shop.Plans.Sum(q => q.StockWeightKg), 2));

        sh.AddEmptyRow();
        sh.AddBoldRow("4. TỔNG HỢP THEO NHÓM ĐƯỜNG KÍNH (khối lượng gia công)");
        sh.AddHeaderRow("Nhóm", "Khối lượng (kg)", "Khối lượng (tấn)");
        double g1 = shop.Bars.Where(b => b.Dia <= 10).Sum(b => b.WeightKg);
        double g2 = shop.Bars.Where(b => b.Dia > 10 && b.Dia <= 18).Sum(b => b.WeightKg);
        double g3 = shop.Bars.Where(b => b.Dia > 18).Sum(b => b.WeightKg);
        sh.AddRow("Ø ≤ 10", Math.Round(g1, 2), g1 / 1000);
        sh.AddRow("10 < Ø ≤ 18", Math.Round(g2, 2), g2 / 1000);
        sh.AddRow("Ø > 18", Math.Round(g3, 2), g3 / 1000);
        sh.AddBoldRow("TỔNG CỘNG", Math.Round(g1 + g2 + g3, 2), (g1 + g2 + g3) / 1000);

        sh.AddEmptyRow();
        sh.AddBoldRow("5. MỐI NỐI THÉP (nguyên tắc: không nối trong nút khung, vùng tới hạn, vùng mô men lớn; so le ≤ 50%)");
        sh.AddHeaderRow("Cấu kiện", "Số hiệu", "Ø (mm)", "Nhóm", "Số thanh", "Tâm mối nối (m)", "Chiều dài nối (mm)", "Vùng cho phép (m)", "Kiểu", "Đánh giá");
        foreach (var sp in shop.Splices)
            sh.AddRow(sp.Member, sp.Mark, sp.Dia, sp.Group, sp.Count, Math.Round(sp.Center, 3), Math.Round(sp.Lap * 1000),
                double.IsNaN(sp.ZoneA) ? "–" : sp.ZoneA.ToString("0.00", DrawUtil.Inv) + "–" + sp.ZoneB.ToString("0.00", DrawUtil.Inv),
                sp.Coupler ? "ống nối ren" : "nối chồng", sp.Violation ? new XlCell("VI PHẠM – vùng cấm", XlStyle.Fail) : (object)"đạt");
        if (shop.Messages.Count > 0)
        {
            sh.AddEmptyRow();
            sh.AddBoldRow("6. GHI CHÚ / CẢNH BÁO (vi phạm, thoả hiệp, đề xuất)");
            foreach (var m in shop.Messages.Distinct()) sh.AddRow(m);
        }
    }
}

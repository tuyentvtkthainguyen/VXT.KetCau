using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Hoàn thiện bản vẽ triển khai: chân gióng kích thước gọn, mặt cắt dầm không thép sàn,
/// bảng thống kê vật liệu (thép, bê tông, ván khuôn), chi tiết cầu thang bộ, định vị dầm lệch trục.</summary>
public static class DrawingFinishTests
{
    static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        var def = DrawingStyle.Default();
        check("Mặc định: chân gióng cố định 4 mm", def.DimFixedExtLength, 4.0, 1e-9);
        check("Mặc định: mặt cắt dầm không vẽ thép sàn", B(!def.ShowSlabRebarInFlange), 1, 0);

        // ---- 1. chân gióng: đoạn gióng chỉ dài FixedExt + ExtBeyond (mm giấy) dù đường kích thước đặt xa
        using (DrawingStyle.Use(def))
        {
            var w = new DxfWriter(50);
            w.Dim(0, 0, 1000, 0, -2000);
            var ext = w.GetPrimitives().Where(pr => pr.Kind == PrimKind.Line && pr.Layer == Lay.KichThuoc
                                                    && Math.Abs(pr.Pts[0] - pr.Pts[2]) < 1e-6).ToList();
            check("Chân gióng: có 2 đường gióng", B(ext.Count >= 2), 1, 0);
            double topY = ext.Count > 0 ? ext.Max(pr => Math.Max(pr.Pts[1], pr.Pts[3])) : 0;
            double legLen = ext.Count > 0 ? ext.Max(pr => Math.Abs(pr.Pts[1] - pr.Pts[3])) : 0;
            check("Chân gióng: bắt đầu cách đường kích thước 4 mm giấy (không kéo về điểm đo)", topY, -(2000 - 4.0 * 50), 1);
            double expLeg = (def.DimFixedExtLength + def.DimExtBeyond) * 50;
            check("Chân gióng: độ dài = cố định + vượt", legLen, expLeg, 1);

            var s0 = def.Clone(); s0.DimFixedExtLength = 0;
            var w0 = new DxfWriter(50) { Style = s0 };
            w0.Dim(0, 0, 1000, 0, -2000);
            double topY0 = w0.GetPrimitives().Where(pr => pr.Kind == PrimKind.Line && pr.Layer == Lay.KichThuoc && Math.Abs(pr.Pts[0] - pr.Pts[2]) < 1e-6)
                             .Select(pr => Math.Max(pr.Pts[1], pr.Pts[3])).DefaultIfEmpty(-9999).Max();
            check("Chân gióng = 0: kéo tới sát điểm đo (như cũ)", B(topY0 > -200), 1, 0);
        }

        // ---- 2. mặt cắt dầm: chỉ cấu tạo dầm (không ghi thép sàn)
        var s = new DetailSettings();
        using (DrawingStyle.Use(def))
        {
            var mb = new ManualBeam { MSpan = 45, MSupport = 60, Q = 70 };
            var rb = DetailingProject.RunSafe(mb, s);
            var figs = mb.Draw(rb, s);
            var texts = figs.SelectMany(f => f.W.Texts()).ToList();
            check("Mặt cắt dầm mặc định: có hình vẽ", B(figs.Count > 0), 1, 0);
            check("Mặt cắt dầm mặc định: không ghi thép sàn \"(sàn)\"", texts.Count(t => t.TrimEnd().EndsWith("(sàn)")), 0, 0);
        }

        // ---- 3. cầu thang bộ CT1
        var stair = ManualStair.Sample();
        check("Cầu thang mẫu: tên CT1", B(stair.Name == "CT1"), 1, 0);
        var rs = DetailingProject.RunSafe(stair, s);
        check("Cầu thang: tính không lỗi", B(!rs.Messages.Any(m => m.StartsWith("LỖI"))), 1, 0);
        check("Cầu thang: loại cấu kiện", B(rs.Kind == "Cầu thang bộ"), 1, 0);
        check("Cầu thang: có thép", B(rs.Bars.Count > 0 && rs.SteelWeight > 0), 1, 0);
        check("Cầu thang: thể tích bê tông > 0", B(rs.ConcreteVolume > 0), 1, 0);
        check("Cầu thang: ván khuôn > 0", B(rs.FormworkArea > 0), 1, 0);
        check("Cầu thang: số hiệu thép không trùng", rs.Bars.Select(b => b.Mark).Distinct().Count(), rs.Bars.Count, 0);
        check("Cầu thang: khối lượng xây bậc gạch", B(rs.Extra.Any(e => e.Item.Contains("bậc", StringComparison.OrdinalIgnoreCase))), 1, 0);
        List<Figure> sfigs;
        using (DrawingStyle.Use(def)) sfigs = stair.Draw(rs, s);
        check("Cầu thang: ≥ 2 hình (mặt bằng + mặt cắt/chi tiết)", B(sfigs.Count >= 2), 1, 0);
        var bad = new ManualStair { FloorHeight = 0 };
        var rbad = DetailingProject.RunSafe(bad, s);
        check("Cầu thang sai số liệu: báo lỗi, không ném ngoại lệ", B(rbad != null), 1, 0);

        Directory.CreateDirectory(Path.Combine(outDir, "TrienKhai"));
        if (sfigs.Count > 0)
        {
            var (sc, paper) = DetailingExport.SheetFor(sfigs);
            string f = Path.Combine(outDir, "TrienKhai", "CauThang.dxf");
            SheetComposer.Compose(sfigs, sc, new SheetComposer.SheetInfo { ProjectName = "Kiểm thử cầu thang", Title = "CHI TIẾT CẦU THANG BỘ CT1", SheetNo = "KC-CT" }, paper).Save(f);
            check("Cầu thang: xuất DXF", B(File.Exists(f) && new FileInfo(f).Length > 1000), 1, 0);
        }

        // ---- 4. bảng thống kê vật liệu
        var dp = DetailingProject.Sample();
        var results = dp.RunAll();
        var rows = MaterialTable.Rows(results);
        var total = rows.LastOrDefault(r => r.Kind == "total");
        check("Thống kê vật liệu: có dòng tổng cộng", B(total != null), 1, 0);
        if (total != null)
        {
            double conc = results.Sum(x => x.Result.ConcreteVolume * Math.Max(1, x.Result.Quantity));
            double concRows = rows.Where(r => r.Kind == "row").Sum(r => r.Conc);
            check("Thống kê vật liệu: tổng bê tông = cộng các dòng", total.Conc, concRows, 1e-6);
            check("Thống kê vật liệu: tổng bê tông = Σ 1CK × SL", total.Conc, conc, 1e-3);
            double form = rows.Where(r => r.Kind == "row").Sum(r => r.Form);
            check("Thống kê vật liệu: tổng ván khuôn = cộng các dòng", total.Form, form, 1e-6);
            double kg = DetailingExport.Quantities(dp, results).Where(q => q.Unit == "kg" && q.Item.Contains("Ø")).Sum(q => q.Qty);
            check("Thống kê vật liệu: tổng thép = bảng khối lượng", total.Steel, kg, Math.Max(0.5, kg * 0.005));
            check("Thống kê vật liệu: có thép 3 nhóm đường kính", B(total.S10 > 0 && total.S18 > 0), 1, 0);
        }
        check("Thống kê vật liệu: có nhóm cầu thang", B(rows.Any(r => r.Kind == "group" && r.Name.ToUpperInvariant().Contains("CẦU THANG"))), 1, 0);
        List<Figure> tfigs;
        using (DrawingStyle.Use(def)) tfigs = MaterialTable.Build(dp, results);
        var ttexts = tfigs.SelectMany(f => f.W.Texts()).ToList();
        check("Thống kê vật liệu: có bảng + tổng hợp", B(tfigs.Count >= 2 && tfigs.Any(f => f.Name == "THVL")), 1, 0);
        check("Thống kê vật liệu: tiêu đề bảng", B(ttexts.Any(t => t.Contains("BẢNG THỐNG KÊ VẬT LIỆU"))), 1, 0);
        check("Thống kê vật liệu: cột ván khuôn", B(ttexts.Any(t => t.Contains("VÁN KHUÔN"))), 1, 0);

        // nhiều cấu kiện → tách bảng (tiếp)
        var many = new List<(IDetailMember Member, DetailResult Result)>();
        for (int i = 0; i < 60; i++) many.Add(results[i % results.Count]);
        List<Figure> mfigs;
        using (DrawingStyle.Use(def)) mfigs = MaterialTable.Build(dp, many);
        check("Thống kê vật liệu: nhiều dòng → tách ≥ 2 bảng", B(mfigs.Count(f => f.Name.StartsWith("TKVL")) >= 2), 1, 0);

        if (tfigs.Count > 0)
        {
            var (sc, paper) = DetailingExport.SheetFor(tfigs);
            string f = Path.Combine(outDir, "TrienKhai", "ThongKeVatLieu.dxf");
            SheetComposer.Compose(tfigs, sc, new SheetComposer.SheetInfo { ProjectName = dp.ProjectName, Title = "BẢNG THỐNG KÊ VẬT LIỆU", SheetNo = "KC-TK" }, paper).Save(f);
            check("Thống kê vật liệu: xuất DXF", B(File.Exists(f) && new FileInfo(f).Length > 1000), 1, 0);
        }

        // ---- 5. định vị dầm lệch trục trên mặt bằng kết cấu (chế độ mô hình)
        var p = ModelBuilder.NewProject("Thử định vị dầm");
        ModelBuilder.SetGrid(p, new[] { 4.0 }, new[] { 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, 0.0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions());
        var top = p.Stories.OrderBy(x => x.Elevation).Last();
        using (DrawingStyle.Use(def))
        {
            var w1 = new DxfWriter(100);
            check("Định vị dầm: dầm trùng trục không thêm kích thước", BeamLocateDims.Add(w1, p, top), 0, 0);
            var er = ModelEdit.AddBeam(p, top.Name, 0, 2.0, 4.0, 2.0, "D22x30");
            check("Định vị dầm: thêm dầm phụ lệch trục", B(er.Ok), 1, 0);
            var w2 = new DxfWriter(100);
            check("Định vị dầm: dầm lệch trục có kích thước định vị", B(BeamLocateDims.Add(w2, p, top) > 0), 1, 0);
            var off = def.Clone(); off.ShowGridDims = false;
            using (DrawingStyle.Use(off))
            {
                var w3 = new DxfWriter(100);
                check("Định vị dầm: tắt kích thước lưới trục → không vẽ", BeamLocateDims.Add(w3, p, top), 0, 0);
            }
        }
    }
}

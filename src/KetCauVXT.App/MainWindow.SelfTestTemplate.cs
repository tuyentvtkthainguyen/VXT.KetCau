using System.IO;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Import;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Tự kiểm tra mẫu định dạng + khung tên người dùng: tạo mẫu từ dự án vào thư viện tạm (SelfTestOut\ThuVienMau),
/// mở cửa sổ mẫu định dạng (không chặn) → ảnh; lưu tờ làm mẫu / tạo tờ từ mẫu; nhập khung tên từ DXF tạo sẵn (khung + chữ {TEN_CONG_TRINH}, {SO_HIEU}…)
/// vào một tờ thử → ảnh cửa sổ dàn trang; xuất DXF tờ đó; trả lại khung tên + danh sách tờ như trước.</summary>
public partial class MainWindow
{
    /// <summary>DXF khung tên 180×40 mm có chỗ trống trường – ghi bằng DxfWriter của chương trình.</summary>
    private static void WriteSelfTestTitleDxf(string path)
    {
        var w = new DxfWriter(1);
        const string L = "KHUNG";
        w.Polyline(L, new List<(double X, double Y)> { (0, 0), (180, 0), (180, 40), (0, 40) }, true, 0.35);
        for (int i = 1; i < 5; i++) w.Line(L, 0, i * 8, i == 1 || i == 2 ? 125 : 180, i * 8);
        w.Line(L, 30, 0, 30, 40);
        w.Line(L, 125, 0, 125, 24);
        w.Text(L, 2, 36, 2, "ĐƠN VỊ TƯ VẤN", 0, 0, 2);
        w.Text(L, 32, 36, 3, "{CONG_TY}", 0, 0, 2);
        w.Text(L, 2, 28, 2, "CÔNG TRÌNH", 0, 0, 2);
        w.Text(L, 32, 28, 3.2, "{TEN_CONG_TRINH}", 0, 0, 2);
        w.Text(L, 2, 20, 2, "BẢN VẼ", 0, 0, 2);
        w.Text(L, 32, 20, 3, "{TEN_BAN_VE}", 0, 0, 2);
        w.Text(L, 2, 12, 2, "CHỦ TRÌ", 0, 0, 2);
        w.Text(L, 32, 12, 2.5, "{CHU_TRI}", 0, 0, 2);
        w.Text(L, 2, 4, 2, "THIẾT KẾ", 0, 0, 2);
        w.Text(L, 32, 4, 2.5, "{THIET_KE}", 0, 0, 2);
        w.Text(L, 127, 20, 2.2, "TỶ LỆ: {TY_LE}", 0, 0, 2);
        w.Text(L, 127, 12, 2.2, "NGÀY: {NGAY}", 0, 0, 2);
        w.Text(L, 152.5, 4, 4.5, "{SO_HIEU}", 0, 1, 2);
        w.Text(L, 178, 36, 1.8, "Tờ {TRANG}/{TONG_TRANG}", 0, 2, 2);
        w.Save(path);
    }

    private async Task SelfTestTemplate(string tag)
    {
        string prevLib = TemplateService.LibraryFolderOverride;
        TemplateService.LibraryFolderOverride = Path.Combine(SelfTestOut, "ThuVienMau");
        var dsg = _design ?? (_res != null ? ConcreteDesigner.Design(_p, _res) : null);
        _p.TitleBlock ??= new TitleBlockInfo();
        var prevCustom = _p.TitleBlock.Custom;
        string prevLeader = _p.TitleBlock.Leader;
        var prevSheets = (_p.Sheets ?? new List<Sheet>()).ToList();
        try
        {
            // ---- 1. thư viện mẫu: tạo từ dự án, mở cửa sổ ----
            var tw = OpenTemplates(false);
            try
            {
                string path = tw.CreateFromProject("Mẫu tự kiểm tra – " + _p.Name, TemplateParts.All, "Tạo tự động khi tự kiểm tra");
                var pkg = TemplateService.Load(path);
                StLog($"mẫu định dạng: {Path.GetFileName(path)} – {TemplateService.PartsText(pkg.Parts)}, {pkg.SheetTemplates?.Count ?? 0} tờ mẫu");
                TemplateService.SetDefault(path);
                tw.Reload(path);
                await Pause(800);
                ShotWindow(tw, $"{tag}_29_Template");
            }
            finally { tw.Close(); }

            // ---- 2. khung tên người dùng từ DXF ----
            string dxf = Path.Combine(SelfTestOut, tag + "_KhungTenNguon.dxf");
            WriteSelfTestTitleDxf(dxf);
            var res = TitleBlockImporter.FromFile(dxf, new TitleBlockImportOptions { Name = "Khung tên tự kiểm tra" });
            StLog("khung tên người dùng: " + res.Info + (res.Warnings.Count > 0 ? " ! " + string.Join("; ", res.Warnings) : ""));
            if (res.Def.Entities.Count == 0 || res.FieldCount < 8) throw new Exception($"Nhập khung tên DXF sai: {res.Def.Entities.Count} thực thể, {res.FieldCount} trường");

            if (_p.Sheets == null || _p.Sheets.Count == 0) _p.Sheets = SheetBuilder.DefaultSet(_p, dsg, _res);
            var sw = new SheetWindow(_p, _res, dsg, () => MarkDirty(false)) { Owner = this, Width = 1500, Height = 900 };
            sw.Show();
            try
            {
                sw.SelectSheet(0);
                await Pause(600);
                _p.TitleBlock.Leader = string.IsNullOrEmpty(_p.TitleBlock.Leader) ? "KS. Chủ trì tự kiểm tra" : _p.TitleBlock.Leader;
                sw.SetCustomTitle(res.Def);

                // tờ mẫu: lưu tờ đầu làm mẫu → tạo tờ mới từ mẫu
                sw.SaveSheetAsTemplate("Tờ tự kiểm tra");
                var st = TemplateService.LibrarySheetTemplates().FirstOrDefault(x => x.Template.Name == "Tờ tự kiểm tra").Template;
                if (st == null) throw new Exception("Không thấy tờ mẫu vừa lưu");
                var s = sw.AddSheetFromTemplate(st);
                s.Name = "KHUNG TÊN NGƯỜI DÙNG";
                s.ShowFrame = true; s.ShowTitleBlock = true;
                StLog($"tờ từ mẫu: {s.Number} {s.Paper}, {s.Views.Count} ô khung nhìn ({string.Join(", ", s.Views.Select(v => SheetBuilder.KindName(v.Kind) + " " + v.Param))})");
                sw.RefreshPage();
                sw.SelectSheet(_p.Sheets.IndexOf(s));
                await Pause(1200);
                ShotWindow(sw, $"{tag}_30_KhungTenNguoiDung");

                string outDxf = Path.Combine(SelfTestOut, tag + "_KhungTenNguoiDung.dxf");
                SheetBuilder.ExportDxf(_p, _res, dsg, s, outDxf);
                var texts = DxfReader.Read(outDxf).Entities.Where(e => e.Type == DxfEntityType.Text).Select(e => e.Text).ToList();
                bool okName = texts.Contains(string.IsNullOrWhiteSpace(_p.TitleBlock.ProjectName) ? _p.Name : _p.TitleBlock.ProjectName.Trim());
                bool okNo = texts.Contains(s.Number);
                bool noPh = !texts.Any(t => TitleFields.HasPlaceholder(t));
                StLog($"khung tên người dùng: DXF {Path.GetFileName(outDxf)} – tên công trình {(okName ? "có" : "THIẾU")}, số hiệu {(okNo ? "có" : "THIẾU")}, chỗ trống {(noPh ? "đã thay hết" : "CÒN")}");
                if (!okName || !okNo || !noPh) throw new Exception("Khung tên người dùng: DXF chưa thay đúng trường");
            }
            finally
            {
                // trả lại như trước
                sw.SetCustomTitle(prevCustom);
                _p.Sheets = prevSheets;
                sw.Close();
            }
        }
        finally
        {
            _p.TitleBlock.Custom = prevCustom;
            _p.TitleBlock.Leader = prevLeader;
            _p.Sheets = prevSheets;
            TemplateService.SetDefault(null);
            TemplateService.LibraryFolderOverride = prevLib;
        }
    }
}

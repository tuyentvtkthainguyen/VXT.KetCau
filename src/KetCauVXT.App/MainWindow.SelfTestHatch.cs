using System.IO;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest): tô vật liệu mặt cắt (HATCH – Drawing/DxfWriter.Hatch.cs, KetCauVXT.Dwg/CadDocWriter.cs).
/// Bật "Tô vật liệu" tạm thời, xuất chi tiết dầm + chi tiết móng ra DXF R12 (mô phỏng), DXF 2018 và DWG 2018 (HATCH thật),
/// mở cửa sổ dàn trang (không modal) ở tờ có chi tiết dầm (mặt cắt có tô bê tông), chụp ảnh, ghi kích thước file / số thực thể.</summary>
public partial class MainWindow
{
    private async Task SelfTestHatch(string tag)
    {
        var dsg = _design ?? (_res != null ? ConcreteDesigner.Design(_p, _res) : null);
        if (dsg == null) { StLog("hatch: chưa có kết quả phân tích/thiết kế – bỏ qua"); return; }
        StLog("hatch: bộ ghi DXF 2018/DWG " + (CadFileWriters.HasWriter ? "đã đăng ký" : "CHƯA đăng ký (ghi R12)") +
              $"; định dạng xuất theo chuẩn: {CadOutputFormats.Label(CadFileWriters.Current)}");
        var st = DrawingStyle.Current.Clone();
        st.HatchSections = HatchMode.On;
        using var styleScope = DrawingStyle.Use(st);   // bật tô vật liệu cả bản R12 trong bước này

        // ---- chi tiết dầm + chi tiết móng → R12 / 2018 / DWG ----
        var beam = dsg.Beams.FirstOrDefault(b => b.Bars.Count > 0);
        var groups = new List<(string Name, string Title, List<Figure> Figs)>();
        if (beam != null) groups.Add(("ChiTietDam", "CHI TIẾT DẦM " + beam.Name + " – MẶT CẮT CÓ TÔ VẬT LIỆU", BeamDrawing.Build(_p, beam, dsg)));
        var foot = FootingDrawing.BuildAll(_p, dsg);
        if (foot.Count > 0) groups.Add(("ChiTietMong", "CHI TIẾT MÓNG – BÊ TÔNG, BÊ TÔNG LÓT, ĐẤT NỀN", foot.Take(6).ToList()));
        if (groups.Count == 0) StLog("hatch: không có dầm/móng để vẽ");
        foreach (var (name, title, figs) in groups)
        {
            if (figs.Count == 0) continue;
            double S = figs.Max(f => f.W.Scale);
            var w = SheetComposer.Compose(figs, S, new SheetComposer.SheetInfo
            {
                ProjectName = _p.Name, Title = title, SheetNo = "KC-HATCH", Designer = _p.Designer, Owner = _p.Owner, Location = _p.Location
            }, "A1");
            int nH = w.CountEntities("HATCH");
            var pats = w.Hatches().Select(h => h.Pattern).Distinct().ToList();
            string basePath = Path.Combine(SelfTestOut, $"{tag}_Hatch_{name}.dxf");
            foreach (var fmt in CadOutputFormats.All)
            {
                string file = fmt switch
                {
                    CadOutputFormat.Dxf2018 => Path.Combine(SelfTestOut, $"{tag}_Hatch_{name}_2018.dxf"),
                    CadOutputFormat.Dwg2018 => Path.Combine(SelfTestOut, $"{tag}_Hatch_{name}.dwg"),
                    _ => Path.Combine(SelfTestOut, $"{tag}_Hatch_{name}_R12.dxf")
                };
                try
                {
                    string outPath = CadFileWriters.Write(w, file, fmt);
                    long len = File.Exists(outPath) ? new FileInfo(outPath).Length : 0;
                    string ents = fmt == CadOutputFormat.DxfR12
                        ? $"{w.EntityCount - nH + w.HatchR12EntityCount()} thực thể R12 (HATCH mô phỏng → {w.HatchR12EntityCount()} LINE/SOLID)"
                        : $"{w.EntityCount} thực thể, {nH} HATCH";
                    StLog($"hatch {name} {CadOutputFormats.Label(fmt)}: {Path.GetFileName(outPath)} {len / 1024.0:0.0} KB, {ents}" + (len == 0 ? " – KHÔNG CÓ FILE" : ""));
                }
                catch (Exception ex) { StLog($"hatch {name} {CadOutputFormats.Label(fmt)}: LỖI {ex.Message}"); }
            }
            StLog($"hatch {name}: {nH} miền tô, mẫu {string.Join(", ", pats)} ({basePath})");
        }

        // ---- cửa sổ dàn trang (không modal): tờ có chi tiết dầm – mặt cắt có tô bê tông ----
        var oldSheets = _p.Sheets;
        bool tempSheets = false;
        try
        {
            if (_p.Sheets == null || _p.Sheets.Count == 0) { _p.Sheets = SheetBuilder.DefaultSet(_p, dsg, _res); tempSheets = true; }
            int idx = _p.Sheets.FindIndex(s => s.Views.Any(v => v.Kind == SheetViewKind.BeamDetail));
            if (idx < 0 && beam != null)
            {
                if (!tempSheets) { _p.Sheets = new List<Sheet>(_p.Sheets); tempSheets = true; }
                _p.Sheets.Add(new Sheet
                {
                    Number = "KC-HATCH", Name = "MẶT CẮT DẦM – TÔ VẬT LIỆU", Paper = "A3", Landscape = true,
                    Views = { new SheetView { Kind = SheetViewKind.BeamDetail, Param = beam.Name, Title = beam.Name, X = 30, Y = 40, W = 360, H = 230 } }
                });
                idx = _p.Sheets.Count - 1;
            }
            if (idx < 0) { StLog("hatch: không có tờ chi tiết dầm – bỏ qua ảnh"); return; }
            var sw = new SheetWindow(_p, _res, dsg, () => MarkDirty(false)) { Owner = this, Width = 1500, Height = 900 };
            sw.Show();
            try
            {
                sw.SelectSheet(idx);
                await Pause(1500);
                ShotWindow(sw, $"{tag}_56_Hatch");
                var composed = SheetBuilder.Compose(_p, _res, dsg, _p.Sheets[idx]);
                StLog($"hatch dàn trang {_p.Sheets[idx].Number}: {composed.CountEntities("HATCH")} miền tô trên tờ, {composed.EntityCount} thực thể");
            }
            finally { sw.Close(); }
        }
        finally { if (tempSheets) _p.Sheets = oldSheets; }
    }
}

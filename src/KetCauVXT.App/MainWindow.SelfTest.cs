using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Mở file .kcvxt từ dòng lệnh và chế độ tự kiểm tra giao diện:
/// KetCauVXT.exe --selftest &lt;file.kcvxt&gt; &lt;thư mục ảnh&gt; → mở, phân tích, thiết kế, chọn đối tượng, sửa mô hình,
/// mở lần lượt các khung nhìn và chụp ảnh; lỗi ghi vào selftest.log, mã thoát ≠ 0.</summary>
public partial class MainWindow
{
    internal static string SelfTestOut;
    private StreamWriter _stLog;

    private void HandleCommandLine()
    {
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        if (args.Length >= 3 && args[0] == "--selftest")
        {
            SelfTestOut = args[2];
            Dispatcher.BeginInvoke(new Action(async () => await RunSelfTest(args[1], args[2])), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            return;
        }
        InitAcadLink(true);                 // liên kết AutoCAD – MainWindow.AcadLink.cs
        if (HandleAcadArgs(args)) return;   // --acad "<file.json>"
        var file = args.FirstOrDefault(a => a.EndsWith(".kcvxt", StringComparison.OrdinalIgnoreCase) && File.Exists(a));
        if (file != null)
        {
            try { SetProject(Project.Load(file), file); SetStatus("Đã mở " + file); }
            catch (Exception ex) { ShowError("Không mở được " + file, ex); }
        }
    }

    private void StLog(string s) { _stLog?.WriteLine($"{DateTime.Now:HH:mm:ss} {s}"); _stLog?.Flush(); }

    private async Task Pause(int ms = 1200)
    {
        await Task.Delay(ms);
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void Shot(string name)
    {
        UpdateLayout();
        var src = (FrameworkElement)Content;
        int w = (int)Math.Max(1, src.ActualWidth), h = (int)Math.Max(1, src.ActualHeight);
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(src);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(Path.Combine(SelfTestOut, name + ".png"));
        enc.Save(fs);
        StLog("ảnh " + name);
    }

    private TabItem ViewTab(string header) => tabView.Items.OfType<TabItem>().First(t => (t.Header as string) == header);

    private async Task RunSelfTest(string file, string outDir)
    {
        int code = 0;
        Directory.CreateDirectory(outDir);
        _stLog = new StreamWriter(Path.Combine(outDir, "selftest.log"), true);
        try
        {
            WindowState = WindowState.Normal; Width = 1600; Height = 950;
            StLog("mở " + file);
            SetProject(Project.Load(file), file);
            await Pause();
            if (!await RunAnalysis()) throw new Exception("Phân tích lỗi");
            _design = ConcreteDesigner.Design(_p, _res);
            RefreshDesign();
            StLog($"phân tích + thiết kế: {_p.Frames.Count} thanh, {_design.All.Count()} cấu kiện");
            string tag = Path.GetFileNameWithoutExtension(file);
            var story = _p.Stories.OrderBy(s => s.Elevation).Skip(1).FirstOrDefault() ?? _p.Stories.First();
            cbStory.SelectedItem = story.Name;
            await Pause();

            tabView.SelectedItem = ViewTab("Mặt bằng"); await Pause(); Shot($"{tag}_1_MatBang");
            var beam = _p.Frames.FirstOrDefault(f => !f.IsVerticalKind && f.Story == story.Name);
            Select(frame: beam); await Pause(); Shot($"{tag}_2_MatBang_ChonDam");
            var slab = _p.Slabs.FirstOrDefault(s => s.Story == story.Name);
            Select(slab: slab); await Pause(); Shot($"{tag}_3_MatBang_ChonSan");

            tabView.SelectedItem = ViewTab("3D"); Select(frame: beam); await Pause(2000); Shot($"{tag}_4_3D_ChonDam");

            tabView.SelectedItem = ViewTab("Biểu đồ nội lực");
            cbDiagramCase.SelectedItem = cbDiagramCase.Items.OfType<object>().FirstOrDefault(o => o.ToString() == "BAO") ?? cbDiagramCase.SelectedItem;
            for (int v = 0; v < cbDiaView.Items.Count; v++)
            {
                cbDiaView.SelectedIndex = v; await Pause(2000); Shot($"{tag}_5_BieuDo_{v}");
            }
            cbDiaView.SelectedIndex = 0; cbDiaQty.SelectedIndex = Math.Min(2, cbDiaQty.Items.Count - 1); await Pause(); Shot($"{tag}_6_BieuDo_LucCat");

            tabView.SelectedItem = ViewTab("Mô hình móng");
            var ft = _design.Footings.FirstOrDefault();
            Select(footingNode: ft?.NodeId ?? 0); await Pause(1500); Shot($"{tag}_7_Mong");

            // sửa mô hình: ban công trên dầm biên, cầu thang, sàn WC
            var edge = ModelEdit.StoryBeams(_p, story.Name).OrderBy(f => Math.Min(_p.Node(f.NodeI).Y, _p.Node(f.NodeJ).Y))
                .FirstOrDefault(f => Math.Abs(_p.Node(f.NodeI).Y - _p.Node(f.NodeJ).Y) < 1e-6);
            var rb = ModelEdit.AddBalcony(_p, story.Name, edge, 1.2, 1.0); StLog("ban công: " + rb.Message);
            var s2 = _p.Slabs.Where(s => s.Story == story.Name).Skip(1).FirstOrDefault();
            if (s2 != null)
            {
                var n0 = _p.Node(s2.Nodes[0]); var n2 = _p.Node(s2.Nodes[2]);
                StLog("cầu thang: " + ModelEdit.AddOpening(_p, story.Name, (n0.X + n2.X) / 2, (n0.Y + n2.Y) / 2, OpeningKind.Stair).Message);
            }
            var s3 = _p.Slabs.FirstOrDefault(s => s.Story == story.Name);
            if (s3 != null) StLog("WC: " + ModelEdit.SetSlabUse(_p, s3, SlabUse.WC).Message);
            MarkDirty(); RefreshAll(false);
            tabView.SelectedItem = ViewTab("Mặt bằng"); Select(slab: _p.Slabs.LastOrDefault(s => s.Story == story.Name)); await Pause(); Shot($"{tag}_8_SauKhiSua");
            if (!await RunAnalysis()) throw new Exception("Phân tích lại lỗi");
            tabView.SelectedItem = ViewTab("3D"); await Pause(2000); Shot($"{tag}_9_3D_SauKhiSua");
            await SelfTestWallsFoundation(tag, story);
            await SelfTestPileLayout(tag); // bố trí cọc – MainWindow.SelfTestPileLayout.cs
            await SelfTestEccentricFooting(tag);
            await SelfTestCuTram(tag); // lùi cọc khỏi ranh, cừ tràm – MainWindow.SelfTestCuTram.cs // móng lệch tâm – MainWindow.SelfTestEccentric.cs
            await SelfTestCurvedBeam(tag, story); // dầm cong – MainWindow.SelfTestCurved.cs
            await SelfTestGeoAppendix(tag); // địa danh gió, hố khoan, gợi ý móng, phụ lục – MainWindow.SelfTestGeo.cs
            // dầm, tường, cấu tạo sàn, móng cọc, móng băng – MainWindow.SelfTestFoundation.cs
            // dàn trang: bộ tờ mặc định → DXF từng tờ + PDF, mở cửa sổ dàn trang và chụp ảnh
            {
                var dsg = _design ?? (_res != null ? ConcreteDesigner.Design(_p, _res) : null);
                _p.Sheets = KetCauVXT.Drawing.SheetBuilder.DefaultSet(_p, dsg, _res);
                var sheetDxf = KetCauVXT.Drawing.SheetBuilder.ExportAllDxf(_p, _res, dsg, Path.Combine(SelfTestOut, tag + "_DanTrang"));
                StLog($"dàn trang: {_p.Sheets.Count} tờ, {sheetDxf.Count} file DXF");
                var sw = new SheetWindow(_p, _res, dsg, () => MarkDirty(false)) { Owner = this, Width = 1500, Height = 900 };
                sw.Show();
                sw.SelectSheet(0); await Pause(1500); ShotWindow(sw, $"{tag}_10_DanTrang_1");
                if (_p.Sheets.Count > 3) { sw.SelectSheet(_p.Sheets.Count / 2); await Pause(1500); ShotWindow(sw, $"{tag}_11_DanTrang_2"); }
                sw.ExportPdf(Path.Combine(SelfTestOut, tag + "_DanTrang.pdf"), _p.Sheets.Take(3).ToList(), 100);
                StLog("dàn trang: PDF " + tag + "_DanTrang.pdf");
                sw.Close();
            }
            await SelfTestResultImport(tag); // nhập kết quả ETABS/SAP – MainWindow.SelfTestResultImport.cs
            await SelfTestRebar(tag); // chọn bố trí thép, shop drawing – MainWindow.SelfTestRebar.cs
            await SelfTestStair(tag); // cầu thang – MainWindow.SelfTestStair.cs
            await SelfTestQuantity(tag); // khối lượng vật tư, thuyết minh tự động – MainWindow.SelfTestQuantity.cs
            await SelfTestCadStandard(tag);
            await SelfTestDrawingStyle(tag); // tỷ lệ, cỡ chữ, kiểu kích thước – MainWindow.SelfTestDrawingStyle.cs // chuẩn layer CAD – MainWindow.SelfTestCad.cs
            await SelfTestAcadLink(tag);
            await SelfTestDwg(tag); // đọc DWG – MainWindow.SelfTestDwg.cs // nhận dữ liệu AutoCAD – MainWindow.SelfTestAcad.cs
            await SelfTestDrawingEdit(tag); // hiệu chỉnh bản vẽ, xuất gộp – MainWindow.SelfTestDrawingEdit.cs
            await SelfTestManualSheet(tag);
            await SelfTestTemplate(tag); // template, khung tên người dùng – MainWindow.SelfTestTemplate.cs // dàn trang thủ công – MainWindow.SelfTestSheetEdit.cs
            StLog("XONG – không lỗi");
        }
        catch (Exception ex)
        {
            code = 1;
            StLog("LỖI: " + ex);
        }
        _stLog.Dispose(); _stLog = null;
        _dirty = false;
        Application.Current.Shutdown(code);
    }
}

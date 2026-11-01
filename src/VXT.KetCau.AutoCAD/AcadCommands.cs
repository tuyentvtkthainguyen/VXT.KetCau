using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using VXT.KetCau.Core.Framing;
using VXT.KetCau.Core.Model;
using VXT.KetCau.Core.Report;
using VXT.KetCau.Core.Workflow;
using VXT.KetCau.Csi;
using VXT.KetCau.Dwg;
using VXT.KetCau.Ui;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace VXT.KetCau.AutoCAD
{
    public class AcadCommands
    {
        static Document Doc => AcApp.DocumentManager.MdiActiveDocument;

        static void Guard(Action a)
        {
            if (Doc == null) return;
            try { a(); }
            catch (System.Exception ex)
            {
                string msg = ex.Message;
                if (ex is FileNotFoundException fe && (fe.FileName ?? "").IndexOf("CSiAPIv1", StringComparison.OrdinalIgnoreCase) >= 0)
                    msg = "Không tìm thấy CSiAPIv1.dll — cần cài ETABS 21+ hoặc SAP2000 25+, hoặc khai báo đường dẫn trong Thông số.";
                MessageBox.Show(msg + Environment.NewLine + Environment.NewLine + ex.GetType().Name, "VXT Kết cấu — Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        static bool ShowDialog(Form f) => AcApp.ShowModalDialog(f) == DialogResult.OK;
        static void Report(string title, IEnumerable<string> lines) { using (var f = new ReportForm(title, lines)) AcApp.ShowModalDialog(f); }

        // ================================================================ Bảng điều khiển
        [CommandMethod("VXT", "VXTKC", CommandFlags.Modal)]
        public void Panel() => Guard(() =>
        {
            var st = AcadSession.Get(Doc);
            using (var f = new PanelForm(st, AcadSession.Result(Doc) != null, Doc.Name))
            {
                if (!ShowDialog(f)) return;
                switch (f.Action)
                {
                    case "THONGSO": Settings(); break;
                    case "KIENTRUC": ArchToStruct(); break;
                    case "PHANTICH": Analyze(); break;
                    case "DOCNOILUC": ReadResults(); break;
                    case "THIETKE": Design(); break;
                    case "SHOP": ShopDrawing(); break;
                    case "DXF": ExportDxf(); break;
                }
            }
        });

        // ================================================================ 0. Thông số
        [CommandMethod("VXT", "VXTKC_THONGSO", CommandFlags.Modal)]
        public void Settings() => Guard(() =>
        {
            var st = AcadSession.Get(Doc);
            var tmp = new ProjectState
            {
                Settings = Newtonsoft.Json.JsonConvert.DeserializeObject<DesignSettings>(Newtonsoft.Json.JsonConvert.SerializeObject(st.Settings)),
                Framing = Newtonsoft.Json.JsonConvert.DeserializeObject<FramingOptions>(Newtonsoft.Json.JsonConvert.SerializeObject(st.Framing)),
                Engine = st.Engine, AttachToRunning = st.AttachToRunning, EnginePath = st.EnginePath, OdaConverterPath = st.OdaConverterPath,
            };
            using (var f = new SettingsForm(tmp))
            {
                if (!ShowDialog(f)) return;
                st.Settings = tmp.Settings; st.Framing = tmp.Framing; st.Engine = tmp.Engine;
                st.AttachToRunning = tmp.AttachToRunning; st.EnginePath = tmp.EnginePath; st.OdaConverterPath = tmp.OdaConverterPath;
                AcadSession.LastEnginePath = st.EnginePath;
                if (st.Model != null) st.Model.Settings = st.Settings;
                AcadSession.Save(Doc);
                Doc.Editor.WriteMessage("\nĐã lưu thông số: " + AcadSession.StateFile(Doc) + "\n");
            }
        });

        // ================================================================ 1. Kiến trúc DWG → Kết cấu
        [CommandMethod("VXT", "VXTKC_KIENTRUC", CommandFlags.Modal)]
        public void ArchToStruct() => Guard(() =>
        {
            var st = AcadSession.Get(Doc);
            using (var f = new DwgPlansForm(Doc.Name))
            {
                if (!ShowDialog(f)) return;
                var log = new List<string>();
                var plans = new List<FloorPlanInput>();
                string tmpDir = Path.Combine(Path.GetTempPath(), "VXT_KetCau_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(tmpDir);
                var converted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in f.Plans)
                {
                    if (!converted.TryGetValue(p.Path, out var dxf))
                    {
                        dxf = ToDxf(p.Path, tmpDir, converted.Count);
                        converted[p.Path] = dxf;
                    }
                    plans.Add(new FloorPlanInput { Path = dxf, LevelName = p.LevelName, Elevation = p.Elevation, IsRoof = p.IsRoof, OffsetX = p.OffsetX, OffsetY = p.OffsetY });
                }
                var imp = new DxfArchImporter();
                var arch = imp.Import(plans);
                arch.ProjectName = Path.GetFileNameWithoutExtension(Doc.Name);
                log.AddRange(imp.Log);
                var gen = new FramingGenerator(st.Settings, st.Framing);
                var sm = gen.Generate(arch);
                log.AddRange(gen.Report.Info); log.AddRange(gen.Report.Warnings.Select(w => "⚠ " + w));
                st.Arch = arch; st.Model = sm;
                AcadSession.Save(Doc);
                log.Add("");
                log.Add($"Mô hình kết cấu: {sm.Stories.Count} tầng, {sm.Columns.Count} cột, {sm.Beams.Count} dầm, {sm.Slabs.Count} ô sàn, {sm.Footings.Count} móng.");
                log.Add("Tiếp theo: bước 2 (ETABS/SAP2000) hoặc bước 3 (Thiết kế, dùng nội lực sơ bộ).");
                try { Directory.Delete(tmpDir, true); } catch { }
                Report("Kiến trúc → Kết cấu", log);
            }
        });

        /// <summary>Chuyển một mặt bằng sang DXF bằng chính AutoCAD (không cần ODA): bản vẽ đang mở → DxfOut; file DWG khác → đọc ngầm rồi DxfOut.</summary>
        static string ToDxf(string path, string tmpDir, int i)
        {
            if (path.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase)) return path;
            string outPath = Path.Combine(tmpDir, $"tang_{i + 1}.dxf");
            var open = AcApp.DocumentManager.Cast<Document>().FirstOrDefault(d => string.Equals(d.Name, path, StringComparison.OrdinalIgnoreCase));
            if (open != null)
            {
                open.Database.DxfOut(outPath, 8, DwgVersion.Current);
                return outPath;
            }
            if (!File.Exists(path)) throw new FileNotFoundException("Không thấy file " + path);
            using (var db = new Database(false, true))
            {
                db.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, "");
                db.CloseInput(true);
                db.DxfOut(outPath, 8, DwgVersion.Current);
            }
            return outPath;
        }

        // ================================================================ 2. ETABS / SAP2000
        [CommandMethod("VXT", "VXTKC_PHANTICH", CommandFlags.Modal)]
        public void Analyze() => Guard(() =>
        {
            var st = AcadSession.Get(Doc);
            if (st.Model == null) throw new InvalidOperationException("Chưa có mô hình kết cấu — chạy bước 1 trước.");
            if (st.Engine == "Sơ bộ") { MessageBox.Show("Đang chọn chế độ 'Sơ bộ' — vào Thông số để chọn ETABS hoặc SAP2000.", "VXT Kết cấu"); return; }
            st.Model.Settings = st.Settings;
            RunCsi(st, prog => Path.Combine(AcadSession.OutputFolder(Doc), Path.GetFileNameWithoutExtension(Doc.Name) + (prog == CsiProgram.Etabs ? ".EDB" : ".sdb")), true);
        });

        [CommandMethod("VXT", "VXTKC_DOCNOILUC", CommandFlags.Modal)]
        public void ReadResults() => Guard(() =>
        {
            var st = AcadSession.Get(Doc);
            if (st.Model == null) throw new InvalidOperationException("Chưa có mô hình kết cấu — chạy bước 1 trước.");
            RunCsi(st, null, false);
        });

        // tách riêng để CSiAPIv1.dll chỉ được nạp khi thực sự cần
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static void RunCsi(ProjectState st, Func<CsiProgram, string> file, bool build)
        {
            var prog = st.Engine == "SAP2000" ? CsiProgram.Sap2000 : CsiProgram.Etabs;
            using (var eng = new CsiEngine(prog))
            {
                if (build)
                {
                    eng.Connect(st.AttachToRunning, string.IsNullOrEmpty(st.EnginePath) ? null : st.EnginePath);
                    string f = file(prog);
                    eng.BuildModel(st.Model, f);
                    eng.RunAnalysis();
                    eng.Log.Add("File mô hình: " + f);
                }
                else eng.Connect(true);
                eng.ReadResults(st.Model);
                AcadSession.Save(Doc);
                eng.Log.Add("Tiếp theo: bước 3 'Thiết kế & cấu tạo thép'.");
                Report($"{(build ? "Phân tích" : "Đọc nội lực")} {eng.Name}", eng.Log);
            }
        }

        // ================================================================ 3. Thiết kế
        [CommandMethod("VXT", "VXTKC_THIETKE", CommandFlags.Modal)]
        public void Design() => Guard(() =>
        {
            var st = AcadSession.Get(Doc);
            var res = AcadSession.Detail(Doc);
            string dir = AcadSession.OutputFolder(Doc);
            string html = Path.Combine(dir, "Thuyet minh ket cau.html");
            File.WriteAllText(html, CalcReport.Html(st.Model, res, st.Engine), new UTF8Encoding(false));
            string csv = Path.Combine(dir, "Thong ke cot thep.csv");
            File.WriteAllText(csv, res.Schedule.ToCsv(), new UTF8Encoding(true));
            var g = res.Schedule.ByGroup();
            var lines = new List<string>
            {
                $"Đã thiết kế {st.Model.Beams.Count} dầm, {st.Model.Columns.Count} cột, {st.Model.Slabs.Count} ô sàn.",
                $"Cấu kiện sau gộp: {res.Elements.Count(e => e.Category == "Dầm")} dầm, {res.Elements.Count(e => e.Category == "Cột")} cột, {res.Elements.Count(e => e.Category == "Sàn")} sàn, {res.Elements.Count(e => e.Category == "Móng")} móng.",
                $"Tổng thép: {g.Total:0.0} kg (Ø≤10: {g.G1:0.0}; 10<Ø≤18: {g.G2:0.0}; Ø>18: {g.G3:0.0}).",
                $"Thuyết minh: {html}",
                $"Bảng thống kê: {csv}",
                "Tiếp theo: bước 4 'Vẽ shop drawing'.",
                "", "CÁC ĐIỂM CẦN KIỂM TRA:",
            };
            lines.AddRange(res.Warnings.Distinct().Select(w => "• " + w));
            Report("Thiết kế & cấu tạo thép", lines);
            try { Process.Start(new ProcessStartInfo(html) { UseShellExecute = true }); } catch { }
        });

        // ================================================================ 4. Shop drawing vào bản vẽ
        [CommandMethod("VXT", "VXTKC_SHOP", CommandFlags.Modal)]
        public void ShopDrawing() => Guard(() =>
        {
            var doc = Doc; var ed = doc.Editor;
            var st = AcadSession.Get(doc);
            var res = AcadSession.Result(doc) ?? AcadSession.Detail(doc);
            var pr = ed.GetPoint(new PromptPointOptions("\nChọn điểm đặt shop drawing (góc trên trái): "));
            if (pr.Status != PromptStatus.OK) return;
            int n;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var comp = new VXT.KetCau.Core.Drawing.ShopDrawingComposer(null, st.Settings);
                double th = comp.PrintTextHeight * comp.ElevationScale;
                var w = new AcadDraw2D(doc.Database, tr, pr.Value, th);
                ShopDrawingDxf.Compose(w, res, st.Settings);
                tr.Commit();
                n = w.Count;
            }
            ed.Regen();
            ed.WriteMessage($"\nĐã vẽ shop drawing: {n} đối tượng, {res.Elements.Count(e => e.Category == "Dầm")} dầm, {res.Elements.Count(e => e.Category == "Cột")} cột + bảng thống kê. In tỉ lệ 1/50 (mặt cắt đã phóng ×2 → 1/25).\n");
            ed.WriteMessage("Gõ ZOOM → E để xem toàn bộ.\n");
        });

        // ================================================================ 5. Xuất DXF
        [CommandMethod("VXT", "VXTKC_DXF", CommandFlags.Modal)]
        public void ExportDxf() => Guard(() =>
        {
            var st = AcadSession.Get(Doc);
            var res = AcadSession.Result(Doc) ?? AcadSession.Detail(Doc);
            string path = Path.Combine(AcadSession.OutputFolder(Doc), "Shop drawing thep.dxf");
            using (var dlg = new SaveFileDialog { Filter = "DXF (*.dxf)|*.dxf", FileName = Path.GetFileName(path), InitialDirectory = Path.GetDirectoryName(path) })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                ShopDrawingDxf.Write(res, st.Settings, dlg.FileName);
                Doc.Editor.WriteMessage("\nĐã xuất: " + dlg.FileName + "\n");
            }
        });
    }
}

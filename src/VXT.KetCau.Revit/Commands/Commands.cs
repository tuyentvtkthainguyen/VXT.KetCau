using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using VXT.KetCau.Core.Framing;
using VXT.KetCau.Core.Model;
using VXT.KetCau.Core.Report;
using VXT.KetCau.Core.Workflow;
using VXT.KetCau.Csi;
using VXT.KetCau.Dwg;
using VXT.KetCau.Revit.Drawing;
using VXT.KetCau.Revit.Model;
using VXT.KetCau.Ui;
using VXT.KetCau.Revit.Util;
// .NET 8 WinForms cũng có TaskDialog — chỉ định rõ bản của Revit
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace VXT.KetCau.Revit.Commands
{
    /// <summary>Lớp cơ sở: bắt lỗi, hiển thị thông báo tiếng Việt.</summary>
    public abstract class CmdBase : IExternalCommand
    {
        protected UIDocument UiDoc;
        protected Document Doc;
        protected ProjectState State;

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UiDoc = data.Application.ActiveUIDocument;
            if (UiDoc == null) { TaskDialog.Show("VXT Kết cấu", "Hãy mở một dự án Revit trước."); return Result.Cancelled; }
            Doc = UiDoc.Document;
            State = Session.Get(Doc);
            try { return Run(); }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex)
            {
                TaskDialog.Show("VXT Kết cấu — Lỗi", ex.Message + Environment.NewLine + Environment.NewLine + ex.GetType().Name);
                return Result.Failed;
            }
        }

        protected abstract Result Run();

        protected string OutputFolder()
        {
            string dir = string.IsNullOrEmpty(Doc.PathName) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VXT Ket cau", Doc.Title)
                                                             : Path.Combine(Path.GetDirectoryName(Doc.PathName), Path.GetFileNameWithoutExtension(Doc.PathName) + "_KetCau");
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>Dựng khung kết cấu trong Revit từ ArchModel (dùng chung cho nguồn Revit và DWG).</summary>
        protected Result BuildFromArch(ArchModel arch, List<string> log)
        {
            var gen = new FramingGenerator(State.Settings, State.Framing);
            var sm = gen.Generate(arch);
            log.AddRange(gen.Report.Info); log.AddRange(gen.Report.Warnings.Select(w => "⚠ " + w));
            var td = new TaskDialog("VXT Kết cấu")
            {
                MainInstruction = "Dựng mô hình kết cấu trong Revit?",
                MainContent = $"{sm.Stories.Count} tầng, {sm.Columns.Count} cột, {sm.Beams.Count} dầm, {sm.Slabs.Count} ô sàn, {sm.Footings.Count} móng.\n" +
                              "Các phần tử VXT dựng trước đó sẽ được thay thế.",
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
            };
            if (td.Show() != TaskDialogResult.Yes) { State.Arch = arch; State.Model = sm; Session.Save(Doc); return Result.Cancelled; }
            using (var t = new Transaction(Doc, "VXT - Dựng kết cấu từ kiến trúc"))
            {
                t.Start();
                var b = new RevitModelBuilder(Doc);
                b.Build(sm, replaceExisting: true);
                t.Commit();
                log.AddRange(b.Log);
            }
            State.Arch = arch; State.Model = sm;
            Session.Save(Doc);
            ReportForm.Show("Kiến trúc → Kết cấu", log);
            return Result.Succeeded;
        }
    }

    // ====================================================================== 1. Kiến trúc Revit → Kết cấu
    [Transaction(TransactionMode.Manual)]
    public class CmdArchToStruct : CmdBase
    {
        protected override Result Run()
        {
            Document src = Doc; Transform tf = null;
            var links = new FilteredElementCollector(Doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().Where(l => l.GetLinkDocument() != null).ToList();
            if (links.Count > 0)
            {
                var td = new TaskDialog("VXT Kết cấu") { MainInstruction = "Lấy hồ sơ kiến trúc từ đâu?" };
                td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Tài liệu hiện hành", Doc.Title);
                td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "File kiến trúc liên kết", links[0].GetLinkDocument().Title + (links.Count > 1 ? $" (và {links.Count - 1} file khác — chọn file đầu tiên)" : ""));
                var r = td.Show();
                if (r == TaskDialogResult.CommandLink2) { src = links[0].GetLinkDocument(); tf = links[0].GetTotalTransform(); }
                else if (r != TaskDialogResult.CommandLink1) return Result.Cancelled;
            }
            var reader = new RevitArchReader(src, tf);
            var arch = reader.Read();
            using (var f = new LevelPickForm(arch.Levels.Select(l => (l.Name, l.Elevation)), src.Title))
            {
                if (f.ShowDialog() != DialogResult.OK) return Result.Cancelled;
                var keep = new HashSet<string>(f.Selected);
                arch.Levels.RemoveAll(l => !keep.Contains(l.Name));
                if (arch.Levels.Count < 2) throw new InvalidOperationException("Cần chọn ít nhất 2 cao độ");
                arch.Levels.OrderBy(l => l.Elevation).Last().IsRoof = true;
                // tường/cột/sàn ở cao độ bị bỏ → gán về cao độ được chọn ngay dưới
                string Snap(string n, double z) => keep.Contains(n ?? "") ? n : arch.Levels.Where(l => l.Elevation <= z + 1).OrderByDescending(l => l.Elevation).FirstOrDefault()?.Name;
                foreach (var w in arch.Walls) w.BaseLevel = Snap(w.BaseLevel, w.BaseElevation);
                foreach (var fl in arch.Floors) fl.Level = Snap(fl.Level, fl.Elevation);
            }
            return BuildFromArch(arch, reader.Log);
        }
    }

    // ====================================================================== 2. DWG/DXF → Kết cấu
    [Transaction(TransactionMode.Manual)]
    public class CmdDwgToStruct : CmdBase
    {
        protected override Result Run()
        {
            using (var f = new DwgPlansForm())
            {
                if (f.ShowDialog() != DialogResult.OK) return Result.Cancelled;
                var imp = new DxfArchImporter();
                var arch = imp.Import(f.Plans, string.IsNullOrEmpty(State.OdaConverterPath) ? null : State.OdaConverterPath);
                return BuildFromArch(arch, imp.Log);
            }
        }
    }

    // ====================================================================== 3. Đọc kết cấu Revit có sẵn
    [Transaction(TransactionMode.Manual)]
    public class CmdReadStruct : CmdBase
    {
        protected override Result Run()
        {
            var r = new RevitStructReader(Doc);
            var sm = r.Read(State.Settings);
            // tải tường/phòng từ kiến trúc trong cùng file (nếu có) hoặc từ lần đọc kiến trúc trước
            var arch = State.Arch ?? new RevitArchReader(Doc).Read();
            new FramingGenerator(State.Settings, State.Framing).BuildLoads(arch, sm);
            State.Model = sm;
            Session.Save(Doc);
            r.Log.Add($"Đã gán tải: {sm.AreaLoads.Count} tải sàn, {sm.LineLoads.Count} tải tường, {sm.Combos.Count} tổ hợp");
            ReportForm.Show("Đọc mô hình kết cấu Revit", r.Log);
            return Result.Succeeded;
        }
    }

    // ====================================================================== 4. Xuất ETABS/SAP2000 + chạy + đọc nội lực
    [Transaction(TransactionMode.Manual)]
    public class CmdAnalyze : CmdBase
    {
        protected override Result Run()
        {
            if (State.Model == null) throw new InvalidOperationException("Chưa có mô hình kết cấu.");
            if (State.Engine == "Sơ bộ") { TaskDialog.Show("VXT Kết cấu", "Đang chọn chế độ 'Sơ bộ' — vào Thông số để chọn ETABS hoặc SAP2000."); return Result.Cancelled; }
            State.Model.Settings = State.Settings;
            var prog = State.Engine == "SAP2000" ? CsiProgram.Sap2000 : CsiProgram.Etabs;
            string file = Path.Combine(OutputFolder(), Path.GetFileNameWithoutExtension(Doc.Title) + (prog == CsiProgram.Etabs ? ".EDB" : ".sdb"));
            using (var eng = new CsiEngine(prog))
            {
                eng.Connect(State.AttachToRunning, string.IsNullOrEmpty(State.EnginePath) ? null : State.EnginePath);
                eng.BuildModel(State.Model, file);
                eng.RunAnalysis();
                eng.ReadResults(State.Model);
                Session.Save(Doc);
                eng.Log.Add($"File mô hình: {file}");
                eng.Log.Add("Tiếp theo: bấm 'Thiết kế & cấu tạo'.");
                ReportForm.Show($"Phân tích {eng.Name}", eng.Log);
            }
            return Result.Succeeded;
        }
    }

    // ====================================================================== 5. Đọc nội lực từ ETABS/SAP đang mở (sau khi tự chỉnh mô hình)
    [Transaction(TransactionMode.Manual)]
    public class CmdReadResults : CmdBase
    {
        protected override Result Run()
        {
            if (State.Model == null) throw new InvalidOperationException("Chưa có mô hình kết cấu.");
            var prog = State.Engine == "SAP2000" ? CsiProgram.Sap2000 : CsiProgram.Etabs;
            using (var eng = new CsiEngine(prog))
            {
                eng.Connect(true);
                eng.ReadResults(State.Model);
                Session.Save(Doc);
                ReportForm.Show($"Đọc nội lực {eng.Name}", eng.Log);
            }
            return Result.Succeeded;
        }
    }

    // ====================================================================== 6. Thiết kế + cấu tạo + thuyết minh
    [Transaction(TransactionMode.Manual)]
    public class CmdDesign : CmdBase
    {
        protected override Result Run()
        {
            var res = Session.Detail(Doc);
            string dir = OutputFolder();
            string html = Path.Combine(dir, "Thuyet minh ket cau.html");
            File.WriteAllText(html, CalcReport.Html(State.Model, res, State.Engine), new UTF8Encoding(false));
            string csv = Path.Combine(dir, "Thong ke cot thep.csv");
            File.WriteAllText(csv, res.Schedule.ToCsv(), new UTF8Encoding(true));
            var g = res.Schedule.ByGroup();
            var lines = new List<string>
            {
                $"Đã thiết kế {State.Model.Beams.Count} dầm, {State.Model.Columns.Count} cột, {State.Model.Slabs.Count} ô sàn.",
                $"Cấu kiện sau gộp: {res.Elements.Count(e => e.Category == "Dầm")} dầm, {res.Elements.Count(e => e.Category == "Cột")} cột, {res.Elements.Count(e => e.Category == "Sàn")} sàn, {res.Elements.Count(e => e.Category == "Móng")} móng.",
                $"Tổng thép: {g.Total:0.0} kg (Ø≤10: {g.G1:0.0}; 10<Ø≤18: {g.G2:0.0}; Ø>18: {g.G3:0.0}).",
                $"Thuyết minh: {html}",
                $"Bảng thống kê: {csv}",
                "", "CÁC ĐIỂM CẦN KIỂM TRA:",
            };
            lines.AddRange(res.Warnings.Distinct().Select(w => "• " + w));
            ReportForm.Show("Thiết kế & cấu tạo thép", lines);
            try { Process.Start(new ProcessStartInfo(html) { UseShellExecute = true }); } catch { }
            return Result.Succeeded;
        }
    }

    // ====================================================================== 7. Tạo thép 3D
    [Transaction(TransactionMode.Manual)]
    public class CmdRebar : CmdBase
    {
        protected override Result Run()
        {
            var res = Session.Result(Doc) ?? Session.Detail(Doc);
            var b = new RevitRebarBuilder(Doc, State.Model);
            using (var t = new Transaction(Doc, "VXT - Tạo thép"))
            {
                t.Start();
                var fh = t.GetFailureHandlingOptions();
                fh.SetFailuresPreprocessor(new SilentWarnings());
                t.SetFailureHandlingOptions(fh);
                b.Build(res);
                t.Commit();
            }
            ReportForm.Show("Tạo thép trong Revit", b.Log);
            return Result.Succeeded;
        }
    }

    // ====================================================================== 8. Shop drawing trong Revit
    [Transaction(TransactionMode.Manual)]
    public class CmdShopDrawing : CmdBase
    {
        protected override Result Run()
        {
            var res = Session.Result(Doc) ?? Session.Detail(Doc);
            var sd = new RevitShopDrawing(Doc, State.Settings);
            using (var t = new Transaction(Doc, "VXT - Shop drawing thép"))
            {
                t.Start();
                var fh = t.GetFailureHandlingOptions();
                fh.SetFailuresPreprocessor(new SilentWarnings());
                t.SetFailureHandlingOptions(fh);
                sd.Build(res);
                t.Commit();
            }
            ReportForm.Show("Shop drawing", sd.Log);
            return Result.Succeeded;
        }
    }

    // ====================================================================== 9. Xuất shop drawing DXF
    [Transaction(TransactionMode.Manual)]
    public class CmdExportDxf : CmdBase
    {
        protected override Result Run()
        {
            var res = Session.Result(Doc) ?? Session.Detail(Doc);
            string path = Path.Combine(OutputFolder(), "Shop drawing thep.dxf");
            using (var dlg = new SaveFileDialog { Filter = "DXF (*.dxf)|*.dxf", FileName = Path.GetFileName(path), InitialDirectory = Path.GetDirectoryName(path) })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return Result.Cancelled;
                ShopDrawingDxf.Write(res, State.Settings, dlg.FileName);
                TaskDialog.Show("VXT Kết cấu", "Đã xuất: " + dlg.FileName);
            }
            return Result.Succeeded;
        }
    }

    // ====================================================================== 10. Thông số
    [Transaction(TransactionMode.Manual)]
    public class CmdSettings : CmdBase
    {
        protected override Result Run()
        {
            // sửa trên bản sao, chỉ ghi lại khi bấm Lưu
            var tmp = new ProjectState
            {
                Settings = Newtonsoft.Json.JsonConvert.DeserializeObject<DesignSettings>(Newtonsoft.Json.JsonConvert.SerializeObject(State.Settings)),
                Framing = Newtonsoft.Json.JsonConvert.DeserializeObject<FramingOptions>(Newtonsoft.Json.JsonConvert.SerializeObject(State.Framing)),
                Engine = State.Engine, AttachToRunning = State.AttachToRunning, EnginePath = State.EnginePath, OdaConverterPath = State.OdaConverterPath,
            };
            using (var f = new SettingsForm(tmp))
            {
                if (f.ShowDialog() != DialogResult.OK) return Result.Cancelled;
                State.Settings = tmp.Settings; State.Framing = tmp.Framing; State.Engine = tmp.Engine;
                State.AttachToRunning = tmp.AttachToRunning; State.EnginePath = tmp.EnginePath; State.OdaConverterPath = tmp.OdaConverterPath;
                if (State.Model != null) State.Model.Settings = State.Settings;
                Session.Save(Doc);
                return Result.Succeeded;
            }
        }
    }

    /// <summary>Bỏ qua cảnh báo (thép nằm ngoài host, chồng phần tử…) để không chặn lệnh hàng loạt.</summary>
    public class SilentWarnings : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor fa)
        {
            foreach (var f in fa.GetFailureMessages())
                if (f.GetSeverity() == FailureSeverity.Warning) fa.DeleteWarning(f);
            return FailureProcessingResult.Continue;
        }
    }
}

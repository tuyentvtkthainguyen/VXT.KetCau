using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace KetCauVXT.RevitExport
{
    // =====================================================================
    // CẦU NỐI AGENT ↔ REVIT  (chỉ trên máy, không mạng, không thu thập dữ liệu)
    //
    // Mặc định TẮT. Người dùng bấm "Cho phép agent" trên ribbon mới bật (mỗi lần mở Revit phải bật lại).
    // Khi bật: agent đặt lệnh vào  %LOCALAPPDATA%\VietConst\Agent\revit\in\<mã>.json
    //   { "lenh": "dung-kien-truc", "file": "D:\\...\\nha.vxtphuongan.json" }
    // Revit chạy lệnh rồi ghi  ...\revit\out\<mã>.ket-qua.json  { "id", "lenh", "ok", "bao_cao", "file" }.
    // Trạng thái:  ...\revit\trang-thai.json  { "dang_bat", "du_an", "luc" }.
    //
    // Chỉ các lệnh trong danh sách dưới. Lệnh kết cấu (dung-ket-cau, nhan-tiet-dien) luôn hiện hộp thoại để NGƯỜI DÙNG duyệt.
    // =====================================================================
    internal sealed class AgentBridge : IExternalEventHandler
    {
        public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VietConst", "Agent", "revit");
        public static string InDir => Path.Combine(Root, "in");
        public static string OutDir => Path.Combine(Root, "out");

        public static readonly string[] Commands =
        {
            "trang-thai", "du-an-moi", "mo", "dung-kien-truc", "xuat-viet-const", "dung-ket-cau", "nhan-tiet-dien", "tao-ban-ve", "anh-3d", "xuat-pdf", "xuat-dwg", "liet-ke-ho", "luu",
        };

        private static AgentBridge _inst;
        private static ExternalEvent _ev;
        private static Timer _timer;
        private static int _pending;
        public static bool Enabled { get; private set; }
        internal static PushButton Button;

        public static void Init()
        {
            _inst = new AgentBridge();
            _ev = ExternalEvent.Create(_inst);
        }

        public static void Toggle(UIApplication app)
        {
            Enabled = !Enabled;
            if (Enabled)
            {
                Directory.CreateDirectory(InDir); Directory.CreateDirectory(OutDir);
                _timer = new Timer(_ => Poll(), null, 1000, 1500);
            }
            else { _timer?.Dispose(); _timer = null; }
            if (Button != null) Button.ItemText = Enabled ? "Agent: ĐANG BẬT\n(bấm để tắt)" : "Cho phép\nagent";
            WriteStatus(app);
        }

        public static void Shutdown() { _timer?.Dispose(); _timer = null; Enabled = false; try { WriteStatus(null); } catch { } }

        private static void Poll()
        {
            try
            {
                if (!Enabled || _ev == null || Interlocked.CompareExchange(ref _pending, 1, 0) != 0) return;
                if (Directory.Exists(InDir) && Directory.EnumerateFiles(InDir, "*.json").Any()) _ev.Raise();
                else Interlocked.Exchange(ref _pending, 0);
            }
            catch { Interlocked.Exchange(ref _pending, 0); }
        }

        public string GetName() => "Việt Const – cầu nối agent";

        public void Execute(UIApplication app)
        {
            try
            {
                foreach (var f in Directory.GetFiles(InDir, "*.json").OrderBy(File.GetCreationTimeUtc).Take(20))
                {
                    if (!Enabled) break;
                    string id = Path.GetFileNameWithoutExtension(f);
                    string body;
                    try { body = File.ReadAllText(f, Encoding.UTF8); File.Delete(f); }
                    catch { continue; }   // agent còn đang ghi → lần sau
                    RunOne(app, id, body);
                }
            }
            catch { }
            finally { Interlocked.Exchange(ref _pending, 0); WriteStatus(app); }
        }

        private static void RunOne(UIApplication app, string id, string body)
        {
            string lenh = "", file = "", outFile = "";
            bool ok = false; string rep;
            var started = DateTime.Now;
            AgentMode.Reset();
            AgentMode.Report = "";
            try
            {
                var o = MiniJson.Parse(body);
                lenh = MiniJson.S(o, "lenh").Trim().ToLowerInvariant();
                file = MiniJson.S(o, "file");
                if (!Commands.Contains(lenh)) throw new InvalidOperationException("Lệnh \"" + lenh + "\" không có trong danh sách cho phép: " + string.Join(", ", Commands));
                var doc = app.ActiveUIDocument?.Document;
                string msg = "";
                Result r = Result.Succeeded;
                AgentMode.Quiet = true;
                switch (lenh)
                {
                    case "trang-thai":
                        AgentMode.Report = doc == null ? "Chưa mở dự án." : Status(doc);
                        break;
                    case "du-an-moi":
                        Need(file, ".rvt");
                        {
                            string tpl = MiniJson.S(o, "mau");
                            if (string.IsNullOrEmpty(tpl)) tpl = app.Application.DefaultProjectTemplate;
                            var nd = string.IsNullOrEmpty(tpl) || !File.Exists(tpl) ? app.Application.NewProjectDocument(UnitSystem.Metric) : app.Application.NewProjectDocument(tpl);
                            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
                            nd.SaveAs(file, new SaveAsOptions { OverwriteExistingFile = false });
                            nd.Close(false);
                            app.OpenAndActivateDocument(file);
                            AgentMode.Report = "Đã tạo dự án mới: " + file + (string.IsNullOrEmpty(tpl) ? " (không có mẫu – hệ mét trống)" : " (mẫu: " + tpl + ")");
                            outFile = file;
                        }
                        break;
                    case "mo":
                        Need(file, ".rvt");
                        app.OpenAndActivateDocument(file);
                        AgentMode.Report = "Đã mở " + file;
                        break;
                    case "dung-kien-truc":
                        Need(file, ".json");
                        AgentMode.AutoApply = true; AgentMode.InputPath = file;
                        foreach (var g in MiniJson.A(o, "lam_lai")) if (g is string gs) AgentMode.Rebuild.Add(gs);
                        r = new BuildArchitectureCommand().Run(app, ref msg);
                        break;
                    case "xuat-viet-const":
                        Need(file, ".json");
                        AgentMode.OutputPath = file; outFile = file;
                        r = new ExportCommand().Run(app, ref msg);
                        break;
                    case "dung-ket-cau":
                        // người dùng duyệt trong hộp thoại (AutoApply = false)
                        Need(file, ".json");
                        AgentMode.InputPath = file; AgentMode.Quiet = false;
                        r = new BuildStructureCommand().Run(app, ref msg);
                        break;
                    case "nhan-tiet-dien":
                        Need(file, ".json");
                        AgentMode.InputPath = file; AgentMode.Quiet = false;
                        r = new SyncSectionsCommand().Run(app, ref msg);
                        break;
                    case "tao-ban-ve":
                        DrawingSetCommand.FrontDeg = MiniJson.D(o, "huong_mat_tien", DrawingSetCommand.FrontDeg);
                        // "pham_vi": "mat-bang" → chỉ mặt bằng sơ bộ (điểm chốt người dùng xác nhận); bỏ trống → cả bộ
                        DrawingSetCommand.Scope = MiniJson.S(o, "pham_vi", "");
                        try { r = new DrawingSetCommand().Run(app, ref msg); }
                        finally { DrawingSetCommand.Scope = ""; }
                        break;
                    case "anh-3d":
                        Need(file, ".png");
                        if (doc == null) throw new InvalidOperationException("Chưa mở dự án.");
                        {
                            var log = new List<string>();
                            double deg = MiniJson.D(o, "huong_mat_tien", DrawingSetCommand.FrontDeg);
                            outFile = DrawingSetCommand.ExportImage(doc, file, deg, (int)MiniJson.D(o, "rong", 1920), log);
                            AgentMode.Report = "Đã lưu ảnh " + outFile + (log.Count > 0 ? "\n" + string.Join("\n", log) : "");
                        }
                        break;
                    case "xuat-pdf":
                    case "xuat-dwg":
                        if (doc == null) throw new InvalidOperationException("Chưa mở dự án.");
                        {
                            // "file": thư mục đích; "so_to": tiền tố số tờ (mặc định "KT-")
                            if (string.IsNullOrWhiteSpace(file)) throw new ArgumentException("Thiếu \"file\" (thư mục đích).");
                            Directory.CreateDirectory(file);
                            string prefix = MiniJson.S(o, "so_to", "KT-");
                            var ids = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                                .Where(v => !v.IsPlaceholder && v.SheetNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                .OrderBy(v => v.SheetNumber).Select(v => v.Id).ToList();
                            if (ids.Count == 0) throw new InvalidOperationException("Không có tờ nào số bắt đầu bằng \"" + prefix + "\" – chạy tao-ban-ve trước.");
                            string name = SafeName(doc.Title) + "_" + prefix.TrimEnd('-');
                            if (lenh == "xuat-pdf")
                            {
                                var po = new PDFExportOptions { Combine = true, FileName = name };
                                doc.Export(file, ids, po);
                                outFile = Path.Combine(file, name + ".pdf");
                            }
                            else
                            {
                                var dwo = new DWGExportOptions { MergedViews = true };
                                doc.Export(file, name, ids, dwo);
                                outFile = file;
                            }
                            AgentMode.Report = $"Đã xuất {ids.Count} tờ ({lenh.Substring(5).ToUpperInvariant()}) vào {file}";
                        }
                        break;
                    case "liet-ke-ho":
                        // họ cửa đi / cửa sổ (và loại, kích thước) trong dự án đang mở hoặc trong "file" (*.rvt / *.rte, mở ngầm)
                        AgentMode.Report = ListFamilies(app, doc, file);
                        break;
                    case "luu":
                        if (doc == null) throw new InvalidOperationException("Chưa mở dự án.");
                        if (!string.IsNullOrEmpty(file))
                        {
                            Need(file, ".rvt");
                            doc.SaveAs(file, new SaveAsOptions { OverwriteExistingFile = true });
                            outFile = file;
                        }
                        else if (string.IsNullOrEmpty(doc.PathName)) throw new InvalidOperationException("Dự án chưa có đường dẫn – gửi kèm \"file\" để lưu thành.");
                        else { doc.Save(); outFile = doc.PathName; }
                        AgentMode.Report = "Đã lưu " + outFile;
                        break;
                }
                ok = r == Result.Succeeded;
                rep = AgentMode.Report ?? "";
                if (!string.IsNullOrEmpty(msg)) rep = (rep + "\n" + msg).Trim();
                if (r == Result.Cancelled && rep.Length == 0) rep = "Người dùng huỷ.";
            }
            catch (Exception ex) { ok = false; rep = ex.Message; }
            finally { AgentMode.Reset(); }

            try
            {
                var w = new JsonWriter();
                w.BeginObject();
                w.Prop("id", id); w.Prop("lenh", lenh); w.Prop("ok", ok); w.Prop("bao_cao", rep);
                w.Prop("file", outFile);
                w.Prop("bat_dau", started.ToString("s", CultureInfo.InvariantCulture));
                w.Prop("xong", DateTime.Now.ToString("s", CultureInfo.InvariantCulture));
                w.EndObject();
                Directory.CreateDirectory(OutDir);
                string tmp = Path.Combine(OutDir, id + ".tmp");
                File.WriteAllText(tmp, w.ToString(), new UTF8Encoding(false));
                string dst = Path.Combine(OutDir, id + ".ket-qua.json");
                if (File.Exists(dst)) File.Delete(dst);
                File.Move(tmp, dst);
            }
            catch { }
        }

        private static string ListFamilies(UIApplication app, Document doc, string file)
        {
            Document sd = doc; bool opened = false;
            if (!string.IsNullOrWhiteSpace(file))
            {
                if (!File.Exists(file)) throw new FileNotFoundException("Không thấy " + file);
                sd = null;
                foreach (Document od in app.Application.Documents) if (string.Equals(od.PathName, file, StringComparison.OrdinalIgnoreCase)) sd = od;
                if (sd == null) { sd = app.Application.OpenDocumentFile(ModelPathUtils.ConvertUserVisiblePathToModelPath(file), new OpenOptions()); opened = true; }
            }
            if (sd == null) throw new InvalidOperationException("Chưa mở dự án.");
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Nguồn: " + (sd.PathName.Length > 0 ? sd.PathName : sd.Title));
                foreach (var cat in new[] { BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows })
                {
                    sb.AppendLine(cat == BuiltInCategory.OST_Doors ? "== CỬA ĐI ==" : "== CỬA SỔ ==");
                    var syms = new FilteredElementCollector(sd).OfClass(typeof(FamilySymbol)).OfCategory(cat).Cast<FamilySymbol>().OrderBy(x => x.FamilyName).ThenBy(x => x.Name);
                    var used = new HashSet<ElementId>(new FilteredElementCollector(sd).OfClass(typeof(FamilyInstance)).OfCategory(cat).Cast<FamilyInstance>().Select(x => x.Symbol.Id));
                    foreach (var x in syms)
                    {
                        var w = x.get_Parameter(cat == BuiltInCategory.OST_Doors ? BuiltInParameter.DOOR_WIDTH : BuiltInParameter.WINDOW_WIDTH) ?? x.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM);
                        var h = x.get_Parameter(cat == BuiltInCategory.OST_Doors ? BuiltInParameter.DOOR_HEIGHT : BuiltInParameter.WINDOW_HEIGHT) ?? x.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM);
                        string ws = w != null && w.StorageType == StorageType.Double ? Math.Round(VxtRevit.ToM(w.AsDouble()) * 1000).ToString(CultureInfo.InvariantCulture) : "?";
                        string hs = h != null && h.StorageType == StorageType.Double ? Math.Round(VxtRevit.ToM(h.AsDouble()) * 1000).ToString(CultureInfo.InvariantCulture) : "?";
                        sb.AppendLine($"{x.FamilyName} | {x.Name} | {ws}x{hs}{(used.Contains(x.Id) ? " | đang dùng" : "")}");
                    }
                }
                return sb.ToString();
            }
            finally { if (opened) sd.Close(false); }
        }

        private static string SafeName(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        private static void Need(string file, string ext)
        {
            if (string.IsNullOrWhiteSpace(file)) throw new ArgumentException("Thiếu \"file\".");
            if (!file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("\"file\" phải là " + ext + ": " + file);
            if (ext != ".png" && ext != ".rvt" && !File.Exists(file) && !file.EndsWith(".vxtrevit.json", StringComparison.OrdinalIgnoreCase)) throw new FileNotFoundException("Không thấy file: " + file);
        }

        private static string Status(Document d)
        {
            int Count(BuiltInCategory c) => new FilteredElementCollector(d).OfCategory(c).WhereElementIsNotElementType().GetElementCount();
            return $"Dự án: {d.Title} ({d.PathName})\nCao độ: {VxtRevit.Levels(d).Count}, tường: {Count(BuiltInCategory.OST_Walls)}, sàn: {Count(BuiltInCategory.OST_Floors)}, " +
                   $"cửa đi: {Count(BuiltInCategory.OST_Doors)}, cửa sổ: {Count(BuiltInCategory.OST_Windows)}, phòng: {Count(BuiltInCategory.OST_Rooms)}, " +
                   $"cột KC: {Count(BuiltInCategory.OST_StructuralColumns)}, dầm KC: {Count(BuiltInCategory.OST_StructuralFraming)}, tờ: {new FilteredElementCollector(d).OfClass(typeof(ViewSheet)).GetElementCount()}";
        }

        private static void WriteStatus(UIApplication app)
        {
            try
            {
                Directory.CreateDirectory(Root);
                var w = new JsonWriter();
                w.BeginObject();
                w.Prop("dang_bat", Enabled);
                string title = "";
                try { title = app?.ActiveUIDocument?.Document?.Title ?? ""; } catch { }
                w.Prop("du_an", title);
                w.Prop("luc", DateTime.Now.ToString("s", CultureInfo.InvariantCulture));
                w.BeginArray("lenh");
                foreach (var c in Commands) w.Prop(null, c);
                w.EndArray();
                w.EndObject();
                File.WriteAllText(Path.Combine(Root, "trang-thai.json"), w.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }
    }

    /// <summary>Nút "Cho phép agent": bật / tắt cầu nối.</summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class AgentToggleCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            bool turningOn = !AgentBridge.Enabled;
            if (turningOn)
            {
                var td = new TaskDialog("Cho phép agent")
                {
                    MainInstruction = "Cho phép agent điều khiển Revit trên máy này?",
                    MainContent = "Agent (Claude, Việt Const) sẽ được chạy các lệnh: " + string.Join(", ", AgentBridge.Commands) +
                                  ".\n\nLệnh dựng / đổi kết cấu vẫn hiện hộp thoại để bạn duyệt. Mọi thứ chạy trên máy, không gửi dữ liệu ra ngoài.\nThư mục lệnh: " + AgentBridge.Root,
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.No,
                };
                if (td.Show() != TaskDialogResult.Yes) return Result.Cancelled;
            }
            AgentBridge.Toggle(commandData.Application);
            return Result.Succeeded;
        }
    }
}

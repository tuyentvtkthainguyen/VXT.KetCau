using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using VXT.KetCau.Core.Documentation;
using VXT.KetCau.Core.Framing;
using VXT.KetCau.Core.Input;
using VXT.KetCau.Core.Model;
using VXT.KetCau.Core.Workflow;
using VXT.KetCau.Csi;
using VXT.KetCau.Dwg;
using VXT.KetCau.Ui;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace VXT.KetCau.AutoCAD
{
    /// <summary>Các lệnh triển khai hồ sơ: VXTHS và các bước con.</summary>
    public class HoSoCommands
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
                    msg = "Không tìm thấy CSiAPIv1.dll — cần cài ETABS 21+ hoặc SAP2000 25+.";
                MessageBox.Show(msg + Environment.NewLine + Environment.NewLine + ex.GetType().Name, "VXT — Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        static bool ShowDialog(Form f) => AcApp.ShowModalDialog(f) == DialogResult.OK;
        static void Report(string title, IEnumerable<string> lines) { using (var f = new ReportForm(title, lines)) AcApp.ShowModalDialog(f); }

        // ================================================================ Bảng điều khiển
        [CommandMethod("VXT", "VXTHS", CommandFlags.Modal)]
        public void Panel() => Guard(() =>
        {
            var st = AcadSession.Get(Doc);
            string act;
            using (var f = new HoSoPanelForm(st, Doc.Name))
            {
                if (!ShowDialog(f)) return;
                act = f.Action;
            }
            switch (act)
            {
                case "QUET": Scan(); break;
                case "FILE": FromFiles(); break;
                case "THONGSO": Parametric(); break;
                case "ETABS": FromEtabs(); break;
                case "KIEMTRA": ReviewArch(); break;
                case "SUAKC": EditStruct(); break;
                case "THONGTIN": Info(); break;
                case "VE": Draw(); break;
                case "DXF": ExportDxf(); break;
            }
        });

        // ================================================================ Đầu vào 1: quét trong bản vẽ
        /// <summary>Khoanh từng tầng trong bản vẽ đang mở, đọc, vẽ lớp nhận diện chồng lên để đối chiếu.</summary>
        [CommandMethod("VXT", "VXTHS_QUET", CommandFlags.Modal)]
        public void Scan() => Guard(() =>
        {
            var doc = Doc; var ed = doc.Editor;
            var st = AcadSession.Get(doc);
            var plans = new List<FloorPlanInput>();
            var bases = new List<PlanBase>();
            string dxf = Path.Combine(Path.GetTempPath(), "VXT_quet_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".dxf");
            int i = 1;
            double nextZ = 0;
            while (true)
            {
                var pn = new PromptStringOptions($"\nTên tầng {i} (Enter để kết thúc) [Tầng {i}]: ") { AllowSpaces = true };
                if (plans.Count == 0) pn.Message = $"\nTên tầng {i} [Tầng {i}]: ";
                var rn = ed.GetString(pn);
                if (rn.Status == PromptStatus.Cancel) break;
                string name = rn.StringResult;
                if (string.IsNullOrWhiteSpace(name))
                {
                    if (plans.Count > 0) break;
                    name = "Tầng " + i;
                }
                var pz = new PromptDoubleOptions($"\nCao độ sàn {name} (m) <{nextZ:0.000}>: ") { AllowNegative = true, AllowZero = true, DefaultValue = nextZ, UseDefaultValue = true };
                var rz = ed.GetDouble(pz);
                if (rz.Status != PromptStatus.OK) break;
                var p1 = ed.GetPoint(new PromptPointOptions($"\nGóc thứ nhất của khung chứa mặt bằng {name}: "));
                if (p1.Status != PromptStatus.OK) break;
                var p2 = ed.GetCorner(new PromptCornerOptions("\nGóc đối diện: ", p1.Value));
                if (p2.Status != PromptStatus.OK) break;
                var pb = ed.GetPoint(new PromptPointOptions($"\nĐiểm gốc của {name} (cùng một giao trục trên mọi tầng): "));
                if (pb.Status != PromptStatus.OK) break;
                plans.Add(new FloorPlanInput
                {
                    Path = dxf, LevelName = name.Trim(), Elevation = rz.Value * 1000,
                    Window = new[] { p1.Value.X, p1.Value.Y, p2.Value.X, p2.Value.Y }, BaseX = pb.Value.X, BaseY = pb.Value.Y,
                });
                bases.Add(new PlanBase { Level = name.Trim(), X = pb.Value.X, Y = pb.Value.Y });
                nextZ = rz.Value + 3.6;
                i++;
            }
            if (plans.Count == 0) return;
            var proof = new PromptDoubleOptions($"\nCao độ mái (m) <{nextZ:0.000}>: ") { AllowNegative = false, DefaultValue = nextZ, UseDefaultValue = true };
            var rr = ed.GetDouble(proof);
            if (rr.Status != PromptStatus.OK) return;
            plans.Add(new FloorPlanInput { LevelName = "Mái", Elevation = rr.Value * 1000, IsRoof = true, LevelOnly = true });

            doc.Database.DxfOut(dxf, 8, DwgVersion.Current);
            var imp = new DxfArchImporter();
            ArchModel arch;
            try { arch = imp.Import(plans); }
            finally { try { File.Delete(dxf); } catch { } }
            if (string.IsNullOrWhiteSpace(st.Doc.ProjectName)) st.Doc.ProjectName = Path.GetFileNameWithoutExtension(doc.Name);
            arch.ProjectName = st.Doc.ProjectName;
            st.Doc.PlanBases = bases;
            Finish(st, arch, imp.Log, imp.ScaleUsed);
            DrawOverlay(doc, st, imp.ScaleUsed);
            ReviewArch();
        });

        /// <summary>Lưu kiến trúc, báo cáo những gì đọc được; mô hình kết cấu cũ không còn đúng nữa.</summary>
        static void Finish(ProjectState st, ArchModel arch, IEnumerable<string> importLog, double unitScale)
        {
            var log = new List<string>(importLog);
            log.AddRange(ArchAnalysis.Prepare(arch));
            st.Arch = arch;
            st.Model = null;
            AcadSession.Save(Doc);
            AcadSession.ClearResult(Doc);
            log.Add("");
            log.Add($"Đọc được: {arch.Levels.Count} cao độ, {arch.Grids.Count} trục, {arch.Walls.Count} tường, {arch.Columns.Count} cột, {arch.Openings.Count} cửa, {arch.Rooms.Count} phòng, {arch.Stairs.Count} thang.");
            if (Math.Abs(unitScale - 1) > 1e-6) log.Add($"Bản vẽ không dùng đơn vị mm: đã đổi với hệ số ×{unitScale}. Lớp đối chiếu không vẽ được đúng chỗ.");
            if (arch.Grids.Count < 2) log.Add("⚠ Không thấy trục: tên lớp trục cần có TRUC, AXIS hoặc GRID.");
            if (arch.Walls.Count == 0) log.Add("⚠ Không thấy tường: tên lớp tường cần có TUONG hoặc WALL, tường vẽ bằng 2 nét song song.");
            log.Add("Tiếp theo: bước Xem và sửa dữ liệu rồi Vẽ hồ sơ.");
            Report("Kết quả nhận diện", log);
        }

        static void DrawOverlay(Document doc, ProjectState st, double unitScale)
        {
            if (Math.Abs(unitScale - 1) > 1e-6 || st.Arch == null || st.Doc.PlanBases.Count == 0) return;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                EraseLayer(doc.Database, tr, "VXT_NHANDIEN");
                foreach (var b in st.Doc.PlanBases)
                {
                    var w = new AcadDraw2D(doc.Database, tr, new Point3d(b.X, b.Y, 0), 250);
                    ArchAnalysis.DrawOverlay(w, st.Arch, b.Level);
                }
                tr.Commit();
            }
            doc.Editor.Regen();
            doc.Editor.WriteMessage("\nĐã vẽ lớp VXT_NHANDIEN chồng lên mặt bằng để đối chiếu. Gõ VXTHS_XOALOP để xoá lớp này.\n");
        }

        static void EraseLayer(Database db, Transaction tr, string layer)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                if (string.Equals(e.Layer, layer, StringComparison.OrdinalIgnoreCase)) { e.UpgradeOpen(); e.Erase(); }
            }
        }

        [CommandMethod("VXT", "VXTHS_XOALOP", CommandFlags.Modal)]
        public void ClearOverlay() => Guard(() =>
        {
            var doc = Doc;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                EraseLayer(doc.Database, tr, "VXT_NHANDIEN");
                tr.Commit();
            }
            doc.Editor.Regen();
        });

        // ================================================================ Đầu vào 2: từ file
        [CommandMethod("VXT", "VXTHS_FILE", CommandFlags.Modal)]
        public void FromFiles() => Guard(() =>
        {
            var st = AcadSession.Get(Doc);
            using (var f = new DwgPlansForm(Doc.Name))
            {
                if (!ShowDialog(f)) return;
                string tmp = Path.Combine(Path.GetTempPath(), "VXT_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(tmp);
                try
                {
                    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var p in f.Plans)
                    {
                        if (!map.TryGetValue(p.Path, out var dxf)) { dxf = ToDxf(p.Path, tmp, map.Count); map[p.Path] = dxf; }
                        p.Path = dxf;
                    }
                    var imp = new DxfArchImporter();
                    var arch = imp.Import(f.Plans);
                    if (string.IsNullOrWhiteSpace(st.Doc.ProjectName)) st.Doc.ProjectName = Path.GetFileNameWithoutExtension(Doc.Name);
                    arch.ProjectName = st.Doc.ProjectName;
                    st.Doc.PlanBases = new List<PlanBase>();
                    Finish(st, arch, imp.Log, imp.ScaleUsed);
                }
                finally { try { Directory.Delete(tmp, true); } catch { } }
            }
            ReviewArch();
        });

        static string ToDxf(string path, string tmpDir, int i)
        {
            if (path.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase)) return path;
            string outPath = Path.Combine(tmpDir, $"tang_{i + 1}.dxf");
            var open = AcApp.DocumentManager.Cast<Document>().FirstOrDefault(d => string.Equals(d.Name, path, StringComparison.OrdinalIgnoreCase));
            if (open != null) { open.Database.DxfOut(outPath, 8, DwgVersion.Current); return outPath; }
            if (!File.Exists(path)) throw new FileNotFoundException("Không thấy file " + path);
            using (var db = new Database(false, true))
            {
                db.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, "");
                db.CloseInput(true);
                db.DxfOut(outPath, 8, DwgVersion.Current);
            }
            return outPath;
        }

        // ================================================================ Đầu vào 3: nhập thông số
        [CommandMethod("VXT", "VXTHS_THONGSO", CommandFlags.Modal)]
        public void Parametric() => Guard(() =>
        {
            var st = AcadSession.Get(Doc);
            var p = new ParametricInput { ProjectName = string.IsNullOrWhiteSpace(st.Doc.ProjectName) ? "Công trình" : st.Doc.ProjectName };
            using (var f = new ObjectEditForm("Nhập thông số công trình", new ParametricView(p), 640, 560))
                if (!ShowDialog(f)) return;
            var arch = p.ToArch();
            st.Doc.ProjectName = p.ProjectName;
            st.Doc.PlanBases = new List<PlanBase>();
            var log = new List<string>(ArchAnalysis.Prepare(arch));
            var gen = new FramingGenerator(st.Settings, st.Framing);
            var m = gen.Generate(arch);
            p.ApplySections(m);
            st.Arch = arch; st.Model = m;
            AcadSession.ClearResult(Doc);
            AcadSession.Save(Doc);
            log.AddRange(gen.Report.Info); log.AddRange(gen.Report.Warnings.Select(w => "⚠ " + w));
            log.Add($"Mô hình: {m.Columns.Count} cột, {m.Beams.Count} dầm, {m.Slabs.Count} ô sàn.");
            log.Add("Cửa và phòng thêm ở mục Xem và sửa dữ liệu kiến trúc; thép đặt tay ở mục Sửa tiết diện và đặt thép tay.");
            Report("Nhập thông số", log);
        });

        // ================================================================ Đầu vào 4: ETABS / SAP2000
        [CommandMethod("VXT", "VXTHS_ETABS", CommandFlags.Modal)]
        public void FromEtabs() => Guard(() =>
        {
            var st = AcadSession.Get(Doc);
            ImportCsi(st);
        });

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static void ImportCsi(ProjectState st)
        {
            var prog = st.Engine == "SAP2000" ? CsiProgram.Sap2000 : CsiProgram.Etabs;
            using (var eng = new CsiEngine(prog))
            {
                eng.Connect(true);
                var m = eng.ImportModel(st.Settings);
                if (m.Combos.Any(c => !c.IsEnvelope))
                {
                    try { eng.ReadResults(m, m.Combos.Where(c => !c.IsEnvelope).Select(c => c.Name)); }
                    catch (InvalidOperationException ex) { eng.Log.Add("Chưa đọc được nội lực (mô hình đã chạy phân tích chưa?): " + ex.Message); }
                }
                var ans = MessageBox.Show($"Lấy diện tích thép từ kết quả thiết kế của {eng.Name}?\n\nCó: đổi diện tích thép đã thiết kế ra số thanh.\nKhông: tự tính thép theo TCVN 5574:2018 từ nội lực.", "VXT", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ans == DialogResult.Yes) eng.ReadDesignedRebar(m);
                st.Model = m;
                st.Arch = null;
                if (string.IsNullOrWhiteSpace(st.Doc.ProjectName)) st.Doc.ProjectName = Path.GetFileNameWithoutExtension(Doc.Name);
                AcadSession.ClearResult(Doc);
                AcadSession.Save(Doc);
                eng.Log.Add("Đầu vào này không có kiến trúc nên hồ sơ chỉ gồm phần kết cấu.");
                Report($"Đọc mô hình {eng.Name}", eng.Log);
            }
        }

        // ================================================================ Kiểm tra, sửa
        [CommandMethod("VXT", "VXTHS_KIEMTRA", CommandFlags.Modal)]
        public void ReviewArch() => Guard(() =>
        {
            var st = AcadSession.Get(Doc);
            if (st.Arch == null) throw new InvalidOperationException("Chưa có dữ liệu kiến trúc — chạy một bước đầu vào trước.");
            using (var f = new ReviewArchForm(st.Arch))
            {
                if (!ShowDialog(f)) return;
                var log = ArchAnalysis.Prepare(st.Arch);
                // kiến trúc đổi thì khung kết cấu sinh lại ở bước vẽ
                st.Model = null;
                AcadSession.ClearResult(Doc);
                AcadSession.Save(Doc);
                if (log.Count > 0) Report("Lưu ý sau khi sửa", log);
                DrawOverlay(Doc, st, 1);
            }
        });

        [CommandMethod("VXT", "VXTHS_SUAKC", CommandFlags.Modal)]
        public void EditStruct() => Guard(() =>
        {
            var st = AcadSession.Get(Doc);
            EnsureModel(st);
            using (var f = new EditStructForm(st.Model))
            {
                if (!ShowDialog(f)) return;
                AcadSession.ClearResult(Doc);
                AcadSession.Save(Doc);
            }
        });

        /// <summary>Sinh khung từ kiến trúc nếu chưa có mô hình.</summary>
        static void EnsureModel(ProjectState st)
        {
            if (st.Model != null) return;
            if (st.Arch == null) throw new InvalidOperationException("Chưa có dữ liệu — chạy một bước đầu vào trước.");
            ArchAnalysis.Prepare(st.Arch);
            var gen = new FramingGenerator(st.Settings, st.Framing);
            st.Model = gen.Generate(st.Arch);
            AcadSession.Save(Doc);
        }

        [CommandMethod("VXT", "VXTHS_THONGTIN", CommandFlags.Modal)]
        public void Info() => Guard(() =>
        {
            var st = AcadSession.Get(Doc);
            using (var f = new ObjectEditForm("Thông tin khung tên, khổ giấy, tỉ lệ", new DocOptionsView(st.Doc)))
                if (ShowDialog(f)) AcadSession.Save(Doc);
            using (var f = new SettingsForm(st))
                if (ShowDialog(f))
                {
                    if (st.Model != null) st.Model.Settings = st.Settings;
                    AcadSession.ClearResult(Doc);
                    AcadSession.Save(Doc);
                }
        });

        // ================================================================ Vẽ hồ sơ
        static DocumentSet BuildSet(ProjectState st, bool realDims)
        {
            bool needStruct = st.Doc.DrawStructure && (st.Model != null || st.Arch != null);
            WorkflowResult res = null;
            if (needStruct)
            {
                EnsureModel(st);
                res = AcadSession.Result(Doc) ?? AcadSession.Detail(Doc);
            }
            if (st.Arch == null && res == null) throw new InvalidOperationException("Chưa có dữ liệu — chạy một bước đầu vào trước.");
            return DocumentSet.Build(st.Arch, st.Model, res, st.Doc, realDims);
        }

        [CommandMethod("VXT", "VXTHS_VE", CommandFlags.Modal)]
        public void Draw() => Guard(() =>
        {
            var doc = Doc; var ed = doc.Editor;
            var st = AcadSession.Get(doc);
            using (var f = new ObjectEditForm("Thông tin khung tên, khổ giấy, tỉ lệ", new DocOptionsView(st.Doc)))
            {
                if (!ShowDialog(f)) return;
                AcadSession.Save(doc);
            }
            var set = BuildSet(st, true);
            if (set.Sheets.Count == 0) throw new InvalidOperationException("Không có hình nào để vẽ — kiểm tra lại lựa chọn Nội dung.");
            var pr = ed.GetPoint(new PromptPointOptions("\nChọn điểm đặt hồ sơ (vùng trống, bên phải bản vẽ gốc): "));
            if (pr.Status != PromptStatus.OK) return;
            int n;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var w = new AcadDraw2D(doc.Database, tr, pr.Value, 2.5 * st.Doc.PlanScale);
                set.Render(w);
                tr.Commit();
                n = w.Count;
            }
            var log = new List<string>
            {
                $"Đã vẽ {set.Sheets.Count} tờ, {n} đối tượng:",
            };
            log.AddRange(set.Sheets.Select(s => $"  {s.Number}  {s.Title}  ({s.Paper}, {s.ScaleText})"));
            if (st.Doc.CreateLayouts)
            {
                log.Add("");
                log.AddRange(AcadLayouts.Create(doc, set.Sheets, pr.Value));
            }
            var warn = set.Warnings.Distinct().ToList();
            var res = AcadSession.Result(doc);
            if (res != null) warn.AddRange(res.Warnings.Distinct());
            if (warn.Count > 0) { log.Add(""); log.Add("CẦN KIỂM TRA:"); log.AddRange(warn.Select(x => "• " + x)); }
            log.Add("");
            log.Add("Hồ sơ này là bản dựng tự động: kỹ sư, KTS kiểm tra và chịu trách nhiệm trước khi phát hành.");
            ed.Regen();
            ed.WriteMessage("\nGõ ZOOM → E để xem toàn bộ hồ sơ.\n");
            Report("Đã lập hồ sơ", log);
        });

        [CommandMethod("VXT", "VXTHS_DXF", CommandFlags.Modal)]
        public void ExportDxf() => Guard(() =>
        {
            var st = AcadSession.Get(Doc);
            var set = BuildSet(st, false);
            string dir = AcadSession.OutputFolder(Doc);
            using (var dlg = new SaveFileDialog { Filter = "DXF (*.dxf)|*.dxf", FileName = "Ho so thiet ke.dxf", InitialDirectory = dir })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                ShopDrawingDxf.WriteDocumentSet(set, dlg.FileName);
                Doc.Editor.WriteMessage($"\nĐã xuất {set.Sheets.Count} tờ: {dlg.FileName}\n");
            }
        });
    }

    /// <summary>Tạo một layout cho mỗi tờ: khổ giấy đúng, khung nhìn đúng tỉ lệ, sẵn sàng in PDF.</summary>
    public static class AcadLayouts
    {
        const string Plotter = "DWG To PDF.pc3";

        static string Media(string paper)
        {
            switch (paper)
            {
                case "A4": return "ISO_full_bleed_A4_(297.00_x_210.00_MM)";
                case "A3": return "ISO_full_bleed_A3_(420.00_x_297.00_MM)";
                case "A2": return "ISO_full_bleed_A2_(594.00_x_420.00_MM)";
                case "A1": return "ISO_full_bleed_A1_(841.00_x_594.00_MM)";
                default: return "ISO_full_bleed_A0_(1189.00_x_841.00_MM)";
            }
        }

        public static List<string> Create(Document doc, IEnumerable<SheetInfo> sheets, Point3d origin)
        {
            var log = new List<string>();
            int ok = 0;
            var lm = LayoutManager.Current;
            using (doc.LockDocument())
            {
                foreach (var s in sheets)
                {
                    try
                    {
                        CreateOne(doc, lm, s, origin);
                        ok++;
                    }
                    catch (System.Exception ex)
                    {
                        log.Add($"Không tạo được layout {s.Number}: {ex.Message}");
                    }
                }
                try { lm.CurrentLayout = "Model"; } catch { }
            }
            log.Insert(0, $"Đã tạo {ok} layout, mỗi tờ một layout, máy in {Plotter}. In loạt bằng lệnh PUBLISH.");
            return log;
        }

        static void CreateOne(Document doc, LayoutManager lm, SheetInfo s, Point3d origin)
        {
            var db = doc.Database;
            string name = s.Number;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var layouts = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                if (layouts.Contains(name)) lm.DeleteLayout(name);
                tr.Commit();
            }
            lm.CreateLayout(name);
            lm.CurrentLayout = name;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var layouts = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                var lay = (Layout)tr.GetObject(layouts.GetAt(name), OpenMode.ForWrite);
                try
                {
                    var psv = PlotSettingsValidator.Current;
                    psv.SetPlotConfigurationName(lay, Plotter, Media(s.Paper));
                    psv.SetPlotPaperUnits(lay, PlotPaperUnit.Millimeters);
                    psv.SetPlotType(lay, Autodesk.AutoCAD.DatabaseServices.PlotType.Layout);
                    psv.SetStdScaleType(lay, StdScaleType.StdScale1To1);
                    psv.SetUseStandardScale(lay, true);
                    psv.SetPlotRotation(lay, PlotRotation.Degrees000);
                    psv.SetPlotCentered(lay, false);
                }
                catch { /* máy không có DWG To PDF: layout vẫn dùng được, chọn máy in sau */ }

                lay.Initialize();
                var ids = lay.GetViewports();
                var btr = (BlockTableRecord)tr.GetObject(lay.BlockTableRecordId, OpenMode.ForWrite);
                Viewport vp = null;
                if (ids.Count >= 2) vp = (Viewport)tr.GetObject(ids[1], OpenMode.ForWrite);
                bool fresh = vp == null;
                if (fresh)
                {
                    vp = new Viewport();
                    btr.AppendEntity(vp);
                    tr.AddNewlyCreatedDBObject(vp, true);
                }
                vp.CenterPoint = new Point3d(s.PaperW / 2, s.PaperH / 2, 0);
                vp.Width = s.PaperW;
                vp.Height = s.PaperH;
                if (fresh) vp.On = true;
                vp.ViewCenter = new Point2d(origin.X + s.X + s.ModelW / 2, origin.Y + s.Y + s.ModelH / 2);
                vp.ViewHeight = s.ModelH;
                vp.CustomScale = 1.0 / s.Scale;
                vp.Locked = true;
                tr.Commit();
            }
        }
    }
}

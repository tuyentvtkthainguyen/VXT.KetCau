using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

[assembly: CommandClass(typeof(KetCauVXT.AcadLink.Commands))]
[assembly: ExtensionApplication(typeof(KetCauVXT.AcadLink.Plugin))]

namespace KetCauVXT.AcadLink
{
    /// <summary>Nạp add-in: in hướng dẫn ngắn.</summary>
    public class Plugin : IExtensionApplication
    {
        public void Initialize()
        {
            try
            {
                var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage("\nKết cấu VXT: đã nạp liên kết AutoCAD – gõ VXT để mở bảng lệnh (VXT_TUONG, VXT_COT, VXT_DAM, VXT_SAN, VXT_TRUC, VXT_CUA, VXT_THANG, VXT_TENPHONG, VXT_TANG, VXT_XEM, VXT_XOA, VXT_LAYER, VXT_GUI).\n");
            }
            catch (Exception) { }
        }

        public void Terminate() { }
    }

    /// <summary>Lệnh AutoCAD của Kết cấu VXT: tích chọn đối tượng → gán nhóm (tường, cột, dầm, sàn, trục, cửa, thang, tên phòng),
    /// khai báo tầng, xem/xoá gán nhóm, tạo chuẩn layer kết cấu, gửi dữ liệu sang chương trình Kết cấu VXT.</summary>
    public class Commands
    {
        private static Document Doc => AcApp.DocumentManager.MdiActiveDocument;

        private const string EntityTypes = "LINE,LWPOLYLINE,POLYLINE,ARC,CIRCLE,INSERT,TEXT,MTEXT,HATCH";

        private static SelectionFilter Filter() =>
            new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, EntityTypes) });

        private static string Up(string s) => (s ?? "").ToUpper(CultureInfo.CurrentCulture);

        // ==================================================================
        // BẢNG LỆNH
        // ==================================================================
        [CommandMethod("VXT", CommandFlags.Modal)]
        public void Menu() => VxtPalette.Show();

        [CommandMethod("VXT_MENU", CommandFlags.Modal)]
        public void Menu2() => VxtPalette.Show();

        // ==================================================================
        // GÁN NHÓM
        // ==================================================================
        [CommandMethod("VXT_TUONG", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void Tuong() => TagCommand(Cat.Tuong);

        [CommandMethod("VXT_COT", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void Cot() => TagCommand(Cat.Cot);

        [CommandMethod("VXT_DAM", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void Dam() => TagCommand(Cat.Dam);

        [CommandMethod("VXT_SAN", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void San() => TagCommand(Cat.San);

        [CommandMethod("VXT_TRUC", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void Truc() => TagCommand(Cat.Truc);

        [CommandMethod("VXT_CUA", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void Cua() => TagCommand(Cat.Cua);

        [CommandMethod("VXT_THANG", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void Thang() => TagCommand(Cat.Thang);

        [CommandMethod("VXT_TENPHONG", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void TenPhong() => TagCommand(Cat.TenPhong);

        [CommandMethod("VXT_BOQUA", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void BoQua() => TagCommand(Cat.BoQua);

        private static string AskKeyword(Editor ed, string message, string keywords, string def)
        {
            var o = new PromptKeywordOptions(message, keywords) { AllowNone = true };
            o.Keywords.Default = def;
            var r = ed.GetKeywords(o);
            if (r.Status == PromptStatus.None) return def;
            if (r.Status != PromptStatus.OK) return null;
            return string.IsNullOrEmpty(r.StringResult) ? def : r.StringResult;
        }

        private static ObjectId[] PickSet(Editor ed)
        {
            var imp = ed.SelectImplied();
            if (imp.Status == PromptStatus.OK && imp.Value != null && imp.Value.Count > 0)
            {
                var ids = imp.Value.GetObjectIds();
                ed.SetImpliedSelection(new ObjectId[0]);
                return ids;
            }
            return null;
        }

        private static ObjectId[] SelectObjects(Editor ed, string message)
        {
            var o = new PromptSelectionOptions { MessageForAdding = message };
            var r = ed.GetSelection(o, Filter());
            return r.Status == PromptStatus.OK ? r.Value.GetObjectIds() : null;
        }

        private static void TagCommand(string cat)
        {
            var doc = Doc;
            if (doc == null) return;
            var ed = doc.Editor; var db = doc.Database;
            string disp = Up(Cat.Display(cat));
            try
            {
                var ids = PickSet(ed);
                if (ids == null)
                {
                    string how = AskKeyword(ed, $"\nGán nhóm {disp} – cách chọn [DoiTuong/Layer] <DoiTuong>: ", "DoiTuong Layer", "DoiTuong");
                    if (how == null) return;
                    if (how == "Layer") { TagByLayer(doc, cat); return; }
                    ids = SelectObjects(ed, $"\nChọn các đối tượng {disp} (đường, đa tuyến, cung, tròn, block, chữ, miền tô): ");
                    if (ids == null) return;
                }
                int n = 0, locked = 0;
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    DwgStore.EnsureRegApp(db, tr);
                    foreach (var id in ids)
                    {
                        try
                        {
                            var e = tr.GetObject(id, OpenMode.ForWrite, false, true) as Entity;
                            if (e == null) continue;
                            DwgStore.SetTag(e, cat);
                            n++;
                        }
                        catch (Exception) { locked++; }
                    }
                    tr.Commit();
                }
                ed.WriteMessage($"\nĐã gán {n} đối tượng vào nhóm {disp}" + (locked > 0 ? $" ({locked} đối tượng không sửa được – bỏ qua)." : ".") +
                                " Định nghĩa được lưu trong bản vẽ (XData KETCAU_VXT).");
                VxtPalette.RefreshCounts();
            }
            catch (Exception ex) { ed.WriteMessage("\nLỗi gán nhóm: " + ex.Message); }
        }

        /// <summary>Chọn theo layer: chọn một đối tượng → mọi đối tượng trên layer đó thuộc nhóm (ghi nhớ layer → nhóm trong bản vẽ).</summary>
        private static void TagByLayer(Document doc, string cat)
        {
            var ed = doc.Editor; var db = doc.Database;
            string disp = Up(Cat.Display(cat));
            var layers = new List<string>();
            while (true)
            {
                var o = new PromptEntityOptions($"\nChọn một đối tượng trên layer {disp} (Enter = xong): ") { AllowNone = true };
                var r = ed.GetEntity(o);
                if (r.Status != PromptStatus.OK) break;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var e = tr.GetObject(r.ObjectId, OpenMode.ForRead) as Entity;
                    if (e != null && !layers.Contains(e.Layer, StringComparer.OrdinalIgnoreCase))
                    {
                        layers.Add(e.Layer);
                        ed.WriteMessage($"\n  Layer \"{e.Layer}\" → {disp}");
                    }
                    tr.Commit();
                }
            }
            if (layers.Count == 0) return;
            int count = 0;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var map = DwgStore.GetLayerMap(db, tr);
                foreach (var l in layers) map[l] = cat;
                DwgStore.SetLayerMap(db, tr, map);
                var set = new HashSet<string>(layers, StringComparer.OrdinalIgnoreCase);
                var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (e != null && set.Contains(e.Layer)) count++;
                }
                tr.Commit();
            }
            ed.WriteMessage($"\nĐã ghi nhớ {layers.Count} layer → nhóm {disp} ({count} đối tượng trên các layer này; đối tượng gán riêng bằng XData được ưu tiên).");
            VxtPalette.RefreshCounts();
        }

        // ==================================================================
        // TẦNG
        // ==================================================================
        [CommandMethod("VXT_TANG", CommandFlags.Modal)]
        public void Tang()
        {
            var doc = Doc;
            if (doc == null) return;
            var ed = doc.Editor; var db = doc.Database;
            try
            {
                List<StoreyDef> list;
                using (var tr = db.TransactionManager.StartTransaction()) { list = DwgStore.GetStoreys(db, tr); tr.Commit(); }
                PrintStoreys(ed, list);
                string act = AskKeyword(ed, "\nKhai báo tầng [Them/Danhsach/Xoa] <Them>: ", "Them Danhsach Xoa", "Them");
                if (act == null || act == "Danhsach") return;
                if (act == "Xoa")
                {
                    var ps = ed.GetString(new PromptStringOptions("\nTên tầng cần xoá: ") { AllowSpaces = true });
                    if (ps.Status != PromptStatus.OK) return;
                    var old = list.FirstOrDefault(s => string.Equals(s.Name, ps.StringResult.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (old == null) { ed.WriteMessage("\nKhông có tầng \"" + ps.StringResult + "\"."); return; }
                    list.Remove(old);
                    using (doc.LockDocument())
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        EraseFrame(db, tr, old.Frame);
                        DwgStore.SetStoreys(db, tr, list);
                        tr.Commit();
                    }
                    ed.WriteMessage("\nĐã xoá tầng \"" + old.Name + "\".");
                    VxtPalette.RefreshCounts();
                    return;
                }

                var last = list.OrderBy(s => s.Elevation).LastOrDefault();
                string defName = "Tầng " + (list.Count + 1).ToString(CultureInfo.InvariantCulture);
                var pn = ed.GetString(new PromptStringOptions($"\nTên tầng (vd. Tầng 1, Tầng 2, Tum, Mái) <{defName}>: ") { AllowSpaces = true });
                if (pn.Status != PromptStatus.OK) return;
                string name = string.IsNullOrWhiteSpace(pn.StringResult) ? defName : pn.StringResult.Trim();
                var prev = list.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

                double defElev = prev?.Elevation ?? (last == null ? 0 : last.Elevation + (last.Height > 0 ? last.Height : 3.3));
                var pe = ed.GetDouble(new PromptDoubleOptions($"\nCao độ mặt sàn (m) <{defElev:0.000}>: ") { AllowNegative = true, AllowZero = true, AllowNone = true, DefaultValue = defElev, UseDefaultValue = true });
                if (pe.Status != PromptStatus.OK && pe.Status != PromptStatus.None) return;
                double elev = pe.Status == PromptStatus.OK ? pe.Value : defElev;

                double defH = prev?.Height ?? (list.Count == 0 ? 3.6 : 3.3);
                var ph = ed.GetDouble(new PromptDoubleOptions($"\nChiều cao tầng (m) <{defH:0.000}>: ") { AllowNegative = false, AllowZero = false, AllowNone = true, DefaultValue = defH, UseDefaultValue = true });
                if (ph.Status != PromptStatus.OK && ph.Status != PromptStatus.None) return;
                double h = ph.Status == PromptStatus.OK ? ph.Value : defH;

                var ucs = ed.CurrentUserCoordinateSystem;
                var pb = ed.GetPoint(new PromptPointOptions("\nChọn điểm gốc của tầng (giao trục đầu tiên, vd. 1–A – chọn cùng giao trục ở mọi tầng): "));
                if (pb.Status != PromptStatus.OK) return;
                var p1 = ed.GetPoint(new PromptPointOptions("\nGóc thứ nhất của vùng mặt bằng tầng (bao cả trục và ký hiệu trục): "));
                if (p1.Status != PromptStatus.OK) return;
                var p2 = ed.GetCorner(new PromptCornerOptions("\nGóc đối diện: ", p1.Value));
                if (p2.Status != PromptStatus.OK) return;
                Point3d b = pb.Value.TransformBy(ucs), c1 = p1.Value.TransformBy(ucs), c2 = p2.Value.TransformBy(ucs);

                var st = new StoreyDef
                {
                    Name = name, Elevation = elev, Height = h, BaseX = b.X, BaseY = b.Y,
                    X0 = Math.Min(c1.X, c2.X), Y0 = Math.Min(c1.Y, c2.Y), X1 = Math.Max(c1.X, c2.X), Y1 = Math.Max(c1.Y, c2.Y),
                };
                if (!st.Contains(st.BaseX, st.BaseY)) ed.WriteMessage("\nLưu ý: điểm gốc nằm ngoài vùng mặt bằng tầng.");
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    if (prev != null) { EraseFrame(db, tr, prev.Frame); list.Remove(prev); }
                    st.Frame = DrawFrame(db, tr, st);
                    list.Add(st);
                    DwgStore.SetStoreys(db, tr, list.OrderBy(s => s.Elevation));
                    tr.Commit();
                }
                ed.WriteMessage($"\n{(prev != null ? "Đã khai báo lại" : "Đã thêm")} tầng \"{name}\": cao độ {elev:+0.000;-0.000;±0.000} m, cao {h:0.000} m. Khung phạm vi vẽ trên layer {DwgStore.FrameLayer} (không in).");
                VxtPalette.RefreshCounts();
            }
            catch (Exception ex) { ed.WriteMessage("\nLỗi khai báo tầng: " + ex.Message); }
        }

        private static void PrintStoreys(Editor ed, List<StoreyDef> list)
        {
            if (list.Count == 0) { ed.WriteMessage("\nChưa khai báo tầng nào."); return; }
            ed.WriteMessage($"\nCác tầng đã khai báo ({list.Count}):");
            foreach (var s in list.OrderBy(s => s.Elevation))
                ed.WriteMessage($"\n  {s.Name}: cao độ {s.Elevation:+0.000;-0.000;±0.000} m, cao {s.Height:0.000} m, gốc ({s.BaseX:0.##}; {s.BaseY:0.##})");
        }

        private static ObjectId EnsureFrameLayer(Database db, Transaction tr)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(DwgStore.FrameLayer)) return lt[DwgStore.FrameLayer];
            var rec = new LayerTableRecord { Name = DwgStore.FrameLayer, Color = Color.FromColorIndex(ColorMethod.ByAci, 8), IsPlottable = false };
            lt.UpgradeOpen();
            var id = lt.Add(rec);
            tr.AddNewlyCreatedDBObject(rec, true);
            return id;
        }

        private static string DrawFrame(Database db, Transaction tr, StoreyDef s)
        {
            var layer = EnsureFrameLayer(db, tr);
            var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
            var pl = new Polyline();
            pl.SetDatabaseDefaults(db);
            pl.AddVertexAt(0, new Point2d(s.X0, s.Y0), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(s.X1, s.Y0), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(s.X1, s.Y1), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(s.X0, s.Y1), 0, 0, 0);
            pl.Closed = true;
            pl.LayerId = layer;
            ms.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
            double th = Math.Max(1e-6, 0.025 * Math.Min(s.X1 - s.X0, s.Y1 - s.Y0));
            var t = new DBText();
            t.SetDatabaseDefaults(db);
            t.TextString = $"{s.Name} ({s.Elevation:+0.000;-0.000;±0.000})";
            t.Height = th;
            t.Position = new Point3d(s.X0 + th, s.Y1 + th * 0.5, 0);
            t.LayerId = layer;
            ms.AppendEntity(t);
            tr.AddNewlyCreatedDBObject(t, true);
            // dấu điểm gốc
            var c = new Circle(new Point3d(s.BaseX, s.BaseY, 0), Vector3d.ZAxis, th * 0.6);
            c.SetDatabaseDefaults(db);
            c.LayerId = layer;
            ms.AppendEntity(c);
            tr.AddNewlyCreatedDBObject(c, true);
            return pl.Handle.ToString() + ";" + t.Handle.ToString() + ";" + c.Handle.ToString();
        }

        private static void EraseFrame(Database db, Transaction tr, string handles)
        {
            if (string.IsNullOrEmpty(handles)) return;
            foreach (var hs in handles.Split(';'))
            {
                try
                {
                    long v;
                    if (!long.TryParse(hs, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) continue;
                    ObjectId id;
                    if (!db.TryGetObjectId(new Handle(v), out id) || id.IsNull || id.IsErased) continue;
                    var e = tr.GetObject(id, OpenMode.ForWrite, false, true) as Entity;
                    e?.Erase();
                }
                catch (Exception) { }
            }
        }

        // ==================================================================
        // QUÉT BẢN VẼ
        // ==================================================================
        /// <summary>Nhóm hiệu lực của mọi đối tượng model space (XData hoặc bảng layer của bản vẽ).</summary>
        internal static Dictionary<string, List<ObjectId>> Scan(Database db, Transaction tr, out Dictionary<string, string> layerMap)
        {
            layerMap = DwgStore.GetLayerMap(db, tr);
            var res = Cat.All.ToDictionary(c => c, c => new List<ObjectId>());
            var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (e == null) continue;
                string c = DwgStore.Effective(e, layerMap, null);
                if (c != null) res[c].Add(id);
            }
            return res;
        }

        [CommandMethod("VXT_XEM", CommandFlags.Modal | CommandFlags.Redraw)]
        public void Xem()
        {
            var doc = Doc;
            if (doc == null) return;
            var ed = doc.Editor; var db = doc.Database;
            try
            {
                Dictionary<string, List<ObjectId>> scan;
                Dictionary<string, string> map;
                List<StoreyDef> storeys;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    scan = Scan(db, tr, out map);
                    storeys = DwgStore.GetStoreys(db, tr);
                    tr.Commit();
                }
                ed.WriteMessage("\nĐối tượng theo nhóm: " + string.Join(", ", Cat.All.Select(c => $"{Cat.Display(c)} {scan[c].Count}")));
                if (map.Count > 0) ed.WriteMessage("\nLayer gán nhóm: " + string.Join(", ", map.Select(kv => $"{kv.Key} → {Cat.Display(kv.Value)}")));
                PrintStoreys(ed, storeys);
                string k = AskKeyword(ed, "\nTô sáng nhóm [Tuong/Cot/Dam/San/Truc/Cua/Thang/TenPhong/BoQua/TatCa] <TatCa>: ",
                    "Tuong Cot Dam San Truc Cua Thang TenPhong BoQua TatCa", "TatCa");
                if (k == null) return;
                var ids = (k == "TatCa" ? Cat.All.Where(c => c != Cat.BoQua).SelectMany(c => scan[c]) : scan[Cat.Normalize(k) ?? Cat.Tuong]).ToArray();
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var id in ids) (tr.GetObject(id, OpenMode.ForRead) as Entity)?.Highlight();
                    tr.Commit();
                }
                ed.SetImpliedSelection(ids);
                ed.WriteMessage($"\nĐã tô sáng và chọn sẵn {ids.Length} đối tượng ({(k == "TatCa" ? "tất cả nhóm" : Cat.Display(Cat.Normalize(k)))}). Bấm Esc hoặc gõ REGEN để bỏ.");
            }
            catch (Exception ex) { ed.WriteMessage("\nLỗi: " + ex.Message); }
        }

        [CommandMethod("VXT_XOA", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void Xoa()
        {
            var doc = Doc;
            if (doc == null) return;
            var ed = doc.Editor; var db = doc.Database;
            try
            {
                var ids = PickSet(ed);
                if (ids == null)
                {
                    string how = AskKeyword(ed, "\nXoá gán nhóm [DoiTuong/Layer/TatCa] <DoiTuong>: ", "DoiTuong Layer TatCa", "DoiTuong");
                    if (how == null) return;
                    if (how == "Layer")
                    {
                        var r = ed.GetEntity(new PromptEntityOptions("\nChọn một đối tượng trên layer cần bỏ gán nhóm: "));
                        if (r.Status != PromptStatus.OK) return;
                        using (doc.LockDocument())
                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            var e = (Entity)tr.GetObject(r.ObjectId, OpenMode.ForRead);
                            var map = DwgStore.GetLayerMap(db, tr);
                            bool had = map.Remove(e.Layer);
                            DwgStore.SetLayerMap(db, tr, map);
                            tr.Commit();
                            ed.WriteMessage(had ? $"\nĐã bỏ gán nhóm cho layer \"{e.Layer}\"." : $"\nLayer \"{e.Layer}\" chưa được gán nhóm.");
                        }
                        VxtPalette.RefreshCounts();
                        return;
                    }
                    if (how == "TatCa")
                    {
                        if (AskKeyword(ed, "\nXoá MỌI gán nhóm (XData và bảng layer) trong bản vẽ? [Co/Khong] <Khong>: ", "Co Khong", "Khong") != "Co") return;
                        int n = 0;
                        using (doc.LockDocument())
                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            DwgStore.EnsureRegApp(db, tr);
                            var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                            foreach (ObjectId id in ms)
                            {
                                var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                                if (e == null || DwgStore.GetTag(e) == null) continue;
                                try { e.UpgradeOpen(); DwgStore.SetTag(e, null); n++; } catch (Exception) { }
                            }
                            DwgStore.SetLayerMap(db, tr, new Dictionary<string, string>());
                            tr.Commit();
                        }
                        ed.WriteMessage($"\nĐã xoá gán nhóm của {n} đối tượng và bảng layer → nhóm.");
                        VxtPalette.RefreshCounts();
                        return;
                    }
                    ids = SelectObjects(ed, "\nChọn các đối tượng cần bỏ gán nhóm: ");
                    if (ids == null) return;
                }
                int cnt = 0;
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    DwgStore.EnsureRegApp(db, tr);
                    foreach (var id in ids)
                    {
                        try
                        {
                            var e = tr.GetObject(id, OpenMode.ForWrite, false, true) as Entity;
                            if (e == null || DwgStore.GetTag(e) == null) continue;
                            DwgStore.SetTag(e, null);
                            cnt++;
                        }
                        catch (Exception) { }
                    }
                    tr.Commit();
                }
                ed.WriteMessage($"\nĐã bỏ gán nhóm {cnt} đối tượng (đối tượng trên layer đã gán nhóm vẫn theo layer – dùng VXT_XOA → Layer).");
                VxtPalette.RefreshCounts();
            }
            catch (Exception ex) { ed.WriteMessage("\nLỗi: " + ex.Message); }
        }

        // ==================================================================
        // GỬI SANG KẾT CẤU VXT
        // ==================================================================
        internal static string SourceName()
        {
            try
            {
                var v = AcApp.Version;
                int year = v.Major == 24 ? 2021 + v.Minor : v.Major == 25 ? 2025 + v.Minor : v.Major == 26 ? 2027 + v.Minor : 0;
                return year > 0 ? "AutoCAD " + year.ToString(CultureInfo.InvariantCulture) : $"AutoCAD R{v.Major}.{v.Minor}";
            }
            catch (Exception) { return "AutoCAD"; }
        }

        [CommandMethod("VXT_GUI", CommandFlags.Modal)]
        public void Gui()
        {
            var doc = Doc;
            if (doc == null) return;
            var ed = doc.Editor; var db = doc.Database;
            try
            {
                List<StoreyDef> storeys;
                Dictionary<string, string> map;
                int tagged = 0;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    storeys = DwgStore.GetStoreys(db, tr).OrderBy(s => s.Elevation).ToList();
                    var scan = Scan(db, tr, out map);
                    tagged = scan.Values.Sum(l => l.Count);
                    tr.Commit();
                }
                bool wholeDrawing = false;
                if (storeys.Count == 0)
                {
                    if (AskKeyword(ed, "\nChưa khai báo tầng (VXT_TANG). Gửi toàn bộ bản vẽ thành 1 tầng? [Co/Khong] <Co>: ", "Co Khong", "Co") != "Co") return;
                    storeys.Add(new StoreyDef { Name = "Tầng 1", Elevation = 0, Height = 0, X0 = db.Extmin.X, Y0 = db.Extmin.Y, X1 = db.Extmax.X, Y1 = db.Extmax.Y });
                    wholeDrawing = true;
                }
                string defRules = tagged == 0 ? "Co" : "Khong";
                string useRules = AskKeyword(ed, $"\nĐối tượng chưa gán nhóm: nhận theo tên layer (chuẩn layer Kết cấu VXT)? [Co/Khong] <{defRules}>: ", "Co Khong", defRules);
                if (useRules == null) return;
                var msgs = new List<string>();
                var std = useRules == "Co" ? LayerStd.Load(msgs) : null;
                foreach (var m in msgs) ed.WriteMessage("\n" + m);

                string unitName;
                double k = Exchange.UnitToM((int)db.Insunits, out unitName);
                var cols = new List<Collector>();
                int outside = 0, total = 0, skipped = 0;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var s in storeys) cols.Add(new Collector(tr, k));
                    var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId lid in lt)
                    {
                        var l = (LayerTableRecord)tr.GetObject(lid, OpenMode.ForRead);
                        if (l.IsFrozen || (std != null && l.IsOff)) hidden.Add(l.Name);
                    }
                    var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                    foreach (ObjectId id in ms)
                    {
                        var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (e == null || string.Equals(e.Layer, DwgStore.FrameLayer, StringComparison.OrdinalIgnoreCase) || hidden.Contains(e.Layer)) continue;
                        string cat = DwgStore.Effective(e, map, std);
                        if (cat == null || cat == Cat.BoQua) continue;
                        double x, y;
                        int si = -1;
                        if (wholeDrawing) si = 0;
                        else if (Collector.RefPoint(e, out x, out y))
                        {
                            double best = double.MaxValue;
                            for (int i = 0; i < storeys.Count; i++)
                            {
                                var s = storeys[i];
                                if (!s.Contains(x, y)) continue;
                                double area = Math.Abs((s.X1 - s.X0) * (s.Y1 - s.Y0));
                                if (area < best) { best = area; si = i; }
                            }
                        }
                        if (si < 0) { outside++; continue; }
                        cols[si].Add(e, cat);
                        total++;
                    }
                    skipped = cols.Sum(c => c.Skipped);
                    tr.Commit();
                }
                if (total == 0)
                {
                    ed.WriteMessage("\nKhông có đối tượng nào đã gán nhóm trong phạm vi các tầng. Dùng VXT_TUONG, VXT_COT, … để gán, VXT_TANG để khai báo tầng.");
                    return;
                }
                string path = Exchange.Write(SourceName(), doc.Name, storeys, cols.Select(c => c.Items).ToList(), k);
                ed.WriteMessage($"\nĐơn vị bản vẽ: {unitName} (×{k.ToString(CultureInfo.InvariantCulture)} → m).");
                for (int i = 0; i < storeys.Count; i++)
                {
                    var items = cols[i].Items;
                    ed.WriteMessage($"\n  {storeys[i].Name}: {items.Count} đối tượng – " +
                                    string.Join(", ", Cat.All.Select(c => new { c, n = items.Count(t => t.Category == c && t.Kind != "Block") }).Where(q => q.n > 0).Select(q => $"{Cat.Display(q.c)} {q.n}")));
                }
                if (outside > 0) ed.WriteMessage($"\n  {outside} đối tượng đã gán nhóm nằm ngoài mọi tầng – bỏ qua.");
                if (skipped > 0) ed.WriteMessage($"\n  {skipped} đối tượng con không hỗ trợ (elip, spline, …) – bỏ qua.");
                ed.WriteMessage("\nĐã ghi " + path);
                ed.WriteMessage("\n" + Exchange.Notify(path));
            }
            catch (Exception ex) { ed.WriteMessage("\nLỗi gửi dữ liệu: " + ex.Message); }
        }

        // ==================================================================
        // CHUẨN LAYER KẾT CẤU
        // ==================================================================
        private static readonly int[] LwValues = { 0, 5, 9, 13, 15, 18, 20, 25, 30, 35, 40, 50, 53, 60, 70, 80, 90, 100, 106, 120, 140, 158, 200, 211 };

        private static LineWeight ToLw(double v)
        {
            if (v <= 0) return LineWeight.ByLineWeightDefault;
            double x = v <= 2.11 ? v * 100 : v;     // mm → 1/100 mm
            int best = LwValues.OrderBy(q => Math.Abs(q - x)).First();
            return (LineWeight)best;
        }

        private static string SafeName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "0";
            var c = s.Trim().ToCharArray();
            for (int i = 0; i < c.Length; i++) if ("<>/\\\":;?*|=`,".IndexOf(c[i]) >= 0 || c[i] < 32) c[i] = '_';
            return new string(c);
        }

        private static ObjectId EnsureLinetype(Database db, Transaction tr, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Equals("BYLAYER", StringComparison.OrdinalIgnoreCase)) name = "Continuous";
            var lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            if (lt.Has(name)) return lt[name];
            foreach (var file in new[] { "acadiso.lin", "acad.lin" })
            {
                try { db.LoadLineTypeFile(name, file); } catch (Exception) { }
                lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                if (lt.Has(name)) return lt[name];
            }
            return lt["Continuous"];
        }

        private static ObjectId EnsureLayer(Database db, Transaction tr, OutLayer o)
        {
            var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            string name = SafeName(o.Name);
            bool isNew = !table.Has(name);
            var rec = isNew ? new LayerTableRecord { Name = name } : (LayerTableRecord)tr.GetObject(table[name], OpenMode.ForWrite);
            rec.Color = Color.FromColorIndex(ColorMethod.ByAci, (short)Math.Max(1, Math.Min(255, o.Color)));
            rec.LinetypeObjectId = EnsureLinetype(db, tr, o.Linetype);
            rec.LineWeight = ToLw(o.Lineweight);
            rec.IsPlottable = o.Plot;
            ObjectId id;
            if (isNew)
            {
                table.UpgradeOpen();
                id = table.Add(rec);
                tr.AddNewlyCreatedDBObject(rec, true);
            }
            else id = rec.ObjectId;
            if (!string.IsNullOrEmpty(o.Description)) { try { rec.Description = o.Description; } catch (Exception) { } }
            return id;
        }

        private static ObjectId EnsureTextStyle(Database db, Transaction tr, string styleName, string face)
        {
            var tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            TextStyleTableRecord ts;
            if (tst.Has(styleName)) ts = (TextStyleTableRecord)tr.GetObject(tst[styleName], OpenMode.ForWrite);
            else
            {
                ts = new TextStyleTableRecord { Name = styleName };
                tst.UpgradeOpen();
                tst.Add(ts);
                tr.AddNewlyCreatedDBObject(ts, true);
            }
            ts.Font = new Autodesk.AutoCAD.GraphicsInterface.FontDescriptor(face, false, false, 0, 0);
            ts.TextSize = 0;
            ts.XScale = 1.0;
            return ts.ObjectId;
        }

        // ---- kiểu kích thước VXT_1-25… theo CadStandard.json (mục "Drawing") ----
        /// <summary>Tạo/cập nhật DIMSTYLE theo tỷ lệ: DIMSCALE = mẫu số, thông số mm giấy; gạch xiên (DIMTSZ = ½ chiều dài gạch) hoặc mũi tên đặc.</summary>
        private static int EnsureDimStyles(Database db, Transaction tr, IEnumerable<DimStyleA> styles, ObjectId textStyle)
        {
            var dst = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
            int n = 0;
            foreach (var d in styles)
            {
                if (string.IsNullOrEmpty(d.Name)) continue;
                DimStyleTableRecord r;
                bool isNew = !dst.Has(d.Name);
                if (isNew) r = new DimStyleTableRecord { Name = d.Name };
                else r = (DimStyleTableRecord)tr.GetObject(dst[d.Name], OpenMode.ForWrite);
                r.Dimscale = d.Scale;
                r.Dimtxt = d.TextHeight;
                r.Dimasz = d.TickSize;
                r.Dimtsz = d.Arrow ? 0.0 : d.TickSize / 2;
                r.Dimexo = d.ExtOffset;
                r.Dimexe = d.ExtBeyond;
                r.Dimdle = d.Arrow ? 0.0 : d.LineExtend;
                r.Dimdli = d.Baseline;
                r.Dimgap = d.TextGap;
                r.Dimlfac = d.LinearFactor;
                r.Dimtad = 1;
                r.Dimtih = false;
                r.Dimtoh = false;
                r.Dimdec = Math.Max(0, Math.Min(8, d.Decimals));
                if (!textStyle.IsNull) r.Dimtxsty = textStyle;
                if (isNew)
                {
                    dst.UpgradeOpen();
                    dst.Add(r);
                    tr.AddNewlyCreatedDBObject(r, true);
                }
                n++;
            }
            return n;
        }
        // ---- hết kiểu kích thước ----

        [CommandMethod("VXT_LAYER", CommandFlags.Modal)]
        public void LayerStandard()
        {
            var doc = Doc;
            if (doc == null) return;
            var ed = doc.Editor; var db = doc.Database;
            try
            {
                var msgs = new List<string>();
                var std = LayerStd.Load(msgs);
                foreach (var m in msgs) ed.WriteMessage("\n" + m);
                if (std.SourceFile.Length == 0) ed.WriteMessage($"\nKhông có {LayerStd.FilePath} – dùng chuẩn layer mặc định của add-in (lưu chuẩn layer trong chương trình Kết cấu VXT để dùng chung).");

                var catLayers = Cat.All.Where(c => c != Cat.BoQua).Select(c => new { c, o = std.ForCategory(c) }).Where(q => q.o != null).ToList();
                var ids = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var q in catLayers) ids[q.c] = EnsureLayer(db, tr, q.o);
                    foreach (var o in std.OutputLayers) EnsureLayer(db, tr, o);
                    tr.Commit();
                }
                ed.WriteMessage($"\nĐã tạo/cập nhật {catLayers.Count + std.OutputLayers.Count} layer: " +
                                string.Join(", ", catLayers.Select(q => SafeName(q.o.Name)).Concat(std.OutputLayers.Select(o => SafeName(o.Name))).Distinct()));

                if (AskKeyword(ed, "\nChuyển đối tượng đã gán nhóm sang layer kết cấu tương ứng (KC-…)? [Co/Khong] <Khong>: ", "Co Khong", "Khong") == "Co")
                {
                    int n = 0;
                    using (doc.LockDocument())
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        DwgStore.EnsureRegApp(db, tr);
                        Dictionary<string, string> map;
                        var scan = Scan(db, tr, out map);
                        foreach (var q in catLayers)
                            foreach (var id in scan[q.c])
                            {
                                try
                                {
                                    var e = (Entity)tr.GetObject(id, OpenMode.ForWrite, false, true);
                                    if (DwgStore.GetTag(e) == null) DwgStore.SetTag(e, q.c);   // giữ nhóm khi đổi layer
                                    e.LayerId = ids[q.c];
                                    n++;
                                }
                                catch (Exception) { }
                            }
                        tr.Commit();
                    }
                    ed.WriteMessage($"\nĐã chuyển {n} đối tượng sang layer kết cấu (nhóm được giữ bằng XData).");
                }

                string defFont = LayerStd.NormName(std.FontName).StartsWith("TIMES", StringComparison.Ordinal) ? "Times" : "Arial";
                string f = AskKeyword(ed, $"\nKiểu chữ bản vẽ [Arial/Times/Khong] <{defFont}>: ", "Arial Times Khong", defFont);
                if (f == "Arial" || f == "Times")
                {
                    string style = f == "Arial" ? "VXT_ARIAL" : "VXT_TIMES";
                    string face = f == "Arial" ? "Arial" : "Times New Roman";
                    using (doc.LockDocument())
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        db.Textstyle = EnsureTextStyle(db, tr, style, face);
                        tr.Commit();
                    }
                    ed.WriteMessage($"\nĐã tạo kiểu chữ {style} ({face}) và đặt làm hiện hành.");
                }
                // ---- kiểu kích thước VXT_1-15 … VXT_1-250 (chữ theo VXT_ARIAL/VXT_TIMES) ----
                if (std.DimStyles.Count > 0)
                {
                    int nd;
                    using (doc.LockDocument())
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        string tsName = f == "Times" || (f != "Arial" && defFont == "Times") ? "VXT_TIMES" : "VXT_ARIAL";
                        string tsFace = tsName == "VXT_TIMES" ? "Times New Roman" : "Arial";
                        var tsId = EnsureTextStyle(db, tr, tsName, tsFace);
                        nd = EnsureDimStyles(db, tr, std.DimStyles, tsId);
                        tr.Commit();
                    }
                    ed.WriteMessage($"\nĐã tạo/cập nhật {nd} kiểu kích thước: " + string.Join(", ", std.DimStyles.Select(q => q.Name)) + ".");
                }
                VxtPalette.RefreshCounts();
            }
            catch (Exception ex) { ed.WriteMessage("\nLỗi tạo chuẩn layer: " + ex.Message); }
        }
    }
}

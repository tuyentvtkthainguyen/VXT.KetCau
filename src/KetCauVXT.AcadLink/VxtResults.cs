using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

[assembly: CommandClass(typeof(KetCauVXT.AcadLink.VxtResults))]

namespace KetCauVXT.AcadLink
{
    /// <summary>Lệnh trao đổi hai chiều với Việt Const:
    /// • VXT_MATDUNG – đọc cao độ (+3.600, ±0.000, COS +7.200…) trên MẶT ĐỨNG / MẶT CẮT → cao độ, chiều cao các tầng đã khai báo (VXT_TANG);
    ///   chưa khai báo tầng → tạo các tầng điển hình dùng chung một mặt bằng.
    /// • VXT_NHAN – vẽ kết quả Việt Const gửi về (tên cấu kiện, tiết diện, thép, khung cột / móng / đài cọc) lên các lớp VXT-KQ-….
    /// • VXT_CHEN – chèn bản vẽ chi tiết rời (không khung tên, mỗi chi tiết một khung bao) để người dùng tự dàn trang in.
    /// Chỉ đọc / ghi file cục bộ trong %AppData%\KetCauVXT, không gửi dữ liệu đi đâu.</summary>
    public class VxtResults
    {
        private static Document Doc => AcApp.DocumentManager.MdiActiveDocument;
        private static string BackDir => Path.Combine(LayerStd.AppDataDir, "Link", "Back");

        private static string AskKeyword(Editor ed, string message, string keywords, string def)
        {
            var o = new PromptKeywordOptions(message, keywords) { AllowNone = true };
            o.Keywords.Default = def;
            var r = ed.GetKeywords(o);
            if (r.Status == PromptStatus.None) return def;
            if (r.Status != PromptStatus.OK) return null;
            return string.IsNullOrEmpty(r.StringResult) ? def : r.StringResult;
        }

        // ==================================================================
        // VXT_MATDUNG
        // ==================================================================
        private static readonly Regex LevelRx = new Regex(
            @"(?<pre>(?:c[oố]t|cos|code|el\.?|elev\.?|▽|▼|∇)\s*)?(?<sgn>[+\-±−]|%%p|%%P)?\s*(?<int>\d{1,5})(?:[.,](?<dec>\d{1,3}))?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex NameRx = new Regex(@"(t[aầ]ng\s*(h[aầ]m\s*)?\d+|t[aầ]ng\s*(tr[eệ]t|l[ửu]ng|m[aá]i|tum|h[aầ]m)|m[aá]i|tum|tr[eệ]t|l[ửu]ng|h[aầ]m\s*\d*)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static bool TryLevel(string text, out double z)
        {
            z = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim();
            if (t.Length > 40) return false;
            foreach (Match m in LevelRx.Matches(t))
            {
                if (!m.Groups["sgn"].Success && !m.Groups["pre"].Success) continue;
                double v;
                if (!m.Groups["dec"].Success)
                {
                    if (!double.TryParse(m.Groups["int"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) continue;
                    v = v >= 100 ? v / 1000.0 : v;
                }
                else if (!double.TryParse(m.Groups["int"].Value + "." + m.Groups["dec"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) continue;
                string s = m.Groups["sgn"].Value;
                if (s == "-" || s == "−") v = -v;
                if (Math.Abs(v) > 300) continue;
                z = v;
                return true;
            }
            return false;
        }

        private static string TextOf(Entity e)
        {
            var dt = e as DBText;
            if (dt != null) return dt.TextString;
            var mt = e as MText;
            if (mt != null) return mt.Text;
            return null;
        }

        private static Point3d PosOf(Entity e)
        {
            var dt = e as DBText;
            if (dt != null) return dt.Position;
            var mt = e as MText;
            return mt != null ? mt.Location : Point3d.Origin;
        }

        [CommandMethod("VXT_MATDUNG", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void MatDung()
        {
            var doc = Doc;
            if (doc == null) return;
            var ed = doc.Editor; var db = doc.Database;
            try
            {
                var flt = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") });
                var r = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nChọn MẶT ĐỨNG / MẶT CẮT (quét cả cụm chữ cao độ +3.600, ±0.000 và tên tầng): " }, flt);
                if (r.Status != PromptStatus.OK) return;
                var marks = new List<(double Z, double X, double Y, double H)>();
                var texts = new List<(string T, double X, double Y, double H)>();
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var id in r.Value.GetObjectIds())
                    {
                        var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        string t = e == null ? null : TextOf(e);
                        if (string.IsNullOrWhiteSpace(t)) continue;
                        var p = PosOf(e);
                        double h = e is DBText ? ((DBText)e).Height : ((MText)e).TextHeight;
                        texts.Add((t, p.X, p.Y, h));
                        double z;
                        if (TryLevel(t, out z)) marks.Add((Math.Round(z, 3), p.X, p.Y, h));
                    }
                    tr.Commit();
                }
                var levels = marks.GroupBy(m => Math.Round(m.Z / 0.005)).OrderBy(g => g.Key).Select(g => g.First()).ToList();
                if (levels.Count < 2) { ed.WriteMessage("\nKhông đọc được đủ cao độ (cần ít nhất 2 chữ dạng +3.600, ±0.000, COS +7.200…)."); return; }
                // tên tầng cùng hàng
                Func<(double Z, double X, double Y, double H), string> nameOf = m =>
                {
                    string best = ""; double bd = double.MaxValue;
                    foreach (var t in texts)
                    {
                        var mm = NameRx.Match(t.T);
                        if (!mm.Success) continue;
                        double dy = Math.Abs(t.Y - m.Y);
                        if (dy > Math.Max(t.H, m.H) * 2.5) continue;
                        double d = dy * 10 + Math.Abs(t.X - m.X);
                        if (d < bd) { bd = d; best = mm.Value.Trim().ToUpper(CultureInfo.CurrentCulture); }
                    }
                    return best;
                };
                double b = levels.Any(l => Math.Abs(l.Z) < 0.0005) ? 0 : levels.Where(l => l.Z > -1.5).Select(l => l.Z).DefaultIfEmpty(levels[0].Z).Min();
                var use = new List<(double Z, double X, double Y, double H)> { levels.OrderBy(l => Math.Abs(l.Z - b)).First() };
                foreach (var l in levels.Where(l => l.Z > use[0].Z + 1e-6))
                    if (l.Z - use[use.Count - 1].Z >= 2.2 - 1e-6) use.Add(l);
                ed.WriteMessage($"\nĐọc được {levels.Count} cao độ: " + string.Join(", ", levels.Select(l => (l.Z >= 0 ? "+" : "") + l.Z.ToString("0.000", CultureInfo.InvariantCulture))));
                if (use.Count < 2) { ed.WriteMessage("\nKhông đủ cao độ tầng (các cao độ cách nhau ≥ 2,2 m)."); return; }
                var stories = new List<(string Name, double Elev, double H)>();
                for (int i = 0; i + 1 < use.Count; i++)
                {
                    string nm = nameOf(use[i]);
                    stories.Add((nm.Length > 0 ? nm : "Tầng " + (i + 1).ToString(CultureInfo.InvariantCulture), use[i].Z, Math.Round(use[i + 1].Z - use[i].Z, 3)));
                }
                foreach (var s in stories)
                    ed.WriteMessage($"\n  {s.Name}: cao độ {(s.Elev >= 0 ? "+" : "")}{s.Elev.ToString("0.000", CultureInfo.InvariantCulture)} m, cao {s.H.ToString("0.00", CultureInfo.InvariantCulture)} m");
                ed.WriteMessage($"\n  Mái: +{use[use.Count - 1].Z.ToString("0.000", CultureInfo.InvariantCulture)} m");

                List<StoreyDef> list;
                using (var tr = db.TransactionManager.StartTransaction()) { list = DwgStore.GetStoreys(db, tr); tr.Commit(); }
                if (list.Count > 0)
                {
                    var ord = list.OrderBy(s => s.Elevation).ToList();
                    int n = Math.Min(ord.Count, stories.Count);
                    for (int i = 0; i < n; i++) { ord[i].Elevation = stories[i].Elev; ord[i].Height = stories[i].H; }
                    using (doc.LockDocument())
                    using (var tr = db.TransactionManager.StartTransaction()) { DwgStore.SetStoreys(db, tr, list); tr.Commit(); }
                    ed.WriteMessage($"\nĐã gán cao độ + chiều cao cho {n} tầng đã khai báo (theo thứ tự cao độ)." + (ord.Count != stories.Count ? $" Lưu ý: bản vẽ khai báo {ord.Count} tầng, mặt đứng có {stories.Count} tầng." : ""));
                }
                else
                {
                    if (AskKeyword(ed, $"\nChưa khai báo tầng. Tạo {stories.Count} tầng ĐIỂN HÌNH dùng chung một mặt bằng? [Co/Khong] <Co>: ", "Co Khong", "Co") != "Co") return;
                    var p1 = ed.GetPoint("\nGóc thứ nhất khung mặt bằng điển hình: ");
                    if (p1.Status != PromptStatus.OK) return;
                    var p2 = ed.GetCorner("\nGóc đối diện: ", p1.Value);
                    if (p2.Status != PromptStatus.OK) return;
                    var pb = ed.GetPoint(new PromptPointOptions("\nĐiểm gốc (giao trục 1-A) <góc dưới trái>: ") { AllowNone = true });
                    if (pb.Status != PromptStatus.OK && pb.Status != PromptStatus.None) return;
                    var basePt = pb.Status == PromptStatus.OK ? pb.Value : new Point3d(Math.Min(p1.Value.X, p2.Value.X), Math.Min(p1.Value.Y, p2.Value.Y), 0);
                    foreach (var s in stories)
                        list.Add(new StoreyDef
                        {
                            Name = s.Name, Elevation = s.Elev, Height = s.H, BaseX = basePt.X, BaseY = basePt.Y,
                            X0 = Math.Min(p1.Value.X, p2.Value.X), Y0 = Math.Min(p1.Value.Y, p2.Value.Y), X1 = Math.Max(p1.Value.X, p2.Value.X), Y1 = Math.Max(p1.Value.Y, p2.Value.Y)
                        });
                    using (doc.LockDocument())
                    using (var tr = db.TransactionManager.StartTransaction()) { DwgStore.SetStoreys(db, tr, list); tr.Commit(); }
                    ed.WriteMessage($"\nĐã tạo {stories.Count} tầng điển hình dùng chung mặt bằng đã chọn – VXT_GUI gửi mặt bằng này cho mọi tầng.");
                }
                VxtPalette.RefreshCounts();
            }
            catch (Exception ex) { ed.WriteMessage("\nLỗi đọc mặt đứng: " + ex.Message); }
        }

        // ==================================================================
        // VXT_NHAN
        // ==================================================================
        private static string NewestPackage()
        {
            if (!Directory.Exists(BackDir)) return null;
            return new DirectoryInfo(BackDir).GetFiles("ketqua_*.json").OrderByDescending(f => f.LastWriteTimeUtc).Select(f => f.FullName).FirstOrDefault();
        }

        private static ObjectId Layer(Database db, Transaction tr, string name, short color)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name)) return lt[name];
            lt.UpgradeOpen();
            var rec = new LayerTableRecord { Name = name, Color = Color.FromColorIndex(ColorMethod.ByAci, color) };
            var id = lt.Add(rec);
            tr.AddNewlyCreatedDBObject(rec, true);
            return id;
        }

        private static ObjectId TextStyle(Database db, Transaction tr)
        {
            var ts = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            const string nm = "VXT_KQ";
            if (ts.Has(nm)) return ts[nm];
            ts.UpgradeOpen();
            var rec = new TextStyleTableRecord { Name = nm, FileName = "arial.ttf" };
            var id = ts.Add(rec);
            tr.AddNewlyCreatedDBObject(rec, true);
            return id;
        }

        private static short ColorOf(string layer)
        {
            if (layer.EndsWith("COT", StringComparison.OrdinalIgnoreCase)) return 1;
            if (layer.EndsWith("DAM", StringComparison.OrdinalIgnoreCase)) return 3;
            if (layer.EndsWith("SAN", StringComparison.OrdinalIgnoreCase)) return 4;
            if (layer.EndsWith("MONG", StringComparison.OrdinalIgnoreCase)) return 6;
            return 2;
        }

        [CommandMethod("VXT_NHAN", CommandFlags.Modal)]
        public void Nhan()
        {
            var doc = Doc;
            if (doc == null) return;
            var ed = doc.Editor; var db = doc.Database;
            try
            {
                string path = NewestPackage();
                if (path == null) { ed.WriteMessage("\nChưa có kết quả – trong Việt Const: Tệp → Gửi kết quả về AutoCAD (hoặc tab Triển khai → Kết quả về AutoCAD)."); return; }
                var root = MiniJson.Parse(File.ReadAllText(path));
                var items = MiniJson.Arr(root, "items");
                bool aligned = MiniJson.Bool(root, "aligned");
                ed.WriteMessage($"\nKết quả: {Path.GetFileName(path)} – {items.Count} đối tượng, dự án \"{MiniJson.Str(root, "project")}\"" + (aligned ? ", đúng toạ độ bản vẽ gốc." : ", theo toạ độ mô hình."));
                string unitName;
                double k = Exchange.UnitToM((int)db.Insunits, out unitName);
                if (k <= 0) k = 1;
                double ox = 0, oy = 0;
                if (!aligned)
                {
                    var pb = ed.GetPoint(new PromptPointOptions("\nĐiểm chèn (gốc toạ độ mô hình – giao trục đầu tiên) <0,0>: ") { AllowNone = true });
                    if (pb.Status == PromptStatus.Cancel) return;
                    if (pb.Status == PromptStatus.OK) { ox = pb.Value.X; oy = pb.Value.Y; }
                }
                string clear = AskKeyword(ed, "\nXoá kết quả cũ trên các lớp VXT-KQ-…? [Co/Khong] <Co>: ", "Co Khong", "Co");
                if (clear == null) return;
                int nT = 0, nP = 0, nDel = 0;
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                    if (clear == "Co")
                        foreach (ObjectId id in ms)
                        {
                            var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (e != null && e.Layer.StartsWith("VXT-KQ-", StringComparison.OrdinalIgnoreCase)) { e.UpgradeOpen(); e.Erase(); nDel++; }
                        }
                    var style = TextStyle(db, tr);
                    var layers = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
                    foreach (var it in items)
                    {
                        string lay = MiniJson.Str(it, "layer", "VXT-KQ-CHU");
                        if (!layers.ContainsKey(lay)) layers[lay] = Layer(db, tr, lay, ColorOf(lay));
                        string type = MiniJson.Str(it, "type", "text");
                        if (type == "text")
                        {
                            string txt = MiniJson.Str(it, "text");
                            if (string.IsNullOrEmpty(txt)) continue;
                            var t = new DBText
                            {
                                TextString = txt, Height = Math.Max(MiniJson.Num(it, "h", 0.25), 0.01) / k, Rotation = MiniJson.Num(it, "rot") * Math.PI / 180,
                                Position = new Point3d(MiniJson.Num(it, "x") / k + ox, MiniJson.Num(it, "y") / k + oy, 0), TextStyleId = style, Layer = lay
                            };
                            ms.AppendEntity(t); tr.AddNewlyCreatedDBObject(t, true); nT++;
                        }
                        else
                        {
                            var pts = MiniJson.Arr(it, "pts").OfType<List<object>>().Where(q => q.Count >= 2).Select(q => new Point2d(Convert.ToDouble(q[0], CultureInfo.InvariantCulture) / k + ox, Convert.ToDouble(q[1], CultureInfo.InvariantCulture) / k + oy)).ToList();
                            if (pts.Count < 2) continue;
                            var pl = new Polyline();
                            for (int i = 0; i < pts.Count; i++) pl.AddVertexAt(i, pts[i], 0, 0, 0);
                            pl.Closed = MiniJson.Bool(it, "closed");
                            pl.Layer = lay;
                            ms.AppendEntity(pl); tr.AddNewlyCreatedDBObject(pl, true); nP++;
                        }
                    }
                    tr.Commit();
                }
                ed.WriteMessage($"\nĐã vẽ {nT} ghi chú, {nP} khung" + (nDel > 0 ? $" (xoá {nDel} đối tượng kết quả cũ)" : "") + ". Lớp: VXT-KQ-COT, -DAM, -SAN, -MONG.");
                var det = MiniJson.Arr(root, "details");
                if (det.Count > 0) ed.WriteMessage($"\nCó {det.Count} bản vẽ chi tiết rời – gõ VXT_CHEN để chèn.");
            }
            catch (Exception ex) { ed.WriteMessage("\nLỗi nhận kết quả: " + ex.Message); }
        }

        // ==================================================================
        // VXT_CHEN
        // ==================================================================
        private static string SafeBlock(string s)
        {
            var c = (s ?? "CT").ToCharArray();
            for (int i = 0; i < c.Length; i++) if ("<>/\\\":;?*|=`,".IndexOf(c[i]) >= 0 || c[i] < 32) c[i] = '_';
            return "VXT_" + new string(c);
        }

        [CommandMethod("VXT_CHEN", CommandFlags.Modal)]
        public void Chen()
        {
            var doc = Doc;
            if (doc == null) return;
            var ed = doc.Editor; var db = doc.Database;
            try
            {
                string path = NewestPackage();
                var files = new List<string>();
                if (path != null) files.AddRange(MiniJson.Arr(MiniJson.Parse(File.ReadAllText(path)), "details").OfType<string>().Where(File.Exists));
                if (files.Count == 0) { ed.WriteMessage("\nChưa có bản vẽ chi tiết – trong Việt Const: Gửi kết quả về AutoCAD (xuất kèm chi tiết rời không khung tên)."); return; }
                for (int i = 0; i < files.Count; i++) ed.WriteMessage($"\n  {i + 1}. {Path.GetFileName(files[i])}");
                var ps = ed.GetString(new PromptStringOptions($"\nChèn bản vẽ số (1–{files.Count}, cách nhau bằng dấu phẩy) hoặc T = tất cả <T>: ") { AllowSpaces = false });
                if (ps.Status != PromptStatus.OK) return;
                var pick = new List<string>();
                string sres = (ps.StringResult ?? "").Trim();
                if (sres.Length == 0 || sres.Equals("T", StringComparison.OrdinalIgnoreCase)) pick.AddRange(files);
                else
                    foreach (var part in sres.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        int n;
                        if (int.TryParse(part, out n) && n >= 1 && n <= files.Count) pick.Add(files[n - 1]);
                    }
                if (pick.Count == 0) return;
                var pp = ed.GetPoint("\nĐiểm chèn (các bản vẽ xếp lần lượt sang phải): ");
                if (pp.Status != PromptStatus.OK) return;
                string unitName;
                double k = Exchange.UnitToM((int)db.Insunits, out unitName);
                if (k <= 0) k = 0.001;
                double sc = 0.001 / k;   // chi tiết vẽ theo mm
                double x = pp.Value.X, y = pp.Value.Y;
                int done = 0;
                using (doc.LockDocument())
                {
                    foreach (var f in pick)
                    {
                        try
                        {
                            using (var src = new Database(false, true))
                            {
                                if (f.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase)) src.ReadDwgFile(f, FileOpenMode.OpenForReadAndAllShare, true, null);
                                else src.DxfIn(f, null);
                                string bname = SafeBlock(Path.GetFileNameWithoutExtension(f));
                                using (var tr0 = db.TransactionManager.StartTransaction())
                                {
                                    var bt0 = (BlockTable)tr0.GetObject(db.BlockTableId, OpenMode.ForRead);
                                    int z = 1; string b0 = bname;
                                    while (bt0.Has(bname)) bname = b0 + "_" + (++z).ToString(CultureInfo.InvariantCulture);
                                    tr0.Commit();
                                }
                                var btrId = db.Insert(bname, src, true);
                                using (var tr = db.TransactionManager.StartTransaction())
                                {
                                    var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                                    var br = new BlockReference(new Point3d(x, y, 0), btrId) { ScaleFactors = new Scale3d(sc) };
                                    ms.AppendEntity(br); tr.AddNewlyCreatedDBObject(br, true);
                                    // đặt góc trái trên của bản vẽ tại điểm chèn, bản sau sang phải
                                    try
                                    {
                                        var ext = br.GeometricExtents;
                                        var mv = new Vector3d(x - ext.MinPoint.X, y - ext.MaxPoint.Y, 0);
                                        br.TransformBy(Matrix3d.Displacement(mv));
                                        x += (ext.MaxPoint.X - ext.MinPoint.X) + 20000 * 0.001 / k;
                                    }
                                    catch (Exception) { x += 1000 * 0.001 / k * 100; }
                                    tr.Commit();
                                }
                                done++;
                            }
                        }
                        catch (Exception ex) { ed.WriteMessage($"\n  Không chèn được {Path.GetFileName(f)}: {ex.Message}"); }
                    }
                }
                ed.WriteMessage($"\nĐã chèn {done} bản vẽ chi tiết (khối VXT_…, mỗi chi tiết có khung bao lớp KHUNG_CHI_TIET). Phá khối (EXPLODE) nếu cần sửa; tự dàn trang in bằng Layout / viewport.");
            }
            catch (Exception ex) { ed.WriteMessage("\nLỗi chèn chi tiết: " + ex.Message); }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using KetCauVXT.Import.Arch;

[assembly: Autodesk.AutoCAD.Runtime.CommandClass(typeof(KetCauVXT.AcadLink.LayerConvertCmd))]

namespace KetCauVXT.AcadLink
{
    using Autodesk.AutoCAD.ApplicationServices;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Runtime;
    using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
    using AcColor = Autodesk.AutoCAD.Colors.Color;
    using AcColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;
    using Exception = System.Exception;

    /// <summary>Lệnh VXT_CHUANLAYER (VXT_CL): chuyển layer bất kỳ của bản vẽ kiến trúc / kết cấu về LAYER CHUẨN Việt Const (VXT-TRUC, VXT-COT,
    /// VXT-TUONG-110/220, VXT-CUA-DI/SO, VXT-THANG, VXT-LO-SAN, VXT-TEN-PHONG, VXT-DAM, VXT-SAN, VXT-KICH-THUOC, VXT-BO-QUA) để chương trình đọc chắc chắn.
    /// Tự nhận theo tên layer + nội dung (cặp nét tường 110/220, chữ nhật cột, nét trục + vòng ký hiệu, block cửa, chữ tên phòng, kích thước);
    /// người dùng sửa trên bảng, chọn đối tượng trên bản vẽ để gán, lưu / mở bộ ánh xạ (%AppData%\VietConst\layer-profiles).
    /// Mặc định KHÔNG sửa bản vẽ gốc: tạo bản sao "&lt;tên&gt;_VXT.dwg" (+ .dxf) cạnh file. Mọi thao tác chỉ trên máy, không gửi dữ liệu đi đâu.</summary>
    public class LayerConvertCmd
    {
        private static Document Doc => AcApp.DocumentManager.MdiActiveDocument;

        // lựa chọn nhớ trong phiên AutoCAD
        internal static int Mode;               // 0 = tạo bản sao chuẩn hoá, 1 = chuyển đối tượng sang layer chuẩn, 2 = đổi tên layer (1:1) + chuyển phần còn lại
        internal static bool InBlocks;          // sửa layer đối tượng trong định nghĩa block (ảnh hưởng mọi block cùng tên)
        internal static bool DoorBlocks = true; // block cửa (CUA*, DOOR*, D1…) → layer cửa
        internal static bool BindXref = true;   // bản sao: bind xref đã nạp trước khi chuyển
        internal static bool SaveDxf = true;    // lưu thêm DXF để mở bằng "Mở mặt bằng CAD"
        internal static bool SendAfter;         // gọi VXT_GUI sau khi chuẩn hoá (chỉ khi sửa trực tiếp bản vẽ)
        internal static LayerProfile Profile;

        [CommandMethod("VXT_CHUANLAYER", CommandFlags.Modal)]
        public void ChuanLayer() => Run();

        [CommandMethod("VXT_CL", CommandFlags.Modal)]
        public void ChuanLayerTat() => Run();

        private sealed class LayerInfo
        {
            public LayerStats Stats;
            public LayerSuggestion Sugg;
            public System.Drawing.Color Swatch = System.Drawing.Color.White;
            public List<ObjectId> Ids = new List<ObjectId>();
            public bool Dependent;
        }

        private static void Run()
        {
            var doc = Doc;
            if (doc == null) return;
            var ed = doc.Editor; var db = doc.Database;
            try
            {
                var msgs = new List<string>();
                var std = LayerStd.Load(msgs);
                string unitName;
                double k = Exchange.UnitToM((int)db.Insunits, out unitName);
                ed.WriteMessage($"\nChuẩn hoá layer – đơn vị bản vẽ: {unitName}. Đang phân tích nội dung từng layer…");

                Dictionary<string, LayerInfo> info;
                List<string> xrefWarn;
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    info = Scan(db, tr, k, out xrefWarn);
                    tr.Commit();
                }
                foreach (var w in xrefWarn) ed.WriteMessage("\n" + w);
                if (info.Count == 0) { ed.WriteMessage("\nBản vẽ không có đối tượng trong model space."); return; }

                var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // layer người dùng bỏ tích "Chuyển" → giữ nguyên
                Func<string, string> userRule = l => std.Classify(l);
                void Resuggest()
                {
                    foreach (var li in info.Values) li.Sugg = LayerConvert.Suggest(li.Stats, Profile, userRule);
                }
                Resuggest();

                // vòng hộp thoại: chọn đối tượng / tô sáng xong thì mở lại
                while (true)
                {
                    var rows = info.Values.OrderByDescending(q => q.Stats.Total).ThenBy(q => q.Stats.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(q => new LayerConvertForm.Row
                        {
                            Layer = q.Stats.Name, Count = q.Ids.Count, Swatch = q.Swatch, Dependent = q.Dependent,
                            Key = overrides.ContainsKey(q.Stats.Name) ? overrides[q.Stats.Name] : q.Sugg.Key,
                            Confidence = overrides.ContainsKey(q.Stats.Name) ? 1 : q.Sugg.Confidence,
                            Reason = overrides.ContainsKey(q.Stats.Name) ? "người dùng chọn" : q.Sugg.Reason,
                            Convert = !skip.Contains(q.Stats.Name)
                        }).ToList();
                    LayerConvertForm.Result res;
                    using (var f = new LayerConvertForm(rows, Profile))
                    {
                        AcApp.ShowModalDialog(f);
                        res = f.Outcome;
                        foreach (var r in f.Rows)
                        {
                            if (r.UserSet) overrides[r.Layer] = r.Key;
                            if (r.Convert) skip.Remove(r.Layer); else skip.Add(r.Layer);
                        }
                        if (f.LoadedProfile != null) { Profile = f.LoadedProfile; overrides.Clear(); Resuggest(); }
                        if (f.ResetRequested) { overrides.Clear(); skip.Clear(); Resuggest(); }
                    }
                    if (res.Action == LayerConvertForm.ActionKind.Cancel) { ed.WriteMessage("\nĐã huỷ chuẩn hoá layer."); return; }
                    if (res.Action == LayerConvertForm.ActionKind.Again) continue;
                    if (res.Action == LayerConvertForm.ActionKind.Pick)
                    {
                        var o = new PromptSelectionOptions { MessageForAdding = $"\nChọn đối tượng – layer của chúng sẽ gán vào {(res.Key.Length == 0 ? "(giữ nguyên)" : StdLayers.Prefix + res.Key)}: " };
                        var sel = ed.GetSelection(o);
                        if (sel.Status == PromptStatus.OK)
                            using (var tr = db.TransactionManager.StartTransaction())
                            {
                                foreach (var id in sel.Value.GetObjectIds())
                                {
                                    var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                                    if (e != null) { overrides[e.Layer] = res.Key; if (res.Key.Length > 0) skip.Remove(e.Layer); }
                                }
                                tr.Commit();
                            }
                        continue;
                    }
                    if (res.Action == LayerConvertForm.ActionKind.Highlight)
                    {
                        LayerInfo li;
                        if (res.Layer != null && info.TryGetValue(res.Layer, out li) && li.Ids.Count > 0)
                        {
                            ed.SetImpliedSelection(li.Ids.ToArray());
                            ed.GetString(new PromptStringOptions($"\nĐang tô sáng {li.Ids.Count} đối tượng layer \"{res.Layer}\" – Enter để quay lại bảng: ") { AllowSpaces = true });
                            ed.SetImpliedSelection(new ObjectId[0]);
                        }
                        continue;
                    }
                    break;   // Apply
                }

                Func<string, string> keyOf = l =>
                {
                    if (l == null || skip.Contains(l)) return "";   // bỏ tích "Chuyển" → giữ nguyên layer
                    string v;
                    if (overrides.TryGetValue(l, out v)) return v ?? "";
                    LayerInfo li;
                    return info.TryGetValue(l, out li) ? li.Sugg.Key : "";
                };
                Apply(doc, keyOf, info.Keys.ToList());
            }
            catch (Exception ex) { ed.WriteMessage("\nLỗi chuẩn hoá layer: " + ex.Message); }
        }

        // ------------------------------------------------------------------
        // QUÉT
        // ------------------------------------------------------------------
        private static Dictionary<string, LayerInfo> Scan(Database db, Transaction tr, double k, out List<string> warnings)
        {
            warnings = new List<string>();
            var res = new Dictionary<string, LayerInfo>(StringComparer.OrdinalIgnoreCase);
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            var ltNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId lid in lt)
            {
                var rec = tr.GetObject(lid, OpenMode.ForRead) as LayerTableRecord;
                if (rec == null) continue;
                string ltn = "";
                try { var ltr = tr.GetObject(rec.LinetypeObjectId, OpenMode.ForRead) as LinetypeTableRecord; if (ltr != null) ltn = ltr.Name; } catch (Exception) { }
                ltNames[rec.Name] = ltn;
                var li = new LayerInfo { Stats = new LayerStats { Name = rec.Name, Linetype = ltn, Color = rec.Color.ColorIndex }, Dependent = rec.IsDependent };
                try { li.Swatch = rec.Color.ColorValue; } catch (Exception) { }
                res[rec.Name] = li;
            }
            // xref chưa nạp
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId bid in bt)
            {
                var btr = tr.GetObject(bid, OpenMode.ForRead) as BlockTableRecord;
                if (btr != null && btr.IsFromExternalReference && !btr.IsResolved)
                    warnings.Add($"Cảnh báo: xref \"{btr.Name}\" chưa nạp (Unloaded / Not found) – đối tượng của nó không được phân tích. Nạp lại xref (XREF → Reload) rồi chạy lại.");
            }

            LayerInfo Get(string layer)
            {
                LayerInfo li;
                if (!res.TryGetValue(layer ?? "0", out li)) { li = new LayerInfo { Stats = new LayerStats { Name = layer ?? "0" } }; res[layer ?? "0"] = li; }
                return li;
            }
            string Lt(Entity e)
            {
                string n = e.Linetype ?? "";
                if (n.Length == 0 || n.Equals("BYLAYER", StringComparison.OrdinalIgnoreCase)) { string v; return ltNames.TryGetValue(e.Layer ?? "", out v) ? v : ""; }
                return n;
            }
            int budget = 300000;
            void Add(Entity e, string ownerLayer, int depth)
            {
                if (e == null || budget-- <= 0) return;
                string layer = e.Layer ?? "0";
                if (depth > 0 && layer == "0" && ownerLayer != null) layer = ownerLayer;   // đối tượng layer 0 trong block lấy layer của block
                var s = Get(layer).Stats;
                if (e is Line)
                {
                    var l = (Line)e;
                    s.AddLine(l.StartPoint.X * k, l.StartPoint.Y * k, l.EndPoint.X * k, l.EndPoint.Y * k, Lt(e));
                }
                else if (e is Polyline)
                {
                    var pl = (Polyline)e;
                    var pts = new List<double[]>();
                    for (int i = 0; i < pl.NumberOfVertices; i++) { var p = pl.GetPoint2dAt(i); pts.Add(new[] { p.X * k, p.Y * k }); }
                    s.AddPolyline(pts, pl.Closed, Lt(e));
                }
                else if (e is Circle) { var c = (Circle)e; s.AddCircle(c.Center.X * k, c.Center.Y * k, c.Radius * k); }
                else if (e is Arc) s.AddArc(((Arc)e).Radius * k);
                else if (e is DBText) s.AddText(((DBText)e).TextString);
                else if (e is MText) s.AddText(((MText)e).Text);
                else if (e is Hatch) s.AddHatch();
                else if (e is Dimension) s.AddDimension();
                else if (e is BlockReference)
                {
                    var br = (BlockReference)e;
                    s.AddInsert(BlockName(tr, br));
                    if (depth < 2)
                    {
                        var parts = new DBObjectCollection();
                        try { br.Explode(parts); } catch (Exception) { }
                        foreach (DBObject o in parts) { Add(o as Entity, layer, depth + 1); o.Dispose(); }
                    }
                }
                else if (e is Polyline2d || e is Polyline3d)
                {
                    var parts = new DBObjectCollection();
                    try { e.Explode(parts); } catch (Exception) { }
                    foreach (DBObject o in parts) { Add(o as Entity, ownerLayer, depth + 1); o.Dispose(); }
                }
            }
            var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (e == null) continue;
                Get(e.Layer).Ids.Add(id);
                Add(e, null, 0);
            }
            if (budget <= 0) warnings.Add("Bản vẽ rất lớn – chỉ phân tích 300 000 đối tượng đầu (đề xuất vẫn dùng được).");
            // bỏ layer không có gì (vẫn giữ layer có đối tượng trong block)
            foreach (var key in res.Where(kv => kv.Value.Stats.Total == 0 && kv.Value.Ids.Count == 0).Select(kv => kv.Key).ToList()) res.Remove(key);
            return res;
        }

        private static string BlockName(Transaction tr, BlockReference br)
        {
            try
            {
                if (br.IsDynamicBlock)
                {
                    var btr = tr.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    if (btr != null) return btr.Name;
                }
            }
            catch (Exception) { }
            return br.Name ?? "";
        }

        // ------------------------------------------------------------------
        // ÁP DỤNG
        // ------------------------------------------------------------------
        private static void Apply(Document doc, Func<string, string> keyOf, List<string> layers)
        {
            var ed = doc.Editor; var db = doc.Database;
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int renamed = 0;
            if (Mode == 0)
            {
                // ---- bản sao chuẩn hoá, bản vẽ gốc giữ nguyên ----
                string dir, name;
                if (!string.IsNullOrEmpty(doc.Name) && Path.IsPathRooted(doc.Name) && File.Exists(doc.Name)) { dir = Path.GetDirectoryName(doc.Name); name = Path.GetFileNameWithoutExtension(doc.Name); }
                else { dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); name = "BanVe"; }
                string dwg = Path.Combine(dir, name + "_VXT.dwg"), dxf = Path.Combine(dir, name + "_VXT.dxf");
                using (doc.LockDocument())
                using (var copy = db.Wblock())
                {
                    if (BindXref)
                    {
                        try
                        {
                            var xids = new ObjectIdCollection();
                            using (var tr = copy.TransactionManager.StartTransaction())
                            {
                                var bt = (BlockTable)tr.GetObject(copy.BlockTableId, OpenMode.ForRead);
                                foreach (ObjectId bid in bt)
                                {
                                    var btr = tr.GetObject(bid, OpenMode.ForRead) as BlockTableRecord;
                                    if (btr != null && btr.IsFromExternalReference && btr.IsResolved) xids.Add(bid);
                                }
                                tr.Commit();
                            }
                            if (xids.Count > 0) { copy.BindXrefs(xids, true); ed.WriteMessage($"\nĐã bind {xids.Count} xref trong bản sao."); }
                        }
                        catch (Exception ex) { ed.WriteMessage("\nKhông bind được xref trong bản sao (" + ex.Message + ") – đối tượng xref giữ nguyên."); }
                    }
                    using (var tr = copy.TransactionManager.StartTransaction())
                    {
                        MoveEntities(copy, tr, keyOf, true, counts);
                        tr.Commit();
                    }
                    copy.SaveAs(dwg, DwgVersion.Current);
                    if (SaveDxf) { try { copy.DxfOut(dxf, 16, DwgVersion.Current); } catch (Exception ex) { ed.WriteMessage("\nKhông lưu được DXF: " + ex.Message); dxf = ""; } }
                    else dxf = "";
                }
                Report(ed, counts, renamed, "Đã tạo bản sao chuẩn hoá (bản vẽ gốc KHÔNG đổi):\n  " + dwg + (dxf.Length > 0 ? "\n  " + dxf + "  ← mở file này trong Việt Const (Tải mặt bằng để lấy kích thước… / Mở mặt bằng CAD)" : ""));
                return;
            }

            // ---- sửa trực tiếp bản vẽ đang mở (một lệnh = một bước Undo: gõ U để hoàn tác) ----
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                if (Mode == 2) renamed = RenameLayers(db, tr, keyOf, layers);
                MoveEntities(db, tr, keyOf, InBlocks, counts);
                tr.Commit();
            }
            string extra = "";
            if (SaveDxf)
            {
                try
                {
                    string dir = !string.IsNullOrEmpty(doc.Name) && Path.IsPathRooted(doc.Name) ? Path.GetDirectoryName(doc.Name) : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string dxf = Path.Combine(dir, (Path.GetFileNameWithoutExtension(doc.Name ?? "") is string nm && nm.Length > 0 ? nm : "BanVe") + "_VXT.dxf");
                    using (doc.LockDocument())
                    using (var copy = db.Wblock()) copy.DxfOut(dxf, 16, DwgVersion.Current);
                    extra = "\n  Đã lưu DXF: " + dxf;
                }
                catch (Exception ex) { extra = "\n  Không lưu được DXF: " + ex.Message; }
            }
            Report(ed, counts, renamed, "Đã chuẩn hoá layer trong bản vẽ đang mở (gõ U để hoàn tác)." + extra);
            VxtPalette.RefreshCounts();
            if (SendAfter) doc.SendStringToExecute("VXT_GUI ", true, false, true);
        }

        private static void Report(Editor ed, Dictionary<string, int> counts, int renamed, string head)
        {
            var sb = new StringBuilder(head);
            if (renamed > 0) sb.Append($"\n  Đổi tên {renamed} layer.");
            sb.Append($"\n  Chuyển {counts.Values.Sum()} đối tượng:");
            foreach (var kv in counts.OrderBy(q => q.Key, StringComparer.Ordinal)) sb.Append($"\n    {kv.Key}: {kv.Value}");
            ed.WriteMessage("\n" + sb);
            try { System.Windows.Forms.MessageBox.Show(sb.ToString(), "Việt Const – chuẩn hoá layer", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information); }
            catch (Exception) { }
        }

        /// <summary>Đổi tên layer khi một layer gốc ↔ một layer chuẩn và tên chuẩn chưa có (không đổi layer 0, DEFPOINTS, layer xref).</summary>
        private static int RenameLayers(Database db, Transaction tr, Func<string, string> keyOf, List<string> layers)
        {
            var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            int n = 0;
            foreach (var g in layers.Where(l => keyOf(l).Length > 0).GroupBy(l => keyOf(l)))
            {
                var src = g.ToList();
                var std = StdLayers.Of(g.Key);
                if (std == null || src.Count != 1 || table.Has(std.Name)) continue;
                string src0 = src[0];
                if (src0 == "0" || src0.Equals("DEFPOINTS", StringComparison.OrdinalIgnoreCase) || !table.Has(src0)) continue;
                try
                {
                    var rec = (LayerTableRecord)tr.GetObject(table[src0], OpenMode.ForWrite);
                    if (rec.IsDependent) continue;
                    rec.Name = std.Name;
                    rec.Color = AcColor.FromColorIndex(AcColorMethod.ByAci, (short)std.Color);
                    rec.LinetypeObjectId = EnsureLinetype(db, tr, std.Linetype);
                    try { rec.Description = std.Description; } catch (Exception) { }
                    n++;
                }
                catch (Exception) { }
            }
            return n;
        }

        /// <summary>Chuyển đối tượng sang layer chuẩn: model space (+ định nghĩa block thường nếu inBlocks). Block cửa → layer cửa, kích thước → VXT-KICH-THUOC.</summary>
        private static void MoveEntities(Database db, Transaction tr, Func<string, string> keyOf, bool inBlocks, Dictionary<string, int> counts)
        {
            var ids = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
            ObjectId LayerId(string name)
            {
                ObjectId id;
                if (ids.TryGetValue(name, out id)) return id;
                id = EnsureLayer(db, tr, StdLayers.Of(name));
                ids[name] = id;
                return id;
            }
            void Do(BlockTableRecord btr)
            {
                foreach (ObjectId id in btr)
                {
                    Entity e;
                    try { e = tr.GetObject(id, OpenMode.ForRead) as Entity; } catch (Exception) { continue; }
                    if (e == null) continue;
                    string key = keyOf(e.Layer);
                    var br = e as BlockReference;
                    string target = LayerConvert.TargetFor(key, br != null ? BlockName(tr, br) : null, e is Dimension, DoorBlocks);
                    if (target.Length == 0 || string.Equals(e.Layer, target, StringComparison.OrdinalIgnoreCase) || StdLayers.Of(target) == null) continue;
                    try
                    {
                        var w = (Entity)tr.GetObject(id, OpenMode.ForWrite, false, true);
                        w.LayerId = LayerId(target);
                        int c; counts.TryGetValue(target, out c); counts[target] = c + 1;
                    }
                    catch (Exception) { }
                }
            }
            Do((BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead));
            if (!inBlocks) return;
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId bid in bt)
            {
                var btr = tr.GetObject(bid, OpenMode.ForRead) as BlockTableRecord;
                if (btr == null || btr.IsLayout || btr.IsFromExternalReference || btr.IsDependent || btr.IsAnonymous) continue;
                Do(btr);
            }
        }

        private static ObjectId EnsureLinetype(Database db, Transaction tr, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Equals("CONTINUOUS", StringComparison.OrdinalIgnoreCase)) name = "Continuous";
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

        private static ObjectId EnsureLayer(Database db, Transaction tr, StdLayer std)
        {
            var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (table.Has(std.Name)) return table[std.Name];
            var rec = new LayerTableRecord { Name = std.Name };
            rec.Color = AcColor.FromColorIndex(AcColorMethod.ByAci, (short)std.Color);
            rec.LinetypeObjectId = EnsureLinetype(db, tr, std.Linetype);
            table.UpgradeOpen();
            var id = table.Add(rec);
            tr.AddNewlyCreatedDBObject(rec, true);
            try { rec.Description = std.Description; } catch (Exception) { }
            return id;
        }
    }
}

namespace KetCauVXT.AcadLink
{
    using System.Windows.Forms;
    using Drawing = System.Drawing;

    /// <summary>Bảng chuẩn hoá layer (WinForms, như bảng lệnh VXT): mỗi dòng một layer gốc → layer chuẩn đề xuất (sửa được).</summary>
    internal sealed class LayerConvertForm : Form
    {
        public enum ActionKind { Cancel, Apply, Pick, Highlight, Again }
        public sealed class Result { public ActionKind Action = ActionKind.Cancel; public string Key = ""; public string Layer; }
        public sealed class Row
        {
            public string Layer = "", Key = "", Reason = "";
            public int Count;
            public double Confidence;
            public Drawing.Color Swatch = Drawing.Color.White;
            public bool Dependent, UserSet;
            public bool Convert = true;   // cột "Chuyển": người dùng xác nhận có chuyển layer này hay không
        }

        public readonly List<Row> Rows;
        public Result Outcome = new Result();
        public LayerProfile LoadedProfile;
        public bool ResetRequested;

        private const string Keep = "(giữ nguyên)";
        private readonly DataGridView _grid = new DataGridView();
        private readonly TextBox _filter = new TextBox();
        private readonly CheckBox _onlyUnknown = new CheckBox { Text = "Chỉ hiện layer chưa nhận", AutoSize = true };
        private readonly ComboBox _pickKey = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
        private readonly RadioButton _mCopy = new RadioButton { Text = "Tạo bản sao chuẩn hoá <tên>_VXT.dwg (bản vẽ gốc giữ nguyên) – khuyên dùng", AutoSize = true };
        private readonly RadioButton _mMove = new RadioButton { Text = "Chuyển đối tượng sang layer chuẩn ngay trong bản vẽ này (gõ U để hoàn tác)", AutoSize = true };
        private readonly RadioButton _mRename = new RadioButton { Text = "Đổi tên layer (1 layer gốc ↔ 1 layer chuẩn), phần còn lại chuyển đối tượng", AutoSize = true };
        private readonly CheckBox _cBlocks = new CheckBox { Text = "Sửa cả đối tượng trong định nghĩa block (ảnh hưởng mọi block cùng tên)", AutoSize = true };
        private readonly CheckBox _cDoor = new CheckBox { Text = "Block cửa (CUA*, DOOR*, D1, S1…) → layer cửa, bất kể đang ở layer nào", AutoSize = true };
        private readonly CheckBox _cXref = new CheckBox { Text = "Bản sao: bind xref đã nạp trước khi chuyển", AutoSize = true };
        private readonly CheckBox _cDxf = new CheckBox { Text = "Lưu thêm DXF để mở trong Việt Const (Mở mặt bằng CAD)", AutoSize = true };
        private readonly CheckBox _cSend = new CheckBox { Text = "Sau khi chuẩn hoá: gửi sang Việt Const (VXT_GUI) – khi sửa trực tiếp bản vẽ", AutoSize = true };
        private readonly LayerProfile _profile;

        private static string Display(string key) => string.IsNullOrEmpty(key) ? Keep : StdLayers.Prefix + key;
        private static string KeyOf(string display) => string.IsNullOrEmpty(display) || display == Keep ? "" : (StdLayers.Of(display)?.Key ?? "");

        public LayerConvertForm(List<Row> rows, LayerProfile profile)
        {
            Rows = rows; _profile = profile;
            Text = "Việt Const – chuẩn hoá layer bản vẽ (VXT_CHUANLAYER)";
            Width = 1100; Height = 720; StartPosition = FormStartPosition.CenterScreen; MinimizeBox = false;
            Font = new Drawing.Font("Segoe UI", 9f);

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 64, Padding = new Padding(6), WrapContents = true };
            top.Controls.Add(new Label { Text = "Lọc:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
            _filter.Width = 160; top.Controls.Add(_filter);
            _onlyUnknown.Margin = new Padding(8, 6, 8, 3); top.Controls.Add(_onlyUnknown);
            top.Controls.Add(Btn("Tích chuyển tất cả", (s, e) => SetAllConvert(true)));
            top.Controls.Add(Btn("Bỏ tích tất cả", (s, e) => SetAllConvert(false)));
            top.Controls.Add(Btn("Tự nhận lại", (s, e) => { ResetRequested = true; Close2(ActionKind.Again); }));
            top.Controls.Add(Btn("Tô sáng layer đang chọn", (s, e) => { var r = Current(); if (r != null) { Outcome.Layer = r.Layer; Close2(ActionKind.Highlight); } }));
            top.Controls.Add(new Label { Text = "Gán vào:", AutoSize = true, Margin = new Padding(12, 7, 3, 3) });
            _pickKey.Items.Add(Keep); foreach (var s in StdLayers.All) _pickKey.Items.Add(s.Name);
            _pickKey.SelectedIndex = 0; top.Controls.Add(_pickKey);
            top.Controls.Add(Btn("Chọn đối tượng trên bản vẽ…", (s, e) => { Outcome.Key = KeyOf(_pickKey.SelectedItem as string); Close2(ActionKind.Pick); }));
            top.Controls.Add(Btn("Mở bộ ánh xạ…", (s, e) => OpenProfile()));
            top.Controls.Add(Btn("Lưu bộ ánh xạ…", (s, e) => SaveProfile()));
            Controls.Add(_grid);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 200, FlowDirection = FlowDirection.TopDown, Padding = new Padding(8), WrapContents = false };
            bottom.Controls.Add(new Label { Text = "CÁCH ÁP DỤNG", AutoSize = true, Font = new Drawing.Font(Font, Drawing.FontStyle.Bold) });
            foreach (Control c in new Control[] { _mCopy, _mMove, _mRename, _cDoor, _cBlocks, _cXref, _cDxf, _cSend }) bottom.Controls.Add(c);
            var btns = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            var ok = Btn("CHUYỂN ĐỔI", (s, e) => { SaveOptions(); if (ConfirmConvert()) Close2(ActionKind.Apply); });
            ok.Font = new Drawing.Font(Font, Drawing.FontStyle.Bold); ok.Width = 160;
            AcceptButton = null;   // không chuyển khi lỡ nhấn Enter – phải bấm nút CHUYỂN ĐỔI và xác nhận
            btns.Controls.Add(ok);
            btns.Controls.Add(Btn("Huỷ", (s, e) => Close2(ActionKind.Cancel)));
            bottom.Controls.Add(btns);
            Controls.Add(bottom);
            Controls.Add(top);

            _mCopy.Checked = LayerConvertCmd.Mode == 0; _mMove.Checked = LayerConvertCmd.Mode == 1; _mRename.Checked = LayerConvertCmd.Mode == 2;
            _cBlocks.Checked = LayerConvertCmd.InBlocks; _cDoor.Checked = LayerConvertCmd.DoorBlocks; _cXref.Checked = LayerConvertCmd.BindXref;
            _cDxf.Checked = LayerConvertCmd.SaveDxf; _cSend.Checked = LayerConvertCmd.SendAfter;

            BuildGrid();
            _filter.TextChanged += (s, e) => ApplyFilter();
            _onlyUnknown.CheckedChanged += (s, e) => ApplyFilter();
            FormClosing += (s, e) => CommitGrid();
        }

        private Button Btn(string text, EventHandler h)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 26, Margin = new Padding(3, 2, 3, 2) };
            b.Click += h;
            return b;
        }

        private void Close2(ActionKind a) { Outcome.Action = a; DialogResult = a == ActionKind.Apply ? DialogResult.OK : DialogResult.Cancel; Close(); }

        private void SaveOptions()
        {
            LayerConvertCmd.Mode = _mMove.Checked ? 1 : _mRename.Checked ? 2 : 0;
            LayerConvertCmd.InBlocks = _cBlocks.Checked; LayerConvertCmd.DoorBlocks = _cDoor.Checked; LayerConvertCmd.BindXref = _cXref.Checked;
            LayerConvertCmd.SaveDxf = _cDxf.Checked; LayerConvertCmd.SendAfter = _cSend.Checked;
        }

        private void BuildGrid()
        {
            _grid.Dock = DockStyle.Fill; _grid.AllowUserToAddRows = false; _grid.AllowUserToDeleteRows = false; _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _grid.MultiSelect = false; _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.DataError += (s, e) => { e.ThrowException = false; };
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Chuyen", HeaderText = "Chuyển", FillWeight = 6 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Layer", HeaderText = "Layer gốc", ReadOnly = true, FillWeight = 26 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mau", HeaderText = "Màu", ReadOnly = true, FillWeight = 5 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SL", HeaderText = "Số đối tượng", ReadOnly = true, FillWeight = 8 });
            var cb = new DataGridViewComboBoxColumn { Name = "Key", HeaderText = "Layer chuẩn", FillWeight = 18, FlatStyle = FlatStyle.Flat };
            cb.Items.Add(Keep); foreach (var s in StdLayers.All) cb.Items.Add(s.Name);
            _grid.Columns.Add(cb);
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tin", HeaderText = "Tin cậy", ReadOnly = true, FillWeight = 7 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ly", HeaderText = "Căn cứ", ReadOnly = true, FillWeight = 36 });
            foreach (var r in Rows)
            {
                int i = _grid.Rows.Add(r.Convert && r.Key.Length > 0, r.Layer + (r.Dependent ? "  (xref)" : ""), "", r.Count.ToString(CultureInfo.InvariantCulture), Display(r.Key),
                                       Math.Round(r.Confidence * 100).ToString(CultureInfo.InvariantCulture) + "%", r.Reason);
                var row = _grid.Rows[i];
                row.Tag = r;
                row.Cells["Mau"].Style.BackColor = r.Swatch;
                row.Cells["Mau"].Style.SelectionBackColor = r.Swatch;
                if (r.Key.Length == 0) row.DefaultCellStyle.ForeColor = Drawing.Color.DimGray;
                else if (r.Confidence < 0.6) row.Cells["Key"].Style.BackColor = Drawing.Color.FromArgb(255, 245, 205);
            }
            _grid.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var gr = _grid.Rows[e.RowIndex];
                var r = gr.Tag as Row;
                if (r == null) return;
                if (e.ColumnIndex == _grid.Columns["Chuyen"].Index)
                {
                    r.Convert = gr.Cells["Chuyen"].Value is bool b && b;
                    gr.DefaultCellStyle.ForeColor = r.Convert && r.Key.Length > 0 ? _grid.DefaultCellStyle.ForeColor : Drawing.Color.DimGray;
                    return;
                }
                if (e.ColumnIndex != _grid.Columns["Key"].Index) return;
                r.Key = KeyOf(gr.Cells["Key"].Value as string); r.UserSet = true;
                gr.Cells["Ly"].Value = "người dùng chọn";
                // chọn layer chuẩn → tự tích "Chuyển"; chọn (giữ nguyên) → bỏ tích
                r.Convert = r.Key.Length > 0;
                gr.Cells["Chuyen"].Value = r.Convert;
            };
            _grid.CurrentCellDirtyStateChanged += (s, e) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        }

        private void SetAllConvert(bool on)
        {
            CommitGrid();
            foreach (DataGridViewRow gr in _grid.Rows)
            {
                var r = gr.Tag as Row;
                if (r == null || !gr.Visible) continue;          // chỉ tác động các dòng đang hiện (theo bộ lọc)
                r.Convert = on && r.Key.Length > 0;
                gr.Cells["Chuyen"].Value = r.Convert;
                gr.DefaultCellStyle.ForeColor = r.Convert ? _grid.DefaultCellStyle.ForeColor : Drawing.Color.DimGray;
            }
        }

        /// <summary>Hỏi xác nhận trước khi chuyển: liệt kê các layer được tích "Chuyển" → layer chuẩn. Trả về true nếu người dùng đồng ý.</summary>
        private bool ConfirmConvert()
        {
            CommitGrid();
            var todo = Rows.Where(r => r.Convert && r.Key.Length > 0).ToList();
            if (todo.Count == 0)
            {
                MessageBox.Show(this, "Chưa có layer nào được tích \"Chuyển\" (và có layer chuẩn).\nHãy tích các layer cần chuyển rồi bấm CHUYỂN ĐỔI.",
                                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            string how = _mMove.Checked ? "chuyển đối tượng ngay trong bản vẽ này (gõ U để hoàn tác)"
                       : _mRename.Checked ? "đổi tên layer / chuyển đối tượng ngay trong bản vẽ này (gõ U để hoàn tác)"
                       : "tạo bản sao <tên>_VXT.dwg, bản vẽ gốc giữ nguyên";
            var sb = new StringBuilder();
            sb.AppendLine($"Sẽ chuyển {todo.Count} layer ({todo.Sum(r => r.Count)} đối tượng) – cách áp dụng: {how}.");
            int skipped = Rows.Count(r => r.Key.Length > 0 && !r.Convert);
            if (skipped > 0) sb.AppendLine($"Giữ nguyên {skipped} layer đã bỏ tích.");
            sb.AppendLine();
            foreach (var r in todo.Take(20)) sb.AppendLine($"  {r.Layer}  →  {Display(r.Key)}");
            if (todo.Count > 20) sb.AppendLine($"  … và {todo.Count - 20} layer khác");
            sb.AppendLine();
            sb.Append("Bạn có muốn chuyển đổi không?");
            return MessageBox.Show(this, sb.ToString(), "Xác nhận chuyển đổi layer", MessageBoxButtons.YesNo,
                                   MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private void CommitGrid()
        {
            try { _grid.EndEdit(); } catch (Exception) { }
        }

        private Row Current() => _grid.CurrentRow?.Tag as Row;

        private void ApplyFilter()
        {
            string f = LayerConvert.Fold(_filter.Text);
            _grid.CurrentCell = null;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                var r = row.Tag as Row;
                if (r == null) continue;
                row.Visible = (f.Length == 0 || LayerConvert.Fold(r.Layer).Contains(f)) && (!_onlyUnknown.Checked || r.Key.Length == 0);
            }
        }

        private static string ProfileDir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VietConst", "layer-profiles");
                try { Directory.CreateDirectory(d); } catch (Exception) { }
                return d;
            }
        }

        private void OpenProfile()
        {
            using (var dlg = new OpenFileDialog { Filter = "Bộ ánh xạ layer (*.json)|*.json", InitialDirectory = ProfileDir, Title = "Mở bộ ánh xạ layer" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try { LoadedProfile = LayerProfile.FromJson(File.ReadAllText(dlg.FileName)); Close2(ActionKind.Again); }
                catch (Exception ex) { MessageBox.Show(this, "Không đọc được bộ ánh xạ: " + ex.Message, Text); }
            }
        }

        private void SaveProfile()
        {
            CommitGrid();
            using (var dlg = new SaveFileDialog { Filter = "Bộ ánh xạ layer (*.json)|*.json", InitialDirectory = ProfileDir, FileName = (_profile?.Name ?? "BoAnhXa") + ".json", Title = "Lưu bộ ánh xạ layer" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var p = new LayerProfile { Name = Path.GetFileNameWithoutExtension(dlg.FileName) };
                foreach (var r in Rows) p.Set(r.Layer, r.Key);
                try { File.WriteAllText(dlg.FileName, p.ToJson(), new UTF8Encoding(false)); }
                catch (Exception ex) { MessageBox.Show(this, "Không lưu được: " + ex.Message, Text); }
            }
        }
    }
}

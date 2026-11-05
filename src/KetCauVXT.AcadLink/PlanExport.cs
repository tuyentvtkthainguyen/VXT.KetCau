using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

[assembly: CommandClass(typeof(KetCauVXT.AcadLink.PlanExport))]

namespace KetCauVXT.AcadLink
{
    /// <summary>Lệnh VXT_MB_KC – gửi MẶT BẰNG KẾT CẤU vẽ trong AutoCAD sang chế độ "Triển khai hồ sơ" của Việt Const:
    /// chọn các nét dầm (layer DAM / BEAM / GIANG), cột (đa tuyến kín layer COT / COLUMN), chữ tên cấu kiện ("D1 220x400", "C1", "S1"…),
    /// cửa (layer CUA / DOOR, block cửa) → ghi %AppData%\KetCauVXT\AcadPick\plan_*.json (đơn vị m). Việt Const tự hỏi nhận vào mặt bằng đang xem
    /// (Core/Detailing/PlanCadImport.cs – StructuralPlanImport.ParseExchange / Import). Không gửi dữ liệu đi đâu khác.</summary>
    public class PlanExport
    {
        private static Document Doc => AcApp.DocumentManager.MdiActiveDocument;

        [CommandMethod("VXT_MB_KC", CommandFlags.Modal)]
        public void GuiMatBang()
        {
            var doc = Doc;
            if (doc == null) return;
            var ed = doc.Editor; var db = doc.Database;
            try
            {
                var o = new PromptSelectionOptions { MessageForAdding = "\nChọn mặt bằng kết cấu (dầm, cột, chữ tên cấu kiện, cửa): " };
                var r = ed.GetSelection(o);
                if (r.Status != PromptStatus.OK) return;
                string unitName;
                double k = Exchange.UnitToM((int)db.Insunits, out unitName);
                var w = new JsonOut();
                w.BeginObject();
                w.Prop("version", 1);
                w.Prop("kind", "matbang");
                w.Prop("unit", "m");
                w.Prop("source", "AutoCAD VXT_MB_KC");
                w.Prop("dwg", doc.Name ?? "");
                w.BeginArray("entities");
                int n = 0;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var id in r.Value.GetObjectIds())
                    {
                        var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (e == null) continue;
                        n += Write(w, e, k, "", 0);
                    }
                    tr.Commit();
                }
                w.EndArray();
                w.EndObject();
                if (n == 0) { ed.WriteMessage("\nKhông có đối tượng nào gửi được (nét, đa tuyến, chữ, block)."); return; }
                string dir = Path.Combine(LayerStd.AppDataDir, "AcadPick");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "plan_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ".json");
                File.WriteAllText(file, w.ToString(), new System.Text.UTF8Encoding(false));
                ed.WriteMessage("\nĐã gửi " + n + " đối tượng (đơn vị bản vẽ: " + unitName + ") → " + file
                                + "\nTrong Việt Const (Triển khai hồ sơ – tab Mặt bằng kết cấu): chương trình hỏi nhận vào mặt bằng đang xem.");
            }
            catch (Exception ex) { ed.WriteMessage("\nLỗi VXT_MB_KC: " + ex.Message); }
        }

        private static int Write(JsonOut w, Entity e, double k, string block, int depth)
        {
            string layer = e.Layer ?? "";
            var ln = e as Line;
            if (ln != null) { Seg(w, layer, block, new List<double[]> { P(ln.StartPoint, k), P(ln.EndPoint, k) }, false); return 1; }
            var pl = e as Polyline;
            if (pl != null)
            {
                var pts = new List<double[]>();
                for (int i = 0; i < pl.NumberOfVertices; i++) { var p = pl.GetPoint2dAt(i); pts.Add(new[] { p.X * k, p.Y * k }); }
                if (pts.Count >= 2) { Seg(w, layer, block, pts, pl.Closed); return 1; }
                return 0;
            }
            var arc = e as Arc;
            if (arc != null)
            {
                var pts = new List<double[]>();
                double a0 = arc.StartAngle, a1 = arc.EndAngle;
                if (a1 < a0) a1 += 2 * Math.PI;
                int m = Math.Max(4, (int)Math.Ceiling((a1 - a0) / (Math.PI / 18)));
                for (int i = 0; i <= m; i++)
                {
                    double a = a0 + (a1 - a0) * i / m;
                    pts.Add(new[] { (arc.Center.X + arc.Radius * Math.Cos(a)) * k, (arc.Center.Y + arc.Radius * Math.Sin(a)) * k });
                }
                Seg(w, layer, block, pts, false);
                return 1;
            }
            var t = e as DBText;
            if (t != null && !string.IsNullOrWhiteSpace(t.TextString)) { Txt(w, layer, P(t.Position, k), t.TextString.Trim()); return 1; }
            var mt = e as MText;
            if (mt != null && !string.IsNullOrWhiteSpace(mt.Text)) { Txt(w, layer, P(mt.Location, k), mt.Text.Replace("\r", " ").Replace("\n", " ").Trim()); return 1; }
            var br = e as BlockReference;
            if (br != null && depth < 3)
            {
                string name = block;
                try
                {
                    var bt = (br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord).GetObject(OpenMode.ForRead) as BlockTableRecord;
                    if (string.IsNullOrEmpty(name) && bt != null) name = bt.Name;
                }
                catch { }
                int n = 0;
                var parts = new DBObjectCollection();
                try { br.Explode(parts); } catch { return 0; }
                foreach (DBObject d in parts)
                {
                    var pe = d as Entity;
                    if (pe != null)
                    {
                        if (pe.Layer == "0") { try { pe.Layer = layer; } catch { } }
                        n += Write(w, pe, k, name, depth + 1);
                    }
                    d.Dispose();
                }
                return n;
            }
            return 0;
        }

        private static double[] P(Point3d p, double k) { return new[] { p.X * k, p.Y * k }; }

        private static void Seg(JsonOut w, string layer, string block, List<double[]> pts, bool closed)
        {
            w.BeginObject();
            w.Prop("t", "line");
            w.Prop("layer", layer);
            if (!string.IsNullOrEmpty(block)) w.Prop("block", block);
            w.Points("pts", pts);
            w.Prop("closed", closed);
            w.EndObject();
        }

        private static void Txt(JsonOut w, string layer, double[] p, string s)
        {
            w.BeginObject();
            w.Prop("t", "text");
            w.Prop("layer", layer);
            w.Prop("x", p[0]);
            w.Prop("y", p[1]);
            w.Prop("s", s);
            w.EndObject();
        }
    }
}

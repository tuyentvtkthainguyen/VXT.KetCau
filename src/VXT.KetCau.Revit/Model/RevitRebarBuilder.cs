using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using VXT.KetCau.Core.Detailing;
using VXT.KetCau.Core.Geometry;
using VXT.KetCau.Core.Model;
using VXT.KetCau.Core.Workflow;
using VXT.KetCau.Revit.Util;

namespace VXT.KetCau.Revit.Model
{
    /// <summary>
    /// Dựng thép 3D trong Revit từ kết quả cấu tạo: thép dọc theo đúng đường tim (kể cả đoạn bẻ móc),
    /// đai/đai C với móc 135°, thép sàn/móng rải theo bước. Ghi Partition = tên cấu kiện, Mark = số hiệu.
    /// </summary>
    public class RevitRebarBuilder
    {
        readonly Document doc;
        readonly StructModel m;
        public List<string> Log { get; } = new List<string>();
        readonly Dictionary<int, RebarBarType> barTypes = new Dictionary<int, RebarBarType>();
        RebarHookType hook135;
        int created, failed;

        public RevitRebarBuilder(Document d, StructModel model) { doc = d; m = model; }

        /// <summary>Gọi trong Transaction.</summary>
        public void Build(WorkflowResult res, bool deleteOld = true)
        {
            if (deleteOld)
            {
                var old = new FilteredElementCollector(doc).OfClass(typeof(Rebar)).Where(r => Rv.GetComment(r).StartsWith("VXT")).Select(r => r.Id).ToList();
                if (old.Count > 0) { doc.Delete(old); Log.Add($"Đã xoá {old.Count} thép VXT cũ"); }
            }
            hook135 = GetHook135();
            var view3d = Rv.All<View3D>(doc).FirstOrDefault(v => !v.IsTemplate);

            foreach (var e in res.Elements)
            {
                var hosts = e.SourceIds.Select(HostOf).Where(h => h != null).ToList();
                if (hosts.Count == 0) { Log.Add($"{e.Label}: chưa có phần tử Revit để gắn thép (hãy dựng mô hình trước)"); continue; }
                foreach (var bar in e.Bars)
                {
                    var bt = BarType(bar.D);
                    foreach (var pl in bar.Placements)
                    {
                        try
                        {
                            var host = PickHost(hosts, pl);
                            var r = Create(bar, pl, bt, host);
                            if (r == null) { failed++; continue; }
                            r.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM)?.Set(e.Label);
                            r.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set(bar.Mark.ToString());
                            Rv.SetComment(r, $"VXT {e.Label} #{bar.Mark}");
                            if (view3d != null) r.SetUnobscuredInView(view3d, true);
                            created++;
                        }
                        catch (Exception ex) { failed++; if (failed < 20) Log.Add($"{e.Label} số hiệu {bar.Mark}: {ex.Message}"); }
                    }
                }
            }
            Log.Add($"Đã tạo {created} nhóm thép" + (failed > 0 ? $", {failed} nhóm lỗi" : ""));
        }

        Element HostOf(string sourceId)
        {
            string uid = m.Beams.FirstOrDefault(b => b.Id == sourceId)?.RevitId
                         ?? m.Columns.FirstOrDefault(c => c.Id == sourceId)?.RevitId
                         ?? m.Slabs.FirstOrDefault(s => s.Id == sourceId)?.RevitId
                         ?? m.Footings.FirstOrDefault(f => f.Id == sourceId)?.RevitId;
            return uid == null ? null : doc.GetElement(uid);
        }

        /// <summary>Chọn host chứa điểm giữa của thanh (dầm nhiều nhịp).</summary>
        static Element PickHost(List<Element> hosts, BarPlacement pl)
        {
            if (hosts.Count == 1) return hosts[0];
            var a = pl.Path.First(); var b = pl.Path.Last();
            var mid = Rv.X(new P3((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2));
            Element best = hosts[0]; double bd = double.MaxValue;
            foreach (var h in hosts)
            {
                var bb = h.get_BoundingBox(null);
                if (bb == null) continue;
                var c = (bb.Min + bb.Max) / 2;
                double d = c.DistanceTo(mid);
                if (d < bd) { bd = d; best = h; }
            }
            return best;
        }

        Rebar Create(DetailBar bar, BarPlacement pl, RebarBarType bt, Element host)
        {
            var pts = pl.Path.Select(Rv.X).ToList();
            // bỏ điểm trùng
            for (int i = pts.Count - 1; i > 0; i--) if (pts[i].DistanceTo(pts[i - 1]) < 1e-4) pts.RemoveAt(i);
            if (pts.Count < 2) return null;
            bool closed = pts.Count > 3 && pts.First().DistanceTo(pts.Last()) < 1e-4;
            var curves = new List<Curve>();
            for (int i = 0; i + 1 < pts.Count; i++) curves.Add(Line.CreateBound(pts[i], pts[i + 1]));
            var normal = Rv.V(pl.Normal);

            Rebar r = null;
            bool tie = bar.Role == BarRole.Stirrup || bar.Role == BarRole.InnerStirrup || bar.Role == BarRole.CTie;
            if (tie)
            {
                // thử các hướng móc cho tới khi Revit chấp nhận
                foreach (var (o1, o2) in new[] { (RebarHookOrientation.Right, RebarHookOrientation.Right), (RebarHookOrientation.Left, RebarHookOrientation.Left),
                                                  (RebarHookOrientation.Right, RebarHookOrientation.Left), (RebarHookOrientation.Left, RebarHookOrientation.Right) })
                {
                    try { r = Rebar.CreateFromCurves(doc, RebarStyle.StirrupTie, bt, hook135, hook135, host, normal, curves, o1, o2, true, true); }
                    catch { r = null; }
                    if (r != null) break;
                }
                if (r == null) r = Rebar.CreateFromCurves(doc, RebarStyle.StirrupTie, bt, null, null, host, normal, curves, RebarHookOrientation.Right, RebarHookOrientation.Right, true, true);
            }
            else
            {
                r = Rebar.CreateFromCurves(doc, RebarStyle.Standard, bt, null, null, host, normal, curves, RebarHookOrientation.Right, RebarHookOrientation.Right, true, true);
            }
            if (r == null) return null;

            if (pl.Count > 1)
            {
                double step = pl.X1 > pl.X0 ? (pl.X1 - pl.X0) / (pl.Count - 1) : pl.Spacing;
                if (step <= 0) step = pl.Spacing;
                // phương rải phải trùng pháp tuyến; nếu ngược thì rải về phía âm
                bool normalSide = pl.Direction.X * normal.X + pl.Direction.Y * normal.Y + pl.Direction.Z * normal.Z >= 0;
                r.GetShapeDrivenAccessor().SetLayoutAsNumberWithSpacing(pl.Count, Rv.Ft(step), normalSide, true, true);
            }
            return r;
        }

        RebarBarType BarType(int d)
        {
            if (barTypes.TryGetValue(d, out var t)) return t;
            t = Rv.All<RebarBarType>(doc).FirstOrDefault(x => Math.Abs(Rv.BarDiameterMm(x) - d) < 0.5);
            if (t == null)
            {
                t = RebarBarType.Create(doc);
                Rv.SetBarDiameter(t, d);
                try { t.Name = $"Ø{d}"; } catch { }
                // bán kính uốn tối thiểu ≈ 2.5d (CB240) … 3d (CB400)
                t.StandardBendDiameter = Rv.Ft(5 * d);
                t.StirrupTieBendDiameter = Rv.Ft(4 * d);
                Log.Add($"Tạo loại thép Ø{d}");
            }
            barTypes[d] = t;
            return t;
        }

        RebarHookType GetHook135()
        {
            var h = Rv.All<RebarHookType>(doc).FirstOrDefault(x => x.Name.Contains("135") && (x.Style == RebarStyle.StirrupTie));
            if (h != null) return h;
            h = RebarHookType.Create(doc, Math.PI * 3 / 4, 10);
            h.Style = RebarStyle.StirrupTie;
            try { h.Name = "VXT - Móc đai 135°"; } catch { }
            return h;
        }
    }
}

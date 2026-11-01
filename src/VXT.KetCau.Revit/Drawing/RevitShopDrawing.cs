using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using VXT.KetCau.Core.Detailing;
using VXT.KetCau.Core.Drawing;
using VXT.KetCau.Core.Model;
using VXT.KetCau.Core.Workflow;
using VXT.KetCau.Revit.Util;

namespace VXT.KetCau.Revit.Drawing
{
    /// <summary>
    /// Triển khai shop drawing thép trong Revit:
    /// - mỗi dầm liên tục: 1 view mặt đứng (section dọc dầm, 1/50) + các mặt cắt (1/25) có thép 3D thật,
    ///   ký hiệu theo Kết cấu Soft (chuỗi vùng đai, nhãn xếp tầng, vạch cắt, kích thước nhịp, trục, cao độ);
    /// - cột: view drafting mặt đứng + mặt cắt;
    /// - bảng thống kê cốt thép (drafting, mẫu Kết cấu Soft) + Rebar Schedule gốc Revit;
    /// - tự xếp view lên các tờ KC-xx.
    /// </summary>
    public class RevitShopDrawing
    {
        readonly Document doc;
        readonly DesignSettings set;
        public List<string> Log { get; } = new List<string>();
        readonly List<View> created = new List<View>();

        public RevitShopDrawing(Document d, DesignSettings s) { doc = d; set = s; }

        ViewFamilyType VFT(ViewFamily f) => Rv.All<ViewFamilyType>(doc).First(t => t.ViewFamily == f);

        /// <summary>Gọi trong Transaction.</summary>
        public void Build(WorkflowResult res, bool beams = true, bool columns = true, bool schedule = true, bool sheets = true)
        {
            var rebarsByLabel = new FilteredElementCollector(doc).OfClass(typeof(Rebar)).Cast<Rebar>()
                .Where(r => Rv.GetComment(r).StartsWith("VXT"))
                .GroupBy(r => r.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM)?.AsString() ?? "")
                .ToDictionary(g => g.Key, g => g.ToList());

            if (beams)
                foreach (var e in res.Elements.Where(x => x.Category == "Dầm"))
                {
                    try { BeamViews(e, rebarsByLabel.TryGetValue(e.Label, out var rb) ? rb : new List<Rebar>()); }
                    catch (Exception ex) { Log.Add($"{e.Label}: {ex.Message}"); }
                }
            if (columns)
                foreach (var e in res.Elements.Where(x => x.Category == "Cột"))
                {
                    try { ColumnView(e); } catch (Exception ex) { Log.Add($"{e.Label}: {ex.Message}"); }
                }
            if (schedule)
            {
                ScheduleDrafting(res.Schedule);
                try { NativeRebarSchedule(); } catch (Exception ex) { Log.Add("Rebar Schedule: " + ex.Message); }
            }
            if (sheets) PlaceOnSheets();
            Log.Add($"Đã tạo {created.Count} view shop drawing");
        }

        // ------------------------------------------------------------ Dầm
        void BeamViews(DetailedElement e, List<Rebar> rebars)
        {
            var g = (BeamChainGeometry)e.Geometry;
            var dir = new XYZ(g.Dir.X, g.Dir.Y, 0).Normalize();
            var perp = new XYZ(g.Perp.X, g.Perp.Y, 0).Normalize();
            var org = Rv.X(g.Origin, g.TopZ);
            double x0 = g.Supports.First().FaceLeft, len = g.Length;

            // ---- Mặt đứng: hộp section dọc dầm, nhìn từ phía -perp
            var t = Transform.Identity;
            t.Origin = org; t.BasisX = dir; t.BasisY = XYZ.BasisZ; t.BasisZ = dir.CrossProduct(XYZ.BasisZ);
            var box = new BoundingBoxXYZ
            {
                Transform = t,
                Min = new XYZ(Rv.Ft(x0 - 2500), Rv.Ft(-g.H - 4500), Rv.Ft(-g.B / 2 - 200)),
                Max = new XYZ(Rv.Ft(x0 + len + 1500), Rv.Ft(3500), Rv.Ft(g.B / 2 + 200)),
            };
            var elev = ViewSection.CreateSection(doc, VFT(ViewFamily.Section).Id, box);
            elev.Name = Rv.UniqueViewName(doc, $"{e.Label} - Mặt đứng");
            elev.Scale = 50;
            elev.DetailLevel = ViewDetailLevel.Fine;
            elev.get_Parameter(BuiltInParameter.VIEWER_BOUND_OFFSET_FAR)?.Set(Rv.Ft(g.B + 400));
            foreach (var r in rebars) r.SetUnobscuredInView(elev, true);
            var comp = new ShopDrawingComposer(new RevitDraw2D(doc, elev, org, t.BasisX, t.BasisY), set) { AnnotationsOnly = true, ElevationScale = 50 };
            comp.DrawBeamElevation(e, 0, 0, withTitle: true);
            created.Add(elev);

            // ---- Mặt cắt
            foreach (var sec in g.Sections.GroupBy(s => s.Name).Select(x => x.First()))
            {
                var p = Rv.X(g.Origin + g.Dir * sec.X, g.TopZ);
                var ts = Transform.Identity;
                ts.Origin = p; ts.BasisX = perp; ts.BasisY = XYZ.BasisZ; ts.BasisZ = perp.CrossProduct(XYZ.BasisZ);
                var bs = new BoundingBoxXYZ
                {
                    Transform = ts,
                    Min = new XYZ(Rv.Ft(-g.B / 2 - 900), Rv.Ft(-g.H - 1600), Rv.Ft(-30)),
                    Max = new XYZ(Rv.Ft(g.B / 2 + 1500), Rv.Ft(1200), Rv.Ft(30)),
                };
                var vs = ViewSection.CreateSection(doc, VFT(ViewFamily.Section).Id, bs);
                vs.Name = Rv.UniqueViewName(doc, $"{e.Label} - Mặt cắt {sec.Name}");
                vs.Scale = 25;
                vs.DetailLevel = ViewDetailLevel.Fine;
                vs.get_Parameter(BuiltInParameter.VIEWER_BOUND_OFFSET_FAR)?.Set(Rv.Ft(60));
                foreach (var r in rebars) r.SetUnobscuredInView(vs, true);
                var cs = new ShopDrawingComposer(new RevitDraw2D(doc, vs, p, ts.BasisX, ts.BasisY), set) { AnnotationsOnly = true, SectionScale = 25 };
                cs.DrawBeamSection(e, sec.X, sec.Name, 0, 0, 1, 2.5 * 25);
                created.Add(vs);
            }
        }

        // ------------------------------------------------------------ Cột (drafting)
        void ColumnView(DetailedElement e)
        {
            var v = ViewDrafting.Create(doc, VFT(ViewFamily.Drafting).Id);
            v.Name = Rv.UniqueViewName(doc, $"{e.Label} - Chi tiết cột");
            v.Scale = 50;
            var comp = new ShopDrawingComposer(RevitDraw2D.ForDrafting(doc, v), set) { ElevationScale = 50, SectionScale = 25 };
            var g = (ColumnGeometry)e.Geometry;
            comp.DrawColumnElevation(e, 0, -g.BaseZ);
            comp.DrawColumnSection(e, g.B / 2 + 4500, g.TopZ - g.BaseZ, 2, 125);
            created.Add(v);
        }

        // ------------------------------------------------------------ Bảng thống kê
        void ScheduleDrafting(RebarSchedule s)
        {
            if (s == null) return;
            var v = ViewDrafting.Create(doc, VFT(ViewFamily.Drafting).Id);
            v.Name = Rv.UniqueViewName(doc, "BẢNG THỐNG KÊ CỐT THÉP");
            v.Scale = 50;
            var draw = RevitDraw2D.ForDrafting(doc, v);
            draw.RealDimensions = false;
            new ShopDrawingComposer(draw, set).DrawSchedule(s, 0, 0, 125);
            created.Add(v);
        }

        void NativeRebarSchedule()
        {
            var vs = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_Rebar));
            vs.Name = Rv.UniqueViewName(doc, "VXT - Thống kê thép (Revit)");
            var def = vs.Definition;
            var wanted = new[] { BuiltInParameter.NUMBER_PARTITION_PARAM, BuiltInParameter.ALL_MODEL_MARK, BuiltInParameter.REBAR_BAR_DIAMETER,
                                 BuiltInParameter.REBAR_ELEM_LENGTH, BuiltInParameter.REBAR_ELEM_QUANTITY_OF_BARS, BuiltInParameter.REBAR_ELEM_TOTAL_LENGTH };
            var fields = def.GetSchedulableFields();
            ScheduleFieldId partId = null;
            foreach (var bip in wanted)
            {
                var sf = fields.FirstOrDefault(f => f.ParameterId == new ElementId(bip));
                if (sf == null) continue;
                var fld = def.AddField(sf);
                if (bip == BuiltInParameter.NUMBER_PARTITION_PARAM) partId = fld.FieldId;
            }
            if (partId != null) def.AddSortGroupField(new ScheduleSortGroupField(partId) { ShowHeader = true });
            def.IsItemized = true;
            created.Add(vs);
        }

        // ------------------------------------------------------------ Tờ bản vẽ
        void PlaceOnSheets()
        {
            var tb = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsElementType().FirstElementId();
            double W = Rv.Ft(780), H = Rv.Ft(540), margin = Rv.Ft(20);
            int no = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Count(s => s.SheetNumber.StartsWith("KC-"));
            ViewSheet sheet = null; double cx = margin, cy = H - margin, rowH = 0;
            foreach (var v in created)
            {
                if (v is ViewSchedule) continue;
                if (sheet == null) sheet = NewSheet(tb, ++no, v);
                if (!Viewport.CanAddViewToSheet(doc, sheet.Id, v.Id)) continue;
                var vp = Viewport.Create(doc, sheet.Id, v.Id, new XYZ(cx, cy, 0));
                var ol = vp.GetBoxOutline();
                double w = ol.MaximumPoint.X - ol.MinimumPoint.X, h = ol.MaximumPoint.Y - ol.MinimumPoint.Y;
                if (cx + w > W && cx > margin) { cx = margin; cy -= rowH + margin; rowH = 0; }
                if (cy - h < margin && cy < H - margin)
                {
                    doc.Delete(vp.Id);
                    sheet = NewSheet(tb, ++no, v); cx = margin; cy = H - margin; rowH = 0;
                    vp = Viewport.Create(doc, sheet.Id, v.Id, new XYZ(cx, cy, 0));
                }
                vp.SetBoxCenter(new XYZ(cx + w / 2, cy - h / 2, 0));
                cx += w + margin; rowH = Math.Max(rowH, h);
            }
        }

        ViewSheet NewSheet(ElementId tb, int no, View first)
        {
            var s = ViewSheet.Create(doc, tb ?? ElementId.InvalidElementId);
            s.SheetNumber = $"KC-{no:00}";
            s.Name = first is ViewDrafting ? "CHI TIẾT CỘT - THỐNG KÊ THÉP" : "CHI TIẾT THÉP DẦM";
            return s;
        }
    }
}

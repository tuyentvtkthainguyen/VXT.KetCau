using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

namespace KetCauVXT.RevitExport
{
    // =====================================================================
    // BỘ BẢN VẼ KIẾN TRÚC CƠ BẢN – từ mô hình đang mở
    // Mặt bằng các tầng (tag phòng, cửa), 4 mặt đứng, 1 mặt cắt dọc, phối cảnh 3D, bảng thống kê cửa / phòng → các tờ KT-01…
    // Chạy lại: khung nhìn / tờ đã có (theo tên, số tờ) được giữ, chỉ thêm phần thiếu. Kỹ sư hoàn thiện kích thước, ghi chú trên Revit.
    // =====================================================================
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DrawingSetCommand : IExternalCommand
    {
        internal const string Title = "Bộ bản vẽ kiến trúc";
        /// <summary>Hướng mặt tiền (độ, 0 = +X, 270 = −Y). Agent đặt theo phương án; mặc định −Y (mép dưới mặt bằng là mặt đường).</summary>
        internal static double FrontDeg = 270;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements) => Run(commandData.Application, ref message);

        internal Result Run(UIApplication uiapp, ref string message)
        {
            var doc = uiapp.ActiveUIDocument?.Document;
            if (doc == null || doc.IsFamilyDocument) { message = "Mở một dự án Revit trước."; return Result.Failed; }
            var log = new List<string>();
            int nv = 0, ns = 0;
            try
            {
                using (var t = new Transaction(doc, Title))
                {
                    t.Start();
                    BuildArchitectureCommand.Swallow(t, log);
                    Make(doc, FrontDeg, log, ref nv, ref ns);
                    t.Commit();
                }
            }
            catch (Exception ex) { message = Title + ": " + ex.Message; AgentMode.Report = message; return Result.Failed; }
            string rep = $"Đã tạo {nv} khung nhìn, {ns} tờ bản vẽ." + (log.Count > 0 ? "\n\nGhi chú:\n• " + string.Join("\n• ", log.Take(30)) : "");
            AgentMode.Report = rep;
            if (!AgentMode.Quiet) TaskDialog.Show(Title, rep);
            return Result.Succeeded;
        }

        private static T Find<T>(Document d, string name) where T : View =>
            new FilteredElementCollector(d).OfClass(typeof(T)).Cast<T>().FirstOrDefault(v => !v.IsTemplate && v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        private static ViewFamilyType Vft(Document d, ViewFamily f) =>
            new FilteredElementCollector(d).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault(v => v.ViewFamily == f);

        /// <summary>Khung bao tường (ft) của toàn mô hình.</summary>
        internal static BoundingBoxXYZ ModelBox(Document d)
        {
            var walls = new FilteredElementCollector(d).OfClass(typeof(Wall)).Cast<Wall>().ToList();
            if (walls.Count == 0) return null;
            double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
            foreach (var w in walls)
            {
                var bb = w.get_BoundingBox(null);
                if (bb == null) continue;
                x0 = Math.Min(x0, bb.Min.X); y0 = Math.Min(y0, bb.Min.Y); z0 = Math.Min(z0, bb.Min.Z);
                x1 = Math.Max(x1, bb.Max.X); y1 = Math.Max(y1, bb.Max.Y); z1 = Math.Max(z1, bb.Max.Z);
            }
            return x0 > x1 ? null : new BoundingBoxXYZ { Min = new XYZ(x0, y0, z0), Max = new XYZ(x1, y1, z1) };
        }

        internal static XYZ Dir(double deg) => new XYZ(Math.Cos(deg * Math.PI / 180), Math.Sin(deg * Math.PI / 180), 0);

        private static void Make(Document d, double frontDeg, List<string> log, ref int nv, ref int ns)
        {
            var box = ModelBox(d);
            if (box == null) { log.Add("Mô hình chưa có tường – dựng kiến trúc trước."); return; }
            var sheets = new List<(string No, string Name, List<ElementId> Views)>();
            // tỷ lệ: nhỏ nhất trong 1:50 / 75 / 100 / 150 / 200 để chiều dài nhà ≤ 46 cm trên giấy (vừa khổ A1–A2)
            double lenM = VxtRevit.ToM(Math.Max(box.Max.X - box.Min.X, box.Max.Y - box.Min.Y));
            // khổ giấy theo khung tên của dự án / template: đo vùng vẽ trên một tờ tạm (trừ chỗ cho kích thước, chú thích ~ 15 %)
            double maxW = 460, maxH = 320;
            {
                var tb0 = TitleBlockType(d);
                if (tb0 != null && tb0 != ElementId.InvalidElementId)
                {
                    try
                    {
                        var tmp = ViewSheet.Create(d, tb0);
                        var a = DrawArea(d, tmp);
                        maxW = VxtRevit.ToM(a.Max.U - a.Min.U) * 1000 * 0.85; maxH = VxtRevit.ToM(a.Max.V - a.Min.V) * 1000 * 0.85;
                        d.Delete(tmp.Id);
                    }
                    catch { }
                }
            }
            double depthM = VxtRevit.ToM(Math.Min(box.Max.X - box.Min.X, box.Max.Y - box.Min.Y)) + 6;
            int sc = new[] { 50, 75, 100, 150, 200 }.FirstOrDefault(k => (lenM + 6) / k * 1000 <= maxW && depthM / k * 1000 <= maxH);
            if (sc == 0) sc = 200;

            // 1. mặt bằng các tầng có tường
            var wallLevels = new HashSet<ElementId>(new FilteredElementCollector(d).OfClass(typeof(Wall)).Cast<Wall>().Select(w => w.LevelId));
            var levels = VxtRevit.Levels(d).Where(l => wallLevels.Contains(l.Id)).ToList();
            var vftPlan = Vft(d, ViewFamily.FloorPlan);
            var rooms = new FilteredElementCollector(d).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().Cast<Room>().Where(r => r.Area > 0).ToList();
            var plans = new List<ElementId>();
            foreach (var lv in levels)
            {
                string name = "KT - Mặt bằng " + lv.Name;
                var v = Find<ViewPlan>(d, name);
                if (v == null && vftPlan != null)
                {
                    v = ViewPlan.Create(d, vftPlan.Id, lv.Id);
                    try { v.Name = name; } catch { }
                    v.Scale = sc; nv++;
                    // vùng cắt mặt bằng ôm công trình + 3 m (bỏ ký hiệu mặt đứng, phần tử mẫu ở xa)
                    try
                    {
                        double m3 = VxtRevit.ToFt(3.0);
                        var cb = v.CropBox;
                        v.CropBox = new BoundingBoxXYZ { Transform = cb.Transform, Min = new XYZ(box.Min.X - m3, box.Min.Y - m3, cb.Min.Z), Max = new XYZ(box.Max.X + m3, box.Max.Y + m3, cb.Max.Z) };
                        v.CropBoxActive = true; v.CropBoxVisible = false;
                    }
                    catch { }
                    try { v.DetailLevel = ViewDetailLevel.Medium; } catch { }
                    d.Regenerate();
                    foreach (var r in rooms.Where(r => r.LevelId == lv.Id))
                        try { if (r.Location is LocationPoint lp) d.Create.NewRoomTag(new LinkElementId(r.Id), new UV(lp.Point.X, lp.Point.Y), v.Id); } catch (Exception ex) { log.Add("Tag phòng " + r.Name + ": " + ex.Message); break; }
                    TagCategory(d, v, BuiltInCategory.OST_Doors, lv, log);
                    TagCategory(d, v, BuiltInCategory.OST_Windows, lv, log);
                    GridDims(d, v, box, log);
                }
                if (v != null) { plans.Add(v.Id); sheets.Add(("", "Mặt bằng " + lv.Name, new List<ElementId> { v.Id })); }
            }

            // 2. mặt đứng 4 phía (chính / sau / trái / phải theo hướng mặt tiền)
            var vftEl = Vft(d, ViewFamily.Elevation);
            var c = (box.Min + box.Max) / 2;
            double half = Math.Max(box.Max.X - box.Min.X, box.Max.Y - box.Min.Y) / 2 + VxtRevit.ToFt(6);
            var sides = new[] { (Deg: frontDeg, Name: "Mặt đứng chính"), (Deg: frontDeg + 180, Name: "Mặt đứng sau"), (Deg: frontDeg + 90, Name: "Mặt đứng bên phải"), (Deg: frontDeg - 90, Name: "Mặt đứng bên trái") };
            var elevIds = new List<ElementId>();
            if (vftEl != null && plans.Count > 0)
                foreach (var sd in sides)
                {
                    string name = "KT - " + sd.Name;
                    var ex = Find<ViewSection>(d, name);
                    if (ex != null) { elevIds.Add(ex.Id); continue; }
                    var n = Dir(sd.Deg);
                    try
                    {
                        // mặt đứng dựng bằng khung nhìn cắt đặt ngoài mặt nhà, nhìn vào (không cắt qua công trình) – chắc chắn đúng hướng
                        var vftS = Vft(d, ViewFamily.Section);
                        if (vftS == null) { log.Add("Dự án chưa có loại khung nhìn mặt cắt."); continue; }
                        // mặt phẳng cắt của khung nhìn nằm ở phía Min.Z, nhìn theo +BasisZ → BasisZ = −n để đứng ngoài mặt nhà phía n nhìn vào
                        var bz = n.Negate(); var by = XYZ.BasisZ; var bx = by.CrossProduct(bz);
                        double halfW = Math.Abs(bx.X) * (box.Max.X - box.Min.X) / 2 + Math.Abs(bx.Y) * (box.Max.Y - box.Min.Y) / 2;
                        double halfD = Math.Abs(n.X) * (box.Max.X - box.Min.X) / 2 + Math.Abs(n.Y) * (box.Max.Y - box.Min.Y) / 2;
                        var tf = Transform.Identity; tf.Origin = new XYZ(c.X, c.Y, box.Min.Z); tf.BasisX = bx; tf.BasisY = by; tf.BasisZ = bz;
                        double m1 = VxtRevit.ToFt(1.5);
                        var eb = new BoundingBoxXYZ { Transform = tf, Min = new XYZ(-halfW - m1, -m1, -halfD - m1), Max = new XYZ(halfW + m1, box.Max.Z - box.Min.Z + m1, halfD + VxtRevit.ToFt(0.6)) };
                        var keep = ViewSection.CreateSection(d, vftS.Id, eb);
                        try { keep.Name = name; } catch { }
                        keep.Scale = sc; nv++;
                        elevIds.Add(keep.Id);
                    }
                    catch (Exception e2) { log.Add(sd.Name + ": " + e2.Message); }
                }
            // mỗi mặt đứng một tờ (tỷ lệ 1:50 hai mặt đứng chồng lên nhau trên một tờ)
            for (int i = 0; i < elevIds.Count; i++)
            {
                string nm = (d.GetElement(elevIds[i]) as View)?.Name ?? "Mặt đứng";
                sheets.Add(("", nm.StartsWith("KT - ") ? nm.Substring(5) : nm, new List<ElementId> { elevIds[i] }));
            }

            // 3. mặt cắt dọc qua giữa nhà (song song cạnh dài)
            var vftSec = Vft(d, ViewFamily.Section);
            var secName = "KT - Mặt cắt A-A";
            var sec = Find<ViewSection>(d, secName);
            if (sec == null && vftSec != null)
                try
                {
                    bool alongY = (box.Max.Y - box.Min.Y) >= (box.Max.X - box.Min.X);
                    var bx = alongY ? XYZ.BasisY : XYZ.BasisX;     // phương cắt (ngang trên khung nhìn)
                    var by = XYZ.BasisZ;
                    var bz = bx.CrossProduct(by);                     // hướng về phía người xem
                    double L = alongY ? box.Max.Y - box.Min.Y : box.Max.X - box.Min.X, W = alongY ? box.Max.X - box.Min.X : box.Max.Y - box.Min.Y;
                    var tf = Transform.Identity; tf.Origin = new XYZ(c.X, c.Y, box.Min.Z); tf.BasisX = bx; tf.BasisY = by; tf.BasisZ = bz;
                    double m1 = VxtRevit.ToFt(1);
                    var sb = new BoundingBoxXYZ { Transform = tf, Min = new XYZ(-L / 2 - m1, -m1, 0), Max = new XYZ(L / 2 + m1, box.Max.Z - box.Min.Z + m1, W / 2 + m1) };
                    sec = ViewSection.CreateSection(d, vftSec.Id, sb);
                    try { sec.Name = secName; } catch { }
                    sec.Scale = sc; nv++;
                }
                catch (Exception e3) { log.Add("Mặt cắt: " + e3.Message); }
            if (sec != null) sheets.Add(("", "Mặt cắt A-A", new List<ElementId> { sec.Id }));

            // 3b. chi tiết cầu thang: mặt bằng phóng to từng tầng có chân thang + mặt cắt B-B qua lồng thang (1:50)
            try
            {
                var stairs = new FilteredElementCollector(d).OfCategory(BuiltInCategory.OST_Stairs).WhereElementIsNotElementType()
                    .Where(e => { try { return VxtRevit.GetVxtId(e).StartsWith(BuildArchitectureCommand.Prefix, StringComparison.Ordinal); } catch { return false; } }).ToList();
                if (stairs.Count > 0)
                {
                    double sx0 = double.MaxValue, sy0 = double.MaxValue, sx1 = double.MinValue, sy1 = double.MinValue;
                    var baseLv = new List<Level>();
                    foreach (var st in stairs)
                    {
                        var bb = st.get_BoundingBox(null); if (bb == null) continue;
                        sx0 = Math.Min(sx0, bb.Min.X); sy0 = Math.Min(sy0, bb.Min.Y); sx1 = Math.Max(sx1, bb.Max.X); sy1 = Math.Max(sy1, bb.Max.Y);
                        if (st is Autodesk.Revit.DB.Architecture.Stairs sts && d.GetElement(sts.get_Parameter(BuiltInParameter.STAIRS_BASE_LEVEL_PARAM)?.AsElementId() ?? ElementId.InvalidElementId) is Level bl && !baseLv.Any(x => x.Id == bl.Id)) baseLv.Add(bl);
                    }
                    double mm = VxtRevit.ToFt(1.2);
                    var detail = new List<ElementId>();
                    foreach (var lv in baseLv.OrderBy(l => l.Elevation))
                    {
                        string name = "KT - Chi tiết thang " + lv.Name;
                        var v = Find<ViewPlan>(d, name);
                        if (v == null && vftPlan != null)
                        {
                            v = ViewPlan.Create(d, vftPlan.Id, lv.Id);
                            try { v.Name = name; } catch { }
                            try { v.ViewTemplateId = ElementId.InvalidElementId; } catch { } // mẫu khung nhìn của template có thể khoá tỷ lệ 1:100
                            v.Scale = 50; nv++;
                            try { v.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE)?.Set(1); } catch { }
                            var cb = v.CropBox;
                            v.CropBox = new BoundingBoxXYZ { Transform = cb.Transform, Min = new XYZ(sx0 - mm, sy0 - mm, cb.Min.Z), Max = new XYZ(sx1 + mm, sy1 + mm, cb.Max.Z) };
                            v.CropBoxActive = true; v.CropBoxVisible = false;
                            try { v.DetailLevel = ViewDetailLevel.Fine; } catch { }
                        }
                        if (v != null) detail.Add(v.Id);
                    }
                    // mặt cắt B-B: mặt phẳng chứa phương vế dài, qua giữa lồng thang
                    var bbName = "KT - Mặt cắt B-B (cầu thang)";
                    var sb2 = Find<ViewSection>(d, bbName);
                    if (sb2 == null && vftSec != null)
                    {
                        bool runsY = (sy1 - sy0) >= (sx1 - sx0);
                        var bx = runsY ? XYZ.BasisY : XYZ.BasisX; var by = XYZ.BasisZ; var bz = bx.CrossProduct(by);
                        var cc = new XYZ((sx0 + sx1) / 2, (sy0 + sy1) / 2, box.Min.Z);
                        double L = runsY ? sy1 - sy0 : sx1 - sx0, W = runsY ? sx1 - sx0 : sy1 - sy0;
                        var tf = Transform.Identity; tf.Origin = cc; tf.BasisX = bx; tf.BasisY = by; tf.BasisZ = bz;
                        var sb = new BoundingBoxXYZ { Transform = tf, Min = new XYZ(-L / 2 - mm, -VxtRevit.ToFt(0.8), 0), Max = new XYZ(L / 2 + mm, box.Max.Z - box.Min.Z + VxtRevit.ToFt(0.8), W / 2 + VxtRevit.ToFt(0.3)) };
                        sb2 = ViewSection.CreateSection(d, vftSec.Id, sb);
                        try { sb2.Name = bbName; } catch { }
                        try { sb2.ViewTemplateId = ElementId.InvalidElementId; } catch { }
                        sb2.Scale = 50; nv++;
                        try { sb2.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE)?.Set(1); } catch { }
                        try { sb2.DetailLevel = ViewDetailLevel.Fine; } catch { }
                    }
                    if (sb2 != null) detail.Add(sb2.Id);
                    if (detail.Count > 0) sheets.Add(("", "Chi tiết cầu thang", detail));
                }
            }
            catch (Exception e4) { log.Add("Chi tiết thang: " + e4.Message); }

            // 4. phối cảnh 3D
            var v3 = Ensure3D(d, frontDeg, log);
            if (v3 != null) { sheets.Add(("", "Phối cảnh", new List<ElementId> { v3.Id })); }

            // 5. thống kê cửa, phòng
            var sch = new List<ElementId>();
            var s1 = Schedule(d, "KT - Thống kê cửa đi", BuiltInCategory.OST_Doors, log); if (s1 != null) sch.Add(s1.Id);
            var s2 = Schedule(d, "KT - Thống kê cửa sổ", BuiltInCategory.OST_Windows, log); if (s2 != null) sch.Add(s2.Id);
            var s3 = Schedule(d, "KT - Thống kê phòng", BuiltInCategory.OST_Rooms, log); if (s3 != null) sch.Add(s3.Id);

            // 6. tờ bản vẽ
            var tb = TitleBlockType(d);
            if (tb == null || tb == ElementId.InvalidElementId) log.Add("Dự án chưa có khung tên – tờ được tạo không khung, nạp khung tên rồi đổi sau.");
            var used = new HashSet<string>(new FilteredElementCollector(d).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Select(s => s.SheetNumber), StringComparer.OrdinalIgnoreCase);
            var placed = new HashSet<ElementId>(new FilteredElementCollector(d).OfClass(typeof(Viewport)).Cast<Viewport>().Select(vp => vp.ViewId));
            int k = 0;
            // số tờ: KT-01… theo thứ tự; số đã có tờ (lần chạy trước) thì lấy số trống kế tiếp
            string NextNo() { string n; do { k++; n = "KT-" + k.ToString("00", CultureInfo.InvariantCulture); } while (used.Contains(n)); used.Add(n); return n; }
            foreach (var sh in sheets)
            {
                var views = sh.Views.Where(id => !placed.Contains(id)).ToList();
                if (views.Count == 0) continue;
                string no = NextNo();
                var sheet = ViewSheet.Create(d, tb ?? ElementId.InvalidElementId);
                sheet.SheetNumber = no; sheet.Name = sh.Name; ns++;
                Place(d, sheet, views, log);
            }
            // danh mục bản vẽ: tờ bìa của template (tờ chưa có khung nhìn) hoặc tờ mới
            try
            {
                var listName = "KT - Danh mục bản vẽ";
                if (Find<ViewSchedule>(d, listName) == null)
                {
                    var sl = ViewSchedule.CreateSheetList(d);
                    try { sl.Name = listName; } catch { }
                    var def = sl.Definition;
                    foreach (var sf in def.GetSchedulableFields())
                    {
                        var pid = sf.ParameterId;
                        if (pid == new ElementId(BuiltInParameter.SHEET_NUMBER) || pid == new ElementId(BuiltInParameter.SHEET_NAME)) def.AddField(sf);
                    }
                    var cover = new FilteredElementCollector(d).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                        .Where(x => !x.IsPlaceholder && x.GetAllPlacedViews().Count == 0).OrderBy(x => x.SheetNumber).FirstOrDefault();
                    if (cover == null) { cover = ViewSheet.Create(d, tb ?? ElementId.InvalidElementId); cover.SheetNumber = "KT-00"; }
                    try { cover.Name = "Danh mục bản vẽ"; } catch { }
                    var oc = DrawArea(d, cover);
                    ScheduleSheetInstance.Create(d, cover.Id, sl.Id, new XYZ(oc.Min.U + VxtRevit.ToFt(0.02), oc.Max.V - VxtRevit.ToFt(0.02), 0));
                }
            }
            catch (Exception e6) { log.Add("Danh mục bản vẽ: " + e6.Message); }
            if (sch.Count > 0)
            {
                bool already = new FilteredElementCollector(d).OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>().Any(x => sch.Contains(x.ScheduleId));
                if (!already)
                {
                    string no = NextNo();
                    var sheet = ViewSheet.Create(d, tb ?? ElementId.InvalidElementId);
                    sheet.SheetNumber = no; sheet.Name = "Thống kê cửa, phòng"; ns++;
                    var o = DrawArea(d, sheet);
                    double gap = VxtRevit.ToFt(0.01), x = o.Min.U + gap, y = o.Max.V - gap, colW = 0;
                    foreach (var id in sch)
                        try
                        {
                            var si = ScheduleSheetInstance.Create(d, sheet.Id, id, new XYZ(x, y, 0));
                            d.Regenerate();
                            var bb = si.get_BoundingBox(sheet);
                            if (bb == null) { y -= VxtRevit.ToFt(0.09); continue; }
                            double bh = bb.Max.Y - bb.Min.Y, bw = bb.Max.X - bb.Min.X;
                            if (y - bh < o.Min.V && y < o.Max.V - gap - 1e-6)
                            {
                                // hết chỗ theo chiều cao → sang cột kế bên
                                x += colW + gap; y = o.Max.V - gap; colW = 0;
                                ElementTransformUtils.MoveElement(d, si.Id, new XYZ(x - bb.Min.X, y - bb.Max.Y, 0));
                            }
                            else ElementTransformUtils.MoveElement(d, si.Id, new XYZ(x - bb.Min.X, y - bb.Max.Y, 0));
                            y -= bh + gap; colW = Math.Max(colW, bw);
                        }
                        catch (Exception e5) { log.Add("Đặt bảng thống kê: " + e5.Message); }
                }
            }
        }

        private static BoundingBoxUV Outline(ViewSheet s)
        {
            var o = s.Outline;
            if (o == null || o.Max.U - o.Min.U < 0.1) return new BoundingBoxUV(0, 0, VxtRevit.ToFt(0.42), VxtRevit.ToFt(0.297));
            return o;
        }

        /// <summary>Khung tên: loại đang dùng trên các tờ có sẵn của dự án / template (khung tên công ty), không thì loại đầu tiên.</summary>
        internal static ElementId TitleBlockType(Document d)
        {
            var used = new FilteredElementCollector(d).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType().Cast<FamilyInstance>()
                .GroupBy(f => f.Symbol.Id).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
            if (used != null && used != ElementId.InvalidElementId) return used;
            return new FilteredElementCollector(d).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_TitleBlocks).FirstElementId();
        }

        /// <summary>Kích thước trục trên mặt bằng: chuỗi khoảng cách trục + tổng, theo hai phương, đặt ngoài công trình.</summary>
        internal static int GridDims(Document d, ViewPlan v, BoundingBoxXYZ box, List<string> log)
        {
            int n = 0;
            var grids = new FilteredElementCollector(d).OfClass(typeof(Grid)).Cast<Grid>().Where(g => g.Curve is Line).ToList();
            double z = v.GenLevel?.Elevation ?? 0;
            foreach (bool vertical in new[] { true, false })
            {
                // lưới đứng (chạy theo Y) → kích thước theo X đặt dưới công trình; lưới ngang → theo Y đặt bên trái
                var gs = grids.Where(g => { var dir = ((Line)g.Curve).Direction; return vertical ? Math.Abs(dir.Y) > 0.99 : Math.Abs(dir.X) > 0.99; })
                              .OrderBy(g => vertical ? g.Curve.GetEndPoint(0).X : g.Curve.GetEndPoint(0).Y).ToList();
                if (gs.Count < 2) continue;
                for (int k = 0; k < 2; k++)
                {
                    var ra = new ReferenceArray();
                    if (k == 0) foreach (var g in gs) ra.Append(new Reference(g));
                    else { ra.Append(new Reference(gs.First())); ra.Append(new Reference(gs.Last())); }
                    double off = VxtRevit.ToFt(1.0 + 0.6 * k);
                    Line ln = vertical
                        ? Line.CreateBound(new XYZ(box.Min.X - 1, box.Min.Y - off, z), new XYZ(box.Max.X + 1, box.Min.Y - off, z))
                        : Line.CreateBound(new XYZ(box.Min.X - off, box.Min.Y - 1, z), new XYZ(box.Min.X - off, box.Max.Y + 1, z));
                    try { d.Create.NewDimension(v, ln, ra); n++; }
                    catch (Exception ex) { log.Add($"Kích thước trục ({v.Name}): {ex.Message}"); }
                }
            }
            return n;
        }

        /// <summary>Vùng vẽ của tờ: theo khung tên thật (khung của template có thể đặt gốc ở bất kỳ đâu), trừ dải khung tên bên phải / dưới.</summary>
        internal static BoundingBoxUV DrawArea(Document d, ViewSheet sheet)
        {
            try
            {
                d.Regenerate();
                var tbi = new FilteredElementCollector(d, sheet.Id).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType().FirstElement();
                var bb = tbi?.get_BoundingBox(sheet);
                if (bb != null && bb.Max.X - bb.Min.X > 0.3)
                {
                    double w = bb.Max.X - bb.Min.X, h = bb.Max.Y - bb.Min.Y;
                    // khung tên đứng (dải bên phải) khi tờ nằm ngang; khung tên nằm (dải dưới) khi tờ đứng
                    if (w >= h) return new BoundingBoxUV(bb.Min.X + w * 0.04, bb.Min.Y + h * 0.04, bb.Max.X - w * 0.2, bb.Max.Y - h * 0.04);
                    return new BoundingBoxUV(bb.Min.X + w * 0.05, bb.Min.Y + h * 0.18, bb.Max.X - w * 0.05, bb.Max.Y - h * 0.04);
                }
            }
            catch { }
            return Outline(sheet);
        }

        private static void Place(Document d, ViewSheet sheet, List<ElementId> views, List<string> log)
        {
            var o = DrawArea(d, sheet);
            double w = o.Max.U - o.Min.U, h = o.Max.V - o.Min.V;
            for (int i = 0; i < views.Count; i++)
            {
                // 1 khung nhìn: giữa tờ; 2 khung nhìn: trên / dưới
                double cy = views.Count == 1 ? o.Min.V + h / 2 : o.Min.V + h * (i == 0 ? 0.72 : 0.3), cx = o.Min.U + w * 0.5;
                if (views.Count >= 3)
                {
                    // cột trái: các mặt bằng xếp chồng; cột phải: khung nhìn cuối (mặt cắt)
                    int nl = views.Count - 1;
                    if (i < nl) { cx = o.Min.U + w * 0.28; cy = o.Max.V - h * (i + 0.5) / nl; }
                    else { cx = o.Min.U + w * 0.75; cy = o.Min.V + h / 2; }
                }
                var pt = new XYZ(cx, cy, 0);
                try { if (Viewport.CanAddViewToSheet(d, sheet.Id, views[i])) Viewport.Create(d, sheet.Id, views[i], pt); }
                catch (Exception ex) { log.Add("Đặt khung nhìn lên " + sheet.SheetNumber + ": " + ex.Message); }
            }
        }

        private static void TagCategory(Document d, View v, BuiltInCategory cat, Level lv, List<string> log)
        {
            foreach (var e in new FilteredElementCollector(d).OfClass(typeof(FamilyInstance)).OfCategory(cat).Cast<FamilyInstance>().Where(f => f.LevelId == lv.Id))
            {
                try
                {
                    if (!(e.Location is LocationPoint lp)) continue;
                    IndependentTag.Create(d, v.Id, new Reference(e), false, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, lp.Point);
                }
                catch (Exception ex) { log.Add("Tag " + (cat == BuiltInCategory.OST_Doors ? "cửa đi" : "cửa sổ") + ": " + ex.Message + " (nạp họ tag rồi chạy lại)"); return; }
            }
        }

        private static ViewSchedule Schedule(Document d, string name, BuiltInCategory cat, List<string> log)
        {
            var ex = Find<ViewSchedule>(d, name);
            if (ex != null) return ex;
            try
            {
                var s = ViewSchedule.CreateSchedule(d, new ElementId(cat));
                try { s.Name = name; } catch { }
                var def = s.Definition;
                var want = cat == BuiltInCategory.OST_Rooms
                    ? new[] { BuiltInParameter.ROOM_LEVEL_ID, BuiltInParameter.ROOM_NUMBER, BuiltInParameter.ROOM_NAME, BuiltInParameter.ROOM_AREA }
                    : new[] { BuiltInParameter.ELEM_TYPE_PARAM, BuiltInParameter.FAMILY_LEVEL_PARAM };
                var fields = def.GetSchedulableFields();
                foreach (var bip in want)
                {
                    var f = fields.FirstOrDefault(x => x.ParameterId == new ElementId(bip));
                    if (f != null) def.AddField(f);
                }
                if (cat != BuiltInCategory.OST_Rooms)
                {
                    var cnt = fields.FirstOrDefault(x => x.FieldType == ScheduleFieldType.Count);
                    if (cnt != null) def.AddField(cnt);
                    var tf = def.GetFieldCount() > 0 ? def.GetField(0) : null;
                    if (tf != null) { def.AddSortGroupField(new ScheduleSortGroupField(tf.FieldId)); def.IsItemized = false; }
                }
                return s;
            }
            catch (Exception e) { log.Add(name + ": " + e.Message); return null; }
        }

        // ------------------------------------------------------------------ 3D
        internal static View3D Ensure3D(Document d, double frontDeg, List<string> log)
        {
            const string name = "KT - Phối cảnh 3D";
            var v = Find<View3D>(d, name);
            var box = ModelBox(d);
            try
            {
                if (v == null)
                {
                    var vft = Vft(d, ViewFamily.ThreeDimensional);
                    if (vft == null) return null;
                    v = View3D.CreateIsometric(d, vft.Id);
                    try { v.Name = name; } catch { }
                }
                if (box != null)
                {
                    var c = (box.Min + box.Max) / 2;
                    double size = (box.Max - box.Min).GetLength();
                    var n = Dir(frontDeg);
                    var side = Dir(frontDeg + 90);
                    var eye = c + n * size * 1.1 + side * size * 0.55 + XYZ.BasisZ * size * 0.45;
                    var fwd = (c - eye).Normalize();
                    var right = fwd.CrossProduct(XYZ.BasisZ).Normalize();
                    var up = right.CrossProduct(fwd).Normalize();
                    v.SetOrientation(new ViewOrientation3D(eye, up, fwd));
                    // hộp cắt ôm công trình (+1 m) → ảnh xuất "vừa trang" ôm sát nhà, không bị thu nhỏ vì phần tử mẫu ở xa
                    try
                    {
                        double m1 = VxtRevit.ToFt(1.0);
                        var sb = new BoundingBoxXYZ { Min = box.Min - new XYZ(m1, m1, m1), Max = box.Max + new XYZ(m1, m1, m1) };
                        v.IsSectionBoxActive = true;
                        v.SetSectionBox(sb);
                    }
                    catch { }
                    // vùng cắt (crop) = hình chiếu khung bao công trình lên mặt phẳng nhìn → xuất ảnh ôm sát nhà
                    try
                    {
                        d.Regenerate();
                        var cb = v.CropBox;
                        var inv = cb.Transform.Inverse;
                        double ux0 = double.MaxValue, uy0 = double.MaxValue, ux1 = double.MinValue, uy1 = double.MinValue;
                        foreach (double x in new[] { box.Min.X, box.Max.X })
                            foreach (double y in new[] { box.Min.Y, box.Max.Y })
                                foreach (double z in new[] { box.Min.Z, box.Max.Z })
                                {
                                    var q = inv.OfPoint(new XYZ(x, y, z));
                                    ux0 = Math.Min(ux0, q.X); uy0 = Math.Min(uy0, q.Y); ux1 = Math.Max(ux1, q.X); uy1 = Math.Max(uy1, q.Y);
                                }
                        double pad = Math.Max(ux1 - ux0, uy1 - uy0) * 0.06;
                        var nb = new BoundingBoxXYZ { Transform = cb.Transform, Min = new XYZ(ux0 - pad, uy0 - pad, cb.Min.Z), Max = new XYZ(ux1 + pad, uy1 + pad, cb.Max.Z) };
                        v.CropBox = nb;
                        v.CropBoxActive = true;
                        v.CropBoxVisible = false;
                    }
                    catch (Exception ce) { log.Add("Vùng cắt ảnh 3D: " + ce.Message); }
                }
                // ảnh phối cảnh: ẩn trục, cao độ, ký hiệu → khung hình ôm sát công trình
                foreach (var bic in new[] { BuiltInCategory.OST_Grids, BuiltInCategory.OST_Levels, BuiltInCategory.OST_SectionBox, BuiltInCategory.OST_RoomSeparationLines, BuiltInCategory.OST_Rooms })
                    try { v.SetCategoryHidden(new ElementId(bic), true); } catch { }
                // Tô bóng (Shaded) dùng màu vật liệu (Material.Color) – vật liệu mặt tiền của phương án hiện đúng màu, kính trong suốt
                try { v.DisplayStyle = DisplayStyle.ShadingWithEdges; } catch { }
                try { v.DetailLevel = ViewDetailLevel.Fine; } catch { }
            }
            catch (Exception e) { log.Add("Phối cảnh 3D: " + e.Message); }
            return v;
        }

        /// <summary>Xuất ảnh khung nhìn 3D (PNG). Gọi ngoài Transaction. Trả về đường dẫn ảnh.</summary>
        internal static string ExportImage(Document d, string path, double frontDeg, int px, List<string> log)
        {
            View3D v;
            using (var t = new Transaction(d, "Khung nhìn phối cảnh"))
            {
                t.Start(); BuildArchitectureCommand.Swallow(t, log);
                v = Ensure3D(d, frontDeg, log);
                t.Commit();
            }
            if (v == null) throw new InvalidOperationException("Không tạo được khung nhìn 3D.");
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(dir);
            var o = new ImageExportOptions
            {
                ExportRange = ExportRange.SetOfViews,
                FilePath = Path.Combine(dir, Path.GetFileNameWithoutExtension(path)),
                ZoomType = ZoomFitType.FitToPage,
                PixelSize = Math.Max(640, Math.Min(px, 6000)),
                ImageResolution = ImageResolution.DPI_150,
                HLRandWFViewsFileType = ImageFileType.PNG,
                ShadowViewsFileType = ImageFileType.PNG,
                FitDirection = FitDirectionType.Horizontal,
            };
            o.SetViewsAndSheets(new List<ElementId> { v.Id });
            var before = DateTime.Now.AddSeconds(-2);
            d.ExportImage(o);
            // Revit tự thêm tên khung nhìn vào tên file → tìm file mới nhất rồi đổi về tên yêu cầu
            var made = new DirectoryInfo(dir).GetFiles(Path.GetFileNameWithoutExtension(path) + "*.png")
                .Where(f => f.LastWriteTime >= before).OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
            if (made == null) throw new IOException("Revit không ghi ra ảnh.");
            string target = Path.ChangeExtension(Path.GetFullPath(path), ".png");
            if (!made.FullName.Equals(target, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(target)) File.Delete(target);
                made.MoveTo(target);
            }
            return target;
        }
    }

    /// <summary>Nút ribbon: lưu ảnh phối cảnh 3D (PNG) – để làm nền render AI / SketchUp.</summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Image3DCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument?.Document;
            if (doc == null || doc.IsFamilyDocument) { message = "Mở một dự án Revit trước."; return Result.Failed; }
            string path;
            using (var dlg = new System.Windows.Forms.SaveFileDialog { Title = "Lưu ảnh phối cảnh 3D", Filter = "Ảnh PNG (*.png)|*.png", FileName = "PhoiCanh_" + doc.Title + ".png" })
            {
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return Result.Cancelled;
                path = dlg.FileName;
            }
            var log = new List<string>();
            try
            {
                string f = DrawingSetCommand.ExportImage(doc, path, DrawingSetCommand.FrontDeg, 1920, log);
                TaskDialog.Show("Việt Const", "Đã lưu ảnh: " + f + (log.Count > 0 ? "\n\n" + string.Join("\n", log.Take(10)) : ""));
                return Result.Succeeded;
            }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }
}

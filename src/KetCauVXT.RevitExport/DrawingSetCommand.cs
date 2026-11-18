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
                    v.Scale = 100; nv++;
                    try { v.DetailLevel = ViewDetailLevel.Medium; } catch { }
                    d.Regenerate();
                    foreach (var r in rooms.Where(r => r.LevelId == lv.Id))
                        try { if (r.Location is LocationPoint lp) d.Create.NewRoomTag(new LinkElementId(r.Id), new UV(lp.Point.X, lp.Point.Y), v.Id); } catch (Exception ex) { log.Add("Tag phòng " + r.Name + ": " + ex.Message); break; }
                    TagCategory(d, v, BuiltInCategory.OST_Doors, lv, log);
                    TagCategory(d, v, BuiltInCategory.OST_Windows, lv, log);
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
                    var org = new XYZ(c.X + n.X * half, c.Y + n.Y * half, box.Min.Z);
                    try
                    {
                        var mk = ElevationMarker.CreateElevationMarker(d, vftEl.Id, org, 100);
                        // tạo đủ 4 hướng rồi mới xoá hướng thừa (xoá hết khung nhìn của ký hiệu thì Revit xoá luôn ký hiệu)
                        var evs = new List<ViewSection>();
                        for (int i = 0; i < mk.MaximumViewCount && i < 4; i++)
                            try { if (mk.IsAvailableIndex(i)) evs.Add(mk.CreateElevation(d, plans[0], i)); } catch { }
                        d.Regenerate();
                        ViewSection keep = evs.OrderByDescending(ev => ev.ViewDirection.DotProduct(n)).FirstOrDefault(ev => ev.ViewDirection.DotProduct(n) > 0.9);
                        foreach (var ev in evs) if (keep == null || ev.Id != keep.Id) d.Delete(ev.Id);
                        if (keep == null) { log.Add(sd.Name + ": không tạo được hướng nhìn."); continue; }
                        try { keep.Name = name; } catch { }
                        keep.Scale = 100; nv++;
                        elevIds.Add(keep.Id);
                    }
                    catch (Exception e2) { log.Add(sd.Name + ": " + e2.Message); }
                }
            for (int i = 0; i < elevIds.Count; i += 2) sheets.Add(("", i == 0 ? "Mặt đứng chính, mặt đứng sau" : "Mặt đứng bên", elevIds.Skip(i).Take(2).ToList()));

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
                    sec.Scale = 100; nv++;
                }
                catch (Exception e3) { log.Add("Mặt cắt: " + e3.Message); }
            if (sec != null) sheets.Add(("", "Mặt cắt A-A", new List<ElementId> { sec.Id }));

            // 4. phối cảnh 3D
            var v3 = Ensure3D(d, frontDeg, log);
            if (v3 != null) { sheets.Add(("", "Phối cảnh", new List<ElementId> { v3.Id })); }

            // 5. thống kê cửa, phòng
            var sch = new List<ElementId>();
            var s1 = Schedule(d, "KT - Thống kê cửa đi", BuiltInCategory.OST_Doors, log); if (s1 != null) sch.Add(s1.Id);
            var s2 = Schedule(d, "KT - Thống kê cửa sổ", BuiltInCategory.OST_Windows, log); if (s2 != null) sch.Add(s2.Id);
            var s3 = Schedule(d, "KT - Thống kê phòng", BuiltInCategory.OST_Rooms, log); if (s3 != null) sch.Add(s3.Id);

            // 6. tờ bản vẽ
            var tb = new FilteredElementCollector(d).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_TitleBlocks).FirstElementId();
            if (tb == null || tb == ElementId.InvalidElementId) log.Add("Dự án chưa có khung tên – tờ được tạo không khung, nạp khung tên rồi đổi sau.");
            var used = new HashSet<string>(new FilteredElementCollector(d).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Select(s => s.SheetNumber), StringComparer.OrdinalIgnoreCase);
            var placed = new HashSet<ElementId>(new FilteredElementCollector(d).OfClass(typeof(Viewport)).Cast<Viewport>().Select(vp => vp.ViewId));
            int k = 0;
            foreach (var sh in sheets)
            {
                k++;
                string no = "KT-" + k.ToString("00", CultureInfo.InvariantCulture);
                var views = sh.Views.Where(id => !placed.Contains(id)).ToList();
                if (views.Count == 0 || used.Contains(no)) continue;
                var sheet = ViewSheet.Create(d, tb ?? ElementId.InvalidElementId);
                sheet.SheetNumber = no; sheet.Name = sh.Name; ns++;
                Place(d, sheet, views, log);
            }
            if (sch.Count > 0)
            {
                k++;
                string no = "KT-" + k.ToString("00", CultureInfo.InvariantCulture);
                bool already = new FilteredElementCollector(d).OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>().Any(x => sch.Contains(x.ScheduleId));
                if (!used.Contains(no) && !already)
                {
                    var sheet = ViewSheet.Create(d, tb ?? ElementId.InvalidElementId);
                    sheet.SheetNumber = no; sheet.Name = "Thống kê cửa, phòng"; ns++;
                    var o = Outline(sheet);
                    double y = o.Max.V - VxtRevit.ToFt(0.03);
                    foreach (var id in sch)
                        try { ScheduleSheetInstance.Create(d, sheet.Id, id, new XYZ(o.Min.U + VxtRevit.ToFt(0.03), y, 0)); y -= VxtRevit.ToFt(0.09); }
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

        private static void Place(Document d, ViewSheet sheet, List<ElementId> views, List<string> log)
        {
            var o = Outline(sheet);
            double w = o.Max.U - o.Min.U, h = o.Max.V - o.Min.V;
            for (int i = 0; i < views.Count; i++)
            {
                // 1 khung nhìn: giữa tờ; 2 khung nhìn: trên / dưới
                double cy = views.Count == 1 ? o.Min.V + h / 2 : o.Min.V + h * (i == 0 ? 0.72 : 0.3);
                var pt = new XYZ(o.Min.U + w * 0.45, cy, 0);
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
                try { v.DisplayStyle = DisplayStyle.Realistic; } catch { }
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

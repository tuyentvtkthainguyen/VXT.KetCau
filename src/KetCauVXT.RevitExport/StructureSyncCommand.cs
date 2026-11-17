using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace KetCauVXT.RevitExport
{
    // =====================================================================
    // DỰNG KẾT CẤU TỪ VIỆT CONST  (kiến trúc → kết cấu)
    // Đọc *.vxt2revit.json: tạo cột, dầm (họ BTCT chữ nhật, loại theo tên Việt Const C22x30 / D22x40), sàn kết cấu (loại S100…),
    // thiếu cao độ thì tạo. Mỗi cấu kiện gắn mã Việt Const (Extensible Storage) – lần sau chỉ cập nhật, không tạo trùng.
    // NHẬN TIẾT DIỆN TỪ VIỆT CONST
    // Sau khi Việt Const tối ưu: tìm cấu kiện Revit tương ứng (theo mã đã gắn, không có thì theo vị trí ±50 mm) và đổi loại theo tiết diện mới.
    // Cả hai lệnh hiện danh sách thay đổi để người dùng chọn trước khi áp dụng.
    // =====================================================================
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class BuildStructureCommand : IExternalCommand
    {
        private const string Title = "Dựng kết cấu từ Việt Const";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument?.Document;
            if (doc == null || doc.IsFamilyDocument) { message = "Mở một dự án Revit trước."; return Result.Failed; }
            VxtStandard std;
            try { std = VxtStandard.Ask(Title + " – chọn file Việt Const (*.vxt2revit.json)", false); }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }
            if (std == null) return Result.Cancelled;
            if (std.Columns.Count + std.Beams.Count + std.Slabs.Count == 0) { TaskDialog.Show(Title, "File không có cột, dầm, sàn nào."); return Result.Cancelled; }

            var log = new List<string>();
            Family colFam, beamFam;
            using (var t = new Transaction(doc, "Nạp họ cột / dầm BTCT"))
            {
                t.Start();
                colFam = std.Columns.Count > 0 ? VxtRevit.RectFamily(doc, BuiltInCategory.OST_StructuralColumns, log) : null;
                beamFam = std.Beams.Count > 0 ? VxtRevit.RectFamily(doc, BuiltInCategory.OST_StructuralFraming, log) : null;
                t.Commit();
            }
            if (std.Columns.Count > 0 && colFam == null) { message = "Không tìm thấy họ cột BTCT chữ nhật (tham số b, h). Nạp họ \"M_Concrete-Rectangular-Column\" (hoặc họ cột BTCT của bạn có tham số b, h) rồi chạy lại."; return Result.Failed; }
            if (std.Beams.Count > 0 && beamFam == null) { message = "Không tìm thấy họ dầm BTCT chữ nhật (tham số b, h). Nạp họ \"M_Concrete-Rectangular Beam\" (hoặc họ dầm BTCT có tham số b, h) rồi chạy lại."; return Result.Failed; }

            var existing = new Dictionary<string, Element>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in new FilteredElementCollector(doc).WhereElementIsNotElementType()
                         .WherePasses(new LogicalOrFilter(new List<ElementFilter> { new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns), new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming), new ElementCategoryFilter(BuiltInCategory.OST_Floors) })))
            {
                string id = VxtRevit.GetVxtId(e);
                if (id.Length > 0 && !existing.ContainsKey(id)) existing[id] = e;
            }

            var items = new List<ChangeItem>();
            // cao độ thiếu
            var lv = VxtRevit.Levels(doc);
            var missLv = std.Levels.Where(v => VxtRevit.LevelAt(lv, v.Elevation) == null).ToList();
            if (missLv.Count > 0)
                items.Add(new ChangeItem
                {
                    Group = "Cao độ", Order = 1, Text = "Tạo " + missLv.Count + " cao độ: " + string.Join(", ", missLv.Select(v => $"{v.Name} +{v.Elevation:0.000}")),
                    Apply = d => { foreach (var v in missLv) { var nl = Level.Create(d, VxtRevit.ToFt(v.Elevation)); nl.Elevation += VxtRevit.ToFt(v.Elevation) - nl.ProjectElevation; try { nl.Name = v.Name; } catch { } } },
                });

            // cột
            foreach (var g in std.Columns.GroupBy(c => c.Section))
            {
                var newOnes = g.Where(c => !existing.ContainsKey(c.Id)).ToList();
                var upd = g.Where(c => existing.ContainsKey(c.Id) && (existing[c.Id] as FamilyInstance)?.Symbol?.Name != c.Section).ToList();
                var c0 = g.First();
                if (newOnes.Count > 0)
                    items.Add(new ChangeItem
                    {
                        Group = "Cột", Order = 3, Text = $"Tạo {newOnes.Count} cột {c0.Section} ({c0.B * 1000:0}×{c0.H * 1000:0})",
                        Note = string.Join(", ", newOnes.Select(c => string.IsNullOrEmpty(c.Label) ? c.Id : c.Label).Distinct().Take(12)),
                        Apply = d => { foreach (var c in newOnes) CreateColumn(d, colFam, c); },
                    });
                if (upd.Count > 0)
                    items.Add(new ChangeItem { Group = "Cột", Order = 3, Text = $"Đổi {upd.Count} cột đã dựng sang loại {c0.Section}", Apply = d => { foreach (var c in upd) ChangeRect(d, existing[c.Id] as FamilyInstance, c.Section, c.B, c.H); } });
            }
            // dầm
            foreach (var g in std.Beams.GroupBy(c => c.Section))
            {
                var newOnes = g.Where(c => !existing.ContainsKey(c.Id)).ToList();
                var upd = g.Where(c => existing.ContainsKey(c.Id) && (existing[c.Id] as FamilyInstance)?.Symbol?.Name != c.Section).ToList();
                var b0 = g.First();
                if (newOnes.Count > 0)
                    items.Add(new ChangeItem
                    {
                        Group = "Dầm", Order = 3, Text = $"Tạo {newOnes.Count} dầm {b0.Section} ({b0.B * 1000:0}×{b0.H * 1000:0})",
                        Note = string.Join(", ", newOnes.Select(c => string.IsNullOrEmpty(c.Label) ? c.Id : c.Label).Distinct().Take(12)),
                        Apply = d => { foreach (var b in newOnes) CreateBeam(d, beamFam, b); },
                    });
                if (upd.Count > 0)
                    items.Add(new ChangeItem { Group = "Dầm", Order = 3, Text = $"Đổi {upd.Count} dầm đã dựng sang loại {b0.Section}", Apply = d => { foreach (var b in upd) ChangeRect(d, existing[b.Id] as FamilyInstance, b.Section, b.B, b.H); } });
            }
            // sàn
            foreach (var g in std.Slabs.GroupBy(c => c.Section))
            {
                var newOnes = g.Where(c => !existing.ContainsKey(c.Id)).ToList();
                var upd = g.Where(c => existing.ContainsKey(c.Id) && doc.GetElement(existing[c.Id].GetTypeId())?.Name != c.Section).ToList();
                var s0 = g.First();
                string mat = std.SlabTypes.FirstOrDefault(x => x.Name == s0.Section)?.Material ?? "Bê tông B25";
                if (newOnes.Count > 0)
                    items.Add(new ChangeItem
                    {
                        Group = "Sàn", Order = 3, Text = $"Tạo {newOnes.Count} ô sàn kết cấu {s0.Section} (dày {s0.Thickness * 1000:0} mm)",
                        Apply = d => { var ft = VxtRevit.SlabType(d, s0.Section, s0.Thickness, mat); foreach (var s in newOnes) CreateSlab(d, ft, s); },
                    });
                if (upd.Count > 0)
                    items.Add(new ChangeItem
                    {
                        Group = "Sàn", Order = 3, Text = $"Đổi {upd.Count} ô sàn đã dựng sang loại {s0.Section}",
                        Apply = d => { var ft = VxtRevit.SlabType(d, s0.Section, s0.Thickness, mat); foreach (var s in upd) existing[s.Id].ChangeTypeId(ft.Id); },
                    });
            }
            string intro = $"File {System.IO.Path.GetFileName(std.File)}: {std.Columns.Count} cột, {std.Beams.Count} dầm, {std.Slabs.Count} ô sàn. "
                           + (existing.Count > 0 ? $"{existing.Count} cấu kiện đã dựng lần trước (chỉ cập nhật). " : "")
                           + (log.Count > 0 ? string.Join("; ", log) + ". " : "") + "Chọn mục cần làm rồi bấm Áp dụng (Ctrl+Z để hoàn tác).";
            return ChangeDialog.Run(doc, Title, intro, items) == null ? Result.Cancelled : Result.Succeeded;
        }

        internal static void CreateColumn(Document d, Family fam, VxtColumn c)
        {
            var lv = VxtRevit.Levels(d);
            var baseLv = VxtRevit.LevelBelow(lv, c.ZBot);
            var topLv = VxtRevit.LevelAt(lv, c.ZTop) ?? VxtRevit.LevelBelow(lv, c.ZTop);
            var sym = VxtRevit.RectType(d, fam, c.Section, c.B, c.H);
            if (!sym.IsActive) { sym.Activate(); d.Regenerate(); }
            var fi = d.Create.NewFamilyInstance(new XYZ(VxtRevit.ToFt(c.X), VxtRevit.ToFt(c.Y), 0), sym, baseLv, StructuralType.Column);
            fi.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM)?.Set(baseLv.Id);
            fi.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM)?.Set(VxtRevit.ToFt(c.ZBot) - baseLv.ProjectElevation);
            fi.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(topLv.Id);
            fi.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM)?.Set(VxtRevit.ToFt(c.ZTop) - topLv.ProjectElevation);
            if (Math.Abs(c.Angle) > 1e-6)
            {
                var p = new XYZ(VxtRevit.ToFt(c.X), VxtRevit.ToFt(c.Y), 0);
                ElementTransformUtils.RotateElement(d, fi.Id, Line.CreateBound(p, p + XYZ.BasisZ), c.Angle * Math.PI / 180);
            }
            if (!string.IsNullOrEmpty(c.Label)) fi.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set(c.Label);
            VxtRevit.SetVxtId(fi, c.Id);
        }

        internal static void CreateBeam(Document d, Family fam, VxtBeam b)
        {
            var a = new XYZ(VxtRevit.ToFt(b.X1), VxtRevit.ToFt(b.Y1), VxtRevit.ToFt(b.Z));
            var e = new XYZ(VxtRevit.ToFt(b.X2), VxtRevit.ToFt(b.Y2), VxtRevit.ToFt(b.Z));
            if (a.DistanceTo(e) < 0.05) return;
            var lv = VxtRevit.LevelBelow(VxtRevit.Levels(d), b.Z);
            var sym = VxtRevit.RectType(d, fam, b.Section, b.B, b.H);
            if (!sym.IsActive) { sym.Activate(); d.Regenerate(); }
            var fi = d.Create.NewFamilyInstance(Line.CreateBound(a, e), sym, lv, StructuralType.Beam);
            if (!string.IsNullOrEmpty(b.Label)) fi.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set(b.Label);
            VxtRevit.SetVxtId(fi, b.Id);
        }

        internal static void CreateSlab(Document d, FloorType ft, VxtSlab s)
        {
            if (ft == null) throw new InvalidOperationException("dự án không có loại sàn nào để nhân bản");
            var lv = VxtRevit.LevelBelow(VxtRevit.Levels(d), s.Z);
            var loop = new CurveLoop();
            double z = lv.ProjectElevation;
            for (int i = 0; i < s.Outline.Count; i++)
            {
                var p = s.Outline[i]; var q = s.Outline[(i + 1) % s.Outline.Count];
                var a = new XYZ(VxtRevit.ToFt(p[0]), VxtRevit.ToFt(p[1]), z); var b = new XYZ(VxtRevit.ToFt(q[0]), VxtRevit.ToFt(q[1]), z);
                if (a.DistanceTo(b) > 1e-3) loop.Append(Line.CreateBound(a, b));
            }
            var fl = Floor.Create(d, new List<CurveLoop> { loop }, ft.Id, lv.Id, true, null, 0);
            fl.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(VxtRevit.ToFt(s.Z) - lv.ProjectElevation);
            VxtRevit.SetVxtId(fl, s.Id);
        }

        /// <summary>Đổi loại cột / dầm sang loại tên Việt Const (b × h), trong cùng họ của cấu kiện.</summary>
        internal static void ChangeRect(Document d, FamilyInstance fi, string name, double b, double h)
        {
            if (fi == null) return;
            var sym = VxtRevit.RectType(d, fi.Symbol.Family, name, b, h);
            if (sym == null) return;
            if (!sym.IsActive) { sym.Activate(); d.Regenerate(); }
            if (fi.Symbol.Id != sym.Id) fi.ChangeTypeId(sym.Id);
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SyncSectionsCommand : IExternalCommand
    {
        private const string Title = "Nhận tiết diện từ Việt Const";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument?.Document;
            if (doc == null || doc.IsFamilyDocument) { message = "Mở một dự án Revit trước."; return Result.Failed; }
            VxtStandard std;
            try { std = VxtStandard.Ask(Title + " – chọn file Việt Const (*.vxt2revit.json)", false); }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }
            if (std == null) return Result.Cancelled;

            const double tol = 0.05;   // m
            var items = new List<ChangeItem>();
            int matched = 0, byPos = 0;
            var notFound = new List<string>();

            var cols = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_StructuralColumns).Cast<FamilyInstance>().ToList();
            var beams = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_StructuralFraming).Cast<FamilyInstance>().ToList();
            var floors = new FilteredElementCollector(doc).OfClass(typeof(Floor)).Cast<Floor>().ToList();
            var colById = cols.Where(c => VxtRevit.GetVxtId(c).Length > 0).GroupBy(VxtRevit.GetVxtId).ToDictionary(g => g.Key, g => g.First());
            var beamById = beams.Where(c => VxtRevit.GetVxtId(c).Length > 0).GroupBy(VxtRevit.GetVxtId).ToDictionary(g => g.Key, g => g.First());
            var floorById = floors.Where(c => VxtRevit.GetVxtId(c).Length > 0).GroupBy(VxtRevit.GetVxtId).ToDictionary(g => g.Key, g => g.First());

            var colChanges = new List<(FamilyInstance E, VxtColumn V, bool Link)>();
            foreach (var v in std.Columns)
            {
                FamilyInstance fi = colById.TryGetValue(v.Id, out var x) ? x : null;
                bool link = false;
                if (fi == null)
                {
                    fi = cols.FirstOrDefault(c => c.Location is LocationPoint lp && Math.Abs(VxtRevit.ToM(lp.Point.X) - v.X) < tol && Math.Abs(VxtRevit.ToM(lp.Point.Y) - v.Y) < tol
                                                  && ColBottom(doc, c) is double zb && Math.Abs(zb - v.ZBot) < 0.15);
                    link = fi != null; if (link) byPos++;
                }
                if (fi == null) { notFound.Add(string.IsNullOrEmpty(v.Label) ? v.Id : v.Label); continue; }
                matched++;
                double b = VxtRevit.TypeSize(fi.Symbol, VxtRevit.BNames), h = VxtRevit.TypeSize(fi.Symbol, VxtRevit.HNames);
                if (fi.Symbol.Name != v.Section || Math.Abs(b - v.B) > 0.002 || Math.Abs(h - v.H) > 0.002 || link) colChanges.Add((fi, v, link));
            }
            foreach (var g in colChanges.GroupBy(c => (From: c.E.Symbol.Name, To: c.V.Section)))
            {
                var list = g.ToList();
                bool onlyLink = list.All(q => q.E.Symbol.Name == q.V.Section);
                items.Add(new ChangeItem
                {
                    Group = "Cột", Order = 3,
                    Text = onlyLink ? $"Gắn mã Việt Const cho {list.Count} cột {g.Key.To} (khớp theo vị trí)" : $"{list.Count} cột \"{g.Key.From}\" → \"{g.Key.To}\" ({list[0].V.B * 1000:0}×{list[0].V.H * 1000:0})",
                    Note = string.Join(", ", list.Select(q => string.IsNullOrEmpty(q.V.Label) ? q.V.Id : q.V.Label).Distinct().Take(12)),
                    Apply = d => { foreach (var q in list) { BuildStructureCommand.ChangeRect(d, q.E, q.V.Section, q.V.B, q.V.H); VxtRevit.SetVxtId(q.E, q.V.Id); } },
                });
            }

            var beamChanges = new List<(FamilyInstance E, VxtBeam V, bool Link)>();
            foreach (var v in std.Beams)
            {
                FamilyInstance fi = beamById.TryGetValue(v.Id, out var x) ? x : null;
                bool link = false;
                if (fi == null)
                {
                    fi = beams.FirstOrDefault(c => c.Location is LocationCurve lc && SameLine(lc.Curve, v, tol));
                    link = fi != null; if (link) byPos++;
                }
                if (fi == null) { notFound.Add(string.IsNullOrEmpty(v.Label) ? v.Id : v.Label); continue; }
                matched++;
                double b = VxtRevit.TypeSize(fi.Symbol, VxtRevit.BNames), h = VxtRevit.TypeSize(fi.Symbol, VxtRevit.HNames);
                if (fi.Symbol.Name != v.Section || Math.Abs(b - v.B) > 0.002 || Math.Abs(h - v.H) > 0.002 || link) beamChanges.Add((fi, v, link));
            }
            foreach (var g in beamChanges.GroupBy(c => (From: c.E.Symbol.Name, To: c.V.Section)))
            {
                var list = g.ToList();
                bool onlyLink = list.All(q => q.E.Symbol.Name == q.V.Section);
                items.Add(new ChangeItem
                {
                    Group = "Dầm", Order = 3,
                    Text = onlyLink ? $"Gắn mã Việt Const cho {list.Count} dầm {g.Key.To} (khớp theo vị trí)" : $"{list.Count} dầm \"{g.Key.From}\" → \"{g.Key.To}\" ({list[0].V.B * 1000:0}×{list[0].V.H * 1000:0})",
                    Note = string.Join(", ", list.Select(q => string.IsNullOrEmpty(q.V.Label) ? q.V.Id : q.V.Label).Distinct().Take(12)),
                    Apply = d => { foreach (var q in list) { BuildStructureCommand.ChangeRect(d, q.E, q.V.Section, q.V.B, q.V.H); VxtRevit.SetVxtId(q.E, q.V.Id); } },
                });
            }

            foreach (var g in std.Slabs.Where(s => floorById.ContainsKey(s.Id)).GroupBy(s => s.Section))
            {
                var list = g.Where(s => doc.GetElement(floorById[s.Id].GetTypeId())?.Name != s.Section).ToList();
                matched += g.Count();
                if (list.Count == 0) continue;
                var s0 = list[0];
                string mat = std.SlabTypes.FirstOrDefault(x => x.Name == s0.Section)?.Material ?? "Bê tông B25";
                items.Add(new ChangeItem
                {
                    Group = "Sàn", Order = 3, Text = $"{list.Count} ô sàn → \"{s0.Section}\" (dày {s0.Thickness * 1000:0} mm)",
                    Apply = d => { var ft = VxtRevit.SlabType(d, s0.Section, s0.Thickness, mat); foreach (var s in list) floorById[s.Id].ChangeTypeId(ft.Id); },
                });
            }
            int slabNoLink = std.Slabs.Count(s => !floorById.ContainsKey(s.Id));

            string intro = $"Khớp {matched} cấu kiện ({byPos} theo vị trí ±{tol * 1000:0} mm, sẽ gắn mã Việt Const). "
                           + (notFound.Count > 0 ? $"Không tìm thấy trong Revit: {notFound.Count} ({string.Join(", ", notFound.Distinct().Take(10))}{(notFound.Count > 10 ? "…" : "")}) – dùng \"Dựng kết cấu từ Việt Const\". " : "")
                           + (slabNoLink > 0 ? $"{slabNoLink} ô sàn chưa có mã (sàn chỉ khớp theo mã đã dựng). " : "")
                           + "Chọn mục rồi bấm Áp dụng.";
            return ChangeDialog.Run(doc, Title, intro, items) == null ? Result.Cancelled : Result.Succeeded;
        }

        private static double? ColBottom(Document doc, FamilyInstance c)
        {
            var lv = doc.GetElement(c.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM)?.AsElementId() ?? ElementId.InvalidElementId) as Level;
            if (lv == null) return null;
            double off = c.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM)?.AsDouble() ?? 0;
            return VxtRevit.ToM(lv.ProjectElevation + off);
        }

        private static bool SameLine(Curve c, VxtBeam v, double tol)
        {
            XYZ a = c.GetEndPoint(0), b = c.GetEndPoint(1);
            double ax = VxtRevit.ToM(a.X), ay = VxtRevit.ToM(a.Y), bx = VxtRevit.ToM(b.X), by = VxtRevit.ToM(b.Y), z = VxtRevit.ToM((a.Z + b.Z) / 2);
            if (Math.Abs(z - v.Z) > 0.15) return false;
            bool same = Math.Abs(ax - v.X1) < tol && Math.Abs(ay - v.Y1) < tol && Math.Abs(bx - v.X2) < tol && Math.Abs(by - v.Y2) < tol;
            bool rev = Math.Abs(ax - v.X2) < tol && Math.Abs(ay - v.Y2) < tol && Math.Abs(bx - v.X1) < tol && Math.Abs(by - v.Y1) < tol;
            return same || rev;
        }
    }
}

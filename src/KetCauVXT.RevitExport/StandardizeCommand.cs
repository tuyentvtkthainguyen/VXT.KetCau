using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace KetCauVXT.RevitExport
{
    // =====================================================================
    // CHUẨN HOÁ THEO VIỆT CONST
    // Lập danh sách thay đổi để mô hình Revit thống nhất với Việt Const, người dùng chọn mục rồi áp dụng (một Transaction, Ctrl+Z hoàn tác):
    //   Đơn vị mm · tên cao độ (theo cao độ Việt Const, thiếu thì tạo) · tên trục (theo toạ độ; không có file: 1, 2, 3… / A, B, C…)
    //   · vật liệu (bê tông B20–B30, thép, vữa, gạch) · loại tường theo chiều dày (lớp cấu tạo chuẩn) · tên loại cột C/ dầm D / sàn S theo kích thước
    //   · thông tin dự án + tham số khung tên · nét in theo hạng mục.
    // =====================================================================
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class StandardizeCommand : IExternalCommand
    {
        private const string Title = "Chuẩn hoá theo Việt Const";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument?.Document;
            if (doc == null || doc.IsFamilyDocument) { message = "Mở một dự án Revit (không phải họ) trước."; return Result.Failed; }
            VxtStandard std;
            try { std = VxtStandard.Ask(Title + " – chọn file chuẩn Việt Const (bỏ qua = chuẩn mặc định)", true); }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }
            if (std == null) return Result.Cancelled;
            try
            {
                var items = Plan(doc, std);
                string intro = (std.FromFile ? "Theo file " + System.IO.Path.GetFileName(std.File) : "Theo chuẩn mặc định của Việt Const")
                               + $": {items.Count} thay đổi đề xuất. Bỏ chọn mục không muốn đổi rồi bấm Áp dụng. Áp dụng xong có thể hoàn tác bằng Ctrl+Z.";
                return ChangeDialog.Run(doc, Title, intro, items) == null ? Result.Cancelled : Result.Succeeded;
            }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }
        }

        internal static List<ChangeItem> Plan(Document doc, VxtStandard std)
        {
            var items = new List<ChangeItem>();
            Units(doc, items);
            Materials(doc, std, items);
            Levels(doc, std, items);
            Grids(doc, std, items);
            WallTypes(doc, std, items);
            FrameTypes(doc, BuiltInCategory.OST_StructuralColumns, "Loại cột", VxtStandard.ColumnName, items);
            FrameTypes(doc, BuiltInCategory.OST_StructuralFraming, "Loại dầm", VxtStandard.BeamName, items);
            FloorTypes(doc, std, items);
            ProjectInfo(doc, std, items);
            LineWeights(doc, std, items);
            return items;
        }

        // ------------------------------------------------------------ đơn vị
        private static void Units(Document doc, List<ChangeItem> items)
        {
            var u = doc.GetUnits();
            var cur = u.GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();
            if (cur != null && cur.Equals(UnitTypeId.Millimeters)) return;
            items.Add(new ChangeItem
            {
                Group = "Đơn vị", Order = 0, Text = "Đơn vị chiều dài → mm (diện tích m², thể tích m³)", Note = "Việt Const nhập / xuất theo mm, m",
                Apply = d =>
                {
                    var uu = d.GetUnits();
                    uu.SetFormatOptions(SpecTypeId.Length, new FormatOptions(UnitTypeId.Millimeters));
                    uu.SetFormatOptions(SpecTypeId.Area, new FormatOptions(UnitTypeId.SquareMeters));
                    uu.SetFormatOptions(SpecTypeId.Volume, new FormatOptions(UnitTypeId.CubicMeters));
                    d.SetUnits(uu);
                },
            });
        }

        // ------------------------------------------------------------ vật liệu
        private static void Materials(Document doc, VxtStandard std, List<ChangeItem> items)
        {
            var have = new HashSet<string>(new FilteredElementCollector(doc).OfClass(typeof(Material)).Select(m => m.Name), StringComparer.OrdinalIgnoreCase);
            var miss = std.Materials.Where(m => !have.Contains(m.Name)).ToList();
            if (miss.Count == 0) return;
            items.Add(new ChangeItem
            {
                Group = "Vật liệu", Order = 0, Text = "Tạo " + miss.Count + " vật liệu: " + string.Join(", ", miss.Select(m => m.Name)),
                Note = "Tên vật liệu giống Việt Const (bê tông theo cấp, thép theo mác, vữa, gạch)",
                Apply = d => { foreach (var m in miss) VxtRevit.MaterialId(d, m.Name, m.Kind); },
            });
        }

        // ------------------------------------------------------------ cao độ
        private static void Levels(Document doc, VxtStandard std, List<ChangeItem> items)
        {
            var lv = VxtRevit.Levels(doc);
            if (std.Levels.Count > 0)
            {
                foreach (var v in std.Levels)
                {
                    var m = VxtRevit.LevelAt(lv, v.Elevation, 0.02);
                    if (m != null)
                    {
                        if (m.Name != v.Name)
                            items.Add(new ChangeItem { Group = "Cao độ", Order = 2, Text = $"Đổi tên cao độ \"{m.Name}\" → \"{v.Name}\"", Note = $"+{v.Elevation:0.000} m", RenameTarget = m, NewName = v.Name });
                    }
                    else
                    {
                        var vv = v;
                        items.Add(new ChangeItem
                        {
                            Group = "Cao độ", Order = 1, Text = $"Tạo cao độ \"{v.Name}\" +{v.Elevation:0.000} m", Note = "Việt Const có tầng này, Revit chưa có",
                            Apply = d =>
                            {
                                var nl = Level.Create(d, VxtRevit.ToFt(vv.Elevation));
                                nl.Elevation += VxtRevit.ToFt(vv.Elevation) - nl.ProjectElevation;   // khớp theo cao độ so với gốc nội bộ
                                try { nl.Name = vv.Name; } catch { nl.Name = vv.Name + " (VC)"; }
                            },
                        });
                    }
                }
            }
            else if (lv.Count >= 2)
            {
                for (int i = 0; i < lv.Count; i++)
                {
                    string n = i == lv.Count - 1 ? "Mái" : "Tầng " + (i + 1).ToString(CultureInfo.InvariantCulture);
                    if (lv[i].Name != n)
                        items.Add(new ChangeItem { Group = "Cao độ", Order = 2, Text = $"Đổi tên cao độ \"{lv[i].Name}\" → \"{n}\"", Note = $"+{VxtRevit.ToM(lv[i].ProjectElevation):0.000} m – quy ước Tầng 1…n, Mái (bỏ chọn nếu không phải mái)", RenameTarget = lv[i], NewName = n });
                }
            }
        }

        // ------------------------------------------------------------ trục
        private static void Grids(Document doc, VxtStandard std, List<ChangeItem> items)
        {
            var grids = new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>().Where(g => g.Curve is Line).ToList();
            var vert = new List<(Grid G, double C)>(); var horz = new List<(Grid G, double C)>();
            foreach (var g in grids)
            {
                var l = (Line)g.Curve;
                XYZ a = l.GetEndPoint(0), b = l.GetEndPoint(1);
                double dx = b.X - a.X, dy = b.Y - a.Y, L = Math.Sqrt(dx * dx + dy * dy);
                if (L < 1e-6) continue;
                if (Math.Abs(dx) < 0.005 * L) vert.Add((g, VxtRevit.ToM((a.X + b.X) / 2)));
                else if (Math.Abs(dy) < 0.005 * L) horz.Add((g, VxtRevit.ToM((a.Y + b.Y) / 2)));
            }
            if (std.Grids.Count > 0)
            {
                foreach (var (g, c) in vert) Match(g, c, std.Grids.Where(x => x.Dir == "X"));
                foreach (var (g, c) in horz) Match(g, c, std.Grids.Where(x => x.Dir == "Y"));
            }
            else
            {
                int i = 0;
                foreach (var (g, _) in vert.OrderBy(q => q.C)) Ren(g, (++i).ToString(CultureInfo.InvariantCulture), "trục dọc đánh số 1, 2, 3… từ trái sang phải");
                i = 0;
                foreach (var (g, _) in horz.OrderBy(q => q.C)) Ren(g, Letter(i++), "trục ngang đánh chữ A, B, C… từ dưới lên");
            }

            void Match(Grid g, double c, IEnumerable<VxtGrid> cands)
            {
                var m = cands.OrderBy(x => Math.Abs(x.Coord - c)).FirstOrDefault();
                if (m != null && Math.Abs(m.Coord - c) <= 0.02) Ren(g, m.Name, $"trùng toạ độ trục Việt Const ({m.Coord:0.000} m)");
            }
            void Ren(Grid g, string n, string note)
            {
                if (g.Name != n) items.Add(new ChangeItem { Group = "Trục", Order = 2, Text = $"Đổi tên trục \"{g.Name}\" → \"{n}\"", Note = note, RenameTarget = g, NewName = n });
            }
        }

        private static string Letter(int i) => i < 26 ? ((char)('A' + i)).ToString() : Letter(i / 26 - 1) + (char)('A' + i % 26);

        // ------------------------------------------------------------ loại tường
        private static void WallTypes(Document doc, VxtStandard std, List<ChangeItem> items)
        {
            var walls = new FilteredElementCollector(doc).OfClass(typeof(Wall)).Cast<Wall>().Where(w => w.WallType != null && w.WallType.Kind == WallKind.Basic).ToList();
            foreach (var g in walls.GroupBy(w => w.WallType.Id))
            {
                var wt = g.First().WallType;
                double t = VxtRevit.ToM(wt.Width);
                var target = std.WallTypes.OrderBy(x => Math.Abs(x.Thickness - t)).FirstOrDefault();
                if (target == null || Math.Abs(target.Thickness - t) > 0.006 || wt.Name == target.Name) continue;
                var ids = g.Select(w => w.Id).ToList();
                var tg = target;
                items.Add(new ChangeItem
                {
                    Group = "Loại tường", Order = 3, Text = $"{ids.Count} tường \"{wt.Name}\" ({t * 1000:0} mm) → \"{tg.Name}\"",
                    Note = "Lớp cấu tạo: " + string.Join(" + ", tg.Layers.Select(l => $"{l.Material} {l.Thickness * 1000:0}")) + (tg.WeightPerM2 > 0 ? $" ({tg.WeightPerM2:0.00} kN/m²)" : ""),
                    Apply = d =>
                    {
                        var nt = VxtRevit.EnsureWallType(d, tg);
                        if (nt == null) throw new InvalidOperationException("không tạo được loại tường (dự án không có loại tường cơ bản nào)");
                        foreach (var id in ids) (d.GetElement(id) as Wall)?.ChangeTypeId(nt.Id);
                    },
                });
            }
            var unmatched = walls.Select(w => w.WallType).GroupBy(x => x.Id).Select(x => x.First())
                .Where(x => !std.WallTypes.Any(s => Math.Abs(s.Thickness - VxtRevit.ToM(x.Width)) <= 0.006)).ToList();
            foreach (var u in unmatched.Take(10))
                items.Add(new ChangeItem { Group = "Loại tường", Order = 9, Checked = false, Text = $"\"{u.Name}\" ({VxtRevit.ToM(u.Width) * 1000:0} mm): không có loại tường Việt Const cùng chiều dày", Note = "Giữ nguyên – Việt Const tính tải theo lớp cấu tạo của loại này", Apply = d => { } });
        }

        // ------------------------------------------------------------ loại cột / dầm
        private static void FrameTypes(Document doc, BuiltInCategory cat, string group, Func<double, double, string> name, List<ChangeItem> items)
        {
            var insts = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).OfCategory(cat).Cast<FamilyInstance>().ToList();
            foreach (var g in insts.GroupBy(i => i.Symbol.Id))
            {
                var sym = g.First().Symbol;
                double b = VxtRevit.TypeSize(sym, VxtRevit.BNames), h = VxtRevit.TypeSize(sym, VxtRevit.HNames);
                if (b <= 0 || h <= 0) continue;   // không phải họ chữ nhật b × h (thép hình…): giữ nguyên
                string n = name(Math.Round(b * 200) / 200, Math.Round(h * 200) / 200);
                if (sym.Name == n) continue;
                items.Add(new ChangeItem
                {
                    Group = group, Order = 2, Text = $"Đổi tên loại \"{sym.Family.Name} : {sym.Name}\" → \"{n}\" ({g.Count()} cấu kiện)",
                    Note = $"b × h = {b * 1000:0} × {h * 1000:0} mm – tên giống tiết diện Việt Const", RenameTarget = sym, NewName = n,
                });
            }
        }

        // ------------------------------------------------------------ loại sàn kết cấu
        private static void FloorTypes(Document doc, VxtStandard std, List<ChangeItem> items)
        {
            var floors = new FilteredElementCollector(doc).OfClass(typeof(Floor)).Cast<Floor>()
                .Where(f => f.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL)?.AsInteger() == 1).ToList();
            foreach (var g in floors.GroupBy(f => f.GetTypeId()))
            {
                var ft = doc.GetElement(g.Key) as FloorType;
                if (ft == null) continue;
                var cs = ft.GetCompoundStructure();
                if (cs == null) continue;
                double t = VxtRevit.ToM(cs.GetWidth());
                string n = std.SlabTypes.FirstOrDefault(s => Math.Abs(s.Thickness - t) < 0.002)?.Name ?? VxtStandard.SlabName(t);
                if (ft.Name == n) continue;
                items.Add(new ChangeItem { Group = "Loại sàn", Order = 2, Text = $"Đổi tên loại sàn \"{ft.Name}\" → \"{n}\" ({g.Count()} sàn kết cấu)", Note = $"dày {t * 1000:0} mm", RenameTarget = ft, NewName = n });
            }
        }

        // ------------------------------------------------------------ thông tin dự án, khung tên
        private static void ProjectInfo(Document doc, VxtStandard std, List<ChangeItem> items)
        {
            var pi = doc.ProjectInformation;
            if (pi == null) return;
            void Set(BuiltInParameter bip, string v, string label)
            {
                if (string.IsNullOrWhiteSpace(v)) return;
                var p = pi.get_Parameter(bip);
                if (p == null || p.IsReadOnly || p.AsString() == v) return;
                items.Add(new ChangeItem { Group = "Thông tin dự án", Order = 4, Text = $"{label}: \"{p.AsString()}\" → \"{v}\"", Apply = d => d.ProjectInformation.get_Parameter(bip).Set(v) });
            }
            Set(BuiltInParameter.PROJECT_NAME, std.ProjectName, "Tên công trình");
            Set(BuiltInParameter.CLIENT_NAME, std.Owner, "Chủ đầu tư");
            Set(BuiltInParameter.PROJECT_ADDRESS, std.Location, "Địa điểm");
            Set(BuiltInParameter.PROJECT_AUTHOR, std.Designer, "Thiết kế");

            if (std.TitleFields.Count == 0) return;
            var tbs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType().ToList();
            var tbTypes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsElementType().ToList();
            foreach (var kv in std.TitleFields)
            {
                var targets = tbs.Concat(tbTypes).Concat(new Element[] { pi })
                    .Where(e => { var p = e.LookupParameter(kv.Key); return p != null && !p.IsReadOnly && p.StorageType == StorageType.String && p.AsString() != kv.Value; })
                    .Select(e => e.Id).ToList();
                if (targets.Count == 0) continue;
                string key = kv.Key, val = kv.Value;
                items.Add(new ChangeItem
                {
                    Group = "Khung tên", Order = 4, Text = $"\"{key}\" = \"{val}\" ({targets.Count} khung tên / thông tin dự án)",
                    Note = "Tham số cùng tên trong khung tên",
                    Apply = d => { foreach (var id in targets) d.GetElement(id)?.LookupParameter(key)?.Set(val); },
                });
            }
        }

        // ------------------------------------------------------------ nét in
        private static readonly Dictionary<string, BuiltInCategory> Cats = new Dictionary<string, BuiltInCategory>(StringComparer.OrdinalIgnoreCase)
        {
            ["Structural Columns"] = BuiltInCategory.OST_StructuralColumns, ["Structural Framing"] = BuiltInCategory.OST_StructuralFraming,
            ["Floors"] = BuiltInCategory.OST_Floors, ["Walls"] = BuiltInCategory.OST_Walls, ["Structural Foundations"] = BuiltInCategory.OST_StructuralFoundation,
            ["Stairs"] = BuiltInCategory.OST_Stairs, ["Doors"] = BuiltInCategory.OST_Doors, ["Windows"] = BuiltInCategory.OST_Windows,
            ["Grids"] = BuiltInCategory.OST_Grids, ["Levels"] = BuiltInCategory.OST_Levels,
        };

        private static void LineWeights(Document doc, VxtStandard std, List<ChangeItem> items)
        {
            var changes = new List<string>(); var acts = new List<Action<Document>>();
            foreach (var lw in std.LineWeights)
            {
                if (!Cats.TryGetValue(lw.Category, out var bic)) continue;
                Category c;
                try { c = Category.GetCategory(doc, bic); } catch { continue; }
                if (c == null) continue;
                int? cut = c.GetLineWeight(GraphicsStyleType.Cut), pr = c.GetLineWeight(GraphicsStyleType.Projection);
                bool cutable = cut.HasValue;
                if ((cutable && cut != lw.Cut) || pr != lw.Projection)
                {
                    changes.Add($"{c.Name}: cắt {(cutable ? cut.ToString() : "–")}→{lw.Cut}, chiếu {pr}→{lw.Projection}");
                    var l = lw;
                    acts.Add(d =>
                    {
                        var cc = Category.GetCategory(d, bic);
                        if (cutable) cc.SetLineWeight(l.Cut, GraphicsStyleType.Cut);
                        cc.SetLineWeight(l.Projection, GraphicsStyleType.Projection);
                    });
                }
            }
            if (changes.Count == 0) return;
            items.Add(new ChangeItem { Group = "Nét in", Order = 4, Text = "Nét in theo chuẩn bản vẽ Việt Const (" + changes.Count + " hạng mục)", Note = string.Join("; ", changes), Apply = d => { foreach (var a in acts) a(d); } });
        }
    }
}

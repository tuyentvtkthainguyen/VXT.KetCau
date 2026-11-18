using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace KetCauVXT.RevitExport
{
    // =====================================================================
    // DỰNG KIẾN TRÚC TỪ PHƯƠNG ÁN  (*.vxtphuongan.json → Revit)
    // Cao độ + mặt bằng tầng, trục, loại tường (trát 2 mặt), tường, sàn (chừa lỗ cầu thang), cửa đi / cửa sổ (tạo loại theo kích thước),
    // phòng có tên, cầu thang 2 vế (thử dựng; không được thì giữ lỗ sàn). Mỗi phần tử gắn mã "KT-<Id>" → chạy lại chỉ thêm phần còn thiếu.
    // Xem trước bằng hộp thoại; qua agent (đã bật "Cho phép agent") thì tự áp dụng.
    // =====================================================================
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class BuildArchitectureCommand : IExternalCommand
    {
        internal const string Title = "Dựng kiến trúc từ phương án";
        internal const string Prefix = "KT-";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements) => Run(commandData.Application, ref message);

        internal Result Run(UIApplication uiapp, ref string message)
        {
            var doc = uiapp.ActiveUIDocument?.Document;
            if (doc == null || doc.IsFamilyDocument) { message = "Mở một dự án Revit trước."; return Result.Failed; }
            ArchSpec s;
            try { s = ArchSpec.Ask(Title + " – chọn file phương án"); }
            catch (Exception ex) { message = "Không đọc được phương án: " + ex.Message; return Result.Failed; }
            if (s == null) return Result.Cancelled;

            VxtRevit.LastTagError = null;
            var pre = new List<string>();
            if (AgentMode.Rebuild.Count > 0) RemoveForRebuild(doc, AgentMode.Rebuild, pre);
            LoadFamilies(uiapp.Application, doc, s, pre);
            var ctx = new Ctx(doc, s);
            ctx.Log.AddRange(pre);
            var items = Plan(ctx);
            string rep = ChangeDialog.Run(doc, Title, "Phương án: " + System.IO.Path.GetFileName(s.File) +
                "\nCác mục dưới đây sẽ được thêm vào mô hình (phần đã dựng lần trước – có mã KT- – được giữ nguyên).", items);
            if (rep == null) return Result.Cancelled;
            if (ctx.WantStairs) BuildStairs(ctx);
            if (VxtRevit.LastTagError != null) ctx.Log.Insert(0, "Không gắn được mã KT- (chạy lại lệnh sẽ dựng trùng): " + VxtRevit.LastTagError);
            string full = (AgentMode.Report ?? "") + (ctx.Log.Count > 0 ? "\n\nGhi chú:\n• " + string.Join("\n• ", ctx.Log.Take(200)) : "");
            AgentMode.Report = full;
            if (!AgentMode.Quiet && ctx.Log.Count > 0) TaskDialog.Show(Title, full);
            return Result.Succeeded;
        }

        // ------------------------------------------------------------------ dựng lại một nhóm
        /// <summary>Xoá phần tử có mã KT- thuộc nhóm "cua" (cửa đi, cửa sổ), "cau-thang", "san", "tuong" (kéo theo cửa trên tường), "dam", "cot", "mat-tien" (lan can, khung, lam, mái kính…) để dựng lại theo phương án đã sửa.</summary>
        internal static void RemoveForRebuild(Document d, HashSet<string> groups, List<string> log)
        {
            var cats = new List<BuiltInCategory>();
            if (groups.Contains("cua")) { cats.Add(BuiltInCategory.OST_Doors); cats.Add(BuiltInCategory.OST_Windows); }
            if (groups.Contains("cau-thang")) cats.Add(BuiltInCategory.OST_Stairs);
            if (groups.Contains("san")) cats.Add(BuiltInCategory.OST_Floors);
            if (groups.Contains("tuong")) cats.Add(BuiltInCategory.OST_Walls);
            if (groups.Contains("dam")) cats.Add(BuiltInCategory.OST_StructuralFraming);
            if (groups.Contains("cot")) cats.Add(BuiltInCategory.OST_StructuralColumns);
            if (groups.Contains("mat-tien")) cats.Add(BuiltInCategory.OST_GenericModel);
            if (cats.Count == 0) return;
            var ids = new List<ElementId>();
            foreach (var bic in cats)
                foreach (var e in new FilteredElementCollector(d).OfCategory(bic).WhereElementIsNotElementType())
                    if (VxtRevit.GetVxtId(e).StartsWith(Prefix, StringComparison.Ordinal)) ids.Add(e.Id);
            if (ids.Count == 0) return;
            using (var t = new Transaction(d, "Xoá để dựng lại theo phương án"))
            {
                t.Start(); Swallow(t, log);
                int n = 0;
                foreach (var id in ids) try { if (d.GetElement(id) != null) { d.Delete(id); n++; } } catch { }
                t.Commit();
                log.Add($"Đã xoá {n} phần tử ({string.Join(", ", groups)}) để dựng lại theo phương án mới.");
            }
        }

        // ------------------------------------------------------------------ nạp họ từ thư viện / dự án mẫu
        /// <summary>
        /// Họ cửa phương án ghi (Openings[].Family) mà dự án chưa có → tìm trong FamilySources:
        /// *.rfa (theo tên họ), thư mục (tìm &lt;tên họ&gt;.rfa, kể cả thư mục con), *.rvt / *.rte (mở ngầm, chép họ sang). Gọi ngoài Transaction.
        /// </summary>
        internal static void LoadFamilies(Autodesk.Revit.ApplicationServices.Application app, Document d, ArchSpec s, List<string> log)
        {
            var want = s.Openings.Select(o => o.Family).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (want.Count == 0) return;
            var have = new HashSet<string>(new FilteredElementCollector(d).OfClass(typeof(Family)).Cast<Family>().Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
            var miss = want.Where(n => !have.Contains(n)).ToList();
            if (miss.Count == 0) return;
            if (s.FamilySources.Count == 0) { log.Add("Phương án cần họ " + string.Join(", ", miss) + " nhưng chưa khai báo FamilySources (dự án mẫu / thư mục họ)."); return; }
            foreach (var src in s.FamilySources)
            {
                if (miss.Count == 0) break;
                try
                {
                    if (Directory.Exists(src))
                    {
                        foreach (var n in miss.ToList())
                        {
                            var fp = Directory.EnumerateFiles(src, n + ".rfa", SearchOption.AllDirectories).FirstOrDefault();
                            if (fp == null) continue;
                            using (var t = new Transaction(d, "Nạp họ " + n)) { t.Start(); if (d.LoadFamily(fp, out _)) { miss.Remove(n); log.Add("Nạp họ " + n + " từ " + fp); } t.Commit(); }
                        }
                    }
                    else if (src.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase) && File.Exists(src))
                    {
                        string n = Path.GetFileNameWithoutExtension(src);
                        if (!miss.Contains(n, StringComparer.OrdinalIgnoreCase)) continue;
                        using (var t = new Transaction(d, "Nạp họ " + n)) { t.Start(); if (d.LoadFamily(src, out _)) { miss.RemoveAll(x => x.Equals(n, StringComparison.OrdinalIgnoreCase)); log.Add("Nạp họ " + n); } t.Commit(); }
                    }
                    else if ((src.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) || src.EndsWith(".rte", StringComparison.OrdinalIgnoreCase)) && File.Exists(src))
                    {
                        Document sd = null; bool opened = false;
                        foreach (Document od in app.Documents) if (string.Equals(od.PathName, src, StringComparison.OrdinalIgnoreCase)) sd = od;
                        if (sd == null)
                        {
                            var mp = ModelPathUtils.ConvertUserVisiblePathToModelPath(src);
                            sd = app.OpenDocumentFile(mp, new OpenOptions()); opened = true;
                        }
                        try
                        {
                            foreach (var fam in new FilteredElementCollector(sd).OfClass(typeof(Family)).Cast<Family>().Where(f => miss.Contains(f.Name, StringComparer.OrdinalIgnoreCase)).ToList())
                            {
                                if (!fam.IsEditable) { log.Add("Họ " + fam.Name + " trong " + Path.GetFileName(src) + " không mở được để chép."); continue; }
                                var fd = sd.EditFamily(fam);
                                try { fd.LoadFamily(d, new KeepLoadOptions()); miss.RemoveAll(x => x.Equals(fam.Name, StringComparison.OrdinalIgnoreCase)); log.Add("Chép họ " + fam.Name + " từ " + Path.GetFileName(src)); }
                                finally { fd.Close(false); }
                            }
                        }
                        finally { if (opened) sd.Close(false); }
                    }
                }
                catch (Exception ex) { log.Add("Nguồn họ " + src + ": " + ex.Message); }
            }
            if (miss.Count > 0) log.Add("Chưa tìm thấy họ: " + string.Join(", ", miss) + " – dùng họ cửa sẵn có trong dự án.");
        }

        private sealed class KeepLoadOptions : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues) { overwriteParameterValues = false; return true; }
            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues) { source = FamilySource.Family; overwriteParameterValues = false; return true; }
        }

        // ------------------------------------------------------------------ ngữ cảnh
        internal sealed class Ctx
        {
            public readonly Document Doc; public readonly ArchSpec S;
            public readonly Dictionary<string, Level> Lv = new Dictionary<string, Level>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, Element> Existing = new Dictionary<string, Element>(StringComparer.OrdinalIgnoreCase);
            public readonly List<(AsWall Spec, Wall W)> Walls = new List<(AsWall, Wall)>();
            public readonly List<string> Log = new List<string>();
            public bool WantStairs;
            public Ctx(Document d, ArchSpec s)
            {
                Doc = d; S = s;
                foreach (var e in new FilteredElementCollector(d).WhereElementIsNotElementType())
                {
                    string id;
                    try { id = VxtRevit.GetVxtId(e); } catch { continue; }
                    if (id.StartsWith(Prefix, StringComparison.Ordinal) && !Existing.ContainsKey(id)) Existing[id] = e;
                }
            }
            public bool Has(string id) => Existing.ContainsKey(Prefix + id);
            public Level UpperLevel(string level)
            {
                var me = S.Levels.FirstOrDefault(l => l.Name.Equals(level, StringComparison.OrdinalIgnoreCase));
                if (me == null) return null;
                var up = S.Levels.Where(l => l.Elevation > me.Elevation + 1e-6).OrderBy(l => l.Elevation).FirstOrDefault();
                return up != null && Lv.TryGetValue(up.Name, out var lv) ? lv : null;
            }
            public double NextElevation(string level)
            {
                var me = S.Levels.FirstOrDefault(l => l.Name.Equals(level, StringComparison.OrdinalIgnoreCase));
                if (me == null) return 0;
                var up = S.Levels.Where(l => l.Elevation > me.Elevation + 1e-6).OrderBy(l => l.Elevation).FirstOrDefault();
                return up == null ? 0 : up.Elevation - me.Elevation;
            }
        }

        private static string Mm(double m) => Math.Round(m * 1000).ToString(CultureInfo.InvariantCulture);
        private static XYZ P(double x, double y, double z = 0) => new XYZ(VxtRevit.ToFt(x), VxtRevit.ToFt(y), VxtRevit.ToFt(z));

        // ------------------------------------------------------------------ lập danh sách
        internal static List<ChangeItem> Plan(Ctx c)
        {
            var s = c.S; var doc = c.Doc;
            var items = new List<ChangeItem>();
            var docLv = VxtRevit.Levels(doc);
            var missing = new List<AsLevel>();
            var rename = new List<(Level L, string Name)>();
            foreach (var l in s.Levels)
            {
                var byName = docLv.FirstOrDefault(x => x.Name.Equals(l.Name, StringComparison.OrdinalIgnoreCase));
                var m = byName ?? VxtRevit.LevelAt(docLv, l.Elevation);
                if (m != null) { c.Lv[l.Name] = m; if (byName == null) rename.Add((m, l.Name)); } else missing.Add(l);
            }
            items.Add(new ChangeItem
            {
                Group = "Cao độ", Order = 0, Checked = true,
                Text = (missing.Count == 0 ? $"Dùng {s.Levels.Count} cao độ sẵn có" : $"Tạo {missing.Count} cao độ + mặt bằng tầng: " + string.Join(", ", missing.Select(l => l.Name + " (" + l.Elevation.ToString("+0.000;-0.000", CultureInfo.InvariantCulture) + ")")))
                       + (rename.Count > 0 ? "; đổi tên " + string.Join(", ", rename.Select(r => r.L.Name + " → " + r.Name)) : ""),
                Note = "Khớp theo tên, rồi theo cao độ (±20 mm).",
                Apply = d =>
                {
                    foreach (var (lv0, nm) in rename) try { lv0.Name = nm; } catch { c.Log.Add("Không đổi được tên cao độ " + lv0.Name + " → " + nm); }
                    var vft = new FilteredElementCollector(d).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault(v => v.ViewFamily == ViewFamily.FloorPlan);
                    foreach (var l in missing)
                    {
                        var lv = Level.Create(d, VxtRevit.ToFt(l.Elevation));
                        try { lv.Name = l.Name; } catch { c.Log.Add("Không đặt được tên cao độ " + l.Name); }
                        c.Lv[l.Name] = lv;
                        if (vft != null) try { var vp = ViewPlan.Create(d, vft.Id, lv.Id); try { vp.Name = "MB " + l.Name; } catch { } } catch (Exception ex) { c.Log.Add("Mặt bằng " + l.Name + ": " + ex.Message); }
                    }
                }
            });

            var gridNames = new HashSet<string>(new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>().Select(g => g.Name), StringComparer.OrdinalIgnoreCase);
            var newGrids = s.Grids.Where(g => !gridNames.Contains(g.Name) && Math.Abs(g.X2 - g.X1) + Math.Abs(g.Y2 - g.Y1) > 0.1).ToList();
            if (newGrids.Count > 0)
                items.Add(new ChangeItem
                {
                    Group = "Trục", Order = 1, Text = $"Tạo {newGrids.Count} trục: " + string.Join(", ", newGrids.Select(g => g.Name)),
                    Apply = d => { foreach (var g in newGrids) { var gr = Grid.Create(d, Line.CreateBound(P(g.X1, g.Y1), P(g.X2, g.Y2))); try { gr.Name = g.Name; } catch { } } }
                });

            var walls = s.Walls.Where(w => !c.Has(w.Id) && Math.Abs(w.X2 - w.X1) + Math.Abs(w.Y2 - w.Y1) > 0.05).ToList();
            var types = walls.GroupBy(w => string.IsNullOrEmpty(w.TypeName) ? VxtStandard.WallName(w.Thickness > 0 ? w.Thickness : 0.11) : w.TypeName).ToList();
            if (walls.Count > 0)
                items.Add(new ChangeItem
                {
                    Group = "Tường", Order = 3, Text = $"Dựng {walls.Count} tường – loại: " + string.Join(", ", types.Select(t => t.Key + " ×" + t.Count())),
                    Note = "Loại tường tạo theo lớp vật liệu của phương án (trát + lõi + trát). Chiều cao 0 → lên tới cao độ trên.",
                    Apply = d =>
                    {
                        var wtByName = new Dictionary<string, WallType>(StringComparer.OrdinalIgnoreCase);
                        foreach (var g in types)
                        {
                            var w0 = g.First();
                            var layers = w0.Layers.Count > 0 ? w0.Layers : new List<VxtLayer> { new VxtLayer { Function = "Structure", Material = "Gạch xây", Thickness = 0.11 } };
                            var wt = VxtRevit.EnsureWallType(d, new VxtWallType { Name = g.Key, Thickness = layers.Sum(l => l.Thickness), Layers = layers });
                            if (wt != null) wtByName[g.Key] = wt; else c.Log.Add("Dự án chưa có loại tường cơ bản để nhân bản (" + g.Key + ").");
                        }
                        foreach (var g in types)
                            foreach (var w in g)
                            {
                                if (!wtByName.TryGetValue(g.Key, out var wt)) continue;
                                if (!c.Lv.TryGetValue(w.Level, out var lv)) { c.Log.Add($"Tường {w.Id}: không có cao độ \"{w.Level}\"."); continue; }
                                double h = w.Height > 0.05 ? w.Height : c.NextElevation(w.Level);
                                if (h <= 0.05) h = 3.3;
                                try
                                {
                                    var wall = Wall.Create(d, Line.CreateBound(P(w.X1, w.Y1, 0), P(w.X2, w.Y2, 0)), wt.Id, lv.Id, VxtRevit.ToFt(h), VxtRevit.ToFt(w.BaseOffset), false, false);
                                    VxtRevit.SetVxtId(wall, Prefix + w.Id);
                                    // tường trong tầng: đỉnh gắn vào cao độ trên, offset âm = cao dầm / bề dày sàn → sửa cao độ tầng thì tường chạy theo
                                    double storey = c.NextElevation(w.Level);
                                    var upLv = c.UpperLevel(w.Level);
                                    if (upLv != null && storey > 0 && h <= storey + 0.005 && h >= storey - 1.0)
                                    {
                                        try
                                        {
                                            wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE)?.Set(upLv.Id);
                                            wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET)?.Set(VxtRevit.ToFt(w.BaseOffset + h - storey));
                                        }
                                        catch (Exception ex2) { c.Log.Add($"Tường {w.Id}: không gắn được đỉnh vào cao độ trên ({ex2.Message})."); }
                                    }
                                    c.Walls.Add((w, wall));
                                }
                                catch (Exception ex) { c.Log.Add($"Tường {w.Id}: {ex.Message}"); }
                            }
                    }
                });

            var floors = s.Floors.Where(f => !c.Has(f.Id) && f.Outline.Count >= 3).ToList();
            if (floors.Count > 0)
                items.Add(new ChangeItem
                {
                    Group = "Sàn", Order = 3, Text = $"Dựng {floors.Count} sàn (" + string.Join(", ", floors.Select(f => f.Level).Distinct()) + ")" + (s.Stairs.Count > 0 ? " – chừa lỗ cầu thang" : ""),
                    Apply = d =>
                    {
                        foreach (var f in floors)
                        {
                            if (!c.Lv.TryGetValue(f.Level, out var lv)) { c.Log.Add($"Sàn {f.Id}: không có cao độ \"{f.Level}\"."); continue; }
                            try
                            {
                                var ft = VxtRevit.SlabType(d, "Sàn BTCT " + Mm(f.Thickness > 0 ? f.Thickness : 0.1), f.Thickness > 0 ? f.Thickness : 0.1, "Bê tông B20");
                                var loops = new List<CurveLoop> { Loop(f.Outline) };
                                foreach (var st in s.Stairs.Where(t => t.TopLevel.Equals(f.Level, StringComparison.OrdinalIgnoreCase) && t.Outline.Count >= 3 && Inside(f.Outline, t.Outline)))
                                    loops.Add(Loop(st.Outline));
                                var fl = Floor.Create(d, loops, ft.Id, lv.Id);
                                VxtRevit.SetVxtId(fl, Prefix + f.Id);
                            }
                            catch (Exception ex) { c.Log.Add($"Sàn {f.Id}: {ex.Message}"); }
                        }
                    }
                });

            var ops = s.Openings.Where(o => !c.Has(o.Id) && o.Width > 0.1 && o.Height > 0.1).ToList();
            if (ops.Count > 0)
                items.Add(new ChangeItem
                {
                    Group = "Cửa", Order = 4,
                    Text = $"Đặt {ops.Count(o => IsDoor(o))} cửa đi, {ops.Count(o => !IsDoor(o))} cửa sổ – loại theo kích thước (VD \"Cửa đi 900x2200\")",
                    Note = "Gắn vào tường gần nhất cùng tầng. Cần có sẵn ít nhất một họ cửa đi / cửa sổ trong dự án.",
                    Apply = d => { d.Regenerate(); PlaceOpenings(c, ops); }
                });

            var rooms = s.Rooms.Where(r => !c.Has(r.Id) && r.Outline.Count >= 3).ToList();
            if (rooms.Count > 0)
                items.Add(new ChangeItem
                {
                    Group = "Phòng", Order = 5, Text = $"Đặt {rooms.Count} phòng có tên: " + string.Join(", ", rooms.Select(r => r.Name).Distinct().Take(12)) + (rooms.Select(r => r.Name).Distinct().Count() > 12 ? "…" : ""),
                    Apply = d => { d.Regenerate(); PlaceRooms(c, rooms); }
                });

            var cols = s.Columns.Where(x => !c.Has(x.Id)).ToList();
            if (cols.Count > 0)
                items.Add(new ChangeItem
                {
                    Group = "Cột", Order = 2, Text = $"Dựng {cols.Count} cột BTCT: " + string.Join(", ", cols.GroupBy(x => $"{x.B * 1000:0}x{x.H * 1000:0}").Select(g => g.Key + " ×" + g.Count())),
                    Note = "Cột từng tầng, chân tại cao độ tầng, đỉnh tại cao độ tầng trên (Việt Const --phoi-hop đặt cột tại giao trục / nút tường 220).",
                    Apply = d => { int n = FacadeBuilder.BuildColumns(d, c, cols); if (n < cols.Count) c.Log.Add($"Dựng được {n}/{cols.Count} cột."); }
                });

            var beams = s.Beams.Where(b => !c.Has(b.Id) && Math.Abs(b.X2 - b.X1) + Math.Abs(b.Y2 - b.Y1) > 0.1).ToList();
            if (beams.Count > 0)
                items.Add(new ChangeItem
                {
                    Group = "Dầm", Order = 3, Text = $"Dựng {beams.Count} dầm BTCT dưới sàn: " + string.Join(", ", beams.GroupBy(b => $"{b.B * 1000:0}x{b.H * 1000:0}").Select(g => g.Key + " ×" + g.Count())),
                    Note = "Mặt trên dầm = cao độ tầng; tường trong phương án đã hạ xuống đáy dầm (lệnh Việt Const --phoi-hop).",
                    Apply = d => { int n = FacadeBuilder.BuildBeams(d, c, beams); if (n < beams.Count) c.Log.Add($"Dựng được {n}/{beams.Count} dầm."); }
                });

            var fes = s.FacadeElements.Where(e => !c.Has(e.Id)).ToList();
            if (fes.Count > 0 || s.FacadeMaterials.Count > 0)
                items.Add(new ChangeItem
                {
                    Group = "Mặt tiền", Order = 6,
                    Text = (fes.Count > 0 ? $"Dựng {fes.Count} chi tiết mặt tiền ({string.Join(", ", fes.GroupBy(e => e.Type).Select(g => g.Key + " ×" + g.Count()))}) + " : "") + "sơn mặt ngoài tường theo màu phương án",
                    Note = "Lan can / khung / lam / mái kính dựng bằng DirectShape (Generic Model) có vật liệu màu; sơn mặt tường không quay vào phòng.",
                    Apply = d =>
                    {
                        int n = FacadeBuilder.BuildElements(d, c, fes);
                        d.Regenerate();
                        int p = FacadeBuilder.PaintExterior(d, c);
                        c.Log.Add($"Mặt tiền: {n} chi tiết, sơn {p} mặt tường ngoài.");
                    }
                });

            var stairs = s.Stairs.Where(t => !c.Has(t.Id) && t.Outline.Count >= 3).ToList();
            if (stairs.Count > 0)
                items.Add(new ChangeItem
                {
                    Group = "Cầu thang", Order = 6, Text = $"Thử dựng {stairs.Count} cầu thang 2 vế trong ô thang",
                    Note = "Dựng sau khi áp dụng các mục khác. Không dựng được thì giữ lỗ sàn và ghi chú để vẽ tay.",
                    Apply = d => c.WantStairs = true
                });

            items.Add(new ChangeItem
            {
                Group = "Kết nối", Order = 8, Text = "Kết nối hình học (join): cột cắt sàn / dầm / tường, sàn cắt dầm / tường, dầm cắt tường",
                Note = "Chỉ phần tử có mã KT-; khối lượng bê tông, gạch không tính trùng; mặt cắt sạch.",
                Apply = d => { d.Regenerate(); c.Log.Add(FacadeBuilder.JoinAll(d)); }
            });

            if (!string.IsNullOrEmpty(s.ProjectName))
                items.Add(new ChangeItem
                {
                    Group = "Thông tin", Order = 7, Checked = string.IsNullOrWhiteSpace(doc.ProjectInformation?.Name) || doc.ProjectInformation.Name == "Project Name",
                    Text = "Tên dự án: " + s.ProjectName,
                    Apply = d => { if (d.ProjectInformation != null) d.ProjectInformation.Name = s.ProjectName; }
                });
            return items;
        }

        private static bool IsDoor(AsOpening o) => !o.Category.StartsWith("Win", StringComparison.OrdinalIgnoreCase) && o.Category.IndexOf("sổ", StringComparison.OrdinalIgnoreCase) < 0;

        internal static CurveLoop Loop(List<double[]> pts)
        {
            var loop = new CurveLoop();
            var ps = pts.ToList();
            if (ps.Count > 2 && Math.Abs(ps[0][0] - ps[ps.Count - 1][0]) < 1e-6 && Math.Abs(ps[0][1] - ps[ps.Count - 1][1]) < 1e-6) ps.RemoveAt(ps.Count - 1);
            for (int i = 0; i < ps.Count; i++)
            {
                var a = ps[i]; var b = ps[(i + 1) % ps.Count];
                if (Math.Abs(a[0] - b[0]) + Math.Abs(a[1] - b[1]) < 1e-4) continue;
                loop.Append(Line.CreateBound(P(a[0], a[1]), P(b[0], b[1])));
            }
            return loop;
        }

        /// <summary>Lỗ nằm hẳn trong sàn: mọi đỉnh ở trong và cách mép sàn ≥ 2 cm (chạm mép / khấc sẵn ở mép thì không thêm lỗ).</summary>
        private static bool Inside(List<double[]> outer, List<double[]> inner) =>
            inner.All(p => PointIn(outer, p[0], p[1]) && Enumerable.Range(0, outer.Count).All(i =>
                DistSeg(p[0], p[1], outer[i][0], outer[i][1], outer[(i + 1) % outer.Count][0], outer[(i + 1) % outer.Count][1]) >= 0.02));

        internal static bool PointIn(List<double[]> poly, double x, double y, double tol = 0)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                double xi = poly[i][0], yi = poly[i][1], xj = poly[j][0], yj = poly[j][1];
                if (DistSeg(x, y, xi, yi, xj, yj) <= tol) return true;
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        private static double DistSeg(double x, double y, double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1, dy = y2 - y1, L2 = dx * dx + dy * dy;
            double t = L2 < 1e-12 ? 0 : Math.Max(0, Math.Min(1, ((x - x1) * dx + (y - y1) * dy) / L2));
            double px = x1 + t * dx - x, py = y1 + t * dy - y;
            return Math.Sqrt(px * px + py * py);
        }

        // ------------------------------------------------------------------ cửa
        private static void PlaceOpenings(Ctx c, List<AsOpening> ops)
        {
            var d = c.Doc;
            // tường để gắn: tường vừa dựng + tường cũ có mã KT-
            var hosts = new List<(string Level, double X1, double Y1, double X2, double Y2, double T, Wall W)>();
            foreach (var (sp, w) in c.Walls) hosts.Add((sp.Level, sp.X1, sp.Y1, sp.X2, sp.Y2, Math.Max(sp.Thickness, 0.1), w));
            foreach (var kv in c.Existing)
                if (kv.Value is Wall w && w.Location is LocationCurve lc && lc.Curve is Line ln)
                {
                    var lv = d.GetElement(w.LevelId) as Level;
                    var a = ln.GetEndPoint(0); var b = ln.GetEndPoint(1);
                    hosts.Add((lv?.Name ?? "", VxtRevit.ToM(a.X), VxtRevit.ToM(a.Y), VxtRevit.ToM(b.X), VxtRevit.ToM(b.Y), VxtRevit.ToM(w.Width), w));
                }
            var doorSyms = Symbols(d, BuiltInCategory.OST_Doors);
            var winSyms = Symbols(d, BuiltInCategory.OST_Windows);
            if (doorSyms.Count == 0 && ops.Any(IsDoor)) c.Log.Add("Dự án chưa có họ cửa đi nào – nạp một họ cửa đi (Insert → Load Family) rồi chạy lại lệnh, chỉ phần còn thiếu được thêm.");
            if (winSyms.Count == 0 && ops.Any(o => !IsDoor(o))) c.Log.Add("Dự án chưa có họ cửa sổ nào – nạp một họ cửa sổ rồi chạy lại lệnh.");

            foreach (var o in ops)
            {
                bool door = IsDoor(o);
                var syms = door ? doorSyms : winSyms;
                if (syms.Count == 0) continue;
                if (!c.Lv.TryGetValue(o.Level, out var lv)) { c.Log.Add($"Cửa {o.Id}: không có cao độ \"{o.Level}\"."); continue; }
                var host = hosts.Where(h => h.Level.Equals(o.Level, StringComparison.OrdinalIgnoreCase))
                                .Select(h => (H: h, D: DistSeg(o.X, o.Y, h.X1, h.Y1, h.X2, h.Y2)))
                                .Where(t => t.D <= t.H.T / 2 + 0.2).OrderBy(t => t.D).Select(t => t.H.W).FirstOrDefault();
                if (host == null) { c.Log.Add($"Cửa {o.Id} ({o.X:0.##}; {o.Y:0.##}) ở {o.Level}: không có tường nào đi qua."); continue; }
                try
                {
                    var sym = SizedSymbol(d, syms, door, o.Width, o.Height, o.TypeName, o.Family);
                    if (!sym.IsActive) { sym.Activate(); d.Regenerate(); }
                    var lc = (LocationCurve)host.Location;
                    var pt = lc.Curve.Project(P(o.X, o.Y, VxtRevit.ToM(lc.Curve.GetEndPoint(0).Z))).XYZPoint;
                    var inst = d.Create.NewFamilyInstance(new XYZ(pt.X, pt.Y, lv.ProjectElevation), sym, host, lv, StructuralType.NonStructural);
                    if (!door || o.Sill > 0.01)
                    {
                        var sp = inst.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
                        if (sp != null && !sp.IsReadOnly) sp.Set(VxtRevit.ToFt(o.Sill > 0.01 ? o.Sill : 0.9));
                    }
                    VxtRevit.SetVxtId(inst, Prefix + o.Id);
                }
                catch (Exception ex) { c.Log.Add($"Cửa {o.Id}: {ex.Message}"); }
            }
        }

        private static List<FamilySymbol> Symbols(Document d, BuiltInCategory cat) =>
            new FilteredElementCollector(d).OfClass(typeof(FamilySymbol)).OfCategory(cat).Cast<FamilySymbol>()
                .Where(fs => fs.Family != null && !fs.Family.IsInPlace).ToList();

        private static FamilySymbol SizedSymbol(Document d, List<FamilySymbol> syms, bool door, double w, double h, string hint, string family = "")
        {
            // họ phương án chỉ định (VD cửa nhôm kính của dự án mẫu): đúng loại + đúng kích thước → dùng luôn; không thì nhân bản trong họ đó
            if (!string.IsNullOrWhiteSpace(family))
            {
                var fam = syms.Where(x => x.FamilyName.Equals(family, StringComparison.OrdinalIgnoreCase)).ToList();
                if (fam.Count > 0)
                {
                    bool Fit(FamilySymbol x) => Math.Abs(SymW(x, door) - w) < 0.01 && Math.Abs(SymH(x, door) - h) < 0.01;
                    var exact = fam.FirstOrDefault(x => x.Name.Equals(hint ?? "", StringComparison.OrdinalIgnoreCase) && Fit(x)) ?? fam.FirstOrDefault(Fit);
                    if (exact != null) return exact;
                    string fn = (hint ?? "").Length > 0 ? hint + " " + Mm(w) + "x" + Mm(h) : Mm(w) + "x" + Mm(h);
                    var again = fam.FirstOrDefault(x => x.Name.Equals(fn, StringComparison.OrdinalIgnoreCase));
                    if (again != null) return again;
                    var baseSym = fam.FirstOrDefault(x => x.Name.Equals(hint ?? "", StringComparison.OrdinalIgnoreCase)) ?? fam[0];
                    var dup = (FamilySymbol)baseSym.Duplicate(fn);
                    SetDim(dup, w, door ? BuiltInParameter.DOOR_WIDTH : BuiltInParameter.WINDOW_WIDTH, "Width", "Rộng", "Chiều rộng", "Bề rộng");
                    SetDim(dup, h, door ? BuiltInParameter.DOOR_HEIGHT : BuiltInParameter.WINDOW_HEIGHT, "Height", "Cao", "Chiều cao");
                    syms.Add(dup);
                    return dup;
                }
            }
            string name = (door ? "Cửa đi " : "Cửa sổ ") + Mm(w) + "x" + Mm(h);
            var found = syms.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;
            // gốc: loại có tên gợi ý (VD "Cửa đi"), loại có kích thước chỉnh được, rồi loại đầu tiên
            var src = syms.FirstOrDefault(s => !string.IsNullOrEmpty(hint) && (s.Name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0 || s.FamilyName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0) && HasSize(s, door))
                      ?? syms.FirstOrDefault(s => HasSize(s, door)) ?? syms[0];
            var nt = (FamilySymbol)src.Duplicate(name);
            SetDim(nt, w, door ? BuiltInParameter.DOOR_WIDTH : BuiltInParameter.WINDOW_WIDTH, "Width", "Rộng", "Chiều rộng", "Bề rộng");
            SetDim(nt, h, door ? BuiltInParameter.DOOR_HEIGHT : BuiltInParameter.WINDOW_HEIGHT, "Height", "Cao", "Chiều cao");
            syms.Add(nt);
            return nt;
        }

        private static double SymW(FamilySymbol s, bool door)
        {
            var p = s.get_Parameter(door ? BuiltInParameter.DOOR_WIDTH : BuiltInParameter.WINDOW_WIDTH) ?? s.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM);
            return p != null && p.StorageType == StorageType.Double ? VxtRevit.ToM(p.AsDouble()) : VxtRevit.TypeSize(s, "Width", "Rộng", "Chiều rộng");
        }

        private static double SymH(FamilySymbol s, bool door)
        {
            var p = s.get_Parameter(door ? BuiltInParameter.DOOR_HEIGHT : BuiltInParameter.WINDOW_HEIGHT) ?? s.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM);
            return p != null && p.StorageType == StorageType.Double ? VxtRevit.ToM(p.AsDouble()) : VxtRevit.TypeSize(s, "Height", "Cao", "Chiều cao");
        }

        private static bool HasSize(FamilySymbol s, bool door)
        {
            var p = s.get_Parameter(door ? BuiltInParameter.DOOR_WIDTH : BuiltInParameter.WINDOW_WIDTH) ?? s.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM);
            return p != null && !p.IsReadOnly;
        }

        private static void SetDim(Element e, double m, BuiltInParameter bip, params string[] names)
        {
            var p = e.get_Parameter(bip);
            if (p == null || p.IsReadOnly) p = e.get_Parameter(bip == BuiltInParameter.DOOR_WIDTH || bip == BuiltInParameter.WINDOW_WIDTH ? BuiltInParameter.FAMILY_WIDTH_PARAM : BuiltInParameter.FAMILY_HEIGHT_PARAM);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double) { p.Set(VxtRevit.ToFt(m)); return; }
            VxtRevit.SetSize(e, m, names);
        }

        // ------------------------------------------------------------------ phòng
        private static void PlaceRooms(Ctx c, List<AsRoom> rooms)
        {
            var d = c.Doc;
            SeparateRooms(c, rooms);
            foreach (var r in rooms)
            {
                if (!c.Lv.TryGetValue(r.Level, out var lv)) { c.Log.Add($"Phòng {r.Name}: không có cao độ \"{r.Level}\"."); continue; }
                var pt = InnerPoint(r.Outline);
                try
                {
                    // đã có phòng ở chỗ này (người dùng tự đặt) → chỉ đặt tên nếu đang trống
                    var probe = new XYZ(VxtRevit.ToFt(pt[0]), VxtRevit.ToFt(pt[1]), lv.ProjectElevation + VxtRevit.ToFt(1.0));
                    var exist = d.GetRoomAtPoint(probe);
                    // vùng này đã thuộc phòng khác của phương án (không có tường ngăn, VD sảnh mở ra phòng khách) → kẻ đường phân chia phòng theo đường bao
                    if (exist != null && VxtRevit.GetVxtId(exist).StartsWith(Prefix, StringComparison.Ordinal) && VxtRevit.GetVxtId(exist) != Prefix + r.Id)
                    {
                        var plan = new FilteredElementCollector(d).OfClass(typeof(ViewPlan)).Cast<ViewPlan>()
                            .FirstOrDefault(v => !v.IsTemplate && v.GenLevel != null && v.GenLevel.Id == lv.Id && v.ViewType == ViewType.FloorPlan);
                        if (plan != null)
                        {
                            var ca = new CurveArray();
                            foreach (var cv in Loop(r.Outline)) ca.Append(Line.CreateBound(new XYZ(cv.GetEndPoint(0).X, cv.GetEndPoint(0).Y, lv.ProjectElevation), new XYZ(cv.GetEndPoint(1).X, cv.GetEndPoint(1).Y, lv.ProjectElevation)));
                            var sp = SketchPlane.Create(d, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, lv.ProjectElevation)));
                            d.Create.NewRoomBoundaryLines(sp, ca, plan);
                            d.Regenerate();
                            exist = d.GetRoomAtPoint(probe);
                            if (exist != null && VxtRevit.GetVxtId(exist).StartsWith(Prefix, StringComparison.Ordinal)) { c.Log.Add($"Phòng {r.Name} ({r.Level}): trùng vùng với phòng khác, đã kẻ đường phân chia nhưng chưa tách được."); continue; }
                            c.Log.Add($"Phòng {r.Name} ({r.Level}): kẻ đường phân chia phòng theo đường bao (không có tường ngăn).");
                        }
                    }
                    if (exist != null)
                    {
                        if (string.IsNullOrWhiteSpace(exist.Name) || exist.Name.StartsWith("Room", StringComparison.OrdinalIgnoreCase) || exist.Name.StartsWith("Phòng ", StringComparison.OrdinalIgnoreCase))
                            exist.Name = r.Name;
                        VxtRevit.SetVxtId(exist, Prefix + r.Id);
                        continue;
                    }
                    var room = d.Create.NewRoom(lv, new UV(VxtRevit.ToFt(pt[0]), VxtRevit.ToFt(pt[1])));
                    room.Name = r.Name;
                    if (!string.IsNullOrEmpty(r.Number)) try { room.Number = r.Number; } catch { }
                    VxtRevit.SetVxtId(room, Prefix + r.Id);
                }
                catch (Exception ex) { c.Log.Add($"Phòng {r.Name} ({r.Level}): {ex.Message}"); }
            }
        }

        /// <summary>
        /// Trước khi đặt phòng: cạnh đường bao phòng không có tường đi dọc (sảnh mở ra phòng khách, ô thang mở ra sinh hoạt chung…)
        /// → kẻ đường phân chia phòng, để mỗi phòng của phương án là một vùng kín riêng.
        /// </summary>
        private static void SeparateRooms(Ctx c, List<AsRoom> rooms)
        {
            var d = c.Doc;
            foreach (var g in rooms.GroupBy(r => r.Level))
            {
                if (!c.Lv.TryGetValue(g.Key, out var lv)) continue;
                var walls = c.S.Walls.Where(w => w.Level.Equals(g.Key, StringComparison.OrdinalIgnoreCase)).ToList();
                var plan = new FilteredElementCollector(d).OfClass(typeof(ViewPlan)).Cast<ViewPlan>()
                    .FirstOrDefault(v => !v.IsTemplate && v.GenLevel != null && v.GenLevel.Id == lv.Id && v.ViewType == ViewType.FloorPlan);
                if (plan == null) { c.Log.Add($"{g.Key}: không có mặt bằng tầng để kẻ đường phân chia phòng."); continue; }
                var segs = new List<(double X1, double Y1, double X2, double Y2)>();
                foreach (var r in g)
                {
                    var p = r.Outline;
                    for (int i = 0; i < p.Count; i++)
                    {
                        double ax = p[i][0], ay = p[i][1], bx = p[(i + 1) % p.Count][0], by = p[(i + 1) % p.Count][1];
                        double len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                        if (len < 0.3) continue;
                        double mx = (ax + bx) / 2, my = (ay + by) / 2;
                        // có tường dọc cạnh này (trung điểm cách tim tường ≤ nửa bề dày + 8 cm) → không cần
                        if (walls.Any(w => DistSeg(mx, my, w.X1, w.Y1, w.X2, w.Y2) <= Math.Max(w.Thickness, 0.1) / 2 + 0.08)) continue;
                        // trùng cạnh đã kẻ (hai phòng chung cạnh)
                        if (segs.Any(q => DistSeg(mx, my, q.X1, q.Y1, q.X2, q.Y2) < 0.02)) continue;
                        segs.Add((ax, ay, bx, by));
                    }
                }
                if (segs.Count == 0) continue;
                var ca = new CurveArray();
                foreach (var q in segs) ca.Append(Line.CreateBound(new XYZ(VxtRevit.ToFt(q.X1), VxtRevit.ToFt(q.Y1), lv.ProjectElevation), new XYZ(VxtRevit.ToFt(q.X2), VxtRevit.ToFt(q.Y2), lv.ProjectElevation)));
                try
                {
                    var sp = SketchPlane.Create(d, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, lv.ProjectElevation)));
                    d.Create.NewRoomBoundaryLines(sp, ca, plan);
                    c.Log.Add($"{g.Key}: kẻ {segs.Count} đường phân chia phòng ở chỗ không có tường ngăn.");
                }
                catch (Exception ex) { c.Log.Add($"{g.Key}: không kẻ được đường phân chia phòng ({ex.Message})."); }
            }
            d.Regenerate();
        }

        /// <summary>Điểm bên trong đa giác: trọng tâm nếu nằm trong, không thì thử lưới điểm.</summary>
        internal static double[] InnerPoint(List<double[]> poly)
        {
            double a = 0, cx = 0, cy = 0;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                double f = poly[j][0] * poly[i][1] - poly[i][0] * poly[j][1];
                a += f; cx += (poly[j][0] + poly[i][0]) * f; cy += (poly[j][1] + poly[i][1]) * f;
            }
            if (Math.Abs(a) > 1e-9) { cx /= 3 * a; cy /= 3 * a; if (PointIn(poly, cx, cy)) return new[] { cx, cy }; }
            double x0 = poly.Min(p => p[0]), x1 = poly.Max(p => p[0]), y0 = poly.Min(p => p[1]), y1 = poly.Max(p => p[1]);
            for (int i = 1; i < 10; i++) for (int k = 1; k < 10; k++)
                {
                    double x = x0 + (x1 - x0) * i / 10, y = y0 + (y1 - y0) * k / 10;
                    if (PointIn(poly, x, y)) return new[] { x, y };
                }
            return new[] { poly.Average(p => p[0]), poly.Average(p => p[1]) };
        }

        // ------------------------------------------------------------------ cầu thang (ngoài Transaction)
        private static void BuildStairs(Ctx c)
        {
            var d = c.Doc;
            foreach (var st in c.S.Stairs.Where(t => !c.Has(t.Id) && t.Outline.Count >= 3))
            {
                if (!c.Lv.TryGetValue(st.BaseLevel, out var b) || !c.Lv.TryGetValue(st.TopLevel, out var t)) { c.Log.Add($"Cầu thang {st.Id}: thiếu cao độ {st.BaseLevel}/{st.TopLevel}."); continue; }
                double x0 = st.Outline.Min(p => p[0]), x1 = st.Outline.Max(p => p[0]), y0 = st.Outline.Min(p => p[1]), y1 = st.Outline.Max(p => p[1]);
                double H = VxtRevit.ToM(t.ProjectElevation - b.ProjectElevation);
                if (H < 1.5) continue;
                if (st.Runs.Count > 0) { BuildRunsStair(c, st, b, t); continue; }
                try
                {
                    using (var scope = new StairsEditScope(d, "Cầu thang " + st.Id))
                    {
                        var sid = scope.Start(b.Id, t.Id);
                        using (var tr = new Transaction(d, "Cầu thang " + st.Id))
                        {
                            tr.Start();
                            Swallow(tr, c.Log);
                            var stairs = (Stairs)d.GetElement(sid);
                            int n = Math.Max(6, (int)Math.Round(H / 0.165));
                            if (n % 2 == 1) n++;
                            stairs.DesiredRisersNumber = n;
                            double tread = VxtRevit.ToM(stairs.ActualTreadDepth);
                            if (tread < 0.2 || tread > 0.35) tread = 0.26;
                            // hướng dài của ô thang là hướng vế; 2 vế song song, chiếu nghỉ ở đầu xa
                            bool alongY = (y1 - y0) >= (x1 - x0);
                            double L = alongY ? y1 - y0 : x1 - x0, W = alongY ? x1 - x0 : y1 - y0;
                            double w = Math.Min(st.Width > 0.6 ? st.Width : 0.9, (W - 0.1) / 2);
                            double run = (n / 2 - 1) * tread;
                            double land = Math.Max(w, L - run);
                            if (run + w > L + 0.3) c.Log.Add($"Cầu thang {st.Id}: ô thang ngắn ({L:0.00} m) so với {n} bậc – kiểm tra lại.");
                            // đường vế đặt ở cao độ chân thang (toạ độ mô hình); cao độ vế / chiếu nghỉ của Revit tính từ chân thang
                            double zb0 = b.ProjectElevation;
                            Func<double, double, XYZ> at = (u, v) => { var q = alongY ? P(x0 + v, y0 + u) : P(x0 + u, y0 + v); return new XYZ(q.X, q.Y, zb0); };
                            if (Straight(st, W))
                            {
                                // 1 vế thẳng dọc ô thang, chạy hết số bậc
                                double runS = (n - 1) * tread;
                                if (runS > L + 0.05) c.Log.Add($"Cầu thang {st.Id} (1 vế): cần {runS:0.00} m, ô thang dài {L:0.00} m – kiểm tra lại.");
                                StairsRun.CreateStraightRun(d, sid, Line.CreateBound(at(0, W / 2), at(Math.Max(runS, 1.0), W / 2)), StairsRunJustification.Center);
                                tr.Commit();
                                goto committed;
                            }
                            var r1 = StairsRun.CreateStraightRun(d, sid, Line.CreateBound(at(0, w / 2), at(run, w / 2)), StairsRunJustification.Center);
                            double zl = r1.TopElevation;
                            var p0 = at(run, W - w / 2); var p1 = at(0, W - w / 2);
                            StairsRun.CreateStraightRun(d, sid, Line.CreateBound(new XYZ(p0.X, p0.Y, zb0 + zl), new XYZ(p1.X, p1.Y, zb0 + zl)), StairsRunJustification.Center);
                            var lp = new List<XYZ> { at(run, 0), at(L, 0), at(L, W), at(run, W) }.Select(q => new XYZ(q.X, q.Y, zb0 + zl)).ToList();
                            var ll = new CurveLoop();
                            for (int i = 0; i < 4; i++) ll.Append(Line.CreateBound(lp[i], lp[(i + 1) % 4]));
                            StairsLanding.CreateSketchedLanding(d, sid, ll, zl);
                            tr.Commit();
                        }
                    committed:
                        scope.Commit(new WarningSwallower(c.Log));
                        var el = d.GetElement(sid);
                        if (el != null)
                            using (var tr = new Transaction(d, "Gắn mã cầu thang")) { tr.Start(); VxtRevit.SetVxtId(el, Prefix + st.Id); tr.Commit(); }
                    }
                }
                catch (Exception ex) { c.Log.Add($"Cầu thang {st.Id}: chưa dựng tự động được ({ex.Message}) – lỗ sàn đã chừa, vẽ cầu thang bằng tay."); }
            }
        }

        /// <summary>
        /// Cầu thang theo các vế phương án ghi sẵn (đường tim vế theo hướng đi lên, số bậc đứng mỗi vế): mặt bậc = dài vế / (bậc đứng − 1),
        /// chiếu nghỉ tự động giữa hai vế liên tiếp (không được thì dựng theo đa giác Landings).
        /// </summary>
        private static void BuildRunsStair(Ctx c, AsStair st, Level b, Level t)
        {
            var d = c.Doc;
            int total = st.Runs.Sum(r => r.Risers);
            var r0 = st.Runs[0];
            double tread = Math.Sqrt((r0.X2 - r0.X1) * (r0.X2 - r0.X1) + (r0.Y2 - r0.Y1) * (r0.Y2 - r0.Y1)) / Math.Max(1, r0.Risers - 1);
            try
            {
                using (var scope = new StairsEditScope(d, "Cầu thang " + st.Id))
                {
                    var sid = scope.Start(b.Id, t.Id);
                    using (var tr = new Transaction(d, "Cầu thang " + st.Id))
                    {
                        tr.Start();
                        Swallow(tr, c.Log);
                        var stairs = (Stairs)d.GetElement(sid);
                        stairs.DesiredRisersNumber = total;
                        try { stairs.ActualTreadDepth = VxtRevit.ToFt(tread); } catch (Exception ex) { c.Log.Add($"Cầu thang {st.Id}: không đặt được mặt bậc {tread * 1000:0} mm ({ex.Message})."); }
                        double zb0 = b.ProjectElevation, zrel = 0;
                        var runs = new List<StairsRun>();
                        for (int i = 0; i < st.Runs.Count; i++)
                        {
                            var r = st.Runs[i];
                            double len = Math.Sqrt((r.X2 - r.X1) * (r.X2 - r.X1) + (r.Y2 - r.Y1) * (r.Y2 - r.Y1));
                            double ux = (r.X2 - r.X1) / len, uy = (r.Y2 - r.Y1) / len;
                            double L = (r.Risers - 1) * tread;            // đúng số mặt bậc của vế
                            var p0 = P(r.X1, r.Y1); var p1 = P(r.X1 + ux * L, r.Y1 + uy * L);
                            var run = StairsRun.CreateStraightRun(d, sid, Line.CreateBound(new XYZ(p0.X, p0.Y, zb0 + zrel), new XYZ(p1.X, p1.Y, zb0 + zrel)), StairsRunJustification.Center);
                            try { run.ActualRunWidth = VxtRevit.ToFt(r.Width); } catch (Exception exw) { c.Log.Add($"Cầu thang {st.Id} vế {i + 1}: bề rộng {r.Width * 1000:0} mm chưa đặt được ({exw.Message})."); }
                            runs.Add(run);
                            zrel = run.TopElevation;
                        }
                        // Revit có thể tính lại mặt bậc theo loại thang khi thêm vế → đặt lại đúng mặt bậc phương án
                        try { if (Math.Abs(VxtRevit.ToM(stairs.ActualTreadDepth) - tread) > 0.001) stairs.ActualTreadDepth = VxtRevit.ToFt(tread); } catch (Exception ex) { c.Log.Add($"Cầu thang {st.Id}: loại thang không cho mặt bậc {tread * 1000:0} mm ({ex.Message})."); }
                        for (int i = 0; i + 1 < runs.Count; i++)
                        {
                            bool ok = false;
                            try { ok = StairsLanding.CreateAutomaticLanding(d, runs[i].Id, runs[i + 1].Id).Count > 0; } catch { }
                            if (!ok && i < st.Landings.Count && st.Landings[i].Count >= 3)
                            {
                                var ll = new CurveLoop(); var pl = st.Landings[i];
                                double zl = runs[i].TopElevation;
                                for (int k = 0; k < pl.Count; k++)
                                {
                                    var a = P(pl[k][0], pl[k][1]); var bb = P(pl[(k + 1) % pl.Count][0], pl[(k + 1) % pl.Count][1]);
                                    ll.Append(Line.CreateBound(new XYZ(a.X, a.Y, zb0 + zl), new XYZ(bb.X, bb.Y, zb0 + zl)));
                                }
                                StairsLanding.CreateSketchedLanding(d, sid, ll, zl); ok = true;
                            }
                            if (!ok) c.Log.Add($"Cầu thang {st.Id}: chưa tạo được chiếu nghỉ giữa vế {i + 1} và {i + 2}.");
                        }
                        tr.Commit();
                    }
                    scope.Commit(new WarningSwallower(c.Log));
                    var el = d.GetElement(sid) as Stairs;
                    if (el != null)
                    {
                        using (var tr = new Transaction(d, "Gắn mã cầu thang"))
                        {
                            tr.Start();
                            Swallow(tr, c.Log);
                            VxtRevit.SetVxtId(el, Prefix + st.Id);
                            // mặt bậc và bề rộng vế theo phương án (sau khi Revit đã chốt thang)
                            if (Math.Abs(VxtRevit.ToM(el.ActualTreadDepth) - tread) > 0.001)
                            {
                                try { el.ActualTreadDepth = VxtRevit.ToFt(tread); }
                                catch (Exception ex) { c.Log.Add($"Cầu thang {st.Id}: không đặt được mặt bậc {tread * 1000:0} mm – {ex.Message}"); }
                            }
                            var runIds = el.GetStairsRuns().ToList();
                            for (int i = 0; i < runIds.Count && i < st.Runs.Count; i++)
                            {
                                if (!(d.GetElement(runIds[i]) is StairsRun sr) || st.Runs[i].Width < 0.3) continue;
                                if (Math.Abs(VxtRevit.ToM(sr.ActualRunWidth) - st.Runs[i].Width) < 0.001) continue;
                                try { sr.ActualRunWidth = VxtRevit.ToFt(st.Runs[i].Width); }
                                catch (Exception ex) { c.Log.Add($"Cầu thang {st.Id} vế {i + 1}: không đặt được bề rộng {st.Runs[i].Width * 1000:0} mm – {ex.Message}"); }
                            }
                            tr.Commit();
                        }
                        c.Log.Add($"Cầu thang {st.Id}: {st.Runs.Count} vế, {el.ActualRisersNumber} bậc × {VxtRevit.ToM(el.ActualRiserHeight) * 1000:0} mm, mặt bậc {VxtRevit.ToM(el.ActualTreadDepth) * 1000:0} mm.");
                    }
                }
            }
            catch (Exception ex) { c.Log.Add($"Cầu thang {st.Id}: chưa dựng tự động được ({ex.Message}) – lỗ sàn đã chừa."); }
        }

        /// <summary>Cầu thang 1 vế: ghi "thẳng" trong Shape, hoặc ô thang hẹp (&lt; 1,6 m) không đủ 2 vế.</summary>
        internal static bool Straight(AsStair st, double w) =>
            (st.Shape ?? "").IndexOf("thẳng", StringComparison.OrdinalIgnoreCase) >= 0 || (st.Shape ?? "").IndexOf("1 vế", StringComparison.OrdinalIgnoreCase) >= 0 || w < 1.6;

        internal static void Swallow(Transaction t, List<string> log)
        {
            var o = t.GetFailureHandlingOptions();
            o.SetFailuresPreprocessor(new WarningSwallower(log));
            t.SetFailureHandlingOptions(o);
        }
    }

    /// <summary>Gom cảnh báo Revit vào báo cáo thay vì bật hộp thoại (agent chạy không có người bấm). Lỗi thật vẫn huỷ thao tác.</summary>
    internal sealed class WarningSwallower : IFailuresPreprocessor
    {
        private readonly List<string> _log;
        public WarningSwallower(List<string> log) { _log = log; }
        public FailureProcessingResult PreprocessFailures(FailuresAccessor fa)
        {
            foreach (var f in fa.GetFailureMessages())
                if (f.GetSeverity() == FailureSeverity.Warning)
                {
                    string m = "Revit cảnh báo: " + f.GetDescriptionText();
                    if (_log != null && !_log.Contains(m)) _log.Add(m);
                    fa.DeleteWarning(f);
                }
            return FailureProcessingResult.Continue;
        }
    }
}

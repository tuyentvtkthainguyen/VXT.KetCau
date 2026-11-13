using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>[Hệ mái tôn] Mái tôn nhà thép tiền chế, thưng bịt tường, mái chống nóng nhà dân dụng – vẽ theo diện KÉO CHỌN trên mặt bằng:
/// chọn loại + profile tôn + bước xà gồ / vì kèo trong hộp thoại → kéo khung trên mặt bằng (bắt trục) → chương trình dựng xà gồ, vì kèo,
/// trụ đỡ, tôn lợp; hiện trên mặt bằng và 3D, bấm vào để xem thông tin (khối lượng, kiểm tra xà gồ), Delete xoá, "Sửa hệ mái…" để sửa.</summary>
public partial class MainWindow
{
    private RoofFraming _rfTool;          // mẫu đang vẽ (null = không vẽ)
    private bool _rfAutoDir = true;       // phương nóc / tường tự theo cạnh dài
    private Point? _rfA, _rfB;
    private bool _rfDragging;
    private RoofFraming _selRoof;
    private bool _hideWalls, _hideDoors, _hideRoofs;
    private readonly Dictionary<int, (string Key, RoofFramingGeom G)> _rfGeo = new();

    private static readonly Brush RfPurlinBrush = new SolidColorBrush(Color.FromRgb(184, 134, 11));
    private static readonly Brush RfTrussBrush = new SolidColorBrush(Color.FromRgb(70, 110, 160));
    private static readonly Brush RfEdgeBrush = new SolidColorBrush(Color.FromRgb(210, 105, 30));

    internal RoofFramingGeom RoofGeom(RoofFraming r)
    {
        string key = System.Text.Json.JsonSerializer.Serialize(r) + "|" + RoofFramingBuilder.StoryElevation(_p, r.Story) + "|" + (RoofFramingBuilder.ProfileOf(_p, r)?.Weight ?? 0);
        if (_rfGeo.TryGetValue(r.Id, out var c) && c.Key == key) return c.G;
        var g = RoofFramingBuilder.Build(_p, r);
        _rfGeo[r.Id] = (key, g);
        return g;
    }

    // ==================================================================
    // Lệnh
    // ==================================================================
    internal void StartRoofFraming(RoofFramingKind kind)
    {
        if (_p == null) return;
        string st = CurrentStory == AllStories || _p.Stories.All(q => q.Name != CurrentStory) ? _p.Stories.OrderBy(s => s.Elevation).LastOrDefault()?.Name ?? "" : CurrentStory;
        var last = _p.RoofFramings.LastOrDefault(q => q.Kind == kind);
        var r = last != null ? last.Clone() : RoofFramingBuilder.Defaults(kind, _p, st);
        r.Story = st; r.Id = 0; r.Name = "";
        if (last != null && kind != RoofFramingKind.ThungTuong) r.BaseOffset = RoofFramingBuilder.Defaults(kind, _p, st).BaseOffset;
        var dlg = new RoofFramingDialog(_p, r, true) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        _rfAutoDir = dlg.AutoDirection;
        _rfTool = r;
        _rfA = _rfB = null;
        ShowPlanTab();
        if (planHost != null) planHost.Cursor = Cursors.Cross;
        SetStatus($"{RoofFraming.KindText(kind)}: KÉO KHUNG trên mặt bằng tầng {r.Story} để chọn diện " +
                  (kind == RoofFramingKind.ThungTuong ? "(tường thưng chạy theo cạnh dài của khung)" : "(nóc mái " + (_rfAutoDir ? "theo cạnh dài" : r.RidgeAlongX ? "song song trục X" : "song song trục Y") + ")") +
                  " – góc khung bắt vào trục; kéo tiếp để vẽ thêm; Esc để kết thúc.");
    }

    internal void EditSelectedRoof()
    {
        var r = _selRoof ?? _p?.RoofFramings.LastOrDefault();
        if (r == null) { SetStatus("Chưa có hệ mái nào – Vẽ → Mái tôn / xà gồ / vì kèo."); return; }
        var copy = r.Clone();
        var dlg = new RoofFramingDialog(_p, copy, false) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        string snap = Snapshot();
        int i = _p.RoofFramings.IndexOf(r);
        if (i < 0) return;
        _p.RoofFramings[i] = copy;
        PushUndo(snap); MarkDirty(false);
        SelectRoof(copy);
        RoofChanged();
        SetStatus($"Đã sửa {copy.Name}.");
    }

    internal void OpenSheetProfiles()
    {
        if (_p == null) return;
        string snap = Snapshot();
        var dlg = new SheetProfileDialog(_p) { Owner = this };
        if (dlg.ShowDialog() == true) { PushUndo(snap); MarkDirty(false); _rfGeo.Clear(); RoofChanged(); }
    }

    private void RoofChanged()
    {
        RedrawPlan();
        try { Build3D(); } catch (Exception) { }
        _need3DRebuild = true;
        RedrawSecondaryViews();
    }

    private void SelectRoof(RoofFraming r)
    {
        ClearHostedSelection(false);
        if (HasSelection) Select();
        _selRoof = r;
        _v3SelId = null;
        OnSelectionChanged();
    }

    /// <summary>Gọi đầu ShowSelection – thông tin hệ mái đang chọn.</summary>
    private bool ShowRoofSelection()
    {
        if (_selRoof != null && (_p == null || !_p.RoofFramings.Contains(_selRoof) || HasSelection)) _selRoof = null;
        if (_selRoof == null) return false;
        txtInfo.Text = RoofInfo(_selRoof);
        return true;
    }

    internal string RoofInfo(RoofFraming r)
    {
        var g = RoofGeom(r);
        var sb = new StringBuilder();
        sb.AppendLine($"{r.Name}  ({RoofFraming.KindText(r.Kind)})");
        sb.AppendLine($"Tầng: {r.Story}   Diện: {r.MaxX - r.MinX:0.##} × {r.MaxY - r.MinY:0.##} m");
        if (r.Kind == RoofFramingKind.ThungTuong)
            sb.AppendLine($"Thưng cao từ +{r.BaseOffset:0.##} đến +{r.TopOffset:0.##} m (so với sàn tầng), dài {(r.RidgeAlongX ? r.MaxX - r.MinX : r.MaxY - r.MinY):0.##} m");
        else
            sb.AppendLine($"{(r.TwoSlopes ? "Hai dốc" : "Một dốc")}, i = {r.SlopePercent:0.#}% ({g.SlopeDeg:0.#}°), nóc song song trục {(r.RidgeAlongX ? "X" : "Y")}, chân mái +{r.BaseOffset:0.##} m, đua {r.Overhang:0.##} m");
        sb.AppendLine($"Tôn: {g.Profile.Name} – {g.SheetArea:0.#} m² ({g.Profile.Weight:0.#} kg/m²)");
        sb.AppendLine($"Xà gồ: {r.PurlinSection} bước {r.PurlinSpacing:0.###} m – {g.PurlinCount} thanh");
        if (r.IncludeTrusses)
            sb.AppendLine(r.Kind == RoofFramingKind.ThungTuong ? $"Cột sườn: {r.ChordSection} bước {r.TrussSpacing:0.##} m – {g.TrussCount} cột"
                                                              : $"Vì kèo: {g.TrussCount} bộ bước {r.TrussSpacing:0.##} m – cánh {r.ChordSection}, bụng {r.WebSection}" + (g.PostCount > 0 ? $", trụ {r.PostSection} × {g.PostCount}" : ""));
        if (g.Ridge != null) sb.AppendLine($"Úp nóc: {(g.Ridge.Value.B - g.Ridge.Value.A).Length:0.##} m");
        sb.AppendLine("Khối lượng thép:");
        foreach (var t in g.Takeoff().OrderBy(t => t.Role))
            sb.AppendLine($"  {t.Role} {t.Section}: {t.Count} thanh, {t.Length:0.#} m, {t.Kg:0} kg");
        sb.AppendLine($"  Tổng: {g.SteelKg:0} kg ({(g.PlanArea > 0 ? g.SteelKg / g.PlanArea : 0):0.#} kg/m² mặt bằng)");
        foreach (var c in g.Checks) sb.AppendLine(c);
        foreach (var n in g.Notes) sb.AppendLine(n);
        sb.Append("Delete: xoá · Sửa: Vẽ → Mái tôn / xà gồ → Sửa hệ mái đang chọn…");
        return sb.ToString();
    }

    // ==================================================================
    // Kéo khung trên mặt bằng
    // ==================================================================
    private bool RoofDragDown(Point s)
    {
        if (_rfTool == null || _p == null) return false;
        _rfA = SnapRoof(s); _rfB = _rfA; _rfDragging = true;
        planHost.CaptureMouse();
        DrawHover();
        return true;
    }

    private bool RoofDragMove(Point s)
    {
        if (_rfTool == null) return false;
        if (_rfDragging) { _rfB = SnapRoof(s); DrawHover(); return true; }
        return false;
    }

    private bool RoofDragUp(Point s)
    {
        if (_rfTool == null || !_rfDragging) return false;
        _rfDragging = false;
        planHost.ReleaseMouseCapture();
        _rfB = SnapRoof(s);
        var (x0, y0) = S2W(_rfA.Value); var (x1, y1) = S2W(_rfB.Value);
        _rfA = _rfB = null;
        if (Math.Abs(x1 - x0) < 0.3 && Math.Abs(y1 - y0) < 0.3)
        {
            SetStatus("Khung quá nhỏ – kéo khung bao diện mái / đoạn tường cần thưng (Esc để kết thúc).");
            DrawHover();
            return true;
        }
        var r = _rfTool.Clone();
        r.X0 = Math.Round(Math.Min(x0, x1), 3); r.X1 = Math.Round(Math.Max(x0, x1), 3);
        r.Y0 = Math.Round(Math.Min(y0, y1), 3); r.Y1 = Math.Round(Math.Max(y0, y1), 3);
        if (_rfAutoDir) r.RidgeAlongX = r.X1 - r.X0 >= r.Y1 - r.Y0;
        CreateRoofFraming(r);
        return true;
    }

    internal RoofFraming CreateRoofFraming(RoofFraming r)
    {
        string snap = Snapshot();
        r.Id = _p.RoofFramings.Select(q => q.Id).DefaultIfEmpty(0).Max() + 1;
        if (string.IsNullOrWhiteSpace(r.Name))
            r.Name = (r.Kind switch { RoofFramingKind.ThungTuong => "TT", RoofFramingKind.MaiTonTienChe => "MT", _ => "CN" }) + "-" + r.Id;
        _p.RoofFramings.Add(r);
        PushUndo(snap); MarkDirty(false);
        SelectRoof(r);
        RoofChanged();
        var g = RoofGeom(r);
        SetStatus($"Đã tạo {r.Name}: {g.PurlinCount} xà gồ, {g.TrussCount} {(r.Kind == RoofFramingKind.ThungTuong ? "cột sườn" : "vì kèo")}, tôn {g.SheetArea:0.#} m², thép {g.SteelKg:0} kg. " +
                  (g.Checks.Count > 0 ? g.Checks[0] : "") + (_rfTool != null ? "  – Kéo tiếp để vẽ thêm, Esc để kết thúc." : ""));
        return r;
    }

    /// <summary>Bắt góc khung vào trục gần nhất (≤ 10 px theo từng phương).</summary>
    private Point SnapRoof(Point s)
    {
        var (x, y) = S2W(s);
        double tol = 10 / Math.Max(_scale, 1e-6);
        var gx = _p.Grids.Where(g => g.Dir == GridDir.X).OrderBy(g => Math.Abs(g.Coord - x)).FirstOrDefault();
        var gy = _p.Grids.Where(g => g.Dir == GridDir.Y).OrderBy(g => Math.Abs(g.Coord - y)).FirstOrDefault();
        if (gx != null && Math.Abs(gx.Coord - x) <= tol) x = gx.Coord;
        if (gy != null && Math.Abs(gy.Coord - y) <= tol) y = gy.Coord;
        return W2S(x, y);
    }

    private bool DrawRoofDragOverlay()
    {
        if (_rfTool == null || _rfA == null || _rfB == null) return false;
        var a = _rfA.Value; var b = _rfB.Value;
        var rect = new Shapes.Rectangle
        {
            Width = Math.Abs(b.X - a.X), Height = Math.Abs(b.Y - a.Y), Stroke = RfEdgeBrush, StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 5, 3 }, Fill = new SolidColorBrush(Color.FromArgb(40, 255, 140, 0)), IsHitTestVisible = false
        };
        Canvas.SetLeft(rect, Math.Min(a.X, b.X)); Canvas.SetTop(rect, Math.Min(a.Y, b.Y));
        planOverlay.Children.Add(rect);
        var (x0, y0) = S2W(a); var (x1, y1) = S2W(b);
        AddText($"{Math.Abs(x1 - x0):0.##} × {Math.Abs(y1 - y0):0.##} m", new Point(Math.Max(a.X, b.X) + 6, Math.Max(a.Y, b.Y) + 4), RfEdgeBrush, 11, false, false, planOverlay);
        return true;
    }

    private bool HandleRoofKey(Key key)
    {
        if (key == Key.Escape && _rfTool != null)
        {
            _rfTool = null; _rfA = _rfB = null; _rfDragging = false;
            if (planHost != null) planHost.Cursor = _mode == PlanEditMode.Select ? null : Cursors.Cross;
            DrawHover();
            SetStatus("Đã kết thúc vẽ hệ mái.");
            return true;
        }
        if (key == Key.Escape && _selRoof != null) { _selRoof = null; OnSelectionChanged(); return true; }
        if (key == Key.Delete && _selRoof != null && _p.RoofFramings.Contains(_selRoof))
        {
            string snap = Snapshot();
            var r = _selRoof;
            _p.RoofFramings.Remove(r);
            _selRoof = null;
            PushUndo(snap); MarkDirty(false);
            OnSelectionChanged();
            RoofChanged();
            SetStatus($"Đã xoá {r.Name} (Ctrl+Z để hoàn tác).");
            return true;
        }
        return false;
    }

    /// <summary>Bấm chọn hệ mái trên mặt bằng: gần nét xà gồ / vì kèo / biên (≤ 6 px) và không trúng cột, dầm, tường.</summary>
    private bool RoofFramingClick(Point s)
    {
        if (_p == null || _hideRoofs || _p.RoofFramings.Count == 0) return false;
        if (ColumnAt(s) != null || NearestBeam(s, 6) != null || WallAtScreen(s, 6) != null) return false;
        RoofFraming best = null; double bd = 6;
        foreach (var r in PlanRoofs())
        {
            var g = RoofGeom(r);
            foreach (var m in g.Members)
            {
                double d = DistPtSeg(s, W2S(m.A.X, m.A.Y), W2S(m.B.X, m.B.Y));
                if (d < bd) { bd = d; best = r; }
            }
            foreach (var pn in g.Panels)
            {
                var c = new[] { pn.P0, pn.P1, pn.P2, pn.P3 };
                for (int i = 0; i < 4; i++)
                {
                    double d = DistPtSeg(s, W2S(c[i].X, c[i].Y), W2S(c[(i + 1) % 4].X, c[(i + 1) % 4].Y));
                    if (d < bd) { bd = d; best = r; }
                }
            }
        }
        if (best == null) { if (_selRoof != null) { _selRoof = null; } return false; }
        SelectRoof(best);
        return true;
    }

    private static double DistPtSeg(Point p, Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, L2 = dx * dx + dy * dy;
        double t = L2 < 1e-9 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / L2, 0, 1);
        double qx = a.X + t * dx - p.X, qy = a.Y + t * dy - p.Y;
        return Math.Sqrt(qx * qx + qy * qy);
    }

    private IEnumerable<RoofFraming> PlanRoofs() =>
        _p == null ? Enumerable.Empty<RoofFraming>() : _p.RoofFramings.Where(r => CurrentStory == AllStories || r.Story == CurrentStory);

    // ==================================================================
    // Vẽ mặt bằng
    // ==================================================================
    private void DrawRoofFramings(bool labels)
    {
        if (_p == null || _hideRoofs) return;
        foreach (var r in PlanRoofs())
        {
            var g = RoofGeom(r);
            bool sel = ReferenceEquals(r, _selRoof);
            Brush edge = sel ? HiBrush : RfEdgeBrush;
            foreach (var pn in g.Panels)
            {
                var pts = new PointCollection { W2S(pn.P0.X, pn.P0.Y), W2S(pn.P1.X, pn.P1.Y), W2S(pn.P2.X, pn.P2.Y), W2S(pn.P3.X, pn.P3.Y) };
                planCanvas.Children.Add(new Shapes.Polygon
                {
                    Points = pts, Stroke = edge, StrokeThickness = sel ? 2.2 : 1.2, StrokeDashArray = r.Kind == RoofFramingKind.ThungTuong ? null : new DoubleCollection { 6, 3 },
                    Fill = new SolidColorBrush(Color.FromArgb(r.Kind == RoofFramingKind.ThungTuong ? (byte)0 : (byte)18, 255, 140, 0)), IsHitTestVisible = false
                });
            }
            if (g.Ridge != null) CLine(planCanvas, W2S(g.Ridge.Value.A.X, g.Ridge.Value.A.Y), W2S(g.Ridge.Value.B.X, g.Ridge.Value.B.Y), edge, sel ? 2.4 : 1.8, true);
            foreach (var m in g.Members)
            {
                var a = W2S(m.A.X, m.A.Y); var b = W2S(m.B.X, m.B.Y);
                if ((b - a).Length < 1.5)
                {
                    if (m.Role.StartsWith("Trụ") || m.Role.StartsWith("Cột"))
                    {
                        var e = new Shapes.Rectangle { Width = 6, Height = 6, Fill = RfTrussBrush, IsHitTestVisible = false };
                        Canvas.SetLeft(e, a.X - 3); Canvas.SetTop(e, a.Y - 3); planCanvas.Children.Add(e);
                    }
                    continue;
                }
                bool purlin = m.Role.StartsWith("Xà gồ");
                if (!purlin && !m.Role.StartsWith("Cánh hạ") && r.Kind != RoofFramingKind.ThungTuong) continue;   // vì kèo: chỉ vẽ cánh hạ trên mặt bằng
                var ln = CLine(planCanvas, a, b, purlin ? RfPurlinBrush : RfTrussBrush, purlin ? 0.9 : 1.4, !purlin);
                if (!purlin) ln.StrokeDashArray = new DoubleCollection { 8, 3, 2, 3 };
                ln.IsHitTestVisible = false;
            }
            if (labels)
            {
                double cx = (r.MinX + r.MaxX) / 2, cy = (r.MinY + r.MaxY) / 2;
                string t = r.Kind == RoofFramingKind.ThungTuong
                    ? $"{r.Name}: thưng tôn, XG {r.PurlinSection} a{r.PurlinSpacing * 1000:0}"
                    : $"{r.Name}: {g.Profile.Name}, i={r.SlopePercent:0.#}% · XG {r.PurlinSection} a{r.PurlinSpacing * 1000:0}" + (r.IncludeTrusses ? $" · VK a{r.TrussSpacing * 1000:0}" : "");
                AddText(t, W2S(cx, cy), edge, 10, true, true);
            }
        }
    }

    // ==================================================================
    // 3D
    // ==================================================================
    private void AddRoofFramings3D(Model3DGroup group, Dictionary<GeometryModel3D, PickMesh> pick, bool dim)
    {
        if (_p == null || _hideRoofs || _p.RoofFramings.Count == 0) return;
        var steel = new PickMesh(); var sel = new PickMesh(); var sheet = new PickMesh(); var wallSheet = new PickMesh();
        foreach (var r in _p.RoofFramings)
        {
            if (!Show3D("MAI-" + r.Id, Discipline.KienTruc)) continue;
            var g = RoofGeom(r);
            bool isSel = ReferenceEquals(r, _selRoof);
            var pm = isSel ? sel : steel;
            pm.Begin(r);
            bool ax = r.RidgeAlongX;
            var along = ax ? Vec3.UnitX : Vec3.UnitY;
            foreach (var m in g.Members)
            {
                var s = ThinSection.Parse(m.Section);
                double d = Math.Max(0.03, s.H), w = Math.Max(0.02, s.B);
                var e1 = (m.B - m.A).Normalize();
                Vec3 wd;
                if (m.Role.StartsWith("Xà gồ tường")) wd = Vec3.UnitZ;
                else if (m.Role.StartsWith("Xà gồ")) wd = e1.Cross(Vec3.UnitZ);
                else wd = along;
                AddBeamBox(pm.Mesh, m.A, m.B, wd, w, d);
            }
            var pf = g.Profile;
            var sm = isSel ? sel : r.Kind == RoofFramingKind.ThungTuong ? wallSheet : sheet;
            sm.Begin(r);
            foreach (var pn in g.Panels) AddSheet3D(sm.Mesh, pn, pf, r.Kind == RoofFramingKind.ThungTuong);
        }
        AddPickModel(group, pick, steel, Diffuse(Color.FromArgb((byte)(dim ? 60 : 255), 120, 130, 145)), false);
        AddPickModel(group, pick, sel, SelectedMaterial3D(), true);
        AddPickModel(group, pick, sheet, Diffuse(Color.FromArgb((byte)(dim ? 40 : 170), 200, 70, 60)), true);
        AddPickModel(group, pick, wallSheet, Diffuse(Color.FromArgb((byte)(dim ? 40 : 170), 90, 140, 190)), true);
    }

    /// <summary>Hộp theo thanh A→B: bề rộng w theo hướng wd (chiếu vuông góc thanh), cao d theo hướng còn lại.</summary>
    private static void AddBeamBox(MeshGeometry3D m, Vec3 a, Vec3 b, Vec3 wd, double w, double d)
    {
        var e1 = (b - a).Normalize();
        var e2 = wd - e1 * e1.Dot(wd);
        if (e2.Length < 1e-6) e2 = Math.Abs(e1.Z) < 0.9 ? e1.Cross(Vec3.UnitZ) : e1.Cross(Vec3.UnitX);
        e2 = e2.Normalize();
        var e3 = e1.Cross(e2).Normalize();
        var c4 = new[] { e2 * (w / 2) + e3 * (d / 2), e2 * (-w / 2) + e3 * (d / 2), e2 * (-w / 2) + e3 * (-d / 2), e2 * (w / 2) + e3 * (-d / 2) };
        AddHexa(m, c4.Select(c => P(a + c)).ToArray(), c4.Select(c => P(b + c)).ToArray());
    }

    /// <summary>Tấm tôn: mặt phẳng + gân sóng (hộp mảnh dọc sóng) theo bước sóng của profile.</summary>
    private static void AddSheet3D(MeshGeometry3D m, RfPanel pn, SheetProfile pf, bool wall)
    {
        int i0 = m.Positions.Count;
        m.Positions.Add(P(pn.P0)); m.Positions.Add(P(pn.P1)); m.Positions.Add(P(pn.P2)); m.Positions.Add(P(pn.P3));
        Quad(m, i0, i0 + 1, i0 + 2, i0 + 3);
        var U = pn.P1 - pn.P0; var V = pn.P3 - pn.P0;
        double Lu = U.Length;
        if (Lu < 0.1 || V.Length < 0.1) return;
        var u = U.Normalize(); var n = U.Cross(V).Normalize();
        if (!wall && n.Z < 0) n = n * -1;
        double pitch = Math.Max(0.05, pf.RibPitch);
        int count = (int)(Lu / pitch);
        int step = Math.Max(1, count / 400);   // giới hạn số gân hiển thị
        double h = Math.Max(0.008, pf.RibHeight), w = Math.Max(0.01, pf.Shape == "Sóng tròn" ? pitch * 0.45 : pf.RibTop);
        for (int k = 0; k <= count; k += step)
        {
            var o = u * (k * pitch + pitch / 2);
            if (k * pitch + pitch / 2 > Lu) break;
            var a = pn.P0 + o + n * (h / 2); var b = pn.P3 + o + n * (h / 2);
            AddBeamBox(m, a, b, u, w, h);
        }
    }

    // ==================================================================
    // Cửa trong 3D (tấm cửa trong ô cửa đã khoét)
    // ==================================================================
    private sealed record HostedRef(WallLine Wall, WallOpening Opening);

    private void AddDoors3D(Model3DGroup group, Dictionary<GeometryModel3D, PickMesh> pick, bool dim)
    {
        if (_p?.Walls == null || _hideDoors || _hideWalls) return;
        var door = new PickMesh(); var glass = new PickMesh(); var sel = new PickMesh();
        foreach (var w in _p.Walls)
        {
            if (w.Openings == null || w.Openings.Count == 0 || w.IsCurved) continue;
            var st = _p.Stories.FirstOrDefault(q => q.Name == w.Story);
            if (st == null || w.Chord < 0.05) continue;
            if (!Show3D("TUONG-" + w.Id, Discipline.KienTruc)) continue;
            double dx = w.X2 - w.X1, dy = w.Y2 - w.Y1, L = Math.Sqrt(dx * dx + dy * dy);
            var u = new Vec3(dx / L, dy / L, 0); var nrm = new Vec3(-u.Y, u.X, 0);
            foreach (var o in w.Openings)
            {
                var t = WallAssembly.TypeOf(_p, o);
                if (t == null) continue;
                bool isDoor = t.Kind == WallOpeningKind.Door;
                var pm = ReferenceEquals(o, _rvSelOpen) ? sel : isDoor ? door : glass;
                pm.Begin(new HostedRef(w, o));
                var c = new Vec3(w.X1, w.Y1, 0) + u * o.Offset;
                double z0 = st.Elevation + t.Sill, z1 = z0 + t.Height;
                var a = new Vec3(c.X, c.Y, z0) - u * (t.Width / 2); var b = new Vec3(c.X, c.Y, z0) + u * (t.Width / 2);
                // khung: 2 thanh đứng + thanh trên; cánh: tấm mỏng
                double fr = Math.Max(0.03, t.Frame);
                AddBeamBox(pm.Mesh, a + u * (fr / 2), a + u * (fr / 2) + Vec3.UnitZ * t.Height, nrm, 0.06, fr);
                AddBeamBox(pm.Mesh, b - u * (fr / 2), b - u * (fr / 2) + Vec3.UnitZ * t.Height, nrm, 0.06, fr);
                AddBeamBox(pm.Mesh, a + Vec3.UnitZ * (t.Height - fr / 2), b + Vec3.UnitZ * (t.Height - fr / 2), nrm, 0.06, fr);
                if (!isDoor) AddBeamBox(pm.Mesh, a + Vec3.UnitZ * (fr / 2), b + Vec3.UnitZ * (fr / 2), nrm, 0.06, fr);
                AddBeamBox(pm.Mesh, a + Vec3.UnitZ * (t.Height / 2), b + Vec3.UnitZ * (t.Height / 2), nrm, 0.025, Math.Max(0.05, t.Height - 2 * fr));
                _ = z1;
            }
        }
        AddPickModel(group, pick, door, Diffuse(Color.FromArgb((byte)(dim ? 50 : 255), 150, 100, 60)), false);
        AddPickModel(group, pick, glass, Diffuse(Color.FromArgb((byte)(dim ? 40 : 150), 120, 180, 230)), true);
        AddPickModel(group, pick, sel, SelectedMaterial3D(), true);
    }

    private string DoorInfo(WallLine w, WallOpening o)
    {
        var t = WallAssembly.TypeOf(_p, o);
        var sb = new StringBuilder();
        sb.AppendLine($"{t?.KindText ?? "Cửa"} {o.Type}  (#{o.Id})");
        sb.AppendLine($"Trên tường #{w.Id} ({w.Type}), tầng {w.Story}");
        if (t != null)
        {
            sb.AppendLine($"Rộng × cao: {t.Width * 1000:0} × {t.Height * 1000:0} mm, bậu {t.Sill * 1000:0} mm");
            sb.AppendLine($"Cách mở: {t.Operation}, khung {t.Frame * 1000:0} mm");
            sb.AppendLine($"Diện tích ô cửa: {t.Width * t.Height:0.##} m²");
        }
        sb.AppendLine($"Tâm cửa cách đầu tường {o.Offset:0.###} m");
        sb.Append("Delete: xoá cửa · Space: lật hướng mở (trên mặt bằng)");
        return sb.ToString();
    }

    // ==================================================================
    // Ẩn / hiện tường, cửa, mái tôn (dùng chung mặt bằng + 3D)
    // ==================================================================
    internal void SetCategoryVisible(string cat, bool visible)
    {
        switch (cat)
        {
            case "Tường": _hideWalls = !visible; break;
            case "Cửa": _hideDoors = !visible; break;
            case "Mái": _hideRoofs = !visible; break;
        }
        SyncCategoryChecks();
        RedrawPlan();
        try { BuildMain3D(false); } catch (Exception) { }
        _need3DRebuild = true;
        RedrawSecondaryViews();
    }

    private readonly List<(string Cat, CheckBox Box)> _catChecks = new();
    private bool _catSyncing;

    private CheckBox CategoryCheck(string cat, string text, Brush fg, string tip)
    {
        var cb = new CheckBox { Content = text, IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), ToolTip = tip };
        if (fg != null) cb.Foreground = fg;
        cb.Checked += (s, e) => { if (!_catSyncing) SetCategoryVisible(cat, true); };
        cb.Unchecked += (s, e) => { if (!_catSyncing) SetCategoryVisible(cat, false); };
        _catChecks.Add((cat, cb));
        return cb;
    }

    private void SyncCategoryChecks()
    {
        _catSyncing = true;
        try
        {
            foreach (var (cat, cb) in _catChecks)
                cb.IsChecked = cat switch { "Tường" => !_hideWalls, "Cửa" => !_hideDoors, _ => !_hideRoofs };
            if (_chkWall3D != null) _chkWall3D.IsChecked = !_hideWalls;
        }
        finally { _catSyncing = false; }
    }

    /// <summary>Thêm ô Tường / Cửa / Mái tôn vào thanh khung nhìn mặt bằng (cạnh "Tên cấu kiện").</summary>
    private void EnsurePlanCategoryChecks()
    {
        if (chkLabels?.Parent is not Panel bar || _catChecks.Any(c => ReferenceEquals(c.Box.Parent, bar))) return;
        int i = bar.Children.IndexOf(chkLabels) + 1;
        bar.Children.Insert(i++, CategoryCheck("Tường", "Tường", null, "Ẩn / hiện tường xây trên mặt bằng và 3D"));
        bar.Children.Insert(i++, CategoryCheck("Cửa", "Cửa", null, "Ẩn / hiện cửa đi, cửa sổ trên mặt bằng và 3D"));
        bar.Children.Insert(i, CategoryCheck("Mái", "Mái tôn", null, "Ẩn / hiện mái tôn, xà gồ, vì kèo, thưng tường"));
    }
}

public partial class MainWindow
{
    private void MnuRoofPeb_Click(object sender, RoutedEventArgs e) => StartRoofFraming(RoofFramingKind.MaiTonTienChe);
    private void MnuRoofWall_Click(object sender, RoutedEventArgs e) => StartRoofFraming(RoofFramingKind.ThungTuong);
    private void MnuRoofHeat_Click(object sender, RoutedEventArgs e) => StartRoofFraming(RoofFramingKind.ChongNong);
    private void MnuRoofEdit_Click(object sender, RoutedEventArgs e) => EditSelectedRoof();
    private void MnuSheetProfile_Click(object sender, RoutedEventArgs e) => OpenSheetProfiles();

    /// <summary>Bảng khối lượng mọi hệ mái: xà gồ, vì kèo, trụ, tôn – xem và chép sang Excel.</summary>
    private void MnuRoofList_Click(object sender, RoutedEventArgs e) => ShowRoofTakeoff();

    internal Window ShowRoofTakeoff()
    {
        if (_p == null) return null;
        if (_p.RoofFramings.Count == 0) { SetStatus("Chưa có hệ mái nào – Vẽ → Mái tôn / xà gồ / vì kèo."); return null; }
        var rows = new List<RoofRow>();
        foreach (var r in _p.RoofFramings)
        {
            var g = RoofGeom(r);
            foreach (var t in g.Takeoff().OrderBy(t => t.Role))
                rows.Add(new RoofRow { He = r.Name, Loai = t.Role, TietDien = t.Section, SoThanh = t.Count, DaiM = Math.Round(t.Length, 2), KhoiLuongKg = Math.Round(t.Kg, 1) });
            rows.Add(new RoofRow { He = r.Name, Loai = "Tôn lợp / thưng (m²)", TietDien = g.Profile.Name, SoThanh = 0, DaiM = Math.Round(g.SheetArea, 2), KhoiLuongKg = Math.Round(g.SheetArea * g.Profile.Weight, 1) });
            if (g.Ridge != null) rows.Add(new RoofRow { He = r.Name, Loai = "Úp nóc (m)", TietDien = "", SoThanh = 0, DaiM = Math.Round((g.Ridge.Value.B - g.Ridge.Value.A).Length, 2), KhoiLuongKg = 0 });
        }
        var w = new Window { Owner = this };
        DialogUi.Setup(w, "Bảng khối lượng hệ mái tôn – xà gồ – vì kèo", 860, 520);
        var dg = DialogUi.NewGrid();
        dg.IsReadOnly = true;
        dg.Columns.Add(DialogUi.Text("Hệ mái", "He", true));
        dg.Columns.Add(DialogUi.Text("Cấu kiện", "Loai", true));
        dg.Columns.Add(DialogUi.Text("Tiết diện / profile", "TietDien", true));
        dg.Columns.Add(DialogUi.Num("Số thanh", "SoThanh", "0", true));
        dg.Columns.Add(DialogUi.Num("Dài (m) / diện tích (m²)", "DaiM", "0.##", true));
        dg.Columns.Add(DialogUi.Num("Khối lượng (kg)", "KhoiLuongKg", "0.#", true));
        dg.ItemsSource = rows;
        DialogUi.HScroll(dg);
        double steel = rows.Where(q => q.SoThanh > 0).Sum(q => q.KhoiLuongKg);
        var foot = DialogUi.Hint($"Tổng thép hệ mái: {steel:0} kg; tôn: {rows.Where(q => q.Loai.StartsWith("Tôn")).Sum(q => q.DaiM):0.#} m². Chọn ô rồi Ctrl+C để chép sang Excel.");
        var dock = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(foot, Dock.Bottom);
        dock.Children.Add(foot); dock.Children.Add(dg);
        w.Content = dock;
        w.Show();
        return w;
    }

    private sealed class RoofRow
    {
        public string He { get; set; } = "";
        public string Loai { get; set; } = "";
        public string TietDien { get; set; } = "";
        public int SoThanh { get; set; }
        public double DaiM { get; set; }
        public double KhoiLuongKg { get; set; }
    }
}

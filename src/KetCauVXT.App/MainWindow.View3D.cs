using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Model;
// System.Windows.Media.Media3D cũng có kiểu Material → bí danh tránh CS0104
using Material = KetCauVXT.Model.Material;
using Mat3D = System.Windows.Media.Media3D.Material;

namespace KetCauVXT.App;

/// <summary>Hình 3D: mỗi thanh là một hộp theo kích thước tiết diện, sàn là tấm mỏng, móng đơn dưới chân cột.
/// Kéo chuột trái để xoay quanh tâm mô hình, con lăn để thu phóng, bấm (không kéo) để chọn cấu kiện.</summary>
public partial class MainWindow
{
    private double _yaw = -45, _pitch = 30, _dist = 50;
    private Point3D _target = new(0, 0, 0);
    private bool _rotating, _rotMoved;
    private FrameworkElement _rotHost;
    private Point _rotStart;
    private double _rotYaw0, _rotPitch0;

    /// <summary>Bảng tra vật thể 3D → cấu kiện (để chọn bằng chuột) cho khung 3D chính.</summary>
    private readonly Dictionary<GeometryModel3D, PickMesh> _pick3D = new();
    /// <summary>Trạng thái mô hình lúc dựng 3D lần cuối (để biết khi nào cần dựng lại).</summary>
    private AnalysisResults _b3dRes;
    private DesignSummary _b3dDesign;
    private Project _b3dProj;
    private bool _view3DInit;
    private CheckBox _chkDim3D, _chkWall3D;

    private static readonly Color SelColor3D = Color.FromRgb(255, 230, 0);

    /// <summary>Lưới tam giác kèm bảng (chỉ số đỉnh đầu tiên → đối tượng) để tra ngược khi bấm chuột.</summary>
    private sealed class PickMesh
    {
        public readonly MeshGeometry3D Mesh = new();
        private readonly List<int> _starts = new();
        private readonly List<object> _objs = new();

        public void Begin(object o)
        {
            _starts.Add(Mesh.Positions.Count);
            _objs.Add(o);
        }

        public object Find(int vertex)
        {
            int lo = 0, hi = _starts.Count - 1, r = -1;
            while (lo <= hi)
            {
                int m = (lo + hi) / 2;
                if (_starts[m] <= vertex) { r = m; lo = m + 1; } else hi = m - 1;
            }
            return r >= 0 ? _objs[r] : null;
        }
    }

    // ------------------------------------------------------------------
    // Dựng cảnh 3D chính
    // ------------------------------------------------------------------
    private void Build3D() => BuildMain3D(true);

    /// <param name="modelChanged">false = chỉ đổi tô sáng (không cần tính lại dữ liệu biểu đồ).</param>
    private void BuildMain3D(bool modelChanged)
    {
        EnsureView3DInit();
        EnsureSecondaryInit();
        _b3dRes = _res; _b3dDesign = _design; _b3dProj = _p;
        if (modelChanged) InvalidateDiagramCache();
        bool dim = _chkDim3D?.IsChecked == true && HasSelection;
        model3DVisual.Content = BuildScene(_pick3D, null, dim);
        FitCameraTarget(false);
        UpdateCamera();
        if (!_inSecondaryRedraw) RedrawSecondaryViews();
    }

    /// <summary>Chuẩn bị lần đầu: cho phép bấm chọn trên Viewport3D, thêm ô "Làm mờ phần còn lại".</summary>
    private void EnsureView3DInit()
    {
        if (_view3DInit) return;
        _view3DInit = true;
        viewport3D.IsHitTestVisible = true;
        if (view3DHost.Child is Viewport3D vp)
        {
            view3DHost.Child = null;
            var g = new Grid();
            g.Children.Add(vp);
            _chkDim3D = new CheckBox
            {
                Content = "Làm mờ phần còn lại", Foreground = Brushes.WhiteSmoke, Margin = new Thickness(8, 6, 8, 6),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                ToolTip = "Khi đang chọn một cấu kiện, các cấu kiện khác hiển thị trong suốt"
            };
            _chkDim3D.Checked += (s, e) => BuildMain3D(false);
            _chkDim3D.Unchecked += (s, e) => BuildMain3D(false);
            g.Children.Add(_chkDim3D);
            // [Kiểu Revit] tường kiến trúc trong 3D, ô cửa khoét thật (Model/RevitFinish.cs WallSolid3D)
            _chkWall3D = new CheckBox
            {
                Content = "Tường (có ô cửa)", IsChecked = true, Foreground = Brushes.WhiteSmoke, Margin = new Thickness(170, 6, 8, 6),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                ToolTip = "Hiện tường xây trong 3D: tường tự khoét ô cửa đi / cửa sổ đã đặt (phần dưới bậu, trên đầu cửa giữ lại)"
            };
            _chkWall3D.Checked += (s, e) => BuildMain3D(false);
            _chkWall3D.Unchecked += (s, e) => BuildMain3D(false);
            g.Children.Add(_chkWall3D);
            var hint = new TextBlock
            {
                Text = "Kéo chuột: xoay  •  Con lăn: thu phóng  •  Bấm: chọn cấu kiện",
                Foreground = new SolidColorBrush(Color.FromRgb(170, 180, 190)), FontSize = 11, IsHitTestVisible = false,
                Margin = new Thickness(8, 6, 8, 6), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom
            };
            g.Children.Add(hint);
            view3DHost.Child = g;
        }
    }

    /// <summary>Đặt tâm xoay theo bao mô hình; đặt lại khoảng cách nếu bất hợp lý (hoặc force).</summary>
    private void FitCameraTarget(bool force)
    {
        if (_p == null || _p.Nodes.Count == 0) return;
        double x0 = _p.Nodes.Min(n => n.X), x1 = _p.Nodes.Max(n => n.X);
        double y0 = _p.Nodes.Min(n => n.Y), y1 = _p.Nodes.Max(n => n.Y);
        double z0 = _p.Nodes.Min(n => n.Z), z1 = _p.Nodes.Max(n => n.Z);
        if (_design != null && _design.Footings.Count > 0) z0 = Math.Min(z0, _design.Footings.Min(f => -Math.Max(f.Depth, 0)));
        _target = new Point3D((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2);
        double size = Math.Max(Math.Max(x1 - x0, y1 - y0), z1 - z0);
        if (force || _dist <= 0 || _dist > 20 * Math.Max(size, 1) || _dist < 0.2 * Math.Max(size, 1)) _dist = Math.Max(size, 5) * 2.2;
    }

    private double ModelSize()
    {
        if (_p == null || _p.Nodes.Count == 0) return 10;
        double dx = _p.Nodes.Max(n => n.X) - _p.Nodes.Min(n => n.X);
        double dy = _p.Nodes.Max(n => n.Y) - _p.Nodes.Min(n => n.Y);
        double dz = _p.Nodes.Max(n => n.Z) - _p.Nodes.Min(n => n.Z);
        return Math.Max(Math.Max(dx, dy), Math.Max(dz, 1));
    }

    private static Model3DGroup NewLitGroup()
    {
        var group = new Model3DGroup();
        group.Children.Add(new AmbientLight(Color.FromRgb(90, 90, 90)));
        group.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-0.4, 0.6, -1)));
        group.Children.Add(new DirectionalLight(Color.FromRgb(120, 120, 120), new Vector3D(0.5, -0.3, 0.6)));
        return group;
    }

    /// <summary>Dựng toàn bộ cảnh: thanh, sàn, móng (nếu đã thiết kế), cộng lớp phủ (biểu đồ) nếu có.
    /// Cấu kiện đang chọn tô vàng phát sáng; dim = làm trong suốt phần còn lại.</summary>
    private Model3DGroup BuildScene(Dictionary<GeometryModel3D, PickMesh> pick, Model3DGroup overlay, bool dim)
    {
        pick.Clear();
        var group = NewLitGroup();
        if (_p == null || _p.Nodes.Count == 0)
        {
            if (overlay != null) group.Children.Add(overlay);
            return group;
        }
        var colMesh = new PickMesh();
        var beamMesh = new PickMesh();
        var steelMesh = new PickMesh();
        var selMesh = new PickMesh();
        var slabMesh = new PickMesh();
        var footMesh = new PickMesh();
        var nodes = NodeMap();
        var secs = SectionMap();

        foreach (var f in _p.Frames)
        {
            if (!nodes.TryGetValue(f.NodeI, out var ni) || !nodes.TryGetValue(f.NodeJ, out var nj)) continue;
            double b = 0.2, h = 0.3;
            if (secs.TryGetValue(f.Section ?? "", out var sec)) { var sp = sec.Props(0.5); b = Math.Max(sp.Width, 0.05); h = Math.Max(sp.Depth, 0.05); }
            var pm = _sel != null && _sel.Id == f.Id ? selMesh : f.IsVerticalKind ? (f.IsConcrete ? colMesh : steelMesh) : (f.IsConcrete ? beamMesh : steelMesh);
            // dầm BTCT: đỉnh dầm ngang mặt sàn → dời trục xuống h/2
            double drop = f.Kind == FrameKind.Beam ? h / 2 : 0;
            pm.Begin(f);
            var cpoly = CurvedPolyline(f);   // dầm cong: chuỗi hộp theo cung (MainWindow.CurvedBeam.cs)
            if (cpoly != null) for (int k = 0; k + 1 < cpoly.Count; k++) AddMemberBox(pm.Mesh, cpoly[k], cpoly[k + 1], f.Angle, b, h, drop);
            else AddMemberBox(pm.Mesh, ni.P, nj.P, f.Angle, b, h, drop);
        }
        foreach (var s in _p.Slabs)
        {
            var pts = s.Nodes.Where(id => nodes.ContainsKey(id)).Select(id => nodes[id].P).ToList();
            if (pts.Count < 3) continue;
            double t = _p.SlabSections.FirstOrDefault(q => q.Name == s.Section)?.Thickness ?? 0.1;
            var pm = _selSlab != null && ReferenceEquals(_selSlab, s) ? selMesh : slabMesh;
            pm.Begin(s);
            AddSlab(pm.Mesh, pts, t);
        }
        if (_design != null)
            foreach (var fd in _design.Footings)
            {
                var pm = _selFooting != 0 && _selFooting == fd.NodeId ? selMesh : footMesh;
                pm.Begin(fd);
                AddFooting3D(pm.Mesh, fd, nodes);
            }

        byte a = (byte)(dim ? 45 : 255);
        // vật liệu đặc trước, trong suốt sau (WPF không tự sắp xếp độ trong suốt)
        if (!dim)
        {
            AddPickModel(group, pick, colMesh, Diffuse(Color.FromRgb(170, 170, 180)), false);
            AddPickModel(group, pick, beamMesh, Diffuse(Color.FromRgb(90, 140, 220)), false);
            AddPickModel(group, pick, steelMesh, Diffuse(Color.FromRgb(230, 140, 40)), false);
            AddPickModel(group, pick, footMesh, Diffuse(Color.FromRgb(150, 150, 150)), false);
        }
        AddPickModel(group, pick, selMesh, SelectedMaterial3D(), true);
        if (dim)
        {
            AddPickModel(group, pick, colMesh, Diffuse(Color.FromArgb(a, 170, 170, 180)), true);
            AddPickModel(group, pick, beamMesh, Diffuse(Color.FromArgb(a, 90, 140, 220)), true);
            AddPickModel(group, pick, steelMesh, Diffuse(Color.FromArgb(a, 230, 140, 40)), true);
            AddPickModel(group, pick, footMesh, Diffuse(Color.FromArgb(a, 150, 150, 150)), true);
        }
        AddPickModel(group, pick, slabMesh, Diffuse(Color.FromArgb((byte)(dim ? 30 : 110), 150, 210, 150)), true);
        if (_chkWall3D == null || _chkWall3D.IsChecked == true)
        {
            var wallMesh = new MeshGeometry3D();
            AddWalls3D(wallMesh);
            AddModel(group, wallMesh, Color.FromArgb((byte)(dim ? 40 : 150), 214, 168, 130), true);
        }
        if (overlay != null) group.Children.Add(overlay);
        return group;
    }

    /// <summary>Tường xây: mỗi đoạn tường tách thành các khối quanh ô cửa (WallSolid3D.Pieces) – ô cửa đi / cửa sổ hiện là lỗ thật.</summary>
    private void AddWalls3D(MeshGeometry3D m)
    {
        if (_p?.Walls == null) return;
        foreach (var w in _p.Walls)
        {
            var st = _p.Stories.FirstOrDefault(q => q.Name == w.Story);
            if (st == null || w.Chord < 0.05) continue;
            var wt = _p.WallTypes?.FirstOrDefault(q => q.Name == w.Type);
            double t = wt == null ? 0.22 : Math.Max(0.05, wt.TotalThickness);
            double H = WallSolid3D.DefaultHeight(_p, w);
            double dx = w.X2 - w.X1, dy = w.Y2 - w.Y1, L = Math.Sqrt(dx * dx + dy * dy);
            double ux = dx / L, uy = dy / L, nx = -uy * t / 2, ny = ux * t / 2;
            foreach (var pc in WallSolid3D.Pieces(_p, w, H))
            {
                double z0 = st.Elevation + pc.Z0, z1 = st.Elevation + pc.Z1;
                double ax = w.X1 + ux * pc.A, ay = w.Y1 + uy * pc.A, bx = w.X1 + ux * pc.B, by = w.Y1 + uy * pc.B;
                Point3D[] Ring(double z) => new[]
                {
                    new Point3D(ax - nx, ay - ny, z), new Point3D(bx - nx, by - ny, z),
                    new Point3D(bx + nx, by + ny, z), new Point3D(ax + nx, ay + ny, z)
                };
                AddHexa(m, Ring(z0), Ring(z1));
            }
        }
    }

    private Dictionary<int, Node> NodeMap()
    {
        var d = new Dictionary<int, Node>();
        if (_p != null) foreach (var n in _p.Nodes) d[n.Id] = n;
        return d;
    }

    private Dictionary<string, FrameSection> SectionMap()
    {
        var d = new Dictionary<string, FrameSection>();
        if (_p != null) foreach (var s in _p.FrameSections) if (s.Name != null) d[s.Name] = s;
        return d;
    }

    private static Mat3D Diffuse(Color c) => new DiffuseMaterial(new SolidColorBrush(c));

    private static Mat3D SelectedMaterial3D()
    {
        var g = new MaterialGroup();
        g.Children.Add(new DiffuseMaterial(new SolidColorBrush(SelColor3D)));
        g.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(200, 170, 0))));
        return g;
    }

    private static void AddPickModel(Model3DGroup g, Dictionary<GeometryModel3D, PickMesh> pick, PickMesh pm, Mat3D mat, bool twoSided)
    {
        if (pm.Mesh.Positions.Count == 0) return;
        pm.Mesh.Freeze();
        var gm = new GeometryModel3D(pm.Mesh, mat);
        if (twoSided) gm.BackMaterial = mat;
        g.Children.Add(gm);
        pick[gm] = pm;
    }

    private static void AddModel(Model3DGroup g, MeshGeometry3D m, Color c, bool twoSided = false)
    {
        if (m.Positions.Count == 0) return;
        m.Freeze();
        var mat = new DiffuseMaterial(new SolidColorBrush(c));
        var gm = new GeometryModel3D(m, mat);
        if (twoSided) gm.BackMaterial = mat;
        g.Children.Add(gm);
    }

    /// <summary>Hệ trục địa phương e1, e2, e3 (trong hệ tổng thể) đúng như bộ phân tích.</summary>
    private static (Vec3 e1, Vec3 e2, Vec3 e3) Axes(Vec3 pi, Vec3 pj, double angleDeg)
    {
        var r = ElementMatrices.LocalAxes(pi, pj, angleDeg);
        return (new Vec3(r[0, 0], r[0, 1], r[0, 2]), new Vec3(r[1, 0], r[1, 1], r[1, 2]), new Vec3(r[2, 0], r[2, 1], r[2, 2]));
    }

    /// <summary>Hộp chữ nhật theo trục thanh: trục 2 (cao h), trục 3 (rộng b) như hệ trục địa phương của chương trình.</summary>
    private static void AddMemberBox(MeshGeometry3D m, Vec3 pi, Vec3 pj, double angleDeg, double b, double h, double drop)
    {
        var (_, e2, e3) = Axes(pi, pj, angleDeg);
        var off = e2 * (-drop);
        var p0 = pi + off; var p1 = pj + off;
        var c4 = new[] { e2 * (h / 2) + e3 * (b / 2), e2 * (h / 2) - e3 * (b / 2), e2 * (-h / 2) - e3 * (b / 2), e2 * (-h / 2) + e3 * (b / 2) };
        AddHexa(m, c4.Select(c => P(p0 + c)).ToArray(), c4.Select(c => P(p1 + c)).ToArray());
    }

    /// <summary>Khối 6 mặt từ 4 đỉnh đáy và 4 đỉnh nắp (cùng thứ tự vòng).</summary>
    private static void AddHexa(MeshGeometry3D m, Point3D[] bot, Point3D[] top)
    {
        int baseIdx = m.Positions.Count;
        foreach (var c in bot) m.Positions.Add(c);
        foreach (var c in top) m.Positions.Add(c);
        for (int k = 0; k < 4; k++)
        {
            int a0 = baseIdx + k, a1 = baseIdx + (k + 1) % 4, b0 = a0 + 4, b1 = a1 + 4;
            Quad(m, a0, a1, b1, b0);
        }
        Quad(m, baseIdx + 3, baseIdx + 2, baseIdx + 1, baseIdx + 0);
        Quad(m, baseIdx + 4, baseIdx + 5, baseIdx + 6, baseIdx + 7);
    }

    private static Point3D[] RectAt(double cx, double cy, double dx, double dy, double z) => new[]
    {
        new Point3D(cx - dx / 2, cy - dy / 2, z), new Point3D(cx + dx / 2, cy - dy / 2, z),
        new Point3D(cx + dx / 2, cy + dy / 2, z), new Point3D(cx - dx / 2, cy + dy / 2, z)
    };

    /// <summary>Móng đơn: bản đế cao Hb, phần vát lên tới H (mép cột + 50 mm), cổ móng tới nút chân cột.</summary>
    private void AddFooting3D(MeshGeometry3D m, FootingDesign fd, Dictionary<int, Node> nodes)
    {
        var g = FootingGeom(fd, nodes);
        if (g.L <= 0 || g.B <= 0) return;
        // [MóngLệchTâm] đế móng lệch tâm (tâm đế = tim cột + độ lệch tâm; móng kết hợp: tâm khung bao)
        double fx = fd.X + (fd.Eccentric?.OffsetX ?? 0), fy = fd.Y + (fd.Eccentric?.OffsetY ?? 0);
        if (fd.Eccentric?.Combined != null)
        {
            var o = fd.Eccentric.Combined.Outline;
            fx = (o.Min(q => q.X) + o.Max(q => q.X)) / 2; fy = (o.Min(q => q.Y) + o.Max(q => q.Y)) / 2;
        }
        AddHexa(m, RectAt(fx, fy, g.L, g.B, g.ZBot), RectAt(fx, fy, g.L, g.B, g.ZBot + g.Hb));
        if (g.H - g.Hb > 1e-3)
            AddHexa(m, RectAt(fd.X, fd.Y, g.L, g.B, g.ZBot + g.Hb), RectAt(fd.X, fd.Y, g.TopX, g.TopY, g.ZBot + g.H));
        if (g.ZNode - (g.ZBot + g.H) > 1e-3)
            AddHexa(m, RectAt(fd.X, fd.Y, g.Cx, g.Cy, g.ZBot + g.H), RectAt(fd.X, fd.Y, g.Cx, g.Cy, g.ZNode));
    }

    /// <summary>Kích thước hình học móng để vẽ (m): đáy ở −Depth (hoặc thấp hơn nút chân cột một đoạn H).</summary>
    private readonly struct FootGeom
    {
        public double L { get; init; }
        public double B { get; init; }
        public double H { get; init; }
        public double Hb { get; init; }
        public double Cx { get; init; }
        public double Cy { get; init; }
        public double TopX { get; init; }
        public double TopY { get; init; }
        public double ZBot { get; init; }
        public double ZNode { get; init; }
    }

    private FootGeom FootingGeom(FootingDesign fd, Dictionary<int, Node> nodes)
    {
        double zNode = nodes != null && nodes.TryGetValue(fd.NodeId, out var n) ? n.Z : (_p?.BaseElevation ?? 0);
        double H = fd.H > 0 ? fd.H : 0.4;
        double hb = fd.Hb > 0 && fd.Hb < H ? fd.Hb : H;
        double zBot = fd.Depth > 0 ? -fd.Depth : zNode - H;
        if (zBot + H > zNode) zBot = zNode - H;
        double cx = fd.ColumnH > 0 ? fd.ColumnH : 0.22, cy = fd.ColumnB > 0 ? fd.ColumnB : 0.22;
        return new FootGeom
        {
            L = fd.L, B = fd.B, H = H, Hb = hb, Cx = cx, Cy = cy,
            TopX = Math.Min(fd.L, cx + 0.1), TopY = Math.Min(fd.B, cy + 0.1),
            ZBot = zBot, ZNode = zNode
        };
    }

    private static void AddSlab(MeshGeometry3D m, List<Vec3> pts, double t)
    {
        int b = m.Positions.Count;
        foreach (var p in pts) m.Positions.Add(new Point3D(p.X, p.Y, p.Z - t));
        // tam giác hoá kiểu quạt (ô sàn lồi)
        for (int i = 1; i + 1 < pts.Count; i++)
        {
            m.TriangleIndices.Add(b); m.TriangleIndices.Add(b + i); m.TriangleIndices.Add(b + i + 1);
        }
    }

    private static Point3D P(Vec3 v) => new(v.X, v.Y, v.Z);

    private static void Quad(MeshGeometry3D m, int a, int b, int c, int d)
    {
        m.TriangleIndices.Add(a); m.TriangleIndices.Add(b); m.TriangleIndices.Add(c);
        m.TriangleIndices.Add(a); m.TriangleIndices.Add(c); m.TriangleIndices.Add(d);
        // mặt sau (để không phụ thuộc chiều pháp tuyến)
        m.TriangleIndices.Add(a); m.TriangleIndices.Add(c); m.TriangleIndices.Add(b);
        m.TriangleIndices.Add(a); m.TriangleIndices.Add(d); m.TriangleIndices.Add(c);
    }

    private void UpdateCamera()
    {
        double yaw = _yaw * Math.PI / 180, pitch = _pitch * Math.PI / 180;
        var dir = new Vector3D(Math.Cos(pitch) * Math.Cos(yaw), Math.Cos(pitch) * Math.Sin(yaw), Math.Sin(pitch));
        var pos = _target + dir * _dist;
        foreach (var cam in new[] { camera3D, diaCamera })
        {
            if (cam == null) continue;
            cam.Position = pos;
            cam.LookDirection = _target - pos;
            cam.UpDirection = new Vector3D(0, 0, 1);
            cam.NearPlaneDistance = Math.Max(0.05, _dist / 1000);
            cam.FarPlaneDistance = _dist * 20;
        }
    }

    // ------------------------------------------------------------------
    // Tô sáng + chọn bằng chuột
    // ------------------------------------------------------------------
    partial void UpdateHighlight3D()
    {
        _need3DRebuild = true;
        RedrawSecondaryViews();
    }

    /// <summary>Tìm cấu kiện dưới con trỏ trong một Viewport3D.</summary>
    private static object Pick3D(Viewport3D vp, Point pt, Dictionary<GeometryModel3D, PickMesh> map)
    {
        object hit = null;
        try
        {
            VisualTreeHelper.HitTest(vp, null, r =>
            {
                if (r is RayMeshGeometry3DHitTestResult rm && rm.ModelHit is GeometryModel3D gm && map.TryGetValue(gm, out var pm))
                {
                    hit = pm.Find(rm.VertexIndex1);
                    if (hit != null) return HitTestResultBehavior.Stop;
                }
                return HitTestResultBehavior.Continue;
            }, new PointHitTestParameters(pt));
        }
        catch (Exception) { hit = null; }
        return hit;
    }

    /// <summary>Chọn đối tượng trả về từ bảng tra (thanh / ô sàn / móng); null = bỏ chọn.</summary>
    private void SelectObject(object o)
    {
        switch (o)
        {
            case FrameElement f: Select(frame: f); break;
            case Slab s: Select(slab: s); break;
            case FootingDesign fd: Select(footingNode: fd.NodeId); break;
            default: if (HasSelection) ClearSelection(); break;
        }
    }

    private void View3D_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _rotHost = sender as FrameworkElement ?? view3DHost;
        _rotating = true; _rotMoved = false;
        _rotStart = e.GetPosition(_rotHost);
        _rotYaw0 = _yaw; _rotPitch0 = _pitch;
        _rotHost.CaptureMouse();
    }

    private void View3D_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_rotating || _rotHost == null) return;
        var p = e.GetPosition(_rotHost);
        if (!_rotMoved && Math.Abs(p.X - _rotStart.X) + Math.Abs(p.Y - _rotStart.Y) < 4) return;
        _rotMoved = true;
        _yaw = _rotYaw0 - (p.X - _rotStart.X) * 0.4;
        _pitch = Math.Clamp(_rotPitch0 + (p.Y - _rotStart.Y) * 0.4, -89, 89);
        UpdateCamera();
    }

    private void View3D_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_rotating) return;
        _rotating = false;
        var host = _rotHost ?? view3DHost;
        host.ReleaseMouseCapture();
        if (_rotMoved) return;
        bool dia = ReferenceEquals(host, diaView3DHost);
        var vp = dia ? diaViewport : viewport3D;
        SelectObject(Pick3D(vp, e.GetPosition(vp), dia ? _pickDia : _pick3D));
    }

    private void View3D_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _dist = Math.Clamp(_dist * (e.Delta > 0 ? 1 / 1.15 : 1.15), 0.5, 5000);
        UpdateCamera();
        e.Handled = true;
    }
}

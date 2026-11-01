using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using KetCauVXT.Model;
// System.Windows.Media.Media3D cũng có kiểu Material → bí danh tránh CS0104
using Material = KetCauVXT.Model.Material;

namespace KetCauVXT.App;

/// <summary>Hình 3D đơn giản: mỗi thanh là một hộp theo kích thước tiết diện, sàn là tấm mỏng.
/// Kéo chuột trái để xoay quanh tâm mô hình, con lăn để thu phóng.</summary>
public partial class MainWindow
{
    private double _yaw = -45, _pitch = 30, _dist = 50;
    private Point3D _target = new(0, 0, 0);
    private bool _rotating;
    private Point _rotStart;
    private double _rotYaw0, _rotPitch0;

    private void Build3D()
    {
        var group = new Model3DGroup();
        group.Children.Add(new AmbientLight(Color.FromRgb(90, 90, 90)));
        group.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-0.4, 0.6, -1)));
        group.Children.Add(new DirectionalLight(Color.FromRgb(120, 120, 120), new Vector3D(0.5, -0.3, 0.6)));

        if (_p != null && _p.Nodes.Count > 0)
        {
            var colMesh = new MeshGeometry3D();
            var beamMesh = new MeshGeometry3D();
            var steelMesh = new MeshGeometry3D();
            var selMesh = new MeshGeometry3D();
            var slabMesh = new MeshGeometry3D();
            var nodes = _p.Nodes.ToDictionary(n => n.Id);

            foreach (var f in _p.Frames)
            {
                if (!nodes.TryGetValue(f.NodeI, out var ni) || !nodes.TryGetValue(f.NodeJ, out var nj)) continue;
                var sec = _p.FrameSections.FirstOrDefault(s => s.Name == f.Section);
                double b = 0.2, h = 0.3;
                if (sec != null) { var sp = sec.Props(0.5); b = Math.Max(sp.Width, 0.05); h = Math.Max(sp.Depth, 0.05); }
                var mesh = _sel != null && _sel.Id == f.Id ? selMesh : f.IsVerticalKind ? (f.IsConcrete ? colMesh : steelMesh) : (f.IsConcrete ? beamMesh : steelMesh);
                // dầm BTCT: đỉnh dầm ngang mặt sàn → dời trục xuống h/2
                double drop = f.Kind == FrameKind.Beam ? h / 2 : 0;
                AddMemberBox(mesh, ni.P, nj.P, f.Angle, b, h, drop);
            }
            foreach (var s in _p.Slabs)
            {
                var pts = s.Nodes.Where(id => nodes.ContainsKey(id)).Select(id => nodes[id].P).ToList();
                if (pts.Count < 3) continue;
                double t = _p.SlabSections.FirstOrDefault(q => q.Name == s.Section)?.Thickness ?? 0.1;
                AddSlab(slabMesh, pts, t);
            }
            AddModel(group, colMesh, Color.FromRgb(170, 170, 180));
            AddModel(group, beamMesh, Color.FromRgb(90, 140, 220));
            AddModel(group, steelMesh, Color.FromRgb(230, 140, 40));
            AddModel(group, selMesh, Color.FromRgb(255, 0, 200));
            AddModel(group, slabMesh, Color.FromArgb(110, 150, 210, 150), true);

            double x0 = _p.Nodes.Min(n => n.X), x1 = _p.Nodes.Max(n => n.X);
            double y0 = _p.Nodes.Min(n => n.Y), y1 = _p.Nodes.Max(n => n.Y);
            double z0 = _p.Nodes.Min(n => n.Z), z1 = _p.Nodes.Max(n => n.Z);
            _target = new Point3D((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2);
            double size = Math.Max(Math.Max(x1 - x0, y1 - y0), z1 - z0);
            if (_dist <= 0 || _dist > 20 * Math.Max(size, 1) || _dist < 0.2 * Math.Max(size, 1)) _dist = Math.Max(size, 5) * 2.2;
        }
        model3DVisual.Content = group;
        UpdateCamera();
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

    /// <summary>Hộp chữ nhật theo trục thanh: trục 2 (cao h), trục 3 (rộng b) như hệ trục địa phương của chương trình.</summary>
    private static void AddMemberBox(MeshGeometry3D m, Vec3 pi, Vec3 pj, double angleDeg, double b, double h, double drop)
    {
        var e1 = (pj - pi).Normalize();
        Vec3 e2, e3;
        double horiz = Math.Sqrt(e1.X * e1.X + e1.Y * e1.Y);
        if (horiz < 1e-3) { e2 = Vec3.UnitX; e3 = e1.Cross(e2).Normalize(); e2 = e3.Cross(e1).Normalize(); }
        else { e3 = e1.Cross(Vec3.UnitZ).Normalize(); e2 = e3.Cross(e1).Normalize(); }
        if (Math.Abs(angleDeg) > 1e-9)
        {
            double a = angleDeg * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            var n2 = e2 * c + e3 * s; var n3 = e2 * (-s) + e3 * c;
            e2 = n2; e3 = n3;
        }
        var off = e2 * (-drop);
        var p0 = pi + off; var p1 = pj + off;
        var c4 = new[] { e2 * (h / 2) + e3 * (b / 2), e2 * (h / 2) - e3 * (b / 2), e2 * (-h / 2) - e3 * (b / 2), e2 * (-h / 2) + e3 * (b / 2) };
        int baseIdx = m.Positions.Count;
        foreach (var c in c4) m.Positions.Add(P(p0 + c));
        foreach (var c in c4) m.Positions.Add(P(p1 + c));
        // 4 mặt bên + 2 đầu
        for (int k = 0; k < 4; k++)
        {
            int a0 = baseIdx + k, a1 = baseIdx + (k + 1) % 4, b0 = a0 + 4, b1 = a1 + 4;
            Quad(m, a0, a1, b1, b0);
        }
        Quad(m, baseIdx + 3, baseIdx + 2, baseIdx + 1, baseIdx + 0);
        Quad(m, baseIdx + 4, baseIdx + 5, baseIdx + 6, baseIdx + 7);
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
        camera3D.Position = pos;
        camera3D.LookDirection = _target - pos;
        camera3D.UpDirection = new Vector3D(0, 0, 1);
        camera3D.NearPlaneDistance = Math.Max(0.05, _dist / 1000);
        camera3D.FarPlaneDistance = _dist * 20;
    }

    private void View3D_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _rotating = true;
        _rotStart = e.GetPosition(view3DHost);
        _rotYaw0 = _yaw; _rotPitch0 = _pitch;
        view3DHost.CaptureMouse();
    }

    private void View3D_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_rotating) return;
        var p = e.GetPosition(view3DHost);
        _yaw = _rotYaw0 - (p.X - _rotStart.X) * 0.4;
        _pitch = Math.Clamp(_rotPitch0 + (p.Y - _rotStart.Y) * 0.4, -89, 89);
        UpdateCamera();
    }

    private void View3D_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _rotating = false;
        view3DHost.ReleaseMouseCapture();
    }

    private void View3D_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _dist = Math.Clamp(_dist * (e.Delta > 0 ? 1 / 1.15 : 1.15), 0.5, 5000);
        UpdateCamera();
        e.Handled = true;
    }
}

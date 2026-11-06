using KetCauVXT.Model;

namespace KetCauVXT.Mep;

/// <summary>Khối 3D đơn giản của điện nước (để khung nhìn 3D, IFC / OBJ, kiểm tra va chạm dùng):
/// thiết bị = hộp theo kích thước họ (hướng theo tường); tuyến = các đoạn thẳng có đường kính.</summary>
public static class MepSolids
{
    /// <summary>Hộp thiết bị: tâm (m), trục dọc (Ux, Uy) trên mặt bằng, kích thước W (dọc), D (sâu), H (cao), màu hệ thống, tên.</summary>
    public readonly record struct Box(int DeviceId, double Cx, double Cy, double Cz, double Ux, double Uy, double W, double D, double H, string Color, string Name);
    /// <summary>Đoạn ống / ống gen: 2 đầu (m), đường kính (m), màu, Id tuyến, nằm trong tường (Id) hay ngoài (0).</summary>
    public readonly record struct Segment(int RunId, double X1, double Y1, double Z1, double X2, double Y2, double Z2, double Dia, string Color, int WallId);

    public static List<Box> Boxes(Project p)
    {
        var m = p.Mep; var res = new List<Box>();
        if (m == null) return res;
        foreach (var d in m.Devices)
        {
            var f = m.FamilyOf(d);
            var pos = MepRouting.DevicePos(p, d);
            double ux = 1, uy = 0, cx = pos.X, cy = pos.Y, dep = f?.D ?? 0.05;
            if (pos.Wall != null)
            {
                double L = Math.Max(1e-9, pos.Wall.Chord);
                ux = (pos.Wall.X2 - pos.Wall.X1) / L; uy = (pos.Wall.Y2 - pos.Wall.Y1) / L;
            }
            var sys = f?.Connectors.FirstOrDefault()?.System ?? MepSystemKind.DienOCam;
            double h = f?.H ?? 0.1;
            double cz = f?.Ceiling == true ? pos.Z - h / 2 : pos.Z;
            res.Add(new Box(d.Id, cx, cy, cz, ux, uy, f?.W ?? 0.1, dep, h, MepSystems.Color(sys), d.Family));
        }
        return res;
    }

    public static List<Segment> Segments(Project p)
    {
        var m = p.Mep; var res = new List<Segment>();
        if (m == null) return res;
        foreach (var r in m.Runs)
        {
            double dia = (r.Kind == MepRunKind.Ong ? r.Size : (r.ConduitSize > 0 ? r.ConduitSize : MepSizing.ConduitFor(r.Size, Math.Max(1, r.Conductors)))) / 1000;
            for (int i = 1; i < r.Path.Count; i++)
            {
                var a = r.Path[i - 1]; var b = r.Path[i];
                res.Add(new Segment(r.Id, a.X, a.Y, a.Z, b.X, b.Y, b.Z, dia, MepSystems.Color(r.System), b.WallId));
            }
        }
        return res;
    }
}

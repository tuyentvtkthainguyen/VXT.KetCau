using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Mep;

/// <summary>Một phần chiếm chỗ của điện nước trong tường: rãnh đục cho ống / ống gen chôn tường, hoặc hộp thiết bị âm tường.</summary>
public sealed class WallChase
{
    public int WallId { get; set; }
    public int RunId { get; set; }
    public int DeviceId { get; set; }
    public string Story { get; set; } = "";
    /// <summary>"Rãnh" | "Hộp"</summary>
    public string Kind { get; set; } = "Rãnh";
    /// <summary>Ø ống / ống gen (mm) – rãnh</summary>
    public double Dia { get; set; }
    /// <summary>Chiều dài rãnh (m); hộp: 0</summary>
    public double Length { get; set; }
    /// <summary>Tiết diện rãnh / kích thước hộp (m)</summary>
    public double Width { get; set; }
    public double Depth { get; set; }
    public double BoxH { get; set; }
    /// <summary>Thể tích khối xây bị chiếm (m³)</summary>
    public double Volume { get; set; }
    /// <summary>Diện tích trát vá (m²)</summary>
    public double PatchArea { get; set; }
}

/// <summary>Phần chiếm chỗ trong tường của ống / ống gen / hộp điện nước chôn tường (mặc định chôn ngầm – MepRouting):
/// <list type="bullet">
/// <item>Rãnh: rộng = Ø + 20 mm, sâu = Ø + 10 mm (không quá lõi tường), dài = chiều dài đoạn tuyến nằm trong tường;
/// thể tích = dài × rộng × sâu; trát vá = dài × (rộng + 2 × 50 mm).</item>
/// <item>Hộp âm tường (ổ cắm, công tắc, hộp chia, aptomat, tủ điện): thể tích = W × H × min(D; lõi tường); trát vá = (W + 0,1) × (H + 0,1).</item>
/// </list>
/// Khối xây tường trừ thể tích chiếm chỗ (QuantityCalculator); tải tường KHÔNG đổi (phần trừ rất nhỏ, thiên về an toàn).</summary>
public static class WallChases
{
    public const double PatchMargin = 0.05;

    public static double ChaseWidth(double diaMm) => diaMm / 1000 + 0.02;
    public static double ChaseDepth(double diaMm) => diaMm / 1000 + 0.01;

    private static readonly MepCategory[] FlushBoxes = { MepCategory.OCam, MepCategory.CongTac, MepCategory.HopChia, MepCategory.Aptomat, MepCategory.TuDien };

    /// <summary>Danh sách phần chiếm chỗ (rãnh + hộp) của toàn mô hình.</summary>
    public static List<WallChase> Compute(Project p)
    {
        var res = new List<WallChase>();
        var m = p?.Mep;
        if (m == null || m.IsEmpty) return res;
        foreach (var r in m.Runs)
        {
            var path = r.Path;
            if (path == null || path.Count < 2) continue;
            if (MepSystems.IsDrain(r.System) && !r.DrainInWall) continue;   // thoát nước đi dưới sàn – không đục tường
            double dia = StructuralImpact.RunDia(r);
            if (dia <= 0) continue;
            foreach (var g in Segments(path).GroupBy(s => s.WallId))
            {
                var w = p.Walls.FirstOrDefault(x => x.Id == g.Key);
                if (w == null) continue;
                double core = CoreOf(p, w);
                double L = g.Sum(s => s.L);
                if (L < 1e-6) continue;
                double wd = ChaseWidth(dia), dp = Math.Min(ChaseDepth(dia), core > 0 ? core : ChaseDepth(dia));
                res.Add(new WallChase
                {
                    WallId = w.Id, RunId = r.Id, Story = r.Story, Kind = "Rãnh", Dia = dia, Length = L, Width = wd, Depth = dp,
                    Volume = L * wd * dp, PatchArea = L * (wd + 2 * PatchMargin)
                });
            }
        }
        foreach (var d in m.Devices.Where(d => d.WallId > 0 && !d.Exposed))
        {
            var fam = m.FamilyOf(d);
            if (fam == null || !FlushBoxes.Contains(fam.Category)) continue;
            var w = p.Walls.FirstOrDefault(x => x.Id == d.WallId);
            if (w == null) continue;
            double core = CoreOf(p, w);
            double dp = Math.Min(fam.D, core > 0 ? core : fam.D);
            res.Add(new WallChase
            {
                WallId = w.Id, DeviceId = d.Id, Story = d.Story, Kind = "Hộp", Width = fam.W, BoxH = fam.H, Depth = dp,
                Volume = fam.W * fam.H * dp, PatchArea = (fam.W + 2 * PatchMargin) * (fam.H + 2 * PatchMargin)
            });
        }
        return res;
    }

    /// <summary>Các đoạn tuyến nằm trong tường: đoạn có điểm cuối gắn tường (WallId &gt; 0) – cùng quy ước với StructuralImpact (rãnh tường).</summary>
    public static IEnumerable<(int WallId, double L)> Segments(List<MepPt> path)
    {
        for (int i = 1; i < path.Count; i++)
        {
            var a = path[i - 1]; var c = path[i];
            if (c.WallId <= 0) continue;
            double dx = c.X - a.X, dy = c.Y - a.Y, dz = c.Z - a.Z;
            yield return (c.WallId, Math.Sqrt(dx * dx + dy * dy + dz * dz));
        }
    }

    /// <summary>Các đoạn rãnh chôn tường (toạ độ 3D đầu – cuối, Ø mm, tường chủ) – dùng vẽ mặt bằng / 3D.</summary>
    public static List<(MepRun Run, MepPt A, MepPt B, double Dia, int WallId)> ChaseSegments(Project p)
    {
        var res = new List<(MepRun, MepPt, MepPt, double, int)>();
        var m = p?.Mep;
        if (m == null || m.IsEmpty) return res;
        foreach (var r in m.Runs)
        {
            var path = r.Path;
            if (path == null || path.Count < 2) continue;
            if (MepSystems.IsDrain(r.System) && !r.DrainInWall) continue;
            double dia = StructuralImpact.RunDia(r);
            if (dia <= 0) continue;
            for (int i = 1; i < path.Count; i++)
            {
                var a = path[i - 1]; var c = path[i];
                if (c.WallId <= 0 || p.Walls.All(x => x.Id != c.WallId)) continue;
                res.Add((r, a, c, dia, c.WallId));
            }
        }
        return res;
    }

    /// <summary>Hạng mục khối 3D rãnh tường (cùng tên với SectionView.ChaseCategory).</summary>
    public const string SolidCategory = "Rãnh tường";

    /// <summary>Khối 3D mảnh thể hiện rãnh đục tường (bề rộng rãnh) – Id "RANH-{tuyến}-{i}", HostId = tường chủ.
    /// Không đăng ký vào ModelSolids.Providers (không xuất IFC / không kiểm tra va chạm) – 3D của chương trình gọi trực tiếp.</summary>
    public static List<Solid> Solids(Project p)
    {
        var res = new List<Solid>();
        int k = 0;
        foreach (var (r, a, c, dia, wid) in ChaseSegments(p))
        {
            k++;
            double wd = ChaseWidth(dia);
            double dx = c.X - a.X, dy = c.Y - a.Y, dz = c.Z - a.Z, lh = Math.Sqrt(dx * dx + dy * dy);
            if (lh < 1e-6 && Math.Abs(dz) < 1e-6) continue;
            Solid so;
            if (lh >= 1e-6)
            {
                var x = new Vec3(dx / lh, dy / lh, 0);
                so = new Solid { Origin = new Vec3(a.X, a.Y, Math.Min(a.Z, c.Z) - wd / 2), XDir = x, ZDir = Vec3.UnitZ,
                    Profile = Solid.Rect(0, -wd / 2, lh, wd / 2), Depth = wd + Math.Abs(dz) };
            }
            else
            {
                so = new Solid { Origin = new Vec3(a.X, a.Y, Math.Min(a.Z, c.Z)), XDir = Vec3.UnitX, ZDir = Vec3.UnitZ,
                    Profile = Solid.Rect(-wd / 2, -wd / 2, wd / 2, wd / 2), Depth = Math.Abs(dz) };
            }
            so.Id = $"RANH-{r.Id}-{k}"; so.Name = $"Rãnh Ø{dia:0} (tuyến {r.Id})"; so.Category = SolidCategory; so.Discipline = Discipline.DienNuoc;
            so.HostId = $"TUONG-{wid}"; so.Story = r.Story; so.IfcType = "IfcBuildingElementProxy"; so.Color = new[] { 0.95, 0.55, 0.15 };
            res.Add(so);
        }
        return res;
    }

    /// <summary>Vẽ rãnh điện nước chôn tường trên mặt bằng (nét đứt mảnh, mm) cho tầng; trả về số đoạn đã vẽ.</summary>
    public static int DrawPlan(KetCauVXT.Drawing.DxfWriter w, Project p, string story)
    {
        int n = 0;
        foreach (var (r, a, c, dia, _) in ChaseSegments(p))
        {
            if (story != null && r.Story != story) continue;
            double lh = Math.Sqrt((c.X - a.X) * (c.X - a.X) + (c.Y - a.Y) * (c.Y - a.Y));
            if (lh < 1e-6) { w.Circle(KetCauVXT.Drawing.Lay.KyHieu, c.X * 1000, c.Y * 1000, Math.Max(dia / 2, 15)); n++; continue; }   // đoạn đứng
            w.Line(KetCauVXT.Drawing.Lay.KyHieu, a.X * 1000, a.Y * 1000, c.X * 1000, c.Y * 1000, "DASHED");
            n++;
        }
        return n;
    }

    private static double CoreOf(Project p, WallLine w)
    {
        try { return MepRouting.WallInfo(p, w).Core; } catch { return 0; }
    }

    /// <summary>Tổng thể tích chiếm chỗ theo tường (m³).</summary>
    public static Dictionary<int, double> VolumeByWall(IEnumerable<WallChase> list) =>
        list.GroupBy(c => c.WallId).ToDictionary(g => g.Key, g => g.Sum(c => c.Volume));

    /// <summary>Trừ thể tích chiếm chỗ khỏi khối xây (giữ ≥ 0, gạch / vữa xây tỉ lệ theo thể tích) và thêm công tác
    /// "Đục rãnh tường (m)", "Trát vá rãnh (m²)", "Đục lỗ hộp âm tường (cái)" vào q.MepWallWorks. Trả về tổng thể tích đã trừ.</summary>
    public static double ApplyToQuantities(Project p, QuantityResult q)
    {
        if (p == null || q == null) return 0;
        var list = Compute(p);
        q.MepWallWorks.Clear();
        if (list.Count == 0) return 0;
        double total = 0;
        foreach (var (wid, v) in VolumeByWall(list))
        {
            var mi = q.Masonry.FirstOrDefault(x => x.WallId == wid);
            if (mi == null || mi.Volume <= 1e-12) continue;
            double dv = Math.Min(v, mi.Volume);
            double k = (mi.Volume - dv) / mi.Volume;
            mi.Volume -= dv; mi.Bricks *= k; mi.Mortar *= k;
            mi.MepVoid = dv;
            total += dv;
        }
        foreach (var g in list.Where(c => c.Kind == "Rãnh").GroupBy(c => (c.Story, c.Dia)).OrderBy(g => g.Key.Story).ThenBy(g => g.Key.Dia))
        {
            double L = g.Sum(c => c.Length);
            q.MepWallWorks.Add(new GroundWorkItem
            {
                Story = g.Key.Story, Member = "Điện nước", Kind = "Đục rãnh", Work = $"Đục rãnh tường chôn ống Ø{g.Key.Dia:0}", Unit = "m",
                Quantity = Math.Round(L, 3), Dims = $"rãnh {g.First().Width * 1000:0}×{g.First().Depth * 1000:0} mm", Note = "trừ khối xây " + Math.Round(g.Sum(c => c.Volume), 4) + " m³"
            });
            q.MepWallWorks.Add(new GroundWorkItem
            {
                Story = g.Key.Story, Member = "Điện nước", Kind = "Trát vá", Work = $"Trát vá rãnh ống Ø{g.Key.Dia:0}", Unit = "m²",
                Quantity = Math.Round(g.Sum(c => c.PatchArea), 3), Dims = $"dài {L:0.##} m × rộng {(g.First().Width + 2 * PatchMargin) * 1000:0} mm"
            });
        }
        foreach (var g in list.Where(c => c.Kind == "Hộp").GroupBy(c => c.Story))
        {
            q.MepWallWorks.Add(new GroundWorkItem
            {
                Story = g.Key, Member = "Điện nước", Kind = "Đục rãnh", Work = "Đục lỗ đặt hộp âm tường (ổ cắm, công tắc, hộp chia, tủ điện)", Unit = "cái",
                Count = g.Count(), Quantity = g.Count(), Note = "trừ khối xây " + Math.Round(g.Sum(c => c.Volume), 4) + " m³"
            });
            q.MepWallWorks.Add(new GroundWorkItem
            {
                Story = g.Key, Member = "Điện nước", Kind = "Trát vá", Work = "Trát vá quanh hộp âm tường", Unit = "m²", Quantity = Math.Round(g.Sum(c => c.PatchArea), 3)
            });
        }
        q.Notes.Add($"Khối xây tường đã trừ phần chiếm chỗ của ống / ống gen / hộp điện nước chôn tường: {total:0.####} m³ (rãnh rộng Ø+20, sâu Ø+10 mm; hộp W×H×sâu). Tải tường giữ nguyên (phần trừ nhỏ, thiên về an toàn).");
        return total;
    }

    /// <summary>Bảng công tác đục rãnh / trát vá (Excel trang xây – trát, Word).</summary>
    public static QtyTable Table(QuantityResult q)
    {
        var t = new QtyTable
        {
            Title = "Đục rãnh tường, trát vá rãnh điện nước (ống / ống gen / hộp chôn tường)",
            Headers = new[] { "STT", "Tầng", "Công tác", "Diễn giải", "Đơn vị", "Khối lượng", "Ghi chú" },
            Widths = new double[] { 6, 10, 40, 30, 8, 12, 24 },
            WordWidths = new[] { 0.9, 1.2, 4.4, 3.6, 1.2, 1.6, 2.6 }
        };
        int k = 1;
        foreach (var x in q.MepWallWorks)
            t.Add(k++, x.Story, x.Work, x.Dims, x.Unit, x.Unit == "cái" ? (object)(long)Math.Round(x.Quantity) : Math.Round(x.Quantity, 3), x.Note);
        foreach (var g in q.MepWallWorks.GroupBy(x => (x.Kind, x.Unit)))
            t.AddTotal(null, null, "Tổng " + g.Key.Kind.ToLowerInvariant() + " (" + g.Key.Unit + ")", null, g.Key.Unit,
                g.Key.Unit == "cái" ? (object)(long)Math.Round(g.Sum(x => x.Quantity)) : Math.Round(g.Sum(x => x.Quantity), 3), null);
        return t;
    }
}

using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

/// <summary>Kết quả kiểm tra chiều cao một dầm đỡ ô sàn hạ cốt (nhịp bất lợi nhất của dải dầm).</summary>
public class SlabDropBeamCheck
{
    public string Beam { get; set; } = "";
    public string Story { get; set; } = "";
    /// <summary>Nhịp (1, 2…) bất lợi nhất.</summary>
    public int Span { get; set; }
    /// <summary>Ô sàn hạ cốt / ô sàn phía bên kia (rỗng = dầm biên, không có sàn).</summary>
    public string LowSlab { get; set; } = "";
    public string HighSlab { get; set; } = "";
    /// <summary>hs1 (ô cao / ô hạ khi là dầm biên), hs2 (ô hạ), Δ = độ hạ của ô thấp so với mặt dầm – mm.</summary>
    public double Hs1 { get; set; }
    public double Hs2 { get; set; }
    public double Drop { get; set; }
    /// <summary>Chiều cao yêu cầu h ≥ hs1 + Δ + hs2 và chiều cao dầm (mm).</summary>
    public double HReq { get; set; }
    public double H { get; set; }
    /// <summary>Chiều cao đề nghị (làm tròn lên bội 50 mm).</summary>
    public double Suggested { get; set; }
    public bool Ok => H >= HReq - 0.5;
}

/// <summary>Quy tắc kết cấu cho SÀN HẠ CỐT (Slab.LevelOffset – Model/SlabDrop.cs):
/// <list type="bullet">
/// <item>Bậc sàn: hai ô kề nhau chênh cốt &gt; SlabDropSettings.StepThreshold (mặc định 30 mm) → cạnh chung KHÔNG liên tục mô men
/// (tấm bị gãy tại bậc, thép mũ không thể vắt thẳng qua). SlabDesigner coi cạnh đó là khớp khi tính nội lực ô (thiên về an toàn cho
/// mô men nhịp), mỗi ô đặt thép mũ cấu tạo riêng (≥ ⅓ thép nhịp, ≥ hàm lượng tối thiểu, a ≤ 200) vươn lx/4 và neo qua dầm;
/// lưới trên liên tục cũng dừng tại bậc. Cạnh do người dùng khai báo ngàm (Slab.EdgeFixed) được giữ ngàm nhưng thép mũ vẫn tách riêng.</item>
/// <item>Dầm đỡ ô hạ cốt: mặt dưới ô hạ nằm thấp hơn mặt dầm Δ + hs2; dưới đó dầm cần thêm tối thiểu một chiều dày sàn hs1 để neo
/// thép sàn và bố trí cốt đai → h ≥ hs1 + Δ + hs2 (dầm biên không có sàn phía kia: hs1 = hs2). Không đạt → CẢNH BÁO kèm chiều cao đề nghị
/// (không chặn thiết kế, không đổi trạng thái đạt/không đạt của dầm).</item>
/// <item>Tải trọng không đổi: ô hạ cốt vẫn truyền tải về các dầm đỡ (lớp vữa tạo dốc / bù cốt đã nằm trong cấu tạo sàn vệ sinh).</item>
/// </list></summary>
public static class SlabDropChecks
{
    /// <summary>Ngưỡng bậc (m).</summary>
    public static double Threshold(Project p) => Math.Max(0, SlabDropSettings.Of(p).StepThreshold);

    /// <summary>Hai ô sàn chênh cốt quá ngưỡng (bậc sàn).</summary>
    public static bool IsStep(Project p, Slab a, Slab b) =>
        a != null && b != null && a.LevelOffset != b.LevelOffset && Math.Abs(a.LevelOffset - b.LevelOffset) > Threshold(p) + 1e-9;

    private static bool OnLine(Vec3 a, Vec3 u, Vec3 q, out double t)
    {
        var d = q - a;
        t = d.Dot(u);
        return (d - u * t).Length < 0.01;
    }

    /// <summary>Cạnh i (pts[i]→pts[i+1]) của ô slab giáp một ô khác cùng cao độ nút nhưng chênh cốt (bậc): trả mảng 4 cờ
    /// và tên ô kề từng cạnh bậc (null nếu không phải bậc).</summary>
    internal static bool[] StepEdges(Project p, Slab slab, List<Vec3> pts, out string[] neighbours)
    {
        int n = pts.Count;
        var res = new bool[n];
        neighbours = new string[n];
        if (p?.Slabs == null || slab == null) return res;
        for (int i = 0; i < n; i++)
        {
            Vec3 a = pts[i], b = pts[(i + 1) % n];
            var u = b - a; double le = u.Length;
            if (le < 1e-6) continue;
            u = u.Normalize();
            foreach (var o in p.Slabs)
            {
                if (o.Id == slab.Id || o.Nodes.Count < 3 || !IsStep(p, slab, o)) continue;
                List<Vec3> op;
                try { op = o.Nodes.Select(id => p.Node(id).P).ToList(); } catch (KeyNotFoundException) { continue; }
                bool hit = false;
                for (int j = 0; j < op.Count && !hit; j++)
                {
                    Vec3 c = op[j], dd = op[(j + 1) % op.Count];
                    if (Math.Abs(c.Z - a.Z) > 0.01 || Math.Abs(dd.Z - a.Z) > 0.01) continue;
                    if (!OnLine(a, u, c, out double t1) || !OnLine(a, u, dd, out double t2)) continue;
                    double lo = Math.Max(0, Math.Min(t1, t2)), hi = Math.Min(le, Math.Max(t1, t2));
                    if (hi - lo > 0.1 * le) hit = true;
                }
                if (!hit) continue;
                res[i] = true;
                neighbours[i] = string.IsNullOrEmpty(o.Label) ? "S" + o.Id.ToString(DesignUtil.Inv) : o.Label;
                break;
            }
        }
        return res;
    }

    /// <summary>Kiểm tra h ≥ hs1 + Δ + hs2 cho mọi dải dầm có ô sàn hạ cốt hai bên / một bên (nhịp bất lợi nhất mỗi dải).</summary>
    public static List<SlabDropBeamCheck> BeamChecks(Project p, DesignSummary s)
    {
        var res = new List<SlabDropBeamCheck>();
        if (p == null || s?.Beams == null || !p.Slabs.Any(SlabDrop.IsLowered)) return res;
        double thrMm = Threshold(p) * 1000;
        var byId = p.Slabs.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var d in s.Beams)
        {
            List<BeamSlabSection> secs;
            try { secs = BeamSlabContext.PerSpan(p, d, null); } catch (Exception) { continue; }
            SlabDropBeamCheck worst = null;
            for (int i = 0; i < secs.Count; i++)
            {
                var sides = new[] { secs[i].Left, secs[i].Right }.Where(x => x != null && x.Hs > 0 && byId.ContainsKey(x.SlabId))
                    .Select(x => (Info: x, Slab: byId[x.SlabId], Dz: byId[x.SlabId].LevelOffset * 1000)).ToList();
                if (sides.Count == 0 || !sides.Any(x => x.Dz < -Math.Max(thrMm, 0.5))) continue;
                var ord = sides.OrderBy(x => x.Dz).ToList();
                var low = ord[0];
                var high = ord.Count > 1 ? ord[^1] : default;
                bool two = ord.Count > 1;
                double hs2 = low.Info.Hs, hs1 = two ? high.Info.Hs : low.Info.Hs, drop = -low.Dz;
                double req = hs1 + drop + hs2;
                double h = d.H * 1000;
                var ck = new SlabDropBeamCheck
                {
                    Beam = d.Name, Story = d.Story ?? "", Span = i + 1, LowSlab = low.Info.Label, HighSlab = two ? high.Info.Label : "",
                    Hs1 = hs1, Hs2 = hs2, Drop = Math.Round(drop), HReq = Math.Round(req), H = Math.Round(h),
                    Suggested = Math.Ceiling(Math.Round(req) / 50 - 1e-9) * 50
                };
                if (worst == null || ck.HReq - ck.H > worst.HReq - worst.H) worst = ck;
            }
            if (worst != null) res.Add(worst);
        }
        return res;
    }

    /// <summary>Lời kiểm tra một dầm (tiếng Việt).</summary>
    public static string Text(SlabDropBeamCheck c)
    {
        string f = $"h ≥ hs1 + Δ + hs2 = {DesignUtil.F(c.Hs1, "0")} + {DesignUtil.F(c.Drop, "0")} + {DesignUtil.F(c.Hs2, "0")} = {DesignUtil.F(c.HReq, "0")} mm";
        string where = "ô " + c.LowSlab + (string.IsNullOrEmpty(c.HighSlab) ? " (dầm biên)" : " / ô " + c.HighSlab) + ", nhịp " + c.Span.ToString(DesignUtil.Inv);
        return c.Ok
            ? $"Dầm biên ô sàn hạ cốt ({where}): {f} ≤ h = {DesignUtil.F(c.H, "0")} mm – đạt"
            : $"CẢNH BÁO Dầm biên ô sàn hạ cốt ({where}): {f} > h = {DesignUtil.F(c.H, "0")} mm – đề nghị tăng chiều cao dầm lên h ≥ {DesignUtil.F(c.Suggested, "0")} mm (hoặc giảm độ hạ cốt)";
    }

    /// <summary>Ghi kết quả kiểm tra vào lời nhắn của dầm và cảnh báo chung (gọi cuối ConcreteDesigner.Design).</summary>
    public static void Apply(Project p, DesignSummary s)
    {
        foreach (var c in BeamChecks(p, s))
        {
            var d = s.Beams.FirstOrDefault(b => b.Name == c.Beam && (b.Story ?? "") == c.Story);
            string t = Text(c);
            if (d != null && !d.Messages.Contains(t)) d.Messages.Add(t);
            if (!c.Ok) s.Warnings.Add("Dầm " + c.Beam + ": " + t);
        }
    }
}

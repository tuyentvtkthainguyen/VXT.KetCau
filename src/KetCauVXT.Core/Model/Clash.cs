namespace KetCauVXT.Model;

// =====================================================================
// KIỂM TRA VA CHẠM GIỮA CÁC BỘ MÔN
// Dùng khối 3D chung (ModelSolids). Va chạm "Cứng": hai khối giao nhau sâu hơn dung sai; "Khe hở": khoảng cách < khe hở yêu cầu;
// "Ống lồng": ống/thiết bị cho phép xuyên (AllowSleeve) qua dầm/tường – cần đặt ống lồng, không phải lỗi thiết kế.
// Bỏ qua: cùng một cấu kiện, cột–dầm–sàn của kết cấu (liên kết nút), cửa với tường chủ, lỗ cửa, khối trong cùng bộ môn
// (trừ khi sameDiscipline = true).
// =====================================================================

public enum ClashType { Cung, KheHo, OngLong }

public sealed class ClashResult
{
    public Solid A { get; init; }
    public Solid B { get; init; }
    public ClashType Type { get; init; }
    /// <summary>Độ chồng lấn (m, ≥ 0) với va chạm cứng; khoảng cách (m) với khe hở</summary>
    public double Amount { get; init; }
    public Vec3 Location { get; init; }
    public string TypeText => Type switch { ClashType.Cung => "Cứng", ClashType.KheHo => "Khe hở", _ => "Ống lồng" };
    public override string ToString() =>
        $"{TypeText}: {DisciplineView.Title(A.Discipline)} {A.Category} {A.Name} ↔ {DisciplineView.Title(B.Discipline)} {B.Category} {B.Name} tại {Location} ({Amount * 1000:0} mm)";
}

public class ClashOptions
{
    /// <summary>Dung sai: chồng lấn nhỏ hơn giá trị này không tính (m)</summary>
    public double Tolerance { get; set; } = 0.005;
    /// <summary>Khe hở yêu cầu giữa các bộ môn (m), 0 = không kiểm tra khe hở</summary>
    public double Clearance { get; set; }
    /// <summary>Kiểm tra cả trong cùng bộ môn (trừ kết cấu–kết cấu)</summary>
    public bool SameDiscipline { get; set; }
}

public static class ClashDetector
{
    /// <summary>Quy tắc ống lồng (đăng ký bởi bộ môn điện nước – StructuralImpact): trả false nếu vị trí xuyên KHÔNG cho phép
    /// (gần gối, ngoài 1/3 giữa chiều cao, Ø &gt; h/3, ống đứng xuyên dầm…) → tính là va chạm cứng. null = mọi ống lồng đều cho phép.</summary>
    public static Func<Project, Solid, Solid, Vec3, bool> SleeveRule { get; set; }

    /// <summary>Kiểm tra va chạm trên toàn mô hình.</summary>
    public static List<ClashResult> Run(Project p, ClashOptions o = null) => Detect(ModelSolids.Build(p, includeVoids: false), o, p);

    /// <summary>Kiểm tra va chạm giữa các khối (hộp bao trục toạ độ, khối nghiêng lấy hộp bao). p ≠ null: áp quy tắc ống lồng (SleeveRule).</summary>
    public static List<ClashResult> Detect(IReadOnlyList<Solid> solids, ClashOptions o = null, Project p = null)
    {
        o ??= new ClashOptions();
        var res = new List<ClashResult>();
        var boxes = solids.Select(s => s.Box()).ToList();
        for (int i = 0; i < solids.Count; i++)
            for (int j = i + 1; j < solids.Count; j++)
            {
                var a = solids[i]; var b = solids[j];
                if (Skip(a, b, o)) continue;
                var (amin, amax) = boxes[i]; var (bmin, bmax) = boxes[j];
                double ox = Math.Min(amax.X, bmax.X) - Math.Max(amin.X, bmin.X);
                double oy = Math.Min(amax.Y, bmax.Y) - Math.Max(amin.Y, bmin.Y);
                double oz = Math.Min(amax.Z, bmax.Z) - Math.Max(amin.Z, bmin.Z);
                double overlap = Math.Min(ox, Math.Min(oy, oz));
                var loc = new Vec3((Math.Max(amin.X, bmin.X) + Math.Min(amax.X, bmax.X)) / 2,
                                   (Math.Max(amin.Y, bmin.Y) + Math.Min(amax.Y, bmax.Y)) / 2,
                                   (Math.Max(amin.Z, bmin.Z) + Math.Min(amax.Z, bmax.Z)) / 2);
                if (overlap > o.Tolerance)
                {
                    bool sleeve = (a.AllowSleeve && IsHostable(b)) || (b.AllowSleeve && IsHostable(a));
                    if (sleeve && p != null && SleeveRule != null)
                    {
                        try { sleeve = SleeveRule(p, a, b, loc); } catch { }
                    }
                    res.Add(new ClashResult { A = a, B = b, Type = sleeve ? ClashType.OngLong : ClashType.Cung, Amount = overlap, Location = loc });
                }
                else if (o.Clearance > 0)
                {
                    double gx = Math.Max(0, -ox), gy = Math.Max(0, -oy), gz = Math.Max(0, -oz);
                    double gap = Math.Sqrt(gx * gx + gy * gy + gz * gz);
                    if (gap < o.Clearance) res.Add(new ClashResult { A = a, B = b, Type = ClashType.KheHo, Amount = gap, Location = loc });
                }
            }
        return res;
    }

    private static bool IsHostable(Solid s) => s.Category is "Dầm" or "Tường" or "Sàn" or "Sàn nền";

    private static bool Skip(Solid a, Solid b, ClashOptions o)
    {
        if (a.IsVoid || b.IsVoid) return true;
        if (a.Id == b.Id) return true;
        if (a.HostId == b.Id || b.HostId == a.Id) return true;   // cửa ↔ tường chủ
        if (a.Discipline == Discipline.KetCau && b.Discipline == Discipline.KetCau) return true;   // nút khung
        if (a.Discipline == b.Discipline && !o.SameDiscipline) return true;
        // tường kiến trúc đứng trên dầm/sàn, kẹp giữa cột: tiếp xúc bình thường
        bool wallA = a.Category == "Tường", wallB = b.Category == "Tường";
        if ((wallA && b.Discipline == Discipline.KetCau) || (wallB && a.Discipline == Discipline.KetCau)) return true;
        bool slabLike(Solid s) => s.Category is "Sàn" or "Sàn nền" or "Trần" or "Mái";
        if ((slabLike(a) && b.Discipline == Discipline.KetCau) || (slabLike(b) && a.Discipline == Discipline.KetCau)) return true;
        return false;
    }
}

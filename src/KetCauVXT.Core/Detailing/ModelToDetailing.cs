using KetCauVXT.Model;

namespace KetCauVXT.Detailing;

// =====================================================================
// MÔ HÌNH (chế độ Thiết kế) → HỒ SƠ TRIỂN KHAI: LANH TÔ TỪ CỬA GẮN TƯỜNG; ÁNH XẠ KIẾN TRÚC TỪ "MỞ MẶT BẰNG CAD"
//  • Mỗi cửa gắn tường (WallLine.Openings – kiểu Revit) → một ô cửa DoorOpening (tâm, phương tường, bề rộng thông thuỷ, chiều dày
//    tường = tổng chiều dày kiểu tường, tường ngoài nhà theo khung bao tường của tầng) → DoorLintels.Place đặt lanh tô lên
//    "Mặt bằng lanh tô tầng X" của hồ sơ (gộp các ô cửa giống nhau (Lo làm tròn 50 mm, tường 110 / 220, loại cửa) thành LT1, LT2…,
//    SL = số ô cửa; ô cửa đã có lanh tô tại chỗ bỏ qua – chạy lại không nhân đôi). Quy tắc an toàn tường 110 do ManualLintel
//    (LintelDesigner) kiểm tra khi tính.
//  • Mặt bằng hồ sơ ứng với tầng mô hình: cùng tên → cùng cao độ (±5 cm) → tạo mới (tên tầng, cao độ tầng).
//  • Toạ độ: mô hình → mặt bằng hồ sơ dời sao cho trục đầu tiên trùng nhau (trục X, Y nhỏ nhất).
// =====================================================================
public static class ModelToDetailing
{
    /// <summary>Độ dời toạ độ mô hình → mặt bằng hồ sơ (m).</summary>
    public static (double Dx, double Dy) Shift(Project model, StructuralPlan plan)
    {
        double mx = model?.GridsX.Select(g => g.Coord).DefaultIfEmpty(0).Min() ?? 0;
        double my = model?.GridsY.Select(g => g.Coord).DefaultIfEmpty(0).Min() ?? 0;
        var xs = plan?.Xs ?? Array.Empty<double>(); var ys = plan?.Ys ?? Array.Empty<double>();
        return ((xs.Length > 0 ? xs.Min() : 0) - mx, (ys.Length > 0 ? ys.Min() : 0) - my);
    }

    /// <summary>Các ô cửa (cửa đi, cửa sổ, ô thoáng – không gồm lỗ chờ) gắn trên tường của tầng story, toạ độ đã dời (dx, dy).</summary>
    public static List<DoorOpening> Openings(Project model, string story, double dx = 0, double dy = 0)
    {
        var res = new List<DoorOpening>();
        if (model?.Walls == null) return res;
        var walls = model.Walls.Where(w => w.Story == story && !w.IsCurved && w.Chord > 1e-6).ToList();
        if (walls.Count == 0) return res;
        double wx0 = walls.Min(w => Math.Min(w.X1, w.X2)), wx1 = walls.Max(w => Math.Max(w.X1, w.X2));
        double wy0 = walls.Min(w => Math.Min(w.Y1, w.Y2)), wy1 = walls.Max(w => Math.Max(w.Y1, w.Y2));
        foreach (var w in walls)
        {
            var wt = model.WallTypes?.FirstOrDefault(t => t.Name == w.Type);
            double tw = wt?.TotalThickness ?? 0.22;
            if (tw <= 1e-6) tw = 0.22;
            double L = w.Chord, ux = (w.X2 - w.X1) / L, uy = (w.Y2 - w.Y1) / L;
            if (ux < -1e-9 || (Math.Abs(ux) < 1e-9 && uy < 0)) { ux = -ux; uy = -uy; }   // phương chuẩn hoá (giống DoorLintels.Detect)
            foreach (var o in w.Openings ?? new List<WallOpening>())
            {
                var ot = WallAssembly.TypeOf(model, o);
                if (ot == null || ot.Kind == WallOpeningKind.Hole || ot.Width <= 1e-6) continue;
                var (px, py) = WallAssembly.PointAt(w, Math.Clamp(o.Offset, 0, L));
                var d = new DoorOpening
                {
                    X = Math.Round(px + dx, 4), Y = Math.Round(py + dy, 4), Width = Math.Round(ot.Width, 3), Ux = ux, Uy = uy, WallT = Math.Round(tw, 3),
                    Kind = ot.Kind == WallOpeningKind.Window || ot.Kind == WallOpeningKind.Vent ? "Cửa sổ" : "Cửa đi", Block = ot.Name
                };
                // tường bao ngoài: tim tường nằm trên biên khung bao tường của tầng
                double tolE = Math.Max(tw, 0.11);
                if (Math.Abs(uy) < 1e-6 && Math.Abs(py - wy0) <= tolE) { d.Exterior = true; d.OutSide = -1; }
                else if (Math.Abs(uy) < 1e-6 && Math.Abs(py - wy1) <= tolE) { d.Exterior = true; d.OutSide = 1; }
                else if (Math.Abs(ux) < 1e-6 && Math.Abs(px - wx0) <= tolE) { d.Exterior = true; d.OutSide = 1; }
                else if (Math.Abs(ux) < 1e-6 && Math.Abs(px - wx1) <= tolE) { d.Exterior = true; d.OutSide = -1; }
                res.Add(d);
            }
        }
        return res;
    }

    /// <summary>Mặt bằng hồ sơ của tầng mô hình st: cùng tên → cùng cao độ (±5 cm) → tạo mới.</summary>
    public static PlanLevel LevelFor(StructuralPlan plan, Story st, bool create = true)
    {
        if (plan == null || st == null) return null;
        var lv = plan.Levels.FirstOrDefault(l => !l.IsFoundation && string.Equals(l.Name, st.Name, StringComparison.OrdinalIgnoreCase))
                 ?? plan.Levels.FirstOrDefault(l => !l.IsFoundation && Math.Abs(l.Elevation - st.Elevation) < 0.05);
        if (lv == null && create)
        {
            lv = new PlanLevel { Name = st.Name, Elevation = st.Elevation };
            plan.Levels.Add(lv);
        }
        return lv;
    }

    /// <summary>Đặt lanh tô cho mọi cửa gắn tường của mô hình lên các mặt bằng lanh tô của hồ sơ. Trả về nhật ký (một dòng mỗi tầng có cửa).</summary>
    public static List<string> LintelsFromModel(Project model, DetailingProject dp)
    {
        var log = new List<string>();
        if (model == null || dp == null) return log;
        var plan = dp.Plan ??= new StructuralPlan();
        var (dx, dy) = Shift(model, plan);
        int before = dp.Lintels.Count;
        foreach (var st in model.Stories.OrderBy(s => s.Elevation))
        {
            var ops = Openings(model, st.Name, dx, dy);
            if (ops.Count == 0) continue;
            var lv = LevelFor(plan, st);
            var added = DoorLintels.Place(dp, lv, ops);
            log.Add($"{lv.Name}: {ops.Count} ô cửa → {added.Count} lanh tô mới ({string.Join(", ", added.Select(a => a.Member).Distinct())})");
        }
        int created = dp.Lintels.Count - before;
        if (log.Count == 0) log.Add("Mô hình chưa có cửa gắn tường (đặt cửa bằng công cụ Cửa đi / Cửa sổ ở giao diện kiểu Revit).");
        else log.Add($"Tạo {created} cấu kiện lanh tô mới; số lượng mỗi cấu kiện = số ô cửa trên các mặt bằng.");
        return log;
    }

    /// <summary>Gắn file CAD vừa dùng ở "Mở mặt bằng CAD" làm ánh xạ kiến trúc của mặt bằng lv (nếu mặt bằng chưa có ánh xạ),
    /// dời sao cho trục đầu tiên đọc từ bản vẽ (originX, originY – m, toạ độ bản vẽ) trùng trục đầu tiên của mặt bằng hồ sơ.</summary>
    public static bool AttachUnderlayFromPick(PlanLevel lv, StructuralPlan plan, string cadFile, double originX, double originY, string projectDir, bool overwrite = false)
    {
        if (lv == null || string.IsNullOrWhiteSpace(cadFile)) return false;
        string ext = System.IO.Path.GetExtension(cadFile).ToLowerInvariant();
        if (ext != ".dxf" && ext != ".dwg") return false;
        if (!overwrite && !string.IsNullOrWhiteSpace(lv.Underlay)) return false;
        ArchUnderlay.Attach(lv, cadFile, projectDir);
        var xs = plan?.Xs ?? Array.Empty<double>(); var ys = plan?.Ys ?? Array.Empty<double>();
        lv.UnderlayDx = Math.Round((xs.Length > 0 ? xs.Min() : 0) - originX, 4);
        lv.UnderlayDy = Math.Round((ys.Length > 0 ? ys.Min() : 0) - originY, 4);
        return true;
    }
}

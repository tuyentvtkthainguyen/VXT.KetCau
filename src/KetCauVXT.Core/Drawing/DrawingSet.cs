using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Xuất toàn bộ bộ bản vẽ kết cấu DXF.</summary>
public static class DrawingSet
{
    /// <summary>Khổ giấy dùng cho bản vẽ (mặc định A1).</summary>
    public static string Paper { get; set; } = "A1";

    [ThreadStatic] private static bool _detailsOnly;
    /// <summary>[Chi tiết rời] đang dựng bản vẽ không lồng khung tên (mỗi chi tiết một khung bao).</summary>
    public static bool DetailsOnly => _detailsOnly;
    private sealed class DetailsScope : IDisposable { private readonly bool _old; public DetailsScope(bool on) { _old = _detailsOnly; _detailsOnly = on; } public void Dispose() => _detailsOnly = _old; }
    /// <summary>Phạm vi dựng bản vẽ chi tiết rời (using).</summary>
    public static IDisposable UseDetailsOnly(bool on) => new DetailsScope(on);

    /// <summary>Ghi các file KC-01..KC-06 vào thư mục, trả về danh sách đường dẫn đã ghi.</summary>
    public static List<string> ExportAll(Project p, DesignSummary s, string folder) => Export(p, s, folder, null).Files;

    /// <summary>Xuất bộ bản vẽ theo tuỳ chọn (tách rời từng bản / gộp 1 file). Lớp chỉnh sửa (Project.DrawingEdits) được áp lại,
    /// bản vẽ đã khoá dùng file đã nạp; result.Messages liệt kê cảnh báo khoá và "chỉnh sửa không còn áp dụng được".</summary>
    public static DrawingExportResult Export(Project p, DesignSummary s, string folder, DrawingExportOptions o)
    {
        if (p == null) throw new ArgumentNullException(nameof(p));
        var res = new DrawingExportResult();
        List<DrawingItem> items;
        using var fmtScope = CadFileWriters.UseFormat(o?.Format);   // tô vật liệu "tự động" theo định dạng chọn khi xuất
        using (UseDetailsOnly(o?.DetailsOnly == true))
        using (DrawingOverlay.Collect(res.Report)) items = BuildDrawings(p, s);
        DrawingExport.Write(items, folder, o, p.Name, res);
        foreach (var m in res.Report.Messages()) if (!res.Messages.Contains(m)) res.Messages.Add(m);
        return res;
    }

    /// <summary>Dựng các bản vẽ KC-01…KC-08 (chưa ghi file). want(khoá): chỉ dựng bản vẽ cần (null = tất cả); bản không dựng có W = null.
    /// Ngoài phạm vi DrawingOverlay.Bypass: đã áp lớp chỉnh sửa và thay bản đã khoá.</summary>
    public static List<DrawingItem> BuildDrawings(Project p, DesignSummary s, Func<string, bool> want = null)
    {
        if (p == null) throw new ArgumentNullException(nameof(p));
        s ??= new DesignSummary();
        p.InvalidateIndex();
        var items = new List<DrawingItem>();
        // [Điện nước → kết cấu] lỗ xuyên dầm / sàn có cảnh báo: lỗ trên mặt đứng dầm, kiểm tra cắt tại lỗ, thép gia cường vào thống kê
        List<KetCauVXT.Mep.PenetrationMark> pens;
        try { pens = KetCauVXT.Mep.PenetrationRegister.Build(p); KetCauVXT.Mep.PenetrationRegister.AttachToDesign(p, s, pens); }
        catch (Exception) { pens = new List<KetCauVXT.Mep.PenetrationMark>(); }

        SheetComposer.SheetInfo Info(string title, string no) => new SheetComposer.SheetInfo
        {
            ProjectName = p.Name, Title = title, SheetNo = no, Designer = p.Designer, Owner = p.Owner, Location = p.Location
        };

        void Write(string file, Func<List<Figure>> build, double scale, string title, string no, string kind, bool always = false, NotesScope? notes = null)
        {
            string key = DrawingOverlay.DrawingSetKey(file);
            var it = new DrawingItem { Key = key, FileName = file, Number = no, Title = title, Scale = scale };
            if (want != null && !want(key)) { items.Add(it); return; }
            var figs = build() ?? new List<Figure>();
            if (figs.Count == 0 && !always) return;   // không xuất tờ trống (vd. nhà thép không có cột/sàn BTCT)
            if (figs.Count == 0)
            {
                var empty = new DxfWriter(scale);
                empty.Text(Lay.Chu, 0, 0, empty.Th, "(Không có dữ liệu)");
                figs.Add(new Figure(empty));
            }
            else if (notes.HasValue)
            {
                // MỘT khối GHI CHÚ CHUNG cho cả bản vẽ (không lặp theo từng cấu kiện); tắt được trong Thiết lập bản vẽ
                var nf = GeneralNotes.FigureFor(p, notes.Value, scale, s);
                if (nf != null) { nf.W.OwnerKey ??= "NOTE:" + notes.Value; figs.Add(nf); }
            }
            DrawingOverlay.AssignOwners(figs, kind);   // khoá hình: chỉnh sửa dùng chung với tờ dàn trang (DrawingOverlay.cs)
            if (_detailsOnly)
            {
                // [Chi tiết rời] không khung tên; chỉnh sửa vị trí theo tờ (lớp chỉnh sửa) không áp – toạ độ khác tờ dàn trang
                it.W = SheetComposer.ComposeDetails(figs, scale, $"{no} – {title}", Paper);
                items.Add(it);
                return;
            }
            var w = SheetComposer.Compose(figs, scale, Info(title, no), Paper);
            if (!DrawingOverlay.Bypassed)
            {
                DrawingOverlay.Apply(p, w, key, true);
                it.W = w;
                DrawingExport.UseFrozen(p, it);
            }
            else it.W = w;
            items.Add(it);
        }

        // KC-01 mặt bằng kết cấu các tầng
        Write("KC-01_MatBangKetCau.dxf", () => FramingPlan.BuildAll(p, s), FramingPlan.CurrentScale,
            "MẶT BẰNG KẾT CẤU CÁC TẦNG", "KC-01", "PLAN", always: true, notes: NotesScope.Plan);

        // KC-02 móng
        Write("KC-02_Mong.dxf", () => FootingDrawing.BuildAll(p, s), FootingDrawing.SheetScale,
            "MẶT BẰNG VÀ CHI TIẾT MÓNG", "KC-02", "FOOT", notes: NotesScope.Foundation);

        // KC-02.1 mặt bằng định vị cọc + đường cong sức chịu tải cọc (chế độ thiết kế – Drawing/DesignPileDrawings.cs)
        if (s.PileCaps != null && s.PileCaps.Count > 0)
            Write("KC-02.1_DinhViCoc.dxf", () => DesignPileDrawings.Figures(p, s), 100,
                "MẶT BẰNG ĐỊNH VỊ CỌC – SỨC CHỊU TẢI CỌC", "KC-02.1", "PILE");

        // KC-03 cột
        Write("KC-03_Cot.dxf", () => ColumnDrawing.BuildAll(p, s.Columns), ColumnDrawing.CurrentScale,
            "CHI TIẾT CỘT", "KC-03", "COL", notes: NotesScope.Column);

        // KC-04 dầm theo tầng
        var storyOrder = p.Stories.OrderBy(x => x.Elevation).Select(x => x.Name).ToList();
        var groups = s.Beams.GroupBy(b => string.IsNullOrEmpty(b.Story) ? StoryOfElevation(p, b.TopElevation) : b.Story)
            .OrderBy(g => { int i = storyOrder.IndexOf(g.Key); return i < 0 ? int.MaxValue : i; })
            .ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
        int k = 0;
        foreach (var g in groups)
        {
            k++;
            string nm = DrawUtil.SafeFile(g.Key);
            // dầm dài: mặt đứng chia đoạn tại gối cho vừa bề rộng khổ giấy (trừ lề + khung) – BeamDrawing.Catalog.cs
            Write($"KC-04_Dam_{nm}.dxf", () => BeamDrawing.BuildAllSplit(p, g, s, SheetComposer.Paper(Paper).W - 60), BeamDrawing.CurrentScale,
                $"CHI TIẾT DẦM TẦNG {g.Key}", $"KC-04.{k}", "BEAM", notes: NotesScope.Beam);
        }

        // KC-05 sàn
        double ss = SlabDrawing.ChooseScale(p);
        Write("KC-05_San.dxf", () => SlabDrawing.BuildAll(p, s, ss), ss, "MẶT BẰNG THÉP SÀN", "KC-05", "SLAB", notes: NotesScope.Slab);

        // KC-06 thống kê thép (vẽ theo mm giấy, tỷ lệ 1/1)
        if (s.All.Any(m => m.Bars.Count > 0))
            Write("KC-06_ThongKeThep.dxf", () => RebarSchedule.BuildFigures(s, 1), 1, "THỐNG KÊ CỐT THÉP", "KC-06", "BTK", notes: NotesScope.Schedule);

        // KC-10 chi tiết gia cường lỗ xuyên kết cấu (từ cảnh báo bộ môn điện nước – Mep/PenetrationRegister.cs)
        if (pens.Count > 0)
            Write("KC-10_GiaCuongLoXuyen.dxf", () => KetCauVXT.Mep.PenetrationRegister.Figures(p, pens), 20,
                KetCauVXT.Mep.PenetrationRegister.SheetTitle, "KC-10", "PEN");

        // KC-09 thống kê cửa (cửa gắn tường – mô hình kiểu Revit; Drawing/WallPlanDrawing.cs)
        if (WallPlanDrawing.Enabled(p))
            Write("KC-09_ThongKeCua.dxf", () => { var df = WallPlanDrawing.ScheduleFigure(p); return df == null ? new List<Figure>() : new List<Figure> { df }; }, 1,
                WallPlanDrawing.ScheduleTitle, "KC-09", "DOOR");

        // ---- KC-08 cầu thang: mặt bằng, mặt cắt vế, chi tiết thép, thống kê thép (StairDrawing.cs) ----
        int kst = 0;
        foreach (var sd in s.Stairs ?? new List<StairDesign>())
        {
            kst++;
            var sdc = sd;
            Write($"KC-08.{kst}_CauThang_{DrawUtil.SafeFile(sd.Name)}.dxf", () => StairDrawing.Build(p, s, sdc, StairDrawing.CurrentScale), StairDrawing.CurrentScale,
                $"CẦU THANG {sd.Name} – MẶT BẰNG, MẶT CẮT, BỐ TRÍ THÉP", $"KC-08.{kst}", "STAIR");
        }
        // ---- hết cầu thang ----
        return items;
    }

    private static string StoryOfElevation(Project p, double z)
    {
        var st = p.Stories.OrderBy(x => Math.Abs(x.Elevation - z)).FirstOrDefault();
        return st?.Name ?? "Khac";
    }
}

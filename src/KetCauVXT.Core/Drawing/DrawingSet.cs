using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Xuất toàn bộ bộ bản vẽ kết cấu DXF.</summary>
public static class DrawingSet
{
    /// <summary>Khổ giấy dùng cho bản vẽ (mặc định A1).</summary>
    public static string Paper { get; set; } = "A1";

    /// <summary>Ghi các file KC-01..KC-06 vào thư mục, trả về danh sách đường dẫn đã ghi.</summary>
    public static List<string> ExportAll(Project p, DesignSummary s, string folder)
    {
        if (p == null) throw new ArgumentNullException(nameof(p));
        s ??= new DesignSummary();
        Directory.CreateDirectory(folder);
        p.InvalidateIndex();
        var paths = new List<string>();

        SheetComposer.SheetInfo Info(string title, string no) => new SheetComposer.SheetInfo
        {
            ProjectName = p.Name, Title = title, SheetNo = no, Designer = p.Designer, Owner = p.Owner, Location = p.Location
        };

        void Write(string file, List<Figure> figs, double scale, string title, string no, bool always = false)
        {
            if (figs.Count == 0 && !always) return;   // không xuất tờ trống (vd. nhà thép không có cột/sàn BTCT)
            if (figs.Count == 0)
            {
                var empty = new DxfWriter(scale);
                empty.Text(Lay.Chu, 0, 0, empty.Th, "(Không có dữ liệu)");
                figs.Add(new Figure(empty));
            }
            var w = SheetComposer.Compose(figs, scale, Info(title, no), Paper);
            string path = Path.Combine(folder, file);
            w.Save(path);
            paths.Add(path);
        }

        // KC-01 mặt bằng kết cấu các tầng
        Write("KC-01_MatBangKetCau.dxf", FramingPlan.BuildAll(p, s), FramingPlan.SheetScale,
            "MẶT BẰNG KẾT CẤU CÁC TẦNG", "KC-01", always: true);

        // KC-02 móng
        Write("KC-02_Mong.dxf", FootingDrawing.BuildAll(p, s), FootingDrawing.SheetScale,
            "MẶT BẰNG VÀ CHI TIẾT MÓNG", "KC-02");

        // KC-03 cột
        Write("KC-03_Cot.dxf", ColumnDrawing.BuildAll(p, s.Columns), ColumnDrawing.SheetScale,
            "CHI TIẾT CỘT", "KC-03");

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
            Write($"KC-04_Dam_{nm}.dxf", BeamDrawing.BuildAll(p, g), BeamDrawing.SheetScale,
                $"CHI TIẾT DẦM TẦNG {g.Key}", $"KC-04.{k}");
        }

        // KC-05 sàn
        double ss = SlabDrawing.ChooseScale(p);
        Write("KC-05_San.dxf", SlabDrawing.BuildAll(p, s, ss), ss, "MẶT BẰNG THÉP SÀN", "KC-05");

        // KC-06 thống kê thép (vẽ theo mm giấy, tỷ lệ 1/1)
        if (s.All.Any(m => m.Bars.Count > 0))
            Write("KC-06_ThongKeThep.dxf", RebarSchedule.BuildFigures(s, 1), 1, "THỐNG KÊ CỐT THÉP", "KC-06");
        return paths;
    }

    private static string StoryOfElevation(Project p, double z)
    {
        var st = p.Stories.OrderBy(x => Math.Abs(x.Elevation - z)).FirstOrDefault();
        return st?.Name ?? "Khac";
    }
}

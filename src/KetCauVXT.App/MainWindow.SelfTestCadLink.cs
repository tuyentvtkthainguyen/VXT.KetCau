using System.Globalization;
using System.IO;
using System.Text;
using KetCauVXT.Import.Link;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest): ghi một mặt bằng DXF nhỏ (đơn vị mm, đặt lệch khỏi mô hình), liên kết vào tầng đang xem,
/// chụp mặt bằng có nền bản vẽ, mở cửa sổ "Bản vẽ liên kết" (không chặn) chụp ảnh, rồi bỏ liên kết.</summary>
public partial class MainWindow
{
    /// <summary>DXF ASCII R12 tối giản: mặt bằng 8 × 6 m tường 220 (2 nét), lưới trục 1-2-3 / A-B có tên trục, đặt tại (ox, oy) mm.</summary>
    internal static string CadLinkSampleDxf(double ox, double oy)
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        void P(int code, object v) { sb.Append(code.ToString(c)).Append('\n').Append(Convert.ToString(v, c)).Append('\n'); }
        void Line(string l, double x1, double y1, double x2, double y2) { P(0, "LINE"); P(8, l); P(10, ox + x1); P(20, oy + y1); P(30, 0.0); P(11, ox + x2); P(21, oy + y2); P(31, 0.0); }
        void Text(string l, double x, double y, double h, string s) { P(0, "TEXT"); P(8, l); P(10, ox + x); P(20, oy + y); P(30, 0.0); P(40, h); P(1, s); }
        P(0, "SECTION"); P(2, "HEADER"); P(9, "$ACADVER"); P(1, "AC1009"); P(9, "$INSUNITS"); P(70, 4); P(0, "ENDSEC");
        P(0, "SECTION"); P(2, "TABLES");
        P(0, "TABLE"); P(2, "LTYPE"); P(70, 1);
        P(0, "LTYPE"); P(2, "CONTINUOUS"); P(70, 0); P(3, "Solid line"); P(72, 65); P(73, 0); P(40, 0.0);
        P(0, "ENDTAB");
        P(0, "TABLE"); P(2, "LAYER"); P(70, 3);
        foreach (var (n, col) in new[] { ("KT-TUONG", 7), ("KT-TRUC", 1), ("KT-TEXT", 2) }) { P(0, "LAYER"); P(2, n); P(70, 0); P(62, col); P(6, "CONTINUOUS"); }
        P(0, "ENDTAB");
        P(0, "ENDSEC");
        P(0, "SECTION"); P(2, "ENTITIES");
        const string W = "KT-TUONG", G = "KT-TRUC";
        Line(W, -110, -110, 8110, -110); Line(W, 110, 110, 7890, 110);
        Line(W, 110, 5890, 7890, 5890); Line(W, -110, 6110, 8110, 6110);
        Line(W, -110, -110, -110, 6110); Line(W, 110, 110, 110, 5890);
        Line(W, 7890, 110, 7890, 5890); Line(W, 8110, -110, 8110, 6110);
        foreach (var (x, n) in new[] { (0.0, "1"), (4000.0, "2"), (8000.0, "3") }) { Line(G, x, -1500, x, 7500); Text(G, x - 150, 7700, 350, n); }
        foreach (var (y, n) in new[] { (0.0, "A"), (6000.0, "B") }) { Line(G, -1500, y, 9500, y); Text(G, -2000, y - 150, 350, n); }
        Text("KT-TEXT", 2500, 3000, 300, "PHONG KHACH");
        P(0, "ENDSEC"); P(0, "EOF");
        return sb.ToString();
    }

    private async Task SelfTestCadLink(string tag)
    {
        if (_p == null || SelfTestOut == null) return;
        // đặt bản vẽ lệch về bên phải mô hình 3 m
        double mx = _p.Nodes.Select(n => n.X).Concat(_p.Grids.Where(g => g.Dir == GridDir.X).Select(g => g.Coord)).DefaultIfEmpty(0).Max();
        double my = _p.Nodes.Select(n => n.Y).Concat(_p.Grids.Where(g => g.Dir == GridDir.Y).Select(g => g.Coord)).DefaultIfEmpty(0).Min();
        string path = Path.Combine(SelfTestOut, tag + "_BanVeLink.dxf");
        File.WriteAllText(path, CadLinkSampleDxf((mx + 3) * 1000, my * 1000), new UTF8Encoding(false));
        string story = CurrentStory == AllStories || _p.Stories.All(q => q.Name != CurrentStory) ? _p.Stories.OrderBy(s => s.Elevation).FirstOrDefault()?.Name : CurrentStory;
        if (story != null) cbStory.SelectedItem = story;
        var l = await CadLinkAdd(path, story ?? CadLink.AllStories);
        var g = CadLinkCache.Get(l);
        StLog(g != null
            ? $"bản vẽ link: {l.DisplayName} – {g.SegmentCount} nét, {g.Texts.Count} chữ, {g.Layers.Count} layer, $INSUNITS = {g.InsUnits} → ×{g.UnitScale}, tầng {l.Story}"
            : "bản vẽ link: KHÔNG đọc được " + path);
        if (g == null) throw new Exception("Bản vẽ liên kết: không đọc được file DXF mẫu");
        var v = g.View(l);
        var snap = CadLinkSnap.Best(_p, story, v.X0 + 0.11, v.Y0 + 0.11, 0.3);
        StLog($"bản vẽ link: phạm vi ({v.X0:0.00}; {v.Y0:0.00}) – ({v.X1:0.00}; {v.Y1:0.00}); bắt điểm góc → {snap}");
        tabView.SelectedItem = ViewTab("Mặt bằng");
        await Pause(800);
        CadLinkZoom(null);
        await Pause(1200);
        Shot($"{tag}_53_BanVeLink");

        var w = OpenCadLinkManager();
        if (w != null)
        {
            await Pause(1500);
            ShotWindow(w, $"{tag}_54_QuanLyLink");
            w.Close();
        }
        try
        {
            var story0 = story != null && _p.Stories.Any(s => s.Name == story) ? story : null;
            if (story0 != null)
            {
                var diff = CadLinkSync.Compare(_p, l, g, story0);
                StLog("bản vẽ link: so sánh – " + diff.Summary);
            }
        }
        catch (Exception ex) { StLog("bản vẽ link: so sánh lỗi – " + ex.Message); }
        CadLinkRemove(l);
        StLog($"bản vẽ link: đã bỏ liên kết – còn {_p.CadLinks.Count} bản vẽ");
        FitPlan();
    }
}

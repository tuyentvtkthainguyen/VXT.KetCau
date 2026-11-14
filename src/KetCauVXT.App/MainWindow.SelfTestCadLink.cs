using System.Windows;
using System.Windows.Input;
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

    /// <summary>DXF lưới trục của mô hình (mm, có vòng tròn + tên trục) dời (ox, oy) m – giống mặt bằng kết cấu do phần mềm xuất.</summary>
    internal static string GridSampleDxf(Project p, double ox, double oy)
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        void P(int code, object v) { sb.Append(code.ToString(c)).Append('\n').Append(Convert.ToString(v, c)).Append('\n'); }
        void Line(double x1, double y1, double x2, double y2) { P(0, "LINE"); P(8, "TRUC"); P(10, x1); P(20, y1); P(30, 0.0); P(11, x2); P(21, y2); P(31, 0.0); }
        void Bubble(double x, double y, string n) { P(0, "CIRCLE"); P(8, "TRUC"); P(10, x); P(20, y); P(30, 0.0); P(40, 400.0); P(0, "TEXT"); P(8, "TRUC"); P(10, x - 120); P(20, y - 175); P(30, 0.0); P(40, 350.0); P(1, n); }
        var gx = p.Grids.Where(g => g.Dir == GridDir.X).ToList(); var gy = p.Grids.Where(g => g.Dir == GridDir.Y).ToList();
        double x0 = gx.Select(g => g.Coord).DefaultIfEmpty(0).Min() - 2, x1 = gx.Select(g => g.Coord).DefaultIfEmpty(0).Max() + 2;
        double y0 = gy.Select(g => g.Coord).DefaultIfEmpty(0).Min() - 2, y1 = gy.Select(g => g.Coord).DefaultIfEmpty(0).Max() + 2;
        P(0, "SECTION"); P(2, "HEADER"); P(9, "$ACADVER"); P(1, "AC1009"); P(9, "$INSUNITS"); P(70, 4); P(0, "ENDSEC");
        P(0, "SECTION"); P(2, "ENTITIES");
        foreach (var g in gx) { double x = (g.Coord + ox) * 1000; Line(x, (y0 + oy) * 1000, x, (y1 + oy) * 1000); Bubble(x, (y1 + oy) * 1000 + 400, g.Name); }
        foreach (var g in gy) { double y = (g.Coord + oy) * 1000; Line((x0 + ox) * 1000, y, (x1 + ox) * 1000, y); Bubble((x0 + ox) * 1000 - 400, y, g.Name); }
        P(0, "ENDSEC"); P(0, "EOF");
        return sb.ToString();
    }

    /// <summary>[Ánh xạ Xref] tự khớp theo tên trục: dời Xref → kết cấu, rồi dời mặt bằng kết cấu → Xref (hoàn tác lại).</summary>
    private async Task SelfTestXrefMap(string tag)
    {
        var gx = _p.Grids.Where(g => g.Dir == GridDir.X).ToList(); var gy = _p.Grids.Where(g => g.Dir == GridDir.Y).ToList();
        if (gx.Count == 0 || gy.Count == 0) { StLog("ánh xạ xref: mô hình không có trục – bỏ qua"); return; }
        string path = Path.Combine(SelfTestOut, tag + "_XrefTruc.dxf");
        File.WriteAllText(path, GridSampleDxf(_p, 37.5, -12.25), new UTF8Encoding(false));
        var l = await XrefAttach(path, (-15.0, -15.0));
        if (l == null || CadLinkCache.Get(l) == null) throw new Exception("Ánh xạ xref: không gắn được bản vẽ trục");
        double Err(CadLink k) => gx.Max(g => Math.Abs(k.ToModel(g.Coord + 37.5, 0).X - g.Coord)) + gy.Max(g => Math.Abs(k.ToModel(0, g.Coord - 12.25).Y - g.Coord));
        StLog($"ánh xạ xref: trước khớp sai lệch {Err(l):0.###} m");
        var r = CadLinkAutoMatch(l, false);
        StLog($"ánh xạ xref (dời Xref): {r?.Message}; sai lệch còn {Err(l):0.####} m");
        if (r == null || !r.Ok || Err(l) > 0.002) throw new Exception("Ánh xạ xref: tự khớp theo trục không đúng – " + r?.Message);
        CadLinkZoom(null); await Pause(1000);
        Shot($"{tag}_56_AnhXaXref");

        // dời Xref lệch đi rồi dời MẶT BẰNG KẾT CẤU đến Xref
        bool hadRes = _res != null, hadDesign = _design != null;
        CadLinkMoveLink(l, 3.0, 2.0);
        double nx = _p.Nodes.Count > 0 ? _p.Nodes[0].X : 0, ny = _p.Nodes.Count > 0 ? _p.Nodes[0].Y : 0;
        double g0 = gx[0].Coord;
        var r2 = CadLinkAutoMatch(l, true);
        double moved = gx[0].Coord - g0;
        StLog($"ánh xạ xref (dời kết cấu): {r2?.Message}; trục {gx[0].Name} dời {moved:0.###} m, nút đầu dời ({(_p.Nodes.Count > 0 ? _p.Nodes[0].X - nx : 0):0.###}; {(_p.Nodes.Count > 0 ? _p.Nodes[0].Y - ny : 0):0.###}), sai lệch {Err(l):0.####} m");
        if (r2 == null || !r2.Ok || Math.Abs(moved - 3.0) > 0.002) throw new Exception("Ánh xạ xref: dời mặt bằng kết cấu không đúng");
        var w = OpenXrefMap(l);
        if (w != null) { await Pause(800); ShotWindow(w, $"{tag}_57_AnhXaXrefHop"); w.Close(); }
        Undo(); Undo();
        StLog($"ánh xạ xref: hoàn tác – trục {gx[0].Name} về {_p.Grids.First(g => g.Dir == GridDir.X).Coord:0.###} (ban đầu {g0:0.###})");
        if (Math.Abs(_p.Grids.First(g => g.Dir == GridDir.X).Coord - g0) > 1e-6) throw new Exception("Ánh xạ xref: hoàn tác không khôi phục toạ độ mô hình");
        var lk = _p.CadLinks.FirstOrDefault(q => q.Id == l.Id) ?? _p.CadLinks.LastOrDefault();
        if (lk != null) CadLinkRemove(lk);
        FitPlan();
        if (hadRes && _res == null) { if (!await RunAnalysis()) throw new Exception("Ánh xạ xref: phân tích lại lỗi"); }
        if (hadDesign && _design == null) await EnsureDesign();
    }

    /// <summary>DXF lớn (nhiều nét + chữ) để đo tốc độ vẽ mặt bằng khi có Xref.</summary>
    private static string HeavyDxf(int lines, int texts, double w, double h)
    {
        var c = CultureInfo.InvariantCulture; var rnd = new Random(7);
        var sb = new StringBuilder("0\nSECTION\n2\nHEADER\n9\n$INSUNITS\n70\n4\n0\nENDSEC\n0\nSECTION\n2\nENTITIES\n");
        for (int i = 0; i < lines; i++)
        {
            double x = rnd.NextDouble() * w, y = rnd.NextDouble() * h, a = rnd.NextDouble() * Math.PI, L = 200 + rnd.NextDouble() * 1500;
            sb.Append("0\nLINE\n8\nKT").Append(i % 12).Append("\n62\n").Append(1 + i % 7)
              .Append("\n10\n").Append(x.ToString(c)).Append("\n20\n").Append(y.ToString(c)).Append("\n30\n0\n11\n").Append((x + L * Math.Cos(a)).ToString(c))
              .Append("\n21\n").Append((y + L * Math.Sin(a)).ToString(c)).Append("\n31\n0\n");
        }
        for (int i = 0; i < texts; i++)
            sb.Append("0\nTEXT\n8\nCHU\n10\n").Append((rnd.NextDouble() * w).ToString(c)).Append("\n20\n").Append((rnd.NextDouble() * h).ToString(c))
              .Append("\n30\n0\n40\n250\n1\nPHONG ").Append(i).Append('\n');
        sb.Append("0\nENDSEC\n0\nEOF\n");
        return sb.ToString();
    }

    /// <summary>[Di chuyển Xref] kéo bản vẽ trục bằng chuột (mô phỏng) cho trùng trục; [Mượt khi vẽ] đo thời gian vẽ mặt bằng với Xref lớn.</summary>
    private async Task SelfTestXrefMovePerf(string tag)
    {
        var gx = _p.GridsX.ToList(); var gy = _p.GridsY.ToList();
        if (gx.Count == 0 || gy.Count == 0) return;
        tabView.SelectedItem = ViewTab("Mặt bằng");
        FitPlan(); await Pause(300);
        // 1) di chuyển Xref bằng chuột
        string path = Path.Combine(SelfTestOut, tag + "_XrefDiChuyen.dxf");
        File.WriteAllText(path, GridSampleDxf(_p, 0.37, -0.29), new UTF8Encoding(false));
        var l = await XrefAttach(path, null);
        if (l == null) throw new Exception("Di chuyển xref: không gắn được bản vẽ");
        // bản vẽ lệch trục 0.37 / -0.29 m (giữ gốc toạ độ vì trùng vùng mô hình)
        FitPlan(); await Pause(300);
        var src = l.ToModel(gx[0].Coord + 0.37, gy[0].Coord - 0.29);
        XrefMoveStart(l.Id);
        var sBase = W2S(src.X, src.Y); sBase.Offset(3, -2);   // lệch 3 px – phải bắt vào giao nét bản vẽ
        var sDst = W2S(gx[0].Coord, gy[0].Coord); sDst.Offset(-4, 3);   // lệch 4 px – phải bắt vào giao trục
        XrefMoveDown(sBase);
        var mid = new Point((sBase.X + sDst.X) / 2, (sBase.Y + sDst.Y) / 2);
        XrefMoveMove(mid); await Pause(200);
        XrefMoveMove(sDst); await Pause(300);
        Shot($"{tag}_58_DiChuyenXref_Keo");
        XrefMoveUp(sDst);
        var c1 = l.ToModel(gx[^1].Coord + 0.37, gy[^1].Coord - 0.29);
        double err = Math.Abs(c1.X - gx[^1].Coord) + Math.Abs(c1.Y - gy[^1].Coord);
        StLog($"di chuyển xref (kéo chuột): lệch gốc ({l.OffsetX:0.###}; {l.OffsetY:0.###}), sai lệch trục cuối {err * 1000:0.#} mm");
        if (err > 0.002) throw new Exception("Di chuyển xref: kéo chuột không đưa bản vẽ trùng trục");
        // nhích bằng phím mũi tên
        HandleXrefMoveKey(Key.Right); HandleXrefMoveKey(Key.Left);
        double err2 = Math.Abs(l.ToModel(gx[0].Coord + 0.37, 0).X - gx[0].Coord);
        StLog($"di chuyển xref: nhích phải + trái → sai lệch {err2 * 1000:0.#} mm");
        if (err2 > 0.0005) throw new Exception("Di chuyển xref: nhích phím mũi tên sai");
        HandleXrefMoveKey(Key.Escape); HandleXrefMoveKey(Key.Escape);
        if (XrefMoveActive) throw new Exception("Di chuyển xref: Esc không kết thúc chế độ");
        await Pause(300);
        Shot($"{tag}_59_DiChuyenXref_Xong");
        CadLinkRemove(l);

        // 2) đo tốc độ với Xref lớn
        string big = Path.Combine(SelfTestOut, tag + "_XrefLon.dxf");
        double W = (gx[^1].Coord - gx[0].Coord + 4) * 1000, H = (gy[^1].Coord - gy[0].Coord + 4) * 1000;
        File.WriteAllText(big, HeavyDxf(60000, 1500, W, H), new UTF8Encoding(false));
        var lb = await XrefAttach(big, null);
        if (lb == null) throw new Exception("Xref lớn: không gắn được");
        // cửa sổ CI nhỏ: phóng to tạm để khung mặt bằng có diện tích thật khi đo
        var ws0 = WindowState; double w0 = Width, h0 = Height;
        WindowState = WindowState.Normal; Width = 1900; Height = 1250; UpdateLayout();
        ShowPlanTab(); tabView.SelectedItem = ViewTab("Mặt bằng"); cbStory.SelectedItem = _p.Stories.OrderBy(q => q.Elevation).First().Name;
        await Pause(600); FitPlan(); RedrawPlan(); await Pause(500);
        StLog($"xref lớn: khung vẽ {planHost.ActualWidth:0}×{planHost.ActualHeight:0}, {planCanvas.Children.Count} hình, chờ vừa màn hình {_fitPending}, tầng {CurrentStory}, áp dụng tầng {lb.AppliesTo(CurrentStory)}");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 5; i++) RedrawPlan();
        double tRedraw = sw.Elapsed.TotalMilliseconds / 5;
        // di chuột khi vẽ dầm (dây chun + bắt điểm Xref)
        SetPlanMode("AddBeam"); await Pause(200);
        var a = W2S(gx[0].Coord, gy[0].Coord); var b = W2S(gx[^1].Coord, gy[^1].Coord);
        sw.Restart();
        for (int i = 0; i < 60; i++) ProcessPlanMove(new Point(a.X + (b.X - a.X) * i / 60.0, a.Y + (b.Y - a.Y) * i / 60.0));
        double tMove = sw.Elapsed.TotalMilliseconds / 60;
        await Pause(100);
        int n0 = PlanRedrawCount;
        sw.Restart();
        for (int i = 0; i < 30; i++) { _ox += 2; PlanQuickView(); }
        double tPan = sw.Elapsed.TotalMilliseconds / 30;
        int redrawsDuringPan = PlanRedrawCount - n0;
        await Pause(500);
        StLog($"tốc độ mặt bằng (Xref 60000 nét + 1500 chữ): vẽ lại {tRedraw:0} ms/lần, di chuột khi vẽ dầm {tMove:0.0} ms/lần, kéo dời {tPan:0.00} ms/lần ({redrawsDuringPan} lần vẽ lại trong 30 bước kéo; sau khi ngừng: {PlanRedrawCount - n0} lần)");
        if (redrawsDuringPan > 0 && planCanvas.Children.Count > 0) throw new Exception("Mượt khi vẽ: kéo dời vẫn vẽ lại toàn bộ mặt bằng");
        Shot($"{tag}_59b_XrefLon");
        rbModeSelect.IsChecked = true;
        CadLinkRemove(lb);
        Width = w0; Height = h0; WindowState = ws0; UpdateLayout(); await Pause(300);
        FitPlan();
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

        // [Xref] gắn như XREF bằng kéo thả: góc trái-dưới bản vẽ đặt tại điểm thả
        var xl = await XrefAttach(path, (-20.0, -20.0));
        var xg = xl != null ? CadLinkCache.Get(xl) : null;
        if (xl == null || xg == null) throw new Exception("Xref: không gắn được bản vẽ");
        var xv = xg.View(xl);
        StLog($"xref: gắn {xl.DisplayName} tại điểm thả (-20; -20) → góc bản vẽ ({xv.X0:0.00}; {xv.Y0:0.00})");
        if (Math.Abs(xv.X0 + 20) > 0.5 || Math.Abs(xv.Y0 + 20) > 0.5) throw new Exception($"Xref: góc bản vẽ không ở điểm thả ({xv.X0:0.00}; {xv.Y0:0.00})");
        CadLinkZoom(null); await Pause(1000);
        Shot($"{tag}_55_Xref");
        CadLinkRemove(xl);
        await SelfTestXrefMap(tag);
        await SelfTestXrefMovePerf(tag);   // [Di chuyển Xref] + [Mượt khi vẽ]
        FitPlan();
    }
}

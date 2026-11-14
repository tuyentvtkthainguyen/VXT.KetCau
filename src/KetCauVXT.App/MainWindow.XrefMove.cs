using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Import.Link;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>[Di chuyển Xref] Dời bản vẽ Xref bằng chuột giống lệnh MOVE của AutoCAD để trùng trục thực tế của mặt bằng kết cấu:
/// bấm (hoặc nhấn giữ) một điểm trên Xref – bắt điểm đầu mút / giao nét của bản vẽ (ký hiệu tím) – rồi kéo / bấm điểm đích –
/// bắt giao trục, nút; không có giao trục gần thì bắt riêng từng phương vào trục X / trục Y gần nhất (căn một phương).
/// Bản vẽ dời theo con trỏ tức thời (không vẽ lại cả mặt bằng). Phím mũi tên: nhích 10 mm (Shift 100 mm, Ctrl 1 mm).
/// Esc để kết thúc. Mỗi lần dời một bước hoàn tác.</summary>
public partial class MainWindow
{
    private int _xmvStep;                 // 0 tắt · 1 chờ điểm gốc · 2 đang dời
    private string _xmvLink;              // Id bản vẽ đang dời
    private (double X, double Y) _xmvBase, _xmvTo;
    private Point _xmvDownAt;
    private bool _xmvDragging;
    private string _xmvSnapText = "";

    internal bool XrefMoveActive => _xmvStep > 0;
    private static string XrefTag(CadLink l) => "XREF:" + l?.Id;

    /// <summary>Điểm bắt bản vẽ GẦN CON TRỎ NHẤT: ưu tiên loại điểm (đầu mút, giao, giữa) chỉ cộng thêm vài pixel – trước đây sắp theo
    /// loại trước rồi mới khoảng cách nên khi thu nhỏ (bán kính bắt lớn) điểm bắt nhảy sang đầu nét ở xa → không đặt được đúng vị trí.</summary>
    private double SnapScore(SnapCandidate c) => c.Dist + Math.Min(c.Priority, 6) * 2.5 / Math.Max(_scale, 1e-6);

    private SnapCandidate NearestXrefSnap(IEnumerable<SnapCandidate> cands)
    {
        var list = cands?.ToList() ?? new List<SnapCandidate>();
        var pts = list.Where(q => q.Kind is SnapKind.End or SnapKind.Intersection or SnapKind.Midpoint or SnapKind.Node or SnapKind.GridIntersection or SnapKind.CadEnd or SnapKind.CadMid or SnapKind.CadIntersection or SnapKind.CadCenter).ToList();
        return (pts.Count > 0 ? pts : list).OrderBy(SnapScore).FirstOrDefault();
    }

    private void BtnXrefMove_Click(object sender, RoutedEventArgs e) => XrefMoveStart();

    /// <summary>Bật chế độ di chuyển Xref. linkId = null: chọn bản vẽ theo điểm bấm đầu tiên.</summary>
    internal void XrefMoveStart(string linkId = null)
    {
        if (_p == null) return;
        var vis = VisibleLinks().ToList();
        if (vis.Count == 0) { SetStatus("Không có bản vẽ Xref nào hiện ở tầng đang xem – gắn bản vẽ (kéo thả file DWG / DXF) trước."); return; }
        var locked = vis.Where(l => l.Locked && (linkId == null || l.Id == linkId)).ToList();
        if (locked.Count > 0)
        {
            if (MessageBox.Show(this, $"Bản vẽ {string.Join(", ", locked.Select(l => l.DisplayName))} đang KHOÁ vị trí. Bỏ khoá để di chuyển?", AppTitle,
                                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            string snap = Snapshot();
            foreach (var l in locked) l.Locked = false;
            PushUndo(snap); MarkDirty(false);
            _clWin?.Reload();
        }
        _clAlignStep = 0;
        _xmvStep = 1; _xmvLink = linkId; _xmvDragging = false;
        try { tabView.SelectedItem = ViewTab("Mặt bằng"); } catch (Exception) { }
        if (planHost != null) { planHost.Cursor = Cursors.Cross; planHost.Focus(); }
        SetStatus("Di chuyển Xref (1/2): bấm hoặc nhấn giữ một điểm trên bản vẽ Xref (giao trục, góc cột… – bắt điểm tím) · mũi tên: nhích 10 mm (Shift 100, Ctrl 1) · Esc: kết thúc.");
        DrawHover();
    }

    private IEnumerable<CadLink> VisibleLinks()
    {
        string st = CurrentStory; bool all = st == AllStories;
        return _p?.CadLinks?.Where(l => l != null && l.ShownOn(all ? null : st) && CadLinkCache.Get(l) != null) ?? Enumerable.Empty<CadLink>();
    }

    private void XrefMoveEnd(string msg = null)
    {
        _xmvStep = 0; _xmvDragging = false;
        XrefPreview(null, 0, 0);
        if (planHost != null) { planHost.ReleaseMouseCapture(); planHost.Cursor = _mode == PlanEditMode.Select ? null : Cursors.Cross; }
        DrawHover();
        SetStatus(msg ?? "Đã kết thúc di chuyển Xref.");
    }

    /// <summary>Điểm gốc trên Xref: điểm bắt gần nhất của mọi bản vẽ đang hiện (hoặc bản vẽ đã chọn).</summary>
    private (CadLink L, double X, double Y, bool Snapped) XrefPickBase(Point s)
    {
        var (x, y) = S2W(s);
        double tol = 12 / Math.Max(_scale, 1e-6);
        var links = VisibleLinks().Where(l => _xmvLink == null || l.Id == _xmvLink).ToList();
        SnapCandidate best = null; CadLink bl = null;
        foreach (var l in links)
        {
            var c = NearestXrefSnap(CadLinkSnap.Candidates(CadLinkSnap.Active(new[] { l }, null, true), x, y, tol, SnapMask.All));
            if (c != null && (best == null || SnapScore(c) < SnapScore(best))) { best = c; bl = l; }
        }
        if (best != null) return (bl, best.X, best.Y, true);
        return (links.FirstOrDefault(), x, y, false);
    }

    /// <summary>Điểm đích trên mặt bằng kết cấu: giao trục → nút → bắt riêng từng phương vào trục gần nhất.</summary>
    private (double X, double Y) XrefPickTarget(Point s, double ddx = 0, double ddy = 0)
    {
        var (x, y) = S2W(s);
        double tol = 12 / Math.Max(_scale, 1e-6);
        _xmvSnapText = "";
        double bd = tol; (double X, double Y)? best = null;
        foreach (var gx in _p.GridsX)
            foreach (var gy in _p.GridsY)
            {
                double d = Math.Sqrt((gx.Coord - x) * (gx.Coord - x) + (gy.Coord - y) * (gy.Coord - y));
                if (d <= bd) { bd = d; best = (gx.Coord, gy.Coord); _xmvSnapText = $"giao trục {gx.Name}-{gy.Name}"; }
            }
        if (best != null) return best.Value;
        foreach (var n in _p.Nodes)
        {
            double d = Math.Sqrt((n.X - x) * (n.X - x) + (n.Y - y) * (n.Y - y));
            if (d <= bd) { bd = d; best = (n.X, n.Y); _xmvSnapText = $"nút {n.Id}"; }
        }
        if (best != null) return best.Value;
        var ax = _p.GridsX.OrderBy(g => Math.Abs(g.Coord - x)).FirstOrDefault();
        var ay = _p.GridsY.OrderBy(g => Math.Abs(g.Coord - y)).FirstOrDefault();
        var parts = new List<string>();
        if (ax != null && Math.Abs(ax.Coord - x) <= tol) { x = ax.Coord; parts.Add("trục " + ax.Name); }
        if (ay != null && Math.Abs(ay.Coord - y) <= tol) { y = ay.Coord; parts.Add("trục " + ay.Name); }
        _xmvSnapText = string.Join(", ", parts);
        return (Math.Round(x, 4), Math.Round(y, 4));
    }

    private bool XrefMoveDown(Point s)
    {
        if (_xmvStep == 0) return false;
        if (_xmvStep == 1)
        {
            var b = XrefPickBase(s);
            if (b.L == null) { SetStatus("Không có bản vẽ Xref để dời."); return true; }
            _xmvLink = b.L.Id; _xmvBase = (b.X, b.Y); _xmvTo = _xmvBase;
            _xmvStep = 2; _xmvDownAt = s; _xmvDragging = true;
            planHost.CaptureMouse();
            SetStatus($"Di chuyển Xref {b.L.DisplayName} (2/2): điểm gốc ({b.X:0.000}; {b.Y:0.000}){(b.Snapped ? "" : " – không bắt được điểm bản vẽ")} – kéo / bấm điểm đích (bắt giao trục, nút, trục) · Esc: huỷ.");
            DrawHover();
            return true;
        }
        // bước 2, kiểu bấm – bấm: bấm điểm đích
        XrefCommit(XrefPickTarget(s));
        return true;
    }

    private bool XrefMoveMove(Point s)
    {
        if (_xmvStep == 0) return false;
        if (_xmvStep == 1) { DrawHover(); return true; }
        _xmvTo = XrefPickTarget(s);
        XrefPreview(_xmvLink, _xmvTo.X - _xmvBase.X, _xmvTo.Y - _xmvBase.Y);
        DrawHover();
        return true;
    }

    private bool XrefMoveUp(Point s)
    {
        if (_xmvStep != 2 || !_xmvDragging) return _xmvStep > 0;
        _xmvDragging = false;
        planHost.ReleaseMouseCapture();
        if ((s - _xmvDownAt).Length < 5) { DrawHover(); return true; }   // bấm (không kéo) → chờ bấm điểm đích
        XrefCommit(XrefPickTarget(s));
        return true;
    }

    private void XrefCommit((double X, double Y) to)
    {
        var l = CadLinkById(_xmvLink);
        double dx = to.X - _xmvBase.X, dy = to.Y - _xmvBase.Y;
        _xmvStep = 1; _xmvDragging = false;
        XrefPreview(null, 0, 0);
        if (l == null) { XrefMoveEnd(); return; }
        if (Math.Abs(dx) < 1e-7 && Math.Abs(dy) < 1e-7) { SetStatus("Điểm đích trùng điểm gốc – Xref giữ nguyên. Bấm điểm gốc khác hoặc Esc."); DrawHover(); return; }
        if (CadLinkMoveLink(l, dx, dy))
            SetStatus($"Đã dời Xref {l.DisplayName}: dx = {dx:0.###} m, dy = {dy:0.###} m{(_xmvSnapText.Length > 0 ? " → " + _xmvSnapText : "")}. " +
                      "Bấm tiếp để dời nữa, mũi tên để nhích, Esc để kết thúc, Ctrl+Z hoàn tác.");
        DrawHover();
    }

    /// <summary>Dời tạm các nét / chữ của bản vẽ trên mặt bằng (không vẽ lại cả mặt bằng).</summary>
    private void XrefPreview(string linkId, double dx, double dy)
    {
        string tag = linkId == null ? null : "XREF:" + linkId;
        double sc = double.IsNaN(_rOx) || _rScale <= 0 ? _scale : _rScale;   // toạ độ canvas theo lần dựng gần nhất
        foreach (var o in planCanvas.Children)
        {
            if (o is not FrameworkElement fe || fe.Tag is not string t || !t.StartsWith("XREF:")) continue;
            fe.RenderTransform = tag != null && t == tag ? new TranslateTransform(dx * sc, -dy * sc) : Transform.Identity;
        }
    }

    /// <summary>Lớp phủ: điểm gốc, đường dời, điểm đích.</summary>
    private bool DrawXrefMoveOverlay()
    {
        if (_xmvStep == 0) return false;
        if (_xmvStep == 1 && _lastMouse is Point m)
        {
            var b = XrefPickBase(m);
            if (b.L != null && b.Snapped)
            {
                var c = W2S(b.X, b.Y);
                planOverlay.Children.Add(MakeCircle(c, 7, ClGlyph));
                AddText($"{b.L.DisplayName} ({b.X:0.000}; {b.Y:0.000})", new Point(c.X + 10, c.Y + 6), ClGlyph, 10, false, false, planOverlay);
            }
            return true;
        }
        if (_xmvStep == 2)
        {
            var a = W2S(_xmvBase.X, _xmvBase.Y); var t = W2S(_xmvTo.X, _xmvTo.Y);
            planOverlay.Children.Add(MakeCircle(a, 6, ClGlyph));
            CLine(planOverlay, a, t, ClGlyph, 1.4, true);
            planOverlay.Children.Add(MakeCircle(t, 7, HiBrush));
            AddText($"dx = {_xmvTo.X - _xmvBase.X:0.###}  dy = {_xmvTo.Y - _xmvBase.Y:0.###} m{(_xmvSnapText.Length > 0 ? "  [" + _xmvSnapText + "]" : "")}",
                    new Point(t.X + 10, t.Y + 8), HiBrush, 11, true, false, planOverlay);
            return true;
        }
        return false;
    }

    /// <summary>Esc, phím mũi tên khi đang ở chế độ di chuyển Xref.</summary>
    private bool HandleXrefMoveKey(Key key)
    {
        if (_xmvStep == 0) return false;
        if (key == Key.Escape)
        {
            if (_xmvStep == 2) { _xmvStep = 1; _xmvDragging = false; XrefPreview(null, 0, 0); planHost.ReleaseMouseCapture(); DrawHover(); SetStatus("Đã huỷ lần dời – bấm điểm gốc khác, Esc lần nữa để kết thúc."); return true; }
            XrefMoveEnd();
            return true;
        }
        if (key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            var l = CadLinkById(_xmvLink) ?? VisibleLinks().LastOrDefault();
            if (l == null) return true;
            double st = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 0.1 : (Keyboard.Modifiers & ModifierKeys.Control) != 0 ? 0.001 : 0.01;
            double dx = key == Key.Left ? -st : key == Key.Right ? st : 0, dy = key == Key.Down ? -st : key == Key.Up ? st : 0;
            _xmvLink = l.Id;
            if (CadLinkMoveLink(l, dx, dy)) SetStatus($"Nhích Xref {l.DisplayName} {dx * 1000:+0;-0;0} / {dy * 1000:+0;-0;0} mm (Shift ×10, Ctrl 1 mm) · lệch gốc hiện tại ({l.OffsetX:0.###}; {l.OffsetY:0.###}) m.");
            return true;
        }
        return false;
    }
}

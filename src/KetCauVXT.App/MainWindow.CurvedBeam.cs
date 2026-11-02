using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// DẦM CONG TRÊN MẶT BẰNG: công cụ "＋ Dầm cong" (bấm điểm đầu → điểm cuối → một điểm trên cung, có dây chun hình cung),
// uốn cong / nắn thẳng dầm đang chọn theo độ vồng hoặc bán kính, tiện ích vẽ dầm cong cho mặt bằng, 3D và biểu đồ.
// Thao tác mô hình ở KetCauVXT.Model.ModelEdit (AddCurvedBeam, SetBeamCurvature, SetBeamRadius); hình học ở CurvedBeam.
// =====================================================================
public partial class MainWindow
{
    // chế độ ＋ Dầm cong: điểm đầu = _lineStart, điểm cuối = _arcEnd (đã bắt), điểm thứ ba = vị trí chuột
    private (double X, double Y)? _arcEnd;

    /// <summary>Hình học cung của dầm (null nếu thẳng hoặc dữ liệu nút chưa đủ).</summary>
    private ArcGeometry SafeArc(FrameElement f)
    {
        if (_p == null || f == null || !f.IsCurved) return null;
        try { return CurvedBeam.Arc(_p, f); }
        catch (KeyNotFoundException) { return null; }
        catch (ArgumentException) { return null; }
    }

    /// <summary>Số đoạn vẽ cung (mỗi đoạn ≤ 3°).</summary>
    private static int ArcDrawSegments(ArcGeometry a) => Math.Clamp((int)Math.Ceiling(a.AngleDeg / 3), 8, 120);

    /// <summary>Đường gấp khúc vẽ dầm cong (toạ độ mô hình, m); null nếu dầm thẳng.</summary>
    private List<Vec3> CurvedPolyline(FrameElement f)
    {
        var arc = SafeArc(f);
        return arc?.Points(ArcDrawSegments(arc));
    }

    /// <summary>Điểm màn hình của trục dầm trên mặt bằng: dầm thẳng 2 điểm, dầm cong theo cung.</summary>
    private PointCollection BeamScreenPoints(FrameElement f)
    {
        var pts = new PointCollection();
        var poly = CurvedPolyline(f);
        if (poly != null)
        {
            foreach (var q in poly) pts.Add(W2S(q.X, q.Y));
            return pts;
        }
        var ni = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeI); var nj = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
        if (ni != null && nj != null) { pts.Add(W2S(ni.X, ni.Y)); pts.Add(W2S(nj.X, nj.Y)); }
        return pts;
    }

    /// <summary>Khoảng cách (px) từ điểm tới đường gấp khúc.</summary>
    private static double DistToPolyline(Point s, PointCollection pts)
    {
        double d = double.MaxValue;
        for (int i = 0; i + 1 < pts.Count; i++) d = Math.Min(d, DistToSeg(s, pts[i], pts[i + 1]));
        return d;
    }

    /// <summary>Vẽ dầm cong lên mặt bằng (đường gấp khúc theo cung) và tên dầm tại giữa cung.</summary>
    private void DrawCurvedBeamPlan(FrameElement f, ArcGeometry arc, Brush br, double width, bool labels)
    {
        var pts = new PointCollection();
        foreach (var q in arc.Points(ArcDrawSegments(arc))) pts.Add(W2S(q.X, q.Y));
        planCanvas.Children.Add(new Shapes.Polyline { Points = pts, Stroke = br, StrokeThickness = width, StrokeLineJoin = PenLineJoin.Round });
        if (!labels) return;
        var m = W2S(arc.Mid.X, arc.Mid.Y);
        var c = W2S(arc.Cx, arc.Cy);
        // ghi tên phía lồi của cung (xa tâm)
        var v = m - c; if (v.Length > 1e-9) v.Normalize();
        AddText($"{f.Label} {f.Section}", m + v * 14, LabelBrush, 9, true);
        AddText($"R={arc.Radius:0.00}", m - v * 12, LabelBrush, 8.5, true);
    }

    /// <summary>Dây chun khi vẽ dầm cong: đã có điểm đầu và điểm cuối → cung qua vị trí chuột, ghi R và chiều dài cung.</summary>
    private void DrawArcPreview((double X, double Y) a, (double X, double Y) b, (double X, double Y)? cur, Brush br)
    {
        var pa = W2S(a.X, a.Y); var pb = W2S(b.X, b.Y);
        // dây cung (nét đứt mảnh)
        planOverlay.Children.Add(new Shapes.Line
        {
            X1 = pa.X, Y1 = pa.Y, X2 = pb.X, Y2 = pb.Y, Stroke = br, StrokeThickness = 1, Opacity = 0.5,
            StrokeDashArray = new DoubleCollection { 2, 3 }, IsHitTestVisible = false
        });
        DrawSnapMarker(_lineStory, b, br, true);
        if (cur == null) return;
        double s = CurvedBeam.SagittaThrough(a.X, a.Y, b.X, b.Y, cur.Value.X, cur.Value.Y);
        double z = 0;
        try { z = ModelEdit.StoryZ(_p, _lineStory); } catch (ArgumentException) { }
        var arc = CurvedBeam.Arc(new Vec3(a.X, a.Y, z), new Vec3(b.X, b.Y, z), s);
        if (arc == null)
        {
            AddText("Điểm thứ ba thẳng hàng – di chuột ra khỏi dây cung", new Point((pa.X + pb.X) / 2 + 8, (pa.Y + pb.Y) / 2 - 16), br, 11, false, false, planOverlay);
            return;
        }
        var pts = new PointCollection();
        foreach (var q in arc.Points(ArcDrawSegments(arc))) pts.Add(W2S(q.X, q.Y));
        planOverlay.Children.Add(new Shapes.Polyline
        {
            Points = pts, Stroke = br, StrokeThickness = 2, Opacity = 0.8, StrokeDashArray = new DoubleCollection { 4, 2 }, IsHitTestVisible = false
        });
        var m = W2S(arc.Mid.X, arc.Mid.Y);
        bool tooBig = Math.Abs(s) > arc.Chord;
        AddText($"R = {arc.Radius:0.00} m, L cung = {arc.Length:0.00} m, f = {Math.Abs(s):0.00} m" + (tooBig ? " (quá vồng)" : ""),
            new Point(m.X + 8, m.Y - 16), tooBig ? Brushes.Red : br, 11, false, false, planOverlay);
    }

    /// <summary>Nút "Uốn cong": đặt độ vồng (hoặc bán kính) cho dầm đang chọn; 0 = nắn thẳng; số âm = vồng về phía phải hướng I→J.</summary>
    private void BtnCurveBeam_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null || _busy) return;
        var f = _sel != null ? _p.Frames.FirstOrDefault(q => q.Id == _sel.Id) : null;
        if (f == null || f.IsVerticalKind) { SetStatus("Chọn một dầm trên mặt bằng trước, rồi nhập độ vồng (m) hoặc bán kính R (m) và bấm 'Uốn cong'."); return; }
        double v = Num(txtCurveValue, 0);
        bool byRadius = cbCurveMode.SelectedIndex == 1;
        string snap = Snapshot();
        EditResult r;
        try
        {
            if (byRadius && Math.Abs(v) > 1e-9)
            {
                // giữ phía vồng hiện tại nếu dầm đã cong; số âm đảo phía
                int side = f.CurveMidOffset < 0 ? -1 : 1;
                if (v < 0) side = -side;
                r = ModelEdit.SetBeamRadius(_p, f, Math.Abs(v), side);
            }
            else r = ModelEdit.SetBeamCurvature(_p, f, v);
        }
        catch (Exception ex)
        {
            RestoreSnapshot(snap);
            ShowError("Không uốn cong được dầm", ex);
            return;
        }
        if (!r.Ok) { SetStatus(r.Message); return; }
        AfterEdit(snap, r, f);
    }

    /// <summary>Thông tin dầm cong cho ô thông tin; đồng thời điền độ vồng/bán kính hiện tại vào ô nhập.</summary>
    private string CurvedInfo(FrameElement f)
    {
        var arc = SafeArc(f);
        if (arc == null) return "";
        if (txtCurveValue != null && !txtCurveValue.IsKeyboardFocusWithin)
            txtCurveValue.Text = (cbCurveMode.SelectedIndex == 1 ? arc.Radius : f.CurveMidOffset).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        int n = CurvedBeam.AnalysisSegmentCount(_p, f, arc);
        return $"DẦM CONG: R = {arc.Radius:0.000} m, chiều dài cung L = {arc.Length:0.000} m\n"
             + $"Dây cung {arc.Chord:0.000} m, độ vồng f = {f.CurveMidOffset:0.000} m, góc ở tâm {arc.AngleDeg:0.#}°\n"
             + $"Phân tích: {n} đoạn thẳng; nội lực theo chiều dài cung (hệ trục tiếp tuyến)\n";
    }
}

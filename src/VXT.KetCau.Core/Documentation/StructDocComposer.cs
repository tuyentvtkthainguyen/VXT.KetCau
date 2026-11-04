using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VXT.KetCau.Core.Codes;
using VXT.KetCau.Core.Detailing;
using VXT.KetCau.Core.Drawing;
using VXT.KetCau.Core.Geometry;
using VXT.KetCau.Core.Model;
using VXT.KetCau.Core.Workflow;

namespace VXT.KetCau.Core.Documentation
{
    /// <summary>
    /// Các hình của hồ sơ kết cấu mà ShopDrawingComposer chưa có: mặt bằng móng, mặt bằng dầm sàn,
    /// mặt bằng thép sàn, chi tiết móng, ghi chú chung. Nhãn thép theo quy tắc Kết cấu Soft.
    /// </summary>
    public class StructDocComposer
    {
        readonly IDraw2D w;
        readonly StructModel m;
        readonly WorkflowResult res;
        readonly DesignSettings set;
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        double s = 100;
        double TH => 2.5 * s;
        string Sym => set.DiaSymbol;

        public StructDocComposer(IDraw2D surface, StructModel model, WorkflowResult result)
        {
            w = surface; m = model; res = result; set = model.Settings;
        }

        /// <summary>Phần tử nguồn → cấu kiện đã gộp, để nhãn trên mặt bằng khớp hình chi tiết và bảng thống kê.</summary>
        DetailedElement Owner(string id) => res.Elements.FirstOrDefault(e => e.SourceIds.Contains(id));
        string Lbl(StructElem e) => Owner(e.Id)?.Label ?? e.Label ?? "";

        IEnumerable<(string Name, Seg2 Line)> GridList => m.Grids.Select(g => (g.Name, g.Line));

        void Extents(IEnumerable<ColumnElem> cols, out double x0, out double y0, out double x1, out double y1, double pad = 0)
        {
            var pts = cols.Select(c => c.Location).Concat(m.Slabs.SelectMany(q => q.Boundary)).ToList();
            if (pts.Count == 0) pts = m.Grids.SelectMany(g => new[] { g.Line.A, g.Line.B }).ToList();
            x0 = pts.Min(p => p.X) - pad; y0 = pts.Min(p => p.Y) - pad; x1 = pts.Max(p => p.X) + pad; y1 = pts.Max(p => p.Y) + pad;
        }

        void Column(ColumnElem c)
        {
            var sec = m.Section(c.Section);
            double r = c.RotationDeg * Math.PI / 180, cs = Math.Cos(r), sn = Math.Sin(r), hb = sec.B / 2, hh = sec.H / 2;
            var pts = new[] { (-hb, -hh), (hb, -hh), (hb, hh), (-hb, hh) }
                .Select(p => (c.Location.X + p.Item1 * cs - p.Item2 * sn, c.Location.Y + p.Item1 * sn + p.Item2 * cs)).ToList();
            w.HatchX("HATCH", pts, "SOLID", 1.5 * s);
            w.Poly("COT", pts, true);
        }

        // ================================================================== MẶT BẰNG MÓNG
        public void DrawFoundationPlan(double scale)
        {
            s = scale; w.SetScaleX(s);
            var ann = new PlanAnnotation(w, TH);
            var baseName = m.Stories[0].Name;
            var cols = m.Columns.Where(c => c.BaseStory == baseName).ToList();
            Extents(cols, out var x0, out var y0, out var x1, out var y1);
            foreach (var f in m.Footings)
            {
                x0 = Math.Min(x0, f.Center.X - f.L / 2); x1 = Math.Max(x1, f.Center.X + f.L / 2);
                y0 = Math.Min(y0, f.Center.Y - f.B / 2); y1 = Math.Max(y1, f.Center.Y + f.B / 2);
            }
            // giằng móng: dầm ở tầng thấp nhất có dầm
            var ground = m.Stories.Count > 1 ? m.Stories[1].Name : null;
            foreach (var b in m.Beams.Where(b => b.Story == ground)) Beam(b, "GIANG");
            foreach (var f in m.Footings)
            {
                w.Rect("BE_TONG", f.Center.X - f.L / 2, f.Center.Y - f.B / 2, f.Center.X + f.L / 2, f.Center.Y + f.B / 2);
                w.Text("CHU", f.Center.X + f.L / 2 - TH * 0.5, f.Center.Y + f.B / 2 - TH * 1.3, TH, Lbl(f), 2, 0, 0);
            }
            foreach (var c in cols) { Column(c); }
            ann.Grids(GridList, x0, y0, x1, y1, 0);
            ann.Title((x0 + x1) / 2, y0 - ann.ChainOffset(1) - TH * 7.5, $"MẶT BẰNG MÓNG ({ShopDrawingComposer.LevelText(m.Stories[0].Elevation)})", s);
        }

        void Beam(BeamElem b, string layer)
        {
            var sec = m.Section(b.Section);
            var d = (b.End - b.Start).Unit; var n = new P2(-d.Y, d.X);
            double t = sec.B / 2;
            var p0 = b.Start + n * t; var p1 = b.End + n * t; var p2 = b.End - n * t; var p3 = b.Start - n * t;
            w.Line(layer, p0.X, p0.Y, p1.X, p1.Y);
            w.Line(layer, p3.X, p3.Y, p2.X, p2.Y);
        }

        void BeamLabel(BeamElem b)
        {
            var sec = m.Section(b.Section);
            var d = (b.End - b.Start).Unit; if (d.X < -0.01 || (Math.Abs(d.X) < 0.01 && d.Y < 0)) d = d * -1;
            var n = new P2(-d.Y, d.X);
            var mid = (b.Start + b.End) * 0.5 + n * (sec.B / 2 + TH * 0.35);
            double rot = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
            w.Text("CHU", mid.X, mid.Y, TH * 0.9, $"{Lbl(b)} ({sec.B:0}x{sec.H:0})", 1, 0, rot);
        }

        // ================================================================== MẶT BẰNG DẦM SÀN
        public void DrawFramingPlan(Story st, double scale)
        {
            s = scale; w.SetScaleX(s);
            var ann = new PlanAnnotation(w, TH);
            var cols = m.Columns.Where(c => Math.Abs(c.TopZ - st.Elevation) < 1).ToList();
            Extents(cols, out var x0, out var y0, out var x1, out var y1);
            foreach (var q in m.Slabs.Where(q => q.Story == st.Name))
            {
                var c = Center(q.Boundary);
                w.Text("CHU", c.X, c.Y + TH * 0.2, TH, q.Label ?? "", 1, 2, 0);
                var sec = m.SlabSection(q.Section);
                if (sec != null) w.Text("CHU", c.X, c.Y - TH * 1.4, TH * 0.9, "h=" + sec.Thickness.ToString("0", Ci), 1, 2, 0);
            }
            foreach (var b in m.Beams.Where(b => b.Story == st.Name)) Beam(b, "BE_TONG");
            // mỗi dầm liên tục ghi tên một lần, ở nhịp giữa
            foreach (var ch in res.BeamChains.Where(q => q.Story == st.Name && q.Spans.Count > 0))
                BeamLabel(ch.Spans[(ch.Spans.Count - 1) / 2]);
            foreach (var c in cols)
            {
                Column(c);
                var sec = m.Section(c.Section);
                if (!string.IsNullOrEmpty(c.Label) && !c.Label.StartsWith("TMP"))
                    w.Text("CHU", c.Location.X + sec.B / 2 + TH * 0.3, c.Location.Y - sec.H / 2 - TH * 1.2, TH * 0.9, c.Label, 0, 0, 0);
            }
            ann.Grids(GridList, x0, y0, x1, y1, 0);
            string name = IsTieLevel(m, st) ? "MẶT BẰNG GIẰNG MÓNG" : "MẶT BẰNG DẦM SÀN " + st.Name.ToUpperInvariant();
            ann.Title((x0 + x1) / 2, y0 - ann.ChainOffset(1) - TH * 7.5, $"{name} ({ShopDrawingComposer.LevelText(st.Elevation)})", s);
        }

        /// <summary>Cao độ thấp nhất có dầm mà không có sàn: đó là giằng móng.</summary>
        public static bool IsTieLevel(StructModel model, Story st) => model.Stories.IndexOf(st) == 1 && !model.Slabs.Any(q => q.Story == st.Name);

        /// <summary>Móng chồng lên nhau: móng đơn không còn phù hợp ở vị trí đó.</summary>
        public static List<string> FootingOverlaps(StructModel model)
        {
            var res = new List<string>();
            var f = model.Footings;
            for (int i = 0; i < f.Count; i++)
                for (int j = i + 1; j < f.Count; j++)
                {
                    double dx = Math.Abs(f[i].Center.X - f[j].Center.X), dy = Math.Abs(f[i].Center.Y - f[j].Center.Y);
                    if (dx < (f[i].L + f[j].L) / 2 && dy < (f[i].B + f[j].B) / 2)
                        res.Add($"Móng {f[i].Label} và {f[j].Label} chồng lên nhau — xem lại kích thước hoặc đổi sang móng băng, móng bè.");
                }
            return res;
        }

        static P2 Center(IList<P2> poly) => new P2(poly.Average(p => p.X), poly.Average(p => p.Y));

        // ================================================================== MẶT BẰNG THÉP SÀN
        /// <summary>Hệ toạ độ ô sàn — cùng quy tắc với ColumnSlabDetailer để đổi vị trí thanh giữa các ô giống hệt.</summary>
        static SlabGeometry Frame(SlabElem q, StructModel model)
        {
            var p = q.Boundary;
            var e0 = p[1] - p[0]; var e1 = p[2] - p[1];
            bool firstShort = e0.Length <= e1.Length;
            var origin = firstShort ? p[0] : p[1];
            var u = (firstShort ? e0 : e1).Unit;
            var g = new SlabGeometry { Origin = origin, U = u, L1 = Math.Min(e0.Length, e1.Length), L2 = Math.Max(e0.Length, e1.Length), TopZ = q.TopZ };
            var c = Center(p);
            if ((c - origin).Dot(g.V) < 0) { g.Origin = origin + u * g.L1; g.U = u * -1; }
            return g;
        }

        public void DrawSlabRebarPlan(Story st, double scale)
        {
            s = scale; w.SetScaleX(s);
            var ann = new PlanAnnotation(w, TH);
            var cols = m.Columns.Where(c => Math.Abs(c.TopZ - st.Elevation) < 1).ToList();
            Extents(cols, out var x0, out var y0, out var x1, out var y1);
            foreach (var b in m.Beams.Where(b => b.Story == st.Name)) Beam(b, "BE_TONG");
            foreach (var c in cols) Column(c);

            foreach (var q in m.Slabs.Where(q => q.Story == st.Name && q.Boundary.Count == 4))
            {
                var e = Owner(q.Id);
                if (e == null || !(e.Geometry is SlabGeometry og)) continue;
                var g = Frame(q, m);
                foreach (var bar in e.Bars)
                {
                    bool first = true;
                    foreach (var pl in bar.Placements)
                    {
                        // Đưa đường thanh của ô gốc về ô này qua toạ độ cục bộ (u, v)
                        var mapped = pl.Path.Select(p =>
                        {
                            var rel = p.XY - og.Origin;
                            double uu = rel.Dot(og.U), vv = rel.Dot(og.V);
                            return (u: uu, v: vv, z: p.Z);
                        }).ToList();
                        // thanh đại diện: lớp dưới ở 35% dải rải, thép mũ ở 65% để hai loại không đè lên nhau
                        double spread = (pl.Count - 1) * pl.Spacing * (bar.Role == BarRole.SlabTop ? 0.65 : 0.35);
                        bool alongU = Math.Abs(mapped.First().u - mapped.Last().u) > Math.Abs(mapped.First().v - mapped.Last().v);
                        var plan = mapped.Select(k => alongU ? g.ToGlobal(k.u, k.v + spread, 0).XY : g.ToGlobal(k.u + spread, k.v, 0).XY).ToList();
                        var clean = new List<P2>();
                        foreach (var p in plan) if (clean.Count == 0 || clean[clean.Count - 1].DistanceTo(p) > 1) clean.Add(p);
                        if (clean.Count < 2) continue;
                        string lay = bar.Role == BarRole.SlabTop ? "THEP" : "THEP";
                        w.Poly(lay, clean.Select(p => (p.X, p.Y)).ToList());
                        // móc: thanh mũ gạch móc ngắn ở đầu để phân biệt lớp trên
                        var dir = (clean[clean.Count - 1] - clean[0]).Unit; var nrm = new P2(-dir.Y, dir.X);
                        if (bar.Role == BarRole.SlabTop)
                            foreach (var endP in new[] { clean[0], clean[clean.Count - 1] })
                                w.Line(lay, endP.X, endP.Y, endP.X + nrm.X * TH * 0.5, endP.Y + nrm.Y * TH * 0.5);
                        if (first)
                        {
                            var mid = clean[0] + (clean[clean.Count - 1] - clean[0]) * (bar.Role == BarRole.SlabTop ? 0.5 : bar.Position == "L1" ? 0.7 : 0.15);
                            double ox = TH * 0.8, oy = TH * 0.9;
                            w.Tag(mid.X, mid.Y, mid.X + ox, mid.Y + oy, bar.TagText(Sym), bar.Mark, TH * 0.9);
                            first = false;
                        }
                    }
                }
                var c = Center(q.Boundary);
                w.Text("CHU", c.X - g.L1 * 0.28 * Math.Abs(g.U.X) - g.L2 * 0.28 * Math.Abs(g.V.X), c.Y - g.L1 * 0.28 * Math.Abs(g.U.Y) - g.L2 * 0.28 * Math.Abs(g.V.Y), TH, e.Label ?? "", 1, 2, 0);
            }
            ann.Grids(GridList, x0, y0, x1, y1, 0);
            ann.Title((x0 + x1) / 2, y0 - ann.ChainOffset(1) - TH * 7.5, $"MẶT BẰNG THÉP SÀN {st.Name.ToUpperInvariant()} ({ShopDrawingComposer.LevelText(st.Elevation)})", s);
        }

        // ================================================================== CHI TIẾT MÓNG
        /// <summary>Mặt bằng + mặt cắt móng đơn, hình phóng k lần so với tỉ lệ tờ.</summary>
        public void DrawFootingDetail(DetailedElement e, double scale, double k)
        {
            s = scale; w.SetScaleX(s);
            var f = m.Footings.FirstOrDefault(q => e.SourceIds.Contains(q.Id));
            if (f == null) return;
            var col = m.Columns.FirstOrDefault(c => c.Id == f.ColumnId);
            var sec = col != null ? m.Section(col.Section) : null;
            double bc = (sec?.B ?? 220) * k, hc = (sec?.H ?? 220) * k;
            double L = f.L * k, B = f.B * k, H = f.H * k, cover = set.CoverFooting * k;
            var barX = e.Bars.FirstOrDefault(b => b.Position == "X") ?? e.Bars.First();
            var barY = e.Bars.FirstOrDefault(b => b.Position == "Y") ?? e.Bars.Last();

            // Mặt bằng: tâm (0,0)
            w.Rect("BE_TONG", -L / 2, -B / 2, L / 2, B / 2);
            w.Rect("BE_TONG", -L / 2 - 100 * k, -B / 2 - 100 * k, L / 2 + 100 * k, B / 2 + 100 * k);
            var colPts = new[] { (-bc / 2, -hc / 2), (bc / 2, -hc / 2), (bc / 2, hc / 2), (-bc / 2, hc / 2) };
            w.HatchX("HATCH", colPts, "SOLID", 1.5 * s);
            w.Poly("COT", colPts, true);
            // vài thanh đại diện mỗi phương
            double sx = barX.Spacing * k, sy = barY.Spacing * k;
            for (int i = -2; i <= 2; i++)
            {
                double y = i * sx; if (Math.Abs(y) < B / 2 - cover) w.Line("THEP", -L / 2 + cover, y, L / 2 - cover, y);
                double x = i * sy; if (Math.Abs(x) < L / 2 - cover) w.Line("THEP", x, -B / 2 + cover, x, B / 2 - cover);
            }
            w.Tag(L / 2 - cover - TH * 2, sx, L / 2 + TH, B / 2 + TH * 1.5, barX.TagText(Sym), barX.Mark, TH);
            w.Tag(sy, -B / 2 + cover + TH * 2, L / 2 + TH, -B / 2 - TH * 1.5, barY.TagText(Sym), barY.Mark, TH);
            w.DimH(-L / 2, L / 2, -B / 2 - 100 * k - TH * 2.5, TH, f.L.ToString("0", Ci));
            w.DimV(-L / 2 - 100 * k - TH * 2.5, -B / 2, B / 2, TH, f.B.ToString("0", Ci));
            w.TextUnderlined("CHU", 0, -B / 2 - 100 * k - TH * 6, TH * 1.4, e.Title);
            w.Text("CHU", 0, -B / 2 - 100 * k - TH * 8.2, TH, "Mặt bằng   tl: 1/" + (s / k).ToString("0", Ci), 1, 0, 0);

            // Mặt cắt bên phải
            double ox = L / 2 + TH * 14 + L / 2, oy = 0;
            double stub = 600 * k;
            w.Rect("BE_TONG", ox - L / 2, oy, ox + L / 2, oy + H);
            w.Rect("BE_TONG", ox - L / 2 - 100 * k, oy - 100 * k, ox + L / 2 + 100 * k, oy);
            w.Line("BE_TONG", ox - bc / 2, oy + H, ox - bc / 2, oy + H + stub);
            w.Line("BE_TONG", ox + bc / 2, oy + H, ox + bc / 2, oy + H + stub);
            double hook = barX.Segments.Count >= 3 ? barX.Segments[0] * k : H - 2 * cover;
            w.Poly("THEP", new[] { (ox - L / 2 + cover, oy + cover + hook), (ox - L / 2 + cover, oy + cover), (ox + L / 2 - cover, oy + cover), (ox + L / 2 - cover, oy + cover + hook) });
            int nDots = Math.Max(3, Math.Min(9, (int)((L - 2 * cover) / (sy))));
            for (int i = 0; i < nDots; i++)
            {
                double x = ox - L / 2 + cover + (L - 2 * cover) * (i + 0.5) / nDots;
                w.Dot("THEP", x, oy + cover + barX.D * k * 1.2, barY.D / 2.0 * k);
            }
            // thép chờ cột
            var colEl = col != null ? Owner(col.Id) : null;
            var main = colEl?.Bars.FirstOrDefault(b => b.Role == BarRole.ColumnMain);
            double cc = set.CoverColumn * k + 10 * k;
            foreach (var xx in new[] { -bc / 2 + cc, bc / 2 - cc })
                w.Poly("THEP", new[] { (ox + xx, oy + H + stub), (ox + xx, oy + cover + 30 * k), (ox + xx + (xx < 0 ? -200 * k : 200 * k), oy + cover + 30 * k) });
            if (main != null) w.Tag(ox + bc / 2 - cc, oy + H + stub * 0.5, ox + bc / 2 + TH, oy + H + stub * 0.85, main.TagText(Sym), main.Mark, TH);
            w.Tag(ox + L / 4, oy + cover, ox + L / 2 + TH * 1.5, oy + H * 0.5, barX.TagText(Sym), barX.Mark, TH);
            w.DimH(ox - L / 2, ox + L / 2, oy - 100 * k - TH * 2.5, TH, f.L.ToString("0", Ci));
            w.DimV(ox - L / 2 - 100 * k - TH * 1.5, oy, oy + H, TH, f.H.ToString("0", Ci));
            w.Text("CHU", ox - L / 2 - 100 * k, oy - 100 * k - TH * 1.0, TH * 0.8, "Lót BT đá 4x6 dày 100", 0, 0, 0);
            w.Text("CHU", ox, oy - 100 * k - TH * 6, TH * 1.1, $"Mặt cắt   tl: 1/{(s / k).ToString("0", Ci)}", 1, 0, 0);
            var pad = new PlanAnnotation(w, TH);
            pad.Level(ox + L / 2 + 100 * k + TH * 1.5, oy, f.BottomZ, "đáy móng");
        }

        // ================================================================== GHI CHÚ CHUNG
        public void DrawGeneralNotes(double scale)
        {
            s = scale; w.SetScaleX(s);
            var C = ConcreteGrade.Get(set.Concrete); var S = SteelGrade.Get(set.MainSteel); var Sw = SteelGrade.Get(set.StirrupSteel);
            double y = 0, line = TH * 1.8;
            void H(string t) { y -= line * 0.6; w.Text("CHU", 0, y, TH * 1.2, t, 0, 0, 0); y -= line; }
            void T(string t) { w.Text("CHU", TH, y, TH, t, 0, 0, 0); y -= line; }
            w.TextUnderlined("CHU", TH * 26, y, TH * 1.8, "GHI CHÚ CHUNG KẾT CẤU");
            y -= line * 1.5;
            H("1. VẬT LIỆU");
            T($"- Bê tông {C.Name}: Rb = {C.Rb.ToString("0.0", Ci)} MPa, Rbt = {C.Rbt.ToString("0.00", Ci)} MPa (TCVN 5574:2018)");
            T($"- Thép dọc {S.Name}: Rs = {S.Rs.ToString("0", Ci)} MPa");
            T($"- Thép đai {Sw.Name}: Rs = {Sw.Rs.ToString("0", Ci)} MPa");
            H("2. LỚP BÊ TÔNG BẢO VỆ (mm)");
            T($"- Dầm {set.CoverBeam:0}; Cột {set.CoverColumn:0}; Sàn {set.CoverSlab:0}; Móng {set.CoverFooting:0}");
            H("3. NEO VÀ NỐI CHỒNG THÉP CHỊU KÉO (mm)");
            var dias = new[] { 12, 14, 16, 18, 20, 22, 25 };
            double u = TH / 125.0 * 2;
            var cw = new[] { 800 * u }.Concat(dias.Select(_ => 450.0 * u)).ToArray();
            var rows = new List<string[]>
            {
                new[] { "Đường kính" }.Concat(dias.Select(d => Sym + d)).ToArray(),
                new[] { "Chiều dài neo" }.Concat(dias.Select(d => Tcvn5574.Anchorage(d, S, C).ToString("0", Ci))).ToArray(),
                new[] { "Chiều dài nối" }.Concat(dias.Select(d => Tcvn5574.LapLength(d, S, C).ToString("0", Ci))).ToArray(),
            };
            y -= SimpleTable.Draw(w, TH, y, TH * 0.9, cw, TH * 2.2, rows) + line;
            H("4. QUY ĐỊNH THỂ HIỆN");
            T("- Kích thước ghi bằng mm, cao độ ghi bằng m.");
            T($"- Thép dọc ghi {Sym.ToString()}: số thanh + đường kính, ví dụ 3{Sym}22. Đai và thép rải ghi {Sym}d a s, ví dụ {Sym}6a100.");
            T("- Mỗi cấu kiện đánh số thép riêng từ 1. Số hiệu trên hình khớp với bảng thống kê.");
            H("5. LƯU Ý");
            T("- Kiểm tra kích thước với hồ sơ kiến trúc trước khi thi công.");
            T("- Nội lực, sức chịu tải nền và tải trọng do kỹ sư thiết kế kiểm tra và chịu trách nhiệm.");
        }

        // ================================================================== BẢNG THỐNG KÊ THEO TRANG
        /// <summary>Chia bảng thống kê theo cấu kiện để mỗi phần vừa một tờ. Không tách một cấu kiện qua hai bảng khi tránh được.</summary>
        public static List<RebarSchedule> SplitSchedule(RebarSchedule all, int maxRows)
        {
            var pages = new List<RebarSchedule>();
            var cur = new RebarSchedule();
            foreach (var grp in all.Rows.GroupBy(r => r.Element))
            {
                var rows = grp.ToList();
                if (cur.Rows.Count > 0 && cur.Rows.Count + rows.Count > maxRows) { pages.Add(cur); cur = new RebarSchedule(); }
                cur.Rows.AddRange(rows);
                while (cur.Rows.Count > maxRows)
                {
                    var over = new RebarSchedule();
                    over.Rows.AddRange(cur.Rows.Take(maxRows));
                    pages.Add(over);
                    var rest = cur.Rows.Skip(maxRows).ToList();
                    cur = new RebarSchedule();
                    cur.Rows.AddRange(rest);
                }
            }
            if (cur.Rows.Count > 0) pages.Add(cur);
            return pages;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using VXT.KetCau.Core.Codes;
using VXT.KetCau.Core.Design;
using VXT.KetCau.Core.Detailing;
using VXT.KetCau.Core.Geometry;
using VXT.KetCau.Core.Input;
using VXT.KetCau.Core.Model;

namespace VXT.KetCau.Core.Workflow
{
    /// <summary>Kết quả toàn bộ quy trình: các cấu kiện đã cấu tạo + bảng thống kê + cảnh báo.</summary>
    public class WorkflowResult
    {
        public List<BeamChain> BeamChains { get; } = new List<BeamChain>();
        public List<DetailedElement> Elements { get; } = new List<DetailedElement>();
        public RebarSchedule Schedule { get; set; }
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// Điều phối: (nội lực sơ bộ nếu chưa có kết quả phân tích) → thiết kế TCVN 5574:2018 → cấu tạo, đánh số, gộp cấu kiện → thống kê.
    /// </summary>
    public class StructuralWorkflow
    {
        readonly StructModel m;
        readonly ConcreteGrade C;
        readonly SteelGrade S, Sw;
        public double SoilBearing { get; set; } = 150;

        public StructuralWorkflow(StructModel model)
        {
            m = model;
            C = ConcreteGrade.Get(m.Settings.Concrete);
            S = SteelGrade.Get(m.Settings.MainSteel);
            Sw = SteelGrade.Get(m.Settings.StirrupSteel);
        }

        // ------------------------------------------------------------ Nội lực sơ bộ (khi chưa chạy ETABS/SAP)
        /// <summary>
        /// Ước lượng nội lực bằng phương pháp diện truyền tải: dầm M⁻ = qL²/12, M⁺ = qL²/14, V = 0.6qL;
        /// cột N = tổng tải truyền xuống. Chỉ dùng thiết kế sơ bộ — hồ sơ phát hành phải dùng kết quả phân tích.
        /// </summary>
        public void PreliminaryForces()
        {
            double gl = 1.25;
            var beamQ = new Dictionary<BeamElem, double>();
            foreach (var b in m.Beams)
            {
                var sec = m.Section(b.Section);
                double q = 1.1 * 25 * sec.B / 1000 * sec.H / 1000;                                   // bản thân
                q += m.LineLoads.Where(l => l.BeamId == b.Id).Sum(l => l.Value) * Tcvn2737.SuperDeadFactor; // tường
                var seg = new Seg2(b.Start, b.End);
                foreach (var s in m.Slabs.Where(s => s.Story == b.Story))
                {
                    // ô sàn có cạnh trùng dầm → truyền 1/2 bề rộng ô vuông góc (tải tương đương phân bố đều)
                    var edges = Enumerable.Range(0, s.Boundary.Count).Select(i => new Seg2(s.Boundary[i], s.Boundary[(i + 1) % s.Boundary.Count]));
                    var e = edges.FirstOrDefault(x => seg.DistanceTo(x.Mid) < Tol.Snap && Math.Abs(x.Dir.Dot(seg.Dir)) > 0.95);
                    if (e == null) continue;
                    double area = Math.Abs(GeomUtil.Area(s.Boundary)) / 1e6;
                    double width = area / (e.Length / 1000);
                    var t = m.SlabSection(s.Section).Thickness;
                    double sdl = m.AreaLoads.Where(a => a.SlabId == s.Id && a.Pattern == "HTHT").Sum(a => a.Value);
                    double qs = 1.1 * 25 * t / 1000 + Tcvn2737.SuperDeadFactor * sdl + gl * s.LiveLoad;
                    q += qs * width / 2 * 0.85; // hệ số quy đổi tải hình thang/tam giác
                }
                beamQ[b] = q;
                double L = b.Length / 1000;
                b.Forces = new BeamForces
                {
                    MnegLeft = -q * L * L / 12, MnegRight = -q * L * L / 12, MposSpan = q * L * L / 14,
                    VmaxLeft = 0.6 * q * L, VmaxRight = 0.6 * q * L,
                };
            }
            // cột: cộng dồn từ trên xuống
            foreach (var grp in m.Columns.GroupBy(c => (Math.Round(c.Location.X), Math.Round(c.Location.Y))))
            {
                double N = 0;
                foreach (var c in grp.OrderByDescending(c => c.TopZ))
                {
                    var st = m.Stories.OrderBy(s => Math.Abs(s.Elevation - c.TopZ)).First();
                    foreach (var b in m.Beams.Where(b => b.Story == st.Name))
                    {
                        if (b.Start.Equals(c.Location) || b.End.Equals(c.Location)) N += beamQ[b] * b.Length / 1000 / 2;
                    }
                    var sec = m.Section(c.Section);
                    N += 1.1 * 25 * sec.B / 1000 * sec.H / 1000 * c.Length / 1000;
                    c.Forces = new ColumnForces();
                    // mô men do khung chưa biết → chỉ kể độ lệch tâm ngẫu nhiên ea (trong ColumnDesigner)
                    c.Forces.Sets.Add(new ColumnForceSet { Combo = "SƠ BỘ", Location = "Chân", N = N });
                }
            }
        }

        // ------------------------------------------------------------ Thiết kế
        public void DesignAll(List<string> warnings)
        {
            var set = m.Settings;
            var bd = new BeamDesigner(C, S, Sw, set.CoverBeam);
            foreach (var b in m.Beams)
            {
                var sec = m.Section(b.Section);
                var f = b.Forces;
                bool manual = !string.IsNullOrWhiteSpace(b.ManualTop) && !string.IsNullOrWhiteSpace(b.ManualBottom) && !string.IsNullOrWhiteSpace(b.ManualStirrupSupport);
                if (f == null) { if (!manual) warnings.Add($"Dầm {b.Label ?? b.Id} chưa có nội lực"); f = new BeamForces(); }
                b.Design = bd.Design(sec.B, sec.H, f.MnegLeft, f.MposSpan, f.MnegRight, f.VmaxLeft, f.VmaxRight, set.BeamBarDiameters, set.StirrupDiameters, f.Tmax);
                ApplyManual(b, warnings);
                foreach (var w in b.Design.Warnings) warnings.Add($"Dầm {b.Label ?? b.Id} ({b.Story}): {w}");
            }

            var cd = new ColumnDesigner(C, S, set.CoverColumn);
            foreach (var c in m.Columns)
            {
                var sec = m.Section(c.Section);
                c.Design = cd.Design(sec.B, sec.H, c.Length, c.Forces?.Sets, set.ColumnBarDiameters, set.StirrupDiameters);
                ApplyManual(c, sec, warnings);
                foreach (var w in c.Design.Warnings) warnings.Add($"Cột tại {c.Location} ({c.BaseStory}→{c.TopStory}): {w}");
            }

            var sd = new SlabDesigner(C, S, set.CoverSlab);
            foreach (var s in m.Slabs)
            {
                var t = m.SlabSection(s.Section).Thickness;
                if (s.Boundary.Count < 3) continue;
                double l1 = s.Boundary[0].DistanceTo(s.Boundary[1]), l2 = s.Boundary.Count > 2 ? s.Boundary[1].DistanceTo(s.Boundary[2]) : l1;
                double extra = m.AreaLoads.Where(a => a.SlabId == s.Id && a.Pattern == "HTHT").Sum(a => a.Value);
                double q = 1.1 * 25 * t / 1000 + Tcvn2737.SuperDeadFactor * extra + Tcvn2737.LiveFactor(s.LiveLoad) * s.LiveLoad;
                s.Design = sd.Design(t, l1, l2, q, set.SlabBarDiameters, s.Forces);
                if (!string.IsNullOrWhiteSpace(s.ManualBottom)) { s.Design.Bottom1 = RebarText.ParseSlab(s.ManualBottom); s.Design.Bottom2 = RebarText.ParseSlab(s.ManualBottom); }
                if (!string.IsNullOrWhiteSpace(s.ManualTop)) { s.Design.Top1 = RebarText.ParseSlab(s.ManualTop); s.Design.Top2 = RebarText.ParseSlab(s.ManualTop); }
                foreach (var w in s.Design.Warnings) warnings.Add($"Sàn {s.Label}: {w}");
            }
        }

        /// <summary>Thép người dùng đặt thay kết quả tự chọn; nếu nội lực có sẵn thì so với diện tích cần để cảnh báo.</summary>
        static void ApplyManual(BeamElem b, List<string> warnings)
        {
            var d = b.Design;
            string who = $"Dầm {b.Label ?? b.Id} ({b.Story})";
            void Check(FlexureResult req, BarGroup g, string where)
            {
                if (req != null && req.AsReq > g.Area * 1.001)
                    warnings.Add($"{who}: thép đặt {where} {g.Text()} ({g.Area:0} mm²) nhỏ hơn yêu cầu {req.AsReq:0} mm²");
            }
            if (!string.IsNullOrWhiteSpace(b.ManualTop))
            {
                var g = RebarText.ParseBars(b.ManualTop);
                Check(d.TopLeft, g, "trên gối trái"); Check(d.TopRight, g, "trên gối phải");
                d.TopLeftBars = g; d.TopRightBars = g;
            }
            if (!string.IsNullOrWhiteSpace(b.ManualBottom))
            {
                var g = RebarText.ParseBars(b.ManualBottom);
                Check(d.Bottom, g, "dưới");
                d.BottomBars = g;
            }
            if (!string.IsNullOrWhiteSpace(b.ManualStirrupSupport)) d.StirrupSupport = RebarText.ParseStirrup(b.ManualStirrupSupport);
            if (!string.IsNullOrWhiteSpace(b.ManualStirrupSpan)) d.StirrupSpan = RebarText.ParseStirrup(b.ManualStirrupSpan);
            else if (!string.IsNullOrWhiteSpace(b.ManualStirrupSupport)) d.StirrupSpan = RebarText.ParseStirrup(b.ManualStirrupSupport);
        }

        static void ApplyManual(ColumnElem c, ConcreteSection sec, List<string> warnings)
        {
            var d = c.Design;
            if (!string.IsNullOrWhiteSpace(c.ManualBars))
            {
                var g = RebarText.ParseBars(c.ManualBars);
                double asAuto = d.As;
                var (nb, nh) = RebarText.SplitColumnBars(g.N, sec.B, sec.H);
                d.Nb = nb; d.Nh = nh; d.D = g.D;
                if (c.Forces != null && d.As < asAuto * 0.999)
                    warnings.Add($"Cột tại {c.Location} ({c.BaseStory}→{c.TopStory}): thép đặt {g.Text()} ({d.As:0} mm²) ít hơn thép tự chọn ({asAuto:0} mm²)");
            }
            if (!string.IsNullOrWhiteSpace(c.ManualStirrup))
            {
                var st = RebarText.ParseStirrup(c.ManualStirrup);
                d.Stirrup = st;
                d.StirrupDense = new StirrupSpec { D = st.D, Legs = st.Legs, S = Math.Max(100, st.S / 2) };
            }
        }

        // ------------------------------------------------------------ Cấu tạo + thống kê
        public WorkflowResult DetailAll()
        {
            var res = new WorkflowResult();
            DesignAll(res.Warnings);
            var set = m.Settings;

            // Dầm
            var bdet = new BeamDetailer(set);
            res.BeamChains.AddRange(BeamChainBuilder.Build(m));
            var beamEls = new List<DetailedElement>();
            foreach (var ch in res.BeamChains)
            {
                try { var e = bdet.Detail(ch); beamEls.Add(e); foreach (var b in ch.Spans) b.Label = ch.Label; }
                catch (Exception ex) { res.Warnings.Add($"Dầm {ch.Label}: {ex.Message}"); }
            }

            // Cột
            var csd = new ColumnSlabDetailer(set);
            var colEls = new List<DetailedElement>();
            foreach (var grp in m.Columns.GroupBy(c => (Math.Round(c.Location.X), Math.Round(c.Location.Y))))
            {
                var stack = grp.OrderBy(c => c.BaseZ).ToList();
                for (int i = 0; i < stack.Count; i++)
                {
                    var c = stack[i];
                    c.Label = "TMP" + c.Id;
                    var st = m.Stories.FirstOrDefault(s => Math.Abs(s.Elevation - c.TopZ) < 1);
                    double hb = m.Beams.Where(b => b.Story == st?.Name && (b.Start.Equals(c.Location) || b.End.Equals(c.Location)))
                                       .Select(b => m.Section(b.Section).H).DefaultIfEmpty(300).Max();
                    var ft = i == 0 ? m.Footings.FirstOrDefault(f => f.ColumnId == c.Id) : null;
                    try { colEls.Add(csd.DetailColumn(c, m.Section(c.Section), i + 1 < stack.Count ? stack[i + 1] : null, ft, hb)); }
                    catch (Exception ex) { res.Warnings.Add($"Cột {c.Location}: {ex.Message}"); }
                }
            }

            // Sàn
            Func<P2, P2, double> bw = (a, b) =>
            {
                var mid = (a + b) * 0.5;
                var beam = m.Beams.FirstOrDefault(x => new Seg2(x.Start, x.End).DistanceTo(mid) < Tol.Snap);
                return beam != null ? m.Section(beam.Section).B : 0;
            };
            var slabEls = csd.DetailSlabs(m, m.Slabs, bw);

            // Móng
            var fd = new FootingDesigner(C, S, set.CoverFooting) { SoilR = SoilBearing };
            var footEls = new List<DetailedElement>();
            foreach (var f in m.Footings)
            {
                var col = m.Columns.FirstOrDefault(c => c.Id == f.ColumnId);
                if (col?.Forces == null || col.Forces.Sets.Count == 0) continue;
                var sec = m.Section(col.Section);
                double N = col.Forces.Sets.Max(s => s.N);
                double M = col.Forces.Sets.Max(s => Math.Max(Math.Abs(s.M2), Math.Abs(s.M3)));
                var d = fd.Design(N, M, sec.B, sec.H);
                f.B = d.B; f.L = d.L; f.H = d.H; f.BottomZ = col.BaseZ - d.H;
                foreach (var w in d.Warnings) res.Warnings.Add($"Móng {f.Label}: {w}");
                footEls.Add(csd.DetailFooting(f, d));
            }

            // Gộp cấu kiện giống hệt và đặt tên
            res.Elements.AddRange(RebarSchedule.MergeIdentical(beamEls));
            var cols = RebarSchedule.MergeIdentical(colEls).OrderBy(e => ((ColumnGeometry)e.Geometry).BaseZ).ThenBy(e => e.Label).ToList();
            for (int i = 0; i < cols.Count; i++) Relabel(cols[i], $"C{i + 1}");
            foreach (var c in m.Columns)
            {
                var owner = cols.FirstOrDefault(e => e.SourceIds.Contains(c.Id));
                if (owner != null) c.Label = owner.Label;
            }
            res.Elements.AddRange(cols);
            res.Elements.AddRange(RebarSchedule.MergeIdentical(slabEls));
            var foots = RebarSchedule.MergeIdentical(footEls).ToList();
            for (int i = 0; i < foots.Count; i++) Relabel(foots[i], $"M{i + 1}");
            foreach (var f in m.Footings)
            {
                var owner = foots.FirstOrDefault(e => e.SourceIds.Contains(f.Id));
                if (owner != null) f.Label = owner.Label;
            }
            res.Elements.AddRange(foots);

            foreach (var e in res.Elements)
                foreach (var err in BarMarker.Validate(e.Bars)) res.Warnings.Add(err);

            res.Schedule = RebarSchedule.Build(res.Elements);
            return res;
        }

        /// <summary>Đổi nhãn cấu kiện (gồm cả các thanh thép) — dùng khi gộp cấu kiện giống hệt.</summary>
        static void Relabel(DetailedElement e, string label)
        {
            e.Label = label;
            foreach (var b in e.Bars) b.Element = label;
        }
    }
}

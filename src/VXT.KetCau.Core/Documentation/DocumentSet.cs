using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VXT.KetCau.Core.Detailing;
using VXT.KetCau.Core.Drawing;
using VXT.KetCau.Core.Model;
using VXT.KetCau.Core.Workflow;

namespace VXT.KetCau.Core.Documentation
{
    /// <summary>
    /// Toàn bộ hồ sơ: dựng từng hình trên bộ ghi, xếp vào tờ, thêm tờ bìa + danh mục, rồi phát ra bề mặt đích.
    /// </summary>
    public class DocumentSet
    {
        public List<SheetInfo> Sheets { get; } = new List<SheetInfo>();
        public List<string> Warnings { get; } = new List<string>();
        readonly DocOptions opt;
        readonly bool realDims;
        SheetComposer sheetComposer;
        DrawingBlock cover;

        DocumentSet(DocOptions o, bool realDimensions) { opt = o; realDims = realDimensions; }

        RecordingDraw2D NewRec() => new RecordingDraw2D(realDims);

        static string ScaleText(double s) => "1/" + s.ToString("0", CultureInfo.InvariantCulture);

        /// <param name="arch">Có thể null (đầu vào từ ETABS/SAP): khi đó bỏ phần kiến trúc.</param>
        /// <param name="model">Có thể null khi chỉ lập hồ sơ kiến trúc.</param>
        public static DocumentSet Build(ArchModel arch, StructModel model, WorkflowResult res, DocOptions o, bool realDimensions)
        {
            var d = new DocumentSet(o, realDimensions);
            var blocks = new List<DrawingBlock>();
            if (o.DrawArchitecture && arch != null) d.BuildArch(arch, model, blocks);
            if (o.DrawStructure && model != null && res != null) d.BuildStruct(model, res, blocks);
            d.sheetComposer = new SheetComposer(o);
            d.Sheets.AddRange(d.sheetComposer.Pack(blocks));
            d.Warnings.AddRange(d.sheetComposer.Warnings);
            d.AddCoverSheet();
            return d;
        }

        void BuildArch(ArchModel a, StructModel m, List<DrawingBlock> blocks)
        {
            ArchAnalysis.Prepare(a);
            double P = opt.PlanScale, D = opt.DetailScale;
            var levels = ArchAnalysis.OrderedLevels(a);
            DrawingBlock Add(string group, double scale, Action<ArchDocComposer> draw)
            {
                var rec = NewRec();
                var c = new ArchDocComposer(rec, a, m, opt);
                draw(c);
                Warnings.AddRange(c.Warnings);
                var b = new DrawingBlock { Discipline = "KT", Group = group, Scale = scale, ScaleText = ScaleText(scale), Drawing = rec };
                blocks.Add(b);
                return b;
            }
            bool firstPlan = true;
            foreach (var lv in levels)
            {
                bool cut = firstPlan && a.Walls.Any(x => x.BaseLevel == lv.Name);
                bool roof = lv.IsRoof || !a.Walls.Any(x => x.BaseLevel == lv.Name);
                string title = roof ? "MẶT BẰNG MÁI" : "MẶT BẰNG " + lv.Name.ToUpperInvariant();
                Add(title, P, c => c.DrawPlan(lv.Name, P, cut));
                if (cut) firstPlan = false;
            }
            for (int side = 0; side < 4; side++)
            {
                int sd = side;
                Add(sd < 2 ? "MẶT ĐỨNG TRƯỚC, SAU" : "MẶT ĐỨNG BÊN", P, c => c.DrawElevation(sd, P));
            }
            var cutter = new ArchDocComposer(NewRec(), a, m, opt);
            var (cx, cy) = cutter.CutPositions();
            Add("MẶT CẮT", P, c => c.DrawSection(true, cx, "A-A", P));
            Add("MẶT CẮT", P, c => c.DrawSection(false, cy, "B-B", P));
            if (a.Openings.Any(o => !string.IsNullOrEmpty(o.Mark))) Add("THỐNG KÊ CỬA", D, c => c.DrawOpeningSchedule(D));
            foreach (var st in a.Stairs.Where(q => q.Boundary.Count >= 4)) Add("CHI TIẾT THANG", D, c => c.DrawStairDetail(st, D));
            var wet = a.Rooms.Where(r => ArchAnalysis.IsWetRoom(r) && r.Boundary.Count == 4 && Math.Abs(GeomUtil2.Area(r)) > 1e6).ToList();
            foreach (var g in wet.GroupBy(r => (Math.Round(Math.Abs(r.Boundary[0].X - r.Boundary[2].X) / 50), Math.Round(Math.Abs(r.Boundary[0].Y - r.Boundary[2].Y) / 50))))
            {
                var r = g.First();
                Add("CHI TIẾT VỆ SINH", 25, c => c.DrawWetRoom(r, 25));
            }
        }

        void BuildStruct(StructModel m, WorkflowResult res, List<DrawingBlock> blocks)
        {
            double P = opt.PlanScale, D = opt.DetailScale;
            DrawingBlock Add(string group, double scale, string scaleText, Action<IDraw2D> draw)
            {
                var rec = NewRec();
                draw(rec);
                var b = new DrawingBlock { Discipline = "KC", Group = group, Scale = scale, ScaleText = scaleText ?? ScaleText(scale), Drawing = rec };
                blocks.Add(b);
                return b;
            }
            string mix = $"{ScaleText(D)}, 1/{D / 2:0}";
            Add("GHI CHÚ CHUNG", D, null, w => new StructDocComposer(w, m, res).DrawGeneralNotes(D));
            if (m.Footings.Count > 0) Add("MẶT BẰNG MÓNG", P, null, w => new StructDocComposer(w, m, res).DrawFoundationPlan(P));
            foreach (var e in res.Elements.Where(x => x.Category == "Móng"))
                Add("CHI TIẾT MÓNG", D, mix, w => new StructDocComposer(w, m, res).DrawFootingDetail(e, D, 2));
            foreach (var st in m.Stories.Skip(1).Where(q => m.Beams.Any(b => b.Story == q.Name)))
                Add("MẶT BẰNG DẦM SÀN " + st.Name.ToUpperInvariant(), P, null, w => new StructDocComposer(w, m, res).DrawFramingPlan(st, P));
            foreach (var st in m.Stories.Skip(1).Where(q => m.Slabs.Any(x => x.Story == q.Name && x.Boundary.Count == 4)))
                Add("THÉP SÀN " + st.Name.ToUpperInvariant(), D, null, w => new StructDocComposer(w, m, res).DrawSlabRebarPlan(st, D));

            // Dầm: mặt đứng + mặt cắt phía dưới; composer vẽ theo tỉ lệ 1/50 cố định nên truyền tỉ lệ hiện hành
            foreach (var e in res.Elements.Where(x => x.Category == "Dầm"))
            {
                Add("CHI TIẾT DẦM", D, mix, w =>
                {
                    w.SetScaleX(D);
                    var comp = new ShopDrawingComposer(w, m.Settings) { ElevationScale = D, SectionScale = D / 2 };
                    double TH = comp.PrintTextHeight * comp.ElevationScale;
                    var g = (BeamChainGeometry)e.Geometry;
                    double x0 = g.Supports.First().FaceLeft;
                    double hElev = comp.DrawBeamElevation(e, -x0, 0);
                    double sx = g.B + TH * 3, y = -hElev - TH * 2;
                    foreach (var sec in g.Sections.GroupBy(q => q.Name).Select(q => q.First()))
                    {
                        comp.DrawBeamSection(e, sec.X, sec.Name, sx, y, 2, TH);
                        sx += g.B * 2 + TH * 15;
                    }
                });
            }
            // Cột
            foreach (var e in res.Elements.Where(x => x.Category == "Cột"))
            {
                Add("CHI TIẾT CỘT", D, mix, w =>
                {
                    w.SetScaleX(D);
                    var comp = new ShopDrawingComposer(w, m.Settings) { ElevationScale = D, SectionScale = D / 2 };
                    double TH = comp.PrintTextHeight * comp.ElevationScale;
                    var g = (ColumnGeometry)e.Geometry;
                    comp.DrawColumnElevation(e, TH * 12, 0);
                    comp.DrawColumnSection(e, TH * 26, g.TopZ, 2, TH);
                });
            }
            // Bảng thống kê chia trang theo khổ giấy
            var sized = new ShopDrawingComposer(null, m.Settings) { ElevationScale = D, ScheduleRowHeight = 650 };
            double paperH = opt.Paper == "A4" ? 210 : opt.Paper == "A2" ? 420 : opt.Paper == "A1" ? 594 : opt.Paper == "A0" ? 841 : 297;
            double usable = (paperH - 2 * SheetComposer.Margin - 2 * SheetComposer.Gap) * D;
            double u = sized.PrintTextHeight * D / 125.0;
            int maxRows = Math.Max(5, (int)((usable - 3000 * u) / (sized.ScheduleRowHeight * u)));
            var pages = StructDocComposer.SplitSchedule(res.Schedule, maxRows);
            foreach (var page in pages)
                Add("BẢNG THỐNG KÊ CỐT THÉP", D, null, w =>
                {
                    w.SetScaleX(D);
                    var comp = new ShopDrawingComposer(w, m.Settings) { ElevationScale = D, ScheduleRowHeight = sized.ScheduleRowHeight };
                    comp.DrawScheduleTable(page, 0, 0, comp.PrintTextHeight * D);
                });
            Add("TỔNG HỢP CỐT THÉP", D, null, w =>
            {
                w.SetScaleX(D);
                var comp = new ShopDrawingComposer(w, m.Settings) { ElevationScale = D };
                comp.DrawScheduleSummary(res.Schedule, 0, 0, comp.PrintTextHeight * D);
            });
        }

        /// <summary>Tờ đầu: tên công trình và danh mục bản vẽ. Vẽ sau cùng vì cần danh sách tờ.</summary>
        void AddCoverSheet()
        {
            if (Sheets.Count == 0) return;
            double s = 100;
            var rec = NewRec();
            rec.SetScale(s);
            double TH = 2.5 * s;
            rec.Text("CHU", 0, 0, TH * 2.6, string.IsNullOrWhiteSpace(opt.ProjectName) ? "HỒ SƠ THIẾT KẾ" : opt.ProjectName.ToUpperInvariant(), 1, 0, 0);
            if (!string.IsNullOrWhiteSpace(opt.Address)) rec.Text("CHU", 0, -TH * 3.2, TH * 1.2, "Địa điểm: " + opt.Address, 1, 0, 0);
            if (!string.IsNullOrWhiteSpace(opt.Owner)) rec.Text("CHU", 0, -TH * 5.2, TH * 1.2, "Chủ đầu tư: " + opt.Owner, 1, 0, 0);
            rec.Text("CHU", 0, -TH * 8.5, TH * 1.5, "DANH MỤC BẢN VẼ", 1, 0, 0);
            var rows = new List<string[]> { new[] { "SỐ HIỆU", "TÊN BẢN VẼ", "KHỔ", "TỈ LỆ" } };
            rows.Add(new[] { "BV-00", "BÌA VÀ DANH MỤC BẢN VẼ", opt.Paper, "" });
            rows.AddRange(Sheets.Select(q => new[] { q.Number, q.Title, q.Paper, q.ScaleText }));
            double[] cw = { 3000, 15000, 1800, 3400 };
            double total = cw.Sum();
            SimpleTable.Draw(rec, -total / 2, -TH * 9.5, TH * 0.9, cw, TH * 1.9, rows);
            cover = new DrawingBlock { Discipline = "KT", Group = "BÌA VÀ DANH MỤC BẢN VẼ", Scale = s, ScaleText = "", Drawing = rec };

            // Danh mục dài có thể cần khổ lớn hơn
            var coverSheets = new SheetComposer(opt);
            var packed = coverSheets.Pack(new[] { cover });
            var sh = packed[0];
            sh.Number = "BV-00";
            sh.Discipline = "BV";
            Warnings.AddRange(coverSheets.Warnings);
            // đặt tờ bìa bên trái hàng đầu tiên
            double firstY = Sheets.Count > 0 ? Sheets.Max(q => q.Y + q.ModelH) : 0;
            double firstX = Sheets.Min(q => q.X);
            sh.X = firstX - sh.ModelW - 40 * s;
            sh.Y = firstY - sh.ModelH;
            Sheets.Insert(0, sh);
        }

        /// <summary>Vẽ toàn bộ hồ sơ lên bề mặt đích.</summary>
        public void Render(IDraw2D target)
        {
            foreach (var sh in Sheets) sheetComposer.Render(target, sh);
        }
    }

    static class GeomUtil2
    {
        public static double Area(ArchRoom r) => Geometry.GeomUtil.Area(r.Boundary);
    }
}

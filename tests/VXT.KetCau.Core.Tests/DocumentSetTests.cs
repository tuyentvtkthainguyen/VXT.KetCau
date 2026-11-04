using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VXT.KetCau.Core.Detailing;
using VXT.KetCau.Core.Documentation;
using VXT.KetCau.Core.Drawing;
using VXT.KetCau.Core.Framing;
using VXT.KetCau.Core.Geometry;
using VXT.KetCau.Core.Input;
using VXT.KetCau.Core.Model;
using VXT.KetCau.Core.Workflow;
using VXT.KetCau.Dwg;
using Xunit;

namespace VXT.KetCau.Core.Tests
{
    public class ParametricTests
    {
        [Fact] public void Spans_support_repeat_syntax() => Assert.Equal(new[] { 3600.0, 3600, 4200 }, ParametricInput.ParseSpans("2x3600 4200"));

        [Fact]
        public void Levels_sorted_last_is_roof()
        {
            var l = ParametricInput.ParseLevels("Mái=7.2; Tầng 1=0; Tầng 2=3,6");
            Assert.Equal("Tầng 1", l[0].Name);
            Assert.Equal(3600, l[1].Elevation);
            Assert.True(l[2].IsRoof);
        }

        [Theory]
        [InlineData("3Ø20", 3, 20)]
        [InlineData("2d16", 2, 16)]
        [InlineData("4f18", 4, 18)]
        public void Bars_parsed(string t, int n, int d)
        {
            var g = RebarText.ParseBars(t);
            Assert.Equal(n, g.N); Assert.Equal(d, g.D);
        }

        [Fact] public void Stirrup_parsed() { var s = RebarText.ParseStirrup("Ø8a150"); Assert.Equal(8, s.D); Assert.Equal(150, s.S); }
        [Fact] public void Bad_rebar_text_rejected() => Assert.Throws<FormatException>(() => RebarText.ParseBars("ba thanh"));

        [Fact]
        public void Column_bars_split_keeps_total()
        {
            var (nb, nh) = RebarText.SplitColumnBars(8, 220, 400);
            Assert.Equal(8, 2 * nb + 2 * (nh - 2));
            Assert.True(nh >= nb);
        }

        [Fact]
        public void Column_bars_from_area_reaches_requirement()
        {
            var (n, d) = RebarText.ColumnBarsFromArea(1500, new[] { 16, 18, 20 });
            Assert.True(n * Core.Codes.Tcvn5574.BarArea(d) >= 1500);
            Assert.Equal(0, n % 2);
        }

        [Fact]
        public void Manual_rebar_overrides_and_warns_when_short()
        {
            var p = new ParametricInput { Levels = "Tầng 1=0; Mái=3.6" };
            var a = p.ToArch();
            ArchAnalysis.Prepare(a);
            var m = new FramingGenerator(new DesignSettings()).Generate(a);
            var wf = new StructuralWorkflow(m);
            wf.PreliminaryForces();
            var beam = m.Beams.First(b => b.Story == "Mái");
            beam.Forces = new BeamForces { MnegLeft = -120, MnegRight = -120, MposSpan = 120, VmaxLeft = 80, VmaxRight = 80 };
            beam.ManualTop = "2Ø12"; beam.ManualBottom = "2Ø12"; beam.ManualStirrupSupport = "Ø6a100";
            var col = m.Columns.First();
            col.ManualBars = "8Ø20";
            var res = wf.DetailAll();
            Assert.Equal(12, beam.Design.BottomBars.D);
            Assert.Equal(20, col.Design.D);
            Assert.Equal(8, col.Design.TotalBars);
            Assert.Contains(res.Warnings, x => x.Contains("nhỏ hơn yêu cầu"));
        }
    }

    public class RecordingTests
    {
        [Fact]
        public void Bounds_and_replay_offset()
        {
            var r = new RecordingDraw2D(false);
            r.Line("A", 0, 0, 100, 50);
            r.Circle("A", 200, 0, 10);
            Assert.Equal(210, r.MaxX);
            Assert.Equal(-10, r.MinY);
            var t = new RecordingDraw2D(false);
            r.Replay(t, 1000, 1000);
            Assert.Equal(1210, t.MaxX);
            Assert.Equal(990, t.MinY);
        }

        [Fact]
        public void Hatch_lines_stay_inside_polygon()
        {
            var t = new RecordingDraw2D(false);
            Draw2DEx.HatchLines(t, "H", new[] { (0.0, 0.0), (100.0, 0.0), (100.0, 100.0), (0.0, 100.0) }, 10);
            Assert.True(t.Count > 5);
            Assert.InRange(t.MinX, -0.01, 100);
            Assert.InRange(t.MaxX, 0, 100.01);
            Assert.InRange(t.MaxY, 0, 100.01);
        }
    }

    public class WallMaskTests
    {
        static ArchAnalysis.Rect R(double a, double b, double c, double d) => new ArchAnalysis.Rect(a, b, c, d);

        [Fact]
        public void T_junction_has_no_line_inside_the_through_wall()
        {
            // tường ngang xuyên qua, tường đứng chạm từ dưới
            var mask = new WallMask(new[] { R(0, 0, 1000, 100), R(450, -500, 550, 0) }, new ArchAnalysis.Rect[0]);
            var edges = mask.Edges().ToList();
            // không có đoạn nào nằm hẳn bên trong vùng tường ngang (y giữa 0 và 100, x giữa 450–550)
            Assert.DoesNotContain(edges, e => Math.Abs(e.Mid.Y - 50) < 40 && e.Mid.X > 451 && e.Mid.X < 549);
            // mép dưới tường ngang bị đứt ở chỗ tường đứng chạm
            Assert.DoesNotContain(edges, e => e.A.Y == 0 && e.B.Y == 0 && e.A.X < 500 && e.B.X > 500);
        }

        [Fact]
        public void Hole_splits_wall_into_two_fills()
        {
            var mask = new WallMask(new[] { R(0, 0, 3000, 220) }, new[] { R(1000, -2, 1900, 222) });
            var fills = mask.FillRects().ToList();
            Assert.Equal(2, fills.Count);
            Assert.Equal(1000, fills[0].X1);
            Assert.Equal(1900, fills[1].X0);
        }
    }

    public class ImporterTests
    {
        /// <summary>Hai tầng vẽ chung một bản vẽ, tầng 2 đặt lệch 30 m; có cửa đi bằng cung, cửa sổ bằng khe, chữ không phải tên phòng.</summary>
        static string WritePlan()
        {
            var w = new DxfWriter();
            w.AddLayer("A-WALL", 7); w.AddLayer("A-DOOR", 3); w.AddLayer("A-GRID", 8); w.AddLayer("A-COLS", 7); w.AddLayer("TEXT", 2);
            foreach (var ox in new[] { 0.0, 30000.0 })
            {
                void Wall(double x0, double y0, double x1, double y1)
                {
                    // hai nét biên song song cách 220
                    if (Math.Abs(y0 - y1) < 1) { w.Line("A-WALL", ox + x0, y0 - 110, ox + x1, y1 - 110); w.Line("A-WALL", ox + x0, y0 + 110, ox + x1, y1 + 110); }
                    else { w.Line("A-WALL", ox + x0 - 110, y0, ox + x1 - 110, y1); w.Line("A-WALL", ox + x0 + 110, y0, ox + x1 + 110, y1); }
                }
                // tường dưới có khe cửa đi 900 tại x=[1000,1900]
                Wall(0, 0, 1000, 0); Wall(1900, 0, 3000, 0); Wall(3000, 0, 6000, 0);
                // tường trên có khe 1200 tại x=[2400,3600], không có ký hiệu cửa → tạm coi cửa sổ
                Wall(0, 5000, 2400, 5000); Wall(3600, 5000, 6000, 5000);
                Wall(0, 0, 0, 5000); Wall(6000, 0, 6000, 5000);
                w.Arc("A-DOOR", ox + 1000, 110, 900, 0, 90);
                w.Line("A-GRID", ox, -1500, ox, 6500); w.Line("A-GRID", ox + 6000, -1500, ox + 6000, 6500);
                w.Line("A-GRID", ox - 1500, 0, ox + 7500, 0); w.Line("A-GRID", ox - 1500, 5000, ox + 7500, 5000);
                w.Text("TEXT", ox + 3000, 2500, 200, "P. KHÁCH", 1, 2, 0);
                w.Text("TEXT", ox + 3000, -3000, 300, "MẶT BẰNG TẦNG 1", 1, 2, 0);
                w.Text("TEXT", ox + 1000, -1000, 200, "TOP 3600", 1, 2, 0);
            }
            string path = Path.Combine(Path.GetTempPath(), "vxt_plan_" + Guid.NewGuid().ToString("N") + ".dxf");
            w.Save(path);
            return path;
        }

        static ArchModel Import(out DxfArchImporter imp)
        {
            string f = WritePlan();
            imp = new DxfArchImporter { UnitScale = 1 };
            return imp.Import(new[]
            {
                new FloorPlanInput { Path = f, LevelName = "Tầng 1", Elevation = 0, Window = new double[] { -5000, -5000, 15000, 10000 } },
                new FloorPlanInput { Path = f, LevelName = "Tầng 2", Elevation = 3600, Window = new double[] { 25000, -5000, 45000, 10000 }, BaseX = 30000 },
                new FloorPlanInput { Path = f, LevelName = "Mái", Elevation = 7200, IsRoof = true, LevelOnly = true },
            });
        }

        [Fact]
        public void Each_level_reads_its_own_window_and_aligns_at_base_point()
        {
            var a = Import(out _);
            Assert.Equal(3, a.Levels.Count);
            Assert.Equal(4, a.Walls.Count(w => w.BaseLevel == "Tầng 1"));
            Assert.Equal(4, a.Walls.Count(w => w.BaseLevel == "Tầng 2"));
            Assert.Empty(a.Walls.Where(w => w.BaseLevel == "Mái"));
            // tầng 2 sau khi trừ điểm gốc phải trùng tầng 1
            Assert.True(a.Walls.Where(w => w.BaseLevel == "Tầng 2").All(w => w.Line.Mid.X < 7000));
            Assert.Equal(4, a.Grids.Count);
        }

        [Fact]
        public void Door_from_arc_and_window_from_gap()
        {
            var a = Import(out var imp);
            var open = a.Openings.Where(o => o.Level == "Tầng 1").ToList();
            var door = Assert.Single(open, o => o.Kind == "Cửa đi");
            Assert.Equal(900, door.Width);
            Assert.InRange(door.Location.X, 1440, 1460);
            var win = Assert.Single(open, o => o.Kind == "Cửa sổ");
            Assert.Equal(1200, win.Width);
            Assert.Contains(imp.Log, l => l.Contains("tạm coi là cửa sổ"));
        }

        [Fact]
        public void Only_real_room_names_are_read()
        {
            var a = Import(out _);
            var rooms = a.Rooms.Where(r => r.Level == "Tầng 1").Select(r => r.Name).ToList();
            Assert.Equal(new[] { "P. KHÁCH" }, rooms);
        }

        [Theory]
        [InlineData("TƯỜNG XÂY", true)]
        [InlineData("Tường", true)]
        [InlineData("A-WALL", true)]
        [InlineData("KY HIEU", false)]
        public void Vietnamese_layer_names_match(string layer, bool expect) => Assert.Equal(expect, LayerMap.Match(layer, new LayerMap().Wall));

        [Theory]
        [InlineData("P. NGỦ 1", true)]
        [InlineData("WC", true)]
        [InlineData("Phòng khách", true)]
        [InlineData("BẾP + ĂN", true)]
        [InlineData("TOP 3600", false)]
        [InlineData("CẤP PHỐI", false)]
        [InlineData("MẶT BẰNG TẦNG 1", false)]
        [InlineData("TỈ LỆ 1/100", false)]
        public void Room_name_filter(string t, bool expect) => Assert.Equal(expect, DxfArchImporter.IsRoomName(t));

        [Fact]
        public void Room_area_comes_from_rays_to_wall_faces()
        {
            var a = Import(out _);
            ArchAnalysis.Prepare(a);
            var r = a.Rooms.First(q => q.Level == "Tầng 1");
            // thông thuỷ (6000−220)×(5000−220) = 27.63 m²
            Assert.Equal(27.63, Math.Abs(GeomUtil.Area(r.Boundary)) / 1e6, 1);
        }
    }

    public class DocumentSetEndToEnd
    {
        static ArchModel House()
        {
            var a = new ParametricInput { ProjectName = "Nhà mẫu", SpansX = "4200 3600", SpansY = "5000 4000 3000", Levels = "Tầng 1=0; Tầng 2=3.6; Tầng 3=7.0; Mái=10.4", Stair = "5400,5000,2400,4000" }.ToArch();
            // cửa + phòng cho từng tầng
            foreach (var lv in new[] { "Tầng 1", "Tầng 2", "Tầng 3" })
            {
                var b = a.Walls.First(w => w.BaseLevel == lv && w.Line.IsHorizontal() && w.Line.Mid.Y == 0);
                a.Openings.Add(new ArchOpening { HostWallId = b.Id, Level = lv, Location = new P2(1500, 0), Width = lv == "Tầng 1" ? 1800 : 1400, Height = lv == "Tầng 1" ? 2400 : 1500, SillHeight = lv == "Tầng 1" ? 0 : 900, Kind = lv == "Tầng 1" ? "Cửa đi" : "Cửa sổ" });
                a.Openings.Add(new ArchOpening { HostWallId = b.Id, Level = lv, Location = new P2(5800, 0), Width = 1200, Height = 1500, SillHeight = 900, Kind = "Cửa sổ" });
                var mid = a.Walls.First(w => w.BaseLevel == lv && w.Line.IsHorizontal() && w.Line.Mid.Y == 5000);
                a.Openings.Add(new ArchOpening { HostWallId = mid.Id, Level = lv, Location = new P2(2000, 5000), Width = 900, Height = 2200, Kind = "Cửa đi" });
                var wc = a.Walls.First(w => w.BaseLevel == lv && w.Line.IsVertical() && w.Line.Mid.X == 4200);
                a.Openings.Add(new ArchOpening { HostWallId = wc.Id, Level = lv, Location = new P2(4200, 10500), Width = 700, Height = 2100, Kind = "Cửa đi" });
                void Room(string n, double x, double y) => a.Rooms.Add(new ArchRoom { Name = n, Usage = n, Level = lv, Boundary = { new P2(x - 50, y - 50), new P2(x + 50, y - 50), new P2(x + 50, y + 50), new P2(x - 50, y + 50) } });
                Room("P. KHÁCH", 2100, 2500); Room("BẾP", 6000, 2500); Room("P. NGỦ", 2100, 7000); Room("WC", 6000, 10500);
            }
            return a;
        }

        static (DocumentSet set, ArchModel arch, StructModel m, WorkflowResult res) Build(bool realDims, string paper = "A3")
        {
            var a = House();
            ArchAnalysis.Prepare(a);
            var m = new FramingGenerator(new DesignSettings()).Generate(a);
            var wf = new StructuralWorkflow(m);
            wf.PreliminaryForces();
            var res = wf.DetailAll();
            var opt = new DocOptions { ProjectName = "NHÀ Ở GIA ĐÌNH MẪU", Owner = "Chủ đầu tư mẫu", Address = "Phường Mẫu, Thành phố Mẫu", Company = "Đơn vị thiết kế mẫu", Designer = "Người thiết kế", Checker = "Người kiểm tra", Director = "Chủ trì", Date = "11/2026", Paper = paper };
            return (DocumentSet.Build(a, m, res, opt, realDims), a, m, res);
        }

        [Fact]
        public void Full_set_has_both_disciplines_and_unique_sheet_numbers()
        {
            var (d, _, _, _) = Build(false);
            Assert.Contains(d.Sheets, s => s.Number.StartsWith("KT-"));
            Assert.Contains(d.Sheets, s => s.Number.StartsWith("KC-"));
            Assert.Equal("BV-00", d.Sheets[0].Number);
            Assert.Equal(d.Sheets.Count, d.Sheets.Select(s => s.Number).Distinct().Count());
            foreach (var t in new[] { "MẶT BẰNG TẦNG 1", "MẶT BẰNG MÁI", "MẶT CẮT", "THỐNG KÊ CỬA", "CHI TIẾT THANG", "CHI TIẾT VỆ SINH", "MẶT BẰNG MÓNG", "CHI TIẾT MÓNG", "CHI TIẾT DẦM", "CHI TIẾT CỘT", "BẢNG THỐNG KÊ CỐT THÉP", "GHI CHÚ CHUNG" })
                Assert.Contains(d.Sheets, s => s.Title.StartsWith(t));
        }

        [Fact]
        public void Every_drawing_fits_inside_its_frame()
        {
            var (d, _, _, _) = Build(false);
            foreach (var s in d.Sheets)
                foreach (var (b, ix, iy) in s.Items)
                {
                    double usableW = (s.PaperW - SheetComposer.MarginLeft - SheetComposer.Margin - SheetComposer.TitleStripW - 2 * SheetComposer.Gap) * s.Scale;
                    double usableH = (s.PaperH - 2 * SheetComposer.Margin - 2 * SheetComposer.Gap) * s.Scale;
                    Assert.True(ix + b.Drawing.Width <= usableW + 1, $"{s.Number} {s.Title}: rộng {ix + b.Drawing.Width:0} > {usableW:0}");
                    Assert.True(iy + b.Drawing.Height <= usableH + 1, $"{s.Number} {s.Title}: cao {iy + b.Drawing.Height:0} > {usableH:0}");
                }
        }

        [Fact]
        public void Sheets_do_not_overlap_in_model_space()
        {
            var (d, _, _, _) = Build(false);
            for (int i = 0; i < d.Sheets.Count; i++)
                for (int j = i + 1; j < d.Sheets.Count; j++)
                {
                    var a = d.Sheets[i]; var b = d.Sheets[j];
                    bool apart = a.X + a.ModelW <= b.X || b.X + b.ModelW <= a.X || a.Y + a.ModelH <= b.Y || b.Y + b.ModelH <= a.Y;
                    Assert.True(apart, $"{a.Number} chồng lên {b.Number}");
                }
        }

        [Fact]
        public void Long_wall_over_several_spans_adds_no_duplicate_beam_and_loads_every_span()
        {
            var (_, _, m, _) = Build(false);
            Assert.DoesNotContain(m.Beams, b => b.IsSecondary);
            foreach (var st in m.Stories)
            {
                var beams = m.Beams.Where(b => b.Story == st.Name).ToList();
                for (int i = 0; i < beams.Count; i++)
                    for (int j = i + 1; j < beams.Count; j++)
                    {
                        var si = new Seg2(beams[i].Start, beams[i].End);
                        Assert.False(si.DistanceTo((beams[j].Start + beams[j].End) * 0.5) < 50 && Math.Abs((beams[i].End - beams[i].Start).Unit.Dot((beams[j].End - beams[j].Start).Unit)) > 0.95, $"Dầm trùng ở tầng {st.Name}");
                    }
            }
            // tường trục A dài 2 nhịp, tầng 2: cả hai dầm trục A đều nhận tải tường
            var axisA = m.Beams.Where(b => b.Story == "Tầng 2" && Math.Abs(b.Start.Y) < 1 && Math.Abs(b.End.Y) < 1).ToList();
            Assert.Equal(2, axisA.Count);
            Assert.All(axisA, b => Assert.Contains(m.LineLoads, l => l.BeamId == b.Id));
        }

        [Fact]
        public void Overlapping_footings_are_reported()
        {
            var m = new StructModel();
            m.Footings.Add(new FootingElem { Label = "M1", Center = new P2(0, 0), B = 3000, L = 3000 });
            m.Footings.Add(new FootingElem { Label = "M2", Center = new P2(2500, 0), B = 3000, L = 3000 });
            m.Footings.Add(new FootingElem { Label = "M3", Center = new P2(9000, 0), B = 3000, L = 3000 });
            Assert.Single(StructDocComposer.FootingOverlaps(m));
        }

        [Fact]
        public void Column_level_text_has_no_double_sign()
        {
            var g = new ColumnGeometry { BaseZ = -1000, TopZ = -50 };
            Assert.Equal("-0.050", g.TopLevelText);
        }

        [Fact]
        public void Footing_mesh_tag_uses_spacing()
        {
            var bar = new DetailBar { Role = BarRole.FootingBottom, D = 12, Shape = ShapeKind.Straight, Segments = { 1000 }, Spacing = 150, Count = 9 };
            Assert.Equal("Ø12a150", bar.TagText());
        }

        [Fact]
        public void Sheets_stay_right_of_the_insertion_point()
        {
            var (d, _, _, _) = Build(false);
            Assert.All(d.Sheets, s => Assert.True(s.X >= 0));
        }

        [Fact]
        public void Schedule_pages_keep_every_row()
        {
            var (_, _, _, res) = Build(false);
            var pages = StructDocComposer.SplitSchedule(res.Schedule, 12);
            Assert.Equal(res.Schedule.Rows.Count, pages.Sum(p => p.Rows.Count));
            Assert.All(pages, p => Assert.True(p.Rows.Count <= 12));
        }

        [Fact]
        public void Merged_elements_keep_consistent_labels_on_plan()
        {
            var (_, _, m, res) = Build(false);
            foreach (var f in m.Footings) Assert.Contains(res.Elements, e => e.Category == "Móng" && e.Label == f.Label);
        }

        [Fact]
        public void Wet_rooms_found_and_marks_assigned()
        {
            var (_, a, _, _) = Build(false);
            Assert.Contains(a.Rooms, r => ArchAnalysis.IsWetRoom(r));
            Assert.All(a.Openings.Where(o => o.HostWallId != null), o => Assert.False(string.IsNullOrEmpty(o.Mark)));
            // cùng kích thước → cùng ký hiệu
            foreach (var g in a.Openings.GroupBy(o => o.Mark)) Assert.Single(g.Select(o => (o.Width, o.Height)).Distinct());
        }

        [Fact]
        public void Dxf_round_trip_and_sample_for_review()
        {
            var (d, _, _, _) = Build(false);
            string dir = Environment.GetEnvironmentVariable("VXT_SAMPLE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.GetTempPath();
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "ho-so-mau.dxf");
            ShopDrawingDxf.WriteDocumentSet(d, path);
            var back = DxfReader.Load(path);
            Assert.True(back.Entities.Count > 2000);
            Assert.Contains(back.Entities.OfType<DxfText>(), t => t.Text.Contains("MẶT BẰNG TẦNG 1"));
            Assert.Contains(back.Entities.OfType<DxfText>(), t => t.Text == "KC-01");
            // danh sách tờ để đối chiếu
            File.WriteAllLines(Path.Combine(dir, "danh-sach-to.txt"), d.Sheets.Select(s => $"{s.Number}\t{s.Title}\t{s.Paper}\t{s.ScaleText}\t{s.X:0}\t{s.Y:0}\t{s.ModelW:0}\t{s.ModelH:0}"));
            File.WriteAllLines(Path.Combine(dir, "canh-bao.txt"), d.Warnings);
        }

        [Fact]
        public void Real_dimension_mode_records_dimensions_without_line_fallback()
        {
            var (real, _, _, _) = Build(true);
            var (fake, _, _, _) = Build(false);
            var counterReal = new CountingSurface();
            var counterFake = new CountingSurface();
            real.Render(counterReal);
            fake.Render(counterFake);
            Assert.True(counterReal.Dims > 100);
            Assert.True(counterReal.Lines < counterFake.Lines);
        }

        class CountingSurface : IDraw2D
        {
            public int Lines, Dims;
            public void Line(string layer, double x1, double y1, double x2, double y2) => Lines++;
            public void Circle(string layer, double x, double y, double r) { }
            public void Dot(string layer, double x, double y, double r) { }
            public void Text(string layer, double x, double y, double height, string text, int h, int v, double rotDeg) { }
            public bool Dimension(double x0, double y0, double x1, double y1, double lineY, bool vertical, string overrideText) { Dims++; return true; }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using VXT.KetCau.Core.Codes;
using VXT.KetCau.Core.Design;
using VXT.KetCau.Core.Documentation;
using VXT.KetCau.Core.Drawing;
using VXT.KetCau.Core.Input;
using VXT.KetCau.Core.Detailing;
using VXT.KetCau.Core.Framing;
using VXT.KetCau.Core.Geometry;
using VXT.KetCau.Core.Model;
using VXT.KetCau.Core.Workflow;
using VXT.KetCau.Dwg;
using Xunit;

namespace VXT.KetCau.Core.Tests
{
    /// <summary>Giá trị kỳ vọng đã đối chiếu bằng tính tay / bảng tính độc lập.</summary>
    public class Tcvn5574Tests
    {
        static readonly ConcreteGrade B20 = ConcreteGrade.Get("B20");
        static readonly SteelGrade CB400 = SteelGrade.Get("CB400-V");
        static readonly SteelGrade CB240 = SteelGrade.Get("CB240-T");

        [Fact] public void XiR_CB400() => Assert.Equal(0.533, Tcvn5574.XiR(CB400), 3);
        [Fact] public void AlphaR_CB400() => Assert.Equal(0.391, Tcvn5574.AlphaR(CB400), 3);
        [Fact] public void XiR_CB240() => Assert.Equal(0.615, Tcvn5574.XiR(CB240), 3);

        [Fact]
        public void Anchorage_B20_CB400()
        {
            Assert.Equal(630, Tcvn5574.Anchorage(16, CB400, B20));   // ≈ 38.9d
            Assert.Equal(750, Tcvn5574.LapLength(16, CB400, B20));
            Assert.Equal(860, Tcvn5574.Anchorage(22, CB400, B20));
        }

        [Fact]
        public void Flexure_220x400_M60()
        {
            var d = new BeamDesigner(B20, CB400, CB240, 25);
            var r = d.Flexure(60, 220, 400, 43);
            Assert.Equal(0.1861, r.AlphaM, 3);
            Assert.InRange(r.AsReq, 530, 541);   // 535.8 mm²
            Assert.False(r.DoublyReinforced);
        }

        [Fact]
        public void Flexure_DoublyReinforced()
        {
            var d = new BeamDesigner(B20, CB400, CB240, 25);
            var r = d.Flexure(160, 220, 400, 43);
            Assert.True(r.DoublyReinforced);
            Assert.True(r.AscReq > 0);
        }

        [Fact]
        public void Shear_Q80_gives_Phi6a100()
        {
            var d = new BeamDesigner(B20, CB400, CB240, 25);
            var r = d.Shear(80, 220, 400, new[] { 6, 8 }, true);
            Assert.Equal(6, r.Stirrup.D);
            Assert.Equal(100, r.Stirrup.S);
            Assert.InRange(r.QswReq, 82, 85);  // 83.4 N/mm
        }

        [Fact]
        public void SelectBars_prefers_single_layer_min_waste()
        {
            var g = BeamDesigner.SelectBars(535.8, 220, 25, 6, new[] { 14, 16, 18, 20, 22 });
            Assert.True(g.Area >= 535.8);
            Assert.True(g.Area < 700);
        }

        [Fact]
        public void Stirrup_length_includes_two_135_hooks()
        {
            var bar = new DetailBar { Role = BarRole.Stirrup, D = 6, Shape = ShapeKind.Stirrup, Segments = { 170, 350 } };
            Assert.Equal(1190, bar.Length);
            Assert.Equal("Ø6a100", bar.TagText("Ø", null, 100));
        }

        [Fact]
        public void Column_axial_capacity_and_uniaxial_moment()
        {
            var cd = new ColumnDesigner(B20, CB400, 25);
            // 6Ø16 (2 thanh mỗi cạnh b, 3 thanh mỗi cạnh h): N0 ≈ 1167 kN
            double r0 = cd.CapacityRatio(220, 300, 2, 3, 16, 8, 1167 * 0.999, 0, 0);
            Assert.InRange(r0, 0.98, 1.01);
            // Uốn quanh trục song song cạnh b, N = 0: Mu ≈ 47.5 kN·m (mô hình thớ, kiểm tra độc lập)
            double r1 = cd.CapacityRatio(220, 300, 2, 3, 16, 8, 0.1, 47.5, 0);
            Assert.InRange(r1, 0.95, 1.05);
        }

        [Fact]
        public void Seismic_spectrum_plateau()
        {
            double sd = Tcvn9386.Sd(0.3, 0.1, "C", 3.9);
            Assert.Equal(0.1 * 1.15 * 2.5 / 3.9, sd, 6);
        }
    }

    public class MarkingTests
    {
        static DetailBar Long(BarRole r, int d, double L, double x) => new DetailBar { Element = "D1-1", Role = r, D = d, Shape = ShapeKind.Straight, Segments = { L }, Count = 3, OrderX = x };

        [Fact]
        public void Top_and_bottom_identical_get_different_marks_bottom_first()
        {
            var m = BarMarker.Assign(new[] { Long(BarRole.Top, 22, 5000, 0), Long(BarRole.Bottom, 22, 5000, 0) });
            Assert.Equal(2, m.Count);
            Assert.Equal(BarRole.Bottom, m.First(b => b.Mark == 1).Role);
            Assert.Equal(BarRole.Top, m.First(b => b.Mark == 2).Role);
        }

        [Fact]
        public void Symmetric_support_bars_share_one_mark()
        {
            var m = BarMarker.Assign(new[] { Long(BarRole.SupportTop, 18, 2100, 0), Long(BarRole.SupportTop, 18, 2100, 8000) });
            Assert.Single(m);
            Assert.Equal(6, m[0].Count);
        }

        [Fact]
        public void Mirrored_L_hooks_share_one_mark()
        {
            var a = new DetailBar { Role = BarRole.Top, D = 16, Shape = ShapeKind.LHook, Segments = { 340, 10460 }, Count = 2 };
            var b = new DetailBar { Role = BarRole.Top, D = 16, Shape = ShapeKind.LHook, Segments = { 10460, 340 }, Count = 2, OrderX = 9600 };
            Assert.Single(BarMarker.Assign(new[] { a, b }));
        }

        [Fact]
        public void Circled_numbers() { Assert.Equal("①", BarMarker.Circled(1)); Assert.Equal("⑳", BarMarker.Circled(20)); }
    }

    public class EndToEndTests
    {
        /// <summary>Nhà 2 nhịp × 2 nhịp (4 m), 2 tầng + mái, tường 220 trên trục.</summary>
        static ArchModel SmallHouse()
        {
            var a = new ArchModel { ProjectName = "Thử nghiệm" };
            a.Levels.Add(new ArchLevel { Name = "Tầng 1", Elevation = 0 });
            a.Levels.Add(new ArchLevel { Name = "Tầng 2", Elevation = 3600 });
            a.Levels.Add(new ArchLevel { Name = "Mái", Elevation = 7200, IsRoof = true });
            double[] xs = { 0, 4000, 8000 }, ys = { 0, 4000, 8000 };
            for (int i = 0; i < 3; i++)
            {
                a.Grids.Add(new ArchGrid { Name = (i + 1).ToString(), Line = new Seg2(new P2(xs[i], -1000), new P2(xs[i], 9000)) });
                a.Grids.Add(new ArchGrid { Name = ((char)('A' + i)).ToString(), Line = new Seg2(new P2(-1000, ys[i]), new P2(9000, ys[i])) });
            }
            int n = 0;
            foreach (var lv in new[] { "Tầng 1", "Tầng 2" })
                for (int i = 0; i < 3; i++)
                {
                    a.Walls.Add(new ArchWall { Id = "W" + ++n, Line = new Seg2(new P2(xs[i], 0), new P2(xs[i], 8000)), Thickness = 220, BaseLevel = lv, Height = 3600 });
                    a.Walls.Add(new ArchWall { Id = "W" + ++n, Line = new Seg2(new P2(0, ys[i]), new P2(8000, ys[i])), Thickness = 220, BaseLevel = lv, Height = 3600 });
                }
            return a;
        }

        [Fact]
        public void Arch_to_shopdrawing_runs()
        {
            var set = new DesignSettings();
            var gen = new FramingGenerator(set);
            var m = gen.Generate(SmallHouse());
            Assert.Equal(9 * 3, m.Columns.Count);          // 9 vị trí × (móng→T1, T1→T2, T2→mái)
            Assert.True(m.Beams.Count >= 3 * 12);          // giằng + 2 sàn, 12 dầm mỗi tầng
            Assert.Equal(8, m.Slabs.Count);                // 4 ô × 2 tầng (T2, mái)

            var wf = new StructuralWorkflow(m);
            wf.PreliminaryForces();
            var res = wf.DetailAll();
            Assert.Contains(res.Elements, e => e.Category == "Dầm");
            Assert.Contains(res.Elements, e => e.Category == "Cột");
            Assert.Contains(res.Elements, e => e.Category == "Sàn");
            Assert.True(res.Schedule.ByGroup().Total > 100);
            foreach (var e in res.Elements) Assert.Empty(BarMarker.Validate(e.Bars));

            string dxf = Path.Combine(Path.GetTempPath(), "vxtkc_test.dxf");
            ShopDrawingDxf.Write(res, set, dxf);
            Assert.True(new FileInfo(dxf).Length > 10000);
            var back = DxfReader.Load(dxf);
            Assert.True(back.Entities.Count > 500);
        }
    }
}

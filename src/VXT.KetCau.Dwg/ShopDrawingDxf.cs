using System;
using System.Linq;
using VXT.KetCau.Core.Detailing;
using VXT.KetCau.Core.Drawing;
using VXT.KetCau.Core.Model;
using VXT.KetCau.Core.Workflow;

namespace VXT.KetCau.Dwg
{
    /// <summary>
    /// Xuất toàn bộ shop drawing thép ra một file DXF (in ở tỉ lệ 1/50; mặt cắt phóng ×2 để in ra 1/25):
    /// mặt đứng + mặt cắt từng dầm, mặt đứng + mặt cắt cột, bảng thống kê và tổng hợp cốt thép.
    /// </summary>
    public static class ShopDrawingDxf
    {
        public static void Write(WorkflowResult res, DesignSettings set, string path)
        {
            var w = new DxfWriter();
            Compose(w, res, set);
            w.Save(path);
        }

        /// <summary>Dựng toàn bộ shop drawing lên một bề mặt vẽ bất kỳ (DXF, AutoCAD…), gốc (0,0) ở góc trên trái.</summary>
        public static void Compose(IDraw2D w, WorkflowResult res, DesignSettings set)
        {
            var comp = new ShopDrawingComposer(w, set);
            double TH = comp.PrintTextHeight * comp.ElevationScale;
            double y = 0;

            // ---- Dầm: mặt đứng + các mặt cắt bên phải
            foreach (var e in res.Elements.Where(x => x.Category == "Dầm"))
            {
                var g = (BeamChainGeometry)e.Geometry;
                double x0 = g.Supports.First().FaceLeft;
                double hElev = comp.DrawBeamElevation(e, -x0, y);
                double sx = g.Length + 3000;
                double hSec = 0;
                foreach (var sec in g.Sections.GroupBy(s => s.Name).Select(gp => gp.First()))
                {
                    hSec = Math.Max(hSec, comp.DrawBeamSection(e, sec.X, sec.Name, sx + g.B, y, 2, TH));
                    sx += g.B * 2 + TH * 14;
                }
                y -= Math.Max(hElev, hSec) + 3000;
            }

            // ---- Cột: mặt đứng + mặt cắt
            double cx = 0, rowH = 0;
            foreach (var e in res.Elements.Where(x => x.Category == "Cột"))
            {
                var g = (ColumnGeometry)e.Geometry;
                double oz = y - (g.TopZ + g.LapAbove) - TH * 6;
                double hEl = comp.DrawColumnElevation(e, cx + TH * 12, oz);
                double hSe = comp.DrawColumnSection(e, cx + TH * 30, y - TH * 4, 2, TH);
                rowH = Math.Max(rowH, Math.Max(hEl, hSe) + TH * 8);
                cx += TH * 30 + g.B * 2 + TH * 22;
                if (cx > 60000) { cx = 0; y -= rowH; rowH = 0; }
            }
            y -= rowH + 3000;

            // ---- Bảng thống kê
            comp.DrawSchedule(res.Schedule, 0, y, TH);
        }
    }
}

using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>Trang tính "Dao động riêng" của bảng Excel: nguồn khối lượng, khối lượng tầng, chu kỳ + tỷ lệ khối lượng tham gia
/// (như bảng "Modal Participating Mass Ratios" của ETABS), phổ phản ứng.</summary>
public static class ModalReport
{
    public static void AddSheet(Project p, ModalResult mr, XlsxSheet sh)
    {
        sh.FirstRowIsHeader = false; sh.FreezeRows = 0;
        sh.SetColumnWidths(10, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 14);
        sh.AddRow(new XlCell("PHÂN TÍCH DAO ĐỘNG RIÊNG", XlStyle.Title));
        sh.AddRow("Nguồn khối lượng", mr.MassSource);
        sh.AddRow("Sàn cứng", mr.Diaphragm ? "Có" : "Không", "Khối lượng đứng", mr.VerticalMass ? "Có" : "Không");
        sh.AddRow("Phương pháp giải", mr.Solver);
        sh.AddRow("Bậc tự do động", mr.DynamicDofs, "Số dạng", mr.Modes.Count);
        sh.AddRow("MX (t)", new XlCell(mr.TotalMass[0], XlStyle.Number2), "MY (t)", new XlCell(mr.TotalMass[1], XlStyle.Number2),
            "Khối lượng tại gối (t)", new XlCell(mr.BaseMass, XlStyle.Number2));
        sh.AddRow("T1 (s)", new XlCell(mr.First?.Period ?? 0, XlStyle.Number3), "Ct·H^0,75 (s)", new XlCell(mr.T1Formula, XlStyle.Number3), "H (m)", new XlCell(mr.Height, XlStyle.Number2));

        sh.AddEmptyRow();
        sh.AddBoldRow("TỶ LỆ KHỐI LƯỢNG THAM GIA DAO ĐỘNG (Modal Participating Mass Ratios)");
        sh.AddHeaderRow("Dạng", "T (s)", "f (Hz)", "ω (rad/s)", "UX", "UY", "UZ", "RZ", "ΣUX", "ΣUY", "ΣRZ", "Phương");
        foreach (var m in mr.Modes)
            sh.AddRow(m.Index, new XlCell(m.Period, XlStyle.Number3), new XlCell(m.Frequency, XlStyle.Number3), new XlCell(m.Omega, XlStyle.Number3),
                new XlCell(m.Ratio[0], XlStyle.Number3), new XlCell(m.Ratio[1], XlStyle.Number3), new XlCell(m.Ratio[2], XlStyle.Number3), new XlCell(m.Ratio[3], XlStyle.Number3),
                new XlCell(m.Cumulative[0], XlStyle.Number3), new XlCell(m.Cumulative[1], XlStyle.Number3), new XlCell(m.Cumulative[3], XlStyle.Number3), m.Dominant);

        if (mr.Stories.Count > 0)
        {
            sh.AddEmptyRow();
            sh.AddBoldRow("KHỐI LƯỢNG TẦNG, TÂM KHỐI LƯỢNG, TÂM CỨNG");
            sh.AddHeaderRow("Tầng", "Cao độ (m)", "M (t)", "W (kN)", "Xm (m)", "Ym (m)", "Xr (m)", "Yr (m)", "Im (t·m²)", "Lx (m)", "Ly (m)");
            foreach (var s in mr.Stories)
                sh.AddRow(s.Story, new XlCell(s.Elevation, XlStyle.Number3), s.Mass, s.Weight, s.Xm, s.Ym,
                    double.IsNaN(s.Xr) ? "-" : (object)s.Xr, double.IsNaN(s.Yr) ? "-" : (object)s.Yr, s.Inertia, s.Lx, s.Ly);
        }

        foreach (var rc in mr.Spectrum)
        {
            sh.AddEmptyRow();
            sh.AddBoldRow($"PHỔ PHẢN ỨNG {rc.Pattern} – phương {rc.Dir} – {rc.Combination}");
            sh.AddHeaderRow("Dạng", "T (s)", "Sd/g", "Meff (t)", "Vk (kN)");
            foreach (var x in rc.Rows)
                sh.AddRow(x.Mode, new XlCell(x.Period, XlStyle.Number3), new XlCell(x.Sd, XlStyle.Number3), x.Meff, x.BaseShear);
            sh.AddRow("V phổ (kN)", rc.BaseShear, "Fb tĩnh (kN)", rc.StaticBaseShear, "T tĩnh (s)", new XlCell(rc.StaticPeriod, XlStyle.Number3), "Hệ số nhân", new XlCell(rc.ScaleFactor, XlStyle.Number3));
            if (rc.Torsion.Count > 0)
            {
                sh.AddHeaderRow("Tầng", "Fi (kN)", "Li (m)", "Mi (kN.m)");
                foreach (var t in rc.Torsion) sh.AddRow(t.story, t.force, t.length, t.moment);
            }
        }
        if (mr.Notes.Count > 0)
        {
            sh.AddEmptyRow();
            sh.AddBoldRow("GHI CHÚ");
            foreach (var n in mr.Notes) sh.AddRow(n);
        }
    }
}

using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Mep;

/// <summary>Một dòng thống kê vật tư điện nước.</summary>
public sealed record MepQtyRow(string Group, string Item, string Unit, double Qty, string Note = "");

/// <summary>Bảng thống kê vật tư điện nước: thiết bị (cái), ống theo vật liệu – Ø (m), ống gen (m), dây theo tiết diện (m ruột),
/// cáp (m), phụ kiện cút / chếch / tê / côn / măng sông / hộp nối (cái).</summary>
public static class MepQuantities
{
    public const string Title = "BẢNG THỐNG KÊ VẬT TƯ ĐIỆN NƯỚC";
    private static string N(double v) => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    public static List<MepQtyRow> Rows(Project p)
    {
        var m = p.Mep; var rows = new List<MepQtyRow>();
        if (m == null || m.IsEmpty) return rows;
        if (m.Fittings.Count == 0) MepRouting.RebuildAll(p);
        foreach (var g in m.Devices.GroupBy(d => d.Family).OrderBy(g => g.Key))
            rows.Add(new MepQtyRow("Thiết bị", g.Key, "cái", g.Count()));
        foreach (var g in m.Runs.Where(r => r.Kind == MepRunKind.Ong).GroupBy(r => (Mat: MepSystems.IsDrain(r.System) ? "uPVC" : "PPR PN20", r.Size)).OrderBy(g => g.Key.Mat).ThenBy(g => g.Key.Size))
            rows.Add(new MepQtyRow("Ống nước", $"Ống {g.Key.Mat} Ø{g.Key.Size:0}", "m", Math.Round(g.Sum(r => r.Length), 2), g.Key.Mat == "uPVC" ? "đi dưới sàn, có độ dốc" : "chôn ngầm tường / sàn"));
        foreach (var g in m.Runs.Where(r => r.Kind == MepRunKind.Ong && MepSystems.IsDrain(r.System)).GroupBy(r => r.Size).OrderBy(g => g.Key))
        {
            int nh = g.Sum(r => MepDrainage.Hangers(p, r));
            if (nh > 0) rows.Add(new MepQtyRow("Phụ kiện treo", $"Quang treo + ty ren ống uPVC Ø{g.Key:0}", "bộ", nh, $"khoảng cách ≤ {MepDrainage.HangerSpacing(m.Settings, g.Key):0.#} m"));
        }
        var elec = m.Runs.Where(r => r.Kind != MepRunKind.Ong).ToList();
        foreach (var g in elec.GroupBy(r => r.ConduitSize > 0 ? r.ConduitSize : MepSizing.ConduitFor(r.Size, Math.Max(1, r.Conductors))).OrderBy(g => g.Key))
            rows.Add(new MepQtyRow("Ống gen", $"Ống gen PVC Ø{g.Key:0}", "m", Math.Round(g.Sum(r => r.Length), 2), "luồn dây âm tường"));
        foreach (var g in elec.Where(r => r.Kind == MepRunKind.Day).GroupBy(r => r.Size).OrderBy(g => g.Key))
            rows.Add(new MepQtyRow("Dây điện", $"Dây CV {N(g.Key)} mm²", "m", Math.Round(g.Sum(r => r.Length * Math.Max(1, r.Conductors)), 2), "tổng chiều dài ruột (L+N+PE)"));
        foreach (var g in elec.Where(r => r.Kind == MepRunKind.Cap).GroupBy(r => (r.Conductors, r.Size)).OrderBy(g => g.Key.Size))
            rows.Add(new MepQtyRow("Cáp điện", $"Cáp CVV {Math.Max(1, g.Key.Conductors)}x{N(g.Key.Size)} mm²", "m", Math.Round(g.Sum(r => r.Length), 2)));
        foreach (var g in m.Fittings.GroupBy(f => f.Name).OrderBy(g => g.Key))
            rows.Add(new MepQtyRow("Phụ kiện", g.Key, "cái", g.Count()));
        // điều hoà – thông gió (Mep/MepHvac.cs)
        rows.AddRange(HvacQuantities.Rows(p));
        return rows;
    }

    public const string SheetName = "Vật tư điện nước";

    /// <summary>Bảng Excel / Word "Vật tư điện nước" (STT, nhóm, vật tư, đơn vị, khối lượng, ghi chú) + bảng tóm tắt độ dốc tuyến thoát; rỗng khi không có điện nước.</summary>
    public static List<KetCauVXT.Report.QtyTable> Tables(Project p)
    {
        var res = new List<KetCauVXT.Report.QtyTable>();
        var rows = Rows(p);
        if (rows.Count == 0) return res;
        var t = new KetCauVXT.Report.QtyTable
        {
            Title = "Thống kê vật tư điện nước (thiết bị, ống theo Ø, ống gen, dây, cáp, phụ kiện)",
            Headers = new[] { "STT", "Nhóm", "Vật tư", "Đơn vị", "Khối lượng", "Ghi chú" },
            Widths = new double[] { 6, 16, 40, 8, 12, 30 }
        };
        int k = 0;
        foreach (var r in rows) t.Add(++k, r.Group, r.Item, r.Unit, r.Qty, r.Note);
        res.Add(t);
        var chains = MepLongSection.Chains(p);
        if (chains.Count > 0)
        {
            var d = new KetCauVXT.Report.QtyTable
            {
                Title = "Tuyến thoát nước: chiều dài ngang, độ dốc, cao độ đáy ống đầu / cuối",
                Headers = new[] { "Tuyến", "Tầng", "Ø (mm)", "Chiều dài ngang (m)", "Độ dốc i", "Đáy ống đầu (m)", "Đáy ống cuối (m)" },
                Widths = new double[] { 14, 10, 8, 14, 10, 14, 14 }
            };
            foreach (var ch in chains)
                foreach (var sg in MepLongSection.Segments(MepLongSection.Points(ch)).GroupBy(q => q.RunId))
                {
                    var pts = MepLongSection.Points(ch).Where(q => q.RunId == sg.Key).ToList();
                    double L = sg.Sum(q => q.Length), drop = sg.Sum(q => q.Drop);
                    d.Add("T" + sg.Key, ch[0].Story, sg.First().Size, Math.Round(L, 2), MepLongSection.Pct(L > 1e-9 ? drop / L : 0),
                        pts.Count > 0 ? Math.Round(pts[0].Invert, 3) : 0.0, pts.Count > 0 ? Math.Round(pts[^1].Invert, 3) : 0.0);
                }
            if (d.Rows.Count > 0) res.Add(d);
        }
        // bảng tủ điện, bể nước / bơm, tải lạnh chi tiết – Mep/MepRound4.cs
        res.AddRange(MepRound4.Tables(p));
        return res;
    }

    /// <summary>Đếm phụ kiện theo loại.</summary>
    public static int Count(Project p, MepFittingKind k) => p.Mep.Fittings.Count(f => f.Kind == k);

    /// <summary>Bảng thống kê dạng hình DXF (tỷ lệ 1/1, đơn vị mm giấy).</summary>
    public static Figure ScheduleFigure(Project p)
    {
        var rows = Rows(p);
        if (rows.Count == 0) return null;
        var w = new DxfWriter(1);
        double th = 2.5, rh = 7;
        var cw = new[] { 12.0, 30, 70, 14, 20, 50 };
        var heads = new[] { "STT", "NHÓM", "VẬT TƯ / THIẾT BỊ", "ĐV", "KHỐI LƯỢNG", "GHI CHÚ" };
        double tw = cw.Sum(), y = 0;
        w.Text(Lay.Chu, tw / 2, y + 4, 1.6 * th, Title, 0, 1, 0);
        var table = new List<string[]> { heads };
        int i = 1;
        foreach (var r in rows) table.Add(new[] { (i++).ToString(), r.Group, r.Item, r.Unit, N(r.Qty), r.Note });
        w.Line(Lay.KhungTen, 0, y, tw, y);
        foreach (var row in table)
        {
            double cx = 0;
            for (int c = 0; c < cw.Length; c++)
            {
                bool left = c is 1 or 2 or 5;
                w.Text(Lay.Chu, left ? cx + 1.5 : cx + cw[c] / 2, y - rh / 2, th, row[c] ?? "", 0, left ? 0 : 1, 2);
                cx += cw[c];
            }
            y -= rh;
            w.Line(Lay.KhungTen, 0, y, tw, y);
        }
        double xx = 0;
        for (int c = 0; c <= cw.Length; c++) { w.Line(Lay.KhungTen, xx, 0, xx, y); if (c < cw.Length) xx += cw[c]; }
        return new Figure(w, Title);
    }
}

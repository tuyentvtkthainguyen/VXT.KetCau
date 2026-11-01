using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using VXT.KetCau.Core.Codes;
using VXT.KetCau.Core.Detailing;
using VXT.KetCau.Core.Model;
using VXT.KetCau.Core.Workflow;

namespace VXT.KetCau.Core.Report
{
    /// <summary>Thuyết minh tính toán kết cấu (HTML — mở bằng Word để xuất .docx/.pdf).</summary>
    public static class CalcReport
    {
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        static string F(double v, string f = "0.0") => v.ToString(f, Ci);
        static string E(string s) => WebUtility.HtmlEncode(s ?? "");

        public static string Html(StructModel m, WorkflowResult r, string engineName)
        {
            var c = ConcreteGrade.Get(m.Settings.Concrete);
            var s = SteelGrade.Get(m.Settings.MainSteel);
            var sw = SteelGrade.Get(m.Settings.StirrupSteel);
            var sb = new StringBuilder();
            sb.Append("<!doctype html><html lang=\"vi\"><head><meta charset=\"utf-8\"><title>Thuyết minh kết cấu</title><style>");
            sb.Append("body{font-family:'Times New Roman',serif;font-size:13pt;max-width:900px;margin:24px auto;color:#111}h1{text-align:center;font-size:18pt}h2{font-size:14pt;border-bottom:1px solid #999;margin-top:28px}");
            sb.Append("table{border-collapse:collapse;width:100%;margin:8px 0;font-size:11pt}th,td{border:1px solid #666;padding:3px 6px;text-align:center}th{background:#eee}.l{text-align:left}.ng{color:#b00;font-weight:bold}.ok{color:#070}");
            sb.Append("</style></head><body>");
            sb.Append($"<h1>THUYẾT MINH TÍNH TOÁN KẾT CẤU<br><small>{E(m.ProjectName)}</small></h1>");

            sb.Append("<h2>1. Căn cứ tính toán</h2><ul>");
            sb.Append("<li>TCVN 5574:2018 — Thiết kế kết cấu bê tông và bê tông cốt thép.</li>");
            sb.Append("<li>TCVN 2737:2023 — Tải trọng và tác động.</li>");
            if (m.Settings.SeismicAgR > 0) sb.Append("<li>TCVN 9386:2012 — Thiết kế công trình chịu động đất.</li>");
            sb.Append($"<li>Phân tích nội lực: {E(engineName)}.</li></ul>");

            sb.Append("<h2>2. Vật liệu</h2><table><tr><th>Vật liệu</th><th>Cấp/mác</th><th>Cường độ tính toán (MPa)</th><th>Mô đun đàn hồi (MPa)</th></tr>");
            sb.Append($"<tr><td class=l>Bê tông</td><td>{c.Name}</td><td>Rb = {F(c.Rb, "0.0")}; Rbt = {F(c.Rbt, "0.00")}</td><td>{F(c.Eb, "0")}</td></tr>");
            sb.Append($"<tr><td class=l>Thép dọc</td><td>{s.Name}</td><td>Rs = {F(s.Rs, "0")}; Rsc = {F(s.Rsc, "0")}</td><td>{F(s.Es, "0")}</td></tr>");
            sb.Append($"<tr><td class=l>Thép đai</td><td>{sw.Name}</td><td>Rsw = {F(sw.Rsw, "0")}</td><td>{F(sw.Es, "0")}</td></tr></table>");
            sb.Append($"<p>ξR = {F(Tcvn5574.XiR(s), "0.000")}; αR = {F(Tcvn5574.AlphaR(s), "0.000")}. Lớp bảo vệ: dầm {m.Settings.CoverBeam:0}, cột {m.Settings.CoverColumn:0}, sàn {m.Settings.CoverSlab:0}, móng {m.Settings.CoverFooting:0} mm.</p>");

            sb.Append("<h2>3. Tải trọng và tổ hợp</h2><table><tr><th>Ô sàn</th><th>Công năng</th><th>Tĩnh tải HT (kN/m²)</th><th>Hoạt tải (kN/m²)</th><th>γf</th></tr>");
            foreach (var g in m.Slabs.GroupBy(x => (x.Usage, x.SuperDead, x.LiveLoad)))
                sb.Append($"<tr><td>{g.Count()} ô</td><td class=l>{E(g.Key.Usage)}</td><td>{F(g.Key.SuperDead, "0.00")}</td><td>{F(g.Key.LiveLoad, "0.00")}</td><td>{F(Tcvn2737.LiveFactor(g.Key.LiveLoad), "0.0")}</td></tr>");
            sb.Append("</table><table><tr><th>Tổ hợp</th><th class=l>Thành phần</th></tr>");
            foreach (var cb in m.Combos)
                sb.Append($"<tr><td>{E(cb.Name)}</td><td class=l>{E(cb.IsEnvelope ? "Bao các tổ hợp" : string.Join(" + ", cb.Items.Select(i => $"{F(i.Factor, "0.00")}×{i.Case}")))}</td></tr>");
            sb.Append("</table>");

            sb.Append("<h2>4. Tính toán dầm</h2><table><tr><th>Dầm</th><th>Tầng</th><th>b×h</th><th>M⁻ trái</th><th>M⁺</th><th>M⁻ phải</th><th>Q max</th><th>Thép trên trái</th><th>Thép dưới</th><th>Thép trên phải</th><th>Đai gối/nhịp</th><th>KL</th></tr>");
            foreach (var b in m.Beams.Where(x => x.Design != null).OrderBy(x => x.Story).ThenBy(x => x.Label))
            {
                var d = b.Design; var f = b.Forces ?? new BeamForces();
                sb.Append($"<tr><td>{E(b.Label)}</td><td>{E(b.Story)}</td><td>{d.B:0}×{d.H:0}</td><td>{F(f.MnegLeft)}</td><td>{F(f.MposSpan)}</td><td>{F(f.MnegRight)}</td><td>{F(Math.Max(f.VmaxLeft, f.VmaxRight))}</td>");
                sb.Append($"<td>{d.TopLeftBars} ({F(d.TopLeft.AsReq, "0")})</td><td>{d.BottomBars} ({F(d.Bottom.AsReq, "0")})</td><td>{d.TopRightBars} ({F(d.TopRight.AsReq, "0")})</td><td>{d.StirrupSupport}/{d.StirrupSpan}</td>");
                sb.Append(d.Ok ? "<td class=ok>Đạt</td></tr>" : "<td class=ng>Không đạt</td></tr>");
            }
            sb.Append("</table><p>Đơn vị: M (kN·m), Q (kN); trong ngoặc là As yêu cầu (mm²).</p>");

            sb.Append("<h2>5. Tính toán cột</h2><table><tr><th>Cột</th><th>Vị trí</th><th>Tầng</th><th>b×h</th><th>λ</th><th>Thép dọc</th><th>μ (%)</th><th>Đai thường/dày</th><th>Hệ số sử dụng</th><th>Tổ hợp</th></tr>");
            foreach (var col in m.Columns.Where(x => x.Design != null).OrderBy(x => x.Label).ThenBy(x => x.BaseZ))
            {
                var d = col.Design;
                sb.Append($"<tr><td>{E(col.Label)}</td><td>{col.Location}</td><td>{E(col.BaseStory)}→{E(col.TopStory)}</td><td>{d.B:0}×{d.H:0}</td><td>{F(d.Lambda, "0")}</td><td>{d.BarsText()}</td><td>{F(d.Mu * 100, "0.00")}</td><td>{d.Stirrup}/{d.StirrupDense}</td>");
                sb.Append($"<td class={(d.Ok ? "ok" : "ng")}>{F(d.Ratio, "0.00")}</td><td>{E(d.GoverningCombo)}</td></tr>");
            }
            sb.Append("</table>");

            sb.Append("<h2>6. Tính toán sàn</h2><table><tr><th>Ô sàn</th><th>L1×L2</th><th>h</th><th>Loại</th><th>M1/M2 nhịp</th><th>M1/M2 gối</th><th>Thép dưới L1/L2</th><th>Thép mũ</th></tr>");
            foreach (var sl in m.Slabs.Where(x => x.Design != null))
            {
                var d = sl.Design;
                sb.Append($"<tr><td>{E(sl.Label)}</td><td>{d.L1:0}×{d.L2:0}</td><td>{d.Thickness:0}</td><td>{(d.TwoWay ? "Bản kê" : "Bản dầm")}</td><td>{F(d.M1Span, "0.00")}/{F(d.M2Span, "0.00")}</td><td>{F(d.M1Sup, "0.00")}/{F(d.M2Sup, "0.00")}</td><td>{d.Bottom1}/{d.Bottom2}</td><td>{d.Top1}/{d.Top2}</td></tr>");
            }
            sb.Append("</table>");

            if (r?.Schedule != null)
            {
                sb.Append("<h2>7. Tổng hợp cốt thép</h2><table><tr><th>Đường kính</th>");
                var byD = r.Schedule.ByDiameter().ToList();
                foreach (var x in byD) sb.Append($"<th>Ø{x.D}</th>");
                sb.Append("</tr><tr><td>Chiều dài (m)</td>");
                foreach (var x in byD) sb.Append($"<td>{F(x.LengthM, "0.0")}</td>");
                sb.Append("</tr><tr><td>Trọng lượng (kg)</td>");
                foreach (var x in byD) sb.Append($"<td>{F(x.Kg, "0.0")}</td>");
                var g = r.Schedule.ByGroup();
                sb.Append($"</tr></table><p>Nhóm Ø≤10: {F(g.G1, "0.0")} kg; nhóm 10&lt;Ø≤18: {F(g.G2, "0.0")} kg; nhóm Ø&gt;18: {F(g.G3, "0.0")} kg. <b>Tổng: {F(g.Total, "0.0")} kg</b>.</p>");
            }

            if (r != null && r.Warnings.Count > 0)
            {
                sb.Append("<h2>8. Các điểm cần kiểm tra</h2><ul>");
                foreach (var w in r.Warnings.Distinct()) sb.Append($"<li>{E(w)}</li>");
                sb.Append("</ul>");
            }
            sb.Append("<p style=\"margin-top:32px;font-style:italic\">Thuyết minh được lập tự động bởi VXT Kết cấu. Kỹ sư thiết kế chịu trách nhiệm kiểm tra số liệu đầu vào, mô hình và kết quả trước khi phát hành hồ sơ.</p>");
            sb.Append("</body></html>");
            return sb.ToString();
        }
    }
}

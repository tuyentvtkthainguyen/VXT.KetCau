using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>[MóngLệchTâm] Thuyết minh: mục "Móng lệch tâm" – ranh giới đất, công thức (e0, M = N·e0, pmax/pmin, tiếp xúc một phần, đòn bẩy R1,
/// giảm tải móng cột trong, dầm cân bằng, móng kết hợp, móng băng lệch tâm), ví dụ tính toán và bảng tổng hợp. Gọi từ CalculationReport.Export / Auto.</summary>
public static partial class CalculationReport
{
    /// <summary>true nếu có móng lệch tâm / dầm cân bằng / móng băng lệch tâm cần thuyết minh.</summary>
    private static bool HasEccentric(DesignSummary s) =>
        s != null && (s.Footings.Any(f => f.Eccentric != null) || s.PileCaps.Any(c => c.Eccentric != null)
                      || (s.StrapBeams?.Count ?? 0) > 0 || s.StripFootings.Any(x => x.FlangeSide != 0));

    private static void EccentricSection(Project p, DesignSummary s, DocxWriter d, int n)
    {
        d.Heading($"{n}. MÓNG LỆCH TÂM (CỘT SÁT RANH GIỚI ĐẤT)", 1);
        var fs = p.Foundation ?? new FoundationSettings();
        var lines = EccentricFooting.Lines(p);
        d.Paragraph("Nhà phố có cột đặt sát ranh giới đất: móng không được vượt ra ngoài ranh giới nên tâm đáy móng lệch so với tim cột (móng lệch tâm). "
                    + $"Mép móng phía ranh giới đặt cách ranh giới {Mm(fs.BoundaryClearance)} mm; cột có mép cách ranh giới ≤ {Mm(fs.BoundaryTolerance)} mm được coi là cột sát ranh.");
        if (lines.Count > 0)
            d.Table(new[] { "Cạnh ranh giới", "Theo trục", "Khoảng cách ra ngoài trục (mm)", "Toạ độ (m)" },
                lines.Select(t => new[] { PropertyBoundary.SideName(t.B.Side), string.IsNullOrWhiteSpace(t.B.Grid) ? "-" : t.B.Grid, Mm(t.B.Offset), N(t.Coord, "0.000") }),
                new[] { 4.0, 2.5, 4.5, 3.0 }, 10);

        d.Heading($"{n}.1. Công thức tính toán", 2);
        d.Paragraph("Độ lệch tâm và áp lực đáy móng (TCVN 9362:2012 mục 4.6; nội lực tiêu chuẩn = tính toán / hệ số vượt tải trung bình):", true);
        d.Equations(new[]
        {
            "e0 = x_tâm móng − x_tim cột (mép móng = ranh giới − khoảng hở);   M = N·e0 + M0 + Q·Df (cộng trị tuyệt đối – thiên về an toàn)",
            "e = M/(N + G);  e ≤ L/6:  pmax,min = (N + G)/(L·B)·(1 ± 6e/L);   ptb ≤ R,  pmax ≤ 1,2R,  pmin ≥ 0",
            "e > L/6 (tách nền một phần):  pmax = 2(N + G)/(3·B·c0),  c0 = L/2 − e;  vùng tiếp xúc 3c0 ≥ 0,75L"
        });
        d.Paragraph("(a) Móng lệch tâm có dầm móng cân bằng (dầm đỡ – strap footing): dầm DM tuyệt đối cứng nối móng biên với móng cột trong cùng trục làm việc như đòn bẩy:", true);
        d.Equations(new[]
        {
            "R1 = N1·L/(L − e)   (L: khoảng cách tim 2 cột, e: độ lệch tâm móng biên);  hai móng biên đối diện: R1 = [N1·(L − e2) − N2·e2]/(L − e1 − e2)",
            $"ΔR = R1 − N1 = N1·e/(L − e);   móng cột trong: N2' = N2 − β·ΔR, β = {N(fs.StrapReliefFactor, "0.##")} (thực hành chỉ kể ≤ 50% phần giảm tải vì hoạt tải có thể không đồng thời)",
            "Móng biên: áp lực đều p = R1/(L·B) + γtb·Df (mô men lệch tâm do dầm tiếp nhận);   dầm DM: M = N1·e + M0 (lớn nhất phía cột biên, căng trên), Q = R1 − N1",
            "Dầm phải cứng hơn cột: I_dầm/L ≥ 2·I_cột/H_tầng 1;  đáy dầm không tựa lên nền (đệm xốp / cát rời) để dầm làm việc như đòn bẩy"
        });
        d.Paragraph("(b) Móng lệch tâm tự cân bằng: đáy móng chịu toàn bộ mô men M = N·e0 + M0; điều kiện pmax ≤ 1,2R, pmin ≥ 0 hoặc tiếp xúc ≥ 75%. "
                    + "Móng sát ranh có e0 ≈ L/2 − hc/2 nên điều kiện tiếp xúc thường không thoả – khi đó mô men ΔM = N·e0 phải do cột tầng 1 và hệ giằng móng tiếp nhận "
                    + "(chương trình ghi ΔM để người thiết kế kiểm tra thêm cột / giằng).", true);
        d.Paragraph("(c) Móng kết hợp (móng phối hợp): một đáy móng chung cho cột biên và cột trong, hợp lực trùng trọng tâm đáy móng:", true);
        d.Equations(new[]
        {
            "x̄ = (N1·x1 + N2·x2)/(N1 + N2) (đo từ mép móng tại ranh giới);  chữ nhật: L = 2·x̄;  hình thang: B1 + B2 = 2A/L,  x̄ = L·(B1 + 2B2)/(3·(B1 + B2))",
            "Phương dọc: dầm ngược chịu áp lực nền ròng hướng lên, 2 gối là chân cột → mô men âm giữa 2 cột (thép lớp trên), mô men dương ở đầu vươn (thép lớp dưới)"
        });
        d.Paragraph("Chọc thủng cột sát mép móng (TCVN 5574:2018 mục 8.1.6.2): tháp 45° và chu vi u (cách mép cột h0/2) bị cắt bởi mép móng – cột biên tính chu vi 3 phía, cột góc 2 phía: F = N − p·A_tháp ≤ Rbt·u·h0.");
        d.Paragraph("Đài cọc lệch tâm: cọc bố trí trong ranh giới, nhóm cọc lệch tâm so với tim cột → Pi = (N + G)/n + ax·(xi − x̄) + ay·(yi − ȳ) tự kể mô men (N + G)·x̄; "
                    + "có dầm cân bằng: lực quy đổi N' = R1 đặt tại trọng tâm nhóm cọc (phản lực cọc đều theo phương dầm).");

        // ---- ví dụ móng đơn lệch tâm ----
        var edge = s.Footings.Where(f => f.Eccentric != null && f.Eccentric.E0 > 1e-6).OrderByDescending(f => f.Eccentric.Scheme == EccentricScheme.Strap ? 1 : 0).ThenByDescending(f => f.N).FirstOrDefault();
        int sub = 2;
        if (edge != null)
        {
            var e = edge.Eccentric;
            var so = p.Soil;
            d.Heading($"{n}.{sub++}. Ví dụ tính toán: móng {edge.Name} (nút {edge.NodeId}) – {EccentricOverride.SchemeName(e.Scheme)}", 2);
            d.Paragraph($"Ranh giới: {e.Sides}. Kích thước đáy L × B × H = {N(edge.L)} × {N(edge.B)} × {N(edge.H)} m; độ lệch tâm tâm móng – tim cột ex = {Mm(e.OffsetX)} mm, ey = {Mm(e.OffsetY)} mm. "
                        + $"Nội lực tính toán (tổ hợp {edge.Combo}): N = {N(edge.N, "0.0")} kN, Mx = {N(edge.Mx, "0.0")}, My = {N(edge.My, "0.0")} kN.m. Đề xuất của chương trình: {EccentricOverride.SchemeName(e.Recommended)} – {e.Recommendation}.");
            double kt = Math.Max(so.LoadFactorAvg, 1.0), A = edge.L * edge.B, G = so.GammaAvg * so.Depth * A;
            if (e.Scheme == EccentricScheme.Strap)
            {
                double Ls = e.SpanX > 0 ? e.SpanX : e.SpanY, ee = e.SpanX > 0 ? Math.Abs(e.OffsetX) : Math.Abs(e.OffsetY);
                double R1 = EccentricFooting.LeverReaction(e.N1, 0, Ls, ee);
                d.Equations(new[]
                {
                    $"R1 = N1·L/(L − e) = {N(e.N1, "0.0")}·{N(Ls)}/({N(Ls)} − {N(ee, "0.000")}) = {N(R1, "0.0")} kN;   ΔR = R1 − N1 = {N(R1 - e.N1, "0.0")} kN",
                    $"ptb = R1/kt/(L·B) + γtb·Df = {N(R1 / kt / A + so.GammaAvg * so.Depth, "0.0")} kN/m²  (chương trình: ptb = {N(edge.Ptb, "0.0")}, pmax = {N(edge.Pmax, "0.0")} kN/m², R = {N(edge.R, "0.0")} kN/m²)",
                    $"Móng cột trong: N2' = N2 − β·ΔR = N2 − {N(fs.StrapReliefFactor, "0.##")}·{N(R1 - e.N1, "0.0")} = N2 − {N(fs.StrapReliefFactor * Math.Max(R1 - e.N1, 0), "0.0")} kN"
                });
            }
            else
            {
                double ntot = edge.N / kt + G, ee = Math.Abs(e.OffsetX) > Math.Abs(e.OffsetY) ? Math.Abs(e.OffsetX) : Math.Abs(e.OffsetY);
                double len = Math.Abs(e.OffsetX) > Math.Abs(e.OffsetY) ? edge.L : edge.B, wid = A / len;
                double er = edge.N / kt * ee / ntot;
                d.Equations(new[]
                {
                    $"M = N·e0 = {N(edge.N, "0.0")}·{N(ee, "0.000")} = {N(edge.N * ee, "0.0")} kN.m (tính toán);   e = M/kt/(N/kt + G) = {N(er, "0.000")} m (L/6 = {N(len / 6, "0.000")} m)",
                    er <= len / 6 + 1e-9
                        ? $"pmax = (N + G)/A·(1 + 6e/L) = {N(ntot / A * (1 + 6 * er / len), "0.0")} kN/m²;  pmin = {N(ntot / A * (1 - 6 * er / len), "0.0")} kN/m²"
                        : $"e > L/6: c0 = L/2 − e = {N(len / 2 - er, "0.000")} m;  pmax = 2(N + G)/(3B·c0) = {N(FootingBehaviour.PartialContactPmax(ntot, er, len, wid), "0.0")} kN/m²;  tiếp xúc 3c0/L = {N(Math.Max(3 * (len / 2 - er) / len, 0) * 100, "0")}%",
                    $"Chương trình (tổ hợp bất lợi, kể M0, Q·Df): ptb = {N(edge.Ptb, "0.0")}, pmax = {N(Math.Min(edge.Pmax, 99999), "0.0")}, pmin = {N(edge.Pmin, "0.0")} kN/m², R = {N(edge.R, "0.0")} kN/m²",
                    $"Mô men lệch tâm truyền vào chân cột / giằng móng: ΔMx = {N(e.ColumnMomentX, "0.0")} kN.m, ΔMy = {N(e.ColumnMomentY, "0.0")} kN.m"
                });
            }
            if (e.Options.Count > 0)
            {
                d.Paragraph("So sánh phương án:", false, true);
                foreach (var o in e.Options) d.Bullet(o);
            }
            foreach (var c in edge.Checks)
                d.Equation($"{c.Name}: {N(c.Demand)} / {N(c.Capacity)} {c.Unit} → tỷ số {N(c.Ratio, "0.000")} – {OkText(c.Ok)}");
            foreach (var m in edge.Messages.Where(x => x.StartsWith("LỖI") || x.StartsWith("Cảnh báo"))) d.Paragraph(m, false, true);
        }

        // ---- dầm cân bằng ----
        var sb0 = (s.StrapBeams ?? new List<StrapBeamDesign>()).OrderByDescending(x => x.M).FirstOrDefault();
        if (sb0 != null)
        {
            var l = sb0.Links[0];
            d.Heading($"{n}.{sub++}. Ví dụ tính toán: dầm móng cân bằng {sb0.Name} ({Mm(sb0.B)}×{Mm(sb0.H)}, nút {l.NodeA} – {l.NodeB})", 2);
            d.Equations(new[]
            {
                $"L = {N(sb0.Span)} m;  e1 = {Mm(sb0.E1)} mm{(sb0.E2 > 0 ? $", e2 = {Mm(sb0.E2)} mm" : "")};  N1 = {N(sb0.N1, "0.0")} kN (tổ hợp {sb0.Combo});  R1 = {N(sb0.R1, "0.0")} kN",
                $"M = N1·e1 + M0 = {N(sb0.M, "0.0")} kN.m → As,trên = {Cm2(sb0.AsTop)} cm² → {sb0.TopBars};  thép dưới (cấu tạo ≥ 30% As,trên) {sb0.BottomBars}",
                $"Q = R1 − N1 = {N(sb0.V, "0.0")} kN → đai {sb0.Stirrups};   độ cứng I/L = {N(sb0.StiffBeam, "0.00000")} ≥ 2·Ic/Hc = {N(sb0.StiffColumn, "0.00000")} m³"
            });
            d.Table(new[] { "Hạng mục", "Giá trị", "Giới hạn", "Đơn vị", "Tỷ số", "Kết luận" },
                sb0.Checks.Select(c => new[] { c.Name, N(c.Demand, "0.###"), N(c.Capacity, "0.###"), c.Unit, N(c.Ratio, "0.000"), OkText(c.Ok) }),
                new[] { 6.0, 1.8, 1.8, 1.3, 1.4, 1.8 }, 9);
        }

        // ---- móng kết hợp ----
        var comb = s.Footings.FirstOrDefault(f => f.Eccentric?.Combined != null);
        if (comb != null)
        {
            var c = comb.Eccentric.Combined;
            d.Heading($"{n}.{sub++}. Móng kết hợp {comb.Name} (nút {c.NodeA} + {c.NodeB})", 2);
            d.Equations(new[]
            {
                $"N1 = {N(c.N1, "0.0")} kN, N2 = {N(c.N2, "0.0")} kN;  x̄ = (N1·x1 + N2·x2)/(N1 + N2) = {N(c.Resultant, "0.000")} m từ mép ranh giới;  trọng tâm đáy móng {N(c.Centroid, "0.000")} m",
                c.Trapezoid ? $"Hình thang L = {N(c.Length)} m, B1 = {N(c.B1)} m (phía ranh giới), B2 = {N(c.B2)} m" : $"Chữ nhật L = {N(c.Length)} m × B = {N(c.B1)} m",
                $"M− = {N(c.MNeg, "0.0")} kN.m (giữa 2 cột), M+ = {N(c.MPos, "0.0")} kN.m;  Q = {N(c.V, "0.0")} kN;  ptb = {N(comb.Ptb, "0.0")}, pmax = {N(comb.Pmax, "0.0")} kN/m², R = {N(comb.R, "0.0")}"
            });
        }

        // ---- móng băng lệch tâm ----
        var sl = s.StripFootings.Where(x => x.FlangeSide != 0).ToList();
        if (sl.Count > 0)
        {
            d.Heading($"{n}.{sub++}. Móng băng lệch tâm trên ranh giới", 2);
            d.Paragraph("Dải móng băng đặt trên ranh giới: sườn sát ranh giới, bản cánh chỉ vươn vào trong (công xôn B − b). Tim cột lệch tâm so với tâm bản đáy e → "
                        + "mô men xoắn phân bố t = q·e (q = ΣN/L); nếu không có giằng ngang, sườn chịu xoắn T = t·l/2 (kiểm tra sơ bộ T ≤ 0,1·Rb·b²·h, TCVN 5574:2018 mục 8.1.4). "
                        + "Khuyến nghị bố trí giằng ngang tại mỗi chân cột chịu M = N·e.");
            d.Table(new[] { "Dải", "Ranh giới", "B (m)", "Sườn b×h", "Cánh (mm)", "e (mm)", "t (kN.m/m)", "T (kN.m)", "M = N·e lớn nhất" },
                sl.Select(x => new[]
                {
                    x.Name, x.Boundary, N(x.Width), $"{Mm(x.RibB)}×{Mm(x.RibH)}", Mm(Math.Max(x.Width - x.RibB, 0)), Mm(x.Eccentricity), N(x.TorsionPerM, "0.0"), N(x.TorsionMax, "0.0"),
                    N(x.ColumnTorsion.Count > 0 ? x.ColumnTorsion.Max() : 0, "0.0")
                }), new[] { 1.2, 2.6, 1.2, 1.8, 1.4, 1.2, 1.6, 1.4, 2.2 }, 9);
        }

        // ---- bảng tổng hợp ----
        var rows = new List<string[]>();
        foreach (var f in s.Footings.Where(x => x.Eccentric != null))
            rows.Add(new[] { f.Name, "Móng đơn", f.Eccentric.Sides, EccentricOverride.SchemeName(f.Eccentric.Scheme), Mm(f.Eccentric.OffsetX), Mm(f.Eccentric.OffsetY),
                             $"{N(f.L)}×{N(f.B)}×{N(f.H)}", N(Math.Min(f.Pmax, 99999), "0"), N(f.R, "0"), OkText(f.Ok) });
        foreach (var c in s.PileCaps.Where(x => x.Eccentric != null))
            rows.Add(new[] { c.Name, "Đài cọc", c.Eccentric.Sides, EccentricOverride.SchemeName(c.Eccentric.Scheme), Mm(c.OffsetX), Mm(c.OffsetY),
                             $"{N(c.L)}×{N(c.B)}×{N(c.H)}", N(c.PMax, "0") + " (cọc)", N(c.PileCapacity, "0") + " [P]", OkText(c.Ok) });
        if (rows.Count > 0)
        {
            d.Heading($"{n}.{sub++}. Bảng tổng hợp móng lệch tâm", 2);
            d.Table(new[] { "Móng", "Loại", "Ranh giới", "Phương án", "ex (mm)", "ey (mm)", "L×B×H (m)", "pmax", "R", "KL" }, rows,
                new[] { 1.2, 1.5, 1.8, 3.2, 1.2, 1.2, 2.4, 1.4, 1.4, 1.5 }, 9);
        }
        if ((s.StrapBeams?.Count ?? 0) > 0)
            d.Table(new[] { "Dầm", "SL", "b×h (mm)", "Nhịp (m)", "M (kN.m)", "Q (kN)", "Thép trên", "Thép dưới", "Đai", "KL" },
                s.StrapBeams.Select(x => new[] { x.Name, x.Quantity.ToString(Inv), $"{Mm(x.B)}×{Mm(x.H)}", N(x.Span), N(x.M, "0.0"), N(x.V, "0.0"), x.TopBars, x.BottomBars, x.Stirrups, OkText(x.Ok) }),
                new[] { 1.2, 0.8, 1.8, 1.4, 1.6, 1.4, 1.8, 1.8, 1.6, 1.5 }, 9);
    }
}

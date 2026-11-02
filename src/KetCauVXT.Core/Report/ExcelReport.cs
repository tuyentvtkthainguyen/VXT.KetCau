using System.Globalization;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>Xuất bảng tính Excel tổng hợp: thông tin, tải trọng, nội lực, phản lực, kết quả thiết kế và thống kê thép.</summary>
public static class ExcelReport
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static XlCell OkCell(bool ok) => ok ? new XlCell("ĐẠT", XlStyle.Ok) : new XlCell("KHÔNG ĐẠT", XlStyle.Fail);
    private static double Mm(double m) => m * 1000.0;
    private static double Cm2(double m2) => m2 * 1e4;

    public static void Export(Project p, AnalysisResults r, DesignSummary s, string path)
    {
        if (p == null) throw new ArgumentNullException(nameof(p));
        s ??= new DesignSummary();
        var x = new XlsxWriter();
        SheetInfo(p, x.AddSheet("Thong tin"));
        SheetLoads(p, x.AddSheet("Tai trong"));
        if (r != null)
        {
            var nl = x.AddSheet("Noi luc");
            ForceTables.AddFrameForces(r, nl);
            var pl = x.AddSheet("Phan luc");
            ForceTables.AddReactions(r, pl);
            var cv = x.AddSheet("Chuyen vi");
            ForceTables.AddDisplacements(r, cv);
            if (r.LateralForces.Count > 0) ForceTables.AddLateral(r, x.AddSheet("Luc ngang tang"));
        }
        SheetBeams(p, s, x.AddSheet("Dam"));
        SheetColumns(s, x.AddSheet("Cot"));
        SheetSlabs(s, x.AddSheet("San"));
        SheetFootings(s, x.AddSheet("Mong"));
        if (s.PileCaps.Count > 0) SheetPileCaps(s, x.AddSheet("Dai coc"));
        if (s.StripFootings.Count > 0) SheetStrips(s, x.AddSheet("Mong bang"));
        SheetSteel(s, x.AddSheet("Thep"));
        SheetRebarSchedule(s, x.AddSheet("Thong ke thep"));
        // gia công thép (shop drawing) – Report/RebarShopReport.cs; lỗi ở bước này không làm hỏng cả bảng tính
        if (s.All.Any(m => m.Bars.Count > 0))
        {
            var gc = x.AddSheet("Gia cong thep");
            try { RebarShopReport.AddSheet(p, KetCauVXT.Drawing.RebarShop.Build(p, s), gc); }
            catch (Exception ex) { gc.AddRow("LỖI lập bảng gia công thép: " + ex.Message); }
        }
        if (s.Warnings.Count > 0 || (r != null && r.Warnings.Count > 0))
        {
            var w = x.AddSheet("Canh bao");
            w.AddRow("Nguồn", "Nội dung");
            if (r != null) foreach (var m in r.Warnings) w.AddRow("Phân tích", m);
            foreach (var m in s.Warnings) w.AddRow("Thiết kế", m);
            w.SetColumnWidths(14, 120);
        }
        AppendixReport.AddSheet(p, x.AddSheet("Phu luc"));   // Report/AppendixReport.cs
        x.Save(path);
    }

    // ------------------------------------------------------------------
    private static void SheetInfo(Project p, XlsxSheet sh)
    {
        sh.FirstRowIsHeader = false; sh.FreezeRows = 0;
        sh.AddRow(new XlCell("THÔNG TIN CÔNG TRÌNH", XlStyle.Title));
        sh.AddRow("Tên công trình", p.Name);
        sh.AddRow("Địa điểm", p.Location);
        sh.AddRow("Chủ đầu tư", p.Owner);
        sh.AddRow("Người thiết kế", p.Designer);
        sh.AddRow("Phiên bản", p.Version);
        sh.AddRow("Số nút / số thanh / số ô sàn", $"{p.Nodes.Count} / {p.Frames.Count} / {p.Slabs.Count}");
        sh.AddRow("Cao độ chân cột (m)", p.BaseElevation);
        sh.AddRow("Sàn tuyệt đối cứng", p.UseRigidDiaphragm ? "Có" : "Không");
        sh.AddEmptyRow();

        sh.AddBoldRow("TẦNG");
        sh.AddHeaderRow("Tên tầng", "Cao độ (m)", "Chiều cao (m)");
        foreach (var st in p.Stories.OrderBy(t => t.Elevation)) sh.AddRow(st.Name, st.Elevation, st.Height);
        sh.AddEmptyRow();

        sh.AddBoldRow("VẬT LIỆU");
        sh.AddHeaderRow("Tên", "Loại", "γ (kN/m³)", "E (MPa)", "Rb (MPa)", "Rbt (MPa)", "Rs (MPa)", "Rsc (MPa)", "Rsw (MPa)", "fy (MPa)", "f (MPa)", "fu (MPa)");
        foreach (var m in p.Materials)
        {
            string kind = m.Kind switch { MaterialKind.Concrete => "Bê tông", MaterialKind.Rebar => "Cốt thép", _ => "Thép hình" };
            sh.AddRow(m.Name, kind, m.UnitWeight, U.ToMPa(m.E), U.ToMPa(m.Rb), U.ToMPa(m.Rbt), U.ToMPa(m.Rs), U.ToMPa(m.Rsc),
                U.ToMPa(m.Rsw), U.ToMPa(m.Fy), U.ToMPa(m.F), U.ToMPa(m.Fu));
        }
        sh.AddEmptyRow();

        sh.AddBoldRow("TIẾT DIỆN THANH");
        sh.AddHeaderRow("Tên", "Dạng", "Vật liệu", "b / bf (mm)", "h (mm)", "h cuối (mm)", "tf (mm)", "tw (mm)", "A (cm²)", "I33 (cm⁴)", "I22 (cm⁴)", "W33 (cm³)");
        foreach (var sc in p.FrameSections)
        {
            var sp = sc.Props(0);
            string shape = sc.Shape switch { SectionShape.RectConcrete => "Chữ nhật BTCT", SectionShape.WeldedI => sc.IsTapered ? "I tổ hợp vát" : "I tổ hợp", _ => "Tổng quát" };
            double b = sc.Shape switch { SectionShape.RectConcrete => sc.B, SectionShape.WeldedI => Math.Max(sc.BfTop, sc.BfBot), _ => sc.GWidth };
            double h = sc.Shape switch { SectionShape.RectConcrete => sc.H, SectionShape.WeldedI => sc.HStart, _ => sc.GDepth };
            double h2 = sc.Shape == SectionShape.WeldedI ? sc.HEnd : h;
            double tf = sc.Shape == SectionShape.WeldedI ? Math.Max(sc.TfTop, sc.TfBot) : 0;
            double tw = sc.Shape == SectionShape.WeldedI ? sc.Tw : 0;
            sh.AddRow(sc.Name, shape, sc.Material, Mm(b), Mm(h), Mm(h2), Mm(tf), Mm(tw), sp.A * 1e4, sp.I33 * 1e8, sp.I22 * 1e8, sp.W33 * 1e6);
        }
        sh.AddEmptyRow();
        sh.AddBoldRow("TIẾT DIỆN SÀN");
        sh.AddHeaderRow("Tên", "Vật liệu", "Chiều dày (mm)");
        foreach (var ss in p.SlabSections) sh.AddRow(ss.Name, ss.Material, Mm(ss.Thickness));
        sh.SetColumnWidths(28, 22, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12);
    }

    private static void SheetLoads(Project p, XlsxSheet sh)
    {
        sh.FirstRowIsHeader = false; sh.FreezeRows = 0;
        sh.AddRow(new XlCell("TẢI TRỌNG VÀ TỔ HỢP", XlStyle.Title));
        sh.AddBoldRow("TRƯỜNG HỢP TẢI");
        sh.AddHeaderRow("Tên", "Loại", "Hệ số TLBT", "γf");
        foreach (var pt in p.Patterns) sh.AddRow(pt.Name, LoadTypeName(pt.Type), pt.SelfWeight, pt.Gamma);
        sh.AddEmptyRow();

        sh.AddBoldRow("TỔ HỢP TẢI TRỌNG");
        sh.AddHeaderRow("Tên", "Loại", "Thành phần");
        foreach (var c in p.Combinations)
        {
            string t = c.Type switch { ComboType.ULS => "TTGH I", ComboType.SLS => "TTGH II", _ => "Bao" };
            sh.AddRow(c.Name, t, c.Type == ComboType.Envelope ? "BAO(" + string.Join("; ", c.Items.Select(i => i.Case)) + ")" : ComboGenerator.Describe(c));
        }
        sh.AddEmptyRow();

        sh.AddBoldRow("TẢI TRÊN THANH");
        sh.AddHeaderRow("Thanh", "Tên", "Trường hợp", "Phương", "Giá trị (kN/m)", "Lực tập trung (kN)", "Nguồn", "Ghi chú");
        var fdict = p.Frames.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var fl in p.FrameLoads)
        {
            string label = fdict.TryGetValue(fl.Element, out var fe) ? fe.Label : "";
            string pts = string.Join("; ", fl.Points.Select(q => $"{q[0].ToString("0.###", Inv)}:{q[1].ToString("0.###", Inv)}"));
            string pls = string.Join("; ", fl.PointLoads.Select(q => $"{q[0].ToString("0.###", Inv)}:{q[1].ToString("0.###", Inv)}"));
            sh.AddRow(fl.Element, label, fl.Pattern, DirName(fl.Dir), pts, pls, fl.Source == "SLAB" ? "Dồn từ sàn" : (fl.Source == "" ? "Nhập" : fl.Source), fl.Note);
        }
        sh.AddEmptyRow();

        sh.AddBoldRow("TẢI TRÊN SÀN");
        sh.AddHeaderRow("Ô sàn", "Tên", "Trường hợp", "q (kN/m²)", "Ghi chú");
        var sdict = p.Slabs.GroupBy(q => q.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var al in p.AreaLoads)
            sh.AddRow(al.Slab, sdict.TryGetValue(al.Slab, out var sl) ? sl.Label : "", al.Pattern, al.Q, al.Note);
        sh.AddEmptyRow();

        var nls = p.NodeLoads.Where(n => n.Source == "").ToList();
        if (nls.Count > 0)
        {
            sh.AddBoldRow("TẢI TẬP TRUNG TẠI NÚT (NGƯỜI DÙNG NHẬP)");
            sh.AddHeaderRow("Nút", "Trường hợp", "Fx", "Fy", "Fz", "Mx", "My", "Mz");
            foreach (var n in nls) sh.AddRow(n.Node, n.Pattern, n.Fx, n.Fy, n.Fz, n.Mx, n.My, n.Mz);
        }
        sh.SetColumnWidths(12, 16, 14, 14, 40, 22, 14, 30);
    }

    public static string LoadTypeName(LoadType t) => t switch
    {
        LoadType.Dead => "Tĩnh tải",
        LoadType.SuperDead => "Tĩnh tải bổ sung",
        LoadType.Live => "Hoạt tải",
        LoadType.RoofLive => "Hoạt tải mái",
        LoadType.Wind => "Gió",
        LoadType.Seismic => "Động đất",
        _ => "Khác"
    };

    public static string DirName(LoadDir d) => d switch
    {
        LoadDir.Gravity => "Trọng lực",
        LoadDir.GlobalX => "X tổng thể",
        LoadDir.GlobalY => "Y tổng thể",
        LoadDir.GlobalZ => "Z tổng thể",
        LoadDir.Local1 => "Trục 1",
        LoadDir.Local2 => "Trục 2",
        _ => "Trục 3"
    };

    // ------------------------------------------------------------------
    private static void SheetBeams(Project p, DesignSummary s, XlsxSheet sh)
    {
        sh.AddRow("Dầm", "Tầng", "b×h (mm)", "Nhịp", "Phần tử", "Gối trái", "Gối phải", "L (m)",
            "M gối trái (kN.m)", "M nhịp (kN.m)", "M gối phải (kN.m)", "Qmax (kN)",
            "As trên trái yc (cm²)", "As trên trái bt (cm²)", "Thép trên trái",
            "As dưới yc (cm²)", "As dưới bt (cm²)", "Thép dưới",
            "As trên phải yc (cm²)", "As trên phải bt (cm²)", "Thép trên phải",
            "Đai", "Bước gối (mm)", "Bước giữa (mm)", "Võng (mm)", "Võng giới hạn (mm)", "Tỷ số max", "Kết luận");
        foreach (var b in s.Beams)
        {
            int k = 1;
            foreach (var sp in b.Spans)
            {
                sh.AddRow(b.Name, b.Story, $"{Mm(b.B):0}×{Mm(b.H):0}", k++, sp.Frame, sp.LeftSupport, sp.RightSupport, sp.L,
                    sp.MLeft, sp.MSpan, sp.MRight, sp.QMax,
                    Cm2(sp.AsTopLeftReq), Cm2(sp.AsTopLeftProv), sp.TopLeftText,
                    Cm2(sp.AsBotReq), Cm2(sp.AsBotProv), sp.BotText,
                    Cm2(sp.AsTopRightReq), Cm2(sp.AsTopRightProv), sp.TopRightText,
                    sp.StirrupDia > 0 ? $"Ø{sp.StirrupDia}" : "", Mm(sp.StirrupSupport), Mm(sp.StirrupMid),
                    Mm(sp.Deflection), Mm(sp.DeflectionLimit), b.MaxRatio, OkCell(b.Ok));
            }
        }
        sh.SetColumnWidths(10, 8, 10, 6, 8, 9, 9, 7, 11, 11, 11, 10, 10, 10, 14, 10, 10, 14, 10, 10, 14, 6, 9, 9, 9, 10, 9, 12);
        AddMessages(sh, s.Beams);
    }

    private static void SheetColumns(DesignSummary s, XlsxSheet sh)
    {
        sh.AddRow("Cột", "Trục", "Tầng", "Phần tử", "b×h (mm)", "Chiều cao (m)", "N (kN)", "M2 (kN.m)", "M3 (kN.m)", "η2", "η3", "Tổ hợp",
            "As yc (cm²)", "As bt (cm²)", "Bố trí", "μ (%)", "Đai", "Bước đai (mm)", "Bước đai đầu cột (mm)", "Hệ số sử dụng", "Kết luận");
        foreach (var c in s.Columns)
            foreach (var st in c.Stories)
                sh.AddRow(c.Name, c.GridName, st.Story, st.Frame, $"{Mm(st.B):0}×{Mm(st.H):0}", st.Length, st.N, st.M2, st.M3, st.Eta2, st.Eta3, st.Combo,
                    Cm2(st.AsReq), Cm2(st.AsProv), st.Dia > 0 ? $"{st.TotalBars}Ø{st.Dia}" : "", st.Ratio,
                    st.StirrupDia > 0 ? $"Ø{st.StirrupDia}" : "", Mm(st.StirrupSpacing), Mm(st.StirrupSpacingEnd), st.Utilization, OkCell(c.Ok));
        sh.SetColumnWidths(9, 8, 8, 8, 10, 9, 10, 10, 10, 7, 7, 9, 10, 10, 10, 8, 6, 9, 11, 10, 12);
        AddMessages(sh, s.Columns);
    }

    private static void SheetSlabs(DesignSummary s, XlsxSheet sh)
    {
        sh.AddRow("Ô sàn", "Tầng", "Lx (m)", "Ly (m)", "Dày (mm)", "Loại", "Liên kết biên", "q (kN/m²)",
            "M1 (kN.m/m)", "M2 (kN.m/m)", "MI (kN.m/m)", "MII (kN.m/m)", "Dưới phương ngắn", "Dưới phương dài", "Mũ gối ngắn", "Mũ gối dài",
            "Võng (mm)", "Tỷ số max", "Kết luận");
        foreach (var sl in s.Slabs)
            sh.AddRow(sl.Name, sl.Story, sl.Lx, sl.Ly, Mm(sl.Thickness), sl.TwoWay ? "Bản kê 4 cạnh" : "Bản dầm", sl.EdgeCondition, sl.Q,
                sl.M1, sl.M2, sl.MI, sl.MII, sl.BotX, sl.BotY, sl.TopX, sl.TopY, Mm(sl.Deflection), sl.MaxRatio, OkCell(sl.Ok));
        sh.SetColumnWidths(10, 8, 7, 7, 8, 14, 16, 9, 10, 10, 10, 10, 12, 12, 12, 12, 9, 9, 12);
        AddMessages(sh, s.Slabs);
    }

    private static void SheetFootings(DesignSummary s, XlsxSheet sh)
    {
        sh.AddRow("Móng", "Nút", "X (m)", "Y (m)", "Cột b×h (mm)", "N (kN)", "Mx (kN.m)", "My (kN.m)", "Qx (kN)", "Qy (kN)", "Ntc (kN)", "Tổ hợp",
            "L (m)", "B (m)", "H (m)", "Hmép (m)", "Df (m)", "R (kN/m²)", "Pmax (kN/m²)", "Pmin (kN/m²)", "Ptb (kN/m²)",
            "As L (cm²)", "Thép L", "As B (cm²)", "Thép B", "Số lượng", "Tỷ số max", "Kết luận");
        foreach (var f in s.Footings)
            sh.AddRow(f.Name, f.NodeId, f.X, f.Y, $"{Mm(f.ColumnB):0}×{Mm(f.ColumnH):0}", f.N, f.Mx, f.My, f.Qx, f.Qy, f.Ntc, f.Combo,
                f.L, f.B, f.H, f.Hb, f.Depth, f.R, f.Pmax, f.Pmin, f.Ptb, Cm2(f.AsL), f.BarsL, Cm2(f.AsB), f.BarsB, f.Quantity, f.MaxRatio, OkCell(f.Ok));
        sh.SetColumnWidths(8, 6, 7, 7, 10, 9, 9, 9, 8, 8, 9, 8, 6, 6, 6, 7, 6, 9, 9, 9, 9, 9, 10, 9, 10, 7, 8, 12);
        AddMessages(sh, s.Footings);
    }

    private static void SheetPileCaps(DesignSummary s, XlsxSheet sh)
    {
        sh.AddRow("Đài", "Số lượng", "Nút", "X (m)", "Y (m)", "Cột b×h (mm)", "Số cọc", "Cọc (mm)", "[P] (kN)", "N (kN)", "Mx (kN.m)", "My (kN.m)",
            "Qx (kN)", "Qy (kN)", "G (kN)", "Tổ hợp", "L (m)", "B (m)", "H (m)", "h0 (m)", "s cọc (mm)", "Pmax (kN)", "Pmin (kN)",
            "ML (kN.m)", "As L (cm²)", "Thép L", "MB (kN.m)", "As B (cm²)", "Thép B", "Lớp trên", "Tỷ số max", "Kết luận");
        foreach (var c in s.PileCaps)
            sh.AddRow(c.Name, c.Quantity, string.Join(",", c.NodeIds), c.X, c.Y, $"{Mm(c.ColumnB):0}×{Mm(c.ColumnH):0}", c.Piles.Count,
                (c.PileRound ? "D" : "") + Mm(c.PileSize).ToString("0", Inv), c.PileCapacity, c.N, c.Mx, c.My, c.Qx, c.Qy, c.Weight, c.Combo,
                c.L, c.B, c.H, c.H0, Mm(c.Spacing), c.PMax, c.PMin, c.MomentL, Cm2(c.AsL), c.BarsL, c.MomentB, Cm2(c.AsB), c.BarsB, c.BarsTop,
                c.MaxRatio, OkCell(c.Ok));
        sh.SetColumnWidths(7, 7, 12, 7, 7, 10, 7, 8, 8, 9, 9, 9, 8, 8, 8, 8, 6, 6, 6, 7, 8, 9, 9, 9, 9, 10, 9, 9, 10, 10, 8, 12);
        // phản lực từng cọc
        sh.AddEmptyRow();
        sh.AddBoldRow("PHẢN LỰC CỌC – TỔ HỢP BẤT LỢI");
        sh.AddHeaderRow("Đài", "Cọc", "x (m)", "y (m)", "P (kN)", "Tổ hợp");
        foreach (var c in s.PileCaps)
            for (int i = 0; i < c.Piles.Count; i++)
                sh.AddRow(c.Name, i + 1, c.Piles[i].X, c.Piles[i].Y, i < c.Reactions.Count ? c.Reactions[i] : 0.0, c.Combo);
        AddChecks(sh, s.PileCaps);
        AddMessages(sh, s.PileCaps);
    }

    private static void SheetStrips(DesignSummary s, XlsxSheet sh)
    {
        sh.AddRow("Dải", "Trục", "Phương", "Số cột", "Từ (m)", "Đến (m)", "Dài (m)", "B (m)", "b sườn (mm)", "h sườn (mm)", "Bản mép (mm)", "Bản gốc (mm)",
            "Df (m)", "R (kN/m²)", "k (kN/m³)", "ptb (kN/m²)", "pmax (kN/m²)", "Tổ hợp TC", "M+ (kN.m)", "M− (kN.m)", "Q (kN)", "Tổ hợp TT",
            "As dưới (cm²)", "Thép dưới", "As trên (cm²)", "Thép trên", "Đai", "As bản (cm²/m)", "Thép bản", "Tỷ số max", "Kết luận");
        foreach (var m in s.StripFootings)
            sh.AddRow(m.Name, m.Grid, m.AlongX ? "X" : "Y", m.NodeIds.Count, m.Start, m.End, m.Length, m.Width, Mm(m.RibB), Mm(m.RibH), Mm(m.FlangeEdge), Mm(m.FlangeRoot),
                m.Depth, m.R, m.K, m.PAvg, m.PMax, m.ComboService, m.MPos, m.MNeg, m.VDesign, m.Combo,
                Cm2(m.AsBottom), m.BottomBars, Cm2(m.AsTop), m.TopBars, m.Stirrups, Cm2(m.AsFlange), m.FlangeBars, m.MaxRatio, OkCell(m.Ok));
        sh.SetColumnWidths(7, 9, 7, 7, 7, 7, 7, 6, 9, 9, 9, 9, 6, 9, 9, 9, 9, 9, 9, 9, 8, 9, 10, 10, 10, 10, 9, 10, 10, 8, 12);
        foreach (var m in s.StripFootings)
        {
            sh.AddEmptyRow();
            sh.AddBoldRow("BIỂU ĐỒ DỌC DẢI " + m.Name + " (" + m.Grid + ")");
            sh.AddHeaderRow("x (m)", "M (kN.m)", "Q (kN)", "Mmax bao", "Mmin bao", "|Q|max bao", "p tiêu chuẩn (kN/m²)");
            for (int i = 0; i < m.Stations.Count; i++)
                sh.AddRow(m.Stations[i] - m.Start, At(m.M, i), At(m.V, i), At(m.MMax, i), At(m.MMin, i), At(m.VMax, i), At(m.Pressure, i));
        }
        AddChecks(sh, s.StripFootings);
        AddMessages(sh, s.StripFootings);
    }

    private static double At(List<double> l, int i) => l != null && i >= 0 && i < l.Count ? l[i] : 0.0;

    private static void AddChecks<T>(XlsxSheet sh, IEnumerable<T> members) where T : MemberDesignBase
    {
        sh.AddEmptyRow();
        sh.AddBoldRow("KIỂM TRA");
        sh.AddHeaderRow("Cấu kiện", "Hạng mục", "Giá trị", "Giới hạn", "Đơn vị", "Tỷ số", "Đánh giá", "Ghi chú");
        foreach (var m in members)
            foreach (var c in m.Checks)
                sh.AddRow(m.Name, c.Name, c.Demand, c.Capacity, c.Unit, c.Ratio, OkCell(c.Ok), c.Note);
    }

    private static void SheetSteel(DesignSummary s, XlsxSheet sh)
    {
        sh.AddRow("Cấu kiện", "Phần tử", "Tiết diện", "Tổ hợp", "Vị trí (m)", "N nén (kN)", "M3 (kN.m)", "V2 (kN)", "λmax",
            "Hạng mục kiểm tra", "Giá trị", "Giới hạn", "Đơn vị", "Tỷ số", "Đánh giá", "Ghi chú");
        foreach (var m in s.Steel)
        {
            bool first = true;
            foreach (var c in m.Checks)
            {
                if (first)
                    sh.AddRow(m.Name, m.Frame, m.Section, m.Combo, m.Station, m.N, m.M, m.V, m.Lambda,
                        c.Name, c.Demand, c.Capacity, c.Unit, c.Ratio, OkCell(c.Ok), c.Note);
                else
                    sh.AddRow(null, null, null, null, null, null, null, null, null,
                        c.Name, c.Demand, c.Capacity, c.Unit, c.Ratio, OkCell(c.Ok), c.Note);
                first = false;
            }
            sh.AddRow(new XlCell(m.Name, XlStyle.Bold), null, null, null, null, null, null, null, null,
                new XlCell("KẾT LUẬN", XlStyle.Bold), null, null, null, m.MaxRatio, OkCell(m.Ok), "");
        }
        sh.SetColumnWidths(10, 8, 14, 9, 9, 10, 10, 9, 8, 36, 10, 10, 8, 8, 12, 60);
        AddMessages(sh, s.Steel);
    }

    private static void AddMessages<T>(XlsxSheet sh, IEnumerable<T> members) where T : MemberDesignBase
    {
        var list = members.Where(m => m.Messages.Count > 0).ToList();
        if (list.Count == 0) return;
        sh.AddEmptyRow();
        sh.AddBoldRow("GHI CHÚ / THÔNG BÁO");
        foreach (var m in list)
            foreach (var msg in m.Messages)
                sh.AddRow(m.Name, msg);
    }

    // ------------------------------------------------------------------
    // THỐNG KÊ CỐT THÉP
    // ------------------------------------------------------------------
    public class RebarRow
    {
        public string Member = "";
        public string Type = "";
        public int Mark;
        public string Role = "";
        public string Shape = "";
        public int Dia;
        public double LengthMm;
        public int Members;
        public int PerMember;
        public int Total => Members * PerMember;
        public double TotalLengthM => Total * LengthMm / 1000.0;
        public double WeightKg => 0.00617 * Dia * Dia * TotalLengthM;
        /// <summary>Tên cấu kiện kèm số lượng "D2-1 (SL=2)".</summary>
        public string MemberLabel => KetCauVXT.Drawing.RebarSchedule.MemberLabel(Member, Members);
    }

    /// <summary>Lập các dòng thống kê thép từ BarMark của mọi cấu kiện. KL = 0.00617·d²·L (kg, d mm, L m).</summary>
    public static List<RebarRow> RebarSchedule(DesignSummary s)
    {
        var rows = new List<RebarRow>();
        void Add(MemberDesignBase m, string type)
        {
            // gộp các BarMark cùng số hiệu (thanh đối xứng) thành một dòng – giống bảng thống kê DXF
            foreach (var g in m.Bars.Where(x => x.Dia > 0 && x.Count > 0 && x.Length > 0).Select((x, i) => (Bar: x, Key: x.Mark > 0 ? x.Mark : -1 - i))
                         .GroupBy(t => t.Key).OrderBy(t => t.Key))
            {
                var b = g.First().Bar;
                rows.Add(new RebarRow
                {
                    Member = m.Name, Type = type, Mark = b.Mark, Role = b.Role, Shape = b.Shape, Dia = b.Dia,
                    LengthMm = Math.Round(b.Length * 100) * 10, Members = Math.Max(1, m.Quantity), PerMember = g.Sum(t => t.Bar.Count)
                });
            }
        }
        foreach (var m in s.Beams) Add(m, "Dầm");
        foreach (var m in s.Columns) Add(m, "Cột");
        foreach (var m in s.Slabs) Add(m, "Sàn");
        foreach (var m in s.Footings) Add(m, "Móng");
        foreach (var m in s.PileCaps) Add(m, "Đài cọc");
        foreach (var m in s.StripFootings) Add(m, "Móng băng");
        foreach (var m in s.Steel) Add(m, "Thép");
        return rows;
    }

    private static void SheetRebarSchedule(DesignSummary s, XlsxSheet sh)
    {
        var rows = RebarSchedule(s);
        sh.AddRow("Cấu kiện", "Loại", "Số hiệu", "Vai trò", "Hình dạng", "Ø (mm)", "Chiều dài 1 thanh (mm)", "Số cấu kiện",
            "Số thanh / 1 CK", "Tổng số thanh", "Tổng chiều dài (m)", "Khối lượng (kg)");
        foreach (var rw in rows)
            sh.AddRow(rw.MemberLabel, rw.Type, rw.Mark, rw.Role, rw.Shape, rw.Dia, rw.LengthMm, rw.Members, rw.PerMember, rw.Total, rw.TotalLengthM, rw.WeightKg);
        sh.SetColumnWidths(12, 8, 8, 16, 10, 8, 14, 10, 12, 12, 14, 14);
        if (rows.Count == 0) { sh.AddRow("Chưa có dữ liệu bố trí thép (chạy thiết kế trước)."); return; }

        sh.AddEmptyRow();
        sh.AddBoldRow("TỔNG HỢP THEO ĐƯỜNG KÍNH");
        sh.AddHeaderRow("Ø (mm)", "Tổng chiều dài (m)", "Khối lượng (kg)", "Khối lượng (tấn)");
        foreach (var g in rows.GroupBy(q => q.Dia).OrderBy(z => z.Key))
        {
            double L = g.Sum(q => q.TotalLengthM), W = g.Sum(q => q.WeightKg);
            sh.AddRow(g.Key, L, W, W / 1000.0);
        }
        sh.AddEmptyRow();
        sh.AddBoldRow("TỔNG HỢP THEO NHÓM ĐƯỜNG KÍNH");
        sh.AddHeaderRow("Nhóm", "Khối lượng (kg)", "Khối lượng (tấn)");
        double w1 = rows.Where(q => q.Dia <= 10).Sum(q => q.WeightKg);
        double w2 = rows.Where(q => q.Dia > 10 && q.Dia <= 18).Sum(q => q.WeightKg);
        double w3 = rows.Where(q => q.Dia > 18).Sum(q => q.WeightKg);
        sh.AddRow("Ø ≤ 10", w1, w1 / 1000);
        sh.AddRow("10 < Ø ≤ 18", w2, w2 / 1000);
        sh.AddRow("Ø > 18", w3, w3 / 1000);
        sh.AddBoldRow("TỔNG CỘNG", w1 + w2 + w3, (w1 + w2 + w3) / 1000);
        sh.AddEmptyRow();
        sh.AddBoldRow("TỔNG HỢP THEO LOẠI CẤU KIỆN");
        sh.AddHeaderRow("Loại", "Khối lượng (kg)");
        foreach (var g in rows.GroupBy(q => q.Type)) sh.AddRow(g.Key, g.Sum(q => q.WeightKg));
    }
}

using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Detailing;

// =====================================================================
// CẦU THANG BỘ – chế độ Triển khai hồ sơ: dùng lại bộ thiết kế / bản vẽ / khối lượng cầu thang của chế độ mô hình
// (Model/Stair.cs, Design/Concrete/StairDesigner.cs, Drawing/StairDrawing.cs, Report/StairQuantity.cs) trên một
// dự án mini: 1 ô thang (lưới 1 × 1 nhịp = kích thước ô thang tim trục), 2 tầng cao H, lỗ cầu thang ở tầng trên.
// Bê tông chỉ gồm bản thang, chiếu nghỉ, dầm chiếu nghỉ, dầm cốn (nếu có); bậc xây gạch (mặc định – thông dụng ở Việt Nam), ốp đá / gỗ tính công tác riêng.
// Các dạng đặc biệt (zíc zắc, xương cá, thép hình) và bậc đổ BTCT liền khối: StairForms.cs; mặt bằng bố trí thép bản thang: StairPlanRebar.cs.
// =====================================================================
public class ManualStair : IDetailMember
{
    [Field("Tên cầu thang", Group = "Chung")] public string Name { get; set; } = "CT1";
    [Field("Số lượng (tầng giống nhau)", Group = "Chung")] public int Quantity { get; set; } = 1;
    [Field("Dạng mặt bằng thang", Group = "Hình học", Choices = "2 vế chữ U|1 vế thẳng|3 vế chữ U")] public string Type { get; set; } = "2 vế chữ U";
    [Field("Chiều cao tầng", Unit = "m", Group = "Hình học")] public double FloorHeight { get; set; } = 3.3;
    [Field("Ô thang – bề rộng (tim trục, ngang vế)", Unit = "m", Group = "Hình học")] public double WellWidth { get; set; } = 2.4;
    [Field("Ô thang – chiều dài (tim trục, dọc vế)", Unit = "m", Group = "Hình học")] public double WellLength { get; set; } = 4.5;
    [Field("Số cổ bậc (0 = tự chọn)", Group = "Hình học")] public int Risers { get; set; } = 20;
    [Field("Bề rộng mặt bậc b", Unit = "mm", Scale = 1000, Group = "Hình học")] public double Tread { get; set; } = 0.27;
    [Field("Khe giữa 2 vế", Unit = "mm", Scale = 1000, Group = "Hình học")] public double Gap { get; set; } = 0.10;
    [Field("Chiều dày bản thang (0 = tự chọn)", Unit = "mm", Scale = 1000, Group = "Hình học")] public double Waist { get; set; } = 0.12;
    [Field("Chiều sâu chiếu nghỉ (0 = tự chọn)", Unit = "m", Group = "Hình học")] public double Landing { get; set; }

    [Field("Sơ đồ chịu lực", Group = "Kết cấu", Choices = "Bản thang tựa dầm chiếu nghỉ|Có dầm cốn 1 bên|Có dầm cốn 2 bên")] public string Support { get; set; } = "Bản thang tựa dầm chiếu nghỉ";
    [Field("Dầm cốn b", Unit = "mm", Scale = 1000, Group = "Kết cấu")] public double StringerB { get; set; } = 0.15;
    [Field("Dầm cốn h", Unit = "mm", Scale = 1000, Group = "Kết cấu")] public double StringerH { get; set; } = 0.30;
    [Field("Có dầm chiếu nghỉ", Group = "Kết cấu")] public bool LandingBeam { get; set; } = true;
    [Field("Dầm chiếu nghỉ b", Unit = "mm", Scale = 1000, Group = "Kết cấu")] public double LandingBeamB { get; set; } = 0.22;
    [Field("Dầm chiếu nghỉ h", Unit = "mm", Scale = 1000, Group = "Kết cấu")] public double LandingBeamH { get; set; } = 0.30;

    [Field("Bậc thang", Group = "Hoàn thiện, tải", Choices = "Xây gạch|Đổ BTCT liền khối")] public string Steps { get; set; } = "Xây gạch";
    [Field("Ốp mặt bậc", Group = "Hoàn thiện, tải", Choices = "Đá granite|Gỗ|Gạch ceramic|Không ốp")] public string Finish { get; set; } = "Đá granite";
    [Field("Lan can", Group = "Hoàn thiện, tải", Choices = "Lan can thép / inox|Lan can kính|Không")] public string Railing { get; set; } = "Lan can thép / inox";
    [Field("Hoạt tải (tiêu chuẩn)", Unit = "kN/m²", Group = "Hoàn thiện, tải")] public double Live { get; set; } = 3.0;

    // ---- dạng kết cấu thang ----
    [Field("Kết cấu thang", Group = "Kết cấu thang", Choices = StairForm.Choices,
        Hint = "Bản thang thường: chỉ thiết kế bản thang + chiếu nghỉ, bậc xây gạch (mặc định) hoặc đổ BTCT liền khối")]
    public string Form { get; set; } = StairForm.Flat;
    [Field("Thép góc mũi bậc BTCT Ø", Unit = "mm", Group = "Kết cấu thang", Hint = "6 hoặc 8 – chỉ khi bậc đổ BTCT liền khối")] public int StepBarDia { get; set; } = 8;
    [Field("Xương cá – dầm cốn giữa b", Unit = "mm", Scale = 1000, Group = "Kết cấu thang")] public double SpineB { get; set; } = 0.30;
    [Field("Xương cá – dầm cốn giữa h", Unit = "mm", Scale = 1000, Group = "Kết cấu thang")] public double SpineH { get; set; } = 0.40;
    [Field("Xương cá – bậc công xôn", Group = "Kết cấu thang", Choices = "2 bên (đối xứng)|1 bên")] public string CantileverSides { get; set; } = "2 bên (đối xứng)";
    [Field("Xương cá – chiều dày bậc (0 = chiều cao bậc)", Unit = "mm", Scale = 1000, Group = "Kết cấu thang")] public double StepDepth { get; set; }
    [Field("Thang thép – tiết diện cốn", Group = "Kết cấu thang", Choices = SteelTables.SectionChoices)] public string SteelSection { get; set; } = "U200";
    [Field("Thang thép – mác thép", Group = "Kết cấu thang", Choices = SteelTables.GradeChoices)] public string SteelGrade { get; set; } = "CCT34";
    [Field("Thang thép – mặt bậc", Group = "Kết cấu thang", Choices = "Thép tấm gân 5 mm|Gỗ dày 40 mm|Ốp đá trên thép tấm")] public string SteelTread { get; set; } = "Thép tấm gân 5 mm";
    [Field("Thang thép – thép góc đỡ bậc", Group = "Kết cấu thang", Choices = SteelTables.AngleChoices)] public string TreadAngle { get; set; } = "L50×5";
    [Field("Thang thép – hệ số điều kiện làm việc γc", Group = "Kết cấu thang")] public double SteelGammaC { get; set; } = 0.95;

    public static ManualStair Sample() => new();

    /// <summary>Dự án mini và cầu thang tương ứng (dùng chung cho Run và Draw).</summary>
    public (Project P, Stair S, string Error) BuildModel(DetailSettings s)
    {
        s ??= new DetailSettings();
        if (!(WellWidth > 0.8) || !(WellLength > 1.5) || !(FloorHeight > 1.5) || FloorHeight > 8)
            return (null, null, "LỖI kích thước ô thang / chiều cao tầng không hợp lệ (bề rộng > 0,8 m, dài > 1,5 m, H 1,5…8 m)");
        var p = ModelBuilder.NewProject("Cầu thang " + Name);
        ModelBuilder.SetGrid(p, new[] { WellWidth }, new[] { WellLength });
        ModelBuilder.SetStories(p, new[] { FloorHeight, FloorHeight }, 0.0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x22", BeamSectionX = "D22x30", BeamSectionY = "D22x30", SlabSection = "S100" });
        var cs = p.Concrete;
        cs.ConcreteSlab = cs.ConcreteBeam = s.Concrete;
        cs.RebarMain = s.RebarMain; cs.RebarStirrup = s.RebarSmall;
        cs.CoverSlab = s.CoverSlab; cs.CoverBeam = s.CoverBeam;
        foreach (var m in new[] { s.Concrete, s.RebarMain, s.RebarSmall })
            if (p.Materials.All(x => x.Name != m))
                try { p.Materials.Add(m == s.Concrete ? Material.Concrete(m) : Material.Rebar(m)); } catch (ArgumentException) { }
        var stories = p.Stories.OrderBy(x => x.Elevation).ToList();
        string top = stories.Count >= 2 ? stories[1].Name : stories[^1].Name;
        var er = ModelEdit.AddOpening(p, top, WellWidth / 2, WellLength / 2, OpeningKind.Stair);
        var op = p.Openings.FirstOrDefault(o => o.Kind == OpeningKind.Stair);
        if (!er.Ok || op == null) return (null, null, "LỖI không tạo được ô thang: " + er.Message);
        var st = StairEdit.Draft(p, op);
        st.Name = Name;
        st.Type = Type.StartsWith("1") ? StairType.Straight : Type.StartsWith("3") ? StairType.ThreeFlightU : StairType.TwoFlightU;
        if (Risers > 0) st.Risers = Risers;
        if (Tread > 0) st.Tread = Tread;
        if (Gap >= 0) st.Gap = Gap;
        if (Waist > 0) st.WaistThickness = Waist;
        if (Landing > 0) st.LandingDepth = Landing;
        st.Support = Support.Contains("cốn") ? StairSupport.Stringer : StairSupport.Slab;
        st.StringerSides = Support.Contains("1 bên") ? 1 : 2;
        st.StringerB = StringerB; st.StringerH = StringerH;
        st.LandingBeam = LandingBeam; st.LandingBeamB = LandingBeamB; st.LandingBeamH = LandingBeamH;
        st.Steps = Steps.StartsWith("Đổ") ? StepConstruction.Concrete : StepConstruction.Brick;
        st.Finish = Finish.StartsWith("Gỗ") ? StepFinish.Wood : Finish.StartsWith("Gạch") ? StepFinish.Tile : Finish.StartsWith("Không") ? StepFinish.None : StepFinish.Granite;
        st.Railing = Railing.StartsWith("Không") ? RailingKind.None : Railing.Contains("kính") ? RailingKind.Glass : RailingKind.Steel;
        st.LiveLoad = Live > 0 ? Live : StairUnits.LiveDefault;
        var ar = StairEdit.Apply(p, st);
        if (!ar.Ok) return (null, null, "LỖI khai báo cầu thang: " + ar.Message);
        return (p, p.Stairs[0], null);
    }

    private Project _p;
    private StairDesign _sd;

    public DetailResult Run(DetailSettings s)
    {
        s ??= new DetailSettings();
        var r = new DetailResult { Kind = "Cầu thang bộ", Name = Name, Quantity = Math.Max(1, Quantity), ConcreteClass = s.Concrete };
        _p = null; _sd = null;
        try
        {
            var (p, st, err) = BuildModel(s);
            if (err != null) { r.Messages.Add(err); return r; }
            if (!StairForm.IsFlat(Form))
            {
                var fr = StairForms.Design(this, st.Layout(p), s);
                fr.Calc.Insert(0, "Kết cấu thang: " + Form + " (dạng mặt bằng " + Type + ").");
                return fr;
            }
            var sd = StairDesigner.DesignOne(p, st);
            // đánh số thép liên tục qua các bộ phận (vế, chiếu nghỉ, dầm) để bảng thống kê không trùng số hiệu
            int offset = 0;
            foreach (var part in sd.Parts)
            {
                int mx = 0;
                foreach (var b in part.Bars.Where(b => b.Mark > 0)) { b.Mark += offset; mx = Math.Max(mx, b.Mark); }
                if (mx > 0) offset = mx;
            }
            _p = p; _sd = sd;
            r.Source = sd;
            var L = st.Layout(p);
            r.Messages.AddRange(sd.Messages);
            r.Checks.AddRange(sd.Checks);
            var srcBars = new List<BarMark>();
            foreach (var part in sd.Parts)
            {
                r.Checks.AddRange(part.Checks);
                foreach (var m in part.Messages) r.Messages.Add(part.Name + ": " + m);
                int qn = Math.Max(1, part.Quantity);
                foreach (var b in part.Bars.Where(b => b.Count > 0 && b.Dia > 0))
                {
                    srcBars.Add(b);
                    r.Bars.Add(new BarMark
                    {
                        Mark = b.Mark, Role = part.Name + " – " + b.Role, Dia = b.Dia, Count = b.Count * qn, Length = b.Length, Shape = b.Shape,
                        Spacing = b.Spacing, IsTop = b.IsTop, Segments = b.Segments?.ToList() ?? new List<double>(), Start = b.Start, End = b.End
                    });
                }
            }
            if (st.Steps == StepConstruction.Concrete) StairForms.AddStepBars(r, L, s, StepBarDia);
            for (int i = 0; i < r.Bars.Count; i++) r.Bars[i].Mark = i + 1;   // đánh số liên tục qua các vế / chiếu nghỉ – mỗi số hiệu một thanh
            for (int i = 0; i < srcBars.Count; i++) srcBars[i].Mark = r.Bars[i].Mark;   // bản vẽ chi tiết (StairDrawing) dùng cùng số hiệu với bảng thống kê
            r.Calc.Add("## Cầu thang " + Name + " – " + sd.TypeText + ", " + sd.SupportText);
            r.Calc.Add("Chiều cao tầng H = " + Dt.F(sd.Height) + " m; " + sd.Risers + " cổ bậc h = " + Dt.Mm(sd.Riser) + " mm, mặt bậc b = " + Dt.Mm(sd.Tread)
                       + " mm; 2h + b = " + Dt.Mm(sd.Check2hb) + " mm (600…650); vế rộng " + Dt.Mm(sd.FlightWidth) + " mm, bản thang " + Dt.Mm(sd.WaistT) + " mm, cos α = " + Dt.F(sd.CosA, "0.000"));
            foreach (var part in sd.Parts)
                r.Calc.Add(part.Name + " (" + part.Kind + "): nhịp " + Dt.F(part.Span) + " m, q = " + Dt.F(part.Q, "0.00") + " kN/m, M = " + Dt.F(part.M, "0.00")
                           + (string.IsNullOrEmpty(part.MomentFormula) ? "" : " (" + part.MomentFormula + ")") + " → As = " + Dt.F(part.AsReq * 1e4, "0.00") + " cm², chọn "
                           + part.MainText + (string.IsNullOrEmpty(part.DistText) ? "" : ", phân bố " + part.DistText) + (string.IsNullOrEmpty(part.StirrupText) ? "" : ", đai " + part.StirrupText));
            r.Calc.Add("Bê tông chỉ tính bản thang, chiếu nghỉ, dầm chiếu nghỉ" + (st.Support == StairSupport.Stringer ? ", dầm cốn" : "") + "; bậc "
                       + Stair.StepsName(st.Steps).ToLowerInvariant() + ", ốp " + Stair.FinishName(st.Finish).ToLowerInvariant() + " tính công tác riêng.");
            if (!sd.Check2hb.Equals(double.NaN) && (sd.Check2hb < 0.58 || sd.Check2hb > 0.67))
                r.Messages.Add("Cảnh báo bậc thang: 2h + b = " + Dt.Mm(sd.Check2hb) + " mm ngoài khoảng 600…650 mm (đi lại không thoải mái)");
            // khối lượng: chỉ hạng mục cầu thang
            var ds = new DesignSummary(); ds.Stairs.Add(sd);
            var q = QuantityCalculator.Compute(p, ds);
            r.ConcreteVolume = q.Volume(QtyCat.Stair);
            r.FormworkArea = q.Formwork(QtyCat.Stair);
            foreach (var g in q.StairWorks.Where(x => x.Quantity > 0).GroupBy(x => (x.Work, x.Unit)))
                r.Extra.Add((g.Key.Work, g.Key.Unit, g.Sum(x => x.Quantity)));
            foreach (var k in r.Checks.Where(x => !x.Ok)) if (!r.Messages.Any(m => m.Contains(k.Name))) r.Messages.Add("LỖI " + k.Name + ": " + Dt.F(k.Demand, "0.00") + " > " + Dt.F(k.Capacity, "0.00") + " " + k.Unit);
        }
        catch (Exception ex) { r.Messages.Add("LỖI tính cầu thang: " + ex.Message); }
        return r;
    }

    /// <summary>Bản vẽ chi tiết cầu thang của chế độ mô hình: mặt bằng thang, mặt cắt các vế, chi tiết thép bản thang, chiếu nghỉ, dầm chiếu nghỉ / dầm cốn.</summary>
    public List<Figure> Draw(DetailResult r, DetailSettings s)
    {
        if (r is StairFormResult fr) return StairForms.Draw(fr, s ?? new DetailSettings());
        if (r?.Source is not StairDesign sd) return new List<Figure>();
        var p = ReferenceEquals(sd, _sd) ? _p : BuildModel(s ?? new DetailSettings()).P;
        if (p == null) return new List<Figure>();
        var ds = new DesignSummary(); ds.Stairs.Add(sd);
        var figs = StairDrawing.Build(p, ds, sd, StairDrawing.CurrentScale);
        var st = sd.Stair ?? p.Stairs.FirstOrDefault();
        if (st == null) return figs;
        var L = StairLayout.Build(p, st);
        // mặt bằng bố trí thép bản thang + mặt cắt ngang A-A: chèn sau mặt bằng thang
        var extra = StairPlanRebar.Build(r, sd, L, s ?? new DetailSettings());
        figs.InsertRange(Math.Min(1, figs.Count), extra);
        if (st.Steps == StepConstruction.Concrete && L.Valid)
            figs.Add(new Figure(StairForms.StepDetail(r, L.Riser, L.Tread, L.WaistT, StepBarDia, s ?? new DetailSettings()), "CHI TIẾT BẬC BTCT " + Name));
        return figs;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using CSiAPIv1;
using VXT.KetCau.Core.Analysis;
using VXT.KetCau.Core.Codes;
using VXT.KetCau.Core.Geometry;
using VXT.KetCau.Core.Model;

namespace VXT.KetCau.Csi
{
    public enum CsiProgram { Etabs, Sap2000 }

    /// <summary>
    /// Tạo mô hình, chạy phân tích và đọc nội lực ETABS / SAP2000 qua CSi OAPI (CSiAPIv1).
    /// Đơn vị làm việc trong OAPI: kN, m, °C. Mô hình trung gian dùng mm → chia 1000 khi xuất.
    /// </summary>
    public class CsiEngine : IAnalysisEngine
    {
        readonly CsiProgram prog;
        cOAPI app;
        cSapModel sm;
        bool startedByUs;
        public List<string> Log { get; } = new List<string>();
        public string Name => prog == CsiProgram.Etabs ? "ETABS" : "SAP2000";
        public string ConcreteName { get; private set; }
        public string RebarName { get; private set; }

        public CsiEngine(CsiProgram p) { prog = p; }

        string ProgId => prog == CsiProgram.Etabs ? "CSI.ETABS.API.ETABSObject" : "CSI.SAP2000.API.SapObject";

        static void Chk(int ret, string what)
        {
            if (ret != 0) throw new InvalidOperationException($"OAPI lỗi khi {what} (mã {ret})");
        }

        void Warn(int ret, string what) { if (ret != 0) Log.Add($"Cảnh báo: {what} trả về mã {ret}"); }

        // =============================================================== Kết nối
        public void Connect(bool attachToRunning, string programPath = null)
        {
            cHelper helper = new Helper();
            if (attachToRunning)
            {
                try { app = helper.GetObject(ProgId); }
                catch { app = null; }
                if (app == null) throw new InvalidOperationException($"Không tìm thấy {Name} đang mở. Hãy mở {Name} trước hoặc chọn 'Khởi động mới'.");
            }
            else
            {
                app = string.IsNullOrEmpty(programPath) ? helper.CreateObjectProgID(ProgId) : helper.CreateObject(programPath);
                Chk(app.ApplicationStart(), $"khởi động {Name}");
                startedByUs = true;
            }
            sm = app.SapModel;
            Log.Add($"Đã kết nối {Name}");
        }

        // =============================================================== Tạo mô hình
        public void BuildModel(StructModel m, string savePath)
        {
            if (sm == null) throw new InvalidOperationException("Chưa kết nối phần mềm phân tích");
            Chk(sm.InitializeNewModel(eUnits.kN_m_C), "khởi tạo mô hình");
            Chk(sm.File.NewBlank(), "tạo mô hình trắng");
            Chk(sm.SetPresentUnits(eUnits.kN_m_C), "đặt đơn vị");

            DefineMaterials(m);
            DefineSections(m);
            if (prog == CsiProgram.Etabs) DefineStoriesEtabs(m);
            DefineGrids(m);

            var baseNames = new HashSet<string>();
            // ----- Cột
            foreach (var c in m.Columns)
            {
                string name = "";
                Chk(sm.FrameObj.AddByCoord(c.Location.X / 1000, c.Location.Y / 1000, c.BaseZ / 1000,
                                           c.Location.X / 1000, c.Location.Y / 1000, c.TopZ / 1000, ref name, c.Section, "", "Global"), $"tạo cột {c.Location}");
                c.CsiName = name;
                if (Math.Abs(c.RotationDeg) > 0.01) Warn(sm.FrameObj.SetLocalAxes(name, c.RotationDeg), "xoay cột");
                if (Math.Abs(c.BaseZ - m.Stories[0].Elevation) < 1)
                {
                    string p1 = "", p2 = "";
                    sm.FrameObj.GetPoints(name, ref p1, ref p2);
                    baseNames.Add(p1);
                }
            }
            // ----- Dầm
            foreach (var b in m.Beams)
            {
                string name = "";
                Chk(sm.FrameObj.AddByCoord(b.Start.X / 1000, b.Start.Y / 1000, b.TopZ / 1000,
                                           b.End.X / 1000, b.End.Y / 1000, b.TopZ / 1000, ref name, b.Section, "", "Global"), $"tạo dầm {b.Start}-{b.End}");
                b.CsiName = name;
                if (prog == CsiProgram.Sap2000)
                {
                    // SAP2000: dầm vẽ tại tim → dịch điểm chèn lên mép trên (cardinal 8 = top center)
                    var off1 = new double[3]; var off2 = new double[3];
                    Warn(sm.FrameObj.SetInsertionPoint(name, 8, false, true, ref off1, ref off2, "Local"), "đặt điểm chèn dầm");
                }
            }
            // ----- Sàn
            foreach (var s in m.Slabs)
            {
                int n = s.Boundary.Count;
                var x = s.Boundary.Select(p => p.X / 1000).ToArray();
                var y = s.Boundary.Select(p => p.Y / 1000).ToArray();
                var z = Enumerable.Repeat(s.TopZ / 1000, n).ToArray();
                string name = "";
                Chk(sm.AreaObj.AddByCoord(n, ref x, ref y, ref z, ref name, s.Section, "", "Global"), $"tạo sàn {s.Label}");
                s.CsiName = name;
            }
            // ----- Liên kết chân cột: ngàm
            foreach (var p in baseNames)
            {
                var r = new[] { true, true, true, true, true, true };
                Warn(sm.PointObj.SetRestraint(p, ref r), "gán ngàm chân cột");
            }
            // ----- Sàn tuyệt đối cứng (ETABS)
            if (prog == CsiProgram.Etabs)
            {
                Warn(sm.Diaphragm.SetDiaphragm("D1", false), "tạo diaphragm");
                foreach (var s in m.Slabs) Warn(sm.AreaObj.SetDiaphragm(s.CsiName, "D1"), "gán diaphragm");
                // ETABS tự chia lưới sàn khi phân tích (Auto Rectangular Mesh)
            }
            else
            {
                // SAP2000: chia mỗi ô sàn 6×6 phần tử để truyền tải về dầm
                foreach (var s in m.Slabs)
                {
                    try { Warn(sm.AreaObj.SetAutoMesh(s.CsiName, 1, 6, 6), "chia lưới sàn"); }
                    catch (Exception ex) { Log.Add("Không chia lưới sàn tự động: " + ex.Message); break; }
                }
            }

            DefineLoads(m);
            DefineCombos(m);

            if (!string.IsNullOrEmpty(savePath)) Chk(sm.File.Save(savePath), "lưu mô hình");
            sm.View.RefreshView(0, false);
            Log.Add($"Đã tạo mô hình {Name}: {m.Columns.Count} cột, {m.Beams.Count} dầm, {m.Slabs.Count} sàn");
        }

        void DefineMaterials(StructModel m)
        {
            var c = ConcreteGrade.Get(m.Settings.Concrete);
            var s = SteelGrade.Get(m.Settings.MainSteel);
            var sw = SteelGrade.Get(m.Settings.StirrupSteel);
            ConcreteName = c.Name;
            Chk(sm.PropMaterial.SetMaterial(c.Name, eMatType.Concrete), "tạo vật liệu bê tông");
            Chk(sm.PropMaterial.SetMPIsotropic(c.Name, c.Eb * 1000, 0.2, 1e-5), "gán E bê tông");
            Chk(sm.PropMaterial.SetWeightAndMass(c.Name, 1, 25), "gán trọng lượng riêng");
            // fc' trụ ≈ Rb,n / 0.8 (quy đổi gần đúng cho thiết kế trong ETABS nếu dùng)
            Warn(sm.PropMaterial.SetOConcrete_1(c.Name, c.Rbn / 0.8 * 1000, false, 0, 2, 4, 0.002, 0.0035, -0.1, 0, 0), "gán fc'");
            foreach (var g in new[] { s, sw }.Distinct())
            {
                Chk(sm.PropMaterial.SetMaterial(g.Name, eMatType.Rebar), "tạo vật liệu thép");
                Chk(sm.PropMaterial.SetMPUniaxial(g.Name, g.Es * 1000, 1.17e-5), "gán E thép");
                Chk(sm.PropMaterial.SetWeightAndMass(g.Name, 1, 78.5), "gán trọng lượng thép");
                Warn(sm.PropMaterial.SetORebar_1(g.Name, g.Rsn * 1000, g.Rsn * 1.25 * 1000, g.Rsn * 1000, g.Rsn * 1.25 * 1000, 2, 1, 0.02, 0.1, -0.1, false), "gán cường độ thép");
            }
            RebarName = s.Name;
        }

        void DefineSections(StructModel m)
        {
            foreach (var sec in m.Sections)
            {
                Chk(sm.PropFrame.SetRectangle(sec.Name, ConcreteName, sec.H / 1000, sec.B / 1000), $"tạo tiết diện {sec.Name}");
                if (sec.Kind == SectionKind.Beam)
                    Warn(sm.PropFrame.SetRebarBeam(sec.Name, RebarName, RebarName, m.Settings.CoverBeam / 1000 + 0.02, m.Settings.CoverBeam / 1000 + 0.02, 0, 0, 0, 0), "khai báo thép dầm");
                else
                    Warn(sm.PropFrame.SetRebarColumn(sec.Name, RebarName, RebarName, 1, 1, m.Settings.CoverColumn / 1000 + 0.01, 0, 3, 3, "16", "8", 0.15, 2, 2, true), "khai báo thép cột");
                // giảm độ cứng tiết diện nứt (ETABS: dầm 0.35, cột 0.7) — người dùng có thể bỏ
                var mod = sec.Kind == SectionKind.Beam ? new double[] { 1, 1, 1, 1, 0.35, 0.35, 1, 1 } : new double[] { 1, 1, 1, 1, 0.7, 0.7, 1, 1 };
                Warn(sm.PropFrame.SetModifiers(sec.Name, ref mod), "hệ số giảm độ cứng");
            }
            foreach (var s in m.SlabSections)
            {
                if (prog == CsiProgram.Etabs)
                    Chk(sm.PropArea.SetSlab(s.Name, eSlabType.Slab, eShellType.ShellThin, ConcreteName, s.Thickness / 1000), $"tạo sàn {s.Name}");
                else
                    Chk(sm.PropArea.SetShell_1(s.Name, 1, true, ConcreteName, 0, s.Thickness / 1000, s.Thickness / 1000), $"tạo sàn {s.Name}");
            }
        }

        void DefineStoriesEtabs(StructModel m)
        {
            var st = m.Stories.Skip(1).ToList();
            int n = st.Count;
            string[] names = st.Select(s => s.Name).ToArray();
            double[] heights = st.Select(s => s.Height / 1000).ToArray();
            bool[] master = new bool[n]; string[] similar = Enumerable.Repeat("None", n).ToArray();
            bool[] splice = new bool[n]; double[] spliceH = new double[n]; int[] color = new int[n];
            Chk(sm.Story.SetStories_2(m.Stories[0].Elevation / 1000, n, ref names, ref heights, ref master, ref similar, ref splice, ref spliceH, ref color), "khai báo tầng");
        }

        void DefineGrids(StructModel m)
        {
            // Lưới trục chỉ để hiển thị; bỏ qua nếu phiên bản không hỗ trợ.
            try
            {
                if (prog != CsiProgram.Etabs) return;
                var xs = m.Grids.Where(g => !g.IsXDir && g.Line.IsVertical(2)).OrderBy(g => g.Line.Mid.X).ToList();
                var ys = m.Grids.Where(g => g.IsXDir).OrderBy(g => g.Line.Mid.Y).ToList();
                if (xs.Count == 0 || ys.Count == 0) return;
                var gridIds = xs.Select(g => g.Name).Concat(ys.Select(g => g.Name)).ToArray();
                Log.Add($"Lưới trục: {string.Join(", ", gridIds)} (hãy kiểm tra Grid System trong ETABS nếu cần hiển thị)");
            }
            catch { }
        }

        void DefineLoads(StructModel m)
        {
            foreach (var lp in m.LoadPatterns)
            {
                eLoadPatternType t;
                switch (lp.Type)
                {
                    case LoadType.Dead: t = eLoadPatternType.Dead; break;
                    case LoadType.SuperDead: t = eLoadPatternType.SuperDead; break;
                    case LoadType.Live: t = eLoadPatternType.Live; break;
                    case LoadType.LiveRoof: t = (eLoadPatternType)11; break; // RoofLive
                    case LoadType.Wind: t = eLoadPatternType.Wind; break;
                    default: t = eLoadPatternType.Other; break;
                }
                Chk(sm.LoadPatterns.Add(lp.Name, t, lp.SelfWeightMultiplier, true), $"tạo trường hợp tải {lp.Name}");
            }
            // tải sàn (hướng trọng lực = 10)
            foreach (var a in m.AreaLoads)
            {
                var s = m.Slabs.FirstOrDefault(x => x.Id == a.SlabId);
                if (s?.CsiName == null || a.Value == 0) continue;
                Warn(sm.AreaObj.SetLoadUniform(s.CsiName, a.Pattern, a.Value, 10, false, "Global"), $"tải sàn {s.Label}");
            }
            // tải tường trên dầm
            foreach (var l in m.LineLoads)
            {
                var b = m.Beams.FirstOrDefault(x => x.Id == l.BeamId);
                if (b?.CsiName == null || l.Value == 0) continue;
                Warn(sm.FrameObj.SetLoadDistributed(b.CsiName, l.Pattern, 1, 10, 0, 1, l.Value, l.Value, "Global", true, false), $"tải tường dầm {b.CsiName}");
            }
            // gió tĩnh: lực tập trung tại tâm từng tầng
            if (m.Settings.WindW0 > 0) DefineWind(m);
            // động đất: phổ phản ứng TCVN 9386
            if (m.Settings.SeismicAgR > 0) DefineSeismic(m);
        }

        void DefineWind(StructModel m)
        {
            var pts = m.Slabs.SelectMany(s => s.Boundary).Concat(m.Columns.Select(c => c.Location)).ToList();
            double Bx = (pts.Max(p => p.X) - pts.Min(p => p.X)) / 1000, By = (pts.Max(p => p.Y) - pts.Min(p => p.Y)) / 1000;
            var ctr = new P2((pts.Max(p => p.X) + pts.Min(p => p.X)) / 2, (pts.Max(p => p.Y) + pts.Min(p => p.Y)) / 2);
            double z0 = m.Stories[1].Elevation;
            var st = m.Stories.Skip(1).ToList();
            for (int i = 0; i < st.Count; i++)
            {
                double hBelow = i == 0 ? 0 : (st[i].Elevation - st[i - 1].Elevation) / 2;
                double hAbove = i + 1 < st.Count ? (st[i + 1].Elevation - st[i].Elevation) / 2 : 0;
                double trib = (hBelow + hAbove) / 1000;
                if (trib <= 0) continue;
                double w = Tcvn2737.WindPressure(m.Settings.WindW0, st[i].Elevation - z0, m.Settings.Terrain);
                double Fx = w * By * trib, Fy = w * Bx * trib; // kN (đã gồm hệ số khí động đón + hút 1.4)
                string pn = "";
                Chk(sm.PointObj.AddCartesian(ctr.X / 1000, ctr.Y / 1000, st[i].Elevation / 1000, ref pn, "", "Global"), "tạo điểm đặt gió");
                if (prog == CsiProgram.Etabs) Warn(sm.PointObj.SetDiaphragm(pn, (eDiaphragmOption)3, "D1"), "gán diaphragm điểm gió");
                foreach (var (pat, v) in new[] { ("GX", new[] { Fx, 0, 0, 0, 0, 0.0 }), ("GXX", new[] { -Fx, 0, 0, 0, 0, 0.0 }), ("GY", new[] { 0, Fy, 0, 0, 0, 0.0 }), ("GYY", new[] { 0, -Fy, 0, 0, 0, 0.0 }) })
                {
                    var val = v;
                    Warn(sm.PointObj.SetLoadForce(pn, pat, ref val, true, "Global"), $"tải gió {pat}");
                }
            }
            Log.Add("Đã gán tải gió tĩnh GX, GXX, GY, GYY tại tâm các tầng (kiểm tra lại W0 và hệ số khí động)");
        }

        void DefineSeismic(StructModel m)
        {
            var tbl = Tcvn9386.Table(m.Settings.SeismicAgR, m.Settings.SeismicGround, m.Settings.SeismicQ, m.Settings.ImportanceFactor);
            var T = tbl.Select(x => x.T).ToArray(); var Sa = tbl.Select(x => x.Sa).ToArray();
            Chk(sm.Func.FuncRS.SetUser("TCVN9386", T.Length, ref T, ref Sa, 0.05), "khai báo phổ TCVN 9386");
            // nguồn khối lượng: TT + HTHT + 0.3·HT (ψE ≈ 0.3 nhà ở)
            try
            {
                string[] pats = { "TT", "HTHT", "HT" }; double[] sf = { 1, 1, 0.3 };
                Warn(sm.SourceMass.SetMassSource("MSS", false, false, true, true, 3, ref pats, ref sf), "nguồn khối lượng");
            }
            catch (Exception ex) { Log.Add("Không đặt được nguồn khối lượng tự động: " + ex.Message); }
            foreach (var (name, dir) in new[] { ("EX", "U1"), ("EY", "U2") })
            {
                Chk(sm.LoadCases.ResponseSpectrum.SetCase(name), $"tạo trường hợp {name}");
                string[] ln = { dir }, fn = { "TCVN9386" }, cs = { "Global" }; double[] sf = { 9.81 }, ang = { 0 };
                Chk(sm.LoadCases.ResponseSpectrum.SetLoads(name, 1, ref ln, ref fn, ref sf, ref cs, ref ang), $"gán phổ {name}");
            }
            Log.Add($"Đã khai báo phổ phản ứng TCVN 9386: agR = {m.Settings.SeismicAgR}g, nền {m.Settings.SeismicGround}, q = {m.Settings.SeismicQ}");
        }

        void DefineCombos(StructModel m)
        {
            foreach (var c in m.Combos)
            {
                Chk(sm.RespCombo.Add(c.Name, c.IsEnvelope ? 1 : 0), $"tạo tổ hợp {c.Name}");
                foreach (var (cs, f) in c.Items)
                {
                    eCNameType t = m.Combos.Any(x => x.Name == cs) ? eCNameType.LoadCombo : eCNameType.LoadCase;
                    Warn(sm.RespCombo.SetCaseList(c.Name, ref t, cs, f), $"thêm {cs} vào {c.Name}");
                }
            }
        }

        // =============================================================== Phân tích & kết quả
        public void RunAnalysis()
        {
            Chk(sm.Analyze.RunAnalysis(), "chạy phân tích");
            Log.Add("Đã chạy phân tích");
        }

        public void ReadResults(StructModel m, IEnumerable<string> combos = null)
        {
            var list = (combos ?? m.Combos.Where(c => !c.IsEnvelope && !c.IsServiceability).Select(c => c.Name)).ToList();
            Chk(sm.SetPresentUnits(eUnits.kN_m_C), "đặt đơn vị");
            Chk(sm.Results.Setup.DeselectAllCasesAndCombosForOutput(), "chọn kết quả");
            foreach (var c in list) Warn(sm.Results.Setup.SetComboSelectedForOutput(c, true), $"chọn tổ hợp {c}");

            foreach (var b in m.Beams.Where(x => x.CsiName != null))
            {
                var f = new BeamForces();
                ReadFrame(b.CsiName, (sta, combo, P, V2, V3, T, M2, M3) =>
                    f.Stations.Add(new BeamStation { X = sta * 1000, Combo = combo, M = M3, V = V2, T = T, N = P }));
                f.Envelope(b.Length);
                b.Forces = f;
            }
            foreach (var c in m.Columns.Where(x => x.CsiName != null))
            {
                var f = new ColumnForces();
                double L = c.Length / 1000;
                ReadFrame(c.CsiName, (sta, combo, P, V2, V3, T, M2, M3) =>
                {
                    if (sta > 1e-3 && sta < L - 1e-3) return; // chỉ lấy chân & đỉnh
                    f.Sets.Add(new ColumnForceSet { Combo = combo, Location = sta < L / 2 ? "Chân" : "Đỉnh", N = -P, M2 = M2, M3 = M3, V2 = V2, V3 = V3 });
                });
                c.Forces = f;
            }
            Log.Add($"Đã đọc nội lực {m.Beams.Count} dầm, {m.Columns.Count} cột theo {list.Count} tổ hợp");
        }

        void ReadFrame(string name, Action<double, string, double, double, double, double, double, double> sink)
        {
            int n = 0;
            string[] obj = null, elm = null, lc = null, stepType = null;
            double[] objSta = null, elmSta = null, stepNum = null, P = null, V2 = null, V3 = null, T = null, M2 = null, M3 = null;
            int ret = sm.Results.FrameForce(name, eItemTypeElm.ObjectElm, ref n, ref obj, ref objSta, ref elm, ref elmSta, ref lc,
                                            ref stepType, ref stepNum, ref P, ref V2, ref V3, ref T, ref M2, ref M3);
            if (ret != 0) { Log.Add($"Không đọc được nội lực phần tử {name}"); return; }
            for (int i = 0; i < n; i++) sink(objSta[i], lc[i], P[i], V2[i], V3[i], T[i], M2[i], M3[i]);
        }

        public void Dispose()
        {
            if (app != null && startedByUs)
            {
                // không tự đóng phần mềm để người dùng xem mô hình; chỉ giải phóng tham chiếu
            }
            sm = null; app = null;
        }
    }
}

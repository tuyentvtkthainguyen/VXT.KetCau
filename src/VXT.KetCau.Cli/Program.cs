using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using VXT.KetCau.Core.Framing;
using VXT.KetCau.Core.Io;
using VXT.KetCau.Core.Model;
using VXT.KetCau.Core.Report;
using VXT.KetCau.Core.Workflow;
using VXT.KetCau.Csi;
using VXT.KetCau.Dwg;

namespace VXT.KetCau.Cli
{
    /// <summary>Cấu hình chạy (JSON).</summary>
    public class RunConfig
    {
        public string ProjectName { get; set; } = "Công trình";
        public List<FloorPlanInput> Plans { get; set; } = new List<FloorPlanInput>();
        public DesignSettings Settings { get; set; } = new DesignSettings();
        public FramingOptions Framing { get; set; } = new FramingOptions();
        /// <summary>"ETABS" | "SAP2000" | "Sơ bộ"</summary>
        public string Engine { get; set; } = "Sơ bộ";
        public bool AttachToRunning { get; set; } = false;
        public string OutputDir { get; set; } = "KetQua";
        public string OdaConverter { get; set; }
    }

    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
            {
                Console.WriteLine("VXT Kết cấu — dòng lệnh");
                Console.WriteLine("  vxtkc mau  cauhinh.json          Tạo file cấu hình mẫu");
                Console.WriteLine("  vxtkc chay cauhinh.json          DWG/DXF → khung → (ETABS/SAP) → thiết kế → DXF + thuyết minh");
                Console.WriteLine("  vxtkc thietke mohinh.vxtkc.json  Thiết kế lại từ mô hình đã lưu (đã có nội lực)");
                return 0;
            }
            try
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "mau": return Sample(args.Length > 1 ? args[1] : "cauhinh.json");
                    case "chay": return RunAll(args[1]);
                    case "thietke": return Redesign(args[1]);
                    default: Console.WriteLine("Lệnh không hợp lệ. Gõ vxtkc --help"); return 1;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("LỖI: " + ex.Message);
                Console.ResetColor();
                return 2;
            }
        }

        static int Sample(string path)
        {
            var c = new RunConfig
            {
                ProjectName = "Nhà phố 4 tầng",
                Plans =
                {
                    new FloorPlanInput { Path = @"C:\HoSo\MB-Tang1.dwg", LevelName = "Tầng 1", Elevation = 0 },
                    new FloorPlanInput { Path = @"C:\HoSo\MB-Tang2.dwg", LevelName = "Tầng 2", Elevation = 3900 },
                    new FloorPlanInput { Path = @"C:\HoSo\MB-Tang3.dwg", LevelName = "Tầng 3", Elevation = 7200 },
                    new FloorPlanInput { Path = @"C:\HoSo\MB-Tang4.dwg", LevelName = "Tầng 4", Elevation = 10500 },
                    new FloorPlanInput { Path = @"C:\HoSo\MB-Mai.dwg", LevelName = "Mái", Elevation = 13800, IsRoof = true },
                },
                Engine = "ETABS",
                AttachToRunning = false,
            };
            c.Settings.WindW0 = 0.83;
            File.WriteAllText(path, JsonConvert.SerializeObject(c, Formatting.Indented), new UTF8Encoding(false));
            Console.WriteLine("Đã tạo " + path + " — sửa đường dẫn file, cao độ, thông số rồi chạy: vxtkc chay " + path);
            return 0;
        }

        static int RunAll(string cfgPath)
        {
            var cfg = JsonConvert.DeserializeObject<RunConfig>(File.ReadAllText(cfgPath, Encoding.UTF8));
            string outDir = Path.IsPathRooted(cfg.OutputDir) ? cfg.OutputDir : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(cfgPath)), cfg.OutputDir);
            Directory.CreateDirectory(outDir);

            Step("1/5 Đọc hồ sơ kiến trúc");
            var imp = new DxfArchImporter();
            var arch = imp.Import(cfg.Plans, cfg.OdaConverter);
            arch.ProjectName = cfg.ProjectName;
            imp.Log.ForEach(Info);
            ModelIo.Save(arch, Path.Combine(outDir, "kientruc.vxtkc.json"));

            Step("2/5 Sinh khung kết cấu");
            var gen = new FramingGenerator(cfg.Settings, cfg.Framing);
            var sm = gen.Generate(arch);
            gen.Report.Info.ForEach(Info); gen.Report.Warnings.ForEach(Warn);

            bool analysed = false;
            if (cfg.Engine == "ETABS" || cfg.Engine == "SAP2000")
            {
                Step($"3/5 Tạo mô hình và chạy {cfg.Engine}");
                var prog = cfg.Engine == "SAP2000" ? CsiProgram.Sap2000 : CsiProgram.Etabs;
                using (var eng = new CsiEngine(prog))
                {
                    eng.Connect(cfg.AttachToRunning);
                    eng.BuildModel(sm, Path.Combine(outDir, cfg.ProjectName + (prog == CsiProgram.Etabs ? ".EDB" : ".sdb")));
                    eng.RunAnalysis();
                    eng.ReadResults(sm);
                    eng.Log.ForEach(Info);
                    analysed = true;
                }
            }
            else Step("3/5 Bỏ qua phân tích — dùng nội lực sơ bộ");

            return DesignAndExport(sm, analysed, outDir, cfg.Engine, cfg.Framing.SoilBearing);
        }

        static int Redesign(string modelPath)
        {
            var sm = ModelIo.LoadStruct(modelPath);
            bool analysed = sm.Beams.Any(b => b.Forces != null && b.Forces.Stations.Count > 0);
            return DesignAndExport(sm, analysed, Path.GetDirectoryName(Path.GetFullPath(modelPath)), analysed ? "ETABS/SAP2000" : "Sơ bộ", 150);
        }

        static int DesignAndExport(StructModel sm, bool analysed, string outDir, string engine, double soilR)
        {
            Step("4/5 Thiết kế TCVN 5574:2018 và cấu tạo thép");
            var wf = new StructuralWorkflow(sm) { SoilBearing = soilR };
            if (!analysed) wf.PreliminaryForces();
            var res = wf.DetailAll();
            if (!analysed) res.Warnings.Insert(0, "Đang dùng NỘI LỰC SƠ BỘ — chỉ dùng tham khảo");

            Step("5/5 Xuất kết quả");
            ModelIo.Save(sm, Path.Combine(outDir, "ketcau.vxtkc.json"));
            File.WriteAllText(Path.Combine(outDir, "Thuyet minh ket cau.html"), CalcReport.Html(sm, res, engine), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(outDir, "Thong ke cot thep.csv"), res.Schedule.ToCsv(), new UTF8Encoding(true));
            ShopDrawingDxf.Write(res, sm.Settings, Path.Combine(outDir, "Shop drawing thep.dxf"));
            var g = res.Schedule.ByGroup();
            Info($"Cấu kiện: {res.Elements.Count(e => e.Category == "Dầm")} dầm, {res.Elements.Count(e => e.Category == "Cột")} cột, {res.Elements.Count(e => e.Category == "Sàn")} sàn, {res.Elements.Count(e => e.Category == "Móng")} móng");
            Info($"Tổng thép: {g.Total:0.0} kg");
            foreach (var w in res.Warnings.Distinct()) Warn(w);
            Info("Kết quả tại: " + outDir);
            return 0;
        }

        static void Step(string s) { Console.ForegroundColor = ConsoleColor.Cyan; Console.WriteLine("== " + s); Console.ResetColor(); }
        static void Info(string s) => Console.WriteLine("   " + s);
        static void Warn(string s) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine("   ⚠ " + s); Console.ResetColor(); }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(VXT.KetCau.AutoCAD.AcadApp))]
[assembly: CommandClass(typeof(VXT.KetCau.AutoCAD.AcadCommands))]

namespace VXT.KetCau.AutoCAD
{
    /// <summary>Nạp add-in: tìm thư viện phụ thuộc trong thư mục add-in và CSiAPIv1.dll trong thư mục cài ETABS/SAP2000.</summary>
    public class AcadApp : IExtensionApplication
    {
        static readonly string Here = Path.GetDirectoryName(typeof(AcadApp).Assembly.Location);

        public void Initialize()
        {
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            try
            {
                var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage("\nVXT Kết cấu đã nạp. Gõ VXTKC để mở bảng điều khiển (hoặc VXTKC_KIENTRUC, VXTKC_PHANTICH, VXTKC_THIETKE, VXTKC_SHOP, VXTKC_THONGSO).\n");
            }
            catch { }
        }

        public void Terminate() { }

        static Assembly Resolve(object sender, ResolveEventArgs e)
        {
            string name = new AssemblyName(e.Name).Name;
            if (string.IsNullOrEmpty(name) || name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase)) return null;
            string local = Path.Combine(Here, name + ".dll");
            if (File.Exists(local)) return Assembly.LoadFrom(local);
            if (name.Equals("CSiAPIv1", StringComparison.OrdinalIgnoreCase))
            {
                string f = FindCsiApi();
                if (f != null) return Assembly.LoadFrom(f);
            }
            return null;
        }

        /// <summary>Tìm CSiAPIv1.dll: thư mục chỉ định trong Thông số → bản ETABS/SAP2000 mới nhất trong Program Files.</summary>
        public static string FindCsiApi()
        {
            try
            {
                string custom = AcadSession.LastEnginePath;
                if (!string.IsNullOrEmpty(custom))
                {
                    string d = Directory.Exists(custom) ? custom : Path.GetDirectoryName(custom);
                    string f = Path.Combine(d ?? "", "CSiAPIv1.dll");
                    if (File.Exists(f)) return f;
                }
                foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"C:\Program Files" }.Distinct())
                {
                    string csi = Path.Combine(root, "Computers and Structures");
                    if (!Directory.Exists(csi)) continue;
                    var hit = Directory.GetDirectories(csi)
                        .OrderByDescending(d => d.IndexOf("ETABS", StringComparison.OrdinalIgnoreCase) >= 0)
                        .ThenByDescending(d => d, StringComparer.OrdinalIgnoreCase)
                        .Select(d => Path.Combine(d, "CSiAPIv1.dll"))
                        .FirstOrDefault(File.Exists);
                    if (hit != null) return hit;
                }
            }
            catch { }
            return null;
        }
    }
}

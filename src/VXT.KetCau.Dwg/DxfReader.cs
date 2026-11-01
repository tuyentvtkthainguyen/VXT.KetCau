using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VXT.KetCau.Core.Geometry;

namespace VXT.KetCau.Dwg
{
    public abstract class DxfEntity { public string Layer = "0"; }
    public class DxfLine : DxfEntity { public P2 A, B; }
    public class DxfPolyline : DxfEntity { public List<P2> Points = new List<P2>(); public bool Closed; }
    public class DxfCircle : DxfEntity { public P2 Center; public double R; }
    public class DxfText : DxfEntity { public P2 At; public string Text; public double Height; }

    /// <summary>
    /// Bộ đọc DXF ASCII tối giản: LINE, LWPOLYLINE, POLYLINE/VERTEX, CIRCLE, TEXT, MTEXT, INSERT (phá khối đệ quy).
    /// Đủ cho mặt bằng kiến trúc; không cần AutoCAD hay thư viện ngoài.
    /// </summary>
    public class DxfReader
    {
        public List<DxfEntity> Entities { get; } = new List<DxfEntity>();
        public int InsUnits { get; private set; }
        readonly Dictionary<string, List<DxfEntity>> blocks = new Dictionary<string, List<DxfEntity>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, P2> blockBase = new Dictionary<string, P2>(StringComparer.OrdinalIgnoreCase);
        readonly List<(string name, P2 at, double sx, double sy, double rot, string layer, List<DxfEntity> target)> inserts = new List<(string, P2, double, double, double, string, List<DxfEntity>)>();

        public static DxfReader Load(string path)
        {
            var r = new DxfReader();
            var text = ReadText(path);
            r.Parse(text);
            return r;
        }

        static string ReadText(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 22));
            if (head.StartsWith("AutoCAD Binary DXF")) throw new NotSupportedException("DXF nhị phân chưa hỗ trợ — hãy lưu DXF dạng ASCII");
            // DXF ≥ 2007 là UTF-8; cũ hơn dùng code page (VN: 1258) — thử UTF-8 trước
            try { return new UTF8Encoding(false, true).GetString(bytes); }
            catch { return Encoding.GetEncoding("iso-8859-1").GetString(bytes); }
        }

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        void Parse(string text)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var pairs = new List<(int code, string val)>(lines.Length / 2);
            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (!int.TryParse(lines[i].Trim(), out var c)) { i--; continue; }
                pairs.Add((c, lines[i + 1].TrimEnd()));
            }
            string section = null;
            string curBlock = null;
            List<DxfEntity> target = Entities;
            for (int i = 0; i < pairs.Count; i++)
            {
                var (code, val) = pairs[i];
                if (code != 0) continue;
                if (val == "SECTION") { section = pairs[i + 1].val; continue; }
                if (val == "ENDSEC") { section = null; continue; }
                if (section == "HEADER") continue;
                if (section == "BLOCKS" && val == "BLOCK")
                {
                    var props = Props(pairs, i);
                    curBlock = Get(props, 2) ?? "";
                    blocks[curBlock] = target = new List<DxfEntity>();
                    blockBase[curBlock] = new P2(D(props, 10), D(props, 20));
                    continue;
                }
                if (section == "BLOCKS" && val == "ENDBLK") { curBlock = null; target = Entities; continue; }
                if (section != "ENTITIES" && section != "BLOCKS") continue;
                var p = Props(pairs, i);
                string layer = Get(p, 8) ?? "0";
                switch (val)
                {
                    case "LINE":
                        target.Add(new DxfLine { Layer = layer, A = new P2(D(p, 10), D(p, 20)), B = new P2(D(p, 11), D(p, 21)) });
                        break;
                    case "LWPOLYLINE":
                    {
                        var pl = new DxfPolyline { Layer = layer, Closed = (I(p, 70) & 1) == 1 };
                        double? x = null;
                        foreach (var (c, v) in p)
                        {
                            if (c == 10) x = Dbl(v);
                            else if (c == 20 && x.HasValue) { pl.Points.Add(new P2(x.Value, Dbl(v))); x = null; }
                        }
                        target.Add(pl);
                        break;
                    }
                    case "POLYLINE":
                    {
                        var pl = new DxfPolyline { Layer = layer, Closed = (I(p, 70) & 1) == 1 };
                        int j = i + 1;
                        while (j < pairs.Count && !(pairs[j].code == 0 && pairs[j].val == "SEQEND"))
                        {
                            if (pairs[j].code == 0 && pairs[j].val == "VERTEX")
                            {
                                var vp = Props(pairs, j);
                                pl.Points.Add(new P2(D(vp, 10), D(vp, 20)));
                            }
                            j++;
                        }
                        target.Add(pl);
                        i = j;
                        break;
                    }
                    case "CIRCLE":
                        target.Add(new DxfCircle { Layer = layer, Center = new P2(D(p, 10), D(p, 20)), R = D(p, 40) });
                        break;
                    case "TEXT":
                    case "MTEXT":
                    case "ATTRIB":
                    {
                        string t = string.Concat(p.Where(x => x.code == 3).Select(x => x.val)) + (Get(p, 1) ?? "");
                        target.Add(new DxfText { Layer = layer, At = new P2(D(p, 10), D(p, 20)), Text = CleanText(t), Height = D(p, 40) });
                        break;
                    }
                    case "INSERT":
                    {
                        double sx = Get(p, 41) != null ? D(p, 41) : 1, sy = Get(p, 42) != null ? D(p, 42) : 1;
                        inserts.Add((Get(p, 2), new P2(D(p, 10), D(p, 20)), sx, sy, D(p, 50), layer, target));
                        break;
                    }
                }
            }
            // phá khối (đệ quy tối đa 4 cấp)
            for (int depth = 0; depth < 4 && inserts.Count > 0; depth++)
            {
                var todo = inserts.ToList(); inserts.Clear();
                foreach (var ins in todo)
                {
                    if (ins.name == null || !blocks.TryGetValue(ins.name, out var ents)) continue;
                    var bp = blockBase[ins.name];
                    double r = ins.rot * Math.PI / 180, cs = Math.Cos(r), sn = Math.Sin(r);
                    Func<P2, P2> T = q => { double x = (q.X - bp.X) * ins.sx, y = (q.Y - bp.Y) * ins.sy; return new P2(ins.at.X + x * cs - y * sn, ins.at.Y + x * sn + y * cs); };
                    foreach (var e in ents)
                    {
                        string lay = e.Layer == "0" ? ins.layer : e.Layer; // thực thể lớp 0 trong khối theo lớp của INSERT
                        switch (e)
                        {
                            case DxfLine l: ins.target.Add(new DxfLine { Layer = lay, A = T(l.A), B = T(l.B) }); break;
                            case DxfPolyline pl: ins.target.Add(new DxfPolyline { Layer = lay, Closed = pl.Closed, Points = pl.Points.Select(T).ToList() }); break;
                            case DxfCircle c: ins.target.Add(new DxfCircle { Layer = lay, Center = T(c.Center), R = c.R * Math.Abs(ins.sx) }); break;
                            case DxfText t: ins.target.Add(new DxfText { Layer = lay, At = T(t.At), Text = t.Text, Height = t.Height * Math.Abs(ins.sy) }); break;
                        }
                    }
                }
            }
        }

        /// <summary>Bỏ mã định dạng MTEXT (\P, {\f…;}, \U+XXXX → ký tự).</summary>
        public static string CleanText(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = Regex.Replace(s, @"\\U\+([0-9A-Fa-f]{4})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
            s = Regex.Replace(s, @"\\[A-Za-z][^;\\{}]*;", "");
            s = s.Replace("\\P", " ").Replace("{", "").Replace("}", "").Replace("%%c", "Ø").Replace("%%C", "Ø");
            return s.Trim();
        }

        static List<(int code, string val)> Props(List<(int code, string val)> pairs, int i)
        {
            var res = new List<(int, string)>();
            for (int j = i + 1; j < pairs.Count && pairs[j].code != 0; j++) res.Add(pairs[j]);
            return res;
        }
        static string Get(List<(int code, string val)> p, int code) { foreach (var x in p) if (x.code == code) return x.val; return null; }
        static double Dbl(string v) => double.TryParse(v.Trim(), NumberStyles.Float, Ci, out var d) ? d : 0;
        static double D(List<(int code, string val)> p, int code) => Dbl(Get(p, code) ?? "0");
        static int I(List<(int code, string val)> p, int code) => int.TryParse((Get(p, code) ?? "0").Trim(), out var v) ? v : 0;
    }

    /// <summary>Chuyển DWG → DXF bằng ODA File Converter (miễn phí, tải tại opendesign.com).</summary>
    public static class DwgConverter
    {
        public static string DefaultExe =>
            new[] { @"C:\Program Files\ODA\ODAFileConverter\ODAFileConverter.exe" }
            .Concat(Directory.Exists(@"C:\Program Files\ODA") ? Directory.GetDirectories(@"C:\Program Files\ODA").Select(d => Path.Combine(d, "ODAFileConverter.exe")) : new string[0])
            .FirstOrDefault(File.Exists);

        /// <summary>Trả về đường dẫn file DXF tạm.</summary>
        public static string ToDxf(string dwgPath, string odaExe = null)
        {
            odaExe = odaExe ?? DefaultExe;
            if (odaExe == null || !File.Exists(odaExe))
                throw new FileNotFoundException("Chưa cài ODA File Converter. Cài từ opendesign.com hoặc tự lưu bản vẽ thành DXF trong AutoCAD.");
            var inDir = Path.Combine(Path.GetTempPath(), "vxtkc_in_" + Guid.NewGuid().ToString("N"));
            var outDir = Path.Combine(Path.GetTempPath(), "vxtkc_out_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(inDir); Directory.CreateDirectory(outDir);
            File.Copy(dwgPath, Path.Combine(inDir, Path.GetFileName(dwgPath)));
            var psi = new ProcessStartInfo(odaExe, $"\"{inDir}\" \"{outDir}\" ACAD2018 DXF 0 1 \"*.DWG\"")
            { UseShellExecute = false, CreateNoWindow = true };
            using (var p = Process.Start(psi)) p.WaitForExit(120000);
            var dxf = Directory.GetFiles(outDir, "*.dxf").FirstOrDefault();
            if (dxf == null) throw new InvalidOperationException("ODA File Converter không tạo được file DXF");
            return dxf;
        }
    }
}

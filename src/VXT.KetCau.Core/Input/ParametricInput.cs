using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using VXT.KetCau.Core.Design;
using VXT.KetCau.Core.Geometry;
using VXT.KetCau.Core.Model;

namespace VXT.KetCau.Core.Input
{
    /// <summary>
    /// Khai thông số thay cho đọc bản vẽ: nhịp trục, cao độ tầng, tiết diện chung. Tạo ra ArchModel như bản vẽ nhập vào,
    /// sau đó dùng chung bước kiểm tra, sinh khung, thiết kế và vẽ hồ sơ.
    /// </summary>
    public class ParametricInput
    {
        public string ProjectName { get; set; } = "Công trình";
        /// <summary>Nhịp theo X (mm), vd "3600 4200" hoặc "3x3600 4200".</summary>
        public string SpansX { get; set; } = "4000 4000";
        public string SpansY { get; set; } = "5000 5000";
        /// <summary>"Tầng 1=0; Tầng 2=3.6; Mái=7.2" (cao độ m). Tầng cuối là mái.</summary>
        public string Levels { get; set; } = "Tầng 1=0; Tầng 2=3.6; Mái=7.2";
        public double WallThickness { get; set; } = 220;
        /// <summary>Xây tường trên mọi trục (đúng) hay chỉ tường bao (sai).</summary>
        public bool WallsOnAllGrids { get; set; } = true;
        public double ColumnB { get; set; } = 220;
        public double ColumnH { get; set; } = 220;
        /// <summary>0 = để phần mềm tự chọn theo nhịp.</summary>
        public double BeamB { get; set; } = 0;
        public double BeamH { get; set; } = 0;
        public double SlabThickness { get; set; } = 0;
        /// <summary>Ô thang "x,y,rộng,dài" (mm) tính từ giao trục đầu; để trống nếu không có.</summary>
        public string Stair { get; set; } = "";

        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        public static List<double> ParseSpans(string text)
        {
            var res = new List<double>();
            foreach (var part in Regex.Split(text ?? "", @"[\s,;]+").Where(q => q.Length > 0))
            {
                var mx = Regex.Match(part, @"^(\d+)[xX*](\d+(?:\.\d+)?)$");
                if (mx.Success)
                {
                    int n = int.Parse(mx.Groups[1].Value, Ci);
                    double v = double.Parse(mx.Groups[2].Value, Ci);
                    for (int i = 0; i < n; i++) res.Add(v);
                }
                else if (double.TryParse(part, NumberStyles.Float, Ci, out var v2)) res.Add(v2);
                else throw new FormatException($"Không đọc được nhịp \"{part}\"");
            }
            if (res.Any(v => v <= 0)) throw new FormatException("Nhịp phải lớn hơn 0");
            return res;
        }

        public static List<ArchLevel> ParseLevels(string text)
        {
            var res = new List<ArchLevel>();
            foreach (var part in (text ?? "").Split(';').Select(q => q.Trim()).Where(q => q.Length > 0))
            {
                var kv = part.Split('=');
                if (kv.Length != 2 || !double.TryParse(kv[1].Trim().Replace(',', '.'), NumberStyles.Float, Ci, out var z))
                    throw new FormatException($"Không đọc được tầng \"{part}\" — viết dạng Tầng 1=0");
                res.Add(new ArchLevel { Name = kv[0].Trim(), Elevation = z * 1000 });
            }
            if (res.Count < 2) throw new FormatException("Cần ít nhất 2 cao độ (tầng và mái)");
            res = res.OrderBy(l => l.Elevation).ToList();
            res[res.Count - 1].IsRoof = true;
            return res;
        }

        public ArchModel ToArch()
        {
            var a = new ArchModel { ProjectName = ProjectName, Source = "Nhập thông số" };
            a.Levels.AddRange(ParseLevels(Levels));
            var xs = Accumulate(ParseSpans(SpansX));
            var ys = Accumulate(ParseSpans(SpansY));
            double L = xs.Last(), B = ys.Last(), ext = 1000;
            for (int i = 0; i < xs.Count; i++)
                a.Grids.Add(new ArchGrid { Name = (i + 1).ToString(Ci), Line = new Seg2(new P2(xs[i], -ext), new P2(xs[i], B + ext)) });
            for (int j = 0; j < ys.Count; j++)
                a.Grids.Add(new ArchGrid { Name = ((char)('A' + j)).ToString(), Line = new Seg2(new P2(-ext, ys[j]), new P2(L + ext, ys[j])) });

            int n = 0;
            foreach (var lv in a.Levels.Take(a.Levels.Count - 1))
            {
                for (int i = 0; i < xs.Count; i++)
                    if (WallsOnAllGrids || i == 0 || i == xs.Count - 1)
                        a.Walls.Add(new ArchWall { Id = "W" + ++n, Line = new Seg2(new P2(xs[i], 0), new P2(xs[i], B)), Thickness = WallThickness, BaseLevel = lv.Name });
                for (int j = 0; j < ys.Count; j++)
                    if (WallsOnAllGrids || j == 0 || j == ys.Count - 1)
                        a.Walls.Add(new ArchWall { Id = "W" + ++n, Line = new Seg2(new P2(0, ys[j]), new P2(L, ys[j])), Thickness = WallThickness, BaseLevel = lv.Name });
                foreach (var x in xs)
                    foreach (var y in ys)
                        a.Columns.Add(new ArchColumn { Id = $"{lv.Name}-C{++n}", Center = new P2(x, y), B = ColumnB, H = ColumnH, BaseLevel = lv.Name });
            }
            if (!string.IsNullOrWhiteSpace(Stair))
            {
                var v = Stair.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(q => double.Parse(q.Trim(), Ci)).ToArray();
                if (v.Length != 4) throw new FormatException("Ô thang viết dạng x,y,rộng,dài (mm)");
                foreach (var lv in a.Levels.Take(a.Levels.Count - 1))
                    a.Stairs.Add(new ArchStair
                    {
                        BaseLevel = lv.Name,
                        Boundary = { new P2(v[0], v[1]), new P2(v[0] + v[2], v[1]), new P2(v[0] + v[2], v[1] + v[3]), new P2(v[0], v[1] + v[3]) },
                    });
            }
            return a;
        }

        static List<double> Accumulate(List<double> spans)
        {
            var res = new List<double> { 0 };
            foreach (var v in spans) res.Add(res.Last() + v);
            return res;
        }

        /// <summary>Áp tiết diện khai tay lên mô hình đã sinh.</summary>
        public void ApplySections(StructModel m)
        {
            if (ColumnB > 0 && ColumnH > 0)
                foreach (var c in m.Columns) c.Section = m.GetOrAddSection("C", ColumnB, ColumnH).Name;
            if (BeamB > 0 && BeamH > 0)
                foreach (var b in m.Beams) b.Section = m.GetOrAddSection("D", BeamB, BeamH).Name;
            if (SlabThickness > 0)
                foreach (var s in m.Slabs) s.Section = m.GetOrAddSlab(SlabThickness).Name;
            m.Sections.RemoveAll(q => !m.Columns.Any(c => c.Section == q.Name) && !m.Beams.Any(b => b.Section == q.Name));
        }
    }

    /// <summary>Đọc chữ thép người dùng nhập: "3Ø20", "3d20", "Ø6a150", "8a200".</summary>
    public static class RebarText
    {
        public static BarGroup ParseBars(string text)
        {
            var mt = Regex.Match((text ?? "").Trim(), @"^(\d+)\s*[ØøφΦdDfF]\s*(\d+)$");
            if (!mt.Success) throw new FormatException($"Không đọc được thép \"{text}\" — viết dạng 3Ø20");
            return new BarGroup(int.Parse(mt.Groups[1].Value), int.Parse(mt.Groups[2].Value));
        }

        public static StirrupSpec ParseStirrup(string text, int legs = 2)
        {
            var (d, s) = ParseSpaced(text);
            return new StirrupSpec { D = d, S = s, Legs = legs };
        }

        public static SlabBar ParseSlab(string text)
        {
            var (d, s) = ParseSpaced(text);
            return new SlabBar { D = d, S = s };
        }

        static (int d, double s) ParseSpaced(string text)
        {
            var mt = Regex.Match((text ?? "").Trim(), @"^[ØøφΦdDfF]?\s*(\d+)\s*[aA@]\s*(\d+)$");
            if (!mt.Success) throw new FormatException($"Không đọc được \"{text}\" — viết dạng Ø6a150");
            return (int.Parse(mt.Groups[1].Value), double.Parse(mt.Groups[2].Value, CultureInfo.InvariantCulture));
        }

        /// <summary>Tổng số thanh cột → số thanh mỗi cạnh b và h (kể cả thanh góc); cạnh dài nhận nhiều thanh hơn.</summary>
        public static (int nb, int nh) SplitColumnBars(int total, double b, double h)
        {
            if (total < 4) throw new FormatException("Cột cần ít nhất 4 thanh");
            if (total % 2 == 1) throw new FormatException($"Cột {total} thanh lẻ không chia đối xứng được");
            int rest = total - 4; // thanh giữa cạnh
            int perPair = rest / 2;
            int onH = b <= h ? (int)Math.Ceiling(perPair * h / (b + h)) : (int)Math.Floor(perPair * h / (b + h));
            int onB = perPair - onH;
            return (2 + onB, 2 + onH);
        }

        /// <summary>Tổng diện tích thép cột (mm²) → số thanh và đường kính nhỏ nhất đạt yêu cầu, số thanh chẵn.</summary>
        public static (int total, int d) ColumnBarsFromArea(double asReq, IEnumerable<int> dias)
        {
            foreach (var d in dias.OrderBy(q => q))
                for (int n = 4; n <= 16; n += 2)
                    if (n * Codes.Tcvn5574.BarArea(d) >= asReq) return (n, d);
            var big = dias.Max();
            return (16, big);
        }

        /// <summary>Diện tích đai trên 1 m dài (mm²/m, tổng các nhánh) → đường kính, bước; bước lấy theo 25 mm, không quá sMax.</summary>
        public static StirrupSpec StirrupFromArea(double avPerM, IEnumerable<int> dias, double sMax = 200, int legs = 2)
        {
            foreach (var d in dias.OrderBy(q => q))
            {
                double area = legs * Codes.Tcvn5574.BarArea(d);
                double s = avPerM > 1 ? Math.Floor(area * 1000 / avPerM / 25) * 25 : sMax;
                if (s >= 75) return new StirrupSpec { D = d, S = Math.Min(s, sMax), Legs = legs };
            }
            return new StirrupSpec { D = dias.Max(), S = 75, Legs = legs };
        }
    }
}

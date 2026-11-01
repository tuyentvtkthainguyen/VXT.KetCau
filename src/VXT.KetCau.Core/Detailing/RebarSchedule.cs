using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using VXT.KetCau.Core.Codes;

namespace VXT.KetCau.Core.Detailing
{
    /// <summary>Một cấu kiện đã cấu tạo thép (dầm liên tục, cột, sàn, móng) + số lượng cấu kiện giống hệt.</summary>
    public class DetailedElement
    {
        /// <summary>Tên hiển thị: D2-8, C2, S3, M1…</summary>
        public string Label { get; set; }
        /// <summary>"Dầm" / "Cột" / "Sàn" / "Móng".</summary>
        public string Category { get; set; }
        public int Quantity { get; set; } = 1;
        /// <summary>Tổng chiều dài cấu kiện (mm) — ghi trên tên hình dầm.</summary>
        public double TotalLength { get; set; }
        public List<DetailBar> Bars { get; set; } = new List<DetailBar>();
        /// <summary>Id các phần tử nguồn (StructElem.Id) gộp trong cấu kiện này.</summary>
        public List<string> SourceIds { get; } = new List<string>();
        /// <summary>Dữ liệu hình học bổ sung cho bản vẽ (nhịp, gối, cao độ…).</summary>
        public object Geometry { get; set; }

        /// <summary>Tên hình kiểu Kết cấu Soft: dầm "D2-8 (SL=2; L=20300)", cột/sàn "C2 (SL=4)".</summary>
        public string Title => Category == "Dầm"
            ? $"{Label} (SL={Quantity}; L={TotalLength:0})"
            : $"{Label} (SL={Quantity})";

        /// <summary>Chữ ký để gộp cấu kiện giống hệt (cùng tiết diện, cùng bộ thép).</summary>
        public string Signature => Category + "|" + string.Join(";", Bars.OrderBy(b => b.Mark).Select(b => $"{b.Mark}:{b.Key}:{b.Count}"));
    }

    public class ScheduleRow
    {
        public string Element { get; set; }
        public int Mark { get; set; }
        public ShapeKind Shape { get; set; }
        public string ShapeText { get; set; }
        public List<double> Segments { get; set; }
        public int D { get; set; }
        public double Length1 { get; set; }
        public int ElementQty { get; set; }
        public int CountPerElement { get; set; }
        public int CountTotal => CountPerElement * ElementQty;
        public double TotalLengthM => Length1 * CountTotal / 1000;
        public double WeightKg => Tcvn5574.UnitWeight(D) * TotalLengthM;
    }

    /// <summary>Bảng thống kê cốt thép + tổng hợp theo đường kính/nhóm (mẫu Kết cấu Soft).</summary>
    public class RebarSchedule
    {
        public List<ScheduleRow> Rows { get; } = new List<ScheduleRow>();

        /// <summary>Gộp cấu kiện giống hệt (tăng SL) rồi lập bảng.</summary>
        public static List<DetailedElement> MergeIdentical(IEnumerable<DetailedElement> elems)
        {
            var res = new List<DetailedElement>();
            foreach (var g in elems.GroupBy(e => e.Signature))
            {
                var f = g.First();
                f.Quantity = g.Sum(e => e.Quantity);
                foreach (var e in g.Skip(1)) f.SourceIds.AddRange(e.SourceIds);
                res.Add(f);
            }
            return res;
        }

        public static RebarSchedule Build(IEnumerable<DetailedElement> elems)
        {
            var s = new RebarSchedule();
            foreach (var e in elems)
                foreach (var b in e.Bars.OrderBy(x => x.Mark))
                    s.Rows.Add(new ScheduleRow
                    {
                        Element = e.Label,
                        Mark = b.Mark,
                        Shape = b.Shape,
                        ShapeText = b.ShapeText,
                        Segments = b.Segments.ToList(),
                        D = b.D,
                        Length1 = b.Length,
                        ElementQty = e.Quantity,
                        CountPerElement = b.Count,
                    });
            return s;
        }

        public IEnumerable<(int D, double LengthM, double Kg)> ByDiameter()
            => Rows.GroupBy(r => r.D).OrderBy(g => g.Key)
                   .Select(g => (g.Key, g.Sum(r => r.TotalLengthM), g.Sum(r => r.WeightKg)));

        /// <summary>Tổng trọng lượng theo nhóm: Ø≤10, 10&lt;Ø≤18, Ø&gt;18 (kg).</summary>
        public (double G1, double G2, double G3, double Total) ByGroup()
        {
            double g1 = Rows.Where(r => r.D <= 10).Sum(r => r.WeightKg);
            double g2 = Rows.Where(r => r.D > 10 && r.D <= 18).Sum(r => r.WeightKg);
            double g3 = Rows.Where(r => r.D > 18).Sum(r => r.WeightKg);
            return (g1, g2, g3, g1 + g2 + g3);
        }

        /// <summary>Xuất CSV (UTF-8 BOM, dấu ; ) — mở được bằng Excel tiếng Việt.</summary>
        public string ToCsv()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("BẢNG THỐNG KÊ CỐT THÉP");
            sb.AppendLine("TÊN CẤU KIỆN;SỐ HIỆU;HÌNH DẠNG & KÍCH THƯỚC (mm);Ø (mm);CHIỀU DÀI 1 THANH (mm);SỐ C.KIỆN;SỐ THANH MỘT CK;SỐ THANH TOÀN BỘ;TỔNG CHIỀU DÀI (m);TỔNG TRỌNG LƯỢNG (kg)");
            foreach (var r in Rows)
                sb.AppendLine(string.Join(";", r.Element, r.Mark, r.ShapeText, r.D, r.Length1.ToString("0", ci), r.ElementQty,
                    r.CountPerElement, r.CountTotal, r.TotalLengthM.ToString("0.00", ci), r.WeightKg.ToString("0.00", ci)));
            sb.AppendLine();
            sb.AppendLine("TỔNG HỢP CỐT THÉP");
            var byD = ByDiameter().ToList();
            sb.AppendLine("Đường kính (mm);" + string.Join(";", byD.Select(x => "Ø" + x.D)));
            sb.AppendLine("Chiều dài (m);" + string.Join(";", byD.Select(x => x.LengthM.ToString("0.00", ci))));
            sb.AppendLine("Trọng lượng (kg);" + string.Join(";", byD.Select(x => x.Kg.ToString("0.00", ci))));
            var g = ByGroup();
            sb.AppendLine($"NHÓM Ø≤10 (kg);{g.G1.ToString("0.00", ci)}");
            sb.AppendLine($"NHÓM 10<Ø≤18 (kg);{g.G2.ToString("0.00", ci)}");
            sb.AppendLine($"NHÓM Ø>18 (kg);{g.G3.ToString("0.00", ci)}");
            sb.AppendLine($"TỔNG CỘNG (kg);{g.Total.ToString("0.00", ci)}");
            return sb.ToString();
        }
    }
}

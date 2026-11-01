using System;
using System.Collections.Generic;
using System.Linq;

namespace VXT.KetCau.Core.Detailing
{
    /// <summary>
    /// Đánh số hiệu thép theo quy tắc Kết cấu Soft:
    /// - mỗi cấu kiện đánh từ 1 (trừ khi bật đánh liên tục);
    /// - thứ tự: thép dọc (dưới, trên, mũ gối, tăng cường, giá) → đai, đai C, đai lồng, thép rải; trong cùng vai trò trái → phải;
    /// - khác vị trí/vai trò, Ø, chiều dài hoặc hình dạng → khác số; trên & dưới giống hệt vẫn khác số;
    /// - thanh đối xứng (vd mũ gối trái/phải giống nhau) dùng chung số.
    /// </summary>
    public static class BarMarker
    {
        /// <summary>Gộp các thanh trùng khoá, gán số hiệu. Trả về danh sách đã gộp, sắp xếp theo số hiệu.</summary>
        public static List<DetailBar> Assign(IEnumerable<DetailBar> bars, int startMark = 1)
        {
            var merged = new List<DetailBar>();
            foreach (var grp in bars.GroupBy(b => b.Key))
            {
                var first = grp.OrderBy(b => b.OrderX).First();
                var m = new DetailBar
                {
                    Element = first.Element,
                    Role = first.Role,
                    Position = first.Position,
                    D = first.D,
                    Shape = first.Shape,
                    Segments = first.Segments.ToList(),
                    Count = grp.Sum(b => b.Count),
                    Spacing = grp.Where(b => b.Spacing > 0).Select(b => b.Spacing).DefaultIfEmpty(0).Min(),
                    OrderX = first.OrderX,
                    Span = grp.Select(b => b.Span).Distinct().Count() == 1 ? first.Span : -1,
                };
                foreach (var b in grp) m.Placements.AddRange(b.Placements);
                merged.Add(m);
            }
            int mark = startMark;
            foreach (var b in merged.OrderBy(b => (int)b.Role).ThenBy(b => Math.Round(b.OrderX / 10)).ThenByDescending(b => b.D))
                b.Mark = mark++;
            return merged.OrderBy(b => b.Mark).ToList();
        }

        /// <summary>Kiểm tra: mỗi số hiệu chỉ ứng với một bộ (Ø, chiều dài, hình dạng).</summary>
        public static List<string> Validate(IEnumerable<DetailBar> marked)
        {
            var errs = new List<string>();
            foreach (var g in marked.GroupBy(b => (b.Element, b.Mark)))
                if (g.Select(b => b.Key).Distinct().Count() > 1)
                    errs.Add($"{g.Key.Element}: số hiệu {g.Key.Mark} ứng với nhiều loại thanh khác nhau");
            return errs;
        }

        /// <summary>Ký hiệu số trong vòng tròn ①..⑳, quá 20 dùng (21)…</summary>
        public static string Circled(int n)
        {
            if (n >= 1 && n <= 20) return ((char)(0x2460 + n - 1)).ToString();
            if (n >= 21 && n <= 35) return ((char)(0x3251 + n - 21)).ToString();
            if (n >= 36 && n <= 50) return ((char)(0x32B1 + n - 36)).ToString();
            return $"({n})";
        }
    }
}

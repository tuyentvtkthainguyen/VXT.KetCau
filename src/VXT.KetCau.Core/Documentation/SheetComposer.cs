using System;
using System.Collections.Generic;
using System.Linq;
using VXT.KetCau.Core.Drawing;

namespace VXT.KetCau.Core.Documentation
{
    /// <summary>
    /// Xếp các hình vào tờ theo khổ giấy và tỉ lệ, đánh số KT-xx / KC-xx, vẽ khung bản vẽ và khung tên dọc.
    /// Khung giấy vẽ trong không gian mô hình bằng khổ giấy × tỉ lệ.
    /// </summary>
    public class SheetComposer
    {
        public const double MarginLeft = 25, Margin = 10, TitleStripW = 55, Gap = 8;

        static readonly (string Name, double W, double H)[] Papers =
        {
            ("A4", 297, 210), ("A3", 420, 297), ("A2", 594, 420), ("A1", 841, 594), ("A0", 1189, 841),
        };

        readonly DocOptions opt;
        public List<string> Warnings { get; } = new List<string>();

        public SheetComposer(DocOptions o) { opt = o; }

        static double UsableW(double paperW) => paperW - MarginLeft - Margin - TitleStripW - 2 * Gap;
        static double UsableH(double paperH) => paperH - 2 * Margin - 2 * Gap;

        /// <summary>Chọn khổ nhỏ nhất (không nhỏ hơn khổ mặc định) chứa được hình, hoặc khổ lớn nhất nếu không vừa.</summary>
        (string Name, double W, double H) PickPaper(double w, double h, double scale)
        {
            int start = Math.Max(0, Array.FindIndex(Papers, p => p.Name == opt.Paper));
            for (int i = start; i < Papers.Length; i++)
                if (w / scale <= UsableW(Papers[i].W) && h / scale <= UsableH(Papers[i].H)) return Papers[i];
            return Papers[Papers.Length - 1];
        }

        /// <summary>Xếp các hình theo đúng thứ tự vào tờ; hình quá lớn tự nâng khổ.</summary>
        public List<SheetInfo> Pack(IEnumerable<DrawingBlock> blocks)
        {
            var sheets = new List<SheetInfo>();
            foreach (var disc in new[] { "KT", "KC" })
            {
                int no = 0;
                foreach (var group in blocks.Where(b => b.Discipline == disc && !b.Drawing.IsEmpty).GroupBy(b => (b.Group, b.Scale)))
                {
                    SheetInfo cur = null;
                    double x = 0, y = 0, rowH = 0, availW = 0, availH = 0;
                    foreach (var b in group)
                    {
                        double s = b.Scale;
                        double w = b.Drawing.Width, h = b.Drawing.Height;
                        var fit = PickPaper(w, h, s);
                        if (w / s > UsableW(fit.W) || h / s > UsableH(fit.H))
                            Warnings.Add($"Hình thuộc \"{group.Key.Group}\" lớn hơn cả khổ {fit.Name} ở tỉ lệ 1/{s:0} — sẽ tràn khung, cần đổi tỉ lệ.");

                        bool newSheet = cur == null || fit.W > cur.PaperW;
                        if (!newSheet)
                        {
                            if (x + w <= availW + 1e-6) { /* cùng hàng */ }
                            else { x = 0; y += rowH + Gap * s; rowH = 0; }
                            if (y + h > availH + 1e-6) newSheet = true;
                        }
                        if (newSheet)
                        {
                            cur = new SheetInfo { Number = $"{disc}-{++no:00}", Title = group.Key.Group, Discipline = disc, Paper = fit.Name, PaperW = fit.W, PaperH = fit.H, Scale = s, ScaleText = b.ScaleText };
                            sheets.Add(cur);
                            x = 0; y = 0; rowH = 0;
                            availW = UsableW(fit.W) * s; availH = UsableH(fit.H) * s;
                        }
                        // lưu vị trí tương đối từ góc trên trái vùng vẽ; chốt thành toạ độ khi vẽ
                        cur.Items.Add((b, x, y));
                        x += w + Gap * s;
                        rowH = Math.Max(rowH, h);
                    }
                }
            }
            // đánh dấu nhiều tờ cùng tên: (tờ 1/2)
            foreach (var g in sheets.GroupBy(s => (s.Discipline, s.Title)).Where(g => g.Count() > 1))
            {
                int i = 0, n = g.Count();
                foreach (var s in g) s.Title += $" ({++i}/{n})";
            }
            // đặt tờ trong không gian mô hình: mỗi bộ môn một hàng, trái sang phải
            double rowY = 0;
            foreach (var disc in new[] { "KT", "KC" })
            {
                var list = sheets.Where(s => s.Discipline == disc).ToList();
                if (list.Count == 0) continue;
                double maxH = list.Max(s => s.ModelH), cx = 0;
                foreach (var s in list)
                {
                    s.X = cx;
                    s.Y = rowY - s.ModelH;
                    cx += s.ModelW + 40 * s.Scale;
                }
                rowY -= maxH + 60 * list.Max(s => s.Scale);
            }
            return sheets;
        }

        /// <summary>Vẽ khung, khung tên và các hình của một tờ.</summary>
        public void Render(IDraw2D w, SheetInfo s)
        {
            w.SetScaleX(s.Scale);
            DrawFrame(w, s);
            double left = s.X + (MarginLeft + Gap) * s.Scale;
            double top = s.Y + s.ModelH - (Margin + Gap) * s.Scale;
            foreach (var (b, ix, iy) in s.Items)
            {
                // đưa góc trên trái khung bao của hình về vị trí đã xếp
                double dx = left + ix - b.Drawing.MinX;
                double dy = top - iy - b.Drawing.MaxY;
                b.Drawing.Replay(w, dx, dy);
            }
            w.SetScaleX(s.Scale);
        }

        void DrawFrame(IDraw2D w, SheetInfo s)
        {
            double k = s.Scale, X = s.X, Y = s.Y, W = s.ModelW, H = s.ModelH;
            const string L = "KHUNG";
            w.Rect(L, X, Y, X + W, Y + H);
            double fx0 = X + MarginLeft * k, fy0 = Y + Margin * k, fx1 = X + W - Margin * k, fy1 = Y + H - Margin * k;
            w.Rect(L, fx0, fy0, fx1, fy1);
            double sx = fx1 - TitleStripW * k;
            w.Line(L, sx, fy0, sx, fy1);

            // Khung tên dọc bên phải, kê từ dưới lên
            double x = sx, wd = TitleStripW * k, y = fy0;
            string Sign(string n) => string.IsNullOrWhiteSpace(n) ? "" : n;
            void Cell(double hmm, string label, string value, double textMm = 2.5, bool bold = false)
            {
                double h = hmm * k;
                w.Line(L, x, y + h, x + wd, y + h);
                if (!string.IsNullOrEmpty(label)) w.Text("CHU", x + 1.5 * k, y + h - 3.2 * k, 1.9 * k, label, 0, 0, 0);
                if (!string.IsNullOrEmpty(value))
                {
                    double th = textMm * k;
                    var lines = Wrap(value, (int)Math.Max(6, (TitleStripW - 3) / (textMm * 0.6)));
                    double ty = y + (h - (string.IsNullOrEmpty(label) ? 0 : 3.8 * k)) / 2 + (lines.Count - 1) * th * 0.7;
                    foreach (var line in lines)
                    {
                        w.Text("CHU", x + wd / 2, ty, th, line, 1, 2, 0);
                        ty -= th * 1.4;
                    }
                }
                y += h;
            }
            void Pair(string a, string va, string b, string vb, double hmm = 10)
            {
                double h = hmm * k;
                w.Line(L, x, y + h, x + wd, y + h);
                w.Line(L, x + wd / 2, y, x + wd / 2, y + h);
                w.Text("CHU", x + 1.5 * k, y + h - 3.2 * k, 1.9 * k, a, 0, 0, 0);
                w.Text("CHU", x + wd * 0.25, y + 2.4 * k, 2.5 * k, va, 1, 2, 0);
                w.Text("CHU", x + wd / 2 + 1.5 * k, y + h - 3.2 * k, 1.9 * k, b, 0, 0, 0);
                w.Text("CHU", x + wd * 0.75, y + 2.4 * k, 2.5 * k, vb, 1, 2, 0);
                y += h;
            }
            Pair("SỐ HIỆU BẢN VẼ", s.Number, "TỈ LỆ", s.ScaleText);
            Pair("NGÀY", opt.Date ?? "", "GIAI ĐOẠN", ShortStage(opt.Stage));
            Cell(24, "TÊN BẢN VẼ", s.Title, 3.0);
            Cell(12, "KIỂM TRA", Sign(opt.Checker));
            Cell(12, "THIẾT KẾ", Sign(opt.Designer));
            Cell(12, "CHỦ TRÌ", Sign(opt.Director));
            Cell(16, "ĐỊA ĐIỂM", opt.Address);
            Cell(20, "CÔNG TRÌNH", opt.ProjectName, 3.0);
            Cell(14, "CHỦ ĐẦU TƯ", opt.Owner);
            Cell(18, "ĐƠN VỊ THIẾT KẾ", opt.Company, 3.0);
        }

        static string ShortStage(string st)
        {
            if (string.IsNullOrWhiteSpace(st)) return "";
            var t = st.ToLowerInvariant();
            if (t.Contains("thi công")) return "TKTC";
            if (t.Contains("kỹ thuật")) return "TKKT";
            if (t.Contains("cơ sở")) return "TKCS";
            return st.Length <= 8 ? st : st.Substring(0, 8);
        }

        public static List<string> Wrap(string text, int maxChars)
        {
            var res = new List<string>();
            var cur = "";
            foreach (var word in (text ?? "").Split(' ').Where(p => p.Length > 0))
            {
                if (cur.Length > 0 && cur.Length + 1 + word.Length > maxChars) { res.Add(cur); cur = word; }
                else cur = cur.Length == 0 ? word : cur + " " + word;
            }
            if (cur.Length > 0) res.Add(cur);
            return res;
        }
    }

    /// <summary>Bảng đơn giản: tiêu đề và các hàng chữ, căn giữa ô.</summary>
    public static class SimpleTable
    {
        /// <returns>Chiều cao đã vẽ.</returns>
        public static double Draw(IDraw2D w, double ox, double oy, double th, double[] cw, double rowH, IList<string[]> rows, int headerRows = 1)
        {
            double y = oy;
            for (int r = 0; r < rows.Count; r++)
            {
                double h = r < headerRows ? rowH * 1.3 : rowH;
                double x = ox;
                for (int c = 0; c < cw.Length; c++)
                {
                    w.Rect("BANG", x, y - h, x + cw[c], y);
                    if (c < rows[r].Length && !string.IsNullOrEmpty(rows[r][c]))
                        w.Text("CHU", x + cw[c] / 2, y - h / 2, r < headerRows ? th * 0.85 : th, rows[r][c], 1, 2, 0);
                    x += cw[c];
                }
                y -= h;
            }
            return oy - y;
        }
    }
}

using KetCauVXT.Import;
using KetCauVXT.Import.Acad;
using KetCauVXT.Import.Arch;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Liên kết AutoCAD → Việt Const: file trao đổi acad_*.json (add-in KetCauVXT.AcadLink, lệnh VXT_GUI)
/// → AcadLinkReader → ArchDrawing → ArchToStructure (đề xuất + ghi mô hình).</summary>
public static class AcadLinkTests
{
    /// <summary>Dữ liệu mẫu: nhà 5×9 m, 2 tầng vẽ cạnh nhau trong 1 DWG (đơn vị mm), tầng 2 dời 20 m sang phải, có điểm gốc riêng.
    /// Tường 2 nét (220 bao, 110 ngăn), cột: tầng 1 đa tuyến kín, tầng 2 vẽ 4 nét rời; trục có vòng tròn + chữ tên trục;
    /// block cửa đã phân rã (nét + cung, mang tên block); dầm (nét), sàn (đa tuyến kín), tên phòng.</summary>
    public static AcadExchange BuildSample()
    {
        var x = new AcadExchange { Source = "AutoCAD 2025 (mẫu kiểm thử)", Dwg = @"D:\DuAn\NhaMau.dwg", Units = "mm", Created = "2026-11-02T08:00:00" };
        var names = new[] { "Tầng 1", "Tầng 2" };
        for (int k = 0; k < 2; k++)
        {
            double ox = k * 20000;
            var s = new AcadStorey { Name = names[k], Elevation = k == 0 ? 0 : 3.6, Height = k == 0 ? 3.6 : 3.3, BaseX = ox, BaseY = 0, X0 = ox - 4000, Y0 = -4000, X1 = ox + 9000, Y1 = 13000 };
            x.Storeys.Add(s);
            void Add(string cat, string kind, string layer, params double[] xy)
            {
                var it = new AcadItem { Category = cat, Kind = kind, Layer = layer };
                for (int i = 0; i + 1 < xy.Length; i += 2) it.Points.Add(new[] { ox + xy[i], xy[i + 1] });
                s.Items.Add(it);
            }
            void Line(string cat, string layer, double x1, double y1, double x2, double y2) => Add(cat, "Line", layer, x1, y1, x2, y2);
            void Text(string cat, string layer, double px, double py, double h, string t)
            {
                s.Items.Add(new AcadItem { Category = cat, Kind = "Text", Layer = layer, Text = t, Height = h, Points = { new[] { ox + px, py } } });
            }
            // tường 2 nét theo tim, chiều dày t, các khe (lỗ cửa) dọc tường
            void Wall(double x1, double y1, double x2, double y2, double t, params (double a, double b)[] gaps)
            {
                bool hz = Math.Abs(y1 - y2) < 1e-6;
                double a0 = hz ? Math.Min(x1, x2) : Math.Min(y1, y2), a1 = hz ? Math.Max(x1, x2) : Math.Max(y1, y2), c = hz ? y1 : x1;
                var pieces = new List<(double, double)>();
                double cur = a0;
                foreach (var (ga, gb) in gaps.OrderBy(g => g.a)) { if (ga > cur) pieces.Add((cur, ga)); cur = gb; }
                if (a1 > cur) pieces.Add((cur, a1));
                foreach (var (pa, pb) in pieces)
                    foreach (double side in new[] { -t / 2, t / 2 })
                    {
                        if (hz) Line(AcadCategory.Tuong, "KT-TUONG", pa, c + side, pb, c + side);
                        else Line(AcadCategory.Tuong, "KT-TUONG", c + side, pa, c + side, pb);
                    }
            }

            // lưới trục 1, 2 (X) – A, B, C (Y); đầu trục: vòng tròn R400 + chữ
            foreach (var (gx, n) in new[] { (0.0, "1"), (5000.0, "2") })
            {
                Line(AcadCategory.Truc, "KT-TRUC", gx, -2000, gx, 11000);
                s.Items.Add(new AcadItem { Category = AcadCategory.Truc, Kind = "Circle", Layer = "KT-TRUC", Radius = 400, Points = { new[] { ox + gx, 11400.0 } } });
                Text(AcadCategory.Truc, "KT-TRUC", gx, 11400, 350, n);
            }
            foreach (var (gy, n) in new[] { (0.0, "A"), (4500.0, "B"), (9000.0, "C") })
            {
                Line(AcadCategory.Truc, "KT-TRUC", -2000, gy, 7000, gy);
                s.Items.Add(new AcadItem { Category = AcadCategory.Truc, Kind = "Circle", Layer = "KT-TRUC", Radius = 400, Points = { new[] { ox - 2400, gy } } });
                Text(AcadCategory.Truc, "KT-TRUC", -2400, gy, 350, n);
            }
            // tường bao 220, tường ngăn 110 (cửa đi 900 tại x = 3800–4700)
            Wall(0, 0, 5000, 0, 220, k == 0 ? new[] { (800.0, 4200.0) } : new[] { (1800.0, 3200.0) });
            Wall(0, 9000, 5000, 9000, 220);
            Wall(0, 0, 0, 9000, 220);
            Wall(5000, 0, 5000, 9000, 220);
            Wall(110, 4500, 4890, 4500, 110, (3800, 4700));
            // cột 220×220 tại giao trục
            foreach (double cx in new[] { 0.0, 5000.0 })
                foreach (double cy in new[] { 0.0, 4500.0, 9000.0 })
                {
                    if (k == 0)
                        s.Items.Add(new AcadItem
                        {
                            Category = AcadCategory.Cot, Kind = "Polyline", Layer = "KT-COT", Closed = true,
                            Points = { new[] { ox + cx - 110, cy - 110 }, new[] { ox + cx + 110, cy - 110 }, new[] { ox + cx + 110, cy + 110 }, new[] { ox + cx - 110, cy + 110 } },
                        });
                    else
                    {
                        Line(AcadCategory.Cot, "KT-COT", cx - 110, cy - 110, cx + 110, cy - 110);
                        Line(AcadCategory.Cot, "KT-COT", cx + 110, cy - 110, cx + 110, cy + 110);
                        Line(AcadCategory.Cot, "KT-COT", cx + 110, cy + 110, cx - 110, cy + 110);
                        Line(AcadCategory.Cot, "KT-COT", cx - 110, cy + 110, cx - 110, cy - 110);
                    }
                }
            // cửa đi D1 (block đã phân rã: cánh + cung 90°)
            s.Items.Add(new AcadItem { Category = AcadCategory.Cua, Kind = "Line", Layer = "0", Block = "CUA_D1", Points = { new[] { ox + 3800, 4500.0 }, new[] { ox + 3800, 5400.0 } } });
            s.Items.Add(new AcadItem { Category = AcadCategory.Cua, Kind = "Arc", Layer = "0", Block = "CUA_D1", Radius = 900, StartAngle = 0, EndAngle = 90, Points = { new[] { ox + 3800, 4500.0 } } });
            // dầm (nét tim) + sàn (đa tuyến kín)
            Line(AcadCategory.Dam, "KC-DAM", 0, 4500, 5000, 4500);
            Line(AcadCategory.Dam, "KC-DAM", 0, 0, 0, 9000);
            Line(AcadCategory.Dam, "KC-DAM", 5000, 0, 5000, 9000);
            s.Items.Add(new AcadItem { Category = AcadCategory.San, Kind = "Polyline", Layer = "KC-SAN", Closed = true, Points = { new[] { ox, 0.0 }, new[] { ox + 5000, 0.0 }, new[] { ox + 5000, 9000.0 }, new[] { ox, 9000.0 } } });
            // tên phòng
            Text(AcadCategory.TenPhong, "KT-TEXT", 2500, 2200, 250, k == 0 ? "P.KHÁCH" : "P.NGỦ 1");
            Text(AcadCategory.TenPhong, "KT-TEXT", 2500, 6800, 250, k == 0 ? "BẾP + ĂN" : "P.NGỦ 2");
            // đối tượng bỏ qua
            Line(AcadCategory.BoQua, "KT-NOITHAT", 500, 500, 1500, 1500);
        }
        return x;
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Console.WriteLine("AcadLink. Liên kết AutoCAD (đối tượng tích chọn → JSON → bản vẽ kiến trúc → triển khai kết cấu)");
        var sample = BuildSample();
        Directory.CreateDirectory(outDir);
        string path = Path.Combine(outDir, "AcadLink_mau.json");
        sample.Save(path);
        var back = AcadExchange.Load(path);
        check("JSON trao đổi: đọc lại đủ 2 tầng và số đối tượng", back.Storeys.Count * 1000 + back.Storeys[0].Items.Count, 2000 + sample.Storeys[0].Items.Count, 0);

        // ---- độ phình (bulge) và cung ----
        var semi = AcadLinkReader.BulgePoints(new List<(double X, double Y)> { (0, 0), (2, 0) }, new List<double> { 1.0 }, false);
        double len = 0;
        for (int i = 0; i + 1 < semi.Count; i++) len += Math.Sqrt(Math.Pow(semi[i + 1].X - semi[i].X, 2) + Math.Pow(semi[i + 1].Y - semi[i].Y, 2));
        check("Bulge = 1: nửa đường tròn R = 1 (chiều dài ≈ π)", len, Math.PI, 0.005);
        check("Bulge dương (ngược chiều kim đồng hồ) đi phía dưới dây cung: y min = −1", semi.Min(p => p.Y), -1, 1e-6);
        var quarter = AcadLinkReader.BulgePoints(new List<(double X, double Y)> { (1, 0), (0, 1) }, new List<double> { Math.Tan(Math.PI / 8) }, false);
        check("Bulge tan(22,5°): cung 1/4 tâm gốc toạ độ, mọi điểm cách tâm 1", quarter.Max(p => Math.Abs(Math.Sqrt(p.X * p.X + p.Y * p.Y) - 1)), 0, 1e-9);
        var st = new AcadStorey { Name = "thử", BaseX = 1000, BaseY = 2000 };
        st.Items.Add(new AcadItem { Category = "Cua", Kind = "Arc", Radius = 900, StartAngle = 0, EndAngle = 90, Points = { new[] { 1000.0, 2000.0 } } });
        st.Items.Add(new AcadItem { Category = "Tuong", Kind = "Polyline", Points = { new[] { 1000.0, 2000.0 }, new[] { 3000.0, 2000.0 } }, Bulges = new List<double> { 1, 0 } });
        var ents = AcadLinkReader.ToEntities(st, AcadLinkReader.UnitScale("mm"));
        var arc = ents[0];
        check("Cung R900 mm → m, dời về điểm gốc: điểm cuối (0; 0,9)", Math.Abs(arc.Points[^1].X) + Math.Abs(arc.Points[^1].Y - 0.9), 0, 1e-9);
        check("Đa tuyến có bulge: điểm xa nhất cách dây cung 1,0 m", ents[1].Points.Min(p => p.Y), -1.0, 1e-6);

        // ---- đọc → ArchDrawing ----
        var ao = new ArchImportOptions();
        var res = AcadLinkReader.Convert(back, ao);
        var dwg = res.Drawing;
        foreach (var l in dwg.Info) Console.WriteLine("   · " + l);
        foreach (var w in dwg.Warnings) Console.WriteLine("   ! " + w);
        foreach (var f in dwg.Floors) foreach (var n in f.Notes) Console.WriteLine($"   ! [{f.Title}] {n}");
        check("Số tầng", dwg.Floors.Count, 2, 0);
        var f1 = dwg.Floors.First(f => f.FloorNo == 1);
        var f2 = dwg.Floors.First(f => f.FloorNo == 2);
        check("Tên tầng lấy theo khai báo VXT_TANG", f1.Title == "Tầng 1" && f2.Title == "Tầng 2" ? 1 : 0, 1, 0);
        check("Chiều cao tầng 1 = 3,6 m", f1.Height, 3.6, 1e-9);
        check("Chiều cao tầng 2 = 3,3 m", f2.Height, 3.3, 1e-9);
        check("Tầng 1: 2 trục X (1, 2)", f1.Grids.Count(g => g.Dir == GridDir.X && (g.Name == "1" || g.Name == "2")), 2, 0);
        check("Tầng 1: 3 trục Y (A, B, C)", f1.Grids.Count(g => g.Dir == GridDir.Y && "ABC".Contains(g.Name)), 3, 0);
        check("Đổi mm → m: trục 2 tại x = 5,0 m", f1.Grids.First(g => g.Name == "2").Coord, 5.0, 1e-6);
        check("Tầng 2 (vẽ lệch 20 m) dời về điểm gốc: trục 1 tại x = 0", f2.Grids.First(g => g.Name == "1").Coord, 0, 1e-6);
        check("Tầng 2: trục C tại y = 9,0 m", f2.Grids.First(g => g.Name == "C").Coord, 9.0, 1e-6);
        check("Tầng 1: 6 cột (đa tuyến kín)", f1.Columns.Count, 6, 0);
        check("Tầng 2: 6 cột (ghép từ 4 nét rời)", f2.Columns.Count, 6, 0);
        check("Kích thước cột 0,22 m", f2.Columns.Max(c => Math.Abs(c.Bx - 0.22) + Math.Abs(c.By - 0.22)), 0, 1e-6);
        check("Tầng 1 có tường 220 và 110", (f1.Walls.Any(w => Math.Abs(w.Thickness - 0.22) < 0.005) ? 1 : 0) + (f1.Walls.Any(w => Math.Abs(w.Thickness - 0.11) < 0.005) ? 1 : 0), 2, 0);
        check("Tầng 2: tường bao dài nhất ≈ 9 m", f2.Walls.Max(w => w.Length), 9.0, 0.02);
        check("Tầng 1: nhận cửa đi (block CUA_D1)", f1.Openings.Count(o => o.Kind == "Cửa đi") >= 1 ? 1 : 0, 1, 0);
        check("Tầng 1: 2 phòng (khách, bếp)", f1.Rooms.Count(r => r.Kind is RoomKind.Living or RoomKind.Kitchen), 2, 0);
        check("Tầng 2: 2 phòng ngủ", f2.Rooms.Count(r => r.Kind == RoomKind.Bedroom), 2, 0);
        check("Đếm đối tượng nhóm Cột: 6 đa tuyến (tầng 1) + 24 nét (tầng 2)", res.Counts[AcadCategory.Cot], 6 + 24, 0);
        check("Dầm người dùng chỉ định: 3 đoạn/tầng", res.Beams.Count, 6, 0);
        check("Dầm tầng 2 dời về gốc: x ≤ 5 m", res.Beams.Where(b => b.Storey == "Tầng 2").Max(b => Math.Max(b.Ax, b.Bx)), 5.0, 1e-6);
        check("Sàn: 1 vùng/tầng, diện tích 45 m²", res.Slabs.Count * 100 + Math.Abs(ImportGeometry.SignedArea(res.Slabs[0].Outline)), 245, 1e-6);

        // ---- triển khai kết cấu ----
        var prop = ArchToStructure.Propose(dwg, ao);
        foreach (var w in prop.Warnings) Console.WriteLine("   ! " + w);
        Console.WriteLine("   Cao trình: " + string.Join(", ", prop.Levels.Select(l => $"{l.Name} +{l.Elevation:0.000} ({l.Beams.Count} dầm, {l.Slabs.Count} ô sàn)")));
        check("Cao trình T2 (+3,6) và MAI (+6,9)", prop.Levels.Count == 2 && Math.Abs(prop.Levels[0].Elevation - 3.6) < 1e-6 && Math.Abs(prop.Levels[1].Elevation - 6.9) < 1e-6 ? 1 : 0, 1, 0);
        check("6 cột tại giao trục", prop.Columns.Count, 6, 0);
        var p = ModelBuilder.NewProject("Từ AutoCAD");
        var rep = ArchToStructure.Apply(p, dwg, prop, ao);
        Console.WriteLine("   " + rep.ToString().Replace("\n", "\n   "));
        check("Mô hình: 5 trục, 2 tầng", p.Grids.Count * 10 + p.Stories.Count, 52, 0);
        check("Mô hình có dầm và sàn", (p.Frames.Any(f => !f.IsVerticalKind) ? 1 : 0) + (p.Slabs.Count > 0 ? 1 : 0), 2, 0);

        // ---- dữ liệu không hợp lệ ----
        var empty = AcadLinkReader.Convert(new AcadExchange(), ao);
        check("File không có tầng → cảnh báo, không lỗi", empty.Drawing.Floors.Count == 0 && empty.Drawing.Warnings.Count == 1 ? 1 : 0, 1, 0);
    }
}

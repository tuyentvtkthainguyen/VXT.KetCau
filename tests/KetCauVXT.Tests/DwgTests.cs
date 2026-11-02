using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CSMath;
using KetCauVXT.Dwg;
using KetCauVXT.Import;
using KetCauVXT.Import.Arch;

namespace KetCauVXT.Tests;

/// <summary>Đọc bản vẽ DWG (thư viện KetCauVXT.Dwg / ACadSharp): ghi DWG mẫu bằng DwgWriter của ACadSharp → đọc lại bằng DwgReader,
/// qua điểm cắm CadFileReaders.Dwg (ArchPlanReader, DwgConverter.ReadAny), so với đường DXF; DXF của DxfWriter (lõi) → DWG → đọc lại;
/// đơn vị, phiên bản cũ, file hỏng.
/// File DWG ghi vào outDir\DocDWG; file DXF trung gian ghi vào thư mục tạm (không để trong outDir vì bước CI kiểm tra mọi *.dxf bằng ezdxf).</summary>
public static class DwgTests
{
    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir ?? Path.GetTempPath(), "DocDWG");
        string tmp = Path.Combine(Path.GetTempPath(), "kcvxt_dwg_test");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(tmp);
        var prevHook = CadFileReaders.Dwg;
        DwgReader.Register();
        try
        {
            Main(check, dir, tmp);
        }
        catch (Exception ex)
        {
            Console.WriteLine("   LOI DwgTests: " + ex);
            check("DWG: chạy hết bộ kiểm thử không ngoại lệ", 0, 1, 0);
        }
        finally
        {
            CadFileReaders.Dwg = prevHook ?? CadFileReaders.Dwg;
        }
    }

    private static int B(bool b) => b ? 1 : 0;

    private static bool Near(double a, double b, double tol = 1e-6) => Math.Abs(a - b) <= tol;

    /// <summary>Bản vẽ kiểm thử: bản vẽ mẫu (lớp TUONG, TRUC, COT, CUA, TEXT) + đa tuyến cung lồi, MTEXT định dạng,
    /// block cửa chèn xoay 90° phóng 2 lần, lớp đóng băng, lớp tắt, miền HATCH.</summary>
    public static CadDocument BuildTestDoc(ACadVersion version = ACadVersion.AC1032)
    {
        var doc = DwgSample.BuildHouse("", version);
        Layer Lay(string name, short aci)
        {
            var l = new Layer(name) { Color = new Color(aci) };
            doc.Layers.Add(l);
            return l;
        }
        var lancan = Lay("LANCAN", 3);
        var an = Lay("AN", 5); an.Flags = LayerFlags.Frozen;
        var tat = Lay("TAT", 6); tat.IsOn = false;
        doc.Layers.TryGetValue("TEXT", out var text);
        doc.Layers.TryGetValue("CUA", out var cua);
        doc.Layers.TryGetValue("COT", out var cot);

        // đa tuyến nửa cung tròn: (0,-3000) → (2000,-3000), bulge 1 (ngược chiều kim đồng hồ, đi qua (1000,-4000))
        var arcPl = new LwPolyline { Layer = lancan };
        arcPl.Vertices.Add(new LwPolyline.Vertex(new XY(0, -3000)) { Bulge = 1 });
        arcPl.Vertices.Add(new LwPolyline.Vertex(new XY(2000, -3000)));
        doc.Entities.Add(arcPl);

        doc.Entities.Add(new Line { StartPoint = new XYZ(0, 0, 0), EndPoint = new XYZ(1000, 1000, 0), Layer = an });
        doc.Entities.Add(new Line { StartPoint = new XYZ(0, 0, 0), EndPoint = new XYZ(1000, -1000, 0), Layer = tat });

        doc.Entities.Add(new MText { Value = "{\\fArial|b1|i0|c0|p34;PHÒNG NGỦ}\\P\\H250;tầng 1", InsertPoint = new XYZ(1800, 7000, 0), Height = 300, Layer = text });

        // block cửa chèn xoay 90°, phóng 2 lần tại (20000, 20000)
        var blk = DwgSample.DoorBlock(doc);
        doc.Entities.Add(new Insert(blk) { InsertPoint = new XYZ(20000, 20000, 0), Rotation = Math.PI / 2, XScale = 2, YScale = 2, Layer = cua });

        // miền tô HATCH (cột tô đặc tại giao trục 2-C)
        var h = new Hatch { Pattern = HatchPattern.Solid, IsSolid = true, PatternType = HatchPatternType.SolidFill, Layer = cot };
        var path = new Hatch.BoundaryPath { Flags = BoundaryPathFlags.External };
        path.Edges.Add(new Hatch.BoundaryPath.Polyline(new[] { new XYZ(4390, 7890, 0), new XYZ(4610, 7890, 0), new XYZ(4610, 8110, 0), new XYZ(4390, 8110, 0) }));
        h.Paths.Add(path);
        doc.Entities.Add(h);
        return doc;
    }

    private static void Main(Action<string, double, double, double> check, string dir, string tmp)
    {
        // ------------------------------------------------------------ 1. ghi DWG 2018 + đọc lại
        string dwg = Path.Combine(dir, "MauKiemThu_2018.dwg");
        DwgReader.WriteDwg(BuildTestDoc(), dwg);
        check("DWG: ghi file mẫu bằng ACadSharp (AC1032)", B(File.Exists(dwg) && new FileInfo(dwg).Length > 1000), 1, 0);
        check("DWG: chữ ký đầu file AC1032", B(DwgReader.ReadSignature(dwg) == "AC1032"), 1, 0);

        var r = DwgReader.Read(dwg);
        Console.WriteLine("   · " + DwgReader.Describe(r, Path.GetFileName(dwg)).Replace("\n", "\n   "));
        check("DWG: $INSUNITS = 4 (mm)", r.InsUnits, 4, 0);
        check("DWG: hệ số đơn vị mm → m", r.Scale, 0.001, 1e-12);
        check("DWG: phiên bản AC1032", B(r.AcadVersion == "AC1032"), 1, 0);
        var ents = r.Entities;
        int On(string layer) => ents.Count(e => e.Layer.Equals(layer, StringComparison.OrdinalIgnoreCase));
        check("DWG: 12 nét tường trên lớp TUONG", On("TUONG"), 12, 0);
        check("DWG: lớp TRUC = 5 đường + 5 vòng tròn + 5 chữ", On("TRUC"), 15, 0);
        check("DWG: 5 vòng tròn đầu trục R400 = 0,4 m", ents.Count(e => e.Layer == "TRUC" && e.Type == DxfEntityType.Circle && Near(e.Radius, 0.4)), 5, 0);
        check("DWG: 6 cột đa tuyến kín 4 đỉnh (COT)", ents.Count(e => e.Layer == "COT" && e.Type == DxfEntityType.Polyline && e.Closed && e.Points.Count == 4), 6, 0);
        check("DWG: tường trục A mép ngoài (0;-0,11)→(4,5;-0,11) m",
            B(ents.Any(e => e.Layer == "TUONG" && e.Type == DxfEntityType.Line && Near(e.Points[0].X, 0) && Near(e.Points[0].Y, -0.11) && Near(e.Points[1].X, 4.5) && Near(e.Points[1].Y, -0.11))), 1, 0);
        var door = ents.Where(e => e.Layer == "CUA").ToList();
        check("DWG: block cửa phân rã (2 lần chèn × cánh + cung + tay nắm lồng)", door.Count, 6, 0);
        check("DWG: thực thể block mang tên CUA_D1, lớp 0 lấy lớp INSERT", door.Count(e => e.Block == "CUA_D1"), 6, 0);
        var leaf = door.FirstOrDefault(e => e.SourceType == "LINE" && Near(e.Points[0].X, 20) && Near(e.Points[0].Y, 20));
        check("DWG: cánh cửa xoay 90°, ×2 → đầu (18,2; 20) m", B(leaf != null && Near(leaf.Points[1].X, 18.2, 1e-6) && Near(leaf.Points[1].Y, 20, 1e-6)), 1, 0);
        var knob = door.FirstOrDefault(e => e.Type == DxfEntityType.Circle && Near(e.Points[0].X, 18.3, 1e-6) && Near(e.Points[0].Y, 20, 1e-6));
        check("DWG: block lồng TAYNAM qua 2 cấp biến đổi (tâm 18,3; 20, R = 0,05 m)", knob != null ? knob.Radius : -1, 0.05, 1e-6);
        var arc = door.FirstOrDefault(e => e.SourceType == "ARC" && Near(e.Points[0].X, 3.2 + 0.9, 1e-6));
        check("DWG: cung cửa R900 0→90° chia 6 đoạn (7 điểm)", arc?.Points.Count ?? 0, 7, 0);
        check("DWG: lớp đóng băng AN bị bỏ", On("AN"), 0, 0);
        check("DWG: lớp đóng băng ghi nhận", B(r.FrozenLayers.Contains("AN")), 1, 0);
        check("DWG: lớp tắt TAT vẫn đọc + ghi nhận OffLayers", On("TAT") + B(r.OffLayers.Contains("TAT")), 2, 0);
        check("DWG: màu lớp TRUC = 1", r.LayerColors.TryGetValue("TRUC", out int ctr) ? ctr : -1, 1, 0);
        var bulge = ents.FirstOrDefault(e => e.Layer == "LANCAN");
        check("DWG: đa tuyến cung lồi (bulge 1) → 13 điểm", bulge?.Points.Count ?? 0, 13, 0);
        check("DWG: điểm giữa cung lồi (1; -4) m", B(bulge != null && bulge.Points.Count == 13 && Near(bulge.Points[6].X, 1.0, 1e-6) && Near(bulge.Points[6].Y, -4.0, 1e-6)), 1, 0);
        var texts = ents.Where(e => e.Type == DxfEntityType.Text).Select(e => e.Text).ToList();
        check("DWG: chữ tiếng Việt TEXT \"P.KHÁCH\", \"MẶT BẰNG TẦNG 1\"", B(texts.Contains("P.KHÁCH") && texts.Contains("MẶT BẰNG TẦNG 1")), 1, 0);
        check("DWG: MTEXT bỏ mã định dạng → \"PHÒNG NGỦ tầng 1\"", B(texts.Contains("PHÒNG NGỦ tầng 1")), 1, 0);
        check("DWG: tên trục 1, 2, A, B, C", texts.Count(t => t is "1" or "2" or "A" or "B" or "C"), 5, 0);
        check("DWG: HATCH → 1 miền tô 4 đỉnh (FillEntities)", r.FillEntities.Count(e => e.SourceType == "HATCH" && e.Points.Count == 4), 1, 0);
        check("DWG: chỉ model space, không có DIMENSION/ATTDEF", B(ents.All(e => e.SourceType is "LINE" or "LWPOLYLINE" or "POLYLINE" or "ARC" or "CIRCLE" or "TEXT" or "MTEXT" or "ATTRIB")), 1, 0);
        var list = DwgReader.ReadEntities(dwg, out string info, out double u2m);
        check("DWG: ReadEntities trả cùng số thực thể + hệ số đơn vị", list.Count + u2m, ents.Count + 0.001, 1e-9);
        check("DWG: thông tin đọc có $INSUNITS", B(info.Contains("$INSUNITS = 4")), 1, 0);

        // ------------------------------------------------------------ 2. so với đường DXF (ACadSharp ghi DXF → bộ đọc DXF của lõi)
        string dxf = Path.Combine(tmp, "MauKiemThu_2018.dxf");
        DwgReader.ConvertToDxf(dwg, dxf);
        var rd = KetCauVXT.Import.DxfReader.Read(dxf);
        check("DWG↔DXF: cùng số thực thể", rd.Entities.Count, ents.Count, 0);
        check("DWG↔DXF: cùng số miền tô", rd.FillEntities.Count, r.FillEntities.Count, 0);
        string LayerSig(KetCauVXT.Import.DxfReader x) => string.Join(",", x.Entities.GroupBy(e => e.Layer.ToUpperInvariant()).OrderBy(g => g.Key).Select(g => g.Key + ":" + g.Count()));
        Console.WriteLine("   · DWG: " + LayerSig(r) + "\n   · DXF: " + LayerSig(rd));
        check("DWG↔DXF: cùng số thực thể từng lớp", B(LayerSig(r) == LayerSig(rd)), 1, 0);
        double maxDev = 0;
        foreach (var e in ents)
        {
            var m = rd.Entities.Where(x => x.Layer == e.Layer && x.Type == e.Type && x.Points.Count == e.Points.Count && x.Text == e.Text)
                .Select(x => x.Points.Zip(e.Points, (p, q) => Math.Max(Math.Abs(p.X - q.X), Math.Abs(p.Y - q.Y))).DefaultIfEmpty(0).Max())
                .DefaultIfEmpty(double.MaxValue).Min();
            maxDev = Math.Max(maxDev, m);
        }
        check("DWG↔DXF: mọi thực thể có bản trùng toạ độ (sai lệch lớn nhất, m)", maxDev, 0, 1e-6);

        // ------------------------------------------------------------ 3. bản vẽ kiến trúc qua điểm cắm (ArchPlanReader, DwgConverter.ReadAny)
        var viaHook = DwgConverter.ReadAny(dwg, 0, out string hookErr);
        check("DWG: DwgConverter.ReadAny dùng bộ đọc tích hợp", B(viaHook != null && viaHook.Entities.Count == ents.Count), 1, 0);
        if (hookErr != null) Console.WriteLine("   ! " + hookErr);
        var ao = new ArchImportOptions();
        var archDwg = ArchPlanReader.Read(dwg, ao);
        var archDxf = ArchPlanReader.Read(dxf, new ArchImportOptions());
        foreach (var wn in archDwg.Warnings) Console.WriteLine("   ! " + wn);
        Console.WriteLine("   · DWG: " + string.Join(" | ", archDwg.Floors.Select(f => f.ToString())));
        Console.WriteLine("   · DXF: " + string.Join(" | ", archDxf.Floors.Select(f => f.ToString())));
        check("DWG → ArchPlanReader: 1 mặt bằng", archDwg.Floors.Count, 1, 0);
        var f1 = archDwg.Floors.FirstOrDefault();
        check("DWG → ArchPlanReader: có tường", B(f1 != null && f1.Walls.Count > 0), 1, 0);
        check("DWG → ArchPlanReader: tường 220 và 110", B(f1 != null && f1.Walls.Any(wl => Math.Abs(wl.Thickness - 0.22) < 0.005)) + B(f1 != null && f1.Walls.Any(wl => Math.Abs(wl.Thickness - 0.11) < 0.005)), 2, 0);
        check("DWG → ArchPlanReader: trục 1, 2 (X)", f1?.Grids.Count(g => g.Dir == KetCauVXT.Model.GridDir.X && (g.Name == "1" || g.Name == "2")) ?? 0, 2, 0);
        check("DWG → ArchPlanReader: trục A, B, C (Y)", f1?.Grids.Count(g => g.Dir == KetCauVXT.Model.GridDir.Y && "ABC".Contains(g.Name) && g.Name.Length == 1) ?? 0, 3, 0);
        var d1 = archDxf.Floors.FirstOrDefault();
        check("DWG = DXF: cùng số tường / trục / cột / phòng",
            B(f1 != null && d1 != null && f1.Walls.Count == d1.Walls.Count && f1.Grids.Count == d1.Grids.Count && f1.Columns.Count == d1.Columns.Count && f1.Rooms.Count == d1.Rooms.Count), 1, 0);
        var scan = CadLayerScan.Scan(new[] { dwg }, new ArchImportOptions());
        check("DWG → quét layer (bảng \"Layer trong file\"): có TUONG, TRUC, COT, CUA",
            new[] { "TUONG", "TRUC", "COT", "CUA" }.Count(n => scan.Layers.Any(l => l.Name.Equals(n, StringComparison.OrdinalIgnoreCase))), 4, 0);

        // ------------------------------------------------------------ 4. đơn vị m, ghi đè hệ số
        var dm = new CadDocument();
        dm.Header.InsUnits = UnitsType.Meters;
        dm.Entities.Add(new Line { StartPoint = new XYZ(0, 0, 0), EndPoint = new XYZ(5, 0, 0) });
        string dwgM = Path.Combine(dir, "DonViMet.dwg");
        DwgReader.WriteDwg(dm, dwgM);
        var rm = DwgReader.Read(dwgM);
        check("DWG đơn vị m: $INSUNITS = 6, nét dài 5 m", rm.InsUnits * 10 + (rm.Entities.Count == 1 ? rm.Entities[0].Points[1].X : -1), 65, 1e-9);
        var rc = DwgReader.Read(dwgM, 0.01);
        check("DWG ghi đè hệ số (cm): 5 → 0,05 m", rc.Entities.Count == 1 ? rc.Entities[0].Points[1].X : -1, 0.05, 1e-9);

        // ------------------------------------------------------------ 5. phiên bản cũ (AutoCAD 2004, 2000)
        foreach (var (ver, sig) in new[] { (ACadVersion.AC1018, "AC1018"), (ACadVersion.AC1015, "AC1015") })
        {
            string p = Path.Combine(dir, $"MauKiemThu_{sig}.dwg");
            try { DwgReader.WriteDwg(BuildTestDoc(ver), p); }
            catch (Exception ex) { Console.WriteLine($"   · ACadSharp không ghi được DWG {sig} ({ex.GetType().Name}: {ex.Message}) – bỏ qua phiên bản này"); continue; }
            try
            {
                var rv = DwgReader.Read(p);
                check($"DWG {sig}: chữ ký + 12 nét tường + 6 cột + 6 nét cửa",
                    B(rv.AcadVersion == sig) + rv.Entities.Count(e => e.Layer == "TUONG") + rv.Entities.Count(e => e.Layer == "COT") + rv.Entities.Count(e => e.Layer == "CUA"), 25, 0);
            }
            catch (Exception ex) { Console.WriteLine($"   ! DWG {sig}: {ex.Message}"); check($"DWG {sig}: đọc được", 0, 1, 0); }
        }

        // ------------------------------------------------------------ 6. file hỏng / không hỗ trợ → thông báo rõ ràng
        string ErrOf(string path)
        {
            string err = null; DxfReader res = null;
            var t = Task.Run(() => res = DwgConverter.ReadAny(path, 0, out err));
            if (!t.Wait(TimeSpan.FromSeconds(60))) return "TREO";
            return res == null ? err ?? "" : "ĐỌC ĐƯỢC";
        }
        string cut = Path.Combine(tmp, "Hong.dwg");
        var bytes = File.ReadAllBytes(dwg);
        File.WriteAllBytes(cut, bytes.Take(Math.Min(2048, bytes.Length / 3)).ToArray());
        string e1 = ErrOf(cut);
        Console.WriteLine("   · file cắt cụt: " + e1);
        check("DWG hỏng (cắt cụt): báo \"Không đọc được DWG … lưu sang DXF\"", B(e1.StartsWith("Không đọc được DWG") && e1.Contains("DXF")), 1, 0);
        string old = Path.Combine(tmp, "Cu_R12.dwg");
        File.WriteAllBytes(old, System.Text.Encoding.ASCII.GetBytes("AC1009" + new string('\0', 200)));
        string e2 = ErrOf(old);
        Console.WriteLine("   · R12: " + e2);
        check("DWG R12 (AC1009): báo phiên bản quá cũ", B(e2.Contains("quá cũ")), 1, 0);
        string fake = Path.Combine(tmp, "KhongPhaiDwg.dwg");
        File.WriteAllText(fake, "day khong phai ban ve");
        string e3 = ErrOf(fake);
        check("File .dwg không phải DWG: báo lỗi", B(e3.StartsWith("Không đọc được DWG")), 1, 0);

        // ------------------------------------------------------------ 7. DXF do DxfWriter của lõi xuất → ACadSharp chuyển DWG → đọc lại
        var w = new KetCauVXT.Drawing.DxfWriter(50);
        w.Line("TUONG", 0, 0, 5000, 0);
        w.Line("TUONG", 0, 220, 5000, 220);
        w.Rect("COT", -110, -110, 110, 110);
        w.Circle("TRUC", 0, 6000, 400);
        w.Arc("CUA", 1000, 220, 900, 0, 90);
        w.Text("TRUC", 0, 6000, 350, "Trục Ⓐ – tầng 1", 0, 1, 2);
        w.Text("TEXT", 2000, 3000, 300, "PHÒNG KHÁCH");
        string ownDxf = Path.Combine(tmp, "TuDxfWriter.dxf");
        w.Save(ownDxf);
        string ownDwg = Path.Combine(dir, "TuDxfWriter.dwg");
        string how = DwgReader.ConvertDxfToDwg(ownDxf, ownDwg);
        Console.WriteLine("   · DxfWriter → DWG: " + how);
        var ra = KetCauVXT.Import.DxfReader.Read(ownDxf);
        var rb = DwgReader.Read(ownDwg);
        check("DxfWriter → DWG: cùng số thực thể", rb.Entities.Count, ra.Entities.Count, 0);
        check("DxfWriter → DWG: cùng tập lớp", B(string.Join(",", ra.Entities.Select(e => e.Layer.ToUpperInvariant()).Distinct().OrderBy(x => x)) ==
                                               string.Join(",", rb.Entities.Select(e => e.Layer.ToUpperInvariant()).Distinct().OrderBy(x => x))), 1, 0);
        var ta = ra.Entities.Where(e => e.Type == DxfEntityType.Text).Select(e => e.Text).OrderBy(x => x).ToList();
        var tb = rb.Entities.Where(e => e.Type == DxfEntityType.Text).Select(e => e.Text).OrderBy(x => x).ToList();
        Console.WriteLine("   · chữ DXF: " + string.Join(" | ", ta) + "  ↔  DWG: " + string.Join(" | ", tb));
        check("DxfWriter → DWG: chữ tiếng Việt giữ nguyên", B(ta.SequenceEqual(tb) && tb.Contains("PHÒNG KHÁCH")), 1, 0);
        var lb = rb.Entities.FirstOrDefault(e => e.Type == DxfEntityType.Line);
        check("DxfWriter → DWG: toạ độ nét tường (5 m)", lb != null ? Math.Max(lb.Points[0].X, lb.Points[1].X) : -1, 5.0, 1e-9);
    }
}

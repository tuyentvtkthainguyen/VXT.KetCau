using KetCauVXT.Detailing;

namespace KetCauVXT.Tests;

/// <summary>Lấy kích thước từ AutoCAD (lệnh VXT_KT → %AppData%\KetCauVXT\AcadPick\pick_*.json) cho chế độ Triển khai hồ sơ:
/// đọc JSON, gán vào cấu kiện, tạo cấu kiện mới, file mới nhất, hình học (nhịp, lưới trục, lỗ).</summary>
public static class AcadPickTests
{
    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ---- dầm ----
        var dam = AcadPick.Parse("{\"version\":1,\"kind\":\"Dam\",\"unit\":\"m\",\"items\":[{\"name\":\"D5\",\"b\":0.22,\"h\":0.45,\"spans\":[4.2,3.6],\"columns\":[{\"name\":\"C1\",\"b\":0.25}]}]}");
        check("AcadPick dầm: đọc 1 đối tượng, không lỗi", dam.Items.Count * 10 + dam.Errors.Count, 10, 0);
        check("AcadPick dầm: loại Dầm", B(dam.KindTitle == "Dầm"), 1, 0);
        var beam = new ManualBeam();
        var log = AcadPick.ApplyTo(beam, dam.Items[0]);
        check("AcadPick dầm: b = 220 mm", beam.B, 0.22, 1e-12);
        check("AcadPick dầm: h = 450 mm", beam.H, 0.45, 1e-12);
        check("AcadPick dầm: chuỗi nhịp \"4,2; 3,6\" đọc lại đúng 2 nhịp", DetailShim.ParseList(beam.Spans).Sum(), 7.8, 1e-12);
        check("AcadPick dầm: bề rộng gối = cột 250", beam.SupportWidth, 0.25, 1e-12);
        check("AcadPick dầm: đổi tên D5 + có nhật ký", B(beam.Name == "D5" && log.Count >= 4), 1, 0);

        // ---- đơn vị mm trong file ----
        var mm = AcadPick.Parse("{\"kind\":\"Cot\",\"unit\":\"mm\",\"items\":[{\"name\":\"C3\",\"b\":220,\"h\":300}]}");
        var col = new ManualColumn();
        AcadPick.ApplyTo(col, mm.Items[0]);
        check("AcadPick cột (file đơn vị mm): b = 0,22 m", col.B, 0.22, 1e-12);
        check("AcadPick cột: h = 0,30 m", col.H, 0.30, 1e-12);

        // ---- móng đơn: phân biệt "L"/"l", "B"/"b" ----
        var mong = AcadPick.Parse("{\"kind\":\"Mong\",\"unit\":\"m\",\"items\":[{\"name\":\"M2\",\"L\":2.0,\"B\":1.6,\"colL\":0.22,\"colB\":0.3,\"colX\":-0.5,\"colY\":0}]}");
        var fo = new ManualFooting();
        AcadPick.ApplyTo(fo, mong.Items[0]);
        check("AcadPick móng: L = 2,0 m", fo.L, 2.0, 1e-12);
        check("AcadPick móng: B = 1,6 m", fo.B, 1.6, 1e-12);
        check("AcadPick móng: cột 220 × 300", fo.ColL * 10 + fo.ColB, 0.22 * 10 + 0.30, 1e-12);
        check("AcadPick móng: lệch tâm cột X đọc được", mong.Items[0].ColX ?? 0, -0.5, 1e-12);

        // ---- móng băng: giữ nội lực đã nhập theo thứ tự cột ----
        var mb = AcadPick.Parse("{\"kind\":\"MongBang\",\"items\":[{\"length\":13.0,\"columns\":[{\"name\":\"A\",\"x\":0.5,\"b\":0.22},{\"name\":\"B\",\"x\":6.5,\"b\":0.3},{\"name\":\"C\",\"x\":12.5,\"b\":0.22}]}]}");
        var st = new ManualStrip();
        double n0 = st.Columns[0].N;
        AcadPick.ApplyTo(st, mb.Items[0]);
        check("AcadPick móng băng: chiều dài 13 m", st.Length, 13.0, 1e-12);
        check("AcadPick móng băng: 3 cột", st.Columns.Count, 3, 0);
        check("AcadPick móng băng: cột giữa x = 6,5, cạnh 300", st.Columns[1].X + st.Columns[1].Bc, 6.8, 1e-12);
        check("AcadPick móng băng: giữ N cột thứ nhất", st.Columns[0].N, n0, 1e-12);

        // ---- sàn ----
        var san = AcadPick.Parse("{\"kind\":\"San\",\"items\":[{\"name\":\"SMB1\",\"spansX\":[3.6,4.2],\"spansY\":[4.5],\"openings\":[{\"name\":\"L1\",\"x\":1.0,\"y\":1.2,\"w\":0.8,\"h\":0.6}]}]}");
        check("AcadPick sàn: 2 nhịp X, 1 nhịp Y, 1 lỗ", san.Items[0].SpansX.Count * 100 + san.Items[0].SpansY.Count * 10 + san.Items[0].Openings.Count, 211, 0);
        check("AcadPick sàn: lỗ rộng 0,8 m", san.Items[0].Openings[0].W, 0.8, 1e-12);
        var slab = new ManualSlab();
        AcadPick.ApplyTo(slab, san.Items[0]);
        check("AcadPick sàn (ô đơn): Lx = nhịp X đầu", slab.Lx, 3.6, 1e-12);
        var created = AcadPick.CreateMembers(san);
        bool hasPlan = AcadPick.SlabPlanType != null;
        check("AcadPick sàn: tạo cấu kiện (sàn mặt bằng 1, hoặc 2 ô sàn đơn)", created.Count, hasPlan ? 1 : 2, 0);
        if (hasPlan)
        {
            var p = created[0].GetType().GetProperty("SpansX");
            check("AcadPick sàn mặt bằng: SpansX gán \"3,6; 4,2\"", DetailShim.ParseList(p?.GetValue(created[0]) as string ?? "").Sum(), 7.8, 1e-12);
            var ol = created[0].GetType().GetProperty("Openings")?.GetValue(created[0]) as System.Collections.IList;
            check("AcadPick sàn mặt bằng: 1 lỗ thủng", ol?.Count ?? 0, 1, 0);
        }

        // ---- tạo cấu kiện mới ----
        var cols = AcadPick.CreateMembers(AcadPick.Parse("{\"kind\":\"Cot\",\"items\":[{\"name\":\"C1\",\"b\":0.22,\"h\":0.22},{\"name\":\"C2\",\"b\":0.22,\"h\":0.3}]}"));
        check("AcadPick tạo cột: 2 ManualColumn", cols.Count(c => c is ManualColumn), 2, 0);
        check("AcadPick tạo cột: C2 h = 0,3", ((ManualColumn)cols[1]).H, 0.3, 1e-12);
        check("AcadPick tạo dầm/móng/móng băng đúng kiểu", B(AcadPick.CreateMembers(dam)[0] is ManualBeam && AcadPick.CreateMembers(mong)[0] is ManualFooting && AcadPick.CreateMembers(mb)[0] is ManualStrip), 1, 0);

        // ---- dữ liệu lỗi không ném ----
        var bad = AcadPick.Parse("{ khong phai json");
        check("AcadPick JSON hỏng: không ném, có lỗi, 0 đối tượng", B(bad.Errors.Count > 0 && bad.Items.Count == 0), 1, 0);
        check("AcadPick chuỗi rỗng: không ném", B(AcadPick.Parse("").Errors.Count > 0), 1, 0);
        check("AcadPick loại lạ: không tạo cấu kiện", AcadPick.CreateMembers(AcadPick.Parse("{\"kind\":\"Xyz\",\"items\":[{\"b\":1}]}")).Count, 0, 0);
        check("AcadPick số dạng chuỗi \"0,25\" đọc được", AcadPick.Parse("{\"kind\":\"Cot\",\"items\":[{\"b\":\"0,25\",\"h\":0.3}]}").Items[0].B ?? 0, 0.25, 1e-12);

        // ---- file mới nhất ----
        string dir = Path.Combine(outDir, "AcadPick");
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        Directory.CreateDirectory(dir);
        AcadPick.FolderOverride = dir;
        try
        {
            check("AcadPick thư mục trống: Latest = null", B(AcadPick.Latest() == null), 1, 0);
            string f1 = Path.Combine(dir, "pick_20260101_080000_000.json"), f2 = Path.Combine(dir, "pick_20260101_090000_000.json");
            File.WriteAllText(f1, "{\"kind\":\"Cot\",\"items\":[{\"b\":0.2,\"h\":0.2}]}");
            File.WriteAllText(f2, "{\"kind\":\"Dam\",\"items\":[{\"b\":0.2,\"h\":0.4,\"spans\":[3]}]}");
            File.SetLastWriteTime(f1, new DateTime(2026, 1, 1, 8, 0, 0));
            File.SetLastWriteTime(f2, new DateTime(2026, 1, 1, 9, 0, 0));
            var lat = AcadPick.Latest();
            check("AcadPick Latest: file mới nhất (Dầm)", B(lat != null && lat.Kind == "Dam" && lat.File == f2), 1, 0);
            check("AcadPick Latest(newerThan): không có file mới hơn → null", B(AcadPick.Latest(new DateTime(2026, 1, 1, 9, 30, 0)) == null), 1, 0);
        }
        finally { AcadPick.FolderOverride = null; }

        // ---- hình học ----
        check("Gộp trục lệch ≤ 10 mm: 3 trục → 2 khoảng", AcadPickGeometry.Spacings(new[] { 0.0, 0.004, 3.6, 7.8, 7.805 }, 0.01).Count, 2, 0);
        check("Khoảng trục đầu = 3,598 (trung bình cụm 0 / 0,004)", AcadPickGeometry.Spacings(new[] { 0.0, 0.004, 3.6, 7.8 }, 0.01)[0], 3.598, 1e-9);
        var axis = new List<(double, double)> { (0, 0), (7.8, 0) };
        var sp = AcadPickGeometry.BeamSpans(axis, new[] { (0.0, 0.05), (4.2, -0.02), (7.8, 0.0), (4.0, 3.0) }, 0.3, 0.01);
        check("Nhịp dầm từ cột sát trục: 4,2 + 3,6 (bỏ cột xa trục)", sp.Count * 10 + sp[0], 24.2, 1e-9);
        var sp2 = AcadPickGeometry.BeamSpans(new List<(double, double)> { (0, 0), (3, 0), (3, 4) }, null, 0.3, 0.01);
        check("Nhịp dầm gãy khúc theo đỉnh đa tuyến: 3 + 4", sp2.Sum(), 7, 1e-12);
        var (s, d) = AcadPickGeometry.Project(new List<(double, double)> { (0, 0), (3, 0), (3, 4) }, 3.1, 2.0);
        check("Chiếu điểm lên trục gãy: s = 3 + 2", s, 5, 1e-12);
        check("Chiếu điểm: khoảng cách 0,1", d, 0.1, 1e-9);
        var op = AcadPickGeometry.Opening(5.0, 6.2, 4.0, 5.0, 2.0, 3.0);
        check("Lỗ so với gốc: X = 2, Y = 2, W = 1, H = 1,2", op.X * 1000 + op.Y * 100 + op.W * 10 + op.H, 2000 + 200 + 10 + 1.2, 1e-9);
        check("Đoán đơn vị: 7800 → mm", AcadPickGeometry.GuessUnit(7800), 0.001, 1e-12);
        check("Đoán đơn vị: 7,8 → m", AcadPickGeometry.GuessUnit(7.8), 1.0, 1e-12);
    }
}

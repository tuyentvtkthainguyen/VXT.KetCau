using KetCauVXT.Drawing;
using KetCauVXT.Mep;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Bộ môn điện nước: thư viện, đặt thiết bị gắn tường, đi tuyến chôn ngầm, phụ kiện, chọn cỡ, đối chiếu kiến trúc, thống kê, bản vẽ.</summary>
public static class MepTests
{
    private static double B(bool v) => v ? 1 : 0;

    /// <summary>Phòng 4 × 3 m tầng T1 (cao 3,3): W1 (0,0)→(4,0) tường 220, W2 (4,0)→(4,3) tường 110, W3 (4,3)→(0,3) 220, W4 (0,3)→(0,0) 220.</summary>
    private static Project Room()
    {
        var p = new Project();
        p.Stories = new List<Story> { new() { Name = "T1", Elevation = 0, Height = 3.3 }, new() { Name = "T2", Elevation = 3.3, Height = 3.3 } };
        p.Walls = new List<WallLine>
        {
            new() { Id = 1, Story = "T1", X1 = 0, Y1 = 0, X2 = 4, Y2 = 0, Type = "Tường 220 trát 2 mặt" },
            new() { Id = 2, Story = "T1", X1 = 4, Y1 = 0, X2 = 4, Y2 = 3, Type = "Tường 110 trát 2 mặt" },
            new() { Id = 3, Story = "T1", X1 = 4, Y1 = 3, X2 = 0, Y2 = 3, Type = "Tường 220 trát 2 mặt" },
            new() { Id = 4, Story = "T1", X1 = 0, Y1 = 3, X2 = 0, Y2 = 0, Type = "Tường 220 trát 2 mặt" },
        };
        return p;
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir, "TrienKhai", "DienNuoc");
        Directory.CreateDirectory(dir);

        // ---- 1. Thư viện ----
        var lib = MepLibrary.Defaults();
        foreach (var n in new[] { "Ống PPR Ø20", "Ống uPVC Ø110", "Cút 90°", "Chếch 45° (lơi)", "Tê đều", "Tê giảm", "Côn thu", "Măng sông",
                     "Ổ cắm đơn", "Ổ cắm đôi", "Công tắc 2 hạt", "Đèn ốp trần", "Đèn tuýp LED 1,2 m", "Quạt trần", "Quạt treo tường", "Hộp chia (điểm chia)",
                     "Ống gen PVC Ø20", "Dây CV 2,5 mm²", "Cáp CVV 3x4 mm²", "Bồn cầu", "Lavabo", "Sen tắm", "Vòi rửa", "Thoát sàn (phễu thu)", "Bình nóng lạnh 30 L" })
            check("Thư viện có: " + n, B(lib.Find(n) != null), 1, 0);
        var lib2 = MepLibrary.FromJson(lib.ToJson());
        check("Thư viện JSON: giữ số họ", lib2.Families.Count, lib.Families.Count, 0);
        check("Thư viện JSON: cao độ công tắc 1,25", lib2.Find("Công tắc 1 hạt").MountHeight, 1.25, 1e-9);
        check("Thư viện JSON: lavabo 3 đầu nối", lib2.Find("Lavabo").Connectors.Count, 3, 0);
        string userPath = Path.Combine(dir, "ThuVienMEP_NguoiDung.json");
        var ul = MepLibrary.Defaults();
        ul.Families.Add(new MepFamily { Name = "Ổ cắm mạng RJ45", Category = MepCategory.OCam, Symbol = "ocam1", MountHeight = 0.4, User = true });
        ul.SaveUser(userPath);
        var ul2 = MepLibrary.Load(userPath);
        check("Thư viện người dùng: nạp họ thêm", B(ul2.Find("Ổ cắm mạng RJ45")?.User == true), 1, 0);
        check("Thư viện người dùng: vẫn đủ họ mặc định", ul2.Families.Count, lib.Families.Count + 1, 0);

        // ---- 2. Đặt thiết bị ----
        var p = Room();
        var w1 = p.Walls[0]; var w2 = p.Walls[1];
        var oc1 = MepRouting.PlaceOnWall(p, "Ổ cắm đơn", w1, 1.0, 1);
        check("Ổ cắm: cao 0,4 m mặc định", oc1.Height, 0.4, 1e-9);
        check("Ổ cắm: X dọc tường", oc1.X, 1.0, 1e-9);
        check("Ổ cắm: Y = mặt tường 220 trát 2 mặt (0,125)", oc1.Y, 0.125, 1e-9);
        check("Ổ cắm: Z tuyệt đối = 0,4", MepRouting.DevicePos(p, oc1).Z, 0.4, 1e-9);
        var oc2 = MepRouting.PlaceOnWall(p, "Ổ cắm đơn", w2, 2.0, 1);
        check("Ổ cắm tường 110: X = 4 − 0,07", oc2.X, 4 - 0.07, 1e-9);
        var den = MepRouting.PlaceFree(p, "Đèn ốp trần", "T1", 2, 1.5);
        check("Đèn trần: cao = 3,3 − 0,1", den.Height, 3.2, 1e-9);
        var ct = MepRouting.Place(p, "Công tắc 1 hạt", "T1", 0.3, 2.0);
        check("Đặt gần tường: bám tường W4", ct.WallId, 4, 0);
        check("Công tắc cao 1,25", ct.Height, 1.25, 1e-9);
        var od = MepRouting.Place(p, "Ổ cắm đôi", "T1", 3.0, 0.3);
        check("Ổ cắm đôi: tường 1, vị trí 3,0, mặt trong phòng", B(od.WallId == 1 && Math.Abs(od.Offset - 3.0) < 1e-9 && od.Side == 1), 1, 0);

        // ---- 3. Đi tuyến chôn tường + phụ kiện ----
        var run = MepRouting.Connect(p, oc1.Id, oc2.Id, MepSystemKind.DienOCam);
        check("Tuyến ổ cắm: 5 điểm (xuống – dọc W1 – dọc W2 – lên)", run.Path.Count, 5, 0);
        check("Tuyến ổ cắm: dài 0,1 + 3 + 2 + 0,1", run.Length, 5.2, 1e-9);
        check("Tuyến ổ cắm: đoạn ngang ở cao độ 0,3", run.Path[2].Z, 0.3, 1e-9);
        check("Tuyến ổ cắm: đoạn 2 nằm trong tường W1", run.Path[2].WallId, 1, 0);
        check("Tuyến ổ cắm: đoạn 3 nằm trong tường W2", run.Path[3].WallId, 2, 0);
        MepRouting.RebuildAll(p);
        var rf = p.Mep.Fittings.Where(f => f.RunId == run.Id).ToList();
        check("Phụ kiện: 3 cút ống gen 90°", rf.Count(f => f.Kind == MepFittingKind.Cut90), 3, 0);
        check("Phụ kiện: 1 khớp nối ống gen (đoạn 3 m, cây 2,9 m)", rf.Count(f => f.Kind == MepFittingKind.MangSong), 1, 0);
        check("Ống gen tự chọn Ø16 cho 3 dây 2,5", rf.First().Size, 16, 0);

        var syn = new MepRun { Id = 99, System = MepSystemKind.CapNuocLanh, Kind = MepRunKind.Ong, Size = 25, Story = "T1",
            Path = new List<MepPt> { new(0, 0, 0), new(2, 0, 0), new(3, 1, 0), new(3, 10, 0) } };
        var sf = MepRouting.RunFittings(p, syn);
        check("Đổi hướng 45° → 2 chếch", sf.Count(f => f.Kind == MepFittingKind.Chech45), 2, 0);
        check("Đổi hướng 45° → 0 cút", sf.Count(f => f.Kind == MepFittingKind.Cut90), 0, 0);
        check("Đoạn 9 m, cây ống 4 m → 2 măng sông", sf.Count(f => f.Kind == MepFittingKind.MangSong), 2, 0);
        check("Góc đổi hướng tại điểm gấp 90°", MepRouting.TurnAngle(new MepPt(0, 0, 0), new MepPt(1, 0, 0), new MepPt(1, 1, 0)), 90, 1e-9);

        // ---- 4. Rẽ nhánh, côn, hộp nối, chọn cỡ ----
        var q = Room();
        var van = MepRouting.PlaceOnWall(q, "Van chặn", q.Walls[0], 0.5, 1);
        var nut = MepRouting.PlaceOnWall(q, "Điểm nối ống", q.Walls[3], 1.5, 1);
        var lv = MepRouting.PlaceOnWall(q, "Lavabo", q.Walls[2], 1.0, 1);
        var sen = MepRouting.PlaceOnWall(q, "Sen tắm", q.Walls[2], 3.0, 1);
        var t1 = MepRouting.Connect(q, van.Id, nut.Id, MepSystemKind.CapNuocLanh);
        var t2 = MepRouting.Connect(q, nut.Id, lv.Id, MepSystemKind.CapNuocLanh);
        var t3 = MepRouting.Connect(q, nut.Id, sen.Id, MepSystemKind.CapNuocLanh);
        check("Lưu lượng N = 1: q = 0,2·2,15·1 + 0,002", MepSizing.SupplyFlow(1.0), 0.432, 1e-9);
        check("q = 0,432 l/s, v ≤ 1,5 → PPR Ø32", MepSizing.SupplyPipe(0.432), 32, 0);
        MepSizing.AutoSize(q);
        check("Ống chính (N = 1,0) Ø32", t1.Size, 32, 0);
        check("Nhánh lavabo (N = 0,33) Ø25", t2.Size, 25, 0);
        check("Nhánh sen (N = 0,67) Ø32", t3.Size, 32, 0);
        MepRouting.RebuildAll(q);
        check("Điểm nối 3 tuyến khác cỡ → 1 tê giảm", q.Mep.Fittings.Count(f => f.Kind == MepFittingKind.TeGiam && f.DeviceId == nut.Id), 1, 0);
        t2.Size = 32; t2.AutoSize = false;
        MepRouting.RebuildAll(q);
        check("Cùng cỡ → tê đều", q.Mep.Fittings.Count(f => f.Kind == MepFittingKind.Te && f.DeviceId == nut.Id), 1, 0);
        var n2 = MepRouting.PlaceOnWall(q, "Điểm nối ống", q.Walls[0], 3.0, 1);
        var v2 = MepRouting.PlaceOnWall(q, "Vòi rửa", q.Walls[1], 1.0, 1);
        var r4 = MepRouting.Connect(q, van.Id, n2.Id, MepSystemKind.CapNuocNong, 32); r4.AutoSize = false;
        var r5 = MepRouting.Connect(q, n2.Id, v2.Id, MepSystemKind.CapNuocNong, 20); r5.AutoSize = false;
        MepRouting.RebuildAll(q);
        check("Thay đổi tiết diện tại điểm nối → côn thu Ø32/Ø20", q.Mep.Fittings.Count(f => f.Kind == MepFittingKind.Con && f.DeviceId == n2.Id && f.Size == 32 && f.Size2 == 20), 1, 0);
        var hc = MepRouting.PlaceOnWall(q, "Hộp chia (điểm chia)", q.Walls[0], 2.0, 1);
        var e1 = MepRouting.PlaceOnWall(q, "Ổ cắm đơn", q.Walls[0], 1.2, 1);
        var e2 = MepRouting.PlaceOnWall(q, "Ổ cắm đơn", q.Walls[1], 2.5, 1);
        var e3 = MepRouting.PlaceOnWall(q, "Ổ cắm đôi", q.Walls[3], 0.6, 1);
        foreach (var e in new[] { e1, e2, e3 }) MepRouting.Connect(q, hc.Id, e.Id, MepSystemKind.DienOCam);
        MepRouting.RebuildAll(q);
        check("Điện: 3 tuyến tại điểm chia → 1 hộp nối", q.Mep.Fittings.Count(f => f.Kind == MepFittingKind.HopNoi && f.DeviceId == hc.Id), 1, 0);

        check("I = 3000/(220·0,85)", MepSizing.Current(3000), 3000 / (220 * 0.85), 1e-9);
        check("3000 W ổ cắm → aptomat 20 A", MepSizing.Breaker(MepSizing.Current(3000)), 20, 0);
        check("3000 W ổ cắm → dây 2,5 mm²", MepSizing.WireFor(3000, MepSystemKind.DienOCam), 2.5, 0);
        check("6000 W điều hoà → 40 A → dây 6 mm²", MepSizing.WireFor(6000, MepSystemKind.DienDieuHoa), 6, 0);
        check("200 W chiếu sáng → dây 1,5 mm²", MepSizing.WireFor(200, MepSystemKind.DienChieuSang), 1.5, 0);
        check("Ổ cắm 300 W vẫn tối thiểu 2,5 mm²", MepSizing.WireFor(300, MepSystemKind.DienOCam), 2.5, 0);
        check("Thoát DFU 2 → Ø49", MepSizing.DrainPipe(2, false), 49, 0);
        check("Thoát DFU 5 → Ø60", MepSizing.DrainPipe(5, false), 60, 0);
        check("Thoát DFU 15 → Ø90", MepSizing.DrainPipe(15, false), 90, 0);
        check("Thoát có bồn cầu → Ø110", MepSizing.DrainPipe(4, true), 110, 0);
        check("Ống gen 3 dây 4 mm² → Ø16", MepSizing.ConduitFor(4, 3), 16, 0);
        check("Ống gen 3 dây 6 mm² → Ø20 (lấp ≤ 40 %)", MepSizing.ConduitFor(6, 3), 20, 0);

        // ---- 5. Đối chiếu kiến trúc (xref) ----
        var a = Room();
        var s1 = MepRouting.PlaceOnWall(a, "Ổ cắm đơn", a.Walls[0], 1.0, 1);
        var s2 = MepRouting.PlaceOnWall(a, "Ổ cắm đơn", a.Walls[1], 2.0, 1);
        MepRouting.Connect(a, s1.Id, s2.Id, MepSystemKind.DienOCam);
        var lvA = MepRouting.PlaceOnWall(a, "Lavabo", a.Walls[2], 1.0, 1);
        var res0 = MepUpdater.Reconcile(a);
        check("Đối chiếu lần đầu: không cảnh báo", res0.Warnings.Count, 0, 0);
        check("Chưa đổi kiến trúc → không có thay đổi", B(!MepUpdater.HasArchChanges(a)), 1, 0);
        a.Walls[0].Y1 = 0.5; a.Walls[0].Y2 = 0.5; a.Walls[1].Y1 = 0.5; a.Walls[3].Y2 = 0.5;
        check("Dời tường → nhận biết thay đổi", B(MepUpdater.HasArchChanges(a)), 1, 0);
        var res1 = MepUpdater.Reconcile(a);
        check("Dời tường: ổ cắm theo tường (Y = 0,5 + 0,125)", s1.Y, 0.625, 1e-9);
        check("Dời tường: báo tường 1 đã đổi", B(res1.ChangedWalls.Contains(1)), 1, 0);
        check("Dời tường: ổ cắm nằm trong danh sách dời", B(res1.MovedDevices.Contains(s1.Id)), 1, 0);
        check("Dời tường: tuyến đi lại, vẫn trong tường W1", B(a.Mep.Runs[0].Path.Any(x => x.WallId == 1 && Math.Abs(x.Y - 0.5) < 1e-9)), 1, 0);
        a.Walls[0].Openings.Add(new WallOpening { Id = 1, Type = "D1 900x2200", Offset = 1.0 });
        var res2 = MepUpdater.Reconcile(a);
        check("Thêm cửa đè ổ cắm → cảnh báo TRONG_CUA", res2.Warnings.Count(x => x.Code == "TRONG_CUA" && x.DeviceId == s1.Id), 1, 0);
        var wDoor = res2.Warnings.First(x => x.Code == "TRONG_CUA");
        check("Sửa nhanh: dời ra cạnh ô cửa", B(MepUpdater.ApplyFix(a, wDoor)), 1, 0);
        check("Sửa nhanh: vị trí mới 1,5 m (ngoài ô cửa 0,55…1,45)", s1.Offset, 1.5, 1e-9);
        check("Sau sửa: hết cảnh báo ô cửa", MepUpdater.Reconcile(a).Warnings.Count(x => x.Code == "TRONG_CUA"), 0, 0);
        var s3 = MepRouting.PlaceOnWall(a, "Ổ cắm đơn", a.Walls[0], 1.5, 1);
        check("Hai ổ cắm cùng chỗ → cảnh báo VA_CHAM", MepUpdater.Reconcile(a).Warnings.Count(x => x.Code == "VA_CHAM"), 1, 0);
        a.Mep.Devices.Remove(s3);
        a.Walls.RemoveAt(2);   // xoá tường W3 có lavabo
        var res3 = MepUpdater.Reconcile(a);
        check("Xoá tường → cảnh báo TUONG_XOA cho lavabo", res3.Warnings.Count(x => x.Code == "TUONG_XOA" && x.DeviceId == lvA.Id), 1, 0);
        check("Xoá tường: báo trong danh sách tường xoá", B(res3.DeletedWalls.Contains(3)), 1, 0);
        check("Sửa nhanh: gắn lavabo vào tường gần nhất", B(MepUpdater.ApplyFix(a, res3.Warnings.First(x => x.Code == "TUONG_XOA")) && lvA.WallId is > 0 and not 3), 1, 0);

        var t = Room();
        var wc = MepRouting.PlaceOnWall(t, "Bồn cầu", t.Walls[1], 1.5, 1);
        var fd = MepRouting.PlaceFree(t, "Thoát sàn (phễu thu)", "T1", 2.0, 1.5);
        var dr = MepRouting.Connect(t, wc.Id, fd.Id, MepSystemKind.ThoatNuocThai, 110); dr.AutoSize = false;
        var x1 = MepRouting.PlaceOnWall(t, "Ổ cắm đơn", t.Walls[1], 0.5, 1);
        var x2 = MepRouting.PlaceOnWall(t, "Ổ cắm đơn", t.Walls[1], 2.6, 1);
        MepRouting.Connect(t, x1.Id, x2.Id, MepSystemKind.DienOCam);
        var rt = MepUpdater.Reconcile(t);
        check("Ống thoát Ø110 trong tường 110 → cảnh báo TUONG_MONG", rt.Warnings.Count(x => x.Code == "TUONG_MONG" && x.RunId == dr.Id && x.WallId == 2), 1, 0);
        check("Ống gen Ø16 trong tường 110 → không cảnh báo", rt.Warnings.Count(x => x.Code == "TUONG_MONG" && x.RunId != dr.Id), 0, 0);
        check("Tường 110 trát 2 mặt: chôn tối đa ½·110 + 15 = 70 mm", MepRouting.WallInfo(t, t.Walls[1]).MaxEmbed, 0.07, 1e-9);
        check("Tường 220 trát 2 mặt: chôn tối đa 125 mm", MepRouting.WallInfo(t, t.Walls[0]).MaxEmbed, 0.125, 1e-9);

        var sb = Room();
        sb.FrameSections.Add(new FrameSection { Name = "D22x30", B = 0.22, H = 0.3 });
        sb.Nodes.Add(new Node { Id = 1, X = 2, Y = -1, Z = 3.3, Story = "T2" });
        sb.Nodes.Add(new Node { Id = 2, X = 2, Y = 4, Z = 3.3, Story = "T2" });
        sb.Frames.Add(new FrameElement { Id = 1, Label = "D1", NodeI = 1, NodeJ = 2, Section = "D22x30", Kind = FrameKind.Beam, Story = "T2" });
        var sw = MepRouting.PlaceOnWall(sb, "Công tắc 1 hạt", sb.Walls[0], 1.0, 1);
        var lt = MepRouting.PlaceFree(sb, "Đèn ốp trần", "T1", 3.0, 1.5);
        var cs = MepRouting.Connect(sb, sw.Id, lt.Id, MepSystemKind.DienChieuSang);
        check("Tuyến chiếu sáng đi sát trần (3,3 − 0,15)", cs.Path[2].Z, 3.15, 1e-9);
        var rb = MepUpdater.Reconcile(sb);
        check("Tuyến sát trần cắt dầm D1 → cảnh báo XUYEN_DAM", rb.Warnings.Count(x => x.Code == "XUYEN_DAM" && x.RunId == cs.Id), 1, 0);

        // ---- 6. Thống kê vật tư ----
        var rows = MepQuantities.Rows(p);
        double Qty(string item) => rows.Where(r => r.Item == item).Sum(r => r.Qty);
        check("Thống kê: 2 ổ cắm đơn", Qty("Ổ cắm đơn"), 2, 0);
        check("Thống kê: ống gen Ø16 = 5,2 m", Qty("Ống gen PVC Ø16"), 5.2, 1e-6);
        check("Thống kê: dây CV 2,5 = 3 ruột × 5,2 m", Qty("Dây CV 2,5 mm²"), 15.6, 1e-6);
        check("Thống kê: 3 cút ống gen Ø16", Qty("Cút ống gen 90° Ø16"), 3, 0);
        check("Thống kê: 1 khớp nối ống gen Ø16", Qty("Khớp nối ống gen Ø16"), 1, 0);
        var rq = MepQuantities.Rows(q);
        check("Thống kê nước: có ống PPR Ø32", B(rq.Any(r => r.Item == "Ống PPR PN20 Ø32" && r.Qty > 0)), 1, 0);
        check("Thống kê nước: có côn thu", B(rq.Any(r => r.Item.StartsWith("Côn thu"))), 1, 0);

        // ---- 7. Bản vẽ ----
        MepDrawings.AutoMark(p);
        check("Ký hiệu tự động: ổ cắm OC1", B(p.Mep.Devices.Any(d => d.Mark == "OC1")), 1, 0);
        var figs = MepDrawings.Figures(p);
        check("Bản vẽ điện nước: ≥ 3 hình (ổ cắm, chiếu sáng, thống kê)", B(figs.Count >= 3), 1, 0);
        var oc = figs.FirstOrDefault(f => f.Name.StartsWith("MẶT BẰNG ĐIỆN Ổ CẮM"));
        check("Có mặt bằng điện ổ cắm T1", B(oc != null), 1, 0);
        if (oc != null)
        {
            string txt = oc.W.ToDxfStringDec();
            check("Mặt bằng ổ cắm: tiêu đề", B(txt.Contains("MẶT BẰNG ĐIỆN Ổ CẮM T1")), 1, 0);
            check("Mặt bằng ổ cắm: bảng ký hiệu", B(txt.Contains(MepDrawings.LegendTitle)), 1, 0);
            check("Mặt bằng ổ cắm: tên họ trong bảng ký hiệu", B(txt.Contains("Ổ cắm đơn")), 1, 0);
            check("Mặt bằng ổ cắm: ký hiệu thiết bị OC1", B(txt.Contains("OC1")), 1, 0);
            check("Mặt bằng ổ cắm: nhãn tuyến 3x2,5 – Ø16", B(txt.Contains("3x2,5 – Ø16")), 1, 0);
            check("Mặt bằng ổ cắm: ký hiệu vẽ trên layer KY_HIEU", B(oc.W.CountEntities("POLYLINE", Lay.KyHieu) + oc.W.CountEntities("LINE", Lay.KyHieu) >= 6), 1, 0);
            oc.W.Save(Path.Combine(dir, "MB_DienOCam_T1.dxf"));
        }
        var sch = figs.FirstOrDefault(f => f.Name == MepQuantities.Title);
        check("Có bảng thống kê vật tư điện nước", B(sch != null), 1, 0);
        if (sch != null) sch.W.Save(Path.Combine(dir, "ThongKe_DienNuoc.dxf"));
        var qf = MepDrawings.Figures(q);
        check("Bản vẽ cấp nước có mặt bằng cấp nước", B(qf.Any(f => f.Name.StartsWith("MẶT BẰNG CẤP NƯỚC"))), 1, 0);
        foreach (var f in qf.Where(f => f.Name.StartsWith("MẶT BẰNG CẤP NƯỚC"))) f.W.Save(Path.Combine(dir, "MB_CapNuoc_T1.dxf"));
        var sw2 = new DxfWriter(100);
        int drawn = MepSymbols.All.Count(s => MepSymbols.Draw(sw2, s, 0, 0, 200) > 0);
        check("Mọi ký hiệu thiết bị đều vẽ được", drawn, MepSymbols.All.Length, 0);

        // ---- 8. Lưu / mở dự án ----
        var json = System.Text.Json.JsonSerializer.Serialize(p, Project.JsonOptions);
        var p2 = System.Text.Json.JsonSerializer.Deserialize<Project>(json, Project.JsonOptions);
        check("Lưu / mở: giữ thiết bị", p2.Mep.Devices.Count, p.Mep.Devices.Count, 0);
        check("Lưu / mở: giữ tuyến và đường đi", p2.Mep.Runs[0].Path.Count, p.Mep.Runs[0].Path.Count, 0);
        check("Lưu / mở: giữ hệ thống (enum chữ)", B(json.Contains("\"DienOCam\"")), 1, 0);
        var old = System.Text.Json.JsonSerializer.Deserialize<Project>("{\"Name\":\"cu\"}", Project.JsonOptions);
        check("Mở file cũ không có điện nước → mô hình rỗng", B(old.Mep != null && old.Mep.IsEmpty), 1, 0);
        var boxes = MepSolids.Boxes(p);
        check("Khối 3D: mỗi thiết bị một hộp", boxes.Count, p.Mep.Devices.Count, 0);
        check("Khối 3D: đoạn ống = tổng số đoạn đường đi", MepSolids.Segments(p).Count, p.Mep.Runs.Sum(r => Math.Max(0, r.Path.Count - 1)), 0);
    }
}

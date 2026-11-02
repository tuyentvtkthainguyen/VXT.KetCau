using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CSMath;

namespace KetCauVXT.Dwg;

/// <summary>Bản vẽ DWG mẫu (dùng cho kiểm thử và tự kiểm tra giao diện): mặt bằng nhà 4,5×8 m một tầng, đơn vị mm.
/// <list type="bullet">
/// <item>Tường 220 hai nét bao quanh, tường ngăn 110 tại trục B có khe cửa 3200–4100 (lớp TUONG);</item>
/// <item>Trục 1, 2 (dọc) và A, B, C (ngang): đường trục + vòng tròn đầu trục R400 + chữ tên trục (lớp TRUC);</item>
/// <item>Cột 220×220 vẽ bằng đa tuyến kín tại 6 giao điểm trục (lớp COT);</item>
/// <item>Block cửa đi CUA_D1 (cánh + cung, có block lồng TAYNAM) chèn tại khe cửa (lớp CUA);</item>
/// <item>Tên phòng "P.KHÁCH", "BẾP" và tên hình "MẶT BẰNG TẦNG 1" (lớp TEXT).</item>
/// </list>
/// Tên lớp = layerPrefix + tên trên (mặc định "KT-": KT-TUONG, KT-TRUC…).</summary>
public static class DwgSample
{
    public const double WallOuter = 220, WallInner = 110;

    /// <summary>Dựng bản vẽ mẫu (chưa ghi file).</summary>
    public static CadDocument BuildHouse(string layerPrefix = "KT-", ACadVersion version = ACadVersion.AC1032)
    {
        layerPrefix ??= "";
        var doc = new CadDocument(version);
        doc.Header.InsUnits = UnitsType.Millimeters;

        Layer Lay(string name, short aci)
        {
            var l = new Layer(layerPrefix + name) { Color = new Color(aci) };
            doc.Layers.Add(l);
            return l;
        }
        var tuong = Lay("TUONG", 7);
        var truc = Lay("TRUC", 1);
        var cot = Lay("COT", 4);
        var cua = Lay("CUA", 30);
        var text = Lay("TEXT", 2);

        void Ln(Layer l, double x1, double y1, double x2, double y2) =>
            doc.Entities.Add(new Line { StartPoint = new XYZ(x1, y1, 0), EndPoint = new XYZ(x2, y2, 0), Layer = l });
        void Tx(Layer l, double x, double y, double h, string s) =>
            doc.Entities.Add(new TextEntity { Value = s, InsertPoint = new XYZ(x, y, 0), Height = h, Layer = l });

        // ---- tường 220 hai nét (tim trục 1, 2, A, C) ----
        foreach (double d in new[] { -WallOuter / 2, WallOuter / 2 })
        {
            Ln(tuong, 0, d, 4500, d); Ln(tuong, 0, 8000 + d, 4500, 8000 + d);
            Ln(tuong, d, 0, d, 8000); Ln(tuong, 4500 + d, 0, 4500 + d, 8000);
        }
        // ---- tường ngăn 110 tại trục B, khe cửa 3200–4100 ----
        foreach (double d in new[] { -WallInner / 2, WallInner / 2 })
        {
            Ln(tuong, 110, 4000 + d, 3200, 4000 + d);
            Ln(tuong, 4100, 4000 + d, 4390, 4000 + d);
        }

        // ---- trục: đường + vòng tròn đầu trục + tên ----
        foreach (var (gx, n) in new[] { (0.0, "1"), (4500.0, "2") })
        {
            Ln(truc, gx, -1500, gx, 9500);
            doc.Entities.Add(new Circle { Center = new XYZ(gx, 9900, 0), Radius = 400, Layer = truc });
            Tx(truc, gx - 100, 9750, 350, n);
        }
        foreach (var (gy, n) in new[] { (0.0, "A"), (4000.0, "B"), (8000.0, "C") })
        {
            Ln(truc, -1500, gy, 6000, gy);
            doc.Entities.Add(new Circle { Center = new XYZ(-1900, gy, 0), Radius = 400, Layer = truc });
            Tx(truc, -2000, gy - 150, 350, n);
        }

        // ---- cột 220×220 (đa tuyến kín) ----
        foreach (double cx in new[] { 0.0, 4500.0 })
            foreach (double cy in new[] { 0.0, 4000.0, 8000.0 })
            {
                var pl = new LwPolyline { Layer = cot, IsClosed = true };
                pl.Vertices.Add(new LwPolyline.Vertex(new XY(cx - 110, cy - 110)));
                pl.Vertices.Add(new LwPolyline.Vertex(new XY(cx + 110, cy - 110)));
                pl.Vertices.Add(new LwPolyline.Vertex(new XY(cx + 110, cy + 110)));
                pl.Vertices.Add(new LwPolyline.Vertex(new XY(cx - 110, cy + 110)));
                doc.Entities.Add(pl);
            }

        // ---- block cửa đi 900 (lớp 0 → lấy lớp của INSERT), lồng block tay nắm ----
        var cuaBlock = DoorBlock(doc);
        doc.Entities.Add(new Insert(cuaBlock) { InsertPoint = new XYZ(3200, 4000, 0), Layer = cua });

        // ---- chữ ----
        Tx(text, 1800, 2000, 300, "P.KHÁCH");
        Tx(text, 1800, 6000, 300, "BẾP");
        Tx(text, 500, -2600, 400, "MẶT BẰNG TẦNG 1");
        return doc;
    }

    /// <summary>Block cửa đi CUA_D1: cánh (0,0)–(0,900), cung R900 0°→90°, block lồng TAYNAM (vòng tròn R25 tại (0,850)).
    /// Tạo một lần cho mỗi bản vẽ.</summary>
    public static BlockRecord DoorBlock(CadDocument doc)
    {
        if (doc.BlockRecords.TryGetValue("CUA_D1", out var existing)) return existing;
        var tay = new BlockRecord("TAYNAM");
        tay.Entities.Add(new Circle { Center = new XYZ(0, 0, 0), Radius = 25 });
        doc.BlockRecords.Add(tay);

        var cua = new BlockRecord("CUA_D1");
        cua.Entities.Add(new Line { StartPoint = new XYZ(0, 0, 0), EndPoint = new XYZ(0, 900, 0) });
        cua.Entities.Add(new Arc { Center = new XYZ(0, 0, 0), Radius = 900, StartAngle = 0, EndAngle = Math.PI / 2 });
        cua.Entities.Add(new Insert(tay) { InsertPoint = new XYZ(0, 850, 0) });
        doc.BlockRecords.Add(cua);
        return cua;
    }

    /// <summary>Ghi bản vẽ mẫu ra file DWG.</summary>
    public static void WriteHouse(string path, string layerPrefix = "KT-", ACadVersion version = ACadVersion.AC1032) =>
        DwgReader.WriteDwg(BuildHouse(layerPrefix, version), path);
}

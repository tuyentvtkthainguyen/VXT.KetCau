using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace KetCauVXT.AcadLink
{
    /// <summary>Nhóm cấu kiện – chuỗi CỐ ĐỊNH, dùng chung với chương trình Kết cấu VXT (CadCategory / AcadCategory).</summary>
    internal static class Cat
    {
        public const string Tuong = "Tuong", Cot = "Cot", Dam = "Dam", San = "San", Truc = "Truc", Cua = "Cua", Thang = "Thang", TenPhong = "TenPhong", BoQua = "BoQua";
        public static readonly string[] All = { Tuong, Cot, Dam, San, Truc, Cua, Thang, TenPhong, BoQua };

        public static string Display(string c)
        {
            switch (c)
            {
                case Tuong: return "Tường";
                case Cot: return "Cột";
                case Dam: return "Dầm";
                case San: return "Sàn";
                case Truc: return "Lưới trục";
                case Cua: return "Cửa";
                case Thang: return "Cầu thang";
                case TenPhong: return "Tên phòng";
                case BoQua: return "Bỏ qua";
                default: return c ?? "";
            }
        }

        public static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            string t = s.Trim();
            foreach (var c in All)
                if (string.Equals(c, t, StringComparison.OrdinalIgnoreCase) || string.Equals(Display(c), t, StringComparison.OrdinalIgnoreCase)) return c;
            string n = LayerStd.NormName(t).Replace(" ", "");
            foreach (var c in All)
                if (string.Equals(c.ToUpperInvariant(), n, StringComparison.Ordinal) || LayerStd.NormName(Display(c)).Replace(" ", "") == n) return c;
            return null;
        }
    }

    /// <summary>Một tầng khai báo trong bản vẽ (lệnh VXT_TANG). Toạ độ theo đơn vị bản vẽ; cao độ, chiều cao: m.</summary>
    internal sealed class StoreyDef
    {
        public string Name = "";
        public double Elevation, Height;
        public double BaseX, BaseY;
        public double X0, Y0, X1, Y1;
        /// <summary>Handle khung phạm vi đã vẽ (để xoá khi khai báo lại)</summary>
        public string Frame = "";

        public bool Contains(double x, double y) => x >= Math.Min(X0, X1) && x <= Math.Max(X0, X1) && y >= Math.Min(Y0, Y1) && y <= Math.Max(Y0, Y1);

        private static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        private static double D(string s) { double v; return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0; }

        public string Encode() => string.Join("\t", new[] { Name.Replace("\t", " "), F(Elevation), F(Height), F(BaseX), F(BaseY), F(X0), F(Y0), F(X1), F(Y1), Frame ?? "" });

        public static StoreyDef Decode(string s)
        {
            var p = (s ?? "").Split('\t');
            if (p.Length < 9) return null;
            return new StoreyDef
            {
                Name = p[0], Elevation = D(p[1]), Height = D(p[2]), BaseX = D(p[3]), BaseY = D(p[4]),
                X0 = D(p[5]), Y0 = D(p[6]), X1 = D(p[7]), Y1 = D(p[8]), Frame = p.Length > 9 ? p[9] : "",
            };
        }
    }

    /// <summary>Lưu định nghĩa trong chính file DWG:
    /// <list type="bullet">
    /// <item>nhóm của từng đối tượng: XData ứng dụng "KETCAU_VXT" (mã 1000 = chuỗi nhóm);</item>
    /// <item>bảng layer → nhóm và danh sách tầng: từ điển "KETCAU_VXT" trong Named Objects Dictionary (Xrecord "LAYERMAP", "STOREYS").</item>
    /// </list></summary>
    internal static class DwgStore
    {
        public const string App = "KETCAU_VXT";
        public const string FrameLayer = "VXT-PHAMVI-TANG";
        private const string LayerMapKey = "LAYERMAP";
        private const string StoreysKey = "STOREYS";

        // ------------------------------------------------------------------ XData
        public static void EnsureRegApp(Database db, Transaction tr)
        {
            var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (rat.Has(App)) return;
            rat.UpgradeOpen();
            var r = new RegAppTableRecord { Name = App };
            rat.Add(r);
            tr.AddNewlyCreatedDBObject(r, true);
        }

        /// <summary>Gán nhóm (null = xoá nhóm). Đối tượng phải mở ForWrite.</summary>
        public static void SetTag(Entity e, string category)
        {
            if (category == null) e.XData = new ResultBuffer(new TypedValue(1001, App));
            else e.XData = new ResultBuffer(new TypedValue(1001, App), new TypedValue(1000, category));
        }

        public static string GetTag(DBObject e)
        {
            ResultBuffer rb = null;
            try { rb = e.GetXDataForApplication(App); }
            catch (System.Exception) { return null; }
            if (rb == null) return null;
            using (rb)
            {
                foreach (TypedValue tv in rb)
                    if (tv.TypeCode == 1000) return Cat.Normalize(tv.Value as string);
            }
            return null;
        }

        // ------------------------------------------------------------------ từ điển bản vẽ
        private static DBDictionary Dict(Database db, Transaction tr, bool create)
        {
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (nod.Contains(App)) return (DBDictionary)tr.GetObject(nod.GetAt(App), create ? OpenMode.ForWrite : OpenMode.ForRead);
            if (!create) return null;
            nod.UpgradeOpen();
            var d = new DBDictionary();
            nod.SetAt(App, d);
            tr.AddNewlyCreatedDBObject(d, true);
            return d;
        }

        private static List<string> ReadStrings(Database db, Transaction tr, string key)
        {
            var res = new List<string>();
            var d = Dict(db, tr, false);
            if (d == null || !d.Contains(key)) return res;
            var xr = tr.GetObject(d.GetAt(key), OpenMode.ForRead) as Xrecord;
            if (xr == null || xr.Data == null) return res;
            using (var rb = xr.Data)
                foreach (TypedValue tv in rb)
                    if (tv.TypeCode == 300 || tv.TypeCode == 1) res.Add(tv.Value as string ?? "");
            return res;
        }

        private static void WriteStrings(Database db, Transaction tr, string key, IEnumerable<string> values)
        {
            var d = Dict(db, tr, true);
            var rb = new ResultBuffer();
            foreach (var v in values) rb.Add(new TypedValue(300, v ?? ""));
            if (d.Contains(key))
            {
                var xr = (Xrecord)tr.GetObject(d.GetAt(key), OpenMode.ForWrite);
                xr.Data = rb;
            }
            else
            {
                var xr = new Xrecord { Data = rb };
                d.SetAt(key, xr);
                tr.AddNewlyCreatedDBObject(xr, true);
            }
        }

        /// <summary>Bảng layer → nhóm lưu trong bản vẽ (lệnh VXT_xxx, cách chọn "Layer").</summary>
        public static Dictionary<string, string> GetLayerMap(Database db, Transaction tr)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var s = ReadStrings(db, tr, LayerMapKey);
            for (int i = 0; i + 1 < s.Count; i += 2)
            {
                string c = Cat.Normalize(s[i + 1]);
                if (!string.IsNullOrEmpty(s[i]) && c != null) map[s[i]] = c;
            }
            return map;
        }

        public static void SetLayerMap(Database db, Transaction tr, Dictionary<string, string> map)
        {
            var list = new List<string>();
            foreach (var kv in map.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)) { list.Add(kv.Key); list.Add(kv.Value); }
            WriteStrings(db, tr, LayerMapKey, list);
        }

        public static List<StoreyDef> GetStoreys(Database db, Transaction tr) =>
            ReadStrings(db, tr, StoreysKey).Select(StoreyDef.Decode).Where(s => s != null).ToList();

        public static void SetStoreys(Database db, Transaction tr, IEnumerable<StoreyDef> storeys) =>
            WriteStrings(db, tr, StoreysKey, storeys.Select(s => s.Encode()));

        /// <summary>Nhóm hiệu lực của đối tượng: XData → bảng layer của bản vẽ → (tuỳ chọn) chuẩn layer của chương trình.</summary>
        public static string Effective(Entity e, Dictionary<string, string> dwgMap, LayerStd std)
        {
            string c = GetTag(e);
            if (c != null) return c;
            string layer = e.Layer ?? "";
            if (dwgMap != null && dwgMap.TryGetValue(layer, out c)) return c;
            if (std != null) return std.Classify(layer);
            return null;
        }
    }
}

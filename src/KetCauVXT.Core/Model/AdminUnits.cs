namespace KetCauVXT.Model;

// =====================================================================
// DANH MỤC ĐƠN VỊ HÀNH CHÍNH SAU SẮP XẾP (34 tỉnh/thành, 3.321 xã/phường/đặc khu – mô hình chính quyền 2 cấp từ 01/7/2025)
// Dữ liệu nhúng: Data/DonViHanhChinh.txt (vietnamese-provinces-database, MIT, theo QĐ 19/2025/QĐ-TTg và các nghị quyết sau đó).
// Chỉ là danh mục tên để chọn địa điểm xây dựng; vùng gió / agR vẫn theo QCVN 02:2022 (người dùng tra / nhập – LocationDb).
// =====================================================================

/// <summary>Một xã / phường / đặc khu.</summary>
public sealed class Commune
{
    public string Code { get; init; } = "";
    /// <summary>Tên đầy đủ, vd "Phường Quyết Thắng"</summary>
    public string FullName { get; init; } = "";
    public string Province { get; init; } = "";
    /// <summary>Loại: Phường, Xã, Đặc khu</summary>
    public string Type => FullName.StartsWith("Phường ") ? "Phường" : FullName.StartsWith("Đặc khu ") ? "Đặc khu" : FullName.StartsWith("Xã ") ? "Xã" : "";
    /// <summary>Tên bỏ tiền tố, vd "Quyết Thắng"</summary>
    public string ShortName => Type.Length > 0 ? FullName.Substring(Type.Length + 1) : FullName;
    public override string ToString() => FullName;
}

public static class AdminUnits
{
    private static Dictionary<string, List<Commune>> _byProvince;
    private static readonly object Lock = new();
    public static string SourceNote { get; private set; } = "";

    private static Dictionary<string, List<Commune>> Data
    {
        get
        {
            if (_byProvince != null) return _byProvince;
            lock (Lock)
            {
                if (_byProvince != null) return _byProvince;
                var res = new Dictionary<string, List<Commune>>();
                var notes = new List<string>();
                try
                {
                    using var st = typeof(AdminUnits).Assembly.GetManifestResourceStream("KetCauVXT.Data.DonViHanhChinh.txt");
                    if (st != null)
                    {
                        using var rd = new StreamReader(st, System.Text.Encoding.UTF8);
                        string line, prov = null;
                        List<Commune> cur = null;
                        while ((line = rd.ReadLine()) != null)
                        {
                            if (line.Length == 0) continue;
                            if (line[0] == '#') { notes.Add(line.TrimStart('#', ' ')); continue; }
                            var parts = line.Split('\t');
                            if (line[0] == '@')
                            {
                                prov = parts.Length > 1 ? parts[1] : parts[0].Substring(1);
                                var key = LocationDb.ProvinceKey(prov);
                                if (!res.TryGetValue(key, out cur)) res[key] = cur = new List<Commune>();
                            }
                            else if (cur != null && parts.Length >= 2)
                                cur.Add(new Commune { Code = parts[0], FullName = parts[1].Trim(), Province = prov });
                        }
                    }
                }
                catch { /* thiếu tài nguyên: danh mục rỗng */ }
                SourceNote = string.Join(" ", notes.Skip(1).Take(1));
                _byProvince = res;
                return res;
            }
        }
    }

    /// <summary>Tổng số xã / phường / đặc khu trong danh mục.</summary>
    public static int Count => Data.Values.Sum(l => l.Count);

    /// <summary>Số tỉnh / thành phố có danh mục.</summary>
    public static int ProvinceCount => Data.Count;

    /// <summary>Các xã / phường / đặc khu của tỉnh (tên tỉnh mới, tên tỉnh cũ hoặc tên gọi khác đều được), sắp theo: phường, xã, đặc khu rồi tên.</summary>
    public static IReadOnlyList<Commune> Of(string province)
    {
        if (string.IsNullOrWhiteSpace(province)) return Array.Empty<Commune>();
        var p = LocationDb.FindProvince(province);
        string key = LocationDb.ProvinceKey(p?.Name ?? province);
        if (!Data.TryGetValue(key, out var list)) return Array.Empty<Commune>();
        return list;
    }

    /// <summary>Tìm xã / phường theo tên (có hoặc không tiền tố, có hoặc không dấu) trong tỉnh.</summary>
    public static Commune Find(string province, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string k = LocationDb.PlaceKey(name);
        return Of(province).FirstOrDefault(c => LocationDb.PlaceKey(c.FullName) == k);
    }

    /// <summary>Tìm theo tên trên toàn quốc (gợi ý tỉnh khi người dùng chỉ gõ tên xã / phường).</summary>
    public static List<Commune> Search(string name, int max = 20)
    {
        if (string.IsNullOrWhiteSpace(name)) return new List<Commune>();
        string k = LocationDb.PlaceKey(name);
        return Data.Values.SelectMany(l => l).Where(c => LocationDb.PlaceKey(c.FullName).Contains(k)).Take(max).ToList();
    }
}

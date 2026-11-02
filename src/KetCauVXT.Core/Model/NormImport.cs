using System.Globalization;
using System.Text.RegularExpressions;
using KetCauVXT.Report;

namespace KetCauVXT.Model;

// =====================================================================
// NHẬP ĐỊNH MỨC TỪ EXCEL / CSV (bảng định mức hiện hành do người dùng cung cấp, vd. Phụ lục VII – định mức vật liệu
// của Định mức xây dựng ban hành kèm TT 38/2026/TT-BXD, thay thế theo CV 9947/BXD-VPB).
// Cột: Mã hiệu | Tên công tác | Đơn vị | Vật liệu | [Đơn vị vật liệu] | Định mức. Dòng có mã hiệu/tên để trống
// dùng lại mã hiệu/tên của dòng trên (kiểu trình bày của bảng định mức).
// Dòng cấp phối bê tông (tên có "bê tông" + cấp độ bền Bxx hoặc mác) và vữa (tên có "vữa" + mác Mxx) được áp vào
// QuantitySettings.ConcreteNorms / MortarNorms (xi măng, cát, đá, nước). Mọi dòng được lưu ở QuantitySettings.ImportedNorms.
// =====================================================================

/// <summary>Một dòng bảng định mức nhập từ Excel.</summary>
public sealed class NormRow
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Material { get; set; } = "";
    public string MaterialUnit { get; set; } = "";
    public double Value { get; set; }
    public override string ToString() => $"{Code} {Name}: {Material} = {Value} {MaterialUnit}";
}

/// <summary>Kết quả nhập bảng định mức.</summary>
public sealed class NormImportResult
{
    /// <summary>Số dòng dữ liệu đọc được</summary>
    public int Rows { get; set; }
    /// <summary>Số cấp phối bê tông / vữa được cập nhật hoặc thêm</summary>
    public int Concrete { get; set; }
    public int Mortar { get; set; }
    /// <summary>Số dòng không áp được vào cấp phối (chỉ lưu tham khảo)</summary>
    public int Other { get; set; }
    public List<string> Messages { get; } = new();
    public List<NormRow> Items { get; } = new();
    public override string ToString() =>
        $"Đọc {Rows} dòng định mức: cập nhật {Concrete} cấp phối bê tông, {Mortar} cấp phối vữa; {Other} dòng lưu tham khảo" + (Messages.Count > 0 ? $" ({Messages.Count} ghi chú)" : "");
}

public static class NormImport
{
    public static readonly string[] Headers = { "Mã hiệu", "Tên công tác", "Đơn vị", "Vật liệu", "Đơn vị vật liệu", "Định mức" };
    public const string ExampleMark = "VÍ DỤ";

    private enum C { None, Code, Name, Unit, Material, MatUnit, Value }

    private static C Classify(string header)
    {
        string h = " " + LocationDb.Norm(header) + " ";
        string t = h.Trim();
        if (t.Length == 0) return C.None;
        if (h.Contains(" MA HIEU ") || t == "MA" || h.Contains(" MA DINH MUC ") || h.Contains(" MA CONG TAC ") || t == "MHDM" || t == "MSDM") return C.Code;
        bool mat = h.Contains(" VAT LIEU ") || h.Contains(" VAT TU ") || h.Contains(" HAO PHI ") || h.Contains(" THANH PHAN ") || t == "VL";
        if (h.Contains(" DON VI ") || t == "DVT" || t == "DV")
            return mat || h.Contains(" VL ") || h.Contains(" VT ") ? C.MatUnit : C.Unit;
        if (h.Contains(" DINH MUC ") || h.Contains(" SO LUONG ") || h.Contains(" GIA TRI ") || h.Contains(" KHOI LUONG ")) return C.Value;
        if (mat) return C.Material;
        if (h.Contains(" TEN ") || h.Contains(" CONG TAC ") || h.Contains(" NOI DUNG ") || h.Contains(" HANG MUC ")) return C.Name;
        return C.None;
    }

    private static double ParseNum(string s, out bool ok)
    {
        s = (s ?? "").Trim().Replace(" ", "");
        ok = false;
        if (s.Length == 0) return 0;
        if (s.Contains(',') && !s.Contains('.')) s = s.Replace(',', '.');
        else if (s.Contains(',') && s.Contains('.')) s = s.Replace(",", "");
        var m = Regex.Match(s, @"^-?\d+(\.\d+)?([eE][-+]?\d+)?$");
        if (!m.Success) return 0;
        ok = double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v);
        return ok ? v : 0;
    }

    /// <summary>Đọc .xlsx/.xlsm (sheet đầu tiên) hoặc .csv/.txt.</summary>
    public static List<string[]> ReadRows(string path)
    {
        string ext = Path.GetExtension(path ?? "").ToLowerInvariant();
        return ext is ".xlsx" or ".xlsm" ? LocationDb.ReadXlsxRows(path) : LocationDb.ReadCsvRows(File.ReadAllBytes(path));
    }

    /// <summary>Nhập file vào QuantitySettings (thay ImportedNorms, áp cấp phối bê tông/vữa, cập nhật ghi chú nguồn).</summary>
    public static NormImportResult ImportFile(QuantitySettings qs, string path)
    {
        var res = ImportRows(qs, ReadRows(path));
        if (qs != null && res.Rows > 0)
        {
            string name = Path.GetFileName(path);
            qs.ImportedNormFile = name + ", nhập " + DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            if (res.Concrete + res.Mortar > 0)
                qs.NormSource = $"Cấp phối bê tông, vữa nhập từ file \"{name}\" – bảng định mức do người dùng cung cấp theo Định mức xây dựng ban hành kèm {Std.Norms} "
                              + $"(Phụ lục VII – định mức vật liệu, thay thế theo {Std.NormLetter}); người dùng chịu trách nhiệm đối chiếu với định mức hiện hành.";
        }
        return res;
    }

    /// <summary>Nhập các dòng (dòng tiêu đề tự nhận dạng).</summary>
    public static NormImportResult ImportRows(QuantitySettings qs, List<string[]> rows)
    {
        var res = new NormImportResult();
        if (qs == null || rows == null || rows.Count == 0) { res.Messages.Add("Bảng trống."); return res; }
        // ---- dòng tiêu đề ----
        int hdr = -1; C[] cols = null;
        for (int i = 0; i < Math.Min(rows.Count, 30) && hdr < 0; i++)
        {
            var cl = rows[i].Select(Classify).ToArray();
            if (cl.Contains(C.Value) && cl.Contains(C.Material) && (cl.Contains(C.Code) || cl.Contains(C.Name))) { hdr = i; cols = cl; }
        }
        if (hdr < 0)
        {
            res.Messages.Add("Không tìm thấy dòng tiêu đề (cần các cột: " + string.Join(", ", Headers) + ").");
            return res;
        }
        int Idx(C c) => Array.IndexOf(cols, c);
        string Get(string[] r, C c) { int i = Idx(c); return i >= 0 && i < r.Length ? (r[i] ?? "").Trim() : ""; }

        string code = "", name = "", unit = "";
        bool skipGroup = false;
        for (int i = hdr + 1; i < rows.Count; i++)
        {
            var r = rows[i];
            if (r == null || r.All(x => string.IsNullOrWhiteSpace(x))) continue;
            string c0 = Get(r, C.Code), n0 = Get(r, C.Name), u0 = Get(r, C.Unit);
            if (r.Any(x => LocationDb.Norm(x).Contains("VI DU")))
            {
                res.Messages.Add($"Dòng {i + 1}: dòng ví dụ – bỏ qua.");
                code = name = unit = ""; skipGroup = true;
                continue;
            }
            if (c0.Length > 0 || n0.Length > 0) { code = c0; name = n0.Length > 0 ? n0 : name; unit = u0; skipGroup = false; }
            else if (u0.Length > 0) unit = u0;
            if (skipGroup || (code.Length == 0 && name.Length == 0)) continue;
            string mat = Get(r, C.Material);
            double v = ParseNum(Get(r, C.Value), out bool ok);
            if (mat.Length == 0 || !ok) continue;   // dòng tiêu đề nhóm / không có số
            res.Items.Add(new NormRow { Code = code, Name = name, Unit = unit, Material = mat, MaterialUnit = Get(r, C.MatUnit), Value = v });
        }
        res.Rows = res.Items.Count;
        if (res.Rows == 0) { res.Messages.Add("Không có dòng định mức hợp lệ (vật liệu + định mức là số)."); return res; }

        qs.ConcreteNorms ??= new List<ConcreteMixNorm>();
        qs.MortarNorms ??= new List<MortarMixNorm>();
        var touchedC = new HashSet<string>(); var touchedM = new HashSet<string>();
        foreach (var it in res.Items)
        {
            string nn = " " + LocationDb.Norm(it.Name) + " ";
            string mm = LocationDb.Norm(it.Material);
            bool mortar = nn.Contains(" VUA ");
            bool concrete = !mortar && nn.Contains(" BE TONG ");
            string part = Part(mm, concrete);
            if ((!concrete && !mortar) || part == null) { res.Other++; continue; }
            double val = Convert(part, it.Value, LocationDb.Norm(it.MaterialUnit));
            if (concrete)
            {
                string grade = ConcreteGrade(it.Name, qs);
                if (grade == null) { res.Other++; res.Messages.Add($"\"{it.Name}\": không nhận ra cấp độ bền bê tông – lưu tham khảo."); continue; }
                var n = qs.ConcreteNorm(grade);
                if (n == null) { n = new ConcreteMixNorm { Grade = grade }; qs.ConcreteNorms.Add(n); }
                if (touchedC.Add(grade)) n.Note = "Nhập từ Excel" + (it.Code.Length > 0 ? " – " + it.Code : "");
                switch (part) { case "XM": n.Cement = val; break; case "CAT": n.Sand = val; break; case "DA": n.Stone = val; break; case "NUOC": n.Water = val; break; }
            }
            else
            {
                string grade = MortarGrade(it.Name);
                if (grade == null || part == "DA") { res.Other++; res.Messages.Add($"\"{it.Name}\": không nhận ra mác vữa – lưu tham khảo."); continue; }
                var n = qs.MortarNorm(grade);
                if (n == null) { n = new MortarMixNorm { Grade = grade }; qs.MortarNorms.Add(n); }
                if (touchedM.Add(grade)) n.Note = "Nhập từ Excel" + (it.Code.Length > 0 ? " – " + it.Code : "");
                switch (part) { case "XM": n.Cement = val; break; case "CAT": n.Sand = val; break; case "NUOC": n.Water = val; break; }
            }
        }
        res.Concrete = touchedC.Count; res.Mortar = touchedM.Count;
        qs.ImportedNorms = res.Items.ToList();
        return res;
    }

    /// <summary>Thành phần cấp phối: "XM", "CAT", "DA", "NUOC"; null = vật liệu khác.</summary>
    private static string Part(string m, bool concrete)
    {
        if (m.StartsWith("XI MANG") || m == "XM" || m.StartsWith("XM ")) return "XM";
        if (m.StartsWith("CAT")) return "CAT";
        if (m == "DA" || m.StartsWith("DA ") || m.StartsWith("SOI")) return "DA";
        if (m.StartsWith("NUOC")) return "NUOC";
        return null;
    }

    /// <summary>Đổi đơn vị về đơn vị của bảng cấp phối: xi măng kg, cát/đá m³, nước lít.</summary>
    private static double Convert(string part, double v, string unitNorm)
    {
        string u = (unitNorm ?? "").Replace(" ", "");
        if (part == "XM" && (u == "TAN" || u == "T")) return v * 1000;
        if (part == "NUOC" && (u == "M3" || u == "M")) return v * 1000;
        return v;
    }

    private static readonly Regex RxB = new(@"(?<![A-Za-z0-9])B\s*(\d+(?:[.,]\d+)?)(?![\d])", RegexOptions.Compiled);
    private static readonly Regex RxMac = new(@"\bMAC\s*(\d+)\b", RegexOptions.Compiled);
    private static readonly Regex RxM = new(@"(?<![A-Z0-9])M\s*(\d+)\b", RegexOptions.Compiled);

    /// <summary>Cấp độ bền từ tên công tác: "… B20 …" → "B20"; "… mác 250 …" → cấp có Mark = "M250" trong bảng (không có → null).</summary>
    public static string ConcreteGrade(string name, QuantitySettings qs = null)
    {
        var m = RxB.Match((name ?? "").ToUpperInvariant());
        if (m.Success) return "B" + m.Groups[1].Value.Replace(',', '.');
        var mm = RxMac.Match(LocationDb.Norm(name));
        if (!mm.Success) mm = RxM.Match(LocationDb.Norm(name));
        if (mm.Success)
        {
            string mark = "M" + mm.Groups[1].Value;
            var n = (qs?.ConcreteNorms ?? QuantitySettings.DefaultConcreteNorms()).FirstOrDefault(x => string.Equals((x.Mark ?? "").Trim(), mark, StringComparison.OrdinalIgnoreCase));
            return n?.Grade;
        }
        return null;
    }

    /// <summary>Mác vữa từ tên công tác: "Vữa xây M75", "vữa mác 75" → "M75".</summary>
    public static string MortarGrade(string name)
    {
        string n = LocationDb.Norm(name);
        var m = RxMac.Match(n);
        if (!m.Success) m = RxM.Match(n);
        return m.Success ? "M" + m.Groups[1].Value : null;
    }

    /// <summary>Ghi file mẫu .xlsx (tiêu đề + 2 dòng ví dụ ghi "VÍ DỤ" – bị bỏ qua khi nhập).</summary>
    public static void WriteTemplate(string path)
    {
        var x = new XlsxWriter();
        var sh = x.AddSheet("Dinh muc");
        sh.AddHeaderRow(Headers.Cast<object>().ToArray());
        sh.AddRow(ExampleMark, "VÍ DỤ – Cấp phối bê tông B20 (thay bằng số liệu định mức hiện hành)", "m³", "Xi măng", "kg", 0);
        sh.AddRow("", "", "", "Cát vàng", "m³", 0);
        sh.SetColumnWidths(16, 60, 10, 24, 14, 12);
        x.Save(path);
    }
}

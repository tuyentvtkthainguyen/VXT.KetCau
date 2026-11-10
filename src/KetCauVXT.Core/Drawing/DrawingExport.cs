using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Kiểu xuất bản vẽ.</summary>
public enum DrawingExportMode
{
    /// <summary>Tách rời: mỗi bản vẽ / tờ một file (như trước).</summary>
    Split,
    /// <summary>Gộp 1 file: mọi bản vẽ đặt cạnh nhau trong một file DXF (PDF: một file nhiều trang).</summary>
    Single
}

/// <summary>Tuỳ chọn xuất bản vẽ: gộp 1 file / tách rời, mẫu tên file. Lựa chọn gần nhất được nhớ trong %AppData%\KetCauVXT\XuatBanVe.json.
/// Mẫu tên: {so} số hiệu bản vẽ/tờ, {ten} tên bản vẽ, {duan} tên công trình, {stt} thứ tự 01, 02…, {ngay} yyyyMMdd.</summary>
public sealed class DrawingExportOptions
{
    public DrawingExportMode Mode { get; set; } = DrawingExportMode.Split;
    /// <summary>Mẫu tên file khi tách rời; rỗng = tên mặc định (KC-01_MatBangKetCau.dxf…).</summary>
    public string FilePattern { get; set; } = "";
    /// <summary>Tên file khi gộp (không phần mở rộng).</summary>
    public string SingleFileName { get; set; } = "{duan}_BoBanVe";
    /// <summary>Định dạng file (DXF R12 / DXF 2018 / DWG 2018); null = theo Chuẩn layer CAD (CadStandard.OutputFormat).</summary>
    public CadOutputFormat? Format { get; set; }
    /// <summary>[Chi tiết rời] true = không lồng khung tờ / khung tên: mỗi chi tiết một khung bao để người dùng tự dàn trang in trong AutoCAD.</summary>
    public bool DetailsOnly { get; set; }

    public static DrawingExportOptions Split => new() { Mode = DrawingExportMode.Split };
    public static DrawingExportOptions SingleFile(string name = null) => new() { Mode = DrawingExportMode.Single, SingleFileName = string.IsNullOrWhiteSpace(name) ? "{duan}_BoBanVe" : name };

    public DrawingExportOptions Clone() => new() { Mode = Mode, FilePattern = FilePattern, SingleFileName = SingleFileName, Format = Format, DetailsOnly = DetailsOnly };

    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true, Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>File lưu lựa chọn của người dùng (cùng thư mục với CadStandard.json).</summary>
    public static string UserFilePath => Path.Combine(CadStandard.UserFolder, "XuatBanVe.json");

    public static DrawingExportOptions LoadUser()
    {
        try
        {
            if (File.Exists(UserFilePath))
                return JsonSerializer.Deserialize<DrawingExportOptions>(File.ReadAllText(UserFilePath), Opts) ?? new DrawingExportOptions();
        }
        catch (Exception) { /* file hỏng → mặc định */ }
        return new DrawingExportOptions();
    }

    public void SaveUser()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(UserFilePath));
            File.WriteAllText(UserFilePath, JsonSerializer.Serialize(this, Opts), new UTF8Encoding(false));
        }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Tên file theo mẫu (đã bỏ ký tự không hợp lệ, chưa có phần mở rộng).</summary>
    public static string ApplyPattern(string pattern, string number, string title, string project, int index)
    {
        string s = (pattern ?? "")
            .Replace("{so}", number ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{ten}", title ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{duan}", project ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{stt}", index.ToString("00", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{ngay}", DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
        if (s.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 4);
        return DrawUtil.SafeFile(s);
    }
}

/// <summary>Một bản vẽ đã dựng (một file khi xuất tách rời).</summary>
public sealed class DrawingItem
{
    /// <summary>Khoá bản vẽ ("DS:KC-01_MatBangKetCau", "SHEET:KC-03", "SHOP:…") – dùng cho chỉnh sửa, khoá bản vẽ.</summary>
    public string Key = "";
    /// <summary>Tên file mặc định (có .dxf).</summary>
    public string FileName = "";
    /// <summary>Số hiệu "KC-01".</summary>
    public string Number = "";
    public string Title = "";
    /// <summary>Mẫu số tỷ lệ của bản vẽ (1 = mm giấy).</summary>
    public double Scale = 1;
    /// <summary>Nội dung (đã áp lớp chỉnh sửa; bản đã khoá nếu có). null = chưa dựng.</summary>
    public DxfWriter W;
    /// <summary>Bản vẽ đã khoá đang dùng thay bản tự sinh (null = không khoá).</summary>
    public FrozenDrawing Frozen;
    /// <summary>Cảnh báo (bản vẽ đã khoá, mô hình thay đổi…).</summary>
    public string Warning;
    /// <summary>Mã băm bản tự sinh (để khoá / so sánh).</summary>
    public string GeneratedHash;

    public override string ToString() => $"{Number} – {Title}" + (Frozen != null ? " [ĐÃ KHOÁ]" : "");
}

/// <summary>Kết quả xuất.</summary>
public sealed class DrawingExportResult
{
    public List<string> Files { get; } = new();
    /// <summary>Cảnh báo (bản vẽ đã khoá – mô hình thay đổi…; chỉnh sửa không còn áp dụng được).</summary>
    public List<string> Messages { get; } = new();
    /// <summary>Khi gộp 1 file: vùng chiếm chỗ của từng bản vẽ trong file (để kiểm tra không chồng nhau).</summary>
    public List<(string Key, Rect2 Region)> Regions { get; } = new();
    public OverlayReport Report { get; set; } = new();
    public int Count => Files.Count;
}

/// <summary>Ghi các bản vẽ đã dựng: tách rời (mỗi bản một file) hoặc gộp 1 file (đặt cạnh nhau, mỗi bản có khung bao + tên phía trên).</summary>
public static class DrawingExport
{
    /// <summary>Ghép các bản vẽ cạnh nhau (trái → phải) trong một bộ ghi: mỗi bản đặt trong vùng riêng có khung bao (lớp KHUNG_TEN) và tên
    /// "KC-01 – MẶT BẰNG…" phía trên; bản vẽ giữ nguyên tỷ lệ và toạ độ tương đối (đo được trong CAD). regions: vùng của từng bản.</summary>
    public static DxfWriter Combine(IList<DrawingItem> items, List<(string Key, Rect2 Region)> regions = null)
    {
        var list = (items ?? new List<DrawingItem>()).Where(i => i?.W != null && i.W.EntityCount > 0).ToList();
        double maxS = list.Count == 0 ? 1 : list.Max(i => i.Scale > 0 ? i.Scale : i.W.Scale);
        var outW = new DxfWriter(maxS);
        double x = 0;
        foreach (var it in list)
        {
            var e = it.W.Extents;
            if (e.IsEmpty) continue;
            double S = it.Scale > 0 ? it.Scale : it.W.Scale;
            if (S <= 0) S = 1;
            double pad = 10 * S, th = 7 * S;
            double rw = e.W + 2 * pad, rh = e.H + 2 * pad;
            outW.Append(it.W, x + pad - e.X1, pad - e.Y1);
            outW.Rect(Lay.KhungTen, x, 0, x + rw, rh);
            string cap = (string.IsNullOrEmpty(it.Number) ? "" : it.Number + " – ") + (it.Title ?? "") + (it.Frozen != null ? " [ĐÃ KHOÁ]" : "");
            var tb = outW.Text(Lay.Chu, x, rh + 0.8 * th, th, cap);
            double regW = Math.Max(rw, tb.IsEmpty ? 0 : tb.X2 - x);
            var region = new Rect2(x, 0, x + regW, rh + 0.8 * th + th);
            regions?.Add((it.Key, region));
            double gap = Math.Max(30 * maxS, 0.05 * regW);
            x += regW + gap;
        }
        return outW;
    }

    /// <summary>Ghi các bản vẽ vào thư mục theo tuỳ chọn. Bản vẽ đã khoá khi tách rời được ghi nguyên văn file đã nạp.
    /// Định dạng: o.Format → CadFileWriters.Current (Chuẩn layer CAD); DWG → phần mở rộng .dwg (Files ghi đúng đường dẫn thực).</summary>
    public static DrawingExportResult Write(IList<DrawingItem> items, string folder, DrawingExportOptions o, string projectName, DrawingExportResult res = null)
    {
        o ??= DrawingExportOptions.Split;
        var fmt = o.Format ?? CadFileWriters.Current;
        res ??= new DrawingExportResult();
        Directory.CreateDirectory(folder);
        var list = (items ?? new List<DrawingItem>()).Where(i => i?.W != null).ToList();
        foreach (var it in list) if (!string.IsNullOrEmpty(it.Warning) && !res.Messages.Contains(it.Warning)) res.Messages.Add(it.Warning);
        if (o.Mode == DrawingExportMode.Single)
        {
            if (list.Count == 0) return res;
            var w = Combine(list, res.Regions);
            string name = DrawingExportOptions.ApplyPattern(string.IsNullOrWhiteSpace(o.SingleFileName) ? "{duan}_BoBanVe" : o.SingleFileName,
                list.Count == 1 ? list[0].Number : "", list.Count == 1 ? list[0].Title : "", projectName, 1);
            if (string.IsNullOrWhiteSpace(name) || name == "Khac") name = "BoBanVe";
            string path = Path.Combine(folder, name + ".dxf");
            res.Files.Add(CadFileWriters.Write(w, path, fmt));
            return res;
        }
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int idx = 0;
        foreach (var it in list)
        {
            idx++;
            string fn = string.IsNullOrWhiteSpace(o.FilePattern) ? it.FileName
                : DrawingExportOptions.ApplyPattern(o.FilePattern, it.Number, it.Title, projectName, idx) + ".dxf";
            if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(fn))) fn = DrawUtil.SafeFile(it.Number) + ".dxf";
            string baseName = Path.GetFileNameWithoutExtension(fn);
            for (int k = 2; !used.Add(fn); k++) fn = baseName + "_" + k.ToString(CultureInfo.InvariantCulture) + ".dxf";
            string path = Path.Combine(folder, fn);
            if (it.Frozen != null && DrawingOverlay.RawBytes(it.Frozen).Length > 0) res.Files.AddRange(DrawingOverlay.WriteFrozen(it.Frozen, path).Where(f => f.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase)));
            else res.Files.Add(CadFileWriters.Write(it.W, path, fmt));
        }
        return res;
    }

    /// <summary>Thay bản tự sinh bằng bản đã khoá (nếu có) và ghi cảnh báo. Gọi sau khi đã áp lớp chỉnh sửa.</summary>
    public static void UseFrozen(Project p, DrawingItem it)
    {
        if (p == null || it?.W == null || DrawingOverlay.Bypassed || DrawingOverlay.FrozenIgnored) return;
        var fz = DrawingOverlay.FrozenOf(p, it.Key);
        if (fz == null) return;
        var fw = DrawingOverlay.FrozenWriter(fz);
        if (fw == null || fw.EntityCount == 0) { it.Warning = $"Bản vẽ đã khoá {it.Key}: không đọc được file {fz.FileName} – dùng bản tự sinh."; return; }
        it.GeneratedHash ??= it.W.ContentHash();
        it.Frozen = fz;
        it.Warning = DrawingOverlay.FrozenWarning(fz, it.GeneratedHash);
        it.W = fw;
        DrawingOverlay.Current?.Warn(it.Warning);
    }
}

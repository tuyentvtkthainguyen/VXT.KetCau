using System.Text.Json;

namespace KetCauVXT.Ui;

// =====================================================================
// GIAO DIỆN GỌN – lưu bố cục cửa sổ của người dùng (chỉ trên máy: %AppData%\VietConst\ui.json).
// Không gửi đi đâu, không chứa dữ liệu dự án; đọc / ghi lỗi → dùng mặc định.
// =====================================================================

/// <summary>Bố cục một cửa sổ (MainWindow, DetailingWindow, DetailSheetWindow…).</summary>
public sealed class WindowLayout
{
    /// <summary>Ẩn thanh menu (chỉ còn nút ☰; Alt hiện tạm thời).</summary>
    public bool MenuHidden { get; set; }
    /// <summary>Ẩn thanh công cụ (Ctrl+F1 – như ribbon Office).</summary>
    public bool ToolbarHidden { get; set; }
    /// <summary>Độ rộng các khung bên (theo khoá khung: "trai", "phai", "cay", "thuoctinh"…), px.</summary>
    public Dictionary<string, double> PanelWidths { get; set; } = new();
    /// <summary>Các khung đang thu gọn.</summary>
    public List<string> Collapsed { get; set; } = new();
}

/// <summary>Thiết lập giao diện chung + bố cục từng cửa sổ.</summary>
public sealed class UiLayoutSettings
{
    public const string DensityCompact = "Gọn", DensityStandard = "Tiêu chuẩn";
    /// <summary>Mật độ giao diện: "Gọn" (mặc định – chữ 12, nút biểu tượng) hoặc "Tiêu chuẩn" (biểu tượng + chữ).</summary>
    public string Density { get; set; } = DensityCompact;
    public Dictionary<string, WindowLayout> Windows { get; set; } = new();

    public bool Compact => !string.Equals(Density, DensityStandard, StringComparison.OrdinalIgnoreCase);
    /// <summary>Cỡ chữ khung bên / thanh trạng thái.</summary>
    public double PanelFontSize => Compact ? 12 : 13;
    public double StatusBarHeight => Compact ? 20 : 24;
    public double ToolbarHeight => Compact ? 28 : 32;

    public WindowLayout For(string window)
    {
        window ??= "";
        if (!Windows.TryGetValue(window, out var w) || w == null) Windows[window] = w = new WindowLayout();
        w.PanelWidths ??= new(); w.Collapsed ??= new();
        return w;
    }

    private static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };
    public string ToJson() => JsonSerializer.Serialize(this, Opt);
    public static UiLayoutSettings FromJson(string json)
    {
        try
        {
            var s = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<UiLayoutSettings>(json);
            s ??= new UiLayoutSettings();
            s.Windows ??= new();
            if (string.IsNullOrWhiteSpace(s.Density)) s.Density = DensityCompact;
            return s;
        }
        catch { return new UiLayoutSettings(); }
    }

    /// <summary>Đường dẫn mặc định: %AppData%\VietConst\ui.json.</summary>
    public static string DefaultPath
    {
        get
        {
            try { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VietConst", "ui.json"); }
            catch { return null; }
        }
    }

    public static UiLayoutSettings Load(string path = null)
    {
        try
        {
            path ??= DefaultPath;
            return path != null && File.Exists(path) ? FromJson(File.ReadAllText(path)) : new UiLayoutSettings();
        }
        catch { return new UiLayoutSettings(); }
    }

    public bool Save(string path = null)
    {
        try
        {
            path ??= DefaultPath;
            if (path == null) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, ToJson());
            return true;
        }
        catch { return false; }
    }
}

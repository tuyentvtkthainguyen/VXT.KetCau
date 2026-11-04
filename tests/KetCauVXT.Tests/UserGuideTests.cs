using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using KetCauVXT.Docs;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng hướng dẫn sử dụng (Docs/UserGuide*.cs): file Word / Markdown, tiêu đề chương, đánh dấu, đường dẫn menu so với
/// MainWindow.xaml, HelpId dùng trong mã giao diện, ánh xạ ngữ cảnh (tab, khung nhìn, chế độ sửa mặt bằng). check(tên, giá trị, kỳ vọng, sai số).
/// Ghi outDir/HuongDan/HuongDanSuDung.docx + .md (bộ cài lấy file .docx).</summary>
public static class UserGuideTests
{
    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        var chs = UserGuide.Chapters();
        var all = UserGuide.AllSections().ToList();
        check("Hướng dẫn: ≥ 14 chương", chs.Count >= 14 ? 1 : 0, 1, 0);
        check("Hướng dẫn: ≥ 120 mục", all.Count >= 120 ? 1 : 0, 1, 0);
        check("Hướng dẫn: mọi mục có HelpId", all.Count(s => string.IsNullOrWhiteSpace(s.HelpId)), 0, 0);
        var dup = all.GroupBy(s => s.HelpId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        foreach (var d in dup) Console.WriteLine("    HelpId trùng: " + d);
        check("Hướng dẫn: HelpId không trùng", dup.Count, 0, 0);
        check("Hướng dẫn: HelpId chỉ gồm a-z 0-9 . -", all.Count(s => !Regex.IsMatch(s.HelpId, "^[a-z0-9.-]+$")), 0, 0);
        check("Hướng dẫn: mọi mục có nội dung hoặc mục con", all.Count(s => s.Blocks.Count == 0 && s.Children.Count == 0), 0, 0);
        check("Hướng dẫn: có ví dụ nhà phố 4 tầng", UserGuide.Find("quy-trinh.vi-du")?.Children.Count >= 5 ? 1 : 0, 1, 0);
        check("Hướng dẫn: ví dụ dùng số liệu NhaPho (4.5 4.5 4.5; 3.9 3.3 3.3 3.3; -0.6)",
            UserGuide.Find("quy-trinh.vi-du.khoi-tao")?.OwnText is string ex && ex.Contains("4.5 4.5 4.5") && ex.Contains("3.9 3.3 3.3 3.3") && ex.Contains("-0.6") ? 1 : 0, 1, 0);

        // ---- đánh dấu: {m:…} và {s:…} đều tồn tại ----
        var badMenus = UserGuide.UsedMenuIds().Where(id => UserGuide.Menus.All(m => m.Id != id)).ToList();
        foreach (var b in badMenus) Console.WriteLine("    {m:" + b + "} không có trong bảng menu");
        check("Hướng dẫn: mọi {m:…} có trong bảng đường dẫn menu", badMenus.Count, 0, 0);
        var badRefs = UserGuide.UsedSectionRefs().Where(id => !UserGuide.Exists(id)).ToList();
        foreach (var b in badRefs) Console.WriteLine("    {s:" + b + "} không có mục");
        check("Hướng dẫn: mọi tham chiếu {s:…} tồn tại", badRefs.Count, 0, 0);

        // ---- Word / Markdown ----
        string dir = Path.Combine(outDir, "HuongDan");
        var (docx, mdPath) = UserGuide.SaveAll(dir);
        check("Hướng dẫn: đã ghi .docx", File.Exists(docx) && new FileInfo(docx).Length > 20000 ? 1 : 0, 1, 0);
        string docXml = "";
        try
        {
            using var z = ZipFile.OpenRead(docx);
            check("Hướng dẫn: .docx là zip hợp lệ có word/document.xml", z.GetEntry("word/document.xml") != null ? 1 : 0, 1, 0);
            check("Hướng dẫn: .docx có chân trang số trang", z.GetEntry("word/footer1.xml") != null ? 1 : 0, 1, 0);
            using var sr = new StreamReader(z.GetEntry("word/document.xml").Open(), Encoding.UTF8);
            docXml = sr.ReadToEnd();
            XDocument.Parse(docXml);   // XML hợp lệ
            check("Hướng dẫn: document.xml là XML hợp lệ", 1, 1, 0);
        }
        catch (Exception exz) { Console.WriteLine("    lỗi đọc docx: " + exz.Message); check("Hướng dẫn: .docx đọc được", 0, 1, 0); }
        check("Hướng dẫn: .docx có mục lục (TOC)", docXml.Contains("TOC \\o") ? 1 : 0, 1, 0);
        int missDocx = chs.Count(c => !docXml.Contains(DocxWriter.Esc(c.FullTitle)));
        check("Hướng dẫn: .docx chứa mọi tiêu đề chương", missDocx, 0, 0);
        string md = File.Exists(mdPath) ? File.ReadAllText(mdPath, Encoding.UTF8) : "";
        check("Hướng dẫn: Markdown không rỗng (> 30 000 ký tự)", md.Length > 30000 ? 1 : 0, 1, 0);
        check("Hướng dẫn: Markdown chứa mọi tiêu đề chương", chs.Count(c => !md.Contains("## " + c.FullTitle)), 0, 0);
        check("Hướng dẫn: không còn đánh dấu chưa giải ({m:, {s:, [menu ?, [mục ?)",
            (md.Contains("{m:") || md.Contains("{s:") || md.Contains("[menu ?") || md.Contains("[mục ?") || docXml.Contains("{m:") || docXml.Contains("[menu ?")) ? 1 : 0, 0, 0);
        Console.WriteLine($"    {chs.Count} chương, {all.Count} mục, Markdown {md.Length} ký tự → {dir}");

        // ---- trợ giúp theo ngữ cảnh: mọi HelpId của bảng ánh xạ, Bắt đầu nhanh tồn tại ----
        var badMap = HelpContext.AllMappedIds().Where(id => !UserGuide.Exists(id)).ToList();
        foreach (var b in badMap) Console.WriteLine("    HelpId ánh xạ không có mục: " + b);
        check("Trợ giúp: mọi HelpId của ánh xạ ngữ cảnh / Bắt đầu nhanh tồn tại", badMap.Count, 0, 0);
        check("Bắt đầu nhanh: 10 bước có tiêu đề", QuickStart.Steps.Count(s => !string.IsNullOrWhiteSpace(s.Title)), 10, 0);
        var empty = KetCauVXT.Model.ModelBuilder.NewProject("thử");
        check("Bắt đầu nhanh: dự án rỗng – bước Phân tích chưa làm", QuickStart.Evaluate(QuickStart.Steps.First(s => s.Id == "phan-tich"), empty, false, null) == false ? 1 : 0, 1, 0);
        check("Bắt đầu nhanh: dự án rỗng – bước Cột, dầm, sàn chưa làm", QuickStart.Evaluate(QuickStart.Steps.First(s => s.Id == "cau-kien"), empty, false, null) == false ? 1 : 0, 1, 0);

        // ---- đối chiếu mã giao diện (cần thư mục mã nguồn) ----
        string root = FindRepoRoot();
        if (root == null) { Console.WriteLine("    (bỏ qua đối chiếu MainWindow.xaml / mã giao diện: không tìm thấy KetCauVXT.sln từ " + AppContext.BaseDirectory + ")"); return; }
        string appDir = Path.Combine(root, "src", "KetCauVXT.App");
        string xamlPath = Path.Combine(appDir, "MainWindow.xaml");
        if (!File.Exists(xamlPath)) { Console.WriteLine("    (bỏ qua: không có " + xamlPath + ")"); return; }
        var appCode = Directory.GetFiles(appDir, "*.cs").ToDictionary(f => f, f => File.ReadAllText(f, Encoding.UTF8));

        // (1) đường dẫn menu
        XDocument x;
        try { x = XDocument.Load(xamlPath); }
        catch (Exception exx) { Console.WriteLine("    không đọc được MainWindow.xaml: " + exx.Message); check("Hướng dẫn: đọc được MainWindow.xaml", 0, 1, 0); return; }
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var leaves = new HashSet<string>(StringComparer.Ordinal);
        var menu = x.Descendants().FirstOrDefault(e => e.Name.LocalName == "Menu");
        void Walk(XElement e, string prefix)
        {
            foreach (var mi in e.Elements().Where(c => c.Name.LocalName == "MenuItem"))
            {
                string h = NormHeader((string)mi.Attribute("Header") ?? "");
                string p = prefix.Length == 0 ? h : prefix + " → " + h;
                paths.Add(p); leaves.Add(h);
                Walk(mi, p);
            }
        }
        if (menu != null) Walk(menu, "");
        var missing = new List<string>(); var inCode = new List<string>(); var planned = new List<string>();
        foreach (var m in UserGuide.Menus)
        {
            string p = string.Join(" → ", m.Path.Select(NormHeader));
            if (paths.Contains(p)) continue;
            if (m.Planned) { planned.Add(m.Display); continue; }
            // menu dựng bằng mã: chữ của mục lá có trong một file .cs của giao diện
            string leaf = m.Path[^1];
            if (appCode.Values.Any(c => c.Contains("\"" + leaf + "\"") || c.Contains("\"" + leaf.Replace("&", "&amp;") + "\""))) { inCode.Add(m.Display); continue; }
            missing.Add(m.Display + "  [" + m.Id + "]");
        }
        foreach (var p in planned) Console.WriteLine("    menu [sắp có] chưa có trong MainWindow.xaml: " + p);
        foreach (var p in inCode) Console.WriteLine("    menu không có trong MainWindow.xaml nhưng có chữ trong mã .cs: " + p);
        foreach (var p in missing) Console.WriteLine("    THIẾU menu trong MainWindow.xaml: " + p);
        check($"Hướng dẫn: mọi đường dẫn menu có trong MainWindow.xaml ({paths.Count} mục menu đọc được)", missing.Count, 0, 0);

        // (2) HelpId dùng trong mã giao diện
        var rx = new Regex("(?:Help\\.Attach\\([^,]+,\\s*|HelpButton\\.Create\\(\\s*|Help\\.SetId\\([^,]+,\\s*|Help\\.Show\\(\\s*|OpenUserGuide\\(\\s*|ShowHelp\\(\\s*)\"([^\"]+)\"");
        var used = appCode.SelectMany(kv => rx.Matches(kv.Value).Select(mt => (File: Path.GetFileName(kv.Key), Id: mt.Groups[1].Value))).ToList();
        var badUsed = used.Where(u => !UserGuide.Exists(u.Id)).ToList();
        foreach (var b in badUsed) Console.WriteLine($"    HelpId \"{b.Id}\" ({b.File}) không có trong hướng dẫn");
        check($"Trợ giúp: mọi HelpId dùng trong mã giao diện tồn tại ({used.Count} chỗ)", badUsed.Count, 0, 0);
        int attached = appCode.Values.Sum(c => Regex.Matches(c, @"Help\.Attach\(this,").Count);
        check($"Trợ giúp: các hộp thoại chính có nút \"?\" (Help.Attach: {attached} ≥ 14)", attached >= 14 ? 1 : 0, 1, 0);

        // (3) mọi tab của cửa sổ chính có mục hướng dẫn
        var tabs = x.Descendants().Where(e => e.Name.LocalName == "TabItem").Select(e => (string)e.Attribute("Header")).Where(h => !string.IsNullOrWhiteSpace(h)).Distinct().ToList();
        tabs.Add("Dao động");   // khung nhìn dựng bằng mã (MainWindow.Modal.cs)
        var noCtx = tabs.Where(h => (HelpContext.ForLeftTab(h) ?? HelpContext.ForViewTab(h) ?? HelpContext.ForLeftTab("Thiết kế", h)) is not string id || !UserGuide.Exists(id)).ToList();
        foreach (var h in noCtx) Console.WriteLine("    tab \"" + h + "\" chưa có ánh xạ trợ giúp");
        check($"Trợ giúp: mọi tab ({tabs.Count}) ánh xạ tới mục hướng dẫn có thật", noCtx.Count, 0, 0);

        // (4) mọi chế độ sửa mặt bằng (enum PlanEditMode) có mục hướng dẫn
        string edit = appCode.FirstOrDefault(kv => Path.GetFileName(kv.Key) == "MainWindow.Edit.cs").Value ?? "";
        var em = Regex.Match(edit, @"enum\s+PlanEditMode\s*\{(.*?)\}", RegexOptions.Singleline);
        var modes = em.Success ? Regex.Replace(em.Groups[1].Value, @"/\*.*?\*/|//[^\n]*", "", RegexOptions.Singleline).Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList() : new List<string>();
        var noMode = modes.Where(m => HelpContext.ForPlanMode(m) is not string id || !UserGuide.Exists(id)).ToList();
        foreach (var m in noMode) Console.WriteLine("    chế độ mặt bằng \"" + m + "\" chưa có ánh xạ trợ giúp");
        check($"Trợ giúp: mọi chế độ sửa mặt bằng ({modes.Count}) có mục hướng dẫn", modes.Count > 0 && noMode.Count == 0 ? 1 : 0, 1, 0);

        // (5) cửa sổ / hộp thoại của giao diện có ánh xạ F1 (chỉ liệt kê, không tính lỗi)
        var winNames = appCode.Values.SelectMany(c => Regex.Matches(c, @"class\s+(\w+)\s*:\s*Window\b").Select(mt => mt.Groups[1].Value)).Distinct().ToList();
        var noWin = winNames.Where(n => HelpContext.ForWindow(n) == null).ToList();
        if (noWin.Count > 0) Console.WriteLine("    cửa sổ chưa có ánh xạ F1 (mở mục mặc định): " + string.Join(", ", noWin));
    }

    /// <summary>Header menu WPF → chữ hiển thị: bỏ '_' phím tắt ("__" = '_'), "..." ≡ "…".</summary>
    private static string NormHeader(string h)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < h.Length; i++)
        {
            if (h[i] == '_') { if (i + 1 < h.Length && h[i + 1] == '_') { sb.Append('_'); i++; } continue; }
            sb.Append(h[i]);
        }
        return sb.ToString().Replace("...", "…").Trim();
    }

    private static string FindRepoRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var d = new DirectoryInfo(start);
            while (d != null)
            {
                if (File.Exists(Path.Combine(d.FullName, "KetCauVXT.sln"))) return d.FullName;
                d = d.Parent;
            }
        }
        return null;
    }
}

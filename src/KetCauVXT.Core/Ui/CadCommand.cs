using KetCauVXT.Model;

namespace KetCauVXT.Ui;

// =====================================================================
// DÒNG LỆNH KIỂU AUTOCAD trên mặt bằng (chế độ Thiết kế – MainWindow.CadCommandLine.cs):
//   • lệnh gõ tắt AutoCAD và tiếng Việt (có / không dấu): L/LINE/DAM → dầm, C/COT, S/SAN, W/TUONG, M/MOVE, CO/COPY,
//     MI/MIRROR, AR/ARRAY, O/OFFSET, TR/TRIM, EX/EXTEND, E/ERASE, U/UNDO, Z/ZOOM, DI/DIST, MA/MATCHPROP, TAI (gán tải)…;
//   • toạ độ: "x,y" tuyệt đối, "@dx,dy" tương đối, "@L<góc" / "L<góc" cực, số đơn = chiều dài theo hướng chuột
//     (dùng BeamTools.ParseCoordinateInput);
//   • F8 trực giao (ortho), F10 bắt góc cực (polar 45°/90°): Constrain().
// Chỉ phân tích chuỗi / hình học – không chạm giao diện (kiểm thử: PlanLoadCadTests).
// =====================================================================

/// <summary>Lệnh mặt bằng.</summary>
public enum CadCmd
{
    None, Beam, Column, Slab, Wall, Move, Copy, Mirror, Array, Offset, Trim, Extend, Stretch, Erase, Undo, Redo,
    Zoom, ZoomExtents, Dist, Match, Load, Select, SelectSimilar, Ortho, Polar, Osnap, Help, Cancel
}

public enum CadInputKind { Empty, Command, Point, Number, Error }

/// <summary>Kết quả phân tích một dòng gõ.</summary>
public sealed class CadCommandInput
{
    public CadInputKind Kind { get; init; }
    public CadCmd Cmd { get; init; }
    /// <summary>Phần còn lại sau tên lệnh (vd. "Z E" → "E").</summary>
    public string Arg { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public string Error { get; init; } = "";
    public string Raw { get; init; } = "";
}

public static class CadCommand
{
    /// <summary>Bảng gõ tắt (đã bỏ dấu, chữ hoa) → lệnh.</summary>
    public static readonly IReadOnlyDictionary<string, CadCmd> Aliases = new Dictionary<string, CadCmd>(StringComparer.Ordinal)
    {
        ["L"] = CadCmd.Beam, ["LINE"] = CadCmd.Beam, ["PL"] = CadCmd.Beam, ["D"] = CadCmd.Beam, ["DAM"] = CadCmd.Beam, ["BEAM"] = CadCmd.Beam,
        ["C"] = CadCmd.Column, ["COT"] = CadCmd.Column, ["COL"] = CadCmd.Column, ["COLUMN"] = CadCmd.Column,
        ["S"] = CadCmd.Slab, ["SAN"] = CadCmd.Slab, ["SLAB"] = CadCmd.Slab, ["REC"] = CadCmd.Slab,
        ["W"] = CadCmd.Wall, ["TUONG"] = CadCmd.Wall, ["WALL"] = CadCmd.Wall,
        ["M"] = CadCmd.Move, ["MOVE"] = CadCmd.Move, ["DOI"] = CadCmd.Move,
        ["CO"] = CadCmd.Copy, ["CP"] = CadCmd.Copy, ["COPY"] = CadCmd.Copy, ["SAOCHEP"] = CadCmd.Copy,
        ["MI"] = CadCmd.Mirror, ["MIRROR"] = CadCmd.Mirror, ["DOIXUNG"] = CadCmd.Mirror,
        ["AR"] = CadCmd.Array, ["ARRAY"] = CadCmd.Array, ["MANG"] = CadCmd.Array,
        ["O"] = CadCmd.Offset, ["OFFSET"] = CadCmd.Offset,
        ["TR"] = CadCmd.Trim, ["TRIM"] = CadCmd.Trim, ["CAT"] = CadCmd.Trim,
        ["EX"] = CadCmd.Extend, ["EXTEND"] = CadCmd.Extend, ["KEODAI"] = CadCmd.Extend,
        ["ST"] = CadCmd.Stretch, ["STRETCH"] = CadCmd.Stretch, ["KEO"] = CadCmd.Stretch,
        ["E"] = CadCmd.Erase, ["ERASE"] = CadCmd.Erase, ["DEL"] = CadCmd.Erase, ["XOA"] = CadCmd.Erase,
        ["U"] = CadCmd.Undo, ["UNDO"] = CadCmd.Undo, ["HOANTAC"] = CadCmd.Undo,
        ["REDO"] = CadCmd.Redo,
        ["Z"] = CadCmd.Zoom, ["ZOOM"] = CadCmd.Zoom, ["ZE"] = CadCmd.ZoomExtents,
        ["DI"] = CadCmd.Dist, ["DIST"] = CadCmd.Dist, ["DO"] = CadCmd.Dist,
        ["MA"] = CadCmd.Match, ["MATCHPROP"] = CadCmd.Match,
        ["TAI"] = CadCmd.Load, ["LOAD"] = CadCmd.Load, ["GANTAI"] = CadCmd.Load,
        ["CH"] = CadCmd.Select, ["CHON"] = CadCmd.Select, ["SEL"] = CadCmd.Select, ["SELECT"] = CadCmd.Select,
        ["SS"] = CadCmd.SelectSimilar, ["SELECTSIMILAR"] = CadCmd.SelectSimilar, ["TUONGTU"] = CadCmd.SelectSimilar,
        ["ORTHO"] = CadCmd.Ortho, ["POLAR"] = CadCmd.Polar, ["OSNAP"] = CadCmd.Osnap, ["OS"] = CadCmd.Osnap,
        ["?"] = CadCmd.Help, ["HELP"] = CadCmd.Help, ["TROGIUP"] = CadCmd.Help,
    };

    /// <summary>Tên hiển thị của lệnh (tiếng Việt).</summary>
    public static string Name(CadCmd c) => c switch
    {
        CadCmd.Beam => "Dầm (LINE)", CadCmd.Column => "Cột", CadCmd.Slab => "Ô sàn", CadCmd.Wall => "Tường",
        CadCmd.Move => "Dời (MOVE)", CadCmd.Copy => "Sao chép (COPY)", CadCmd.Mirror => "Đối xứng (MIRROR)", CadCmd.Array => "Mảng (ARRAY)",
        CadCmd.Offset => "Offset dầm", CadCmd.Trim => "Trim", CadCmd.Extend => "Extend", CadCmd.Stretch => "Kéo dầm (STRETCH)",
        CadCmd.Erase => "Xoá (ERASE)", CadCmd.Undo => "Hoàn tác", CadCmd.Redo => "Làm lại", CadCmd.Zoom => "Zoom",
        CadCmd.ZoomExtents => "Zoom toàn bộ", CadCmd.Dist => "Đo khoảng cách (DIST)", CadCmd.Match => "Sao thuộc tính (MATCHPROP)",
        CadCmd.Load => "Gán tải", CadCmd.Select => "Chọn nhiều", CadCmd.SelectSimilar => "Chọn tương tự", CadCmd.Ortho => "Trực giao (F8)",
        CadCmd.Polar => "Góc cực (F10)", CadCmd.Osnap => "Bắt điểm (F3)", CadCmd.Help => "Trợ giúp lệnh", CadCmd.Cancel => "Huỷ",
        _ => ""
    };

    /// <summary>Tìm lệnh theo chữ gõ (bỏ dấu, không phân biệt hoa thường, bỏ tiền tố "_" / "." kiểu AutoCAD).</summary>
    public static CadCmd Resolve(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return CadCmd.None;
        string w = CommandSearch.Fold(word.Trim().TrimStart('_', '.', '-')).Replace(" ", "").ToUpperInvariant();
        return Aliases.TryGetValue(w, out var c) ? c : CadCmd.None;
    }

    private static bool LooksLikeCoordinate(string s)
    {
        if (s.Length == 0) return false;
        char c = s[0];
        return char.IsDigit(c) || c is '@' or '#' or '-' or '+' or '.' or '<';
    }

    /// <summary>Phân tích một dòng gõ: rỗng (Enter = lặp lệnh), lệnh (+ tham số), toạ độ điểm, hoặc số (chiều dài theo hướng chuột).
    /// last = điểm trước (cho @ và số đơn), dirTo = vị trí chuột (hướng cho số đơn).</summary>
    public static CadCommandInput Parse(string text, (double X, double Y)? last, CoordUnit unit, (double X, double Y)? dirTo = null)
    {
        string s = (text ?? "").Trim();
        if (s.Length == 0) return new CadCommandInput { Kind = CadInputKind.Empty, Raw = text ?? "" };
        if (LooksLikeCoordinate(s))
        {
            var ci = BeamTools.ParseCoordinateInput(s, last, unit, dirTo);
            if (!ci.Ok) return new CadCommandInput { Kind = CadInputKind.Error, Error = ci.Error, Raw = s };
            return new CadCommandInput
            {
                Kind = ci.Kind == CoordInputKind.Distance ? CadInputKind.Number : CadInputKind.Point, X = ci.X, Y = ci.Y, Raw = s
            };
        }
        int sp = s.IndexOfAny(new[] { ' ', '\t' });
        string head = sp < 0 ? s : s[..sp], arg = sp < 0 ? "" : s[(sp + 1)..].Trim();
        var cmd = Resolve(head);
        if (cmd == CadCmd.None) cmd = Resolve(s);   // "Dời dầm" viết có dấu cách
        if (cmd == CadCmd.None)
            return new CadCommandInput { Kind = CadInputKind.Error, Raw = s, Error = $"Không rõ lệnh \"{head}\" – gõ ? để xem danh sách lệnh." };
        if (cmd == CadCmd.Zoom && arg.Length > 0 && CommandSearch.Fold(arg).ToUpperInvariant().StartsWith("E")) cmd = CadCmd.ZoomExtents;
        return new CadCommandInput { Kind = CadInputKind.Command, Cmd = cmd, Arg = arg, Raw = s };
    }

    /// <summary>Ràng buộc điểm theo trực giao (ortho: chỉ phương X hoặc Y, theo thành phần lớn hơn) hoặc góc cực
    /// (polarStepDeg &gt; 0: bắt về bội số gần nhất của bước góc khi lệch ≤ toleranceDeg; ngoài khoảng đó giữ nguyên).
    /// Chiều dài giữ bằng hình chiếu lên phương đã chọn.</summary>
    public static (double X, double Y) Constrain((double X, double Y) from, (double X, double Y) to, bool ortho, double polarStepDeg = 0, double toleranceDeg = 6)
    {
        double dx = to.X - from.X, dy = to.Y - from.Y;
        if (Math.Abs(dx) < 1e-12 && Math.Abs(dy) < 1e-12) return to;
        if (ortho) return Math.Abs(dx) >= Math.Abs(dy) ? (to.X, from.Y) : (from.X, to.Y);
        if (polarStepDeg <= 0) return to;
        double ang = Math.Atan2(dy, dx) * 180 / Math.PI;
        double snapped = Math.Round(ang / polarStepDeg) * polarStepDeg;
        if (Math.Abs(ang - snapped) > toleranceDeg) return to;
        double a = snapped * Math.PI / 180, ux = Math.Cos(a), uy = Math.Sin(a);
        double L = dx * ux + dy * uy;
        return (from.X + L * ux, from.Y + L * uy);
    }

    /// <summary>Điểm cách "from" một đoạn L theo hướng tới "dirTo" (nhập chiều dài động).</summary>
    public static (double X, double Y)? Along((double X, double Y) from, (double X, double Y) dirTo, double length)
    {
        double dx = dirTo.X - from.X, dy = dirTo.Y - from.Y, d = Math.Sqrt(dx * dx + dy * dy);
        if (d < 1e-9) return null;
        return (from.X + length * dx / d, from.Y + length * dy / d);
    }

    /// <summary>Danh sách lệnh (cho lệnh "?").</summary>
    public static string HelpText()
    {
        var groups = Aliases.GroupBy(kv => kv.Value).OrderBy(g => (int)g.Key)
            .Select(g => $"{Name(g.Key)}: {string.Join(", ", g.Select(kv => kv.Key).Take(4))}");
        return string.Join(" · ", groups) + " · Toạ độ: x,y | @dx,dy | @L<góc | số = chiều dài theo hướng chuột · F3 bắt điểm, F8 trực giao, F10 góc cực, Enter/Space lặp lệnh, Esc huỷ.";
    }
}

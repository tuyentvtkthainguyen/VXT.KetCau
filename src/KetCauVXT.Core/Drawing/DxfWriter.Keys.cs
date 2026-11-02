using System.Security.Cryptography;
using System.Text;

namespace KetCauVXT.Drawing;

/// <summary>Thực thể của bộ ghi nhìn từ ngoài (chọn trên màn hình, hiệu chỉnh bản vẽ, kiểm thử).</summary>
public sealed class EntityRef
{
    /// <summary>Chỉ số trong bộ ghi (đổi khi xoá thực thể; chỉ dùng ngay sau khi lấy danh sách).</summary>
    public int Index;
    public string Type = "";
    public string Layer = "0";
    /// <summary>Khoá ổn định trong hình chứa thực thể.</summary>
    public string Key;
    /// <summary>Nhóm (nhãn thép "TAG|…", tên hình "TITLE|…", kích thước "DIM|…", vòng số "MARK|…", trục "AXIS|…"…); null = đơn lẻ.</summary>
    public string Group;
    public string Role;
    /// <summary>Khoá hình gốc (Figure) – null: thực thể của chính bộ ghi (khung tên, viền…).</summary>
    public string Owner;
    /// <summary>Hệ số phóng hình gốc → bộ ghi.</summary>
    public double OwnerK = 1;
    public Rect2 Box;
    /// <summary>Nội dung chữ (TEXT, đã giải mã).</summary>
    public string Text;
    /// <summary>Điểm chèn / điểm đầu.</summary>
    public double X, Y;
    public double Height, Rotation;
}

/// <summary>Khoá ổn định, nhóm và hình gốc của thực thể – nền tảng cho "Hiệu chỉnh bản vẽ" (Drawing/DrawingOverlay.cs):
/// mỗi thực thể có khoá suy từ loại + lớp + toạ độ làm tròn 1 đơn vị (+ nội dung chữ), trùng thì thêm "~n" theo thứ tự vẽ.
/// Bản vẽ dựng lại từ cùng mô hình cho cùng khoá ⇒ chỉnh sửa lưu theo khoá được áp lại sau mỗi lần xuất.
/// Các ký hiệu tổ hợp (nhãn thép, tên hình, kích thước, cao độ, đầu trục, vòng số hiệu) được gom nhóm để chọn/dời cả cụm.</summary>
public sealed partial class DxfWriter
{
    private string _group, _role, _nextKey;
    private Dictionary<string, int> _groupSeen;

    /// <summary>Khoá của hình (Figure) mà bộ ghi này thể hiện, vd. "BEAM:D2-1". Thực thể ghép sang bộ ghi khác mang theo khoá này (Owner).</summary>
    public string OwnerKey { get; set; }

    internal List<DxfEnt> Ents => _ents;

    /// <summary>Số làm tròn dùng trong khoá ("-0" → "0").</summary>
    public static string KeyNum(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
        double r = Math.Round(v);
        if (r == 0) r = 0;
        return r.ToString("0", Inv);
    }

    /// <summary>Bắt đầu nhóm thực thể (trả về nhóm cũ để truyền cho <see cref="EndGroup"/>). Đang trong nhóm khác thì giữ nhóm ngoài.
    /// Khoá nhóm trùng trong cùng bộ ghi được thêm "#n".</summary>
    public string BeginGroup(string key)
    {
        string prev = _group;
        if (prev != null || string.IsNullOrEmpty(key)) return prev;
        _groupSeen ??= new Dictionary<string, int>(StringComparer.Ordinal);
        if (_groupSeen.TryGetValue(key, out int n)) { _groupSeen[key] = n + 1; key = key + "#" + (n + 1).ToString(Inv); }
        else _groupSeen[key] = 1;
        _group = key;
        return null;
    }

    public void EndGroup(string previous) => _group = previous;

    /// <summary>Đặt khoá tường minh cho thực thể vẽ ngay sau đây (thay cho khoá tự suy).</summary>
    public void KeyNext(string key) => _nextKey = string.IsNullOrEmpty(key) ? null : key;

    // ------------------------------------------------------------------
    // Khoá
    // ------------------------------------------------------------------
    private static string LayerOf(DxfEnt e)
    {
        foreach (var (c, v) in e.Codes) if (c == 8) return v as string ?? "0";
        return "0";
    }

    private static double Num(object v) => v is double d ? d : v is int i ? i : 0;

    /// <summary>Giá trị mã nhóm đầu tiên (trước VERTEX).</summary>
    private static object HeadCode(DxfEnt e, int code)
    {
        foreach (var (c, v) in e.Codes)
        {
            if (c == 0) break;
            if (c == code) return v;
        }
        return null;
    }

    private static string BaseKey(DxfEnt e)
    {
        var sb = new StringBuilder(64);
        sb.Append(e.Type).Append('|').Append(LayerOf(e));
        void P(int code) => sb.Append('|').Append(KeyNum(Num(HeadCode(e, code))));
        switch (e.Type)
        {
            case "LINE": P(10); P(20); P(11); P(21); break;
            case "CIRCLE": P(10); P(20); P(40); break;
            case "ARC": P(10); P(20); P(40); P(50); break;
            case "SOLID": P(10); P(20); P(11); P(21); P(12); P(22); break;
            case "TEXT":
                P(10); P(20);
                sb.Append('|').Append(HeadCode(e, 1) as string ?? "");
                break;
            case "POLYLINE":
            {
                bool inV = false; int nv = 0; double fx = double.NaN, fy = double.NaN;
                foreach (var (c, v) in e.Codes)
                {
                    if (c == 0) { inV = (v as string) == "VERTEX"; if (inV) nv++; continue; }
                    if (!inV || nv != 1) continue;
                    if (c == 10) fx = Num(v); else if (c == 20) fy = Num(v);
                }
                sb.Append('|').Append(KeyNum(fx)).Append('|').Append(KeyNum(fy)).Append("|n").Append(nv.ToString(Inv));
                break;
            }
            default:
                sb.Append('|').Append(KeyNum(e.Box.X1)).Append('|').Append(KeyNum(e.Box.Y1));
                break;
        }
        return sb.ToString();
    }

    /// <summary>Gán khoá cho các thực thể chưa có và làm khoá của thực thể riêng (Owner = null) duy nhất ("~n"). Gọi lại nhiều lần không đổi kết quả.</summary>
    public void EnsureKeys()
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in _ents)
        {
            e.Key ??= BaseKey(e);
            if (e.Owner != null) continue;
            string k = e.Key;
            for (int n = 2; !used.Add(k); n++) k = e.Key + "~" + n.ToString(Inv);
            e.Key = k;
        }
    }

    /// <summary>Danh sách thực thể (đã có khoá) – để chọn trên màn hình / kiểm thử.</summary>
    public List<EntityRef> EntityRefs()
    {
        EnsureKeys();
        var res = new List<EntityRef>(_ents.Count);
        for (int i = 0; i < _ents.Count; i++)
        {
            var e = _ents[i];
            var r = new EntityRef
            {
                Index = i, Type = e.Type, Layer = LayerOf(e), Key = e.Key, Group = e.Group, Role = e.Role,
                Owner = e.Owner, OwnerK = e.OwnerK, Box = e.Box
            };
            if (e.Type == "POLYLINE")
            {
                bool inV = false;
                foreach (var (c, v) in e.Codes)
                {
                    if (c == 0) { inV = (v as string) == "VERTEX"; continue; }
                    if (!inV) continue;
                    if (c == 10) r.X = Num(v);
                    else if (c == 20) { r.Y = Num(v); break; }
                }
            }
            else
            {
                r.X = Num(HeadCode(e, 10)); r.Y = Num(HeadCode(e, 20));
            }
            if (e.Type == "TEXT")
            {
                r.Text = Decode(HeadCode(e, 1) as string ?? "");
                r.Height = Num(HeadCode(e, 40));
                r.Rotation = Num(HeadCode(e, 50));
            }
            res.Add(r);
        }
        return res;
    }

    // ------------------------------------------------------------------
    // Thao tác hiệu chỉnh
    // ------------------------------------------------------------------
    /// <summary>Tịnh tiến thực thể i. endOnly (đường dẫn của nhãn): chỉ dời điểm cuối (mã 11/21) của LINE.</summary>
    public void MoveEntity(int i, double dx, double dy, bool endOnly = false)
    {
        if (i < 0 || i >= _ents.Count) return;
        var e = _ents[i];
        bool lineEnd = endOnly && e.Type == "LINE";
        for (int j = 0; j < e.Codes.Count; j++)
        {
            var (code, val) = e.Codes[j];
            if (val is not double d) continue;
            if (lineEnd)
            {
                if (code == 11) e.Codes[j] = (code, d + dx);
                else if (code == 21) e.Codes[j] = (code, d + dy);
                continue;
            }
            if (code >= 10 && code <= 18) e.Codes[j] = (code, d + dx);
            else if (code >= 20 && code <= 28) e.Codes[j] = (code, d + dy);
        }
        if (lineEnd)
            e.Box = new Rect2(Num(HeadCode(e, 10)), Num(HeadCode(e, 20)), Num(HeadCode(e, 11)), Num(HeadCode(e, 21)));
        else e.Box = e.Box.Offset(dx, dy);
    }

    /// <summary>Đổi nội dung chữ của thực thể TEXT i (cập nhật phạm vi). Trả về false nếu không phải chữ.</summary>
    public bool SetEntityText(int i, string text)
    {
        if (i < 0 || i >= _ents.Count || _ents[i].Type != "TEXT") return false;
        var e = _ents[i];
        string enc = Encode(text ?? "");
        if (enc.Length == 0) enc = " ";
        for (int j = 0; j < e.Codes.Count; j++)
            if (e.Codes[j].Code == 1) { e.Codes[j] = (1, enc); break; }
        int ha = (int)Num(HeadCode(e, 72)), va = (int)Num(HeadCode(e, 73));
        bool al = ha != 0 || va != 0;
        double x = Num(HeadCode(e, al ? 11 : 10)), y = Num(HeadCode(e, al ? 21 : 20));
        e.Box = TextBox(x, y, Num(HeadCode(e, 40)), text ?? "", Num(HeadCode(e, 50)), ha, va);
        return true;
    }

    /// <summary>Đổi lớp (null = giữ) và màu ACI (0 = theo lớp, giữ nguyên nếu &lt; 0) của thực thể i.</summary>
    public void RestyleEntity(int i, string layer, int aci)
    {
        if (i < 0 || i >= _ents.Count) return;
        var c = _ents[i].Codes;
        if (!string.IsNullOrEmpty(layer))
            for (int j = 0; j < c.Count; j++) if (c[j].Code == 8) c[j] = (8, layer);
        if (aci < 0) return;
        int at = c.FindIndex(x => x.Code == 62);
        if (aci == 0) { if (at >= 0) c.RemoveAt(at); return; }
        if (aci > 255) return;
        if (at >= 0) c[at] = (62, aci);
        else c.Insert(c.Count > 0 && c[0].Code == 8 ? 1 : 0, (62, aci));
    }

    /// <summary>Xoá các thực thể theo chỉ số.</summary>
    public int RemoveEntities(IEnumerable<int> indices)
    {
        var set = new HashSet<int>(indices ?? Enumerable.Empty<int>());
        if (set.Count == 0) return 0;
        int before = _ents.Count;
        var keep = new List<DxfEnt>(_ents.Count);
        for (int i = 0; i < _ents.Count; i++) if (!set.Contains(i)) keep.Add(_ents[i]);
        _ents.Clear();
        _ents.AddRange(keep);
        RecalcExtents();
        return before - _ents.Count;
    }

    /// <summary>Tính lại phạm vi bản vẽ từ các thực thể.</summary>
    public void RecalcExtents()
    {
        var r = Rect2.Empty;
        foreach (var e in _ents) r = r.Union(e.Box);
        _ext = r;
    }

    /// <summary>Bản sao độc lập (thực thể, khoá, kiểu kích thước, thiết lập).</summary>
    public DxfWriter Clone()
    {
        var w = new DxfWriter(Scale) { Standard = Standard, OwnerKey = OwnerKey };
        w._style = _style;
        foreach (var e in _ents)
        {
            var n = new DxfEnt { Type = e.Type, Box = e.Box, Codes = new List<(int Code, object Val)>(e.Codes) };
            n.Key = e.Key; n.Group = e.Group; n.Role = e.Role; n.Owner = e.Owner; n.OwnerK = e.OwnerK;
            w._ents.Add(n);
        }
        w._ext = _ext;
        foreach (var d in _dimUsed) w._dimUsed.Add(d);
        if (_groupSeen != null) w._groupSeen = new Dictionary<string, int>(_groupSeen, StringComparer.Ordinal);
        return w;
    }

    /// <summary>Mã băm nội dung (loại + mã nhóm của mọi thực thể, tên layer nội bộ) – nhận biết bản vẽ dựng lại có thay đổi hay không.</summary>
    public string ContentHash()
    {
        var sb = new StringBuilder(_ents.Count * 48);
        foreach (var e in _ents)
        {
            sb.Append(e.Type).Append(';');
            foreach (var (code, val) in e.Codes)
            {
                sb.Append(code.ToString(Inv)).Append('=');
                sb.Append(val switch { double d => F(d), int n => n.ToString(Inv), string s => s, _ => "" });
                sb.Append(';');
            }
            sb.Append('\n');
        }
        var h = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(h);
    }
}

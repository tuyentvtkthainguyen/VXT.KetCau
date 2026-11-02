using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KetCauVXT.Import;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Kết quả áp lớp chỉnh sửa: số chỉnh sửa đã áp, các chỉnh sửa "không còn áp dụng được" (thực thể không còn sau khi dựng lại),
/// cảnh báo bản vẽ đã khoá.</summary>
public sealed class OverlayReport
{
    private readonly HashSet<DrawingEdit> _staleSet = new();
    public int Applied;
    public List<DrawingEdit> Stale { get; } = new();
    public List<string> Warnings { get; } = new();

    public void AddStale(DrawingEdit e) { if (e != null && _staleSet.Add(e)) Stale.Add(e); }
    public void Warn(string s) { if (!string.IsNullOrEmpty(s) && !Warnings.Contains(s)) Warnings.Add(s); }

    /// <summary>Dòng thông báo: "chỉnh sửa không còn áp dụng được" + cảnh báo khoá.</summary>
    public List<string> Messages()
    {
        var res = new List<string>(Warnings);
        foreach (var e in Stale) res.Add("Chỉnh sửa không còn áp dụng được: " + e.Describe());
        return res;
    }
}

/// <summary>Bộ máy "Hiệu chỉnh bản vẽ sau khi xuất": gán khoá hình (Owner) cho mảnh hình, áp lớp chỉnh sửa (Project.DrawingEdits,
/// Project.DrawingNotes) lên bản vẽ mỗi lần dựng lại/xuất, ghi nhận chỉnh sửa từ cửa sổ hiệu chỉnh, quản lý bản vẽ đã khoá (Project.FrozenDrawings).
/// <para>Khoá hình = loại + tên hình ("BEAM:D2-1", "COL:C1 1-1", "PLAN:Tầng 2", "BTK:BTK1"…) – giống nhau ở bộ bản vẽ KC-xx và ở tờ dàn trang,
/// nên sửa một lần thấy ở cả hai. Khoá bản vẽ: "DS:&lt;tên file&gt;" (bộ KC-xx), "SHOP:&lt;tên file&gt;" (gia công), "SHEET:&lt;số tờ&gt;".</para></summary>
public static class DrawingOverlay
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ------------------------------------------------------------------
    // Phạm vi: bỏ qua lớp chỉnh sửa (cửa sổ hiệu chỉnh dựng bản gốc), thu thập báo cáo
    // ------------------------------------------------------------------
    [ThreadStatic] private static int _bypass;
    [ThreadStatic] private static int _noFrozen;
    [ThreadStatic] private static OverlayReport _current;

    /// <summary>Đang dựng bản vẽ gốc (không áp chỉnh sửa, không thay bằng bản đã khoá).</summary>
    public static bool Bypassed => _bypass > 0;

    /// <summary>Đang dựng bản tự sinh (vẫn áp chỉnh sửa) thay vì dùng bản đã khoá – để so mã băm với lúc khoá.</summary>
    public static bool FrozenIgnored => _noFrozen > 0;

    /// <summary>Trong phạm vi này không thay bản vẽ bằng bản đã khoá (lớp chỉnh sửa vẫn áp).</summary>
    public static IDisposable IgnoreFrozen()
    {
        _noFrozen++;
        return new Scope(() => _noFrozen = Math.Max(0, _noFrozen - 1));
    }

    /// <summary>Báo cáo đang thu thập (null = không thu).</summary>
    public static OverlayReport Current => _current;

    private sealed class Scope : IDisposable
    {
        private readonly Action _end;
        private bool _done;
        public Scope(Action end) { _end = end; }
        public void Dispose() { if (_done) return; _done = true; _end(); }
    }

    /// <summary>Trong phạm vi này các bộ vẽ không áp lớp chỉnh sửa và không dùng bản đã khoá: <c>using (DrawingOverlay.Bypass()) …</c></summary>
    public static IDisposable Bypass()
    {
        _bypass++;
        return new Scope(() => _bypass = Math.Max(0, _bypass - 1));
    }

    /// <summary>Thu thập báo cáo áp chỉnh sửa trong phạm vi (lồng nhau: phạm vi trong cùng nhận).</summary>
    public static IDisposable Collect(OverlayReport rep)
    {
        var prev = _current;
        _current = rep;
        return new Scope(() => _current = prev);
    }

    // ------------------------------------------------------------------
    // Khoá
    // ------------------------------------------------------------------
    public static string DrawingSetKey(string fileName) => "DS:" + Path.GetFileNameWithoutExtension(fileName ?? "");
    public static string ShopKey(string fileName) => "SHOP:" + Path.GetFileNameWithoutExtension(fileName ?? "");
    public static string SheetKey(Sheet s) => "SHEET:" + (s?.Number ?? "");

    /// <summary>Tiền tố khoá hình theo loại khung nhìn – trùng tiền tố bộ bản vẽ KC-xx tương ứng.</summary>
    public static string KindOf(SheetViewKind k) => k switch
    {
        SheetViewKind.PlanStory => "PLAN",
        SheetViewKind.SlabRebarPlan => "SLAB",
        SheetViewKind.Elevation => "ELEV",
        SheetViewKind.ForceDiagram => "DIAG",
        SheetViewKind.FoundationPlan or SheetViewKind.FoundationDetail => "FOOT",
        SheetViewKind.BeamDetail => "BEAM",
        SheetViewKind.ColumnDetail => "COL",
        SheetViewKind.RebarSchedule => "BTK",
        SheetViewKind.Notes => "NOTE",
        SheetViewKind.View3D => "V3D",
        SheetViewKind.RebarShop => "SHOP",
        SheetViewKind.StairDetail => "STAIR",
        _ => k.ToString().ToUpperInvariant()
    };

    /// <summary>Gán khoá hình "kind:tên" (trùng tên thêm "#n") cho các mảnh hình chưa có khoá.</summary>
    public static void AssignOwners(IList<Figure> figs, string kind)
    {
        if (figs == null) return;
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var f in figs)
        {
            if (f?.W == null) continue;
            string nm = string.IsNullOrWhiteSpace(f.Name) ? "hinh" : f.Name.Trim();
            string key = (kind ?? "FIG") + ":" + nm;
            if (seen.TryGetValue(key, out int n)) { seen[key] = n + 1; key += "#" + (n + 1).ToString(Inv); }
            else seen[key] = 1;
            f.W.OwnerKey ??= key;
        }
    }

    // ------------------------------------------------------------------
    // Áp lớp chỉnh sửa
    // ------------------------------------------------------------------
    /// <summary>Áp chỉnh sửa của từng hình lên chính hình đó (tờ dàn trang, xem trước) – trừ khi đang Bypass.</summary>
    public static int ApplyFigures(Project p, IList<Figure> figs, OverlayReport rep = null)
    {
        if (p == null || figs == null || Bypassed) return 0;
        int n = 0;
        foreach (var f in figs)
            if (f?.W?.OwnerKey != null) n += Apply(p, f.W, f.W.OwnerKey, false, rep);
        return n;
    }

    private static bool HasAny(Project p) =>
        (p.DrawingEdits != null && p.DrawingEdits.Count > 0) || (p.DrawingNotes != null && p.DrawingNotes.Count > 0);

    /// <summary>Áp chỉnh sửa lên bộ ghi w có khoá ownerKey. nested = true: áp cả chỉnh sửa của các hình đã ghép vào (thực thể có Owner).
    /// reportNested = false: không báo "không còn áp dụng" cho chỉnh sửa của hình ghép (tờ dàn trang cắt bớt hình theo khung nhìn).
    /// Trả về số chỉnh sửa đã áp. Báo cáo ghi vào rep hoặc báo cáo đang thu thập (Collect).</summary>
    public static int Apply(Project p, DxfWriter w, string ownerKey, bool nested, OverlayReport rep = null, bool reportNested = true)
    {
        rep ??= _current;
        if (p == null || w == null || !HasAny(p)) return 0;
        w.EnsureKeys();
        var ents = w.Ents;
        // chỉ mục (owner, khoá) và (owner, nhóm)
        var byKey = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var byGroup = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var owners = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < ents.Count; i++)
        {
            var e = ents[i];
            string own = e.Owner ?? ownerKey;
            if (own == null) continue;
            if (e.Owner != null && !nested) continue;
            owners.Add(own);
            Add(byKey, own + "\n" + e.Key, i);
            if (e.Group != null) Add(byGroup, own + "\n" + e.Group, i);
        }
        if (ownerKey != null) owners.Add(ownerKey);
        int applied = 0;
        var remove = new HashSet<int>();
        foreach (var ed in p.DrawingEdits ?? new List<DrawingEdit>())
        {
            if (ed == null || string.IsNullOrEmpty(ed.Owner) || !owners.Contains(ed.Owner)) continue;
            string tgt = ed.Target ?? "";
            bool grp = tgt.StartsWith("G:", StringComparison.Ordinal);
            var dict = grp ? byGroup : byKey;
            if (!dict.TryGetValue(ed.Owner + "\n" + (grp ? tgt.Substring(2) : tgt), out var members) || members.Count == 0)
            {
                if (ed.Owner == ownerKey || reportNested) rep?.AddStale(ed);
                continue;
            }
            switch (ed.Op)
            {
                case DrawingEditOp.Move:
                    foreach (int i in members)
                    {
                        var e = ents[i];
                        if (grp && e.Role == "anchor") continue;
                        double k = e.Owner != null && e.Owner == ed.Owner ? e.OwnerK : 1;
                        w.MoveEntity(i, ed.Dx * k, ed.Dy * k, grp && e.Role == "leader");
                    }
                    break;
                case DrawingEditOp.EditText:
                {
                    int ti = members.FirstOrDefault(i => ents[i].Type == "TEXT", -1);
                    if (ti < 0) { rep?.AddStale(ed); continue; }
                    w.SetEntityText(ti, ed.Text ?? "");
                    break;
                }
                case DrawingEditOp.Delete:
                    foreach (int i in members) remove.Add(i);
                    break;
                case DrawingEditOp.Restyle:
                    foreach (int i in members) w.RestyleEntity(i, string.IsNullOrWhiteSpace(ed.Layer) ? null : ed.Layer.Trim(), ed.Color);
                    break;
            }
            applied++;
        }
        if (remove.Count > 0) w.RemoveEntities(remove);
        // đối tượng thêm (toạ độ mm giấy của bản vẽ chủ)
        if (ownerKey != null)
            foreach (var an in p.DrawingNotes ?? new List<DrawingAnnotation>())
            {
                if (an?.Item == null || an.Owner != ownerKey) continue;
                int start = w.Ents.Count;
                w.AppendScaled(SheetBuilder.ItemFigure(an.Item), w.Scale, 0, 0);
                for (int i = start; i < w.Ents.Count; i++)
                {
                    var e = w.Ents[i];
                    e.Owner = null; e.OwnerK = 1;
                    e.Group = "ADD|" + an.Id;
                    e.Key = "ADD|" + an.Id + "|" + (i - start).ToString(Inv);
                }
                applied++;
            }
        w.RecalcExtents();
        if (rep != null) rep.Applied += applied;
        return applied;
    }

    private static void Add(Dictionary<string, List<int>> d, string k, int i)
    {
        if (!d.TryGetValue(k, out var l)) d[k] = l = new List<int>();
        l.Add(i);
    }

    // ------------------------------------------------------------------
    // Ghi nhận chỉnh sửa (cửa sổ hiệu chỉnh)
    // ------------------------------------------------------------------
    /// <summary>Khoá chủ của thực thể trong bản vẽ drawingKey.</summary>
    public static string OwnerOf(EntityRef e, string drawingKey) => e?.Owner ?? drawingKey;

    /// <summary>Mục tiêu của chỉnh sửa: nhóm ("G:…") nếu thực thể thuộc nhóm và chọn cả nhóm, ngược lại khoá thực thể.</summary>
    public static string TargetOf(EntityRef e, bool group) => group && !string.IsNullOrEmpty(e?.Group) ? "G:" + e.Group : e?.Key ?? "";

    private static DrawingAnnotation AnnotationOf(Project p, EntityRef e)
    {
        if (e?.Group == null || !e.Group.StartsWith("ADD|", StringComparison.Ordinal)) return null;
        string id = e.Group.Substring(4);
        return p.DrawingNotes?.FirstOrDefault(a => a.Id == id);
    }

    /// <summary>Dời thực thể/nhóm một đoạn (dx, dy) tính theo đơn vị của bản vẽ đang mở (đổi sang đơn vị hình chủ). Dời liên tiếp được cộng dồn.</summary>
    public static DrawingEdit RecordMove(Project p, string drawingKey, EntityRef e, bool group, double dx, double dy, double drawingScale = 1)
    {
        if (p == null || e == null) return null;
        var an = AnnotationOf(p, e);
        if (an != null)
        {
            double s = drawingScale > 0 ? drawingScale : 1;
            ShiftItem(an.Item, dx / s, dy / s);
            return null;
        }
        p.DrawingEdits ??= new List<DrawingEdit>();
        string owner = OwnerOf(e, drawingKey), tgt = TargetOf(e, group);
        double k = e.Owner != null && e.OwnerK > 0 ? e.OwnerK : 1;
        var ex = p.DrawingEdits.LastOrDefault(x => x.Op == DrawingEditOp.Move && x.Owner == owner && x.Target == tgt);
        if (ex != null) { ex.Dx += dx / k; ex.Dy += dy / k; return ex; }
        var ed = new DrawingEdit { Owner = owner, Target = tgt, Op = DrawingEditOp.Move, Dx = dx / k, Dy = dy / k, Drawing = drawingKey ?? "", Created = DateTime.Now };
        p.DrawingEdits.Add(ed);
        return ed;
    }

    /// <summary>Sửa nội dung chữ (thực thể TEXT).</summary>
    public static DrawingEdit RecordText(Project p, string drawingKey, EntityRef e, string text)
    {
        if (p == null || e == null) return null;
        var an = AnnotationOf(p, e);
        if (an != null) { an.Item.Text = text ?? ""; return null; }
        p.DrawingEdits ??= new List<DrawingEdit>();
        string owner = OwnerOf(e, drawingKey), tgt = e.Key ?? "";
        var ex = p.DrawingEdits.LastOrDefault(x => x.Op == DrawingEditOp.EditText && x.Owner == owner && x.Target == tgt);
        if (ex != null) { ex.Text = text ?? ""; return ex; }
        var ed = new DrawingEdit { Owner = owner, Target = tgt, Op = DrawingEditOp.EditText, Text = text ?? "", OldText = e.Text ?? "", Drawing = drawingKey ?? "", Created = DateTime.Now };
        p.DrawingEdits.Add(ed);
        return ed;
    }

    /// <summary>Xoá thực thể/nhóm.</summary>
    public static DrawingEdit RecordDelete(Project p, string drawingKey, EntityRef e, bool group)
    {
        if (p == null || e == null) return null;
        var an = AnnotationOf(p, e);
        if (an != null) { p.DrawingNotes.Remove(an); return null; }
        p.DrawingEdits ??= new List<DrawingEdit>();
        var ed = new DrawingEdit { Owner = OwnerOf(e, drawingKey), Target = TargetOf(e, group), Op = DrawingEditOp.Delete, OldText = e.Text, Drawing = drawingKey ?? "", Created = DateTime.Now };
        p.DrawingEdits.Add(ed);
        return ed;
    }

    /// <summary>Đổi lớp/màu.</summary>
    public static DrawingEdit RecordRestyle(Project p, string drawingKey, EntityRef e, bool group, string layer, int aci)
    {
        if (p == null || e == null) return null;
        p.DrawingEdits ??= new List<DrawingEdit>();
        var ed = new DrawingEdit { Owner = OwnerOf(e, drawingKey), Target = TargetOf(e, group), Op = DrawingEditOp.Restyle, Layer = layer, Color = aci, Drawing = drawingKey ?? "", Created = DateTime.Now };
        p.DrawingEdits.Add(ed);
        return ed;
    }

    /// <summary>Thêm đối tượng (SheetItem, toạ độ mm giấy của bản vẽ) vào bản vẽ drawingKey.</summary>
    public static DrawingAnnotation AddAnnotation(Project p, string drawingKey, SheetItem it)
    {
        if (p == null || it == null) return null;
        p.DrawingNotes ??= new List<DrawingAnnotation>();
        var an = new DrawingAnnotation { Owner = drawingKey ?? "", Item = it, Created = DateTime.Now };
        p.DrawingNotes.Add(an);
        return an;
    }

    public static void ShiftItem(SheetItem it, double dx, double dy)
    {
        if (it == null) return;
        it.X += dx; it.Y += dy;
        if (it.Pts != null)
            for (int i = 0; i + 1 < it.Pts.Count; i += 2) { it.Pts[i] += dx; it.Pts[i + 1] += dy; }
    }

    /// <summary>Các chỉnh sửa / đối tượng thêm được tạo trên bản vẽ drawingKey.</summary>
    public static (List<DrawingEdit> Edits, List<DrawingAnnotation> Notes) MadeIn(Project p, string drawingKey) =>
        ((p?.DrawingEdits ?? new List<DrawingEdit>()).Where(e => e.Drawing == drawingKey || e.Owner == drawingKey).ToList(),
         (p?.DrawingNotes ?? new List<DrawingAnnotation>()).Where(a => a.Owner == drawingKey).ToList());

    /// <summary>Xoá mọi chỉnh sửa tạo trên bản vẽ drawingKey (khôi phục bản tự sinh). Trả về số mục đã xoá.</summary>
    public static int ClearEdits(Project p, string drawingKey)
    {
        if (p == null) return 0;
        int n = p.DrawingEdits?.RemoveAll(e => e.Drawing == drawingKey || e.Owner == drawingKey) ?? 0;
        n += p.DrawingNotes?.RemoveAll(a => a.Owner == drawingKey) ?? 0;
        return n;
    }

    /// <summary>Xoá các chỉnh sửa trong danh sách (vd. các chỉnh sửa không còn áp dụng được).</summary>
    public static int RemoveEdits(Project p, IEnumerable<DrawingEdit> edits)
    {
        if (p?.DrawingEdits == null || edits == null) return 0;
        var set = new HashSet<DrawingEdit>(edits);
        return p.DrawingEdits.RemoveAll(set.Contains);
    }

    /// <summary>Ảnh chụp lớp chỉnh sửa (hoàn tác/làm lại).</summary>
    public static string Snapshot(Project p) => System.Text.Json.JsonSerializer.Serialize(
        new OverlaySnapshot { Edits = p?.DrawingEdits ?? new List<DrawingEdit>(), Notes = p?.DrawingNotes ?? new List<DrawingAnnotation>() }, Project.JsonOptions);

    public static void Restore(Project p, string json)
    {
        if (p == null || string.IsNullOrEmpty(json)) return;
        var s = System.Text.Json.JsonSerializer.Deserialize<OverlaySnapshot>(json, Project.JsonOptions);
        p.DrawingEdits = s?.Edits ?? new List<DrawingEdit>();
        p.DrawingNotes = s?.Notes ?? new List<DrawingAnnotation>();
    }

    public sealed class OverlaySnapshot
    {
        public List<DrawingEdit> Edits { get; set; } = new();
        public List<DrawingAnnotation> Notes { get; set; } = new();
    }

    // ------------------------------------------------------------------
    // Bản vẽ đã khoá (sửa trong CAD rồi nạp lại)
    // ------------------------------------------------------------------
    public static FrozenDrawing FrozenOf(Project p, string key) =>
        string.IsNullOrEmpty(key) ? null : p?.FrozenDrawings?.FirstOrDefault(f => f != null && f.Key == key);

    /// <summary>Nạp file DXF/DWG làm bản khoá của bản vẽ key. baseHash: mã băm bản vẽ tự sinh hiện tại (ContentHash). DWG được chuyển sang DXF
    /// bằng bộ chuyển đã đăng ký (CadFileReaders.DwgToDxf – ACadSharp) hoặc ODA File Converter.</summary>
    public static FrozenDrawing Freeze(Project p, string key, string title, string path, string baseHash, double scale, out string error)
    {
        error = null;
        if (p == null || string.IsNullOrEmpty(key)) { error = "Chưa chọn bản vẽ."; return null; }
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) { error = $"Không tìm thấy file '{path}'."; return null; }
        byte[] bytes = File.ReadAllBytes(path);
        bool dwg = DwgConverter.IsDwg(path);
        string dxfText;
        if (!dwg)
        {
            dxfText = DxfWriter.DecodeDxfBytes(bytes);
            if (dxfText.StartsWith("AutoCAD Binary DXF", StringComparison.Ordinal)) { error = DxfReader.BinaryDxfMessage; return null; }
        }
        else
        {
            dxfText = DwgText(path, out error);
            if (dxfText == null) return null;
        }
        var test = DxfWriter.FromDxfText(dxfText, scale, out _);
        if (test.EntityCount == 0) { error = "File không có thực thể nào trong model space (hoặc không đọc được)."; return null; }
        p.FrozenDrawings ??= new List<FrozenDrawing>();
        p.FrozenDrawings.RemoveAll(f => f == null || f.Key == key);
        var fz = new FrozenDrawing
        {
            Key = key, Title = title ?? "", FileName = Path.GetFileName(path), SourcePath = Path.GetFullPath(path),
            Format = dwg ? "dwg" : "dxf", Data = Convert.ToBase64String(bytes), DxfText = dwg ? dxfText : "",
            Hash = Convert.ToHexString(SHA256.HashData(bytes)), BaseHash = baseHash ?? "", Scale = scale > 0 ? scale : 1, FrozenAt = DateTime.Now
        };
        p.FrozenDrawings.Add(fz);
        return fz;
    }

    private static string DwgText(string path, out string error)
    {
        error = null;
        string tmp = Path.Combine(Path.GetTempPath(), "KetCauVXT_frz_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".dxf");
        try
        {
            if (CadFileReaders.DwgToDxf != null)
            {
                try
                {
                    CadFileReaders.DwgToDxf(path, tmp);
                    if (File.Exists(tmp)) return DxfWriter.DecodeDxfBytes(File.ReadAllBytes(tmp));
                }
                catch (Exception ex) { error = ex.Message; }
            }
            string dxf = DwgConverter.ConvertToDxf(path, out string e2);
            if (dxf != null && File.Exists(dxf)) { error = null; return DxfWriter.DecodeDxfBytes(File.ReadAllBytes(dxf)); }
            error = (error != null ? error + "; " : "") + (e2 ?? "Không chuyển được DWG sang DXF.") + " Hãy lưu bản vẽ dạng DXF (SAVEAS → DXF) rồi nạp lại.";
            return null;
        }
        finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    /// <summary>Bỏ khoá: bản vẽ được dựng lại từ mô hình.</summary>
    public static bool Unfreeze(Project p, string key) => (p?.FrozenDrawings?.RemoveAll(f => f == null || f.Key == key) ?? 0) > 0;

    private static readonly Dictionary<string, DxfWriter> _frozenCache = new();
    private static readonly object _lock = new();

    /// <summary>Bộ ghi (bản sao) của bản vẽ đã khoá. null nếu không đọc được.</summary>
    public static DxfWriter FrozenWriter(FrozenDrawing f) => FrozenWriter(f, out _);

    public static DxfWriter FrozenWriter(FrozenDrawing f, out List<string> warnings)
    {
        warnings = new List<string>();
        if (f == null) return null;
        string ck = f.Hash + "|" + f.Scale.ToString("R", Inv);
        lock (_lock)
            if (_frozenCache.TryGetValue(ck, out var cached)) return cached.Clone();
        string text = !string.IsNullOrEmpty(f.DxfText) ? f.DxfText : DxfWriter.DecodeDxfBytes(RawBytes(f));
        var w = DxfWriter.FromDxfText(text, f.Scale > 0 ? f.Scale : 1, out warnings);
        w.OwnerKey = null;
        lock (_lock)
        {
            if (_frozenCache.Count > 16) _frozenCache.Clear();
            _frozenCache[ck] = w;
        }
        return w.Clone();
    }

    public static byte[] RawBytes(FrozenDrawing f)
    {
        if (string.IsNullOrEmpty(f?.Data)) return Array.Empty<byte>();
        try { return Convert.FromBase64String(f.Data); } catch (FormatException) { return Array.Empty<byte>(); }
    }

    /// <summary>Cảnh báo "bản vẽ đã khoá": luôn nhắc đã khoá; nếu bản tự sinh hiện tại khác lúc khoá thì báo mô hình thay đổi sau ngày khoá.</summary>
    public static string FrozenWarning(FrozenDrawing f, string currentHash)
    {
        if (f == null) return null;
        string day = f.FrozenAt.ToString("dd/MM/yyyy HH:mm", Inv);
        bool changed = !string.IsNullOrEmpty(currentHash) && !string.IsNullOrEmpty(f.BaseHash) && currentHash != f.BaseHash;
        return changed
            ? $"Bản vẽ đã khoá {f.Key} ({f.FileName}) – mô hình thay đổi sau ngày {day}, cần kiểm tra."
            : $"Bản vẽ đã khoá {f.Key} ({f.FileName}, khoá ngày {day}) – dùng bản đã sửa trong CAD.";
    }

    /// <summary>Mô hình đã thay đổi so với lúc khoá.</summary>
    public static bool ModelChanged(FrozenDrawing f, string currentHash) =>
        f != null && !string.IsNullOrEmpty(currentHash) && !string.IsNullOrEmpty(f.BaseHash) && currentHash != f.BaseHash;

    /// <summary>Ghi bản khoá ra file (xuất "tách rời"): DXF gốc ghi nguyên văn vào path; DWG: ghi DXF đã chuyển vào path và chép file .dwg gốc cạnh đó.
    /// Trả về các file đã ghi.</summary>
    public static List<string> WriteFrozen(FrozenDrawing f, string path)
    {
        var res = new List<string>();
        if (f == null) return res;
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        if (f.Format == "dwg")
        {
            var w = FrozenWriter(f);
            if (w != null) { w.Save(path); res.Add(path); }
            string dwg = Path.ChangeExtension(path, ".dwg");
            File.WriteAllBytes(dwg, RawBytes(f));
            res.Add(dwg);
        }
        else
        {
            File.WriteAllBytes(path, RawBytes(f));
            res.Add(path);
        }
        return res;
    }
}

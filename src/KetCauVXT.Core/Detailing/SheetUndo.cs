namespace KetCauVXT.Detailing;

/// <summary>Hoàn tác / làm lại thao tác trên bộ bản vẽ (tạo, xoá, đổi tên, đánh số, đặt / dời / gỡ khung nhìn):
/// lưu ảnh chụp JSON của DetailSheetSet (qua DetailingProject để dùng chung chuẩn hoá khi đọc lại).</summary>
public sealed class SheetUndoStack
{
    private readonly List<string> _undo = new();
    private readonly List<string> _redo = new();
    public int Capacity { get; set; } = 50;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;

    /// <summary>Ảnh chụp JSON (null set → chuỗi rỗng = "chưa dàn trang").</summary>
    public static string Snap(DetailSheetSet set) => set == null ? "" : new DetailingProject { Sheets = set }.ToJson();

    /// <summary>Khôi phục từ ảnh chụp.</summary>
    public static DetailSheetSet Restore(string snap) => string.IsNullOrEmpty(snap) ? null : DetailingProject.FromJson(snap).Sheets;

    /// <summary>Gọi TRƯỚC khi sửa: ghi trạng thái hiện tại vào ngăn hoàn tác, xoá ngăn làm lại.</summary>
    public void Push(DetailSheetSet before)
    {
        _undo.Add(Snap(before));
        if (_undo.Count > Capacity) _undo.RemoveAt(0);
        _redo.Clear();
    }

    /// <summary>Ghi một ảnh chụp có sẵn (trạng thái TRƯỚC khi sửa) vào ngăn hoàn tác, xoá ngăn làm lại.</summary>
    public void PushSnap(string before)
    {
        _undo.Add(before ?? "");
        if (_undo.Count > Capacity) _undo.RemoveAt(0);
        _redo.Clear();
    }

    /// <summary>Hoàn tác: trả trạng thái trước đó (current được đưa vào ngăn làm lại). Không có → trả current.</summary>
    public DetailSheetSet Undo(DetailSheetSet current)
    {
        if (_undo.Count == 0) return current;
        _redo.Add(Snap(current));
        string s = _undo[^1]; _undo.RemoveAt(_undo.Count - 1);
        return Restore(s);
    }

    /// <summary>Làm lại: trả trạng thái sau đó. Không có → trả current.</summary>
    public DetailSheetSet Redo(DetailSheetSet current)
    {
        if (_redo.Count == 0) return current;
        _undo.Add(Snap(current));
        string s = _redo[^1]; _redo.RemoveAt(_redo.Count - 1);
        return Restore(s);
    }

    public void Clear() { _undo.Clear(); _redo.Clear(); }
}

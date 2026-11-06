using KetCauVXT.Ui;

namespace KetCauVXT.Tests;

/// <summary>Ô "Tất cả" ba trạng thái cho danh sách layer có ô tích: trạng thái, bấm "Tất cả", Chọn hết / Bỏ hết / Đảo, chỉ dòng đang hiện.</summary>
public static class CheckAllTests
{
    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        static double S(bool? v) => v == true ? 1 : v == false ? 0 : 0.5;
        static double B(bool v) => v ? 1 : 0;

        check("Trạng thái: tích hết → 1", S(CheckAll.State(new[] { true, true, true })), 1, 0);
        check("Trạng thái: không ô nào → 0", S(CheckAll.State(new[] { false, false })), 0, 0);
        check("Trạng thái: một phần → không xác định (0,5)", S(CheckAll.State(new[] { true, false, true })), 0.5, 0);
        check("Trạng thái: danh sách rỗng → 0", S(CheckAll.State(Array.Empty<bool>())), 0, 0);
        check("Trạng thái: null → 0", S(CheckAll.State(null)), 0, 0);

        check("Bấm Tất cả khi đang tích hết → bỏ hết", B(CheckAll.ToggleTarget(true)), 0, 0);
        check("Bấm Tất cả khi không ô nào → tích hết", B(CheckAll.ToggleTarget(false)), 1, 0);
        check("Bấm Tất cả khi một phần → tích hết", B(CheckAll.ToggleTarget(null)), 1, 0);

        var v = new[] { true, false, true, false, false };
        check("Chọn hết: 5/5", CheckAll.Apply(v, "all").Count(x => x), 5, 0);
        check("Bỏ hết: 0/5", CheckAll.Apply(v, "none").Count(x => x), 0, 0);
        var inv = CheckAll.Apply(v, "invert");
        check("Đảo: 2 → 3 ô tích", inv.Count(x => x), 3, 0);
        check("Đảo: từng ô ngược lại", B(inv.Select((x, i) => x == !v[i]).All(x => x)), 1, 0);
        check("Toggle từ một phần → tích hết", CheckAll.Apply(v, "toggle").Count(x => x), 5, 0);
        check("Toggle từ tích hết → bỏ hết", CheckAll.Apply(new[] { true, true }, "toggle").Count(x => x), 0, 0);
        check("Không đổi mảng gốc", v.Count(x => x), 2, 0);

        // chỉ dòng đang hiện (lọc): chỉ số chẵn
        Func<int, bool> even = i => i % 2 == 0;
        var f1 = CheckAll.Apply(v, "none", even);
        check("Lọc: bỏ hết chỉ các dòng hiện (0, 2, 4)", B(!f1[0] && !f1[2] && !f1[4]), 1, 0);
        check("Lọc: dòng ẩn giữ nguyên (1, 3)", B(f1[1] == v[1] && f1[3] == v[3]), 1, 0);
        var f2 = CheckAll.Apply(new[] { true, false, true, false }, "toggle", even);
        check("Lọc: toggle xét trạng thái dòng hiện (đã tích hết → bỏ hết)", B(!f2[0] && !f2[2]), 1, 0);
        check("Lọc: toggle không đụng dòng ẩn", B(!f2[1] && !f2[3]), 1, 0);
        var f3 = CheckAll.Apply(new[] { false, true, false }, "all", i => i == 2);
        check("Lọc: chọn hết chỉ dòng hiện", B(!f3[0] && f3[1] && f3[2]), 1, 0);

        var (on, n) = CheckAll.Count(v);
        check("Đếm ô tích", on, 2, 0);
        check("Đếm tổng", n, 5, 0);
        check("Chữ đếm \"2/5\"", B(CheckAll.CountText(v) == "2/5"), 1, 0);

        // danh sách lớn (vài trăm layer) – một lần áp
        var big = Enumerable.Range(0, 800).Select(i => i % 3 == 0).ToArray();
        check("800 layer: chọn hết", CheckAll.Apply(big, "all").Count(x => x), 800, 0);
        check("800 layer: trạng thái sau bỏ hết", S(CheckAll.State(CheckAll.Apply(big, "none"))), 0, 0);
    }
}

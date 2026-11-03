"""Kết cấu bê tông cốt thép theo TCVN 5574:2018."""
from __future__ import annotations
import math
import numpy as np
from .registry import problem, num, sel, tbl, InputError
from ..core.report import Report, fmt
from ..core.drawing import Drawing
from ..core.materials import CONCRETE, REBAR, xi_R, bar_area
from .rc_common import choose_bars, draw_rc_section, beam_elevation, rebar_tables, stirrup_len, round10, pick_slab_bar

MOD = "Kết cấu bê tông cốt thép"
STD = "TCVN 5574:2018"
_conc = sel("conc", "Cấp bền bê tông", list(CONCRETE), "B25")
_steel = sel("steel", "Nhóm cốt thép dọc", list(REBAR), "CB400-V")
_steelw = sel("steelw", "Nhóm cốt thép đai", list(REBAR), "CB240-T")
_gb = sel("gb", "Hệ số điều kiện làm việc γb1", [(0.9, "0,9: có tải dài hạn"), (1.0, "1,0: chỉ tải ngắn hạn")], 0.9, help="TCVN 5574:2018 mục 6.1: tải tác dụng dài hạn lấy 0,9")


def _mat_rows(r, p, conc, s, gb, Rb, with_w=None):
    rows = [["Bê tông " + p["conc"], "Rb tính toán", fmt(conc["Rb"]) + " MPa"], ["", "γb1·Rb", fmt(Rb) + " MPa"], ["", "Rbt", fmt(conc["Rbt"]) + " MPa"],
            ["Cốt thép " + p.get("steel", ""), "Rs / Rsc", f"{s['Rs']} / {s['Rsc']} MPa"]]
    if with_w:
        rows.append(["Cốt đai " + p["steelw"], "Rsw", f"{with_w['Rsw']} MPa"])
    r.table(["Vật liệu", "Đại lượng", "Giá trị"], rows, widths=[6, 4, 4])


def _h0(h, a):
    if h - a <= 0:
        raise InputError(f"Chiều cao làm việc h₀ = h − a ≤ 0 (h = {fmt(h)}, a = {fmt(a)}). Kiểm tra lại h và a.")
    return h - a


def _rect_capacity(b, h0, As, Asc, ap, Rb, Rs, Rsc, xR):
    x = (Rs * As - Rsc * Asc) / (Rb * b)
    if Asc > 0 and x < 2 * ap:
        return Rs * As * (h0 - ap), x, "x<2a'"
    if x > xR * h0:
        x2 = xR * h0
        return Rb * b * x2 * (h0 - x2 / 2) + Rsc * Asc * (h0 - ap), x, "x>xR"
    return Rb * b * x * (h0 - x / 2) + Rsc * Asc * (h0 - ap), x, "ok"


@problem("btct_beam_design", "btct", "Dầm chữ nhật: tính cốt thép dọc, thể hiện thép",
         "Tính cốt đơn hoặc cốt kép từ M, chọn thanh, kiểm tra lại, vẽ mặt đứng, mặt cắt, bảng thống kê thép kiểu Kết cấu Soft.",
         [num("M", "Mômen M", "kN·m", 180, 0), _conc, _steel, _gb, num("b", "Bề rộng b", "mm", 220, 100), num("h", "Chiều cao h", "mm", 500, 150),
          num("a", "Khoảng cách a giả thiết", "mm", 45, 15), num("ap", "Khoảng cách a' giả thiết", "mm", 40, 15),
          num("L", "Nhịp thông thuỷ L (để vẽ và thống kê)", "mm", 5500, 500), num("sup", "Bề rộng gối", "mm", 220, 100),
          num("ds", "Đường kính đai", "mm", 8, 6), num("s1", "Bước đai vùng gối", "mm", 100, 50), num("s2", "Bước đai giữa nhịp", "mm", 200, 50)])
def beam_design(p):
    r = Report("Dầm chữ nhật: tính cốt thép dọc", MOD, [STD])
    conc, s = CONCRETE[p["conc"]], REBAR[p["steel"]]
    gb = float(p["gb"])
    Rb = conc["Rb"] * gb
    Rs, Rsc = s["Rs"], s["Rsc"]
    b, h, a, ap, M = p["b"], p["h"], p["a"], p["ap"], p["M"]
    r.given([("Mômen tính toán", "M", M, "kN·m"), ("Tiết diện", "b×h", f"{fmt(b)}×{fmt(h)}", "mm")])
    _mat_rows(r, p, conc, s, gb, Rb)
    r.h("Bước 1. Các hệ số giới hạn")
    xR = xi_R(Rs, s["Es"])
    aR = xR * (1 - xR / 2)
    r.calc("ε_(s,el)", "[[R_s // E_s]]", f"[[{Rs} // {s['Es']}]]", Rs / s["Es"], d=5)
    r.calc("ξ_R", "[[0,8 // 1 + ε_(s,el)/ε_b2]]", f"[[0,8 // 1 + {fmt(Rs / s['Es'], 5)}/0,0035]]", xR, key="xiR")
    r.calc("α_R", "ξ_R(1 − 0,5ξ_R)", f"{fmt(xR)}(1 − 0,5·{fmt(xR)})", aR, key="aR")
    r.h("Bước 2. Tính cốt thép")
    Mn = M * 1e6
    a0, ap0 = a, ap
    for _ in range(6):  # a thực phụ thuộc thép chọn, lặp đến khi a giả thiết đủ an toàn
        h0_ = _h0(h, a)
        am_ = Mn / (Rb * b * h0_ ** 2)
        if am_ <= aR:
            Ast = (1 - math.sqrt(1 - 2 * am_)) * Rb * b * h0_ / Rs
            Acst = 0.0
        else:
            if h0_ <= ap:
                raise InputError("Tiết diện quá thấp so với a'. Hãy tăng chiều cao dầm.")
            Acst = (Mn - aR * Rb * b * h0_ ** 2) / (Rsc * (h0_ - ap))
            Ast = (xR * Rb * b * h0_ + Rsc * Acst) / Rs
        cb_ = choose_bars(max(Ast, 0.001 * b * h0_), b, ds=p["ds"])
        ct_ = choose_bars(Acst, b, ds=p["ds"]) if Acst > 0 else None
        if cb_ is None or (Acst > 0 and ct_ is None):
            raise InputError("Không bố trí nổi cốt thép trong bề rộng b. Hãy tăng tiết diện.")
        na = max(a, cb_["a"])
        nap = max(ap, ct_["a"]) if ct_ else ap
        if na <= a + 0.5 and nap <= ap + 0.5:
            break
        a, ap = na, nap
    if a != a0 or ap != ap0:
        r.p(f"Giả thiết ban đầu a = {fmt(a0)} mm, a' = {fmt(ap0)} mm nhỏ hơn thực tế sau khi chọn thép. Chương trình tính lại với a = {fmt(a)} mm, a' = {fmt(ap)} mm.")
    h0 = _h0(h, a)
    r.calc("h_0", "h − a", f"{fmt(h)} − {fmt(a)}", h0, "mm")
    am = Mn / (Rb * b * h0 ** 2)
    r.calc("α_m", "[[M // γ_b1·R_b·b·h_0^2]]", f"[[{fmt(M)}·10^6 // {fmt(Rb)}·{fmt(b)}·{fmt(h0)}^2]]", am, key="am")
    if am >= 0.5:
        r.warn("αm quá lớn, tiết diện quá nhỏ. Nên tăng h hoặc cấp bền bê tông.")
    Asc = 0.0
    if am <= aR:
        r.p("Vì $α_m$ ≤ $α_R$: đặt cốt đơn.")
        xi = 1 - math.sqrt(1 - 2 * am)
        As = xi * Rb * b * h0 / Rs
        r.calc("ξ", "1 − √[[1 − 2α_m]]", f"1 − √[[1 − 2·{fmt(am)}]]", xi, key="xi")
        r.calc("A_s", "[[ξ·γ_b1·R_b·b·h_0 // R_s]]", f"[[{fmt(xi)}·{fmt(Rb)}·{fmt(b)}·{fmt(h0)} // {Rs}]]", As, "mm²", key="As")
    else:
        r.p("Vì $α_m$ > $α_R$: đặt cốt kép, lấy ξ = $ξ_R$.")
        Asc = (Mn - aR * Rb * b * h0 ** 2) / (Rsc * (h0 - ap))
        As = (xR * Rb * b * h0 + Rsc * Asc) / Rs
        r.calc("A'_s", "[[M − α_R·γ_b1·R_b·b·h_0^2 // R_sc(h_0 − a')]]", f"[[{fmt(M)}·10^6 − {fmt(aR)}·{fmt(Rb)}·{fmt(b)}·{fmt(h0)}^2 // {Rsc}({fmt(h0)} − {fmt(ap)})]]", Asc, "mm²", key="Asc")
        r.calc("A_s", "[[ξ_R·γ_b1·R_b·b·h_0 + R_sc·A'_s // R_s]]", f"[[{fmt(xR)}·{fmt(Rb)}·{fmt(b)}·{fmt(h0)} + {Rsc}·{fmt(Asc)} // {Rs}]]", As, "mm²", key="As")
    mu_min = 0.001
    Asmin = mu_min * b * h0
    if As < Asmin:
        r.p(f"Cốt thép tính được nhỏ hơn hàm lượng tối thiểu μmin = 0,1%: lấy $A_s$ = {fmt(Asmin)} mm².")
        As = Asmin
    r.results["As_req"] = As
    r.results["Asc_req"] = Asc
    r.h("Bước 3. Chọn cốt thép")
    kk = 1.0
    for _ in range(12):  # chọn thép; nếu a thực làm giảm chút Mgh thì tăng thép một cấp
        cb = choose_bars(As * kk, b, ds=p["ds"])
        if cb is None:
            raise InputError("Không bố trí nổi cốt thép trong bề rộng b. Hãy tăng tiết diện.")
        if Asc > 0:
            ct = choose_bars(max(Asc * kk, 1), b, ds=p["ds"])
            if ct is None:
                raise InputError("Không bố trí nổi cốt chịu nén. Hãy tăng tiết diện.")
        else:
            ct = dict(n=2, d=14, As=2 * bar_area(14), layers=1, a=25 + p["ds"] + 7, fit=2)
        if _rect_capacity(b, h - cb["a"], cb["As"], ct["As"] if Asc > 0 else 0.0, ct["a"], Rb, Rs, Rsc, xR)[0] >= Mn:
            break
        kk *= 1.03
    r.table(["Vị trí", "Yêu cầu (mm²)", "Chọn", "Diện tích (mm²)", "a thực (mm)"], [["Thép chịu kéo", As, f"{cb['n']}Ø{cb['d']}" + (" (2 lớp)" if cb["layers"] == 2 else ""), cb["As"], cb["a"]], ["Thép vùng nén" if Asc > 0 else "Thép cấu tạo trên", Asc, f"{ct['n']}Ø{ct['d']}", ct["As"], ct["a"]]])
    r.h("Bước 4. Kiểm tra lại với thép đã chọn")
    h0t = h - cb["a"]
    Ascc = ct["As"] if Asc > 0 else 0.0
    Mgh, x, st = _rect_capacity(b, h0t, cb["As"], Ascc, ct["a"], Rb, Rs, Rsc, xR)
    r.calc("h_0", "h − a_thực", f"{fmt(h)} − {fmt(cb['a'])}", h0t, "mm")
    r.calc("x", "[[R_s·A_s − R_sc·A'_s // γ_b1·R_b·b]]", f"[[{Rs}·{fmt(cb['As'])} − {Rsc}·{fmt(Ascc)} // {fmt(Rb)}·{fmt(b)}]]", x, "mm", key="x")
    if st == "x<2a'":
        r.calc("M_gh", "R_s·A_s(h_0 − a')", f"{Rs}·{fmt(cb['As'])}({fmt(h0t)} − {fmt(ct['a'])})·10^(-6)", Mgh / 1e6, "kN·m", key="Mgh")
    elif st == "x>xR":
        r.p("x vượt $ξ_R·h_0$, lấy x = $ξ_R·h_0$ trong công thức tính khả năng chịu lực.")
        r.calc("M_gh", "α_R·γ_b1·R_b·b·h_0^2 + R_sc·A'_s(h_0 − a')", None, Mgh / 1e6, "kN·m", key="Mgh")
    else:
        r.calc("M_gh", "γ_b1·R_b·b·x(h_0 − 0,5x) + R_sc·A'_s(h_0 − a')", f"{fmt(Rb)}·{fmt(b)}·{fmt(x)}({fmt(h0t)} − 0,5·{fmt(x)}) + {Rsc}·{fmt(Ascc)}({fmt(h0t)} − {fmt(ct['a'])})", Mgh / 1e6, "kN·m", key="Mgh")
    r.check("Khả năng chịu uốn", f"M = {fmt(M)} {'≤' if M * 1e6 <= Mgh else '>'} M_gh = {fmt(Mgh / 1e6)} kN·m", M * 1e6 <= Mgh * 1.0000001)
    mu = cb["As"] / (b * h0t)
    r.calc("μ", "[[A_s // b·h_0]]", f"[[{fmt(cb['As'])} // {fmt(b)}·{fmt(h0t)}]]", mu * 100, "%")
    r.check("Hàm lượng tối thiểu", f"μ = {fmt(mu * 100)}% ≥ μ_min = 0,1%", mu >= mu_min)
    r.h("Bước 5. Thể hiện cốt thép")
    st_end = dict(d=int(p["ds"]), s=int(p["s1"]))
    st_mid = dict(d=int(p["ds"]), s=int(p["s2"]))
    D, nst = beam_elevation(p["L"], b, h, p["sup"], cb, ct, st_end, st_mid, p["L"] / 4)
    r.fig(D, "Mặt đứng dầm (D1), tl: 1/50")
    r.fig(draw_rc_section(b, h, cb, ct, st_end, "1-1"), "Mặt cắt 1-1 tại gối", width_cm=6.5)
    r.fig(draw_rc_section(b, h, cb, ct, st_mid, "2-2"), "Mặt cắt 2-2 giữa nhịp", width_cm=6.5)
    sl, bc, hc = stirrup_len(b, h, p["ds"])
    Lbar = round10(p["L"] + p["sup"] - 50 + 2 * 10 * cb["d"])
    Ltop = round10(p["L"] + p["sup"] - 50 + 2 * 10 * ct["d"])
    r.h("Thống kê cốt thép", 3)
    r.note("Chiều dài thanh tính thẳng suốt nhịp và 2 đoạn bẻ móc 10d. Khi triển khai thật cần cắt thép, neo, nối theo yêu cầu cấu tạo của TCVN 5574:2018 mục 10.")
    kg = rebar_tables(r, "D1", [
        dict(id=1, shape=f"Thẳng, bẻ 2 đầu 10d={10 * cb['d']}", d=cb["d"], L=Lbar, n=cb["n"]),
        dict(id=2, shape=f"Thẳng, bẻ 2 đầu 10d={10 * ct['d']}", d=ct["d"], L=Ltop, n=ct["n"]),
        dict(id=3, shape=f"Đai {bc}×{hc}, móc 135°", d=int(p["ds"]), L=sl, n=nst),
    ])
    r.results["kg"] = kg
    r.results.update(n=cb["n"], d=cb["d"], Mgh_v=Mgh / 1e6)
    r.conclude(f"Thép chịu kéo: {cb['n']}Ø{cb['d']} (As = {fmt(cb['As'])} mm²); thép {'nén' if Asc > 0 else 'cấu tạo'}: {ct['n']}Ø{ct['d']}.")
    r.conclude(f"Mgh = {fmt(Mgh / 1e6)} kN·m " + ("≥" if r.ok else "<") + f" M = {fmt(M)} kN·m: " + ("ĐẠT." if r.ok else "KHÔNG ĐẠT."))
    r.conclude(f"Tổng lượng thép dầm: {fmt(kg, 1)} kg.")
    return r.finish()


@problem("btct_beam_check", "btct", "Dầm chữ nhật: kiểm tra khả năng chịu uốn",
         "Cho sẵn tiết diện, thép chịu kéo, thép chịu nén: tính x, Mgh, so với M.",
         [num("M", "Mômen M", "kN·m", 130, 0), _conc, _steel, _gb, num("b", "b", "mm", 220, 100), num("h", "h", "mm", 500, 150),
          num("n", "Số thanh thép kéo", "", 3, 1), num("d", "Ø thép kéo", "mm", 20, 6), num("a", "a", "mm", 45, 15),
          num("nc", "Số thanh thép nén", "", 2, 0), num("dc", "Ø thép nén", "mm", 16, 6), num("ap", "a'", "mm", 40, 15)])
def beam_check(p):
    r = Report("Dầm chữ nhật: kiểm tra khả năng chịu uốn", MOD, [STD])
    conc, s = CONCRETE[p["conc"]], REBAR[p["steel"]]
    gb = float(p["gb"])
    Rb = conc["Rb"] * gb
    Rs, Rsc = s["Rs"], s["Rsc"]
    b, h = p["b"], p["h"]
    As = p["n"] * bar_area(p["d"])
    Asc = p["nc"] * bar_area(p["dc"])
    r.given([("Mômen", "M", p["M"], "kN·m"), ("Tiết diện", "b×h", f"{fmt(b)}×{fmt(h)}", "mm"), ("Thép kéo", "As", f"{int(p['n'])}Ø{int(p['d'])} = {fmt(As)}", "mm²"), ("Thép nén", "A's", f"{int(p['nc'])}Ø{int(p['dc'])} = {fmt(Asc)}", "mm²")])
    _mat_rows(r, p, conc, s, gb, Rb)
    h0 = _h0(h, p["a"])
    xR = xi_R(Rs, s["Es"])
    r.h("Lời giải")
    r.calc("h_0", "h − a", None, h0, "mm")
    r.calc("ξ_R", "[[0,8 // 1 + R_s/(E_s·0,0035)]]", None, xR)
    x = (Rs * As - Rsc * Asc) / (Rb * b)
    r.calc("x", "[[R_s·A_s − R_sc·A'_s // γ_b1·R_b·b]]", f"[[{Rs}·{fmt(As)} − {Rsc}·{fmt(Asc)} // {fmt(Rb)}·{fmt(b)}]]", x, "mm", key="x")
    r.calc("ξ_R·h_0", None, None, xR * h0, "mm")
    Mgh, x, st = _rect_capacity(b, h0, As, Asc, p["ap"], Rb, Rs, Rsc, xR)
    if st == "x<2a'":
        r.p("Vì x < 2a': coi cốt nén không đạt $R_sc$, lấy mômen quanh trọng tâm thép nén.")
        r.calc("M_gh", "R_s·A_s(h_0 − a')", f"{Rs}·{fmt(As)}({fmt(h0)} − {fmt(p['ap'])})·10^(-6)", Mgh / 1e6, "kN·m", key="Mgh")
    elif st == "x>xR":
        r.p("Vì x > $ξ_R·h_0$: tiết diện quá thép, lấy x = $ξ_R·h_0$.")
        r.calc("M_gh", "α_R·γ_b1·R_b·b·h_0^2 + R_sc·A'_s(h_0 − a')", None, Mgh / 1e6, "kN·m", key="Mgh")
        r.warn("Tiết diện quá cốt thép: phá hoại giòn, nên tăng tiết diện hoặc cấp bền bê tông.")
    else:
        r.calc("M_gh", "γ_b1·R_b·b·x(h_0 − 0,5x) + R_sc·A'_s(h_0 − a')", f"{fmt(Rb)}·{fmt(b)}·{fmt(x)}({fmt(h0)} − 0,5·{fmt(x)}) + {Rsc}·{fmt(Asc)}({fmt(h0)} − {fmt(p['ap'])})", Mgh / 1e6, "kN·m", key="Mgh")
    ok = p["M"] * 1e6 <= Mgh
    r.check("Khả năng chịu uốn", f"M = {fmt(p['M'])} {'≤' if ok else '>'} M_gh = {fmt(Mgh / 1e6)} kN·m", ok)
    bot = dict(n=int(p["n"]), d=int(p["d"]), layers=1)
    top = dict(n=max(int(p["nc"]), 2), d=int(p["dc"]), layers=1)
    r.fig(draw_rc_section(b, h, bot, top, dict(d=8, s=150), "1-1"), "Mặt cắt dầm", width_cm=6.5)
    r.conclude(f"x = {fmt(x)} mm; Mgh = {fmt(Mgh / 1e6)} kN·m: " + ("ĐẠT." if ok else "KHÔNG ĐẠT."))
    return r.finish()


@problem("btct_tbeam", "btct", "Dầm tiết diện chữ T: tính cốt thép dọc",
         "Xác định bề rộng cánh tính toán, vị trí trục trung hoà trong cánh hay trong sườn, tính As.",
         [num("M", "Mômen dương M", "kN·m", 260, 0), _conc, _steel, _gb, num("b", "Bề rộng sườn b", "mm", 220, 100), num("h", "Chiều cao h", "mm", 500, 150),
          num("hf", "Chiều dày cánh h'f", "mm", 100, 30), num("bf", "Bề rộng cánh b'f", "mm", 1200, 100), num("a", "a giả thiết", "mm", 50, 15),
          num("L", "Nhịp dầm", "mm", 6000, 500), num("clr", "Khoảng cách thông thuỷ giữa các sườn", "mm", 3000, 0)])
def tbeam(p):
    r = Report("Dầm tiết diện chữ T: tính cốt thép dọc", MOD, [STD])
    conc, s = CONCRETE[p["conc"]], REBAR[p["steel"]]
    gb = float(p["gb"])
    Rb = conc["Rb"] * gb
    Rs = s["Rs"]
    b, h, hf, bf, a, M = p["b"], p["h"], p["hf"], p["bf"], p["a"], p["M"]
    if bf < b:
        raise InputError("Bề rộng cánh phải ≥ bề rộng sườn.")
    r.given([("Mômen", "M", M, "kN·m"), ("Sườn", "b×h", f"{fmt(b)}×{fmt(h)}", "mm"), ("Cánh", "b'f×h'f", f"{fmt(bf)}×{fmt(hf)}", "mm")])
    _mat_rows(r, p, conc, s, gb, Rb)
    r.h("Bước 1. Kiểm tra bề rộng cánh tính toán")
    Sf = (bf - b) / 2
    lim = [p["L"] / 6]
    desc = ["L/6"]
    if hf >= 0.1 * h:
        lim.append(p["clr"] / 2)
        desc.append("½ khoảng thông thuỷ giữa sườn")
    else:
        lim.append(6 * hf)
        desc.append("6h'f")
    Sfm = min(lim)
    r.calc("S_f", "[[b'_f − b // 2]]", f"[[{fmt(bf)} − {fmt(b)} // 2]]", Sf, "mm")
    r.p(f"Giới hạn độ vươn cánh (TCVN 5574:2018 mục 8.1.2): " + "; ".join(f"{d} = {fmt(v)} mm" for d, v in zip(desc, lim)) + ".")
    r.note("Với cánh công xôn (dầm biên, dầm độc lập), giới hạn độ vươn nhỏ hơn theo tỷ số h'f/h: hãy tự kiểm tra thêm.")
    r.check("Độ vươn cánh", f"S_f = {fmt(Sf)} {'≤' if Sf <= Sfm else '>'} {fmt(Sfm)} mm", Sf <= Sfm)
    if Sf > Sfm:
        bf = b + 2 * Sfm
        r.warn(f"Cánh nhập vượt giới hạn, chương trình lấy b'f = {fmt(bf)} mm để tính.")
    r.h("Bước 2. Vị trí trục trung hoà")
    h0 = _h0(h, a)
    Mf = Rb * bf * hf * (h0 - hf / 2)
    r.calc("h_0", "h − a", None, h0, "mm")
    r.calc("M_f", "γ_b1·R_b·b'_f·h'_f(h_0 − 0,5h'_f)", f"{fmt(Rb)}·{fmt(bf)}·{fmt(hf)}({fmt(h0)} − 0,5·{fmt(hf)})·10^(-6)", Mf / 1e6, "kN·m", key="Mf")
    xR = xi_R(Rs, s["Es"])
    aR = xR * (1 - xR / 2)
    r.calc("ξ_R", None, None, xR)
    r.calc("α_R", None, None, aR)
    Mn = M * 1e6
    r.h("Bước 3. Tính cốt thép")
    if Mn <= Mf:
        r.p("Vì M ≤ $M_f$: trục trung hoà qua cánh, tính như tiết diện chữ nhật $b'_f$ × h.")
        am = Mn / (Rb * bf * h0 ** 2)
        xi = 1 - math.sqrt(1 - 2 * am)
        As = xi * Rb * bf * h0 / Rs
        r.calc("α_m", "[[M // γ_b1·R_b·b'_f·h_0^2]]", f"[[{fmt(M)}·10^6 // {fmt(Rb)}·{fmt(bf)}·{fmt(h0)}^2]]", am, key="am")
        r.calc("ξ", "1 − √[[1 − 2α_m]]", None, xi)
        r.calc("A_s", "[[ξ·γ_b1·R_b·b'_f·h_0 // R_s]]", f"[[{fmt(xi)}·{fmt(Rb)}·{fmt(bf)}·{fmt(h0)} // {Rs}]]", As, "mm²", key="As")
        Asc = 0
    else:
        r.p("Vì M > $M_f$: trục trung hoà qua sườn.")
        Ff = Rb * (bf - b) * hf
        Mff = Ff * (h0 - hf / 2)
        am = (Mn - Mff) / (Rb * b * h0 ** 2)
        r.calc("α_m", "[[M − γ_b1·R_b(b'_f − b)h'_f(h_0 − 0,5h'_f) // γ_b1·R_b·b·h_0^2]]", f"[[{fmt(M)}·10^6 − {fmt(Rb)}({fmt(bf)} − {fmt(b)}){fmt(hf)}({fmt(h0)} − 0,5·{fmt(hf)}) // {fmt(Rb)}·{fmt(b)}·{fmt(h0)}^2]]", am, key="am")
        if am > aR:
            raise InputError(f"αm = {fmt(am)} > αR = {fmt(aR)}: cần cốt kép hoặc tăng tiết diện dầm T.")
        xi = 1 - math.sqrt(1 - 2 * am)
        As = (xi * Rb * b * h0 + Ff) / Rs
        r.calc("ξ", "1 − √[[1 − 2α_m]]", None, xi)
        r.calc("A_s", "[[ξ·γ_b1·R_b·b·h_0 + γ_b1·R_b(b'_f − b)h'_f // R_s]]", None, As, "mm²", key="As")
    Asmin = 0.001 * b * h0
    if As < Asmin:
        r.p(f"Lấy theo hàm lượng tối thiểu: $A_s$ = {fmt(Asmin)} mm².")
        As = Asmin
    cb = choose_bars(As, b)
    if cb is None:
        raise InputError("Không bố trí nổi cốt thép trong sườn dầm. Hãy tăng tiết diện.")
    r.h("Bước 4. Chọn thép")
    r.p(f"Chọn {cb['n']}Ø{cb['d']}, diện tích {fmt(cb['As'])} mm²" + (", xếp 2 lớp" if cb["layers"] == 2 else "") + f"; a thực = {fmt(cb['a'])} mm (giả thiết a = {fmt(a)} mm).")
    r.check("Diện tích thép", f"A_s,chọn = {fmt(cb['As'])} ≥ A_s,yc = {fmt(As)} mm²", cb["As"] >= As)
    r.check("Giả thiết a an toàn", f"a_thực = {fmt(cb['a'])} {'≤' if cb['a'] <= a else '>'} a giả thiết = {fmt(a)} mm", cb["a"] <= a)
    D = draw_rc_section(b, h, cb, dict(n=2, d=14, layers=1), dict(d=8, s=150), "1-1")
    D.rect(-(bf - b) / 2, h - hf, bf, hf, "BE_TONG")
    D.dim(-(bf - b) / 2, h + D.th * 7, b + (bf - b) / 2, h + D.th * 7, 0, f"b'f={fmt(bf)}")
    r.fig(D, "Mặt cắt dầm chữ T", width_cm=11)
    r.conclude(f"Thép chịu kéo: {cb['n']}Ø{cb['d']} (As = {fmt(cb['As'])} mm²).")
    return r.finish()


@problem("btct_shear", "btct", "Tính cốt đai chịu cắt",
         "Kiểm tra dải nghiêng, xét khả năng bê tông, chọn bước đai theo tiết diện nghiêng nguy hiểm.",
         [num("Q", "Lực cắt Q tại gối", "kN", 150, 0), num("q", "Tải phân bố trên dầm", "kN/m", 25, 0, help="Khi hoạt tải lớn, nên nhập tĩnh tải + ½ hoạt tải để an toàn"), _conc, _steelw, _gb,
          num("b", "b", "mm", 220, 100), num("h", "h", "mm", 500, 150), num("a", "a", "mm", 45, 15),
          sel("dw", "Đường kính đai", [6, 8, 10, 12], 8), sel("nw", "Số nhánh đai", [2, 3, 4], 2)])
def shear(p):
    r = Report("Tính cốt đai chịu cắt", MOD, [STD + " mục 8.1.3"])
    conc, sw = CONCRETE[p["conc"]], REBAR[p["steelw"]]
    gb = float(p["gb"])
    Rb, Rbt = conc["Rb"] * gb, conc["Rbt"] * gb
    Rsw = sw["Rsw"]
    b, h, Q, q = p["b"], p["h"], p["Q"], p["q"]
    h0 = _h0(h, p["a"])
    dw, nw = int(p["dw"]), int(p["nw"])
    r.given([("Lực cắt", "Q", Q, "kN"), ("Tải phân bố", "q", q, "kN/m"), ("Tiết diện", "b×h", f"{fmt(b)}×{fmt(h)}", "mm"), ("Đai", "Rsw", Rsw, "MPa"), ("Bê tông", "γb1·Rb; γb1·Rbt", f"{fmt(Rb)}; {fmt(Rbt)}", "MPa")])
    r.calc("h_0", "h − a", None, h0, "mm")
    r.h("Bước 1. Kiểm tra dải bê tông giữa các vết nứt nghiêng")
    Qs = 0.3 * Rb * b * h0 / 1e3
    r.calc("Q_max", "0,3·γ_b1·R_b·b·h_0", f"0,3·{fmt(Rb)}·{fmt(b)}·{fmt(h0)}·10^(-3)", Qs, "kN", key="Qstrut")
    r.check("Dải bê tông nghiêng", f"Q = {fmt(Q)} {'≤' if Q <= Qs else '>'} {fmt(Qs)} kN", Q <= Qs)
    if Q > Qs:
        r.warn("Không đạt: phải tăng tiết diện hoặc cấp bền bê tông. Cốt đai không khắc phục được.")
    r.h("Bước 2. Khả năng chịu cắt khi chưa có cốt đai")
    Qbmin = 0.5 * Rbt * b * h0 / 1e3
    r.calc("Q_(b,min)", "0,5·γ_b1·R_bt·b·h_0", f"0,5·{fmt(Rbt)}·{fmt(b)}·{fmt(h0)}·10^(-3)", Qbmin, "kN", key="Qbmin")
    Mb = 1.5 * Rbt * b * h0 ** 2  # N.mm
    r.calc("M_b", "1,5·γ_b1·R_bt·b·h_0^2", f"1,5·{fmt(Rbt)}·{fmt(b)}·{fmt(h0)}^2·10^(-6)", Mb / 1e6, "kN·m", key="Mb")
    r.h("Bước 3. Chọn đai")
    Asw = nw * bar_area(dw)
    r.calc("A_sw", "n·[[π·d_w^2 // 4]]", f"{nw}·[[π·{dw}^2 // 4]]", Asw, "mm²")
    smax_con = min(0.5 * h0, 300)
    smax_r = Rbt * b * h0 ** 2 / (Q * 1e3) if Q > 0 else 1e9
    r.calc("s_(ct)", "min(0,5·h_0; 300)", None, smax_con, "mm", note="Bước đai cấu tạo vùng gối")
    r.calc("s_(max)", "[[γ_b1·R_bt·b·h_0^2 // Q]]", f"[[{fmt(Rbt)}·{fmt(b)}·{fmt(h0)}^2 // {fmt(Q)}·10^3]]", smax_r, "mm", note="Giới hạn để vết nứt nghiêng không lọt giữa 2 đai")
    q_n_mm = q  # kN/m = N/mm
    chosen = None
    trials = [s for s in (300, 250, 200, 175, 150, 125, 100, 90, 80, 75, 60, 50) if s <= min(smax_con, smax_r)]
    if not trials:
        raise InputError("Không có bước đai phù hợp: Q quá lớn so với tiết diện.")

    def analyse(s):
        qsw = Rsw * Asw / s
        counted = qsw >= 0.25 * Rbt * b
        worst = None
        for C in np.linspace(h0, 3 * h0, 81):
            Qb = min(max(Mb / C, 0.5 * Rbt * b * h0), 2.5 * Rbt * b * h0)
            C0 = min(max(C, h0), 2 * h0)
            Qw = 0.75 * qsw * C0 if counted else 0.0
            Qd = Q * 1e3 - q_n_mm * C
            ratio = Qd / (Qb + Qw)
            if worst is None or ratio > worst[0]:
                worst = (ratio, C, Qb, Qw, Qd, C0)
        return qsw, counted, worst

    rows = []
    for s in trials:
        qsw, counted, w = analyse(s)
        rows.append([s, qsw, "có" if counted else "không", w[1], w[2] / 1e3, w[3] / 1e3, w[4] / 1e3, w[0]])
        if w[0] <= 1 and chosen is None:
            chosen = (s, qsw, counted, w)
    r.f("q_sw = [[R_sw·A_sw // s]];  Q_b = [[M_b // C]] (0,5R_bt·b·h_0 ≤ Q_b ≤ 2,5R_bt·b·h_0);  Q_sw = 0,75·q_sw·C_0 (h_0 ≤ C_0 ≤ 2h_0);  h_0 ≤ C ≤ 3h_0")
    r.table(["s (mm)", "qsw (N/mm)", "Tính Qsw?", "C nguy hiểm (mm)", "Qb (kN)", "Qsw (kN)", "Q(C) (kN)", "Tỷ số"], rows, "Thử các bước đai với điều kiện qsw ≥ 0,25·Rbt·b mới kể Qsw, Q(C) = Q − q·C.")
    if chosen is None:
        r.check("Khả năng chịu cắt", f"đai {nw} nhánh Ø{dw} chưa đủ với mọi bước đai đã thử", False)
        r.warn("Cần tăng đường kính, tăng số nhánh đai hoặc tăng tiết diện.")
        r.conclude(f"Đai {nw} nhánh Ø{dw} KHÔNG ĐẠT, cần tăng đai hoặc tiết diện.")
        return r.finish()
    s, qsw, counted, w = chosen
    r.h("Bước 4. Kiểm tra với bước đai đã chọn", 2)
    r.calc("q_sw", "[[R_sw·A_sw // s]]", f"[[{Rsw}·{fmt(Asw)} // {s}]]", qsw, "N/mm", key="qsw")
    r.calc("C", None, None, w[1], "mm", note="Hình chiếu tiết diện nghiêng nguy hiểm")
    r.calc("Q_b", "[[M_b // C]]", None, w[2] / 1e3, "kN")
    r.calc("Q_sw", "0,75·q_sw·C_0", f"0,75·{fmt(qsw)}·{fmt(w[5])}·10^(-3)", w[3] / 1e3, "kN")
    r.calc("Q(C)", "Q − q·C", f"{fmt(Q)} − {fmt(q)}·{fmt(w[1] / 1000)}", w[4] / 1e3, "kN")
    r.check("Tiết diện nghiêng", f"Q(C) = {fmt(w[4] / 1e3)} ≤ Q_b + Q_sw = {fmt((w[2] + w[3]) / 1e3)} kN", w[0] <= 1)
    s_mid = int(min(0.75 * h0, 500) // 10 * 10)
    r.h("Bước 5. Bố trí")
    r.p(f"Vùng gối (đoạn L/4 mỗi đầu): Ø{dw}a{s}, {nw} nhánh. Giữa nhịp: bước không vượt min(0,75$h_0$; 500) = {s_mid} mm, kiểm tra thêm theo lực cắt tại vị trí đó.")
    r.results.update(s=s, smid=s_mid)
    bot = dict(n=3, d=20, layers=1)
    top = dict(n=2, d=14, layers=1)
    r.fig(draw_rc_section(b, h, bot, top, dict(d=dw, s=s), "1-1"), "Mặt cắt tại gối, thép dọc chỉ minh hoạ", width_cm=6.5)
    r.conclude(f"Chọn đai Ø{dw}a{s}, {nw} nhánh ở vùng gối.")
    r.conclude(f"Q(C) = {fmt(w[4] / 1e3)} kN ≤ Qb + Qsw = {fmt((w[2] + w[3]) / 1e3)} kN: ĐẠT." if r.ok else "Tiết diện KHÔNG ĐẠT kiểm tra dải nghiêng.")
    return r.finish()


# ---------- CỘT ----------
def column_capacity(b, h, a, ap, As, Asp, Rb, Rs, Rsc, xR, N, e):
    """Kiểm tra tiết diện với N và e: trả về (Ngh·e tại N, x). Tính Mgh quanh trọng tâm As."""
    h0 = _h0(h, a)

    def resid(x):
        ss = Rs if x <= xR * h0 else max((2 * (1 - x / h0) / (1 - xR) - 1) * Rs, -Rsc)
        return Rb * b * x + Rsc * Asp - ss * As - N

    lo, hi = 0.0, h
    if resid(hi) < 0:
        x = h
    elif resid(lo) > 0:
        x = 0.0
    else:
        for _ in range(60):
            m = (lo + hi) / 2
            if resid(m) > 0:
                hi = m
            else:
                lo = m
        x = (lo + hi) / 2
    if x < 2 * ap and N > 0:
        Mgh = Rs * As * (h0 - ap) + N * (h0 - ap)
        return Mgh, x
    xx = min(x, h)
    return Rb * b * xx * (h0 - xx / 2) + Rsc * Asp * (h0 - ap), x


def design_symmetric(N, e, b, h, a, ap, Rb, Rs, Rsc, xR):
    """As = A's theo Sổ tay SP 52-101 (cách đặt ẩn αn, αm1, αs). Đơn vị N, mm."""
    h0 = _h0(h, a)
    d = ap / h0
    an = N / (Rb * b * h0)
    am1 = N * e / (Rb * b * h0 ** 2)
    As_ = (am1 - an * (1 - an / 2)) / (1 - d)
    if an <= xR:
        case = "lon"
        As = Rb * b * h0 / Rsc * As_
        if an < 2 * d:
            case = "lon_x_nho"
            ep = e - (h0 - ap)
            As = N * ep / (Rs * (h0 - ap))
    else:
        case = "be"
        xi = (an * (1 - xR) + 2 * max(As_, 0) * xR) / (1 - xR + 2 * max(As_, 0))
        As = Rb * b * h0 / Rsc * (am1 - xi * (1 - xi / 2)) / (1 - d)
    return max(As, 0.0), case, an, am1, As_


@problem("btct_column", "btct", "Cột chữ nhật nén lệch tâm phẳng, thép đối xứng",
         "Độ mảnh, độ lệch tâm, uốn dọc η, phân biệt lệch tâm lớn, bé, tính As = A's, chọn thép, kiểm tra lại.",
         [num("N", "Lực nén N", "kN", 1200, 0), num("M", "Mômen M", "kN·m", 180, 0), num("NL", "Phần dài hạn của N", "kN", 800, 0), num("ML", "Phần dài hạn của M", "kN·m", 90, 0),
          _conc, _steel, sel("gb", "Hệ số điều kiện làm việc của bê tông", [(0.9, "γb1 = 0,9: có tải dài hạn"), (1.0, "1,0: chỉ tải ngắn hạn"), (0.765, "0,9 × γb3 = 0,85: cột đổ bê tông đứng, cao trên 1,5 m")], 0.9), num("b", "Cạnh b (vuông góc mặt phẳng uốn)", "mm", 300, 150), num("h", "Cạnh h (trong mặt phẳng uốn)", "mm", 500, 150),
          num("a", "a = a'", "mm", 45, 15), num("l0", "Chiều dài tính toán l0 trong mặt phẳng uốn", "mm", 3300, 0), num("l0b", "Chiều dài tính toán ngoài mặt phẳng uốn", "mm", 3300, 0), num("L", "Chiều cao cột (để tính ea)", "mm", 3300, 0),
          sel("sys", "Loại kết cấu", [("sieu", "Siêu tĩnh"), ("tinh", "Tĩnh định")]),
          sel("dw", "Đường kính đai", [6, 8, 10], 8), num("sw", "Bước đai", "mm", 200, 50), num("name_n", "Số cột giống nhau", "", 1, 1)])
def column(p):
    r = Report("Cột nén lệch tâm phẳng, thép đối xứng", MOD, [STD + " mục 8.1.2"])
    conc, s = CONCRETE[p["conc"]], REBAR[p["steel"]]
    gb = float(p["gb"])
    Rb = conc["Rb"] * gb
    Rs, Rsc = s["Rs"], s["Rsc"]
    b, h, a, l0, L = p["b"], p["h"], p["a"], p["l0"], p["L"]
    N = p["N"] * 1e3
    M = p["M"] * 1e6
    if N <= 0:
        raise InputError("Lực nén N phải dương.")
    r.given([("Lực dọc, mômen", "N; M", f"{fmt(p['N'])}; {fmt(p['M'])}", "kN; kN·m"), ("Phần dài hạn", "NL; ML", f"{fmt(p['NL'])}; {fmt(p['ML'])}", "kN; kN·m"), ("Tiết diện", "b×h", f"{fmt(b)}×{fmt(h)}", "mm"), ("Chiều dài tính toán", "l0", l0, "mm")])
    _mat_rows(r, p, conc, s, gb, Rb)
    h0 = _h0(h, a)
    xR = xi_R(Rs, s["Es"])
    r.h("Bước 1. Độ lệch tâm")
    e1 = M / N
    ea = max(L / 600, h / 30, 10)
    r.calc("h_0", "h − a", None, h0, "mm")
    r.calc("e_1", "[[M // N]]", f"[[{fmt(p['M'])}·10^6 // {fmt(p['N'])}·10^3]]", e1, "mm", key="e1")
    r.calc("e_a", "max([[L // 600]]; [[h // 30]]; 10)", f"max({fmt(L / 600)}; {fmt(h / 30)}; 10)", ea, "mm", key="ea")
    if p["sys"] == "sieu":
        e0 = max(e1, ea)
        r.calc("e_0", "max(e_1; e_a)", None, e0, "mm", note="Kết cấu siêu tĩnh", key="e0")
    else:
        e0 = e1 + ea
        r.calc("e_0", "e_1 + e_a", None, e0, "mm", note="Kết cấu tĩnh định", key="e0")
    r.h("Bước 2. Ảnh hưởng uốn dọc")
    i = h / math.sqrt(12)
    lam = l0 / i
    r.calc("λ", "[[l_0 // i]]", f"[[{fmt(l0)} // {fmt(i)}]]", lam, key="lam")
    Es, Eb = s["Es"], conc["Eb"]
    I = b * h ** 3 / 12
    As_guess = max(0.005 * b * h0, 0.0)
    eta = 1.0
    As_final = None
    if lam <= 14:
        r.p("Vì λ ≤ 14: bỏ qua ảnh hưởng uốn dọc, η = 1.")
    else:
        d_e = max(min(e0 / h, 1.5), 0.15)
        r.calc("δ_e", "[[e_0 // h]]  (0,15 ≤ δ_e ≤ 1,5)", None, d_e)
        M1 = M + N * (h0 - a) / 2
        M1L = p["ML"] * 1e6 + p["NL"] * 1e3 * (h0 - a) / 2
        phiL = min(1 + M1L / M1, 2.0) if M1 > 0 else 1.0
        r.calc("M_1", "M + N·[[h_0 − a // 2]]", None, M1 / 1e6, "kN·m")
        r.calc("M_(1L)", "M_L + N_L·[[h_0 − a // 2]]", None, M1L / 1e6, "kN·m")
        r.calc("φ_L", "1 + [[M_(1L) // M_1]] ≤ 2", None, phiL)
        kb = 0.15 / (phiL * (0.3 + d_e))
        ks = 0.7
        r.calc("k_b", "[[0,15 // φ_L(0,3 + δ_e)]]", None, kb)
        r.calc("k_s", None, None, ks)
        r.calc("I", "[[b·h^3 // 12]]", None, I, "mm⁴")
    r.h("Bước 3. Tính cốt thép (lặp vì D phụ thuộc thép)")
    iters = []
    converged = False
    for it in range(60):
        if lam > 14:
            Is = 2 * As_guess * ((h0 - a) / 2) ** 2
            D = kb * Eb * I + ks * Es * Is
            Ncr = math.pi ** 2 * D / l0 ** 2
            if N >= Ncr:
                if it == 0 or As_guess < 0.06 * b * h:
                    As_guess = max(As_guess * 1.5, 0.01 * b * h0)  # thép ít quá làm D nhỏ: thử tăng thép
                    iters.append([it + 1, fmt(As_guess), "N ≥ Ncr", "–", "–"])
                    continue
                raise InputError(f"N = {fmt(N / 1e3)} kN ≥ Ncr = {fmt(Ncr / 1e3)} kN ngay cả khi hàm lượng thép 6%: cột mất ổn định. Tăng tiết diện cột.")
            eta = 1 / (1 - N / Ncr)
        e = e0 * eta + (h0 - a) / 2
        As_req, case, an, am1, As_ = design_symmetric(N, e, b, h, a, a, Rb, Rs, Rsc, xR)
        As_new = max(As_req, 0.0)
        iters.append([it + 1, fmt(As_guess), fmt(eta), fmt(e), fmt(As_new)])
        if abs(As_new - As_guess) < 1 and it > 0:
            As_guess = As_new
            converged = True
            break
        As_guess = 0.5 * (As_guess + As_new)  # giảm dao động với cột mảnh
    if not converged:
        r.warn("Vòng lặp thép và uốn dọc chưa hội tụ: cột quá mảnh so với tải. Nên tăng tiết diện. Phép kiểm tra lại ở Bước 4 vẫn dùng đúng thép đã chọn.")
    if len(iters) > 8:
        iters = iters[:3] + [["...", "...", "...", "...", "..."]] + iters[-3:]
    r.table(["Vòng lặp", "As giả thiết (mm²)", "η", "e (mm)", "As tính (mm²)"], iters, widths=[2.5, 4, 2.5, 3, 4])
    if lam > 14:
        r.calc("I_s", "2A_s([[h_0 − a // 2]])^2", None, Is, "mm⁴")
        r.calc("D", "k_b·E_b·I + k_s·E_s·I_s", None, D / 1e9, "kN·m²", key="D")
        r.calc("N_cr", "[[π^2·D // l_0^2]]", None, Ncr / 1e3, "kN", key="Ncr")
        r.calc("η", "[[1 // 1 − N/N_cr]]", f"[[1 // 1 − {fmt(N / 1e3)}/{fmt(Ncr / 1e3)}]]", eta, key="eta")
    r.results["eta"] = eta
    r.calc("e", "e_0·η + [[h_0 − a // 2]]", f"{fmt(e0)}·{fmt(eta)} + [[{fmt(h0)} − {fmt(a)} // 2]]", e, "mm", key="e")
    x1 = N / (Rb * b)
    r.calc("x_1", "[[N // γ_b1·R_b·b]]", f"[[{fmt(p['N'])}·10^3 // {fmt(Rb)}·{fmt(b)}]]", x1, "mm", key="x1")
    r.calc("ξ_R·h_0", None, f"{fmt(xR)}·{fmt(h0)}", xR * h0, "mm")
    r.calc("α_n", "[[N // γ_b1·R_b·b·h_0]]", None, an)
    r.calc("α_(m1)", "[[N·e // γ_b1·R_b·b·h_0^2]]", None, am1)
    r.calc("δ", "[[a' // h_0]]", None, a / h0)
    if case == "lon_x_nho":
        r.p("Vì $x_1$ < 2a': lệch tâm lớn, cốt nén không đạt $R_sc$. Lấy mômen quanh trọng tâm cốt nén:")
        r.calc("A_s = A'_s", "[[N(e − h_0 + a') // R_s(h_0 − a')]]", None, As_guess, "mm²", key="As")
    elif case == "lon":
        r.p("Vì 2a' ≤ $x_1$ ≤ $ξ_R·h_0$: lệch tâm lớn (phá hoại từ vùng kéo).")
        r.calc("A_s = A'_s", "[[γ_b1·R_b·b·h_0 // R_sc]]·[[α_(m1) − α_n(1 − 0,5α_n) // 1 − δ]]", None, As_guess, "mm²", key="As")
    else:
        r.p("Vì $x_1$ > $ξ_R·h_0$: lệch tâm bé (phá hoại từ vùng nén).")
        r.calc("α_s", "[[α_(m1) − α_n(1 − 0,5α_n) // 1 − δ]]", None, As_)
        r.f("ξ = [[α_n(1 − ξ_R) + 2α_s·ξ_R // 1 − ξ_R + 2α_s]]")
        r.calc("A_s = A'_s", "[[γ_b1·R_b·b·h_0 // R_sc]]·[[α_(m1) − ξ(1 − 0,5ξ) // 1 − δ]]", None, As_guess, "mm²", key="As")
    # hàm lượng tối thiểu theo độ mảnh
    mu_min = 0.001 if lam <= 17 else (0.0025 if lam >= 87 else 0.001 + 0.0015 * (lam - 17) / 70)
    Asmin = mu_min * b * h0
    r.calc("μ_min", None, None, mu_min * 100, "%", note="Nội suy theo độ mảnh: 0,1% khi λ ≤ 17 đến 0,25% khi λ ≥ 87")
    As_use = As_guess
    if As_use < Asmin:
        r.p(f"Lấy $A_s$ = $μ_min·b·h_0$ = {fmt(Asmin)} mm² mỗi phía.")
        As_use = Asmin
    r.results["As_use"] = As_use
    r.results["case"] = case
    r.h("Bước 4. Chọn thép và kiểm tra lại")
    def weak_axis(cb_, a_w):
        """Khả năng chịu lực theo phương cạnh b với độ lệch tâm ngẫu nhiên."""
        nb_ = cb_["n"]
        Ab1_ = cb_["As"] / nb_
        lay = [(a_w + (b - 2 * a_w) * k_ / (nb_ - 1) if nb_ > 1 else b / 2, 2 * Ab1_) for k_ in range(nb_)]
        Is_ = sum(A_ * (y - b / 2) ** 2 for y, A_ in lay)
        ea_ = max(L / 600, b / 30, 10)
        lam_ = p["l0b"] / (b / math.sqrt(12))
        eta_ = 1.0
        kb_ = Ncr_ = None
        if lam_ > 14:
            de_ = max(min(ea_ / b, 1.5), 0.15)
            kb_ = 0.15 / (2.0 * (0.3 + de_))
            Ncr_ = math.pi ** 2 * (kb_ * Eb * h * b ** 3 / 12 + 0.7 * Es * Is_) / p["l0b"] ** 2
            if N >= Ncr_:
                return dict(ok=False, stable=False, lam=lam_, ea=ea_, kb=kb_, Ncr=Ncr_, eta=None, Ng=0.0)
            eta_ = 1 / (1 - N / Ncr_)
        Ng_, _ = uniaxial_capacity(h, b, lay, ea_ * eta_, Rb, Rs, Rsc, Es)
        return dict(ok=N <= Ng_, stable=True, lam=lam_, ea=ea_, kb=kb_, Ncr=Ncr_, eta=eta_, Ng=Ng_)

    kk = 1.0
    ok = False
    for _ in range(120):  # kiểm tra lại đúng thép chọn: a thực, η theo thép thực, cả hai phương
        cb = choose_bars(As_use * kk, b, ds=p["dw"], bars=[14, 16, 18, 20, 22, 25, 28, 32], min_n=2)
        if cb is None:
            raise InputError("Không bố trí nổi lượng thép đủ chịu lực trong tiết diện này (xét cả hai phương). Hãy tăng tiết diện cột.")
        a_t = max(a, cb["a"])
        h0t = h - a_t
        eta_t = 1.0
        Ncr_t = float("inf")
        stable = True
        if lam > 14:
            Is_t = 2 * cb["As"] * ((h0t - a_t) / 2) ** 2
            D_t = kb * Eb * I + ks * Es * Is_t
            Ncr_t = math.pi ** 2 * D_t / l0 ** 2
            stable = N < Ncr_t
            if stable:
                eta_t = 1 / (1 - N / Ncr_t)
        e_t = e0 * eta_t + (h0t - a_t) / 2
        if stable:
            Mgh, x = column_capacity(b, h, a_t, a_t, cb["As"], cb["As"], Rb, Rs, Rsc, xi_R(Rs, Es), N, e_t)
            if N * e_t <= Mgh and weak_axis(cb, a_t)["ok"]:
                ok = True
                break
        if 2 * cb["As"] / (b * h) > 0.06:
            break
        kk *= 1.05
    if kk > 1.0:
        r.p(f"Với thép theo công thức, phép kiểm tra lại chưa đạt, chương trình tăng thép lên {fmt(kk, 2)} lần.")
    r.p(f"Chọn mỗi phía {cb['n']}Ø{cb['d']}, $A_s$ = {fmt(cb['As'])} mm²; a thực = {fmt(a_t)} mm.")
    At = 2 * cb["As"]
    mut = At / (b * h)
    r.calc("μ_t", "[[2A_s // b·h]]", None, mut * 100, "%")
    if mut > 0.06:
        r.warn("Hàm lượng thép vượt 6%, nên tăng tiết diện cột.")
    if not stable:
        r.check("Ổn định trong mặt phẳng uốn", f"N = {fmt(N / 1e3)} ≥ N_cr = {fmt(Ncr_t / 1e3)} kN", False)
        Mgh, x = 0.0, 0.0
    else:
        r.calc("η", "[[1 // 1 − N/N_cr]]", None, eta_t, note="Tính lại theo thép đã chọn", key="eta")
        r.calc("e", "e_0·η + [[h_0 − a // 2]]", None, e_t, "mm", key="e")
        r.calc("x", None, None, x, "mm", note="Giải từ phương trình cân bằng lực với σs theo mục 8.1.2")
        Ne = N * e_t
        r.check("Khả năng chịu lực trong mặt phẳng uốn", f"N·e = {fmt(Ne / 1e6)} {'≤' if Ne <= Mgh else '>'} M_gh = {fmt(Mgh / 1e6)} kN·m", Ne <= Mgh)
    r.h("Bước 5. Kiểm tra ngoài mặt phẳng uốn")
    r.p("Theo phương cạnh b, chỉ có độ lệch tâm ngẫu nhiên.")
    wk = weak_axis(cb, a_t)
    r.calc("λ_b", "[[l_0b // i_b]]", f"[[{fmt(p['l0b'])} // {fmt(b / math.sqrt(12))}]]", wk["lam"], key="lamb")
    r.calc("e_a", "max([[L // 600]]; [[b // 30]]; 10)", None, wk["ea"], "mm")
    if max(lam, wk["lam"]) > 120:
        r.warn("Độ mảnh cột vượt 120: không nên dùng.")
    if wk["kb"] is not None:
        r.calc("k_b", "[[0,15 // φ_L(0,3 + δ_e)]]", None, wk["kb"], note="Lấy φL = 2 thiên về an toàn")
        r.calc("N_cr", "[[π^2·D // l_0b^2]]", None, wk["Ncr"] / 1e3, "kN", key="Ncrb")
    if wk["stable"]:
        if wk["eta"] and wk["eta"] > 1:
            r.calc("η_b", None, None, wk["eta"])
        r.calc("N_(gh,b)", None, None, wk["Ng"] / 1e3, "kN", note="Biến dạng tuyến tính, khối nén chữ nhật, đủ các hàng thép theo cạnh b", key="Ngb")
        r.check("Khả năng chịu lực ngoài mặt phẳng uốn", f"N = {fmt(N / 1e3)} {'≤' if wk['ok'] else '>'} N_gh,b = {fmt(wk['Ng'] / 1e3)} kN", wk["ok"])
    else:
        r.check("Ổn định ngoài mặt phẳng uốn", f"N = {fmt(N / 1e3)} ≥ N_cr,b = {fmt(wk['Ncr'] / 1e3)} kN", False)
    if r.ok is False:
        r.warn("Cột không đạt: tăng tiết diện, đặc biệt cạnh b nếu lỗi ở ngoài mặt phẳng uốn.")
    D2 = draw_rc_section(b, h, cb, cb, dict(d=int(p["dw"]), s=int(p["sw"])), "1-1", ids=(1, 2, 3))
    r.fig(D2, "Mặt cắt cột (thép đối xứng)", width_cm=6.5)
    sl, bc, hc = stirrup_len(b, h, p["dw"])
    Lb = round10(L + 500)
    nst = int(L // p["sw"]) + 1
    n_col = int(p["name_n"])
    kg = rebar_tables(r, "C1", [
        dict(id=1, shape="Thẳng, gồm đoạn nối 500", d=cb["d"], L=Lb, n=cb["n"]),
        dict(id=2, shape="Thẳng, gồm đoạn nối 500", d=cb["d"], L=Lb, n=cb["n"]),
        dict(id=3, shape=f"Đai {bc}×{hc}, móc 135°", d=int(p["dw"]), L=sl, n=nst),
    ], count=n_col)
    r.results["kg"] = kg
    r.conclude(f"η = {fmt(eta_t)}; e = {fmt(e_t)} mm; trường hợp: " + {"lon": "lệch tâm lớn", "lon_x_nho": "lệch tâm lớn, x < 2a'", "be": "lệch tâm bé"}[case] + ".")
    r.conclude(f"Mỗi phía {cb['n']}Ø{cb['d']} (As = {fmt(cb['As'])} mm²), μt = {fmt(mut * 100)}%: " + ("ĐẠT cả hai phương." if r.ok else "KHÔNG ĐẠT."))
    return r.finish()


# ---------- CỘT LỆCH TÂM XIÊN ----------
def uniaxial_capacity(b, h, layers, e0_from_center, Rb, Rs, Rsc, Es=200000.0):
    """Lực giới hạn N (N) ứng với độ lệch tâm e tính từ trọng tâm hình học.

    Khối nén chữ nhật 1,0Rb cao 0,8xc, biến dạng tuyến tính εb2 = 0,0035.
    layers: [(y từ thớ nén, As)]. Dùng phân đôi trên xc."""
    def NM(xc):
        xb = min(0.8 * xc, h)
        C = Rb * b * xb
        MM = C * (h / 2 - xb / 2)
        NN = C
        for y, A in layers:
            eps = 0.0035 * (xc - y) / xc
            fy = Rsc if eps >= 0 else Rs
            sg = max(-fy, min(fy, eps * Es))
            NN += sg * A
            MM += sg * A * (h / 2 - y)
        return NN, MM

    lo, hi = 1.0, 8 * h
    # e giảm khi xc tăng
    for _ in range(80):
        m = (lo + hi) / 2
        NN, MM = NM(m)
        if NN <= 0:
            lo = m
            continue
        if MM / NN > e0_from_center:
            lo = m
        else:
            hi = m
    NN, MM = NM((lo + hi) / 2)
    return NN, (lo + hi) / 2


def _layers_for(hh, nh, nb, Ab, a):
    """Lớp thép theo phương chiều cao hh: nh thanh mỗi hàng biên (hai hàng), nb thanh ở biên kia."""
    out = [(a, nh * Ab), (hh - a, nh * Ab)]
    if nb > 2:
        k = nb - 2
        for i in range(k):
            y = a + (hh - 2 * a) * (i + 1) / (k + 1)
            out.append((y, 2 * Ab))
    return out


@problem("btct_column_biaxial", "btct", "Cột chữ nhật nén lệch tâm xiên",
         "Chọn thép chu vi, xét uốn dọc hai phương, kiểm tra theo công thức tương tác Bresler với khả năng chịu lực từng phương.",
         [num("N", "Lực nén N", "kN", 1500, 0), num("Mx", "Mô men Mx (uốn quanh trục x, theo cạnh Cy)", "kN·m", 90, 0), num("My", "Mô men My (uốn quanh trục y, theo cạnh Cx)", "kN·m", 60, 0),
          _conc, _steel, _gb, num("Cx", "Cạnh Cx", "mm", 400, 150), num("Cy", "Cạnh Cy", "mm", 400, 150), num("a", "a", "mm", 45, 15),
          num("l0x", "l0 uốn theo cạnh Cx", "mm", 3300, 0), num("l0y", "l0 uốn theo cạnh Cy", "mm", 3300, 0),
          num("nx", "Số thanh trên cạnh Cx", "", 3, 2), num("ny", "Số thanh trên cạnh Cy", "", 3, 2), num("d0", "Ø nhỏ nhất thử chọn", "mm", 16, 12), num("philong", "Hệ số φL (1 đến 2)", "", 1.5, 1, 2)])
def column_biaxial(p):
    r = Report("Cột chữ nhật nén lệch tâm xiên", MOD, [STD, "Công thức tương tác Bresler (không thuộc TCVN 5574:2018)"])
    conc, s = CONCRETE[p["conc"]], REBAR[p["steel"]]
    gb = float(p["gb"])
    Rb = conc["Rb"] * gb
    Rs, Rsc = s["Rs"], s["Rsc"]
    Es, Eb = s["Es"], conc["Eb"]
    Cx, Cy, a = p["Cx"], p["Cy"], p["a"]
    N = p["N"] * 1e3
    nx, ny = int(p["nx"]), int(p["ny"])
    if N <= 0:
        raise InputError("Lực nén N phải dương.")
    r.given([("Lực nén", "N", p["N"], "kN"), ("Mô men", "Mx; My", f"{fmt(p['Mx'])}; {fmt(p['My'])}", "kN·m"), ("Tiết diện", "Cx×Cy", f"{fmt(Cx)}×{fmt(Cy)}", "mm"), ("Thép chu vi", "nx; ny", f"{nx}; {ny}", "thanh")])
    _mat_rows(r, p, conc, s, gb, Rb)
    r.note("TCVN 5574:2018 khuyến nghị mô hình biến dạng phi tuyến cho nén lệch tâm xiên. Dạng bài này dùng công thức Bresler, phổ biến và thiên về an toàn khi N ≥ 0,1N₀, phù hợp để tính sơ bộ và đối chiếu. Hồ sơ quan trọng nên kiểm tra lại bằng phần mềm mô hình biến dạng.")
    r.h("Bước 1. Độ lệch tâm theo hai phương")
    # phương theo Cy: Mx ; phương theo Cx: My
    ex1 = p["My"] * 1e6 / N
    ey1 = p["Mx"] * 1e6 / N
    eax = max(p["l0x"] / 600, Cx / 30, 10)
    eay = max(p["l0y"] / 600, Cy / 30, 10)
    ex0 = max(ex1, eax)
    ey0 = max(ey1, eay)
    r.calc("e_x", "max([[M_y // N]]; e_(a,x))", f"max({fmt(ex1)}; {fmt(eax)})", ex0, "mm", key="ex0")
    r.calc("e_y", "max([[M_x // N]]; e_(a,y))", f"max({fmt(ey1)}; {fmt(eay)})", ey0, "mm", key="ey0")
    r.h("Bước 2. Thử chọn thép và kiểm tra")
    r.f("[[1 // N_gh]] = [[1 // N_x]] + [[1 // N_y]] − [[1 // N_0]]")
    r.p("$N_x$, $N_y$: lực giới hạn khi chỉ lệch tâm theo từng phương, tính bằng biến dạng tuyến tính với $ε_b2$ = 0,0035 và khối nén chữ nhật cao 0,8$x_c$.")
    A = Cx * Cy
    rows = []
    found = None
    for d in (int(p["d0"]), 18, 20, 22, 25, 28, 32):
        if d < p["d0"]:
            continue
        Ab = bar_area(d)
        nt = 2 * nx + 2 * ny - 4
        Ast = nt * Ab
        Isx = sum(A_ * (y - Cx / 2) ** 2 for y, A_ in _layers_for(Cx, ny, nx, Ab, a))
        Isy = sum(A_ * (y - Cy / 2) ** 2 for y, A_ in _layers_for(Cy, nx, ny, Ab, a))
        etas = []
        for (hh, bb, Is, e0, l0) in ((Cx, Cy, Isx, ex0, p["l0x"]), (Cy, Cx, Isy, ey0, p["l0y"])):
            lam = l0 / (hh / math.sqrt(12))
            if lam <= 14:
                etas.append((1.0, lam, float("inf")))
                continue
            de = max(min(e0 / hh, 1.5), 0.15)
            kb = 0.15 / (p["philong"] * (0.3 + de))
            D = kb * Eb * bb * hh ** 3 / 12 + 0.7 * Es * Is
            Ncr = math.pi ** 2 * D / l0 ** 2
            if N >= Ncr:
                etas.append((None, lam, Ncr))
            else:
                etas.append((1 / (1 - N / Ncr), lam, Ncr))
        if etas[0][0] is None or etas[1][0] is None:
            rows.append([f"{nt}Ø{d}", fmt(100 * Ast / A), "mất ổn định", "–", "–", "–", "–", "–"])
            continue
        Nx, _ = uniaxial_capacity(Cy, Cx, _layers_for(Cx, ny, nx, Ab, a), ex0 * etas[0][0], Rb, Rs, Rsc, Es)
        Ny, _ = uniaxial_capacity(Cx, Cy, _layers_for(Cy, nx, ny, Ab, a), ey0 * etas[1][0], Rb, Rs, Rsc, Es)
        N0 = Rb * A + Rsc * Ast
        Ngh = 1 / (1 / Nx + 1 / Ny - 1 / N0)
        rows.append([f"{nt}Ø{d}", fmt(100 * Ast / A), fmt(etas[0][0]), fmt(etas[1][0]), fmt(Nx / 1e3), fmt(Ny / 1e3), fmt(N0 / 1e3), fmt(Ngh / 1e3)])
        if Ngh >= N and found is None:
            found = dict(d=d, nt=nt, Ast=Ast, Nx=Nx, Ny=Ny, N0=N0, Ngh=Ngh, etax=etas[0][0], etay=etas[1][0], lamx=etas[0][1], lamy=etas[1][1])
    r.table(["Thép", "μt (%)", "ηx", "ηy", "Nx (kN)", "Ny (kN)", "N0 (kN)", "Ngh (kN)"], rows)
    if found is None:
        r.check("Khả năng chịu lực", "thép đến Ø32 chưa đủ", False)
        r.warn("Cần tăng tiết diện cột, tăng số thanh hoặc tăng cấp bền bê tông.")
        r.conclude("Tiết diện KHÔNG ĐẠT với bố trí thép đã cho.")
        return r.finish()
    f = found
    r.h("Bước 3. Kết quả kiểm tra")
    r.calc("λ_x", None, None, f["lamx"])
    r.calc("λ_y", None, None, f["lamy"])
    r.calc("N_0", "γ_b1·R_b·A + R_sc·A_st", f"{fmt(Rb)}·{fmt(A)} + {Rsc}·{fmt(f['Ast'])}", f["N0"] / 1e3, "kN")
    r.calc("N_gh", "[[1 // 1/N_x + 1/N_y − 1/N_0]]", f"[[1 // 1/{fmt(f['Nx'] / 1e3)} + 1/{fmt(f['Ny'] / 1e3)} − 1/{fmt(f['N0'] / 1e3)}]]", f["Ngh"] / 1e3, "kN", key="Ngh")
    r.check("Khả năng chịu lực", f"N = {fmt(p['N'])} ≤ N_gh = {fmt(f['Ngh'] / 1e3)} kN", f["Ngh"] >= N)
    r.check("Điều kiện áp dụng công thức Bresler", f"N = {fmt(p['N'])} {'≥' if N >= 0.1 * f['N0'] else '<'} 0,1N_0 = {fmt(0.1 * f['N0'] / 1e3)} kN", N >= 0.1 * f["N0"])
    if N < 0.1 * f["N0"]:
        r.warn("N < 0,1N₀: công thức Bresler không đủ tin cậy, phải kiểm tra theo uốn xiên hoặc mô hình biến dạng.")
    th = max(Cx, Cy) / 11
    D = Drawing(th, "cot_xien")
    D.rect(0, 0, Cx, Cy, "BE_TONG")
    D.rect(25, 25, Cx - 50, Cy - 50, "DAI")
    c = 25 + 8 + f["d"] / 2
    pts = set()
    for i in range(nx):
        x = c + (Cx - 2 * c) * i / (nx - 1)
        pts.add((round(x, 1), round(c, 1)))
        pts.add((round(x, 1), round(Cy - c, 1)))
    for i in range(ny):
        y = c + (Cy - 2 * c) * i / (ny - 1)
        pts.add((round(c, 1), round(y, 1)))
        pts.add((round(Cx - c, 1), round(y, 1)))
    for (x, y) in pts:
        D.circle(x, y, f["d"] / 2, "THEP", True)
    D.tag(Cx / 2, Cy - c, Cx * .55, Cy + th * 1.6, f"{f['nt']}Ø{f['d']}", 1)
    D.tag(Cx - 25, Cy / 2, Cx + th * 1.2, Cy / 2 + th * 1.2, "Ø8a200", 2)
    D.dim(0, -th * 1.8, Cx, -th * 1.8, 0, fmt(Cx))
    D.dim(0, 0, 0, Cy, th * 1.4, fmt(Cy))
    D.title(Cx / 2, -th * 4.2, "1-1", "tl: 1/25")
    r.fig(D, "Mặt cắt cột", width_cm=9)
    r.results.update(d=f["d"], nt=f["nt"], Ngh_v=f["Ngh"] / 1e3)
    r.conclude(f"Chọn {f['nt']}Ø{f['d']} chu vi (μt = {fmt(100 * f['Ast'] / A)}%). Ngh = {fmt(f['Ngh'] / 1e3)} kN, N = {fmt(p['N'])} kN: " + ("ĐẠT." if r.ok else "KHÔNG ĐẠT, xem mục kiểm tra."))
    return r.finish()


# ---------- SÀN ----------
def plate_coeffs(l1, l2, edges, nu=0.2, n=40):
    """Giải sai phân bản Kirchhoff chịu tải đều, q=1, D=1.

    edges = (trái x=0, phải x=l2, dưới y=0, trên y=l1), 'k' khớp, 'n' ngàm. l1 (ngắn) theo y.
    Trả về: α1, α2 (M nhịp/(q·l1·l2)), β trái,phải,dưới,trên (|M gối|/(q·l1·l2))."""
    nx = int(round(n * l2 / l1))
    ny = n
    hx = l2 / nx
    hy = l1 / ny
    mx, my = nx - 1, ny - 1
    N = mx * my
    A = np.zeros((N, N))
    b = np.ones(N)

    def idx(i, j):
        return (i - 1) + (j - 1) * mx

    sg = {"k": -1.0, "n": 1.0}

    def add(row, i, j, c):
        # (i,j) có thể nằm ngoài biên 1 nút
        if i == 0 or i == nx or j == 0 or j == ny:
            return
        if i == -1:
            add(row, 1, j, c * sg[edges[0]])
            return
        if i == nx + 1:
            add(row, nx - 1, j, c * sg[edges[1]])
            return
        if j == -1:
            add(row, i, 1, c * sg[edges[2]])
            return
        if j == ny + 1:
            add(row, i, ny - 1, c * sg[edges[3]])
            return
        A[row, idx(i, j)] += c

    a1, a2 = 1 / hx ** 4, 1 / hy ** 4
    a3 = 2 / (hx ** 2 * hy ** 2)
    for j in range(1, ny):
        for i in range(1, nx):
            row = idx(i, j)
            add(row, i, j, 6 * a1 + 6 * a2 + 4 * a3)
            add(row, i - 1, j, -4 * a1 - 2 * a3)
            add(row, i + 1, j, -4 * a1 - 2 * a3)
            add(row, i, j - 1, -4 * a2 - 2 * a3)
            add(row, i, j + 1, -4 * a2 - 2 * a3)
            add(row, i - 2, j, a1)
            add(row, i + 2, j, a1)
            add(row, i, j - 2, a2)
            add(row, i, j + 2, a2)
            for (ii, jj) in ((i - 1, j - 1), (i + 1, j - 1), (i - 1, j + 1), (i + 1, j + 1)):
                add(row, ii, jj, a3)
    w = np.linalg.solve(A, b)
    W = np.zeros((nx + 1, ny + 1))
    for j in range(1, ny):
        for i in range(1, nx):
            W[i, j] = w[idx(i, j)]
    Mx_c = np.zeros_like(W)
    My_c = np.zeros_like(W)
    for j in range(1, ny):
        for i in range(1, nx):
            wxx = (W[i - 1, j] - 2 * W[i, j] + W[i + 1, j]) / hx ** 2
            wyy = (W[i, j - 1] - 2 * W[i, j] + W[i, j + 1]) / hy ** 2
            Mx_c[i, j] = -(wxx + nu * wyy)
            My_c[i, j] = -(wyy + nu * wxx)
    P = l1 * l2
    jm, im = ny // 2, nx // 2
    # mômen gối tại giữa cạnh ngàm: w_xx = 2·w1/h²
    bl = 2 * W[1, jm] / hx ** 2 if edges[0] == "n" else 0
    br = 2 * W[nx - 1, jm] / hx ** 2 if edges[1] == "n" else 0
    bb = 2 * W[im, 1] / hy ** 2 if edges[2] == "n" else 0
    bt = 2 * W[im, ny - 1] / hy ** 2 if edges[3] == "n" else 0
    return dict(a_y=My_c.max() / P, a_x=Mx_c.max() / P, b_left=bl / P, b_right=br / P, b_bot=bb / P, b_top=bt / P, w=W.max())


def _slab_As(M_kNm_per_m, h, a, Rb, Rs):
    h0 = _h0(h, a)
    am = M_kNm_per_m * 1e6 / (Rb * 1000 * h0 ** 2)
    if am >= 0.5:
        raise InputError("Sàn quá mỏng so với mômen: tăng chiều dày bản.")
    xi = 1 - math.sqrt(1 - 2 * am)
    As = xi * Rb * 1000 * h0 / Rs
    return As, am, xi


@problem("btct_slab", "btct", "Sàn bản kê bốn cạnh, bản dầm",
         "Hệ số mômen tính bằng giải số phương trình bản đàn hồi. Với bản dầm, tính theo dải 1 m. Tính cốt thép nhịp và gối, chọn Ø, bước.",
         [num("L1", "Cạnh ngắn L1", "m", 4, 0), num("L2", "Cạnh dài L2", "m", 5, 0), num("q", "Tải toàn phần tính toán q", "kN/m²", 10, 0),
          sel("e1", "Biên song song L2, phía 1 (gối L1)", [("n", "Ngàm"), ("k", "Khớp")]), sel("e2", "Biên song song L2, phía 2 (gối L1)", [("n", "Ngàm"), ("k", "Khớp")]),
          sel("e3", "Biên song song L1, phía 1 (gối L2)", [("n", "Ngàm"), ("k", "Khớp")]), sel("e4", "Biên song song L1, phía 2 (gối L2)", [("n", "Ngàm"), ("k", "Khớp")]),
          num("h", "Chiều dày bản h", "mm", 100, 50), num("a", "a", "mm", 20, 10), _conc, sel("steel", "Nhóm cốt thép", list(REBAR), "CB300-V"), _gb])
def slab(p):
    r = Report("Sàn bản kê bốn cạnh, bản dầm", MOD, [STD])
    L1, L2, q = p["L1"], p["L2"], p["q"]
    if L1 > L2:
        raise InputError("L1 phải là cạnh ngắn (L1 ≤ L2).")
    conc, s = CONCRETE[p["conc"]], REBAR[p["steel"]]
    gb = float(p["gb"])
    Rb = conc["Rb"] * gb
    Rs = s["Rs"]
    h, a = p["h"], p["a"]
    r.given([("Cạnh ngắn, dài", "L1; L2", f"{fmt(L1)}; {fmt(L2)}", "m"), ("Tải tính toán", "q", q, "kN/m²"), ("Chiều dày bản", "h", h, "mm"), ("Biên", "gối L1 / gối L2", f"{p['e1']}, {p['e2']} / {p['e3']}, {p['e4']}", "n: ngàm; k: khớp")])
    _mat_rows(r, p, conc, s, gb, Rb)
    rat = L2 / L1
    h0 = _h0(h, a)
    r.h("Bước 1. Phân loại ô bản")
    r.calc("L_2/L_1", None, f"{fmt(L2)}/{fmt(L1)}", rat)
    lim_h = L1 * 1000 / 45
    r.calc("h_0", "h − a", None, h0, "mm")
    if h < L1 * 1000 / 50:
        r.warn(f"Bản mỏng hơn L1/50 = {fmt(L1 * 1000 / 50)} mm: nên kiểm tra võng và tăng chiều dày.")
    res = {}
    items = []
    if rat >= 2:
        r.p("Vì $L_2/L_1$ ≥ 2: tính như bản dầm một phương, cắt dải rộng 1 m theo phương $L_1$.")
        nn = (p["e1"] == "n") + (p["e2"] == "n")
        if nn == 2:
            Ms, Mg = q * L1 ** 2 / 24, q * L1 ** 2 / 12
            r.calc("M_nh", "[[q·L_1^2 // 24]]", f"[[{fmt(q)}·{fmt(L1)}^2 // 24]]", Ms, "kN·m/m", key="Mn")
            r.calc("M_g", "[[q·L_1^2 // 12]]", f"[[{fmt(q)}·{fmt(L1)}^2 // 12]]", Mg, "kN·m/m", key="Mg")
        elif nn == 1:
            Ms, Mg = 9 * q * L1 ** 2 / 128, q * L1 ** 2 / 8
            r.calc("M_nh", "[[9q·L_1^2 // 128]]", None, Ms, "kN·m/m", key="Mn")
            r.calc("M_g", "[[q·L_1^2 // 8]]", None, Mg, "kN·m/m", key="Mg")
        else:
            Ms, Mg = q * L1 ** 2 / 8, 0
            r.calc("M_nh", "[[q·L_1^2 // 8]]", None, Ms, "kN·m/m", key="Mn")
        items = [("Nhịp phương L1", Ms), ("Gối phương L1", Mg)]
    else:
        r.p("Vì $L_2/L_1$ < 2: bản làm việc hai phương (bản kê bốn cạnh).")
        r.h("Bước 2. Hệ số mômen", 2)
        r.p("Giải số phương trình bản Kirchhoff bằng sai phân hữu hạn lưới mịn với hệ số Poisson ν = 0,2. Mômen quy về dạng $M = α·q·L_1·L_2$.")
        c = plate_coeffs(L1, L2, (p["e3"], p["e4"], p["e1"], p["e2"]), nu=0.2, n=40 if rat <= 1.5 else 30)
        P = q * L1 * L2
        r.calc("P", "q·L_1·L_2", f"{fmt(q)}·{fmt(L1)}·{fmt(L2)}", P, "kN")
        r.table(["Vị trí", "Hệ số α", "M (kN·m/m)"], [
            ["Nhịp, phương L1 (cạnh ngắn)", f"{c['a_y']:.4f}".replace(".", ","), fmt(c["a_y"] * P)],
            ["Nhịp, phương L2 (cạnh dài)", f"{c['a_x']:.4f}".replace(".", ","), fmt(c["a_x"] * P)],
            ["Gối L1 (biên 1)", f"{c['b_bot']:.4f}".replace(".", ","), fmt(c["b_bot"] * P)],
            ["Gối L1 (biên 2)", f"{c['b_top']:.4f}".replace(".", ","), fmt(c["b_top"] * P)],
            ["Gối L2 (biên 1)", f"{c['b_left']:.4f}".replace(".", ","), fmt(c["b_left"] * P)],
            ["Gối L2 (biên 2)", f"{c['b_right']:.4f}".replace(".", ","), fmt(c["b_right"] * P)],
        ])
        r.note("Hệ số trong các sổ tay Việt Nam có thể khác vài phần trăm do khác ν và sơ đồ quy đổi. Nếu đồ án yêu cầu tra bảng, hãy đối chiếu thêm.")
        r.results.update(aL1=c["a_y"], aL2=c["a_x"], bL1=max(c["b_bot"], c["b_top"]), bL2=max(c["b_left"], c["b_right"]))
        items = [("Nhịp phương L1", c["a_y"] * P), ("Nhịp phương L2", c["a_x"] * P), ("Gối L1", max(c["b_bot"], c["b_top"]) * P), ("Gối L2", max(c["b_left"], c["b_right"]) * P)]
    r.h("Bước 3. Tính cốt thép trên dải 1 m")
    r.f("α_m = [[M // γ_b1·R_b·1000·h_0^2]];  ξ = 1 − √[[1 − 2α_m]];  A_s = [[ξ·γ_b1·R_b·1000·h_0 // R_s]]")
    rows = []
    chosen = []
    Asmin = 0.001 * 1000 * h0
    xRs = xi_R(Rs, s["Es"])
    aRs = xRs * (1 - xRs / 2)
    am_max = 0.0
    for lb, Mv in items:
        if Mv <= 0:
            continue
        As, am, xi = _slab_As(Mv, h, a, Rb, Rs)
        am_max = max(am_max, am)
        Au = max(As, Asmin)
        d, sp, Ar = pick_slab_bar(Au)
        rows.append([lb, Mv, f"{am:.4f}".replace(".", ","), f"{xi:.4f}".replace(".", ","), As, Au, f"Ø{d}a{sp}", Ar])
        chosen.append((lb, d, sp, Ar))
        res[lb] = (As, d, sp)
    r.table(["Vị trí", "M (kN·m/m)", "αm", "ξ", "As tính (mm²/m)", "As dùng (mm²/m)", "Chọn", "As chọn (mm²/m)"], rows, f"As dùng ≥ μmin·1000·h₀ = {fmt(Asmin)} mm²/m (μmin = 0,1%). Bước thép không quá 200 mm.")
    r.check("Tiết diện bản không quá thép", f"α_m,max = {fmt(am_max)} {'≤' if am_max <= aRs else '>'} α_R = {fmt(aRs)}", am_max <= aRs)
    if am_max > aRs:
        r.warn("Bản quá mỏng: phải tăng chiều dày hoặc cấp bền bê tông. Thép chọn trong bảng không dùng được.")
    r.results["slab"] = {k: dict(As=v[0], d=v[1], s=v[2]) for k, v in res.items()}
    th = L2 * 1000 / 42
    D = Drawing(th, "san")
    X, Y = L2 * 1000, L1 * 1000
    D.rect(0, 0, X, Y, "BE_TONG")
    sym = {"n": "ngàm", "k": "khớp"}
    D.dim(0, -th * 1.5, X, -th * 1.5, 0, fmt(X))
    D.dim(0, 0, 0, Y, th * 1.5, fmt(Y))
    num_ = 1
    dic = {c[0]: c for c in chosen}
    if "Nhịp phương L1" in dic:
        c = dic["Nhịp phương L1"]
        D.line(X * .25, Y * .1, X * .25, Y * .9, "THEP")
        D.tag(X * .25, Y * .5, X * .28, Y * .62, f"Ø{c[1]}a{c[2]}", num_)
        num_ += 1
    if "Nhịp phương L2" in dic:
        c = dic["Nhịp phương L2"]
        D.line(X * .1, Y * .3, X * .9, Y * .3, "THEP")
        D.tag(X * .6, Y * .3, X * .63, Y * .42, f"Ø{c[1]}a{c[2]}", num_)
        num_ += 1
    if "Gối L1" in dic or "Gối phương L1" in dic:
        c = dic.get("Gối L1") or dic.get("Gối phương L1")
        D.line(X * .5, Y - L1 * 250, X * .5, Y + th * .1, "THEP", dash=True)
        D.tag(X * .5, Y - L1 * 125, X * .53, Y + th * 1.4, f"Ø{c[1]}a{c[2]}", num_)
        num_ += 1
    if "Gối L2" in dic:
        c = dic["Gối L2"]
        D.line(X - L1 * 250, Y * .7, X + th * .1, Y * .7, "THEP", dash=True)
        D.tag(X - L1 * 125, Y * .7, X - L1 * 400, Y * .84, f"Ø{c[1]}a{c[2]}", num_, side=1)
    D.title(X / 2, -th * 4, "S1 (SL=1)", "tl: 1/50")
    r.fig(D, "Mặt bằng ô sàn: nét liền thép lớp dưới, nét đứt thép mũ lớp trên")
    for lb, d, sp, Ar in chosen:
        r.conclude(f"{lb}: Ø{d}a{sp} (As = {fmt(Ar)} mm²/m).")
    return r.finish()


# ---------- VẾT NỨT, VÕNG ----------
@problem("btct_sls", "btct", "Dầm chữ nhật: vết nứt và độ võng",
         "Mô men gây nứt, ứng suất cốt thép, bề rộng vết nứt, độ cong và độ võng dài hạn theo trạng thái giới hạn II.",
         [num("Mser", "Mômen tiêu chuẩn toàn phần", "kN·m", 95, 0), num("ML", "Mômen tiêu chuẩn dài hạn", "kN·m", 70, 0), _conc, _steel, num("b", "b", "mm", 220, 100), num("h", "h", "mm", 500, 150),
          num("n", "Số thanh thép kéo", "", 3, 1), num("d", "Ø thép kéo", "mm", 20, 6), num("a", "a", "mm", 45, 15),
          num("L", "Nhịp L", "mm", 5500, 500), sel("S", "Sơ đồ võng", [(5 / 48, "Dầm đơn giản, tải đều (S = 5/48)"), (1 / 12, "Dầm đơn giản, lực giữa nhịp (S = 1/12)"), (1 / 3, "Công xôn, lực đầu mút (S = 1/3)"), (0.25, "Công xôn, tải đều (S = 1/4)")], 5 / 48),
          num("flim", "Độ võng cho phép L/", "", 250, 1), num("aL", "Giới hạn nứt dài hạn", "mm", 0.3, 0), num("aS", "Giới hạn nứt ngắn hạn", "mm", 0.4, 0)])
def sls(p):
    r = Report("Dầm chữ nhật: vết nứt và độ võng", MOD, [STD + " mục 8.2"])
    conc, s = CONCRETE[p["conc"]], REBAR[p["steel"]]
    b, h, a = p["b"], p["h"], p["a"]
    As = p["n"] * bar_area(p["d"])
    ds = p["d"]
    Es, Eb = s["Es"], conc["Eb"]
    Rbts, Rbs = conc["Rbts"], conc["Rbs"]
    h0 = _h0(h, a)
    M, ML = p["Mser"] * 1e6, p["ML"] * 1e6
    if ML > M:
        raise InputError("Mômen dài hạn không thể lớn hơn mômen toàn phần.")
    r.given([("Mômen tiêu chuẩn", "Mser; ML", f"{fmt(p['Mser'])}; {fmt(p['ML'])}", "kN·m"), ("Tiết diện", "b×h", f"{fmt(b)}×{fmt(h)}", "mm"), ("Thép kéo", "As", f"{int(p['n'])}Ø{int(ds)} = {fmt(As)}", "mm²"), ("Cường độ", "Rb,ser; Rbt,ser", f"{fmt(Rbs)}; {fmt(Rbts)}", "MPa")])
    r.h("Bước 1. Mômen gây nứt")
    alpha = Es / Eb
    Ared = b * h + alpha * As
    y_t = (b * h * h / 2 + alpha * As * a) / Ared  # từ thớ kéo
    Ired = b * h ** 3 / 12 + b * h * (h / 2 - y_t) ** 2 + alpha * As * (y_t - a) ** 2
    W = Ired / y_t
    Wpl = 1.3 * W
    Mcrc = Rbts * Wpl
    r.calc("α", "[[E_s // E_b]]", f"[[{Es} // {Eb}]]", alpha)
    r.calc("A_red", "b·h + α·A_s", None, Ared, "mm²")
    r.calc("y_t", None, None, y_t, "mm", note="Khoảng cách trọng tâm tiết diện quy đổi đến thớ chịu kéo")
    r.calc("I_red", None, None, Ired, "mm⁴")
    r.calc("W_red", "[[I_red // y_t]]", None, W, "mm³")
    r.calc("W_pl", "γ·W_red", f"1,3·{fmt(W)}", Wpl, "mm³", note="γ = 1,3 với tiết diện chữ nhật")
    r.calc("M_crc", "R_(bt,ser)·W_pl", f"{fmt(Rbts)}·{fmt(Wpl)}·10^(-6)", Mcrc / 1e6, "kN·m", key="Mcrc")
    cracked = M > Mcrc
    r.h("Bước 2. Tiết diện có vết nứt")
    r.p("Dầm " + ("có" if cracked else "không") + " xuất hiện vết nứt dưới tải tiêu chuẩn (M " + (">" if cracked else "≤") + " $M_crc$).")
    Ebred = Rbs / 0.0015
    a1 = Es / Ebred
    r.calc("E_(b,red)", "[[R_(b,ser) // ε_(b1,red)]]", f"[[{fmt(Rbs)} // 0,0015]]", Ebred, "MPa")
    r.calc("α_s1", "[[E_s // E_(b,red)]]", None, a1)
    mu = As / (b * h0)
    r.calc("μ_s", "[[A_s // b·h_0]]", None, mu, d=5)
    xm = h0 * (-mu * a1 + math.sqrt((mu * a1) ** 2 + 2 * mu * a1))
    r.calc("x_m", "h_0(√[[(μ_s·α_s1)^2 + 2μ_s·α_s1]] − μ_s·α_s1)", None, xm, "mm", note="Chiều cao vùng nén của tiết diện nứt", key="xm")
    z = h0 - xm / 3
    Icrc = b * xm ** 3 / 3 + a1 * As * (h0 - xm) ** 2
    r.calc("I_crc", "[[b·x_m^3 // 3]] + α_s1·A_s(h_0 − x_m)^2", None, Icrc, "mm⁴")
    r.h("Bước 3. Bề rộng vết nứt")
    if not cracked:
        acL = acS = 0.0
        r.p("Không có vết nứt nên $a_crc$ = 0.")
        r.results.update(acL=0.0, acS=0.0)
        ss_all = 0.0
    else:
        def sigma(Mm):
            return Mm * (h0 - xm) * a1 / Icrc
        sL = sigma(ML)
        sA = sigma(M)
        scrc = sigma(Mcrc)
        r.calc("σ_s", "[[M(h_0 − x_m) // I_crc]]·α_s1", f"[[{fmt(p['Mser'])}·10^6({fmt(h0)} − {fmt(xm)}) // {fmt(Icrc)}]]·{fmt(a1)}", sA, "MPa", key="ss")
        r.calc("σ_sL", None, None, sL, "MPa", note="Do mômen dài hạn")
        r.calc("σ_(s,crc)", None, None, scrc, "MPa", note="Ứng với mômen gây nứt")
        if sA > s["Rs"] * 1.0:
            r.warn("σs vượt Rs,ser xấp xỉ: nên kiểm tra lại bố trí thép.")
        Abt_h = min(max(y_t, 2 * a), 0.5 * h)
        Abt = b * Abt_h
        ls0 = 0.5 * Abt / As * ds
        ls = min(max(ls0, 10 * ds, 100), 40 * ds, 400)
        r.calc("y_t,tính", "max(2a; y_t) ≤ 0,5h", None, Abt_h, "mm", note="Chiều cao vùng bê tông chịu kéo để tính ls")
        r.calc("A_bt", "b·y_t,tính", None, Abt, "mm²")
        r.calc("l_s", "0,5·[[A_bt // A_s]]·d_s", f"0,5·[[{fmt(Abt)} // {fmt(As)}]]·{fmt(ds)}", ls0, "mm")
        r.calc("l_s", None, None, ls, "mm", note="Giới hạn: 10ds ≤ ls ≤ 40ds, 100 ≤ ls ≤ 400 mm", key="ls")
        def crack(phi1, sg):
            psi = max(1 - 0.8 * scrc / sg, 0.2) if sg > 0 else 0
            return phi1 * 0.5 * 1.0 * psi * sg / Es * ls, psi
        a1L, ps1 = crack(1.4, sL)
        a2A, ps2 = crack(1.0, sA)
        a3L, ps3 = crack(1.0, sL)
        r.f("a_crc = φ_1·φ_2·φ_3·ψ_s·[[σ_s // E_s]]·l_s;  φ_2 = 0,5 (thép có gờ);  φ_3 = 1 (cấu kiện chịu uốn);  ψ_s = 1 − 0,8·[[σ_(s,crc) // σ_s]]")
        r.calc("a_(crc,1)", None, f"1,4·0,5·1·{fmt(ps1)}·[[{fmt(sL)} // {Es}]]·{fmt(ls)}", a1L, "mm", note="Tải dài hạn, φ1 = 1,4")
        r.calc("a_(crc,2)", None, f"1·0,5·1·{fmt(ps2)}·[[{fmt(sA)} // {Es}]]·{fmt(ls)}", a2A, "mm", note="Toàn bộ tải, ngắn hạn, φ1 = 1")
        r.calc("a_(crc,3)", None, f"1·0,5·1·{fmt(ps3)}·[[{fmt(sL)} // {Es}]]·{fmt(ls)}", a3L, "mm", note="Tải dài hạn, ngắn hạn, φ1 = 1")
        acL = a1L
        acS = a1L + a2A - a3L
        r.calc("a_(crc,dài hạn)", "a_(crc,1)", None, acL, "mm", key="acL")
        r.calc("a_(crc,ngắn hạn)", "a_(crc,1) + a_(crc,2) − a_(crc,3)", None, acS, "mm", key="acS")
    r.check("Vết nứt dài hạn", f"a_crc = {fmt(acL)} {'≤' if acL <= p['aL'] else '>'} {fmt(p['aL'])} mm", acL <= p["aL"])
    r.check("Vết nứt ngắn hạn", f"a_crc = {fmt(acS)} {'≤' if acS <= p['aS'] else '>'} {fmt(p['aS'])} mm", acS <= p["aS"])
    r.h("Bước 4. Độ cong và độ võng")
    S = float(p["S"])
    r.f("[[1 // r]] = [[1 // r_1]] − [[1 // r_2]] + [[1 // r_3]]")
    r.p("$r_1$: toàn bộ tải, tác dụng ngắn hạn. $r_2$: tải dài hạn, tác dụng ngắn hạn. $r_3$: tải dài hạn, tác dụng dài hạn.")

    def curvature(Mm, eps, longterm):
        """Độ cong (1/mm) và các đại lượng trung gian."""
        if Mm <= 0:
            return 0.0, "-", 0.0, 0.0
        if Mm > Mcrc:
            Ebr = Rbs / eps
            psi_ = max(1 - 0.8 * Mcrc / Mm, 0.2)
            a_ = Es / psi_ / Ebr
            m_ = mu * a_
            xx = h0 * (-m_ + math.sqrt(m_ ** 2 + 2 * m_))
            II = b * xx ** 3 / 3 + a_ * As * (h0 - xx) ** 2
            return Mm / (Ebr * II), "có nứt", Ebr, II
        Eb_ = Eb / (1 + conc["cr"]) if longterm else 0.85 * Eb
        a_ = Es / Eb_
        Ar = b * h + a_ * As
        yy = (b * h * h / 2 + a_ * As * a) / Ar
        II = b * h ** 3 / 12 + b * h * (h / 2 - yy) ** 2 + a_ * As * (yy - a) ** 2
        return Mm / (Eb_ * II), "không nứt", Eb_, II

    c_1 = curvature(M, 0.0015, False)
    c_2 = curvature(ML, 0.0015, False)
    c_3 = curvature(ML, 0.0028, True)
    r.table(["Độ cong", "M (kN·m)", "Trạng thái", "E bê tông (MPa)", "I (mm⁴)", "1/r (1/mm)"], [
        ["1/r₁", fmt(M / 1e6), c_1[1], fmt(c_1[2]), fmt(c_1[3]), f"{c_1[0]:.3e}"],
        ["1/r₂", fmt(ML / 1e6), c_2[1], fmt(c_2[2]), fmt(c_2[3]), f"{c_2[0]:.3e}"],
        ["1/r₃", fmt(ML / 1e6), c_3[1], fmt(c_3[2]), fmt(c_3[3]), f"{c_3[0]:.3e}"]],
        "Tiết diện có nứt: E = Rb,ser/εb1,red (0,0015 ngắn hạn; 0,0028 dài hạn), thép quy đổi Es/ψs với ψs = 1 − 0,8Mcrc/M. Tiết diện không nứt: 0,85Eb hoặc Eb/(1+φb,cr).")
    c1 = c_1[0] - c_2[0] + c_3[0]
    f = S * p["L"] ** 2 * c1
    r.calc("f", "S·L^2·[[1 // r]]", f"{fmt(S)}·{fmt(p['L'])}^2·{c1:.3e}".replace(".", ","), f, "mm", key="f")
    flim = p["L"] / p["flim"]
    r.check("Độ võng", f"f = {fmt(f)} {'≤' if f <= flim else '>'} L/{fmt(p['flim'])} = {fmt(flim)} mm", f <= flim)
    r.note("Độ võng tính tại tiết diện giữa nhịp với toàn bộ tải. Với tiêu chuẩn đang áp dụng cho công trình, giới hạn võng cần lấy theo Bảng M.1 TCVN 5574:2018 hoặc TCVN 2737:2023, hãy nhập đúng mẫu số L/.")
    r.conclude(f"Mcrc = {fmt(Mcrc / 1e6)} kN·m. Vết nứt dài hạn {fmt(acL)} mm; ngắn hạn {fmt(acS)} mm. Độ võng f = {fmt(f)} mm.")
    r.conclude("Dầm ĐẠT trạng thái giới hạn II." if r.ok else "Dầm KHÔNG ĐẠT ít nhất một điều kiện trạng thái giới hạn II.")
    return r.finish()


# ---------- CHỌC THỦNG ----------
@problem("btct_punching", "btct", "Chọc thủng bản do cột, không có cốt ngang",
         "Tính chu vi tính toán, kiểm tra chọc thủng do lực tập trung kèm mômen theo TCVN 5574:2018.",
         [num("F", "Lực chọc thủng F", "kN", 400, 0), num("Mx", "Mômen Mx (0 nếu không có)", "kN·m", 30, 0), num("My", "Mômen My (0 nếu không có)", "kN·m", 0, 0),
          _conc, _gb, num("cx", "Cạnh cột cx", "mm", 300, 50), num("cy", "Cạnh cột cy", "mm", 400, 50), num("h", "Chiều dày bản", "mm", 320, 80), num("a", "a", "mm", 30, 10)])
def punching(p):
    r = Report("Chọc thủng bản do cột", MOD, [STD + " mục 8.1.6"])
    conc = CONCRETE[p["conc"]]
    gb = float(p["gb"])
    Rbt = conc["Rbt"] * gb
    h0 = _h0(p["h"], p["a"])
    cx, cy = p["cx"], p["cy"]
    F = p["F"] * 1e3
    r.given([("Lực", "F", p["F"], "kN"), ("Mô men", "Mx; My", f"{fmt(p['Mx'])}; {fmt(p['My'])}", "kN·m"), ("Cột", "cx×cy", f"{fmt(cx)}×{fmt(cy)}", "mm"), ("Bản", "h", p["h"], "mm"), ("Cường độ kéo", "γb1·Rbt", Rbt, "MPa")])
    r.note("Dạng bài này áp dụng cho cột giữa có đường bao kín. Cột biên, cột góc có chu vi hở, chưa được xử lý.")
    r.h("Bước 1. Chu vi tính toán")
    r.p("Đường bao lấy cách chu vi vùng đặt tải một đoạn 0,5$h_0$.")
    r.calc("h_0", "h − a", None, h0, "mm")
    Lx, Ly = cx + h0, cy + h0
    u = 2 * (Lx + Ly)
    r.calc("L_x", "c_x + h_0", None, Lx, "mm")
    r.calc("L_y", "c_y + h_0", None, Ly, "mm")
    r.calc("u", "2(L_x + L_y)", None, u, "mm")
    r.h("Bước 2. Khả năng chịu lực")
    Ab = u * h0
    Fb = Rbt * Ab
    r.calc("A_b", "u·h_0", f"{fmt(u)}·{fmt(h0)}", Ab, "mm²")
    r.calc("F_(b,ult)", "γ_b1·R_bt·A_b", f"{fmt(Rbt)}·{fmt(Ab)}·10^(-3)", Fb / 1e3, "kN", key="Fb")
    Wx = Lx * (Lx / 3 + Ly)
    Wy = Ly * (Ly / 3 + Lx)
    Mbx = Rbt * Wx * h0
    Mby = Rbt * Wy * h0
    r.f("W_bx = L_x([[L_x // 3]] + L_y);  M_(bx,ult) = γ_b1·R_bt·W_bx·h_0")
    r.calc("M_(bx,ult)", None, None, Mbx / 1e6, "kN·m")
    r.calc("M_(by,ult)", None, None, Mby / 1e6, "kN·m")
    r.h("Bước 3. Kiểm tra")
    tF = F / Fb
    tM = abs(p["Mx"]) * 1e6 / Mbx + abs(p["My"]) * 1e6 / Mby
    tM2 = min(tM, 0.5 * tF)
    if tM > 0.5 * tF:
        r.p("Số hạng mômen vượt 0,5 lần số hạng lực, lấy bằng 0,5 lần số hạng lực theo mục 8.1.6.")
    r.calc("Tỷ số", "[[F // F_(b,ult)]] + [[M_x // M_(bx,ult)]] + [[M_y // M_(by,ult)]]", f"{fmt(tF)} + {fmt(tM2)}", tF + tM2, key="ratio")
    ok = tF + tM2 <= 1
    r.check("Chọc thủng", f"tỷ số = {fmt(tF + tM2)} {'≤' if ok else '>'} 1", ok)
    th = max(Lx, Ly) / 12
    D = Drawing(th, "choc_thung")
    D.rect(-cx / 2, -cy / 2, cx, cy, "BE_TONG")
    D.rect(-Lx / 2, -Ly / 2, Lx, Ly, "KY_HIEU", dash=True)
    D.dim(-cx / 2, -Ly / 2 - th, cx / 2, -Ly / 2 - th, 0, fmt(cx))
    D.dim(-Lx / 2, -Ly / 2 - th * 3.4, Lx / 2, -Ly / 2 - th * 3.4, 0, f"Lx={fmt(Lx)}")
    D.dim(-Lx / 2 - th, -Ly / 2, -Lx / 2 - th, Ly / 2, 0, f"Ly={fmt(Ly)}")
    r.fig(D, "Chu vi tính toán chọc thủng (nét đứt)", width_cm=9)
    r.conclude(f"Tỷ số kiểm tra = {fmt(tF + tM2)}: " + ("ĐẠT." if ok else "KHÔNG ĐẠT, cần tăng chiều dày bản, mũ cột hoặc đặt cốt chống chọc thủng."))
    return r.finish()

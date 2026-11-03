"""Nền móng: TCVN 9362:2012, TCVN 10304:2014, TCVN 5574:2018."""
from __future__ import annotations
import math
import numpy as np
from .registry import problem, num, sel, tbl, InputError
from ..core.report import Report, fmt
from ..core.drawing import Drawing
from ..core.materials import CONCRETE, REBAR, bar_area, xi_R
from .rc_common import pick_slab_bar, round10

MOD = "Nền móng"
_conc = sel("conc", "Cấp bền bê tông", list(CONCRETE), "B20")
_steel = sel("steel", "Nhóm cốt thép", list(REBAR), "CB300-V")
_gb = sel("gb", "Hệ số γb1 của bê tông", [(0.9, "0,9: có tải dài hạn"), (1.0, "1,0: chỉ tải ngắn hạn")], 0.9)
PILE_STD_NOTE = "TCVN 10304:2025 đã được công bố. Chương trình đang theo TCVN 10304:2014, hãy đối chiếu hệ số với bản hiện hành khi lập hồ sơ thật."


def abd(phi_deg):
    f = math.radians(phi_deg)
    if phi_deg <= 0:
        return 0.0, 1.0, math.pi
    if phi_deg >= 89:
        raise InputError("Góc ma sát trong không hợp lệ.")
    k = 1 / math.tan(f) + f - math.pi / 2
    return 0.25 * math.pi / k, 1 + math.pi / k, math.pi / math.tan(f) / k


def calc_R(m1, m2, ktc, b, h, gII, gIIp, c, phi):
    A, B, D = abd(phi)
    return m1 * m2 / ktc * (A * b * gII + B * h * gIIp + D * c), A, B, D


def _report_R(r, m1, m2, ktc, b, hm, g, gp, c, phi):
    Rv, A, B, D = calc_R(m1, m2, ktc, b, hm, g, gp, c, phi)
    r.f("A = [[0,25π // cotφ + φ − π/2]];  B = 1 + [[π // cotφ + φ − π/2]];  D = [[π·cotφ // cotφ + φ − π/2]]")
    r.calc("A", None, None, A, key="A")
    r.calc("B", None, None, B, key="B")
    r.calc("D", None, None, D, key="D")
    r.calc("R", "[[m_1·m_2 // k_tc]](A·b·γ_II + B·h·γ'_II + D·c_II)", f"[[{fmt(m1)}·{fmt(m2)} // {fmt(ktc)}]]({fmt(A)}·{fmt(b)}·{fmt(g)} + {fmt(B)}·{fmt(hm)}·{fmt(gp)} + {fmt(D)}·{fmt(c)})", Rv, "kPa", key="R")
    return Rv


_soil = [num("gII", "γII đất dưới đáy móng", "kN/m³", 18, 0), num("gp", "γ'II đất từ đáy móng trở lên", "kN/m³", 17, 0), num("c", "Lực dính cII", "kPa", 15, 0), num("phi", "Góc ma sát trong φII", "độ", 18, 0, 45),
         num("m1", "Hệ số m1", "", 1.1, 0), num("m2", "Hệ số m2", "", 1.0, 0), num("ktc", "Hệ số ktc", "", 1.0, 0)]


@problem("nm_R", "nm", "Sức chịu tải tính toán của nền R",
         "Tính R theo TCVN 9362:2012 với hệ số A, B, D tính trực tiếp từ φ.",
         [num("b", "Bề rộng móng b", "m", 1.5, 0), num("h", "Độ sâu đặt móng h", "m", 1.2, 0)] + _soil + [num("p", "Áp lực đáy móng tiêu chuẩn ptb (0 nếu không kiểm tra)", "kPa", 150, 0)])
def bearing(p):
    r = Report("Sức chịu tải tính toán của nền", MOD, ["TCVN 9362:2012"])
    r.given([("Bề rộng móng", "b", p["b"], "m"), ("Độ sâu đặt móng", "h", p["h"], "m"), ("Dung trọng dưới đáy", "γII", p["gII"], "kN/m³"), ("Dung trọng trên đáy", "γ'II", p["gp"], "kN/m³"), ("Lực dính", "cII", p["c"], "kPa"), ("Góc ma sát", "φII", p["phi"], "độ")])
    r.h("Lời giải")
    Rv = _report_R(r, p["m1"], p["m2"], p["ktc"], p["b"], p["h"], p["gII"], p["gp"], p["c"], p["phi"])
    r.note("Tiêu chuẩn còn trừ thêm số hạng γII·h₀ khi có tầng hầm; dạng bài này áp dụng cho nhà không có tầng hầm. Hệ số m1, m2, ktc lấy theo Bảng 15 và mục 4.6.11 TCVN 9362:2012.")
    if p["p"]:
        r.check("Áp lực trung bình", f"p_tb = {fmt(p['p'])} {'≤' if p['p'] <= Rv else '>'} R = {fmt(Rv)} kPa", p["p"] <= Rv)
    r.conclude(f"R = {fmt(Rv)} kPa.")
    return r.finish()


def flex_As(Mknm, width, h0, Rb, Rs):
    """As theo αm cho tiết diện chữ nhật cốt đơn. Trả về (As, αm)."""
    am = Mknm * 1e6 / (Rb * width * h0 ** 2)
    if am >= 0.5:
        return float("inf"), am
    return (1 - math.sqrt(1 - 2 * am)) * Rb * width * h0 / Rs, am


def report_flex(r, name, Mv, width, h0, Rb, Rs, aR):
    As, am = flex_As(Mv, width, h0, Rb, Rs)
    r.calc(f"α_(m,{name})", "[[M // γ_b1·R_b·b·h_0^2]]", f"[[{fmt(Mv)}·10^6 // {fmt(Rb)}·{fmt(width)}·{fmt(h0)}^2]]", am)
    r.check(f"Không quá thép, {name}", f"α_m = {fmt(am)} {'≤' if am <= aR else '>'} α_R = {fmt(aR)}", am <= aR)
    if As != float("inf"):
        r.calc(f"A_(s,{name})", "[[(1 − √[[1 − 2α_m]])γ_b1·R_b·b·h_0 // R_s]]", None, As, "mm²", key=f"As{name}")
    return As, am


def shear_scan(Lc_mm, h0, width, Rbt, q_at):
    """Tiết diện nghiêng không cốt đai: q_at(C) là lực cắt (kN) cách mép cột một đoạn C (mm).

    Trả về (tỷ số lớn nhất, C, Q, Qb)."""
    worst = (0.0, h0, 0.0, 0.0)
    if Lc_mm <= h0:
        return worst
    for C in np.linspace(h0, min(3 * h0, Lc_mm), 40):
        Qb = min(max(1.5 * Rbt * width * h0 ** 2 / C, 0.5 * Rbt * width * h0), 2.5 * Rbt * width * h0) / 1e3
        Q = q_at(C)
        if Q / Qb > worst[0]:
            worst = (Q / Qb, C, Q, Qb)
    return worst


def _punch_check(r, N, Mx, My, cx, cy, h0, Rbt, _unused=None):
    """Chọc thủng theo mục 8.1.6 TCVN 5574:2018, N: lực chọc thủng (kN), kích thước mm."""
    Lx, Ly = cx + h0, cy + h0
    u = 2 * (Lx + Ly)
    Fb = Rbt * u * h0 / 1e3
    r.calc("u", "2(c_x + c_y + 2h_0)", f"2({fmt(cx)} + {fmt(cy)} + 2·{fmt(h0)})", u, "mm")
    r.calc("F_(b,ult)", "γ_b1·R_bt·u·h_0", f"{fmt(Rbt)}·{fmt(u)}·{fmt(h0)}·10^(-3)", Fb, "kN")
    Wx = Lx * (Lx / 3 + Ly)
    Wy = Ly * (Ly / 3 + Lx)
    Mbx = Rbt * Wx * h0 / 1e6
    Mby = Rbt * Wy * h0 / 1e6
    tF = N / Fb
    tM = abs(Mx) / Mbx + abs(My) / Mby
    tM2 = min(tM, 0.5 * tF)
    r.calc("Tỷ số", "[[F // F_(b,ult)]] + [[M_x // M_(bx,ult)]] + [[M_y // M_(by,ult)]]", f"[[{fmt(N)} // {fmt(Fb)}]] + {fmt(tM2)}", tF + tM2, key="punch")
    return tF + tM2


# ---------- MÓNG ĐƠN ----------
@problem("nm_pad", "nm", "Móng đơn BTCT lệch tâm",
         "Chọn kích thước đáy móng theo R, kiểm tra áp lực, chọc thủng, cắt, tính thép hai phương, vẽ mặt bằng và mặt cắt móng.",
         [num("N", "Lực dọc tính toán N", "kN", 800, 0), num("M", "Mômen tính toán M (theo cạnh l)", "kN·m", 80), num("H", "Lực ngang tính toán H (theo cạnh l)", "kN", 20), num("nf", "Hệ số vượt tải trung bình n", "", 1.15, 1),
          num("cl", "Cạnh cột theo l", "mm", 400, 100), num("cb", "Cạnh cột theo b", "mm", 300, 100), num("hm", "Độ sâu đặt móng", "m", 1.5, 0), num("ratio", "Tỷ lệ l/b", "", 1.2, 1, 2.5), num("gtb", "γtb đất và móng", "kN/m³", 20, 0)] + _soil + [_conc, _steel, _gb, num("hmin", "Chiều cao móng nhỏ nhất thử", "mm", 400, 200), num("a", "Lớp bảo vệ tính a", "mm", 50, 30)])
def pad(p):
    r = Report("Móng đơn BTCT lệch tâm", MOD, ["TCVN 9362:2012", "TCVN 5574:2018"])
    N, M, H, nf = p["N"], p["M"], p["H"], p["nf"]
    cl, cb, hm, g = p["cl"], p["cb"], p["hm"], p["gtb"]
    conc, s = CONCRETE[p["conc"]], REBAR[p["steel"]]
    gb = float(p["gb"])
    Rbt, Rb = conc["Rbt"] * gb, conc["Rb"] * gb
    Rs = s["Rs"]
    xR = xi_R(Rs, s["Es"])
    aR = xR * (1 - xR / 2)
    a_ = p["a"]
    if p["hmin"] <= a_:
        raise InputError("Chiều cao móng phải lớn hơn lớp bảo vệ a.")
    if N <= 0:
        raise InputError("Lực dọc N phải dương.")
    Nt, Mt, Ht = N / nf, M / nf, H / nf
    rat = p["ratio"]

    def sizing(hf):
        b = 0.8
        rows = []
        while b < 8:
            l = math.ceil(b * rat * 10 - 1e-9) / 10
            Rv, *_ = calc_R(p["m1"], p["m2"], p["ktc"], b, hm, p["gII"], p["gp"], p["c"], p["phi"])
            Mtot = abs(Mt + Ht * hf)
            ptb = Nt / (b * l) + g * hm
            pmax = ptb + 6 * Mtot / (b * l ** 2)
            pmin = ptb - 6 * Mtot / (b * l ** 2)
            ok = ptb <= Rv and pmax <= 1.2 * Rv and pmin >= 0
            rows.append([fmt(b), fmt(l), fmt(Rv), fmt(ptb), fmt(pmax), fmt(pmin), "đạt" if ok else "chưa"])
            if ok:
                return b, l, Rv, ptb, pmax, pmin, Mtot, rows
            b = round(b + 0.1, 3)
        raise InputError("Không tìm được kích thước móng dưới 8 m: nền quá yếu hoặc tải quá lớn. Xem xét móng cọc.")

    def forces(b, l, hf):
        Ma = abs(M + H * hf)
        p0 = N / (b * l)
        return Ma, p0, p0 + 6 * Ma / (b * l ** 2), p0 - 6 * Ma / (b * l ** 2)

    def net(x_from_center, l, p1, p2):
        return p2 + (p1 - p2) * (x_from_center + l / 2) / l

    def punch(b, l, hmm):
        h0 = hmm - a_
        Ma, p0, p1, p2 = forces(b, l, hmm / 1000)
        Lp, Bp = cl + 2 * h0, cb + 2 * h0
        Li, Bi = min(Lp / 1000, l), min(Bp / 1000, b)  # phần giao thật của tháp với đáy móng
        F = p0 * (b * l - Li * Bi)
        frac = (Bi / b) * (Li / l) ** 3  # phần mômen do áp lực nằm trong tháp
        Mr = Ma * (1 - frac)
        Lx, Ly = cl + h0, cb + h0
        Fb = Rbt * 2 * (Lx + Ly) * h0 / 1e3
        Mbx = Rbt * Lx * (Lx / 3 + Ly) * h0 / 1e6
        tF = F / Fb
        return tF + min(Mr / Mbx, 0.5 * tF), F, Mr, Li, Bi

    def shear_l(b, l, hmm):
        h0 = hmm - a_
        Ma, p0, p1, p2 = forces(b, l, hmm / 1000)
        Lc = (l - cl / 1000) / 2

        def Q(C):
            x1 = cl / 2000 + C / 1000
            return b * (l / 2 - x1) * (p1 + net(x1, l, p1, p2)) / 2
        return shear_scan(Lc * 1000, h0, b * 1000, Rbt, Q)

    def shear_b(b, l, hmm):
        h0 = hmm - a_
        Ma, p0, p1, p2 = forces(b, l, hmm / 1000)
        Bc = (b - cb / 1000) / 2
        return shear_scan(Bc * 1000, h0, l * 1000, Rbt, lambda C: l * (Bc - C / 1000) * p0)

    def steel_moments(b, l, hmm):
        Ma, p0, p1, p2 = forces(b, l, hmm / 1000)
        Lc = (l - cl / 1000) / 2
        pI = net(cl / 2000, l, p1, p2)
        Bc = (b - cb / 1000) / 2
        return b * Lc ** 2 * (2 * p1 + pI) / 6, l * Bc ** 2 * p0 / 2, Lc, pI, Bc

    hmm = p["hmin"]
    for _ in range(8):  # chiều cao móng ảnh hưởng mômen đáy móng, lặp đến ổn định
        b, l, Rv, ptb, pmax, pmin, Mtot, trial_rows = sizing(hmm / 1000)
        h_new = p["hmin"]
        while h_new < 2500:
            MI, MII, *_ = steel_moments(b, l, h_new)
            ok = punch(b, l, h_new)[0] <= 1 and shear_l(b, l, h_new)[0] <= 1 and shear_b(b, l, h_new)[0] <= 1 and flex_As(MI, b * 1000, h_new - a_, Rb, Rs)[1] <= aR and flex_As(MII, l * 1000, h_new - a_ - 12, Rb, Rs)[1] <= aR
            if ok:
                break
            h_new += 50
        else:
            raise InputError("Móng cần cao hơn 2,5 m: xem lại tải, kích thước hoặc tăng cấp bền bê tông.")
        if h_new == hmm:
            break
        hmm = h_new
    hf = hmm / 1000
    h0 = hmm - a_
    r.given([("Tải tính toán", "N; M; H", f"{fmt(N)}; {fmt(M)}; {fmt(H)}", "kN; kN·m; kN"), ("Hệ số vượt tải", "n", nf, ""), ("Cột", "cl×cb", f"{fmt(cl)}×{fmt(cb)}", "mm"), ("Độ sâu đặt móng", "hm", hm, "m")])
    r.h("Bước 1. Tải tiêu chuẩn")
    r.calc("N^tc", "[[N // n]]", f"[[{fmt(N)} // {fmt(nf)}]]", Nt, "kN")
    r.calc("M^tc", "[[M // n]]", None, Mt, "kN·m")
    r.calc("H^tc", "[[H // n]]", None, Ht, "kN")
    r.h("Bước 2. Chọn kích thước đáy móng")
    r.p(f"Tăng dần b, l theo bước 100 mm (l ≈ {fmt(rat)}b) đến khi thoả ba điều kiện áp lực. Chiều cao móng $h_f$ = {fmt(hmm)} mm lấy theo Bước 4.")
    r.table(["b (m)", "l (m)", "R (kPa)", "ptb", "pmax", "pmin", "Kết quả"], trial_rows[-6:], "Các lần thử cuối, áp lực đơn vị kPa.")
    r.h("Với kích thước đã chọn", 3)
    Rv = _report_R(r, p["m1"], p["m2"], p["ktc"], b, hm, p["gII"], p["gp"], p["c"], p["phi"])
    r.results.update(b=b, l=l)
    r.calc("M^tc_đáy", "|M^tc + H^tc·h_f|", f"|{fmt(Mt)} + {fmt(Ht)}·{fmt(hf)}|", Mtot, "kN·m")
    r.calc("p_tb", "[[N^tc // b·l]] + γ_tb·h", f"[[{fmt(Nt)} // {fmt(b)}·{fmt(l)}]] + {fmt(g)}·{fmt(hm)}", ptb, "kPa", key="ptb")
    r.calc("p_max", "p_tb + [[6M^tc // b·l^2]]", f"{fmt(ptb)} + [[6·{fmt(Mtot)} // {fmt(b)}·{fmt(l)}^2]]", pmax, "kPa", key="pmax")
    r.calc("p_min", "p_tb − [[6M^tc // b·l^2]]", None, pmin, "kPa", key="pmin")
    r.check("Áp lực trung bình", f"p_tb = {fmt(ptb)} ≤ R = {fmt(Rv)} kPa", ptb <= Rv)
    r.check("Áp lực lớn nhất", f"p_max = {fmt(pmax)} ≤ 1,2R = {fmt(1.2 * Rv)} kPa", pmax <= 1.2 * Rv)
    r.check("Không tách đáy móng", f"p_min = {fmt(pmin)} ≥ 0 kPa", pmin >= 0)
    r.h("Bước 3. Áp lực tính toán ròng lên đáy móng")
    r.p("Trọng lượng móng và đất đắp truyền thẳng xuống nền, không gây uốn và chọc thủng, nên áp lực ròng chỉ do N, M, H.")
    Ma, p0, p1, p2 = forces(b, l, hf)
    r.calc("M_đáy", "|M + H·h_f|", None, Ma, "kN·m")
    r.calc("p_0", "[[N // b·l]]", None, p0, "kPa")
    r.calc("p_(0,max)", "p_0 + [[6M_đáy // b·l^2]]", None, p1, "kPa", key="p01")
    r.calc("p_(0,min)", "p_0 − [[6M_đáy // b·l^2]]", None, p2, "kPa", key="p02")
    r.h("Bước 4. Chiều cao móng: chọc thủng và cắt")
    r.p(f"Tăng chiều cao từ {fmt(p['hmin'])} mm đến khi đạt các điều kiện chọc thủng, cắt và không quá thép, chọn $h_f$ = {fmt(hmm)} mm.")
    r.calc("h_0", "h_f − a", None, h0, "mm", key="h0")
    r.results["hf"] = hmm
    rt, F_p, Mr, Li, Bi = punch(b, l, hmm)
    if F_p <= 1e-9:
        r.p("Tháp chọc thủng 45° phủ kín đáy móng cả hai phương, không xảy ra chọc thủng.")
    else:
        r.calc("F", "p_0(b·l − A_tháp)", f"{fmt(p0)}({fmt(b)}·{fmt(l)} − {fmt(Li)}·{fmt(Bi)})", F_p, "kN", note="Lấy phần giao thật của tháp 45° với đáy móng")
        r.calc("M_còn", "M_đáy(1 − [[B_t // b]]·([[L_t // l]])^3)", None, Mr, "kN·m", note="Mômen của phần áp lực nằm ngoài tháp")
        ratio = _punch_check(r, F_p, Mr, 0, cl, cb, h0, Rbt)
        r.check("Chọc thủng", f"tỷ số = {fmt(ratio)} ≤ 1", ratio <= 1)
    r.p("Kiểm tra cắt không có cốt đai, tiết diện nghiêng xuất phát từ mép cột, $h_0$ ≤ C ≤ min(3$h_0$; $L_c$):")
    r.f("Q_b = [[1,5·γ_b1·R_bt·b·h_0^2 // C]],  0,5·γ_b1·R_bt·b·h_0 ≤ Q_b ≤ 2,5·γ_b1·R_bt·b·h_0")
    for nm, sc in (("theo cạnh l", shear_l(b, l, hmm)), ("theo cạnh b", shear_b(b, l, hmm))):
        if sc[3] > 0:
            r.check(f"Cắt {nm}", f"Q(C={fmt(sc[1])}) = {fmt(sc[2])} ≤ Q_b = {fmt(sc[3])} kN", sc[0] <= 1)
        else:
            r.p(f"Cắt {nm}: phần công xôn ngắn hơn $h_0$, không cần kiểm tra.")
    r.h("Bước 5. Tính cốt thép")
    MI, MII, Lc, pI, Bc = steel_moments(b, l, hmm)
    r.calc("L_c", "[[l − c_l // 2]]", None, Lc, "m")
    r.calc("p_I", "p_(0,min) + (p_(0,max) − p_(0,min))·[[l − L_c // l]]", None, pI, "kPa")
    r.calc("M_I", "b·L_c^2·[[2p_(0,max) + p_I // 6]]", f"{fmt(b)}·{fmt(Lc)}^2·[[2·{fmt(p1)} + {fmt(pI)} // 6]]", MI, "kN·m", key="MI")
    AsI0, _ = report_flex(r, "I", MI, b * 1000, h0, Rb, Rs, aR)
    r.calc("B_c", "[[b − c_b // 2]]", None, Bc, "m")
    r.calc("M_II", "l·B_c^2·[[p_0 // 2]]", f"{fmt(l)}·{fmt(Bc)}^2·[[{fmt(p0)} // 2]]", MII, "kN·m", key="MII")
    h02 = h0 - 12
    AsII0, _ = report_flex(r, "II", MII, l * 1000, h02, Rb, Rs, aR)
    AsI = max(AsI0, 0.001 * b * 1000 * h0)
    AsII = max(AsII0, 0.001 * l * 1000 * h02)
    d1, s1, _ = pick_slab_bar(AsI / b, ds=(12, 14, 16, 18, 20, 22, 25), smax=200)
    d2, s2, _ = pick_slab_bar(AsII / l, ds=(12, 14, 16, 18, 20, 22, 25), smax=200)
    r.table(["Phương", "As dùng (mm²)", "Trên 1 m (mm²/m)", "Chọn"], [["Theo l (thép ① đặt dưới)", AsI, AsI / b, f"Ø{d1}a{s1}"], ["Theo b (thép ② đặt trên)", AsII, AsII / l, f"Ø{d2}a{s2}"]], "As dùng ≥ μmin = 0,1%.")
    bm, lm = b * 1000, l * 1000
    th = max(lm, bm) / 22
    D = Drawing(th, "mong_don")
    D.rect(-lm / 2, -bm / 2, lm, bm, "BE_TONG")
    D.rect(-cl / 2, -cb / 2, cl, cb, "BE_TONG")
    D.line(-cl / 2, -cb / 2, cl / 2, cb / 2, "BE_TONG")
    D.line(-cl / 2, cb / 2, cl / 2, -cb / 2, "BE_TONG")
    D.line(-lm / 2 + 50, bm * .28, lm / 2 - 50, bm * .28, "THEP")
    D.line(lm * .28, -bm / 2 + 50, lm * .28, bm / 2 - 50, "THEP")
    D.tag(-lm * .1, bm * .28, -lm * .05, bm * .4, f"Ø{d1}a{s1}", 1)
    D.tag(lm * .28, -bm * .2, lm * .22, -bm * .36, f"Ø{d2}a{s2}", 2, side=-1)
    D.dim(-lm / 2, -bm / 2 - th, lm / 2, -bm / 2 - th, 0, fmt(lm))
    D.dim(-lm / 2, -bm / 2, -lm / 2, bm / 2, th, fmt(bm))
    D.title(0, -bm / 2 - th * 3.6, "M1 (SL=1)", "tl: 1/50")
    y0 = -bm / 2 - th * 16
    D.rect(-lm / 2, y0, lm, hmm, "BE_TONG")
    D.rect(-cl / 2, y0 + hmm, cl, hmm, "BE_TONG")
    D.line(-lm / 2 - th, y0 - 100, lm / 2 + th, y0 - 100, "BE_TONG")
    D.line(-lm / 2 + 50, y0 + a_, lm / 2 - 50, y0 + a_, "THEP")
    D.tag(lm * .05, y0 + a_, lm * .12, y0 - th * 1.6, f"Ø{d1}a{s1}", 1)
    D.dim(lm / 2, y0, lm / 2, y0 + hmm, -th * 1.2, fmt(hmm))
    D.dim(-lm / 2, y0 - th * 3.2, lm / 2, y0 - th * 3.2, 0, fmt(lm))
    D.title(0, y0 - th * 5.4, "1-1", "tl: 1/25")
    r.fig(D, "Mặt bằng và mặt cắt móng")
    r.conclude(f"Đáy móng b×l = {fmt(b)}×{fmt(l)} m, chiều cao hf = {fmt(hmm)} mm.")
    r.conclude(f"ptb = {fmt(ptb)} kPa; pmax = {fmt(pmax)} kPa; R = {fmt(Rv)} kPa.")
    r.conclude(f"Thép: Ø{d1}a{s1} theo cạnh l; Ø{d2}a{s2} theo cạnh b.")
    return r.finish()


# ---------- MÓNG BĂNG ----------
@problem("nm_strip", "nm", "Móng băng dưới tường",
         "Bề rộng móng từ R, áp lực đáy, kiểm tra lực cắt, thép ngang trên 1 m dài.",
         [num("N", "Lực tính toán N/1 m dài", "kN/m", 180, 0), num("M", "Mômen tính toán/1 m", "kN·m/m", 10), num("nf", "Hệ số vượt tải n", "", 1.15, 1), num("tw", "Bề dày tường", "mm", 220, 50), num("hm", "Độ sâu đặt móng", "m", 1.0, 0), num("gtb", "γtb", "kN/m³", 20, 0)] + _soil + [_conc, _steel, _gb, num("hf", "Chiều cao móng", "mm", 300, 150), num("a", "a", "mm", 50, 30)])
def strip(p):
    r = Report("Móng băng dưới tường", MOD, ["TCVN 9362:2012", "TCVN 5574:2018"])
    N, M, nf, tw, hm, g = p["N"], abs(p["M"]), p["nf"], p["tw"], p["hm"], p["gtb"]
    conc, s = CONCRETE[p["conc"]], REBAR[p["steel"]]
    gb = float(p["gb"])
    Rbt, Rb = conc["Rbt"] * gb, conc["Rb"] * gb
    xR = xi_R(s["Rs"], s["Es"])
    aR = xR * (1 - xR / 2)
    r.given([("Lực tính toán", "N; M", f"{fmt(N)}; {fmt(M)}", "kN/m; kN·m/m"), ("Tường", "t", tw, "mm"), ("Độ sâu", "hm", hm, "m")])
    Nt, Mt = N / nf, M / nf
    r.h("Bước 1. Chọn bề rộng móng")
    b = 0.5
    while b < 6:
        Rv, *_ = calc_R(p["m1"], p["m2"], p["ktc"], b, hm, p["gII"], p["gp"], p["c"], p["phi"])
        ptb = Nt / b + g * hm
        pmax = ptb + 6 * Mt / b ** 2
        pmin = ptb - 6 * Mt / b ** 2
        if ptb <= Rv and pmax <= 1.2 * Rv and pmin >= 0:
            break
        b = round(b + 0.05, 3)
    else:
        raise InputError("Bề rộng móng vượt 6 m: xem xét giải pháp khác.")
    r.calc("b", None, None, b, "m", key="b", note="Bề rộng nhỏ nhất thoả mãn cả ba điều kiện áp lực")
    Rv = _report_R(r, p["m1"], p["m2"], p["ktc"], b, hm, p["gII"], p["gp"], p["c"], p["phi"])
    r.calc("p_tb", "[[N^tc // b]] + γ_tb·h", f"[[{fmt(Nt)} // {fmt(b)}]] + {fmt(g)}·{fmt(hm)}", ptb, "kPa", key="ptb")
    r.calc("p_max", "p_tb + [[6M^tc // b^2]]", None, pmax, "kPa", key="pmax")
    r.calc("p_min", "p_tb − [[6M^tc // b^2]]", None, pmin, "kPa", key="pmin")
    r.check("Áp lực trung bình", f"p_tb = {fmt(ptb)} ≤ R = {fmt(Rv)} kPa", ptb <= Rv)
    r.check("Áp lực lớn nhất", f"p_max = {fmt(pmax)} ≤ 1,2R = {fmt(1.2 * Rv)} kPa", pmax <= 1.2 * Rv)
    r.check("Không tách đáy móng", f"p_min = {fmt(pmin)} ≥ 0 kPa", pmin >= 0)
    r.h("Bước 2. Áp lực ròng và nội lực bản móng")
    p1 = N / b + 6 * M / b ** 2
    p2 = N / b - 6 * M / b ** 2
    Lc = (b - tw / 1000) / 2
    pI = p2 + (p1 - p2) * (b - Lc) / b
    r.calc("p_(0,max)", "[[N // b]] + [[6M // b^2]]", None, p1, "kPa")
    r.calc("L_c", "[[b − t // 2]]", None, Lc, "m")
    r.calc("p_I", None, None, pI, "kPa")
    MI = Lc ** 2 * (2 * p1 + pI) / 6
    QI = Lc * (p1 + pI) / 2
    r.calc("M", "L_c^2·[[2p_(0,max) + p_I // 6]]", f"{fmt(Lc)}^2·[[2·{fmt(p1)} + {fmt(pI)} // 6]]", MI, "kN·m/m", key="MI")
    r.calc("Q", "L_c·[[p_(0,max) + p_I // 2]]", None, QI, "kN/m", key="QI")
    h0 = p["hf"] - p["a"]
    if h0 <= 0:
        raise InputError("Chiều cao móng phải lớn hơn a.")
    r.h("Bước 3. Kiểm tra cắt (không cốt ngang)")
    Lc_mm = Lc * 1000
    sc = shear_scan(Lc_mm, h0, 1000, Rbt, lambda C: (Lc - C / 1000) * (p1 + (p2 + (p1 - p2) * (b - Lc + C / 1000) / b)) / 2)
    r.f("Q_b = [[1,5·γ_b1·R_bt·b·h_0^2 // C]],  0,5·γ_b1·R_bt·b·h_0 ≤ Q_b ≤ 2,5·γ_b1·R_bt·b·h_0")
    if sc[3] > 0:
        r.check("Lực cắt", f"Q(C={fmt(sc[1])}) = {fmt(sc[2])} {'≤' if sc[0] <= 1 else '>'} Q_b = {fmt(sc[3])} kN/m", sc[0] <= 1)
    else:
        r.p("Phần công xôn ngắn hơn $h_0$: không cần kiểm tra cắt.")
    r.h("Bước 4. Cốt thép")
    As0, _ = report_flex(r, "m", MI, 1000, h0, Rb, s["Rs"], aR)
    r.results["As"] = As0
    if As0 == float("inf"):
        raise InputError("Móng quá thấp so với mômen: tăng chiều cao móng.")
    As = max(As0, 0.001 * 1000 * h0)
    d, sp, Ar = pick_slab_bar(As, ds=(10, 12, 14, 16, 18, 20))
    r.p(f"Chọn Ø{d}a{sp} ($A_s$ = {fmt(Ar)} mm²/m), thép dọc cấu tạo Ø10a200.")
    bm = b * 1000
    th = bm / 16
    D = Drawing(th, "mong_bang")
    hf = p["hf"]
    D.rect(-bm / 2, 0, bm, hf, "BE_TONG")
    D.rect(-tw / 2, hf, tw, hf * 1.6, "BE_TONG")
    D.line(-bm / 2 + 40, p["a"], bm / 2 - 40, p["a"], "THEP")
    D.tag(0, p["a"], bm * .1, -th * 1.6, f"Ø{d}a{sp}", 1)
    D.dim(-bm / 2, -th * 3.2, bm / 2, -th * 3.2, 0, fmt(bm))
    D.dim(bm / 2, 0, bm / 2, hf, -th * 1.3, fmt(hf))
    D.title(0, -th * 5.4, "M1 (móng băng)", "tl: 1/25")
    r.fig(D, "Mặt cắt móng băng", width_cm=10)
    r.conclude(f"Bề rộng móng b = {fmt(b)} m; chiều cao {fmt(hf)} mm; thép Ø{d}a{sp}.")
    return r.finish()


# ---------- LÚN ----------
def k0_center(l, b, z):
    """Hệ số ứng suất dưới tâm móng chữ nhật (nghiệm Boussinesq lấy tích phân)."""
    if z <= 0:
        return 1.0
    a_, b_ = l / 2, b / 2
    R3 = math.sqrt(a_ ** 2 + b_ ** 2 + z ** 2)
    R1 = math.sqrt(a_ ** 2 + z ** 2)
    R2 = math.sqrt(b_ ** 2 + z ** 2)
    return 4 / (2 * math.pi) * (math.atan2(a_ * b_, z * R3) + a_ * b_ * z / R3 * (1 / R1 ** 2 + 1 / R2 ** 2))


@problem("nm_settle", "nm", "Độ lún móng: phân tầng cộng lún",
         "Ứng suất bản thân, ứng suất gây lún dưới tâm móng, chiều sâu tính lún, tổng độ lún.",
         [num("p", "Áp lực trung bình đáy móng ptb", "kPa", 180, 0), num("b", "Bề rộng móng b", "m", 2, 0), num("l", "Chiều dài móng l", "m", 2.4, 0), num("hm", "Độ sâu đặt móng", "m", 1.5, 0), num("gm", "γ đất trên đáy móng", "kN/m³", 17, 0),
          tbl("lay", "Các lớp đất dưới đáy móng", [("t", "Dày (m)"), ("g", "γ (kN/m³)"), ("E", "E (kPa)")], [dict(t=3, g=18, E=8000), dict(t=4, g=18.5, E=12000), dict(t=6, g=19, E=20000)]),
          num("beta", "Hệ số β", "", 0.8, 0, 1), num("dh", "Bề dày lớp phân tố", "m", 0.4, 0.1), num("lim", "Độ lún cho phép", "cm", 8, 0), num("ratio", "Giới hạn dừng σgl/σbt", "", 0.2, 0.05, 0.5)])
def settle(p):
    r = Report("Độ lún móng: phân tầng cộng lún", MOD, ["TCVN 9362:2012"])
    b, l, pm, hm = p["b"], p["l"], p["p"], p["hm"]
    if l < b:
        raise InputError("Cần l ≥ b.")
    r.given([("Áp lực trung bình", "ptb", pm, "kPa"), ("Kích thước", "b×l", f"{fmt(b)}×{fmt(l)}", "m"), ("Độ sâu đặt móng", "hm", hm, "m")])
    r.table(["Lớp", "Bề dày (m)", "γ (kN/m³)", "E (kPa)"], [[i + 1, q["t"], q["g"], q["E"]] for i, q in enumerate(p["lay"])])
    r.h("Bước 1. Ứng suất gây lún tại đáy móng")
    s0 = p["gm"] * hm
    pgl = pm - s0
    if pgl <= 0:
        raise InputError("Áp lực gây lún ≤ 0: không có lún do tải công trình.")
    r.calc("σ_(bt,0)", "γ·h_m", f"{fmt(p['gm'])}·{fmt(hm)}", s0, "kPa")
    r.calc("p_gl", "p_tb − σ_(bt,0)", f"{fmt(pm)} − {fmt(s0)}", pgl, "kPa", key="pgl")
    r.h("Bước 2. Phân tố và ứng suất")
    r.f("σ_(gl,z) = k_0·p_gl,  k_0 = [[2 // π]]([[a·c·z // R_3]]([[1 // R_1^2]] + [[1 // R_2^2]]) + atan[[a·c // z·R_3]]),  a = l/2,  c = b/2,  R_1 = √[[a^2+z^2]], R_2 = √[[c^2+z^2]], R_3 = √[[a^2+c^2+z^2]]")
    # chia
    dh = p["dh"]
    z = 0.0
    sbt = s0
    rows = []
    S = 0.0
    done = False
    stop_z = None
    prev = (0.0, s0, pgl)
    for i, q in enumerate(p["lay"]):
        if q["t"] <= 0 or q["E"] <= 0:
            raise InputError("Bề dày và E của lớp đất phải dương.")
        nn = max(int(round(q["t"] / dh)), 1)
        hi = q["t"] / nn
        for k in range(nn):
            z2 = z + hi
            sbt2 = sbt + q["g"] * hi
            k0 = k0_center(l, b, z2)
            sg2 = k0 * pgl
            mean = (prev[2] + sg2) / 2
            dS = p["beta"] * mean * hi / q["E"]
            S += dS
            rows.append([i + 1, fmt(z2), fmt(sbt2), f"{k0:.4f}".replace(".", ","), fmt(sg2), fmt(mean), fmt(dS * 100, 3)])
            prev = (z2, sbt2, sg2)
            z = z2
            sbt = sbt2
            lim_ratio = min(p["ratio"], 0.1) if q["E"] < 5000 else p["ratio"]  # đất yếu: tính sâu hơn
            if sg2 <= lim_ratio * sbt2:
                done = True
                stop_z = z2
                break
        if done:
            break
    r.table(["Lớp", "z (m)", "σbt (kPa)", "k₀", "σgl (kPa)", "σgl TB (kPa)", "Si (cm)"], rows, "z tính từ đáy móng; Si = β·σgl,TB·hi/E.")
    if not done:
        r.warn("Hết các lớp đất đã nhập nhưng chưa đạt điều kiện dừng. Độ lún thực sẽ lớn hơn; hãy bổ sung lớp đất sâu hơn.")
        r.check("Đủ chiều sâu tính lún", f"chưa đạt σ_gl ≤ {fmt(p['ratio'])}σ_bt ở đáy lớp cuối", False)
    else:
        r.p(f"Dừng tại z = {fmt(stop_z)} m (σgl ≤ {fmt(p['ratio'])}σbt).")
    r.h("Bước 3. Tổng độ lún")
    S_cm = S * 100
    r.calc("S", "Σβ·[[σ_(gl,TB)·h_i // E_i]]", None, S_cm, "cm", key="S", d=2)
    r.check("Điều kiện lún", f"S = {fmt(S_cm, 2)} {'≤' if S_cm <= p['lim'] else '>'} S_gh = {fmt(p['lim'])} cm", S_cm <= p["lim"])
    th = b * 1000 / 12
    D = Drawing(th, "lun")
    D.rect(-b * 500, 0, b * 1000, 400, "BE_TONG")
    k = 1000  # 1 m = 1000 đơn vị vẽ
    zz = [0.0] + [float(x[1].replace(",", ".")) for x in rows]
    sg = [pgl] + [float(x[4].replace(",", ".").replace("−", "-")) for x in rows]
    sb = [s0] + [float(x[2].replace(",", ".").replace("−", "-")) for x in rows]
    m = max(max(sg), max(sb))
    W = 1.4 * b * 1000
    D.pline([(s * W / m, -z_ * k) for s, z_ in zip(sg, zz)], "BIEU_DO")
    D.pline([(-s * W / m, -z_ * k) for s, z_ in zip(sb, zz)], "KY_HIEU")
    D.line(0, 0, 0, -zz[-1] * k - th, "TRUC", True)
    D.text(W * .5, th * 1.0, "σgl (kPa)", th * .9, "BIEU_DO")
    D.text(-W * .5, th * 1.0, "σbt (kPa)", th * .9, "KY_HIEU")
    D.text(sg[0] * W / m + th * .4, -th * .5, fmt(sg[0]), th * .8, "BIEU_DO", ha="left")
    D.text(sg[-1] * W / m + th * .4, -zz[-1] * k, fmt(sg[-1]), th * .8, "BIEU_DO", ha="left")
    D.text(-sb[-1] * W / m - th * .4, -zz[-1] * k, fmt(sb[-1]), th * .8, "KY_HIEU", ha="right")
    d0 = 0
    for i, q in enumerate(p["lay"]):
        d0 += q["t"]
        if d0 <= zz[-1] + 1e-9:
            D.line(-W, -d0 * k, W, -d0 * k, "PHU", True)
            D.text(W + th * .3, -d0 * k + th * .5, f"Hết lớp {i + 1}", th * .7, "CHU", ha="left")
    r.fig(D, "Biểu đồ ứng suất bản thân (trái) và ứng suất gây lún (phải)", width_cm=12)
    r.conclude(f"pgl = {fmt(pgl)} kPa; độ lún S = {fmt(S_cm, 2)} cm: " + ("ĐẠT." if r.ok else "KHÔNG ĐẠT, xem mục kiểm tra."))
    return r.finish()


# ---------- CỌC ----------
@problem("nm_pile", "nm", "Sức chịu tải cọc đơn",
         "Theo vật liệu; theo đất nền bằng chỉ tiêu cơ lý nhập fi, qb hoặc theo SPT công thức Nhật Bản. Xác định sức chịu tải thiết kế.",
         [sel("shape", "Tiết diện cọc", [("sq", "Vuông cạnh d"), ("ci", "Tròn đường kính d")]), num("d", "Cạnh hoặc đường kính d", "mm", 300, 100),
          sel("ptype", "Loại cọc", [("driven", "Đóng, ép"), ("bored", "Khoan nhồi")]),
          sel("method", "Phương pháp theo đất nền", [("phys", "Chỉ tiêu cơ lý: nhập fi, qb tra bảng"), ("spt", "SPT: công thức Nhật Bản")]),
          tbl("lay", "Các lớp đất cọc đi qua", [("t", "Dày (m)"), ("kind", "Đất", ["sét", "cát"]), ("fi", "fi (kPa)"), ("Ns", "Ns SPT"), ("cu", "cu (kPa)"), ("ap", "αp·fL")],
              [dict(t=4, kind="sét", fi=25, Ns=6, cu=38, ap=0.8), dict(t=5, kind="cát", fi=45, Ns=15, cu=0, ap=0), dict(t=4, kind="cát", fi=60, Ns=25, cu=0, ap=0)]),
          num("qb", "qb tại mũi (chỉ tiêu cơ lý)", "kPa", 4000, 0), sel("tip", "Đất tại mũi cọc (SPT)", ["cát", "sét"]), num("Np", "Np SPT quanh mũi", "", 30, 0), num("cup", "cu tại mũi (SPT, đất sét)", "kPa", 0, 0),
          num("gc", "γc", "", 1.0, 0), num("gcq", "γcq", "", 1.0, 0), num("gcf", "γcf", "", 1.0, 0),
          num("g0", "γ0", "", 1.15, 0), num("gn", "γn", "", 1.15, 0), num("gk", "γk", "", 1.75, 0),
          _conc, sel("steel", "Thép cọc", list(REBAR), "CB400-V"), num("Ast", "Tổng thép dọc cọc", "mm²", 1018, 0), num("phi", "Hệ số uốn dọc φ", "", 0.9, 0, 1), num("gcb", "Hệ số thi công cọc khoan nhồi γcb·γ'cb", "", 0.595, 0, 1, help="Chỉ dùng cho cọc khoan nhồi: γcb = 0,85 nhân γ'cb 0,7÷1,0 theo điều kiện đổ bê tông"), _gb])
def pile(p):
    r = Report("Sức chịu tải cọc đơn", MOD, ["TCVN 10304:2014", "TCVN 5574:2018"])
    d = p["d"] / 1000
    if p["shape"] == "sq":
        Ab, u = d ** 2, 4 * d
    else:
        Ab, u = math.pi * d ** 2 / 4, math.pi * d
    conc, s = CONCRETE[p["conc"]], REBAR[p["steel"]]
    r.given([("Kích thước", "d", p["d"], "mm"), ("Loại cọc", "", "Đóng, ép" if p["ptype"] == "driven" else "Khoan nhồi", ""), ("Thép dọc", "Ast", p["Ast"], "mm²")])
    r.table(["Lớp", "Dày (m)", "Đất", "fi (kPa)", "Ns", "cu (kPa)", "αp·fL"], [[i + 1, q["t"], q["kind"], q["fi"], q["Ns"], q["cu"], q["ap"]] for i, q in enumerate(p["lay"])])
    r.h("Bước 1. Đặc trưng cọc")
    r.calc("A_b", None, None, Ab, "m²", d=4)
    r.calc("u", None, None, u, "m")
    r.warn(PILE_STD_NOTE)
    r.h("Bước 2. Theo vật liệu")
    Rbp = conc["Rb"] * float(p["gb"]) * (p["gcb"] if p["ptype"] == "bored" else 1.0)
    r.calc("R_b,tính", "k·R_b", None, Rbp, "MPa", note="k gồm γb1 và, với cọc khoan nhồi, γcb·γ'cb")
    Rvl = p["phi"] * (Rbp * Ab * 1e6 + s["Rsc"] * p["Ast"]) / 1e3
    r.calc("R_vl", "φ(R_b,tính·A_b + R_sc·A_st)", f"{fmt(p['phi'])}({fmt(Rbp)}·{fmt(Ab * 1e6)} + {s['Rsc']}·{fmt(p['Ast'])})·10^(-3)", Rvl, "kN", key="Rvl")
    r.h("Bước 3. Theo đất nền")
    rows = []
    if p["method"] == "phys":
        r.f("R_(c,u) = γ_c(γ_cq·q_b·A_b + u·Σγ_cf·f_i·l_i)")
        for i, q in enumerate(p["lay"]):
            rows.append([i + 1, q["t"], q["fi"], p["gcf"] * q["fi"] * q["t"]])
        side = sum(x[3] for x in rows)
        r.table(["Lớp", "lᵢ (m)", "fᵢ (kPa)", "γcf·fᵢ·lᵢ (kN/m)"], rows)
        tip = p["gcq"] * p["qb"] * Ab
        Rs_ = u * side
        r.calc("Q_b", "γ_cq·q_b·A_b", f"{fmt(p['gcq'])}·{fmt(p['qb'])}·{fmt(Ab)}", tip, "kN", key="Qb")
        r.calc("Q_s", "u·Σγ_cf·f_i·l_i", f"{fmt(u)}·{fmt(side)}", Rs_, "kN", key="Qs")
        Rcu = p["gc"] * (tip + Rs_)
        r.calc("R_(c,u)", "γ_c(Q_b + Q_s)", None, Rcu, "kN", key="Rcu")
        r.note("fi, qb tra Bảng 2, Bảng 3 TCVN 10304:2014 theo loại đất, độ sệt và chiều sâu. Bạn nhập trực tiếp giá trị đã tra.")
    else:
        r.f("R_(c,u) = q_b·A_b + u·(Σf_(c,i)·l_(c,i) + Σf_(s,i)·l_(s,i))  (Phụ lục G, công thức của Viện Kiến trúc Nhật Bản)")
        driven = p["ptype"] == "driven"
        Npv = min(p["Np"], 50.0)
        if p["Np"] > 50:
            r.p(f"Np nhập {fmt(p['Np'])} lớn hơn 50, lấy Np = 50.")
        if p["tip"] == "cát":
            qb = (300 if driven else 150) * Npv
            r.calc("q_b", f"{300 if driven else 150}·N_p", f"{300 if driven else 150}·{fmt(Npv)}", qb, "kPa", key="qb")
        else:
            qb = (9 if driven else 6) * p["cup"]
            r.calc("q_b", f"{9 if driven else 6}·c_u", f"{9 if driven else 6}·{fmt(p['cup'])}", qb, "kPa", key="qb")
        side = 0.0
        for i, q in enumerate(p["lay"]):
            if q["kind"] == "cát":
                nsv = min(q["Ns"], 50.0)
                f = 10 * nsv / (3 if driven else 5)
                note = f"10·{fmt(nsv)}/{3 if driven else 5}"
            else:
                f = q["ap"] * q["cu"]
                note = f"{fmt(q['ap'])}·{fmt(q['cu'])}"
            f = min(f, 200)
            side += f * q["t"]
            rows.append([i + 1, q["kind"], q["t"], note, f, f * q["t"]])
        r.table(["Lớp", "Đất", "lᵢ (m)", "Cách tính f", "f (kPa)", "f·l (kN/m)"], rows, "Giới hạn f ≤ 200 kPa, Ns ≤ 50. Cọc khoan nhồi lấy 10Ns/5 thiên về an toàn; một số tài liệu ghi 10Ns/3, hãy đối chiếu Phụ lục G.")
        tip = qb * Ab
        Rs_ = u * side
        r.calc("Q_b", "q_b·A_b", f"{fmt(qb)}·{fmt(Ab)}", tip, "kN", key="Qb")
        r.calc("Q_s", "u·Σf·l", f"{fmt(u)}·{fmt(side)}", Rs_, "kN", key="Qs")
        Rcu = tip + Rs_
        r.calc("R_(c,u)", "Q_b + Q_s", None, Rcu, "kN", key="Rcu")
        r.note("Hệ số αp, fL tra Phụ lục G TCVN 10304:2014 theo cu/σ'v và độ mảnh cọc; bạn nhập tích αp·fL cho mỗi lớp sét.")
    r.h("Bước 4. Sức chịu tải thiết kế")
    Rcd = p["g0"] * Rcu / (p["gn"] * p["gk"])
    r.calc("R_(c,d)", "[[γ_0·R_(c,k) // γ_n·γ_k]]", f"[[{fmt(p['g0'])}·{fmt(Rcu)} // {fmt(p['gn'])}·{fmt(p['gk'])}]]", Rcd, "kN", key="Rcd", note="Lấy Rc,k bằng Rc,u cho một giá trị tính")
    use = min(Rcd, Rvl)
    r.calc("[P]", "min(R_(c,d); R_vl)", None, use, "kN", key="Pa")
    if Rvl < Rcd:
        r.p("Vật liệu quyết định: có thể giảm chiều dài cọc hoặc tăng tiết diện.")
    r.note("γ0 = 1,15 cho móng nhiều cọc, γn theo cấp công trình, γk theo số liệu đất nền: nhập đúng theo mục 7.1.11 TCVN 10304:2014.")
    th = 220
    D = Drawing(th, "coc")
    y = 0
    k = 1000
    D.line(-3000, 0, 3000, 0, "BE_TONG")
    for i, q in enumerate(p["lay"]):
        y2 = y - q["t"] * k
        D.line(-3000, y2, 3000, y2, "PHU", True)
        D.text(1800, (y + y2) / 2, f"Lớp {i + 1}: {q['kind']}, {fmt(q['t'])} m", th, "CHU")
        y = y2
    W = max(p["d"], 300)
    D.rect(-W / 2 * 2, y, W * 2, -y, "BE_TONG")
    D.dim(-1400, 0, -1400, y, 0, f"{fmt(-y / 1000)} m")
    r.fig(D, "Sơ đồ cọc trong các lớp đất", width_cm=9)
    r.conclude(f"Rvl = {fmt(Rvl)} kN; Rc,u = {fmt(Rcu)} kN; Rc,d = {fmt(Rcd)} kN. Chọn sức chịu tải thiết kế [P] = {fmt(use)} kN.")
    return r.finish()


@problem("nm_pilecap", "nm", "Móng cọc: phân lực cọc, đài cọc",
         "Bố trí cọc lưới, tính phản lực từng cọc, kiểm tra với [P], chọc thủng đài, tính thép đài.",
         [num("N", "Lực dọc tính toán N", "kN", 2000, 0), num("Mx", "Mômen Mx (quanh trục x)", "kN·m", 120), num("My", "Mômen My (quanh trục y)", "kN·m", 80), num("nf", "Hệ số vượt tải n", "", 1.15, 1), num("Pa", "Sức chịu tải [P]", "kN", 500, 0),
          num("d", "Cạnh cọc d", "mm", 300, 100), num("nx", "Số cọc phương x", "", 3, 1, 8), num("ny", "Số cọc phương y", "", 2, 1, 8), num("s", "Khoảng cách cọc s", "mm", 1000, 300), num("eg", "Mép đài đến tim cọc biên", "mm", 300, 100),
          num("cx", "Cột cx", "mm", 500, 100), num("cy", "Cột cy", "mm", 400, 100), num("hd", "Chiều cao đài", "mm", 700, 300), num("a", "a", "mm", 100, 50), num("gtb", "γtb đài và đất", "kN/m³", 20, 0), num("hm", "Độ sâu đáy đài", "m", 1.5, 0), _conc, _steel, _gb])
def pilecap(p):
    r = Report("Móng cọc: phân lực cọc, đài cọc", MOD, ["TCVN 10304:2014", "TCVN 5574:2018"])
    nx, ny = int(p["nx"]), int(p["ny"])
    s_, eg = p["s"], p["eg"]
    if nx * ny < 2:
        raise InputError("Cần ít nhất 2 cọc.")
    if s_ < 3 * p["d"]:
        r.warn(f"Khoảng cách cọc {fmt(s_)} mm nhỏ hơn 3d = {fmt(3 * p['d'])} mm.")
    conc, st = CONCRETE[p["conc"]], REBAR[p["steel"]]
    gb = float(p["gb"])
    Rbt, Rb = conc["Rbt"] * gb, conc["Rb"] * gb
    xR = xi_R(st["Rs"], st["Es"])
    aR = xR * (1 - xR / 2)
    r.warn(PILE_STD_NOTE)
    X = (nx - 1) * s_ + 2 * eg
    Y = (ny - 1) * s_ + 2 * eg
    r.given([("Tải", "N; Mx; My", f"{fmt(p['N'])}; {fmt(p['Mx'])}; {fmt(p['My'])}", "kN; kN·m"), ("Cọc", "nx×ny", f"{nx}×{ny}", "cọc"), ("Khoảng cách cọc", "s", s_, "mm"), ("Đài", "X×Y×h", f"{fmt(X)}×{fmt(Y)}×{fmt(p['hd'])}", "mm"), ("Sức chịu tải", "[P]", p["Pa"], "kN")])
    r.h("Bước 1. Phản lực cọc")
    G = p["nf"] * p["gtb"] * X * Y / 1e6 * p["hm"]
    Nt = p["N"] + G
    r.calc("G", "n·γ_tb·X·Y·h_m", f"{fmt(p['nf'])}·{fmt(p['gtb'])}·{fmt(X / 1000)}·{fmt(Y / 1000)}·{fmt(p['hm'])}", G, "kN")
    r.calc("N_tt", "N + G", None, Nt, "kN")
    r.f("P_i = [[N_tt // n]] + [[M_x·y_i // Σy_i^2]] + [[M_y·x_i // Σx_i^2]]")
    piles = []
    for j in range(ny):
        for i in range(nx):
            piles.append(((i - (nx - 1) / 2) * s_ / 1000, (j - (ny - 1) / 2) * s_ / 1000))
    n = len(piles)
    Sx2 = sum(x ** 2 for x, y in piles)
    Sy2 = sum(y ** 2 for x, y in piles)
    r.calc("Σx_i^2", None, None, Sx2, "m²")
    r.calc("Σy_i^2", None, None, Sy2, "m²")
    rows = []
    Ps = []
    for k, (x, y) in enumerate(piles):
        P = Nt / n + (p["Mx"] * y / Sy2 if Sy2 else 0) + (p["My"] * x / Sx2 if Sx2 else 0)
        Ps.append(P)
        rows.append([k + 1, x, y, P])
    r.table(["Cọc", "x (m)", "y (m)", "Pᵢ (kN)"], rows)
    Pm, Pn = max(Ps), min(Ps)
    r.results.update(Pmax=Pm, Pmin=Pn)
    r.check("Cọc chịu nén lớn nhất", f"P_max = {fmt(Pm)} {'≤' if Pm <= p['Pa'] else '>'} [P] = {fmt(p['Pa'])} kN", Pm <= p["Pa"])
    r.check("Không có cọc nhổ", f"P_min = {fmt(Pn)} {'≥' if Pn >= 0 else '<'} 0 kN", Pn >= 0)
    r.h("Bước 2. Chọc thủng đài do cột")
    h0 = p["hd"] - p["a"]
    if h0 <= 0:
        raise InputError("Chiều cao đài phải lớn hơn a.")
    r.calc("h_0", "h_đ − a", None, h0, "mm")
    cx, cy = p["cx"], p["cy"]
    # phản lực ròng (không kể G) của cọc nằm ngoài tháp 45° từ mép cột
    Ppure = [P - G / n for P in Ps]
    Fp = 0.0
    for (x, y), P in zip(piles, Ppure):
        if (abs(x) * 1000 > cx / 2 + h0 or abs(y) * 1000 > cy / 2 + h0) and P > 0:
            Fp += P
    r.calc("F", "ΣP_i ngoài tháp 45°", None, Fp, "kN", key="Fp", note="Chỉ cộng cọc chịu nén, phản lực ròng không kể trọng lượng đài")
    if Fp > 0:
        ratio = _punch_check(r, Fp, abs(p["Mx"]), abs(p["My"]), cx, cy, h0, Rbt)
        r.check("Chọc thủng đài", f"tỷ số = {fmt(ratio)} {'≤' if ratio <= 1 else '>'} 1", ratio <= 1)
    else:
        r.p("Tất cả tim cọc nằm trong tháp chọc thủng 45°: không cần kiểm tra.")
    r.h("Bước 3. Kiểm tra cắt đài")
    r.f("Q_b = [[1,5·γ_b1·R_bt·b·h_0^2 // C]],  0,5·γ_b1·R_bt·b·h_0 ≤ Q_b ≤ 2,5·γ_b1·R_bt·b·h_0")

    def cap_shear(axis, half_c, width):
        res = []
        for sg in (1, -1):
            rows_ = sorted({sg * (x if axis == 0 else y) for x, y in piles if sg * (x if axis == 0 else y) * 1000 > half_c})
            for r0 in rows_:
                C = max((r0 * 1000 - p["d"] / 2) - half_c, h0)
                C = min(C, 3 * h0)
                Q = sum(max(P, 0) for (x, y), P in zip(piles, Ppure) if sg * (x if axis == 0 else y) >= r0 - 1e-9)
                Qb = min(max(1.5 * Rbt * width * h0 ** 2 / C, 0.5 * Rbt * width * h0), 2.5 * Rbt * width * h0) / 1e3
                res.append((Q / Qb, C, Q, Qb))
        return max(res, key=lambda t: t[0]) if res else None

    for nm, sc in (("phương x", cap_shear(0, cx / 2, Y)), ("phương y", cap_shear(1, cy / 2, X))):
        if sc:
            r.check(f"Cắt {nm}", f"Q(C={fmt(sc[1])}) = {fmt(sc[2])} {'≤' if sc[0] <= 1 else '>'} Q_b = {fmt(sc[3])} kN", sc[0] <= 1)
    r.h("Bước 4. Thép đài")
    def side_m(axis, half):
        out = []
        for sg in (1, -1):
            out.append(sum(max(P, 0) * (sg * (x if axis == 0 else y) - half) for (x, y), P in zip(piles, Ppure) if sg * (x if axis == 0 else y) > half))
        return max(out)
    Mxdir = side_m(0, cx / 2000)
    Mydir = side_m(1, cy / 2000)
    r.calc("M_I", "ΣP_i·r_i", None, Mxdir, "kN·m", note="Phía bất lợi theo x, mômen quanh mép cột", key="MI")
    r.calc("M_II", "ΣP_i·r_i", None, Mydir, "kN·m", note="Phía bất lợi theo y, mômen quanh mép cột", key="MII")
    Rs = st["Rs"]
    As10, _ = report_flex(r, "x", Mxdir, Y, h0, Rb, Rs, aR)
    As20, _ = report_flex(r, "y", Mydir, X, h0 - 15, Rb, Rs, aR)
    if float("inf") in (As10, As20):
        raise InputError("Đài quá thấp so với mômen: tăng chiều cao đài.")
    Asx = max(As10, 0.001 * Y * h0)
    Asy = max(As20, 0.001 * X * h0)
    d1, s1, _ = pick_slab_bar(Asx / (Y / 1000), ds=(12, 14, 16, 18, 20, 22, 25))
    d2, s2, _ = pick_slab_bar(Asy / (X / 1000), ds=(12, 14, 16, 18, 20, 22, 25))
    r.table(["Phương", "As (mm²)", "Chọn"], [["Thép chịu lực theo x", Asx, f"Ø{d1}a{s1}"], ["Thép chịu lực theo y", Asy, f"Ø{d2}a{s2}"]])
    th = max(X, Y) / 18
    D = Drawing(th, "dai_coc")
    D.rect(-X / 2, -Y / 2, X, Y, "BE_TONG")
    D.rect(-cx / 2, -cy / 2, cx, cy, "BE_TONG")
    for k, (x, y) in enumerate(piles):
        D.rect(x * 1000 - p["d"] / 2, y * 1000 - p["d"] / 2, p["d"], p["d"], "KET_CAU", dash=True)
        D.text(x * 1000, y * 1000, str(k + 1), th * .8, "KY_HIEU")
    D.line(-X / 2 + 50, Y * .38, X / 2 - 50, Y * .38, "THEP")
    D.line(X * .38, -Y / 2 + 50, X * .38, Y / 2 - 50, "THEP")
    D.tag(0, Y * .38, X * .03, Y / 2 + th * 1.3, f"Ø{d1}a{s1}", 1)
    D.tag(X * .38, -Y * .4, X / 2 + th * .8, -Y * .3, f"Ø{d2}a{s2}", 2)
    D.dim(-X / 2, -Y / 2 - th, X / 2, -Y / 2 - th, 0, fmt(X))
    D.dim(-X / 2, -Y / 2, -X / 2, Y / 2, th, fmt(Y))
    D.title(0, -Y / 2 - th * 3.6, f"Đài {nx * ny} cọc (SL=1)", "tl: 1/50")
    r.fig(D, "Mặt bằng đài cọc")
    r.conclude(f"Pmax = {fmt(Pm)} kN; Pmin = {fmt(Pn)} kN với [P] = {fmt(p['Pa'])} kN.")
    r.conclude(f"Thép đài: Ø{d1}a{s1} theo x; Ø{d2}a{s2} theo y.")
    return r.finish()


# ---------- TƯỜNG CHẮN ----------
@problem("nm_wall", "nm", "Tường chắn đất trọng lực",
         "Áp lực đất chủ động Rankine, kiểm tra lật, trượt, áp lực đáy tường.",
         [num("H", "Chiều cao tường H", "m", 3, 0), num("bt", "Bề rộng đỉnh", "m", 0.6, 0), num("bb", "Bề rộng đáy", "m", 2.4, 0), num("gw", "γ vật liệu tường", "kN/m³", 22, 0),
          num("gs", "γ đất đắp", "kN/m³", 18, 0), num("phi", "φ đất đắp", "độ", 30, 0, 45), num("c", "c đất đắp", "kPa", 0, 0), num("q", "Hoạt tải mặt đất q", "kPa", 10, 0),
          num("f", "Hệ số ma sát đáy f", "", 0.5, 0, 1), num("Rd", "Sức chịu tải nền", "kPa", 200, 0), num("Kl", "Hệ số an toàn lật", "", 1.5, 1), num("Kt", "Hệ số an toàn trượt", "", 1.3, 1)])
def wall(p):
    r = Report("Tường chắn đất trọng lực", MOD, ["Rankine", "TCVN 9362:2012"])
    H, bt, bb, gw, gs, phi, c, q = p["H"], p["bt"], p["bb"], p["gw"], p["gs"], p["phi"], p["c"], p["q"]
    if bb < bt:
        raise InputError("Bề rộng đáy phải ≥ bề rộng đỉnh.")
    r.given([("Chiều cao", "H", H, "m"), ("Đỉnh, đáy", "bt; bb", f"{fmt(bt)}; {fmt(bb)}", "m"), ("γ tường", "γw", gw, "kN/m³"), ("Đất đắp", "γ; φ; c", f"{fmt(gs)}; {fmt(phi)}; {fmt(c)}", ""), ("Hoạt tải", "q", q, "kPa")])
    r.h("Bước 1. Áp lực đất chủ động")
    Ka = math.tan(math.radians(45 - phi / 2)) ** 2
    r.calc("K_a", "tan^2(45° − [[φ // 2]])", f"tan^2(45° − {fmt(phi / 2)}°)", Ka, key="Ka")
    p0 = q * Ka - 2 * c * math.sqrt(Ka)
    pH = (q + gs * H) * Ka - 2 * c * math.sqrt(Ka)
    r.calc("p_a(đỉnh)", "q·K_a − 2c√[[K_a]]", None, p0, "kPa")
    r.calc("p_a(đáy)", "(q + γ·H)K_a − 2c√[[K_a]]", None, pH, "kPa", key="pH")
    if p0 < 0:
        z0 = -p0 / (gs * Ka)
        r.calc("z_0", None, None, z0, "m", note="Chiều sâu vùng kéo, bỏ qua áp lực trong vùng này")
        Ea = pH * (H - z0) / 2
        ya = (H - z0) / 3
    else:
        Ea = (p0 + pH) / 2 * H
        ya = H * (2 * p0 + pH) / (3 * (p0 + pH)) if p0 + pH > 0 else H / 3
    r.calc("E_a", "[[p_a(đỉnh) + p_a(đáy) // 2]]·H", None, Ea, "kN/m", key="Ea")
    r.calc("y_a", None, None, ya, "m", note="Cao độ điểm đặt Ea so với đáy tường")
    r.h("Bước 2. Trọng lượng tường và mômen quanh mép trước")
    r.p("Lưng tường phẳng thẳng đứng phía đất, phần vát nằm phía trước tường.")
    w1 = bt * H * gw
    x1 = bb - bt / 2
    w2 = (bb - bt) * H / 2 * gw
    x2 = (bb - bt) * 2 / 3
    W = w1 + w2
    Mg = w1 * x1 + w2 * x2
    r.table(["Phần", "G (kN/m)", "Tay đòn (m)", "G·x (kN·m/m)"], [["Chữ nhật", w1, x1, w1 * x1], ["Tam giác", w2, x2, w2 * x2], ["Tổng", W, "", Mg]])
    Ml = Ea * ya
    r.calc("M_lật", "E_a·y_a", f"{fmt(Ea)}·{fmt(ya)}", Ml, "kN·m/m")
    r.h("Bước 3. Kiểm tra")
    Kl = Mg / Ml if Ml > 0 else float("inf")
    r.calc("K_lật", "[[ΣG·x // E_a·y_a]]", f"[[{fmt(Mg)} // {fmt(Ml)}]]", Kl, key="Kl")
    r.check("Chống lật", f"K = {fmt(Kl)} {'≥' if Kl >= p['Kl'] else '<'} {fmt(p['Kl'])}", Kl >= p["Kl"])
    Kt = p["f"] * W / Ea if Ea > 0 else float("inf")
    r.calc("K_trượt", "[[f·ΣG // E_a]]", f"[[{fmt(p['f'])}·{fmt(W)} // {fmt(Ea)}]]", Kt, key="Kt")
    r.check("Chống trượt", f"K = {fmt(Kt)} {'≥' if Kt >= p['Kt'] else '<'} {fmt(p['Kt'])}", Kt >= p["Kt"])
    e = bb / 2 - (Mg - Ml) / W
    r.calc("e", "[[b // 2]] − [[ΣG·x − E_a·y_a // ΣG]]", None, e, "m", key="e")
    pmx = W / bb * (1 + 6 * abs(e) / bb)
    pmn = W / bb * (1 - 6 * abs(e) / bb)
    r.calc("p_max", "[[ΣG // b]](1 + [[6e // b]])", None, pmx, "kPa", key="pmax")
    r.calc("p_min", "[[ΣG // b]](1 − [[6e // b]])", None, pmn, "kPa", key="pmin")
    ptb_w = W / bb
    r.calc("p_tb", "[[ΣG // b]]", None, ptb_w, "kPa")
    r.check("Áp lực trung bình", f"p_tb = {fmt(ptb_w)} {'≤' if ptb_w <= p['Rd'] else '>'} R = {fmt(p['Rd'])} kPa", ptb_w <= p["Rd"])
    r.check("Sức chịu tải nền", f"p_max = {fmt(pmx)} {'≤' if pmx <= 1.2 * p['Rd'] else '>'} 1,2R = {fmt(1.2 * p['Rd'])} kPa", pmx <= 1.2 * p["Rd"])
    r.check("Điểm đặt hợp lực trong lõi đáy", f"|e| = {fmt(abs(e))} {'≤' if abs(e) <= bb / 6 else '>'} b/6 = {fmt(bb / 6)} m", abs(e) <= bb / 6)
    k = 1000
    th = H * k / 20
    D = Drawing(th, "tuong_chan")
    D.pline([(0, 0), (bb * k, 0), (bb * k, H * k), ((bb - bt) * k, H * k)], "BE_TONG", True)
    D.line(bb * k, H * k, bb * k + 2.2 * H * k * .5, H * k, "PHU")
    sc = H * k * .28 / max(pH, 1)
    D.pline([(bb * k, H * k), (bb * k + max(p0, 0) * sc, H * k), (bb * k + pH * sc, 0), (bb * k, 0)], "BIEU_DO", True)
    D.text(bb * k + pH * sc + th * .3, th, f"{fmt(pH)} kPa", th * .85, "BIEU_DO", ha="left")
    D.arrow(bb * k + pH * sc * .7 + th * 2, ya * k, bb * k + th * .2, ya * k)
    D.text(bb * k + pH * sc * .7 + th * 2.4, ya * k, f"Ea={fmt(Ea)} kN/m", th * .9, "TAI_TRONG", ha="left")
    D.dim(0, -th * 1.4, bb * k, -th * 1.4, 0, f"{fmt(bb)} m")
    D.dim(0, 0, 0, H * k, th * 1.2, f"{fmt(H)} m")
    D.line(-th, 0, bb * k + H * k * .6, 0, "PHU", True)
    r.fig(D, "Sơ đồ tường và biểu đồ áp lực đất", width_cm=12)
    r.conclude(f"Ea = {fmt(Ea)} kN/m; Klật = {fmt(Kl)}; Ktrượt = {fmt(Kt)}; pmax = {fmt(pmx)} kPa.")
    r.conclude("Tường ĐẠT các điều kiện ổn định." if r.ok else "Tường KHÔNG ĐẠT ít nhất một điều kiện, cần tăng bề rộng đáy.")
    return r.finish()

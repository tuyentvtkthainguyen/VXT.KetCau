"""Kết cấu thép: công thức theo TCVN 5575:2012 và các điểm đã cập nhật theo TCVN 5575:2024."""
from __future__ import annotations
import math
from .registry import problem, num, sel, tbl, InputError
from ..core.report import Report, fmt
from ..core.drawing import Drawing
from ..core.materials import STEEL, E_STEEL, BOLTS, BOLT_AREA, ELECTRODE

MOD = "Kết cấu thép"
STD = "TCVN 5575:2024 (đã thay thế TCVN 5575:2012)"
DISCLAIMER = ("TCVN 5575:2024 đã thay thế bản 2012. Chương trình dùng E = 206000 MPa, f = fy/γM và các dạng công thức tương đương, "
              "nhưng chưa đối chiếu được toàn văn bản 2024 về hệ số, mác thép, cường độ bu lông. fy lấy cho thép mỏng, chưa giảm theo bề dày. Với hồ sơ thật, hãy đối chiếu với bản gốc của tiêu chuẩn.")
_steel = sel("steel", "Mác thép", list(STEEL), "CCT38")
_gm = num("gM", "Hệ số vật liệu γM", "", 1.05, 1)
_gc = num("gc", "Hệ số điều kiện làm việc γc", "", 1.0, 0, 1.2)
_sec = [num("h", "Chiều cao h", "mm", 400, 50), num("bf", "Bề rộng cánh bf", "mm", 200, 30), num("tw", "Bề dày bụng tw", "mm", 8, 2), num("tf", "Bề dày cánh tf", "mm", 12, 2)]


def I_props(h, bf, tw, tf):
    hw = h - 2 * tf
    if hw <= 0:
        raise InputError("Chiều cao bụng ≤ 0: kiểm tra h và tf.")
    A = 2 * bf * tf + hw * tw
    Ix = bf * h ** 3 / 12 - (bf - tw) * hw ** 3 / 12
    Iy = 2 * tf * bf ** 3 / 12 + hw * tw ** 3 / 12
    Wx = Ix / (h / 2)
    Wy = Iy / (bf / 2)
    Sx = bf * tf * (h - tf) / 2 + tw * hw ** 2 / 8
    Sf = bf * tf * (h - tf) / 2
    It = (2 * bf * tf ** 3 + hw * tw ** 3) / 3
    return dict(hw=hw, A=A, Ix=Ix, Iy=Iy, Wx=Wx, Wy=Wy, Sx=Sx, Sf=Sf, It=It, ix=math.sqrt(Ix / A), iy=math.sqrt(Iy / A))


def _report_sec(r, S, h, bf, tw, tf):
    r.h("Đặc trưng tiết diện", 2)
    r.calc("h_w", "h − 2t_f", None, S["hw"], "mm")
    r.calc("A", "2b_f·t_f + h_w·t_w", f"2·{fmt(bf)}·{fmt(tf)} + {fmt(S['hw'])}·{fmt(tw)}", S["A"], "mm²", key="A")
    r.calc("I_x", "[[b_f·h^3 − (b_f − t_w)h_w^3 // 12]]", None, S["Ix"], "mm⁴", key="Ix")
    r.calc("I_y", "[[2t_f·b_f^3 + h_w·t_w^3 // 12]]", None, S["Iy"], "mm⁴", key="Iy")
    r.calc("W_x", "[[2I_x // h]]", None, S["Wx"], "mm³", key="Wx")
    r.calc("S_x", "b_f·t_f·[[h − t_f // 2]] + [[t_w·h_w^2 // 8]]", None, S["Sx"], "mm³")
    r.calc("i_x", "√[[I_x/A]]", None, S["ix"], "mm")
    r.calc("i_y", "√[[I_y/A]]", None, S["iy"], "mm")


def _draw_I(h, bf, tw, tf, dim=True):
    th = max(h, bf) / 14
    D = Drawing(th, "tiet_dien_I")
    D.rect(-bf / 2, 0, bf, tf, "KET_CAU")
    D.rect(-bf / 2, h - tf, bf, tf, "KET_CAU")
    D.rect(-tw / 2, tf, tw, h - 2 * tf, "KET_CAU")
    D.dim(-bf / 2, -th, bf / 2, -th, 0, fmt(bf))
    D.dim(bf / 2 + th, 0, bf / 2 + th, h, -th * .1, fmt(h))
    D.text(tw + th * .3, h / 2, f"tw={fmt(tw)}", th * .85, ha="left")
    D.text(0, h + th * .9, f"tf={fmt(tf)}", th * .85)
    return D


def phi_buckling(lam_bar, curve):
    """Hệ số uốn dọc: công thức SP 16.13330.2017 (nguồn biên soạn TCVN 5575:2024), đường cong a, b, c."""
    al, be = {"a": (0.03, 0.06), "b": (0.04, 0.09), "c": (0.04, 0.14)}[curve]
    if lam_bar <= 0.4:
        return 1.0
    delta = 9.87 * (1 - al + be * lam_bar) + lam_bar ** 2
    phi = min(0.5 * (delta - math.sqrt(delta ** 2 - 39.48 * lam_bar ** 2)) / lam_bar ** 2, 1.0)
    if lam_bar > {"a": 3.8, "b": 4.4, "c": 5.8}[curve]:
        phi = min(phi, 7.6 / lam_bar ** 2)
    return phi


def phi_b_of(S, h, lo, f, load="top", E=E_STEEL):
    """Hệ số ổn định tổng thể dầm I đối xứng, tải đều."""
    alpha = 1.54 * S["It"] / S["Iy"] * (lo / h) ** 2
    if load == "top":
        psi = 1.6 + 0.08 * alpha if alpha <= 40 else 3.15 + 0.04 * alpha - 2.7e-5 * alpha ** 2
    else:
        psi = 3.8 + 0.08 * alpha if alpha <= 40 else 5.35 + 0.04 * alpha - 2.7e-5 * alpha ** 2
    phi1 = psi * S["Iy"] / S["Ix"] * (h / lo) ** 2 * E / f
    return (phi1 if phi1 <= 0.85 else min(0.68 + 0.21 * phi1, 1.0)), alpha, psi, phi1


def _report_mat(r, p, E=E_STEEL):
    st = STEEL[p["steel"]]
    f = st["fy"] / p["gM"]
    fv = 0.58 * f
    r.calc("f", "[[f_y // γ_M]]", f"[[{st['fy']} // {fmt(p['gM'])}]]", f, "MPa", key="f")
    r.calc("f_v", "0,58f", None, fv, "MPa")
    r.calc("E", None, None, E, "MPa")
    return st, f, fv


@problem("thep_beam", "thep", "Dầm thép chữ I chịu uốn",
         "Kiểm tra bền uốn, bền cắt, ứng suất tương đương, ổn định tổng thể, ổn định cục bộ, độ võng.",
         [num("M", "Mômen tính toán Mx", "kN·m", 120, 0), num("V", "Lực cắt tính toán V", "kN", 80, 0), num("qs", "Tải tiêu chuẩn phân bố", "kN/m", 18, 0), num("L", "Nhịp L", "m", 6, 0), num("flim", "Độ võng cho phép L/", "", 250, 1),
          num("lo", "Khoảng cách giằng cánh nén lo", "m", 3, 0), sel("load", "Vị trí tải", [("top", "Tải đều, đặt ở cánh trên"), ("bot", "Tải đều, đặt ở cánh dưới")]),
          _steel, _gm, _gc] + _sec)
def beam(p):
    r = Report("Dầm thép chữ I chịu uốn", MOD, [STD])
    h, bf, tw, tf = p["h"], p["bf"], p["tw"], p["tf"]
    S = I_props(h, bf, tw, tf)
    r.given([("Mômen, lực cắt", "M; V", f"{fmt(p['M'])}; {fmt(p['V'])}", "kN·m; kN"), ("Nhịp", "L", p["L"], "m"), ("Tiết diện", "h×bf×tw×tf", f"{fmt(h)}×{fmt(bf)}×{fmt(tw)}×{fmt(tf)}", "mm")])
    r.warn(DISCLAIMER)
    r.h("Vật liệu", 2)
    st, f, fv = _report_mat(r, p)
    gc = p["gc"]
    E = E_STEEL
    _report_sec(r, S, h, bf, tw, tf)
    r.fig(_draw_I(h, bf, tw, tf), "Tiết diện chữ I", width_cm=8)
    r.h("Bước 1. Bền chịu uốn")
    sg = p["M"] * 1e6 / S["Wx"]
    r.calc("σ", "[[M // W_x]]", f"[[{fmt(p['M'])}·10^6 // {fmt(S['Wx'])}]]", sg, "MPa", key="sigma")
    r.check("Bền uốn", f"σ = {fmt(sg)} {'≤' if sg <= f * gc else '>'} f·γ_c = {fmt(f * gc)} MPa", sg <= f * gc)
    r.h("Bước 2. Bền chịu cắt")
    ta = p["V"] * 1e3 * S["Sx"] / (S["Ix"] * tw)
    r.calc("τ", "[[V·S_x // I_x·t_w]]", f"[[{fmt(p['V'])}·10^3·{fmt(S['Sx'])} // {fmt(S['Ix'])}·{fmt(tw)}]]", ta, "MPa", key="tau")
    r.check("Bền cắt", f"τ = {fmt(ta)} {'≤' if ta <= fv * gc else '>'} f_v·γ_c = {fmt(fv * gc)} MPa", ta <= fv * gc)
    r.h("Bước 3. Ứng suất tương đương tại mép bụng")
    s1 = sg * S["hw"] / h
    t1 = p["V"] * 1e3 * S["Sf"] / (S["Ix"] * tw)
    std = math.sqrt(s1 ** 2 + 3 * t1 ** 2)
    r.calc("σ_1", "σ·[[h_w // h]]", None, s1, "MPa")
    r.calc("τ_1", "[[V·S_f // I_x·t_w]]", None, t1, "MPa")
    r.calc("σ_tđ", "√[[σ_1^2 + 3τ_1^2]]", f"√[[{fmt(s1)}^2 + 3·{fmt(t1)}^2]]", std, "MPa", key="std", note="Bỏ qua ứng suất cục bộ")
    r.check("Ứng suất tương đương", f"σ_tđ = {fmt(std)} ≤ 1,15f·γ_c = {fmt(1.15 * f * gc)} MPa", std <= 1.15 * f * gc)
    r.h("Bước 4. Ổn định tổng thể")
    lo = p["lo"] * 1000
    phib, alpha, psi, phi1 = phi_b_of(S, h, lo, f, p["load"])
    r.calc("I_t", "[[2b_f·t_f^3 + h_w·t_w^3 // 3]]", None, S["It"], "mm⁴")
    r.calc("α", "1,54·[[I_t // I_y]]·([[l_o // h]])^2", None, alpha)
    r.calc("ψ", None, None, psi, note="Dầm có tải đều, không có giằng trung gian (Phụ lục E)")
    r.calc("φ_1", "ψ·[[I_y // I_x]]·([[h // l_o]])^2·[[E // f]]", None, phi1)
    r.calc("φ_b", "φ_1 khi φ_1 ≤ 0,85; 0,68 + 0,21φ_1 khi φ_1 > 0,85 (≤ 1)", None, phib, key="phib")
    r.calc("σ_b", "[[M // φ_b·W_x]]", None, sg / phib, "MPa")
    r.check("Ổn định tổng thể", f"σ_b = {fmt(sg / phib)} {'≤' if sg / phib <= f * gc else '>'} f·γ_c = {fmt(f * gc)} MPa", sg / phib <= f * gc)
    r.h("Bước 5. Ổn định cục bộ")
    b0 = (bf - tw) / 2
    lb = b0 / tf
    lbl = 0.5 * math.sqrt(E / f)
    r.calc("b_0/t_f", "[[(b_f − t_w)/2 // t_f]]", None, lb)
    r.check("Cánh nén", f"b_0/t_f = {fmt(lb)} {'≤' if lb <= lbl else '>'} 0,5√[[E/f]] = {fmt(lbl)}", lb <= lbl)
    lw = S["hw"] / tw * math.sqrt(f / E)
    r.calc("λ̄_w", "[[h_w // t_w]]·√[[f/E]]", None, lw, key="lw")
    r.check("Bụng không cần sườn gia cường", f"λ̄_w = {fmt(lw)} {'≤' if lw <= 3.2 else '>'} 3,2", lw <= 3.2)
    if lw > 3.2:
        r.warn("Bụng mảnh: cần bố trí sườn ngang và kiểm tra ổn định các ô bụng.")
    r.h("Bước 6. Độ võng")
    fv_ = 5 * p["qs"] * (p["L"] * 1000) ** 4 / (384 * E * S["Ix"])
    flim = p["L"] * 1000 / p["flim"]
    r.calc("Δ", "[[5q_s·L^4 // 384·E·I_x]]", f"[[5·{fmt(p['qs'])}·{fmt(p['L'] * 1000)}^4 // 384·{fmt(E)}·{fmt(S['Ix'])}]]", fv_, "mm", key="defl")
    r.check("Độ võng", f"Δ = {fmt(fv_)} {'≤' if fv_ <= flim else '>'} L/{fmt(p['flim'])} = {fmt(flim)} mm", fv_ <= flim)
    r.conclude(f"σ = {fmt(sg)} MPa; τ = {fmt(ta)} MPa; σtđ = {fmt(std)} MPa; φb = {fmt(phib)}; Δ = {fmt(fv_)} mm.")
    r.conclude("Dầm ĐẠT các điều kiện." if r.ok else "Dầm KHÔNG ĐẠT ít nhất một điều kiện, xem các mục kiểm tra.")
    return r.finish()


@problem("thep_column", "thep", "Cột thép chữ I nén đúng tâm",
         "Độ mảnh hai phương, hệ số uốn dọc φ theo đường cong, kiểm tra bền, ổn định tổng thể, ổn định cục bộ.",
         [num("N", "Lực nén N", "kN", 800, 0), num("lx", "Chiều dài tính toán lx", "m", 6, 0), num("ly", "Chiều dài tính toán ly", "m", 3, 0),
          sel("cx", "Đường cong uốn dọc phương x", ["b", "a", "c"]), sel("cy", "Đường cong uốn dọc phương y", ["c", "b", "a"]),
          num("lim", "Độ mảnh giới hạn [λ]", "", 120, 0), _steel, _gm, _gc] + [num("h", "Chiều cao h", "mm", 300, 50), num("bf", "Bề rộng cánh bf", "mm", 250, 30), num("tw", "Bề dày bụng tw", "mm", 8, 2), num("tf", "Bề dày cánh tf", "mm", 14, 2)])
def column(p):
    r = Report("Cột thép chữ I nén đúng tâm", MOD, [STD])
    h, bf, tw, tf = p["h"], p["bf"], p["tw"], p["tf"]
    S = I_props(h, bf, tw, tf)
    N = p["N"]
    r.given([("Lực nén", "N", N, "kN"), ("Chiều dài tính toán", "lx; ly", f"{fmt(p['lx'])}; {fmt(p['ly'])}", "m"), ("Tiết diện", "h×bf×tw×tf", f"{fmt(h)}×{fmt(bf)}×{fmt(tw)}×{fmt(tf)}", "mm")])
    r.warn(DISCLAIMER)
    r.h("Vật liệu", 2)
    st, f, fv = _report_mat(r, p)
    gc = p["gc"]
    E = E_STEEL
    _report_sec(r, S, h, bf, tw, tf)
    r.fig(_draw_I(h, bf, tw, tf), "Tiết diện cột", width_cm=8)
    r.h("Bước 1. Độ mảnh")
    lx, ly = p["lx"] * 1000 / S["ix"], p["ly"] * 1000 / S["iy"]
    r.calc("λ_x", "[[l_x // i_x]]", f"[[{fmt(p['lx'] * 1000)} // {fmt(S['ix'])}]]", lx, key="lx")
    r.calc("λ_y", "[[l_y // i_y]]", f"[[{fmt(p['ly'] * 1000)} // {fmt(S['iy'])}]]", ly, key="ly")
    lm = max(lx, ly)
    r.check("Độ mảnh giới hạn", f"λ_max = {fmt(lm)} {'≤' if lm <= p['lim'] else '>'} [λ] = {fmt(p['lim'])}", lm <= p["lim"])
    r.h("Bước 2. Hệ số uốn dọc")
    r.f("λ̄ = λ·√[[f/E]];  δ = 9,87(1 − α + β·λ̄) + λ̄^2;  φ = [[0,5(δ − √[[δ^2 − 39,48λ̄^2]]) // λ̄^2]]")
    r.p("α, β theo đường cong: a (0,03; 0,06); b (0,04; 0,09); c (0,04; 0,14). φ không vượt 7,6/λ̄² khi λ̄ lớn hơn 3,8; 4,4; 5,8 tương ứng. Công thức theo SP 16.13330.2017, tài liệu gốc biên soạn TCVN 5575:2024.")
    lbx, lby = lx * math.sqrt(f / E), ly * math.sqrt(f / E)
    px, py = phi_buckling(lbx, p["cx"]), phi_buckling(lby, p["cy"])
    r.table(["Phương", "λ", "λ̄", "Đường cong", "φ"], [["x", lx, lbx, p["cx"], px], ["y", ly, lby, p["cy"], py]])
    phi = min(px, py)
    r.results.update(px=px, py=py)
    r.h("Bước 3. Kiểm tra")
    sb = N * 1e3 / S["A"]
    r.calc("σ", "[[N // A]]", f"[[{fmt(N)}·10^3 // {fmt(S['A'])}]]", sb, "MPa")
    r.check("Bền", f"σ = {fmt(sb)} ≤ f·γ_c = {fmt(f * gc)} MPa", sb <= f * gc)
    so = N * 1e3 / (phi * S["A"])
    r.calc("σ_(ôđ)", "[[N // φ_min·A]]", f"[[{fmt(N)}·10^3 // {fmt(phi)}·{fmt(S['A'])}]]", so, "MPa", key="so")
    r.check("Ổn định tổng thể", f"σ = {fmt(so)} {'≤' if so <= f * gc else '>'} f·γ_c = {fmt(f * gc)} MPa", so <= f * gc)
    lb = max(lbx, lby)
    r.h("Bước 4. Ổn định cục bộ")
    bo = (bf - tw) / 2 / tf
    limf = (0.36 + 0.1 * min(max(lb, 0.8), 4)) * math.sqrt(E / f)
    r.calc("[b_0/t_f]", "(0,36 + 0,1λ̄)√[[E/f]]", None, limf, note="0,8 ≤ λ̄ ≤ 4")
    r.check("Cánh", f"b_0/t_f = {fmt(bo)} {'≤' if bo <= limf else '>'} {fmt(limf)}", bo <= limf)
    lwl = min((1.2 + 0.35 * lb) if lb > 2 else (1.3 + 0.15 * lb ** 2), 2.3) * math.sqrt(E / f)
    r.calc("[h_w/t_w]", None, None, lwl, note="Theo công thức giới hạn độ mảnh bụng cột chữ I")
    r.check("Bụng", f"h_w/t_w = {fmt(S['hw'] / tw)} {'≤' if S['hw'] / tw <= lwl else '>'} {fmt(lwl)}", S["hw"] / tw <= lwl)
    r.conclude(f"λmax = {fmt(lm)}; φmin = {fmt(phi)}; σ ổn định = {fmt(so)} MPa, tỷ số {fmt(so / (f * gc))}.")
    r.conclude("Cột ĐẠT các điều kiện." if r.ok else "Cột KHÔNG ĐẠT ít nhất một điều kiện.")
    return r.finish()


@problem("thep_beamcol", "thep", "Cột thép nén lệch tâm",
         "Kiểm tra bền, ổn định trong mặt phẳng uốn bằng công thức tương tác, ổn định ngoài mặt phẳng.",
         [num("N", "Lực nén N", "kN", 500, 0), num("M", "Mômen Mx", "kN·m", 120, 0), num("lx", "lx", "m", 6, 0), num("ly", "ly", "m", 3, 0), sel("cx", "Đường cong phương x", ["b", "a", "c"]), sel("cy", "Đường cong phương y", ["c", "b", "a"]), _steel, _gm, _gc] + _sec)
def beamcol(p):
    r = Report("Cột thép nén lệch tâm", MOD, [STD])
    h, bf, tw, tf = p["h"], p["bf"], p["tw"], p["tf"]
    S = I_props(h, bf, tw, tf)
    N, M = p["N"], p["M"]
    r.given([("Lực nén, mômen", "N; M", f"{fmt(N)}; {fmt(M)}", "kN; kN·m"), ("Chiều dài tính toán", "lx; ly", f"{fmt(p['lx'])}; {fmt(p['ly'])}", "m"), ("Tiết diện", "h×bf×tw×tf", f"{fmt(h)}×{fmt(bf)}×{fmt(tw)}×{fmt(tf)}", "mm")])
    r.warn(DISCLAIMER)
    r.warn("Ổn định trong mặt phẳng uốn tiêu chuẩn tra hệ số φe từ bảng theo λ̄ và mef. Chương trình chưa có bảng này nên dùng công thức tương tác gần đúng. Chỉ dùng để tính sơ bộ; hồ sơ thật cần tra φe theo tiêu chuẩn.")
    r.h("Vật liệu", 2)
    st, f, fv = _report_mat(r, p)
    gc = p["gc"]
    E = E_STEEL
    _report_sec(r, S, h, bf, tw, tf)
    r.h("Bước 1. Bền")
    sg = N * 1e3 / S["A"] + M * 1e6 / S["Wx"]
    r.calc("σ", "[[N // A]] + [[M // W_x]]", f"[[{fmt(N)}·10^3 // {fmt(S['A'])}]] + [[{fmt(M)}·10^6 // {fmt(S['Wx'])}]]", sg, "MPa", key="sg")
    r.check("Bền", f"σ = {fmt(sg)} {'≤' if sg <= f * gc else '>'} f·γ_c = {fmt(f * gc)} MPa", sg <= f * gc)
    r.h("Bước 2. Độ mảnh và độ lệch tâm")
    lx, ly = p["lx"] * 1000 / S["ix"], p["ly"] * 1000 / S["iy"]
    lbx, lby = lx * math.sqrt(f / E), ly * math.sqrt(f / E)
    px, py = phi_buckling(lbx, p["cx"]), phi_buckling(lby, p["cy"])
    e = M * 1e6 / (N * 1e3) if N else 0
    m = e * S["A"] / S["Wx"]
    r.table(["Phương", "λ", "λ̄", "φ"], [["x", lx, lbx, px], ["y", ly, lby, py]])
    r.calc("e", "[[M // N]]", None, e, "mm")
    r.calc("m_x", "[[e·A // W_x]]", None, m, key="m")
    r.h("Bước 3. Ổn định trong mặt phẳng uốn (gần đúng)")
    NE = math.pi ** 2 * E * S["Ix"] / (p["lx"] * 1000) ** 2 / 1e3
    r.calc("N_E", "[[π^2·E·I_x // l_x^2]]", None, NE, "kN")
    if N >= NE:
        raise InputError("N ≥ NE: cột mất ổn định, tăng tiết diện.")
    t1 = N * 1e3 / (px * S["A"] * f * gc)
    t2 = M * 1e6 / (S["Wx"] * (1 - N / NE) * f * gc)
    r.calc("Tỷ số", "[[N // φ_x·A·f·γ_c]] + [[M // W_x(1 − N/N_E)f·γ_c]]", f"{fmt(t1)} + {fmt(t2)}", t1 + t2, key="ratio")
    r.check("Ổn định trong mặt phẳng", f"tỷ số = {fmt(t1 + t2)} {'≤' if t1 + t2 <= 1 else '>'} 1", t1 + t2 <= 1)
    r.h("Bước 4. Ổn định ngoài mặt phẳng uốn")
    mx = m
    al = 0.65 + 0.05 * min(mx, 5.0) if mx >= 1 else 0.7
    lc = 3.14 * math.sqrt(E / f)
    be = 1.0 if ly <= lc else math.sqrt(phi_buckling(lc * math.sqrt(f / E), p["cy"]) / py)
    phib_c, *_ = phi_b_of(S, h, p["ly"] * 1000, f)
    c5 = be / (1 + al * min(mx, 5.0))
    c10 = 1 / (1 + mx * py / phib_c) if mx > 5 else None
    if mx <= 5:
        c = c5
    elif mx >= 10:
        c = c10
    else:
        c10_ = 1 / (1 + 10 * py / phib_c)
        c = c5 * (2 - 0.2 * mx) + c10_ * (0.2 * mx - 1)
    r.calc("φ_b", None, None, phib_c, note="Theo chiều dài ly, tải đều cánh trên")
    r.calc("α", "0,65 + 0,05m_x", None, al, note="Tiết diện chữ I, 1 ≤ mx ≤ 5")
    r.calc("β", None, None, be)
    if mx > 5:
        r.p("Vì $m_x$ > 5: khi $m_x$ ≥ 10 lấy c = 1/(1 + $m_x$·$φ_y$/$φ_b$), khoảng 5 ÷ 10 nội suy.")
    r.calc("c", "[[β // 1 + α·m_x]]", None, c, key="c")
    so = N * 1e3 / (c * py * S["A"])
    r.calc("σ", "[[N // c·φ_y·A]]", None, so, "MPa", key="so")
    r.check("Ổn định ngoài mặt phẳng", f"σ = {fmt(so)} {'≤' if so <= f * gc else '>'} f·γ_c = {fmt(f * gc)} MPa", so <= f * gc)
    sbb = M * 1e6 / (phib_c * S["Wx"])
    r.check("Ổn định tổng thể do uốn", f"M/(φ_b·W_x) = {fmt(sbb)} {'≤' if sbb <= f * gc else '>'} f·γ_c = {fmt(f * gc)} MPa", sbb <= f * gc)
    r.h("Bước 5. Ổn định cục bộ")
    lb_ = max(lbx, lby)
    bo = (bf - tw) / 2 / tf
    limf = (0.36 + 0.1 * min(max(lb_, 0.8), 4)) * math.sqrt(E / f)
    r.check("Cánh", f"b_0/t_f = {fmt(bo)} {'≤' if bo <= limf else '>'} {fmt(limf)}", bo <= limf)
    lwl = min((1.2 + 0.35 * lb_) if lb_ > 2 else (1.3 + 0.15 * lb_ ** 2), 2.3) * math.sqrt(E / f)
    r.check("Bụng (thiên về an toàn theo giới hạn cột nén đúng tâm)", f"h_w/t_w = {fmt(S['hw'] / tw)} {'≤' if S['hw'] / tw <= lwl else '>'} {fmt(lwl)}", S["hw"] / tw <= lwl)
    r.conclude(f"σ bền = {fmt(sg)} MPa; tỷ số ổn định trong mặt phẳng = {fmt(t1 + t2)}; σ ngoài mặt phẳng = {fmt(so)} MPa.")
    r.conclude("Cột ĐẠT các điều kiện (trong mặt phẳng là kiểm tra gần đúng)." if r.ok else "Cột KHÔNG ĐẠT ít nhất một điều kiện.")
    return r.finish()


@problem("thep_bolt", "thep", "Liên kết bu lông thường chịu cắt, kéo",
         "Khả năng chịu cắt, ép mặt, kéo của một bu lông, lực lớn nhất trong nhóm bu lông chịu V, M, T, kiểm tra tổ hợp.",
         [num("V", "Lực cắt V", "kN", 120, 0), num("M", "Mômen trong mặt phẳng nhóm", "kN·m", 15, 0), num("T", "Lực kéo T (toàn nhóm)", "kN", 0, 0),
          sel("grade", "Cấp bền bu lông", list(BOLTS), "8.8"), sel("d", "Đường kính", list(BOLT_AREA), 20),
          num("nv", "Số mặt cắt", "", 1, 1, 4), num("gb", "Hệ số γb liên kết", "", 0.9, 0, 1), num("sumt", "Tổng bề dày nhỏ nhất ép mặt Σt", "mm", 12, 1), _steel,
          num("nr", "Số hàng (phương đứng)", "", 4, 1, 12), num("nc", "Số cột (phương ngang)", "", 2, 1, 6), num("py", "Khoảng cách hàng", "mm", 80, 20), num("px", "Khoảng cách cột", "mm", 80, 20), _gc])
def bolt(p):
    r = Report("Liên kết bu lông thường", MOD, [STD])
    d = int(p["d"])
    A, Abn = BOLT_AREA[d]
    bg = BOLTS[str(p["grade"])]
    st = STEEL[p["steel"]]
    gb, gc = p["gb"], p["gc"]
    r.given([("Nội lực", "V; M; T", f"{fmt(p['V'])}; {fmt(p['M'])}; {fmt(p['T'])}", "kN; kN·m; kN"), ("Bu lông", "d; cấp bền", f"M{d}; {p['grade']}", ""), ("Nhóm", "hàng×cột", f"{int(p['nr'])}×{int(p['nc'])}", "bu lông")])
    r.warn(DISCLAIMER + " Bản 2024 đã bỏ cấp bền 4.6, 4.8, 6.6 và thêm 12.9.")
    r.h("Bước 1. Khả năng chịu lực một bu lông")
    fvb, ftb = bg["fvb"], bg["ftb"]
    fu = st["fu"]
    fcb = (0.6 + 340 * fu / 210000) * fu
    r.calc("f_vb", None, None, fvb, "MPa")
    r.calc("f_tb", None, None, ftb, "MPa")
    r.calc("f_cb", "(0,6 + 340[[f_u // E]])f_u", f"(0,6 + 340·[[{fu} // 210000]]){fu}", fcb, "MPa", note="Bu lông thô, thường; đối chiếu Bảng cường độ ép mặt")
    Nv = fvb * gb * A * p["nv"] / 1e3
    Nc = fcb * gb * d * p["sumt"] / 1e3
    Nt = ftb * Abn / 1e3
    r.calc("[N]_v", "f_vb·γ_b·A·n_v", f"{fvb}·{fmt(gb)}·{fmt(A)}·{int(p['nv'])}·10^(-3)", Nv, "kN", key="Nv")
    r.calc("[N]_c", "f_cb·γ_b·d·Σt", f"{fmt(fcb)}·{fmt(gb)}·{d}·{fmt(p['sumt'])}·10^(-3)", Nc, "kN", key="Nc")
    r.calc("[N]_t", "f_tb·A_bn", f"{ftb}·{fmt(Abn)}·10^(-3)", Nt, "kN", key="Nt")
    Nmin = min(Nv, Nc) * gc
    r.calc("[N]_min", "min([N]_v; [N]_c)·γ_c", None, Nmin, "kN")
    r.h("Bước 2. Lực trong bu lông bất lợi nhất")
    nr, nc = int(p["nr"]), int(p["nc"])
    n = nr * nc
    pts = [((i - (nc - 1) / 2) * p["px"], (j - (nr - 1) / 2) * p["py"]) for j in range(nr) for i in range(nc)]
    Sr2 = sum(x ** 2 + y ** 2 for x, y in pts)
    xm = max(abs(x) for x, y in pts)
    ym = max(abs(y) for x, y in pts)
    r.calc("n", None, None, n)
    r.calc("Σr_i^2", "Σ(x_i^2 + y_i^2)", None, Sr2, "mm²")
    Vd = p["V"] / n
    if Sr2 > 0:
        Mx = p["M"] * 1e3 * ym / Sr2
        My = p["M"] * 1e3 * xm / Sr2
    else:
        Mx = My = 0
    r.calc("N_V", "[[V // n]]", None, Vd, "kN")
    r.calc("N_(M,x)", "[[M·y_max // Σr^2]]", None, Mx, "kN")
    r.calc("N_(M,y)", "[[M·x_max // Σr^2]]", None, My, "kN")
    Nb = math.hypot(Mx, Vd + My)
    r.calc("N_b", "√[[N_(M,x)^2 + (N_V + N_(M,y))^2]]", f"√[[{fmt(Mx)}^2 + ({fmt(Vd)} + {fmt(My)})^2]]", Nb, "kN", key="Nb")
    r.check("Chịu cắt, ép mặt", f"N_b = {fmt(Nb)} {'≤' if Nb <= Nmin else '>'} [N]_min = {fmt(Nmin)} kN", Nb <= Nmin)
    if p["T"]:
        Nt1 = p["T"] / n
        r.calc("N_t", "[[T // n]]", None, Nt1, "kN")
        r.check("Chịu kéo", f"N_t = {fmt(Nt1)} ≤ [N]_t = {fmt(Nt)} kN", Nt1 <= Nt)
        rat = math.hypot(Nb / Nv, Nt1 / Nt)
        r.calc("Tổ hợp", "√[[(N_b/[N]_v)^2 + (N_t/[N]_t)^2]]", None, rat, key="comb")
        r.check("Đồng thời cắt và kéo", f"{fmt(rat)} ≤ 1", rat <= 1)
    th = max((nc - 1) * p["px"], (nr - 1) * p["py"], 100) / 8
    D = Drawing(th, "nhom_bulong")
    W = (nc - 1) * p["px"] + 2 * 1.5 * d
    H = (nr - 1) * p["py"] + 2 * 2 * d
    D.rect(-W / 2, -H / 2, W, H, "KET_CAU")
    for x, y in pts:
        D.circle(x, y, d / 2 + 1, "THEP")
        D.line(x - d * .9, y, x + d * .9, y, "TRUC")
        D.line(x, y - d * .9, x, y + d * .9, "TRUC")
    if nc > 1:
        D.dim(-(nc - 1) * p["px"] / 2, -H / 2 - th, (nc - 1) * p["px"] / 2, -H / 2 - th, 0, f"{nc - 1}×{fmt(p['px'])}")
    if nr > 1:
        D.dim(-W / 2 - th, -(nr - 1) * p["py"] / 2, -W / 2 - th, (nr - 1) * p["py"] / 2, 0, f"{nr - 1}×{fmt(p['py'])}")
    r.fig(D, "Bố trí nhóm bu lông", width_cm=8)
    r.conclude(f"Một bu lông: [N]min = {fmt(Nmin)} kN. Lực lớn nhất Nb = {fmt(Nb)} kN: " + ("ĐẠT." if r.ok else "KHÔNG ĐẠT."))
    return r.finish()


@problem("thep_weld", "thep", "Liên kết hàn góc và hàn đối đầu",
         "Kiểm tra đường hàn chịu N, V, M theo tiết diện kim loại đường hàn và biên nóng chảy, hoặc hàn đối đầu.",
         [sel("kind", "Dạng đường hàn", [("fillet", "Hàn góc, 2 đường dọc"), ("butt", "Hàn đối đầu")]),
          num("N", "Lực dọc N (vuông góc đường hàn)", "kN", 150, 0), num("V", "Lực cắt V (dọc đường hàn)", "kN", 80, 0), num("M", "Mômen M", "kN·m", 8, 0),
          num("lw", "Chiều dài đường hàn lw (đã trừ 10 mm)", "mm", 250, 20), num("hf", "Chiều cao hf", "mm", 8, 3), num("t", "Bề dày nhỏ nhất của bản hàn", "mm", 10, 2),
          sel("el", "Que hàn", list(ELECTRODE), "N42"), _steel, _gm, _gc, num("bf", "βf", "", 0.7, 0, 1), num("bs", "βs", "", 1.0, 0, 1.2), sel("qc", "Kiểm tra chất lượng hàn đối đầu chịu kéo", [(1.0, "Có (fw = f)"), (0.85, "Không (fw = 0,85f)")], 1.0)])
def weld(p):
    r = Report("Liên kết hàn", MOD, [STD])
    st = STEEL[p["steel"]]
    gc = p["gc"]
    f = st["fy"] / p["gM"]
    N, V, M, lw = p["N"], p["V"], p["M"], p["lw"]
    r.given([("Nội lực", "N; V; M", f"{fmt(N)}; {fmt(V)}; {fmt(M)}", "kN; kN; kN·m"), ("Đường hàn", "lw; hf", f"{fmt(lw)}; {fmt(p['hf'])}", "mm"), ("Thép", p["steel"], f"fy = {st['fy']}; fu = {st['fu']}", "MPa")])
    r.warn(DISCLAIMER)
    if p["kind"] == "fillet":
        fwf = ELECTRODE[p["el"]]
        fws = 0.45 * st["fu"]
        bf_, bs = p["bf"], p["bs"]
        hf = p["hf"]
        r.h("Bước 1. Cường độ")
        r.calc("f_wf", None, None, fwf, "MPa", note=f"Que hàn {p['el']}")
        r.calc("f_ws", "0,45f_u", f"0,45·{st['fu']}", fws, "MPa")
        r.h("Bước 2. Cấu tạo")
        hmax = 1.2 * p["t"]
        r.check("Chiều cao đường hàn", f"h_f = {fmt(hf)} {'≤' if hf <= hmax else '>'} 1,2t = {fmt(hmax)} mm", hf <= hmax)
        r.check("Chiều dài đường hàn", f"l_w = {fmt(lw)} ≥ max(4h_f; 40) = {fmt(max(4 * hf, 40))} mm", lw >= max(4 * hf, 40))
        r.check("Chiều dài tính toán tối đa", f"l_w = {fmt(lw)} {'≤' if lw <= 85 * bf_ * hf else '>'} 85β_f·h_f = {fmt(85 * bf_ * hf)} mm", lw <= 85 * bf_ * hf)
        r.note("Chiều cao đường hàn tối thiểu tra theo bề dày bản và cách hàn: hãy đối chiếu bảng của tiêu chuẩn.")
        r.h("Bước 3. Ứng suất")
        def stresses(beta):
            A = 2 * beta * hf * lw
            W = 2 * beta * hf * lw ** 2 / 6
            sn = N * 1e3 / A + M * 1e6 / W
            tv = V * 1e3 / A
            return A, W, sn, tv, math.hypot(sn, tv)
        Af, Wf, sf, tf_, rf = stresses(bf_)
        As, Ws, ss, ts, rs = stresses(bs)
        r.p("Kiểm tra theo kim loại đường hàn với $β_f$:")
        r.calc("A_w", "2β_f·h_f·l_w", f"2·{fmt(bf_)}·{fmt(hf)}·{fmt(lw)}", Af, "mm²")
        r.calc("W_w", "[[2β_f·h_f·l_w^2 // 6]]", None, Wf, "mm³")
        r.calc("τ_N", "[[N // A_w]] + [[M // W_w]]", None, sf, "MPa")
        r.calc("τ_V", "[[V // A_w]]", None, tf_, "MPa")
        r.calc("τ_(wf)", "√[[τ_N^2 + τ_V^2]]", f"√[[{fmt(sf)}^2 + {fmt(tf_)}^2]]", rf, "MPa", key="twf")
        r.check("Kim loại đường hàn", f"τ_wf = {fmt(rf)} {'≤' if rf <= fwf * gc else '>'} f_wf·γ_c = {fmt(fwf * gc)} MPa", rf <= fwf * gc)
        r.p("Kiểm tra theo biên nóng chảy với $β_s$:")
        r.calc("τ_(ws)", "√[[τ_N^2 + τ_V^2]]", None, rs, "MPa", key="tws")
        r.check("Biên nóng chảy", f"τ_ws = {fmt(rs)} {'≤' if rs <= fws * gc else '>'} f_ws·γ_c = {fmt(fws * gc)} MPa", rs <= fws * gc)
        r.conclude(f"τwf = {fmt(rf)} MPa; τws = {fmt(rs)} MPa: " + ("ĐẠT." if r.ok else "KHÔNG ĐẠT."))
    else:
        t = p["t"]
        k = float(p["qc"])
        fw = k * f
        fwv = 0.58 * f
        r.h("Lời giải")
        r.calc("f_w", f"{fmt(k)}·f", None, fw, "MPa")
        r.calc("f_wv", "0,58f", None, fwv, "MPa")
        A = t * lw
        W = t * lw ** 2 / 6
        I = t * lw ** 3 / 12
        sn = N * 1e3 / A + M * 1e6 / W
        tv = 1.5 * V * 1e3 / A
        r.calc("σ_w", "[[N // t·l_w]] + [[6M // t·l_w^2]]", None, sn, "MPa", key="sw")
        r.check("Ứng suất pháp", f"σ_w = {fmt(sn)} {'≤' if sn <= fw * gc else '>'} f_w·γ_c = {fmt(fw * gc)} MPa", sn <= fw * gc)
        r.calc("τ_w", "[[1,5V // t·l_w]]", None, tv, "MPa", key="tw")
        r.check("Ứng suất tiếp", f"τ_w = {fmt(tv)} {'≤' if tv <= fwv * gc else '>'} f_wv·γ_c = {fmt(fwv * gc)} MPa", tv <= fwv * gc)
        st_ = math.sqrt(sn ** 2 + 3 * tv ** 2)
        r.calc("σ_tđ", "√[[σ_w^2 + 3τ_w^2]]", None, st_, "MPa", key="std")
        r.check("Tương đương", f"σ_tđ = {fmt(st_)} {'≤' if st_ <= 1.15 * fw * gc else '>'} 1,15f_w·γ_c = {fmt(1.15 * fw * gc)} MPa", st_ <= 1.15 * fw * gc)
        r.conclude(f"σw = {fmt(sn)} MPa; τw = {fmt(tv)} MPa; σtđ = {fmt(st_)} MPa: " + ("ĐẠT." if r.ok else "KHÔNG ĐẠT."))
    return r.finish()

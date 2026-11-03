"""Cơ học kết cấu."""
from __future__ import annotations
import math
import numpy as np
from .registry import problem, num, sel, tbl, InputError
from ..core.report import Report, fmt
from ..core.drawing import Drawing, diagram
from ..core.beam import BeamModel, report_beam_solution, draw_scheme
from ..core.frame import Frame, draw_frame, draw_frame_diagram

MOD = "Cơ học kết cấu"


# ---------- DẦM NHIỀU NHỊP TĨNH ĐỊNH ----------
@problem("ckc_gerber", "ckc", "Dầm tĩnh định nhiều nhịp (có khớp)",
         "Giải dầm Gerber bằng hệ phương trình cân bằng cộng điều kiện mômen bằng 0 tại khớp, vẽ biểu đồ Q, M.",
         [num("L", "Tổng chiều dài dầm", "m", 14, 0),
          tbl("sup", "Gối tựa", [("x", "x (m)"), ("t", "Loại", ["khớp", "ngàm"])], [dict(x=0, t="ngàm"), dict(x=8, t="khớp"), dict(x=14, t="khớp")]),
          tbl("h", "Khớp trong dầm", [("x", "x (m)")], [dict(x=5), dict(x=10)], min_rows=0),
          tbl("P", "Lực tập trung", [("x", "x (m)"), ("P", "P xuống + (kN)")], [dict(x=11, P=40)], min_rows=0),
          tbl("M", "Mômen tập trung", [("x", "x (m)"), ("M", "M thuận kim + (kN·m)")], [], min_rows=0),
          tbl("q", "Tải phân bố", [("a", "từ x (m)"), ("b", "đến x (m)"), ("q1", "q đầu (kN/m)"), ("q2", "q cuối (kN/m)")], [dict(a=0, b=8, q1=12, q2=12)], min_rows=0)])
def gerber(p):
    r = Report("Dầm tĩnh định nhiều nhịp", MOD)
    sup = [(s["x"], "fixed" if s["t"] == "ngàm" else "pin") for s in p["sup"]]
    try:
        b = BeamModel(p["L"], sup, [h["x"] for h in p["h"]], [(q["x"], q["P"]) for q in p["P"]], [(q["x"], q["M"]) for q in p["M"]], [(q["a"], q["b"], q["q1"], q["q2"]) for q in p["q"]])
    except ValueError as e:
        raise InputError(str(e))
    nn = len(b.reactions)
    r.h("Phân tích cấu tạo")
    r.p(f"Số ẩn phản lực: {nn}. Số phương trình: 2 phương trình cân bằng + {len(b.hinges)} điều kiện tại khớp = {2 + len(b.hinges)}. Hệ tĩnh định.")
    m = report_beam_solution(r, b)
    r.conclude(f"|M|max = {fmt(abs(m['M']))} kN·m tại x = {fmt(m['xM'])} m; |Q|max = {fmt(abs(m['Q']))} kN.")
    r.conclude("Phản lực: " + "; ".join(f"{lb.replace('_', '')} = {fmt(v)}" for lb, k, x, v in b.reactions))
    return r.finish()


# ---------- BA MÔMEN ----------
def _fixed_terms(span):
    """Trả về (A_trái, A_phải): vế phải 6Ωb/L, 6Ωa/L của nhịp đơn giản."""
    L = span["L"]
    AL = AR = 0.0
    q = span["q"]
    if q:
        AL += q * L ** 3 / 4
        AR += q * L ** 3 / 4
    P_, a = span["P"], span["a"]
    if P_:
        b = L - a
        AL += P_ * a * b * (L + b) / L
        AR += P_ * a * b * (L + a) / L
    return AL, AR


@problem("ckc_threemoment", "ckc", "Dầm liên tục: phương trình ba mômen",
         "Lập và giải phương trình ba mômen Clapeyron, tính mômen gối, phản lực, biểu đồ Q, M. Mỗi nhịp có tải đều và một lực tập trung.",
         [tbl("sp", "Các nhịp", [("L", "L (m)"), ("I", "I tương đối"), ("q", "q (kN/m)"), ("P", "P (kN)"), ("a", "a từ gối trái (m)")],
              [dict(L=6, I=1, q=20, P=0, a=0), dict(L=5, I=1, q=20, P=0, a=0), dict(L=6, I=1, q=20, P=50, a=3)], min_rows=2),
          sel("left", "Đầu trái", [("pin", "Khớp"), ("fixed", "Ngàm")]),
          sel("right", "Đầu phải", [("pin", "Khớp"), ("fixed", "Ngàm")])])
def threemoment(p):
    r = Report("Dầm liên tục: phương trình ba mômen", MOD)
    sp = p["sp"]
    for s in sp:
        if s["L"] <= 0 or s["I"] <= 0:
            raise InputError("L và I của nhịp phải dương.")
        if s["P"] and not (0 < s["a"] < s["L"]):
            raise InputError("Vị trí lực P phải nằm trong nhịp.")
    n = len(sp)
    r.h("Số liệu đề bài")
    r.table(["Nhịp", "L (m)", "I", "q (kN/m)", "P (kN)", "a (m)"], [[i + 1, s["L"], s["I"], s["q"], s["P"], s["a"]] for i, s in enumerate(sp)])
    r.p(f"Đầu trái: {'ngàm' if p['left'] == 'fixed' else 'khớp'}. Đầu phải: {'ngàm' if p['right'] == 'fixed' else 'khớp'}.")
    r.h("Bước 1. Hệ cơ bản")
    r.p("Đặt khớp tại các gối trung gian, biến dầm thành chuỗi dầm đơn giản. Ẩn số là mômen tại các gối. Với đầu ngàm, xem như có thêm nhịp ảo chiều dài bằng 0.")
    r.f("M_(i−1)·[[L_i // I_i]] + 2M_i·([[L_i // I_i]] + [[L_(i+1) // I_(i+1)]]) + M_(i+1)·[[L_(i+1) // I_(i+1)]] = −[[A_i^p // I_i]] − [[A_(i+1)^t // I_(i+1)]]")
    r.p("Vế phải theo tải trên nhịp đơn giản: tải đều $A^t = A^p = q·L^3/4$; lực tập trung $A^t = P·a·b(L+b)/L$, $A^p = P·a·b(L+a)/L$ (với b = L − a).")
    r.h("Bước 2. Vế phải của từng nhịp")
    AA = [_fixed_terms(s) for s in sp]
    r.table(["Nhịp", "Aᵗ (kN·m²)", "Aᵖ (kN·m²)"], [[i + 1, AA[i][0], AA[i][1]] for i in range(n)])
    # ẩn: M tại gối 0..n
    idx = []
    if p["left"] == "fixed":
        idx.append(0)
    idx += list(range(1, n))
    if p["right"] == "fixed":
        idx.append(n)
    m = len(idx)
    A = np.zeros((m, m))
    B = np.zeros(m)
    pos = {g: k for k, g in enumerate(idx)}
    eqs = []
    for k, g in enumerate(idx):
        ll = sp[g - 1]["L"] / sp[g - 1]["I"] if g > 0 else 0.0
        lr = sp[g]["L"] / sp[g]["I"] if g < n else 0.0
        A[k, k] = 2 * (ll + lr)
        if g - 1 in pos:
            A[k, pos[g - 1]] = ll
        if g + 1 in pos:
            A[k, pos[g + 1]] = lr
        rhs = 0.0
        if g > 0:
            rhs -= AA[g - 1][1] / sp[g - 1]["I"]
        if g < n:
            rhs -= AA[g][0] / sp[g]["I"]
        B[k] = rhs
        t = []
        if g - 1 in pos:
            t.append(f"{fmt(ll)}·M_{g - 1}")
        t.append(f"{fmt(A[k, k])}·M_{g}")
        if g + 1 in pos:
            t.append(f"{fmt(lr)}·M_{g + 1}")
        eqs.append(" + ".join(t) + f" = {fmt(rhs)}")
    r.h("Bước 3. Hệ phương trình")
    for k, e in enumerate(eqs):
        r.f(f"Gối {idx[k]}: {e}")
    sol = np.linalg.solve(A, B)
    Ms = [0.0] * (n + 1)
    for k, g in enumerate(idx):
        Ms[g] = float(sol[k])
    r.h("Bước 4. Mômen tại các gối")
    for g in range(n + 1):
        r.calc(f"M_{g}", None, None, Ms[g], "kN·m", key=f"M{g}")
    r.h("Bước 5. Lực cắt, phản lực và biểu đồ")
    r.f("Q_(đầu trái) = Q^0_t + [[M_p − M_t // L]];  Q_(đầu phải) = Q^0_p + [[M_p − M_t // L]]")
    # xây xs,Q,M
    xs, Qs, Mv = [], [], []
    x0 = 0
    Rs = [0.0] * (n + 1)
    rows = []
    for i, s in enumerate(sp):
        L = s["L"]
        ML, MR = Ms[i], Ms[i + 1]
        R0L = s["q"] * L / 2 + (s["P"] * (L - s["a"]) / L if s["P"] else 0)
        R0R = s["q"] * L / 2 + (s["P"] * s["a"] / L if s["P"] else 0)
        V = (MR - ML) / L
        QL = R0L + V
        QR = -R0R + V
        Rs[i] += QL
        Rs[i + 1] -= QR
        rows.append([i + 1, R0L, R0R, V, QL, QR])
        ts = np.linspace(0, L, 61)
        if s["P"]:
            ts = np.unique(np.sort(np.concatenate([ts, [s["a"] - 1e-6, s["a"] + 1e-6]])))
        for t in ts:
            q_ = QL - s["q"] * t - (s["P"] if s["P"] and t > s["a"] else 0)
            m_ = ML + QL * t - s["q"] * t ** 2 / 2 - (s["P"] * (t - s["a"]) if s["P"] and t > s["a"] else 0)
            xs.append(x0 + t)
            Qs.append(q_)
            Mv.append(m_)
        x0 += L
    r.table(["Nhịp", "Q⁰ trái", "Q⁰ phải", "(Mp−Mt)/L", "Q đầu trái", "Q đầu phải"], rows, "Đơn vị kN; Q⁰ là lực cắt đầu nhịp của dầm đơn giản (đầu phải lấy trị số).")
    for g in range(n + 1):
        r.calc(f"R_{g}", None, None, Rs[g], "kN", key=f"R{g}")
    tot = sum(s["q"] * s["L"] + s["P"] for s in sp)
    r.check("Cân bằng lực đứng", f"ΣR = {fmt(sum(Rs))} kN = tổng tải {fmt(tot)} kN", abs(sum(Rs) - tot) < 1e-6 * max(tot, 1))
    Ltot = sum(s["L"] for s in sp) * 1000
    th = Ltot / 45
    d = Drawing(th, "bieu_do")
    y = 0
    xp = 0
    H = Ltot * .12
    d.line(0, y, Ltot, y, "KET_CAU")
    sup_pos = [0]
    for s in sp:
        xp += s["L"] * 1000
        sup_pos.append(xp)
    for k, X in enumerate(sup_pos):
        fx = (k == 0 and p["left"] == "fixed") or (k == n and p["right"] == "fixed")
        if fx:
            d.fixed(X, y, -1 if k == 0 else 1)
        else:
            d.pin(X, y)
    for k, s in enumerate(sp):
        a, c = sup_pos[k], sup_pos[k + 1]
        if s["q"]:
            d.line(a, y + th * 3, c, y + th * 3, "TAI_TRONG")
            for t in range(max(int((c - a) / (th * 2.4)), 2) + 1):
                xk = a + (c - a) * t / max(int((c - a) / (th * 2.4)), 2)
                d.arrow(xk, y + th * 3, xk, y + th * .25)
            d.text((a + c) / 2, y + th * 3.9, f"q={fmt(s['q'])} kN/m", th, "TAI_TRONG")
        if s["P"]:
            d.arrow(a + s["a"] * 1000, y + th * 5.6, a + s["a"] * 1000, y + th * .2)
            d.text(a + s["a"] * 1000 + th * .3, y + th * 5.6, f"P={fmt(s['P'])}", th, "TAI_TRONG", ha="left")
        d.dim(a, y - th * 3.8, c, y - th * 3.8, 0, f"{fmt(s['L'])} m")
    Xs = np.array(xs) * 1000
    Qa = np.array(Qs)
    Ma = np.array(Mv)
    yq = -H - th * 6
    sq = H / max(np.abs(Qa).max(), 1e-9)
    diagram(d, Xs, Qa, 0, yq, sq, n_hatch=45)
    d.text(-th * 2.2, yq, "Q (kN)", th)
    ym = yq - 2.2 * H - th * 3
    sm = H / max(np.abs(Ma).max(), 1e-9)
    diagram(d, Xs, Ma, 0, ym, sm, flip=True, n_hatch=45)
    d.text(-th * 2.2, ym, "M (kN·m)", th)
    for g, X in enumerate(sup_pos):
        if Ms[g]:
            d.text(X, ym - Ms[g] * sm + np.sign(-Ms[g]) * th * .9, fmt(Ms[g]), th * .9)
    x0 = 0
    Mspan = []
    for i, s in enumerate(sp):
        mask = (np.array(xs) >= x0 - 1e-9) & (np.array(xs) <= x0 + s["L"] + 1e-9)
        j = int(np.argmax(np.where(mask, Ma, -1e30)))
        Mspan.append(float(Ma[j]))
        if Ma[j] > 0:
            d.text(Xs[j], ym - Ma[j] * sm - th * .9, fmt(float(Ma[j])), th * .9)
        x0 += s["L"]
    r.fig(d, "Sơ đồ dầm và biểu đồ Q, M")
    r.results.update(Mspan=Mspan)
    r.conclude("Mômen gối: " + "; ".join(f"M{g} = {fmt(Ms[g])}" for g in range(n + 1)) + " kN·m.")
    r.conclude("Mômen nhịp lớn nhất: " + "; ".join(f"nhịp {i + 1}: {fmt(v)}" for i, v in enumerate(Mspan)) + " kN·m.")
    r.conclude("Phản lực: " + "; ".join(f"R{g} = {fmt(Rs[g])}" for g in range(n + 1)) + " kN.")
    return r.finish()


# ---------- KHUNG ----------
_frame_fields = [
    tbl("nodes", "Nút", [("x", "x (m)"), ("y", "y (m)")], [dict(x=0, y=0), dict(x=0, y=4), dict(x=6, y=4), dict(x=6, y=0)], min_rows=2),
    tbl("elems", "Thanh (nút đánh số từ 1)", [("i", "Nút i"), ("j", "Nút j"), ("EI", "EI (kN·m²)"), ("qy", "q đứng, xuống + (kN/m)"), ("qx", "q ngang, sang phải + (kN/m)"), ("hi", "Khớp đầu i (1/0)"), ("hj", "Khớp đầu j (1/0)")],
        [dict(i=1, j=2, EI=20000, qy=0, qx=0, hi=0, hj=0), dict(i=2, j=3, EI=40000, qy=20, qx=0, hi=0, hj=0), dict(i=3, j=4, EI=20000, qy=0, qx=0, hi=0, hj=0)]),
    tbl("sup", "Gối tựa", [("n", "Nút"), ("ux", "Giữ ngang (1/0)"), ("uy", "Giữ đứng (1/0)"), ("rz", "Giữ xoay (1/0)")], [dict(n=1, ux=1, uy=1, rz=1), dict(n=4, ux=1, uy=1, rz=1)]),
    tbl("load", "Tải tại nút", [("n", "Nút"), ("fx", "Fx phải + (kN)"), ("fy", "Fy lên + (kN)"), ("m", "M ngược kim + (kN·m)")], [dict(n=2, fx=30, fy=0, m=0)], min_rows=0),
]


def _build_frame(p, EA_factor=1e5, ea=None):
    nodes = [(q["x"], q["y"]) for q in p["nodes"]]
    n = len(nodes)
    elems = []
    for q in p["elems"]:
        i, j = int(q["i"]) - 1, int(q["j"]) - 1
        if not (0 <= i < n and 0 <= j < n) or i == j:
            raise InputError(f"Thanh {int(q['i'])}-{int(q['j'])}: số nút không hợp lệ.")
        if q["EI"] <= 0:
            raise InputError("EI phải dương.")
        elems.append(dict(i=i, j=j, EI=q["EI"], EA=q["EI"] * EA_factor, qx=q["qx"], qy=q["qy"], rel_i=bool(q["hi"]), rel_j=bool(q["hj"])))
    sup = {}
    for s in p["sup"]:
        k = int(s["n"]) - 1
        if not 0 <= k < n:
            raise InputError("Gối đặt tại nút không tồn tại.")
        sup[k] = (int(s["ux"]), int(s["uy"]), int(s["rz"]))
    nodal = []
    for l in p["load"]:
        k = int(l["n"]) - 1
        if not 0 <= k < n:
            raise InputError("Tải đặt tại nút không tồn tại.")
        nodal.append((k, l["fx"], l["fy"], l["m"]))
    return nodes, elems, sup, nodal


def _report_member_forces(r, fr):
    rows = []
    for k, e in enumerate(fr.elems):
        L = fr.loc[k][0]
        Ni, Vi, Mi = fr.forces(k, 0)
        Nj, Vj, Mj = fr.forces(k, L)
        rows.append([k + 1, f"{e['i'] + 1}-{e['j'] + 1}", L, Ni, Vi, Mi, Nj, Vj, Mj])
    r.table(["Thanh", "i-j", "L (m)", "Nᵢ (kN)", "Qᵢ (kN)", "Mᵢ (kN·m)", "Nⱼ (kN)", "Qⱼ (kN)", "Mⱼ (kN·m)"], rows, "N dương khi kéo. M dương gây căng thớ bên phải chiều i → j (thanh ngang i → j từ trái sang phải: căng thớ dưới).")


def _reactions_rows(fr):
    rows = []
    for nd, rr in fr.supports.items():
        rows.append([nd + 1, fr.R[3 * nd] if rr[0] else "–", fr.R[3 * nd + 1] if rr[1] else "–", fr.R[3 * nd + 2] if rr[2] else "–"])
    return rows


@problem("ckc_frame_stiff", "ckc", "Khung phẳng: phương pháp chuyển vị (độ cứng)",
         "Lập ma trận độ cứng từng thanh, ghép tổng thể, giải chuyển vị nút, nội lực và biểu đồ M, Q, N. Giải được khung siêu tĩnh bất kỳ, có thể có khớp đầu thanh.",
         _frame_fields)
def frame_stiff(p):
    r = Report("Khung phẳng: phương pháp chuyển vị", MOD)
    nodes, elems, sup, nodal = _build_frame(p)
    fr = Frame(nodes, elems, sup, nodal)
    try:
        fr.solve()
    except ValueError as e:
        raise InputError(str(e))
    r.h("Số liệu đề bài")
    r.fig(draw_frame(fr), "Sơ đồ khung, số hiệu nút và thanh")
    r.table(["Thanh", "i-j", "L (m)", "EI (kN·m²)", "q đứng", "q ngang"], [[k + 1, f"{e['i'] + 1}-{e['j'] + 1}", fr.loc[k][0], e["EI"], e["qy"], e["qx"]] for k, e in enumerate(elems)])
    r.note("Bỏ qua biến dạng dọc trục: chương trình lấy EA = 10⁵·EI, đủ lớn để sai khác với lời giải tay không đáng kể.")
    r.h("Bước 1. Ma trận độ cứng phần tử")
    r.p("Trong hệ địa phương, với chuyển vị [u₁, v₁, θ₁, u₂, v₂, θ₂]:")
    r.f("a = [[EA // L]];  b = [[12EI // L^3]];  c = [[6EI // L^2]];  d = [[4EI // L]];  e = [[2EI // L]]")
    rows = []
    for k, e in enumerate(elems):
        L = fr.loc[k][0]
        EI, EA = e["EI"], e["EA"]
        rows.append([k + 1, L, EA / L, 12 * EI / L ** 3, 6 * EI / L ** 2, 4 * EI / L, 2 * EI / L])
    r.table(["Thanh", "L", "a", "b", "c", "d", "e"], rows, "Các hệ số độ cứng từng thanh.")
    r.p("Chuyển sang hệ tổng thể bằng ma trận quay: $K_e = T^T·k·T$, với $T$ phụ thuộc cosα và sinα của thanh.")
    r.h("Bước 2. Tải trọng nút tương đương")
    r.f("Với tải đều q vuông góc thanh: F = [[q·L // 2]];  M_(ngàm) = [[q·L^2 // 12]]")
    rows = []
    for k, e in enumerate(elems):
        L, T, ke, f0, qa, qt = fr.loc[k]
        if qa or qt:
            rows.append([k + 1, qt, qa, abs(qt) * L / 2, abs(qt) * L ** 2 / 12])
    if rows:
        r.table(["Thanh", "q ⊥ thanh (kN/m)", "q dọc thanh (kN/m)", "qL/2 (kN)", "qL²/12 (kN·m)"], rows)
    else:
        r.p("Không có tải phân bố trên thanh.")
    r.h("Bước 3. Ghép và giải hệ")
    r.p(f"Tổng số bậc tự do: {3 * len(nodes)}; số bậc tự do bị giữ: {len(fr.fixed)}; số ẩn chuyển vị: {len(fr.free)}. Giải $K_ff·U_f = F_f$.")
    if len(fr.free) <= 8:
        names = [f"{'uvθ'[q % 3]}{q // 3 + 1}" for q in fr.free]
        r.table(["K"] + names + ["F"], [[names[i]] + [f"{v:.4g}".replace(".", ",") for v in fr.Kff[i]] + [f"{fr.F_full[fr.free[i]]:.4g}".replace(".", ",")] for i in range(len(names))], "Ma trận độ cứng thu gọn và véc tơ tải.")
    r.h("Bước 4. Chuyển vị nút")
    r.table(["Nút", "u (mm)", "v (mm)", "θ (10⁻³ rad)"], [[i + 1, fr.U[3 * i] * 1000, fr.U[3 * i + 1] * 1000, fr.U[3 * i + 2] * 1000] for i in range(len(nodes))])
    r.results["U"] = fr.U.tolist()
    r.h("Bước 5. Phản lực")
    r.table(["Nút", "Rx (kN)", "Ry (kN)", "M (kN·m)"], _reactions_rows(fr))
    r.results["R"] = fr.R.tolist()
    r.h("Bước 6. Nội lực đầu thanh")
    _report_member_forces(r, fr)
    r.results["M_ends"] = [(fr.forces(k, 0)[2], fr.forces(k, fr.loc[k][0])[2]) for k in range(len(elems))]
    r.h("Bước 7. Biểu đồ nội lực")
    r.fig(draw_frame_diagram(fr, "M", "Biểu đồ M", "kN·m"), "Biểu đồ mômen M (vẽ về phía thớ căng)")
    r.fig(draw_frame_diagram(fr, "V", "Biểu đồ Q", "kN"), "Biểu đồ lực cắt Q")
    r.fig(draw_frame_diagram(fr, "N", "Biểu đồ N", "kN"), "Biểu đồ lực dọc N")
    r.h("Kiểm tra cân bằng tổng thể")
    Fx = sum(l[1] for l in nodal) + sum(e["qx"] * fr.loc[k][0] for k, e in enumerate(elems))
    Fy = sum(l[2] for l in nodal) - sum(e["qy"] * fr.loc[k][0] for k, e in enumerate(elems))
    Rx = sum(fr.R[3 * nd] for nd, rr in sup.items() if rr[0])
    Ry = sum(fr.R[3 * nd + 1] for nd, rr in sup.items() if rr[1])
    r.check("ΣX", f"ΣRx + ΣFx = {fmt(Rx + Fx, 4)} kN ≈ 0", abs(Rx + Fx) < 1e-5 * max(abs(Fx), abs(Fy), 1))
    r.check("ΣY", f"ΣRy + ΣFy = {fmt(Ry + Fy, 4)} kN ≈ 0", abs(Ry + Fy) < 1e-5 * max(abs(Fx), abs(Fy), 1))
    Mmax = max(max(abs(a), abs(b)) for a, b in r.results["M_ends"])
    r.conclude(f"Giải xong khung {len(elems)} thanh, {len(fr.free)} ẩn chuyển vị. |M| đầu thanh lớn nhất = {fmt(Mmax)} kN·m.")
    r.conclude(f"Chuyển vị ngang lớn nhất tại nút: {fmt(max(abs(fr.U[3 * i]) for i in range(len(nodes))) * 1000)} mm.")
    return r.finish()


@problem("ckc_frame_force", "ckc", "Khung siêu tĩnh: phương pháp lực",
         "Bỏ liên kết thừa ở gối để có hệ cơ bản, vẽ biểu đồ đơn vị, tính δ, Δ, giải hệ phương trình chính tắc, cộng biểu đồ. Đối chiếu với phương pháp chuyển vị.",
         _frame_fields + [tbl("red", "Liên kết thừa bỏ đi", [("n", "Nút gối"), ("c", "Thành phần", ["ngang", "đứng", "xoay"])], [dict(n=4, c="ngang"), dict(n=4, c="đứng"), dict(n=4, c="xoay")], help="Số dòng phải bằng bậc siêu tĩnh")])
def frame_force(p):
    r = Report("Khung siêu tĩnh: phương pháp lực", MOD)
    nodes, elems, sup, nodal = _build_frame(p)
    fr = Frame(nodes, elems, sup, nodal)
    try:
        fr.solve()
    except ValueError as e:
        raise InputError(str(e))
    rrr = sum(sum(v) for v in sup.values())
    hinges = sum(int(e["rel_i"]) + int(e["rel_j"]) for e in elems)
    deg = 3 * len(elems) + rrr - 3 * len(nodes) - hinges
    r.h("Số liệu đề bài")
    r.fig(draw_frame(fr), "Sơ đồ khung")
    r.h("Bước 1. Bậc siêu tĩnh")
    r.calc("n", "3·t + r − 3·k − h", f"3·{len(elems)} + {rrr} − 3·{len(nodes)} − {hinges}", deg, note="t: số thanh; r: số liên kết gối; k: số nút; h: số khớp đầu thanh", key="n")
    if deg <= 0:
        raise InputError(f"Hệ có bậc siêu tĩnh n = {deg}, không cần phương pháp lực.")
    if len(p["red"]) != deg:
        raise InputError(f"Bậc siêu tĩnh là {deg} nhưng bảng Liên kết thừa có {len(p['red'])} dòng. Hãy chọn đúng {deg} liên kết.")
    r.h("Bước 2. Hệ cơ bản")
    comps = {"ngang": 0, "đứng": 1, "xoay": 2}
    sup0 = {k: list(v) for k, v in sup.items()}
    red = []
    for q in p["red"]:
        nd = int(q["n"]) - 1
        c = comps.get(q["c"])
        if nd not in sup0 or not sup0[nd][c]:
            raise InputError(f"Nút {nd + 1} không có liên kết «{q['c']}» để bỏ.")
        sup0[nd][c] = 0
        red.append((nd, c))
    sup0 = {k: tuple(v) for k, v in sup0.items()}
    r.p("Bỏ các liên kết thừa, thay bằng các ẩn lực $X_i$: " + "; ".join(f"X{i + 1} = {['lực ngang', 'lực đứng', 'mômen'][c]} tại nút {nd + 1}" for i, (nd, c) in enumerate(red)) + ". Chiều dương của ẩn: lực hướng sang phải, lực hướng lên, mômen ngược chiều kim đồng hồ.")
    try:
        fp = Frame(nodes, elems, sup0, nodal).solve()
        units = []
        for (nd, c) in red:
            load = [nd, 0.0, 0.0, 0.0]
            load[c + 1] = 1.0
            e0 = [dict(e, qx=0.0, qy=0.0) for e in elems]
            units.append(Frame(nodes, e0, sup0, [tuple(load)]).solve())
    except ValueError:
        raise InputError("Hệ cơ bản biến hình. Hãy chọn tập liên kết thừa khác.")
    r.fig(draw_frame(Frame(nodes, elems, sup0, nodal), loads=True), "Hệ cơ bản chịu tải trọng")
    r.h("Bước 3. Biểu đồ mômen đơn vị và do tải")
    for i, u in enumerate(units):
        r.fig(draw_frame_diagram(u, "M", f"M{i + 1} khi X{i + 1}=1", "kN·m"), f"Biểu đồ đơn vị M{i + 1}", cad=i < 4, width_cm=9.5)
    r.fig(draw_frame_diagram(fp, "M", "Mp do tải", "kN·m"), "Biểu đồ mômen do tải trên hệ cơ bản Mp", width_cm=9.5)
    r.h("Bước 4. Hệ số và số hạng tự do")
    r.f("δ_ij = Σ∫[[M_i·M_j // EI]]dx;  Δ_ip = Σ∫[[M_i·M_p // EI]]dx")
    r.p("Tích phân tính theo công thức Simpson trên từng thanh, chính xác với biểu đồ bậc nhất nhân biểu đồ bậc hai (tương đương nhân biểu đồ Vêrêsaghin).")
    n = deg
    D = np.zeros((n, n))
    B = np.zeros(n)
    m = len(elems)
    for i in range(n):
        for j in range(n):
            D[i, j] = sum(units[i].bending_integrate(units[j], k) for k in range(m))
        B[i] = sum(units[i].bending_integrate(fp, k) for k in range(m))
    # ví dụ chi tiết một số hạng
    rows = []
    for k in range(m):
        L = fp.loc[k][0]
        a = units[0].forces(k, 0)[2]
        c = units[0].forces(k, L / 2)[2]
        b = units[0].forces(k, L)[2]
        rows.append([k + 1, L, a, c, b, elems[k]["EI"], units[0].bending_integrate(units[0], k)])
    r.table(["Thanh", "L", "M₁ đầu i", "M₁ giữa", "M₁ đầu j", "EI", "∫M₁²/EI"], rows, "Ví dụ tính δ₁₁ theo từng thanh: δ = L/(6EI)·(a² + 4c² + b²).")
    r.table(["δ"] + [f"X{j + 1}" for j in range(n)] + ["Δip"], [[f"Hàng {i + 1}"] + [f"{v:.5g}".replace(".", ",") for v in D[i]] + [f"{B[i]:.5g}".replace(".", ",")] for i in range(n)], "Ma trận hệ số δij và số hạng tự do Δip.")
    r.h("Bước 5. Hệ phương trình chính tắc")
    # gối bỏ đi có thành phần lực: chiều X theo chiều tải đơn vị
    for i in range(n):
        r.f(" + ".join(f"{D[i, j]:.5g}·X_{j + 1}".replace(".", ",") for j in range(n)) + f" + ({B[i]:.5g})".replace(".", ",") + " = 0")
    X = np.linalg.solve(D, -B)
    for i in range(n):
        r.calc(f"X_{i + 1}", None, None, float(X[i]), ["kN", "kN", "kN·m"][red[i][1]], key=f"X{i + 1}")
    r.h("Bước 6. Biểu đồ mômen cuối cùng")
    r.f("M = M_p + " + " + ".join(f"X_{i + 1}·M_{i + 1}" for i in range(n)))
    # lắp lại trực tiếp từ phương pháp chuyển vị đã giải
    rows = []
    maxdiff = 0
    for k in range(m):
        L = fp.loc[k][0]
        vals = []
        for t in (0, L):
            Mfinal = fp.forces(k, t)[2] + sum(X[i] * units[i].forces(k, t)[2] for i in range(n))
            Mfem = fr.forces(k, t)[2]
            maxdiff = max(maxdiff, abs(Mfinal - Mfem))
            vals += [Mfinal, Mfem]
        rows.append([k + 1, *vals])
    r.table(["Thanh", "Mᵢ PP lực", "Mᵢ PP chuyển vị", "Mⱼ PP lực", "Mⱼ PP chuyển vị"], rows, "Đối chiếu hai phương pháp (kN·m).")
    r.results["maxdiff"] = maxdiff
    r.results["M_ends"] = [(rows[k][1], rows[k][3]) for k in range(m)]
    Mref = max(max(abs(a[1]), abs(a[3])) for a in rows)
    r.check("Hai phương pháp khớp nhau", f"chênh lệch lớn nhất = {fmt(maxdiff, 3)} kN·m (≤ 1% của {fmt(Mref)})", maxdiff <= 0.01 * max(Mref, 1e-9))
    r.fig(draw_frame_diagram(fr, "M", "Biểu đồ M cuối cùng", "kN·m"), "Biểu đồ mômen cuối cùng")
    r.fig(draw_frame_diagram(fr, "V", "Biểu đồ Q", "kN"), "Biểu đồ lực cắt Q")
    r.fig(draw_frame_diagram(fr, "N", "Biểu đồ N", "kN"), "Biểu đồ lực dọc N")
    r.conclude("Ẩn số: " + "; ".join(f"X{i + 1} = {fmt(float(X[i]))}" for i in range(n)) + ".")
    r.conclude(f"Biểu đồ cuối cùng khớp với phương pháp chuyển vị, sai lệch {fmt(maxdiff, 3)} kN·m.")
    return r.finish()


# ---------- DÀN ----------
@problem("ckc_truss", "ckc", "Dàn phẳng tĩnh định: tách nút",
         "Kiểm tra cấu tạo, phản lực, phương trình cân bằng từng nút, lực dọc các thanh, biểu đồ trên sơ đồ.",
         [tbl("nodes", "Nút", [("x", "x (m)"), ("y", "y (m)")], [dict(x=0, y=0), dict(x=3, y=0), dict(x=6, y=0), dict(x=9, y=0), dict(x=3, y=2.5), dict(x=6, y=2.5)], min_rows=3),
          tbl("bars", "Thanh", [("i", "Nút i"), ("j", "Nút j")], [dict(i=1, j=2), dict(i=2, j=3), dict(i=3, j=4), dict(i=5, j=6), dict(i=1, j=5), dict(i=2, j=5), dict(i=3, j=6), dict(i=4, j=6), dict(i=3, j=5)], min_rows=3),
          tbl("sup", "Gối", [("n", "Nút"), ("ux", "Giữ ngang (1/0)"), ("uy", "Giữ đứng (1/0)")], [dict(n=1, ux=1, uy=1), dict(n=4, ux=0, uy=1)]),
          tbl("load", "Tải tại nút", [("n", "Nút"), ("fx", "Fx phải + (kN)"), ("fy", "Fy lên + (kN)")], [dict(n=2, fx=0, fy=-20), dict(n=3, fx=0, fy=-20), dict(n=5, fx=10, fy=-10)], min_rows=0)])
def truss(p):
    r = Report("Dàn phẳng tĩnh định: tách nút", MOD)
    nodes = [(q["x"], q["y"]) for q in p["nodes"]]
    N = len(nodes)
    bars = []
    for q in p["bars"]:
        i, j = int(q["i"]) - 1, int(q["j"]) - 1
        if not (0 <= i < N and 0 <= j < N) or i == j:
            raise InputError("Số nút của thanh không hợp lệ.")
        bars.append((i, j))
    sup = []
    for s in p["sup"]:
        k = int(s["n"]) - 1
        if not 0 <= k < N:
            raise InputError("Gối đặt tại nút không tồn tại.")
        if s["ux"]:
            sup.append((k, 0))
        if s["uy"]:
            sup.append((k, 1))
    B = len(bars)
    R = len(sup)
    r.h("Bước 1. Kiểm tra cấu tạo")
    r.calc("2k", None, f"2·{N}", 2 * N, note="k: số nút")
    r.calc("t + r", None, f"{B} + {R}", B + R, note="t: số thanh; r: số liên kết gối")
    if 2 * N != B + R:
        raise InputError(f"Dàn không tĩnh định: 2k = {2 * N} nhưng t + r = {B + R}. Cần t + r = 2k.")
    r.p("Thoả mãn điều kiện cần 2k = t + r: dàn có số phương trình bằng số ẩn.")
    A = np.zeros((2 * N, 2 * N))
    F = np.zeros(2 * N)
    for k, (i, j) in enumerate(bars):
        dx, dy = nodes[j][0] - nodes[i][0], nodes[j][1] - nodes[i][1]
        L = math.hypot(dx, dy)
        if L == 0:
            raise InputError("Thanh có chiều dài bằng 0.")
        c, s = dx / L, dy / L
        A[2 * i, k] += c
        A[2 * i + 1, k] += s
        A[2 * j, k] -= c
        A[2 * j + 1, k] -= s
    for m, (nd, d) in enumerate(sup):
        A[2 * nd + d, B + m] = 1
    for l in p["load"]:
        k = int(l["n"]) - 1
        if not 0 <= k < N:
            raise InputError("Tải đặt tại nút không tồn tại.")
        F[2 * k] -= l["fx"]
        F[2 * k + 1] -= l["fy"]
    if np.linalg.matrix_rank(A, tol=1e-8) < 2 * N:
        raise InputError("Dàn biến hình: ma trận cân bằng suy biến, kiểm tra lại cách bố trí thanh hoặc gối.")
    sol = np.linalg.solve(A, F)
    r.h("Số liệu và sơ đồ")
    th = max(max(n[0] for n in nodes) - min(n[0] for n in nodes), max(n[1] for n in nodes) - min(n[1] for n in nodes)) * 1000 / 40
    def base():
        d = Drawing(th, "dan")
        for (i, j) in bars:
            d.line(nodes[i][0] * 1000, nodes[i][1] * 1000, nodes[j][0] * 1000, nodes[j][1] * 1000, "KET_CAU")
        for i, n in enumerate(nodes):
            d.circle(n[0] * 1000, n[1] * 1000, th * .18, "KET_CAU")
        return d
    d0 = base()
    for k, (i, j) in enumerate(bars):
        d0.text((nodes[i][0] + nodes[j][0]) * 500, (nodes[i][1] + nodes[j][1]) * 500 + th * .5, f"({k + 1})", th * .75, "CHU")
    for i, n in enumerate(nodes):
        d0.text(n[0] * 1000 - th * .6, n[1] * 1000 + th * .8, str(i + 1), th * .9, "KY_HIEU")
    su = {}
    for nd, c in sup:
        su.setdefault(nd, set()).add(c)
    for nd, cs in su.items():
        X, Y = nodes[nd][0] * 1000, nodes[nd][1] * 1000
        (d0.pin if len(cs) == 2 else d0.roller)(X, Y - th * .2)
    for l in p["load"]:
        k = int(l["n"]) - 1
        X, Y = nodes[k][0] * 1000, nodes[k][1] * 1000
        if l["fy"]:
            sg = 1 if l["fy"] > 0 else -1
            d0.arrow(X, Y - sg * th * 3.6, X, Y - sg * th * .3)
            d0.text(X + th * .3, Y - sg * th * 3.2, f"{fmt(abs(l['fy']))} kN", th * .9, "TAI_TRONG", ha="left")
        if l["fx"]:
            sg = 1 if l["fx"] > 0 else -1
            d0.arrow(X - sg * th * 3.6, Y, X - sg * th * .3, Y)
            d0.text(X - sg * th * 2.2, Y + th * .7, f"{fmt(abs(l['fx']))} kN", th * .9, "TAI_TRONG")
    r.fig(d0, "Sơ đồ dàn")
    r.h("Bước 2. Phản lực")
    rows = []
    for m, (nd, c) in enumerate(sup):
        rows.append([nd + 1, "ngang (X)" if c == 0 else "đứng (Y)", float(sol[B + m])])
        r.results[f"R{nd + 1}{'xy'[c]}"] = float(sol[B + m])
    r.table(["Nút", "Thành phần", "Phản lực (kN)"], rows, "Dương theo chiều dương trục toạ độ.")
    r.h("Bước 3. Phương trình cân bằng từng nút")
    r.p("Giả thiết lực dọc các thanh đều kéo (hướng ra khỏi nút). $N_k$ là lực trong thanh k; $c, s$ là cos, sin góc nghiêng so với trục x.")
    for nd in range(N):
        tx, ty = [], []
        for k, (i, j) in enumerate(bars):
            if nd in (i, j):
                dx, dy = nodes[j][0] - nodes[i][0], nodes[j][1] - nodes[i][1]
                L = math.hypot(dx, dy)
                sg = 1 if nd == i else -1
                cx, cy = sg * dx / L, sg * dy / L
                if abs(cx) > 1e-9:
                    tx.append(f"{fmt(cx)}·N_{k + 1}".replace("+-", "-"))
                if abs(cy) > 1e-9:
                    ty.append(f"{fmt(cy)}·N_{k + 1}")
        fx = -F[2 * nd]
        fy = -F[2 * nd + 1]
        for m, (s_nd, c) in enumerate(sup):
            if s_nd == nd:
                (tx if c == 0 else ty).append(f"R_{'xy'[c]}")
        r.h(f"Nút {nd + 1}", 3)
        r.f("ΣX: " + (" + ".join(tx) or "0") + (f" + ({fmt(fx)})" if fx else "") + " = 0")
        r.f("ΣY: " + (" + ".join(ty) or "0") + (f" + ({fmt(fy)})" if fy else "") + " = 0")
    r.h("Bước 4. Lực dọc các thanh")
    rows = []
    for k, (i, j) in enumerate(bars):
        v = float(sol[k])
        r.results[f"N{k + 1}"] = v
        L = math.hypot(nodes[j][0] - nodes[i][0], nodes[j][1] - nodes[i][1])
        rows.append([k + 1, f"{i + 1}-{j + 1}", L, v, "kéo" if v > 1e-9 else ("nén" if v < -1e-9 else "không lực")])
    r.table(["Thanh", "Nút", "L (m)", "N (kN)", "Trạng thái"], rows)
    d1 = base()
    for k, (i, j) in enumerate(bars):
        v = float(sol[k])
        a = math.degrees(math.atan2(nodes[j][1] - nodes[i][1], nodes[j][0] - nodes[i][0]))
        if a > 90 or a <= -90:
            a -= 180 if a > 0 else -180
        rr = math.radians(a)
        d1.text((nodes[i][0] + nodes[j][0]) * 500 - math.sin(rr) * th * .7, (nodes[i][1] + nodes[j][1]) * 500 + math.cos(rr) * th * .7, fmt(v), th * .85, "THEP" if v < 0 else "KY_HIEU", rot=a)
    r.fig(d1, "Lực dọc trên các thanh (kN), kéo dương, nén âm")
    # kiểm tra
    res = A @ sol - F
    r.check("Cân bằng toàn bộ nút", f"sai số lớn nhất = {fmt(float(np.abs(res).max()), 6)} kN ≈ 0", np.abs(res).max() < 1e-6)
    t = float(sol[:B].max())
    c = float(sol[:B].min())
    r.conclude(f"Thanh kéo lớn nhất: {fmt(t)} kN; thanh nén lớn nhất: {fmt(c)} kN.")
    return r.finish()


# ---------- VÒM ----------
@problem("ckc_arch", "ckc", "Vòm ba khớp parabol",
         "Phản lực, lực xô ngang, nội lực M, Q, N tại các mặt cắt, biểu đồ. Khớp đỉnh ở giữa nhịp hoặc lệch.",
         [num("L", "Nhịp L", "m", 20, 0), num("f", "Mũi tên f", "m", 5, 0), num("xc", "Vị trí khớp C", "m", 10, 0),
          tbl("q", "Tải đều trên hình chiếu", [("a", "từ x (m)"), ("b", "đến x (m)"), ("q", "q (kN/m)")], [dict(a=0, b=10, q=10)], min_rows=0),
          tbl("P", "Lực tập trung", [("x", "x (m)"), ("P", "P (kN)")], [dict(x=15, P=40)], min_rows=0),
          num("ns", "Số đoạn chia", "", 10, 4, 40)])
def arch(p):
    r = Report("Vòm ba khớp parabol", MOD)
    L, f, xc = p["L"], p["f"], p["xc"]
    if not 0 < xc < L:
        raise InputError("Khớp C phải nằm trong nhịp.")
    if f <= 0:
        raise InputError("Mũi tên f phải dương.")
    ycf = 4 * f * xc * (L - xc) / L ** 2
    r.given([("Nhịp", "L", L, "m"), ("Mũi tên", "f", f, "m"), ("Vị trí khớp đỉnh", "xC", xc, "m")])
    r.f("Trục vòm: y(x) = [[4f·x(L − x) // L^2]];  tanα = y'(x) = [[4f(L − 2x) // L^2]]")
    r.h("Bước 1. Dầm tương đương và phản lực đứng")
    b = BeamModel(L, [(0, "pin"), (L, "roller")], [], [(q["x"], q["P"]) for q in p["P"]], [], [(q["a"], q["b"], q["q"], q["q"]) for q in p["q"]])
    VA = b.reactions[0][3]
    VB = b.reactions[1][3]
    r.calc("V_A = V_A^0", None, None, VA, "kN", key="VA")
    r.calc("V_B = V_B^0", None, None, VB, "kN", key="VB")
    r.h("Bước 2. Lực xô ngang")
    pc = b.piece_at(xc)
    M0c = float(np.polynomial.polynomial.polyval(xc, pc["M"]))
    H = M0c / ycf
    r.calc("M_C^0", None, None, M0c, "kN·m", note="Mômen của dầm tương đương tại C")
    r.calc("y_C", None, None, ycf, "m")
    r.calc("H", "[[M_C^0 // y_C]]", f"[[{fmt(M0c)} // {fmt(ycf)}]]", H, "kN", key="H")
    r.h("Bước 3. Nội lực tại các mặt cắt")
    r.f("M = M^0 − H·y;  Q = Q^0·cosα − H·sinα;  N = −(Q^0·sinα + H·cosα)")
    n = int(p["ns"])
    rows = []
    xs = sorted(set(list(np.linspace(0, L, n + 1)) + [xc]))
    S = []
    for x in xs:
        pc = b.piece_at(x)
        M0 = float(np.polynomial.polynomial.polyval(x, pc["M"]))
        Q0 = float(np.polynomial.polynomial.polyval(x, pc["Q"]))
        y = 4 * f * x * (L - x) / L ** 2
        ta = 4 * f * (L - 2 * x) / L ** 2
        al = math.atan(ta)
        M = M0 - H * y
        Q = Q0 * math.cos(al) - H * math.sin(al)
        N_ = -(Q0 * math.sin(al) + H * math.cos(al))
        S.append((x, y, al, M, Q, N_))
        rows.append([fmt(x), fmt(y), fmt(math.degrees(al)), fmt(M0), fmt(Q0), fmt(M), fmt(Q), fmt(N_)])
    r.table(["x (m)", "y (m)", "α (°)", "M⁰", "Q⁰", "M (kN·m)", "Q (kN)", "N (kN)"], rows)
    r.results["M_c"] = [s[3] for s in S if abs(s[0] - xc) < 1e-9][0]
    th = L * 1000 / 42
    d = Drawing(th, "vom")
    ts = np.linspace(0, L, 80)
    d.pline([(t * 1000, 4 * f * t * (L - t) / L ** 2 * 1000) for t in ts], "KET_CAU")
    d.pin(0, 0)
    d.pin(L * 1000, 0)
    d.hinge(xc * 1000, ycf * 1000)
    for q in p["q"]:
        a, c = q["a"], q["b"]
        top = f * 1000 + th * 3.5
        d.line(a * 1000, top, c * 1000, top, "TAI_TRONG")
        for t in np.linspace(a, c, 7):
            d.arrow(t * 1000, top, t * 1000, top - th * 1.8)
        d.text((a + c) * 500, top + th * .9, f"q={fmt(q['q'])} kN/m", th, "TAI_TRONG")
    for q in p["P"]:
        y = 4 * f * q["x"] * (L - q["x"]) / L ** 2 * 1000
        d.arrow(q["x"] * 1000, y + th * 4, q["x"] * 1000, y + th * .2)
        d.text(q["x"] * 1000 + th * .3, y + th * 3.5, f"P={fmt(q['P'])}", th, "TAI_TRONG", ha="left")
    d.dim(0, -th * 3.5, L * 1000, -th * 3.5, 0, f"L={fmt(L)} m")
    mm = max(abs(s[3]) for s in S) or 1
    sc = L * 1000 * .1 / mm
    y0 = -th * 6 - L * 1000 * .1
    diagram(d, [s[0] * 1000 for s in S], [s[3] for s in S], 0, y0, sc, flip=True)
    d.text(-th * 2.3, y0, "M (kN·m)", th)
    im = max(S, key=lambda s: abs(s[3]))
    d.text(im[0] * 1000, y0 - im[3] * sc + np.sign(-im[3]) * th * .9, fmt(im[3]), th)
    vm = max(abs(s[5]) for s in S) or 1
    y1 = y0 - L * 1000 * .1 * 2 - th * 3
    sc2 = L * 1000 * .1 / vm
    diagram(d, [s[0] * 1000 for s in S], [s[5] for s in S], 0, y1, sc2)
    d.text(-th * 2.3, y1, "N (kN)", th)
    i2 = max(S, key=lambda s: abs(s[5]))
    d.text(i2[0] * 1000, y1 + i2[5] * sc2 - th, fmt(i2[5]), th)
    r.fig(d, "Sơ đồ vòm và biểu đồ M, N theo hình chiếu")
    r.check("Mômen tại khớp C bằng 0", f"M_C = {fmt(r.results['M_c'], 6)} kN·m ≈ 0", abs(r.results["M_c"]) < 1e-6)
    r.results.update(Mmax=im[3], Nmax=i2[5])
    r.conclude(f"VA = {fmt(VA)} kN; VB = {fmt(VB)} kN; H = {fmt(H)} kN.")
    r.conclude(f"|M|max = {fmt(abs(im[3]))} kN·m tại x = {fmt(im[0])} m; |N|max = {fmt(abs(i2[5]))} kN.")
    return r.finish()


# ---------- ĐƯỜNG ẢNH HƯỞNG ----------
@problem("ckc_il", "ckc", "Đường ảnh hưởng dầm đơn giản, đoàn tải di động",
         "Tung độ đường ảnh hưởng RA, RB, Mk, Qk, vị trí bất lợi của đoàn lực tập trung, tải đều rải.",
         [num("L", "Nhịp L", "m", 12, 0), num("o1", "Đầu thừa trái", "m", 0, 0), num("o2", "Đầu thừa phải", "m", 2, 0), num("xk", "Mặt cắt k cách gối A", "m", 4),
          tbl("tr", "Đoàn lực tập trung (từ đầu đoàn)", [("P", "P (kN)"), ("d", "Cách lực trước (m)")], [dict(P=120, d=0), dict(P=120, d=1.5), dict(P=80, d=3)], min_rows=0),
          num("q", "Tải đều rải bất kỳ", "kN/m", 10, 0)])
def il(p):
    r = Report("Đường ảnh hưởng dầm đơn giản", MOD)
    L, o1, o2, xk = p["L"], p["o1"], p["o2"], p["xk"]
    if L <= 0 or not 0 < xk < L:
        raise InputError("Mặt cắt k phải nằm trong nhịp (0 < xk < L).")
    r.given([("Nhịp", "L", L, "m"), ("Đầu thừa trái", "a₁", o1, "m"), ("Đầu thừa phải", "a₂", o2, "m"), ("Mặt cắt k", "xk", xk, "m"), ("Tải đều", "q", p["q"], "kN/m")])
    r.h("Bước 1. Biểu thức đường ảnh hưởng")
    r.p("Cho lực đơn vị P = 1 chạy trên dầm, x là toạ độ của lực tính từ gối A (x âm trên đầu thừa trái).")
    r.f("đ.a.h R_A: y = [[L − x // L]];  đ.a.h R_B: y = [[x // L]]")
    r.f("đ.a.h M_k: y = [[x(L − x_k) // L]] khi x ≤ x_k;  y = [[x_k(L − x) // L]] khi x ≥ x_k")
    r.f("đ.a.h Q_k: y = −[[x // L]] khi x < x_k;  y = [[L − x // L]] khi x > x_k")
    lines = {
        "RA": lambda x: (L - x) / L,
        "RB": lambda x: x / L,
        "Mk": lambda x: x * (L - xk) / L if x <= xk else xk * (L - x) / L,
        "Qk": lambda x: -x / L if x < xk else (L - x) / L,
    }
    keypts = sorted(set([-o1, 0.0, xk, L, L + o2]))
    rows = []
    for x in keypts:
        rows.append([x, lines["RA"](x), lines["RB"](x), lines["Mk"](x), lines["Qk"](x) if x != xk else f"{fmt(lines['Qk'](xk - 1e-9))} / {fmt(lines['Qk'](xk + 1e-9))}"])
    r.table(["x (m)", "y(RA)", "y(RB)", "y(Mk) (m)", "y(Qk)"], rows, "Tung độ tại các điểm đặc trưng.")
    tr = p["tr"]
    offs = []
    s = 0
    for t in tr:
        s += t["d"]
        offs.append(s)
    results = {}

    def envelope(fn):
        """Tìm (max, min) của tổng P·y với mọi vị trí đoàn tải; đặt một lực lên đỉnh."""
        best_p, best_n = (0.0, None), (0.0, None)
        span = offs[-1] if offs else 0.0
        for off_set in (offs, [span - o for o in offs]):  # thử cả hai chiều chạy của đoàn tải
            cands = []
            for vert in keypts:
                for off in off_set:
                    cands += [vert - off, vert - off - 1e-6, vert - off + 1e-6]
            for c in cands:
                v = 0.0
                for t, off in zip(tr, off_set):
                    x = c + off
                    if -o1 - 1e-9 <= x <= L + o2 + 1e-9:
                        v += t["P"] * fn(x)
                if v > best_p[0]:
                    best_p = (v, c)
                if v < best_n[0]:
                    best_n = (v, c)
        return best_p, best_n

    def area(fn):
        pts = sorted(set(keypts + [xk - 1e-9, xk + 1e-9]))
        # chia tại điểm đổi dấu
        Ap = An = 0.0
        for a, b in zip(pts[:-1], pts[1:]):
            ya, yb = fn(a + 1e-12), fn(b - 1e-12)
            h = b - a
            if ya * yb >= 0:
                A = (ya + yb) / 2 * h
                if A >= 0:
                    Ap += A
                else:
                    An += A
            else:
                z = h * abs(ya) / (abs(ya) + abs(yb))
                A1, A2 = ya * z / 2, yb * (h - z) / 2
                for A in (A1, A2):
                    if A >= 0:
                        Ap += A
                    else:
                        An += A
        return Ap, An

    r.h("Bước 2. Giá trị bất lợi do đoàn lực và tải rải")
    r.f("S = ΣP_i·y_i + q·Ω,  Ω: diện tích phần đường ảnh hưởng cùng dấu")
    rows = []
    units = {"RA": "kN", "RB": "kN", "Mk": "kN·m", "Qk": "kN"}
    for key, fn in lines.items():
        (pv, pc), (nv, nc) = envelope(fn)
        Ap, An = area(fn)
        smax = pv + p["q"] * Ap
        smin = nv + p["q"] * An
        results[key] = (smax, smin)
        r.results[key + "max"] = smax
        r.results[key + "min"] = smin
        rows.append([key, units[key], pv, Ap, smax, nv, An, smin])
    r.table(["Đại lượng", "Đơn vị", "ΣPy lớn nhất", "Ω dương", "Smax", "ΣPy nhỏ nhất", "Ω âm", "Smin"], rows, "Đoàn lực được thử đặt với lần lượt từng lực tại mỗi đỉnh đường ảnh hưởng.")
    Lt = (L + o1 + o2) * 1000
    th = Lt / 45
    d = Drawing(th, "dah")
    d.line(-o1 * 1000, 0, (L + o2) * 1000, 0, "KET_CAU")
    d.pin(0, 0)
    d.roller(L * 1000, 0)
    d.text(0, -th * 3, "A", th)
    d.text(L * 1000, -th * 3, "B", th)
    d.line(xk * 1000, th * 1.2, xk * 1000, -th * 1.2, "KY_HIEU", True)
    d.text(xk * 1000, th * 1.9, "k", th, "KY_HIEU")
    y = -th * 6
    for key, fn in lines.items():
        pts_ = sorted(set(keypts + [xk - 1e-6, xk + 1e-6]))
        v = [fn(t) for t in pts_]
        vm = max(max(abs(q) for q in v), 1e-9)
        sc = th * 3.2 / vm
        diagram(d, [t * 1000 for t in pts_], v, 0, y, sc, n_hatch=40, flip=True)
        d.text(-(o1 * 1000) - th * 3.0, y, f"đ.a.h {key}", th)
        for t, q in zip(pts_, v):
            if abs(q) > 1e-6:
                d.text(t * 1000, y - q * sc + (-th * .8 if q > 0 else th * .8), fmt(q), th * .8)
        y -= th * 9.5
    r.fig(d, "Các đường ảnh hưởng (tung độ dương vẽ phía dưới đường chuẩn)")
    r.conclude(f"Mk: max {fmt(results['Mk'][0])} / min {fmt(results['Mk'][1])} kN·m. Qk: max {fmt(results['Qk'][0])} / min {fmt(results['Qk'][1])} kN.")
    r.conclude(f"RA: max {fmt(results['RA'][0])} kN. RB: max {fmt(results['RB'][0])} kN.")
    return r.finish()

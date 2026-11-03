"""Sức bền vật liệu."""
from __future__ import annotations
import math
import numpy as np
from .registry import problem, num, sel, tbl, InputError
from ..core.report import Report, fmt
from ..core.drawing import Drawing, diagram
from ..core.beam import BeamModel, report_beam_solution

MOD = "Sức bền vật liệu"


# ---------- ĐẶC TRƯNG HÌNH HỌC ----------
def _shape_rects(shape, p):
    """Trả về danh sách (b,h,xc,yc,dấu); toạ độ tính từ đáy, đối xứng trục giữa."""
    b, h, tw, tf, bf2, tf2 = p["b"], p["h"], p["tw"], p["tf"], p["b2"], p["t2"]
    if shape == "rect":
        return [(b, h, 0, h / 2, 1)]
    if shape == "I":
        hw = h - tf - tf2
        if hw <= 0:
            raise InputError("Chiều cao bụng hw ≤ 0: kiểm tra h, tf, t2.")
        return [(bf2, tf2, 0, tf2 / 2, 1), (tw, hw, 0, tf2 + hw / 2, 1), (b, tf, 0, h - tf / 2, 1)]
    if shape == "T":
        hw = h - tf
        return [(tw, hw, 0, hw / 2, 1), (b, tf, 0, h - tf / 2, 1)]
    if shape == "box":
        if b <= 2 * tw or h <= 2 * tf:
            raise InputError("Tiết diện hộp: bề dày quá lớn so với kích thước.")
        return [(b, h, 0, h / 2, 1), (b - 2 * tw, h - 2 * tf, 0, h / 2, -1)]
    raise InputError("Dạng tiết diện chưa hỗ trợ")


def section_props(parts):
    A = sum(s * b * h for b, h, x, y, s in parts)
    if A <= 0:
        raise InputError("Diện tích tiết diện ≤ 0.")
    Sx = sum(s * b * h * y for b, h, x, y, s in parts)
    Sy = sum(s * b * h * x for b, h, x, y, s in parts)
    yc, xc = Sx / A, Sy / A
    Ix = sum(s * (b * h ** 3 / 12 + b * h * (y - yc) ** 2) for b, h, x, y, s in parts)
    Iy = sum(s * (h * b ** 3 / 12 + b * h * (x - xc) ** 2) for b, h, x, y, s in parts)
    Ixy = sum(s * (b * h * (x - xc) * (y - yc)) for b, h, x, y, s in parts)
    return dict(A=A, Sx=Sx, Sy=Sy, xc=xc, yc=yc, Ix=Ix, Iy=Iy, Ixy=Ixy)


@problem("sbvl_section", "sbvl", "Đặc trưng hình học tiết diện",
         "Trọng tâm, mômen quán tính, mômen chống uốn, bán kính quán tính của tiết diện chữ nhật, tròn, ống, chữ I, chữ T, hộp hoặc tổ hợp hình chữ nhật.",
         [sel("shape", "Dạng tiết diện", [("I", "Chữ I"), ("T", "Chữ T"), ("rect", "Chữ nhật"), ("box", "Hộp"), ("circle", "Tròn đặc"), ("ring", "Ống tròn"), ("custom", "Tổ hợp hình chữ nhật")], "I"),
          num("b", "Bề rộng b (cánh trên)", "mm", 200, 0),
          num("h", "Chiều cao h / đường kính D", "mm", 400, 0),
          num("tw", "Bề dày bụng tw", "mm", 8, 0),
          num("tf", "Bề dày cánh trên tf", "mm", 12, 0),
          num("b2", "Bề rộng cánh dưới", "mm", 200, 0, help="Chỉ dùng cho chữ I"),
          num("t2", "Bề dày cánh dưới", "mm", 12, 0, help="Chỉ dùng cho chữ I; với ống tròn là bề dày thành"),
          tbl("parts", "Tổ hợp (chỉ dùng khi chọn Tổ hợp)", [("b", "b (mm)"), ("h", "h (mm)"), ("x", "xc (mm)"), ("y", "yc (mm)"), ("sign", "+1 đặc / −1 lỗ")],
              [dict(b=300, h=20, x=0, y=10, sign=1), dict(b=20, h=300, x=0, y=170, sign=1)], min_rows=0)])
def sec_props(p):
    r = Report("Đặc trưng hình học tiết diện", MOD)
    shape = p["shape"]
    r.h("Số liệu đề bài")
    d = Drawing(max(p["h"], p["b"]) / 18, "tiet_dien")
    if shape in ("circle", "ring"):
        D = p["h"]
        t = p["t2"]
        Di = 0 if shape == "circle" else D - 2 * t
        if Di < 0:
            raise InputError("Bề dày thành quá lớn so với đường kính.")
        r.table(["Đại lượng", "Giá trị (mm)"], [["Đường kính ngoài D", D]] + ([["Đường kính trong d", Di]] if Di else []), widths=[8, 4])
        r.h("Lời giải")
        A = math.pi * (D ** 2 - Di ** 2) / 4
        Ix = math.pi * (D ** 4 - Di ** 4) / 64
        Wx = Ix / (D / 2)
        ii = math.sqrt(Ix / A)
        Ip = 2 * Ix
        r.calc("A", "[[π(D^2 − d^2) // 4]]", f"[[π({fmt(D)}^2 − {fmt(Di)}^2) // 4]]", A, "mm²", key="A")
        r.calc("I_x = I_y", "[[π(D^4 − d^4) // 64]]", f"[[π({fmt(D)}^4 − {fmt(Di)}^4) // 64]]", Ix, "mm⁴", key="Ix")
        r.calc("W_x", "[[I_x // D/2]]", f"[[{fmt(Ix)} // {fmt(D / 2)}]]", Wx, "mm³", key="Wx")
        r.calc("i_x", "√[[I_x/A]]", f"√[[{fmt(Ix)}/{fmt(A)}]]", ii, "mm", key="ix")
        r.calc("I_p", "2·I_x", None, Ip, "mm⁴")
        r.calc("W_p", "[[I_p // D/2]]", None, Ip / (D / 2), "mm³")
        d.circle(0, 0, D / 2, "KET_CAU")
        if Di:
            d.circle(0, 0, Di / 2, "KET_CAU")
        d.line(-D * .65, 0, D * .65, 0, "TRUC", True)
        d.line(0, -D * .65, 0, D * .65, "TRUC", True)
        d.dim(-D / 2, -D / 2, D / 2, -D / 2, -d.th * 1.4, f"D={fmt(D)}")
        r.fig(d, "Tiết diện")
        r.conclude(f"A = {fmt(A)} mm²; Ix = {fmt(Ix)} mm⁴; Wx = {fmt(Wx)} mm³; ix = {fmt(ii)} mm.")
        return r.finish()
    if shape == "custom":
        parts = [(q["b"], q["h"], q["x"], q["y"], 1 if q["sign"] >= 0 else -1) for q in p["parts"]]
        if not parts:
            raise InputError("Chưa nhập hình thành phần nào trong bảng Tổ hợp.")
    else:
        parts = _shape_rects(shape, p)
    s = section_props(parts)
    r.table(["Hình", "b (mm)", "h (mm)", "xc (mm)", "yc (mm)", "Loại"], [[i + 1, a, b, x, y, "đặc" if sg > 0 else "lỗ"] for i, (a, b, x, y, sg) in enumerate(parts)], "Chia tiết diện thành các hình chữ nhật, toạ độ trọng tâm từng hình tính từ gốc O.")
    r.h("Bước 1. Diện tích và trọng tâm")
    rows = []
    for i, (a, b, x, y, sg) in enumerate(parts, 1):
        Ai = sg * a * b
        rows.append([i, Ai, y, Ai * y, x, Ai * x])
    rows.append(["Tổng", s["A"], "", s["Sx"], "", s["Sy"]])
    r.table(["Hình", "Aᵢ (mm²)", "yᵢ (mm)", "Aᵢ·yᵢ (mm³)", "xᵢ (mm)", "Aᵢ·xᵢ (mm³)"], rows)
    r.calc("A", "ΣA_i", None, s["A"], "mm²", key="A")
    r.calc("y_c", "[[ΣA_i·y_i // A]]", f"[[{fmt(s['Sx'])} // {fmt(s['A'])}]]", s["yc"], "mm", key="yc")
    r.calc("x_c", "[[ΣA_i·x_i // A]]", f"[[{fmt(s['Sy'])} // {fmt(s['A'])}]]", s["xc"], "mm", key="xc")
    r.h("Bước 2. Mômen quán tính trung tâm (định lý trục song song)")
    r.f("I_x = Σ([[b_i·h_i^3 // 12]] + A_i·(y_i − y_c)^2);  I_y = Σ([[h_i·b_i^3 // 12]] + A_i·(x_i − x_c)^2)")
    rows = []
    for i, (a, b, x, y, sg) in enumerate(parts, 1):
        rows.append([i, sg * a * b ** 3 / 12, sg * a * b * (y - s["yc"]) ** 2, sg * b * a ** 3 / 12, sg * a * b * (x - s["xc"]) ** 2])
    r.table(["Hình", "I riêng x (mm⁴)", "A·d² x (mm⁴)", "I riêng y (mm⁴)", "A·d² y (mm⁴)"], rows)
    r.calc("I_x", None, None, s["Ix"], "mm⁴", key="Ix")
    r.calc("I_y", None, None, s["Iy"], "mm⁴", key="Iy")
    ymax = max(y + a_h / 2 for a_b, a_h, x, y, sg in parts)
    ymin = min(y - a_h / 2 for a_b, a_h, x, y, sg in parts)
    xmax = max(x + a_b / 2 for a_b, a_h, x, y, sg in parts)
    xmin = min(x - a_b / 2 for a_b, a_h, x, y, sg in parts)
    yt, yb = ymax - s["yc"], s["yc"] - ymin
    r.h("Bước 3. Mômen chống uốn và bán kính quán tính")
    r.calc("W_(x,trên)", "[[I_x // y_t]]", f"[[{fmt(s['Ix'])} // {fmt(yt)}]]", s["Ix"] / yt, "mm³", key="Wxt")
    r.calc("W_(x,dưới)", "[[I_x // y_d]]", f"[[{fmt(s['Ix'])} // {fmt(yb)}]]", s["Ix"] / yb, "mm³", key="Wxb")
    xr = max(xmax - s["xc"], s["xc"] - xmin)
    r.calc("W_y", "[[I_y // x_max]]", f"[[{fmt(s['Iy'])} // {fmt(xr)}]]", s["Iy"] / xr, "mm³", key="Wy")
    ix, iy = math.sqrt(s["Ix"] / s["A"]), math.sqrt(s["Iy"] / s["A"])
    r.calc("i_x", "√[[I_x/A]]", None, ix, "mm", key="ix")
    r.calc("i_y", "√[[I_y/A]]", None, iy, "mm", key="iy")
    if abs(s["Ixy"]) > 1e-6 * max(s["Ix"], s["Iy"]):
        Ic = (s["Ix"] + s["Iy"]) / 2
        Rr = math.hypot((s["Ix"] - s["Iy"]) / 2, s["Ixy"])
        r.calc("I_xy", None, None, s["Ixy"], "mm⁴")
        r.calc("I_max", "[[I_x+I_y // 2]] + √[[([[I_x−I_y // 2]])^2 + I_xy^2]]", None, Ic + Rr, "mm⁴")
        r.calc("I_min", "[[I_x+I_y // 2]] − √[[([[I_x−I_y // 2]])^2 + I_xy^2]]", None, Ic - Rr, "mm⁴")
    for a, b, x, y, sg in parts:
        d.rect(x - a / 2, y - b / 2, a, b, "KET_CAU", dash=sg < 0)
    W = xmax - xmin
    d.line(xmin - W * .2, s["yc"], xmax + W * .2, s["yc"], "TRUC", True)
    d.line(s["xc"], ymin - W * .15, s["xc"], ymax + W * .15, "TRUC", True)
    d.circle(s["xc"], s["yc"], d.th * .25, "KY_HIEU", True)
    d.text(xmax + W * .24, s["yc"], "x", d.th, "CHU", ha="left")
    d.text(s["xc"], ymax + W * .2, "y", d.th, "CHU")
    d.dim(xmin, ymin, xmax, ymin, -d.th * 1.6, fmt(W))
    d.dim(xmax, ymin, xmax, s["yc"], -d.th * 1.6, f"yc={fmt(s['yc'] - ymin)}")
    d.dim(xmax, ymin, xmax, ymax, -d.th * 4.0, fmt(ymax - ymin))
    r.fig(d, "Tiết diện và trục trung tâm")
    r.conclude(f"A = {fmt(s['A'])} mm²; yc = {fmt(s['yc'])} mm; Ix = {fmt(s['Ix'])} mm⁴; Iy = {fmt(s['Iy'])} mm⁴.")
    r.conclude(f"Wx,trên = {fmt(s['Ix'] / yt)} mm³; Wx,dưới = {fmt(s['Ix'] / yb)} mm³; ix = {fmt(ix)} mm; iy = {fmt(iy)} mm.")
    return r.finish()


# ---------- KÉO NÉN ----------
@problem("sbvl_axial", "sbvl", "Thanh bậc chịu kéo nén đúng tâm",
         "Biểu đồ lực dọc, ứng suất, biến dạng từng đoạn, chuyển vị đầu tự do, kiểm tra bền. Thanh ngàm ở đầu trái.",
         [tbl("segs", "Các đoạn tính từ ngàm", [("L", "L (m)"), ("A", "A (cm²)"), ("E", "E (10⁴ kN/cm²)"), ("P", "Lực tại cuối đoạn, hướng ra xa ngàm + (kN)")],
              [dict(L=1, A=20, E=2.1, P=0), dict(L=1, A=20, E=2.1, P=60), dict(L=1.5, A=10, E=2.1, P=-30)]),
          num("allow", "Ứng suất cho phép [σ]", "kN/cm²", 16, 0)])
def axial(p):
    r = Report("Thanh bậc chịu kéo nén đúng tâm", MOD)
    segs = p["segs"]
    for s in segs:
        if s["L"] <= 0 or s["A"] <= 0 or s["E"] <= 0:
            raise InputError("L, A, E của mỗi đoạn phải dương.")
    r.h("Số liệu đề bài")
    r.table(["Đoạn", "L (m)", "A (cm²)", "E (kN/cm²)", "Lực cuối đoạn (kN)"], [[i + 1, s["L"], s["A"], s["E"] * 1e4, s["P"]] for i, s in enumerate(segs)])
    r.p(f"Ứng suất cho phép [σ] = {fmt(p['allow'])} kN/cm². Lực dương hướng ra xa ngàm (gây kéo các đoạn phía trong).")
    r.h("Bước 1. Lực dọc từng đoạn (phương pháp mặt cắt)")
    r.p("Cắt qua đoạn đang xét, giữ phần phía đầu tự do. Lực dọc bằng tổng các lực ngoài đặt từ mặt cắt ra đầu tự do.")
    n = len(segs)
    N = []
    for i in range(n):
        Ni = sum(s["P"] for s in segs[i:])
        N.append(Ni)
        terms = " + ".join(fmt(s["P"]) if s["P"] >= 0 else f"({fmt(s['P'])})" for s in segs[i:])
        r.calc(f"N_{i + 1}", None, terms, Ni, "kN", key=f"N{i + 1}")
    r.calc("V_ngàm", "−N_1", None, -N[0], "kN", note="Phản lực tại ngàm", key="R")
    r.h("Bước 2. Ứng suất")
    r.f("σ_i = [[N_i // A_i]]")
    sig = []
    for i, s in enumerate(segs):
        si = N[i] / s["A"]
        sig.append(si)
        r.calc(f"σ_{i + 1}", None, f"[[{fmt(N[i])} // {fmt(s['A'])}]]", si, "kN/cm²", key=f"s{i + 1}")
    r.h("Bước 3. Biến dạng và chuyển vị")
    r.f("Δl_i = [[N_i·L_i // E_i·A_i]]")
    dl = []
    for i, s in enumerate(segs):
        Li = s["L"] * 100
        Ei = s["E"] * 1e4
        dli = N[i] * Li / (Ei * s["A"])
        dl.append(dli)
        r.calc(f"Δl_{i + 1}", None, f"[[{fmt(N[i])}·{fmt(Li)} // {fmt(Ei)}·{fmt(s['A'])}]]", dli, "cm", d=5)
    tot = sum(dl)
    r.calc("Δ_(tự do)", "ΣΔl_i", None, tot, "cm", key="delta", d=5, note="Dương: đầu tự do dịch ra xa ngàm")
    r.h("Bước 4. Kiểm tra bền")
    sm = max(abs(x) for x in sig)
    r.check("Điều kiện bền", f"|σ|_max = {fmt(sm)} {'≤' if sm <= p['allow'] else '>'} [σ] = {fmt(p['allow'])} kN/cm²", sm <= p["allow"], key="ok")
    Ltot = sum(s["L"] for s in segs) * 1000
    th = Ltot / 42
    d = Drawing(th, "thanh_bac")
    Amax = max(s["A"] for s in segs)
    x = 0
    d.fixed(0, 0, -1, s=th * 3)
    pos = [0]
    for i, s in enumerate(segs):
        hh = th * 2.4 * math.sqrt(s["A"] / Amax)
        Lmm = s["L"] * 1000
        d.rect(x, -hh, Lmm, 2 * hh, "KET_CAU")
        d.text(x + Lmm / 2, 0, f"A={fmt(s['A'])}", th * .8, "CHU")
        x += Lmm
        pos.append(x)
        if s["P"]:
            sgn = 1 if s["P"] > 0 else -1
            d.arrow(x, th * 3.4, x + sgn * th * 4, th * 3.4)
            d.text(x + sgn * th * 2, th * 4.4, f"{fmt(abs(s['P']))} kN", th, "TAI_TRONG")
    for i in range(n):
        d.dim(pos[i], -th * 3.5, pos[i + 1], -th * 3.5, 0, f"{fmt(segs[i]['L'])} m")
    # biểu đồ N
    y0 = -th * 12
    sc = th * 3.5 / max(max(abs(v) for v in N), 1e-9)
    xs, vs = [], []
    for i in range(n):
        xs += [pos[i], pos[i + 1]]
        vs += [N[i], N[i]]
        d.text((pos[i] + pos[i + 1]) / 2, y0 + N[i] * sc + (th * .8 if N[i] >= 0 else -th * .8), fmt(N[i]), th * .9, "CHU")
    diagram(d, xs, vs, 0, y0, sc)
    d.text(-th * 2.5, y0, "N (kN)", th, "CHU")
    y1 = -th * 23
    sc = th * 3.5 / max(sm, 1e-9)
    xs, vs = [], []
    for i in range(n):
        xs += [pos[i], pos[i + 1]]
        vs += [sig[i], sig[i]]
        d.text((pos[i] + pos[i + 1]) / 2, y1 + sig[i] * sc + (th * .8 if sig[i] >= 0 else -th * .8), fmt(sig[i]), th * .9, "CHU")
    diagram(d, xs, vs, 0, y1, sc)
    d.text(-th * 2.5, y1, "σ (kN/cm²)", th, "CHU")
    r.fig(d, "Sơ đồ thanh, biểu đồ lực dọc N và ứng suất σ")
    r.conclude(f"Phản lực ngàm V = {fmt(-N[0])} kN. Ứng suất lớn nhất |σ| = {fmt(sm)} kN/cm². Chuyển vị đầu tự do Δ = {fmt(tot, 5)} cm.")
    r.conclude("Thanh ĐẢM BẢO điều kiện bền." if sm <= p["allow"] else "Thanh KHÔNG đảm bảo điều kiện bền, cần tăng tiết diện.")
    return r.finish()


# ---------- UỐN ----------
_support_opts = [("simple", "Hai gối (khớp cố định + di động)"), ("cant_l", "Công xôn ngàm trái"), ("cant_r", "Công xôn ngàm phải")]
_load_cols_P = [("x", "x (m)"), ("P", "P xuống + (kN)")]
_load_cols_M = [("x", "x (m)"), ("M", "M thuận kim + (kN·m)")]
_load_cols_q = [("a", "từ x (m)"), ("b", "đến x (m)"), ("q1", "q đầu, xuống + (kN/m)"), ("q2", "q cuối (kN/m)")]


def build_supports(p):
    t = p["stype"]
    if t == "simple":
        if p["xB"] <= p["xA"]:
            raise InputError("Gối B phải nằm bên phải gối A.")
        return [(p["xA"], "pin"), (p["xB"], "roller")]
    return [(0.0, "fixed")] if t == "cant_l" else [(p["L"], "fixed")]


@problem("sbvl_beam", "sbvl", "Uốn ngang phẳng dầm tĩnh định",
         "Phản lực, biểu thức Q(x), M(x), biểu đồ nội lực, ứng suất pháp, ứng suất tiếp, độ võng. Dầm hai gối, có đầu thừa hoặc công xôn.",
         [num("L", "Chiều dài dầm L", "m", 6, 0),
          sel("stype", "Liên kết", _support_opts),
          num("xA", "Vị trí gối A", "m", 0, 0), num("xB", "Vị trí gối B", "m", 6, 0),
          tbl("P", "Lực tập trung", _load_cols_P, [dict(x=2, P=30)], min_rows=0),
          tbl("M", "Mômen tập trung", _load_cols_M, [], min_rows=0),
          tbl("q", "Tải phân bố", _load_cols_q, [dict(a=0, b=6, q1=10, q2=10)], min_rows=0),
          sel("sec", "Tiết diện", [("rect", "Chữ nhật b×h"), ("custom", "Nhập Ix, Wx, Sx, tw")]),
          num("b", "b", "mm", 200, 0), num("h", "h", "mm", 400, 0),
          num("Ix", "Ix (khi tự nhập)", "cm⁴", 8000, 0), num("Wx", "Wx (khi tự nhập)", "cm³", 500, 0),
          num("Sx", "Sx nửa tiết diện (khi tự nhập)", "cm³", 280, 0), num("tw", "Bề rộng tại trục trung hoà (khi tự nhập)", "mm", 8, 0),
          num("E", "Mô đun đàn hồi E", "MPa", 200000, 0), num("sa", "[σ]", "MPa", 160, 0), num("ta", "[τ]", "MPa", 95, 0), num("flim", "Độ võng cho phép L/", "", 250, 1)])
def beam(p):
    r = Report("Uốn ngang phẳng dầm tĩnh định", MOD)
    sup = build_supports(p)
    try:
        b = BeamModel(p["L"], sup, [], [(q["x"], q["P"]) for q in p["P"]], [(q["x"], q["M"]) for q in p["M"]], [(q["a"], q["b"], q["q1"], q["q2"]) for q in p["q"]])
    except ValueError as e:
        raise InputError(str(e))
    r.given([("Chiều dài dầm", "L", p["L"], "m"), ("Mô đun đàn hồi", "E", p["E"], "MPa"), ("Ứng suất pháp cho phép", "[σ]", p["sa"], "MPa"), ("Ứng suất tiếp cho phép", "[τ]", p["ta"], "MPa")])
    m = report_beam_solution(r, b)
    if p["sec"] == "rect":
        A = p["b"] * p["h"]
        Ix = p["b"] * p["h"] ** 3 / 12
        Wx = p["b"] * p["h"] ** 2 / 6
        Sx = p["b"] * p["h"] ** 2 / 8
        tw = p["b"]
        r.h("Bước 5. Đặc trưng tiết diện")
        r.calc("I_x", "[[b·h^3 // 12]]", f"[[{fmt(p['b'])}·{fmt(p['h'])}^3 // 12]]", Ix, "mm⁴")
        r.calc("W_x", "[[b·h^2 // 6]]", f"[[{fmt(p['b'])}·{fmt(p['h'])}^2 // 6]]", Wx, "mm³")
        r.calc("S_x", "[[b·h^2 // 8]]", None, Sx, "mm³")
    else:
        Ix, Wx, Sx, tw = p["Ix"] * 1e4, p["Wx"] * 1e3, p["Sx"] * 1e3, p["tw"]
        r.h("Bước 5. Đặc trưng tiết diện")
        r.p(f"Theo số liệu nhập: Ix = {fmt(Ix)} mm⁴; Wx = {fmt(Wx)} mm³; Sx = {fmt(Sx)} mm³; b = {fmt(tw)} mm.")
    r.h("Bước 6. Kiểm tra ứng suất")
    Mm = abs(m["M"])
    Qm = abs(m["Q"])
    sg = Mm * 1e6 / Wx
    ta = Qm * 1e3 * Sx / (Ix * tw)
    r.calc("σ_max", "[[|M|_max // W_x]]", f"[[{fmt(Mm)}·10^6 // {fmt(Wx)}]]", sg, "MPa", key="sigma")
    r.check("Bền ứng suất pháp", f"σ_max = {fmt(sg)} {'≤' if sg <= p['sa'] else '>'} [σ] = {fmt(p['sa'])} MPa", sg <= p["sa"])
    r.calc("τ_max", "[[|Q|_max·S_x // I_x·b]]", f"[[{fmt(Qm)}·10^3·{fmt(Sx)} // {fmt(Ix)}·{fmt(tw)}]]", ta, "MPa", note="Công thức Zhuravski", key="tau")
    r.check("Bền ứng suất tiếp", f"τ_max = {fmt(ta)} {'≤' if ta <= p['ta'] else '>'} [τ] = {fmt(p['ta'])} MPa", ta <= p["ta"])
    r.h("Bước 7. Độ võng")
    EI = p["E"] * 1e3 * Ix * 1e-12  # kN·m²
    r.calc("E·I", None, f"{fmt(p['E'])}·{fmt(Ix)}·10^(-9)", EI, "kN·m²")
    r.p("Tích phân hai lần phương trình đường đàn hồi $E·I·y'' = −M(x)$ theo từng đoạn, dùng điều kiện liên tục và điều kiện biên tại gối.")
    X, Y = b.deflection(EI)
    i = int(np.argmax(np.abs(Y)))
    f = float(Y[i])
    flim = p["L"] / p["flim"]
    r.calc("f_max", None, None, f * 1000, "mm", note=f"tại x = {fmt(float(X[i]))} m", key="fmax")
    r.check("Điều kiện độ võng", f"|f|_max = {fmt(abs(f) * 1000)} {'≤' if abs(f) <= flim else '>'} L/{fmt(p['flim'])} = {fmt(flim * 1000)} mm", abs(f) <= flim)
    d = Drawing(p["L"] * 1000 / 42, "duong_dan_hoi")
    sc = p["L"] * 1000 * .1 / max(abs(f) * 1000, 1e-9)
    diagram(d, X * 1000, Y * 1000, 0, 0, sc, flip=True)
    d.text(float(X[i]) * 1000, -f * 1000 * sc - np.sign(f) * d.th, f"f = {fmt(f * 1000)} mm", d.th, "CHU")
    r.fig(d, "Đường đàn hồi của dầm (hướng xuống là dương)")
    r.conclude(f"|M|max = {fmt(Mm)} kN·m; |Q|max = {fmt(Qm)} kN.")
    r.conclude(f"σmax = {fmt(sg)} MPa; τmax = {fmt(ta)} MPa; độ võng lớn nhất f = {fmt(abs(f) * 1000)} mm.")
    r.conclude("Dầm ĐẠT các điều kiện bền và độ võng." if r.ok else "Dầm KHÔNG ĐẠT ít nhất một điều kiện, xem mục kiểm tra phía trên.")
    return r.finish()


# ---------- XOẮN ----------
@problem("sbvl_torsion", "sbvl", "Xoắn thuần tuý trục tròn bậc",
         "Biểu đồ mômen xoắn, ứng suất tiếp, góc xoắn. Trục ngàm đầu trái.",
         [tbl("segs", "Các đoạn tính từ ngàm", [("L", "L (m)"), ("D", "D ngoài (mm)"), ("d", "d trong (mm), 0 nếu đặc"), ("T", "Mômen xoắn tại cuối đoạn (kN·m)")],
              [dict(L=1, D=80, d=0, T=2), dict(L=1, D=60, d=0, T=-1)]),
          num("G", "Mô đun trượt G", "MPa", 80000, 0), num("ta", "[τ]", "MPa", 60, 0), num("ga", "Góc xoắn cho phép [θ]", "độ/m", 1, 0)])
def torsion(p):
    r = Report("Xoắn thuần tuý trục tròn bậc", MOD)
    S = p["segs"]
    for s in S:
        if s["L"] <= 0 or s["D"] <= 0 or s["d"] < 0 or s["d"] >= s["D"]:
            raise InputError("Mỗi đoạn cần L > 0, D > 0 và 0 ≤ d < D.")
    r.h("Số liệu đề bài")
    r.table(["Đoạn", "L (m)", "D (mm)", "d (mm)", "T cuối đoạn (kN·m)"], [[i + 1, s["L"], s["D"], s["d"], s["T"]] for i, s in enumerate(S)])
    r.p(f"G = {fmt(p['G'])} MPa; [τ] = {fmt(p['ta'])} MPa; [θ] = {fmt(p['ga'])} độ/m.")
    r.h("Bước 1. Mômen xoắn nội lực")
    Tn = []
    for i in range(len(S)):
        v = sum(s["T"] for s in S[i:])
        Tn.append(v)
        r.calc(f"T_{i + 1}", None, " + ".join(fmt(s["T"]) if s["T"] >= 0 else f"({fmt(s['T'])})" for s in S[i:]), v, "kN·m", key=f"T{i + 1}")
    r.h("Bước 2. Đặc trưng hình học")
    r.f("I_p = [[π(D^4 − d^4) // 32]];  W_p = [[I_p // D/2]]")
    Ip, Wp = [], []
    for i, s in enumerate(S):
        ip = math.pi * (s["D"] ** 4 - s["d"] ** 4) / 32
        Ip.append(ip)
        Wp.append(ip / (s["D"] / 2))
    r.table(["Đoạn", "Ip (mm⁴)", "Wp (mm³)"], [[i + 1, Ip[i], Wp[i]] for i in range(len(S))])
    r.h("Bước 3. Ứng suất tiếp lớn nhất")
    tt = []
    for i in range(len(S)):
        t = abs(Tn[i]) * 1e6 / Wp[i]
        tt.append(t)
        r.calc(f"τ_{i + 1}", "[[|T|·10^6 // W_p]]", f"[[{fmt(abs(Tn[i]))}·10^6 // {fmt(Wp[i])}]]", t, "MPa", key=f"tau{i + 1}")
    tm = max(tt)
    r.check("Điều kiện bền", f"τ_max = {fmt(tm)} {'≤' if tm <= p['ta'] else '>'} [τ] = {fmt(p['ta'])} MPa", tm <= p["ta"])
    r.h("Bước 4. Góc xoắn")
    phi = 0
    rows = []
    tmx = 0
    for i, s in enumerate(S):
        dp = Tn[i] * 1e6 * s["L"] * 1e3 / (p["G"] * Ip[i])
        th_ = abs(dp) / s["L"] * 180 / math.pi
        tmx = max(tmx, th_)
        phi += dp
        r.calc(f"φ_{i + 1}", "[[T·L // G·I_p]]", f"[[{fmt(Tn[i])}·10^6·{fmt(s['L'] * 1000)} // {fmt(p['G'])}·{fmt(Ip[i])}]]", dp, "rad", d=5)
    r.calc("φ_(tổng)", "Σφ_i", None, phi, "rad", d=5, key="phi")
    r.calc("φ_(tổng)", None, None, phi * 180 / math.pi, "độ", d=3)
    r.check("Điều kiện cứng", f"θ_max = {fmt(tmx)} {'≤' if tmx <= p['ga'] else '>'} [θ] = {fmt(p['ga'])} độ/m", tmx <= p["ga"])
    Lt = sum(s["L"] for s in S) * 1000
    th = Lt / 40
    d = Drawing(th, "truc")
    x = 0
    pos = [0]
    Dm = max(s["D"] for s in S)
    d.fixed(0, 0, -1, s=th * 3)
    for s in S:
        h = th * 2.4 * s["D"] / Dm
        d.rect(x, -h, s["L"] * 1000, 2 * h, "KET_CAU")
        d.text(x + s["L"] * 500, 0, f"D{fmt(s['D'])}", th * .8)
        x += s["L"] * 1000
        pos.append(x)
        if s["T"]:
            d.text(x, th * 3.7, f"T={fmt(s['T'])} kN·m", th, "TAI_TRONG")
            d.arc(x, 0, th * 2.6, 40, 140, "TAI_TRONG")
    y0 = -th * 9
    sc = th * 3.5 / max(max(abs(v) for v in Tn), 1e-9)
    xs, vs = [], []
    for i in range(len(S)):
        xs += [pos[i], pos[i + 1]]
        vs += [Tn[i], Tn[i]]
        d.text((pos[i] + pos[i + 1]) / 2, y0 + Tn[i] * sc + (th * .8 if Tn[i] >= 0 else -th * .8), fmt(Tn[i]), th * .9)
    diagram(d, xs, vs, 0, y0, sc)
    d.text(-th * 2.5, y0, "T (kN·m)", th)
    r.fig(d, "Sơ đồ trục và biểu đồ mômen xoắn")
    r.conclude(f"τmax = {fmt(tm)} MPa; θmax = {fmt(tmx)} độ/m; góc xoắn đầu tự do φ = {fmt(phi * 180 / math.pi)} độ.")
    r.conclude("Trục ĐẠT điều kiện bền và cứng." if r.ok else "Trục KHÔNG ĐẠT, xem mục kiểm tra.")
    return r.finish()


# ---------- TRẠNG THÁI ỨNG SUẤT ----------
@problem("sbvl_mohr", "sbvl", "Trạng thái ứng suất phẳng, vòng Mohr",
         "Ứng suất chính, phương chính, ứng suất tiếp cực trị, ứng suất trên mặt nghiêng, kiểm tra theo thuyết bền III và IV.",
         [num("sx", "σx (kéo +)", "MPa", 80), num("sy", "σy (kéo +)", "MPa", -40), num("t", "τxy", "MPa", 30),
          num("alpha", "Góc mặt nghiêng α", "độ", 30), num("sa", "[σ]", "MPa", 160, 0)])
def mohr(p):
    r = Report("Trạng thái ứng suất phẳng, vòng Mohr", MOD)
    sx, sy, t, al = p["sx"], p["sy"], p["t"], math.radians(p["alpha"])
    r.given([("Ứng suất pháp", "σx", sx, "MPa"), ("Ứng suất pháp", "σy", sy, "MPa"), ("Ứng suất tiếp", "τxy", t, "MPa"), ("Góc mặt nghiêng", "α", p["alpha"], "độ"), ("Ứng suất cho phép", "[σ]", p["sa"], "MPa")])
    r.h("Bước 1. Ứng suất chính")
    c = (sx + sy) / 2
    R = math.hypot((sx - sy) / 2, t)
    r.calc("σ_tb", "[[σ_x + σ_y // 2]]", f"[[{fmt(sx)} + ({fmt(sy)}) // 2]]", c, "MPa")
    r.calc("R", "√[[([[σ_x − σ_y // 2]])^2 + τ_xy^2]]", f"√[[([[{fmt(sx)} − ({fmt(sy)}) // 2]])^2 + {fmt(t)}^2]]", R, "MPa", note="Bán kính vòng Mohr")
    s1, s2 = c + R, c - R
    r.calc("σ_max", "σ_tb + R", None, s1, "MPa", key="s1")
    r.calc("σ_min", "σ_tb − R", None, s2, "MPa", key="s2")
    r.h("Bước 2. Phương chính")
    a0 = 0.5 * math.atan2(2 * t, sx - sy)
    r.calc("tan2α_0", "[[2τ_xy // σ_x − σ_y]]", f"[[2·{fmt(t)} // {fmt(sx - sy)}]]" if sx != sy else None, (2 * t / (sx - sy)) if sx != sy else "∞")
    r.calc("α_0", None, None, math.degrees(a0), "độ", note="Phương của σmax so với trục x (ngược chiều kim đồng hồ dương)", key="a0")
    r.h("Bước 3. Ứng suất tiếp cực trị")
    r.calc("τ_max", "R", None, R, "MPa", key="tmax", note="Trong mặt phẳng ứng suất, trên mặt lệch 45° so với mặt chính")
    if s1 * s2 > 0:
        r.calc("τ_(max,3D)", "[[max(|σ_max|; |σ_min|) // 2]]", None, max(abs(s1), abs(s2)) / 2, "MPa", note="σmax, σmin cùng dấu nên τ lớn nhất tuyệt đối nằm ngoài mặt phẳng ứng suất")
    r.h("Bước 4. Ứng suất trên mặt nghiêng góc α")
    sa = c + (sx - sy) / 2 * math.cos(2 * al) + t * math.sin(2 * al)
    ta = -(sx - sy) / 2 * math.sin(2 * al) + t * math.cos(2 * al)
    r.calc("σ_α", "[[σ_x+σ_y // 2]] + [[σ_x−σ_y // 2]]cos2α + τ_xy·sin2α", None, sa, "MPa", key="sa")
    r.calc("τ_α", "−[[σ_x−σ_y // 2]]sin2α + τ_xy·cos2α", None, ta, "MPa", key="ta")
    r.h("Bước 5. Kiểm tra theo thuyết bền")
    pr = sorted([s1, s2, 0.0], reverse=True)
    t3 = pr[0] - pr[2]
    t4 = math.sqrt(0.5 * ((pr[0] - pr[1]) ** 2 + (pr[1] - pr[2]) ** 2 + (pr[2] - pr[0]) ** 2))
    r.p(f"Sắp xếp ứng suất chính: $σ_1$ = {fmt(pr[0])}; $σ_2$ = {fmt(pr[1])}; $σ_3$ = {fmt(pr[2])} MPa.")
    r.calc("σ_(tđ,III)", "σ_1 − σ_3", f"{fmt(pr[0])} − ({fmt(pr[2])})", t3, "MPa", key="s3")
    r.check("Thuyết bền III (ứng suất tiếp lớn nhất)", f"σ_tđ = {fmt(t3)} {'≤' if t3 <= p['sa'] else '>'} [σ] = {fmt(p['sa'])} MPa", t3 <= p["sa"])
    r.calc("σ_(tđ,IV)", "√[[[[1 // 2]]((σ_1−σ_2)^2 + (σ_2−σ_3)^2 + (σ_3−σ_1)^2)]]", None, t4, "MPa", key="s4")
    r.check("Thuyết bền IV (thế năng biến đổi hình dáng)", f"σ_tđ = {fmt(t4)} {'≤' if t4 <= p['sa'] else '>'} [σ] = {fmt(p['sa'])} MPa", t4 <= p["sa"])
    k = 1000 / max(R, 1e-9)
    d = Drawing(70, "vong_mohr")
    cx = c * k
    rr = R * k
    d.circle(cx, 0, rr, "KET_CAU")
    x0 = min(s2, 0) * k - 150
    x1 = max(s1, 0) * k + 150
    d.arrow(x0, 0, x1, 0, "TRUC")
    d.arrow(0, -rr - 150, 0, rr + 150, "TRUC")
    d.text(x1 + 40, 40, "σ", 90, ha="left")
    d.text(50, rr + 170, "τ", 90, ha="left")
    d.line(sx * k, t * k, sy * k, -t * k, "BIEU_DO")
    d.circle(sx * k, t * k, 14, "KY_HIEU", True)
    d.circle(sy * k, -t * k, 14, "KY_HIEU", True)
    d.text(sx * k + 30, t * k + 60, f"X({fmt(sx)}; {fmt(t)})", 70, ha="left")
    d.text(sy * k - 30, -t * k - 60, f"Y({fmt(sy)}; {fmt(-t)})", 70, ha="right")
    d.text(s1 * k - 10, -70, f"σmax={fmt(s1)}", 70, ha="right")
    d.text(s2 * k + 10, 70, f"σmin={fmt(s2)}", 70, ha="left")
    d.text(cx, rr + 60, f"τmax={fmt(R)}", 70)
    r.fig(d, "Vòng tròn Mohr ứng suất", width_cm=11)
    r.conclude(f"σmax = {fmt(s1)} MPa; σmin = {fmt(s2)} MPa; τmax = {fmt(R)} MPa; α0 = {fmt(math.degrees(a0))}°.")
    r.conclude(f"σtđ III = {fmt(t3)} MPa; σtđ IV = {fmt(t4)} MPa: " + ("ĐẠT." if r.ok else "KHÔNG ĐẠT."))
    return r.finish()


# ---------- CHỊU LỰC PHỨC TẠP ----------
@problem("sbvl_combined", "sbvl", "Nén lệch tâm, uốn xiên tiết diện chữ nhật",
         "Ứng suất tại 4 góc, đường trung hoà, lõi tiết diện, kiểm tra bền.",
         [num("N", "Lực dọc N (kéo +, nén −)", "kN", -500), num("Mx", "Mô men Mx (gây kéo phía +y)", "kN·m", 40), num("My", "Mô men My (gây kéo phía +x)", "kN·m", 20),
          num("b", "Cạnh b (theo x)", "mm", 300, 0), num("h", "Cạnh h (theo y)", "mm", 500, 0),
          num("st", "[σ] kéo", "MPa", 160, 0), num("sc", "[σ] nén", "MPa", 160, 0)])
def combined(p):
    r = Report("Nén lệch tâm, uốn xiên tiết diện chữ nhật", MOD)
    b, h, N, Mx, My = p["b"], p["h"], p["N"], p["Mx"], p["My"]
    r.given([("Lực dọc", "N", N, "kN"), ("Mô men", "Mx", Mx, "kN·m"), ("Mô men", "My", My, "kN·m"), ("Tiết diện", "b×h", f"{fmt(b)}×{fmt(h)}", "mm")])
    r.h("Bước 1. Đặc trưng tiết diện")
    A = b * h
    Ix = b * h ** 3 / 12
    Iy = h * b ** 3 / 12
    r.calc("A", "b·h", f"{fmt(b)}·{fmt(h)}", A, "mm²")
    r.calc("I_x", "[[b·h^3 // 12]]", None, Ix, "mm⁴")
    r.calc("I_y", "[[h·b^3 // 12]]", None, Iy, "mm⁴")
    r.h("Bước 2. Ứng suất tại điểm (x, y)")
    r.f("σ = [[N // A]] + [[M_x // I_x]]·y + [[M_y // I_y]]·x")
    rows = []
    vals = []
    for nm, x, y in (("1", b / 2, h / 2), ("2", -b / 2, h / 2), ("3", -b / 2, -h / 2), ("4", b / 2, -h / 2)):
        s = N * 1e3 / A + Mx * 1e6 / Ix * y + My * 1e6 / Iy * x
        vals.append(s)
        rows.append([nm, x, y, N * 1e3 / A, Mx * 1e6 / Ix * y, My * 1e6 / Iy * x, s])
    r.table(["Góc", "x (mm)", "y (mm)", "N/A", "Mx·y/Ix", "My·x/Iy", "σ (MPa)"], rows)
    smax, smin = max(vals), min(vals)
    r.results.update(smax=smax, smin=smin)
    r.h("Bước 3. Kiểm tra bền")
    if smax > 0:
        r.check("Bền kéo", f"σ_max = {fmt(smax)} {'≤' if smax <= p['st'] else '>'} [σ]k = {fmt(p['st'])} MPa", smax <= p["st"])
    r.check("Bền nén", f"|σ_min| = {fmt(abs(smin))} {'≤' if abs(smin) <= p['sc'] else '>'} [σ]n = {fmt(p['sc'])} MPa", abs(min(smin, 0)) <= p["sc"])
    r.h("Bước 4. Đường trung hoà và lõi tiết diện")
    r.p("Lõi tiết diện chữ nhật là hình thoi có đường chéo b/3 và h/3. Lực đặt trong lõi thì toàn tiết diện cùng dấu ứng suất.")
    d = Drawing(max(b, h) / 20, "tiet_dien")
    d.rect(-b / 2, -h / 2, b, h, "KET_CAU")
    d.pline([(b / 6, 0), (0, h / 6), (-b / 6, 0), (0, -h / 6)], "PHU", True, True)
    d.line(-b * .7, 0, b * .7, 0, "TRUC", True)
    d.line(0, -h * .7, 0, h * .7, "TRUC", True)
    if N != 0:
        ex, ey = My * 1e6 / (N * 1e3), Mx * 1e6 / (N * 1e3)
        r.calc("e_x", "[[M_y // N]]", None, ex, "mm")
        r.calc("e_y", "[[M_x // N]]", None, ey, "mm")
        inside = abs(ex) / (b / 6) + abs(ey) / (h / 6) <= 1
        r.p("Điểm đặt lực " + ("nằm trong lõi: toàn tiết diện một dấu ứng suất." if inside else "nằm ngoài lõi: tiết diện có cả vùng kéo và vùng nén."))
        pts = []
        s0 = N * 1e3 / A
        a, c = Mx * 1e6 / Ix, My * 1e6 / Iy
        cand = []
        if abs(c) > 1e-12:
            for y in (-h / 2, h / 2):
                x = -(s0 + a * y) / c
                if -b / 2 <= x <= b / 2:
                    cand.append((x, y))
        if abs(a) > 1e-12:
            for x in (-b / 2, b / 2):
                y = -(s0 + c * x) / a
                if -h / 2 <= y <= h / 2:
                    cand.append((x, y))
        if len(cand) >= 2:
            d.line(cand[0][0], cand[0][1], cand[1][0], cand[1][1], "THEP")
            d.text(cand[1][0], cand[1][1] + d.th, "đường trung hoà", d.th * .8, "THEP")
        if abs(ex) < b and abs(ey) < h:
            d.circle(ex, ey, d.th * .25, "TAI_TRONG", True)
    for nm, x, y, v in zip("1234", (b / 2, -b / 2, -b / 2, b / 2), (h / 2, h / 2, -h / 2, -h / 2), vals):
        d.text(x + (d.th * 2.6 if x > 0 else -d.th * 2.6), y, f"{nm}: {fmt(v)}", d.th * .85)
    d.dim(-b / 2, -h / 2, b / 2, -h / 2, -d.th * 1.5, fmt(b))
    d.dim(-b / 2, h / 2, -b / 2, -h / 2, -d.th * 5, fmt(h))
    r.fig(d, "Tiết diện, lõi tiết diện (nét đứt), đường trung hoà và ứng suất góc (MPa)", width_cm=11)
    r.conclude(f"σmax = {fmt(smax)} MPa; σmin = {fmt(smin)} MPa: " + ("ĐẠT." if r.ok else "KHÔNG ĐẠT."))
    return r.finish()


# ---------- ỔN ĐỊNH ----------
@problem("sbvl_buckling", "sbvl", "Ổn định thanh chịu nén",
         "Độ mảnh, lực tới hạn theo Euler hoặc Iasinski, hệ số an toàn ổn định.",
         [num("L", "Chiều dài thanh L", "m", 4, 0),
          sel("mu", "Liên kết hai đầu", [(1.0, "Khớp - khớp (μ = 1)"), (2.0, "Ngàm - tự do (μ = 2)"), (0.7, "Ngàm - khớp (μ = 0,7)"), (0.5, "Ngàm - ngàm (μ = 0,5)")], 1.0),
          num("A", "Diện tích A", "cm²", 40, 0), num("Imin", "Imin", "cm⁴", 800, 0),
          num("E", "E", "MPa", 200000, 0), num("sp", "Giới hạn tỷ lệ σtl", "MPa", 200, 0), num("a", "Hệ số a (Iasinski)", "MPa", 304, 0), num("b", "Hệ số b (Iasinski)", "MPa", 1.12, 0), num("s0", "Giới hạn chảy σch", "MPa", 240, 0),
          num("N", "Lực nén N", "kN", 300, 0), num("k", "Hệ số an toàn ổn định yêu cầu", "", 2.0, 1)])
def buckling(p):
    r = Report("Ổn định thanh chịu nén", MOD)
    mu = float(p["mu"])
    L, A, I, E = p["L"], p["A"], p["Imin"], p["E"]
    r.given([("Chiều dài", "L", L, "m"), ("Hệ số liên kết", "μ", mu, ""), ("Diện tích", "A", A, "cm²"), ("Mômen quán tính nhỏ nhất", "Imin", I, "cm⁴"), ("Mô đun đàn hồi", "E", E, "MPa"), ("Giới hạn tỷ lệ", "σtl", p["sp"], "MPa"), ("Lực nén", "N", p["N"], "kN")])
    r.h("Bước 1. Độ mảnh")
    i = math.sqrt(I / A)
    lam = mu * L * 100 / i
    r.calc("i_min", "√[[I_min/A]]", f"√[[{fmt(I)}/{fmt(A)}]]", i, "cm")
    r.calc("λ", "[[μ·L // i_min]]", f"[[{fmt(mu)}·{fmt(L * 100)} // {fmt(i)}]]", lam, key="lam")
    lam0 = math.pi * math.sqrt(E / p["sp"])
    lam1 = (p["a"] - p["s0"]) / p["b"]
    r.calc("λ_0", "π√[[E/σ_tl]]", f"π√[[{fmt(E)}/{fmt(p['sp'])}]]", lam0, key="lam0")
    r.calc("λ_1", "[[a − σ_ch // b]]", f"[[{fmt(p['a'])} − {fmt(p['s0'])} // {fmt(p['b'])}]]", lam1)
    r.h("Bước 2. Ứng suất tới hạn")
    if lam >= lam0:
        s = math.pi ** 2 * E / lam ** 2
        r.p("Vì λ ≥ λ₀: thanh mất ổn định đàn hồi, dùng công thức Euler.")
        r.calc("σ_th", "[[π^2·E // λ^2]]", f"[[π^2·{fmt(E)} // {fmt(lam)}^2]]", s, "MPa", key="scr")
        method = "Euler"
    elif lam >= lam1:
        s = p["a"] - p["b"] * lam
        r.p("Vì λ₁ ≤ λ < λ₀: thanh mất ổn định ngoài đàn hồi, dùng công thức Iasinski.")
        r.calc("σ_th", "a − b·λ", f"{fmt(p['a'])} − {fmt(p['b'])}·{fmt(lam)}", s, "MPa", key="scr")
        method = "Iasinski"
    else:
        s = p["s0"]
        r.p("Vì λ < λ₁: thanh mảnh nhỏ, phá hoại do bền, lấy $σ_th$ = $σ_ch$.")
        r.calc("σ_th", "σ_ch", None, s, "MPa", key="scr")
        method = "Bền"
    Ncr = s * A * 100 / 1000
    r.calc("N_th", "σ_th·A", f"{fmt(s)}·{fmt(A * 100)}·10^(-3)", Ncr, "kN", key="Ncr")
    r.h("Bước 3. Kiểm tra ổn định")
    k = Ncr / p["N"] if p["N"] else float("inf")
    r.calc("k_(ôđ)", "[[N_th // N]]", f"[[{fmt(Ncr)} // {fmt(p['N'])}]]", k, key="k")
    r.check("Điều kiện ổn định", f"k = {fmt(k)} {'≥' if k >= p['k'] else '<'} [k] = {fmt(p['k'])}", k >= p["k"])
    d = Drawing(L * 1000 / 18, "thanh_nen")
    H = L * 1000
    d.arrow(0, H + H * .3, 0, H + H * .03)
    d.text(d.th * .6, H + H * .25, f"N={fmt(p['N'])} kN", d.th, ha="left", layer="TAI_TRONG")
    amp = H * .08
    ptsc = [(amp * math.sin(math.pi * t / 30 * (1 if mu == 1 else .5)), H * t / 30) for t in range(31)]
    d.line(0, 0, 0, H, "KET_CAU")
    d.pline(ptsc, "PHU", dash=True)
    d.dim(-H * .05, 0, -H * .05, H, 0, f"L={fmt(L)} m")
    d.text(H * .3, H / 2, f"μ = {fmt(mu)}", d.th, ha="left")
    r.fig(d, "Sơ đồ thanh chịu nén và dạng mất ổn định (nét đứt)", width_cm=7)
    r.conclude(f"λ = {fmt(lam)}; λ₀ = {fmt(lam0)}; công thức dùng: {method}; Nth = {fmt(Ncr)} kN; hệ số an toàn k = {fmt(k)}.")
    r.conclude("Thanh ĐẠT điều kiện ổn định." if r.ok else "Thanh KHÔNG ĐẠT điều kiện ổn định.")
    return r.finish()

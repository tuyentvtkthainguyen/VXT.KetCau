"""Dầm tĩnh định tổng quát (gồm dầm có khớp): phản lực, biểu thức Q(x), M(x), độ võng."""
from __future__ import annotations
import numpy as np
from numpy.polynomial import polynomial as P
from .report import fmt
from .drawing import Drawing, diagram


def poly_str(c, var="x"):
    """c: hệ số từ bậc 0 lên. Trả về chuỗi công thức."""
    terms = []
    for i in range(len(c) - 1, -1, -1):
        v = c[i]
        if abs(v) < 1e-9:
            continue
        a = fmt(abs(v))
        if i == 0:
            t = a
        else:
            pw = var if i == 1 else f"{var}^{i}"
            t = pw if a == "1" else f"{a}·{pw}"
        terms.append(("−" if v < 0 else "+", t))
    if not terms:
        return "0"
    s = ("−" if terms[0][0] == "−" else "") + terms[0][1]
    for sg, t in terms[1:]:
        s += f" {sg} {t}"
    return s


class BeamModel:
    def __init__(self, L, supports, hinges=(), points=(), moments=(), dist=()):
        self.L = float(L)
        self.supports = sorted([(float(x), t) for x, t in supports], key=lambda s: s[0])
        self.hinges = sorted(float(h) for h in hinges)
        self.points = [(float(x), float(v)) for x, v in points]
        self.moments = [(float(x), float(v)) for x, v in moments]
        self.dist = [(float(a), float(b), float(c), float(d)) for a, b, c, d in dist]
        self.reactions = []  # (nhãn, loại 'R'|'M', x, giá trị)
        self._validate()
        self._solve()
        self._pieces()

    def _validate(self):
        L = self.L
        if L <= 0:
            raise ValueError("Chiều dài dầm phải dương")
        for x, _ in self.supports:
            if x < -1e-9 or x > L + 1e-9:
                raise ValueError(f"Gối tại x = {x} nằm ngoài dầm")
        for x, _ in self.points + self.moments:
            if x < -1e-9 or x > L + 1e-9:
                raise ValueError(f"Tải tại x = {x} nằm ngoài dầm")
        for a, b, *_ in self.dist:
            if a < -1e-9 or b > L + 1e-9 or b <= a:
                raise ValueError(f"Tải phân bố {a}–{b} không hợp lệ")

    @staticmethod
    def _label(i):
        return "ABCDEFGHIJKLMNOP"[i]

    def _solve(self):
        unknown = []
        for i, (x, t) in enumerate(self.supports):
            lb = self._label(i)
            unknown.append(("R", x, f"V_{lb}"))
            if t == "fixed":
                unknown.append(("M", x, f"M_{lb}"))
        n = len(unknown)
        need = 2 + len(self.hinges)
        if n != need:
            raise ValueError(
                f"Hệ không tĩnh định: có {n} ẩn phản lực nhưng chỉ có {need} phương trình cân bằng. "
                "Dùng dạng bài dầm liên tục hoặc khung siêu tĩnh, hoặc sửa gối/khớp."
            )
        A = np.zeros((n, n))
        b = np.zeros(n)
        # ΣV = 0 ; ΣM quanh x=0 (lấy chiều mômen uốn tại đầu phải x=L: M(L)=0)
        for j, (k, x, _) in enumerate(unknown):
            A[0, j] = 1 if k == "R" else 0
            A[1, j] = (self.L - x) if k == "R" else 1
        tot, mom = self._load_sums(self.L)
        b[0] = tot
        b[1] = mom
        for r, h in enumerate(self.hinges):
            for j, (k, x, _) in enumerate(unknown):
                if x < h - 1e-9:
                    A[2 + r, j] = (h - x) if k == "R" else 1
            _, m = self._load_sums(h)
            b[2 + r] = m
        if abs(np.linalg.det(A)) < 1e-9:
            raise ValueError("Hệ biến hình (không bất biến hình): kiểm tra lại vị trí gối và khớp.")
        sol = np.linalg.solve(A, b)
        self.A, self.b = A, b
        self.reactions = [(u[2], u[0], u[1], float(v)) for u, v in zip(unknown, sol)]

    def _load_sums(self, x):
        """Tổng lực xuống và mômen (dấu theo M uốn) của tải nằm bên trái mặt cắt x."""
        tot = 0.0
        m = 0.0
        for xi, v in self.points:
            if xi <= x + 1e-9:
                tot += v
                m += v * (x - xi)
        for xi, v in self.moments:
            if xi <= x + 1e-9:
                m -= v
        for a, b, q1, q2 in self.dist:
            if a >= x:
                continue
            e = min(b, x)
            k = (q2 - q1) / (b - a)
            qe = q1 + k * (e - a)
            R = (q1 + qe) / 2 * (e - a)
            M1 = (e - a) ** 2 * (q1 / 2 + (qe - q1) / 3)  # mômen tĩnh quanh a
            tot += R
            m += R * (x - a) - M1
        return tot, m

    def _pieces(self):
        pts = {0.0, self.L}
        pts |= {x for x, _ in self.supports} | set(self.hinges)
        pts |= {x for x, _ in self.points} | {x for x, _ in self.moments}
        for a, b, *_ in self.dist:
            pts |= {a, b}
        bp = sorted(pts)
        merged = [bp[0]]
        for x in bp[1:]:
            if x - merged[-1] > 1e-9:
                merged.append(x)
        self.breaks = merged
        self.pieces = []
        for a, b in zip(merged[:-1], merged[1:]):
            mid = (a + b) / 2
            Q = np.zeros(4)
            M = np.zeros(5)

            def add(Qc, Mc):
                nonlocal Q, M
                Q[: len(Qc)] += Qc
                M[: len(Mc)] += Mc

            for lb, k, x, v in self.reactions:
                if x < mid:
                    if k == "R":
                        add([v], [-v * x, v])
                    else:
                        add([], [v])
            for x, v in self.points:
                if x < mid:
                    add([-v], [v * x, -v])
            for x, v in self.moments:
                if x < mid:
                    add([], [v])
            for x1, x2, q1, q2 in self.dist:
                if x1 >= mid:
                    continue
                k = (q2 - q1) / (x2 - x1)
                if x2 >= mid:
                    s = np.array([-x1, 1.0])
                    s2 = P.polymul(s, s)
                    s3 = P.polymul(s2, s)
                    Qc = -(q1 * np.pad(s, (0, 1)) + k / 2 * s2)
                    Mc = -(q1 / 2 * np.pad(s2, (0, 1)) + k / 6 * s3)
                    add(Qc, Mc)
                else:
                    R = (q1 + q2) / 2 * (x2 - x1)
                    M1 = (x2 - x1) ** 2 * (q1 / 2 + (q2 - q1) / 3)
                    add([-R], [R * x1 + M1, -R])
            self.pieces.append(dict(a=a, b=b, Q=np.trim_zeros(Q, "b") if np.any(Q) else np.array([0.0]), M=np.trim_zeros(M, "b") if np.any(M) else np.array([0.0])))

    def piece_at(self, x):
        for p in self.pieces:
            if p["a"] - 1e-9 <= x <= p["b"] + 1e-9:
                return p
        return self.pieces[-1]

    def sample(self, n=20):
        xs, Qs, Ms = [], [], []
        for p in self.pieces:
            t = np.linspace(p["a"], p["b"], n + 1)
            xs += list(t)
            Qs += list(P.polyval(t, p["Q"]))
            Ms += list(P.polyval(t, p["M"]))
        return np.array(xs), np.array(Qs), np.array(Ms)

    def extrema(self):
        """Các điểm cực trị M trong đoạn (Q đổi dấu)."""
        out = []
        for p in self.pieces:
            if len(p["Q"]) < 2:
                continue
            for r in P.polyroots(p["Q"]):
                if abs(r.imag) < 1e-9 and p["a"] + 1e-6 < r.real < p["b"] - 1e-6:
                    out.append((float(r.real), float(P.polyval(r.real, p["M"]))))
        return out

    def max_abs(self):
        x, Q, M = self.sample()
        ex = self.extrema()  # lấy cả cực trị thật giữa các điểm chia
        if ex:
            x = np.concatenate([x, [e[0] for e in ex]])
            M = np.concatenate([M, [e[1] for e in ex]])
            Q = np.concatenate([Q, np.zeros(len(ex))])
        im = int(np.argmax(np.abs(M)))
        iq = int(np.argmax(np.abs(Q)))
        return dict(M=float(M[im]), xM=float(x[im]), Q=float(Q[iq]), xQ=float(x[iq]), Mpos=float(M.max()), Mneg=float(M.min()))

    def deflection(self, EI, n=20):
        """Độ võng (m, dương hướng xuống) với EI kN·m². Chỉ dầm không khớp."""
        if self.hinges:
            return None
        # θ = -∫M/EI (y xuống dương: y'' = -M/EI)
        xs, ys, ts = [], [], []
        t0 = 0.0
        y0 = 0.0
        segs = []
        for p in self.pieces:
            th = P.polyint(-p["M"] / EI)
            th[0] += t0 - P.polyval(p["a"], th)
            yy = P.polyint(th)
            yy[0] += y0 - P.polyval(p["a"], yy)
            t0 = P.polyval(p["b"], th)
            y0 = P.polyval(p["b"], yy)
            segs.append((p, th, yy))

        def Y(x):
            for p, th, yy in segs:
                if x <= p["b"] + 1e-9:
                    return P.polyval(x, yy), P.polyval(x, th)
            return P.polyval(x, segs[-1][2]), P.polyval(x, segs[-1][1])

        fx = [s for s in self.supports if s[1] == "fixed"]
        if fx:
            xf = fx[0][0]
            yf, tf = Y(xf)
            C1 = -tf
            C2 = -yf - C1 * xf
        else:
            x1, x2 = self.supports[0][0], self.supports[1][0]
            y1, _ = Y(x1)
            y2, _ = Y(x2)
            C1 = -(y2 - y1) / (x2 - x1)
            C2 = -y1 - C1 * x1
        X, Yv = [], []
        for p, th, yy in segs:
            t = np.linspace(p["a"], p["b"], n + 1)
            X += list(t)
            Yv += list(P.polyval(t, yy) + C1 * t + C2)
        return np.array(X), np.array(Yv)


def draw_scheme(b: BeamModel, title="") -> Drawing:
    L = b.L * 1000
    th = max(L / 42, 60)
    d = Drawing(th, "so_do")
    y = 0
    d.line(0, y, L, y, "KET_CAU")
    for i, (x, t) in enumerate(b.supports):
        X = x * 1000
        if t == "fixed":
            d.fixed(X, y, -1 if x < b.L / 2 else 1)
        elif i == 0 or t == "pin":
            d.pin(X, y)
        else:
            d.roller(X, y)
        d.text(X, y - th * 3.0, b._label(i), th, "CHU")
    for h in b.hinges:
        d.hinge(h * 1000, y)
    H = th * 4
    # tải phân bố
    for x1, x2, q1, q2 in b.dist:
        qm = max(abs(q1), abs(q2), 1e-9)
        a, c = x1 * 1000, x2 * 1000
        h1 = H * (0.35 + .65 * abs(q1) / qm) if q1 else 0
        h2 = H * (0.35 + .65 * abs(q2) / qm) if q2 else 0
        d.pline([(a, y + h1 + th * .3), (c, y + h2 + th * .3)], "TAI_TRONG")
        m = max(int((c - a) / (th * 2)), 2)
        for k in range(m + 1):
            xk = a + (c - a) * k / m
            hk = h1 + (h2 - h1) * k / m
            if hk > th * .2:
                d.arrow(xk, y + hk + th * .3, xk, y + th * .25)
        label = f"q={fmt(q1)} kN/m" if q1 == q2 else f"q={fmt(q1)}÷{fmt(q2)} kN/m"
        d.text((a + c) / 2, y + max(h1, h2) + th * 1.0, label, th, "TAI_TRONG")
    for x, v in b.points:
        X = x * 1000
        d.arrow(X, y + H * 1.25, X, y + th * .1) if v >= 0 else d.arrow(X, y - H, X, y - th * .1)
        d.text(X + th * .3, y + H * 1.25 + th * .8, f"P={fmt(abs(v))} kN", th, "TAI_TRONG", ha="left")
    for x, v in b.moments:
        X = x * 1000
        r = th * 1.3
        if v >= 0:
            d.arc(X, y, r, -60, 150, "TAI_TRONG")
            d.arrow(X + r * .6, y - r * .9, X + r * .5, y - r * .87, size=th * .6)
        else:
            d.arc(X, y, r, 30, 240, "TAI_TRONG")
            d.arrow(X - r * .6, y - r * .9, X - r * .5, y - r * .87, size=th * .6)
        d.text(X, y + r + th * .9, f"M={fmt(abs(v))} kN·m", th, "TAI_TRONG")
    # chuỗi kích thước
    prev = 0
    yd = -th * 4.8
    for x in b.breaks[1:]:
        d.dim(prev * 1000, yd, x * 1000, yd, 0, fmt(x - prev) + " m")
        prev = x
    return d


def draw_diagrams(b: BeamModel) -> Drawing:
    L = b.L * 1000
    th = max(L / 42, 60)
    d = Drawing(th, "bieu_do")
    xs, Q, M = b.sample()
    H = L * 0.14
    qm = max(np.abs(Q).max(), 1e-9)
    mm = max(np.abs(M).max(), 1e-9)
    sq = H / qm
    sm = H / mm

    def labels(y0, vals, xs_, flip, scale):
        seen = []
        for x, v in zip(xs_, vals):
            if abs(v) < 1e-6 * max(np.abs(vals).max(), 1e-9):
                continue
            X = x * 1000
            if any(abs(X - sx) < th * 2.2 and abs(v - sv) < 1e-6 for sx, sv in seen):
                continue
            yy = y0 + (-v if flip else v) * scale
            dy = (th * .75) if yy >= y0 else (-th * .75)
            # lệch ngang khi 2 nhãn gần nhau
            shift = 0
            if any(abs(X - sx) < th * 2.2 for sx, _ in seen):
                shift = th * 2.0
            d.text(X + shift, yy + dy, fmt(v), th * .9, "CHU")
            seen.append((X, v))

    # Q
    yQ = 0
    diagram(d, xs * 1000, Q, 0, yQ, sq)
    pts_x, pts_v = [], []
    for p in b.pieces:
        pts_x += [p["a"] + 1e-9, p["b"] - 1e-9]
        pts_v += [float(P.polyval(p["a"], p["Q"])), float(P.polyval(p["b"], p["Q"]))]
    labels(yQ, np.array(pts_v), pts_x, False, sq)
    d.text(-th * 2.2, yQ, "Q (kN)", th, "CHU")
    # M
    yM = -(2.4 * H + th * 4)
    diagram(d, xs * 1000, M, 0, yM, sm, flip=True)
    pts_x, pts_v = [], []
    for p in b.pieces:
        pts_x += [p["a"] + 1e-9, p["b"] - 1e-9]
        pts_v += [float(P.polyval(p["a"], p["M"])), float(P.polyval(p["b"], p["M"]))]
    for x, v in b.extrema():
        pts_x.append(x)
        pts_v.append(v)
    order = np.argsort(pts_x)
    labels(yM, np.array(pts_v)[order], np.array(pts_x)[order], True, sm)
    d.text(-th * 2.2, yM, "M (kN·m)", th, "CHU")
    # đường gióng
    for x in b.breaks:
        d.line(x * 1000, yM - H - th * .5, x * 1000, yQ + H + th * .5, "PHU", True)
    return d


def report_beam_solution(r, b: BeamModel, sol_deflect=None):
    """Ghi phần giải: phản lực, biểu thức, biểu đồ. Trả về dict lớn nhất."""
    r.h("Sơ đồ tính")
    r.fig(draw_scheme(b), "Sơ đồ tính dầm")
    r.h("Bước 1. Xác định phản lực")
    r.p("Quy ước: lực hướng xuống dương với tải trọng; phản lực hướng lên dương; mômen tập trung dương khi quay thuận chiều kim đồng hồ.")
    tot, _ = b._load_sums(b.L)
    r.p("Phương trình cân bằng lực đứng:")
    names = [lb for lb, k, x, v in b.reactions if k == "R"]
    r.f("ΣY = 0: " + " + ".join(names) + f" = {fmt(tot)} kN")
    r.p("Phương trình cân bằng mômen (tương đương M = 0 tại đầu phải dầm):")
    terms = []
    for lb, k, x, v in b.reactions:
        terms.append(f"{fmt(b.L - x)}·{lb}" if k == "R" else lb)
    _, ml = b._load_sums(b.L)
    r.f(" + ".join(terms) + f" = {fmt(ml)} kN·m")
    for h, row, rhs in zip(b.hinges, b.A[2:], b.b[2:]):
        r.p(f"Điều kiện mômen bằng 0 tại khớp x = {fmt(h)} m:")
        t = [f"{fmt(c)}·{u[0]}" if abs(c - 1) > 1e-9 else u[0] for c, u in zip(row, b.reactions) if abs(c) > 1e-12]
        r.f(" + ".join(t) + f" = {fmt(rhs)} kN·m")
    r.p("Giải hệ phương trình, được:")
    for lb, k, x, v in b.reactions:
        r.calc(lb, None, None, v, "kN" if k == "R" else "kN·m", key=lb.replace("_", ""))
    r.h("Bước 2. Biểu thức nội lực theo từng đoạn")
    r.p("Dùng phương pháp mặt cắt, xét phần dầm bên trái mặt cắt, x tính từ đầu trái (m).")
    for i, p in enumerate(b.pieces, 1):
        r.h(f"Đoạn {i}: {fmt(p['a'])} ≤ x ≤ {fmt(p['b'])} m", 3)
        r.f(f"Q(x) = {poly_str(p['Q'])}  (kN)")
        r.f(f"M(x) = {poly_str(p['M'])}  (kN·m)")
    r.h("Bước 3. Giá trị tại các mặt cắt đặc trưng")
    rows = []
    for i, p in enumerate(b.pieces, 1):
        rows.append([i, fmt(p["a"]), fmt(float(P.polyval(p["a"], p["Q"]))), fmt(float(P.polyval(p["a"], p["M"])))])
        for xe, me in b.extrema():
            if p["a"] < xe < p["b"]:
                rows.append([f"{i} (cực trị)", fmt(xe), "0", fmt(me)])
        rows.append([i, fmt(p["b"]), fmt(float(P.polyval(p["b"], p["Q"]))), fmt(float(P.polyval(p["b"], p["M"])))])
    r.table(["Đoạn", "x (m)", "Q (kN)", "M (kN·m)"], rows, widths=[3, 3, 4, 4])
    r.h("Bước 4. Biểu đồ nội lực")
    r.fig(draw_diagrams(b), "Biểu đồ lực cắt Q và mômen M (M vẽ về phía thớ căng)")
    r.h("Kiểm tra cân bằng", 3)
    sumV = sum(v for _, k, _, v in b.reactions if k == "R") - tot
    Mend = float(P.polyval(b.L, b.pieces[-1]["M"]))
    # nếu ngàm phải đã có trong b.reactions thì M(L)=0 sau khi cộng; ở đây dùng biểu thức đoạn cuối
    resid = Mend
    for lb, k, x, v in b.reactions:
        if k == "M" and abs(x - b.L) < 1e-9:
            pass
    r.f(f"ΣY = {fmt(sumV, 6)} kN ≈ 0")
    m = b.max_abs()
    r.results.update(Mmax=m["M"], Qmax=m["Q"], Mpos=m["Mpos"], Mneg=m["Mneg"])
    return m

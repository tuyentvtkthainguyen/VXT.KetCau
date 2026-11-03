"""Khung phẳng: phương pháp độ cứng trực tiếp, có giải phóng mômen đầu thanh."""
from __future__ import annotations
import math
import numpy as np
from .drawing import Drawing
from .report import fmt


class Frame:
    def __init__(self, nodes, elems, supports, nodal=()):
        """nodes: [(x,y)] m; elems: dict(i,j,EI,EA,qx,qy,rel_i,rel_j) đơn vị kN, m
        qx: + sang phải; qy: + hướng xuống, theo chiều dài phần tử.
        supports: {nút: (ux,uy,rz)} với 1 = bị giữ. nodal: [(nút,Fx,Fy,M)] Fy + lên, M + ngược kim."""
        self.nodes = [tuple(map(float, n)) for n in nodes]
        self.elems = elems
        self.supports = supports
        self.nodal = nodal

    def _local(self, e):
        (xi, yi), (xj, yj) = self.nodes[e["i"]], self.nodes[e["j"]]
        L = math.hypot(xj - xi, yj - yi)
        if L == 0:
            raise ValueError("Phần tử có chiều dài bằng 0.")
        c, s = (xj - xi) / L, (yj - yi) / L
        EA, EI = e["EA"], e["EI"]
        k = np.zeros((6, 6))
        a = EA / L
        b = 12 * EI / L ** 3
        d = 6 * EI / L ** 2
        g = 4 * EI / L
        h = 2 * EI / L
        k[np.ix_([0, 3], [0, 3])] = [[a, -a], [-a, a]]
        k[np.ix_([1, 2, 4, 5], [1, 2, 4, 5])] = [[b, d, -b, d], [d, g, -d, h], [-b, -d, b, -d], [d, h, -d, g]]
        T = np.array([[c, s, 0, 0, 0, 0], [-s, c, 0, 0, 0, 0], [0, 0, 1, 0, 0, 0], [0, 0, 0, c, s, 0], [0, 0, 0, -s, c, 0], [0, 0, 0, 0, 0, 1]])
        # tải phân bố trên hệ địa phương (qt theo +y địa phương)
        qx, qy = e.get("qx", 0.0), -e.get("qy", 0.0)
        qa = qx * c + qy * s
        qt = -qx * s + qy * c
        f0 = np.array([-qa * L / 2, -qt * L / 2, -qt * L ** 2 / 12, -qa * L / 2, -qt * L / 2, qt * L ** 2 / 12])  # nội lực do ngàm hai đầu
        rel = []
        if e.get("rel_i"):
            rel.append(2)
        if e.get("rel_j"):
            rel.append(5)
        if rel:
            keep = [q for q in range(6) if q not in rel]
            Krr = k[np.ix_(rel, rel)]
            Krk = k[np.ix_(rel, keep)]
            Kkr = k[np.ix_(keep, rel)]
            inv = np.linalg.inv(Krr)
            k2 = np.zeros((6, 6))
            k2[np.ix_(keep, keep)] = k[np.ix_(keep, keep)] - Kkr @ inv @ Krk
            f2 = np.zeros(6)
            f2[keep] = f0[keep] - Kkr @ inv @ f0[rel]
            k, f0 = k2, f2
        return L, T, k, f0, qa, qt

    def solve(self):
        n = len(self.nodes)
        K = np.zeros((3 * n, 3 * n))
        F = np.zeros(3 * n)
        self.loc = []
        for e in self.elems:
            L, T, k, f0, qa, qt = self._local(e)
            self.loc.append((L, T, k, f0, qa, qt))
            dof = [3 * e["i"], 3 * e["i"] + 1, 3 * e["i"] + 2, 3 * e["j"], 3 * e["j"] + 1, 3 * e["j"] + 2]
            K[np.ix_(dof, dof)] += T.T @ k @ T
            F[dof] -= T.T @ f0
        for nd, fx, fy, m in self.nodal:
            F[3 * nd] += fx
            F[3 * nd + 1] += fy
            F[3 * nd + 2] += m
        self.K_full = K.copy()
        self.F_full = F.copy()
        fixed = [3 * nd + q for nd, r in self.supports.items() for q in range(3) if r[q]]
        free = [q for q in range(3 * n) if q not in fixed]
        Kff = K[np.ix_(free, free)]
        sc = np.max(np.abs(np.diag(Kff))) if len(free) else 1
        if len(free) and np.linalg.matrix_rank(Kff, tol=sc * 1e-10) < len(free):
            raise ValueError("Hệ biến hình (ma trận độ cứng suy biến): thiếu liên kết hoặc đặt khớp sai.")
        U = np.zeros(3 * n)
        if free:
            U[free] = np.linalg.solve(Kff, F[free])
        self.U = U
        self.free = free
        self.fixed = fixed
        self.Kff = Kff
        self.R = K @ U - F
        self.end = []
        for e, (L, T, k, f0, qa, qt) in zip(self.elems, self.loc):
            dof = [3 * e["i"], 3 * e["i"] + 1, 3 * e["i"] + 2, 3 * e["j"], 3 * e["j"] + 1, 3 * e["j"] + 2]
            self.end.append(k @ (T @ U[dof]) + f0)
        return self

    def forces(self, idx, xi):
        """N, V, M tại x (m) từ đầu i của phần tử. M + gây căng thớ phía −y địa phương."""
        L, T, k, f0, qa, qt = self.loc[idx]
        f = self.end[idx]
        N = -f[0] - qa * xi
        V = f[1] + qt * xi
        M = -f[2] + f[1] * xi + qt * xi ** 2 / 2
        return N, V, M

    def bending_integrate(self, other, idx):
        """∫ M_this·M_other / EI dx trên phần tử (Simpson, chính xác tới bậc 3)."""
        L = self.loc[idx][0]
        EI = self.elems[idx]["EI"]
        a = self.forces(idx, 0)[2] * other.forces(idx, 0)[2]
        b = self.forces(idx, L / 2)[2] * other.forces(idx, L / 2)[2]
        c = self.forces(idx, L)[2] * other.forces(idx, L)[2]
        return L / 6 * (a + 4 * b + c) / EI


def _size(fr):
    xs = [n[0] for n in fr.nodes]
    ys = [n[1] for n in fr.nodes]
    return max(max(xs) - min(xs), max(ys) - min(ys), 1.0) * 1000


def draw_frame(fr: Frame, loads=True, node_labels=True) -> Drawing:
    S = _size(fr)
    th = S / 38
    d = Drawing(th, "so_do_khung")
    for k, e in enumerate(fr.elems):
        a, b = fr.nodes[e["i"]], fr.nodes[e["j"]]
        x1, y1, x2, y2 = a[0] * 1000, a[1] * 1000, b[0] * 1000, b[1] * 1000
        d.line(x1, y1, x2, y2, "KET_CAU")
        L = math.hypot(x2 - x1, y2 - y1)
        ux, uy = (x2 - x1) / L, (y2 - y1) / L
        if e.get("rel_i"):
            d.circle(x1 + ux * th * .35, y1 + uy * th * .35, th * .28, "KET_CAU")
        if e.get("rel_j"):
            d.circle(x2 - ux * th * .35, y2 - uy * th * .35, th * .28, "KET_CAU")
        d.text((x1 + x2) / 2 - uy * th * .9, (y1 + y2) / 2 + ux * th * .9, f"({k + 1})", th * .8, "CHU")
        if loads:
            qx, qy = e.get("qx", 0), e.get("qy", 0)
            if qy:
                m = max(int(L / (th * 2.2)), 2)
                sg = 1 if qy > 0 else -1
                for t in range(m + 1):
                    px, py = x1 + (x2 - x1) * t / m, y1 + (y2 - y1) * t / m
                    d.arrow(px, py + sg * th * 2.6, px, py + sg * th * .2)
                d.line(x1, y1 + sg * th * 2.6, x2, y2 + sg * th * 2.6, "TAI_TRONG")
                d.text((x1 + x2) / 2, (y1 + y2) / 2 + sg * th * 3.4, f"q={fmt(abs(qy))} kN/m", th, "TAI_TRONG")
            if qx:
                m = max(int(L / (th * 2.2)), 2)
                sg = 1 if qx > 0 else -1
                for t in range(m + 1):
                    px, py = x1 + (x2 - x1) * t / m, y1 + (y2 - y1) * t / m
                    d.arrow(px - sg * th * 2.6, py, px - sg * th * .2, py)
                d.line(x1 - sg * th * 2.6, y1, x2 - sg * th * 2.6, y2, "TAI_TRONG")
                d.text((x1 + x2) / 2 - sg * th * 3.6, (y1 + y2) / 2, f"q={fmt(abs(qx))}", th, "TAI_TRONG", rot=90)
    for i, n in enumerate(fr.nodes):
        X, Y = n[0] * 1000, n[1] * 1000
        if node_labels:
            d.text(X + th * .7, Y + th * .7, str(i + 1), th * .85, "KY_HIEU")
        r = fr.supports.get(i)
        if r:
            if r[2] and r[0] and r[1]:
                d.fixed_base(X, Y)
            elif r[0] and r[1]:
                d.pin(X, Y)
            elif r[1]:
                d.roller(X, Y)
            elif r[0]:
                d.line(X - th * 1.2, Y - th, X - th * 1.2, Y + th, "KET_CAU")
                d.line(X - th * 1.2, Y, X, Y, "KET_CAU")
    if loads:
        for nd, fx, fy, m in fr.nodal:
            X, Y = fr.nodes[nd][0] * 1000, fr.nodes[nd][1] * 1000
            if fx:
                sg = 1 if fx > 0 else -1
                d.arrow(X - sg * th * 3.4, Y, X - sg * th * .2, Y)
                d.text(X - sg * th * 3.2, Y + th * .8, f"{fmt(abs(fx))} kN", th, "TAI_TRONG")
            if fy:
                sg = 1 if fy > 0 else -1
                d.arrow(X, Y - sg * th * 3.4, X, Y - sg * th * .2)
                d.text(X + th * .4, Y - sg * th * 3.2, f"{fmt(abs(fy))} kN", th, "TAI_TRONG", ha="left")
            if m:
                d.arc(X, Y, th * 1.5, 30, 240, "TAI_TRONG")
                d.text(X, Y + th * 2.3, f"M={fmt(m)}", th, "TAI_TRONG")
    return d


def draw_frame_diagram(fr: Frame, comp: str, title: str, unit: str, labelled=True, max_h=None) -> Drawing:
    S = _size(fr)
    th = S / 38
    d = Drawing(th, f"bieu_do_{comp}")
    idx = dict(N=0, V=1, M=2)[comp]
    all_v = []
    for k in range(len(fr.elems)):
        L = fr.loc[k][0]
        for t in np.linspace(0, L, 21):
            all_v.append(abs(fr.forces(k, t)[idx]))
    vm = max(all_v) if all_v else 1
    sc = (max_h or S * 0.16) / max(vm, 1e-9)
    for k, e in enumerate(fr.elems):
        a, b = fr.nodes[e["i"]], fr.nodes[e["j"]]
        x1, y1, x2, y2 = a[0] * 1000, a[1] * 1000, b[0] * 1000, b[1] * 1000
        d.line(x1, y1, x2, y2, "KET_CAU")
        L = fr.loc[k][0]
        ux, uy = (x2 - x1) / (L * 1000), (y2 - y1) / (L * 1000)
        nx, ny = -uy, ux  # +y địa phương
        sign = -1 if comp == "M" else 1  # M vẽ về thớ căng
        pts = []
        T = np.linspace(0, L, 25)
        vs = []
        for t in T:
            v = fr.forces(k, t)[idx]
            vs.append(v)
            px = x1 + ux * t * 1000 + nx * sign * v * sc
            py = y1 + uy * t * 1000 + ny * sign * v * sc
            pts.append((px, py))
            d.line(x1 + ux * t * 1000, y1 + uy * t * 1000, px, py, "BIEU_DO")
        d.pline(pts, "BIEU_DO")
        if labelled:
            lab = [(0, vs[0]), (L, vs[-1])]
            if comp == "M":
                im = int(np.argmax(np.abs(vs[1:-1]))) + 1
                if abs(vs[im]) > max(abs(vs[0]), abs(vs[-1])) * 1.02 or abs(vs[im] - (vs[0] + vs[-1]) / 2) > 0.05 * vm:
                    lab.append((T[im], vs[im]))
            for t, v in lab:
                if abs(v) < 1e-6 * vm:
                    continue
                off = sign * v * sc + (np.sign(sign * v) if v else 1) * th * .9
                px = x1 + ux * t * 1000 + nx * off
                py = y1 + uy * t * 1000 + ny * off
                d.text(px, py, fmt(float(v)), th * .85, "CHU")
    xs = [n[0] * 1000 for n in fr.nodes]
    ys = [n[1] * 1000 for n in fr.nodes]
    d.text((min(xs) + max(xs)) / 2, min(ys) - th * 3, f"{title} ({unit})", th, "CHU")
    return d

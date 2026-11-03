"""Hình vẽ kỹ thuật dùng chung: một mô hình, xuất ảnh PNG và DXF.

Toạ độ tính bằng mm thực (mô hình). Lớp theo quy ước BE_TONG, THEP, DAI,
KICH_THUOC, CHU, KY_HIEU cộng thêm BIEU_DO, TRUC, KET_CAU, TAI_TRONG.
"""
from __future__ import annotations
import io
import math
import os
from typing import List, Optional, Tuple

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib import font_manager
from matplotlib.patches import Circle, Polygon, Arc

FONT_DIR = os.path.join(os.path.dirname(os.path.dirname(__file__)), "fonts")
_FONT_SANS = os.path.join(FONT_DIR, "LiberationSans-Regular.ttf")
_fp = font_manager.FontProperties(fname=_FONT_SANS)

LAYER_COLORS = {
    "0": 7, "BE_TONG": 7, "THEP": 1, "DAI": 3, "KICH_THUOC": 8, "CHU": 7, "KY_HIEU": 5,
    "BIEU_DO": 4, "TRUC": 8, "KET_CAU": 7, "TAI_TRONG": 1, "PHU": 8,
}
MPL_COLORS = {
    "BE_TONG": "#222222", "THEP": "#c62828", "DAI": "#2e7d32", "KICH_THUOC": "#555555", "CHU": "#111111",
    "KY_HIEU": "#1565c0", "BIEU_DO": "#00695c", "TRUC": "#888888", "KET_CAU": "#111111", "TAI_TRONG": "#c62828",
    "PHU": "#999999", "0": "#111111",
}
LW = {"BE_TONG": 1.3, "THEP": 1.4, "DAI": 1.0, "KET_CAU": 1.8, "BIEU_DO": 1.1, "TAI_TRONG": 1.0}

CIRCLED = "⓪①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳"


class Drawing:
    def __init__(self, text_h: float = 100.0, name: str = "hinh"):
        self.th = text_h
        self.name = name
        self.cad_scale = 1.0  # hệ số phóng khi ghép vào bản vẽ DXF (mặt cắt 1/25 so với mặt đứng 1/50)
        self.items: List[tuple] = []

    # ---------- nguyên thuỷ ----------
    def line(self, x1, y1, x2, y2, layer="0", dash=False):
        self.items.append(("line", x1, y1, x2, y2, layer, dash))

    def pline(self, pts, layer="0", closed=False, dash=False):
        self.items.append(("pline", [tuple(p) for p in pts], layer, closed, dash))

    def rect(self, x, y, w, h, layer="0", dash=False):
        self.pline([(x, y), (x + w, y), (x + w, y + h), (x, y + h)], layer, True, dash)

    def circle(self, x, y, r, layer="0", fill=False):
        self.items.append(("circle", x, y, r, layer, fill))

    def arc(self, x, y, r, a1, a2, layer="0"):
        self.items.append(("arc", x, y, r, a1, a2, layer))

    def text(self, x, y, s, h=None, layer="CHU", ha="center", va="center", rot=0.0):
        self.items.append(("text", x, y, str(s), h or self.th, layer, ha, va, rot))

    # ---------- ký hiệu ghép ----------
    def arrow(self, x1, y1, x2, y2, layer="TAI_TRONG", size=None):
        """Mũi tên từ (x1,y1) đến (x2,y2), đầu nhọn tại (x2,y2)."""
        s = size or self.th * 0.8
        self.line(x1, y1, x2, y2, layer)
        a = math.atan2(y2 - y1, x2 - x1)
        for da in (2.75, -2.75):
            self.line(x2, y2, x2 + s * math.cos(a + da), y2 + s * math.sin(a + da), layer)

    def dim(self, x1, y1, x2, y2, off, text=None, layer="KICH_THUOC"):
        """Đường kích thước giữa hai điểm, lệch off về bên trái hướng p1→p2."""
        L = math.hypot(x2 - x1, y2 - y1)
        if L == 0:
            return
        ux, uy = (x2 - x1) / L, (y2 - y1) / L
        nx, ny = -uy, ux
        ax, ay = x1 + nx * off, y1 + ny * off
        bx, by = x2 + nx * off, y2 + ny * off
        sgn = 1 if off >= 0 else -1
        ext = self.th * 0.35
        self.line(x1, y1, ax + nx * ext * sgn, ay + ny * ext * sgn, layer)
        self.line(x2, y2, bx + nx * ext * sgn, by + ny * ext * sgn, layer)
        self.line(ax, ay, bx, by, layer)
        t = self.th * 0.35
        for (px, py) in ((ax, ay), (bx, by)):
            self.line(px - (ux + nx) * t, py - (uy + ny) * t, px + (ux + nx) * t, py + (uy + ny) * t, layer)
        rot = math.degrees(math.atan2(uy, ux))
        if rot > 90 or rot <= -90:
            rot -= 180 if rot > 0 else -180
        tx, ty = (ax + bx) / 2, (ay + by) / 2
        lift = self.th * 0.65
        # chữ luôn nằm phía "trên" đường theo hướng đọc
        rr = math.radians(rot)
        self.text(tx - math.sin(rr) * lift, ty + math.cos(rr) * lift, text if text is not None else f"{L:.0f}", self.th * 0.8, "KICH_THUOC", rot=rot)

    def tag(self, px, py, sx, sy, text, num=None, side=1, layer="KY_HIEU"):
        """Nhãn thép kiểu Kết cấu Soft: chấm → đường dẫn → vai ngang có chữ → vòng số hiệu.

        (px,py): điểm chấm tại thanh; (sx,sy): gốc vai; side=+1 vai sang phải, -1 sang trái.
        """
        self.circle(px, py, self.th * 0.12, layer, True)
        self.line(px, py, sx, sy, layer)
        w = max(len(text) * self.th * 0.62, self.th * 2)
        ex = sx + side * w
        self.line(sx, sy, ex, sy, layer)
        self.text((sx + ex) / 2, sy + self.th * 0.62, text, self.th * 0.85, "CHU")
        if num is not None:
            r = self.th * 0.62
            cx = ex + side * r
            self.circle(cx, sy, r, layer)
            self.text(cx, sy, str(num), self.th * 0.72, "KY_HIEU")

    def title(self, x, y, s, sub=None, h=None):
        """Tên hình gạch chân, dòng dưới ghi tỷ lệ."""
        h = h or self.th * 1.25
        self.text(x, y, s, h, "CHU")
        w = len(s) * h * 0.55
        self.line(x - w / 2, y - h * 0.8, x + w / 2, y - h * 0.8, "CHU")
        if sub:
            self.text(x, y - h * 1.7, sub, self.th * 0.85, "CHU")

    def pin(self, x, y, s=None, layer="KET_CAU"):
        s = s or self.th * 1.4
        self.pline([(x, y), (x - s * .6, y - s), (x + s * .6, y - s)], layer, True)
        self.line(x - s, y - s, x + s, y - s, layer)
        for i in range(5):
            xx = x - s + i * s / 2
            self.line(xx, y - s, xx - s * .3, y - s * 1.3, layer)

    def roller(self, x, y, s=None, layer="KET_CAU"):
        s = s or self.th * 1.4
        self.pline([(x, y), (x - s * .6, y - s * .8), (x + s * .6, y - s * .8)], layer, True)
        self.line(x - s, y - s * 1.1, x + s, y - s * 1.1, layer)
        for i in range(5):
            xx = x - s + i * s / 2
            self.line(xx, y - s * 1.1, xx - s * .3, y - s * 1.4, layer)

    def fixed(self, x, y, direction=-1, s=None, layer="KET_CAU"):
        """Ngàm đứng tại đầu dầm; direction=-1 tường bên trái, +1 bên phải."""
        s = s or self.th * 1.6
        self.line(x, y - s, x, y + s, layer)
        for i in range(5):
            yy = y - s + i * s / 2
            self.line(x, yy, x + direction * s * .4, yy - s * .4, layer)

    def fixed_base(self, x, y, s=None, layer="KET_CAU"):
        """Ngàm chân cột (nền nằm ngang)."""
        s = s or self.th * 1.6
        self.line(x - s, y, x + s, y, layer)
        for i in range(5):
            xx = x - s + i * s / 2
            self.line(xx, y, xx - s * .4, y - s * .4, layer)

    def hinge(self, x, y, r=None, layer="KET_CAU"):
        self.circle(x, y, r or self.th * .28, layer)

    # ---------- phạm vi ----------
    def extents(self):
        xs, ys = [], []
        for it in self.items:
            k = it[0]
            if k == "line":
                xs += [it[1], it[3]]
                ys += [it[2], it[4]]
            elif k == "pline":
                xs += [p[0] for p in it[1]]
                ys += [p[1] for p in it[1]]
            elif k in ("circle", "arc"):
                xs += [it[1] - it[3], it[1] + it[3]]
                ys += [it[2] - it[3], it[2] + it[3]]
            elif k == "text":
                _, x, y, s, h, *_ = it
                w = len(s) * h * 0.56
                rot = math.radians(it[8])
                ha = it[6]
                x0 = {"center": -w / 2, "left": 0, "right": -w}[ha]
                for dx in (x0, x0 + w):
                    for dy in (-h * .65, h * .65):
                        xs.append(x + dx * math.cos(rot) - dy * math.sin(rot))
                        ys.append(y + dx * math.sin(rot) + dy * math.cos(rot))
        if not xs:
            return (0, 0, 1, 1)
        return (min(xs), min(ys), max(xs), max(ys))

    # ---------- PNG ----------
    def to_png(self, width_in: float = 6.4, dpi: int = 200, max_h_in: float = 8.0) -> bytes:
        x0, y0, x1, y1 = self.extents()
        pad = self.th * 0.8
        x0 -= pad; y0 -= pad; x1 += pad; y1 += pad
        W = max(x1 - x0, 1e-6)
        H = max(y1 - y0, 1e-6)
        h_in = width_in * H / W
        if h_in > max_h_in:
            width_in = width_in * max_h_in / h_in
            h_in = max_h_in
        fig = plt.figure(figsize=(width_in, h_in), dpi=dpi)
        ax = fig.add_axes([0, 0, 1, 1])
        ax.set_xlim(x0, x1)
        ax.set_ylim(y0, y1)
        ax.axis("off")
        k = width_in * 72 / W  # điểm in / đơn vị mô hình
        for it in self.items:
            kind = it[0]
            if kind == "line":
                _, a, b, c, d, layer, dash = it
                ax.plot([a, c], [b, d], color=MPL_COLORS.get(layer, "#111"), lw=LW.get(layer, 0.7), ls="--" if dash else "-", solid_capstyle="round")
            elif kind == "pline":
                _, pts, layer, closed, dash = it
                P = pts + [pts[0]] if closed else pts
                ax.plot([p[0] for p in P], [p[1] for p in P], color=MPL_COLORS.get(layer, "#111"), lw=LW.get(layer, 0.7), ls="--" if dash else "-", solid_joinstyle="round")
            elif kind == "circle":
                _, a, b, r, layer, fill = it
                ax.add_patch(Circle((a, b), r, fill=fill, ec=MPL_COLORS.get(layer, "#111"), fc=MPL_COLORS.get(layer, "#111") if fill else "none", lw=LW.get(layer, 0.8)))
            elif kind == "arc":
                _, a, b, r, a1, a2, layer = it
                ax.add_patch(Arc((a, b), 2 * r, 2 * r, theta1=a1, theta2=a2, color=MPL_COLORS.get(layer, "#111"), lw=LW.get(layer, 0.8)))
            elif kind == "text":
                _, a, b, s, h, layer, ha, va, rot = it
                ax.text(a, b, s, fontsize=max(h * k * 1.15, 3.5), fontproperties=_fp, color=MPL_COLORS.get(layer, "#111"), ha=ha, va=va, rotation=rot, rotation_mode="anchor")
        buf = io.BytesIO()
        fig.savefig(buf, format="png", dpi=dpi, facecolor="white")
        plt.close(fig)
        return buf.getvalue()

    def translated(self, dx: float, dy: float, k: float = 1.0) -> "Drawing":
        """Bản sao đã phóng k lần rồi tịnh tiến (dùng ghép nhiều hình vào một bản vẽ DXF)."""
        d = Drawing(self.th * k, self.name)
        for it in self.items:
            t = it[0]
            if t == "line":
                d.items.append(("line", it[1] * k + dx, it[2] * k + dy, it[3] * k + dx, it[4] * k + dy, it[5], it[6]))
            elif t == "pline":
                d.items.append(("pline", [(p[0] * k + dx, p[1] * k + dy) for p in it[1]], it[2], it[3], it[4]))
            elif t == "circle":
                d.items.append(("circle", it[1] * k + dx, it[2] * k + dy, it[3] * k, it[4], it[5]))
            elif t == "arc":
                d.items.append(("arc", it[1] * k + dx, it[2] * k + dy, it[3] * k, it[4], it[5], it[6]))
            elif t == "text":
                d.items.append(("text", it[1] * k + dx, it[2] * k + dy, it[3], it[4] * k, *it[5:]))
        return d


def diagram(d: Drawing, xs, vals, x0, y0, scale, layer="BIEU_DO", label_fn=None, flip=False, n_hatch=24):
    """Vẽ biểu đồ nội lực ngang: đường cong, đường gạch tung độ.

    xs: toạ độ mm từ x0; vals: giá trị; ordinate = val*scale (dương vẽ xuống nếu flip).
    """
    sign = -1 if flip else 1
    pts = [(x0 + x, y0 + sign * v * scale) for x, v in zip(xs, vals)]
    d.pline(pts, layer)
    d.line(x0 + xs[0], y0, x0 + xs[-1], y0, "KET_CAU")
    d.line(pts[0][0], y0, pts[0][0], pts[0][1], layer)
    d.line(pts[-1][0], y0, pts[-1][0], pts[-1][1], layer)
    L = xs[-1] - xs[0]
    if L <= 0:
        return
    step = L / n_hatch
    k = 0
    for i in range(1, n_hatch):
        xq = xs[0] + i * step
        while k < len(xs) - 2 and xs[k + 1] < xq:
            k += 1
        dx = xs[k + 1] - xs[k]
        t = 0 if dx == 0 else (xq - xs[k]) / dx
        v = vals[k] + t * (vals[k + 1] - vals[k])
        if abs(v * scale) > d.th * 0.05:
            d.line(x0 + xq, y0, x0 + xq, y0 + sign * v * scale, layer)

"""Xuất DXF (ASCII, AC1009). Không phụ thuộc thư viện ngoài; đơn vị mm.

Chữ tiếng Việt ghi bằng \\U+XXXX, AutoCAD hiển thị đúng với kiểu chữ Arial.
Mỗi loại nét một layer: BE_TONG, THEP, DAI, KICH_THUOC, CHU, KY_HIEU, BIEU_DO...
"""
from __future__ import annotations
import math
from ..core.drawing import Drawing, LAYER_COLORS
from ..core.report import Report


def _enc(s: str) -> str:
    out = ""
    for ch in s:
        c = ord(ch)
        out += ch if 32 <= c < 127 else f"\\U+{c:04X}"
    return out


class W:
    def __init__(self):
        self.b = []

    def g(self, code, val):
        self.b.append(f"{code:>3}\n{val}\n")

    def s(self):
        return "".join(self.b)


def _f(x):
    return f"{x:.4f}"


def _header(w: W, ext):
    w.g(0, "SECTION"); w.g(2, "HEADER")
    w.g(9, "$ACADVER"); w.g(1, "AC1009")
    w.g(9, "$INSBASE"); w.g(10, _f(0)); w.g(20, _f(0)); w.g(30, _f(0))
    w.g(9, "$EXTMIN"); w.g(10, _f(ext[0])); w.g(20, _f(ext[1]))
    w.g(9, "$EXTMAX"); w.g(10, _f(ext[2])); w.g(20, _f(ext[3]))
    w.g(9, "$LIMMIN"); w.g(10, _f(ext[0])); w.g(20, _f(ext[1]))
    w.g(9, "$LIMMAX"); w.g(10, _f(ext[2])); w.g(20, _f(ext[3]))
    w.g(9, "$LTSCALE"); w.g(40, _f(max((ext[2] - ext[0]) / 300, 1)))
    w.g(9, "$TEXTSTYLE"); w.g(7, "VN")
    w.g(0, "ENDSEC")


def _tables(w: W, layers):
    w.g(0, "SECTION"); w.g(2, "TABLES")
    w.g(0, "TABLE"); w.g(2, "LTYPE"); w.g(70, 2)
    w.g(0, "LTYPE"); w.g(2, "CONTINUOUS"); w.g(70, 0); w.g(3, "Solid line"); w.g(72, 65); w.g(73, 0); w.g(40, _f(0))
    w.g(0, "LTYPE"); w.g(2, "DASHED"); w.g(70, 0); w.g(3, "Dashed"); w.g(72, 65); w.g(73, 2); w.g(40, _f(1)); w.g(49, _f(0.6)); w.g(49, _f(-0.4))
    w.g(0, "ENDTAB")
    w.g(0, "TABLE"); w.g(2, "LAYER"); w.g(70, len(layers))
    for nm in layers:
        w.g(0, "LAYER"); w.g(2, nm); w.g(70, 0); w.g(62, LAYER_COLORS.get(nm, 7)); w.g(6, "CONTINUOUS")
    w.g(0, "ENDTAB")
    w.g(0, "TABLE"); w.g(2, "STYLE"); w.g(70, 1)
    w.g(0, "STYLE"); w.g(2, "VN"); w.g(70, 0); w.g(40, _f(0)); w.g(41, _f(1)); w.g(50, _f(0)); w.g(71, 0); w.g(42, _f(2.5)); w.g(3, "arial.ttf"); w.g(4, "")
    w.g(0, "ENDTAB")
    w.g(0, "ENDSEC")


def _pline(w: W, pts, layer, closed=False, dash=False, width=None, bulge=None):
    w.g(0, "POLYLINE"); w.g(8, layer); w.g(66, 1); w.g(70, 1 if closed else 0)
    if dash:
        w.g(6, "DASHED")
    if width is not None:
        w.g(40, _f(width)); w.g(41, _f(width))
    for p in pts:
        w.g(0, "VERTEX"); w.g(8, layer); w.g(10, _f(p[0])); w.g(20, _f(p[1])); w.g(30, _f(0))
        if bulge is not None:
            w.g(42, _f(bulge))
    w.g(0, "SEQEND"); w.g(8, layer)


def _entities(w: W, d: Drawing):
    for it in d.items:
        k = it[0]
        if k == "line":
            _, a, b, c, e, layer, dash = it
            w.g(0, "LINE"); w.g(8, layer)
            if dash:
                w.g(6, "DASHED")
            w.g(10, _f(a)); w.g(20, _f(b)); w.g(30, _f(0)); w.g(11, _f(c)); w.g(21, _f(e)); w.g(31, _f(0))
        elif k == "pline":
            _, pts, layer, closed, dash = it
            _pline(w, pts, layer, closed, dash)
        elif k == "circle":
            _, a, b, r, layer, fill = it
            if fill:
                # hình vành khăn đặc biểu diễn thanh thép / chấm nhãn
                _pline(w, [(a - r / 2, b), (a + r / 2, b)], layer, True, False, r, 1.0)
            else:
                w.g(0, "CIRCLE"); w.g(8, layer); w.g(10, _f(a)); w.g(20, _f(b)); w.g(30, _f(0)); w.g(40, _f(r))
        elif k == "arc":
            _, a, b, r, a1, a2, layer = it
            w.g(0, "ARC"); w.g(8, layer); w.g(10, _f(a)); w.g(20, _f(b)); w.g(30, _f(0)); w.g(40, _f(r)); w.g(50, _f(a1)); w.g(51, _f(a2))
        elif k == "text":
            _, a, b, s, h, layer, ha, va, rot = it
            w.g(0, "TEXT"); w.g(8, layer); w.g(10, _f(a)); w.g(20, _f(b)); w.g(30, _f(0))
            w.g(40, _f(h)); w.g(1, _enc(s)); w.g(50, _f(rot)); w.g(7, "VN")
            w.g(72, {"left": 0, "center": 1, "right": 2}[ha])
            w.g(73, 2)
            w.g(11, _f(a)); w.g(21, _f(b)); w.g(31, _f(0))


def compose(r: Report) -> Drawing:
    """Ghép mọi hình của bài vào một bản vẽ, xếp thành hàng dọc, mỗi hình có chú thích."""
    y = 0.0
    out = Drawing(100, "ban_ve")
    for dr, cap in r.drawings:
        k = getattr(dr, "cad_scale", 1.0)
        x0, y0, x1, y1 = dr.extents()
        moved = dr.translated(-x0 * k, y - y1 * k, k)
        out.items += moved.items
        th = dr.th * k * 1.1
        out.text(0, y + th * 1.4, cap, th, "CHU", ha="left")
        y = y - (y1 - y0) * k - th * 6
    return out


class W:
    def __init__(self):
        self.b = []

    def g(self, code, val):
        self.b.append(f"{code:>3}\n{val}\n")

    def s(self):
        return "".join(self.b)


def _f(x):
    return f"{x:.4f}"


def _header(w: W, ext):
    w.g(0, "SECTION"); w.g(2, "HEADER")
    w.g(9, "$ACADVER"); w.g(1, "AC1009")
    w.g(9, "$INSBASE"); w.g(10, _f(0)); w.g(20, _f(0)); w.g(30, _f(0))
    w.g(9, "$EXTMIN"); w.g(10, _f(ext[0])); w.g(20, _f(ext[1]))
    w.g(9, "$EXTMAX"); w.g(10, _f(ext[2])); w.g(20, _f(ext[3]))
    w.g(9, "$LIMMIN"); w.g(10, _f(ext[0])); w.g(20, _f(ext[1]))
    w.g(9, "$LIMMAX"); w.g(10, _f(ext[2])); w.g(20, _f(ext[3]))
    w.g(9, "$LTSCALE"); w.g(40, _f(max((ext[2] - ext[0]) / 300, 1)))
    w.g(9, "$TEXTSTYLE"); w.g(7, "VN")
    w.g(0, "ENDSEC")


def _tables(w: W, layers):
    w.g(0, "SECTION"); w.g(2, "TABLES")
    w.g(0, "TABLE"); w.g(2, "LTYPE"); w.g(70, 2)
    w.g(0, "LTYPE"); w.g(2, "CONTINUOUS"); w.g(70, 0); w.g(3, "Solid line"); w.g(72, 65); w.g(73, 0); w.g(40, _f(0))
    w.g(0, "LTYPE"); w.g(2, "DASHED"); w.g(70, 0); w.g(3, "Dashed"); w.g(72, 65); w.g(73, 2); w.g(40, _f(1)); w.g(49, _f(0.6)); w.g(49, _f(-0.4))
    w.g(0, "ENDTAB")
    w.g(0, "TABLE"); w.g(2, "LAYER"); w.g(70, len(layers))
    for nm in layers:
        w.g(0, "LAYER"); w.g(2, nm); w.g(70, 0); w.g(62, LAYER_COLORS.get(nm, 7)); w.g(6, "CONTINUOUS")
    w.g(0, "ENDTAB")
    w.g(0, "TABLE"); w.g(2, "STYLE"); w.g(70, 1)
    w.g(0, "STYLE"); w.g(2, "VN"); w.g(70, 0); w.g(40, _f(0)); w.g(41, _f(1)); w.g(50, _f(0)); w.g(71, 0); w.g(42, _f(2.5)); w.g(3, "arial.ttf"); w.g(4, "")
    w.g(0, "ENDTAB")
    w.g(0, "ENDSEC")


def _pline(w: W, pts, layer, closed=False, dash=False, width=None, bulge=None):
    w.g(0, "POLYLINE"); w.g(8, layer); w.g(66, 1); w.g(70, 1 if closed else 0)
    if dash:
        w.g(6, "DASHED")
    if width is not None:
        w.g(40, _f(width)); w.g(41, _f(width))
    for p in pts:
        w.g(0, "VERTEX"); w.g(8, layer); w.g(10, _f(p[0])); w.g(20, _f(p[1])); w.g(30, _f(0))
        if bulge is not None:
            w.g(42, _f(bulge))
    w.g(0, "SEQEND"); w.g(8, layer)


def _entities(w: W, d: Drawing):
    for it in d.items:
        k = it[0]
        if k == "line":
            _, a, b, c, e, layer, dash = it
            w.g(0, "LINE"); w.g(8, layer)
            if dash:
                w.g(6, "DASHED")
            w.g(10, _f(a)); w.g(20, _f(b)); w.g(30, _f(0)); w.g(11, _f(c)); w.g(21, _f(e)); w.g(31, _f(0))
        elif k == "pline":
            _, pts, layer, closed, dash = it
            _pline(w, pts, layer, closed, dash)
        elif k == "circle":
            _, a, b, r, layer, fill = it
            if fill:
                # hình vành khăn đặc biểu diễn thanh thép / chấm nhãn
                _pline(w, [(a - r / 2, b), (a + r / 2, b)], layer, True, False, r, 1.0)
            else:
                w.g(0, "CIRCLE"); w.g(8, layer); w.g(10, _f(a)); w.g(20, _f(b)); w.g(30, _f(0)); w.g(40, _f(r))
        elif k == "arc":
            _, a, b, r, a1, a2, layer = it
            w.g(0, "ARC"); w.g(8, layer); w.g(10, _f(a)); w.g(20, _f(b)); w.g(30, _f(0)); w.g(40, _f(r)); w.g(50, _f(a1)); w.g(51, _f(a2))
        elif k == "text":
            _, a, b, s, h, layer, ha, va, rot = it
            w.g(0, "TEXT"); w.g(8, layer); w.g(10, _f(a)); w.g(20, _f(b)); w.g(30, _f(0))
            w.g(40, _f(h)); w.g(1, _enc(s)); w.g(50, _f(rot)); w.g(7, "VN")
            w.g(72, {"left": 0, "center": 1, "right": 2}[ha])
            w.g(73, 2)
            w.g(11, _f(a)); w.g(21, _f(b)); w.g(31, _f(0))


def compose(r: Report) -> Drawing:
    """Ghép mọi hình của bài vào một bản vẽ, xếp thành hàng dọc, mỗi hình có chú thích."""
    y = 0.0
    out = Drawing(100, "ban_ve")
    ref_h = None
    for dr, cap in r.drawings:
        x0, y0, x1, y1 = dr.extents()
        moved = dr.translated(-x0, y - y1)
        out.items += moved.items
        th = dr.th * 1.1
        out.text(0, y + th * 1.4, cap, th, "CHU", ha="left")
        y = y - (y1 - y0) - th * 6
    return out


def export(r: Report) -> bytes:
    d = compose(r)
    layers = sorted({"0"} | {it[5] for it in d.items if it[0] in ("line",)} | {it[2] for it in d.items if it[0] == "pline"} | {it[4] for it in d.items if it[0] == "circle"} | {it[6] for it in d.items if it[0] == "arc"} | {it[5] for it in d.items if it[0] == "text"})
    w = W()
    ext = d.extents()
    _header(w, ext)
    _tables(w, layers)
    w.g(0, "SECTION"); w.g(2, "ENTITIES")
    _entities(w, d)
    w.g(0, "ENDSEC")
    w.g(0, "EOF")
    return w.s().encode("ascii")

"""Ngôn ngữ công thức gọn, an toàn với f-string.

Cú pháp:
    R_b, R_(b,ser)      chỉ số dưới
    L^2, 10^(-3)        chỉ số trên
    [[ tử // mẫu ]]     phân số
    √[[ biểu thức ]]    căn bậc hai
Mọi ký tự còn lại giữ nguyên (Unicode: φ, ξ, ≤, ·, ...).
"""
from __future__ import annotations
from dataclasses import dataclass, field
from html import escape as _esc
import unicodedata
from typing import List, Optional, Union


@dataclass
class T:
    s: str


@dataclass
class Script:
    base: str
    sub: Optional[list] = None
    sup: Optional[list] = None


@dataclass
class Frac:
    num: list
    den: list


@dataclass
class Sqrt:
    body: list


Node = Union[T, Script, Frac, Sqrt]
_WORD_EXTRA = ",."


def _is_word(c: str) -> bool:
    return c.isalnum() or unicodedata.category(c) == "Mn"


def _match_close(s: str, i: int) -> int:
    """i trỏ ngay sau '[['; trả về vị trí của ']]' tương ứng."""
    depth = 1
    j = i
    while j < len(s) - 1:
        if s.startswith("[[", j):
            depth += 1
            j += 2
            continue
        if s.startswith("]]", j):
            depth -= 1
            if depth == 0:
                return j
            j += 2
            continue
        j += 1
    raise ValueError(f"Thiếu ']]' trong công thức: {s}")


def _split_frac(s: str):
    depth = 0
    j = 0
    while j < len(s) - 1:
        if s.startswith("[[", j):
            depth += 1
            j += 2
            continue
        if s.startswith("]]", j):
            depth -= 1
            j += 2
            continue
        if depth == 0 and s.startswith("//", j):
            return s[:j], s[j + 2:]
        j += 1
    return None


def _read_script(s: str, i: int):
    """Đọc phần chỉ số tại s[i:]; trả về (nodes, vị trí mới)."""
    if i < len(s) and s[i] == "(":
        depth = 1
        j = i + 1
        while j < len(s) and depth:
            if s[j] == "(":
                depth += 1
            elif s[j] == ")":
                depth -= 1
            j += 1
        return parse(s[i + 1 : j - 1]), j
    j = i
    if j < len(s) and s[j] in "-−":
        j += 1
    while j < len(s) and _is_word(s[j]):
        j += 1
    return [T(s[i:j])], j


def parse(s: str) -> List[Node]:
    out: List[Node] = []
    buf = ""
    i = 0

    def flush():
        nonlocal buf
        if buf:
            out.append(T(buf))
            buf = ""

    while i < len(s):
        if s.startswith("√[[", i):
            flush()
            e = _match_close(s, i + 3)
            out.append(Sqrt(parse(s[i + 3 : e].strip())))
            i = e + 2
            continue
        if s.startswith("[[", i):
            flush()
            e = _match_close(s, i + 2)
            inner = s[i + 2 : e]
            parts = _split_frac(inner)
            if parts is None:
                raise ValueError(f"Phân số thiếu '//' : {inner}")
            out.append(Frac(parse(parts[0].strip()), parse(parts[1].strip())))
            i = e + 2
            continue
        if s[i] in "_^" and i + 1 < len(s) and (buf or (out and isinstance(out[-1], (Frac, Sqrt)))):
            # tách base là từ cuối trong buf
            k = len(buf)
            while k > 0 and _is_word(buf[k - 1]):
                k -= 1
            if k == len(buf) and buf:
                k = len(buf) - 1  # ví dụ ')' hoặc ký hiệu đơn
            base = buf[k:]
            buf = buf[:k]
            flush()
            sub = sup = None
            while i < len(s) and s[i] in "_^" and i + 1 < len(s):
                kind = s[i]
                val, i = _read_script(s, i + 1)
                if kind == "_":
                    sub = val
                else:
                    sup = val
            out.append(Script(base, sub, sup))
            continue
        buf += s[i]
        i += 1
    flush()
    return out


# ---------- HTML ----------
def to_html(s: str) -> str:
    return _html(parse(s))


def _html(nodes) -> str:
    r = ""
    for n in nodes:
        if isinstance(n, T):
            r += _esc(n.s)
        elif isinstance(n, Script):
            r += _esc(n.base)
            if n.sub is not None and n.sup is not None:
                r += f'<span class="ss"><sup>{_html(n.sup)}</sup><sub>{_html(n.sub)}</sub></span>'
            elif n.sub is not None:
                r += f"<sub>{_html(n.sub)}</sub>"
            elif n.sup is not None:
                r += f"<sup>{_html(n.sup)}</sup>"
        elif isinstance(n, Frac):
            r += f'<span class="fr"><span class="nu">{_html(n.num)}</span><span class="de">{_html(n.den)}</span></span>'
        elif isinstance(n, Sqrt):
            r += f'<span class="rt">√<span class="ov">{_html(n.body)}</span></span>'
    return r


# ---------- Chuỗi thuần (bảng, tên hình) ----------
def to_plain(s: str) -> str:
    return _plain(parse(s))


_SUBS = str.maketrans("0123456789", "₀₁₂₃₄₅₆₇₈₉")
_SUPS = str.maketrans("0123456789-−", "⁰¹²³⁴⁵⁶⁷⁸⁹⁻⁻")


def _plain(nodes) -> str:
    r = ""
    for n in nodes:
        if isinstance(n, T):
            r += n.s
        elif isinstance(n, Script):
            r += n.base
            if n.sub is not None:
                r += _plain(n.sub)
            if n.sup is not None:
                p = _plain(n.sup)
                r += p.translate(_SUPS) if all(c in "0123456789-−" for c in p) else "^" + p
        elif isinstance(n, Frac):
            r += f"({_plain(n.num)})/({_plain(n.den)})"
        elif isinstance(n, Sqrt):
            r += f"√({_plain(n.body)})"
    return r


def to_runs(s: str):
    """Dạng [(text, 'sub'|'sup'|None)] cho văn bản thường trong bảng/đoạn."""
    runs = []
    for n in parse(s):
        if isinstance(n, T):
            runs.append((n.s, None))
        elif isinstance(n, Script):
            runs.append((n.base, None))
            if n.sub is not None:
                runs.append((_plain(n.sub), "sub"))
            if n.sup is not None:
                runs.append((_plain(n.sup), "sup"))
        else:
            runs.append((_plain([n]), None))
    return runs


# ---------- OMML cho Word ----------
_M = 'xmlns:m="http://schemas.openxmlformats.org/officeDocument/2006/math"'
_W = 'xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"'


def _xml(t: str) -> str:
    return t.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def _mr(t: str) -> str:
    return (
        '<m:r><m:rPr><m:sty m:val="p"/></m:rPr>'
        '<w:rPr><w:rFonts w:ascii="Cambria Math" w:hAnsi="Cambria Math"/></w:rPr>'
        f'<m:t xml:space="preserve">{_xml(t)}</m:t></m:r>'
    )


def _omml(nodes) -> str:
    r = ""
    for n in nodes:
        if isinstance(n, T):
            r += _mr(n.s)
        elif isinstance(n, Script):
            base = _mr(n.base) if n.base else _mr("​")
            if n.sub is not None and n.sup is not None:
                r += f"<m:sSubSup><m:e>{base}</m:e><m:sub>{_omml(n.sub)}</m:sub><m:sup>{_omml(n.sup)}</m:sup></m:sSubSup>"
            elif n.sub is not None:
                r += f"<m:sSub><m:e>{base}</m:e><m:sub>{_omml(n.sub)}</m:sub></m:sSub>"
            else:
                r += f"<m:sSup><m:e>{base}</m:e><m:sup>{_omml(n.sup)}</m:sup></m:sSup>"
        elif isinstance(n, Frac):
            r += f"<m:f><m:num>{_omml(n.num)}</m:num><m:den>{_omml(n.den)}</m:den></m:f>"
        elif isinstance(n, Sqrt):
            r += f'<m:rad><m:radPr><m:degHide m:val="1"/></m:radPr><m:deg/><m:e>{_omml(n.body)}</m:e></m:rad>'
    return r


def to_omml(s: str) -> str:
    return f"<m:oMath {_M} {_W}>{_omml(parse(s))}</m:oMath>"


# ---------- Dàn trang hộp cho PDF / ảnh ----------
@dataclass
class Box:
    w: float
    asc: float
    desc: float
    ops: list = field(default_factory=list)  # ('text',x,y,str,size) | ('line',x1,y1,x2,y2,lw)  y hướng lên


def layout(s: str, size: float, measure) -> Box:
    return _layout(parse(s), size, measure)


def _shift(ops, dx, dy):
    out = []
    for o in ops:
        if o[0] == "text":
            out.append(("text", o[1] + dx, o[2] + dy, o[3], o[4]))
        else:
            out.append(("line", o[1] + dx, o[2] + dy, o[3] + dx, o[4] + dy, o[5]))
    return out


def _hcat(boxes) -> Box:
    x = 0.0
    asc = 0.0
    desc = 0.0
    ops = []
    for b in boxes:
        ops += _shift(b.ops, x, 0)
        x += b.w
        asc = max(asc, b.asc)
        desc = max(desc, b.desc)
    return Box(x, asc, desc, ops)


def _text(t, size, measure):
    return Box(measure(t, size), size * 0.78, size * 0.24, [("text", 0, 0, t, size)])


def _layout(nodes, size, measure) -> Box:
    boxes = []
    for n in nodes:
        if isinstance(n, T):
            boxes.append(_text(n.s, size, measure))
        elif isinstance(n, Script):
            base = _text(n.base, size, measure)
            ss = max(size * 0.68, 5.5)
            w = base.w
            ops = list(base.ops)
            asc, desc = base.asc, base.desc
            sw = 0
            if n.sub is not None:
                b = _layout(n.sub, ss, measure)
                ops += _shift(b.ops, w, -size * 0.22)
                desc = max(desc, b.desc + size * 0.22)
                sw = max(sw, b.w)
            if n.sup is not None:
                b = _layout(n.sup, ss, measure)
                ops += _shift(b.ops, w, size * 0.40)
                asc = max(asc, b.asc + size * 0.40)
                sw = max(sw, b.w)
            boxes.append(Box(w + sw, asc, desc, ops))
        elif isinstance(n, Frac):
            fs = max(size * 0.92, 5.5)
            a = _layout(n.num, fs, measure)
            b = _layout(n.den, fs, measure)
            w = max(a.w, b.w) + size * 0.3
            axis = size * 0.28
            gap = size * 0.14
            ops = _shift(a.ops, (w - a.w) / 2, axis + gap + a.desc)
            ops += _shift(b.ops, (w - b.w) / 2, axis - gap - b.asc)
            ops.append(("line", size * 0.05, axis, w - size * 0.05, axis, max(size * 0.05, 0.4)))
            pad = size * 0.12
            boxes.append(
                Box(w + 2 * pad, axis + gap + a.desc + a.asc, -(axis - gap - b.asc - b.desc), _shift(ops, pad, 0))
            )
        elif isinstance(n, Sqrt):
            b = _layout(n.body, size, measure)
            rw = measure("√", size)
            top = b.asc + size * 0.1
            ops = [("text", 0, 0, "√", size)] + _shift(b.ops, rw, 0)
            ops.append(("line", rw, top, rw + b.w, top, max(size * 0.05, 0.4)))
            boxes.append(Box(rw + b.w, top + size * 0.05, b.desc, ops))
    if not boxes:
        return Box(0, size * 0.78, size * 0.24, [])
    return _hcat(boxes)

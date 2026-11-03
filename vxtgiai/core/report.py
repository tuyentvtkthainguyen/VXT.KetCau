"""Mô hình lời giải: danh sách khối độc lập với định dạng xuất."""
from __future__ import annotations
import math
from dataclasses import dataclass, field
from typing import Any, List, Optional


def fmt(x: float, d: Optional[int] = None) -> str:
    """Định dạng số kiểu Việt Nam: dấu phẩy thập phân, dấu trừ chuẩn."""
    if isinstance(x, str):
        return x
    if x is None:
        return "–"
    if isinstance(x, (int,)) and not isinstance(x, bool):
        s = str(abs(x))
        return ("−" if x < 0 else "") + s
    if not math.isfinite(x):
        return "∞"
    a = abs(x)
    if d is None:
        if a == 0:
            d = 0
        elif a >= 1000:
            d = 0
        elif a >= 100:
            d = 1
        elif a >= 10:
            d = 2
        elif a >= 0.1:
            d = 3
        else:
            d = max(3, 2 - int(math.floor(math.log10(a))))
    s = f"{a:.{d}f}"
    if "." in s:
        s = s.rstrip("0").rstrip(".")
    if float(s) == 0:
        return "0"
    return ("−" if x < 0 else "") + s.replace(".", ",")


@dataclass
class Block:
    kind: str
    data: dict = field(default_factory=dict)


class Report:
    def __init__(self, title: str, module: str, standards: Optional[List[str]] = None):
        self.title = title
        self.module = module
        self.standards = standards or []
        self.blocks: List[Block] = []
        self.results: dict = {}   # kết quả số cho kiểm thử
        self.summary: List[str] = []  # kết luận cho trang tóm tắt
        self.drawings: list = []
        self.ok: Optional[bool] = None

    def _add(self, kind, **data):
        self.blocks.append(Block(kind, data))

    def h(self, text: str, level: int = 1):
        self._add("h", text=text, level=level)

    def p(self, text: str):
        """Đoạn văn; đoạn công thức nội dòng đặt trong $...$ (chỉ chỉ số trên/dưới)."""
        self._add("p", text=text)

    def f(self, expr: str):
        self._add("f", expr=expr)

    def calc(self, sym: str, formula: Optional[str], sub: Optional[str], val: Any, unit: str = "", note: str = "", key: Optional[str] = None, d: Optional[int] = None):
        """Một bước tính: ký hiệu = công thức = thay số = kết quả đơn vị."""
        parts = [sym]
        if formula:
            parts.append(formula)
        if sub:
            parts.append(sub)
        v = fmt(val, d) if not isinstance(val, str) else val
        parts.append(f"{v}" + (f" {unit}" if unit else ""))
        expr = " = ".join(parts)
        self._add("f", expr=expr, note=note)
        if key:
            self.results[key] = val

    def table(self, headers: List[str], rows: List[list], caption: str = "", widths: Optional[List[float]] = None):
        rows2 = [[fmt(c) if isinstance(c, (int, float)) and not isinstance(c, bool) else str(c) for c in r] for r in rows]
        self._add("table", headers=headers, rows=rows2, caption=caption, widths=widths)

    def fig(self, drawing, caption: str = "", cad: bool = True, width_cm: float = 15.5):
        self._add("fig", drawing=drawing, caption=caption, width_cm=width_cm)
        if cad:
            self.drawings.append((drawing, caption))

    def check(self, name: str, expr: str, ok: bool, key: Optional[str] = None):
        self._add("check", name=name, expr=expr, ok=bool(ok))
        if self.ok is None:
            self.ok = bool(ok)
        else:
            self.ok = self.ok and bool(ok)
        if key:
            self.results[key] = bool(ok)

    def note(self, text: str):
        self._add("note", text=text)

    def warn(self, text: str):
        self._add("warn", text=text)

    def conclude(self, text: str):
        self.summary.append(text)

    def given(self, rows: List[tuple]):
        """Bảng số liệu đề bài: (tên, ký hiệu, giá trị, đơn vị)."""
        self.h("Số liệu đề bài")
        self.table(["Đại lượng", "Ký hiệu", "Giá trị", "Đơn vị"], [list(r) for r in rows], widths=[8.5, 2.5, 3, 2.5])

    def finish(self):
        self.h("Kết luận")
        for s in self.summary:
            self._add("result", text=s)
        return self


def ratio_txt(a, b) -> str:
    return fmt(a / b if b else float("inf"), 3)

"""Xuất PDF vector: công thức vẽ trực tiếp, chữ tiếng Việt nhúng phông."""
from __future__ import annotations
import io
import os
import re
from html import escape
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_JUSTIFY
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import cm
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Flowable, Image, KeepTogether, Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle
from ..core.drawing import FONT_DIR
from ..core.mathmk import layout, to_runs
from ..core.report import Report

_REG = False
ACCENT = colors.HexColor("#0F4C5C")


def _fonts():
    global _REG
    if _REG:
        return
    for nm, f in (("VN", "LiberationSerif-Regular.ttf"), ("VN-B", "LiberationSerif-Bold.ttf"), ("VN-I", "LiberationSerif-Italic.ttf"), ("VN-BI", "LiberationSerif-BoldItalic.ttf")):
        pdfmetrics.registerFont(TTFont(nm, os.path.join(FONT_DIR, f)))
    pdfmetrics.registerFontFamily("VN", normal="VN", bold="VN-B", italic="VN-I", boldItalic="VN-BI")
    _REG = True


def rich(text: str) -> str:
    out = ""
    for part in re.split(r"(\$[^$]+\$)", text):
        if part.startswith("$") and part.endswith("$") and len(part) > 1:
            for t, m in to_runs(part[1:-1]):
                e = escape(t)
                out += f"<sub>{e}</sub>" if m == "sub" else (f"<super>{e}</super>" if m == "sup" else e)
        else:
            out += escape(part)
    return out


def _measure(t, size):
    return pdfmetrics.stringWidth(t, "VN", size)


class Formula(Flowable):
    def __init__(self, expr, size=11.5, indent=14, note=None, avail=None):
        super().__init__()
        self.box = layout(expr, size, _measure)
        self.indent = indent
        self.note = note
        self.size = size

    def wrap(self, aw, ah):
        need = self.box.w + self.indent
        self.k = min(1.0, (aw - 2) / need) if need > 0 else 1.0
        self.width = aw
        self.height = (self.box.asc + self.box.desc) * self.k + 7
        return aw, self.height

    def draw(self):
        c = self.canv
        c.saveState()
        k = self.k
        c.translate(self.indent * k, (self.box.desc * k) + 3.5)
        c.scale(k, k)
        for o in self.box.ops:
            if o[0] == "text":
                c.setFont("VN", o[4])
                c.drawString(o[1], o[2], o[3])
            else:
                c.setLineWidth(o[5])
                c.line(o[1], o[2], o[3], o[4])
        c.restoreState()


def export(r: Report) -> bytes:
    _fonts()
    buf = io.BytesIO()

    def foot(canvas, doc):
        canvas.saveState()
        canvas.setFont("VN", 9.5)
        canvas.setFillColor(colors.HexColor("#666666"))
        canvas.drawString(3 * cm, 1.2 * cm, r.title)
        canvas.drawRightString(A4[0] - 2 * cm, 1.2 * cm, f"Trang {doc.page}")
        canvas.restoreState()

    doc = SimpleDocTemplate(buf, pagesize=A4, leftMargin=3 * cm, rightMargin=2 * cm, topMargin=2 * cm, bottomMargin=2 * cm, title=r.title, author="VXT Giải Kết Cấu")
    base = ParagraphStyle("b", fontName="VN", fontSize=12, leading=16, spaceAfter=5, alignment=TA_JUSTIFY)
    H = {1: ParagraphStyle("h1", parent=base, fontName="VN-B", fontSize=13.5, leading=18, spaceBefore=12, spaceAfter=5, textColor=ACCENT, alignment=0, keepWithNext=1),
         2: ParagraphStyle("h2", parent=base, fontName="VN-B", fontSize=12.5, leading=16, spaceBefore=8, spaceAfter=4, textColor=ACCENT, alignment=0, keepWithNext=1),
         3: ParagraphStyle("h3", parent=base, fontName="VN-BI", fontSize=12, leading=16, spaceBefore=5, spaceAfter=3, alignment=0, keepWithNext=1)}
    cap = ParagraphStyle("c", parent=base, fontName="VN-I", fontSize=10.5, leading=13, alignment=TA_CENTER, textColor=colors.HexColor("#444444"))
    fnote = ParagraphStyle("fn", parent=base, fontName="VN-I", fontSize=10.5, leading=13, leftIndent=14, textColor=colors.HexColor("#555555"), spaceAfter=3, alignment=0)
    S = []
    S.append(Paragraph(escape(r.module.upper()), ParagraphStyle("m", parent=base, fontName="VN-B", fontSize=10.5, textColor=ACCENT, spaceAfter=0)))
    S.append(Paragraph(escape(r.title), ParagraphStyle("t", parent=base, fontName="VN-B", fontSize=18, leading=23, spaceAfter=4, alignment=0)))
    if r.standards:
        S.append(Paragraph("Căn cứ: " + escape("; ".join(r.standards)), ParagraphStyle("s", parent=base, fontName="VN-I", fontSize=10.5, leading=14)))
    if r.ok is not None:
        S.append(Paragraph("Kết quả chung: " + ("tất cả điều kiện kiểm tra ĐẠT" if r.ok else "có điều kiện KHÔNG ĐẠT"), ParagraphStyle("v", parent=base, fontName="VN-B", textColor=colors.HexColor("#1B7A3E") if r.ok else colors.HexColor("#B71C1C"))))
    W = A4[0] - 5 * cm
    for b in r.blocks:
        k, d = b.kind, b.data
        if k == "h":
            S.append(Paragraph(escape(d["text"]), H[min(d["level"], 3)]))
        elif k == "p":
            S.append(Paragraph(rich(d["text"]), base))
        elif k == "f":
            S.append(Formula(d["expr"]))
            if d.get("note"):
                S.append(Paragraph(rich(d["note"]), fnote))
        elif k == "table":
            n = len(d["headers"])
            sz = 10 if n <= 6 else (9 if n <= 8 else 7.6)
            cs = ParagraphStyle("td", fontName="VN", fontSize=sz, leading=sz + 2.2)
            hs = ParagraphStyle("th", fontName="VN-B", fontSize=sz, leading=sz + 2.2, alignment=TA_CENTER)
            data = [[Paragraph(rich(h), hs) for h in d["headers"]]]
            for row in d["rows"]:
                data.append([Paragraph(rich(c), cs) for c in row])
            if d.get("widths"):
                sc = min(W / (sum(d["widths"]) * cm), 1.0)
                cw = [w * cm * sc for w in d["widths"][:n]]
            else:
                cw = [W / n] * n
            t = Table(data, colWidths=cw, repeatRows=1)
            t.setStyle(TableStyle([("GRID", (0, 0), (-1, -1), .4, colors.HexColor("#8A9BA0")), ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#E3EEF0")), ("VALIGN", (0, 0), (-1, -1), "MIDDLE"), ("LEFTPADDING", (0, 0), (-1, -1), 3), ("RIGHTPADDING", (0, 0), (-1, -1), 3), ("TOPPADDING", (0, 0), (-1, -1), 2), ("BOTTOMPADDING", (0, 0), (-1, -1), 2)]))
            S.append(t)
            if d.get("caption"):
                S.append(Spacer(1, 3))
                S.append(Paragraph(rich(d["caption"]), cap))
            S.append(Spacer(1, 8))
        elif k == "fig":
            png = d["drawing"].to_png(width_in=6.4, dpi=200)
            iw, ih = ImageReader(io.BytesIO(png)).getSize()
            w = min(d.get("width_cm", 15.5) * cm, W)
            h = w * ih / iw
            mh = (A4[1] - 4 * cm) * 0.82
            if h > mh:
                w, h = w * mh / h, mh
            S.append(KeepTogether([Image(io.BytesIO(png), width=w, height=h), Spacer(1, 2), Paragraph(escape(d["caption"]), cap), Spacer(1, 6)]))
        elif k == "check":
            col = "#1B7A3E" if d["ok"] else "#B71C1C"
            lab = "ĐẠT" if d["ok"] else "KHÔNG ĐẠT"
            S.append(Paragraph(f'<font name="VN-B" color="{col}">{lab}</font>  {escape(d["name"])}:', ParagraphStyle("ck", parent=base, leftIndent=14, spaceAfter=0, alignment=0, fontSize=11.5)))
            S.append(Formula(d["expr"], indent=28))
        elif k in ("note", "warn"):
            bg = "#F1F3F4" if k == "note" else "#FFF3D6"
            pre = "Ghi chú: " if k == "note" else "Lưu ý: "
            t = Table([[Paragraph(pre + rich(d["text"]), ParagraphStyle("nt", parent=base, fontSize=10.5, leading=13.5, spaceAfter=0))]], colWidths=[W])
            t.setStyle(TableStyle([("BACKGROUND", (0, 0), (-1, -1), colors.HexColor(bg)), ("BOX", (0, 0), (-1, -1), .3, colors.HexColor("#BBBBBB"))]))
            S.append(t)
            S.append(Spacer(1, 7))
        elif k == "result":
            S.append(Paragraph("• " + rich(d["text"]), ParagraphStyle("res", parent=base, fontName="VN-B", leftIndent=12, firstLineIndent=-10, alignment=0)))
    doc.build(S, onFirstPage=foot, onLaterPages=foot)
    return buf.getvalue()

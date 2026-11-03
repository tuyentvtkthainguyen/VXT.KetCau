"""Xuất slide PowerPoint: tiêu đề, đề bài, từng bước, hình, kết luận."""
from __future__ import annotations
import io
import os
import re
from PIL import Image as PILImage, ImageDraw, ImageFont
from pptx import Presentation
from pptx.dml.color import RGBColor
from pptx.enum.shapes import MSO_SHAPE
from pptx.enum.text import MSO_ANCHOR, PP_ALIGN
from pptx.util import Emu, Inches, Pt
from ..core.drawing import FONT_DIR
from ..core.mathmk import layout, to_runs
from ..core.report import Report

INK = RGBColor(0x1C, 0x26, 0x2B)
ACC = RGBColor(0x0F, 0x4C, 0x5C)
MUTE = RGBColor(0x5A, 0x6A, 0x70)
OK = RGBColor(0x1B, 0x7A, 0x3E)
BAD = RGBColor(0xB7, 0x1C, 0x1C)
PAPER = RGBColor(0xF6, 0xF4, 0xEE)
FACE = "Times New Roman"
_F = os.path.join(FONT_DIR, "LiberationSerif-Regular.ttf")
_cache = {}
SW, SH = 13.333, 7.5
LM = 0.6
CW = SW - 2 * LM
TOP = 1.2
BOT = 7.05


def _font(sz):
    s = max(int(round(sz)), 6)
    if s not in _cache:
        _cache[s] = ImageFont.truetype(_F, s)
    return _cache[s]


def _measure(t, size):
    return _font(size).getlength(t)


def formula_png(expr, size=20, scale=3):
    box = layout(expr, size * scale, _measure)
    W = int(box.w + 8)
    H = int(box.asc + box.desc + 8)
    im = PILImage.new("RGBA", (max(W, 1), max(H, 1)), (255, 255, 255, 0))
    dr = ImageDraw.Draw(im)
    base = box.asc + 4
    for o in box.ops:
        if o[0] == "text":
            f = _font(o[4])
            dr.text((o[1] + 2, base - o[2]), o[3], font=f, fill=(28, 38, 43, 255), anchor="ls")
        else:
            dr.line([(o[1] + 2, base - o[2]), (o[3] + 2, base - o[4])], fill=(28, 38, 43, 255), width=max(int(o[5]), 1))
    b = io.BytesIO()
    im.save(b, "PNG")
    return b.getvalue(), W / scale, H / scale


class Deck:
    def __init__(self, r):
        self.r = r
        self.prs = Presentation()
        self.prs.slide_width = Inches(SW)
        self.prs.slide_height = Inches(SH)
        self.blank = self.prs.slide_layouts[6]
        self.s = None
        self.y = TOP
        self.sec = ""
        self.n = 0

    def _text(self, slide, x, y, w, h, text, size=18, bold=False, color=INK, align=PP_ALIGN.LEFT, italic=False, anchor=MSO_ANCHOR.TOP):
        tb = slide.shapes.add_textbox(Inches(x), Inches(y), Inches(w), Inches(h))
        tf = tb.text_frame
        tf.word_wrap = True
        tf.vertical_anchor = anchor
        tf.margin_left = tf.margin_right = tf.margin_top = tf.margin_bottom = 0
        p = tf.paragraphs[0]
        p.alignment = align
        self._runs(p, text, size, bold, color, italic)
        return tb

    def _runs(self, p, text, size, bold=False, color=INK, italic=False):
        for part in re.split(r"(\$[^$]+\$)", text):
            if part.startswith("$") and part.endswith("$") and len(part) > 1:
                for t, m in to_runs(part[1:-1]):
                    r = p.add_run()
                    r.text = t
                    self._style(r, size, bold, color, italic)
                    if m:
                        r.font._element.set("baseline", "-25000" if m == "sub" else "30000")
            elif part:
                r = p.add_run()
                r.text = part
                self._style(r, size, bold, color, italic)

    @staticmethod
    def _style(r, size, bold, color, italic):
        r.font.name = FACE
        r.font.size = Pt(size)
        r.font.bold = bold
        r.font.italic = italic
        r.font.color.rgb = color

    def new(self, title=None):
        s = self.prs.slides.add_slide(self.blank)
        self.s = s
        self.n += 1
        self.y = TOP
        t = title or self.sec
        self._text(s, LM, 0.38, CW, .6, t, 28, True, ACC, anchor=MSO_ANCHOR.MIDDLE)
        ln = s.shapes.add_shape(MSO_SHAPE.RECTANGLE, Inches(LM), Inches(1.0), Inches(CW), Emu(14000))
        ln.fill.solid()
        ln.fill.fore_color.rgb = ACC
        ln.line.fill.background()
        self._text(s, LM, 7.1, CW - 1, .3, self.r.title, 11, False, MUTE)
        self._text(s, SW - LM - 1, 7.1, 1, .3, str(self.n), 11, False, MUTE, PP_ALIGN.RIGHT)
        return s

    def ensure(self, h):
        if self.s is None or self.y + h > BOT:
            self.new(self.sec + (" (tiếp)" if self.s is not None and self.sec_started else ""))
            self.sec_started = True

    sec_started = False

    def para(self, text, size=18, **kw):
        lines = max(1, int(len(re.sub(r"[$_^]", "", text)) * size * 0.0072 / CW) + 1)
        h = lines * size * 1.3 / 72 + .08
        self.ensure(h)
        self._text(self.s, LM, self.y, CW, h, text, size, **kw)
        self.y += h

    def formula(self, expr, note=None):
        png, w, h = formula_png(expr, 20)
        wi, hi = w / 72, h / 72
        k = min(1.0, (CW - .2) / wi)
        wi *= k
        hi *= k
        need = hi + .1 + (.3 if note else 0)
        self.ensure(need)
        self.s.shapes.add_picture(io.BytesIO(png), Inches(LM + .15), Inches(self.y), width=Inches(wi), height=Inches(hi))
        self.y += hi + .1
        if note:
            self._text(self.s, LM + .15, self.y - .06, CW - .3, .3, note, 13, italic=True, color=MUTE)
            self.y += .28

    def table(self, headers, rows, caption="", widths=None):
        rows = list(rows)
        n = len(headers)
        fs = 15 if n <= 5 else (13 if n <= 7 else 11)
        if widths and sum(widths) > 0:
            cw = [w / sum(widths[:n]) * CW for w in widths[:n]]
        else:
            cw = [CW / n] * n

        def height(row, bold=False):
            lines = 1
            for v, w in zip(row, cw):
                t = re.sub(r"[$_^]", "", str(v))
                lines = max(lines, int(len(t) * fs * (0.0088 if bold else 0.0078) / max(w - .1, .2)) + 1)
            return lines * fs * 1.22 / 72 + .1

        hh = height(headers, True)
        idx = 0
        while idx < len(rows):
            room = BOT - self.y - hh
            part = []
            used = 0.0
            while idx + len(part) < len(rows):
                h1 = height(rows[idx + len(part)])
                if used + h1 > room:
                    break
                part.append(rows[idx + len(part)])
                used += h1
            if not part:
                if self.y <= TOP + .01:
                    part = [rows[idx]]
                    used = height(rows[idx])
                else:
                    self.new(self.sec + " (tiếp)")
                    continue
            idx += len(part)
            gs = self.s.shapes.add_table(len(part) + 1, n, Inches(LM), Inches(self.y), Inches(CW), Inches(hh + used))
            tb = gs.table
            tblPr = gs._element.graphic.graphicData.tbl.tblPr
            for a in list(tblPr):
                if a.tag.endswith("tableStyleId"):
                    tblPr.remove(a)
            for j, w in enumerate(cw):
                tb.columns[j].width = Inches(w)
            for j, hd in enumerate(headers):
                self._cell(tb.cell(0, j), hd, fs, True, RGBColor(0xE3, 0xEE, 0xF0))
            tb.rows[0].height = Inches(hh)
            for i, row in enumerate(part, 1):
                for j, v in enumerate(row):
                    self._cell(tb.cell(i, j), v, fs, False, RGBColor(0xFF, 0xFF, 0xFF) if i % 2 else RGBColor(0xF6, 0xF8, 0xF8))
                tb.rows[i].height = Inches(height(row))
            self.y += hh + used + .1
            if idx < len(rows):
                self.new(self.sec + " (tiếp)")
        if caption:
            self.para(caption, 13, italic=True, color=MUTE)

    def _cell(self, c, text, fs, bold, fill):
        c.fill.solid()
        c.fill.fore_color.rgb = fill
        c.margin_left = c.margin_right = Inches(.05)
        c.margin_top = c.margin_bottom = Inches(.02)
        c.vertical_anchor = MSO_ANCHOR.MIDDLE
        p = c.text_frame.paragraphs[0]
        p.alignment = PP_ALIGN.CENTER if bold else PP_ALIGN.LEFT
        self._runs(p, text, fs, bold)

    def figure(self, drawing, caption):
        png = drawing.to_png(width_in=9, dpi=170, max_h_in=5.0)
        im = PILImage.open(io.BytesIO(png))
        iw, ih = im.size
        ar = iw / ih
        availh = BOT - TOP - .4
        h = min(availh, CW / ar)
        w = h * ar
        if self.y + min(h, 2.4) + .4 > BOT or (self.y > TOP + .1 and BOT - self.y - .4 < h * .75):
            self.new(self.sec + " (tiếp)")
        h = min(BOT - self.y - .4, CW / ar)
        w = h * ar
        self.s.shapes.add_picture(io.BytesIO(png), Inches((SW - w) / 2), Inches(self.y), width=Inches(w), height=Inches(h))
        self.y += h + .02
        self._text(self.s, LM, self.y, CW, .3, caption, 13, italic=True, color=MUTE, align=PP_ALIGN.CENTER)
        self.y += .34


def export(r: Report) -> bytes:
    D = Deck(r)
    # bìa
    s = D.prs.slides.add_slide(D.blank)
    D.n = 1
    bg = s.background.fill
    bg.solid()
    bg.fore_color.rgb = ACC
    D._text(s, 0.9, 1.9, SW - 1.8, .5, r.module.upper(), 18, True, RGBColor(0xBF, 0xE0, 0xE4))
    D._text(s, 0.9, 2.45, SW - 1.8, 2.0, r.title, 44, True, RGBColor(0xFF, 0xFF, 0xFF))
    if r.standards:
        D._text(s, 0.9, 5.0, SW - 1.8, .6, "Căn cứ: " + "; ".join(r.standards), 16, False, RGBColor(0xD8, 0xEA, 0xEC))
    D.s = None
    # nội dung
    blocks = r.blocks
    i = 0
    while i < len(blocks):
        b = blocks[i]
        k, d = b.kind, b.data
        if k == "h" and d["level"] <= 2:
            if d["text"] == "Kết luận":
                i += 1
                break
            D.sec = d["text"]
            D.s = None
            D.sec_started = False
            D.ensure(.1)
        elif k == "h":
            D.para(d["text"], 17, bold=True, color=ACC)
        elif k == "p":
            D.para(d["text"], 17)
        elif k == "f":
            D.formula(d["expr"], d.get("note"))
        elif k == "table":
            D.ensure(.8)
            D.table(d["headers"], d["rows"], d.get("caption", ""), d.get("widths"))
        elif k == "fig":
            D.ensure(.1)
            D.figure(d["drawing"], d["caption"])
        elif k == "check":
            D.para(("ĐẠT  " if d["ok"] else "KHÔNG ĐẠT  ") + d["name"], 16, bold=True, color=OK if d["ok"] else BAD)
            D.formula(d["expr"])
        elif k == "note":
            D.para("Ghi chú: " + d["text"], 13, color=MUTE)
        elif k == "warn":
            D.para("Lưu ý: " + d["text"], 13, color=RGBColor(0x8A, 0x5A, 0x00))
        i += 1
    # kết luận
    D.sec = "Kết luận"
    D.s = None
    D.new("Kết luận")
    if r.ok is not None:
        D.para("Tất cả điều kiện kiểm tra ĐẠT" if r.ok else "Có điều kiện KHÔNG ĐẠT", 24, bold=True, color=OK if r.ok else BAD)
    for t in r.summary:
        D.para("• " + t, 20)
    b = io.BytesIO()
    D.prs.save(b)
    return b.getvalue()

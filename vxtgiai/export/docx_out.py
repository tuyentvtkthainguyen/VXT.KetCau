"""Xuất Word: công thức là OMML, sửa trực tiếp được trong Word."""
from __future__ import annotations
import io
import re
from docx import Document
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import parse_xml, OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor
from ..core.mathmk import to_omml, to_runs
from ..core.report import Report

FONT = "Times New Roman"
ACCENT = RGBColor(0x0F, 0x4C, 0x5C)
OKC = RGBColor(0x1B, 0x7A, 0x3E)
BAD = RGBColor(0xB7, 0x1C, 0x1C)


def _font(run, size=None, bold=None, italic=None, color=None):
    run.font.name = FONT
    run._element.rPr.rFonts.set(qn("w:eastAsia"), FONT)
    if size:
        run.font.size = Pt(size)
    if bold is not None:
        run.bold = bold
    if italic is not None:
        run.italic = italic
    if color is not None:
        run.font.color.rgb = color


def _add_rich(par, text, size=13, bold=None, italic=None, color=None):
    for part in re.split(r"(\$[^$]+\$)", text):
        if part.startswith("$") and part.endswith("$") and len(part) > 1:
            for t, m in to_runs(part[1:-1]):
                rr = par.add_run(t)
                _font(rr, size, bold, italic, color)
                if m == "sub":
                    rr.font.subscript = True
                elif m == "sup":
                    rr.font.superscript = True
        elif part:
            _font(par.add_run(part), size, bold, italic, color)


def _shade(cell, color):
    tc = cell._element.get_or_add_tcPr()
    sh = OxmlElement("w:shd")
    sh.set(qn("w:val"), "clear")
    sh.set(qn("w:color"), "auto")
    sh.set(qn("w:fill"), color)
    tc.append(sh)


def _page_number(par):
    for instr in ("PAGE",):
        r = par.add_run()
        a = OxmlElement("w:fldChar")
        a.set(qn("w:fldCharType"), "begin")
        t = OxmlElement("w:instrText")
        t.set(qn("xml:space"), "preserve")
        t.text = instr
        e = OxmlElement("w:fldChar")
        e.set(qn("w:fldCharType"), "end")
        r._element.append(a)
        r._element.append(t)
        r._element.append(e)
        _font(r, 11)


def _omath(par, expr):
    par._element.append(parse_xml(to_omml(expr)))


def export(r: Report) -> bytes:
    doc = Document()
    sec = doc.sections[0]
    sec.page_width, sec.page_height = Cm(21), Cm(29.7)
    sec.left_margin, sec.right_margin = Cm(3), Cm(2)
    sec.top_margin, sec.bottom_margin = Cm(2), Cm(2)
    st = doc.styles["Normal"]
    st.font.name = FONT
    st.font.size = Pt(13)
    st.element.rPr.rFonts.set(qn("w:eastAsia"), FONT)
    st.paragraph_format.space_after = Pt(4)
    for name, size, before in (("Heading 1", 14, 12), ("Heading 2", 13, 10), ("Heading 3", 13, 6)):
        hs = doc.styles[name]
        hs.font.name = FONT
        for att in ("w:asciiTheme", "w:hAnsiTheme", "w:eastAsiaTheme", "w:cstheme"):
            hs.element.rPr.rFonts.attrib.pop(qn(att), None)
        hs.element.rPr.rFonts.set(qn("w:ascii"), FONT)
        hs.element.rPr.rFonts.set(qn("w:hAnsi"), FONT)
        hs.element.rPr.rFonts.set(qn("w:eastAsia"), FONT)
        hs.font.size = Pt(size)
        hs.font.bold = True
        hs.font.italic = name == "Heading 3"
        hs.font.color.rgb = ACCENT if name != "Heading 3" else RGBColor(0x22, 0x22, 0x22)
        hs.paragraph_format.space_before = Pt(before)
        hs.paragraph_format.space_after = Pt(4)
        hs.paragraph_format.keep_with_next = True
    ft = sec.footer.paragraphs[0]
    ft.alignment = WD_ALIGN_PARAGRAPH.CENTER
    _page_number(ft)
    p = doc.add_paragraph()
    _font(p.add_run(r.module.upper()), 11, True, color=ACCENT)
    p.paragraph_format.space_after = Pt(0)
    p = doc.add_paragraph()
    _font(p.add_run(r.title), 18, True)
    p.paragraph_format.space_after = Pt(2)
    if r.standards:
        p = doc.add_paragraph()
        _font(p.add_run("Căn cứ: " + "; ".join(r.standards)), 11, italic=True)
    if r.ok is not None:
        p = doc.add_paragraph()
        _font(p.add_run("Kết quả chung: " + ("tất cả điều kiện kiểm tra ĐẠT" if r.ok else "có điều kiện KHÔNG ĐẠT")), 12, True, color=OKC if r.ok else BAD)
    text_w = 16.0
    for b in r.blocks:
        k, d = b.kind, b.data
        if k == "h":
            doc.add_heading(d["text"], level=min(d["level"], 3))
        elif k == "p":
            p = doc.add_paragraph()
            p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
            _add_rich(p, d["text"])
        elif k == "f":
            p = doc.add_paragraph()
            p.paragraph_format.left_indent = Cm(0.6)
            p.paragraph_format.space_after = Pt(5)
            _omath(p, d["expr"])
            if d.get("note"):
                _font(p.add_run("   "), 11)
                _add_rich(p, d["note"], 11, italic=True, color=RGBColor(0x55, 0x55, 0x55))
        elif k == "table":
            rows = d["rows"]
            n = len(d["headers"])
            t = doc.add_table(rows=1 + len(rows), cols=n)
            t.style = "Table Grid"
            t.alignment = WD_TABLE_ALIGNMENT.CENTER
            size = 11 if n <= 6 else (10 if n <= 8 else 9)
            for j, hd in enumerate(d["headers"]):
                c = t.rows[0].cells[j]
                c.paragraphs[0].alignment = WD_ALIGN_PARAGRAPH.CENTER
                _add_rich(c.paragraphs[0], hd, size, True)
                _shade(c, "E3EEF0")
            trp = t.rows[0]._tr.get_or_add_trPr()
            th = OxmlElement("w:tblHeader")
            th.set(qn("w:val"), "true")
            trp.append(th)
            for i, row in enumerate(rows, 1):
                for j, v in enumerate(row):
                    c = t.rows[i].cells[j]
                    par = c.paragraphs[0]
                    par.paragraph_format.space_after = Pt(0)
                    _add_rich(par, v, size)
                    if re.fullmatch(r"[−\-]?[\d,./ ]+", v or ""):
                        par.alignment = WD_ALIGN_PARAGRAPH.RIGHT
            if d.get("widths") and sum(d["widths"]) > 0:
                sc = min(text_w / sum(d["widths"]), 1.0)
                for j, w in enumerate(d["widths"][:n]):
                    for row_ in t.rows:
                        row_.cells[j].width = Cm(w * sc)
            cap = doc.add_paragraph()
            cap.paragraph_format.space_before = Pt(3)
            if d.get("caption"):
                _add_rich(cap, d["caption"], 11, italic=True)
        elif k == "fig":
            img = d["drawing"].to_png(width_in=6.4, dpi=200)
            p = doc.add_paragraph()
            p.alignment = WD_ALIGN_PARAGRAPH.CENTER
            p.paragraph_format.keep_with_next = True
            p.add_run().add_picture(io.BytesIO(img), width=Cm(min(d.get("width_cm", 15.5), text_w)))
            cp = doc.add_paragraph()
            cp.alignment = WD_ALIGN_PARAGRAPH.CENTER
            _add_rich(cp, d["caption"], 11, italic=True)
        elif k == "check":
            p = doc.add_paragraph()
            p.paragraph_format.left_indent = Cm(0.6)
            _font(p.add_run(("ĐẠT" if d["ok"] else "KHÔNG ĐẠT") + "  "), 12, True, color=OKC if d["ok"] else BAD)
            _font(p.add_run(d["name"] + ": "), 12)
            _omath(p, d["expr"])
        elif k in ("note", "warn"):
            t = doc.add_table(rows=1, cols=1)
            c = t.rows[0].cells[0]
            _shade(c, "F1F3F4" if k == "note" else "FFF3D6")
            _add_rich(c.paragraphs[0], ("Ghi chú: " if k == "note" else "Lưu ý: ") + d["text"], 11)
            doc.add_paragraph().paragraph_format.space_after = Pt(2)
        elif k == "result":
            p = doc.add_paragraph(style="List Bullet")
            _add_rich(p, d["text"], 13, True)
    buf = io.BytesIO()
    doc.core_properties.title = r.title
    doc.core_properties.author = "VXT Giải Kết Cấu"
    doc.save(buf)
    return buf.getvalue()

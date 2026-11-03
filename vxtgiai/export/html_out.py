"""Lời giải dạng HTML hiển thị trong giao diện."""
from __future__ import annotations
import base64
import re
from html import escape
from ..core.mathmk import to_html
from ..core.report import Report


def inline(text: str) -> str:
    out = ""
    parts = re.split(r"(\$[^$]+\$)", text)
    for part in parts:
        if part.startswith("$") and part.endswith("$") and len(part) > 1:
            out += to_html(part[1:-1])
        else:
            out += escape(part)
    return out


def report_html(r: Report) -> str:
    h = [f'<header class="rep-head"><div class="rep-mod">{escape(r.module)}</div><h1>{escape(r.title)}</h1>']
    if r.standards:
        h.append(f'<div class="rep-std">Căn cứ: {escape("; ".join(r.standards))}</div>')
    if r.ok is not None:
        h.append(f'<div class="verdict {"ok" if r.ok else "bad"}">{"Tất cả điều kiện kiểm tra ĐẠT" if r.ok else "Có điều kiện KHÔNG ĐẠT"}</div>')
    h.append("</header>")
    for b in r.blocks:
        k, d = b.kind, b.data
        if k == "h":
            lv = min(d["level"] + 1, 4)
            h.append(f"<h{lv}>{escape(d['text'])}</h{lv}>")
        elif k == "p":
            h.append(f"<p>{inline(d['text'])}</p>")
        elif k == "f":
            n = f'<span class="fnote">{inline(d["note"])}</span>' if d.get("note") else ""
            h.append(f'<div class="eq"><span class="eqb">{to_html(d["expr"])}</span>{n}</div>')
        elif k == "table":
            h.append('<div class="tw"><table><thead><tr>')
            h += [f"<th>{inline(c)}</th>" for c in d["headers"]]
            h.append("</tr></thead><tbody>")
            for row in d["rows"]:
                h.append("<tr>" + "".join(f"<td>{inline(c)}</td>" for c in row) + "</tr>")
            h.append("</tbody></table></div>")
            if d.get("caption"):
                h.append(f'<div class="cap">{inline(d["caption"])}</div>')
        elif k == "fig":
            data = base64.b64encode(d["drawing"].to_png(width_in=6.4, dpi=130)).decode()
            w = d.get("width_cm", 15.5)
            h.append(f'<figure><img alt="{escape(d["caption"])}" style="max-width:{w * 37.8:.0f}px" src="data:image/png;base64,{data}"><figcaption>{escape(d["caption"])}</figcaption></figure>')
        elif k == "check":
            cls = "ok" if d["ok"] else "bad"
            lab = "ĐẠT" if d["ok"] else "KHÔNG ĐẠT"
            h.append(f'<div class="chk {cls}"><span class="tag">{lab}</span><span class="nm">{escape(d["name"])}:</span> <span class="eqb">{to_html(d["expr"])}</span></div>')
        elif k == "note":
            h.append(f'<div class="note">{inline(d["text"])}</div>')
        elif k == "warn":
            h.append(f'<div class="warn">{inline(d["text"])}</div>')
        elif k == "result":
            h.append(f'<div class="res">{inline(d["text"])}</div>')
    return "\n".join(h)

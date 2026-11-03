"""Máy chủ cục bộ: phục vụ giao diện và nhận lệnh giải, xuất file."""
from __future__ import annotations
import io
import os
import re
import traceback
import unicodedata
from flask import Flask, jsonify, request, send_file, send_from_directory
from .solvers import registry
from .solvers.registry import InputError
from .export import FORMATS
from .export.html_out import report_html

UI_DIR = os.path.join(os.path.dirname(__file__), "ui")
app = Flask(__name__)
app.json.sort_keys = False


def safe_name(title: str) -> str:
    s = unicodedata.normalize("NFD", title.replace("đ", "d").replace("Đ", "D"))
    s = "".join(c for c in s if unicodedata.category(c) != "Mn")
    s = re.sub(r"[^A-Za-z0-9]+", "_", s).strip("_")
    return s or "LoiGiai"


def build(fmt: str, pid: str, params: dict):
    """Giải rồi xuất; trả về (bytes, mimetype, tên file)."""
    if fmt not in FORMATS:
        raise InputError("Định dạng xuất không hợp lệ.")
    r = registry.run(pid, params)
    fn, mime, _ = FORMATS[fmt]
    return fn(r), mime, f"{safe_name(r.title)}.{fmt}"


@app.get("/")
def index():
    return send_from_directory(UI_DIR, "index.html")


@app.get("/api/catalog")
def catalog():
    return jsonify(modules=registry.MODULES, problems=registry.catalog())


def _guard(fn):
    try:
        return fn()
    except InputError as e:
        return jsonify(error=str(e)), 400
    except (ValueError, ZeroDivisionError, OverflowError, KeyError, TypeError, IndexError) as e:
        traceback.print_exc()
        return jsonify(error=f"Không giải được với dữ liệu này: {e}"), 400
    except Exception as e:  # lỗi ngoài dự kiến vẫn báo được cho người dùng
        traceback.print_exc()
        return jsonify(error=f"Lỗi chương trình: {e}"), 500


@app.post("/api/solve")
def solve():
    def go():
        q = request.get_json(force=True)
        r = registry.run(q["id"], q.get("params", {}))
        return jsonify(html=report_html(r), ok=r.ok, summary=r.summary)
    return _guard(go)


@app.post("/api/export/<fmt>")
def export_file(fmt):
    def go():
        q = request.get_json(force=True)
        data, mime, name = build(fmt, q["id"], q.get("params", {}))
        return send_file(io.BytesIO(data), mimetype=mime, as_attachment=True, download_name=name)
    return _guard(go)

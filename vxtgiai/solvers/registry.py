"""Danh mục bài toán: mô tả ô nhập để giao diện tự sinh biểu mẫu."""
from __future__ import annotations

PROBLEMS = {}
MODULES = {
    "sbvl": "Sức bền vật liệu",
    "ckc": "Cơ học kết cấu",
    "btct": "Kết cấu bê tông cốt thép",
    "nm": "Nền móng",
    "thep": "Kết cấu thép",
}


class InputError(ValueError):
    """Dữ liệu nhập không hợp lệ: thông báo hiển thị thẳng cho người dùng."""


def num(key, label, unit="", default=0.0, min=None, max=None, help="", sym=""):
    return dict(key=key, type="num", label=label, unit=unit, default=default, min=min, max=max, help=help, sym=sym)


def sel(key, label, options, default=None, help=""):
    opts = [dict(value=o[0], label=o[1]) if isinstance(o, (tuple, list)) else dict(value=o, label=str(o)) for o in options]
    return dict(key=key, type="sel", label=label, options=opts, default=default if default is not None else opts[0]["value"], help=help)


def tbl(key, label, cols, rows, help="", min_rows=1):
    """cols: [(khoá, nhãn, 'num'|'text'|[lựa chọn])]"""
    return dict(key=key, type="tbl", label=label, cols=[dict(key=c[0], label=c[1], kind=c[2] if len(c) > 2 else "num") for c in cols], default=rows, help=help, min_rows=min_rows)


def problem(pid, module, title, desc, fields):
    def deco(fn):
        PROBLEMS[pid] = dict(id=pid, module=module, title=title, desc=desc, fields=fields, fn=fn)
        return fn
    return deco


def _coerce(field, v):
    t = field["type"]
    if t == "num":
        if v is None or v == "":
            raise InputError(f"Chưa nhập: {field['label']}")
        try:
            x = float(str(v).replace(",", "."))
        except ValueError:
            raise InputError(f"Giá trị không phải số: {field['label']} = {v}")
        if field["min"] is not None and x < field["min"]:
            raise InputError(f"{field['label']} phải ≥ {field['min']}")
        if field["max"] is not None and x > field["max"]:
            raise InputError(f"{field['label']} phải ≤ {field['max']}")
        return x
    if t == "sel":
        vals = [o["value"] for o in field["options"]]
        if v not in vals:
            for c in vals:
                if str(c) == str(v):
                    return c
            raise InputError(f"Lựa chọn không hợp lệ: {field['label']}")
        return v
    if t == "tbl":
        rows = []
        for r in v or []:
            row = {}
            blank = True
            for c in field["cols"]:
                raw = r.get(c["key"], "")
                if c["kind"] == "num":
                    if raw in ("", None):
                        row[c["key"]] = None
                        continue
                    blank = False
                    try:
                        row[c["key"]] = float(str(raw).replace(",", "."))
                    except ValueError:
                        raise InputError(f"{field['label']}: «{raw}» không phải số (cột {c['label']})")
                else:
                    row[c["key"]] = raw
                    if raw not in ("", None) and not isinstance(c["kind"], list):
                        blank = False
            if not blank:
                numeric = [c for c in field["cols"] if c["kind"] == "num"]
                for c in numeric:
                    if row[c["key"]] is None:
                        row[c["key"]] = 0.0
                rows.append(row)
        if len(rows) < field["min_rows"]:
            raise InputError(f"{field['label']}: cần ít nhất {field['min_rows']} dòng")
        return rows
    return v


def run(pid, params):
    p = PROBLEMS.get(pid)
    if p is None:
        raise InputError(f"Không có dạng bài: {pid}")
    clean = {}
    for f in p["fields"]:
        clean[f["key"]] = _coerce(f, params.get(f["key"], f["default"]))
    try:
        return p["fn"](clean)
    except InputError:
        raise
    except ZeroDivisionError:
        raise InputError("Số liệu làm phép tính chia cho 0: hãy kiểm tra các kích thước và hệ số đã nhập khác 0.")
    except ValueError as e:
        raise InputError(f"Số liệu không hợp lệ về mặt tính toán: {e}")


def defaults(pid):
    return {f["key"]: f["default"] for f in PROBLEMS[pid]["fields"]}


def catalog():
    out = []
    for p in PROBLEMS.values():
        out.append({k: p[k] for k in ("id", "module", "title", "desc", "fields")})
    return out

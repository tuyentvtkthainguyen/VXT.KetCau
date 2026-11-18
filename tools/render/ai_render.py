#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Render phối cảnh bằng AI từ ảnh khối SketchUp / Revit + mô tả mặt tiền trong phương án Việt Const.

    python ai_render.py --anh PhoiCanh.png --phuong-an nha.vxtphuongan.json --ra Render.png
    python ai_render.py --anh PhoiCanh.png --phuong-an nha.vxtphuongan.json --chi-cau-lenh    (chỉ in câu lệnh, không gửi)

- Giữ nguyên hình khối, tỷ lệ, vị trí cửa của ảnh gốc; chỉ "phủ" vật liệu, ánh sáng, cảnh quan theo mô tả.
- Dùng Gemini (Google) – khoá API của chính người dùng, đặt trong biến môi trường GEMINI_API_KEY trên máy.
  Ảnh + câu lệnh được gửi tới Google khi chạy (không có --chi-cau-lenh). Không gửi gì khác, không ghi nhật ký, không thu thập dữ liệu.
- Không có khoá: in câu lệnh để dán vào công cụ render AI khác (hoặc dùng làm ghi chú cho Enscape / Vantage).
Chỉ dùng thư viện chuẩn Python 3.8+.
"""
import argparse
import base64
import json
import mimetypes
import os
import sys
import urllib.error
import urllib.request

API = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"
DEFAULT_MODEL = "gemini-2.5-flash-image"

TIME = {
    "ngay": "bright late-morning daylight, soft shadows, clear sky with a few clouds",
    "chieu": "warm late-afternoon golden-hour sunlight, long soft shadows",
    "toi": "blue-hour dusk, warm interior lights glowing through the windows, exterior wall washers on",
}


def load_plan(path):
    if not path:
        return {}
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def build_prompt(plan, time_key="ngay", extra="", view="perspective"):
    fac = plan.get("Facade") or {}
    levels = plan.get("Levels") or []
    storeys = max(0, len([l for l in levels if l.get("Name", "").lower() not in ("mái", "mai", "roof")]))
    site = plan.get("Site") or {}
    mats = fac.get("Materials") or []
    mat_lines = "\n".join(
        f"- {m.get('Part', '')}: {m.get('Material', '')}" + (f" (colour {m.get('Color')})" if m.get("Color") else "")
        for m in mats
    )
    width = site.get("Width")
    lines = [
        "Turn this architectural massing render into a photorealistic exterior visualisation of the SAME building.",
        "STRICT: keep the exact geometry, proportions, number of floors, window and door positions, roof line and camera angle of the input image. "
        "Do not add or remove floors, balconies or openings. Do not change the camera.",
        f"Building: Vietnamese townhouse (nhà phố){f', {storeys} storeys' if storeys else ''}{f', {width:g} m wide frontage' if width else ''}.",
        f"Architectural style: {fac.get('Style') or 'modern'}.",
    ]
    if fac.get("Description"):
        lines.append(f"Facade description (Vietnamese, from the architect): {fac['Description']}")
    if mat_lines:
        lines.append("Materials:\n" + mat_lines)
    lines += [
        f"Lighting: {TIME.get(time_key, TIME['ngay'])}.",
        "Context: a clean Vietnamese urban street, neighbouring houses kept simple and out of focus, a few potted plants, "
        "paved sidewalk; no text, no logos, no watermarks, no people in the foreground.",
        "Output: high-resolution photoreal architectural photograph, straight verticals, natural colours.",
    ]
    if view == "elevation":
        lines.append("This is an orthographic front elevation: keep it flat and frontal, no perspective distortion.")
    if extra:
        lines.append("Additional instructions: " + extra)
    return "\n".join(lines)


def image_part(path):
    mime = mimetypes.guess_type(path)[0] or "image/png"
    with open(path, "rb") as f:
        return {"inline_data": {"mime_type": mime, "data": base64.b64encode(f.read()).decode("ascii")}}


def render(image, refs, prompt, out, model, key, timeout=180):
    parts = [{"text": prompt}, image_part(image)]
    for r in refs:
        parts.append({"text": "Reference image for facade mood and materials only (do NOT copy its geometry):"})
        parts.append(image_part(r))
    body = json.dumps({"contents": [{"role": "user", "parts": parts}],
                       "generationConfig": {"responseModalities": ["IMAGE", "TEXT"]}}).encode("utf-8")
    req = urllib.request.Request(API.format(model=model), data=body, method="POST",
                                 headers={"Content-Type": "application/json", "x-goog-api-key": key})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            res = json.loads(r.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        msg = e.read().decode("utf-8", "replace")[:800]
        raise SystemExit(f"Lỗi API ({e.code}): {msg}")
    texts, saved = [], False
    for c in res.get("candidates", []):
        for p in (c.get("content") or {}).get("parts", []):
            data = p.get("inlineData") or p.get("inline_data")
            if data and data.get("data") and not saved:
                os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
                with open(out, "wb") as f:
                    f.write(base64.b64decode(data["data"]))
                saved = True
            elif p.get("text"):
                texts.append(p["text"])
    if not saved:
        reason = (res.get("promptFeedback") or {}).get("blockReason") or "; ".join(texts) or json.dumps(res)[:400]
        raise SystemExit("API không trả về ảnh: " + reason)
    return texts


def main(argv=None):
    ap = argparse.ArgumentParser(description="Render phối cảnh AI từ ảnh khối + phương án Việt Const")
    ap.add_argument("--anh", required=True, help="ảnh khối / phối cảnh từ SketchUp hoặc Revit (png/jpg)")
    ap.add_argument("--phuong-an", default="", help="file *.vxtphuongan.json (lấy mô tả mặt tiền, vật liệu)")
    ap.add_argument("--ra", default="", help="ảnh kết quả (mặc định <ảnh>_render.png)")
    ap.add_argument("--anh-tham-khao", nargs="*", default=None, help="ảnh mặt tiền tham khảo (mặc định: Facade.ReferenceImages có trên máy)")
    ap.add_argument("--gio", choices=sorted(TIME), default="ngay", help="ngày / chiều / tối")
    ap.add_argument("--goc", choices=["perspective", "elevation"], default="perspective")
    ap.add_argument("--them", default="", help="yêu cầu thêm (tiếng Việt hoặc Anh)")
    ap.add_argument("--model", default=os.environ.get("VXT_RENDER_MODEL", DEFAULT_MODEL))
    ap.add_argument("--chi-cau-lenh", action="store_true", help="chỉ in câu lệnh, không gửi ảnh đi đâu")
    a = ap.parse_args(argv)

    if not os.path.isfile(a.anh):
        raise SystemExit("Không thấy ảnh: " + a.anh)
    plan = load_plan(a.phuong_an)
    prompt = build_prompt(plan, a.gio, a.them, a.goc)
    refs = a.anh_tham_khao
    if refs is None:
        refs = [p for p in ((plan.get("Facade") or {}).get("ReferenceImages") or []) if os.path.isfile(p)]
    refs = refs[:3]
    if a.chi_cau_lenh:
        print(prompt)
        return 0
    key = os.environ.get("GEMINI_API_KEY", "").strip()
    if not key:
        print(prompt)
        print("\n[Chưa có GEMINI_API_KEY trên máy – mới in câu lệnh. Đặt khoá của bạn: setx GEMINI_API_KEY \"...\" rồi mở cửa sổ mới.]", file=sys.stderr)
        return 2
    out = a.ra or os.path.splitext(a.anh)[0] + "_render.png"
    texts = render(a.anh, refs, prompt, out, a.model, key)
    print("Đã lưu: " + out)
    for t in texts:
        print(t)
    return 0


if __name__ == "__main__":
    sys.exit(main())

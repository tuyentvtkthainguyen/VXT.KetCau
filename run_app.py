"""Điểm vào của VXT Giải Kết Cấu. Mở cửa sổ riêng (WebView2); nếu không có thì mở trình duyệt."""
from __future__ import annotations
import json
import os
import socket
import sys
import tempfile
import threading
import time
import webbrowser


def resource_root():
    return getattr(sys, "_MEIPASS", os.path.dirname(os.path.abspath(__file__)))


def selftest() -> int:
    """Giải mọi dạng bài bằng số liệu mẫu và xuất đủ 4 định dạng. Dùng khi kiểm thử bản cài."""
    from vxtgiai.solvers import registry
    from vxtgiai.export import FORMATS
    from vxtgiai.export.html_out import report_html
    out = tempfile.mkdtemp(prefix="vxt_selftest_")
    bad = 0
    for pid in registry.PROBLEMS:
        try:
            r = registry.run(pid, registry.defaults(pid))
            report_html(r)
            for f, (fn, _, _) in FORMATS.items():
                data = fn(r)
                if len(data) < 500:
                    raise RuntimeError(f"file {f} quá nhỏ")
                with open(os.path.join(out, f"{pid}.{f}"), "wb") as fh:
                    fh.write(data)
            print("OK  ", pid)
        except Exception as e:  # noqa: BLE001
            bad += 1
            print("LỖI ", pid, e)
    print(f"{len(registry.PROBLEMS) - bad}/{len(registry.PROBLEMS)} dạng bài đạt; file tại {out}")
    return 1 if bad else 0


def _free_port():
    s = socket.socket()
    s.bind(("127.0.0.1", 0))
    p = s.getsockname()[1]
    s.close()
    return p


class Api:
    """Hàm gọi từ giao diện khi chạy trong cửa sổ riêng: dùng hộp thoại lưu file của Windows."""
    window = None

    def _save_dialog(self, name, kinds):
        import webview
        r = self.window.create_file_dialog(webview.SAVE_DIALOG, save_filename=name, file_types=kinds)
        if not r:
            return None
        return r if isinstance(r, str) else r[0]

    def save_export(self, fmt, pid, params):
        from vxtgiai.server import build
        from vxtgiai.solvers.registry import InputError
        try:
            data, _, name = build(fmt, pid, params)
            labels = {"dxf": "Bản vẽ CAD (*.dxf)", "docx": "Word (*.docx)", "pptx": "PowerPoint (*.pptx)", "pdf": "PDF (*.pdf)"}
            path = self._save_dialog(name, (labels[fmt],))
            if not path:
                return {}
            with open(path, "wb") as f:
                f.write(data)
            return {"path": path}
        except (InputError, ValueError, OSError) as e:
            return {"error": str(e)}

    def save_text(self, text, name):
        try:
            path = self._save_dialog(name, ("Đề bài VXT (*.vxtde)",))
            if not path:
                return {}
            with open(path, "w", encoding="utf-8") as f:
                f.write(text)
            return {"path": path}
        except OSError as e:
            return {"error": str(e)}

    def open_text(self):
        import webview
        r = self.window.create_file_dialog(webview.OPEN_DIALOG, file_types=("Đề bài VXT (*.vxtde)", "JSON (*.json)"))
        if not r:
            return {}
        with open(r[0], encoding="utf-8") as f:
            return {"text": f.read()}


def main():
    # bản đóng gói không có cửa sổ console: tránh lỗi khi ghi log
    if sys.stdout is None:
        sys.stdout = open(os.devnull, "w", encoding="utf-8")
    if sys.stderr is None:
        sys.stderr = open(os.devnull, "w", encoding="utf-8")
    if "--selftest" in sys.argv:
        sys.exit(selftest())
    from vxtgiai.server import app
    port = _free_port()
    url = f"http://127.0.0.1:{port}/"
    th = threading.Thread(target=lambda: app.run(host="127.0.0.1", port=port, debug=False, use_reloader=False), daemon=True)
    th.start()
    for _ in range(100):
        try:
            socket.create_connection(("127.0.0.1", port), 0.1).close()
            break
        except OSError:
            time.sleep(0.05)
    if "--browser" not in sys.argv:
        try:
            import webview
            api = Api()
            api.window = webview.create_window("VXT Giải Kết Cấu", url, js_api=api, width=1440, height=900, min_size=(1000, 640))
            webview.start()
            return
        except Exception as e:  # noqa: BLE001  không có WebView2: dùng trình duyệt
            print("Không mở được cửa sổ riêng:", e)
    webbrowser.open(url)
    print("Đang chạy tại", url, "- đóng cửa sổ này để thoát.")
    th.join()


if __name__ == "__main__":
    main()

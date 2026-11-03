# -*- mode: python ; coding: utf-8 -*-
# Đóng gói: pyinstaller vxtgiai.spec --noconfirm
from PyInstaller.utils.hooks import collect_data_files, collect_submodules

datas = [("vxtgiai/ui", "vxtgiai/ui"), ("vxtgiai/fonts", "vxtgiai/fonts")]
datas += collect_data_files("docx") + collect_data_files("pptx") + collect_data_files("reportlab")
hidden = collect_submodules("webview") + collect_submodules("vxtgiai")
excl = ["tkinter", "PyQt5", "PySide6", "IPython", "pytest", "scipy", "pandas"]

a = Analysis(["run_app.py"], pathex=["."], datas=datas, hiddenimports=hidden, excludes=excl)
pyz = PYZ(a.pure)
exe = EXE(pyz, a.scripts, [], exclude_binaries=True, name="VXTGiaiKetCau", console=False, icon=None)
coll = COLLECT(exe, a.binaries, a.datas, name="VXTGiaiKetCau")

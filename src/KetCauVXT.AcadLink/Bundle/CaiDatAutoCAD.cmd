@echo off
rem Cai add-in AutoCAD Viet Const (them tham so -Go de go cai dat)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0CaiDatAutoCAD.ps1" %*
pause

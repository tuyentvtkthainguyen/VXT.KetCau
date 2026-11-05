# Cài add-in AutoCAD "Việt Const – liên kết AutoCAD" (AutoCAD 2021–2026, Windows 64-bit).
# Chép thư mục KetCauVXT.bundle (đặt cạnh file này) vào %APPDATA%\Autodesk\ApplicationPlugins.
# Chạy: bấm đúp CaiDatAutoCAD.cmd, hoặc: powershell -ExecutionPolicy Bypass -File CaiDatAutoCAD.ps1 [-Go]   (-Go = gỡ cài đặt)
param([switch]$Go)
$ErrorActionPreference = 'Stop'
$src = Join-Path $PSScriptRoot 'KetCauVXT.bundle'
$dstRoot = Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins'
$dst = Join-Path $dstRoot 'KetCauVXT.bundle'
try {
    if (Get-Process -Name acad -ErrorAction SilentlyContinue) {
        Write-Host 'Lưu ý: AutoCAD đang mở – hãy đóng AutoCAD trước khi cài (hoặc khởi động lại AutoCAD sau khi cài).' -ForegroundColor Yellow
    }
    if ($Go) {
        if (Test-Path $dst) { Remove-Item $dst -Recurse -Force; Write-Host "Đã gỡ add-in: $dst" -ForegroundColor Green }
        else { Write-Host 'Add-in chưa được cài.' }
        exit 0
    }
    if (-not (Test-Path (Join-Path $src 'PackageContents.xml'))) { throw "Không tìm thấy $src\PackageContents.xml (giải nén đủ thư mục AutoCAD trong gói phát hành)." }
    New-Item -ItemType Directory -Force $dstRoot | Out-Null
    if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
    Copy-Item $src $dst -Recurse -Force
    Get-ChildItem $dst -Recurse -File | Unblock-File -ErrorAction SilentlyContinue
    $vers = (Get-ChildItem (Join-Path $dst 'Contents') -Directory | ForEach-Object { $_.Name }) -join ', '
    Write-Host "Đã cài add-in Việt Const cho AutoCAD: $vers" -ForegroundColor Green
    Write-Host "Thư mục: $dst"
    Write-Host 'Mở AutoCAD, gõ lệnh VXT để hiện bảng lệnh (gán tường, cột, dầm, sàn, trục, khai báo tầng, gửi sang Việt Const).'
    Write-Host 'Nếu AutoCAD hỏi có nạp add-in không: chọn "Always Load / Luôn nạp".'
} catch {
    Write-Host ('Lỗi cài đặt: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}

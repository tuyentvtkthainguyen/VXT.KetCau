<#
  Gửi một lệnh cho Revit hoặc SketchUp qua cầu nối thư mục của Việt Const và chờ kết quả.
  Chỉ ghi / đọc file trong %LOCALAPPDATA%\VietConst\Agent – không mạng, không thu thập dữ liệu.

  .\GuiLenh.ps1 -Ung revit -Lenh dung-kien-truc -File "D:\DuAn\nha.vxtphuongan.json"
  .\GuiLenh.ps1 -Ung sketchup -Lenh anh -File "D:\DuAn\PhoiCanh.png" -Them @{ canh = 1; rong = 2400; cao = 1350 }
  Kết quả: đối tượng { id, lenh, ok, bao_cao, file }. Mã thoát 0 = ok, 1 = lệnh lỗi, 2 = cầu nối chưa bật / hết giờ.
#>
param(
  [Parameter(Mandatory)][ValidateSet('revit', 'sketchup')][string]$Ung,
  [Parameter(Mandatory)][string]$Lenh,
  [string]$File = '',
  [hashtable]$Them = @{},
  [int]$ChoGiay = 900
)
$root = Join-Path $env:LOCALAPPDATA "VietConst\Agent\$Ung"
$st = Join-Path $root 'trang-thai.json'
if (-not (Test-Path $st) -or -not ((Get-Content $st -Raw -Encoding UTF8 | ConvertFrom-Json).dang_bat)) {
  Write-Output "Cầu nối $Ung CHƯA BẬT. Nhờ người dùng: $(if ($Ung -eq 'revit') { 'Revit → tab Việt Const → Phương án → hồ sơ → Cho phép agent' } else { 'SketchUp → Extensions → Việt Const → Cho phép agent điều khiển SketchUp' })."
  exit 2
}
$id = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $Lenh
$job = @{ lenh = $Lenh; file = $File } + $Them
New-Item -ItemType Directory -Force (Join-Path $root 'in') | Out-Null
$tmp = Join-Path $root "in\$id.tmp"
[IO.File]::WriteAllText($tmp, ($job | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding $false))
Move-Item $tmp (Join-Path $root "in\$id.json")
$res = Join-Path $root "out\$id.ket-qua.json"
$t0 = Get-Date
while (-not (Test-Path $res)) {
  if (((Get-Date) - $t0).TotalSeconds -gt $ChoGiay) { Write-Output "Hết giờ chờ $Ung ($ChoGiay s) – ứng dụng có đang mở hộp thoại chờ người dùng không?"; exit 2 }
  Start-Sleep -Milliseconds 800
}
$r = Get-Content $res -Raw -Encoding UTF8 | ConvertFrom-Json
$r
if ($r.ok) { exit 0 } else { exit 1 }

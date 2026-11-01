# VXT Kết cấu — build toàn bộ và chạy kiểm thử
# Cách dùng (PowerShell, tại thư mục gốc giải pháp):
#   .\build.ps1                      # test lõi + build CLI + add-in Revit 2022–2026
#   .\build.ps1 -Revit 2024,2025     # chỉ một số phiên bản Revit
#   .\build.ps1 -CsiApiDir "D:\CSI\ETABS 22"
param(
    [int[]]$Revit = @(2022, 2023, 2024, 2025, 2026),
    [string]$CsiApiDir = "",
    [switch]$SkipTests
)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$extra = @()
if ($CsiApiDir) { $extra += "-p:CsiApiDir=$CsiApiDir" }

function Step($t) { Write-Host "== $t" -ForegroundColor Cyan }
function Run { param([string[]]$a) & dotnet @a; if ($LASTEXITCODE -ne 0) { throw "Lỗi: dotnet $($a -join ' ')" } }

Step "Kiểm tra .NET SDK"
Run @("--version")

if (-not $SkipTests) {
    Step "Kiểm thử lõi (TCVN, đánh số, quy trình đầu-cuối)"
    Run @("test", "$root\tests\VXT.KetCau.Core.Tests", "-c", "Release", "--logger", "trx;LogFileName=ketqua.trx", "--results-directory", "$root\TestResults")
}

$csiDll = if ($CsiApiDir) { Join-Path $CsiApiDir "CSiAPIv1.dll" } else { "C:\Program Files\Computers and Structures\ETABS 22\CSiAPIv1.dll" }
if (-not (Test-Path $csiDll)) {
    Write-Warning "Không thấy $csiDll — bỏ qua CLI và add-in Revit (cần ETABS/SAP2000). Dùng -CsiApiDir để chỉ đường dẫn."
    exit 0
}

Step "Dòng lệnh vxtkc"
Run (@("build", "$root\src\VXT.KetCau.Cli", "-c", "Release") + $extra)

foreach ($v in $Revit) {
    $yy = $v - 2000
    Step "Add-in Revit $v"
    Run (@("build", "$root\src\VXT.KetCau.Revit", "-c", "Release R$yy") + $extra)
}
Step "Xong. Add-in đã chép vào %AppData%\Autodesk\Revit\Addins\<năm>"

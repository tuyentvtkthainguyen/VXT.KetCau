# Gom bản build AutoCAD thành VXTKetCau.bundle (dist\) — chạy sau: dotnet build src\VXT.KetCau.AutoCAD -c Release
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$bin = Join-Path $root "src\VXT.KetCau.AutoCAD\bin\Release"
$dist = Join-Path $root "dist"
$bundle = Join-Path $dist "VXTKetCau.bundle"
if (Test-Path $bundle) { Remove-Item $bundle -Recurse -Force }
New-Item -ItemType Directory -Force -Path $bundle | Out-Null
Copy-Item (Join-Path $root "src\VXT.KetCau.AutoCAD\Bundle\PackageContents.xml") $bundle
foreach ($p in @(@("net48", "2022-2024"), @("net8.0-windows", "2025-2026"))) {
    $src = Join-Path $bin $p[0]
    $dst = Join-Path $bundle ("Contents\" + $p[1])
    New-Item -ItemType Directory -Force -Path $dst | Out-Null
    Get-ChildItem $src -File | Where-Object {
        $_.Extension -in ".dll", ".json", ".config" -and
        $_.Name -notmatch "^(CSiAPIv1|ETABSv1|SAP2000v1|ac|Ac|AdWindows|AdUi)" -and
        $_.Name -notlike "*.runtimeconfig.json"
    } | Copy-Item -Destination $dst
}
# CSiAPIv1.dll KHÔNG chép kèm: add-in tự tìm trong thư mục cài ETABS/SAP2000 của máy người dùng
Get-ChildItem $bundle -Recurse -File | Select-Object FullName, Length | Format-Table -AutoSize

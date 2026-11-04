; ==========================================================================
;  Bộ cài đặt chuẩn Kết cấu VXT (Inno Setup 6)
;  - Cài cho tài khoản hiện tại (không cần quyền quản trị) hoặc cho mọi người dùng (chọn khi cài)
;  - Biểu tượng Start Menu / Desktop, liên kết file .kcvxt, gỡ cài đặt trong Settings → Apps
;  - Add-in AutoCAD 2021–2026 (tuỳ chọn), dự án mẫu
;  - Bộ cài chia nhiều phần (KetCauVXT-Setup.exe + KetCauVXT-Setup-*.bin) – để cùng một thư mục rồi chạy file .exe
;  - Không đóng cưỡng bức chương trình đang mở (AppMutex) để tránh mất dữ liệu chưa lưu
; ==========================================================================
#ifndef AppVer
  #define AppVer "1.0.0"
#endif
#ifndef SrcDir
  #define SrcDir "..\publish"
#endif
#ifndef SampleDir
  #define SampleDir "..\KetQuaMau"
#endif
#ifndef OutDir
  #define OutDir "..\dist\setup"
#endif

[Setup]
AppId={{6F1E2B7C-5A3D-4C8E-9B21-4B43565854A1}
AppName=Kết cấu VXT
AppVersion={#AppVer}
AppVerName=Kết cấu VXT {#AppVer}
AppPublisher=Vũ Xuân Tuyến
AppPublisherURL=https://github.com/tuyentvtkthainguyen/VXT.KetCau
AppSupportURL=https://github.com/tuyentvtkthainguyen/VXT.KetCau
VersionInfoVersion={#AppVer}
VersionInfoProductName=Kết cấu VXT
VersionInfoDescription=Bộ cài đặt Kết cấu VXT
DefaultDirName={autopf}\KetCauVXT
DefaultGroupName=Kết cấu VXT
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutDir}
OutputBaseFilename=KetCauVXT-Setup
SetupIconFile=..\src\KetCauVXT.App\KetCauVXT.ico
UninstallDisplayIcon={app}\KetCauVXT.exe
UninstallDisplayName=Kết cấu VXT
Compression=lzma2/max
SolidCompression=yes
DiskSpanning=yes
DiskSliceSize=24000000
SlicesPerDisk=1
WizardStyle=modern
AppMutex=KetCauVXT_DangChay
CloseApplications=no
RestartApplications=no
ChangesAssociations=yes
UsePreviousAppDir=yes
UsePreviousTasks=yes
InfoBeforeFile=GioiThieu.txt

[Languages]
#ifexist "Vietnamese.isl"
Name: "vi"; MessagesFile: "Vietnamese.isl"
#endif
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
vi.TaskDesktop=Tạo biểu tượng trên Desktop
en.TaskDesktop=Create a desktop shortcut
vi.TaskAssoc=Mở file dự án .kcvxt bằng Kết cấu VXT
en.TaskAssoc=Open .kcvxt project files with Kết cấu VXT
vi.TaskAcad=Cài add-in AutoCAD 2021–2026 (lệnh VXT, VXT_TUONG, VXT_COT, VXT_GUI…)
en.TaskAcad=Install the AutoCAD 2021–2026 add-in
vi.GroupExtra=Tuỳ chọn thêm:
en.GroupExtra=Additional options:
vi.RunApp=Mở Kết cấu VXT
en.RunApp=Launch Kết cấu VXT
vi.Samples=Dự án mẫu
en.Samples=Sample projects
vi.Guide=Hướng dẫn sử dụng
en.Guide=User guide
vi.GuideWord=Hướng dẫn sử dụng (Word)
en.GuideWord=User guide (Word)
vi.Uninst=Gỡ cài đặt Kết cấu VXT
en.Uninst=Uninstall Kết cấu VXT

[Tasks]
Name: "desktopicon"; Description: "{cm:TaskDesktop}"; GroupDescription: "{cm:GroupExtra}"
Name: "assoc"; Description: "{cm:TaskAssoc}"; GroupDescription: "{cm:GroupExtra}"
Name: "acad"; Description: "{cm:TaskAcad}"; GroupDescription: "{cm:GroupExtra}"; Flags: unchecked

[InstallDelete]
; dọn bản cài thủ công cũ (CaiDat.ps1): lối tắt và script gỡ cũ
Type: files; Name: "{userprograms}\Kết cấu VXT.lnk"
Type: files; Name: "{app}\GoCaiDat.ps1"

[Files]
Source: "{#SrcDir}\*"; DestDir: "{app}"; Excludes: "AutoCAD\*,*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcDir}\AutoCAD\*"; DestDir: "{app}\AutoCAD"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
; add-in AutoCAD: thư mục tự nạp ApplicationPlugins (cài cho mọi người dùng → ProgramData, cho 1 người → AppData)
Source: "{#SrcDir}\AutoCAD\KetCauVXT.bundle\*"; DestDir: "{autoappdata}\Autodesk\ApplicationPlugins\KetCauVXT.bundle"; Tasks: acad; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#SampleDir}\NhaPho\NhaPho.kcvxt"; DestDir: "{app}\DuAnMau"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#SampleDir}\TuBanVeKienTruc\TuBanVeKienTruc.kcvxt"; DestDir: "{app}\DuAnMau"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#SampleDir}\TuBanVeKienTruc\*_DauVao.dxf"; DestDir: "{app}\DuAnMau"; Flags: ignoreversion skipifsourcedoesntexist
; [HướngDẫn] hướng dẫn sử dụng Word do bộ kiểm thử sinh (UserGuideTests → KetQuaMau\HuongDan)
Source: "{#SampleDir}\HuongDan\HuongDanSuDung.docx"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\Kết cấu VXT"; Filename: "{app}\KetCauVXT.exe"; WorkingDir: "{app}"
Name: "{group}\{cm:Samples}"; Filename: "{app}\DuAnMau"
Name: "{group}\{cm:Guide}"; Filename: "{app}\HuongDan.md"
Name: "{group}\{cm:GuideWord}"; Filename: "{app}\HuongDanSuDung.docx"; Check: GuideDocxExists
Name: "{group}\{cm:Uninst}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Kết cấu VXT"; Filename: "{app}\KetCauVXT.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Classes\.kcvxt"; ValueType: string; ValueName: ""; ValueData: "KetCauVXT.DuAn"; Flags: uninsdeletevalue; Tasks: assoc
Root: HKA; Subkey: "Software\Classes\.kcvxt\OpenWithProgids"; ValueType: string; ValueName: "KetCauVXT.DuAn"; ValueData: ""; Flags: uninsdeletevalue; Tasks: assoc
Root: HKA; Subkey: "Software\Classes\KetCauVXT.DuAn"; ValueType: string; ValueName: ""; ValueData: "Dự án Kết cấu VXT"; Flags: uninsdeletekey; Tasks: assoc
Root: HKA; Subkey: "Software\Classes\KetCauVXT.DuAn\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\KetCauVXT.exe,0"; Tasks: assoc
Root: HKA; Subkey: "Software\Classes\KetCauVXT.DuAn\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\KetCauVXT.exe"" ""%1"""; Tasks: assoc
; gỡ mục "Programs and Features" của bản cài thủ công cũ (CaiDat.ps1)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\KetCauVXT"; Flags: deletekey dontcreatekey

[Run]
Filename: "{app}\KetCauVXT.exe"; Description: "{cm:RunApp}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{autoappdata}\Autodesk\ApplicationPlugins\KetCauVXT.bundle"

[Code]
// [HướngDẫn] chỉ tạo lối tắt "Hướng dẫn sử dụng (Word)" khi file đã được cài (bộ kiểm thử có sinh file)
function GuideDocxExists: Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\HuongDanSuDung.docx'));
end;

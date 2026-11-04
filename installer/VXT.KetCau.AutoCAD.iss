; Bộ cài VXT Kết cấu cho AutoCAD 2022–2026 (Inno Setup 6)
; Cài vào %AppData%\Autodesk\ApplicationPlugins\VXTKetCau.bundle — AutoCAD tự nạp khi khởi động, không cần quyền quản trị.
#define AppVer GetEnv("APP_VERSION")
#if AppVer == ""
  #define AppVer "1.0.0"
#endif

[Setup]
AppId={{1CB25A65-5565-4CBC-AD23-6172FDA48B60}
AppName=VXT Kết cấu cho AutoCAD
AppVersion={#AppVer}
AppPublisher=VXT - Vũ Xuân Tuyến
DefaultDirName={userappdata}\Autodesk\ApplicationPlugins\VXTKetCau.bundle
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=VXT.KetCau.AutoCAD-Setup-{#AppVer}
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
CloseApplications=yes
UninstallDisplayName=VXT Kết cấu cho AutoCAD
WizardStyle=modern

[Files]
Source: "..\dist\VXTKetCau.bundle\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Messages]
WelcomeLabel2=Cài đặt [name/ver] cho AutoCAD 2022–2026.%n%nHãy đóng AutoCAD trước khi cài. Sau khi cài, mở AutoCAD và gõ lệnh VXTHS.
FinishedLabel=Đã cài xong [name]. Mở AutoCAD và gõ lệnh VXTHS để bắt đầu.

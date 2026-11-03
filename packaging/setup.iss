; Bộ cài Windows, biên dịch bằng Inno Setup 6
#define AppName "VXT Giải Kết Cấu"
#define AppVer GetEnv("APP_VERSION")
#if AppVer == ""
  #define AppVer "1.0.0"
#endif
[Setup]
AppId={{7E0C5A52-2B1E-4E7E-9A77-6E1B3C5D9A10}
AppName={#AppName}
AppVersion={#AppVer}
AppPublisher=Vũ Xuân Tuyến
DefaultDirName={autopf}\VXTGiaiKetCau
DefaultGroupName={#AppName}
OutputDir=..\dist-installer
OutputBaseFilename=VXTGiaiKetCau_Setup_{#AppVer}
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
UninstallDisplayName={#AppName}
[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
[Tasks]
Name: "desktopicon"; Description: "Tạo biểu tượng trên màn hình"; Flags: unchecked
[Files]
Source: "..\dist\VXTGiaiKetCau\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\VXTGiaiKetCau.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\VXTGiaiKetCau.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\VXTGiaiKetCau.exe"; Description: "Mở {#AppName}"; Flags: nowait postinstall skipifsilent

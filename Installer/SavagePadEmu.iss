; SavagePadEmulator installer (Inno Setup 6)
; Build with: Installer\Build-Installer.ps1
#define MyAppName "SavagePadEmulator"
#define MyAppVersion "1.0.3"
#define MyAppPublisher "SavagePadEmulator"
#define MyAppExeName "SavagePadEmu.exe"
#define PublishDir "..\\publish\\win-x64"

[Setup]
AppId={{D4C920B2-63DB-4D6E-9A7A-B6AAEDB23B0E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts
OutputBaseFilename=SavagePadEmulator-Setup-{#MyAppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  MsgBox('SavagePadEmulator requires the ViGEmBus virtual controller driver to emulate an Xbox controller.' + #13#10 + #13#10 +
         'This installer installs SavagePadEmulator only. Install or repair ViGEmBus separately before starting emulation.',
         mbInformation, MB_OK);
end;

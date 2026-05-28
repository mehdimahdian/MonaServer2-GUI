; MonaServer2 GUI — Windows Installer
; Author: Mehdi Mahdian <m.mahdian@gmail.com>
; https://github.com/mehdimahdian/MonaServer2-GUI
;
; Build from repo root:
;   iscc installer\windows\MonaServer2-GUI.iss /DMyAppVersion=1.0.0
;
; Requires: Inno Setup 6.3+

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName        "MonaServer2 GUI"
#define MyAppPublisher   "Mehdi Mahdian"
#define MyAppURL         "https://github.com/mehdimahdian/MonaServer2-GUI"
#define MyAppExeName     "MonaServer2.Desktop.exe"
#define MyServiceExe     "MonaServer2.Service.exe"
#define MyServiceName    "MonaServer2GUI"
#define MyServiceDisplay "MonaServer2 GUI Service"
#define MyServiceDesc    "Manages the MonaServer2 process and hosts the REST API, SignalR hub, and web dashboard."

[Setup]
AppId={{A7B3C2D1-E4F5-4A6B-8C7D-9E0F1A2B3C4D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\..\LICENSE
OutputDir=..\..\dist\installer
OutputBaseFilename=MonaServer2-GUI-{#MyAppVersion}-win-x64-Setup
SetupIconFile=..\..\src\MonaServer2.Desktop\Assets\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=120
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
CloseApplications=yes
CloseApplicationsFilter=MonaServer2.Desktop.exe,MonaServer2.Service.exe
RestartApplications=no
; Signing is handled outside Inno Setup by Sign-Binaries.ps1 in CI

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";     Description: "Create a &desktop shortcut";           GroupDescription: "Additional shortcuts:"; Flags: checkedonce
Name: "startservice";    Description: "&Start the companion service at login"; GroupDescription: "Service options:";    Flags: checkedonce
Name: "launchafterinstall"; Description: "&Launch MonaServer2 GUI after installation"; GroupDescription: ""; Flags: checkedonce

[Dirs]
Name: "{app}\service"
Name: "{app}\tools\monaserver2"
Name: "{app}\obs-plugin\win-x64"
Name: "{localappdata}\MonaServer2-GUI\logs"; Flags: uninsneveruninstall

[Files]
; ── Desktop application ──────────────────────────────────────────────────────
Source: "..\..\publish\win-x64\desktop\{#MyAppExeName}"; DestDir: "{app}";         Flags: ignoreversion signonce
Source: "..\..\publish\win-x64\desktop\*";               DestDir: "{app}";         Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "{#MyAppExeName}"

; ── Companion service ────────────────────────────────────────────────────────
Source: "..\..\publish\win-x64\service\{#MyServiceExe}"; DestDir: "{app}\service"; Flags: ignoreversion signonce
Source: "..\..\publish\win-x64\service\*";               DestDir: "{app}\service"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "{#MyServiceExe}"

; ── MonaServer2 binary (bundled) ─────────────────────────────────────────────
Source: "..\..\tools\monaserver2\*"; DestDir: "{app}\tools\monaserver2"; \
        Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; ── OBS plugin (optional — included if built) ────────────────────────────────
Source: "..\..\obs-plugin\build\Release\obs-mona-live.dll"; DestDir: "{app}\obs-plugin\win-x64"; \
        Flags: ignoreversion skipifsourcedoesntexist signonce
Source: "..\..\obs-plugin\data\*"; DestDir: "{app}\obs-plugin\data"; \
        Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
; Start Menu
Name: "{group}\{#MyAppName}";              Filename: "{app}\{#MyAppExeName}";     IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Web Dashboard";             Filename: "http://localhost:8080";     IconFilename: "{sys}\shell32.dll"; IconIndex: 13
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"; IconFilename: "{app}\{#MyAppExeName}"

; Desktop
Name: "{userdesktop}\{#MyAppName}";        Filename: "{app}\{#MyAppExeName}";     IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Remember install path for other tools to find the service
Root: HKLM; Subkey: "SOFTWARE\{#MyAppPublisher}\{#MyAppName}"; \
        ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\{#MyAppPublisher}\{#MyAppName}"; \
        ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"; Flags: uninsdeletekey

; Auto-start tray icon with Windows (optional — user-driven)
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
        ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; \
        Flags: uninsdeletevalue; Tasks: startservice

[Run]
; ── Install and start the companion service ───────────────────────────────────
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}"; \
        Flags: runhidden waituntilterminated; StatusMsg: "Stopping existing service..."; \
        Check: IsServiceInstalled

Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; \
        Flags: runhidden waituntilterminated; StatusMsg: "Removing old service entry..."; \
        Check: IsServiceInstalled

Filename: "{sys}\sc.exe"; \
        Parameters: "create {#MyServiceName} binPath= ""{app}\service\{#MyServiceExe}"" start= auto DisplayName= ""{#MyServiceDisplay}"""; \
        Flags: runhidden waituntilterminated; StatusMsg: "Installing service..."

Filename: "{sys}\sc.exe"; \
        Parameters: "description {#MyServiceName} ""{#MyServiceDesc}"""; \
        Flags: runhidden waituntilterminated

Filename: "{sys}\sc.exe"; Parameters: "start {#MyServiceName}"; \
        Flags: runhidden waituntilterminated; StatusMsg: "Starting service..."

; ── Launch app after install ─────────────────────────────────────────────────
Filename: "{app}\{#MyAppExeName}"; \
        Description: "Launch {#MyAppName}"; \
        Flags: nowait postinstall skipifsilent; \
        Tasks: launchafterinstall

[UninstallRun]
; Stop and remove the Windows Service before file deletion
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}";   Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden waituntilterminated

[Code]
// ── Helper: check if the service already exists ─────────────────────────────
function IsServiceInstalled(): Boolean;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\sc.exe'), 'query {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := (ResultCode = 0);
end;

// ── Before install: warn if OBS is open ─────────────────────────────────────
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

// ── After uninstall: offer to keep user data ─────────────────────────────────
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
  ResultCode: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{localappdata}\MonaServer2-GUI');
    if DirExists(DataDir) then
    begin
      if MsgBox('Remove user data (logs, configuration cache) from:' + #13#10 + DataDir + '?',
                mbConfirmation, MB_YESNO) = IDYES then
        DelTree(DataDir, True, True, True);
    end;
  end;
end;

; Inno Setup script for Mona Live Output OBS Plugin (Windows x64)
; Build: iscc installer\windows-installer.iss  (from obs-plugin\ directory)

#define AppName     "Mona Live Output for OBS"
#define AppVersion  "1.0.0"
#define AppPublisher "Mehdi Mahdian"
#define AppURL      "https://github.com/mehdimahdian/MonaServer2-GUI"
#define PluginName  "obs-mona-live"

[Setup]
AppId={{3A7C2F1B-8D4E-4A5F-9C6B-1E2D3F4A5B6C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
DefaultDirName={autopf}\obs-studio
DefaultGroupName={#AppName}
DisableDirPage=yes
OutputDir=..\dist
OutputBaseFilename=obs-mona-live-{#AppVersion}-windows-x64-installer
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
LicenseFile=..\LICENSE
SetupIconFile=
UninstallDisplayIcon={app}\obs-plugins\64bit\{#PluginName}.dll
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Plugin DLL
Source: "..\build\Release\{#PluginName}.dll";  DestDir: "{app}\obs-plugins\64bit"; Flags: ignoreversion

; libsrt runtime (must be present on the system or bundled)
Source: "..\deps\srt.dll";   DestDir: "{app}\obs-plugins\64bit"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\deps\libcurl.dll"; DestDir: "{app}\obs-plugins\64bit"; Flags: ignoreversion skipifsourcedoesntexist

; Locale data
Source: "..\data\*"; DestDir: "{app}\data\obs-plugins\{#PluginName}"; Flags: ignoreversion recursesubdirs

[Registry]
Root: HKLM; Subkey: "SOFTWARE\{#AppPublisher}\{#PluginName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey

[UninstallDelete]
Type: filesandordirs; Name: "{app}\data\obs-plugins\{#PluginName}"
Type: files;          Name: "{app}\obs-plugins\64bit\{#PluginName}.dll"

[Messages]
WelcomeLabel2=This will install [name/ver] — an ultra-low-latency WebRTC/SRT output plugin for OBS Studio that streams via MonaServer2.%n%nPowered by MonaServer2 (MonaSolutions / Haivision). GPLv2.

[Code]
function OBSInstalled(): Boolean;
begin
  Result := DirExists(ExpandConstant('{autopf}\obs-studio\obs-plugins\64bit'));
end;

function InitializeSetup(): Boolean;
begin
  if not OBSInstalled() then begin
    MsgBox(
      'OBS Studio (64-bit) does not appear to be installed.' + #13#10 +
      'Please install OBS Studio from https://obsproject.com before continuing.',
      mbWarning, MB_OK);
  end;
  Result := True;
end;

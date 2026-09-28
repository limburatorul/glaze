; Inno Setup script for Glaze. Build it through build.ps1, which publishes the app first and passes the
; version in. Modelled on File Labs' installer (vault: File Labs/Release si update.md).

#define AppName "Glaze"
#define AppExe "Glaze.exe"
#define AppPublisher "Protagonist Labs"

#ifndef AppVersion
  #define AppVersion "2.0.0"
#endif

[Setup]
; Never change AppId: it is what ties an update to the installation it replaces.
AppId={{93E393FD-2A35-4E0F-9CEE-B98EC1665065}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://protagonistlabs.app/glaze/
VersionInfoVersion={#AppVersion}

; Per user, like the Electron build was: no UAC prompt, and the updater can run it silently.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

CloseApplications=force
RestartApplications=no

OutputDir=..\dist
; Same name as the Electron build's: the site's download links are built from it.
OutputBaseFilename=Glaze-Setup-{#AppVersion}
SetupIconFile=..\build\icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "..\dist\app\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Straight in the Start menu, where Glaze 1.x put it, so it takes that shortcut's place.
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; The unpacked page files and WebView2's data (sign-in included). Settings in %APPDATA%\Glaze stay.
Type: filesandordirs; Name: "{localappdata}\Glaze"

[Code]
const
  { electron-builder's key for Glaze 1.x: a UUID derived from the old appId, the same on every machine. }
  ElectronKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\7acadde0-7765-52d7-add8-46fd458819c4';

{ Setup cannot replace a file the running app holds open, so stop it first. }
procedure StopGlaze();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(300);
end;

{ Glaze 1.x (Electron) installed itself into the same folder under its own uninstaller. It is removed
  quietly first, so the Start menu is left with one Glaze. Its settings.json is kept and read by 2.x.
  An NSIS uninstaller normally copies itself to %TEMP%, starts that copy and returns at once - which let
  it delete this installer's new files after they were copied. "_?=<folder>" (last, unquoted) makes it
  run in place and return when done; what it then cannot delete (itself, the folder) is deleted here. }
procedure RemoveElectronGlaze();
var
  Command, Uninstaller, Dir: String;
  ResultCode: Integer;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, ElectronKey, 'UninstallString', Command) then exit;
  { "C:\...\glaze\Uninstall Glaze.exe" /currentuser }
  Uninstaller := Copy(Command, 2, Pos('"', Copy(Command, 2, Length(Command))) - 1);
  Dir := ExtractFileDir(Uninstaller);
  if not FileExists(Uninstaller) then exit;
  Exec(Uninstaller, '/currentuser /S _?=' + Dir, Dir, SW_HIDE, ewWaitUntilTerminated, ResultCode);
  DelTree(Dir, True, True, True);
  RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, ElectronKey);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopGlaze();
  RemoveElectronGlaze();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopGlaze();
  Result := True;
end;

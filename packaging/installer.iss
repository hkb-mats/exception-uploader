; The Windows installer, built by .github/workflows/uploader.yml:
;   iscc /DAppVersion=1.0.0 /DSourceExe=<path to ExceptionUploader.exe> /O<output folder> installer.iss
; Installs for the current user, so it needs no administrator rights.
; The release workflow adds /DSign=1 and /Ssigner=<command>, which signs the uninstaller and the installer.

[Setup]
AppId={{6F0C2B7E-4B1D-4E7A-9C33-2A5D8E1F7B40}
AppName=Exception Uploader
AppVersion={#AppVersion}
AppPublisher=Exception
DefaultDirName={localappdata}\Programs\Exception Uploader
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputBaseFilename=Exception-Uploader-Setup
SetupIconFile=..\src\Exception.Uploader\Assets\icon.ico
UninstallDisplayIcon={app}\ExceptionUploader.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
#ifdef Sign
SignTool=signer
#endif

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\Exception Uploader"; Filename: "{app}\ExceptionUploader.exe"

[Registry]
; The app adds this value itself; uninstalling takes it away
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "Exception Uploader"; ValueType: none; Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\ExceptionUploader.exe"; Description: "Open Exception Uploader"; Flags: nowait postinstall

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM ExceptionUploader.exe /F"; Flags: runhidden; RunOnceId: "StopUploader"

#define AppName "SwExportAddin"
#define AppVersion "1.0.0"
#define AppPublisher "SwExportAddin"
#define AppURL ""
#define AddinGuid "CE1AB140-57A9-4C42-82C5-57FF212F9941"
#define RegAsmPath "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\regasm.exe"

[Setup]
AppId={{A1C3E4F2-8F5E-4B5E-9E74-8E0E2D7F4B10}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
DefaultDirName={pf64}\SwExportAddin
DefaultGroupName=SwExportAddin
OutputDir=Output
OutputBaseFilename=SwExportAddin_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\SwExportAddin.dll
CloseApplications=yes
RestartApplications=yes

[Files]
Source: "SwExportAddin\bin\x64\Release\SwExportAddin.dll"; DestDir: "{app}"; Flags: ignoreversion

[UninstallDelete]
Type: dirifempty; Name: "{app}"

[Registry]
; HKLM - rama general (formato recomendado con llaves)
Root: HKLM; Subkey: "SOFTWARE\SolidWorks\Addins\{{{#AddinGuid}}}"; ValueType: dword; ValueName: ""; ValueData: 1; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\SolidWorks\Addins\{{{#AddinGuid}}}"; ValueType: string; ValueName: "Description"; ValueData: "Export PDF/DWG Batch"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\SolidWorks\Addins\{{{#AddinGuid}}}"; ValueType: string; ValueName: "Title"; ValueData: "SwExportAddin"; Flags: uninsdeletekey

; HKLM - rama SOLIDWORKS 2025
Root: HKLM; Subkey: "SOFTWARE\SolidWorks\SOLIDWORKS 2025\Addins\{{{#AddinGuid}}}"; ValueType: dword; ValueName: ""; ValueData: 1; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\SolidWorks\SOLIDWORKS 2025\Addins\{{{#AddinGuid}}}"; ValueType: string; ValueName: "Description"; ValueData: "Export PDF/DWG Batch"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\SolidWorks\SOLIDWORKS 2025\Addins\{{{#AddinGuid}}}"; ValueType: string; ValueName: "Title"; ValueData: "SwExportAddin"; Flags: uninsdeletekey

; HKCU startup
Root: HKCU; Subkey: "Software\SolidWorks\AddInsStartup\{{{#AddinGuid}}}"; ValueType: dword; ValueName: ""; ValueData: 1; Flags: uninsdeletekey

[Code]
function InitializeSetup: Boolean;
begin
  Result := IsAdminLoggedOn;
  if not Result then
    MsgBox('Este instalador requiere permisos de administrador.', mbError, MB_OK);
end;

procedure CleanupLegacyKeys;
var
  GuidPlain: string;
begin
  GuidPlain := '{#AddinGuid}';
  RegDeleteKeyIncludingSubkeys(HKLM, 'SOFTWARE\SolidWorks\Addins\' + GuidPlain);
  RegDeleteKeyIncludingSubkeys(HKLM, 'SOFTWARE\SolidWorks\SOLIDWORKS 2025\Addins\' + GuidPlain);
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\SolidWorks\AddInsStartup\' + GuidPlain);
end;

procedure EnsureAddinEnabled;
var
  GuidBraced: string;
begin
  GuidBraced := '{' + '{#AddinGuid}' + '}';

  RegWriteDWordValue(HKLM, 'SOFTWARE\SolidWorks\Addins\' + GuidBraced, '', 1);
  RegWriteDWordValue(HKLM, 'SOFTWARE\SolidWorks\SOLIDWORKS 2025\Addins\' + GuidBraced, '', 1);
  RegWriteDWordValue(HKCU, 'Software\SolidWorks\AddInsStartup\' + GuidBraced, '', 1);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  Params: string;
begin
  if CurStep = ssPostInstall then
  begin
    CleanupLegacyKeys;
    EnsureAddinEnabled;

    Params := '/codebase "' + ExpandConstant('{app}\SwExportAddin.dll') + '"';
    if not Exec('{#RegAsmPath}', Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      MsgBox('No se pudo ejecutar RegAsm.', mbError, MB_OK)
    else if ResultCode <> 0 then
      MsgBox('RegAsm devolvió error: ' + IntToStr(ResultCode), mbError, MB_OK);

    EnsureAddinEnabled;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
  Params: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    Params := '/u "' + ExpandConstant('{app}\SwExportAddin.dll') + '"';
    Exec('{#RegAsmPath}', Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

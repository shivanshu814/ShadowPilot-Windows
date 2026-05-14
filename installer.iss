; Inno Setup script — generates ShadowPilot-Setup.exe
; Built automatically by GitHub Actions on Windows runner

#define AppName      "ShadowPilot"
#define AppVersion   "1.0"
#define AppPublisher "ShadowPilot"
#define AppExeName   "ShadowPilot.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=dist
OutputBaseFilename=ShadowPilot-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayName={#AppName}
SetupIconFile=
; No icon file yet — Inno will use default

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "publish\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}";  Filename: "{app}\{#AppExeName}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
var
  ApiKeyPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  ApiKeyPage := CreateInputQueryPage(
    wpSelectDir,
    'API Key Setup',
    'Enter your OpenAI API key',
    'ShadowPilot needs an OpenAI API key (sk-...) to generate answers.' + #13#10 +
    'You can also skip and add it later to %USERPROFILE%\.shadowpilot.env'
  );
  ApiKeyPage.Add('OPENAI_API_KEY:', False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  KeyVal, EnvContent, EnvPath: String;
begin
  if CurStep = ssPostInstall then
  begin
    KeyVal := Trim(ApiKeyPage.Values[0]);
    EnvPath := ExpandConstant('{%USERPROFILE}') + '\.shadowpilot.env';

    if (KeyVal <> '') and (Pos('sk-', KeyVal) = 1) then
      EnvContent := 'OPENAI_API_KEY=' + KeyVal + #13#10
    else
      EnvContent := '# OPENAI_API_KEY=sk-your-key-here' + #13#10 +
                    '# OPENROUTER_API_KEY=sk-or-your-key-here' + #13#10;

    SaveStringToFile(EnvPath, EnvContent, False);
  end;
end;

; مثبت أربش المستقل لويندوز.
; يضع الأمر arbsh وحده في bin على PATH، والطرفية الرسومية في terminal خارج PATH
; حتى لا تظهر مكتبات .NET المرافقة في بحث النظام عن المكتبات.

#define MyAppId "{{37388BF3-5853-4720-90FD-AB45B9B9E7A7}"
#define MyAppName "أربش"
#ifndef MyAppVersion
  #define MyAppVersion "0.9.1"
#endif
#ifndef ArbShCommandDir
  #define ArbShCommandDir "dist\payload\bin"
#endif
#ifndef ArbShTerminalDir
  #define ArbShTerminalDir "dist\payload\terminal"
#endif
#define MyAppPublisher "Omar Aglan"
#define MyAppURL "https://github.com/RuqoomTech/ArbSh"
#define MyCommandExeName "arbsh.exe"
#define MyTerminalExeName "ArbSh.Terminal.exe"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\ArbSh
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes
OutputDir=dist\installer
OutputBaseFilename=arbsh-setup-{#MyAppVersion}-x64
#ifdef InstallerSignTool
SignTool={#InstallerSignTool}
SignedUninstaller=yes
#endif
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern dynamic windows11 hidebevels
WizardSizePercent=110,110
DisableWelcomePage=no
WizardImageFile=installer\wizard-sidebar.png
WizardImageFileDynamicDark=installer\wizard-sidebar.png
WizardSmallImageFile=installer\wizard-mark.png
WizardSmallImageFileDynamicDark=installer\wizard-mark.png
WizardImageStretch=yes
LZMANumBlockThreads=1
CompressionThreads=1
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog commandline
ChangesEnvironment=yes
SetupLogging=yes
SetupIconFile=src_csharp\ArbSh.Terminal\Assets\Brand\ArbShIcon.ico
UsePreviousAppDir=yes
UsePreviousLanguage=yes
UsePreviousTasks=yes
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\terminal\{#MyTerminalExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}

[LangOptions]
DialogFontName=Segoe UI
DialogFontSize=10
WelcomeFontName=Segoe UI

[Messages]
arabic.WelcomeLabel1=صدفة وطرفية عربية
arabic.WelcomeLabel2=أربش صدفة عربية أولًا وطرفية مستقلة.%n%nيضيف المثبت الأمر arbsh إلى PATH%nلتستدعيه السكربتات والأدوات الأخرى.
english.WelcomeLabel1=An Arabic shell and terminal
english.WelcomeLabel2=ArbSh is an Arabic-first shell with a standalone terminal.%n%nSetup adds the arbsh command to PATH for scripts and other tools.

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "contextmenu"; Description: "إضافة «افتح أربش هنا» إلى قائمة المجلدات"; GroupDescription: "تكامل مستكشف الملفات:"
Name: "desktopicon"; Description: "إنشاء اختصار على سطح المكتب"; GroupDescription: "اختصارات إضافية:"; Flags: unchecked

[Files]
Source: "{#ArbShCommandDir}\{#MyCommandExeName}"; DestDir: "{app}\bin"; Flags: ignoreversion
Source: "{#ArbShTerminalDir}\*"; DestDir: "{app}\terminal"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "README.md"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "docs\CHANGELOG.md"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "docs\USAGE_EXAMPLES.md"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "docs\ARBSH_HOST_V1.md"; DestDir: "{app}\docs"; Flags: ignoreversion

[InstallDelete]
; الترقية والإصلاح يستبدلان الحمولة كاملة فلا تبقى ملفات من إصدار سابق.
Type: filesandordirs; Name: "{app}\bin"
Type: filesandordirs; Name: "{app}\terminal"
Type: filesandordirs; Name: "{app}\docs"

[Registry]
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\ArbSh"; ValueType: string; ValueData: "افتح أربش هنا"; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\ArbSh"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\terminal\{#MyTerminalExeName}"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\ArbSh\command"; ValueType: string; ValueData: """{app}\terminal\{#MyTerminalExeName}"" --working-dir ""%V"""; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\shell\ArbSh"; ValueType: string; ValueData: "افتح أربش هنا"; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\shell\ArbSh"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\terminal\{#MyTerminalExeName}"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\shell\ArbSh\command"; ValueType: string; ValueData: """{app}\terminal\{#MyTerminalExeName}"" --working-dir ""%1"""; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Drive\shell\ArbSh"; ValueType: string; ValueData: "افتح أربش هنا"; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Drive\shell\ArbSh"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\terminal\{#MyTerminalExeName}"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Drive\shell\ArbSh\command"; ValueType: string; ValueData: """{app}\terminal\{#MyTerminalExeName}"" --working-dir ""%1"""; Tasks: contextmenu

[Icons]
Name: "{autoprograms}\أربش\أربش"; Filename: "{app}\terminal\{#MyTerminalExeName}"; Parameters: "--working-dir ~"
Name: "{autoprograms}\أربش\دليل أربش"; Filename: "{app}\docs\USAGE_EXAMPLES.md"
Name: "{autoprograms}\أربش\إزالة أربش"; Filename: "{uninstallexe}"
Name: "{autodesktop}\أربش"; Filename: "{app}\terminal\{#MyTerminalExeName}"; Parameters: "--working-dir ~"; Tasks: desktopicon

[Run]
Filename: "{app}\terminal\{#MyTerminalExeName}"; Description: "تشغيل أربش"; Flags: nowait postinstall skipifsilent unchecked

[Code]
#include "installer\windows_wizard.iss"
#include "installer\windows_environment.iss"
#include "installer\windows_scope_migration.iss"

const
  ARBSH_INSTALLER_KEY = 'Software\BaaEcosystem\ArbSh';
  ARBSH_PATH_OWNED_VALUE = 'PathOwned';

function PrepareToInstall(var NeedsRestart: Boolean): string;
begin
  Result := EcoMigrateOppositeInstall('{#MyAppName}',
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{37388BF3-5853-4720-90FD-AB45B9B9E7A7}_is1');
end;

procedure ArbShRegistryRoot(var Root: Integer);
begin
  if IsAdminInstallMode then
    Root := HKLM
  else
    Root := HKCU;
end;

function ArbShPathOwned: Boolean;
var
  Root: Integer;
  Value: Cardinal;
begin
  ArbShRegistryRoot(Root);
  Result := RegQueryDWordValue(Root, ARBSH_INSTALLER_KEY, ARBSH_PATH_OWNED_VALUE, Value) and
    (Value = 1);
end;

procedure ApplyArbShEnvironment;
var
  Root: Integer;
begin
  ArbShRegistryRoot(Root);
  if EcoEnsurePathContains(ExpandConstant('{app}\bin')) then
    RegWriteDWordValue(Root, ARBSH_INSTALLER_KEY, ARBSH_PATH_OWNED_VALUE, 1);
  RegWriteStringValue(Root, ARBSH_INSTALLER_KEY, 'InstallLocation',
    ExpandConstant('{app}'));
  RegWriteStringValue(Root, ARBSH_INSTALLER_KEY, 'Version',
    '{#MyAppVersion}');
end;

function RunArbShHealthProbe: Boolean;
var
  ExitCode: Integer;
begin
  Result :=
    FileExists(ExpandConstant('{app}\terminal\{#MyTerminalExeName}')) and
    Exec(ExpandConstant('{app}\bin\{#MyCommandExeName}'), '--version',
      ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ExitCode) and
    (ExitCode = 0);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    ApplyArbShEnvironment;
    EcoBroadcastEnvironmentChange;
    if not RunArbShHealthProbe then
      EcoInstallFailed('فشل فحص أربش بعد التثبيت.');
    if not WizardSilent then
      MsgBox('اكتمل تثبيت أربش. افتح طرفية جديدة لاستخدام الأمر arbsh.',
        mbInformation, MB_OK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Root: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if ArbShPathOwned then
      EcoEnsurePathRemoved(ExpandConstant('{app}\bin'));
    ArbShRegistryRoot(Root);
    RegDeleteKeyIncludingSubkeys(Root, ARBSH_INSTALLER_KEY);
    EcoBroadcastEnvironmentChange;
  end;
end;

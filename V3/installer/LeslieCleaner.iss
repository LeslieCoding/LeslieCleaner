#define MyAppName "LeslieCleaner"
#define MyAppVersion "3.0.0"
#define MyAppPublisher "LeslieCleaner"
#define MyAppExeName "LeslieCleaner.App.exe"
#define PublishDir "D:\SoftWare\leslieCleaner\V3\installer\publish"

[Setup]
AppId={{B7E3A1C4-2F5D-4A8E-9C1B-3D5E7F9A1B2C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\LeslieCleaner
DefaultGroupName=LeslieCleaner
OutputDir=D:\SoftWare\leslieCleaner\V3\installer\output
OutputBaseFilename=LeslieCleanerSetup-{#MyAppVersion}
SetupIconFile=D:\SoftWare\leslieCleaner\V3\assets\lesliecleaner.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\LeslieCleaner"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall LeslieCleaner"; Filename: "{uninstallexe}"
Name: "{autodesktop}\LeslieCleaner"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch LeslieCleaner"; Flags: nowait postinstall skipifsilent

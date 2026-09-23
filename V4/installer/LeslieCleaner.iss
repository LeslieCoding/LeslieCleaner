#define MyAppName "LeslieCleaner"
#define MyAppVersion "4.0.0"
#define MyAppPublisher "LeslieCleaner"
#define MyAppExeName "LeslieCleaner.App.exe"
#define PublishDir SourcePath + "\publish"

[Setup]
AppId={{B7E3A1C4-2F5D-4A8E-9C1B-3D5E7F9A1B2C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\LeslieCleaner
DefaultGroupName=LeslieCleaner
OutputDir={#SourcePath}\output
OutputBaseFilename=LeslieCleanerSetup-{#MyAppVersion}
SetupIconFile={#SourcePath}\..\assets\lesliecleaner.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
AllowRootDirectory=no
UsePreviousAppDir=yes
UsePreviousTasks=yes
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
Uninstallable=yes

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked
Name: "startmenuicon"; Description: "创建开始菜单快捷方式"; Flags: checkedonce

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.xml,*.trx,*Test*"; Flags: ignoreversion recursesubdirs createallsubdirs restartreplace

[Dirs]
; 数据库、注册表备份、隔离区和完整性清单由普通用户运行的主程序持续更新。
; 明确授予 Users 修改权限，避免管理员安装/基线配置后普通用户只能创建却不能修改已有文件。
Name: "{commonappdata}\LeslieCleaner"; Permissions: users-modify

[Icons]
Name: "{group}\LeslieCleaner"; Filename: "{app}\{#MyAppExeName}"; Tasks: startmenuicon
Name: "{group}\卸载 LeslieCleaner"; Filename: "{uninstallexe}"; Tasks: startmenuicon
Name: "{autodesktop}\LeslieCleaner"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 LeslieCleaner"; Flags: nowait postinstall skipifsilent

[Code]
var
  RemoveUserData: Boolean;
  UpgradeBackupPath: String;

function IsPathWithin(const Candidate, Parent: String): Boolean;
var
  C, P: String;
begin
  C := AddBackslash(Lowercase(ExpandFileName(Candidate)));
  P := AddBackslash(Lowercase(ExpandFileName(Parent)));
  Result := Pos(P, C) = 1;
end;

function HasEnoughSpace(const Candidate: String): Boolean;
var
  FreeBytes, TotalBytes: Int64;
  DriveRoot: String;
begin
  DriveRoot := ExtractFileDrive(Candidate) + '\';
  Result := GetSpaceOnDisk64(DriveRoot, FreeBytes, TotalBytes) and
    (FreeBytes >= 536870912);
end;

function IsInstallPathValid(const Candidate: String; var Reason: String): Boolean;
var
  Expanded: String;
begin
  Result := False;
  Expanded := RemoveBackslashUnlessRoot(ExpandFileName(Candidate));
  if (Length(Expanded) <= 3) or (CompareText(Expanded, ExtractFileDrive(Expanded) + '\') = 0) then
  begin
    Reason := '不能安装到磁盘根目录，请选择专用的 LeslieCleaner 文件夹。';
    Exit;
  end;
  if IsPathWithin(Expanded, ExpandConstant('{win}')) or
     IsPathWithin(Expanded, ExpandConstant('{sys}')) then
  begin
    Reason := '不能安装到 Windows 系统目录。';
    Exit;
  end;
  if IsPathWithin(Expanded, ExpandConstant('{commonappdata}')) then
  begin
    Reason := 'ProgramData 用于用户数据，不应作为程序安装目录。';
    Exit;
  end;
  if not HasEnoughSpace(Expanded) then
  begin
    Reason := '目标磁盘可用空间不足 512 MB，或安装器无法读取磁盘空间。';
    Exit;
  end;
  Result := True;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Reason: String;
begin
  Result := True;
  if CurPageID = wpSelectDir then
  begin
    if not IsInstallPathValid(WizardDirValue, Reason) then
    begin
      MsgBox(Reason, mbError, MB_OK);
      Result := False;
    end;
  end;
end;

procedure CopyTree(const SourceDir, DestinationDir: String);
var
  FindRec: TFindRec;
  SourceItem, DestinationItem: String;
begin
  if not DirExists(SourceDir) then
    Exit;
  ForceDirectories(DestinationDir);
  if FindFirst(AddBackslash(SourceDir) + '*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
        begin
          SourceItem := AddBackslash(SourceDir) + FindRec.Name;
          DestinationItem := AddBackslash(DestinationDir) + FindRec.Name;
          if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
            CopyTree(SourceItem, DestinationItem)
          else if not CopyFile(SourceItem, DestinationItem, False) then
            RaiseException('无法备份升级数据：' + SourceItem);
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

procedure BackupUpgradeData;
var
  Stamp, UserSource, MachineSource, UserBackup, MachineBackup: String;
begin
  Stamp := GetDateTimeString('yyyymmdd_hhnnss', '-', ':');
  UserSource := ExpandConstant('{localappdata}\LeslieCleaner');
  MachineSource := ExpandConstant('{commonappdata}\LeslieCleaner');
  UserBackup := ExpandConstant('{localappdata}\LeslieCleaner.UpgradeBackups\Backup_') + Stamp;
  MachineBackup := ExpandConstant('{commonappdata}\LeslieCleaner.UpgradeBackups\Backup_') + Stamp;
  if (not IsAdminInstallMode) and DirExists(UserSource) then
  begin
    CopyTree(UserSource, UserBackup);
    UpgradeBackupPath := UserBackup;
  end;
  if IsAdminInstallMode and DirExists(MachineSource) then
  begin
    CopyTree(MachineSource, MachineBackup);
    if UpgradeBackupPath = '' then
      UpgradeBackupPath := MachineBackup;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExistingExe: String;
begin
  Result := '';
  ExistingExe := ExpandConstant('{app}\{#MyAppExeName}');
  if FileExists(ExistingExe) then
  begin
    if MsgBox(
      '检测到现有 LeslieCleaner。升级将保留数据库、设置、白名单和备份，并在替换程序文件前创建配置副本。是否继续？',
      mbConfirmation, MB_YESNO) <> IDYES then
    begin
      Result := '用户取消了升级。';
      Exit;
    end;
    try
      BackupUpgradeData;
    except
      Result := GetExceptionMessage;
    end;
  end;
end;

procedure WriteInstallRecord;
var
  LogDir, ModeText, RecordText: String;
begin
  if IsAdminInstallMode then
  begin
    ModeText := 'AllUsers';
    LogDir := ExpandConstant('{commonappdata}\LeslieCleaner\Logs');
  end
  else
  begin
    ModeText := 'CurrentUser';
    LogDir := ExpandConstant('{localappdata}\LeslieCleaner\Logs');
  end;
  ForceDirectories(LogDir);
  RecordText := 'Time=' + GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':') + #13#10 +
    'Version={#MyAppVersion}' + #13#10 +
    'InstallPath=' + ExpandConstant('{app}') + #13#10 +
    'Mode=' + ModeText + #13#10 +
    'UpgradeBackup=' + UpgradeBackupPath + #13#10 +
    'Result=Success' + #13#10;
  SaveStringToFile(AddBackslash(LogDir) + 'install.log', RecordText, True);
end;

procedure ProvisionIntegrityBaseline;
var
  ExecutablePath: String;
  ResultCode: Integer;
begin
  ExecutablePath := ExpandConstant('{app}\{#MyAppExeName}');
  if not Exec(
    ExecutablePath,
    '--provision-integrity-baseline',
    ExpandConstant('{app}'),
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) then
    RaiseException('Unable to start integrity baseline provisioning.');
  if ResultCode <> 0 then
    RaiseException(
      'Integrity baseline provisioning failed with exit code ' + IntToStr(ResultCode) +
      '. The application was left in safe read-only mode.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if not FileExists(ExpandConstant('{app}\{#MyAppExeName}')) then
      RaiseException('安装验证失败：主程序文件不存在。安装器将报告失败。');
    ProvisionIntegrityBaseline;
    WriteInstallRecord;
  end;
end;

function InitializeUninstall: Boolean;
var
  Choice: Integer;
begin
  if UninstallSilent then
  begin
    { 自动化/企业静默卸载默认只移除程序，绝不无提示删除数据库、日志和备份。 }
    RemoveUserData := False;
    Result := True;
    Exit;
  end;

  Choice := MsgBox(
    '请选择卸载方式：' + #13#10 + #13#10 +
    '“是” = 删除程序及 LeslieCleaner 应用数据（数据库、日志、备份和设置）' + #13#10 +
    '“否” = 仅删除程序，保留应用数据，便于以后恢复' + #13#10 +
    '“取消” = 不卸载' + #13#10 + #13#10 +
    '不会删除 LeslieCleaner 数据目录以外的个人文件。',
    mbConfirmation, MB_YESNOCANCEL);
  Result := Choice <> IDCANCEL;
  RemoveUserData := Choice = IDYES;
  if RemoveUserData then
    Result := MsgBox('此操作会永久删除 LeslieCleaner 的数据库、日志、备份和设置。确认继续？',
      mbConfirmation, MB_YESNO) = IDYES;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usUninstall) and RemoveUserData then
  begin
    if IsAdminInstallMode then
      DelTree(ExpandConstant('{commonappdata}\LeslieCleaner'), True, True, True)
    else
      DelTree(ExpandConstant('{localappdata}\LeslieCleaner'), True, True, True);
  end;
end;

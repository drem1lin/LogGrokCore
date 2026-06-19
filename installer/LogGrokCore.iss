#define MyAppName "LogGrokCore"
#define MyAppPublisher "LogGrokCore"
#define MyAppURL "https://github.com/drem1lin/LogGrokCore"
#define MyAppExeName "LogGrokCore.exe"

[Setup]
AppId={{B8A3C4E1-7F2D-4A5B-9E6C-1D2F3A4B5C6D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=LogGrokCore-{#MyAppVersion}-setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "fileassoc_log"; Description: "Associate with .log files"; GroupDescription: "File associations:"
Name: "fileassoc_txt"; Description: "Associate with .txt files"; GroupDescription: "File associations:"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Classes\.log\OpenWithProgids"; ValueType: string; ValueName: "LogGrokCore.log"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc_log
Root: HKA; Subkey: "Software\Classes\.txt\OpenWithProgids"; ValueType: string; ValueName: "LogGrokCore.txt"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc_txt
Root: HKA; Subkey: "Software\Classes\LogGrokCore.log"; ValueType: string; ValueName: ""; ValueData: "Log File"; Flags: uninsdeletekey; Tasks: fileassoc_log
Root: HKA; Subkey: "Software\Classes\LogGrokCore.log\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: fileassoc_log
Root: HKA; Subkey: "Software\Classes\LogGrokCore.log\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc_log
Root: HKA; Subkey: "Software\Classes\LogGrokCore.txt"; ValueType: string; ValueName: ""; ValueData: "Text File"; Flags: uninsdeletekey; Tasks: fileassoc_txt
Root: HKA; Subkey: "Software\Classes\LogGrokCore.txt\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: fileassoc_txt
Root: HKA; Subkey: "Software\Classes\LogGrokCore.txt\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc_txt

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

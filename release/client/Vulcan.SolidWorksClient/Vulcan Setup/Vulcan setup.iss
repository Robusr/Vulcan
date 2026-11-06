[Setup]
AppId={{03D4BCE0-7A21-4660-82A1-4CEEF0FBD651}
AppName=Vulcan
AppVersion=1.2.1
AppVerName=Vulcan 1.2.1
AppPublisher=Robusr
AppPublisherURL=https://github.com/Robusr/Vulcan
AppSupportURL=https://github.com/Robusr/Vulcan
AppUpdatesURL=https://github.com/Robusr/Vulcan
DefaultDirName={autopf}\Vulcan
DefaultGroupName=Vulcan
OutputDir=Output
OutputBaseFilename=Vulcan-Setup-1.2.1
Compression=lzma
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\..\..\..\LICENSE
DisableDirPage=no
DisableProgramGroupPage=yes
WizardImageFile=embedded\banner.bmp
WizardSmallImageFile=embedded\Robusr.bmp
SetupIconFile=embedded\Vulcan.ico
AlwaysUsePersonalGroup=no
AlwaysShowDirOnReadyPage=no
DisableReadyPage=yes
DisableWelcomePage=no
UninstallDisplayName=Vulcan AI For SolidWorks
UninstallDisplayIcon={app}\icons\Vulcan.ico
AppComments=Vulcan AI For SolidWorks
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
UsePreviousAppDir=yes
VersionInfoVersion=1.2.1.0
VersionInfoProductVersion=1.2.1
VersionInfoProductName=Vulcan
VersionInfoCompany=Robusr
VersionInfoTextVersion=v1.2.1
VersionInfoCopyright=GPL-3.0-or-later
VersionInfoDescription=Vulcan AI For SolidWorks

[Files]
Source: "..\dist\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Run]
Filename: "{app}\install.bat"; Description: "Register Vulcan with SolidWorks"; StatusMsg: "正在安装 Vulcan..."; Flags: runhidden waituntilterminated skipifdoesntexist; Parameters: "/quiet"

[UninstallRun]
Filename: "{app}\uninstall.bat"; StatusMsg: "正在卸载 Vulcan..."; Flags: runhidden waituntilterminated skipifdoesntexist; Parameters: "/quiet"; RunOnceId: "UnregisterVulcan"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

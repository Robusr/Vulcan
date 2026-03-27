[Setup]
AppName=Vulcan
AppVersion=v1.0.0
AppPublisher=Robusr
AppPublisherURL=https://github.com/Robusr/Vulcan
AppSupportURL=https://github.com/Robusr/Vulcan
AppUpdatesURL=https://github.com/Robusr/Vulcan
DefaultDirName=C:\Program Files\Vulcan
DefaultGroupName=Vulcan
OutputBaseFilename=Vulcan
Compression=lzma
DisableDirPage=no
DisableProgramGroupPage=yes
WizardImageFile=embedded\banner.bmp
WizardSmallImageFile=embedded\Robusr.bmp
SetupIconFile=embedded\Vulcan.ico
AlwaysUsePersonalGroup=no
AlwaysShowDirOnReadyPage=no 
DisableReadyPage=yes
DisableWelcomePage=no
UninstallDisplayName=Vulcan
UninstallDisplayIcon=embedded\Vulcan.ico
AppComments=Vulcan
VersionInfoVersion=1.0
VersionInfoCompany=Robusr
VersionInfoTextVersion=v1.0
VersionInfoCopyright=Code By Robusr 仅供学习交流，请勿用于商业用途
VersionInfoDescription=Vulcan AI For SolidWorks

[Files]
[Files]
Source: {pf}\*.*; DestDir: {app}; Flags: ignoreversion recursesubdirs createallsubdirs; Permissions: everyone-full

[Dirs]
Name: {app}; Permissions: everyone-full

[Run]
Filename: {app}\install.bat; Description: BAT; StatusMsg: 正在安装 Vulcan...; Flags: runhidden skipifdoesntexist

[UninstallRun]
Filename: {app}\uninstall.bat; StatusMsg: 正在卸载 Vulcan...; Flags: runhidden skipifdoesntexist

[UninstallDelete]
Type: filesandordirs; Name: {app}\*.*
Type: filesandordirs; Name: {app}\
set path=%~d0
cd %path%
cd /d %~dp0

RegAsm.exe Vulcan.SolidWorksClient.dll /codebase
pause


set path=%~d0
cd %path%
cd /d %~dp0

RegAsm.exe VulcanAddin.dll /codebase
pause


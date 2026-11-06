@echo off
setlocal EnableExtensions
cd /d "%~dp0"

rem Self-elevate: if not already admin, relaunch via PowerShell.
net session >nul 2>&1
if errorlevel 1 (
  echo [Vulcan] Requesting administrator privileges...
  set "ARGS=%*"
  if defined ARGS (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -ArgumentList '%ARGS%' -Verb RunAs"
  ) else (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  )
  exit /b %errorlevel%
)

set "QUIET=0"
if /I "%~1"=="/quiet" set "QUIET=1"

tasklist /FI "IMAGENAME eq SLDWORKS.exe" 2>nul | find /I "SLDWORKS.exe" >nul
if not errorlevel 1 (
  echo [Vulcan] Please close SolidWorks before installing Vulcan.
  if not "%QUIET%"=="1" pause
  exit /b 3
)

if not exist "Vulcan.SolidWorksClient.dll" (
  echo [Vulcan] Vulcan.SolidWorksClient.dll not found.
  if not "%QUIET%"=="1" pause
  exit /b 1
)

if not exist "RegAsm.exe" (
  echo [Vulcan] RegAsm.exe not found.
  if not "%QUIET%"=="1" pause
  exit /b 1
)

echo [Vulcan] Registering add-in...
RegAsm.exe "Vulcan.SolidWorksClient.dll" /codebase
if errorlevel 1 (
  echo [Vulcan] Registration failed.
  if not "%QUIET%"=="1" pause
  exit /b 1
)

echo [Vulcan] Add-in registered. Restart SolidWorks.
if not "%QUIET%"=="1" pause
exit /b 0

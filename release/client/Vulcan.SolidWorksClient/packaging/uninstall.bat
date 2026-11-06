@echo off
setlocal
cd /d "%~dp0"

net session >nul 2>&1
if errorlevel 1 (
  echo [Vulcan] Please run this script as Administrator.
  exit /b 1
)

tasklist /FI "IMAGENAME eq SLDWORKS.exe" 2>nul | find /I "SLDWORKS.exe" >nul
if not errorlevel 1 (
  echo [Vulcan] Please close SolidWorks before uninstalling Vulcan.
  exit /b 3
)

if not exist "Vulcan.SolidWorksClient.dll" (
  echo [Vulcan] Vulcan.SolidWorksClient.dll not found.
  exit /b 1
)

if not exist "RegAsm.exe" (
  echo [Vulcan] RegAsm.exe not found.
  exit /b 1
)

echo [Vulcan] Unregistering add-in...
RegAsm.exe "Vulcan.SolidWorksClient.dll" /u
if errorlevel 1 (
  echo [Vulcan] Unregistration failed.
  exit /b 1
)

echo [Vulcan] Add-in unregistered.

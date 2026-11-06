param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$InnoSetup = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$release = Join-Path $root "bin\$Platform\$Configuration"
$dist = Join-Path $root "dist"
$clientDll = Join-Path $release "Vulcan.SolidWorksClient.dll"

if (-not (Test-Path $clientDll)) {
    throw "Build $Configuration|$Platform first. Missing: $clientDll"
}

Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $dist | Out-Null

$payload = @(
    "Vulcan.SolidWorksClient.dll",
    "Vulcan.SolidWorksClient.tlb",
    "Newtonsoft.Json.dll",
    "solidworkstools.dll"
)

$interop = @(
    "SolidWorks.Interop.sldworks.dll",
    "SolidWorks.Interop.swconst.dll",
    "SolidWorks.Interop.swpublished.dll"
)

foreach ($file in $payload) {
    $source = Join-Path $release $file
    if (Test-Path $source) {
        Copy-Item $source $dist
    }
}

foreach ($file in $interop) {
    $source = Join-Path $root "ReferenceDLL\$file"
    if (-not (Test-Path $source)) {
        throw "Missing interop assembly: $source"
    }
    Copy-Item $source $dist
}

Copy-Item (Join-Path $root "packaging\install.bat") $dist
Copy-Item (Join-Path $root "packaging\uninstall.bat") $dist

$regAsm = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
if (-not (Test-Path $regAsm)) {
    throw "64-bit RegAsm.exe not found at $regAsm"
}
Copy-Item $regAsm $dist

Write-Host "Payload staged in $dist"

if (Test-Path $InnoSetup) {
    & $InnoSetup (Join-Path $root "Vulcan Setup\Vulcan setup.iss")
    Write-Host "Installer written to $(Join-Path $root 'Vulcan Setup\Output')"
} else {
    Write-Host "Inno Setup 6 not found. Payload is ready in $dist."
}

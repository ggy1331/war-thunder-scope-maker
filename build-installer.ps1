param([string]$Version = '0.8.1')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory "dist-$Version"
$payload = Join-Path $PSScriptRoot "War-Thunder-Scope-Maker-$Version-prealpha.zip"
Compress-Archive -Path (Join-Path $PSScriptRoot "dist-$Version\*") -DestinationPath $payload -Force
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ("/resource:" + $payload + ',payload.zip') ("/win32icon:" + (Join-Path $PSScriptRoot 'assets\branding\scope-maker.ico')) ("/out:" + (Join-Path $PSScriptRoot "WT-Scope-Maker-Setup-$Version.exe")) (Join-Path $PSScriptRoot 'installer\Setup.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }

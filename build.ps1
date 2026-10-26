param([string]$OutputDirectory = 'dist-0.8.1')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $root $OutputDirectory
New-Item -ItemType Directory -Force -Path $output | Out-Null
$iconArgs = @()
$icon = Join-Path $root 'assets\branding\scope-maker.ico'
if (Test-Path -LiteralPath $icon) { $iconArgs = @('/win32icon:' + $icon) }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler /nologo /target:winexe /optimize+ /utf8output /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll ('/out:' + (Join-Path $output 'War Thunder Scope Maker.exe')) $iconArgs $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $output 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $root 'examples') -Destination $output -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'templates') -Destination $output -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination $output -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'assets') -Destination $output -Recurse -Force
Write-Host "Built $output\War Thunder Scope Maker.exe"

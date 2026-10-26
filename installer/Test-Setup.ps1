$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$target = Join-Path $root ('qa-ui\setup-test-' + [guid]::NewGuid().ToString('N'))
$setup = Join-Path $root 'WT-Scope-Maker-Setup-0.8.1.exe'
function Install-Test { $p = Start-Process $setup -ArgumentList '--test-install', ('"' + $target + '"') -WindowStyle Hidden -Wait -PassThru; return $p.ExitCode }
if ((Install-Test) -ne 0) { throw 'Fresh install failed' }
[IO.File]::WriteAllText((Join-Path $target 'settings.json'), 'preserve settings')
[IO.File]::WriteAllText((Join-Path $target 'user.wtsm'), 'preserve project')
if ((Install-Test) -ne 0) { throw 'Update failed' }
if ([IO.File]::ReadAllText((Join-Path $target 'settings.json')) -ne 'preserve settings') { throw 'Settings lost' }
if ([IO.File]::ReadAllText((Join-Path $target 'user.wtsm')) -ne 'preserve project') { throw 'Project lost' }
$exe = Join-Path $target 'War Thunder Scope Maker.exe'
$before = (Get-FileHash $exe).Hash
$lock = [IO.File]::Open($exe, 'Open', 'Read', 'None')
try { if ((Install-Test) -ne 1) { throw 'Locked update not rejected' } } finally { $lock.Dispose() }
if ((Get-FileHash $exe).Hash -ne $before) { throw 'Executable damaged' }
'PASS: fresh install, update, settings/project preservation, locked-file recovery'

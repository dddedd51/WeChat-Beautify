param([string]$Executable = '微信美化.exe')
$ErrorActionPreference = 'Stop'
$exe=Join-Path (Join-Path $PSScriptRoot 'dist') $Executable
$testDir=Join-Path $PSScriptRoot ('research\self-test-' + [guid]::NewGuid().ToString('N'))
$p=Start-Process -FilePath $exe -ArgumentList @('--self-test',('"'+$testDir+'"')) -WindowStyle Hidden -Wait -PassThru
if ($p.ExitCode -ne 0) { Get-Content (Join-Path $testDir 'self-test-error.txt') -ErrorAction SilentlyContinue; throw "Self-test failed in $testDir" }
Get-Content (Join-Path $testDir 'results.txt')
Write-Output "Preview: $testDir\controller-preview.png"

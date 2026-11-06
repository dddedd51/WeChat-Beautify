param([switch]$ControllerOnly, [string]$OutputName = '微信美化.exe', [string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
if ([IO.Path]::GetFileName($OutputName) -ne $OutputName -or !$OutputName.EndsWith('.exe')) { throw 'OutputName must be an EXE filename.' }
$root=$PSScriptRoot
$dist=if($OutputDirectory){[IO.Path]::GetFullPath($OutputDirectory)}else{Join-Path $root 'dist'}
$runtime=Join-Path $dist 'native-runtime'
$generated=Join-Path $root 'research/build'
New-Item -ItemType Directory -Force $dist,$runtime,$generated | Out-Null
if (!$ControllerOnly) {
    & (Join-Path $root 'native/independent/build-semantic.ps1') -BuildFolder independent-release
    if ($LASTEXITCODE -ne 0) { throw 'Independent engine build failed.' }
    foreach($name in @('qttestability.dll','beautify-engine.dll')) {
        Copy-Item -LiteralPath (Join-Path $root "research/independent-release/$name") -Destination $runtime -Force
    }
}
$entryHash=(Get-FileHash (Join-Path $runtime 'qttestability.dll')).Hash.ToLowerInvariant()
$engineHash=(Get-FileHash (Join-Path $runtime 'beautify-engine.dll')).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $generated 'RuntimeIdentity.cs'), "internal static class RuntimeIdentity { internal const string Hash = `"$entryHash`"; internal const string EngineHash = `"$engineHash`"; }")
$assemblyName=[IO.Path]::GetFileNameWithoutExtension($OutputName)
& dotnet build (Join-Path $root '微信美化.csproj') -c Release -o $dist "-p:AssemblyName=$assemblyName" --ignore-failed-sources --nologo
if ($LASTEXITCODE -ne 0) { throw 'Controller build failed.' }
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $dist '使用说明.md') -Force
Copy-Item -LiteralPath (Join-Path $root 'THIRD-PARTY.md') -Destination $dist -Force
$shortcut=(New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $dist '微信美化.lnk'))
$shortcut.TargetPath=Join-Path $dist $OutputName
$shortcut.WorkingDirectory=$dist
$shortcut.WindowStyle=1
$shortcut.IconLocation=(Join-Path $dist $OutputName)+',0'
$shortcut.Description='微信美化 · 青屿主题工作室'
$shortcut.Save()
Write-Output "Built independent engine + controller: $dist"

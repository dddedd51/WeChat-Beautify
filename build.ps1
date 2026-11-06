param([switch]$ControllerOnly, [string]$OutputName = '微信美化.exe')
$ErrorActionPreference = 'Stop'
if ([IO.Path]::GetFileName($OutputName) -ne $OutputName -or !$OutputName.EndsWith('.exe')) { throw 'OutputName must be an EXE filename.' }
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$runtime = Join-Path $dist 'native-runtime'
New-Item -ItemType Directory -Force $dist,$runtime | Out-Null
if (!$ControllerOnly) {
& node (Join-Path $root 'native\prepare-runtime.cjs')
if ($LASTEXITCODE -ne 0) { throw 'Native preparation failed.' }
$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$vs) { throw 'MSVC x64 build tools are required.' }
$msvc = Get-ChildItem (Join-Path $vs 'VC\Tools\MSVC') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$sdk = 'C:\Program Files (x86)\Windows Kits\10'
$sdkVersion = (Get-ChildItem (Join-Path $sdk 'Lib') -Directory | Sort-Object Name -Descending | Select-Object -First 1).Name
$env:INCLUDE = "$($msvc.FullName)\include;$sdk\Include\$sdkVersion\ucrt;$sdk\Include\$sdkVersion\shared;$sdk\Include\$sdkVersion\um"
$env:LIB = "$($msvc.FullName)\lib\x64;$sdk\Lib\$sdkVersion\ucrt\x64;$sdk\Lib\$sdkVersion\um\x64"
$cl = Join-Path $msvc.FullName 'bin\Hostx64\x64\cl.exe'
& $cl /nologo /std:c11 /O2 /MT /LD /D_CRT_SECURE_NO_WARNINGS "/Fo:$root\research\build\runtime.obj" (Join-Path $root 'research\build\runtime.c') /link "/OUT:$runtime\qttestability.dll" "/IMPLIB:$root\research\build\runtime.lib" user32.lib kernel32.lib dwmapi.lib bcrypt.lib
if ($LASTEXITCODE -ne 0) { throw "Native build failed: $LASTEXITCODE" }
}
$moduleHash = (Get-FileHash (Join-Path $runtime 'qttestability.dll')).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $root 'research\build\RuntimeIdentity.cs'), "internal static class RuntimeIdentity { internal const string Hash = `"$moduleHash`"; }")
$assemblyName = [IO.Path]::GetFileNameWithoutExtension($OutputName)
& dotnet build (Join-Path $root '微信美化.csproj') -c Release -o $dist "-p:AssemblyName=$assemblyName" --ignore-failed-sources --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $dist '使用说明.md') -Force
Copy-Item -LiteralPath (Join-Path $root 'THIRD-PARTY.md') -Destination $dist -Force
Write-Output "Built: $dist\$OutputName"
$hostExe = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
$shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $dist '微信美化.lnk'))
$shortcut.TargetPath = $hostExe
$shortcut.Arguments = '"' + (Join-Path $dist "$assemblyName.dll") + '"'
$shortcut.WorkingDirectory = $dist
$shortcut.WindowStyle = 7
$shortcut.IconLocation = (Join-Path $dist $OutputName) + ',0'
$shortcut.Description = '微信美化 · 主题工作室'
$shortcut.Save()

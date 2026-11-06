param([string]$BuildFolder='independent-probe')
$ErrorActionPreference='Stop'
if($BuildFolder -notmatch '^[a-zA-Z0-9_-]+$'){throw 'BuildFolder must be a plain directory name.'}
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output=Join-Path $root ('research\'+$BuildFolder)
New-Item -ItemType Directory -Force -Path $output | Out-Null
$vswhere='C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$vs=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$vs){throw 'Visual Studio C++ build tools are required.'}
$msvc=Get-ChildItem (Join-Path $vs 'VC\Tools\MSVC') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$sdk='C:\Program Files (x86)\Windows Kits\10'
$version=(Get-ChildItem (Join-Path $sdk 'Lib') -Directory | Sort-Object Name -Descending | Select-Object -First 1).Name
$env:INCLUDE="$($msvc.FullName)\include;$sdk\Include\$version\ucrt;$sdk\Include\$version\shared;$sdk\Include\$version\um;$sdk\Include\$version\winrt"
$env:LIB="$($msvc.FullName)\lib\x64;$sdk\Lib\$version\ucrt\x64;$sdk\Lib\$version\um\x64"
& (Join-Path $msvc.FullName 'bin\Hostx64\x64\cl.exe') /nologo /std:c11 /W4 /O2 /MT /LD /D_CRT_SECURE_NO_WARNINGS (Join-Path $PSScriptRoot 'engine-entry.c') "/Fo:$output\probe.obj" /link "/OUT:$output\qttestability.dll" "/IMPLIB:$output\probe.lib" user32.lib bcrypt.lib
if($LASTEXITCODE -ne 0){throw 'Probe compilation failed.'}
Write-Output "Built: $output\qttestability.dll"
& (Join-Path $msvc.FullName 'bin\Hostx64\x64\cl.exe') /nologo /std:c++17 /W4 /EHsc /O2 /MT /LD /D_CRT_SECURE_NO_WARNINGS (Join-Path $PSScriptRoot 'dxgi-probe.cpp') "/Fo:$output\dxgi-probe.obj" /link "/OUT:$output\dxgi-probe.dll" "/IMPLIB:$output\dxgi-probe.lib" user32.lib dxgi.lib d3d11.lib dcomp.lib windowscodecs.lib ole32.lib
if($LASTEXITCODE -ne 0){throw 'DXGI probe compilation failed.'}
& (Join-Path $msvc.FullName 'bin\Hostx64\x64\cl.exe') /nologo /std:c++17 /EHsc /W4 /O2 /MT /D_CRT_SECURE_NO_WARNINGS (Join-Path $PSScriptRoot 'test-compositor.cpp') "/Fo:$output\test.obj" "/Fe:$output\test-compositor.exe"
if($LASTEXITCODE -ne 0){throw 'Compositor test build failed.'}
& (Join-Path $output 'test-compositor.exe')
if($LASTEXITCODE -ne 0){throw 'Compositor tests failed.'}
& (Join-Path $msvc.FullName 'bin\Hostx64\x64\cl.exe') /nologo /std:c++17 /EHsc /W4 /O2 /MT /D_CRT_SECURE_NO_WARNINGS (Join-Path $PSScriptRoot 'test-presentation.cpp') "/Fo:$output\presentation-test.obj" "/Fe:$output\test-presentation.exe" /link user32.lib dxgi.lib d3d11.lib dcomp.lib ole32.lib
if($LASTEXITCODE -ne 0){throw 'Presentation test build failed.'}

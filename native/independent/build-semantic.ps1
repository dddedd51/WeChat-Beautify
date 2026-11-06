param([string]$BuildFolder='independent-semantic')
$ErrorActionPreference='Stop'
if($BuildFolder -notmatch '^[a-zA-Z0-9_-]+$'){throw 'Invalid build folder'}
$project=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output=Join-Path $project ('research\'+$BuildFolder)
New-Item -ItemType Directory -Force $output | Out-Null
$vs=& 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$vs){throw 'MSVC required'}
$msvc=Get-ChildItem (Join-Path $vs 'VC\Tools\MSVC') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$sdk='C:\Program Files (x86)\Windows Kits\10'
$version=(Get-ChildItem (Join-Path $sdk 'Lib') -Directory | Sort-Object Name -Descending | Select-Object -First 1).Name
$env:INCLUDE="$($msvc.FullName)\include;$sdk\Include\$version\ucrt;$sdk\Include\$version\shared;$sdk\Include\$version\um"
$env:LIB="$($msvc.FullName)\lib\x64;$sdk\Lib\$version\ucrt\x64;$sdk\Lib\$version\um\x64"
$compiler=Join-Path $msvc.FullName 'bin\Hostx64\x64\cl.exe'
& $compiler /nologo /std:c11 /W4 /O2 /MT /LD /D_CRT_SECURE_NO_WARNINGS /DSEMANTIC_ENGINE (Join-Path $PSScriptRoot 'engine-entry.c') "/Fo:$output\entry.obj" /link "/OUT:$output\qttestability.dll" "/IMPLIB:$output\entry.lib" bcrypt.lib
if($LASTEXITCODE -ne 0){throw 'Entry build failed'}
& $compiler /nologo /std:c++17 /W4 /EHsc /O2 /MT /LD /D_CRT_SECURE_NO_WARNINGS (Join-Path $PSScriptRoot 'semantic-engine.cpp') "/Fo:$output\semantic.obj" /link "/OUT:$output\beautify-engine.dll" "/IMPLIB:$output\semantic.lib" user32.lib
if($LASTEXITCODE -ne 0){throw 'Semantic adapter build failed'}
& $compiler /nologo /std:c++17 /W4 /EHsc /O2 /MT (Join-Path $PSScriptRoot 'test-semantic.cpp') "/Fo:$output\test.obj" "/Fe:$output\test-semantic.exe"
if($LASTEXITCODE -ne 0){throw 'Test build failed'}
& (Join-Path $output 'test-semantic.exe')
if($LASTEXITCODE -ne 0){throw 'Tests failed'}

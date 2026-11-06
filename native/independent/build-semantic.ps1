param([string]$BuildFolder='independent-semantic',[string]$AdapterId='weixin-4.1.15.56-x64',[switch]$SkipTests)
$ErrorActionPreference='Stop'
if($BuildFolder -notmatch '^[a-zA-Z0-9_-]+$'){throw 'Invalid build folder'}
if($AdapterId -notmatch '^weixin-[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+-x64$'){throw 'Invalid adapter ID'}
$adapter=Join-Path $PSScriptRoot "adapters/$AdapterId"
$manifest=Get-Content -LiteralPath (Join-Path $adapter 'manifest.json') -Raw | ConvertFrom-Json
if($manifest.id -ne $AdapterId -or $manifest.architecture -ne 'x64' -or $manifest.status -notin @('verified','experimental')){throw "Adapter is not buildable: $AdapterId"}
if($manifest.targetSha256 -notmatch '^[0-9a-f]{64}$'){throw 'Invalid target hash'}
foreach($header in @('target.generated.h','paint-targets.generated.h')){if(!(Test-Path -LiteralPath (Join-Path $adapter $header))){throw "Missing adapter header: $header"}}
$targetHeader=Get-Content -LiteralPath (Join-Path $adapter 'target.generated.h') -Raw
if($targetHeader -notmatch ('(?m)^#define BEAUTIFY_ADAPTER_ID "'+[regex]::Escape($AdapterId)+'"\s*$')){throw 'Adapter ID/header mismatch'}
$baseline=[version]$manifest.version
foreach($component in @(@('MAJOR',$baseline.Major),@('MINOR',$baseline.Minor),@('PATCH',$baseline.Build))){if($targetHeader -notmatch ('(?m)^#define BEAUTIFY_VERSION_'+$component[0]+' '+$component[1]+'\s*$')){throw 'Adapter version/header mismatch'}}
$hashMatch=[regex]::Match($targetHeader,'(?m)^#define BEAUTIFY_TARGET_HASH_BYTES \{([^}]+)\}')
$headerHash=([regex]::Matches($hashMatch.Groups[1].Value,'0x([0-9a-fA-F]{2})') | ForEach-Object {$_.Groups[1].Value}) -join ''
if($headerHash -cne $manifest.targetSha256){throw 'Adapter hash/header mismatch'}
$selection='/DBEAUTIFY_TARGET_HEADER="adapters/'+$AdapterId+'/target.generated.h"'
$paintSelection='/DBEAUTIFY_PAINT_HEADER="adapters/'+$AdapterId+'/paint-targets.generated.h"'
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
& $compiler /nologo /std:c11 /W4 /O2 /MT /LD /D_CRT_SECURE_NO_WARNINGS /DSEMANTIC_ENGINE $selection (Join-Path $PSScriptRoot 'engine-entry.c') "/Fo:$output\entry.obj" /link "/OUT:$output\qttestability.dll" "/IMPLIB:$output\entry.lib" bcrypt.lib version.lib
if($LASTEXITCODE -ne 0){throw 'Entry build failed'}
& $compiler /nologo /std:c++17 /W4 /EHsc /O2 /MT /LD /D_CRT_SECURE_NO_WARNINGS $selection $paintSelection (Join-Path $PSScriptRoot 'semantic-engine.cpp') "/Fo:$output\semantic.obj" /link "/OUT:$output\beautify-engine.dll" "/IMPLIB:$output\semantic.lib" user32.lib
if($LASTEXITCODE -ne 0){throw 'Semantic adapter build failed'}
& $compiler /nologo /std:c++17 /W4 /EHsc /O2 /MT $selection (Join-Path $PSScriptRoot 'test-semantic.cpp') "/Fo:$output\test.obj" "/Fe:$output\test-semantic.exe"
if($LASTEXITCODE -ne 0){throw 'Test build failed'}
if(!$SkipTests){
    & (Join-Path $output 'test-semantic.exe')
    if($LASTEXITCODE -ne 0){throw 'Tests failed'}
}
& $compiler /nologo /std:c++17 /W4 /EHsc /O2 /MT /D_CRT_SECURE_NO_WARNINGS $selection $paintSelection (Join-Path $PSScriptRoot 'test-preflight.cpp') "/Fo:$output\preflight-test.obj" "/Fe:$output\test-preflight.exe" /link user32.lib
if($LASTEXITCODE -ne 0){throw 'Preflight test build failed'}
if(!$SkipTests){
    & (Join-Path $output 'test-preflight.exe')
    if($LASTEXITCODE -ne 0){throw 'Preflight tests failed'}
}else{Write-Warning 'Native tests were compiled but not run (-SkipTests); this is not a validated release build.'}
Copy-Item -LiteralPath (Join-Path $adapter 'manifest.json') -Destination (Join-Path $output 'adapter.json') -Force

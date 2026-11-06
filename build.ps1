param([switch]$ControllerOnly, [string]$OutputName = '微信美化.exe', [string]$OutputDirectory = '', [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
if($SkipTests){Write-Warning 'Compile only: native tests will not run; this is not a validated release build.'}
if ([IO.Path]::GetFileName($OutputName) -ne $OutputName -or !$OutputName.EndsWith('.exe')) { throw 'OutputName must be an EXE filename.' }
$root=$PSScriptRoot
$dist=if($OutputDirectory){[IO.Path]::GetFullPath($OutputDirectory)}else{Join-Path $root 'dist'}
$runtime=Join-Path $dist 'native-runtime'
$generated=Join-Path $root 'research/build'
New-Item -ItemType Directory -Force $dist,$runtime,$generated | Out-Null
$profiles=@()
$manifests=@(Get-ChildItem -LiteralPath (Join-Path $root 'native/independent/adapters') -Directory | Sort-Object Name | ForEach-Object {Join-Path $_.FullName 'manifest.json'})
if(!$manifests.Count){throw 'No adapter manifests found.'}
$ids=@{}
foreach($file in $manifests){
    $profile=Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    if($profile.version -notmatch '^\d+\.\d+\.\d+\.\d+$' -or $profile.id -ne ('weixin-'+$profile.version+'-x64') -or $profile.id -ne (Split-Path (Split-Path $file -Parent) -Leaf) -or $profile.architecture -ne 'x64' -or $profile.targetSha256 -notmatch '^[0-9a-f]{64}$' -or $profile.status -notin @('verified','experimental','pending')){throw "Invalid adapter manifest: $file"}
    if($ids.ContainsKey($profile.id)){throw "Duplicate adapter: $($profile.id)"};$ids[$profile.id]=$true
    $entryHash='';$engineHash=''
    if($profile.status -ne 'pending'){
        $destination=Join-Path $runtime $profile.id
        New-Item -ItemType Directory -Force $destination | Out-Null
        if(!$ControllerOnly){
            $buildFolder='release-'+$profile.id.Replace('.','-')
            & (Join-Path $root 'native/independent/build-semantic.ps1') -BuildFolder $buildFolder -AdapterId $profile.id -SkipTests:$SkipTests
            if($LASTEXITCODE -ne 0){throw "Independent engine build failed: $($profile.id)"}
            foreach($name in @('qttestability.dll','beautify-engine.dll')){
                Copy-Item -LiteralPath (Join-Path $root "research/$buildFolder/$name") -Destination $destination -Force
            }
        }
        $entryHash=(Get-FileHash -LiteralPath (Join-Path $destination 'qttestability.dll')).Hash.ToLowerInvariant()
        $engineHash=(Get-FileHash -LiteralPath (Join-Path $destination 'beautify-engine.dll')).Hash.ToLowerInvariant()
    }
    # JSON string escaping is also valid for these C# string literals. It is
    # never interpolated into shell command text.
    $values=@($profile.id,$profile.version,$profile.targetSha256,$profile.status,[string]$profile.reason,$entryHash,$engineHash) | ForEach-Object {ConvertTo-Json -InputObject ([string]$_) -Compress}
    $profiles+=('new AdapterProfile('+($values -join ',')+')')
}
[IO.File]::WriteAllText((Join-Path $generated 'RuntimeIdentity.cs'), "internal static class RuntimeIdentity { internal static readonly System.Collections.Generic.IReadOnlyList<AdapterProfile> Profiles = System.Array.AsReadOnly(new AdapterProfile[] {`n"+($profiles -join ",`n")+"`n}); }")
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

param([string]$Version='0.6.0-preview.1',[string]$BuiltDirectory='',[string]$ValidationDirectory='')
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$'){throw 'Invalid version'}
$stage=if($BuiltDirectory){[IO.Path]::GetFullPath($BuiltDirectory)}else{Join-Path $PSScriptRoot ('research/package-'+[guid]::NewGuid().ToString('N'))}
if(!$BuiltDirectory){
    # Run tests from the extracted validation directory, not from a potentially
    # low-integrity development checkout. No security labels are changed.
    & (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $stage -SkipTests
    if($LASTEXITCODE -ne 0){throw 'Build failed'}
}
# Explicit allowlist: no data, personal images, diagnostics, shortcuts or old runtimes.
$files=@('微信美化.exe','微信美化.dll','微信美化.deps.json','微信美化.runtimeconfig.json','使用说明.md','THIRD-PARTY.md')
$adapterIds=@()
foreach($folder in (Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'native/independent/adapters') -Directory | Sort-Object Name)){
    $profile=Get-Content -LiteralPath (Join-Path $folder.FullName 'manifest.json') -Raw | ConvertFrom-Json
    if($profile.status -eq 'pending'){continue}
    $adapterIds+=$profile.id
    foreach($name in @('qttestability.dll','beautify-engine.dll')){$files+='native-runtime/'+$profile.id+'/'+$name}
}
$output=Join-Path $PSScriptRoot 'releases'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$zip=Join-Path $output ('WeChat-Beautify-'+$Version+'-win-x64.zip')
if(Test-Path -LiteralPath $zip){throw 'Release ZIP already exists; use a new version or inspect it before replacing.'}
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($file in $files){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $stage $file),$file,[IO.Compression.CompressionLevel]::Optimal) | Out-Null}}finally{$archive.Dispose()}
$validation=if($ValidationDirectory){[IO.Path]::GetFullPath($ValidationDirectory)}else{Join-Path ([IO.Path]::GetTempPath()) ('WeChat-Beautify-verify-'+[guid]::NewGuid().ToString('N'))}
if(Test-Path -LiteralPath $validation){throw 'Validation directory already exists; use a fresh directory.'}
[IO.Compression.ZipFile]::ExtractToDirectory($zip,$validation)
foreach($file in $files){if((Get-FileHash -LiteralPath (Join-Path $stage $file)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $validation $file)).Hash){throw "Extracted file mismatch: $file"}}
$checks=Join-Path $validation 'checks'
$test=Start-Process -FilePath (Join-Path $validation '微信美化.exe') -ArgumentList @('--self-test',('"'+$checks+'"')) -WindowStyle Hidden -Wait -PassThru
if($test.ExitCode -ne 0){throw "Self-test failed: $checks; do not publish ZIP"}
foreach($id in $adapterIds){
    $buildFolder='release-'+$id.Replace('.','-')
    foreach($name in @('qttestability.dll','beautify-engine.dll')){if((Get-FileHash -LiteralPath (Join-Path $stage "native-runtime/$id/$name")).Hash -ne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot "research/$buildFolder/$name")).Hash){throw "Native test build and bundle differ: $id"}}
    foreach($name in @('test-semantic.exe','test-preflight.exe')){
        $source=Join-Path $PSScriptRoot "research/$buildFolder/$name";$target=Join-Path $checks ($id+'-'+$name)
        [IO.File]::WriteAllBytes($target,[IO.File]::ReadAllBytes($source))
        $test=Start-Process -FilePath $target -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput ($target+'.txt')
        if($test.ExitCode -ne 0){throw "Native tests failed: $target; do not publish ZIP"}
        Get-Content -LiteralPath ($target+'.txt')
    }
}
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($zip+'.sha256',$hash+'  '+[IO.Path]::GetFileName($zip)+"`n",[Text.UTF8Encoding]::new($false))
Write-Output "Package: $zip"
Write-Output "SHA256: $hash"
Write-Output "Validation: $checks/results.txt"

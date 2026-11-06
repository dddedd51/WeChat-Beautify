param([string]$Version='0.5.0-preview.1')
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$'){throw 'Invalid version'}
$stage=Join-Path $PSScriptRoot ('research/package-'+[guid]::NewGuid().ToString('N'))
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $stage
if($LASTEXITCODE -ne 0){throw 'Build failed'}
$checks=Join-Path $stage 'checks'
$test=Start-Process -FilePath (Join-Path $stage '微信美化.exe') -ArgumentList @('--self-test',('"'+$checks+'"')) -WindowStyle Hidden -Wait -PassThru
if($test.ExitCode -ne 0){throw "Self-test failed: $checks"}
# Explicit allowlist: no data, personal images, diagnostics, shortcuts or old runtimes.
$files=@('微信美化.exe','微信美化.dll','微信美化.deps.json','微信美化.runtimeconfig.json','使用说明.md','THIRD-PARTY.md','native-runtime/qttestability.dll','native-runtime/beautify-engine.dll')
$output=Join-Path $PSScriptRoot 'releases'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$zip=Join-Path $output ('WeChat-Beautify-'+$Version+'-win-x64.zip')
if(Test-Path -LiteralPath $zip){throw 'Release ZIP already exists; use a new version or inspect it before replacing.'}
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($file in $files){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $stage $file),$file,[IO.Compression.CompressionLevel]::Optimal) | Out-Null}}finally{$archive.Dispose()}
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($zip+'.sha256',$hash+'  '+[IO.Path]::GetFileName($zip)+"`n",[Text.UTF8Encoding]::new($false))
Write-Output "Package: $zip"
Write-Output "SHA256: $hash"
Write-Output "Validation: $checks/results.txt"

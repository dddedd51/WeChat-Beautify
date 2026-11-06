# Fetch only the pinned build input; upstream source stays outside version control.
$ErrorActionPreference = 'Stop'
$commit = '37d8188502ed4365ddcde928c7b2e50fda5e7226'
$expectedHash = '4473abd121825524676ce6dcea7fb310bd6266541d10d4039380423ff7be3a06'
$sourceDir = Join-Path $PSScriptRoot "research\reference\Windows-WeChat-Theme-Studio-$commit\native"
$destination = Join-Path $sourceDir 'runtime.c'

if (Test-Path -LiteralPath $destination) {
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) {
        throw 'Existing upstream runtime.c does not match the pinned SHA-256; keep a backup and inspect it before fetching again.'
    }
    Write-Output 'Pinned upstream runtime.c is already present and verified.'
    return
}

New-Item -ItemType Directory -Force -Path $sourceDir | Out-Null
$temporary = Join-Path $sourceDir ('runtime-' + [guid]::NewGuid().ToString('N') + '.tmp')
try {
    $uri = "https://raw.githubusercontent.com/starfalll521/Windows-WeChat-Theme-Studio/$commit/native/runtime.c"
    Invoke-WebRequest -Uri $uri -OutFile $temporary -UseBasicParsing -TimeoutSec 60
    if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) {
        throw 'Downloaded upstream runtime.c failed SHA-256 verification.'
    }
    Move-Item -LiteralPath $temporary -Destination $destination
    Write-Output "Fetched and verified upstream runtime.c at commit $commit."
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
}

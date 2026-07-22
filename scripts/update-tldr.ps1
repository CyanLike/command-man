[CmdletBinding()]
param(
    [string]$Python = 'python',
    [string]$Git = 'git',
    [string]$Branch = 'main',
    [string]$CachePath = '.cache\tldr',
    [switch]$Offline
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$repositoryUrl = 'https://github.com/tldr-pages/tldr.git'
$upstreamRoot = if ([System.IO.Path]::IsPathRooted($CachePath)) {
    [System.IO.Path]::GetFullPath($CachePath)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $CachePath))
}
$indexPath = Join-Path $repositoryRoot 'src\CommandMan.Core\Data\tldr.json'
$sourcePath = Join-Path $repositoryRoot 'third_party\tldr\SOURCE.json'
$licensePath = Join-Path $repositoryRoot 'third_party\tldr\LICENSE.md'

New-Item -ItemType Directory -Path (Split-Path -Parent $upstreamRoot) -Force | Out-Null
$gitDirectory = Join-Path $upstreamRoot '.git'
if (Test-Path -LiteralPath $gitDirectory -PathType Container) {
    if ($Offline) {
        Write-Host 'Using the cached tldr-pages checkout without fetching.'
    }
    else {
        Write-Host "Updating cached tldr-pages checkout from '$Branch'..."
        & $Git -C $upstreamRoot fetch --depth 1 origin $Branch
        if ($LASTEXITCODE -ne 0) { throw 'TLDR repository fetch failed. Use -Offline to rebuild from the existing cache.' }

        & $Git -C $upstreamRoot checkout --detach FETCH_HEAD
        if ($LASTEXITCODE -ne 0) { throw 'TLDR cached checkout update failed.' }
    }
}
elseif (Test-Path -LiteralPath $upstreamRoot) {
    throw "TLDR cache path exists but is not a Git checkout: $upstreamRoot"
}
else {
    if ($Offline) {
        throw "Offline update requested, but the TLDR cache does not exist: $upstreamRoot"
    }

    Write-Host "Shallow-cloning tldr-pages '$Branch' with a sparse checkout..."
    & $Git clone --depth 1 --filter=blob:none --sparse --single-branch --branch $Branch `
        $repositoryUrl $upstreamRoot
    if ($LASTEXITCODE -ne 0) { throw 'TLDR repository clone failed.' }
}

& $Git -C $upstreamRoot sparse-checkout set pages pages.zh
if ($LASTEXITCODE -ne 0) { throw 'TLDR sparse checkout failed.' }

$commit = (& $Git -C $upstreamRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or -not $commit) { throw 'Unable to resolve the TLDR commit.' }

Copy-Item -LiteralPath (Join-Path $upstreamRoot 'LICENSE.md') -Destination $licensePath -Force

& $Python (Join-Path $repositoryRoot 'tools\import_tldr.py') `
    --english-root (Join-Path $upstreamRoot 'pages') `
    --chinese-root (Join-Path $upstreamRoot 'pages.zh') `
    --output $indexPath
if ($LASTEXITCODE -ne 0) { throw 'TLDR index generation failed.' }

$supportedDirectories = @('common', 'linux', 'windows')
$englishPageCount = @($supportedDirectories | ForEach-Object {
    Get-ChildItem -LiteralPath (Join-Path $upstreamRoot "pages\$_") -Filter '*.md' -File
}).Count
$chinesePageCount = @($supportedDirectories | ForEach-Object {
    Get-ChildItem -LiteralPath (Join-Path $upstreamRoot "pages.zh\$_") -Filter '*.md' -File
}).Count

$metadata = [ordered]@{
    name = 'tldr-pages'
    repository = $repositoryUrl
    updateMode = 'cached-shallow-sparse-clone'
    offline = [bool]$Offline
    branch = $Branch
    commit = $commit
    downloadedAt = [DateTimeOffset]::UtcNow.ToString('O')
    sources = @(
        [ordered]@{
            language = 'en'
            path = 'pages'
            supportedPageCount = $englishPageCount
        },
        [ordered]@{
            language = 'zh'
            path = 'pages.zh'
            supportedPageCount = $chinesePageCount
        }
    )
    generatedIndex = '../../src/CommandMan.Core/Data/tldr.json'
    license = 'CC BY 4.0'
}
$metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $sourcePath -Encoding utf8
Write-Host "Updated offline TLDR index from commit $commit"
Write-Host "English pages: $englishPageCount; Chinese overlays: $chinesePageCount"
Write-Host "Cached checkout: $upstreamRoot"
Write-Host "Index: $indexPath"

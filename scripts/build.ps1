[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Architecture = 'x64',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$DotNet = 'dotnet',

    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot "$Architecture\CommandMan"))
$archivePath = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot "CommandMan-$Architecture.zip"))
$pluginProject = Join-Path $repositoryRoot 'src\Community.PowerToys.Run.Plugin.CommandMan\Community.PowerToys.Run.Plugin.CommandMan.csproj'
$buildOutput = Join-Path $repositoryRoot "src\Community.PowerToys.Run.Plugin.CommandMan\bin\$Architecture\$Configuration\net10.0-windows10.0.22621.0"

if (-not $publishDirectory.StartsWith($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Publish directory escaped the artifacts directory: $publishDirectory"
}

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

& $DotNet restore (Join-Path $repositoryRoot 'CommandMan.sln') -p:Platform=$Architecture
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

if (-not $SkipTests) {
    $hostArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    if ($Architecture -eq 'x64' -or $hostArchitecture -eq 'Arm64') {
        & $DotNet test (Join-Path $repositoryRoot 'tests\CommandMan.Tests\CommandMan.Tests.csproj') `
            --configuration $Configuration `
            --no-restore `
            -p:Platform=$Architecture
        if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }
    }
    else {
        & $DotNet build (Join-Path $repositoryRoot 'tests\CommandMan.Tests\CommandMan.Tests.csproj') `
            --configuration $Configuration `
            --no-restore `
            -p:Platform=$Architecture
        if ($LASTEXITCODE -ne 0) { throw 'ARM64 test compilation failed.' }
        Write-Host 'ARM64 tests were compiled but not executed on this x64 host.'
    }
}

& $DotNet build $pluginProject `
    --configuration $Configuration `
    --no-restore `
    -p:Platform=$Architecture
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

$packageFiles = @(
    'CommandMan.Core.dll',
    'CommandMan.Core.pdb',
    'Community.PowerToys.Run.Plugin.CommandMan.deps.json',
    'Community.PowerToys.Run.Plugin.CommandMan.dll',
    'Community.PowerToys.Run.Plugin.CommandMan.pdb',
    'plugin.json',
    'THIRD_PARTY_NOTICES.md'
)

foreach ($fileName in $packageFiles) {
    $sourceFile = Join-Path $buildOutput $fileName
    if (-not (Test-Path -LiteralPath $sourceFile)) {
        throw "Required package file is missing: $sourceFile"
    }

    Copy-Item -LiteralPath $sourceFile -Destination $publishDirectory
}

Copy-Item -LiteralPath (Join-Path $buildOutput 'Images') -Destination $publishDirectory -Recurse
Copy-Item -LiteralPath (Join-Path $buildOutput 'Licenses') -Destination $publishDirectory -Recurse

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

Compress-Archive -Path $publishDirectory -DestinationPath $archivePath -CompressionLevel Optimal

Write-Host "Plugin directory: $publishDirectory"
Write-Host "Plugin archive:   $archivePath"

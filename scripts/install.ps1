[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Architecture = 'x64',

    [string]$SourceDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($SourceDirectory)) {
    $SourceDirectory = Join-Path $repositoryRoot "artifacts\$Architecture\CommandMan"
}

$source = [System.IO.Path]::GetFullPath($SourceDirectory)
if (-not (Test-Path -LiteralPath (Join-Path $source 'plugin.json'))) {
    throw "No built plugin was found at '$source'. Run scripts\build.ps1 first."
}

$pluginsRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys\PowerToys Run\Plugins'))
$target = [System.IO.Path]::GetFullPath((Join-Path $pluginsRoot 'CommandMan'))
if (-not $target.StartsWith($pluginsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Installation target escaped the PowerToys Run plugin directory: $target"
}

New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force

Write-Host "Command Man installed at: $target"
Write-Host 'Restart PowerToys, then open PowerToys Run and type: cmd git commit'

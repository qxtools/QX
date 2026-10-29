#requires -Version 7.3
param(
    [string]$output = (Join-Path $PSScriptRoot '../artifacts/framework'),
    [string]$version
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& (Join-Path $PSScriptRoot 'check-framework.ps1')
$output = [IO.Path]::GetFullPath($output)
$configured_version = & dotnet msbuild (Join-Path $root 'src/QX.Hosting/QX.Hosting.csproj') -getProperty:QxFrameworkVersion -verbosity:quiet
if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the framework version.' }
$configured_version = $configured_version.Trim()
if (!$version) { $version = $configured_version }
if ($version -notmatch '^(0\.1\.(?:0|[1-9][0-9]*))(-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') { throw 'Version must belong to the 0.1 series.' }
$build_version = $Matches[1]
$solution = Join-Path $root 'QX.Framework.slnx'
$projects = ([xml][IO.File]::ReadAllText($solution)).SelectNodes('//Project')
foreach ($project in $projects) {
    $name = [IO.Path]::GetFileNameWithoutExtension($project.Path)
    if ($name -eq 'Flazzy') { $name = 'QX.Flazzy' }
    if (Test-Path -LiteralPath (Join-Path $output "$name.$version.nupkg")) {
        throw "Package $name $version already exists. Increment QxFrameworkVersion before replacing a framework release."
    }
}
& dotnet restore $solution --locked-mode --disable-build-servers
if ($LASTEXITCODE -ne 0) { throw 'Framework restore failed.' }
& dotnet pack $solution -c Release --no-restore --disable-build-servers -o $output "-p:Version=$version" "-p:PackageVersion=$version" "-p:QxBuildVersion=$build_version"
if ($LASTEXITCODE -ne 0) { throw 'Framework packaging failed.' }
if (@(Get-ChildItem -LiteralPath $output -Filter "*.$version.nupkg").Count -ne $projects.Count) {
    throw 'The framework package set is incomplete.'
}
Write-Output $output

#requires -Version 7.3
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$framework = ([xml][IO.File]::ReadAllText((Join-Path $root 'QX.Framework.slnx'))).SelectNodes('//Project')
$projects = @{}
foreach ($project in $framework) {
    $path = [IO.Path]::GetFullPath((Join-Path $root $project.Path))
    $projects[$path] = [xml][IO.File]::ReadAllText($path)
}
$dependencies = @{}
$bundled = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($path in $projects.Keys) {
    foreach ($entry in $projects[$path].SelectNodes('//BundledProject')) {
        $target = [IO.Path]::GetFullPath((Join-Path (Split-Path $path) $entry.Include.Replace('\', '/')))
        if (!$projects.ContainsKey($target)) { throw "Unknown bundled project: $target" }
        if (!$bundled.Add($target)) { throw "Project belongs to multiple packages: $target" }
    }
}
foreach ($path in $projects.Keys) {
    if ($projects[$path].SelectNodes('//BundledProject').Count -eq 0 -and !$bundled.Contains($path)) {
        throw "Framework project is missing from the public packages: $path"
    }
}
foreach ($path in $projects.Keys) {
    $references = @()
    foreach ($reference in $projects[$path].SelectNodes('//ProjectReference')) {
        $target = [IO.Path]::GetFullPath((Join-Path (Split-Path $path) $reference.Include.Replace('\', '/')))
        if (!$projects.ContainsKey($target)) { throw "Framework references application source: $path -> $target" }
        $references += $target
    }
    $dependencies[$path] = $references
    foreach ($file in Get-ChildItem -LiteralPath (Split-Path $path) -Filter '*.cs' -Recurse) {
        if ($file.FullName -match '[/\\](obj|bin)[/\\]') { continue }
        $content = [IO.File]::ReadAllText($file.FullName)
        if ($content -match '(?m)^namespace\s+Qx\.(Messages|Headers\.Flash|Game\.(Application|Protocol|WiredSimulation))([.;\s])') {
            throw "Retired framework namespace: $($file.FullName)"
        }
        if ($content -match 'InternalsVisibleTo\("QX\.(Scripter|Presentation|Desktop|App)"' -or
            $content -match '(using|namespace)\s+Qx\.(Scripter|Presentation|Desktop|App)([.;\s])') {
            throw "Framework has an application coupling: $($file.FullName)"
        }
    }
}
function Get-Dependencies([string]$path, [string[]]$parents = @()) {
    if ($path -in $parents) { throw "Framework dependency cycle: $($parents -join ' -> ') -> $path" }
    foreach ($target in $dependencies[$path]) {
        $target
        Get-Dependencies $target ($parents + $path)
    }
}
foreach ($path in $projects.Keys) {
    $closure = @(Get-Dependencies $path)
    $name = [IO.Path]::GetFileNameWithoutExtension($path)
    $forbidden = switch ($name) {
        'QX.Hosting' { 'QX.Scripting', 'QX.Mcp', 'QX.Platform' }
        'QX.Game' { 'QX.Scripting', 'QX.Mcp', 'QX.Hosting', 'QX.Platform' }
        'QX.Mcp' { 'QX.Scripting', 'QX.Hosting', 'QX.Platform' }
        default { @() }
    }
    foreach ($target in $closure) {
        if ([IO.Path]::GetFileNameWithoutExtension($target) -in $forbidden) {
            throw "Invalid framework layering: $name depends on $target"
        }
    }
}
Write-Output 'Framework dependency boundaries verified.'

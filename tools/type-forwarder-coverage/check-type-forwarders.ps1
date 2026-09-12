param(
    [string]$Configuration = "Debug",
    [string]$Framework = "net10.0"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$targets = @(
    @{ Legacy = "ClassIsland"; Current = "ClassFabric" },
    @{ Legacy = "ClassIsland.Core"; Current = "ClassFabric.Core" },
    @{ Legacy = "ClassIsland.Shared"; Current = "ClassFabric.Shared" },
    @{ Legacy = "ClassIsland.Shared.IPC"; Current = "ClassFabric.Shared.IPC" },
    @{ Legacy = "ClassIsland.Platforms.Abstractions"; Current = "ClassFabric.Platforms.Abstractions" },
    @{ Legacy = "ClassIsland.PluginSdk"; Current = "ClassFabric.PluginSdk" }
)

$inspectorRoot = Join-Path $env:TEMP "classfabric-forwarder-inspector"
New-Item -ItemType Directory -Force $inspectorRoot | Out-Null

$inspectorProject = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
"@
[System.IO.File]::WriteAllText((Join-Path $inspectorRoot "Inspector.csproj"), $inspectorProject, (New-Object System.Text.UTF8Encoding $false))

$inspectorProgram = @'
using System.Runtime.Loader;

var assemblyPath = Path.GetFullPath(args[0]);
var assemblyDirectory = Path.GetDirectoryName(assemblyPath)!;
var probeDirectories = args.Skip(1)
    .Select(Path.GetFullPath)
    .Prepend(assemblyDirectory)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
var context = new AssemblyLoadContext("forwarder-inspector", true);
context.Resolving += (_, name) =>
{
    var dependency = probeDirectories
        .Select(directory => Path.Combine(directory, $"{name.Name}.dll"))
        .FirstOrDefault(File.Exists);
    return dependency is not null ? context.LoadFromAssemblyPath(dependency) : null;
};

var assembly = context.LoadFromAssemblyPath(assemblyPath);
foreach (var type in assembly.GetExportedTypes()
             .Where(type => !type.IsNested && type.Namespace != "CompiledAvaloniaXaml")
             .OrderBy(type => type.FullName, StringComparer.Ordinal))
{
    Console.WriteLine(type.FullName);
}
'@
[System.IO.File]::WriteAllText((Join-Path $inspectorRoot "Program.cs"), $inspectorProgram, (New-Object System.Text.UTF8Encoding $false))

dotnet build (Join-Path $inspectorRoot "Inspector.csproj") -c Release --nologo -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Failed to build type inspector (exit code $LASTEXITCODE)." }

$inspector = Join-Path $inspectorRoot "bin\Release\net10.0\Inspector.dll"
$dependencyDirectories = @(
    (Join-Path $repoRoot "build\bin\$Configuration"),
    (Join-Path $repoRoot "ClassFabric.Desktop\bin\$Configuration\net10.0-windows10.0.19041.0")
) | Where-Object { Test-Path $_ }
$allMissing = @()

foreach ($target in $targets) {
    $assemblyPath = Join-Path $repoRoot "$($target.Current)\bin\$Configuration\$Framework\$($target.Current).dll"
    $forwarderDirectory = Join-Path $repoRoot "ClassIslandCompatibility\$($target.Legacy)"

    if (-not (Test-Path $assemblyPath) -or -not (Test-Path $forwarderDirectory)) {
        throw "Implementation assembly or compatibility directory not found for $($target.Legacy)."
    }

    $publicTypes = @(& dotnet $inspector $assemblyPath $dependencyDirectories)
    if ($LASTEXITCODE -ne 0) {
        throw "Type inspection failed for $($target.Current) with exit code $LASTEXITCODE."
    }
    $publicTypes = $publicTypes |
        ForEach-Object {
            $name = if ($_.StartsWith("ClassFabric.")) { "ClassIsland" + $_.Substring("ClassFabric".Length) } else { $_ }
            if ($name -match '^(.*)`([0-9]+)$') {
                $name = $Matches[1] + '<' + (',' * ([int]$Matches[2] - 1)) + '>'
            }
            $name
        } |
        Sort-Object -Unique

    $declared = Get-ChildItem $forwarderDirectory -Filter "TypeForwarders*.cs" |
        Get-Content |
        ForEach-Object {
            if ($_ -match 'TypeForwardedTo\(typeof\(([^)]+)\)\)') { $Matches[1] }
        } |
        Sort-Object -Unique

    $missing = $publicTypes | Where-Object { $_ -notin $declared }
    if ($missing) {
        Write-Host "Missing forwarders for $($target.Legacy):"
        $missing | ForEach-Object { Write-Host "  $_" }
        $allMissing += $missing
    }
    else {
        Write-Host "$($target.Legacy): complete"
    }
}

if ($allMissing.Count -gt 0) { exit 1 }
exit 0

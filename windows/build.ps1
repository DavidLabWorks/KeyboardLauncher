param(
    [ValidateSet('ARM64', 'x64')][string]$Architecture = $(if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'ARM64' } else { 'x64' }),
    [switch]$Publish,
    [switch]$Run
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'KeyboardLauncher/KeyboardLauncher.csproj'
$checks = Join-Path $PSScriptRoot 'KeyboardLauncher.Checks/KeyboardLauncher.Checks.csproj'
dotnet run --project $checks --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
if ($Publish) {
    $output = Join-Path $PSScriptRoot "artifacts/$Architecture"
    dotnet publish $project --configuration Release "-p:Platform=$Architecture" --output $output
} else {
    dotnet build $project --configuration Debug "-p:Platform=$Architecture"
    $runtime = if ($Architecture -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }
    $output = Join-Path $PSScriptRoot "KeyboardLauncher/bin/$Architecture/Debug/net8.0-windows10.0.19041.0/$runtime"
}
if ($LASTEXITCODE -ne 0) { throw 'Windows build failed.' }
if ($Run) { Start-Process -FilePath (Join-Path $output 'KeyboardLauncher.exe') }

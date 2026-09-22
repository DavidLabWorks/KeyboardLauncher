param(
    [ValidateSet('ARM64', 'x64')][string]$Architecture = 'ARM64',
    [string]$OutputDirectory = 'D:\KeyboardLauncher-Release',
    [string]$Compiler = 'C:\Program Files (x86)\NSIS\makensis.exe'
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $Compiler)) { throw 'NSIS is required to build the installer.' }
$project = Join-Path $PSScriptRoot 'KeyboardLauncher\KeyboardLauncher.csproj'
$config = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../app.json') -Encoding UTF8 -Raw | ConvertFrom-Json
$version = $config.version
if ([string]::IsNullOrWhiteSpace($config.name) -or $config.name -match '["$\r\n]') { throw 'Invalid application name for installer.' }
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must have three numeric components.' }
$appDirectory = Join-Path $OutputDirectory "$Architecture\app"
$artifacts = Join-Path $env:LOCALAPPDATA "Temp\KeyboardLauncher-release-build\$Architecture"
dotnet publish $project -c Release "-p:Platform=$Architecture" --artifacts-path $artifacts --output $appDirectory --nologo
if ($LASTEXITCODE -ne 0) { throw 'Release publish failed.' }
$appDirectory = (Resolve-Path $appDirectory).ProviderPath
$manifest = Join-Path $OutputDirectory 'uninstall-files.nsh'
$lines = @()
Get-ChildItem -LiteralPath $appDirectory -File -Recurse | Where-Object Extension -ne '.pdb' | ForEach-Object {
    $relative = $_.FullName.Substring($appDirectory.Length + 1).Replace('$', '$$')
    $lines += 'Delete "$INSTDIR\' + $relative + '"'
}
Get-ChildItem -LiteralPath $appDirectory -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
    $relative = $_.FullName.Substring($appDirectory.Length + 1).Replace('$', '$$')
    $lines += 'RMDir "$INSTDIR\' + $relative + '"'
}
[IO.File]::WriteAllLines($manifest, $lines)
$outputFile = Join-Path $OutputDirectory "KeyboardLauncher-$version-win-$($Architecture.ToLower())-Setup.exe"
& $Compiler /V2 "/DAPP_NAME=$($config.name)" "/DAPP_VERSION=$version" "/DAPP_ARCH=$Architecture" "/DAPP_DIR=$appDirectory" "/DOUTPUT_FILE=$outputFile" "/DUNINSTALL_FILES=$manifest" (Join-Path $PSScriptRoot 'installer\KeyboardLauncher.nsi')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$hash = (Get-FileHash -LiteralPath $outputFile -Algorithm SHA256).Hash
[IO.File]::WriteAllText($outputFile + '.sha256', "$hash  $([IO.Path]::GetFileName($outputFile))`r`n")
Get-Item -LiteralPath $outputFile | Select-Object FullName, Length

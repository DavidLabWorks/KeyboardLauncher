param([Parameter(Mandatory)][string]$OutputFile)
$ErrorActionPreference = 'Stop'
$config = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../app.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($config.name) -or $config.version -notmatch '^\d+\.\d+\.\d+$') {
    throw 'app.json requires a nonempty name and a three-part numeric version.'
}
$xml = New-Object System.Xml.XmlDocument
$root = $xml.CreateElement('app')
[void]$xml.AppendChild($root)
foreach ($key in @('name', 'version')) {
    $element = $xml.CreateElement($key)
    $element.InnerText = $config.$key
    [void]$root.AppendChild($element)
}
$xml.Save([IO.Path]::GetFullPath($OutputFile))

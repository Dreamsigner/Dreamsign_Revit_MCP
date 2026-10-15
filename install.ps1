$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dll = Join-Path $root 'bin\AstraRevitBridge.v5.dll'
if (!(Test-Path -LiteralPath $dll)) { throw 'Build first' }
$dir = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2027'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$manifest = Join-Path $dir 'AstraRevitBridge.addin'
$assembly = [Security.SecurityElement]::Escape($dll)
$xml = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns><AddIn Type="Application">
<Name>Astra Revit Local Bridge</Name><Assembly>$assembly</Assembly>
<AddInId>82C55A6F-5AB5-4A4B-A8E0-4C0E4E6B08C1</AddInId>
<FullClassName>AstraRevitBridge.App</FullClassName>
<VendorId>ASTR</VendorId><VendorDescription>Local validated Revit API bridge</VendorDescription>
</AddIn></RevitAddIns>
"@
if (Test-Path -LiteralPath $manifest) {
    if ([IO.File]::ReadAllText($manifest) -ne $xml) {
        [xml]$existing = Get-Content -LiteralPath $manifest
        if ($existing.RevitAddIns.AddIn.Count -gt 1 -or $existing.RevitAddIns.AddIn.AddInId.ToLower() -ne '82c55a6f-5ab5-4a4b-a8e0-4c0e4e6b08c1' -or $existing.RevitAddIns.AddIn.FullClassName -ne 'AstraRevitBridge.App') { throw 'Unrecognized add-in manifest; refusing replacement' }
        $manifestBackup = Join-Path $root 'backups\before-0.3\AstraRevitBridge.addin'
        if (!(Test-Path -LiteralPath $manifestBackup)) { New-Item -ItemType Directory -Force -Path (Split-Path $manifestBackup) | Out-Null; Copy-Item -LiteralPath $manifest -Destination $manifestBackup }
        [IO.File]::WriteAllText($manifest,$xml,[Text.UTF8Encoding]::new($false))
    }
} else { [IO.File]::WriteAllText($manifest, $xml, [Text.UTF8Encoding]::new($false)) }
Write-Output "Installed: $manifest"

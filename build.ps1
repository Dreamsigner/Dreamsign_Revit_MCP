param([switch]$AdapterOnly,[string]$RevitDirectory=(Join-Path $env:ProgramFiles 'Autodesk\Revit 2027'))
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$revit = $RevitDirectory
New-Item -ItemType Directory -Force -Path (Join-Path $root 'bin') | Out-Null
# Reuse the Roslyn compiler shipped with PowerShell; no SDK download or Autodesk changes.
Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')
$runtime = [System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
$paths = @((Get-ChildItem $runtime -Filter '*.dll').FullName) + @("$revit\RevitAPI.dll", "$revit\RevitAPIUI.dll")
$refs = [Microsoft.CodeAnalysis.MetadataReference[]]@($paths | ForEach-Object { try { $null = [Reflection.AssemblyName]::GetAssemblyName($_); [Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($_) } catch [BadImageFormatException] {} })
$tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText("$root\Bridge.cs"))
$opts = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary).WithOptimizationLevel([Microsoft.CodeAnalysis.OptimizationLevel]::Release)
if (!$AdapterOnly) {
& node (Join-Path $root 'generate-contract.mjs')
if ($LASTEXITCODE -ne 0) {throw 'Contract generation failed'}
$comp = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('AstraRevitBridgeV5', [Microsoft.CodeAnalysis.SyntaxTree[]]@($tree, [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText("$root\CoreFeatures.cs"))), $refs, $opts)
$stream = [IO.File]::Create("$root\bin\AstraRevitBridge.v5.dll")
try { $result = $comp.Emit($stream); $result.Diagnostics | Where-Object Severity -eq Error | ForEach-Object { Write-Output $_.ToString() }; if (!$result.Success) { throw 'Compilation failed' } } finally { $stream.Dispose() }
Get-Item "$root\bin\AstraRevitBridge.v5.dll" | Select-Object FullName,Length
}
$adapterTree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText("$root\BimAdapter.cs"))
$adapter = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('AstraBimAdapter', [Microsoft.CodeAnalysis.SyntaxTree[]]@($adapterTree), $refs, $opts)
$stream = [IO.File]::Create("$root\bin\AstraBimAdapter.dll")
try { $result=$adapter.Emit($stream); $result.Diagnostics | Where-Object Severity -eq Error | ForEach-Object { Write-Output $_.ToString() }; if(!$result.Success){throw 'Adapter compilation failed'} } finally { $stream.Dispose() }

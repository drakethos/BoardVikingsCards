# ServerSync was compiled when ZRoutedRpc.Everybody was a static field; in Valheim 1.0 it is `const long Everybody = 0`.
# Its `ldsfld ZRoutedRpc::Everybody` then throws MissingFieldException ("Using static instructions with literal field")
# the moment a synced setting changes, so live config changes never reach clients. This rewrites each such load to the
# constant (`ldc.i8 0`), which is what a recompile against Valheim 1.0 produces. Idempotent.
#   powershell -File build\scripts\Patch-ServerSyncEverybody.ps1 -Dll libs\ServerSync.dll -Cecil <Mono.Cecil.dll>
param(
    [Parameter(Mandatory = $true)][string]$Dll,
    [Parameter(Mandatory = $true)][string]$Cecil
)
$ErrorActionPreference = 'Stop'
Add-Type -Path $Cecil

$full = (Resolve-Path $Dll).Path
$bytes = [System.IO.File]::ReadAllBytes($full)
$module = [Mono.Cecil.ModuleDefinition]::ReadModule((New-Object System.IO.MemoryStream(, $bytes)))
$patched = 0
foreach ($type in $module.GetTypes()) {
    foreach ($method in $type.Methods) {
        if (-not $method.HasBody) { continue }
        foreach ($ins in $method.Body.Instructions) {
            if ($ins.OpCode -eq [Mono.Cecil.Cil.OpCodes]::Ldsfld -and $ins.Operand -is [Mono.Cecil.FieldReference] `
                    -and $ins.Operand.Name -eq 'Everybody' -and $ins.Operand.DeclaringType.Name -eq 'ZRoutedRpc') {
                $ins.OpCode = [Mono.Cecil.Cil.OpCodes]::Ldc_I8
                $ins.Operand = [long]0
                $patched++
                Write-Host "patched $($type.FullName)::$($method.Name)"
            }
        }
    }
}
if ($patched -gt 0) {
    $module.Write($full)
}
Write-Host "PATCHED_EVERYBODY=$patched"

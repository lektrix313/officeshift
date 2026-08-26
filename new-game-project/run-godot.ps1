# run-godot.ps1 — detached Godot invocation for this box (never pipe stdout directly).
# Usage: powershell -File run-godot.ps1 -Log g1.log -Args "--headless --script scenes/BuildWorld.cs"
param(
    [Parameter(Mandatory=$true)][string]$Log,
    [Parameter(Mandatory=$true)][string]$Args,
    [int]$TimeoutSec = 180
)
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
$p = Start-Process -FilePath "godot" -ArgumentList $Args.Split(' ') -WorkingDirectory (Get-Location) -PassThru -WindowStyle Hidden
if ($p.WaitForExit($TimeoutSec * 1000)) {
    exit $p.ExitCode
} else {
    $p.Kill()
    Write-Error "godot timed out after ${TimeoutSec}s"
    exit -999
}

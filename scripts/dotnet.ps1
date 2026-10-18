# Uses the workspace-local SDK when present; otherwise uses the installed .NET SDK.
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $workspaceRoot '.tools\dotnet\dotnet.exe'
if (Test-Path -LiteralPath $localDotnet) {
    $env:DOTNET_ROOT = Split-Path -Parent $localDotnet
    $env:DOTNET_CLI_HOME = Join-Path $workspaceRoot '.cli'
    $env:NUGET_PACKAGES = Join-Path $workspaceRoot '.tools\packages'
    & $localDotnet @args
} else {
    & dotnet @args
}
exit $LASTEXITCODE

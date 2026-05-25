$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
dotnet build (Join-Path $root 'Swai Key.csproj') -c Release

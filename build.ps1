$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
if (!(Test-Path $msbuild)) {
  $msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe'
}
if (!(Test-Path $msbuild)) {
  throw 'MSBuild not found.'
}

& $msbuild (Join-Path $root 'Swai Key.csproj') /nologo /t:Restore,Build /p:Configuration=Release /p:Platform=AnyCPU
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}

$exe = Join-Path $root 'publish\Swai Key.exe'
Write-Host "Built: $exe"

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $repo "WordleItaliano"

$env:TEMP = Join-Path $repo ".tmp"
$env:TMP = $env:TEMP
$env:APPDATA = Join-Path $repo ".appdata"
$env:LOCALAPPDATA = Join-Path $repo ".localappdata"
$env:DOTNET_CLI_HOME = Join-Path $repo ".dotnet"
$env:NUGET_PACKAGES = Join-Path $repo ".nuget\packages"
New-Item -ItemType Directory -Force -Path $env:TEMP,$env:APPDATA,$env:LOCALAPPDATA,$env:DOTNET_CLI_HOME,$env:NUGET_PACKAGES | Out-Null

Push-Location $project
try {
    dotnet restore --configfile .\NuGet.Config -r win-x64
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet publish -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=false -o .\publish\win-x64
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}

Write-Host "Pubblicato in: $project\publish\win-x64"

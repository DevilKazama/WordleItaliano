param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "WordleItaliano\WordleItaliano.csproj"
$nugetConfig = Join-Path $root "WordleItaliano\NuGet.Config"
$publishDir = Join-Path $root "WordleItaliano\publish\velopack-win-x64"
$releaseDir = Join-Path $root "WordleItaliano\releases"
$packId = "WordleItalianoApp"

$env:TEMP = Join-Path $root ".tmp"
$env:TMP = $env:TEMP
$env:APPDATA = Join-Path $root ".appdata"
$env:LOCALAPPDATA = Join-Path $root ".localappdata"
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet"
$sharedNugetPackages = Join-Path (Split-Path -Parent $root) ".nuget\packages"
$env:NUGET_PACKAGES = if (Test-Path $sharedNugetPackages) { $sharedNugetPackages } else { Join-Path $root ".nuget\packages" }
New-Item -ItemType Directory -Force -Path $env:TEMP,$env:APPDATA,$env:LOCALAPPDATA,$env:DOTNET_CLI_HOME,$env:NUGET_PACKAGES | Out-Null

[xml]$projectXml = Get-Content $project
$version = $projectXml.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Versione non trovata nel csproj."
}

function Remove-OutputDirectory([string]$path) {
    $fullRoot = [System.IO.Path]::GetFullPath($root)
    $fullPath = [System.IO.Path]::GetFullPath($path)
    if (-not $fullPath.StartsWith($fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Percorso output non valido: $fullPath"
    }

    Remove-Item -Recurse -Force $fullPath -ErrorAction SilentlyContinue
}

Remove-OutputDirectory $publishDir
Remove-OutputDirectory $releaseDir
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

dotnet tool restore --configfile $nugetConfig --ignore-failed-sources
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet restore $project --configfile $nugetConfig -r win-x64 --ignore-failed-sources
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    --no-restore `
    -o $publishDir `
    /p:PublishSingleFile=false `
    /p:PublishReadyToRun=false
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet vpk pack `
    --packId $packId `
    --packVersion $version `
    --packDir $publishDir `
    --mainExe WordleItaliano.exe `
    --outputDir $releaseDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Pacchetti Velopack creati in: $releaseDir"
Write-Host "Carica questi file nella GitHub Release v${version}:"
Get-ChildItem $releaseDir | Sort-Object Name | Select-Object Name, Length

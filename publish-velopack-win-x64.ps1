param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "WordleItaliano\WordleItaliano.csproj"
$publishDir = Join-Path $root "WordleItaliano\publish\velopack-win-x64"
$releaseDir = Join-Path $root "WordleItaliano\releases"
$packId = "WordleItalianoApp"

[xml]$projectXml = Get-Content $project
$version = $projectXml.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Versione non trovata nel csproj."
}

Remove-Item -Recurse -Force $publishDir -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force $releaseDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

dotnet tool restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
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

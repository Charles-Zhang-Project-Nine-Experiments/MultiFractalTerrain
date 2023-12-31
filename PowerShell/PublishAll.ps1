$PrevPath = Get-Location

Write-Host "Publish for Final Packaging build."
Set-Location $PSScriptRoot

$PublishFolder = "$PSScriptRoot\..\Publish"
$LibraryPublishFolder = "$PublishFolder\Libraries"
$NugetPublishFolder = "$PublishFolder\Nugets"

# Delete current data
Remove-Item $PublishFolder -Recurse -Force

# Publish Executables
$PublishExecutables = @(
)
foreach ($Item in $PublishExecutables)
{
    dotnet publish $PSScriptRoot\..\$Item --use-current-runtime --output $PublishFolder
}
# Publish Windows-only Executables
$PublishWindowsExecutables = @(
    "Samples\DefaultGeneration\DefaultGeneration.csproj"
)
foreach ($Item in $PublishWindowsExecutables)
{
    dotnet publish $PSScriptRoot\..\$Item --runtime win-x64 --self-contained --output $PublishFolder
}
# Publish Loose Libraries
$PublishLibraries = @(
    "Core\TerrainGenerator\TerrainGenerator.csproj"
)
foreach ($Item in $PublishLibraries)
{
    dotnet publish $PSScriptRoot\..\$Item --use-current-runtime --output $LibraryPublishFolder
}
# Publish Nugets
$PublishNugets = @(
    "Core\TerrainGenerator\TerrainGenerator.csproj"
)
foreach ($Item in $PublishNugets)
{
    dotnet pack $PSScriptRoot\..\$Item --output $NugetPublishFolder
}

# Create archive
$Date = Get-Date -Format yyyyMMdd
$ArchiveFolder = "$PublishFolder\..\Packages"
$ArchivePath = "$ArchiveFolder\MultiFractalTerrainGenerator_DistributionBuild_Windows_B$Date.zip"
New-Item -ItemType Directory -Force -Path $ArchiveFolder
Compress-Archive -Path $PublishFolder\* -DestinationPath $ArchivePath -Force

# Validation
if (-Not (Test-Path (Join-Path $PublishFolder "TerrainGenerator.dll")))
{
    Write-Host "Build failed."
    Exit
}

Set-Location $PrevPath
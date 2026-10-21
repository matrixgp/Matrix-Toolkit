param([ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$metadata = [xml](Get-Content (Join-Path $PSScriptRoot 'Directory.Build.props') -Raw)
$build = 'Build-{0:000}' -f [int]$metadata.Project.PropertyGroup.MatrixBuild
$output = Join-Path $PSScriptRoot "artifacts/$build/$Runtime"
dotnet publish (Join-Path $PSScriptRoot 'MatrixToolkit/MatrixToolkit.csproj') -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $output
if ($LASTEXITCODE -ne 0) { throw 'Sestavení aplikace selhalo.' }
Copy-Item (Join-Path $PSScriptRoot '../LICENSE') $output
Copy-Item (Join-Path $PSScriptRoot 'README.md') $output
Copy-Item (Join-Path $PSScriptRoot 'CHANGELOG.md') $output
Compress-Archive -Path "$output/*" -DestinationPath (Join-Path $PSScriptRoot "artifacts/MatrixToolkit-$build-$Runtime.zip") -Force
Write-Host "Hotovo: $output/MatrixToolkit.exe"

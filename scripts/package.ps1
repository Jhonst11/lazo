$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'build.ps1') -Release
if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar Lazo.' }
$dist = Join-Path $project 'dist'
$folder = Join-Path $dist 'Lazo-0.3.0'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
Copy-Item -LiteralPath (Join-Path $project 'bin\Lazo.exe') -Destination (Join-Path $folder 'Lazo.exe') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'enable-private-network.ps1') -Destination (Join-Path $folder 'enable-private-network.ps1') -Force
Copy-Item -LiteralPath (Join-Path $project 'README.md') -Destination (Join-Path $folder 'README.md') -Force
New-Item -ItemType Directory -Force -Path (Join-Path $folder 'docs') | Out-Null
Copy-Item -LiteralPath (Join-Path $project 'docs\minimal-preview.png') -Destination (Join-Path $folder 'docs\minimal-preview.png') -Force
Copy-Item -LiteralPath (Join-Path $project 'docs\vidrio.png') -Destination (Join-Path $folder 'docs\vidrio.png') -Force
$zip = Join-Path $dist 'Lazo-0.3.0.zip'
Compress-Archive -LiteralPath $folder -DestinationPath $zip -Force
Write-Host "Paquete: $zip"

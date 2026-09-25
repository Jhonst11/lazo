$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$refs = foreach ($name in @('PresentationCore','PresentationFramework','WindowsBase','System.Xaml')) {
    $dll = Get-ChildItem 'C:/Windows/Microsoft.NET/assembly' -Directory | ForEach-Object {
        Get-ChildItem $_.FullName -Directory -Filter $name
    } | ForEach-Object { Get-ChildItem $_.FullName -Recurse -Filter ($name + '.dll') } | Select-Object -First 1
    '/reference:' + $dll.FullName
}
$exe = Join-Path $project 'bin/VisualSmoke.exe'
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' /nologo /target:exe ('/out:' + $exe) $refs (Join-Path $project 'tests/VisualSmoke.cs')
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de la prueba visual.' }
& $exe (Join-Path $project 'bin/Lazo.exe') (Join-Path $project 'bin/visual-check')
if ($LASTEXITCODE -ne 0) { throw 'Falló la prueba visual.' }

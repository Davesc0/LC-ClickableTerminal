# Builds the release dll and puts the Thunderstore zip in dist\.
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$version = (Get-Content "$root\manifest.json" -Raw | ConvertFrom-Json).version_number

if (-not (Test-Path "$root\icon.png")) { throw "icon.png is missing (256x256)." }

dotnet build "$root\ClickableTerminal\ClickableTerminal.csproj" -c Release -nologo -v q
$dll = "$root\ClickableTerminal\bin\Release\netstandard2.1\ClickableTerminal.dll"

$stage = "$root\dist\stage"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item "$root\manifest.json", "$root\README.md", "$root\CHANGELOG.md", "$root\icon.png", $dll -Destination $stage

$zip = "$root\dist\ClickableTerminal-$version.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path "$stage\*" -DestinationPath $zip
Remove-Item $stage -Recurse -Force
Write-Host "Packed $zip"

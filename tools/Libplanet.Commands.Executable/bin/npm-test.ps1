#!/usr/bin/env pwsh
param (
  [Parameter(Mandatory, Position=0, HelpMessage="Enter a version to download.")]
  [ValidatePattern("^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$")]
  [string]
  $Version
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"
$DebugPreference = "Continue"

function New-TemporaryDirectory {
  New-TemporaryFile | ForEach-Object {
    Remove-Item $_
    New-Item -ItemType Directory -Path $_
  }
}

function Test-Npx(
  [Parameter(Mandatory, Position=0)][string[]]$Command,
  [Parameter(Mandatory, Position=1)][string]$Expected
) {
  if ($env:ACTIONS_RUNNER_DEBUG -eq "true") {
    $cmd = $Command -Join " "
    & "npx" @Command
    Write-Debug "The command 'npx $cmd' terminated with $?."
  }
  $actual = & "npx" @Command
  if ($actual -ne $Expected) {
    $cmd = $Command -Join " "
    Write-Error "The command 'npx $cmd' printed an unexpected output.
  Expected: $Expected
  Actual: $actual"
    exit 1
  }
}

function Test-Planet() {
  Test-Npx @("planet", "--version") "planet $Version"
}

if (-not (Get-Command yarn 2> $null)) {
  Write-Error "The yarn is unavailable."
  exit 1
} elseif (-not (Get-Command npm 2> $null)) {
  Write-Error "The npm is unavailable."
  exit 1
} elseif (-not (Get-Command npx 2> $null)) {
  Write-Error "The npx command is unavailable."
  exit 1
}

$PackageDir = Resolve-Path (Split-Path -Path $PSScriptRoot -Parent)

Copy-Item (Join-Path -Path $PackageDir -ChildPath "package.json") `
  (Join-Path -Path $PackageDir -ChildPath ".package.json.bak")
try {
  $Package = Get-Content "package.json" | ConvertFrom-Json
  $Package.private = $false
  $Package.version = $Version
  ConvertTo-Json -Depth 100 $Package | Set-Content "package.json"

  if (Test-Path package.tgz) {
    Remove-Item -Force package.tgz
  }
  yarn pack --install-if-needed
  $PackagePath = Join-Path `
    -Path $PackageDir `
    -ChildPath "package.tgz"

  Write-Information 'Test with "npm install"...'
  $tempDir = New-TemporaryDirectory
  Push-Location $tempDir
  Write-Debug "Enter a temporary directory: $($tempDir.FullName)"
  npm install --save $PackagePath
  Test-Planet
  Pop-Location

  Write-Information 'Test with "npm install --ignore-scripts"...'
  $tempDir = New-TemporaryDirectory
  Push-Location $tempDir
  Write-Debug "Enter a temporary directory: $($tempDir.FullName)"
  npm install --quiet --ignore-scripts --save $PackagePath
  Test-Planet
  Pop-Location

  Write-Output "Succeeded!"
} finally {
  Remove-Item (Join-Path -Path $PackageDir -ChildPath "package.json")
  Rename-Item (Join-Path -Path $PackageDir -ChildPath ".package.json.bak") `
    (Join-Path -Path $PackageDir -ChildPath "package.json")
  Pop-Location -PassThru
}

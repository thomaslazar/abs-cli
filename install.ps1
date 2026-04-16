$ErrorActionPreference = "Stop"

$Repo = "thomaslazar/abs-cli"
$InstallDir = if ($env:ABS_CLI_INSTALL_DIR) { $env:ABS_CLI_INSTALL_DIR } else { Join-Path $env:LOCALAPPDATA "abs-cli" }
$Version = $env:ABS_CLI_VERSION

# Detect architecture
$Arch = $env:PROCESSOR_ARCHITECTURE
switch ($Arch) {
    "AMD64" { $Rid = "win-x64" }
    "ARM64" { $Rid = "win-arm64" }
    default { Write-Error "Unsupported architecture: $Arch"; exit 1 }
}

# Resolve version
if (-not $Version) {
    $Release = Invoke-RestMethod "https://api.github.com/repos/$Repo/releases/latest"
    $Version = $Release.tag_name
}

Write-Host "Installing abs-cli $Version ($Rid)..."

# Download
$DownloadUrl = "https://github.com/$Repo/releases/download/$Version/abs-cli-${Rid}.exe"
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
$BinaryPath = Join-Path $InstallDir "abs-cli.exe"
Invoke-WebRequest -Uri $DownloadUrl -OutFile $BinaryPath -UseBasicParsing

# Add to user PATH if not already present
$UserPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($UserPath -notlike "*$InstallDir*") {
    [Environment]::SetEnvironmentVariable("Path", "$UserPath;$InstallDir", "User")
    $env:Path = "$env:Path;$InstallDir"
    Write-Host "Added $InstallDir to user PATH."
}

# Verify
& $BinaryPath --version
Write-Host "abs-cli installed to $BinaryPath"

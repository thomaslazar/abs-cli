# Fetches a release binary from GitHub Releases and verifies it against the
# release's SHA256SUMS before installing. Both come from the same release, so
# this catches a corrupt or truncated download, not a compromised release.

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
$Asset = "abs-cli-$Rid.exe"

# Resolve version
if (-not $Version) {
    $Release = Invoke-RestMethod "https://api.github.com/repos/$Repo/releases/latest"
    $Version = $Release.tag_name
}

Write-Host "Installing abs-cli $Version ($Rid)..."

$BaseUrl = "https://github.com/$Repo/releases/download/$Version"

# Download into a temp dir first, so a failed or corrupt download never
# replaces a working install.
$TmpDir = Join-Path ([System.IO.Path]::GetTempPath()) ("abs-cli-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $TmpDir | Out-Null
try {
    $TmpBinary = Join-Path $TmpDir $Asset
    Invoke-WebRequest -Uri "$BaseUrl/$Asset" -OutFile $TmpBinary -UseBasicParsing

    # Verify
    $SumsPath = Join-Path $TmpDir "SHA256SUMS"
    try {
        Invoke-WebRequest -Uri "$BaseUrl/SHA256SUMS" -OutFile $SumsPath -UseBasicParsing
    }
    catch {
        throw "Could not download SHA256SUMS for $Version. Releases before checksums were added have none; download from the release page instead."
    }
    $Expected = $null
    foreach ($Line in Get-Content -Path $SumsPath) {
        $Parts = $Line.Trim() -split '\s+', 2
        if ($Parts.Count -eq 2 -and $Parts[1].TrimStart('*') -eq $Asset) {
            $Expected = $Parts[0]
            break
        }
    }
    if (-not $Expected) {
        throw "SHA256SUMS has no entry for $Asset"
    }
    $Actual = (Get-FileHash -Algorithm SHA256 -Path $TmpBinary).Hash
    if ($Actual -ne $Expected) {
        throw "Checksum mismatch for $Asset`n  expected: $Expected`n  actual:   $Actual"
    }
    Write-Host "Checksum verified."

    # Install
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    $BinaryPath = Join-Path $InstallDir "abs-cli.exe"
    Move-Item -Path $TmpBinary -Destination $BinaryPath -Force
}
finally {
    Remove-Item -Path $TmpDir -Recurse -Force -ErrorAction SilentlyContinue
}

# Add to user PATH if not already present
$UserPath = [Environment]::GetEnvironmentVariable("Path", "User")
$PathEntries = if ($UserPath) { $UserPath -split ";" } else { @() }
if ($InstallDir -notin $PathEntries) {
    $NewPath = if ($UserPath) { "$UserPath;$InstallDir" } else { $InstallDir }
    [Environment]::SetEnvironmentVariable("Path", $NewPath, "User")
    $env:Path = "$env:Path;$InstallDir"
    Write-Host "Added $InstallDir to user PATH."
}

# Run it
& $BinaryPath --version
Write-Host "abs-cli installed to $BinaryPath"

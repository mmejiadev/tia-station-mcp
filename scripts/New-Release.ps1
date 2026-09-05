#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the server and packs a versioned release artefact with its SHA-256.

.DESCRIPTION
    Produces tia-station-mcp-<version>.zip: the built server, the precondition check, INSTALL.md and
    the example write policy. That is everything somebody needs to install this and nothing they do
    not - no sources, no tests, no project files.

    **Byte identity matters more here than it usually would.** TIA Portal binds its Openness
    whitelist to the exact executable, so a rebuild is a different program as far as TIA is
    concerned and costs every user a confirmation dialog. That is why releases are versioned and
    few, why the hash is published beside the artefact, and why this script refuses to build one
    from a working tree with uncommitted changes unless told to.

    It has to run on a machine with TIA Portal installed. The build resolves Siemens.Engineering
    from the installation, so there is no way to produce this artefact anywhere else - the same
    constraint that shapes the whole of phase 5b.

.PARAMETER OutputDirectory
    Where to write the artefact. Defaults to 'release' beside the repository.

.PARAMETER AllowDirtyTree
    Pack even though the working tree has uncommitted changes. For trying the script out; a release
    built this way cannot be tied to a commit.

.OUTPUTS
    The artefact path and its SHA-256.

.EXAMPLE
    .\New-Release.ps1
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory,
    [switch] $AllowDirtyTree
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repository = Split-Path -Parent $PSScriptRoot
$configuration = 'Release'
$framework = 'net48'

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repository 'release'
}

function Assert-CleanTree {
    if ($AllowDirtyTree) {
        Write-Warning 'Packing a working tree with uncommitted changes: this artefact cannot be tied to a commit.'

        return
    }

    $status = & git -C $repository status --porcelain

    if ($status) {
        throw ("The working tree has uncommitted changes, so the artefact could not be tied to a " +
               "commit. Commit them, or pass -AllowDirtyTree if you are only trying this out.")
    }
}

function Get-Commit {
    try {
        return (& git -C $repository rev-parse --short HEAD).Trim()
    } catch {
        return 'unknown'
    }
}

Assert-CleanTree

Write-Host 'Building the server...'

$project = Join-Path $repository 'src\TiaMcpServer\TiaMcpServer.csproj'

& dotnet build $project -c $configuration --nologo -v quiet

if ($LASTEXITCODE -ne 0) {
    throw 'The build failed, so there is nothing to pack.'
}

$binaries = Join-Path $repository "src\TiaMcpServer\bin\$configuration\$framework"
$executable = Join-Path $binaries 'TiaMcpServer.exe'

if (-not (Test-Path -LiteralPath $executable)) {
    throw "The build reported success but produced no $executable."
}

# The version comes off the assembly rather than out of the .csproj: what ships is what the binary
# says it is, and those two can differ if a build was skipped.
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable).FileVersion
$commit = Get-Commit
$name = "tia-station-mcp-$version"

Write-Host "Packing $name (commit $commit)..."

$staging = Join-Path ([IO.Path]::GetTempPath()) "tia-release-$([Guid]::NewGuid().ToString('N'))"
$root = Join-Path $staging $name

New-Item -ItemType Directory -Path $root -Force | Out-Null

try {
    # The server and everything it loads. Openness itself is not here and must not be: it belongs
    # to the TIA Portal installation on the target machine and is resolved from it at run time.
    Copy-Item -Path (Join-Path $binaries '*') -Destination $root -Recurse

    # The XML documentation files describe an API for someone writing code against it. This ships an
    # executable, so they are weight with no reader. The .pdb files stay: they are what turns a
    # stack trace from a user's machine into something with line numbers in it.
    Get-ChildItem -Path $root -Filter '*.xml' -File | Remove-Item -Force

    # INSTALL.md tells the reader to run the check from the folder they unzipped, so it ships.
    New-Item -ItemType Directory -Path (Join-Path $root 'scripts') -Force | Out-Null
    Copy-Item -Path (Join-Path $repository 'scripts\Test-Preconditions.ps1') -Destination (Join-Path $root 'scripts')

    Copy-Item -Path (Join-Path $repository 'INSTALL.md') -Destination $root
    Copy-Item -Path (Join-Path $repository 'LICENSE.txt') -Destination $root

    # Without a policy every write is refused, so the example travels with the artefact. It is NOT
    # copied to policy.json: that decision is the installer's, and shipping a working policy would
    # hand a fresh installation write access nobody chose.
    New-Item -ItemType Directory -Path (Join-Path $root '.tia-mcp') -Force | Out-Null
    Copy-Item -Path (Join-Path $repository '.tia-mcp\policy.example.json') -Destination (Join-Path $root '.tia-mcp')

    Set-Content -Path (Join-Path $root 'VERSION.txt') -Encoding utf8 -Value @(
        "tia-station-mcp $version"
        "commit $commit"
        "built $(Get-Date -Format 'o')"
        ''
        'TIA Portal V20 with Openness is required and is not included: it is resolved from the'
        'installation on the machine this runs on.'
    )

    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

    $archive = Join-Path $OutputDirectory "$name.zip"

    if (Test-Path -LiteralPath $archive) {
        Remove-Item -LiteralPath $archive -Force
    }

    Compress-Archive -Path $root -DestinationPath $archive

    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash

    Set-Content -Path "$archive.sha256" -Encoding utf8 -Value "$hash  $name.zip"

    Write-Host ''
    Write-Host "artefact : $archive"
    Write-Host "sha256   : $hash"
    Write-Host "size     : $([Math]::Round((Get-Item -LiteralPath $archive).Length / 1MB, 1)) MB"
    Write-Host ''
    Write-Host 'Publish the hash beside the artefact. Whoever installs it should compare before running it.'
} finally {
    Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
}

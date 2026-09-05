#Requires -Version 5.1
<#
.SYNOPSIS
    Checks whether this machine can run tia-station-mcp, and says what to do about each thing it cannot.

.DESCRIPTION
    Every check answers one question and, when the answer is no, names the fix in a sentence. It
    reads the machine and changes nothing: no install, no group membership granted, no setting
    written. That is deliberate. A bootstrap that fixes things on its own decides for the person
    running it, and half of these need a decision - a licence, a sign-out, an installer.

    It is PowerShell because it has to run before anything else exists. Node, .NET and TIA Portal
    are what it checks for; it cannot be written in any of them.

    The dashboard's Guide view runs this same script with -Json rather than asking the same
    questions again in TypeScript. One implementation, two readers: a second copy would drift, and
    the copy that drifts is the one telling somebody their machine is ready when it is not.

.PARAMETER TiaPortalLocation
    Where TIA Portal V20 is installed. Defaults to the standard location.

.PARAMETER Json
    Emit the result as JSON instead of as text for a person.

.OUTPUTS
    Text, or JSON with -Json. Exit code 0 when every required check passed, 1 when any did not.

.EXAMPLE
    .\Test-Preconditions.ps1

.EXAMPLE
    .\Test-Preconditions.ps1 -Json
#>
[CmdletBinding()]
param(
    [string] $TiaPortalLocation = 'C:\Program Files\Siemens\Automation\Portal V20',
    [switch] $Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The group Openness requires. Windows grants its token at sign-in, which is why every message
# about it has to mention signing out - being added is not the same as having it.
$OpennessGroup = 'Siemens TIA Openness'

# The floor the harness declares. Below it, `node --test` cannot strip TypeScript on its own.
$MinimumNodeMajor = 22
$MinimumNodeMinor = 6

function New-Check {
    param(
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [bool] $Met,
        [Parameter(Mandatory)] [bool] $Required,
        [string] $Found = '',
        [string] $Fix = ''
    )

    [pscustomobject]@{
        Name     = $Name
        Met      = $Met
        Required = $Required
        Found    = $Found
        Fix      = $Fix
    }
}

function Test-TiaPortal {
    $openness = Join-Path $TiaPortalLocation 'PublicAPI\V20\Siemens.Engineering.dll'

    if (Test-Path -LiteralPath $openness) {
        return New-Check -Name 'TIA Portal V20 with Openness' -Met $true -Required $true -Found $TiaPortalLocation
    }

    New-Check -Name 'TIA Portal V20 with Openness' -Met $false -Required $true `
        -Found "no Siemens.Engineering.dll under $TiaPortalLocation" `
        -Fix ("Install TIA Portal V20 including the Openness option, or point this check at the " +
              "installation with -TiaPortalLocation. V21 will not do: this server targets V20 and " +
              "the API is versioned.")
}

function Test-OpennessGroup {
    # The check is on the *token this session holds*, not on the group's member list, because that
    # is what Openness actually tests. An account added an hour ago and not signed out since still
    # fails, and reading the member list would report success while the server refuses to start.
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $held = $false

    foreach ($group in $identity.Groups) {
        try {
            $name = $group.Translate([Security.Principal.NTAccount]).Value
        } catch {
            # A SID with no local name is not the group we are looking for.
            continue
        }

        if ($name -like "*\$OpennessGroup") {
            $held = $true
            break
        }
    }

    if ($held) {
        return New-Check -Name "Member of '$OpennessGroup'" -Met $true -Required $true -Found $identity.Name
    }

    New-Check -Name "Member of '$OpennessGroup'" -Met $false -Required $true `
        -Found "$($identity.Name) does not hold the group in this session" `
        -Fix ("Run as administrator: net localgroup `"$OpennessGroup`" `"%USERNAME%`" /add " +
              "- then sign out of Windows and back in. Windows only grants the group to a new " +
              "sign-in, so adding it is not enough on its own.")
}

function Test-DotNetFramework {
    $key = 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full'

    if (Test-Path -LiteralPath $key) {
        $release = (Get-ItemProperty -LiteralPath $key).Release

        # 528040 is .NET Framework 4.8. Documented by Microsoft as the value to compare against.
        if ($release -ge 528040) {
            return New-Check -Name '.NET Framework 4.8' -Met $true -Required $true -Found "release $release"
        }

        return New-Check -Name '.NET Framework 4.8' -Met $false -Required $true `
            -Found "release $release, which is older than 4.8" `
            -Fix 'Install the .NET Framework 4.8 runtime from Microsoft.'
    }

    New-Check -Name '.NET Framework 4.8' -Met $false -Required $true `
        -Found 'not installed' `
        -Fix 'Install the .NET Framework 4.8 runtime from Microsoft.'
}

function Test-Node {
    $node = Get-Command node -ErrorAction SilentlyContinue

    if ($null -eq $node) {
        return New-Check -Name "Node $MinimumNodeMajor.$MinimumNodeMinor or newer" -Met $false -Required $false `
            -Found 'not on PATH' `
            -Fix ('Install Node from nodejs.org. Only the harness, the dashboard and the ' +
                  'documentation lookup need it; the MCP server itself runs without it.')
    }

    $version = (& node --version) -replace '^v', ''
    $parts = $version.Split('.')
    $major = [int]$parts[0]
    $minor = [int]$parts[1]

    if ($major -gt $MinimumNodeMajor -or ($major -eq $MinimumNodeMajor -and $minor -ge $MinimumNodeMinor)) {
        return New-Check -Name "Node $MinimumNodeMajor.$MinimumNodeMinor or newer" -Met $true -Required $false -Found "v$version"
    }

    New-Check -Name "Node $MinimumNodeMajor.$MinimumNodeMinor or newer" -Met $false -Required $false `
        -Found "v$version" `
        -Fix ("Upgrade Node. Below $MinimumNodeMajor.$MinimumNodeMinor it cannot run the harness's " +
              'TypeScript without a build step.')
}

function Test-PlcSim {
    # PLCSIM_API_PATH is how the build already finds this - see TiaMcpServer.csproj. Honouring
    # the same variable is the difference between a check and a check that lies: a machine with
    # PLCSIM installed elsewhere would be told it has none, having configured it exactly as the
    # project asks.
    $api = $env:PLCSIM_API_PATH

    if (-not $api) {
        $api = Join-Path $env:ProgramW6432 'Siemens\Automation\PLCSIM_V20\resources\bin\wwwroot\assets\lib\runtime\Siemens.Simatic.Simulation.Runtime.Api.x64.dll'
    }

    if (Test-Path -LiteralPath $api) {
        return New-Check -Name 'PLCSIM Advanced V20' -Met $true -Required $false -Found $api
    }

    New-Check -Name 'PLCSIM Advanced V20' -Met $false -Required $false `
        -Found "no runtime API at $api" `
        -Fix ('Install PLCSIM Advanced V20 with its own licence, or set PLCSIM_API_PATH to where ' +
              'its runtime API is. Without it the server still compiles and exports, but every ' +
              'simulation tool reports the runtime as unavailable and nothing can be downloaded.')
}

$checks = @(
    Test-TiaPortal
    Test-OpennessGroup
    Test-DotNetFramework
    Test-Node
    Test-PlcSim
)

$blocking = @($checks | Where-Object { $_.Required -and -not $_.Met })
$ready = $blocking.Count -eq 0

if ($Json) {
    [pscustomobject]@{
        Ready  = $ready
        Checks = $checks
    } | ConvertTo-Json -Depth 4
} else {
    foreach ($check in $checks) {
        if ($check.Met) {
            $mark = 'OK  '
        } elseif ($check.Required) {
            $mark = 'STOP'
        } else {
            $mark = 'WARN'
        }

        Write-Host "$mark $($check.Name)"

        if ($check.Found) {
            Write-Host "     found: $($check.Found)"
        }

        if (-not $check.Met -and $check.Fix) {
            Write-Host "     fix:   $($check.Fix)"
        }
    }

    Write-Host ''

    if ($ready) {
        Write-Host 'This machine meets everything the server requires.'
    } else {
        Write-Host "$($blocking.Count) requirement(s) not met. The server will not run until they are."
    }
}

if ($ready) { exit 0 } else { exit 1 }

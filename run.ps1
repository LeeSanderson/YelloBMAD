#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Starts the Yello AppHost - Yello.Host, Yello.Client and the SQL Server 2025 container.

.DESCRIPTION
    Wraps `dotnet aspire run`, the invocation tests/TESTING-CONVENTIONS.md documents, with the
    two preflight steps that are easy to forget on a cold machine.

    The Aspire CLI is a LOCAL tool (.config/dotnet-tools.json pins 13.4.6, rollForward false),
    so `dotnet aspire` does not exist until `dotnet tool restore` has run - which is also why
    the invocation is `dotnet aspire run` rather than a bare `aspire run`.

    The container-runtime check earns its place because `aspire run` starts perfectly happily
    with the runtime down: the Host and the Client come up, the `sql` resource never does, and
    the failure surfaces minutes later as a connectivity error rather than as "your container
    engine is not running". That exact confusion is on the record in story 1.1.

    `aspire run` resolves the AppHost from aspire.config.json at the repository root, so the
    working directory is part of the invocation rather than incidental; the script sets it.

.PARAMETER SkipToolRestore
    Skip `dotnet tool restore`. On a warm machine it is pure latency.

.PARAMETER SkipRuntimeCheck
    Start even when no container runtime answers. The `sql` resource will fail, and the Host
    will log it - see Yello.Host/StartupConnectivityCheck.cs.

.PARAMETER AspireArgs
    Passed through to `aspire run` verbatim. Put `--` first so PowerShell does not try to bind
    the flags itself: `./run.ps1 -- --watch`.

.EXAMPLE
    ./run.ps1

.EXAMPLE
    ./run.ps1 -- --watch
#>

[CmdletBinding()]
param(
    [switch]$SkipToolRestore,
    [switch]$SkipRuntimeCheck,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AspireArgs = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Probed out-of-process with a timeout rather than by calling `docker info` inline: when the
# Rancher Desktop backend is wedged rather than stopped, the CLI can sit on a dead Hyper-V
# socket instead of returning. A hung preflight is worse than a failed one.
function Test-ContainerRuntime {
    param([int]$TimeoutSeconds = 20)

    $docker = Get-Command docker -ErrorAction SilentlyContinue
    if (-not $docker) {
        return [pscustomobject]@{ Ok = $false; Reason = 'no `docker` CLI on PATH' }
    }

    $stdout = New-TemporaryFile
    $stderr = New-TemporaryFile
    try {
        $probe = Start-Process -FilePath $docker.Source `
            -ArgumentList 'info', '--format', '{{.ServerVersion}}' `
            -NoNewWindow -PassThru `
            -RedirectStandardOutput $stdout -RedirectStandardError $stderr

        if (-not $probe.WaitForExit($TimeoutSeconds * 1000)) {
            try { $probe.Kill($true) } catch { }
            return [pscustomobject]@{
                Ok     = $false
                Reason = "``docker info`` did not answer within ${TimeoutSeconds}s - the backend is wedged, not merely stopped"
            }
        }

        if ($probe.ExitCode -ne 0) {
            $why = (Get-Content -LiteralPath $stderr -Raw).Trim()
            if ([string]::IsNullOrWhiteSpace($why)) { $why = "``docker info`` exited $($probe.ExitCode)" }
            return [pscustomobject]@{ Ok = $false; Reason = $why }
        }

        return [pscustomobject]@{
            Ok      = $true
            Version = (Get-Content -LiteralPath $stdout -Raw).Trim()
        }
    }
    finally {
        Remove-Item -LiteralPath $stdout, $stderr -Force -ErrorAction SilentlyContinue
    }
}

Push-Location -LiteralPath $PSScriptRoot
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'The .NET SDK is not on PATH. global.json pins 10.0.303 (rollForward latestPatch).'
    }

    if (-not $SkipToolRestore) {
        Write-Host '==> dotnet tool restore (pinned Aspire CLI 13.4.6)' -ForegroundColor Cyan
        dotnet tool restore
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet tool restore failed with exit code $LASTEXITCODE."
        }
    }

    if ($SkipRuntimeCheck) {
        Write-Warning 'Container runtime check skipped. The `sql` resource will fail if nothing is listening.'
    }
    else {
        Write-Host '==> checking the container runtime' -ForegroundColor Cyan
        $runtime = Test-ContainerRuntime

        if ($runtime.Ok) {
            Write-Host "    container engine $($runtime.Version) is up" -ForegroundColor DarkGray
        }
        else {
            # The remedy is machine-specific and known: `rdctl start` is a no-op when the app is
            # already running with its backend down, so restarting the app is not enough.
            throw @"
No container runtime is reachable: $($runtime.Reason).

The AppHost needs one for the SQL Server 2025 container. This machine runs Rancher Desktop,
where a stopped backend behind a running app does NOT recover from ``rdctl start`` - it no-ops.
The sequence that works here:

    rdctl shutdown
    wsl --terminate rancher-desktop
    # relaunch Rancher Desktop, in Linux container ("dockerd/moby") mode

Then re-run this script. To start anyway and watch the ``sql`` resource fail, pass -SkipRuntimeCheck.
"@
        }
    }

    Write-Host '==> dotnet aspire run' -ForegroundColor Cyan
    dotnet aspire run @AspireArgs
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}

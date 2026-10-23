Describe "azsdk.bat MCP cleanup" {
    BeforeAll {
        $launcherPath = Join-Path (Join-Path $PSScriptRoot "..") "azsdk.bat"
        $cleanupLine = Get-Content $launcherPath | Where-Object { $_ -like "powershell.exe *" }
        $commandPrefix = 'powershell.exe -NoProfile -Command "'
        if (-not $cleanupLine.StartsWith($commandPrefix) -or -not $cleanupLine.EndsWith('"')) {
            throw "Could not extract the launcher cleanup command."
        }
        $script:cleanup = [scriptblock]::Create($cleanupLine.Substring($commandPrefix.Length, $cleanupLine.Length - $commandPrefix.Length - 1))
        $script:originalRoot = $env:AZSDK_DEV_CLI_ROOT
    }

    BeforeEach {
        $env:AZSDK_DEV_CLI_ROOT = (Join-Path $TestDrive "Azure.Sdk.Tools.Cli") + "\"
        $script:processes = @(
            [pscustomobject]@{
                ProcessId = 12345
                ExecutablePath = Join-Path $env:AZSDK_DEV_CLI_ROOT "azsdk.exe"
                CommandLine = '"C:\repo with spaces\azsdk.exe" mcp'
            }
        )
        Mock Get-CimInstance { $script:processes }
        Mock Get-Process {
            $process = [pscustomobject]@{}
            $process | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value {
                param([int] $Milliseconds)
                return $true
            }
            return $process
        }
        Mock Stop-Process {}
        $script:originalError = [Console]::Error
        $script:stderr = New-Object IO.StringWriter
        [Console]::SetError($script:stderr)
    }

    AfterEach {
        [Console]::SetError($script:originalError)
        $script:stderr.Dispose()
        $env:AZSDK_DEV_CLI_ROOT = $script:originalRoot
    }

    It "stops repository servers with arguments: <Arguments>" -TestCases @(
        @{ Arguments = "mcp" }
        @{ Arguments = "start" }
        @{ Arguments = "--debug mcp" }
        @{ Arguments = "--debug true mcp" }
        @{ Arguments = "--debug false start" }
        @{ Arguments = "--debug=true mcp" }
        @{ Arguments = '--debug "false" mcp' }
        @{ Arguments = "--debug:false start" }
        @{ Arguments = "--output json mcp" }
        @{ Arguments = "-o plain start" }
        @{ Arguments = "--output=json mcp" }
        @{ Arguments = '-o "json" mcp' }
        @{ Arguments = '--output="json" mcp' }
        @{ Arguments = "-o:json start" }
        @{ Arguments = "--debug --output json mcp --tools example" }
        @{ Arguments = "--output plain --debug false start" }
    ) {
        param([string] $Arguments)
        $script:processes[0].CommandLine = '"C:\repo with spaces\azsdk.exe" ' + $Arguments

        $stdout = @(& $script:cleanup)

        $stdout.Count | Should Be 0
        $script:stderr.ToString().Trim() | Should Be "Stopping repository MCP server PID 12345"
        Assert-MockCalled Stop-Process -Times 1 -Exactly -Scope It -ParameterFilter { $Id -eq 12345 -and $Force }
    }

    It "does not stop non-server commands: <Arguments>" -TestCases @(
        @{ Arguments = "" }
        @{ Arguments = "--debug" }
        @{ Arguments = "package get-approval-status --output json" }
        @{ Arguments = "--debug package get-approval-status" }
        @{ Arguments = "--output json package get-approval-status" }
        @{ Arguments = "package mcp" }
        @{ Arguments = "--unknown mcp" }
        @{ Arguments = "--output mcp" }
        @{ Arguments = "--output mcp start" }
        @{ Arguments = "mcp-other" }
        @{ Arguments = "--debug mcp-other" }
        @{ Arguments = "--help mcp" }
    ) {
        param([string] $Arguments)
        $script:processes[0].CommandLine = "azsdk.exe " + $Arguments

        & $script:cleanup

        Assert-MockCalled Stop-Process -Times 0 -Exactly -Scope It
        $script:stderr.ToString() | Should Be ""
    }

    It "does not stop servers outside this repository's output directory" {
        $script:processes[0].ExecutablePath = Join-Path $TestDrive "Azure.Sdk.Tools.Cli.other\azsdk.exe"

        & $script:cleanup

        Assert-MockCalled Stop-Process -Times 0 -Exactly -Scope It
    }

    It "does not stop processes whose executable path is unavailable" {
        $script:processes[0].ExecutablePath = $null

        & $script:cleanup

        Assert-MockCalled Stop-Process -Times 0 -Exactly -Scope It
    }
}

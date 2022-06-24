# These functions are meant to be used within a cmdlet supporting `-WhatIf`, i.e.
# with [CmdletBinding(SupportsShouldProcess=$true)], in order to enable dry run behavior
# when calling non-powershell commands (external binaries/scripts). The functions handle the 
# variable $WhatIfPreference that gets set via the -WhatIf flag in the outer cmdlet.
# For normal usage, the command is printed instead of run. The `RunSupportingWhatIfFlag`
# function supports running commands that support their own custom dry run flags.

function _run([string]$CustomWhatIfFlag)
{
    if ($WhatIfPreference -and [string]::IsNullOrEmpty($CustomWhatIfFlag)) {
        Write-Host "`n==> [What if] $args`n" -ForegroundColor Green
        return
    } else {
        $cmdArgs = $args
        if ($WhatIfPreference) {
            $cmdArgs += $CustomWhatIfFlag
        }
        Write-Host "`n==> $cmdArgs`n" -ForegroundColor Green
        $command, $arguments = $cmdArgs
        & $command $arguments
    }
    if ($LASTEXITCODE) {
        Write-Error "Command '$args' failed with code: $LASTEXITCODE" -ErrorAction 'Continue'
    }
}

# USAGE: `Run echo foobar`
function Run() {
    _run '' @args
}

# USAGE: `RunSupportingWhatIfFlag --what-if az deployment group create ...`
#           -> `az deployment group create ...` `$WhatIfPreference == $false`
#           -> `az deployment group create ... --what-if` `$WhatIfPreference == $true`
function RunSupportingWhatIfFlag([string]$CustomWhatIfFlag)
{
    if ($WhatIfPreference) {
        _run $CustomWhatIfFlag @args
    } else {
        _run '' @args
    }
    if ($LASTEXITCODE) {
        exit $LASTEXITCODE
    }
}

# TODO: This can be phased out after $PSNativeCommandErrorActionPreference lands in powershell 7.3
# https://docs.microsoft.com/powershell/scripting/learn/experimental-features?view=powershell-7.3#psnativecommanderroractionpreference
# USAGE: `RunOrExit wget doesnotexist`
function RunOrExit()
{
    $LASTEXITCODE = 0
    _run '' @args
    if ($LASTEXITCODE) {
        exit $LASTEXITCODE
    }
}


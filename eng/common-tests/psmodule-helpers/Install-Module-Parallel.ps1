# Manual test to verify the threadsafety of "Install-ModuleIfNotInstalled"
# If test runs several iterations with no errors, the function is likely threadsafe
# If test throws any errors related to installing or loading the module, the function likely has a race condition

$command = {
    . $PWD/../../common/scripts/Helpers/PSModule-Helpers.ps1
    Write-Host 'Install-ModuleIfNotInstalled "powershell-yaml" "0.4.1" | Import-Module'
    Install-ModuleIfNotInstalled "powershell-yaml" "0.4.1" | Import-Module
    Write-Host
}

while ($true) {
    Write-Host 'Uninstall-Module "powershell-yaml"'
    Uninstall-Module "powershell-yaml"

    $job1 = Start-Job -ScriptBlock $command
    $job2 = Start-Job -ScriptBlock $command
    $job3 = Start-Job -ScriptBlock $command
    Wait-Job -Job $job1, $job2, $job3 | Out-Null
    Receive-Job -Job $job1, $job2, $job3

    Write-Host

    $job1 = Start-Job -ScriptBlock $command
    $job2 = Start-Job -ScriptBlock $command
    $job3 = Start-Job -ScriptBlock $command
    Wait-Job -Job $job1, $job2, $job3 | Out-Null
    Receive-Job -Job $job1, $job2, $job3

    Write-Host
}

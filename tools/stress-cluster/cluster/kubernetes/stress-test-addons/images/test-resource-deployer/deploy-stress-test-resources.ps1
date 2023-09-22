$secrets = @{}
$secretsDir = "/mnt/secrets/static/*"
Get-ChildItem -Path $secretsDir | ForEach-Object {
    foreach($line in Get-Content $_) {
        $idx = $line.IndexOf("=")
        if ($idx -gt 0) {
            $key = $line.Substring(0, $idx)
            $val = $line.Substring($idx + 1)
            $secrets.Add($key, $val)
        }
    }
}

mkdir /azure
Copy-Item "/scripts/stress-test/test-resources-post.ps1" -Destination "/azure/"
Copy-Item "/mnt/testresources/*" -Destination "/azure/"

Write-Host "Job completion index $($env:JOB_COMPLETION_INDEX)"

# Avoiding ARM deployment racing condition for multiple pods running in parallel
if ($env:JOB_COMPLETION_INDEX -and ($env:JOB_COMPLETION_INDEX -ne "0")) {
    $cmd = "kubectl get pods -n $($env:NAMESPACE) -l job-name=$($env:JOB_NAME) -o jsonpath='{.items[?(@.metadata.annotations.batch\.kubernetes\.io/job-completion-index==`"0`")]..status.initContainerStatuses[?(@.name==`"init-azure-deployer`")].state.terminated.reason}'"
    Write-Host $cmd
    $result = ""
    while ($result -ne "Completed") {
        Write-Host "Waiting for pod index 0 deployment to complete."
        Start-Sleep 10
        $result = Invoke-Expression $cmd
        if ($LASTEXITCODE) {
            Write-Host $result
            throw "Failure getting pods"
        }
    }
}

# Capture output so we don't print environment variable secrets
$env = & /common/TestResources/New-TestResources.ps1 `
    -BaseName $env:BASE_NAME `
    -ResourceGroupName $env:RESOURCE_GROUP_NAME `
    -SubscriptionId $secrets.AZURE_SUBSCRIPTION_ID `
    -TenantId $secrets.AZURE_TENANT_ID `
    -ProvisionerApplicationId $secrets.AZURE_CLIENT_ID `
    -ProvisionerApplicationSecret $secrets.AZURE_CLIENT_SECRET `
    -TestApplicationId $secrets.AZURE_CLIENT_ID `
    -TestApplicationSecret $secrets.AZURE_CLIENT_SECRET `
    -TestApplicationOid $secrets.AZURE_CLIENT_OID `
    -Location 'westus3' `
    -DeleteAfterHours 168 `
    -ServiceDirectory '/azure/' `
    -SuppressVsoCommands:$true `
    -CI `
    -Force

#>

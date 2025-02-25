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

Write-Host "Logging in with federated token"
# Token file, Tenant and Client IDs are set by AKS when workload identity is enabled
$token = Get-Content -Raw $env:AZURE_FEDERATED_TOKEN_FILE
Connect-AzAccount -ServicePrincipal -Tenant $env:AZURE_TENANT_ID -ApplicationId $env:AZURE_CLIENT_ID -FederatedToken $token

Write-Host "Finding provisioner object id"
$identity = Get-AzUserAssignedIdentity -ResourceGroupName $env:STRESS_CLUSTER_RESOURCE_GROUP | Where-Object { $_.ClientId -eq $env:AZURE_CLIENT_ID }
if (!$identity) {
    throw "User Assigned Identity $($env:AZURE_CLIENT_ID) not found in resource group $($env:STRESS_CLUSTER_RESOURCE_GROUP)"
}

# Capture output so we don't print environment variable secrets
$env = & /common/TestResources/New-TestResources.ps1 `
    -BaseName $env:BASE_NAME `
    -ResourceGroupName $env:RESOURCE_GROUP_NAME `
    -SubscriptionId $env:AZURE_SUBSCRIPTION_ID `
    -TenantId $env:AZURE_TENANT_ID `
    -ProvisionerApplicationId $env:AZURE_CLIENT_ID `
    -ProvisionerApplicationSecret $identity.PrincipalId `
    -TestApplicationId $env:AZURE_CLIENT_ID `
    -TestApplicationSecret "" `
    -TestApplicationOid $identity.PrincipalId `
    -Location 'westus3' `
    -DeleteAfterHours 168 `
    -ServiceDirectory '/azure/' `
    -SuppressVsoCommands:$true `
    -CI `
    -Force

#>

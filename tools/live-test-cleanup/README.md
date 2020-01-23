# Live Test Resource Cleanup

Removes resource groups from the given subscription. Cleans up resources that
might be left behind after a normal pipeline's execution encounters an abnormal
termination (timeout, crash, etc.). This is accomplished by parsing the value in
the `DeleteAfter` tag using .NET [DateTime.Parse](https://docs.microsoft.com/en-us/dotnet/api/system.datetime.parse?view=netframework-4.8)
and removing if the current time is later than the `DeleteAfter` time.

Run this script on some cadence (e.g. 1x every 24 hours) to ensure subscriptions
do not have unused resources lying around. (see
`eng/pipelines/live-test-cleanup.yml`)

## Requirements

* [az CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest)
* AAD App with permissions in the subscription to delete resource groups

## Parameters

* `ProvisionerApplicationId` -- AAD Application ID
* `ProvisionerApplicationSecret` -- AAD Application Secret
* `ProvisionerApplicationTenantId` -- AAD Tenant ID
* `SubscriptionId` -- Subscription ID to clean
* `Verbose` -- Verbose output
* `WhatIf` -- Outputs destructive operations but does not perform them
* `Confirm` -- Prompts before destructive operations
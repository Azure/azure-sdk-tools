## Resource Management Guidelines

This document contains guidelines for creating and managing Azure resources in the Azure SDK
subscriptions. EngSys has automation that will delete resources based on the criteria outlined below. This is to
improve our overall security risks and manage costs.

  * [Managing Dev Resources in the Playground Subscription](#managing-dev-resources-in-the-playground-subscription)
     * [Resource Groups](#resource-groups)
     * [Role Assignments](#role-assignments)
  * [Managing CI resources in the Test Subscription](#managing-ci-resources-in-the-test-subscription)
     * [Resource Groups](#resource-groups-1)
     * [Role Assignments](#role-assignments-1)

## Managing Dev Resources in the Playground Subscription

This section applies to resource groups located in the `Azure SDK Developer Playground` subscription.

Currently, the automation will inspect resource groups and role assignments only.

### Resource Groups

The automation will delete all resource groups in the playground subscription DAILY, unless they meet at least one of the following criteria:

- The resource group starts with a valid Microsoft alias within `microsoft.com` or `ntdev.microsoft.com`, followed by an
  optional hyphen and extra characters.
    - Valid group name examples: `myalias`, `myalias-test-foobar` for `myalias@microsoft.com`
- The resource group contains a tag with the name `Owners` where the value is a csv formatted string that contains at
  least one valid Microsoft alias within `microsoft.com` or `ntdev.microsoft.com`. This convention should only be used
  when it is not possible to name the resource group with an alias (for example, inner groups auto-created by a resource
  provider like AKS).
    - Valid owner tag examples: `Owners: myalias`, `Owners: myalias,anotheralias,lastalias`
- The resource group contains a tag with the name `DeleteAfter` and an [ISO8601 formatted date value](https://www.iso.org/iso-8601-date-and-time-format.html)
  (see below for examples), where the date is greater than the current date and not greater than 7 days in the future.
    - If the `DeleteAfter` value is in the past, or greater than 7 days in the future, it will be deleted.
    - If you have a resource group for which you would like to extend the lifetime, update the `DeleteAfter` tag to a
      future date to renew the lease.
    - Resource groups which do not contain a `DeleteAfter` tag will have one added for a 24 hour duration
      to mark them for deletion and give any test pipelines that are actively using the resources time to
      complete.
    - Valid date tag format: `DeleteAfter: 2022-01-29T00:35:48.9372617Z`
      Example extending resource group lease by three days:
      ```
      # Test Resources Script
      # Update default resource group created via New-TestResources.ps1 and a default DeleteAfter of 48 hours.
      ./eng/common/TestResources/Update-TestResources.ps1
      # Update custom resource group name and/or custom DeleteAfter hours value (max 168).
      ./eng/common/TestResources/Update-TestResources.ps1 -ResourceGroupName <rg name> -DeleteAfterHours 120

      # azure powershell, set manually
      Set-AzResourceGroup -Name <group name> -Tag @{ DeleteAfter = [DateTime]::UtcNow.AddDays(3).ToString("o") }

      # azure cli, set manually
      az group update -g <group name> --tags DeleteAfter=$(date -u +"%Y-%m-%dT%H:%M:%SZ" -d "$(date) + 3 day")
      ```

For long-lived resources, please also add a resource group tag named `Purpose` describing the purpose of the group.

### Role Assignments

Role assignments should be created at the resource group scope or below, where the resource group follows the above
guidelines. Subscription-level role assignments may be deleted at any time. Reach out to the EngSys team for exemptions.

## Managing CI Resources in the Test Subscription

This section applies to resource groups located in the `Azure SDK Test Resources` subscription. Developers should not
create resources for individual testing in this subscription, but sometimes resources may need to be created to host
static assets, secrets or configuration related to pipeline tests.

Currently all EngSys tooling will inspect resource groups and role assignments only.

### Resource Groups

EngSys will delete all resource groups in the testing subscription DAILY, unless they meet at least one of the following criteria:

- The resource group contains a tag with the name `Owners` where the value is a csv formatted string that contains at
  least one valid Microsoft alias within `microsoft.com` or `ntdev.microsoft.com`. This convention should only be used
  when it is not possible to name the resource group with an alias (for example, inner groups auto-created by a resource
  provider like AKS).
    - Valid owner tag examples: `Owners: myalias`, `Owners: myalias,anotheralias,lastalias`
- The resource group contains a tag with the name `DeleteAfter` and an [ISO8601 formatted date value](https://www.iso.org/iso-8601-date-and-time-format.html)
  (see below for examples), where the date is greater than the current date and not greater than 7 days in the future.
    - If the `DeleteAfter` value is in the past, or greater than 7 days in the future, it will be deleted.
    - If you have a resource group for which you would like to extend the lifetime, update the `DeleteAfter` tag to a
      future date to renew the lease.
    - Resource groups which do not contain a `DeleteAfter` tag will have one added for a 24 hour duration
      to mark them for deletion and give any test pipelines that are actively using the resources time to
      complete.
    - Valid date tag format: `DeleteAfter: 2022-01-29T00:35:48.9372617Z`
      Example extending resource group lease by three days:
      ```
      # Test Resources Script
      # Update custom resource group name and/or custom DeleteAfter hours value (max 168).
      ./eng/common/TestResources/Update-TestResources.ps1 -ResourceGroupName <rg name> -DeleteAfterHours 120

      # azure powershell, set manually
      Set-AzResourceGroup -Name <group name> -Tag @{ DeleteAfter = [DateTime]::UtcNow.AddDays(3).ToString("o") }

      # azure cli, set manually
      az group update -g <group name> --tags DeleteAfter=$(date -u +"%Y-%m-%dT%H:%M:%SZ" -d "$(date) + 3 day")
      ```

For long-lived resources, please also add a resource group tag named `Purpose` describing the purpose of the group.

### Role Assignments

Role Assignments should be created at the resource group scope or below, where the resource group follows the above
guidelines. Subscription level role assignments may be deleted at any time. Reach out to the EngSys team for exemptions.

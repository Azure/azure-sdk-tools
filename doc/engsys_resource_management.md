## Resource Management Guidelines

This document contains guidelines for creating and managing Azure resources in the Azure SDK
subscriptions. EngSys has automation that will delete resources based on the criteria outlined below. This is to
improve our overall security risks and manage costs.

  * [Managing Development and Testing Resources](#managing-development-and-testing-resources)
     * [Resource Groups](#resource-groups)
        * [Quick Start Creating Resources](#quick-start-creating-resources)
        * [Full Resource Management Specification](#full-resource-management-specification)
     * [Role Assignments](#role-assignments)

## Managing Development and Testing Resources

This section applies to resource groups located in any of the dev/test subscriptions managed by the Azure SDK
Engineering System team, such as:

- Azure SDK Developer Playground
- Azure SDK Test Resources
- Azure SDK Test Resources - Preview
- Sovereign cloud test subscriptions

Currently, the automation will inspect resource groups and role assignments only.

### Resource Groups

**NOTE:** Development resources should only be created in the `Azure SDK Developer Playground` subscription. The other
subscriptions are intended to be used for CI resources and not manual testing.

#### Quick Start Creating Resources

*To create temporary resources for testing:*

Create a resource group to contain all testing resources. The resource group will be deleted after approximately 24-28 hours.
If you need the group for a little more time, add/update a tag named `DeleteAfter` with a new date by running the below powershell script:

```
./eng/common/TestResources/Update-TestResources.ps1 -ResourceGroupName <rg name> -DeleteAfterHours 120
```

*To create long-lived resources:*

Create a resource group to contain all testing resources. The resource group name should start with your Microsoft alias.
Valid group name examples: `myalias`, `myalias-feature-101-testing`.

#### Full Resource Management Specification

The automation will delete all resource groups in managed subscriptions multiple times DAILY, unless they meet at least one of the following criteria:

- The resource group starts with a valid Microsoft alias within `microsoft.com` or `ntdev.microsoft.com`, followed by an
  optional hyphen and extra characters.
    - Valid group name examples: `myalias`, `myalias-test-foobar` for `myalias@microsoft.com`
- The resource group contains a tag with the name `Owners` where the value is a csv formatted string that contains at
  least one valid Microsoft alias within `microsoft.com` or `ntdev.microsoft.com`.
    - This convention should only be used when it is not possible to name the resource group with an alias
        (for example, inner groups auto-created by a resource provider like AKS).
    - Valid owner tag examples: `Owners: myalias`, `Owners: myalias,anotheralias,lastalias`
- The resource group contains a tag with the name `DeleteAfter` and an [ISO8601 formatted date value](https://www.iso.org/iso-8601-date-and-time-format.html)
  (see below for examples), where the date is greater than the current date.
    - If the `DeleteAfter` value is in the past at the time the cleanup pipeline runs, it will be deleted.
      The cleanup pipeline runs multiple times daily, starting at 12am pacific time (UTC-8). See the [schedule](https://dev.azure.com/azure-sdk/internal/_apps/hub/ms.vss-ciworkflow.build-ci-hub?_a=edit-build-definition&id=1357&view=Tab_Triggers).
    - If you have a resource group for which you would like to extend the lifetime, update the `DeleteAfter` tag to a
      future date to renew the lease.
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

Resource groups which do not satisfy at least one of the above criteria will have a `DeleteAfter` tag added for a
24 hour duration to mark them for deletion and give any test pipelines that are actively using the resources time to
complete.

**NOTE:** For long-lived resources, please also add a resource group tag named `Purpose` describing the purpose of the group.

### Role Assignments

Role assignments should be created at the resource group scope or below, where the resource group follows the above
guidelines. Subscription-level role assignments may be deleted at any time. Reach out to the EngSys team for exemptions.

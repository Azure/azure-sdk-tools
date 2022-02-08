This document contains a record of commits to the common engineering system that may be backwards incompatible with older versions of the codebase. In cases where SDK code must be released and/or pipelines must pass for historical commits (e.g. a backport to a hotfix branch), the following commits may need to be applied.

The commit SHAs below are listed based on the commit in the [azure-sdk-tools](https://github.com/Azure/azure-sdk-tools) repository. These changes are then synced to the SDK repositories. To backport one of these commits to an SDK repository, run the following:

```
# Run this if using SSH auth
git remote add tools git@github.com:azure/azure-sdk-tools.git
# Run this if using HTTPS auth
git remote add tools https://github.com/azure/azure-sdk-tools

# Pull commits from azure-sdk-tools repository
git fetch tools origin/main

# Apply relevant backport to SDK repository eng/common
git cherry-pick <commit sha> -X theirs --allow-empty
```

## Breaking changes

**Commit**

[10980eff19ade0f00cfb2174fd4ce71a11a9c88a](https://github.com/Azure/azure-sdk-tools/pull/2721/commits/10980eff19ade0f00cfb2174fd4ce71a11a9c88a)

**Notes**

Adds a `ProvisionerApplicationOid` parameter to the `New-TestResources.ps1` script and the
corresponding subscription configuration objects in the testing keyvaults. Older version of the
script will fail when invoked with the `-ProvisionerApplicationOid` parameter as it does not exist.

This commit should be backported to fix the pipeline error:

```
A parameter cannot be found that matches parameter name 'ProvisionerApplicationOid'.
```


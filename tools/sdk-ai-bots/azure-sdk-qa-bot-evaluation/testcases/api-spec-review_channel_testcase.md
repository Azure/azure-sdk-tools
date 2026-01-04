# Suppression Review in RPSaaSDev

## question 
Hi, I just created a PR to RPSaaSDev for our product which will help us test using the new RestrictTrafficToTestTenants feature. 
 
The idea is that the changes we plan to merge to RPSaaSMaster will be mirrored in a dedicated folder in RPSaaSDev so we can test the exact swaggers we plan to ship, while we have another 'dev' folder where we put our iterative swagger changes which we plan to ship later.
 
I raised the [PR against RPSaaSDev](https://github.com/Azure/azure-rest-api-specs-pr/pull/25834), but it got flagged for a suppression review. Is this a merge blocker for RPSaaSDev? I think it just got flagged since it's a new folder, but its the same suppressions that are already approved for that namespace in our dev folder as well as in RPSaaSMaster. Can we just merge? Thanks in advance!
 
(This dual folder setup will never be reflected for our product in RPSaaSMaster by the way, just a development helper for us!)

## answer
RPSaaSDev does not require that checks pass before checking in the code,  so this is more like a warning that when this moves to one of the protected branches (RPSaaSMaster or public main) you will need to go through a suppression review.

# How to Sdk generation in local

## question 
I have couple of prs failing with sdk validation.
Is there a way to reproduce these errors in local?

## answer
You can run our tooling locally with our Azure SDK Tools MCP: [AzSDK agent](https://eng.ms/docs/products/azure-developer-experience/develop/azsdk-tools-mcp)
It will help:
validating your local environment
generating the SDKs locally
learn more about the SDK generation and release process
The documentation shared has example prompts you can try.

# Update the enum for the exisitng API version

## question 
API Spec Review
 
We have the api-spec for verion 2025-08-01 for the storage mover [azure-rest-api-specs-pr/specification/storagemover/resource-manager/Microsoft.StorageMover/stable/2…](https://github.com/Azure/azure-rest-api-specs-pr/tree/RPSaaSMaster/specification/storagemover/resource-manager/Microsoft.StorageMover/stable/2025-08-01)
 
This version is not live yet.And no customer are using this. If I have to change a Enum and also add new optional for this version, can it be changed and we get the approval from the breaking change team?

## answer
If this version merged to RPSaaSMaster, it has "released".  When you added label PublishToCustomers to the PR, you were agreeing to this statement:
```
This PR targets either the main branch of the public specs repo or the RPSaaSMaster branch of the private specs repo. These branches are not intended for iterative development. Therefore, you must acknowledge you understand that after this PR is merged, the APIs are considered shipped to Azure customers. Any further attempts at in-place modifications to the APIs will be subject to Azure's versioning and breaking change policies.
```
That said, you can open a PR with the changes you'd like to make, then follow the process in the "next steps to merge comment" once your breaking changes are detected.
 
The process is documented at these two links:
1.https://aka.ms/brch
2.https://aka.ms/azsdk/pr-brch-deep
You can self-apply a label if your PR meets the qualifications (2).
 
Or contact the breaking changes board for review and approval, by attending the office hours, or sending email to azbreakchangereview@microsoft.com (1).

# Assistance required with breaking change PR

## question 
Hi team,
We have the following open PR: [[Microsoft.Marketplace] - Adding new product fields by eladschartz · Pull Request #38748 · Azure/az…](https://github.com/Azure/azure-rest-api-specs/pull/38748)
 
Which adds a few missing fields to our returned payload and seems to be marked as a breaking change (violation of rule 1041 - AddedPropertyInResponse). Since we are a REST API only, I'm not sure how counts as a breaking change as it changes nothing about the way existing customers interact with our APIs.
 
Is there some way to suppress this rule/ request an exception?

## answer
Follow the process documented in the "next steps to merge" comment in your PR, to engage with the breaking change reviewers.
For your breaking changes, unless your spec has a special exception, I think you might need to move these changes to a new API version.  Existing API versions should generally not be updated like your PR is doing.

# Quick Check on PR Review Status

## question 
Hi API Spec Review,

I noticed [this PR](https://github.com/Azure/azure-rest-api-specs/pull/38001) has been waiting for review for a couple of days—which is totally fine—but I just wanted to check in to confirm whether it's showing up as ready for review on your end. The GitHub bot added the `ARMSignedOff` label, but it hasn’t been approved yet, so I’m wondering if it’s currently in the review queue or if there’s anything else needed from our side.

Thanks in advance!

## answer
Once `ARMSignedOff` label is added (either manually by a reviewer, or automatically by the bot), it's been approved by ARM. You can ask for your team member to approve it. Anyone with write access can do that. To request access, you can follow this: https://eng.ms/docs/products/azure-developer-experience/onboard/access?tabs=write-access
You can folllow the `Next Steps to Merge` section on what's next.

# Licence agreement

## question 
Hi,

[Added Mongo Types by sougho · Pull Request #23842 · Azure/azure-rest-api-specs-pr](https://github.com/Azure/azure-rest-api-specs-pr/pull/23842)

We have a PR which has all pipelines passing except the license agreement.
It looks like only author's license agreement comment will be considered by the automated pipeline.
Can someone help to resolve this? author of this PR won't be available for a while and i have taken over this PR now

## answer
If the PR author is no longer available to agree to the `license/cla`, you will need to open a new PR from a user who can agree.

# Avocado warning

## question 
Hi, 
 
Need some help on this PR - [Renaming the RP to Lionrock from PlannedQuota by sathchan · Pull Request #24569 · Azure/azure-rest-…](https://github.com/Azure/azure-rest-api-specs-pr/pull/24569)

[Swagger Avocado](https://github.com/Azure/azure-rest-api-specs-pr/actions/runs/18212677882) and [Swagger BreakingChange](https://github.com/Azure/azure-rest-api-specs-pr/actions/runs/18212677881/job/51856124052?pr=24569) validations fail, because in this PR we are removing Microsoft.PlannedQuota namespace and the corresponding specification file: 
```
RPSaaSMaster/specification/plannedquota/resource-manager/Microsoft.PlannedQuota/preview/2025-10-01-preview/plannedQuota.json
```
This is completely expected and safe to remove, as this namespace was created in the [previous PR](https://github.com/Azure/azure-rest-api-specs-pr/pull/24280) and was never in use.

Please note, Microsoft.PlannedQuota was neither registered nor in use. 

Could you help on how to bypass this?

## answer
Avocado failing when you completely delete a spec folder is a known issue:
 
[[MISSING_README] False positive when migrate spec to FSv2 · Issue #163 · Azure/avocado](https://github.com/Azure/avocado/issues/163)
 
If you need to merge a PR with this failing, add a note to your PR for your ARM reviewer to add label `Approved-Avocado` to unblock your one PR, until the bug in the tool is fixed.

# TypeSpec Errors as a result of moving existing swaggers to different directory

## question 
In [PR37469](https://github.com/Azure/azure-rest-api-specs/pull/37469) we are moving swagger files related to existing customer facing API that are GA/stable, but are located in a 'preview' directory and are moving them to a 'stable' directory. As a result are getting multiple TypeSpec Errors because these were not created using TypeSpec. There are no plans to change existing released API. Is there any reason these errors cannot be suppressed? 
 
Would the following suppression be adequate?

```
  - suppress: TypeSpec
    from: <filename>.json
    reason: The reason for this suppression is the API is already released and introducing changes create undo customer risk.
```

## answer
Yes, you can suppress the error if this swagger should never be converted to TypeSpec.
 
https://github.com/Azure/azure-rest-api-specs/wiki/TypeSpec-Requirement#suppressing-failures

# Guidelines for revising an already published API spec?

## question 
Hello, we recently published a new API version, however, we realized that a property should have been marked optional. Are there guidelines on how to revise an already published API version? We have not announced this version to our customer yet. 

## answer
Your best next step, is to create a PR fixing the problem.  The "next steps to merge" comment should guide you from there, including the breaking change process you may need to follow.

# Creating new service typespec definition

## question 
Hello,
I'm currently writing a new definition for a service and wanted to ask if there's any guidance on how to go about it. I saw this guide [Work against the release branch](https://eng.ms/docs/products/azure-developer-experience/design/api-specs-pr/api-tutorial/api-tutorial-2-update) however it doesn't go over creating the service folder and creating the readme.md file, please help with this, thanks

## answer
The first thing is to start in the specs repositories. 
You can use:
Copilot to help you create folder structure, files needed, etc
Use the [Azure SDK Tools MCP](https://aka.ms/azsdk/agent#generate-code) which will have more context and will utilize what it is in the specs repositories.

This is a new service API? If it's new, you should be able to ask copilot (in vscode) to create a new service, and it will prompt you for some information to create the proper folder structure
 
The documentation it is using is based on https://github.com/Azure/azure-rest-api-specs/wiki/Azure-Service-Versioning-Guideline, then it will initialize a project for you after the folders are created.

# Creating a Management Plane RP and referencing Data Plane model

## question 
Hello API Spec Review, I'm currently working on creating a new management plane RPAAS RP in the private spec repo based off of the `RPSaaSMaster` branch, the service spec requires a reference to Compute RP model `VMProfileProperties` however the `specification\compute` folder for Compute is only available in the main branch of the private repo, so I wanted to ask:
Is it possible to create a RPAAS RP based on the main branch in the private repo?
Can a RPAAS RP reference a model in a data plane RP?
Is there a way to reference the compute model from the RPSaaSMaster branch?
Thanks!

## answer
resource-manager specs should not share anything with data-plane specs.  Our latest guidelines go even further, preventing any sharing across different specs:
https://github.com/Azure/azure-rest-api-specs/wiki/Azure-Service-Versioning-Guideline

# IntegerTypeMustHaveFormat errors

## question 
I've got multiple [IntegerTypeMustHaveFormat errors](https://github.com/Azure/azure-rest-api-specs/actions/runs/18047835415) in existing swagger files in my [PR](https://github.com/Azure/azure-rest-api-specs/pull/37469). These swagger files all represent stable versions of Azure Maps REST API that have been available to customers for some time, I'm just moving the files to different directories, so changes could potentially have customer impact. If there is no format defined, what does it default to? I would imagine `int32` given an int in C# is int32 (int64 is a long)... Does this even apply to REST API? If the default value is int32 for example, would it be appropriate to define them all as `"format": "int32"`,? or would it be better to suppress these errors?

## answer
if you are just moving swagger files, your best option is to leave them unchanged, and add suppressions to readme.md:
 
https://github.com/Azure/azure-rest-api-specs/wiki/Swagger-LintDiff#adding-scoped-suppressions
 
if you have many instances of the same error, you could use a global suppression for that error

# Trying to understand API spec validation errors

## question 
Hello all,

I just published a new PR for adding a new API version for our service. I followed the same procedure we have followed over last couple of years, but I am seeing validation errors which are pretty hard to decipher with my limited knowledge regarding them.

PR: https://github.com/Azure/azure-rest-api-specs/pull/38843

Can someone please take a look and help me narrow down the issues?

The best I can see is that this file is missing?
```
Cause: ResolverError: Error reading file "/home/runner/work/azure-rest-api-specs/azure-rest-api-specs/after/specification/storagecache/common-types/resource-management/v3/types.json"
```

## answer
It looks like you have incorrect relative paths to common-types.
 
In your previous spec: 
```
"$ref": "../../../../../../common-types/resource-management/v3/types.json#/parameters/ApiVersionParameter"
```
(6 segments bfore common-types)
in this PR:
```
"$ref": "../../../../../common-types/resource-management/v3/types.json#/parameters/ApiVersionParameter"
```
(5 segments before common-types)

# Generating Dictionary<string,object> type for swagger

## question 
Hi API Spec Review, 
We are currently using `IDictionary<string, object>` in our models to allow passing through various key-value pairs, where the values can be a string, an array, or other types. So I defined the property in Swagger and provided examples in one PR, but encountered errors during PR validation. 
Could you please suggest the right way to define such a flexible dictionary type in Swagger, and provide an example?
PR Link: [Add AddtionalProperties for DataConnector by LaylaLiu-gmail · Pull Request #38644 · Azure/azure-res…](https://github.com/Azure/azure-rest-api-specs/pull/38644/files#diff-2770767541dae7e1213d29b59c6e3bed488a458947839f25fdc31b3c7844ad42)
Dictionary Definition in PR:
```
"extendedProperties": {
  "type": "object",
  "additionalProperties": {
  "type": "object"
}
```
Examples in PR:
```
"extendedProperties": {
  "environment": "production",
  "owner": "alice",
  "additionalEndpoints": [
    "https://foo.kusto.windows.net/databasename",
    "https://bar.kusto.windows.net/databasename"
  ]
}
```
Validation Error:
```
❌  Check failure on line 8 in specification/app/resource-manager/Microsoft.App/ContainerApps/stable/2026-01-01/examples/Ag...

GitHub Actions / Swagger ModelValidation - Analyze Code

INVALID_TYPE: Expected type object but found type array

❌  Check failure on line 8 in specification/app/resource-manager/Microsoft.App/ContainerApps/stable/2026-01-01/examples/Ag...

GitHub Actions / Swagger ModelValidation - Analyze Code

INVALID_TYPE: Expected type object but found type string
```

## answer
I think what you want (unfortunately) is
```
"extendedProperties": {
  "type": "object",
  "additionalProperties": {}
}
```
strictly speaking, these should be the same, but autorest interprets {} to be any json value
In typespec this would be `Record<unknown>`  (a dictionary in which the properties can be any json type)

# SDK Validation - .NET

## question 
I have an open PR [inital genome api spec by samira-farhin · Pull Request #25097 · Azure/azure-rest-api-specs-pr](https://github.com/Azure/azure-rest-api-specs-pr/pull/25097) that's currently failing on [SDK Validation - .NET - PR](https://github.com/Azure/azure-rest-api-specs-pr/pull/25097/checks?check_run_id=53550607849). My latest changes to resolve SDK Validation in "tspconfig.yaml" are included below. After these updates, the SDK validation issues for Go and Java were resolved, but the C# issue still remains. I'm unsure what else needs to be addressed.
[inital genome api spec by samira-farhin · Pull Request #25097 · Azure/azure-rest-api-specs-pr](https://github.com/Azure/azure-rest-api-specs-pr/pull/25097/commits/85af075feeb98889d8dc27bdd838d409239f5eab)
[inital genome api spec by samira-farhin · Pull Request #25097 · Azure/azure-rest-api-specs-pr](https://github.com/Azure/azure-rest-api-specs-pr/pull/25097/commits/a43851cea6c490ee183943383e817d882b85be23)
```
command  pwsh ./eng/scripts/Automation-Sdk-Init.ps1 ../azure-sdk-for-net-pr_tmp/initInput.json ../azure-sdk-for-net-pr_tmp/initOutput.json
command  pwsh ./eng/scripts/Invoke-GenerateAndBuildV2.ps1 ../azure-sdk-for-net-pr_tmp/generateInput.json ../azure-sdk-for-net-pr_tmp/generateOutput.json
cmdout  [.Net] Start to call tsp-client to generate package:Azure.ResourceManager.Genome
cmdout  [.Net] Start to build sdk project: /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src
cmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/GenomeAccountCollection.cs(270,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=netstandard2.0]
cmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/GenomeAccountCollection.cs(300,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=netstandard2.0]
cmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/Extensions/MockableGenomeSubscriptionResource.cs(69,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=netstandard2.0]
cmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/Extensions/MockableGenomeSubscriptionResource.cs(99,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=netstandard2.0]
cmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/Extensions/MockableGenomeSubscriptionResource.cs(69,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=net8.0]
cmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/Extensions/MockableGenomeSubscriptionResource.cs(99,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=net8.0]
cmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/GenomeAccountCollection.cs(270,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=net8.0]
cmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/GenomeAccountCollection.cs(300,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=net8.0]
cmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/GenomeAccountCollection.cs(270,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=netstandard2.0]
cmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/GenomeAccountCollection.cs(300,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=netstandard2.0]
cmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/Extensions/MockableGenomeSubscriptionResource.cs(69,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=netstandard2.0]
cmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/Extensions/MockableGenomeSubscriptionResource.cs(99,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=netstandard2.0]
cmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/Extensions/MockableGenomeSubscriptionResource.cs(69,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=net8.0]
cmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/Extensions/MockableGenomeSubscriptionResource.cs(99,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=net8.0]
cmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/GenomeAccountCollection.cs(270,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=net8.0]
cmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/GenomeAccountCollection.cs(300,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=net8.0]
cmdout  [.Net]     8 Error(s)
cmdout  [.Net] [ERROR] Failed to build the sdk project: Azure.ResourceManager.Genome for service: genome. Exit code: False. Please review the detail errors for potential fixes. If the issue persists, contact the DotNet language support channel at https://aka.ms/azsdk/dotnet-teams-channel and include this spec pull request.
```

## answer
First, your PR is to branch `main` of repo `specs-pr`.  Such PRs can be used to preview check results, but they cannot be merged.  Did you intend to open a PR to `RPSaaSMaster` or the public `specs` repo?

Second, `check SDK Validation - .NET - PR` is not required, so you can merge a PR with it failing, if you are OK with it failing.  If you want support on the check failure:
 
Docs: https://github.com/Azure/azure-rest-api-specs/wiki/SDK-Validation
Teams: [Code Generation - .NET | Azure SDK | Microsoft Teams](https://teams.microsoft.com/l/channel/19%3Aacbd512e57bd475198ea6bf4564599e3%40thread.skype/Code%20Generation%20-%20.NET?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47)

# PR Review for GalleryRP 2025-03-03 Release

## question 
Hello Team! Running into some issues with BreakingChange-Go-Sdk and BreakingChange-Javascript-Sdk labels on two PRs that we are trying to approve for the GalleryRP 2025-03-03 Release. I'm not sure if this is the right channel to be asking in but we need these Breaking Changes approved before moving forward with the Release, thank you for the help!

The links to the PRs are as follows:
Introducing New Resource Type - Gallery Scripts found [here](https://github.com/Azure/azure-rest-api-specs/pull/35887)
Adding New Property StorageAccountStrategy found [here](https://github.com/Azure/azure-rest-api-specs/pull/35769)
```
GitHub Notifications
Pull Request | Azure/azure-rest-api-specs #35887
New resource type - Gallery Scripts
ARM (Control Plane) API Specification Update Pull Request
grizzlytheodore wants to merge 18 commits from galleryTsp-GalleryScript into feature/cplat-2025-03-03-tsp
```

## answer
both your PRs are to a feature branch (feature/cplat-2025-03-03-tsp), which has no required checks.  you will need to get these breaking changes reviewed when you try to merge to a release branch like main or RPSaaSMaster.
https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/ci-fix.md#sdk-breaking-change-review

# Stuck in license/cla and Automated Merge checks for more than 1 hour

## question 
Hi, my PR got ARMSignoff and all requirements seem to have met. But it is still waiting on these 2 required checks - "license/cla" and "Automated Merge" which I verified that are completed and succeeded. It is stuck here for more than an hour. Is this expected or known issue? Any pointers would help. Thanks!

PR - [Add ACA env connection to caphost by sarajag · Pull Request #38223 · Azure/azure-rest-api-specs](https://github.com/Azure/azure-rest-api-specs/pull/38223)

## answer
this membership must be public: https://github.com/orgs/Azure/people?query=sarajag
 
please follow all the steps here: https://aka.ms/azsdk/access
 
you should also join Microsoft org (and make your membership public):
 
https://github.com/orgs/Microsoft/people?query=sarajag
 
after fixing your org memberships, push another commit to your PR to re-run checks

# Copy-ApiVersion.ps1 is no longer available

## question 
I am networking team and working on creation of new swagger release branch for api version 2025-05-01. As part of the process, we used to execute Copy-ApiVersion.ps1 command to update some of the references form old version to new version
 
I see that above script file is no longer available. Could you please suggest if there is any alternative script added in place of this or do we need to perform the changes manually?

## answer
The script is deprecated, and no longer supported.  It was removed in these PRs, which you can view for context on the change:
 
https://github.com/Azure/azure-rest-api-specs/pull/37818
https://dev.azure.com/azure-sdk/internal/_git/azure-sdk-docs-eng.ms/pullrequest/1323
The new docs have (brief) instructions you can use in place of the script:
```
Create a new directory that will include all the OpenAPI spec files and examples.
Copy the OpenAPI specs files from an existing version to use it as a base for your new work, and update all the old version references in said specs files to the new version.
Update the Autorest configuration readme with the new version Tag.
```

# RPSaaSMaster - Simple string change pullrequest marked as a breaking-change for unrelated packages

## question 
I submitted this pullrequest to clean up some references that were missed when we changed a new resource name

Branch: RPSaaSMaster
Service: Microsoft.PortalServices/copilot
PR: [Typo Fix: Clean up old copilotPlans references in Microsoft.PortalServices/copilots 2025-10-01-prev…](https://github.com/Azure/azure-rest-api-specs-pr/pull/24760)

It has failed validation for breaking change - looking at the results, the errors appear to reference unrelated services/packages as the source of the breaking change:

Example:
```
❌ Breaking Change(Cross-Version) - Analyze Code
appendOadRuntimeErrors: {"type":"Raw","level":"Error","message":"Runtime Exception","time":"2025-09-24T16:53:33.708Z","groupName":"stable","extra":{"new":"https://github.com/Azure/azure-rest-api-specs-pr/blob/05536a3ad9be35b516ecb39df1c828ba8fdacc9b/specification/devcenter/resource-manager/Microsoft.DevCenter/preview/2023-11-01-preview/devcenter.json","old":"https://github.com/Azure/azure-rest-api-specs-pr/blob/main/specification/devcenter/resource-manager/Microsoft.DevCenter/stable/2023-04-01/devcenter.json","details":"incompatible properties : resourceId\n    definitions/OperationStatus/properties/resourceId\n    at file:///home/runner/work/azure-rest-api-specs-pr/azure-rest-api-specs-pr/specification/devcenter/resource-manager/Microsoft.DevCenter/preview/2023-11-01-preview/devcenter.json#L5192:8\n    definitions/OperationStatusResult/properties/resourceId\n    at file:///home/runner/work/azure-rest-api-specs-pr/azure-rest-api-specs-pr/specification/common-types/resource-management/v5/types.json#L279:8"}}
```

Am I misreading these errors? or is there a problem with the validation - if so do we need to request a sign off?

## answer
Something about the history of the head branch of your PR, is confusing our BreakingChanges check, to thinking you modified more files than shown in the "diff" of your PR.  Maybe an unusual merge or something.

Easiest fix:
pull latest changes in RPSaaSMaster
create a new branch from RPSaaSMaster
create a single commit with the changes you want
create a new PR, using this as the head branch

# DNS Resolver New Version Review

## question 
Hi team, I am trying to get a review on my PR but bot removes the WaitForARMFeedback label. Could someone help take a look and approve/add the breaking change label? The breaking change CI/CD is flagging as a property has been changed from required to not required.
```
The following breaking changes have been detected in comparison to the latest stable version
❌ 1025 - RequiredStatusChange
Displaying 1 out of 1 occurrences.
Index | Description
1 | The required status changed from the old version ('True') to the new version ('False').
New: DnsResolver/preview/2025-10-01-preview/openapi.json#L4901:7
definitions.DnsSecurityRuleProperties.properties
Old: DnsResolver/stable/2025-05-01/openapi.json#L4892:7

❌ 1027 - DefaultValueChanged
Displaying 1 out of 1 occurrences.
Index | Description
1 | The new version has a different default value than the previous one.
New: DnsResolver/preview/2025-10-01-preview/openapi.json#L4916:9
definitions.DnsSecurityRuleProperties.properties.allowedDomainsLists
Old: DnsResolver/stable/2025-05-01/openapi.json#L4907:9
```
[Release 2025-10-01-preview for DNS Resolver by jamesvoongms · Pull Request #24470 · Azure/azure-res…](https://github.com/Azure/azure-rest-api-specs-pr/pull/24470)

## answer
In the "next steps to merge" comment, your PR indicates it's ready to merge. 
Since your PR is to a "release-*" branch, the Breaking Changes board will not review it, and the check is not required.  Your breaking changes will be reviewed when your release branch merges to main/RPSaaSMaster.
https://eng.ms/docs/products/azure-developer-experience/design/api-specs-pr/api-versions-and-branches#branch-protection-rules-table
# Azure Device Registry - Release Plan Missing our sdk packages in other language sdks

## question
Hi 
General! i'm trying to fill out the sdk planner for rolling out our service to GA for each of the supported language sdks. I tried adding them but other than java and .NET, i'm unable to find in the dropdowns the Device Registry sdk packages (Go, Python, JS). Typing in the dropdowns doesn't do anything. Meanwhile, I confirm that our sdks exist:
JS: Release @azure/arm-deviceregistry_1.0.0 · Azure/azure-sdk-for-js
go: azure-sdk-for-go/sdk/resourcemanager/deviceregistry at main · Azure/azure-sdk-for-go
python: azure-sdk-for-python/sdk/deviceregistry at main · Azure/azure-sdk-for-python
If someone knows how to resolve this, please let me know. thank you!

## answer
They are all ready. Mariana Rios had to update information in our end to recognize the package

# What's pending to consider "Onboard using Release Planner app..." item as done?

## question
In order to address CPEX onboarding item "Onboard using Release Planner app...", we have created release planner for our control and data plane APIs. Azure SDK Release Planner - Power Apps.
What's the criteria to consider this item as done in S360?
## answer
It does look like you onboarded. We just introduced new KPI approval automation last week. It looks like the automation processed the onboarding KPI, but marked it as N/A instead of Completed. I am trying to understand why and will let you know when we have it figured out. Either way you shouldn't be blocked. For the data and management plane KPIs, those will be automatically approved once you complete the release plans.
The KPI has been approved. It was the date that was set to N/A. You should be good to go.
# change the sdk release planner version

## question
Hi 
General
 is there a way to change the sdk release planner version for python sdk? The release planner says version 1.0.0 for our python sdk. however, that was our last GA release version, and our generated PR changelog shows it is actually 1.1.0 version we want to release. is there a way to change this version in our release planner? thanks 
javascript is also incorrect version:  (should be 1.1.0) and Go sdk is blank when it should be 2.0.0 from changelog:
```
## 2.0.0 (2025-10-23)
### Breaking Changes
```
## answer
the version of the packages reflects what currently exists on main on the languages repo,because your PR hasn't been merged, you are seeing the previous version,.NET was already merged, so you see the right version.


# SDK Validation - .NET

## question
I have an open PR inital genome api spec by samira-farhin · Pull Request #25097 · Azure/azure-rest-api-specs-pr that's currently failing on SDK Validation - .NET - PR. My latest changes to resolve SDK Validation in "tspconfig.yaml" are included below. After these updates, the SDK validation issues for Go and Java were resolved, but the C# issue still remains. I'm unsure what else needs to be addressed.
inital genome api spec by samira-farhin · Pull Request #25097 · Azure/azure-rest-api-specs-pr
inital genome api spec by samira-farhin · Pull Request #25097 · Azure/azure-rest-api-specs-pr
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
This has been fixed in azure-sdk-for-net, and will be synced to azure-sdk-for-net-pr during the day. Your spec is in private repo, after the sync rerun should resolve this. And meanwhile, .NET SDK validation is optional for your spec PR merge, you can ignore it for now.

# Permission to merge to RPSaaSMaster

## question
Hi 
General
,
 
Due to recent team reorganization I am now in charge of my team and I need to get permission to merge changes to RPSaaSMaster, e.g. I need to merge this PR - Small fix for Dsts Sci Groups by Alessar · Pull Request #24977 · Azure/azure-rest-api-specs-pr
 
Could you please help me with that?
 
Thank you
## answer
https://aka.ms/azsdk/access

# MSWB API spec removal

## question
The Azure Modeling and Simulation Workbench (MSWB) preview service has been retired, so I'm trying to remove its related API specs from the REST API specs repository.
PR Deleting 5 API specs for the deprecated MSWB service - RPSaaSDev by yochu-msft · Pull Request #2508… targets RPSaaSDev to delete 5 API specs.
I want to move forward with merging this PR without resolving the Swagger LintDiff failure since the specs are being removed.
'Next Steps to Merge' says "If you still want to proceed merging this PR without addressing the above failures, refer to step 4 in the PR workflow diagram." but 'PR workflow diagram' step 4 loops back that "Follow the instructions in the Next Steps to Merge comment." How should I merge the PR without fixing the failure?
cc Mick Zaffke
## answer
PRs to RPSaaSDev have no required checks, so you can merge this as-is.
If you need to remove this spec from another branch like public/main or private/RPSaaSMaster, you will need more approvals.


# Deprecation of PostgreSql Single Server and its SDK Guidance

## question
Hi, PostgreSql Single Server officially deprecated in March 2025 with support completely ending September 2025. Efforts these past few months have been made to migrate users to PostgreSql Flexible Server.
 
We wish to remove the operations associated with single server found here: https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/postgresql/Azure.ResourceManager.PostgreSql/src/PostgreSql
Want to confirm what is the exact process to remove the operations: 
Is official request needed?
Do we manually remove the files associated to the resource?
 
Thank you for the help.

## answer
You will need to update the libraries for all 5 languages, not just .NET, to support the deprecation / retirement and migration.  
 
For .NET, does the library currently contain operations for both the single server and flexible server?  If so, the REST API version(s) in question would need to be retired first, and then new SDK libraries would need to be generated.  
For some of the other languages (like Python, Go, and Java), were there separate libraries for single server and flexible server?
I'm looping in our Shanghai team for assistance - Josephine, Arthur, and Renhe.
 
Once we work out how the libraries map to the different service versions, the instructions on how to deprecate an entire library for each language are here: [Deprecating Azure SDKs](https://eng.ms/docs/products/azure-developer-experience/retirement/sdk-deprecation)

# Confirm deprecation workflow for HDInsightOnAks (Preview, usage = 0): API & SDK

## question
Hi team, 
HDInsightOnAks service is already in deprecation, which is Preview and currently has 0 usage. We need to retire both API and SDKs. After reviewing some discussions about Deprecation of PostgreSql, I’ve drafted the following workflow and would like the SDK team to confirm and help clarify a few questions.
Workflow and Questions:
1. Remove the HDInsightOnAks spec folder from azure-rest-api-specs
2. After removing the HDInsightOnAks API, send SDK deprecation email to Josephine
a) For SDK deprecation, do we only need to send an email? Is there a recommended email template?
b) For each language repo, should I manually remove the code, or will the SDK team handle the removal?
c) Is the final deprecation outcome to add a Deprecated annotation?
Please confirm and add any missing steps. Thanks!

## answer
Before you remove the specs folder, you need to make sure that any REST API docs on Learn are moved first:  Retiring Azure APIs.
 
Details on how to mark each SDK library as deprecated are here:  Deprecating Azure SDKs.  If you have already emails your service customers as part of the service deprecation, no separate email for SDKs is required.
 
Josephine can assist with coordinating on the removal of code (but you should be able to submit PRs after all other work is done).

# Official API documentation publishing

## question
General,
 
How do we generate official API documentation like below?
Azure REST API reference documentation | Microsoft Learn
 
 
For a new RP like below. Is this done by the SDK team?
 
[azure-rest-api-specs/specification/cdn/resource-manager/Microsoft.Cdn/Cdn/preview/2024-07-22-previe…](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/cdn/resource-manager/Microsoft.Cdn/Cdn/preview/2024-07-22-preview/edgeaction.json)

## answer
You can find information about this here: https://eng.ms/docs/products/azure-developer-experience/design/api-docs

# Problem with merging PR into target branch

## question
Hello team, 
 
I am having trouble merging my PR. It seems I am being blocked by breaking changes errors. After consulting with the breaking changes team, they say the pr pipeline must be bust. Can you please help on this.
PR: Enabling free services acquisition via APIs by adrianta · Pull Request #25230 · Azure/azure-rest-ap…
## answer
You are confusing two checks:
    Breaking Changes(Cross-Version) - Analyze Code
        implementation detail
        not required
    Breaking Changes(Cross-Version)
        required
        passing due to label
I suspect your PR cannot be merged, because your github account is missing one or more required permissions. https://aka.ms/azsdk/access
This must be public, not private: https://github.com/orgs/Azure/people?query=adrianta
Once your permissions are fixed, push a change to your PR (empty commit is fine) to rerun checks.

# Clarification on Folder Structure Alignment for CP and DP SDKs

## question
Hi 
General
,
 
Our CP SDKs for both .NET and JavaScript are currently placed under the playwright folder. 
js->azure-sdk-for-js/sdk/playwright at main · Azure/azure-sdk-for-js
net->azure-sdk-for-net/sdk/playwright at main · Azure/azure-sdk-for-net
while the non-Azure (DP) SDKs are placed under the loadtesting folder.
js->azure-sdk-for-js/sdk/loadtesting at main · Azure/azure-sdk-for-js
net->azure-sdk-for-net/sdk/loadtestservice at main · Azure/azure-sdk-for-net
 we wanted to confirm:
Is it important or recommended to move the DP SDKs under the playwright folder to align with the CP SDK structure or the current structure is fine?
If yes, could you please guide us on the recommended process or steps to make this shift (e.g., repo structure changes, PR approach, or naming conventions to follow)?
Context:
As part of the retirement of Microsoft Playwright Testing (MPT) and its merger into Azure App Testing, we deprecated the old SDK packages associated with MPT and created new alternative packages about whose folder structure I mentioned above.
MPT’s core web testing capabilities are being integrated into Azure App Testing to provide a unified experience for both Load Testing and Playwright-based web testing. The standalone MPT service will be retired, and customers will need to migrate to Azure App Testing.
Qiaoqiao Zhang Please help us with this.
Thanks
cc Madhuri 

## answer
I think this should be a cross-language issue as in your tspconfig, the service-dir is sdk/playwrite which means all languages will generate the code under sdk/playwript. So you should update it in your tspconfig first. Then we can move the generated package into sdk/loadtesting folder in sdk repo
tspconfig:
```
parameters:
  "service-dir":
    default: "sdk/playwright"
```
For Java, I see you already had a GA release in
https://github.com/Azure/azure-sdk-for-java/tree/main/sdk/playwright/azure-resourcemanager-playwright
 
If this is your SDK, please do not modify the directory on Java. If you update the service-dir in parameters, please add a line
```
service-dir: sdk/playwright
```
under "@azure-tools/typespec-java":
 
Generally, diretory in Java repo follows your package name and namespace, which both is playwright. Unless your package be e.g. azure-resourcemanager-loadtesting-playwright, the folder won't be loadtesting.

# Error running "npx prettier": Cannot find module 'prettier/plugins/estree'

## question
$  npx prettier --write specification/maps/data-plane/Weather/stable/1.1/weather.json
[error] Cannot find module 'prettier/plugins/estree'
[error] Require stack:
[error] - C:\github\azure-rest-api-specs\eng\scripts\prettier-swagger-plugin.js
[error] - C:\github\azure-rest-api-specs\node_modules\prettier\index.js
[error] - C:\github\azure-rest-api-specs\node_modules\prettier\cli.js
[error] - C:\github\azure-rest-api-specs\node_modules\prettier\bin-prettier.js

## answer
you should't need to install prettier yourself in any way.  Just run npm ci from the specs repo root.
 
If you've installed prettier globally, using either npx prettier or npm install -g prettier, they might interfere.  I believe npm/npx are supposed to handle this, and prefer the local install from the specs repo, but maybe that wasn't happening on your dev machine.

# Error writing to PR in RPSAASDev branch

## question
I'm seeing an error when trying to push my changes to a branch off of RPSaaSDev. Has anyone seen this before or would it be a sign that my write access request (Azure SDK Partners on CoreIdentity) is still propagating?  
pushing to this PR:[Private.DeviceRegistry 2026-04-01 by yijinglu-microsoft · Pull Request #25274 · Azure/azure-rest-ap…](https://github.com/Azure/azure-rest-api-specs-pr/pull/25274/files)
## answer
GitHub is experience an outage on all HTTP and SSH git operations, please check status using https://www.githubstatus.com/.


# Override description property in referenced parameter.
## question
In this PR I need to override the description for the typeahead parameter for a specific API, so that all other API that reference this property will not be affected: 
 
 
But this is throwing an error:
 
error: Schema violation: must NOT have additional properties (paths > /search/address/{format} > get > parameters > 4)
  additionalProperty: description
 
 
I don't know why it is throwing an error, it worked in a similar situation for the BoundingBoxCompassNotation object in PR38554
## answer
You should be able to workaround this be slightly refactoring your swagger, for example create a new parameter TypeaheadDeprecated with the new description.

# doc only update?
## question
Hey team,
 
I'm a PM on the VMSS team. I'm trying to understand the new Typespec way to do a documentation only update to swagger? In this case, I just want to update these doc strings to remove/modify the preview notes. I believe I updated the correct text in the models.tsp files. The swagger compiled correctly. Now my questions:
 
I want this to be updated in current documentation posted online, as well as SDK docs. How do I submit this change against main or the latest feature branch? Do I need to do additional work to make sure this change is propagated through to SDKs?
When I submitted the PR, got this error. Not sure how to fix: "The default tag contains multiple API versions swaggers."
 
 
The PR for reference:
 
[VMSS prioritizeunhealthyvm and force delete doc update by fitzgeraldsteele · Pull Request #25785 ](https://github.com/Azure/azure-rest-api-specs-pr/pull/25785)

## answer
Swagger and TSP must always match.  So the same PR, but to public/main instead of private/main.The error from "Swagger Avocado" is an existing problem in your spec, that should be fixed eventually.  See this run from a previous PR to your spec.
 
https://github.com/Azure/azure-rest-api-specs/actions/runs/18538687430
 
Docs:
 
https://github.com/Azure/azure-rest-api-specs/wiki/Swagger-Avocado#multiple_api_version


# Do ARM SDKs support OData filtering like the APIs do?

## question
For list operations in our resource provider, we know we can perform filtering, sorting and searching using query parameters ($filter, $sort, $search) in our  API calls, and we've implemented the backend support for these queries already.
 
Is it possible to do the same sort of server side filtering (or generally add logic to include query parameters with certain calls) with our Azure SDKs as well?
## answer
ARM SDKs do support OData‑style query parameters (such as filter, orderby, search, top, skip, select, and expand), but only if your API spec explicitly defines them using TypeSpec or OpenAPI models like Azure.Core.FilterQueryParameter and OrderByQueryParameter. The SDKs don’t add these automatically or treat them specially—they simply expose whatever query parameters your spec declares, following Azure API Guidelines (camelCase names, not $filter). Both C# and Python treat these as normal query parameters, and paging behaviors reinject parameters as needed. In short: define the parameters in your spec, and the SDKs will fully support them.
here is an example of the paging pattern in C#
```
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;

namespace Azure.Analytics.OnlineExperimentation
{
    internal partial class OnlineExperimentationClientGetMetricsAsyncCollectionResult : AsyncPageable<BinaryData>
    {
        private readonly OnlineExperimentationClient _client;
        private readonly int? _top;
        private readonly int? _skip;
        private readonly int? _maxpagesize;
        private readonly RequestContext _context;

        /// <summary> Initializes a new instance of OnlineExperimentationClientGetMetricsAsyncCollectionResult, which is used to iterate over the pages of a collection. </summary>
        /// <param name="client"> The OnlineExperimentationClient client used to send requests. </param>
        /// <param name="top"> The number of result items to return. </param>
        /// <param name="skip"> The number of result items to skip. </param>
        /// <param name="maxpagesize"> The maximum number of result items per page. </param>
        /// <param name="context"> The request options, which can override default behaviors of the client pipeline on a per-call basis. </param>
        public OnlineExperimentationClientGetMetricsAsyncCollectionResult(OnlineExperimentationClient client, int? top, int? skip, int? maxpagesize, RequestContext context) : base(context?.CancellationToken ?? default)
        {
            _client = client;
            _top = top;
            _skip = skip;
            _maxpagesize = maxpagesize;
            _context = context;
        }

        /// <summary> Gets the pages of OnlineExperimentationClientGetMetricsAsyncCollectionResult as an enumerable collection. </summary>
        /// <param name="continuationToken"> A continuation token indicating where to resume paging. </param>
        /// <param name="pageSizeHint"> The number of items per page. </param>
        /// <returns> The pages of OnlineExperimentationClientGetMetricsAsyncCollectionResult as an enumerable collection. </returns>
        public override async IAsyncEnumerable<Page<BinaryData>> AsPages(string continuationToken, int? pageSizeHint)
        {
            Uri nextPage = continuationToken != null ? new Uri(continuationToken) : null;
            while (true)
            {
                Response response = await GetNextResponseAsync(pageSizeHint, nextPage).ConfigureAwait(false);
                if (response is null)
                {
                    yield break;
                }
                PagedExperimentMetric result = (PagedExperimentMetric)response;
                List<BinaryData> items = new List<BinaryData>();
                foreach (var item in result.Value)
                {
                    items.Add(ModelReaderWriter.Write(item, ModelSerializationExtensions.WireOptions, AzureAnalyticsOnlineExperimentationContext.Default));
                }
                yield return Page<BinaryData>.FromValues(items, nextPage?.AbsoluteUri, response);
                nextPage = result.NextLink;
                if (nextPage == null)
                {
                    yield break;
                }
            }
        }

        /// <summary> Get next page. </summary>
        /// <param name="pageSizeHint"> The number of items per page. </param>
        /// <param name="nextLink"> The next link to use for the next page of results. </param>
        private async ValueTask<Response> GetNextResponseAsync(int? pageSizeHint, Uri nextLink)
        {
            HttpMessage message = nextLink != null ? _client.CreateNextGetMetricsRequest(nextLink, _top, _skip, _maxpagesize, _context) : _client.CreateGetMetricsRequest(_top, _skip, _maxpagesize, _context);
            using DiagnosticScope scope = _client.ClientDiagnostics.CreateScope("OnlineExperimentationClient.GetMetrics");
            scope.Start();
            try
            {
                return await _client.Pipeline.ProcessMessageAsync(message, _context).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }
    }
}
```


# Failed to join MicrosoftDocs organization and can not open portal
## question
When I try to join MicrosoftDocs with link `repos.opensource.microsoft.com/orgs/MicrosoftDocs`, I can see nothing on the page, and when I open the portal Open Source Management Portal, the page is also white
## answer
The site is currently down. You will need to wait for it to be fixed by the owning team (which is not the Azure SDK team).

# SDK release help
## question
It seems that channel is not actively monitored thus move it here 
Ryan Zhang: .NET SDK is not generated | Azure SDK > SDK release support | Microsoft Teams
## answer
Log from the pipeline:
 [SGS-WARN] SDK configuration is not enabled for specification-containerservice-Fleet.Management-tspconfig.yaml. Refer to the full log for details.
.NET is missing in your tspconfig.yaml => azure-rest-api-specs/specification/containerservice/Fleet.Management/tspconfig.yaml at d1319a40758b…
You can use azure-rest-api-specs/specification/widget/resource-manager/Microsoft.Widget/Widget/tspconfig.yaml a… as a template

# azure-rest-api-specs-pr/InternalARMContracts
## question
Is this branch getting all changes automatically merged from main, which would itself get merged changes from azure-rest-api-specs?
 
It looks that way but then
Pull requests · Azure/azure-rest-api-specs-pr
 
I'm a bit puzzled about why we should need a PR like this (if that is the right thing to do)
Update redis folder from main to internal branch by JimRoberts-MS · Pull Request #25870 · Azure/azu…
## answer
Individual specs are not synced between any of our branches.  There is a process to sync the infrastructure, but not the specs themselves, from public/main to these branches in specs-pr:
 
RPSaaSMaster
RPSaaSDev
RPSaaSCanary
ARMCoreRPDev
InternalARMContracts
 
Syncing specs between these branches is the responsibility of the spec owner.
 
https://github.com/Azure/azure-sdk-tools/blob/813e623459f3e8c47df98ce1196581510f65ffe6/eng/pipelines/mirror-repos.yml#L34-L61


# Adoption of CBOR (and COSE) encoding/decoding throughout SDKs
## question
Hi, we are adopting CBOR in multiple places in the company, especially in the context of COSE signing envelopes, their verification.
 
CBOR and COSE encoding/decoding is supported natively in .NET SDK via System.Security.Cryptography.Cose already, and is being used in at least one SDK which follows specific RFC drafts.
 
However, other languages would need to also adopt it when the service expands. Which poses a question what libraries to use in each? I know specific dependencies that we can leverage but is there an approval process to "allow" them to become part of the SDKs, or should I just include the dependencies and review those in arch/sdk meeting for each language separately?
## answer
I would suggest an email to azsdkarch@microsoft.com to discuss this.

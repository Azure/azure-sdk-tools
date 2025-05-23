# Start to see ARM Incremental TypeSpec(Preview) check fail in InternalARMContracts branch

## Question 
Hi 
TypeSpec Discussion
 team,
 
I am doing some test in InternalARMContracts branch [[Test][InternalARMContracts] Add canonical swagger by dinwa-ms · Pull Request #21917 · Azure/azure-rest-api-specs-pr](https://github.com/Azure/azure-rest-api-specs-pr/pull/21917). I started to see "ARM Incremental TypeSpec(Preview)" check failure in my PR, which was successful earlier [[Testing][InternalARMContracts] Update contosowidgetmanager with canonical · Azure/azure-rest-api-specs-pr@d35e2fc](https://github.com/Azure/azure-rest-api-specs-pr/actions/runs/13794702570/job/38583331217).
 
Error: Unhandled error: Error: Command failed: git -c core.quotepath=off ls-tree -r --name-only HEAD^:specification/contosowidgetmanager/resource-manager/Microsoft.Contoso
78fatal: Not a valid object name HEAD^:specification/contosowidgetmanager/resource-manager/Microsoft.Contoso
 
Looks like they are running different cmd. Can someone help me with this? Thanks
![alt text](image-2.png)

## Answer
these are out-of-sync:
![alt text](image-3.png)

# Seeking Guidance on Defining ResourceStatusCode in TypeSpec

## Question 
Hello TypeSpec Discussion
I am working on defining a `ResourceStatusCode` in TypeSpec, which is similar to HTTP status codes but specific to resource states. I would appreciate your guidance on the following:
1. Should I use an `enum` or a `int` to define the `ResourceStatusCode`?
2. What are the best practices for defining status codes in TypeSpec?
3. How can I ensure that the `ResourceStatusCode` remains extensible for future updates?
I want to add statuses like:
- `NotSpecified: 204 No Content` - This indicates that the request was successful, but there is no content to return.
- `Pending: 102 Processing` - This indicates that the server has received and is processing the request, but no response is available yet.
- `Running: 202 Accepted` - This indicates that the request has been accepted for processing, but the processing has not been completed.
- `Succeeded: 200 OK` - This indicates that the request has succeeded.
- `Failed: 500 Internal Server Error` - This indicates that the server encountered an unexpected condition that prevented it from fulfilling the request.
Thank you for your assistance.

## Answer
As per one of our previous understanding, we defined similarly as an open union: [azure-rest-api-specs-pr/specification/impact/Impact.Management/connectors.tsp at RPSaaSMaster · Azure/azure-rest-api-specs-pr](https://github.com/Azure/azure-rest-api-specs-pr/blob/RPSaaSMaster/specification/impact/Impact.Management/connectors.tsp#L88-L94).  This explicitly allows any string value.
 
Generally, the reason for doing this is that you think additional values will be enabled in future versions (or even in this version).  Note that, if you do not make this an open union, then adding any values in any future api-version would be a breaking change (which is why this is recommended).
 
There are RPaaS extensions for validation that would allow you to reject requests for values that are not valid.
 
# change empty {} to record<unknown>

## Question 
TypeSpec Engineering/TypeSpec Spec PRs/TypeSpec Discussion
 
we currently have a working version with a property declared as empty object, we started process generating sdks for this version and got feedback from sdkteam  saying empty object needs to be converted to `Record<unknown>` for sdk generation to work for our needs
 
additionalProperties?: {}
to 
 additionalProperties?: `Record<unknown>`;
 
we created this PR [change empty obj to record<unknown>, sdk regenartion works better thi… by adityareddy305 · Pull Request #33352 · Azure/azure-rest-api-specs](https://github.com/Azure/azure-rest-api-specs/pull/33352/checks?check_run_id=39125235332), which is leading to typespec validation failure
 
workloadImpacts.tsp:64:3 - error @azure-tools/typespec-azure-resource-manager/arm-no-record: Model properties or operation parameters should not be of type Record. ARM requires Resource provider teams to define types explicitly.
> 64 |   additionalProperties?: `Record<unknown>`;
     |   ^^^^^^^^^^^^^^^^^^^^
 
Found 1 error.

 
how can we fix this issue  or suppress this?

## Answer
if this is indeed a record object where the values can contain anything and this passed Arm review then you can suppress the warning [https://typespec.io/docs/language-basics/directives/#suppress](https://typespec.io/docs/language-basics/directives/#suppress)

# Swagger LintDiff and TypeSpec API View Errors in PR

## Question 
hi 
TypeSpec Discussion, 
Getting Swagger LintDiff failures from today morning after taking the latest updates from main 

PR - [Update Public Repo with Liftr Neon Preview Versions from Private Repo by alluri02 · Pull Request #33311 · Azure/azure-rest-api-specs](https://github.com/Azure/azure-rest-api-specs/pull/33311)
![alt text](image-4.png)
TypeSpec api view is passing locally 
![alt text](image-5.png)
Kindly look into the issue and let me know if I'm missing anything.

## Answer
Check "Swagger LintDiff (Staging)" is under development, not a required check, and should be ignored.  Check "Swagger LintDiff" is required, and the only one you should pay attention to.
![alt text](image-6.png)
We thought that adding the suffix "(Staging)" to the check name, and making the check non-required, would be sufficient to inform PR authors the check should be ignored.  But it doesn't seem to have worked this time.  We also tried the suffix "(Preview)" without success.
 
Is there another way we could name our checks that are "in development" and "the results should be ignored", that would make it clear to a spec author the check (and any failures) should be ignored, without needing to contact us?
 
How about this?
`[TEST-IGNORE] Swagger LintDiff`

# Notice of update of services returning bytes

## Question 
Latest upgrade of TypeSpec changed the default interpretation of a body of `bytes` without an explicit content type to assume `application/octet-stream` from previously being a json string of base64 encoded data
```
op getFile(): bytes;
```
There were 3 services that add this pattern and the swagger was just updated to reflect the behavior: 
- OpenAI.Assistant
- Microsoft.App.DynamicSessions
- AzureOpenAI/inference
![alt text](image-7.png)
Creating this post as a notice and pinging service teams affected to get confirmation that the spec was actually incorrect before. If it was indeed correct before (those apis returned base64 json string and not the raw file) then we can go back and add the explicit `@header contentType: "application/json"` to revert back.
TypeSpec Engineering Weidong Xu not sure where to find the service owner to ping if anyone knows and can add them to this post
## Answer
Context for Java SDK update (PR not merged) on this (affect "azure-ai-openai" and "azure-ai-openai-assistants")
https://github.com/Azure/azure-sdk-for-java/pull/44730#issuecomment-2742522605
Would appeciate input from service about the expected content-type 2 `getFileContent` API.

# What is `x-ms-long-running-operation-options` for LRO operation of data-plane when `emit-lro-options: none` in `@azure-tools/typespec-autorest`?

## Question 
Many `tspconfig.yaml` files for data-plane have an option `emit-lro-options: none` for emitter `@azure-tools/typespec-autorest`, which means only emit `x-ms-long-running-operation` but does not emit`x-ms-long-running-operation-option` for resource providers, like the following loadtestservice `tspconfig.yaml`: [azure-rest-api-specs/specification/loadtestservice/LoadTestService/tspconfig.yaml at main · Azure/azure-rest-api-specs](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/loadtestservice/LoadTestService/tspconfig.yaml#L24)

So what exactly does this data-plane operation use when polling LRO request then? Can someone explain a little more about this situation of `emit-lro-options: none`? thanks

## Answer
This is just an emitter option for emission of OpenAPI from the spec.  It doesn't impact how other emitters view the LRO - the LRO is resolved based on the encoding of the operation.
 
Because it's nice to have a visual indicator that the lro is encoded correctly, it is highly encouraged that spec authors use no emit-lro-options seting, , or use `emit-lro-options: "all"` to check .  But this is not required for check-in, because the lro-options are a microsoft-specific extension with little or no documentary value to customers.

We don't generate data plane clients from OpenAPI if there is a corresponding TypeSpec, they are generated form TypeSpec directly.

# Type Spec review for Data plane API specs

## Question 
Hi TypeSpec Discussion, do we need review from type spec team before we could merge data plane API spec PR on github specs repo?

## Answer
All data plane API specs must be reviewed by the API Stewardship board.  Please create a release plan and then you can schedule a review.  [What is a release plan?](https://eng.ms/docs/products/azure-developer-experience/plan/release-plan)

# Naming of JS/TS emitters

## Question 
We need consistency and accuracy in our naming of client and server emitters for JS/TS for our 1.0-RC release. The client currently has 'ts' in the name and the server has 'javascript' in the name. Do these emitters actually generate TypeScript or is it just JavaScript? 
 
I know TS is syntactic sugar on top of JS, but I believe the distinction will matter to 3P devs who may rely on TypeScript type checking. 
## Answer
The renaming of the emitters to use js aligns with the broader trend where the distinction between JavaScript and TypeScript is becoming less important. Most developers expect JavaScript libraries to include TypeScript definitions, and using js in the name, even for TypeScript code, shouldn't cause confusion, especially since tools are available to strip TypeScript annotations. Both client and server emitters are now using js in their names for consistency.

# Support for @includeInapplicableMetadataInPayload decorator

## Question 
After pulling the latest changes, I been getting errors regarding "@includeInapplicableMetadataInPayload(false)" decorator not being supported anymore.
 
Is it not possible to make use of the decorator? If I removed the decorator from the model ts file, it changes the expected model definition for the API path on swagger.

## Answer
The decorator was moved to a private namespace in the March 2025 TypeSpec release, which may cause breaking changes if it's removed. The recommendation is to consider using existing Resource models like TrackedResource<T> to avoid this. If the spec is on the main branch, it should already have the necessary updates. Make sure to pull the latest changes and update the local compiler to avoid issues.

# Typespec Validation issue

## Question 
I've a PR where the Typespec validation is failing with some weird error. This is not being reproduced locally - 
```
  specification/storagedatamanagementrp/Private.StorageDataManagement.Management/main.tsp:17:10 - error expect-value: Is a model expression type, but is being used as a value here. Use #{} to create an object value.
53  > 17 | @service({
54       |          ^
55  > 18 |   title: "Storage Data Management Resource Provider",
56       | ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
57  > 19 | })
58       | ^^
```
```
specification/storagedatamanagementrp/Private.StorageDataManagement.Management/Connectors/connectorModels.tsp:19:15 - error invalid-argument: Argument of type '"read"' is not assignable to parameter of type 'valueof EnumMember'
60  > 19 |   @visibility("read")
61       |               ^^^^^^
```
I'm not sure how to resolve this error. Could someone please help here?
[Update Connector and DataShare Swagger by ujjawaljain-msft · Pull Request #21997 · Azure/azure-rest-api-specs-pr](https://github.com/Azure/azure-rest-api-specs-pr/pull/21997)

## Answer
You may need to update to the latest typespec version - some of these particular properties have changed in a recent version. What you are using here isn't the latest style to specify these things.
Doc for future reference: [https://github.com/Azure/azure-rest-api-specs/wiki/TypeSpec-Validation#running-locally](https://github.com/Azure/azure-rest-api-specs/wiki/TypeSpec-Validation#running-locally)

# How do I remove a parameter?

## Question 
Hi! I'm converting my Go SDK that's a brown-field service to generate with typespec, and I need to remove an optional parameter. I'm struggling to figure out how to do it though.
 
I've tried changing the access, but it fails to generate:
![alt text](image-8.png)
![alt text](image-9.png)
I also tried changing the visibility:
![alt text](image-10.png)
![alt text](image-12.png)
I'm pretty new to typespec, so I don't know all the decorators. What's the recommended way to get a parameter not to generate in the SDK? Thank you!

## Answer
Currently, TCGC does not support excluding parameters from SDK generation. The best approach for now is to either mark the entire operation or model as internal access or use the `@access` decorator to make parameters private. However, TCGC’s `@override` decorator, while useful, doesn't currently support removing parameters and is primarily intended for option bags. There is no active GitHub issue for this specific use case, but one could be filed.

# Control Plane trafficManagerProfiles endpointName and endpointType.

## Question 
Hi Team,
I had a question regarding generating an endpoint for the trafficManagerprofiles which needs to look like this-
```
"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/trafficmanagerprofiles/{profileName}/{endpointType}/{endpointName}"
```
I tried creating resources/interfaces for this as below-
![alt text](image-13.png)
But when I try to generate the openapi specs, the endpoint generated looks like this-
"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/trafficmanagerprofiles/{profileName}/{endpointName}/{endpointName}"
![alt text](image-14.png)
Can someone please guide me in this situation?
Thanks in advance

## Answer
To model your legacy Traffic Manager API correctly with TypeSpec, especially given the constraints of legacy ARM APIs, you'll need to use lower-level mechanisms. Since TypeSpec supports various types of APIs, you can describe your CRUD operations over child resources like endpoints, but standard templates may not apply to your scenario. Instead, using legacy templates might help, although you won’t be able to auto-generate certain components like ServiceRoutes or Controllers due to the lack of support in the ResourceManager library.

As for the ProxyResource vs. TrackedResource question, both are valid options, but TrackedResource is likely the better choice unless you have specific reasons to use ProxyResource. If needed, I can provide further templates and guidance to assist with the conversion.

# Annotate same model with SubscriptionLocationResource and ResourceGroupLocationResource

## Question 
1.We have added a new SubscriptionLocationResource named "ValidatedSolutionRecipe", as per the typespec docs to our RP - Microsoft.AzureStackHCI. Here is the typespec for this resource
2.This is a proxy resource and the URL path for this resource looks like "/subscriptions/921d26b3-c14d-4efc-b56e-93a2439e028c/providers/Microsoft.AzureStackHCI/locations/eastus/validatedSolutionRecipes/10.2502.0?api-version=2023-12-01-preview"
As above API is a subscription level API, the clients of the API need to have subscription level RBAC. Due to security requirements, we need to have a similar API, but at resource group scope.
1.From the typespec docs and our prototyping, we see that we can achieve this by havning a ResourceGroupLocationResource.
2.However, same model in typespec can't be annotated with both SubscriptionLocationResource and ResourceGroupLocationResource. When we do so, the generated swagger only has either subscription level paths or resourcegroup level paths, depending on which annotation is first on the model.
3.Thus, to work around this, we had to introduce a new resource type with an undesirable name - "ResourceGroupValidatedSolutionRecipe".
But it is the same resource. Just because of the limitation of not being able to support both SubscriptionLocationResource and ResourceGroupLocationResource, we have to create a new model with an undesirable name - "ResourceGroupValidatedSolutionRecipe".
 
Please help us and let us know how can we utilize the model with same name (i.e. the same resource type) for both of the above APIs.

## Answer
You’re essentially dealing with two distinct resources in ARM due to their different sets of operations at the subscription and resource group levels. While they might have identical properties, ARM requires them to be treated as separate resource types, and thus, they need separate registrations and operations.

To simplify the model and avoid confusion for customers, you could potentially use a custom operation or adjust your approach for handling these two resource types. If both locations expose the same GET operation and are largely the same resource, using a single model might be possible with custom operation handling. However, ARM likely won’t allow this without separate registrations due to the operational differences.

In SDKs, this could create confusion, as the variance between the two APIs might not be intuitive. It’s important to consider customer use cases, such as whether they need to transfer resources between locations or share code between these two types. A shared type could work if the operations align, but it's crucial to decide whether the benefits of exposing this as a single resource outweigh the potential complexity.

# Inconsistent access modifiers in C# generated code

## Question 
Hi,
I used to generate the code for Azure.AI.Projects. In the code we have [upload file method](https://github.com/Azure/azure-rest-api-specs/blob/366f7e94f14f6c2a4af81b9b0b6726751de97da7/specification/ai/Azure.AI.Projects/agents/files/routes.tsp#L57C6-L57C20), which takes in the file contents. In C# @multipartBody will be translated into the internal class UploadFileRequest, and as a result, we will end up with the public method UploadFile, using internal class, resulting in compilation error. Is it a regression in the new compiler?
## Answer
You might want to ask this in the dotnet channel as well: [Azure SDK | Language - DotNet | Microsoft Teams](https://teams.microsoft.com/l/channel/19%3A7b87fb348f224b37b6206fa9d89a105b%40thread.skype/Language%20-%20DotNet?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47)

# Convert ARM OpenAPI to UserRP

## Question 
Hi team, when using @armResourceOperations decoration it generates ARM openapi spec but I need the UserRP spec eventually, is there a tool that currently does it? I know ProviderHub template has the ability to generate UserRP API but wondering if there's a separate tool that does only openapi transformation so that it gives flexibility to transform it to APIs of different languages. 

## Answer
Currently, there isn’t a dedicated tool for transforming ARM OpenAPI specs into UserRP API specs, although this functionality could be developed. If you’d like to request this feature, you can file an issue on the Azure TypeSpec GitHub repository here. However, there are currently around 300 open issues, so it’s unlikely that this request will be picked up soon.

# TypesSpec PR channel is locked to moderator

## Question 
Is that intended?
It doesn't make much sense as a support channel
![alt text](image-16.png)
![alt text](image-17.png)
## Answer
That channel is deprecated, and replaced with this channel.  I archived the channel and updated its description.

# How to restrict importing typespec files in main based off of versions.

## Question 
Hi Team,
We have 2 imports in our main.tsp to include other resource type tsp files, however we do not want to include one of the resourcetype to the new api version we want to introduce.  Is there a way to do conditional imports based off of version in main?
 
Thank you in advance!

## Answer
no, you have to mark the models/types and everything that you want to remove with the `@removed` decorator using the versioning library
 
if you are in preview version I think also the policy is to only have a single preview version in the spec repo at the timme now so you could also just delete it

# Issues with Specs PR for GA release | Needed urgent help.

## Question 
Hi Folks:
We have a GA dates coming, can someone take a look at this PR, i have raised separate issues in each language and generation is failing, model validation and other validations have passed, not sure what we are missing. Please help!
https://github.com/Azure/azure-rest-api-specs/pull/32911/checks?check_run_id=38252615436

## Answer
The issue arises because the Go SDK emitter and other client emitters have not yet been updated to the latest TypeSpec version, which includes breaking changes to some decorators. The ideal solution is to wait for the client emitters to update, but if time is critical, workarounds like using the old syntax with a suppression are available, though this will require a future update.

There is also a planned GA release on March 31st, and the user is seeking help to meet this deadline. Additionally, there is an issue related to the ARM version in the PR, where a regex pattern needs to be adjusted, and a breaking change for the Go SDK was introduced in the latest preview. The PR has been signed off, but the regex change is necessary, which will trigger a re-review.

# Brownfield TypeSpec migration

## Question 
Hi, Is there a timeframe for existing brownfield RPs to move from OpenAPI swagger to TypeSpec.  Is it possible to mix TypeSpec with handwritten swagger and migrate in phases. Say for example migrate one resource type at a time to minimize risk. 

## Answer
Migration to TypeSpec for existing services is not yet mandatory, but it is suggested, and teams should be planning for it in Bromine and Krypton
Services must wholly switch to TypeSpec, there is no allowed mixing of hand-written and generated swagger
Servicesmust conform to a single, unified api-version for their service, servicesthat currently use different api-versions for parts of their service are going to need to plan for conformance -this either means SDK splitting or version uniformity.  Teams that use this 'different api-versions for different resources in the same sdk' pattern are not good candidates for conversion at the moment
In generally, the more compliant your service is to the RPC and best practices, the easier conversion will be
There is documentation on converting here: [Getting started | TypeSpec Azure](https://azure.github.io/typespec-azure/docs/migrate-swagger/01-get-started/)

We highly encourage you do the migration. Any problem related to https://azure.github.io/typespec-azure/docs/migrate-swagger/01-get-started/, don't hesitate to reach out to me.

# Multi-level inheritance in TSP

## Question 
Hi I have this hierarchy in Model:
 
JobProperties --> ChildJobProperties --> RecoveryChildJobProperties:
 
Base
![alt text](image-18.png)
Level1 inheritance:
![alt text](image-19.png)
This yields an error, as in Level1 derived Ive set a value on the objectType discriminator field, and then setting a value again in L2 derived object.
![alt text](image-20.png)
Ive tried a lot of variants:
 - have 2 disciminators first in base, second introduced in Level1
- Use diff values in discriminator in all levels 
-etc
 
Nothing seems to work. So whats the right way for multi-level inheritance with discriminators ?

## Answer
TypeSpec does not currently support multi-level inheritance with multiple discriminators, as this pattern is incompatible with its type system. Azure SDKs specifically advise against using this pattern.

If your goal is property reuse, consider spreading base properties instead of using inheritance. However, if you need different job types (like Job, ChildJob, and RecoveryJob), the recommended approach is to use a base model like Job that does not specify a "kind" and then use discriminators for the derived types like ChildJob and RecoveryJob, which would specify a kind property. This avoids the need for nested inheritance.

Nested discriminated inheritance is not supported and will not be added to TypeSpec in the near future.

# New attribute is not getting added in the Swagger for new version

## Question 
hi TypeSpec Discussion, 
i added new attribute for new version in one of the existing model , but the new attribute is not getting relected in the swagger json 
 
TypeSpec changes - 
https://github.com/Azure/azure-rest-api-specs-pr/blob/4565dbc00d85dfaf734bb8e41d4f778625ea66f9/specification/liftrneon/Neon.Postgres.Management/LiftrBase.Data/main.tsp#L32C2-L34C62
 
Swagger json:
[azure-rest-api-specs-pr/specification/liftrneon/resource-manager/Neon.Postgres/preview/2025-02-01-preview/neon.json at RPSaaSMaster · Azure/azure-rest-api-specs-pr](https://github.com/Azure/azure-rest-api-specs-pr/blob/RPSaaSMaster/specification/liftrneon/resource-manager/Neon.Postgres/preview/2025-02-01-preview/neon.json#L3240C4-L3270C9)

## Answer
it's because you are versioning this nested library
 
1.you shouldn't do that as you are finding out now it is super confusing 
2.the problem is that you say you are using v1 of your library in your spec

# Typescript DPG encode int64 as string in headers

## Question 
Hi DPG TypeScript, we recently have this change to our TypeSpec: [Batch: Encode all long types as strings by skapur12 · Pull Request #32584 · Azure/azure-rest-api-specs](https://github.com/Azure/azure-rest-api-specs/pull/32584/files) to encode all long types as strings in our rest API.
 
After regenerating the RLC, I found the following changes:
In header: `content-length` changed from string to number.
![alt text](image-21.png)
In the return body: fields change from number to string.
![alt text](image-22.png)
Since I believe RLC is doesn't have special deserialization logic, the`content-length` header interface should have string type instead of `number`?

## Answer
There is a bug on the emitter side that causes issues with deserializing the @encode("string") decorator in response headers, tracked under GitHub issue #3090. The issue has been acknowledged, and ZiWei Chen is looking into it. The fix has been prioritized as a P0 task by Mary Gao.

# Facing issues with ApiCompat in .NET while generating package

## Question 
TypeSpec Discussion
, DPG C#:
I am from Azure Load Testing and we are migrating to Typespec DPG, after our earlier SDK versions were generated from Swagger/AutoRest flow.
I was able to generate the package and build the code as well but while packaging, ApiCompat target is giving me errors like:
```
C:\Nuget\microsoft.dotnet.apicompat\5.0.0-beta.20467.1\build\Microsoft.DotNet.ApiCompat.targets(82,5): error : TypesMus
tExist : Type 'Azure.Developer.LoadTesting.LoadTestingClientOptions' does not exist in the implementation but it does e
xist in the contract. [D:\Work\SDK_Generation\azure-sdk-for-net\sdk\loadtestservice\Azure.Developer.LoadTesting\src\Azu
re.Developer.LoadTesting.csproj::TargetFramework=netstandard2.0]
C:\Nuget\microsoft.dotnet.apicompat\5.0.0-beta.20467.1\build\Microsoft.DotNet.ApiCompat.targets(82,5): error : MembersM
ustExist : Member 'public void Azure.Developer.LoadTesting.LoadTestRunClient..ctor(System.Uri, Azure.Core.TokenCredenti
al, Azure.Developer.LoadTesting.LoadTestingClientOptions)' does not exist in the implementation but it does exist in th
e contract. [D:\Work\SDK_Generation\azure-sdk-for-net\sdk\loadtestservice\Azure.Developer.LoadTesting\src\Azure.Develop
er.LoadTesting.csproj::TargetFramework=netstandard2.0]
```
I have also ran the Export-API.ps1 script and ensured the generated API files contain the new members, but I am still getting these errors. Is there any guidance or doc on how I should resolve them?

## Answer
The issue arises from a breaking change where the client options class was renamed due to the lack of a library-name specification in the tspconfig.yaml. To fix this, the recommended solution is to either:

Update the tspconfig.yaml to specify library-name: "AzureLoadTesting", which will preserve the desired client options name.

Update the customization file to match the new generated client options name (AzureDeveloperLoadTestingClientOptions).

Once this issue is resolved, the team can focus on any remaining errors related to response types or other minor issues.

# Using the Record type in a typespec model

## Question 
Hi, due to our service design we need to have a dictionary-like field in one of our models. So I've added a suppression for the arm-no-record violation. Will this cause problems during PR review in RPSaaSMaster and if so is there an alternative for Record? [Relevant line in code](https://github.com/Azure/azure-rest-api-specs-pr/pull/20696/files#diff-593178784810c64818e9c24df3ff3ffd7fd62fb40d3f0fc164a2423b90b3cbedR37)

## Answer
Have you already discussed the design with the ARM reviewers? Not following the prescribed patterns can cause lots of challenges down the road for everything that derives from your service spec. Being different has a cost. Is there a alternative service design that would not violate anything?

# ARM Parameters for Tenant level resource routing

## Question 
Hi, i want to add a new route for an existing RT, this is extension resource type that currently exists at subscription and RG level. we want to now create it at management group/service group. The route should look like this - "/providers/Microsoft.Management/serviceGroups/{servicegroupName}/providers/Microsoft.Edge/sites/{siteName}"
 
if i try to use existing ARM operations for tenant level resource i can only create something like this "provider/Microsoft.Edge/sites/{siteName}", if i use ARM extension resource parameter to include a {scope} tag as a suffix im not able to restrict the scope to only tenant level. 
 
For the above scenario i tried defining custom routes instead of ARM operations, in that case i see swagger spellcheck getting flagged as servicegroup is not recognised as a word. On checking the documentation i see that providing custom names in route might be getting blocked soon. 
link to the PR for more reference - [initial commit for adding tenant level routing by urjaBanati · Pull Request #20695 · Azure/azure-rest-api-specs-pr](https://github.com/Azure/azure-rest-api-specs-pr/pull/20695/checks?check_run_id=34978981295)
is there any existing operation that i can use to generate route specified above?
what is the recommended way to resolve the spellcheck error ?

## Answer
We are tracking specific management group support here: https://github.com/Azure/typespec-azure/issues/1840
 
However, if your resource is an extension resource, it should already be applicable to essentially any resource, including management groups.
 
We would like to have resources express a constraint over the resources that an extension can be applied to, which is tracked here: https://github.com/Azure/typespec-azure/issues/1270
 
But this shouldn't be required for OpenAPI or client generation at this point, it is just useful information for the consumer that we would like to support.

# x-ms-examples are not getting added without @Autorest.example decorator

## Question 
Hi Team, I'm currently having this below PR for onboarding a new RP
https://github.com/Azure/azure-rest-api-specs-pr/pull/20724
 
For some reason, x-ms-examples are not getting added without using @Autorest.example decorator but typespec validation is failing because of using this decorator. I tried to even follow this below doc but it is not really helping out and even I noticed that the example files are not getting copied to resource-manager folder.
https://azure.github.io/typespec-azure/docs/migrate-swagger/faq/x-ms-examples/
 
I would appreciate if I can get some help in fixing this.

## Answer
You don't need any decorator to add x-ms-examples. I saw your compiler version is still 0.59. Could you upgrade to latest version?
 
Just merge from latest code you will get latest package.json for latest compiler version.
 
# Customize path parameter

## Question 
Use this Employee example, the only change I made is adding a path parameter to listByResourceGroup like
```
listByResourceGroup is ArmResourceListByParent<Employee,
  Parameters = {
    /** doc */
    @path
    @segment("locations")
    location: string;
  }>;
```
Now you will see the route becomes /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.ContosoProviderHub/locations/{location}/employees
 
Please correct me if I'm wrong:
I assume the resource type for this route is Microsoft.ContosoProviderHub/locations/employees, which is different from the other operations, whose resource type is Microsoft.ContosoProviderHub/employees
I assume getArmResources will return all of the operations including listByResourceGroup for resource model Employee right? Something like 
```
operations:
    - resourceType: "Microsoft.ContosoProviderHub/employees"
      operations: [get, update, delete, ...]
    - resourceType: "Microsoft.ContosoProviderHub/locations/employees"
      operations: [listByResourceGroup]
```
If that is the case, we implicitly add a new resource with another resource type, I feel like that not what TypeSpec should do.
 
Currently, .net SDK's behavior (swagger input) is listByResourceGroup operation doesn't belongs to Employee resource model.

## Answer
The issue reported arises from the current behavior in Swagger, where resource relationships are determined by "guessing" based on route lengths. The goal with TypeSpec is to allow explicit user input to define resource hierarchies, thus avoiding the guessing mechanism. The specific problem involves an operation that doesn't clearly fit into any resource model, and it's being speculated as a resource operation when it could be more like a list resource operation.

To resolve this, the user is asking for getArmResources (or resolveAzureResources) to retrieve the explicit resource information from the user, allowing the system to better understand and handle the relationships between resources and operations.

An issue has been opened on GitHub to address this need, which you can track [here](https://github.com/Azure/typespec-azure/issues/2043).

# Suppression review required for ManagedIdentityUpdate AnonymousTypes

## Question 
Hi Team,
 
Based on past discussions in this channel, we added suppression for ManagedIdentityUpdate (getting added in swagger as part ArmResourcePatchAsync in type spec) in this below PR, can we get sign off on this suppression pls?
https://github.com/Azure/azure-rest-api-specs-pr/pull/20724

## Answer
Left a com ent - it looks like you need to change the suppression string to match the definition name: [Add OnlineExperimentation/workspaces RP by sakoll · Pull Request #20724 · Azure/azure-rest-api-specs-pr](https://github.com/Azure/azure-rest-api-specs-pr/pull/20724#pullrequestreview-2535489831)

# Enforcing scope restrictions for extension resources

## Question 
Hi 
TypeSpec Discussion
, 
 
We are building a new extension resource under a new RP - Microsoft.StorageDataRP and require this resource to be strictly scoped to:
/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{accountName}
We would like requests with any other scope to be blocked by ARM/RPaaS and not reach our RP.
What is the recommended approach to enforce this restriction?

Note: Our RP is hosted on RPaaS.
Currently, I have modeled the extension resource as shown below, but I am unable to enforce the scope restriction:
```
@@path(ResourceUriParameter.resourceUri, "scope");
@@Azure.ResourceManager.CommonTypes.Private.armCommonParameter(
  ResourceUriParameter.resourceUri,
  "ScopeParameter",
  Azure.ResourceManager.CommonTypes.Versions.v5
);

@@doc(Azure.ResourceManager.ResourceUriParameter.resourceUri,
  "The scope of the operation or resource. Valid scopes is: resource (format: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{accountName}'"
);

@doc("A MountPoint is a tracked ARM resource modeled as a sub-resource of a Storage Account")
model MountPoint is ExtensionResource<MountPointProperties, false> {
  @doc("The name of the mount point")
  @pattern("^[a-zA-Z0-9-]{3,24}$")
  @key("mountpointName")
  @segment("mountPoints")
  @path
  name: string;
}
```

## Answer
The issue of assigning resource types for extension resources is recognized, and while it’s currently in the backlog, a possible solution is to use extension resources in TypeSpec. This allows you to attach resources to others, but restrictions on which resources can be used might require customizations.

Rather than customizing in TypeSpec itself, client SDK customizations are suggested. This would involve validating or parameterizing the resource IDs of the extended resources. For direct REST calls in UserRP, you would still need to handle these checks at the RP level if necessary.

You can refer to the documentation on extension resources in TypeSpec for more details on how to approach this. The team is encouraged to avoid extensive customizations in favor of maintaining the TypeSpec approach, but if needed, SDK-level customizations can provide flexibility.

# Typespec validation error

## Question 
Could I get help with the following error?   
```
azure-rest-api-specs-pr> npx tsv specification/azurestackhci/AzureStackHCI.StackHCIVM.Management
Running TypeSpecValidation on folder:  C:/repos/ArcVM/forked-magund/azure-rest-api-specs-pr/specification/azurestackhci/AzureStackHCI.StackHCIVM.Management

Executing rule: FolderStructure
folder: C:/repos/ArcVM/forked-magund/azure-rest-api-specs-pr/specification/azurestackhci/AzureStackHCI.StackHCIVM.Management
config files: ["C:/repos/ArcVM/forked-magund/azure-rest-api-specs-pr/specification/azurestackhci/AzureStackHCI.StackHCIVM.Management/tspconfig.yaml"]
azure-resource-provider-folder: "resource-manager"


Executing rule: NpmPrefix
run command:npm prefix
Expected npm prefix: C:/repos/ArcVM/forked-magund/azure-rest-api-specs-pr
Actual npm prefix: C:/repos/ArcVM/forked-magund/azure-rest-api-specs-pr

Executing rule: EmitAutorest
mainTspExists: true
emit: ["@azure-tools/typespec-autorest"]


Executing rule: FlavorAzure

Executing rule: LinterRuleset
azure-resource-provider-folder: "resource-manager"
files: ["main.tsp"]
linter.extends: ["@azure-tools/typespec-azure-rulesets/resource-manager"]

Executing rule: Compile
run command:npm exec --no -- tsp compile --warn-as-error C:/repos/ArcVM/forked-magund/azure-rest-api-specs-pr/specification/azurestackhci/AzureStackHCI.StackHCIVM.Management
TypeSpec compiler v0.63.0

Compilation completed successfully.

Running git diff on folder C:/repos/ArcVM/forked-magund/azure-rest-api-specs-pr/specification/azurestackhci

Executing rule: Format
run command:npm exec --no -- tsp format "../**/*.tsp"
run command:npm exec --no -- prettier --write tspconfig.yaml
TypeSpec compiler v0.63.0

tspconfig.yaml 28ms (unchanged)
Running git diff on folder C:/repos/ArcVM/forked-magund/azure-rest-api-specs-pr/specification/azurestackhci
Rule Format failed
{"not_added":[],"conflicted":[],"created":[],"deleted":[],"modified":["specification/azurestackhci/AzureStackHCI.StackHCIVM.Management/VirtualHardDisks.tsp"],"renamed":[],"files":[{"path":"specification/azurestackhci/AzureStackHCI.StackHCIVM.Management/VirtualHardDisks.tsp","index":" ","working_dir":"M"}],"staged":[],"ahead":61,"behind":7,"current":"users/magund/DiskResize","tracking":"origin/users/magund/DiskResize","detached":false}diff --git a/specification/azurestackhci/AzureStackHCI.StackHCIVM.Management/VirtualHardDisks.tsp b/specification/azurestackhci/AzureStackHCI.StackHCIVM.Management/VirtualHardDisks.tsp
index 820d986e90..cf7b02838f 100644
--- a/specification/azurestackhci/AzureStackHCI.StackHCIVM.Management/VirtualHardDisks.tsp
+++ b/specification/azurestackhci/AzureStackHCI.StackHCIVM.Management/VirtualHardDisks.tsp
@@ -47,10 +47,7 @@ interface VirtualHardDisks {
   @parameterVisibility
   @added(Versions.v2025_02_01_preview)
   @sharedRoute
-  update is ArmCustomPatchAsync<
-    VirtualHardDisk,
-    VirtualHardDisksUpdateRequest
-  >;
+  update is ArmCustomPatchAsync<VirtualHardDisk, VirtualHardDisksUpdateRequest>;

   @doc("The operation to delete a virtual hard disk.")
   delete is ArmResourceDeleteWithoutOkAsync<VirtualHardDisk>;

Files have been changed after `tsp format`. Run `tsp format` and ensure all files are included in your change.
```
Here's my PR https://github.com/Azure/azure-rest-api-specs-pr/pull/20812

## Answer
Looks like you just need to format the typespec. You can run `npx tsp format` for fix manually.
 
-  update is ArmCustomPatchAsync<
-    VirtualHardDisk,
-    VirtualHardDisksUpdateRequest
-  \>;
+  update is ArmCustomPatchAsync<VirtualHardDisk, VirtualHardDisksUpdateRequest>;

# TypeSpec APIView check failure: TSP directory doesn't exist

## Question 
Hello -- I'm trying to get a [TSP conversion PR](https://github.com/Azure/azure-rest-api-specs/pull/30239) merged for Key Vault, but I'm seeing a failure in the TypeSpec APIView check that I'm unsure how to deal with: [Pipelines - Run 20250108.90 logs](https://dev.azure.com/azure-sdk/public/_build/results?buildId=4462078&view=logs&j=011e1ec8-6569-5e69-4f06-baf193d1351e&t=91baa004-7830-58b9-b0bb-1d930da3801a)
```
Push-Location: /mnt/vss/_work/1/s/eng/scripts/Create-APIView.ps1:209
Line |
209 |      Push-Location $ProjectPath
     |      ~~~~~~~~~~~~~~~~~~~~~~~~~~
     | Cannot find path
     | '/mnt/vss/_work/1/s/specification/keyvault/Security.KeyVault.BackupRestore' because it does not exist.
```
The PR introduces a few new TypeSpec directories, of which Security.KeyVault.BackupRestore is one. Can anyone help point to what's going wrong with this check? Is this a blocker for merging?

## Answer
The issue here is that the merge commit was not properly checked out
 
Will submit a fix in few minutes 

https://github.com/Azure/azure-rest-api-specs/pull/32076

# cspell on file name

## Question 
Hi! I have a quick question about cspell/spellcheck.
 
My PR pipeline is failing spellcheck due to cspell detecting a file name (roleassignmentitem.tsp) as a misspelled word.
 
What would be the best approach to fix this?
 
I would probably still want roleassignmentitem to be counted as a misspelling normally since it's not camelCase, but not as part of the filename (since I think the convention is to use all lowercase?).
 
So should I disable and re-enable cspell before/after that import line?
 
Or is it better to do something like, enableCompoundWords; or, add roleassignmentitem as a valid spelling/allowed word? 
 
The error:
![alt text](image-23.png)
And the file/spelling in question, with enableCompoundWords as example fix:
![alt text](image-24.png)
TLDR: Is there a way to say "add this as a valid word but only for the next line"? Or would it be better to just disable spellcheck for the line in question, and enable it again on the next line?

## Answer
You can add an exception for that word in that particular file using the `overrides` in `cSpell.json` 
Here's the docs for adding overrides: [azure-rest-api-specs/documentation/ci-fix.md at main · Azure/azure-rest-api-specs](https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/ci-fix.md#swagger-spellcheck)

# Date-times in TypeSpec

## Question 
https://typespec.io/docs/language-basics/built-in-types/#date-and-time-types isn't clear on the actual string formats of date-times. It seems to imply that `utcDateTime` use either RFC3339 or ISO8601 by the format (note: those two are not equivalent but close; REST API Guidelines recommend RFC3339), and `offsetDateTime` is RFC1123-like. The month isn't abbreviated though, maybe that's not strictly required. But the presence of "utc" or "offset" can't be all that dictates RFC1123 vs. RFC3339, can it? After all, either of those RFCs support GMT/UTC or offset TZs.

## Answer
In TypeSpec, utcDateTime and offsetDateTime are conceptual types and do not enforce a specific serialization format. The actual serialization is determined by the emitter (e.g., OpenAPI, language SDK emitters). By default, emitters serialize date-time values in the body using RFC3339, and in headers using RFC1123 (technically RFC7231).

To override these defaults, you can use the @encode decorator to explicitly specify the format.

While these defaults align with common HTTP and Azure service practices, the documentation does not clearly surface this behavior, making it harder for developers to understand or discover. The community is working to improve the documentation to clarify the defaults per emitter and protocol.

# Suppress Typespec Compiler Error?

## Question 
Hello,
 
I am in the process of converting our existing API specs to Typespec, and we have a few properties in a model that can't be patchable. However, when trying to compile, I get this error. Is there a way to suppress this error so I can get compilation? I was working with Mark Cowlishaw but I think he is OOF today:
 
- error @azure-tools/typespec-azure-resource-manager/patch-envelope: The Resource PATCH request for resource 'ScalingPlan' is missing envelope properties:  [identity, managedBy, plan, sku]. Since these properties are supported in the resource, they must also be updatable via PATCH.

> 2947 | model ScalingPlanPatch {

## Answer
You should just be able to suppress this.  If you will point at the pr with the corresponding typespec-validation error, I can show where this is (generally you would just need to put the suppression on the PATCH operation)
Ahh, I see why I wasn't able to get this into a playground - you actuallky need to suppress this on the PATCH request model,  as in this playground

# Is it possible to emit several SDKs from a single TypeSpec, each one targeting a sub-set of REST APIs?

## Question 
We are building new data plane REST APIs (actually combining different sets of existing data plane APIs into one improved set, one service). So we will have a single TypeSpec project with many REST APIs.
 
My question is the following:  We will use emitters to auto-generate SDKs (+some hand coding). We do not want to create a single complex SDK from the whole TypeSpec. We want to create multiple SDKs (different packages), each one targeting a specific sub-set of REST API in the TypeSpec description.
 
Is that possible to do today?

## Answer
A single TypeSpec project can only generate one SDK. While it's possible to generate SDKs for multiple languages, they must all describe the same REST API. To create multiple SDKs for different parts of a service, the TypeSpec must be split into separate projects, each with its own configuration. Shared models can be placed in a common folder and referenced across different TypeSpec projects.

# Spellcheck error in OptionalProperties<UpdateableProperties<Azure.ResourceManager.Foundations.ManagedServiceIdentity>>;

## Question 
Hello,
 
I have created a PR [Microsoft.ManagedNetworkFabric 2024-06-15-preview API updates by nnellikunnu · Pull Request #20593 · Azure/azure-rest-api-specs-pr](https://github.com/Azure/azure-rest-api-specs-pr/pull/20593) which was ARMSignedOff
 
I merged to the latest RPSaaSMaster branch and it started failing for SpellCheck for the above property definition which is part of Standard Azure typespec models.
 
The error is reported for "UpdateableProperties"
 
Can you please suggest how can I resolve the issue?

## Answer
To resolve the issue with the spelling of "Updateable," you can add it as an exception in the cSpell.json file using the overrides section. This can be done by modifying the cSpell.json file in the repository.

You can refer to the documentation for adding overrides here:
https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/ci-fix.md#swagger-spellcheck

While both "updateable" and "updatable" are generally acceptable forms, the "updatable" form is more commonly used, and a recent update to the cSpell tool may have removed the less common form.

SpellCheck has always been enabled, but a recent migration to a new implementation is causing some new issues.

# SDK automation checks with multi-directory TSP projects

## Question 
Key Vault's Administration library has multiple subservices, which are kept in separate TSP directories (PR link). Each of these directories references a Common directory with some shared definitions. The SDK automation checks have been failing because they're not resolving imports across directories.
 
This type of error seems consistent with using tsp-client to generate when additionalDirectories aren't included. Is that the issue here? If so, how can we get these checks to work with our directory setup?

## Answer
Hi McCoy Patiño I have identified that the failure was resolved by adding the missing `additionalDirectories`. tsp-client already supports `additionalDirectories`.
The Python SDK can now be successfully generated in the latest build. However, the Java and .NET SDKs are still failing, likely due to custom code. There might also be issues with the JavaScript code generation.
Mary Gao is currently assisting with troubleshooting the JavaScript issue, while the Go SDK is also under investigation.
Please let me know if you need any additional details or further assistance.

# Non-specs repo client

## Question 
To generate a client (test-proxy) from TypeSpec that is not in the azure-rest-api-specs repo, should we use tsp-client still or just tsp? Both https://aka.ms/typespec and https://aka.ms/typespec/azure both document tsp and I don't remember where I even installed tsp-client. Where are its docs and what I can do with tsp-location.yaml?

## Answer
The tsp-client uses the emitter-package.json file to determine which emitter to use, based on the dependencies listed under "dependency." The emitter options, such as the @azure-tools/typespec-rust emitter, are specified in the tspconfig.yaml file, and additional dependencies should be added in the emitter-package.json file.

If errors occur related to missing dependencies, it’s important to ensure that the necessary TypeSpec modules are listed under "devDependencies" in the emitter-package.json of the SDK repository. Dependencies should not be added to the tools repo's package.json file, but rather in the respective emitter-package.json file.

The issue encountered with the @azure-tools/typespec-rust emitter crashing appears to be a bug related to an unimplemented feature (model property kind header NYI), and a report should be filed for this. Additionally, it's noted that dependencies must be installed using npm install within the TempTypeSpecFiles directory to avoid errors related to missing imports.

# PR Typespec Validation Failing but Succeeding Locally

## Question 
Hello,
 
I am working on converting my service to use typespec. In my PR, the typespec validation check is failing, but when I run the command locally on the directory, everything succeeds. There aren't any files that haven't been committed or pushed, is there a reason why it would be failing in the PR check but succeeding locally?
 
https://github.com/Azure/azure-rest-api-specs/pull/31722/

## Answer
The error message is saying 
```
Argument of type 'Microsoft.DesktopVirtualization.PrivateEndpointConnection' is not assignable to parameter of type 'Azure.ResourceManager.CommonTypes.Resource'
```
which is expected because your PrivateEndpointConnection is extending self-defined resource, not the common type one.

The PR check is failing because your spec cannot be compiled.  If you can't reproduce this locally, try a fresh clone of your branch and run npm ci again.  Or share instructions for us to repro the problem locally.

# LintDiff warnings Supression

## Question 
Hi,
https://github.com/Azure/azure-rest-api-specs-pr/pull/20148
 
Is it fine to suppress a few LintDiff warnings if we have a dependency (call those APIs for a few operations) on external opensource APIs which we do not control? 

Also I am noticing the CI pipeline does not have lint diff check now. Is there a way to run the lintdiff while we are still working on fixing the ci errors?

## Answer
We don't check specs in to the main branch of the private repo.  Why does this spec need to be private?  I would suggest reading the docs in the `readme.md` about onboarding new specs: https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/Getting%20started%20with%20OpenAPI%20specifications.md
 
Also for questions about the azure-rest-api-specs repo, you will likely get better answers in the API SPec Review channel: [Azure SDK | API Spec Review | Microsoft Teams](https://teams.microsoft.com/l/channel/19%3A0351f5f9404446e4b4fd4eaf2c27448d%40thread.skype/API%20Spec%20Review?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47)

Specifically, if you are onboarding a new data plane API, you will want to get on the schedule for api review and on the calendar for SDKs,  and this will also help with targeting your PR correctly.

# Swagger PrettierCheck and TypeSpec Validation conflicts

## Question 
I have this PR. The Swagger PrettierCheck will fail if I don't add a newline to "specification/hybridconnectivity/HybridConnectivity.Management/examples/2024-12-01/XXX.json", while if I add the newline the TypeSpec Validation will fail with 
```
  --- a/specification/hybridconnectivity/resource-manager/Microsoft.HybridConnectivity/stable/2024-12-01/examples/EndpointsGetCustom.json
  +++ b/specification/hybridconnectivity/resource-manager/Microsoft.HybridConnectivity/stable/2024-12-01/examples/EndpointsGetCustom.json
  @@ -19,4 +19,4 @@
     },
     "operationId": "Endpoints_Get",
     "title": "HybridConnectivityEndpointsGetCustom"
  -}
  \ No newline at end of file
  +}
```
What action could I take?

## Answer
When the check fails, it's recommended to follow the instructions provided. Specifically, after running tsp compile, ensure all files are included in the change. However, an issue arises where adding a newline causes the Swagger PrettierCheck to fail, while not adding the newline causes the TypeSpec Validation to fail.

If this issue is in an example file, it's suggested to run Swagger Prettier on the source example, as TypeSpec merely copies the source example into the examples folder in the Swagger folder.

# Implementing Private Endpoint with RPaaS and TypeSpec generated code

## Question 
Hello team,
 
I'm currently working on implementing the Private Endpoint feature for a new service called Planetary Computer Pro, designed for storing and searching geospatial data, typically satellite images. Our resource provider is developed using RPaaS, with code generated from the service TypeSpec, and it overrides necessary functions such as OnValidateCreate and OnCreateAsync for our main resource (GeoCatalog). To support Private Endpoint, which is mandatory by APEX, I've added a new resource model defined as:
```
@added(Versions.`2025-01-31-preview`)@doc("Holder for private endpoint connections")@parentResource(GeoCatalog)model PrivateEndpointConnectionResource  is ProxyResource<PrivateEndpointConnectionProperties> {  ...PrivateEndpointConnectionParameter;}
```
I used a couple service implementations from MSAzure/One as models. You can review the complete spec in the attached file.
 
Based on the documentation provided in Private Endpoints Support [PREVIEW] | ARM Wiki, it's my understanding that I need to override the following methods in my resource manager:
- `OnCreateAsync` for the `PrivateEndpointConnectionProxy` resource, which is invoked by the `ResourceCreationBegin` extension.
- `OnCreateAsyncfor` the `PrivateEndpointConnection` resource, which is invoked by the `ResourceCreationBegin` extension.

I am encountering an issue where the code generated during the TypeSpec compilation is producing a `PrivateEndpointConnectionResourceControllerBase` class, but not a `PrivateEndpointConnectionProxyResourceControllerBase` class. This prevents me from overriding all the necessary methods. I believe I may be overlooking something, likely in the TypeSpec definition. Any assistance would be greatly appreciated. Additionally, if anyone can direct me to a similar example where the Private Endpoint feature is implemented in an RPaaS-based resource manager with code generated from TypeSpec, it would be really helpful. Thanks! 

## Answer
Just as background - privateEndpointConnectionProxies appears to be a service-to-service part of the API that implements extensibility points used only from metaRP, with no representation in the public API and very specific userRP extension requirements.  Since it isn't part of user-facing API, we wouldn't include it in public-facing specs.  To support this in service generation, either there would need to be a service-to-service part of the spec only included for userRP extension generation, or there would need to be specific support for privateEndpoints that automatically generated the needed service-to-service extensions.  This is a case crying out for specific support, as the requirements are very prescribed.

# Sharing API version enum across TSP directories

## Question 
Hi, as part of our KV TSP conversion we have a couple of different TSP directories, which all share some common definitions in a dedicated shared directory. We want to define our service API version enum in this shared directory so that all libraries automatically get API version updates. Unfortunately I haven't been able to sort out how to make this work; here's a TypeSpec playground to illustrate.
 
The playground contains a KeyVaultCommon namespace, representing our shared directory; a KeyVault namespace representing a single library; and a ClientCustomizations namespace, representing a single library's client.tsp namespace. Things compile successfully in the playground, but this has KeyVault redefining the API version enum -- if we try to do what we want, which is to version KeyVault and ClientCustomizations with KeyVaultCommon.KeyVaultVersions, we get the following error:
```
Namespace 'ClientCustomizations' is referencing types from versioned namespace 'KeyVault'
but didn't specify which versions with @useDependency.
```
Based on trial and error, it looks to me like TSP is only satisfied if ClientCustomizations uses a version enum that's defined in, and used to version, the namespace that defines the operation being customized. It seems like TSP can't understand that the version enum referenced in ClientCustomizations is the same one being used by KeyVault. Am I doing something wrong here, or is this shared API version enum setup just not possible today?
An additional note on this: I tried to get around the limitation by defining an alias for KeyVaultCommon.KeyVaultVersions inside of the KeyVault. Unfortunately this doesn't resolve the error; I can only get the error to go away by fully defining a new enum inside KeyVault.

## Answer
Yes, version enums are specific to the namespace they are defined in (and its sub-namespaces).  Taking dependencies from versioned namespace to versioned namespace is a bit tricky.  Likely the best option is to have versions defined in each and use the @useDependency on the leaf namespace version to associate it with the corresponding version in the shared namespace.
 
i.e.
```typescript
@versioned(Versions)
namespace MySharedNamespace {
  enum Versions {
    V1: "1.0",
    V2: "2.0"
  }
}

@versioned(Versions)
namespace MyLeafNamespace {
  enum Versions {
    @useDependency(MySharedNamespace.Versions.V1)
    V1: "1.0",
    @useDependency(MySharedNamespace.Versions.V2)
    V2: "2.0"
  }
}  
```


# Operation Name 'import' not allowed?

## Question 
Hello,
 
Our previous API specs had an operation Name import, but when I try to use it in typespec it defaults to the language use of the word import instead. Is there a way around this?
![alt text](image-25.png)

## Answer
I think you might be able to escape it: [Identifiers | TypeSpec](https://typespec.io/docs/language-basics/identifiers/#reserved-identifiers)

# Allowing "breaking changes" for brownfield conversion

## Question 
Hello, I'm converting a brownfield service (Key Vault) to TypeSpec. Because we can't match the original Swaggers exactly, there are errors reported in "Swagger BreakingChange" checks for each KV subservice;  we've confirmed that these changes are not actually breaking and will be a necessary part of the TSP migration.
 
Mike Kistler, Jeffrey Richter, is there a process to bypass migration-related breaking changes? Mike Harder informed me that there's a label for benign, approved changes -- should we add this to our PRs? Below are links to each.
 
Administration; Certificates; Keys; Secrets

## Answer
The conversation discusses applying the appropriate "BreakingChange-Approved" label for changes to the typespec, including handling suppression requests for unsupported unixtime formats. For unixtime, it's agreed that legacy specifications can continue using it, but new specifications should avoid it. Suppressions for unixtime should be added to the readme.md, and approvals for these suppressions should be handled by the SDK team. The review of these suppressions can be requested through a specific channel.

# Requesting assistance to merge the type spec PR.

## Question 
Hi,
The below PR has been approved and is ready to merge. However, I don't have the permissions to merge it myself. Requesting assistance to merge the typespec PR.
https://github.com/Azure/azure-rest-api-specs-pr/pull/19683

## Answer
The PR is ready to merge but requires approval from someone with write access to the repository. The approver can be from the same team, as long as they have the necessary permissions. To merge the PR, ensure all validations pass and the "Next Steps to Merge" and "Automated merging requirements met" checks are green.

# Conditionally Excluding Internal Enum Values from OpenAPI Specs

## Question 
Hi team,
 
I have enums where some values are for internal use only and should not be included in the generated OpenAPI spec for external users. However, I still want to use these internal values for partner teams. How can I mark certain enum values as internal and exclude them from the OpenAPI spec conditionally? I will have code generated for internal partners to include all properties and for external it would be excluded. 
 
This scenario may also apply to properties of a model, but hopefully that would be solved with the same solution.
```
enum SourceType {

  customerLakeStore: "CUSTOMER_LAKE_STORE";

  customerProcessingStore: "CUSTOMER_PROCESSING_STORE"; // exclude from openapi spec conditionally

  customerTable: "CUSTOMER_TABLE";

  customerKusto: "CUSTOMER_KUSTO";

}
```
## Answer
In TypeSpec, there is no built-in functionality for conditional omission, so teams would need to model this as two separate specs, potentially with shared types. The idea of extending the @visibility attribute to support conditional compilation was discussed, but currently, there is no library configuration, only emitter configuration. 

# Model v Interface Names

## Question 
I need to change interface AppAttachPackages to AppAttachPackage for operationIds, but can't because the model is already named AppAttachPackage. Is there a directive I can add so an interface or model name can be named something else? Similar to how @action works? https://github.com/Azure/azure-rest-api-specs/pull/31722/checks?check_run_id=35630442277

## Answer
In this conversation, the focus is on handling operationId in TypeSpec without relying on OpenAPI-specific decorators. The solution involves using @@clientName(AppAttachPackages, "AppAttachPackage") from TypeSpec-client-generator-core, which participates in conflict checks. The main concern is whether the operationId is needed for generated Swagger or the SDK description. For Swagger, operationId is currently the only available option, but for SDK generation, alternative solutions are being explored. The goal is to avoid OpenAPI-specific decorations and instead focus on generating directly from TypeSpec, though Swagger will still be part of the release due to dependencies from tools like documentation generators.

# Unions - `modelAsString:true` hinders RPaaS validation. How to resolve this?

## Question 
Hi 
TypeSpec Discussion
, I'm working on a new swagger for our Resource Provider using RPaaS and while testing we realized that RPaaS isn't validating our properties which are modeled as union with string literals. Any value passed by the user for the enum fields are accepted and saved. 
 
We raised this question to the RPaaS team (https://portal.microsofticm.com/imp/v5/incidents/details/577866916/summary) and they responded saying "since the enum has modelAsString : true, they are treated as normal string and not validated". They suggested to make "modelAsString: false" so that RPaaS validations can be kicked in.
 
To resolve this issue, I tried the following approaches - 
Converting union to enum - This doesn't pass TypeSpec CI validations (Azure services should not use the enum keyword. Extensible enums should be defined as unions with "string" as an accepted variant)
Suppressing the warning "@azure-tools/typespec-azure-core/no-enum" - But the Typespec validation still fails.
Using union without the string literal and suppressing the warning as mentioned below - This approach works and the CI validation also succeeds, but we wanted to understand if this is the right approach? 
Note - This is the snippet for Private Preview of the feature, post that we might have to add other values to the union as well.
```typescript
#suppress "@azure-tools/typespec-azure-core/no-closed-literal-union" "Suppress warning for enums"
@doc("The state of the connector")
union State {
  @doc("Whether the connector is enabled")
  Enabled: "Enabled",

  @doc("Whether the connector is disabled")
  Disabled: "Disabled",
}
```
Could someone please help understand if we should go ahead with approach #3 and whether we would be able to add new values to the union later? If not, what is the recommended approach such that "modelAsString: false" is present and RPaaS validations work.
 
Other alternative we have is for the UserRP to validate the enum values (but since most of the other validations are already done by RPaaS, we would want this to be handled by them).
 
Thanks!

## Answer
In this conversation, the discussion revolves around using enums and unions in TypeSpec for API validation, especially when considering backward compatibility across API versions. The main point is that using modelAsString: true allows for flexibility and avoids breaking changes when new enum values are added in future versions. This is crucial because closed enums will cause a breaking change if new values are added, even in a new API version.

If modelAsString: false is used, adding new enum values would be a breaking change, as the enum is closed. The recommendation is to use unions (which are extensible) instead of enums, as unions allow for future expansion without breaking previous versions of the API.

If validation is required on the RPaaS side, a custom validation extension could be implemented, ensuring that any new values are handled appropriately. It's also advised to keep the enum open for future versions, and not make it closed unless absolutely necessary, as it could lead to issues when adding new values in later versions.

For older API versions, if a new enum value is introduced, it should be handled carefully to avoid issues with ARM, which requires resources to be compatible across API versions. In practice, this means either ignoring the new field in older versions or returning a default/empty value, depending on the specific requirements.

# Polymorphism and ARM Patch Sync

## Question 
We have some polymorphic discriminator properties in our typespec. One of the examples beloe
```
// Polymorphic Source
@discriminator("type")
@doc("The type of backing data source")
model Source {
  @doc("Type of the Storage Connector – Bucket. Not mutable once the Storage Connector is created.")
  type?: SourceType;
}

// Bucket source extends source
@doc("The properties of the backing data store.")
model BucketSource extends Source {
  @visibility("read")
  type: SourceType.Bucket;

  @doc("Details for how to connect to the backing data store.")
  connection: Connection;

  @doc("Details for how to authenticate to the backing data store.")
  authProperties: AuthProperties;

  @doc("The host to use when computing the signature for requests to the backing data store. If not provided, defaults to what is provided in the endpoint for the connection.")
  hostOverride?: string;
}

// The actual connector properties 
@doc("Details of the Storage Connector.")
model ConnectorProperties {
  @visibility("read", "create")
  @doc("System-generated identifier for the Storage Connector. Not a valid input parameter when creating.")
  uniqueId?: string;

  @doc("State – Enabled or Disabled. Whether or not the Storage Connector should start as enabled (default: Enabled) (While set to false on the Storage Connector, all data plane requests using this Storage Connector fail, and this Storage Connector is not billed if it would be otherwise.)")
  state?: State = State.Enabled;

  @visibility("read", "create")
  @doc("System-generated creation time for the Storage Connector. Not a valid input parameter when creating.")
  creationTime?: string;

  @doc("Arbitrary description of this Storage Connector. Max 250 characters.")
  @maxLength(250)
  description?: string;

  @doc("Information about how to communicate with and authenticate to the backing data store.")
  source: Source;

  @visibility("read")
  @doc("The status of the last operation.")
  provisioningState?: ProvisioningState;
}

// Conenctor resource as tarcked resource
@doc("A Connector is a tracked ARM resource modeled as a sub-resource of a Storage Account")
model Connector is TrackedResource<ConnectorProperties> {
  @doc("The name of the connector")
  @pattern("^[a-zA-Z0-9-]{3,24}$")
  @key("connectorName")
  @segment("connectors")
  @path
  name: string;
}
```
When adding a update patch method for this resource -
```
@armResourceOperations(Connector)
interface Connectors {
  @doc("Get the specified Storage Connector.")
  get is ArmResourceRead<Connector>;

  @doc("Create or update a Storage Connector.")
  createOrReplace is ArmResourceCreateOrReplaceAsync<Connector>;

  @doc("Update a Storage Connector.")
  update is ArmCustomPatchAsync<
    Connector,
    ConnectorProperties
    >;
}
```
We see an error from swagger Lint DIff validation stating that all properties of a patch resoucre should be optional.
Typespec does nota low to have optional discriminator values
Is there a way we can model this in typespec for patch api?

## Answer
In this conversation, the main focus is on handling polymorphic properties and patch resource models in TypeSpec for ARM, specifically related to suppressing LintDiff errors. The issue arises because Swagger LintDiff validation requires all properties of a PATCH resource to be optional, which conflicts with the need to model required polymorphic properties in a patch.

To address this, it's agreed that suppressing the LintDiff error is the right approach for now, as the pace of change is slow. The specific error to suppress is related to the requirement that all PATCH resource properties should be optional, and the suppression rule needs to be applied to that specific path in the model.

Additionally, a PR was reviewed and updated to use a more specific suppression path for the patch parameters, and it was approved with the appropriate labels.

# Questions About the Usage of Record

## Question 
Hi Team,
 
I’m using the Record type in Typespec to represent a dictionary relationship between ConfusionMatrix and ConfusionMatrixRow, ConfusionMatrixRow and ConfusionMatrixCell. However, after generating the .NET SDK, the dictionary values are incorrectly generated as BinaryData.
 
Could anyone please advise on how to address this and ensure the correct generation of the dictionary? Thank you!
 
Typespec: https://github.com/amber-ccc/azure-rest-api-specs/blob/57396f185b86c7aaddac6f0ea401f2d68cef1f3a/specification/cognitiveservices/Language.AnalyzeConversations-authoring/models.tsp#L1032
 
Dotnet sdk: https://github.com/amber-ccc/azure-sdk-for-net/blob/5eda9c7dcd7fa5046f2f0bc215b22eddc30b5f6d/sdk/cognitivelanguage/Azure.AI.Language.Conversations.Authoring/src/Generated/Models/ConfusionMatrix.cs#L24

## Answer
In this conversation, the main issue is how to model dictionary relationships between models in TypeSpec and their representation in the .NET SDK. The syntax model A is `Record\<B> {} defines "additional properties," meaning model A accepts values of type B, but this isn't the same as a dictionary in .NET. If you need a dictionary-like structure, consider using nested Record types or aliases.

In .NET SDK, when the value type is not primitive, additional properties are represented as IDictionary<string, BinaryData>. However, you can't directly constrain BinaryData to a specific type like ConfusionMatrixCell. A workaround is to use nested records such as Record<Record\<ConfusionMatrixCell>> or aliases for clarity.

In summary, Record\<B> defines additional properties, not dictionaries. To model dictionaries, you should use nested records or aliases. In .NET, BinaryData fields can't be strictly constrained, so it's best to adjust your TypeSpec accordingly.

# How to Split Models into Different Namespaces in the Generated SDK

## Question 
Hi Team,
 
In the generated SDK, the .Models namespace currently contains too many classes, making it difficult to manage. We would like to split these models into separate namespaces.
 
I attempted to organize models into different namespaces in TypeSpec by placing some in common.tsp with the .Common namespace and others in Project.tsp with the .Project namespace. However, after generating the .NET SDK, all the models are still grouped together under the .Models namespace.
 
How can we ensure that the models are generated in their respective namespaces in the .NET SDK? Thank you!
 
common.tsp: https://github.com/amber-ccc/azure-rest-api-specs/blob/amber/release_first_beta_version_of_authoring/specification/cognitiveservices/Language.AnalyzeConversations-authoring/models/common.tsp
 
project.tsp: https://github.com/amber-ccc/azure-rest-api-specs/blob/amber/release_first_beta_version_of_authoring/specification/cognitiveservices/Language.AnalyzeConversations-authoring/models/project.tsp

## Answer
currently, for azure libraries, this is not possible.
We are making a feature to support this in unbranded (non-azure) generators.
There is no immediate plan to implement this for azure generator yet.
 
To clarify, an "azure library" means your spec goes into azure-rest-api-specs repo, which requires you to use the azure flavor to generate your library, therefore your library is called an "azure library" which is short for maybe "azure-flavored library"

# Generate typespec for a specific version of the API

## Question 
Hey Folks, 
I'm adding a new version to my API. The new version has some changes amongst which one change is to make a required property optional. Running a vanilla tsp compile . seems to be compiling old and new version. This is causing a validation failure in the PR. Wondering if there is a way to tsp compile and provide a specific version so that a new specification is created just for the new version and old version is not updated.

## Answer
TypeSpec specs are inherently multi-version.  You use the version library to encode the changes from the last version to the current version, see: [10. Versioning | TypeSpec Azure](https://azure.github.io/typespec-azure/docs/getstarted/azure-core/step10/)
You need to make sure both that `@typespec/versioning` is in your package.json and you have imported it in your spec.  If you are targeting one of the azure-rest-api-specs repos, the package.json at the root of the repo already includes the package

# Setting up TypeSpec tsp cli in order to run "tsp compile ." command

## Question 
[Read the docs](https://armwiki.azurewebsites.net/rpliteonboarding/typespecGettingStarted.html)
 
Tried to install the tsp cli using npm install -g @typespec/rest @typespec/openapi3 @azure-tools/typespec-azure-core @azure-tools/typespec-autores and then ran the tsp command, got this error in the screenshot. Could I be missing a setup step?
![alt text](image-26.png)
I see these folders go installed globally too and this path is in my environment variables too
![alt text](image-28.png)![alt text](image-27.png)

## Answer
The user faced issues with installing TypeSpec packages globally, which is not recommended when working within the Azure REST API specs repository. The correct approach is to use npx to ensure that the locally installed packages in the repository are used, as the repository has a specific package.json that pins the required versions. The TypeSpec compiler can be installed globally to access the tsp command, but other TypeSpec packages should not be installed globally to avoid conflicts. The documentation may be inconsistent, and it is suggested to follow the Azure-specific docs in the azure-rest-api-specs repo for proper setup. The discussion also highlights the difference between core TypeSpec documentation, which is for a broad audience, and Azure-specific documentation for internal use.
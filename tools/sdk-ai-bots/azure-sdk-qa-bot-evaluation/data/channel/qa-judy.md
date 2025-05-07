 
# Is there a unique and certain `namespace` for data-plane api?

## Question 
in tsp for data-plane of devcenter, the namespace is DevCenterService,  while in the swagger api, the namespace looks like to be Microsoft.DevCenter, azure-rest-api-specs/specification/devcenter/DevCenter/main.tsp at main · Azure/azure-rest-api-specs
 
Is the namespace certain in someway, or it's just a sequence or chars and it doesn't influence the api?
 
For mgmt-plane, the namespace matters cause it's a part of the operation id path, but for data-plane, not sure about the namespace constraints.

## Answer
For data-plane client libraries, the namespace is generally defined in tspconfig.yaml through the namespace option and that takes precedence over what is in main.tsp.  Since DevCenter spec already has this defined in tspconfig.yaml, the namespace defined in main.tsp doesn't matter for generating client libraries. 
 
 

# Setting x-ms-identifiers for objects in Azure.Core.Page<T>?

## Question 
Hi, I'm working on a typespec migration.
 
I have a list operation response definition:
model FaultSimulationListResult is Azure.Core.Page<FaultSimulation>;
 
Our existing swagger has the output as:
"FaultSimulationListResult": {
      "type": "object",
      "properties": {
        "value": {
          "type": "array",
          "title": "fault simulation list value.",
          "description": "The list of fault simulations.",
          "items": {
            "$ref": "#/definitions/FaultSimulation"
          },
          "x-ms-identifiers": [
            "simulationId"
          ]
        },
        "nextLink": {
          "type": "string",
          "description": "The URL to use for getting the next set of results."
        }
      },
      "description": "Fault simulation list results"
    }
 
When I try adding a @OpenAPI.extension("x-ms-identifiers", #["simulationId"])
 decorator to the model in Typespec, I get:
```
    "FaultSimulationListResult": {
      "type": "object",
      "description": "Fault simulation list results",
      "properties": {
        "value": {
          "type": "array",
          "description": "The FaultSimulation items on this page",
          "items": {
            "$ref": "#/definitions/FaultSimulation"
          },
          "x-ms-identifiers": []
        },
        "nextLink": {
          "type": "string",
          "format": "uri",
          "description": "The link to the next page of items"
        }
      },
      "required": [
        "value"
      ],
      "x-ms-identifiers": [
        "simulationId"
      ]
    }
 ```
Is there a way to tag the array within the Azure.Core.Page?

## Answer
  - There is 2 options:
     - mark simulationId property with @key
     - Add @@identifiers(FaultSimulationListResult.value, #["simulationId"])


# Issues with Spec PR

## Question 
I am seeing below errors with TypeSpec file validation.
Can anyone let me know how to fix them?
 
 ```
  Executing rule: Compile
137  run command:npm exec --no -- tsp compile --warn-as-error /home/runner/work/azure-rest-api-specs-pr/azure-rest-api-specs-pr/specification/carbon/Carbon.Management
138  TypeSpec compiler v0.66.0
139  
140  Compiling...
141  ✔ Compiling
142  Running @azure-tools/typespec-autorest...
143  ✔ @azure-tools/typespec-autorest	
144  Diagnostics were reported during compilation:
 
  specification/carbon/Carbon.Management/main.tsp:16:10 - error deprecated: Deprecated: Using a model as a value is deprecated. Use an object value instead(with #{}).
147  > 16 | @service({
148       |          ^
149  > 17 |   title: "Carbon",
150       | ^^^^^^^^^^^^^^^^^^
151  > 18 | })
152       | ^^
153  specification/carbon/Carbon.Management/main.tsp:122:34 - error deprecated: Deprecated: Using a tuple as a value is deprecated. Use an array value instead(with #[]).
154  > 122 |   @extension("x-ms-identifiers", [])
155        |                                  ^^
156  specification/carbon/Carbon.Management/main.tsp:126:34 - error deprecated: Deprecated: Using a tuple as a value is deprecated. Use an array value instead(with #[]).
157  > 126 |   @extension("x-ms-identifiers", [])
158        |                                  ^^
159  specification/carbon/Carbon.Management/main.tsp:130:34 - error deprecated: Deprecated: Using a tuple as a value is deprecated. Use an array value instead(with #[]).
160  > 130 |   @extension("x-ms-identifiers", [])
161        |                                  ^^
162  specification/carbon/Carbon.Management/main.tsp:134:34 - error deprecated: Deprecated: Using a tuple as a value is deprecated. Use an array value instead(with #[]).
163  > 134 |   @extension("x-ms-identifiers", [])
164        |                                  ^^
165  specification/carbon/Carbon.Management/main.tsp:138:34 - error deprecated: Deprecated: Using a tuple as a value is deprecated. Use an array value instead(with #[]).
166  > 138 |   @extension("x-ms-identifiers", [])
167        |                                  ^^
168  specification/carbon/Carbon.Management/main.tsp:501:34 - error deprecated: Deprecated: Using a tuple as a value is deprecated. Use an array value instead(with #[]).
169  > 501 |   @extension("x-ms-identifiers", [])
170        |                                  ^^
171  specification/carbon/Carbon.Management/main.tsp:509:34 - error deprecated: Deprecated: Using a tuple as a value is deprecated. Use an array value instead(with #[]).
172  > 509 |   @extension("x-ms-identifiers", [])
173        |                                  ^^
```
## Answer
You probably need to update your local fork from the target branch.  There was a recent breaking change that made all openapi extesnions  (and also the @service decorator ) use value types, and issued a warning diagnostic if you used the old syntax).  If you bring in the latest, these were all changed to use the new syntax.
 
Note that there is also now an @identifiers decorator that allows you to set these values:, which you use [like this](https://github.com/Azure/azure-rest-api-specs-pr/blob/RPSaaSMaster/specification/carbon/Carbon.Management/main.tsp#L16)
 


# Regex Pattern is not getting updated


## Question 
Hi, 
in this PR - Update Neon Spec with expanded entities for GA Release by alluri02 · Pull Request #21671 · Azure/azure-rest-api-specs-pr
i added proxy resource Types and added Regex for Name Properties.. 
```
@doc("The Project resource type.")
@added(Neon.Postgres.Versions.v2_preview)
@parentResource(OrganizationResource)
model Project is ProxyResource<ProjectProperties> {
  ...ResourceNameParameter<
    Resource = Project,
    KeyName = "projectName",
    SegmentName = "projects",
    NamePattern = "^\\S.{0,62}\\S$|^\\S$"
  >;
}
```
However when i run the tsp compile command it is reflecting the the swagger. https://github.com/Azure/azure-rest-api-specs-pr/blob/f6fe0333d8f47439546ae06d0b4e93de4765716f/specification/liftrneon/resource-manager/Neon.Postgres/preview/2025-02-01-preview/neon.json#L606
 
It is still using the default regex "pattern": "^[a-zA-Z0-9-]{3,24}$"
 
Can you please help me on this issue?

## Answer
try @patten() instead of NamePattern, Here's what works for me: https://github.com/Azure/azure-rest-api-specs-pr/blob/RPSaaSDev/specification/throttling/Microsoft.Throttling.Management/throttling.tsp#L67


# Updating enum with an arbitrary string

## Question 
Hi team, this is the type spec definition for my resource: azure-rest-api-specs-pr/specification/impact/Impact.Management/connectors.tsp at RPSaaSMaster · Azure/azure-rest-api-specs-pr
 
connectorType is one of the properties of this resource. This property is defined as an enum with only one value.
 
Our expectation is updating this property with a value not present in the enum should throw an error. But we see that customers are able to update this property with a PATCH http call. We haven't implemented any additional logic for PATCH. RPaaS handles patch calls with default logic.
 
Please help me understand the reason why updating the property with random values is allowed even when I define the property to be an enum
 
 
Replicating the issue locally:


## Answer
You defined this property as an open union: azure-rest-api-specs-pr/specification/impact/Impact.Management/connectors.tsp at RPSaaSMaster · Azure/azure-rest-api-specs-pr.  This explicitly allows any string value.
 
Generally, the reason for doing this is that you think additional values will be enabled in future versions (or even in this version).  Note that, if you do not make this an open union, then adding any values in any future api-version would be a breaking change (which is why this is recommended).
 
As to what kind of validation is done over the spec, the RPaaS team is the one to ask about it, however, there are RPaaS extensions for validation that would allow you to reject requests for values that are not valid.



# data-plane: LongRunningResourceDelete and LintDiff.AvoidAnonymousTypes

## Question 
Spec is data-plane and uses LongRunningResourceDelete:
 
```
  @pollingOperation(getCleanupStatus)
  op deleteOrCleanupEntityRequest is Operations.LongRunningResourceDelete<
    CleanupEntityRequest,
    CleanupQueryParams & BackgroundJobHeaders
  >;
```
Generates swagger which includes a LintDiff error:
 
 
Latest docs I have recommend adding suppressions for data-plane specs:
 
https://github.com/Azure/azure-rest-api-specs/wiki/Swagger-LintDiff#avoidanonymoustypes
 
Do we have any updated guidance?  Did we try to address this in our last round of TypeSpec+LintDiff cleanup?
 
[1] 
Lekhana Somidi: Need Help Suppressing Swagger lintDiff Errors
posted in Azure SDK / API Spec Review on Monday, March 3, 2025 5:42 AM
 
[2] https://github.com/Azure/azure-rest-api-specs/pull/32929/files#diff-fb2ed0af460579105ff11d2e24ee4c057a19efbd5a21ac7b4fb7c74e99d5d64dR95

## Answer
   - Status: No real status; item is in the "as-needed" category.
   - Anonymous model linting issues: Known in typespec-azure; discussion moved to TCGC; current status unclear.
   - LongRunningResourceDelete: Needs review by Travis Prescott or Mark Cowlishaw to confirm if it's the correct type to use.
   - Current recommendation: Suppress the issue.
   - Issue tracking:
Initially in typespec-azure-pr; should be (and is being) moved to typespec-azure.
   - Replacement issue: [Accepted response in Azure.Core LRO templates should not have inline schema (#2290)](https://github.com/Azure/typespec-azure/issues/2290).
   - Wiki update: Documented [here](https://github.com/Azure/azure-rest-api-specs/wiki/Swagger-LintDiff#longrunningresourceaction).



# Urgent review for GA: Typespec regex pattern change

## Question 
Hi 
TypeSpec Discussion
,
I created a new GA version and I changed the regex pattern and it changed the pattern in older swagger jsons, which is causing a breaking change. Should I check-in only new GA swagger json or older swagger also where pattern is updated?  
Is versioning available here? 
We plan to take this to GA and need to get signOff by end of this week. Please let me know further actions on this.
 
 
Added 2025-03-30 GA stable version by amritas · Pull Request #32911 · Azure/azure-rest-api-specs

## Answer
 You want to accurately describe the constraint, which means you likely want to change it for all api-versions, or not make the change at all.  Note that, if all versions are preview, you should make the change for accuracy - generated clients do not use pattern values to validate inputs



# Description changes across versions?

## Question 
Hi, as part of this PR: Service Fabric Managed Clusters - API version 2025-03-01-preview · Azure/azure-rest-api-specs@599e269
 
My team wanted to add more details to a model description. This change results in a change in all spec versions generated with Typespec, and causes the Typespec validation to fail if I don't include the changes to the older specs. 
 
I wanted to know what the best course of action was for passing this check. Since we don't expect updates to our older specs, is it ok to just change the output path in our tspconfig.yaml to only point at the current version of the output spec? Or is there a better way to handle this?

## Answer
Honestly, the best thing is to update your docs and take the update in previous versions (which are likely now more accurately described as well).  Documentation updates are not breaking changes, and, if changes are limited to documentation, this should be passed easily by the breaking change board. Documentation-only updates should not be flagged as breaking changes at all. If they are for some reason, I'll just approve it.



# Default value starting from a specific API version?

## Question 
Hello, we currently have a property (StatelessServiceProperties,minInstancePercentage) that does not currently have a default value in our spec, but in practice is treated as if it is 0.
[azure-rest-api-specs/specification/servicefabricmanagedclusters/resource-manager/Microsoft.ServiceFabric/preview/2025-03-01-preview/servicefabricmanagedclusters.json at main · Azure/azure-rest-api-specs](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/servicefabricmanagedclusters/resource-manager/Microsoft.ServiceFabric/preview/2025-03-01-preview/servicefabricmanagedclusters.json)
 
We would like to treat the default prior to 2025-06-01 as 0, then as a different value from 2025-06-01 onward in our service.
 We haven't changed the default for an existing property before. Are there concerns about this intended behavior? 
In Typespec, is it possible to add a default value in our spec from a specific api version (say 2025-06-01) onward?
 

## Answer
I think you need to run this change by the breaking change board, as a change in the default may be breaking, depending on the details.  Also, the most important thing is to make sure that the API description accurately reflects service behavior - if the default has always been in place, for example, it may be better to just change the default and go through the breaking change process.  Yes, it is possible to do this in TypeSpec, but involves removing and renaming the old property and adding a new property with the new default, [like this](https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZ8wfYXp1cmUtdG9vbHMvyCstxhVjb3Jl3yvIK3Jlc291cmNlLW1hbmFnZXIiOwoKdXNpbmcgSHR0cDvHDFJlc3TIDFbpAI7IEkHESi5Db3JlzhJSx1xNxls7CgovKiogQ29udG9zb8RUxR4gUHJvdmlkZXIg5gCDbWVudCBBUEkuICovCkBhcm3IIE5hbWVzcGFjZQpAc2VydmljZSgjeyB0aXRsZTogIsdXyC1IdWJDbGllbnQiIH0pCkDnAUNlZCjnAL9zKQpuyFAgTWljcm9zb2Z0LtJG7wC2QVBJIMdNc%2BQAoWVudW3oARNzIHsKICDELjIwMjQtMTAtMDEtcHJldmlld8g1xDQgIEB1c2VEZXBlbmRlbmN5KPUBLy7IVi52MV8wX1DGSF8xKcRAYXJtQ29tbW9uVOQBz8cq10jLKctUNcRIYPIAqWAsCu0AxTH%2FAMX%2FAMX%2FAMX%2FAMX%2FAMXtAMXsAKntAMU1LTAzLTAx%2FwC9%2FwC9%2FwC9%2FwC9%2FgC95wChYCwKfeYCbUHoAm%2FrAogg6AOq5ADAbW9kZWwgRW1wbG95ZWUgaXMgVHJhY2tlZMh6PMgcUHJvcGVydGllcz7lAp4uLukAnuQDUFBhcmFtZXRlcskxPjvoAIbJX3DJRNJ8ymDpAv1BZ2Ugb2YgZcg%2F5gFwcmVtb3brA4Au9AG35QFacmXkA5pkRnJvbd4uLCAiYWdlIsQ1Zm9ybWVyQWdlPzogaW50MzI76AIB9gCOYWRk%2FwCM5QCMYcpRID0gMjHJVkNpdHnSV2NpdHk%2FOiBzdHLlBR7HLFByb2ZpbPQAhmVuY29kZSgiYmFzZTY0dXJs5QDMcMYwPzogYnl0ZXPJSFRoZSBzdGF0dXPES3RoZSBsYXN0IOQBgGF0aW9u5QUxICBAdmlzaWJpbGl0eShMaWZlY3ljbOQCk2Fkx13EIOUFwFN0YXTEZ%2BUCZswU6QIDxHPMMuUAgOUAymHpApDFd0Bscm%2FEO3VzCnVu5ANl0VTlAh%2FmARrpA53EX8hHIGNyZcQncmVxdWVzdCBoYXMgYmVlbiBhY2NlcHRlZMRnICBBxw46ICLICyLWUGnEQOQAtOkAwchE7ACcOiAizA%2FaTHVwZGF0xE%2FFQ1XHDjogIsgLyjvpBtrpAMTmANxk5wGiU3VjY2VlZOUAxckM0z%2FFNuQBTWZhaWzJPkbFDTogIsYJ3Dh3YXMgY2FuY2XKPkPHD%2BQHMscL%2FwFAIGRlbGXpAYBExA3mAPnICyLpBLjpBDLkA%2BvpAcrpBDRNb3ZlUscV6AQtxHNtb3bEaGZyb20gbG9j5gC8xW7EE%2FEDUMszdG%2FPMXRvyi%2F3AJVzcG9uc%2BsFRuYAlscW7ACX7gNsxT7FZMZ85gLuzW5pbnRlcmbkCFFP6AOUcyBleHRlbmRz9gkTLsspe30K5QjvyCPKG8tZ6ADN5gVuZ2V05AGnQco15APn7AXLIOcCe09y5QKn5QHWyy9Dxx1SZXBsYWNlQXN5bmPOP%2BUC99A3UGF0Y2hTzCws8wYRxUDmAj3PQOUCOmVXaXRob3V0T2vTd2xpc3RCecgwR3JvdXDPREzFIlBhcmVudNQ8U3Vic2NyaXDlAhzGO8YzzBnMOegGMCBzYW1wbOsDCWFjxUR0aGF05gIG6QXGdG8gZGlmZmXkAITvAonFKe4AskHFSO8BN%2BsDFMgN5gKL8wCSSEVBROoF78R%2BY2hlY2vqAKpleGlzdGVu5ggkIMYeRckU7wH7zR3uB%2F8%3D&e=%40azure-tools%2Ftypespec-autorest&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fresource-manager%22%5D%7D%7D)



# Entity Framework Emitter?

## Question 
Has anyone built an Entity Framework Emitter?    I'm thinking it emits a DbContext and implementation of the http-server-csharp emitter.

## Answer
I don't know enough (anything) about entity framework, but would be happy to discuss.  Would want to determine if this is a separate emitter, a configuration option (like asp.net web app versus minimal api), or something else.
 
It is definitely on the radar to think about various kinds of back ends to an emitted asp.net service
 



# Proper way to add x-ms-examples examples to swagger

## Question 
When I add my examples like this in my pr https://github.com/Azure/azure-rest-api-specs-pr/pull/21080, the pr breaks. Any guidance?

## Answer
are you saying that the x-ms-examples are not included? Make sure everything is correctly configured as this doc explain https://azure.github.io/typespec-azure/docs/migrate-swagger/faq/x-ms-examples/#_top



# Response body for ResourceCreateWithServiceProvidedName?

## Question 
Quick question for the typespec experts:
Is it possible to have a response body for ResourceCreateWithServiceProvidedName operation? We don't want a put or patch operation so the other options (ResourceCreateOrUpdate and ResourceCreateOrReplace) don’t seem to be a good fit.

## Answer
The pattern for that template matches the guidelines for POST create of a resource - if you are planning something else, you should review your plans with the api vreview board

# Incorrect URL formation

## Question 
Incorrect URL formation
General : Working on typespec migration and I have the API 
"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Confluent/organizations/{organizationName}/access/default/deleteRoleBinding/{roleBindingId}"
 ```
@delete
  @action("access/default/{roleId}")
  deleteRoleBinding is ArmResourceActionSync<
    Organization,
    void,
    OkResponse | NoContentResponse,
    Parameters = {
      @path
    /** doc */
    roleId: string;
  }
```
The URL that is getting formed is like this. 
"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Confluent/organizations/{organizationName}/{roleId}/access/default/{roleId}"
How to fix this issue ? 
 
Note : We have made access as an RT which is treated as a proxy resource. 
Playground Link : Playground
 
 
URL 2 
"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Confluent/organizations/{organizationName}/{apiKeyId}/{apiKeyId}"
Generated by the tool 
```
#suppress "@azure-tools/typespec-azure-core/no-openapi" "non-standard operations"
  @delete
  @action("{apiKeyId}")
 
  deleteClusterApiKey is ArmResourceActionSync<
    Organization,
    void,
    OkResponse | NoContentResponse,
    Parameters = {
      /**
       * Confluent API Key id
       */
      @path
      apiKeyId: string;
    }
    >
```
Actual URL that we have defined earlier "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Confluent/organizations/{organizationName}/apiKeys/{apiKeyId}"
 

## Answer
This is for your first url. It falls into legacy operation.


# HTTP response codes for ArmResourceActionNoContentAsync

## Question 
For [ArmResourceActionNoContentAsync](https://azure.github.io/typespec-azure/docs/getstarted/azure-resource-manager/step04/), it defines a 202 and 204 response and final-state-via via location. The location URL is polled. When complete, our server-side returns a 200 for that location URL with a provisioningState or Succeeded.  Is a 200 a valid status code response? My understanding is that the location URL is not modeled and it is a valid response. This was worked with the generated Azure SDKs since 2021. It is not working correctly with the Azure CLI team's new Python code generator. They are saying that we have a problem in our spec, so I'm looking to clarify.

## Answer
You should be using ArmResourceActionNoResponseContentAsync.  It is no longer allowed to have 204 responses for async POST APIs without content.  The hover documentation that shows up in vscode shows this, and this would show up in RPaaS lintdiff rules later when you make your PR.
 
It is acceptable for a POST action that returns data to return a 200 with the schema, but a POST action that returns nothing should only have a single 202 response.  The StatusMonitor is typically not modeled in most cases.
 
Not sure if this is what they meant, but it is something you should fix.
 
BTW - this is assuming you are writing a new API, if you are converting an existing API you will likely need to match the swagger, although in this case, it is highly unlikely that your service actually returns a 204.
 
The python cli code generator is still in development, so I would not be surprised by a  few hiccups.


# TypeSpec JsonSchema Emitter - possible to specify API version?

## Question 
Hello,
We are looking to generate a specific set of schemas aligned with a specific API version, and therefore want to respect all usage of @@removed and @addedas specified via TypeSpec versioning. Is that currently possible?
 
Example: don't include "Foo" property in model if was removed in version I am currently generating JsonSchema for.

## Answer
https://github.com/microsoft/typespec/issues/2051


# Internal Compiler Error When Using LRO on ArmResourceActionAsync?

## Question 
Hello,
 
I am getting an Internal Compiler Error when I tried to implement a LRO operation in Typespec. The screensnip is shown here:
With the error being generated here:
 
Internal compiler error!
 
Error: Multiple responses are not supported.
    at getResponseBody (file:///C:/Repos/AVD-RDDAM/avd-rest-api-specs/node_modules/@azure-tools/typespec-azure-resource-manager/dist/src/rules/arm-post-response-codes.js:21:23)
    at validateAsyncPost (file:///C:/Repos/AVD-RDDAM/avd-rest-api-specs/node_modules/@azure-tools/typespec-azure-resource-manager/dist/src/rules/arm-post-response-codes.js:42:29)
    at root (file:///C:/Repos/AVD-RDDAM/avd-rest-api-specs/node_modules/@azure-tools/typespec-azure-resource-manager/dist/src/rules/arm-post-response-codes.js:86:29)
    at EventEmitter.emit (file:///C:/Repos/AVD-RDDAM/avd-rest-api-specs/node_modules/@typespec/compiler/dist/src/core/semantic-walker.js:365:17)
    at validateAsyncPost (file:///C:/Repos/AVD-RDDAM/avd-rest-api-specs/node_modules/@azure-tools/typespec-azure-resource-manager/dist/src/rules/arm-post-response-codes.js:42:29)
    at root (file:///C:/Repos/AVD-RDDAM/avd-rest-api-specs/node_modules/@azure-tools/typespec-azure-resource-manager/dist/src/rules/arm-post-response-codes.js:86:29)
    at EventEmitter.emit (file:///C:/Repos/AVD-RDDAM/avd-rest-api-specs/node_modules/@typespec/compiler/dist/src/core/semantic-walker.js:365:17)
s/arm-post-response-codes.js:86:29)
    at EventEmitter.emit (file:///C:/Repos/AVD-RDDAM/avd-rest-api-specs/node_modules/@typespec/compiler/dist/src/core/semantic-walker.js:365:17)
ker.js:365:17)
    at listener.<computed> [as root] (file:///C:/Repos/AVD-RDDAM/avd-rest-api-specs/node_modules/@typespec/compiler/dist/src/core/semantic-walker.js:63:26)
    at Object.emit (file:///C:/Repos/AVD-RDDAM/avd-rest-api-specs/node_modules/@typespec/compiler/dist/src/core/semantic-walker.jssemantic-walker.js:63:26)
    at Object.emit (file:///C:/Repos/AVD-RDDAM/avd-rest-api-specs/node_modules/@typespec/compiler/dist/src/core/semantic-walker.js    at Object.emit (file:///C:/Repos/AVD-RDDAM/avd-rest-api-specs/node_modules/@typespec/compiler/dist/src/core/semantic-walker.js:81:49)
    at navigateProgram (file:///C:/Repos/AVD-RDDAM/avd-rest-api-specs/node_modules/@typespec/compiler/dist/src/core/semantic-walker.js:14:13)
:81:49)
    at navigateProgram (file:///C:/Repos/AVD-RDDAM/avd-rest-api-specs/node_modules/@typespec/compiler/dist/src/core/semantic-walker.js:14:13)
r.js:14:13)
    at Object.lint (file:///C:/Repos/AVD-RDDAM/avd-rest-api-specs/node_modules/@typespec/compiler/dist/src/core/linter.js:113:9)  
    at compile (file:///C:/Repos/AVD-RDDAM/avd-rest-api-specs/node_modules/@typespec/compiler/dist/src/core/program.js:120:38)    
    at async compileOnce (file:///C:/Repos/AVD-RDDAM/avd-rest-api-specs/node_modules/@typespec/compiler/dist/src/core/cli/actions/compile/compile.js:36:25)
 
Repo for reference: [Alecb typespecmigration 20240808-preview by alec-baird · Pull Request #31722 · Azure/azure-rest-api-specs](https://github.com/Azure/azure-rest-api-specs/pull/31722)
## Answer
### Solution:
   - Use NoResponse for your operation to prevent Typespec from assuming there should also be a 200 OK response.
   - Don't manually add an ArmAcceptedResponse unless you need to customize headers.
   - The template will handle the 202 response automatically with final-state-via: location.
### About casing issue:
   - The Typespec OpenAPI emitter incorrectly PascalCases operation IDs.
   - To work around this now, explicitly set the @operationId decorator to preserve your intended casing.
   - A more permanent fix will require a tooling update — you can file a GitHub issue to help track this.


# Issue with Implementing Options Bag in Java

## Question 
Hi team,
 
I'm on Azure Batch, and we're working on integrating the "options bag" feature into our TypeSpec for our Java SDK. It looks like this decorator override is the solution for the options bag: [Decorators | TypeSpec Azure](https://azure.github.io/typespec-azure/docs/libraries/typespec-client-generator-core/reference/decorators/#@Azure.ClientGenerator.Core.override)
 
I'm following the docs to get started with it but am running into an error. I am trying to just reproduce the very simple example that is at that link. I am trying to make an options bag for listJobs. I added this to the client.tsp:
 
```
@override(Azure.Batch.Jobs.listJobs, "java")
op listJobs(params: Azure.Batch.ListBatchJobsOptions): void;
```
And then I modified our listJobs in routes.tsp like this (just to simplify it and mimic the example):
 
```
@summary("Lists all of the Jobs in the specified Account.")
  @doc("Lists all of the Jobs in the specified Account.")
  @route("/jobs")
  @clientName("listJobsInternal", "java")
  listJobs is ListOperation<
    {
      foo: string;
      bar: string;
    },
    void
  >;
```
 
And then added this model to models.tsp:
models.tsp
 
```
model ListBatchJobsOptions {
  foo: string;
  bar: string;
}
```
 
But I am getting this error:
Method "listJobs" is not directly referencing the same parameters as in the original operation. The original method has parameters "bar", "foo", while the override method has parameters "bar", "foo".TypeSpec(@azure-tools/typespec-client-generator-core/override-method-parameters-mismatch)
 
I am really confused about where this error is coming from since both of them have identical parameters "bar" and "foo" with identical types. Can someone help me understand what's going on? Thanks!

## Answer
Discussion continues in Java channel
[Sanjana Kapur: Issue with Implementing Options Bag]()


# Build Checks for private.rest-api-specs-pr Failing – Need Help Debugging

## Question 
Hi Team,
The build checks for private.rest-api-specs-pr are failing, but I couldn't find the exact reason for the failure. Can someone help point me in the right direction or identify what might be causing this issue?
Any insights would be appreciated. Thanks!
https://github.com/Azure/azure-rest-api-specs-pr/pull/21172

## Answer
The check is failing by design, to prevent any PRs being merged to specs-pr/main.  This branch cannot be merged to.  It's only purpose is to create test PRs in private before creating the PR in public.


# Remove status 200? 

## Question 
Hello!
 
We are trying to move private API specs to public, but we are getting some lint error because some of our delete actions have status code 200 and we should only have 202, 204 and default.
 
How can we remove that status 200 when generating the openapi.json file?
 
On the main.tsp I see we have 
 ```
@doc("Workload Interface")
@armResourceOperations
interface Workload {
  get is ArmResourceRead<WorkloadResource>;
  @useFinalStateVia("azure-async-operation")
  createOrUpdate is ArmResourceCreateOrReplaceAsync<
    WorkloadResource,
    LroHeaders = Azure.Core.Foundations.RetryAfterHeader &
      ArmAsyncOperationHeader
  >;
  update is ArmResourcePatchAsync<WorkloadResource, WorkloadProperties>;
  #suppress "@azure-tools/typespec-azure-resource-manager/arm-delete-operation-response-codes" "For backward compatibility"
  #suppress "deprecated" "Backwards compatibility"
  delete is ArmResourceDeleteAsync<WorkloadResource>;
  listByEnclaveResource is ArmResourceListByParent<WorkloadResource>;
  listBySubscription is ArmListBySubscription<WorkloadResource>;
}
```
## Answer
use the ArmResourceDeleteWithoutOkAsync template for delete, as in [this playground](https://azure.github.io/typespec-azure/playground/?options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fresource-manager%22%5D%7D%7D&sample=Azure+Resource+Manager+framework)


# Deep link in playground to sample and emitter?

## Question 
Can I use a querystring to deeplink into a sample and emitter in the PlayGround? 

## Answer
You can do this for your own sample typespec [like this](https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7DQoNCnVzaW5nIFR5cGVTcGVjLkh0dHA7DQpAc2VydmljZSh7DQogIHRpdGxlOiAiV2lkZ2V0IFPGHCIsDQp9KQ0KbmFtZXNwYWNlIERlbW%2FHHcVcbW9kZWwgxzbFSkB2aXNpYmlsaXR5KCJyZWFkIiwgInVwZGF0ZSIpxSFwYXRoxAlpZDogc3RyaW5nxUsgIHdlaWdodDogaW50MzI7xBJjb2xvcjogInJlZCIgfCAiYmx1ZeQA1X3EMUBlcnJvcugAhEXEDeYAg2NvZGXMRm1lc3NhZ2XLbsZAcm91dGUoIi935QC%2Fc%2BQAn0B0YWco5wEGxRFpbnRlcmbkAPvHFcZjQOQA8Gxpc3QoKTrHGltdIHzmAIbFdcUi5AEAKOUA8esA7skyzjBwb3N0IGNyZWF0ZSguLi7GI9gsYXRjaCDmAVbfLSBAZGVsZXRlIMYH9ACOdm9pZM4z5wEae2lkfS9hbmFseXplIinnAKPHENRJxgnLS30NCg%3D%3D&e=%40typespec%2Fopenapi3&options=%7B%22options%22%3A%7B%22%40typespec%2Fopenapi3%22%3A%7B%22openapi-versions%22%3A%5B%223.1.0%22%5D%7D%7D%7D)


# Multiple Continuation Token

## Question 
Just checking to be sure, is something like that valid?
 
```TypeScript
@list op listPets(
      @continuationToken @header token?: string,
      @continuationToken @query foo?: string
  ): {
  @pageItems pets: Pet[];
  @continuationToken token?: string;
  @continuationToken foo?: string
};
```
IOW, multiple continuation token, with some form of inference to know how to connect them (here, I used the same name, but I could be more evil).
 
Brian ?

## Answer
no, must be a single one https://typespec.io/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7Cgp1c2luZyBUeXBlU3BlYy5IdHRwOwpAc2VydmljZSh7CiAgdGl0bGU6ICJXaWRnZXQgU8YbIiwKfSkKbmFtZXNwYWNlIERlbW%2FHGzsKCm1vZGVsIFBldCDEQkB2aXNpYmlsaXR5KCJyZWFkIiwgInVwZGF0ZSIpxCBwYXRoCiAgaWQ6IHN0cmluZzsKCiAgd2VpZ2h0OiBpbnQzMjsKICBjb2xvcjogInJlZCIgfCAiYmx1ZSI7Cn0KCkBlcnJvcsd3RcQMxXljb2Rly0BtZXNzYWdlymXEOmxpc3Qgb3AgxAhQZXRzKOQAkWNvbnRpbnVhdGlvblRva2VuIEBoZWFkZXIgdMQOP8hDLNctcXVlcnkgZm9vyyopOuYBDXBhZ2VJdGVtcyBwZXRzOuQBJltd5ACf00nObtclzGc7Cn07Cg%3D%3D&e=%40typespec%2Fopenapi3&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40typespec%2Fhttp%2Fall%22%5D%7D%7D


# Compile warning for properties which already contains the provisioningState property

## Question 
 
General
 : Can anyone help resolve this typespec validatoin error. 
[Microsoft.confluent typespec 1601 by DeepikaNMS · Pull Request #20936 · Azure/azure-rest-api-specs-pr](https://github.com/Azure/azure-rest-api-specs-pr/pull/20936) [draft PR]
 
```Plain Text
@doc("An Organization Resource by Confluent.")
model Organization
  is Azure.ResourceManager.TrackedResource<OrganizationResourceProperties> {      
      ...ResourceNameParameter<Organization>,

  ...ManagedServiceIdentityProperty;
  }
/**
* Organization resource property
*/
model OrganizationResourceProperties {
  /**
   * The creation time of the resource.
   */
  @visibility("read")
  // FIXME: (utcDateTime) Please double check that this is the correct type for your scenario.
  createdTime?: utcDateTime;
  /**
   * Provision states for confluent RP
   */
  @visibility("read")
  provisioningState?: ProvisionState;
  /**
   * Id of the Confluent organization.
   */
  @visibility("read")
  organizationId?: string;
  /**
   * SSO url for the Confluent organization.
   */
  @visibility("read")
  ssoUrl?: string;
  /**
   * Confluent offer detail
   */
  offerDetail: OfferDetail;
  /**
   * Subscriber detail
   */
  userDetail: UserDetail;
  /**
   * Link an existing Confluent organization
   */
  linkOrganization?: LinkOrganization;

}
```
The organizationProperties model has the provisioningState but still i get the warning 
```Text
The RP-specific property model in the 'properties' property of this resource must contain a 'provisioningState property.  The property type should be an enum or a union of string values, and it must specify known state values 'Succeeded', 'Failed', and 'Canceled'.
> 508 | model Organization
      |       ^^^^^^^^^^^^
 ```

## Answer
It is saying this one. 
```
model ValidationModel is Azure.ResourceManager.ProxyResource<Organization>
```
The model argument for ProxyResource should be properties model not a resource model.
 


# Renaming old enum names for SDK

## Question 
TypeSpec Discussion
 : Hi team,
Our GA Swagger (and the underlying SDK) had some enum names that had abbreviations or acronyms etc. (e.g. PFAction). However, now we have moved to Typespec and the new SDKs based off of those generate the enum models as well (earlier SDKs from the GA APIs didnt have models).
Given this, in the SDK review there are suggestions to rename the enum names to better user friendly names which we agree with. However, I would rather prefer to rename the actual enum to a more meaningful name (e.g. PassFailAction) instead of going the client customization route. These will show up in all of our Swagger versions but I dont think its a breaking change since model type names dont make any sense for API users. 
 
Given this, is it better to rename enum type names in the actual specification or should we stick to client customizations route?

## Answer
Rename in the typespec. While some breaking change tools may trigger, we know there are many false positives when you do a conversion. Assuming you are not changing what goes on the wire, of course!


# TypeSpec generate SDK parameter ordering

## Question 
Hi TypeSpec team
 
We just went through the C# SDK review and the reviewer pointed out the order of the parameter may not be friendly to use, we want to ask how is the parameter order determined, and is there any way to specify/customize ordering of the parameters?
 
This is what we are expecting to have the required field conversationId having the ID as the first param
 
```Plain Text
public virtual Response<AddParticipantsResult> AddParticipants(string conversationId, AddParticipantsRequest body, CancellationToken cancellationToken = default); 
public virtual Response AddParticipants(string conversationId, RequestContent content, RequestContext context = null); 
public virtual Task<Response<AddParticipantsResult>> AddParticipantsAsync(string conversationId, AddParticipantsRequest body, CancellationToken cancellationToken = default); 
public virtual Task<Response> AddParticipantsAsync(string conversationId, RequestContent content, RequestContext context = null);
 ```
 
But for some other API the participantId ID is not the first parameter eg.
 
```Plain Text
public virtual Pageable<Conversation> GetConversations(int? maxpagesize = null, string participantId = null, Guid? channelId = null, CancellationToken cancellationToken = default); 
public virtual Pageable<BinaryData> GetConversations(int? maxpagesize, string participantId, Guid? channelId, RequestContext context); 
public virtual AsyncPageable<Conversation> GetConversationsAsync(int? maxpagesize = null, string participantId = null, Guid? channelId = null, CancellationToken cancellationToken = default); 
public virtual AsyncPageable<BinaryData> GetConversationsAsync(int? maxpagesize, string participantId, Guid? channelId, RequestContext context);
 ```
Is there any way to specify/customize the order when generating the SDK from TypeSpec?
 
Here is our TypeSpec https://github.com/Azure/azure-rest-api-specs/pull/32411
and our C# api view https://spa.apiview.dev/review/559f6501a7504af9be4dfe7eb9b80bbe?activeApiRevisionId=e8f3642672a8482eb163714eeab8a3d3&diffApiRevisionId=760762805c364dd59892c816924471a1&nId=Azure.Communication.Messages.ConversationManagementClient
 

## Answer
Ground rule: required parameters must go first, before any optional parameters, this is C# syntax, we cannot do anything about it.
Besides that, the order of parameters is determined by which one appears first in typespec. For example:
```
alias Parameters = {
   ...Something;
   p1?: string;
}
 
op foo(...Parameters): void;
 
in foo, anything comes from Something will go first in the parameter list, followed by p1.
If I change it to:
alias Parameters = {
   p1?: string;
   ...Something;
}
```
now p1 is the first parameter.
 
Your spec is quite complicated therefore I did not really find which part the APIs in your question are defined. But since you are using templates, the parameter's order kind of depends on how the template is implemented, because as I said above, the order of the parameters is the order of which one appears first in the typespec with the same optionality.
 
You could take it in this way: you have an order of parameters as they defined in typespec, in C# generator, we do a stable sorting on those parameters to let the required ones go first.


# Record key validation

## Question 
https://github.com/microsoft/typespec/discussions/5990

## Answer
There currently is not a mechanism for this, though one could imagine a future Map type that would similarly allow using enums or custom scalars based on string to allow pattern and length constraints.

# SdkTspConfigValidation

## Question 
I'm making unrelated changes to my spec and tsv fails locally:
 ```
Executing rule: SdkTspConfigValidation
[SdkTspConfigValidation]: validation failed.
- Failed to find "parameters.service-dir.default". Please add "parameters.service-dir.default".
- Failed to find "options.@azure-tools/typespec-java.package-dir". Please add "options.@azure-tools/typespec-java.package-dir".
- Failed to find "options.@azure-tools/typespec-ts.generateMetadata". Please add "options.@azure-tools/typespec-ts.generateMetadata".
- Failed to find "options.@azure-tools/typespec-ts.hierarchyClient". Please add "options.@azure-tools/typespec-ts.hierarchyClient".
- Failed to find "options.@azure-tools/typespec-ts.experimentalExtensibleEnums". Please add "options.@azure-tools/typespec-ts.experimentalExtensibleEnums".
- Failed to find "options.@azure-tools/typespec-ts.enableOperationGroup". Please add "options.@azure-tools/typespec-ts.enableOperationGroup".
- Failed to find "options.@azure-tools/typespec-ts.package-dir". Please add "options.@azure-tools/typespec-ts.package-dir".
- Failed to find "options.@azure-tools/typespec-ts.packageDetails.name". Please add "options.@azure-tools/typespec-ts.packageDetails.name".
- Failed to find "options.@azure-tools/typespec-go.service-dir". Please add "options.@azure-tools/typespec-go.service-dir".
```
I searched for SdkTspConfigValidation in this team (across channels) but couldn't find a single mention of it.
Here's my README:
 
 
Markdown
```yaml $(swagger-to-sdk)
swagger-to-sdk:
  - repo: azure-resource-manager-schemas
  - repo: azure-sdk-for-net
  - repo: azure-powershell
  - repo: azure-cli-extensions
```
 
which is just a stub, I don't mind removing all of it for now. But I don't have any references of Java, TS, or Go. Where is it coming from?

## Answer
Rule `SdkTspConfigValidation` should generate warnings, but not errors and not cause TSV to fail.  You can ignore the warnings for now.

# Which common type to use: Resource + tags?

## Question 
Hi, I'm migrating servicefabricmanagedclusters to TypeSpec. Our NodeType resource is modeled as a "ManagedProxyResource" [azure-rest-api-specs/specification/servicefabricmanagedclusters/resource-manager/Microsoft.ServiceFabric/preview/2024-11-01-preview/nodetype.json at main · Azure/azure-rest-api-specs](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/servicefabricmanagedclusters/resource-manager/Microsoft.ServiceFabric/preview/2024-11-01-preview/nodetype.json)
 
I'd like to switch it over to using a common type, but the properties don't exactly match up with TrackedResource (extraneous location property) or ProxyResource (missing tags property). What would be the advised definition in this situation? Here is the current converted definition [azure-rest-api-specs/specification/servicefabricmanagedclusters/ServiceFabricManagedClusters.Management/NodeType.tsp at user/iliu/2024-11-01-preview-migrate · iliu816/azure-rest-api-specs](https://github.com/iliu816/azure-rest-api-specs/blob/user/iliu/2024-11-01-preview-migrate/specification/servicefabricmanagedclusters/ServiceFabricManagedClusters.Management/NodeType.tsp)

## Answer
Here you could either use ProxyResource and add a tags property (using ...TagsProperty) or you could create your own type that extends Azure.ResourceManager.CommonTypes.Resource  and add a TagsProperty. in the same way.  Either way you may need to suppress a warning about adding tags to an untracked resource, but that is fine in this case.

# Versioning of updating the interface of an operation

## Question 
I have a question about versioning.
 
In the current version, this is an operation that we have,
 ```
interface ItemsOperations {
    /** List top-level items. */
    list is ListOperation<ItemDetails>;
  }
 ```
In the new version, we add query parameters to this interface,
 ```
    list is CustomListOperation<
      Resource = ItemDetails,
      Response = PagedItemDetails,
      Parameters = {
        ...CustomFilterQueryParameter;
        ...CustomSkipQueryParameter;
        ...CustomTopQueryParameter;
        ...CustomOrderByQueryParameter;
      }
    >;
 ```
What can I do to apply versioning to these two operations in different versions? Thanks
 
I was planning to add @add to the new version, and @remove in the old version, but the error indicates there are two list operations (different versions)
 
If I remove the old version and only use the current one, I see this error, I do not know how to fix this issue either
 

## Answer
Generally, the easiest way to version such an operation is to version the data (in this case, the parameters).  [Here is an example](https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZ8wfYXp1cmUtdG9vbHMvyCstxhVjb3Jl3yvIK3Jlc291cmNlLW1hbmFnZXIiOwoKdXNpbmcgVHlwZVNwZWMuSHR0cDvQFVJlc3TRFVbpAKnIG0HEZS5Db3JlzhJSx3dNxnY7CgovKiogQ29udG9zbyDIHiBQcm92aWRlciDmAJ5tZW50IEFQSS4gKi8KQGFybcggTmFtZXNwYWNlCkBzZXJ2aWNlKHsKICB0aXRsZTogIsdYyC5IdWJDbGllbnQiLAp9KQpA5wFgZWQo5wDBcykKbshSIE1pY3Jvc29mdC7SR%2B8AuEFQSSDHTXPkAKNlbnVtIMhUIOQAksQuMjAyMS0xMC0wMS1wcmV2aWV3yDXENCAgQHVzZURlcGVuZGVuY3ko9QEx6AFrcy52MV8wX1DGSF8xKdhA5AGD1zUyxTVhcm1Db21tb27kAc5zxyrXfcspy1Q1xEhg8gDeYCwK6gD6NP8A%2Bv8A%2Bv8A%2Bv8A%2Bv8A%2Bv8A%2Bv8A%2BuQA%2Bu8A3mAsCn3mAiJB6AIk6wI9IOgDfOQA%2FW1vZGVsIEVtcGxveWVlIGlzIFRyYWNrZWToAII8yBxQcm9wZXJ0aWVzPuUCUy4u6QCm5AMHUGFyYW1ldGVyyTE%2BO%2BgAhslfcMlE0nzKYOkCskFnZSBvZiBlyD%2FlAa1hZ2U%2FOiBpbnQzMjvoAeFDaXR50ipjaXR5Pzogc3Ry5QQaxyxQcm9maWzTWUBlbmNvZGUoImJhc2U2NHVybCLkAWBwxjA%2FOiBieXRlc8lIVGhlIHN0YXR1c8RLdGhlIGxhc3Qg5ADFYXRpb27lBC0gIEB2aXNpYmlsaXR5KCJyZWFkyFXEGOUEtFN0YXTEX%2BUBo8wU6QFAxGvMMsV45QDCYekBzcVvQGxyb8Q7dXMKdW7kAt%2FRVOUBXOYBEukDH8RfyEcgY3JlxCdyZXF1ZXN0IGhhcyBiZWVuIGFjY2VwdGVkxGcgIEHHDjogIsgLItZQacRA5AC06QDByETsAJw6ICLMD9pMdXBkYXTET8VDVccOOiAiyAvKO%2BkFzukAxOYA3GTnAZpTdWNjZWVk5QDFyQzTP8U25AFNZmFpbMk%2BRsUNOiAixgncOHdhcyBjYW5jZco%2BQ8cP5AYlxwv%2FAUAgZGVsZekBgETEDeYA%2BcgL5AZV7wNvbW926gHK6QNxTW92ZVLHFegDasRzbW92xGhmcm9tIGxvY%2BYAvMVuxBPxA0jLM3RvzzF0b8ov9wCVc3BvbnPrBIPmAJbHFuwAl%2B4DZMU%2BxWTGfOYC7s1uYWxpYXMgUXVlcnnpBJVzID3NVW51bWJlcsRVaXRlbXPkAMJza2lwxlpAYWRk6weVLvQFYuUFwnHEZOQDUGtpcPIEacRobWF4aW11bdRwcmV0dXLoBoHfctFydG%2FMcX07CgppbnRlcmbkCDlP6ASCcyBleHRlbmRz9gj9Lsspe30K5QjZyCPKG8tZ6AHD5gWhZ2V05AKdQco1UmVhZOwF%2FiDnA3FPcuUDneUCzMsvQ8cdUmVwbGFjZUFzeW5jzj%2FlA%2B3QN1BhdGNoU8wsLPMGRMVA5gMzz0DlAzBlV2l0aG91dE9r03dsaXN0QnnIMEdyb3Vwz0RMxSJQYXJlbnQ8CiAgIOkAgizFDu0CeO8CigogIMtmU3Vic2NyaXDlAzzGZcZdzBnsAJ%2FoBxsgc2FtcGzrBClhY8VEdGhhdOYDJukG3nRvIGRpZmZl5ACu7wOpxSnuANxBxUjvAWHrBDTIDeYDq%2FMAkkhFQUTqBwfEfmNoZWNr6gCqZXhpc3RlbuYIgSDGHkXJFO8CJc0d7ghc&e=%40azure-tools%2Ftypespec-autorest&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fresource-manager%22%5D%7D%7D) of how you would do this in ArmResourceListByParent, but using the Parameters prameter of any operation template shoudl work similarly.

# TypeSpec Versioning Question

## Question 
I have a question about TypeSpec versioning decorators.
Let's say we start with this:
 
```TypeScript
enum Versions {
  v1,
  latest,
}
model Model {
  @added(Versions.latest)
  foo: string;
}
 ```
Later, we add a v2 version (before latest),  updating the versions enum to:
 
```TypeScript
enum Versions {
  v1,
  v2,
  latest,
}
 ```
We also update the decorator on foo property from latest to v2:
 
```TypeScript
model Model {
  @added(Versions.v2)
  foo: string;
}
 ```
In this case, will both v2 version and latest version include the foo property?

## Answer
Yes, The version enum is ordered. 
 

# Using @client decorator in the Client.tsp prevents other clients from generating

## Question 
Using @client decorator in the Client.tsp prevents other clients from generating
Hi All, I'm trying to merge two generated clients into one in my Client.tsp, so I'm following the One client and two operation groups docs to do so. However, I noticed that using a @client decorator will stop the other clients that I had before from being generated.
 
I want to understand if this is the expected behavior and I have to explicitly specify all of the clients that I want, or if there's a better way to accomplish my goal.
 
For context, here is what my Client.tsp looks like:
 ```
@client(
    {
      name: "KeyVaultAccessControlRestClient",
      service: KeyVault,
    },
    "csharp"
  )
  @operationGroup
    interface RoleDefinitions{
        deleteRoleDefinition is KeyVault.RoleDefinitions.delete;
        createOrUpdateRoleDefinition is KeyVault.RoleDefinitions.createOrUpdate;
        getRoleDefinition is KeyVault.RoleDefinitions.get;
        listRoleDefinitions is KeyVault.RoleDefinitions.list;
    }
  @operationGroup
    interface RoleAssignments{
        deleteRoleAssignment is KeyVault.RoleAssignments.delete;
        createRoleAssignment is KeyVault.RoleAssignments.create;
        getRoleAssignment is KeyVault.RoleAssignments.get;
        listRoleAssignments is KeyVault.RoleAssignments.listForScope;
    }
```
By doing this, I get a KeyVaultAccessControlRestClient generated client  but I lost my KeyVaultRestClient that was also generated before 
 
CC: Isabella Cai, Chenjie Shi, Catalina Peralta
Clients | TypeSpec Azure
 

## Answer
it is expected and mentioned in doc. when you use explicit @client, you need to define all clients explicitly. we are not able to infer some clients from ns/interface, some from @client.

# Optional path params

## Question 
Hello is there a way to specify optional path params in TSP :
E.G.
Based on the comment The secret-version param is optional. As it stands right now it is required, which is less than desirable.
```
/**
* The GET operation is applicable to any secret stored in Azure Key Vault. This operation requires the secrets/get permission.
*/
#suppress "@azure-tools/typespec-azure-core/use-standard-operations" "Foundations.Operation is necessary for Key Vault"
@summary("Get a specified secret from a given key vault.")
@route("/secrets/{secret-name}/{secret-version}")
@get
op getSecret is KeyVaultOperation<
  {
    /**
     * The name of the secret.
     */
    @path("secret-name")
    @clientName("name", "go")
    secretName: string;
 
    /**
     * The version of the secret. This URI fragment is optional. If not specified, the latest version of the secret is returned.
     */
    @path("secret-version")
    @clientName("version", "go")
    secretVersion: string;
  },
  SecretBundle
>;
```
## Answer
it is not supported yet but is something we'll do in the future, you'll just have to have the parameter respect the uri template syntax https://github.com/microsoft/typespec/issues/4126  
Allowing it in typespec is also just he easy step, client emitters will also need to add support

# .NET generation not creating types that are in new @added version 

## Question 
Hi team,
 
We have a new version c2025_03_01_Preview in our TypeSpec https://github.com/Azure/azure-rest-api-specs/pull/32411 , but when generating the .NET code, it does not create the all the types types for some reason
 
Not sure if this has to do with preview not picking up everything in GA version or something 
```Plain Text
@doc("Azure Communication Messages Versions")
enum Versions {
  @doc("Azure Communication Messages 2024-02-01 api version")
  @useDependency(Azure.Core.Versions.v1_0_Preview_2)
  c2024_02_01: "2024-02-01",
  @doc("Azure Communication Messages 2024-08-30 api version")
  @useDependency(Azure.Core.Versions.v1_0_Preview_2)
  c2024_08_30: "2024-08-30",
  @doc("Azure Communication Messages 2025-03-01-preview api version")
  @useDependency(Azure.Core.Versions.v1_0_Preview_2)
  c2025_03_01_Preview: "2025-03-01-preview",
}
 ```
 
Similar issue when trying to generate JS SDK. Any assistance is appreciated, thank you!

## Answer
I think i figured it out, i was using tsp-client to generate from local typespec file, but when using dotnet build t:GenerateCode reading from tsp-location in remote repository it works, but kind of weird they work differently

# Is it useful to use prism mock to test azure-rest-api-specs-pr?
## Question 
've been implementing a private RP and wondering what is a recommended way to test the json? Is it necessary to merge changes to RPaasDev and test using real ARM endpoints, or is there a suggested mock which can be used? I see "oav run" is available, but I don't see any scenarios in the tree, so I wasn't sure if this was used just for fuzzing; I'm not there yet.
 
I've been experimenting with prism, but I've encountered some odd issues and I'm not sure if they are prism-specific, or issues in my typespec. How do others test validations?

## Answer
   - For new Azure Resource Providers (RPs), you must not edit the generated Swagger manually; all edits must be made through TypeSpec.
   - The generated Swagger is treated as a build artifact.
   - Validation rules (like Kubernetes CRD validations) should be implemented directly in TypeSpec.
   - Mocking ARM (e.g., using Prism and pytest) is reasonable for testing, but it's not officially documented or common practice among spec authors.
   - This is more a question for the ARM team, not the Azure SDK team.You can reach them via ARM Office Hours.
   - It’s encouraged to update others with what you learn.

# How to keep service code consistent with TypeSpec definition?

## Question 
I have a question regarding maintaining consistency between service code and TypeSpec definitions.
Currently, our TypeSpec definitions are written manually, but I’m concerned that as the service evolves, the definitions may start to drift from the actual code implementation. This could lead to mismatches and potential errors down the line.
Could you provide recommendations or best practices for keeping the service code in sync with the TypeSpec definition? Is there a way to automate the process of verifying that the code matches the TypeSpec, or to better integrate the TypeSpec definitions directly with the service code to reduce the chances of divergence?
Any guidance on strategies for keeping these two aligned would be greatly appreciated.
Thank you!

## Answer
Well, eventually the answer is code and maybe even test generation from typespec itself.  For ARM services, live validation occurs based on the emitted swagger, so you get some assurance 

# The azure-sdk-for-js Pipeline Failed 

## Question 
Hi Team,
 
I am using the azure-sdk-for-js pipeline to generate the JavaScript SDK, but it is failing due to a missing package.json. I am not sure which specific package.json file is required in this context.
 
Could anyone provide some insights on this? Thank you! [DPG TypeScript
add Python, JS, Java emitter for authoring by amber-ccc · Pull Request #32454 · Azure/azure-rest-api-specs](https://github.com/Azure/azure-rest-api-specs/pull/32454/checks?check_run_id=36689530921)

## Answer
could you double confirm the package dir is by design? Currently "_" in package-dir is not allowed, and typical pacakge-dir should be similar to this:

```
package-dir: "ai-content-safety-rest"
packageDetails:
      name: "@azure-rest/ai-content-safety"
```
[azure-sdk-for-js/rush.json at f85470533f728899ad50518b7aeddbde65b8bc4f · azure-sdk/azure-sdk-for-js](https://github.com/azure-sdk/azure-sdk-for-js/blob/f85470533f728899ad50518b7aeddbde65b8bc4f/rush.json#L2332)

azure-sdk/azure-sdk-for-js
rush.json
```
      "projectFolder": "sdk/cognitivelanguage/azure",
```
View Code in GitHub


# [Python Generation] Access Settings Being Ignored

## Question 
Hi 
TypeSpec Discussion
, hoping someone can help me understand some weird behavior I'm seeing when attempting to generate our Python SDK, or point me to the right place to ask.
 
As we've been developing the betas for our SDK, we've had a couple of different typespec branches which we've be using for different languages, due to various idiosyncrasies in how the different language generators work. We're now in the process of attempting to merge them, and running into an issue, which I've narrowed down to happening when I attempt to change one of our aliases to a model.
 
I've created a draft PR [here]((https://github.com/Azure/azure-rest-api-specs/pull/32513/files)) to make it easier to see the specific change, or the full spec can be seen [here](https://github.com/trangevi/azure-rest-api-specs/tree/trangevi/typespec-debugging/specification/ai/ModelClient). When I make that change, and regenerate the python SDK, the generator makes a number of models that we have public, despite the fact that they are all explicitly set to internal in our client.tsp. Again, for simplicity on seeing the change, a draft PR for the python code is [here](https://github.com/Azure/azure-sdk-for-python/pull/39629/files#diff-a0f9254a115c9b9fe8c4d33bab5b8e7e3d3518c462716744b138d3da4c03eb2c), and the full package is [here](https://github.com/Azure/azure-sdk-for-python/tree/trangevi/typespec-debugging/sdk/ai/azure-ai-inference). Most of those classes you can see the explicit internal specification at this place in the [client.tsp](https://github.com/trangevi/azure-rest-api-specs/blob/trangevi/typespec-debugging/specification/ai/ModelClient/client.tsp#L88) file, the others are nearby.
 
To add to the confusion, the object which I am attempting to change from an alias to a model does not seem to be added to the generated python code (which makes sense, it's not attempting to use that object, but rather the spread of the properties). But none of the models which are being made public reference or are referenced by the alias.
 
So, I'm hoping someone might understand what is happening here and why our settings seem to be ignored due to a seemingly unrelated change. Thanks in advance!

## Answer
I understand your question is: Why some models are still marked as public  after changing alias to model even if they are decorated with @internal?
Let me introduce some context here: whether models are exposed publicly depends on whether it is referred by other public op / models. If a model is referred by other public models or ops, it is still overrided to public even if the model is marked with decorator @internal. Since ChatCompletionsOptions is changed from alias to model, all type of its properties is considered as public models according to up logic. 
Add Chenjie Shi for awareness.

# Correct way to specify duration in Specs
## Question 
Hi 
TypeSpec Discussion
,
Certain duration properties in our API, following API guidance are modeled as integers with explicit unit in the name. However, in SDK review there are recommendations to convert those property to the language's equivalent duration format (e.g. TimeSpan in .NET, Duration in Java).
 
Given I can not specify encoding via alternate type, my understanding is the following is the only way to do it where we specify encoding in the API part of the spec, name the property with unit in it and then rename it for the languages:
 
```TypeScript
// In spec.tsp
@encode(DurationKnownEncoding.seconds, int32)
durationInSeconds: duration;
// In client.tsp
@@clientName(durationInSeconds, duration, "csharp,java");
```
Is this the correct approach to go about it? Asking since this will always result in a rename for all such properties

## Answer
### You are on the right path.
   - REST API should use explicit names with units (e.g., durationInSeconds) to clearly communicate what the client must provide.
   - SDKs should abstract serialization details to make APIs easier for developers, e.g., expose a TimeSpan/Duration without requiring users to handle units manually.
   - TypeSpec fully supports both int+unit and ISO-8601 duration formats; SDK codegen can map between wire format and SDK type using things like @clientName.
   - There is no contradiction: it’s intentional that REST APIs are explicit while SDKs provide a cleaner developer experience.
   - Consistency between Data Plane (DP) and ARM (Control Plane) is ideal but not required right now. Historically, ARM uses ISO-8601 for durations, but DP tends to prefer simple int+unit fields unless complex recurrence expressions are needed.

# All resource must have a delete operation ?

## Question 
There is a rule "no-resource-delete-operation" validating all the resource should have a delete operation. However, from this page, I only saw tracked resource must support a delete operation, while proxy resource only should support a delete operation, it's not a must. If that is the case I think we should modify this rule. Our customer is a [proxy resource](https://github.com/Azure/azure-rest-api-specs/blob/918c561c1a43a2ba9bbde91ff515be65307a9ab8/specification/storage/resource-manager/Microsoft.Storage/stable/2024-01-01/storage.json#L5019) and doesn't have a delete operation.  I think it [doesn't violate any guidance](https://github.com/Azure/azure-rest-api-specs/blob/918c561c1a43a2ba9bbde91ff515be65307a9ab8/specification/storage/resource-manager/Microsoft.Storage/stable/2024-01-01/storage.json#L2160). Please correct me if I missed any document related to it.

## Answer
✅ Rules are designed for greenfield APIs:
Linting rules encourage best practices for new (greenfield) APIs.
Brownfield APIs (existing ones) may need to suppress rules if they don't align due to historical reasons.

✅ Create requires Delete:
Any resource with a create operation should also have a delete operation.
Exceptions may exist for read-only or singleton patterns.

✅ Handling Brownfield APIs:
It's a good idea to create a separate ruleset or allow automatic suppressions for brownfield services.
This keeps greenfield APIs aligned with best practices without burdening older APIs.

✅ Raising Issues is Valuable:
Pointing out pain points helps improve the system.
You correctly opened an issue to track the idea of a brownfield ruleset.

# Breaking error location property

## Question 
General
: 
Need some help to resolve the breaking error. In the typespec 
I haven;t added any location properties so not sure how this is coming up 
 
The new version lists new non-read-only properties as required: 'location'. These properties were not listed as required in the old version.
 
[Microsoft.confluent typespec 1601 by DeepikaNMS · Pull Request #20936 · Azure/azure-rest-api-specs-pr](https://github.com/Azure/azure-rest-api-specs-pr/pull/20936/checks?check_run_id=37698049776)
## Answer
✅ TrackedResource includes required location and tags properties.

✅ If you don't want location and tags, use ProxyResource instead.

✅ You have Avocado validation errors that need to be fixed to align with ARM RPC requirements.

# Is there any simplified way to create several operation for one specific resource?

## Question
I have one specific resource with URL path: /configurations/event-hub
That means event-hub is one specific resource under configurations.
 
Currently, I write code like below, and does anyone can help review to see if I can refactor my code with any other simplified way? Because all the 4 operations are based on the same route: /configurations/event-hub, and they are the PUT/GET/DELETE/POST action for the same resource.
 
 
#suppress "@azure-tools/typespec-azure-core/use-standard-operations" "Using custom operation signature for specific requirements"
@added(ApiVersions.v2025_05_20)
@doc("Create or replace EventHub configuration.")
@route("/configurations/event-hub")
@put
op createOrReplaceEventHubConfig(
 
  @doc("API version parameter")
  @query("api-version") apiVersion: string,
 
  @doc("EventHub configuration.")
  @added(ApiVersions.v2025_05_20)
  @body body: EventHubConfig
): {
  @statusCode status: 200;
 
  @doc("EventHub configuration.")
  @added(ApiVersions.v2025_05_20)
  @body body: EventHubConfig;
};
 
#suppress "@azure-tools/typespec-azure-core/use-standard-operations" "Using custom operation signature for specific requirements"
@added(ApiVersions.v2025_05_20)
@doc("Get EventHub configuration.")
@route("/configurations/event-hub")
@get
op getEventHubConfig(
  @doc("API version parameter")
  @query("api-version") apiVersion: string,
): {
  #suppress "@azure-tools/typespec-azure-core/no-closed-literal-union" "Union of literals should include the base scalar as a variant to make it an open enum. (ex: `union Choice { Yes: "yes", No: "no", string };`)."
  @statusCode status: 200 | 404;  
 
  @doc("EventHub configuration.")
  @added(ApiVersions.v2025_05_20)
  @body body: EventHubConfig;
};
 
#suppress "@azure-tools/typespec-azure-core/use-standard-operations" "Using custom operation signature for specific requirements"
@added(ApiVersions.v2025_05_20)
@doc("Delete EventHub configuration.")
@route("/configurations/event-hub")
@delete
op deleteEventHubConfig(
  @doc("API version parameter")
  @query("api-version") apiVersion: string,
): {
  @statusCode status: 204;
};
 
#suppress "@azure-tools/typespec-azure-core/use-standard-operations" "Using custom operation signature for specific requirements"
@added(ApiVersions.v2025_05_20)
@doc("Send ping to EventHub to verify configuration.")
@route("/configurations/event-hub:ping")
@post
op pingEventHub(
  @doc("API version parameter")
  @query("api-version") apiVersion: string,
): {
  @statusCode status: 200;
};

## Answer
I've recommended Dapeng to use RpcOperation for these ops.
 
One item we likely to hear your opinion, on using RpcOperation<{}, Response> for an op that does not take request body.
https://github.com/Azure/azure-rest-api-specs/pull/34199#discussion_r2059441091

# Need help in Adding final-state-schema for a single post action

## Question
Hi 
TypeSpec Discussion
,
PR: https://github.com/Azure/azure-rest-api-specs-pr/pull/22427
 
We would like to add final-state-schema for a single post action LRO operation for now. Is it possible to do it for a single post action or not ?
 
when we add emit-lro-options: "all" in tspconfig.yml. It is reflected in all the long-running options..

## Answer
Change the response type to match the final result type you want  [Here is a playground](https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZ8wfYXp1cmUtdG9vbHMvyCstxhVjb3Jl3yvIK3Jlc291cmNlLW1hbmFnZXIiOwoKdXNpbmcgSHR0cDvHDFJlc3TIDFbpAI7IEkHESi5Db3JlzhJSx1xNxls7CgovKiogQ29udG9zb8RUxR4gUHJvdmlkZXIg5gCDbWVudCBBUEkuICovCkBhcm3IIE5hbWVzcGFjZQpAc2VydmljZSgjeyB0aXRsZTogIsdXyC1IdWJDbGllbnQiIH0pCkDnAUNlZCjnAL9zKQpuyFAgTWljcm9zb2Z0LtJG7wC2QVBJIMdNc%2BQAoWVudW3oARNzIHsKICDELjIwMjEtMTAtMDEtcHJldmlld8g1xDQgIEB1c2VEZXBlbmRlbmN5KPUBLy7IVi52MV8wX1DGSF8xKcRAYXJtQ29tbW9uVOQBz8cq10jLKctUNcRIYPIAqWAsCn3mAPNB6AD16wEOIOgCMOQAyG1vZGVsIEVtcGxveWVlIGlzIFRyYWNrZWToAII8yBxQcm9wZXJ0aWVzPuUBJC4u6QCm5AHWUGFyYW1ldGVyyTE%2BO%2BgAhslfcMlE0nzKYOkBg0FnZSBvZiBlyD%2FlAXhhZ2U%2FOiBpbnQzMjsKxylDaXR50ipjaXR5Pzogc3Ry5QLpxyxQcm9maWzTWUBlbmNvZGUoImJhc2U2NHVybCLkAWBwxjA%2FOiBieXRlc8lIVGhlIHN0YXR1c8RLdGhlIGxhc3Qg5ADFYXRpb27lAvwgIEB2aXNpYmlsaXR5KExpZmVjeWNs5AHgYWTHXcQg5QOLU3RhdMRn5QGrzBTpAUjEc8wy5QCA5QDKYekB1cV3QGxyb8Q7dXMKdW7kArLRVOUBZOYBGizsANLIRyBjcmXEJ3JlcXVlc3QgaGFzIGJlZW4gYWNjZXB0ZWTEZyAgQccOOiAiyAsi1lBpxEDkALTpAMHIROwAnDogIswP2kx1cGRhdMRPxUNVxw46ICLIC8o76QSl6QDE5gDcZOcBolN1Y2NlZWTlAMXJDNM%2FxTbkAU1mYWlsyT5GxQ06ICLGCdw4d2FzIGNhbmNlyj5Dxw%2FkBP3HC%2F8BQCBkZWxl6QGARMQN5gD5yAsi6QP96QN3bW926gHK6QN5TW92ZVLHFegDcsRzbW92xGhmcm9tIGxvY%2BYAvMVuxBPxA1DLM3RvzzF0b8ov9wCVc3BvbnPrBIvmAJbHFuwAl%2B4DbMU%2BxWTGfOYC7s1uaW50ZXJm5AYcT%2BgDlHMgZXh0ZW5kc%2FYG3i7LKXt9CuUGusgjyhvLWegAzeYEs2dldOQBp0HKNeQD5%2BwFECDnAntPcuUCp%2BUB1ssvQ8cdUmVwbGFjZUFzeW5jzj%2FlAvfIN0N1c3RvbVBhdGNoU8QqCiAgIOkAkyzFDvYA50ZvdW7kAybkBkXIHOYAkU3kAYTGSdBLyhDqBarFGT4KICDlAJ3mAprvANTlApdlV2l0aG91dE9r8wDUbGlzdEJ5yDBHcm91cM9ETMUiUGFyZW501DxTdWJzY3JpcOUCecY7xjPMGcw56AZgIHNhbXBs6wNmYWPFRHRoYXTmAmPpBiN0byBkaWZmZeQAhO8C5sUp7gCyQcVI7gDtLOwDcsgN5gLp8wCTSEVBROoGTcR%2FY2hlY2vqAKtleGlzdGVu5gfHIMYeRckU7wJZzR3uB6I%3D&e=%40azure-tools%2Ftypespec-autorest&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fresource-manager%22%5D%7D%2C%22options%22%3A%7B%22%40azure-tools%2Ftypespec-autorest%22%3A%7B%22emit-lro-options%22%3A%22all%22%7D%7D%7D) showing that the response parameter in ArmResourceActionAsync shows up both in the 200 response and in the final-state-schema, which means that typespec-based emitters  will get the right final response value and so will swagger-based emitters.  
 
To be clear the final-state-schema in this case is just for debugging purposes is it not necessary in the actual swagger.  I added a comment to the PR showing the change you should make here.
 
I don't see anything in the discussion that would contradict this.

# Avocado Failing on PR

## Question

Hi 
TypeSpec Discussion
 
I have a PR here to add a new set of Azure AI APIs: https://github.com/Azure/azure-rest-api-specs/pull/33130
 
I'm a bit confused on why the avocado step is failing? As this is a brand new API, we started from scratch with tsp itself, and these swaggers are the output of npx tsv .... Do I really need to include a README.md in the swagger directories, or is this just failing incorrectly? I don't see README.md files in other service directories (i.e. keyvault) for example.
 
FYI: Johan

## Answer

You can look at my RPs project which uses Typespec - yes you need a readme.md in the generated swagger folder. Mine is a resource manager / RP project though. Not sure if that makes a difference.
 
https://github.com/Azure/azure-rest-api-specs/blob/main/specification/durabletask/resource-manager/readme.md
azure-rest-api-specs/specification/durabletask/resource-manager/readme.md at main · Azure/azure-rest-api-specs
The source for REST API specifications for Microsoft Azure. - Azure/azure-rest-api-specs

# Override contentType: "application/json" for ResourceCreateOrUpdate

## Question
Hi 
TypeSpec Discussion
,
 
I am migrating an old swagger to typespec. I came across a method which is a PATCH ops with a application/json as content type. The API behaves exactly as a merge-patch route, but I cannot change it since it'll be consider a breaking change. In order to still use the convenient functionalities of typespec traits, I have define a custom function like so playground and am using it.
 
Is there a better alternative?
 
## Answer
Yes. it's expected that if you are using a non-standard content-type for PATCH you will need to use a custom operation or a custom operation template.

# Defining a GET /latest API in TypeSpec

Hello,
we're in the process of designing a new set of ARM resources, one of which exposes a LRO that can last for more than a day. Following this RPC guidance, we want to expose a dedicated GET latest endpoint in addition to a GET endpoint by an id:
GET /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/exampleRG/providers/Microsoft.Chaos/workspaces/exampleWorkspace/scenarios/123e4567-e89b-12d3-a456-426614174000/runs/latest
GET /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/exampleRG/providers/Microsoft.Chaos/workspaces/exampleWorkspace/scenarios/123e4567-e89b-12d3-a456-426614174000/runs/123e4567-e89b-12d3-a456-426614174002
How would we define this /latest endpoint in TypeSpec? Currently we have the following for the "byId" endpoint:
 
```
  /**
   * Get a scenario run by run id.
   */
  get is ArmResourceRead<ScenarioRun>;
```
For reference, I can see that Microsoft.Billing uses this approach in their swagger v2 definition: https://github.com/Azure/azure-rest-api-specs/blob/dc2b7baf6b8845d955f83c16800522f60e343149/specification/billing/resource-manager/Microsoft.Billing/preview/2017-02-27-preview/billing.json#L156
 
Thanks!

## Answer
We could either model this as a singleton sub-resource using resource read, or as a resource action (overriding the verb to get) over the resource

[Here is a playground](https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZ8wfYXp1cmUtdG9vbHMvyCstxhVjb3Jl3yvIK3Jlc291cmNlLW1hbmFnZXIiOwoKdXNpbmcgSHR0cDvHDFJlc3TIDFbpAI7IEkHESi5Db3JlzhJSx1xNxls7CgovKiogQ29udG9zb8RUxR4gUHJvdmlkZXIg5gCDbWVudCBBUEkuICovCkBhcm3IIE5hbWVzcGFjZQpAc2VydmljZSgjeyB0aXRsZTogIsdXyC1IdWJDbGllbnQiIH0pCkDnAUNlZCjnAL9zKQpuyFAgTWljcm9zb2Z0LtJG7wC2QVBJIMdNc%2BQAoWVudW3oARNzIHsKICDELjIwMjEtMTAtMDEtcHJldmlld8g1xDQgIEB1c2VEZXBlbmRlbmN5KPUBLy7IVi52MV8wX1DGSF8xKcRAYXJtQ29tbW9uVOQBz8cq10jLKctUNcRIYPIAqWAsCn3mAPNB6AD16wEOIOgCMOQAyG1vZGVsIEVtcGxveWVlIGlzIFRyYWNrZWToAII8yBxQcm9wZXJ0aWVzPuUBJC4u6QCm5AHWUGFyYW1ldGVyyTE%2BO%2BQAhkBwYXJlbnTIKyjIHikKQOQCZ2xldG9uKCJsYXTkAzop5wCQUnVu9ACLUnVu%2FwCG6gCGUnVuPjvGIURlZmF1bHTlAQPnAvdTdGF06ADSeeUAqOQBLspdIG9mIHRoZSBydeUB7iNzdXBwcmVzc%2F8Die8DiS9hcm3KFXDrAIAtc%2BQAgSIgImtpbmRh6QGSIuoBAOsAjOgCkuQAkWRlc2NyaXB05AKH7gCkIMwcPzogc3Ry5QPg5wDc6QHjcMpZ8QIA03VBZ2XEaWXIP8VqYWdlPzogaW50MzI7CscpQ2l0edIqY2l0eesAjccsUHJvZmls01lAZW5jb2RlKCJiYXNlNjR1cmwi5ALkcMYwPzogYnl0ZXPJSFRoZSDkAUp16QGxbGFzdCDkAMVh5AEH5QSAICBA5AF4YmlsaXR5KExpZmVjeWNs5ANkYWTHXe4CGD865gTQzBTpAUjEc8wy5QCA5QDK6gHHxXdAbHJvxDt1cwp1buQBr9FU5QFk5gEaLOwA0shHIGNyZcQncmVxdWVzdCBoYXMgYmVlbiBhY2NlcHRlZMRnICBBxw46ICLICyLWUGnEQOQAtOkAwchE7ACcOiAizA%2FaTHVwZGF0xE%2FFQ1XHDjogIsgLyjvpBinpAMTmANxk5wGiU3VjY2VlZOUAxckM0z%2FFNuQBTWZhaWzJPkbFDTogIsYJ3Dh3YXMgY2FuY2XKPkPHD%2BQGgccL%2FwFAIGRlbGXpAYBExA3mAPnICyLpBYHpA3dtb3bqAcrpA3lNb3ZlUscV6ANyxHNtb3bEaGZyb20gbG9j5gC8xW7EE%2FEDUMszdG%2FPMXRv%2BgQM5wCVc3BvbnPrBg%2FmAJbHFuwAl%2B4DbMU%2BxWTGfOYC7s1uaW50ZXJm5AegT%2BgDlHMgZXh0ZW5kc%2FYIYi7LKXvkBkBhcm3II8oby1noAM3mBLNnZXTkAadByjXkA%2BfsBpQg5wJ7T3LlAqflAdbLL0PHHVJlcGxhY2VBc3luY84%2F5QL30DdQYXRjaFPMLCzzBVbFQOYCPc9A5QI6ZVdpdGhvdXRPa9N3bGlzdEJ5yDBHcm91cM9ETMUiUOUHcNQ8U3Vi6gY9xjvGM8wZzDnoBgMgc2FtcGzrAwlhY8VEdGhhdOYCBukFxnRvIGRpZmZl5ACE7wKJxSnuALJBxUjvATfrAxTIDeYCi%2FMAkkhFQUTqBe%2FEfmNoZWNr6gCqZXhpc3RlbuYI7SDGHkXJFO8B%2B80d7wEJZ2V05gLgzzXlAl%2FmCG19Cg%3D%3D&e=%40azure-tools%2Ftypespec-autorest&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fresource-manager%22%5D%7D%7D) showing this as a resource
 
[Here is a playground](https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZ8wfYXp1cmUtdG9vbHMvyCstxhVjb3Jl3yvIK3Jlc291cmNlLW1hbmFnZXIiOwoKdXNpbmcgSHR0cDvHDFJlc3TIDFbpAI7IEkHESi5Db3JlzhJSx1xNxls7CgovKiogQ29udG9zb8RUxR4gUHJvdmlkZXIg5gCDbWVudCBBUEkuICovCkBhcm3IIE5hbWVzcGFjZQpAc2VydmljZSgjeyB0aXRsZTogIsdXyC1IdWJDbGllbnQiIH0pCkDnAUNlZCjnAL9zKQpuyFAgTWljcm9zb2Z0LtJG7wC2QVBJIMdNc%2BQAoWVudW3oARNzIHsKICDELjIwMjEtMTAtMDEtcHJldmlld8g1xDQgIEB1c2VEZXBlbmRlbmN5KPUBLy7IVi52MV8wX1DGSF8xKcRAYXJtQ29tbW9uVOQBz8cq10jLKctUNcRIYPIAqWAsCn3mAPNB6AD16wEOIOgCMOQAyG1vZGVsIEVtcGxveWVlIGlzIFRyYWNrZWToAII8yBxQcm9wZXJ0aWVzPuUBJC4u6QCm5AHWUGFyYW1ldGVyyTE%2BO%2BgAhkRldGFpbHMgb2YgYSBydeUBQcZ5UnVu6QFxcnAt5ALVaWZpYyBwyW3ELCDLED86xDXqAI07CuYAjERlZmF1bHTlAOjnAtxTdGF06AC3eekAjco75ACQdGhl6ACSI3N1cHByZXNz%2FwNu7wNuL2FybcoVcOsAgC1z5ACBIiAia2luZGHpAXci6gD86wCM6AEG5ACRZGVzY3JpcHTkAmzuAKQgzBw%2FOiBzdHLlA8XnANzpAcjuAUDuAeXTdUFnZcRpZcg%2FxWphZ2U%2FOiBpbnQzMuUBZcQpQ2l0edIqY2l0eesAjccsUHJvZmls01lAZW5jb2RlKCJiYXNlNjR1cmwi5ALJcMYwPzogYnl05wHZxEhUaGUg5AFKdekBsWxhc3Qg5ADFYeQBB%2BUEZSAgQOQBeGJpbGl0eShMaWZlY3ljbOQDSWFkx13uAhg%2FOuYEtcwU6QFIxHPMMuUAgOUAyuoBx8V3QGxyb8Q7dXMKdW7kAa%2FRVOUBZOYBGizsANLIRyBjcmXEJ3JlcXVlc3QgaGFzIGJlZW4gYWNjZXB0ZWTEZyAgQccOOiAiyAsi1lBpxEDkALTpAMHIROwAnDogIswP2kx1cGRhdMRPxUNVxw46ICLIC8o76QYO6QDE5gDcZOcBolN1Y2NlZWTlAMXJDNM%2FxTbkAU1mYWlsyT5GxQ06ICLGCdw4d2FzIGNhbmNlyj5Dxw%2FkBmbHC%2F8BQCBkZWxl6QGARMQN5gD5yAsi6QVm6QN3bW926gHK6QN5TW92ZVLHFegDcsRzbW92xGhmcm9tIGxvY%2BYAvMVuxBPxA1DLM3RvzzF0b%2FoEDOcAlXNwb25z6wX05gCWxxbsAJfuA2zFPsVkxnzmAu7NbmludGVyZuQHhU%2FoA5RzIGV4dGVuZHP2CEcuyyl7fQrlCCPII8oby1noAM3mBLNnZXTkAadByjXkA%2BfsBnkg5wJ7T3LlAqflAdbLL0PHHVJlcGxhY2VBc3luY84%2F5QL30DdQYXRjaFPMLCzzBVbFQOYCPc9A5QI6ZVdpdGhvdXRPa9N3bGlzdEJ5yDBHcm91cM9ETMUiUGFyZW501DxTdWLqBj3GO8YzzBnMOegGAyBzYW1wbOsDCWFjxUR0aGF05gIG6QXGdG8gZGlmZmXkAITvAonFKe4AskHFSO8BN%2BsDFMgN5gKL8wCSSEVBROoF78R%2BY2hlY2vqAKpleGlzdGVu5gjSIMYeRckU7wH7zR3vAQlAZ2V0xAfmAPsoInJ1bnMvbGF05AvW5AZGZ2V05gMA%2FwDj5QDjdm9pZCzkB%2BblCQk%3D&e=%40azure-tools%2Ftypespec-autorest&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fresource-manager%22%5D%7D%7D) showing modeling this as an action.  In both cases, using 'getStatus' as the operation name.

# Is path case sensitive?

## Question

I have these swaggers paths:
 
```
"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.ElasticSan/elasticSans/{elasticSanName}/volumeGroups"
"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.ElasticSan/elasticSans/{elasticSanName}/volumegroups/{volumeGroupName}"
```
The second path represents a resource like
```
model VolumeGroup
  is Azure.ResourceManager.ProxyResource<VolumeGroupProperties> {
  ...ResourceNameParameter<
    Resource = VolumeGroup,
    KeyName = "volumeGroupName",
    SegmentName = "volumegroups",
    NamePattern = "^[A-Za-z0-9]+((-|_)[a-z0-9A-Z]+)*$"
  >;
 ```
Pay attention to segment is volumegroups. 
 
The first path is a list operation to this resource. However, its last segment is volumeGroups. If I use ArmResourceListByParent<VolumeGroup> for the first path it produces volumegroups. Can I use it?

## Answer
static segments in ARM urls are meant to be case-insensitive.  In this case, the swagger is incorrect, since this is clearly meant to be the ARM type name.  You should use the correct type name in both cases.

If the url is /subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/providers/..., is that the same as `/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/...? The "G" in resourceGroups has different cases.
These are case insensitive,  we should favor the camel case here, there is no need to match the exact casing of these in existing swagger

# Defining custom action with path /:action

## Question
Hi 
TypeSpec Discussion
,
 
What is the right way to define a path like following. Its an action but its not a ResourceAction. Closest I could find was an RpcOperation. Is there a better alternative?
```
"/:query-test":{
  "post":{
    "operationId": "QuestionAnswering_GetAnswersFromText",
    "summary": "Answers the specified question using the provided text in the body.",
    "products":[
      "application/json"
    ],
    "consumes":[
      "application/json"
    ],
  }
}
```
```
  @route("/:query-text")
  @post
  getAnswersFromText is RpcOperation<
    {
      @bodyRoot
      textQueryOptions: AnswersFromTextOptions;
    },
    AnswersFromTextResult,
    {},
    ErrorResponse
  >;
```

## Answer
RPC operation is essentially for this.

# GraphQL emitters

## Question
Is anyone working on GraphQL client/server emitters? 

## Answer
yeah I think they work on this repo https://github.com/pinterest/typespec/tree/feature/graphql, they started working on a branch of microsoft/typespec but the friction was a bit too much

# Typespec -> Autorest generation : multiple specs per service

## Question
Currently while we are able to organize and manage multiple typespec files per service easily, the final generated swagger is a single file.
I was asked during my API review to check if there is feasibility to produce multiple specs per service for organizational purposes considering the generated file is huge. 
I see prior posts on this indicating this is not supported, but looking for any latest update/guidance here.
Secondly, if the above is in fact supported, any idea if the SDK generation part can handle multiple specs per service?

## Answer
It was confirmed that splitting a TypeSpec specification into multiple OpenAPI files is not supported. Additionally, merging Swagger files does not impact REST API reference documentation, and the readme.md file should be checked for updates.

# Version Control in TypeSpec

## Question
Hi team,
 
We are introducing a new version, 2025-05-01-GA, which adds new APIs based on 2023-04-01-GA. However, we already have several preview versions introduced between these two GA versions — 2023-04-15-preview, 2024-11-15-preview, and 2025-05-15-preview.
 
The issue is that 2025-05-01-GA only includes a subset of the APIs introduced in 2023-04-15-preview. If we make 2025-05-01-GA the latest version, it will appear to remove a number of APIs or models that exist in 2025-05-15-preview.
 
Would it be better to insert 2025-05-01-GA right after 2023-04-01-GA, treating it as a smaller update? We could then convert some APIs from 2023-04-15-preview into 2025-05-01-GA, and let later preview versions inherit from 2025-05-01-GA accordingly.
 
What’s the best approach for handling this versioning situation in TypeSpec?
Thank you!
```
/**
 * The available API versions.
 */
enum Versions {
  /** Version 2023-04-01 */
  @useDependency(Azure.Core.Versions.v1_0_Preview_2)
  v2023_04_01: "2023-04-01",

  /**
   * The 2025-05-01 API version.
   */
  @useDependency(Azure.Core.Versions.v1_0_Preview_2)
  v2025_05_01: "2025-05-01",

  /**
   * The 2023-04-15-preview API version.
   */
  @useDependency(Azure.Core.Versions.v1_0_Preview_2)
  v2023_04_15_preview: "2023-04-15-preview",

  /**
   * The 2024-11-15-preview API version.
   */
  @useDependency(Azure.Core.Versions.v1_0_Preview_2)
  v2024_11_15_preview: "2024-11-15-preview",

  /**
   * The 2025-05-15-preview API version.
   */
  @useDependency(Azure.Core.Versions.v1_0_Preview_2)
  v2025_05_15_preview: "2025-05-15-preview",
}
```

## Answer
Preview versions should stop working in 90 days after a new preview or stable version is introduced. 
2025-05-15-preview will be the corresponding preview version for 2025-05-01-GA. 
Old preview versions will be retired and old swaggers removed from the repo. 
The final typespec should have any @added/@removed between GA versions and then just @added for the new preview version.
A formal retirement process is needed for previous preview versions in the service backend.

# Discriminators/polymorphism

## Question
I'm looking into introducing polymorphism into one of our APIs. This question provides great insight but is apparently closed as a duplicate of an issue that does not seem exist. Mark Cowlishaw, do you know what the outcome of this no longer existent issue? 
 Note that there is an issue around how we should encourage types using extends in Azure APIs to use discriminators here: https://github.com/Azure/typespec-azure/issues/3510
Like Brian Terlson,  I tend to favor the union approach for operations:
 op create(@body body: Cat | Dog // replace the base class with union of subs)
Is this the recommended Azure approach, union (i.e. @body: SubModel1 | SubModel2 for operation with @discriminator in base model type for @body ?

## Answer
Azure recommends using @discriminator to model polymorphism, but there are still some missing pieces before discriminated union types will be allowed in Azure APIs. The polymorphism in the model looks correct, and it is suggested to use PascalCase for union variant names. Additionally, narrowing inherited property types in inheriting classes (except for the discriminator) is not allowed. 

# Proper Service Versioning

## Question
Hi 
TypeSpec Discussion
. I'm looking to understand how to properly add a new service version to my team's typespec. I've been looking at this doc here as a baseline, and I think I generally understand everything there. But I've got a couple of questions for my specific case.
What does the added decorator actually do? Just tell the swagger which version should/shouldn't contain a property? I'm assuming the question of whether it has an impact on any of the SDKs is a question for the individual generator teams?
My team's spec has a client.tsp file with a customization namespace, which has this decorator: @useDependency(AI.Model.Versions.v2024_05_01_Preview). I get the general gist that this ties things to a specific version, but what does that mean from a practical standpoint? I maybe can understand client customizations being specific to individual versions, but what about modifications that work across versions? I don't seem to be able to provide a list or anything to that decorator. I've tried removing it, and I get an error saying that the customization namespace is referencing a versioned namespace and should add the decorator. I've also tried just changing the namespace to match, but then I get an error from my client interfaces saying that I have duplicate operations. So I'm trying to understand how to correctly handle this.
Any pointers would be appreciated. Thanks in advance!

## Answer
TypeSpec allows versioning based on differences, tagging changes with versioning decorators to reconstruct the API at each active version. 
The @added decorator is used for adding new types to the spec, indicating the version when the change occurred. Similar decorators include @removed, @renamedFrom, and @typeChangedFrom.
Versioning is tied to a namespace, including all child namespaces. If the client.tsp is a child namespace of the versioned namespace, no explicit version coupling is required. Otherwise, replicate the versions enum from the service namespace in client.tsp and tie each version explicitly.
Version-specific client.tsp changes are possible, but using @useDependency decorators ensures the client.tsp is used in both versions. 

# Traditional RP to use typespec. Can this be done? Are there any challenges?

## Question
Hi team,
 
We are currently using traditional RP and wanted to leverage using typespec for our newer set of service offering using the same RP. We came across typespec and wanted to check if we can leverage using typespec for tradiiotnal RPs. Are there any other teams who have done something like that. Are there any challenges with this approach?
 
Thanks!!

## Answer
I am not sure what you mean by 'traditional RP'  can you clarify?  There is some documentation on converting older RPs to TypeSpec here: [Migrate ARM spec | TypeSpec Azure](https://azure.github.io/typespec-azure/docs/migrate-swagger/checklists/migrate-arm-tips/)

# Folder structure recommendation for typespec.

## Question
Hi Team,
 
Can you please confirm what the folder structure needs to be for the .tsp files, specifically for teams that are separating services within the same RP namespace?

## Answer
There is documentation for this here: [azure-rest-api-specs/documentation/typespec-structure-guidelines.md at main · Azure/azure-rest-api-specs](https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/typespec-structure-guidelines.md#libraries-for-service-groups)

# Record<string> alternative

## Question
Our spec used Record<string> in the past and this is no longer recommended per this rule: link
 
Our service was designed in such a way to depend on allowing the user to pass a dictionary with a string with any key/any value.
 
Other than suppression, is there a suggested workaround?

## Answer
You will have to suppress the rule.

# Regex Pattern is not Reflecting correctly for Proxy Resources

## Question
hi 
TypeSpec Engineering,
I added the Regex pattern for Proxy Resource Types, but it isn't correctly reflecting in the generated Swagger after the Type Spec compilation.
 
RegEx Pattern Link - https://github.com/Azure/azure-rest-api-specs/blob/dce18ad6d9dbc9e8c9912339f2c542f168ac4cb0/specification/liftrneon/Neon.Postgres.Management/Neon.Postgres.Models/projects.tsp#L20
 
Swagger Link - https://github.com/Azure/azure-rest-api-specs/blob/dce18ad6d9dbc9e8c9912339f2c542f168ac4cb0/specification/liftrneon/resource-manager/Neon.Postgres/preview/2025-03-01-preview/neon.json#L579C11-L586C12
 
 
i have tried similar changes as this as suggested by Nikhil - https://github.com/Azure/azure-rest-api-specs/blob/209b504aa68f7571e6ac365a2bba80e28c13cba1/specification/scvmm/ScVmm.Management/InventoryItem.tsp#L14  but still it did not work . 
 
Any insights on this would be greatly appreciated, as we're targeting a General Availability release by April 30.

## Answer
I believe its because its not using that model but the one you ahve defined in main.tsp 
https://github.com/Azure/azure-rest-api-specs/blob/dce18ad6d9dbc9e8c9912339f2c542f168ac4cb0/specification/liftrneon/Neon.Postgres.Management/main.tsp#L111

# Change parent resource

## Question
Hi, 
 
I have a scenario where I'm renaming a tracked resource AutoActions to ScheduledActions a new API version.  The way I thought about doing this is by leveraging @added and @removed decorators. 
 
However, AutoActions has a child resource occurrence, that now needs to be a child resource of Scheduled Actions. But if I do something like this it gives conflict error. How can I unblock this scenario? 
```
@added(Microsoft.ComputeSchedule.Versions.`2024-08-01-preview`)
@removed(Microsoft.ComputeSchedule.Versions.`2025-04-01-preview`)
@parentResource(AutoAction)
model Occurrence is ProxyResource<OccurrenceProperties> {
  ...ResourceNameParameter<
    Occurrence,
    KeyName = "occurrenceId",
    NamePattern = "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}"
  >;
}

@added(Microsoft.ComputeSchedule.Versions.`2025-04-01-preview`)
@parentResource(ScheduledAction)
model Occurrence is ProxyResource<OccurrenceProperties> {
  ...ResourceNameParameter<
    Occurrence,
    KeyName = "occurrenceId",
    NamePattern = "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}"
  >;
} 
```

## Answer
Renaming a tracked resource from AutoActions to ScheduledActions in a new API version caused conflict errors, leading to renaming old resources and filing a bug report for TypeSpec support.
[Bug](https://github.com/Azure/typespec-azure/issues/2551): Renaming resource not working properly · Issue #2551 · Azure/typespec-azure

# JSON merge-patch support in TypeSpec

## Question
How exactly does TypeSpec support application/merge-patch+json i.e., "JSON merge-patch"? Is there explicit types, or is it really just a matter of service authors adding | null to their type defs e.g.,
 
```
model M {
    @key("id")
    id: string;
    name: string | null;
    dob?: utcDateTime | null;
}
 ```
This will greatly affect how Rust will support this, give a discussion Larry, Johan, and I were having yesterday.

## Answer
Design a new MergePatch template to make it easier for service authors to create accurate MergePatch schemas in OpenAPI.
Azure takes the same stance as Graph, where JSON merge patch is considered a service fundamental and null is not expressed in TypeSpec models.
Generators for both client and service side types should make everything optional to accurately represent JSON merge-patch support.

# Versioning the body of a route

## Question
We added a post route called align in our last preview version, 2025-03-01-preview

```

@action("align")
@added(APIVersions.v2025_03_01_preview)
alignDevBox is DevCenterOps.LongRunningResourceAction<
  DevBox,
  {}, // Body of the POST request
  OperationStatus
>;
```
However, now we need to change it from no body (i.e parameter 2, {}) into taking in one in our new preview API version, 2025-04-01-preview

Full example 
```
@action("align")
@added(APIVersions.v2025_03_01_preview)
alignDevBox is DevCenterOps.LongRunningResourceAction<
  DevBox,
  AlignBody, // Went from no body, to taking in this new model
  OperationStatus
>;
```

This returns error message DevCenterService.DevBoxes.alignDevBox' was added in version 'v2025_03_01_preview' but referencing type 'DevCenterService.AlignBody' added in version 'v2025_04_01_preview'. 

However, we don't see a way to version a parameter. What would be the best course of action here?

## Answer
The team decided to open an issue for allowing versioning on body parameter of a route/operation in TypeSpec. 
[Issue](https://github.com/microsoft/typespec/issues/7032)

# How to make an interface internal ?

## Question
I am looking for ways to make an interface internal so that it does not appear in public interface of python SDK. This is the interface which emits EvaluationResultsOperations which shows up on client. I would like to generate it but keep it hidden from public interface.
 
I tried following:
Mark all operations under it internal
Adding @access decorator to interface but that fails.
Is there a way to achieve it ?

## Answer
Remove all the output and regenerate the code completely to resolve the issue with the folder mix in the previous generated code.
Confirmed that hiding an entire operation group is not supported, and suggested using client.tsp to reorganize operations or _patch.py to customize the code.

# Define multiple response types from ResourceCollectionAction

## Question
Hi,
Is there a way to define multiple responses for "ResourceCollectionAction"?
For instance, I need to create a "Post" request where the response body varies based on the input values. However, "ResourceCollectionAction" seems to only allow a single response model by default. I am looking to pass multiple response models, such as 200, 201, and 202, to this single action operation "ResourceCollectionAction."

## Answer
Use the @sharedRoute decorator to model multiple logical operations at the same path.
Prefer using a union of two models rather than a union of status codes for better API evolution.

# error deprecated: Deprecated: Implicit multipart is deprecated, use @multipartBody instead with HttpPart

## Question
Our topic branch (and TypeSpec project) here https://github.com/Azure/azure-rest-api-specs/tree/feature/azure-ai-projects-1dp/specification/ai/Azure.AI.Projects got these tool updates:
    "@azure-tools/typespec-autorest": "0.53.0",
    "@azure-tools/typespec-azure-core": "0.53.0",
    "@azure-tools/typespec-azure-portal-core": "0.53.0",
    "@azure-tools/typespec-azure-resource-manager": "0.53.0",
    "@azure-tools/typespec-azure-rulesets": "0.53.0",
    "@azure-tools/typespec-client-generator-cli": "0.15.3",
    "@azure-tools/typespec-client-generator-core": "0.53.0",
This now results in the following new errors. Looking for some guidance on how to fix this. Thanks!
```
Running TypeSpecValidation on folder:  E:/src/azure-rest-api-specs-2/specification/ai/Azure.AI.Projects

Executing rule: FolderStructure
folder: E:/src/azure-rest-api-specs-2/specification/ai/Azure.AI.Projects
config files: ["E:/src/azure-rest-api-specs-2/specification/ai/Azure.AI.Projects/tspconfig.yaml"]
azure-resource-provider-folder: "data-plane"


Executing rule: NpmPrefix
run command:npm prefix
Expected npm prefix: E:/src/azure-rest-api-specs-2
Actual npm prefix: E:/src/azure-rest-api-specs-2

Executing rule: EmitAutorest
mainTspExists: true
emit: ["@azure-tools/typespec-autorest"]


Executing rule: FlavorAzure
"@azure-tools/typespec-python":
  flavor: azure
"@azure-tools/typespec-csharp":
  flavor: azure
"@azure-tools/typespec-ts":
  flavor: azure


Executing rule: LinterRuleset
azure-resource-provider-folder: "data-plane"
files: ["main.tsp","client.tsp"]
linter.extends: ["@azure-tools/typespec-azure-rulesets/data-plane"]

Executing rule: Compile
run command:npm exec --no -- tsp compile --warn-as-error E:/src/azure-rest-api-specs-2/specification/ai/Azure.AI.Projects
run command:npm exec --no -- tsp compile --no-emit --warn-as-error E:\src\azure-rest-api-specs-2\specification\ai\Azure.AI.Projects\client.tsp
TypeSpec compiler v0.67.1

Diagnostics were reported during compilation:

../E:/src/azure-rest-api-specs-2/node_modules/@azure-tools/typespec-azure-core/lib/foundations.tsp:290:2 - error deprecated: Deprecated: Implicit multipart is deprecated, use @multipartBody instead with HttpPart
> 290 | >(
      |  ^
> 291 |   ...TraitProperties<
      | ^^^^^^^^^^^^^^^^^^^^^
> 292 |     Traits & VersionParameterTrait<ApiVersionParameter>,
      | ^^^^^^^^^^^^^^^^^^^^^
> 293 |     TraitLocation.ApiVersionParameter
      | ^^^^^^^^^^^^^^^^^^^^^
> 294 |   >,
      | ^^^^^^^^^^^^^^^^^^^^^
> 295 |   ...Parameters,
      | ^^^^^^^^^^^^^^^^^^^^^
> 296 | ): Response | ErrorResponse;
      | ^^
  ../E:/src/azure-rest-api-specs-2/specification/ai/Azure.AI.Projects/assistants/files/routes.tsp:50:18 - occurred while instantiating template
  > 50 | op uploadFile is Azure.Core.Foundations.Operation<
       |                  ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
  > 51 |   {
       | ^^^
  > 52 |     @doc("The name of the file to upload.")
       | ^^^
  > 53 |     @header
       | ^^^
  > 54 |     contentType: "multipart/form-data";
       | ^^^
  > 55 |
       | ^^^
  > 56 |     @doc("The file data, in bytes.")
       | ^^^
  > 57 |     @clientName("Data", "csharp")
       | ^^^
  > 58 |     file: bytes;
       | ^^^
  > 59 |
       | ^^^
  > 60 |     @doc("The intended purpose of the uploaded file. Use `assistants` for Agents and Message files, `vision` for Agents image file inputs, `batch` for Batch API, and `fine-tune` for Fine-tuning.")
       | ^^^
  > 61 |     purpose: FilePurpose;
       | ^^^
  > 62 |
       | ^^^
  > 63 |     /*
       | ^^^
  > 64 |      * Spec note: filename is not documented as a distinct option but functionally should be one. The value is encoded
       | ^^^
  > 65 |      *            in the multipart Content-Disposition header for the data section and can be provided independently of
       | ^^^
  > 66 |      *            any specific data source like a file. It can be inferred in some circumstances (as when using direct
       | ^^^
  > 67 |      *            file input, like curl does) but should remain configurable when using a stream or other data source
       | ^^^
  > 68 |      *            lacking an a priori name.
       | ^^^
  > 69 |      */
       | ^^^
  > 70 |     @doc("The name of the file.")
       | ^^^
  > 71 |     filename?: string;
       | ^^^
  > 72 |   },
       | ^^^
  > 73 |   OpenAIFile
       | ^^^
  > 74 | >;
       | ^^
../E:/src/azure-rest-api-specs-2/node_modules/@azure-tools/typespec-azure-core/lib/foundations.tsp:290:2 - error deprecated: Deprecated: Implicit multipart is deprecated, use @multipartBody instead with HttpPart
> 290 | >(
      |  ^
> 291 |   ...TraitProperties<
      | ^^^^^^^^^^^^^^^^^^^^^
> 292 |     Traits & VersionParameterTrait<ApiVersionParameter>,
      | ^^^^^^^^^^^^^^^^^^^^^
> 293 |     TraitLocation.ApiVersionParameter
      | ^^^^^^^^^^^^^^^^^^^^^
> 294 |   >,
      | ^^^^^^^^^^^^^^^^^^^^^
> 295 |   ...Parameters,
      | ^^^^^^^^^^^^^^^^^^^^^
> 296 | ): Response | ErrorResponse;
      | ^^
  ../E:/src/azure-rest-api-specs-2/specification/ai/Azure.AI.Projects/assistants/files/routes.tsp:50:18 - occurred while instantiating template
  > 50 | op uploadFile is Azure.Core.Foundations.Operation<
       |                  ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
  > 51 |   {
       | ^^^
  > 52 |     @doc("The name of the file to upload.")
       | ^^^
  > 53 |     @header
       | ^^^
  > 54 |     contentType: "multipart/form-data";
       | ^^^
  > 55 |
       | ^^^
  > 56 |     @doc("The file data, in bytes.")
       | ^^^
  > 57 |     @clientName("Data", "csharp")
       | ^^^
  > 58 |     file: bytes;
       | ^^^
  > 59 |
       | ^^^
  > 60 |     @doc("The intended purpose of the uploaded file. Use `assistants` for Agents and Message files, `vision` for Agents image file inputs, `batch` for Batch API, and `fine-tune` for Fine-tuning.")
       | ^^^
  > 61 |     purpose: FilePurpose;
       | ^^^
  > 62 |
       | ^^^
  > 63 |     /*
       | ^^^
  > 64 |      * Spec note: filename is not documented as a distinct option but functionally should be one. The value is encoded
       | ^^^
  > 65 |      *            in the multipart Content-Disposition header for the data section and can be provided independently of
       | ^^^
  > 66 |      *            any specific data source like a file. It can be inferred in some circumstances (as when using direct
       | ^^^
  > 67 |      *            file input, like curl does) but should remain configurable when using a stream or other data source
       | ^^^
  > 68 |      *            lacking an a priori name.
       | ^^^
  > 69 |      */
       | ^^^
  > 70 |     @doc("The name of the file.")
       | ^^^
  > 71 |     filename?: string;
       | ^^^
  > 72 |   },
       | ^^^
  > 73 |   OpenAIFile
       | ^^^
  > 74 | >;
       | ^^
../E:/src/azure-rest-api-specs-2/node_modules/@azure-tools/typespec-azure-core/lib/foundations.tsp:290:2 - error deprecated: Deprecated: Implicit multipart is deprecated, use @multipartBody instead with HttpPart
> 290 | >(
      |  ^
> 291 |   ...TraitProperties<
      | ^^^^^^^^^^^^^^^^^^^^^
> 292 |     Traits & VersionParameterTrait<ApiVersionParameter>,
      | ^^^^^^^^^^^^^^^^^^^^^
> 293 |     TraitLocation.ApiVersionParameter
      | ^^^^^^^^^^^^^^^^^^^^^
> 294 |   >,
      | ^^^^^^^^^^^^^^^^^^^^^
> 295 |   ...Parameters,
      | ^^^^^^^^^^^^^^^^^^^^^
> 296 | ): Response | ErrorResponse;
      | ^^
  ../E:/src/azure-rest-api-specs-2/specification/ai/Azure.AI.Projects/assistants/files/routes.tsp:50:18 - occurred while instantiating template
  > 50 | op uploadFile is Azure.Core.Foundations.Operation<
       |                  ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
  > 51 |   {
       | ^^^
  > 52 |     @doc("The name of the file to upload.")
       | ^^^
  > 53 |     @header
       | ^^^
  > 54 |     contentType: "multipart/form-data";
       | ^^^
  > 55 |
       | ^^^
  > 56 |     @doc("The file data, in bytes.")
       | ^^^
  > 57 |     @clientName("Data", "csharp")
       | ^^^
  > 58 |     file: bytes;
       | ^^^
  > 59 |
       | ^^^
  > 60 |     @doc("The intended purpose of the uploaded file. Use `assistants` for Agents and Message files, `vision` for Agents image file inputs, `batch` for Batch API, and `fine-tune` for Fine-tuning.")
       | ^^^
  > 61 |     purpose: FilePurpose;
       | ^^^
  > 62 |
       | ^^^
  > 63 |     /*
       | ^^^
  > 64 |      * Spec note: filename is not documented as a distinct option but functionally should be one. The value is encoded
       | ^^^
  > 65 |      *            in the multipart Content-Disposition header for the data section and can be provided independently of
       | ^^^
  > 66 |      *            any specific data source like a file. It can be inferred in some circumstances (as when using direct
       | ^^^
  > 67 |      *            file input, like curl does) but should remain configurable when using a stream or other data source
       | ^^^
  > 68 |      *            lacking an a priori name.
       | ^^^
  > 69 |      */
       | ^^^
  > 70 |     @doc("The name of the file.")
       | ^^^
  > 71 |     filename?: string;
       | ^^^
  > 72 |   },
       | ^^^
  > 73 |   OpenAIFile
       | ^^^
  > 74 | >;
       | ^^

Found 3 errors.

TypeSpec compiler v0.67.1

Diagnostics were reported during compilation:

../E:/src/azure-rest-api-specs-2/node_modules/@azure-tools/typespec-azure-core/lib/foundations.tsp:290:2 - error deprecated: Deprecated: Implicit multipart is deprecated, use @multipartBody instead with HttpPart
> 290 | >(
      |  ^
> 291 |   ...TraitProperties<
      | ^^^^^^^^^^^^^^^^^^^^^
> 292 |     Traits & VersionParameterTrait<ApiVersionParameter>,
      | ^^^^^^^^^^^^^^^^^^^^^
> 293 |     TraitLocation.ApiVersionParameter
      | ^^^^^^^^^^^^^^^^^^^^^
> 294 |   >,
      | ^^^^^^^^^^^^^^^^^^^^^
> 295 |   ...Parameters,
      | ^^^^^^^^^^^^^^^^^^^^^
> 296 | ): Response | ErrorResponse;
      | ^^
  ../E:/src/azure-rest-api-specs-2/specification/ai/Azure.AI.Projects/assistants/files/routes.tsp:50:18 - occurred while instantiating template
  > 50 | op uploadFile is Azure.Core.Foundations.Operation<
       |                  ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
  > 51 |   {
       | ^^^
  > 52 |     @doc("The name of the file to upload.")
       | ^^^
  > 53 |     @header
       | ^^^
  > 54 |     contentType: "multipart/form-data";
       | ^^^
  > 55 |
       | ^^^
  > 56 |     @doc("The file data, in bytes.")
       | ^^^
  > 57 |     @clientName("Data", "csharp")
       | ^^^
  > 58 |     file: bytes;
       | ^^^
  > 59 |
       | ^^^
  > 60 |     @doc("The intended purpose of the uploaded file. Use `assistants` for Agents and Message files, `vision` for Agents image file inputs, `batch` for Batch API, and `fine-tune` for Fine-tuning.")
       | ^^^
  > 61 |     purpose: FilePurpose;
       | ^^^
  > 62 |
       | ^^^
  > 63 |     /*
       | ^^^
  > 64 |      * Spec note: filename is not documented as a distinct option but functionally should be one. The value is encoded
       | ^^^
  > 65 |      *            in the multipart Content-Disposition header for the data section and can be provided independently of
       | ^^^
  > 66 |      *            any specific data source like a file. It can be inferred in some circumstances (as when using direct
       | ^^^
  > 67 |      *            file input, like curl does) but should remain configurable when using a stream or other data source
       | ^^^
  > 68 |      *            lacking an a priori name.
       | ^^^
  > 69 |      */
       | ^^^
  > 70 |     @doc("The name of the file.")
       | ^^^
  > 71 |     filename?: string;
       | ^^^
  > 72 |   },
       | ^^^
  > 73 |   OpenAIFile
       | ^^^
  > 74 | >;
       | ^^

Found 1 error.


Rule Compile failed
Command failed: npm exec --no -- tsp compile --warn-as-error E:/src/azure-rest-api-specs-2/specification/ai/Azure.AI.Projects
- Compiling...
× Compiling
- Running @azure-tools/typespec-autorest...
× @azure-tools/typespec-autorest    ../
- Compiling...
× Compiling
- Running @azure-tools/typespec-autorest...
× @azure-tools/typespec-autorest    ../
Command failed: npm exec --no -- tsp compile --no-emit --warn-as-error E:\src\azure-rest-api-specs-2\specification\ai\Azure.AI.Projects\client.tsp
- Compiling...
× Compiling
- Compiling...
× Compiling
```

## Answer
To replace the implicit multipart with the explicit multipart for TypeSpec projects. 
To directly use the new form for TypeSpec work in feature branches, as the legacy form will be removed in the 1.0-rc release. 
To proceed with the required changes immediately due to the removal of the suppression feature in 1.0rc. 

# ARM SDK generation failing - error import-not-found: Couldn't resolve import "@azure-tools/typespec-azure-portal-core"

## Question
Need some help diagnosing this error when generating an ARM SDK from TypeSpec:
tsp-client init --tsp-config https://github.com/Azure/azure-rest-api-specs-pr/blob/0b86cdd12d0c07cc7a85214d6e510fc1bb15172a/specification/onlineexperimentation/OnlineExperimentation.Management/tspconfig.yaml --debug
Fails the same locally and in PR build pipelines on:
error import-not-found: Couldn't resolve import "@azure-tools/typespec-azure-portal-core"
The working branches are synced up with main / RPSaaSMaster branches.
https://github.com/Azure/azure-rest-api-specs-pr/pull/22246
https://github.com/Azure/azure-rest-api-specs/pull/33840
 
I see a bunch of other projects importing the same package (Code search results) in *.tsp files but I couldn't spot any dependency declarations, etc. and the package seems to be in packges.json / package-lock.json already.
 
The package docs (https://eng.ms/docs/products/azure-portal-framework-ibizafx/declarative/typespec) say to run (which I alrady did locally)
npm install -g @azure-tools/typespec-azure-portal-core
But tsp-client seems to sets up its own virtual environment, so should it be declared somewhere like tspconfig.yaml?

## Answer
Management plane libraries should use the @azure-tools/typespec-azure-portal-core package as a dependency to ensure they can compile management plane specs. 
Serguei Michtchenko removed the decorators and import due to having a custom portal extension implementation. 
Discussion will be held to ensure a clear pathway for future use of these decorators. 

# Sharing models between data plane and control plane

## Question
Has anyone successfully shared models between control plane and data plane? I'm struggling with this seemingly simple task and could use come guidance or an example other than the trivial one for sharing a TSP file withing control plan or within data plane for a single service.
 
Even to share models across separate versioned data plane APIs, I ended up creating a new data plane shared namespace and associated version to get it working. Do I need to create a control plane shared namespace and version? This makes me a bit nervous about conflicting versions of the same dependency between the service namespace, data plane shared namespace and control plane shared namespace.
 
When I try to cross data plane and control plane TypeSpec, I end up with unhelpful errors like this:
```
<unknown location>:1:1 - error @typespec/versioning/using-versioned-library: Namespace '' is referencing types from versioned namespace 'Azure.Core' but didn't specify which versions with @useDependency.
<unknown location>:1:1 - error @typespec/versioning/using-versioned-library: Namespace '' is referencing types from versioned namespace 'Azure.ResourceManager' but didn't specify which versions with @useDependency.
```
Or errors about multiple namespace or about @service not specifying a namespace even though it does.

## Answer
Decided to use a shared namespace for TypeSpec between data plane APIs and control plane APIs, despite concerns about dependency versions. 
Removed the ARM dependency version from the Discovery.Shared TSP version based on guidance to avoid using ARM dependencies in data plane APIs. 
Clarified that the data plane library should not have a dependency on the management plane library.

# TypeSpec Validation failure issue

## Question
Hi Team,
 
I have a PR to get new properties added on to an existing api version : [Bharam/update new properties in2024 11 30 preview version by Bharam-Msft · Pull Request #22220 · Azure/azure-rest-api-specs-pr](https://github.com/Azure/azure-rest-api-specs-pr/pull/22220)
 
i see TypeSpec validations are failing. i have followed steps to mitigate and locally it works fine but the PR validation is failing.
 
can some one help me on resolving this issue?

## Answer
Tried following the steps here? 
https://github.com/Azure/azure-rest-api-specs/wiki/TypeSpec-Validation#running-locally

# Question regarding the unexpected readonly, and customize the enum name

## Question
Hi team,
 
When dealing with the TypeSpec migration, we hit below two issues. Could you help take a look and see if there is any way to fix them? Thanks!
1. Haven't add @visibility(Lifecycle.Read) to the property, but the definition has "readOnly": true on it.
TSP: https://github.com/Azure/azure-rest-api-specs/blob/45317772ce7c50313eaf55b8d242f4d12ca6fe06/specification/desktopvirtualization/DesktopVirtualization.Management/models.tsp#L3665
Swagger: https://github.com/Azure/azure-rest-api-specs/blob/45317772ce7c50313eaf55b8d242f4d12ca6fe06/specification/desktopvirtualization/resource-manager/Microsoft.DesktopVirtualization/preview/2025-04-01-preview/desktopvirtualization.json#L11380
TSP: https://github.com/Azure/azure-rest-api-specs/blob/45317772ce7c50313eaf55b8d242f4d12ca6fe06/specification/desktopvirtualization/DesktopVirtualization.Management/models.tsp#L4859
Swagger: https://github.com/Azure/azure-rest-api-specs/blob/45317772ce7c50313eaf55b8d242f4d12ca6fe06/specification/desktopvirtualization/resource-manager/Microsoft.DesktopVirtualization/preview/2025-04-01-preview/desktopvirtualization.json#L10826
2. How to make enum's "x-ms-enum" name to be different than the type name like below?
https://github.com/Azure/azure-rest-api-specs/blob/cb262725d128f6dfec4622cca03bc9e04e2d0f1f/specification/desktopvirtualization/resource-manager/Microsoft.DesktopVirtualization/preview/2024-11-01-preview/desktopvirtualization.json#L9487C4-L9493C33

## Answer
1. This is because of this https://azure.github.io/typespec-azure/docs/troubleshoot/status-read-only-error/#_top
2. No its not possible change the enum name to be what is in x-ms-enum.name it is pointless information otherwise

# How to keep the preview swagger files at same time when we publish a new version

## Question
PR link: [Add first stable version for ACO API by liangchenmicrosoft · Pull Request #21743 · Azure/azure-rest-api-specs-pr](https://teams.microsoft.com/l/message/19:906c1efbbec54dc8949ac736633e6bdf@thread.skype/1744044764127?tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47&groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&parentMessageId=1744044764127&teamName=Azure%20SDK&channelName=TypeSpec%20Discussion&createdTime=1744044764127)
We are going to publish a stable version API , but validation pipeline was failed with below error message, it looks like a new enabled validation rule, PR was build success previously.  Is it possible that we can skip this validation rule?
 
Add first stable version for ACO API · Azure/azure-rest-api-specs-pr@7c9d64c
 
  Generated Swaggers:
175  specification/carbon/resource-manager/Microsoft.Carbon/stable/2025-04-01/main.json
176  
177  Output folder:
178  specification/carbon/resource-manager/Microsoft.Carbon
179  
180  Swaggers matching output folder and filename:
181  specification/carbon/resource-manager/Microsoft.Carbon/preview/2023-04-01-preview/main.json
182  specification/carbon/resource-manager/Microsoft.Carbon/preview/2024-02-01-preview/main.json
183  specification/carbon/resource-manager/Microsoft.Carbon/stable/2025-04-01/main.json
184  
185  Swaggers excluded via suppressions.yaml:
186  
187  
188  Remaining swaggers:
189  specification/carbon/resource-manager/Microsoft.Carbon/preview/2023-04-01-preview/main.json
190  specification/carbon/resource-manager/Microsoft.Carbon/preview/2024-02-01-preview/main.json
191  specification/carbon/resource-manager/Microsoft.Carbon/stable/2025-04-01/main.json
192  
193  Rule Compile failed
194  
195  Output folder 'specification/carbon/resource-manager/Microsoft.Carbon' appears to contain TypeSpec-generated swagger files, not generated from the current TypeSpec sources. Perhaps you deleted a version from your TypeSpec, but didn't delete the associated swaggers?
196  
197  specification/carbon/resource-manager/Microsoft.Carbon/preview/2023-04-01-preview/main.json
198  specification/carbon/resource-manager/Microsoft.Carbon/preview/2024-02-01-preview/main.json

## Answer
Delete extra swagger files listed in the error or add previous versions back to TSP sources.
Add suppression rule for "ExtraSwagger" failure in the PR.
Discuss and provide proper guidance on how and when previews should be removed.
Keep only one active preview version across all Azure specs.
Enforce the standard of having only one active preview version in TypeSpec in the current quarter.

# Customizing key for child operation

## Question
Is there a way to customize the key of a parent resource for a specific child operation?
 
In SDKs we are being asked to change the name, but I couldn't find a good place to apply the @clientName decorator.

## Answer
Rename the property globally to "jobName" for consistent naming.
Use the @key decorator on the name property of the resource.

# Default value starting from a specific API version

## Question
Hello, we currently have a property (StatelessServiceProperties,minInstancePercentage) that does not currently have a default value in our spec, but in practice is treated as if it is 0.
azure-rest-api-specs/specification/servicefabricmanagedclusters/resource-manager/Microsoft.ServiceFabric/preview/2025-03-01-preview/servicefabricmanagedclusters.json at main · Azure/azure-rest-api-specs
 
We would like to treat the default prior to 2025-06-01 as 0, then as a different value from 2025-06-01 onward in our service.
 We haven't changed the default for an existing property before. Are there concerns about this intended behavior? 
In Typespec, is it possible to add a default value in our spec from a specific api version (say 2025-06-01) onward?

## Answer
I think you need to run this change by the breaking change board, as a change in the default may be breaking, depending on the details.  Also, the most important thing is to make sure that the API description accurately reflects service behavior - if the default has always been in place, for example, it may be better to just change the default and go through the breaking change process.  Yes, it is possible to do this in TypeSpec, but involves removing and renaming the old property and adding a new property with the new default, [like this](https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZ8wfYXp1cmUtdG9vbHMvyCstxhVjb3Jl3yvIK3Jlc291cmNlLW1hbmFnZXIiOwoKdXNpbmcgSHR0cDvHDFJlc3TIDFbpAI7IEkHESi5Db3JlzhJSx1xNxls7CgovKiogQ29udG9zb8RUxR4gUHJvdmlkZXIg5gCDbWVudCBBUEkuICovCkBhcm3IIE5hbWVzcGFjZQpAc2VydmljZSgjeyB0aXRsZTogIsdXyC1IdWJDbGllbnQiIH0pCkDnAUNlZCjnAL9zKQpuyFAgTWljcm9zb2Z0LtJG7wC2QVBJIMdNc%2BQAoWVudW3oARNzIHsKICDELjIwMjQtMTAtMDEtcHJldmlld8g1xDQgIEB1c2VEZXBlbmRlbmN5KPUBLy7IVi52MV8wX1DGSF8xKcRAYXJtQ29tbW9uVOQBz8cq10jLKctUNcRIYPIAqWAsCu0AxTH%2FAMX%2FAMX%2FAMX%2FAMX%2FAMXtAMXsAKntAMU1LTAzLTAx%2FwC9%2FwC9%2FwC9%2FwC9%2FgC95wChYCwKfeYCbUHoAm%2FrAogg6AOq5ADAbW9kZWwgRW1wbG95ZWUgaXMgVHJhY2tlZMh6PMgcUHJvcGVydGllcz7lAp4uLukAnuQDUFBhcmFtZXRlcskxPjvoAIbJX3DJRNJ8ymDpAv1BZ2Ugb2YgZcg%2F5gFwcmVtb3brA4Au9AG35QFacmXkA5pkRnJvbd4uLCAiYWdlIsQ1Zm9ybWVyQWdlPzogaW50MzI76AIB9gCOYWRk%2FwCM5QCMYcpRID0gMjHJVkNpdHnSV2NpdHk%2FOiBzdHLlBR7HLFByb2ZpbPQAhmVuY29kZSgiYmFzZTY0dXJs5QDMcMYwPzogYnl0ZXPJSFRoZSBzdGF0dXPES3RoZSBsYXN0IOQBgGF0aW9u5QUxICBAdmlzaWJpbGl0eShMaWZlY3ljbOQCk2Fkx13EIOUFwFN0YXTEZ%2BUCZswU6QIDxHPMMuUAgOUAymHpApDFd0Bscm%2FEO3VzCnVu5ANl0VTlAh%2FmARrpA53EX8hHIGNyZcQncmVxdWVzdCBoYXMgYmVlbiBhY2NlcHRlZMRnICBBxw46ICLICyLWUGnEQOQAtOkAwchE7ACcOiAizA%2FaTHVwZGF0xE%2FFQ1XHDjogIsgLyjvpBtrpAMTmANxk5wGiU3VjY2VlZOUAxckM0z%2FFNuQBTWZhaWzJPkbFDTogIsYJ3Dh3YXMgY2FuY2XKPkPHD%2BQHMscL%2FwFAIGRlbGXpAYBExA3mAPnICyLpBLjpBDLkA%2BvpAcrpBDRNb3ZlUscV6AQtxHNtb3bEaGZyb20gbG9j5gC8xW7EE%2FEDUMszdG%2FPMXRvyi%2F3AJVzcG9uc%2BsFRuYAlscW7ACX7gNsxT7FZMZ85gLuzW5pbnRlcmbkCFFP6AOUcyBleHRlbmRz9gkTLsspe30K5QjvyCPKG8tZ6ADN5gVuZ2V05AGnQco15APn7AXLIOcCe09y5QKn5QHWyy9Dxx1SZXBsYWNlQXN5bmPOP%2BUC99A3UGF0Y2hTzCws8wYRxUDmAj3PQOUCOmVXaXRob3V0T2vTd2xpc3RCecgwR3JvdXDPREzFIlBhcmVudNQ8U3Vic2NyaXDlAhzGO8YzzBnMOegGMCBzYW1wbOsDCWFjxUR0aGF05gIG6QXGdG8gZGlmZmXkAITvAonFKe4AskHFSO8BN%2BsDFMgN5gKL8wCSSEVBROoF78R%2BY2hlY2vqAKpleGlzdGVu5ggkIMYeRckU7wH7zR3uB%2F8%3D&e=%40azure-tools%2Ftypespec-autorest&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fresource-manager%22%5D%7D%7D)

# Discrepancy in the original LRO response and Status Monitor Response

## Question
Hi 
TypeSpec Discussion
 team,
 
We have created a long running resource action on our dataplane resource and have created a common status monitor endpoint (`operations/{operationId}`). Now the default status monitor response for the LRO comes out to be :
 
```
{
    "id": "",
    "status": ""
    "error": ""
}
```
However, following standard resource conventions for the status monitor, the LRO for it comes out to be : 
 
```
{
    "operationId": "",
    "status": "",
    "kind": ""
    "error": ""
}
 ```
 
Given this, how can I change the response of our Long Running Action defined? 
Here is Repro Link: https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZ8wfYXp1cmUtdG9vbHMvyCstxhVjb3JlIjsKCnVzaW5nIEh0dHA7xwxSZXN0yAxWyVfIEkHEPi5Db3Jl0hIuVHJhaXRzOwoKQHVzZUF1dGgoCiAgQXBpS2V5xA48xgtMb2NhdGlvbi5oZWFkZXIsICJhcGkta2V5Ij4gfCBPxCoyxS9bCiAgICB7xQYgIOQAwTrHH0Zsb3dUeXBlLmltcGxpY2l0LMclYXV0aG9yaXrFYVVybDogIuQBTXM6Ly9sb2dpbi5jb250b3NvLmNvbS9jb21tb24vb8Q1Mi92Mi4wL8hAZSLIUnNjb3BlczogW8lJd2lkZ2V0zUouZGVmYXVsdCJdxjd9CiAgXT4KKQpAc2VydmljZSgjeyB0aXRsZTogIkPGOSBXxUggTWFuYWdlciIgfccvZXLkATcie2VuZHBvaW50fcdy5QCT0D5BUElzxRnmAScvKiogClN1cOQCAWVkyCVT5gCFcyDIU3MgKHByb3RvY29sIGFuZCBob3N0bmFtZSwgZm9yIGV4YW1wbGU6CukA8mVzdHVzLmFwaS7yAP0pLgogKi%2FFfshfOiBzdHJpbmfkAJh95AEG5wKfZWQo5wC%2FLuYAmucA%2FC7nAmhzKQrkAIZzcGFjZSDVKjsKCuQA41RoySDvAUcg5wFxIMdzLuQAnGVudW3oAsxz5QEqxEXHESAyMDIyLTA4LTMxxCwgIOQCvURlcGVuZGVuY3ko6wLgyEMudjFfMF9QcmV2aWV3XzIpCiAgYMlGMGAsCn0KCi8vIE1vZGVscyAv0wHqAMdjb2xvciBvZiBhIOcBXOQAhnVu5ACa5gDbQ8Ui5AC26AFW5wDBQmxhY2vHJSDGJuUAwcUYOiAixQgiyS9XaGl0ZdMvxRg6ICLFCMovUuoCQMstUmVkOiAiUmVkyilHcmVl6ACszCvFGDogIsUIyi9CbHX0AIbEF%2BUAtHXkAITkAUEqKiBB7AEUQHJlc291cmNlKCLGFnMiKQpt5AFkyE%2FoAdrkAVjGIyDkAlDFPiAgQGtlecg7TmFtZSLkAbhAdmlzaWJpbGl0eShMaWZlY3ljbGUuUmVhZMQexD7oAs076ADBy17lAb3HX8UMOuwBtM0ySUTkAeV0yTwncyBtYW51ZmFjdHVyZchFzBNJZM17Li4uRXRhZ1Byb3BlcnR5O%2BQBJEBkb2MoIk9wZXLlBPEgSWQgUGF0aCBQYXJhbWV0ZXIu6QEjySVJZMQjySLlATXGSOQAp3VuaXF17ACuxG7FOMRL5gFF%2FwE3IMo07ADD6QCvS2luZMhebG9uZyBydW7kBmPOa%2BYDAukAtsU17wME8AD%2BcmVwcmVzZeQE32EgY2xvbukBqMla5QDEQ8Qb5gGo5AWPyg7mAnnGV1N0YXR1c%2BYDmfoApusClMkY6gKX6QCbx03uAWtzdGF0ZfcBZ8QcdXM6IEZvdW5kxlYuzU5l5QIUyk1r6wFVz0zEGzruAUrLPUVycuQEgGJqZWN0IHRoYXQgZGVzY3JpYmVzxU1lxSB3aGVu5wCVIGlzIFwiRmFpbGVkXCLGY8UlP%2B4Ar8VZ6AK6%2BAJ%2B5QIX5gEt5QGx5gGIUmVxdWVz5gPQbmV3xhVJZD%2FtAk0vL%2BoA1PgFZWFsaWFz6Ab%2F5gieID0g5wcgc1JlcGVhdGFibGXHcXMgJgogyR9Db25kacReYWzWIGxpZW7oAKxJ5AFUxm%2FrAJY97AkeUucCK8ogPO0Amj47CgppbnRlcmbkBv9McstI5AEJZ2V08AJQaXMgU3RhbmRhcmTSXclw5AOWPM87PuUBQ8pu5gFjxGnoBdVldCBhxxfmBJtnZXTHD2nsAOTOZ8Yi5ADKxkflA13pBtvGSUBwb2xsaW5nyUQo7ADsLvIA6OUEeGZpbmFsyjPGacUuxgvFJWFjxR0i5QJM5QKa6wJW%2FwEvTG9uZ1LmA%2BTIHkHFT%2BcA1SzzAqIsIG5ldmVy5ADwfQo%3D&e=%40azure-tools%2Ftypespec-autorest&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fdata-plane%22%5D%7D%7D

## Answer
The "id" of the status monitor should be "id," not "operationId." 
Use "@key("operationId") @visibility(Lifecycle.Read) id: string;" to ensure the id is called "operationId" in the route template but "id" in the response object.

# Swagger LintDiff error: Properties of a PATCH request body must not be required, property:location

## Question
Hi all,
I'm getting a LintDiff error for a PATCH API, where it wants the location property of the TrackedResource type to not be required (you can see the failure is here: Release the Fleet 2025-04-01-preview API by dvadas · Pull Request #33620 · Azure/azure-rest-api-specs). What's the simplest way to fix this?
 
I see an old thread about this issue: Alexander Zaslonov: Location property in TrackedResource breaks validation
posted in Azure SDK / TypeSpec Discussion on Thursday, October 24, 2024 12:15 PM. There are a few solutions mentioned there:
Use the #emit-common-types-schema setting and suppress the LintDiff error (how?)
Use the "better support for PATCH common types in both swagger and typespec that will be rolling out next sprint." Assumedly this is rolled out by now, but I'm not clear what it is?
Fallback to a custom solution
None of these are very clear. I tried setting #emit-common-types-schema to for-visibility-changesand it does indeed go and update a bunch of old API versions (as described in the old thread), so that seems like a bad idea.

## Answer
Use ArmCustomPatchAsync instead of ArmResourcePatchAsync for the PATCH API issue.
Create a custom GatePatch model to resolve the LintDiff error.

# How to make properties version-specific

## Question
Hi Team,
 
We have a new property that replaces an old one in a previous version. Specifically, the assignedResources property was introduced in version v2025_05_15_preview, and it is meant to replace the assignedResourceIds property from version v2023_04_15_preview.
 
Link: https://github.com/Azure/azure-rest-api-specs/blob/c2436c5068eed7978af83614c475866bf2550b6e/specification/cognitiveservices/Language.AnalyzeConversations-authoring/models/project.tsp#L506
 
How can we make assignedResourceIds available only in version v2023_04_15_preview, and to remain the generated swagger unchanged?
 
Thank you!

## Answer
se typespec versioning to manage property availability across versions. 
Replace properties between preview versions as per ARM requirements for API retirement. 
Allow multiple previews with associated swaggers in *.tsp. 
Use @typeChangedFrom to model type changes and renaming/removal for default changes. 

# TSP not generating right openapi.json (after fixing "Consistent Patch Properties" error)

## Question
Hi,
 
Here is my model (called Validation - and its ValidationProperties):
azure-rest-api-specs-pr/specification/azureresiliencemanagement/AzureResilience.Management/models/validation/validation.models.tsp at f1c4e7fcc685bc1043ae37aa7ec40060d564837e · Azure/azure-rest-api-specs-pr
Here is its PATCH definition:
```
@route(ServiceGroupRoute)
update is ArmCustomPatchAsync<
    Validation, 
    ValidationProperties,
    ServiceGroupParameters
>;
```
Here is the ValidationPatchProperties model:
azure-rest-api-specs-pr/specification/azureresiliencemanagement/AzureResilience.Management/models/validation/validation.models.tsp at f1c4e7fcc685bc1043ae37aa7ec40060d564837e · Azure/azure-rest-api-specs-pr
 
 
I had to add the additional properties  section in the ValidationPatchProperties model, otherwise I get the "Consistent Patch Properties" error:
Adding new RP Microsoft.AzureResilienceManagement by mkherani · Pull Request #21804 · Azure/azure-rest-api-specs-pr
 
But after adding the properties, although the error goes away, but the OpenApi.json is like this:
 
 
The C# models for the ValidationPatchProperties getting generated are also incorrect - they fail to capture every field except Identity (attached).
 
What am I missing ?

## Answer
The issue is that you are passing the only the rp-specific properties of your resource Request property of the ArmCustomPatch template.  You need to pass the entire PATCH request body, [as in this example](https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZ8wfYXp1cmUtdG9vbHMvyCstxhVjb3Jl3yvIK3Jlc291cmNlLW1hbmFnZXIiOwoKdXNpbmcgSHR0cDvHDFJlc3TIDFbpAI7IEkHESi5Db3JlzhJSx1xNxls7CgovKiogQ29udG9zb8RUxR4gUHJvdmlkZXIg5gCDbWVudCBBUEkuICovCkBhcm3IIE5hbWVzcGFjZQpAc2VydmljZSgjeyB0aXRsZTogIsdXyC1IdWJDbGllbnQiIH0pCkDnAUNlZCjnAL9zKQpuyFAgTWljcm9zb2Z0LtJG7wC2QVBJIMdNc%2BQAoWVudW3oARNzIHsKICDELjIwMjEtMTAtMDEtcHJldmlld8g1xDQgIEB1c2VEZXBlbmRlbmN5KPUBLy7IVi52MV8wX1DGSF8xKcRAYXJtQ29tbW9uVOQBz8cq10jLKctUNcRIYPIAqWAsCn3mAPNB6AD16wEOIOgCMOQAyG1vZGVsIEVtcGxveWVlIGlzIFRyYWNrZWToAII8yBxQcm9wZXJ0aWVzPuUBJC4u6QCm5AHWUGFyYW1ldGVyyTE%2BO%2BgAhslfcMlE0nzKYOkBg0FnZSBvZiBlyD%2FlAXhhZ2U%2FOiBpbnQzMjsKxylDaXR50ipjaXR5Pzogc3Ry5QLpxyxQcm9maWzTWUBlbmNvZGUoImJhc2U2NHVybCLkAWBwxjA%2FOiBieXRlc8lIVGhlIHN0YXR1c8RLdGhlIGxhc3Qg5ADFYXRpb27lAvwgIEB2aXNpYmlsaXR5KExpZmVjeWNs5AHgYWTHXcQg5QOLU3RhdMRn5QGrzBTpAUjEc8wy5QCA5QDKYekB1cV3QGxyb8Q7dXMKdW7kArLRVOUBZOYBGizsANLIRyBjcmXEJ3JlcXVlc3QgaGFzIGJlZW4gYWNjZXB0ZWTEZyAgQccOOiAiyAsi1lBpxEDkALTpAMHIROwAnDogIswP2kx1cGRhdMRPxUNVxw46ICLIC8o76QSl6QDE5gDcZOcBolN1Y2NlZWTlAMXJDNM%2FxTbkAU1mYWlsyT5GxQ06ICLGCdw4d2FzIGNhbmNlyj5Dxw%2FkBP3HC%2F8BQCBkZWxl6QGARMQN5gD5yAsi6QP96QN3bW926gHK6QN5TW92ZVLHFegDcsRzbW92xGhmcm9tIGxvY%2BYAvMVuxBPxA1DLM3RvzzF0b8ov9wCVc3BvbnPrBIvmAJbHFuwAl%2B4DbMU%2BxWTGfOYC7s1uaW50ZXJm5AYcT%2BgDlHMgZXh0ZW5kc%2FYG3i7LKXt9CuUGusgjyhvLWegAzeYEs2dldOQBp0HKNeQD5%2BwFECDnAntPcuUCp%2BUB1ssvQ8cdUmVwbGFjZUFzeW5jzj%2FlAvfIN0N1c3RvbVBhdGNoU8QqCiAgIOkAkyzFDvYA50ZvdW7kAybkBkXIHOYAkU3kAYTGSdBLyhDqBarFGT4KICDlAJ3mAprvANTlApdlV2l0aG91dE9r8wDUbGlzdEJ5yDBHcm91cM9ETMUiUGFyZW501DxTdWJzY3JpcOUCecY7xjPMGcw56AZgIHNhbXBs6wNmYWPFRHRoYXTmAmPpBiN0byBkaWZmZeQAhO8C5sUp7gCyQcVI5QGWyHcs7ANxyA3mAujzAJJIRUFE6gZMxH5jaGVja%2BoAqmV4aXN0ZW7mB8Ygxh5FyRTvAljNHe4HoQ%3D%3D&e=%40azure-tools%2Ftypespec-autorest&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fresource-manager%22%5D%7D%7D).  Note that if, you use the playground, or the vscode/vs extension, you can get documentation on the template parameters by hovering over the template.

# Is there way to change property from required to optional?

## Question
Hi, dear, I am working releasing video translation GA API with new version.
And we want to change a property from required to optional in the new version.
 
Previously we have a required property: sourceLocale: localeName
(Here localeName is a defined as: scalar localeName extends string;)
And now we want to change the property to optional: sourceLocale?: localeName
 
Is there anyway I can do to make the change for the new version?
 
I have tried this but doesn't work:
@doc("Translation input.")
model TranslationInput {
  @typeChangedFrom(ApiVersions.v2024_05_20_preview, localeName)
  sourceLocale?: localeName;
}
 
Could anyone help please?

## Answer
https://typespec.io/docs/libraries/versioning/reference/decorators/#@TypeSpec.Versioning.madeOptional

# tsp-client crash on C# generation

## Question
I get crash when trying to generate csharp code from TypeSpec.
Does .net sdk support tsp-client similar to other languages?
 
Here is the log:
ExternalError: Emitter "@azure-tools/typespec-csharp" crashed! This is a bug.
Please file an issue at https://github.com/Azure/autorest.csharp/issues

Error: Command failed: dotnet --roll-forward Major C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP/TempTypeSpecFiles/node_modules/@autorest/csharp/AutoRest.CSharp.dll --project-path C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP --new-project  --clear-output-folder false
    at genericNodeError (node:internal/errors:984:15)
    at wrappedFn (node:internal/errors:538:14)
    at checkExecSyncError (node:child_process:891:11)
    at execSync (node:child_process:963:15)
    at Object.$onEmit [as emitFunction] (file:///C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP/TempTypeSpecFiles/node_modules/@azure-tools/typespec-csharp/dist/src/emitter.js:131:13)
    at async runEmitter (file:///C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP/TempTypeSpecFiles/node_modules/@typespec/compiler/dist/src/core/program.js:668:9)
    at async file:///C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP/TempTypeSpecFiles/node_modules/@typespec/compiler/dist/src/core/program.js:647:9
    at async emit (file:///C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP/TempTypeSpecFiles/node_modules/@typespec/compiler/dist/src/core/program.js:646:5)
    at async compile (file:///C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP/TempTypeSpecFiles/node_modules/@typespec/compiler/dist/src/core/program.js:48:9)
    at async compileTsp (file:///C:/Users/jhakulin/AppData/Roaming/npm/node_modules/@azure-tools/typespec-client-generator-cli/dist/typespec.js:106:21)

--------------------------------------------------
Library Version                0.2.0-beta.20250326.2
TypeSpec Compiler Version      0.67.2
--------------------------------------------------
    at runEmitter (file:///C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP/TempTypeSpecFiles/node_modules/@typespec/compiler/dist/src/core/program.js:671:15)
    at async file:///C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP/TempTypeSpecFiles/node_modules/@typespec/compiler/dist/src/core/program.js:647:9
    at async emit (file:///C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP/TempTypeSpecFiles/node_modules/@typespec/compiler/dist/src/core/program.js:646:5)
    at async compile (file:///C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP/TempTypeSpecFiles/node_modules/@typespec/compiler/dist/src/core/program.js:48:9)
    at async compileTsp (file:///C:/Users/jhakulin/AppData/Roaming/npm/node_modules/@azure-tools/typespec-client-generator-cli/dist/typespec.js:106:21)
    at async generateCommand (file:///C:/Users/jhakulin/AppData/Roaming/npm/node_modules/@azure-tools/typespec-client-generator-cli/dist/commands.js:248:35)
    at async initCommand (file:///C:/Users/jhakulin/AppData/Roaming/npm/node_modules/@azure-tools/typespec-client-generator-cli/dist/commands.js:125:9)
    at async Object.handler (file:///C:/Users/jhakulin/AppData/Roaming/npm/node_modules/@azure-tools/typespec-client-generator-cli/dist/index.js:98:5) {
  info: {
    kind: 'emitter',
    metadata: {
      type: 'module',
      name: '@azure-tools/typespec-csharp',
      homepage: 'https://github.com/Microsoft/typespec',
      bugs: [Object],
      version: '0.2.0-beta.20250326.2'
    },
    error: Error: Command failed: dotnet --roll-forward Major C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP/TempTypeSpecFiles/node_modules/@autorest/csharp/AutoRest.CSharp.dll --project-path C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP --new-project  --clear-output-folder false
        at genericNodeError (node:internal/errors:984:15)
        at wrappedFn (node:internal/errors:538:14)
        at checkExecSyncError (node:child_process:891:11)
        at execSync (node:child_process:963:15)
        at Object.$onEmit [as emitFunction] (file:///C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP/TempTypeSpecFiles/node_modules/@azure-tools/typespec-csharp/dist/src/emitter.js:131:13)
        at async runEmitter (file:///C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP/TempTypeSpecFiles/node_modules/@typespec/compiler/dist/src/core/program.js:668:9)
        at async file:///C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP/TempTypeSpecFiles/node_modules/@typespec/compiler/dist/src/core/program.js:647:9
        at async emit (file:///C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP/TempTypeSpecFiles/node_modules/@typespec/compiler/dist/src/core/program.js:646:5)
        at async compile (file:///C:/Git/agents/azure-sdk-for-net/sdk/ai/Azure.AI.Projects.1DP/TempTypeSpecFiles/node_modules/@typespec/compiler/dist/src/core/program.js:48:9)
        at async compileTsp (file:///C:/Users/jhakulin/AppData/Roaming/npm/node_modules/@azure-tools/typespec-client-generator-cli/dist/typespec.js:106:21) {
      status: 3762504530,
      signal: null,
      output: [Array],
      pid: 125332,
      stdout: null,
      stderr: null
    }

## Answer
To ask in [another channel](https://teams.microsoft.com/l/message/19:906c1efbbec54dc8949ac736633e6bdf@thread.skype/1743387266613?tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47&groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&parentMessageId=1743383082045&teamName=Azure%20SDK&channelName=TypeSpec%20Discussion&createdTime=1743387266613) for support regarding the crash issue when generating C# code from TypeSpec.
To follow up on the issue and ensure there is a record of the question/answer for future users in the channel.

# Visibility-sealed error while validating locally

## Question
I am getting this error while running npx tsv locally:
```
error visibility-sealed: Visibility of property 'name' is sealed and cannot be changed.
> 155 | @Azure.ResourceManager.Private.armResourceInternal(FileSystemResourceProperties)
```
I tried removing the visibility decorator for the field/property 'name', but still the error was coming.
How do I resolve this?
Package version details:
```
"@typespec/compiler": "0.67.2",
"@typespec/http": "0.67.1",
"@typespec/sse": "0.67.1",
"@typespec/events": "0.67.1",
"@typespec/openapi": "0.67.1",
"@typespec/openapi3": "0.67.1",
"@typespec/prettier-plugin-typespec": "0.67.1",
"@typespec/rest": "0.67.1",
"@typespec/streams": "0.67.1",
"@typespec/versioning": "0.67.1",
"@typespec/xml": "0.67.1",
```
The relevant typespec file:
```
import "./../LiftrBase/main.tsp";

import "@typespec/openapi";
import "@typespec/http";
import "@typespec/rest";
import "@typespec/versioning";

using Azure.ResourceManager;
using LiftrBase;
using TypeSpec.Http;
using TypeSpec.OpenAPI;
using TypeSpec.Rest;
using TypeSpec.Versioning;
using TypeSpec.Reflection;
using Azure.ResourceManager.Foundations;

@versioned(LiftrBase.Storage.Versions)
@armLibraryNamespace
namespace LiftrBase.Storage;

@doc("Supported versions for LiftrBase.Storage resource model")
enum Versions {
  @doc("Dependent on Azure.ResourceManager.Versions.v1_0_Preview_1 and LiftrBase.Versions.v1_preview")
  @useDependency(Azure.ResourceManager.Versions.v1_0_Preview_1)
  @useDependency(LiftrBase.Versions.v2_preview)
  @armCommonTypesVersion(Azure.ResourceManager.CommonTypes.Versions.v3)
  v2_preview: "2024-02-01-preview",
}

/**
 * Properties specific to the Qumulo File System resource
 */
model FileSystemResourceProperties {
  /**
   * Marketplace details
   */
  marketplaceDetails: MarketplaceDetails;

  /**
   * Provisioning State of the resource
   */
  @visibility(Lifecycle.Read)
  provisioningState?: ProvisioningState;

  /**
   * Storage Sku
   */
  storageSku: string;

  /**
   * User Details
   */
  userDetails: UserDetails;

  /**
   * Delegated subnet id for Vnet injection
   */
  delegatedSubnetId: string;

  /**
   * File system Id of the resource
   */
  clusterLoginUrl?: string;

  /**
   * Private IPs of the resource
   */
  #suppress "@azure-tools/typespec-azure-core/casing-style" "This is the correct name"
  privateIPs?: string[];

  /**
   * Initial administrator password of the resource
   */
  @extension("x-ms-secret", true)
  adminPassword: string;

  /**
   * Availability zone
   */
  availabilityZone?: string;
}

/**
 * Common fields that are returned in the response for all Azure Resource Manager resources
 */
model Resource {
  /**
   * Fully qualified resource ID for the resource. Ex - /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{resourceType}/{resourceName}
   */
  @visibility(Lifecycle.Read)
  id?: string;

  /**
   * The name of the resource
   */
  @visibility(Lifecycle.Read)
  name?: string;

  /**
   * The type of the resource. E.g. "Microsoft.Compute/virtualMachines" or "Microsoft.Storage/storageAccounts"
   */
  @visibility(Lifecycle.Read)
  type?: string;

  /**
   * Azure Resource Manager metadata containing createdBy and modifiedBy information.
   */
  @visibility(Lifecycle.Read)
  systemData?: SystemData;
}

/**
 * The type used for update operations of the FileSystemResource.
 */
model FileSystemResourceUpdate {
  /**
   * The managed service identities assigned to this resource.
   */
  identity?: Azure.ResourceManager.CommonTypes.ManagedServiceIdentity;

  /**
   * Resource tags.
   */
  tags?: Record<string>;

  /**
   * The updatable properties of the FileSystemResource.
   */
  properties?: FileSystemResourceUpdateProperties;
}

/**
 * The updatable properties of the FileSystemResource.
 */
model FileSystemResourceUpdateProperties {
  /**
   * Marketplace details
   */
  marketplaceDetails?: MarketplaceDetails;

  /**
   * User Details
   */
  userDetails?: UserDetails;

  /**
   * Delegated subnet id for Vnet injection
   */
  delegatedSubnetId?: string;
}

#suppress "@azure-tools/typespec-azure-core/composition-over-inheritance" "For backward compatibility"
#suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-invalid-envelope-property" "For backward compatibility"
@Http.Private.includeInapplicableMetadataInPayload(false)
@Azure.ResourceManager.Private.armResourceInternal(FileSystemResourceProperties)
model FileSystemResource
  is Azure.ResourceManager.TrackedResource<FileSystemResourceProperties> {
  /**
   * Name of the File System resource
   */
  @path
  @key("fileSystemName")
  @segment("fileSystems")
  @visibility(Lifecycle.Read)
  @pattern("^[a-zA-Z0-9_-]*$")
  name: string;

  ...Azure.ResourceManager.ManagedServiceIdentityProperty;
}
```

## Answer
Remove the armResourceInternal decorator because it is redundant.
Refer to the docs for details on client codegen to generate the dotnet SDK artifact.https://teams.microsoft.com/l/message/19:906c1efbbec54dc8949ac736633e6bdf@thread.skype/1743533951656?tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47&groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&parentMessageId=1743500145279&teamName=Azure%20SDK&channelName=TypeSpec%20Discussion&createdTime=1743533951656

# Playground goes blank when choosing TCGC output

## Question
I've this playground [simple spec](https://teams.microsoft.com/l/message/19:906c1efbbec54dc8949ac736633e6bdf@thread.skype/1743179774446?tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47&groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&parentMessageId=1743179774446&teamName=Azure%20SDK&channelName=TypeSpec%20Discussion&createdTime=1743179774446) which outputs in openapi3 format, but as soon as I change output format to TCGC, the page goes blank.

## Answer
File a bug for the playground crash issue.
Fix the invalid value returned by TCGC causing the playground to crash.
Reproduce the TCGC crash using any emitter and create a TCGC issue.
Fix the playground crash issue in core and merge it.
Fix the TCGC error related to the @override method missing some params.

# Value template parameters as default values

## Question
I feel like I should be able to do something like this:
```
// Reusable package
@get op searchIssues<TQDefault extends valueof string="">(
    @query q: string=TQD): string;


// Concrete package implementing a Copilot plugin
@get op is global.githubapi.searchIssues<"repo:microsoft/typespec">;
```
Am I missing a technique or is this a gap?
 
The motivator here is that Sydney (M365 Copilot's orchestrator) for err, reasons, uses the default value in the OpenAPI spec as the actual forced value for the function parameter.
 
We're trying to create a really nice terse syntax for reusing a standard function but overriding a parameter value here or there.

## Answer
To track the issue for the capability of allowing template parameters to be used in default values. 
To create a custom decorator in the package called @omit(Model) that is a simple cover over @withoutOmittedProperties listing out the names of the properties of the model in a string union. 
To raise issues for minor additions to clean up the syntax, including the ability to put @query on a model and have it apply to all properties, and the ability to pass a type as the parameter to @withoutOmittedProperties. 

# Does TypeSpec support example generation for new added versions?

## Question
Hi team, when adding a new api version in the TypeSpec, is there any way I can generate thoes example Json files from preview version and with the "api-version" property changed? Or I will need to manually cope the example Json files from preview version and update the "api-version" property inside all of them?

## Answer
Example generation for specs works the same way as it did before.
Version-specific examples need to be placed under examples[version].
Swagger-based example generation can be used for new versions, but it is less attractive if examples are customized.

# Typespec models not emitted for Java

## Question
Hi Team, 
In our Typespec project, there are certain types which are not referenced by any operation or route. We wanted them emitted for manual coding. As you can see in the screenshot and link to the actual file (for. eg. MessageDeltaTextFileCitationAnnotation, they have been annotated @@usage and @@access to ensure generation. It does work and emits for python and C#, but doesn't work for Java. Any suggestions?
Thanks.
https://github.com/Azure/azure-rest-api-specs/blob/feature/azure-ai-projects/specification/ai/Azure.AI.Projects/agents/client.tsp

## Answer
Ask in the Java SDK channel about the omit-unreachable-types option.
Ensure using the latest main branch of azure-sdk-for-java or typespec-java emitter (0.28.0).

# Update API definition in typespec-providerhub

## Question
Hi team, I'm using providerhub template to generate a new RP, when trying to add custom API(simple health check for testing) in main.tsp, after build it doesn't generate the new model and controllers that I added, does the template have restrictions on what kind of APIs can be added? Here's what I tried to add into typespec.
```
// Add the health check operation
@doc("Health check endpoint to verify the service is running.")
model HealthCheckResponse {
  message: string;
}

interface HealthCheck {
  @get
  @route("/api/healthcheck")
  @doc("Returns a simple message indicating the service is running.")
  healthCheck(): HealthCheckResponse;
}
```

## Answer
The emitter is specifically for generating RPaaS extensions, not APIs. Custom controllers can be written outside of generated extensions.
Documentation for the typespec-providerhub-controller emitter can be found at [typespec-azure-pr/docs/getstarted/providerhub at providerhub · Azure/typespec-azure-pr](https://github.com/Azure/typespec-azure-pr/tree/providerhub/docs/getstarted/providerhub) and [typespec-azure-pr/packages/typespec-providerhub-controller/readme.md at providerhub · Azure/typespec-azure-pr](https://github.com/Azure/typespec-azure-pr/blob/providerhub/packages/typespec-providerhub-controller/readme.md).

# model service bus APIs or Event-Driven APIs

## Question
In Azure VMware Solution (AVS) we have many internal service bus APIs. I would like to be able to better model those in TypeSpec and be able to generate code from their TypeSpec definitions. I've done some experimenting with the WASI component model. I would like to use TypeSpec as a ES module in a JS-base component, but need https://github.com/microsoft/typespec/issues/5502 solved to be able to use it. Any chance it can be added to the roadmap in the next semester?

## Answer
Event-driven APIs should be added to the roadmap.
Packaging TypeSpec as an ES module should be considered for the roadmap.

# Data Plane Resource Additional List operation

## Question
We have a data plane resource that exists under a parent scope and has a list operation. We would like to add another list operation to enable the caller to list all resources of that type across parents.  How do I define this operation?
 
Have list by parent: GET /parentType/{parantName}/childType
 
Want list all: GET /childType
 
I'm currently duplicating the type model ( where duplicate model has no parent) to make this possible but I'm not a fan of the duplicate type:
```
@doc("A catalog agent definition.")
@parentResource(Catalog)
@resource("agentDefinitions")
model AgentDefinition {
  @doc("The agent name.")
  @visibility(Lifecycle.Read)
  @key("agentName")
  name: string;

  ...BaseAgentDefinition;
}

@doc("A catalog agent definition.")
@resource("agentDefinitions")
model AgentDefinitionAnyCatalog {
  @doc("The agent name.")
  @visibility(Lifecycle.Read)
  @key("agentName")
  name: string;

  ...BaseAgentDefinition;
}
```
```
  @doc("List AgentDefinition resources by Project")
  listAgentDefinitions is ResourceList<AgentDefinition>;

  @doc("List AgentDefinition resources for all Projects")
  #suppress "@azure-tools/typespec-azure-core/no-explicit-routes-resource-ops"
  @route("/")
  listAgentDefinitionsAll is ResourceList<AgentDefinitionAnyCatalog>;
```

## Answer
Use RPC template, ResourceAction template (with verb overridden to @get), or a lower-level operation template for listing all resources across parents.
Ensure @autoRoute is not enabled for the operation and add @listsResource decorator for associating operations with particular resources.
Use CustomPage or Page directly for paged responses.
Group operations using an interface or namespace.

# RP modeling Q: Resource Properties required on PUT but Optional on PATCH

## Question
Hello 
TypeSpec Discussion
,
 
Per Resource Provier Contract( https://armwiki.azurewebsites.net/api_contracts/guidelines/rpc.html) PATCH should support partial updates.
 
I have a few properties that are required on a PUT, but unfortunately this means that the PATCH experience is quite terrible in the Management SDKs generated using MTG.
 
How can I express properties as optional in PATCH while keeping them required in PUT? I'd really love some kind of OptionalPatch  annotation.
 
I would prefer avoiding defining the resource properties twice as that creates bloat in API docs and SDKs. Is that the preferred approach for now?
Alternatively, I could mark the properties all as optional and rely on server side validation logic (rather than RPaaS builtin swagger validation), but that's also not ideal.

## Answer
Use ArmCustomPatchAsync<Scheduler, Scheduler> to see if it generates a different SDK shape.
Change the TypeSpec model as suggested by Pan Shao to avoid a swagger breaking change.
Use @parameterVisibility(Lifecycle.Update) for the update operation.
The default PATCH template will implement best practices for ARM PATCH APIs once the linked issue is resolved in the next sprint.

# ARM Incremental TypeSpec (Preview) failures

## Question
Looks like it is an error and not a warning.
Unclear what the error is.
[Microsoft.DeviceUpdate] Added ExtendedLocation to all resource types · Azure/azure-rest-api-specs-pr@9ca73a8

## Answer
The check can be ignored for now.  This is why it has "Preview" in the name and doesn't block PRs.

# How to properly update the TypeSpec environment?

## Question
I tried inferring steps from the various installation documents but just managed to break my environment and have no idea how to fix it.
 
I saw a recent post where it was said to run `npm install -g @typespec/compiler` to get the latest (0.66) but it looks like it did not work for me. My compiler is still 0.64.
```
NORTHAMERICA+darkoa@darkoa-ws MINGW64 /d/Dev/Projects/git/github/azure-rest-api-specs-pr (RPSaaSMaster)
$ npm install -g @typespec/compiler

changed 268 packages in 11s

34 packages are looking for funding
  run `npm fund` for details

NORTHAMERICA+darkoa@darkoa-ws MINGW64 /d/Dev/Projects/git/github/azure-rest-api-specs-pr (RPSaaSMaster)
$ tsp compile specification/deviceupdate/DeviceUpdate.Edge.Management/
TypeSpec compiler v0.64.0

Diagnostics were reported during compilation:
```
I get a bunch of errors, although we made no changes recently. I am guessing those changes were made by the TypeSpec team and I am also guessing that if I manage to properly update tools, the errors should go away.
 
So, is there a single document that describes how to update the environment to the latest?

## Answer
Dependency Installation: The team decided that dependencies should be installed using npm ci in the spec repo. 1 2 3
Global CLI Access: The team agreed that running npm install -g @typespec/compiler only upgrades the global version for CLI access, not for local dependencies.
Documentation Reference: The team decided to use the documentation link shared by Mike Harder for managing the environment in the spec repo.
Warnings Clarification: The team clarified that the warnings received are related to SDK emitters configuration and are not errors.
Feedback Process: The team decided that feedback for the TypeSpecValidation rule should be directed to the Shanghai team via an issue in azure-rest-api-specs assigned to "wanlwanl".

# Changing TCGC generated names

## Question
I generated a Go package that can't build because it has conflicting definitions for a type. I didn't find any reference to this type in the input TSP, so I looked at the TCGC output. There I found a model with the name in question and isGeneratedName: true. So, it appears TCGC generated a name for this model and the Go emitter dutifully wrote a type having that name. Unfortunately, Go SDK guidelines mandate using the same name for a response envelope type, so the emitter wrote a conflicting type.
 
I want to continue following the Go SDK guidelines, so I need to change TCGC's generated name. Can I do this in TSP (I don't see anything I can apply @clientName to)?

## Answer
BatchCertificate has some properties that has only CREATE visibility, so typespec http lib will create a type without such properties to be the http operation response. since this type is created by http lib, not defined in the spec, we have no way to locate it.
since most languages do not care about visibility, i think there are two ways to workaround the problem:
1. change the spec to add @body directly, see line 833 of TypeSpec Azure Playground:
```
  getCertificate is ReadOperation<
    {
      @doc("The algorithm used to derive the thumbprint parameter. This must be sha1.")
      @path
      thumbprintAlgorithm: string;

      @doc("The thumbprint of the Certificate to get.")
      @path
      thumbprint: string;

      @doc("An OData $select clause.")
      @query
      @clientName("select")
      $select?: string[];
    },
    {@body body: BatchCertificate}
  >;
```
2. i'm working on a tcgc change to switch back to the original model for http response body with visibility. after that, the response will changed to BatchCertificate with CREATE properties, not the created GetCertificateResponse model. see pr [[tcgc] ignore visibility when finding response type if it is anonymous model by tadelesh · Pull Request #2402 · Azure/typespec-azure](https://teams.microsoft.com/l/message/19:906c1efbbec54dc8949ac736633e6bdf@thread.skype/1742279390520?tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47&groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&parentMessageId=1741990183297&teamName=Azure%20SDK&channelName=TypeSpec%20Discussion&createdTime=1742279390520)
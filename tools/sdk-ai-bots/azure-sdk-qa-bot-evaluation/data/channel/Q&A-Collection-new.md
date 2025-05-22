# Output folder 'ContentUnderstanding' appears to contain TypeSpec-generated swagger files, not generated from the current TypeSpec sources. Perhaps you deleted a version from your TypeSpec, but didn't delete the associated swaggers?

## question 
[Pull Request #21772](https://github.com/Azure/azure-rest-api-specs-pr/pull/21772) is currently failing TypeSpec validation because data-plane contains the Swagger for an older preview not specified in main.tsp.  The guidance I got from API board a month ago is that main.tsp should only list the latest preview. API version  But doing so triggered this error.
 
Should this be an error?  Or can we make this into a warning instead?

## answer
You should only track the latest preview version in your TypeSpec files.
If there are older preview Swagger files still in the repo, they can remain there for the required 90-day deprecation window, even if they're no longer listed in your .tsp files.

While TypeSpec validation currently throws an error if Swagger files exist without a matching version in TypeSpec, you can suppress this error by adding an entry to suppressions.yaml in your service's spec folder (not the global one).

Longer-term, the goal is to eliminate swagger files entirely, but for now, swagger and TypeSpec coexist. So:

Keep only the latest preview in .tsp

Keep older generated swagger for 90 days

Add suppressions if needed to prevent TypeSpec validation from failing

# Typespec Validation Failing on PR due to `typespec-go` configuration missing

## question 
Hi TypeSpec Discussion, 
CI has been failing constantly for our PR ([Azure Load Testing\] Add 2025-03-01-preview Data-Plane APIs by Harshan01 · Pull Request #32585 · Azure/azure-rest-api-specs](https://github.com/Azure/azure-rest-api-specs/pull/32585)) for Typespec Validation step. The logs show that we are missing the Go SDK configuration and I am able to produce this error locally as well. However, our service doesnt have a Go SDK and we are not planning to put it in scope right now. This check has suddenly started failing for our PRs, what should we do?

```
Executing rule：SdkTspConfigVa1idation
[SdkTspconfigVa1idation]：validation failed．
- Failed to find "options.@azure-tools/typespec-go.generate-fakes"．Please add "options.@azure-tools/typespec-go.generate-fakes".
- Failed to find "options.@azure-tools/typespec-go.inject一spans“．Please add "options.@azure-tools/typespec-go.inject一spans.
- Failed to find "options.@azure-tools/typespec-go.service-dir"．Please add "options.@azure-tools/typespec-go.service-dir".
- Failed to find "options.@azure-tools/typespec-go.package-dir"．Please add"options.@azure-tools/typespec-go.package-dir".
Please See https://aka.ms/azsdk/spec-gen-sdk-config for more info．
For additional information on TypeSpec validation, please refer to https://aka.ms/azsdk/specs/typespec-validation.
```

## answer
Suppressing go specific rules worked for me (locally, checking CI now). Thanks Mike. Also FYI Darren Cohen. Here's our suppression file:

```yml
- tool: TypeSpecValidation
  paths:
    - tspconfig.yaml
  rules:
    - SdkTspConfigValidation
  sub-rules:
    # Suppress validation for a Go emitter options
    - options.@azure-tools/typespec-go.generate-fakes
    - options.@azure-tools/typespec-go.inject-spans
    - options.@azure-tools/typespec-go.service-dir
    - options.@azure-tools/typespec-go.package-dir
  reason: 'Azure Load Testing does not support a Go SDK currently'
```
Let me know if I can help with anything!

# SdkTspConfigValidation failures for JS dataplane code generation

## question 
We have a TypeSpec PR here: https://github.com/Azure/azure-rest-api-specs/pull/33130 . We are emitting the new type of JS client library (not RLC), I think it's called dataplane code generation, or modular library, but I'm not sure. We set `is-modular-library: true` in the tspconfig.yaml. However, we're still getting `SdkTspConfigValidation` failures complaining that the word "rest" does not appear in the package name and folder name. See below. I believe that restriction only applies to RLC. I will try to suppress these errors, but checking in here if I'm doing the right thing in tspconfig.yaml, and if so, why do we see these errors? Thanks! Tagging Mike Harder.
```
Executing rule: SdkTspConfigValidation
Skip validation on options.@azure-tools/typespec-go.generate-fakes.
Skip validation on options.@azure-tools/typespec-go.inject-spans.
Skip validation on options.@azure-tools/typespec-go.service-dir.
Skip validation on options.@azure-tools/typespec-go.package-dir.
[SdkTspConfigValidation]: validation failed.
- The value of options.@azure-tools/typespec-ts.package-dir "ai-projects" does not match "/^(?:[a-z]+-)*rest$/". Please update the value of "options.@azure-tools/typespec-ts.package-dir" to match "/^(?:[a-z]+-)*rest$/".
- The value of options.@azure-tools/typespec-ts.package-details.name "@azure/ai-projects" does not match "/^\@azure-rest\/[a-z]+(?:-[a-z]+)*$/". Please update the value of "options.@azure-tools/typespec-ts.package-details.name" to match "/^\@azure-rest\/[a-z]+(?:-[a-z]+)*$/".
Please see https://aka.ms/azsdk/spec-gen-sdk-config for more info.
For additional information on TypeSpec validation, please refer to https://aka.ms/azsdk/specs/typespec-validation.
```

## answer
Yes, you are right the `rest` check should only apply to RLC and I will check if the validation works as expected. One thing to confirm is if we got the JS architects approval to release Modular for AI projects, generally we would recommend to release RLC for data-plane.

# Support for pagination

## question 
Hi, I have a typespec API which is a list api 
```
 listResources is ArmResourceActionSync<
    AutoAction,
    void,
    AutoActionResourceListResponse
  >;

model AutoActionResourceListResponse is Azure.Core.Page<AutoActionResource>;
```
This api needs to support pagination. How can I achieve that using Microsoft.TypeSpec.Providerhub.Controller? we use the package to generate our controllers, which doesn't let me add query parameters to our API

## answer
The controller emitter will emit an endpoint for your action, but you will need to add an endpoint manually for the next page endpoint (which is, presumably, a GET).
get is required for paging.  If you are not using GET, then automated paging mechanisms in ARM clients won't work.
It's possible to use a different paging mechanism, but will require custom code in SDKs
typespec-providerhub does not provide extra GET endpoints for resource actions that return lists - you would need to add this endpoint.  Since the controllers are partial, this should be fairly straightforward.
The question of whether RPaaS (providerhub) should support such an endpoint and typespec-providerhub should provide any required extension support for this is a good one.

# Multiple layers of inheritance for discriminative model

## question 
Like this
```
@discriminator("discountType")
model DiscountTypeProperties {
  discountType: string;
}

model DiscountTypeCustomPrice extends DiscountTypeProperties {
  discountType: "CustomPrice"
}

model DiscountTypeCustomPriceMultiCurrency extends DiscountTypeCustomPrice {
  discountType: "CustomPriceMultiCurrency";
}
```
DiscountTypeCustomPriceMultiCurrency is extending DiscountTypeCustomPrice, but these two have different discriminator values. How could I represent it?

## answer
this is not supported in TypeSpec with the inheritance based discriminator, I think we talked about that in the past and that was an anti pattern.
You could use discriminated union to represent that instead but I don' think they will be supported in the same way in emitters for now

# Extend ResourceModelWithAllowedPropertySet

## question 
My customer has this resource definition:
```
"Discount": {
      "type": "object",
      "x-ms-azure-resource": true,
      "description": "Resource definition for Discounts.",
      "allOf": [
        {
          "$ref": "../../../../../common-types/resource-management/v6/types.json#/definitions/ResourceModelWithAllowedPropertySet"
        }
      ],
      "properties": {
        "properties": {
          "description": "Discount properties",
          "x-ms-client-flatten": true,
          "$ref": "#/definitions/DiscountProperties"
        }
      }
    }
```
I tried this TypeSpec
```
@Azure.ResourceManager.Private.armResourceInternal(DiscountProperties)
@TypeSpec.Http.Private.includeInapplicableMetadataInPayload(false)
model Discount extends Azure.ResourceManager.CommonTypes.ResourceModelWithAllowedPropertySet {
  ...ResourceNameParameter<
    Resource = Discount,
    KeyName = "discountName",
    SegmentName = "discounts",
    NamePattern = "^[a-zA-Z0-9_\\-\\.]+$"
  >;

  @doc("The resource-specific properties for this resource.")
  @Azure.ResourceManager.Private.conditionalClientFlatten
  properties: DiscountProperties;
}
```
Error message is: @azure-tools/typespec-azure-resource-manager/arm-resource-invalid-base-type: The @armResourceInternal decorator can only be used on a type that ultimately extends TrackedResource, ProxyResource, or ExtensionResource.
 
I don't quite understand this error, since ResourceModelWithAllowedPropertySet does extend TrackedResource. How could I represent that swagger in TypeSpec?

## answer
We should not use ResourceModelWithAllowedPropertySet.  Instead, we should spread in the appropriate properties using a tracked resource.
 
The ResourceModelWithAllowedPropertySet is meant as an example, not as something resources should use, and so far usage in the specs repo has been incredibly light.  We should not be afraid of this kind of break to make the resulting spec more accurate and easier to evolve over time.

# Non-resource long running operation

## question 
[This](https://github.com/Azure/azure-rest-api-specs/blob/4e8d16d3793228046ac6171eadda4b8d26ad2b4f/specification/botservice/resource-manager/Microsoft.BotService/preview/2023-09-15-preview/botservice.json#L1235) is a long running operation, which is not a resource operation. [This](https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZ8wfYXp1cmUtdG9vbHMvyCstxhVjb3Jl3yvIK3Jlc291cmNlLW1hbmFnZXIiOwoKdXNpbmcgSHR0cDvHDFJlc3TIDFbpAI7IEkHESi5Db3JlzhJSx1xNxls7CgovKiogQ29udG9zb8RUxR4gUHJvdmlkZXIg5gCDbWVudCBBUEkuICovCkBhcm3IIE5hbWVzcGFjZQpAc2VydmljZSgjeyB0aXRsZTogIsdXyC1IdWJDbGllbnQiIH0pCkDnAUNlZCjnAL9zKQpuyFAgTWljcm9zb2Z0LtJG7wC2QVBJIMdNc%2BQAoWVudW3oARNzIHsKICDELjIwMjEtMTAtMDEtcHJldmlld8g1xDQgIEB1c2VEZXBlbmRlbmN5KPUBLy7IVi52MV8wX1DGSF8xKdhA5AGB1zUyxTVhcm1Db21tb25U5AIExyrXfcspy1Q1xEhg8gDeYCwKfeYBKEHoASrrAUMg6AJl5AD9bW9kZWwgRW1wbG95ZWUgaXMgVHJhY2tlZOgAgjzIHFByb3BlcnRpZXM%2B5QFZLi7pAKbkAgtQYXJhbWV0ZXLJMT476ACGyV9wyUTSfMpg6QG4QWdlIG9mIGXIP%2BUBrWFnZT86IGludDMyOwrHKUNpdHnSKmNpdHk%2FOiBzdHLlAx7HLFByb2ZpbNNZQGVuY29kZSgiYmFzZTY0dXJsIuQBYHDGMD86IGJ5dGVzyUhUaGUgc3RhdHVzxEt0aGUgbGFzdCDkAMVhdGlvbuUDMSAgQHZpc2liaWxpdHkoTGlmZWN5Y2zkAeBhZMddxCDlA8BTdGF0xGflAavMFOkBSMRzzDLlAIDlAMph6QHVxXdAbHJvxDt1cwp1buQC59FU5QFk5gEaLOwA0shHIGNyZcQncmVxdWVzdCBoYXMgYmVlbiBhY2NlcHRlZMRnICBBxw46ICLICyLWUGnEQOQAtOkAwchE7ACcOiAizA%2FaTHVwZGF0xE%2FFQ1XHDjogIsgLyjvpBNrpAMTmANxk5wGiU3VjY2VlZOUAxckM0z%2FFNuQBTWZhaWzJPkbFDTogIsYJ3Dh3YXMgY2FuY2XKPkPHD%2BQFMscL%2FwFAIGRlbGXpAYBExA3mAPnICyLpA%2F3pA3dtb3bqAcrpA3lNb3ZlUscV6ANyxHNtb3bEaGZyb20gbG9j5gC8xW7EE%2FEDUMszdG%2FPMXRvyi%2F3AJVzcG9uc%2BsEi%2BYAlscW7ACX7gNsxT7FZMZ85gLuzW5pbnRlcmbkBlFP6AOUcyBleHRlbmRz9gcTLsspe30K5QbvyCPKG8tZ6ADN5gSzZ2V05AGnQco15APn7AUQIOcCe09y5QKn5QHWyy9Dxx1SZXBsYWNlQXN5bmPOP%2BUC98g3Q3VzdG9tUGF0Y2jGKwogICDpAJQsxQ72AOhGb3Vu5AMn5AZGyBzmAJJN5AGFyXQs8wWePsZZTHJvSGVhZGVycyA95ACN5QCC6QElxhs8RmluYWxSZXN1bHQgPclMPiAmxUPoAJ7lByruAJN0cnlBZnRlcsZICiAg5QD%2B5gL77wE15QL4ZVdpdGhvdXRPa%2FMBNWxpc3RCecgwR3JvdXDPREzFIlBhcmVudNQ8U3Vic2NyaXDlAtrGO8YzzBnMOegGwSBzYW1wbOsDx2FjxUR0aGF05gLE6QaEdG8gZGlmZmXkAITvA0fFKe4AskHFSFPsAOws7APSyA3mA0nzAJJIRUFE6gatxH5jaGVja%2BoAqmV4aXN0ZW7mCCcgxh5FyRTvArnNHe8IAuwDilTmBG3kBxhyb3V0ZSgiL3PrAUNzL3vMD0lkfS%2FlBi%2FkAmcv6goXQm90U%2BYKcy%2FpAMJy5QJmcy97yRLmAnhJZH3lB8dAZ2V05gOkKOUCfS4uLkFwaecJTOkIveYC3C4uLuwB2UlkyyDFIS8qKsUIIOYEm0lE6ASXyX3kAUjkAvZ0b8RzLscu5AFJICBAcGF0aMUK8QCs6ATB5ACIKTrnAWPlAcM87QHQIHzEHOgHc0xyb8kn8AOYTHJvTOcCTfUDlcRX5Antx1jkA5r%2FA5boA5bkAIVFcnJvcsg%2B5QHu&e=%40azure-tools%2Ftypespec-autorest&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fresource-manager%22%5D%7D%2C%22options%22%3A%7B%22%40azure-tools%2Ftypespec-autorest%22%3A%7B%22omit-unreachable-types%22%3Afalse%2C%22emit-common-types-schema%22%3A%22never%22%7D%7D%7D) is what I wrote in TypeSpec.

```
@route("/subscriptions/{subscriptionId}/providers/Microsoft.BotService/operationresults/{operationResultId}")
  @get
  get(
    ...ApiVersionParameter,
    ...SubscriptionIdParameter,

    /**
     * The ID of the operation result to get.
     */
    @path
    operationResultId: string,
  ): ArmResponse<MoveResponse> | ArmAcceptedLroResponse<LroHeaders = ArmLroLocationHeader<FinalResult = MovedResponse> &
  Azure.Core.Foundations.RetryAfterHeader> | ErrorResponse;
```
It's still not LRO. How could I represent this operation in TypeSpec?

## answer
long-running GET is not allowed in ARM, or in Azure at all.  We should not support any such operation, this is undoubtedly a mistake, if it appears in any spec.
It would be allowed to do a non-resource POST operation, which you might model [like this](https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZ8wfYXp1cmUtdG9vbHMvyCstxhVjb3Jl3yvIK3Jlc291cmNlLW1hbmFnZXIiOwoKdXNpbmcgSHR0cDvHDFJlc3TIDFbpAI7IEkHESi5Db3JlzhJSx1xNxls7CgovKiogQ29udG9zb8RUxR4gUHJvdmlkZXIg5gCDbWVudCBBUEkuICovCkBhcm3IIE5hbWVzcGFjZQpAc2VydmljZSgjeyB0aXRsZTogIsdXyC1IdWJDbGllbnQiIH0pCkDnAUNlZCjnAL9zKQpuyFAgTWljcm9zb2Z0LtJG7wC2QVBJIMdNc%2BQAoWVudW3oARNzIHsKICDELjIwMjEtMTAtMDEtcHJldmlld8g1xDQgIEB1c2VEZXBlbmRlbmN5KPUBLy7IVi52MV8wX1DGSF8xKcRAYXJtQ29tbW9uVOQBz8cq10jLKctUNcRIYPIAqWAsCn3mAPNB6AD16wEOIOgCMOQAyG1vZGVsIEVtcGxveWVlIGlzIFRyYWNrZWToAII8yBxQcm9wZXJ0aWVzPuUBJC4u6QCm5AHWUGFyYW1ldGVyyTE%2BO%2BgAhslfcMlE0nzKYOkBg0FnZSBvZiBlyD%2FlAXhhZ2U%2FOiBpbnQzMjsKxylDaXR50ipjaXR5Pzogc3Ry5QLpxyxQcm9maWzTWUBlbmNvZGUoImJhc2U2NHVybCLkAWBwxjA%2FOiBieXRlc8lIVGhlIHN0YXR1c8RLdGhlIGxhc3Qg5ADFYXRpb27lAvwgIEB2aXNpYmlsaXR5KExpZmVjeWNs5AHgYWTHXcQg5QOLU3RhdMRn5QGrzBTpAUjEc8wy5QCA5QDKYekB1cV3QGxyb8Q7dXMKdW7kArLRVOUBZOYBGizsANLIRyBjcmXEJ3JlcXVlc3QgaGFzIGJlZW4gYWNjZXB0ZWTEZyAgQccOOiAiyAsi1lBpxEDkALTpAMHIROwAnDogIswP2kx1cGRhdMRPxUNVxw46ICLIC8o76QSl6QDE5gDcZOcBolN1Y2NlZWTlAMXJDNM%2FxTbkAU1mYWlsyT5GxQ06ICLGCdw4d2FzIGNhbmNlyj5Dxw%2FkBP3HC%2F8BQCBkZWxl6QGARMQN5gD5yAsi6QP96QN3bW926gHK6QN5TW92ZVLHFegDcsRzbW92xGhmcm9tIGxvY%2BYAvMVuxBPxA1DLM3RvzzF0b8ov9wCVc3BvbnPrBIvmAJbHFuwAl%2B4DbMU%2BxWTGfOYC7s1uaW50ZXJm5AYcT%2BgDlHMgZXh0ZW5kc%2FYG3i7LKXt9CuUGusgjyhvLWegAzeYEs2dldOQBp0HKNeQD5%2BwFECDnAntPcuUCp%2BUB1ssvQ8cdUmVwbGFjZUFzeW5jzj%2FlAvfIN0N1c3RvbVBhdGNoU8QqCiAgIOkAkyzFDvYA50ZvdW7kAybkBkXIHOYAkU3kAYTGSdBLyhDqBarFGT4KICDlAJ3mAprvANTlApdlV2l0aG91dE9r8wDUbGlzdEJ5yDBHcm91cM9ETMUiUGFyZW501DxTdWJzY3JpcOUCecY7xjPMGcw56AZgIHNhbXBs6wNmYWPFRHRoYXTmAmPpBiN0byBkaWZmZeQAhO8C5sUp7gCyQcVI5QGWyHcs7ANxyA3mAujqAJJsb25nLXJ1buUF3uQCUOQC9mlzdGlj5Qcy5QJk5ALrxxPGdegH3MZ15gFi5QGadm9pZOYBveQCn8QBLi4uT2vId%2BQBQSAgfcYi7AErxklTY29wZeYB3PABMEhFQUTqBurkARxjaGVja%2BoBSGV4aXN0ZW7mCGQgxh5FyRTvAvbNHe4IPw%3D%3D&e=%40azure-tools%2Ftypespec-autorest&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fresource-manager%22%5D%7D%2C%22options%22%3A%7B%22%40azure-tools%2Ftypespec-autorest%22%3A%7B%22emit-lro-options%22%3A%22all%22%7D%7D%7D)
But, in this case, this looks like they are modeling a subscription-level operationResult resource, which should just be modeled as a resource operation get that is not long-running.
 

# Extending 'Azure.ResourceManager.CommonTypes.ProxyResource' that doesn't define a discriminator.

## question 
Like this. I got warning
```
Model 'Employee' is extending 'Azure.ResourceManager.CommonTypes.ProxyResource' that doesn't define a discriminator. If 'Azure.ResourceManager.CommonTypes.ProxyResource' is meant to be used: - For composition consider using spread `...` or `model is` instead. - As a polymorphic relation, add the `@discriminator` decorator on the base model.
```
Why this gets related to discriminator?

## answer
In general we want to discourage any use of in inheritance if not used with discriminator hence the warning. I assume you have to do this because there is a non standard resource tghat can't do `model is ProxyResource`? If so probably have to suppress that too

# Model validation failures - Newer models introduced in new version adds the parent models in the older version.

## question 
Hi team!
 
I am trying to create a new version with a new models only specific to the latest version. I have also added the @added attribute to it. 
 
Despite this it is adding the models from the parent model - here (Recomendation, MigrationIssues, MigrationSuitability, etc) to all the older versions of the swagger which is not an intended behaviour.
 
Added the model implementation for more context. and adding the PR for a more broader context. 

[WACA changes for assessedWebApps by alphaNewrex · Pull Request #22616 · Azure/azure-rest-api-specs-pr](https://github.com/Azure/azure-rest-api-specs-pr/pull/22616/files)

```
@doc("Compound Assessment Recommendations.")
@added(WACAApiVersions.v2025_03_30_preview)
model CompoundAssessmentRecommendations
  is Recommendations<
    MigrationIssues,
    MigrationSuitability,
    Skus<MigrationSuitability>
  > {
  @doc("Arm id of the assessed resource. to get extended details.")
  extendedDetailsAssessedResourceArmId: string;
}
```

## answer
Yes, `@added` does not process a tree of models,  any model you introduce will have to be versioned in the same way (so all of those models would need their own `@added` decorators).

# Duplicate Example files for Typespec and other for Swagger. By design?

## question 
Following folder have same 54 files of example json. Is it by design?
- specification\apicenter\ApiCenter.Management\examples\2024-06-01-preview
- specification\apicenter\resource-manager\Microsoft.ApiCenter\preview\2024-06-01-preview\examples

## answer
Yes.  The example files in the OpenAPI directory are automatically copied when you compile the spec, they are essentially part of the emitted OpenAPI

# Override contentType: "application/json" for ResourceCreateOrUpdate

## question
Hi TypeSpec Discussion,
I am migrating an old swagger to typespec. I came across a method which is a PATCH ops with a application/json as content type. The API behaves exactly as a merge-patch route, but I cannot change it since it'll be consider a breaking change. In order to still use the convenient functionalities of typespec traits, I have define a custom function like so [playground](https://azure.github.io/typespec-azure/playground/?c=Ly8gLd8B3wHdAQovLyBDb3B5cmlnaHQgKGMpIE1pY3Jvc29mdCBDb3Jwb3JhdGlvbi4gQWxsIMUlcyByZXNlcnZlZC7EPUxpY2Vuc2VkIHVuZGVyIHRoZSBNSVTIFy4gU2VlyQ10eHQgaW7FJHByb2plY3Qgcm9vdCBmb3IgbMYkIGluZm9ybcZ1xGD%2FAMDfAd4BCgppbXBvcnQgIkB0eXBlc3BlYy9yZXN0IjvTGXZlcnNpb25pbmfVH2h0dHDVGW9wZW5hcGnMHGF6dXJlLXRvb2xzL8goLcYVY29yZSI7Cgp1c2luZyBUeXBlU3BlYy5IdHRwO9AVUmVzdMgVQcQ%2BLkNvcmXSEi5UcmFpdHM7CgpuYW1lc3BhY2XGHjsKCiNzdXBwcmVzc%2F8Al29yZS9uby1wcml2YXRlLXVzYWdlIiAiIgpARm91bmTlAbBzLlDGHy5lbnN1cmVWZXJiKCJSZXNvdXJjZUNyZWF0ZU9yVXBkYXRlIiwgIlBBVENIIikKQGPFG3PIHHPIMyjICSkKQHBhcmFtZXRlclZpc2liaWxpdHkoTGlmZWN5Y2xlLsZdLCDKEsZLxTl0Y2gKb3Ag9gCJV2l0aEpzb25Db250ZW505AFdPAogyS0gZXh0ZW5kc%2BwBdmZsZWPlAqBNb2RlbCwKICDmAWHfLGRlbCA9IHt9xDFJbnRlcmZhY2XfOtI6RXJyb3JSZXNwb%2BQDLj3sAefsAYrNJwo%2BIGlz2CroAOpPcGXmA%2BzsAP%2FEcnsKICAgIC8qKsUIICogVGhlIOQCSiBvZu0DzHRvIHVzZS7HJy%2FFCEBkb2MoIlRoaeQEN3F1ZXN0IGhhcyBhIEpTT04gYm9keS4iKcYq7QLtLmhlYWRlcigi5wGYLcQexypj6gGrOiAiYXBwbGlj5QC6L2pzb27kAz7EJi4u9QDnQm9keTzIDT47yCvxAy7pAszFD1Byb3BlcnRpZXPkAR7EAcYmICbwAcvkATLJIExv5gCZLlDoAr7OIOUA3Xh05wK%2FIHzOFuYCocUw5QCp5QIA%2FwHGxkpkT3JPa%2BgB%2FukA7iAmxUz%2FAOv%2FAOv%2FAOtu5ACKxXf%2FAOn4AOk%2B6wNQ1XLvAto7Cg%3D%3D&e=%40azure-tools%2Ftypespec-autorest&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fresource-manager%22%5D%7D%7D) and am using it.

Is there a better alternative?
## answer
Yes. it's expected that if you are using a non-standard content-type for PATCH you will need to use a custom operation or a custom operation template.

# Customizing key for child operation

## question 
Is there a way to customize the key of a parent resource for a specific child operation?
 
In SDKs we are being asked to change the name, but I couldn't find a good place to apply the `@clientName` decorator.

## answer   
In the data plane API, resource keys are typically derived from the parent resource’s key and are not individually specified for each operation. By default, the key names for operations are inherited from the parent resource's key. The @clientName decorator is used to modify the name used by the client, not to change the parent resource's key in operations. To change the parent resource's key name globally, the simplest solution is to use the @key decorator on the name property to ensure consistency.

Changing the key name specifically for certain operations is not straightforward because operation keys are typically tied to the parent resource key. Although you can explicitly define different keys for an operation, this generally requires redefining the operation.

In your case, the most reasonable approach would be to update the parent resource's key name to jobName across all operations to maintain consistency rather than using a different name for some operations. This approach reduces naming conflicts and ensures consistency.

For how to explicitly specify key definitions, you can refer to the Azure Data Plane documentation, which outlines how to define keys for each service's operations and resources.

# Default value starting from a specific API version?

## question 
Hello, we currently have a property (StatelessServiceProperties,minInstancePercentage) that does not currently have a default value in our spec, but in practice is treated as if it is 0.
[azure-rest-api-specs/specification/servicefabricmanagedclusters/resource-manager/Microsoft.ServiceFabric/preview/2025-03-01-preview/servicefabricmanagedclusters.json at main · Azure/azure-rest-api-specs](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/servicefabricmanagedclusters/resource-manager/Microsoft.ServiceFabric/preview/2025-03-01-preview/servicefabricmanagedclusters.json)
We would like to treat the default prior to 2025-06-01 as 0, then as a different value from 2025-06-01 onward in our service.
1. We haven't changed the default for an existing property before. Are there concerns about this intended behavior? 
2. In Typespec, is it possible to add a default value in our spec from a specific api version (say 2025-06-01) onward?

## answer
I think you need to run this change by the breaking change board, as a change in the default may be breaking, depending on the details.  Also, the most important thing is to make sure that the API description accurately reflects service behavior - if the default has always been in place, for example, it may be better to just change the default and go through the breaking change process.  Yes, it is possible to do this in TypeSpec, but involves removing and renaming the old property and adding a new property with the new default, [like this](https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZ8wfYXp1cmUtdG9vbHMvyCstxhVjb3Jl3yvIK3Jlc291cmNlLW1hbmFnZXIiOwoKdXNpbmcgSHR0cDvHDFJlc3TIDFbpAI7IEkHESi5Db3JlzhJSx1xNxls7CgovKiogQ29udG9zb8RUxR4gUHJvdmlkZXIg5gCDbWVudCBBUEkuICovCkBhcm3IIE5hbWVzcGFjZQpAc2VydmljZSgjeyB0aXRsZTogIsdXyC1IdWJDbGllbnQiIH0pCkDnAUNlZCjnAL9zKQpuyFAgTWljcm9zb2Z0LtJG7wC2QVBJIMdNc%2BQAoWVudW3oARNzIHsKICDELjIwMjQtMTAtMDEtcHJldmlld8g1xDQgIEB1c2VEZXBlbmRlbmN5KPUBLy7IVi52MV8wX1DGSF8xKcRAYXJtQ29tbW9uVOQBz8cq10jLKctUNcRIYPIAqWAsCu0AxTH%2FAMX%2FAMX%2FAMX%2FAMX%2FAMXtAMXsAKntAMU1LTAzLTAx%2FwC9%2FwC9%2FwC9%2FwC9%2FgC95wChYCwKfeYCbUHoAm%2FrAogg6AOq5ADAbW9kZWwgRW1wbG95ZWUgaXMgVHJhY2tlZMh6PMgcUHJvcGVydGllcz7lAp4uLukAnuQDUFBhcmFtZXRlcskxPjvoAIbJX3DJRNJ8ymDpAv1BZ2Ugb2YgZcg%2F5gFwcmVtb3brA4Au9AG35QFacmXkA5pkRnJvbd4uLCAiYWdlIsQ1Zm9ybWVyQWdlPzogaW50MzI76AIB9gCOYWRk%2FwCM5QCMYcpRID0gMjHJVkNpdHnSV2NpdHk%2FOiBzdHLlBR7HLFByb2ZpbPQAhmVuY29kZSgiYmFzZTY0dXJs5QDMcMYwPzogYnl0ZXPJSFRoZSBzdGF0dXPES3RoZSBsYXN0IOQBgGF0aW9u5QUxICBAdmlzaWJpbGl0eShMaWZlY3ljbOQCk2Fkx13EIOUFwFN0YXTEZ%2BUCZswU6QIDxHPMMuUAgOUAymHpApDFd0Bscm%2FEO3VzCnVu5ANl0VTlAh%2FmARrpA53EX8hHIGNyZcQncmVxdWVzdCBoYXMgYmVlbiBhY2NlcHRlZMRnICBBxw46ICLICyLWUGnEQOQAtOkAwchE7ACcOiAizA%2FaTHVwZGF0xE%2FFQ1XHDjogIsgLyjvpBtrpAMTmANxk5wGiU3VjY2VlZOUAxckM0z%2FFNuQBTWZhaWzJPkbFDTogIsYJ3Dh3YXMgY2FuY2XKPkPHD%2BQHMscL%2FwFAIGRlbGXpAYBExA3mAPnICyLpBLjpBDLkA%2BvpAcrpBDRNb3ZlUscV6AQtxHNtb3bEaGZyb20gbG9j5gC8xW7EE%2FEDUMszdG%2FPMXRvyi%2F3AJVzcG9uc%2BsFRuYAlscW7ACX7gNsxT7FZMZ85gLuzW5pbnRlcmbkCFFP6AOUcyBleHRlbmRz9gkTLsspe30K5QjvyCPKG8tZ6ADN5gVuZ2V05AGnQco15APn7AXLIOcCe09y5QKn5QHWyy9Dxx1SZXBsYWNlQXN5bmPOP%2BUC99A3UGF0Y2hTzCws8wYRxUDmAj3PQOUCOmVXaXRob3V0T2vTd2xpc3RCecgwR3JvdXDPREzFIlBhcmVudNQ8U3Vic2NyaXDlAhzGO8YzzBnMOegGMCBzYW1wbOsDCWFjxUR0aGF05gIG6QXGdG8gZGlmZmXkAITvAonFKe4AskHFSO8BN%2BsDFMgN5gKL8wCSSEVBROoF78R%2BY2hlY2vqAKpleGlzdGVu5ggkIMYeRckU7wH7zR3uB%2F8%3D&e=%40azure-tools%2Ftypespec-autorest&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fresource-manager%22%5D%7D%7D)
 
# Does TypeSpec support example generation for new added versions?

## question 
Hi team, when adding a new api version in the TypeSpec, is there any way I can generate thoes example Json files from preview version and with the "api-version" property changed? Or I will need to manually cope the example Json files from preview version and update the "api-version" property inside all of them?

## answer
Example generation for specs works the same way as it did before.
You will need to place version specific examples under `examples\[version]`. So if you are adding a new version, you can copy over the example files and make appropriate add/remove/update to them including the `api-version`
Note that you can also use swagger-based example generation, for the new version (which is less attractive if you have customized the examples)

# Description changes across versions?

## question 
Hi, as part of this PR: [Service Fabric Managed Clusters - API version 2025-03-01-preview · Azure/azure-rest-api-specs@599e269](https://github.com/Azure/azure-rest-api-specs/actions/runs/14090043123/job/39464153437?pr=33332)

My team wanted to add more details to a model description. This change results in a change in all spec versions generated with Typespec, and causes the Typespec validation to fail if I don't include the changes to the older specs. 
 
I wanted to know what the best course of action was for passing this check. Since we don't expect updates to our older specs, is it ok to just change the output path in our tspconfig.yaml to only point at the current version of the output spec? Or is there a better way to handle this?

## answer
Honestly, the best thing is to update your docs and take the update in previous versions (which are likely now more accurately described as well). Documentation-only updates should not be flagged as breaking changes at all. If they are for some reason, I'll just approve it.  

# Typespec Validation issue

## question 
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

## answer
You may need to update to the latest typespec version - some of these particular properties have changed in a recent version. What you are using here isn't the latest style to specify these things.
Doc for future reference: https://github.com/Azure/azure-rest-api-specs/wiki/TypeSpec-Validation#running-locally

# Seeking Guidance on Defining ResourceStatusCode in TypeSpec

## question 
Hello 
TypeSpec Discussion
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

## answer
As per one of our previous understanding, we defined similarly as an open union: [azure-rest-api-specs-pr/specification/impact/Impact.Management/connectors.tsp at RPSaaSMaster · Azure/azure-rest-api-specs-pr](https://github.com/Azure/azure-rest-api-specs-pr/blob/RPSaaSMaster/specification/impact/Impact.Management/connectors.tsp#L88-L94).  This explicitly allows any string value.
 
Generally, the reason for doing this is that you think additional values will be enabled in future versions (or even in this version).  Note that, if you do not make this an open union, then adding any values in any future api-version would be a breaking change (which is why this is recommended).
 
There are RPaaS extensions for validation that would allow you to reject requests for values that are not valid.

# Update API definition in typespec-providerhub

## question 
Hi team, I'm using [providerhub template](https://armwiki.azurewebsites.net/rpaas/gettingstarted.html#bootstrap-your-development-with-typespec-formerly-cadl) to generate a new RP, when trying to add custom API(simple health check for testing) in main.tsp, after build it doesn't generate the new model and controllers that I added, does the template have restrictions on what kind of APIs can be added? Here's what I tried to add into typespec.

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

## answer
Yes, the emitter is specifically about generating RPaaS extensions, not about generating APIs.  You should be able to generate the model, however from your spec.
 
The emitter only updates a specific set of folders, so you can write your own controllers for any APIs outside of generated extensions, and just be sure not to place them in the folder with generated artifacts.

# How to properly update the TypeSpec environment?

## question 
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

## answer
The issue you're facing is related to updating your local TypeSpec environment. Here's a summary of the solution:

Update Local Dependencies:

Running npm install -g @typespec/compiler only updates the global TypeSpec compiler, which is not typically used for local development unless you need access to TypeSpec commands globally.

To properly update your local environment, navigate to your repository and run npm ci. This command installs the exact versions of dependencies specified in the package-lock.json, ensuring consistency.

TypeSpec Versions in Repositories:

When working with repositories like azure-rest-api-specs, you should always install dependencies based on the local package.json and package-lock.json at the root of your branch. This ensures you're using the correct version of TypeSpec and associated tools for your current project.

Dealing with Configuration Warnings:

The warnings you're seeing (such as missing options for SDK emitters) are configuration-related, not errors. These warnings appear when SDK emitters for specific languages (like Go, Python, C#) are not installed, but they do not affect your immediate work with TypeSpec validation. These can be safely ignored unless you need to work with SDK generation.

Suppression and Documentation:

If you want to suppress specific warnings, you can modify the suppressions.yaml file, but it's important to follow the TypeSpec guidelines to ensure proper environment configuration.

# How to restrict importing typespec files in main based off of versions.

## question 
Hi Team,
We have 2 imports in our main.tsp to include other resource type tsp files, however we do not want to include one of the resourcetype to the new api version we want to introduce.  Is there a way to do conditional imports based off of version in main?
 
Thank you in advance!
## answer
no, you have to mark the models/types and everything that you want to remove with the @removed decorator using the versioning library
 
if you are in preview version I think also the policy is to only have a single preview version in the spec repo at the timme now so you could also just delete it

# Brownfield TypeSpec migration

## question 
Hi, Is there a timeframe for existing brownfield RPs to move from OpenAPI swagger to TypeSpec.  Is it possible to mix TypeSpec with handwritten swagger and migrate in phases. Say for example migrate one resource type at a time to minimize risk. 

## answer
Migration to TypeSpec for existing services is not yet mandatory, but it is suggested, and teams should be planning for it in Bromine and Krypton
Services must wholly switch to TypeSpec, there is no allowed mixing of hand-written and generated swagger
Servicesmust conform to a single, unified api-version for their service, servicesthat currently use different api-versions for parts of their service are going to need to plan for conformance -this either means SDK splitting or version uniformity.  Teams that use this 'different api-versions for different resources in the same sdk' pattern are not good candidates for conversion at the moment
In generally, the more compliant your service is to the RPC and best practices, the easier conversion will be
There is documentation on converting here: [Getting started | TypeSpec Azure](https://azure.github.io/typespec-azure/docs/migrate-swagger/01-get-started/)

We highly encourage you do the migration. Any problem related to https://azure.github.io/typespec-azure/docs/migrate-swagger/01-get-started/, don't hesitate to reach out to me.
 
# Setting default value for a union type for only some API versions

## question 
Hi team,
Is it possible to set default value for a union type for only some API versions?
 
For below union type, I want to set default value as ApprovalStatus.Pending from API version 2024-12-01-preview onwards. Is this possible?

```
@doc("Approval Status Enum")
union ApprovalStatus {
  @doc("ApprovalStatus Type Approved")
  Approved: "Approved",

  @doc("ApprovalStatus Type Rejected")
  Rejected: "Rejected",

  @doc("ApprovalStatus Type Pending")
  Pending: "Pending",

  @added(Microsoft.Mission.Versions.v2024_11_01_preview)
  @doc("ApprovalStatus Type Deleted")
  Deleted: "Deleted",

  @added(Microsoft.Mission.Versions.v2024_11_01_preview)
  @doc("ApprovalStatus Type Expired")
  Expired: "Expired",

  string,
}
```

## answer
From a purely "what is the TypeSpec syntax for assigning a default value to a type", no, I don't think we have any way of doing this. You can create a reusable property that you can spread into various places if that is what you are trying to accomplish.
 
From a service API design perspective, what actually changed between the API versions? The pending status was already there in older API versions. Presumably the property that had a type of ApprovalStatus always created things in a pending state (it looks like this is a type that would be used in a persisted model/resource). Wasn't that always conceptually pending? 
I think that shouldn't a problem on the breaking change part. 

# Readonly on model

## question 
We have such swagger
```
"ReadonlyOnModel": {
  readonly: true,
  properties: {}
},
"AnotherModel": {
  properties: {
    "a": {
      $ref: "ReadonlyOnModel"
    }
  }
}
```
When the type of property "a" in another model is that "ReadonlyOnModel", M4 will give a readonly on that property "a". I want to confirm if the equivalent TypeSpec should be
```
model AnotherModel {
  @visibility(Lifecycle.Read)
  a: ReadonlyOnModel;
}
model ReadonlyOnModel {}
```
cc Alitzel Mendez : Common type replacement relates to this. I remember we have several models in the original common type have readonly on them and you removed these because TypeSpec cannot represent readonly on model. Then if any service refers to this model, those properties might need to be updated.

## answer
TypeSpec can only attach readOnly to properties, not to models or scalars.  Functionally (assuming all references to ReadOnlyModel are through readOnly properties),  these Swagger docs are equivalent, see: [autorest/docs/openapi/howto/$ref-siblings.md at main · Azure/autorest](https://github.com/Azure/autorest/blob/main/docs/openapi/howto/%24ref-siblings.md) from the perspective of both TypeSpec and autorest, these descriptions are equivalent.
 
# Exclude property from list that is in create/update/get

## question 
We have an ARM resource that has a property , e.g.  properties.blob that will contain a very large amount of text not appropriate for list responses. 
 
I see we can override the properties for ArmResourceCreateOrReplaceAsync and ArmResourcePatchAsync, but I don't see a way to do this for ArmResourceRead or ArmResourceListByParent.
 
Is there a way to exclude a property from properties when listing other than defining a new resource type that lacks the specific property?
 
The property is required. The only way I can see to do this would be to make it appear optional but return an error is the user tried to set it to empty or null.

## answer
First, has this been through ARM Review?  Having a very large piece of data as part of a resource could present issues.
 
To answer the question, you can change the response type for a list operation using the `Response` parameter, [as in this playground](https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZ8wfYXp1cmUtdG9vbHMvyCstxhVjb3Jl3yvIK3Jlc291cmNlLW1hbmFnZXIiOwoKdXNpbmcgSHR0cDvHDFJlc3TIDFbpAI7IEkHESi5Db3JlzhJSx1xNxls7CgovKiogQ29udG9zb8RUxR4gUHJvdmlkZXIg5gCDbWVudCBBUEkuICovCkBhcm3IIE5hbWVzcGFjZQpAc2VydmljZSgjeyB0aXRsZTogIsdXyC1IdWJDbGllbnQiIH0pCkDnAUNlZCjnAL9zKQpuyFAgTWljcm9zb2Z0LtJG7wC2QVBJIMdNc%2BQAoWVudW3oARNzIHsKICDELjIwMjEtMTAtMDEtcHJldmlld8g1xDQgIEB1c2VEZXBlbmRlbmN5KPUBLy7IVi52MV8wX1DGSF8xKcRAYXJtQ29tbW9uVOQBz8cq10jLKctUNcRIYPIAqWAsCn3mAPNB6AD16wEOIOgCMOQAyG1vZGVsIEVtcGxveWVlIGlzIFRyYWNrZWToAII8yBxQcm9wZXJ0aWVzPuUBJC4u6QCm5AHWUGFyYW1ldGVyyTE%2BO%2BgAhslfcMlE0nzKYOkBg0FnZSBvZiBlyD%2FlAXhhZ2U%2FOiBpbnQzMjsKxylDaXR50ipjaXR5Pzogc3Ry5QLpxyxQcm9maWzTWUBlbmNvZGUoImJhc2U2NHVybCLkAWBwxjA%2FOiBieXRlc8lIc29tZSBsb25nIHRleHTGQ8QPVGV4dNJ2VGhlIHN0YXR1c8R5dGhlIGxhc3Qg5ADzYXRpb27lAyogIEB2aXNpYmlsaXR5KExpZmVjeWNs5AIOYWTnAIvEIOUDuVN0YXTkAJXlAdnMFOkBdv8B%2FPABgExpc3T8AgDEIP8CBP8CBP8CBNFk%2FwII%2FwII%2FwII%2FwII%2FwII6wII%2FwHa%2FwHa%2FwHazRTlAUzyATFSZXN1bOUBu%2BgBjMoW7QG%2BPucFCOQAssxx5QC%2F5QEJYekCIOYFumxyb8R6dXMKdW7kBPnxAJPlAaPmAVks7AERyEcgY3JlxCdyZXF1ZXN0IGhhcyBiZWVuIGFjY2VwdGVkxGcgIEHHDjogIsgLItZQacRA5AC06QDByETsAJw6ICLMD9pMdXBkYXTET8VDVccOOiAiyAvKO%2BkG7OkAxOYA3GTnAeFTdWNjZWVk5QDFyQzTP8U25AFNZmFpbMk%2BRsUNOiAixgncOHdhcyBjYW5jZco%2BQ8cP5AdExwv%2FAUAgZGVsZekBgETEDeYA%2BcgLIukGROkDum1vduoByukDvE1vdmVSxxXoA7HEc21vdsRoZnJvbSBsb2PmALzFbsQT9QUhxzN0b88xdG%2FKL%2FcAlXNwb25z6wTW5gCWxxbsAJfuA6vFPsVkxnzmAu7NbmludGVyZuQIY0%2FoA9NzIGV4dGVuZHP2CSUuyyl7fQrlCQHII8oby1noAM3mBPJnZeUDzUHKNeQEJuwFUyDnAntPcuUCp%2BUB1ssvQ8cdUmVwbGFjZUFzeW5jzj%2FlAvfIN0N1c3RvbVBhdGNoU8QqCiAgIOkAkyzFDvYA50ZvdW7kAybkCIzIHOYAkU3kAYTGSdBLyhDqBenFGT4KICDlAJ3mAprvANTlApdlV2l0aG91dE9r8wDUI3N1cHByZXNz%2Fwst7wstL2FybcoV6QW2xBPlAj0i5AfXaXN0QnnoAItHcm91cO8An%2BQFX0J5UGFyZW509AFB6QKLPfMFu%2BgA%2Bf8Awv8Awv8AwuQAoFN1YnNjcmlwxB%2FnAMHmALnMGf8Av%2FgAv%2BgHqyBzYW1wbOsEcmFjxW90aGF05gNv6QdudG8gZGlmZmXkATXvA%2FLFKe4BY0HFSOUCoukAnewEfcgN5gCq8wCSSEVBROoHl8R%2BY2hlY2vqAKpleGlzdGVu5gkdIMYeRckU7wNkzR3uCPA%3D&e=%40azure-tools%2Ftypespec-autorest&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fresource-manager%22%5D%7D%7D)

# Additional OKResponse

## question 
Like [this](https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZ8wfYXp1cmUtdG9vbHMvyCstxhVjb3Jl3yvIK3Jlc291cmNlLW1hbmFnZXIiOwoKdXNpbmcgSHR0cDvHDFJlc3TIDFbpAI7IEkHESi5Db3JlzhJSx1xNxls7CgovKiogQ29udG9zb8RUxR4gUHJvdmlkZXIg5gCDbWVudCBBUEkuICovCkBhcm3IIE5hbWVzcGFjZQpAc2VydmljZSgjeyB0aXRsZTogIsdXyC1IdWJDbGllbnQiIH0pCkDnAUNlZCjnAL9zKQpuyFAgTWljcm9zb2Z0LtJG7wC2QVBJIMdNc%2BQAoWVudW3oARNzIHsKICDELjIwMjEtMTAtMDEtcHJldmlld8g1xDQgIEB1c2VEZXBlbmRlbmN5KPUBLy7IVi52MV8wX1DGSF8xKcRAYXJtQ29tbW9uVOQBz8cq10jLKctUNcRIYPIAqWAsCn3mAPNB6AD16wEOIOgCMOQAyG1vZGVsIEVtcGxveWVlIGlzIFRyYWNrZWToAII8yBxQcm9wZXJ0aWVzPuUBJC4u6QCm5AHWUGFyYW1ldGVyyTE%2BO%2BgAhslfcMlE0nzKYOkBg0FnZSBvZiBlyD%2FlAXhhZ2U%2FOiBpbnQzMjsKxylDaXR50ipjaXR5Pzogc3Ry5QLpxyxQcm9maWzTWUBlbmNvZGUoImJhc2U2NHVybCLkAWBwxjA%2FOiBieXRlc8lIVGhlIHN0YXR1c8RLdGhlIGxhc3Qg5ADFYXRpb27lAvwgIEB2aXNpYmlsaXR5KExpZmVjeWNs5AHgYWTHXcQg5QOLU3RhdMRn5QGrzBTpAUjEc8wy5QCA5QDKYekB1cV3QGxyb8Q7dXMKdW7kArLRVOUBZOYBGizsANLIRyBjcmXEJ3JlcXVlc3QgaGFzIGJlZW4gYWNjZXB0ZWTEZyAgQccOOiAiyAsi1lBpxEDkALTpAMHIROwAnDogIswP2kx1cGRhdMRPxUNVxw46ICLIC8o76QSl6QDE5gDcZOcBolN1Y2NlZWTlAMXJDNM%2FxTbkAU1mYWlsyT5GxQ06ICLGCdw4d2FzIGNhbmNlyj5Dxw%2FkBP3HC%2F8BQCBkZWxl6QGARMQN5gD5yAsi6QP96QN3bW926gHK6QN5TW92ZVLHFegDcsRzbW92xGhmcm9tIGxvY%2BYAvMVuxBPxA1DLM3RvzzF0b8ov9wCVc3BvbnPrBIvmAJbHFuwAl%2B4DbMU%2BxWTGfOYC7s1uaW50ZXJm5AYcT%2BgDlHMgZXh0ZW5kc%2FYG3i7LKXt9CuUGusgjyhvLWegAzeQEs%2BgEtCBzYW1wbOsBumFjxDcgdGhhdOYAt%2BkEd3RvIGRpZmZlcuQHL%2B4BOsUpaXMgQeoAgkHFSEFzeW5j6QVkLOwBxiwgT2voATvlBX0%3D&e=%40azure-tools%2Ftypespec-client-generator-core&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fresource-manager%22%5D%7D%7D). My expected response is
```
"200": {
 "description": "ignore"
},
"202": {
 "description": "ignore"
},
"default": {
  "description": "ignore",
    "schema": {
    "$ref": "../../../../../common-types/resource-management/v5/types.json#/definitions/ErrorResponse"
  }
}
```
And it is lro. Therefore I wrote this TypeSpec
```
move is ArmResourceActionAsync<Employee, MoveRequest, OkResponse>;
```
However, this will emit a useless model `TypeSpec.Http.OkResponse` which causes problems in downstream. Can we remove this model? (omit-unreachable-types: true does not work)

## answer
If the `OkResponse` model is being emitted but not actually referenced, that does sound like a bug — we shouldn't be generating unused models. Especially in cases like this where you're doing a long-running POST action and the 200 response is effectively empty, it’s more accurate to model the operation with just a `202` and an error response.

For your case, you can avoid using `OkResponse` entirely by switching to `ArmResourceActionNoResponseContentAsync` or passing `never` as the third argument. That will prevent the generation of the unnecessary `OkResponse` model.

Also, keep in mind that for LROs, the POST operation typically doesn't return 200 anyway. That response is just a placeholder in the old pattern to signal "void". If you're converting from existing Swagger, and it doesn’t actually return 200, you should be able to safely drop that response from the Swagger and avoid the breaking change.

Alternatively, if you do need to customize the final response for the LRO, `getLroResponse` can be used to override the response returned at the end of the operation — no need to rely on `OkResponse` at all.

 
# TSP Install fails with below error

## question 
I am trying to install TSP and its dependencies and it fails with below error. I have latest node.js Can you please help?

Error: spawn EINVAL
    at ChildProcess.spawn (node:internal/child_process:420:11)
    at spawn (node:child_process:753:9)
    at installTypeSpecDependencies (file:///C:/Users/anponnet/AppData/Roaming/npm/node_modules/@typespec/compiler/dist/src/core/install.js:4:19)
    at file:///C:/Users/anponnet/AppData/Roaming/npm/node_modules/@typespec/compiler/dist/src/core/cli/cli.js:152:95
    at Object.handler (file:///C:/Users/anponnet/AppData/Roaming/npm/node_modules/@typespec/compiler/dist/src/core/cli/utils.js:16:16)
    at file:///C:/Users/anponnet/AppData/Roaming/npm/node_modules/@typespec/compiler/node_modules/yargs/build/lib/command.js:206:54
    at maybeAsyncResult (file:///C:/Users/anponnet/AppData/Roaming/npm/node_modules/@typespec/compiler/node_modules/yargs/build/lib/utils/maybe-async-result.js:9:15)
    at CommandInstance.handleValidationAndGetResult (file:///C:/Users/anponnet/AppData/Roaming/npm/node_modules/@typespec/compiler/node_modules/yargs/build/lib/command.js:205:25)
    at CommandInstance.applyMiddlewareAndGetResult (file:///C:/Users/anponnet/AppData/Roaming/npm/node_modules/@typespec/compiler/node_modules/yargs/build/lib/command.js:245:20)
    at CommandInstance.runCommand (file:///C:/Users/anponnet/AppData/Roaming/npm/node_modules/@typespec/compiler/node_modules/yargs/build/lib/command.js:128:20) {
  errno: -4071,
  code: 'EINVAL',
  syscall: 'spawn'
}

## answer
1. which version of the compiler did you install globally, this looks liek quite an old one?
2. where are you trying to use typespec, if its the azure spec repo please follow the docs there https://github.com/Azure/azure-rest-api-specs/blob/7fc6689d84858b1c71b786526b04c014c4589968/documentation/typespec-rest-api-dev-process.md

# Augmented decorators on resource in the multi-path scenario

## question 
This is a resource [model](https://github.com/Azure/azure-rest-api-specs/blob/3a8d2effa54913b5f5365e9a4610810825366409/specification/notificationhubs/Notificationhubs.Management/SharedAccessAuthorizationRuleResource.tsp#L18) in the multi-path scenario. It wants to have [minLength and maxLength](https://github.com/Azure/azure-rest-api-specs/blob/3a8d2effa54913b5f5365e9a4610810825366409/specification/notificationhubs/Notificationhubs.Management/SharedAccessAuthorizationRuleResource.tsp#L179-L180). Its [parent](https://github.com/Azure/azure-rest-api-specs/blob/3a8d2effa54913b5f5365e9a4610810825366409/specification/notificationhubs/Notificationhubs.Management/NotificationHubResource.tsp#L18) also has [augmented decorator](https://github.com/Azure/azure-rest-api-specs/blob/3a8d2effa54913b5f5365e9a4610810825366409/specification/notificationhubs/Notificationhubs.Management/NotificationHubResource.tsp#L116-L117) on it. 
 
Actual: No minLength and maxLength [at generated parameter](https://github.com/Azure/azure-rest-api-specs/blob/3a8d2effa54913b5f5365e9a4610810825366409/specification/notificationhubs/resource-manager/Microsoft.NotificationHubs/preview/2023-10-01-preview/notificationhubs.json#L1286-L1308). 
Expected: [This](https://github.com/Azure/azure-rest-api-specs/blob/5351ac8e1e6fdf48933bae2cd879434b93b36ac0/specification/notificationhubs/resource-manager/Microsoft.NotificationHubs/preview/2023-10-01-preview/notificationhubs.json#L417-L425) is the original swagger. There is limitations on minLength and maxLength.

## answer
Yes, because the actual path parameters do not come from the resource, you would need to decorate the parameters in the LegacyOperations instantiation.

When you construct the LegacyOperations interface, you pass in the parameters - those passed-in parameters would need to be decorated.
 
I wonder if we shouldn't have a legacy resource template that omits the name parameter, just to avoid confusion, it is literally unused in this context.

If you need to decorate the name parameter, you will need to define them directly, or name the resulting model, so they can be decoratred.  You can decorate a model statement, but not a model expression.
 
# First experience of TSP ApiVersion introduction - passed all CI checks, what's next?

## question 
Hi TypeSpec friends! 
 
We are bringing 2nd "private-preview" API version to exercise TSP-based wirings and learn the ecosystem:
https://github.com/Azure/azure-rest-api-specs-pr/pull/22321
 
First round was back in the swagger days.
Now that we are bringing TSP, it comes with a lot of learning.
We made lots of adjustments to satisfy the checks, that were not really changing the protocol, and we had to apply several suppressions otherwise since development of this version has completed - and it is heading to PowerShell CLI partners.
 
It is clear that specification work of the next API version need to be exercised through Azure REST repository tooling to flip the process around, and be able to make protocol affecting changes.
 
With that said - with all checks satisfied - except for "Automated merging requirements met" - what is the next step to bring this PR into the review loop?
 
Updated based on the review comments:

1) for comment [about metadata](https://github.com/Azure/azure-rest-api-specs-pr/pull/22321#discussion_r2072077880)

we have:
```
  @doc("The metadata")
  metadata?: Record<string>;
```
suggestion was: Consider using array of KVP
 
my follow up suggestion: our desired over-the-wire representation is { "mykey": "myvalue" }
can we achieve that via below, will that be supported:
```
model MetadataModel {  [key: string]: string;}
```
2) regarding a [question for the purpose of the PR](https://github.com/Azure/azure-rest-api-specs-pr/pull/22321#issuecomment-2847987559) - I provided brief [answer](https://github.com/Azure/azure-rest-api-specs-pr/pull/22321#issuecomment-2848108430), can you please share the "control plane template" form for me to fill?
 
3) for the initatorId property [question](https://github.com/Azure/azure-rest-api-specs-pr/pull/22321#discussion_r2072077812) - I answered it, not sure if I should put all of my answer in the @doc, since some of that doesn't have to be public facing. Would a regular comment be of help for reviewers, something that is otherwise invisible to the swagger?
 
4) there was a [recommendation](https://github.com/Azure/azure-rest-api-specs-pr/pull/22321#discussion_r2072053387) on how operation ids should look like - and my question was how do we control operation ids, since I am not seeing TSP code of ours being responsible for operation id strings that end up in the swagger?

## answer
Some thoughts:
1. That issue isn't how to get Typespec to construct that over-the-wire pattern. The issue is that the dictionary pattern is an ARM anti-pattern. It defeats important ARM features that customers expect to be able to use (ARG, and Azure Policy). There are other issues with this pattern: (how are the supported keys documented, how are the supported keys versioned, how do clients determine what keys are required vs. optional, etc.).
2. You can create a new dummy PR in github to get a template file and add it to your existing PR. If you use the link to create the PR, Github automatically adds the template file.
3. The type of that property was string. The suggestion is to use the name and @doc to help clients understand what that string represents and how to correctly populate it. It's up to you how much to share about internals.
4. This appears to be the only TypeSpec Discussion question here. I don't know the answer.
Follow-up on items 1-3 above should be in PR comments rather than here. Current on-call reviewer will follow up.

# Namespace when not specified?

## question 
[azure-rest-api-specs/specification/ai/Face/tspconfig.yaml at main · Azure/azure-rest-api-specs](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/ai/Face/tspconfig.yaml)
I'm working on adding Tier 1 language namespace names to TypeSpec APIView. I can get this from the compiler options when namespace is specified in the tspconfig.yaml. However, what should the behavior be when, as in the above config, the namespace isn't specified (for typespec-ts)?  What heuristic is applied to determine the namespace? Is it language specific?

## answer
When the `namespace` isn't explicitly specified in `tspconfig.yaml`, the behavior falls back to what's defined in the TypeSpec file itself—i.e., whatever `namespace` is declared there. If no namespace is declared in the TypeSpec, then the emitter determines the default, which may vary by language.

In the management plane, it's fairly standardized: we derive the namespace based on the resource provider name, stripping prefixes like `Azure` or `Microsoft`, flattening separators, and applying language-specific naming conventions. For example:

* .NET: `Azure.ResourceManager.[ProviderName]`
* Python: `azure-mgmt-[providername]`
* Java: `com.azure.resourcemanager.[providername]`
* JS: `@azure/arm-[providername]`

For **data plane**, it's similar but instead of "ResourceManager", you use the service group (like `AI`, `Data`, etc.). By default, it uses the namespace from the TypeSpec unless you override it via:

```yaml
namespace: Azure.LoadTesting
```

in the `tspconfig.yaml`. That flag overrides the namespace across all language emitters.

There was some confusion with how this gets surfaced through TCGC. Although `clients[0].namespace` is supposed to reflect the effective namespace, it wasn't showing the expected value (`azure.ai.vision.face`). Isabella helped identify the issue and confirm that it was due to a workaround or emitter configuration not being applied as expected.
 
# Resource Action LRO response modelling

## question 
I want to define an Asyn resource action. Calling this action doesn't produce a  body in the immediate response, but the  long running operation will have a body when it finally reaches a `Succeeded` state. What's the correct way to model this in typespec? This is management plane.

## answer
The way this is currently modeled in swagger (and required by lintdiff rules) is that you have a 200 response that represents the eventual operation return value when the operation is resolved, like the 'move' operation [in this example](https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZ8wfYXp1cmUtdG9vbHMvyCstxhVjb3Jl3yvIK3Jlc291cmNlLW1hbmFnZXIiOwoKdXNpbmcgSHR0cDvHDFJlc3TIDFbpAI7IEkHESi5Db3JlzhJSx1xNxls7CgovKiogQ29udG9zb8RUxR4gUHJvdmlkZXIg5gCDbWVudCBBUEkuICovCkBhcm3IIE5hbWVzcGFjZQpAc2VydmljZSgjeyB0aXRsZTogIsdXyC1IdWJDbGllbnQiIH0pCkDnAUNlZCjnAL9zKQpuyFAgTWljcm9zb2Z0LtJG7wC2QVBJIMdNc%2BQAoWVudW3oARNzIHsKICDELjIwMjEtMTAtMDEtcHJldmlld8g1xDQgIEB1c2VEZXBlbmRlbmN5KPUBLy7IVi52MV8wX1DGSF8xKcRAYXJtQ29tbW9uVOQBz8cq10jLKctUNcRIYPIAqWAsCn3mAPNB6AD16wEOIOgCMOQAyG1vZGVsIEVtcGxveWVlIGlzIFRyYWNrZWToAII8yBxQcm9wZXJ0aWVzPuUBJC4u6QCm5AHWUGFyYW1ldGVyyTE%2BO%2BgAhslfcMlE0nzKYOkBg0FnZSBvZiBlyD%2FlAXhhZ2U%2FOiBpbnQzMjsKxylDaXR50ipjaXR5Pzogc3Ry5QLpxyxQcm9maWzTWUBlbmNvZGUoImJhc2U2NHVybCLkAWBwxjA%2FOiBieXRlc8lIVGhlIHN0YXR1c8RLdGhlIGxhc3Qg5ADFYXRpb27lAvwgIEB2aXNpYmlsaXR5KExpZmVjeWNs5AHgYWTHXcQg5QOLU3RhdMRn5QGrzBTpAUjEc8wy5QCA5QDKYekB1cV3QGxyb8Q7dXMKdW7kArLRVOUBZOYBGizsANLIRyBjcmXEJ3JlcXVlc3QgaGFzIGJlZW4gYWNjZXB0ZWTEZyAgQccOOiAiyAsi1lBpxEDkALTpAMHIROwAnDogIswP2kx1cGRhdMRPxUNVxw46ICLIC8o76QSl6QDE5gDcZOcBolN1Y2NlZWTlAMXJDNM%2FxTbkAU1mYWlsyT5GxQ06ICLGCdw4d2FzIGNhbmNlyj5Dxw%2FkBP3HC%2F8BQCBkZWxl6QGARMQN5gD5yAsi6QP96QN3bW926gHK6QN5TW92ZVLHFegDcsRzbW92xGhmcm9tIGxvY%2BYAvMVuxBPxA1DLM3RvzzF0b8ov9wCVc3BvbnPrBIvmAJbHFuwAl%2B4DbMU%2BxWTGfOYC7s1uaW50ZXJm5AYcT%2BgDlHMgZXh0ZW5kc%2FYG3i7LKXt9CuUGusgjyhvLWegAzeYEs2dldOQBp0HKNeQD5%2BwFECDnAntPcuUCp%2BUB1ssvQ8cdUmVwbGFjZUFzeW5jzj%2FlAvfIN0N1c3RvbVBhdGNoU8QqCiAgIOkAkyzFDvYA50ZvdW7kAybkBkXIHOYAkU3kAYTGSdBLyhDqBarFGT4KICDlAJ3mAprvANTlApdlV2l0aG91dE9r8wDUbGlzdEJ5yDBHcm91cM9ETMUiUGFyZW501DxTdWJzY3JpcOUCecY7xjPMGcw56AZgIHNhbXBs6wNmYWPFRHRoYXTmAmPpBiN0byBkaWZmZeQAhO8C5sUp7gCyQcVI7gDtLOwDcsgN5gLp8wCTSEVBROoGTcR%2FY2hlY2vqAKtleGlzdGVu5gfHIMYeRckU7wJZzR3uB6I%3D&e=%40azure-tools%2Ftypespec-autorest&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fresource-manager%22%5D%7D%7D).
 
We would like to move to a representation where the 200 response is not in the swagger and the return value is represented in the long-running-operation-options extension, but not all emitters for required languages support this yet.

# How to customize "original-uri" in arm template?

## question 
We have a customer who sets ["final-state-via": "original-uri"](https://github.com/Azure/azure-rest-api-specs/blob/11059b2f00c7572b276dc9862c0b41db8702cc78/specification/dashboard/resource-manager/Microsoft.Dashboard/stable/2024-10-01/grafana.json#L1007). We have ArmAsyncOperationHeader, ArmLroLocationHeader. Seems we don't have a header for original-uri?

## answer
`original-uri` is only a valid setting for PUT operations, it means that the original URI in the PUT request is used to retrieve the resource, which has a status field that determines its state. Generally, this is the default for LRO PUT operations, but if there are multiple valid pathways to resolve the lro, you can use `@useFinalStateVia` to choose which one should be favored.
[like this](https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZ8wfYXp1cmUtdG9vbHMvyCstxhVjb3Jl3yvIK3Jlc291cmNlLW1hbmFnZXIiOwoKdXNpbmcgSHR0cDvHDFJlc3TIDFbpAI7IEkHESi5Db3JlzhJSx1xNxls7CgovKiogQ29udG9zb8RUxR4gUHJvdmlkZXIg5gCDbWVudCBBUEkuICovCkBhcm3IIE5hbWVzcGFjZQpAc2VydmljZSgjeyB0aXRsZTogIsdXyC1IdWJDbGllbnQiIH0pCkDnAUNlZCjnAL9zKQpuyFAgTWljcm9zb2Z0LtJG7wC2QVBJIMdNc%2BQAoWVudW3oARNzIHsKICDELjIwMjEtMTAtMDEtcHJldmlld8g1xDQgIEB1c2VEZXBlbmRlbmN5KPUBLy7IVi52MV8wX1DGSF8xKcRAYXJtQ29tbW9uVOQBz8cq10jLKctUNcRIYPIAqWAsCn3mAPNB6AD16wEOIOgCMOQAyG1vZGVsIEVtcGxveWVlIGlzIFRyYWNrZWToAII8yBxQcm9wZXJ0aWVzPuUBJC4u6QCm5AHWUGFyYW1ldGVyyTE%2BO%2BgAhslfcMlE0nzKYOkBg0FnZSBvZiBlyD%2FlAXhhZ2U%2FOiBpbnQzMjsKxylDaXR50ipjaXR5Pzogc3Ry5QLpxyxQcm9maWzTWUBlbmNvZGUoImJhc2U2NHVybCLkAWBwxjA%2FOiBieXRlc8lIVGhlIHN0YXR1c8RLdGhlIGxhc3Qg5ADFYXRpb27lAvwgIEB2aXNpYmlsaXR5KExpZmVjeWNs5AHgYWTHXcQg5QOLU3RhdMRn5QGrzBTpAUjEc8wy5QCA5QDKYekB1cV3QGxyb8Q7dXMKdW7kArLRVOUBZOYBGizsANLIRyBjcmXEJ3JlcXVlc3QgaGFzIGJlZW4gYWNjZXB0ZWTEZyAgQccOOiAiyAsi1lBpxEDkALTpAMHIROwAnDogIswP2kx1cGRhdMRPxUNVxw46ICLIC8o76QSl6QDE5gDcZOcBolN1Y2NlZWTlAMXJDNM%2FxTbkAU1mYWlsyT5GxQ06ICLGCdw4d2FzIGNhbmNlyj5Dxw%2FkBP3HC%2F8BQCBkZWxl6QGARMQN5gD5yAsi6QP96QN3bW926gHK6QN5TW92ZVLHFegDcsRzbW92xGhmcm9tIGxvY%2BYAvMVuxBPxA1DLM3RvzzF0b8ov9wCVc3BvbnPrBIvmAJbHFuwAl%2B4DbMU%2BxWTGfOYC7s1uaW50ZXJm5AYcT%2BgDlHMgZXh0ZW5kc%2FYG3i7LKXt9CuUGusgjyhvLWegAzeYEs2dldOQBp0HKNeQD5%2BwFEOYGNkZpbmFs5QOGVmlhKCJvcmlnxBItdXJp5QRy5wN7zktDxRVPclJlcGxhY2VBc3luY85b5QMTyDdDdXN0b21QYXRjaFPEKgogICDpAK8sxQ72AQNGb3Vu5ANC5AZhyBzlA1RlTeQBoMZJ0EvKEOoFxsUZPgogIOUAneYCtu8A1OUCs2VXaXRob3V0T2vzANRsaXN0QnnIMEdyb3Vwz0RMxSJQYXJlbnTUPFN1YnNjcmlw5QKVxjvGM8wZzDnoBnwgc2FtcGzrA4JhY8VEdGhhdOYCf%2BkGP3RvIGRpZmZl5ACE7wMCxSnuALJBxUjlAZbIdyzsA43IDeYDBPMAkkhFQUTqBmjEfmNoZWNr6gCqZXhpc3RlbuYH4iDGHkXJFO8CWM0d7ge9&e=%40azure-tools%2Ftypespec-autorest&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fresource-manager%22%5D%7D%2C%22options%22%3A%7B%22%40azure-tools%2Ftypespec-autorest%22%3A%7B%22emit-lro-options%22%3A%22final-state-only%22%7D%7D%7D).  When you have multiple valid pathways to resolve the lro, you might have to use `@useFinalStateVia` to prefer one over the other.  Since this API also has an Azure-AsyncOperation header, and for ARM, looking at it the default for PUT is Azure-AsyncOperation, when it is present.

# Avocado Failing on PR

## question 
Hi TypeSpec Discussion
 
I have a PR here to add a new set of Azure AI APIs: https://github.com/Azure/azure-rest-api-specs/pull/33130
 
I'm a bit confused on why the avocado step is failing? As this is a brand new API, we started from scratch with tsp itself, and these swaggers are the output of `npx tsv` .... Do I really need to include a README.md in the swagger directories, or is this just failing incorrectly? I don't see README.md files in other service directories (i.e. keyvault) for example.

## answer
you need a readme.md somewhere to generate SDKs.  location and factoring of the readme.md(s) can vary by spec.

# Need help in Adding final-state-schema for a single post action

## question 
Hi TypeSpec Discussion,
PR: https://github.com/Azure/azure-rest-api-specs-pr/pull/22427
 
We would like to add final-state-schema for a single post action LRO operation for now. Is it possible to do it for a single post action or not ?
 
when we add emit-lro-options: "all" in tspconfig.yml. It is reflected in all the long-running options..

## answer
So, what you need to do is the following:  Change the response type to match the final result type you want  Here is a playground showing that the response parameter in ArmResourceActionAsync shows up both in the 200 response and in the final-state-schema, which means that typespec-based emitters  will get the right final response value and so will swagger-based emitters.  
 
To be clear the final-state-schema in this case is just for debugging purposes is it not necessary in the actual swagger.  I added a comment to the PR showing the change you should make here.
 
# Typespec -> Autorest generation : multiple specs per service

## question 
Currently while we are able to organize and manage multiple typespec files per service easily, the final generated swagger is a single file.
I was asked during my API review to check if there is feasibility to produce multiple specs per service for organizational purposes considering the generated file is huge. 
I see prior posts on this indicating this is not supported, but looking for any latest update/guidance here.
Secondly, if the above is in fact supported, any idea if the SDK generation part can handle multiple specs per service?

## answer
No, since the swagger is now just an emitted artifact, there is no real reason to organize it.  There is no mechanism for splitting a TypeSpec spec into multiple OpenAPI files.

# Proper Service Versioning

## question 
Hi TypeSpec Discussion. I'm looking to understand how to properly add a new service version to my team's typespec. I've been looking at this doc here as a baseline, and I think I generally understand everything there. But I've got a couple of questions for my specific case.
1. What does the `added` decorator actually do? Just tell the swagger which version should/shouldn't contain a property? I'm assuming the question of whether it has an impact on any of the SDKs is a question for the individual generator teams?
2. My team's spec has a [client.tsp file](https://github.com/Azure/azure-rest-api-specs/blob/dargilco/ai-model-inference/specification/ai/ModelClient/client.tsp#L8) with a `customization` namespace, which has this decorator: `@useDependency(AI.Model.Versions.v2024_05_01_Preview)`. I get the general gist that this ties things to a specific version, but what does that mean from a practical standpoint? I maybe can understand client customizations being specific to individual versions, but what about modifications that work across versions? I don't seem to be able to provide a list or anything to that decorator. I've tried removing it, and I get an error saying that the `customization` namespace is referencing a versioned namespace and should add the decorator. I've also tried just changing the namespace to match, but then I get an error from my client interfaces saying that I have duplicate operations. So I'm trying to understand how to correctly handle this.

Any pointers would be appreciated. Thanks in advance!

## answer
On the first question, TypeSpec is allowing you to version based on differences,  starting with the base api-version, whenever you make changes to the api, you just need to tag those changes with the 'versioning' decorator to ensure that typespec can reconstruct the api at each version that is still active.
In the case of `@added` this is used whenever adding a new type to the spec - a new model, a new model property, a new interface, a new parameter, a new operation - you simply decorate the element with this decorator and pass in the version that this change occurred in.  `@removed` works similarly for removing types (which is always a breaking change).  decorators like `@renamedFrom` allow you to rename types, and take the version the change occurred at and the old name of the type (the type name is changed inline). `@typeChangedFrom` works similarly - describing the state of the type before the change occurred.
There are some limitations around versioning (for example, versioning decorators is impossible, so the decorated types themselves generally need to be versioned.
 
As far as the client.tsp goes, versioning is tightly tied to a namespace (but includes all the child namespaces).  If your client.tsp is a child namespace of the versioned namespace, then no explicit version coupling is required.  If not, then you will need to replicate the versions enum from the service namespace in client.tsp,  and explicitly tie each version to the corresponding version.  I have shown an example of what I mean [in this playground](https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZ8wfYXp1cmUtdG9vbHMvyCstxhVjb3JlIjsKCnVzaW5nIEh0dHA7xwxSZXN0yAxWyVfIEkHEPi5Db3Jl0hIuVHJhaXRzOwoKQOcAkGVkKENvbnRvc28uV2lkZ2V0TWFuYWdlci7HWXMpCm5hbWVzcGFjZSDVKiB7CiAgLyoqIFRoySIgxiIgyCNzZXJ2aWNlIMd1LiAqLwogIGVudW3oAMFzxUfGSccTIDIwMjItMDgtMzDGMCAgQHVzZURlcGVuZGVuY3ko6wDZyEcudjFfMF9QcmV2aWV3XzIpxTdgykpgLArUaDUtMDHfaN9oyGjHSmAsCiAgfcRsLy8gTW9kZWxzIC%2FTAcUi5wE9Y29sb3Igb2YgYSB35QFA5wEodW7kAKbmAVNDxSTmASxzdHJpbmfrANFCbGFja8cpIMYqxUEgxho6ICLFCCLLM1doaXRl1TPFGjogIsUIzDNSZWTVMVJlZDogIlJlZMwtR3JlZegAvM4vxRo6ICLFCMwzQmx19gCSxBnlAMR15ACQ6AFfKiogQe4BMEByZXNvdXJjZSgixhhzIuQBoG3kAYbIWeoCbuQBesYnIOQC6sdEICBAa2V5yEFOYW1lxUQgIEB2aXNpYmlsaXR5KExpZmVjeWNsZS5SZWFkxiDERDrnAZs76gDVy2blAefJZ8UOOuwB3s82SUTkAhN0yUAncyBtYW51ZmFjdHVyZcpJzBVJZO8Agy4uLkV0YWdQcm9wZXJ0eTvsAT7EZXJlcGFpciBzdGF0ZfMCgkBscm9TdGF0dXPvAo9SxTjEG2X4ApXGKcdicyBzdWNjZWVkZWTHWyAgU8gSOiAiyQztAqTOQmZhaWzLP0bFDzogIsYJ2zl3ZXJlIGNhbmNlzEBDxxE6ICLIC9NEd2FzIHNlbnQgdG%2FlAYz1AYNTZW50VG9NyxvkANPRFfACvXN1Ym1pdHRlZOgBh3JlcXVlc3QgZm9y8AGK7ALF5gF9Usct7gLS6QHK6gI5z17HUSDIEOUBxegCiesB2O8Cj2TEWGFuZCB0aW1lIHdoZW7FY8dcaXMgc2NoZWR1bGVk5AErb2NjdeoBIMkbRGF0ZVRp5AMpdXRjyA3faclp5gDv5AGRY3JlYXTrAcvHEN9e317JXnVwxCXKXscQ317fXsleY29tcGxlzGDJEtdi7wOzcGFyYW1ldGXkAxLrAijlAfp1c8ls9wI25gPOUMRE7Ad79ARxIGJl5AhG5gHi6wCwQHBhdGjFCsYo7AR18wWdJ3PkALPzBaTEFuYFokBwYXJlbnRSyBvmALHwBbxQYXLwAu7kBXXoALXLZe4FxcQ4%2FwXJ%2FwXJyG9JROQC3HVzZeUBenJlb3JkZcQn8gCAxA3xBY3yAWnEJ%2F8F2P8F2PcF2GRldGFpbHPEaGHoAKnvBF%2FqAVXzBGPEF1LGN%2FIEaElkZW50aWZpZXMgd2hvIHNpZ25lZCBvZuYA1M9ux10gxydPZmZCefICMC8gQW4gZXhh5AMK5gC15ArCbGV0b24g6AIy5wDfUHJvdmlkZXMgYW5hbHl0aWNzIGFib3V0xX3kAZzkA4BtYWludGVu5AXYxk%2FmANfyAoXJRf8CiewBHUHJee4CjmnoARhy5QFj5ACQyV8gb2JqZWN0LiDEKnJl5AUxb25seSBvbuYCxmQgJ2N1csR9J%2B8CxclDSWT%2FAsLoAsJp5AbUx07kDErtAzJ1bWJl5QptdXNl5QIg6gPxyXl1c2VDb3VudDogaW50NjTZQ%2BQE5nPLQeUE4fEEMMYRzlHtBCjsAurEV8ZNc%2FIBrcwp7Anc7Adl7wGUzDMnc%2BQIxHF15AOC7wFlzCf%2FAWjuAWj1A%2BHPde0KX%2FsEX883ZnVsbCBhZGRyZXPoAQwgyBD%2FBA%2FrA1hPcGVyYXTlDWr%2FDIjpDa505A4tZXPsBBZTxhvmDkfnAWMuLi5TdXDkDtFzUmVwZWF0YWJsZecELnPkAIPNI0NvbmRp5ACIYWzaJGxp5QN%2BxR9JZDzpDXXsDWE%2B6QDeYWxpYXPsAOE97A7z6APFyiA87QDGPjsKfQoK5A3yQXV0aCgKICBBcGlLZXnEDjzGC0xvY8U6LmhlYWRlciwgImFwaS1rZXkiPiB8IE%2FEKjLFL1vlAMrmARcgIOQP0TrHH0Zsb3dUeXBlLmltcGxpY2l05AoExAFhdXRob3JpesVhVXJsOiAi5BBdczovL2xvZ2luLmPnD3Vjb20vY29tbW9uL2%2FENTIvdjIuMC%2FIQOYNE8QBc2NvcGVzOiBbyUnnA9fMSi5kZWZhdWx0Il3GN30KICBdPgopCkDnAf4oI3sgdGl0bOQNcvYP1iIgfccvZXLkATcie2VuZHBvaW50fcdy5QCT0D5BUElzxRnqA6EK5wH56g5Z5wGicyDIU3MgKHByb3RvY29s5QXZaG9zdOQDQyzlBWPnBjI6CukA8mVzdHVzLmFwaS7yAP0pLgroA0fIX%2BgDSOYLminsER%2F7EQnrAnPkAM3mEW71ESXlA6DkBotyZsRAxhzGNu4DniDpDWfnD0DEJ%2BcJ0%2BsGwSDkA%2BHFMeoEwXNoYXJlZFJvdXTkByMgIGdldMYtyV3GXOQGdMkTcy5H5AoT7wMjxik8xj%2FlA2n%2BAI5kZeQKuf8Ale4AlUTFM%2F8Am%2FoAm8VNIOcAgOgC%2Bm5ldmVyxQw%2B6AVTL8cjy1fGGSoqIEPlDC9zIG9y5wvcc%2BoA0mFzeW5jaHJvbm91c2x56grzb2xsaW5nyU3nCBpzLvgBbeYGIuYMmU9yVcVnx2PuAOxMb25nUnVu5BPK6AD05gCgyDbqAY7sAY%2FqALLnAKPpAInPYMhV5Aa300vmAY%2F4AQDrC%2FT%2FAQH3AdnmAQfmAif%2FAP%2FpAP%2FGSfMArExpc3TqD9zGLecGpiAgbGlz5wCJ8AJOyFjEP%2FYCQMQaUXVlcnnlDRjlDVzlBiQ8U3RhbmRhcmTTISAmxz3kD%2F9sZWPPID73Ao%2FpCjz1Ac3JHusB1soU%2FwHZyyvJeP4C7tFvxiHfcvYCoNJ86QD05g569AOK5AD85Ao%2F8gRb7RDk6QKHI3N1cHDkCQD%2FF0NvcmUvdXNlLXPnAcAt6QSicyIgIlRoaeUA22EgY3VzdG9t6gTA6ACJ6AYfLiLFd0By5ATIKCLnBt9zL3vGCUlkfS%2FnEmUve8lFSWR95wpB7w92xHRGb3VuZOoE5%2F0E38w85g%2By6AKx8wEk7gJwKiogU%2BcRdeoQIuYOtcQ06gpQ%2FwPZ%2BQTa6BHT5gCE8AIq8wPWQWPEG%2FYDgPMAz9sbICYgxwpJZFJlc3BvbnNlSOUJoegBBOcKIPAHeOQO1ewNtPcG1sQn8wbT6QEQxCD%2FBsD%2FB1vFQ%2FQDpO0Ag%2B0S3csY%2FAOVV2l0aOcJROcPKGTkEPvXd%2BwEe8xz7QDr13DrBHfQV%2B8GVMxa7AYK213tBgPQX%2BsGB8VD%2BwYLxCD9Bg%2FWYucQ%2BiBhbGzlEaPmE2rzDyn4Az7FeOoDQvMCMuYDRucRLPQAp%2FQIGG9s5AZpaW9u9ANNxEXuAzbyEcX1CTHxAy7sD0%2F9AzDMKfYDMswi%2FwM0%2BgM0zEX3BtpyZXBsYeQCA%2B8Ale0DRE9yUsYnzSP%2BBuTHMvkAhOYDQ9R07wD%2B12jlA0fYXekDSc1J%2FwmjyWUozDblAo77AY7sCan7ALf6Ca%2F4AMTlA67NUvUDsMwi%2FQOyz2jvFPj%2FHoj%2FHoj%2FHoj%2FHiD6HiDPN%2F4feeUQ7cd4YP8eyP8eyP8AqP8AqP8AqO0RlfgfCOYV2yAiZ2xvYmFsIiBSUEPqBCnnAbjmB8RkcyB3aXRo6ARgaW5mb3JtxSfrFd9vdmVy5AWV5wHQ6RQi5gmfxxbkCfZ0deYUIG9w5AOI5wcS6gR4UnBj6QJg5gVGe33mBRzoEcTGRFPlEB3sFq8gIMcp7RJMCuUFSX0K&e=%40azure-tools%2Ftypespec-autorest&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fdata-plane%22%5D%7D%7D)
Feel free to reach out with any specific questions.
The linked playground is a little more complex than it needs to be, but, depending on what is in your client.tsp, is likely similar.  Note that this also gives you the option of making version-specific client.tsp changes, but simply having the linkage as shown using the `@useDependency` decorators will ensure that the client.tsp is used in both versions.
 
# JSON merge-patch support in TypeSpec

## question 
How exactly does TypeSpec support `application/merge-patch+json` i.e., "JSON merge-patch"? Is there explicit types, or is it really just a matter of service authors adding `| null` to their type defs e.g.,

```
model M {
    @key("id")
    id: string;
    name: string | null;
    dob?: utcDateTime | null;
}
```
This will greatly affect how Rust will support this, give a discussion Larry, Johan, and I were having yesterday.

## answer
Currently, TypeSpec does **not** have built-in or explicit support for `application/merge-patch+json` (JSON Merge Patch). That means there are no dedicated keywords or types in the language to model it directly.

The current recommended approach is to **manually define a separate PATCH model** for each resource. This model should mirror the resource structure, but all properties should be made optional. This expresses the correct merge-patch behavior where only specified fields are updated.

Importantly, we **do not use `| null` in the model to indicate erasable fields**. Just like in Azure and Graph, we treat merge-patch support as a **fundamental protocol decision of the service**, not something that should be reflected in the type system. In other words, the ability to pass `null` is not encoded in the TypeSpec models today.

That said, we recognize that for generation-first languages like Rust, it's essential to know whether a field can explicitly be `null` versus being omitted. To address this, we're currently designing a new **`MergePatch` template**, which will help service authors more easily generate accurate merge-patch OpenAPI schemas. It will also make it possible for emitters to trace a merge-patch model back to its original resource definition.

# How to make an interface internal ?

## question 
I am looking for ways to make an interface internal so that it does not appear in public interface of python SDK. This is the interface which emits `EvaluationResultsOperations` which shows up on client. I would like to generate it but keep it hidden from public interface.
 
I tried following:
1. Mark all operations under it internal
2. Adding @access decorator to interface but that fails.

Is there a way to achieve it ?

## answer
If your goal is to **hide an operations interface from the public Python SDK surface**, here's the key takeaway:

> **We currently do not support hiding an entire operation group directly.**

However, there are **two main approaches** to achieve your intent:

1. **Use `client.tsp` to redefine your client** and control which operations appear on the public client. When you redefine the client structure this way, **default service clients (and their `Operations` classes) should not be emitted**—as long as there’s no leftover generated code from a previous run. Make sure to:

   * Remove all previously generated SDK output before regenerating.
   * Verify your custom client structure is complete.

2. **Use `_patch.py` to customize visibility**, e.g., by renaming `client.evaluation_results` to `client._evaluation_results`, if needed. This is a workaround until more flexible TypeSpec features are available.

> Also, note that the `@access` decorator doesn’t work on interfaces right now—it’s not interpreted that way by the client generator (tcgc).

So, the current best practice is to **redefine the client properly in `client.tsp`**, clean your generated code, and regenerate to reflect the changes accurately.
 
# Sharing models between data plane and control plane

## question 
Has anyone successfully shared models between control plane and data plane? I'm struggling with this seemingly simple task and could use come guidance or an example other than the trivial one for sharing a TSP file withing control plan or within data plane for a single service.
 
Even to share models across separate versioned data plane APIs, I ended up creating a new data plane shared namespace and associated version to get it working. Do I need to create a control plane shared namespace and version? This makes me a bit nervous about conflicting versions of the same dependency between the service namespace, data plane shared namespace and control plane shared namespace.
 
When I try to cross data plane and control plane TypeSpec, I end up with unhelpful errors like this:
```
<unknown location>:1:1 - error @typespec/versioning/using-versioned-library: Namespace '' is referencing types from versioned namespace 'Azure.Core' but didn't specify which versions with @useDependency.
<unknown location>:1:1 - error @typespec/versioning/using-versioned-library: Namespace '' is referencing types from versioned namespace 'Azure.ResourceManager' but didn't specify which versions with @useDependency.
```
Or errors about multiple namespace or about @service not specifying a namespace even though it does.

## answer
Yes, you **can share models** between data plane and control plane, but you need to be careful:

* ✅ **Create a shared namespace** (e.g., `Discovery.Shared`) that’s not tied to either control or data plane.
* ✅ **Version the shared types independently**, not tied to any API version.
* ✅ **Avoid ARM library dependencies in data plane** — model things like resource IDs using aliases or common types.
* ✅ Use `@useDependency` to reference versioned namespaces like `Azure.Core` or `Azure.ResourceManager`.

This way, you can safely reuse types without introducing versioning conflicts or unwanted dependencies.


# Question regarding the unexpected readonly, and customize the enum name

## question 
Hi team,
 
When dealing with the TypeSpec migration, we hit below two issues. Could you help take a look and see if there is any way to fix them? Thanks!
1. Haven't add @visibility(Lifecycle.Read) to the property, but the definition has "readOnly": true on it.
- TSP: https://github.com/Azure/azure-rest-api-specs/blob/45317772ce7c50313eaf55b8d242f4d12ca6fe06/specification/desktopvirtualization/DesktopVirtualization.Management/models.tsp#L3665
Swagger: https://github.com/Azure/azure-rest-api-specs/blob/45317772ce7c50313eaf55b8d242f4d12ca6fe06/specification/desktopvirtualization/resource-manager/Microsoft.DesktopVirtualization/preview/2025-04-01-preview/desktopvirtualization.json#L11380
- TSP: https://github.com/Azure/azure-rest-api-specs/blob/45317772ce7c50313eaf55b8d242f4d12ca6fe06/specification/desktopvirtualization/DesktopVirtualization.Management/models.tsp#L4859
Swagger: https://github.com/Azure/azure-rest-api-specs/blob/45317772ce7c50313eaf55b8d242f4d12ca6fe06/specification/desktopvirtualization/resource-manager/Microsoft.DesktopVirtualization/preview/2025-04-01-preview/desktopvirtualization.json#L10826
2. How to make enum's "x-ms-enum" name to be different than the type name like below?
- https://github.com/Azure/azure-rest-api-specs/blob/cb262725d128f6dfec4622cca03bc9e04e2d0f1f/specification/desktopvirtualization/resource-manager/Microsoft.DesktopVirtualization/preview/2024-11-01-preview/desktopvirtualization.json#L9487C4-L9493C33

## answer
1. This is because of this https://azure.github.io/typespec-azure/docs/troubleshoot/status-read-only-error/#_top
2. No its not possible change the enum name to be what is in `x-ms-enum.name` it is pointless information otherwise
 
# Discrepancy in the original LRO response and Status Monitor Response

## question 
Hi TypeSpec Discussion team,
 
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

## answer
A notice on Guideline on LRO, the "id" of the status monitor should be "id", not "operationId"
https://github.com/microsoft/api-guidelines/blob/vNext/azure/ConsiderationsForServiceDesign.md#long-running-action-operations
To ensure its called {operationId} in the route template but id in the response object, I think you can use this
```
  @key("operationId")
  @visibility(Lifecycle.Read)
  id: string;
```

# Visibility-sealed error while validating locally

## question 
I am getting this error while running `npx tsv` locally:
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

## answer
I believe this occurs because  the `TrackedResource` template already has the @Azure.ResourceManager.Private.armResourceInternal decorator applied, and using the model is, you are including both decorators in that type.  Based on the suppression you also have there on 'composition-over-inheritance', my guess s that this type previouslu used `FileSystemResource extends TrackedResource<FileSystemProperties>`  in this case, the decorator from TrackedResource<T>  would not be applied.
The solution would be to remove the `armResourceInternal`  decorator here, because it is redundant

# Annotate same model with SubscriptionLocationResource and ResourceGroupLocationResource

## question 
1. We have added a new SubscriptionLocationResource named "ValidatedSolutionRecipe", as per the typespec docs to our RP - Microsoft.AzureStackHCI. Here is the typespec for this resource
2. This is a proxy resource and the URL path for this resource looks like "/subscriptions/921d26b3-c14d-4efc-b56e-93a2439e028c/providers/Microsoft.AzureStackHCI/locations/eastus/validatedSolutionRecipes/10.2502.0?api-version=2023-12-01-preview"
As above API is a subscription level API, the clients of the API need to have subscription level RBAC. Due to security requirements, we need to have a similar API, but at resource group scope.
1. From the typespec docs and our prototyping, we see that we can achieve this by havning a ResourceGroupLocationResource.
2. However, same model in typespec can't be annotated with both SubscriptionLocationResource and ResourceGroupLocationResource. When we do so, the generated swagger only has either subscription level paths or resourcegroup level paths, depending on which annotation is first on the model.
3. Thus, to work around this, we had to introduce a new resource type with an undesirable name - "ResourceGroupValidatedSolutionRecipe".
But it is the same resource. Just because of the limitation of not being able to support both [SubscriptionLocationResource](https://azure.github.io/typespec-azure/docs/libraries/azure-resource-manager/reference/data-types/#Azure.ResourceManager.SubscriptionLocationResource) and [ResourceGroupLocationResource](https://azure.github.io/typespec-azure/docs/libraries/azure-resource-manager/reference/data-types/#Azure.ResourceManager.ResourceGroupLocationResource), we have to create a new model with an undesirable name - "ResourceGroupValidatedSolutionRecipe".
 
Please help us and let us know how can we utilize the model with same name (i.e. the same resource type) for both of the above APIs.

## answer
he main issue is that TypeSpec does not support annotating the same model with both SubscriptionLocationResource and ResourceGroupLocationResource. This leads to the creation of two different models (e.g., ValidatedSolutionRecipe and ResourceGroupValidatedSolutionRecipe), even though they represent the same resource. According to ARM requirements, these resources must be registered as two different types because they have different scopes and operations.

Solution:
Shared Model: If the operations for both resources are identical, you can consider using custom operations to share the same model, avoiding the need to create two different resource types.

RPaaS Proxy Resources: By using RPaaS extensions, you can handle this scenario and simplify the API, reducing confusion for customers.

In summary, although the resources may represent the same entity, due to ARM registration requirements and operational differences, two models may be necessary. However, using custom operations or extension resource patterns can help avoid redundant model creation in certain cases.

# Support for @includeInapplicableMetadataInPayload decorator

## question 
After pulling the latest changes, I been getting errors regarding "@includeInapplicableMetadataInPayload(false)" decorator not being supported anymore.
 
Is it not possible to make use of the decorator? If I removed the decorator from the model ts file, it changes the expected model definition for the API path on swagger.

## answer
The issue is related to the deprecation of the @includeInapplicableMetadataInPayload(false) decorator in TypeSpec version 0.67. It has been moved to a private namespace, and you can still use it, but with a warning.
Decorator Usage: You can still use the @includeInapplicableMetadataInPayload decorator, but it will trigger a warning since it's now in a private namespace.

Breaking Change: Removing this decorator causes a breaking change for existing API versions because the Swagger model definition is altered.

Suggestion: The team suggests not relying on the decorator and using existing resource models like TrackedResource<T> instead.

# Type Spec review for Data plane API specs

## question 
Hi TypeSpec Discussion, do we need review from type spec team before we could merge data plane API spec PR on github specs repo?

## answer
All data plane API specs must be reviewed by the API Stewardship board.  Please create a release plan and then you can schedule a review.  [What is a release plan](https://eng.ms/docs/products/azure-developer-experience/plan/release-plan)?

# What is `x-ms-long-running-operation-options` for LRO operation of data-plane when `emit-lro-options: none` in `@azure-tools/typespec-autorest`?

## question 
Many `tspconfig.yaml` files for data-plane have an option `emit-lro-options: none` for emitter `@azure-tools/typespec-autorest`, which means only emit `x-ms-long-running-operation` but does not emit`x-ms-long-running-operation-option` for resource providers, like the following loadtestservice `tspconfig.yaml`: [azure-rest-api-specs/specification/loadtestservice/LoadTestService/tspconfig.yaml at main · Azure/azure-rest-api-specs](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/loadtestservice/LoadTestService/tspconfig.yaml#L24)

So what exactly does this data-plane operation use when polling LRO request then? Can someone explain a little more about this situation of `emit-lro-options: none`? thanks

## answer
This is just an emitter option for emission of OpenAPI from the spec.  It doesn't impact how other emitters view the LRO - the LRO is resolved based on the encoding of the operation.
 
Because it's nice to have a visual indicator that the lro is encoded correctly, it is highly encouraged that spec authors use no emit-lro-options seting, , or use `emit-lro-options: "all"` to check .  But this is not required for check-in, because the lro-options are a microsoft-specific extension with little or no documentary value to customers.

We don't generate data plane clients from OpenAPI if there is a corresponding TypeSpec, they are generated form TypeSpec directly.

# Why does the PR bot add the `WaitForARMFeedback` label when TypeSpec validation pipeline fails?

## question 
Hi, not a typespec question per se, but I am curious why the PR bot now adds the WaitForARMFeedback label even when required checks fail? If I'm remembering correctly, this didn't used to be the case.
 
And now that it does add the label, it seems like the reviewer will typically manually say "Fix X pipeline check." and then switch it to "ARMChangesRequested".
 
Would it be possible for that to happen automatically? I feel bad for wasting the reviewer's time if I don't sit around and wait for the pipeline to fail and then manually remove the label myself. 
 
Or is the expectation that a commit shouldn't be pushed if I think the required checks may fail?

## answer
The WaitForARMFeedback label being added even when required checks fail is intended behavior and has always worked this way, according to the ARM review team.

Ideally, contributors should open a draft PR first and only mark it "ready for review" after all required checks pass. This avoids wasting reviewer time.

The suggestion to automatically change the label to ARMChangesRequested when checks fail is a good idea and is already on the backlog.

The engineering team is currently migrating the labeling system to GitHub Actions, which should make improvements like this easier in the future.

# Is path case sensitive?

## question

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

## answer
static segments in ARM urls are meant to be case-insensitive.  In this case, the swagger is incorrect, since this is clearly meant to be the ARM type name.  You should use the correct type name in both cases.

If the url is /subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/providers/..., is that the same as `/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/...? The "G" in resourceGroups has different cases.
These are case insensitive,  we should favor the camel case here, there is no need to match the exact casing of these in existing swagger.

# Discriminators/polymorphism

## question
I'm looking into introducing polymorphism into one of our APIs. This question provides great insight but is apparently closed as a duplicate of an issue that does not seem exist. Mark Cowlishaw, do you know what the outcome of this no longer existent issue? 
 Note that there is an issue around how we should encourage types using extends in Azure APIs to use discriminators here: https://github.com/Azure/typespec-azure/issues/3510
Like Brian Terlson,  I tend to favor the union approach for operations:
 op create(@body body: Cat | Dog // replace the base class with union of subs)
Is this the recommended Azure approach, union (i.e. @body: SubModel1 | SubModel2 for operation with @discriminator in base model type for @body ?

## answer
Azure recommends using @discriminator to model polymorphism, but there are still some missing pieces before discriminated union types will be allowed in Azure APIs. The polymorphism in the model looks correct, and it is suggested to use PascalCase for union variant names. Additionally, narrowing inherited property types in inheriting classes (except for the discriminator) is not allowed. 

# Folder structure recommendation for typespec.

## question
Hi Team,
 
Can you please confirm what the folder structure needs to be for the .tsp files, specifically for teams that are separating services within the same RP namespace?

## answer
1. Service Folder Naming:

Each service should reside in its own folder under the RP namespace.
Use PascalCase for RP namespace folders (e.g., Language, Vision).
Service folders should be singular and lowercase (e.g., TextAnalytics, ComputerVision).
2. Shared Libraries:

If multiple services share models or utilities, create a Shared folder at the same level as the services.
Example:
-> specification
   -> cognitiveservices
      -> Language.TextAnalytics
      -> Language.QnA
      -> Language.Shared
      -> Vision.ComputerVision
      -> Vision.CustomVision
      -> Vision.Shared
3. Required Files in Each Service Folder:

tspconfig.yaml: Configuration for emitters and SDK generation.
main.tsp: Entry point for the TypeSpec definitions.
Supporting *.tsp files: For models, operations, etc.
examples/: Folder with example JSON files.
4. SDK Generation:

Only services intending to generate SDKs should include emitter configuration in tspconfig.yaml.
Shared libraries should not include tspconfig.yaml.
5. No package.json in Service Folder:

Use the root-level package.json for dependencies like @azure-tools/typespec-autorest.
This structure ensures modularity and clarity when multiple services coexist under a single RP namespace, while also supporting shared components and SDK generation workflows.

Let me know if you’d like help drafting a sample folder layout or tspconfig.yaml for your team.

# Is there way to change property from required to optional?

## question
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

## answer
use the @madeOptional decorator like this:
```
@doc("Translation input.")
model TranslationInput {
  @typeChangedFrom(ApiVersions.v2024_05_20_preview, localeName)
  @madeOptional
  sourceLocale?: localeName;
}
```
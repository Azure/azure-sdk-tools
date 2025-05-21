# Need help in Adding final-state-schema for a single post action

## Question
Hi TypeSpec Discussion,
PR: https://github.com/Azure/azure-rest-api-specs-pr/pull/22427
 
We would like to add final-state-schema for a single post action LRO operation for now. Is it possible to do it for a single post action or not ?
 
when we add emit-lro-options: "all" in tspconfig.yml. It is reflected in all the long-running options..

## Answer
So, what you need to do is the following:  Change the response type to match the final result type you want  [Here is a playground](https://azure.github.io/typespec-azure/playground/?c=aW1wb3J0ICJAdHlwZXNwZWMvaHR0cCI7CtIZcmVzdNUZdmVyc2lvbmluZ8wfYXp1cmUtdG9vbHMvyCstxhVjb3Jl3yvIK3Jlc291cmNlLW1hbmFnZXIiOwoKdXNpbmcgSHR0cDvHDFJlc3TIDFbpAI7IEkHESi5Db3JlzhJSx1xNxls7CgovKiogQ29udG9zb8RUxR4gUHJvdmlkZXIg5gCDbWVudCBBUEkuICovCkBhcm3IIE5hbWVzcGFjZQpAc2VydmljZSgjeyB0aXRsZTogIsdXyC1IdWJDbGllbnQiIH0pCkDnAUNlZCjnAL9zKQpuyFAgTWljcm9zb2Z0LtJG7wC2QVBJIMdNc%2BQAoWVudW3oARNzIHsKICDELjIwMjEtMTAtMDEtcHJldmlld8g1xDQgIEB1c2VEZXBlbmRlbmN5KPUBLy7IVi52MV8wX1DGSF8xKcRAYXJtQ29tbW9uVOQBz8cq10jLKctUNcRIYPIAqWAsCn3mAPNB6AD16wEOIOgCMOQAyG1vZGVsIEVtcGxveWVlIGlzIFRyYWNrZWToAII8yBxQcm9wZXJ0aWVzPuUBJC4u6QCm5AHWUGFyYW1ldGVyyTE%2BO%2BgAhslfcMlE0nzKYOkBg0FnZSBvZiBlyD%2FlAXhhZ2U%2FOiBpbnQzMjsKxylDaXR50ipjaXR5Pzogc3Ry5QLpxyxQcm9maWzTWUBlbmNvZGUoImJhc2U2NHVybCLkAWBwxjA%2FOiBieXRlc8lIVGhlIHN0YXR1c8RLdGhlIGxhc3Qg5ADFYXRpb27lAvwgIEB2aXNpYmlsaXR5KExpZmVjeWNs5AHgYWTHXcQg5QOLU3RhdMRn5QGrzBTpAUjEc8wy5QCA5QDKYekB1cV3QGxyb8Q7dXMKdW7kArLRVOUBZOYBGizsANLIRyBjcmXEJ3JlcXVlc3QgaGFzIGJlZW4gYWNjZXB0ZWTEZyAgQccOOiAiyAsi1lBpxEDkALTpAMHIROwAnDogIswP2kx1cGRhdMRPxUNVxw46ICLIC8o76QSl6QDE5gDcZOcBolN1Y2NlZWTlAMXJDNM%2FxTbkAU1mYWlsyT5GxQ06ICLGCdw4d2FzIGNhbmNlyj5Dxw%2FkBP3HC%2F8BQCBkZWxl6QGARMQN5gD5yAsi6QP96QN3bW926gHK6QN5TW92ZVLHFegDcsRzbW92xGhmcm9tIGxvY%2BYAvMVuxBPxA1DLM3RvzzF0b8ov9wCVc3BvbnPrBIvmAJbHFuwAl%2B4DbMU%2BxWTGfOYC7s1uaW50ZXJm5AYcT%2BgDlHMgZXh0ZW5kc%2FYG3i7LKXt9CuUGusgjyhvLWegAzeYEs2dldOQBp0HKNeQD5%2BwFECDnAntPcuUCp%2BUB1ssvQ8cdUmVwbGFjZUFzeW5jzj%2FlAvfIN0N1c3RvbVBhdGNoU8QqCiAgIOkAkyzFDvYA50ZvdW7kAybkBkXIHOYAkU3kAYTGSdBLyhDqBarFGT4KICDlAJ3mAprvANTlApdlV2l0aG91dE9r8wDUbGlzdEJ5yDBHcm91cM9ETMUiUGFyZW501DxTdWJzY3JpcOUCecY7xjPMGcw56AZgIHNhbXBs6wNmYWPFRHRoYXTmAmPpBiN0byBkaWZmZeQAhO8C5sUp7gCyQcVI7gDtLOwDcsgN5gLp8wCTSEVBROoGTcR%2FY2hlY2vqAKtleGlzdGVu5gfHIMYeRckU7wJZzR3uB6I%3D&e=%40azure-tools%2Ftypespec-autorest&options=%7B%22linterRuleSet%22%3A%7B%22extends%22%3A%5B%22%40azure-tools%2Ftypespec-azure-rulesets%2Fresource-manager%22%5D%7D%2C%22options%22%3A%7B%22%40azure-tools%2Ftypespec-autorest%22%3A%7B%22emit-lro-options%22%3A%22all%22%7D%7D%7D) showing that the response parameter in ArmResourceActionAsync shows up both in the 200 response and in the final-state-schema, which means that typespec-based emitters  will get the right final response value and so will swagger-based emitters.  
 
To be clear the final-state-schema in this case is just for debugging purposes is it not necessary in the actual swagger.  I added a comment to the PR showing the change you should make here.
 
I don't see anything in the discussion that would contradict this.

# TypeSpec Authoring Instructions

## CRITICAL: Query Parameter Best Practice
When adding query parameters like `top` or `skip` to ARM operations, you MUST use the spread pattern with Azure.Core standard models:
- Use `...Azure.Core.TopQueryParameter` instead of `@query("$top") top?: int32`
- Use `...Azure.Core.SkipQueryParameter` instead of `@query("$skip") skip?: int32`

## CRITICAL: Extension Resource Operations
Extension resources MUST use `Extension.*` operation templates:
- `Extension.Read<Target, Resource>` instead of `ArmResourceRead<Resource>`
- `Extension.CreateOrReplaceAsync<Target, Resource>` instead of `ArmResourceCreateOrReplaceAsync<Resource>`
- `Extension.DeleteWithoutOkAsync<Target, Resource>`
- `Extension.ListByTarget<Target, Resource>`

## CRITICAL: LRO Headers for POST Actions
For async POST resource actions, always specify LRO headers explicitly:
- Use `LroHeaders = ArmCombinedLroHeaders<FinalResult = ResponseModel>`
- Example: `move is ArmResourceActionAsync<Employee, MoveRequest, MoveResponse, LroHeaders = ArmCombinedLroHeaders<FinalResult = MoveResponse>>`

## CRITICAL: Inline Suppress
When suppressing TypeSpec warnings, use inline `#suppress` comments directly above the offending declaration in the `.tsp` file. Do NOT use `linter.disable` in `tspconfig.yaml`.

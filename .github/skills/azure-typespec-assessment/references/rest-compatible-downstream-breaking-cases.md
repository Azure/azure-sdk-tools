# REST-Compatible Downstream Breaking Cases

Use these patterns to identify TypeSpec changes that preserve the REST contract but can break generated SDKs or other downstream clients.

## Client location changes

Changing `@@clientLocation` or operation-group assignment can move existing methods between generated clients. REST paths and payloads remain unchanged, but existing client construction and method calls can stop compiling.

## Client parameter order changes

An override can preserve historical positional parameter order while keeping the same path, query, and body serialization. Adding, removing, or changing that override can break callers even when requests on the wire are identical.

## Property flattening changes

Changing `flattenProperty` scope can switch a language between flattened and nested model access. The JSON still contains the same object, but object construction and member access change.

## Operation-group casing changes

Changing only the casing of a `@@clientLocation` group name can rename generated operation-group classes or accessors. REST operations remain identical, but downstream imports and member access can break.

## Client enum naming changes

Changing generated enum member names with `@@clientName` does not change serialized wire values. It can still break source code that references previously released language-specific enum constants.

All cases must emit Azure compliance as `not-assessed`.

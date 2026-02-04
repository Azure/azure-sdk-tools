# TypeSpec Decorators

Common questions about TypeSpec decorators and their usage.

---

## question
Does TypeSpec support negative lookahead in regex patterns for the `@pattern` decorator?

## answer
No, TypeSpec's `@pattern` decorator only supports simple regex syntax, the same as OpenAPI. Negative lookahead and other advanced regex features are not supported.

Note that `@pattern` is primarily for documentation purposes from the SDK's perspective - it won't validate at runtime regardless of the pattern complexity.

For complex name validation requirements (length, character restrictions, position rules), consider:
1. Using `@minLength` and `@maxLength` for length constraints
2. Using a simplified `@pattern` for basic character class validation
3. Implementing additional validation logic in your service layer

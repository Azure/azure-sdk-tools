# TypeSpec Decorators

Common issues and solutions for TypeSpec decorators and their usage.

## TypeSpec @pattern decorator does not support negative lookahead in regex

TypeSpec's `@pattern` decorator only supports simple regex syntax, the same as OpenAPI. Negative lookahead and other advanced regex features are not supported.

Note that `@pattern` is primarily for documentation purposes from the SDK's perspective - it won't validate at runtime regardless of the pattern complexity.

For complex name validation requirements (length, character restrictions, position rules), consider:
1. Using `@minLength` and `@maxLength` for length constraints
2. Using a simplified `@pattern` for basic character class validation
3. Implementing additional validation logic in your service layer

## TypeSpec Does Not Support Inheritance-Based Discriminators

In TypeSpec, using inheritance to model multiple layers of discriminated models where each layer fixes a different discriminator value is **not supported** and is considered an **anti‑pattern**. In an inheritance‑based discriminator design, once a model binds a specific discriminator value, that model is treated as a concrete leaf. Extending it again with a different discriminator value breaks the semantic contract of discriminators and leads to ambiguous or incorrect behavior in emitters such as OpenAPI and SDK generators. As a result, patterns like “base model → fixed discriminator → further derived model with another discriminator value” are intentionally rejected.
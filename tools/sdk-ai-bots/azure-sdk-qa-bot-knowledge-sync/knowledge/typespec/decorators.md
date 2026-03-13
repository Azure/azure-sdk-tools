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

## Use closed enum for truly fixed value sets like days of week

When modeling a value set that will never change (e.g., days of the week), a closed union or enum is appropriate and will be approved. Open unions (with `string`) are recommended only when the set may grow over time.

```typespec
enum DayOfWeek {
  Monday,
  Tuesday,
  Wednesday,
  Thursday,
  Friday,
  Saturday,
  Sunday,
}
```

Note: Prefer `/** */` doc comments over the `@doc` decorator for documentation.

## Do not use @example or @opExample decorators in Azure specs

Azure specs should not use the `@example` or `@opExample` decorator to provide inline examples. The `typespec-autorest` emitter automatically matches and inserts `x-ms-examples` by operationId from separate example files.

To generate or customize example values, use `oav generate-examples` or the api-scenario mechanism. You can also manually edit example files after generation.
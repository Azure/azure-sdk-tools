# ts-no-const-enums

Recommends against the usage of TypeScript's const enums.

## Examples

### Good

```ts
enum Directions {
  North,
  East,
  South,
  West
}
```

### Bad

```ts
const enum Directions {
  North,
  East,
  South,
  West
}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-no-const-enums)

# ts-package-json-types

Requires inclusion of type declarations in the package.

In practice, this means `types` in `package.json` must be set to a path pointing to a `.d.ts` file.

## Examples

### Good

```json
{
  "types": "types/index.d.ts"
}
```

### Bad

```json
{
  "types": "types/index.ts"
}
```

```json
{}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-types)

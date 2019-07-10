# ts-config-importhelpers

Requires `compilerOptions.importHelpers` in `tsconfig.json` to be set to `true`.

## Examples

### Good

```json
{
  "compilerOptions": {
    "importHelpers": true
  }
}
```

### Bad

```json
{
  "compilerOptions": {
    "importHelpers": false
  }
}
```

```json
{
  "compilerOptions": {}
}
```

```json
{}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-importhelpers)

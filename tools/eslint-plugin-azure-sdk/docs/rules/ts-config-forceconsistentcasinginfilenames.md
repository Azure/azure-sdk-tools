# ts-config-forceconsistentcasinginfilenames

Requires `compilerOptions.forceConsistentCasingInFileNames` in `tsconfig.json` to be set to `true`.

This rule is fixable using the `--fix` option.

## Examples

### Good

```json
{
  "compilerOptions": {
    "forceConsistentCasingInFileNames": true
  }
}
```

### Bad

```json
{
  "compilerOptions": {
    "forceConsistentCasingInFileNames": false
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

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-forceconsistentcasinginfilenames)

# ts-config-moduleresolution

Requires `compilerOptions.moduleResolution` in `tsconfig.json` to be set to `"node"`.

This rule is fixable using the `--fix` option.

## Examples

### Good

```json
{
  "compilerOptions": {
    "moduleResolution": "node"
  }
}
```

### Bad

```json
{
  "compilerOptions": {
    "moduleResolution": "classic"
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

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-moduleresolution)

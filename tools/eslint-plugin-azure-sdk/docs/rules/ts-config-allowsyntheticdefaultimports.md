# ts-config-allowsyntheticdefaultimports

Requires `compilerOptions.allowSyntheticDefaultImports` in `tsconfig.json` to be set to `true`.

This rule is fixable using the `--fix` option.

## Examples

### Good

```json
{
  "compilerOptions": {
    "allowSyntheticDefaultImports": true
  }
}
```

### Bad

```json
{
  "compilerOptions": {
    "allowSyntheticDefaultImports": false
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

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-allowsyntheticdefaultimports)

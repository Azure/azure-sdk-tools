# ts-config-strict

Requires `compilerOptions.strict` in `tsconfig.json` to be set to `false`.

This rule is fixable using the `--fix` option.

## Examples

### Good

```json
{
  "compilerOptions": {
    "strict": false
  }
}
```

### Bad

```json
{
  "compilerOptions": {
    "strict": true
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

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-strict)

# ts-config-declaration

Requires `compilerOptions.declaration` in `tsconfig.json` to be set to `true`.

This rule is fixable using the `--fix` option.

## Examples

### Good

```json
{
  "compilerOptions": {
    "declaration": true
  }
}
```

### Bad

```json
{
  "compilerOptions": {
    "declaration": false
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

## [Source](https://azure.github.io/azure-sdk/typescript_design.html#ts-config-declaration)

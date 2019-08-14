# ts-config-esmoduleinterop

Requires `compilerOptions.esModuleInterop` in `tsconfig.json` to be set to `true`.

This rule is fixable using the `--fix` option.

## Examples

### Good

```json
{
  "compilerOptions": {
    "esModuleInterop": true
  }
}
```

### Bad

```json
{
  "compilerOptions": {
    "esModuleInterop": false
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

## [Source](https://azure.github.io/azure-sdk/typescript_design.html#ts-config-esmoduleinterop)

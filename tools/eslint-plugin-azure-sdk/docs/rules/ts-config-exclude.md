# ts-config-exclude

Requires `compilerOptions.exclude` in `tsconfig.json` to include `node_modules`.

This rule is fixable using the `--fix` option.

## Examples

### Good

```json
{
  "compilerOptions": {
    "exclude": ["node_modules"]
  }
}
```

```json
{
  "compilerOptions": {
    "exclude": ["node_modules", "test"]
  }
}
```

### Bad

```json
{
  "compilerOptions": {
    "exclude": []
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

## [Source](https://azure.github.io/azure-sdk/typescript_design.html#ts-config-exclude)

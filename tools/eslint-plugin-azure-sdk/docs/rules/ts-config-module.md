# ts-config-module

Requires `compilerOptions.module` in `tsconfig.json` to be set to `"es6"`.

## Examples

### Good

```json
{
  "compilerOptions": {
    "module": "es6"
  }
}
```

### Bad

```json
{
  "compilerOptions": {
    "module": "es5"
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

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-module)

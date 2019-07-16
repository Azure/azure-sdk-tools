# ts-config-isolatedmodules

Recommends `compilerOptions.isolatedModules` in `tsconfig.json` to be set to `true`.

## Examples

### Good

```json
{
  "compilerOptions": {
    "isolatedModules": true
  }
}
```

### Bad

```json
{
  "compilerOptions": {
    "isolatedModules": false
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

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-isolatedmodules)

# ts-config-declaration

Requires `compilerOptions.declaration` in `tsconfig.json` to be set to `true`.

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

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-declaration)

# ts-config-exclude

Requires `compilerOptions.exclude` in `tsconfig.json` to include `node_modules`.

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

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-exclude)

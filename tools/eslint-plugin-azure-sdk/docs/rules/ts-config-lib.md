# ts-config-lib

Requires `compilerOptions.lib` in `tsconfig.json` to be set to an empty array.

## Examples

### Good

```json
{
    "compilerOptions": {
        "lib": []
    }
}
```

### Bad

```json
{
    "compilerOptions": {
        "lib": "exnext"
    }
}
```

```json
{
    "compilerOptions": {
        "lib": ["esnext", "dom"]
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

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-lib)

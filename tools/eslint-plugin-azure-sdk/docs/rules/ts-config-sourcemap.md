# ts-config-sourcmap

Requires `compilerOptions.sourceMap` and `compilerOptions.declarationMap` in `tsconfig.json` to be set to `true`.

## Examples

### Good

```json
{
    "compilerOptions": {
        "declarationMap": true,
        "sourceMap": true
    }
}
```

### Bad

```json
{
    "compilerOptions": {
        "declarationMap": false,
        "sourceMap": true
    }
}
```

```json
{
    "compilerOptions": {
        "sourceMap": true
    }
}

```json
{
    "compilerOptions": {}
}
```

```json
{}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-sourcemap)

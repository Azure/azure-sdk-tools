# ts-config-allow-syntheticdefaultimports

Requires `compilerOptions.allowSyntheticDefaultImports` in `tsconfig.json` to be set to true.

## Examples

### Good

```json
{
    "compilerOptions": {
        "allowSyntheticDefaultImports": true
    }
}
```

### Bad

```json
{
    "compilerOptions": {
        "allowSyntheticDefaultImports": false
    }
}
```

```json
{
    "compilerOptions": {}
}
```

# ts-package-json-homepage

Requires `homepage` in `package.json` to be set to the library's readme.

## Examples

### Good

```json
{
    "homepage": "https://github.com/Azure/azure-sdk-for-js/blob/master/sdk/servicebus/service-bus/README.md"
}
```

```json
{
    "homepage": "https://github.com/Azure/azure-sdk-for-js/blob/master/sdk/servicebus/service-bus"
}
```

### Bad

```json
{
    "homepage": "https://github.com/Azure/azure-sdk-for-js/blob/master/README.md"
}
```

```json
{
    "homepage": "https://github.com/Azure/azure-sdk-for-js/blob/master"
}
```

```json
{
    "homepage": "https://github.com/Azure/azure-sdk-for-java/blob/master/sdk/servicebus/service-bus/README.md"
}
```

```json
{}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-homepage)

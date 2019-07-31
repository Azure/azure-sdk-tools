# ts-naming-options

Requires client method options parameter types to be suffixed with `Options` and prefixed with the method name.

## Examples

### Good

```ts
class ServiceClient {
  createItem(options: CreateItemOptions): Item {
    /* code to return instance of Item */
  }
  upsertItem(options: UpsertItemOptions): Item {
    /* code to return instance of Item */
  }
}
```

### Bad

```ts
class ServiceClient {
  createItem(options: Options): Item {
    /* code to return instance of Item */
  }
}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-naming-options)

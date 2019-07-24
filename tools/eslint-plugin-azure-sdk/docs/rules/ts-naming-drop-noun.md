# ts-naming-drop-noun

Requires client methods that return instances of the client to drop the client name from the method name.

## Examples

### Good

```ts
class ServiceClient {
  create(): ServiceClient {
    /* code to return instance of ServiceClient */
  }
}
```

```ts
// private methods are ignored
class ServiceClient {
  private _createService(): ServiceClient {
    /* code to return instance of ServiceClient */
  }
}
```

### Bad

```ts
class ServiceClient {
  createService(): ServiceClient {
    /* code to return instance of ServiceClient */
  }
}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-naming-drop-noun)

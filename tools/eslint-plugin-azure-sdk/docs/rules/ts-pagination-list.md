# ts-pagination-list

Requires clients to include a `list` method that returns a `PagedAsyncIterableIterator`.

## Examples

### Good

```ts
class ServiceClient {
  listItems(): PagedAsyncIterableIterator<Item> {
    /* code to return instance of PagedAsyncIterableIterator<Item> */
  }
}
```

### Bad

```ts
// no list method
class ServiceClient {}
```

```ts
// no return type
class ServiceClient {
  listItems() {
    /* code here */
  }
}
```

```ts
// different return type
class ServiceClient {
  listItems(): AsyncIterator<Item> {
    /* code to return instance of AsyncIterator<Item> */
  }
}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-pagination-list)

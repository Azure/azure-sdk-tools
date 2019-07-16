# github-source-headers

Requires copyright headers in every source file.

Specifically, a source file is defined as any TypeScript file and the expected header text is as follows:

```fundamental
Copyright (c) Microsoft Corporation. All rights reserved.
Licensed under the MIT License.
```

## Examples

### Good

These must be located at the top of every source file.

```ts
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
```

```ts
/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License.
 */
```

```ts
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// Other comment text here.
```

### Bad

```ts
// Copyright (c) Microsoft Corporation. All rights reserved.
```

```ts
class Class {
  /* source code here */
}
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#github-source-headers)

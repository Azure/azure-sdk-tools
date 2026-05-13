## Overview

This application tokenises a Javascript project into a format useful for JavaScript API reviews. JavaScript API review parser is used by APIView system and CI pipelines to convert a JSON output file created by `api-extractor` to JSON token file interpreted by APIView to create and present review in APIView system.

The parser accepts two input formats:

- **`.api.json`** — the output of `api-extractor` (original behaviour)
- **`.d.ts`** — a TypeScript declaration file (no `api-extractor` run required)

## Building

1. Go to project directory `<repo root>/tools/apiview/parsers/js-api-parser` and Install npm packages.
    `npm install`
2. Run `npm run-script build`

## How To Use

### api-extractor JSON input (`.api.json`)

Run API extractor step on JS project to create json output file. This step is integrated within build commend for all Azure SDK projects in azure-sdk-for-js monorepo. So running build step is good enough to create input file for APIvIew parser. You can see a JSON file created in temp directory within package root directory once build step is completed successfully for the package.

Run `node ./dist/export.js <Path to api-extractor JSON output> <Path to apiviewFile> [Path to metadata.json]`

For e.g.

`node .\export.js C:\git\azure-sdk-for-js\sdk\core\core-client\temp\core-client.api.json C:\git\azure-sdk-for-js\sdk\core\core-client\temp\apiview.json`

Or if you have the package installed, you can run `ts-genapi <Path to api-extractor JSON output> <Path to apiviewFile> [Path to metadata.json]`.

### TypeScript declaration file input (`.d.ts`)

You can generate an API review directly from a `.d.ts` file without running `api-extractor`.

```
ts-genapi <path/to/package.d.ts> <path/to/output.json> [path/to/metadata.json] [--package-name <name>] [--package-version <version>]
```

**Package metadata** is resolved in this order:

1. `--package-name` / `--package-version` CLI flags (highest priority)
2. `name` / `version` fields in a `package.json` file located in the **same directory** as the `.d.ts` file
3. The `.d.ts` filename (without extension) is used as the package name if nothing else is found

**Subpath exports** — if the `.d.ts` file contains `declare module "..."` blocks, each block is treated as a separate subpath export in the review. This mirrors the multi-entry-point behaviour of `.api.json` files.

```typescript
// Example: package.d.ts
declare module "." {
  export interface MyClient { ... }
}
declare module "./models" {
  export type MyModel = { ... };
}
```

Each `declare module` block becomes its own collapsible subpath section in APIView.

**TSDoc tags** — `@beta`, `@alpha`, and `@deprecated` tags in JSDoc comments are detected and rendered, matching the behaviour of the `.api.json` path:

- `@beta` / `@alpha` → an annotation line is emitted before the declaration; child members inside a `@beta` container do not re-emit the same tag unless they carry a different one.
- `@deprecated` → a `@deprecated` annotation line is emitted and all tokens on the declaration are marked with `IsDeprecated: true`.

**Examples**

```
# Flat .d.ts file (no declare module blocks) — package.json in same directory
ts-genapi ./dist/index.d.ts ./review.json

# Override package name and version
ts-genapi ./dist/index.d.ts ./review.json --package-name @azure/my-package --package-version 1.2.3

# With cross-language metadata
ts-genapi ./dist/index.d.ts ./review.json metadata.json
```

### Cross-Language IDs

The optional third parameter allows you to specify a path to a `metadata.json` file that contains cross-language definitions. This enables API reviewers to correlate APIs that are generated from the same service API specification across different languages.

Example usage with metadata file:
`ts-genapi input.api.json output.json metadata.json`

The metadata.json file should have the following structure:
```json
{
  "crossLanguageDefinitions": {
    "CrossLanguageDefinitionId": {
      "@azure-rest/ai-content-safety!AnalyzeTextOptions:interface": "ContentSafety.AnalyzeTextOptions",
      "@azure-rest/ai-content-safety!AnalyzeTextResult:interface": "ContentSafety.AnalyzeTextResult"
    }
  }
}
```

When this mapping is provided, the parser will set the `CrossLanguageId` property on review lines for API items whose canonical reference exists in the mapping.

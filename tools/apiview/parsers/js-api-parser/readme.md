## Overview

This application tokenises a Javascript project into a format useful for JavaScript API reviews. JavaScript API review parser is used by APIView system and CI pipelines to convert a JSON output file created by `api-extractor` to JSON token file interpreted by APIView to create and present review in APIView system.

## Building

1. Go to project directory `<repo root>/tools/apiview/parsers/js-api-parser` and Install npm packages.
    `npm install
2. Run `npm run-script build`

## How To Use

Run API extractor step on JS project to create json output file. This step is integrated within build commend for all Azure SDK projects in azure-sdk-for-js monorepo. So running build step is good enough to create input file for APIvIew parser. You can see a JSON file created in temp directory within package root directory once build step is completed successfully for the package.

Run `node ./dist/export.js <Path to api-extractor JSON output> <Path to apiviewFile> [Path to metadata.json]`

For e.g.

`node .\export.js C:\git\azure-sdk-for-js\sdk\core\core-client\temp\core-client.api.json C:\git\azure-sdk-for-js\sdk\core\core-client\temp\apiview.json` 

Or if you have the package installed, you can run `ts-genapi <Path to api-extractor JSON output> <Path to apiviewFile> [Path to metadata.json]`.

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
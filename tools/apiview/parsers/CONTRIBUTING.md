# APIView token parsers

This page describes how to contribute to [APIView](../../../src//dotnet/APIView/APIViewWeb/APIViewWeb.csproj) language level parsers.
Specifically how to create or update a language parser to produce a hierarchy of the API using tokens with context, instead of a flat token list.

## Benefits of token schema with parent - child context

- Ability to granularly identify a specific node and it's sub nodes. For e.g. if a review line for a class is available then all it's child nodes contain all methods and properties within that class.
- Faster diffing using tree shaker instead of current text based comparison.
- Provide diffing with context of where the line that changed belong to in the tree, instead of showing the 5 lines before and after the change.
- Provide cross language view for a granular section within an API review.
- Less data to be stored in token file which are located in azure storage blob.

## Key concepts

APIView token schema is available in [TypeSpec](./apiview-treestyle-parser-schema/main.tsp) and [JSON](./apiview-treestyle-parser-schema/CodeFile.json). APIView requires all languages parser to create a JSON file as per the above mentioned schema to render the API review correctly. Navigation panel is automatically created based on the information in the tokens. Following are the main objects in the JSON schema.

- CodeFile
- ReviewLine
- ReviewToken
- CodeDiagnostic

### CodeFile

The `CodeFile` object represents the entire API review, which is rendered on the APIView tool. So the goal of language parser is to create a `CodeFile` object as per the [TypeSpec schema](./apiview-treestyle-parser-schema/main.tsp).

`CodeFile` object contains an array of `ReviewLine` object, an array of `CodeDiagnostic` and some metadata about the package for e.g. package name, package version, language etc.  

### ReviewLine

`ReviewLine` object corresponds a line displayed on APIView and it contains the API review tree context.
Each `ReviewLine` contains a list of `ReviewToken` which represents contents on the line.

Each `ReviewLine` object also has children of `ReviewLine` to include sub nodes that needs to be listed in a review as sub-lines under current API review line.

If a review line is related to another line at same level then id of related line should be set as `RelatedToLine` to ensure current line is not visible when related line is hidden. This use case is possible in diff view in which only modified node is visible.

### ReviewToken

A review line contains a list of `ReviewToken` and each `ReviewToken` represents a keyword, punctuation, text, type name etc. `ReviewToken` object also has a property that controls whether a space is required on review page after the current content. If the underlying content should act as an HREF then parser should set `NavigateToId` to the line ID of the target review line. `ReviewToken` can also be marked to be skipped from diff calculations when comparing two revisions by setting `SkipDiff` property of review token to true. For more information, refer the schema of `ReviewToken` in [TypeSpec schema](./apiview-treestyle-parser-schema/main.tsp)

### CodeDiagnostic

Code file contains a list of diagnostics detected by parser. Each `CodeDiagnostic` has a text, level and target review line ID. This also has optional diagnostic ID and help link URL.

## How to include a line in navigation panel

APIView generates a navigation tree based on the information in the tokens. A token is included in the navigation tree if `NavigatonDisplayName` is set in `ReviewToken` and `LineId` is set in `ReviewLine` object that contains the `ReviewToken`

## Serialization

Serialize the generated code file to JSON. The output file should have `.json` extension. Try to make the json as small as possible by ignoring null values and empty collections.
Don't worry about indentation as it will be handled by the tree structure based on the parent-child relationship among `ReviewLine` objects.

## Examples

A sample token file for .NET package `Azure.Template` is present [here](./apiview-treestyle-parser-schema/sample/Azure.Template_token.json) and corresponding rendered text representation is available [here](./apiview-treestyle-parser-schema/sample/Azure.Template_review_content.txt).

See following sample API review line and corresponding `ReviewLine` token.

API Review Line: `namespace Azure.Data.Tables {`

```json
{
  "LineId": "Azure.Data.Tables",
  "Tokens": [
    {
      "Kind": 2,
      "Value": "namespace",
      "HasSuffixSpace": true,
      "RenderClasses": []
    },
    {
       "Kind": 0,
       "Value": "Azure.Data.Tables",
       "NavigationDisplayName":"Azure.Data.Tables",
       "HasSuffixSpace": true,
       "RenderClasses": [
          "namespace"
        ]
    },
    {
       "Kind": 1,
       "Value": "{",
       "HasSuffixSpace": true,
       "RenderClasses": []
    }
  ],
  "Children": [],
  "IsHidden": false
}
```


## JSON token validation

You can validate JSON tokens against required JSON schema using [JSON schema validator](https://www.jsonschemavalidator.net/).

- Select `Custom` as schema type and copy and paste the contents in [json schema](./apiview-treestyle-parser-schema/CodeFile.json) to left panel.
- Generate APIView token file and paste generated JSON content onto right side panel to validate.

## Get help

Please reach out at [APIView Teams Channel](https://teams.microsoft.com/l/channel/19%3A3adeba4aa1164f1c889e148b1b3e3ddd%40thread.skype/APIView?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47) if you need more information.

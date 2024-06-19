# Tree token parser

This page describes how to contribute to [APIView](../../../src//dotnet/APIView/APIViewWeb/APIViewWeb.csproj) language level parsers.
Specifically how to create or update a language parser to produce a hierarchy of the API using a tree data tokens, instead of a flat token list.

## Tree style token parser benefits

- Ability to granularly identify a specific class and it’s methods or a specific API alone within a large review without rendering entire tokens.
- Faster diffing using tree shaker instead of current text based comparison.
- Provide diffing with context of where the line that changed belong to in the tree, instead of showing the 5 lines before and after the change.
- Provide cross language view for a granular section within an API review.
- Support for collapsible views at each class and namespace level to make it easier to review the change. User will be able to expand/collapse an entire class or a namespace and it’s children.
- Support dynamic representation of APIs for e.g. some users prefer to see all method arguments in same line and some users prefer to see them in multiple lines.
- Less data to be stored in token file which are located in azure storage blob.

## Key concepts

Previously APIview tokens were created as a flat list assigned to the `CodeFileToken[] Tokens`  property of the [CodeFile](../../../src/dotnet/APIView/APIView/Model/CodeFile.cs). Then the page navigation is created and assigned to `NavigationItem[] Navigation`. For tree style tokens these two properties are no longer required, instead a `List<APITreeNode> APIForest` property will be used to capture the generated tree of tokens.

![APITree](APITree.svg)

- Each module of the API (namespace, class, methods) should be its own node. Members of a module (methods in a class), (classes in a namespace) should be added as child nodes of its parent module.
- Each tree node has top tokens which should be used to capture the main tokens on the node, these can span multiple lines.
- Module name, decorators, and parameters should be modeled as `TopTokens`.
- If the language requires it use the bottom tokens to capture tokens that closes out the node, this is usually just the closing bracket and/or empty lines.

### Object Definitions

- Here are the models needed

  ```
  object APITreeNode
    string Name
    string Id
    string Kind
    Set<string> Tags
    Dictionary<string, string> Properties
    List<StructuredToken> TopTokens
    List<StructuredToken> BottomTokens
    List<APITreeNode> Children

  object StructuredToken
    string Value
    string Id
    StructuredTokenKind Kind
    Set<string> Tags
    Dictionary<string, string> Properties 
    Set<string> RenderClasses 

  enum StructuredTokenKind
    Content
    LineBreak
    NoneBreakingSpace
    TabSpace
    ParameterSeparator
    Url
  ```

### APITreeNode

- Ensure each node has an Id and Kind. The combination of `Id`, `Kind`, and `SubKind` should make the node unique across all nodes in the tree. For example a class and a method can potentially have the same Id, but the kind should differentiate them from each other.
- Sort each node at each level of the tree by your desired property, this is to ensure that difference in node order does not result in diff.

### StructuredToken

- Assign the final parsed value to a `List<APITreeNode> APIForest` property of the `CodeFile`

## Serialization

Serialize the generated code file to JSON and then compress the file using Gzip compression. Try to make the json as small as possible by ignoring null values and empty collections.
Don't worry about indentation that will be handled by the tree structure. In the case you want to have indentation between the tokens then use `TabSpace` token kind.

## How to handle commons Scenarios

- TEXT, KEYWORD, COMMENT : Use `text`, `keyword`, `comment` to property `RenderClasses` of `StructuredToken`.
- NEW_LINE : Create a token with `Kind = LineBreak`.
- WHITE_SPACE :  Create token with `Kind = NoneBreakingSpace`.
- PUNCTUATION : Create a token with `Kind = Content` and the `Value = <the punctuation>`.
- DOCUMENTATION : Add `GroupId = doc` in the properties of the token. This identifies a range of consecutive tokens as belonging to a group.
- SKIP_DIFF :  Add `SkipDiff` to the Tag to indicate that the node or token should not be included in diff computation.
- LINE_ID_MARKER : You can add a empty token. `Kind = Content` and `Value = ""` then give it an `Id` to make it commentable.
- EXTERNAL_LINK : Create a single token set `Kind = Url`, `Value = link` then add the link text as a properties `LinkText`;
- Common Tags: `Deprecated`, `Hidden`, `HideFromNav`, `SkipDiff`
- Cross Language Id: Use `CrossLangId` as key with value in the node properties.

## Get help

Please reach out at [APIView Teams Channel](https://teams.microsoft.com/l/channel/19%3A3adeba4aa1164f1c889e148b1b3e3ddd%40thread.skype/APIView?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47) if you need more information.

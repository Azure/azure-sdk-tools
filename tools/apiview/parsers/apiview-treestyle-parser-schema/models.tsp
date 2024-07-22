scalar StructuredTokenId extends string;
scalar APITreeNodeId extends string; 

enum StructuredTokenKind {
  /** Default kind */
  Content: 0,

  /** Starts a new line */
  LineBreak: 1,

  /** Single Space */
  NonBreakingSpace: 2,

  /** Four spaces */
  TabSpace: 3,
  
  /** Use this between method parameters. Depending on user setting this would result in a LineBreak or a LineBreak + TabSpace */
  ParameterSeparator: 4
}

enum CodeDiagnosticLevel {
  Info: 1,
  Warning: 2,
  Error: 3,
  Fatal: 4
}

/** Classes used to render the StructuredTokens. Extendable by contributing styles at https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/ClientSPA/src/app/_components/code-panel/code-panel.component.scss */
union StructuredTokenRenderClasses {
  "comment",
  "keyword",
  "literal",
  /** string literal */
  "sliteral",
  /** member name */
  "mname",
  /** type name */
  "tname",
  /** punctuation */
  "punc",
  "text",
  /** Contribute extra classes at https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/ClientSPA/src/app/_components/code-panel/code-panel.component.scss */
  string
}

union APITreeNodeTags {
  /** Mark a token as deprecated. */
  "Deprecated",
  /** Mark a node as Hidden. */
  "Hidden",
  /** Indicate that a node should be hidden from the page navigation. */
  "HideFromNav",
  /** Indicate that a node should not be used in computation of diff. */
  "SkipDiff"
}

union StructuredTokenTags {
  /** Mark a token as deprecated */
  "Deprecated",
  /** Indicate that a Token should not be used in computing diff*/
  "SkipDiff",
}

model CodeFile {
  /** The version of the parser */
  VersionString: string;

  /** Name the ocde file. Usually just the package name and version*/
  Name: string;
  
  Language: "C" | "C#"| "C++" | "Go" | "Java" | "JavaScript" | "Json" | "Kotlin" | "Python" | "Swagger" | "Swift" | "TypeSpec" | "Xml";
  LanguageVariant?: "Default" | "Spring" | "Android" = "Default";
  PackageName: string;
  ServiceName?: string;
  PackageVersion: string;

  /** Maps related packages across Languages */
  CrossLanguagePackageId?: string;

  /** The API Tree(s) */
  APIForest: APITreeNode[];

  Diagnostics: CodeDiagnostic[];
}

model CodeDiagnostic {
  /** String associated with a given diagnostic usieally produced by an analyzer*/
  DiagnosticId: string;
  Text: string;
  HelpLinkUri: string;
  /** Maps to the APITreeNode id */
  TargetId: APITreeNodeId;
  Level: CodeDiagnosticLevel;
}

model APITreeNode {
  /** The name of the tree node which will be used as label for the API Navigation. Generally this is the name of the module (class, method). */
  Name: string;

  /** Id of the node, which should be unique at the node level. i.e. unique among its siblings. This was previously represented by the DefinitionId for the main Token of the node. */
  Id: APITreeNodeId;

  /** Descriptor for the kind of the node. Currently used to select navigation icons */
  Kind: "assembly" | "namespace" | "class" | "delegate" | "enum" | "interface" | "method" | "package" | "struct" | "type";

  /** The main data of the node. This is all the tokens that actually define the node. When rendering, TopTokens are rendered first, followed by any Children, and then finally BottomTokens */
  TopTokens?: StructuredToken[];

  /** Tokens which are rendered after all child nodes. Depending on the language this would include the closing curly brace and/or empty lines. */
  BottomTokens?: StructuredToken[];

  /** The nodes immediate children. For a namespace this would be classes, for a class this would be the class constructors and methods. Children are rendered after TopTokens but before BottomTokens, and are automatically indented.*/
  Children?: APITreeNode[];

  /** Properties of the APITreeNode. */
  Properties?: APITreeNodeProperties;

  /** Tags of the APITreeNode. */
  Tags?: APITreeNodeTags[];
}

model StructuredToken {
  /** Token Id. Previously known as DefinitionId. */
  Id: StructuredTokenId;

  /** Represents the type of a structured token*/
  Kind: StructuredTokenKind;

  /** The token value which will be displayed. Spacing tokens (LineBreak, NonBreakingSpace, TabSpace, and ParameterSeparator) don't need to have value */
  Value?: string;

  /** Properties of the StructuredToken. */
  Properties?: StructuredTokenProperties;

  /** Tags of the StructuredToken. */
  Tags?: StructuredTokenTags[];

  /** Classes used to render the StructuredTokens. */
  RenderClasses?: StructuredTokenRenderClasses[]
}

model APITreeNodeProperties {
  /** Use this only if you are looking to add a custom icon different from language wide defaults set by APITreeNode kind*/
  IconName?: string;
    /** The cross language definitionId for the node. */
  CrossLangDefId?: string;
}

model StructuredTokenProperties {
  /** Group a sequence of consecutive StructuredTokens. doc indicates that the group of tokens is documentation. */
  GroupId?: "doc";

  /** Id to navigate to when StructuredToken is clicked. */
  NavigateToId?:  APITreeNodeId;
}
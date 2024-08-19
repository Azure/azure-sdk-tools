import "@typespec/json-schema";

using TypeSpec.JsonSchema;

@jsonSchema

/** ReviewFile represents entire API review object. This will be processed to render review lines. */
model CodeFile {
  PackageName: string;
  PackageVersion: string;
  /** version of the APIview language parser used to create token file*/
  ParserVersion: string;
  Language: "C"|"C++"|"C#"|"Go"|"Java"|"JavaScript"|"Kotlin"|"Python"|"Swagger"|"Swift"|"TypeSpec";
  /** Language variant is applicable only for java variants*/
  LanguageVariant?: "None" | "Spring" | "Android" = "None";
  CrossLanguagePackageId?: string;
  ReviewLines: Array<ReviewLine>;
  /** Add any system generated comments. Each comment is linked to review line ID */
  Diagnostics?: Array<CodeDiagnostic>;
}


/** ReviewLine object corresponds to each line displayed on API review. If an empty line is required then add a code line object without any token. */
model ReviewLine {
  /** lineId is only required if we need to support commenting on a line that contains this token. 
   *  Usually code line for documentation or just punctuation is not required to have lineId. lineId should be a unique value within 
   *  the review token file to use it assign to review comments as well as navigation Id within the review page.
   *  for e.g Azure.Core.HttpHeader.Common, azure.template.template_main
   */
  LineId?: string;
  CrossLanguageId?: string;
  /** list of tokens that constructs a line in API review */
  Tokens: Array<ReviewToken>;
  /** Add any child lines as children. For e.g. all classes and namespace level methods are added as a children of namespace(module) level code line. 
   *  Similarly all method level code lines are added as children of it's class code line.*/
  Children?: Array<ReviewLine>;
  /** Set current line as hidden code line by default. .NET has hidden APIs and architects don't want to see them by default. */
  IsHidden?: boolean;
}


/** Token corresponds to each component within a code line. A separate token is required for keyword, punctuation, type name, text etc. */
model ReviewToken {  
  Kind: TokenKind;
  Value: string;
  /** NavigationDisplayName is used to create a tree node in the navigation panel. Navigation nodes will be created only if token contains navigation display name.*/
  NavigationDisplayName?: string;
  /** navigateToId should be set if the underlying token is required to be displayed as HREF to another type within the review.
   * For e.g. a param type which is class name in the same package
   */
  NavigateToId?: string;
  /** set skipDiff to true if underlying token needs to be ignored from diff calculation. For e.g. package metadata or dependency versions 
   *  are usually excluded when comparing two revisions to avoid reporting them as API changes*/
  SkipDiff?: boolean = false;
  /** This is set if API is marked as deprecated */
  IsDeprecated?: boolean = false;
  /** Set this to false if there is no suffix space required before next token. For e.g, punctuation right after method name */
  HasSuffixSpace?: boolean = true;
  /** Set isDocumentation to true if current token is part of documentation */
  IsDocumentation?: boolean = false;
  /** Language specific style css class names */
  RenderClasses?: Array<string>;
}


/** System comment object is to add system generated comment. It can be one of the 4 different types of system comments. */
model CodeDiagnostic {
  /** Diagnostic ID is auto generated ID by CSharp analyzer. */
  DiagnosticId?: string;
    /** Id of ReviewLine object where this diagnostic needs to be displayed */
  TargetId: string;
  /** Auto generated system comment to be displayed under targeted line. */
  Text: string;
  Level: CodeDiagnosticLevel;
  HelpLinkUri?: string;
}

enum TokenKind {
  Text: 0,
  Punctuation: 1,
  Keyword: 2,
  TypeName: 3,
  MemberName: 4,
  StringLiteral: 5,
  Literal: 6,
  Comment: 7
}

enum CodeDiagnosticLevel {
 Info: 1,
 Warning: 2,
 Error:	3,
 Fatal:	4
}
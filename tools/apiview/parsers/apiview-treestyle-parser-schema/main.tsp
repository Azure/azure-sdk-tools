import "@typespec/json-schema";
import "@azure-tools/typespec-client-generator-core";

using Azure.ClientGenerator.Core;
using TypeSpec.JsonSchema;

@usage(Usage.input | Usage.output)
@service({title: "Schemas"})
namespace Schemas;

/** ReviewFile represents entire API review object. This will be processed to render review lines. */
@jsonSchema
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
  /** Navigation items are used to create a tree view in the navigation panel. Each navigation item is linked to a review line ID. This is optional.
  * If navigation items are not provided then navigation panel will be automatically generated using the review lines. Navigation items should be provided only if you want to customize the navigation panel.
  */
  Navigation?: Array<NavigationItem>;
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
  /** Set current line as context end line. For e.g. line with token } or empty line after the class or function/method to mark end of context. */
  IsContextEndLine?: boolean;
  /** Set ID of related line to ensure current line is not visible when a related line is hidden.
   * One e.g. is a code line for class attribute that should set class line's Line ID as related line ID.
   * OR a method line decorator that should set method's line ID as related line ID.
  */
  RelatedToLine?: string;
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
  /** Set skipDiff to true if underlying token needs to be ignored from diff calculation. For e.g. package metadata or dependency versions 
   *  are usually excluded when comparing two revisions to avoid reporting them as API changes*/
  SkipDiff?: boolean = false;
  /** This is set if API is marked as deprecated */
  IsDeprecated?: boolean = false;
  /** Set this to false if there is no suffix space required before next token. For e.g, punctuation right after method name */
  HasSuffixSpace?: boolean = true;
  /** Set this to true if there is a prefix space required before current token. For e.g, space before token for = */
  HasPrefixSpace?: boolean = false;
  /** Set isDocumentation to true if current token is part of documentation */
  IsDocumentation?: boolean = false;
  /** Language specific style css class names. To render navigation icons, one of the following must be specified:
   * "namespace", "class", "method", "enum". If NavigationDisplayName is specified, then this field should be set.
  */
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
  /** Text: Token kind should be set as Text for any plan text token. for e.g documentation, namespace value, or attribute or decorator tokens **/
  Text: 0,
  /** Punctuation **/
  Punctuation: 1,
  /** Keyword **/
  Keyword: 2,
  /** TypeName: Kind should be set as TypeName for class definitions, base class token, parameter types etc **/
  TypeName: 3,
  /** MemberName: Kind should be set as MemberName for method name tokens, member variable tokens **/
  MemberName: 4,
  /** StringLiteral: Token kind for any metadata or string literals to show in API view **/
  StringLiteral: 5,
  /** Literal: Token kind for any literals, for e.g. enum value or numerical constant literal or default value **/
  Literal: 6,
  /** Comment: Comment text within the code that's really a documentation. 
   *  Few languages wants to show comments within API review that's not tagged as documentation **/
  Comment: 7,
  /** External Url */
  ExternalUrl: 8
}

enum CodeDiagnosticLevel {
 Info: 1,
 Warning: 2,
 Error:	3,
  /** Fatal level diagnostic will block API review approval and it will show an error message to the user. Approver will have to 
  * override fatal level system comments before approving a review.*/
 Fatal:	4
}

model NavigationItem {
  Text: string;
  NavigationId: string;
  ChildItems: Array<NavigationItem>;
  Tags: Record<string>;
}

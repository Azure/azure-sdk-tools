import "@typespec/json-schema";

using TypeSpec.JsonSchema;

@jsonSchema

/** CodeFile represents entire API review object. This will be processed to render review lines. */
model CodeFile {
  packageName: string;
  packageVersion: string;
  /** version of the APIview language parser used to create token file*/
  parserVersion: string;
  language: "C"|"C++"|"C#"|"Go"|"Java"|"JavaScript"|"Kotlin"|"Python"|"Swagger"|"Swift"|"TypeSpec";
  /** Language variant is applicable only for java variants*/
  languageVariant?: "None" | "Spring" | "Android" = "None";
  crossLanguagePackageId?: string;
  codeLines: Array<CodeLine>;
  /** Add any system generated comments for the current token. */
  systemComments?: Array<CodeDiagnostic>;
}


/** Code line object corresponds to each line displayed on API review. If an empty line is required then add a code line object without any token. */
model CodeLine {
  /** lineId is only required if we need to support commenting on a line that contains this token. 
   *  Usually code line for documentation or just punctuation is not required to have lineId. lineId should be a unique value within 
   *  the review token file to use it assign to review comments as well as navigation Id within the review page.
   *  for e.g Azure.Core.HttpHeader.Common, azure.template.template_main
   */
  lineId?: string;
  crossLanguageId?: string;
  /** list of tokens that constructs a line in API review */
  tokens: Array<Token>;
  /** Add any child lines as children. For e.g. all classes and namespace level methods are added as a children of namespace(module) level code line. 
   *  Similarly all method level code lines are added as children of it's class code line.*/
  children?: Array<CodeLine>;
}


/** Token corresponds to each component within a code line. A separate token is required for keyword, punctuation, type name, text etc. */
model Token {  
  kind: TokenKind;
  value: string;
  /** Subkind is required when kind is TypeName or Membername */
  SubKind?: TokenSubKind;
  /** navigateToId should be set if the underlying token is required to be displayed as HREF to another type within the review.
   * For e.g. a param type which is class name in the same package
   */
  navigateToId?: string;
  /** set skipDiff to true if underlying token needs to be ignored from diff calculation. For e.g. package metadata or dependency versions 
   *  are usually excluded when comparing two revisions to avoid reporting them as API changes*/
  skipDiff?: boolean = false;
  /** This is set if API is marked as hidden */
  isHidden?: boolean = false;
  /** This is set if API is marked as deprecated */
  isDeprecated?: boolean = false;
  /** Set this to false if there is no suffix space required before next token. For e.g, punctuation right after method name */
  hasSuffixSpace?: boolean = true;
  /** Set isDocumentation to true if current token is part of documentation */
  isDocumentation?: boolean = false;
  /** Language specific style css class names */
  languageStyleClasses?: Array<string>;
}


/** Code diagnostic object is to add system generated comment. It can be one of the 4 different types of system comments. */
model CodeDiagnostic {
  /** Id of CodeLine object where this diagnostic needs to be displayed. */
  lineId: string;
  text: string;
  level: DiagnosticLevel;
  helpLinkUri?: url;
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

enum TokenSubKind {
  Assembly: 0,
  NameSpace: 1,
  Class: 2,
  Delegate: 3,
  Template: 4,
  Enum: 5,
  Interface: 6,
  Method: 7,
  Struct: 8,
  Union: 9,
  Type: 10,
  Package: 11,
  Member: 12
}

enum DiagnosticLevel {
 Info: 0,
 Warning: 1,
 Error:	2,
 Fatal:	3
}
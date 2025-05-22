// These schemas are all adapted from the TypeSpec definition here: 
// https://github.com/Azure/azure-sdk-tools/blob/main/tools/apiview/parsers/apiview-treestyle-parser-schema/main.tsp

import { AliasStatementNode, EnumStatementNode, InterfaceStatementNode, IntersectionExpressionNode, ModelExpressionNode, ModelStatementNode, ObjectLiteralNode, OperationStatementNode, ScalarStatementNode, SyntaxKind, UnionExpressionNode, UnionStatementNode } from "@typespec/compiler/ast";
import { NamespaceModel } from "./namespace-model.js";
import { NamespaceStack } from "./util.js";

// CORE API VIEW SCHEMAS

export enum TokenKind {
  Text = 0,
  Punctuation = 1,
  Keyword = 2,
  TypeName = 3,
  MemberName = 4,
  StringLiteral = 5,
  Literal = 6,
  Comment = 7
}
  
/** ReviewFile represents entire API review object. This will be processed to render review lines. */
export interface CodeFile {
  Name: string;
  PackageName: string;
  PackageVersion: string;
  /** version of the APIview language parser used to create token file*/
  ParserVersion: string;
  Language: string;
  /** Language variant is applicable only for java variants*/
  LanguageVariant: string | undefined;
  CrossLanguagePackageId: string | undefined;
  ReviewLines: ReviewLine[];
  /** Add any system generated comments. Each comment is linked to review line ID */
  Diagnostics: CodeDiagnostic[] | undefined;
  /** Navigation items are used to create a tree view in the navigation panel. Each navigation item is linked to a review line ID. This is optional.
  * If navigation items are not provided then navigation panel will be automatically generated using the review lines. Navigation items should be provided only if you want to customize the navigation panel.
  */
  Navigation: NavigationItem[] | undefined;
}

export interface ReviewLineOptions {
  /** Set current line as hidden code line by default. .NET has hidden APIs and architects don't want to see them by default. */
  IsHidden?: boolean;
  /** Set current line as context end line. For e.g. line with token } or empty line after the class to mark end of context. */
  IsContextEndLine?: boolean;
  /** Set ID of related line to ensure current line is not visible when a related line is hidden.
   * One e.g. is a code line for class attribute should set class line's Line ID as related line ID.
  */
  RelatedToLine?: string;
}

/** ReviewLine object corresponds to each line displayed on API review. If an empty line is required then add a code line object without any token. */
export interface ReviewLine extends ReviewLineOptions {
  /** lineId is only required if we need to support commenting on a line that contains this token. 
   *  Usually code line for documentation or just punctuation is not required to have lineId. lineId should be a unique value within 
   *  the review token file to use it assign to review comments as well as navigation Id within the review page.
   *  for e.g Azure.Core.HttpHeader.Common, azure.template.template_main
   */
  LineId: string | undefined;
  CrossLanguageId: string | undefined;
  /** list of tokens that constructs a line in API review */
  Tokens: ReviewToken[];
  /** Add any child lines as children. For e.g. all classes and namespace level methods are added as a children of namespace(module) level code line. 
   *  Similarly all method level code lines are added as children of it's class code line.*/
  Children: ReviewLine[];
}

export interface ReviewTokenOptions {
  /** NavigationDisplayName is used to create a tree node in the navigation panel. Navigation nodes will be created only if token contains navigation display name.*/
  NavigationDisplayName?: string;
  /** navigateToId should be set if the underlying token is required to be displayed as HREF to another type within the review.
   * For e.g. a param type which is class name in the same package
   */
  NavigateToId?: string;
  /** set skipDiff to true if underlying token needs to be ignored from diff calculation. For e.g. package metadata or dependency versions 
   *  are usually excluded when comparing two revisions to avoid reporting them as API changes*/
  SkipDiff?: boolean;
  /** This is set if API is marked as deprecated */
  IsDeprecated?: boolean;
  /** Set this to true if a prefix space is required before the next value. */
  HasPrefixSpace?: boolean;
  /** Set this to true if a suffix space required before next token. For e.g, punctuation right after method name */
  HasSuffixSpace?: boolean;
  /** Set isDocumentation to true if current token is part of documentation */
  IsDocumentation?: boolean;
  /** Language specific style css class names */
  RenderClasses?: Array<string>;
}

/** Token corresponds to each component within a code line. A separate token is required for keyword, punctuation, type name, text etc. */
export interface ReviewToken extends ReviewTokenOptions {
  Kind: TokenKind;
  Value: string;
}

// CODE DIAGNOSTIC SCHEMAS

export enum CodeDiagnosticLevel {
  Info = 1,
  Warning = 2,
  Error = 3,
  /** Fatal level diagnostic will block API review approval and it will show an error message to the user. Approver will have to 
  * override fatal level system comments before approving a review.*/
  Fatal = 4
}

/** System comment object is to add system generated comment. It can be one of the 4 different types of system comments. */
export interface CodeDiagnostic {
  /** Auto generated system comment to be displayed under targeted line. */
  Text: string;
  /** Diagnostic ID is auto generated ID by CSharp analyzer. */
  DiagnosticId?: string;
  /** Id of ReviewLine object where this diagnostic needs to be displayed */
  TargetId: string;
  Level: CodeDiagnosticLevel;
  HelpLinkUri?: string;
}

// NAVIGATION SCHEMAS

export class NavigationItem {
  Text: string;
  NavigationId: string | undefined;
  ChildItems: NavigationItem[];
  Tags: ApiViewNavigationTag;

  constructor(
    objNode:
      | AliasStatementNode
      | NamespaceModel
      | ModelStatementNode
      | OperationStatementNode
      | InterfaceStatementNode
      | EnumStatementNode
      | ModelExpressionNode
      | IntersectionExpressionNode
      | ScalarStatementNode
      | UnionStatementNode
      | UnionExpressionNode
      | ObjectLiteralNode,
      stack: NamespaceStack
  ) {
    let obj;
    switch (objNode.kind) {
      case SyntaxKind.NamespaceStatement:
        stack.push(objNode.name);
        this.Text = objNode.name;
        this.Tags = { TypeKind: ApiViewNavigationKind.Module };
        const operationItems = new Array<NavigationItem>();
        for (const node of objNode.operations.values()) {
          operationItems.push(new NavigationItem(node, stack));
        }
        const resourceItems = new Array<NavigationItem>();
        for (const node of objNode.resources.values()) {
          resourceItems.push(new NavigationItem(node, stack));
        }
        const modelItems = new Array<NavigationItem>();
        for (const node of objNode.models.values()) {
          modelItems.push(new NavigationItem(node, stack));
        }
        const aliasItems = new Array<NavigationItem>();
        for (const node of objNode.aliases.values()) {
            aliasItems.push(new NavigationItem(node, stack));
        }
        this.ChildItems = [];
        if (operationItems.length) {
          this.ChildItems.push({ Text: "Operations", ChildItems: operationItems, Tags: { TypeKind: ApiViewNavigationKind.Method }, NavigationId: "" });
        }
        if (resourceItems.length) {
          this.ChildItems.push({ Text: "Resources", ChildItems: resourceItems, Tags: { TypeKind: ApiViewNavigationKind.Class }, NavigationId: "" });
        }
        if (modelItems.length) {
          this.ChildItems.push({ Text: "Models", ChildItems: modelItems, Tags: { TypeKind: ApiViewNavigationKind.Class }, NavigationId: "" });
        }
        if (aliasItems.length) {
            this.ChildItems.push({ Text: "Aliases", ChildItems: aliasItems, Tags: { TypeKind: ApiViewNavigationKind.Class }, NavigationId: "" });
        }
        break;
      case SyntaxKind.ModelStatement:
        obj = objNode as ModelStatementNode;
        stack.push(obj.id.sv);
        this.Text = obj.id.sv;
        this.Tags = { TypeKind: ApiViewNavigationKind.Class };
        this.ChildItems = [];
        break;
      case SyntaxKind.EnumStatement:
        obj = objNode as EnumStatementNode;
        stack.push(obj.id.sv);
        this.Text = obj.id.sv;
        this.Tags = { TypeKind: ApiViewNavigationKind.Enum };
        this.ChildItems = [];
        break;
      case SyntaxKind.OperationStatement:
        obj = objNode as OperationStatementNode;
        stack.push(obj.id.sv);
        this.Text = obj.id.sv;
        this.Tags = { TypeKind: ApiViewNavigationKind.Method };
        this.ChildItems = [];
        break;
      case SyntaxKind.InterfaceStatement:
        obj = objNode as InterfaceStatementNode;
        stack.push(obj.id.sv);
        this.Text = obj.id.sv;
        this.Tags = { TypeKind: ApiViewNavigationKind.Method };
        this.ChildItems = [];
        for (const child of obj.operations) {
          this.ChildItems.push(new NavigationItem(child, stack));
        }
        break;
      case SyntaxKind.UnionStatement:
        obj = objNode as UnionStatementNode;
        stack.push(obj.id.sv);
        this.Text = obj.id.sv;
        this.Tags = { TypeKind: ApiViewNavigationKind.Enum };
        this.ChildItems = [];
        break;
      case SyntaxKind.AliasStatement:
        obj = objNode as AliasStatementNode;
        stack.push(obj.id.sv);
        this.Text = obj.id.sv;
        this.Tags = { TypeKind: ApiViewNavigationKind.Class };
        this.ChildItems = [];
        break;
      case SyntaxKind.ModelExpression:
        throw new Error(`Navigation unsupported for "ModelExpression".`);
      case SyntaxKind.IntersectionExpression:
        throw new Error(`Navigation unsupported for "IntersectionExpression".`);
      case SyntaxKind.ScalarStatement:
        obj = objNode as ScalarStatementNode;
        stack.push(obj.id.sv);
        this.Text = obj.id.sv;
        this.Tags = { TypeKind: ApiViewNavigationKind.Class };
        this.ChildItems = [];
        break;
      default:
        throw new Error(`Navigation unsupported for "${objNode.kind.toString()}".`);
    }
    this.NavigationId = stack.value();
    stack.pop();
  }
}

export interface ApiViewNavigationTag {
  TypeKind: ApiViewNavigationKind;
}

export const enum ApiViewNavigationKind {
  Class = "class",
  Enum = "enum",
  Method = "method",
  Module = "namespace",
  Package = "assembly",
}

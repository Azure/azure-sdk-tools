import {
  AliasStatementNode,
  ArrayExpressionNode,
  AugmentDecoratorStatementNode,
  BaseNode,
  BooleanLiteralNode,
  DecoratorExpressionNode,
  DirectiveExpressionNode,
  EnumMemberNode,
  EnumSpreadMemberNode,
  EnumStatementNode,
  Expression,
  getNamespaceFullName,
  getSourceLocation,
  IdentifierNode,
  InterfaceStatementNode,
  IntersectionExpressionNode,
  MemberExpressionNode,
  ModelExpressionNode,
  ModelPropertyNode,
  ModelSpreadPropertyNode,
  ModelStatementNode,
  Namespace,
  navigateProgram,
  NumericLiteralNode,
  OperationSignatureDeclarationNode,
  OperationSignatureReferenceNode,
  OperationStatementNode,
  Program,
  ScalarStatementNode,
  StringLiteralNode,
  StringTemplateExpressionNode,
  StringTemplateHeadNode,
  StringTemplateSpanNode,
  SyntaxKind,
  TemplateArgumentNode,
  TemplateParameterDeclarationNode,
  TupleExpressionNode,
  TypeReferenceNode,
  UnionExpressionNode,
  UnionStatementNode,
  UnionVariantNode,
  ValueOfExpressionNode,
} from "@typespec/compiler";
import { ApiViewDiagnostic, ApiViewDiagnosticLevel } from "./diagnostic.js";
import { generateId, NamespaceModel } from "./namespace-model.js";
import { LIB_VERSION } from "./version.js";

const WHITESPACE = " ";

/** Supported render classes for APIView v2.
 *  You can add custom ones but need to provide CSS to EngSys.
 */
export const enum RenderClass {
  text,
  keyword,
  punctuation,
  literal,
  comment,
  typeName = "type-name",
  memberName = "member-name",
  stringLiteral = "string-literal",
}

/** Tags supported by APIView v2 */
export enum Tag {
  /** Show item as deprecated. */
  deprecated,
  /** Hide item from APIView. */
  hidden,
  /** Hide item from APIView Navigation. */
  hideFromNav,
  /** Ignore differences in this item when calculating diffs. */
  skipDiff,
}

export const enum TokenLocation {
  /** ApiTreeNode.TopTokens. Most tokens will go here. */
  top,
  /** ApiTreeNode.BottomTokens. Useful for closing braces. */
  bottom,
}

/**
 * Describes the type of structured token.
 */
export const enum TokenKind {
  content = 0,
  lineBreak = 1,
  nonBreakingSpace = 2,
  tabSpace = 3,
  parameterSeparator = 4,
  url = 5,
}

/**
 * New-style structured APIView token.
 */
export class StructuredToken {
  value?: string;
  id?: string;
  kind: TokenKind;
  tags?: Set<string>;
  properties: Map<string, string>;
  renderClasses: Set<string>;

  constructor(kind: TokenKind, options?: TokenOptions) {
    this.id = options?.lineId;
    this.kind = kind;
    this.value = options?.value;
    this.properties = options?.properties ?? new Map<string, string>();
    this.renderClasses = new Set([...(options?.renderClasses ?? []).toString()]);
  }
}

/**
 * Options when creating a new ApiTreeNode.
 */
export interface NodeOptions {
  tags?: Tag[];
  properties?: Map<string, string>;
}

/**
 * Options when creating a new StructuredToken.
 */
export interface TokenOptions {
  renderClasses?: RenderClass[];
  value?: string;
  tags?: Tag[];
  properties?: Map<string, string>;
  location?: TokenLocation;
  lineId?: string;
}

/**
 * New-style structured APIView node.
 */
export class ApiTreeNode {
  name: string;
  id: string;
  kind: string;
  tags?: Set<string>;
  properties: Map<string, string>;
  topTokens: StructuredToken[];
  bottomTokens: StructuredToken[];
  children: ApiTreeNode[];

  constructor(name: string, id: string, kind: string, options?: NodeOptions) {
    this.name = name;
    this.id = id;
    this.kind = kind;
    this.tags = new Set([...(options?.tags ?? []).toString()]);
    this.properties = options?.properties ?? new Map<string, string>();
    this.topTokens = [];
    this.bottomTokens = [];
    this.children = [];
  }

  /**
   * Creates a new token to add to the tree node.
   * @param kind the token kind to create
   * @param lineId an optional line id
   * @param options options you can set
   */
  token(kind: TokenKind, options?: TokenOptions) {
    const token: StructuredToken = {
      kind: kind,
      value: options?.value,
      id: options?.lineId ?? "",
      tags: new Set([...(options?.tags ?? []).toString()]),
      properties: options?.properties ?? new Map<string, string>(),
      renderClasses: new Set([...(options?.renderClasses ?? []).toString()]),
    };
    const location = options?.location ?? TokenLocation.top;
    if (location === TokenLocation.top) {
      this.topTokens.push(token);
    } else {
      this.bottomTokens.push(token);
    }
  }

  /**
   * Adds the specified number of spaces.
   * @param count number of spaces to add
   */
  whitespace(count: number = 1) {
    this.topTokens.push(new StructuredToken(TokenKind.nonBreakingSpace, { value: WHITESPACE.repeat(count) }));
  }

  /**
   * Ensures exactly one space.
   */
  space() {    
    if (this.topTokens[this.topTokens.length - 1]?.kind !== TokenKind.nonBreakingSpace) {
      this.topTokens.push(new StructuredToken(TokenKind.nonBreakingSpace, { value: WHITESPACE }));
    }
  }

  /**
   * Adds a newline token.
   */
  newline() {
    this.topTokens.push(new StructuredToken(TokenKind.lineBreak));
  }

  /**
   * Ensures a specific number of blank lines.
   * @param count number of blank lines to add
   */
  blankLines(count: number) {
    for (let i = 0; i < count; i++) {
      this.token(TokenKind.lineBreak, { tags: [Tag.skipDiff] });
    }
    // TODO: Erase this is no longer needed.
    // // count the number of trailing newlines (ignoring indent whitespace)
    // let newlineCount: number = 0;
    // for (let i = this.tokens.length; i > 0; i--) {
    //   const token = this.tokens[i - 1];
    //   if (token.Kind === ApiViewTokenKind.Newline) {
    //     newlineCount++;
    //   } else if (token.Kind === ApiViewTokenKind.Whitespace) {
    //     continue;
    //   } else {
    //     break;
    //   }
    // }
    // if (newlineCount < count + 1) {
    //   // if there aren't new enough newlines, add some
    //   const toAdd = count + 1 - newlineCount;
    //   for (let i = 0; i < toAdd; i++) {
    //     this.newline();
    //   }
    // } else if (newlineCount > count + 1) {
    //   // if there are too many newlines, remove some
    //   let toRemove = newlineCount - (count + 1);
    //   while (toRemove) {
    //     const popped = this.tokens.pop();
    //     if (popped?.Kind === ApiViewTokenKind.Newline) {
    //       toRemove--;
    //     }
    //   }
    // }
  }

  punctuation(value: string, options?: {prefixSpace?: boolean, postfixSpace?: boolean, location?: TokenLocation}) {
    const prefixSpace = options?.prefixSpace ?? false;
    const postfixSpace = options?.postfixSpace ?? false;
    const location = options?.location ?? TokenLocation.top;

    if (prefixSpace) {
      this.space();
    }
    this.token(TokenKind.content, {
      value: value,
      renderClasses: [RenderClass.punctuation],
      location: location,
    });
    if (postfixSpace) {
      this.space();
    }
  }

  // lineMarker(addCrossLanguageId: boolean = false) {
  //   const token = {
  //     Kind: ApiViewTokenKind.LineIdMarker,
  //     DefinitionId: this.namespaceStack.value(),
  //     CrossLanguageDefinitionId: addCrossLanguageId ? this.namespaceStack.value() : undefined,
  //   };
  //   this.tokens.push(token);
  // }

  text(text: string) {
    this.token(TokenKind.content, {
      value: text,
      renderClasses: [RenderClass.text],
    });
  }

  keyword(keyword: string, options: {prefixSpace?: boolean, postfixSpace?: boolean}) {
    const prefixSpace = options.prefixSpace ?? false;
    const postfixSpace = options.postfixSpace ?? false;
    if (prefixSpace) {
      this.space();
    }
    this.token(TokenKind.content, {
      value: keyword,
      renderClasses: [RenderClass.keyword],
    });
    if (postfixSpace) {
      this.space();
    }
  }

  // typeDeclaration(typeName: string, typeId: string | undefined, addCrossLanguageId: boolean) {
  //   if (typeId) {
  //     if (this.typeDeclarations.has(typeId)) {
  //       throw new Error(`Duplication ID "${typeId}" for declaration will result in bugs.`);
  //     }
  //     this.typeDeclarations.add(typeId);
  //   }
  //   this.tokens.push({
  //     Kind: ApiViewTokenKind.TypeName,
  //     DefinitionId: typeId,
  //     Value: typeName,
  //     CrossLanguageDefinitionId: addCrossLanguageId ? typeId : undefined,
  //   });
  // }

  // typeReference(typeName: string, targetId?: string) {
  //   this.tokens.push({
  //     Kind: ApiViewTokenKind.TypeName,
  //     Value: typeName,
  //     NavigateToId: targetId ?? "__MISSING__",
  //   });
  // }

  member(name: string) {
    this.token(TokenKind.content, {
      value: name,
      renderClasses: [RenderClass.memberName],
    });
  }

  // stringLiteral(value: string) {
  //   const lines = value.split("\n");
  //   if (lines.length === 1) {
  //     this.tokens.push({
  //       Kind: ApiViewTokenKind.StringLiteral,
  //       Value: `\u0022${value}\u0022`,
  //     });
  //   } else {
  //     this.punctuation(`"""`);
  //     this.newline();
  //     for (const line of lines) {
  //       this.literal(line);
  //       this.newline();
  //     }
  //     this.punctuation(`"""`);
  //   }
  // }

  literal(value: string) {
    this.topTokens.push(
      new StructuredToken(TokenKind.content, {
        renderClasses: [RenderClass.literal],
        value: value,
      }),
    );
  }
}

export interface ApiViewDocument {
  name: string;
  packageName: string;
  tokens: null;
  apiForest: ApiTreeNode[] | null;
  navigation: null;
  diagnostics: ApiViewDiagnostic[];
  versionString: string;
  language: string;
  crossLanguagePackageId: string | undefined;
}

export class ApiView {
  name: string;
  packageName: string;
  crossLanguagePackageId: string | undefined;
  nodes: ApiTreeNode[] = [];
  diagnostics: ApiViewDiagnostic[] = [];
  versionString: string;

  namespaceStack = new NamespaceStack();
  typeDeclarations = new Set<string>();
  includeGlobalNamespace: boolean;

  constructor(name: string, packageName: string, versionString?: string, includeGlobalNamespace?: boolean) {
    this.name = name;
    this.packageName = packageName;
    this.versionString = versionString ?? "";
    this.includeGlobalNamespace = includeGlobalNamespace ?? false;
    this.crossLanguagePackageId = packageName;
    this.emitHeader();
  }

  node(
    parent: ApiTreeNode | ApiView,
    name: string,
    id: string,
    options?: { kind?: string; tags?: Tag[]; properties?: Map<string, string> },
  ): ApiTreeNode {
    const child = new ApiTreeNode(name, id, options?.kind ?? "ApiTreeNode", {
      tags: options?.tags,
      properties: options?.properties,
    });
    if (parent instanceof ApiView) {
      parent.nodes.push(child);
    } else {
      parent.children.push(child);
    }
    return child;
  }

  diagnostic(message: string, targetId: string, level: ApiViewDiagnosticLevel) {
    this.diagnostics.push(new ApiViewDiagnostic(message, targetId, level));
  }

  shouldEmitNamespace(name: string): boolean {
    if (name === "" && this.includeGlobalNamespace) {
      return true;
    }
    if (name === this.packageName) {
      return true;
    }
    if (!name.startsWith(this.packageName)) {
      return false;
    }
    const suffix = name.substring(this.packageName.length);
    return suffix.startsWith(".");
  }

  emit(program: Program) {
    let allNamespaces = new Map<string, Namespace>();

    // collect namespaces in program
    navigateProgram(program, {
      namespace(obj) {
        const name = getNamespaceFullName(obj);
        allNamespaces.set(name, obj);
      },
    });
    allNamespaces = new Map([...allNamespaces].sort());

    for (const [name, ns] of allNamespaces.entries()) {
      if (!this.shouldEmitNamespace(name)) {
        continue;
      }
      // use a fake name to make the global namespace clear
      const namespaceName = name === "" ? "::GLOBAL::" : name;
      const nsModel = new NamespaceModel(namespaceName, ns, program);
      if (nsModel.shouldEmit()) {
        this.tokenizeNamespaceModel(nsModel);
      }
    }
  }

  private emitHeader() {
    const toolVersion = LIB_VERSION;
    const headerText = `// Package parsed using @azure-tools/typespec-apiview (version:${toolVersion})`;
    this.namespaceStack.push("GLOBAL");
    const node = this.node(this, "preamble", "::GLOBAL::preamble", { tags: [Tag.hideFromNav, Tag.skipDiff] });
    node.literal(headerText);
    this.namespaceStack.pop();
    // TODO: Source URL?
    node.blankLines(2);
  }

  tokenize(node: BaseNode, treeNode: ApiTreeNode) {
    let obj;
    let isExpanded = false;
    switch (node.kind) {
      case SyntaxKind.AliasStatement:
        obj = node as AliasStatementNode;
        this.namespaceStack.push(obj.id.sv);
        treeNode.keyword("alias", {postfixSpace: true});
        treeNode.typeDeclaration(obj.id.sv, this.namespaceStack.value(), true);
        this.tokenizeTemplateParameters(obj.templateParameters, treeNode);
        treeNode.punctuation("=", {prefixSpace: true, postfixSpace: true});
        this.tokenize(obj.value, treeNode);
        this.namespaceStack.pop();
        break;
      case SyntaxKind.ArrayExpression:
        obj = node as ArrayExpressionNode;
        this.tokenize(obj.elementType, treeNode);
        treeNode.punctuation("[]");
        break;
      case SyntaxKind.AugmentDecoratorStatement:
        obj = node as AugmentDecoratorStatementNode;
        const decoratorName = this.getNameForNode(obj.target);
        this.namespaceStack.push(decoratorName);
        treeNode.punctuation("@@");
        this.tokenizeIdentifier(obj.target, "keyword", treeNode);
        if (obj.arguments.length) {
          const last = obj.arguments.length - 1;
          treeNode.punctuation("(");
          this.tokenize(obj.targetType, treeNode);
          if (obj.arguments.length) {
            treeNode.punctuation(",", {postfixSpace: true});
          }
          for (let x = 0; x < obj.arguments.length; x++) {
            const arg = obj.arguments[x];
            this.tokenize(arg, treeNode);
            if (x !== last) {
              treeNode.punctuation(",", {postfixSpace: true});
            }
          }
          treeNode.punctuation(")");
          this.namespaceStack.pop();
        }
        break;
      case SyntaxKind.BooleanLiteral:
        obj = node as BooleanLiteralNode;
        treeNode.literal(obj.value.toString());
        break;
      case SyntaxKind.BlockComment:
        throw new Error(`Case "BlockComment" not implemented`);
      case SyntaxKind.TypeSpecScript:
        throw new Error(`Case "TypeSpecScript" not implemented`);
      case SyntaxKind.DecoratorExpression:
        obj = node as DecoratorExpressionNode;
        treeNode.punctuation("@");
        this.tokenizeIdentifier(obj.target, "keyword", treeNode);
        if (obj.arguments.length) {
          const last = obj.arguments.length - 1;
          treeNode.punctuation("(");
          for (let x = 0; x < obj.arguments.length; x++) {
            const arg = obj.arguments[x];
            this.tokenize(arg, treeNode);
            if (x !== last) {
              treeNode.punctuation(",", {postfixSpace: true});
            }
          }
          treeNode.punctuation(")");
        }
        break;
      case SyntaxKind.DirectiveExpression:
        obj = node as DirectiveExpressionNode;
        this.namespaceStack.push(generateId(node)!);
        treeNode.keyword(`#${obj.target.sv}`, {postfixSpace: true});
        for (const arg of obj.arguments) {
          switch (arg.kind) {
            case SyntaxKind.StringLiteral:
              treeNode.stringLiteral(arg.value);
              treeNode.space();
              break;
            case SyntaxKind.Identifier:
              treeNode.stringLiteral(arg.sv);
              treeNode.space();
              break;
          }
        }
        treeNode.newline();
        this.namespaceStack.pop();
        break;
      case SyntaxKind.EmptyStatement:
        throw new Error(`Case "EmptyStatement" not implemented`);
      case SyntaxKind.EnumMember:
        obj = node as EnumMemberNode;
        this.tokenizeDecoratorsAndDirectives(obj.decorators, obj.directives, false);
        this.tokenizeIdentifier(obj.id, "member");
        if (obj.value) {
          treeNode.punctuation(":", {postfixSpace: true});
          this.tokenize(obj.value, treeNode);
        }
        break;
      case SyntaxKind.EnumSpreadMember:
        obj = node as EnumSpreadMemberNode;
        treeNode.punctuation("...");
        this.tokenize(obj.target, treeNode);
        break;
      case SyntaxKind.EnumStatement:
        this.tokenizeEnumStatement(node as EnumStatementNode);
        break;
      case SyntaxKind.JsNamespaceDeclaration:
        throw new Error(`Case "JsNamespaceDeclaration" not implemented`);
      case SyntaxKind.JsSourceFile:
        throw new Error(`Case "JsSourceFile" not implemented`);
      case SyntaxKind.Identifier:
        obj = node as IdentifierNode;
        const id = this.namespaceStack.value();
        treeNode.typeReference(obj.sv, id);
        break;
      case SyntaxKind.ImportStatement:
        throw new Error(`Case "ImportStatement" not implemented`);
      case SyntaxKind.IntersectionExpression:
        obj = node as IntersectionExpressionNode;
        for (let x = 0; x < obj.options.length; x++) {
          const opt = obj.options[x];
          this.tokenize(opt, treeNode);
          if (x !== obj.options.length - 1) {
            treeNode.punctuation("&", {prefixSpace: true, postfixSpace: true});
          }
        }
        break;
      case SyntaxKind.InterfaceStatement:
        this.tokenizeInterfaceStatement(node as InterfaceStatementNode);
        break;
      case SyntaxKind.InvalidStatement:
        throw new Error(`Case "InvalidStatement" not implemented`);
      case SyntaxKind.LineComment:
        throw new Error(`Case "LineComment" not implemented`);
      case SyntaxKind.MemberExpression:
        this.tokenizeIdentifier(node as MemberExpressionNode, "reference");
        break;
      case SyntaxKind.ModelExpression:
        this.tokenizeModelExpression(node as ModelExpressionNode, false, false);
        break;
      case SyntaxKind.ModelProperty:
        this.tokenizeModelProperty(node as ModelPropertyNode, false);
        break;
      case SyntaxKind.ModelSpreadProperty:
        obj = node as ModelSpreadPropertyNode;
        treeNode.punctuation("...");
        this.tokenize(obj.target, treeNode);
        break;
      case SyntaxKind.ModelStatement:
        obj = node as ModelStatementNode;
        this.tokenizeModelStatement(obj);
        break;
      case SyntaxKind.NamespaceStatement:
        throw new Error(`Case "NamespaceStatement" not implemented`);
      case SyntaxKind.NeverKeyword:
        treeNode.keyword("never", {prefixSpace: true, postfixSpace: true});
        break;
      case SyntaxKind.NumericLiteral:
        obj = node as NumericLiteralNode;
        treeNode.literal(obj.value.toString());
        break;
      case SyntaxKind.OperationStatement:
        this.tokenizeOperationStatement(node as OperationStatementNode);
        break;
      case SyntaxKind.OperationSignatureDeclaration:
        obj = node as OperationSignatureDeclarationNode;
        treeNode.punctuation("(");
        // TODO: heuristic for whether operation signature should be inlined or not.
        const inline = false;
        this.tokenizeModelExpression(obj.parameters, true, inline);
        treeNode.punctuation("):", {postfixSpace: true});
        this.tokenizeReturnType(obj, inline);
        break;
      case SyntaxKind.OperationSignatureReference:
        obj = node as OperationSignatureReferenceNode;
        treeNode.keyword("is", {prefixSpace: true, postfixSpace: true});
        this.tokenize(obj.baseOperation, treeNode);
        break;
      case SyntaxKind.Return:
        throw new Error(`Case "Return" not implemented`);
      case SyntaxKind.StringLiteral:
        obj = node as StringLiteralNode;
        treeNode.stringLiteral(obj.value);
        break;
      case SyntaxKind.ScalarStatement:
        this.tokenizeScalarStatement(node as ScalarStatementNode);
        break;
      case SyntaxKind.TemplateParameterDeclaration:
        obj = node as TemplateParameterDeclarationNode;
        this.tokenize(obj.id, treeNode);
        if (obj.constraint) {
          treeNode.keyword("extends", {prefixSpace: true, postfixSpace: true});
          this.tokenize(obj.constraint, treeNode);
        }
        if (obj.default) {
          treeNode.punctuation("=", {prefixSpace: true, postfixSpace: true});
          this.tokenize(obj.default, treeNode);
        }
        break;
      case SyntaxKind.TupleExpression:
        obj = node as TupleExpressionNode;
        treeNode.punctuation("[", {prefixSpace: true, postfixSpace: true});
        for (let x = 0; x < obj.values.length; x++) {
          const val = obj.values[x];
          this.tokenize(val, treeNode);
          if (x !== obj.values.length - 1) {
            this.renderPunctuation(",", treeNode);
          }
        }
        treeNode.punctuation("]", {postfixSpace: true});
        break;
      case SyntaxKind.TypeReference:
        obj = node as TypeReferenceNode;
        isExpanded = this.isTemplateExpanded(obj);
        this.tokenizeIdentifier(obj.target, "reference", treeNode);
        // Render the template parameter instantiations
        if (obj.arguments.length) {
          treeNode.punctuation("<");
          if (isExpanded) {
            treeNode.newline();
          }
          for (let x = 0; x < obj.arguments.length; x++) {
            const arg = obj.arguments[x];
            this.tokenize(arg, treeNode);
            if (x !== obj.arguments.length - 1) {
              this.renderPunctuation(",", treeNode);
              if (isExpanded) {
                treeNode.newline();
              }
            }
          }
          if (isExpanded) {
            treeNode.newline();
          }
          treeNode.punctuation(">");
        }
        break;
      case SyntaxKind.UnionExpression:
        obj = node as UnionExpressionNode;
        for (let x = 0; x < obj.options.length; x++) {
          const opt = obj.options[x];
          this.tokenize(opt, treeNode);
          if (x !== obj.options.length - 1) {
            treeNode.punctuation("|", {prefixSpace: true, postfixSpace: true});
          }
        }
        break;
      case SyntaxKind.UnionStatement:
        this.tokenizeUnionStatement(node as UnionStatementNode);
        break;
      case SyntaxKind.UnionVariant:
        this.tokenizeUnionVariant(node as UnionVariantNode);
        break;
      case SyntaxKind.UnknownKeyword:
        treeNode.keyword("any", {prefixSpace: true, postfixSpace: true});
        break;
      case SyntaxKind.UsingStatement:
        throw new Error(`Case "UsingStatement" not implemented`);
      case SyntaxKind.ValueOfExpression:
        treeNode.keyword("valueof", {prefixSpace: true, postfixSpace: true})
        this.tokenize((node as ValueOfExpressionNode).target, treeNode);
        break;
      case SyntaxKind.VoidKeyword:
        treeNode.keyword("void", {prefixSpace: true});
        break;
      case SyntaxKind.TemplateArgument:
        obj = node as TemplateArgumentNode;
        isExpanded = obj.argument.kind === SyntaxKind.ModelExpression && obj.argument.properties.length > 0;
        if (isExpanded) {
          treeNode.blankLines(0);
        }
        if (obj.name) {
          treeNode.text(obj.name.sv);
          treeNode.punctuation("=", {prefixSpace: true, postfixSpace: true});
        }
        if (isExpanded) {
          this.tokenizeModelExpressionExpanded(obj.argument as ModelExpressionNode, false, false);
        } else {
          this.tokenize(obj.argument, treeNode);
        }
        break;
      case SyntaxKind.StringTemplateExpression:
        obj = node as StringTemplateExpressionNode;
        const stringValue = this.buildTemplateString(obj);
        const multiLine = stringValue.includes("\n");
        // single line case
        if (!multiLine) {
          treeNode.stringLiteral(stringValue);
          break;
        }
        // otherwise multiline case
        const lines = stringValue.split("\n");
        treeNode.punctuation(`"""`);
        treeNode.newline();
        for (const line of lines) {
          treeNode.literal(line);
          treeNode.newline();
        }
        treeNode.punctuation(`"""`);
        break;
      case SyntaxKind.StringTemplateSpan:
        obj = node as StringTemplateSpanNode;
        treeNode.punctuation("${");
        this.tokenize(obj.expression, treeNode);
        treeNode.punctuation("}");
        this.tokenize(obj.literal, treeNode);
        break;
      case SyntaxKind.StringTemplateHead:
      case SyntaxKind.StringTemplateMiddle:
      case SyntaxKind.StringTemplateTail:
        obj = node as StringTemplateHeadNode;
        treeNode.literal(obj.value);
        break;
      default:
        // All Projection* cases should fail here...
        throw new Error(`Case "${SyntaxKind[node.kind].toString()}" not implemented`);
    }
  }

  peekTokens(count: number = 20): string {
    let result = "";
    const tokens = this.tokens.slice(-count);
    for (const token of tokens) {
      if (token.Value) {
        result += token.Value;
      }
    }
    return result;
  }

  private buildExpressionString(node: Expression) {
    switch (node.kind) {
      case SyntaxKind.StringLiteral:
        return `"${(node as StringLiteralNode).value}"`;
      case SyntaxKind.NumericLiteral:
        return (node as NumericLiteralNode).value.toString();
      case SyntaxKind.BooleanLiteral:
        return (node as BooleanLiteralNode).value.toString();
      case SyntaxKind.StringTemplateExpression:
        return this.buildTemplateString(node as StringTemplateExpressionNode);
      case SyntaxKind.VoidKeyword:
        return "void";
      case SyntaxKind.NeverKeyword:
        return "never";
      case SyntaxKind.TypeReference:
        const obj = node as TypeReferenceNode;
        switch (obj.target.kind) {
          case SyntaxKind.Identifier:
            return (obj.target as IdentifierNode).sv;
          case SyntaxKind.MemberExpression:
            return this.getFullyQualifiedIdentifier(obj.target as MemberExpressionNode);
        }
      default:
        throw new Error(`Unsupported expression kind: ${SyntaxKind[node.kind]}`);
      //unsupported ArrayExpressionNode | MemberExpressionNode | ModelExpressionNode | TupleExpressionNode | UnionExpressionNode | IntersectionExpressionNode | TypeReferenceNode | ValueOfExpressionNode | AnyKeywordNode;
    }
  }

  /** Constructs a single string with template markers. */
  private buildTemplateString(node: StringTemplateExpressionNode): string {
    let result = node.head.value;
    for (const span of node.spans) {
      result += "${" + this.buildExpressionString(span.expression) + "}";
      result += span.literal.value;
    }
    return result;
  }

  private tokenizeModelStatement(node: ModelStatementNode, treeNode: ApiTreeNode) {
    this.namespaceStack.push(node.id.sv);
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    treeNode.keyword("model", {postfixSpace: true});
    this.tokenizeIdentifier(node.id, "declaration");
    if (node.extends) {
      treeNode.keyword("extends", {prefixSpace: true, postfixSpace: true});
      this.tokenize(node.extends, treeNode);
    }
    if (node.is) {
      treeNode.keyword("is", {prefixSpace: true, postfixSpace: true});
      this.tokenize(node.is, treeNode);
    }
    this.tokenizeTemplateParameters(node.templateParameters);
    if (node.properties.length) {
      for (const prop of node.properties) {
        const propName = this.getNameForNode(prop);
        this.namespaceStack.push(propName);
        this.tokenize(prop, treeNode);
        treeNode.punctuation(";");
        this.namespaceStack.pop();
        treeNode.blankLines(0);
      }
    } else {
      treeNode.punctuation("{}", {postfixSpace: false});
    }
    this.namespaceStack.pop();
  }

  private tokenizeScalarStatement(node: ScalarStatementNode, treeNode: ApiTreeNode) {
    this.namespaceStack.push(node.id.sv);
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    treeNode.keyword("scalar", {postfixSpace: true});
    this.tokenizeIdentifier(node.id, "declaration", treeNode);
    if (node.extends) {
      treeNode.keyword("extends", {prefixSpace: true, postfixSpace: true});
      this.tokenize(node.extends, treeNode);
    }
    this.tokenizeTemplateParameters(node.templateParameters, treeNode);
    treeNode.blankLines(0);
    this.namespaceStack.pop();
  }

  private tokenizeInterfaceStatement(node: InterfaceStatementNode, treeNode: ApiTreeNode) {
    this.namespaceStack.push(node.id.sv);
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    treeNode.keyword("interface", {postfixSpace: true});
    this.tokenizeIdentifier(node.id, "declaration", treeNode);
    this.tokenizeTemplateParameters(node.templateParameters, treeNode);
    for (let x = 0; x < node.operations.length; x++) {
      const op = node.operations[x];
      this.tokenizeOperationStatement(op, true, treeNode);
      treeNode.blankLines(x !== node.operations.length - 1 ? 1 : 0);
    }
    this.namespaceStack.pop();
  }

  private tokenizeEnumStatement(node: EnumStatementNode) {
    this.namespaceStack.push(node.id.sv);
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    this.keyword("enum", false, true);
    this.tokenizeIdentifier(node.id, "declaration");
    this.beginGroup();
    for (const member of node.members) {
      const memberName = this.getNameForNode(member);
      this.namespaceStack.push(memberName);
      this.tokenize(member);
      this.punctuation(",");
      this.namespaceStack.pop();
      this.blankLines(0);
    }
    this.endGroup();
    this.namespaceStack.pop();
  }

  private tokenizeUnionStatement(node: UnionStatementNode) {
    this.namespaceStack.push(node.id.sv);
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    this.keyword("union", false, true);
    this.tokenizeIdentifier(node.id, "declaration");
    this.beginGroup();
    for (let x = 0; x < node.options.length; x++) {
      const variant = node.options[x];
      const variantName = this.getNameForNode(variant);
      this.namespaceStack.push(variantName);
      this.tokenize(variant);
      this.namespaceStack.pop();
      if (x !== node.options.length - 1) {
        this.punctuation(",");
      }
      this.blankLines(0);
    }
    this.namespaceStack.pop();
    this.endGroup();
  }

  private tokenizeUnionVariant(node: UnionVariantNode) {
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    if (node.id !== undefined) {
      this.tokenizeIdentifier(node.id, "member");
      this.punctuation(":", false, true);
    }
    this.lineMarker(true);
    this.tokenize(node.value);
  }

  private tokenizeModelProperty(node: ModelPropertyNode, inline: boolean) {
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, inline);
    this.tokenizeIdentifier(node.id, "member");
    this.lineMarker();
    this.punctuation(node.optional ? "?:" : ":", false, true);
    this.tokenize(node.value);
    if (node.default) {
      this.punctuation("=", true, true);
      this.tokenize(node.default);
    }
  }

  private tokenizeModelExpressionInline(node: ModelExpressionNode, isOperationSignature: boolean) {
    if (node.properties.length) {
      if (!isOperationSignature) {
        this.punctuation("{", true, true);
      }
      for (let x = 0; x < node.properties.length; x++) {
        const prop = node.properties[x];
        switch (prop.kind) {
          case SyntaxKind.ModelProperty:
            this.tokenizeModelProperty(prop, true);
            break;
          case SyntaxKind.ModelSpreadProperty:
            this.tokenize(prop);
            break;
        }
        if (isOperationSignature) {
          if (x !== node.properties.length - 1) {
            this.punctuation(",", false, true);
          }
        } else {
          this.punctuation(";");
        }
      }
      if (!isOperationSignature) {
        this.punctuation("}", true, true);
      }
    }
  }

  private tokenizeModelExpressionExpanded(
    node: ModelExpressionNode,
    isOperationSignature: boolean,
    leadingNewline: boolean,
  ) {
    if (node.properties.length) {
      if (leadingNewline) {
        this.blankLines(0);
        this.indent();
      }
      if (!isOperationSignature) {
        this.punctuation("{", false, false);
        this.blankLines(0);
        this.indent();
      }
      this.namespaceStack.push("anonymous");
      for (let x = 0; x < node.properties.length; x++) {
        const prop = node.properties[x];
        const propName = this.getNameForNode(prop);
        this.namespaceStack.push(propName);
        switch (prop.kind) {
          case SyntaxKind.ModelProperty:
            this.tokenizeModelProperty(prop, false);
            break;
          case SyntaxKind.ModelSpreadProperty:
            this.tokenize(prop);
        }
        this.namespaceStack.pop();
        if (isOperationSignature) {
          if (x !== node.properties.length - 1) {
            this.trim(true);
            this.renderPunctuation(",");
          }
        } else {
          this.trim(true);
          this.renderPunctuation(";");
        }
        this.blankLines(0);
      }
      this.namespaceStack.pop();
      this.blankLines(0);
      if (!isOperationSignature) {
        this.deindent();
        this.punctuation("}", false, false);
        this.blankLines(0);
      }
      this.trim();
      if (leadingNewline) {
        this.deindent();
      }
    } else if (!isOperationSignature) {
      this.punctuation("{}", true, false);
    }
  }

  private tokenizeModelExpression(node: ModelExpressionNode, isOperationSignature: boolean, inline: boolean) {
    if (inline) {
      this.tokenizeModelExpressionInline(node, isOperationSignature);
    } else {
      this.tokenizeModelExpressionExpanded(node, isOperationSignature, true);
    }
  }

  private tokenizeOperationStatement(node: OperationStatementNode, suppressOpKeyword: boolean = false) {
    this.namespaceStack.push(node.id.sv);
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    if (!suppressOpKeyword) {
      this.keyword("op", false, true);
    }
    this.tokenizeIdentifier(node.id, "declaration");
    this.tokenizeTemplateParameters(node.templateParameters);
    this.tokenize(node.signature);
    this.punctuation(";", false, false);
    this.namespaceStack.pop();
  }

  private tokenizeNamespaceModel(model: NamespaceModel) {
    this.namespaceStack.push(model.name);
    if (model.node.kind === SyntaxKind.NamespaceStatement) {
      this.tokenizeDecoratorsAndDirectives(model.node.decorators, model.node.directives, false);
    }
    this.keyword("namespace", false, true);
    this.typeDeclaration(model.name, this.namespaceStack.value(), true);
    this.beginGroup();
    for (const node of model.augmentDecorators) {
      this.tokenize(node);
      this.blankLines(1);
    }
    for (const node of model.operations.values()) {
      this.tokenize(node);
      this.blankLines(1);
    }
    for (const node of model.resources.values()) {
      this.tokenize(node);
      this.blankLines(1);
    }
    for (const node of model.models.values()) {
      this.tokenize(node);
      this.blankLines(1);
    }
    for (const node of model.aliases.values()) {
      this.tokenize(node);
      this.punctuation(";");
      this.blankLines(1);
    }
    this.endGroup();
    this.blankLines(1);
    this.namespaceStack.pop();
  }

  private tokenizeDecoratorsAndDirectives(
    decorators: readonly DecoratorExpressionNode[] | undefined,
    directives: readonly DirectiveExpressionNode[] | undefined,
    inline: boolean,
  ) {
    const docDecorators = ["doc", "summary", "example"];
    if ((directives || []).length === 0 && (decorators || []).length === 0) {
      return;
    }
    for (const directive of directives ?? []) {
      this.tokenize(directive);
    }
    if (!inline && decorators?.length && directives === undefined) {
      while (this.tokens.length) {
        const item = this.tokens.pop()!;
        if (item.Kind === ApiViewTokenKind.LineIdMarker && item.DefinitionId === "GLOBAL") {
          this.tokens.push(item);
          this.blankLines(2);
          break;
        } else if ([ApiViewTokenKind.Punctuation, ApiViewTokenKind.TypeName].includes(item.Kind)) {
          this.tokens.push(item);
          // for now, render with no newlines, per stewardship board request
          const lineCount = ["{", "("].includes(item.Value!) ? 0 : 0;
          this.blankLines(lineCount);
          break;
        }
      }
    }
    // render each decorator
    for (const node of decorators || []) {
      this.namespaceStack.push(generateId(node)!);
      const isDoc = docDecorators.includes((node.target as IdentifierNode).sv);
      if (isDoc) {
        this.tokens.push({
          Kind: ApiViewTokenKind.DocumentRangeStart,
        });
      }
      this.tokenize(node);
      if (inline) {
        this.space();
      }
      this.namespaceStack.pop();
      if (!inline) {
        this.blankLines(0);
      }
      if (isDoc) {
        this.tokens.push({
          Kind: ApiViewTokenKind.DocumentRangeEnd,
        });
      }
    }
  }

  private getFullyQualifiedIdentifier(node: MemberExpressionNode, suffix?: string): string {
    switch (node.base.kind) {
      case SyntaxKind.Identifier:
        return `${node.base.sv}.${suffix}`;
      case SyntaxKind.MemberExpression:
        return this.getFullyQualifiedIdentifier(node.base, `${node.base.id.sv}.${suffix}`);
    }
  }

  private tokenizeIdentifier(
    node: IdentifierNode | MemberExpressionNode | StringLiteralNode,
    style: "declaration" | "reference" | "member" | "keyword",
  ) {
    switch (node.kind) {
      case SyntaxKind.MemberExpression:
        const defId = this.getFullyQualifiedIdentifier(node, node.id.sv);
        switch (style) {
          case "reference":
            this.typeReference(defId);
            break;
          case "member":
            this.member(defId);
            break;
          case "keyword":
            this.keyword(defId);
            break;
          case "declaration":
            throw new Error(`MemberExpression cannot be a "declaration".`);
        }
        break;
      case SyntaxKind.StringLiteral:
        if (style !== "member") {
          throw new Error(`StringLiteral type can only be a member name. Unexpectedly "${style}"`);
        }
        this.stringLiteral(node.value);
        break;
      case SyntaxKind.Identifier:
        switch (style) {
          case "declaration":
            this.typeDeclaration(node.sv, this.namespaceStack.value(), true);
            break;
          case "reference":
            const defId = this.definitionIdFor(node.sv, this.packageName);
            this.typeReference(node.sv, defId);
            break;
          case "member":
            this.member(this.getRawText(node));
            break;
          case "keyword":
            this.keyword(node.sv);
            break;
        }
    }
  }

  private getRawText(node: IdentifierNode): string {
    return getSourceLocation(node).file.text.slice(node.pos, node.end);
  }

  private isTemplateExpanded(node: TypeReferenceNode): boolean {
    if (node.arguments.length === 0) {
      return false;
    }
    const first = node.arguments[0];
    return first.argument.kind === SyntaxKind.ModelExpression;
  }

  private tokenizeTemplateParameters(nodes: readonly TemplateParameterDeclarationNode[], isExpanded: boolean = false) {
    if (nodes.length) {
      this.punctuation("<", false, false);
      for (let x = 0; x < nodes.length; x++) {
        const param = nodes[x];
        this.tokenize(param);
        if (x !== nodes.length - 1) {
          this.renderPunctuation(",");
          this.space();
        }
        if (isExpanded) {
          this.trim(true);
          this.newline();
        }
      }
      this.punctuation(">");
    }
  }

  private tokenizeReturnType(node: OperationSignatureDeclarationNode, inline: boolean) {
    if (!inline && node.parameters.properties.length) {
      const offset = this.tokens.length;
      this.tokenize(node.returnType);
      const returnTokens = this.tokens.slice(offset);
      const returnTypeString = returnTokens
        .filter((x) => x.Value)
        .flatMap((x) => x.Value)
        .join("");
      this.namespaceStack.push(returnTypeString);
      this.lineMarker();
      this.namespaceStack.pop();
    } else {
      this.tokenize(node.returnType);
    }
  }

  private getNameForNode(node: BaseNode | NamespaceModel): string {
    const id = generateId(node);
    if (id) {
      return id.split(".").splice(-1)[0];
    } else {
      throw new Error("Unable to get name for node.");
    }
  }

  private renderPunctuation(punctuation: string) {
    this.punctuation(punctuation, false, true);
  }

  resolveMissingTypeReferences() {
    for (const token of this.tokens) {
      if (token.Kind === ApiViewTokenKind.TypeName && token.NavigateToId === "__MISSING__") {
        token.NavigateToId = this.definitionIdFor(token.Value!, this.packageName);
      }
    }
  }

  asApiViewDocument(): ApiViewDocument {
    return {
      name: this.name,
      packageName: this.packageName,
      tokens: null,
      apiForest: this.nodes,
      navigation: null,
      diagnostics: this.diagnostics,
      versionString: this.versionString,
      language: "TypeSpec",
      crossLanguagePackageId: this.crossLanguagePackageId,
    };
  }

  definitionIdFor(value: string, prefix: string): string | undefined {
    if (value.includes(".")) {
      const fullName = `${prefix}.${value}`;
      return this.typeDeclarations.has(fullName) ? fullName : undefined;
    }
    for (const item of this.typeDeclarations) {
      if (item.split(".").splice(-1)[0] === value) {
        return item;
      }
    }
    return undefined;
  }
}

export class NamespaceStack {
  stack = new Array<string>();

  push(val: string) {
    this.stack.push(val);
  }

  pop(): string | undefined {
    return this.stack.pop();
  }

  value(): string {
    return this.stack.join(".");
  }

  reset() {
    this.stack = Array<string>();
  }
}

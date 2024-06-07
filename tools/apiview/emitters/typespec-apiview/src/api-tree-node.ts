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
  IdentifierNode,
  InterfaceStatementNode,
  IntersectionExpressionNode,
  MemberExpressionNode,
  ModelExpressionNode,
  ModelPropertyNode,
  ModelSpreadPropertyNode,
  ModelStatementNode,
  NumericLiteralNode,
  OperationSignatureDeclarationNode,
  OperationSignatureReferenceNode,
  OperationStatementNode,
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
import { RenderClass, StructuredToken, TokenKind, TokenLocation, TokenOptions, TokenTag } from "./structured-token.js";
import {
  getFullyQualifiedIdentifier,
  definitionIdFor,
  getRawText,
  generateId,
  buildTemplateString,
} from "./helpers.js";
import { NamespaceModel } from "./namespace-model.js";
import { ApiView } from "./apiview.js";

const WHITESPACE = " ";

/** Tags supported by APIView v2 */
export enum NodeTag {
  /** Show item as deprecated. */
  deprecated,
  /** Hide item from APIView. */
  hidden,
  /** Hide item from APIView Navigation. */
  hideFromNav,
  /** Ignore differences in this item when calculating diffs. */
  skipDiff,
}

/** The kind values APIView supports for ApiTreeNodes. */
export enum NodeKind {
  assembly,
  class,
  delegate,
  enum,
  interface,
  method,
  namespace,
  package,
  struct,
  type,
}

/** Options when creating a new ApiTreeNode. */
export interface NodeOptions {
  tags?: NodeTag[];
  properties?: Map<string, string>;
}

/** New-style structured APIView node. */
export class ApiTreeNode {
  name: string;
  id: string;
  kind: NodeKind;
  tags?: Set<string>;
  properties: Map<string, string>;
  topTokens: StructuredToken[];
  bottomTokens: StructuredToken[];
  children: ApiTreeNode[];

  constructor(name: string, id: string, kind: NodeKind, options?: NodeOptions) {
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
   * Creates a new node on the parent.
   * @param parent the parent, which can be APIView itself for top-level nodes or another ApiTreeNode
   * @param name name of the node
   * @param id identifier of the node
   * @param options options for node creation
   * @returns the created node
   */
  node(parent: ApiTreeNode | ApiView, name: string, id: string, kind: NodeKind, options?: NodeOptions): ApiTreeNode {
    const child = new ApiTreeNode(name, id, kind, {
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
      this.token(TokenKind.lineBreak, { tags: [TokenTag.skipDiff], location: TokenLocation.bottom });
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

  /**
   *
   * @param value to render
   * @param options to configure the token.
   *  prefixSpace: ensure the value is preceded exactly one space. Default is false.
   *  postfixSpace: ensure the value is followed by exactly one space. Default is false.
   *  location: where to place the token in the node. Default is top.
   */
  punctuation(value: string, options?: { prefixSpace?: boolean; postfixSpace?: boolean; location?: TokenLocation }) {
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

  /**
   * Renders simple text.
   * @param text to render
   */
  text(text: string) {
    this.token(TokenKind.content, {
      value: text,
      renderClasses: [RenderClass.text],
    });
  }

  /**
   * Renders text as a language-specific keyword.
   * @param keyword to render
   * @param options to configure the token.
   *    prefixSpace: ensure the keyword is preceded exactly one space. Default is false.
   *    postfixSpace: ensure the keyword is followed by exactly one space. Default is false.
   */
  keyword(keyword: string, options?: { prefixSpace?: boolean; postfixSpace?: boolean }) {
    const prefixSpace = options?.prefixSpace ?? false;
    const postfixSpace = options?.postfixSpace ?? false;
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

  /**
   * Registers a new type that can be referenced later and renders the type name.
   * @param typeName to render
   * @param typeId the unique, deterministic identifier for the type registration
   * @param addCrossLanguageId whether to add a cross-language identifier
   */
  typeDeclaration(typeName: string, typeId: string | undefined, addCrossLanguageId: boolean) {
    if (typeId) {
      if (this.typeDeclarations.has(typeId)) {
        throw new Error(`Duplication ID "${typeId}" for declaration will result in bugs.`);
      }
      this.typeDeclarations.add(typeId);
    }
    this.token(TokenKind.content, {
      value: typeName,
      renderClasses: [RenderClass.typeName],
      lineId: typeId,
    });
  }

  /**
   * Renders a type reference that, when clicked, will navigate to the target type.
   * @param typeName to render
   * @param targetId the id of the target type for clickable linking
   */
  typeReference(typeName: string, targetId?: string) {
    this.token(TokenKind.content, {
      value: typeName,
      renderClasses: [RenderClass.typeName],
      lineId: targetId,
    });
  }

  /**
   * Renders a class member name.
   * @param name to render
   */
  member(name: string) {
    this.token(TokenKind.content, {
      value: name,
      renderClasses: [RenderClass.memberName],
    });
  }

  /**
   * Renders a value as a string literal, surrounded by double quotes.
   * @param value to render
   */
  stringLiteral(value: string) {
    const lines = value.split("\n");
    if (lines.length === 1) {
      this.token(TokenKind.content, {
        value: `\u0022${value}\u0022`,
        renderClasses: [RenderClass.stringLiteral],
      });
    } else {
      this.punctuation(`"""`);
      this.newline();
      for (const line of lines) {
        this.literal(line);
        this.newline();
      }
      this.punctuation(`"""`);
    }
  }

  /**
   * Renders a value as a literal.
   * @param value to render
   */
  literal(value: string) {
    this.topTokens.push(
      new StructuredToken(TokenKind.content, {
        renderClasses: [RenderClass.literal],
        value: value,
      }),
    );
  }

  // Special tokenize methods

  tokenize(node: BaseNode) {
    let obj;
    let isExpanded = false;
    switch (node.kind) {
      case SyntaxKind.AliasStatement:
        obj = node as AliasStatementNode;
        const aliasId = obj.id.sv;
        const aliasNode = this.node(this, aliasId, aliasId, NodeKind.type);
        aliasNode.keyword("alias", { postfixSpace: true });
        aliasNode.typeDeclaration(aliasId, aliasId, true);
        aliasNode.tokenizeTemplateParameters(obj.templateParameters);
        aliasNode.punctuation("=", { prefixSpace: true, postfixSpace: true });
        aliasNode.tokenize(obj.value);
        break;
      case SyntaxKind.ArrayExpression:
        obj = node as ArrayExpressionNode;
        this.tokenize(obj.elementType);
        this.punctuation("[]");
        break;
      case SyntaxKind.AugmentDecoratorStatement:
        obj = node as AugmentDecoratorStatementNode;
        const decoratorName = this.getNameForNode(obj.target);
        const decoratorNode = this.node(this, decoratorName, decoratorName, "decorator", {
          tags: [NodeTag.hideFromNav],
        });
        decoratorNode.punctuation("@@");
        decoratorNode.tokenizeIdentifier(obj.target, "keyword");
        if (obj.arguments.length) {
          const last = obj.arguments.length - 1;
          decoratorNode.punctuation("(");
          decoratorNode.tokenize(obj.targetType);
          if (obj.arguments.length) {
            decoratorNode.punctuation(",", { postfixSpace: true });
          }
          for (let x = 0; x < obj.arguments.length; x++) {
            const arg = obj.arguments[x];
            decoratorNode.tokenize(arg);
            if (x !== last) {
              decoratorNode.punctuation(",", { postfixSpace: true });
            }
          }
          decoratorNode.punctuation(")");
        }
        break;
      case SyntaxKind.BooleanLiteral:
        obj = node as BooleanLiteralNode;
        this.literal(obj.value.toString());
        break;
      case SyntaxKind.BlockComment:
        throw new Error(`Case "BlockComment" not implemented`);
      case SyntaxKind.TypeSpecScript:
        throw new Error(`Case "TypeSpecScript" not implemented`);
      case SyntaxKind.DecoratorExpression:
        obj = node as DecoratorExpressionNode;
        this.punctuation("@");
        this.tokenizeIdentifier(obj.target, "keyword");
        if (obj.arguments.length) {
          const last = obj.arguments.length - 1;
          this.punctuation("(");
          for (let x = 0; x < obj.arguments.length; x++) {
            const arg = obj.arguments[x];
            this.tokenize(arg);
            if (x !== last) {
              this.punctuation(",", { postfixSpace: true });
            }
          }
          this.punctuation(")");
        }
        break;
      case SyntaxKind.DirectiveExpression:
        obj = node as DirectiveExpressionNode;
        const nodeId = generateId(node)!;
        const directiveNode = this.node(this, nodeId, nodeId, "directive", { tags: [NodeTag.hideFromNav] });
        directiveNode.keyword(`#${obj.target.sv}`, { postfixSpace: true });
        for (const arg of obj.arguments) {
          switch (arg.kind) {
            case SyntaxKind.StringLiteral:
              directiveNode.stringLiteral(arg.value);
              directiveNode.space();
              break;
            case SyntaxKind.Identifier:
              directiveNode.stringLiteral(arg.sv);
              directiveNode.space();
              break;
          }
        }
        directiveNode.newline();
        break;
      case SyntaxKind.EmptyStatement:
        throw new Error(`Case "EmptyStatement" not implemented`);
      case SyntaxKind.EnumMember:
        obj = node as EnumMemberNode;
        this.tokenizeDecoratorsAndDirectives(obj.decorators, obj.directives, false);
        this.tokenizeIdentifier(obj.id, "member");
        if (obj.value) {
          this.punctuation(":", { postfixSpace: true });
          this.tokenize(obj.value);
        }
        break;
      case SyntaxKind.EnumSpreadMember:
        obj = node as EnumSpreadMemberNode;
        this.punctuation("...");
        this.tokenize(obj.target);
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
        this.typeReference(obj.sv, obj.sv);
        break;
      case SyntaxKind.ImportStatement:
        throw new Error(`Case "ImportStatement" not implemented`);
      case SyntaxKind.IntersectionExpression:
        obj = node as IntersectionExpressionNode;
        for (let x = 0; x < obj.options.length; x++) {
          const opt = obj.options[x];
          this.tokenize(opt);
          if (x !== obj.options.length - 1) {
            this.punctuation("&", { prefixSpace: true, postfixSpace: true });
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
        this.punctuation("...");
        this.tokenize(obj.target);
        break;
      case SyntaxKind.ModelStatement:
        obj = node as ModelStatementNode;
        this.tokenizeModelStatement(obj);
        break;
      case SyntaxKind.NamespaceStatement:
        throw new Error(`Case "NamespaceStatement" not implemented`);
      case SyntaxKind.NeverKeyword:
        this.keyword("never", { prefixSpace: true, postfixSpace: true });
        break;
      case SyntaxKind.NumericLiteral:
        obj = node as NumericLiteralNode;
        this.literal(obj.value.toString());
        break;
      case SyntaxKind.OperationStatement:
        this.tokenizeOperationStatement(node as OperationStatementNode);
        break;
      case SyntaxKind.OperationSignatureDeclaration:
        obj = node as OperationSignatureDeclarationNode;
        this.punctuation("(");
        // TODO: heuristic for whether operation signature should be inlined or not.
        const inline = false;
        this.tokenizeModelExpression(obj.parameters, true, inline);
        this.punctuation("):", { postfixSpace: true });
        this.tokenizeReturnType(obj, inline);
        break;
      case SyntaxKind.OperationSignatureReference:
        obj = node as OperationSignatureReferenceNode;
        this.keyword("is", { prefixSpace: true, postfixSpace: true });
        this.tokenize(obj.baseOperation);
        break;
      case SyntaxKind.Return:
        throw new Error(`Case "Return" not implemented`);
      case SyntaxKind.StringLiteral:
        obj = node as StringLiteralNode;
        this.stringLiteral(obj.value);
        break;
      case SyntaxKind.ScalarStatement:
        this.tokenizeScalarStatement(node as ScalarStatementNode);
        break;
      case SyntaxKind.TemplateParameterDeclaration:
        obj = node as TemplateParameterDeclarationNode;
        this.tokenize(obj.id);
        if (obj.constraint) {
          this.keyword("extends", { prefixSpace: true, postfixSpace: true });
          this.tokenize(obj.constraint);
        }
        if (obj.default) {
          this.punctuation("=", { prefixSpace: true, postfixSpace: true });
          this.tokenize(obj.default);
        }
        break;
      case SyntaxKind.TupleExpression:
        obj = node as TupleExpressionNode;
        this.punctuation("[", { prefixSpace: true, postfixSpace: true });
        for (let x = 0; x < obj.values.length; x++) {
          const val = obj.values[x];
          this.tokenize(val);
          if (x !== obj.values.length - 1) {
            this.punctuation(",", { postfixSpace: true });
          }
        }
        this.punctuation("]", { postfixSpace: true });
        break;
      case SyntaxKind.TypeReference:
        obj = node as TypeReferenceNode;
        isExpanded = this.isTemplateExpanded(obj);
        this.tokenizeIdentifier(obj.target, "reference");
        // Render the template parameter instantiations
        if (obj.arguments.length) {
          this.punctuation("<");
          if (isExpanded) {
            this.newline();
          }
          for (let x = 0; x < obj.arguments.length; x++) {
            const arg = obj.arguments[x];
            this.tokenize(arg);
            if (x !== obj.arguments.length - 1) {
              this.punctuation(",", { postfixSpace: true });
              if (isExpanded) {
                this.newline();
              }
            }
          }
          if (isExpanded) {
            this.newline();
          }
          this.punctuation(">");
        }
        break;
      case SyntaxKind.UnionExpression:
        obj = node as UnionExpressionNode;
        for (let x = 0; x < obj.options.length; x++) {
          const opt = obj.options[x];
          this.tokenize(opt);
          if (x !== obj.options.length - 1) {
            this.punctuation("|", { prefixSpace: true, postfixSpace: true });
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
        this.keyword("any", { prefixSpace: true, postfixSpace: true });
        break;
      case SyntaxKind.UsingStatement:
        throw new Error(`Case "UsingStatement" not implemented`);
      case SyntaxKind.ValueOfExpression:
        this.keyword("valueof", { prefixSpace: true, postfixSpace: true });
        this.tokenize((node as ValueOfExpressionNode).target);
        break;
      case SyntaxKind.VoidKeyword:
        this.keyword("void", { prefixSpace: true });
        break;
      case SyntaxKind.TemplateArgument:
        obj = node as TemplateArgumentNode;
        isExpanded = obj.argument.kind === SyntaxKind.ModelExpression && obj.argument.properties.length > 0;
        if (isExpanded) {
          this.blankLines(0);
        }
        if (obj.name) {
          this.text(obj.name.sv);
          this.punctuation("=", { prefixSpace: true, postfixSpace: true });
        }
        if (isExpanded) {
          this.tokenizeModelExpressionExpanded(obj.argument as ModelExpressionNode, false, false);
        } else {
          this.tokenize(obj.argument);
        }
        break;
      case SyntaxKind.StringTemplateExpression:
        obj = node as StringTemplateExpressionNode;
        const stringValue = buildTemplateString(obj);
        const multiLine = stringValue.includes("\n");
        // single line case
        if (!multiLine) {
          this.stringLiteral(stringValue);
          break;
        }
        // otherwise multiline case
        const lines = stringValue.split("\n");
        this.punctuation(`"""`);
        this.newline();
        for (const line of lines) {
          this.literal(line);
          this.newline();
        }
        this.punctuation(`"""`);
        break;
      case SyntaxKind.StringTemplateSpan:
        obj = node as StringTemplateSpanNode;
        this.punctuation("${");
        this.tokenize(obj.expression);
        this.punctuation("}");
        this.tokenize(obj.literal);
        break;
      case SyntaxKind.StringTemplateHead:
      case SyntaxKind.StringTemplateMiddle:
      case SyntaxKind.StringTemplateTail:
        obj = node as StringTemplateHeadNode;
        this.literal(obj.value);
        break;
      default:
        // All Projection* cases should fail here...
        throw new Error(`Case "${SyntaxKind[node.kind].toString()}" not implemented`);
    }
  }

  /**
   * Tokenize a TypeSpec identifier.
   */
  tokenizeIdentifier(
    node: IdentifierNode | MemberExpressionNode | StringLiteralNode,
    style: "declaration" | "reference" | "member" | "keyword",
  ) {
    switch (node.kind) {
      case SyntaxKind.MemberExpression:
        const defId = getFullyQualifiedIdentifier(node, node.id.sv);
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
            this.typeDeclaration(node.sv, node.sv, true);
            break;
          case "reference":
            const defId = definitionIdFor(node.sv, this.packageName);
            this.typeReference(node.sv, defId);
            break;
          case "member":
            this.member(getRawText(node));
            break;
          case "keyword":
            this.keyword(node.sv);
            break;
        }
    }
  }

  /**
   * Returns the text rendering for a specified number of tokens.
   */
  peekTokens(count: number = 20): string {
    let result = "";
    const tokens = this.topTokens.slice(-count);
    for (const token of tokens) {
      if (token.value) {
        result += token.value;
      }
    }
    return result;
  }

  private tokenizeModelStatement(node: ModelStatementNode) {
    const nodeId = node.id.sv;
    const modelNode = this.node(this, nodeId, nodeId, NodeKind.class);
    modelNode.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    modelNode.keyword("model", { postfixSpace: true });
    modelNode.tokenizeIdentifier(node.id, "declaration");
    if (node.extends) {
      modelNode.keyword("extends", { prefixSpace: true, postfixSpace: true });
      modelNode.tokenize(node.extends);
    }
    if (node.is) {
      modelNode.keyword("is", { prefixSpace: true, postfixSpace: true });
      modelNode.tokenize(node.is);
    }
    modelNode.tokenizeTemplateParameters(node.templateParameters);
    if (node.properties.length) {
      for (const prop of node.properties) {
        const propName = this.getNameForNode(prop);
        const propNode = modelNode.node(modelNode, propName, propName, "member");
        propNode.tokenize(prop);
        propNode.punctuation(";");
        propNode.blankLines(0);
      }
    } else {
      modelNode.punctuation("{}", { postfixSpace: false });
    }
  }

  private tokenizeScalarStatement(node: ScalarStatementNode) {
    const nodeId = node.id.sv;
    const scalarNode = this.node(this, nodeId, nodeId, NodeKind.type);
    scalarNode.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    scalarNode.keyword("scalar", { postfixSpace: true });
    scalarNode.tokenizeIdentifier(node.id, "declaration");
    if (node.extends) {
      scalarNode.keyword("extends", { prefixSpace: true, postfixSpace: true });
      scalarNode.tokenize(node.extends);
    }
    scalarNode.tokenizeTemplateParameters(node.templateParameters);
    scalarNode.blankLines(0);
  }

  private tokenizeInterfaceStatement(node: InterfaceStatementNode) {
    const nodeId = node.id.sv;
    const interfaceNode = this.node(this, nodeId, nodeId, NodeKind.interface);
    interfaceNode.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    interfaceNode.keyword("interface", { postfixSpace: true });
    interfaceNode.tokenizeIdentifier(node.id, "declaration");
    interfaceNode.tokenizeTemplateParameters(node.templateParameters);
    for (let x = 0; x < node.operations.length; x++) {
      const op = node.operations[x];
      const opId = generateId(op)!;
      const opNode = interfaceNode.node(interfaceNode, opId, opId, NodeKind.method);
      opNode.tokenizeOperationStatement(op, true);
      interfaceNode.blankLines(x !== node.operations.length - 1 ? 1 : 0);
    }
  }

  private tokenizeEnumStatement(node: EnumStatementNode) {
    const nodeId = node.id.sv;
    const enumNode = this.node(this, nodeId, nodeId, NodeKind.enum);
    enumNode.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    enumNode.keyword("enum", { postfixSpace: true });
    enumNode.tokenizeIdentifier(node.id, "declaration");
    for (const member of node.members) {
      const memberName = this.getNameForNode(member);
      const memberNode = enumNode.node(enumNode, memberName, memberName, "member");
      memberNode.tokenize(member);
      memberNode.punctuation(",");
      memberNode.blankLines(0);
    }
  }

  private tokenizeUnionStatement(node: UnionStatementNode) {
    const nodeId = node.id.sv;
    const unionNode = this.node(this, nodeId, nodeId, NodeKind.enum);
    unionNode.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    unionNode.keyword("union", { postfixSpace: true });
    unionNode.tokenizeIdentifier(node.id, "declaration");
    for (let x = 0; x < node.options.length; x++) {
      const variant = node.options[x];
      const variantName = this.getNameForNode(variant);
      const variantNode = unionNode.node(unionNode, variantName, variantName, "member");
      variantNode.tokenize(variant);
      if (x !== node.options.length - 1) {
        variantNode.punctuation(",");
      }
      variantNode.blankLines(0);
    }
  }

  private tokenizeUnionVariant(node: UnionVariantNode) {
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    if (node.id !== undefined) {
      this.tokenizeIdentifier(node.id, "member");
      this.punctuation(":", { postfixSpace: true });
    }
    this.tokenize(node.value);
  }

  private tokenizeModelProperty(node: ModelPropertyNode, inline: boolean) {
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, inline);
    this.tokenizeIdentifier(node.id, "member");
    this.punctuation(node.optional ? "?:" : ":", { postfixSpace: true });
    this.tokenize(node.value);
    if (node.default) {
      this.punctuation("=", { prefixSpace: true, postfixSpace: true });
      this.tokenize(node.default);
    }
  }

  private tokenizeModelExpressionInline(node: ModelExpressionNode, isOperationSignature: boolean) {
    if (node.properties.length) {
      if (!isOperationSignature) {
        this.punctuation("{", { prefixSpace: true, postfixSpace: true });
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
            this.punctuation(",", { postfixSpace: true });
          }
        } else {
          this.punctuation(";");
        }
      }
      if (!isOperationSignature) {
        this.punctuation("}", { prefixSpace: true, postfixSpace: true });
      }
    }
  }

  private tokenizeModelExpressionExpanded(
    node: ModelExpressionNode,
    isOperationSignature: boolean,
    leadingNewline: boolean,
  ) {
    const modelNode = this.node(this, "anonymous", "anonymous", NodeKind.class);
    if (node.properties.length) {
      if (leadingNewline) {
        modelNode.blankLines(0);
      }
      if (!isOperationSignature) {
        modelNode.punctuation("{");
        modelNode.blankLines(0);
      }
      for (let x = 0; x < node.properties.length; x++) {
        const prop = node.properties[x];
        const propName = this.getNameForNode(prop);
        const propNode = modelNode.node(modelNode, propName, propName, "modelProperty", {
          tags: [NodeTag.hideFromNav],
        });
        switch (prop.kind) {
          case SyntaxKind.ModelProperty:
            propNode.tokenizeModelProperty(prop, false);
            break;
          case SyntaxKind.ModelSpreadProperty:
            propNode.tokenize(prop);
        }
        if (isOperationSignature) {
          if (x !== node.properties.length - 1) {
            propNode.punctuation(",", { postfixSpace: true });
          }
        } else {
          propNode.punctuation(";", { postfixSpace: true });
        }
        propNode.blankLines(0);
      }
      modelNode.blankLines(0);
      if (!isOperationSignature) {
        modelNode.punctuation("}");
        modelNode.blankLines(0);
      }
    } else if (!isOperationSignature) {
      modelNode.punctuation("{}", { prefixSpace: true });
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
    const nodeId = node.id.sv;
    const opNode = this.node(this, nodeId, nodeId, NodeKind.method);
    opNode.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    if (!suppressOpKeyword) {
      opNode.keyword("op", { postfixSpace: true });
    }
    opNode.tokenizeIdentifier(node.id, "declaration");
    opNode.tokenizeTemplateParameters(node.templateParameters);
    opNode.tokenize(node.signature);
    opNode.punctuation(";");
  }

  tokenizeDecoratorsAndDirectives(
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
      while (this.topTokens.length) {
        const item = this.topTokens.pop()!;
        if (item.Kind === ApiViewTokenKind.LineIdMarker && item.DefinitionId === "GLOBAL") {
          this.topTokens.push(item);
          this.blankLines(2);
          break;
        } else if ([ApiViewTokenKind.Punctuation, ApiViewTokenKind.TypeName].includes(item.Kind)) {
          this.topTokens.push(item);
          // for now, render with no newlines, per stewardship board request
          const lineCount = ["{", "("].includes(item.value!) ? 0 : 0;
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
        this.topTokens.push({
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
        this.topTokens.push({
          Kind: ApiViewTokenKind.DocumentRangeEnd,
        });
      }
    }
  }

  private isTemplateExpanded(node: TypeReferenceNode): boolean {
    if (node.arguments.length === 0) {
      return false;
    }
    const first = node.arguments[0];
    return first.argument.kind === SyntaxKind.ModelExpression;
  }

  private tokenizeReturnType(node: OperationSignatureDeclarationNode, inline: boolean) {
    this.tokenize(node.returnType);
  }

  private getNameForNode(node: BaseNode | NamespaceModel): string {
    const id = generateId(node);
    if (id) {
      return id.split(".").splice(-1)[0];
    } else {
      throw new Error("Unable to get name for node.");
    }
  }

  private tokenizeTemplateParameters(nodes: readonly TemplateParameterDeclarationNode[], isExpanded: boolean = false) {
    if (nodes.length) {
      this.punctuation("<");
      for (let x = 0; x < nodes.length; x++) {
        const param = nodes[x];
        this.tokenize(param);
        if (x !== nodes.length - 1) {
          this.punctuation(",", { postfixSpace: true });
          this.space();
        }
        if (isExpanded) {
          this.newline();
        }
      }
      this.punctuation(">");
    }
  }
}

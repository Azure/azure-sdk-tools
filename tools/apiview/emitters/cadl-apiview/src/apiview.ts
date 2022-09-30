import {
  ArrayExpressionNode,
  BaseNode,
  BooleanLiteralNode,
  DecoratorExpressionNode,
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
  Namespace,
  navigateProgram,
  NumericLiteralNode,
  OperationSignatureDeclarationNode,
  OperationSignatureReferenceNode,
  OperationStatementNode,
  Program,
  StringLiteralNode,
  SyntaxKind,
  TupleExpressionNode,
  TypeReferenceNode,
  UnionExpressionNode,
  UnionStatementNode,
  UnionVariantNode,
} from "@cadl-lang/compiler";
import { ApiViewDiagnostic, ApiViewDiagnosticLevel } from "./diagnostic.js";
import { ApiViewNavigation } from "./navigation.js";
import { generateId, NamespaceModel } from "./namespace-model.js";
import { LIB_VERSION } from "./version.js";

const WHITESPACE = " ";

export const enum ApiViewTokenKind {
  Text = 0,
  Newline = 1,
  Whitespace = 2,
  Punctuation = 3,
  Keyword = 4,
  LineIdMarker = 5, // use this if there are no visible tokens with ID on the line but you still want to be able to leave a comment for it
  TypeName = 6,
  MemberName = 7,
  StringLiteral = 8,
  Literal = 9,
  Comment = 10,
  DocumentRangeStart = 11,
  DocumentRangeEnd = 12,
  DeprecatedRangeStart = 13,
  DeprecatedRangeEnd = 14,
  SkipDiffRangeStart = 15,
  SkipDiffRangeEnd = 16
}

export interface ApiViewToken {
  Kind: ApiViewTokenKind;
  Value?: string;
  DefinitionId?: string;
  NavigateToId?: string;
  CrossLanguageDefinitionId?: string;
}

export interface ApiViewDocument {
  Name: string;
  PackageName: string;
  Tokens: ApiViewToken[];
  Navigation: ApiViewNavigation[];
  Diagnostics: ApiViewDiagnostic[];
  VersionString: string;
  Language: string;
}

export class ApiView {
  name: string;
  packageName: string;
  tokens: ApiViewToken[] = [];
  navigationItems: ApiViewNavigation[] = [];
  diagnostics: ApiViewDiagnostic[] = [];
  versionString: string;

  indentString: string = "";
  indentSize: number = 2;

  constructor(name: string, packageName: string, versionString: string) {
    this.name = name;
    this.packageName = packageName;
    this.versionString = versionString;

    this.emitHeader();
  }

  token(kind: ApiViewTokenKind, value?: string, lineId?: string, navigateToId?: string) {
    this.tokens.push({
      Kind: kind,
      Value: value,
      DefinitionId: lineId,
      NavigateToId: navigateToId,
    });
  }

  indent() {
    this.trim();
    this.indentString = WHITESPACE.repeat(this.indentString.length + this.indentSize);
    this.tokens.push({ Kind: ApiViewTokenKind.Whitespace, Value: this.indentString });
  }

  deindent() {
    this.trim();
    this.indentString = WHITESPACE.repeat(this.indentString.length - this.indentSize);
    this.tokens.push({ Kind: ApiViewTokenKind.Whitespace, Value: this.indentString });
  }

  trim() {
    let last = this.tokens[this.tokens.length - 1]
    while (last) {
      if (last.Kind == ApiViewTokenKind.Whitespace) {
        this.tokens.pop();
        last = this.tokens[this.tokens.length - 1];
      } else {
        return;
      }
    }
  }

  beginGroup() {
    this.punctuation("{", true, false);
    this.blankLines(0);
    this.indent();
  }

  endGroup() {
    this.blankLines(0);
    this.deindent();
    this.punctuation("}");
  }

  whitespace(count: number = 1) {
    this.tokens.push({
      Kind: ApiViewTokenKind.Whitespace,
      Value: WHITESPACE.repeat(count),
    });
  }

  space() {
    if (this.tokens[this.tokens.length - 1]?.Kind != ApiViewTokenKind.Whitespace) {
      this.tokens.push({
        Kind: ApiViewTokenKind.Whitespace,
        Value: WHITESPACE,
      });
    }
  }

  newline() {
    this.trim();
    this.tokens.push({
      Kind: ApiViewTokenKind.Newline,
    });
    this.tokens.push({ Kind: ApiViewTokenKind.Whitespace, Value: this.indentString });
  }

  blankLines(count: number) {
    // count the number of trailing newlines (ignoring indent whitespace)
    let newlineCount: number = 0;
    for (let i = this.tokens.length; i > 0; i--) {
      const token = this.tokens[i - 1];
      if (token.Kind == ApiViewTokenKind.Newline) {
        newlineCount++;
      } else if (token.Kind == ApiViewTokenKind.Whitespace) {
        continue;
      } else {
        break;
      }
    }
    if (newlineCount < count + 1) {
      // if there aren't new enough newlines, add some
      const toAdd = count + 1 - newlineCount;
      for (let i = 0; i < toAdd; i++) {
        this.newline();
      }
    } else if (newlineCount > count + 1) {
      // if there are too many newlines, remove some
      let toRemove = newlineCount - (count + 1);
      while (toRemove) {
        const popped = this.tokens.pop();
        if (popped?.Kind == ApiViewTokenKind.Newline) {
          toRemove--;
        }
      }
    }
  }

  punctuation(value: string, prefixSpace: boolean = false, postfixSpace: boolean = false) {
    if (prefixSpace) {
      this.space();
    }
    this.tokens.push({
      Kind: ApiViewTokenKind.Punctuation,
      Value: value,
    });
    if (postfixSpace) {
      this.space();
    }
  }

  lineMarker() {
    this.tokens.push({
      Kind: ApiViewTokenKind.LineIdMarker,
      DefinitionId: NamespaceStack.value(),
    });
  }

  text(text: string, addCrossLanguageId: boolean = false) {
    const token = {
      Kind: ApiViewTokenKind.Text,
      Value: text,
    };
    // TODO: Cross-language definition ID
    this.tokens.push(token);
  }

  keyword(keyword: string, prefixSpace: boolean = false, postfixSpace: boolean = false) {
    if (prefixSpace) {
      this.space();
    }
    this.tokens.push({
      Kind: ApiViewTokenKind.Keyword,
      Value: keyword,
    });
    if (postfixSpace) {
      this.space();
    }
  }

  typeDeclaration(typeName: string, typeId: string | undefined) {
    if (typeId) {
      if (typeDeclarations.has(typeId)) {
        throw new Error(`Duplication ID "${typeId}" for declaration will result in bugs.`);
      }
      typeDeclarations.add(typeId);
    }
    this.tokens.push({
      Kind: ApiViewTokenKind.TypeName,
      DefinitionId: typeId,
      Value: typeName,
    });
  }

  typeReference(typeName: string, targetId?: string) {
    this.tokens.push({
      Kind: ApiViewTokenKind.TypeName,
      Value: typeName,
      NavigateToId: targetId ?? "__MISSING__",
    });
  }

  member(name: string) {
    this.tokens.push({
      Kind: ApiViewTokenKind.MemberName,
      Value: name,
    });
  }

  stringLiteral(value: string) {
    this.tokens.push({
      Kind: ApiViewTokenKind.StringLiteral,
      Value: `\u0022${value}\u0022`,
    });
  }

  literal(value: string) {
    this.tokens.push({
      Kind: ApiViewTokenKind.StringLiteral,
      Value: value,
    });
  }

  diagnostic(message: string, targetId: string, level: ApiViewDiagnosticLevel) {
    this.diagnostics.push(new ApiViewDiagnostic(message, targetId, level));
  }

  navigation(item: ApiViewNavigation) {
    this.navigationItems.push(item);
  }

  emit(program: Program) {
    let allNamespaces = new Map<string, Namespace>();

    // collect namespaces in program
    navigateProgram(program, {
      namespace(obj) {
        const name = program.checker.getNamespaceString(obj);
        allNamespaces.set(name, obj);
      },
    });
    allNamespaces = new Map([...allNamespaces].sort());

    // Skip namespaces which are outside the root namespace.
    for (const [name, ns] of allNamespaces.entries()) {
      if (!name.startsWith(this.packageName)) {
        continue;
      }
      const nsModel = new NamespaceModel(name, ns);
      this.tokenizeNamespaceModel(nsModel);
      this.buildNavigation(nsModel);
    }
  }

  private emitHeader() {
    const toolVersion = LIB_VERSION;
    const headerText = `// Package parsed using @azure-tools/cadl-apiview (version:${toolVersion})`;
    this.token(ApiViewTokenKind.SkipDiffRangeStart);
    this.literal(headerText);
    NamespaceStack.push("GLOBAL");
    this.lineMarker();
    NamespaceStack.pop();
    // TODO: Source URL?
    this.token(ApiViewTokenKind.SkipDiffRangeEnd);
    this.blankLines(2);
  }

  tokenize(node: BaseNode) {
    let obj;
    switch (node.kind) {
      case SyntaxKind.AliasStatement:
        throw new Error(`Case "AliasStatement" not implemented`);
      case SyntaxKind.ArrayExpression:
        obj = node as ArrayExpressionNode;
        this.typeReference("Array");
        this.punctuation("<");
        this.tokenize(obj.elementType);
        this.punctuation(">");
        break;
      case SyntaxKind.BooleanLiteral:
        obj = node as BooleanLiteralNode;
        this.literal(obj.value.toString());
        break;
      case SyntaxKind.BlockComment:
        throw new Error(`Case "BlockComment" not implemented`);
      case SyntaxKind.CadlScript:
        throw new Error(`Case "CadlScript" not implemented`);
      case SyntaxKind.DecoratorExpression:
        obj = node as DecoratorExpressionNode;
        this.punctuation("@", false, false);
        this.tokenizeIdentifer(obj.target, "member");
        if (obj.arguments.length) {
          const last = obj.arguments.length - 1;
          this.punctuation("(", false, false);
          for (let x = 0; x < obj.arguments.length; x++) {
            const arg = obj.arguments[x];
            this.tokenize(arg);
            if (x != last) {
              this.punctuation(",", false, true);
            }
          }
          this.punctuation(")", false, false);
        }
        break;
      case SyntaxKind.DirectiveExpression:
        throw new Error(`Case "DirectiveExpression" not implemented`);
      case SyntaxKind.EmptyStatement:
        throw new Error(`Case "EmptyStatement" not implemented`);
      case SyntaxKind.EnumMember:
        obj = node as EnumMemberNode;
        this.tokenizeDecorators(obj.decorators, false);
        this.tokenizeIdentifer(obj.id, "member");
        if (obj.value != undefined) {
          this.punctuation(":", false, true);
          this.tokenize(obj.value);
        }
        break;
      case SyntaxKind.EnumSpreadMember:
        obj = node as EnumSpreadMemberNode;
        this.punctuation("...", false, false);
        this.tokenize(obj.target);
        break;
      case SyntaxKind.EnumStatement:
        this.tokenizeEnumStatement(node as EnumStatementNode);
        break;
      case SyntaxKind.JsSourceFile:
        throw new Error(`Case "JsSourceFile" not implemented`);
      case SyntaxKind.Identifier:
        obj = node as IdentifierNode;
        const id = NamespaceStack.value();
        this.typeReference(obj.sv, id);
        break;
      case SyntaxKind.ImportStatement:
        throw new Error(`Case "ImportStatement" not implemented`);
      case SyntaxKind.IntersectionExpression:
        obj = node as IntersectionExpressionNode;
        for (let x = 0; x < obj.options.length; x++) {
          const opt = obj.options[x];
          this.tokenize(opt);
          if (x != obj.options.length - 1) {
            this.punctuation("&", true, true);
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
        this.tokenizeIdentifer(node as MemberExpressionNode, "reference");
        break;
      case SyntaxKind.ModelExpression:
        this.tokenizeModelExpression(node as ModelExpressionNode, true, false);
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
        this.keyword("never", true, true);
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
        this.punctuation("(", false, false);
        this.tokenizeModelExpression(obj.parameters, false, true);
        this.punctuation("):", false, true);
        this.tokenize(obj.returnType);
        break;
      case SyntaxKind.OperationSignatureReference:
        obj = node as OperationSignatureReferenceNode;
        this.keyword("is", true, true);
        this.tokenize(obj.baseOperation);
        break;
      case SyntaxKind.Return:
        throw new Error(`Case "Return" not implemented`);
      case SyntaxKind.StringLiteral:
        obj = node as StringLiteralNode;
        this.stringLiteral(obj.value);
        break;
      case SyntaxKind.TemplateParameterDeclaration:
        throw new Error(`Case "TemplateParameter" not implemented`);
      case SyntaxKind.TupleExpression:
        obj = node as TupleExpressionNode;
        this.punctuation("[", true, true);
        for (let x = 0; x < obj.values.length; x++) {
          const val = obj.values[x];
          this.tokenize(val);
          if (x != obj.values.length - 1) {
            this.punctuation(",", false, true);
          }
        }
        this.punctuation("]", true, false);
        break;
      case SyntaxKind.TypeReference:
        obj = node as TypeReferenceNode;
        this.tokenizeIdentifer(obj.target, "reference");
        if (obj.arguments.length) {
          this.punctuation("<", false, false);  
          for (let x = 0; x < obj.arguments.length; x++) {
            const arg = obj.arguments[x];
            this.tokenize(arg);
            if (x != obj.arguments.length - 1) {
              this.punctuation(",", false, true);
            }
          }
          this.punctuation(">");
        }
        break;
      case SyntaxKind.UnionExpression:
        obj = node as UnionExpressionNode;
        for (let x = 0; x < obj.options.length; x++) {
          const opt = obj.options[x];
          this.tokenize(opt);
          if (x != obj.options.length -1) {
            this.punctuation("|", true, true);
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
        this.keyword("any", true, true);
        break;
      case SyntaxKind.UsingStatement:
        throw new Error(`Case "UsingStatement" not implemented`);
      case SyntaxKind.VoidKeyword:
        this.keyword("void", true, true);
        break;
      default:
        // All Projection* cases should fall in here...
        throw new Error(`Case "${node.kind.toString()}" not implemented`);
    }
  }

  private tokenizeModelStatement(node: ModelStatementNode) {
    this.tokenizeDecorators(node.decorators, false);
    this.keyword("model", false, true);
    NamespaceStack.push(node.id.sv);
    this.tokenizeIdentifer(node.id, "declaration");
    this.lineMarker();
    if (node.extends != undefined) {
      this.keyword("extends", true, true);
      this.tokenize(node.extends);
    }
    if (node.is != undefined) {
      this.keyword("is", true, true);
      this.tokenize(node.is);
    }
    if (node.properties.length) {
      this.beginGroup();
      for (const prop of node.properties) {
        const propName = this.getNameForNode(prop);
        NamespaceStack.push(propName);
        this.tokenize(prop);
        this.punctuation(";", false, false);
        this.lineMarker();
        NamespaceStack.pop();
        this.blankLines(0);
      }
      this.endGroup();
    } else {
      this.punctuation("{}", true, false);
    }
    NamespaceStack.pop();
  }

  private tokenizeInterfaceStatement(node: InterfaceStatementNode) {
    this.tokenizeDecorators(node.decorators, false);
    this.keyword("interface", false, true);
    NamespaceStack.push(node.id.sv);
    this.tokenizeIdentifer(node.id, "declaration");
    this.lineMarker();
    this.beginGroup();
    for (let x = 0; x < node.operations.length; x++) {
      const op = node.operations[x];
      this.tokenizeOperationStatement(op, true);
      this.blankLines((x != node.operations.length -1) ? 1 : 0);
    }
    this.endGroup();
    NamespaceStack.pop();
  }

  private tokenizeEnumStatement(node: EnumStatementNode) {
    this.tokenizeDecorators(node.decorators, false);
    this.keyword("enum", false, true);
    NamespaceStack.push(node.id.sv);
    this.tokenizeIdentifer(node.id, "declaration");
    this.lineMarker();
    this.beginGroup();
    for (const member of node.members) {
      const memberName = this.getNameForNode(member);
      NamespaceStack.push(memberName);
      this.tokenize(member);
      this.punctuation(",");
      this.lineMarker();
      NamespaceStack.pop();
      this.blankLines(0);
    }
    this.endGroup();
    NamespaceStack.pop();
  }

  private tokenizeUnionStatement(node: UnionStatementNode) {
    this.tokenizeDecorators(node.decorators, false);
    this.keyword("union", false, true);
    NamespaceStack.push(node.id.sv);
    this.tokenizeIdentifer(node.id, "declaration");
    this.lineMarker();
    this.beginGroup();
    for (let x = 0; x < node.options.length; x++) {
      const variant = node.options[x];
      const variantName = this.getNameForNode(node);
      NamespaceStack.push(variantName);
      this.tokenize(variant);
      this.lineMarker();
      NamespaceStack.pop();
      if (x != node.options.length - 1) {
        this.punctuation(",");
      }
      this.blankLines(0);
    }
    NamespaceStack.pop();
    this.endGroup();
  }

  private tokenizeUnionVariant(node: UnionVariantNode) {
    this.tokenizeDecorators(node.decorators, false);
    this.tokenizeIdentifer(node.id, "member");
    this.punctuation(":", false, true);
    this.tokenize(node.value);
  }

  private tokenizeModelProperty(node: ModelPropertyNode, inline: boolean) {
    this.tokenizeDecorators(node.decorators, inline);
    this.tokenizeIdentifer(node.id, "member");
    this.punctuation(node.optional ? "?:" : ":", false, true);
    this.tokenize(node.value);
    if (node.default != undefined) {
      this.tokenize(node.default);
    }
  }

  private tokenizeModelExpressionInline(node: ModelExpressionNode, displayBrackets: boolean) {
    if (node.properties.length) {
      if (displayBrackets) {
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
        if (x != node.properties.length - 1) {
          this.punctuation(",", false, true);
        }
      }
      if (displayBrackets) {
        this.punctuation("}", true, true);
      }
    }
  }

  private tokenizeModelExpressionExpanded(node: ModelExpressionNode, displayBrackets: boolean) {
    // FIXME: Needs line IDs for each property.
    if (node.properties.length) {
      this.blankLines(0);
      this.indent();
      this.punctuation("{", false, false);
      this.blankLines(0);
      this.indent();
      NamespaceStack.push("anonymous");
      for (let x = 0; x < node.properties.length; x++) {
        const prop = node.properties[x];
        const propName = this.getNameForNode(prop);
        NamespaceStack.push(propName);
        this.lineMarker();
        switch (prop.kind) {
          case SyntaxKind.ModelProperty:
            this.tokenizeModelProperty(prop, false);
            break;
          case SyntaxKind.ModelSpreadProperty:
            this.tokenize(prop);
        }
        NamespaceStack.pop();
        if (x != node.properties.length - 1) {
          this.punctuation(",", false, false);
        }
        this.blankLines(0);
      }
      NamespaceStack.pop();
      this.blankLines(0);
      this.deindent();
      this.punctuation("}", false, false);
      this.blankLines(0);
      this.trim();
      this.deindent();
    } else {
      this.punctuation("{}", true, false);
    }
  }

  private tokenizeModelExpression(
    node: ModelExpressionNode,
    displayBrackets: boolean,
    inline: boolean
  ) {
    if (inline) {
      this.tokenizeModelExpressionInline(node, displayBrackets)
    } else {
      this.tokenizeModelExpressionExpanded(node, displayBrackets)
    }
  }

  private tokenizeOperationStatement(node: OperationStatementNode, suppressOpKeyword: boolean = false) {
    this.tokenizeDecorators(node.decorators, false);
    if (!suppressOpKeyword) {
      this.keyword("op", false, true);
    }
    NamespaceStack.push(node.id.sv);
    this.tokenizeIdentifer(node.id, "declaration");
    this.lineMarker();
    this.tokenize(node.signature);
    this.punctuation(";", false, false);
    NamespaceStack.pop();
  }

  private tokenizeNamespaceModel(model: NamespaceModel) {
    this.tokenizeDecorators(model.node.decorators, false);
    this.keyword("namespace", false, true);
    NamespaceStack.push(model.name);
    this.typeDeclaration(model.name, NamespaceStack.value());
    this.lineMarker();
    this.beginGroup();
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
    this.endGroup();
    NamespaceStack.pop();
  }

  private filterDecorators(nodes: readonly DecoratorExpressionNode[]): DecoratorExpressionNode[] {
    const filterOut = ["doc", "summary", "example"];
    const filtered = Array<DecoratorExpressionNode>();
    for (const node of nodes) {
      if (filterOut.includes((node.target as IdentifierNode).sv)) {
        continue;
      }
      filtered.push(node);
    }
    return filtered;
  }

  private tokenizeDecorators(nodes: readonly DecoratorExpressionNode[], inline: boolean) {
    const filteredNodes = this.filterDecorators(nodes);
    if (!inline && filteredNodes.length) {
      const lineCount = this.tokens[this.tokens.length - 3].Kind == ApiViewTokenKind.Punctuation ? 0 : 1;
      this.blankLines(lineCount);
    }
    for (const node of filteredNodes) {
      NamespaceStack.push(generateId(node)!);
      this.tokenize(node);
      if (!inline) {
        this.lineMarker();
      }
      NamespaceStack.pop();
      if (!inline) {
        this.blankLines(0);
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

  private tokenizeIdentifer(
    node: IdentifierNode | MemberExpressionNode | StringLiteralNode,
    style: "declaration" | "reference" | "member"
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
          case "declaration":
            throw new Error(`MemberExpression cannot be a "declaration".`);
        }
        break;
      case SyntaxKind.StringLiteral:
        if (style != "member") {
          throw new Error(`StringLiteral type can only be a member name. Unexpectedly "${style}"`);
        }
        this.stringLiteral(node.value);
        break;
      case SyntaxKind.Identifier:
        switch (style) {
          case "declaration":
            this.typeDeclaration(node.sv, NamespaceStack.value());
            break;
          case "reference":
            const defId = definitionIdFor(node.sv);
            this.typeReference(node.sv, defId);
            break;
          case "member":
            this.member(node.sv);
            break;
        }
    }
  }

  private buildNavigation(ns: NamespaceModel) {
    NamespaceStack.reset();
    this.navigation(new ApiViewNavigation(ns));
  }

  private getNameForNode(node: BaseNode | NamespaceModel): string {
    const id = generateId(node);
    if (id != undefined) {
      return id.split(".").splice(-1)[0];
    } else {
      throw new Error("Unable to get name for node.");
    }
  }

  resolveMissingTypeReferences() {
    for (const token of this.tokens) {
      if (token.Kind == ApiViewTokenKind.TypeName && token.NavigateToId == "__MISSING__") {
        token.NavigateToId = definitionIdFor(token.Value!);
      }
    }
  }

  asApiViewDocument(): ApiViewDocument {
    return {
      Name: this.name,
      PackageName: this.packageName,
      Tokens: this.tokens,
      Navigation: this.navigationItems,
      Diagnostics: this.diagnostics,
      VersionString: this.versionString,
      Language: "Cadl"
    };
  }
}

export abstract class NamespaceStack {
  private static stack = Array<string>();

  static push(val: string) {
    NamespaceStack.stack.push(val);
  }

  static pop(): string | undefined {
    return NamespaceStack.stack.pop();
  }

  static value(): string {
    return NamespaceStack.stack.join(".");
  }

  static reset() {
    this.stack = Array<string>();
  }
}

const typeDeclarations = new Set<string>();

function definitionIdFor(value: string): string | undefined {
  if (value.includes(".")) {
    return typeDeclarations.has(value) ? value : undefined;
  }
  for (const item of typeDeclarations) {
    if (item.split(".").splice(-1)[0] == value) {
      return item;
    }
  }
  return undefined;
}

import {
  AliasStatementNode,
  ArrayExpressionNode,
  ArrayLiteralNode,
  AugmentDecoratorStatementNode,
  BaseNode,
  BooleanLiteralNode,
  ConstStatementNode,
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
  ObjectLiteralNode,
  ObjectLiteralPropertyNode,
  ObjectLiteralSpreadPropertyNode,
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
import { generateId, NamespaceModel } from "./namespace-model.js";
import { LIB_VERSION } from "./version.js";
import { CodeDiagnostic, CodeDiagnosticLevel, CodeFile, ReviewLine, ReviewTokenOptions, TokenKind } from "./schemas.js";
import { NavigationItem } from "./navigation.js";

export class ApiView {
  name: string;
  packageName: string;
  crossLanguagePackageId: string | undefined;
  currentLine: ReviewLine;
  reviewLines: ReviewLine[] = [];
  navigationItems: NavigationItem[] = [];
  diagnostics: CodeDiagnostic[] = [];
  packageVersion: string;

  indentString: string = "";
  indentSize: number = 2;
  namespaceStack = new NamespaceStack();
  typeDeclarations = new Set<string>();
  includeGlobalNamespace: boolean;

  constructor(name: string, packageName: string, packageVersion?: string, includeGlobalNamespace?: boolean) {
    this.name = name;
    this.packageName = packageName;
    this.packageVersion = packageVersion ?? "";
    this.includeGlobalNamespace = includeGlobalNamespace ?? false;
    this.crossLanguagePackageId = packageName;
    this.currentLine = {
      LineId: "",
      CrossLanguageId: "",
      Tokens: [],
      Children: [],
    }
    this.emitHeader();
  }

  token(kind: TokenKind, value: string, options?: ReviewTokenOptions) {
    this.currentLine.Tokens.push({
      Kind: kind,
      Value: value,
      ...options
    });
  }

  indent() {
    // TODO: Maybe this all can be deleted.
    // this.trim();
    // this.indentString = WHITESPACE.repeat(this.indentString.length + this.indentSize);
    // if (this.indentString.length) {
    //   this.currentLine.Tokens.push({ Kind: TokenKind.Whitespace, Value: this.indentString });
    // }
  }

  deindent() {
    // TODO: Maybe this all can be deleted.
    // this.trim();
    // this.indentString = WHITESPACE.repeat(this.indentString.length - this.indentSize);
    // if (this.indentString.length) {
    //   this.currentLine.Tokens.push({ Kind: TokenKind.Whitespace, Value: this.indentString });
    // }
  }

  trim(trimNewlines: boolean = false) {
    // TODO: Maybe this all can be deleted.
    // let last = this.currentLine.Tokens[this.currentLine.Tokens.length - 1];
    // while (last) {
    //   if (last.Kind === TokenKind.Whitespace || (trimNewlines && last.Kind === TokenKind.Newline)) {
    //     this.currentLine.Tokens.pop();
    //     last = this.currentLine.Tokens[this.currentLine.Tokens.length - 1];
    //   } else {
    //     return;
    //   }
    // }
  }

  beginGroup() {
    this.punctuation("{");
    this.blankLines(0);
    this.indent();
  }

  endGroup() {
    this.blankLines(0);
    this.deindent();
    this.punctuation("}");
  }

  whitespace(count: number = 1) {
    // TODO: Maybe this all can be deleted.
    // this.currentLine.Tokens.push({
    //   Kind: TokenKind.Text,
    //   Value: WHITESPACE.repeat(count),
    // });
  }

  space() {
    // TODO: Maybe this all can be deleted
    // if (this.currentLine.Tokens[this.currentLine.Tokens.length - 1]?.Kind !== TokenKind.Whitespace) {
    //   this.currentLine.Tokens.push({
    //     Kind: TokenKind.Whitespace,
    //     Value: WHITESPACE,
    //   });
    // }
  }

  newline() {
    this.trim();
    this.reviewLines.push(this.currentLine);
    // Probably need to calculate the line id and cross language id at this point from the gathered tokens?
    this.currentLine = {
      LineId: "",
      CrossLanguageId: "",
      Tokens: [],
      Children: [],
    }
    if (this.indentString.length) {
      this.whitespace(this.indentString.length);
    }
  }

  blankLines(count: number) {
    // count the number of trailing newlines (ignoring indent whitespace)
    let newlineCount: number = 0;
    for (let i = this.reviewLines.length; i > 0; i--) {
      const line = this.reviewLines[i - 1];
      if (line.Tokens.length === 0) {
          newlineCount++;
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
        const popped = this.reviewLines.pop();
        if (popped?.Tokens.length === 0) {
          toRemove--;
        }
      }
    }
  }

  lineMarker(options?: {value?: string, addCrossLanguageId?: boolean}) {
    this.currentLine.LineId = options?.value ?? this.namespaceStack.value();
    this.currentLine.CrossLanguageId = options?.addCrossLanguageId ? (options?.value ?? this.namespaceStack.value()) : undefined;
  }

  punctuation(value: string, options?: ReviewTokenOptions) {
    this.token(TokenKind.Punctuation, value, options);
  }

  text(text: string, options?: ReviewTokenOptions) {
    this.token(TokenKind.Text, text, options);
  }

  keyword(keyword: string, options?: ReviewTokenOptions) {
    this.token(TokenKind.Keyword, keyword, options);
  }

  typeDeclaration(typeName: string, typeId: string | undefined, addCrossLanguageId: boolean, options?: ReviewTokenOptions) {
    if (typeId) {
      if (this.typeDeclarations.has(typeId)) {
        throw new Error(`Duplication ID "${typeId}" for declaration will result in bugs.`);
      }
      this.typeDeclarations.add(typeId);
    }
    this.lineMarker({value: typeId, addCrossLanguageId: true});
    this.token(TokenKind.TypeName, typeName, options);
  }

  typeReference(typeName: string, options?: ReviewTokenOptions) {
    options = options ?? {};
    options.NavigateToId = options.NavigateToId ?? "__MISSING__";
    this.token(TokenKind.TypeName, typeName, {...options});
  }

  member(name: string, options?: ReviewTokenOptions) {
    this.token(TokenKind.MemberName, name, options);
  }

  stringLiteral(value: string, options?: ReviewTokenOptions) {
    const lines = value.split("\n");
    if (lines.length === 1) {
      this.currentLine.Tokens.push({
        Kind: TokenKind.StringLiteral,
        Value: `\u0022${value}\u0022`,
        ...options
      });
    } else {
      this.punctuation(`"""`, options);
      this.newline();
      for (const line of lines) {
        this.literal(line, options);
        this.newline();
      }
      this.punctuation(`"""`, options);
    }
  }

  literal(value: string, options?: ReviewTokenOptions) {
    this.token(TokenKind.StringLiteral, value, options);
  }

  diagnostic(message: string, targetId: string, level: CodeDiagnosticLevel) {
    this.diagnostics.push({
      Text: message,
      TargetId: targetId,
      Level: level,
    })
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
        this.buildNavigation(nsModel);
      }
    }
  }

  private emitHeader() {
    const toolVersion = LIB_VERSION;
    const headerText = `// Package parsed using @azure-tools/typespec-apiview (version:${toolVersion})`;
    this.literal(headerText, {SkipDiff: true});
    this.namespaceStack.push("GLOBAL");
    this.lineMarker();
    this.namespaceStack.pop();
    // TODO: Source URL?
    this.blankLines(2);
  }

  tokenize(node: BaseNode) {
    let obj;
    let last = 0;  // track the final index of an array
    let isExpanded = false;
    switch (node.kind) {
      case SyntaxKind.AliasStatement:
        obj = node as AliasStatementNode;
        this.namespaceStack.push(obj.id.sv);
        this.keyword("alias", {HasSuffixSpace: true});
        this.typeDeclaration(obj.id.sv, this.namespaceStack.value(), true);
        this.tokenizeTemplateParameters(obj.templateParameters);
        this.punctuation("=", {HasSuffixSpace: true});
        this.tokenize(obj.value);
        this.namespaceStack.pop();
        break;
      case SyntaxKind.ArrayExpression:
        obj = node as ArrayExpressionNode;
        this.tokenize(obj.elementType);
        this.punctuation("[]");
        break;
      case SyntaxKind.ArrayLiteral:
        obj = node as ArrayLiteralNode;
        this.punctuation("#[");
        last = obj.values.length - 1;
        obj.values.forEach((val, i) => {
          this.tokenize(val);
          if (i !== last) {
            this.punctuation(",", {HasSuffixSpace: true});
          }
        });
        this.punctuation("]");
        break;
      case SyntaxKind.AugmentDecoratorStatement:
        obj = node as AugmentDecoratorStatementNode;
        const decoratorName = this.getNameForNode(obj.target);
        this.namespaceStack.push(decoratorName);
        this.punctuation("@@");
        this.tokenizeIdentifier(obj.target, "keyword");
        this.lineMarker();
        if (obj.arguments.length) {
          const last = obj.arguments.length - 1;
          this.punctuation("(");
          this.tokenize(obj.targetType);
          if (obj.arguments.length) {
            this.punctuation(",", {HasSuffixSpace: true});
          }
          for (let x = 0; x < obj.arguments.length; x++) {
            const arg = obj.arguments[x];
            this.tokenize(arg);
            if (x !== last) {
              this.punctuation(",", {HasSuffixSpace: true});
            }
          }
          this.punctuation(")");
          this.namespaceStack.pop();
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
      case SyntaxKind.ConstStatement:
        obj = node as ConstStatementNode;
        this.namespaceStack.push(obj.id.sv);
        this.keyword("const", {HasSuffixSpace: true});
        this.tokenizeIdentifier(obj.id, "declaration");
        this.punctuation("=", {HasSuffixSpace: true});
        this.tokenize(obj.value);
        this.namespaceStack.pop();
        break;
      case SyntaxKind.DecoratorExpression:
        obj = node as DecoratorExpressionNode;
        this.punctuation("@");
        this.tokenizeIdentifier(obj.target, "keyword");
        this.lineMarker();
        if (obj.arguments.length) {
          last = obj.arguments.length - 1;
          this.punctuation("(");
          for (let x = 0; x < obj.arguments.length; x++) {
            const arg = obj.arguments[x];
            this.tokenize(arg);
            if (x !== last) {
              this.punctuation(",", {HasSuffixSpace: true});
            }
          }
          this.punctuation(")");
        }
        break;
      case SyntaxKind.DirectiveExpression:
        obj = node as DirectiveExpressionNode;
        this.namespaceStack.push(generateId(node)!);
        this.lineMarker();
        this.keyword(`#${obj.target.sv}`, {HasSuffixSpace: true});
        for (const arg of obj.arguments) {
          switch (arg.kind) {
            case SyntaxKind.StringLiteral:
              this.stringLiteral(arg.value);
              this.space();
              break;
            case SyntaxKind.Identifier:
              this.stringLiteral(arg.sv);
              this.space();
              break;
          }
        }
        this.newline();
        this.namespaceStack.pop();
        break;
      case SyntaxKind.EmptyStatement:
        throw new Error(`Case "EmptyStatement" not implemented`);
      case SyntaxKind.EnumMember:
        obj = node as EnumMemberNode;
        this.tokenizeDecoratorsAndDirectives(obj.decorators, obj.directives, false);
        this.tokenizeIdentifier(obj.id, "member");
        this.lineMarker({addCrossLanguageId: true});
        if (obj.value) {
          this.punctuation(":", {HasSuffixSpace: true});
          this.tokenize(obj.value);
        }
        break;
      case SyntaxKind.EnumSpreadMember:
        obj = node as EnumSpreadMemberNode;
        this.punctuation("...");
        this.tokenize(obj.target);
        this.lineMarker();
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
        this.typeReference(obj.sv, {NavigateToId: id});
        break;
      case SyntaxKind.ImportStatement:
        throw new Error(`Case "ImportStatement" not implemented`);
      case SyntaxKind.IntersectionExpression:
        obj = node as IntersectionExpressionNode;
        for (let x = 0; x < obj.options.length; x++) {
          const opt = obj.options[x];
          this.tokenize(opt);
          if (x !== obj.options.length - 1) {
            this.punctuation("&", {HasSuffixSpace: true});
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
        this.lineMarker();
        break;
      case SyntaxKind.ModelStatement:
        obj = node as ModelStatementNode;
        this.tokenizeModelStatement(obj);
        break;
      case SyntaxKind.NamespaceStatement:
        throw new Error(`Case "NamespaceStatement" not implemented`);
      case SyntaxKind.NeverKeyword:
        this.keyword("never", {HasSuffixSpace: true});
        break;
      case SyntaxKind.NumericLiteral:
        obj = node as NumericLiteralNode;
        this.literal(obj.value.toString());
        break;
      case SyntaxKind.ObjectLiteral:
        obj = node as ObjectLiteralNode;
        this.punctuation("#{");
        last = obj.properties.length - 1;
        obj.properties.forEach((prop, i) => {
          this.tokenize(prop);
          if (i !== last) {
            this.punctuation(",", {HasSuffixSpace: true});
          }
        });
        this.punctuation("}");
        break;
      case SyntaxKind.ObjectLiteralProperty:
        obj = node as ObjectLiteralPropertyNode;
        this.tokenizeIdentifier(obj.id, "member");
        this.punctuation(":", {HasSuffixSpace: true});
        this.tokenize(obj.value);
        break;
      case SyntaxKind.ObjectLiteralSpreadProperty:
        obj = node as ObjectLiteralSpreadPropertyNode;
        // TODO: Whenever there is an example?
        throw new Error(`Case "ObjectLiteralSpreadProperty" not implemented`);
      case SyntaxKind.OperationStatement:
        this.tokenizeOperationStatement(node as OperationStatementNode);
        break;
      case SyntaxKind.OperationSignatureDeclaration:
        obj = node as OperationSignatureDeclarationNode;
        this.punctuation("(");
        // TODO: heuristic for whether operation signature should be inlined or not.
        const inline = false;
        this.tokenizeModelExpression(obj.parameters, true, inline);
        this.punctuation("):", {HasSuffixSpace: true});
        this.tokenizeReturnType(obj, inline);
        break;
      case SyntaxKind.OperationSignatureReference:
        obj = node as OperationSignatureReferenceNode;
        this.keyword("is", {HasSuffixSpace: true});
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
          this.keyword("extends", {HasSuffixSpace: true});
          this.tokenize(obj.constraint);
        }
        if (obj.default) {
          this.punctuation("=", {HasSuffixSpace: true});
          this.tokenize(obj.default);
        }
        break;
      case SyntaxKind.TupleExpression:
        obj = node as TupleExpressionNode;
        this.punctuation("[", {HasSuffixSpace: true});
        for (let x = 0; x < obj.values.length; x++) {
          const val = obj.values[x];
          this.tokenize(val);
          if (x !== obj.values.length - 1) {
            this.renderPunctuation(",");
          }
        }
        this.punctuation("]");
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
            this.indent();
          }
          for (let x = 0; x < obj.arguments.length; x++) {
            const arg = obj.arguments[x];
            this.tokenize(arg);
            if (x !== obj.arguments.length - 1) {
              this.trim(true);
              this.renderPunctuation(",");
              if (isExpanded) {
                this.newline();
              }
            }
          }
          if (isExpanded) {
            this.trim(true);
            this.newline();
            this.deindent();
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
            this.punctuation("|", {HasSuffixSpace: true});
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
        this.keyword("unknown", {HasSuffixSpace: true});
        break;
      case SyntaxKind.UsingStatement:
        throw new Error(`Case "UsingStatement" not implemented`);
      case SyntaxKind.ValueOfExpression:
        this.keyword("valueof", {HasSuffixSpace: true});
        this.tokenize((node as ValueOfExpressionNode).target);
        break;
      case SyntaxKind.VoidKeyword:
        this.keyword("void");
        break;
      case SyntaxKind.TemplateArgument:
        obj = node as TemplateArgumentNode;
        isExpanded = obj.argument.kind === SyntaxKind.ModelExpression && obj.argument.properties.length > 0;
        if (isExpanded) {
          this.blankLines(0);
        }
        if (obj.name) {
          this.text(obj.name.sv);
          this.punctuation("=", {HasSuffixSpace: true});
        }
        if (isExpanded) {
          this.tokenizeModelExpressionExpanded(obj.argument as ModelExpressionNode, false, false);
        } else {
          this.tokenize(obj.argument);
        }
        break;
      case SyntaxKind.StringTemplateExpression:
        obj = node as StringTemplateExpressionNode;
        const stringValue = this.buildTemplateString(obj);
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
        this.indent();
        for (const line of lines) {
          this.literal(line);
          this.newline();
        }
        this.deindent();
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

  peekTokens(count: number = 20): string {
    let result = "";
    const tokens = this.currentLine.Tokens.slice(-count);
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

  private tokenizeModelStatement(node: ModelStatementNode) {
    this.namespaceStack.push(node.id.sv);
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    this.keyword("model", {HasSuffixSpace: true});
    this.tokenizeIdentifier(node.id, "declaration");
    if (node.extends) {
      this.keyword("extends", {HasSuffixSpace: true});
      this.tokenize(node.extends);
    }
    if (node.is) {
      this.keyword("is", {HasSuffixSpace: true});
      this.tokenize(node.is);
    }
    this.tokenizeTemplateParameters(node.templateParameters);
    if (node.properties.length) {
      this.beginGroup();
      for (const prop of node.properties) {
        const propName = this.getNameForNode(prop);
        this.namespaceStack.push(propName);
        this.tokenize(prop);
        this.trim(true);
        this.punctuation(";");
        this.namespaceStack.pop();
        this.blankLines(0);
      }
      this.endGroup();
    } else {
      this.punctuation("{}");
    }
    this.namespaceStack.pop();
  }

  private tokenizeScalarStatement(node: ScalarStatementNode) {
    this.namespaceStack.push(node.id.sv);
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    this.keyword("scalar", {HasSuffixSpace: true});
    this.tokenizeIdentifier(node.id, "declaration");
    if (node.extends) {
      this.keyword("extends", {HasSuffixSpace: true});
      this.tokenize(node.extends);
    }
    this.tokenizeTemplateParameters(node.templateParameters);
    this.blankLines(0);
    this.namespaceStack.pop();
  }

  private tokenizeInterfaceStatement(node: InterfaceStatementNode) {
    this.namespaceStack.push(node.id.sv);
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    this.keyword("interface", {HasSuffixSpace: true});
    this.tokenizeIdentifier(node.id, "declaration");
    this.tokenizeTemplateParameters(node.templateParameters);
    this.beginGroup();
    for (let x = 0; x < node.operations.length; x++) {
      const op = node.operations[x];
      this.tokenizeOperationStatement(op, true);
      this.blankLines(x !== node.operations.length - 1 ? 1 : 0);
    }
    this.endGroup();
    this.namespaceStack.pop();
  }

  private tokenizeEnumStatement(node: EnumStatementNode) {
    this.namespaceStack.push(node.id.sv);
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    this.keyword("enum", {HasSuffixSpace: true});
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
    this.keyword("union", {HasSuffixSpace: true});
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
      this.punctuation(":", {HasSuffixSpace: true});
    }
    this.lineMarker({addCrossLanguageId: true});
    this.tokenize(node.value);
  }

  private tokenizeModelProperty(node: ModelPropertyNode, inline: boolean) {
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, inline);
    this.tokenizeIdentifier(node.id, "member");
    this.lineMarker();
    this.punctuation(node.optional ? "?:" : ":", {HasSuffixSpace: true});
    this.tokenize(node.value);
    if (node.default) {
      this.punctuation("=", {HasSuffixSpace: true});
      this.tokenize(node.default);
    }
  }

  private tokenizeModelExpressionInline(node: ModelExpressionNode, isOperationSignature: boolean) {
    if (node.properties.length) {
      if (!isOperationSignature) {
        this.punctuation("{", {HasSuffixSpace: true});
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
            this.punctuation(",", {HasSuffixSpace: true});
          }
        } else {
          this.punctuation(";");
        }
      }
      if (!isOperationSignature) {
        this.punctuation("}", {HasSuffixSpace: true});
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
        this.punctuation("{");
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
        this.punctuation("}");
        this.blankLines(0);
      }
      this.trim();
      if (leadingNewline) {
        this.deindent();
      }
    } else if (!isOperationSignature) {
      this.punctuation("{}");
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
      this.keyword("op", {HasSuffixSpace: true});
    }
    this.tokenizeIdentifier(node.id, "declaration");
    this.tokenizeTemplateParameters(node.templateParameters);
    this.tokenize(node.signature);
    this.punctuation(";");
    this.namespaceStack.pop();
  }

  private tokenizeNamespaceModel(model: NamespaceModel) {
    this.namespaceStack.push(model.name);
    if (model.node.kind === SyntaxKind.NamespaceStatement) {
      this.tokenizeDecoratorsAndDirectives(model.node.decorators, model.node.directives, false);
    }
    this.keyword("namespace", {HasSuffixSpace: true});
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
    for (const node of model.constants.values()) {
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
      while (this.currentLine.Tokens.length) {
        const item = this.currentLine.pop()!;
        if (item.Kind === TokenKind.LineIdMarker && item.LineId === "GLOBAL") {
          this.currentLine.Tokens.push(item);
          this.blankLines(2);
          break;
        } else if ([TokenKind.Punctuation, TokenKind.TypeName].includes(item.Kind)) {
          this.currentLine.Tokens.push(item);
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
      this.tokenize(node);
      if (inline) {
        this.space();
      }
      this.namespaceStack.pop();
      if (isDoc) {
        for (const token of this.currentLine.Tokens) {
          token.IsDocumentation = true;
        }
      }
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
            this.typeReference(node.sv, {NavigateToId: defId});
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
      this.punctuation("<");
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
      const offset = this.currentLine.Tokens.length;
      this.tokenize(node.returnType);
      const returnTokens = this.currentLine.Tokens.slice(offset);
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

  private buildNavigation(ns: NamespaceModel) {
    this.namespaceStack.reset();
    this.navigationItems.push(new NavigationItem(ns, this.namespaceStack));
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
    this.punctuation(punctuation, {HasSuffixSpace: true});
  }

  resolveMissingTypeReferences() {
    for (const token of this.currentLine.Tokens) {
      if (token.Kind === TokenKind.TypeName && token.NavigateToId === "__MISSING__") {
        token.NavigateToId = this.definitionIdFor(token.Value!, this.packageName);
      }
    }
  }

  asApiViewDocument(): CodeFile {
    return {
      Name: this.name,
      PackageName: this.packageName,
      PackageVersion: this.packageVersion,
      ParserVersion: LIB_VERSION,
      Language: "TypeSpec",
      LanguageVariant: undefined,
      CrossLanguagePackageId: this.crossLanguagePackageId,
      ReviewLines: this.reviewLines,
      Diagnostics: this.diagnostics,
      Navigation: this.navigationItems,
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

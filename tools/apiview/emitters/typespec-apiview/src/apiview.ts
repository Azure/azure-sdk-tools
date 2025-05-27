import {
  Expression,
  getNamespaceFullName,
  getSourceLocation,
  Namespace,
  navigateProgram,
  Program
} from "@typespec/compiler";
import { 
  AliasStatementNode,
  ArrayExpressionNode,
  ArrayLiteralNode,
  AugmentDecoratorStatementNode,
  BaseNode,
  BooleanLiteralNode,
  CallExpressionNode,
  ConstStatementNode,
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
  ObjectLiteralNode,
  ObjectLiteralPropertyNode,
  ObjectLiteralSpreadPropertyNode,
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
  ValueOfExpressionNode
} from "@typespec/compiler/ast";
import { generateId, NamespaceModel } from "./namespace-model.js";
import { LIB_VERSION } from "./version.js";
import { CodeDiagnostic, CodeDiagnosticLevel, CodeFile, NavigationItem, ReviewLine, ReviewToken, ReviewTokenOptions, TokenKind } from "./schemas.js";
import { NamespaceStack, reviewLineText } from "./util.js";

export class ApiView {
  name: string;
  packageName: string;
  crossLanguagePackageId: string | undefined;
  /** Stores the current line. All helper methods append to this. */
  currentLine: ReviewLine;
  /** Stores the parent of the current line. */
  currentParent: ReviewLine | undefined;
  /** Stores the stack of parent lines. */
  parentStack: ReviewLine[];

  reviewLines: ReviewLine[] = [];
  navigationItems: NavigationItem[] = [];
  diagnostics: CodeDiagnostic[] = [];
  packageVersion: string;

  namespaceStack = new NamespaceStack();
  typeDeclarations = new Set<string>();
  includeGlobalNamespace: boolean;

  constructor(name: string, packageName: string, includeGlobalNamespace?: boolean) {
    this.name = name;
    this.packageName = packageName;
    this.packageVersion = "ALL";
    this.includeGlobalNamespace = includeGlobalNamespace ?? false;
    this.crossLanguagePackageId = packageName;
    this.currentLine = {
      LineId: "",
      CrossLanguageId: "",
      Tokens: [],
      Children: [],
    }
    this.parentStack = []
    this.currentParent = undefined;
    this.emitHeader();
  }

  /** Compiles the APIView model for output.  */
  compile(program: Program) {
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
    // Enable this if desired to debug the output
    //this.appendDebugInfo();
  }

  private appendDebugInfo() {

    function processLines(lines: ReviewLine[]) {
      for (const line of lines) {
        const lineId = line.LineId;
        const relatedLineId = line.RelatedToLine;
        const isContextEnd = line.IsContextEndLine
        let string = "";
        if (lineId && lineId !== "") {
          string += ` LINE_ID: ${lineId} `;
        }
        if (relatedLineId && relatedLineId !== "") {
          string += ` RELATED_TO: ${relatedLineId} `;
        }
        if (isContextEnd) {
          string += " IS_CONTEXT_END TRUE ";
        }
        if (string !== "") {
          line.Tokens.push({
            Kind: TokenKind.Comment,
            Value: `// ${string.trim()}`,
          })
        }
        processLines(line.Children);
      }  
    }

    processLines(this.reviewLines);
  }

  /** Attempts to resolve any type references marked as __MISSING__. */
  resolveMissingTypeReferences() {
    for (const token of this.currentLine.Tokens) {
      if (token.Kind === TokenKind.TypeName && token.NavigateToId === "__MISSING__") {
        token.NavigateToId = this.definitionIdFor(token.Value!, this.packageName);
      }
    }
  }

  /** Apply workarounds to the model before output */
  private adjustLines(lines: ReviewLine[]) {
    let currentContext: string | undefined = undefined;
    let contextMatchFound: boolean = false;
    for (const line of lines) {
      // run the normal adjust line logic
      this.adjustLine(line);

      const lineId = line.LineId;
      if (lineId === "Azure.Test.ConstrainedComplex") {
        let test = "best";
      }
      const relatedTo = line.RelatedToLine;
      const isContextEnd = line.IsContextEndLine;

      if (isContextEnd && relatedTo) {
        throw new Error("Context end line should not have a relatedTo line.");
      }

      if (currentContext) {
        if (lineId === currentContext) {
          contextMatchFound = true;
          line.RelatedToLine = undefined;
          line.IsContextEndLine = false;
        }
        if (relatedTo && currentContext !== relatedTo) {
          if (!contextMatchFound) {
            // catches the scenario where the relatedTo line is different within what should be the current context
            throw new Error("Mismatched contexts. Expected ${currentContext}, got ${lineId}");
          } else {
            // covers the instance where there never is an IsContextEndLine set, which happens if there's no closing brace
            // on a separate line
            currentContext = relatedTo;
          }
        } else {
          // key to this whole method. This copies RelatedToLine to all lines between the start and end of a context
          line.RelatedToLine = currentContext;
        }
        if (isContextEnd) {
          currentContext = undefined;
          contextMatchFound = false;
          line.RelatedToLine = undefined;
        }
      } else {
        // if currentContext isn't set and we encounter a relatedTo line, set the current context
        if (relatedTo) {
          currentContext = relatedTo;
        // if currentContext isn't set but there's a lineId with childrent, set the current context
        } else if (lineId && line.Children.length > 0) {
          currentContext = lineId;
          contextMatchFound = true;
        // If a context end is found without a start, then ignore the contextEnd
        } else if (isContextEnd) {
          line.IsContextEndLine = false;
        }
      }
    }
  }

  private adjustLine(line: ReviewLine) {
    for (const token of line.Tokens) {
      this.adjustToken(token);
    }
    this.adjustLines(line.Children);
  }

  private adjustToken(token: ReviewToken) {
    // the server has a bizarre "true" default for HasSuffixSpace that we
    // need to account for. Also, we can delete the property if it's true
    // since that's the server default.
    token.HasSuffixSpace = token.HasSuffixSpace ?? false;
    if (token.HasSuffixSpace) {
      delete token.HasSuffixSpace;
    }
  }

  /** Output the APIView model to the CodeFile JSON format. */
  asCodeFile(): CodeFile {
    this.adjustLines(this.reviewLines);
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

  /** Outputs the APIView model to a string approximation of what will display on the web. */
  asString(): string {
    return this.reviewLines.map(l => reviewLineText(l, 0)).join("\n");
  }

  private token(kind: TokenKind, value: string, options?: ReviewTokenOptions) {
    this.currentLine.Tokens.push({
      Kind: kind,
      Value: value,
      ...options,
    });
  }

  private indent() {
    // ensure no trailing space at the end of the line
    try {
      const lastToken = this.currentLine.Tokens[this.currentLine.Tokens.length - 1];
      lastToken.HasSuffixSpace = false;
    } catch (e) {
      // no tokens, so nothing to do
      return;
    }

    if (this.currentParent) {
      this.currentParent.Children.push(this.currentLine);
      this.parentStack.push(this.currentParent);
    } else {
      this.reviewLines.push(this.currentLine);
    }
    this.currentParent = this.currentLine;
    this.currentLine = {
      LineId: "",
      CrossLanguageId: "",
      Tokens: [],
      Children: [],
    }
  }

  private deindent() {
    if (!this.currentParent) {
      return;
    }
    // Ensure that the last line before the deindent has no blank lines
    const lastChild = this.currentParent.Children.pop();
    if (lastChild && lastChild.Tokens.length > 0) {
      this.currentParent.Children.push(lastChild);
    }
    this.currentParent = this.parentStack.pop();
    this.currentLine = {
      LineId: "",
      CrossLanguageId: "",
      Tokens: [],
      Children: [],
    }
  }

  /** Set the exact number of desired newlines. */
  private blankLines(count: number = 0) {
    this.newline();
    const parentLines = this.currentParent ? this.currentParent.Children : this.reviewLines;
    let newlineCount = 0;
    if (parentLines.length) {
      for (let i = parentLines.length - 1; i >= 0; i--) {
        if (parentLines[i].Tokens.length === 0) {
          newlineCount++;
        } else {
          break;
        }
      }
    }
    if (newlineCount == count) {
      return;
    } else if (newlineCount > count) {
      const toRemove = newlineCount - count;
      for (let i = 0; i < toRemove; i++) {
        parentLines.pop();
      }
    } else {
      for (let i = newlineCount; i < count; i++) {
        this.newline();
      }
    }
  }

  private newline() {
    // ensure no trailing space at the end of the line
    if (this.currentLine.Tokens.length > 0) {
      const lastToken = this.currentLine.Tokens[this.currentLine.Tokens.length - 1];
      lastToken.HasSuffixSpace = false;
      const firstToken = this.currentLine.Tokens[0];
      firstToken.HasPrefixSpace = false;
    }
    
    if (this.currentParent) {
      this.currentParent.Children.push(this.currentLine);
    } else {
      this.reviewLines.push(this.currentLine);
    }
    this.currentLine = {
      LineId: "",
      CrossLanguageId: "",
      Tokens: [],
      Children: [],
    }
  }

  private getLastLine(): ReviewLine | undefined {
    if (!this.currentParent) {
      return undefined;
    }
    const lastChild = this.currentParent.Children[this.currentParent.Children.length - 1];
    const lastGrandchild = lastChild?.Children[lastChild.Children.length - 1];
    if (lastGrandchild?.Children.length > 0) {
      throw new Error("Unexpected great-grandchild in getLastLine()!");
    }
    return lastGrandchild ?? lastChild;
  }

  /** 
   * Places the provided token in the tree based on the provided characters.
   * param token The token to snap.
   * param characters The characters to snap to.
   */
  private snapToken(punctuationToken: ReviewToken, characters: string) {
    const allowed = new Set(characters.split(""));
    const lastLine = this.getLastLine() ?? this.currentLine;

    // iterate through tokens in reverse order
    for (let i = lastLine.Tokens.length - 1; i >= 0; i--) {
      const token = lastLine.Tokens[i];
      if (token.Kind === TokenKind.Text) {
        // skip blank whitespace tokens
        const value = token.Value.trim();
        if (value.length === 0) {
          continue;
        } else {
          // no snapping, so render in place
          this.currentLine.Tokens.push(punctuationToken);
          return;
        }
      } else if (token.Kind === TokenKind.Punctuation) {
        // ensure no whitespace after the trim character
        if (allowed.has(token.Value)) {
          token.HasSuffixSpace = false;
          punctuationToken.HasSuffixSpace = false;
          lastLine.Tokens.push(punctuationToken);
        } else {
          // no snapping, so render in place
          this.currentLine.Tokens.push(punctuationToken);
          return;
        }
      } else {
        // no snapping, so render in place
        this.currentLine.Tokens.push(punctuationToken);
        return;
      }
    }
  }

  private lineMarker(options?: {value?: string, addCrossLanguageId?: boolean, relatedLineId?: string}) {
    this.currentLine.LineId = options?.value ?? this.namespaceStack.value();
    this.currentLine.CrossLanguageId = options?.addCrossLanguageId ? (options?.value ?? this.namespaceStack.value()) : undefined;
    this.currentLine.RelatedToLine = options?.relatedLineId;
  }

  private punctuation(value: string, options?: ReviewTokenOptions & {snapTo?: string, isContextEndLine?: boolean}) {
    const snapTo = options?.snapTo;
    delete options?.snapTo;
    const isContextEndLine = options?.isContextEndLine;
    delete options?.isContextEndLine;

    const token = {
      Kind: TokenKind.Punctuation,
      Value: value,
      ...options,
    }

    if (snapTo) {
      this.snapToken(token, snapTo);
    } else {
      this.currentLine.Tokens.push(token);
    }
    if (isContextEndLine) {
      this.currentLine.IsContextEndLine = true;
    }
  }

  private text(text: string, options?: ReviewTokenOptions) {
    this.token(TokenKind.Text, text, options);
  }

  private keyword(keyword: string, options?: ReviewTokenOptions) {
    this.token(TokenKind.Keyword, keyword, options);
  }

  private typeDeclaration(typeName: string, typeId: string | undefined, addCrossLanguageId: boolean, options?: ReviewTokenOptions) {
    if (typeId) {
      if (this.typeDeclarations.has(typeId)) {
        throw new Error(`Duplication ID "${typeId}" for declaration will result in bugs.`);
      }
      this.typeDeclarations.add(typeId);
    }
    this.lineMarker({value: typeId, addCrossLanguageId: true});
    this.token(TokenKind.TypeName, typeName, options);
  }

  private typeReference(typeName: string, options?: ReviewTokenOptions) {
    options = options ?? {};
    options.NavigateToId = options.NavigateToId ?? "__MISSING__";
    this.token(TokenKind.TypeName, typeName, {...options});
  }

  private member(name: string, options?: ReviewTokenOptions) {
    this.token(TokenKind.MemberName, name, options);
  }

  private stringLiteral(value: string, options?: ReviewTokenOptions) {
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

  private literal(value: string, options?: ReviewTokenOptions) {
    this.token(TokenKind.StringLiteral, value, options);
  }

  private diagnostic(message: string, targetId: string, level: CodeDiagnosticLevel) {
    this.diagnostics.push({
      Text: message,
      TargetId: targetId,
      Level: level,
    })
  }

  private shouldEmitNamespace(name: string): boolean {
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

  private emitHeader() {
    const toolVersion = LIB_VERSION;
    const headerText = `// Package parsed using @azure-tools/typespec-apiview (version:${toolVersion})`;
    this.literal(headerText, {SkipDiff: true});
    this.namespaceStack.push("GLOBAL");
    this.lineMarker();
    this.namespaceStack.pop();
    // TODO: Source URL?
    this.blankLines(2)
  }

  private tokenize(node: BaseNode) {
    let obj;
    let last = 0;  // track the final index of an array
    let parentNamespace: string;
    switch (node.kind) {
      case SyntaxKind.AliasStatement:
        obj = node as AliasStatementNode;
        this.namespaceStack.push(obj.id.sv);
        this.keyword("alias", {HasSuffixSpace: true});
        this.typeDeclaration(obj.id.sv, this.namespaceStack.value(), true);
        this.tokenizeTemplateParameters(obj.templateParameters);
        this.punctuation("=", {HasSuffixSpace: true, HasPrefixSpace: true});
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
        this.punctuation("=", {HasSuffixSpace: true, HasPrefixSpace: true});
        this.tokenize(obj.value);
        this.namespaceStack.pop();
        break;
      case SyntaxKind.DecoratorExpression:
        obj = node as DecoratorExpressionNode;
        parentNamespace = this.namespaceStack.value();
        this.namespaceStack.push(generateId(obj)!);
        this.punctuation("@");
        this.tokenizeIdentifier(obj.target, "keyword");
        this.lineMarker({relatedLineId: parentNamespace});
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
        this.namespaceStack.pop();
        break;
      case SyntaxKind.DirectiveExpression:
        obj = node as DirectiveExpressionNode;
        parentNamespace = this.namespaceStack.value();
        this.namespaceStack.push(generateId(node)!);
        this.lineMarker({relatedLineId: parentNamespace});
        this.keyword(`#${obj.target.sv}`, {HasSuffixSpace: true});
        for (const arg of obj.arguments) {
          switch (arg.kind) {
            case SyntaxKind.StringLiteral:
              this.stringLiteral(arg.value, {HasSuffixSpace: true});
              break;
            case SyntaxKind.Identifier:
              this.stringLiteral(arg.sv, {HasSuffixSpace: true});
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
            this.punctuation("&", {HasPrefixSpace: true, HasSuffixSpace: true});
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
        this.indent();
        this.tokenizeModelExpression(node as ModelExpressionNode, {isOperationSignature: false});
        this.deindent();
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
        this.keyword("never");
        break;
      case SyntaxKind.NumericLiteral:
        obj = node as NumericLiteralNode;
        this.literal(obj.value.toString());
        break;
      case SyntaxKind.ObjectLiteral:
        obj = node as ObjectLiteralNode;
        this.punctuation("#{");
        this.indent();
        last = obj.properties.length - 1;
        obj.properties.forEach((prop, i) => {
          this.tokenize(prop);
          if (i !== last) {
            this.punctuation(",", {HasSuffixSpace: false});
          }
          this.newline();
        });
        this.deindent();
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
        if (obj.parameters.properties.length) {
          this.indent();
          this.tokenizeModelExpression(obj.parameters, {isOperationSignature: true});
          this.deindent();  
        }
        this.punctuation("):", {HasSuffixSpace: true});
        this.tokenizeReturnType(obj, {isExpanded: true});
        break;
      case SyntaxKind.OperationSignatureReference:
        obj = node as OperationSignatureReferenceNode;
        this.keyword("is", {HasPrefixSpace: true, HasSuffixSpace: true});
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
          this.keyword("extends", {HasSuffixSpace: true, HasPrefixSpace: true});
          this.tokenize(obj.constraint);
        }
        if (obj.default) {
          this.punctuation("=", {HasSuffixSpace: true, HasPrefixSpace: true});
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
            this.punctuation(",", {HasSuffixSpace: true});
          }
        }
        this.punctuation("]");
        break;
      case SyntaxKind.TypeReference:
        obj = node as TypeReferenceNode;
        this.tokenizeIdentifier(obj.target, "reference");
        this.tokenizeTemplateInstantiation(obj);
        break;
      case SyntaxKind.UnionExpression:
        obj = node as UnionExpressionNode;
        for (let x = 0; x < obj.options.length; x++) {
          const opt = obj.options[x];
          this.tokenize(opt);
          if (x !== obj.options.length - 1) {
            this.punctuation("|", {HasPrefixSpace: true, HasSuffixSpace: true});
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
        this.keyword("unknown");
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
      case SyntaxKind.CallExpression:
        obj = node as CallExpressionNode;
        this.tokenize(obj.target);
        this.punctuation("(", {HasSuffixSpace: false});
        for (let x = 0; x < obj.arguments.length; x++) {
          const arg = obj.arguments[x];
          this.tokenize(arg);
          if (x !== obj.arguments.length - 1) {
            this.punctuation(",", {HasSuffixSpace: true, snapTo: "}"});
          }
        }
        this.punctuation(")");
        break;
      default:
        // All Projection* cases should fail here...
        throw new Error(`Case "${SyntaxKind[node.kind].toString()}" not implemented`);
    }
  }

  private tokenizeTemplateInstantiation(obj: TypeReferenceNode) {
    if (!obj.arguments.length) {
      return;
    }

    // if any argument is a ModelExpression, then we need to expand the template to multiple lines
    const isExpanded = obj.arguments.some(arg => arg.argument.kind === SyntaxKind.ModelExpression);

    this.punctuation("<");
    if (isExpanded) {
      this.indent();
    }
    for (let x = 0; x < obj.arguments.length; x++) {
      const arg = obj.arguments[x];
      this.tokenizeTemplateArgument(arg);
      if (x !== obj.arguments.length - 1) {
        this.punctuation(",", {HasSuffixSpace: true, snapTo: "}"});
        if (isExpanded && arg.argument.kind) {
          this.blankLines(0);
        }
      }
    }
    if (isExpanded) {
      this.newline();
      this.deindent();
    }
    this.punctuation(">");
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
      this.keyword("extends", {HasSuffixSpace: true, HasPrefixSpace: true});
      this.tokenize(node.extends);
    }
    if (node.is) {
      this.keyword("is", {HasPrefixSpace: true, HasSuffixSpace: true});
      this.tokenize(node.is);
    }
    this.tokenizeTemplateParameters(node.templateParameters);
    if (node.properties.length) {
      this.punctuation("{", {HasPrefixSpace: true});
      this.indent();
      for (const prop of node.properties) {
        const propName = this.getNameForNode(prop);
        this.namespaceStack.push(propName);
        this.tokenize(prop);
        this.punctuation(";");
        this.namespaceStack.pop();
        this.newline()
      }
      this.deindent();
      this.punctuation("}", {isContextEndLine: true});
    } else {
      this.punctuation("{}", {HasPrefixSpace: true});
    }
    this.namespaceStack.pop();
  }

  private tokenizeScalarStatement(node: ScalarStatementNode) {
    this.namespaceStack.push(node.id.sv);
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    this.keyword("scalar", {HasSuffixSpace: true});
    this.tokenizeIdentifier(node.id, "declaration");
    if (node.extends) {
      this.keyword("extends", {HasSuffixSpace: true, HasPrefixSpace: true});
      this.tokenize(node.extends);
    }
    this.tokenizeTemplateParameters(node.templateParameters);
    this.newline()
    this.namespaceStack.pop();
  }

  private tokenizeInterfaceStatement(node: InterfaceStatementNode) {
    this.namespaceStack.push(node.id.sv);
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    this.keyword("interface", {HasSuffixSpace: true});
    this.tokenizeIdentifier(node.id, "declaration");
    this.tokenizeTemplateParameters(node.templateParameters);
    this.punctuation("{", {HasPrefixSpace: true});
    this.indent();
    for (let x = 0; x < node.operations.length; x++) {
      const op = node.operations[x];
      this.tokenizeOperationStatement(op, true);
      this.blankLines(1)
    }
    this.deindent();
    this.punctuation("}", {isContextEndLine: true});
    this.namespaceStack.pop();
  }

  private tokenizeEnumStatement(node: EnumStatementNode) {
    this.namespaceStack.push(node.id.sv);
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    this.keyword("enum", {HasSuffixSpace: true});
    this.tokenizeIdentifier(node.id, "declaration");
    this.punctuation("{", {HasPrefixSpace: true});
    this.indent();
    for (const member of node.members) {
      const memberName = this.getNameForNode(member);
      this.namespaceStack.push(memberName);
      this.tokenize(member);
      this.punctuation(",");
      this.namespaceStack.pop();
      this.newline()
    }
    this.deindent();
    this.punctuation("}", {isContextEndLine: true});
    this.namespaceStack.pop();
  }

  private tokenizeUnionStatement(node: UnionStatementNode) {
    this.namespaceStack.push(node.id.sv);
    this.tokenizeDecoratorsAndDirectives(node.decorators, node.directives, false);
    this.keyword("union", {HasSuffixSpace: true});
    this.tokenizeIdentifier(node.id, "declaration");
    this.punctuation("{", {HasPrefixSpace: true});
    this.indent();
    for (let x = 0; x < node.options.length; x++) {
      const variant = node.options[x];
      const variantName = this.getNameForNode(variant);
      this.namespaceStack.push(variantName);
      this.tokenize(variant);
      this.namespaceStack.pop();
      if (x !== node.options.length - 1) {
        this.punctuation(",");
      }
      this.newline()
    }
    this.deindent();
    this.punctuation("}", {isContextEndLine: true});
    this.namespaceStack.pop();
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
      this.punctuation("=", {HasSuffixSpace: true, HasPrefixSpace: true});
      this.tokenize(node.default);
    }
  }

  /** Expands and tokenizes a model expression (anonymous model) */
  private tokenizeModelExpression(
    node: ModelExpressionNode,
    options: {isOperationSignature: boolean}) {
      const isOperationSignature = options.isOperationSignature;

      // display {} for empty model or nothing for empty operation signature
      if (!node.properties.length) {
        if (!isOperationSignature) {
          this.punctuation("{}", {HasPrefixSpace: true});
        }
        return;
      }

      if (!isOperationSignature) {
        this.punctuation("{");
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
            this.punctuation(",", {HasSuffixSpace: true, snapTo: "}"});
          }
        } else {
          this.punctuation(";", {HasSuffixSpace: true, snapTo: "}"});
        }
        this.blankLines(0);
      }
      if (!isOperationSignature) {
        this.deindent();
        this.punctuation("}");
        this.newline();
      }
      this.namespaceStack.pop();
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
    this.punctuation(";", {isContextEndLine: true});
    this.namespaceStack.pop();
  }

  private tokenizeNamespaceModel(model: NamespaceModel) {
    this.namespaceStack.push(model.name);
    if (model.node.kind === SyntaxKind.NamespaceStatement) {
      this.tokenizeDecoratorsAndDirectives(model.node.decorators, model.node.directives, false);
    }
    this.keyword("namespace", {HasSuffixSpace: true});
    this.typeDeclaration(model.name, this.namespaceStack.value(), true, {HasSuffixSpace: true});
    this.punctuation("{", {HasPrefixSpace: true});
    this.indent();
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
        this.blankLines(0);
    }
    this.deindent();
    this.punctuation("}", {isContextEndLine: true});
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
    // render each decorator
    for (const node of decorators || []) {
      const isDoc = docDecorators.includes((node.target as IdentifierNode).sv);
      this.tokenize(node);
      if (isDoc) {
        // if any token in a line is documentation, then the whole line is
        for (const token of this.currentLine.Tokens) {
          token.IsDocumentation = true;
        }
      }
      if (!inline) {
        this.newline()
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
            this.typeDeclaration(node.sv, this.namespaceStack.value(), true, {HasSuffixSpace: false});
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

  private tokenizeTemplateParameters(nodes: readonly TemplateParameterDeclarationNode[]) {
    if (nodes.length) {
      this.punctuation("<");
      for (let x = 0; x < nodes.length; x++) {
        const param = nodes[x];
        this.tokenize(param);
        if (x !== nodes.length - 1) {
          this.punctuation(",", {HasSuffixSpace: true});
        }
      }
      this.punctuation(">");
    }
  }

  private tokenizeTemplateArgument(obj: TemplateArgumentNode) {
    if (obj.name) {
      this.text(obj.name.sv);
      this.punctuation("=", {HasSuffixSpace: true, HasPrefixSpace: true});
    }
    if (obj.argument.kind === SyntaxKind.ModelExpression) {
      this.tokenizeModelExpression(obj.argument, {isOperationSignature: false});
    } else {
      this.tokenize(obj.argument);
    }
  }

  private tokenizeReturnType(node: OperationSignatureDeclarationNode, options: { isExpanded: boolean}) {
    if (options.isExpanded && node.parameters.properties.length) {
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

  private definitionIdFor(value: string, prefix: string): string | undefined {
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

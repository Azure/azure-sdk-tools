import { getNamespaceFullName, Namespace, navigateProgram, Program } from "@typespec/compiler";
import { ApiViewDiagnostic, ApiViewDiagnosticLevel } from "./diagnostic.js";
import { NamespaceModel } from "./namespace-model.js";
import { LIB_VERSION } from "./version.js";
import { ApiTreeNode, NodeKind, NodeOptions, NodeTag } from "./api-tree-node.js";

export class ApiView {
  name: string;
  packageName: string;
  crossLanguagePackageId: string | undefined;
  nodes: ApiTreeNode[] = [];
  diagnostics: ApiViewDiagnostic[] = [];
  versionString: string;

  typeDeclarations = new Set<string>();
  includeGlobalNamespace: boolean;

  constructor(name: string, packageName: string, versionString?: string, includeGlobalNamespace?: boolean) {
    this.name = name;
    this.packageName = packageName;
    this.versionString = versionString ?? "";
    this.includeGlobalNamespace = includeGlobalNamespace ?? false;
    this.crossLanguagePackageId = packageName;
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
      node.bottomTokens.push(token);
    }
  }

  child(parent: ApiTreeNode | ApiViewDocument, name: string, id: string, tags?: Tag[]) {
    const child: ApiTreeNode = {
      _kind: "ApiTreeNode",
      name: name,
      id: id,
      kind: "",
      tags: tags ? new Set([...tags.toString()]) : undefined,
      properties: new Map<string, string>(),
      topTokens: [],
      bottomTokens: [],
      children: []
    };
    if (parent._kind === "ApiViewDocument") {
      if (parent.apiForest === null) {
        parent.apiForest = [];
      }
      parent.apiForest.push(child);
    } else if (parent._kind === "ApiTreeNode") {
      parent.children.push(child);
    }
  }

  whitespace(count: number = 1) {
    this.tokens.push({
      Kind: ApiViewTokenKind.Whitespace,
      Value: WHITESPACE.repeat(count),
    });
  }

  space() {
    if (this.tokens[this.tokens.length - 1]?.Kind !== ApiViewTokenKind.Whitespace) {
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
  }

  blankLines(count: number) {
    // count the number of trailing newlines (ignoring indent whitespace)
    let newlineCount: number = 0;
    for (let i = this.tokens.length; i > 0; i--) {
      const token = this.tokens[i - 1];
      if (token.Kind === ApiViewTokenKind.Newline) {
        newlineCount++;
      } else if (token.Kind === ApiViewTokenKind.Whitespace) {
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
        if (popped?.Kind === ApiViewTokenKind.Newline) {
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

  lineMarker(addCrossLanguageId: boolean = false) {
    const token = {
      Kind: ApiViewTokenKind.LineIdMarker,
      DefinitionId: this.namespaceStack.value(),
      CrossLanguageDefinitionId: addCrossLanguageId ? this.namespaceStack.value() : undefined,
    };
    this.tokens.push(token);
  }

  text(text: string) {
    const token = {
      Kind: ApiViewTokenKind.Text,
      Value: text,
    };
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

  typeDeclaration(typeName: string, typeId: string | undefined, addCrossLanguageId: boolean) {
    if (typeId) {
      if (this.typeDeclarations.has(typeId)) {
        throw new Error(`Duplication ID "${typeId}" for declaration will result in bugs.`);
      }
      this.typeDeclarations.add(typeId);
    }
    this.tokens.push({
      Kind: ApiViewTokenKind.TypeName,
      DefinitionId: typeId,
      Value: typeName,
      CrossLanguageDefinitionId: addCrossLanguageId ? typeId : undefined,
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
    const lines = value.split("\n");
    if (lines.length === 1) {
      this.tokens.push({
        Kind: ApiViewTokenKind.StringLiteral,
        Value: `\u0022${value}\u0022`,
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

  literal(value: string) {
    this.tokens.push({
      Kind: ApiViewTokenKind.StringLiteral,
      Value: value,
    });
  }

  diagnostic(message: string, targetId: string, level: ApiViewDiagnosticLevel) {
    this.diagnostics.push(new ApiViewDiagnostic(message, targetId, level));
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

    this.emitHeader();
    for (const [name, ns] of allNamespaces.entries()) {
      if (!this.shouldEmitNamespace(name)) {
        continue;
      }
      // use a fake name to make the global namespace clear
      const namespaceName = name === "" ? "::GLOBAL::" : name;
      const nsModel = new NamespaceModel(namespaceName, ns, program);
      if (nsModel.shouldEmit()) {
        nsModel.tokenize(this);
      }
    }
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
    const globalId = "GLOBAL";
    const node = this.node(this, globalId, globalId, "Preamble", { tags: [NodeTag.hideFromNav, NodeTag.skipDiff] });
    node.literal(headerText);
    // TODO: Source URL?
    this.token(ApiViewTokenKind.SkipDiffRangeEnd);
    this.blankLines(2);
  }
}

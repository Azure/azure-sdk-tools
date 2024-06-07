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
      parent.children.push(child);
    }
    return child;
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
    node.blankLines(2);
  }
}

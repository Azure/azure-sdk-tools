import {
  AliasStatementNode,
  Namespace,
  ModelStatementNode,
  OperationStatementNode,
  InterfaceStatementNode,
  EnumStatementNode,
  NamespaceStatementNode,
  ModelExpressionNode,
  IntersectionExpressionNode,
  ProjectionModelExpressionNode,
  SyntaxKind,
  UnionStatementNode,
  UnionExpressionNode,
  AugmentDecoratorStatementNode,
  Program,
  ScalarStatementNode,
  JsNamespaceDeclarationNode,
} from "@typespec/compiler";
import { caseInsensitiveSort, findNodes, generateId } from "./helpers.js";
import { ApiView } from "./apiview.js";
import { TokenLocation } from "./structured-token.js";
import { NodeKind, NodeTag } from "./api-tree-node.js";

export class NamespaceModel {
  kind = SyntaxKind.NamespaceStatement;
  name: string;
  node: NamespaceStatementNode | JsNamespaceDeclarationNode;
  operations = new Map<string, OperationStatementNode | InterfaceStatementNode>();
  resources = new Map<
    string,
    | AliasStatementNode
    | ModelStatementNode
    | ModelExpressionNode
    | IntersectionExpressionNode
    | ProjectionModelExpressionNode
    | EnumStatementNode
    | ScalarStatementNode
    | UnionStatementNode
    | UnionExpressionNode
  >();
  models = new Map<
    string,
    | ModelStatementNode
    | ModelExpressionNode
    | IntersectionExpressionNode
    | ProjectionModelExpressionNode
    | EnumStatementNode
    | ScalarStatementNode
    | UnionStatementNode
    | UnionExpressionNode
  >();
  aliases = new Map<string, AliasStatementNode>();
  augmentDecorators = new Array<AugmentDecoratorStatementNode>();

  constructor(name: string, ns: Namespace, program: Program) {
    this.name = name;
    this.node = ns.node;

    // Gather operations
    for (const [opName, op] of ns.operations) {
      this.operations.set(opName, op.node);
    }
    for (const [intName, int] of ns.interfaces) {
      this.operations.set(intName, int.node);
    }

    // Gather models and resources
    for (const [modelName, model] of ns.models) {
      if (this.node !== undefined) {
        let isResource = false;
        for (const dec of model.decorators) {
          if (dec.decorator.name === "$resource") {
            isResource = true;
            break;
          }
        }
        if (isResource) {
          this.resources.set(modelName, model.node);
        } else {
          this.models.set(modelName, model.node);
        }
      } else {
        throw new Error("Unexpectedly found undefined model node.");
      }
    }
    for (const [enumName, en] of ns.enums) {
      this.models.set(enumName, en.node);
    }
    for (const [unionName, un] of ns.unions) {
      this.models.set(unionName, un.node);
    }
    for (const [scalarName, sc] of ns.scalars) {
      this.models.set(scalarName, sc.node);
    }

    // Gather aliases
    for (const alias of findNodes(SyntaxKind.AliasStatement, program, ns)) {
      this.aliases.set(alias.id.sv, alias);
    }

    // collect augment decorators
    for (const augment of findNodes(SyntaxKind.AugmentDecoratorStatement, program, ns)) {
      this.augmentDecorators.push(augment);
    }

    // sort operations and models
    this.operations = new Map([...this.operations].sort(caseInsensitiveSort));
    this.resources = new Map([...this.resources].sort(caseInsensitiveSort));
    this.models = new Map([...this.models].sort(caseInsensitiveSort));
    this.aliases = new Map([...this.aliases].sort(caseInsensitiveSort));
  }

  /**
   * Don't emit an empty namespace
   * @returns true if there are models, resources or operations
   */
  shouldEmit(): boolean {
    return (
      (this.node as NamespaceStatementNode).decorators !== undefined ||
      this.models.size > 0 ||
      this.operations.size > 0 ||
      this.resources.size > 0
    );
  }

  tokenize(apiview: ApiView) {
    const treeNode = apiview.node(apiview, this.name, this.name, NodeKind.namespace);

    if (this.node.kind === SyntaxKind.NamespaceStatement) {
      treeNode.tokenizeDecoratorsAndDirectives(this.node.decorators, this.node.directives, false);
    }
    treeNode.keyword("namespace", { postfixSpace: true });
    treeNode.typeDeclaration(this.name, this.name, true);
    for (const node of this.augmentDecorators) {
      const nodeId = generateId(node)!;
      const subNode = apiview.node(treeNode, nodeId, nodeId, "augmentDecorator", { tags: [NodeTag.hideFromNav] });
      subNode.tokenize(node);
      subNode.blankLines(1);
    }
    for (const node of this.operations.values()) {
      const nodeId = generateId(node)!;
      const subNode = apiview.node(treeNode, nodeId, nodeId, NodeKind.method);
      subNode.tokenize(node);
      subNode.blankLines(1);
    }
    for (const node of this.resources.values()) {
      const nodeId = generateId(node)!;
      const subNode = apiview.node(treeNode, nodeId, nodeId, NodeKind.class);
      subNode.tokenize(node);
      subNode.blankLines(1);
    }
    for (const node of this.models.values()) {
      const nodeId = generateId(node)!;
      const subNode = apiview.node(treeNode, nodeId, nodeId, NodeKind.class);
      subNode.tokenize(node);
      subNode.blankLines(1);
    }
    for (const node of this.aliases.values()) {
      const nodeId = generateId(node)!;
      const subNode = apiview.node(treeNode, nodeId, nodeId, NodeKind.type);
      subNode.tokenize(node);
      subNode.punctuation(";");
      subNode.blankLines(1);
    }
    treeNode.punctuation("}", { location: TokenLocation.bottom });
  }
}

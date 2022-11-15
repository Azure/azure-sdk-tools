import {
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
  BaseNode,
  IdentifierNode,
  ModelPropertyNode,
  EnumMemberNode,
  ModelSpreadPropertyNode,
  EnumSpreadMemberNode,
  DecoratorExpressionNode,
  MemberExpressionNode,
  UnionStatementNode,
  UnionExpressionNode,
  UnionVariantNode,
} from "@cadl-lang/compiler";

export class NamespaceModel {
  kind = SyntaxKind.NamespaceStatement;
  name: string;
  node: NamespaceStatementNode;
  operations = new Map<string, OperationStatementNode | InterfaceStatementNode>();
  resources = new Map<
    string,
    | ModelStatementNode
    | ModelExpressionNode
    | IntersectionExpressionNode
    | ProjectionModelExpressionNode
    | EnumStatementNode
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
    | UnionStatementNode
    | UnionExpressionNode
  >();

  constructor(name: string, ns: Namespace) {
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
      if (model.node != undefined) {
        let isResource = false;
        for (const dec of model.decorators) {
          if (dec.decorator.name == "$resource") {
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

    // sort operations and models
    this.operations = new Map([...this.operations].sort());
    this.resources = new Map([...this.resources].sort());
    this.models = new Map([...this.models].sort());
  }

  /**
   * Don't emit an empty namespace
   * @returns true if there are models, resources or operations
   */
  shouldEmit(): boolean {
    return (this.models.size > 0 || this.operations.size > 0 || this.resources.size > 0);
  }
}

export function generateId(obj: BaseNode | NamespaceModel | undefined): string | undefined {
  let node;
  if (obj == undefined) {
    return undefined;
  }
  if (obj instanceof NamespaceModel) {
    return obj.name;
  }
  let name: string;
  let parentId: string | undefined;
  switch (obj.kind) {
    case SyntaxKind.NamespaceStatement:
      node = obj as NamespaceStatementNode;
      name = node.id.sv;
      parentId = generateId(node.parent);
      break;
    case SyntaxKind.DecoratorExpression:
      node = obj as DecoratorExpressionNode;
      switch (node.target.kind) {
        case SyntaxKind.Identifier:
          return `@${node.target.sv}`;
        case SyntaxKind.MemberExpression:
          return generateId(node.target);
      }
      break;
    case SyntaxKind.EnumMember:
      node = obj as EnumMemberNode;
      switch (node.id.kind) {
        case SyntaxKind.Identifier:
          name = node.id.sv;
          break;
        case SyntaxKind.StringLiteral:
          name = node.id.value;
          break;
      }
      parentId = generateId(node.parent);
      break;
    case SyntaxKind.EnumSpreadMember:
      node = obj as EnumSpreadMemberNode;
      return generateId(node.target);
    case SyntaxKind.EnumStatement:
      node = obj as EnumStatementNode;
      name = node.id.sv;
      parentId = generateId(node.parent);
      break;
    case SyntaxKind.Identifier:
      node = obj as IdentifierNode;
      return node.sv;
    case SyntaxKind.InterfaceStatement:
      node = obj as InterfaceStatementNode;
      name = node.id.sv;
      parentId = generateId(node.parent);
      break;
    case SyntaxKind.MemberExpression:
      node = obj as MemberExpressionNode;
      name = node.id.sv;
      parentId = generateId(node.base);
      break;
    case SyntaxKind.ModelProperty:
      node = obj as ModelPropertyNode;
      switch (node.id.kind) {
        case SyntaxKind.Identifier:
          name = node.id.sv;
          break;
        case SyntaxKind.StringLiteral:
          name = node.id.value;
          break;
      }
      parentId = generateId(node.parent);
      break;
    case SyntaxKind.ModelSpreadProperty:
      node = obj as ModelSpreadPropertyNode;
      switch (node.target.target.kind) {
        case SyntaxKind.Identifier:
          name = (node.target.target as IdentifierNode).sv;
          break;
        case SyntaxKind.MemberExpression:
          name = (node.target.target as MemberExpressionNode).id.sv;
          break;
      }
      parentId = generateId(node.parent);
      break;
    case SyntaxKind.ModelStatement:
      node = obj as ModelStatementNode;
      name = node.id.sv;
      parentId = generateId(node.parent);
      break;
    case SyntaxKind.OperationStatement:
      node = obj as OperationStatementNode;
      name = node.id.sv;
      parentId = generateId(node.parent);
      break;
    case SyntaxKind.UnionStatement:
      node = obj as UnionStatementNode;
      name = node.id.sv;
      parentId = generateId(node.parent);
      break;
    case SyntaxKind.UnionVariant:
      node = obj as UnionVariantNode;
      switch (node.id.kind) {
        case SyntaxKind.Identifier:
          name = node.id.sv;
          break;
        case SyntaxKind.StringLiteral:
          name = node.id.value;
          break;
      }
      parentId = generateId(node.parent);
      break;
    default:
      return undefined;
  }
  if (parentId != undefined) {
    return `${parentId}.${name}`;
  } else {
    return name;
  }
}

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
} from "@cadl-lang/compiler";

export class NamespaceModel {
  kind = SyntaxKind.NamespaceStatement;
  name: string;
  node: NamespaceStatementNode;
  operations = new Map<string, OperationStatementNode | InterfaceStatementNode>();
  models = new Map<
    string,
    | ModelStatementNode
    | ModelExpressionNode
    | IntersectionExpressionNode
    | ProjectionModelExpressionNode
    | EnumStatementNode
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

    // Gather models
    for (const [modelName, model] of ns.models) {
      if (model.node != undefined) {
        this.models.set(modelName, model.node);
      } else {
        throw new Error("Unexpectedly found undefined model node.");
      }
    }
    for (const [enumName, en] of ns.enums) {
      this.models.set(enumName, en.node);
    }

    // sort operations and models
    this.operations = new Map([...this.operations].sort());
    this.models = new Map([...this.models].sort());
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
      return generateId(node.target);
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
    default:
      return undefined;
  }
  if (parentId != undefined) {
    return `${parentId}.${name}`;
  } else {
    return name;
  }
}

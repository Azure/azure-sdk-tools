import {
  BaseNode,
  BooleanLiteralNode,
  DecoratorExpressionNode,
  DirectiveExpressionNode,
  EnumMemberNode,
  EnumSpreadMemberNode,
  EnumStatementNode,
  Expression,
  getSourceLocation,
  IdentifierNode,
  InterfaceStatementNode,
  MemberExpressionNode,
  ModelPropertyNode,
  ModelSpreadPropertyNode,
  ModelStatementNode,
  Namespace,
  NamespaceStatementNode,
  Node,
  NumericLiteralNode,
  OperationStatementNode,
  Program,
  StringLiteralNode,
  StringTemplateExpressionNode,
  SyntaxKind,
  TypeReferenceNode,
  UnionStatementNode,
  UnionVariantNode,
  visitChildren,
} from "@typespec/compiler";
import { NamespaceModel } from "./namespace-model.js";

export function buildExpressionString(node: Expression) {
  switch (node.kind) {
    case SyntaxKind.StringLiteral:
      return `"${(node as StringLiteralNode).value}"`;
    case SyntaxKind.NumericLiteral:
      return (node as NumericLiteralNode).value.toString();
    case SyntaxKind.BooleanLiteral:
      return (node as BooleanLiteralNode).value.toString();
    case SyntaxKind.StringTemplateExpression:
      return buildTemplateString(node as StringTemplateExpressionNode);
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
          return getFullyQualifiedIdentifier(obj.target as MemberExpressionNode);
      }
    // eslint-disable-next-line no-fallthrough
    default:
      throw new Error(`Unsupported expression kind: ${SyntaxKind[node.kind]}`);
    //unsupported ArrayExpressionNode | MemberExpressionNode | ModelExpressionNode | TupleExpressionNode | UnionExpressionNode | IntersectionExpressionNode | TypeReferenceNode | ValueOfExpressionNode | AnyKeywordNode;
  }
}

/** Constructs a single string with template markers. */
export function buildTemplateString(node: StringTemplateExpressionNode): string {
  let result = node.head.value;
  for (const span of node.spans) {
    result += "${" + buildExpressionString(span.expression) + "}";
    result += span.literal.value;
  }
  return result;
}

export function caseInsensitiveSort(a: [string, any], b: [string, any]): number {
  const aLower = a[0].toLowerCase();
  const bLower = b[0].toLowerCase();
  return aLower > bLower ? 1 : aLower < bLower ? -1 : 0;
}

export function definitionIdFor(value: string, prefix: string): string | undefined {
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

export function findNodes<T extends SyntaxKind>(
  kind: T,
  program: Program,
  namespace: Namespace,
): (Node & { kind: T })[] {
  const nodes: Node[] = [];
  for (const file of program.sourceFiles.values()) {
    visitChildren(file, function visit(node) {
      if (node.kind === kind && inNamespace(node, program, namespace)) {
        nodes.push(node);
      }
      visitChildren(node, visit);
    });
  }
  return nodes as any;
}

/**
 * Generated an identifier for the given object.
 * @param obj to generate the ID for
 * @returns id
 */
export function generateId(obj: BaseNode | NamespaceModel | undefined): string | undefined {
  let node;
  if (obj === undefined) {
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
    // eslint-disable-next-line no-fallthrough
    case SyntaxKind.EnumMember:
      node = obj as EnumMemberNode;
      name = node.id.sv;
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
      name = node.id.sv;
      parentId = generateId(node.parent);
      break;
    case SyntaxKind.ModelSpreadProperty:
      node = obj as ModelSpreadPropertyNode;
      name = generateId(node.target)!;
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
    case SyntaxKind.StringLiteral:
      node = obj as StringLiteralNode;
      name = node.value;
      parentId = undefined;
      break;
    case SyntaxKind.UnionStatement:
      node = obj as UnionStatementNode;
      name = node.id.sv;
      parentId = generateId(node.parent);
      break;
    case SyntaxKind.UnionVariant:
      node = obj as UnionVariantNode;
      if (node.id?.sv !== undefined) {
        name = node.id.sv;
      } else {
        // TODO: Should never have default value of _
        name = generateId(node.value) ?? "_";
      }
      parentId = generateId(node.parent);
      break;
    case SyntaxKind.TypeReference:
      node = obj as TypeReferenceNode;
      name = generateId(node.target)!;
      parentId = undefined;
      break;
    case SyntaxKind.DirectiveExpression:
      node = obj as DirectiveExpressionNode;
      name = `#${generateId(node.target)!}`;
      for (const arg of node.arguments) {
        name += `_${generateId(arg)}`;
      }
      parentId = generateId(node.parent);
      break;
    default:
      return undefined;
  }
  if (parentId !== undefined) {
    return `${parentId}.${name}`;
  } else {
    return name;
  }
}

export function getFullyQualifiedIdentifier(node: MemberExpressionNode, suffix?: string): string {
  switch (node.base.kind) {
    case SyntaxKind.Identifier:
      return `${node.base.sv}.${suffix}`;
    case SyntaxKind.MemberExpression:
      return getFullyQualifiedIdentifier(node.base, `${node.base.id.sv}.${suffix}`);
  }
}

export function getRawText(node: IdentifierNode): string {
  return getSourceLocation(node).file.text.slice(node.pos, node.end);
}

export function inNamespace(node: Node, program: Program, namespace: Namespace): boolean {
  for (let n: Node | undefined = node; n; n = n.parent) {
    switch (n.kind) {
      case SyntaxKind.NamespaceStatement:
        return program.checker.getTypeForNode(n) === namespace;
      case SyntaxKind.TypeSpecScript:
        if (n.inScopeNamespaces.length > 0 && inNamespace(n.inScopeNamespaces[0], program, namespace)) {
          return true;
        }
        return false;
    }
  }
  return false;
}

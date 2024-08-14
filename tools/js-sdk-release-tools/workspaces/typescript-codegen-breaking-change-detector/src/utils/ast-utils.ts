import { ParserServices, ParserServicesWithTypeInformation } from '@typescript-eslint/typescript-estree';
import { Scope, ScopeManager } from '@typescript-eslint/scope-manager';

import { TSESTree } from '@typescript-eslint/types';
import { findVariable } from '@typescript-eslint/utils/ast-utils';
import { logger } from '../logging/logger.js';
import {
  InterfaceDeclaration,
  TypeAliasDeclaration,
  Node,
  createWrappedNode,
  TypeReferenceNode,
  SyntaxKind,
  EnumDeclaration,
} from 'ts-morph';

function tryFindDeclaration<TNode extends TSESTree.Node>(
  name: string,
  scope: Scope,
  typeGuard: (node: TSESTree.Node) => node is TNode,
  shouldLog: boolean = true
): TNode | undefined {
  const variable = findVariable(scope as Scope, name);
  const node = variable?.defs?.[0]?.node;
  if (!node) {
    if (shouldLog) logger.warn(`Failed to find ${name}'s declaration`);
    return undefined;
  }
  if (!typeGuard(node)) {
    if (shouldLog) logger.warn(`Found ${name}'s declaration but with another node type "${node.type}"`);
    return undefined;
  }
  return node;
}

function isTypeAliasDeclarationNode(node: TSESTree.Node): node is TSESTree.TSTypeAliasDeclaration {
  return node.type === TSESTree.AST_NODE_TYPES.TSTypeAliasDeclaration;
}

function isEnumDeclarationNode(node: TSESTree.Node): node is TSESTree.TSEnumDeclaration {
  return node.type === TSESTree.AST_NODE_TYPES.TSEnumDeclaration;
}

function getTypeReferencesUnder(node: Node) {
  const types: TypeReferenceNode[] = [];
  if (!node) return types;
  node.forEachChild((child) => {
    if (Node.isTypeReference(child)) {
      types.push(child);
    }
    const childTypeAliases = getTypeReferencesUnder(child);
    types.push(...childTypeAliases);
  });
  return types;
}

function handleTypeReference<
  TEsDeclaration extends TSESTree.TSInterfaceDeclaration | TSESTree.TSTypeAliasDeclaration | TSESTree.TSEnumDeclaration,
  TDeclaration extends TypeAliasDeclaration | InterfaceDeclaration | EnumDeclaration,
>(
  r: TypeReferenceNode,
  scope: Scope,
  service: ParserServicesWithTypeInformation,
  list: TDeclaration[],
  findNext: (node: Node) => void,
  kind: SyntaxKind,
  typeGuardForEs: (node: TSESTree.Node) => node is TEsDeclaration,
  typeGuardForMo: (node: Node) => node is TDeclaration
) {
  const esDeclaration = tryFindDeclaration(r.getText(), scope, typeGuardForEs, false);
  if (!esDeclaration) return;
  const declaration = convertToMorphNode(esDeclaration, service).asKindOrThrow(kind);
  if (!typeGuardForMo(declaration)) {
    throw new Error(`Failed to find expected type from reference ${r.getText()}`);
  }
  list.push(declaration);
  findNext(declaration);
}

export function getGlobalScope(scopeManager: ScopeManager | null): Scope {
  const globalScope = scopeManager?.globalScope;
  if (!globalScope) throw new Error(`Failed to find global scope`);
  return globalScope;
}

export function findDeclaration<TNode extends TSESTree.Node>(
  name: string,
  scope: Scope,
  typeGuard: (node: TSESTree.Node) => node is TNode
): TNode {
  const node = tryFindDeclaration(name, scope, typeGuard);
  if (!node) throw new Error(`Failed to find "${name}"`);
  return node;
}

export function isParseServiceWithTypeInfo(service: ParserServices): service is ParserServicesWithTypeInformation {
  return service.program !== null;
}

export function isInterfaceDeclarationNode(node: TSESTree.Node): node is TSESTree.TSInterfaceDeclaration {
  return node.type === TSESTree.AST_NODE_TYPES.TSInterfaceDeclaration;
}

export function convertToMorphNode(node: TSESTree.Node, service: ParserServicesWithTypeInformation) {
  const tsNode = service.esTreeNodeToTSNodeMap.get(node);
  const typeChecker = service.program.getTypeChecker();
  const moNode = createWrappedNode(tsNode, { typeChecker });
  return moNode;
}

export function findAllRenameableDeclarationsUnder(
  node: Node,
  scope: Scope,
  service: ParserServicesWithTypeInformation
): { interfaces: Set<InterfaceDeclaration>; typeAliases: Set<TypeAliasDeclaration>; enums: Set<EnumDeclaration> } {
  const interfaces = new Set<InterfaceDeclaration>();
  const typeAliases = new Set<TypeAliasDeclaration>();
  const enums = new Set<EnumDeclaration>();
  if (!node) return { interfaces, typeAliases, enums };
  console.log("🚀 ~ node:", node.getText());
  const findNext = (node: Node) => {
    const result = findAllRenameableDeclarationsUnder(node, scope, service);
    
    interfaces.(result.interfaces);
    typeAliases.add(result.typeAliases);
    enums.push(...result.enums);
  };

  const references = getTypeReferencesUnder(node);
  references.forEach((r) => {
    handleTypeReference(
      r,
      scope,
      service,
      interfaces,
      findNext,
      SyntaxKind.InterfaceDeclaration,
      isInterfaceDeclarationNode,
      Node.isInterfaceDeclaration
    );
    handleTypeReference(
      r,
      scope,
      service,
      typeAliases,
      findNext,
      SyntaxKind.TypeAliasDeclaration,
      isTypeAliasDeclarationNode,
      Node.isTypeAliasDeclaration
    );
    handleTypeReference(
      r,
      scope,
      service,
      enums,
      findNext,
      SyntaxKind.EnumDeclaration,
      isEnumDeclarationNode,
      Node.isEnumDeclaration
    );
  });
  return { interfaces, typeAliases, enums };
}

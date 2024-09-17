import {
  EnumDeclaration,
  InterfaceDeclaration,
  Node,
  SyntaxKind,
  TypeAliasDeclaration,
  TypeReferenceNode,
  createWrappedNode,
} from 'ts-morph';
import { ParserServices, ParserServicesWithTypeInformation } from '@typescript-eslint/typescript-estree';
import { Scope, ScopeManager } from '@typescript-eslint/scope-manager';

import { RenameAbleDeclarations } from '../azure/common/types';
import { TSESTree } from '@typescript-eslint/types';
import { findVariable } from '@typescript-eslint/utils/ast-utils';
import { logger } from '../logging/logger';

function tryFindDeclaration<TNode extends TSESTree.Node>(
  name: string,
  scope: Scope,
  typeGuard: ((node: TSESTree.Node) => node is TNode) | undefined,
  shouldLog: boolean = true
): TNode | undefined {
  const variable = findVariable(scope as Scope, name);
  const node = variable?.defs?.[0]?.node;
  if (!node) {
    if (shouldLog) logger.warn(`Failed to find ${name}'s declaration`);
    return undefined;
  }
  if (typeGuard && !typeGuard(node)) {
    if (shouldLog) logger.warn(`Found ${name}'s declaration but with another node type "${node.type}"`);
    return undefined;
  }
  return node as TNode;
}

function getAllTypeReferencesInNode(node: Node, found: Set<string>) {
  const types: TypeReferenceNode[] = [];
  if (!node) return types;
  node.forEachChild((child) => {
    if (Node.isTypeReference(child) && !found.has(child.getText())) {
      types.push(child);
    }

    const childTypes = getAllTypeReferencesInNode(child, found);
    childTypes.forEach((c) => {
      types.push(c);
    });
  });
  return types;
}

function updateRenameAbleDeclarations(
  declaration: TypeAliasDeclaration | InterfaceDeclaration | EnumDeclaration,
  renameAbleDeclarations: RenameAbleDeclarations,
  found: Set<string>
) {
  const foundDeclarations = new Set<string>(
    [...renameAbleDeclarations.interfaces, ...renameAbleDeclarations.typeAliases, ...renameAbleDeclarations.enums].map(
      (i) => i.getName()
    )
  );
  if (foundDeclarations.has(declaration.getName())) return;
  switch (declaration.getKind()) {
    case SyntaxKind.InterfaceDeclaration:
      renameAbleDeclarations.interfaces.push(declaration as InterfaceDeclaration);
      break;
    case SyntaxKind.TypeAliasDeclaration:
      renameAbleDeclarations.typeAliases.push(declaration as TypeAliasDeclaration);
      break;
    case SyntaxKind.EnumDeclaration:
      renameAbleDeclarations.enums.push(declaration as EnumDeclaration);
      break;
  }
  found.add(declaration.getName());
}

function findDeclarationOfTypeReference(
  reference: TypeReferenceNode,
  scope: Scope,
  service: ParserServicesWithTypeInformation
): (TypeAliasDeclaration | InterfaceDeclaration | EnumDeclaration) | undefined {
  const esDeclaration = tryFindDeclaration(reference.getText(), scope, undefined, false);
  if (!esDeclaration) return;
  const msDeclaration = convertToMorphNode(esDeclaration, service);
  const msDeclarationKind = msDeclaration.getKind();
  if (
    msDeclarationKind !== SyntaxKind.InterfaceDeclaration &&
    msDeclarationKind !== SyntaxKind.TypeAliasDeclaration &&
    msDeclarationKind !== SyntaxKind.EnumDeclaration
  )
    return;
  const declaration = msDeclaration as TypeAliasDeclaration | InterfaceDeclaration | EnumDeclaration;
  return declaration;
}

function findAllRenameAbleDeclarationsInNodeCore(
  node: Node,
  scope: Scope,
  service: ParserServicesWithTypeInformation,
  renameAbleDeclarations: RenameAbleDeclarations,
  found: Set<string>
): void {
  if (!node) return;

  const references = getAllTypeReferencesInNode(node, found);
  references.forEach((reference) => {
    const declaration = findDeclarationOfTypeReference(reference, scope, service);
    if (!declaration) return;
    updateRenameAbleDeclarations(declaration, renameAbleDeclarations, found);
    findAllRenameAbleDeclarationsInNodeCore(declaration, scope, service, renameAbleDeclarations, found);
  });
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

export function findAllRenameAbleDeclarationsInNode(
  node: Node,
  scope: Scope,
  service: ParserServicesWithTypeInformation
): { interfaces: InterfaceDeclaration[]; typeAliases: TypeAliasDeclaration[]; enums: EnumDeclaration[] } {
  const renameAbleDeclarations: RenameAbleDeclarations = {
    interfaces: [],
    typeAliases: [],
    enums: [],
  };
  const found = new Set<string>();
  findAllRenameAbleDeclarationsInNodeCore(node, scope, service, renameAbleDeclarations, found);
  return renameAbleDeclarations;
}

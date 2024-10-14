import { RuleListener, RuleModule } from '@typescript-eslint/utils/eslint-utils';

import { ParserServices } from '@typescript-eslint/parser';
import type { ScopeManager } from '@typescript-eslint/scope-manager';
import { TSESTree } from '@typescript-eslint/utils';
import type { VisitorKeys } from '@typescript-eslint/visitor-keys';
import {
  EnumDeclaration,
  InterfaceDeclaration,
  Node,
  Signature,
  SourceFile,
  TypeAliasDeclaration,
  TypeNode,
} from 'ts-morph';

export interface ParseForESLintResult {
  ast: TSESTree.Program & {
    range?: [number, number];
    tokens?: TSESTree.Token[];
    comments?: TSESTree.Comment[];
  };
  services: ParserServices;
  visitorKeys: VisitorKeys;
  scopeManager: ScopeManager;
}

export interface CreateOperationRule {
  (baselineParsedResult: ParseForESLintResult | undefined): RuleModule<'default', readonly unknown[], RuleListener>;
}

export interface RuleMessage {
  id: string;
  kind: RuleMessageKind;
}

export enum RuleMessageKind {
  InlineDeclarationNameSetMessage = 'InlineDeclarationNameSetMessage',
}

export interface InlineDeclarationNameSetMessage extends RuleMessage {
  baseline: Map<string, NodeContext>;
  current: Map<string, NodeContext>;
  kind: RuleMessageKind.InlineDeclarationNameSetMessage;
}

export interface LinterSettings {
  reportInlineDeclarationNameSetMessage(message: InlineDeclarationNameSetMessage): void;
}

export interface NodeContext {
  node: InterfaceDeclaration | TypeAliasDeclaration | EnumDeclaration;
  used: boolean;
}

export interface RenameAbleDeclarations {
  interfaces: InterfaceDeclaration[];
  typeAliases: TypeAliasDeclaration[];
  enums: EnumDeclaration[];
}

export interface AstContext {
  baseline: SourceFile;
  current: SourceFile;
}

export interface NameNode {
  name: string;
  node: Node | TypeNode;
}

export enum BreakingReasons {
  None = 0,
  Removed = 1,
  TypeChanged = 2,
  CountChanged = 4,
  RequiredToOptional = 8,
  ReadonlyToMutable = 16,
}

export interface BreakingPair {
  target?: NameNode;
  source?: NameNode;
  location: BreakingLocation;
  reasons: BreakingReasons;
  messages: Map<BreakingReasons, string>;
  modelType: ModelType;
}

export enum BreakingLocation {
  None,
  Call,
  Function,
  FunctionOverload,
  FunctionReturnType,
  FunctionParameterList,
  FunctionParameter,
  ClassicProperty,
  TypeAlias,
  Interface,
}

export enum ModelType {
  None,
  Input,
  Output,
}

export type FindMappingCallSignature = (
  target: Signature,
  signatures: Signature[]
) => { signature: Signature; id: string } | undefined;

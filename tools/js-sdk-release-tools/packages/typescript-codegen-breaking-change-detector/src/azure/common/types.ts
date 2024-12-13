import { RuleListener, RuleModule } from '@typescript-eslint/utils/eslint-utils';

import { ParserServices } from '@typescript-eslint/parser';
import type { ScopeManager } from '@typescript-eslint/scope-manager';
import { TSESTree } from '@typescript-eslint/utils';
import type { VisitorKeys } from '@typescript-eslint/visitor-keys';
import {
  EnumDeclaration,
  FunctionDeclaration,
  InterfaceDeclaration,
  ParameterDeclaration,
  Signature,
  SourceFile,
  TypeAliasDeclaration,
  Node,
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
  node: Node;
}

export enum DiffReasons {
  None = 0,

  // breaking changes
  Removed = 2 ** 0,
  TypeChanged = 2 ** 1,
  CountChanged = 2 ** 2,
  NameChanged = 2 ** 3,
  RequiredToOptional = 2 ** 4,
  ReadonlyToMutable = 2 ** 5,

  // new features
  Added = 2 ** 10,
}

export interface DiffPair {
  target?: NameNode;
  source?: NameNode;
  location: DiffLocation;
  reasons: DiffReasons;
  messages: Map<DiffReasons, string>;
  assignDirection: AssignDirection;
}

// NOTE: When there is a '_', the first word indicate the node type
export enum DiffLocation {
  None,
  // NOTE: Signatue includes method/arrow-function/call-signature in interface
  //       Signatue includes method/arrow-function/constructor class
  //       Signatue includes function/arrow-function
  Signature,
  Signature_Overload,
  Signature_ReturnType,
  Signature_ParameterList,
  Parameter,
  Property,
  TypeAlias,
  Interface,
}

export enum AssignDirection {
  None,
  BaselineToCurrent, // e.g. input model
  CurrentToBaseline, // e.g. output model
}

export type FindMappingCallSignature = (
  target: Signature,
  signatures: Signature[]
) => { signature: Signature; id: string } | undefined;

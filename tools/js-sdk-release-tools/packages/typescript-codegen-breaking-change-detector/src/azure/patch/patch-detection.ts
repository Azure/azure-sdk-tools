import { FunctionDeclaration, Node, Signature, SourceFile, SyntaxKind } from 'ts-morph';
import { AstContext, DiffLocation, DiffPair, DiffReasons, AssignDirection } from '../common/types';
import {
  findFunctionBreakingChanges,
  findInterfaceBreakingChanges,
  findTypeAliasBreakingChanges,
  checkRemovedDeclaration,
  createDiffPair,
  checkAddedDeclaration,
} from '../diff/declaration-diff';
import { logger } from '../../logging/logger';

function findMappingCallSignature(
  target: Signature,
  signatures: Signature[]
): { signature: Signature; id: string } | undefined {
  const path = target
    .getParameters()
    .find((p) => p.getName() === 'path')
    ?.getValueDeclarationOrThrow()
    .getText();
  if (!path) throw new Error(`Failed to find path in signature: ${target.getDeclaration().getText()}`);

  const foundPaths = signatures.filter((p) => {
    const foundPath = p
      .getParameters()
      .find((p) => p.getName() === 'path')
      ?.getValueDeclarationOrThrow()
      .getText();
    return foundPath && path === foundPath;
  });

  if (foundPaths.length === 0) return undefined;
  if (foundPaths.length > 1) logger.warn(`Found more than one mapping call signature for path '${path}'`);
  return { signature: foundPaths[0], id: path };
}

export function patchRoutes(astContext: AstContext): DiffPair[] {
  const baseline = astContext.baseline.getInterface('Routes');
  const current = astContext.current.getInterface('Routes');

  if (!baseline && !current) throw new Error(`Failed to find interface 'Routes' in baseline and current package`);

  const addPair = checkAddedDeclaration(DiffLocation.Interface, baseline, current);
  if (addPair) return [addPair];

  const removePair = checkRemovedDeclaration(DiffLocation.Interface, baseline, current);
  if (removePair) return [removePair];

  const breakingChangePairs = patchDeclaration(
    AssignDirection.CurrentToBaseline,
    findInterfaceBreakingChanges,
    baseline!,
    current!,
    findMappingCallSignature
  );

  const newFeaturePairs = patchDeclaration(
    AssignDirection.BaselineToCurrent,
    findInterfaceBreakingChanges,
    current!,
    baseline!,
    findMappingCallSignature
  )
    .filter((p) => p.reasons === DiffReasons.Removed)
    .map((p) => {
      p.reasons = DiffReasons.Added;
      p.assignDirection = AssignDirection.CurrentToBaseline;
      const temp = p.source;
      p.source = p.target;
      p.target = temp;
      return p;
    });
  return [...breakingChangePairs, ...newFeaturePairs];
}

export function patchUnionType(name: string, astContext: AstContext, modelType: AssignDirection): DiffPair[] {
  const baseline = astContext.baseline.getTypeAlias(name);
  const current = astContext.current.getTypeAlias(name);

  if (!baseline && !current) throw new Error(`Failed to find type '${name}' in baseline and current package`);

  const addPair = checkAddedDeclaration(DiffLocation.TypeAlias, baseline, current);
  if (addPair) return [addPair];

  const removePair = checkRemovedDeclaration(DiffLocation.TypeAlias, baseline, current);
  if (removePair) return [removePair];
  return patchDeclaration(modelType, findTypeAliasBreakingChanges, baseline!, current!);
}

export function patchFunction(name: string, astContext: AstContext): DiffPair[] {
  const getFunctions = (source: SourceFile) =>
    source
      .getStatements()
      .filter((s) => s.isKind(SyntaxKind.FunctionDeclaration) && s.getName() === name)
      .map((s) => s.asKindOrThrow(SyntaxKind.FunctionDeclaration));

  const baselineFunctions = getFunctions(astContext.baseline);
  const currentFunctions = getFunctions(astContext.current);

  if (baselineFunctions.length === 0 && currentFunctions.length === 0) {
    throw new Error(`Failed to find function '${name}' in baseline and current package`);
  }

  if (baselineFunctions.length > 1 || currentFunctions.length > 1) {
    logger.warn(`Found overloads for function '${name}'`);
  }

  const addPair = checkAddedDeclaration(
    DiffLocation.Function,
    baselineFunctions.length > 0 ? baselineFunctions[0] : undefined,
    currentFunctions.length > 0 ? currentFunctions[0] : undefined
  );
  if (addPair) return [addPair];

  const removePair = checkRemovedDeclaration(
    DiffLocation.Function,
    baselineFunctions.length > 0 ? baselineFunctions[0] : undefined,
    currentFunctions.length > 0 ? currentFunctions[0] : undefined
  );
  if (removePair) return [removePair];

  const getNameNode = (s: FunctionDeclaration) => ({ name, node: s as Node });
  if (currentFunctions.length === 0) {
    return [createDiffPair(DiffLocation.Function, DiffReasons.Removed, undefined, getNameNode(baselineFunctions[0]))];
  }

  const pairs = patchDeclaration(
    AssignDirection.CurrentToBaseline,
    findFunctionBreakingChanges,
    baselineFunctions[0],
    currentFunctions[0]
  );
  return pairs;
}

export function patchDeclaration<T extends Node>(
  modelType: AssignDirection,
  findBreakingChanges: (source: T, target: T, ...extra: any) => DiffPair[],
  baseline: T,
  current: T,
  ...extra: any
): DiffPair[] {
  const updateModelType = (pair: DiffPair) => {
    pair.assignDirection = modelType;
    return pair;
  };
  switch (modelType) {
    case AssignDirection.BaselineToCurrent: {
      return findBreakingChanges(baseline, current, ...extra).map(updateModelType);
    }
    case AssignDirection.CurrentToBaseline: {
      return findBreakingChanges(current, baseline, ...extra).map(updateModelType);
    }
    default:
      throw new Error(`Unsupported model type: ${modelType}`);
  }
}

import { FunctionDeclaration, Node, Signature, SourceFile, SyntaxKind } from 'ts-morph';
import { AstContext, BreakingLocation, BreakingPair, BreakingReasons, ModelType } from '../common/types';
import {
  findFunctionBreakingChanges,
  findInterfaceBreakingChanges,
  findTypeAliasBreakingChanges,
  makeBreakingPair,
} from '../diff/ts-diff';
import { logger } from '../../logging/logger';

// TODO: handle add
function handleRemove(
  location: BreakingLocation,
  baseline?: Node,
  current?: Node,
  modelType: ModelType = ModelType.Output
): BreakingPair | undefined {
  if (baseline && current) return undefined;

  const getNameNode = (node?: Node) => {
    if (!node) return undefined;
    const name = Node.hasName(node) ? node.getName() : node.getText();
    return { name, node };
  };
  const sourceNameNode = modelType === ModelType.Input ? getNameNode(baseline) : getNameNode(current);
  const targetNameNode = modelType === ModelType.Input ? getNameNode(current) : getNameNode(baseline);
  if (!current) return makeBreakingPair(location, BreakingReasons.Removed, sourceNameNode, targetNameNode, modelType);
}

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

export function patchRoutes(astContext: AstContext): BreakingPair[] {
  const baseline = astContext.baseline.getInterface('Routes');
  const current = astContext.current.getInterface('Routes');
  const removePair = handleRemove(BreakingLocation.Interface, baseline, current);
  if (removePair) return [removePair];
  return patchDeclaration(
    ModelType.Output,
    findInterfaceBreakingChanges,
    baseline!,
    current!,
    findMappingCallSignature
  );
}

export function patchUnionType(name: string, astContext: AstContext, modelType: ModelType): BreakingPair[] {
  const baseline = astContext.baseline.getTypeAliasOrThrow(name);
  const current = astContext.current.getTypeAliasOrThrow(name);
  const removePair = handleRemove(BreakingLocation.TypeAlias, baseline, current);
  if (removePair) return [removePair];
  return patchDeclaration(modelType, findTypeAliasBreakingChanges, baseline, current);
}

export function patchFunction(name: string, astContext: AstContext): BreakingPair[] {
  const getFunctions = (source: SourceFile) =>
    source
      .getStatements()
      .filter((s) => s.isKind(SyntaxKind.FunctionDeclaration) && s.getName() === name)
      .map((s) => s.asKindOrThrow(SyntaxKind.FunctionDeclaration));

  const baselineFunctions = getFunctions(astContext.baseline);
  const currentFunctions = getFunctions(astContext.current);

  if (baselineFunctions.length > 1 || currentFunctions.length > 1) {
    logger.warn(`Found overloads for function '${name}'`);
  }

  const removePair = handleRemove(
    BreakingLocation.Function,
    baselineFunctions.length > 0 ? baselineFunctions[0] : undefined,
    currentFunctions.length > 0 ? currentFunctions[0] : undefined
  );
  if (removePair) return [removePair];

  const getNameNode = (s: FunctionDeclaration) => ({ name, node: s as Node });
  if (currentFunctions.length === 0) {
    return [
      makeBreakingPair(
        BreakingLocation.Function,
        BreakingReasons.Removed,
        undefined,
        getNameNode(baselineFunctions[0])
      ),
    ];
  }

  const pairs = patchDeclaration(
    ModelType.Output,
    findFunctionBreakingChanges,
    baselineFunctions[0],
    currentFunctions[0]
  );
  return pairs;
}

export function patchDeclaration<T extends Node>(
  modelType: ModelType,
  findBreakingChanges: (source: T, target: T, ...extra: any) => BreakingPair[],
  baseline: T,
  current: T,
  ...extra: any
): BreakingPair[] {
  const updateModelType = (pair: BreakingPair) => {
    pair.modelType = modelType;
    return pair;
  };
  switch (modelType) {
    case ModelType.Input: {
      return findBreakingChanges(baseline, current, ...extra).map(updateModelType);
    }
    case ModelType.Output: {
      return findBreakingChanges(current, baseline, ...extra).map(updateModelType);
    }
    default:
      throw new Error(`Unsupported model type: ${modelType}`);
  }
}

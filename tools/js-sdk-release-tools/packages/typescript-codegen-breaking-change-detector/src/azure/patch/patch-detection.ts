import {
  CallSignatureDeclaration,
  FunctionDeclaration,
  InterfaceDeclaration,
  Node,
  ParameterDeclaration,
  Signature,
  SourceFile,
  SyntaxKind,
} from 'ts-morph';
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
    baseline!,
    current!,
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

interface OperationParametersContext {
  name: string;
  parameters: ParameterDeclaration[];
  operation: Node;
}

interface OperationGroupParametersContext {
  //   name: string;
  context: Map<string, OperationParametersContext>;
}

interface OperationGroupParameterContextPairs {
  baseline: Map<string, OperationGroupParametersContext>;
  current: Map<string, OperationGroupParametersContext>;
}

interface FindOperationGroupContextPairs {
  (astContext: AstContext): OperationGroupParameterContextPairs;
}

function findParameterNamesChanges(operationGroupContextsPairs: OperationGroupParameterContextPairs): DiffPair[] {
  const findParameterNamesChangesCore = (
    baselineOpCtx: OperationParametersContext,
    currentOpCtx: OperationParametersContext
  ) => {
    currentOpCtx.parameters.forEach((currentParameter, i) => {
      const baselineParameter = baselineOpCtx.parameters[i];
      if (baselineParameter.getName() !== currentParameter.getName()) {
        const source = { name: baselineOpCtx.parameters[i].getName(), node: baselineOpCtx.parameters[i] };
        const target = { name: currentOpCtx.parameters[i].getName(), node: currentOpCtx.parameters[i] };
        const direction = AssignDirection.BaselineToCurrent;
        const location = DiffLocation.Parameter;
        const pair = createDiffPair(location, DiffReasons.NameChanged, source, target, direction);
        pairs.push(pair);
      }
    });
  };

  const pairs = new Array<DiffPair>();
  operationGroupContextsPairs.baseline.forEach((baselineOpGroupCtx, name) => {
    const currentGroupOpCtx = operationGroupContextsPairs.current.get(name);

    baselineOpGroupCtx.context.forEach((baselineOpCtx) => {
      const currentOpCtx = currentGroupOpCtx?.context.get(baselineOpCtx.name);

      // NOTE: already handle it
      if (!currentOpCtx || currentOpCtx.parameters.length !== baselineOpCtx.parameters.length) return;

      findParameterNamesChangesCore(baselineOpCtx, currentOpCtx);
    });
  });
  return pairs;
}

export const findOperationContextPairsCore = (
  astContext: AstContext,
  operationGroupPredicate: (int: InterfaceDeclaration) => boolean,
  extractOperationParametersContextFromMember: (member: Node) => OperationParametersContext
): OperationGroupParameterContextPairs => {
  const getOperationGroupParaCtxMap = (source: SourceFile) => {
    const operationGroups = source.getInterfaces().filter(operationGroupPredicate);

    // extract op
    const operationGroupParaCtxMap = operationGroups.reduce((operationGroupParaCtxMap, operationGroup) => {
      const operationParaContext = operationGroup.getMembers().reduce((map, member) => {
        const context = extractOperationParametersContextFromMember(member);
        map.set(context.name, context);
        return map;
      }, new Map<string, OperationParametersContext>());

      const operationGroupParaCtx: OperationGroupParametersContext = {
        context: operationParaContext,
      };
      operationGroupParaCtxMap.set(operationGroup.getName(), operationGroupParaCtx);
      return operationGroupParaCtxMap;
    }, new Map<string, OperationGroupParametersContext>());
    return operationGroupParaCtxMap;
  };

  return {
    baseline: getOperationGroupParaCtxMap(astContext.baseline),
    current: getOperationGroupParaCtxMap(astContext.current),
  };
};

// TODO: find operation without group
export const findOperationContextPairsInHighLevelClient: FindOperationGroupContextPairs = (astContext: AstContext) => {
  const operationGroupPredicate = (i: InterfaceDeclaration) =>
    i.getMembers().every((m) => m.getKind() === SyntaxKind.MethodSignature);

  const extractOpParaCtxFromMember = (member: Node) => {
    // high level client's operation group contains only methods
    const operation = member.asKindOrThrow(SyntaxKind.MethodSignature);
    const name = operation.getName();
    const parameters = operation.getParameters();
    const context = { name, parameters, operation };
    return context;
  };

  return findOperationContextPairsCore(astContext, operationGroupPredicate, extractOpParaCtxFromMember);
};

export const findOperationContextPairsInModularClient: FindOperationGroupContextPairs = (astContext: AstContext) => {
  const operationGroupPredicate = (i: InterfaceDeclaration) => i.getName().endsWith('Operations');
  const extractOpParaCtxFromMember = (member: Node) => {
    // modular client's operation group contains only arrow functions
    const operation = member.asKindOrThrow(SyntaxKind.PropertySignature);
    const functionType = operation.getTypeNodeOrThrow().asKindOrThrow(SyntaxKind.FunctionType);

    const name = operation.getName();
    const parameters = functionType.getParameters();
    const context = { name, parameters, operation };
    return context;
  };

  return findOperationContextPairsCore(astContext, operationGroupPredicate, extractOpParaCtxFromMember);
};

export const findOperationContextPairsInRestLevelClient: FindOperationGroupContextPairs = (astContext: AstContext) => {
  const operationGroupPredicate = (i: InterfaceDeclaration) => i.getName() === 'Routes';
  const extractOpParaCtxFromMember = (member: Node) => {
    const operation = member.asKindOrThrow(SyntaxKind.CallSignature).asKindOrThrow(SyntaxKind.CallSignature);
    const name = operation.getParameterOrThrow('path').getTypeNodeOrThrow().getText();
    const parameters = operation.getParameters();
    const context = { name, parameters, operation };
    return context;
  };
  return findOperationContextPairsCore(astContext, operationGroupPredicate, extractOpParaCtxFromMember);
};

export function patchOperationParameterName(
  astContext: AstContext,
  findOperationContextPairs: FindOperationGroupContextPairs
): DiffPair[] {
  const operationContextsPairs = findOperationContextPairs(astContext);
  const pairs = findParameterNamesChanges(operationContextsPairs);
  return pairs;
}

export function patchTypeAlias(name: string, astContext: AstContext, assignDirection: AssignDirection): DiffPair[] {
  const baseline = astContext.baseline.getTypeAlias(name);
  const current = astContext.current.getTypeAlias(name);

  if (!baseline && !current) throw new Error(`Failed to find type '${name}' in baseline and current package`);

  const addPair = checkAddedDeclaration(DiffLocation.TypeAlias, baseline, current);
  if (addPair) return [addPair];

  const removePair = checkRemovedDeclaration(DiffLocation.TypeAlias, baseline, current);
  if (removePair) return [removePair];
  return patchDeclaration(assignDirection, findTypeAliasBreakingChanges, baseline!, current!);
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
    DiffLocation.Signature,
    baselineFunctions.length > 0 ? baselineFunctions[0] : undefined,
    currentFunctions.length > 0 ? currentFunctions[0] : undefined
  );
  if (addPair) return [addPair];

  const removePair = checkRemovedDeclaration(
    DiffLocation.Signature,
    baselineFunctions.length > 0 ? baselineFunctions[0] : undefined,
    currentFunctions.length > 0 ? currentFunctions[0] : undefined
  );
  if (removePair) return [removePair];

  const getNameNode = (s: FunctionDeclaration) => ({ name, node: s as Node });
  if (currentFunctions.length === 0) {
    return [createDiffPair(DiffLocation.Signature, DiffReasons.Removed, undefined, getNameNode(baselineFunctions[0]))];
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
  assignDirection: AssignDirection,
  findBreakingChanges: (source: T, target: T, ...extra: any) => DiffPair[],
  baseline: T,
  current: T,
  ...extra: any
): DiffPair[] {
  const updateAssignDirection = (pair: DiffPair) => {
    pair.assignDirection = assignDirection;
    return pair;
  };
  switch (assignDirection) {
    case AssignDirection.BaselineToCurrent: {
      return findBreakingChanges(baseline, current, ...extra).map(updateAssignDirection);
    }
    case AssignDirection.CurrentToBaseline: {
      return findBreakingChanges(current, baseline, ...extra).map(updateAssignDirection);
    }
    default:
      throw new Error(`Unsupported model type: ${assignDirection}`);
  }
}

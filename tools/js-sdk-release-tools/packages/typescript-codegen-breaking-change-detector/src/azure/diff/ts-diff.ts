// TODO: support class
import {
  InterfaceDeclaration,
  Node,
  ParameterDeclaration,
  Signature,
  SyntaxKind,
  Symbol,
  SymbolFlags,
  FunctionDeclaration,
  TypePredicateNode,
  PropertySignature,
  TypeNode,
  PropertyDeclaration,
  MethodDeclaration,
  TypeAliasDeclaration,
  CallSignatureDeclaration,
} from 'ts-morph';
import {
  BreakingLocation,
  BreakingPair,
  BreakingReasons,
  FindMappingCallSignature,
  ModelType,
  NameNode,
} from '../common/types';
import {
  getCallableEntityParametersFromSymbol,
  isMethodOrArrowFunction,
  isPropertyArrowFunction,
  isPropertyMethod,
  isSameSignature,
} from '../../utils/ast-utils';
import { logger } from '../../logging/logger';

// TODO: limit node
function findBreakingReasons(target: Node, source: Node): BreakingReasons {
  // Note: if return type node defined,
  // it's a funtion/method/signature's return type node,
  // return it, it will be used to compare later
  // Otherwise, it's a non-funtion/method/signature node, return its type node
  const getTypeNode = (node: Node): TypeNode => {
    if (Node.isReturnTyped(node)) return node.getReturnTypeNodeOrThrow();
    if (Node.isTyped(node)) return node.getTypeNodeOrThrow();
    throw new Error(`Unsupported ${node.getKindName()} node: "${node.getText()}"`);
  };
  let breakingReasons = BreakingReasons.None;

  const targetTypeNode = getTypeNode(target);
  const sourceTypeNode = getTypeNode(source);

  // check if concrete type -> any. e.g. string -> any
  const isConcretTypeToAny = canConvertConcretTypeToAny(targetTypeNode?.getKind(), sourceTypeNode?.getKind());
  if (isConcretTypeToAny) breakingReasons |= BreakingReasons.TypeChanged;

  // check type predicates
  if (
    targetTypeNode &&
    sourceTypeNode &&
    targetTypeNode.isKind(SyntaxKind.TypePredicate) &&
    sourceTypeNode.isKind(SyntaxKind.TypePredicate)
  ) {
    const getTypeName = (node: TypeNode) => node.asKindOrThrow(SyntaxKind.TypePredicate).getTypeNodeOrThrow().getText();
    if (getTypeName(targetTypeNode) !== getTypeName(sourceTypeNode)) breakingReasons |= BreakingReasons.TypeChanged;
  }

  // check type
  const assignable = sourceTypeNode.getType().isAssignableTo(targetTypeNode.getType());
  if (!assignable) breakingReasons |= BreakingReasons.TypeChanged;

  // check required -> optional
  const isOptional = (node: Node) => node.getSymbolOrThrow().isOptional();
  const incompatibleOptional = isOptional(target) && !isOptional(source);
  if (incompatibleOptional) breakingReasons |= BreakingReasons.RequiredToOptional;

  // check readonly -> mutable
  const isReadonly = (node: Node) => Node.isReadonlyable(node) && node.isReadonly();
  const incompatibleReadonly = isReadonly(target) && !isReadonly(source);
  if (incompatibleReadonly) breakingReasons |= BreakingReasons.ReadonlyToMutable;

  return breakingReasons;
}

function findCallSignatureBreakingChanges(
  targetSignatures: Signature[],
  sourceSignatures: Signature[],
  findMappingCallSignature?: FindMappingCallSignature
): BreakingPair[] {
  const pairs = targetSignatures.reduce((result, targetSignature) => {
    const defaultFindMappingCallSignature: FindMappingCallSignature = (target: Signature, signatures: Signature[]) => {
      const signature = signatures.find((s) => isSameSignature(target, s));
      if (!signature) return undefined;
      const id = signature.getDeclaration().asKindOrThrow(SyntaxKind.CallSignature).getText();
      return { id, signature };
    };
    const resolvedFindMappingCallSignature = findMappingCallSignature || defaultFindMappingCallSignature;
    const sourceContext = resolvedFindMappingCallSignature(targetSignature, sourceSignatures);
    if (sourceContext) {
      const sourceSignature = sourceContext.signature;
      // handle return type
      const getDeclaration = (s: Signature): CallSignatureDeclaration =>
        s.getDeclaration().asKindOrThrow(SyntaxKind.CallSignature);
      const targetDeclaration = getDeclaration(targetSignature)!;
      const sourceDeclaration = getDeclaration(sourceSignature)!;
      const returnPairs = findReturnTypeBreakingChangesCore(targetDeclaration, sourceDeclaration);
      if (returnPairs.length > 0) result.push(...returnPairs);

      // handle parameters
      const getParameters = (s: Signature): ParameterDeclaration[] =>
        s.getDeclaration().asKindOrThrow(SyntaxKind.CallSignature).getParameters();
      const parameterPairs = findParameterBreakingChangesCore(
        getParameters(targetSignature),
        getParameters(sourceSignature),
        sourceContext.id,
        sourceContext.id,
        targetSignature.getDeclaration(),
        sourceSignature.getDeclaration()
      );
      if (parameterPairs.length > 0) result.push(...parameterPairs);

      return result;
    }

    const getNode = (s: Signature): Node => s.compilerSignature.getDeclaration() as unknown as Node;
    const getName = (s: Signature): string => s.compilerSignature.getDeclaration().getText();
    const getNameNode = (s: Signature): NameNode => ({ name: getName(s), node: getNode(s) });
    const targetNameNode = getNameNode(targetSignature);
    const pair = makeBreakingPair(BreakingLocation.Call, BreakingReasons.Removed, undefined, targetNameNode);
    result.push(pair);
    return result;
  }, new Array<BreakingPair>());
  return pairs;
}

function getNameNode(s: Symbol): NameNode {
  return { name: s.getName(), node: s.getValueDeclarationOrThrow() };
}

function isClassicProperty(p: Symbol) {
  return (p.getFlags() & SymbolFlags.Property) !== 0 && !isMethodOrArrowFunction(p);
}

function canConvertConcretTypeToAny(targetKind: SyntaxKind | undefined, sourceKind: SyntaxKind | undefined) {
  return targetKind !== SyntaxKind.AnyKeyword && sourceKind === SyntaxKind.AnyKeyword;
}

function findClassicPropertyBreakingChanges(
  targetProperty: Symbol,
  sourceProperty: Symbol
): BreakingPair | undefined {
  const reasons = findBreakingReasons(
    targetProperty.getValueDeclarationOrThrow(),
    sourceProperty.getValueDeclarationOrThrow()
  );

  if (reasons === BreakingReasons.None) return undefined;
  return makeBreakingPair(
    BreakingLocation.ClassicProperty,
    reasons,
    getNameNode(targetProperty),
    getNameNode(sourceProperty)
  );
}

// NOTE: this function compares methods and arrow functions in interface
function findPropertyBreakingChanges(targetProperties: Symbol[], sourceProperties: Symbol[]): BreakingPair[] {
  const sourcePropMap = sourceProperties.reduce((map, p) => {
    map.set(p.getName(), p);
    return map;
  }, new Map<string, Symbol>());

  const removed = targetProperties.reduce((result, targetProperty) => {
    const name = targetProperty.getName();
    if (sourcePropMap.has(name)) {
      return result;
    }

    const isPropertyFunction = isMethodOrArrowFunction(targetProperty);
    const location = isPropertyFunction ? BreakingLocation.Function : BreakingLocation.ClassicProperty;
    const pair = makeBreakingPair(location, BreakingReasons.Removed, undefined, getNameNode(targetProperty));
    result.push(pair);
    return result;
  }, new Array<BreakingPair>());

  const changed = targetProperties.reduce((result, targetProperty) => {
    const name = targetProperty.getName();
    const sourceProperty = sourcePropMap.get(name);
    if (!sourceProperty) return result;

    const isTargetPropertyClassic = isClassicProperty(targetProperty);
    const isSourcePropertyClassic = isClassicProperty(sourceProperty);

    // handle different property kinds
    if (isTargetPropertyClassic !== isSourcePropertyClassic) {
      return [
        ...result,
        makeBreakingPair(
          BreakingLocation.Function,
          BreakingReasons.TypeChanged,
          getNameNode(sourceProperty),
          getNameNode(targetProperty)
        ),
      ];
    }

    // handle classic property
    if (isTargetPropertyClassic && isSourcePropertyClassic) {
      const classicBreakingPair = findClassicPropertyBreakingChanges(targetProperty, sourceProperty);
      if (!classicBreakingPair) return result;
      return [...result, classicBreakingPair];
    }

    // handle method and arrow function
    if (
      (isPropertyMethod(targetProperty) || isPropertyArrowFunction(targetProperty)) &&
      (isPropertyMethod(sourceProperty) || isPropertyArrowFunction(sourceProperty))
    ) {
      const functionPropertyDetails = findFunctionPropertyBreakingChangeDetails(targetProperty, sourceProperty);
      return [...result, ...functionPropertyDetails];
    }

    throw new Error('Should never reach here');
  }, new Array<BreakingPair>());
  return [...removed, ...changed];
}

function findReturnTypeBreakingChangesCore(target: Node, source: Node): BreakingPair[] {
  const reasons = findBreakingReasons(target, source);
  if (reasons === BreakingReasons.None) return [];
  const targetNameNode = target ? { name: target.getText(), node: target } : undefined;
  const sourceNameNode = source ? { name: source.getText(), node: source } : undefined;
  const pair = makeBreakingPair(BreakingLocation.FunctionReturnType, reasons, sourceNameNode, targetNameNode);
  return [pair];
}

function findReturnTypeBreakingChanges(targetMethod: Symbol, sourceMethod: Symbol): BreakingPair[] {
  const targetDeclaration = targetMethod.getValueDeclarationOrThrow();
  const sourceDeclaration = sourceMethod.getValueDeclarationOrThrow();
  return findReturnTypeBreakingChangesCore(targetDeclaration, sourceDeclaration);
}

function findParameterBreakingChangesCore(
  targetParameters: ParameterDeclaration[],
  sourceParameters: ParameterDeclaration[],
  targetName: string,
  sourceName: string,
  targetNode: Node | TypeNode,
  sourceNode: Node | TypeNode
): BreakingPair[] {
  const pairs: BreakingPair[] = [];

  // handle parameter counts
  const isSameParameterCount = targetParameters.length === sourceParameters.length;
  if (!isSameParameterCount) {
    const source = {
      name: sourceName,
      node: sourceNode,
    };
    const target = {
      name: targetName,
      node: targetNode,
    };
    const pair = makeBreakingPair(BreakingLocation.FunctionParameterList, BreakingReasons.CountChanged, source, target);
    pairs.push(pair);
    return pairs;
  }

  // NOTE: parameter count is the same
  // handle each parameter
  targetParameters.forEach((targetParameter, i) => {
    const sourceParameter = sourceParameters[i];
    const getParameterNameNode = (p: ParameterDeclaration | undefined) =>
      p ? { name: p.getName() || '', node: p } : undefined;
    const target = getParameterNameNode(targetParameter);
    const source = getParameterNameNode(sourceParameter);
    const reasons = findBreakingReasons(targetParameter, sourceParameter);
    const pair = makeBreakingPair(BreakingLocation.FunctionParameter, reasons, source, target);
    pair.reasons = findBreakingReasons(targetParameter, sourceParameter);
    if (pair.reasons !== BreakingReasons.None) pairs.push(pair);
  });

  return pairs;
}

// TODO: not support for overloads
function findParameterBreakingChanges(targetMethod: Symbol, sourceMethod: Symbol): BreakingPair[] {
  const targetParameters = getCallableEntityParametersFromSymbol(targetMethod);
  const sourceParameters = getCallableEntityParametersFromSymbol(sourceMethod);
  return findParameterBreakingChangesCore(
    targetParameters,
    sourceParameters,
    targetMethod.getName(),
    sourceMethod.getName(),
    targetMethod.getValueDeclarationOrThrow(),
    sourceMethod.getValueDeclarationOrThrow()
  );
}

function findFunctionPropertyBreakingChangeDetails(targetMethod: Symbol, sourceMethod: Symbol): BreakingPair[] {
  const returnTypePairs = findReturnTypeBreakingChanges(targetMethod, sourceMethod);
  const parameterPairs = findParameterBreakingChanges(targetMethod, sourceMethod);
  return [...returnTypePairs, ...parameterPairs];
}

// TODO: support readonly properties
// TODO: add generic test case: parameter with generic, return type with generic
export function findInterfaceBreakingChanges(
  source: InterfaceDeclaration,
  target: InterfaceDeclaration,
  findMappingCallSignature?: FindMappingCallSignature
): BreakingPair[] {
  const targetSignatures = target.getType().getCallSignatures();
  const sourceSignatures = source.getType().getCallSignatures();
  const callSignatureBreakingChanges = findCallSignatureBreakingChanges(
    targetSignatures,
    sourceSignatures,
    findMappingCallSignature
  );
  const targetProperties = target.getType().getProperties();
  const sourceProperties = source.getType().getProperties();

  const propertyBreakingChanges = findPropertyBreakingChanges(targetProperties, sourceProperties);

  return [...callSignatureBreakingChanges, ...propertyBreakingChanges];
}

// TODO: support arrow function
export function findFunctionBreakingChanges(source: FunctionDeclaration, target: FunctionDeclaration): BreakingPair[] {
  const sourceOverloads = source.getOverloads();
  const targetOverloads = target.getOverloads();

  if (sourceOverloads.length > 1 || targetOverloads.length > 1) {
    logger.warn('Function has overloads');
    const pairs = targetOverloads
      .filter((t) => {
        const compatibleSourceFunction = sourceOverloads.find((s) => {
          // NOTE: isTypeAssignableTo does not work for overloads
          const returnTypePairs = findReturnTypeBreakingChangesCore(t, s);
          if (returnTypePairs.length > 0) return false;
          const parameterPairs = findParameterBreakingChangesCore(t.getParameters(), s.getParameters(), '', '', t, s);
          return parameterPairs.length === 0;
        });
        return compatibleSourceFunction === undefined;
      })
      .map((t) =>
        makeBreakingPair(BreakingLocation.FunctionOverload, BreakingReasons.Removed, undefined, {
          name: t.getName()!,
          node: t,
        })
      );

    return pairs;
  }

  // function has no overloads
  const returnTypePairs = findReturnTypeBreakingChangesCore(target, source);

  const parameterPairs = findParameterBreakingChangesCore(
    target.getParameters(),
    source.getParameters(),
    target.getName()!,
    source.getName()!,
    target,
    source
  );

  return [...returnTypePairs, ...parameterPairs];
}

export function findTypeAliasBreakingChanges(
  source: TypeAliasDeclaration,
  target: TypeAliasDeclaration
): BreakingPair[] {
  if (source.getType().isAssignableTo(target.getType())) return [];

  let sourceNameNode = <NameNode>{
    name: source.getName(),
    node: source,
  };
  let targetNameNode = <NameNode>{
    name: target.getName(),
    node: target,
  };
  return [makeBreakingPair(BreakingLocation.TypeAlias, BreakingReasons.TypeChanged, sourceNameNode, targetNameNode)];
}

export function makeBreakingPair(
  location: BreakingLocation,
  reasons: BreakingReasons,
  source?: NameNode,
  target?: NameNode,
  modelType: ModelType = ModelType.None
): BreakingPair {
  const messages = new Map<BreakingReasons, string>();
  return { location, reasons, messages, target, source, modelType };
}

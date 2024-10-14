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
function findBreakingReasons(baselineNode: Node, currentNode: Node): BreakingReasons {
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

  const baselineTypeNode = getTypeNode(baselineNode);
  const currentTypeNode = getTypeNode(currentNode);

  // check if concrete type -> any. e.g. string -> any
  const isConcretTypeToAny = canConvertConcretTypeToAny(baselineTypeNode?.getKind(), currentTypeNode?.getKind());
  if (isConcretTypeToAny) breakingReasons |= BreakingReasons.TypeChanged;

  // check type predicates
  if (
    baselineTypeNode &&
    currentTypeNode &&
    baselineTypeNode.isKind(SyntaxKind.TypePredicate) &&
    currentTypeNode.isKind(SyntaxKind.TypePredicate)
  ) {
    const getTypeName = (node: TypeNode) => node.asKindOrThrow(SyntaxKind.TypePredicate).getTypeNodeOrThrow().getText();
    if (getTypeName(baselineTypeNode) !== getTypeName(currentTypeNode)) breakingReasons |= BreakingReasons.TypeChanged;
  }

  // check type
  const assignable = currentTypeNode.getType().isAssignableTo(baselineTypeNode.getType());
  if (!assignable) breakingReasons |= BreakingReasons.TypeChanged;

  // check required -> optional
  const isOptional = (node: Node) => node.getSymbolOrThrow().isOptional();
  const incompatibleOptional = isOptional(baselineNode) && !isOptional(currentNode);
  if (incompatibleOptional) breakingReasons |= BreakingReasons.RequiredToOptional;

  // check readonly -> mutable
  const isReadonly = (node: Node) => Node.isReadonlyable(node) && node.isReadonly();
  const incompatibleReadonly = isReadonly(baselineNode) && !isReadonly(currentNode);
  if (incompatibleReadonly) breakingReasons |= BreakingReasons.ReadonlyToMutable;

  return breakingReasons;
}

function findCallSignatureBreakingChanges(
  baselineSignatures: Signature[],
  currentSignatures: Signature[],
  findMappingCallSignature?: FindMappingCallSignature
): BreakingPair[] {
  const pairs = baselineSignatures.reduce((result, baselineSignature) => {
    const defaultFindMappingCallSignature: FindMappingCallSignature = (target: Signature, signatures: Signature[]) => {
      const signature = signatures.find((s) => isSameSignature(target, s));
      if (!signature) return undefined;
      const id = signature.getDeclaration().asKindOrThrow(SyntaxKind.CallSignature).getText();
      return { id, signature };
    };
    const resolvedFindMappingCallSignature = findMappingCallSignature || defaultFindMappingCallSignature;
    const currentContext = resolvedFindMappingCallSignature(baselineSignature, currentSignatures);
    if (currentContext) {
      const currentSignature = currentContext.signature;
      // handle return type
      const getDeclaration = (s: Signature): CallSignatureDeclaration =>
        s.getDeclaration().asKindOrThrow(SyntaxKind.CallSignature);
      const baselineDeclaration = getDeclaration(baselineSignature)!;
      const currentDeclaration = getDeclaration(currentSignature)!;
      const returnPairs = findReturnTypeBreakingChangesCore(baselineDeclaration, currentDeclaration);
      if (returnPairs.length > 0) result.push(...returnPairs);

      // handle parameters
      const getParameters = (s: Signature): ParameterDeclaration[] =>
        s.getDeclaration().asKindOrThrow(SyntaxKind.CallSignature).getParameters();
      const parameterPairs = findParameterBreakingChangesCore(
        getParameters(baselineSignature),
        getParameters(currentSignature),
        currentContext.id,
        currentContext.id,
        baselineSignature.getDeclaration(),
        currentSignature.getDeclaration()
      );
      if (parameterPairs.length > 0) result.push(...parameterPairs);

      return result;
    }

    const getNode = (s: Signature): Node => s.compilerSignature.getDeclaration() as unknown as Node;
    const getName = (s: Signature): string => s.compilerSignature.getDeclaration().getText();
    const getNameNode = (s: Signature): NameNode => ({ name: getName(s), node: getNode(s) });
    const baselineNameNode = getNameNode(baselineSignature);
    const pair = makeBreakingPair(BreakingLocation.Call, BreakingReasons.Removed, undefined, baselineNameNode);
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

function canConvertConcretTypeToAny(baselineKind: SyntaxKind | undefined, currentKind: SyntaxKind | undefined) {
  return baselineKind !== SyntaxKind.AnyKeyword && currentKind === SyntaxKind.AnyKeyword;
}

function findClassicPropertyBreakingChanges(
  baselineProperty: Symbol,
  currentProperty: Symbol
): BreakingPair | undefined {
  const reasons = findBreakingReasons(
    baselineProperty.getValueDeclarationOrThrow(),
    currentProperty.getValueDeclarationOrThrow()
  );

  if (reasons === BreakingReasons.None) return undefined;
  return makeBreakingPair(
    BreakingLocation.ClassicProperty,
    reasons,
    getNameNode(baselineProperty),
    getNameNode(currentProperty)
  );
}

// NOTE: this function compares methods and arrow functions in interface
function findPropertyBreakingChanges(baselineProperties: Symbol[], currentProperties: Symbol[]): BreakingPair[] {
  const currentPropMap = currentProperties.reduce((map, p) => {
    map.set(p.getName(), p);
    return map;
  }, new Map<string, Symbol>());

  const removed = baselineProperties.reduce((result, baselineProperty) => {
    const name = baselineProperty.getName();
    if (currentPropMap.has(name)) {
      return result;
    }

    const isPropertyFunction = isMethodOrArrowFunction(baselineProperty);
    const location = isPropertyFunction ? BreakingLocation.Function : BreakingLocation.ClassicProperty;
    const pair = makeBreakingPair(location, BreakingReasons.Removed, undefined, getNameNode(baselineProperty));
    result.push(pair);
    return result;
  }, new Array<BreakingPair>());

  const changed = baselineProperties.reduce((result, baselineProperty) => {
    const name = baselineProperty.getName();
    const currentProperty = currentPropMap.get(name);
    if (!currentProperty) return result;

    const isBaselinePropertyClassic = isClassicProperty(baselineProperty);
    const isCurrentPropertyClassic = isClassicProperty(currentProperty);

    // handle different property kinds
    if (isBaselinePropertyClassic !== isCurrentPropertyClassic) {
      return [
        ...result,
        makeBreakingPair(
          BreakingLocation.Function,
          BreakingReasons.TypeChanged,
          getNameNode(currentProperty),
          getNameNode(baselineProperty)
        ),
      ];
    }

    // handle classic property
    if (isBaselinePropertyClassic && isCurrentPropertyClassic) {
      const classicBreakingPair = findClassicPropertyBreakingChanges(baselineProperty, currentProperty);
      if (!classicBreakingPair) return result;
      return [...result, classicBreakingPair];
    }

    // handle method and arrow function
    if (
      (isPropertyMethod(baselineProperty) || isPropertyArrowFunction(baselineProperty)) &&
      (isPropertyMethod(currentProperty) || isPropertyArrowFunction(currentProperty))
    ) {
      const functionPropertyDetails = findFunctionPropertyBreakingChangeDetails(baselineProperty, currentProperty);
      return [...result, ...functionPropertyDetails];
    }

    throw new Error('Should never reach here');
  }, new Array<BreakingPair>());
  return [...removed, ...changed];
}

function findReturnTypeBreakingChangesCore(baseline: Node, current: Node): BreakingPair[] {
  const reasons = findBreakingReasons(baseline, current);
  if (reasons === BreakingReasons.None) return [];
  const baselineNameNode = baseline ? { name: baseline.getText(), node: baseline } : undefined;
  const currentNameNode = current ? { name: current.getText(), node: current } : undefined;
  const pair = makeBreakingPair(BreakingLocation.FunctionReturnType, reasons, currentNameNode, baselineNameNode);
  return [pair];
}

function findReturnTypeBreakingChanges(baselineMethod: Symbol, currentMethod: Symbol): BreakingPair[] {
  const baselineDeclaration = baselineMethod.getValueDeclarationOrThrow();
  const currentDeclaration = currentMethod.getValueDeclarationOrThrow();
  return findReturnTypeBreakingChangesCore(baselineDeclaration, currentDeclaration);
}

function findParameterBreakingChangesCore(
  baselineParameters: ParameterDeclaration[],
  currentParameters: ParameterDeclaration[],
  baselineName: string,
  currentName: string,
  baselineNode: Node | TypeNode,
  currentNode: Node | TypeNode
): BreakingPair[] {
  const pairs: BreakingPair[] = [];

  // handle parameter counts
  const isSameParameterCount = baselineParameters.length === currentParameters.length;
  if (!isSameParameterCount) {
    const source = {
      name: currentName,
      node: currentNode,
    };
    const target = {
      name: baselineName,
      node: baselineNode,
    };
    const pair = makeBreakingPair(BreakingLocation.FunctionParameterList, BreakingReasons.CountChanged, source, target);
    pairs.push(pair);
    return pairs;
  }

  // NOTE: parameter count is the same
  // handle each parameter
  baselineParameters.forEach((baselineParameter, i) => {
    const currentParameter = currentParameters[i];
    const getParameterNameNode = (p: ParameterDeclaration | undefined) =>
      p ? { name: p.getName() || '', node: p } : undefined;
    const target = getParameterNameNode(baselineParameter);
    const source = getParameterNameNode(currentParameter);
    const reasons = findBreakingReasons(baselineParameter, currentParameter);
    const pair = makeBreakingPair(BreakingLocation.FunctionParameter, reasons, source, target);
    pair.reasons = findBreakingReasons(baselineParameter, currentParameter);
    if (pair.reasons !== BreakingReasons.None) pairs.push(pair);
  });

  return pairs;
}

// TODO: not support for overloads
function findParameterBreakingChanges(baselineMethod: Symbol, currentMethod: Symbol): BreakingPair[] {
  const baselineParameters = getCallableEntityParametersFromSymbol(baselineMethod);
  const currentParameters = getCallableEntityParametersFromSymbol(currentMethod);
  return findParameterBreakingChangesCore(
    baselineParameters,
    currentParameters,
    baselineMethod.getName(),
    currentMethod.getName(),
    baselineMethod.getValueDeclarationOrThrow(),
    currentMethod.getValueDeclarationOrThrow()
  );
}

function findFunctionPropertyBreakingChangeDetails(baselineMethod: Symbol, currentMethod: Symbol): BreakingPair[] {
  const returnTypePairs = findReturnTypeBreakingChanges(baselineMethod, currentMethod);
  const parameterPairs = findParameterBreakingChanges(baselineMethod, currentMethod);
  return [...returnTypePairs, ...parameterPairs];
}

// TODO: support readonly properties
// TODO: add generic test case: parameter with generic, return type with generic
export function findInterfaceBreakingChanges(
  source: InterfaceDeclaration,
  target: InterfaceDeclaration,
  findMappingCallSignature?: FindMappingCallSignature
): BreakingPair[] {
  const baselineSignatures = target.getType().getCallSignatures();
  const currentSignatures = source.getType().getCallSignatures();
  const callSignatureBreakingChanges = findCallSignatureBreakingChanges(
    baselineSignatures,
    currentSignatures,
    findMappingCallSignature
  );
  const baselineProperties = target.getType().getProperties();
  const currentProperties = source.getType().getProperties();

  const propertyBreakingChanges = findPropertyBreakingChanges(baselineProperties, currentProperties);

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

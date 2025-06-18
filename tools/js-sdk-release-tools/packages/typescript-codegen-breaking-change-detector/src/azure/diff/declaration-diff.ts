// TODO: add test for interfaces
import {
  InterfaceDeclaration,
  Node,
  ParameterDeclaration,
  Signature,
  SyntaxKind,
  Symbol,
  SymbolFlags,
  FunctionDeclaration,
  TypeNode,
  TypeAliasDeclaration,
  CallSignatureDeclaration,
  ClassDeclaration,
} from 'ts-morph';
import {
  DiffLocation,
  DiffPair,
  DiffReasons,
  FindMappingCallSignatureLikeDeclaration,
  AssignDirection,
  NameNode,
  CallSignatureLikeDeclaration,
} from '../common/types';
import {
  getCallableEntityParametersFromSymbol,
  isMethodOrArrowFunction,
  isPropertyArrowFunction,
  isPropertyMethod,
  isSameCallSignatureLikeDeclaration,
} from '../../utils/ast-utils';

function findBreakingReasons(source: Node, target: Node): DiffReasons {
  // Note: if return type node defined,
  // it's a funtion/method/signature's return type node,
  // return it, it will be used to compare later
  // Otherwise, it's a non-funtion/method/signature node, return its type node
  const getTypeNode = (node: Node): TypeNode => {
    const symbol = node.getSymbol();
    const isTyped = Node.isTyped(node);
    if (symbol && isPropertyArrowFunction(symbol)) {
      if (isTyped) return node.getTypeNodeOrThrow().asKindOrThrow(SyntaxKind.FunctionType).getReturnTypeNodeOrThrow();
      else throw new Error(`Should not reach here: "${node.getText()}"`);
    }
    // Note: if the node is a constructor, the return type is the instance type
    if (Node.isReturnTyped(node)) return node.getReturnTypeNodeOrThrow();
    if (isTyped) return node.getTypeNodeOrThrow();
    throw new Error(`Unsupported ${node.getKindName()} node: "${node.getText()}"`);
  };
  let breakingReasons = DiffReasons.None;

  const targetTypeNode = getTypeNode(target);
  const sourceTypeNode = getTypeNode(source);

  // check if concrete type -> any. e.g. string -> any
  const isConcretTypeToAny = canConvertConcretTypeToAny(targetTypeNode?.getKind(), sourceTypeNode?.getKind());
  if (isConcretTypeToAny) breakingReasons |= DiffReasons.TypeChanged;

  // check type predicates
  if (
    targetTypeNode &&
    sourceTypeNode &&
    targetTypeNode.isKind(SyntaxKind.TypePredicate) &&
    sourceTypeNode.isKind(SyntaxKind.TypePredicate)
  ) {
    const getTypeName = (node: TypeNode) => node.asKindOrThrow(SyntaxKind.TypePredicate).getTypeNodeOrThrow().getText();
    if (getTypeName(targetTypeNode) !== getTypeName(sourceTypeNode)) breakingReasons |= DiffReasons.TypeChanged;
  }

  // check type
  const assignable = sourceTypeNode.getType().isAssignableTo(targetTypeNode.getType());
  if (!assignable) breakingReasons |= DiffReasons.TypeChanged;

  // check required -> optional
  const isOptional = (node: Node) => node.getSymbolOrThrow().isOptional();
  const incompatibleOptional = isOptional(target) && !isOptional(source);
  if (incompatibleOptional) breakingReasons |= DiffReasons.RequiredToOptional;

  // check readonly -> mutable
  const isReadonly = (node: Node) => Node.isReadonlyable(node) && node.isReadonly();
  const incompatibleReadonly = isReadonly(target) && !isReadonly(source);
  if (incompatibleReadonly) breakingReasons |= DiffReasons.ReadonlyToMutable;

  return breakingReasons;
}

// TODO: fix target signature map to the same signatures
const defaultFindMappingCallSignatureLikeDeclaration: FindMappingCallSignatureLikeDeclaration<
  CallSignatureLikeDeclaration
> = (target: CallSignatureLikeDeclaration, declarations: CallSignatureLikeDeclaration[]) => {
  const declaration = declarations.find((s) => isSameCallSignatureLikeDeclaration(target, s));
  if (!declaration) return undefined;
  const id = declaration.getText();
  return { id, declaration };
};

function findCallSignatureLikeDeclarationBreakingChanges<T extends CallSignatureLikeDeclaration>(
  sourceDeclarations: T[],
  targetDeclarations: T[],
  findMappingConstructorLikeDeclaration: FindMappingCallSignatureLikeDeclaration<T>
): DiffPair[] {
  const pairs = targetDeclarations.reduce((result, targetDeclaration) => {
    const sourceContext = findMappingConstructorLikeDeclaration(targetDeclaration, sourceDeclarations);
    if (sourceContext) {
      const sourceDeclaration = sourceContext.declaration;
      // handle return type
      const returnPairs = findReturnTypeBreakingChangesCore(sourceDeclaration, targetDeclaration);
      if (returnPairs.length > 0) result.push(...returnPairs);

      // handle parameters
      const path = sourceContext.id;
      const parameterPairs = findParameterBreakingChangesCore(
        sourceDeclaration.getParameters(),
        targetDeclaration.getParameters(),
        path,
        path,
        sourceDeclaration,
        targetDeclaration
      );
      if (parameterPairs.length > 0) result.push(...parameterPairs);

      return result;
    }

    // not found
    const getNameNode = (n: T): NameNode => ({ name: n.getText(), node: n });
    const targetNameNode = getNameNode(targetDeclaration);
    const pair = createDiffPair(DiffLocation.Signature, DiffReasons.Removed, undefined, targetNameNode);
    result.push(pair);
    return result;
  }, new Array<DiffPair>());
  return pairs;
}

function getNameNodeFromSymbol(s: Symbol): NameNode {
  return { name: s.getName(), node: s.getValueDeclarationOrThrow() };
}

function getNameNodeFromNode(node?: Node): NameNode | undefined {
  if (!node) return undefined;
  const name = Node.hasName(node) ? node.getName() : node.getText();
  return { name, node };
}

function isClassicProperty(p: Symbol) {
  return (p.getFlags() & SymbolFlags.Property) !== 0 && !isMethodOrArrowFunction(p);
}

function canConvertConcretTypeToAny(targetKind: SyntaxKind | undefined, sourceKind: SyntaxKind | undefined) {
  return targetKind !== SyntaxKind.AnyKeyword && sourceKind === SyntaxKind.AnyKeyword;
}

function findClassicPropertyBreakingChanges(sourceProperty: Symbol, targetProperty: Symbol): DiffPair | undefined {
  console.log(
    '🚀 ~ findClassicPropertyBreakingChanges ~ sourceProperty.getValueDeclarationOrThrow():',
    sourceProperty.getValueDeclarationOrThrow().getText()
  );
  console.log(
    '🚀 ~ findClassicPropertyBreakingChanges ~ targetProperty.getValueDeclarationOrThrow():',
    targetProperty.getValueDeclarationOrThrow().getText()
  );

  const reasons = findBreakingReasons(
    sourceProperty.getValueDeclarationOrThrow(),
    targetProperty.getValueDeclarationOrThrow()
  );
  console.log('🚀 ~ findClassicPropertyBreakingChanges ~ reasons:', reasons);

  if (reasons === DiffReasons.None) return undefined;
  return createDiffPair(
    DiffLocation.Property,
    reasons,
    getNameNodeFromSymbol(sourceProperty),
    getNameNodeFromSymbol(targetProperty)
  );
}

function findPropertyBreakingChanges(sourceProperties: Symbol[], targetProperties: Symbol[]): DiffPair[] {
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
    const location = isPropertyFunction ? DiffLocation.Signature : DiffLocation.Property;
    const pair = createDiffPair(location, DiffReasons.Removed, undefined, getNameNodeFromSymbol(targetProperty));
    result.push(pair);
    return result;
  }, new Array<DiffPair>());

  const changed = targetProperties.reduce((result, targetProperty) => {
    const name = targetProperty.getName();
    const sourceProperty = sourcePropMap.get(name);
    if (!sourceProperty) return result;

    const isTargetPropertyClassic = isClassicProperty(targetProperty);
    console.log('🚀 ~ changed ~ isTargetPropertyClassic:', isTargetPropertyClassic, targetProperty.getFlags());
    const isSourcePropertyClassic = isClassicProperty(sourceProperty);

    // handle different property kinds
    if (isTargetPropertyClassic !== isSourcePropertyClassic) {
      return [
        ...result,
        createDiffPair(
          DiffLocation.Signature,
          DiffReasons.TypeChanged,
          getNameNodeFromSymbol(sourceProperty),
          getNameNodeFromSymbol(targetProperty)
        ),
      ];
    }
    console.log('🚀 ~ changed ~ sourceProperty `:', sourceProperty.getValueDeclarationOrThrow().getText());
    console.log('🚀 ~ changed ~ targetProperty `:', targetProperty.getValueDeclarationOrThrow().getText());

    // handle classic property
    if (isTargetPropertyClassic && isSourcePropertyClassic) {
      const classicBreakingPair = findClassicPropertyBreakingChanges(sourceProperty, targetProperty);
      if (!classicBreakingPair) return result;
      return [...result, classicBreakingPair];
    }

    console.log(
      '🚀 ~ changed ~ targetProperty:',
      isPropertyMethod(targetProperty),
      isPropertyArrowFunction(targetProperty),
      targetProperty.getFlags()
    );
    console.log(
      '🚀 ~ changed ~ sourceProperty:',
      isPropertyMethod(sourceProperty),
      isPropertyArrowFunction(sourceProperty),
      sourceProperty.getFlags()
    );

    // handle method and arrow function
    if (
      (isPropertyMethod(targetProperty) || isPropertyArrowFunction(targetProperty)) &&
      (isPropertyMethod(sourceProperty) || isPropertyArrowFunction(sourceProperty))
    ) {
      const functionPropertyDetails = findFunctionPropertyBreakingChangeDetails(sourceProperty, targetProperty);
      console.log('🚀 ~ changed ~ functionPropertyDetails:', functionPropertyDetails);
      return [...result, ...functionPropertyDetails];
    }

    const x = targetProperty.getValueDeclaration()?.getText() || '';
    const y = sourceProperty.getValueDeclaration()?.getText() || '';
    console.log(
      '🚀 ~ changed ~ x:',
      x,
      isPropertyMethod(targetProperty),
      isPropertyArrowFunction(targetProperty),
      targetProperty.getFlags()
    );
    console.log(
      '🚀 ~ changed ~ y:',
      y,
      isPropertyMethod(sourceProperty),
      isPropertyArrowFunction(sourceProperty),
      sourceProperty.getFlags()
    );
    throw new Error('Should never reach here');
  }, new Array<DiffPair>());
  return [...removed, ...changed];
}

function findReturnTypeBreakingChangesCore(source: Node, target: Node): DiffPair[] {
  const getName = (node: Node) => (Node.hasName(node) ? node.getName() : node.getText());
  const targetNameNode = target ? { name: getName(target), node: target } : undefined;
  const sourceNameNode = source ? { name: getName(source), node: source } : undefined;

  const isSourceConstructorDeclaration = Node.isConstructorDeclaration(source);
  const isTargetConstructorDeclaration = Node.isConstructorDeclaration(target);
  if (isSourceConstructorDeclaration !== isTargetConstructorDeclaration) {
    const pair = createDiffPair(DiffLocation.Signature, DiffReasons.NotComparable, sourceNameNode, targetNameNode);
    return [pair];
  }
  if (isSourceConstructorDeclaration) return [];

  const reasons = findBreakingReasons(source, target);
  if (reasons === DiffReasons.None) return [];
  const pair = createDiffPair(DiffLocation.Signature_ReturnType, reasons, sourceNameNode, targetNameNode);
  return [pair];
}

function findReturnTypeBreakingChanges(sourceMethod: Symbol, targetMethod: Symbol): DiffPair[] {
  const targetDeclaration = targetMethod.getValueDeclarationOrThrow();
  const sourceDeclaration = sourceMethod.getValueDeclarationOrThrow();
  return findReturnTypeBreakingChangesCore(sourceDeclaration, targetDeclaration);
}

function findParameterBreakingChangesCore(
  sourceParameters: ParameterDeclaration[],
  targetParameters: ParameterDeclaration[],
  sourceName: string,
  targetName: string,
  sourceNode: Node,
  targetNode: Node
): DiffPair[] {
  const pairs: DiffPair[] = [];

  // handle parameter counts
  const isSameParameterCount = targetParameters.length === sourceParameters.length;
  if (!isSameParameterCount) {
    const source = { name: sourceName, node: sourceNode };
    const target = { name: targetName, node: targetNode };
    const pair = createDiffPair(DiffLocation.Signature_ParameterList, DiffReasons.CountChanged, source, target);
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
    const reasons = findBreakingReasons(sourceParameter, targetParameter);
    const pair = createDiffPair(DiffLocation.Parameter, reasons, source, target);
    pair.reasons = findBreakingReasons(sourceParameter, targetParameter);
    if (pair.reasons !== DiffReasons.None) pairs.push(pair);
  });

  return pairs;
}

// TODO: not support for overloads
function findParameterBreakingChanges(sourceMethod: Symbol, targetMethod: Symbol): DiffPair[] {
  const targetParameters = getCallableEntityParametersFromSymbol(targetMethod);
  const sourceParameters = getCallableEntityParametersFromSymbol(sourceMethod);
  return findParameterBreakingChangesCore(
    sourceParameters,
    targetParameters,
    sourceMethod.getName(),
    targetMethod.getName(),
    sourceMethod.getValueDeclarationOrThrow(),
    targetMethod.getValueDeclarationOrThrow()
  );
}

function findFunctionPropertyBreakingChangeDetails(sourceMethod: Symbol, targetMethod: Symbol): DiffPair[] {
  const returnTypePairs = findReturnTypeBreakingChanges(sourceMethod, targetMethod);
  const parameterPairs = findParameterBreakingChanges(sourceMethod, targetMethod);
  return [...returnTypePairs, ...parameterPairs];
}

function updateDiffPairForNewFeature(p: DiffPair): DiffPair {
  p.reasons = DiffReasons.Added;
  const temp = p.source;
  p.source = p.target;
  p.target = temp;
  return p;
}

// TODO: support readonly properties
// TODO: add generic test case: parameter with generic, return type with generic
export function findInterfaceDifferences(
  source: InterfaceDeclaration,
  target: InterfaceDeclaration,
  findMappingCallSignature = defaultFindMappingCallSignatureLikeDeclaration
): DiffPair[] {
  const getDeclaration = (s: Signature): CallSignatureDeclaration =>
    s.getDeclaration().asKindOrThrow(SyntaxKind.CallSignature);

  const targetSignatures = target
    .getType()
    .getCallSignatures()
    .map((c) => getDeclaration(c));
  const sourceSignatures = source
    .getType()
    .getCallSignatures()
    .map((c) => getDeclaration(c));
  const callSignatureBreakingChanges = findCallSignatureLikeDeclarationBreakingChanges(
    sourceSignatures,
    targetSignatures,
    findMappingCallSignature
  );
  const callSignatureNewFeatures = findCallSignatureLikeDeclarationBreakingChanges(
    targetSignatures,
    sourceSignatures,
    findMappingCallSignature
  )
    .filter((p) => p.reasons === DiffReasons.Removed)
    .map(updateDiffPairForNewFeature);
  const targetProperties = target.getType().getProperties();
  const sourceProperties = source.getType().getProperties();
  console.log('🚀 ~ source:', source.getText());
  console.log('🚀 ~ target:', target.getText());
  console.log(
    '🚀 ~ sourceProperties:',
    sourceProperties.map((p) => p.getValueDeclarationOrThrow().getText())
  );
  console.log(
    '🚀 ~ targetProperties:',
    targetProperties.map((p) => p.getValueDeclarationOrThrow().getText())
  );

  const propertyBreakingChanges = findPropertyBreakingChanges(sourceProperties, targetProperties);
  const propertyNewFeatures = findPropertyBreakingChanges(targetProperties, sourceProperties)
    .filter((p) => p.reasons === DiffReasons.Removed)
    .map(updateDiffPairForNewFeature);

  return [
    ...callSignatureBreakingChanges,
    ...callSignatureNewFeatures,
    ...propertyBreakingChanges,
    ...propertyNewFeatures,
  ];
}

// TODO: detect static properties and methods
export function findClassDifferences(source: ClassDeclaration, target: ClassDeclaration) {
  const getConstructors = (node: ClassDeclaration) => {
    return node
      .getChildSyntaxList()
      ?.getChildrenOfKind(SyntaxKind.Constructor) ?? [];
  };
  const sourceConstructors = getConstructors(source);
  const targetConstructors = getConstructors(target);
  console.log(
    '🚀 ~',
    source.getText(),
    '---',
    source.getChildren().map((m) => m.getKindName())
  );
  console.log(
    '🚀 ~ findClassDifferences ~ source.getConstructors().lenx:',
    sourceConstructors,
    // source
    //   .getChildSyntaxList()
    //   ?.getChildrenOfKind(SyntaxKind.Constructor)
    //   .map((c) => c.getText())
  );
  console.log('🚀 ~ findClassDifferences ~ target.getConstructors().lenx:', targetConstructors);

  // find constructor breaking changes
  const constructorBreakingChanges = findCallSignatureLikeDeclarationBreakingChanges(
    sourceConstructors,
    targetConstructors,
    defaultFindMappingCallSignatureLikeDeclaration
  );

  const constructorNewFeatures = findCallSignatureLikeDeclarationBreakingChanges(
    targetConstructors,
    sourceConstructors,
    defaultFindMappingCallSignatureLikeDeclaration
  )
    .filter((p) => p.reasons === DiffReasons.Removed)
    .map(updateDiffPairForNewFeature);

  const targetProperties = target.getType().getProperties();
  const sourceProperties = source.getType().getProperties();
  const propertyBreakingChanges = findPropertyBreakingChanges(sourceProperties, targetProperties);
  const propertyNewFeatures = findPropertyBreakingChanges(targetProperties, sourceProperties)
    .filter((p) => p.reasons === DiffReasons.Removed)
    .map(updateDiffPairForNewFeature);

  return [...constructorBreakingChanges, ...constructorNewFeatures, ...propertyBreakingChanges, ...propertyNewFeatures];
}

function findRemovedFunctionOverloads(
  sourceOverloads: FunctionDeclaration[],
  targetOverloads: FunctionDeclaration[]
): FunctionDeclaration[] {
  const overloads = targetOverloads.filter((t) => {
    const compatibleSourceFunction = sourceOverloads.find((s) => {
      // NOTE: isTypeAssignableTo does not work for overloads
      const returnTypePairs = [...findReturnTypeBreakingChangesCore(s, t), ...findReturnTypeBreakingChangesCore(t, s)];
      if (returnTypePairs.length > 0) return false;
      const parameterPairs = [
        ...findParameterBreakingChangesCore(s.getParameters(), t.getParameters(), '', '', s, t),
        ...findParameterBreakingChangesCore(t.getParameters(), s.getParameters(), '', '', t, s),
      ];
      return parameterPairs.length === 0;
    });
    return compatibleSourceFunction === undefined;
  });
  return overloads;
}

// TODO: support arrow function
export function findFunctionDifferences(source: FunctionDeclaration, target: FunctionDeclaration): DiffPair[] {
  const sourceOverloads = source.getOverloads();
  const targetOverloads = target.getOverloads();

  // function has overloads
  if (sourceOverloads.length > 1 || targetOverloads.length > 1) {
    const removedPairs = findRemovedFunctionOverloads(sourceOverloads, targetOverloads).map((t) =>
      createDiffPair(DiffLocation.Signature_Overload, DiffReasons.Removed, undefined, {
        name: t.getName()!,
        node: t,
      })
    );

    const addedPairs = findRemovedFunctionOverloads(targetOverloads, sourceOverloads).map(
      (t) =>
        createDiffPair(DiffLocation.Signature_Overload, DiffReasons.Added, {
          name: t.getName()!,
          node: t,
        }),
      undefined
    );
    return [...removedPairs, ...addedPairs];
  }

  // function has no overloads
  const returnTypePairs = findReturnTypeBreakingChangesCore(source, target);

  const parameterPairs = findParameterBreakingChangesCore(
    source.getParameters(),
    target.getParameters(),
    source.getName()!,
    target.getName()!,
    source,
    target
  );

  return [...returnTypePairs, ...parameterPairs];
}

export function findTypeAliasBreakingChanges(source: TypeAliasDeclaration, target: TypeAliasDeclaration): DiffPair[] {
  if (source.getType().isAssignableTo(target.getType()) && target.getType().isAssignableTo(source.getType())) return [];

  let sourceNameNode: NameNode = { name: source.getName(), node: source };
  let targetNameNode: NameNode = { name: target.getName(), node: target };
  return [createDiffPair(DiffLocation.TypeAlias, DiffReasons.TypeChanged, sourceNameNode, targetNameNode)];
}

export function createDiffPair(
  location: DiffLocation,
  reasons: DiffReasons,
  source?: NameNode,
  target?: NameNode,
  assignDirection: AssignDirection = AssignDirection.None
): DiffPair {
  const messages = new Map<DiffReasons, string>();
  return { location, reasons, messages, target, source, assignDirection };
}

export function checkRemovedDeclaration(
  location: DiffLocation,
  baseline?: Node,
  current?: Node,
  assignDirection: AssignDirection = AssignDirection.CurrentToBaseline
): DiffPair | undefined {
  if (baseline && current) return undefined;

  const sourceNameNode =
    assignDirection === AssignDirection.BaselineToCurrent
      ? getNameNodeFromNode(baseline)
      : getNameNodeFromNode(current);
  const targetNameNode =
    assignDirection === AssignDirection.BaselineToCurrent
      ? getNameNodeFromNode(current)
      : getNameNodeFromNode(baseline);
  if (!current) return createDiffPair(location, DiffReasons.Removed, sourceNameNode, targetNameNode, assignDirection);
}

export function checkAddedDeclaration(
  location: DiffLocation,
  baseline?: Node,
  current?: Node,
  assignDirection: AssignDirection = AssignDirection.CurrentToBaseline
): DiffPair | undefined {
  if (baseline && current) return undefined;

  const sourceNameNode =
    assignDirection === AssignDirection.BaselineToCurrent
      ? getNameNodeFromNode(baseline)
      : getNameNodeFromNode(current);
  const targetNameNode =
    assignDirection === AssignDirection.BaselineToCurrent
      ? getNameNodeFromNode(current)
      : getNameNodeFromNode(baseline);
  if (!baseline) return createDiffPair(location, DiffReasons.Added, sourceNameNode, targetNameNode, assignDirection);
}

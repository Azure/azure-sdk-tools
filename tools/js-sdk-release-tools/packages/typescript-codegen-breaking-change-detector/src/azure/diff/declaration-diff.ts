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
  TypeNode,
  TypeAliasDeclaration,
  CallSignatureDeclaration,
  ClassDeclaration,
  ConstructorDeclaration,
  ClassMemberTypes,
  ts,
} from 'ts-morph';
import {
  DiffLocation,
  DiffPair,
  DiffReasons,
  FindMappingCallSignature,
  AssignDirection,
  NameNode,
  FindMappingConstructor,
} from '../common/types';
import {
  getCallableEntityParametersFromSymbol,
  isMethodOrArrowFunction,
  isPropertyArrowFunction,
  isPropertyMethod,
  isClassMethod,
  isSameSignature,
  isSameConstructor,
} from '../../utils/ast-utils';

function findBreakingReasons(source: Node, target: Node): DiffReasons {
  // Note: if return type node defined,
  // it's a funtion/method/signature's return type node,
  // return it, it will be used to compare later
  // Otherwise, it's a non-funtion/method/signature node, return its type node
  const getTypeNode = (node: Node): TypeNode => {
    if (Node.isReturnTyped(node)) return node.getReturnTypeNodeOrThrow();
    if (Node.isTyped(node)) return node.getTypeNodeOrThrow();
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
  const isOptional = (node: Node) => node.asKind(SyntaxKind.Parameter)?.isOptional() || node.getSymbolOrThrow().isOptional();
  const incompatibleOptional = isOptional(target) && !isOptional(source);
  if (incompatibleOptional) breakingReasons |= DiffReasons.RequiredToOptional;

  // check readonly -> mutable
  const isReadonly = (node: Node) => Node.isReadonlyable(node) && node.isReadonly();
  const incompatibleReadonly = isReadonly(target) && !isReadonly(source);
  if (incompatibleReadonly) breakingReasons |= DiffReasons.ReadonlyToMutable;

  return breakingReasons;
}

function findBreakingChanges<T extends Signature | ConstructorDeclaration>(
  sources: T[],
  targets: T[],
  isSame: (a: T, b: T) => boolean,
  getParameters: (s: T) => any[],
  getNameNode: (s: T) => NameNode,
  getDeclaration?: (s: T) => any,  
  findMappingCallSignature?: FindMappingCallSignature
): DiffPair[] {
  const defaultFindMapping = (target: any, constraints: any[]) => {
    const mapped = constraints.find((s) => isSame(target, s));
    if (!mapped) return undefined;
    const id = mapped instanceof Signature ? mapped.getDeclaration().asKindOrThrow(SyntaxKind.CallSignature).getText():"";
    return { mapped, id: id};
  };

  const pairs = targets.reduce((result, target) => {    
    const resolvedFindMappingCallSignature = findMappingCallSignature || defaultFindMapping;
    const sourceContext = resolvedFindMappingCallSignature(target as Node, sources);
    if (sourceContext) {
      const source = sourceContext.mapped;
      if(getDeclaration) {
        // handle return type
        const targetDeclaration = getDeclaration(target)!;
        const sourceDeclaration = getDeclaration(source)!;
        const returnPairs = findReturnTypeBreakingChangesCore(sourceDeclaration, targetDeclaration);
        if (returnPairs.length > 0) result.push(...returnPairs);
      }      

      // handle parameters
      const path = sourceContext.id;
      const sourceNode = source instanceof Signature ? source.getDeclaration(): source;
      const targetNode = target instanceof Signature ? target.getDeclaration(): target as Node;
      const parameterPairs = findParameterBreakingChangesCore(
        getParameters(source),
        getParameters(target),
        path,
        path,
        sourceNode,
        targetNode
      );
      if (parameterPairs.length > 0) result.push(...parameterPairs);

      return result;
    }

    // not found
    const targetNameNode = getNameNode(target);
    const location = target instanceof Signature ? DiffLocation.Signature : DiffLocation.Constructor;
    const pair = createDiffPair(location, DiffReasons.Removed, undefined, targetNameNode);
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
  const reasons = findBreakingReasons(
    sourceProperty.getValueDeclarationOrThrow(),
    targetProperty.getValueDeclarationOrThrow()
  );

  if (reasons === DiffReasons.None) return undefined;
  return createDiffPair(
    DiffLocation.Property,
    reasons,
    getNameNodeFromSymbol(targetProperty),
    getNameNodeFromSymbol(sourceProperty)
  );
}

// NOTE: this function compares methods and arrow functions in interface
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

    // handle classic property
    if (isTargetPropertyClassic && isSourcePropertyClassic) {
      const classicBreakingPair = findClassicPropertyBreakingChanges(sourceProperty, targetProperty);
      if (!classicBreakingPair) return result;
      return [...result, classicBreakingPair];
    }

    // handle method and arrow function
    if (
      (isPropertyMethod(targetProperty) || isPropertyArrowFunction(targetProperty)) &&
      (isPropertyMethod(sourceProperty) || isPropertyArrowFunction(sourceProperty))
    ) {
      const functionPropertyDetails = findFunctionPropertyBreakingChangeDetails(sourceProperty, targetProperty);
      return [...result, ...functionPropertyDetails];
    }

    if(isClassMethod(targetProperty) && isClassMethod(sourceProperty)) {
      return []
    }

    throw new Error('Should never reach here');
  }, new Array<DiffPair>());
  return [...removed, ...changed];
}

function findReturnTypeBreakingChangesCore(source: Node, target: Node): DiffPair[] {
  const reasons = findBreakingReasons(source, target);
  if (reasons === DiffReasons.None) return [];
  const getName = (node: Node) => (Node.hasName(node) ? node.getName() : node.getText());
  const targetNameNode = target ? { name: getName(target), node: target } : undefined;
  const sourceNameNode = source ? { name: getName(source), node: source } : undefined;
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

// TODO: support readonly properties
// TODO: add generic test case: parameter with generic, return type with generic
export function findInterfaceBreakingChanges(
  source: InterfaceDeclaration,
  target: InterfaceDeclaration,
  findMappingCallSignature?: FindMappingCallSignature
): DiffPair[] {
  const getDeclaration = (s: Signature): CallSignatureDeclaration => 
    s.getDeclaration().asKindOrThrow(SyntaxKind.CallSignature);
  const getParameters = (s: Signature): ParameterDeclaration[] =>
    s.getDeclaration().asKindOrThrow(SyntaxKind.CallSignature).getParameters();
  const getNameNode = (s: Signature): NameNode => 
    ({ name: s.getDeclaration().getText(), node: s.getDeclaration() });

  const targetSignatures = target.getType().getCallSignatures();
  const sourceSignatures = source.getType().getCallSignatures();
  const callSignatureBreakingChanges = findBreakingChanges(sourceSignatures , targetSignatures, isSameSignature, getParameters, getNameNode, getDeclaration, findMappingCallSignature)
  const targetProperties = target.getType().getProperties();
  const sourceProperties = source.getType().getProperties();

  const propertyBreakingChanges = findPropertyBreakingChanges(sourceProperties, targetProperties);

  return [...callSignatureBreakingChanges, ...propertyBreakingChanges];
}

export function findRemovedFunctionOverloads<T extends ConstructorDeclaration | FunctionDeclaration>(
  sourceOverloads: T[],
  targetOverloads: T[]
): T[] {
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
export function findFunctionBreakingChanges(source: FunctionDeclaration, target: FunctionDeclaration): DiffPair[] {
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

export function findClassDeclarationBreakingChanges(source: ClassDeclaration, target: ClassDeclaration): DiffPair[] {
  const getParameters = (s: ConstructorDeclaration): ParameterDeclaration[] => 
    s.getParameters();
  const getNameNode = (c: ConstructorDeclaration): NameNode => 
    ({ name: c.getText(), node: c });
  
  const targetConstructors = target.getConstructors();
  const sourceConstructors = source.getConstructors();  
  const constructorBreakingChanges = findBreakingChanges(sourceConstructors, targetConstructors, isSameConstructor, getParameters, getNameNode);
  const targetProperties = target.getType().getProperties();
  const sourceProperties = source.getType().getProperties();
  const propertyBreakingChanges = findPropertyBreakingChanges(sourceProperties, targetProperties);

  const targetMembers = target.getMembers();
  const sourceMembers = source.getMembers();
  const memberBreakingChanges = findMemberBreakingChanges(sourceMembers, targetMembers);
  
  return [...constructorBreakingChanges, ...propertyBreakingChanges, ...memberBreakingChanges];
}

function findMemberBreakingChanges(sourceMembers: ClassMemberTypes[], targetMembers: ClassMemberTypes[]): DiffPair[] {
  const sourcePropMap = sourceMembers.reduce((map, p) => {
    map.set(p.getSymbol()!.getName(), p);
    return map;
  }, new Map<string, ClassMemberTypes>());

  const getLocation = (symbol: Symbol): DiffLocation => {
    let location;
    switch(symbol.getFlags()){
      case SymbolFlags.Method:
        location = DiffLocation.Signature;
        break;
      case SymbolFlags.Property:  
        location = DiffLocation.Property;
        break;  
      default:
        location = DiffLocation.Constructor;
    }
    return location
  };  

  const changed = targetMembers.reduce((result, targetMember) => {
    const name = targetMember.getSymbol()!.getName();
    const sourceMember = sourcePropMap.get(name);
    if (!sourceMember) return result;

    const targetMemberModifierFlag = targetMember.getCombinedModifierFlags();
    const sourceMemberModifierFlag = sourceMember.getCombinedModifierFlags();
    const location = getLocation(targetMember.getSymbol()!);
    const allowedFlags = [ts.ModifierFlags.Public, ts.ModifierFlags.None];
    if(location === DiffLocation.Signature && allowedFlags.includes(sourceMemberModifierFlag)) {
      // optional method becomes required method is not a breaking change.
      return [];      
    }
    if (targetMemberModifierFlag !== sourceMemberModifierFlag) {
      return [
        ...result,
        createDiffPair(
          location,
          DiffReasons.ModifierFlag,
          getNameNodeFromSymbol(sourceMember.getSymbol()!),
          getNameNodeFromSymbol(targetMember.getSymbol()!)
        ),
      ];
    }
    return result;
  }, new Array<DiffPair>());
  return [ ...changed];
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

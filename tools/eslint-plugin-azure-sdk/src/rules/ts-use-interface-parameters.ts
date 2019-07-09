/**
 * @fileoverview Rule to encourage usage of interfaces over classes as function parameters.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import {
  AssignmentPattern,
  FunctionDeclaration,
  FunctionExpression,
  Identifier,
  MethodDefinition,
  Pattern
} from "estree";
import {
  Declaration,
  isArrayTypeNode,
  Node,
  PropertySignature,
  Symbol,
  SymbolFlags,
  Type,
  TypeChecker,
  SourceFile,
  TypeReferenceNode,
  TypeReference
} from "typescript";
import { ParserServices } from "@typescript-eslint/experimental-utils";
import { TSESTree, TSNode } from "@typescript-eslint/typescript-estree";
import { ParserWeakMap } from "@typescript-eslint/typescript-estree/dist/parser-options";

//------------------------------------------------------------------------------
// Helpers
//------------------------------------------------------------------------------

type FunctionType = FunctionExpression | FunctionDeclaration;

const getParamAsIdentifier = (param: Pattern): Identifier => {
  let identifier = param;
  if (param.type === "AssignmentPattern") {
    const assignmentPattern: AssignmentPattern = param as AssignmentPattern;
    identifier = assignmentPattern.left;
  }
  return identifier as Identifier;
};

const getTypeOfParam = (
  param: Pattern,
  converter: ParserWeakMap<TSESTree.Node, TSNode>,
  typeChecker: TypeChecker
): Type => {
  const identifier = getParamAsIdentifier(param);
  const tsNode = converter.get(identifier as TSESTree.Node);

  const type = typeChecker.getTypeAtLocation(tsNode) as TypeReference;
  const typeNode = typeChecker.typeToTypeNode(type);
  if (typeNode !== undefined && isArrayTypeNode(typeNode)) {
    const elementTypeReference = typeNode.elementType as TypeReferenceNode;
    const typeName = elementTypeReference.typeName as any; // eslint-disable-line @typescript-eslint/no-explicit-any
    return typeChecker.getDeclaredTypeOfSymbol(typeName.symbol);
  }
  return type;
};

/* eslint-disable @typescript-eslint/ban-types */
const addSeenSymbols = (
  symbol: Symbol,
  symbols: Symbol[],
  typeChecker: TypeChecker
): void => {
  symbols.push(symbol);
  typeChecker
    .getPropertiesOfType(typeChecker.getDeclaredTypeOfSymbol(symbol))
    .forEach((element: Symbol): void => {
      const memberType = typeChecker.getTypeAtLocation(
        element.valueDeclaration
      );
      const memberTypeNode = typeChecker.typeToTypeNode(memberType);
      let memberSymbol: Symbol | undefined;
      if (memberTypeNode !== undefined && isArrayTypeNode(memberTypeNode)) {
        const elementTypeReference = memberTypeNode.elementType as TypeReferenceNode;
        const typeName = elementTypeReference.typeName as any; // eslint-disable-line @typescript-eslint/no-explicit-any
        memberSymbol = typeName.symbol;
      } else {
        memberSymbol = memberType.getSymbol();
      }
      if (memberSymbol !== undefined) {
        let isExternal = false;
        let isOptional = false;
        const declaration: PropertySignature = memberSymbol.valueDeclaration as PropertySignature;
        if (declaration !== undefined) {
          isOptional = declaration.questionToken !== undefined;
          let parent: Node = declaration.parent;
          while (
            !parent.hasOwnProperty("fileName") &&
            parent.hasOwnProperty("parent")
          ) {
            parent = parent.parent;
          }
          const sourceFile = parent as SourceFile;
          const externalRegex = /node_modules/;
          isExternal = externalRegex.test(sourceFile.fileName);
        }
        !isExternal &&
          !isOptional &&
          [SymbolFlags.Class, SymbolFlags.Interface].includes(
            memberSymbol.getFlags()
          ) &&
          !symbols.includes(memberSymbol) &&
          addSeenSymbols(memberSymbol, symbols, typeChecker);
      }
    });
};

const getSymbolsUsedInParam = (
  param: Pattern,
  converter: ParserWeakMap<TSESTree.Node, TSNode>,
  typeChecker: TypeChecker
): Symbol[] => {
  const symbols: Symbol[] = [];
  const type = getTypeOfParam(param, converter, typeChecker);
  const symbol = type.getSymbol();
  if (symbol !== undefined) {
    addSeenSymbols(symbol, symbols, typeChecker);
  }
  return symbols;
};

const isValidParam = (
  param: Pattern,
  converter: ParserWeakMap<TSESTree.Node, TSNode>,
  typeChecker: TypeChecker
): boolean => {
  const tsIdentifier: TSESTree.Identifier = param as TSESTree.Identifier;
  if (tsIdentifier.optional) {
    return true;
  }
  return getSymbolsUsedInParam(param, converter, typeChecker).every(
    (symbol: Symbol): boolean => {
      return symbol === undefined || symbol.getFlags() !== SymbolFlags.Class;
    }
  );
};
/* eslint-enable @typescript-eslint/ban-types */

const isValidOverload = (
  overloads: FunctionType[],
  converter: ParserWeakMap<TSESTree.Node, TSNode>,
  typeChecker: TypeChecker
): boolean => {
  return overloads.some((overload: FunctionType): boolean => {
    return overload.params.every((overloadParam: Pattern): boolean => {
      return isValidParam(overloadParam, converter, typeChecker);
    });
  });
};

const evaluateOverloads = (
  overloads: FunctionType[],
  converter: ParserWeakMap<TSESTree.Node, TSNode>,
  typeChecker: TypeChecker,
  verified: string[],
  name: string,
  param: Pattern,
  context: Rule.RuleContext
): void => {
  if (
    overloads.length !== 0 &&
    isValidOverload(overloads, converter, typeChecker)
  ) {
    verified.push(name);
    return;
  }

  const type = getTypeOfParam(param, converter, typeChecker);
  const identifier = getParamAsIdentifier(param);
  context.report({
    node: identifier,
    message:
      "type {{ type }} of parameter {{ param }} of function {{ func }} is a class or contains a class as a member",
    data: {
      type: typeChecker.typeToString(type),
      param: identifier.name,
      func: name
    }
  });
};

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description:
        "encourage usage of interfaces over classes as function parameters",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-use-interface-parameters"
    },
    schema: [] // no options
  },

  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const parserServices: ParserServices = context.parserServices;
    if (
      parserServices.program === undefined ||
      parserServices.esTreeNodeToTSNodeMap === undefined
    ) {
      return {};
    }
    const typeChecker = parserServices.program.getTypeChecker();
    const converter = parserServices.esTreeNodeToTSNodeMap;
    const reverter: ParserWeakMap<
      TSNode,
      TSESTree.Node
    > = parserServices.tsNodeToESTreeNodeMap as ParserWeakMap<
      TSNode,
      TSESTree.Node
    >;

    const verifiedMethods: string[] = [];
    const verifiedDeclarations: string[] = [];

    return {
      "MethodDefinition > FunctionExpression": (
        node: FunctionExpression
      ): void => {
        const ancestors = context.getAncestors().reverse();
        const parent: MethodDefinition = ancestors[0] as MethodDefinition;
        const key: Identifier = parent.key as Identifier;
        const name = key.name;

        if (
          name !== undefined &&
          name !== "" &&
          verifiedMethods.includes(name)
        ) {
          return;
        }

        node.params.forEach((param: Pattern): void => {
          if (!isValidParam(param, converter, typeChecker)) {
            const tsNode = converter.get(node as TSESTree.Node);
            const type = typeChecker.getTypeAtLocation(tsNode);
            const symbol = type.getSymbol();
            const overloads =
              symbol !== undefined
                ? symbol.declarations.map(
                    (declaration: Declaration): FunctionExpression => {
                      const method: MethodDefinition = reverter.get(
                        declaration as TSNode
                      ) as MethodDefinition;
                      return method.value;
                    }
                  )
                : [];
            evaluateOverloads(
              overloads,
              converter,
              typeChecker,
              verifiedMethods,
              name,
              param,
              context
            );
          }
        });
      },

      FunctionDeclaration: (node: FunctionDeclaration): void => {
        const id: Identifier = node.id as Identifier;
        const name = id.name;
        if (
          name !== undefined &&
          name !== "" &&
          verifiedDeclarations.includes(name)
        ) {
          return;
        }

        node.params.forEach((param: Pattern): void => {
          if (!isValidParam(param, converter, typeChecker)) {
            const tsNode = converter.get(node as TSESTree.Node);
            const type = typeChecker.getTypeAtLocation(tsNode);
            const symbol = type.getSymbol();
            const overloads =
              symbol !== undefined
                ? symbol.declarations.map(
                    (declaration: Declaration): FunctionDeclaration => {
                      return reverter.get(
                        declaration as TSNode
                      ) as FunctionDeclaration;
                    }
                  )
                : [];
            evaluateOverloads(
              overloads,
              converter,
              typeChecker,
              verifiedDeclarations,
              name,
              param,
              context
            );
          }
        });
      }
    } as Rule.RuleListener;
  }
};

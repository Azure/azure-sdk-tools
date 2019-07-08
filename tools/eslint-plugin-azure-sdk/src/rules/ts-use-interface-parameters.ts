/**
 * @fileoverview Rule to encourage usage of interfaces over classes as function parameters.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import {
  AssignmentPattern,
  BlockStatement,
  ClassBody,
  FunctionDeclaration,
  FunctionExpression,
  Identifier,
  MethodDefinition,
  Node,
  Pattern,
  Program
} from "estree";
import {
  Node as NodeType,
  PropertySignature,
  Symbol,
  SymbolFlags,
  Type,
  TypeChecker,
  SourceFile
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
  return typeChecker.getTypeAtLocation(tsNode);
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
      const memberSymbol = typeChecker
        .getTypeAtLocation(element.valueDeclaration)
        .getSymbol();
      if (memberSymbol !== undefined) {
        let isExternal = false;
        let isOptional = false;
        const declaration: PropertySignature = memberSymbol.valueDeclaration as PropertySignature;
        if (declaration !== undefined) {
          isOptional = declaration.questionToken !== undefined;
          let parent: NodeType = declaration.parent;
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
            const bodyNode: ClassBody = ancestors.find(
              (ancestor: Node): boolean => {
                return ancestor.type === "ClassBody";
              }
            ) as ClassBody;
            const overloads: FunctionExpression[] = bodyNode.body
              .filter((element: Node): boolean => {
                if (element.type !== "MethodDefinition") {
                  return false;
                }
                const methodDefinition = element as MethodDefinition;
                const key: Identifier = methodDefinition.key as Identifier;
                const functionExpression = methodDefinition.value;
                return (
                  key.name === name && functionExpression.params !== node.params
                );
              })
              .map(
                (element: Node): FunctionExpression => {
                  const methodDefinition = element as MethodDefinition;
                  return methodDefinition.value;
                }
              );
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

        const ancestors = context.getAncestors().reverse();
        node.params.forEach((param: Pattern): void => {
          if (!isValidParam(param, converter, typeChecker)) {
            const bodyNode: BlockStatement | Program = ancestors.find(
              (ancestor: Node): boolean => {
                return ["BlockStatement", "Program"].includes(ancestor.type);
              }
            ) as BlockStatement | Program;
            const overloads: FunctionDeclaration[] = bodyNode.body.filter(
              (element: Node): boolean => {
                if (element.type !== "FunctionDeclaration") {
                  return false;
                }
                const functionDeclaration = element as FunctionDeclaration;
                const id: Identifier = functionDeclaration.id as Identifier;
                return (
                  id.name === name && functionDeclaration.params !== node.params
                );
              }
            ) as FunctionDeclaration[];
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

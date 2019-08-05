/**
 * @fileoverview Rule to require async client methods to accept an AbortSignalLike parameter.
 * @author Arpan Laha
 */

import {
  TSESTree,
  ParserServices
} from "@typescript-eslint/experimental-utils";
import { Rule } from "eslint";
import { ClassDeclaration, Identifier, MethodDefinition } from "estree";
import { getPublicMethods, getRuleMetaData } from "../utils";
import { Symbol as TSSymbol, SymbolFlags } from "typescript";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: getRuleMetaData(
    "ts-apisurface-supportcancellation",
    "require async client methods to accept an AbortSignalLike parameter"
  ),
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const parserServices = context.parserServices as ParserServices;
    if (
      parserServices.program === undefined ||
      parserServices.esTreeNodeToTSNodeMap === undefined
    ) {
      return {};
    }
    const typeChecker = parserServices.program.getTypeChecker();
    const converter = parserServices.esTreeNodeToTSNodeMap;
    return {
      // callback functions

      // call on Client classes
      "ClassDeclaration[id.name=/Client$/]": (node: ClassDeclaration): void => {
        getPublicMethods(node).forEach((method: MethodDefinition): void => {
          const key = method.key as Identifier;
          const TSFunction = method.value as TSESTree.FunctionExpression;

          // report if async and no parameter of type AbortSignalLike
          if (
            TSFunction.async &&
            TSFunction.params.every((param: TSESTree.Parameter): boolean => {
              // validate param type
              if (
                param.type === "Identifier" &&
                param.typeAnnotation !== undefined
              ) {
                const typeAnnotation = param.typeAnnotation.typeAnnotation;
                if (
                  typeAnnotation.type === "TSTypeReference" &&
                  typeAnnotation.typeName.type === "Identifier"
                ) {
                  if (typeAnnotation.typeName.name === "AbortSignalLike") {
                    return false;
                  }
                  // check for if param is an interface
                  const type = typeChecker.getTypeAtLocation(
                    converter.get(param)
                  );
                  const symbol = type.getSymbol();
                  if (
                    symbol === undefined ||
                    symbol.flags !== SymbolFlags.Interface
                  ) {
                    return true;
                  }
                  // check interface property type names for AbortSignalLike
                  return typeChecker
                    .getPropertiesOfType(type)
                    .every((memberSymbol: TSSymbol): boolean => {
                      const memberDeclaration = memberSymbol.valueDeclaration as any;
                      const memberType = memberDeclaration.type as any;
                      return (
                        memberType.typeName.escapedText !== "AbortSignalLike"
                      );
                    });
                }
              }
              return true;
            })
          ) {
            context.report({
              node: method,
              message: `async method ${key.name} should accept an AbortSignalLike parameter or option`
            });
          }
        });
      }
    } as Rule.RuleListener;
  }
};

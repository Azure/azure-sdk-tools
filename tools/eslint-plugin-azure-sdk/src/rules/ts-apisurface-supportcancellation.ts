/**
 * @fileoverview Rule to require async client methods to accept an AbortSignalLike parameter.
 * @author Arpan Laha
 */

import { TSESTree } from "@typescript-eslint/typescript-estree";
import { Rule } from "eslint";
import { ClassDeclaration, Identifier, MethodDefinition } from "estree";
import { getPublicMethods, getRuleMetaData } from "../utils";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: getRuleMetaData(
    "ts-apisurface-supportcancellation",
    "require async client methods to accept an AbortSignalLike parameter"
  ),
  create: (context: Rule.RuleContext): Rule.RuleListener =>
    ({
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
                  return typeAnnotation.typeName.name !== "AbortSignalLike";
                }
              }
              return true;
            })
          ) {
            context.report({
              node: method,
              message: `async method ${key.name} should accept an AbortSignalLike parameter`
            });
          }
        });
      }
    } as Rule.RuleListener)
};

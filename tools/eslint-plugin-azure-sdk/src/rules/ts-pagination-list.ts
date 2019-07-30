/**
 * @fileoverview Rule to require clients to include a list method returning a PagedAsyncIterableIterator.
 * @author Arpan Laha
 */

import {
  TSESTree,
  AST_NODE_TYPES
} from "@typescript-eslint/experimental-utils";
import { Rule } from "eslint";
import { ClassDeclaration, Identifier, MethodDefinition } from "estree";
import { getPublicMethods, getRuleMetaData } from "../utils";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: getRuleMetaData(
    "ts-pagination-list",
    "require clients to include a list method returning a PagedAsyncIterableIterator"
  ),
  create: (context: Rule.RuleContext): Rule.RuleListener =>
    ({
      // callback functions

      // call on Client classes
      "ClassDeclaration[id.name=/Client$/]": (node: ClassDeclaration): void => {
        const listMethods = getPublicMethods(node).filter(
          (method: MethodDefinition): boolean => {
            const key = method.key as Identifier;
            return !/^list($|([A-Z][a-zA-Z]*s$))/.test(key.name);
          }
        );

        if (listMethods.length === 0) {
          context.report({
            node: node,
            message: "no list method found"
          });
          return;
        }

        if (
          listMethods.every((listMethod: MethodDefinition): boolean => {
            const TSFunction = listMethod.value as TSESTree.FunctionExpression;
            if (
              TSFunction.returnType === undefined ||
              TSFunction.returnType.typeAnnotation.type !==
                AST_NODE_TYPES.TSTypeReference
            ) {
              return true;
            }
            const typeIdentifier = TSFunction.returnType.typeAnnotation
              .typeName as Identifier;
            return typeIdentifier.name !== "PagedAsyncIterableIterator";
          })
        ) {
          context.report({
            node: node,
            message: "list methods do not return a PagedAsyncIterableIterator"
          });
        }
      }
    } as Rule.RuleListener)
};

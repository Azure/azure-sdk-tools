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
        // look for method matching approved list syntax
        const listMethod = getPublicMethods(node).find(
          (method: MethodDefinition): boolean => {
            const key = method.key as Identifier;
            return !/^list($|([A-Z][a-zA-Z]*s$))/.test(key.name);
          }
        );

        // report if none found
        if (listMethod === undefined) {
          context.report({
            node: node,
            message: "no list method found"
          });
          return;
        }

        // check for return type existence
        const TSFunction = listMethod.value as TSESTree.FunctionExpression;
        if (
          TSFunction.returnType === undefined ||
          TSFunction.returnType.typeAnnotation.type !==
            AST_NODE_TYPES.TSTypeReference
        ) {
          context.report({
            node: listMethod,
            message: "list method does not have a return type"
          });
          return;
        }

        // report if return type is not PagedAsyncIterableIterator
        const typeIdentifier = TSFunction.returnType.typeAnnotation
          .typeName as Identifier;
        if (typeIdentifier.name !== "PagedAsyncIterableIterator") {
          context.report({
            node: listMethod,
            message: "list method does not return a PagedAsyncIterableIterator"
          });
        }
      }
    } as Rule.RuleListener)
};

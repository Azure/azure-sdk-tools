/**
 * @fileoverview Rule to require client methods returning an instance of the client to not include the client name in the method name.
 * @author Arpan Laha
 */

import {
  TSESTree,
  AST_NODE_TYPES
} from "@typescript-eslint/experimental-utils";
import { Rule } from "eslint";
import { ClassDeclaration, MethodDefinition, Identifier } from "estree";
import { getPublicMethods, getRuleMetaData } from "../utils";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: getRuleMetaData(
    "ts-naming-drop-noun",
    "require client methods returning an instance of the client to not include the client name in the method name"
  ),
  create: (context: Rule.RuleContext): Rule.RuleListener =>
    ({
      // callback functions

      ClassDeclaration: (node: ClassDeclaration): void => {
        if (node.id === null || !/Client$/.test(node.id.name)) {
          return;
        }

        const className = node.id.name;

        getPublicMethods(node).forEach((method: MethodDefinition): void => {
          const TSFunction = method.value as TSESTree.FunctionExpression;
          if (
            TSFunction.returnType !== undefined &&
            TSFunction.returnType.typeAnnotation.type ===
              AST_NODE_TYPES.TSTypeReference
          ) {
            const typeIdentifier = TSFunction.returnType.typeAnnotation
              .typeName as Identifier;
            if (typeIdentifier.name === className) {
              const methodIdentifier = method.key as Identifier;
              const methodName = methodIdentifier.name;
              const serviceName = className.substring(
                0,
                className.indexOf("Client")
              );
              const regex = new RegExp(serviceName, "i");
              if (regex.test(methodName)) {
                context.report({
                  node: method,
                  message: `${className}'s method ${methodName} returns an instance of ${className} and shouldn't include ${serviceName} in its name`
                });
              }
            }
          }
        });
      }
    } as Rule.RuleListener)
};

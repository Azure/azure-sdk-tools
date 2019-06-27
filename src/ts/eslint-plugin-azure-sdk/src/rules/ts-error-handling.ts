/**
 * @fileoverview Rule to limit thrown errors to ECMAScript built-in error types (TypeError, RangeError, Error).
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { Identifier, NewExpression, ThrowStatement } from "estree";
import { TypeChecker } from "typescript";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description:
        "limit thrown errors to ECMAScript built-in error types (TypeError, RangeError, Error)",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-error-handling"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    return {
      // callback functions

      // if throwing a literal value
      "ThrowStatement[argument.type='Literal']": (
        node: ThrowStatement
      ): void => {
        context.report({
          node: node,
          message: "statement is throwing a literal"
        });
      },

      "ThrowStatement[argument.type='Identifier']": (
        node: ThrowStatement
      ): void => {
        const thrown: Identifier = node.argument as Identifier;
        const parserServices = context.parserServices;
        const typeChecker: TypeChecker = parserServices.program.getTypeChecker();
        const TSNode = parserServices.esTreeNodeToTSNodeMap.get(thrown);
        const type = typeChecker.typeToString(
          typeChecker.getTypeAtLocation(TSNode)
        );

        const allowedTypes = ["TypeError", "RangeError", "Error", "any"];

        !allowedTypes.includes(type) &&
          context.report({
            node: thrown,
            message:
              "error thrown is not one of the following types: TypeError, RangeError, Error"
          });
      },

      // check to see that thrown error is valid type
      "ThrowStatement[argument.type='NewExpression']": (
        node: ThrowStatement
      ): void => {
        const allowedTypes = ["TypeError", "RangeError", "Error"];
        const argument = node.argument as NewExpression;
        const callee = argument.callee as Identifier;

        !allowedTypes.includes(callee.name) &&
          context.report({
            node: callee,
            message:
              "error thrown is not one of the following types: TypeError, RangeError, Error"
          });
      }
    } as Rule.RuleListener;
  }
};

/**
 * @fileoverview Rule to limit thrown errors to ECMAScript built-in error types (TypeError, RangeError, Error).
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { Identifier, NewExpression, ThrowStatement } from "estree";

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

      // if not 'throw new <error>'
      "ThrowStatement[argument.type!='NewExpression']": (
        node: ThrowStatement
      ): void => {
        context.report({
          node: node,
          message: "statement is not throwing a new error object"
        });
      },

      // check to see that thrown error is valid type
      "ThrowStatement[argument.type='NewExpression']": (
        node: ThrowStatement
      ): void => {
        const valid = ["TypeError", "RangeError", "Error"];
        const argument = node.argument as NewExpression;
        const callee = argument.callee as Identifier;

        !valid.includes(callee.name) &&
          context.report({
            node: callee,
            message:
              "error thrown is not one of the following types: TypeError, RangeError, Error"
          });
      }
    } as Rule.RuleListener;
  }
};

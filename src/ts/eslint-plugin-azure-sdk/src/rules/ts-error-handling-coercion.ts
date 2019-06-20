/**
 * @fileoverview Rule to encourage coercion of incorrect types when possible.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { Identifier } from "estree";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description: "encourage coercion of incorrect types when possible",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-error-handling-coercion"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    return {
      // callback functions

      // check to see that thrown error is valid type
      "ThrowStatement > NewExpression > Identifier[name='TypeError']": (
        node: Identifier
      ): void => {
        const ancestors = context.getAncestors().reverse();

        // look for closest ancestor where a choice was made
        const ifNode = ancestors.find(ancestor => {
          return ancestor.type === "IfStatement";
        });
        // return if none found
        if (!ifNode) {
          return;
        }
        context.report({
          node: node,
          message: "placeholder"
        });
      }
    } as Rule.RuleListener;
  }
};

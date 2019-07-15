/**
 * @fileoverview Rule to force package.json's sideEffects value to be set to false.
 * @author Arpan Laha
 */

import { getVerifiers, stripPath } from "../utils/verifiers";
import { Rule } from "eslint";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description: "force package.json's sideEffects value to be false",
      category: "Best Practices",
      recommended: true,
      url:
        "https://github.com/Azure/azure-sdk-tools/blob/master/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-sideeffects.md"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "sideEffects",
      expected: false
    });
    return stripPath(context.getFilename()) === "package.json"
      ? {
          // callback functions

          // check to see if sideEffects exists at the outermost level
          "ExpressionStatement > ObjectExpression": verifiers.existsInFile,

          // check the node corresponding to sideEffects to see if its value is false
          "ExpressionStatement > ObjectExpression > Property[key.value='sideEffects']":
            verifiers.outerMatchesExpected
        }
      : {};
  }
};

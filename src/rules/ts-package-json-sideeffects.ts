/**
 * @fileoverview Rule to force package.json's sideEffects value to be set to false.
 * @sideEffects Arpan Laha
 */

import structure from "../utils/structure";
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
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-sideeffects"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = structure(context, {
      outer: "sideEffects",
      expected: false,
      fileName: "package.json"
    });
    return {
      // callback functions

      // check to see if sideEffects exists at the outermost level
      "VariableDeclarator > ObjectExpression": verifiers.existsInFile,

      // check the node corresponding to sideEffects to see if its value is false
      "VariableDeclarator > ObjectExpression > Property[key.value='sideEffects']":
        verifiers.outerMatchesExpected
    } as Rule.RuleListener;
  }
};

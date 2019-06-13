/**
 * @fileoverview Rule to force tsconfig.json's "exclude" value to at least contain "node_modules"
 * @author Arpan Laha
 */

import getVerifiers from "../utils/verifiers";
import { Rule } from "eslint";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description:
        "force tsconfig.json's compilerOptions.exclude value to at least contain 'node_modules'",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-exclude"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "exclude",
      expected: "node_modules",
      fileName: "tsconfig.json"
    });
    return {
      // callback functions

      // check to see if exclude exists at the outermost level
      "VariableDeclarator > ObjectExpression": verifiers.existsInFile,

      // check the node corresponding to exclude to see if its value contains "node_modules"
      "VariableDeclarator > ObjectExpression > Property[key.value='exclude']":
        verifiers.outerContainsExpected
    } as Rule.RuleListener;
  }
};

/**
 * @fileoverview Rule to force tsconfig.json's "exclude" value to at least contain "node_modules"
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
      expected: "node_modules"
    });
    return stripPath(context.getFilename()) === "tsconfig.json"
      ? {
          // callback functions

          // check to see if exclude exists at the outermost level
          "ExpressionStatement > ObjectExpression": verifiers.existsInFile,

          // check the node corresponding to exclude to see if its value contains "node_modules"
          "ExpressionStatement > ObjectExpression > Property[key.value='exclude']":
            verifiers.outerContainsExpected
        }
      : {};
  }
};

/**
 * @fileoverview Rule to force there to be no default exports at the top level.
 */

import { Rule } from "eslint";
import { ExportDefaultDeclaration } from "estree";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description: "force there to be no default exports at the top level",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-modules-no-default"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    return {
      // callback functions
      ExportDefaultDeclaration: (node: ExportDefaultDeclaration): void => {
        context.report({
          node: node,
          message: "default exports exist at top level"
        });
      }
    } as Rule.RuleListener;
  }
};

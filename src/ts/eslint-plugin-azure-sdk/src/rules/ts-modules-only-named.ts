/**
 * @fileoverview Rule to force there to be no default exports at the top level.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { ExportDefaultDeclaration } from "estree";
import { normalize, relative } from "path";

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
    return !relative(
      normalize(context.getFilename()),
      normalize(context.settings.main)
    )
      ? // return context
        //   .getFilename()
        //   .replace("\\", "/")
        //   .endsWith(context.settings.main.replace("\\", "/"))
        ({
          // callback functions
          ExportDefaultDeclaration: (node: ExportDefaultDeclaration): void => {
            context.report({
              node: node,
              message: "default exports exist at top level"
            });
          }
        } as Rule.RuleListener)
      : {};
  }
};

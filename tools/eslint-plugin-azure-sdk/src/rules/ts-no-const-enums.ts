/**
 * @fileoverview Rule to forbid usage of TypeScript's const enums.
 * @author Arpan Laha
 */

import { Rule } from "eslint";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description: "forbid usage of TypeScript's const enums",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-no-const-enums"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    return {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      TSEnumDeclaration: (node: any): void => {
        node.const &&
          context.report({
            node: node,
            message: "const enums should not be used"
          });
      }
    } as Rule.RuleListener;
  }
};

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
        "https://github.com/Azure/azure-sdk-tools/blob/master/tools/eslint-plugin-azure-sdk/docs/rules/ts-no-const-enums.md"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    return {
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

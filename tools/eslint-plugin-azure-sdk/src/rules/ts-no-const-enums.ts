/**
 * @fileoverview Rule to forbid usage of TypeScript's const enums.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { getRuleMetaData } from "../utils";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: getRuleMetaData(
    "ts-no-const-enums",
    "forbid usage of TypeScript's const enums"
  ),
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    return {
      // callback functions

      // check Enum to make sure it doesn't have a const keyword
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

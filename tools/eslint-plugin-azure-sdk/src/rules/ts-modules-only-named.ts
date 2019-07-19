/**
 * @fileoverview Rule to force there to be only named exports at the top level.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { ExportDefaultDeclaration } from "estree";
import { getRuleMetaData } from "../utils";

// @ts-ignore (path has no typings)
import { normalize, relative } from "path";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: getRuleMetaData(
    "ts-modules-only-named",
    "force there to be only named exports at the top level"
  ),
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    return relative(
      normalize(context.getFilename()),
      normalize(context.settings.main)
    ) === ""
      ? ({
          // callback functions

          // throw error if an export default declaration is seen
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

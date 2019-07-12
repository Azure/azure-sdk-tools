/**
 * @fileoverview Rule to require TSDoc comments to include '@ignore' if the object is not public-facing.
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { getLocalExports } from "../utils";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description:
        "require TSDoc comments to include '@ignore' if the object is not public-facing",
      category: "Best Practices",
      recommended: true,
      url: "to be added" //TODO
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    if (!context.settings.exported) {
      const packageExports = getLocalExports(context);
      if (packageExports !== undefined) {
        context.settings.exported = packageExports;
      } else {
        return {};
      }
    }
    return {
      // callback functions
    } as Rule.RuleListener;
  }
};

/**
 * @fileoverview Rule to force package.json's author value to be set to "Microsoft Corporation".
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
        "force package.json's author value to be 'Microsoft Corporation'",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-author"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "author",
      expected: "Microsoft Corporation"
    });
    return stripPath(context.getFilename()) === "package.json"
      ? {
          // callback functions

          // check to see if author exists at the outermost level
          "ExpressionStatement > ObjectExpression": verifiers.existsInFile,

          // check the node corresponding to author to see if its value is "Microsoft Corporation"
          "ExpressionStatement > ObjectExpression > Property[key.value='author']":
            verifiers.outerMatchesExpected
        }
      : {};
  }
};

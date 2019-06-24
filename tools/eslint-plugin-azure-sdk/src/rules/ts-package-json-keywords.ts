/**
 * @fileoverview Rule to force package.json's keywords value to contain at least "Azure" and "cloud".
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
      outer: "keywords",
      expected: ["Azure", "cloud"]
    });
    return stripPath(context.getFilename()) === "package.json"
      ? {
          // callback functions

          // check to see if keywords exists at the outermost level
          "ExpressionStatement > ObjectExpression": verifiers.existsInFile,

          // check the node corresponding to keywords to see if its value contains "Azure" and "cloud"
          "ExpressionStatement > ObjectExpression > Property[key.value='keywords']":
            verifiers.outerContainsExpected
        }
      : {};
  }
};

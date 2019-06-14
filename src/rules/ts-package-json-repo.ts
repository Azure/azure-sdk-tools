/**
 * @fileoverview Rule to force package.json's repository value to be set to github:Azure/azure-sdk-for-js.
 * @author Arpan Laha
 */

import getVerifiers from "../utils/verifiers";
import { Rule } from "eslint";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description:
        "force package.json's repository value to be 'github:Azure/azure-sdk-for-js'",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-repo"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "repository",
      expected: "github:Azure/azure-sdk-for-js",
      fileName: "package.json"
    });
    return {
      // callback functions

      // check to see if repository exists at the outermost level
      "Program > ObjectExpression": verifiers.existsInFile,

      // check the node corresponding to repository to see if its value is github:Azure/azure-sdk-for-js
      "Program > ObjectExpression > Property[key.value='repository']":
        verifiers.outerMatchesExpected
    } as Rule.RuleListener;
  }
};

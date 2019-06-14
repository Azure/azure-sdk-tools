/**
 * @fileoverview Rule to force package.json's bugs.url value to be "https://github.com/Azure/azure-sdk-for-js/issues".
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
        "force package.json's bugs.url value to be 'https://github.com/Azure/azure-sdk-for-js/issues'",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-bugs"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "bugs",
      inner: "url",
      expected: "https://github.com/Azure/azure-sdk-for-js/issues",
      fileName: "package.json"
    });
    return {
      // callback functions

      // check to see if bugs exists at the outermost level
      "Program > ObjectExpression": verifiers.existsInFile,

      // check that url is a member of bugs
      "Property[key.value='bugs']": verifiers.isMemberOf,

      // check the node corresponding to bugs.url to see if it is set to 'https://github.com/Azure/azure-sdk-for-js/issues'
      "Program > ObjectExpression > Property[key.value='bugs'] > ObjectExpression > Property[key.value='url']":
        verifiers.innerMatchesExpected
    } as Rule.RuleListener;
  }
};

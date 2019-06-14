/**
 * @fileoverview Rule to recommend tsconfig.json's compilerOptions.isolatedModules value to be true.
 * @author Arpan Laha
 */

import getVerifiers from "../utils/verifiers";
import { Rule } from "eslint";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "suggestion",

    docs: {
      description:
        "recommend tsconfig.json's compilerOptions.isolatedModules value to be true",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-isolatedModules"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "compilerOptions",
      inner: "isolatedModules",
      expected: true,
      fileName: "tsconfig.json"
    });
    return {
      // callback functions

      // check to see if compilerOptions exists at the outermost level
      "ExpressionStatement > ObjectExpression": verifiers.existsInFile,

      // check that isolatedModules is a member of compilerOptions
      "Property[key.value='compilerOptions']": verifiers.isMemberOf,

      // check the node corresponding to compilerOptions.isolatedModules to see if it is set to true
      "ExpressionStatement > ObjectExpression > Property[key.value='compilerOptions'] > ObjectExpression > Property[key.value='isolatedModules']":
        verifiers.innerMatchesExpected
    } as Rule.RuleListener;
  }
};

/**
 * @fileoverview Rule to force tsconfig.json's compilerOptions.experimentalDecorators value to be false.
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
        "force tsconfig.json's compilerOptions.experimentalDecorators value to be false",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-experimentaldecorators"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "compilerOptions",
      inner: "experimentalDecorators",
      expected: false,
      fileName: "tsconfig.json"
    });
    return {
      // callback functions

      // check to see if compilerOptions exists at the outermost level
      "Program > ObjectExpression": verifiers.existsInFile,

      // check the node corresponding to compilerOptions.experimentalDecorators to see if it is set to false
      "Program > ObjectExpression > Property[key.value='compilerOptions'] > ObjectExpression > Property[key.value='experimentalDecorators']":
        verifiers.innerMatchesExpected
    } as Rule.RuleListener;
  }
};

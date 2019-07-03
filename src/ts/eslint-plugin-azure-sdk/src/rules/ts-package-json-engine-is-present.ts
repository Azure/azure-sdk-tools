/**
 * @fileoverview Rule to force Node support for all LTS versions.
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
      description: "force Node support for all LTS versions",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-engine-is-present"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    /**
     * definition of LTS Node versions
     * * needs updating as definitions change
     */
    const LTS = ">=8.0.0";

    const verifiers = getVerifiers(context, {
      outer: "engines",
      inner: "node",
      expected: LTS
    });
    return stripPath(context.getFilename()) === "package.json"
      ? ({
          // callback functions

          // check to see if engines exists at the outermost level
          "ExpressionStatement > ObjectExpression": verifiers.existsInFile,

          // check that node is a member of engines
          "ExpressionStatement > ObjectExpression > Property[key.value='engines']":
            verifiers.isMemberOf,

          // check the node corresponding to engines.node to see if it is set to '>=8.0.0'
          "ExpressionStatement > ObjectExpression > Property[key.value='engines'] > ObjectExpression > Property[key.value='node']":
            verifiers.innerMatchesExpected
        } as Rule.RuleListener)
      : {};
  }
};

/**
 * @fileoverview Rule to recommend tsconfig.json's compilerOptions.isolatedModules value to be true.
 * @author Arpan Laha
 */

import { getVerifiers, stripPath } from "../utils";
import { Rule } from "eslint";
import { getRuleMetaData } from "../utils";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: getRuleMetaData(
    "ts-config-isolatedmodules",
    "recommend tsconfig.json's compilerOptions.isolatedModules value to be true"
  ),
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "compilerOptions",
      inner: "isolatedModules",
      expected: true
    });
    return stripPath(context.getFilename()) === "tsconfig.json"
      ? ({
          // callback functions

          // check to see if compilerOptions exists at the outermost level
          "ExpressionStatement > ObjectExpression": verifiers.existsInFile,

          // check that isolatedModules is a member of compilerOptions
          "ExpressionStatement > ObjectExpression > Property[key.value='compilerOptions']":
            verifiers.isMemberOf,

          // check the node corresponding to compilerOptions.isolatedModules to see if it is set to true
          "ExpressionStatement > ObjectExpression > Property[key.value='compilerOptions'] > ObjectExpression > Property[key.value='isolatedModules']":
            verifiers.innerMatchesExpected
        } as Rule.RuleListener)
      : {};
  }
};

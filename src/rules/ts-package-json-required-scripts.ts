/**
 * @fileoverview Rule to force package.json's scripts value to at least contain build and test.
 * @author Arpan Laha
 */

import getVerifiers from "../utils/verifiers";
import { Rule } from "eslint";
import { Property } from "estree";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description:
        "force package.json's scripts value to at least contain build, test, and prepack",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-required-scripts"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    var buildVerifiers = getVerifiers(context, {
      outer: "scripts",
      inner: "build",
      fileName: "package.json"
    });
    var testVerifiers = getVerifiers(context, {
      outer: "scripts",
      inner: "test",
      fileName: "package.json"
    });
    return {
      // callback functions

      // check to see if scripts exists at the outermost level
      "VariableDeclarator > ObjectExpression": buildVerifiers.existsInFile,

      // check to see if scripts contains both build and test
      "Property[key.value='scripts']": (node: Property): void => {
        buildVerifiers.isMemberOf(node);
        testVerifiers.isMemberOf(node);
      }
    } as Rule.RuleListener;
  }
};

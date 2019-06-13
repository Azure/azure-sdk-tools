/**
 * @fileoverview Rule to force tsconfig.json's compilerOptions.sourceMap and compilerOptions.declarationMap values to both be true.
 * @author Arpan Laha
 */

import structure from "../utils/structure";
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
        "force tsconfig.json's compilerOptions.sourceMap and compilerOptions.declarationMap values to both be true",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-sourcemap"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    var sourceMapCheckers = structure(context, {
      outer: "compilerOptions",
      inner: "sourceMap",
      expected: true,
      fileName: "tsconfig.json"
    });
    var declarationMapCheckers = structure(context, {
      outer: "compilerOptions",
      inner: "declarationMap",
      expected: true,
      fileName: "tsconfig.json"
    });

    return {
      // callback functions

      // check to see if compilerOptions exists at the outermost level
      "VariableDeclarator > ObjectExpression": sourceMapCheckers.existsInFile,

      // check that sourceMap and declarationMap are both members of compilerOptions
      "Property[key.value='compilerOptions']": (node: Property): void => {
        sourceMapCheckers.isMemberOf(node);
        declarationMapCheckers.isMemberOf(node);
      },

      // check the node corresponding to compilerOptions.sourceMap to see if it is set to true
      "VariableDeclarator > ObjectExpression > Property[key.value='compilerOptions'] > ObjectExpression > Property[key.value='sourceMap']":
        sourceMapCheckers.innerMatchesExpected,

      // check the node corresponding to compilerOptions.declarationMap to see if it is set to true
      "VariableDeclarator > ObjectExpression > Property[key.value='compilerOptions'] > ObjectExpression > Property[key.value='declarationMap']":
        declarationMapCheckers.innerMatchesExpected
    } as Rule.RuleListener;
  }
};

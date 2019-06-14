/**
 * @fileoverview Rule to force tsconfig.json's compilerOptions.lib value to not be used.
 * @author Arpan Laha
 */

import getVerifiers from "../utils/verifiers";
import { Rule } from "eslint";
import { Literal, ObjectExpression, Property } from "estree";

//------------------------------------------------------------------------------
// Rule Definition
//------------------------------------------------------------------------------

export = {
  meta: {
    type: "problem",

    docs: {
      description:
        "force tsconfig.json's compilerOptions.lib value to not be used",
      category: "Best Practices",
      recommended: true,
      url:
        "https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-lib"
    },
    schema: [] // no options
  },
  create: (context: Rule.RuleContext): Rule.RuleListener => {
    const verifiers = getVerifiers(context, {
      outer: "compilerOptions",
      fileName: "tsconfig.json"
    });
    return {
      // callback functions

      // check to see if compilerOptions exists at the outermost level
      "ExpressionStatement > ObjectExpression": verifiers.existsInFile,

      // check that lib is not a member of compilerOptions
      "Property[key.value='compilerOptions']": (node: Property): void => {
        if (
          context.getFilename().replace(/^.*[\\\/]/, "") === "tsconfig.json"
        ) {
          const value: ObjectExpression = node.value as ObjectExpression;
          const properties: Property[] = value.properties as Property[];
          let foundInner = false;
          for (const property of properties) {
            if (property.key) {
              const key = property.key as Literal;
              if (key.value === "lib") {
                foundInner = true;
                break;
              }
            }
          }

          foundInner &&
            context.report({
              node: node,
              message: "compilerOptions.lib should not be used"
            } as any);
        }
      }
    } as Rule.RuleListener;
  }
};

/**
 * @fileoverview Helper methods for rules pertaining to object structure
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { TSESTree } from "@typescript-eslint/experimental-utils";
import { Property } from "@typescript-eslint/typescript-estree/dist/ts-estree/ts-estree";

interface StructureData {
  outer: string;
  inner: string;
  expectedValue: any;
  fileName: string;
}

export const structure = function(
  context: Rule.RuleContext,
  data: StructureData
) {
  return {
    // check to see if if the outer key exists at the outermost level
    existsInFile: function(node: TSESTree.ObjectExpression) {
      const outer = data.outer;
      const fileName = data.fileName;

      const properties : Property[] = node.properties;
      let foundOuter = false;

      properties.forEach( function(value :Property, index: number) : void {
        if (element.key && element.key
      })

      for (const (property of properties) {
        if (property.key && property.key.value === outer) {
          foundOuter = true;
          break;
        }
      }
      context.getFilename() === fileName
        ? foundOuter
          ? []
          : context.report({
              node: node,
              message:
                fileName +
                ": " +
                outer +
                " does not exist at the outermost level"
            })
        : [];
    },

    // check that the inner key is a member of the outer key
    isMemberOf: function(node: TSESTree.Property) {
      const outer = data.outer;
      const inner = data.inner;
      const fileName = data.fileName;

      const properties = node.value.properties;
      let foundInner = false;
      for (const property of properties) {
        if (property.key && property.key.value === inner) {
          foundInner = true;
          break;
        }
      }
      context.getFilename() === fileName
        ? foundInner
          ? []
          : context.report({
              node: node,
              message: fileName + ": " + inner + " is not a member of " + outer
            })
        : [];
    },

    // check the node corresponding to the inner value to see if it is set to true
    innerMatchesExpected: function(node: TSESTree.Property) {
      const outer = data.outer;
      const inner = data.inner;
      const expectedValue = data.expectedValue;
      const fileName = data.fileName;

      context.getFilename() === fileName
        ? node.value.value === expectedValue
          ? []
          : context.report({
              node: node,
              message:
                fileName +
                ": " +
                outer +
                "." +
                inner +
                " is set to {{identifier }} when it should be set to " +
                expectedValue,
              data: {
                identifier: node.value.value
              }
            })
        : [];
    }
  };
};

/**
 * @fileoverview Helper methods for rules pertaining to object structure
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { Property, ObjectExpression, Literal } from "estree";

interface StructureData {
  outer: string;
  inner?: string;
  expectedValue: any;
  fileName?: string;
}

const stripPath = function(pathOrFileName: string): string {
  return pathOrFileName.replace(/^.*[\\\/]/, "");
};

export = function(context: Rule.RuleContext, data: StructureData) {
  return {
    // check to see if if the outer key exists at the outermost level
    existsInFile: function(node: ObjectExpression) {
      const outer = data.outer;
      const fileName = data.fileName;

      const properties: Property[] = node.properties as Property[];
      let foundOuter = false;

      for (const property of properties) {
        if (property.key) {
          let key = property.key as Literal;
          if (key.value === outer) {
            foundOuter = true;
            break;
          }
        }
      }

      stripPath(context.getFilename()) === fileName
        ? foundOuter
          ? []
          : context.report({
              node: node,
              message:
                fileName +
                ": " +
                outer +
                " does not exist at the outermost level"
            } as any)
        : [];
    },

    // check to see if the value of the outer key matches the expected value
    outerMatchesExpected: function(node: Property) {
      const outer = data.outer;
      const expectedValue = data.expectedValue;
      const fileName = data.fileName;

      const nodeValue: Literal = node.value as Literal;

      stripPath(context.getFilename()) === fileName
        ? nodeValue.value === expectedValue
          ? []
          : context.report({
              node: node,
              message:
                fileName +
                ": " +
                outer +
                " is set to {{ identifier }} when it should be set to " +
                expectedValue,
              data: {
                identifier: nodeValue.value as string
              }
            } as any)
        : [];
    },

    // check that the inner key is a member of the outer key
    isMemberOf: function(node: Property) {
      const outer = data.outer;
      const inner = data.inner;
      const fileName = data.fileName;

      const value: ObjectExpression = node.value as ObjectExpression;
      const properties: Property[] = value.properties as Property[];
      let foundInner = false;
      for (const property of properties) {
        if (property.key) {
          let key = property.key as Literal;
          if (key.value === inner) {
            foundInner = true;
            break;
          }
        }
      }
      stripPath(context.getFilename()) === fileName
        ? foundInner
          ? []
          : context.report({
              node: node,
              message: fileName + ": " + inner + " is not a member of " + outer
            } as any)
        : [];
    },

    // check the node corresponding to the inner value to see if it is set to true
    innerMatchesExpected: function(node: Property) {
      const outer = data.outer;
      const inner = data.inner;
      const expectedValue = data.expectedValue;
      const fileName = data.fileName;

      let nodeValue: Literal = node.value as Literal;

      stripPath(context.getFilename()) === fileName
        ? nodeValue.value === expectedValue
          ? []
          : context.report({
              node: node,
              message:
                fileName +
                ": " +
                outer +
                "." +
                inner +
                " is set to {{ identifier }} when it should be set to " +
                expectedValue,
              data: {
                identifier: nodeValue.value as string
              }
            } as any)
        : [];
    }
  };
};

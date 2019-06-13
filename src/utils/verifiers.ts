/**
 * @fileoverview Helper methods for rules pertaining to object structure
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { Property, ObjectExpression, Literal, ArrayExpression } from "estree";

interface StructureData {
  outer: string;
  inner?: string;
  expected?: any;
  fileName?: string;
}

const stripPath = (pathOrFileName: string): string => {
  return pathOrFileName.replace(/^.*[\\\/]/, "");
};

export = (context: Rule.RuleContext, data: StructureData): any => {
  return {
    // check to see if if the outer key exists at the outermost level
    existsInFile: (node: ObjectExpression): void => {
      const fileName = data.fileName;
      if (stripPath(context.getFilename()) === fileName) {
        const outer = data.outer;

        const properties: Property[] = node.properties as Property[];
        let foundOuter = false;

        for (const property of properties) {
          if (property.key) {
            const key = property.key as Literal;
            if (key.value === outer) {
              foundOuter = true;
              break;
            }
          }
        }

        !foundOuter &&
          context.report({
            node: node,
            message: outer + " does not exist at the outermost level"
          } as any);
      }
    },

    // check to see if the value of the outer key matches the expected value
    outerMatchesExpected: (node: Property): void => {
      const fileName = data.fileName;
      if (stripPath(context.getFilename()) === fileName) {
        const outer = data.outer;
        const expected = data.expected;

        const nodeValue: Literal = node.value as Literal;

        nodeValue.value !== expected &&
          context.report({
            node: node,
            message:
              outer +
              " is set to {{ identifier }} when it should be set to " +
              expected,
            data: {
              identifier: nodeValue.value as string
            }
          } as any);
      }
    },

    // check that the inner key is a member of the outer key
    isMemberOf: (node: Property): void => {
      const fileName = data.fileName;
      if (stripPath(context.getFilename()) === fileName) {
        const outer = data.outer;
        const inner = data.inner;

        const value: ObjectExpression = node.value as ObjectExpression;
        const properties: Property[] = value.properties as Property[];
        let foundInner = false;
        for (const property of properties) {
          if (property.key) {
            const key = property.key as Literal;
            if (key.value === inner) {
              foundInner = true;
              break;
            }
          }
        }

        !foundInner &&
          context.report({
            node: node,
            message: inner + " is not a member of " + outer
          } as any);
      }
    },

    // check the node corresponding to the inner value to see if it is set to the expected value
    innerMatchesExpected: (node: Property): void => {
      const fileName = data.fileName;
      if (stripPath(context.getFilename()) === fileName) {
        const outer = data.outer;
        const inner = data.inner;
        const expected = data.expected;

        const nodeValue: Literal = node.value as Literal;

        nodeValue.value !== expected &&
          context.report({
            node: node,
            message:
              outer +
              "." +
              inner +
              " is set to {{ identifier }} when it should be set to " +
              expected,
            data: {
              identifier: nodeValue.value as string
            }
          } as any);
      }
    },

    // check the node corresponding to the inner value to see if it contains the expected value
    outerContainsExpected: (node: Property): void => {
      const outer = data.outer;
      const expected = data.expected;
      const fileName = data.fileName;

      const nodeValue: ArrayExpression = node.value as ArrayExpression;
      const candidateArray: Literal[] = nodeValue.elements as Literal[];

      if (stripPath(context.getFilename()) === fileName) {
        if (expected instanceof Array) {
          for (const value of expected) {
            let foundValue: boolean = false;
            for (const candidate of candidateArray) {
              if (candidate.value === value) {
                foundValue = true;
                break;
              }
            }

            !foundValue &&
              context.report({
                node: node,
                message: outer + " does not contain " + value
              } as any);
          }
        } else {
          let foundValue: boolean = false;
          for (const candidate of candidateArray) {
            if (candidate.value === expected) {
              foundValue = true;
              break;
            }
          }

          !foundValue &&
            context.report({
              node: node,
              message: outer + " does not contain " + expected
            } as any);
        }
      }
    }
  };
};

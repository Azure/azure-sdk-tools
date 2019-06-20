/**
 * @fileoverview Helper methods for rules pertaining to object structure
 * @author Arpan Laha
 */

import { Rule } from "eslint";
import { Property, ObjectExpression, Literal, ArrayExpression } from "estree";

interface StructureData {
  outer: string;
  inner?: string;
  expected?: any; //eslint-disable-line @typescript-eslint/no-explicit-any
}

export const stripPath = (pathOrFileName: string): string => {
  return pathOrFileName.replace(/^.*[\\\/]/, "");
};

/*eslint-disable @typescript-eslint/no-explicit-any*/
export const getVerifiers = (
  context: Rule.RuleContext,
  data: StructureData
): any => {
  /* eslint-enable @typescript-eslint/no-explicit-any*/
  return {
    // check to see if if the outer key exists at the outermost level
    existsInFile: (node: ObjectExpression): void => {
      const outer = data.outer;

      const properties: Property[] = node.properties as Property[];

      !properties.find(property => {
        const key = property.key as Literal;
        return key.value === outer;
      }) &&
        context.report({
          node: node,
          message: outer + " does not exist at the outermost level"
        });
    },

    // check to see if the value of the outer key matches the expected value
    outerMatchesExpected: (node: Property): void => {
      const outer = data.outer;
      const expected = data.expected;

      const nodeValue: Literal = node.value as Literal;

      nodeValue.value !== expected &&
        context.report({
          node: nodeValue,
          message:
            outer +
            " is set to {{ identifier }} when it should be set to " +
            expected,
          data: {
            identifier: nodeValue.value as string
          }
        });
    },

    // check that the inner key is a member of the outer key
    isMemberOf: (node: Property): void => {
      const outer = data.outer;
      const inner = data.inner;

      const value: ObjectExpression = node.value as ObjectExpression;
      const properties: Property[] = value.properties as Property[];

      !properties.find(property => {
        const key = property.key as Literal;
        return key.value === inner;
      }) &&
        context.report({
          node: value,
          message: inner + " is not a member of " + outer
        });
    },

    // check the node corresponding to the inner value to see if it is set to the expected value
    innerMatchesExpected: (node: Property): void => {
      const outer = data.outer;
      const inner = data.inner;
      const expected = data.expected;

      const nodeValue: Literal = node.value as Literal;

      nodeValue.value !== expected &&
        context.report({
          node: nodeValue,
          message:
            outer +
            "." +
            inner +
            " is set to {{ identifier }} when it should be set to " +
            expected,
          data: {
            identifier: nodeValue.value as string
          }
        });
    },

    // check the node corresponding to the inner value to see if it contains the expected value
    outerContainsExpected: (node: Property): void => {
      const outer = data.outer;
      const expected = data.expected;

      const nodeValue: ArrayExpression = node.value as ArrayExpression;
      const candidateArray: Literal[] = nodeValue.elements as Literal[];

      if (expected instanceof Array) {
        expected.forEach(value => {
          !candidateArray.find(candidate => {
            return candidate.value === value;
          }) &&
            context.report({
              node: nodeValue,
              message: outer + " does not contain " + value
            });
        });
      } else {
        !candidateArray.find(candidate => {
          return candidate.value === expected;
        }) &&
          context.report({
            node: nodeValue,
            message: outer + " does not contain " + expected
          });
      }
    }
  };
};

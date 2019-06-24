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

/* eslint-disable @typescript-eslint/no-explicit-any */
/**
 * Returns structural verifiers given input
 * @param context provided ESLint context object
 * @param data matches StructureData interface, contains outer and optional inner and expected values
 * @return existsInFile, outerMatchesExpected, isMemberOf, innerMatchesExpected, and outerContainsExpected verifiers
 */
export const getVerifiers = (
  context: Rule.RuleContext,
  data: StructureData
): any => {
  /* eslint-enable @typescript-eslint/no-explicit-any*/
  return {
    /**
     * check to see if if the outer key exists at the outermost level
     * @param node the ObjectExpression node we check to see if it contains data.outer as a key
     * @throws an ESLint report if node does not contain data.outer as a key
     */
    existsInFile: (node: ObjectExpression): void => {
      const outer = data.outer;

      const properties: Property[] = node.properties as Property[];

      !properties.find((property: Property): boolean => {
        const key = property.key as Literal;
        return key.value === outer;
      }) &&
        context.report({
          node: node,
          message: outer + " does not exist at the outermost level"
        });
    },

    /**
     * check to see if the value of the outer key matches the expected value
     * @param node the Property node we want to check
     * @throws an ESlint report if node.value is not a literal or is not the expected value
     */
    outerMatchesExpected: (node: Property): void => {
      const outer = data.outer;
      const expected = data.expected;

      // check to see that node value is a Literal before casting
      node.value.type !== "Literal" &&
        context.report({
          node: node.value,
          message:
            outer +
            " is not set to a literal (string | boolean | null | number | RegExp)"
        });

      const nodeValue: Literal = node.value as Literal;

      // check node value against expected value
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
        });
    },

    /**
     * check that the inner key is a member of the outer key
     * @param node the Property node corresponding to the outer key
     * @throws an ESLint report if the inner key is not a member of the outer key's value
     */
    isMemberOf: (node: Property): void => {
      const outer = data.outer;
      const inner = data.inner;

      const value: ObjectExpression = node.value as ObjectExpression;
      const properties: Property[] = value.properties as Property[];

      !properties.find((property: Property): boolean => {
        const key = property.key as Literal;
        return key.value === inner;
      }) &&
        context.report({
          node: node,
          message: inner + " is not a member of " + outer
        });
    },

    /**
     * check the node corresponding to the inner value to see if it is set to the expected value
     * @param node the Property node corresponding to the inner key
     * @throws an ESLint report if the inner value is not a literal or does not match the expected value
     */
    innerMatchesExpected: (node: Property): void => {
      const outer = data.outer;
      const inner = data.inner;
      const expected = data.expected;

      // check to see that node value is a Literal before casting
      node.value.type !== "Literal" &&
        context.report({
          node: node.value,
          message:
            outer +
            "." +
            inner +
            " is not set to a literal (string | boolean | null | number | RegExp)"
        });

      const nodeValue: Literal = node.value as Literal;

      // check node value against expected value
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
        });
    },

    /**
     * check the node corresponding to the inner value to see if it contains the expected value
     * @param node the Property node corresponding to the outer key
     * @throws an ESLint repot of the node's value is not an array of literals or does not contain the expectec value(s)
     */
    outerContainsExpected: (node: Property): void => {
      const outer = data.outer;
      const expected = data.expected;

      node.value.type !== "ArrayExpression" &&
        context.report({
          node: node.value,
          message: outer + " is not set to an array"
        });

      const nodeValue: ArrayExpression = node.value as ArrayExpression;

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const nonLiteral = nodeValue.elements.find((element: any): boolean => {
        return element.type !== "Literal";
      });

      nonLiteral &&
        context.report({
          node: nonLiteral,
          message:
            outer +
            " contains non-literal (string | boolean | null | number | RegExp) elements"
        });

      const candidateArray: Literal[] = nodeValue.elements as Literal[];

      if (expected instanceof Array) {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        expected.forEach((value: any): void => {
          !candidateArray.find((candidate: Literal): boolean => {
            return candidate.value === value;
          }) &&
            context.report({
              node: node,
              message: outer + " does not contain " + value
            });
        });
      } else {
        !candidateArray.find((candidate: Literal): boolean => {
          return candidate.value === expected;
        }) &&
          context.report({
            node: node,
            message: outer + " does not contain " + expected
          });
      }
    }
  };
};

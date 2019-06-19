/**
 * @fileoverview Testing the ts-modules-no-default rule.
 * @author Arpan Laha
 */

import rule from "../../src/rules/ts-modules-no-default";
import { RuleTester } from "eslint";

//------------------------------------------------------------------------------
// Tests
//------------------------------------------------------------------------------

const ruleTester = new RuleTester({
  parser: "@typescript-eslint/parser",
  parserOptions: {
    project: "./tsconfig.json"
  }
});

ruleTester.run("ts-modules-no-default", rule, {
  valid: [
    // different non-default exports
    {
      code: 'export = {test: "test"}'
    },
    {
      code: 'const foo = {test: "test"}; export {foo}'
    },
    {
      code: 'export const foo = {test: "test"}'
    }
  ],
  invalid: [
    {
      code: 'export default {test: "test"}',
      errors: [
        {
          message: "default exports exist at top level"
        }
      ]
    },
    {
      code: 'const foo = {test: "test"}; export default foo',
      errors: [
        {
          message: "default exports exist at top level"
        }
      ]
    }
  ]
});

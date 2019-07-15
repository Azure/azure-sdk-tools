/**
 * @fileoverview Testing the ts-doc-internal rule.
 * @author Arpan Laha
 */

import rule from "../../src/rules/ts-doc-internal";
import { RuleTester } from "eslint";

//------------------------------------------------------------------------------
// Tests
//------------------------------------------------------------------------------

const ruleTester = new RuleTester({
  parser: "@typescript-eslint/parser",
  parserOptions: {
    project: "./tsconfig.json"
  },
  settings: {
    exported: []
  }
});

ruleTester.run("ts-doc-internal", rule, {
  valid: [
    // class
    {
      code: `
            /**
             * Other documentation
             * @internal
             */
            class ExampleClass {}`
    },
    {
      code: `
            /**
             * Other documentation
             * @ignore
             */
            class ExampleClass {}`
    },
    // interface
    {
      code: `
            /**
             * Other documentation
             * @internal
             */
            interface ExampleInterface {}`
    },
    {
      code: `
            /**
             * Other documentation
             * @ignore
             */
            interface ExampleInterface {}`
    },
    // function
    {
      code: `
            /**
             * Other documentation
             * @internal
             */
            function ExampleFunction() {}`
    },
    {
      code: `
            /**
             * Other documentation
             * @ignore
             */
            function ExampleFunction() {}`
    }
  ],
  invalid: [
    // class
    {
      code: `
            /**
             * Other documentation
             */
            class ExampleClass {}`,
      errors: [
        {
          message:
            "internal items with TSDoc comments should include an @internal or @ignore tag"
        }
      ]
    },
    // interface
    {
      code: `
            /**
             * Other documentation
             */
            interface ExampleInterface {}`,
      errors: [
        {
          message:
            "internal items with TSDoc comments should include an @internal or @ignore tag"
        }
      ]
    },
    // function
    {
      code: `
            /**
             * Other documentation
             */
            function ExampleFunction() {}`,
      errors: [
        {
          message:
            "internal items with TSDoc comments should include an @internal or @ignore tag"
        }
      ]
    }
  ]
});

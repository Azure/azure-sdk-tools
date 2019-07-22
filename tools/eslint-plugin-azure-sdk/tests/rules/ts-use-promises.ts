/**
 * @fileoverview Testing the ts-use-promises rule.
 * @author Arpan Laha
 */

import rule from "../../src/rules/ts-use-promises";
import { RuleTester } from "eslint";

//------------------------------------------------------------------------------
// Example files
//------------------------------------------------------------------------------

const example = `
const promise = (): Promise<string> => {
    return new Promise(resolve => resolve("example"));
}
`;

//------------------------------------------------------------------------------
// Tests
//------------------------------------------------------------------------------

const ruleTester = new RuleTester({
  parser: "@typescript-eslint/parser",
  parserOptions: {
    project: "./tsconfig.json",
    ecmaVersion: 6,
    sourceType: "module",
    ecmaFeatures: {
      modules: true
    }
  }
});

ruleTester.run("ts-use-promises", rule, {
  valid: [
    {
      code: example
    }
  ],
  invalid: [
    {
      code: `import Promise from 'bluebird';${example}`,
      errors: [
        {
          message:
            "promises should use the in-built Promise type, not libraries or polyfills"
        }
      ]
    }
  ]
});

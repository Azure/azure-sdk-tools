/**
 * @fileoverview Testing the ts-error-handling rule.
 * @author Arpan Laha
 */

import rule from "../../src/rules/ts-error-handling";
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

ruleTester.run("ts-error-handling", rule, {
  valid: [
    // different non-default exports
    {
      code: 'throw new TypeError("test")'
    },
    {
      code: 'throw new RangeError("test")'
    },
    {
      code: 'throw new Error("test")'
    },
    {
      code: 'const err = new Error("test"); throw err'
    }
  ],
  invalid: [
    // string-value exception
    {
      code: 'throw "test"',
      errors: [
        {
          message: "statement is throwing a literal"
        }
      ]
    },
    // integer-value exception
    {
      code: "throw 1",
      errors: [
        {
          message: "statement is throwing a literal"
        }
      ]
    },
    // user-defined exception
    {
      code:
        "function UserException(message) { this.message = message; this.name = 'UserException';}; throw new UserException('test')",
      errors: [
        {
          message:
            "error thrown is not one of the following types: TypeError, RangeError, Error"
        }
      ]
    }
  ]
});

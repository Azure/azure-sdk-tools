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
    // different valid errors
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
      code: 'const err = new TypeError("test"); throw err'
    },
    {
      code: 'const err = new RangeError("test"); throw err'
    },
    {
      code: 'const err = new Error("test"); throw err'
    },
    {
      code: 'try { console.log("test"); } catch(err) { throw err; }'
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
        'function UserException(message) { this.message = message; this.name = "UserException";}; throw new UserException("test")',
      errors: [
        {
          message:
            "error thrown is not one of the following types: TypeError, RangeError, Error"
        }
      ]
    },
    {
      code:
        'class TestError extends Error { constructor(m: string) { super(m); }; } const err = new TestError("test"); throw err',
      //'interface UserExceptionType { message: string, name: string }; function UserException(message: string): UserExceptionType { this.message = message; this.name = "UserException";}; const err = new UserException("test"); throw err',
      errors: [
        {
          message:
            "error thrown is not one of the following types: TypeError, RangeError, Error"
        }
      ]
    }
  ]
});

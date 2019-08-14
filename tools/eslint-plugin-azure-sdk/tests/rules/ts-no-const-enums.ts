/**
 * @file Testing the ts-no-const-enums rule.
 * @author Arpan Laha
 */

import rule from "../../src/rules/ts-no-const-enums";
import { RuleTester } from "eslint";

//------------------------------------------------------------------------------
// Tests
//------------------------------------------------------------------------------

const ruleTester = new RuleTester({
  parser: "@typescript-eslint/parser",
  parserOptions: {
    project: "./tsconfig.json",
    sourceType: "module"
  },
  settings: {
    main: "test.ts"
  }
});

ruleTester.run("ts-no-const-enums", rule, {
  valid: [
    {
      code: "enum Enum { a = 1 }",
      filename: "src/test.ts"
    }
  ],
  invalid: [
    {
      code: "const enum Enum { a = 1 }",
      filename: "src/test.ts",
      errors: [
        {
          message: "const enums should not be used"
        }
      ]
    }
  ]
});

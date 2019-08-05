/**
 * @fileoverview Testing the ts-apisurface-supportcancellation rule.
 * @author Arpan Laha
 */

import rule from "../../src/rules/ts-apisurface-supportcancellation";
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

ruleTester.run("ts-apifurface-supportcancellation", rule, {
  valid: [
    // parameter
    {
      code:
        "class ExampleClient { async createItem(cancelToken: AbortSignalLike): void {}; };"
    },
    //option
    {
      code:
        "interface ExampleOptions { cancelToken: AbortSignalLike }; class ExampleClient { async createItem(options: ExampleOptions): void {}; };"
    },
    // sync
    {
      code: "class ExampleClient { createItem(): void {}; };"
    },
    // private
    {
      code: "class ExampleClient { private async makeItem(): void {}; };"
    },
    // not client
    {
      code: "class Example { async makeItem(): void {}; };"
    }
  ],
  invalid: [
    {
      code: "class ExampleClient { async createItem(): void {}; };",
      errors: [
        {
          message:
            "async method createItem should accept an AbortSignalLike parameter or option"
        }
      ]
    }
  ]
});

/**
 * @fileoverview Testing the ts-apisurface-standardized-verbs rule.
 * @author Arpan Laha
 */

import rule from "../../src/rules/ts-apisurface-standardized-verbs";
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

ruleTester.run("ts-apifurface-standardized-verbs", rule, {
  valid: [
    // single method
    {
      code: "class ExampleClient { createItem(): void {}; };"
    },
    // mutliple methods
    {
      code: `
        class ExampleClient {
          createItem(): void {};
          upsertItem(): void {};
          setItem(): void {};
          updateItem(): void {};
          replaceItem(): void {};
          appendItem(): void {};
          addItem(): void {};
          getItem(): void {};
          listItems(): void {};
          itemExists(): void {};
          deleteItem(): void {};
          removeItem(): void {};
      };`
    },
    // private
    {
      code: "class ExampleClient { private moveItem(): void {}; };"
    },
    // not client
    {
      code: "class Example { moveItem(): void {}; };"
    }
  ],
  invalid: [
    // single error
    {
      code: "class ExampleClient { moveItem(): void {}; };",
      errors: [
        {
          message:
            "method name moveItem does not include one of the approved verb prefixes or suffixes"
        }
      ]
    },
    // mutliple errors
    {
      code:
        "class ExampleClient { moveItem(): void {}; makeItem(): void {}; };",
      errors: [
        {
          message:
            "method name moveItem does not include one of the approved verb prefixes or suffixes"
        },
        {
          message:
            "method name makeItem does not include one of the approved verb prefixes or suffixes"
        }
      ]
    }
  ]
});

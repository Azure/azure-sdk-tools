/**
 * @fileoverview Testing the ts-pagination-list-bypage rule.
 * @author Arpan Laha
 */

import rule from "../../src/rules/ts-pagination-list-bypage";
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

ruleTester.run("ts-pagination-list-bypage", rule, {
  valid: [
    // simple valid example
    {
      code:
        "class ExampleClient { listItems(): PagedAsyncIterableIterator<Item> { return { byPage(continuationToken, maxPageSize) { console.log('test'); } }; }; };"
    },
    // not a client
    {
      code:
        "class Example { listItems(): PagedAsyncIterableIterator<Item> { return { byPage() { console.log('test'); } }; }; };"
    },
    // not in list method
    {
      code:
        "class ExampleClient { getItems(): PagedAsyncIterableIterator<Item> { return { byPage() { console.log('test'); } }; }; };"
    }
  ],
  invalid: [
    // no byPage property
    {
      code:
        "class ExampleClient { listItems(): PagedAsyncIterableIterator<Item> { return { }; }; };",
      errors: [
        {
          message: "returned object does not contain a byPage function"
        }
      ]
    },
    // continuationToken missing
    {
      code:
        "class ExampleClient { listItems(): PagedAsyncIterableIterator<Item> { return { byPage(maxPageSize) { console.log('test'); } }; }; };",
      errors: [
        {
          message: "byPage does not contain an option for continuationToken"
        }
      ]
    },
    // both missing
    {
      code:
        "class ExampleClient { listItems(): PagedAsyncIterableIterator<Item> { return { byPage() { console.log('test'); } }; }; };",
      errors: [
        {
          message: "byPage does not contain an option for continuationToken"
        },
        {
          message: "byPage does not contain an option for maxPageSize"
        }
      ]
    }
  ]
});

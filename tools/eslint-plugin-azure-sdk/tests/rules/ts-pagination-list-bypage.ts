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
    // function expressions
    {
      code:
        "class ExampleClient { listItems(): PagedAsyncIterableIterator<Item> { return { byPage({continuationToken, maxPageSize}) { console.log('test'); } }; }; };"
    },
    // arrow function expressions
    {
      code:
        "class ExampleClient { listItems(): PagedAsyncIterableIterator<Item> { return { byPage: ({continuationToken, maxPageSize}): void => { console.log('test'); } }; }; };"
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
    // function expression
    {
      // continuationToken missing
      code:
        "class ExampleClient { listItems(): PagedAsyncIterableIterator<Item> { return { byPage({maxPageSize}) { console.log('test'); } }; }; };",
      errors: [
        {
          message: "byPage does not contain an option for continuationToken"
        }
      ]
    },
    {
      // maxPageSize missing
      code:
        "class ExampleClient { listItems(): PagedAsyncIterableIterator<Item> { return { byPage({continuationToken}) { console.log('test'); } }; }; };",
      errors: [
        {
          message: "byPage does not contain an option for maxPageSize"
        }
      ]
    },
    {
      // both missing
      code:
        "class ExampleClient { listItems(): PagedAsyncIterableIterator<Item> { return { byPage({}) { console.log('test'); } }; }; };",
      errors: [
        {
          message: "byPage does not contain an option for continuationToken"
        },
        {
          message: "byPage does not contain an option for maxPageSize"
        }
      ]
    },
    {
      // both as regular parameters, not in options
      code:
        "class ExampleClient { listItems(): PagedAsyncIterableIterator<Item> { return { byPage(continuationToken, maxPageSize) { console.log('test'); } }; }; };",
      errors: [
        {
          message: "byPage does not contain an option for continuationToken"
        },
        {
          message: "byPage does not contain an option for maxPageSize"
        }
      ]
    },
    // arrow function expressions
    {
      // continuationToken missing
      code:
        "class ExampleClient { listItems(): PagedAsyncIterableIterator<Item> { return { byPage: ({maxPageSize}): void => { console.log('test'); } }; }; };",
      errors: [
        {
          message: "byPage does not contain an option for continuationToken"
        }
      ]
    },
    {
      // maxPageSize missing
      code:
        "class ExampleClient { listItems(): PagedAsyncIterableIterator<Item> { return { byPage: ({continuationToken}): void => { console.log('test'); } }; }; };",
      errors: [
        {
          message: "byPage does not contain an option for maxPageSize"
        }
      ]
    },
    {
      // both missing
      code:
        "class ExampleClient { listItems(): PagedAsyncIterableIterator<Item> { return { byPage: ({}): void => { console.log('test'); } }; }; };",
      errors: [
        {
          message: "byPage does not contain an option for continuationToken"
        },
        {
          message: "byPage does not contain an option for maxPageSize"
        }
      ]
    },
    {
      // both as regular parameters, not in options
      code:
        "class ExampleClient { listItems(): PagedAsyncIterableIterator<Item> { return { byPage: (continuationToken, maxPageSize): void => { console.log('test'); } }; }; };",
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

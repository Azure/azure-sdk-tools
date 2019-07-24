/**
 * @fileoverview Testing the github-source-headers rule.
 * @author Arpan Laha
 */

import rule from "../../src/rules/github-source-headers";
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

const valid = `
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
console.log("hello")`;

const invalid1 = `
// Copyright (c) Microsoft.
// Licensed under the MIT license.
console.log("hello")`;

const invalid2 = `
// Copyright (c) Microsoft Corporation.
// Licensed under the Apache 2.0 license.
console.log("hello")`;

const configError = `copyright header not properly configured - expected value:
Copyright (c) Microsoft Corporation.
Licensed under the MIT license.
`;

ruleTester.run("github-source-headers", rule, {
  valid: [
    {
      // only the fields we care about
      code: valid,
      filename: "test.ts"
    },
    {
      // incorrect format but in a file we don't care about
      code: 'console.log("hello")',
      filename: "test.js"
    }
  ],
  invalid: [
    {
      // no comments
      code: 'console.log("hello")',
      filename: "test.ts",
      errors: [
        {
          message: "no copyright header found"
        }
      ]
    },
    // wrong headers
    {
      code: invalid1,
      filename: "test.ts",
      errors: [
        {
          message: configError
        }
      ]
    },
    {
      code: invalid2,
      filename: "test.ts",
      errors: [
        {
          message: configError
        }
      ]
    }
  ]
});

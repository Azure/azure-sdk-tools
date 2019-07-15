/**
 * @fileoverview Testing the ts-doc-external rule.
 * @author Arpan Laha
 */

import rule from "../../src/rules/ts-doc-external";
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

// TODO: figure out how to test this rule
ruleTester.run("ts-doc-external", rule, {
  valid: [],
  invalid: []
});

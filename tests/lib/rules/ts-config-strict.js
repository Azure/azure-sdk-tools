/**
 * @fileoverview Testing the ts-config-strict rule.
 * @author Arpan Laha
 */

"use strict";

var rule = require("../../../lib/rules/ts-config-strict");
var RuleTester = require("eslint").RuleTester;
var processJSON = require("../../../lib/index").processors[".json"];

var ruleTester = new RuleTester({
  parser: "@typescript-eslint/parser",
  parserOptions: { globalReturn: true }
});

ruleTester.run("ts-config-strict", rule, {
  valid: [
    {
      code: '{"compilerOptions": { "strict": true }}',
      filename: Object.assign(processJSON, { filename: "tsconfig.json" }) // this is stupid but it works
    }
  ],
  invalid: [
    // {
    //   code: '{"compilerOptions": { "strict": false }}',
    //   filename: "tsconfig.json"
    // }
  ]
});

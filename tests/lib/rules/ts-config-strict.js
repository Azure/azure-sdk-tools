/**
 * @fileoverview Testing the ts-config-strict rule.
 * @author Arpan Laha
 */

"use strict";

var rule = require("../../../lib/rules/ts-config-strict");
var RuleTester = require("eslint").RuleTester;
var preprocess = require("../../../lib/index").processors[".json"].preprocess;

var ruleTester = new RuleTester({ parser: "@typescript-eslint/parser" });

ruleTester.run("ts-config-strict", rule, {
  valid: [
    {
      filename: "tsconfig.json",
      code: preprocess(
        '{"compilerOptions": { "strict": true }}',
        "tsconfig.json"
      )[0]
    }
  ],
  invalid: [
    // {
    //   code: '{"compilerOptions": { "strict": false }}',
    //   filename: "tsconfig.json"
    // }
  ]
});

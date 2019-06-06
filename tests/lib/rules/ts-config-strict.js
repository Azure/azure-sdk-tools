var rule = require("../../../lib/rules/ts-config-strict");
var RuleTester = require("eslint").RuleTester;

var ruleTester = new RuleTester({ parser: "@typescript-eslint/parser" });
ruleTester.run("ts-config-strict", rule, {
  valid: [
    {
      code: 'const json = {"compilerOptions": { "strict": true }}',
      filename: "tsconfig.json"
    }
  ],
  invalid: [
    // {
    //   code: '{"compilerOptions": { "strict": false }}',
    //   filename: "tsconfig.json"
    // }
  ]
});

const json = { compilerOptions: { strict: true } };

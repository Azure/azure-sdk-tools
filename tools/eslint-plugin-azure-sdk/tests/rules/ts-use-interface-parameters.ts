/**
 * @fileoverview Testing the ts-use-interface-parameters.
 * @author Arpan Laha
 */

import rule from "../../src/rules/ts-use-interface-parameters";
import { RuleTester } from "eslint";

//------------------------------------------------------------------------------
// Example class & interface
//------------------------------------------------------------------------------

const example = `class A {
  message: string
}

interface B {
  message: string
}
`;

//------------------------------------------------------------------------------
// Tests
//------------------------------------------------------------------------------

const ruleTester = new RuleTester({
  parser: "@typescript-eslint/parser",
  parserOptions: {
    project: "./tsconfig.json"
  }
});

ruleTester.run("ts-use-interface-parameters", rule, {
  valid: [
    // single parameter
    {
      // arrow function expression
      code: example + "const func = (b: B): void => { console.log(b); }"
    },
    {
      // function expression
      code: example + "const func = function(b: B): void { console.log(b); }"
    },
    {
      // function expression
      code: example + "function func(b: B): void { console.log(b); }"
    },
    // multiple parameters
    {
      // arrow function expression
      code: example + "const func = (b1: B, b2: B): void => { console.log(b); }"
    },
    {
      // function expression
      code:
        example +
        "const func = function(b1: B, b2: B): void { console.log(b); }"
    },
    {
      // function expression
      code: example + "function func(b1: B, b2: B): void { console.log(b); }"
    }
  ],
  invalid: [
    // single parameter
    {
      // arrow function expression
      code: example + "const func = (a: A): void => { console.log(a); }",
      errors: [
        {
          message: "type A of parameter a is a class, not an interface"
        }
      ]
    },
    {
      // function expression
      code: example + "const func = function(a: A): void { console.log(a); }",
      errors: [
        {
          message: "type A of parameter a is a class, not an interface"
        }
      ]
    },
    {
      // function declaration
      code: example + "function func(a: A): void { console.log(a); }",
      errors: [
        {
          message: "type A of parameter a is a class, not an interface"
        }
      ]
    },
    // one interface, one class
    {
      // arrow function expression
      code:
        example + "const func = (a: A, b: B): void => { console.log(a, b); }",
      errors: [
        {
          message: "type A of parameter a is a class, not an interface"
        }
      ]
    },
    {
      // function expression
      code:
        example +
        "const func = function(a: A, b: B): void { console.log(a, b); }",
      errors: [
        {
          message: "type A of parameter a is a class, not an interface"
        }
      ]
    },
    {
      // function declaration
      code: example + "function func(a: A, b: B): void { console.log(a, b); }",
      errors: [
        {
          message: "type A of parameter a is a class, not an interface"
        }
      ]
    },
    // multiple classes
    {
      // arrow function expression
      code:
        example +
        "const func = (a1: A, a2: A): void => { console.log(a1, a2); }",
      errors: [
        {
          message: "type A of parameter a1 is a class, not an interface"
        },
        {
          message: "type A of parameter a2 is a class, not an interface"
        }
      ]
    },
    {
      // function expression
      code:
        example +
        "const func = function(a1: A, a2: A): void { console.log(a1, a2); }",
      errors: [
        {
          message: "type A of parameter a1 is a class, not an interface"
        },
        {
          message: "type A of parameter a2 is a class, not an interface"
        }
      ]
    },
    {
      // function declaration
      code:
        example + "function func(a1: A, a2: A): void { console.log(a1, a2); }",
      errors: [
        {
          message: "type A of parameter a1 is a class, not an interface"
        },
        {
          message: "type A of parameter a2 is a class, not an interface"
        }
      ]
    }
  ]
});

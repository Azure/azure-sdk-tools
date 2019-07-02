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

class A2 {
  message: string
}

interface B {
  message: string
}
`;

// class Foo {
//   constructor() { }
//   method() { }
//   set foo(v) { };
// }

// class OverloadExample {
//   constructor(a: A);
//   constructor(b: B);
//   construcotR(a: A | B) {

//   }
// `;

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
      code: example + "const func1 = (b: B): void => { console.log(b); }"
    },
    {
      // function expression
      code: example + "const func2 = function(b: B): void { console.log(b); }"
    },
    {
      // function declaration
      code: example + "function func3(b: B): void { console.log(b); }"
    },
    {
      // class method
      code: example + "class C { method1(b: B): void { console.log(b); } }"
    },
    // multiple parameters
    {
      // arrow function expression
      code:
        example + "const func4 = (b1: B, b2: B): void => { console.log(b); }"
    },
    {
      // function expression
      code:
        example +
        "const func5 = function(b1: B, b2: B): void { console.log(b); }"
    },
    {
      // function declaration
      code: example + "function func6(b1: B, b2: B): void { console.log(b); }"
    },
    {
      // class method
      code:
        example + "class C { method2(b1: B, b2: B): void { console.log(b); } }"
    },
    // overloads
    {
      // class methods
      code:
        example +
        "class C { overloadMethod(a: A): void { console.log(a); }; overloadMethod(b: B): void { console.log(b); }; }"
    },
    {
      // function declaration
      code:
        example +
        "function overloadDeclaration(a: A): void { console.log(a); }; function overloadDeclaration(b: B): void { console.log(b); }"
    }
  ],
  invalid: [
    // single parameter
    {
      // arrow function expression
      code: example + "const func7 = (a: A): void => { console.log(a); }",
      errors: [
        {
          message: "type A of parameter a is a class, not an interface"
        }
      ]
    },
    {
      // function expression
      code: example + "const func8 = function(a: A): void { console.log(a); }",
      errors: [
        {
          message: "type A of parameter a is a class, not an interface"
        }
      ]
    },
    {
      // function declaration
      code: example + "function func9(a: A): void { console.log(a); }",
      errors: [
        {
          message: "type A of parameter a is a class, not an interface"
        }
      ]
    },
    {
      // class method
      code: example + "class { method3(a: A): void { console.log(a); } }",
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
        example + "const func10 = (a: A, b: B): void => { console.log(a, b); }",
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
        "const func11 = function(a: A, b: B): void { console.log(a, b); }",
      errors: [
        {
          message: "type A of parameter a is a class, not an interface"
        }
      ]
    },
    {
      // function declaration
      code:
        example + "function func12(a: A, b: B): void { console.log(a, b); }",
      errors: [
        {
          message: "type A of parameter a is a class, not an interface"
        }
      ]
    },
    {
      // class method
      code:
        example + "class { method4(a: A, b: B): void { console.log(a, b); } }",
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
        "const func13 = (a1: A, a2: A): void => { console.log(a1, a2); }",
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
        "const func14 = function(a1: A, a2: A): void { console.log(a1, a2); }",
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
        example +
        "function func15(a1: A, a2: A): void { console.log(a1, a2); }",
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
      // class method
      code:
        example +
        "class { method3(a1: A, a2: A): void { console.log(a1, a2); } }",
      errors: [
        {
          message: "type A of parameter a1 is a class, not an interface"
        },
        {
          message: "type A of parameter a2 is a class, not an interface"
        }
      ]
    },
    // bad overloads
    {
      // class methods
      code:
        example +
        "class C { overloadMethodBad(a: A): void { console.log(a); } overloadMethodBad(a1: A, a2: A): void { console.log(a1, a2); }; }",
      errors: [
        {
          message: "type A of parameter a is a class, not an interface"
        },
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
        example +
        "function overloadDeclarationBad(a: A): void { console.log(a); } function overloadDeclarationBad(a1: A, a2: A): void { console.log(a1, a2); }",
      errors: [
        {
          message: "type A of parameter a is a class, not an interface"
        },
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

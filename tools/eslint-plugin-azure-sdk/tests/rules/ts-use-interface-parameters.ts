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

interface B2 {
  message: B
}

interface B3 {
  message: A
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
      // function declaration
      code: example + "function func3(b: B): void { console.log(b); }"
    },
    {
      // class method
      code: example + "class C { method1(b: B): void { console.log(b); } }"
    },
    // multiple parameters
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
    },
    // nested objects
    {
      // class methods
      code:
        example + "class C { nestedMethod(b: B2): void { console.log(b); }; }"
    },
    {
      // function declaration
      code:
        example + "function nestedDeclaration(b: B2): void { console.log(b); }"
    },
    // optional parameters
    {
      // class methods
      code:
        example +
        "class C { nestedMethod(b: B, a?: A): void { console.log(b); a && console.log(a); }; }"
    },
    {
      // function declaration
      code:
        example +
        "function nestedDeclaration(b: B, a?: A): void { console.log(b); a && console.log(a); }"
    }
  ],
  invalid: [
    // single parameter
    {
      // function declaration
      code: example + "function func9(a: A): void { console.log(a); }",
      errors: [
        {
          message:
            "type A of parameter a of function func9 is a class, not an interface"
        }
      ]
    },
    {
      // class method
      code: example + "class { method3(a: A): void { console.log(a); } }",
      errors: [
        {
          message:
            "type A of parameter a of function method3 is a class, not an interface"
        }
      ]
    },
    // one interface, one class
    {
      // function declaration
      code:
        example + "function func12(a: A, b: B): void { console.log(a, b); }",
      errors: [
        {
          message:
            "type A of parameter a of function func12 is a class, not an interface"
        }
      ]
    },
    {
      // class method
      code:
        example + "class { method4(a: A, b: B): void { console.log(a, b); } }",
      errors: [
        {
          message:
            "type A of parameter a of function method4 is a class, not an interface"
        }
      ]
    },
    // multiple classes
    {
      // function declaration
      code:
        example +
        "function func15(a1: A, a2: A): void { console.log(a1, a2); }",
      errors: [
        {
          message:
            "type A of parameter a1 of function func15 is a class, not an interface"
        },
        {
          message:
            "type A of parameter a2 of function func15 is a class, not an interface"
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
          message:
            "type A of parameter a1 of function method3 is a class, not an interface"
        },
        {
          message:
            "type A of parameter a2 of function method3 is a class, not an interface"
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
          message:
            "type A of parameter a of function overloadMethodBad is a class, not an interface"
        },
        {
          message:
            "type A of parameter a1 of function overloadMethodBad is a class, not an interface"
        },
        {
          message:
            "type A of parameter a2 of function overloadMethodBad is a class, not an interface"
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
          message:
            "type A of parameter a of function overloadDeclarationBad is a class, not an interface"
        },
        {
          message:
            "type A of parameter a1 of function overloadDeclarationBad is a class, not an interface"
        },
        {
          message:
            "type A of parameter a2 of function overloadDeclarationBad is a class, not an interface"
        }
      ]
    },
    // nested objects
    {
      // class methods
      code:
        example +
        "class C { nestedMethodBad(b: B3): void { console.log(b); }; }",
      errors: [
        {
          message:
            "type B3 of parameter b of function nestedMethodBad is a class, not an interface"
        }
      ]
    },
    {
      // function declaration
      code:
        example +
        "function nestedDeclarationBad(b: B3): void { console.log(b); }",
      errors: [
        {
          message:
            "type B3 of parameter b of function nestedDeclarationBad is a class, not an interface"
        }
      ]
    }
  ]
});

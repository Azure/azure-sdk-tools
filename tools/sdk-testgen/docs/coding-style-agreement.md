# General
This is a draft for setting up coding style agreement for typescript projects.
Below articles are referenced in the initialization of this page:
- [Google TypeScript Style Guide](https://google.github.io/styleguide/tsguide.html#any-unknown)
- [Coding Guidelines in Microsoft/TypeScript](https://github.com/Microsoft/TypeScript/wiki/Coding-guidelines#these-guidelines-are-meant-for-contributors-to-the-typescript-projects-codebase)


# Coding Styles

## Eslint Covered Rules

Rules in this section are mandatory and can be figured out by [eslint](../eslintrc.json) automatically.
- [Identifiers Naming Convention](https://google.github.io/styleguide/tsguide.html#identifiers)
  - UpperCamelCase:	class / interface / type / enum / decorator / type parameters
  - lowerCamelCase:	variable / parameter / function / method / property / module alias
  - CONSTANT_CASE:	global constant values, including enum values
- [Constructor](https://google.github.io/styleguide/tsguide.html#constructors) calls must use parentheses, even when no arguments are passed
```
const x = new Foo;  // BAD
const x = new Foo();  // GOOD
```
- [must not instantiate the wrapper classes for the primitive types](https://google.github.io/styleguide/tsguide.html#primitive-types-wrapper-classes)
```
const s = new String('hello');  // BAD
const s = 'hello';              // GOOD
```
- [No Array Constructor](https://google.github.io/styleguide/tsguide.html#array-constructor)
```
const b = new Array(2, 3); // [2, 3];   // BAD
const b = [2, 3];                       // GOOD
```
- Use 'let' instead of 'var'
- [throw with new xxx](https://google.github.io/styleguide/tsguide.html#exceptions)
```
throw Error('Foo is not a valid bar.');         // BAD
throw new Error('Foo is not a valid bar.');     // GOOD
```
- [Don't use **for (... in ...)**](https://google.github.io/styleguide/tsguide.html#iterating-objects)
```
//BAD
for (const x in someObj) {
  // x could come from some parent prototype!
}

//GOOD
for (const x in someObj) {
  if (!someObj.hasOwnProperty(x)) continue;
  // now x was definitely defined on someObj
}
for (const x of Object.keys(someObj)) { // note: for _of_!
  // now x was definitely defined on someObj
}
for (const [key, value] of Object.entries(someObj)) { // note: for _of_!
  // now key was definitely defined on someObj
}
```
- [Multiple lines always use blocks for the containing code](https://google.github.io/styleguide/tsguide.html#control-flow-statements-blocks)
```
// BAD
if (x)
  x.doFoo();
for (let i = 0; i < x; i++)
  doSomethingWithALongMethodName(i);

// GOOD
for (let i = 0; i < x; i++) {
  doSomethingWith(i);
  andSomeMore();
}
if (x) {
  doSomethingWithALongMethodName(x);
}
if (x) x.doFoo();
```
- [Switch should has default](https://google.github.io/styleguide/tsguide.html#switch-statements)
```
switch (x) {
  case Y:
    doSomethingElse();
    break;
  default:                  // DON'T MISS THIS
    // nothing to do.
}

```
- [Always use ===/!== ](https://google.github.io/styleguide/tsguide.html#equality-checks)
```
// BAD
if (foo == 'bar' || baz != bam) {
  // Hard to understand behaviour due to type coercion.
}

// GOOD
if (foo === 'bar' || baz !== bam) {
  // All good here.
}
// EXCEPTION (GOOD)
if (foo == null) {
  // Will trigger when foo is null or undefined.
}
```
- [Use arrow functions in expressions](https://google.github.io/styleguide/tsguide.html#use-arrow-functions-in-expressions)
```
bar(function() { ... })     // BAD
bar(() => { this.doSomething(); })      // GOOD
```
- [Use Function Declaration in General](https://google.github.io/styleguide/tsguide.html#function-declarations)
```
// BAD
const foo = () => 3;  // ERROR: Invalid left-hand side of assignment expression.

// GOOD
function foo() { ... }
```
- [Use (z as Foo) instead of \<Foo\>z](https://google.github.io/styleguide/tsguide.html#type-assertions-syntax)
```
const y = <Foo>z.length;        // BAD
const x = (z as Foo).length;    // GOOD
```
- [Use semicolon for class member declaration](https://google.github.io/styleguide/tsguide.html#member-property-declarations)
- [No import from absolute path](https://google.github.io/styleguide/tsguide.html#import-paths)
- [No default export](https://google.github.io/styleguide/tsguide.html#exports)
```
export default class Foo { ... } // BAD

export class Foo { ... }        // GOOD
```
- [No mutable export](https://google.github.io/styleguide/tsguide.html#mutable-exports)
```
export let foo = 3;     // BAD

// GOOD
let foo = 3;
export function getFoo() { return foo; };
```
- [no-unnecessary-type-assertion](https://google.github.io/styleguide/tsguide.html#type-inference)
```
const x: boolean = true;    // BAD
const x = 15;       // GOOD
```
- [use undefined instead of null](https://github.com/Microsoft/TypeScript/wiki/Coding-guidelines#null-and-undefined)
- [Use T[] for simple T and Array\<T\> for complex](https://google.github.io/styleguide/tsguide.html#arrayt-type)


## Hard Rules for Review
Below items aren't eslint-configurable, but is hard rules we should follow on.
- [Aliases](https://google.github.io/styleguide/tsguide.html#aliases) use the format of the existing identifier
```
const CAPACITY = 5;

class Teapot {
  readonly BrewStateEnum = BrewStateEnum;
  readonly Capacity = CAPACITY;     // BAD
  readonly CAPACITY = CAPACITY;     // GOOD
}
```
- [Naming style](https://google.github.io/styleguide/tsguide.html#naming-style): names should not be decorated with information that is included in the type
```
class A {
    private _a;     // BAD
    private a;      // GOOD

    optional_name?: string  // BAD
    name?: string   // GOOD
}

interface FooInterface  // BAD
interface Foo;          // GOOD
```
- [Descriptive names](https://google.github.io/styleguide/tsguide.html#naming-style)
- File encoding: UTF-8
- Use a single declaration per variable statement
```
let i, j        // BAD

//GOOD
let i
let j
```
- [Document all top-level exports of modules](https://google.github.io/styleguide/tsguide.html#document-all-top-level-exports-of-modules)
- Concise initiator 
  - use [Parameter properties](https://google.github.io/styleguide/tsguide.html#parameter-properties)
  ```
  class Foo {
    constructor(private readonly barService: BarService) {}
  }
  ```
  - use [Field initializers](https://google.github.io/styleguide/tsguide.html#field-initializers)
  ```
  class Foo {
    private readonly userList: string[] = [];
  }
  ```
- Mark properties that are never reassigned outside of the constructor with the [readonly](https://google.github.io/styleguide/tsguide.html#use-readonly) modifier
- [Type Conversion](https://google.github.io/styleguide/tsguide.html#type-coercion)
```
// Below are typical recommend cases, encourage to read original Google Doc

const bool = Boolean(false);
const str = String(aNumber);
const bool2 = !!str;
const str2 = `result: ${bool2}`;

const aNumber = Number('123');
if (isNaN(aNumber)) throw new Error(...);  // Handle NaN if the string might not contain a number
```
- [Using the spread operator](https://google.github.io/styleguide/tsguide.html#using-the-spread-operator)
```
const foo = shouldUseFoo ? {num: 7} : {};
const bar = {num: 5, ...foo};
const fooStrings = ['a', 'b', 'c'];
const ids = [...fooStrings, 'd', 'e'];
```
- [Expression bodies vs block bodies](https://google.github.io/styleguide/tsguide.html#expression-bodies-vs-block-bodies)
```
// BAD: use a block ({ ... }) if the return value of the function is not used.
myPromise.then(v => console.log(v));

// GOOD: return value is unused, use a block body.
myPromise.then(v => {
  console.log(v);
});
// GOOD: code may use blocks for readability.
const transformed = [1, 2, 3].map(v => {
  const intermediate = someComplicatedExpr(v);
  const more = acrossManyLines(intermediate);
  return worthWrapping(more);
});
```
- [Don't use 'this' rebinding in normal function](https://google.github.io/styleguide/tsguide.html#rebinding-this)
```
// BAD
function clickHandler() {
  // Bad: what's `this` in this context?
  this.textContent = 'Hello';
}
// Bad: the `this` pointer reference is implicitly set to document.body.
document.body.onclick = clickHandler;


// GOOD
document.body.onclick = () => { document.body.textContent = 'hello'; };
```
- [Use arrow functions over anonymous function expressions](https://github.com/Microsoft/TypeScript/wiki/Coding-guidelines#style)
```
foo(function(){})   // BAD
foo(()=>{})         // GOOD
```
- [Don't use arrow function to initialize class function](https://google.github.io/styleguide/tsguide.html#arrow-functions-as-properties)
```
// Arrow functions usually should not be properties.
class DelayHandler {
  private patienceTracker = () => {     // BAD
    this.waitedPatiently = true;
  }
}
```
- [Use Object Literal/type definition](https://google.github.io/styleguide/tsguide.html#type-assertions-and-object-literals)
```
// BAD
interface Foo {
  bar: number;
  baz?: string;  // was "bam", but later renamed to "baz".
}
const foo = {
  bar: 123,
  bam: 'abc',  // no error!
} as Foo;
function func() {
  return {
    bar: 123,
    bam: 'abc',  // no error!
  } as Foo;
}

// GOOD
interface Foo {
  bar: number;
  baz?: string;
}

const foo: Foo = {
  bar: 123,
  bam: 'abc',  // complains about "bam" not being defined on Foo.
};

function func(): Foo {
  return {
    bar: 123,
    bam: 'abc',   // complains about "bam" not being defined on Foo.
  };
}
```
- [No mix quoted property access](https://google.github.io/styleguide/tsguide.html#optimization-compatibility-for-property-access)
```
// Bad: code must use either non-quoted or quoted access for any property
// consistently across the entire application:
console.log(x['someField']);
console.log(x.someField);
```
- [No 'const enum'](https://google.github.io/styleguide/tsguide.html#enums)
- [module vs destructuring import](https://google.github.io/styleguide/tsguide.html#module-versus-destructuring-imports)
```
// Bad: overlong import statement of needlessly namespaced names.
import {TableViewItem, TableViewHeader, TableViewRow, TableViewModel,
  TableViewRenderer} from './tableview';
let item: TableViewItem = ...;

// Better: use the module for namespacing.
import * as tableview from './tableview';
let item: tableview.Item = ...;
```
- [Use type annotation on complex objects/return types]:
    - https://google.github.io/styleguide/tsguide.html#return-types
    - https://google.github.io/styleguide/tsguide.
    - https://google.github.io/styleguide/tsguide.html#mapped-conditional-typeshtml#structural-types-vs-nominal-types
- [No Nullable/undefined type aliases](https://google.github.io/styleguide/tsguide.html#nullableundefined-type-aliases)
```
// Bad
type CoffeeResponse = Latte|Americano|undefined;

// Better
type CoffeeResponse = Latte|Americano;
class CoffeeService {
  getLatte(): CoffeeResponse|undefined { ... };
}

// Best
type CoffeeResponse = Latte|Americano;
class CoffeeService {
  getLatte(): CoffeeResponse {
    return assert(fetchResponse(), 'Coffee maker is broken, file a ticket');
  };
}
```
- [prefer Optionals to |undefined type](https://google.github.io/styleguide/tsguide.html#optionals-vs-undefined-type)
```
interface CoffeeOrder {
  sugarCubes: number;
  milk?: Whole|LowFat|HalfHalf;
}
```
- [prefer 'interface' to 'type']
    - https://google.github.io/styleguide/tsguide.html#interfaces-vs-type-aliases
    - https://google.github.io/styleguide/tsguide.html#mapped-conditional-types
```
// BAD
type User = {
  firstName: string,
  lastName: string,
}

// GOOD
interface User {
  firstName: string;
  lastName: string;
}

```
- [named tuple](https://google.github.io/styleguide/tsguide.html#tuple-types)
```
function splitHostPort(address: string): {host: string, port: number} {
  ...
}

// Use it like:
const address = splitHostPort(userAddress);
use(address.port);

// You can also get tuple-like behavior using destructuring:
const {host, port} = splitHostPort(userAddress);
```


## Soft Rules
Items in this section is suggestable in general cases.
- JSDoc vs comments
  - Use /** JSDoc */ comments for documentation, i.e. comments a user of the code should read.
  - Use // line comments for implementation comments, i.e. comments that only concern the implementation of the code itself.
  - JSDoc comments should follow [JavaScript style guide's rules for JSDoc](https://google.github.io/styleguide/jsguide.html#jsdoc)
- Make comments that actually add information.
- [Visiblility](https://google.github.io/styleguide/tsguide.html#visibility): Restricting visibility of properties, methods, and entire types helps with keeping code decoupled.
  - TypeScript symbols **are public by default**. Never use the public modifier except when declaring non-readonly public parameter properties (in constructors).
- [Properties used outside of class lexical scope](https://google.github.io/styleguide/tsguide.html#properties-used-outside-of-class-lexical-scope): envelop with Getter/Setter when only have more logic
``` diff
  // Incorrect case
  class Bar {
  private barInternal = '';
  // Neither of these accessors have logic, so just make bar public.
  get bar() {
    return this.barInternal;
  }

  set bar(value: string) {
    this.barInternal = value;
  }
}
```
- [Type and Non-nullability Assertions](https://google.github.io/styleguide/tsguide.html#type-and-non-nullability-assertions)
```
// BAD
(x as Foo).foo();
y!.bar();

// GOOD
if (x instanceof Foo) {
  x.foo();
}
if (y) {
  y.bar();
}
```
- [No new decorators](https://google.github.io/styleguide/tsguide.html#decorators)
- [Don't define namespace](https://google.github.io/styleguide/tsguide.html#namespaces-vs-modules)
- [Do not create container classes with static methods or properties for the sake of namespacing](https://google.github.io/styleguide/tsguide.html#container-classes)
```
// BAD
export class Container {
  static FOO = 1;
  static bar() { return 1; }
}

// GOOD
export const FOO = 1;
export function bar() { return 1; }
```
- [Do not use 'import type'/'export type'](https://google.github.io/styleguide/tsguide.html#import-export-type)
- More than 2 related Boolean properties on a type should be turned into a flag.
- 'any' is dangerous, 'unknown' is a bit better


## Discussion
- shall we enable airbnb https://github.com/airbnb/javascript
    - heavy
    - not microsoft


# Code Review Goals
Below are general guidelins copied from [Google TypeScript Style Guide](https://google.github.io/styleguide/tsguide.html#goals)
```
In general, engineers usually know best about what's needed in their code, so if there are multiple options and the choice is situation dependent, we should let decisions be made locally. So the default answer should be "leave it out".

The following points are the exceptions:
1. Code should avoid patterns that are known to cause problems, especially for users new to the language.
2. Code across projects should be consistent across irrelevant variations.
3. Code should be maintainable in the long term.
4. Code reviewers should be focused on improving the quality of the code, not enforcing arbitrary rules.
```
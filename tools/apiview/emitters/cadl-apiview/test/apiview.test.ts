import { resolvePath } from "@cadl-lang/compiler";
import { expectDiagnosticEmpty } from "@cadl-lang/compiler/testing";
import { strictEqual } from "assert";
import { ApiViewDocument, ApiViewTokenKind } from "../src/apiview.js";
import { ApiViewEmitterOptions } from "../src/lib.js";
import { createApiViewTestRunner } from "./test-host.js";

describe("apiview: tests", () => {
  async function apiViewFor(code: string, options: ApiViewEmitterOptions): Promise<ApiViewDocument> {
    const runner = await createApiViewTestRunner({withVersioning: true});
    const outPath = resolvePath("/apiview.json");
    const diagnostics = await runner.diagnose(code, {
      noEmit: false,
      emitters: { "@azure-tools/cadl-apiview": { ...options, "output-file": outPath } },
    });
    expectDiagnosticEmpty(diagnostics);

    const jsonText = runner.fs.get(outPath)!;
    const apiview = JSON.parse(jsonText) as ApiViewDocument;
    return apiview;
  }

  function apiViewText(apiview: ApiViewDocument): string[] {
    const vals = new Array<string>;
    for (const token of apiview.Tokens) {
      switch (token.Kind) {
        case ApiViewTokenKind.Newline:
          vals.push("\n");
          break;
        default:
          if (token.Value != undefined) {
            vals.push(token.Value);
          }
          break;
      }
    }
    return vals.join("").split("\n");
  }

  function compare(expect: string, lines: string[], offset: number) {
    // split the input into lines and ignore leading or trailing empty lines.
    let expectedLines = expect.split("\n");
    if (expectedLines[0].trim() == '') {
      expectedLines = expectedLines.slice(1);
    }
    if (expectedLines[expectedLines.length - 1].trim() == '') {
      expectedLines = expectedLines.slice(0, -1);
    }
    // remove any leading indentation
    const indent = expectedLines[0].length - expectedLines[0].trimStart().length;
    for (let x = 0; x < expectedLines.length; x++) {
      expectedLines[x] = expectedLines[x].substring(indent);
    }
    const checkLines = lines.slice(offset, offset + expectedLines.length);
    strictEqual(expectedLines.length, checkLines.length);
    for (let x = 0; x < checkLines.length; x++) {
      strictEqual(expectedLines[x], checkLines[x], `Actual differed from expected at line #${x + 1}\nACTUAL: '${checkLines[x]}'\nEXPECTED: '${expectedLines[x]}'`);
    }
  }

  it("describes model", async () => {
    const input = `
    @Cadl.serviceTitle("Test")
    namespace Azure.Test {
      model Foo {
        name: string;
        age?: int16;
      }
    }
    `;
    const apiview = await apiViewFor(input, {});
    const actual = apiViewText(apiview);
    compare(input, actual, 3);
  });

  it("describes templated model", async () => {
    const input = `
    @Cadl.serviceTitle("Test")
    namespace Azure.Test {
      model Foo<TOne, TTwo> {
        one: TOne;
        two: TTwo;
      }
    }
    `;
    const apiview = await apiViewFor(input, {});
    const actual = apiViewText(apiview);
    compare(input, actual, 3);
  });

  it("describes enum", async () => {
    const input = `
    @Cadl.serviceTitle("Test")
    namespace Azure.Test {

      enum SomeEnum {
        Plain,
        "Literal",
      }

      enum SomeStringEnum {
        A: "A",
        B: "B",
      }

      enum SomeIntEnum {
        A: 1,
        B: 2,
      }
    }`;
    const expect = `
    @Cadl.serviceTitle("Test")
    namespace Azure.Test {
      enum SomeEnum {
        Plain,
        "Literal",
      }

      enum SomeIntEnum {
        A: 1,
        B: 2,
      }

      enum SomeStringEnum {
        A: "A",
        B: "B",
      }
    }`;
    const apiview = await apiViewFor(input, {});
    const actual = apiViewText(apiview);
    compare(expect, actual, 3);
  });

  it("describes union", async () =>{
    const input = `
    @Cadl.serviceTitle("Test")
    namespace Azure.Test {
      union MyUnion {
        cat: Cat,
        dog: Dog,
        snake: Snake
      }

      model Cat {
        name: string;
      }

      model Dog {
        name: string;
      }

      model Snake {
        name: string;
        length: int16;
      }
    }`;
    const expect = `
    @Cadl.serviceTitle("Test")
    namespace Azure.Test {
      model Cat {
        name: string;
      }

      model Dog {
        name: string;
      }

      union MyUnion {
        cat: Cat,
        dog: Dog,
        snake: Snake
      }

      model Snake {
        name: string;
        length: int16;
      }
    }
    `;
    const apiview = await apiViewFor(input, {});
    const actual = apiViewText(apiview);
    compare(expect, actual, 3);
  });

  it("describes template operation", async () =>{
    const input = `
    @Cadl.serviceTitle("Test")
    namespace Azure.Test {
      model FooParams {
        a: string;
        b: string;
      }

      op ResourceRead<TResource, TParams>(resource: TResource, params: TParams): TResource;

      op GetFoo is ResourceRead<
        {
          @query
          @doc("The name")
          name: string,
          ...FooParams
        },
        {
          parameters: {
            @query
            @doc("The collection id.")
            fooId: string
          };
        }
      >;
    }`;
    const expect = `
    @Cadl.serviceTitle("Test")
    namespace Azure.Test {
      op GetFoo is ResourceRead<
        {
          @query
          name: string,
          ...FooParams
        },
        {
          parameters:
            {
              @query
              fooId: string
            }
        }
      >;

      op ResourceRead<TResource, TParams>(
        resource: TResource,
        params: TParams
      ): TResource;

      model FooParams {
        a: string;
        b: string;
      }
    }`;
    const apiview = await apiViewFor(input, {});
    const lines = apiViewText(apiview);
    compare(expect, lines, 3);
  });

  it("describes operation with anonymous models", async () =>{
    const input = `
    @Cadl.serviceTitle("Test")
    namespace Azure.Test {
      op SomeOp(
        param1: {
          name: string
        },
        param2: {
          age: int16
        }
      ): string;
    }`;
    const expect = `
    @Cadl.serviceTitle("Test")
    namespace Azure.Test {
      op SomeOp(
        param1:
          {
            name: string
          },
        param2:
          {
            age: int16
          }
      ): string;
    }`;
    const apiview = await apiViewFor(input, {});
    const lines = apiViewText(apiview);
    compare(expect, lines, 3);
  });

  it("describes interface", async () => {
    const input = `
    @Cadl.serviceTitle("Test")
    namespace Azure.Test {
      interface Foo {
        @get 
        @route("get/{name}")
        get(@path name: string): string;


        @get
        @route("list")
        list(): string[];
      }
    }
    `;
    const expect = `
    @Cadl.serviceTitle("Test")
    namespace Azure.Test {
      interface Foo {
        @get
        @route("get/{name}")
        get(
          @path
          name: string
        ): string;

        @get
        @route("list")
        list(): string[];
      }
    }
    `;
    const apiview = await apiViewFor(input, {});
    const lines = apiViewText(apiview);
    compare(expect, lines, 3);
  });  
});

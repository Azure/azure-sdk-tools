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

  function stringifyLines(lines: string[], start: number, end: number): string {
    const slice = lines.slice(start, end + 1)
    return slice.map((line) => line.trimStart()).join("");
  }

  function indentCounts(lines: string[], start: number, end: number): number[] {
    const counts = Array<number>();
    const slice = lines.slice(start, end + 1);
    for (const line of slice) {
      let count = 0;
      for (const char of [...line]) {
        if (char == " ") {
          count += 1;
        } else {
          break;
        }
      }
      counts.push(count);
    }
    return counts;
  }

  it("describes model", async () => {
    const input = `
    @Cadl.serviceTitle("Model Test")
    namespace Azure.Test {
      model Foo {
        name: string;
        age?: int16;
      }
    }
    `;
    const apiview = await apiViewFor(input, {});
    const lines = apiViewText(apiview);
    strictEqual(stringifyLines(lines, 4, 7), `enum SomeEnum {Plain,"Literal",}`);  
  });


  it("describes enum", async () => {
    const input = `
    @Cadl.serviceTitle("Enum Test")
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
    const apiview = await apiViewFor(input, {});
    const lines = apiViewText(apiview);
    strictEqual(stringifyLines(lines, 4, 7), `enum SomeEnum {Plain,"Literal",}`);
    strictEqual(stringifyLines(lines, 9, 12), `enum SomeIntEnum {A: 1,B: 2,}`);
    strictEqual(stringifyLines(lines, 14, 17), `enum SomeStringEnum {A: "A",B: "B",}`)
  });

  it("describes union", async () =>{
    const input = `
    @Cadl.serviceTitle("Union Test")
    namespace Azure.Test {
      union MyUnion {
        cat: Cat,
        dog: Dog,
        snake: Snake
      }

      model Cat {
        name: string
      };

      model Dog {
        name: string
      };

      model Snake {
        name: string,
        length: int16
      };
    }`;
    const apiview = await apiViewFor(input, {});
    const lines = apiViewText(apiview);
    strictEqual(stringifyLines(lines, 12, 16), `union MyUnion {cat: Cat,dog: Dog,snake: Snake}`);
  });

  it("describes operation", async () =>{
    const input = `
    @Cadl.serviceTitle("Operation Test")
    namespace Azure.Test {
      op ResourceRead<TResource, TParams>(resource: TResource, params: TParams): TResource;

      op GetFoo is ResourceRead<
        {
          @query
          @doc("The name")
          name: string;
        },
        {
          parameters: {
            @query
            @doc("The collection id.")
            fooId: string;
          };
        }
      >;
    }`;
    const apiview = await apiViewFor(input, {});
    const lines = apiViewText(apiview);
    strictEqual(indentCounts(lines, 4, 6), [0, 2, 4]);
    strictEqual(indentCounts(lines, 7, 10), [0, 2, 4, 4]);
  });

  it("describes interface", async () => {
    const input = `
    @Cadl.serviceTitle("Model Test")
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
    const apiview = await apiViewFor(input, {});
    const lines = apiViewText(apiview);
    strictEqual(stringifyLines(lines, 4, 7), `enum SomeEnum {Plain,"Literal",}`);  
  });  
});

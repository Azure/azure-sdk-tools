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

  it("describes enums", async () => {
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

  namespace BuildingBlocks {
    model Block is string;

    model Thing {
      someInt: SomeIntEnum;
    }
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
    namespace Azure.Test {
      union MyUnion {
        cat: Cat,
        dog: Dog,
        snake: Snake
      }

      model Cat is string;

      model Dog is string;

      model Snake is string;
    }`;
    const apiview = await apiViewFor(input, {});
    const lines = apiViewText(apiview);
    strictEqual(stringifyLines(lines, 2, 6), `union MyUnion {cat: Cat,dog: Dog,snake: Snake}`);
  });
});

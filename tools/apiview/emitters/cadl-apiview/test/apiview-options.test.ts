import { Diagnostic, logDiagnostics, resolvePath } from "@cadl-lang/compiler";
import { expectDiagnosticEmpty } from "@cadl-lang/compiler/testing";
import { strictEqual } from "assert";
import { apiViewFor, apiViewText, compare } from "./test-host.js";

describe("apiview-options: tests", () => {

  it("omits namespaces that aren't proper subnamespaces", async () => {
    const input = `
    @Cadl.service( { title: "Test", version: "1" } )
    namespace Azure.Test {
      model Foo {};
    }

    namespace Azure.Test.Sub {
      model SubFoo {};
    };

    namespace Azure.TestBad {
      model BadFoo {};
    };
    `;
    const expect = `
    namespace Azure.Test {
      model Foo {}
    }

    namespace Azure.Test.Sub {
      model SubFoo {}
    }
    `
    const apiview = await apiViewFor(input, {});
    const actual = apiViewText(apiview);
    compare(expect, actual, 9);
  });

  it("outputs the global namespace when --include-global-namespace is set", async () => {
    const input = `
    model SomeGlobal {};

    @Cadl.service( { title: "Test", version: "1" } )
    namespace Azure.Test {
      model Foo {};
    }
    `;
    const expect = `
    model SomeGlobal {};

    namespace Azure.Test {
      model Foo {}
    }
    `
    const apiview = await apiViewFor(input, {
      "include-global-namespace": true
    });
    const actual = apiViewText(apiview);
    compare(expect, actual, 9);
  });

});

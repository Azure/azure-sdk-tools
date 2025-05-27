import { expectDiagnostics } from "@typespec/compiler/testing";
import { apiViewFor, apiViewText, compare, diagnosticsFor } from "./test-host.js";
import { describe, it } from "vitest";

describe("apiview-options: tests", () => {

  it("omits namespaces that aren't proper subnamespaces", async () => {
    const input = `
    @TypeSpec.service( #{ title: "Test"} )
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
    compare(expect, actual, 6);
  });

  it("outputs the global namespace when --include-global-namespace is set", async () => {
    const input = `
    model SomeGlobal {};

    @TypeSpec.service( #{ title: "Test"} )
    namespace Azure.Test {
      model Foo {};
    }
    `;
    const expect = `
    namespace ::GLOBAL:: {
      model SomeGlobal {}
    }

    @TypeSpec.service(#{
      title: "Test"
    })
    namespace Azure.Test {
      model Foo {}
    }
    `
    const apiview = await apiViewFor(input, {
      "include-global-namespace": true
    });
    // TODO: Update once bug is fixed: https://github.com/microsoft/typespec/issues/3165
    const actual = apiViewText(apiview);
    compare(expect, actual, 3);
  });
});

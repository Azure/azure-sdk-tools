import { Diagnostic, logDiagnostics, resolvePath } from "@cadl-lang/compiler";
import { expectDiagnosticEmpty, expectDiagnostics } from "@cadl-lang/compiler/testing";
import { strictEqual } from "assert";
import { apiViewFor, apiViewText, compare, createApiViewTestRunner, diagnosticsFor } from "./test-host.js";

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
    namespace ::GLOBAL:: {
      model SomeGlobal {}
    }

    @Cadl.service(
      {
        title: "Test";
        version: "1";
      }
    )
    namespace Azure.Test {
      model Foo {}
    }
    `
    const apiview = await apiViewFor(input, {
      "include-global-namespace": true
    });
    const actual = apiViewText(apiview);
    compare(expect, actual, 1);
  });

  it("emits error if multi-service package tries to specify version", async () => {
    const input = `
    @Cadl.service( { title: "Test", version: "1" } )
    namespace Azure.Test {
      model Foo {};
    }

    @Cadl.service( { title: "OtherTest", version: "1" } )
    namespace Azure.OtherTest {
      model Foo {};
    }
    `
    const diagnostics = await diagnosticsFor(input, {"version": "1"});
    expectDiagnostics(diagnostics, [
      {
        code: "@azure-tools/cadl-apiview/invalid-option",
        message: `Option "--output-file" cannot be used with multi-service specs unless "--service" is also supplied.`
      },
      {
        code: "@azure-tools/cadl-apiview/invalid-option",
        message: `Option "--version" cannot be used with multi-service specs unless "--service" is also supplied.`
      }
    ]);
  });

  it("allows options if multi-service package specifies --service", async () => {
    const input = `
    @Cadl.service( { title: "Test", version: "1" } )
    namespace Azure.Test {
      model Foo {};
    }

    @Cadl.service( { title: "OtherTest", version: "1" } )
    namespace Azure.OtherTest {
      model Foo {};
    }
    `;
    const expect = `
    namespace Azure.OtherTest {
      model Foo {}
    }
    `;
    const apiview = await apiViewFor(input, {"version": "1", "service": "OtherTest"});
    const actual = apiViewText(apiview);
    compare(expect, actual, 9);
  });
});

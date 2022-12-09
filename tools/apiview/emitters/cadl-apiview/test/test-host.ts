import { createTestHost, createTestWrapper } from "@cadl-lang/compiler/testing";
import { RestTestLibrary } from "@cadl-lang/rest/testing";
import { VersioningTestLibrary } from "@cadl-lang/versioning/testing";
import { AzureCoreTestLibrary } from "@azure-tools/cadl-azure-core/testing";
import { ApiViewTestLibrary } from "../src/testing/index.js";
import "@azure-tools/cadl-apiview";

export async function createApiViewTestHost() {
  return createTestHost({
    libraries: [ApiViewTestLibrary, RestTestLibrary, VersioningTestLibrary, AzureCoreTestLibrary],
  });
}

export async function createApiViewTestRunner({
  withVersioning,
}: { withVersioning?: boolean } = {}) {
  const host = await createApiViewTestHost();
  const autoUsings = [
    "Cadl.Rest",
    "Cadl.Http",
  ]
  if (withVersioning) {
    autoUsings.push("Cadl.Versioning");
  }
  return createTestWrapper(host, {
    autoUsings: autoUsings,
    compilerOptions: {
      emit: ["@azure-tools/cadl-apiview"],
    }
  });
}

import { resolvePath } from "@typespec/compiler";
import { createTestLibrary, findTestPackageRoot, TypeSpecTestLibrary } from "@typespec/compiler/testing";
import { fileURLToPath } from "url";

export const ApiViewTestLibrary: TypeSpecTestLibrary = createTestLibrary({
  name: "@azure-tools/typespec-apiview",
  packageRoot: await findTestPackageRoot(import.meta.url),
});

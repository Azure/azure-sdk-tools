import { createTestLibrary, findTestPackageRoot, TypeSpecTestLibrary } from "@typespec/compiler/testing";

export const ApiViewTestLibrary: TypeSpecTestLibrary = createTestLibrary({
  name: "@azure-tools/typespec-apiview",
  packageRoot: await findTestPackageRoot(import.meta.url),
});

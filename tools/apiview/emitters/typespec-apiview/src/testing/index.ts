import { resolvePath } from "@typespec/compiler";
import { TypeSpecTestLibrary } from "@typespec/compiler/testing";
import { fileURLToPath } from "url";

export const ApiViewTestLibrary: TypeSpecTestLibrary = {
  name: "@azure-tools/typespec-apiview",
  packageRoot: resolvePath(fileURLToPath(import.meta.url), "../../../../"),
  files: [
    {
      realDir: "",
      pattern: "package.json",
      virtualPath: "./node_modules/@azure-tools/typespec-apiview",
    },
    {
      realDir: "dist/src",
      pattern: "*.js",
      virtualPath: "./node_modules/@azure-tools/typespec-apiview/dist/src",
    },
  ],
};

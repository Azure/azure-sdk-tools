import { resolvePath } from "@cadl-lang/compiler";
import { CadlTestLibrary } from "@cadl-lang/compiler/testing";
import { fileURLToPath } from "url";

export const ApiViewTestLibrary: CadlTestLibrary = {
  name: "@azure-tools/cadl-apiview",
  packageRoot: resolvePath(fileURLToPath(import.meta.url), "../../../../"),
  files: [
    {
      realDir: "",
      pattern: "package.json",
      virtualPath: "./node_modules/@azure-tools/cadl-apiview",
    },
    {
      realDir: "dist/src",
      pattern: "*.js",
      virtualPath: "./node_modules/@azure-tools/cadl-apiview/dist/src",
    },
  ],
};

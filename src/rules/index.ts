/**
 * @fileoverview All rules
 * @author Arpan Laha
 */

import tsConfigAllowSyntheticDefaultImports from "./ts-config-allowsyntheticdefaultimports";
import tsConfigDeclaration from "./ts-config-declaration";
import tsConfigEsModuleInterop from "./ts-config-esmoduleinterop";
import tsConfigForceConsistentCasingInFileNames from "./ts-config-forceconsistentcasinginfilenames";
import tsConfigImportHelpers from "./ts-config-importhelpers";
import tsConfigIsolatedModules from "./ts-config-isolatedmodules";
import tsConfigModule from "./ts-config-module";
import tsConfigNoExperimentalDecorators from "./ts-config-no-experimentaldecorators";
import tsConfigStrict from "./ts-config-strict";
import tsPackageJsonBugs from "./ts-package-json-bugs";
import tsPackageJsonRepo from "./ts-package-json-repo";

export = {
  "ts-config-allowsyntheticdefaultimports": tsConfigAllowSyntheticDefaultImports,
  "ts-config-declaration": tsConfigDeclaration,
  "ts-config-esmoduleinterop": tsConfigEsModuleInterop,
  "ts-config-forceconsistentcasinginfilenames": tsConfigForceConsistentCasingInFileNames,
  "ts-config-importhelpers": tsConfigImportHelpers,
  "ts-config-isolatedmodules": tsConfigIsolatedModules,
  "ts-config-module": tsConfigModule,
  "ts-config-noexperimentaldecorators": tsConfigNoExperimentalDecorators,
  "ts-config-strict": tsConfigStrict,
  "ts-package-json-bugs": tsPackageJsonBugs,
  "ts-package-json-repo": tsPackageJsonRepo
};

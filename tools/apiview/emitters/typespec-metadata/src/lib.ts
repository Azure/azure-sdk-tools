import { createTypeSpecLibrary, paramMessage } from "@typespec/compiler";
import { metadataEmitterOptionsSchema } from "./options.js";

export const $lib = createTypeSpecLibrary({
  name: "typespec-metadata",
  diagnostics: {
    "failed-to-emit": {
      severity: "error",
      messages: {
        default: paramMessage`Failed to emit metadata file: ${"message"}.`,
      },
    },
    "no-types-found": {
      severity: "warning",
      messages: {
        default:
          "The metadata emitter didn't find any TypeSpec declarations defined in the current project. An empty snapshot will be produced.",
      },
    },
  },
  emitter: {
    options: metadataEmitterOptionsSchema,
  },
});

export const { reportDiagnostic, createDiagnostic } = $lib;

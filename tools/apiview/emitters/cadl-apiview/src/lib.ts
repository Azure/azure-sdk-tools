import { createCadlLibrary, JSONSchemaType, paramMessage } from "@cadl-lang/compiler";

export interface ApiViewEmitterOptions {
  "output-dir"?: string;
  "output-file"?: string;
  "namespace"?: string;
  "version"?: string;
}

const ApiViewEmitterOptionsSchema: JSONSchemaType<ApiViewEmitterOptions> = {
  type: "object",
  additionalProperties: false,
  properties: {
    "output-dir": { type: "string", nullable: true },
    "output-file": { type: "string", nullable: true },
    "namespace": { type: "string", nullable: true },
    "version": {type: "string", nullable: true },
  },
  required: [],
};


export const $lib = createCadlLibrary({
  name: "@azure-tools/cadl-apiview",
  diagnostics: {
    "use-namespace-option": {
      severity: "error",
      messages: {
        default: "Unable to resolve namespace. Please supply `--option \"@azure-tools/cadl-apiview.namespace={value}\"`.",
      }
    },
    "version-not-found": {
      severity: "error",
      messages: {
        default: paramMessage`Version "${"version"}" not found. Allowed values: ${"allowed"}.`,
      }
    },
  },
  emitter: {
    options: ApiViewEmitterOptionsSchema,
  },
});
export const { reportDiagnostic } = $lib;

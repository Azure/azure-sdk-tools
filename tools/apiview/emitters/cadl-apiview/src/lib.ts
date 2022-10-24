import { createCadlLibrary, JSONSchemaType } from "@cadl-lang/compiler";

export interface ApiViewEmitterOptions {
  "output-dir"?: string;
  "output-file"?: string;
  "service-namespace"?: string;
}

const ApiViewEmitterOptionsSchema: JSONSchemaType<ApiViewEmitterOptions> = {
  type: "object",
  additionalProperties: false,
  properties: {
    "output-dir": { type: "string", nullable: true },
    "output-file": { type: "string", nullable: true },
    "service-namespace": { type: "string", nullable: true },
  },
  required: [],
};

export const libDef = {
  name: "@azure-tools/cadl-apiview",
  diagnostics: {
    "use-namespace-option": {
      severity: "error",
      messages: {
        default: "Unable to resolve namespace. Please supply `--option \"@azure-tools/cadl-apiview.service-namespace={value}\"`.",
      }
    },
  },
  emitter: {
    options: ApiViewEmitterOptionsSchema,
  },
} as const;

export const $lib = createCadlLibrary(libDef);
export const { reportDiagnostic } = $lib;

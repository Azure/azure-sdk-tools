import { createTypeSpecLibrary, JSONSchemaType, paramMessage } from "@typespec/compiler";

export interface ApiViewEmitterOptions {
  "output-file"?: string;
  "service"?: string;
  "version"?: string;
  "include-global-namespace"?: boolean,
  "mapping-path"?: string;
}

const ApiViewEmitterOptionsSchema: JSONSchemaType<ApiViewEmitterOptions> = {
  type: "object",
  additionalProperties: false,
  properties: {
    "output-file": { type: "string", nullable: true },
    "service": { type: "string", nullable: true },
    "version": {type: "string", nullable: true },
    "include-global-namespace": {type: "boolean", nullable: true},
    "mapping-path": { type: "string", nullable: true },
  },
  required: [],
};


export const $lib = createTypeSpecLibrary({
  name: "@azure-tools/typespec-apiview",
  diagnostics: {
    "no-services-found": {
      severity: "error",
      messages: {
        default: "No services found. Ensure there is a namespace in the spec annotated with the `@service` decorator."
      }
    },
    "invalid-service": {
      severity: "error",
      messages: {
        default: paramMessage`Service "${"value"}" was not found. Please check for typos.`,
      }
    },
    "invalid-option": {
      severity: "error",
      messages: {
        default: paramMessage`Option "--${"name"}" cannot be used with multi-service specs unless "--service" is also supplied.`,
      }
    },
    "version-not-found": {
      severity: "error",
      messages: {
        default: paramMessage`Version "${"version"}" not found for service "${"serviceName"}". Allowed values: ${"allowed"}.`,
      }
    },
  },
  emitter: {
    options: ApiViewEmitterOptionsSchema,
  },
});
export const { reportDiagnostic } = $lib;

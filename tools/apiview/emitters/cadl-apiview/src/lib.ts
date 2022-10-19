import { createCadlLibrary, JSONSchemaType } from "@cadl-lang/compiler";

export interface ApiViewEmitterOptions {
  "output-dir"?: string;
  "output-file"?: string;
}

const ApiViewEmitterOptionsSchema: JSONSchemaType<ApiViewEmitterOptions> = {
  type: "object",
  additionalProperties: false,
  properties: {
    "output-dir": { type: "string", nullable: true },
    "output-file": { type: "string", nullable: true },
  },
  required: [],
};

export const $lib = createCadlLibrary({
  name: "ApiView",
  diagnostics: {},
  emitter: {
    options: ApiViewEmitterOptionsSchema,
  },
});

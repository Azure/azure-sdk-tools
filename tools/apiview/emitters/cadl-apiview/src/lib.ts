import { createCadlLibrary, JSONSchemaType } from "@cadl-lang/compiler";

export interface ApiViewEmitterOptions {
  "output-file"?: string;
}

const ApiViewEmitterOptionsSchema: JSONSchemaType<ApiViewEmitterOptions> = {
  type: "object",
  additionalProperties: false,
  properties: {
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

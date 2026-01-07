import type { JSONSchemaType } from "@typespec/compiler";

export type MetadataOutputFormat = "yaml" | "json";

export interface MetadataEmitterOptions {
  /**
   * Relative path of the emitted metadata file. When omitted, the emitter picks an extension based on the format.
   */
  outputFile?: string;
  /**
   * Serialization format for the metadata snapshot.
   */
  format?: MetadataOutputFormat;
}

export interface NormalizedMetadataEmitterOptions {
  outputFile: string;
  format: MetadataOutputFormat;
}

const DEFAULT_FORMAT: MetadataOutputFormat = "yaml";
const FALLBACK_FILENAMES: Record<MetadataOutputFormat, string> = {
  json: "typespec-metadata.json",
  yaml: "typespec-metadata.yaml",
};

export const metadataEmitterOptionsSchema: JSONSchemaType<MetadataEmitterOptions> = {
  type: "object",
  additionalProperties: false,
  properties: {
    outputFile: { type: "string", nullable: true },
    format: { type: "string", enum: ["yaml", "json"], nullable: true },
  },
};

export function normalizeOptions(
  rawOptions: MetadataEmitterOptions | undefined,
): NormalizedMetadataEmitterOptions {
  const format = rawOptions?.format ?? DEFAULT_FORMAT;
  const sanitizedOutput = rawOptions?.outputFile?.trim();
  return {
    format,
    outputFile: sanitizedOutput && sanitizedOutput.length > 0 ? sanitizedOutput : FALLBACK_FILENAMES[format],
  };
}

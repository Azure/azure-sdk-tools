export interface SpecNamespaceMetadata {
  /** Fully-qualified namespace, e.g. Contoso.Example. */
  name: string;
  /** Summary from @summary decorator. */
  summary?: string;
  /** Documentation from @doc decorator. */
  documentation?: string;
}

export interface SpecMetadata {
  /** Ordered list of root namespaces discovered in the program. */
  namespaces: SpecNamespaceMetadata[];
  /** Optional high-level summary derived from the first namespace with a summary. */
  summary?: string;
}

export interface LanguagePackageMetadata {
  /** Name of the emitter entry in tspconfig (package or path). */
  emitterName: string;
  /** Package/Binary identifier configured for the language emitter. */
  packageName?: string;
  /** Service namespace configured for the language emitter. */
  namespace?: string;
  /** Output directory for the emitter. */
  outputDir?: string;
  /** Flavor of the emitter (e.g., 'azure'). */
  flavor?: string;
  /** Service directory path for this language emitter. */
  serviceDir?: string;
}

export interface MetadataSnapshot {
  /** Semantic version for the metadata payload schema. */
  emitterVersion: string;
  /** ISO timestamp to simplify debugging when metadata was produced. */
  generatedAt: string;
  /** TypeSpec-level metadata (namespaces, descriptions, etc.). */
  spec: SpecMetadata;
  /** Per-language package metadata extracted from tspconfig, keyed by language. */
  languages: Record<string, LanguagePackageMetadata>;
  /** Absolute tspconfig path when available. */
  sourceConfigPath?: string;
}

export interface SpecNamespaceMetadata {
  /** Simple namespace identifier (unqualified). */
  name: string;
  /** Fully-qualified namespace, e.g. Contoso.Example. */
  fullName: string;
  /** Primary description sourced from @doc or @summary. */
  description?: string;
  /** Raw doc comment content, if provided. */
  documentation?: string;
}

export interface SpecMetadata {
  /** Ordered list of root namespaces discovered in the program. */
  namespaces: SpecNamespaceMetadata[];
  /** Optional high-level description derived from the first namespace with docs. */
  description?: string;
}

export interface LanguagePackageMetadata {
  /** Name of the emitter entry in tspconfig (package or path). */
  emitterName: string;
  /** Friendly language identifier inferred from the emitter. */
  language: string;
  /** Package/Binary identifier configured for the language emitter. */
  packageName?: string;
  /** Service namespace configured for the language emitter. */
  namespace?: string;
  /** All options captured verbatim from tspconfig for this emitter. */
  options: Record<string, unknown>;
}

export interface MetadataSnapshot {
  /** Semantic version for the metadata payload schema. */
  version: string;
  /** ISO timestamp to simplify debugging when metadata was produced. */
  generatedAt: string;
  /** TypeSpec-level metadata (namespaces, descriptions, etc.). */
  spec: SpecMetadata;
  /** Per-language package metadata extracted from tspconfig. */
  languages: LanguagePackageMetadata[];
  /** Absolute tspconfig path when available. */
  sourceConfigPath?: string;
}

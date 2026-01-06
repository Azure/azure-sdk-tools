import { getDoc, getNamespaceFullName, getSummary, type Namespace, type Program } from "@typespec/compiler";
import { LanguagePackageMetadata, SpecMetadata, SpecNamespaceMetadata } from "./metadata.js";

const PACKAGE_NAME_KEYS = ["package-name", "packageName", "package"];
const NAMESPACE_KEYS = ["namespace", "namespace-name", "namespaceName"];

const LANGUAGE_ALIASES: Record<string, string> = {
  "@azure-tools/typespec-csharp": "csharp",
  "@azure-tools/typespec-java": "java",
  "@azure-tools/typespec-python": "python",
  "@azure-tools/typespec-typescript": "typescript",
  "@azure-tools/typespec-js": "javascript",
  "@azure-tools/typespec-swift": "swift",
  "@azure-tools/typespec-go": "go",
};

export interface LanguageCollectionResult {
  languages: LanguagePackageMetadata[];
  sourceConfigPath?: string;
}

export async function collectLanguagePackages(program: Program): Promise<LanguageCollectionResult> {
  const optionMap = program.compilerOptions.options ?? {};
  return {
    languages: buildLanguageMetadata(optionMap),
    sourceConfigPath: program.compilerOptions.config,
  };
}

export function buildSpecMetadata(program: Program): SpecMetadata {
  const globalNamespace = program.getGlobalNamespaceType();
  const discoveredNamespaces = Array.from(globalNamespace.namespaces.values()).map((ns) =>
    createNamespaceMetadata(program, ns),
  );

  if (discoveredNamespaces.length === 0) {
    discoveredNamespaces.push(createNamespaceMetadata(program, globalNamespace));
  }

  return {
    namespaces: discoveredNamespaces,
    description: discoveredNamespaces.find((ns) => !!ns.description)?.description,
  };
}

function buildLanguageMetadata(optionMap: Record<string, Record<string, unknown>>): LanguagePackageMetadata[] {
  return Object.entries(optionMap).map(([emitterName, emitterOptions]) =>
    createLanguageMetadata(emitterName, emitterOptions ?? {}),
  );
}

function createLanguageMetadata(emitterName: string, emitterOptions: Record<string, unknown>): LanguagePackageMetadata {
  const normalizedOptions = normalizeOptionsObject(emitterOptions);
  return {
    emitterName,
    language: inferLanguageFromEmitterName(emitterName),
    packageName: extractOption(normalizedOptions, PACKAGE_NAME_KEYS),
    namespace: extractOption(normalizedOptions, NAMESPACE_KEYS),
    options: normalizedOptions,
  };
}

function createNamespaceMetadata(program: Program, namespaceType: Namespace): SpecNamespaceMetadata {
  const doc = trimOrUndefined(getDoc(program, namespaceType));
  const summary = trimOrUndefined(getSummary(program, namespaceType));
  const description = doc ?? summary;
  const fullName = trimOrUndefined(getNamespaceFullName(namespaceType)) ?? namespaceType.name ?? "(global)";
  const simpleName = namespaceType.name ?? fullName;

  return {
    name: simpleName || "(global)",
    fullName,
    description,
    documentation: doc,
  };
}

function normalizeOptionsObject(options: Record<string, unknown> | undefined): Record<string, unknown> {
  if (!options) {
    return {};
  }

  const normalized: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(options)) {
    normalized[key] = value;
  }
  return normalized;
}

function extractOption(options: Record<string, unknown>, candidates: string[]): string | undefined {
  for (const [key, value] of Object.entries(options)) {
    const normalizedKey = normalizeKey(key);
    for (const candidate of candidates) {
      if (normalizedKey === normalizeKey(candidate)) {
        if (value === undefined || value === null) {
          continue;
        }
        return String(value);
      }
    }
  }
  return undefined;
}

function normalizeKey(key: string): string {
  return key.replace(/[^a-z0-9]/gi, "").toLowerCase();
}

function inferLanguageFromEmitterName(emitterName: string): string {
  const normalized = emitterName.toLowerCase();
  if (LANGUAGE_ALIASES[normalized]) {
    return LANGUAGE_ALIASES[normalized];
  }

  const basename = normalized.split(/[\\/]/).pop() ?? normalized;
  const cadlIndex = basename.lastIndexOf("cadl-");
  if (cadlIndex >= 0) {
    const suffix = basename.substring(cadlIndex + "cadl-".length);
    if (suffix) {
      return suffix;
    }
  }

  const typespecIndex = basename.lastIndexOf("typespec-");
  if (typespecIndex >= 0) {
    const suffix = basename.substring(typespecIndex + "typespec-".length);
    if (suffix) {
      return suffix;
    }
  }

  const lastDash = basename.lastIndexOf("-");
  if (lastDash >= 0 && lastDash < basename.length - 1) {
    return basename.substring(lastDash + 1);
  }

  const sanitized = basename.replace(/[^a-z]/g, "");
  return sanitized || "unknown";
}

function trimOrUndefined(value: string | undefined): string | undefined {
  const trimmed = value?.trim();
  return trimmed && trimmed.length > 0 ? trimmed : undefined;
}

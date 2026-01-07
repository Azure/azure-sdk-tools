import { getDoc, getNamespaceFullName, getSummary, type Namespace, type Program } from "@typespec/compiler";
import { LanguagePackageMetadata, SpecMetadata, SpecNamespaceMetadata } from "./metadata.js";

const PACKAGE_NAME_KEYS = ["package-name", "packageName", "package"];
const NAMESPACE_KEYS = ["namespace", "namespace-name", "namespaceName"];

/**
 * Replace {var} placeholders in a string with values from the data object.
 * Recursively resolves nested placeholders.
 */
function fillVars(value: unknown, data: Record<string, unknown>): unknown {
  if (typeof value !== "string") {
    return value;
  }
  
  let prev: string | undefined;
  let current = value;
  
  while (prev !== current) {
    prev = current;
    current = current.replace(/\{([^{}]+)\}/g, (match, key) => {
      const replacement = data[key];
      return replacement !== undefined && replacement !== null ? String(replacement) : match;
    });
  }
  
  return current;
}

/**
 * Extract parameters from compiler options for variable substitution.
 */
function extractParameters(optionMap: Record<string, Record<string, unknown>>): Record<string, unknown> {
  const params: Record<string, unknown> = {};
  
  // Check if parameters are defined at the root level
  for (const [key, value] of Object.entries(optionMap)) {
    if (key === "parameters" && typeof value === "object" && value !== null) {
      for (const [paramKey, paramValue] of Object.entries(value)) {
        if (typeof paramValue === "object" && paramValue !== null && "default" in paramValue) {
          params[paramKey] = (paramValue as any).default;
        } else {
          params[paramKey] = paramValue;
        }
      }
    }
  }
  
  return params;
}

const LANGUAGE_ALIASES: Record<string, string> = {
  "@azure-tools/typespec-csharp": "csharp",
  "@azure-tools/typespec-java": "java",
  "@azure-tools/typespec-python": "python",
  "@azure-tools/typespec-typescript": "typescript",
  "@azure-tools/typespec-ts": "typescript",
  "@azure-tools/typespec-js": "javascript",
  "@azure-tools/typespec-swift": "swift",
  "@azure-tools/typespec-go": "go",
  "@azure-tools/typespec-rust": "rust",
};

interface LanguageParserResult {
  packageName?: string;
  namespace?: string;
}

type LanguageParser = (options: Record<string, unknown>, params: Record<string, unknown>) => LanguageParserResult;

/**
 * Python-specific metadata parser.
 * Handles package-name and namespace, with fallback conversion between them.
 * Strips implementation-specific suffixes like ._generated from namespaces.
 */
function parsePython(options: Record<string, unknown>, params: Record<string, unknown>): LanguageParserResult {
  let packageName = options["package-name"] ?? options["package_name"];
  let namespace = options["namespace"];
  
  // Strip implementation-specific suffixes from namespace
  if (namespace) {
    namespace = String(namespace).replace(/\._generated$/, "");
  }
  
  // Fallback: derive namespace from package-name
  if (packageName && !namespace) {
    namespace = String(packageName).replace(/-/g, ".");
  } else if (namespace && !packageName) {
    packageName = String(namespace).replace(/\./g, "-");
  }
  
  return {
    packageName: packageName ? String(fillVars(packageName, params)) : undefined,
    namespace: namespace ? String(fillVars(namespace, params)) : undefined,
  };
}

/**
 * Java-specific metadata parser.
 * Strips 'com.' prefix from namespace if present for package name derivation.
 */
function parseJava(options: Record<string, unknown>, params: Record<string, unknown>): LanguageParserResult {
  let packageName = options["package-name"] ?? options["package_name"];
  let namespace = options["namespace"];
  
  if (namespace && !packageName) {
    const ns = String(namespace);
    const stripped = ns.startsWith("com.") ? ns.substring(4) : ns;
    packageName = stripped.replace(/\./g, "-");
  }
  
  return {
    packageName: packageName ? String(fillVars(packageName, params)) : undefined,
    namespace: namespace ? String(fillVars(namespace, params)) : undefined,
  };
}

/**
 * C#-specific metadata parser.
 * Package name and namespace are typically identical.
 */
function parseCSharp(options: Record<string, unknown>, params: Record<string, unknown>): LanguageParserResult {
  let packageName = options["package-name"] ?? options["package_name"];
  let namespace = options["namespace"];
  
  if (packageName && !namespace) {
    namespace = packageName;
  } else if (namespace && !packageName) {
    packageName = namespace;
  }
  
  return {
    packageName: packageName ? String(fillVars(packageName, params)) : undefined,
    namespace: namespace ? String(fillVars(namespace, params)) : undefined,
  };
}

/**
 * TypeScript-specific metadata parser.
 * Extracts from package-details if available.
 */
function parseTypeScript(options: Record<string, unknown>, params: Record<string, unknown>): LanguageParserResult {
  const packageDetails = options["package-details"];
  let packageName: unknown;
  let namespace: unknown;
  
  if (packageDetails && typeof packageDetails === "object") {
    const details = packageDetails as Record<string, unknown>;
    packageName = details["name"];
    namespace = details["namespace"];
  }
  
  if (packageName && !namespace) {
    namespace = packageName;
  } else if (namespace && !packageName) {
    packageName = namespace;
  }
  
  return {
    packageName: packageName ? String(fillVars(packageName, params)) : undefined,
    namespace: namespace ? String(fillVars(namespace, params)) : undefined,
  };
}

/**
 * Go-specific metadata parser.
 * Extracts from module path, looking for azure-sdk-for-go suffix.
 */
function parseGo(options: Record<string, unknown>, params: Record<string, unknown>): LanguageParserResult {
  let packageName = options["package-name"] ?? options["package_name"];
  let namespace = options["namespace"];
  const module = options["module"];
  
  if (module) {
    const modulePath = String(fillVars(module, params));
    const sdkIndex = modulePath.indexOf("azure-sdk-for-go/");
    const after = sdkIndex >= 0 ? modulePath.substring(sdkIndex + "azure-sdk-for-go/".length) : modulePath;
    
    if (!packageName) {
      packageName = after;
    }
    if (!namespace) {
      namespace = after;
    }
  }
  
  return {
    packageName: packageName ? String(fillVars(packageName, params)) : undefined,
    namespace: namespace ? String(fillVars(namespace, params)) : undefined,
  };
}

/**
 * Rust-specific metadata parser.
 * Uses crate-name as the primary identifier.
 */
function parseRust(options: Record<string, unknown>, params: Record<string, unknown>): LanguageParserResult {
  let packageName = options["crate-name"];
  let namespace = options["namespace"];
  
  if (packageName && !namespace) {
    namespace = packageName;
  } else if (namespace && !packageName) {
    packageName = namespace;
  }
  
  return {
    packageName: packageName ? String(fillVars(packageName, params)) : undefined,
    namespace: namespace ? String(fillVars(namespace, params)) : undefined,
  };
}

/**
 * Map of language-specific parsers.
 */
const LANGUAGE_PARSERS: Record<string, LanguageParser> = {
  "@azure-tools/typespec-python": parsePython,
  "@azure-tools/typespec-java": parseJava,
  "@azure-tools/typespec-csharp": parseCSharp,
  "@azure-tools/typespec-typescript": parseTypeScript,
  "@azure-tools/typespec-ts": parseTypeScript,
  "@azure-tools/typespec-go": parseGo,
  "@azure-tools/typespec-rust": parseRust,
};

export interface LanguageCollectionResult {
  languages: Record<string, LanguagePackageMetadata>;
  sourceConfigPath?: string;
}

export async function collectLanguagePackages(program: Program, baseOutputDir: string): Promise<LanguageCollectionResult> {
  const optionMap = program.compilerOptions.options ?? {};
  const params = extractParameters(optionMap);
  
  // Extract default service-dir from config file parameters
  let defaultServiceDir: string | undefined;
  const configFile = (program.compilerOptions as any).configFile;
  if (configFile?.parameters && 'service-dir' in configFile.parameters) {
    const serviceDirParam = configFile.parameters['service-dir'];
    if (serviceDirParam && typeof serviceDirParam === 'object' && 'default' in serviceDirParam) {
      defaultServiceDir = String(serviceDirParam.default);
    }
  }
  
  return {
    languages: buildLanguageMetadata(optionMap, params, baseOutputDir, defaultServiceDir),
    sourceConfigPath: program.compilerOptions.config,
  };
}

export function buildSpecMetadata(program: Program): SpecMetadata {
  const globalNamespace = program.getGlobalNamespaceType();
  const discoveredNamespaces = Array.from(globalNamespace.namespaces.values())
    .filter((ns) => isServiceNamespace(ns))
    .map((ns) => createNamespaceMetadata(program, ns));

  if (discoveredNamespaces.length === 0) {
    discoveredNamespaces.push(createNamespaceMetadata(program, globalNamespace));
  }

  return {
    namespaces: discoveredNamespaces,
    summary: discoveredNamespaces.find((ns) => !!ns.summary)?.summary,
  };
}

/**
 * Determines if a namespace is a service namespace (not a framework/standard namespace).
 * Filters out standard TypeSpec and Azure framework namespaces.
 */
function isServiceNamespace(ns: Namespace): boolean {
  const fullName = getNamespaceFullName(ns);
  const name = ns.name;
  
  // Filter out standard framework namespaces by exact name match
  const excludedNamespaces = [
    "TypeSpec",
    "Azure",
  ];
  
  // Exclude if it's exactly one of the framework namespaces (not a child namespace)
  if (excludedNamespaces.includes(fullName)) {
    return false;
  }
  
  // Also exclude if it's a simple name match (top-level only)
  if (excludedNamespaces.includes(name) && !fullName.includes(".")) {
    return false;
  }
  
  return true;
}

function buildLanguageMetadata(
  optionMap: Record<string, Record<string, unknown>>,
  params: Record<string, unknown>,
  baseOutputDir: string,
  defaultServiceDir?: string,
): Record<string, LanguagePackageMetadata> {
  // Only include SDK language emitters
  const includedEmitters = [
    "@azure-tools/typespec-python",
    "@azure-tools/typespec-java",
    "@azure-tools/typespec-csharp",
    "@azure-tools/typespec-ts",
    "@azure-tools/typespec-go",
    "@azure-tools/typespec-rust",
    "@azure-tools/typespec-js",
    "@azure-tools/typespec-swift",
  ];
  
  const languagesDict: Record<string, LanguagePackageMetadata> = {};
  
  for (const [emitterName, emitterOptions] of Object.entries(optionMap)) {
    if (includedEmitters.includes(emitterName)) {
      const metadata = createLanguageMetadata(emitterName, emitterOptions ?? {}, params, baseOutputDir, defaultServiceDir);
      const language = inferLanguageFromEmitterName(emitterName);
      languagesDict[language] = metadata;
    }
  }
  
  return languagesDict;
}

function createLanguageMetadata(
  emitterName: string,
  emitterOptions: Record<string, unknown>,
  params: Record<string, unknown>,
  baseOutputDir: string,
  defaultServiceDir?: string,
): LanguagePackageMetadata {
  const normalizedOptions = normalizeOptionsObject(emitterOptions);
  
  // Extract common options
  const outputDirRaw = normalizedOptions["emitter-output-dir"] ?? normalizedOptions["emitterOutputDir"];
  const flavor = normalizedOptions["flavor"];
  const languageServiceDir = normalizedOptions["service-dir"];
  
  // Use language-specific service-dir if present, otherwise use default
  const serviceDir = languageServiceDir ? String(languageServiceDir) : defaultServiceDir;
  
  // Use language-specific parser if available
  let packageName: string | undefined;
  let namespace: string | undefined;
  
  const normalizedEmitterName = emitterName.toLowerCase();
  const parser = LANGUAGE_PARSERS[normalizedEmitterName];
  
  if (parser) {
    const result = parser(normalizedOptions, params);
    packageName = result.packageName;
    namespace = result.namespace;
  } else {
    // Fallback to generic extraction
    packageName = extractOption(normalizedOptions, PACKAGE_NAME_KEYS);
    namespace = extractOption(normalizedOptions, NAMESPACE_KEYS);
  }
  
  // Convert outputDir to use {output-dir} placeholder
  let relativeOutputDir: string | undefined;
  if (outputDirRaw) {
    const absolutePath = String(outputDirRaw);
    const normalizedBase = baseOutputDir.replace(/\\/g, '/').replace(/\/$/, '');
    const normalizedPath = absolutePath.replace(/\\/g, '/');
    
    // Replace the base output directory with {output-dir} placeholder
    if (normalizedPath.startsWith(normalizedBase + '/')) {
      const relativePart = normalizedPath.substring(normalizedBase.length + 1);
      relativeOutputDir = `{output-dir}/${relativePart}`;
    } else if (normalizedPath === normalizedBase) {
      relativeOutputDir = '{output-dir}';
    } else {
      // If it doesn't start with base, keep as-is
      relativeOutputDir = absolutePath;
    }
  }
  
  return {
    emitterName,
    packageName,
    namespace,
    outputDir: relativeOutputDir,
    flavor: flavor ? String(flavor) : undefined,
    serviceDir,
  };
}

function createNamespaceMetadata(program: Program, namespaceType: Namespace): SpecNamespaceMetadata {
  const doc = trimOrUndefined(getDoc(program, namespaceType));
  const summary = trimOrUndefined(getSummary(program, namespaceType));
  const name = trimOrUndefined(getNamespaceFullName(namespaceType)) ?? namespaceType.name ?? "(global)";

  return {
    name,
    summary,
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

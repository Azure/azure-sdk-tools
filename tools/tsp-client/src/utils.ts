import { joinPaths, normalizeSlashes } from "@typespec/compiler";
import { randomUUID } from "node:crypto";
import { access, constants, mkdir, writeFile, readFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { Logger } from "./log.js";
import { TspLocation } from "./typespec.js";
import { normalizeDirectory } from "./fs.js";
import { parse as parseYaml } from "yaml";
import { resolvePath } from "@typespec/compiler";
import { downloadFile } from "./network.js";
import { resolveTspConfigUrl } from "./typespec.js";

export function formatAdditionalDirectories(additionalDirectories?: string[]): string {
  let additionalDirOutput = "\n";
  for (const dir of additionalDirectories ?? []) {
    additionalDirOutput += `- ${normalizeDirectory(dir)}\n`;
  }
  return additionalDirOutput;
}

export function getAdditionalDirectoryName(dir: string): string {
  let normalizedDir = normalizeSlashes(dir);
  if (normalizedDir.slice(-1) === "/") {
    normalizedDir = normalizedDir.slice(0, -1);
  }
  const finalDirName = normalizedDir.split("/").pop();
  if (!finalDirName) {
    throw new Error(`Could not find a final directory for the following value: ${normalizedDir}`);
  }
  return finalDirName;
}

export async function makeSparseSpecDir(repoRoot: string): Promise<string> {
  const spareSpecPath = joinPaths(repoRoot, "..", `sparse-spec${randomUUID()}`);
  await mkdir(spareSpecPath, { recursive: true });
  return spareSpecPath;
}

export function getServiceDir(configYaml: any, emitter: string): string {
  // Check if service-dir is defined in the emitter specific configurations in tspconfig.yaml.
  // Default to the top level service-dir parameter in tspconfig.yaml.
  const serviceDir =
    configYaml?.options?.[emitter]?.["service-dir"] ??
    configYaml?.parameters?.["service-dir"]?.default;
  if (!serviceDir) {
    throw new Error(
      `Parameter service-dir is not defined correctly in tspconfig.yaml. Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.`,
    );
  }
  Logger.debug(`Service directory: ${serviceDir}`);
  return serviceDir;
}

/**
 * Deep merge tspconfig configurations with child overriding parent (iterative, non-recursive)
 */
export function deepMergeConfigs(parent: any, child: any): any {
  if (!parent) return child;
  if (!child) return parent;

  const result = { ...parent };

  for (const key in child) {
    if (child[key] === null || child[key] === undefined) {
      continue;
    }

    if (Array.isArray(child[key])) {
      // Arrays are replaced, not merged (child completely overrides parent)
      result[key] = child[key];
    } else if (typeof child[key] === 'object' && !Array.isArray(child[key])) {
      // Deep merge objects using iterative approach with stack
      result[key] = result[key] || {};
      
      // Create a deep copy of the parent object at this key to preserve existing values
      if (parent[key] && typeof parent[key] === 'object' && !Array.isArray(parent[key])) {
        result[key] = JSON.parse(JSON.stringify(parent[key]));
      }
      
      // Use a stack to iteratively merge nested objects
      const mergeStack: Array<{ 
        sourceObj: any; 
        targetObj: any; 
      }> = [{ sourceObj: child[key], targetObj: result[key] }];

      while (mergeStack.length > 0) {
        const { sourceObj, targetObj } = mergeStack.pop()!;
        
        for (const nestedKey in sourceObj) {
          if (sourceObj[nestedKey] === null || sourceObj[nestedKey] === undefined) {
            continue;
          }
          
          if (typeof sourceObj[nestedKey] === 'object' && !Array.isArray(sourceObj[nestedKey])) {
            // Nested object - ensure target exists and add to stack for processing
            targetObj[nestedKey] = targetObj[nestedKey] || {};
            mergeStack.push({
              sourceObj: sourceObj[nestedKey],
              targetObj: targetObj[nestedKey]
            });
          } else {
            // Primitive value or array - direct assignment (child overrides parent)
            targetObj[nestedKey] = sourceObj[nestedKey];
          }
        }
      }
    } else {
      // Replace primitive values and arrays
      result[key] = child[key];
    }
  }

  return result;
}

/**
 * Resolves parent tspconfig URLs relative to child URL
 */
export function buildParentConfigUrl(
  childConfigUrl: string, 
  extendsPath: string
): { resolvedUrl: string; commit: string; repo: string; path: string } {
  
  // Parse child URL
  const childParsed = resolveTspConfigUrl(childConfigUrl);
  
  // Resolve relative path
  const childDir = dirname(childParsed.path);
  const parentPath = joinPaths(childDir, extendsPath).replace(/\\/g, '/');
  
  // Construct parent URL
  const parentUrl = `https://raw.githubusercontent.com/${childParsed.repo}/${childParsed.commit}/${parentPath}`;
  
  return {
    resolvedUrl: parentUrl,
    commit: childParsed.commit,
    repo: childParsed.repo,
    path: dirname(parentPath) // Directory containing the parent config
  };
}

/**
 * Config loader interface for unified inheritance resolution
 */
export interface ConfigLoader {
  loadConfig(path: string): Promise<{ config: any; metadata: any }>;
  resolveParentPath(currentPath: string, extendsPath: string): string;
  normalizeKey(path: string): string;
  isAbsolutePath(path: string): boolean;
}

/**
 * Single entry point for loading tspconfig with inheritance (iterative with single inheritance)
 */

/**
 * Resolve additional directory path to absolute path based on config location
 */
function resolveAdditionalDirectoryPath(dirPath: string, configMetadata: any): string {
  // If it's already an absolute path, return as-is
  if (dirPath.startsWith('/') || dirPath.includes('://')) {
    return dirPath;
  }
  
  // Relative path - resolve based on config location
  if (typeof configMetadata === 'string') {
    // Local config - metadata is absolute file path
    // Resolve to absolute filesystem path
    const configDir = dirname(configMetadata);
    return resolvePath(configDir, dirPath);
  } else if (configMetadata && typeof configMetadata === 'object') {
    // Remote config - metadata has directory, repo, commit structure  
    // Keep paths relative to repository root for remote configs
    const { directory } = configMetadata as { directory: string; repo: string; commit: string };
    const configDir = directory;
    
    // For remote configs, resolve relative to the config directory but keep it repo-relative
    if (dirPath.startsWith('./')) {
      // Remove leading './' and resolve relative to config directory
      const relativePath = dirPath.substring(2);
      return joinPaths(configDir, relativePath).replace(/\\/g, '/');
    } else {
      // Direct relative path
      return joinPaths(configDir, dirPath).replace(/\\/g, '/');
    }
  }
  
  // Fallback - return as-is if we can't determine the context
  return dirPath;
}

/**
 * Loads and merges TypeSpec configuration with inheritance support
 */
export async function loadTspConfig<T>(
  initialPath: string,
  loader: ConfigLoader
): Promise<{ config: any; rootMetadata: T }> {
  
  const visitedPaths = new Set<string>();
  const configChain: Array<{ config: any; metadata: any }> = [];
  let rootMetadata: T | null = null;
  
  // Phase 1: Build the inheritance chain using iterative traversal
  let currentPath: string | null = initialPath;
  while (currentPath) {
    // Prevent circular references
    const normalizedPath = loader.normalizeKey(currentPath);
    if (visitedPaths.has(normalizedPath)) {
      throw new Error(`Circular inheritance detected: ${normalizedPath} is already in the inheritance chain`);
    }
    visitedPaths.add(normalizedPath);

    // Load current config
    const { config, metadata } = await loader.loadConfig(currentPath);
    configChain.push({ config, metadata });
    
    // Validate single inheritance constraint
    if (config.extends) {
      if (Array.isArray(config.extends)) {
        throw new Error(`Multiple inheritance not supported. Config at ${currentPath} extends multiple parents: ${config.extends.join(', ')}`);
      }
      
      // Move to parent config
      currentPath = loader.resolveParentPath(currentPath, config.extends);
    } else {
      // This is a root config - store its metadata and stop
      rootMetadata = metadata as T;
      currentPath = null;
    }
  }
  
  // Phase 2: Merge configs from root (last) to child (first)
  let mergedConfig = {};
  
  // Reverse the chain to start from root
  for (let i = configChain.length - 1; i >= 0; i--) {
    const configEntry = configChain[i];
    if (configEntry) {
      const configToMerge = { ...configEntry.config };
      delete configToMerge.extends; // Remove extends field
      
      // Resolve additionalDirectories to absolute paths based on this config's location
      if (configToMerge.options?.["@azure-tools/typespec-client-generator-cli"]?.additionalDirectories) {
        const additionalDirs = configToMerge.options["@azure-tools/typespec-client-generator-cli"].additionalDirectories;
        if (Array.isArray(additionalDirs)) {
          const resolvedDirs = additionalDirs.map(dir => {
            if (typeof dir === 'string') {
              return resolveAdditionalDirectoryPath(dir, configEntry.metadata);
            }
            return dir;
          });
          configToMerge.options["@azure-tools/typespec-client-generator-cli"].additionalDirectories = resolvedDirs;
        }
      }
      
      mergedConfig = deepMergeConfigs(mergedConfig, configToMerge);
    }
  }

  if (!rootMetadata) {
    throw new Error("No root configuration found");
  }

  return { config: mergedConfig, rootMetadata };
}

/**
 * Creates a remote config loader for GitHub URLs
 */
export function createRemoteConfigLoader(): ConfigLoader {
  return {
    async loadConfig(url: string) {
      const configInfo = resolveTspConfigUrl(url);
      const configData = await downloadFile(configInfo.resolvedUrl);
      const config = parseYaml(configData);
      
      const metadata = {
        url: url,
        directory: configInfo.path,
        commit: configInfo.commit,
        repo: configInfo.repo
      };
      
      return { config, metadata };
    },
    
    resolveParentPath(currentUrl: string, extendsPath: string): string {
      // Handle different types of extends paths
      if (this.isAbsolutePath(extendsPath)) {
        if (extendsPath.startsWith('http://') || extendsPath.startsWith('https://')) {
          // Full URL - use as-is
          return extendsPath;
        } else {
          // Absolute path: starts with "/" - treat as absolute within the same repo
          const currentConfigInfo = resolveTspConfigUrl(currentUrl);
          return `https://raw.githubusercontent.com/${currentConfigInfo.repo}/${currentConfigInfo.commit}${extendsPath}`;
        }
      } else {
        // Relative path - resolve relative to current config's directory
        const parentConfigInfo = buildParentConfigUrl(currentUrl, extendsPath);
        return parentConfigInfo.resolvedUrl;
      }
    },
    
    normalizeKey(url: string): string {
      return url; // URLs are already normalized
    },
    
    isAbsolutePath(path: string): boolean {
      return path.startsWith('/') || path.startsWith('http://') || path.startsWith('https://');
    }
  };
}

/**
 * Creates a local config loader for file system paths
 */
export function createLocalConfigLoader(): ConfigLoader {
  return {
    async loadConfig(path: string) {
      const configData = await readFile(path, "utf8");
      const config = parseYaml(configData);
      const absolutePath = resolvePath(path);
      
      return { config, metadata: absolutePath };
    },
    
    resolveParentPath(currentPath: string, extendsPath: string): string {
      // Handle different types of extends paths
      if (this.isAbsolutePath(extendsPath)) {
        // Absolute path - use as-is (already absolute)
        return extendsPath;
      } else {
        // Relative path - resolve relative to current config's directory
        return resolvePath(dirname(currentPath), extendsPath);
      }
    },
    
    normalizeKey(path: string): string {
      return resolvePath(path); // Normalize to absolute path
    },
    
    isAbsolutePath(path: string): boolean {
      // Check if it's an absolute file path or URL
      return resolvePath(path) === path || path.startsWith('http://') || path.startsWith('https://');
    }
  };
}

/**
 * Extract specification path from root config path for directory calculation
 */
export function extractSpecificationPath(rootConfigPath: string): string {
  const normalizedPath = rootConfigPath.replaceAll("\\", "/");
  const matchRes = normalizedPath.match(".*/(?<path>specification/.*)/tspconfig.yaml$");
  
  if (matchRes?.groups?.["path"]) {
    return matchRes.groups["path"];
  }
  
  // Fallback: use directory containing the root config
  return dirname(normalizedPath);
}

/**
 * Returns path to a dependency package under node_modules
 *
 * @param dependency Name of dependency.
 *
 * @example
 * ```
 * // Prints '/home/user/foo/node_modules/@autorest/bar':
 * console.log(getPathToDependency("@autorest/bar"));
 * ```
 */
export async function getPathToDependency(dependency: string): Promise<string> {
  // Example: /home/user/foo/node_modules/@autorest/bar/dist/index.js
  const entrypoint = fileURLToPath(import.meta.resolve(dependency));

  // Walk up directory tree to first folder containing "package.json"
  let currentDir = dirname(entrypoint);
  while (true) {
    const packageJsonFile = join(currentDir, "package.json");
    try {
      // Throws if file cannot be read
      await access(packageJsonFile, constants.R_OK);
      return currentDir;
    } catch {
      const parentDir = dirname(currentDir);
      if (parentDir !== currentDir) {
        currentDir = parentDir;
      } else {
        // Reached fs root but no package.json found
        throw new Error(`Unable to find package.json in folder tree above '${entrypoint}'`);
      }
    }
  }
}

/**
 * Writes tsp-location.yaml file at the given projectPath. Ensures additional directories are formatted correctly.
 *
 * @param tspLocation TspLocation object containing tsp location information.
 * @param projectPath Path to the project.
 */
export async function writeTspLocationYaml(
  tspLocation: TspLocation,
  projectPath: string,
): Promise<void> {
  let tspLocationContent = `directory: ${tspLocation.directory}\ncommit: ${tspLocation.commit}\nrepo: ${tspLocation.repo}\nadditionalDirectories: ${formatAdditionalDirectories(tspLocation.additionalDirectories)}`;
  if (tspLocation.entrypointFile) {
    tspLocationContent += `\nentrypointFile: ${tspLocation.entrypointFile}`;
  }
  if (tspLocation.emitterPackageJsonPath) {
    tspLocationContent += `\nemitterPackageJsonPath: ${tspLocation.emitterPackageJsonPath}`;
  }
  await writeFile(joinPaths(projectPath, "tsp-location.yaml"), tspLocationContent);
}

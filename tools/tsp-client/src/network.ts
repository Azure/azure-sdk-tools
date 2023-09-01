import { createDefaultHttpClient, createPipelineRequest } from "@azure/core-rest-pipeline";
import { createFileTree } from "./fileTree.js";
import { resolveImports } from "./typespec.js";
import { Logger } from "./log.js";

const httpClient = createDefaultHttpClient();

export async function fetch(url: string, method: "GET" | "HEAD" = "GET"): Promise<string> {
  const result = await httpClient.sendRequest(createPipelineRequest({ url, method }));
  if (result.status !== 200) {
    throw new Error(`failed to fetch ${url}: ${result.status}`);
  }
  return String(result.bodyAsText);
}

export function isValidUrl(url: string) {
  try {
    new URL(url);
    return true;
  } catch {
    return false;
  }
}

export function doesFileExist(url: string): Promise<boolean> {
  return fetch(url, "HEAD")
    .then(() => true)
    .catch(() => false);
}

export function rewriteGitHubUrl(url: string): string {
  if (url.includes("github.com")) {
    const result = url.replace("github.com", "raw.githubusercontent.com");
    Logger.debug(`rewriting github url to direct link: ${result}`);
    return result;
  }
  return url;
}

export async function downloadTsp(rootUrl: string) {
  const seenFiles = new Set<string>();
  const moduleImports = new Set<string>();
  // fetch the root file
  const filesToProcess = [rootUrl];
  seenFiles.add(rootUrl);
  const fileTree = createFileTree(rootUrl);

  while (filesToProcess.length > 0) {
    const url = filesToProcess.shift()!;
    const sourceFile = await fetch(url);
    fileTree.addFile(url, sourceFile);
    // process imports, fetching any relatively referenced files
    const imports = await resolveImports(sourceFile);
    for (const fileImport of imports) {
      // Check if the module name is referencing a path(./foo, /foo, file:/foo)
      if (/^(?:\.\.?(?:\/|$)|\/|([A-Za-z]:)?[/\\])/.test(fileImport)) {
        if (fileImport.startsWith("file:")) {
          throw new Error(`file protocol imports are not supported: ${fileImport}`);
        }
        let resolvedImport: string;
        if (fileImport.startsWith("http:")) {
          throw new Error(`absolute url imports are not supported: ${fileImport}`);
        } else {
          resolvedImport = new URL(fileImport, url).toString();
        }
        if (!seenFiles.has(resolvedImport)) {
          Logger.debug(`discovered import ${resolvedImport}`);
          filesToProcess.push(resolvedImport);
          seenFiles.add(resolvedImport);
        }
      } else {
        Logger.debug(`discovered module import ${fileImport}`);
        moduleImports.add(fileImport);
      }
    }
  }

  // look for a tspconfig.yaml next to the root
  try {
    const tspConfigUrl = new URL("tspconfig.yaml", rootUrl).toString();
    const tspConfig = await fetch(tspConfigUrl);
    Logger.debug("found tspconfig.yaml");
    fileTree.addFile(tspConfigUrl, tspConfig);
  } catch (e) {
    Logger.debug("no tspconfig.yaml found");
  }

  return {
    moduleImports,
    fileTree,
  };
}

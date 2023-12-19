import { resolvePath, getDirectoryPath, ResolveCompilerOptionsOptions } from "@typespec/compiler";
import { ModuleResolutionResult, resolveModule, ResolveModuleHost } from "@typespec/compiler/module-resolver";
import { Logger } from "./log.js";
import { readFile, readdir, realpath, stat } from "fs/promises";
import { pathToFileURL } from "url";


export interface TspLocation {
  directory: string;
  commit: string;
  repo: string;
  additionalDirectories?: string[];
}

export function resolveTspConfigUrl(configUrl: string): {
  resolvedUrl: string;
  commit: string;
  repo: string;
  path: string;
} {
  let resolvedConfigUrl = configUrl;

  const res = configUrl.match('^https://(?<urlRoot>github|raw.githubusercontent).com/(?<repo>[^/]*/azure-rest-api-specs(-pr)?)/(tree/|blob/)?(?<commit>[0-9a-f]{40})/(?<path>.*)/tspconfig.yaml$')
  if (res && res.groups) {
    if (res.groups["urlRoot"]! === "github") {
      resolvedConfigUrl = configUrl.replace("github.com", "raw.githubusercontent.com");
      resolvedConfigUrl = resolvedConfigUrl.replace("/blob/", "/");
    }
    return {
      resolvedUrl: resolvedConfigUrl,
      commit: res.groups!["commit"]!,
      repo: res.groups!["repo"]!,
      path: res.groups!["path"]!,
    }
  } else {
    throw new Error(`Invalid tspconfig.yaml url: ${configUrl}`);
  }
}


export async function discoverMainFile(srcDir: string): Promise<string> {
  Logger.debug(`Discovering entry file in ${srcDir}`)
  let entryTsp = "";
  const files = await readdir(srcDir, {recursive: true });
  for (const file of files) {
    if (file.includes("client.tsp") || file.includes("main.tsp")) {
      entryTsp = file;
      Logger.debug(`Found entry file: ${entryTsp}`);
      return entryTsp;
    }
  };
  throw new Error(`No main.tsp or client.tsp found`);
}

export async function compileTsp({
  emitterPackage,
  outputPath,
  resolvedMainFilePath,
  additionalEmitterOptions,
  saveInputs,
}: {
  emitterPackage: string;
  outputPath: string;
  resolvedMainFilePath: string;
  additionalEmitterOptions?: string;
  saveInputs?: boolean;
}) {
  const parsedEntrypoint = getDirectoryPath(resolvedMainFilePath);
  const { compile, NodeHost, getSourceLocation, resolveCompilerOptions } = await importTsp(parsedEntrypoint);

  const outputDir = resolvePath(outputPath);
  const overrideOptions: Record<string, Record<string, string>> = {
    [emitterPackage]: {
      "emitter-output-dir": outputDir,
    },
  };
  const emitterOverrideOptions = overrideOptions[emitterPackage] ?? {[emitterPackage]: {}};
  if (saveInputs) {
    emitterOverrideOptions["save-inputs"] = "true";
  }
  if (additionalEmitterOptions) {
    additionalEmitterOptions.split(";").forEach((option) => {
      const [key, value] = option.split("=");
      if (key && value) {
        emitterOverrideOptions[key] = value;
      }
    });
  }
  const overrides: Partial<ResolveCompilerOptionsOptions["overrides"]> = {
    outputDir,
    emit: [emitterPackage],
    options: overrideOptions,
  };
  Logger.info(`Compiling tsp using ${emitterPackage}...`);
  const [options, diagnostics] = await resolveCompilerOptions(NodeHost, {
    cwd: process.cwd(),
    entrypoint: resolvedMainFilePath,
    overrides,
  });
  Logger.debug(`Compiler options: ${JSON.stringify(options)}`);
  if (diagnostics.length > 0) {
    // This should not happen, but if it does, we should log it.
    Logger.debug(`Compiler options diagnostic information: ${JSON.stringify(diagnostics)}`);
  }

  const program = await compile(NodeHost, resolvedMainFilePath, options);

  if (program.diagnostics.length > 0) {
    for (const diagnostic of program.diagnostics) {
      const location = getSourceLocation(diagnostic.target);
      const source = location ? location.file.path : "unknown";
      console.error(
        `${diagnostic.severity}: ${diagnostic.code} - ${diagnostic.message} @ ${source}`,
      );
    }
  } else {
    Logger.success("generation complete");
  }
}

export async function importTsp(baseDir: string): Promise<typeof import("@typespec/compiler")> {
  try {
    const host: ResolveModuleHost = {
      realpath,
      readFile: async (path: string) => await readFile(path, "utf-8"),
      stat,
    };
    const resolved: ModuleResolutionResult = await resolveModule(host, "@typespec/compiler", {
      baseDir,
    });

    Logger.info(`Resolved path: ${resolved.path}`);

    if (resolved.type === "module") {
      return import(pathToFileURL(resolved.mainFile).toString());
    }
    return import(pathToFileURL(resolved.path).toString());
  } catch (err: any) {
    if (err.code === "MODULE_NOT_FOUND") {
      // Resolution from cwd failed: use current package.
      return import("@typespec/compiler");
    } else {
      throw err;
    }
  }
}

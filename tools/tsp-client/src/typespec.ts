import { resolvePath, getDirectoryPath, ResolveCompilerOptionsOptions } from "@typespec/compiler";
import {
  ModuleResolutionResult,
  resolveModule,
  ResolveModuleHost,
} from "@typespec/compiler/module-resolver";
import { Logger } from "./log.js";
import { readFile, readdir, realpath, stat } from "fs/promises";
import { pathToFileURL } from "url";

export interface TspLocation {
  directory?: string;
  commit?: string;
  repo?: string;
  additionalDirectories?: string[];
  entrypointFile?: string;
  emitterPackageJsonPath?: string;
  batch?: string[];
}

export function resolveTspConfigUrl(configUrl: string): {
  resolvedUrl: string;
  commit: string;
  repo: string;
  path: string;
} {
  let resolvedConfigUrl = configUrl;

  const res = configUrl.match(
    "^https://(?<urlRoot>github|raw.githubusercontent).com/(?<repo>[^/]*/azure-rest-api-specs(-pr)?)/(tree/|blob/)?(?<commit>[0-9a-f]{40})/(?<path>.*)/tspconfig.yaml$",
  );
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
    };
  } else {
    throw new Error(`Invalid tspconfig.yaml url: ${configUrl}`);
  }
}

export async function discoverEntrypointFile(
  srcDir: string,
  specifiedEntrypointFile?: string,
): Promise<string> {
  Logger.debug(`Discovering entry file in ${srcDir}`);
  let entryTsp: string | undefined = undefined;
  const files = await readdir(srcDir, { recursive: true });

  function findEntrypoint(name: string): string | undefined {
    return files.find((file) => file === name) ?? undefined;
  }
  if (specifiedEntrypointFile) {
    entryTsp = findEntrypoint(specifiedEntrypointFile);
    if (!entryTsp) {
      throw new Error(
        `Couldn't find the entrypoint file specified in tsp-location.yaml: "${specifiedEntrypointFile}". Please verify that the entrypoint file name is correct.`,
      );
    }
  } else {
    entryTsp = findEntrypoint("client.tsp") ?? findEntrypoint("main.tsp");
    if (!entryTsp) {
      throw new Error(`No main.tsp or client.tsp found`);
    }
  }
  Logger.debug(`Found entry file: ${entryTsp}`);
  return entryTsp;
}

export function tryParseEmitterOptionAsObject(value: string): object | string {
  try {
    const obj = JSON.parse(value);
    if (typeof obj === "object" && obj !== null && !Array.isArray(obj)) {
      return obj;
    }
  } catch {
    // no-op
  }
  return value;
}

export async function compileTsp({
  emitterPackage,
  outputPath,
  resolvedMainFilePath,
  additionalEmitterOptions,
  saveInputs,
  trace,
  legacyPathResolution,
}: {
  emitterPackage: string;
  outputPath: string;
  resolvedMainFilePath: string;
  additionalEmitterOptions?: string;
  saveInputs?: boolean;
  trace?: string[];
  legacyPathResolution?: boolean;
}): Promise<{ success: boolean; exampleCmd: string }> {
  const parsedEntrypoint = getDirectoryPath(resolvedMainFilePath);
  const { compile, NodeHost, resolveCompilerOptions, formatDiagnostic } =
    await importTsp(parsedEntrypoint);

  const outputDir = resolvePath(outputPath);
  const emitterOverrideOptions: Record<string, any> = {};
  if (additionalEmitterOptions) {
    additionalEmitterOptions.split(";").forEach((option) => {
      const [key, value] = option.split("=");
      if (key && value !== undefined) {
        emitterOverrideOptions[key] = tryParseEmitterOptionAsObject(value);
      }
    });
  }
  const overrideOptions: Record<string, Record<string, string>> = {
    [emitterPackage]: {
      ...emitterOverrideOptions,
    },
  };

  if (saveInputs) {
    // Only override save-inputs if the user explicitly passed --save-inputs to tsp-client.
    // If the user did not pass --save-inputs, we do not override it here, and leave save-inputs
    // as it was specified in tspconfig.yaml (or not specified at all).
    overrideOptions[emitterPackage]!["save-inputs"] = "true";
    Logger.debug(
      "The save-inputs option will be set to true for the emitter options at compile time.",
    );
  }

  if (legacyPathResolution) {
    overrideOptions[emitterPackage]!["emitter-output-dir"] = outputDir;
  }
  const overrides: Partial<ResolveCompilerOptionsOptions["overrides"]> = {
    outputDir,
    emit: [emitterPackage],
    options: overrideOptions,
    trace: trace,
  };
  Logger.info(`Compiling tsp using ${emitterPackage}...`);
  const [options, diagnostics] = await resolveCompilerOptions(NodeHost, {
    cwd: process.cwd(),
    entrypoint: resolvedMainFilePath,
    overrides,
  });
  const emitterOutputDir = options.options?.[emitterPackage]?.["emitter-output-dir"] ?? undefined;
  Logger.debug(`Compiler options: ${JSON.stringify(options)}`);

  const cliOptions = Object.entries(options.options?.[emitterPackage] ?? {})
    .map(([key, value]) => {
      if (typeof value === "object") {
        value = JSON.stringify(value);
      }
      return `--option ${key}=${value}`;
    })
    .join(" ");

  const exampleCmd = `npx tsp compile ${resolvedMainFilePath} --emit ${emitterPackage} ${cliOptions}`;
  const compileResult: { success: boolean; exampleCmd: string } = {
    success: false,
    exampleCmd,
  };
  if (!emitterOutputDir) {
    // TODO: Once we have fully switched to emitter-output-dir, we can make this an error and immediately return the compileResult.
    Logger.warn(
      `Missing emitter-output-dir in ${emitterPackage} options of tspconfig.yaml. Soon tsp-client will deprecate "package-dir" in favor of "emitter-output-dir".`,
    );
  }
  if (diagnostics.length > 0) {
    let errorDiagnostic = false;
    // This should not happen, but if it does, we should log it.
    Logger.warn(
      "Diagnostics were reported while resolving compiler options. Use the `--debug` flag to see if there is warning diagnostic output.",
    );
    for (const diagnostic of diagnostics) {
      if (diagnostic.severity === "error") {
        Logger.error(formatDiagnostic(diagnostic));
        errorDiagnostic = true;
      } else {
        Logger.debug(formatDiagnostic(diagnostic));
      }
    }
    if (errorDiagnostic) {
      return compileResult;
    }
  }

  const program = await compile(NodeHost, resolvedMainFilePath, options);

  if (program.diagnostics.length > 0) {
    let errorDiagnostic = false;
    Logger.warn(
      "Diagnostics were reported during compilation. Use the `--debug` flag to see if there is warning diagnostic output.",
    );
    for (const diagnostic of program.diagnostics) {
      if (diagnostic.severity === "error") {
        Logger.error(formatDiagnostic(diagnostic));
        errorDiagnostic = true;
      } else {
        Logger.debug(formatDiagnostic(diagnostic));
      }
    }
    if (errorDiagnostic) {
      return compileResult;
    }
  }
  Logger.success("generation complete");
  compileResult.success = true;
  return compileResult;
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

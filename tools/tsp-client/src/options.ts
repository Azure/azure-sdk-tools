import { parseArgs } from "node:util";
import { Logger, printUsage, printVersion } from "./log.js";
import * as path from "node:path";
import { knownLanguages, languageAliases } from "./languageSettings.js";
import { doesFileExist } from "./network.js";
import PromptSync from "prompt-sync";

export interface Options {
  debug: boolean;
  command: string;
  tspConfig?: string;
  emitter?: string;
  mainFile?: string;
  outputDir: string;
  noCleanup: boolean;
  skipSyncAndGenerate: boolean;
  commit?: string;
  repo?: string;
  isUrl: boolean;
  localSpecRepo?: string;
  emitterOptions?: string;
}

export async function getOptions(): Promise<Options> {
  const { values, positionals } = parseArgs({
    allowPositionals: true,
    options: {
      help: {
        type: "boolean",
        short: "h",
      },
      version: {
        type: "boolean",
        short: "v",
      },
      debug: {
        type: "boolean",
        short: "d",
      },
      emitter: {
        type: "string",
        short: "e",
      },
      mainFile: {
        type: "string",
        short: "m",
      },
      outputDir: {
        type: "string",
        short: "o",
      },
      tspConfig: {
        type: "string",
        short: "c",
      },
      commit: {
        type: "string",
        short: "C",
      },
      repo: {
        type: "string",
        short: "R",
      },
      ["emitter-options"]: {
        type: "string",
      },
      ["local-spec-repo"]: {
        type: "string",
      },
      ["save-inputs"]: {
        type: "boolean",
      },
      ["skip-sync-and-generate"]: {
        type: "boolean",
      }
    },
  });
  if (values.help) {
    printUsage();
    process.exit(0);
  }

  if (values.version) {
    await printVersion();
    process.exit(0);
  }

  if (values.emitter) {
    let emitter = values.emitter.toLowerCase();
    if (emitter in languageAliases) {
      emitter = languageAliases[emitter]!;
    }
    if (!(knownLanguages as readonly string[]).includes(emitter)) {
      Logger.error(`Unknown language ${values.emitter}`);
      Logger.error(`Valid languages are: ${knownLanguages.join(", ")}`);
      printUsage();
      process.exit(1);
    }
  }

  if (positionals.length === 0) {
    Logger.error("Command is required");
    printUsage();
    process.exit(1);
  }

  if (positionals[0] !== "sync" && positionals[0] !== "generate" && positionals[0] !== "update" && positionals[0] !== "init") {
    Logger.error(`Unknown command ${positionals[0]}`);
    printUsage();
    process.exit(1);
  }

  let isUrl = false;
  if (positionals[0] === "init") {
    if (!values.tspConfig) {
      Logger.error("tspConfig is required");
      printUsage();
      process.exit(1);
    }
    if (await doesFileExist(values.tspConfig)) {
      isUrl = true;
    }
    if (!isUrl) {
      if (values.commit === undefined || values.repo === undefined) {
        Logger.error("The commit and repo options are required when tspConfig is a local directory");
        printUsage();
        process.exit(1);
      }
    }
  }
  // By default, assume that the command is run from the output directory
  let outputDir = ".";
  if (values.outputDir) {
    outputDir = values.outputDir;
  }
  outputDir = path.resolve(path.normalize(outputDir));

  // Ask user is this is the correct output directory
  const prompt = PromptSync();
  let useOutputDir = prompt("Use output directory '" + outputDir + "'? (y/n) ", "y");

  if (useOutputDir.toLowerCase() === "n") {
    const newOutputDir = prompt("Enter output directory: ");
    if (!newOutputDir) {
      Logger.error("Output directory is required");
      printUsage();
      process.exit(1);
    }
    outputDir = path.resolve(path.normalize(newOutputDir));
  }
  Logger.info("Using output directory '" + outputDir + "'");

  return {
    debug: values.debug ?? false,
    command: positionals[0],
    tspConfig: values.tspConfig,
    emitter: values.emitter,
    mainFile: values.mainFile,
    noCleanup: values["save-inputs"] ?? false,
    skipSyncAndGenerate: values["skip-sync-and-generate"] ?? false,
    outputDir: outputDir,
    commit: values.commit,
    repo: values.repo,
    isUrl: isUrl,
    localSpecRepo: values["local-spec-repo"],
    emitterOptions: values["emitter-options"],
  };
}

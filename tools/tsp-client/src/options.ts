import { parseArgs } from "node:util";
import { Logger, printUsage, printVersion } from "./log.js";
import * as path from "node:path";
import { doesFileExist } from "./network.js";
import PromptSync from "prompt-sync";

export interface Options {
  debug: boolean;
  command: string;
  tspConfig?: string;
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
      ["output-dir"]: {
        type: "string",
        short: "o",
      },
      ["tsp-config"]: {
        type: "string",
        short: "c",
      },
      commit: {
        type: "string",
      },
      repo: {
        type: "string",
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
    if (!values["tsp-config"]) {
      Logger.error("tspConfig is required");
      printUsage();
      process.exit(1);
    }
    if (await doesFileExist(values["tsp-config"])) {
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
  if (values["output-dir"]) {
    outputDir = values["output-dir"];
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
    tspConfig: values["tsp-config"],
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

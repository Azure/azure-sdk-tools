import { parseArgs } from "node:util";
import { Logger, printUsage, printVersion } from "./log.js";
import process from  "node:process";
import { doesFileExist } from "./network.js";
import PromptSync from "prompt-sync";
import { normalizePath, resolvePath } from "@typespec/compiler";

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
  swaggerReadme?: string;
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
      },
      ["swagger-readme"]: {
        type: "string",
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
  const supportedCommands = ["sync", "generate", "update", "init", "convert"];

  const command = positionals[0];
  if (!command) {
    Logger.error("Command is required");
    printUsage();
    process.exit(1);
  }

  if (!supportedCommands.includes(command)) {
    Logger.error(`Unknown command ${command}`);
    printUsage();
    process.exit(1);
  }

  let isUrl = true;
  if (command === "init") {
    if (!values["tsp-config"]) {
      Logger.error("tspConfig is required");
      printUsage();
      process.exit(1);
    }
    if (await doesFileExist(values["tsp-config"])) {
      isUrl = false;
    }
    if (!isUrl) {
      if (!values.commit || !values.repo) {
        Logger.error("The commit and repo options are required when tspConfig is a local directory");
        printUsage();
        process.exit(1);
      }
    }
  }

  if (command === "convert") {
    if (!values["swagger-readme"]) {
      Logger.error("Must specify a swagger readme with the `--swagger-readme` flag");
      printUsage();
      process.exit(1);
    }
  }

  // By default, assume that the command is run from the output directory
  let outputDir = ".";
  if (values["output-dir"]) {
    outputDir = values["output-dir"];
  }
  outputDir = resolvePath(process.cwd(), outputDir);

  let useOutputDir;
  if (process.stdin.isTTY) {
    // Ask user is this is the correct output directory
    const prompt = PromptSync();
    useOutputDir = prompt("Use output directory '" + outputDir + "'? (y/n) ", "y");
  } else {
    // There is no user to ask, so assume yes
    useOutputDir = 'y';
  }
  
  if (useOutputDir.toLowerCase() === "n") {
    const newOutputDir = prompt("Enter output directory: ");
    if (!newOutputDir) {
      Logger.error("Output directory is required");
      printUsage();
      process.exit(1);
    }
    outputDir = resolvePath(normalizePath(newOutputDir));
  }
  Logger.info("Using output directory '" + outputDir + "'");

  return {
    debug: values.debug ?? false,
    command: command,
    tspConfig: values["tsp-config"],
    noCleanup: values["save-inputs"] ?? false,
    skipSyncAndGenerate: values["skip-sync-and-generate"] ?? false,
    outputDir: outputDir,
    commit: values.commit,
    repo: values.repo,
    isUrl: isUrl,
    localSpecRepo: values["local-spec-repo"],
    emitterOptions: values["emitter-options"],
    swaggerReadme: values["swagger-readme"],
  };
}

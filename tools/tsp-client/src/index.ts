import yargs from "yargs/yargs";
import { hideBin } from "yargs/helpers";
import { checkDebugLogging, Logger, printBanner, usageText } from "./log.js";
import {
  convertCommand,
  generateCommand,
  generateConfigFilesCommand,
  generateLockFileCommand,
  initCommand,
  installDependencies,
  sortSwaggerCommand,
  syncCommand,
  updateCommand,
} from "./commands.js";
import { joinPaths, normalizePath, resolvePath } from "@typespec/compiler";
import PromptSync from "prompt-sync";
import { readFile } from "fs/promises";
import { fileURLToPath } from "url";
import { dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));

const packageJson = JSON.parse(await readFile(joinPaths(__dirname, "..", "package.json"), "utf8"));

/**
 * Prints the command preamble, including the version and banner.
 * This is called at the start of each command to provide context.
 *
 * @param argv The parsed arguments from yargs.
 */
function commandPreamble(argv: any) {
  checkDebugLogging(argv);
  printBanner();
  Logger.info(packageJson.version);
}

/** Ensure the output directory exists and allow interactive users to confirm or override the value. */
export function resolveOutputDir(argv: any): string {
  let outputDir = resolvePath(process.cwd(), argv["output-dir"]);
  const usePrompt = argv["prompt"];

  let useOutputDir;
  if (process.stdin.isTTY && usePrompt) {
    // Ask user is this is the correct output directory
    const prompt = PromptSync();
    useOutputDir = prompt(`Use output directory '${outputDir}'? (y/n)`, "y");
  } else {
    // There is no user to ask, so assume yes
    useOutputDir = "y";
  }

  if (useOutputDir.toLowerCase() === "n") {
    const newOutputDir = prompt("Enter output directory: ");
    if (!newOutputDir) {
      Logger.error("Output directory is required");
      process.exit(1);
    }
    outputDir = resolvePath(normalizePath(newOutputDir));
  }
  Logger.info(`Using output directory '${outputDir}'`);

  commandPreamble(argv);
  return outputDir;
}

const parser = yargs(hideBin(process.argv))
  .version(packageJson.version)
  .alias("v", "version")
  .scriptName("")
  .usage(usageText)
  .option("debug", {
    alias: "d",
    type: "boolean",
    description: "Enable debug logging",
  })
  .option("output-dir", {
    alias: "o",
    type: "string",
    description: "Specify an alternate output directory for the generated files.",
    default: ".",
  })
  .option("no-prompt", {
    alias: "y",
    type: "boolean",
    description: "Skip any interactive prompts.",
  })
  .command(
    "init",
    "Initialize the SDK project folder from a tspconfig.yaml",
    (yargs: any) => {
      return yargs
        .option("tsp-config", {
          alias: "c",
          type: "string",
          description: "Path to tspconfig.yaml",
          demandOption: true,
        })
        .option("skip-sync-and-generate", {
          type: "boolean",
          description: "Skip syncing and generating the TypeSpec project",
        })
        .option("local-spec-repo", {
          type: "string",
          description: "Path to local repository with the TypeSpec project",
        })
        .option("save-inputs", {
          type: "boolean",
          description: "Don't clean up the temp directory after generation",
        })
        .option("emitter-options", {
          type: "string",
          description: "The options to pass to the emitter",
        })
        .option("commit", {
          type: "string",
          description: "Commit hash to be used",
        })
        .option("repo", {
          type: "string",
          description: "Repository where the project is defined",
        })
        .option("skip-install", {
          type: "boolean",
          description: "Skip installing dependencies",
        })
        .option("emitter-package-json-path", {
          type: "string",
          description: "Alternate path for emitter-package.json file",
        })
        .option("trace", {
          type: "array",
          description: "Enable tracing during compile",
        })
        .option("update-if-exists", {
          type: "boolean",
          description: "Update the library if it exists, keeping extra tsp-location.yaml data",
          default: false,
        });
    },
    async (argv: any) => {
      argv["output-dir"] = resolveOutputDir(argv);
      await initCommand(argv);
    },
  )
  .command(
    "sync",
    "Sync TypeSpec project specified in tsp-location.yaml",
    (yargs: any) => {
      return yargs.option("local-spec-repo", {
        type: "string",
        description: "Path to local spec repo",
      });
    },
    async (argv: any) => {
      argv["output-dir"] = resolveOutputDir(argv);
      await syncCommand(argv);
    },
  )
  .command(
    "generate",
    "Generate from a TypeSpec project",
    (yargs: any) => {
      return yargs
        .options("emitter-options", {
          type: "string",
          description: "The options to pass to the emitter",
        })
        .options("save-inputs", {
          type: "boolean",
          description: "Don't clean up the temp directory after generation",
        })
        .option("skip-install", {
          type: "boolean",
          description: "Skip installing dependencies",
        })
        .option("trace", {
          type: "array",
          description: "Enable tracing during compile",
        });
    },
    async (argv: any) => {
      argv["output-dir"] = resolveOutputDir(argv);
      await generateCommand(argv);
    },
  )
  .command(
    "update",
    "Sync and generate from a TypeSpec project",
    (yargs: any) => {
      return yargs
        .option("repo", {
          type: "string",
          description: "Repository where the project is defined",
        })
        .option("commit", {
          type: "string",
          description: "Commit hash to be used",
        })
        .option("tsp-config", {
          type: "string",
          description: "Path to tspconfig.yaml",
        })
        .option("local-spec-repo", {
          type: "string",
          description: "Path to local spec repo",
        })
        .option("emitter-options", {
          type: "string",
          description: "The options to pass to the emitter",
        })
        .option("save-inputs", {
          type: "boolean",
          description: "Don't clean up the temp directory after generation",
        })
        .option("skip-install", {
          type: "boolean",
          description: "Skip installing dependencies",
        })
        .option("trace", {
          type: "array",
          description: "Enable tracing during compile",
        });
    },
    async (argv: any) => {
      argv["output-dir"] = resolveOutputDir(argv);
      await updateCommand(argv);
    },
  )
  .command(
    "convert",
    "Convert a swagger specification to TypeSpec",
    (yargs: any) => {
      return yargs
        .option("swagger-readme", {
          type: "string",
          description: "Path to the swagger readme file",
          demandOption: true,
        })
        .option("arm", {
          type: "boolean",
          description: "Convert swagger to ARM TypeSpec",
        })
        .option("fully-compatible", {
          type: "boolean",
          description: "Convert swagger to fully compatible TypeSpec",
        });
    },
    async (argv: any) => {
      argv["output-dir"] = resolveOutputDir(argv);
      await convertCommand(argv);
    },
  )
  .command(
    "generate-config-files",
    "Generate emitter-package.json and emitter-package-lock.json files from a TypeSpec emitter's package.json",
    (yargs: any) => {
      return yargs
        .option("package-json", {
          type: "string",
          description: "Path to the emitter's package.json file",
          demandOption: true,
        })
        .option("overrides", {
          type: "string",
          description: "Path to an override config file for pinning specific dependencies",
        })
        .option("emitter-package-json-path", {
          type: "string",
          description: "Alternate path for emitter-package.json file",
        });
    },
    async (argv: any) => {
      argv["output-dir"] = resolveOutputDir(argv);
      await generateConfigFilesCommand(argv);
    },
  )
  .command(
    "generate-lock-file",
    "Generate a lock file under the eng/ directory from an existing emitter-package.json",
    (yargs: any) => {
      return yargs.option("emitter-package-json-path", {
        type: "string",
        description: "Alternate path for emitter-package.json file",
      });
    },
    async (argv: any) => {
      argv["output-dir"] = resolveOutputDir(argv);
      await generateLockFileCommand(argv);
    },
  )
  .command(
    "sort-swagger <swagger-file>",
    "Sort a swagger file to be the same content order with TypeSpec generated swagger",
    (yargs: any) => {
      return yargs.positional("swagger-file", {
        type: "string",
        description: "Path to the swagger file",
      });
    },
    async (argv: any) => {
      commandPreamble(argv);
      await sortSwaggerCommand(argv);
    },
  )
  .command(
    "install-dependencies [path]",
    "Install dependencies for the TypeSpec project. Default to the root of the repository.",
    (yargs: any) => {
      return yargs
        .option("output-dir", {
          alias: "o",
          type: "string",
          description: "This option is disabled for this command",
          hidden: true, // Hide the option from help output
          default: undefined, // Remove the default value
        })
        .positional("path", {
          type: "string",
          description: "Install path of the node_modules/ directory",
        });
    },
    async (argv: any) => {
      await installDependencies(argv);
    },
  )
  .demandCommand(1, "Please provide a command.")
  .help()
  .showHelpOnFail(true);

try {
  await parser.parse();
} catch (err: any) {
  Logger.error(err);
  process.exit(1);
}

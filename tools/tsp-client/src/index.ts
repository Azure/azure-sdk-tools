import yargs from "yargs/yargs";
import { hideBin } from "yargs/helpers";
import { checkDebugLogging, Logger, printBanner, usageText } from "./log.js";
import {
  compareCommand,
  convertCommand,
  generateCommand,
  generateLockFileCommand,
  initCommand,
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
    "generate-lock-file",
    "Generate a lock file under the eng/ directory from an existing emitter-package.json",
    {},
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
    "compare",
    "Compare two Swaggers for functional equivalency. This is typically used to compare a source Swagger with a TypeSpec project or TypeSpec generated Swagger to ensure that the TypeSpec project is functionally equivalent to the source Swagger.",
    (yargs: any) => {
      return yargs.help(false);
    },
    async (argv: any) => {
      argv["output-dir"] = resolveOutputDir(argv);
      const rawArgs = process.argv.slice(3);
      await compareCommand(argv, rawArgs);
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

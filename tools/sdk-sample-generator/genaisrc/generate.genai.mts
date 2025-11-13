import { getUniqueDirName, getWorkspaceFiles } from "./src/utils.ts";
import { generateOrLoadIdeas as generateOrLoadSampleIdeas } from "./src/generateIdeas.ts";
import { selectSampleIdeas } from "./src/getFinalIdeasList.ts";
import { parseUserPrompt } from "./src/parseUserPrompt.ts";
import { appEnvPaths } from "./src/paths.ts";
import type { Language } from "./src/types.ts";
import { run } from "./src/run.ts";
import { languages } from "./src/languages.ts";

script({
  model: "none",
  description: "Generates sample code for from API specifications",
  parameters: {
    "rest-api": {
      type: "string",
      description: "The REST API specification file or folder",
      required: false,
    },
    "client-api": {
      type: "string",
      description: "The client API specification file or folder",
      required: false,
    },
    language: {
      type: "string",
      description: "The programming language for the samples",
    },
    "samples-count": {
      type: "number",
      description: "The maximum number of samples to generate",
      default: "10",
    },
    "ideas-model": {
      type: "string",
      description: "The model to use for generating sample ideas",
      default: "azure:gpt-4.1-nano",
    },
    "coding-model": {
      type: "string",
      description: "The model to use for generating sample code",
      default: "azure:gpt-4.1-mini",
    },
    "reviewing-model": {
      type: "string",
      description: "The model to use for reviewing generated samples",
      default: "azure:gpt-4.1-mini",
    },
    "skip-review": {
      type: "boolean",
      description: "Whether to skip review of the generated samples",
      default: false,
    },
    out: {
      type: "string",
      description: "The location to save the generated samples",
      required: false,
    },
    "client-dist": {
      type: "string",
      description:
        "The client distribution that should be installed when verifying the samples",
      required: false,
    },
    "client-dist-name": {
      type: "string",
      description: "The name of the client distribution, e.g. package name",
      required: false,
    },
    interactive: {
      type: "boolean",
      description: "Whether to run in interactive mode.",
      default: false,
    },
    "use-ideas-cache": {
      type: "boolean",
      description: "Whether to use the cached sample ideas.",
      default: false,
    },
    "extra-files": {
      type: "string",
      description:
        "A comma-separated list of extra files or folders to include in the specification passed to the model.",
      required: false,
    },
    "user-prompt": {
      type: "string",
      description:
        "Path to a markdown file containing a user prompt. If provided, skips idea generation and uses the prompt as the sample idea.",
      required: false,
    },
  },
  system: [
    "system",
    "system.typescript",
    "system.typescript_typecheck",
    "system.python",
    "system.python_typecheck",
    "system.go",
    "system.go_typecheck",
    "system.java",
    "system.java_typecheck",
  ],
});

const ideasModel = env.vars["ideas-model"] as string;
const codingModel = env.vars["coding-model"] as string;
const reviewingModel = env.vars["reviewing-model"] as string;
const interactive = env.vars.interactive as boolean;
const skipReview = env.vars["skip-review"] as boolean;
const useIdeasCache = env.vars["use-ideas-cache"] as boolean;
const inputLangVal = env.vars.language;
const clientApiPath = env.vars["client-api"];
const restApiPath = env.vars["rest-api"];
const samplesLocation = env.vars["out"] as string | undefined;
const samplesCount = env.vars["samples-count"] as number;
const extraFiles = env.vars["extra-files"] as string | undefined;
const userPromptPath = env.vars["user-prompt"] as string | undefined;
const clientDist = env.vars["client-dist"] as string | undefined;
const pkgName = env.vars["client-dist-name"] as string | undefined;

if (samplesCount <= 0) {
  throw new Error("Invalid sample count");
}

const hasRestApi = !!restApiPath;
const hasClientApi = !!clientApiPath;

if (!hasRestApi && !hasClientApi) {
  throw new Error(
    "At least one of 'rest-api' or 'client-api' must be provided",
  );
}

if (!["curl", undefined].includes(inputLangVal) && !hasClientApi) {
  throw new Error(
    "The 'language' parameter with a value other than \"curl\" can only be used with 'client-api'.",
  );
}

const extraFilesList: WorkspaceFile[] = !extraFiles
  ? []
  : (
      await Promise.all(
        extraFiles.split(",").map((file) => getWorkspaceFiles(file.trim())),
      )
    ).flat();

const clientApiFiles = !hasClientApi
  ? extraFilesList
  : (await getWorkspaceFiles(clientApiPath)).concat(extraFilesList);

const restApiFiles = !hasRestApi ? [] : await getWorkspaceFiles(restApiPath);

const selectedLanguages = (
  inputLangVal
    ? [inputLangVal]
    : hasRestApi && !hasClientApi
      ? ["curl"]
      : languages
) as Language[];

const cacheFolder = path.join(appEnvPaths.cache, getUniqueDirName());

let selectedSampleIdeas;

if (userPromptPath) {
  const userSampleIdea = await parseUserPrompt(userPromptPath);
  selectedSampleIdeas = [userSampleIdea];
} else {
  const sampleIdeas = await generateOrLoadSampleIdeas({
    model: ideasModel,
    spec: restApiFiles.length > 0 ? restApiFiles : clientApiFiles,
    samplesCount: samplesCount,
    useIdeasCache,
    cacheFolder,
  });

  selectedSampleIdeas = await selectSampleIdeas({
    sampleIdeas,
    samplesCount,
    interactive,
  });
}

if (selectedLanguages.length === 1) {
  const language = selectedLanguages[0];
  await run({
    spec: clientApiFiles,
    codingModel,
    reviewingModel,
    skipReview,
    sampleIdeas: selectedSampleIdeas,
    language,
    samplesFolder: samplesLocation ?? path.join(cacheFolder, language),
    clientDist,
    pkgName,
  });
} else {
  throw new Error(
    "Multiple languages are not supported. Please select one language.",
  );
}

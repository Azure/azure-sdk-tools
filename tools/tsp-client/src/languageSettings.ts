import { join } from "node:path";

interface LanguageEmitterSettings {
  emitterName: string;
  emitterOptions: Record<string, string>;
}

export const knownLanguages = ["csharp", "java", "javascript", "python", "openapi"] as const;
export type KnownLanguage = (typeof knownLanguages)[number];
export const languageAliases: Record<string, KnownLanguage> = {
  cs: "csharp",
  js: "javascript",
  ts: "javascript",
  typescript: "javascript",
  py: "python",
};

export function getEmitterPackage(language: string): string {
  if (language in languageEmitterSettings) {
    return languageEmitterSettings[language as KnownLanguage].emitterName;
  }
  throw new Error(`Unknown language ${language}`);
}

export function getEmitterOutputPath(language: string, projectDirectory: string): string {
  if (language === "csharp") {
    return join(projectDirectory, "src");
  }
  return projectDirectory;
}

const languageEmitterSettings: Record<KnownLanguage, LanguageEmitterSettings> = {
  csharp: {
    emitterName: "@azure-tools/typespec-csharp",
    emitterOptions: {
      "emitter-output-dir": "$projectDirectory/src",
    },
  },
  java: {
    emitterName: "@azure-tools/typespec-java",
    emitterOptions: {
      "emitter-output-dir": "$projectDirectory",
    },
  },
  javascript: {
    emitterName: "@azure-tools/typespec-ts",
    emitterOptions: {
      "emitter-output-dir": "$projectDirectory",
    },
  },
  python: {
    emitterName: "@azure-tools/typespec-python",
    emitterOptions: {
      "emitter-output-dir": "$projectDirectory",
    },
  },
  openapi: {
    emitterName: "@typespec/openapi3",
    emitterOptions: {},
  },
};

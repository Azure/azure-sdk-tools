import { describe, expect, test } from "vitest";
import {
    resolveOptions,
    specifyApiVersionToGenerateSDKByTypeSpec,
} from "../../common/utils.js";
import path from "path";
import { deepStrictEqual, strictEqual } from "assert";
import * as fs from "fs";
import { isStableSDKReleaseType } from "../../utils/version.js";
import { getRandomInt } from "../utils/utils.js";
import { ensureDir, remove, writeFile } from "fs-extra";
import { stringify } from "yaml";

describe("resolveOptions", () => {
    test("loads config at the given path", async () => {
        const configPath = path.join(__dirname, "config/myConfig.yaml");
        const emitterOutputDir = path.posix
            .join(__dirname, "/config")
            .replace(/\\/g, "/");
        const options = await resolveOptions(configPath);
        deepStrictEqual(options.options, {
            "@azure-tools/typespec-autorest": {
                "azure-resource-provider-folder": "resource-manager",
                "emitter-output-dir": `${emitterOutputDir}/..`,
                "output-file":
                    "resource-manager/{service-name}/{version-status}/{version}/openapi.json",
            },
            "@azure-tools/typespec-ts": {
                "output-folder": "sdk/informatica/src/generated",
                "package-dir": "op/value",
                "service-dir": "sdk/informatica",
            },
            usingParam: {
                "output-file": "sdk/informaticadatamanagement",
                "output-dir": "params/default/value",
            },
        });
        strictEqual(
            options.configFile.parameters?.["service-dir"]?.default,
            "sdk/informaticadatamanagement",
        );
    });
});

describe("specifiyApiVersionToGenerateSDKByTypeSpec", () => {
    test("Updated API version into tspconfig.yaml", async () => {
        const fakeTspConfig = {
            options: {
                "@azure-tools/typespec-ts": {
                    "is-modular-library": true,
                },
            },
        };
        const tempSpecFolder = path.join(
            __dirname,
            `tmp/spec-${getRandomInt(10000)}`,
        );
        try {
            await ensureDir(tempSpecFolder);
            await writeFile(
                path.join(tempSpecFolder, "tspconfig.yaml"),
                stringify(fakeTspConfig),
                {
                    encoding: "utf8",
                    flush: true,
                },
            );
            const expectedVersion = "2023-10-01";
            specifyApiVersionToGenerateSDKByTypeSpec(
                tempSpecFolder,
                expectedVersion,
            );
            const data: string = fs.readFileSync(
                path.join(tempSpecFolder, "tspconfig.yaml"),
                "utf8",
            );
            expect(data.includes(`api-version: '${expectedVersion}'`)).toBe(
                true,
            );
        } finally {
            await remove(tempSpecFolder);
        }
    });

    test("tspconfig.yaml does not exist", async () => {
        const tempSpecFolder = path.join(
            __dirname,
            `tmp/spec-${getRandomInt(10000)}`,
        );
        try {
            await ensureDir(tempSpecFolder);            
            expect(() =>
                specifyApiVersionToGenerateSDKByTypeSpec(
                    tempSpecFolder,
                    "2023-10-01",
                ),
            ).toThrow(
                `Failed to find tspconfig.yaml in ${tempSpecFolder}.`,
            );
        } finally {
            await remove(tempSpecFolder);
        }
    });

    test("not found @azure-tools/typespec-ts options in tspconfig.yaml", async () => {
        const fakeTspConfig = {
            options: {
                "@azure-tools/typespec-go": {
                    "is-modular-library": true,
                },
            },
        };
        const tempSpecFolder = path.join(
            __dirname,
            `tmp/spec-${getRandomInt(10000)}`,
        );
        try {
            await ensureDir(tempSpecFolder);
            await writeFile(
                path.join(tempSpecFolder, "tspconfig.yaml"),
                stringify(fakeTspConfig),
                {
                    encoding: "utf8",
                    flush: true,
                },
            );
            expect(() =>
                specifyApiVersionToGenerateSDKByTypeSpec(
                    tempSpecFolder,
                    "2023-10-01",
                ),
            ).toThrow(
                `Failed to find @azure-tools/typespec-ts options in tspconfig.yaml.`,
            );
        } finally {
            await remove(tempSpecFolder);
        }
    });

    test("Failed to parse tspconfig.yaml", async () => {    
        const badYaml = `
            skills:
              - JavaScript
              - Node.js
              - YAML
                - extra-indent-error
        `;    
        const tempSpecFolder = path.join(
            __dirname,
            `tmp/spec-${getRandomInt(10000)}`,
        );
        try {
            await ensureDir(tempSpecFolder);
            await writeFile(
                path.join(tempSpecFolder, "tspconfig.yaml"),
                badYaml,
                {
                    encoding: "utf8",
                    flush: true,
                },
            );
            expect(() =>
                specifyApiVersionToGenerateSDKByTypeSpec(
                    tempSpecFolder,
                    "2023-10-01",
                ),
            ).toThrowError('Failed to parse tspconfig.yaml');
        } finally {
            await remove(tempSpecFolder);
        }
    });
});

describe("getReleaseStatus", () => {
    test("apiVersion is stable, sdkReleaseType is stable", async () => {
        const result = await isStableSDKReleaseType("Preview", {
            apiVersion: "2023-10-01",
            sdkReleaseType: "stable",
        });
        expect(result).toBe(true);
    });

    test("apiVersion is stable, sdkReleaseType is beta", async () => {
        const result = await isStableSDKReleaseType("Preview", {
            apiVersion: "2023-10-01",
            sdkReleaseType: "beta",
        });
        expect(result).toBe(false);
    });

    test("apiVersion is preview, sdkReleaseType is stable", async () => {
        const result = await isStableSDKReleaseType("Stable", {
            apiVersion: "2023-10-01-preview",
            sdkReleaseType: "stable",
        });
        expect(result).toBe(true);
    });

    test("apiVersion is preview, sdkReleaseType is beta", async () => {
        const result = await isStableSDKReleaseType("Stable", {
            apiVersion: "2023-10-01-preview",
            sdkReleaseType: "beta",
        });
        expect(result).toBe(false);
    });

    test("apiVersion not be provided", async () => {
        const result = await isStableSDKReleaseType("Preview", {
            apiVersion: "",
            sdkReleaseType: "stable",
        });
        expect(result).toBe(false);
    });
});

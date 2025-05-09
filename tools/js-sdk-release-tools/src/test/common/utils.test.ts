import { describe, expect, test } from "vitest";
import { resolveOptions, updateApiVersionInTspConfig } from "../../common/utils.js";
import path from "path";
import { deepStrictEqual, strictEqual } from "assert";
import * as fs from "fs";

describe("resolveOptions", () => {
    test("loads config at the given path", async () => {
        const configPath = path.join(__dirname, "config/myConfig.yaml");
        const emitterOutputDir = path.posix.join(__dirname, "/config").replace(/\\/g, '/');
        const options = await resolveOptions(configPath);
        deepStrictEqual(options.options, {
            "@azure-tools/typespec-autorest": {
                "azure-resource-provider-folder": "resource-manager",
                "emitter-output-dir":
                `${emitterOutputDir}/..`,
                "output-file":
                    "resource-manager/{service-name}/{version-status}/{version}/openapi.json",
            },
            "@azure-tools/typespec-ts": {
                "output-folder": "sdk/informatica/src/generated",
                "package-dir": "op/value",
                "service-dir": "sdk/informatica",
            },
            "usingParam":{
                "output-file": "sdk/informaticadatamanagement",
                "output-dir": "params/default/value"
            }
        });
        strictEqual(
            options.configFile.parameters?.["service-dir"]?.default,
            "sdk/informaticadatamanagement",
        );
    });
});

describe("updateApiVersionInTspConfig", () => {
    test("Updated API version into tspconfig.yaml ", () => {
        const typeSpecDirectory = path.join(__dirname, "tsp");
        const expectedVersion = "2023-10-01";
        updateApiVersionInTspConfig(typeSpecDirectory, expectedVersion);
        const data: string = fs.readFileSync(path.join(typeSpecDirectory, 'tspconfig.yaml'), 'utf8');
        expect(data.includes(`api-version: '${expectedVersion}'`)).toBe(true);
    });
    test("not found tspconfig.yaml ", () => {
        const typeSpecDirectory = path.join(__dirname);
        const expectedVersion = "2023-10-01";
        expect(() => updateApiVersionInTspConfig(typeSpecDirectory, expectedVersion)).toThrow(
            "tspconfig.yaml not found at path:"
        );
    });
});

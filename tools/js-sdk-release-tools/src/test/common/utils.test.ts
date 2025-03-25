import { describe, expect, test } from "vitest";
import { resolveOptions } from "../../common/utils.js";
import path from "path";
import { deepStrictEqual, strictEqual } from "assert";

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
                "service-dir": "sdk/informatica",
            },
            "usingParam":{
                "output-file": "sdk/informaticadatamanagement"
            }
        });
        strictEqual(
            options.configFile.parameters?.["service-dir"]?.default,
            "sdk/informaticadatamanagement",
        );
    });
});

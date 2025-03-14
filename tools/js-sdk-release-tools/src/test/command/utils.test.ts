import { describe, expect, test } from "vitest";
import { resolveOptions } from "../../common/utils.js";
import path from "path";
import { deepStrictEqual, strictEqual } from "assert";

describe("resolveOptions", () => {
    test("loads config at the given path", async () => {
        const configPath = path.join(__dirname, "config/myConfig.yaml");
        const options = await resolveOptions(configPath);
        deepStrictEqual(options.options, {
            "@azure-tools/typespec-autorest": {
                "azure-resource-provider-folder": "resource-manager",
                "emitter-output-dir":
                    "D:/GithubRepos/tmpRepo/azure-sdk-tools/tools/js-sdk-release-tools/src/test/command/config/..",
                "output-file":
                    "resource-manager/{service-name}/{version-status}/{version}/openapi.json",
            },
            "@azure-tools/typespec-ts": {
                azureSdkForJs: true,
                enableOperationGroup: true,
                experimentalExtensibleEnums: true,
                flavor: "azure",
                generateMetadata: true,
                hierarchyClient: false,
                isModularLibrary: true,
                "package-dir": "arm-informaticadatamanagement",
                packageDetails: {
                    name: "@azure/arm-informaticadatamanagement",
                },
                "service-dir": "sdk/informatica",
            },
        });
        deepStrictEqual(options.configFile.parameters, {
            "service-dir": {
                default: "sdk/informaticadatamanagement",
            },
        });
    });
});

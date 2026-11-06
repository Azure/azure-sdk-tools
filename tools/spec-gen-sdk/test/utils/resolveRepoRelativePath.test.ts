import { describe, it, expect } from "vitest";
import * as path from "path";
import { resolveRepoRelativePath } from "../../src/utils/workflowUtils";

// `specificationRepositoryConfiguration.json` lives in the spec repository, so
// `configFilePath` is content that arrives with a specification change. It names
// the automation config to load out of the SDK checkout, and that config in turn
// chooses which scripts run and which environment variables they receive, so the
// path must stay inside the repository it is declared against.
const REPO_ROOT = path.resolve(path.sep === "\\" ? "C:\\work\\azure-sdk-for-js" : "/work/azure-sdk-for-js");

// Values that climb out of the repository once the separators are normalized.
const TRAVERSING_PATHS = [
    "../spec/config.json",
    "../../../../etc/passwd",
    "nested/../../escape.json",
    "..",
    "../",
    "..\\..\\escape.json",
    "nested\\..\\..\\escape.json",
];

// Values that name a location directly rather than relative to the repository.
const ABSOLUTE_PATHS = [
    "/etc/passwd",
    "//host/share/config.json",
    "C:/Windows/win.ini",
    "c:/Windows/win.ini",
    "C:\\Windows\\win.ini",
    "\\\\host\\share\\config.json",
];

describe("resolveRepoRelativePath", () => {
    describe("accepts paths inside the repository", () => {
        it.each([
            ["swagger_to_sdk_config.json", ["swagger_to_sdk_config.json"]],
            ["./swagger_to_sdk_config.json", ["swagger_to_sdk_config.json"]],
            ["eng/swagger_to_sdk_config.json", ["eng", "swagger_to_sdk_config.json"]],
            ["eng/nested/deep/config.json", ["eng", "nested", "deep", "config.json"]],
            ["eng/./config.json", ["eng", "config.json"]],
            ["eng/inner/../config.json", ["eng", "config.json"]],
        ])("resolves %s", (configuredPath, expectedSegments) => {
            expect(resolveRepoRelativePath(REPO_ROOT, configuredPath, "configFilePath")).toBe(
                path.join(REPO_ROOT, ...(expectedSegments as string[]))
            );
        });

        it("accepts a Windows-style separator on every platform", () => {
            expect(resolveRepoRelativePath(REPO_ROOT, "eng\\config.json", "configFilePath")).toBe(
                path.join(REPO_ROOT, "eng", "config.json")
            );
        });

        it("resolves the repository root itself", () => {
            expect(resolveRepoRelativePath(REPO_ROOT, ".", "configFilePath")).toBe(REPO_ROOT);
        });
    });

    describe("rejects paths outside the repository", () => {
        it.each(TRAVERSING_PATHS)("rejects %s", (configuredPath) => {
            expect(() => resolveRepoRelativePath(REPO_ROOT, configuredPath, "configFilePath")).toThrow(
                /resolves outside the repository/
            );
        });

        it.each(ABSOLUTE_PATHS)("rejects %s", (configuredPath) => {
            expect(() => resolveRepoRelativePath(REPO_ROOT, configuredPath, "configFilePath")).toThrow(
                /must be a path relative to the repository root/
            );
        });

        it("names the offending setting and value so the spec author can fix it", () => {
            expect(() => resolveRepoRelativePath(REPO_ROOT, "../spec/config.json", "configFilePath")).toThrow(
                /configFilePath/
            );
            expect(() => resolveRepoRelativePath(REPO_ROOT, "../spec/config.json", "configFilePath")).toThrow(
                /\.\.\/spec\/config\.json/
            );
        });

        it("does not accept a sibling directory that shares the repository name as a prefix", () => {
            // A plain `startsWith` on the root without a trailing separator would
            // let `azure-sdk-for-js-secrets` through.
            expect(() => resolveRepoRelativePath(REPO_ROOT, "../azure-sdk-for-js-secrets/config.json", "configFilePath")).toThrow(
                /resolves outside the repository/
            );
        });
    });

    it("normalizes the repository root before comparing", () => {
        const unnormalizedRoot = path.join(REPO_ROOT, "sdk", "..");

        expect(resolveRepoRelativePath(unnormalizedRoot, "swagger_to_sdk_config.json", "configFilePath")).toBe(
            path.join(REPO_ROOT, "swagger_to_sdk_config.json")
        );
    });
});

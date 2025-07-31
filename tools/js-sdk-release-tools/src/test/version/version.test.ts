import { describe, expect, test } from "vitest";
import { generateTestNpmView } from "../utils/utils.js";
import { getLatestStableVersion, getNextBetaVersion, getUsedVersions } from "../../utils/version.js";
import { tryGetNpmView } from "../../common/npmUtils.js";

interface TestCase {
    latestVersion?: string;
    latestVersionDate?: string;
    betaVersion?: string;
    betaVersionDate?: string;
    nextVersion?: string;
    nextVersionDate?: string;
    expectedVersion?: string;
    description?: string;
}

describe("Get latest stable version after GA but beta version before GA", () => {
    const cases: TestCase[] = [
        {
            latestVersion: "1.0.0",
            latestVersionDate: "2025-06-20T09:13:48.079Z",
            betaVersion: "1.0.0-beta.1",
            betaVersionDate: "2025-06-01T07:07:56.529Z",
            expectedVersion: "1.0.0",
        },
        {
            latestVersion: "1.0.0",
            latestVersionDate: "2025-06-01T09:13:48.079Z",
            betaVersion: "1.0.0-beta.1",
            betaVersionDate: "2025-06-21T07:07:56.529Z",
            expectedVersion: "1.0.0",
        },
        {
            latestVersion: "1.0.0-beta.1",
            betaVersion: undefined,
            expectedVersion: "1.0.0-beta.1",
        },
        {
            latestVersion: undefined,
            betaVersion: "1.0.0-beta.1",
            expectedVersion: "1.0.0-beta.1",
        },
    ];
    test.each(cases)(
        "Stable: $latestVersion on data: $latestVersionDate, Beta: $betaVersion on data $betaVersionDate, Expected:$expectedVersion",
        async ({
            latestVersion,
            betaVersion,
            expectedVersion,
            latestVersionDate,
            betaVersionDate,
        }) => {
            const npmView = generateTestNpmView(
                latestVersion,
                betaVersion,
                latestVersionDate,
                betaVersionDate,
            );
            const version = getLatestStableVersion(npmView!);
            expect(version).toBe(expectedVersion);
        },
    );
});

describe("Get next beta version from beta and next tags", () => {
    const cases: TestCase[] = [
        // When both beta and next tags exist, and next is more recent
        {
            betaVersion: "1.0.0-beta.1",
            betaVersionDate: "2025-06-01T07:07:56.529Z",
            nextVersion: "1.0.0-next.1",
            nextVersionDate: "2025-06-20T09:13:48.079Z",
            expectedVersion: "1.0.0-next.1" // next is more recent
        },
        // When both beta and next tags exist, and beta is more recent
        {
            betaVersion: "1.0.0-beta.2",
            betaVersionDate: "2025-06-20T09:13:48.079Z",
            nextVersion: "1.0.0-next.1",
            nextVersionDate: "2025-06-01T07:07:56.529Z",
            expectedVersion: "1.0.0-beta.2" // beta is more recent
        },
        // When only beta tag exists
        {
            betaVersion: "1.0.0-beta.1",
            betaVersionDate: "2025-06-01T07:07:56.529Z",
            nextVersion: undefined,
            expectedVersion: "1.0.0-beta.1" // only beta exists
        },
        // When only next tag exists
        {
            betaVersion: undefined,
            nextVersion: "1.0.0-next.1",
            nextVersionDate: "2025-06-01T07:07:56.529Z",
            expectedVersion: "1.0.0-next.1" // only next exists
        },
        // When neither beta nor next tag exists
        {
            betaVersion: undefined,
            nextVersion: undefined,
            expectedVersion: undefined // neither exists
        },
        // When dates are not available, default to next
        {
            betaVersion: "1.0.0-beta.1",
            nextVersion: "1.0.0-next.1",
            expectedVersion: "1.0.0-next.1" // no dates, default to next
        }
    ];

    test.each(cases)(
        "Beta: $betaVersion ($betaVersionDate), Next: $nextVersion ($nextVersionDate), Expected: $expectedVersion",
        async ({
            betaVersion,
            betaVersionDate,
            nextVersion,
            nextVersionDate,
            expectedVersion,
        }) => {
            const npmView = generateTestNpmView(
                undefined, // latest version not needed for this test
                betaVersion,
                undefined, // latest version date not needed
                betaVersionDate,
                nextVersion,
                nextVersionDate
            );

            const version = getNextBetaVersion(npmView);
            expect(version).toBe(expectedVersion);
        },
    );

    test("returns undefined when npmViewResult is undefined", () => {
        const version = getNextBetaVersion(undefined);
        expect(version).toBeUndefined();
    });
});

describe("Used Versions", async () => {
    test("Get used versions from npm view", async () => {
        const view = {
            versions: {
                "3.0.0-alpha.20250619.1": {
                    name: "@azure/arm-test",
                    version: "3.0.0-alpha.20250619.1",
                    keywords: ["node"],
                    author: { name: "Microsoft Corporation" },
                },
                "3.0.0": {
                    name: "@azure/arm-test",
                    version: "3.0.0",
                    keywords: ["node"],
                    author: { name: "Microsoft Corporation" },
                },
            },
        };
        const versions = getUsedVersions(view!);
        expect(versions).toEqual(["3.0.0-alpha.20250619.1", "3.0.0"]);
    });
});

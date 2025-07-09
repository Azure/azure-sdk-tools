import { describe, expect, test } from "vitest";
import { generateTestNpmView } from "../utils/utils.js";
import { getLatestStableVersion } from "../../utils/version.js";

interface TestCase {
    latestVersion?: string;
    latestVersionDate?: string;
    betaVersion?: string;
    betaVersionDate?: string;
    expectedVersion?: string;
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

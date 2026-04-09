import { describe, test, expect, beforeEach, afterEach } from "vitest";
import path from "path";
import { ensureDir, remove, writeFile, pathExists } from "fs-extra";
import { getRandomInt } from "./utils/utils.js";
import { generateCiYamlCli } from "../generateCiYamlCli.js";

describe("generateCiYamlCli", () => {
    let sdkRepoDir: string;
    let otherDir: string;
    let originalCwd: string;

    beforeEach(async () => {
        originalCwd = process.cwd();
        const suffix = getRandomInt(100000);
        sdkRepoDir = path.join(__dirname, `tmp/sdk-repo-${suffix}`);
        otherDir = path.join(__dirname, `tmp/other-dir-${suffix}`);
        await ensureDir(sdkRepoDir);
        await ensureDir(otherDir);
    });

    afterEach(async () => {
        process.chdir(originalCwd);
        await remove(path.join(__dirname, "tmp"));
    });

    test("creates ci.yml relative to sdkRepoPath even when CWD differs", async () => {
        // Set up a fake package inside the SDK repo
        const packageRelPath = "sdk/storage/storage-blob";
        const packageAbsPath = path.join(sdkRepoDir, packageRelPath);
        await ensureDir(packageAbsPath);
        await writeFile(
            path.join(packageAbsPath, "package.json"),
            JSON.stringify({ name: "@azure/storage-blob", version: "12.0.0" })
        );
        await ensureDir(path.join(sdkRepoDir, "sdk/storage"));

        // CWD is intentionally NOT the SDK repo
        process.chdir(otherDir);

        await generateCiYamlCli(sdkRepoDir, packageAbsPath);

        // CI yaml should be written inside sdkRepoDir, not in otherDir
        const expectedCiPath = path.join(sdkRepoDir, "sdk/storage/ci.yml");
        expect(await pathExists(expectedCiPath)).toBe(true);

        // Nothing should be written to otherDir
        const wrongCiPath = path.join(otherDir, "sdk/storage/ci.yml");
        expect(await pathExists(wrongCiPath)).toBe(false);
    });
});

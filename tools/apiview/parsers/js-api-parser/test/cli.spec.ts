import { describe, expect, it, beforeEach, afterEach } from "vitest";
import { exec } from "node:child_process";
import { promisify } from "node:util";
import path from "path";
import { writeFile, unlink } from "node:fs/promises";

const execAsync = promisify(exec);

describe("CLI", () => {
  const binPath = path.join(__dirname, "../bin/ts-genapi.cjs");
  const testDataPath = path.join(__dirname, "data/renamedEnum.json");
  const tempOutputPath = "/tmp/cli-test-output.json";

  afterEach(async () => {
    try {
      await unlink(tempOutputPath);
    } catch {
      // Ignore if file doesn't exist
    }
  });

  it("shows help message with --help", async () => {
    const { stdout } = await execAsync(`node ${binPath} --help`);
    expect(stdout).toContain("Usage:");
    expect(stdout).toContain("path-to-api-extractor-json");
    expect(stdout).toContain("--output");
    expect(stdout).toContain("--metadata-file");
  });

  it("shows help message with -h", async () => {
    const { stdout } = await execAsync(`node ${binPath} -h`);
    expect(stdout).toContain("Usage:");
    expect(stdout).toContain("path-to-api-extractor-json");
    expect(stdout).toContain("--output");
  });

  it("fails when no arguments provided", async () => {
    try {
      await execAsync(`node ${binPath}`);
      expect.fail("Should have thrown an error");
    } catch (error: any) {
      expect(error.code).toBe(1);
      expect(error.stderr).toContain("Error: Both input file and --output are required");
    }
  });

  it("fails when only input provided", async () => {
    try {
      await execAsync(`node ${binPath} ${testDataPath}`);
      expect.fail("Should have thrown an error");
    } catch (error: any) {
      expect(error.code).toBe(1);
      expect(error.stderr).toContain("Error: Both input file and --output are required");
    }
  });

  it("fails when only output provided", async () => {
    try {
      await execAsync(`node ${binPath} --output ${tempOutputPath}`);
      expect.fail("Should have thrown an error");
    } catch (error: any) {
      expect(error.code).toBe(1);
      expect(error.stderr).toContain("Error: Both input file and --output are required");
    }
  });

  it("processes file successfully with required arguments", async () => {
    const { stdout, stderr } = await execAsync(
      `node ${binPath} ${testDataPath} --output ${tempOutputPath}`
    );
    
    expect(stderr).toBe("");
    expect(stdout).toBe("");

    // Verify output file was created and contains expected content
    const fs = await import("node:fs/promises");
    const outputContent = await fs.readFile(tempOutputPath, "utf-8");
    const parsed = JSON.parse(outputContent);
    
    expect(parsed).toHaveProperty("Name");
    expect(parsed).toHaveProperty("PackageName");
    expect(parsed).toHaveProperty("ReviewLines");
    expect(parsed.Language).toBe("JavaScript");
  });

  it("accepts metadata-file parameter", async () => {
    // This test just verifies the parameter is accepted without error
    const { stdout, stderr } = await execAsync(
      `node ${binPath} ${testDataPath} --output ${tempOutputPath} --metadata-file some-metadata.json`
    );
    
    expect(stderr).toBe("");
    expect(stdout).toBe("");
  });
});
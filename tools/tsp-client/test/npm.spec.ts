import { describe, it, expect } from "vitest";
import { isWorkspaceVersion, hasWorkspaceVersions } from "../src/npm.js";

describe("isWorkspaceVersion", () => {
  it("returns true for workspace:~ versions", () => {
    expect(isWorkspaceVersion("workspace:~")).toBe(true);
  });

  it("returns true for workspace:^ versions", () => {
    expect(isWorkspaceVersion("workspace:^")).toBe(true);
  });

  it("returns true for workspace:* versions", () => {
    expect(isWorkspaceVersion("workspace:*")).toBe(true);
  });

  it("returns true for workspace:~x.y.z versions", () => {
    expect(isWorkspaceVersion("workspace:~0.62.0")).toBe(true);
  });

  it("returns true for workspace:^x.y.z versions", () => {
    expect(isWorkspaceVersion("workspace:^1.6.0")).toBe(true);
  });

  it("returns false for regular semver versions", () => {
    expect(isWorkspaceVersion("1.6.0")).toBe(false);
  });

  it("returns false for range versions", () => {
    expect(isWorkspaceVersion("^1.6.0")).toBe(false);
    expect(isWorkspaceVersion("~0.62.0")).toBe(false);
  });

  it("returns false for latest", () => {
    expect(isWorkspaceVersion("latest")).toBe(false);
  });
});

describe("hasWorkspaceVersions", () => {
  it("returns true when at least one package has a workspace version", () => {
    const devDeps = {
      "@typespec/compiler": "workspace:~",
      "@typespec/http": "1.6.0",
    };
    expect(hasWorkspaceVersions(devDeps, ["@typespec/compiler", "@typespec/http"])).toBe(true);
  });

  it("returns false when no packages have workspace versions", () => {
    const devDeps = {
      "@typespec/compiler": "1.6.0",
      "@typespec/http": "1.6.0",
    };
    expect(hasWorkspaceVersions(devDeps, ["@typespec/compiler", "@typespec/http"])).toBe(false);
  });

  it("returns false for empty package list", () => {
    const devDeps = {
      "@typespec/compiler": "workspace:~",
    };
    expect(hasWorkspaceVersions(devDeps, [])).toBe(false);
  });

  it("returns false when packages are not in devDependencies", () => {
    const devDeps = {
      "@typespec/compiler": "workspace:~",
    };
    expect(hasWorkspaceVersions(devDeps, ["@typespec/http"])).toBe(false);
  });
});

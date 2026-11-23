import {
  access,
  chmod,
  mkdir,
  mkdtemp,
  readFile,
  readdir,
  readlink,
  rm,
  stat,
  symlink,
  writeFile,
} from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { BatchEmitterDependencies } from "../src/emitter-dependency-reuse.js";
import { npmCommand, npmReuseFingerprint } from "../src/npm.js";

vi.mock("../src/npm.js", () => ({
  npmCommand: vi.fn(),
  npmReuseFingerprint: vi.fn(),
}));

const dependency = {
  version: "1.0.0",
  resolved: "https://registry.npmjs.org/fixture/-/fixture-1.0.0.tgz",
  integrity: `sha512-${Buffer.alloc(64).toString("base64")}`,
};
const manifest = { dependencies: { fixture: "1.0.0" } };
const lock = { lockfileVersion: 3, packages: { "": manifest, "node_modules/fixture": dependency } };
let root: string;
let reuse: BatchEmitterDependencies;

async function workspace(name: string, packageManifest = manifest, packageLock: unknown = lock) {
  const dir = join(root, name, "spec");
  await mkdir(dir, { recursive: true });
  await writeFile(join(dirname(dir), "package.json"), JSON.stringify(packageManifest));
  await writeFile(join(dirname(dir), "package-lock.json"), JSON.stringify(packageLock));
  return dir;
}

beforeEach(async () => {
  root = await mkdtemp(join(tmpdir(), "tsp-emitter-reuse-test-"));
  vi.stubEnv("TMPDIR", root);
  vi.stubEnv("TMP", root);
  vi.stubEnv("TEMP", root);
  vi.stubEnv("TSP_CLIENT_EMITTER_REUSE", undefined);
  reuse = new BatchEmitterDependencies();
  vi.mocked(npmReuseFingerprint).mockResolvedValue("fixture npm configuration");
  vi.mocked(npmCommand).mockImplementation(async (dir, args) => {
    if (args.includes("--package-lock-only")) return;
    const modules = join(dirname(dir), "node_modules");
    await mkdir(join(modules, "fixture"), { recursive: true });
    await writeFile(join(modules, "fixture/package.json"), '{"name":"fixture","version":"1.0.0"}');
    await writeFile(join(modules, "fixture/index.js"), "export const value = 1;\n");
    await writeFile(
      join(modules, ".package-lock.json"),
      JSON.stringify({ ...lock, packages: { "node_modules/fixture": dependency } }),
    );
  });
});

afterEach(async () => {
  await reuse.dispose();
  await rm(root, { recursive: true, force: true });
  vi.unstubAllEnvs();
  vi.resetAllMocks();
});

it("installs once and validates and audits each subsequent compatible workspace", async () => {
  const first = await workspace("first");
  const second = await workspace("second");
  await reuse.install(dirname(first), first, ["ci"]);
  const firstFile = join(dirname(first), "node_modules/fixture/index.js");
  await writeFile(firstFile, "changed by the first emitter\n");
  await rm(dirname(first), { recursive: true });
  await reuse.install(dirname(second), second, ["ci"]);
  expect(await readFile(join(dirname(second), "node_modules/fixture/index.js"), "utf8")).toBe(
    "export const value = 1;\n",
  );
  expect(vi.mocked(npmCommand).mock.calls).toEqual([
    [first, ["ci"]],
    [second, ["ci", "--package-lock-only"]],
  ]);
});

it("uses full installs for changed configuration, missing lock, force, and script-bearing manifests", async () => {
  const first = await workspace("first");
  await reuse.install(dirname(first), first, ["ci"]);
  vi.mocked(npmReuseFingerprint).mockResolvedValue("changed npm configuration");
  const different = await workspace("changed");
  await reuse.install(dirname(different), different, ["ci"]);
  const scripted = await workspace("scripted", {
    ...manifest,
    scripts: { prepare: "script" },
  } as typeof manifest);
  await reuse.install(dirname(scripted), scripted, ["ci"]);
  const forced = await workspace("forced");
  await reuse.install(dirname(forced), forced, ["ci", "--force"]);
  const missing = await workspace("missing");
  await rm(join(dirname(missing), "package-lock.json"));
  await reuse.install(dirname(missing), missing, ["install"]);
  expect(vi.mocked(npmCommand).mock.calls.map(([, args]) => args)).toEqual([
    ["ci"],
    ["ci"],
    ["ci"],
    ["ci", "--force"],
    ["install"],
  ]);
});

it("falls back on a corrupt private snapshot and removes it at the end of the batch", async () => {
  const first = await workspace("first");
  await reuse.install(dirname(first), first, ["ci"]);
  const [snapshot] = (await readdir(root)).filter((entry) => entry.startsWith("tsp-emitter-deps-"));
  expect(snapshot).toBeDefined();
  await writeFile(join(root, snapshot!, "node_modules/fixture/index.js"), "corrupt");
  const second = await workspace("second");
  await reuse.install(dirname(second), second, ["ci"]);
  expect(vi.mocked(npmCommand).mock.calls.map(([, args]) => args)).toEqual([["ci"], ["ci"]]);
  expect(await readFile(join(dirname(second), "node_modules/fixture/index.js"), "utf8")).toBe(
    "export const value = 1;\n",
  );
  await reuse.dispose();
  expect((await readdir(root)).filter((entry) => entry.startsWith("tsp-emitter-deps-"))).toEqual(
    [],
  );
});

it("does not share snapshots with a concurrent batch or a bypassed install", async () => {
  const first = await workspace("first");
  const second = await workspace("second");
  const other = new BatchEmitterDependencies();
  try {
    await Promise.all([
      reuse.install(dirname(first), first, ["ci"]),
      other.install(dirname(second), second, ["ci"]),
    ]);
    expect(
      (await readdir(root)).filter((name) => name.startsWith("tsp-emitter-deps-")),
    ).toHaveLength(2);
  } finally {
    await other.dispose();
  }
  vi.stubEnv("TSP_CLIENT_EMITTER_REUSE", "0");
  const third = await workspace("third");
  await reuse.install(dirname(third), third, ["ci"]);
  expect(vi.mocked(npmCommand).mock.calls.map(([, args]) => args)).toEqual([
    ["ci"],
    ["ci"],
    ["ci"],
  ]);
});

it.skipIf(process.platform === "win32")(
  "preserves relative npm bin links and executable files",
  async () => {
    const install = vi.mocked(npmCommand).getMockImplementation()!;
    vi.mocked(npmCommand).mockImplementationOnce(async (dir, args) => {
      await install(dir, args);
      const modules = join(dirname(dir), "node_modules");
      await mkdir(join(modules, ".bin"));
      await chmod(join(modules, "fixture/index.js"), 0o755);
      await symlink("../fixture/index.js", join(modules, ".bin/fixture"));
    });
    const first = await workspace("first");
    const second = await workspace("second");
    await reuse.install(dirname(first), first, ["ci"]);
    await rm(dirname(first), { recursive: true });
    await reuse.install(dirname(second), second, ["ci"]);
    const modules = join(dirname(second), "node_modules");
    expect(await readlink(join(modules, ".bin/fixture"))).toBe("../fixture/index.js");
    expect((await stat(join(modules, ".bin/fixture"))).mode & 0o777).toBe(0o755);
    expect(vi.mocked(npmCommand).mock.calls[1]?.[1]).toEqual(["ci", "--package-lock-only"]);
  },
);

it("keeps full installs when an installed package has a lifecycle hook omitted from the lock", async () => {
  const install = vi.mocked(npmCommand).getMockImplementation()!;
  vi.mocked(npmCommand).mockImplementationOnce(async (dir, args) => {
    await install(dir, args);
    await writeFile(
      join(dirname(dir), "node_modules/fixture/package.json"),
      '{"name":"fixture","version":"1.0.0","scripts":{"postinstall":"example"}}',
    );
  });
  const first = await workspace("first");
  const second = await workspace("second");
  await reuse.install(dirname(first), first, ["ci"]);
  await reuse.install(dirname(second), second, ["ci"]);
  expect(vi.mocked(npmCommand).mock.calls.map(([, args]) => args)).toEqual([["ci"], ["ci"]]);
});

it.each(["predependencies", "dependencies", "postdependencies"])(
  "keeps full installs for npm's root %s hook",
  async (event) => {
    const scripted = { ...manifest, scripts: { [event]: "example" } };
    const first = await workspace("first", scripted);
    const second = await workspace("second", scripted);
    await reuse.install(dirname(first), first, ["ci"]);
    await reuse.install(dirname(second), second, ["ci"]);
    expect(vi.mocked(npmCommand).mock.calls.map(([, args]) => args)).toEqual([["ci"], ["ci"]]);
  },
);

it("does not reuse when npm reports install scripts in its installed metadata", async () => {
  const install = vi.mocked(npmCommand).getMockImplementation()!;
  vi.mocked(npmCommand).mockImplementationOnce(async (dir, args) => {
    await install(dir, args);
    await writeFile(
      join(dirname(dir), "node_modules/.package-lock.json"),
      JSON.stringify({
        ...lock,
        packages: { "node_modules/fixture": { ...dependency, hasInstallScript: true } },
      }),
    );
  });
  const first = await workspace("first");
  const second = await workspace("second");
  await reuse.install(dirname(first), first, ["ci"]);
  await reuse.install(dirname(second), second, ["ci"]);
  expect(vi.mocked(npmCommand).mock.calls.map(([, args]) => args)).toEqual([["ci"], ["ci"]]);
});

it("does not cache an incomplete optional install or a private-feed lock", async () => {
  const optional = {
    ...lock,
    packages: {
      ...lock.packages,
      "node_modules/optional-fixture": { ...dependency, optional: true },
    },
  };
  const first = await workspace("first", manifest, optional);
  const second = await workspace("second", manifest, optional);
  await reuse.install(dirname(first), first, ["ci"]);
  await reuse.install(dirname(second), second, ["ci"]);
  const privateLock = {
    ...lock,
    packages: {
      ...lock.packages,
      "node_modules/fixture": { ...dependency, resolved: "https://registry.example/fixture.tgz" },
    },
  };
  const third = await workspace("third", manifest, privateLock);
  await reuse.install(dirname(third), third, ["ci"]);
  expect(vi.mocked(npmCommand).mock.calls.map(([, args]) => args)).toEqual([
    ["ci"],
    ["ci"],
    ["ci"],
  ]);
  expect((await readdir(root)).filter((entry) => entry.startsWith("tsp-emitter-deps-"))).toEqual(
    [],
  );
});

it("lets ordinary npm report a malformed lockfile", async () => {
  const dir = await workspace("invalid");
  await writeFile(join(dirname(dir), "package-lock.json"), "not JSON");
  vi.mocked(npmCommand).mockRejectedValueOnce(new Error("ordinary npm ci rejected the lock"));
  await expect(reuse.install(dirname(dir), dir, ["ci"])).rejects.toThrow(
    "ordinary npm ci rejected the lock",
  );
  expect(vi.mocked(npmCommand).mock.calls).toEqual([[dir, ["ci"]]]);
});

it("falls back when npm rejects warm lock-only validation", async () => {
  const first = await workspace("first");
  await reuse.install(dirname(first), first, ["ci"]);
  const second = await workspace("second");
  vi.mocked(npmCommand).mockRejectedValueOnce(new Error("lock-only failed"));
  await reuse.install(dirname(second), second, ["ci"]);
  expect(vi.mocked(npmCommand).mock.calls.map(([, args]) => args)).toEqual([
    ["ci"],
    ["ci", "--package-lock-only"],
    ["ci"],
  ]);
  await expect(
    access(join(dirname(second), "node_modules/fixture/package.json")),
  ).resolves.toBeUndefined();
});

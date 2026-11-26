import { chmod, mkdir, mkdtemp, readdir, rm, symlink, writeFile } from "fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { normalizePath } from "@typespec/compiler";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { discoverEntrypointFile } from "../src/typespec.js";

vi.mock("fs/promises", async (original) => {
  const actual = await original<typeof import("fs/promises")>();
  return { ...actual, readdir: vi.fn(actual.readdir) };
});

let root: string;
beforeEach(async () => {
  root = await mkdtemp(join(tmpdir(), "tsp-entrypoint-"));
  await mkdir(join(root, "nested"));
  await mkdir(join(root, "historical", "2020"), { recursive: true });
  await writeFile(join(root, "nested", "chosen.tsp"), "");
  await writeFile(join(root, "historical", "2020", "resource.json"), "{}");
  vi.clearAllMocks();
});

afterEach(async () => rm(root, { recursive: true, force: true }));

async function legacy(entrypoint?: string) {
  const fs = await vi.importActual<typeof import("fs/promises")>("fs/promises");
  const names = (await fs.readdir(root, { recursive: true })).map(normalizePath);
  const candidates = entrypoint ? [normalizePath(entrypoint)] : ["main.tsp", "client.tsp"];
  return candidates.find((name) => names.includes(name));
}

it.each(["nested/chosen.tsp", "nested\\chosen.tsp", "./nested/../nested/chosen.tsp"])(
  "looks up %s without traversing unrelated directories",
  async (entrypoint) => {
    expect(await discoverEntrypointFile(root, entrypoint)).toBe("nested/chosen.tsp");
    expect(vi.mocked(readdir).mock.calls.map(([path]) => path)).toEqual([
      root,
      join(root, "nested"),
    ]);
    expect(
      vi
        .mocked(readdir)
        .mock.calls.some(([, options]) => typeof options === "object" && options?.recursive),
    ).toBe(false);
  },
);

it("finds a root override and prefers root main to client without a recursive read", async () => {
  await writeFile(join(root, "main.tsp"), "");
  await writeFile(join(root, "client.tsp"), "");
  expect(await discoverEntrypointFile(root)).toBe("main.tsp");
  expect(await discoverEntrypointFile(root, "client.tsp")).toBe("client.tsp");
  expect(vi.mocked(readdir).mock.calls.map(([path]) => path)).toEqual([root, root]);
  expect(
    vi
      .mocked(readdir)
      .mock.calls.some(([, options]) => typeof options === "object" && options?.recursive),
  ).toBe(false);
});

it("retains client-only behavior and the exact missing-entrypoint diagnostics", async () => {
  await writeFile(join(root, "nested", "main.tsp"), "");
  await expect(discoverEntrypointFile(root)).rejects.toThrow("No main.tsp or client.tsp found");
  await writeFile(join(root, "client.tsp"), "");
  expect(await discoverEntrypointFile(root)).toBe("client.tsp");
  expect(await discoverEntrypointFile(root, "")).toBe("client.tsp");
  for (const name of [
    "missing\\chosen.tsp",
    "Nested/chosen.tsp",
    "nested/chosen.tsp/",
    "../client.tsp",
  ]) {
    await expect(discoverEntrypointFile(root, name)).rejects.toThrow(
      `Couldn't find the entrypoint file specified in tsp-location.yaml: "${name}". Please verify that the entrypoint file name is correct.`,
    );
  }
  await expect(discoverEntrypointFile(join(root, "absent"))).rejects.toMatchObject({
    code: "ENOENT",
  });
});

it.skipIf(process.platform === "win32" || process.getuid?.() === 0)(
  "preserves an error on a matched, unreadable directory entrypoint",
  async () => {
    const candidate = join(root, "main.tsp");
    await mkdir(candidate);
    await writeFile(join(candidate, "main.tsp"), "");
    await chmod(candidate, 0o111);
    try {
      await expect(legacy()).rejects.toMatchObject({ code: "EACCES" });
      await expect(discoverEntrypointFile(root)).rejects.toMatchObject({ code: "EACCES" });
      await expect(discoverEntrypointFile(root, "main.tsp")).rejects.toMatchObject({
        code: "EACCES",
      });
    } finally {
      await chmod(candidate, 0o755);
    }
  },
);

it("keeps filesystem errors ahead of malformed override errors", async () => {
  const malformed = true as unknown as string;
  await expect(discoverEntrypointFile(join(root, "absent"), malformed)).rejects.toMatchObject({
    code: "ENOENT",
  });
  await expect(discoverEntrypointFile(root, malformed)).rejects.toBeInstanceOf(TypeError);
  const empty = join(root, "empty");
  await mkdir(empty);
  await expect(discoverEntrypointFile(empty, malformed)).rejects.toThrow(
    'Couldn\'t find the entrypoint file specified in tsp-location.yaml: "true". Please verify that the entrypoint file name is correct.',
  );
});

it.skipIf(process.platform === "win32")(
  "preserves legacy name matching for symlinks and literal separators",
  async () => {
    await writeFile(join(root, "client.tsp"), "");
    await mkdir(join(root, "directory.tsp"));
    await symlink("missing", join(root, "dangling.tsp"));
    await symlink("nested", join(root, "nested-link"));
    await writeFile(join(root, "aliased\\chosen.tsp"), "");
    await writeFile(join(root, "nested", "..\\main.tsp"), "");
    for (const name of [
      undefined,
      "directory.tsp",
      "dangling.tsp",
      "nested-link/chosen.tsp",
      "aliased/chosen.tsp",
    ]) {
      const expected = await legacy(name);
      if (expected) expect(await discoverEntrypointFile(root, name)).toBe(expected);
      else
        await expect(discoverEntrypointFile(root, name)).rejects.toThrow(
          "Couldn't find the entrypoint file",
        );
    }
  },
);

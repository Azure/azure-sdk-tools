import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  _setGhCliAvailableForTests,
  _setGhRunnerForTests,
  buildRawUrl,
  buildTreeApiUrl,
  getGitHubToken,
  listTree,
  resolveGitHubFetchOptions,
  tryFetchFileFromGitHub,
  tryFetchSpecFromGitHub,
} from "../src/githubFetch.js";
import { mkdtemp, readFile, readdir, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";

const ORIGINAL_FETCH = globalThis.fetch;
const ORIGINAL_GH_TOKEN = process.env["GH_TOKEN"];
const ORIGINAL_GITHUB_TOKEN = process.env["GITHUB_TOKEN"];

function makeJsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}

function makeBufferResponse(body: string | Buffer, status = 200): Response {
  return new Response(body as any, { status });
}

function unsetTokens(): void {
  delete process.env["GITHUB_TOKEN"];
  delete process.env["GH_TOKEN"];
}

function restoreTokens(): void {
  if (ORIGINAL_GITHUB_TOKEN === undefined) delete process.env["GITHUB_TOKEN"];
  else process.env["GITHUB_TOKEN"] = ORIGINAL_GITHUB_TOKEN;
  if (ORIGINAL_GH_TOKEN === undefined) delete process.env["GH_TOKEN"];
  else process.env["GH_TOKEN"] = ORIGINAL_GH_TOKEN;
}

afterEach(() => {
  globalThis.fetch = ORIGINAL_FETCH;
  _setGhCliAvailableForTests(undefined);
  _setGhRunnerForTests(undefined);
  restoreTokens();
});

describe("getGitHubToken", () => {
  beforeEach(unsetTokens);

  it("prefers GITHUB_TOKEN over GH_TOKEN", () => {
    process.env["GITHUB_TOKEN"] = "ghp_primary";
    process.env["GH_TOKEN"] = "ghp_secondary";
    expect(getGitHubToken()).toBe("ghp_primary");
  });

  it("falls back to GH_TOKEN when GITHUB_TOKEN is unset", () => {
    process.env["GH_TOKEN"] = "ghp_secondary";
    expect(getGitHubToken()).toBe("ghp_secondary");
  });

  it("returns undefined when no token env var is set", () => {
    expect(getGitHubToken()).toBeUndefined();
  });
});

describe("URL builders", () => {
  it("buildRawUrl produces raw.githubusercontent.com URLs and percent-encodes segments", () => {
    expect(
      buildRawUrl(
        "Azure/azure-rest-api-specs",
        "abc123",
        "specification/contoso/Contoso.WidgetManager/main.tsp",
      ),
    ).toBe(
      "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/abc123/specification/contoso/Contoso.WidgetManager/main.tsp",
    );

    // -pr repo and a path segment that needs encoding
    expect(
      buildRawUrl("Azure/azure-rest-api-specs-pr", "deadbeef", "specification/foo bar/main.tsp"),
    ).toBe(
      "https://raw.githubusercontent.com/Azure/azure-rest-api-specs-pr/deadbeef/specification/foo%20bar/main.tsp",
    );
  });

  it("buildTreeApiUrl appends recursive query when requested", () => {
    expect(buildTreeApiUrl("Azure/azure-rest-api-specs", "treeSha", true)).toBe(
      "https://api.github.com/repos/Azure/azure-rest-api-specs/git/trees/treeSha?recursive=1",
    );
    expect(buildTreeApiUrl("Azure/azure-rest-api-specs", "treeSha", false)).toBe(
      "https://api.github.com/repos/Azure/azure-rest-api-specs/git/trees/treeSha",
    );
  });
});

describe("resolveGitHubFetchOptions", () => {
  beforeEach(unsetTokens);

  it("uses gh CLI when available and picks up env token", async () => {
    _setGhCliAvailableForTests(true);
    process.env["GITHUB_TOKEN"] = "ghp_env";
    const opts = await resolveGitHubFetchOptions();
    expect(opts.useGhCli).toBe(true);
    expect(opts.token).toBe("ghp_env");
  });

  it("falls back to fetch with no token when neither gh nor env token is set", async () => {
    _setGhCliAvailableForTests(false);
    const opts = await resolveGitHubFetchOptions();
    expect(opts.useGhCli).toBe(false);
    expect(opts.token).toBeUndefined();
  });
});

describe("listTree (REST path)", () => {
  beforeEach(() => {
    unsetTokens();
    _setGhCliAvailableForTests(false);
  });

  it("walks segments to subtree and returns blob entries when not truncated", async () => {
    const calls: string[] = [];
    const fetchMock = vi.fn(async (input: any) => {
      const url = String(input);
      calls.push(url);
      if (url.endsWith("/repos/Azure/azure-rest-api-specs/commits/abc")) {
        return makeJsonResponse({ commit: { tree: { sha: "rootTreeSha" } } });
      }
      if (url.endsWith("/git/trees/rootTreeSha")) {
        return makeJsonResponse({
          sha: "rootTreeSha",
          truncated: false,
          tree: [{ path: "specification", type: "tree", sha: "specTreeSha" }],
        });
      }
      if (url.endsWith("/git/trees/specTreeSha")) {
        return makeJsonResponse({
          sha: "specTreeSha",
          truncated: false,
          tree: [{ path: "contoso", type: "tree", sha: "contosoTreeSha" }],
        });
      }
      if (url.endsWith("/git/trees/contosoTreeSha?recursive=1")) {
        return makeJsonResponse({
          sha: "contosoTreeSha",
          truncated: false,
          tree: [
            { path: "main.tsp", type: "blob", sha: "blob1", size: 10 },
            { path: "models", type: "tree", sha: "modelsTreeSha" },
            { path: "models/foo.tsp", type: "blob", sha: "blob2", size: 20 },
          ],
        });
      }
      throw new Error(`Unexpected fetch URL: ${url}`);
    });
    globalThis.fetch = fetchMock as any;

    const blobs = await listTree("Azure/azure-rest-api-specs", "abc", "specification/contoso", {
      useGhCli: false,
    });
    expect(blobs.map((b) => b.path).sort()).toEqual([
      "specification/contoso/main.tsp",
      "specification/contoso/models/foo.tsp",
    ]);
    expect(fetchMock).toHaveBeenCalled();
  });

  it("recurses into subtrees when the recursive listing is truncated", async () => {
    const fetchMock = vi.fn(async (input: any) => {
      const url = String(input);
      if (url.endsWith("/commits/abc")) {
        return makeJsonResponse({ commit: { tree: { sha: "rootTreeSha" } } });
      }
      if (url.endsWith("/git/trees/rootTreeSha")) {
        return makeJsonResponse({
          sha: "rootTreeSha",
          truncated: false,
          tree: [{ path: "specification", type: "tree", sha: "specTreeSha" }],
        });
      }
      if (url.endsWith("/git/trees/specTreeSha?recursive=1")) {
        return makeJsonResponse({
          sha: "specTreeSha",
          truncated: true,
          tree: [{ path: "x.tsp", type: "blob", sha: "blobX", size: 1 }],
        });
      }
      if (url.endsWith("/git/trees/specTreeSha")) {
        return makeJsonResponse({
          sha: "specTreeSha",
          truncated: false,
          tree: [
            { path: "x.tsp", type: "blob", sha: "blobX", size: 1 },
            { path: "sub", type: "tree", sha: "subTreeSha" },
          ],
        });
      }
      if (url.endsWith("/git/trees/subTreeSha?recursive=1")) {
        return makeJsonResponse({
          sha: "subTreeSha",
          truncated: false,
          tree: [{ path: "y.tsp", type: "blob", sha: "blobY", size: 2 }],
        });
      }
      throw new Error(`Unexpected fetch URL: ${url}`);
    });
    globalThis.fetch = fetchMock as any;

    const blobs = await listTree("Azure/azure-rest-api-specs", "abc", "specification", {
      useGhCli: false,
    });
    expect(blobs.map((b) => b.path).sort()).toEqual([
      "specification/sub/y.tsp",
      "specification/x.tsp",
    ]);
  });

  it("propagates non-2xx responses as errors", async () => {
    globalThis.fetch = vi.fn(async () => makeJsonResponse({ message: "Not Found" }, 404)) as any;
    await expect(
      listTree("Azure/azure-rest-api-specs", "abc", "specification", { useGhCli: false }),
    ).rejects.toThrow(/404/);
  });
});

describe("tryFetchSpecFromGitHub", () => {
  beforeEach(() => {
    unsetTokens();
    _setGhCliAvailableForTests(false);
  });

  it("returns false when the underlying fetch fails (so callers can fall back)", async () => {
    globalThis.fetch = vi.fn(async () => makeJsonResponse({}, 500)) as any;
    const result = await tryFetchSpecFromGitHub({
      repo: "Azure/azure-rest-api-specs",
      commit: "abc",
      directory: "specification/contoso",
      destRoot: "C:/tmp/should-not-be-written",
    });
    expect(result).toBe(false);
  });

  it("downloads every blob into the destination root preserving repo-relative paths", async () => {
    const tmp = await mkdtemp(join(tmpdir(), "tspc-gh-"));
    try {
      const fetchMock = vi.fn(async (input: any) => {
        const url = String(input);
        if (url.endsWith("/commits/abc")) {
          return makeJsonResponse({ commit: { tree: { sha: "rootTreeSha" } } });
        }
        if (url.endsWith("/git/trees/rootTreeSha")) {
          return makeJsonResponse({
            sha: "rootTreeSha",
            truncated: false,
            tree: [{ path: "specification", type: "tree", sha: "specTreeSha" }],
          });
        }
        if (url.endsWith("/git/trees/specTreeSha")) {
          return makeJsonResponse({
            sha: "specTreeSha",
            truncated: false,
            tree: [{ path: "contoso", type: "tree", sha: "contosoTreeSha" }],
          });
        }
        if (url.endsWith("/git/trees/contosoTreeSha?recursive=1")) {
          return makeJsonResponse({
            sha: "contosoTreeSha",
            truncated: false,
            tree: [
              { path: "main.tsp", type: "blob", sha: "b1", size: 11 },
              { path: "models/foo.tsp", type: "blob", sha: "b2", size: 12 },
            ],
          });
        }
        if (url.includes("raw.githubusercontent.com") && url.endsWith("/main.tsp")) {
          return makeBufferResponse("main contents");
        }
        if (url.includes("raw.githubusercontent.com") && url.endsWith("/models/foo.tsp")) {
          return makeBufferResponse("foo contents");
        }
        throw new Error(`Unexpected fetch URL: ${url}`);
      });
      globalThis.fetch = fetchMock as any;

      const ok = await tryFetchSpecFromGitHub({
        repo: "Azure/azure-rest-api-specs",
        commit: "abc",
        directory: "specification/contoso",
        destRoot: tmp,
      });
      expect(ok).toBe(true);

      const main = await readFile(join(tmp, "specification/contoso/main.tsp"), "utf-8");
      expect(main).toBe("main contents");
      const foo = await readFile(join(tmp, "specification/contoso/models/foo.tsp"), "utf-8");
      expect(foo).toBe("foo contents");
    } finally {
      await rm(tmp, { recursive: true, force: true });
    }
  });
});

describe("tryFetchFileFromGitHub", () => {
  beforeEach(() => {
    unsetTokens();
    _setGhCliAvailableForTests(false);
  });

  it("downloads a single file via raw URL", async () => {
    const tmp = await mkdtemp(join(tmpdir(), "tspc-gh-"));
    try {
      const fetchMock = vi.fn(async (input: any) => {
        const url = String(input);
        if (url.includes("raw.githubusercontent.com") && url.endsWith("/tspconfig.yaml")) {
          return makeBufferResponse("name: tspconfig\n");
        }
        throw new Error(`Unexpected fetch URL: ${url}`);
      });
      globalThis.fetch = fetchMock as any;

      const dest = join(tmp, "specification/contoso/tspconfig.yaml");
      const ok = await tryFetchFileFromGitHub({
        repo: "Azure/azure-rest-api-specs",
        commit: "abc",
        path: "specification/contoso/tspconfig.yaml",
        destFile: dest,
      });
      expect(ok).toBe(true);
      expect(await readFile(dest, "utf-8")).toBe("name: tspconfig\n");
    } finally {
      await rm(tmp, { recursive: true, force: true });
    }
  });

  it("returns false on 404 so callers can fall back to git", async () => {
    globalThis.fetch = vi.fn(async () => makeBufferResponse("not found", 404)) as any;
    const ok = await tryFetchFileFromGitHub({
      repo: "Azure/azure-rest-api-specs",
      commit: "abc",
      path: "specification/missing/tspconfig.yaml",
      destFile: join(tmpdir(), "should-not-be-written.yaml"),
    });
    expect(ok).toBe(false);
  });
});

describe("authenticated fetch", () => {
  beforeEach(() => {
    unsetTokens();
    _setGhCliAvailableForTests(false);
  });

  it("sends a Bearer Authorization header when GITHUB_TOKEN is set", async () => {
    process.env["GITHUB_TOKEN"] = "ghp_primary";
    let observedAuth: string | undefined;
    globalThis.fetch = vi.fn(async (input: any, init: any) => {
      const headers = new Headers(init?.headers);
      observedAuth = headers.get("authorization") ?? undefined;
      const url = String(input);
      if (url.includes("raw.githubusercontent.com")) {
        return makeBufferResponse("hello");
      }
      throw new Error(`Unexpected fetch URL: ${url}`);
    }) as any;

    const tmp = await mkdtemp(join(tmpdir(), "tspc-gh-"));
    try {
      const dest = join(tmp, "tspconfig.yaml");
      const ok = await tryFetchFileFromGitHub({
        repo: "Azure/azure-rest-api-specs",
        commit: "abc",
        path: "specification/contoso/tspconfig.yaml",
        destFile: dest,
      });
      expect(ok).toBe(true);
      expect(observedAuth).toBe("Bearer ghp_primary");
    } finally {
      await rm(tmp, { recursive: true, force: true });
    }
  });
});

describe("destination cleanup on failure", () => {
  beforeEach(() => {
    unsetTokens();
    _setGhCliAvailableForTests(false);
  });

  it("empties destRoot when a spec fetch fails partway through", async () => {
    const tmp = await mkdtemp(join(tmpdir(), "tspc-gh-"));
    try {
      const fetchMock = vi.fn(async (input: any) => {
        const url = String(input);
        if (url.endsWith("/commits/abc")) {
          return makeJsonResponse({ commit: { tree: { sha: "rootTreeSha" } } });
        }
        if (url.endsWith("/git/trees/rootTreeSha")) {
          return makeJsonResponse({
            sha: "rootTreeSha",
            truncated: false,
            tree: [{ path: "specification", type: "tree", sha: "specTreeSha" }],
          });
        }
        if (url.endsWith("/git/trees/specTreeSha")) {
          return makeJsonResponse({
            sha: "specTreeSha",
            truncated: false,
            tree: [{ path: "contoso", type: "tree", sha: "contosoTreeSha" }],
          });
        }
        if (url.endsWith("/git/trees/contosoTreeSha?recursive=1")) {
          return makeJsonResponse({
            sha: "contosoTreeSha",
            truncated: false,
            tree: [
              { path: "main.tsp", type: "blob", sha: "b1", size: 11 },
              { path: "models/foo.tsp", type: "blob", sha: "b2", size: 12 },
            ],
          });
        }
        if (url.includes("raw.githubusercontent.com") && url.endsWith("/main.tsp")) {
          return makeBufferResponse("partial-write");
        }
        // fail the second download to simulate a partial failure
        if (url.includes("raw.githubusercontent.com") && url.endsWith("/models/foo.tsp")) {
          return makeBufferResponse("server error", 500);
        }
        throw new Error(`Unexpected fetch URL: ${url}`);
      });
      globalThis.fetch = fetchMock as any;

      const ok = await tryFetchSpecFromGitHub({
        repo: "Azure/azure-rest-api-specs",
        commit: "abc",
        directory: "specification/contoso",
        destRoot: tmp,
      });
      expect(ok).toBe(false);
      // destRoot must be empty so the git clone fallback can write into it cleanly.
      const remaining = await readdir(tmp);
      expect(remaining).toEqual([]);
    } finally {
      await rm(tmp, { recursive: true, force: true });
    }
  });

  it("empties destRoot when a single-file fetch fails", async () => {
    const tmp = await mkdtemp(join(tmpdir(), "tspc-gh-"));
    try {
      globalThis.fetch = vi.fn(async () => makeBufferResponse("not found", 404)) as any;
      const ok = await tryFetchFileFromGitHub({
        repo: "Azure/azure-rest-api-specs",
        commit: "abc",
        path: "specification/missing/tspconfig.yaml",
        destFile: join(tmp, "specification/missing/tspconfig.yaml"),
        destRoot: tmp,
      });
      expect(ok).toBe(false);
      const remaining = await readdir(tmp);
      expect(remaining).toEqual([]);
    } finally {
      await rm(tmp, { recursive: true, force: true });
    }
  });
});

describe("gh CLI -> REST API fallback", () => {
  beforeEach(() => {
    unsetTokens();
    _setGhCliAvailableForTests(true);
  });

  it("retries via REST when the gh CLI strategy fails", async () => {
    // Make every gh subprocess invocation fail (simulates `gh` installed but
    // unauthenticated, network blocked, etc.).
    _setGhRunnerForTests(async () => ({
      code: 1,
      stdout: Buffer.alloc(0),
      stderr: "gh: not authenticated",
    }));

    let restCalls = 0;
    globalThis.fetch = vi.fn(async (input: any) => {
      restCalls++;
      const url = String(input);
      if (url.endsWith("/commits/abc")) {
        return makeJsonResponse({ commit: { tree: { sha: "rootTreeSha" } } });
      }
      if (url.endsWith("/git/trees/rootTreeSha")) {
        return makeJsonResponse({
          sha: "rootTreeSha",
          truncated: false,
          tree: [{ path: "specification", type: "tree", sha: "specTreeSha" }],
        });
      }
      if (url.endsWith("/git/trees/specTreeSha")) {
        return makeJsonResponse({
          sha: "specTreeSha",
          truncated: false,
          tree: [{ path: "contoso", type: "tree", sha: "contosoTreeSha" }],
        });
      }
      if (url.endsWith("/git/trees/contosoTreeSha?recursive=1")) {
        return makeJsonResponse({
          sha: "contosoTreeSha",
          truncated: false,
          tree: [{ path: "main.tsp", type: "blob", sha: "b1", size: 11 }],
        });
      }
      if (url.includes("raw.githubusercontent.com") && url.endsWith("/main.tsp")) {
        return makeBufferResponse("rest contents");
      }
      throw new Error(`Unexpected fetch URL: ${url}`);
    }) as any;

    const tmp = await mkdtemp(join(tmpdir(), "tspc-gh-"));
    try {
      const ok = await tryFetchSpecFromGitHub({
        repo: "Azure/azure-rest-api-specs",
        commit: "abc",
        directory: "specification/contoso",
        destRoot: tmp,
      });
      expect(ok).toBe(true);
      expect(restCalls).toBeGreaterThan(0);
      const main = await readFile(join(tmp, "specification/contoso/main.tsp"), "utf-8");
      expect(main).toBe("rest contents");
    } finally {
      await rm(tmp, { recursive: true, force: true });
    }
  });
});

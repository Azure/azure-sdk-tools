import { describe, test, expect, vi, afterEach } from "vitest";

process.env.KEYVAULT_NAME = "test-vault";
process.env.KEYVAULT_KEY_NAME = "test-key";
process.env.GITHUB_APP_NUMERIC_ID = "12345";
process.env.GITHUB_INSTALL_OWNER = "TestOrg";

vi.mock("@azure/identity", () => ({
  DefaultAzureCredential: vi.fn().mockImplementation(function () {
    return {
      getToken: vi.fn().mockResolvedValue({ token: "mock-bearer-token" }),
    };
  }),
}));

import {
  devopsRequest,
  runWiql,
  fetchWorkItemsBatch,
  fetchPackageWorkItems,
  fetchReleasedPackageCsvs,
  getAuthHeader,
} from "../lib/devops-api.js";

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("getAuthHeader", () => {
  test("returns Bearer token from DefaultAzureCredential", async () => {
    const header = await getAuthHeader();
    expect(header).toBe("Bearer mock-bearer-token");
  });
});

describe("devopsRequest", () => {
  test("GET request returns parsed JSON", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        text: () => Promise.resolve(JSON.stringify({ count: 5 })),
        headers: new Headers(),
      }),
    );
    const result = await devopsRequest("https://dev.azure.com/test", "GET");
    expect(result).toEqual({ count: 5 });
  });

  test("defaults to GET when method is not provided", async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      text: () => Promise.resolve(JSON.stringify({ items: [] })),
      headers: new Headers(),
    });
    vi.stubGlobal("fetch", mockFetch);
    const result = await devopsRequest("https://dev.azure.com/test");
    expect(result).toEqual({ items: [] });
    expect(mockFetch.mock.calls[0][1].method).toBe("GET");
  });

  test("POST with body sends JSON", async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      text: () => Promise.resolve(JSON.stringify({ success: true })),
      headers: new Headers(),
    });
    vi.stubGlobal("fetch", mockFetch);

    const body = { query: "SELECT [System.Id] FROM WorkItems" };
    const result = await devopsRequest(
      "https://dev.azure.com/test",
      "POST",
      body,
    );
    expect(result).toEqual({ success: true });
    expect(mockFetch).toHaveBeenCalledTimes(1);
    const callArgs = mockFetch.mock.calls[0];
    expect(JSON.parse(callArgs[1].body)).toEqual(body);
  });

  test("non-200 response throws error", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 404,
        text: () => Promise.resolve("Not Found"),
        headers: new Headers(),
      }),
    );
    await expect(
      devopsRequest("https://dev.azure.com/test", "GET"),
    ).rejects.toThrow("DevOps 404");
  });

  test("response that isn't valid JSON returns text", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        text: () => Promise.resolve("plain text response"),
        headers: new Headers(),
      }),
    );
    const result = await devopsRequest("https://dev.azure.com/test", "GET");
    expect(result).toBe("plain text response");
  });

  test("options.returnHeaders returns { body, headers }", async () => {
    const headers = new Headers({ "x-request-id": "abc123" });
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        text: () => Promise.resolve(JSON.stringify({ data: "value" })),
        headers,
      }),
    );
    const result = await devopsRequest(
      "https://dev.azure.com/test",
      "GET",
      null,
      { returnHeaders: true },
    );
    expect(result.body).toEqual({ data: "value" });
    expect(result.headers).toHaveProperty("x-request-id", "abc123");
  });
});

describe("runWiql", () => {
  test("returns work item IDs from WIQL response", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        text: () =>
          Promise.resolve(
            JSON.stringify({
              workItems: [{ id: 1 }, { id: 2 }, { id: 3 }],
            }),
          ),
        headers: new Headers(),
      }),
    );
    const ids = await runWiql("SELECT [System.Id] FROM WorkItems");
    expect(ids).toEqual([1, 2, 3]);
  });

  test("returns empty array when no workItems in response", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        text: () => Promise.resolve(JSON.stringify({})),
        headers: new Headers(),
      }),
    );
    const ids = await runWiql("SELECT [System.Id] FROM WorkItems");
    expect(ids).toEqual([]);
  });
});

describe("fetchWorkItemsBatch", () => {
  test("empty ids returns empty array", async () => {
    const result = await fetchWorkItemsBatch([]);
    expect(result).toEqual([]);
  });

  test("fetches work items with field selection", async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      text: () =>
        Promise.resolve(
          JSON.stringify({
            value: [
              { id: 1, fields: { "System.Title": "Item 1" } },
              { id: 2, fields: { "System.Title": "Item 2" } },
            ],
          }),
        ),
      headers: new Headers(),
    });
    vi.stubGlobal("fetch", mockFetch);

    const result = await fetchWorkItemsBatch(
      [1, 2],
      ["System.Title", "System.State"],
    );
    expect(result).toHaveLength(2);
    expect(result[0].id).toBe(1);
    // Should have fields param in URL, not $expand
    const calledUrl = mockFetch.mock.calls[0][0];
    expect(calledUrl).toContain("fields=");
    expect(calledUrl).not.toContain("$expand");
  });

  test("fetches work items with expand=All when no fields param", async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      text: () =>
        Promise.resolve(
          JSON.stringify({
            value: [{ id: 1, fields: {}, relations: [] }],
          }),
        ),
      headers: new Headers(),
    });
    vi.stubGlobal("fetch", mockFetch);

    const result = await fetchWorkItemsBatch([1]);
    expect(result).toHaveLength(1);
    const calledUrl = mockFetch.mock.calls[0][0];
    expect(calledUrl).toContain("$expand=All");
    expect(calledUrl).not.toContain("fields=");
  });

  test("batches in groups of 200", async () => {
    const ids = Array.from({ length: 450 }, (_, i) => i + 1);
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      text: () => Promise.resolve(JSON.stringify({ value: [{ id: 1 }] })),
      headers: new Headers(),
    });
    vi.stubGlobal("fetch", mockFetch);

    const result = await fetchWorkItemsBatch(ids);
    // 450 items should be 3 batches: 200, 200, 50
    // Each batch triggers 1 fetch call from devopsRequest (+ 1 for getAuthHeader internally)
    // devopsRequest calls fetch once per batch
    expect(mockFetch.mock.calls.length).toBe(3);
    expect(result).toHaveLength(3); // 1 item per batch response
  });
});

describe("fetchPackageWorkItems", () => {
  test("empty input returns empty Map", async () => {
    const result = await fetchPackageWorkItems([]);
    expect(result).toBeInstanceOf(Map);
    expect(result.size).toBe(0);
  });

  test("fetches and maps package data correctly", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation((url) => {
        // WIQL query response
        if (url.includes("wiql")) {
          return Promise.resolve({
            ok: true,
            status: 200,
            text: () =>
              Promise.resolve(JSON.stringify({ workItems: [{ id: 100 }] })),
            headers: new Headers(),
          });
        }
        // Work items batch response
        return Promise.resolve({
          ok: true,
          status: 200,
          text: () =>
            Promise.resolve(
              JSON.stringify({
                value: [
                  {
                    id: 100,
                    fields: {
                      "Custom.Package": "azure-core",
                      "Custom.Language": "Python",
                      "Custom.PackageVersion": "1.2.0",
                      "Custom.APIReviewStatus": "Approved",
                      "Custom.PackageNameApprovalStatus": "Approved",
                      "System.ChangedDate": "2024-06-01T00:00:00Z",
                    },
                  },
                ],
              }),
            ),
          headers: new Headers(),
        });
      }),
    );

    const result = await fetchPackageWorkItems([
      { pkg: "azure-core", lang: "Python" },
    ]);
    expect(result).toBeInstanceOf(Map);
    expect(result.has("azure-core|python")).toBe(true);
    const entry = result.get("azure-core|python");
    expect(entry.version).toBe("1.2.0");
    expect(entry.apiReviewStatus).toBe("Approved");
    expect(entry.namespaceApproval).toBe("Approved");
  });

  test("normalizes language key to lowercase for Go packages stored as 'go' in ADO", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation((url) => {
        if (url.includes("wiql")) {
          return Promise.resolve({
            ok: true,
            status: 200,
            text: () =>
              Promise.resolve(JSON.stringify({ workItems: [{ id: 200 }] })),
            headers: new Headers(),
          });
        }
        return Promise.resolve({
          ok: true,
          status: 200,
          text: () =>
            Promise.resolve(
              JSON.stringify({
                value: [
                  {
                    id: 200,
                    fields: {
                      "Custom.Package": "azure-sdk-go",
                      "Custom.Language": "go",
                      "Custom.PackageVersion": "1.5.0",
                      "Custom.APIReviewStatus": "Approved",
                      "Custom.PackageNameApprovalStatus": "Approved",
                      "System.ChangedDate": "2024-06-01T00:00:00Z",
                    },
                  },
                ],
              }),
            ),
          headers: new Headers(),
        });
      }),
    );

    const result = await fetchPackageWorkItems([
      { pkg: "azure-sdk-go", lang: "Go" },
    ]);
    expect(result).toBeInstanceOf(Map);
    // Key should be lowercase regardless of how ADO stores the language
    expect(result.has("azure-sdk-go|go")).toBe(true);
    const entry = result.get("azure-sdk-go|go");
    expect(entry.version).toBe("1.5.0");
  });

  test("keeps most recent by changedDate when duplicates", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation((url) => {
        if (url.includes("wiql")) {
          return Promise.resolve({
            ok: true,
            status: 200,
            text: () =>
              Promise.resolve(
                JSON.stringify({ workItems: [{ id: 100 }, { id: 101 }] }),
              ),
            headers: new Headers(),
          });
        }
        return Promise.resolve({
          ok: true,
          status: 200,
          text: () =>
            Promise.resolve(
              JSON.stringify({
                value: [
                  {
                    id: 100,
                    fields: {
                      "Custom.Package": "azure-core",
                      "Custom.Language": "Python",
                      "Custom.PackageVersion": "1.0.0",
                      "System.ChangedDate": "2024-01-01T00:00:00Z",
                    },
                  },
                  {
                    id: 101,
                    fields: {
                      "Custom.Package": "azure-core",
                      "Custom.Language": "Python",
                      "Custom.PackageVersion": "2.0.0",
                      "System.ChangedDate": "2024-06-01T00:00:00Z",
                    },
                  },
                ],
              }),
            ),
          headers: new Headers(),
        });
      }),
    );

    const result = await fetchPackageWorkItems([
      { pkg: "azure-core", lang: "Python" },
    ]);
    const entry = result.get("azure-core|python");
    expect(entry.version).toBe("2.0.0");
  });

  test("skips language when WIQL returns no work items", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation((url) => {
        if (url.includes("wiql")) {
          return Promise.resolve({
            ok: true,
            status: 200,
            text: () => Promise.resolve(JSON.stringify({ workItems: [] })),
            headers: new Headers(),
          });
        }
        return Promise.resolve({
          ok: true,
          status: 200,
          text: () => Promise.resolve(JSON.stringify({ value: [] })),
          headers: new Headers(),
        });
      }),
    );

    const result = await fetchPackageWorkItems([
      { pkg: "azure-core", lang: "Python" },
    ]);
    expect(result.size).toBe(0);
  });

  test("handles errors gracefully (logs warning)", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockRejectedValue(new Error("Network error")),
    );
    const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});

    const result = await fetchPackageWorkItems([
      { pkg: "azure-core", lang: "Python" },
    ]);
    expect(result).toBeInstanceOf(Map);
    expect(result.size).toBe(0);
    expect(warnSpy).toHaveBeenCalled();

    warnSpy.mockRestore();
  });

  test("filters out empty package names", async () => {
    const result = await fetchPackageWorkItems([
      { pkg: "", lang: "Python" },
      { pkg: null, lang: "Java" },
    ]);
    expect(result.size).toBe(0);
  });

  test("continues when WIQL returns no IDs for a batch", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation((url) => {
        if (url.includes("wiql")) {
          // Return empty workItems array
          return Promise.resolve({
            ok: true,
            status: 200,
            text: () => Promise.resolve(JSON.stringify({ workItems: [] })),
            headers: new Headers(),
          });
        }
        return Promise.resolve({
          ok: true,
          status: 200,
          text: () => Promise.resolve(JSON.stringify({ value: [] })),
          headers: new Headers(),
        });
      }),
    );

    const result = await fetchPackageWorkItems([
      { pkg: "azure-core", lang: "Python" },
    ]);
    expect(result).toBeInstanceOf(Map);
    expect(result.size).toBe(0);
  });
});

describe("fetchReleasedPackageCsvs", () => {
  test("returns empty map when all fetches fail", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockRejectedValue(new Error("Network error")),
    );
    const result = await fetchReleasedPackageCsvs();
    expect(result.size).toBe(0);
  });

  test("returns empty map when response is not ok", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false }));
    const result = await fetchReleasedPackageCsvs();
    expect(result.size).toBe(0);
  });

  test("parses CSV content and builds map", async () => {
    const csvContent = `"Package","VersionGA","VersionPreview"
"Azure.Core","1.0.0","2.0.0-beta.1"
"Azure.Storage","","1.0.0-preview.3"`;
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        text: () => Promise.resolve(csvContent),
      }),
    );
    const result = await fetchReleasedPackageCsvs();
    // All 5 languages fetch the same CSV, so each lang contributes entries
    expect(result.size).toBeGreaterThan(0);
    // Check one language
    const coreEntry = result.get(".net|azure.core");
    expect(coreEntry).toEqual({ versionGA: "1.0.0" });
    const storageEntry = result.get(".net|azure.storage");
    expect(storageEntry).toEqual({ versionGA: "" });
  });

  test("skips CSV without Package header", async () => {
    const csvContent = `"Name","Version"
"SomePkg","1.0.0"`;
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        text: () => Promise.resolve(csvContent),
      }),
    );
    const result = await fetchReleasedPackageCsvs();
    expect(result.size).toBe(0);
  });

  test("skips CSV with only header row", async () => {
    const csvContent = `"Package","VersionGA"`;
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        text: () => Promise.resolve(csvContent),
      }),
    );
    const result = await fetchReleasedPackageCsvs();
    expect(result.size).toBe(0);
  });

  test("skips rows with empty package name", async () => {
    const csvContent = `"Package","VersionGA"
"","1.0.0"
"Azure.Valid","2.0.0"`;
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        text: () => Promise.resolve(csvContent),
      }),
    );
    const result = await fetchReleasedPackageCsvs();
    // Only Azure.Valid should be in the map (for all 5 languages)
    const validEntry = result.get(".net|azure.valid");
    expect(validEntry).toEqual({ versionGA: "2.0.0" });
    // Empty package should not create an entry
    expect(result.has(".net|")).toBe(false);
  });

  test("handles missing VersionGA column gracefully", async () => {
    const csvContent = `"Package","VersionPreview"
"Azure.NoGA","1.0.0-beta.1"`;
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        text: () => Promise.resolve(csvContent),
      }),
    );
    const result = await fetchReleasedPackageCsvs();
    const entry = result.get(".net|azure.noga");
    expect(entry).toEqual({ versionGA: "" });
  });
});
